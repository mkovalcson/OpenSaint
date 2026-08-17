// ---------------------------------------------------------------------------
// FolderSettings.cs
//
// Where the app's files live: the CONFIG folder (servo configuration JSONs,
// the TIC\ folder with Pololu's ticcmd, the Library\ folders, and the
// Projects\ folder holding sequence files) and the PROJECT folder (source
// audio and exported animation JSONs). Persisted to Paths.json in the exe
// folder. On first run the app first looks for an animatorConfig folder beside
// the ServoAnimator project folder; if it exists, it becomes the default CONFIG
// folder automatically. Otherwise the user is prompted. Config > Set Paths… can
// change it later. A legacy Folder.json (with its old "audioFolder" field) is
// migrated to Paths.json automatically.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServoAnimator
{
    public class FolderSettings
    {
        [JsonPropertyName("configFolder")]
        public string ConfigFolder { get; set; } = "";

        [JsonPropertyName("projectFolder")]
        public string ProjectFolder { get; set; } = "";

        /// <summary>Folder most recently used to load or save a sequence.
        /// Persisted independently of ProjectFolder so the sequence dialogs
        /// reopen where the operator last worked.</summary>
        [JsonPropertyName("lastSequenceFolder")]
        public string LastSequenceFolder { get; set; } = "";

        /// <summary>Legacy field name from Folder.json ("audioFolder"):
        /// read-only migration into ProjectFolder, never written back.</summary>
        [JsonPropertyName("audioFolder")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyAudioFolder
        {
            get => null;
            set
            {
                if (string.IsNullOrWhiteSpace(ProjectFolder) &&
                    !string.IsNullOrWhiteSpace(value))
                    ProjectFolder = value;
            }
        }

        /// <summary>Paths.json lives beside the executable.</summary>
        public static string FilePath =>
            Path.Combine(AppContext.BaseDirectory, "Paths.json");

        private static string LegacyFilePath =>
            Path.Combine(AppContext.BaseDirectory, "Folder.json");

        /// <summary>Load the persisted paths. On first run, before prompting,
        /// automatically use a sibling `animatorConfig` folder when one exists at
        /// the same level as the ServoAnimator project directory. A legacy
        /// Folder.json is loaded and re-saved as Paths.json automatically.</summary>
        public static FolderSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<FolderSettings>(
                        File.ReadAllText(FilePath));

                if (File.Exists(LegacyFilePath))
                {
                    var migrated = JsonSerializer.Deserialize<FolderSettings>(
                        File.ReadAllText(LegacyFilePath));
                    migrated?.Save();          // write Paths.json going forward
                    return migrated;
                }

                string siblingConfig = FindSiblingAnimatorConfigFolder();
                if (!string.IsNullOrWhiteSpace(siblingConfig))
                {
                    var discovered = new FolderSettings { ConfigFolder = siblingConfig };
                    // Persist when possible, but still use the discovered folder for
                    // this session if the executable directory is not writable.
                    try { discovered.Save(); } catch { }
                    return discovered;
                }

                return null;
            }
            catch { return null; }
        }

        /// <summary>Find `animatorConfig` beside the ServoAnimator project folder.
        /// This works both when launched from Visual Studio's bin output directory
        /// and when the current directory is somewhere above the project.</summary>
        private static string FindSiblingAnimatorConfigFolder()
        {
            try
            {
                for (DirectoryInfo dir = new(AppContext.BaseDirectory);
                     dir != null;
                     dir = dir.Parent)
                {
                    // Typical development layout:
                    //   <root>\ServoAnimator\bin\Debug\net10.0-windows\...
                    //   <root>\animatorConfig
                    if (string.Equals(dir.Name, "ServoAnimator",
                                      StringComparison.OrdinalIgnoreCase) &&
                        dir.Parent != null)
                    {
                        string candidate = Path.Combine(
                            dir.Parent.FullName, "animatorConfig");
                        if (Directory.Exists(candidate))
                            return Path.GetFullPath(candidate);
                    }

                    // Also recognize an ancestor containing both sibling folders.
                    string projectFolder = Path.Combine(dir.FullName, "ServoAnimator");
                    string configFolder = Path.Combine(dir.FullName, "animatorConfig");
                    if (Directory.Exists(projectFolder) && Directory.Exists(configFolder))
                        return Path.GetFullPath(configFolder);
                }
            }
            catch { }

            return null;
        }

        public void Save()
        {
            ProjectFolder = Path.Combine(ConfigFolderOrDefault, "Projects");
            try { Directory.CreateDirectory(ProjectFolder); } catch { }
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>The config folder, falling back to the exe folder when
        /// unset or missing on disk.</summary>
        public string ConfigFolderOrDefault =>
            !string.IsNullOrWhiteSpace(ConfigFolder) && Directory.Exists(ConfigFolder)
                ? ConfigFolder : AppContext.BaseDirectory;

        /// <summary>The Projects folder is always directly under the selected
        /// Configuration folder. The persisted ProjectFolder field is retained
        /// only for backward-compatible Paths.json reading.</summary>
        public string ProjectFolderOrDefault
        {
            get
            {
                string projects = Path.Combine(ConfigFolderOrDefault, "Projects");
                try { Directory.CreateDirectory(projects); } catch { }
                return Directory.Exists(projects) ? projects : ConfigFolderOrDefault;
            }
        }
    }
}
