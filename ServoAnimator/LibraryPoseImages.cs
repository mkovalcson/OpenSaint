using System.IO;
using System.Windows.Media.Imaging;

namespace ServoAnimator;

internal static class LibraryPoseImages
{
    internal static void Attach(LibraryItemInfo item, string sourcePath)
    {
        if (item?.IsValid != true) throw new InvalidOperationException("Select a valid Library pose first.");
        string posePath = Path.GetFullPath(item.FullPath);
        string folder = Path.GetDirectoryName(posePath);
        // A distinct attachment avoids overwriting a source image, shared image, or another pose's artwork.
        string imageName = Path.GetFileNameWithoutExtension(posePath) + "_image_" + Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath);
        string imagePath = Path.Combine(folder, imageName);
        string stagedPose = Path.Combine(folder, ".pose-image-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool imageCreated = false, saved = false;
        try
        {
            File.Copy(sourcePath, imagePath, overwrite: false);
            imageCreated = true;
            // Validate the copied bytes before changing the pose. OnLoad releases the file handle.
            using (var stream = File.OpenRead(imagePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream; bitmap.DecodePixelWidth = 512;
                bitmap.EndInit(); bitmap.Freeze();
            }
            File.Copy(posePath, stagedPose, overwrite: false);
            AnimationDocument.UpdateLibraryImage(stagedPose, imageName);
            File.Replace(stagedPose, posePath, null);
            saved = true;
            item.SetImagePath(imagePath);
            item.Modified = File.GetLastWriteTime(posePath);
        }
        finally
        {
            if (File.Exists(stagedPose)) { try { File.Delete(stagedPose); } catch { } }
            if (imageCreated && !saved) { try { File.Delete(imagePath); } catch { } }
        }
    }
}
