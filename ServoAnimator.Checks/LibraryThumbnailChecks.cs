using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void PoseButtonPreview(string imagePath, string outputPath)
    {
        var bitmap = new BitmapImage(new Uri(Path.GetFullPath(imagePath)));
        var render = typeof(MainWindow).GetMethod("RenderLibraryButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        string data = (string)render.Invoke(null, new object[] { bitmap, "#56d19c", false });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        File.WriteAllBytes(outputPath, Convert.FromBase64String(data.Split(',')[1]));
        Console.WriteLine("Rendered pose button: " + Path.GetFullPath(outputPath));
    }

    private static void LibraryThumbnailChecks(string root, Func<EditorApiRequest, object> call)
    {
        string folder = Path.Combine(root, "Library", "Commands");
        string pose = Path.Combine(folder, "DirectPose.json"), image = Path.Combine(folder, "DirectPose.png");
        object Thumbnail() => call(new() { Method = "get_library_thumbnail", LibraryKind = "pose", Name = "DirectPose.json" });
        JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, EditorApiServer.Json);
        void Fixture(int width, int height, byte red, byte blue)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = blue; pixels[i + 2] = red; pixels[i + 3] = 255; }
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(image)) encoder.Save(output);
            File.SetLastWriteTimeUtc(image, DateTime.UtcNow.AddSeconds(width));
        }
        BitmapSource Decode(object reply, string state = "connected")
        {
            string data = Json(reply).GetProperty("images").GetProperty(state).GetString();
            using var stream = new MemoryStream(Convert.FromBase64String(data.Split(',')[1]));
            return new FormatConvertedBitmap(BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0], PixelFormats.Bgra32, null, 0);
        }
        bool Pixel(BitmapSource source, int x, int y, byte red, byte green, byte blue)
        {
            byte[] pixel = new byte[4]; source.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return pixel[0] == blue && pixel[1] == green && pixel[2] == red;
        }
        Check(Json(Thumbnail()).GetProperty("images").ValueKind == JsonValueKind.Null, "A pose without an image uses the default Stream Deck icon.");
        AnimationDocument.SaveLibraryCommand(pose, AnimationDocument.LoadLibraryItem(pose).Commands, imageFile: "DirectPose.png");
        Fixture(480, 160, 255, 0);
        byte[] originalImage = File.ReadAllBytes(image), originalPose = File.ReadAllBytes(pose);
        object wide = Thumbnail(); var wideBitmap = Decode(wide);
        Check(wideBitmap.PixelWidth == 144 && wideBitmap.PixelHeight == 144
            && Pixel(wideBitmap, 72, 45, 255, 0, 0) && Pixel(wideBitmap, 72, 15, 24, 35, 46),
            "Landscape pose art fits a 144px key with its original aspect ratio.");
        Check(Pixel(wideBitmap, 72, 105, 24, 35, 46) && Pixel(wideBitmap, 72, 130, 24, 35, 46),
            "Pose artwork leaves the lower caption area clear.");
        Check(Pixel(wideBitmap, 124, 20, 86, 209, 156) && Pixel(Decode(wide, "disconnected"), 124, 20, 220, 102, 102)
            && Pixel(Decode(wide, "running"), 124, 20, 241, 189, 97), "Thumbnail keys preserve the connection status colors.");
        Check(ReferenceEquals(wide, Thumbnail()), "Unchanged pose art reuses cached PNGs.");
        Check(originalImage.SequenceEqual(File.ReadAllBytes(image)) && originalPose.SequenceEqual(File.ReadAllBytes(pose)),
            "Thumbnail generation never modifies source artwork or pose files.");
        Fixture(160, 480, 0, 255);
        object tall = Thumbnail(); var tallBitmap = Decode(tall);
        Check(!ReferenceEquals(wide, tall) && Pixel(tallBitmap, 72, 45, 0, 0, 255) && Pixel(tallBitmap, 30, 45, 24, 35, 46),
            "Changed images invalidate the cache and portrait art preserves its proportions.");
        File.WriteAllText(image, "corrupt image");
        Check(Json(Thumbnail()).GetProperty("images").ValueKind == JsonValueKind.Null, "Corrupt pose artwork falls back without blocking playback.");
        File.Delete(image);
        Check(Json(Thumbnail()).GetProperty("images").ValueKind == JsonValueKind.Null, "Deleted pose artwork clears its cached thumbnail.");
        Fixture(480, 160, 255, 0); // Keep valid artwork for the packaged plugin's real-pipe test.
    }
}
