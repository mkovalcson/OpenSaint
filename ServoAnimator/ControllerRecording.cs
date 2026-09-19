namespace ServoAnimator;

/// <summary>A draft take. Values are accepted native controller targets, not servo feedback.
/// No timeline or hardware is mutated by sampling or processing this class.</summary>
internal sealed class ControllerRecording
{
    internal sealed record Frame(double Time, ServoCommand[] Commands);
    internal readonly List<Frame> Frames = new();
    private readonly Dictionary<(ServoNames, RobotControls?), ServoCommand> _targets = new();
    internal HashSet<ServoNames> Armed { get; }
    private readonly HashSet<RobotControls> _armedChildren;
    private readonly HashSet<ServoNames> _childGroups;
    internal bool Owns(ServoNames servo, RobotControls? child = null) => child.HasValue
        ? _armedChildren.Contains(child.Value)
        : ServoConfiguration.ControlsFor(servo) is var controls && controls.Length > 0 && controls.All(_armedChildren.Contains);
    internal bool Touches(ServoNames servo) => ServoConfiguration.ControlsFor(servo).Any(_armedChildren.Contains);
    internal IEnumerable<ServoCommand> Unrecorded(ServoCommand command)
    {
        if (command.IsTextServo || !Touches(command.Servo)) { yield return command.Clone(); yield break; }
        if (command.Control.HasValue) { if (!Owns(command.Servo, command.Control)) yield return command.Clone(); yield break; }
        foreach (var child in ServoConfiguration.ControlsFor(command.Servo).Where(c => !Owns(command.Servo, c)))
        {
            var copy = command.Clone(); copy.Control = child;
            if (command.Servo == ServoNames.BothEyePop)
            { copy.Servo = child == RobotControls.LeftEyePop ? ServoNames.LeftEyePop : ServoNames.RightEyePop; copy.Control = null; }
            yield return copy;
        }
    }
    internal double Start { get; }
    internal double End => Frames.Count == 0 ? Start : Frames[^1].Time;
    internal ControllerRecording(double start, IEnumerable<ServoNames> armed, IEnumerable<(ServoNames Servo, RobotControls Control)> children = null)
    {
        Start = ServoCommand.TimeKey(start); Armed = ExpandSelection(armed);
        var individual = (children ?? Array.Empty<(ServoNames, RobotControls)>()).ToArray();
        _childGroups = individual.Select(p => p.Servo).ToHashSet();
        _armedChildren = Armed.SelectMany(ServoConfiguration.ControlsFor).Concat(individual.Select(p => p.Control)).ToHashSet();
        Armed.UnionWith(Enum.GetValues<ServoNames>().Where(Touches));
    }

    internal static HashSet<ServoNames> ExpandSelection(IEnumerable<ServoNames> selected)
    {
        var result = selected.Where(s => !ServoCommand.IsTextValued(s)).ToHashSet();
        if (result.Contains(ServoNames.NeckNodUp) || result.Contains(ServoNames.NeckTiltRight))
            result.UnionWith(new[] { ServoNames.NeckNodUp, ServoNames.NeckTiltRight });
        if (result.Overlaps(new[] { ServoNames.BothEyePop, ServoNames.LeftEyePop, ServoNames.RightEyePop }))
            result.UnionWith(new[] { ServoNames.BothEyePop, ServoNames.LeftEyePop, ServoNames.RightEyePop });
        return result;
    }

    internal void Accept(ServoCommand command)
    {
        if (!Touches(command.Servo) || command.Disable || command.IsTextServo) return;
        if (command.Servo == ServoNames.BothEyePop)
        {
            foreach (var side in new[] { ServoNames.LeftEyePop, ServoNames.RightEyePop })
            {
                if (command.Control.HasValue && command.Control != (side == ServoNames.LeftEyePop ? RobotControls.LeftEyePop : RobotControls.RightEyePop)) continue;
                var c = command.Clone(); c.Servo = side; c.Control = null; Accept(c);
            }
            return;
        }
        if (command.Servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
        {
            var other = command.Servo == ServoNames.NeckNodUp ? ServoNames.NeckTiltRight : ServoNames.NeckNodUp;
            foreach (var key in _targets.Keys.Where(k => k.Item1 == other).ToArray()) _targets.Remove(key);
        }
        var children = ServoConfiguration.ControlsFor(command.Servo).ToArray();
        var controls = children.Length > 1
            ? (command.Control.HasValue ? new RobotControls?[] { command.Control } : children.Select(c => (RobotControls?)c).ToArray())
            : new RobotControls?[] { null };
        foreach (var control in controls)
        {
            if (!Owns(command.Servo, control)) continue;
            var copy = command.Clone(); copy.Control = control; copy.ClampToRange();
            var key = (copy.Servo, control);
            if (copy.Speed == ServoSpeed.NoChange && _targets.TryGetValue(key, out var previous)) copy.Speed = previous.Speed;
            _targets[key] = copy;
        }
    }

    internal double? Current(ServoNames servo, RobotControls? control)
    {
        if (servo == ServoNames.BothEyePop)
            return (Current(ServoNames.LeftEyePop, null) + Current(ServoNames.RightEyePop, null)) / 2;
        if (_targets.TryGetValue((servo, control), out var value)) return value.NumericValue;
        var values = _targets.Values.Where(c => c.Servo == servo).ToArray();
        return values.Length == 0 ? null : values.Average(c => c.NumericValue);
    }

    internal void Sample(double time)
    {
        time = ServoCommand.TimeKey(Math.Max(Start, time));
        if (Frames.Count > 0 && time < End) return;
        var commands = _targets.Values.Select(c => { var clone = c.Clone(); clone.OffsetSeconds = time; return clone; }).ToArray();
        var frame = new Frame(time, commands);
        if (Frames.Count > 0 && time == End) Frames[^1] = frame;
        else Frames.Add(frame);
    }

    internal List<ServoCommand> Process(double smoothingMs, double tolerancePercent, ISet<ServoNames> splines)
    {
        if (Frames.Count < 2) return new();
        // A gang stays a single editable track only if all of its children agree
        // throughout the take. Otherwise retain independent children throughout.
        var gangable = Frames.SelectMany(f => f.Commands).Select(c => c.Servo).Distinct().Where(servo =>
        {
            int count = ServoConfiguration.ControlsFor(servo).Count();
            return count > 1 && !_childGroups.Contains(servo) && Frames.All(f =>
            {
                var group = f.Commands.Where(c => c.Servo == servo).ToArray();
                return group.Length == 0 || (group.Length == count && group.All(c => c.NumericValue == group[0].NumericValue && c.Speed == group[0].Speed));
            });
        }).ToHashSet();
        var tracks = new Dictionary<(ServoNames, RobotControls?), List<(int Frame, ServoCommand Command)>>();
        for (int i = 0; i < Frames.Count; i++)
        foreach (var group in Frames[i].Commands.GroupBy(c => c.Servo))
        foreach (var original in gangable.Contains(group.Key) ? group.Take(1) : group)
        {
            var c = original.Clone(); if (gangable.Contains(c.Servo)) c.Control = null;
            var key = (c.Servo, c.Control);
            if (!tracks.TryGetValue(key, out var track)) tracks[key] = track = new();
            track.Add((i, c));
        }
        var result = new List<ServoCommand>();
        foreach (var track in tracks.Values)
        {
            // Switching shared neck modes ends a run; never smooth across it.
            int first = 0;
            for (int i = 1; i <= track.Count; i++)
                if (i == track.Count || track[i].Frame != track[i - 1].Frame + 1)
                {
                    var run = track.GetRange(first, i - first).Select(p => p.Command).ToArray();
                    Smooth(run, Math.Clamp(smoothingMs, 0, 500) / 1000);
                    var range = ServoCommand.RangeFor(run[0].Servo);
                    double error = (range.Max - range.Min) * Math.Clamp(tolerancePercent, 0, 10) / 100;
                    // The shared neck curve combines mode owners. Keep its samples
                    // so independent reduction cannot change tangents at a switch.
                    bool neck = run[0].Servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight;
                    result.AddRange(Reduce(run, error, !run[0].Control.HasValue && splines.Contains(run[0].Servo), neck));
                    first = i;
                }
        }
        var neckCurve = result.Where(c => !c.Control.HasValue && c.Servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight).OrderBy(c => c.OffsetSeconds).ToArray();
        if (neckCurve.Length > 2 && (splines.Contains(ServoNames.NeckNodUp) || splines.Contains(ServoNames.NeckTiltRight)))
        {
            result.RemoveAll(c => !c.Control.HasValue && c.Servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight);
            result.AddRange(Reduce(neckCurve, 200 * Math.Clamp(tolerancePercent, 0, 10) / 100, true, false));
        }
        foreach (var c in result) c.Reason = "Controller recording";
        return result.OrderBy(c => c.OffsetSeconds).ThenBy(c => c.Servo).ThenBy(c => c.Control).ToList();
    }

    private static void Smooth(ServoCommand[] commands, double window)
    {
        if (window <= 0 || commands.Length < 3) return;
        // Time-weighted, centered box filter over held input targets. Keep exact
        // endpoints and split at speed changes rather than blending profiles.
        int start = 0;
        for (int end = 1; end <= commands.Length; end++)
        {
            if (end < commands.Length && commands[end].Speed == commands[start].Speed) continue;
            var original = commands[start..end].Select(c => c.NumericValue).ToArray();
            for (int i = start + 1; i < end - 1; i++)
            {
                double lo = Math.Max(commands[start].OffsetSeconds, commands[i].OffsetSeconds - window / 2);
                double hi = Math.Min(commands[end - 1].OffsetSeconds, commands[i].OffsetSeconds + window / 2);
                double total = 0;
                for (int j = i; j >= start; j--)
                {
                    double weight = Math.Max(0, Math.Min(hi, commands[j + 1].OffsetSeconds) - Math.Max(lo, commands[j].OffsetSeconds));
                    total += weight * original[j - start];
                    if (commands[j].OffsetSeconds <= lo) break;
                }
                for (int j = i + 1; j < end - 1 && commands[j].OffsetSeconds < hi; j++)
                    total += (Math.Min(hi, commands[j + 1].OffsetSeconds) - commands[j].OffsetSeconds) * original[j - start];
                if (hi > lo) commands[i].NumericValue = (int)Math.Round(total / (hi - lo));
                commands[i].ClampToRange();
            }
            start = end;
        }
    }

    private static IEnumerable<ServoCommand> Reduce(ServoCommand[] c, double error, bool spline, bool sharedNeck)
    {
        if (c.Length <= 2 || (spline && sharedNeck)) return c;
        var keep = new SortedSet<int> { 0, c.Length - 1 };
        for (int i = 1; i < c.Length; i++)
            if (c[i].Speed != c[i - 1].Speed || c[i].Servo != c[i - 1].Servo) { keep.Add(i - 1); keep.Add(i); }
        if (!spline)
        {
            int previous = 0;
            for (int i = 1; i < c.Length; i++)
                if (keep.Contains(i) || Math.Abs(c[i].NumericValue - c[previous].NumericValue) > error)
                { keep.Add(i); previous = i; }
        }
        else
        {
            // Seed with a linear simplification before validating the actual
            // Hermite interpolation. This avoids retaining a whole curved take
            // just because most samples differ from the endpoint-to-endpoint line.
            var spans = new Stack<(int A, int B)>();
            var anchors = keep.ToArray();
            for (int k = 1; k < anchors.Length; k++) spans.Push((anchors[k - 1], anchors[k]));
            while (spans.Count > 0)
            {
                var (a, b) = spans.Pop(); int worst = -1; double largest = error;
                for (int i = a + 1; i < b; i++)
                {
                    double f = (c[i].OffsetSeconds - c[a].OffsetSeconds) / (c[b].OffsetSeconds - c[a].OffsetSeconds);
                    double delta = Math.Abs(c[i].NumericValue - (c[a].NumericValue + f * (c[b].NumericValue - c[a].NumericValue)));
                    if (delta > largest) { largest = delta; worst = i; }
                }
                if (worst >= 0) { keep.Add(worst); spans.Push((a, worst)); spans.Push((worst, b)); }
            }
            // Validate against the editor's actual Hermite curve, including
            // midpoints between samples, rather than assuming linear playback.
            var fullT = c.Select(p => p.OffsetSeconds).ToArray();
            var fullV = c.Select(p => (double)p.NumericValue).ToArray();
            var fullM = SplineUtil.Tangents(fullT, fullV);
            for (int pass = 0; pass < 32; pass++)
            {
                var indices = keep.ToArray();
                var t = indices.Select(i => fullT[i]).ToArray(); var v = indices.Select(i => fullV[i]).ToArray();
                var m = SplineUtil.Tangents(t, v); var add = new HashSet<int>();
                for (int span = 1; span < indices.Length; span++)
                {
                    double largest = error + 1e-9; int worst = -1;
                    for (int i = indices[span - 1]; i < indices[span]; i++)
                    for (int quarter = 0; quarter < 4; quarter++)
                    {
                        double x = fullT[i] + (fullT[i + 1] - fullT[i]) * quarter / 4;
                        double delta = Math.Abs(SplineUtil.Eval(t, v, m, x) - SplineUtil.Eval(fullT, fullV, fullM, x));
                        if (delta > largest) { largest = delta; worst = i; }
                    }
                    if (worst < 0) continue;
                    if (!keep.Contains(worst)) add.Add(worst);
                    else if (!keep.Contains(worst + 1)) add.Add(worst + 1);
                    else
                    {
                        if (worst > 0 && !keep.Contains(worst - 1)) add.Add(worst - 1);
                        if (worst + 2 < c.Length && !keep.Contains(worst + 2)) add.Add(worst + 2);
                    }
                }
                if (add.Count == 0) break;
                keep.UnionWith(add);
                if (pass == 31) return c;
            }
        }
        return keep.Select(i => c[i]);
    }

    internal List<ServoCommand> Merge(IEnumerable<ServoCommand> existing, IEnumerable<ServoCommand> take, ISet<ServoNames> splines = null)
    {
        var source = existing.ToArray();
        var retained = source.OrderBy(c => c.Control.HasValue ? 0 : 1).SelectMany(c => ServoCommand.TimeKey(c.OffsetSeconds) < Start || ServoCommand.TimeKey(c.OffsetSeconds) > End
            ? new[] { c.Clone() } : Unrecorded(c)).GroupBy(c => (c.Servo, c.Control, Time: ServoCommand.TimeKey(c.OffsetSeconds))).Select(g => g.Last()).ToList();
        // Splitting a splined parent into held child commands must not flatten
        // its unrecorded siblings. Preserve their evaluated motion across the take.
        foreach (var servo in (splines ?? new HashSet<ServoNames>()).Where(s => Touches(s) && !Owns(s)))
        {
            var parents = source.Where(c => c.Servo == servo && !c.Control.HasValue).OrderBy(c => c.OffsetSeconds).ToArray();
            var points = parents.Where(c => !c.Disable).GroupBy(c => ServoCommand.TimeKey(c.OffsetSeconds)).Select(g => g.Last()).ToArray();
            if (points.Length < 2) continue;
            var t = points.Select(c => c.OffsetSeconds).ToArray(); var v = points.Select(c => (double)c.NumericValue).ToArray(); var m = SplineUtil.Tangents(t, v);
            var times = new SortedSet<double>(source.Where(c => c.Servo == servo && c.OffsetSeconds >= Start && c.OffsetSeconds <= End).Select(c => ServoCommand.TimeKey(c.OffsetSeconds))) { Start, End };
            for (int i = 1; Start + i / 30.0 < End; i++) times.Add(ServoCommand.TimeKey(Start + i / 30.0));
            foreach (var child in ServoConfiguration.ControlsFor(servo).Where(c => !Owns(servo, c)))
            {
                var children = source.Where(c => c.Servo == servo && c.Control == child).OrderBy(c => c.OffsetSeconds).ToArray();
                retained.RemoveAll(c => c.Servo == servo && c.Control == child && c.OffsetSeconds >= Start && c.OffsetSeconds <= End);
                foreach (double time in times)
                {
                    var parent = parents.LastOrDefault(c => c.OffsetSeconds <= time);
                    var individual = children.LastOrDefault(c => c.OffsetSeconds <= time);
                    bool childOwns = individual != null && (parent == null || individual.OffsetSeconds > parent.OffsetSeconds);
                    var basis = childOwns ? individual : parent;
                    var c = basis?.Clone() ?? new ServoCommand { Servo = servo };
                    c.Control = child; c.OffsetSeconds = time;
                    if (!childOwns && !c.Disable && time >= t[0]) c.NumericValue = (int)Math.Round(SplineUtil.Eval(t, v, m, time));
                    c.ClampToRange(); retained.Add(c);
                }
            }
        }
        return retained.Concat(take.Select(c => c.Clone())).OrderBy(c => c.OffsetSeconds).ToList();
    }
}
