// ---------------------------------------------------------------------------
// LibraryItemInfo.cs
//
// Display model and recursive scanner for Animation Library JSON files.
// ---------------------------------------------------------------------------

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ServoAnimator
{
    public class LibraryItemInfo : INotifyPropertyChanged
    {
        private string _description = "";
        private DateTime _modified;

        public string FullPath { get; init; } = "";
        public string RelativePath { get; init; } = "";
        public string AudioFiles { get; init; } = "";
        public string ReadError { get; init; } = "";
        public bool IsValid => string.IsNullOrEmpty(ReadError);

        public DateTime Modified
        {
            get => _modified;
            set
            {
                _modified = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModifiedDisplay));
            }
        }

        public string ModifiedDisplay => Modified.ToString("yyyy-MM-dd HH:mm");

        public string Description
        {
            get => _description;
            set
            {
                _description = value ?? "";
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static List<LibraryItemInfo> Scan(string rootFolder)
        {
            var results = new List<LibraryItemInfo>();
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                return results;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(rootFolder, "*.json",
                                                  SearchOption.AllDirectories).ToList();
            }
            catch
            {
                return results;
            }

            foreach (string path in files)
            {
                string relative;
                try { relative = Path.GetRelativePath(rootFolder, path); }
                catch { relative = path; }

                try
                {
                    var item = AnimationDocument.LoadLibraryItem(path);
                    string audio = string.Join(", ", item.Commands
                        .Where(c => c.Servo == ServoNames.Play)
                        .OrderBy(c => c.OffsetSeconds)
                        .Select(c => Path.GetFileName(c.TextValue ?? ""))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                    results.Add(new LibraryItemInfo
                    {
                        FullPath = path,
                        RelativePath = relative,
                        Modified = File.GetLastWriteTime(path),
                        Description = item.Description ?? "",
                        AudioFiles = audio,
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new LibraryItemInfo
                    {
                        FullPath = path,
                        RelativePath = relative,
                        Modified = File.GetLastWriteTime(path),
                        Description = "[Unreadable JSON]",
                        ReadError = ex.Message,
                    });
                }
            }

            return results
                .OrderBy(i => i.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
