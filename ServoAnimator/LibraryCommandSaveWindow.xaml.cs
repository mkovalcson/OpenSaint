using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ServoAnimator
{
    /// <summary>Prompts for the filename, description and optional image for a
    /// single-time-point Library Command. The caller owns the JSON/image write.</summary>
    public partial class LibraryCommandSaveWindow : Window
    {
        public string FileNameText { get; private set; } = "";
        public string DescriptionText { get; private set; } = "";
        public string ImageSourcePath { get; private set; } = "";

        public LibraryCommandSaveWindow()
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "animation-library");
            FileNameBox.SelectAll();
            Loaded += (_, _) => FileNameBox.Focus();
        }

        private void AttachImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Attach Library Command Image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) != true) return;
            ImageSourcePath = dlg.FileName;
            ImagePathText.Text = Path.GetFileName(dlg.FileName);
            ImagePathText.ToolTip = dlg.FileName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = (FileNameBox.Text ?? "").Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Enter a file name for the Library Command.",
                                "Create Library Command", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name += ".json";

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains(Path.DirectorySeparatorChar) ||
                name.Contains(Path.AltDirectorySeparatorChar))
            {
                MessageBox.Show(this, "The file name contains characters that are not valid in a Windows file name.",
                                "Create Library Command", MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            FileNameText = name;
            DescriptionText = DescriptionBox.Text ?? "";
            DialogResult = true;
        }
    }
}
