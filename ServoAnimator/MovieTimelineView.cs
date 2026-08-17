using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator
{
    /// <summary>
    /// Movie timeline viewport. Sequence blocks are always contiguous in time,
    /// so they cannot overlap. Supports cursor placement, drag-to-reorder with
    /// an insertion line, right-click insertion/removal, wheel zoom and
    /// middle-button panning. Long movies remain readable instead of shrinking
    /// every block into a few pixels.
    /// </summary>
    public sealed class MovieTimelineView : FrameworkElement
    {
        private readonly List<MovieSequenceItem> _items = new();
        private int _mouseDownIndex = -1;
        private int _dragBoundaryIndex = -1;
        private Point _mouseDownPoint;
        private bool _dragging;
        private bool _panning;
        private Point _panStart;
        private double _panStartView;
        private double _pixelsPerSecond;
        private double _viewStart;
        private bool _autoFitOnResize = true;

        private const double MinPixelsPerSecond = 0.25;
        private const double MaxPixelsPerSecond = 1000;
        private const double TargetMinimumBlockWidth = 70;

        public event Action<double, int> CursorRequested;
        public event Action<int, int> ReorderRequested;
        public event Action<int> InsertRequested;
        public event Action<int> RemoveRequested;
        public event Action ViewChanged;

        public Func<MovieSequenceItem, string> BlockToolTipProvider { get; set; }
        public IReadOnlyList<MovieSequenceItem> Items => _items;

        public double CursorTime { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public double TotalDuration => _items.Sum(i => Math.Max(0.001, i.DurationSeconds));
        public double ViewStart => _viewStart;
        public double PixelsPerSecond => EffectivePixelsPerSecond;
        public double VisibleSeconds => ActualWidth <= 1 ? TotalDuration : ActualWidth / Math.Max(1e-9, EffectivePixelsPerSecond);

        private double FitPixelsPerSecond => ActualWidth <= 1 || TotalDuration <= 0
            ? 1 : ActualWidth / TotalDuration;
        private double EffectivePixelsPerSecond => _pixelsPerSecond > 0 ? _pixelsPerSecond : Math.Max(MinPixelsPerSecond, FitPixelsPerSecond);

        public MovieTimelineView()
        {
            Focusable = true;
            ToolTipService.SetInitialShowDelay(this, 250);
            ToolTipService.SetShowDuration(this, 12000);
        }

        public void SetItems(IEnumerable<MovieSequenceItem> items)
        {
            bool hadItems = _items.Count > 0;
            double oldPps = _pixelsPerSecond;
            double oldView = _viewStart;
            _items.Clear();
            if (items != null) _items.AddRange(items);
            CursorTime = Math.Clamp(CursorTime, 0, TotalDuration);
            if (SelectedIndex >= _items.Count) SelectedIndex = _items.Count - 1;

            // Only choose the initial readable scale when the strip is populated
            // for the first time. Later refreshes preserve the user's zoom/pan.
            if (!hadItems && _items.Count > 0 && ActualWidth > 1)
            {
                double shortest = _items.Min(i => Math.Max(0.05, i.DurationSeconds));
                double readable = Math.Min(50, TargetMinimumBlockWidth / shortest);
                _pixelsPerSecond = Math.Clamp(Math.Max(FitPixelsPerSecond, readable),
                                              MinPixelsPerSecond, MaxPixelsPerSecond);
                _autoFitOnResize = Math.Abs(_pixelsPerSecond - FitPixelsPerSecond) < 0.0001;
            }
            else
            {
                _pixelsPerSecond = oldPps;
                _viewStart = oldView;
            }
            ClampView();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public double StartOf(int index)
        {
            index = Math.Clamp(index, 0, _items.Count);
            double t = 0;
            for (int i = 0; i < index; i++) t += Math.Max(0.001, _items[i].DurationSeconds);
            return t;
        }

        public int IndexAtTime(double time, bool boundaryChoosesNext = true)
        {
            if (_items.Count == 0) return -1;
            time = Math.Clamp(time, 0, TotalDuration);
            double t = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                double end = t + Math.Max(0.001, _items[i].DurationSeconds);
                if (time < end - 1e-9) return i;
                if (!boundaryChoosesNext && Math.Abs(time - end) < 1e-9) return i;
                t = end;
            }
            return _items.Count - 1;
        }

        public int NearestBoundaryIndex(double time)
        {
            if (_items.Count == 0) return 0;
            time = Math.Clamp(time, 0, TotalDuration);
            int best = 0;
            double bestDist = Math.Abs(time);
            double t = 0;
            for (int i = 1; i <= _items.Count; i++)
            {
                t += Math.Max(0.001, _items[i - 1].DurationSeconds);
                double d = Math.Abs(time - t);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        public void ZoomBy(double factor, double anchorX = double.NaN)
        {
            if (TotalDuration <= 0 || ActualWidth <= 1) return;
            if (double.IsNaN(anchorX)) anchorX = ActualWidth / 2;
            anchorX = Math.Clamp(anchorX, 0, ActualWidth);
            double anchorTime = TimeAtX(anchorX);
            double fit = FitPixelsPerSecond;
            double minimumZoom = Math.Min(MaxPixelsPerSecond, Math.Max(MinPixelsPerSecond, fit));
            _pixelsPerSecond = Math.Clamp(EffectivePixelsPerSecond * factor,
                                          minimumZoom, MaxPixelsPerSecond);
            _autoFitOnResize = Math.Abs(_pixelsPerSecond - fit) < 0.0001;
            _viewStart = anchorTime - anchorX / _pixelsPerSecond;
            ClampView();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void ZoomToFit()
        {
            _pixelsPerSecond = Math.Clamp(FitPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
            _viewStart = 0;
            _autoFitOnResize = true;
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void PanTo(double seconds)
        {
            _viewStart = seconds;
            ClampView();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void EnsureVisible(double time)
        {
            double vis = VisibleSeconds;
            if (time < _viewStart) PanTo(time);
            else if (time > _viewStart + vis) PanTo(time - vis * 0.85);
        }

        private void ClampView()
        {
            double max = Math.Max(0, TotalDuration - VisibleSeconds);
            _viewStart = Math.Clamp(_viewStart, 0, max);
        }

        private double TimeAtX(double x) =>
            Math.Clamp(_viewStart + x / Math.Max(1e-9, EffectivePixelsPerSecond), 0, TotalDuration);

        private double XAtTime(double t) => (t - _viewStart) * EffectivePixelsPerSecond;

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var movieBg = ThemeManager.GetColor("MoviePanelBackground", Color.FromRgb(31, 30, 26));
            var movieAccent = ThemeManager.GetColor("MovieAccent", Color.FromRgb(192, 150, 85));
            var selectedFill = ThemeManager.GetColor("MovieAccentSurface", Color.FromRgb(91, 72, 43));
            var evenFill = ThemeManager.GetColor("ElevatedPanelBackground", Color.FromRgb(55, 52, 43));
            var oddFill = ThemeManager.GetColor("ControlBackground", Color.FromRgb(62, 58, 47));
            dc.DrawRectangle(new SolidColorBrush(movieBg),
                             new Pen(new SolidColorBrush(movieAccent), 1),
                             new Rect(0, 0, ActualWidth, ActualHeight));

            if (_items.Count == 0)
            {
                DrawText(dc, "Movie timeline is empty — right-click to insert a sequence.",
                    new Point(8, Math.Max(4, (ActualHeight - 16) / 2)), Brushes.Gray, 12,
                    Math.Max(0, ActualWidth - 16));
                return;
            }

            double start = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                double dur = Math.Max(0.001, _items[i].DurationSeconds);
                double x0 = XAtTime(start);
                double x1 = XAtTime(start + dur);
                start += dur;
                if (x1 < 0 || x0 > ActualWidth) continue;

                var rect = new Rect(x0 + 1, 4, Math.Max(1, x1 - x0 - 2), Math.Max(10, ActualHeight - 8));
                Color fillColor = i == SelectedIndex
                    ? selectedFill
                    : (i % 2 == 0 ? evenFill : oddFill);
                dc.DrawRoundedRectangle(new SolidColorBrush(fillColor), new Pen(Brushes.DimGray, 1), rect, 3, 3);

                string label = Path.GetFileNameWithoutExtension(_items[i].FilePath ?? "sequence");
                DrawText(dc, label, new Point(rect.X + 6, rect.Y + 4), Brushes.WhiteSmoke,
                         12, Math.Max(0, rect.Width - 18));
                if (rect.Width > 58)
                    DrawText(dc, dur.ToString("0.###", CultureInfo.InvariantCulture) + " s",
                             new Point(rect.X + 6, rect.Bottom - 18), Brushes.LightGray,
                             10, Math.Max(0, rect.Width - 18));

                // Small grip tells the user that the block itself is draggable.
                if (rect.Width > 28)
                {
                    var gripPen = new Pen(new SolidColorBrush(movieAccent), 1);
                    double gx = rect.Right - 7;
                    for (int g = -3; g <= 3; g += 3)
                        dc.DrawLine(gripPen, new Point(gx + g, rect.Top + 7), new Point(gx + g, rect.Bottom - 7));
                }
            }

            if (_dragging && _dragBoundaryIndex >= 0)
            {
                double bx = XAtTime(StartOf(_dragBoundaryIndex));
                dc.DrawLine(new Pen(new SolidColorBrush(movieAccent), 3), new Point(bx, 1), new Point(bx, ActualHeight - 1));
            }

            double cx = XAtTime(CursorTime);
            if (cx >= -2 && cx <= ActualWidth + 2)
                dc.DrawLine(new Pen(Brushes.OrangeRed, 2), new Point(cx, 0), new Point(cx, ActualHeight));
        }

        private static void DrawText(DrawingContext dc, string text, Point p,
            Brush brush, double size, double maxWidth)
        {
            if (maxWidth <= 2) return;
            var ft = new FormattedText(text ?? "", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1.0)
            {
                MaxTextWidth = maxWidth,
                MaxTextHeight = size * 1.4,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(ft, p);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomBy(e.Delta > 0 ? 1.25 : 1 / 1.25, e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle)
            {
                _panning = true;
                _panStart = e.GetPosition(this);
                _panStartView = _viewStart;
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            _mouseDownPoint = e.GetPosition(this);
            _mouseDownIndex = IndexAtTime(TimeAtX(_mouseDownPoint.X));
            _dragBoundaryIndex = -1;
            _dragging = false;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);

            if (_panning && e.MiddleButton == MouseButtonState.Pressed)
            {
                double dx = p.X - _panStart.X;
                _viewStart = _panStartView - dx / Math.Max(1e-9, EffectivePixelsPerSecond);
                ClampView();
                InvalidateVisual();
                ViewChanged?.Invoke();
                return;
            }

            if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && _mouseDownIndex >= 0)
            {
                if (!_dragging && Math.Abs(p.X - _mouseDownPoint.X) >= 5) _dragging = true;
                if (_dragging)
                {
                    int boundary = NearestBoundaryIndex(TimeAtX(p.X));
                    if (boundary != _dragBoundaryIndex)
                    {
                        _dragBoundaryIndex = boundary;
                        InvalidateVisual();
                    }
                }
                return;
            }

            int idx = IndexAtTime(TimeAtX(p.X));
            ToolTip = idx >= 0 && idx < _items.Count
                ? (BlockToolTipProvider?.Invoke(_items[idx]) ??
                   $"{Path.GetFileName(_items[idx].FilePath)}\nDuration: {_items[idx].DurationSeconds:0.###} s")
                : null;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Middle && _panning)
            {
                _panning = false;
                if (IsMouseCaptured) ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            Point p = e.GetPosition(this);
            if (IsMouseCaptured) ReleaseMouseCapture();

            if (_dragging && _mouseDownIndex >= 0 && _dragBoundaryIndex >= 0)
            {
                int target = _dragBoundaryIndex;
                if (target > _mouseDownIndex) target--;
                target = Math.Clamp(target, 0, Math.Max(0, _items.Count - 1));
                if (target != _mouseDownIndex)
                    ReorderRequested?.Invoke(_mouseDownIndex, target);
            }
            else
            {
                double t = TimeAtX(p.X);
                int idx = IndexAtTime(t);
                CursorTime = t;
                SelectedIndex = idx;
                InvalidateVisual();
                CursorRequested?.Invoke(t, idx);
            }

            _mouseDownIndex = -1;
            _dragBoundaryIndex = -1;
            _dragging = false;
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Point p = e.GetPosition(this);
            double t = TimeAtX(p.X);
            int idx = IndexAtTime(t, boundaryChoosesNext: false);
            int boundary = NearestBoundaryIndex(t);
            CursorTime = StartOf(boundary);
            if (idx >= 0) SelectedIndex = idx;
            InvalidateVisual();

            var menu = new ContextMenu();
            var insert = new MenuItem { Header = "Insert Sequence Here…" };
            insert.Click += (_, _) => InsertRequested?.Invoke(boundary);
            menu.Items.Add(insert);

            if (idx >= 0 && idx < _items.Count)
            {
                var remove = new MenuItem
                {
                    Header = "Remove " + Path.GetFileNameWithoutExtension(_items[idx].FilePath ?? "sequence")
                };
                remove.Click += (_, _) => RemoveRequested?.Invoke(idx);
                menu.Items.Add(new Separator());
                menu.Items.Add(remove);
            }

            ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_autoFitOnResize && TotalDuration > 0)
                _pixelsPerSecond = Math.Clamp(FitPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
            ClampView();
            ViewChanged?.Invoke();
        }
    }
}
