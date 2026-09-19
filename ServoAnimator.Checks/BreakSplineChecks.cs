using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using ServoAnimator;
using SkiaSharp;

internal static partial class Program
{
    private static void BreakSplineChecks()
    {
        var app = new App(); app.InitializeComponent();
        var json = new JsonSerializerOptions(); json.Converters.Add(new ServoCommandJsonConverter());
        var command = new ServoCommand { Servo = ServoNames.NeckTurn, OffsetSeconds = 3, NumericValue = 20, BreakSpline = true };
        Check(JsonSerializer.Deserialize<ServoCommand>(JsonSerializer.Serialize(command, json), json).BreakSpline, "Break Spline survives command JSON roundtrip");
        Check(!JsonSerializer.Deserialize<ServoCommand>("{\"servo\":\"NeckTurn\",\"value\":20}", json).BreakSpline, "Older commands default to a continuous line");
        Check(command.Clone().BreakSpline && new AnimationDocument { Commands = new() { command } }.Clone().Commands[0].BreakSpline,
            "Command/document snapshots preserve breaks for copy/paste and undo");
        var original = command.Clone(); original.BreakSpline = false;
        var session = new CommandEditSession(new[] { original }, 3);
        var draft = new CommandSplineDraft(new[] { ServoNames.NeckTurn });
        var vm = new CommandVM(session.Commands[0], null, null, null, null, null, draft);
        Check(vm.SupportsBreakSpline, "Spline-enabled numeric commands allow a break");
        vm.BreakSpline = true;
        Check(!original.BreakSpline && session.Merge(new[] { original }).Single().BreakSpline, "Break-only edits remain drafts until merged and are recognized as changes");
        vm.SplineEnabled = false;
        Check(!vm.SupportsBreakSpline && vm.BreakSpline, "Turning spline off preserves the saved break for later use");
        vm.SplineEnabled = true; vm.Disable = true;
        Check(!vm.SupportsBreakSpline, "Disabled commands do not offer a nonexistent spline segment");
        vm.Disable = false;
        Check(vm.SupportsBreakSpline, "Reenabling a numeric command restores the break checkbox");
        vm.SelectedServoItem = CommandVM.ServoPickOptions.First(i => i.Servo == ServoNames.RGBCommand);
        Check(!vm.SupportsBreakSpline, "RGB commands cannot create numeric spline breaks");

        var view = new SplineView { PixelsPerSecond = 80, Duration = 10, ViewStart = 0 };
        view.Measure(new Size(900, 200)); view.Arrange(new Rect(0, 0, 900, 200));
        var curve = new SplineCurve { Servo = ServoNames.NeckTurn, T = new[] { 1.0, 3, 5, 7 }, V = new double[4], M = new double[4],
            Min = -100, Max = 100, Color = SKColors.Lime, BreakAfter = new[] { false, true, false, true } };
        view.Curves = new() { curve };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        float X(double time) => view.XAtTime(time);
        var draw = typeof(SplineView).GetMethod("DrawCurve", flags);
        using var bitmap = new SKBitmap(900, 200); using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent); draw.Invoke(view, new object[] { canvas, curve, 900f, 200f, 8f });
        Check(bitmap.GetPixel((int)X(2), 100).Alpha > 0 && bitmap.GetPixel((int)X(4), 100).Alpha == 0 && bitmap.GetPixel((int)X(6), 100).Alpha > 0,
            "Renderer leaves precisely the outgoing segment blank and resumes at the next command");
        Check(bitmap.GetPixel((int)X(3), 100).Alpha > 0 && bitmap.GetPixel((int)X(5), 100).Alpha > 0, "Both ends of a broken segment retain editable command dots");
        object HitLine(double time) => typeof(SplineView).GetMethod("HitLine", flags).Invoke(view, new object[] { new Point(X(time), 100), 0.0, 0.0 });
        Check(HitLine(4) == null && ReferenceEquals(HitLine(6), curve), "Hidden spline segments cannot be selected as phantom lines");
        double evaluation = SplineUtil.Eval(curve.T, curve.V, curve.M, 4);
        curve.BreakAfter[1] = false;
        Check(evaluation == SplineUtil.Eval(curve.T, curve.V, curve.M, 4), "Break metadata does not change interpolation");
        canvas.Clear(SKColors.Transparent); draw.Invoke(view, new object[] { canvas, curve, 900f, 200f, 8f });
        Check(bitmap.GetPixel((int)X(4), 100).Alpha > 0, "Unchecking Break Spline restores its line");

        var window = new MainWindow(); // Not shown: no hardware connections.
        var doc = new AnimationDocument { Commands = new()
        {
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 1, BreakSpline = true },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 3 },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 2, Disable = true },
            new() { Servo = ServoNames.NeckNodUp, OffsetSeconds = 1, BreakSpline = true },
            new() { Servo = ServoNames.NeckTiltRight, OffsetSeconds = 3 },
            new() { Servo = ServoNames.NeckNodUp, OffsetSeconds = 5 }
        } };
        typeof(MainWindow).GetField("_doc", flags).SetValue(window, doc);
        typeof(MainWindow).GetMethod("ApplySplineSettings", flags).Invoke(window, new object[] { new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp } });
        typeof(MainWindow).GetMethod("RebuildSplineData", flags).Invoke(window, null);
        var curves = ((SplineView)window.FindName("Spline")).Curves;
        var normal = curves.Single(c => c.Servo == ServoNames.NeckTurn);
        var shared = curves.Single(c => c.IsSharedNeck);
        Check(normal.T.SequenceEqual(new[] { 1.0, 3 }) && !normal.IsSegmentVisible(0), "Curve rebuilding aligns breaks with authored points, excluding disabled commands");
        Check(!shared.IsSegmentVisible(0) && shared.IsSegmentVisible(1), "Shared neck ownership carries a break only on its outgoing segment");
        string folder = Path.Combine(Environment.CurrentDirectory, "spline-previews"); Directory.CreateDirectory(folder);
        var editor = new CommandEditorWindow(doc, 1, null, null, null, null, null, folder,
            new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp }, (_, _) => true);
        RenderControl((FrameworkElement)editor.Content, Path.Combine(folder, "BreakSplineEditor.png"), 1010, 280);
        string savePath = Path.Combine(folder, "BreakSplineRoundtrip.json"); doc.Save(savePath);
        Check(AnimationDocument.Load(savePath).Commands.Count(c => c.BreakSpline) == 2, "Sequence save/load retains breaks on separate controls");
        SplineWorkflowChecks(window, folder);
    }
}
