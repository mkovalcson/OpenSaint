// ---------------------------------------------------------------------------
// Shared browser for selecting/managing Library Sequences and Library Commands.
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using Microsoft.Win32;

namespace ServoAnimator
{
    public partial class LibraryItemSelectionWindow : Window
    {
        private readonly bool _manageMode;
        private readonly bool _isCommandMode;
        private readonly string _itemLabel;
        private readonly ObservableCollection<LibraryItemInfo> _items;
        private readonly ICollectionView _view;

        public LibraryItemInfo SelectedLibraryItem { get; private set; }

        public LibraryItemSelectionWindow(string libraryFolder, bool manageMode,
                                          string itemLabel = "Library Sequence",
                                          bool showAudioFiles = true)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "animation-library");
            _manageMode = manageMode;
            _itemLabel = itemLabel;
            _isCommandMode = itemLabel.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
            _items = new ObservableCollection<LibraryItemInfo>(LibraryItemInfo.Scan(libraryFolder));
            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = LibraryFilter;
            ItemsGrid.ItemsSource = _view;

            string plural = _isCommandMode ? "Library Commands" : "Library Sequences";
            Title = manageMode ? $"Manage {plural}" : $"Select {itemLabel}";
            string selectAction = _isCommandMode ? "Insert Selected Command" : "Insert Selected Sequence";
            ModeText.Text = manageMode
                ? $"Select a {itemLabel.ToLowerInvariant()}. Edit its description or delete the selected file. " +
                  "Folder and filename are shown separately; child folders are scanned recursively."
                : $"Select a row and click {selectAction} (or double-click it). " +
                  $"The list contains {plural.ToLowerInvariant()} alphabetically by folder and filename.";
            DescriptionLabel.Text = $"Selected {itemLabel.ToLowerInvariant()} description:";
            SelectButton.Content = selectAction;
            AudioFilesColumn.Visibility = showAudioFiles ? Visibility.Visible : Visibility.Collapsed;
            SearchBox.ToolTip = showAudioFiles
                ? "Filter by folder, filename, description, or audio filename"
                : "Filter by folder, filename, or description";

            DescriptionEditor.IsReadOnly = !manageMode;
            SaveDescriptionButton.Visibility = manageMode ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = manageMode ? Visibility.Visible : Visibility.Collapsed;
            ImageButton.Visibility = manageMode && _isCommandMode ? Visibility.Visible : Visibility.Collapsed;
            SelectButton.Visibility = manageMode ? Visibility.Collapsed : Visibility.Visible;

            // Only Library Commands support attached images. Keep the Library
            // Sequence description editor at full width.
            if (!_isCommandMode)
            {
                ImagePanel.Visibility = Visibility.Collapsed;
                ImageColumn.Width = new GridLength(0);
                DescriptionEditor.Margin = new Thickness(0);
            }

            if (_items.Count > 0)
            {
                ItemsGrid.SelectedIndex = 0;
                ItemsGrid.ScrollIntoView(_items[0]);
            }
            else
            {
                SetEmptyState(_isCommandMode
                    ? "No JSON Library Commands were found."
                    : "No JSON Library Sequences were found.");
            }
        }

        private bool LibraryFilter(object obj)
        {
            if (obj is not LibraryItemInfo item) return false;
            string q = SearchBox?.Text?.Trim() ?? "";
            if (q.Length == 0) return true;
            return (item.Folder?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.FileName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
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
            DeleteButton.IsEnabled = _manageMode && item != null;
            ImageButton.IsEnabled = _manageMode && _isCommandMode && item != null && item.IsValid;
            SelectButton.IsEnabled = !_manageMode && item != null && item.IsValid;

            if (_isCommandMode)
            {
                CommandImage.Source = item?.ImageSource;
                NoImageText.Visibility = item?.ImageSource == null ? Visibility.Visible : Visibility.Collapsed;
                ImageButton.Content = item?.ImageSource == null ? "Add Image…" : "Change Image…";
            }
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
                MessageBox.Show(this, $"Could not update the {_itemLabel}:\n" + ex.Message,
                                "Library update error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void AddChangeImage_Click(object sender, RoutedEventArgs e)
        {
            var item = Current;
            if (!_manageMode || !_isCommandMode || item == null || !item.IsValid) return;

            var dlg = new OpenFileDialog
            {
                Title = item.ImageSource == null ? "Add Library Command Image" : "Change Library Command Image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                string jsonPath = Path.GetFullPath(item.FullPath);
                string jsonDir = Path.GetDirectoryName(jsonPath) ?? "";
                string ext = Path.GetExtension(dlg.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                string destination = Path.Combine(jsonDir,
                    Path.GetFileNameWithoutExtension(jsonPath) + "-image" + ext.ToLowerInvariant());

                string oldLocalImage = "";
                try
                {
                    var oldDoc = AnimationDocument.LoadLibraryItem(jsonPath);
                    if (!string.IsNullOrWhiteSpace(oldDoc.ImageFile))
                    {
                        string oldPath = Path.IsPathRooted(oldDoc.ImageFile)
                            ? Path.GetFullPath(oldDoc.ImageFile)
                            : Path.GetFullPath(Path.Combine(jsonDir, oldDoc.ImageFile));
                        if (string.Equals(Path.GetDirectoryName(oldPath), jsonDir,
                                          StringComparison.OrdinalIgnoreCase))
                            oldLocalImage = oldPath;
                    }
                }
                catch { }

                string source = Path.GetFullPath(dlg.FileName);
                if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                    File.Copy(source, destination, overwrite: true);

                AnimationDocument.UpdateLibraryImage(jsonPath, Path.GetFileName(destination));

                if (!string.IsNullOrWhiteSpace(oldLocalImage) &&
                    !string.Equals(oldLocalImage, destination, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(oldLocalImage))
                {
                    try { File.Delete(oldLocalImage); } catch { }
                }

                item.SetImagePath(destination);
                item.Modified = File.GetLastWriteTime(jsonPath);
                CommandImage.Source = item.ImageSource;
                NoImageText.Visibility = item.ImageSource == null ? Visibility.Visible : Visibility.Collapsed;
                ImageButton.Content = item.ImageSource == null ? "Add Image…" : "Change Image…";
                ErrorText.Foreground = Brushes.LightGreen;
                ErrorText.Text = "Library Command image saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not attach the Library Command image:\n" + ex.Message,
                                "Library image error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
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
                _view.Refresh();
                if (_view.IsEmpty)
                    SetEmptyState(_isCommandMode
                        ? "No JSON Library Commands were found."
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
            DescriptionEditor.Text = "";
            DescriptionEditor.IsEnabled = false;
            SaveDescriptionButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            ImageButton.IsEnabled = false;
            SelectButton.IsEnabled = false;
            CommandImage.Source = null;
            NoImageText.Visibility = Visibility.Visible;
            ErrorText.Foreground = Brushes.IndianRed;
            ErrorText.Text = message;
        }
    }
}
