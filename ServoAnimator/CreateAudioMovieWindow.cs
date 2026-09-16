using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ServoAnimator
{
    internal sealed class CreateAudioMovieWindow : Window
    {
        public string MovieName { get; private set; }
        public string ProjectFolder { get; private set; }

        public CreateAudioMovieWindow(Window owner, string initialFolder)
        {
            Owner = owner;
            Title = "Create Movie from Audio Files";
            Width = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            var panel = new StackPanel { Margin = new Thickness(16) };
            Content = panel;
            panel.Children.Add(new TextBlock { Text = "Movie name" });
            var name = new TextBox { Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(6) };
            panel.Children.Add(name);
            panel.Children.Add(new TextBlock { Text = "Project folder containing audio files (inside Configuration)" });
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 12) };
            var browse = new Button { Content = "Browse…", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            var folder = new TextBox { Text = initialFolder, Padding = new Thickness(6) };
            row.Children.Add(folder);
            panel.Children.Add(row);
            browse.Click += (_, _) =>
            {
                var picker = new OpenFolderDialog { Title = "Select project folder with audio files", InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : initialFolder };
                if (picker.ShowDialog(this) == true) folder.Text = picker.FolderName;
            };
            panel.Children.Add(new TextBlock
            {
                Text = "Numbered MP3, WAV, AIFF, WMA and M4A files in this folder become sequences in numeric order (1, 2, 10). Unnumbered files and subfolders are ignored. The movie is saved here, with sequences in a new <movie name>.Sequences folder. Existing files are never overwritten.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14)
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var create = new Button { Content = "Create", IsDefault = true, Padding = new Thickness(12, 5, 12, 5) };
            buttons.Children.Add(create);
            buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 5, 12, 5) });
            panel.Children.Add(buttons);
            create.Click += (_, _) =>
            {
                if (!PendingMovieSequence.TryName(name.Text, out string validName))
                {
                    MessageBox.Show(this, "Enter a valid movie filename, up to 150 characters.", Title);
                    name.Focus();
                    return;
                }
                if (!Directory.Exists(folder.Text.Trim()))
                {
                    MessageBox.Show(this, "Choose an existing project folder.", Title);
                    return;
                }
                MovieName = validName;
                ProjectFolder = Path.GetFullPath(folder.Text.Trim());
                DialogResult = true;
            };
            Loaded += (_, _) => name.Focus();
        }
    }
}
