using SkiaSharp;

namespace ServoAnimator
{
    internal sealed class TimelineDrawingCache : IDisposable
    {
        private SKImage _image;
        private object _key;
        public void Invalidate() { _image?.Dispose(); _image = null; _key = null; }
        public void Draw(SKCanvas target, SKImageInfo info, object key, Action<SKCanvas> draw)
        {
            if (_image == null || !Equals(_key, key))
            {
                Invalidate();
                using var surface = SKSurface.Create(info);
                if (surface == null)
                {
                    target.Save();
                    try { draw(target); } finally { target.Restore(); }
                    return;
                }
                draw(surface.Canvas);
                _image = surface.Snapshot();
                _key = key;
            }
            target.DrawImage(_image, 0, 0);
        }
        public void Dispose() => Invalidate();
    }
}
