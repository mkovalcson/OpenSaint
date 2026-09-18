namespace ServoAnimator;

internal static class SplineBreakOperations
{
    internal static bool SupportsSpline(ServoCommand c) => c != null && !c.Control.HasValue &&
        c.Servo is not ServoNames.Play and not ServoNames.RGBCommand;

    // Nod and Tilt contribute to one physical curve, with Tilt winning ties.
    internal static ServoNames Curve(ServoNames servo) => servo == ServoNames.NeckTiltRight ? ServoNames.NeckNodUp : servo;

    internal static int BreakPreceding(IEnumerable<ServoCommand> commands, IEnumerable<ServoNames> splines, double at)
    {
        var enabled = splines.Select(Curve).ToHashSet();
        var preceding = commands.Where(c => SupportsSpline(c) && !c.Disable &&
            enabled.Contains(Curve(c.Servo)) && ServoCommand.TimeKey(c.OffsetSeconds) < ServoCommand.TimeKey(at))
            .GroupBy(c => Curve(c.Servo)).Select(g => g.OrderBy(c => ServoCommand.TimeKey(c.OffsetSeconds))
                .ThenBy(c => c.Servo == ServoNames.NeckTiltRight ? 1 : 0).Last()).ToArray();
        foreach (var c in preceding) c.BreakSpline = true;
        return preceding.Length;
    }
}
