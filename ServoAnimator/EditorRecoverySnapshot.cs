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
            return JsonSerializer.Deserialize<EditorRecoverySnapshot>(
                File.ReadAllText(path), Options);
        }

        public void Save(string configFolder)
        {
            string path = PathFor(configFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, Options));
            File.Move(temporary, path, overwrite: true);
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
