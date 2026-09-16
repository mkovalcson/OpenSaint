using System.IO;

namespace ServoAnimator;

/// <summary>Independent library transport: never replaces the editor document.</summary>
internal sealed class ControllerLibraryRun
{
    private readonly ServoCommand[] _commands;
    private int _next;
    public double Position { get; private set; }
    public double Duration { get; }
    public bool Loop { get; }
    public bool Finished { get; private set; }
    public bool Restarted { get; private set; }
    public ControllerLibraryRun(IEnumerable<ServoCommand> commands, double duration, bool loop)
    {
        _commands = commands.Select(c => c.Clone()).OrderBy(c => c.OffsetSeconds).ToArray();
        if (_commands.Length == 0) throw new InvalidDataException("The Library item has no commands.");
        foreach (var c in _commands)
        {
            if (!Enum.IsDefined(c.Servo) || !Enum.IsDefined(c.Speed) || !double.IsFinite(c.OffsetSeconds) || c.OffsetSeconds < 0 ||
                (c.Control.HasValue && !ServoConfiguration.ControlsFor(c.Servo).Contains(c.Control.Value)))
                throw new InvalidDataException("The Library item contains an invalid command.");
            var range = ServoCommand.RangeFor(c.Servo);
            if (!c.IsTextServo && !c.Disable && (c.NumericValue < range.Min || c.NumericValue > range.Max))
                throw new InvalidDataException($"{c.Servo} must be in {range.Min}..{range.Max}.");
        }
        if (!double.IsFinite(duration) || duration < 0) throw new InvalidDataException("Invalid Library duration.");
        Duration = Math.Max(0.05, Math.Max(duration, _commands[^1].OffsetSeconds)); Loop = loop;
    }
    public IReadOnlyList<ServoCommand> Advance(double seconds)
    {
        Restarted = false;
        var due = new List<ServoCommand>();
        if (Finished) return due;
        Position = Math.Min(Duration, Position + Math.Max(0, seconds));
        while (_next < _commands.Length && _commands[_next].OffsetSeconds <= Position + 1e-9) due.Add(_commands[_next++]);
        if (Position >= Duration)
        {
            if (Loop)
            {
                // Do not replay missed cycles after a stalled UI thread.
                Position = 0; _next = 0; Restarted = true;
                while (_next < _commands.Length && _commands[_next].OffsetSeconds <= 0) due.Add(_commands[_next++]);
            }
            else Finished = true;
        }
        return due;
    }
}
