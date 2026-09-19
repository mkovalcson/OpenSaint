using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ServoAnimator;

internal static partial class Program
{
    private static void SplineWorkflowChecks(MainWindow window, string folder)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        var doc = new AnimationDocument { Commands = new()
        {
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 1 },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 3, BreakSpline = true },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 5 },
            new() { Servo = ServoNames.NeckNodUp, OffsetSeconds = 2 },
            new() { Servo = ServoNames.NeckTiltRight, OffsetSeconds = 2 },
            new() { Servo = ServoNames.IrisClose, OffsetSeconds = 1 },
            new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 4, Disable = true },
            new() { Servo = ServoNames.NeckTurn, Control = RobotControls.NeckTurn, OffsetSeconds = 4.5 }
        } };
        var copy = doc.Clone();
        int changed = SplineBreakOperations.BreakPreceding(copy.Commands, new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp }, 5);
        Check(changed == 2 && copy.Commands[1].BreakSpline && copy.Commands[4].BreakSpline, "Break predecessors on all enabled physical splines, with shared Neck Tilt tie ownership");
        Check(!copy.Commands[0].BreakSpline && !copy.Commands[2].BreakSpline && !copy.Commands[3].BreakSpline &&
            !copy.Commands[5].BreakSpline && !copy.Commands[6].BreakSpline && !copy.Commands[7].BreakSpline,
            "Preceding breaks skip older, same-time, disabled, child-only, and non-splined commands");
        Check(SplineBreakOperations.BreakPreceding(copy.Commands, new[] { ServoNames.NeckTurn }, 0) == 0, "Insertion at zero creates no preceding command");
        Set("_doc", doc); Set("_cursorTime", 1.0);
        Call("ApplySplineSettings", new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp });
        Call("RefreshAfterEdit");
        var list = (ListBox)window.FindName("CommandsAtPointList");
        var row = list.Items.OfType<CommandInspectorRow>().Single(r => r.Command.Servo == ServoNames.NeckTurn);
        Check(row.SplineEnabled && row.CanBreak && !row.BreakSpline, "Command List exposes the live spline and break states");
        string fingerprint = (string)Call("CurrentSequenceFingerprint");
        Call("CommandBreak_Click", new CheckBox { DataContext = row, IsChecked = true }, new RoutedEventArgs(Button.ClickEvent));
        Check(doc.Commands[0].BreakSpline, "Command List Break Spline edits the command");
        Check(fingerprint != (string)Call("CurrentSequenceFingerprint"), "Break-only edits participate in unsaved-change detection");
        Call("UndoSteps", 1);
        Check(!doc.Commands[0].BreakSpline, "Command List break is undoable");
        row = list.Items.OfType<CommandInspectorRow>().Single(r => r.Command.Servo == ServoNames.NeckTurn);
        Call("CommandSpline_Click", new CheckBox { DataContext = row, IsChecked = false }, new RoutedEventArgs(Button.ClickEvent));
        Check(!list.Items.OfType<CommandInspectorRow>().Single(r => r.Command.Servo == ServoNames.NeckTurn).CanBreak, "Turning Spline off disables the break checkbox");
        Call("UndoSteps", 1);
        var wave = (WaveformView)window.FindName("Waveform");
        wave.SetMarkerSelection(new[] { 1.0, 3.0 }); Set("_showModifiedControls", true); Call("UpdateCommandsAtPointList");
        var modified = list.Items.OfType<ModifiedCommandControl>().Single(c => c.Servo == ServoNames.NeckTurn);
        Check(modified.CanSetBreaks && modified.CanClearBreaks && !list.Items.OfType<ModifiedCommandControl>().Single(c => c.Servo == ServoNames.IrisClose).CanSetBreaks,
            "Modified Controls offers both buttons for existing breaks and none for non-splined controls");
        list.SelectedItem = modified;
        Call("ModifiedBreaks_Click", new Button { DataContext = modified, Tag = "Set" }, new RoutedEventArgs(Button.ClickEvent));
        Check(doc.Commands[0].BreakSpline && doc.Commands[1].BreakSpline && !doc.Commands[2].BreakSpline && !doc.Commands[5].BreakSpline,
            "Set Breaks edits only the selected times and exact command type");
        Check(list.SelectedItem is ModifiedCommandControl { Servo: ServoNames.NeckTurn }, "Bulk edit retains the selected control");
        RenderControl((FrameworkElement)window.FindName("CommandsAtPointPanel"), Path.Combine(folder, "ModifiedBreakButtons.png"), 700, 220);
        modified = (ModifiedCommandControl)list.SelectedItem;
        Call("ModifiedBreaks_Click", new Button { DataContext = modified, Tag = "Clear" }, new RoutedEventArgs(Button.ClickEvent));
        Check(!doc.Commands[0].BreakSpline && !doc.Commands[1].BreakSpline &&
            !((ModifiedCommandControl)list.SelectedItem).CanClearBreaks, "Clear Breaks clears selected flags and hides itself");
        Call("UndoSteps", 1);
        Check(doc.Commands[0].BreakSpline && doc.Commands[1].BreakSpline, "One undo restores the complete bulk break edit");
        Set("_showModifiedControls", false); wave.SetMarkerSelection(Array.Empty<double>()); Call("UpdateCommandsAtPointList");
        RenderControl((FrameworkElement)window.FindName("CommandsAtPointPanel"), Path.Combine(folder, "CommandListSplineChecks.png"), 700, 220);

        string config = Path.Combine(folder, "workflow-library");
        Set("_folders", new FolderSettings { ConfigFolder = config });
        string library = Path.Combine(config, "Library", "Commands"); Directory.CreateDirectory(library);
        string path = Path.Combine(library, "TestPose.json");
        AnimationDocument.SaveLibraryCommand(path, new[] { new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = 20 } }, "Test", null);
        int beforeCount = doc.Commands.Count;
        Call("InsertApiLibrary", path, "pose", 6.0, true);
        Check(doc.Commands.Count == beforeCount + 1 && doc.Commands.Single(c => c.Servo == ServoNames.NeckTurn && c.OffsetSeconds == 5).BreakSpline,
            "Library insertion and preceding breaks apply together");
        Call("UndoSteps", 1);
        Check(doc.Commands.Count == beforeCount && !doc.Commands.Single(c => c.Servo == ServoNames.NeckTurn && c.OffsetSeconds == 5).BreakSpline,
            "One undo removes the inserted Library Pose and restores preceding breaks");
        Call("InsertLibrarySequence", path, 6.0, true);
        Check(doc.Commands.Count == beforeCount + 1 && doc.Commands.Single(c => c.OffsetSeconds == 5).BreakSpline, "UI Library Sequence insertion applies preceding breaks");
        Call("UndoSteps", 1);
        Check(doc.Commands.Count == beforeCount && !doc.Commands.Single(c => c.OffsetSeconds == 5).BreakSpline, "Sequence insertion and breaks undo as one operation");
        var picker = new LibraryItemSelectionWindow(library, false, "Library Pose", false, offerBreakPreceding: true);
        var button = (Button)picker.FindName("SelectBreakButton");
        Check(button.Visibility == Visibility.Visible && button.IsEnabled && button.Content.ToString() == "Insert Pose, Break Preceding Splines", "Pose picker offers the additional insertion button");
        RenderControl((FrameworkElement)picker.Content, Path.Combine(folder, "LibraryBreakInsertion.png"), 1100, 550);
        var loader = new LibraryItemSelectionWindow(library, false, "Library Pose", false, "Load Selected Pose");
        Check(((Button)loader.FindName("SelectBreakButton")).Visibility == Visibility.Collapsed, "Live pose loaders do not offer timeline break actions");

        var spline = new SplineView();
        typeof(SplineView).GetField("_drag", flags).SetValue(spline, Enum.Parse(typeof(SplineView).GetField("_drag", flags).FieldType, "Time"));
        typeof(SplineView).GetField("_dragServo", flags).SetValue(spline, ServoNames.NeckTurn);
        typeof(SplineView).GetField("_dragKey", flags).SetValue(spline, 3.0);
        bool menu = false, moved = false;
        spline.PointRightClicked += (s, t) => menu = s == ServoNames.NeckTurn && t == 3;
        spline.DragCompleted += () => moved = true;
        typeof(SplineView).GetMethod("OnMouseUp", flags).Invoke(spline, new object[] { new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.MouseUpEvent } });
        Check(menu && !moved, "Right-click on a spline point opens its break menu without moving the point");
        menu = false;
        typeof(SplineView).GetField("_drag", flags).SetValue(spline, Enum.Parse(typeof(SplineView).GetField("_drag", flags).FieldType, "Time"));
        typeof(SplineView).GetField("_rightDragMoved", flags).SetValue(spline, true);
        typeof(SplineView).GetMethod("OnMouseUp", flags).Invoke(spline, new object[] { new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.MouseUpEvent } });
        Check(!menu && moved, "Right-drag continues to move points instead of opening the break menu");
    }
}
