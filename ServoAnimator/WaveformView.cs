// ---------------------------------------------------------------------------
// WaveformView.cs
//
// A SkiaSharp-based control that renders:
//   * the audio waveform (min/max peak envelope)
//   * a scalable time axis (tick spacing adapts to zoom level)
//   * shallow downward-pointing triangle markers at every unique command offset
//   * the playback / selection cursor (vertical line)
//
// Interaction handled here:
//   * Left click            -> raises TimeClicked (snapped to a nearby command
//                              marker when within a few pixels)
//   * Mouse wheel           -> zoom in/out around the mouse position
//   * Middle button drag    -> pan (scroll) the visible time range
//   * Right click           -> raises RightClicked so MainWindow can open
//                              its context menu (cursor is NOT moved)
//
// The view exposes ViewStart (seconds at left edge) and PixelsPerSecond
// (zoom). MainWindow keeps an external horizontal ScrollBar in sync via the
// ViewChanged event.
// ---------------------------------------------------------------------------

using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ServoAnimator
{
    public class WaveformView : SKElement
    {
        private static SKColor ThemeSk(string key, SKColor fallback)
        {
            var c = ThemeManager.GetColor(key,
                System.Windows.Media.Color.FromArgb(fallback.Alpha, fallback.Red, fallback.Green, fallback.Blue));
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        // ---------------- audio peak data ----------------
        // The full audio is reduced to a fixed-resolution envelope: for every
        // "bucket" of samples we store the min and max sample seen. Rendering
        // then aggregates buckets per screen pixel, which stays fast at any
        // zoom level and any file length.
        private float[] _peakMin = Array.Empty<float>();
        private readonly TimelineDrawingCache _drawingCache = new();
        private bool _combinedPresentation;
        public bool CombinedPresentation
        {
            get => _combinedPresentation;
            set { if (_combinedPresentation == value) return; _combinedPresentation = value; InvalidateVisual(); }
        }
        public new void InvalidateVisual() { _drawingCache.Invalidate(); _cursorPresenter?.Update(); base.InvalidateVisual(); }
        private readonly TimelineCursorPresenter _cursorPresenter;
        public void InvalidateCursor() { if (_cursorPresenter?.Update() != true) base.InvalidateVisual(); }
        private float[] _peakMax = Array.Empty<float>();
        private double _bucketDuration = 0.001;    // seconds represented by one bucket

        // ---------------- view state ----------------
        public double Duration { get; set; }              // total timeline length (s)
                                                          // (content + edit tail)
        /// <summary>Content-only extent (last waveform/command); ZoomToFit
        /// fits this so the view isn't dominated by the empty tail.</summary>
        public double ContentDuration { get; set; }

        // ---------------- audio start offset ----------------
        // The audio can be shifted right on the timeline so that commands can
        // be placed BEFORE the audio starts. A small drag handle is drawn at
        // the audio's start position (top-left of the waveform); dragging it
        // moves the whole waveform. Timeline time = AudioOffset + audio time.
        private double _audioOffset;
        private double _audioLength;                       // decoded audio length (s)
        public double AudioOffset
        {
            get => _audioOffset;
            set
            {
                _audioOffset = Math.Max(0, value);
                if (_audioLength > 0)
                    Duration = _audioOffset + _audioLength;   // timeline grows/shrinks
                InvalidateVisual();
            }
        }
        /// <summary>Raised continuously while the offset handle is dragged.</summary>
        public event Action<double> AudioOffsetChanged;
        /// <summary>Raised once when the offset-handle drag is released, so
        /// MainWindow can close the undo step and do a full refresh.</summary>
        public event Action AudioOffsetDragEnded;
        private bool _draggingOffset;
        private const float HandleWidth = 14f;             // grab-handle size (DIU)
        // Always reserve room for the handle plus a gap before a zero-time triangle.
        public const float TimelineLeftInset = HandleWidth + 10f;
        private double _offsetGrabDeltaX;
        private const float OffsetHandleHeight = 18f;       // independent of staggered marker-lane height

        // ---------------- Animation Library selection arrows ----------------
        // Used by the Animation Library menu:
        //   * Range mode  - a GREEN start arrow and a RED end arrow (both
        //     pointing down at the top of the timeline, draggable with the
        //     left mouse button; the red arrow can never sit left of the
        //     green one). Create Library Sequence exports the commands between
        //     them, re-based so the green arrow is 0 s.
        //   * Insert mode - a single BLUE arrow marking where a library
        //     sequence will be inserted.
        // The arrows exist only while a mode is active and disappear when
        // MainWindow calls EndArrowMode().
        public enum SelectionMode { None, Range, Insert }
        public SelectionMode ArrowMode { get; private set; } = SelectionMode.None;
        public double RangeStart { get; private set; }
        public double RangeEnd { get; private set; }
        public double InsertTime { get; private set; }
        private int _dragArrow;   // 0 none, 1 green, 2 red, 3 blue

        /// <summary>Return the portion of the timeline that is currently
        /// visible, clamped to the editable timeline extent.</summary>
        private (double Start, double End) VisibleTimelineWindow()
        {
            double duration = Math.Max(0, Duration);
            double start = Math.Clamp(ViewStart, 0, duration);
            double end = Math.Clamp(ViewStart + VisibleSeconds, start, duration);

            // A zero-width window can occur briefly during initial layout.
            // Fall back to the full timeline so the arrows remain usable.
            if (end - start < 0.001)
            {
                start = 0;
                end = duration;
            }
            return (start, end);
        }

        /// <summary>Default Library range includes the first through last command.</summary>
        public void BeginRangeSelect()
        {
            ArrowMode = SelectionMode.Range;
            RangeStart = Markers.Count == 0 ? 0 : Markers.Min();
            RangeEnd = Markers.Count == 0 ? 0 : Markers.Max();
            InvalidateVisual();
        }

        internal void MoveRangeArrow(int arrow, double time)
        {
            double snapped = Markers.Count == 0 ? 0 : Markers.OrderBy(m => Math.Abs(m - time)).ThenBy(m => m).First();
            if (arrow == 1) RangeStart = Math.Min(snapped, RangeEnd);
            if (arrow == 2) RangeEnd = Math.Max(snapped, RangeStart);
            InvalidateVisual();
        }

        /// <summary>Start Insert-Library-Sequence selection with the blue
        /// arrow centered in the currently visible timeline viewport.</summary>
        public void BeginInsertSelect()
        {
            ArrowMode = SelectionMode.Insert;
            var (start, end) = VisibleTimelineWindow();
            InsertTime = start + Math.Max(0, end - start) / 2.0;
            InvalidateVisual();
        }

        /// <summary>Hide the arrows (mode finished or cancelled).</summary>
        public void EndArrowMode()
        {
            ArrowMode = SelectionMode.None;
            _dragArrow = 0;
            InvalidateVisual();
        }
        public double ViewStart { get; private set; }      // time at the left edge (s)
        public double PixelsPerSecond { get; private set; } = 100.0;
        public double CursorTime { get; set; }             // current cursor position (s)

        /// <summary>Unique command offsets that get a downward triangle marker.</summary>
        private IReadOnlyList<double> _markers = Array.Empty<double>();
        public IReadOnlyList<double> Markers
        {
            get => _markers;
            set { _markers = value ?? Array.Empty<double>(); HideMarkerToolTip(); }
        }

        /// <summary>Command offsets whose command group introduced a URDF
        /// collision during playback. These markers are rendered bright red
        /// until MainWindow clears the warnings after the commands are edited.</summary>
        public IReadOnlyCollection<double> CollisionMarkers { get; set; } = Array.Empty<double>();

        /// <summary>How close (pixels) a left-click must be to a marker to snap to it.</summary>
        public double MarkerSnapPixels { get; set; } = 6.0;

        /// <summary>Optional formatter supplied by MainWindow for rich marker hover text.</summary>
        public Func<double, string> MarkerToolTipProvider { get; set; }
        private readonly ToolTip _markerToolTip = new()
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint,
            IsHitTestVisible = false,
            StaysOpen = true,
            MaxWidth = 640,
        };
        private double? _hoverMarker;

        private void HideMarkerToolTip()
        {
            _hoverMarker = null;
            _markerToolTip.IsOpen = false;
        }

        private void UpdateMarkerToolTip(Point point)
        {
            if (!TryHitMarker(point, out double marker)) { HideMarkerToolTip(); return; }
            if (_hoverMarker != marker)
            {
                _hoverMarker = marker;
                _markerToolTip.Content = new TextBlock
                {
                    Text = MarkerToolTipProvider?.Invoke(marker) ?? $"Commands at {marker:F3} s",
                    TextWrapping = TextWrapping.Wrap,
                };
            }
            _markerToolTip.HorizontalOffset = point.X + 14;
            _markerToolTip.VerticalOffset = point.Y + 12;
            // Explicitly reopen on every re-entry, without ToolTipService's per-control timeout.
            _markerToolTip.IsOpen = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HideMarkerToolTip();
        }

        // ---------------- additional audio clips ----------------
        // Each additional audio file on the timeline is backed by a "Play"
        // command in the document; these visuals (peaks + name + a drag
        // handle at the lower-left of the clip's start) are DERIVED from
        // those commands, so Start always reads the command's offset.
        public List<AudioClipVisual> AudioClips { get; set; } = new();

        /// <summary>The primary audio's filename, drawn at the lower-left
        /// of where it starts (replaces the old top-bar label).</summary>
        public string PrimaryAudioName { get; set; } = "";

        /// <summary>A clip handle is being dragged / had its offset set:
        /// (clip, requested new start). MainWindow moves the clip's Play
        /// command AND everything at/right of its old start - commands,
        /// other clips, and the primary waveform.</summary>
        public event Action<AudioClipVisual, double> ClipMoveRequested;
        /// <summary>The clip-handle drag was released.</summary>
        public event Action ClipDragCompleted;
        /// <summary>Right-click on a clip handle: open the set-offset dialog.</summary>
        public event Action<AudioClipVisual> ClipOffsetDialogRequested;

        private AudioClipVisual _draggingClip;
        private const float ClipHandleW = 9f;
        private const float ClipHandleH = 16f;

        // ---------------- events ----------------
        /// <summary>Left click: time in seconds (already snapped to a marker if close).</summary>
        public event Action<double> TimeClicked;

        // ---- command-marker dragging: grab a triangle in the top lane and drag
        //      the whole command group at that time to a new offset ----
        /// <summary>A grabbed marker moved: (old time key, new time key).
        /// MainWindow moves the commands and refreshes the marker list.</summary>
        public event Action<double, double> MarkerDragged;
        public event Action<double> CommandsEditRequested;
        /// <summary>The marker drag was released at the given final key.</summary>
        public event Action<double> MarkerDragCompleted;
        public event Action<double> SelectedMarkersMoved;
        public event Action MarkerSelectionChanged;
        public HashSet<double> HighlightedControlMarkers { get; } = new();
        private readonly HashSet<double> _selectedMarkers = new();
        public IReadOnlyCollection<double> SelectedMarkers => _selectedMarkers;
        public double[][] CommandGroups { get; set; } = Array.Empty<double[]>();
        public Dictionary<double, SKColor> GroupColors { get; set; } = new();
        private void ExpandPersistentSelection()
        {
            bool changed;
            do
            {
                int count = _selectedMarkers.Count;
                foreach (var group in CommandGroups) if (group.Any(_selectedMarkers.Contains)) _selectedMarkers.UnionWith(group);
                changed = count != _selectedMarkers.Count;
            } while (changed);
        }
        private double? _selectionAnchor;
        private double _groupDragOffset;

        public void SetMarkerSelection(IEnumerable<double> times)
        {
            var keys = times.Select(ServoCommand.TimeKey).ToArray();
            _selectedMarkers.Clear();
            _selectedMarkers.UnionWith(keys);
            ExpandPersistentSelection();
            if (_selectedMarkers.Count == 0) _selectionAnchor = null;
            else if (!_selectionAnchor.HasValue || !_selectedMarkers.Contains(_selectionAnchor.Value))
                _selectionAnchor = _selectedMarkers.Min();
            InvalidateVisual();
            MarkerSelectionChanged?.Invoke();
        }

        internal void SelectMarker(double time, bool control, bool shift)
        {
            time = ServoCommand.TimeKey(time);
            if (shift)
            {
                double anchor = _selectionAnchor ?? (Markers.Count == 0 ? time : Markers.OrderBy(m => Math.Abs(m - CursorTime)).First());
                _selectionAnchor = anchor;
                _selectedMarkers.UnionWith(Markers.Where(m => m >= Math.Min(anchor, time) && m <= Math.Max(anchor, time)));
            }
            else if (control)
            {
                if (_selectedMarkers.Remove(time))
                {
                    foreach (var group in CommandGroups.Where(g => g.Contains(time))) _selectedMarkers.ExceptWith(group);
                    if (_selectionAnchor == time)
                        _selectionAnchor = _selectedMarkers.Count == 0 ? null : _selectedMarkers.OrderBy(m => Math.Abs(m - time)).First();
                }
                else
                {
                    _selectedMarkers.Add(time);
                    _selectionAnchor = time;
                }
            }
            else
            {
                _selectedMarkers.Add(time);
                _selectionAnchor = time;
            }
            ExpandPersistentSelection();
            InvalidateVisual();
            MarkerSelectionChanged?.Invoke();
        }

        internal void PreviewSelectedMarkerMove(double offset)
        {
            if (_selectedMarkers.Count == 0) return;
            _groupDragOffset = ServoCommand.TimeKey(Math.Clamp(offset, -_selectedMarkers.Min(), Math.Max(0, Duration - _selectedMarkers.Max())));
            InvalidateVisual();
        }

        private bool _markerPressPending;   // pressed a marker, not yet dragging
        private bool _draggingMarker;
        private double _markerKey;          // key of the group being dragged
        private double _markerPressX;
        private const double MarkerDragThreshold = 4.0;   // px before a drag starts
        /// <summary>Right click: raw time in seconds under the mouse.</summary>
        public event Action<double> RightClicked;
        /// <summary>Raised whenever zoom/scroll changes so the scrollbar can sync.</summary>
        public event Action ViewChanged;

        // layout constants (device-independent pixels)
        private const float AxisHeight = 26f;   // time axis strip at the bottom
        private const float MarkerLane = 38f;   // staggered triangle-marker strip at the top

        private sealed class MarkerVisual
        {
            public double Time { get; init; }
            public float X { get; init; }
            public float BaseY { get; init; }
            public float TipY { get; init; }
        }

        /// <summary>Assign nearby command markers to vertical lanes so their
        /// triangles remain separately visible/clickable when their X ranges
        /// overlap. The lane assignment is recalculated from the current zoom
        /// and pan, so it always reflects what is actually overlapping on screen.</summary>
        private List<MarkerVisual> BuildMarkerVisuals(float width)
        {
            const int laneCount = 5;
            const float minSeparation = 14f;
            const float baseTop = 2f;
            const float laneStep = 7f;
            const float triangleHeight = 5f;

            float[] lastX = Enumerable.Repeat(float.NegativeInfinity, laneCount).ToArray();
            var result = new List<MarkerVisual>();

            foreach (double time in Markers.OrderBy(m => m + (_selectedMarkers.Contains(m) ? _groupDragOffset : 0)))
            {
                float x = XAtTime(time + (_selectedMarkers.Contains(time) ? _groupDragOffset : 0));
                if (x < -12 || x > width + 12) continue;

                int lane = -1;
                for (int i = 0; i < laneCount; i++)
                    if (x - lastX[i] >= minSeparation) { lane = i; break; }

                // Extremely dense clusters can exceed the available lanes.
                // Reuse the lane whose previous marker is furthest away.
                if (lane < 0)
                {
                    lane = 0;
                    for (int i = 1; i < laneCount; i++)
                        if (lastX[i] < lastX[lane]) lane = i;
                }

                float baseY = baseTop + lane * laneStep;
                result.Add(new MarkerVisual
                {
                    Time = time,
                    X = x,
                    BaseY = baseY,
                    TipY = baseY + triangleHeight,
                });
                lastX[lane] = x;
            }
            return result;
        }

        /// <summary>Hit-test the visible triangle itself, including its
        /// staggered Y lane, so overlapping command times can be selected
        /// individually rather than only by nearest X coordinate.</summary>
        private bool TryHitMarker(Point point, out double markerTime)
        {
            markerTime = 0;
            MarkerVisual best = null;
            double bestScore = double.MaxValue;
            foreach (var mv in BuildMarkerVisuals((float)ActualWidth))
            {
                double dx = Math.Abs(point.X - mv.X);
                if (dx > 8) continue;
                if (point.Y < mv.BaseY - 3 || point.Y > mv.TipY + 4) continue;
                double dy = Math.Abs(point.Y - (mv.BaseY + mv.TipY) / 2.0);
                double score = dx + dy * 0.6;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = mv;
                }
            }
            if (best == null) return false;
            markerTime = best.Time;
            return true;
        }

        internal bool TryEditMarker(Point point, int clickCount)
        {
            if (clickCount != 2 || !TryHitMarker(point, out double time)) return false;
            _markerPressPending = false;
            _draggingMarker = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            HideMarkerToolTip();
            CommandsEditRequested?.Invoke(time);
            return true;
        }

        private Point _panStart;
        private double _panStartView;
        private bool _panning;

        public WaveformView()
        {
            _cursorPresenter = new TimelineCursorPresenter(this, () => XAtTime(CursorTime),
                System.Windows.Media.Color.FromRgb(255, 80, 80), AxisHeight);
            Focusable = true;
            _markerToolTip.PlacementTarget = this;
            Unloaded += (_, _) => { HideMarkerToolTip(); _drawingCache.Dispose(); };
            SnapsToDevicePixels = true;
        }

        // ==================== public API ====================

        /// <summary>Peak amplitude (0..1) of the audio around a position in
        /// AUDIO seconds - a ±15 ms window over the peak buckets. Drives the
        /// robot head's talking rectangle during playback.</summary>
        public double AmplitudeAt(double audioSeconds)
        {
            if (_peakMin.Length == 0 || audioSeconds < 0) return 0;

            int i0 = (int)((audioSeconds - 0.015) / _bucketDuration);
            int i1 = (int)((audioSeconds + 0.015) / _bucketDuration);
            i0 = Math.Clamp(i0, 0, _peakMin.Length - 1);
            i1 = Math.Clamp(i1, i0, _peakMin.Length - 1);

            float amp = 0;
            for (int i = i0; i <= i1; i++)
                amp = Math.Max(amp, Math.Max(Math.Abs(_peakMax[i]), Math.Abs(_peakMin[i])));
            return Math.Clamp(amp, 0, 1);
        }

        /// <summary>Install new audio peak data (called after decoding a file).</summary>
        public void SetAudio(float[] peakMin, float[] peakMax, double bucketDuration, double audioDuration)
        {
            _peakMin = peakMin ?? Array.Empty<float>();
            _peakMax = peakMax ?? Array.Empty<float>();
            _bucketDuration = Math.Max(1e-6, bucketDuration);
            _audioLength = audioDuration;
            Duration = _audioOffset + _audioLength;   // timeline = offset + audio
            ZoomToFit();
        }

        /// <summary>Convert a pixel X coordinate to a time in seconds.</summary>
        public double TimeAtX(double x) => ViewStart + (x - TimelineLeftInset) / PixelsPerSecond;

        /// <summary>Convert a time in seconds to a pixel X coordinate.</summary>
        public float XAtTime(double t) => TimelineLeftInset + (float)((t - ViewStart) * PixelsPerSecond);

        /// <summary>Seconds of audio currently visible.</summary>
        public double PlotWidth => Math.Max(1.0, ActualWidth - TimelineLeftInset);
        public double VisibleSeconds => PlotWidth / PixelsPerSecond;

        public void ZoomToFit()
        {
            double fit = ContentDuration > 0 ? ContentDuration : Duration;
            if (fit <= 0 || ActualWidth <= 0) return;
            PixelsPerSecond = Math.Max(1.0, PlotWidth / fit);
            SetViewStart(0);
            InvalidateVisual();
        }

        public void ZoomBy(double factor)
        {
            if (!double.IsFinite(factor) || factor <= 0 || ActualWidth <= 0) return;
            double cursorX = (CursorTime - ViewStart) * PixelsPerSecond;

            PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, 1.0, 20000.0);

            // Anchor wheel and button zoom to the timeline cursor, not the mouse
            // or viewport center. Normal timeline bounds still apply on zoom-out.
            SetViewStart(CursorTime - cursorX / PixelsPerSecond);
            InvalidateVisual();
        }

        public void SetViewStart(double seconds)
        {
            HideMarkerToolTip();
            double maxStart = Math.Max(0, Duration - VisibleSeconds);
            ViewStart = Math.Clamp(seconds, 0, maxStart);
            ViewChanged?.Invoke();
            InvalidateVisual();
        }

        /// <summary>Scroll (if needed) so the given time is on screen -
        /// used to follow the cursor during playback.</summary>
        public void EnsureVisible(double t)
        {
            if (t < ViewStart || t > ViewStart + VisibleSeconds * 0.95)
                SetViewStart(t - VisibleSeconds * 0.1);
        }

        // ==================== rendering ====================

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);
            var canvas = e.Surface.Canvas;
            canvas.Clear(ThemeSk("AppBackground", new SKColor(24, 26, 30)));

            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            // The Skia surface is in device pixels; scale so the rest of the
            // drawing code can work in WPF device-independent units.
            float scale = (float)(e.Info.Width / Math.Max(1.0, ActualWidth));

            float w = (float)ActualWidth;
            float h = (float)ActualHeight;
            float waveTop = MarkerLane;
            float waveBottom = h - AxisHeight;
            float waveMidY = (waveTop + waveBottom) / 2f;
            float waveHalf = (waveBottom - waveTop) / 2f * 0.95f;

            _drawingCache.Draw(canvas, e.Info,
                (e.Info.Width, e.Info.Height, w, h, ViewStart, PixelsPerSecond, ThemeManager.CurrentTheme),
                background =>
                {
                    background.Clear(ThemeSk("AppBackground", new SKColor(24, 26, 30)));
                    background.Scale(scale);
                    DrawPreRollShade(background, waveTop, waveBottom);
                    DrawWaveform(background, w, waveMidY, waveHalf);
                    DrawAudioClips(background, w, waveMidY, waveHalf, waveBottom);
                    DrawTimeAxis(background, w, h);
                    DrawMarkers(background, w);
                    DrawOffsetHandle(background);
                });
            canvas.Scale(scale);
            DrawSelectionArrows(canvas, h);
            if (!_cursorPresenter.IsAttached) DrawCursor(canvas, h);
        }

        private void DrawWaveform(SKCanvas canvas, float width, float midY, float half)
        {
            using var wavePaint = new SKPaint
            {
                Color = ThemeSk("SequenceAccent", new SKColor(90, 190, 255)).WithAlpha(CombinedPresentation ? (byte)90 : (byte)220),
                StrokeWidth = 1,
                IsAntialias = false,
            };
            using var centerPaint = new SKPaint
            {
                Color = ThemeSk("DividerBrush", new SKColor(70, 74, 82)),
                StrokeWidth = 1,
            };

            canvas.DrawLine(TimelineLeftInset, midY, width, midY, centerPaint);
            if (_peakMin.Length == 0) return;

            // One vertical line per pixel column: aggregate the min/max of all
            // peak buckets that fall inside that pixel's time span. The audio
            // is shifted right by AudioOffset, so convert timeline time to
            // audio-relative time before indexing into the peak buckets.
            for (int x = (int)TimelineLeftInset; x < (int)width; x++)
            {
                double t0 = TimeAtX(x) - _audioOffset;      // audio-relative
                double t1 = TimeAtX(x + 1) - _audioOffset;
                if (t1 <= 0 || t0 >= _audioLength) continue;

                int i0 = Math.Clamp((int)(t0 / _bucketDuration), 0, _peakMin.Length - 1);
                int i1 = Math.Clamp((int)(t1 / _bucketDuration), i0, _peakMin.Length - 1);

                var (mn, mx) = PeakEnvelope.For(_peakMin, _peakMax).Query(i0, i1);
                if (mn > mx) continue;

                float yTop = midY - mx * half;
                float yBot = midY - mn * half;
                if (yBot - yTop < 1f) yBot = yTop + 1f;   // always visible
                canvas.DrawLine(x, yTop, x, yBot, wavePaint);
            }
        }

        /// <summary>
        /// Additional audio clips: an amber envelope over the clip's span,
        /// the filename at the LOWER-LEFT of where the clip starts, and a
        /// small drag handle just left of the name. The primary audio's
        /// name is drawn the same way (green, no extra handle - the green
        /// offset handle already moves it).
        /// </summary>
        private void DrawAudioClips(SKCanvas canvas, float width, float midY,
                                    float half, float waveBottom)
        {
            using var clipPaint = new SKPaint
            {
                Color = new SKColor(235, 170, 70, CombinedPresentation ? (byte)75 : (byte)150), StrokeWidth = 1,
                IsAntialias = false,
            };
            using var handleFill = new SKPaint
            {
                Color = new SKColor(235, 170, 70), IsAntialias = true,
            };
            using var nameClip = new SKPaint
            {
                Color = new SKColor(245, 200, 120), TextSize = 11, IsAntialias = true,
            };
            using var namePrimary = new SKPaint
            {
                Color = new SKColor(140, 230, 160), TextSize = 11, IsAntialias = true,
            };

            foreach (var clip in AudioClips)
            {
                // Envelope (only when the file was found and scanned).
                if (clip.PeakMin != null && clip.Duration > 0)
                {
                    float sx = Math.Max(TimelineLeftInset, XAtTime(clip.Start));
                    float ex = Math.Min(width, XAtTime(clip.Start + clip.Duration));
                    for (int x = (int)sx; x < (int)ex; x++)
                    {
                        double t0 = TimeAtX(x) - clip.Start;
                        double t1 = TimeAtX(x + 1) - clip.Start;
                        if (t1 <= 0 || t0 >= clip.Duration) continue;

                        int i0 = Math.Clamp((int)(t0 / 0.001), 0, clip.PeakMin.Length - 1);
                        int i1 = Math.Clamp((int)(t1 / 0.001), i0, clip.PeakMin.Length - 1);
                        var (mn, mx) = PeakEnvelope.For(clip.PeakMin, clip.PeakMax).Query(i0, i1);
                        if (mn > mx) continue;
                        float yTop = midY - mx * half;
                        float yBot = midY - mn * half;
                        if (yBot - yTop < 1f) yBot = yTop + 1f;
                        canvas.DrawLine(x, yTop, x, yBot, clipPaint);
                    }
                }

                // Handle + name at the lower-left of the clip start.
                float hx = XAtTime(clip.Start);
                if (hx > -60 && hx < width + 12)
                {
                    var rect = new SKRect(hx, waveBottom - ClipHandleH - 2,
                                          hx + ClipHandleW, waveBottom - 2);
                    canvas.DrawRoundRect(rect, 2, 2, handleFill);
                    canvas.DrawText(clip.Name ?? "", hx + ClipHandleW + 3,
                                    waveBottom - 5, nameClip);
                }
            }

            // Primary audio name at ITS start.
            if (!string.IsNullOrEmpty(PrimaryAudioName))
            {
                float px = XAtTime(_audioOffset);
                if (px > -60 && px < width + 12)
                    canvas.DrawText(PrimaryAudioName, px + 3, waveBottom - 5,
                                    namePrimary);
            }
        }

        /// <summary>The clip whose handle contains the point, or null.</summary>
        public AudioClipVisual ClipHandleAt(System.Windows.Point p)
        {
            float waveBottom = (float)ActualHeight - AxisHeight;
            if (p.Y < waveBottom - ClipHandleH - 4 || p.Y > waveBottom) return null;
            foreach (var clip in AudioClips)
            {
                float hx = XAtTime(clip.Start);
                if (p.X >= hx - 2 && p.X <= hx + ClipHandleW + 2) return clip;
            }
            return null;
        }

        /// <summary>Subtly shade the pre-audio region (0 .. AudioOffset) so
        /// it is obvious that the audio has not started yet there.</summary>
        private void DrawPreRollShade(SKCanvas canvas, float top, float bottom)
        {
            if (_audioOffset <= 0) return;
            float x0 = Math.Max(0, XAtTime(0));
            float x1 = XAtTime(_audioOffset);
            if (x1 <= 0) return;

            using var shade = new SKPaint { Color = new SKColor(255, 255, 255, 10) };
            canvas.DrawRect(x0, top, x1 - x0, bottom - top, shade);

            using var edge = new SKPaint { Color = new SKColor(120, 220, 140, 120), StrokeWidth = 1 };
            canvas.DrawLine(x1, top, x1, bottom, edge);   // audio start line
        }

        /// <summary>The draggable grab handle at the audio's start position
        /// (top-left of the waveform). Dragging it shifts the audio to a new
        /// timeline offset - see the mouse handlers below.</summary>
        private void DrawOffsetHandle(SKCanvas canvas)
        {
            var r = OffsetHandleRect();
            using var fill = new SKPaint { Color = new SKColor(120, 220, 140), IsAntialias = true };
            using var grip = new SKPaint { Color = new SKColor(24, 26, 30), StrokeWidth = 1.5f, IsAntialias = true };

            canvas.DrawRoundRect(r.Left, r.Top, r.Width, r.Height, 3, 3, fill);
            // three vertical grip lines
            for (int i = 1; i <= 3; i++)
            {
                float gx = r.Left + r.Width * i / 4f;
                canvas.DrawLine(gx, r.Top + 4, gx, r.Bottom - 4, grip);
            }
        }

        /// <summary>Draw the library-selection arrows (down-pointing
        /// triangles at the top of the timeline) with a faint guide line
        /// running down the waveform, while a selection mode is active.</summary>
        private void DrawSelectionArrows(SKCanvas canvas, float height)
        {
            if (ArrowMode == SelectionMode.None) return;

            void Arrow(double t, SKColor color)
            {
                float x = XAtTime(t);
                if (x < -12 || x > ActualWidth + 12) return;

                using var fill = new SKPaint { Color = color, IsAntialias = true };
                using var path = new SKPath();
                path.MoveTo(x - 8, 1);
                path.LineTo(x + 8, 1);
                path.LineTo(x, 17);      // apex pointing DOWN
                path.Close();
                canvas.DrawPath(path, fill);

                using var guide = new SKPaint { Color = color.WithAlpha(110), StrokeWidth = 1 };
                canvas.DrawLine(x, 17, x, height - AxisHeight, guide);
            }

            if (ArrowMode == SelectionMode.Range)
            {
                Arrow(RangeStart, new SKColor(80, 210, 110));    // green = start
                Arrow(RangeEnd, new SKColor(235, 80, 80));       // red = end
            }
            else
            {
                Arrow(InsertTime, new SKColor(90, 150, 255));    // blue = insert
            }
        }

        /// <summary>Which arrow (if any) is under the point: 0 none,
        /// 1 green start, 2 red end, 3 blue insert.</summary>
        private int HitArrow(System.Windows.Point p)
        {
            if (ArrowMode == SelectionMode.None || p.Y > 20) return 0;
            bool Near(double t) => Math.Abs(XAtTime(t) - p.X) <= 9;

            if (ArrowMode == SelectionMode.Range)
            {
                // Prefer whichever arrow is closer when they overlap.
                bool g = Near(RangeStart), r = Near(RangeEnd);
                if (g && r)
                    return Math.Abs(XAtTime(RangeStart) - p.X) <=
                           Math.Abs(XAtTime(RangeEnd) - p.X) ? 1 : 2;
                if (g) return 1;
                if (r) return 2;
            }
            else if (Near(InsertTime)) return 3;
            return 0;
        }

        /// <summary>Screen rectangle of the offset handle (in DIU).</summary>
        private SKRect OffsetHandleRect()
        {
            float x = XAtTime(_audioOffset) - TimelineLeftInset;
            return new SKRect(x, 1, x + HandleWidth, 1 + OffsetHandleHeight);
        }

        /// <summary>
        /// The scalable time axis. A "nice" tick interval is chosen so that
        /// labels are at least ~70 px apart at the current zoom, then labels
        /// are formatted with just enough decimal places for that interval.
        /// </summary>
        private void DrawTimeAxis(SKCanvas canvas, float width, float height)
        {
            float axisTop = height - AxisHeight;

            using var bg = new SKPaint { Color = ThemeSk("PanelBackground", new SKColor(34, 37, 43)) };
            canvas.DrawRect(0, axisTop, width, AxisHeight, bg);

            using var tickPaint = new SKPaint { Color = ThemeSk("HeaderText", new SKColor(140, 145, 155)), StrokeWidth = 1 };
            using var gridPaint = new SKPaint { Color = ThemeSk("DividerBrush", new SKColor(46, 50, 58)), StrokeWidth = 1 };
            using var textPaint = new SKPaint
            {
                Color = ThemeSk("SecondaryText", new SKColor(190, 195, 205)),
                IsAntialias = true,
                TextSize = 11,
            };

            // Candidate intervals in seconds, from 1 ms up to 10 minutes.
            double[] nice = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                              1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
            double interval = nice[^1];
            foreach (double n in nice)
                if (n * PixelsPerSecond >= 70) { interval = n; break; }

            int decimals = interval < 0.01 ? 3 : interval < 0.1 ? 2 : interval < 1 ? 1 : 0;

            double first = Math.Floor(ViewStart / interval) * interval;
            for (double t = first; t <= ViewStart + VisibleSeconds + interval; t += interval)
            {
                if (t < 0) continue;
                float x = XAtTime(t);
                if (x < TimelineLeftInset || x > width + 50) continue;

                bool major = Math.Abs(t - Math.Round(t)) < 1e-6;
                gridPaint.Color = ThemeSk("SecondaryText", new SKColor(150, 165, 185)).WithAlpha(major ? (byte)65 : (byte)27);
                canvas.DrawLine(x, 0, x, axisTop, gridPaint);
                canvas.DrawLine(x, axisTop, x, axisTop + 6, tickPaint); // tick
                canvas.DrawText(t.ToString("F" + decimals) + "s",
                                x + 3, axisTop + 18, textPaint);
            }
        }

        /// <summary>Draw a shallow upside-down triangle for every command
        /// group. Nearby markers are staggered vertically so their hit targets
        /// do not sit directly on top of one another.</summary>
        private void DrawMarkers(SKCanvas canvas, float width)
        {
            using var normalPaint = new SKPaint
            {
                Color = new SKColor(255, 200, 60),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var normalOutline = new SKPaint
            {
                Color = new SKColor(255, 220, 100),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true,
            };
            using var normalStem = new SKPaint
            {
                Color = new SKColor(255, 200, 60, 80),
                StrokeWidth = 1,
            };
            using var collisionPaint = new SKPaint
            {
                Color = new SKColor(255, 0, 0),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var collisionOutline = new SKPaint
            {
                Color = new SKColor(255, 180, 180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true,
            };
            using var collisionStem = new SKPaint
            {
                Color = new SKColor(255, 0, 0, 150),
                StrokeWidth = 1.5f,
            };

            var collisionTimes = CollisionMarkers == null
                ? new HashSet<double>()
                : CollisionMarkers.Select(ServoCommand.TimeKey).ToHashSet();

            using var selectedOutline = new SKPaint { Color = new SKColor(70, 210, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            using var purpleFill = new SKPaint { Color = new SKColor(175, 95, 245), IsAntialias = true };
            using var groupedFill = new SKPaint { Color = new SKColor(40, 205, 160), IsAntialias = true };
            using var purpleOutline = new SKPaint { Color = new SKColor(225, 180, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };

            const float halfWidth = 6f;
            foreach (var mv in BuildMarkerVisuals(width))
            {
                bool collision = collisionTimes.Contains(ServoCommand.TimeKey(mv.Time));
                bool highlighted = HighlightedControlMarkers.Contains(mv.Time);
                if (GroupColors.TryGetValue(mv.Time, out var groupColor)) groupedFill.Color = groupColor;
                var fill = GroupColors.ContainsKey(mv.Time) ? groupedFill : highlighted ? purpleFill : collision ? collisionPaint : normalPaint;
                var outline = highlighted ? purpleOutline : _selectedMarkers.Contains(mv.Time) ? selectedOutline : collision ? collisionOutline : normalOutline;
                var stem = collision ? collisionStem : normalStem;

                using var path = new SKPath();
                path.MoveTo(mv.X - halfWidth, mv.BaseY);
                path.LineTo(mv.X + halfWidth, mv.BaseY);
                path.LineTo(mv.X, mv.TipY);
                path.Close();
                canvas.DrawPath(path, fill);
                canvas.DrawPath(path, outline);
                canvas.DrawLine(mv.X, mv.TipY, mv.X,
                                (float)ActualHeight - AxisHeight, stem);
            }
        }

        private void DrawCursor(SKCanvas canvas, float height)
        {
            float x = XAtTime(CursorTime);
            if (x < -2 || x > ActualWidth + 2) return;

            using var cursorPaint = new SKPaint
            {
                Color = new SKColor(255, 80, 80),
                StrokeWidth = 2,
            };
            canvas.DrawLine(x, 0, x, height - AxisHeight, cursorPaint);
        }

        // ==================== mouse interaction ====================

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            _panning = false;
            base.OnLostMouseCapture(e);
            if (!_markerPressPending && !_draggingMarker) return;
            // Aborted drags only discard their visual preview; document commands
            // are not changed until a normal left-button release.
            _markerPressPending = _draggingMarker = false;
            _groupDragOffset = 0;
            Cursor = Cursors.Arrow;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            var pos = e.GetPosition(this);

            // Library-selection arrows (while a mode is active) take priority
            // over everything else at the top of the view.
            int arrow = HitArrow(pos);
            if (arrow > 0)
            {
                _dragArrow = arrow;
                CaptureMouse();
                return;
            }

            // Grabbing the audio-offset handle starts a drag instead of a
            // cursor click.
            var hr = OffsetHandleRect();
            if (pos.X >= hr.Left - 3 && pos.X <= hr.Right + 3 &&
                pos.Y >= hr.Top && pos.Y <= hr.Bottom)
            {
                _draggingOffset = true;
                _offsetGrabDeltaX = pos.X - XAtTime(_audioOffset);
                CaptureMouse();
                return;
            }

            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (shift && Markers.Count > 0)
            {
                double nearest = Markers.OrderBy(m => Math.Abs(XAtTime(m) - pos.X)).First();
                SelectMarker(nearest, control, shift: true);
                TimeClicked?.Invoke(nearest);
                e.Handled = true;
                return;
            }
            if (control && TryHitMarker(pos, out double toggled))
            {
                SelectMarker(toggled, control: true, shift: false);
                TimeClicked?.Invoke(toggled);
                e.Handled = true;
                return;
            }

            if (TryEditMarker(pos, e.ClickCount))
            {
                e.Handled = true;
                return;
            }

            double px = pos.X;
            double t = Math.Clamp(TimeAtX(px), 0, Math.Max(0, Duration));

            // Grabbing an additional-audio clip handle (bottom-left of the
            // clip): drag moves the clip AND the commands riding on it.
            var clipHit = ClipHandleAt(pos);
            if (clipHit != null)
            {
                _draggingClip = clipHit;
                CaptureMouse();
                Cursor = System.Windows.Input.Cursors.SizeWE;
                return;
            }

            // Pressing directly on a staggered triangle arms a drag. Using
            // both X and Y here is what makes dense overlapping groups
            // individually selectable.
            if (pos.Y <= MarkerLane + 4 && TryHitMarker(pos, out double hitKey))
            {
                if (_selectedMarkers.Count == 0) SelectMarker(hitKey, false, false);
                if (!_selectedMarkers.Contains(hitKey))
                {
                    TimeClicked?.Invoke(hitKey);
                    e.Handled = true;
                    return;
                }
                _selectionAnchor = hitKey;
                _groupDragOffset = 0;
                _markerPressPending = true;
                _markerKey = hitKey;
                _markerPressX = px;
                CaptureMouse();
                return;   // TimeClicked deferred until release-without-drag
            }

            // Elsewhere in the waveform, snap to the nearest command marker
            // if the click is within a few pixels in X.
            double snapSeconds = MarkerSnapPixels / PixelsPerSecond;
            double best = double.MaxValue;
            double snapped = t;
            foreach (double m in Markers)
            {
                double d = Math.Abs(m - t);
                if (d < best && d <= snapSeconds) { best = d; snapped = m; }
            }

            TimeClicked?.Invoke(snapped);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);

            // During a library insert, right-click always confirms the blue
            // insertion arrow, even when the pointer happens to be over an
            // audio-clip handle.
            if (ArrowMode == SelectionMode.Insert)
            {
                RightClicked?.Invoke(TimeAtX(e.GetPosition(this).X));
                return;
            }

            if (_selectedMarkers.Count > 0)
            {
                RightClicked?.Invoke(TimeAtX(e.GetPosition(this).X));
                e.Handled = true;
                return;
            }

            if (TryHitMarker(e.GetPosition(this), out double groupedHit) && CommandGroups.Any(g => g.Contains(groupedHit)))
            {
                SetMarkerSelection(new[] { groupedHit });
                RightClicked?.Invoke(groupedHit); e.Handled = true; return;
            }

            var clipUnder = ClipHandleAt(e.GetPosition(this));
            if (clipUnder != null)
            {
                // Right-click ON a clip handle: set a numeric time offset
                // for that audio (and the commands riding on it) instead of
                // opening the normal context menu.
                ClipOffsetDialogRequested?.Invoke(clipUnder);
                return;
            }

            RightClicked?.Invoke(TimeAtX(e.GetPosition(this).X));
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
            ZoomBy(factor);
            e.Handled = true;
        }

        // Middle-button drag pans the view.
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            HideMarkerToolTip();
            if (e.ChangedButton == MouseButton.Middle)
            {
                _panning = true;
                _panStart = e.GetPosition(this);
                _panStartView = ViewStart;
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Additional-audio clip dragging.
            if (_draggingClip != null)
            {
                double t = Math.Round(Math.Clamp(
                    TimeAtX(e.GetPosition(this).X), 0, Math.Max(0, Duration)), 3);
                ClipMoveRequested?.Invoke(_draggingClip, t);
                InvalidateVisual();
                return;
            }

            // Command-marker dragging.
            if (_markerPressPending &&
                Math.Abs(e.GetPosition(this).X - _markerPressX) > MarkerDragThreshold)
            {
                _markerPressPending = false;
                _draggingMarker = true;
                Cursor = System.Windows.Input.Cursors.SizeWE;
            }
            if (_draggingMarker)
            {
                if (_selectedMarkers.Count > 0)
                {
                    double offset = (e.GetPosition(this).X - _markerPressX) / PixelsPerSecond;
                    PreviewSelectedMarkerMove(offset);
                    return;
                }
                double newKey = Math.Round(Math.Clamp(
                    TimeAtX(e.GetPosition(this).X), 0, Math.Max(0, Duration)), 3);

                // Skip positions already occupied by ANOTHER marker - two
                // groups never merge silently mid-drag.
                if (newKey != _markerKey &&
                    !Markers.Any(m => m == newKey))
                {
                    MarkerDragged?.Invoke(_markerKey, newKey);
                    _markerKey = newKey;
                }
                return;
            }

            // Dragging a library-selection arrow. The red end arrow is
            // clamped so it can never sit left of the green start arrow
            // (and vice versa).
            if (_dragArrow > 0)
            {
                double t = Math.Round(Math.Clamp(
                    TimeAtX(e.GetPosition(this).X), 0, Math.Max(0, Duration)), 3);
                switch (_dragArrow)
                {
                    case 1: MoveRangeArrow(1, t); break;
                    case 2: MoveRangeArrow(2, t); break;
                    case 3: InsertTime = t; break;
                }
                InvalidateVisual();
                return;
            }

            if (_draggingOffset)
            {
                // Drag the audio to a new start offset (snapped to the ms).
                double t = Math.Max(0, TimeAtX(e.GetPosition(this).X - _offsetGrabDeltaX));
                AudioOffset = Math.Round(t, 3);
                AudioOffsetChanged?.Invoke(AudioOffset);
                return;
            }

            if (_panning)
            {
                double dx = e.GetPosition(this).X - _panStart.X;
                SetViewStart(_panStartView - dx / PixelsPerSecond);
            }

            // Rich hover information follows the actual staggered triangle
            // hit target, so closely spaced markers expose the intended group.
            var hover = e.GetPosition(this);
            if (!_panning && e.LeftButton == MouseButtonState.Released &&
                e.RightButton == MouseButtonState.Released && hover.Y <= MarkerLane + 5)
                UpdateMarkerToolTip(hover);
            else HideMarkerToolTip();

            // Show a horizontal-resize cursor when hovering the handle.
            var hr = OffsetHandleRect();
            var p = e.GetPosition(this);
            Cursor = (p.X >= hr.Left - 3 && p.X <= hr.Right + 3 &&
                      p.Y >= hr.Top && p.Y <= hr.Bottom)
                     ? System.Windows.Input.Cursors.SizeWE
                     : System.Windows.Input.Cursors.Arrow;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Left && _draggingClip != null)
            {
                _draggingClip = null;
                ReleaseMouseCapture();
                Cursor = System.Windows.Input.Cursors.Arrow;
                ClipDragCompleted?.Invoke();
                return;
            }

            if (e.ChangedButton == MouseButton.Left &&
                (_markerPressPending || _draggingMarker))
            {
                bool wasDrag = _draggingMarker;
                _markerPressPending = false;
                _draggingMarker = false;
                ReleaseMouseCapture();
                Cursor = System.Windows.Input.Cursors.Arrow;

                if (wasDrag && _selectedMarkers.Count > 0)
                {
                    double offset = _groupDragOffset;
                    _groupDragOffset = 0;
                    if (offset != 0) SelectedMarkersMoved?.Invoke(offset);
                    InvalidateVisual();
                }
                else if (wasDrag)
                    MarkerDragCompleted?.Invoke(_markerKey);   // group moved
                else
                    TimeClicked?.Invoke(_markerKey);           // plain select
                return;
            }

            if (e.ChangedButton == MouseButton.Left && _dragArrow > 0)
            {
                _dragArrow = 0;
                ReleaseMouseCapture();
            }
            if (e.ChangedButton == MouseButton.Left && _draggingOffset)
            {
                _draggingOffset = false;
                ReleaseMouseCapture();
                AudioOffsetDragEnded?.Invoke();
            }
            if (e.ChangedButton == MouseButton.Middle && _panning)
            {
                _panning = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            SetViewStart(ViewStart);   // re-clamp + notify scrollbar on resize
        }
    }

    /// <summary>One additional audio file on the timeline, backed by its
    /// "Play" command (Start reads the command's offset, so marker drags,
    /// undo, and edits all stay in sync automatically). PeakMin/PeakMax are
    /// 1 ms envelope buckets; null when the file wasn't found.</summary>
    public class AudioClipVisual
    {
        public ServoCommand Command;
        public string Name;
        public double Duration;
        public float[] PeakMin, PeakMax;
        /// <summary>Playable full path (null when the file wasn't found) -
        /// used by the in-app sequential playback.</summary>
        public string ResolvedPath;
        public double Start => Command?.OffsetSeconds ?? 0;
    }
}
