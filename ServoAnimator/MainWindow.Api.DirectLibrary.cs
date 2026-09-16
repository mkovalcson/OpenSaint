using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace ServoAnimator;

public partial class MainWindow
{
    private readonly DispatcherTimer _apiLibraryTimer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private LibraryItemSelectionWindow _apiLibraryPicker;
    private long _apiLibraryGeneration;

    private string EditorLibraryToken() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        EditorTransportToken() + "|" + ConfigRoot + "|" + _apiLibraryGeneration)));

    private object EditorApiStatus() => new
    {
        processId = Environment.ProcessId, version = AppVersion, generationDate = AppGenerationDate,
        libraryToken = EditorLibraryToken(), transportToken = EditorTransportToken(),
        sequencePath = _jsonPath, moviePath = _moviePath, liveDrive = LiveDrive,
        hardwareConnected = _hw.Connected, playback = _mode.ToString(),
        capabilities = new[] { "editor_configured_library_outputs", "stream_deck_connection" },
        streamDeck = new { connected = _streamDeckConnection.Connected, enabled = _streamDeckConnection.Enabled },
        library = new { running = _controllerLibraryRun != null, name = _controllerLibraryLabel,
            kind = _controllerLibraryKind, source = _apiLibraryOwnsOutput ? "api" : "controller",
            loop = _controllerLibraryRun?.Loop ?? false, positionSeconds = _controllerLibraryRun?.Position ?? 0,
            durationSeconds = _controllerLibraryRun?.Duration ?? 0, selecting = _apiLibraryPicker != null,
            urdfOutput = _apiLibraryOwnsOutput && ApiLibraryOutputAllowed(false),
            physicalOutput = _apiLibraryOwnsOutput && ApiLibraryOutputAllowed(true) },
        methods = new[] { "get_status", "list_library", "get_library_thumbnail", "apply_library_pose", "play_library_sequence", "stop_library", "get_request", "stop" }
    };

    private bool ApiLibraryOutputAllowed(bool physical) => (physical ? _apiLibraryPhysical && LiveDrive && _hw.Connected : _apiLibraryUrdf)
        && FocusControlPolicy.Controller(IsActive || _head?.IsActive == true,
            _focusControl.StreamDeck && (physical ? _focusControl.StreamDeckPhysical : _focusControl.StreamDeckUrdf),
            !IsEnabled || OwnedWindows.Cast<Window>().Any(w => w.IsVisible && w != _head));

    private void TickApiLibrary()
    {
        if (!_apiLibraryOwnsOutput) return;
        try
        {
            if (EditorApiEnabled.IsChecked != true) { StopControllerLibrary(); return; }
            RefreshStreamDeckConnection();
            if (!_apiLibraryOwnsOutput) return;
            RefreshControllerOutputPermissions(); TickControllerLibrary();
        }
        catch (Exception ex) { StopControllerLibrary(); ShowStatus("API Library playback stopped: " + ex.Message); }
    }

    private void CancelApiLibraryPicker()
    {
        _streamDeckPickerPending = false;
        _apiLibraryGeneration++;
        if (_apiLibraryPicker?.IsVisible == true) _apiLibraryPicker.DialogResult = false;
    }

    private object HandleDirectLibraryApi(EditorApiRequest request, string payload)
    {
        if (request.Method == "stop_library")
        {
            CancelApiLibraryPicker(); StopControllerLibrary();
            return ApiReceipt(request, payload, "completed");
        }
        string kind = request.Method == "apply_library_pose" ? "pose" : "sequence";
        if (kind == "pose" && request.Loop == true) throw new InvalidOperationException("Only Library sequences can loop.");
        bool editorOutputs = request.UseEditorOutputs == true;
        bool urdf = editorOutputs || (request.DriveUrdf ?? true), physical = editorOutputs || (request.DrivePhysical ?? false);
        if (!urdf && !physical) throw new InvalidOperationException("Choose at least one output: driveUrdf or drivePhysical.");
        if (!editorOutputs && physical && (!LiveDrive || !_hw.Connected)) throw new InvalidOperationException("Physical playback requires Drive HW on and connected hardware.");
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            // Resolve before interrupting so a misspelled/ambiguous name leaves playback alone.
            string name = System.IO.Path.GetRelativePath(ApiLibraryRoot(kind), ResolveApiLibraryName(ApiLibraryRoot(kind), request.Name));
            CancelApiLibraryPicker();
            try
            {
                StartLibraryPlayback(new("Library:" + kind, 0, name, request.Loop == true), true, urdf, physical, !string.IsNullOrEmpty(request.StreamDeckSession));
                return ApiReceipt(request, payload, "completed");
            }
            catch (Exception ex) { return ApiReceipt(request, payload, "failed", error: ex.Message); }
        }

        CancelApiLibraryPicker();
        long generation = _apiLibraryGeneration;
        string token = EditorTransportToken(), root = ApiLibraryRoot(kind);
        var pending = ApiReceipt(request, payload, "awaiting_selection");
        _streamDeckPickerPending = !string.IsNullOrEmpty(request.StreamDeckSession);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            LibraryItemSelectionWindow picker = null;
            try
            {
                if (generation != _apiLibraryGeneration) { ApiReceipt(request, payload, "cancelled"); return; }
                EnsureStreamDeckControl(request);
                if (EditorApiEnabled.IsChecked != true || !IsEnabled || token != EditorTransportToken() || root != ApiLibraryRoot(kind))
                    throw new InvalidOperationException("Editor context changed before selection.");
                picker = new LibraryItemSelectionWindow(root, manageMode: false,
                    itemLabel: kind == "pose" ? "Library Pose" : "Library Sequence", showAudioFiles: kind == "sequence") { Owner = this };
                _apiLibraryPicker = picker;
                bool accepted = picker.ShowDialog() == true;
                if (ReferenceEquals(_apiLibraryPicker, picker)) _apiLibraryPicker = null;
                if (!accepted || generation != _apiLibraryGeneration || picker.SelectedLibraryItem == null)
                { ApiReceipt(request, payload, "cancelled"); return; }
                if (EditorApiEnabled.IsChecked != true || token != EditorTransportToken() || root != ApiLibraryRoot(kind))
                    throw new InvalidOperationException("Editor context changed during selection.");
                EnsureStreamDeckControl(request);
                _streamDeckPickerPending = false;
                if (!editorOutputs && physical && (!LiveDrive || !_hw.Connected)) throw new InvalidOperationException("Physical hardware is no longer enabled/connected.");
                _apiLibraryGeneration++;
                StartLibraryPlayback(new("Library:" + kind, 0, picker.SelectedLibraryItem.RelativePath, request.Loop == true), true, urdf, physical, !string.IsNullOrEmpty(request.StreamDeckSession));
                ApiReceipt(request, payload, "completed");
            }
            catch (Exception ex) { ApiReceipt(request, payload, "failed", error: ex.Message); }
            finally { if (ReferenceEquals(_apiLibraryPicker, picker)) _apiLibraryPicker = null; }
        }));
        return pending;
    }
}
