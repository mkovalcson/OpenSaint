// ---------------------------------------------------------------------------
// SplineView.cs
//
// The spline area shown BELOW the waveform. For every servo whose "Spline"
// checkbox is ticked in the grid, this control draws the servo's command
// points interpolated with a CUBIC HERMITE SPLINE (Catmull-Rom style
// finite-difference tangents), one colored line per servo.
//
// Time synchronization: the view does not own its own zoom/scroll state -
// MainWindow copies ViewStart / PixelsPerSecond / CursorTime / Duration from
// the WaveformView whenever they change, so the spline area always lines up
// with the waveform above it (zoom, fit, scroll, playback cursor).
// Mouse input here (wheel zoom, middle-drag pan, left-click cursor) is
// forwarded to the WaveformView via SyncTarget, so interacting with either
// area drives both.
//
// Point editing (control points are the servo's commands):
//   * LEFT-drag a point        -> change its VALUE (vertical, clamped to the
//                                 servo's range)
//   * RIGHT-drag a point       -> move its TIME OFFSET (horizontal)
//   * CTRL + LEFT-click a line OR DOUBLE-LEFT-click a line -> create a NEW
//                                 point on the curve at that time (a new
//                                 command; a '+' appears on the waveform)
//   * LEFT-click a point, then press DELETE -> delete that point (the
//                                 selected point is drawn with a white ring)
//   * MIDDLE-drag anywhere (including points) -> pan the timeline
// The view raises events; MainWindow mutates the underlying ServoCommand
// objects and rebuilds the curves, so edits flow through the whole app.
//
// Vertical mapping: each servo's curve is normalized to ITS OWN value range
// (-100..100, 0..100, or 0..2000) mapped onto the full height of the area,
// so differently-ranged servos are all readable in one strip.
// ---------------------------------------------------------------------------

using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ServoAnimator
{
    // ==================== spline math ====================

    /// <summary>
    /// Cubic Hermite spline utilities, shared by the renderer and by the
    /// save-time sample generator in MainWindow.
    /// </summary>
    public static class SplineUtil
    {
        /// <summary>
        /// Catmull-Rom style tangents by finite differences:
        ///   interior: m[i] = (v[i+1] - v[i-1]) / (t[i+1] - t[i-1])
        ///   ends:     one-sided slope
        /// </summary>
        public static double[] Tangents(double[] t, double[] v)
        {
            int n = t.Length;
            var m = new double[n];
            if (n <= 1) return m;   // no segments -> no tangents (0 or 1 point)

            m[0] = (v[1] - v[0]) / Math.Max(1e-9, t[1] - t[0]);
            m[n - 1] = (v[n - 1] - v[n - 2]) / Math.Max(1e-9, t[n - 1] - t[n - 2]);
            for (int i = 1; i < n - 1; i++)
                m[i] = (v[i + 1] - v[i - 1]) / Math.Max(1e-9, t[i + 1] - t[i - 1]);
            return m;
        }

        /// <summary>
        /// Evaluate the Hermite spline at time x. Outside the control-point
        /// range the curve holds its end values. Uses the standard Hermite
        /// basis on the segment containing x.
        /// </summary>
        public static double Eval(double[] t, double[] v, double[] m, double x)
        {
            int n = t.Length;
            if (n == 0) return 0;
            if (n == 1 || x <= t[0]) return v[0];
            if (x >= t[n - 1]) return v[n - 1];

            // Binary search for the segment [t[i], t[i+1]] containing x.
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (t[mid] <= x) lo = mid; else hi = mid;
            }

            double h = t[lo + 1] - t[lo];
            double s = (x - t[lo]) / h;
            double s2 = s * s, s3 = s2 * s;

            double h00 = 2 * s3 - 3 * s2 + 1;   // Hermite basis functions
            double h10 = s3 - 2 * s2 + s;
            double h01 = -2 * s3 + 3 * s2;
            double h11 = s3 - s2;

            return h00 * v[lo] + h10 * h * m[lo] +
                   h01 * v[lo + 1] + h11 * h * m[lo + 1];
        }
    }

    /// <summary>One servo's spline data prepared for rendering/sampling.</summary>
    public class SplineCurve
    {
        public ServoNames Servo;
        public SKColor Color;
        public double[] T = Array.Empty<double>();   // control point times (sorted)
        public double[] V = Array.Empty<double>();   // control point values
        public double[] M = Array.Empty<double>();   // precomputed tangents
        public double Min, Max;                      // servo value range
        public bool Visible = true;                  // legend show/hide

        // NeckNodUp and NeckTiltRight can share one physical spline.  For
        // that special curve Owners/PointColors are parallel to T/V and say
        // which logical neck control owns each control point (and therefore
        // the segment that follows it).  Ordinary curves leave these empty.
        public bool IsSharedNeck;
        public ServoNames[] Owners = Array.Empty<ServoNames>();
        public SKColor[] PointColors = Array.Empty<SKColor>();
        public bool[] PointVisible = Array.Empty<bool>();
    }

    // ==================== the control ====================

    public class SplineView : SKElement
    {
        private readonly TimelineDrawingCache _drawingCache = new();
        public new void InvalidateVisual() { _drawingCache.Invalidate(); _cursorPresenter?.Update(); base.InvalidateVisual(); }
        private readonly TimelineCursorPresenter _cursorPresenter;
        public void InvalidateCursor() { if (_cursorPresenter?.Update() != true) base.InvalidateVisual(); }
        public bool CombinedMode { get; set; }
        private static SKColor ThemeSk(string key, SKColor fallback)
        {
            var c = ThemeManager.GetColor(key,
                System.Windows.Media.Color.FromArgb(fallback.Alpha, fallback.Red, fallback.Green, fallback.Blue));
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        // View state - mirrored from the WaveformView by MainWindow.
        public double ViewStart { get; set; }
        public double PixelsPerSecond { get; set; } = 100;
        public double CursorTime { get; set; }
        public double Duration { get; set; }

        /// <summary>Curves to draw (rebuilt by MainWindow after any edit).</summary>
        private List<SplineCurve> _curves = new();
        public List<SplineCurve> Curves
        {
            get => _curves;
            set { _curves = value ?? new(); InvalidateVisual(); }
        }

        /// <summary>The waveform to forward zoom/pan gestures to, keeping the
        /// two areas driving each other.</summary>
        public WaveformView SyncTarget { get; set; }

        /// <summary>Left click: move the cursor (handled by MainWindow).</summary>
        public event Action<double> TimeClicked;
        /// <summary>Right click on the cursor: open the shared cursor menu.</summary>
        public event Action<double> RightClicked;
        public event Action<double> CommandsEditRequested;

        // ---- point-editing events (MainWindow mutates the commands) ----
        /// <summary>Left-drag: (servo, point time key, new value).</summary>
        public event Action<ServoNames, double, int> PointValueChanged;
        /// <summary>Right-drag: (servo, old time key, new time key).</summary>
        public event Action<ServoNames, double, double> PointTimeChanged;
        /// <summary>Ctrl+left-click or double-left-click on a line:
        /// (servo, time key, value).</summary>
        public event Action<ServoNames, double, int> PointAdded;
        /// <summary>A point drag finished (MainWindow does a full refresh).</summary>
        public event Action DragCompleted;
        /// <summary>Delete pressed with a point selected: (servo, time key).</summary>
        public event Action<ServoNames, double> PointDeleted;
        private readonly System.Windows.Controls.ToolTip _pointInfo = new()
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint,
            IsHitTestVisible = false,
            StaysOpen = true,
        };

        private void ShowPointInfo(string text)
        {
            if (!IsMouseOver) { HidePointInfo(); return; }
            var p = Mouse.GetPosition(this);
            _pointInfo.Content = text;
            _pointInfo.HorizontalOffset = p.X + 14;
            _pointInfo.VerticalOffset = p.Y;
            _pointInfo.IsOpen = true;
        }

        private void HidePointInfo() => _pointInfo.IsOpen = false;

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HidePointInfo();
        }

        // ---- selection (left-click a point, then Delete removes it) ----
        private bool _hasSelection;
        private ServoNames _selServo;
        private double _selKey;

        public SplineView()
        {
            _cursorPresenter = new TimelineCursorPresenter(this, () => XAtTime(CursorTime),
                System.Windows.Media.Color.FromRgb(255, 80, 80), visible: () => !CombinedMode);
            _pointInfo.PlacementTarget = this;
            Unloaded += (_, _) => { HidePointInfo(); _drawingCache.Dispose(); };
            // Needed so the view can receive the Delete key after a point is
            // selected with the left mouse button.
            Focusable = true;
        }

        private enum DragKind { None, Value, Time }
        private DragKind _drag = DragKind.None;
        private ServoNames _dragServo;
        private double _dragKey;            // time key of the point being dragged

        private const float HitRadius = 7f;  // px tolerance for grabbing points/lines
        private const float CursorHitRadius = 7f;
        private const float Pad = 8f;        // vertical padding (matches rendering)

        private System.Windows.Point _panStart;
        private double _panStartView;
        private bool _panning;

        public double TimeAtX(double x) => ViewStart + (x - WaveformView.TimelineLeftInset) / PixelsPerSecond;
        public float XAtTime(double t) => WaveformView.TimelineLeftInset + (float)((t - ViewStart) * PixelsPerSecond);

        // ==================== rendering ====================

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);
            var canvas = e.Surface.Canvas;
            canvas.Clear(CombinedMode ? SKColors.Transparent : ThemeSk("PanelBackground", new SKColor(27, 30, 35)));
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            float scale = (float)(e.Info.Width / Math.Max(1.0, ActualWidth));

            float w = (float)ActualWidth;
            float h = (float)ActualHeight;
            float pad = Pad;   // vertical padding inside the strip

            _drawingCache.Draw(canvas, e.Info,
                (e.Info.Width, e.Info.Height, w, h, ViewStart, PixelsPerSecond, ThemeManager.CurrentTheme, CombinedMode),
                background =>
                {
                    background.Clear(CombinedMode ? SKColors.Transparent : ThemeSk("PanelBackground", new SKColor(27, 30, 35)));
                    background.Scale(scale);
                    if (!CombinedMode) DrawTimeGrid(background, w, h);
                    DrawValueGrid(background, w, h);
                    foreach (var c in Curves.Where(c => c.Visible)) DrawCurve(background, c, w, h, pad);
                });
            canvas.Scale(scale);

            // Playback / selection cursor, matching the waveform's.
            if (!CombinedMode && !_cursorPresenter.IsAttached)
            using (var cur = new SKPaint { Color = new SKColor(255, 80, 80), StrokeWidth = 2 })
            {
                float cx = XAtTime(CursorTime);
                if (cx >= -2 && cx <= w + 2) canvas.DrawLine(cx, 0, cx, h, cur);
            }
        }

        /// <summary>Vertical grid lines at the same "nice" tick spacing the
        /// waveform's time axis uses, so the two strips visibly align.</summary>
        private void DrawTimeGrid(SKCanvas canvas, float w, float h)
        {
            using var grid = new SKPaint { Color = ThemeSk("DividerBrush", new SKColor(46, 50, 58)), StrokeWidth = 1 };

            double[] nice = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                              1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
            double interval = nice[^1];
            foreach (double n in nice)
                if (n * PixelsPerSecond >= 70) { interval = n; break; }

            double visible = Math.Max(1, w - WaveformView.TimelineLeftInset) / Math.Max(1e-9, PixelsPerSecond);
            double first = Math.Floor(ViewStart / interval) * interval;
            for (double t = first; t <= ViewStart + visible + interval; t += interval)
            {
                if (t < 0) continue;
                float x = XAtTime(t);
                if (x < WaveformView.TimelineLeftInset || x > w + 2) continue;
                bool major = Math.Abs(t - Math.Round(t)) < 1e-6;
                grid.Color = ThemeSk("SecondaryText", new SKColor(150, 165, 185)).WithAlpha(major ? (byte)65 : (byte)27);
                canvas.DrawLine(x, 0, x, h, grid);
            }
        }

        internal static float StandardValueY(double value, float height) =>
            (float)(height - Pad - (value + 100) / 200 * (height - 2 * Pad));

        /// <summary>Reference lines for the usual -100..100 range. Other ranges
        /// still normalize to the same graph height; their exact values are shown on hover.</summary>
        private void DrawValueGrid(SKCanvas canvas, float width, float height)
        {
            using var line = new SKPaint { Color = ThemeSk("DividerBrush", new SKColor(70, 74, 82)), StrokeWidth = 1 };
            using var label = new SKPaint { Color = ThemeSk("SecondaryText", new SKColor(170, 175, 185)), TextSize = 9, IsAntialias = true };
            for (int value = -100; value <= 100; value += 25)
            {
                float y = StandardValueY(value, height);
                line.StrokeWidth = value == 0 ? 1.5f : 1;
                line.Color = ThemeSk("SecondaryText", new SKColor(170, 175, 185)).WithAlpha(value == 0 ? (byte)140 : (byte)48);
                canvas.DrawLine(0, y, width, y, line);
                // At small graph heights, retain every line but reduce label density.
                if (height >= 120 || value % 50 == 0)
                    canvas.DrawText(value > 0 ? $"+{value}" : value.ToString(), 4,
                        Math.Clamp(y - 2, 9, Math.Max(9, height - 2)), label);
            }
        }

        /// <summary>
        /// Draw one servo's spline: the Hermite-interpolated line sampled per
        /// screen pixel between its first and last control point, plus a dot
        /// at every control point. Values are normalized to the servo's own
        /// range so all curves use the full strip height.
        /// </summary>
        /// <summary>Servo value -> screen Y (each curve normalized to its
        /// own range over the strip height).</summary>
        private float YOf(SplineCurve c, double v)
        {
            float h = (float)ActualHeight;
            double norm = Math.Clamp((v - c.Min) / Math.Max(1e-9, c.Max - c.Min), 0, 1);
            return (float)(h - Pad - norm * (h - 2 * Pad));
        }

        /// <summary>Screen Y -> servo value (inverse of YOf, clamped).</summary>
        private double VOf(SplineCurve c, double y)
        {
            float h = (float)ActualHeight;
            double norm = (h - Pad - y) / Math.Max(1e-9, h - 2 * Pad);
            return c.Min + Math.Clamp(norm, 0, 1) * (c.Max - c.Min);
        }

        private void DrawCurve(SKCanvas canvas, SplineCurve c, float w, float h, float pad)
        {
            if (c.T.Length == 0) return;

            float YOf(double v) => this.YOf(c, v);

            using var line = new SKPaint
            {
                Color = c.Color, StrokeWidth = 2, IsAntialias = true,
                Style = SKPaintStyle.Stroke,
            };
            using var dot = new SKPaint { Color = c.Color, IsAntialias = true };

            if (c.T.Length >= 2)
            {
                if (c.IsSharedNeck && c.PointColors.Length == c.T.Length)
                {
                    // The neck pair is ONE Hermite curve, but ownership changes
                    // at its control points. Draw each interval in the color of
                    // the point that owns that interval so the line visibly
                    // changes color at NeckNodUp/NeckTiltRight hand-offs.
                    for (int seg = 0; seg < c.T.Length - 1; seg++)
                    {
                        if (c.PointVisible.Length == c.T.Length && !c.PointVisible[seg])
                            continue;
                        line.Color = c.PointColors[seg];
                        using var path = new SKPath();
                        bool started = false;
                        int x0 = Math.Max((int)WaveformView.TimelineLeftInset, (int)XAtTime(c.T[seg]));
                        int x1 = Math.Min((int)w, (int)XAtTime(c.T[seg + 1]) + 1);
                        for (int x = x0; x <= x1; x++)
                        {
                            double t = TimeAtX(x);
                            if (t < c.T[seg] || t > c.T[seg + 1]) continue;
                            float y = YOf(SplineUtil.Eval(c.T, c.V, c.M, t));
                            if (!started) { path.MoveTo(x, y); started = true; }
                            else path.LineTo(x, y);
                        }
                        if (started) canvas.DrawPath(path, line);
                    }
                }
                else
                {
                    // Ordinary servo: one color for the whole curve.
                    double tFirst = c.T[0], tLast = c.T[^1];
                    using var path = new SKPath();
                    bool started = false;

                    int x0 = Math.Max((int)WaveformView.TimelineLeftInset, (int)XAtTime(tFirst));
                    int x1 = Math.Min((int)w, (int)XAtTime(tLast) + 1);
                    for (int x = x0; x <= x1; x++)
                    {
                        double t = TimeAtX(x);
                        if (t < tFirst || t > tLast) continue;
                        float y = YOf(SplineUtil.Eval(c.T, c.V, c.M, t));
                        if (!started) { path.MoveTo(x, y); started = true; }
                        else path.LineTo(x, y);
                    }
                    if (started) canvas.DrawPath(path, line);
                }
            }

            // Control-point dots (the actual commands on the timeline). The
            // point selected with the left mouse button gets a white ring
            // (press Delete to remove it).
            using var sel = new SKPaint
            {
                Color = ThemeSk("PrimaryText", SKColors.White), StrokeWidth = 2.5f,
                Style = SKPaintStyle.Stroke, IsAntialias = true,
            };
            for (int i = 0; i < c.T.Length; i++)
            {
                double t = c.T[i], v = c.V[i];
                if (c.IsSharedNeck && c.PointVisible.Length == c.T.Length && !c.PointVisible[i])
                    continue;
                float x = XAtTime(t);
                if (x < -4 || x > w + 4) continue;
                float y = YOf(v);
                ServoNames owner = OwnerAt(c, i);
                dot.Color = c.IsSharedNeck && c.PointColors.Length == c.T.Length
                    ? c.PointColors[i] : c.Color;
                canvas.DrawCircle(x, y, 3.5f, dot);

                if (_hasSelection && _selServo == owner &&
                    ServoCommand.TimeKey(t) == _selKey)
                    canvas.DrawCircle(x, y, 6.5f, sel);
            }
        }

        // ==================== hit testing ====================

        /// <summary>Find a control-point dot near p (visible curves only).
        /// Returns the curve and sets index; null if nothing close.</summary>
        private SplineCurve HitPoint(System.Windows.Point p, out int index)
        {
            index = -1;
            SplineCurve best = null;
            double bestD = HitRadius;

            foreach (var c in Curves.Where(c => c.Visible))
            {
                for (int i = 0; i < c.T.Length; i++)
                {
                    if (c.IsSharedNeck && c.PointVisible.Length == c.T.Length && !c.PointVisible[i])
                        continue;
                    double dx = XAtTime(c.T[i]) - p.X;
                    double dy = YOf(c, c.V[i]) - p.Y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < bestD) { bestD = d; best = c; index = i; }
                }
            }
            return best;
        }

        /// <summary>Find a spline LINE near p: the curve whose interpolated Y
        /// at p.X is within HitRadius. Sets the time/value on the curve at
        /// that X. Null when p isn't near any visible line.</summary>
        private SplineCurve HitLine(System.Windows.Point p, out double t, out double v)
        {
            t = 0; v = 0;
            SplineCurve best = null;
            double bestD = HitRadius;

            foreach (var c in Curves.Where(c => c.Visible && c.T.Length >= 2))
            {
                double tt = TimeAtX(p.X);
                if (tt < c.T[0] || tt > c.T[^1]) continue;
                if (c.IsSharedNeck && c.PointVisible.Length == c.T.Length)
                {
                    // Determine visibility from the actual latest point, not merely
                    // the first occurrence of this owner.
                    int latest = 0;
                    for (int i = 1; i < c.T.Length && c.T[i] <= tt + 1e-9; i++) latest = i;
                    if (!c.PointVisible[latest]) continue;
                }
                double vv = SplineUtil.Eval(c.T, c.V, c.M, tt);
                double d = Math.Abs(YOf(c, vv) - p.Y);
                if (d < bestD) { bestD = d; best = c; t = tt; v = vv; }
            }
            return best;
        }

        private static bool IsSharedNeckServo(ServoNames servo) =>
            servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight;

        private static ServoNames OwnerAt(SplineCurve curve, int index)
        {
            if (curve.IsSharedNeck && curve.Owners.Length == curve.T.Length &&
                index >= 0 && index < curve.Owners.Length)
                return curve.Owners[index];
            return curve.Servo;
        }

        private static ServoNames OwnerAtTime(SplineCurve curve, double time)
        {
            if (!curve.IsSharedNeck || curve.Owners.Length != curve.T.Length ||
                curve.T.Length == 0)
                return curve.Servo;

            // Ownership changes exactly at a neck control point.  Between
            // points the most recent point remains in control.
            int index = 0;
            for (int i = 1; i < curve.T.Length && curve.T[i] <= time + 1e-9; i++)
                index = i;
            return curve.Owners[index];
        }

        private SplineCurve CurveOf(ServoNames servo) =>
            Curves.FirstOrDefault(c => c.Servo == servo ||
                (c.IsSharedNeck && IsSharedNeckServo(servo)));

        // ==================== mouse interaction ====================

        protected override System.Windows.Media.HitTestResult HitTestCore(System.Windows.Media.PointHitTestParameters parameters)
        {
            if (CombinedMode && !WantsCombinedInput(parameters.HitPoint)) return null;
            return base.HitTestCore(parameters);
        }

        internal bool WantsCombinedInput(System.Windows.Point point)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return false;
            if (SyncTarget != null && System.Windows.Media.VisualTreeHelper.GetParent(this) is System.Windows.DependencyObject parent &&
                ReferenceEquals(parent, System.Windows.Media.VisualTreeHelper.GetParent(SyncTarget)) &&
                SyncTarget.ClipHandleAt(TranslatePoint(point, SyncTarget)) != null) return false;
            return HitPoint(point, out _) != null || HitLine(point, out _, out _) != null;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            // Zoom the shared timeline; MainWindow mirrors the change back here.
            SyncTarget?.ZoomBy(e.Delta > 0 ? 1.25 : 1 / 1.25);
            e.Handled = true;
        }

        internal bool TryEditSelectedPoint(System.Windows.Point point, int clickCount)
        {
            if (clickCount != 2 || !_hasSelection) return false;
            var curve = HitPoint(point, out int index);
            if (curve == null || OwnerAt(curve, index) != _selServo ||
                ServoCommand.TimeKey(curve.T[index]) != _selKey) return false;
            _drag = DragKind.None;
            if (IsMouseCaptured) ReleaseMouseCapture();
            HidePointInfo();
            CommandsEditRequested?.Invoke(_selKey);
            return true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var p = e.GetPosition(this);

            // A point takes precedence over the line underneath it. This keeps
            // double-clicking an existing point from accidentally trying to add
            // another point at the same time.
            var pc = HitPoint(p, out int idx);

            if (TryEditSelectedPoint(p, e.ClickCount))
            {
                e.Handled = true;
                return;
            }

            // CTRL + left click OR a double-left-click on a spline line creates
            // a new control point on the curve.  On the shared neck curve the
            // new point inherits the logical owner of the PREVIOUS point, so a
            // user can extend a Nod or Tilt stretch without changing ownership.
            bool addGesture = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                              e.ClickCount >= 2;
            if (addGesture && pc == null)
            {
                var lc = HitLine(p, out double lt, out double lv);
                if (lc != null)
                {
                    double key = ServoCommand.TimeKey(lt);
                    ServoNames owner = OwnerAtTime(lc, lt);
                    PointAdded?.Invoke(owner, key, (int)Math.Round(lv));
                    TimeClicked?.Invoke(key);

                    // The new point becomes the selection (Delete undoes it).
                    _hasSelection = true;
                    _selServo = owner;
                    _selKey = key;
                    Focus();
                    ShowPointInfo($"{owner}: {lv:0} @ {key:F3} s (selected)");
                    InvalidateVisual();
                    e.Handled = true;
                }
                return;
            }

            // Left press ON a control point: SELECT it (Delete now removes
            // it) and start a VALUE drag (vertical).
            if (pc != null)
            {
                _drag = DragKind.Value;
                _dragServo = OwnerAt(pc, idx);
                _dragKey = ServoCommand.TimeKey(pc.T[idx]);

                _hasSelection = true;
                _selServo = _dragServo;
                _selKey = _dragKey;

                // Selecting a spline control point also selects that exact
                // timeline location. MainWindow consequently refreshes the
                // Commands-at-cursor list so every command sharing the point's
                // millisecond time key is visible immediately.
                TimeClicked?.Invoke(_dragKey);

                Focus();               // receive the Delete key
                CaptureMouse();
                ShowPointInfo($"{_dragServo}: {pc.V[idx]:0} @ {pc.T[idx]:F3} s (selected)");
                InvalidateVisual();    // show the selection ring
                return;
            }

            // Plain click on empty space: clear any selection and move the
            // cursor (as on the waveform).
            _hasSelection = false;
            HidePointInfo();
            InvalidateVisual();
            TimeClicked?.Invoke(Math.Clamp(TimeAtX(p.X), 0, Math.Max(0, Duration)));
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            // Right press ON a control point: start a TIME drag (horizontal).
            var pc = HitPoint(e.GetPosition(this), out int idx);
            if (pc != null)
            {
                _drag = DragKind.Time;
                _dragServo = OwnerAt(pc, idx);
                _dragKey = ServoCommand.TimeKey(pc.T[idx]);
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle)
            {
                var p = e.GetPosition(this);
                _panning = true;
                _panStart = p;
                _panStartView = ViewStart;
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            _panning = false;
            base.OnLostMouseCapture(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = e.GetPosition(this);

            if (_drag == DragKind.Value)
            {
                // Dragging up/down rewrites the point's value, clamped to the
                // servo's range by VOf(). MainWindow updates the command and
                // rebuilds the curves, so the line follows the mouse live.
                var c = CurveOf(_dragServo);
                if (c != null)
                {
                    int value = (int)Math.Round(VOf(c, p.Y));
                    PointValueChanged?.Invoke(_dragServo, _dragKey, value);
                    ShowPointInfo($"{_dragServo}: {value} @ {_dragKey:F3} s (selected)");
                }
                return;
            }

            if (_drag == DragKind.Time)
            {
                if (_pointInfo.IsOpen) ShowPointInfo(_pointInfo.Content as string);
                // Dragging left/right moves the point's time offset. Skip
                // moves that would land exactly on another point of the same
                // servo (two control points can't share a time).
                double newKey = ServoCommand.TimeKey(
                    Math.Clamp(TimeAtX(p.X), 0, Math.Max(0, Duration)));
                if (newKey == _dragKey) return;

                var c = CurveOf(_dragServo);
                if (c != null && c.T.Any(t => ServoCommand.TimeKey(t) == newKey))
                    return;   // collision with an existing point

                PointTimeChanged?.Invoke(_dragServo, _dragKey, newKey);
                if (_hasSelection && _selServo == _dragServo && _selKey == _dragKey)
                    _selKey = newKey;   // the selected point moved with the drag
                _dragKey = newKey;      // continue the drag from the new key
                var moved = CurveOf(_dragServo);
                int movedIndex = moved == null ? -1 : Array.FindIndex(moved.T, tt => ServoCommand.TimeKey(tt) == newKey);
                double movedValue = movedIndex >= 0 ? moved.V[movedIndex] : 0;
                ShowPointInfo($"{_dragServo}: {movedValue:0} @ {newKey:F3} s (selected)");
                return;
            }

            if (_panning)
            {
                HidePointInfo();
                double dx = p.X - _panStart.X;
                SyncTarget?.SetViewStart(_panStartView - dx / PixelsPerSecond);
                return;
            }

            // Hover feedback: show exact servo/time/value without requiring a click.
            var hp = HitPoint(p, out int hi);
            if (hp != null)
            {
                Cursor = Cursors.Hand;
                ServoNames owner = OwnerAt(hp, hi);
                ShowPointInfo($"{owner}: {hp.V[hi]:0} @ {hp.T[hi]:F3} s" +
                    (_hasSelection && _selServo == owner && _selKey == ServoCommand.TimeKey(hp.T[hi]) ? " (selected)" : ""));
            }
            else
            {
                var hl = HitLine(p, out double ht, out double hv);
                Cursor = hl != null ? Cursors.Cross : Cursors.Arrow;
                if (hl != null) ShowPointInfo($"{OwnerAtTime(hl, ht)}: {hv:0.##} @ {ht:F3} s");
                else HidePointInfo();
            }
        }

        /// <summary>Delete removes the point selected with the left mouse
        /// button. MainWindow deletes the underlying command(s) and refreshes
        /// (the '+' disappears from the waveform if nothing else lives at
        /// that time).</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key != Key.Delete || !_hasSelection) return;

            PointDeleted?.Invoke(_selServo, _selKey);
            _hasSelection = false;
            HidePointInfo();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_drag != DragKind.None &&
                (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right))
            {
                _drag = DragKind.None;
                ReleaseMouseCapture();
                DragCompleted?.Invoke();   // MainWindow does a full refresh
                return;
            }

            // Keep right-drag on a control point dedicated to moving its time,
            // but let a right click elsewhere on the vertical cursor open the
            // same cursor actions that are available from the waveform.
            if (e.ChangedButton == MouseButton.Right &&
                (CombinedMode || Math.Abs(e.GetPosition(this).X - XAtTime(CursorTime)) <= CursorHitRadius))
            {
                RightClicked?.Invoke(CursorTime);
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Middle && _panning)
            {
                _panning = false;
                ReleaseMouseCapture();
            }
        }
    }
}
