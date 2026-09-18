// ---------------------------------------------------------------------------
// Shared browser for selecting/managing Library Sequences and Library Poses.
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
        private readonly bool _isCommandMode;
        private readonly string _itemLabel;
        private readonly ObservableCollection<LibraryItemInfo> _items;
        private readonly ICollectionView _view;
        private readonly LibraryCategories _categories;
        private readonly Dictionary<LibraryItemInfo, string> _savedDescriptions;

        public LibraryItemInfo SelectedLibraryItem { get; private set; }
        public bool BreakPrecedingSplines { get; private set; }

        public LibraryItemSelectionWindow(string libraryFolder, bool manageMode,
                                          string itemLabel = "Library Sequence",
                                          bool showAudioFiles = true,
                                          string selectActionText = null, bool offerBreakPreceding = false)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "animation-library");
            _manageMode = manageMode;
            SelectBreakButton.Visibility = !manageMode && offerBreakPreceding ? Visibility.Visible : Visibility.Collapsed;
            SelectBreakButton.Content = itemLabel.EndsWith("Pose", StringComparison.OrdinalIgnoreCase)
                ? "Insert Pose, Break Preceding Splines" : "Insert Sequence, Break Preceding Splines";
            _itemLabel = itemLabel;
            _isCommandMode = itemLabel.EndsWith("Command", StringComparison.OrdinalIgnoreCase) ||
                             itemLabel.EndsWith("Pose", StringComparison.OrdinalIgnoreCase);
            _items = new ObservableCollection<LibraryItemInfo>(LibraryItemInfo.Scan(libraryFolder));
            _savedDescriptions = _items.ToDictionary(item => item, item => item.Description ?? "");
            _categories = new LibraryCategories(libraryFolder, _items);
            Closing += SaveDescriptionsOnClosing;
            CategoryPicker.ItemsSource = _categories.Names.ToList();
            CategoryPanel.Visibility = manageMode ? Visibility.Visible : Visibility.Collapsed;
            UpdateFolderColumn();
            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = LibraryFilter;
            _view.SortDescriptions.Add(new SortDescription(nameof(LibraryItemInfo.Category), ListSortDirection.Ascending));
            _view.SortDescriptions.Add(new SortDescription(nameof(LibraryItemInfo.FileName), ListSortDirection.Ascending));
            ItemsGrid.ItemsSource = _view;

            string plural = _isCommandMode ? "Library Poses" : "Library Sequences";
            Title = manageMode ? $"Manage {plural}" : $"Select {itemLabel}";
            string selectAction = selectActionText ??
                (_isCommandMode ? "Insert Selected Pose" : "Insert Selected Sequence");
            ModeText.Text = manageMode
                ? "Double-click a Description cell to edit it. Descriptions save automatically when this window closes. " +
                  (_isCommandMode ? "Select a row to set its category, choose an image, or delete the file." : "Select a row to set its category or delete the file.")
                : $"Select a row and click {selectAction} (or double-click it). " +
                  $"The list contains {plural.ToLowerInvariant()} sorted by category, then filename.";
            SelectButton.Content = selectAction;
            AudioFilesColumn.Visibility = showAudioFiles ? Visibility.Visible : Visibility.Collapsed;
            SearchBox.ToolTip = showAudioFiles
                ? "Filter by category, folder, filename, description, or audio filename"
                : "Filter by category, folder, filename, or description";

            ItemsGrid.IsReadOnly = !manageMode;
            PoseImageColumn.Visibility = _isCommandMode ? Visibility.Visible : Visibility.Collapsed;
            ChooseImageButton.Visibility = manageMode && _isCommandMode ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = manageMode ? Visibility.Visible : Visibility.Collapsed;
            SelectButton.Visibility = manageMode ? Visibility.Collapsed : Visibility.Visible;

            if (_items.Count > 0)
            {
                ItemsGrid.SelectedIndex = 0;
                ItemsGrid.ScrollIntoView(_items[0]);
            }
            else
            {
                SetEmptyState(_isCommandMode
                    ? "No JSON Library Poses were found."
                    : "No JSON Library Sequences were found.");
            }
        }

        private bool LibraryFilter(object obj)
        {
            if (obj is not LibraryItemInfo item) return false;
            string q = SearchBox?.Text?.Trim() ?? "";
            if (q.Length == 0) return true;
            return (item.Folder?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   item.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   (item.FileName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.AudioFiles?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!CommitDescriptionEdit()) return;
            _view?.Refresh();
            if (_view?.IsEmpty == false && ItemsGrid.SelectedItem == null)
                ItemsGrid.SelectedIndex = 0;
        }

        private LibraryItemInfo Current => ItemsGrid.SelectedItem as LibraryItemInfo;

        private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = Current;
            CategoryPicker.SelectedItem = _categories?.Names.FirstOrDefault(n => n.Equals(item?.Category ?? "none", StringComparison.OrdinalIgnoreCase));
            CategoryPicker.IsEnabled = SaveCategoryButton.IsEnabled = _manageMode && item?.IsValid == true;
            ChooseImageButton.IsEnabled = _manageMode && _isCommandMode && item?.IsValid == true;
            ErrorText.Foreground = Brushes.IndianRed;
            ErrorText.Text = item?.ReadError ?? "";
            DeleteButton.IsEnabled = _manageMode && item != null;
            SelectButton.IsEnabled = !_manageMode && item != null && item.IsValid;
            SelectBreakButton.IsEnabled = SelectButton.IsEnabled;

        }

        private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_manageMode && Current?.IsValid == true)
                SelectCurrent();
        }

        private void Select_Click(object sender, RoutedEventArgs e) => SelectCurrent();
        private void SelectBreak_Click(object sender, RoutedEventArgs e)
        {
            if (Current?.IsValid != true) return;
            BreakPrecedingSplines = true;
            SelectCurrent();
        }

        private void SelectCurrent()
        {
            var item = Current;
            if (item == null || !item.IsValid) return;
            SelectedLibraryItem = item;
            DialogResult = true;
        }

        private void ItemsGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            e.Cancel = !_manageMode || e.Row.Item is not LibraryItemInfo { IsValid: true };
        }

        private bool CommitDescriptionEdit() =>
            ItemsGrid == null ||
            (ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true) &&
             ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true));

        private void SaveDescriptionsOnClosing(object sender, CancelEventArgs e)
        {
            if (!_manageMode) return;
            if (!CommitDescriptionEdit())
            {
                e.Cancel = true;
                MessageBox.Show(this, "Finish editing the current description before closing.",
                    "Description not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save all changed rows, including those currently hidden by the search.
            // Update only description metadata, preserving categories and commands.
            foreach (var item in _items.Where(item => item.IsValid))
            {
                string description = item.Description ?? "";
                if (_savedDescriptions.TryGetValue(item, out string saved) && description == saved)
                    continue;
                try
                {
                    AnimationDocument.UpdateLibraryDescription(item.FullPath, description);
                    _savedDescriptions[item] = description;
                    item.Modified = File.GetLastWriteTime(item.FullPath);
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    MessageBox.Show(this,
                        $"Could not save the description for {item.FileName}:\n{ex.Message}\n\n" +
                        "The window will remain open so your edits are not lost. Resolve the problem and close again to retry.",
                        "Library update error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDescriptionEdit()) return;
            var item = Current;
            if (item == null) return;
            var answer = MessageBox.Show(this,
                $"Delete this {_itemLabel}?\n\n{item.FileName}\n\nThis cannot be undone.",
                $"Delete {_itemLabel}", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                string attachedImage = "";
                if (_isCommandMode && item.IsValid)
                {
                    try
                    {
                        var doc = AnimationDocument.LoadLibraryItem(item.FullPath);
                        if (!string.IsNullOrWhiteSpace(doc.ImageFile))
                        {
                            string jsonDir = Path.GetDirectoryName(Path.GetFullPath(item.FullPath)) ?? "";
                            string imagePath = Path.IsPathRooted(doc.ImageFile)
                                ? Path.GetFullPath(doc.ImageFile)
                                : Path.GetFullPath(Path.Combine(jsonDir, doc.ImageFile));
                            // Delete only an attachment stored alongside the JSON;
                            // never delete an arbitrary external absolute image.
                            if (string.Equals(Path.GetDirectoryName(imagePath), jsonDir,
                                              StringComparison.OrdinalIgnoreCase))
                                attachedImage = imagePath;
                        }
                    }
                    catch { }
                }

                File.Delete(item.FullPath);
                if (!string.IsNullOrWhiteSpace(attachedImage) && File.Exists(attachedImage))
                {
                    try { File.Delete(attachedImage); } catch { }
                }

                _items.Remove(item);
                _savedDescriptions.Remove(item);
                UpdateFolderColumn();
                _view.Refresh();
                if (_view.IsEmpty)
                    SetEmptyState(_isCommandMode
                        ? "No JSON Library Poses were found."
                        : "No JSON Library Sequences were found.");
                else
                    ItemsGrid.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not delete the {_itemLabel}:\n" + ex.Message,
                                "Library delete error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void SetEmptyState(string message)
        {
            ChooseImageButton.IsEnabled = false;
            CategoryPicker.IsEnabled = SaveCategoryButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            SelectButton.IsEnabled = false;
            SelectBreakButton.IsEnabled = false;
            ErrorText.Foreground = Brushes.IndianRed;
            ErrorText.Text = message;
        }

        private void UpdateFolderColumn() => FolderColumn.Visibility = _items.Any(i => !string.IsNullOrWhiteSpace(i.Folder))
            ? Visibility.Visible : Visibility.Collapsed;

        private void SaveCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDescriptionEdit() || Current?.IsValid != true || CategoryPicker.SelectedItem is not string category) return;
            try
            {
                _categories.Assign(Current, category);
                _view.Refresh();
                ErrorText.Foreground = Brushes.LightGreen;
                ErrorText.Text = "Category saved.";
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Category save error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDescriptionEdit()) return;
            new LibraryCategoryWindow(this, _categories).ShowDialog();
            CategoryPicker.ItemsSource = _categories.Names.ToList();
            CategoryPicker.SelectedItem = _categories.Names.FirstOrDefault(n => n.Equals(Current?.Category ?? "none", StringComparison.OrdinalIgnoreCase));
            _view.Refresh();
        }

        private void ChooseImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_manageMode || !_isCommandMode || !CommitDescriptionEdit() || Current?.IsValid != true) return;
            var item = Current;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose Image for " + item.FileName,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                LibraryPoseImages.Attach(item, dialog.FileName);
                ErrorText.Foreground = Brushes.LightGreen;
                ErrorText.Text = "Image saved for " + item.FileName + ".";
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "Could not attach the image:\n" + error.Message,
                    "Pose image", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
