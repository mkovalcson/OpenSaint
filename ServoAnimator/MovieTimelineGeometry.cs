namespace ServoAnimator
{
    /// <summary>Piecewise time/pixel mapping: short sequences get readable blocks without
    /// stretching every other sequence. Cached by the view until items or zoom change.</summary>
    internal sealed class MovieTimelineGeometry
    {
        internal const double MinimumBlockWidth = 100;
        internal const double BlockGap = 2;
        internal const double MinimumSpan = MinimumBlockWidth + BlockGap;
        private readonly double[] _times;
        private readonly double[] _pixels;
        public double Width => _pixels[^1];

        public MovieTimelineGeometry(IReadOnlyList<MovieSequenceItem> items, double pixelsPerSecond)
        {
            _times = new double[items.Count + 1];
            _pixels = new double[items.Count + 1];
            for (int i = 0; i < items.Count; i++)
            {
                double duration = Math.Max(0.001, items[i].DurationSeconds);
                _times[i + 1] = _times[i] + duration;
                _pixels[i + 1] = _pixels[i] + Math.Max(MinimumFor(items[i]), duration * pixelsPerSecond);
            }
        }

        public double PixelAtTime(double time) => Interpolate(time, _times, _pixels);
        public double TimeAtPixel(double pixel) => Interpolate(pixel, _pixels, _times);

        private static double Interpolate(double value, double[] from, double[] to)
        {
            if (value <= 0 || from.Length == 1) return 0;
            if (value >= from[^1]) return to[^1];
            int index = Array.BinarySearch(from, value);
            if (index >= 0) return to[index];
            index = ~index - 1;
            double fraction = (value - from[index]) / (from[index + 1] - from[index]);
            return to[index] + fraction * (to[index + 1] - to[index]);
        }

        public static double FitScale(IReadOnlyList<MovieSequenceItem> items, double availableWidth)
        {
            if (items.Count == 0) return 1;
            if (items.Any(i => !string.IsNullOrEmpty(i.Trigger)))
            {
                double low = 0, high = Math.Max(1, availableWidth / items.Min(i => Math.Max(0.001, i.DurationSeconds)));
                for (int n = 0; n < 60; n++)
                {
                    double middle = (low + high) / 2;
                    double width = items.Sum(i => Math.Max(MinimumFor(i), Math.Max(0.001, i.DurationSeconds) * middle));
                    if (width <= availableWidth) low = middle;
                    else high = middle;
                }
                return Math.Max(0.000001, low);
            }
            var durations = items.Select(i => Math.Max(0.001, i.DurationSeconds)).OrderBy(d => d).ToArray();
            // If even minimum-sized blocks overflow, show all at their minimum size and scroll.
            if (availableWidth <= items.Count * MinimumSpan)
                return MinimumSpan / durations[^1];
            double remainingDuration = durations.Sum();
            foreach (double duration in durations)
            {
                double scale = availableWidth / remainingDuration;
                if (duration * scale >= MinimumSpan) return scale;
                availableWidth -= MinimumSpan;
                remainingDuration -= duration;
            }
            return MinimumSpan / durations[^1];
        }

        private static double MinimumFor(MovieSequenceItem item)
        {
            if (string.IsNullOrEmpty(item.Trigger)) return MinimumSpan;
            var text = new System.Windows.Media.FormattedText(item.Trigger,
                System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface("Segoe UI"), 10, System.Windows.Media.Brushes.Black, 1);
            return Math.Max(MinimumSpan, text.Width + 42);
        }
    }
}
