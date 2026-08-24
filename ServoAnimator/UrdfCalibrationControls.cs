// ---------------------------------------------------------------------------
// UrdfCalibrationControls.cs
// Visual calibration controls used by the URDF Configuration window.
// ---------------------------------------------------------------------------

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ServoAnimator
{
    /// <summary>
    /// Circular degree editor.  The zero reference is always drawn horizontally
    /// to the right.  Minimum and maximum are shown as angular offsets from zero;
    /// reversing the servo swaps the side of the zero line on which those offsets
    /// are displayed.  Drag the endpoint handles to change Min/Max.
    /// </summary>
    public sealed class AngularRangeDial : FrameworkElement
    {
        private enum DragTarget { None, Minimum, Maximum }
        private DragTarget _dragTarget;

        public AngularRangeDial()
        {
            MinWidth = 150;
            MinHeight = 150;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
        }

        public static readonly DependencyProperty MinimumExtentProperty =
            DependencyProperty.Register(nameof(MinimumExtent), typeof(double), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVisualPropertyChanged));

        public static readonly DependencyProperty MaximumExtentProperty =
            DependencyProperty.Register(nameof(MaximumExtent), typeof(double), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVisualPropertyChanged));

        public static readonly DependencyProperty ZeroExtentProperty =
            DependencyProperty.Register(nameof(ZeroExtent), typeof(double), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVisualPropertyChanged));

        public static readonly DependencyProperty ReverseProperty =
            DependencyProperty.Register(nameof(Reverse), typeof(bool), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(false, OnVisualPropertyChanged));

        public static readonly DependencyProperty EditorMinimumProperty =
            DependencyProperty.Register(nameof(EditorMinimum), typeof(double), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(-180.0, OnVisualPropertyChanged));

        public static readonly DependencyProperty EditorMaximumProperty =
            DependencyProperty.Register(nameof(EditorMaximum), typeof(double), typeof(AngularRangeDial),
                new FrameworkPropertyMetadata(180.0, OnVisualPropertyChanged));

        public double MinimumExtent
        {
            get => (double)GetValue(MinimumExtentProperty);
            set => SetValue(MinimumExtentProperty, value);
        }

        public double MaximumExtent
        {
            get => (double)GetValue(MaximumExtentProperty);
            set => SetValue(MaximumExtentProperty, value);
        }

        public double ZeroExtent
        {
            get => (double)GetValue(ZeroExtentProperty);
            set => SetValue(ZeroExtentProperty, value);
        }

        public bool Reverse
        {
            get => (bool)GetValue(ReverseProperty);
            set => SetValue(ReverseProperty, value);
        }

        public double EditorMinimum
        {
            get => (double)GetValue(EditorMinimumProperty);
            set => SetValue(EditorMinimumProperty, value);
        }

        public double EditorMaximum
        {
            get => (double)GetValue(EditorMaximumProperty);
            set => SetValue(EditorMaximumProperty, value);
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((AngularRangeDial)d).InvalidateVisual();

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 178 : Math.Min(178, availableSize.Width);
            double h = double.IsInfinity(availableSize.Height) ? 178 : Math.Min(178, availableSize.Height);
            return new Size(Math.Max(150, w), Math.Max(150, h));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            Point center = Center;
            double radius = Radius;

            Brush outlineBrush = ResourceBrush("DividerBrush", Brushes.Gray);
            Brush zeroBrush = ResourceBrush("PrimaryText", Brushes.WhiteSmoke);
            Brush minBrush = ResourceBrush("WarningText", Brushes.Goldenrod);
            Brush maxBrush = ResourceBrush("SequenceAccent", Brushes.CornflowerBlue);
            Brush panelBrush = ResourceBrush("PanelBackground", Brushes.Black);

            dc.DrawEllipse(null, new Pen(outlineBrush, 2.25), center, radius, radius);

            Point zeroEnd = PointForAngle(0, radius + 10);
            dc.DrawLine(new Pen(zeroBrush, 4.0), center, zeroEnd);

            double minMagnitude = Math.Clamp(Math.Abs(ZeroExtent - MinimumExtent), 0.0, 180.0);
            double maxMagnitude = Math.Clamp(Math.Abs(MaximumExtent - ZeroExtent), 0.0, 180.0);
            double minAngle = Reverse ? -minMagnitude : minMagnitude;
            double maxAngle = Reverse ? maxMagnitude : -maxMagnitude;

            Point minEnd = PointForAngle(minAngle, radius + 10);
            Point maxEnd = PointForAngle(maxAngle, radius + 10);

            dc.DrawLine(new Pen(minBrush, 4.0), center, minEnd);
            dc.DrawLine(new Pen(maxBrush, 4.0), center, maxEnd);
            // Transparent hit areas make the small endpoint handles easy to grab.
            dc.DrawEllipse(Brushes.Transparent, null, minEnd, 18, 18);
            dc.DrawEllipse(Brushes.Transparent, null, maxEnd, 18, 18);
            dc.DrawEllipse(panelBrush, new Pen(minBrush, 3.0), minEnd, 7, 7);
            dc.DrawEllipse(panelBrush, new Pen(maxBrush, 3.0), maxEnd, 7, 7);

            // Small center hub makes the three radial references visually explicit.
            dc.DrawEllipse(zeroBrush, null, center, 3.5, 3.5);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point p = e.GetPosition(this);
            Point min = MinimumHandlePoint;
            Point max = MaximumHandlePoint;

            double dMin = Distance(p, min);
            double dMax = Distance(p, max);
            const double hitRadius = 18.0;

            if (dMin <= hitRadius || dMax <= hitRadius)
            {
                _dragTarget = dMin <= dMax ? DragTarget.Minimum : DragTarget.Maximum;
                CaptureMouse();
                UpdateDrag(p);
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed) return;
            UpdateDrag(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragTarget == DragTarget.None) return;
            UpdateDrag(e.GetPosition(this));
            _dragTarget = DragTarget.None;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dragTarget = DragTarget.None;
        }

        private void UpdateDrag(Point p)
        {
            Point c = Center;
            double angle = Math.Atan2(p.Y - c.Y, p.X - c.X) * 180.0 / Math.PI;
            double magnitude = Math.Clamp(Math.Abs(Normalize180(angle)), 0.0, 180.0);

            if (_dragTarget == DragTarget.Minimum)
            {
                double value = Math.Clamp(ZeroExtent - magnitude,
                    EditorMinimum, Math.Min(ZeroExtent, EditorMaximum));
                SetCurrentValue(MinimumExtentProperty, value);
            }
            else if (_dragTarget == DragTarget.Maximum)
            {
                double value = Math.Clamp(ZeroExtent + magnitude,
                    Math.Max(ZeroExtent, EditorMinimum), EditorMaximum);
                SetCurrentValue(MaximumExtentProperty, value);
            }
        }

        private Point Center => new(ActualWidth / 2.0, ActualHeight / 2.0);
        private double Radius => Math.Max(18, Math.Min(ActualWidth, ActualHeight) / 2.0 - 18.0);

        private Point MinimumHandlePoint
        {
            get
            {
                double magnitude = Math.Clamp(Math.Abs(ZeroExtent - MinimumExtent), 0.0, 180.0);
                return PointForAngle(Reverse ? -magnitude : magnitude, Radius + 10);
            }
        }

        private Point MaximumHandlePoint
        {
            get
            {
                double magnitude = Math.Clamp(Math.Abs(MaximumExtent - ZeroExtent), 0.0, 180.0);
                return PointForAngle(Reverse ? magnitude : -magnitude, Radius + 10);
            }
        }

        private Point PointForAngle(double degrees, double length)
        {
            double radians = degrees * Math.PI / 180.0;
            Point c = Center;
            return new Point(c.X + Math.Cos(radians) * length,
                             c.Y + Math.Sin(radians) * length);
        }

        private static double Normalize180(double degrees)
        {
            double d = degrees % 360.0;
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            return d;
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private Brush ResourceBrush(string key, Brush fallback) =>
            TryFindResource(key) as Brush ?? Application.Current?.TryFindResource(key) as Brush ?? fallback;
    }

    /// <summary>
    /// Two-handle linear extent editor for millimetre travel.  The numeric range
    /// remains ordered low-to-high; the zero marker follows Minimum in normal
    /// direction and Maximum when Reversed.
    /// </summary>
    public sealed class LinearRangeTrack : FrameworkElement
    {
        private enum DragTarget { None, Minimum, Maximum }
        private DragTarget _dragTarget;

        public LinearRangeTrack()
        {
            MinHeight = 42;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
        }

        public static readonly DependencyProperty MinimumExtentProperty =
            DependencyProperty.Register(nameof(MinimumExtent), typeof(double), typeof(LinearRangeTrack),
                new FrameworkPropertyMetadata(0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVisualPropertyChanged));

        public static readonly DependencyProperty MaximumExtentProperty =
            DependencyProperty.Register(nameof(MaximumExtent), typeof(double), typeof(LinearRangeTrack),
                new FrameworkPropertyMetadata(100.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVisualPropertyChanged));

        public static readonly DependencyProperty ReverseProperty =
            DependencyProperty.Register(nameof(Reverse), typeof(bool), typeof(LinearRangeTrack),
                new FrameworkPropertyMetadata(false, OnVisualPropertyChanged));

        public static readonly DependencyProperty EditorMinimumProperty =
            DependencyProperty.Register(nameof(EditorMinimum), typeof(double), typeof(LinearRangeTrack),
                new FrameworkPropertyMetadata(0.0, OnVisualPropertyChanged));

        public static readonly DependencyProperty EditorMaximumProperty =
            DependencyProperty.Register(nameof(EditorMaximum), typeof(double), typeof(LinearRangeTrack),
                new FrameworkPropertyMetadata(100.0, OnVisualPropertyChanged));

        public double MinimumExtent
        {
            get => (double)GetValue(MinimumExtentProperty);
            set => SetValue(MinimumExtentProperty, value);
        }

        public double MaximumExtent
        {
            get => (double)GetValue(MaximumExtentProperty);
            set => SetValue(MaximumExtentProperty, value);
        }

        public bool Reverse
        {
            get => (bool)GetValue(ReverseProperty);
            set => SetValue(ReverseProperty, value);
        }

        public double EditorMinimum
        {
            get => (double)GetValue(EditorMinimumProperty);
            set => SetValue(EditorMinimumProperty, value);
        }

        public double EditorMaximum
        {
            get => (double)GetValue(EditorMaximumProperty);
            set => SetValue(EditorMaximumProperty, value);
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((LinearRangeTrack)d).InvalidateVisual();

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
            return new Size(Math.Max(260, w), 42);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0) return;

            Brush trackBrush = ResourceBrush("DividerBrush", Brushes.Gray);
            Brush minBrush = ResourceBrush("WarningText", Brushes.Goldenrod);
            Brush maxBrush = ResourceBrush("SequenceAccent", Brushes.CornflowerBlue);
            Brush zeroBrush = ResourceBrush("PrimaryText", Brushes.WhiteSmoke);
            Brush panelBrush = ResourceBrush("PanelBackground", Brushes.Black);

            double y = ActualHeight / 2.0;
            double x0 = TrackStart;
            double x1 = TrackEnd;
            dc.DrawLine(new Pen(trackBrush, 4.0), new Point(x0, y), new Point(x1, y));

            double minX = XForValue(MinimumExtent);
            double maxX = XForValue(MaximumExtent);
            dc.DrawLine(new Pen(minBrush, 4.0), new Point(minX, y - 12), new Point(minX, y + 12));
            dc.DrawLine(new Pen(maxBrush, 4.0), new Point(maxX, y - 12), new Point(maxX, y + 12));
            dc.DrawEllipse(Brushes.Transparent, null, new Point(minX, y), 20, 20);
            dc.DrawEllipse(Brushes.Transparent, null, new Point(maxX, y), 20, 20);
            dc.DrawEllipse(panelBrush, new Pen(minBrush, 3.0), new Point(minX, y), 7, 7);
            dc.DrawEllipse(panelBrush, new Pen(maxBrush, 3.0), new Point(maxX, y), 7, 7);

            double zeroX = Reverse ? maxX : minX;
            dc.DrawEllipse(null, new Pen(zeroBrush, 3.0), new Point(zeroX, y), 11, 11);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);
            double minX = XForValue(MinimumExtent);
            double maxX = XForValue(MaximumExtent);
            double dMin = Math.Abs(p.X - minX);
            double dMax = Math.Abs(p.X - maxX);
            if (Math.Min(dMin, dMax) > 20) return;

            _dragTarget = dMin <= dMax ? DragTarget.Minimum : DragTarget.Maximum;
            CaptureMouse();
            UpdateDrag(p.X);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed) return;
            UpdateDrag(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragTarget == DragTarget.None) return;
            UpdateDrag(e.GetPosition(this).X);
            _dragTarget = DragTarget.None;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dragTarget = DragTarget.None;
        }

        private void UpdateDrag(double x)
        {
            double value = ValueForX(x);
            if (_dragTarget == DragTarget.Minimum)
            {
                value = Math.Min(value, MaximumExtent);
                SetCurrentValue(MinimumExtentProperty, value);
            }
            else if (_dragTarget == DragTarget.Maximum)
            {
                value = Math.Max(value, MinimumExtent);
                SetCurrentValue(MaximumExtentProperty, value);
            }
        }

        private double TrackStart => 16.0;
        private double TrackEnd => Math.Max(TrackStart + 1, ActualWidth - 16.0);

        private double XForValue(double value)
        {
            double span = EditorMaximum - EditorMinimum;
            if (span <= 1e-9) return TrackStart;
            double t = (Math.Clamp(value, EditorMinimum, EditorMaximum) - EditorMinimum) / span;
            return TrackStart + t * (TrackEnd - TrackStart);
        }

        private double ValueForX(double x)
        {
            double t = (Math.Clamp(x, TrackStart, TrackEnd) - TrackStart) / (TrackEnd - TrackStart);
            return EditorMinimum + t * (EditorMaximum - EditorMinimum);
        }

        private Brush ResourceBrush(string key, Brush fallback) =>
            TryFindResource(key) as Brush ?? Application.Current?.TryFindResource(key) as Brush ?? fallback;
    }
}
