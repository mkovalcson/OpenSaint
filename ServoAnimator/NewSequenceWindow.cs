using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    internal sealed class NewSequenceWindow : Window
    {
        public string SequenceName { get; private set; }

        public NewSequenceWindow(Window owner)
        {
            Owner = owner;
            Title = "New Movie Sequence";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            var layout = new StackPanel { Margin = new Thickness(16) };
            Content = layout;
            layout.Children.Add(new TextBlock { Text = "Sequence name", Margin = new Thickness(0, 0, 0, 6) });
            var name = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 155, Padding = new Thickness(6) };
            layout.Children.Add(name);
            layout.Children.Add(new TextBlock
            {
                Text = "Start an empty Sequence. It will be added after the last Movie block when you save it.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 12),
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var create = new Button { Content = "Create", IsDefault = true, Padding = new Thickness(12, 5, 12, 5) };
            create.Click += (_, _) =>
            {
                if (!PendingMovieSequence.TryName(name.Text, out string validName))
                {
                    MessageBox.Show(this, "Enter a valid file name (up to 150 characters), without a folder path or reserved filename characters.",
                        "Sequence name", MessageBoxButton.OK, MessageBoxImage.Information);
                    name.Focus();
                    return;
                }
                SequenceName = validName;
                DialogResult = true;
            };
            buttons.Children.Add(create);
            buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 5, 12, 5) });
            layout.Children.Add(buttons);
            Loaded += (_, _) => name.Focus();
        }
    }
}
