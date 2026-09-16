using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ServoAnimator;

internal static partial class Program
{
    private static void PlaybackPerformanceChecks(bool baseline)
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow(); // Unshown: no controllers, audio or hardware start.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        head.SetServoConfiguration(ServoConfiguration.CreateDefault());
        head.SetCollisionWarningsEnabled(false); head.SetUrdfDriveEnabled(true);
        var scene = (UrdfScene)typeof(RobotHeadView).GetField("_scene", flags).GetValue(head);
        var status = (TextBlock)typeof(RobotHeadView).GetField("_status", flags).GetValue(head);
        void Measure(string name, Action step, int count)
        {
            for (int i = 0; i < 30; i++) step();
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) step();
            clock.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Console.WriteLine($"{name}: {count} updates, {clock.Elapsed.TotalMilliseconds:0.00} ms CPU, {bytes:N0} bytes allocated");
        }
        int foregroundChanges = 0; Brush foreground = status.Foreground;
        for (int i = 0; i < 100; i++)
        {
            head.RefreshCollisionNow();
            if (!ReferenceEquals(status.Foreground, foreground)) foregroundChanges++;
            foreground = status.Foreground;
        }
        Console.WriteLine($"Warnings Off: {foregroundChanges} unnecessary legend brush replacements / 100 updates");
        if (!baseline) Check(foregroundChanges == 0, "Warnings Off performs no repeated legend brush writes");
        Measure("Warnings Off refresh", head.RefreshCollisionNow, 5000);

        head.SetSpeedCalibration(new() { UseInUrdf = false });
        head.SetServo(ServoNames.NeckTurn, 30);
        string before = JsonSerializer.Serialize(head.CapturePose());
        var evaluate = typeof(MainWindow).GetMethod("EvaluateUrdfCollisionPairsAt", flags);
        var pairs = (HashSet<string>)evaluate.Invoke(window, new object[] { 3.0 });
        bool poseUnchanged = before == JsonSerializer.Serialize(head.CapturePose());
        Console.WriteLine($"Warnings Off command evaluation preserves pose: {poseUnchanged}");
        if (!baseline) Check(pairs.Count == 0 && poseUnchanged, "Disabled command warnings skip pose evaluation entirely");
        Measure("Warnings Off command evaluation", () => evaluate.Invoke(window, new object[] { 3.0 }), 1000);

        // Timeline initializes channels for the entire model, unlike a stick
        // driving a single joint. Settled channels should consume no render work.
        head.SetSpeedCalibration(new()); head.SnapCalibratedMotion = true;
        foreach (var servo in Enum.GetValues<ServoNames>().Where(s => !ServoCommand.IsTextValued(s))) head.SetServo(servo, 0);
        head.SnapCalibratedMotion = false; head.StopCalibratedMotion();
        head.CalibratedMotionAllowed = null;
        Measure("Full-model settled motion", () => head.AdvanceCalibratedMotion(1.0 / 60), 5000);
        before = JsonSerializer.Serialize(head.CapturePose());
        long revision = (long)typeof(RobotHeadView).GetField("_poseRevision", flags).GetValue(head);
        head.AdvanceCalibratedMotion(1.0 / 60);
        Check(before == JsonSerializer.Serialize(head.CapturePose()) && revision == (long)typeof(RobotHeadView).GetField("_poseRevision", flags).GetValue(head),
            "Settled channels leave the rendered pose unchanged");
        head.SetServo(ServoNames.LeftEyePop, 2000);
        for (int i = 0; i < 60; i++) head.AdvanceCalibratedMotion(1.0 / 60);
        Check(Math.Abs(head.CapturePose().LeftEyePop - 2000 / 1.1) < .1 && head.CapturePose().RightEyePop == 0,
            "Moving channels retain their calibrated elapsed-time speed while stationary channels hold");

        // Repeated warnings for an unchanged collision must not remove and
        // reapply the same materials, invalidating the full scene each time.
        head.SetSpeedCalibration(new() { UseInUrdf = false });
        head.SetServo(ServoNames.BothEyePop, 0);
        head.SetCollisionWarningsEnabled(true);
        bool found = false;
        var random = new Random(190519);
        for (int pose = 0; pose < 96; pose++)
        {
            foreach (var joint in new[] { "NoseBody", "NoseBasket", "LeftLensHorizontal", "RightLensHorizontal", "LeftLensVertical", "RightLensVertical",
                "BrowLeftTopOpen", "BrowRightTopOpen", "BrowLeftBottomOpen", "BrowRightBottomOpen", "BrowLeftTopTilt", "BrowRightTopTilt" })
                scene.SetJoint(joint, random.NextDouble() * 2 - 1);
            head.RefreshCollisionNow();
            if (scene.HasActiveCollision) { found = true; break; }
        }
        Check(found, "Regression fixture contains an actual colliding pose");
        var highlighted = ((HashSet<GeometryModel3D>)typeof(UrdfScene).GetField("_highlightedVisuals", flags).GetValue(scene)).ToArray();
        int visualChanges = 0; EventHandler changed = (_, _) => visualChanges++;
        foreach (var visual in highlighted) visual.Changed += changed;
        var expectedPairs = scene.ActiveCollisionPairs.ToHashSet();
        head.RefreshCollisionNow();
        foreach (var visual in highlighted) visual.Changed -= changed;
        Console.WriteLine($"Unchanged collision: {visualChanges} material notifications / refresh");
        if (!baseline) Check(visualChanges == 0, "Unchanged collision highlights do not repaint materials");
        Check(expectedPairs.SetEquals(scene.ActiveCollisionPairs), "Warning optimization retains the collision pairs");
        head.SetCollisionWarningsEnabled(false);
        Check(!scene.HasActiveCollision && ((HashSet<GeometryModel3D>)typeof(UrdfScene).GetField("_highlightedVisuals", flags).GetValue(scene)).Count == 0,
            "Turning warnings off clears existing highlights immediately");
    }
}
