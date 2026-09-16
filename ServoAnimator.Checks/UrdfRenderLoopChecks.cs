using System.Reflection;
using System.Windows;
using ServoAnimator;

internal static partial class Program
{
    private static void UrdfRenderLoopChecks()
    {
        foreach (int displayHz in new[] { 15, 24, 30, 60, 75, 120, 144, 240 })
        {
            var loop = new UrdfRenderLoop(false);
            int targets = 0, motions = 0;
            loop.AddMotion(_ => { Check(targets == motions + 1, "Fresh targets precede motion regardless of registration order"); motions++; });
            loop.AddTargets(_ => targets++);
            for (int frame = 0; frame < displayHz * 2; frame++)
            {
                var now = TimeSpan.FromSeconds(frame / (double)displayHz);
                loop.Advance(now);
                Check(!loop.Advance(now), "Repeated WPF render notification cannot apply a duplicate frame");
            }
            Check(Math.Abs(motions - Math.Min(displayHz, 60) * 2) <= 1 && targets == motions,
                $"Synchronized visual cadence follows a {displayHz} Hz display without exceeding its 60 Hz target");
            int before = motions;
            loop.Advance(TimeSpan.FromSeconds(5));
            Check(motions == before + 1, "A delayed frame does one update without a catch-up burst");
        }
        ControllerPreviewChecks();

        var app = new App(); app.InitializeComponent();
        var head = new RobotHeadView();
        head.SetServoConfiguration(ServoConfiguration.CreateDefault()); head.SetCollisionWarningsEnabled(false);
        head.SetUrdfDriveEnabled(true);
        void Reset()
        {
            head.SetSpeedCalibration(new()); head.CalibratedMotionAllowed = null; head.CalibratedMotionPaused = false;
            head.SnapCalibratedMotion = true; head.SetServo(ServoNames.BothEyePop, 0); head.SnapCalibratedMotion = false;
        }
        foreach (int displayHz in new[] { 15, 30, 60, 144 })
        {
            Reset();
            var loop = new UrdfRenderLoop(false);
            var preview = new ControllerPreviewUpdates();
            loop.AddMotion(head.RenderCalibratedMotion);
            loop.AddTargets(_ => preview.Flush());
            preview.Enqueue("pop", () => head.SetServo(ServoNames.BothEyePop, 2000));
            loop.Advance(TimeSpan.Zero);
            for (int frame = 1; frame <= displayHz; frame++) loop.Advance(TimeSpan.FromSeconds(frame / (double)displayHz));
            Check(Math.Abs(head.CapturePose().LeftEyePop - 2000 / 1.1) < .01,
                $"Calibrated travel depends on elapsed time, not {displayHz} Hz display rate");
        }

        Reset();
        var frames = new UrdfRenderLoop(false);
        var queued = new ControllerPreviewUpdates();
        frames.AddMotion(head.RenderCalibratedMotion); frames.AddTargets(_ => queued.Flush());
        frames.Advance(TimeSpan.Zero);
        queued.Enqueue("pop", () => head.SetServo(ServoNames.BothEyePop, 0));
        queued.Enqueue("pop", () => head.SetServo(ServoNames.BothEyePop, 2000));
        frames.Advance(TimeSpan.FromSeconds(.1));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 200 / 1.1) < .01, "Newest controller target affects motion in the same rendered frame");
        frames.Advance(TimeSpan.FromSeconds(.55));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1000) < .01, "Dropped frames preserve calibrated wall-clock speed");
        head.CalibratedMotionPaused = true; frames.Advance(TimeSpan.FromSeconds(10));
        head.CalibratedMotionPaused = false; frames.Advance(TimeSpan.FromSeconds(20));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1000) < .01, "Paused time is not integrated on resume");
        frames.Advance(TimeSpan.FromSeconds(20.11));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1200) < .01, "Motion resumes at its calibrated speed");

        bool allowed = false; head.CalibratedMotionAllowed = () => allowed;
        head.RefreshCalibratedMotionPermission(); // Background input polling observes the loss of permission.
        allowed = true; head.RefreshCalibratedMotionPermission();
        frames.Advance(TimeSpan.FromSeconds(40));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1200) < .01, "Focus transitions while rendering is suspended do not advance forbidden motion");
        frames.Advance(TimeSpan.FromSeconds(40.11));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1400) < .01, "Motion continues normally after focus permission returns");
        head.StopCalibratedMotion(); frames.Advance(TimeSpan.FromSeconds(50)); frames.Advance(TimeSpan.FromSeconds(51));
        Check(Math.Abs(head.CapturePose().LeftEyePop - 1400) < .01, "Stopped motion cannot be replayed by later frames");

        int participants = UrdfRenderLoop.Current.ParticipantCount;
        head.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        head.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Check(UrdfRenderLoop.Current.ParticipantCount == participants + 1, "Loading or docking a view cannot double-subscribe motion");
        head.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check(UrdfRenderLoop.Current.ParticipantCount == participants, "Unloading a view releases its render subscription");
        head.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Check(UrdfRenderLoop.Current.ParticipantCount == participants + 1, "Reloading a view restores its render subscription");
        head.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        // Exercise the actual render path with a blocked move rather than
        // bypassing the collision guard or emitting physical hardware output.
        Reset();
        head.SnapCalibratedMotion = true;
        head.SetServo(ServoNames.FlapTiltUp, 0); head.SetServo(ServoNames.FlapsOpen, 0);
        head.SnapCalibratedMotion = false;
        ServoCommand blocked = null;
        foreach (var servo in new[] { ServoNames.FlapTiltUp, ServoNames.FlapsOpen })
        foreach (int value in new[] { -100, 100 })
        {
            var command = new ServoCommand { Servo = servo, NumericValue = value };
            if (!head.ControllerPathClear(new[] { command }, out _)) { blocked = command; break; }
        }
        Check(blocked != null, "A real flap path exercises collision rejection");
        var scene = (UrdfScene)typeof(RobotHeadView).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(head);
        var joints = scene.CaptureMotionState().JointPositions;
        bool stopped = false;
        head.CollisionSafeguardActive = () => true; head.CollisionSafeguardBlocked = _ => stopped = true;
        head.SetServo(blocked.Servo, blocked.NumericValue);
        var guarded = new UrdfRenderLoop(false); guarded.AddMotion(head.RenderCalibratedMotion);
        guarded.Advance(TimeSpan.Zero); guarded.Advance(TimeSpan.FromSeconds(60));
        var after = scene.CaptureMotionState().JointPositions;
        Check(stopped && joints.All(p => Math.Abs(p.Value - after[p.Key]) < 1e-9),
            "Even a very late rendered frame rejects collision before changing the URDF: stopped=" + stopped + "; changed=" +
            string.Join(", ", joints.Where(p => Math.Abs(p.Value - after[p.Key]) >= 1e-9).Select(p => $"{p.Key}: {p.Value} -> {after[p.Key]}")));
    }
}
