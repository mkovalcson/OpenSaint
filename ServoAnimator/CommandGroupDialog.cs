using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    internal sealed class CommandGroupDialog : Window
    {
        public double Offset { get; private set; }
        public int Repetitions { get; private set; } = 1;
        public CommandGroupDialog(Window owner, bool repeat, double mean)
        {
            Owner = owner; Title = repeat ? "Repeat Commands" : "Uniform Offset";
            Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
            var panel = new StackPanel { Margin = new Thickness(16) }; Content = panel;
            panel.Children.Add(new TextBlock
            {
                Text = repeat ? "Append copies after the last selected command. Offset is the gap before each repetition."
                    : $"Mean time between selected triangles: {mean:0.######} seconds. The first stays in place; the rest keep their order.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
            });
            var repetitions = new TextBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Text = "1", Padding = new Thickness(5) };
            repetitions.SetResourceReference(TextBox.StyleProperty, "NumericInputStyle");
            if (repeat)
            {
                panel.Children.Add(new TextBlock { Text = "Repetitions (additional copies):" });
                panel.Children.Add(repetitions);
            }
            panel.Children.Add(new TextBlock { Text = "Offset (seconds):", Margin = new Thickness(0, 8, 0, 4) });
            var offset = new TextBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Text = (repeat ? 0 : mean).ToString("0.######", CultureInfo.CurrentCulture), Padding = new Thickness(5) };
            offset.SetResourceReference(TextBox.StyleProperty, "NumericInputStyle");
            panel.Children.Add(offset);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var apply = new Button { Content = "Apply", IsDefault = true, Padding = new Thickness(12, 5, 12, 5) };
            apply.Click += (_, _) =>
            {
                double value = repeat ? 0 : mean;
                bool valid = string.IsNullOrWhiteSpace(offset.Text) ||
                    double.TryParse(offset.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                    double.TryParse(offset.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                if (!valid || !double.IsFinite(value) || value < 0 ||
                    !int.TryParse(repetitions.Text, out int count) || count < 1 || count > 10000)
                {
                    MessageBox.Show(this, "Enter a nonnegative decimal offset and 1–10,000 repetitions.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Offset = value; Repetitions = count; DialogResult = true;
            };
            buttons.Children.Add(apply);
            buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) });
            panel.Children.Add(buttons);
            Loaded += (_, _) => { offset.Focus(); offset.SelectAll(); };
        }
    }
}
