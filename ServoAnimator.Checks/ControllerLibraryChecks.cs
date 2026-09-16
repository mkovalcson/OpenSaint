using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerLibraryChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "j5-library-buttons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Library", "Commands"));
        Directory.CreateDirectory(Path.Combine(root, "Library", "Animation"));
        new SpeedCalibrationData { UseInUrdf = false }.Save(root);
        try
        {
            foreach (var kind in Enum.GetValues<ControllerKind>())
            {
                var profile = ControllerProfile.Empty(kind);
                for (int bank = 0; bank < 4; bank++)
                {
                    profile.Layers[bank]["A"] = new() { Target = "Library:sequence", LibraryName = "Wave.json", Loop = true };
                    profile.Layers[bank]["B"] = new() { Target = "Library:pose", LibraryName = "Rest.json" };
                }
                ControllerProfileStore.Save(root, profile);
                var saved = ControllerProfileStore.Load(root, kind);
                Check(saved.Layers.All(b => b["A"].Loop && b["A"].LibraryName == "Wave.json" && b["B"].Target == "Library:pose"), "Both device profiles preserve Library selections and loop flags in all banks.");
                for (int bank = 0; bank < 4; bank++)
                {
                    ControllerSample Sample(double a, double b) => new() { Connected = true, DeviceId = 5, LeftShoulder = (bank & 1) != 0, RightShoulder = (bank & 2) != 0, Values = new() { ["A"] = a, ["B"] = b } };
                    var engine = new ControllerInputEngine(); engine.Evaluate(saved, Sample(0, 0), 0.025, _ => 0);
                    var first = engine.Evaluate(saved, Sample(1, 0), 0.025, _ => 0).Single();
                    Check(first.Loop && first.LibraryName == "Wave.json", "Library button emits sequence and loop once.");
                    Check(engine.Evaluate(saved, Sample(1, 0), 0.025, _ => 0).Count == 0, "Holding a Library button does not restart playback.");
                    Check(engine.Evaluate(saved, Sample(1, 1), 0.025, _ => 0).Single().LibraryName == "Rest.json", "A different button interrupts even while the original button stays held.");
                }
                saved.Layers[0]["B"].Loop = true;
                bool rejected = false; try { saved.Validate(); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Only sequences may loop.");
            }
            var commands = new[] { new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = 10, OffsetSeconds = 0 },
                new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = 30, OffsetSeconds = 1 } };
            var run = new ControllerLibraryRun(commands, 1, true);
            Check(run.Advance(0).Single().NumericValue == 10, "Sequence fires time zero immediately.");
            Check(run.Advance(0.5).Count == 0, "Future commands wait for their time.");
            Check(run.Advance(0.5).Select(c => c.NumericValue).SequenceEqual(new[] { 30, 10 }) && run.Restarted && !run.Finished, "Loop dispatches endpoint before restarting time zero.");
            var once = new ControllerLibraryRun(commands, 1, false);
            Check(once.Advance(1).Count == 2 && once.Finished && once.Advance(10).Count == 0, "One-shot finishes once and emits no later commands.");

            AnimationDocument.SaveCommandsOnly(Path.Combine(root, "Library", "Animation", "Wave.json"), commands.ToList());
            AnimationDocument.SaveCommandsOnly(Path.Combine(root, "Library", "Animation", "Second.json"), new() {
                new() { Servo = ServoNames.NeckTurn, NumericValue = -10 },
                new() { Servo = ServoNames.NeckTurn, NumericValue = 20, OffsetSeconds = 0.5 } });
            AnimationDocument.SaveLibraryCommand(Path.Combine(root, "Library", "Commands", "Rest.json"), new[] { new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = -25 } });
            var app = new App();
            app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            app.InitializeComponent();
            var window = new MainWindow();
            ((RobotHeadView)window.FindName("EmbeddedHeadView")).SetServoConfiguration(ServoConfiguration.CreateDefault());
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
            object Call(string name, params object[] args)
            {
                try { return typeof(MainWindow).GetMethod(name, flags).Invoke(window, args); }
                catch (TargetInvocationException ex) { throw ex.InnerException; }
            }
            typeof(MainWindow).GetField("_folders", flags).SetValue(window, new FolderSettings { ConfigFolder = root, ProjectFolder = root });
            var original = Get("_doc");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Check(Get("_controllerLibraryRun") != null && (double)Call("ControllerCurrentValue", "Servo:NeckTurn") == 10, "Real editor starts Library sequence and applies time zero.");
            Call("TickControllerLibrary", (double?)1);
            Check(Get("_controllerLibraryRun") != null && (double)Call("ControllerCurrentValue", "Servo:NeckTurn") == 10, "Real editor loops back to initial pose.");
            Call("StartControllerLibrary", new ControllerIntent("Library:pose", 0, "Rest.json"));
            Check(Get("_controllerLibraryRun") == null && (double)Call("ControllerCurrentValue", "Servo:NeckTurn") == -25, "Pose replaces a loop immediately and cancels its scheduler.");
            Call("TickControllerLibrary", (double?)5);
            Check((double)Call("ControllerCurrentValue", "Servo:NeckTurn") == -25 && ReferenceEquals(original, Get("_doc")), "Stopped loop never overwrites replacement pose; editor document is preserved.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Second.json"));
            Check((double)Call("ControllerCurrentValue", "Servo:NeckTurn") == -10 && Get("_controllerLibraryRun") is ControllerLibraryRun { Loop: false }, "A different selected sequence replaces the loop immediately with its own loop setting.");
            Call("TickControllerLibrary", (double?)0.5);
            Check((double)Call("ControllerCurrentValue", "Servo:NeckTurn") == 20 && Get("_controllerLibraryRun") == null, "Replacement one-shot reaches its endpoint and stops.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Missing.json", true));
            Check(Get("_controllerLibraryRun") == null, "Missing replacement stops old playback and does not leave the old loop running.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Call("StopPlayback", true);
            Check(Get("_controllerLibraryRun") == null, "Existing Stop transport stops Library playback.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Call("ResetControllerMotion");
            Check(Get("_controllerLibraryRun") == null, "Focus/disconnect/disable reset cancels Library playback.");
            typeof(MainWindow).GetField("_controllerConfigOpen", flags).SetValue(window, true);
            void Preview(bool testing, params ControllerIntent[] intents) => Call("PreviewControllerConfiguration", intents, testing);
            Preview(true, new ControllerIntent("Servo:NeckTurn", 42));
            Check((double)Call("ControllerCurrentValue", "Servo:NeckTurn") == 42, "Config test applies numeric mappings to the model state.");
            Preview(false, new ControllerIntent("Servo:NeckTurn", 80));
            Check((double)Call("ControllerCurrentValue", "Servo:NeckTurn") == 42, "Unchecked or unfocused config testing does not apply controller movement.");
            Preview(true, new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Check(Get("_controllerLibraryRun") is ControllerLibraryRun { Loop: true }, "Config test can start a Library loop.");
            Preview(true, new ControllerIntent("Library:pose", 0, "Rest.json"));
            Check(Get("_controllerLibraryRun") == null && (double)Call("ControllerCurrentValue", "Servo:NeckTurn") == -25,
                "A config Library pose immediately replaces a test sequence.");
            Preview(true, new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Preview(false);
            Check(Get("_controllerLibraryRun") == null, "Ending config testing cancels the running loop.");
            Preview(true, new ControllerIntent("Action:Snapshot", 0));
            Check(ReferenceEquals(original, Get("_doc")), "Config testing preserves the open timeline.");
            typeof(MainWindow).GetField("_controllerConfigOpen", flags).SetValue(window, false);
            var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
            var visualQueue = (ControllerPreviewUpdates)Get("_controllerPreview");
            double priorVisual = head.CapturePose().NeckTurn;
            Call("ApplyControllerPosition", "Servo:NeckTurn", 17d);
            Call("ApplyControllerPosition", "Servo:NeckTurn", 23d);
            Check(head.CapturePose().NeckTurn == priorVisual && visualQueue.Count == 1,
                "Real controller movement updates logical values without applying every poll to the URDF.");
            visualQueue.Flush();
            Check(head.CapturePose().NeckTurn == 23, "The actual URDF receives the latest coalesced controller value.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Wave.json", true));
            Call("StartControllerLibrary", new ControllerIntent("Library:pose", 0, "Rest.json"));
            visualQueue.Flush();
            Check(head.CapturePose().NeckTurn == -25, "Queued Library animation cannot overwrite a replacement pose in the actual URDF.");
            Call("StartControllerLibrary", new ControllerIntent("Library:sequence", 0, "Second.json"));
            Call("TickControllerLibrary", (double?)0.5);
            visualQueue.Flush();
            Check(head.CapturePose().NeckTurn == 20, "Natural sequence completion retains the final queued URDF endpoint.");
            Call("ApplyControllerPosition", "Servo:NeckTurn", 80d);
            Call("ResetControllerMotion"); visualQueue.Flush();
            Check(head.CapturePose().NeckTurn == 20, "Controller reset discards movement waiting for its visual frame.");
            FocusControlRoutingChecks(window);
            var fpsClock = (System.Diagnostics.Stopwatch)typeof(RobotHeadView).GetField("_fpsClock", flags).GetValue(head);
            var fpsText = (System.Windows.Controls.TextBlock)typeof(RobotHeadView).GetField("_fps", flags).GetValue(head);
            var fpsFrames = (Queue<double>)typeof(RobotHeadView).GetField("_fpsFrameTimes", flags).GetValue(head);
            var measureFps = typeof(RobotHeadView).GetMethod("MeasureFps", flags);
            void RenderFps(int frame) => measureFps.Invoke(head, new object[] { null,
                Activator.CreateInstance(typeof(RenderingEventArgs), flags, null, new object[] { TimeSpan.FromSeconds(frame / 60.0) }, null) });
            RenderFps(0); fpsFrames.Clear();
            RenderFps(1);
            Check(fpsFrames.Count == 0, "Unrelated WPF render frames do not inflate URDF FPS.");
            var revision = typeof(RobotHeadView).GetField("_poseRevision", flags);
            revision.SetValue(head, (long)revision.GetValue(head) + 3);
            RenderFps(2); RenderFps(2); RenderFps(3);
            Check(fpsFrames.Count == 1, "Multiple joint updates and duplicate render callbacks count as one changed URDF frame.");
            var mouth = typeof(RobotHeadView).GetField("_lastMouthStep", flags);
            mouth.SetValue(head, (int)mouth.GetValue(head) + 1);
            RenderFps(4);
            Check(fpsFrames.Count == 2, "Mouth animation contributes to actual URDF update FPS.");
            fpsFrames.Clear();
            fpsText.Text = "fps: 60"; fpsClock.Start();
            typeof(RobotHeadView).GetMethod("RefreshFps", flags).Invoke(head, new object[] { null, EventArgs.Empty });
            Check(fpsText.Text == "fps: 0", "FPS refresh clears the old rate when no new frames have rendered.");
            typeof(RobotHeadView).GetMethod("StopFpsCounter", flags).Invoke(head, null);
            // Render the actual Hardware panel to verify small icons and device ordering.
            string previews = Path.Combine(Environment.CurrentDirectory, "controller-previews"); Directory.CreateDirectory(previews);
            var panel = (FrameworkElement)window.FindName("HardwareSection");
            panel.Measure(new Size(128, 650)); panel.Arrange(new Rect(0, 0, 128, 650)); panel.UpdateLayout();
            var focusButton = (FrameworkElement)window.FindName("FocusControlButton");
            var xboxButton = (FrameworkElement)window.FindName("XboxControllerButton");
            var deckButton = (FrameworkElement)window.FindName("StreamDeckButton");
            var steamButton = (FrameworkElement)window.FindName("SteamControllerButton");
            Check(focusButton.TranslatePoint(new Point(), panel).Y < deckButton.TranslatePoint(new Point(), panel).Y
                && deckButton.TranslatePoint(new Point(), panel).Y < xboxButton.TranslatePoint(new Point(), panel).Y
                && Math.Abs(deckButton.ActualWidth - xboxButton.ActualWidth) < 1
                && xboxButton.TranslatePoint(new Point(), panel).Y < steamButton.TranslatePoint(new Point(), panel).Y
                && steamButton.TranslatePoint(new Point(0, steamButton.ActualHeight), panel).Y > 620,
                "Focus Control, Stream Deck, Xbox and Steam appear in order; Stream Deck keeps the same button width.");
            var image = new RenderTargetBitmap(145, 670, 96, 96, PixelFormats.Pbgra32); image.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(previews, "HardwareControllerButtons.png")); encoder.Save(file);
        }
        finally { Directory.Delete(root, true); }
    }
}
