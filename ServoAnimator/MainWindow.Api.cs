using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ServoAnimator;

public partial class MainWindow
{
    private EditorApiServer _editorApi;
    private AnimationDocument _apiDocument;
    private string _apiDocumentId = Guid.NewGuid().ToString("N");
    private long _apiEditGeneration;
    // Retain receipts for this editor session. Do not evict and risk reapplying an old retry.
    private readonly Dictionary<string, (string Request, object Result)> _apiReceipts = new();

    private void InitializeEditorApi()
    {
        if (_editorApi != null || EditorApiEnabled.IsChecked != true) return;
        _editorApi = new EditorApiServer(EditorApiServer.PipeName(Environment.ProcessId),
            request => Dispatcher.InvokeAsync(() => HandleEditorApi(request)).Task);
        _apiLibraryTimer.Stop(); _apiLibraryTimer.Tick -= ApiLibraryTimer_Tick;
        _apiLibraryTimer.Tick += ApiLibraryTimer_Tick; _apiLibraryTimer.Start();
    }

    private void ApiLibraryTimer_Tick(object sender, EventArgs e) => TickApiLibrary();

    private void EditorApiEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (EditorApiEnabled.IsChecked) InitializeEditorApi();
        else { CancelApiLibraryPicker(); if (_apiLibraryOwnsOutput) StopControllerLibrary(); _apiLibraryTimer.Stop(); _editorApi?.Dispose(); _editorApi = null; }
        ShowStatus(EditorApiEnabled.IsChecked ? "Codex connection enabled" : "Codex connection disabled");
    }

    private string EditorApiToken()
    {
        if (!ReferenceEquals(_apiDocument, _doc))
        {
            _apiDocument = _doc;
            _apiDocumentId = Guid.NewGuid().ToString("N");
        }
        string context = _apiDocumentId + "|" + _apiEditGeneration + "|" + _jsonPath + "|" + _moviePath + "|" +
            _cursorTime.ToString("R", CultureInfo.InvariantCulture) + "|" + CurrentSequenceFingerprint() + "|" + CurrentMovieFingerprint() + "|" +
            string.Join(",", Waveform.SelectedMarkers.OrderBy(t => t).Select(t => t.ToString("R", CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context)));
    }

    private object EditorApiState() => new
    {
        token = EditorApiToken(), processId = Environment.ProcessId, sequencePath = _jsonPath,
        libraryToken = EditorLibraryToken(), libraryPlayback = EditorApiStatus(),
        transportToken = EditorTransportToken(), collisions = EditorCollisionState(),
        moviePath = _moviePath, description = _doc.Description, cursorSeconds = _cursorTime,
        movieSequences = _movieItems.Select((item, index) => new { index, path = item.FilePath, description = item.Description,
            durationSeconds = item.DurationSeconds, looping = item.IsLooping, trigger = item.Trigger, selected = index == _movieSelectedIndex }).ToArray(),
        durationSeconds = TimelineDuration, playback = _mode.ToString(), liveDrive = LiveDrive,
        unsavedChanges = SequenceHasUnsavedChanges(),
        nextUndo = _undoStack.Count > 0 ? _undoStack[^1].Description : null,
        selectedMarkerSeconds = Waveform.SelectedMarkers.ToArray(),
        splineServos = SplineServosEnabled().Select(s => s.ToString()).ToArray(),
        controls = Enum.GetValues<ServoNames>().Where(s => !ServoCommand.IsTextValued(s)).Select(s => new
        {
            name = s.ToString(), min = ServoCommand.RangeFor(s).Min, max = ServoCommand.RangeFor(s).Max,
            currentValue = _rows.FirstOrDefault(r => r.Servo == s)?.Value,
            children = ServoConfiguration.ControlsFor(s).Select(c => c.ToString()).ToArray()
        }).ToArray(),
        commands = _doc.Commands.Select(c => new { servo = c.Servo.ToString(), control = c.Control?.ToString(),
            offsetSeconds = c.OffsetSeconds, value = c.NumericValue, text = c.TextValue,
            speed = c.SpeedDisplay, disable = c.Disable, reason = c.Reason }).ToArray(),
        methods = new[] { "get_state", "get_status", "get_library_thumbnail", "apply_library_pose", "play_library_sequence", "stop_library", "insert_commands", "undo", "list_library", "insert_library", "get_request", "play", "pause", "stop", "seek", "get_collisions", "set_collision_warnings", "movie_move", "movie_remove", "movie_insert", "movie_create_sequence" },
        timeUnits = "seconds; afterSeconds is relative to the cursor captured by token; rounded to milliseconds"
    };

    private object HandleEditorApi(EditorApiRequest request)
    {
        if (EditorApiEnabled.IsChecked != true) throw new InvalidOperationException("Codex connection is disabled.");
        if (request.Method == "get_state") return new { ok = true, state = EditorApiState() };
        if (request.Method == "get_status") return new { ok = true, state = EditorApiStatus() };
        if (request.Method == "stream_deck_status") return StreamDeckPresence(request);
        if (request.Method == "get_collisions") return new { ok = true, collisions = EditorCollisionState() };
        if (request.Method == "list_library") return new { ok = true, items = LibraryItemInfo.Scan(ApiLibraryRoot(request.LibraryKind), loadImages: false).Select(i => new { name = i.FileName, path = i.RelativePath, description = i.Description, valid = i.IsValid }).ToArray() };
        if (request.Method == "get_library_thumbnail") return GetLibraryThumbnail(request);
        if (request.Method == "get_request") return _apiReceipts.TryGetValue(request.RequestId ?? "", out var status) ? status.Result : new { ok = false, error = "Unknown requestId." };
        if (_speedCalibrationBusy) throw new InvalidOperationException("Stop speed calibration before API edits or playback.");
        if (_recordingWindow != null) throw new InvalidOperationException("Close the controller recording window before API edits or playback.");
        bool transport = request.Method is "play" or "pause" or "stop" or "seek";
        bool directLibrary = request.Method is "apply_library_pose" or "play_library_sequence" or "stop_library";
        bool movieEdit = request.Method is "movie_move" or "movie_remove" or "movie_insert" or "movie_create_sequence";
        if (request.Method is not ("insert_commands" or "undo" or "insert_library" or "set_collision_warnings") && !transport && !movieEdit && !directLibrary) throw new InvalidOperationException("Unknown API method.");
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128)
            throw new InvalidOperationException("Edits require a unique requestId (up to 128 characters).");
        string payload = JsonSerializer.Serialize(request, EditorApiServer.Json);
        if (_apiReceipts.TryGetValue(request.RequestId, out var receipt))
        {
            if (receipt.Request != payload) throw new InvalidOperationException("requestId was already used for a different request.");
            return receipt.Result;
        }
        if (_apiReceipts.Count >= 10000) throw new InvalidOperationException("Session receipt limit reached; restart the editor before more API edits.");
        EnsureStreamDeckControl(request);
        bool replacingPicker = (directLibrary || request.Method == "stop") && _apiLibraryPicker != null;
        if ((!IsEnabled && !replacingPicker) || OwnedWindows.Cast<Window>().Any(w => w.IsVisible && w != _head && !(replacingPicker && w == _apiLibraryPicker)))
            throw new InvalidOperationException("Close editor dialogs and auxiliary windows before API edits.");
        if (IsRunning && !transport && !directLibrary && request.Method != "set_collision_warnings") throw new InvalidOperationException("Pause playback before API edits.");
        if (LiveDrive && !directLibrary && request.Method is not ("pause" or "stop" or "set_collision_warnings")) throw new InvalidOperationException("Turn off Live Drive before API edits/playback.");
        if (request.ExpectedToken != EditorApiToken() && !((transport || request.Method == "set_collision_warnings") && request.ExpectedToken == EditorTransportToken())
            && !(directLibrary && request.ExpectedToken == EditorLibraryToken()))
            throw new InvalidOperationException("Editor context changed. Read get_state and prepare the edit again.");
        if (directLibrary) return HandleDirectLibraryApi(request, payload);
        if (transport || request.Method is "insert_library" or "set_collision_warnings")
            return HandleExtendedEditorApi(request, payload);
        if (movieEdit) return HandleMovieEditorApi(request, payload);
        int count = 0;
        if (request.Method == "undo")
        {
            if (_undoStack.Count == 0) throw new InvalidOperationException("Nothing to undo.");
            UndoSteps(1);
        }
        else
        {
            var commands = EditorApiCommands.Prepare(request, _cursorTime, _doc.Commands);
            var splines = new List<ServoNames>();
            foreach (string name in request.EnableSplines ?? new())
            {
                if (!Enum.TryParse<ServoNames>(name, false, out var servo) ||
                    !_rows.Any(r => r.Servo == servo && !r.IsTextRow))
                    throw new InvalidOperationException("Invalid spline servo: " + name);
                splines.Add(servo);
            }
            var before = Snapshot();
            var previousSplines = SplineServosEnabled();
            var previousUndo = _undoStack.ToArray();
            var previousRedo = _redoStack.ToArray();
            try
            {
                PushUndo("Codex: " + (request.Description ?? "Insert commands"));
                _doc.Commands.AddRange(commands);
                ApplySplineSettings(previousSplines.Concat(splines));
                RefreshAfterEdit();
            }
            catch
            {
                _doc.Commands = before;
                ApplySplineSettings(previousSplines);
                _undoStack.Clear(); _undoStack.AddRange(previousUndo);
                _redoStack.Clear(); _redoStack.AddRange(previousRedo);
                RefreshAfterEdit();
                throw;
            }
            count = commands.Count;
            ShowStatus($"Codex inserted {count} commands at {_cursorTime:0.###} s — Ctrl+Z to undo");
        }
        _apiEditGeneration++;
        object result = new { ok = true, requestId = request.RequestId, inserted = count, state = EditorApiState() };
        _apiReceipts.Add(request.RequestId, (payload, result));
        return result;
    }
}
