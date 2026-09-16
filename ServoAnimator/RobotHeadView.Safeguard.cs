namespace ServoAnimator;

internal readonly record struct CollisionMotionTarget(ServoNames Servo, RobotControls? Control, double Value);

public sealed partial class RobotHeadView
{
    public Func<bool> CollisionSafeguardActive { get; set; }
    public Action<string> CollisionSafeguardBlocked { get; set; }

    // Check the same calibrated flap/eye contact geometry as collision warnings,
    // without toggling warnings, touching motion targets, or exposing trial poses.
    internal bool ControllerPathClear(IReadOnlyList<ServoCommand> commands, out string reason)
        => ControllerMotionPathClear(commands.Where(c => !c.Disable && !c.IsTextServo).Select(c => new CollisionMotionTarget(c.Servo, c.Control, c.NumericValue)).ToArray(), out reason);

    internal bool ControllerMotionPathClear(IReadOnlyList<CollisionMotionTarget> commands, out string reason)
    {
        reason = "";
        if (commands.Count == 0) return true;
        if (_scene?.SafeguardAvailable != true) { reason = "Collision geometry is unavailable"; return false; }
        var targets = new Dictionary<(ServoNames Parent, RobotControls Control), double>();
        foreach (var command in commands)
        foreach (var control in command.Control.HasValue ? new[] { command.Control.Value } : ServoConfiguration.ControlsFor(command.Servo))
            if (SafeguardControl(control)) targets[(control == RobotControls.LeftEyePop ? ServoNames.LeftEyePop : control == RobotControls.RightEyePop ? ServoNames.RightEyePop : command.Servo, control)] = command.Value;
        if (targets.Count == 0) return true;
        double Current(RobotControls control) => control switch
        {
            RobotControls.LeftEyePop => _leftEyePopLogical, RobotControls.RightEyePop => _rightEyePopLogical,
            RobotControls.NoseBody => _pose.NoseBody, RobotControls.NoseBasket => _pose.NoseBasket,
            RobotControls.LeftLensHorizontal => _pose.LeftEyeHorizontal, RobotControls.RightLensHorizontal => _pose.RightEyeHorizontal,
            RobotControls.LeftLensVertical => _pose.LeftEyeVertical, RobotControls.RightLensVertical => _pose.RightEyeVertical,
            RobotControls.BrowLeftTopOpen => _pose.LeftTopFlapOpen, RobotControls.BrowRightTopOpen => _pose.RightTopFlapOpen,
            RobotControls.BrowLeftBottomOpen => _pose.LeftBottomFlapOpen, RobotControls.BrowRightBottomOpen => _pose.RightBottomFlapOpen,
            RobotControls.BrowLeftTopTilt => _pose.LeftTopFlapTilt, RobotControls.BrowRightTopTilt => _pose.RightTopFlapTilt,
            _ => 0
        };
        var starts = targets.Keys.ToDictionary(k => k, k => _lastControlValues.GetValueOrDefault(k, Current(k.Control)));
        double requiredSteps = targets.Max(p => Math.Abs(p.Value - starts[p.Key]) /
            (p.Key.Control is RobotControls.LeftEyePop or RobotControls.RightEyePop ? 4 : .4));
        if (!double.IsFinite(requiredSteps) || requiredSteps > 10000) { reason = "Invalid collision-check target"; return false; }
        int steps = Math.Max(1, (int)Math.Ceiling(requiredSteps));
        var session = _scene.CaptureCollisionSession();
        var drives = targets.Select(target =>
        {
            var (parent, control) = target.Key;
            bool pop = control is RobotControls.LeftEyePop or RobotControls.RightEyePop;
            double factor = pop ? .001 : control is RobotControls.BrowLeftTopOpen or RobotControls.BrowRightTopOpen ? -Deg : Deg;
            double max = pop ? 2000 : 100;
            // Snapshot both sides of the calibrated zero; a single endpoint
            // interpolation would lose asymmetric Min/Zero/Max calibration.
            return new CollisionDrive(control, control.ToString(), starts[target.Key], target.Value, max,
                Motion(parent, control, -100) * factor, Motion(parent, control, 0) * factor, Motion(parent, control, max) * factor);
        }).ToArray();
        double left = _leftEyePopLogical, right = _rightEyePopLogical;
        // Include intermediate poses at the existing resolution. All trial
        // positions live in the numeric session, never in the rendered scene.
        for (int i = 0; i <= steps; i++)
        {
            foreach (var drive in drives)
            {
                double value = drive.Start + (drive.End - drive.Start) * i / steps;
                if (!session.SetJoint(drive.Joint, drive.Map(value)))
                { reason = "Collision joint geometry is unavailable"; return false; }
                if (drive.Control == RobotControls.LeftEyePop) left = value;
                if (drive.Control == RobotControls.RightEyePop) right = value;
            }
            if (session.HasCollision(left > .0001, right > .0001))
            { reason = "Predicted flap / eye / gimbal collision"; return false; }
        }
        return true;
    }

    private readonly record struct CollisionDrive(RobotControls Control, string Joint, double Start, double End,
        double MaximumInput, double Low, double Zero, double High)
    {
        internal double Map(double value) => value < 0
            ? Zero + Math.Clamp(-value / 100, 0, 1) * (Low - Zero)
            : Zero + Math.Clamp(value / MaximumInput, 0, 1) * (High - Zero);
    }
    private static bool SafeguardControl(RobotControls control) => control is
        RobotControls.NoseBody or RobotControls.NoseBasket or RobotControls.LeftLensHorizontal or RobotControls.RightLensHorizontal or
        RobotControls.LeftLensVertical or RobotControls.RightLensVertical or RobotControls.BrowLeftTopOpen or RobotControls.BrowRightTopOpen or
        RobotControls.BrowLeftBottomOpen or RobotControls.BrowRightBottomOpen or RobotControls.BrowLeftTopTilt or RobotControls.BrowRightTopTilt or
        RobotControls.LeftEyePop or RobotControls.RightEyePop;
}
