namespace ServoAnimator
{
    internal static class CommandGroupOperations
    {
        public static List<ServoCommand> CopyAt(IEnumerable<ServoCommand> commands, double time)
        {
            var source = commands.ToList();
            if (source.Count == 0) return new();
            double first = source.Min(c => c.OffsetSeconds);
            return source.Select(c =>
            {
                var copy = c.Clone();
                copy.OffsetSeconds = ServoCommand.TimeKey(time + c.OffsetSeconds - first);
                return copy;
            }).ToList();
        }

        public static double MeanSpacing(IEnumerable<double> times)
        {
            var keys = times.Select(ServoCommand.TimeKey).Distinct().OrderBy(t => t).ToArray();
            return keys.Length < 2 ? 0 : (keys[^1] - keys[0]) / (keys.Length - 1);
        }

        public static Dictionary<double, double> UniformTimes(IEnumerable<double> times, double spacing)
        {
            if (!double.IsFinite(spacing) || spacing < 0) throw new ArgumentException("Spacing must be a nonnegative number of seconds.");
            var keys = times.Select(ServoCommand.TimeKey).Distinct().OrderBy(t => t).ToArray();
            return keys.Select((key, i) => (key, value: ServoCommand.TimeKey(keys[0] + i * spacing)))
                .ToDictionary(p => p.key, p => p.value);
        }

        public static List<ServoCommand> Repeat(IEnumerable<ServoCommand> commands, int repetitions, double gap)
        {
            var selected = commands.ToList();
            if (repetitions < 1 || repetitions > 10000 || (long)selected.Count * repetitions > 100000)
                throw new ArgumentException("Use 1–10,000 repetitions, creating no more than 100,000 commands at once.");
            if (!double.IsFinite(gap) || gap < 0) throw new ArgumentException("Offset must be a nonnegative number of seconds.");
            if (selected.Count == 0) return new();
            double first = selected.Min(c => c.OffsetSeconds), last = selected.Max(c => c.OffsetSeconds);
            var result = new List<ServoCommand>();
            for (int repeat = 1; repeat <= repetitions; repeat++)
                foreach (var command in selected)
                {
                    var copy = command.Clone();
                    copy.OffsetSeconds = ServoCommand.TimeKey(command.OffsetSeconds + repeat * (last - first + gap));
                    if (!double.IsFinite(copy.OffsetSeconds)) throw new ArgumentException("The resulting command time is too large.");
                    result.Add(copy);
                }
            return result;
        }
    }
}
