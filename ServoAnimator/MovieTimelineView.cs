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
    public sealed class MovieTimelineView : Panel
    {
        private readonly TimelineCursorPresenter _cursorPresenter;
        private readonly MovieProgressPresenter _progressPresenter;
        public void InvalidateCursor() { _progressPresenter?.Update(); if (_cursorPresenter?.Update() != true) InvalidateVisual(); }
        private const double NewSequenceTailWidth = 140;
        private readonly Button _newSequenceButton;
        private bool _canCreateSequence;
        public event Action NewSequenceRequested;
        public bool CanCreateSequence
        {
            get => _canCreateSequence;
            set
            {
                if (_canCreateSequence == value) return;
                _canCreateSequence = value;
                _newSequenceButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (_autoFitOnResize) _pixelsPerSecond = Math.Clamp(FitPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
                ClampView();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
                ViewChanged?.Invoke();
            }
        }
        // Scrollbar units are display pixels divided by the nominal scale. They need
        // not equal playback seconds when a block is held at its minimum width.
        public double ScrollableDuration => (Geometry.Width +
            (CanCreateSequence ? NewSequenceTailWidth : 0)) / EffectivePixelsPerSecond;

        protected override Size MeasureOverride(Size availableSize)
        {
            _newSequenceButton.Measure(new Size(124, 28));
            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _newSequenceButton.Arrange(new Rect(XAtTime(TotalDuration) + 8,
                Math.Max(0, (finalSize.Height - 28) / 2), 124, 28));
            return finalSize;
        }

        private readonly List<MovieSequenceItem> _items = new();
        private MovieTimelineGeometry _geometry;
        private double _geometryScale;
        private MovieTimelineGeometry Geometry
        {
            get
            {
                double scale = EffectivePixelsPerSecond;
                if (_geometry == null || _geometryScale != scale)
                {
                    _geometry = new MovieTimelineGeometry(_items, scale);
                    _geometryScale = scale;
                }
                return _geometry;
            }
        }
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
        private int _tooltipIndex = -1;
        private readonly Dictionary<int, Geometry> _descriptionEditRegions = new();
        private int _descriptionEditDownIndex = -1;
        public void RefreshBlockToolTip()
        {
            _tooltipIndex = -1;
            ToolTip = null;
        }

        private const double MinPixelsPerSecond = 0.000001;
        private const double MaxPixelsPerSecond = 1000;

        public event Action<double, int> CursorRequested;
        public event Action<int, int> ReorderRequested;
        public event Action<int> InsertRequested;
        public event Action<int> RemoveRequested;
        public event Action<int> LoopToggleRequested;
        public event Action<int> AssignTriggerRequested;
        public event Action<int> DescriptionEditRequested;
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
            ? 1 : MovieTimelineGeometry.FitScale(_items,
                Math.Max(1, ActualWidth - (CanCreateSequence ? NewSequenceTailWidth : 0)));
        private double EffectivePixelsPerSecond => _pixelsPerSecond > 0 ? _pixelsPerSecond : Math.Max(MinPixelsPerSecond, FitPixelsPerSecond);

        public MovieTimelineView()
        {
            _progressPresenter = new MovieProgressPresenter(this, ProgressBounds);
            _cursorPresenter = new TimelineCursorPresenter(this, () => XAtTime(CursorTime), Colors.OrangeRed);
            ClipToBounds = true;
            _newSequenceButton = new Button
            {
                Content = "New Sequence", Visibility = Visibility.Collapsed,
                ToolTip = "Build a new Sequence and append it to this Movie after saving",
            };
            _newSequenceButton.Click += (_, e) => { e.Handled = true; NewSequenceRequested?.Invoke(); };
            Children.Add(_newSequenceButton);
            Focusable = true;
            ToolTipService.SetInitialShowDelay(this, 250);
            ToolTipService.SetShowDuration(this, 12000);
        }

        public void SetItems(IEnumerable<MovieSequenceItem> items)
        {
            _descriptionEditRegions.Clear();
            RefreshBlockToolTip();
            bool hadItems = _items.Count > 0;
            double oldPps = _pixelsPerSecond;
            double oldView = _viewStart;
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _geometry = null;
            CursorTime = Math.Clamp(CursorTime, 0, TotalDuration);
            if (SelectedIndex >= _items.Count) SelectedIndex = _items.Count - 1;

            // Choose the initial fit once; minimum block widths provide readability.
            // Later refreshes preserve the user's zoom/pan.
            if (!hadItems && _items.Count > 0 && ActualWidth > 1)
            {
                _pixelsPerSecond = Math.Clamp(FitPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
                _autoFitOnResize = true;
            }
            else
            {
                _pixelsPerSecond = oldPps;
                _viewStart = oldView;
            }
            ClampView();
            InvalidateArrange();
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

        public void ZoomBy(double factor)
        {
            if (!double.IsFinite(factor) || factor <= 0 || TotalDuration <= 0 || ActualWidth <= 1) return;
            double cursorX = XAtTime(CursorTime);
            double fit = FitPixelsPerSecond;
            double minimumZoom = Math.Min(MaxPixelsPerSecond, Math.Max(MinPixelsPerSecond, fit));
            _pixelsPerSecond = Math.Clamp(EffectivePixelsPerSecond * factor,
                                          minimumZoom, MaxPixelsPerSecond);
            _autoFitOnResize = Math.Abs(_pixelsPerSecond - fit) < 0.0001;
            _viewStart = (Geometry.PixelAtTime(CursorTime) - cursorX) / _pixelsPerSecond;
            ClampView();
            InvalidateArrange();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void ZoomToFit()
        {
            _pixelsPerSecond = Math.Clamp(FitPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
            _viewStart = 0;
            _autoFitOnResize = true;
            InvalidateArrange();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void PanTo(double seconds)
        {
            _viewStart = seconds;
            ClampView();
            InvalidateArrange();
            InvalidateVisual();
            ViewChanged?.Invoke();
        }

        public void EnsureVisible(double time)
        {
            double x = XAtTime(time);
            double position = Geometry.PixelAtTime(time);
            if (x < 0) PanTo(position / EffectivePixelsPerSecond);
            else if (x > ActualWidth) PanTo((position - ActualWidth * 0.85) / EffectivePixelsPerSecond);
        }

        private void ClampView()
        {
            double max = Math.Max(0, ScrollableDuration - VisibleSeconds);
            _viewStart = Math.Clamp(_viewStart, 0, max);
        }

        internal double TimeAtX(double x) => Geometry.TimeAtPixel(x + _viewStart * EffectivePixelsPerSecond);

        internal double XAtTime(double t) => Geometry.PixelAtTime(t) - _viewStart * EffectivePixelsPerSecond;

        private Rect ProgressBounds()
        {
            if (SelectedIndex < 0 || SelectedIndex >= _items.Count || ActualHeight <= 12) return Rect.Empty;
            double start = StartOf(SelectedIndex), duration = Math.Max(0.001, _items[SelectedIndex].DurationSeconds);
            double left = XAtTime(start) + MovieTimelineGeometry.BlockGap / 2 + 2;
            double width = Math.Max(0, XAtTime(start + duration) - left - MovieTimelineGeometry.BlockGap / 2 - 2);
            return new Rect(left, 6, width * Math.Clamp((CursorTime - start) / duration, 0, 1), ActualHeight - 12);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _cursorPresenter.Update();
            _progressPresenter.Update();
            _descriptionEditRegions.Clear();
            var movieBg = ThemeManager.GetColor("MoviePanelBackground", Color.FromRgb(31, 30, 26));
            var movieAccent = ThemeManager.GetColor("MovieAccent", Color.FromRgb(192, 150, 85));
            var selectedFill = ThemeManager.GetColor("MovieAccentSurface", Color.FromRgb(91, 72, 43));
            var evenFill = ThemeManager.GetColor("ElevatedPanelBackground", Color.FromRgb(55, 52, 43));
            var oddFill = ThemeManager.GetColor("ControlBackground", Color.FromRgb(62, 58, 47));
            var primaryText = new SolidColorBrush(ThemeManager.GetColor("PrimaryText", Colors.WhiteSmoke));
            var secondaryText = new SolidColorBrush(ThemeManager.GetColor("SecondaryText", Colors.LightGray));
            dc.DrawRectangle(new SolidColorBrush(movieBg),
                             new Pen(new SolidColorBrush(movieAccent), 1),
                             new Rect(0, 0, ActualWidth, ActualHeight));

            if (_items.Count == 0)
            {
                if (CanCreateSequence) return;
                DrawText(dc, "Movie timeline is empty — right-click to insert a sequence.",
                    new Point(8, Math.Max(4, (ActualHeight - 16) / 2)), secondaryText, 12,
                    Math.Max(0, ActualWidth - 16));
                return;
            }

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            double start = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                double dur = Math.Max(0.001, _items[i].DurationSeconds);
                double x0 = XAtTime(start);
                double x1 = XAtTime(start + dur);
                start += dur;
                if (x1 < 0 || x0 > ActualWidth) continue;

                var rect = new Rect(x0 + MovieTimelineGeometry.BlockGap / 2, 4,
                    x1 - x0 - MovieTimelineGeometry.BlockGap, Math.Max(10, ActualHeight - 8));
                Color fillColor = i == SelectedIndex
                    ? selectedFill
                    : (i % 2 == 0 ? evenFill : oddFill);
                var borderColor = i == SelectedIndex ? movieAccent : ThemeManager.GetColor("DividerBrush", Colors.DimGray);
                dc.DrawRoundedRectangle(new SolidColorBrush(fillColor), new Pen(new SolidColorBrush(borderColor), i == SelectedIndex ? 1.8 : 1), rect, 6, 6);

                string name = Path.GetFileNameWithoutExtension(_items[i].FilePath ?? "sequence");
                string label = $"{name}{(_items[i].IsModified ? "*" : "")}  " +
                               dur.ToString("0.###", CultureInfo.InvariantCulture) + " s";
                string trigger = _items[i].Trigger ?? "";
                var triggerText = new FormattedText(trigger, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 10, primaryText, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                double triggerWidth = trigger.Length == 0 ? 0 : triggerText.Width + 12;
                double loopReserve = Math.Max(triggerWidth, _items[i].IsLooping ? 22 : 0);
                if (triggerWidth > 0)
                {
                    var keyRect = new Rect(rect.Right - 12 - triggerWidth, rect.Y + 3, triggerWidth, 18);
                    dc.DrawRoundedRectangle(new SolidColorBrush(ThemeManager.GetColor("MoviePanelBackground", movieBg)),
                        new Pen(secondaryText, 1), keyRect, 3, 3);
                    dc.DrawText(triggerText, new Point(keyRect.X + 6, keyRect.Y + (18 - triggerText.Height) / 2));
                }
                double titleWidth = Math.Max(1, rect.Width - 20 - loopReserve);
                var title = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 12, primaryText, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    { MaxTextWidth = titleWidth, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                int durationStart = name.Length + (_items[i].IsModified ? 1 : 0);
                title.SetFontWeight(FontWeights.SemiBold, 0, durationStart);
                title.SetForegroundBrush(secondaryText, durationStart, label.Length - durationStart);
                title.SetFontSize(10, durationStart, label.Length - durationStart);
                dc.DrawText(title, new Point(rect.X + 7, rect.Y + 5));
                if (_items[i].IsLooping && rect.Width > 26)
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(ThemeManager.GetColor("MoviePanelBackground", movieBg)), null,
                        new Rect(rect.Right - 35, rect.Y + 3 + (triggerWidth > 0 ? 21 : 0), 23, 18), 7, 7);
                    DrawText(dc, "∞", new Point(rect.Right - 32, rect.Y + 1 + (triggerWidth > 0 ? 21 : 0)),
                             new SolidColorBrush(movieAccent), 16, 18);
                }
                double descriptionTop = rect.Y + 24;
                double descriptionHeight = Math.Max(0, rect.Bottom - 6 - descriptionTop);
                if (descriptionHeight >= 9)
                    DrawDescription(dc, i, _items[i].Description,
                                    new Point(rect.X + 6, descriptionTop), secondaryText,
                                    10, Math.Max(0, rect.Width - 20 - loopReserve), descriptionHeight);
                // Small grip tells the user that the block itself is draggable.
                if (rect.Width > 28)
                {
                    var gripPen = new Pen(new SolidColorBrush(ThemeManager.GetColor("SecondaryText", Colors.Gray)) { Opacity = 0.35 }, 1);
                    double gx = rect.Right - 7;
                    for (int g = -3; g <= 3; g += 3)
                        dc.DrawLine(gripPen, new Point(gx + g, rect.Bottom - 16), new Point(gx + g, rect.Bottom - 7));
                }
            }

            if (_dragging && _dragBoundaryIndex >= 0)
            {
                double bx = XAtTime(StartOf(_dragBoundaryIndex));
                dc.DrawLine(new Pen(new SolidColorBrush(movieAccent), 3), new Point(bx, 1), new Point(bx, ActualHeight - 1));
            }

            double cx = XAtTime(CursorTime);
            if (!_cursorPresenter.IsAttached && cx >= -2 && cx <= ActualWidth + 2)
                dc.DrawLine(new Pen(Brushes.OrangeRed, 2), new Point(cx, 0), new Point(cx, ActualHeight));
            dc.Pop();
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

        private void DrawDescription(DrawingContext dc, int index, string text, Point p,
            Brush brush, double size, double maxWidth, double maxHeight)
        {
            if (maxWidth < 20 || maxHeight < size * 1.4) return;
            string description = (text ?? "").TrimEnd();
            FormattedText Format(string value)
            {
                string label = value.Length == 0 ? "edit" : value + "  edit";
                var result = new FormattedText(label, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = maxWidth };
                result.SetFontWeight(FontWeights.Bold, label.Length - 4, 4);
                return result;
            }
            var ft = Format(description);
            if (ft.Height > maxHeight)
            {
                // Keep the edit link visible even when a long description is shortened.
                int low = 0, high = description.Length;
                ft = Format("");
                while (low < high)
                {
                    int length = (low + high + 1) / 2;
                    var candidate = Format(description[..length].TrimEnd() + "…");
                    if (candidate.Height <= maxHeight) { low = length; ft = candidate; }
                    else high = length - 1;
                }
            }
            dc.DrawText(ft, p);
            var region = ft.BuildHighlightGeometry(p, ft.Text.Length - 4, 4);
            if (region != null) _descriptionEditRegions[index] = region;
        }

        private int DescriptionEditAt(Point point)
        {
            foreach (var entry in _descriptionEditRegions)
                if (entry.Value.FillContains(point)) return entry.Key;
            return -1;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomBy(e.Delta > 0 ? 1.25 : 1 / 1.25);
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
            _descriptionEditDownIndex = DescriptionEditAt(_mouseDownPoint);
            if (_descriptionEditDownIndex >= 0)
            {
                CaptureMouse();
                e.Handled = true;
                return;
            }
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

            bool overEdit = DescriptionEditAt(p) >= 0;
            Cursor = overEdit ? Cursors.Hand : Cursors.Arrow;
            if (_descriptionEditDownIndex >= 0) return;

            if (_panning && e.MiddleButton == MouseButtonState.Pressed)
            {
                double dx = p.X - _panStart.X;
                _viewStart = _panStartView - dx / Math.Max(1e-9, EffectivePixelsPerSecond);
                ClampView();
                InvalidateArrange();
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
                        InvalidateArrange();
                        InvalidateVisual();
                    }
                }
                return;
            }

            if (overEdit)
            {
                _tooltipIndex = -1;
                ToolTip = "Edit sequence description";
                return;
            }
            int idx = IndexAtTime(TimeAtX(p.X));
            if (idx == _tooltipIndex) return;
            _tooltipIndex = idx;
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

            if (_descriptionEditDownIndex >= 0)
            {
                int index = _descriptionEditDownIndex;
                _descriptionEditDownIndex = -1;
                e.Handled = true;
                if (DescriptionEditAt(p) == index) DescriptionEditRequested?.Invoke(index);
                return;
            }

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
                InvalidateArrange();
                InvalidateVisual();
                CursorRequested?.Invoke(t, idx);
            }

            _mouseDownIndex = -1;
            _dragBoundaryIndex = -1;
            _dragging = false;
            InvalidateArrange();
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
            InvalidateArrange();
            InvalidateVisual();

            var menu = new ContextMenu();
            var insert = new MenuItem { Header = "Insert Sequence Here…" };
            insert.Click += (_, _) => InsertRequested?.Invoke(boundary);
            menu.Items.Add(insert);

            if (idx >= 0 && idx < _items.Count)
            {
                var assign = new MenuItem { Header = "Assign trigger" };
                assign.Click += (_, _) => AssignTriggerRequested?.Invoke(idx);
                menu.Items.Add(assign);

                var loop = new MenuItem
                {
                    Header = "Loop Sequence",
                    IsCheckable = true,
                    IsChecked = _items[idx].IsLooping,
                };
                loop.Click += (_, _) => LoopToggleRequested?.Invoke(idx);
                menu.Items.Add(loop);

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
            InvalidateArrange();
            ViewChanged?.Invoke();
        }
    }
}
