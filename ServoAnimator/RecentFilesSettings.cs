// ---------------------------------------------------------------------------
// RecentFilesSettings.cs
//
// Persistent File > Open Recent history and last-active document. Stored as
// RecentFiles.json in the selected animator Configuration folder so each
// configured robot/editor environment keeps its own history.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;

namespace ServoAnimator
{
    internal sealed class RecentFileEntry
    {
        public string Path { get; set; } = "";
        public string Kind { get; set; } = "Sequence"; // Sequence | Movie
        public DateTime LastOpenedUtc { get; set; } = DateTime.UtcNow;
    }

    internal sealed class RecentFilesSettings
    {
        public string LastActivePath { get; set; } = "";
        public string LastActiveKind { get; set; } = "";
        public List<RecentFileEntry> Files { get; set; } = new();

        private static string PathFor(string configFolder) =>
            System.IO.Path.Combine(configFolder, "RecentFiles.json");

        public static RecentFilesSettings Load(string configFolder)
        {
            if (string.IsNullOrWhiteSpace(configFolder)) return new RecentFilesSettings();
            try
            {
                string path = PathFor(configFolder);
                var loaded = File.Exists(path)
                    ? JsonSerializer.Deserialize<RecentFilesSettings>(File.ReadAllText(path))
                    : null;
                loaded ??= new RecentFilesSettings();
                loaded.Files ??= new List<RecentFileEntry>();
                loaded.Files = loaded.Files
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Path))
                    .OrderByDescending(e => e.LastOpenedUtc)
                    .Take(10)
                    .ToList();
                return loaded;
            }
            catch
            {
                return new RecentFilesSettings();
            }
        }

        public void Touch(string path, string kind, bool setActive)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch { full = path; }

            Files ??= new List<RecentFileEntry>();
            Files.RemoveAll(e => string.Equals(e.Path, full, StringComparison.OrdinalIgnoreCase));
            Files.Insert(0, new RecentFileEntry
            {
                Path = full,
                Kind = string.Equals(kind, "Movie", StringComparison.OrdinalIgnoreCase)
                    ? "Movie" : "Sequence",
                LastOpenedUtc = DateTime.UtcNow,
            });
            if (Files.Count > 10)
                Files.RemoveRange(10, Files.Count - 10);

            if (setActive)
            {
                LastActivePath = full;
                LastActiveKind = string.Equals(kind, "Movie", StringComparison.OrdinalIgnoreCase)
                    ? "Movie" : "Sequence";
            }
        }

        public void ClearLastActive()
        {
            LastActivePath = "";
            LastActiveKind = "";
        }

        public void Save(string configFolder)
        {
            if (string.IsNullOrWhiteSpace(configFolder)) return;
            Directory.CreateDirectory(configFolder);
            File.WriteAllText(PathFor(configFolder), JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
