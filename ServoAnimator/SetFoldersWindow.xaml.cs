// ---------------------------------------------------------------------------
// SetFoldersWindow.xaml.cs
//
// Edits the Configuration folder. Projects is always Configuration\Projects.
// Shown modally on first run only when neither Paths.json nor a sibling
// animatorConfig folder is available, and from Config > Set Paths… afterwards.
// OK writes Paths.json; Cancel leaves everything untouched (on first run the app
// falls back to the exe folder for that session and discovery/prompting runs
// again next start).
//
// Folder picking uses Microsoft.Win32.OpenFolderDialog (WPF, .NET 10).
// ---------------------------------------------------------------------------

using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ServoAnimator
{
    public partial class SetFoldersWindow : Window
    {
        private readonly FolderSettings _settings;

        public SetFoldersWindow(FolderSettings settings, bool firstRun)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "files-configuration");
            _settings = settings;
            ConfigBox.Text = settings.ConfigFolder ?? "";
            if (firstRun)
                IntroText.Text = "Welcome! Since this is the first run, " +
                    "please choose the folder holding the servo configuration " +
                    "files (and the TIC\\ folder with Pololu's ticcmd). " +
                    "The Projects folder directly beneath it is used for " +
                    "sequences, movies, source audio, and exports. This path is " +
                    "saved in Paths.json beside the executable and read " +
                    "automatically from now on (Config > Set Paths… edits " +
                    "them later).";
        }

        private void Browse(System.Windows.Controls.TextBox box, string title)
        {
            var dlg = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = string.IsNullOrWhiteSpace(box.Text)
                                   ? AppContext.BaseDirectory : box.Text,
            };
            if (dlg.ShowDialog(this) == true)
                box.Text = dlg.FolderName;
        }

        private void BrowseConfig_Click(object sender, RoutedEventArgs e) =>
            Browse(ConfigBox, "Select the configuration folder");

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _settings.ConfigFolder = ConfigBox.Text?.Trim() ?? "";
            _settings.ProjectFolder = Path.Combine(_settings.ConfigFolder, "Projects");
            try { Directory.CreateDirectory(_settings.ProjectFolder); } catch { }
            try
            {
                _settings.Save();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "Could not save Paths.json:\n" + ex.Message,
                                "Save error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
