using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ServoAnimator;

public partial class MainWindow
{
    private readonly Dictionary<string, (string Stamp, object Reply)> _apiThumbnails = new(StringComparer.OrdinalIgnoreCase);

    // Read-only, bounded button artwork. The lower 56 pixels stay clear for Stream Deck's title.
    private object GetLibraryThumbnail(EditorApiRequest request)
    {
        if (request.LibraryKind != "pose" || string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Supply libraryKind: pose and a Library pose name.");
        string pose = ResolveApiLibraryName(ApiLibraryRoot("pose"), request.Name);
        string relativeImage = AnimationDocument.LoadLibraryItem(pose).ImageFile;
        object empty = new { ok = true, images = (object)null };
        if (string.IsNullOrWhiteSpace(relativeImage)) { _apiThumbnails.Remove(pose); return empty; }
        try
        {
            string path = Path.GetFullPath(Path.IsPathRooted(relativeImage) ? relativeImage : Path.Combine(Path.GetDirectoryName(pose), relativeImage));
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 64 * 1024 * 1024) { _apiThumbnails.Remove(pose); return empty; }
            string stamp = path + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
            if (_apiThumbnails.TryGetValue(pose, out var cached) && cached.Stamp == stamp) return cached.Reply;
            object reply;
            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                bool wide = (double)frame.PixelWidth / frame.PixelHeight >= 120d / 78;
                stream.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream;
                if (wide) bitmap.DecodePixelWidth = Math.Min(120, frame.PixelWidth);
                else bitmap.DecodePixelHeight = Math.Min(78, frame.PixelHeight);
                bitmap.EndInit(); bitmap.Freeze();
                reply = new { ok = true, images = new {
                    connected = RenderLibraryButton(bitmap, "#56d19c", false),
                    disconnected = RenderLibraryButton(bitmap, "#dc6666", false),
                    running = RenderLibraryButton(bitmap, "#f1bd61", false),
                    active = RenderLibraryButton(bitmap, "#f1bd61", true)
                } };
            }
            catch { reply = empty; } // Missing/corrupt/unsupported artwork never prevents pose playback.
            if (_apiThumbnails.Count >= 128) _apiThumbnails.Clear();
            _apiThumbnails[pose] = (stamp, reply);
            return reply;
        }
        catch { _apiThumbnails.Remove(pose); return empty; }
    }

    private static string RenderLibraryButton(BitmapSource bitmap, string statusColor, bool active)
    {
        static Brush Color(string value) => (Brush)new BrushConverter().ConvertFromString(value);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRoundedRectangle(Color("#18232e"), null, new Rect(0, 0, 144, 144), 18, 18);
            drawing.DrawRoundedRectangle(null, new Pen(Color(active ? "#f1bd61" : "#3e5368"), active ? 6 : 2), new Rect(3, 3, 138, 138), 16, 16);
            double scale = Math.Min(120d / bitmap.PixelWidth, 78d / bitmap.PixelHeight);
            double width = bitmap.PixelWidth * scale, height = bitmap.PixelHeight * scale;
            drawing.DrawImage(bitmap, new Rect((144 - width) / 2, 10 + (78 - height) / 2, width, height));
            drawing.DrawEllipse(Color(statusColor), new Pen(Color("#18232e"), 3), new Point(124, 20), 7, 7);
        }
        var result = new RenderTargetBitmap(144, 144, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(result));
        using var output = new MemoryStream(); encoder.Save(output);
        return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
    }
}
