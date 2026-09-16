using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media.Media3D;
using ServoAnimator;

internal static partial class Program
{
    private static void CollisionPerformanceChecks()
    {
        var app = new App(); app.InitializeComponent();
        var head = new RobotHeadView();
        head.SetServoConfiguration(ServoConfiguration.CreateDefault());
        head.SetSpeedCalibration(new() { UseInUrdf = false });
        head.SetUrdfDriveEnabled(true); head.SetCollisionWarningsEnabled(false);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var scene = (UrdfScene)typeof(RobotHeadView).GetField("_scene", flags).GetValue(head);
        Check(scene?.SafeguardAvailable == true, "Real collision model loaded");
        var reference = typeof(UrdfScene).GetMethod("DetectCollisionPairsReference", flags);
        HashSet<string> Reference(bool baseline, bool left, bool right) => ((IEnumerable)reference.Invoke(scene, new object[] { baseline, left, right }))
            .Cast<object>().Select(h => (string)h.GetType().GetProperty("PairKey").GetValue(h)).ToHashSet();
        HashSet<string> Numeric(UrdfScene.CollisionSession session, bool baseline, bool left, bool right)
        {
            var pairs = new HashSet<string>();
            session.VisitCollisions(baseline, left, right, p => pairs.Add(p.Key));
            return pairs;
        }
        var original = scene.CaptureMotionState();
        var random = new Random(190519);
        string[] rotating = { "NoseBody", "NoseBasket", "LeftLensHorizontal", "RightLensHorizontal", "LeftLensVertical", "RightLensVertical",
            "BrowLeftTopOpen", "BrowRightTopOpen", "BrowLeftBottomOpen", "BrowRightBottomOpen", "BrowLeftTopTilt", "BrowRightTopTilt", "NeckTurn" };
        var session = scene.CaptureCollisionSession();
        int hitPoses = 0, clearPoses = 0;
        for (int pose = 0; pose < 48; pose++)
        {
            foreach (string joint in rotating)
            {
                if (!original.JointPositions.ContainsKey(joint)) continue;
                double value = pose == 0 ? original.JointPositions[joint] : (random.NextDouble() - .5) * 2;
                scene.SetJoint(joint, value); session.SetJoint(joint, value);
            }
            foreach (string joint in new[] { "LeftEyePop", "RightEyePop" })
            {
                double value = pose == 0 ? original.JointPositions[joint] : random.NextDouble() * .1;
                scene.SetJoint(joint, value); session.SetJoint(joint, value);
            }
            for (int eyes = 0; eyes < 4; eyes++)
            foreach (bool baseline in new[] { false, true })
            {
                bool left = (eyes & 1) != 0, right = (eyes & 2) != 0;
                var expected = Reference(baseline, left, right);
                var actual = Numeric(session, baseline, left, right);
                Check(expected.SetEquals(actual), $"Numerical and rendered geometry agree for pose {pose}, eyes {eyes}, baseline {baseline}: expected {expected.Count}, got {actual.Count}");
                if (baseline)
                {
                    Check(session.HasCollision(left, right) == (expected.Count != 0), "Early-exit guard matches complete collision set");
                    if (expected.Count != 0) hitPoses++; else clearPoses++;
                }
            }
            if (pose % 8 == 0)
            {
                scene.UpdateCollisionState(true, true);
                Check(Reference(true, true, true).SetEquals(scene.ActiveCollisionPairs), "Warning highlights use the same cached contact pairs");
            }
        }
        Check(hitPoses > 0 && clearPoses > 0, "Comparison covers both clear and colliding poses");
        var workerExpected = Numeric(session, true, true, true);
        Check(Task.Run(() => Numeric(session, true, true, true)).GetAwaiter().GetResult().SetEquals(workerExpected), "Detached session can run on a worker with no WPF thread access");

        // Include non-joint link transforms, such as runtime model adjustments.
        var adjusted = scene.CaptureMotionState();
        var matrix = Matrix3D.Identity; matrix.Scale(new Vector3D(1.2, 1.2, 1.2));
        matrix.Rotate(new Quaternion(new Vector3D(0, 1, 0), 17)); matrix.Translate(new Vector3D(.03, -.01, .02));
        adjusted.LinkTransforms["head_link"] = matrix; scene.RestoreMotionState(adjusted);
        Check(Reference(true, true, true).SetEquals(Numeric(scene.CaptureCollisionSession(), true, true, true)), "Link transforms are captured independently of rendered objects");
        object shapes = typeof(UrdfScene).GetField("_collisionShapes", flags).GetValue(scene);
        object model = typeof(UrdfScene).GetField("_collisionModel", flags).GetValue(scene);
        scene.EstablishCollisionBaseline();
        Check(Reference(true, false, false).SetEquals(Numeric(scene.CaptureCollisionSession(), true, false, false)), "Rebuilt baseline refreshes cached pair exclusions");
        Check(ReferenceEquals(shapes, typeof(UrdfScene).GetField("_collisionShapes", flags).GetValue(scene)) &&
            !ReferenceEquals(model, typeof(UrdfScene).GetField("_collisionModel", flags).GetValue(scene)), "Baseline refresh reuses geometry but replaces baseline metadata");
        scene.RestoreMotionState(original); head.SetServoConfiguration(ServoConfiguration.CreateDefault());

        // A full 501-position scan at the original sampling density, without
        // early exit, gives both engines exactly the same amount of work.
        const int count = 501;
        var samples = Enumerable.Range(0, count).Select(i => -.9 + i * 1.8 / (count - 1)).ToArray();
        var numeric = scene.CaptureCollisionSession();
        int RunReference()
        {
            int hits = 0;
            foreach (double value in samples)
            {
                scene.SetJoint("BrowLeftTopTilt", value);
                if (Reference(true, true, true).Count > 0) hits++;
            }
            return hits;
        }
        int RunNumeric()
        {
            int hits = 0;
            foreach (double value in samples)
            {
                numeric.SetJoint("BrowLeftTopTilt", value);
                if (Numeric(numeric, true, true, true).Count > 0) hits++;
            }
            return hits;
        }
        RunReference(); RunNumeric();
        (double Ms, long Bytes, int Hits) Measure(Func<int> run)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            int hits = run(); watch.Stop();
            return (watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes, hits);
        }
        var old = Measure(RunReference); var fast = Measure(RunNumeric);
        Check(old.Hits == fast.Hits, "All 501 benchmark positions return the same collision decision");
        Console.WriteLine($"501-position complete collision scan: old {old.Ms:F1} ms / {old.Bytes / 1024.0:F0} KiB; cached {fast.Ms:F1} ms / {fast.Bytes / 1024.0:F0} KiB ({old.Ms / fast.Ms:F1}x faster). Hits: {fast.Hits}.");
        scene.RestoreMotionState(original);

        // Whole-controller path parity also covers native-unit calibration,
        // per-child reversal, gang expansion and eye-pop gating.
        var config = (UrdfConfiguration)typeof(RobotHeadView).GetField("_urdfConfiguration", flags).GetValue(head);
        var setting = config.Get(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt);
        setting.MinExtent = -27; setting.ZeroExtent = 8; setting.MaxExtent = 73; setting.ReverseOverride = true;
        head.SetUrdfConfiguration(config); head.SetServoConfiguration(ServoConfiguration.CreateDefault());
        foreach (var servo in new[] { ServoNames.FlapsOpen, ServoNames.FlapTiltUp, ServoNames.NoseBody, ServoNames.BothEyePop })
        foreach (int target in new[] { ServoCommand.RangeFor(servo).Min, ServoCommand.RangeFor(servo).Max })
        {
            head.SetServo(servo, 0);
            var command = new ServoCommand { Servo = servo, NumericValue = target };
            var before = scene.CaptureMotionState();
            var linkObjects = ((IDictionary)typeof(UrdfScene).GetField("_links", flags).GetValue(scene)).Values.Cast<Model3DGroup>().Select(l => l.Transform).ToArray();
            int changes = 0; EventHandler changed = (_, _) => changes++;
            foreach (var transform in linkObjects) if (!transform.IsFrozen) transform.Changed += changed;
            bool actual = head.ControllerPathClear(new[] { command }, out _);
            foreach (var transform in linkObjects) if (!transform.IsFrozen) transform.Changed -= changed;
            Check(changes == 0 && before.JointPositions.SequenceEqual(scene.CaptureMotionState().JointPositions) &&
                linkObjects.SequenceEqual(((IDictionary)typeof(UrdfScene).GetField("_links", flags).GetValue(scene)).Values.Cast<Model3DGroup>().Select(l => l.Transform)), "Guard never modifies or replaces rendered transforms");
            bool expected = ReferencePath(command);
            Check(actual == expected, $"Whole native path matches reference: {servo} to {target}");
        }
        Check(!head.ControllerMotionPathClear(new[] { new CollisionMotionTarget(ServoNames.FlapTiltUp, null, double.NaN) }, out _) &&
            !head.ControllerMotionPathClear(new[] { new CollisionMotionTarget(ServoNames.FlapTiltUp, null, double.MaxValue) }, out _), "Non-finite and excessive path requests fail closed before integer conversion");

        bool ReferencePath(ServoCommand command)
        {
            var cache = (Dictionary<(ServoNames Parent, RobotControls Control), double>)typeof(RobotHeadView).GetField("_lastControlValues", flags).GetValue(head);
            var savedCache = cache.ToArray(); var snapshot = scene.CaptureMotionState();
            var controls = ServoConfiguration.ControlsFor(command.Servo).ToArray();
            ServoNames Parent(RobotControls c) => c == RobotControls.LeftEyePop ? ServoNames.LeftEyePop : c == RobotControls.RightEyePop ? ServoNames.RightEyePop : command.Servo;
            var starts = controls.ToDictionary(c => c, c => cache.GetValueOrDefault((Parent(c), c)));
            int steps = Math.Max(1, (int)Math.Ceiling(controls.Max(c => Math.Abs(command.NumericValue - starts[c]) / (c is RobotControls.LeftEyePop or RobotControls.RightEyePop ? 4 : .4))));
            string[] savedFields = { "_poseInternalUpdate", "_leftEyePopLogical", "_rightEyePopLogical", "_poseRevision" };
            var fields = savedFields.Select(n => typeof(RobotHeadView).GetField(n, flags)).ToArray();
            var values = fields.Select(f => f.GetValue(head)).ToArray();
            fields[0].SetValue(head, true);
            try
            {
                for (int i = 0; i <= steps; i++)
                {
                    foreach (var control in controls) head.SetChildServo(Parent(control), control, starts[control] + (command.NumericValue - starts[control]) * i / steps);
                    if (Reference(true, (double)fields[1].GetValue(head) > .0001, (double)fields[2].GetValue(head) > .0001).Count > 0) return false;
                }
                return true;
            }
            finally
            {
                scene.RestoreMotionState(snapshot); cache.Clear(); foreach (var pair in savedCache) cache.Add(pair.Key, pair.Value);
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(head, values[i]);
            }
        }
    }
}
