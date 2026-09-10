using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    /// <summary>Small shared editor for Sequence and Movie descriptions.</summary>
    internal sealed class DescriptionEditWindow : Window
    {
        private readonly TextBox _descriptionBox;

        public string DescriptionText => _descriptionBox.Text ?? "";

        public DescriptionEditWindow(Window owner, string documentKind, string description)
        {
            Owner = owner;
            Title = $"Edit {documentKind} Description";
            Width = 560;
            Height = 260;
            MinWidth = 400;
            MinHeight = 190;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            var layout = new DockPanel { Margin = new Thickness(14) };
            Content = layout;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0),
            };
            var save = new Button
            {
                Content = "Save description",
                IsDefault = true,
                Padding = new Thickness(12, 5, 12, 5),
            };
            save.Click += (_, _) => DialogResult = true;
            buttons.Children.Add(save);
            buttons.Children.Add(new Button
            {
                Content = "Cancel",
                IsCancel = true,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 5, 12, 5),
            });
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(buttons);

            _descriptionBox = new TextBox
            {
                Text = description ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8),
            };
            layout.Children.Add(_descriptionBox);
            Loaded += (_, _) =>
            {
                _descriptionBox.Focus();
                _descriptionBox.CaretIndex = _descriptionBox.Text.Length;
            };
        }
    }
}
