// ---------------------------------------------------------------------------
// LibraryItemSelectionWindow.xaml.cs
//
// Shared browser for inserting and managing Animation Library items.
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace ServoAnimator
{
    public partial class LibraryItemSelectionWindow : Window
    {
        private readonly bool _manageMode;
        private readonly ObservableCollection<LibraryItemInfo> _items;
        private readonly ICollectionView _view;

        public LibraryItemInfo SelectedLibraryItem { get; private set; }

        public LibraryItemSelectionWindow(string libraryFolder, bool manageMode)
        {
            InitializeComponent();
            _manageMode = manageMode;
            _items = new ObservableCollection<LibraryItemInfo>(
                LibraryItemInfo.Scan(libraryFolder));
            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = LibraryFilter;
            ItemsGrid.ItemsSource = _view;

            Title = manageMode ? "Manage Animation Library Items"
                               : "Select Animation Library Item";
            ModeText.Text = manageMode
                ? "Select a library item. Edit its description below, then click Save Description. " +
                  "Files are listed alphabetically by pathname, including child folders."
                : "Select a row and click Use Selected Item (or double-click it). Files are listed " +
                  "alphabetically by pathname, including child folders.";

            DescriptionEditor.IsReadOnly = !manageMode;
            SaveDescriptionButton.Visibility = manageMode
                ? Visibility.Visible : Visibility.Collapsed;
            SelectButton.Visibility = manageMode
                ? Visibility.Collapsed : Visibility.Visible;

            if (_items.Count > 0)
            {
                ItemsGrid.SelectedIndex = 0;
                ItemsGrid.ScrollIntoView(_items[0]);
            }
            else
            {
                DescriptionEditor.IsEnabled = false;
                SaveDescriptionButton.IsEnabled = false;
                SelectButton.IsEnabled = false;
                ErrorText.Text = "No JSON library items were found.";
            }
        }


        private bool LibraryFilter(object obj)
        {
            if (obj is not LibraryItemInfo item) return false;
            string q = SearchBox?.Text?.Trim() ?? "";
            if (q.Length == 0) return true;
            return (item.RelativePath?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.AudioFiles?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _view?.Refresh();
            if (_view?.IsEmpty == false && ItemsGrid.SelectedItem == null)
                ItemsGrid.SelectedIndex = 0;
        }

        private LibraryItemInfo Current => ItemsGrid.SelectedItem as LibraryItemInfo;

        private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = Current;
            DescriptionEditor.Text = item?.Description ?? "";
            ErrorText.Foreground = Brushes.IndianRed;
            ErrorText.Text = item?.ReadError ?? "";
            DescriptionEditor.IsEnabled = item != null && item.IsValid;
            SaveDescriptionButton.IsEnabled = _manageMode && item != null && item.IsValid;
            SelectButton.IsEnabled = !_manageMode && item != null && item.IsValid;
        }

        private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_manageMode && Current?.IsValid == true)
                SelectCurrent();
        }

        private void Select_Click(object sender, RoutedEventArgs e) => SelectCurrent();

        private void SelectCurrent()
        {
            var item = Current;
            if (item == null || !item.IsValid) return;
            SelectedLibraryItem = item;
            DialogResult = true;
        }

        private void SaveDescription_Click(object sender, RoutedEventArgs e)
        {
            var item = Current;
            if (item == null || !item.IsValid) return;

            try
            {
                string description = DescriptionEditor.Text ?? "";
                AnimationDocument.UpdateLibraryDescription(item.FullPath, description);
                item.Description = description;
                item.Modified = File.GetLastWriteTime(item.FullPath);
                ErrorText.Foreground = Brushes.LightGreen;
                ErrorText.Text = "Description saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not update the library item:\n" + ex.Message,
                                "Library update error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

    }
}
