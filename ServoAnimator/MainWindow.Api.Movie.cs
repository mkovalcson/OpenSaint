using System.IO;
using System.Text.Json;

namespace ServoAnimator;

public partial class MainWindow
{
    private object HandleMovieEditorApi(EditorApiRequest request, string payload)
    {
        if (string.IsNullOrWhiteSpace(_moviePath))
            throw new InvalidOperationException("Load or save a movie before editing its sequence list.");
        bool inserting = request.Method is "movie_insert" or "movie_create_sequence";
        int index = request.Index ?? (inserting ? _movieItems.Count : -1);
        if (index < 0 || index > _movieItems.Count || (!inserting && index == _movieItems.Count))
            throw new InvalidOperationException("Invalid sequence index. Use the zero-based indices in movieSequences.");
        if (request.Method == "movie_move")
        {
            if (request.ToIndex is not int target || target < 0 || target >= _movieItems.Count)
                throw new InvalidOperationException("toIndex must be a valid final zero-based position.");
            MovieTimeline_ReorderRequested(index, target);
        }
        else if (request.Method == "movie_remove")
        {
            // Detach only: retain the open sequence and any unsaved edits, and never delete its file.
            _movieItems.RemoveAt(index);
            if (_movieSelectedIndex == index) _movieSelectedIndex = -1;
            else if (_movieSelectedIndex > index) _movieSelectedIndex--;
            RefreshMovieTimelineView();
            MovieTimeline.CursorTime = MovieTimeline.StartOf(Math.Min(index, _movieItems.Count));
            ShowStatus("Codex removed sequence from movie; sequence file retained");
        }
        else
        {
            string path;
            AnimationDocument document;
            if (request.Method == "movie_create_sequence")
            {
                if (!PendingMovieSequence.TryName(request.Name, out var name))
                    throw new InvalidOperationException("Supply a valid new sequence name.");
                path = Path.Combine(Path.GetDirectoryName(_moviePath), name + ".json");
                if (!ConfigPathService.IsWithin(ConfigRoot, path)) throw new InvalidOperationException("Movie must be inside Configuration.");
                document = new AnimationDocument { Description = name, DurationSeconds = 1 };
                // CreateNew prevents overwriting another sequence or the movie itself.
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(file, document, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                if (!ConfigPathService.TryResolve(ConfigRoot, request.SequencePath, out path) || !File.Exists(path))
                    throw new InvalidOperationException("sequencePath must identify an existing sequence inside Configuration.");
                using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (!json.RootElement.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array || json.RootElement.TryGetProperty("sequences", out _))
                    throw new InvalidOperationException("The selected file is not a sequence document.");
                document = AnimationDocument.Load(path);
            }
            var item = new MovieSequenceItem { FilePath = path, Description = document.Description,
                DurationSeconds = SequenceDurationFromPath(path) };
            _movieItems.Insert(index, item);
            if (_movieSelectedIndex >= index) _movieSelectedIndex++;
            RefreshMovieTimelineView();
            ShowStatus("Codex inserted sequence into movie: " + Path.GetFileName(path));
        }
        _moviePlaybackActive = false;
        _moviePlaybackIndex = -1;
        MoviePlayButton.Content = "▶ Movie";
        _apiEditGeneration++;
        return ApiReceipt(request, payload, "completed");
    }
}
