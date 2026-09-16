using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private sealed class FakeMaestro : IMaestroCalibrationPort
    {
        internal double Now, Pulse = 1500, TargetPulse = 1500, Rate = 100;
        internal bool Stuck;
        internal Action TargetSent;
        internal readonly List<(int Channel, int Target)> Targets = new();
        internal readonly List<(int Speed, int Accel)> Profiles = new();
        public void Configure(int channel, int speed, int acceleration) { Profiles.Add((speed, acceleration)); Rate = speed == 0 ? double.PositiveInfinity : speed * 25; }
        public void Target(int channel, int quarterMicroseconds)
        {
            Targets.Add((channel, quarterMicroseconds)); TargetPulse = quarterMicroseconds / 4d;
            if (quarterMicroseconds == 0 || double.IsPositiveInfinity(Rate)) Pulse = TargetPulse;
            TargetSent?.Invoke();
        }
        public int Position(int channel) => (int)Math.Round(Pulse * 4);
        internal void Sleep(int milliseconds)
        {
            Now += milliseconds / 1000d;
            if (!Stuck) Pulse += Math.Sign(TargetPulse - Pulse) * Math.Min(Math.Abs(TargetPulse - Pulse), Rate * milliseconds / 1000d);
        }
    }
    private sealed class ImmediateProgress(Action<SpeedCalibrationProgress> report) : IProgress<SpeedCalibrationProgress>
    { public void Report(SpeedCalibrationProgress value) => report(value); }

    private static void SpeedCalibrationChecks()
    {
        PredictedCalibrationChecks();
        ServoSpecificFallbackChecks();
        bool Reject<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }
        void Near(double value, double expected, double tolerance, string message) => Check(Math.Abs(value - expected) <= tolerance, $"{message}: {value} vs {expected}");
        var config = ServoConfiguration.CreateDefault();
        var entry = config.Get(RobotControls.NeckTurn);
        entry.MinPwm = 1000; entry.MaxPwm = 2000; entry.DefaultPwm = 1500; entry.Reversed = false;
        entry.Speeds = new[] { 4, 1, 0, 2 }; entry.Accels = new[] { 0, 0, 1, 4 };
        var data = new SpeedCalibrationData();
        Near(data.ConfiguredLimits(entry, ServoSpeed.Default).Speed, 100, 1e-9, "20 ms Maestro speed units");
        Near(data.ConfiguredLimits(entry, ServoSpeed.Fast).Acceleration, 312.5, 1e-9, "Maestro acceleration units");
        Check(double.IsPositiveInfinity(data.ConfiguredLimits(entry, ServoSpeed.Fast).Speed), "Zero speed is unlimited even with finite acceleration.");
        Check(data.ConfiguredLimits(entry, ServoSpeed.Fast).TraverseSeconds(100) > 1, "Zero speed plus acceleration still takes time.");
        data.PeriodMs = 10; Near(data.ConfiguredLimits(entry, ServoSpeed.Default).Speed, 100, 1e-9, "Mini 10 ms uses period as tick");
        data.PeriodMs = 40; Near(data.ConfiguredLimits(entry, ServoSpeed.Default).Speed, 50, 1e-9, "Mini 40 ms uses half-period tick");
        data.MiniMaestro = false; Near(data.ConfiguredLimits(entry, ServoSpeed.Default).Speed, 100, 1e-9, "Micro has fixed 10 ms tick");
        data.MiniMaestro = true; data.PeriodMs = 20;
        entry.Speeds[0] = -1;
        Check(data.Limits(entry, ServoSpeed.Default, true).Speed == 0, "Malformed profiles hold the model without crashing its render timer");
        entry.Speeds[0] = 4;
        var trapezoid = new MotionLimits(100, 200);
        Near(trapezoid.TraverseSeconds(200), 2.5, 1e-9, "Long move accelerates, cruises and brakes");
        Near(trapezoid.TraverseSeconds(8), .4, 1e-9, "Short move is triangular");
        Near(MotionLimits.FitSpeed(200, 2.5, 200), 100, 1e-9, "Measured traverse fits acceleration-aware speed");
        var positions = new List<double>();
        foreach (var hz in new[] { 15, 30, 60, 144 })
        {
            var motion = new ServoMotionState(0) { Target = 200 };
            for (int i = 0; i < hz; i++) motion.Advance(1d / hz, trapezoid);
            Near(motion.Position, 75, .1, $"Elapsed-time motion at {hz} fps"); positions.Add(motion.Position);
            motion.Advance(2, trapezoid); Near(motion.Position, 200, .01, "Move reaches target without overshoot");
        }
        Check(positions.Max() - positions.Min() < .01, "Frame cadence does not materially change servo travel");
        var reversal = new ServoMotionState(1500) { Target = 2000 };
        reversal.Advance(.3, trapezoid); double beforeReverse = reversal.Position;
        reversal.Target = 1000; reversal.Advance(.01, trapezoid, 1000, 2000);
        Check(reversal.Position > beforeReverse, "Acceleration-limited reversal retains momentum initially");
        reversal.Advance(20, trapezoid, 1000, 2000); Near(reversal.Position, 1000, .001, "Reversal remains within physical bounds");

        var plan = new SpeedCalibrationPlan(1500, 1600, 2, true, new[] { ServoSpeed.Default }, false, true, 200);
        var fake = new FakeMaestro(); var completed = new List<SpeedCalibrationResult>();
        var measured = SpeedCalibrationRunner.Run(fake, entry, data, plan, "fake", ServoSpeed.Slow,
            new ImmediateProgress(p => { if (p.Completed != null) completed.Add(p.Completed); }), CancellationToken.None, () => fake.Now, fake.Sleep);
        Check(measured.Count == 8 && completed.Count == 8, "Two repeats include both directions and short traverses");
        Near(measured[0].Seconds, 1, .011, "Runner timestamps commanded full travel");
        Near(measured[2].Seconds, .25, .011, "Runner timestamps short travel separately");
        Check(measured.All(r => r.Samples.Count > 4 && r.Fingerprint == data.Fingerprint(entry)), "Samples and configuration fingerprint are retained");
        Check(fake.Targets.All(t => t.Channel == entry.MaestroPort && t.Target is >= 6000 and <= 6400) && fake.Targets[^1].Target == 6000, "Only reviewed channel/range is driven and ends at start");
        Check(fake.Profiles[^1] == (1, 0), "Previous active speed/acceleration profile is restored");
        var hold = new FakeMaestro();
        HardwareManager.HoldMaestroMotion(hold, config, new Dictionary<RobotControls, ServoSpeed>());
        Check(hold.Targets.Count == 24 && hold.Targets.All(t => t.Target == 6000), "Collision hold requests the current output pulse on every Maestro channel without releasing torque");
        var disabledHold = new FakeMaestro { Pulse = 0 };
        HardwareManager.HoldMaestroMotion(disabledHold, config, new Dictionary<RobotControls, ServoSpeed>());
        Check(disabledHold.Targets.Count == 0 && disabledHold.Profiles.Count == 0, "Collision hold does not energize previously disabled channels");
        Check(MaestroCalibrationPort.Command(0x84, 22, 0).SequenceEqual(new byte[] { 0x84, 22, 0, 0 }), "Disable is Set Target zero");
        Check(MaestroCalibrationPort.Command(0x84, 22, 6000).SequenceEqual(new byte[] { 0x84, 22, 112, 46 }), "Targets use quarter-microseconds and seven-bit payload bytes");
        fake = new FakeMaestro { Pulse = 0 };
        Check(Reject<InvalidOperationException>(() => SpeedCalibrationRunner.Run(fake, entry, data, plan, "fake", ServoSpeed.Default, null, CancellationToken.None)), "Unpowered servo fails preflight");
        Check(fake.Targets.Count == 0 && fake.Profiles.Count == 0, "Failed preflight sends no output initialization");
        Check(Reject<InvalidOperationException>(() => SpeedCalibrationRunner.Validate(entry, plan with { FromPwm = 900 })), "Out-of-range test is rejected");
        Check(Reject<InvalidOperationException>(() => SpeedCalibrationRunner.Validate(entry, plan with { PreparedAtStart = false })), "Preparation requires explicit confirmation");
        foreach (bool release in new[] { false, true })
        {
            fake = new FakeMaestro(); using var cancel = new CancellationTokenSource(); fake.TargetSent = () => cancel.Cancel();
            Check(Reject<OperationCanceledException>(() => SpeedCalibrationRunner.Run(fake, entry, data, plan with { DisableOnAbort = release }, "fake", ServoSpeed.Slow, null, cancel.Token, () => fake.Now, fake.Sleep)), "Abort cancels a run");
            Check(fake.Targets[^1].Target == (release ? 0 : 6000) && fake.Profiles[^1] == (1, 0), "Abort applies selected hold/release then restores profile");
        }
        fake = new FakeMaestro { Stuck = true };
        Check(Reject<TimeoutException>(() => SpeedCalibrationRunner.Run(fake, entry, data, plan, "fake", ServoSpeed.Default, null, CancellationToken.None, () => fake.Now, fake.Sleep)), "Stuck output times out");
        data.Results.AddRange(measured);
        Near(data.Limits(entry, ServoSpeed.Default, true).Speed, 100, 1.1, "Controller measurement informs URDF timing");
        data.Physical.Add(new() { Control = entry.Control, CeilingUsPerSecond = 50, Notes = "Observed under load" });
        Near(data.Limits(entry, ServoSpeed.Default, true).Speed, 50, .001, "Physical estimate caps simulated speed");
        measured[0].PhysicalSeconds = 4;
        Near(data.Limits(entry, ServoSpeed.Default, true).Speed, 25, .001, "Physical timing can further reduce simulated speed");
        entry.Speeds[0] = 1;
        data.Physical.Clear();
        Near(data.Limits(entry, ServoSpeed.Default, true).Speed, 25, .001, "Changed settings exclude stale measurements");
        entry.Speeds[0] = 4;
        foreach (var parent in new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp, ServoNames.NeckTiltRight, ServoNames.IrisClose })
        foreach (var control in ServoConfiguration.ControlsFor(parent))
        foreach (bool reverse in new[] { false, true })
        {
            var map = config.Get(control); map.Reversed = reverse;
            var hardware = new MaestroServo(map, "unused");
            foreach (int value in new[] { -100, -35, 0, 40, 100 }.Where(v => v >= ServoCommand.RangeFor(parent).Min))
            {
                double pulse = ServoPulseMapping.ToPulse(config, parent, map, value);
                Near(Math.Round(pulse), hardware.MapDelta(value, config.GangReversed(parent, control), ServoCommand.RangeFor(parent).Min < 0), 0, "URDF pulse mapping matches physical mapping");
                Near(ServoPulseMapping.ToPulse(config, parent, map, ServoPulseMapping.ToLogical(config, parent, map, pulse)), pulse, .001, "Pulse/logical round trip respects reversals");
            }
        }
        entry.Reversed = false;
        string root = Path.Combine(Path.GetTempPath(), "ServoSpeedChecks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            data.Save(root); var loaded = SpeedCalibrationData.Load(root);
            Check(loaded.Results.Count == 8 && loaded.Results[0].Samples.Count == measured[0].Samples.Count, "Samples persist and reload");
            Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "Atomic save leaves no temporary files");
            data.Results[0].PhysicalSeconds = double.NaN;
            Check(Reject<InvalidDataException>(() => data.Save(root)), "Invalid physical times cannot overwrite saved data");
            Check(SpeedCalibrationData.Load(root).Results[0].PhysicalSeconds == 4, "Failed save preserves prior calibration");
            data.Results.Clear(); data.UseInUrdf = true; data.Save(root);
            SpeedCalibrationUiChecks(config, data, root);
        }
        finally { Directory.Delete(root, true); }
    }

    private static void SpeedCalibrationUiChecks(ServoConfiguration config, SpeedCalibrationData data, string root)
    {
        var app = new App(); app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); app.InitializeComponent();
        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        object Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, values);
        Set("_folders", new FolderSettings { ConfigFolder = root, ProjectFolder = root });
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView"); head.SetServoConfiguration(config); head.SetCollisionWarningsEnabled(false);
        head.SetSpeedCalibration(data); head.SnapCalibratedMotion = true; head.SetServo(ServoNames.NeckTurn, 0); head.SnapCalibratedMotion = false;
        head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.Default); head.SetServo(ServoNames.NeckTurn, 100);
        Check(head.CapturePose().NeckTurn == 0, "Calibrated target does not jump immediately");
        head.AdvanceCalibratedMotion(1); Check(Math.Abs(head.CapturePose().NeckTurn - 20) < .01, "Real URDF advances at configured speed");
        head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.Slow); head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.NoChange);
        head.AdvanceCalibratedMotion(1); Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "N/C preserves last speed on the real model");
        head.CalibratedMotionPaused = true; head.AdvanceCalibratedMotion(10); Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "Pause freezes visual movement");
        head.CalibratedMotionPaused = false; head.CalibratedMotionAllowed = () => false; head.AdvanceCalibratedMotion(10); Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "Focus policy gates ongoing visual movement");
        head.CalibratedMotionAllowed = null; head.StopCalibratedMotion(); head.AdvanceCalibratedMotion(10); Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "Stop cancels the remaining target");
        head.SetCalibratedControlEnabled(ServoNames.NeckTurn, null, false); head.SetServo(ServoNames.NeckTurn, -100); head.AdvanceCalibratedMotion(10); Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "Disabled channel holds visual position");
        head.SetCalibratedControlEnabled(ServoNames.NeckTurn, null, true); head.SnapCalibratedMotion = true; head.SetServo(ServoNames.NeckTurn, -50); head.SnapCalibratedMotion = false;
        Check(head.CapturePose().NeckTurn == -50, "Seek snaps to selected authored pose");
        head.SetSpeedCalibration(new() { UseInUrdf = false }); head.SetServo(ServoNames.NeckTurn, 60); Check(head.CapturePose().NeckTurn == 60, "Disabling calibrated motion restores immediate preview");
        Set("_speedCalibrationBusy", true);
        Check(!(bool)Call("ControllerOutputAllowed", true) && !(bool)Call("PlaybackOutputAllowed", true), "Calibration excludes controller and timeline hardware output");
        window.MoveServoNow(ServoSpeed.Default, ServoNames.NeckTurn, 0); Check(head.CapturePose().NeckTurn == 60, "Calibration excludes direct jogs");
        Set("_speedCalibrationBusy", false);
        // Route real timeline and Library requests through MainWindow with a
        // temporary configuration root. Hardware remains disconnected.
        var editorConfig = (ServoConfiguration)typeof(MainWindow).GetField("_servoConfig", flags).GetValue(window);
        var editorEntry = editorConfig.Get(RobotControls.NeckTurn);
        editorEntry.MinPwm = 1000; editorEntry.MaxPwm = 2000; editorEntry.DefaultPwm = 1500; editorEntry.Reversed = false;
        editorEntry.Speeds = new[] { 4, 1, 0, 2 }; editorEntry.Accels = new int[4];
        head.SetServoConfiguration(editorConfig);
        var doc = (AnimationDocument)typeof(MainWindow).GetField("_doc", flags).GetValue(window);
        doc.Commands = new() { new() { Servo = ServoNames.NeckTurn, NumericValue = 0, Speed = ServoSpeed.Default },
            new() { Servo = ServoNames.NeckTurn, NumericValue = 100, Speed = ServoSpeed.NoChange, OffsetSeconds = 1 },
            new() { Servo = ServoNames.NeckTurn, Control = RobotControls.NeckTurn, NumericValue = 100, Speed = ServoSpeed.Slow, OffsetSeconds = 2 } };
        Call("RebuildPlaybackIndexes"); Call("SetCursor", 0d);
        Set("_cursorTime", 1d); Call("UpdateServoState", 1d); head.AdvanceCalibratedMotion(1);
        Check(Math.Abs(head.CapturePose().NeckTurn - 20) < .01, "Timeline N/C target uses last explicit profile");
        Set("_cursorTime", 2d); Call("UpdateServoState", 2d); head.AdvanceCalibratedMotion(1);
        Check(Math.Abs(head.CapturePose().NeckTurn - 25) < .01, "Timeline child profile overrides parent speed on the physical channel");
        Call("SetCursor", 1d); Check(head.CapturePose().NeckTurn == 100, "MainWindow seek snaps calibrated model to authored endpoint");
        string library = Path.Combine(root, "Library", "Commands"); Directory.CreateDirectory(library);
        AnimationDocument.SaveLibraryCommand(Path.Combine(library, "TimedPose.json"), new[] { new ServoCommand { Servo = ServoNames.NeckTurn, Speed = ServoSpeed.Default, NumericValue = 0 } });
        Call("StartControllerLibrary", new ControllerIntent("Library:pose", 0, "TimedPose.json"));
        var preview = (ControllerPreviewUpdates)typeof(MainWindow).GetField("_controllerPreview", flags).GetValue(window); preview.Flush();
        Check(head.CapturePose().NeckTurn == 100, "Library pose starts a timed target without jumping");
        head.AdvanceCalibratedMotion(1); Check(Math.Abs(head.CapturePose().NeckTurn - 80) < .01, "Library pose uses calibrated travel after its scheduler completes");
        Call("StopControllerLibrary", true); head.AdvanceCalibratedMotion(10);
        Check(Math.Abs(head.CapturePose().NeckTurn - 80) < .01, "Library Stop cancels residual servo movement");
        string animations = Path.Combine(root, "Library", "Animation"); Directory.CreateDirectory(animations);
        AnimationDocument.SaveCommandsOnly(Path.Combine(animations, "TimedSequence.json"), new() {
            new() { Servo = ServoNames.NeckTurn, NumericValue = 0, Speed = ServoSpeed.Slow },
            new() { Servo = ServoNames.NeckTurn, NumericValue = -100, Speed = ServoSpeed.NoChange, OffsetSeconds = .01 } });
        Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "TimedSequence.json")); Call("TickControllerLibrary", (double?).02); preview.Flush(); head.AdvanceCalibratedMotion(1);
        Check(Math.Abs(head.CapturePose().NeckTurn - 75) < .01, "Coalesced Library N/C target retains an earlier speed in the same display frame");
        Call("StopControllerLibrary", true);
        var dialog = new SpeedCalibrationWindow(config, data, root, (_, _, _, _) => Task.CompletedTask, () => { }, false);
        StepperMotionChecks();
        Check(dialog.Title == "Speed Calibration" && dialog.Content is TabControl tabs && tabs.Items.Count == 2, "Calibration window has Servos and Stepper Motors tabs");
        var content = (FrameworkElement)dialog.Content;
        content.Measure(new Size(1098, 790)); content.Arrange(new Rect(0, 0, 1098, 790)); content.UpdateLayout();
        var start = (Button)typeof(SpeedCalibrationWindow).GetField("_start", flags).GetValue(dialog);
        Check(!start.IsEnabled, "Disconnected calibration is view/edit only");
        var selector = (ComboBox)typeof(SpeedCalibrationWindow).GetField("_servo", flags).GetValue(dialog);
        var ceiling = (TextBox)typeof(SpeedCalibrationWindow).GetField("_ceiling", flags).GetValue(dialog);
        var first = (RobotControls)selector.SelectedItem; ceiling.Text = "123"; selector.SelectedIndex = 1;
        Check(data.Physical.Any(p => p.Control == first && p.CeilingUsPerSecond == 123), "Switching servo preserves physical estimate edits");
        data.Results.Add(new() { Control = RobotControls.NeckTurn, Setting = ServoSpeed.Default, Fingerprint = data.Fingerprint(config.Get(RobotControls.NeckTurn)),
            FromPwm = 1500, ToPwm = 1600, Seconds = 1, MeasuredUtc = DateTime.UtcNow,
            Samples = Enumerable.Range(0, 101).Select(i => new RampSample(i / 100d, 1500 + i)).ToList() });
        selector.SelectedItem = RobotControls.NeckTurn;
        content.UpdateLayout(); content.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        string previews = Path.Combine(Environment.CurrentDirectory, "speed-calibration-previews"); Directory.CreateDirectory(previews);
        var bitmap = new RenderTargetBitmap(1098, 790, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(previews, "calibration.png"))) png.Save(file);
        ((TabControl)dialog.Content).SelectedIndex = 1;
        RenderControl((FrameworkElement)dialog.Content, Path.Combine(previews, "steppers.png"), 1098, 790);
        dialog.Close();
        Call("ServoConfigurationSaved");
        var saved = SpeedCalibrationData.Load(root);
        Check(saved.Results.Count(r => r.IsPredicted) == 192, "Servo Configuration Save populates all predicted profiles");
        editorEntry.Speeds[0] = 8;
        Call("ServoConfigurationSaved");
        saved = SpeedCalibrationData.Load(root);
        Check(saved.Results.Single(r => r.IsPredicted && r.Control == RobotControls.NeckTurn && r.Setting == ServoSpeed.Default && r.ToPwm > r.FromPwm).Seconds == 5,
            "Saving changed servo speed regenerates the persisted prediction");
        // Never show or close the real editor here: Loaded/Closing restore or save user state.
    }

    private static void StepperMotionChecks()
    {
        var data = new SpeedCalibrationData();
        Check(data.Steppers.Count == 2 && data.Steppers.All(s => s.ExtendSeconds == 1.1 && s.RetractSeconds == 1.1), "Both eye-pop channels default to 1.1 seconds in each direction");
        var view = new RobotHeadView(); view.SetServoConfiguration(ServoConfiguration.CreateDefault()); view.SetSpeedCalibration(data); view.SetCollisionWarningsEnabled(false);
        view.SetServo(ServoNames.BothEyePop, 2000);
        Check(view.CapturePose().LeftEyePop == 0 && view.CapturePose().RightEyePop == 0, "Eye-pop targets do not snap immediately");
        view.AdvanceCalibratedMotion(.55);
        Check(Math.Abs(view.CapturePose().LeftEyePop - 1000) < .001 && Math.Abs(view.CapturePose().RightEyePop - 1000) < .001, "Both eye pops advance halfway in 0.55 seconds");
        view.AdvanceCalibratedMotion(.55);
        Check(Math.Abs(view.CapturePose().LeftEyePop - 2000) < .001, "Full eye-pop extension completes in 1.1 seconds");
        data.Steppers.Single(s => s.Control == RobotControls.RightEyePop).RetractSeconds = 2.2;
        view.SetServo(ServoNames.BothEyePop, 0); view.AdvanceCalibratedMotion(.55);
        Check(Math.Abs(view.CapturePose().LeftEyePop - 1000) < .001 && Math.Abs(view.CapturePose().RightEyePop - 1500) < .001, "Independent retraction settings control each eye-pop channel");
        view.StopCalibratedMotion(); view.AdvanceCalibratedMotion(5);
        Check(Math.Abs(view.CapturePose().RightEyePop - 1500) < .001, "Stop cancels remaining stepper motion");
        view.SnapCalibratedMotion = true; view.SetServo(ServoNames.BothEyePop, 0); view.SnapCalibratedMotion = false;
        Check(view.CapturePose().RightEyePop == 0, "Seeking still snaps eye pops to the authored position");
        data.Steppers[0].ExtendSeconds = double.NaN;
        bool rejected = false; try { data.Validate(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid stepper times are rejected before saving");
    }

    private static void ServoSpecificFallbackChecks()
    {
        var config = ServoConfiguration.CreateDefault();
        var data = new SpeedCalibrationData();
        foreach (var (control, seconds) in new[] {
            (RobotControls.NoseBody, .10), (RobotControls.NoseBasket, .10),
            (RobotControls.LeftIris, .11), (RobotControls.RightIris, .11),
            (RobotControls.LeftEyeVent, .11), (RobotControls.RightEyeVent, .11),
            (RobotControls.NeckTurn, .15) })
        {
            var entry = config.Get(control);
            entry.MinPwm = 1000; entry.MaxPwm = 2000;
            entry.Speeds = new[] { 0, 1, 0, 2 }; entry.Accels = new[] { 0, 0, 1, 4 };
            var legacy = new SpeedCalibrationResult { IsPredicted = true, Control = control, Setting = ServoSpeed.Default,
                Fingerprint = data.Fingerprint(entry), FromPwm = 1000, ToPwm = 2000, Seconds = .45, PredictionBasis = "Estimated 0.15 s / 60°" };
            data.Results.Add(legacy); data.RegeneratePredictions(config);
            foreach (bool increasing in new[] { true, false })
            {
                Check(Math.Abs(data.Limits(entry, ServoSpeed.Default, increasing).TraverseSeconds(1000d / 3) - seconds) < 1e-9, $"{control}: zero/zero uses requested 60-degree duration in both directions");
                var row = data.Results.Single(r => r.IsPredicted && r.Control == control && r.Setting == ServoSpeed.Default && (r.ToPwm > r.FromPwm) == increasing);
                Check(Math.Abs(row.Seconds - seconds * 3) < 1e-9 && row.Evidence == "Estimated " + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s / 60°", $"{control}: existing predictions refresh duration and evidence");
            }
            Check(data.Limits(entry, ServoSpeed.Slow, true).Speed == 25 && double.IsPositiveInfinity(data.Limits(entry, ServoSpeed.Fast, true).Speed), "Servo-specific fallback only applies to zero speed AND zero acceleration");
            data.Physical.Add(new() { Control = control, CeilingUsPerSecond = 100 });
            Check(data.Limits(entry, ServoSpeed.Default, true).Speed == 100, "Servo-specific fallback respects explicit physical ceiling");
        }
    }

    private static void PredictedCalibrationChecks()
    {
        var config = ServoConfiguration.CreateDefault();
        var entry = config.Get(RobotControls.BrowLeftTopTilt);
        entry.MinPwm = 910; entry.MaxPwm = 1900; entry.Speeds = new[] { 40, 10, 0, 2 }; entry.Accels = new[] { 10, 5, 0, 3 };
        var data = new SpeedCalibrationData();
        Check(data.UseInUrdf, "Calibrated motion defaults on");
        Check(data.RegeneratePredictions(config) && data.Results.Count == 192, "24 Maestro servos receive four bidirectional predictions");
        Check(!data.RegeneratePredictions(config), "Unchanged configuration does not duplicate or replace predictions");
        foreach (var expected in new[] { (ServoSpeed.Default, 1.31), (ServoSpeed.Slow, 4.12), (ServoSpeed.Fast, .45), (ServoSpeed.Crawl, 19.8533333333) })
        {
            var rows = data.Results.Where(r => r.Control == entry.Control && r.Setting == expected.Item1).ToArray();
            Check(rows.Length == 2 && rows.All(r => Math.Abs(r.Seconds - expected.Item2) < 1e-8), "Brow tilt predictions include acceleration in both directions");
            Check(rows.All(r => r.IsPredicted && r.Evidence == (expected.Item1 == ServoSpeed.Fast ? "Estimated 0.15 s / 60°" : "Predicted from configuration") && r.Samples[0].Pulse == r.FromPwm && Math.Abs(r.Samples[^1].Pulse - r.ToPwm) < 1e-8), "Prediction samples and evidence match the reviewed endpoints");
        }
        var timed = data.Results.First(r => r.Control == entry.Control && r.Setting == ServoSpeed.Slow);
        timed.PhysicalSeconds = 7;
        var measured = new SpeedCalibrationResult { Control = entry.Control, Setting = ServoSpeed.Slow, Fingerprint = data.Fingerprint(entry), FromPwm = 910, ToPwm = 1900, Seconds = 5, MeasuredUtc = DateTime.UtcNow };
        data.Results.Add(measured);
        data.Physical.Add(new() { Control = entry.Control, CeilingUsPerSecond = 200, Notes = "retain my estimate" });
        entry.Speeds[1] = 20; data.RegeneratePredictions(config);
        Check(data.Results.Contains(measured) && data.Results.Contains(timed) && timed.Fingerprint != data.Fingerprint(entry), "Regeneration preserves measured and physically timed evidence as stale");
        var regenerated = data.Results.Single(r => r.IsPredicted && r.Control == entry.Control && r.Setting == ServoSpeed.Slow && r.ToPwm > r.FromPwm && r.Fingerprint == data.Fingerprint(entry));
        Check(Math.Abs(regenerated.Seconds - 2.3) < 1e-8 && data.Physical[0].Notes == "retain my estimate", "New speed changes predicted duration and preserves physical settings");
        data.Physical.Clear();
        Check(data.Limits(entry, ServoSpeed.Slow, true).Speed == 500, "Predictions are not treated as measured calibration or stale physical timing");
        entry.Accels[1] = 10; data.RegeneratePredictions(config);
        Check(Math.Abs(data.Results.Single(r => r.IsPredicted && r.Control == entry.Control && r.Setting == ServoSpeed.Slow && r.ToPwm > r.FromPwm && r.Fingerprint == data.Fingerprint(entry)).Seconds - 2.14) < 1e-8, "Acceleration-only changes also regenerate predictions");
        data.PeriodMs = 40; data.RegeneratePredictions(config);
        Check(Math.Abs(data.Results.Single(r => r.IsPredicted && r.Control == entry.Control && r.Setting == ServoSpeed.Slow && r.ToPwm > r.FromPwm && r.Fingerprint == data.Fingerprint(entry)).Seconds - 4.28) < 1e-8, "Controller period participates in regeneration");
        var fast = data.Limits(entry, ServoSpeed.Fast, true);
        Check(Math.Abs(fast.TraverseSeconds(330) - .15) < 1e-9 && Math.Abs(fast.TraverseSeconds(990) - .45) < 1e-9, "Default 180-degree sweep takes 0.15 seconds per 60 degrees");
        Check(data.ConfiguredLimits(entry, ServoSpeed.Fast).TraverseSeconds(990) == 0, "Physical fallback leaves actual Maestro commanded limits unchanged");
        var motion = new ServoMotionState(910) { Target = 1900 };
        motion.Advance(.15, fast);
        Check(Math.Abs(motion.Position - 1240) < 1e-6, "Zero/zero preview advances only 60 degrees in 0.15 seconds");
        var old = data.Results.First(r => r.Control == entry.Control && r.Setting == ServoSpeed.Fast);
        old.Seconds = 0; old.PredictionBasis = "";
        Check(data.RegeneratePredictions(config) && !data.Results.Contains(old), "Legacy instantaneous predictions migrate even with unchanged servo fingerprint");
        var physical = new ServoPhysicalTiming { Control = entry.Control, AssumedTravelDegrees = 120 };
        data.Physical.Add(physical);
        string fingerprint = data.Fingerprint(entry);
        Check(data.RegeneratePredictions(config) && data.Fingerprint(entry) == fingerprint && Math.Abs(data.Limits(entry, ServoSpeed.Fast, false).TraverseSeconds(990) - .3) < 1e-9, "Editable shaft sweep changes fallback without invalidating controller measurements");
        Check(data.Results.Where(r => r.Control == entry.Control && r.Setting == ServoSpeed.Fast).All(r => Math.Abs(r.Seconds - .3) < 1e-9), "Updated sweep regenerates both prediction directions");
        physical.CeilingUsPerSecond = 9900;
        data.RegeneratePredictions(config);
        Check(Math.Abs(data.Limits(entry, ServoSpeed.Fast, true).TraverseSeconds(990) - .1) < 1e-9, "Explicit physical ceiling replaces the default assumption even when faster");
        physical.CeilingUsPerSecond = null;
        var physicalRow = data.Results.First(r => r.Control == entry.Control && r.Setting == ServoSpeed.Fast && r.ToPwm > r.FromPwm);
        physicalRow.PhysicalSeconds = .12;
        data.RegeneratePredictions(config);
        Check(data.Results.Contains(physicalRow) && Math.Abs(data.Limits(entry, ServoSpeed.Fast, true).TraverseSeconds(990) - .12) < 1e-9, "User physical timing is preserved and replaces fallback for its direction");
        Check(Math.Abs(data.Limits(entry, ServoSpeed.Fast, false).TraverseSeconds(990) - .3) < 1e-9 && !data.RegeneratePredictions(config), "Opposite direction retains fallback and regeneration is stable");
        data.Results.Clear(); data.Physical.Clear();
        entry.Accels[2] = 1;
        Check(double.IsPositiveInfinity(data.Limits(entry, ServoSpeed.Fast, true).Speed), "Zero speed with nonzero acceleration does not receive zero/zero fallback");
        entry.Accels[2] = 0;
        data.Results.Add(new() { Control = entry.Control, Setting = ServoSpeed.Fast, Fingerprint = data.Fingerprint(entry), FromPwm = 910, ToPwm = 1900, Seconds = .05 });
        Check(Math.Abs(data.Limits(entry, ServoSpeed.Fast, true).TraverseSeconds(990) - .45) < 1e-9, "Fast measured controller output cannot bypass the physical fallback");
    }
}
