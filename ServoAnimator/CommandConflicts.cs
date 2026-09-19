namespace ServoAnimator
{
    /// <summary>Authored commands share a target only when both the logical servo
    /// and optional child control match. Ganged/child overrides remain distinct.</summary>
    internal static class CommandConflicts
    {
        internal sealed record Group(double Time, ServoNames Servo, RobotControls? Control,
                                     List<ServoCommand> Commands);

        public static List<Group> Find(IEnumerable<ServoCommand> commands) => commands
            .GroupBy(c => (Time: ServoCommand.TimeKey(c.OffsetSeconds), c.Servo, c.Control))
            .Where(g => g.Count() > 1)
            .Select(g => new Group(g.Key.Time, g.Key.Servo, g.Key.Control, g.ToList()))
            .OrderBy(g => g.Time).ToList();

        // Filter in place: survivors retain their original insertion order.
        public static void KeepSelected(List<ServoCommand> commands, IReadOnlyList<Group> groups,
                                        IReadOnlyList<ServoCommand> selected)
        {
            if (groups.Count != selected.Count || groups.Where((g, i) =>
                    !g.Commands.Contains(selected[i])).Any())
                throw new ArgumentException("Choose exactly one command in every conflict group.");
            var remove = groups.SelectMany(g => g.Commands).ToHashSet();
            remove.ExceptWith(selected);
            commands.RemoveAll(remove.Contains);
        }

        public static bool SameContent(ServoCommand a, ServoCommand b) =>
            a.OffsetSeconds == b.OffsetSeconds && a.Servo == b.Servo && a.Control == b.Control &&
            a.NumericValue == b.NumericValue && a.TextValue == b.TextValue &&
            a.Disable == b.Disable && a.BreakSpline == b.BreakSpline && a.GroupId == b.GroupId && a.Speed == b.Speed && a.ColorHex == b.ColorHex &&
            a.Reason == b.Reason && a.ScaledExportValue == b.ScaledExportValue;
    }

    /// <summary>Keep incomplete editor rows out of the playable document. Merge
    /// only this session's edits, preserving changes made in the main window.</summary>
    internal sealed class CommandEditSession
    {
        private readonly Dictionary<ServoCommand, ServoCommand> _drafts = new();
        private readonly Dictionary<ServoCommand, ServoCommand> _originals = new();
        public List<ServoCommand> Commands { get; } = new();

        public CommandEditSession(IEnumerable<ServoCommand> source, double time)
        {
            foreach (var command in source.Where(c => ServoCommand.TimeKey(c.OffsetSeconds) ==
                                                       ServoCommand.TimeKey(time)))
            {
                var draft = command.Clone();
                _drafts.Add(command, draft);
                _originals.Add(command, command.Clone());
                Commands.Add(draft);
            }
        }

        public List<ServoCommand> Merge(IEnumerable<ServoCommand> current)
        {
            var result = current.ToList();
            var edited = new List<ServoCommand>();
            foreach (var pair in _drafts)
            {
                if (!result.Contains(pair.Key)) continue; // another action removed it
                bool deleted = !Commands.Contains(pair.Value);
                bool changed = !CommandConflicts.SameContent(_originals[pair.Key], pair.Value);
                if (!deleted && !changed) continue;
                // If the live command also changed, retain it as an earlier
                // candidate so the conflict chooser can compare both versions.
                if (CommandConflicts.SameContent(_originals[pair.Key], pair.Key))
                    result.Remove(pair.Key);
                if (!deleted) edited.Add(pair.Value);
            }
            result.AddRange(edited.Select(c => c.Clone()));
            var existingDrafts = _drafts.Values.ToHashSet();
            result.AddRange(Commands.Where(c => !existingDrafts.Contains(c)).Select(c => c.Clone()));
            return result;
        }
    }
}
