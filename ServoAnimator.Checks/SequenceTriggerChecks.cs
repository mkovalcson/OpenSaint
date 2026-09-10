using ServoAnimator;
using System.IO;
using System.Windows.Input;

internal static partial class Program
{
    private static void SequenceTriggers()
    {
        Check(SequenceTrigger.FromKey(Key.D5, ModifierKeys.Control | ModifierKeys.Shift) == "Ctrl+Shift+5", "Trigger modifier ordering and digit label");
        Check(SequenceTrigger.FromKey(Key.F2, ModifierKeys.None) == "F2", "Single key trigger");
        Check(SequenceTrigger.FromKey(Key.Right, ModifierKeys.None) == "", "Right arrow remains transport");
        Check(SequenceTrigger.FromKey(Key.S, ModifierKeys.Control) == "", "Save remains available");
        Check(SequenceTrigger.FromKey(Key.LeftCtrl, ModifierKeys.Control) == "", "Modifier alone is not a trigger");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var movie = new MovieDocument { Sequences = new() { "a.json", "b.json" }, SequenceLoops = new() { true, false }, SequenceTriggers = new() { "Ctrl+Shift+5", "" } };
            movie.Save(path);
            var loaded = MovieDocument.Load(path);
            Check(loaded.SequenceTriggers.SequenceEqual(movie.SequenceTriggers) && loaded.SequenceLoops[0], "Trigger and loop round trip");
            File.WriteAllText(path, "{\"sequences\":[\"a.json\"]}");
            Check(MovieDocument.Load(path).SequenceTriggers.Count == 0, "Old movies have no triggers");
            var geometry = new MovieTimelineGeometry(new[] { new MovieSequenceItem { DurationSeconds = 1, Trigger = "Ctrl+Alt+Shift+Win+PageDown" } }, 1);
            Check(geometry.Width > MovieTimelineGeometry.MinimumSpan, "Long trigger expands minimum block width");
        }
        finally { File.Delete(path); }
    }
}
