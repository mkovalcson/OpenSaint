// ---------------------------------------------------------------------------
// FolderSettings.cs
//
// Where the app's files live: the CONFIG folder (servo configuration JSONs,
// the TIC\ folder with Pololu's ticcmd, the Library\ folders, and the
// Projects\ folder holding sequence files) and the PROJECT folder (source
// audio and exported animation JSONs). Persisted to Paths.json in the exe
// folder. On first run the app first looks for Config beside the executable
// (the deployed layout), then for animatorConfig beside the ServoAnimator
// project folder (the live-development layout). Config > Set Paths… can
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
        /// automatically use a child `Config` folder in a deployment, or a sibling
        /// `animatorConfig` folder in the live development tree. A legacy
        /// Folder.json is loaded and re-saved as Paths.json automatically.</summary>
        public static FolderSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return NormalizeLoaded(JsonSerializer.Deserialize<FolderSettings>(
                        File.ReadAllText(FilePath)));

                if (File.Exists(LegacyFilePath))
                {
                    var migrated = JsonSerializer.Deserialize<FolderSettings>(
                        File.ReadAllText(LegacyFilePath));
                    migrated = NormalizeLoaded(migrated);
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

        /// <summary>Find the portable deployed Config folder first, then retain
        /// the established live-development animatorConfig discovery behavior.</summary>
        private static string FindSiblingAnimatorConfigFolder()
        {
            try
            {
                // Deployment layout:
                //   J5_Animator\AnimationEditorPlayer.exe
                //   J5_Animator\Config\...
                string deployedConfig = Path.Combine(AppContext.BaseDirectory, "Config");
                if (Directory.Exists(deployedConfig))
                    return Path.GetFullPath(deployedConfig);

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
            string config = Path.GetFullPath(ConfigFolderOrDefault);
            string storedConfig = ConfigPathService.IsWithin(AppContext.BaseDirectory, config)
                ? Path.GetRelativePath(AppContext.BaseDirectory, config)
                : config;
            var persisted = new FolderSettings
            {
                ConfigFolder = storedConfig,
                ProjectFolder = "Projects",
                LastSequenceFolder = ConfigPathService.IsWithin(config, LastSequenceFolder)
                    ? Path.GetRelativePath(config, Path.GetFullPath(LastSequenceFolder))
                    : "",
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                persisted, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static FolderSettings NormalizeLoaded(FolderSettings settings)
        {
            if (settings == null) return null;
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.ConfigFolder) &&
                    !Path.IsPathRooted(settings.ConfigFolder))
                    settings.ConfigFolder = Path.GetFullPath(Path.Combine(
                        AppContext.BaseDirectory, settings.ConfigFolder));

                if (!string.IsNullOrWhiteSpace(settings.ConfigFolder))
                {
                    settings.ProjectFolder = Path.Combine(settings.ConfigFolder, "Projects");
                    if (!string.IsNullOrWhiteSpace(settings.LastSequenceFolder) &&
                        !Path.IsPathRooted(settings.LastSequenceFolder))
                        settings.LastSequenceFolder = Path.GetFullPath(Path.Combine(
                            settings.ConfigFolder, settings.LastSequenceFolder));
                    if (!ConfigPathService.IsWithin(settings.ConfigFolder,
                                                     settings.LastSequenceFolder))
                        settings.LastSequenceFolder = "";
                }
            }
            catch { settings.LastSequenceFolder = ""; }
            return settings;
        }

        /// <summary>The config folder, falling back to the exe folder when
        /// unset or missing on disk.</summary>
        [JsonIgnore]
        public string ConfigFolderOrDefault =>
            !string.IsNullOrWhiteSpace(ConfigFolder) && Directory.Exists(ConfigFolder)
                ? Path.GetFullPath(ConfigFolder) : AppContext.BaseDirectory;

        /// <summary>The Projects folder is always directly under the selected
        /// Configuration folder. The persisted ProjectFolder field is retained
        /// only for backward-compatible Paths.json reading.</summary>
        [JsonIgnore]
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
