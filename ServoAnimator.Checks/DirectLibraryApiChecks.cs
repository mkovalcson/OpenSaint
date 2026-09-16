using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ServoAnimator;

internal static partial class Program
{
    private static void DirectLibraryApiChecks(MainWindow window, string root, Func<EditorApiRequest, object> call)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        object Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, EditorApiServer.Json);
        JsonElement Status() => Json(call(new() { Method = "get_status" })).GetProperty("state");
        EditorApiRequest Request(string method, string name = null, bool loop = false) => new() { Method = method,
            Name = name, Loop = loop, RequestId = Guid.NewGuid().ToString("N"), ExpectedToken = Status().GetProperty("libraryToken").GetString() };
        bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
        AnimationDocument.SaveCommandsOnly(Path.Combine(root, "Library", "Animation", "DirectWave.json"), new()
        { new() { Servo = ServoNames.NeckTurn, NumericValue = 10 }, new() { Servo = ServoNames.NeckTurn, NumericValue = 30, OffsetSeconds = 1 } });
        AnimationDocument.SaveLibraryCommand(Path.Combine(root, "Library", "Commands", "DirectPose.json"), new[]
        { new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = -25 } });
        var original = Get("_doc");
        string commands = Json(call(new() { Method = "get_state" })).GetProperty("state").GetProperty("commands").GetRawText();
        double cursor = (double)Get("_cursorTime");
        var preview = (ControllerPreviewUpdates)Get("_controllerPreview");
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        var start = Request("play_library_sequence", "DirectWave", true);
        Check(Json(call(start)).GetProperty("status").GetString() == "completed", "Direct Library sequence starts through the API.");
        Check(Status().GetProperty("library").GetProperty("running").GetBoolean() && Status().GetProperty("library").GetProperty("loop").GetBoolean(), "Lightweight API status reports Library playback and looping.");
        Check(Get("_enabledController") == null, "API Library playback does not require an enabled gamepad.");
        Invoke("TickControllerLibrary", (double?)0.5);
        call(start);
        Check(((ControllerLibraryRun)Get("_controllerLibraryRun")).Position == 0.5, "Retrying the same request does not restart a running sequence.");
        Check(Reject(() => call(new() { Method = "stop_library", ExpectedToken = start.ExpectedToken, RequestId = Guid.NewGuid().ToString("N") })), "A superseded Library token rejects new commands.");
        call(Request("apply_library_pose", "DirectPose")); preview.Flush();
        Check(Get("_controllerLibraryRun") == null && head.CapturePose().NeckTurn == -25, "A pose immediately replaces a loop and reaches the URDF.");
        Check(ReferenceEquals(original, Get("_doc")) && (double)Get("_cursorTime") == cursor
            && Json(call(new() { Method = "get_state" })).GetProperty("state").GetProperty("commands").GetRawText() == commands,
            "Direct playback preserves document identity, timeline commands, and cursor.");
        call(Request("play_library_sequence", "DirectWave", true));
        Invoke("TickControllerLibrary", (double?)1);
        Check(((ControllerLibraryRun)Get("_controllerLibraryRun")).Position == 0 && ((ControllerLibraryRun)Get("_controllerLibraryRun")).Loop, "API sequences loop at their endpoint.");
        var running = Get("_controllerLibraryRun");
        Check(Reject(() => call(Request("apply_library_pose", "missing"))) && ReferenceEquals(running, Get("_controllerLibraryRun")), "A missing Library name does not interrupt the current performance.");
        Check(Reject(() => call(Request("apply_library_pose", "DirectPose", true))), "Poses cannot be requested in a loop.");
        var physical = Request("play_library_sequence", "DirectWave"); physical.DrivePhysical = true;
        Check(Reject(() => call(physical)), "Physical output is rejected until Drive HW and hardware are enabled.");
        var noOutput = Request("apply_library_pose", "DirectPose"); noOutput.DriveUrdf = false;
        Check(Reject(() => call(noOutput)), "Commands require at least one requested output.");
        call(Request("play_library_sequence", "DirectWave"));
        typeof(MainWindow).GetField("_controllerLibraryLastTime", flags).SetValue(window, -2d);
        Invoke("TickApiLibrary"); preview.Flush();
        Check(Get("_controllerLibraryRun") == null && head.CapturePose().NeckTurn == 30, "The independent API timer advances to the final pose without gamepad polling.");
        var focus = (FocusControlSettings)Get("_focusControl"); focus.StreamDeckUrdf = false;
        call(Request("apply_library_pose", "DirectPose")); preview.Flush();
        Check(head.CapturePose().NeckTurn == 30 && !Status().GetProperty("library").GetProperty("urdfOutput").GetBoolean(), "Stream Deck background URDF permission blocks API pose output.");
        focus.StreamDeckUrdf = true;
        var automatic = Request("apply_library_pose", "DirectPose"); automatic.UseEditorOutputs = true;
        call(automatic); preview.Flush();
        Check(head.CapturePose().NeckTurn == -25 && (bool)Get("_apiLibraryUrdf") && (bool)Get("_apiLibraryPhysical")
            && !Status().GetProperty("library").GetProperty("physicalOutput").GetBoolean(),
            "Editor-configured output requests both models and drives URDF even when physical hardware is unavailable.");
        focus.StreamDeckUrdf = false;
        automatic = Request("play_library_sequence", "DirectWave", true); automatic.UseEditorOutputs = true;
        call(automatic); preview.Flush();
        Check(head.CapturePose().NeckTurn == -25 && !(bool)Invoke("ApiLibraryOutputAllowed", false),
            "Automatic Stream Deck playback respects the URDF background opt-out.");
        call(Request("stop_library"));
        // Evaluate real routing with simulated availability only. No hardware calls, ticks, or event handlers run here.
        var liveButton = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("LiveDriveBtn");
        var handler = (RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler), window,
            typeof(MainWindow).GetMethod("LiveDrive_Changed", flags));
        liveButton.Checked -= handler; liveButton.Unchecked -= handler;
        var hardware = (HardwareManager)Get("_hw");
        var connected = typeof(HardwareManager).GetProperty("Connected");
        typeof(MainWindow).GetField("_apiLibraryUrdf", flags).SetValue(window, true);
        typeof(MainWindow).GetField("_apiLibraryPhysical", flags).SetValue(window, true);
        try
        {
            foreach (bool urdf in new[] { false, true })
            foreach (bool physicalAllowed in new[] { false, true })
            foreach (bool available in new[] { false, true })
            foreach (bool drive in new[] { false, true })
            {
                focus.StreamDeckUrdf = urdf; focus.StreamDeckPhysical = physicalAllowed;
                connected.SetValue(hardware, available); liveButton.IsChecked = drive;
                Check((bool)Invoke("ApiLibraryOutputAllowed", false) == urdf
                    && (bool)Invoke("ApiLibraryOutputAllowed", true) == (physicalAllowed && available && drive),
                    "Stream Deck routes both models independently using Focus Control, Drive HW, and current connectivity.");
            }
            focus.StreamDeck = false;
            Check(!(bool)Invoke("ApiLibraryOutputAllowed", false) && !(bool)Invoke("ApiLibraryOutputAllowed", true),
                "The Stream Deck master background switch blocks both destinations.");
        }
        finally
        {
            connected.SetValue(hardware, false); liveButton.IsChecked = false;
            liveButton.Checked += handler; liveButton.Unchecked += handler;
            focus.StreamDeck = focus.StreamDeckUrdf = focus.StreamDeckPhysical = true;
        }
        call(Request("play_library_sequence", "DirectWave", true));
        call(Request("stop_library")); preview.Flush();
        Check(Get("_controllerLibraryRun") == null && preview.Count == 0, "API Stop cancels the loop and queued output.");
        var pending = Request("apply_library_pose"); pending.UseEditorOutputs = true;
        Check(Json(call(pending)).GetProperty("status").GetString() == "awaiting_selection", "Omitting the name returns an asynchronous Library picker receipt.");
        call(pending); call(Request("stop_library"));
        window.Dispatcher.Invoke(() => {}, DispatcherPriority.ContextIdle);
        Check(Json(call(new() { Method = "get_request", RequestId = pending.RequestId })).GetProperty("status").GetString() == "cancelled", "Stop cancels even a queued picker, and a retry cannot open a second picker.");
        Check(!Status().TryGetProperty("commands", out _), "Frequent Stream Deck status polling does not serialize the timeline.");
        LibraryThumbnailChecks(root, call);
        ConnectedControlChecks(window, call);
        StreamDeckClientIntegration(window, call);
    }

    private static void StreamDeckClientIntegration(MainWindow window, Func<EditorApiRequest, object> call)
    {
        string script = Path.Combine(Environment.CurrentDirectory, "StreamDeckXL", "test", "integration.mjs");
        using var server = new EditorApiServer(EditorApiServer.PipeName(Environment.ProcessId),
            request => window.Dispatcher.InvokeAsync(() => call(request)).Task);
        var process = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        process.ArgumentList.Add(script); process.ArgumentList.Add(Environment.ProcessId.ToString());
        var task = Task.Run(async () =>
        {
            using var child = System.Diagnostics.Process.Start(process);
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
            catch { child.Kill(entireProcessTree: true); throw; }
            if (child.ExitCode != 0) throw new Exception(await error + await output);
            return await output;
        });
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => window.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        Dispatcher.PushFrame(frame);
        string result = task.GetAwaiter().GetResult();
        Check(result.Contains("PASS"), "The packaged Node plugin drives the real editor API through Windows named pipes.");
        Console.WriteLine(result.Trim());
    }
}
