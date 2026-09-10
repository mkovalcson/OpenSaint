using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator
{
    internal sealed class CommandConflictWindow : Window
    {
        private readonly List<ServoCommand> _selected = new();

        private CommandConflictWindow(Window owner, IReadOnlyList<CommandConflicts.Group> groups)
        {
            Owner = owner;
            Title = "Redundant commands — choose which to keep";
            Width = 850;
            Height = 580;
            MinWidth = 550;
            MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(242, 242, 242));
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));

            var layout = new DockPanel { Margin = new Thickness(16) };
            Content = layout;
            var explanation = new TextBlock
            {
                Text = "More than one command targets the same servo/control at the same time. " +
                       "Choose one command in each group. Earlier commands appear first; newer commands appear below them. " +
                       "Keep selected removes the other commands in these groups. Cancel leaves this operation unapplied.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
            };
            explanation.Foreground = new SolidColorBrush(Color.FromRgb(72, 47, 0));
            DockPanel.SetDock(explanation, Dock.Top);
            layout.Children.Add(explanation);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0),
            };
            var keep = new Button { Content = "Keep selected", Padding = new Thickness(14, 6, 14, 6) };
            keep.Click += (_, _) => DialogResult = true;
            buttons.Children.Add(keep);
            buttons.Children.Add(new Button
            {
                Content = "Cancel", IsCancel = true, Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(14, 6, 14, 6),
            });
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(buttons);

            var list = new StackPanel();
            layout.Children.Add(new ScrollViewer
            {
                Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });
            for (int index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                int groupIndex = index;
                _selected.Add(group.Commands[0]);
                var panel = new StackPanel { Margin = new Thickness(12) };
                var border = new Border
                {
                    Child = panel, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 0, 12),
                    Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
                };
                list.Children.Add(border);
                panel.Children.Add(new TextBlock
                {
                    Text = $"{group.Time:F3} s — {group.Servo}" +
                           (group.Control.HasValue ? $" / {group.Control}" : ""),
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
                });
                for (int candidate = 0; candidate < group.Commands.Count; candidate++)
                {
                    ServoCommand command = group.Commands[candidate];
                    string order = candidate == 0 ? "earliest" :
                                   candidate == group.Commands.Count - 1 ? "most recent" : "later";
                    var choice = new RadioButton
                    {
                        GroupName = $"Conflict{index}", IsChecked = candidate == 0,
                        Margin = new Thickness(0, 5, 0, 5),
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Content = new TextBlock
                        {
                            Text = $"{candidate + 1}. ({order})  Value: {command.ValueDisplay}" +
                                   $"    Disable: {(command.Disable ? "Yes" : "No")}    Speed: {command.SpeedDisplay}" +
                                   (string.IsNullOrWhiteSpace(command.ColorHex) ? "" : $"    Color: {command.ColorHex}") +
                                   (string.IsNullOrWhiteSpace(command.Reason) ? "" : $"\nReason: {command.Reason}"),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                        },
                    };
                    choice.Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                    choice.Checked += (_, _) => _selected[groupIndex] = command;
                    panel.Children.Add(choice);
                }
            }
        }

        /// <summary>No mutation on Cancel, including the window close button.</summary>
        public static bool Resolve(Window owner, List<ServoCommand> commands)
        {
            var groups = CommandConflicts.Find(commands);
            if (groups.Count == 0) return true;
            var window = new CommandConflictWindow(owner, groups);
            if (window.ShowDialog() != true) return false;
            CommandConflicts.KeepSelected(commands, groups, window._selected);
            return true;
        }
    }
}
