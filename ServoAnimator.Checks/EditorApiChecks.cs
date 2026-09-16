using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ServoAnimator;

internal static partial class Program
{
    private static void EditorApiChecks()
    {
        var request = new EditorApiRequest { Commands = new() {
            new() { Servo = "NeckTurn", Value = -30 },
            new() { Servo = "NeckTurn", Value = 0, AfterSeconds = 2 } } };
        var prepared = EditorApiCommands.Prepare(request, 1.25, Array.Empty<ServoCommand>());
        Check(prepared[0].OffsetSeconds == 1.25 && prepared[1].OffsetSeconds == 3.25, "API must anchor offsets to the captured cursor.");
        bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
        Check(Reject(() => EditorApiCommands.Prepare(request, 1.25, prepared)), "Existing conflicts must reject the whole batch.");
        request.Commands[0].Value = 101;
        Check(Reject(() => EditorApiCommands.Prepare(request, 0, Array.Empty<ServoCommand>())), "Out-of-range values must be rejected.");
        request.Commands[0].Value = 0;
        request.Commands[0].AfterSeconds = -1;
        Check(Reject(() => EditorApiCommands.Prepare(request, 0, Array.Empty<ServoCommand>())), "Negative offsets must be rejected.");

        // Instantiate the real WPF editor without showing it: no Loaded startup,
        // restored user documents, hardware connection, or saved user settings.
        var app = new App();
        // Drain Application's queued startup before assigning StartupUri. Later
        // dispatcher-based pipe checks must not launch a second real editor.
        app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        app.InitializeComponent();
        var window = new MainWindow();
        object Call(EditorApiRequest input)
        {
            try { return typeof(MainWindow).GetMethod("HandleEditorApi", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { input }); }
            catch (TargetInvocationException ex) { throw ex.InnerException; }
        }
        JsonElement State() => JsonSerializer.SerializeToElement(Call(new() { Method = "get_state" }), EditorApiServer.Json).GetProperty("state");
        string token = State().GetProperty("token").GetString();
        var insert = new EditorApiRequest { Method = "insert_commands", RequestId = "test-1", ExpectedToken = token,
            Commands = new() { new() { Servo = "NeckTurn", Value = 20, AfterSeconds = 1 } }, EnableSplines = new() { "NeckTurn" } };
        Call(insert);
        Check(State().GetProperty("commands").GetArrayLength() == 1, "API edit must update the live document.");
        Check(State().GetProperty("splineServos").EnumerateArray().Any(s => s.GetString() == "NeckTurn"), "API enables requested splines.");
        Call(insert);
        Check(State().GetProperty("commands").GetArrayLength() == 1, "An identical retry must not duplicate commands.");
        Check(Reject(() => Call(new() { Method = "undo", RequestId = "stale", ExpectedToken = token })), "Stale tokens must reject edits.");
        Call(new() { Method = "undo", RequestId = "undo-1", ExpectedToken = State().GetProperty("token").GetString() });
        Check(State().GetProperty("commands").GetArrayLength() == 0 && State().GetProperty("splineServos").GetArrayLength() == 0, "One undo must restore commands and spline settings.");
        string libraryRoot = Path.Combine(Path.GetTempPath(), "api-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(libraryRoot, "Library", "Commands"));
        Directory.CreateDirectory(Path.Combine(libraryRoot, "Library", "Animation"));
        // These routing assertions inspect immediate endpoints; calibrated
        // timing is exercised separately by SpeedCalibrationChecks.
        new SpeedCalibrationData { UseInUrdf = false }.Save(libraryRoot);
        typeof(MainWindow).GetField("_folders", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window,
            new FolderSettings { ConfigFolder = libraryRoot, ProjectFolder = libraryRoot });
        try
        {
            var fixture = new AnimationDocument { Commands = new() {
                new() { Servo = ServoNames.NeckTurn, OffsetSeconds = 1, NumericValue = 20 },
                new() { Servo = ServoNames.NeckNodUp, OffsetSeconds = 3, NumericValue = 10 } } };
            fixture.Save(Path.Combine(libraryRoot, "Library", "Commands", "Greeting.json"));
            fixture.Save(Path.Combine(libraryRoot, "Library", "Animation", "Greeting.json"));
            object Edit(string method, string kind = null, string name = null, double? seconds = null, bool? enabled = null) => Call(new() {
                Method = method, RequestId = Guid.NewGuid().ToString("N"), ExpectedToken = State().GetProperty("token").GetString(),
                LibraryKind = kind, Name = name, Seconds = seconds, Enabled = enabled });
            Edit("insert_library", "pose", "Greeting");
            Check(State().GetProperty("commands").EnumerateArray().All(c => c.GetProperty("offsetSeconds").GetDouble() == 0), "Pose insertion must collapse all commands to cursor.");
            Edit("undo");
            Edit("insert_library", "sequence", "Greeting.json");
            Check(State().GetProperty("commands")[1].GetProperty("offsetSeconds").GetDouble() == 3, "Sequence insertion preserves offsets.");
            Check(Reject(() => Edit("insert_library", "sequence", "Greeting")), "Library insertion conflicts reject atomically.");
            Check(State().GetProperty("commands").GetArrayLength() == 2, "Failed library insertion preserves commands.");
            Edit("seek", seconds: 1);
            Check(State().GetProperty("cursorSeconds").GetDouble() == 1, "API seek updates cursor.");
            Edit("play");
            Check(State().GetProperty("playback").GetString() == "Running", "API play starts sequence playback.");
            string transportToken = State().GetProperty("transportToken").GetString();
            Call(new() { Method = "pause", RequestId = "pause-transport", ExpectedToken = transportToken });
            Check(State().GetProperty("playback").GetString() == "Paused", "Transport token allows pausing moving playback.");
            Edit("stop");
            Check(State().GetProperty("playback").GetString() == "Stopped", "API stop stops playback.");
            Edit("set_collision_warnings", enabled: true);
            var collision = JsonSerializer.SerializeToElement(Call(new() { Method = "get_collisions" }), EditorApiServer.Json).GetProperty("collisions");
            Check(collision.GetProperty("enabled").GetBoolean(), "Collision warnings can be enabled and queried.");
            Check(collision.TryGetProperty("modelAvailable", out var availability) && availability.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "Collision status must explicitly report model availability.");
            Edit("set_collision_warnings", enabled: false);
            Edit("undo");
            string moviePath = Path.Combine(libraryRoot, "Movie.json");
            new MovieDocument().Save(moviePath);
            typeof(MainWindow).GetField("_moviePath", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, moviePath);
            object Movie(string method, int? index = null, int? target = null, string name = null, string path = null) => Call(new() {
                Method = method, RequestId = Guid.NewGuid().ToString("N"), ExpectedToken = State().GetProperty("token").GetString(),
                Index = index, ToIndex = target, Name = name, SequencePath = path });
            Movie("movie_create_sequence", name: "First");
            Movie("movie_create_sequence", name: "Second");
            Check(State().GetProperty("movieSequences").GetArrayLength() == 2, "New sequences are created and appended.");
            string oldMovieToken = State().GetProperty("token").GetString();
            Movie("movie_move", 0, 1);
            Check(State().GetProperty("movieSequences")[1].GetProperty("path").GetString().EndsWith("First.json"), "Move uses final destination index.");
            Check(Reject(() => Call(new() { Method = "movie_remove", Index = 0, RequestId = "stale-movie", ExpectedToken = oldMovieToken })), "Movie changes invalidate edit tokens.");
            Movie("movie_remove", 1);
            Check(File.Exists(Path.Combine(libraryRoot, "First.json")), "Removal never deletes the sequence file.");
            Movie("movie_insert", 0, path: "First.json");
            Check(State().GetProperty("movieSequences")[0].GetProperty("path").GetString().EndsWith("First.json"), "Existing sequence inserts at specified index.");
            Check(Reject(() => Movie("movie_insert", path: "Movie.json")), "Movie JSON must not be accepted as a sequence.");
            Check(Reject(() => Movie("movie_move", 0, 99)), "Invalid reorder indices must reject.");
            bool overwriteRejected = false;
            try { Movie("movie_create_sequence", name: "First"); } catch (IOException) { overwriteRejected = true; }
            Check(overwriteRejected, "Creating a new sequence must not overwrite an existing file.");
            DirectLibraryApiChecks(window, libraryRoot, Call);
        }
        finally { Directory.Delete(libraryRoot, true); }
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        using (var server = new EditorApiServer(EditorApiServer.PipeName(Environment.ProcessId),
            input => dispatcher.InvokeAsync(() => Call(input)).Task))
        {
            var process = new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AnimationEditorPlayer.exe")) { UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "--editor-api", "--process", Environment.ProcessId.ToString() }) process.ArgumentList.Add(arg);
            var work = Task.Run(async () =>
            {
                using var child = System.Diagnostics.Process.Start(process);
                var output = child.StandardOutput.ReadToEndAsync();
                var errors = child.StandardError.ReadToEndAsync();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
                if (child.ExitCode != 0) throw new Exception(await errors);
                return await output;
            });
            var frame = new System.Windows.Threading.DispatcherFrame();
            _ = work.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            var reply = JsonDocument.Parse(work.GetAwaiter().GetResult());
            Check(reply.RootElement.GetProperty("ok").GetBoolean() && reply.RootElement.GetProperty("state").GetProperty("commands").GetArrayLength() == 0,
                "Compiled client must read the real editor state through the UI dispatcher.");
        }
        // Test actual pipe framing separately from UI dispatch.
        PipeRoundTrip().GetAwaiter().GetResult();
    }

    private static async Task PipeRoundTrip()
    {
        string name = "Johnny5.ApiChecks." + Guid.NewGuid().ToString("N");
        using var server = new EditorApiServer(name, request => Task.FromResult<object>(new { ok = true, method = request.Method }));
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        using var writer = new StreamWriter(client, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, false, 4096, true);
        await writer.WriteLineAsync("{\"method\":\"get_state\"}");
        var response = JsonDocument.Parse(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Check(response.RootElement.GetProperty("ok").GetBoolean(), "Named pipe must return a JSON response.");
        server.Dispose();
        await server.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
