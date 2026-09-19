using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerRecordingChecks()
    {
        ServoCommand Command(ServoNames s, int value, double time = 0, RobotControls? child = null, ServoSpeed speed = ServoSpeed.Default) =>
            new() { Servo = s, NumericValue = value, OffsetSeconds = time, Control = child, Speed = speed };
        var splines = new HashSet<ServoNames> { ServoNames.NeckTurn };
        var take = new ControllerRecording(5, new[] { ServoNames.NeckTurn });
        for (int i = 0; i <= 600; i++) { take.Accept(Command(ServoNames.NeckTurn, (int)Math.Round(65 * Math.Sin(i * Math.PI / 300)))); take.Sample(5 + i / 30.0); }
        var reduced = take.Process(0, 1, splines);
        Check(reduced.Count < 100, $"Curved gesture reduces 601 samples substantially ({reduced.Count} commands)");
        Check(reduced[0].OffsetSeconds == 5 && reduced[^1].OffsetSeconds == 25 && reduced[0].NumericValue == 0 && reduced[^1].NumericValue == 0, "Nonzero cursor and both endpoints retained");
        var full = take.Frames.Select(f => f.Commands.Single()).ToArray();
        double Evaluate(IEnumerable<ServoCommand> commands, double time)
        {
            var all = commands.ToArray(); var t = all.Select(c => c.OffsetSeconds).ToArray(); var v = all.Select(c => (double)c.NumericValue).ToArray();
            return SplineUtil.Eval(t, v, SplineUtil.Tangents(t, v), time);
        }
        double maxError = Enumerable.Range(0, 2401).Max(i => Math.Abs(Evaluate(full, 5 + i / 120.0) - Evaluate(reduced, 5 + i / 120.0)));
        Check(maxError <= 2.01, $"Reduced Hermite motion remains within one percent of full range ({maxError:F3})");
        var exact = take.Process(0, 0, splines);
        Check(Enumerable.Range(0, 601).All(i => Math.Abs(Evaluate(exact, full[i].OffsetSeconds) - full[i].NumericValue) < 1e-6), "Zero tolerance preserves every sampled spline value");

        var noise = new ControllerRecording(0, new[] { ServoNames.NoseBasket });
        for (int i = 0; i <= 100; i++) { noise.Accept(Command(ServoNames.NoseBasket, i is 0 or 100 ? 50 : i % 2 == 0 ? 54 : 46)); noise.Sample(i / 50.0); }
        var smoothed = noise.Process(200, 0, new HashSet<ServoNames>());
        Check(smoothed[0].NumericValue == 50 && smoothed[^1].NumericValue == 50, "Smoothing preserves endpoints");
        Check(smoothed.Where(c => c.OffsetSeconds > .2 && c.OffsetSeconds < 1.8).All(c => Math.Abs(c.NumericValue - 50) <= 1), "Time-weighted smoothing suppresses joystick jitter");
        var held = take.Process(0, 1, new HashSet<ServoNames>());
        Check(full.All(c => Math.Abs(c.NumericValue - held.Last(p => p.OffsetSeconds <= c.OffsetSeconds).NumericValue) <= 2), "Non-spline reduction respects held-command playback");

        var sides = new ControllerRecording(0, new[] { ServoNames.EyesHorizontalRight });
        sides.Accept(Command(ServoNames.EyesHorizontalRight, 10)); sides.Sample(0);
        sides.Accept(Command(ServoNames.EyesHorizontalRight, 40, child: RobotControls.LeftLensHorizontal)); sides.Sample(1);
        sides.Accept(Command(ServoNames.EyesHorizontalRight, 20, speed: ServoSpeed.Slow)); sides.Sample(2);
        var split = sides.Process(0, 0, new HashSet<ServoNames> { ServoNames.EyesHorizontalRight });
        Check(split.All(c => c.Control.HasValue) && split.Count(c => c.OffsetSeconds == 0) == 2, "A split gesture retains independent children for the entire take");
        Check(split.Single(c => c.OffsetSeconds == 1 && c.Control == RobotControls.LeftLensHorizontal).NumericValue == 40 &&
            split.Last(c => c.OffsetSeconds <= 1 && c.Control == RobotControls.RightLensHorizontal).NumericValue == 10, "Moving one child preserves its sibling");
        Check(split.Where(c => c.OffsetSeconds == 2).All(c => c.Speed == ServoSpeed.Slow), "Gang speed changes retained for both children");
        sides.Accept(Command(ServoNames.EyesHorizontalRight, 30)); sides.Sample(3);
        Check(sides.Frames[^1].Commands.All(c => c.Speed == ServoSpeed.Default), "Explicit profile changes replace previous speeds");
        sides.Accept(new ServoCommand { Servo = ServoNames.EyesHorizontalRight, NumericValue = 31, Speed = ServoSpeed.NoChange }); sides.Sample(4);
        Check(sides.Frames[^1].Commands.All(c => c.Speed == ServoSpeed.Default), "N/C retains the effective profile");

        var neck = new ControllerRecording(0, new[] { ServoNames.NeckNodUp });
        for (int i = 0; i < 90; i++) { neck.Accept(Command(i < 30 || i >= 60 ? ServoNames.NeckNodUp : ServoNames.NeckTiltRight, 10)); neck.Sample(i / 30.0); }
        var modes = neck.Process(0, 1, new HashSet<ServoNames> { ServoNames.NeckNodUp, ServoNames.NeckTiltRight });
        Check(neck.Armed.Contains(ServoNames.NeckTiltRight) && modes.Count < 12, "Shared neck selection and reduction avoid hundreds of constant samples");
        Check(modes.Any(c => c.Servo == ServoNames.NeckTiltRight && c.OffsetSeconds == 1) && modes.Any(c => c.Servo == ServoNames.NeckNodUp && c.OffsetSeconds == 2), "Neck mode switch boundaries retained");
        Check(modes.GroupBy(c => c.OffsetSeconds).All(g => g.Count() == 1), "No competing shared neck owners at the same time");
        var pops = new ControllerRecording(0, new[] { ServoNames.LeftEyePop });
        pops.Accept(Command(ServoNames.BothEyePop, 999)); pops.Sample(0); pops.Sample(1);
        Check(pops.Armed.Count == 3 && pops.Frames[0].Commands.Length == 2 && pops.Current(ServoNames.BothEyePop, null) == 999, "Eye-pop aliases resolve to independent sides with correct current value");
        pops.Accept(Command(ServoNames.BothEyePop, 123, child: RobotControls.LeftEyePop)); pops.Sample(2);
        Check(pops.Current(ServoNames.LeftEyePop, null) == 123 && pops.Current(ServoNames.RightEyePop, null) == 999, "An individual child of BothEyePop does not overwrite its sibling");
        var childTake = new ControllerRecording(1, Array.Empty<ServoNames>(), new[] { (ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal) });
        childTake.Accept(Command(ServoNames.EyesHorizontalRight, 10)); childTake.Sample(1);
        childTake.Accept(Command(ServoNames.EyesHorizontalRight, 40)); childTake.Sample(2);
        var childOutput = childTake.Process(0, 0, new HashSet<ServoNames>());
        Check(childOutput.All(c => c.Control == RobotControls.LeftLensHorizontal) && !childTake.Owns(ServoNames.EyesHorizontalRight), "Child-only take never widens into a parent command");
        var partialMerge = childTake.Merge(new[] { Command(ServoNames.EyesHorizontalRight, 77, 1.5) }, childOutput);
        Check(partialMerge.Any(c => c.Control == RobotControls.RightLensHorizontal && c.NumericValue == 77) && !partialMerge.Any(c => c.Control == null), "Overdubbing a child splits old parent commands to preserve siblings");
        var childSplineMerge = childTake.Merge(new[] { Command(ServoNames.EyesHorizontalRight, 0, 1), Command(ServoNames.EyesHorizontalRight, 100, 2) }, childOutput, new HashSet<ServoNames> { ServoNames.EyesHorizontalRight });
        Check(childSplineMerge.Any(c => c.Control == RobotControls.RightLensHorizontal && c.OffsetSeconds == 1.5 && c.NumericValue == 50), "Splined sibling movement is preserved while overwriting one child");
        var eyeChild = new ControllerRecording(0, Array.Empty<ServoNames>(), new[] { (ServoNames.BothEyePop, RobotControls.LeftEyePop) });
        Check(!eyeChild.Owns(ServoNames.RightEyePop) && eyeChild.Unrecorded(Command(ServoNames.BothEyePop, 500)).Single().Servo == ServoNames.RightEyePop, "Eye-pop child selection preserves the opposite side using its native channel");

        var before = new[] { Command(ServoNames.NeckTurn, -10, 4), Command(ServoNames.NeckTurn, 20, 8), Command(ServoNames.NeckTurn, 30, 26), Command(ServoNames.NoseBasket, 44, 10) };
        before[0].BreakSpline = true;
        var merged = take.Merge(before, reduced);
        Check(merged.Any(c => c.OffsetSeconds == 4 && c.BreakSpline) && merged.Any(c => c.OffsetSeconds == 26 && c.NumericValue == 30) && merged.Any(c => c.Servo == ServoNames.NoseBasket && c.NumericValue == 44), "Overdub preserves outside commands, break flags, and unarmed controls");
        Check(!merged.Any(c => c.OffsetSeconds == 8 && c.NumericValue == 20) && before[1].NumericValue == 20, "Replacement is limited to armed interval and never mutates source snapshots");
        Check(take.Frames.Count == 601 && take.Frames[10].Commands[0].Reason == "", "Processing is repeatable and leaves captured samples intact");

        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // WPF's public setter rejects null. Clear the queued startup document
        // before rendering test controls pumps the dispatcher; no editor may
        // be shown and no Loaded hardware initialization may run in these checks.
        typeof(Application).GetField("_startupUri", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, null);
        var window = new MainWindow(); // Never show it: Loaded opens hardware.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        var doc = new AnimationDocument { Commands = before.Select(c => c.Clone()).ToList() };
        Set("_doc", doc); Call("RefreshAfterEdit"); Call("SetCursor", 5.0);
        var recorder = new ControllerRecordingWindow(5);
        string folder = Path.Combine(Environment.CurrentDirectory, "controller-previews"); Directory.CreateDirectory(folder);
        RenderControl((FrameworkElement)recorder.Content, Path.Combine(folder, "ControllerRecording.png"), 570, 770);
        Set("_recordingWindow", recorder); Set("_recordingCursor", 5.0);
        var connections = (Dictionary<ControllerKind, ControlConnectionState>)Get("_controllerConnections"); connections[ControllerKind.Xbox].Connected = true;
        Set("_focusControl", new FocusControlSettings { CollisionSafeguard = false });
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        var calibration = new SpeedCalibrationData { UseInUrdf = false };
        Set("_speedCalibration", calibration); Set("_speedCalibrationRoot", typeof(MainWindow).GetProperty("ConfigRoot", flags).GetValue(window)); head.SetSpeedCalibration(calibration); head.SetCollisionWarningsEnabled(false);
        Check(head.CollisionModelAvailable, "Test URDF is available without starting hardware");
        var armedChecks = (Dictionary<ServoNames, CheckBox>)typeof(ControllerRecordingWindow).GetField("_controls", flags).GetValue(recorder);
        foreach (var pair in armedChecks) pair.Value.IsChecked = pair.Key == ServoNames.NeckTurn;
        ((TextBox)typeof(ControllerRecordingWindow).GetField("_duration", flags).GetValue(recorder)).Text = "10";
        recorder.StartRequested += (source, selected, duration) => Call("StartControllerRecording", source, selected, duration);
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent(ControllerCatalog.RecordingTarget, 0));
        Check(!recorder.Recording, "REC cannot start before Ready to Record");
        ((Button)typeof(ControllerRecordingWindow).GetField("_record", flags).GetValue(recorder)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(recorder.Armed && !recorder.Recording, "Ready to Record arms configuration without starting playback");
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent(ControllerCatalog.RecordingTarget, 0));
        Call("StopPlaybackRendering");
        Check(recorder.Recording && (double)typeof(MainWindow).GetProperty("CurrentPlaybackEnd", flags).GetValue(window) == 15, "Recording starts at cursor and uses the chosen duration");
        var recordButton = (Button)window.FindName("SequenceRecordButton");
        Check(((System.Windows.Media.SolidColorBrush)recordButton.Background).Color == System.Windows.Media.Color.FromRgb(224, 32, 48), "Mapped REC starts recording and brightens the sequence button");
        Set("_cursorTime", 6.0);
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent("Servo:NeckTurn", 42));
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent("Servo:NoseBasket", 99));
        Call("HandleRecordingIntent", ControllerKind.Steam, new ControllerIntent("Servo:NeckTurn", 88));
        var active = (ControllerRecording)Get("_controllerTake");
        active.Sample(6);
        Check(active.Current(ServoNames.NeckTurn, null) == 42 && active.Frames.All(f => f.Commands.All(c => c.Servo == ServoNames.NeckTurn)), "Only the chosen controller and armed controls enter a take");
        ((ControllerPreviewUpdates)Get("_controllerPreview")).Flush();
        Call("UpdateServoState", 10.0);
        Check(Math.Abs(head.CapturePose().NeckTurn - 42) < .01 && Math.Abs(head.CapturePose().NoseBasket - 44) < .01, "Playback updates unarmed model parts without overwriting controller-owned parts");
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent("Action:Speed Slow", 0));
        Check((ServoSpeed)Get("_controllerSpeed") == ServoSpeed.Slow, "Speed buttons remain available while recording");
        bool apiBlocked = false;
        try { Call("HandleEditorApi", new EditorApiRequest { Method = "play" }); }
        catch (TargetInvocationException ex) { apiBlocked = ex.InnerException is InvalidOperationException && ex.InnerException.Message.Contains("recording"); }
        Check(apiBlocked, "API mutations cannot replace the document or transport under an active take");
        Set("_cursorTime", 7.0); Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent(ControllerCatalog.RecordingTarget, 0));
        Check(((System.Windows.Media.SolidColorBrush)recordButton.Background).Color == System.Windows.Media.Color.FromRgb(112, 27, 36), "Mapped REC stops the take and restores dark red");
        Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent(ControllerCatalog.RecordingTarget, 0));
        Check(!recorder.Recording && ReferenceEquals(active, Get("_controllerTake")), "Another REC press cannot overwrite a take awaiting review");
        Check(!recorder.Recording && doc.Commands.Count == before.Length, "Stopping produces a draft without editing the timeline");
        var output = active.Process(0, 1, new HashSet<ServoNames>()); Set("_processedTake", output);
        var draftReplay = new ControllerRecordingReplay(output, new HashSet<ServoNames>(), active.Start, active.End);
        Check(draftReplay.Evaluate(draftReplay.Duration).All(c => c.Servo == ServoNames.NeckTurn) && draftReplay.Evaluate(draftReplay.Duration).Single().NumericValue == 42,
            "Replay contains only recorded controls and reaches the recorded endpoint");
        int undoCount = ((System.Collections.ICollection)Get("_undoStack")).Count;
        Call("ReplayControllerRecording");
        ((ControllerPreviewUpdates)Get("_controllerPreview")).Flush();
        Check(doc.Commands.Count == before.Length && ((System.Collections.ICollection)Get("_undoStack")).Count == undoCount && (double)Get("_cursorTime") == active.End,
            "Draft replay changes neither the document, cursor, nor undo history");
        Call("StopRecordingReplay");
        Check(!(bool)Get("_recordingReplayOutput"), "Stopping draft replay releases its output permission");
        var curvedReplay = new ControllerRecordingReplay(reduced, splines, take.Start, take.End);
        Check(curvedReplay.Evaluate(2.5).Single().NumericValue == (int)Math.Round(Evaluate(reduced, take.Start + 2.5)), "Replay uses the same spline evaluator as the kept take");
        Call("KeepControllerRecording"); Set("_recordingWindow", null);
        Check(doc.Commands.Any(c => c.NumericValue == 42 && c.Reason == "Controller recording"), "Keep commits ordinary editable commands");
        var recorded = doc.Commands.Where(c => c.Reason == "Controller recording").ToList();
        Check(recorded.Count > 0 && recorded.All(c => c.GroupId.Length > 0) && recorded.Select(c => c.GroupId).Distinct().Count() == 1,
            "Keep groups recorded commands by default");
        Call("UndoSteps", 1);
        Check(doc.Commands.Count == before.Length && doc.Commands.Any(c => c.NumericValue == 20 && c.OffsetSeconds == 8), "One undo restores the complete previous timeline");
        var steamRecorder = new ControllerRecordingWindow(5);
        ((ComboBox)typeof(ControllerRecordingWindow).GetField("_source", flags).GetValue(steamRecorder)).SelectedIndex = 1;
        Set("_recordingWindow", steamRecorder); connections[ControllerKind.Steam].Connected = true;
        Call("StartControllerRecording", ControllerKind.Steam, new HashSet<ServoNames> { ServoNames.NoseBasket }, 10.0); Call("StopPlaybackRendering");
        Set("_cursorTime", 6.0); Call("HandleRecordingIntent", ControllerKind.Steam, new ControllerIntent("Servo:NoseBasket", 22));
        Check(steamRecorder.Recording && ((ControllerRecording)Get("_controllerTake")).Current(ServoNames.NoseBasket, null) == 22, "Steam controller records through the same production path");
        connections[ControllerKind.Steam].Connected = false;
        Call("RecordingTimer_Tick", null, EventArgs.Empty);
        Check(!steamRecorder.Recording && doc.Commands.Count == before.Length, "Disconnect stops the take for review without committing edits");
        steamRecorder.Review(false); steamRecorder.Close(); Set("_recordingWindow", null);
        var childWindow = new ControllerRecordingWindow(5);
        Check(childWindow.Title == "Record from Controller", "Recording window uses requested title");
        var parentBoxes = (Dictionary<ServoNames, CheckBox>)typeof(ControllerRecordingWindow).GetField("_controls", flags).GetValue(childWindow);
        foreach (var box in parentBoxes.Values) box.IsChecked = false;
        var childBoxes = (Dictionary<(ServoNames, RobotControls), CheckBox>)typeof(ControllerRecordingWindow).GetField("_children", flags).GetValue(childWindow);
        childBoxes[(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal)].IsChecked = true;
        ((Button)typeof(ControllerRecordingWindow).GetField("_record", flags).GetValue(childWindow)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(childWindow.ArmedChildren.SetEquals(new[] { (ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal) }), "Ready captures the independently selected child");
        foreach (var box in childBoxes.Values) ((StackPanel)box.Parent).Visibility = Visibility.Visible;
        RenderControl((FrameworkElement)childWindow.Content, Path.Combine(folder, "ControllerRecordingChildren.png"), 570, 770);
        doc = new AnimationDocument { Commands = new() { Command(ServoNames.EyesHorizontalRight, 10, 5), Command(ServoNames.EyesHorizontalRight, 20, 6) } };
        Set("_doc", doc); Set("_recordingWindow", null); Call("ClearManualPoseOverrides"); Call("RefreshAfterEdit"); Call("SetCursor", 5.0);
        Set("_recordingWindow", childWindow);
        Call("StartControllerRecording", ControllerKind.Xbox, new HashSet<ServoNames>(), 10.0); Call("StopPlaybackRendering");
        Set("_cursorTime", 6.0); Call("HandleRecordingIntent", ControllerKind.Xbox, new ControllerIntent("Servo:EyesHorizontalRight", 40));
        ((ControllerPreviewUpdates)Get("_controllerPreview")).Flush(); Call("UpdateServoState", 6.0);
        Check(Math.Abs(head.CapturePose().LeftEyeHorizontal - 40) < .01 && Math.Abs(head.CapturePose().RightEyeHorizontal - 20) < .01,
            "Recording one eye intercepts parent controller input while timeline continues driving the other eye");
        Call("StopControllerRecording", "Child test stopped"); childWindow.Review(false); childWindow.Close(); Set("_recordingWindow", null);
        ((System.Windows.Threading.DispatcherTimer)Get("_recordingProcessingTimer")).Stop();
        Check(!window.IsLoaded && app.Windows.OfType<MainWindow>().Count() == 1, "Focused checks never show an editor or initialize hardware");
        Console.WriteLine($"Recording example: {full.Length} samples → {reduced.Count} spline commands; maximum sampled error {maxError:F3} native units.");
    }
}
