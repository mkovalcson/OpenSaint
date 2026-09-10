using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServoAnimator
{
    /// <summary>Periodic, disposable recovery data. It is deliberately kept
    /// separate from project/movie files so autosave never overwrites the
    /// user's chosen file.</summary>
    internal sealed class EditorRecoverySnapshot
    {
        public DateTime SavedUtc { get; set; }
        public string SequencePath { get; set; } = "";
        public string PendingMovieSequenceName { get; set; } = "";
        public AnimationDocument Sequence { get; set; }
        public string MoviePath { get; set; } = "";
        public string MovieDescription { get; set; } = "";
        public string MovieCreatedDate { get; set; } = "";
        public List<MovieSequenceItem> MovieItems { get; set; } = new();
        public int MovieSelectedIndex { get; set; } = -1;
        public double SequenceCursorTime { get; set; }
        public double MovieCursorTime { get; set; }
        public string ActiveDocumentKind { get; set; } = "None";
        public bool SequenceWasDirty { get; set; }
        public bool MovieWasDirty { get; set; }
        public bool ConfigurationWasDirty { get; set; }
        public ServoConfiguration ServoConfiguration { get; set; }
        public UrdfConfiguration UrdfConfiguration { get; set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string PathFor(string configFolder) =>
            Path.Combine(string.IsNullOrWhiteSpace(configFolder)
                ? AppContext.BaseDirectory : configFolder, "EditorRecovery.json");

        public static EditorRecoverySnapshot Load(string configFolder)
        {
            string path = PathFor(configFolder);
            if (!File.Exists(path)) return null;
            var snapshot = JsonSerializer.Deserialize<EditorRecoverySnapshot>(
                File.ReadAllText(path), Options);
            if (snapshot == null) return null;
            snapshot.SequencePath = ResolveOrEmpty(configFolder, snapshot.SequencePath);
            snapshot.MoviePath = ResolveOrEmpty(configFolder, snapshot.MoviePath);
            ConfigPathService.ResolveDocumentPaths(snapshot.Sequence, configFolder);
            foreach (var item in snapshot.MovieItems ?? new List<MovieSequenceItem>())
                item.FilePath = ResolveOrEmpty(configFolder, item.FilePath);
            return snapshot;
        }

        public void Save(string configFolder)
        {
            string path = PathFor(configFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
            string temporary = path + ".tmp";
            var persisted = new EditorRecoverySnapshot
            {
                SavedUtc = SavedUtc,
                SequencePath = RelativeOrEmpty(configFolder, SequencePath),
                PendingMovieSequenceName = PendingMovieSequenceName,
                Sequence = CloneSequenceForStorage(configFolder),
                MoviePath = RelativeOrEmpty(configFolder, MoviePath),
                MovieDescription = MovieDescription,
                MovieCreatedDate = MovieCreatedDate,
                MovieItems = (MovieItems ?? new List<MovieSequenceItem>())
                    .Where(i => ConfigPathService.IsWithin(configFolder, i.FilePath))
                    .Select(i => new MovieSequenceItem
                    {
                        FilePath = ConfigPathService.ToRelative(configFolder, i.FilePath),
                        DurationSeconds = i.DurationSeconds,
                        Description = i.Description,
                        IsLooping = i.IsLooping,
                    }).ToList(),
                MovieSelectedIndex = MovieSelectedIndex,
                SequenceCursorTime = SequenceCursorTime,
                MovieCursorTime = MovieCursorTime,
                ActiveDocumentKind = ActiveDocumentKind,
                SequenceWasDirty = SequenceWasDirty,
                MovieWasDirty = MovieWasDirty,
                ConfigurationWasDirty = ConfigurationWasDirty,
                ServoConfiguration = ServoConfiguration,
                UrdfConfiguration = UrdfConfiguration,
            };
            File.WriteAllText(temporary, JsonSerializer.Serialize(persisted, Options));
            File.Move(temporary, path, overwrite: true);
        }

        private static string ResolveOrEmpty(string configFolder, string storedPath) =>
            ConfigPathService.TryResolve(configFolder, storedPath, out string full)
                ? full : "";

        private static string RelativeOrEmpty(string configFolder, string fullPath) =>
            string.IsNullOrWhiteSpace(fullPath) ? "" :
                ConfigPathService.ToRelative(configFolder, fullPath);

        private AnimationDocument CloneSequenceForStorage(string configFolder)
        {
            if (Sequence == null) return null;
            var clone = new AnimationDocument
            {
                Description = Sequence.Description,
                AudioFiles = Sequence.AudioFiles,
                AudioFile = Sequence.AudioFile,
                AudioFilePath = Sequence.AudioFilePath,
                DurationSeconds = Sequence.DurationSeconds,
                AudioStartOffsetSeconds = Sequence.AudioStartOffsetSeconds,
                SplineServos = Sequence.SplineServos?.ToList() ?? new(),
                SplineSampleHz = Sequence.SplineSampleHz,
                AnimateMode = Sequence.AnimateMode,
                ScaleValues = Sequence.ScaleValues,
                Commands = Sequence.Commands?.Select(c => c.Clone()).ToList() ?? new(),
            };
            return ConfigPathService.MakeDocumentPathsRelative(clone, configFolder);
        }

        public static void Delete(string configFolder)
        {
            string path = PathFor(configFolder);
            if (File.Exists(path)) File.Delete(path);
            string temporary = path + ".tmp";
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
