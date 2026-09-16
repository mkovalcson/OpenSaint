using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace ServoAnimator;

public partial class MainWindow
{
    private string EditorTransportToken()
    {
        _ = EditorApiToken();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_apiDocumentId + "|" + _jsonPath + "|" + _moviePath)));
    }

    private object EditorCollisionState()
    {
        var view = _urdfUndocked ? _head?.HeadView : EmbeddedHeadView;
        view?.RefreshCollisionNow();
        return new { enabled = view?.CollisionWarningsEnabled == true,
            modelAvailable = view?.CollisionModelAvailable == true,
            urdfDriveEnabled = view?.UrdfDriveEnabled == true,
            cursorSeconds = _cursorTime, pairs = view?.CollisionPairKeys.ToArray() ?? Array.Empty<string>(),
            commandMarkerSeconds = _collisionCommandMarkers.OrderBy(t => t).ToArray(),
            scope = "Current preview pose and command markers observed during playback; not a complete swept-motion collision test." };
    }

    private string ApiLibraryRoot(string kind) => kind switch
    {
        "pose" => LibraryCommandsFolder(), "sequence" => LibraryFolder(),
        _ => throw new InvalidOperationException("libraryKind must be pose or sequence.")
    };

    internal static string ResolveApiLibraryName(string root, string name)
    {
        var items = LibraryItemInfo.Scan(root, loadImages: false).Where(i => i.IsValid).ToList();
        var exact = items.Where(i => string.Equals(i.RelativePath, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var matches = exact.Length > 0 ? exact : items.Where(i =>
            string.Equals(i.FileName, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(i.FileName), name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException(matches.Length == 0
            ? "Library item not found. Use list_library to find its name." : "Ambiguous library name. Specify the relative path from list_library.");
        return matches[0].FullPath;
    }

    private object ApiReceipt(EditorApiRequest request, string payload, string status, int inserted = 0, string error = null)
    {
        object response = new { ok = error == null, requestId = request.RequestId, status, inserted, error, state = EditorApiState() };
        _apiReceipts[request.RequestId] = (payload, response);
        return response;
    }

    private object HandleExtendedEditorApi(EditorApiRequest request, string payload)
    {
        switch (request.Method)
        {
            case "play":
                if (!IsRunning) PlayPause_Click(PlayPauseBtn, new RoutedEventArgs());
                break;
            case "pause":
                if (IsRunning) PausePlayback();
                break;
            case "stop":
                SequenceStop_Click(this, new RoutedEventArgs());
                break;
            case "seek":
                if (request.Seconds is not double seconds || !double.IsFinite(seconds) || seconds < 0 || seconds > TimelineDuration)
                    throw new InvalidOperationException("seconds must be within the current timeline.");
                SetCursor(seconds);
                break;
            case "set_collision_warnings":
                if (!request.Enabled.HasValue) throw new InvalidOperationException("Supply enabled: true or false.");
                HeadView_CollisionWarningEnabledChanged(request.Enabled.Value);
                break;
            case "insert_library":
                string root = ApiLibraryRoot(request.LibraryKind);
                if (!string.IsNullOrWhiteSpace(request.Name))
                    return ApiReceipt(request, payload, "completed", InsertApiLibrary(ResolveApiLibraryName(root, request.Name), request.LibraryKind, _cursorTime));
                // Return before opening the modal picker: selection can take arbitrarily long.
                // The pending receipt prevents retries from opening a second picker.
                var pending = ApiReceipt(request, payload, "awaiting_selection");
                string token = EditorApiToken();
                double at = _cursorTime;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (EditorApiEnabled.IsChecked != true || IsRunning || LiveDrive || token != EditorApiToken())
                            throw new InvalidOperationException("Editor context changed before the picker opened.");
                        var picker = new LibraryItemSelectionWindow(root, manageMode: false,
                            itemLabel: request.LibraryKind == "pose" ? "Library Pose" : "Library Sequence",
                            showAudioFiles: request.LibraryKind == "sequence") { Owner = this };
                        if (picker.ShowDialog() != true || picker.SelectedLibraryItem == null)
                        { ApiReceipt(request, payload, "cancelled"); return; }
                        if (EditorApiEnabled.IsChecked != true || token != EditorApiToken() || IsRunning || LiveDrive)
                            throw new InvalidOperationException("Editor context changed while selecting a library item.");
                        ApiReceipt(request, payload, "completed", InsertApiLibrary(picker.SelectedLibraryItem.FullPath, request.LibraryKind, at));
                    }
                    catch (Exception ex) { ApiReceipt(request, payload, "failed", error: ex.Message); ShowStatus("Codex library insertion: " + ex.Message); }
                }));
                return pending;
        }
        return ApiReceipt(request, payload, "completed");
    }

    private int InsertApiLibrary(string path, string kind, double at)
    {
        if (!ConfigPathService.IsWithin(ApiLibraryRoot(kind), path)) throw new InvalidOperationException("Item is outside the selected library.");
        var commands = AnimationDocument.LoadCommandsOnly(path).Select(c => c.Clone()).ToList();
        if (commands.Count == 0) throw new InvalidOperationException("Library item contains no commands.");
        foreach (var command in commands)
        {
            command.OffsetSeconds = ServoCommand.TimeKey(at + (kind == "pose" ? 0 : command.OffsetSeconds));
            if (!double.IsFinite(command.OffsetSeconds) || command.OffsetSeconds < 0)
                throw new InvalidOperationException("Library item has an invalid time.");
            if (command.Servo == ServoNames.Play)
            {
                string audio = ConfigPathService.TryResolve(ConfigRoot, command.TextValue, out var stored) && File.Exists(stored)
                    ? stored : Path.Combine(LibraryAudioFolder(), Path.GetFileName(command.TextValue ?? ""));
                if (!File.Exists(audio)) throw new InvalidOperationException("Library audio is missing: " + command.TextValue);
                command.TextValue = ConfigPathService.ToRelative(ConfigRoot, audio);
            }
        }
        if (CommandConflicts.Find(_doc.Commands.Concat(commands)).Count > 0)
            throw new InvalidOperationException("Library commands conflict with commands at this cursor. Nothing was inserted.");
        var before = Snapshot();
        var undo = _undoStack.ToArray();
        var redo = _redoStack.ToArray();
        try
        {
            PushUndo("Codex: Insert Library " + kind + " " + Path.GetFileNameWithoutExtension(path));
            _doc.Commands.AddRange(commands);
            RefreshAfterEdit();
        }
        catch
        {
            _doc.Commands = before;
            _undoStack.Clear(); _undoStack.AddRange(undo);
            _redoStack.Clear(); _redoStack.AddRange(redo);
            RefreshAfterEdit();
            throw;
        }
        ShowStatus($"Codex inserted Library {kind} at {at:0.###} s — Ctrl+Z to undo");
        return commands.Count;
    }
}
