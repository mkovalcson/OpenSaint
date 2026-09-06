using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ServoAnimator
{
    public enum MissingFileKind
    {
        PrimaryAudio,
        InsertedAudio,
        MovieSequence,
    }

    public sealed class MissingFileReference : INotifyPropertyChanged
    {
        public MissingFileKind Kind { get; init; }
        public string MissingPath { get; init; } = "";
        public object Owner { get; init; }
        public string ReplacementPath { get; set; } = "";
        public bool Remove { get; set; }
        public string DisplayKind => Kind switch
        {
            MissingFileKind.PrimaryAudio => "Sequence audio",
            MissingFileKind.InsertedAudio => "Inserted audio",
            _ => "Movie sequence",
        };
        public string ResolutionText => Remove ? "Remove reference" :
            (string.IsNullOrWhiteSpace(ReplacementPath) ? "Not resolved" : ReplacementPath);
        public void Refresh() => PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(ResolutionText)));
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class MissingFileRepairWindow : Window
    {
        private readonly string _configFolder;
        public ObservableCollection<MissingFileReference> Files { get; }

        public MissingFileRepairWindow(IEnumerable<MissingFileReference> files,
                                       string configFolder)
        {
            InitializeComponent();
            _configFolder = configFolder;
            Files = new ObservableCollection<MissingFileReference>(files ?? Array.Empty<MissingFileReference>());
            DataContext = Files;
            if (Files.Count > 0) FilesGrid.SelectedIndex = 0;
        }

        private MissingFileReference Selected => FilesGrid.SelectedItem as MissingFileReference;

        private static string FilterFor(MissingFileReference item) => item?.Kind switch
        {
            MissingFileKind.MovieSequence => "Sequence JSON files (*.json)|*.json|All files (*.*)|*.*",
            _ => "Audio files (*.mp3;*.wav;*.aiff;*.wma;*.m4a)|*.mp3;*.wav;*.aiff;*.wma;*.m4a|All files (*.*)|*.*",
        };

        private string ChooseReplacement(MissingFileReference item)
        {
            if (item == null) return null;
            var dialog = new OpenFileDialog
            {
                Title = "Locate " + Path.GetFileName(item.MissingPath),
                Filter = FilterFor(item),
                FileName = Path.GetFileName(item.MissingPath),
                InitialDirectory = _configFolder,
            };
            if (dialog.ShowDialog(this) != true) return null;
            if (ConfigPathService.IsWithin(_configFolder, dialog.FileName))
                return dialog.FileName;
            MessageBox.Show(this,
                "Replacement files must be inside the Configuration folder or one of its child folders:\n\n" +
                _configFolder,
                "File outside Configuration folder", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        private void Locate_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected;
            string path = ChooseReplacement(item);
            if (path == null) return;
            item.ReplacementPath = path;
            item.Remove = false;
            item.Refresh();
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected;
            string path = ChooseReplacement(selected);
            if (path == null) return;
            foreach (var item in Files.Where(f => string.Equals(f.MissingPath,
                         selected.MissingPath, StringComparison.OrdinalIgnoreCase)))
            {
                item.ReplacementPath = path;
                item.Remove = false;
                item.Refresh();
            }
        }

        private void SearchFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Search for missing files",
                InitialDirectory = _configFolder,
            };
            if (dialog.ShowDialog(this) != true) return;
            if (!ConfigPathService.IsWithin(_configFolder, dialog.FolderName))
            {
                MessageBox.Show(this,
                    "The search folder must be inside the Configuration folder:\n\n" +
                    _configFolder,
                    "Folder outside Configuration folder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int found = 0;
            foreach (var item in Files.Where(f => !f.Remove && string.IsNullOrWhiteSpace(f.ReplacementPath)))
            {
                try
                {
                    string name = Path.GetFileName(item.MissingPath);
                    string match = Directory.EnumerateFiles(dialog.FolderName, name,
                        SearchOption.AllDirectories).FirstOrDefault();
                    if (match == null) continue;
                    item.ReplacementPath = match;
                    item.Refresh();
                    found++;
                }
                catch (Exception) { }
            }
            MessageBox.Show(this, $"Found {found} of {Files.Count} missing file(s).",
                "Search complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            Selected.Remove = true;
            Selected.ReplacementPath = "";
            Selected.Refresh();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
