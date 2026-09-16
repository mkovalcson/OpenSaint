using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void LibraryPoseImageChecks()
    {
        var app = new App();
        app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        app.InitializeComponent();
        string root = Path.Combine(Path.GetTempPath(), "pose-image-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string library = Path.Combine(root, "Library"); Directory.CreateDirectory(library);
            string pose = Path.Combine(library, "Greeting.json"), source = Path.Combine(root, "source.png");
            File.WriteAllText(pose, "{\"description\":\"Original description\",\"category\":\"Gestures\",\"futureField\":42,\"commands\":[]}");
            var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(source)) encoder.Save(output);
            byte[] original = File.ReadAllBytes(source);
            var manager = new LibraryItemSelectionWindow(library, true, "Library Pose", false);
            try
            {
                var grid = (DataGrid)manager.FindName("ItemsGrid");
                var button = (Button)manager.FindName("ChooseImageButton");
                var item = (LibraryItemInfo)grid.SelectedItem;
                Check(button.Visibility == Visibility.Visible && button.IsEnabled, "Manage Library Poses offers an enabled image chooser for the selected valid pose.");
                item.Description = "Pending description edit";
                LibraryPoseImages.Attach(item, source);
                var saved = JsonNode.Parse(File.ReadAllText(pose));
                string attachment = Path.Combine(library, (string)saved["image"]);
                Check(!Path.IsPathRooted((string)saved["image"]) && attachment != source && original.SequenceEqual(File.ReadAllBytes(attachment)),
                    "Selected artwork is copied beside the pose with a portable relative reference.");
                Check((string)saved["description"] == "Original description" && (string)saved["category"] == "Gestures" && (int)saved["futureField"] == 42
                    && saved["commands"].AsArray().Count == 0, "Attaching artwork preserves commands and other metadata.");
                Check(item.ImageSource != null && item.ImagePath == attachment && item.Description == "Pending description edit",
                    "The current row immediately previews the new image without losing pending description edits.");
                LibraryPoseImages.Attach(item, attachment);
                Check(item.ImagePath != attachment && File.Exists(attachment) && original.SequenceEqual(File.ReadAllBytes(source)),
                    "Replacing artwork preserves the original source and previous attachment, even when selecting the current image.");
                byte[] before = File.ReadAllBytes(pose);
                string[] files = Directory.GetFiles(library).Order().ToArray();
                string bad = Path.Combine(root, "invalid.png"); File.WriteAllText(bad, "Not an image");
                bool rejected = false;
                try { LibraryPoseImages.Attach(item, bad); } catch { rejected = true; }
                Check(rejected && before.SequenceEqual(File.ReadAllBytes(pose)) && files.SequenceEqual(Directory.GetFiles(library).Order()),
                    "Invalid artwork leaves the pose unchanged and cleans up its temporary copy.");
                using (var locked = new FileStream(pose, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    rejected = false;
                    try { LibraryPoseImages.Attach(item, source); } catch (IOException) { rejected = true; }
                    Check(rejected && before.SequenceEqual(File.ReadAllBytes(pose)) && files.SequenceEqual(Directory.GetFiles(library).Order()),
                        "A failed pose save preserves the previous image reference and removes staged files.");
                }
                var content = (FrameworkElement)manager.Content;
                content.Measure(new Size(1156, 670)); content.Arrange(new Rect(0, 0, 1156, 670)); content.UpdateLayout();
                var preview = new RenderTargetBitmap(1156, 670, 96, 96, PixelFormats.Pbgra32); preview.Render(content);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(preview));
                using (var output = File.Create(Path.Combine(AppContext.BaseDirectory, "manage-library-poses-check.png"))) png.Save(output);
                grid.SelectedItem = null;
                Check(!button.IsEnabled, "The image chooser is disabled when no pose is selected.");
            }
            finally { manager.Close(); }
            Check(AnimationDocument.LoadLibraryItem(pose).Description == "Pending description edit" && !string.IsNullOrEmpty(AnimationDocument.LoadLibraryItem(pose).ImageFile),
                "Closing the manager saves edited descriptions and retains the newly attached image.");
            foreach (var mode in new[] { (Manage: false, Label: "Library Pose"), (Manage: true, Label: "Library Sequence") })
            {
                var browser = new LibraryItemSelectionWindow(library, mode.Manage, mode.Label);
                Check(((Button)browser.FindName("ChooseImageButton")).Visibility == Visibility.Collapsed, "Image editing is hidden in insertion browsers and sequence management.");
                browser.Close();
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
