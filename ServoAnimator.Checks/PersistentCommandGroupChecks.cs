using System.Reflection;
using System.Text.Json;
using System.Windows;
using ServoAnimator;
using SkiaSharp;

internal static partial class Program
{
    private static void PersistentCommandGroupChecks()
    {
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Application).GetField("_startupUri", flags).SetValue(app, null);
        var window = new MainWindow();
        object Call(string method, params object[] values) => typeof(MainWindow).GetMethod(method, flags).Invoke(window, values);
        var doc = new AnimationDocument { Commands = new()
        {
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 1, NumericValue = 10 },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 2, NumericValue = 20 },
            new() { Servo = ServoNames.NoseBasket, OffsetSeconds = 8, NumericValue = 30 }
        }};
        typeof(MainWindow).GetField("_doc", flags).SetValue(window, doc); Call("RefreshAfterEdit");
        var wave = (WaveformView)window.FindName("Waveform");
        wave.SetMarkerSelection(new[] { 1.0, 2 });
        string fingerprint = (string)Call("CurrentSequenceFingerprint");
        Call("SetPersistentCommandGroup", true);
        string id = doc.Commands[0].GroupId;
        Check(id.Length > 0 && doc.Commands[1].GroupId == id && doc.Commands[2].GroupId == "", "Grouping changes only selected commands");
        Check(wave.SelectedMarkers.Count == 0 && wave.CommandGroups.Length == 1, "Grouping deselects markers while preserving group membership");
        Check((string)Call("CurrentSequenceFingerprint") != fingerprint, "Group-only edits mark the sequence dirty");
        var json = new JsonSerializerOptions(); json.Converters.Add(new ServoCommandJsonConverter());
        var roundtrip = JsonSerializer.Deserialize<ServoCommand[]>(JsonSerializer.Serialize(doc.Commands, json), json);
        Check(roundtrip[0].GroupId == id && roundtrip[1].Clone().GroupId == id, "Group metadata survives JSON and undo snapshots");
        var copy = CommandGroupOperations.CopyAt(doc.Commands.Take(2), 10);
        Check(copy[0].GroupId != id && copy[0].GroupId == copy[1].GroupId, "Pasted groups are independent from their originals");
        var repeats = CommandGroupOperations.Repeat(doc.Commands.Take(2), 2, 1);
        Check(repeats[0].GroupId == repeats[1].GroupId && repeats[2].GroupId == repeats[3].GroupId && repeats[0].GroupId != repeats[2].GroupId, "Each repeated group has independent membership");
        wave.SetMarkerSelection(new[] { 1.0 });
        Check(wave.SelectedMarkers.OrderBy(t => t).SequenceEqual(new[] { 1.0, 2 }), "Selecting one triangle selects the complete group");
        Call("MoveSelectedCommandMarkers", 3.0);
        Check(doc.Commands.Where(c => c.GroupId == id).Select(c => c.OffsetSeconds).OrderBy(t => t).SequenceEqual(new[] { 4.0, 5 }) && doc.Commands.Single(c => c.GroupId == "").OffsetSeconds == 8, "Grouped triangles move together while unrelated commands stay put");
        Call("DeleteSelectedCommandGroup");
        Check(doc.Commands.Count == 1 && doc.Commands[0].Servo == ServoNames.NoseBasket, "Delete removes every group member");
        Call("UndoSteps", 1);
        Check(doc.Commands.Count(c => c.GroupId == id) == 2, "Undo restores deleted group membership");
        wave.SetMarkerSelection(new[] { 4.0 }); Call("SetPersistentCommandGroup", false);
        Check(doc.Commands.All(c => c.GroupId == "") && wave.CommandGroups.Length == 0 && wave.SelectedMarkers.Count == 0, "Ungroup removes membership and grouped coloring and deselects");
        Call("UndoSteps", 1); wave.SetMarkerSelection(Array.Empty<double>());
        wave.Width = 900; wave.Height = 180;
        wave.Measure(new Size(900, 180)); wave.Arrange(new Rect(0, 0, 900, 180));
        using var bitmap = new SKBitmap(900, 180); using var canvas = new SKCanvas(bitmap);
        typeof(WaveformView).GetMethod("DrawMarkers", flags).Invoke(wave, new object[] { canvas, 900f });
        Check(bitmap.Pixels.Contains(MainWindow.CommandGroupColor(1)), "Grouped triangles render in their group color");
        Check(doc.CommandGroupNumbers[id] == 1, "First group is numbered one");
        wave.SetMarkerSelection(new[] { 8.0 }); Call("SetPersistentCommandGroup", true);
        string secondId = doc.Commands.Single(c => c.OffsetSeconds == 8).GroupId;
        Check(doc.CommandGroupNumbers[secondId] == 2 && wave.GroupColors[8] != wave.GroupColors[4], "New groups have sequential numbers and different colors");
        doc.HiddenCommandGroups.Add(id); Call("RefreshGroupVisibility");
        var ordered = (ServoCommand[])typeof(MainWindow).GetField("_orderedCommands", flags).GetValue(window);
        Check(!wave.Markers.Contains(4) && !wave.Markers.Contains(5) && ordered.All(c => c.GroupId != id), "Hidden group is excluded from timeline and playback indexes");
        var showButtons = (System.Windows.Controls.WrapPanel)window.FindName("HiddenGroupButtons");
        var show = (System.Windows.Controls.Button)showButtons.Children[0];
        Check((string)show.Content == "Show Group 1", "Hidden group gets a numbered restore button");
        show.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(wave.Markers.Contains(4) && doc.HiddenCommandGroups.Count == 0 && showButtons.Children.Count == 0, "Show restores commands and removes its button");
        doc.HiddenCommandGroups.Add(id);
        var saved = JsonSerializer.Deserialize<AnimationDocument>(JsonSerializer.Serialize(doc));
        Check(saved.CommandGroupNumbers[id] == 1 && saved.HiddenCommandGroups.Count == 0, "Group numbering persists but hiding is temporary");
        Check(doc.Clone().CommandGroupNumbers[secondId] == 2, "Save clones preserve group numbering");
        var recorder = new ControllerRecordingWindow(0);
        var children = (Dictionary<(ServoNames Servo, RobotControls Control), System.Windows.Controls.CheckBox>)typeof(ControllerRecordingWindow).GetField("_children", flags).GetValue(recorder);
        Check(children.Keys.All(c => ServoConfiguration.ControlsFor(c.Servo).Distinct().Count() > 1), "Recorder expands only parents with multiple children");
        Check(!window.IsLoaded, "Checks do not initialize hardware");
    }
}
