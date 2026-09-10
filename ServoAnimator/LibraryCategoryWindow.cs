using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    internal sealed class LibraryCategoryWindow : Window
    {
        public LibraryCategoryWindow(Window owner, LibraryCategories categories)
        {
            Owner = owner; Title = "Manage Library Categories"; Width = 460; Height = 390;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
            var panel = new DockPanel { Margin = new Thickness(14) }; Content = panel;
            var controls = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(controls, Dock.Bottom); panel.Children.Add(controls);
            var name = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 100, Padding = new Thickness(5), ToolTip = "Category name" };
            controls.Children.Add(name);
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; controls.Children.Add(buttons);
            var list = new ListBox { ItemsSource = categories.Names.ToList() }; panel.Children.Add(list);
            list.SelectionChanged += (_, _) => name.Text = list.SelectedItem as string ?? "";
            void Button(string label, Action action)
            {
                var button = new Button { Content = label, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) };
                button.Click += (_, _) =>
                {
                    try { action(); list.ItemsSource = categories.Names.ToList(); }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Categories", MessageBoxButton.OK, MessageBoxImage.Warning); }
                };
                buttons.Children.Add(button);
            }
            Button("Add", () => categories.Add(name.Text));
            Button("Rename", () => { if (list.SelectedItem is string old) categories.RenameOrDelete(old, name.Text); });
            Button("Delete", () =>
            {
                if (list.SelectedItem is string old && old != "none" && MessageBox.Show(this,
                    $"Delete category '{old}'? Its items will be assigned to 'none'; no Library files will be deleted.",
                    "Delete category", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    categories.RenameOrDelete(old, null);
            });
            Button("Close", Close);
        }
    }
}
