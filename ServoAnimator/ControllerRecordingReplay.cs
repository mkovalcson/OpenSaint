namespace ServoAnimator;

/// <summary>Evaluates only a draft take, without installing a temporary document.</summary>
internal sealed class ControllerRecordingReplay
{
    private sealed record Track(ServoCommand[] Commands, double[] Times, double[] Values, double[] Tangents, bool Spline);
    private readonly Track[] _tracks;
    internal double Start { get; }
    internal double Duration { get; }
    internal ControllerRecordingReplay(IEnumerable<ServoCommand> commands, ISet<ServoNames> splines, double start, double end)
    {
        Start = start; Duration = Math.Max(0, end - start);
        _tracks = commands.Where(c => !c.IsTextServo && !c.Disable).GroupBy(c =>
            (Servo: c.Servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight ? ServoNames.NeckNodUp : c.Servo, c.Control))
            .Select(g =>
            {
                var points = g.OrderBy(c => c.OffsetSeconds).Select(c => c.Clone()).ToArray();
                var t = points.Select(c => c.OffsetSeconds).ToArray(); var v = points.Select(c => (double)c.NumericValue).ToArray();
                bool spline = !g.Key.Control.HasValue && (splines.Contains(g.Key.Servo) || g.Key.Servo == ServoNames.NeckNodUp && splines.Contains(ServoNames.NeckTiltRight));
                return new Track(points, t, v, SplineUtil.Tangents(t, v), spline);
            }).ToArray();
    }
    internal ServoCommand[] Evaluate(double elapsed)
    {
        double time = Start + Math.Clamp(elapsed, 0, Duration);
        var result = new List<ServoCommand>();
        foreach (var track in _tracks)
        {
            int index = Array.BinarySearch(track.Times, time);
            if (index < 0) index = ~index - 1;
            if (index < 0) continue;
            var c = track.Commands[index].Clone();
            if (track.Spline) c.NumericValue = (int)Math.Round(SplineUtil.Eval(track.Times, track.Values, track.Tangents, time));
            c.ClampToRange(); result.Add(c);
        }
        return result.ToArray();
    }
}
