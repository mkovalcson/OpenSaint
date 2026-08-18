// ---------------------------------------------------------------------------
// MainWindow.xaml.cs
//
// The heart of the application. Logical sections (marked with #region):
//
//   1. Fields & construction     - state, timer, event wiring
//   2. Audio loading & peaks     - decode file with NAudio, build waveform
//   3. Playback & timing         - play/pause, cursor, firing commands
//   4. Servo status grid         - "last value of each servo at time t"
//   5. Timeline interaction      - left click cursor, zoom, scrollbar
//   6. Right-click context menu  - insert/edit/delete/copy/paste/import/generate
//   7. JSON load / save / clear  - full document I/O + title bar update
//   8. Hardware stubs            - MoveServoNow(), PlayBackServoValues()
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;

namespace ServoAnimator
{
    public partial class MainWindow : Window
    {
        // ================================================================
        #region 1. Fields & construction
        // ================================================================

        /// <summary>The animation document currently being edited. All
        /// timeline operations mutate _doc.Commands directly.</summary>
        private AnimationDocument _doc = new();

        private string _jsonPath;    // path of the loaded/saved JSON (title bar)
        private string _audioPath;   // path of the loaded audio file

        /// <summary>Prevents the collapsed and expanded description editors
        /// from recursively updating one another.</summary>
        private bool _syncingDescription;
        private bool _syncingMovieDescription;

        // NAudio playback objects. _reader is also used once at load time to
        // decode the whole file into peak buckets for the waveform display.
        private AudioFileReader _reader;
        private WaveOutEvent _waveOut;

        /// <summary>Playback volume shared by primary audio and inserted Play clips.
        /// 0.0 = muted, 1.0 = full volume.</summary>
        private float _playbackVolume = 1.0f;

        /// <summary>Primary audio length in seconds (0 = none loaded).
        /// Decoupled from _reader, which now holds whichever source the
        /// output device is playing (primary OR an additional clip).</summary>
        private double _primaryDuration;

        /// <summary>The audio source the device is currently playing
        /// (null = silence). In-app playback is SEQUENTIAL: at any time the
        /// source whose span contains the cursor with the LATEST start wins
        /// (an additional clip takes over from the primary and the primary
        /// resumes mid-position when the clip ends).</summary>
        private AudioSource _activeSource;
        private object _lastDesiredKey;

        private class AudioSource
        {
            public object Key;          // identity: "primary" or the clip visual
            public string Path;
            public double Start;
            public double Duration;
            public bool IsPrimary;
            public float[] PeakMin, PeakMax;   // clip envelopes (mouth amplitude)
        }

        /// <summary>UI refresh timer while playing (~30 fps).</summary>
        private readonly DispatcherTimer _timer;

        /// <summary>Current cursor position in seconds (selection + playback).</summary>
        private double _cursorTime;

        /// <summary>During playback: the last time up to which commands have
        /// already been fired, so each command fires exactly once.</summary>
        private double _lastFiredTime;

        /// <summary>
        /// Playback state. A single WALL-CLOCK anchor drives the timeline
        /// cursor at all times while Running; the audio is started as a side
        /// effect when the cursor crosses the audio offset, and once it is
        /// actually playing, the anchor is re-synchronized to NAudio's
        /// position every tick (so long runs never drift from the audio).
        /// If the audio fails to start on a given tick, the wall clock keeps
        /// the cursor moving and the start is retried on the next tick -
        /// the cursor can never freeze at the audio boundary.
        /// </summary>
        private enum PlayMode { Stopped, Running, Paused }
        private PlayMode _mode = PlayMode.Stopped;
        private DateTime _anchorWall;     // wall-clock instant of the anchor
        private double _anchorTime;       // timeline seconds at that instant

        /// <summary>Consecutive failed attempts to start the audio during a
        /// run; after MaxAudioStartAttempts the run continues silently on
        /// the wall clock instead of thrashing the device every tick.</summary>
        private int _audioStartAttempts;
        private const int MaxAudioStartAttempts = 10;

        /// <summary>One undo step per audio-offset handle drag (the drag
        /// shifts every command's time offset along with the audio).</summary>
        private bool _offsetDragUndoPushed;

        /// <summary>One undo step per command-marker drag (moving a command
        /// group along the timeline).</summary>
        private bool _markerDragUndoPushed;

        /// <summary>One undo step per audio-clip handle drag.</summary>
        private bool _clipDragUndoPushed;

        /// <summary>Peak-envelope cache per audio path, so refreshing the
        /// clip visuals never rescans files.</summary>
        private readonly Dictionary<string, (float[] Min, float[] Max, double Dur)>
            _peakCache = new();

        /// <summary>Where the audio starts on the timeline (seconds). Set by
        /// dragging the handle at the top-left of the waveform. Commands can
        /// be placed on the timeline before this point.</summary>
        private double _audioOffset;

        /// <summary>Copy/paste buffer for command groups (deep copies).</summary>
        private readonly List<ServoCommand> _clipboard = new();

        // ----- undo / redo (Edit menu, Ctrl+Z / Ctrl+Y) -----
        // Snapshot-based: before every mutating timeline operation the whole
        // command list is deep-copied onto the undo stack. Undo swaps the
        // current list with the top snapshot (pushing the current one onto
        // the redo stack); any NEW edit clears the redo stack.
        private readonly List<List<ServoCommand>> _undoStack = new();
        private readonly List<List<ServoCommand>> _redoStack = new();
        private const int UndoLimit = 100;
        /// <summary>True from the first change of a spline point drag until
        /// the drag completes, so a whole drag is ONE undo step.</summary>
        private bool _dragUndoPushed;

        // ----- Animation Library prompt state -----
        private enum LibraryPrompt { None, CreateItem, InsertSequence }
        private LibraryPrompt _libraryPrompt = LibraryPrompt.None;
        private LibraryRangePromptWindow _libraryRangeWindow;
        private string _pendingLibraryItemPath;
        private string _pendingLibraryItemDescription;
        private bool _endingLibraryOperation;

        // ----- Movie timeline -----
        private readonly List<MovieSequenceItem> _movieItems = new();
        private string _moviePath;
        private int _movieSelectedIndex = -1;
        private bool _moviePlaybackActive;
        private int _moviePlaybackIndex = -1;
        private string _movieDescription = "";
        private string _movieCreatedDate = DateTime.Today.ToString("yyyy-MM-dd");

        private enum ActiveDocumentKind { None, Sequence, Movie }
        private ActiveDocumentKind _activeDocumentKind = ActiveDocumentKind.None;
        private RecentFilesSettings _recentFiles = new();

        // Fingerprint of the sequence as it last existed on disk. Movie
        // sequence switching compares this with the live editor state so a
        // single save/discard/cancel prompt covers every kind of edit.
        private string _savedSequenceFingerprint = "";
        private string _savedMovieFingerprint = "";
        private readonly DispatcherTimer _statusTimer;

        // Versioning starts with the first generated project that contains
        // this rule. Future project generations increment patch on the same
        // day, increment minor/reset patch on a new day, and major only on
        // explicit user request.
        private const string AppDisplayName = "Animation Editor & Player";
        private const string AppVersion = "1.8.3";

        /// <summary>Detached URDF preview used when the user presses Undock.
        /// The old View > Robot Head entry has been removed; docking is now
        /// controlled directly from the URDF view.</summary>
        private RobotHeadWindow _head;
        private UrdfConfigWindow _urdfConfigWindow;

        // Live-reload the selected Configuration folder's URDFconfig.json.
        // FileSystemWatcher can raise several events for one save, so a short
        // dispatcher debounce applies the final complete file once.
        private FileSystemWatcher _urdfConfigWatcher;
        private readonly DispatcherTimer _urdfConfigReloadTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        /// <summary>The physical servo configuration (PWM ranges, direction,
        /// speed/accel arrays). Shared with the Servo Configuration window
        /// and the grid's expanded RobotControl sub-rows. Starts from the
        /// values scraped out of ConfigureServos.cs; Load/Save in the
        /// configuration window persists it to JSON.</summary>
        private readonly ServoConfiguration _servoConfig =
            ServoConfiguration.CreateDefault();

        /// <summary>Per-child visual URDF motion extents. Direction inherits
        /// Servo Configuration unless a visual-only override is stored for that
        /// child in URDFconfig.json. Configuration auto-loads from the selected
        /// Configuration folder.</summary>
        private readonly UrdfConfiguration _urdfConfig =
            UrdfConfiguration.CreateDefault();

        /// <summary>The physical devices (Maestro, 2x Tic T249, Arduino
        /// RGB). Nothing is touched until Live Drive is first turned ON;
        /// Connect() then scans USB and reports anything missing in an
        /// error popup. Retried on later toggles while devices are absent.</summary>
        private readonly HardwareManager _hw = new();

        /// <summary>Configuration/project folder locations, normally from
        /// Paths.json beside the exe. On first run a sibling animatorConfig folder
        /// is auto-discovered before prompting; Config > Set Paths… edits.</summary>
        private FolderSettings _folders;

        /// <summary>Rows of the servo status grid, one per ServoNames value.</summary>
        private readonly ObservableCollection<ServoStateRow> _rows = new();

        // ----- spline system -----

        /// <summary>Legend entries for the spline area (one per spline-checked
        /// servo): colored square + name + show/hide checkbox.</summary>
        private readonly ObservableCollection<SplineLegendItem> _legend = new();

        /// <summary>Remembered show/hide state per servo so toggling a spline
        /// checkbox off and on keeps the legend visibility choice.</summary>
        private readonly Dictionary<ServoNames, bool> _lineVisible = new();

        /// <summary>Spline sample frequency (Hz) used at save time. Bound to
        /// the picklist at the top-right of the spline area; default 50.</summary>
        private int _splineHz = 50;
        private static readonly int[] SplineHzOptions = { 10, 20, 40, 50, 60 };

        /// <summary>Distinct line colors, indexed by (int)ServoNames value so
        /// each servo always gets the same color.</summary>
        private static readonly string[] CurvePalette =
        {
            "#FF6B6B", "#4ECDC4", "#FFD166", "#6A9BFF", "#C792EA",
            "#8BC34A", "#FF9F43", "#00BCD4", "#F06292", "#A3E635",
            "#4DB6AC", "#E57373", "#BA68C8", "#7986CB", "#AED581",
            "#FFB74D", "#4FC3F7", "#DCE775", "#90A4AE",
        };

        /// <summary>Set while the grid is being updated programmatically so
        /// slider ValueChanged events don't get mistaken for user input.</summary>
        private bool _suppressGridEvents;

        /// <summary>
        /// Grid rows manually changed at the current cursor position. These
        /// values intentionally remain staged together while the user adjusts
        /// additional rows, so "Generate commands from grid values" captures
        /// the complete staged pose. Moving to another timeline time or
        /// starting playback clears the staged overrides.
        /// </summary>
        private readonly HashSet<ServoNames> _manualGridOverrides = new();

        /// <summary>Timeline command groups whose resulting calibrated URDF
        /// pose was colliding during playback. WaveformView renders these command
        /// triangles bright red until any command edit invalidates the result.</summary>
        private readonly HashSet<double> _collisionCommandMarkers = new();
        private bool _syncingCollisionWarningToggle;

        /// <summary>Last user-selected spline-area height so hiding/re-showing
        /// the spline does not discard the audio/spline GridSplitter ratio.</summary>
        private GridLength _lastSplineTimelineHeight = new(190, GridUnitType.Pixel);

        /// <summary>Explicit embedded URDF pane height in pixels. A value <= 0
        /// means follow the normal top editor row height until the user drags
        /// the bottom-center resize handle.</summary>
        private double _embeddedUrdfHeightPixels;

        /// <summary>True while the URDF preview is hosted in its separate
        /// window. The main servo grid then occupies the full editor width and
        /// switches to its two-column section layout.</summary>
        private bool _urdfUndocked;
        private GridLength _lastDockedServoColumnWidth = new(1, GridUnitType.Star);
        private GridLength _lastDockedUrdfColumnWidth = new(1, GridUnitType.Star);
        private Rect _savedUrdfWindowBounds = Rect.Empty;
        private WindowState _savedUrdfWindowState = WindowState.Normal;

        private bool IsRunning => _mode == PlayMode.Running;
        private bool LiveDrive => LiveDriveBtn.IsChecked == true;

        /// <summary>Editable tail kept beyond the last content, so the
        /// cursor can be placed - and commands/audio inserted - AFTER every
        /// waveform ends. The tail rolls forward as content grows.</summary>
        private const double TimelineTailSeconds = 60.0;

        /// <summary>Where the CONTENT actually ends: the primary audio's
        /// end, every additional clip's end, the last command's offset, and
        /// the document duration - whichever is furthest. Playback stops
        /// here (not at the editable tail's end).</summary>
        private double ContentEnd
        {
            get
            {
                double d = _primaryDuration > 0
                    ? _audioOffset + _primaryDuration
                    : (_doc.DurationSeconds > 0 ? _doc.DurationSeconds : 60.0);
                foreach (var c in Waveform.AudioClips)
                    if (c.Duration > 0) d = Math.Max(d, c.Start + c.Duration);
                foreach (var c in _doc.Commands)
                    d = Math.Max(d, c.OffsetSeconds);
                return d;
            }
        }

        /// <summary>Timeline extent for scrolling/clicking/inserting: the
        /// content plus the editable tail.</summary>
        private double TimelineDuration => ContentEnd + TimelineTailSeconds;

        public MainWindow()
        {
            InitializeComponent();
            if (EmbeddedHeadView != null)
            {
                EmbeddedHeadView.CollisionWarningEnabledChanged += HeadView_CollisionWarningEnabledChanged;
                EmbeddedHeadView.DockToggleRequested += EmbeddedHeadView_DockToggleRequested;
                EmbeddedHeadView.VerticalResizeDeltaRequested += EmbeddedHeadView_VerticalResizeDeltaRequested;
                EmbeddedHeadView.SetDockedHostState();
            }
            UpdateThemeMenuChecks();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); if (StatusText != null) StatusText.Text = "Ready"; };

            // One row object per servo in the enum. _rows stays the master
            // collection used by all update logic; the two on-screen columns
            // are ordered views over the same row objects.
            foreach (ServoNames s in Enum.GetValues<ServoNames>())
            {
                if (s == ServoNames.Play) continue;   // export-only pseudo-servo
                _rows.Add(new ServoStateRow(s));
            }
            BuildServoGridColumns();
            RefreshGridChildren();   // populate the [+/-] RobotControl sub-rows

            // Waveform events.
            Waveform.TimeClicked += Waveform_TimeClicked;
            Waveform.RightClicked += Waveform_RightClicked;
            Waveform.ViewChanged += SyncScrollBar;
            Waveform.ViewChanged += SyncSplineView;   // keep spline zoom/scroll matched
            Waveform.AudioOffsetChanged += Waveform_AudioOffsetChanged;
            Waveform.AudioOffsetDragEnded += Waveform_AudioOffsetDragEnded;
            Waveform.MarkerDragged += Waveform_MarkerDragged;
            Waveform.MarkerDragCompleted += Waveform_MarkerDragCompleted;
            Waveform.ClipMoveRequested += Waveform_ClipMoveRequested;
            Waveform.ClipDragCompleted += Waveform_ClipDragCompleted;
            Waveform.ClipOffsetDialogRequested += Waveform_ClipOffsetDialog;
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            Waveform.MarkerToolTipProvider = MarkerSummaryAt;

            // Movie timeline events. Blocks are contiguous by construction,
            // so drag operations only change order and can never overlap.
            MovieTimeline.CursorRequested += MovieTimeline_CursorRequested;
            MovieTimeline.ReorderRequested += MovieTimeline_ReorderRequested;
            MovieTimeline.InsertRequested += MovieTimeline_InsertRequested;
            MovieTimeline.RemoveRequested += MovieTimeline_RemoveRequested;
            MovieTimeline.ViewChanged += SyncMovieScroll;
            MovieTimeline.BlockToolTipProvider = MovieBlockToolTip;
            MovieTimeline.SetItems(_movieItems);
            SetMovieDescriptionText(_movieDescription);
            RefreshMovieMetadataView();

            // Spline area: forwards zoom/pan to the waveform, clicks move the
            // cursor just like clicking the waveform.
            Spline.SyncTarget = Waveform;
            Spline.TimeClicked += Waveform_TimeClicked;
            Spline.PointValueChanged += Spline_PointValueChanged;
            Spline.PointTimeChanged += Spline_PointTimeChanged;
            Spline.PointAdded += Spline_PointAdded;
            Spline.PointDeleted += Spline_PointDeleted;
            Spline.InfoChanged += text => { if (SplineInspectorText != null) SplineInspectorText.Text = text; };
            Spline.DragCompleted += () => { _dragUndoPushed = false; RefreshAfterEdit(); };
            SplineLegend.ItemsSource = _legend;

            // ~33 ms UI tick while playing.
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += Timer_Tick;

            UpdateTitle();
            UpdateTimeText();
            _savedSequenceFingerprint = CurrentSequenceFingerprint();
            _savedMovieFingerprint = CurrentMovieFingerprint();
            UpdateDocumentStatusIndicators();

            _urdfConfigReloadTimer.Tick += (_, _) =>
            {
                _urdfConfigReloadTimer.Stop();
                TryAutoLoadUrdfConfig(showErrors: false);
                ApplyUrdfConfigurationToViews();
                _urdfConfigWindow?.ReloadFromSharedConfig();
                PushHeadPose();
            };

            Loaded += (_, _) =>
            {
                // First run: FolderSettings.Load first checks for a sibling
                // animatorConfig folder beside the ServoAnimator project and uses
                // it automatically. Only prompt when neither saved paths nor that
                // conventional folder are available.
                _folders = FolderSettings.Load();
                if (_folders == null)
                {
                    _folders = new FolderSettings();
                    new SetFoldersWindow(_folders, firstRun: true)
                    { Owner = this }.ShowDialog();
                    // Cancel: fall back to the exe folder for this session
                    // (no Paths.json written, so discovery/prompting happens again
                    // next run).
                }

                // Load recent-file history from the selected Configuration folder.
                // The File > Open Recent menu is populated before the last document
                // is automatically restored below.
                _recentFiles = RecentFilesSettings.Load(_folders.ConfigFolderOrDefault);
                RefreshOpenRecentMenu();

                // Restore the user's last screen arrangement from the selected
                // Configuration folder before loading the model/configuration data.
                LoadEditorLayout();
                EditorTimelineGrid.SizeChanged += (_, _) =>
                {
                    if (!_urdfUndocked)
                        ApplyEmbeddedUrdfHeight();
                };
                Dispatcher.BeginInvoke(new Action(() => ApplyEmbeddedUrdfHeight()),
                    System.Windows.Threading.DispatcherPriority.Loaded);

                // Auto-load physical-servo and URDF-visual configurations
                // from the selected Configuration folder.
                TryAutoLoadServoConfig();
                TryAutoLoadUrdfConfig();
                ApplyUrdfConfigurationToViews();
                ConfigureUrdfConfigWatcher();

                // Reopen the movie or standalone sequence that was active when
                // the editor was last closed. Missing files are skipped without
                // preventing the application from starting.
                bool restoredDocument = TryRestoreLastDocument();
                if (!restoredDocument)
                {
                    Waveform.ZoomToFit();
                    SyncScrollBar();
                }

                // The URDF starts docked unless EditorLayout.json restores an
                // undocked layout. Dock/Undock is controlled directly from the
                // URDF view rather than from the View menu.
                PushHeadPose();
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveEditorLayout();
            SaveLastActiveDocument();
            if (_head != null) { _head.ForceClose = true; _head.Close(); }
            _urdfConfigReloadTimer.Stop();
            _urdfConfigWatcher?.Dispose();
            _urdfConfigWatcher = null;
            DisposeAudioDevice();
            _reader?.Dispose();
            base.OnClosed(e);
        }

        /// <summary>Apply one preview update to the embedded URDF view and,
        /// when created, the detachable URDF window.</summary>
        private void ForEachHeadView(Action<RobotHeadView> action)
        {
            if (EmbeddedHeadView != null) action(EmbeddedHeadView);
            if (_head?.HeadView != null) action(_head.HeadView);
        }

        private void HeadView_CollisionWarningEnabledChanged(bool enabled)
        {
            if (_syncingCollisionWarningToggle) return;
            _syncingCollisionWarningToggle = true;
            try
            {
                // Collision Warning is an editor-wide diagnostic mode even
                // though each URDF preview owns its own button. Keep embedded
                // and detached previews synchronized.
                ForEachHeadView(v => v.SetCollisionWarningsEnabled(enabled));
                if (!enabled)
                    ClearCollisionCommandWarnings();
            }
            finally
            {
                _syncingCollisionWarningToggle = false;
            }
        }

        #endregion

        // ================================================================
        #region 2. Audio loading & peak (waveform) generation
        // ================================================================

        private void OpenAudio_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select audio file",
                Filter = "Audio files (*.mp3;*.wav;*.aiff;*.wma;*.m4a)|*.mp3;*.wav;*.aiff;*.wma;*.m4a|All files (*.*)|*.*",
                InitialDirectory = _folders?.ProjectFolderOrDefault ?? "",
            };
            if (dlg.ShowDialog() != true) return;
            LoadAudio(dlg.FileName);
        }

        /// <summary>
        /// Opens an audio file, decodes the entire stream once to build the
        /// min/max peak envelope for drawing, then rewinds it and hands it to
        /// a WaveOutEvent for playback.
        /// </summary>
        private void LoadAudio(string path)
        {
            try
            {
                StopPlayback();            // also disposes the output device
                _reader?.Dispose();

                _reader = new AudioFileReader(path);   // decodes to 32-bit float
                _audioPath = path;

                int sampleRate = _reader.WaveFormat.SampleRate;
                int channels = _reader.WaveFormat.Channels;

                // One peak bucket per millisecond of audio: fine enough to look
                // correct fully zoomed in, small enough to render instantly.
                int framesPerBucket = Math.Max(1, sampleRate / 1000);
                var mins = new List<float>();
                var maxs = new List<float>();

                float[] buffer = new float[sampleRate * channels]; // ~1 s chunks
                float mn = float.MaxValue, mx = float.MinValue;
                int framesInBucket = 0;

                int read;
                while ((read = _reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Fold all channels of a frame into one min/max envelope.
                    for (int i = 0; i < read; i += channels)
                    {
                        for (int c = 0; c < channels && i + c < read; c++)
                        {
                            float s = buffer[i + c];
                            if (s < mn) mn = s;
                            if (s > mx) mx = s;
                        }
                        if (++framesInBucket >= framesPerBucket)
                        {
                            mins.Add(mn); maxs.Add(mx);
                            mn = float.MaxValue; mx = float.MinValue;
                            framesInBucket = 0;
                        }
                    }
                }
                if (framesInBucket > 0) { mins.Add(mn); maxs.Add(mx); }

                _reader.Position = 0;   // rewind; the output device is
                                        // created on demand by SwitchAudioTo

                double duration = _reader.TotalTime.TotalSeconds;
                _primaryDuration = duration;
                Waveform.AudioOffset = _audioOffset;   // keep any existing offset
                Waveform.SetAudio(mins.ToArray(), maxs.ToArray(),
                                  (double)framesPerBucket / sampleRate, duration);

                _doc.AudioFile = Path.GetFileName(path);
                _doc.DurationSeconds = Math.Round(_audioOffset + duration, 2);

                // The audio's name is drawn at the lower-left of where it
                // starts on the waveform (replacing the old top-bar label).
                Waveform.PrimaryAudioName = Path.GetFileName(path);

                SetCursor(0);
                SyncScrollBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open audio file:\n" + ex.Message,
                                "Audio error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        // ================================================================
        #region 3. Playback & timing
        // ================================================================

        /// <summary>
        /// Transport button. The audio played is the SAME file selected with
        /// "Open Audio…" (whose waveform is showing) - _reader/_waveOut were
        /// created from it in LoadAudio(). Three states:
        ///   * Stopped: "▶ Play" — starts playback FROM THE BEGINNING of the
        ///     timeline (t = 0). If the audio has been dragged to an offset,
        ///     the pre-roll phase runs first: the cursor moves and commands
        ///     fire (PlayBackServoValues) with no audio, then the audio
        ///     starts exactly when the cursor reaches the offset.
        ///   * Playing (pre-roll or audio): "❚❚ Pause".
        ///   * Paused:  "▶ Resume" — continues from the pause point (or from
        ///     wherever the timeline was clicked while paused).
        /// </summary>
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            // The ordinary sequence transport is independent of the movie
            // transport. Pressing it takes ownership of playback.
            if (_moviePlaybackActive)
            {
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                if (MoviePlayButton != null) MoviePlayButton.Content = "▶ Movie";
            }

            bool hasAudio = _primaryDuration > 0 ||
                            Waveform.AudioClips.Any(c => c.Duration > 0);
            if (!hasAudio && TimelineDuration <= 0)
            {
                MessageBox.Show(this, "Open or insert an audio file first.",
                                "No audio", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (IsRunning)
            {
                PausePlayback();
            }
            else if (_mode == PlayMode.Paused)
            {
                StartPlaybackAt(_cursorTime);      // resume
            }
            else
            {
                SetCursor(0);                      // fresh Play: from the top
                StartPlaybackAt(0);
            }
        }

        /// <summary>
        /// Tear down the output device completely. WaveOutEvent has a race
        /// where Play() called right after Stop() can be killed by the old
        /// playback thread's shutdown - the audio silently never starts. The
        /// cure is to never reuse a stopped device: dispose it and create a
        /// fresh one per start (see SwitchAudioTo). Creation costs ~a
        /// millisecond and only happens on play/seek.
        /// </summary>
        private void DisposeAudioDevice()
        {
            if (_waveOut == null) return;
            try { _waveOut.Stop(); } catch { /* device may already be dead */ }
            _waveOut.Dispose();
            _waveOut = null;
        }

        /// <summary>
        /// (Re)start the audio at a position in AUDIO seconds (relative to
        /// the start of the file): FRESH READER + FRESH DEVICE, block-aligned
        /// seek, Play. This is the ONLY place audio output is started, so the
        /// audio is always synced to exactly (timeline cursor - audio
        /// offset), i.e. the audio begins precisely at the start of the
        /// waveform.
        ///
        /// Why a fresh reader too: WaveOutEvent.Dispose() does not join its
        /// playback thread, so a dying device's thread can still perform one
        /// last Read() on a SHARED reader after the new device has started -
        /// racing the stream position (often to end-of-file), which makes
        /// the new device stop instantly and silently. Giving every start
        /// its own AudioFileReader removes the shared state entirely.
        /// Opening a reader is cheap (header parse) and only happens on
        /// play/seek.
        /// </summary>
        /// <summary>Which audio source owns time t: among the primary and
        /// every playable additional clip whose span contains t, the one
        /// with the LATEST start wins (an additional clip beats the primary
        /// on a tie). Null = silence (pre-roll / gaps).</summary>
        private AudioSource DesiredSourceAt(double t)
        {
            AudioSource best = null;

            if (_primaryDuration > 0 && !string.IsNullOrEmpty(_audioPath) &&
                t >= _audioOffset - 1e-9 && t < _audioOffset + _primaryDuration)
                best = new AudioSource
                {
                    Key = "primary", Path = _audioPath, Start = _audioOffset,
                    Duration = _primaryDuration, IsPrimary = true,
                };

            foreach (var c in Waveform.AudioClips)
            {
                if (c.ResolvedPath == null || c.Duration <= 0) continue;
                if (t < c.Start - 1e-9 || t >= c.Start + c.Duration) continue;
                if (best == null || c.Start >= best.Start)
                    best = new AudioSource
                    {
                        Key = c, Path = c.ResolvedPath, Start = c.Start,
                        Duration = c.Duration, IsPrimary = false,
                        PeakMin = c.PeakMin, PeakMax = c.PeakMax,
                    };
            }
            return best;
        }

        private static bool SameSource(AudioSource a, AudioSource b) =>
            Equals(a?.Key, b?.Key);

        /// <summary>
        /// Switch the output device to a source (or to silence) at timeline
        /// time t: FRESH READER + FRESH DEVICE for the source's file,
        /// block-aligned seek to (t - source start), Play. The fresh-per-
        /// start rule (see DisposeAudioDevice) applies to clip switches
        /// exactly as it does to primary starts.
        /// </summary>
        private void SwitchAudioTo(AudioSource src, double t)
        {
            DisposeAudioDevice();
            _activeSource = src;
            if (src == null) return;

            var old = _reader;
            _reader = new AudioFileReader(src.Path);
            old?.Dispose();               // old device thread (if any) holds
                                          // its own reference-free stream now
            SeekAudio(t - src.Start);

            _waveOut = new WaveOutEvent { DesiredLatency = 100, Volume = _playbackVolume };
            _waveOut.Init(_reader);
            _waveOut.Play();
            Debug.WriteLine($"[audio] {(src.IsPrimary ? "primary" : src.Path)} " +
                            $"at {t - src.Start:F3}s (state={_waveOut.PlaybackState})");
        }

        /// <summary>
        /// Seek the audio to a position given in AUDIO seconds (i.e. already
        /// relative to the start of the file, not the timeline).
        ///
        /// IMPORTANT: this aligns the byte position to the stream's
        /// BlockAlign. Seeking AudioFileReader via CurrentTime can land on a
        /// byte offset in the middle of a sample frame, which makes the
        /// playback thread abort immediately after Play() (raising
        /// PlaybackStopped) - the cause of playback dying exactly at the
        /// audio-offset boundary, where the seek time is fractional.
        /// </summary>
        private void SeekAudio(double audioSeconds)
        {
            if (_reader == null) return;
            audioSeconds = Math.Max(0, audioSeconds);

            long pos = (long)(audioSeconds * _reader.WaveFormat.AverageBytesPerSecond);
            int align = Math.Max(1, _reader.WaveFormat.BlockAlign);
            pos -= pos % align;                            // frame-aligned
            _reader.Position = Math.Min(pos, _reader.Length);
        }

        /// <summary>
        /// Begin (or re-anchor) playback at a timeline position. Sets the
        /// wall-clock anchor; if the position is inside the audio region the
        /// audio is started there immediately, otherwise the audio is parked
        /// and Timer_Tick starts it when the cursor reaches the offset.
        /// </summary>
        private void StartPlaybackAt(double t)
        {
            // Playback always returns the grid to the authored timeline pose;
            // any manually staged multi-row values are intentionally discarded.
            ClearManualGridOverrides();

            // Rebuild immediately before every run so the preview evaluates
            // the latest spline control points, including edits made since
            // the last save/load.  UpdateServoGrid() also pushes that exact
            // evaluated pose into the URDF view before the first timer tick.
            RebuildSplineData();

            _cursorTime = Math.Clamp(t, 0, TimelineDuration);
            _lastFiredTime = _cursorTime;
            _anchorWall = DateTime.UtcNow;
            _anchorTime = _cursorTime;
            _audioStartAttempts = 0;   // fresh retry budget for this run
            _lastDesiredKey = null;

            // Start (or park in silence for pre-roll/gaps) on whichever
            // source owns this position; Timer_Tick keeps switching as clip
            // boundaries are crossed.
            try { SwitchAudioTo(DesiredSourceAt(_cursorTime), _cursorTime); }
            catch { /* the tick retries */ }

            UpdateServoGrid(_cursorTime);

            _mode = PlayMode.Running;
            _timer.Start();
            PlayPauseBtn.Content = "❚❚ Pause";
        }

        private void PausePlayback()
        {
            // Resume always rebuilds a fresh device at the right source and
            // position, so the paused device is simply torn down.
            DisposeAudioDevice();
            _activeSource = null;
            _timer.Stop();
            _mode = PlayMode.Paused;
            ForEachHeadView(v => v.SetMouth(0));
            PlayPauseBtn.Content = "▶ Resume";
        }

        private void StopPlayback()
        {
            _timer.Stop();
            DisposeAudioDevice();
            _activeSource = null;
            _mode = PlayMode.Stopped;
            ForEachHeadView(v => v.SetMouth(0));
            PlayPauseBtn.Content = "▶ Play";
            if (_moviePlaybackActive)
            {
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                if (MoviePlayButton != null) MoviePlayButton.Content = "▶ Movie";
            }
        }

        /// <summary>
        /// Playback heartbeat (~30 fps). The wall clock is the master:
        ///   * timeline time advances from the anchor unconditionally, so
        ///     the cursor moves smoothly through the pre-audio region and
        ///     never stalls waiting for the audio device
        ///   * when the cursor is inside the audio region and the audio is
        ///     not playing yet (first crossing, or a failed start), it is
        ///     (re)started at the matching position
        ///   * while the audio IS playing, the anchor re-syncs to NAudio's
        ///     position, making the audio the effective clock
        ///   * fires PlayBackServoValues() for every command offset crossed
        ///     since the last tick, updates the cursor/time/grid displays.
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_mode != PlayMode.Running) return;

            double t = _anchorTime + (DateTime.UtcNow - _anchorWall).TotalSeconds;

            // ---- sequential audio: whichever source (primary or an
            //      additional clip) owns t plays; crossing a clip's start
            //      switches to it, its end falls back to whatever still
            //      spans t (silence in gaps). A fresh retry budget applies
            //      per source change so a refusing device can't thrash. ----
            var desired = DesiredSourceAt(t);
            if (!Equals(desired?.Key, _lastDesiredKey))
            {
                _lastDesiredKey = desired?.Key;
                _audioStartAttempts = 0;
            }

            bool playing = _waveOut?.PlaybackState == PlaybackState.Playing;
            bool wrongSource = !SameSource(desired, _activeSource);
            if ((wrongSource || (desired != null && !playing)) &&
                _audioStartAttempts < MaxAudioStartAttempts &&
                t < ContentEnd - 0.05)
            {
                _audioStartAttempts++;
                try { SwitchAudioTo(desired, t); }
                catch { /* retry next tick */ }
                playing = _waveOut?.PlaybackState == PlaybackState.Playing;
            }

            if (_activeSource != null && playing)
            {
                // The active audio is the authoritative clock: re-anchor.
                _audioStartAttempts = 0;
                t = _activeSource.Start + _reader.CurrentTime.TotalSeconds;
                _anchorWall = DateTime.UtcNow;
                _anchorTime = t;
            }
            // else: wall clock carries through silence/gaps.

            // End of the CONTENT (last waveform/command) - the editable
            // tail beyond it is for authoring, not for playing silence.
            if (t >= ContentEnd - 0.005)
            {
                FireCommandsBetween(_lastFiredTime, ContentEnd);
                _cursorTime = ContentEnd;
                Waveform.CursorTime = _cursorTime;
                Waveform.InvalidateVisual();
                SyncSplineView();
                UpdateTimeText();
                UpdateServoGrid(_cursorTime);
                if (_moviePlaybackActive && _moviePlaybackIndex >= 0 &&
                    _moviePlaybackIndex < _movieItems.Count)
                {
                    MovieTimeline.CursorTime = MovieTimeline.StartOf(_moviePlaybackIndex) +
                                               _movieItems[_moviePlaybackIndex].DurationSeconds;
                    MovieTimeline.SelectedIndex = _moviePlaybackIndex;
                    MovieTimeline.EnsureVisible(MovieTimeline.CursorTime);
                    MovieTimeline.InvalidateVisual();
                }
                StopPlayback();
                return;
            }

            FireCommandsBetween(_lastFiredTime, t);
            _lastFiredTime = t;

            _cursorTime = t;
            if (_moviePlaybackActive && _moviePlaybackIndex >= 0 &&
                _moviePlaybackIndex < _movieItems.Count)
            {
                MovieTimeline.CursorTime = MovieTimeline.StartOf(_moviePlaybackIndex) +
                                           Math.Min(t, _movieItems[_moviePlaybackIndex].DurationSeconds);
                MovieTimeline.SelectedIndex = _moviePlaybackIndex;
                MovieTimeline.EnsureVisible(MovieTimeline.CursorTime);
                MovieTimeline.InvalidateVisual();
            }
            Waveform.CursorTime = t;
            Waveform.EnsureVisible(t);
            Waveform.InvalidateVisual();
            SyncSplineView();

            // Talking rectangle: amplitude of WHICHEVER audio is playing
            // (primary via the waveform's peaks, clips via their own),
            // 0 in pre-roll/gaps/silence.
            double amp = 0;
            if (_activeSource != null &&
                _waveOut?.PlaybackState == PlaybackState.Playing)
            {
                double at = t - _activeSource.Start;
                amp = _activeSource.IsPrimary
                    ? Waveform.AmplitudeAt(at)
                    : AmplitudeFrom(_activeSource.PeakMin,
                                    _activeSource.PeakMax, at);
            }
            ForEachHeadView(v => v.SetMouth(amp));

            UpdateTimeText();

            // This is the authoritative playback-to-preview path.  The grid
            // evaluates spline-enabled servos at the exact timeline time and
            // PushHeadPose() sends those values to every URDF joint each tick.
            // Non-spline servos continue to hold their latest command value.
            UpdateServoGrid(t);
        }

        /// <summary>
        /// Finds all commands with from &lt; offset &lt;= to, groups them by
        /// identical time offset and calls PlayBackServoValues() once per
        /// group, in chronological order. This is the real-time dispatch that
        /// a hardware layer would hook into. Works identically during
        /// pre-roll (before the audio starts) and during audio playback.
        /// </summary>
        private void FireCommandsBetween(double from, double to)
        {
            if (to <= from) return;

            var groups = _doc.Commands
                .Where(c => c.OffsetSeconds > from && c.OffsetSeconds <= to)
                .GroupBy(c => ServoCommand.TimeKey(c.OffsetSeconds))
                .OrderBy(g => g.Key);

            foreach (var g in groups)
            {
                ServoCommand[] commands = g.ToArray();
                bool affectsGeometry = commands.Any(c =>
                    !c.Disable &&
                    c.Servo != ServoNames.Play &&
                    c.Servo != ServoNames.RGBCommand);

                PlayBackServoValues(commands);

                if (affectsGeometry)
                {
                    // A red triangle means the resulting calibrated URDF pose
                    // AT THIS COMMAND TIME is in collision. This deliberately
                    // tests the command endpoint even when spline interpolation
                    // may have entered the collision slightly before the point.
                    HashSet<string> after = EvaluateUrdfCollisionPairsAt(g.Key);
                    if (after.Count > 0)
                        MarkCollisionCommand(g.Key);
                }
            }
        }

        /// <summary>Evaluate the calibrated URDF at an exact timeline time and
        /// return its active collision-pair IDs. During playback this is used
        /// at each geometry-affecting command group so the command triangle can
        /// be marked red when that command-time pose is unsafe.</summary>
        private HashSet<string> EvaluateUrdfCollisionPairsAt(double time)
        {
            double oldCursor = _cursorTime;
            _cursorTime = time;
            try
            {
                UpdateServoGrid(time);
                RobotHeadView view = _urdfUndocked
                    ? _head?.HeadView
                    : EmbeddedHeadView;
                if (view?.UrdfDriveEnabled != true || !view.CollisionWarningsEnabled)
                    view = null;

                return view == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : view.CollisionPairKeys.ToHashSet(StringComparer.Ordinal);
            }
            finally
            {
                _cursorTime = oldCursor;
            }
        }

        private void MarkCollisionCommand(double time)
        {
            double key = ServoCommand.TimeKey(time);
            if (!_collisionCommandMarkers.Add(key)) return;
            Waveform.CollisionMarkers = _collisionCommandMarkers.ToList();
            Waveform.InvalidateVisual();
        }

        private void ClearCollisionCommandWarnings()
        {
            if (_collisionCommandMarkers.Count == 0 &&
                (Waveform.CollisionMarkers?.Count ?? 0) == 0) return;

            _collisionCommandMarkers.Clear();
            Waveform.CollisionMarkers = Array.Empty<double>();
            Waveform.InvalidateVisual();
        }

        /// <summary>Show the current time offset with 3 decimal places.</summary>
        private void UpdateTimeText() => TimeText.Text = _cursorTime.ToString("F3") + " s";

        private void PlaybackVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _playbackVolume = (float)Math.Clamp(e.NewValue / 100.0, 0.0, 1.0);
            if (_waveOut != null) _waveOut.Volume = _playbackVolume;
            if (VolumeText != null) VolumeText.Text = $"{Math.Round(e.NewValue):0}%";
        }

        /// <summary>
        /// The audio-offset handle was dragged: SHIFT EVERY COMMAND's time
        /// offset by the same amount so the commands stay aligned with the
        /// audio (clamped at 0), mirror the new offset into the document
        /// (saved as "audioStartOffsetSeconds"), grow/shrink the timeline,
        /// and refresh everything that depends on time. The whole drag is a
        /// single undo step.
        /// </summary>
        private void Waveform_AudioOffsetChanged(double offset)
        {
            double delta = offset - _audioOffset;
            double old = _audioOffset;

            // Everything AT/RIGHT of the primary's old start rides along:
            // commands AND additional audio clips (their Play commands).
            // Anything left of the handle stays put - the same rule every
            // clip handle follows.
            if (Math.Abs(delta) > 1e-9 && _doc.Commands.Count > 0)
            {
                if (!_offsetDragUndoPushed) { PushUndo(); _offsetDragUndoPushed = true; }
                foreach (var c in _doc.Commands)
                    if (c.OffsetSeconds >= old - 1e-9)
                        c.OffsetSeconds = ServoCommand.TimeKey(
                            Math.Max(0, c.OffsetSeconds + delta));
            }

            _audioOffset = offset;
            _doc.AudioStartOffsetSeconds = offset;
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            SyncScrollBar();
            RefreshMarkers();          // '+' symbols follow the shifted commands
            RebuildSplineData();       // spline curves follow too
            UpdateServoGrid(_cursorTime);
            UpdateCommandsAtPointList();
        }

        /// <summary>
        /// A command marker is being dragged: move EVERY command at the old time
        /// key to the new key so the whole group travels together. The
        /// markers and spline curves refresh live under the drag; the whole
        /// drag is a single undo step. (The view already refused positions
        /// occupied by another marker, so groups never merge silently.)
        /// </summary>
        private void Waveform_MarkerDragged(double oldKey, double newKey)
        {
            if (!_markerDragUndoPushed) { PushUndo(); _markerDragUndoPushed = true; }

            foreach (var c in _doc.Commands.Where(c =>
                         ServoCommand.TimeKey(c.OffsetSeconds) == oldKey).ToList())
                c.OffsetSeconds = newKey;

            RefreshMarkers();
            RebuildSplineData();
        }

        // ================== additional audio clips ==================
        // Each additional audio file IS a "Play" command in the document;
        // the waveform's clip visuals (peaks, name, handle) are derived
        // from those commands by RefreshAudioClips().

        /// <summary>Scan (or fetch cached) peaks for an audio file.</summary>
        private (float[] Min, float[] Max, double Dur) BuildPeaks(string path)
        {
            using var r = new AudioFileReader(path);
            double dur = r.TotalTime.TotalSeconds;
            int buckets = Math.Max(1, (int)Math.Ceiling(dur / 0.001));
            var mn = new float[buckets];
            var mx = new float[buckets];
            Array.Fill(mn, float.MaxValue);
            Array.Fill(mx, float.MinValue);

            int chans = r.WaveFormat.Channels;
            double perSample = 1.0 / r.WaveFormat.SampleRate;
            var buf = new float[r.WaveFormat.SampleRate * chans];
            long frame = 0;
            int read;
            while ((read = r.Read(buf, 0, buf.Length)) > 0)
            {
                for (int i = 0; i < read; i += chans)
                {
                    float v = buf[i];
                    int b = Math.Min(buckets - 1, (int)(frame * perSample / 0.001));
                    if (v < mn[b]) mn[b] = v;
                    if (v > mx[b]) mx[b] = v;
                    frame++;
                }
            }
            for (int b = 0; b < buckets; b++)
                if (mn[b] > mx[b]) { mn[b] = 0; mx[b] = 0; }
            return (mn, mx, dur);
        }

        /// <summary>Peak amplitude (0..1) of a clip's envelope around a
        /// position in its own audio seconds (±15 ms window over 1 ms
        /// buckets - mirrors WaveformView.AmplitudeAt for the primary).</summary>
        private static double AmplitudeFrom(float[] mn, float[] mx, double secs)
        {
            if (mn == null || mx == null || mn.Length == 0 || secs < 0) return 0;
            int i0 = Math.Clamp((int)((secs - 0.015) / 0.001), 0, mn.Length - 1);
            int i1 = Math.Clamp((int)((secs + 0.015) / 0.001), i0, mn.Length - 1);
            float amp = 0;
            for (int i = i0; i <= i1; i++)
                amp = Math.Max(amp, Math.Max(Math.Abs(mx[i]), Math.Abs(mn[i])));
            return Math.Clamp(amp, 0, 1);
        }

        /// <summary>Resolve a clip's stored path: as-is, then the audio
        /// folder, then next to the project file; null when not found.</summary>
        private string ResolveAudioPath(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (File.Exists(stored)) return stored;
            string name = Path.GetFileName(stored);
            string p = Path.Combine(_folders?.ProjectFolderOrDefault ?? "", name);
            if (File.Exists(p)) return p;
            if (!string.IsNullOrEmpty(_jsonPath))
            {
                p = Path.Combine(Path.GetDirectoryName(_jsonPath) ?? "", name);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>Rebuild the waveform's clip visuals from the document's
        /// "Play" commands. Returns the names of files that couldn't be
        /// found (those clips still show a handle + name, no envelope).</summary>
        private List<string> RefreshAudioClips()
        {
            var missing = new List<string>();
            var clips = new List<AudioClipVisual>();

            foreach (var c in _doc.Commands.Where(c => c.Servo == ServoNames.Play))
            {
                float[] mn = null, mx = null;
                double dur = 0;
                string path = ResolveAudioPath(c.TextValue);
                if (path != null)
                {
                    try
                    {
                        if (!_peakCache.TryGetValue(path, out var pk))
                        {
                            pk = BuildPeaks(path);
                            _peakCache[path] = pk;
                        }
                        (mn, mx, dur) = pk;
                    }
                    catch { missing.Add(c.TextValue); }
                }
                else missing.Add(c.TextValue ?? "(empty path)");

                clips.Add(new AudioClipVisual
                {
                    Command = c,
                    Name = Path.GetFileName(c.TextValue ?? ""),
                    PeakMin = mn,
                    PeakMax = mx,
                    Duration = dur,
                    ResolvedPath = path,
                });
            }

            Waveform.AudioClips = clips;
            Waveform.InvalidateVisual();
            return missing;
        }

        /// <summary>Move a clip to a new start: EVERYTHING at/right of the
        /// clip's OLD start rides along - commands, the other clips (their
        /// Play commands), and the primary waveform when it starts at/after
        /// that point. Anything to the left stays put. Same rule as the
        /// primary's green handle; one undo step per drag / dialog apply.</summary>
        private void Waveform_ClipMoveRequested(AudioClipVisual clip, double newStart)
        {
            newStart = Math.Max(0, ServoCommand.TimeKey(newStart));
            double old = clip.Command.OffsetSeconds;
            double delta = newStart - old;
            if (Math.Abs(delta) < 1e-9) return;

            if (!_clipDragUndoPushed) { PushUndo(); _clipDragUndoPushed = true; }

            foreach (var c in _doc.Commands)
            {
                if (c == clip.Command) continue;
                if (c.OffsetSeconds >= old - 1e-9)
                    c.OffsetSeconds = Math.Max(0,
                        ServoCommand.TimeKey(c.OffsetSeconds + delta));
            }
            clip.Command.OffsetSeconds = newStart;

            // The primary waveform rides too when it sits at/right of the
            // moved handle.
            if (_primaryDuration > 0 && _audioOffset >= old - 1e-9)
            {
                _audioOffset = Math.Max(0,
                    ServoCommand.TimeKey(_audioOffset + delta));
                _doc.AudioStartOffsetSeconds = _audioOffset;
                Waveform.AudioOffset = _audioOffset;
            }

            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            SyncScrollBar();
            RefreshMarkers();
            RebuildSplineData();
            Waveform.InvalidateVisual();
            SyncSplineView();
        }

        private void Waveform_ClipDragCompleted()
        {
            _clipDragUndoPushed = false;
            RefreshAfterEdit();
        }

        /// <summary>Right-click on a clip handle: numeric time-offset entry
        /// for that audio (commands on it move by the same delta).</summary>
        private void Waveform_ClipOffsetDialog(AudioClipVisual clip)
        {
            double? t = PromptForTime(
                $"Time offset for {clip.Name}", clip.Command.OffsetSeconds);
            if (!t.HasValue) return;
            Waveform_ClipMoveRequested(clip, t.Value);
            _clipDragUndoPushed = false;
            RefreshAfterEdit();
        }

        /// <summary>Tiny modal numeric prompt (seconds, 3 decimals).</summary>
        private double? PromptForTime(string title, double current)
        {
            var box = new TextBox { Text = current.ToString("F3"), Margin = new Thickness(0, 6, 0, 10) };
            var ok = new Button { Content = "OK", Width = 70, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "Cancel", Width = 70, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = "Start time (seconds):", Foreground = System.Windows.Media.Brushes.LightGray });
            panel.Children.Add(box);
            panel.Children.Add(buttons);
            var win = new Window
            {
                Title = title,
                Content = panel,
                Width = 280,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (System.Windows.Media.Brush)new System.Windows.Media
                    .BrushConverter().ConvertFromString("#23262C"),
                ResizeMode = ResizeMode.NoResize,
            };
            ok.Click += (_, _) => { win.DialogResult = true; };
            box.Focus();
            box.SelectAll();
            if (win.ShowDialog() != true) return null;
            return double.TryParse(box.Text, out double v) && v >= 0
                   ? ServoCommand.TimeKey(v) : null;
        }

        /// <summary>Insert an additional audio file at the cursor: creates
        /// a "Play" command (value = full path) whose clip then renders
        /// with peaks, name, and a drag handle.</summary>
        private void InsertAudioFileAtCursor()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Insert audio file on the timeline",
                Filter = "Audio files (*.mp3;*.wav;*.aiff;*.wma;*.m4a)|*.mp3;*.wav;*.aiff;*.wma;*.m4a|All files (*.*)|*.*",
                InitialDirectory = _folders?.ProjectFolderOrDefault ?? "",
            };
            if (dlg.ShowDialog() != true) return;

            PushUndo();
            _doc.Commands.Add(new ServoCommand
            {
                OffsetSeconds = ServoCommand.TimeKey(_cursorTime),
                Servo = ServoNames.Play,
                TextValue = dlg.FileName,
                Speed = ServoSpeed.NoChange,
                Reason = Path.GetFileName(dlg.FileName),
            });
            RefreshAfterEdit();
        }

        /// <summary>Marker drag released: close the undo step, move the
        /// cursor to the group's new time (so the grid and the commands
        /// list show it) and do a full refresh.</summary>
        private void Waveform_MarkerDragCompleted(double finalKey)
        {
            _markerDragUndoPushed = false;
            SetCursor(finalKey);
            RefreshAfterEdit();
        }

        /// <summary>The offset handle was released: close the undo step and
        /// do a full refresh.</summary>
        private void Waveform_AudioOffsetDragEnded()
        {
            _offsetDragUndoPushed = false;
            RefreshAfterEdit();
        }

        #endregion

        // ================================================================
        #region 4. Servo status grid ("last state of each servo at time t")
        // ================================================================

        /// <summary>
        /// Lays out every servo group in a single left-side column beside the
        /// embedded Robot Head. Group order is:
        /// Eye Flaps, Nose, Eyes, Neck, Lighting & Vents, Eye Pop,
        /// Headtop Controls. Headtop Controls contains MFR, Whip Antenna and
        /// Microphone and starts collapsed; thin separators distinguish those
        /// subgroups.
        /// </summary>
        private void BuildServoGridColumns()
        {
            ServoStateRow R(ServoNames servo) => _rows.First(r => r.Servo == servo);

            foreach (var r in _rows)
            {
                r.DividerBelow = false;
                r.SubDividerBelow = false;
                r.GroupName = "";
                r.GroupHeader = "";
                r.GroupCollapsed = false;
            }

            void Group(string name, params ServoNames[] servos)
            {
                for (int i = 0; i < servos.Length; i++)
                {
                    var row = R(servos[i]);
                    row.GroupName = name;
                    row.GroupHeader = i == 0 ? name : "";
                    if (i == servos.Length - 1) row.DividerBelow = true;
                }
            }

            // Single-column section order beside the embedded Robot Head:
            // Eye Flaps, Nose, Eyes, Neck, Lighting & Vents, Eye Pop,
            // Headtop Controls.
            Group("Eye Flaps", ServoNames.FlapsOpen, ServoNames.FlapTiltUp);
            Group("Eyes", ServoNames.IrisClose, ServoNames.EyesVerticalUp, ServoNames.EyesHorizontalRight);
            Group("Nose", ServoNames.NoseBasket, ServoNames.NoseBody);
            Group("Neck", ServoNames.NeckTurn, ServoNames.NeckNodUp, ServoNames.NeckTiltRight);
            Group("Eye Pop", ServoNames.BothEyePop, ServoNames.LeftEyePop, ServoNames.RightEyePop);
            Group("Lighting & Vents", ServoNames.RGBCommand, ServoNames.VentsOpen);
            Group("Headtop Controls",
                  ServoNames.MFR_UpDown, ServoNames.MFR_Rotate,
                  ServoNames.Whip_Antenna_RaiseLower, ServoNames.Whip_Antenna_Rotate,
                  ServoNames.Microphone_RaiseLower);

            // Thin subgroup separators inside the single Headtop Controls group.
            R(ServoNames.MFR_Rotate).SubDividerBelow = true;
            R(ServoNames.Whip_Antenna_Rotate).SubDividerBelow = true;

            // Headtop Controls starts collapsed on every application launch.
            foreach (var row in _rows.Where(r => r.GroupName == "Headtop Controls"))
                row.GroupCollapsed = true;

            var eyeFlapsRows = new[] { ServoNames.FlapsOpen, ServoNames.FlapTiltUp }.Select(R).ToList();
            var noseRows = new[] { ServoNames.NoseBasket, ServoNames.NoseBody }.Select(R).ToList();
            var eyesRows = new[] { ServoNames.IrisClose, ServoNames.EyesVerticalUp, ServoNames.EyesHorizontalRight }.Select(R).ToList();
            var neckRows = new[] { ServoNames.NeckTurn, ServoNames.NeckNodUp, ServoNames.NeckTiltRight }.Select(R).ToList();
            var lightingVentsRows = new[] { ServoNames.RGBCommand, ServoNames.VentsOpen }.Select(R).ToList();
            var eyePopRows = new[] { ServoNames.BothEyePop, ServoNames.LeftEyePop, ServoNames.RightEyePop }.Select(R).ToList();
            var headtopRows = new[]
            {
                ServoNames.MFR_UpDown, ServoNames.MFR_Rotate,
                ServoNames.Whip_Antenna_RaiseLower, ServoNames.Whip_Antenna_Rotate,
                ServoNames.Microphone_RaiseLower,
            }.Select(R).ToList();

            // Docked single-column view.
            ServoGridEyeFlaps.ItemsSource = eyeFlapsRows;
            ServoGridNose.ItemsSource = noseRows;
            ServoGridEyes.ItemsSource = eyesRows;
            ServoGridNeck.ItemsSource = neckRows;
            ServoGridLightingVents.ItemsSource = lightingVentsRows;
            ServoGridEyePop.ItemsSource = eyePopRows;
            ServoGridHeadtop.ItemsSource = headtopRows;

            // Undocked two-column view uses the exact same row objects so edits,
            // group collapse state and live values stay synchronized. The right
            // column begins with Lighting & Vents as requested.
            UndockedServoGridEyeFlaps.ItemsSource = eyeFlapsRows;
            UndockedServoGridNose.ItemsSource = noseRows;
            UndockedServoGridEyes.ItemsSource = eyesRows;
            UndockedServoGridNeck.ItemsSource = neckRows;
            UndockedServoGridLightingVents.ItemsSource = lightingVentsRows;
            UndockedServoGridEyePop.ItemsSource = eyePopRows;
            UndockedServoGridHeadtop.ItemsSource = headtopRows;
        }

        private void ServoGroupToggle_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ServoStateRow row ||
                string.IsNullOrWhiteSpace(row.GroupName)) return;
            bool collapse = !_rows.Where(r => r.GroupName == row.GroupName).All(r => r.GroupCollapsed);
            foreach (var r in _rows.Where(r => r.GroupName == row.GroupName))
                r.GroupCollapsed = collapse;
            ShowStatus($"{row.GroupName} group {(collapse ? "collapsed" : "expanded")}");
        }

        /// <summary>Recovery Dock button shown under Headtop Controls while the
        /// URDF preview is undocked. Uses the same docking path as the detached
        /// URDF window so all window/layout state stays synchronized.</summary>
        private void DockUrdfFromGrid_Click(object sender, RoutedEventArgs e)
        {
            if (_urdfUndocked)
                SetUrdfUndocked(false);
        }

        /// <summary>
        /// Recomputes every grid row so it shows the most recent command for
        /// that servo at or before time <paramref name="t"/>. Servos with no
        /// command yet show Value 0 / Speed Default / Offset "—".
        /// Called on every left click, every playback tick, and after edits.
        /// </summary>
        private void UpdateServoGrid(double t)
        {
            // The editor grid and URDF preview always represent the timeline
            // state at the current cursor, regardless of Live Drive. Live Drive
            // only gates whether user/playback movements are also sent to the
            // physical robot.
            _suppressGridEvents = true;   // slider updates below are not user input
            try
            {
                foreach (var row in _rows)
                {
                    // Never clobber a row the user is actively editing or
                    // one that has been manually staged at this cursor time.
                    // Staged values remain together until another time is
                    // selected, playback begins, or they are generated into
                    // timeline commands.
                    if (row.IsEditing || _manualGridOverrides.Contains(row.Servo)) continue;

                    ServoCommand last = null;
                    ServoCommand lastSpeed = null;
                    foreach (var c in _doc.Commands)
                    {
                        if (c.Servo != row.Servo) continue;
                        if (c.Control.HasValue) continue;   // individual-control
                                                            // commands don't drive
                                                            // the ganged row
                        if (c.Disable) continue;            // Disable commands turn
                                                            // servos off, not move them
                        if (c.OffsetSeconds > t + 1e-9) continue;
                        if (last == null || c.OffsetSeconds >= last.OffsetSeconds)
                            last = c;

                        // N/C position commands deliberately leave Maestro
                        // speed/acceleration unchanged.  The grid therefore
                        // shows the most recent EXPLICIT speed profile, not
                        // merely the speed field of the last position command.
                        if (c.Speed != ServoSpeed.NoChange &&
                            (lastSpeed == null || c.OffsetSeconds >= lastSpeed.OffsetSeconds))
                            lastSpeed = c;
                    }

                    // The grid represents the active speed state. Before any
                    // explicit speed command, Maestro channels start in the
                    // configured Default profile.
                    row.Speed = lastSpeed?.Speed ?? ServoSpeed.Default;

                    if (last == null)
                    {
                        row.Offset = null;
                        row.Value = row.Min <= 0 ? 0 : row.Min;   // default 0 (or range floor)
                        row.TextValue = "";
                        row.ColorHex = "";
                    }
                    else
                    {
                        row.Offset = last.OffsetSeconds;
                        if (row.IsTextRow)
                        {
                            row.TextValue = last.TextValue;        // last command text used
                            row.ColorHex = last.ColorHex;          // palette color box
                        }
                        else
                            row.Value = last.NumericValue;
                    }

                    // SPLINE-checked servos: instead of holding the last
                    // command's step value, show the Cubic-Hermite
                    // INTERPOLATED value of the curve at the current time -
                    // so during playback (and when clicking the timeline) the
                    // grid tracks the smooth motion the hardware will follow.
                    // Outside the curve's range the ends hold (Eval clamps),
                    // and servos with fewer than 2 points fall back to the
                    // normal last-command behavior above.
                    if (row.SplineEnabled && !row.IsTextRow)
                    {
                        var curve = Spline?.Curves?.FirstOrDefault(
                            cv => cv.Servo == row.Servo);
                        if (curve != null && curve.T.Length >= 2)
                            row.Value = Math.Clamp(
                                SplineUtil.Eval(curve.T, curve.V, curve.M, t),
                                curve.Min, curve.Max);
                    }
                }
            }
            finally { _suppressGridEvents = false; }

            // The head preview mirrors the grid: same values, including
            // spline interpolation, so moving the timeline cursor (or
            // playback) animates the head.
            PushHeadPose();
        }

        /// <summary>The Servo Configuration was saved or loaded: refresh
        /// everything that uses it - grid sub-rows (ranges), the hardware
        /// layer (rebuilt servo objects), and gang-relative directions,
        /// which take effect immediately since they're looked up at drive
        /// time.</summary>
        private void OnServoConfigChanged()
        {
            RefreshGridChildren();
            if (_hw.Connected)
                _hw.Reconfigure(_servoConfig);

            // URDF child direction inherits Servo Configuration unless that
            // row has a visual-only override. Refresh any open calibration
            // window and the current preview pose immediately.
            _urdfConfigWindow?.RefreshInheritedDirections();
            ApplyUrdfConfigurationToViews();
            PushHeadPose();
        }

        /// <summary>Rebuild the RobotControl sub-rows under every ServoName
        /// from the gang map + the current servo configuration. Only GANGED
        /// ServoNames (more than one control) get sub-rows and the [+/-]
        /// expander - single servos are not ganged.</summary>
        private void RefreshGridChildren()
        {
            foreach (var row in _rows)
            {
                row.Children.Clear();
                var controls = ServoConfiguration.ControlsFor(row.Servo);
                if (controls.Length > 1)
                {
                    foreach (var control in controls)
                    {
                        var entry = _servoConfig.Get(control);
                        if (entry == null) continue;
                        row.Children.Add(new RobotControlRow(entry, row.Servo)
                        {
                            SliderEnabled = true,
                        });
                    }
                }
                row.RaiseHasChildren();
                if (row.Children.Count == 0) row.IsExpanded = false;
            }
        }

        /// <summary>The grid Speed picklist changed. The selection is always
        /// editable; when Live Drive is on, push the speed/accel pair to the
        /// physical servo gang.</summary>
        private void RowSpeed_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGridEvents) return;
            if ((sender as FrameworkElement)?.DataContext is not ServoStateRow row) return;
            if (!row.ShowSpeed) return;

            _manualGridOverrides.Add(row.Servo);

            if (LiveDrive && _hw.Connected)
                _hw.ConfigureGangSpeed(row.Servo, row.Speed);
            Debug.WriteLine($"ConfigureGangSpeed(servo={row.Servo}, speed={row.Speed})");
        }

        /// <summary>"Disable All": disable PWM on every Maestro servo
        /// channel so the servos go limp (safety / rest). Works whenever
        /// hardware is connected, regardless of the Live Drive state.</summary>
        private void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            if (_hw.Connected)
                _hw.DisableAll();
            else
                Debug.WriteLine("DisableAll: hardware not connected");
        }

        /// <summary>Reset the physical robot and the editor preview to their
        /// home state. This is an explicit hardware command and therefore works
        /// regardless of the Live Drive toggle once hardware is connected.
        /// Maestro servos use their configured Default PWM values, both Eye Pops
        /// move to 0, and the Arduino receives ClearAll.</summary>
        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (!_hw.AllConnected)
            {
                // Reset is itself an explicit request to move hardware, so allow
                // it to establish the connection even if Live Drive is currently off.
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    var problems = _hw.Connect(_servoConfig,
                        _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory);
                    if (problems.Count > 0)
                        Debug.WriteLine("[hardware reset] " + string.Join(" | ", problems));
                    UpdateHardwareStatusIndicators();
                }
                finally { Mouse.OverrideCursor = null; }
            }

            if (_hw.Connected)
                _hw.ResetAll();
            else
                Debug.WriteLine("ResetAll: hardware not connected");

            // Mirror the reset in the editable grid/URDF without creating
            // timeline commands. Treat the rows as manually staged values so
            // they remain visible until the cursor moves or playback starts.
            _suppressGridEvents = true;
            try
            {
                _manualGridOverrides.Clear();
                foreach (var row in _rows)
                {
                    row.Offset = null;
                    if (row.IsTextRow)
                    {
                        row.TextValue = "ClearAll";
                        row.ColorHex = "#000000";
                    }
                    else
                    {
                        row.Value = 0;
                        foreach (var child in row.Children)
                            child.Value = 0;
                    }
                    _manualGridOverrides.Add(row.Servo);
                }
            }
            finally { _suppressGridEvents = false; }

            PushHeadPose();
            ShowStatus(_hw.Connected
                ? "Robot reset to servo defaults; Eye Pop = 0; Arduino ClearAll"
                : "Reset preview applied; hardware not connected");
        }

        /// <summary>A RobotControl sub-row slider moved: always update the
        /// corresponding URDF part; when Live Drive is on, also drive that
        /// single physical servo.</summary>
        private void ChildSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressGridEvents) return;
            if ((sender as FrameworkElement)?.DataContext is not RobotControlRow row) return;

            int value = (int)Math.Round(e.NewValue);
            ForEachHeadView(v => v.SetChildServo(row.Parent, row.Control, value));   // preview always

            bool centered = ServoCommand.RangeFor(row.Parent).Min < 0;
            if (LiveDrive && _hw.Connected)
                _hw.DriveControlValue(row.Parent, row.Control,
                                      ServoSpeed.NoChange, value, centered);
            Debug.WriteLine($"ChildDrive(gang={row.Parent}, control={row.Control}, value={value})");
        }

        /// <summary>Expand ganged commands into per-control child commands
        /// (see the Export comment). Returns a new list.</summary>
        private List<ServoCommand> ExpandGangedCommands(List<ServoCommand> commands)
        {
            var result = new List<ServoCommand>();
            foreach (var c in commands)
            {
                bool ganged = !c.IsTextServo && !c.Control.HasValue &&
                              c.Servo != ServoNames.Play;
                var controls = ganged ? ServoConfiguration.ControlsFor(c.Servo)
                                      : Array.Empty<RobotControls>();
                if (!ganged || controls.Length <= 1)
                {
                    result.Add(c);
                    continue;
                }

                if (c.Servo == ServoNames.BothEyePop)
                {
                    // The children exist as ServoNames of their own.
                    foreach (var name in new[] { ServoNames.LeftEyePop,
                                                 ServoNames.RightEyePop })
                    {
                        var child = c.Clone();
                        child.Servo = name;
                        result.Add(child);
                    }
                    continue;
                }

                var (min, _) = ServoCommand.RangeFor(c.Servo);
                bool centered = min < 0;
                foreach (var control in controls)
                {
                    var child = c.Clone();
                    child.Control = control;
                    // Gang reversal negates CENTERED values (matching
                    // MapDeltatoServo's isGangReversed); 0..100 values pass
                    // through - the servo's own hardware Reverse handles
                    // that span's direction. Disable commands keep their
                    // "Disable" value on every child.
                    if (!c.Disable && centered &&
                        _servoConfig.GangReversed(c.Servo, control))
                        child.NumericValue = -c.NumericValue;
                    result.Add(child);
                }
            }
            return result;
        }

        /// <summary>Config > Servo Configuration…: modal editor over the
        /// shared configuration; its verify sliders drive the servos.</summary>
        private void ServoConfig_Click(object sender, RoutedEventArgs e)
        {
            var win = new ServoConfigWindow(_servoConfig, MoveRobotControlNow,
                (servo, value) => MoveServoNow(ServoSpeed.NoChange, servo, value),
                OnServoConfigChanged,
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory)
            { Owner = this };
            win.ShowDialog();
        }

        /// <summary>Config > URDF Configuration…: calibrate visual travel per
        /// physical child servo. Direction begins from Servo Configuration but
        /// may be overridden for URDF visuals. The window is deliberately
        /// modeless so the embedded/detached 3-D view can still be orbited,
        /// zoomed and otherwise inspected while making adjustments.</summary>
        private void UrdfConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_urdfConfigWindow != null)
            {
                if (_urdfConfigWindow.WindowState == WindowState.Minimized)
                    _urdfConfigWindow.WindowState = WindowState.Normal;
                _urdfConfigWindow.Activate();
                return;
            }

            ApplyUrdfConfigurationToViews();
            var win = new UrdfConfigWindow(_urdfConfig, _servoConfig,
                (servo, value) =>
                {
                    ApplyUrdfConfigurationToViews();
                    // Normal URDF calibration rows preview the complete logical gang.
                    ForEachHeadView(h => h.SetServo(servo, value));
                },
                (servo, control, value) =>
                {
                    ApplyUrdfConfigurationToViews();
                    // Sub-gang flap calibration previews only the physical
                    // children owned by the selected Upper or Lower row.
                    ForEachHeadView(h => h.SetChildServo(servo, control, value));
                },
                () =>
                {
                    // The upper flap hinges are children of NoseBody -> NoseBasket.
                    // Use the CURRENT editor-grid nose pose (including manually
                    // staged values) and the active URDF calibration/directions
                    // to calculate the mirrored flap zero extents that cancel
                    // that parent pitch and leave all flap panels horizontal.
                    double noseBodyValue = _rows.First(r => r.Servo == ServoNames.NoseBody).Value;
                    double noseBasketValue = _rows.First(r => r.Servo == ServoNames.NoseBasket).Value;
                    double noseBodyAngle = _urdfConfig.Map(ServoNames.NoseBody, RobotControls.NoseBody,
                                                           noseBodyValue, _servoConfig);
                    double noseBasketAngle = _urdfConfig.Map(ServoNames.NoseBasket, RobotControls.NoseBasket,
                                                             noseBasketValue, _servoConfig);
                    _urdfConfig.SetHorizontalFlapZeroes(noseBodyAngle + noseBasketAngle);

                    ApplyUrdfConfigurationToViews();
                    PushHeadPose();

                    // Show the newly defined logical-zero pose immediately, even
                    // if the editor grid currently has non-zero flap values.
                    ForEachHeadView(h =>
                    {
                        h.SetChildServo(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, 0);
                        h.SetChildServo(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, 0);
                        h.SetChildServo(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, 0);
                        h.SetChildServo(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, 0);
                        h.SetChildServo(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, 0);
                        h.SetChildServo(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, 0);
                    });
                },
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                () =>
                {
                    // Save Default is authoritative: reload the just-written
                    // file into the shared configuration object, then refresh
                    // both active Robot Head previews immediately.
                    TryAutoLoadUrdfConfig();
                    ApplyUrdfConfigurationToViews();
                    ConfigureUrdfConfigWatcher();
                    _urdfConfigWindow?.ReloadFromSharedConfig();
                    PushHeadPose();
                })
            { Owner = this };

            _urdfConfigWindow = win;
            win.Closed += (_, _) =>
            {
                _urdfConfigWindow = null;
                // Calibration test sliders temporarily override the displayed pose.
                // Restore the editor/timeline pose after the modeless window closes.
                ApplyUrdfConfigurationToViews();
                PushHeadPose();
            };
            win.Show();
        }

        private void ApplyUrdfConfigurationToViews()
        {
            // Collision results depend on the calibrated URDF extents/zeroes
            // and inherited servo directions. A calibration change invalidates
            // any command-time collision markers just like a command edit does.
            ClearCollisionCommandWarnings();
            ForEachHeadView(h =>
            {
                h.SetUrdfConfiguration(_urdfConfig);
                h.SetServoConfiguration(_servoConfig);
            });
        }

        /// <summary>Send the head-mapped servo values (as currently shown
        /// in the grid, spline-interpolated where applicable) to the robot
        /// head preview.
        ///
        /// NeckNodUp and NeckTiltRight use the SAME physical servos, so they
        /// are exclusive: whichever received the MOST RECENT command (by
        /// offset, at/before the cursor) is active and the other behaves as
        /// 0 until its own next command. On identical offsets NeckTiltRight
        /// takes precedence.</summary>
        private void PushHeadPose()
        {
            if (EmbeddedHeadView == null && _head?.HeadView == null) return;
            ServoStateRow Row(ServoNames s) => _rows.First(r => r.Servo == s);
            double RV(ServoNames s) => Row(s).Value;

            var nodRow = Row(ServoNames.NeckNodUp);
            var tiltRow = Row(ServoNames.NeckTiltRight);
            double nod = nodRow.Value, tilt = tiltRow.Value;
            double? no = nodRow.Offset, to = tiltRow.Offset;

            if (no.HasValue && (!to.HasValue || no.Value > to.Value))
                tilt = 0;                        // nod command is newer
            else if (to.HasValue)
                nod = 0;                         // tilt newer (or tie -> tilt)
            // neither servo has a command yet: both stay at their defaults (0)

            // NoseBasket is a positive 0..100 servo. Its default/neutral
            // authoring value is 0, matching both the grid and hardware
            // configuration default position.
            var nbRow = Row(ServoNames.NoseBasket);
            double noseBasketVal = nbRow.Value;

            // Eye-pop commands can be authored individually or with the
            // BothEyePop gang. For each side, whichever command is newer at
            // the cursor owns that eye (ties go to the ganged command).
            var bothPop = Row(ServoNames.BothEyePop);
            var leftPop = Row(ServoNames.LeftEyePop);
            var rightPop = Row(ServoNames.RightEyePop);
            double EyePopValue(ServoStateRow individual)
            {
                if (!bothPop.Offset.HasValue) return individual.Value;
                if (!individual.Offset.HasValue || bothPop.Offset.Value >= individual.Offset.Value)
                    return bothPop.Value;
                return individual.Value;
            }

            // ---- per-side values (SCREEN sides), with the robot-POV
            //      mirror: the robot faces the viewer, so its LEFT controls
            //      drive the SCREEN-RIGHT parts and vice versa. Each part's
            //      value follows the gang/individual precedence: the gang
            //      row's (spline-interpolated) value unless an individual
            //      command for that specific control is NEWER at the cursor
            //      (ties -> the gang). This precedence is identical whether
            //      Live Drive is on or off; Live Drive only gates hardware.
            double Part(ServoNames gang, RobotControls control)
            {
                var row = Row(gang);

                // While manually staging multiple grid values, the visible
                // ganged row is the user's intended pose and must not be
                // displaced by an older individual-child command.
                if (_manualGridOverrides.Contains(gang)) return row.Value;

                ServoCommand last = null;
                foreach (var c in _doc.Commands)
                    if (c.Servo == gang && c.Control == control && !c.Disable &&
                        c.OffsetSeconds <= _cursorTime + 1e-9 &&
                        (last == null || c.OffsetSeconds >= last.OffsetSeconds))
                        last = c;

                if (last == null) return row.Value;
                if (row.Offset.HasValue && row.Offset.Value >= last.OffsetSeconds)
                    return row.Value;              // gang command is newer
                return last.NumericValue;          // child owns this servo
            }

            void ApplyPose(RobotHeadView headView) => headView.SetPose(
                eyeHLeft: Part(ServoNames.EyesHorizontalRight, RobotControls.RightLensHorizontal),
                eyeHRight: Part(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal),
                eyeVLeft: Part(ServoNames.EyesVerticalUp, RobotControls.RightLensVertical),
                eyeVRight: Part(ServoNames.EyesVerticalUp, RobotControls.LeftLensVertical),
                irisLeft: Part(ServoNames.IrisClose, RobotControls.RightIris),
                irisRight: Part(ServoNames.IrisClose, RobotControls.LeftIris),
                topFlapLeft: Part(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen),
                topFlapRight: Part(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen),
                bottomFlapLeft: Part(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen),
                bottomFlapRight: Part(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen),
                tiltLeft: Part(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt),
                tiltRight: Part(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt),
                ventsLeft: Part(ServoNames.VentsOpen, RobotControls.RightEyeVent),
                ventsRight: Part(ServoNames.VentsOpen, RobotControls.LeftEyeVent),
                neckTilt: tilt,
                neckNod: nod,
                neckTurn: RV(ServoNames.NeckTurn),
                whip: RV(ServoNames.Whip_Antenna_RaiseLower),
                mic: RV(ServoNames.Microphone_RaiseLower),
                mfr: RV(ServoNames.MFR_UpDown),
                noseBody: RV(ServoNames.NoseBody),
                noseBasket: noseBasketVal,
                eyeColorHex: _rows.First(r => r.Servo == ServoNames.RGBCommand).ColorHex,
                leftEyePop: EyePopValue(leftPop),
                rightEyePop: EyePopValue(rightPop),
                whipRotate: RV(ServoNames.Whip_Antenna_Rotate),
                mfrRotate: RV(ServoNames.MFR_Rotate));

            if (EmbeddedHeadView != null) ApplyPose(EmbeddedHeadView);
            if (_head?.HeadView != null) ApplyPose(_head.HeadView);
        }

        private void LiveDrive_Changed(object sender, RoutedEventArgs e)
        {
            if (LiveDriveState != null)
                LiveDriveState.Text = LiveDrive ? "On" : "Off";
            if (LiveDriveBtn != null)
            {
                LiveDriveBtn.Background = new System.Windows.Media.SolidColorBrush(
                    LiveDrive
                        ? System.Windows.Media.Color.FromRgb(36, 83, 58)
                        : ThemeManager.GetColor("ControlBackground", System.Windows.Media.Color.FromRgb(48, 53, 61)));
                LiveDriveBtn.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    LiveDrive
                        ? System.Windows.Media.Color.FromRgb(80, 170, 112)
                        : ThemeManager.GetColor("ControlBorder", System.Windows.Media.Color.FromRgb(74, 81, 96)));
            }

            // First press of Live Drive (or a retry after one or more missing
            // devices): scan the USB hardware now - never earlier. A partial
            // rig remains usable, while missing hardware is represented by the
            // red/green indicators on the menu line instead of a popup.
            if (LiveDrive && !_hw.AllConnected)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    var problems = _hw.Connect(_servoConfig,
                        _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory);
                    if (problems.Count > 0)
                        Debug.WriteLine("[hardware] " + string.Join(" | ", problems));
                }
                finally { Mouse.OverrideCursor = null; }
            }

            // The labels are visible from startup; the circles first appear
            // after Live Drive is pressed and then retain the last scan result.
            if (LiveDrive)
                UpdateHardwareStatusIndicators();

            foreach (var row in _rows)
            {
                row.SliderEnabled = true;
                row.SpeedEnabled = row.ShowSpeed;
                foreach (var child in row.Children)
                    child.SliderEnabled = true;
            }

            // Live Drive changes hardware output only; editor/URDF behavior
            // is identical in both states.
            ShowStatus(LiveDrive ? "Live Drive enabled" : "Live Drive disabled");
        }

        /// <summary>Show the four USB-status circles and color each one
        /// from the most recent HardwareManager scan. Labels themselves are
        /// always visible in XAML, even before the first Live Drive attempt.</summary>
        private void UpdateHardwareStatusIndicators()
        {
            static void SetStatus(System.Windows.Shapes.Ellipse dot, bool connected)
            {
                if (dot == null) return;
                dot.Visibility = Visibility.Visible;
                dot.Fill = connected
                    ? System.Windows.Media.Brushes.LimeGreen
                    : System.Windows.Media.Brushes.Red;
                dot.ToolTip = connected ? "Connected" : "Not connected";
            }

            SetStatus(MaestroStatusDot, _hw.MaestroConnected);
            SetStatus(ArduinoStatusDot, _hw.ArduinoConnected);
            SetStatus(LeftTicStatusDot, _hw.LeftTicConnected);
            SetStatus(RightTicStatusDot, _hw.RightTicConnected);
        }

        /// <summary>
        /// A grid slider moved. Update the URDF preview in either Live Drive
        /// state; MoveServoNow gates physical hardware output on Live Drive.
        /// </summary>
        private void RowSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressGridEvents) return;
            if ((sender as FrameworkElement)?.DataContext is not ServoStateRow row) return;

            _manualGridOverrides.Add(row.Servo);
            int value = (int)Math.Round(e.NewValue);
            MoveServoNow(ServoSpeed.NoChange, row.Servo, value);
            ReflectGangIntoChildren(row, value);
        }

        /// <summary>Moving a ganged ServoName's slider also moves its child
        /// sliders to their appropriate positions: the same parent-range
        /// value, negated for gang-reversed children on centered ranges
        /// (mirroring MapDeltatoServo's isGangReversed). Suppressed so the
        /// child updates don't re-drive hardware - the gang move already
        /// drove every member.</summary>
        private void ReflectGangIntoChildren(ServoStateRow row, int value)
        {
            if (row.Children.Count == 0) return;
            bool centered = row.Min < 0;
            bool was = _suppressGridEvents;
            _suppressGridEvents = true;
            try
            {
                foreach (var child in row.Children)
                {
                    bool gangRev = _servoConfig.GangReversed(row.Servo, child.Control);
                    child.Value = (centered && gangRev) ? -value : value;
                }
            }
            finally { _suppressGridEvents = was; }
        }

        /// <summary>RGBCommand row text box: Enter commits and sends the text
        /// to the hardware stub (Live Drive on).</summary>
        private void RowRgbBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox box) return;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            SendRgbRow(box);
        }

        /// <summary>RGBCommand row text box: leaving the box also sends the
        /// (binding-committed) text when Live Drive is on.</summary>
        private void RowRgbBox_LostFocus(object sender, RoutedEventArgs e) =>
            SendRgbRow(sender as TextBox);

        private void SendRgbRow(TextBox box)
        {
            if (_suppressGridEvents) return;
            if (box?.DataContext is not ServoStateRow row || !row.IsTextRow) return;

            // Make sure the typed text has been committed to the row before
            // sending (our LostFocus handler can run before the binding's own
            // LostFocus update, which would send the previous value).
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            _manualGridOverrides.Add(row.Servo);
            ForEachHeadView(v => v.SetEyeColor(row.ColorHex));
            MoveServoNow(ServoSpeed.NoChange, row.Servo, row.TextValue);
        }

        /// <summary>Grid editor (slider / value box / RGB text box) gained
        /// keyboard focus: mark the row as being edited so playback refreshes
        /// leave it alone while the user works on it (Live Drive).</summary>
        private void RowEditor_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ServoStateRow row)
                row.IsEditing = true;
        }

        /// <summary>Focus left the editor. A manually changed row remains
        /// staged at its edited value so additional grid rows can be changed
        /// before generating a command group. Timeline selection/playback is
        /// what returns staged rows to timeline tracking.</summary>
        private void RowEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ServoStateRow row)
                row.IsEditing = false;
        }

        private void ClearManualGridOverrides()
        {
            _manualGridOverrides.Clear();
            foreach (var row in _rows) row.IsEditing = false;
        }

        #endregion

        // ================================================================
        #region 5. Timeline interaction (cursor, zoom, scrollbar)
        // ================================================================

        /// <summary>Left click on the waveform: move the cursor there (the view
        /// already snapped to a nearby command marker) and show what lives there.</summary>
        private void Waveform_TimeClicked(double t) => SetCursor(t);

        /// <summary>
        /// Central "move the cursor" routine. Clamps, seeks the audio if it is
        /// currently playing, updates the time readout, the servo grid (last
        /// value of every servo up to this time) and the commands-at-point list.
        /// </summary>
        private void SetCursor(double t)
        {
            double newTime = Math.Clamp(t, 0, TimelineDuration);
            if (Math.Abs(newTime - _cursorTime) > 1e-9)
                ClearManualGridOverrides();
            _cursorTime = newTime;

            if (_reader != null)
            {
                if (IsRunning)
                {
                    // Click while playing: re-anchor playback at the new
                    // position (this also picks pre-roll vs audio correctly
                    // when the click lands before/after the audio offset).
                    StartPlaybackAt(_cursorTime);
                }
                else if (_mode == PlayMode.Paused)
                {
                    // Resume rebuilds a fresh device on whichever source
                    // owns the new position, so just clear the old one.
                    DisposeAudioDevice();
                    _activeSource = null;
                }
                _lastFiredTime = _cursorTime;   // playback resumes from here
            }

            Waveform.CursorTime = _cursorTime;
            Waveform.InvalidateVisual();
            SyncSplineView();
            UpdateTimeText();
            UpdateServoGrid(_cursorTime);
            UpdateCommandsAtPointList();
        }

        /// <summary>All commands whose offset rounds to the same millisecond
        /// as the given time (i.e. "the commands at this timeline point").</summary>
        private List<ServoCommand> CommandsAt(double t)
        {
            double key = ServoCommand.TimeKey(t);
            return _doc.Commands
                       .Where(c => ServoCommand.TimeKey(c.OffsetSeconds) == key)
                       .ToList();
        }

        /// <summary>Refresh the bottom list showing every command at the cursor.</summary>
        private void UpdateCommandsAtPointList()
        {
            var cmds = CommandsAt(_cursorTime);
            int keep = CommandsAtPointList.SelectedIndex;
            CommandsAtPointList.Items.Clear();

            CommandsAtPointHeader.Text = cmds.Count == 0
                ? $"Commands at cursor {_cursorTime:F3} s: (none)"
                : $"Commands at cursor {_cursorTime:F3} s: {cmds.Count} command(s)";

            foreach (var c in cmds)
                CommandsAtPointList.Items.Add(
                    $"{c.OffsetSeconds:F3}s  {c.Servo}" +
                    (c.Control.HasValue ? $"[{c.Control}]" : "") +
                    $" = {c.ValueDisplay}  ({c.SpeedDisplay})" +
                    (string.IsNullOrWhiteSpace(c.Reason) ? "" : $"  — {c.Reason}"));
            if (CommandsAtPointList.Items.Count > 0)
                CommandsAtPointList.SelectedIndex = Math.Clamp(keep, 0, CommandsAtPointList.Items.Count - 1);
        }

        private string MarkerSummaryAt(double time)
        {
            var cmds = CommandsAt(time);
            if (cmds.Count == 0) return $"No commands at {time:F3} s";
            var lines = cmds.Take(8).Select(c =>
                $"{c.Servo}{(c.Control.HasValue ? $"[{c.Control}]" : "")}: {c.ValueDisplay}");
            string text = $"{cmds.Count} command{(cmds.Count == 1 ? "" : "s")} @ {time:F3} s\n" +
                          string.Join("\n", lines);
            if (cmds.Count > 8) text += $"\n… +{cmds.Count - 8} more";
            return text;
        }

        private void CommandsAtPointAdd_Click(object sender, RoutedEventArgs e) => InsertNewCommand();
        private void CommandsAtPointEdit_Click(object sender, RoutedEventArgs e)
        {
            var cmds = CommandsAt(_cursorTime);
            if (cmds.Count == 0) return;
            int i = CommandsAtPointList.SelectedIndex;
            EditCommandsAtCursor(i >= 0 && i < cmds.Count ? cmds[i] : null);
        }
        private void CommandsAtPointDelete_Click(object sender, RoutedEventArgs e) => DeleteSelectedCommandAtCursor();
        private void CommandsAtPointList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var cmds = CommandsAt(_cursorTime);
            int i = CommandsAtPointList.SelectedIndex;
            if (i >= 0 && i < cmds.Count) EditCommandsAtCursor(cmds[i]);
        }
        private void CommandsAtPointList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            DeleteSelectedCommandAtCursor();
            e.Handled = true;
        }
        private void DeleteSelectedCommandAtCursor()
        {
            var cmds = CommandsAt(_cursorTime);
            int i = CommandsAtPointList.SelectedIndex;
            if (i < 0 || i >= cmds.Count) return;
            PushUndo();
            _doc.Commands.Remove(cmds[i]);
            RefreshAfterEdit();
            ShowStatus("Selected command deleted");
        }

        /// <summary>Rebuild the command-marker list from the unique command offsets.</summary>
        private void RefreshMarkers()
        {
            Waveform.Markers = _doc.Commands
                .Select(c => ServoCommand.TimeKey(c.OffsetSeconds))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var liveMarkers = Waveform.Markers.ToHashSet();
            Waveform.CollisionMarkers = _collisionCommandMarkers
                .Where(liveMarkers.Contains)
                .OrderBy(t => t)
                .ToList();
            Waveform.InvalidateVisual();
        }

        /// <summary>One-stop refresh after any timeline edit. Spline curves
        /// are rebuilt FIRST because the grid now reads interpolated values
        /// from them for spline-checked servos.</summary>
        private void RefreshAfterEdit()
        {
            // Collision warnings describe the exact command values that were
            // previously played. Any command modification invalidates them;
            // playback will repopulate red markers from the edited sequence.
            ClearCollisionCommandWarnings();
            RebuildSplineData();   // control points may have changed
            RefreshMarkers();
            RefreshAudioClips();   // "Play" clips are derived from commands

            // Commands or clips may now extend past the old content end:
            // roll the editable tail forward and keep views in sync.
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            Spline.Duration = TimelineDuration;
            SyncScrollBar();
            UpdateServoGrid(_cursorTime);
            UpdateCommandsAtPointList();
            UpdateDocumentStatusIndicators();
        }

        // --- zoom buttons + scrollbar sync -----------------------------

        private void SequenceRestart_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            SetCursor(0);
            ShowStatus("Sequence cursor moved to beginning");
        }

        private void SequenceStop_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            ShowStatus("Sequence playback stopped");
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => Waveform.ZoomBy(1.5);
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => Waveform.ZoomBy(1 / 1.5);
        private void ZoomFit_Click(object sender, RoutedEventArgs e) => Waveform.ZoomToFit();

        /// <summary>Keep the external scrollbar in sync with the waveform view
        /// whenever zoom, scroll or window size changes.</summary>
        private void SyncScrollBar()
        {
            double visible = Waveform.VisibleSeconds;
            HScroll.Minimum = 0;
            HScroll.Maximum = Math.Max(0, TimelineDuration - visible);
            HScroll.ViewportSize = visible;
            HScroll.LargeChange = visible * 0.9;
            HScroll.SmallChange = visible * 0.1;
            HScroll.Value = Waveform.ViewStart;
        }

        private void HScroll_Scroll(object sender, ScrollEventArgs e) =>
            Waveform.SetViewStart(e.NewValue);

        #endregion

        // ================================================================
        #region 6. Right-click context menu
        // ================================================================

        /// <summary>
        /// Right click on the waveform: build a context menu for the *last
        /// selected cursor position* (right-clicking does not move the cursor,
        /// per spec). Items appear/enable depending on whether commands exist
        /// at the cursor and whether the clipboard holds copied commands.
        /// </summary>
        private void Waveform_RightClicked(double clickTime)
        {
            if (_libraryPrompt == LibraryPrompt.InsertSequence)
            {
                ConfirmPendingLibraryInsert();
                return;
            }

            var atCursor = CommandsAt(_cursorTime);
            var menu = new ContextMenu();

            MenuItem Item(string header, RoutedEventHandler click, bool enabled = true)
            {
                var mi = new MenuItem { Header = header, IsEnabled = enabled };
                mi.Click += click;
                menu.Items.Add(mi);
                return mi;
            }

            Item($"Insert new command at {_cursorTime:F3} s…", (_, _) => InsertNewCommand());

            Item($"Edit {atCursor.Count} command(s) at {_cursorTime:F3} s…",
                 (_, _) => EditCommandsAtCursor(), atCursor.Count > 0);

            Item($"Delete all commands at {_cursorTime:F3} s",
                 (_, _) => DeleteCommandsAtCursor(), atCursor.Count > 0);

            menu.Items.Add(new Separator());

            Item($"Copy {atCursor.Count} command(s) at {_cursorTime:F3} s",
                 (_, _) => CopyCommandsAtCursor(), atCursor.Count > 0);

            Item($"Paste {_clipboard.Count} copied command(s) at {_cursorTime:F3} s",
                 (_, _) => PasteClipboardAtCursor(), _clipboard.Count > 0);

            menu.Items.Add(new Separator());

            Item($"Insert commands from JSON file at {_cursorTime:F3} s…",
                 (_, _) => InsertCommandsFromFile());

            Item($"Insert audio file at {_cursorTime:F3} s…",
                 (_, _) => InsertAudioFileAtCursor());

            Item($"Generate commands from grid values at {_cursorTime:F3} s",
                 (_, _) => GenerateCommandsFromGrid());

            menu.PlacementTarget = Waveform;
            menu.IsOpen = true;
        }

        /// <summary>Create one new command at the cursor and open the editor
        /// so its fields can be filled in.</summary>
        private void InsertNewCommand()
        {
            PushUndo();
            var cmd = new ServoCommand
            {
                OffsetSeconds = ServoCommand.TimeKey(_cursorTime),
                Servo = ServoNames.NeckTurn,
                NumericValue = 0,
                Speed = ServoSpeed.NoChange,
            };
            _doc.Commands.Add(cmd);
            RefreshAfterEdit();
            EditCommandsAtCursor();
        }

        /// <summary>Open the modal editor for every command at the cursor.
        /// The editor mutates the live ServoCommand objects, so a single
        /// refresh after it closes brings the whole UI up to date.</summary>
        private void EditCommandsAtCursor(ServoCommand focusCommand = null)
        {
            PushUndo();   // one undo step for the whole editor session
            var editor = new CommandEditorWindow(_doc, _cursorTime,
                                                 MoveServoNow,      // numeric variant
                                                 MoveServoNow,      // text variant (RGBCommand)
                                                 MoveChildServoNow, // individual-control variant
                                                 ConfigureServoSpeedNow,
                                                 ConfigureChildServoSpeedNow,
                                                 focusCommand)
            {
                Owner = this,
            };
            editor.ShowDialog();
            RefreshAfterEdit();
        }

        /// <summary>Delete every command at the cursor; the command marker is
        /// removed automatically because RefreshMarkers() rebuilds from the
        /// remaining commands.</summary>
        private void DeleteCommandsAtCursor()
        {
            PushUndo();
            foreach (var c in CommandsAt(_cursorTime))
                _doc.Commands.Remove(c);
            RefreshAfterEdit();
        }

        /// <summary>Copy the command group at the cursor into the clipboard
        /// as deep copies (so later edits don't change the copies).</summary>
        private void CopyCommandsAtCursor()
        {
            _clipboard.Clear();
            _clipboard.AddRange(CommandsAt(_cursorTime).Select(c => c.Clone()));
            ShowStatus($"{_clipboard.Count} command(s) copied");
        }

        /// <summary>Paste the copied group at the cursor: all pasted commands
        /// get the cursor's time offset, and a '+' appears there.</summary>
        private void PasteClipboardAtCursor()
        {
            PushUndo();
            double t = ServoCommand.TimeKey(_cursorTime);
            foreach (var c in _clipboard)
            {
                var copy = c.Clone();
                copy.OffsetSeconds = t;
                _doc.Commands.Add(copy);
            }
            RefreshAfterEdit();
            ShowStatus($"{_clipboard.Count} command(s) pasted at {_cursorTime:F3} s");
        }

        /// <summary>
        /// "Insert commands from JSON file": every command read from the file
        /// has its offsetSeconds incremented by the cursor offset, then it is
        /// added to the timeline. Each unique resulting offset gets a '+' and
        /// the commands become editable like any others.
        /// </summary>
        private void InsertCommandsFromFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Insert commands from JSON",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var cmds = AnimationDocument.LoadCommandsOnly(dlg.FileName);
                PushUndo();
                foreach (var c in cmds)
                {
                    c.OffsetSeconds = ServoCommand.TimeKey(c.OffsetSeconds + _cursorTime);
                    _doc.Commands.Add(c);
                }
                RefreshAfterEdit();
                ShowStatus($"Inserted {cmds.Count} command(s) from {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read commands:\n" + ex.Message,
                                "Insert error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Create one command per servo at the cursor, capturing the
        /// grid's current Speed and Value (numeric or text) for each servo
        /// (a "keyframe all").</summary>
        private void GenerateCommandsFromGrid()
        {
            PushUndo();
            double t = ServoCommand.TimeKey(_cursorTime);
            foreach (var row in _rows)
            {
                _doc.Commands.Add(new ServoCommand
                {
                    OffsetSeconds = t,
                    Servo = row.Servo,
                    NumericValue = row.IsTextRow ? 0 : (int)Math.Round(row.Value),
                    TextValue = row.IsTextRow ? row.TextValue : "",
                    Speed = row.Speed,
                    Reason = "generated from grid",
                });
            }
            // The staged values are now real timeline commands at this point.
            ClearManualGridOverrides();
            RefreshAfterEdit();
        }

        #endregion

        // ================================================================
        #region 6b. Spline system
        // ================================================================

        /// <summary>A spline-checked servo toggled: rebuild legend + curves.</summary>
        private void SplineCheck_Click(object sender, RoutedEventArgs e)
        {
            RebuildSplineData();
            UpdateDocumentStatusIndicators();
        }

        /// <summary>Legend show/hide checkbox toggled: remember and redraw.</summary>
        private void LegendToggle_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SplineLegendItem item)
                _lineVisible[item.Servo] = item.Visible;
            RebuildSplineData();
        }

        /// <summary>True when "Animate individual" is selected: exports
        /// expand ganged commands into per-child-control commands.</summary>
        /// <summary>Export mode, held on the document (chosen in the
        /// Export Animation JSON dialog, saved with the sequence).</summary>
        private bool AnimateIndividual => _doc.AnimateMode != "Ganged";

        /// <summary>
        /// LEFT-drag on a spline point: rewrite the value of the command(s)
        /// at that servo+time. Called continuously during the drag; only the
        /// spline curves and the grid are refreshed live, the full (heavier)
        /// refresh happens once on DragCompleted.
        /// </summary>
        private void Spline_PointValueChanged(ServoNames servo, double timeKey, int value)
        {
            if (!_dragUndoPushed) { PushUndo(); _dragUndoPushed = true; }
            foreach (var c in _doc.Commands.Where(c =>
                         c.Servo == servo &&
                         ServoCommand.TimeKey(c.OffsetSeconds) == timeKey))
                c.NumericValue = value;   // clamped per-servo by the model

            RebuildSplineData();
            UpdateServoGrid(_cursorTime);
        }

        /// <summary>
        /// RIGHT-drag on a spline point: move the command(s) at oldKey to
        /// newKey (the SplineView already refused collisions with another
        /// point of the same servo). Markers are refreshed live so the '+'
        /// follows the drag on the waveform.
        /// </summary>
        private void Spline_PointTimeChanged(ServoNames servo, double oldKey, double newKey)
        {
            if (!_dragUndoPushed) { PushUndo(); _dragUndoPushed = true; }
            foreach (var c in _doc.Commands.Where(c =>
                         c.Servo == servo &&
                         ServoCommand.TimeKey(c.OffsetSeconds) == oldKey).ToList())
                c.OffsetSeconds = newKey;

            RefreshMarkers();
            RebuildSplineData();
        }

        /// <summary>
        /// CTRL + LEFT-click on a spline line: create a new control point ON
        /// the curve (a new command with the curve's value at that time).
        /// The full refresh places the corresponding '+' on the waveform.
        /// </summary>
        private void Spline_PointAdded(ServoNames servo, double timeKey, int value)
        {
            // Never duplicate an existing point of this servo.
            if (_doc.Commands.Any(c => c.Servo == servo &&
                    ServoCommand.TimeKey(c.OffsetSeconds) == timeKey))
                return;

            PushUndo();

            _doc.Commands.Add(new ServoCommand
            {
                OffsetSeconds = timeKey,
                Servo = servo,
                NumericValue = value,
                Speed = ServoSpeed.NoChange,
                Reason = "spline point",
            });
            RefreshAfterEdit();
        }

        /// <summary>
        /// DELETE pressed with a spline point selected (left-clicked): remove
        /// the command(s) of that servo at that time. The full refresh also
        /// removes the '+' from the waveform if nothing else remains there.
        /// </summary>
        private void Spline_PointDeleted(ServoNames servo, double timeKey)
        {
            PushUndo();
            foreach (var c in _doc.Commands.Where(c =>
                         c.Servo == servo &&
                         ServoCommand.TimeKey(c.OffsetSeconds) == timeKey).ToList())
                _doc.Commands.Remove(c);

            RefreshAfterEdit();
        }

        /// <summary>All servos currently spline-checked (text servos never are).</summary>
        private List<ServoNames> SplineServosEnabled() =>
            _rows.Where(r => r.SplineEnabled && !r.IsTextRow)
                 .Select(r => r.Servo).ToList();

        /// <summary>This servo's spline control points: its commands on the
        /// timeline, deduped per millisecond time key, sorted by time.</summary>
        private (double[] T, double[] V) SplinePoints(ServoNames servo)
        {
            var pts = _doc.Commands
                .Where(c => c.Servo == servo && !c.Control.HasValue && !c.Disable)
                .GroupBy(c => ServoCommand.TimeKey(c.OffsetSeconds))
                .OrderBy(g => g.Key)
                .Select(g => (T: g.Key, V: (double)g.Last().NumericValue))
                .ToArray();
            return (pts.Select(p => p.T).ToArray(), pts.Select(p => p.V).ToArray());
        }

        private System.Windows.Media.Brush BrushFor(ServoNames s) =>
            (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                .ConvertFromString(CurvePalette[(int)s % CurvePalette.Length]);

        private SkiaSharp.SKColor SkColorFor(ServoNames s) =>
            SkiaSharp.SKColor.Parse(CurvePalette[(int)s % CurvePalette.Length]);

        /// <summary>
        /// Rebuilds everything the spline area shows: the legend (one entry
        /// per spline-checked servo, preserving each servo's show/hide state)
        /// and the curve data (control points + precomputed Hermite tangents).
        /// Hides the whole area when no servo is spline-checked.
        /// </summary>
        private void RebuildSplineData()
        {
            var enabled = SplineServosEnabled();
            bool showSpline = enabled.Count > 0;
            if (showSpline)
            {
                SplineArea.Visibility = Visibility.Visible;
                if (SplineTimelineRow.Height.Value <= 0.0)
                    SplineTimelineRow.Height = _lastSplineTimelineHeight;
            }
            else
            {
                if (SplineTimelineRow.ActualHeight > 1.0)
                    _lastSplineTimelineHeight = new GridLength(
                        Math.Max(80.0, SplineTimelineRow.ActualHeight), GridUnitType.Pixel);
                SplineArea.Visibility = Visibility.Collapsed;
                SplineTimelineRow.Height = new GridLength(0, GridUnitType.Pixel);
            }

            // Legend: rebuild in grid order, keeping remembered visibility.
            _legend.Clear();
            foreach (var s in enabled)
            {
                if (!_lineVisible.TryGetValue(s, out bool vis)) vis = true;
                _legend.Add(new SplineLegendItem
                {
                    Servo = s,
                    Name = s.ToString(),
                    Brush = BrushFor(s),
                    Visible = vis,
                });
            }

            // Curves for the renderer. A spline-checked servo with NO
            // commands yet is not graphed at all (no line, no dots) until
            // its first command exists; its legend entry still shows so the
            // checked state is visible.
            var curves = new List<SplineCurve>();
            foreach (var s in enabled)
            {
                var (t, v) = SplinePoints(s);
                if (t.Length == 0) continue;   // nothing to graph yet

                var (mn, mx) = ServoCommand.RangeFor(s);
                curves.Add(new SplineCurve
                {
                    Servo = s,
                    Color = SkColorFor(s),
                    T = t,
                    V = v,
                    M = SplineUtil.Tangents(t, v),
                    Min = mn,
                    Max = mx,
                    Visible = !_lineVisible.TryGetValue(s, out bool lv) || lv,
                });
            }
            Spline.Curves = curves;
            SyncSplineView();
        }

        /// <summary>Copy the waveform's zoom/scroll/cursor into the spline
        /// view so the two strips always line up.</summary>
        private void SyncSplineView()
        {
            if (Spline == null) return;
            Spline.ViewStart = Waveform.ViewStart;
            Spline.PixelsPerSecond = Waveform.PixelsPerSecond;
            Spline.CursorTime = _cursorTime;
            Spline.Duration = TimelineDuration;
            Spline.InvalidateVisual();
        }

        /// <summary>
        /// Used at SAVE time: for one spline-checked servo, generate sampled
        /// commands along the Hermite curve at the selected frequency,
        /// between the servo's first and last control point.
        ///
        /// A sample is only emitted when the servo's (rounded) value has
        /// CHANGED since the previous offset for that servo - flat stretches
        /// of the curve produce no redundant commands. "Previous offset"
        /// tracks the full time-ordered output stream, i.e. both the
        /// hand-placed control points (which are already in the file) and
        /// previously emitted samples. Samples landing exactly on a control
        /// point's time are skipped regardless. Generated commands use
        /// Speed=Default and a reason tag identifying them.
        /// </summary>
        private IEnumerable<ServoCommand> GenerateSplineSamples(ServoNames servo)
        {
            var (t, v) = SplinePoints(servo);
            if (t.Length < 2) yield break;

            var m = SplineUtil.Tangents(t, v);
            var (mn, mx) = ServoCommand.RangeFor(servo);
            var controlKeys = new HashSet<double>(t.Select(ServoCommand.TimeKey));

            double dt = 1.0 / _splineHz;
            int nextControl = 1;                       // index of the next control point
            int lastValue = (int)Math.Round(v[0]);     // value at the previous offset

            for (double x = t[0] + dt; x < t[^1] - 1e-9; x += dt)
            {
                // Any control points passed since the last sample update the
                // "value at the previous offset" - they are already commands
                // in the saved file.
                while (nextControl < t.Length && t[nextControl] <= x + 1e-9)
                {
                    lastValue = (int)Math.Round(v[nextControl]);
                    nextControl++;
                }

                double key = ServoCommand.TimeKey(x);
                if (controlKeys.Contains(key)) continue;   // already a command there

                int value = (int)Math.Round(Math.Clamp(
                    SplineUtil.Eval(t, v, m, x), mn, mx));

                if (value == lastValue) continue;          // unchanged: emit nothing

                lastValue = value;
                yield return new ServoCommand
                {
                    OffsetSeconds = key,
                    Servo = servo,
                    NumericValue = value,
                    Speed = ServoSpeed.NoChange,
                    Reason = $"spline {_splineHz}Hz",
                };
            }
        }

        #endregion

        // ================================================================
        #region 7. JSON load / save / clear
        // ================================================================

        /// <summary>
        /// File > Load Project: reads a PROJECT JSON (audio file pathname,
        /// spline sample frequency, and the command control points - no
        /// interpolated spline samples). Also opens older exported animation
        /// files, falling back to the audio filename next to the JSON when
        /// no full pathname is stored.
        /// </summary>
        private void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Load sequence JSON",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = SequenceDialogFolder(),
            };
            if (dlg.ShowDialog() != true) return;
            LoadSequenceFromPath(dlg.FileName, 0, fitTimeline: true, recordRecent: true, setActiveDocument: true);
        }

        /// <summary>Loads a sequence from a known pathname. Movie selection
        /// uses the same path so every selection is a fresh disk reload and
        /// therefore reflects edits made outside the movie editor as well.</summary>
        private bool LoadSequenceFromPath(string path, double initialCursor,
                                          bool fitTimeline,
                                          bool recordRecent = true,
                                          bool setActiveDocument = true)
        {
            try
            {
                StopPlayback();
                _reader?.Dispose();
                _reader = null;
                _audioPath = null;
                _primaryDuration = 0;
                _activeSource = null;
                _lastDesiredKey = null;

                _doc = AnimationDocument.Load(path);
                _jsonPath = path;
                RememberSequencePath(path);
                _undoStack.Clear();
                _redoStack.Clear();

                SetDescriptionText(_doc.Description);

                foreach (var row in _rows)
                    row.SplineEnabled = !row.IsTextRow &&
                        (_doc.SplineServos?.Contains(row.Servo.ToString()) ?? false);
                _splineHz = Array.IndexOf(SplineHzOptions, _doc.SplineSampleHz) >= 0
                    ? _doc.SplineSampleHz : 50;

                _audioOffset = Math.Max(0, _doc.AudioStartOffsetSeconds);
                Waveform.PrimaryAudioName = "";
                Waveform.AudioOffset = _audioOffset;
                Waveform.SetAudio(null, null, 0.001, 0);

                var missingClips = RefreshAudioClips();
                if (missingClips.Count > 0)
                    MessageBox.Show(this,
                        "These additional audio files could not be found " +
                        "(their clips show without a waveform — re-insert " +
                        "or fix the paths): " +
                        string.Join(" ", missingClips),
                        "Missing audio files", MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                string candidate = _doc.AudioFilePath;
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                    candidate = string.IsNullOrWhiteSpace(_doc.AudioFile) ? null
                        : Path.Combine(Path.GetDirectoryName(path) ?? "", _doc.AudioFile);

                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    LoadAudio(candidate);
                }
                else if (!string.IsNullOrWhiteSpace(_doc.AudioFilePath) ||
                         !string.IsNullOrWhiteSpace(_doc.AudioFile))
                {
                    MessageBox.Show(this,
                        "The project's audio file could not be found:" +
                        (_doc.AudioFilePath ?? _doc.AudioFile) +
                        "Use File > Open Audio… to relink it.",
                        "Audio not found", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                Waveform.AudioOffset = _audioOffset;
                Waveform.Duration = TimelineDuration;
                Waveform.ContentDuration = ContentEnd;

                SetCursor(Math.Clamp(initialCursor, 0, ContentEnd));
                RefreshAfterEdit();
                if (fitTimeline) Waveform.ZoomToFit();
                SyncScrollBar();
                UpdateTitle();
                _savedSequenceFingerprint = CurrentSequenceFingerprint();
                UpdateDocumentStatusIndicators();
                if (setActiveDocument)
                    _activeDocumentKind = ActiveDocumentKind.Sequence;
                if (recordRecent)
                    RecordRecentFile(path, ActiveDocumentKind.Sequence, setActiveDocument);
                ShowStatus($"Sequence loaded: {Path.GetFileName(path)}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open JSON:" + ex.Message,
                                "Open error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>File > Save Project (Save As when never saved).</summary>
        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_jsonPath)) { SaveProjectAs_Click(sender, e); return; }
            SaveProjectTo(_jsonPath);
        }

        private void SaveProjectAs_Click(object sender, RoutedEventArgs e) =>
            SaveProjectAsInteractive();

        private bool SaveProjectAsInteractive()
        {
            // Default name: <audio base name>_seq.json (e.g. song.wav ->
            // song_seq.json), or sequence_seq.json without audio. The
            // description is intentionally not used as a filename.
            string projDefault;
            if (!string.IsNullOrEmpty(_jsonPath))
                projDefault = Path.GetFileName(_jsonPath);
            else if (!string.IsNullOrEmpty(_doc.AudioFile))
                projDefault = Path.GetFileNameWithoutExtension(_doc.AudioFile)
                              + "_seq.json";
            else
                projDefault = "sequence_seq.json";

            var dlg = new SaveFileDialog
            {
                Title = "Save sequence JSON",
                Filter = "JSON files (*.json)|*.json",
                FileName = projDefault,
                InitialDirectory = SequenceDialogFolder(),
            };
            if (dlg.ShowDialog() != true) return false;
            return SaveProjectTo(dlg.FileName);
        }

        /// <summary>
        /// File > Export Animation JSON: the playback-ready file - all
        /// control-point commands PLUS commands generated along each
        /// spline-checked servo's curve at the selected sample frequency.
        /// Does not change the project path/title.
        /// </summary>
        private void ExportAnimation_Click(object sender, RoutedEventArgs e)
        {
            // Default: <audio base name>_ani.json in the Project folder.
            string aniDefault = !string.IsNullOrEmpty(_doc.AudioFile)
                ? Path.GetFileNameWithoutExtension(_doc.AudioFile) + "_ani.json"
                : "animation.json";
            string defaultPath = Path.Combine(
                _folders?.ProjectFolderOrDefault ?? "", aniDefault);

            // One dialog holds the export options (animate mode, spline
            // sample rate, Scale ±1 - moved here from the spline area) and
            // the destination file. Choices persist with the sequence.
            var win = new ExportAnimationWindow(
                SplineHzOptions, AnimateIndividual, _splineHz,
                _doc.ScaleValues, defaultPath)
            { Owner = this };
            if (win.ShowDialog() != true) return;

            _doc.AnimateMode = win.AnimateIndividual ? "Individual" : "Ganged";
            _doc.ScaleValues = win.ScaleValues;
            _splineHz = win.SampleHz;

            ExportAnimationTo(win.FilePath);
        }

        /// <summary>Fill the document's metadata from the current UI state
        /// (shared by project save and animation export).</summary>
        private void SyncDocMetadata()
        {
            _doc.Description = DescriptionBox.Text;
            _doc.AudioStartOffsetSeconds = _audioOffset;
            _doc.SplineServos = SplineServosEnabled().Select(s => s.ToString()).ToList();
            _doc.SplineSampleHz = _splineHz;
            if (_primaryDuration > 0)
            {
                _doc.AudioFilePath = _audioPath;                    // full pathname
                _doc.AudioFile = Path.GetFileName(_audioPath);
                // Total timeline length = pre-roll offset + audio length.
                _doc.DurationSeconds = Math.Round(
                    _audioOffset + _reader.TotalTime.TotalSeconds, 2);
            }
        }

        /// <summary>
        /// PROJECT save: audio file pathname, sample frequency, and all the
        /// data points shown on the waveform - the command control points -
        /// but NOT the interpolated spline values. Shows the filename in the
        /// title bar.
        /// </summary>
        /// <summary>'|'-delimited list of every audio file: the primary
        /// audio first, then each additional "Play" clip in offset order.</summary>
        private string BuildAudioFilesHeader()
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(_doc.AudioFile)) names.Add(_doc.AudioFile);
            names.AddRange(_doc.Commands
                .Where(c => c.Servo == ServoNames.Play)
                .OrderBy(c => c.OffsetSeconds)
                .Select(c => Path.GetFileName(c.TextValue ?? "")));
            return string.Join("|", names);
        }

        private bool SaveProjectTo(string path)
        {
            _doc.AudioFiles = BuildAudioFilesHeader();
            string oldPath = _jsonPath;

            try
            {
                SyncDocMetadata();
                _doc.Save(path);          // _doc holds only control points
                _jsonPath = path;
                RememberSequencePath(path);

                // If Save As renamed the sequence currently represented by a
                // movie block, keep that block attached to the new file.
                if (_movieSelectedIndex >= 0 && _movieSelectedIndex < _movieItems.Count &&
                    !string.IsNullOrWhiteSpace(oldPath) &&
                    PathsEqual(_movieItems[_movieSelectedIndex].FilePath, oldPath))
                    _movieItems[_movieSelectedIndex].FilePath = path;

                _savedSequenceFingerprint = CurrentSequenceFingerprint();
                RefreshMovieDurationForPath(path);
                UpdateDocumentStatusIndicators();
                if (_activeDocumentKind != ActiveDocumentKind.Movie)
                {
                    _activeDocumentKind = ActiveDocumentKind.Sequence;
                    RecordRecentFile(path, ActiveDocumentKind.Sequence, setActive: true);
                }
                ShowStatus($"Sequence saved: {Path.GetFileName(path)}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save project:" + ex.Message,
                                "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// ANIMATION export: control points PLUS spline samples, for playback
        /// hardware. The in-memory timeline is NOT modified - the control
        /// points stay the editing master, and re-exporting regenerates
        /// fresh samples.
        /// </summary>
        private void ExportAnimationTo(string path)
        {
            try
            {
                SyncDocMetadata();

                var export = new AnimationDocument
                {
                    Description = _doc.Description,
                    AudioFile = _doc.AudioFile,
                    AudioFilePath = _doc.AudioFilePath,
                    DurationSeconds = _doc.DurationSeconds,
                    AudioStartOffsetSeconds = _doc.AudioStartOffsetSeconds,
                    SplineServos = _doc.SplineServos,
                    SplineSampleHz = _doc.SplineSampleHz,
                    Commands = _doc.Commands.Select(c => c.Clone()).ToList(),
                };
                export.AudioFiles = BuildAudioFilesHeader();

                foreach (var servo in SplineServosEnabled())
                    export.Commands.AddRange(GenerateSplineSamples(servo));

                // Audio start command: tells the playback hardware which file
                // to start and when. Field values mirror the project file:
                //   offsetSeconds = audioStartOffsetSeconds
                //   servo = "Play", value = audioFilePath, speed = Default,
                //   reason = audioFile
                // Save() sorts by offset, keeping it in offset-seconds order.
                if (_primaryDuration > 0)
                {
                    export.Commands.Add(new ServoCommand
                    {
                        OffsetSeconds = _doc.AudioStartOffsetSeconds,
                        Servo = ServoNames.Play,
                        TextValue = _doc.AudioFilePath,
                        Speed = ServoSpeed.NoChange,
                        Reason = _doc.AudioFile,
                    });
                }

                // "Animate individual": every ganged command whose
                // ServoName drives MORE THAN ONE control is replaced by one
                // command per child control, with values adjusted for the
                // child's gang-relative direction (centered: negate; 0..100:
                // 100-v) so each exported value is directly meaningful for
                // that servo. Commands already targeting an individual
                // control, single-servo commands, RGB/Play, and everything
                // else pass through unchanged. BothEyePop expands to the
                // LeftEyePop/RightEyePop ServoNames. "Animate ganged"
                // exports ganged values as-is (individually-added child
                // commands are in the list either way).
                if (AnimateIndividual)
                    export.Commands = ExpandGangedCommands(export.Commands);

                // "Scale ±1": divide every numeric value by its range's
                // maximum, so -100..100 -> -1.000..1.000, 0..100 ->
                // 0..1.000, and the 0..2000 eye pops -> 0..1.000 (3
                // decimals). Text values (RGB, Play), Disable commands,
                // and everything non-numeric pass through unchanged. The
                // export list holds clones, so the in-memory project keeps
                // its native-range values.
                if (_doc.ScaleValues)
                {
                    foreach (var c in export.Commands)
                    {
                        if (c.IsTextServo || c.Disable ||
                            c.Servo == ServoNames.Play) continue;
                        var (_, max) = ServoCommand.RangeFor(c.Servo);
                        c.ScaledExportValue = (double)c.NumericValue / max;
                    }
                }

                export.Save(path);
                ShowStatus($"Animation exported: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not export animation:\n" + ex.Message,
                                "Export error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>"Clear" with an explicit Are-you-sure Y/N confirmation.
        /// Removes every command and therefore every command marker.</summary>
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(this,
                "Are you sure? This removes ALL commands from the timeline.",
                "Clear timeline", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;

            PushUndo();
            _doc.Commands.Clear();
            RefreshAfterEdit();
        }

        /// <summary>Set both description editors without creating a
        /// TextChanged feedback loop.</summary>
        private void SetDescriptionText(string text)
        {
            _syncingDescription = true;
            string value = text ?? "";
            if (DescriptionBox != null) DescriptionBox.Text = value;
            if (DescriptionExpandedBox != null) DescriptionExpandedBox.Text = value;
            _syncingDescription = false;
        }

        private void DescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingDescription) return;
            _syncingDescription = true;
            string value = DescriptionBox?.Text ?? "";
            if (DescriptionExpandedBox != null && DescriptionExpandedBox.Text != value)
                DescriptionExpandedBox.Text = value;
            if (_doc != null) _doc.Description = value;
            _syncingDescription = false;
            UpdateDocumentStatusIndicators();
        }

        private void DescriptionExpandedBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingDescription) return;
            _syncingDescription = true;
            string value = DescriptionExpandedBox?.Text ?? "";
            if (DescriptionBox != null && DescriptionBox.Text != value)
                DescriptionBox.Text = value;
            if (_doc != null) _doc.Description = value;
            _syncingDescription = false;
            UpdateDocumentStatusIndicators();
        }

        private void DescriptionExpand_Click(object sender, RoutedEventArgs e)
        {
            SetDescriptionText(DescriptionBox?.Text);
            DescriptionOverlay.Visibility = Visibility.Visible;
            DescriptionExpandedBox.Focus();
            DescriptionExpandedBox.CaretIndex = DescriptionExpandedBox.Text.Length;
        }

        private void DescriptionCollapse_Click(object sender, RoutedEventArgs e)
        {
            DescriptionOverlay.Visibility = Visibility.Collapsed;
            DescriptionBox.Focus();
            DescriptionBox.CaretIndex = DescriptionBox.Text.Length;
        }

        /// <summary>Window title and compact file/dirty indicators.</summary>
        private void UpdateTitle()
        {
            string file = string.IsNullOrEmpty(_jsonPath) ? "" : $" — {Path.GetFileName(_jsonPath)}";
            string dirty = SequenceHasUnsavedChanges() ? " *" : "";
            Title = AppDisplayName + file + dirty;
        }

        private string CurrentMovieFingerprint()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_movieDescription ?? "");
            sb.AppendLine(_movieCreatedDate ?? "");
            foreach (var item in _movieItems)
                sb.AppendLine(item.FilePath ?? "");
            return sb.ToString();
        }

        private bool MovieHasUnsavedChanges() =>
            !string.Equals(_savedMovieFingerprint ?? "", CurrentMovieFingerprint(), StringComparison.Ordinal);

        private void UpdateDocumentStatusIndicators()
        {
            if (SequenceFileText != null)
            {
                string name = string.IsNullOrWhiteSpace(_jsonPath) ? "(unsaved)" : Path.GetFileNameWithoutExtension(_jsonPath);
                SequenceFileText.Text = $"Sequence: {name}{(SequenceHasUnsavedChanges() ? " *" : "")}";
                SequenceFileText.ToolTip = string.IsNullOrWhiteSpace(_jsonPath) ? "Unsaved sequence" : _jsonPath;
            }
            if (MovieFileText != null)
            {
                string name = string.IsNullOrWhiteSpace(_moviePath) ? "(unsaved)" : Path.GetFileNameWithoutExtension(_moviePath);
                MovieFileText.Text = name + (MovieHasUnsavedChanges() ? " *" : "");
                MovieFileText.ToolTip = string.IsNullOrWhiteSpace(_moviePath) ? "Unsaved movie" : _moviePath;
            }
            UpdateTitle();
        }

        private void ShowStatus(string text)
        {
            if (StatusText == null) return;
            StatusText.Text = text ?? "Ready";
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        #endregion

        // ================================================================
        #region 8. Undo / Redo (Edit menu, Ctrl+Z / Ctrl+Y)
        // ================================================================

        private List<ServoCommand> Snapshot() =>
            _doc.Commands.Select(c => c.Clone()).ToList();

        /// <summary>Push the current command list onto the undo stack.
        /// Called BEFORE every mutating timeline operation. Any new edit
        /// invalidates the redo history.</summary>
        private void PushUndo()
        {
            _undoStack.Add(Snapshot());
            if (_undoStack.Count > UndoLimit) _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        private void RestoreSnapshot(List<ServoCommand> snap)
        {
            _doc.Commands = snap.Select(c => c.Clone()).ToList();
            RefreshAfterEdit();
        }

        private void Undo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
            => e.CanExecute = _undoStack.Count > 0;

        private void Redo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
            => e.CanExecute = _redoStack.Count > 0;

        /// <summary>Undo: current state goes to the redo stack, the top undo
        /// snapshot becomes the timeline.</summary>
        private void Undo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Add(Snapshot());
            var snap = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            RestoreSnapshot(snap);
        }

        /// <summary>Redo: puts the last undone change back.</summary>
        private void Redo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Add(Snapshot());
            var snap = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            RestoreSnapshot(snap);
        }

        #endregion

        // ================================================================
        #region 9. File menu (New / Exit) + About
        // ================================================================

        /// <summary>File > New: clears everything - commands, audio,
        /// description, offsets, spline state and undo history - after confirmation.</summary>
        private void FileNew_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(this,
                "Start a new project? This clears the timeline, audio and all settings.",
                "New project", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            StopPlayback();            // disposes the output device
            _reader?.Dispose(); _reader = null;
            _audioPath = null;
            _jsonPath = null;
            _activeDocumentKind = ActiveDocumentKind.None;
            _recentFiles?.ClearLastActive();
            SaveRecentFiles();

            _doc = new AnimationDocument();
            SetDescriptionText(_doc.Description);

            Waveform.PrimaryAudioName = "";
            _primaryDuration = 0;
            _activeSource = null;
            _peakCache.Clear();
            _audioOffset = 0;
            _undoStack.Clear();
            _redoStack.Clear();
            _clipboard.Clear();

            foreach (var row in _rows) row.SplineEnabled = false;
            _splineHz = 50;   // export options come from the new document
                              // (Individual / 50 Hz / no scaling)

            Waveform.AudioOffset = 0;
            Waveform.SetAudio(null, null, 0.001, 0);   // empty waveform
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;       // default empty canvas
            EndArrowPrompt();

            SetCursor(0);
            RefreshAfterEdit();
            Waveform.ZoomToFit();
            SyncScrollBar();
            UpdateTitle();
            _savedSequenceFingerprint = CurrentSequenceFingerprint();
            UpdateDocumentStatusIndicators();
            ShowStatus("New sequence created");
        }

        /// <summary>Restore the last editor/window arrangement saved in the
        /// active Configuration folder. Document content is not part of this file.</summary>
        private void LoadEditorLayout()
        {
            var layout = EditorLayoutSettings.Load(_folders?.ConfigFolderOrDefault);
            if (layout == null)
            {
                // Ensure the initial toggle label reflects the default visible state.
                CommandsListToggle_Changed(CommandsListToggle, new RoutedEventArgs());
                return;
            }

            try
            {
                if (layout.WindowWidth >= MinWidth && layout.WindowHeight >= MinHeight &&
                    EditorLayoutSettings.IsVisibleOnVirtualDesktop(
                        layout.WindowLeft, layout.WindowTop, layout.WindowWidth, layout.WindowHeight))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = layout.WindowLeft;
                    Top = layout.WindowTop;
                    Width = layout.WindowWidth;
                    Height = layout.WindowHeight;
                }

                _lastDockedServoColumnWidth = layout.ServoEditorColumn.ToGridLength(new GridLength(1, GridUnitType.Star));
                _lastDockedUrdfColumnWidth = layout.UrdfEditorColumn.ToGridLength(new GridLength(1, GridUnitType.Star));
                ServoEditorColumn.Width = _lastDockedServoColumnWidth;
                UrdfEditorColumn.Width = _lastDockedUrdfColumnWidth;
                UndockedServoLeftColumn.Width = layout.UndockedServoLeftColumn.ToGridLength(new GridLength(1, GridUnitType.Star));
                UndockedServoRightColumn.Width = layout.UndockedServoRightColumn.ToGridLength(new GridLength(1, GridUnitType.Star));
                TopEditorRow.Height = layout.TopEditorRow.ToGridLength(new GridLength(250, GridUnitType.Pixel));
                AudioTimelineRow.Height = layout.AudioTimelineRow.ToGridLength(new GridLength(1, GridUnitType.Star));
                _lastSplineTimelineHeight = layout.LastSplineTimelineHeight.ToGridLength(
                    new GridLength(190, GridUnitType.Pixel));

                if (EditorLayoutSettings.IsVisibleOnVirtualDesktop(
                    layout.UrdfWindowLeft, layout.UrdfWindowTop,
                    layout.UrdfWindowWidth, layout.UrdfWindowHeight))
                {
                    _savedUrdfWindowBounds = new Rect(
                        layout.UrdfWindowLeft, layout.UrdfWindowTop,
                        layout.UrdfWindowWidth, layout.UrdfWindowHeight);
                }
                _savedUrdfWindowState = Enum.TryParse<WindowState>(layout.UrdfWindowState, true, out var urdfState)
                    ? urdfState
                    : WindowState.Normal;

                CommandsListToggle.IsChecked = layout.CommandsVisible;
                CommandsListToggle_Changed(CommandsListToggle, new RoutedEventArgs());

                MovieTimelineToggle.IsChecked = layout.MovieTimelineVisible;
                MovieTimelineToggle_Changed(MovieTimelineToggle, new RoutedEventArgs());

                _embeddedUrdfHeightPixels = layout.EmbeddedUrdfHeightPixels > 0
                    ? layout.EmbeddedUrdfHeightPixels
                    : 0;
                ApplyEmbeddedUrdfHeight();
                SetUrdfUndocked(layout.UrdfUndocked);

                // Never reopen with a transient description editor expanded.
                MovieDescriptionExpandedPanel.Visibility = Visibility.Collapsed;
                MovieDescriptionExpandedRow.Height = new GridLength(0, GridUnitType.Pixel);
                MovieTimelinePanel.Height = MovieTimelineCollapsedHeight;

                if (string.Equals(layout.WindowState, nameof(WindowState.Maximized), StringComparison.OrdinalIgnoreCase))
                    WindowState = WindowState.Maximized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[layout] Could not apply EditorLayout.json: {ex.Message}");
            }
        }

        /// <summary>Save splitter positions, window placement and user-selectable
        /// editor panels into EditorLayout.json in the active Configuration folder.</summary>
        private void SaveEditorLayout()
        {
            if (_folders == null) return;

            try
            {
                Rect bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, ActualWidth, ActualHeight)
                    : RestoreBounds;

                double splineHeight = SplineArea?.Visibility == Visibility.Visible &&
                                      SplineTimelineRow.ActualHeight > 1.0
                    ? SplineTimelineRow.ActualHeight
                    : _lastSplineTimelineHeight.Value;

                if (_head != null)
                {
                    _savedUrdfWindowBounds = _head.GetNormalBounds();
                    _savedUrdfWindowState = _head.WindowState;
                }

                var dockedServoWidth = _urdfUndocked
                    ? _lastDockedServoColumnWidth
                    : ServoEditorColumn.Width;
                var dockedUrdfWidth = _urdfUndocked
                    ? _lastDockedUrdfColumnWidth
                    : UrdfEditorColumn.Width;

                var layout = new EditorLayoutSettings
                {
                    WindowLeft = bounds.Left,
                    WindowTop = bounds.Top,
                    WindowWidth = Math.Max(MinWidth, bounds.Width),
                    WindowHeight = Math.Max(MinHeight, bounds.Height),
                    WindowState = WindowState == WindowState.Maximized
                        ? nameof(WindowState.Maximized)
                        : nameof(WindowState.Normal),
                    ServoEditorColumn = GridLengthSetting.From(dockedServoWidth),
                    UrdfEditorColumn = GridLengthSetting.From(dockedUrdfWidth),
                    UndockedServoLeftColumn = GridLengthSetting.From(UndockedServoLeftColumn.Width),
                    UndockedServoRightColumn = GridLengthSetting.From(UndockedServoRightColumn.Width),
                    TopEditorRow = GridLengthSetting.From(TopEditorRow.Height),
                    AudioTimelineRow = GridLengthSetting.From(AudioTimelineRow.Height),
                    LastSplineTimelineHeight = GridLengthSetting.From(
                        new GridLength(Math.Max(80.0, splineHeight), GridUnitType.Pixel)),
                    CommandsVisible = CommandsListToggle?.IsChecked == true,
                    MovieTimelineVisible = MovieTimelineToggle?.IsChecked == true,
                    EmbeddedUrdfHeightPixels = _embeddedUrdfHeightPixels,
                    UrdfUndocked = _urdfUndocked,
                    UrdfWindowLeft = _savedUrdfWindowBounds.IsEmpty ? 120 : _savedUrdfWindowBounds.Left,
                    UrdfWindowTop = _savedUrdfWindowBounds.IsEmpty ? 120 : _savedUrdfWindowBounds.Top,
                    UrdfWindowWidth = _savedUrdfWindowBounds.IsEmpty ? 900 : _savedUrdfWindowBounds.Width,
                    UrdfWindowHeight = _savedUrdfWindowBounds.IsEmpty ? 650 : _savedUrdfWindowBounds.Height,
                    UrdfWindowState = _savedUrdfWindowState.ToString(),
                };
                layout.Save(_folders.ConfigFolderOrDefault);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[layout] Could not save EditorLayout.json: {ex.Message}");
            }
        }

        /// <summary>Config > Set Paths…: edit + save the configuration folder
        /// location (Paths.json). Confirming re-runs the automatic
        /// ServoConfig.json load from the (possibly new) config folder.</summary>
        private void SetFolders_Click(object sender, RoutedEventArgs e)
        {
            _folders ??= new FolderSettings();
            bool? ok = new SetFoldersWindow(_folders, firstRun: false)
            { Owner = this }.ShowDialog();
            if (ok == true)
            {
                _recentFiles = RecentFilesSettings.Load(_folders.ConfigFolderOrDefault);
                RefreshOpenRecentMenu();
                TryAutoLoadServoConfig();
                TryAutoLoadUrdfConfig();
                ApplyUrdfConfigurationToViews();
                ConfigureUrdfConfigWatcher();
                PushHeadPose();
            }
        }

        /// <summary>
        /// Automatically load ServoConfig.json from the configuration
        /// folder (startup, and after Set Paths). The loaded values go into
        /// the SHARED configuration instance so everything - the grid's
        /// sub-rows, gang directions, connected hardware, and the Servo
        /// Configuration window when opened - reflects and uses them.
        /// </summary>
        private void TryAutoLoadServoConfig()
        {
            string path = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "ServoConfig.json");
            if (!File.Exists(path)) return;

            try
            {
                var loaded = ServoConfiguration.Load(path);
                _servoConfig.Servos = loaded.Servos;
                _servoConfig.GangDirections = loaded.GangDirections;
                _servoConfig.LeftTicSerialNumber = loaded.LeftTicSerialNumber;
                OnServoConfigChanged();
                Debug.WriteLine($"[config] auto-loaded {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not load ServoConfig.json from the configuration " +
                    "folder:\n" + ex.Message,
                    "Servo configuration", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>Automatically load URDFconfig.json from the current
        /// Configuration folder.  If the file does not exist, the built-in
        /// defaults remain active. Once Save Default is used in the URDF
        /// Configuration window, this file is used on every later startup.</summary>
        private void TryAutoLoadUrdfConfig(bool showErrors = true)
        {
            string path = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "URDFconfig.json");
            if (!File.Exists(path))
            {
                _urdfConfig.CopyFrom(UrdfConfiguration.CreateDefault());
                ApplyUrdfConfigurationToViews();
                return;
            }

            try
            {
                _urdfConfig.CopyFrom(UrdfConfiguration.Load(path));
                ApplyUrdfConfigurationToViews();
                Debug.WriteLine($"[config] auto-loaded {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[config] could not load URDFconfig.json: {ex.Message}");
                if (showErrors)
                    MessageBox.Show(this,
                        "Could not load URDFconfig.json from the configuration folder:\n" + ex.Message,
                        "URDF configuration", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
            }
        }

        /// <summary>Watch the selected Configuration folder so edits/saves to
        /// URDFconfig.json outside the calibration window are reflected live.
        /// Save Default also explicitly reloads, so both internal and external
        /// saves converge on the exact file contents.</summary>
        private void ConfigureUrdfConfigWatcher()
        {
            _urdfConfigWatcher?.Dispose();
            _urdfConfigWatcher = null;

            string folder = _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

            _urdfConfigWatcher = new FileSystemWatcher(folder, "URDFconfig.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                               NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            void Changed(object sender, FileSystemEventArgs e) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _urdfConfigReloadTimer.Stop();
                    _urdfConfigReloadTimer.Start();
                }));

            _urdfConfigWatcher.Changed += Changed;
            _urdfConfigWatcher.Created += Changed;
            _urdfConfigWatcher.Renamed += (sender, e) => Changed(sender, e);
        }

        private void FileExit_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Create the detachable URDF window on demand. It is kept
        /// alive while the editor runs so camera/full-screen state can survive
        /// repeated Dock/Undock operations.</summary>
        private RobotHeadWindow EnsureDetachedHeadWindow()
        {
            if (_head != null) return _head;

            _head = new RobotHeadWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            _head.HeadView.CollisionWarningEnabledChanged += HeadView_CollisionWarningEnabledChanged;
            _head.DockRequested += () => SetUrdfUndocked(false);

            if (EmbeddedHeadView != null)
            {
                _head.HeadView.SetCollisionWarningsEnabled(EmbeddedHeadView.CollisionWarningsEnabled);
                _head.HeadView.SetUrdfDriveEnabled(EmbeddedHeadView.UrdfDriveEnabled);
            }
            _head.HeadView.SetUrdfConfiguration(_urdfConfig);
            _head.HeadView.SetServoConfiguration(_servoConfig);

            if (!_savedUrdfWindowBounds.IsEmpty &&
                EditorLayoutSettings.IsVisibleOnVirtualDesktop(
                    _savedUrdfWindowBounds.Left, _savedUrdfWindowBounds.Top,
                    _savedUrdfWindowBounds.Width, _savedUrdfWindowBounds.Height))
            {
                _head.WindowStartupLocation = WindowStartupLocation.Manual;
                _head.ApplyNormalBounds(_savedUrdfWindowBounds);
            }

            return _head;
        }

        private void EmbeddedHeadView_DockToggleRequested() =>
            SetUrdfUndocked(!_urdfUndocked);

        private void EmbeddedHeadView_VerticalResizeDeltaRequested(double delta)
        {
            if (_urdfUndocked || RobotHeadEmbeddedBorder == null || Math.Abs(delta) < 0.01)
                return;

            double current = RobotHeadEmbeddedBorder.ActualHeight > 1
                ? RobotHeadEmbeddedBorder.ActualHeight
                : GetEmbeddedUrdfMinimumHeight();
            _embeddedUrdfHeightPixels = Math.Clamp(
                current + delta,
                GetEmbeddedUrdfMinimumHeight(),
                GetEmbeddedUrdfMaximumHeight());
            ApplyEmbeddedUrdfHeight();
        }

        /// <summary>Switch between the embedded URDF pane and the detachable
        /// window. Undocking expands the servo editor across the full width and
        /// changes it to two section columns, with Lighting & Vents starting the
        /// right column.</summary>
        private void SetUrdfUndocked(bool undocked)
        {
            if (_urdfUndocked == undocked)
            {
                ApplyUrdfDockLayout();
                return;
            }

            if (undocked)
            {
                // The expanded two-column grid should expose Headtop Controls
                // automatically whenever the URDF is detached.
                foreach (var row in _rows.Where(r => r.GroupName == "Headtop Controls"))
                    row.GroupCollapsed = false;

                // Remember the docked splitter ratio before collapsing the URDF
                // columns; restoring later should reproduce the user's layout.
                if (ServoEditorColumn.Width.Value > 0)
                    _lastDockedServoColumnWidth = ServoEditorColumn.Width;
                if (UrdfEditorColumn.Width.Value > 0)
                    _lastDockedUrdfColumnWidth = UrdfEditorColumn.Width;
            }

            _urdfUndocked = undocked;
            ApplyUrdfDockLayout();

            if (undocked)
            {
                var window = EnsureDetachedHeadWindow();
                window.HeadView.SetUrdfConfiguration(_urdfConfig);
                window.HeadView.SetServoConfiguration(_servoConfig);
                if (EmbeddedHeadView != null)
                    window.HeadView.SetUrdfDriveEnabled(EmbeddedHeadView.UrdfDriveEnabled);
                window.HeadView.SetDetachedHostState();
                if (!window.IsVisible)
                    window.Show();
                window.WindowState = _savedUrdfWindowState;
                window.Activate();
                PushHeadPose();
            }
            else if (_head != null)
            {
                _savedUrdfWindowBounds = _head.GetNormalBounds();
                _savedUrdfWindowState = _head.WindowState;
                if (EmbeddedHeadView != null)
                    EmbeddedHeadView.SetUrdfDriveEnabled(_head.HeadView.UrdfDriveEnabled);
                _head.Hide();
                EmbeddedHeadView?.SetUrdfConfiguration(_urdfConfig);
                EmbeddedHeadView?.SetServoConfiguration(_servoConfig);
                PushHeadPose();
            }
        }

        private void ApplyUrdfDockLayout()
        {
            if (ServoGridBorder == null || RobotHeadEmbeddedBorder == null) return;

            if (_urdfUndocked)
            {
                RobotHeadEmbeddedBorder.Visibility = Visibility.Collapsed;
                RobotHeadEmbeddedBorder.Height = double.NaN;
                UrdfColumnSplitter.Visibility = Visibility.Collapsed;
                UrdfSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
                UrdfEditorColumn.MinWidth = 0;
                UrdfEditorColumn.Width = new GridLength(0, GridUnitType.Pixel);
                ServoEditorColumn.Width = new GridLength(1, GridUnitType.Star);
                Grid.SetColumnSpan(ServoGridBorder, 3);
                ServoGridBorder.Margin = new Thickness(6, 5, 6, 0);
                Grid.SetColumnSpan(DescriptionOverlay, 3);
                DescriptionOverlay.Margin = new Thickness(6, 5, 6, 0);
                DockedServoGridScroll.Visibility = Visibility.Collapsed;
                UndockedServoGridColumns.Visibility = Visibility.Visible;
            }
            else
            {
                Grid.SetColumnSpan(ServoGridBorder, 1);
                ServoGridBorder.Margin = new Thickness(6, 5, 0, 0);
                Grid.SetColumnSpan(DescriptionOverlay, 1);
                DescriptionOverlay.Margin = new Thickness(6, 5, 0, 0);
                DockedServoGridScroll.Visibility = Visibility.Visible;
                UndockedServoGridColumns.Visibility = Visibility.Collapsed;
                UrdfSplitterColumn.Width = new GridLength(8, GridUnitType.Pixel);
                UrdfEditorColumn.MinWidth = 260;
                ServoEditorColumn.Width = _lastDockedServoColumnWidth;
                UrdfEditorColumn.Width = _lastDockedUrdfColumnWidth;
                UrdfColumnSplitter.Visibility = Visibility.Visible;
                RobotHeadEmbeddedBorder.Visibility = Visibility.Visible;
                ApplyEmbeddedUrdfHeight();
                EmbeddedHeadView?.SetDockedHostState();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(this,
                $"{AppDisplayName}\nVersion {AppVersion}\nDesigned by Mark Kovalcson\n\n" +
                "Edits servo animation timelines against an audio waveform.\n" +
                "Cubic Hermite spline interpolation, live drive, an animation\n" +
                "library, and a movie timeline for ordered sequence projects.\n\n" +
                "Built with WPF, SkiaSharp and NAudio.",
                $"About {AppDisplayName}", MessageBoxButton.OK, MessageBoxImage.Information);


        private void HelpControls_Click(object sender, RoutedEventArgs e)
        {
            new ControlsHelpWindow { Owner = this }.ShowDialog();
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not string themeName)
                return;

            ThemeManager.Apply(themeName);
            UpdateThemeMenuChecks();

            // Live Drive sets a local brush while toggled, so refresh its OFF
            // chrome explicitly after a palette switch. The ON state remains
            // semantic green regardless of the selected theme.
            if (!LiveDrive && LiveDriveBtn != null)
            {
                LiveDriveBtn.Background = new System.Windows.Media.SolidColorBrush(
                    ThemeManager.GetColor("ControlBackground", System.Windows.Media.Color.FromRgb(48, 53, 61)));
                LiveDriveBtn.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    ThemeManager.GetColor("ControlBorder", System.Windows.Media.Color.FromRgb(74, 81, 96)));
            }

            // Custom-drawn timeline/spline surfaces are not ordinary WPF
            // controls, so explicitly repaint them after a palette change.
            Waveform?.InvalidateVisual();
            Spline?.InvalidateVisual();
            MovieTimeline?.InvalidateVisual();
            ForEachHeadView(v => v.InvalidateVisual());

            ShowStatus($"Color theme: {themeName}");
        }

        private void UpdateThemeMenuChecks()
        {
            if (ThemeGraphiteMenuItem == null) return;
            ThemeGraphiteMenuItem.IsChecked =
                ThemeManager.CurrentTheme.Equals("Graphite", StringComparison.OrdinalIgnoreCase);
            ThemeSteelBlueMenuItem.IsChecked =
                ThemeManager.CurrentTheme.Equals("Steel Blue", StringComparison.OrdinalIgnoreCase);
            ThemeTealMenuItem.IsChecked =
                ThemeManager.CurrentTheme.Equals("Teal", StringComparison.OrdinalIgnoreCase);
            ThemeVioletMenuItem.IsChecked =
                ThemeManager.CurrentTheme.Equals("Violet", StringComparison.OrdinalIgnoreCase);
        }

        private void ViewMovieTimeline_Click(object sender, RoutedEventArgs e)
        {
            MovieTimelineToggle.IsChecked = ViewMovieTimelineMenuItem.IsChecked;
        }

        #endregion

        // ================================================================
        #region 10. Movie timeline
        // ================================================================

        private void MovieTimelineToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (MovieTimelinePanel == null) return;
            bool show = MovieTimelineToggle.IsChecked == true;
            MovieTimelinePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (ViewMovieTimelineMenuItem != null) ViewMovieTimelineMenuItem.IsChecked = show;
            if (show) SyncMovieScroll();
        }

        private void CommandsListToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (CommandsAtPointPanel == null || CommandsListToggle == null) return;
            bool show = CommandsListToggle.IsChecked == true;
            CommandsAtPointPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CommandsListToggle.Content = show ? "Hide Commands" : "Show Commands";
        }

        private double GetEmbeddedUrdfMinimumHeight()
        {
            double actual = TopEditorRow?.ActualHeight ?? 0;
            if (actual > 1) return actual;
            if (TopEditorRow != null && TopEditorRow.Height.IsAbsolute)
                return Math.Max(80.0, TopEditorRow.Height.Value);
            return 250.0;
        }

        private double GetEmbeddedUrdfMaximumHeight()
        {
            if (EditorTimelineGrid == null) return GetEmbeddedUrdfMinimumHeight();
            double total = 0;
            // The URDF may extend through rows 0..4 (editor, audio and spline),
            // but never over the Commands row at index 5.
            for (int i = 0; i < Math.Min(5, EditorTimelineGrid.RowDefinitions.Count); i++)
                total += EditorTimelineGrid.RowDefinitions[i].ActualHeight;
            return Math.Max(GetEmbeddedUrdfMinimumHeight(), total);
        }

        private void ApplyEmbeddedUrdfHeight()
        {
            if (RobotHeadEmbeddedBorder == null) return;

            if (_urdfUndocked)
            {
                Grid.SetRowSpan(RobotHeadEmbeddedBorder, 1);
                RobotHeadEmbeddedBorder.Height = double.NaN;
                return;
            }

            Grid.SetRowSpan(RobotHeadEmbeddedBorder, 5);
            RobotHeadEmbeddedBorder.VerticalAlignment = VerticalAlignment.Top;
            double min = GetEmbeddedUrdfMinimumHeight();
            double max = GetEmbeddedUrdfMaximumHeight();
            double target = _embeddedUrdfHeightPixels > 0 ? _embeddedUrdfHeightPixels : min;
            target = Math.Clamp(target, min, max);
            if (_embeddedUrdfHeightPixels > 0)
                _embeddedUrdfHeightPixels = target;
            RobotHeadEmbeddedBorder.Height = target;
            EmbeddedHeadView?.SetDockedHostState();
        }

        private void SetMovieDescriptionText(string text)
        {
            _syncingMovieDescription = true;
            _movieDescription = text ?? "";
            if (MovieDescriptionBox != null) MovieDescriptionBox.Text = _movieDescription;
            if (MovieDescriptionExpandedBox != null) MovieDescriptionExpandedBox.Text = _movieDescription;
            _syncingMovieDescription = false;
        }

        private void MovieDescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingMovieDescription) return;
            _syncingMovieDescription = true;
            _movieDescription = MovieDescriptionBox?.Text ?? "";
            if (MovieDescriptionExpandedBox != null && MovieDescriptionExpandedBox.Text != _movieDescription)
                MovieDescriptionExpandedBox.Text = _movieDescription;
            _syncingMovieDescription = false;
            UpdateDocumentStatusIndicators();
        }

        private void MovieDescriptionExpandedBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingMovieDescription) return;
            _syncingMovieDescription = true;
            _movieDescription = MovieDescriptionExpandedBox?.Text ?? "";
            if (MovieDescriptionBox != null && MovieDescriptionBox.Text != _movieDescription)
                MovieDescriptionBox.Text = _movieDescription;
            _syncingMovieDescription = false;
            UpdateDocumentStatusIndicators();
        }

        private const double MovieTimelineCollapsedHeight = 132.0;
        private const double MovieDescriptionExpandedHeight = 165.0;

        private void MovieDescriptionExpand_Click(object sender, RoutedEventArgs e)
        {
            SetMovieDescriptionText(MovieDescriptionBox?.Text);
            MovieDescriptionExpandedPanel.Visibility = Visibility.Visible;
            MovieDescriptionExpandedRow.Height = new GridLength(MovieDescriptionExpandedHeight, GridUnitType.Pixel);
            MovieTimelinePanel.Height = MovieTimelineCollapsedHeight + MovieDescriptionExpandedHeight;
            MovieDescriptionExpandedBox.Focus();
            MovieDescriptionExpandedBox.CaretIndex = MovieDescriptionExpandedBox.Text.Length;
        }

        private void MovieDescriptionCollapse_Click(object sender, RoutedEventArgs e)
        {
            MovieDescriptionExpandedPanel.Visibility = Visibility.Collapsed;
            MovieDescriptionExpandedRow.Height = new GridLength(0, GridUnitType.Pixel);
            MovieTimelinePanel.Height = MovieTimelineCollapsedHeight;
            MovieDescriptionBox.Focus();
            MovieDescriptionBox.CaretIndex = MovieDescriptionBox.Text.Length;
        }

        private void RefreshMovieMetadataView()
        {
            if (MovieCreatedText != null)
                MovieCreatedText.Text = $"Created: {_movieCreatedDate}";
        }

        /// <summary>A stable representation of every editable sequence value.
        /// It intentionally includes UI-only project settings that are copied
        /// into the JSON only at save time, such as spline checkboxes.</summary>
        private string CurrentSequenceFingerprint()
        {
            var sb = new StringBuilder();
            sb.AppendLine(DescriptionBox?.Text ?? _doc?.Description ?? "");
            sb.AppendLine(_audioPath ?? "");
            sb.AppendLine(_audioOffset.ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine(_splineHz.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(_doc?.AnimateMode ?? "");
            sb.AppendLine((_doc?.ScaleValues ?? false) ? "1" : "0");

            foreach (var row in _rows.Where(r => r.SplineEnabled).OrderBy(r => r.Servo))
                sb.Append("S:").Append(row.Servo).AppendLine();

            if (_doc?.Commands != null)
            {
                foreach (var c in _doc.Commands
                    .OrderBy(c => c.OffsetSeconds)
                    .ThenBy(c => c.Servo)
                    .ThenBy(c => c.Control?.ToString() ?? ""))
                {
                    sb.Append(c.OffsetSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                      .Append(c.Servo).Append('|')
                      .Append(c.Control?.ToString() ?? "").Append('|')
                      .Append(c.NumericValue).Append('|')
                      .Append(c.TextValue ?? "").Append('|')
                      .Append(c.Disable ? '1' : '0').Append('|')
                      .Append(c.Speed).Append('|')
                      .Append(c.ColorHex ?? "").Append('|')
                      .Append(c.Reason ?? "").AppendLine();
                }
            }
            return sb.ToString();
        }

        private bool SequenceHasUnsavedChanges() =>
            !string.IsNullOrEmpty(_savedSequenceFingerprint) &&
            !string.Equals(_savedSequenceFingerprint, CurrentSequenceFingerprint(),
                           StringComparison.Ordinal);

        /// <summary>Called before the movie editor reloads another sequence.
        /// Yes saves the existing file, No discards by allowing the reload,
        /// and Cancel leaves the current editor untouched.</summary>
        private bool ConfirmSequenceSwitch()
        {
            if (!SequenceHasUnsavedChanges()) return true;

            var answer = MessageBox.Show(this,
                "The current sequence has unsaved changes.\n\n" +
                "Save the changes before selecting another sequence?",
                "Unsaved sequence changes",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.No)
            {
                RefreshMovieDurationForPath(_jsonPath);
                return true;
            }
            return !string.IsNullOrWhiteSpace(_jsonPath)
                ? SaveProjectTo(_jsonPath)
                : SaveProjectAsInteractive();
        }

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                                     Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        }

        private string ResolveSequenceAudioPath(AnimationDocument doc, string sequencePath,
                                                string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (File.Exists(stored)) return stored;

            string seqDir = Path.GetDirectoryName(sequencePath) ?? "";
            string p = Path.Combine(seqDir, Path.GetFileName(stored));
            if (File.Exists(p)) return p;

            p = Path.Combine(_folders?.ProjectFolderOrDefault ?? "", Path.GetFileName(stored));
            if (File.Exists(p)) return p;
            return null;
        }

        /// <summary>Determine the actual sequence length for a movie block.
        /// The saved duration and last command are considered, and available
        /// primary/Play audio files extend the block to their real end.</summary>
        private double SequenceDurationFromPath(string path)
        {
            try
            {
                var doc = AnimationDocument.Load(path);
                double d = Math.Max(0, doc.DurationSeconds);
                if (doc.Commands != null && doc.Commands.Count > 0)
                    d = Math.Max(d, doc.Commands.Max(c => c.OffsetSeconds));

                string primary = ResolveSequenceAudioPath(doc, path,
                    !string.IsNullOrWhiteSpace(doc.AudioFilePath) ? doc.AudioFilePath : doc.AudioFile);
                if (primary != null)
                {
                    using var r = new AudioFileReader(primary);
                    d = Math.Max(d, Math.Max(0, doc.AudioStartOffsetSeconds) + r.TotalTime.TotalSeconds);
                }

                foreach (var c in doc.Commands?.Where(c => c.Servo == ServoNames.Play)
                                              ?? Enumerable.Empty<ServoCommand>())
                {
                    string clip = ResolveSequenceAudioPath(doc, path, c.TextValue);
                    if (clip == null) continue;
                    using var r = new AudioFileReader(clip);
                    d = Math.Max(d, c.OffsetSeconds + r.TotalTime.TotalSeconds);
                }
                return Math.Max(0.05, d);
            }
            catch { return 1.0; }
        }

        private void RefreshMovieDurationForPath(string path)
        {
            bool changed = false;
            foreach (var item in _movieItems)
            {
                if (!PathsEqual(item.FilePath, path)) continue;
                item.DurationSeconds = SequenceDurationFromPath(path);
                changed = true;
            }
            if (changed) RefreshMovieTimelineView();
        }

        private void RefreshMovieTimelineView()
        {
            MovieTimeline.SetItems(_movieItems);
            MovieTimeline.SelectedIndex = _movieSelectedIndex;
            MovieTimeline.CursorTime = Math.Clamp(MovieTimeline.CursorTime, 0, MovieTimeline.TotalDuration);
            MovieTimeline.EnsureVisible(MovieTimeline.CursorTime);
            MovieTimeline.InvalidateVisual();
            SyncMovieScroll();
            UpdateDocumentStatusIndicators();
        }

        private string MovieBlockToolTip(MovieSequenceItem item)
        {
            if (item == null) return "";
            string description = "";
            string audio = "";
            try
            {
                var doc = AnimationDocument.Load(item.FilePath);
                description = doc.Description ?? "";
                var names = new List<string>();
                if (!string.IsNullOrWhiteSpace(doc.AudioFile)) names.Add(Path.GetFileName(doc.AudioFile));
                names.AddRange((doc.Commands ?? new List<ServoCommand>())
                    .Where(c => c.Servo == ServoNames.Play)
                    .Select(c => Path.GetFileName(c.TextValue ?? ""))
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
                audio = string.Join(", ", names.Distinct(StringComparer.OrdinalIgnoreCase));
            }
            catch { }
            string tip = $"{Path.GetFileName(item.FilePath)}\nDuration: {item.DurationSeconds:0.###} s";
            if (!string.IsNullOrWhiteSpace(description)) tip += $"\n{description}";
            if (!string.IsNullOrWhiteSpace(audio)) tip += $"\nAudio: {audio}";
            if (!string.IsNullOrWhiteSpace(_jsonPath) && PathsEqual(_jsonPath, item.FilePath))
                tip += SequenceHasUnsavedChanges() ? "\nCurrent sequence — unsaved edits" : "\nCurrent sequence";
            tip += $"\n{item.FilePath}";
            return tip;
        }

        private void SyncMovieScroll()
        {
            if (MovieScroll == null || MovieTimeline == null) return;
            double visible = Math.Max(0.001, MovieTimeline.VisibleSeconds);
            MovieScroll.Minimum = 0;
            MovieScroll.Maximum = Math.Max(0, MovieTimeline.TotalDuration - visible);
            MovieScroll.ViewportSize = visible;
            MovieScroll.LargeChange = visible * 0.9;
            MovieScroll.SmallChange = visible * 0.1;
            MovieScroll.Value = Math.Clamp(MovieTimeline.ViewStart, MovieScroll.Minimum, MovieScroll.Maximum);
        }

        private void MovieScroll_Scroll(object sender, ScrollEventArgs e) => MovieTimeline.PanTo(e.NewValue);
        private void MovieZoomIn_Click(object sender, RoutedEventArgs e) => MovieTimeline.ZoomBy(1.5);
        private void MovieZoomOut_Click(object sender, RoutedEventArgs e) => MovieTimeline.ZoomBy(1 / 1.5);
        private void MovieZoomFit_Click(object sender, RoutedEventArgs e) => MovieTimeline.ZoomToFit();

        private string MovieStoredSequencePath(string fullPath)
        {
            try
            {
                string root = Path.GetFullPath(ProjectsFolder())
                                  .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(fullPath);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return Path.GetRelativePath(ProjectsFolder(), full);
            }
            catch { }
            return fullPath;
        }

        private string ResolveMovieSequencePath(string stored, string moviePath)
        {
            if (string.IsNullOrWhiteSpace(stored)) return stored;
            if (Path.IsPathRooted(stored)) return stored;

            string p = Path.Combine(ProjectsFolder(), stored);
            if (File.Exists(p)) return p;

            p = Path.Combine(Path.GetDirectoryName(moviePath) ?? ProjectsFolder(), stored);
            return p;
        }

        private void LoadMovie_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Load movie JSON",
                Filter = "Movie JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = ProjectsFolder(),
            };
            if (dlg.ShowDialog() != true) return;
            LoadMovieFromPath(dlg.FileName, alreadyConfirmed: false);
        }

        /// <summary>Load a movie from a known pathname. Used by File > Load
        /// Movie, File > Open Recent and startup document restoration.</summary>
        private bool LoadMovieFromPath(string moviePath, bool alreadyConfirmed)
        {
            try
            {
                var movie = MovieDocument.Load(moviePath);
                var loaded = new List<MovieSequenceItem>();
                var missing = new List<string>();
                foreach (string stored in movie.Sequences)
                {
                    string path = ResolveMovieSequencePath(stored, moviePath);
                    if (!File.Exists(path)) missing.Add(stored);
                    loaded.Add(new MovieSequenceItem
                    {
                        FilePath = path,
                        DurationSeconds = File.Exists(path) ? SequenceDurationFromPath(path) : 1.0,
                    });
                }

                if (!alreadyConfirmed && !ConfirmSequenceSwitch()) return false;

                StopPlayback();
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                _movieItems.Clear();
                _movieItems.AddRange(loaded);
                _moviePath = moviePath;
                _activeDocumentKind = ActiveDocumentKind.Movie;
                _movieDescription = movie.Description ?? "";
                _movieCreatedDate = string.IsNullOrWhiteSpace(movie.CreatedDate)
                    ? DateTime.Today.ToString("yyyy-MM-dd") : movie.CreatedDate;
                SetMovieDescriptionText(_movieDescription);
                RefreshMovieMetadataView();
                _movieSelectedIndex = -1;
                MovieTimeline.CursorTime = 0;
                MovieTimelineToggle.IsChecked = true;
                _savedMovieFingerprint = CurrentMovieFingerprint();
                RefreshMovieTimelineView();
                RecordRecentFile(moviePath, ActiveDocumentKind.Movie, setActive: true);
                ShowStatus($"Movie loaded: {Path.GetFileName(_moviePath)}");

                if (missing.Count > 0)
                    MessageBox.Show(this,
                        "These sequence files referenced by the movie were not found:\n\n  • " +
                        string.Join("\n  • ", missing),
                        "Missing movie sequences", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (_movieItems.Count > 0 && File.Exists(_movieItems[0].FilePath))
                    SelectMovieSequence(0, 0, alreadyConfirmed: true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load movie:\n" + ex.Message,
                    "Load movie", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>File > Save Movie: save edits back to the currently loaded
        /// movie file. A movie that has never been saved falls through to Save As.</summary>
        private void SaveMovie_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_moviePath))
            {
                SaveMovieAs_Click(sender, e);
                return;
            }

            SaveMovieToPath(_moviePath);
        }

        private void SaveMovieAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save movie JSON",
                Filter = "Movie JSON files (*.json)|*.json",
                InitialDirectory = ProjectsFolder(),
                FileName = string.IsNullOrWhiteSpace(_moviePath)
                    ? "movie.json" : Path.GetFileName(_moviePath),
            };
            if (dlg.ShowDialog() != true) return;

            // Save As keeps movie projects in the configured Projects folder,
            // matching the application's existing project organization.
            string savePath = Path.Combine(ProjectsFolder(), Path.GetFileName(dlg.FileName));
            SaveMovieToPath(savePath);
        }

        /// <summary>Write the complete in-memory movie state to one pathname.
        /// This is shared by Save Movie and Save Movie As so reordered/inserted/
        /// removed sequences and edited description text are all persisted.</summary>
        private bool SaveMovieToPath(string savePath)
        {
            try
            {
                _movieDescription = MovieDescriptionBox?.Text ?? _movieDescription ?? "";
                if (string.IsNullOrWhiteSpace(_movieCreatedDate))
                    _movieCreatedDate = DateTime.Today.ToString("yyyy-MM-dd");

                var movie = new MovieDocument
                {
                    Description = _movieDescription,
                    CreatedDate = _movieCreatedDate,
                    Sequences = _movieItems.Select(i => MovieStoredSequencePath(i.FilePath)).ToList(),
                };

                movie.Save(savePath);
                _moviePath = savePath;
                _activeDocumentKind = ActiveDocumentKind.Movie;
                RecordRecentFile(savePath, ActiveDocumentKind.Movie, setActive: true);
                _savedMovieFingerprint = CurrentMovieFingerprint();
                RefreshMovieTimelineView();
                UpdateDocumentStatusIndicators();
                ShowStatus($"Movie saved: {Path.GetFileName(savePath)}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save movie:\n" + ex.Message,
                    "Save movie", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void MovieTimeline_InsertRequested(int boundaryIndex)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Insert sequence into movie",
                Filter = "Sequence JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = SequenceDialogFolder(),
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Parse now so a non-sequence JSON produces an immediate error.
                AnimationDocument.Load(dlg.FileName);
                if (!ConfirmSequenceSwitch()) return;
                var item = new MovieSequenceItem
                {
                    FilePath = dlg.FileName,
                    DurationSeconds = SequenceDurationFromPath(dlg.FileName),
                };
                boundaryIndex = Math.Clamp(boundaryIndex, 0, _movieItems.Count);
                _movieItems.Insert(boundaryIndex, item);
                _movieSelectedIndex = -1;
                MovieTimeline.CursorTime = MovieTimeline.StartOf(boundaryIndex);
                RefreshMovieTimelineView();
                SelectMovieSequence(boundaryIndex, 0, alreadyConfirmed: true);
                ShowStatus($"Inserted {Path.GetFileNameWithoutExtension(dlg.FileName)} into movie");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not insert sequence:\n" + ex.Message,
                    "Insert sequence", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MovieTimeline_RemoveRequested(int index)
        {
            if (index < 0 || index >= _movieItems.Count) return;
            string name = Path.GetFileNameWithoutExtension(_movieItems[index].FilePath);
            if (MessageBox.Show(this, $"Remove '{name}' from the movie?",
                    "Remove movie sequence", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            bool removingCurrent = index == _movieSelectedIndex;
            if (removingCurrent && !ConfirmSequenceSwitch()) return;

            _movieItems.RemoveAt(index);
            if (_movieSelectedIndex > index) _movieSelectedIndex--;
            else if (removingCurrent) _movieSelectedIndex = -1;

            MovieTimeline.CursorTime = MovieTimeline.StartOf(Math.Min(index, _movieItems.Count));
            RefreshMovieTimelineView();

            if (removingCurrent && _movieItems.Count > 0)
            {
                int next = Math.Min(index, _movieItems.Count - 1);
                SelectMovieSequence(next, 0, alreadyConfirmed: true);
            }
            ShowStatus($"Removed {name} from movie");
        }

        private void MovieTimeline_ReorderRequested(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _movieItems.Count ||
                toIndex < 0 || toIndex >= _movieItems.Count || fromIndex == toIndex) return;

            var moving = _movieItems[fromIndex];
            _movieItems.RemoveAt(fromIndex);
            _movieItems.Insert(toIndex, moving);

            if (_movieSelectedIndex == fromIndex) _movieSelectedIndex = toIndex;
            else if (fromIndex < _movieSelectedIndex && toIndex >= _movieSelectedIndex) _movieSelectedIndex--;
            else if (fromIndex > _movieSelectedIndex && toIndex <= _movieSelectedIndex) _movieSelectedIndex++;

            int cursorIndex = _movieSelectedIndex >= 0 ? _movieSelectedIndex : toIndex;
            RefreshMovieTimelineView();
            MovieTimeline.CursorTime = MovieTimeline.StartOf(cursorIndex);
            MovieTimeline.InvalidateVisual();
            ShowStatus("Movie sequence order changed");
        }

        private void MovieTimeline_CursorRequested(double movieTime, int index)
        {
            if (index < 0 || index >= _movieItems.Count) return;
            double fallbackMovieTime = _movieSelectedIndex >= 0 && _movieSelectedIndex < _movieItems.Count
                ? MovieTimeline.StartOf(_movieSelectedIndex) + Math.Min(_cursorTime, _movieItems[_movieSelectedIndex].DurationSeconds)
                : 0;
            double local = Math.Clamp(movieTime - MovieTimeline.StartOf(index),
                                      0, _movieItems[index].DurationSeconds);
            if (!SelectMovieSequence(index, local))
            {
                MovieTimeline.CursorTime = fallbackMovieTime;
                MovieTimeline.SelectedIndex = _movieSelectedIndex;
                MovieTimeline.InvalidateVisual();
                return;
            }
            MovieTimeline.CursorTime = movieTime;
            MovieTimeline.InvalidateVisual();
        }

        private bool SelectMovieSequence(int index, double localTime,
                                         bool alreadyConfirmed = false)
        {
            if (index < 0 || index >= _movieItems.Count) return false;
            string path = _movieItems[index].FilePath;
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "Sequence file not found:\n" + path,
                    "Movie sequence", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!alreadyConfirmed && !ConfirmSequenceSwitch()) return false;

            StopPlayback();
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            MoviePlayButton.Content = "▶ Movie";

            if (!LoadSequenceFromPath(path, localTime, fitTimeline: true,
                                      recordRecent: false, setActiveDocument: false)) return false;

            _movieSelectedIndex = index;
            _movieItems[index].DurationSeconds = SequenceDurationFromPath(path);
            MovieTimeline.SelectedIndex = index;
            MovieTimeline.CursorTime = MovieTimeline.StartOf(index) +
                                       Math.Clamp(localTime, 0, _movieItems[index].DurationSeconds);
            RefreshMovieTimelineView();
            return true;
        }

        private void MoviePlay_Click(object sender, RoutedEventArgs e)
        {
            if (_movieItems.Count == 0)
            {
                MessageBox.Show(this, "Insert or load at least one sequence first.",
                    "Movie timeline", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_moviePlaybackActive && IsRunning)
            {
                PausePlayback();
                MoviePlayButton.Content = "▶ Resume";
                return;
            }
            if (_moviePlaybackActive && _mode == PlayMode.Paused &&
                _moviePlaybackIndex >= 0)
            {
                StartPlaybackAt(_cursorTime);
                MoviePlayButton.Content = "❚❚ Pause";
                return;
            }

            int index = MovieTimeline.IndexAtTime(MovieTimeline.CursorTime);
            if (index < 0) return;
            double local = Math.Clamp(MovieTimeline.CursorTime - MovieTimeline.StartOf(index),
                                      0, _movieItems[index].DurationSeconds);

            bool currentBlockAlreadyLoaded = index == _movieSelectedIndex &&
                !string.IsNullOrWhiteSpace(_jsonPath) &&
                PathsEqual(_movieItems[index].FilePath, _jsonPath);
            if (currentBlockAlreadyLoaded)
            {
                // Play the editor's live state, including unsaved edits. A
                // disk reload belongs to selection, not to the Play button.
                _movieItems[index].DurationSeconds = Math.Max(0.05, ContentEnd);
                MovieTimeline.CursorTime = MovieTimeline.StartOf(index) +
                                           Math.Min(local, _movieItems[index].DurationSeconds);
                SetCursor(Math.Min(local, ContentEnd));
                RefreshMovieTimelineView();
            }
            else if (!SelectMovieSequence(index, local)) return;

            _moviePlaybackActive = true;
            _moviePlaybackIndex = index;
            MoviePlayButton.Content = "❚❚ Pause";
            StartPlaybackAt(local);
        }

        private void MovieNext_Click(object sender, RoutedEventArgs e)
        {
            if (_movieItems.Count == 0) return;
            int current = _movieSelectedIndex >= 0
                ? _movieSelectedIndex
                : MovieTimeline.IndexAtTime(MovieTimeline.CursorTime, boundaryChoosesNext: false);
            int next = Math.Clamp(current + 1, 0, _movieItems.Count - 1);
            if (current >= _movieItems.Count - 1) return;

            double start = MovieTimeline.StartOf(next);
            MovieTimeline.CursorTime = start;
            if (!SelectMovieSequence(next, 0)) return;

            _moviePlaybackActive = true;
            _moviePlaybackIndex = next;
            MoviePlayButton.Content = "❚❚ Pause";
            StartPlaybackAt(0);
        }

        #endregion

        // ================================================================
        #region 11. Animation Library
        // ================================================================

        /// <summary>
        /// Begin a library-range selection. The movable modeless window keeps
        /// the timeline active so the green/red arrows can be dragged while a
        /// ten-line description is visible and editable.
        /// </summary>
        private void LibraryCreate_Click(object sender, RoutedEventArgs e)
        {
            EndArrowPrompt();
            _libraryPrompt = LibraryPrompt.CreateItem;
            Waveform.BeginRangeSelect();

            _libraryRangeWindow = new LibraryRangePromptWindow(
                _doc.Description,
                description =>
                {
                    try { CreateLibraryItem(description); }
                    finally { EndArrowPrompt(); }
                },
                EndArrowPrompt)
            {
                Owner = this,
            };
            _libraryRangeWindow.Show();
        }

        /// <summary>
        /// Select the library item first. Once selected, display a blue arrow
        /// centered in the visible timeline. A right-click on the waveform
        /// asks for final confirmation and shows the selected description.
        /// </summary>
        private void LibraryInsert_Click(object sender, RoutedEventArgs e)
        {
            EndArrowPrompt();

            var win = new LibraryItemSelectionWindow(LibraryFolder(), manageMode: false)
            {
                Owner = this,
            };
            if (win.ShowDialog() != true || win.SelectedLibraryItem == null)
                return;

            _pendingLibraryItemPath = win.SelectedLibraryItem.FullPath;
            _pendingLibraryItemDescription = win.SelectedLibraryItem.Description ?? "";
            _libraryPrompt = LibraryPrompt.InsertSequence;
            Waveform.BeginInsertSelect();
        }

        /// <summary>Open the same recursive library browser in management
        /// mode, where the selected item's description can be edited.</summary>
        private void LibraryManage_Click(object sender, RoutedEventArgs e)
        {
            EndArrowPrompt();
            new LibraryItemSelectionWindow(LibraryFolder(), manageMode: true)
            {
                Owner = this,
            }.ShowDialog();
        }

        /// <summary>
        /// Window-level shortcuts. Escape abandons either active library-arrow
        /// operation. Movie transport/navigation shortcuts:
        ///   Up    = play/pause/resume the current movie sequence
        ///   Right = load and play the next sequence
        ///   Left  = stop and return to the start of the current sequence; if
        ///           already at its start, load the previous sequence at t=0
        ///   Down  = stop and move to the beginning of the movie
        /// Arrow shortcuts are deliberately not intercepted while the user is
        /// editing text or a numeric text field.
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _libraryPrompt != LibraryPrompt.None)
            {
                EndArrowPrompt();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers != ModifierKeys.None || IsTextEditingControl(Keyboard.FocusedElement))
                return;

            if (e.Key == Key.Up)
            {
                MoviePlay_Click(MoviePlayButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                MovieNext_Click(MovieNextButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                MoviePreviousOrRestart();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                MovieGoToBeginning();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Left-arrow movie navigation. Playback always stops. If the movie
        /// cursor is anywhere after the selected sequence's start (including
        /// while it is actively playing or paused), return to that sequence's
        /// t=0 without reloading it. If already at the boundary, select the
        /// previous sequence at t=0.
        /// </summary>
        private void MoviePreviousOrRestart()
        {
            if (_movieItems.Count == 0) return;

            int index = _movieSelectedIndex >= 0
                ? _movieSelectedIndex
                : MovieTimeline.IndexAtTime(MovieTimeline.CursorTime, boundaryChoosesNext: false);
            if (index < 0 || index >= _movieItems.Count) return;

            double start = MovieTimeline.StartOf(index);
            double local = Math.Max(0, MovieTimeline.CursorTime - start);
            bool movieTransportActive = _moviePlaybackActive &&
                                        (_mode == PlayMode.Running || _mode == PlayMode.Paused);
            bool atStart = !movieTransportActive &&
                           local <= 0.001 && _cursorTime <= 0.001;

            StopPlayback();
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            MoviePlayButton.Content = "▶ Movie";

            if (!atStart)
            {
                // Stay in the live editor state (including unsaved edits); only
                // reposition both cursors to the beginning of the current block.
                SetCursor(0);
                MovieTimeline.SelectedIndex = index;
                MovieTimeline.CursorTime = start;
                MovieTimeline.InvalidateVisual();
                return;
            }

            if (index == 0)
            {
                SetCursor(0);
                MovieTimeline.SelectedIndex = 0;
                MovieTimeline.CursorTime = 0;
                MovieTimeline.InvalidateVisual();
                return;
            }

            int previous = index - 1;
            double previousStart = MovieTimeline.StartOf(previous);
            MovieTimeline.CursorTime = previousStart;
            if (!SelectMovieSequence(previous, 0))
            {
                MovieTimeline.SelectedIndex = index;
                MovieTimeline.CursorTime = start;
                MovieTimeline.InvalidateVisual();
                return;
            }

            MovieTimeline.SelectedIndex = previous;
            MovieTimeline.CursorTime = previousStart;
            MovieTimeline.InvalidateVisual();
        }

        /// <summary>Down-arrow movie navigation: stop playback and move to the
        /// first sequence at t=0. Switching from another sequence uses the same
        /// save/discard/cancel protection as mouse selection.</summary>
        private void MovieGoToBeginning()
        {
            if (_movieItems.Count == 0) return;

            StopPlayback();
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            MoviePlayButton.Content = "▶ Movie";

            if (_movieSelectedIndex == 0)
            {
                SetCursor(0);
                MovieTimeline.SelectedIndex = 0;
                MovieTimeline.CursorTime = 0;
                MovieTimeline.InvalidateVisual();
                return;
            }

            double oldMovieTime = MovieTimeline.CursorTime;
            int oldIndex = _movieSelectedIndex;
            MovieTimeline.CursorTime = 0;
            if (!SelectMovieSequence(0, 0))
            {
                MovieTimeline.SelectedIndex = oldIndex;
                MovieTimeline.CursorTime = oldMovieTime;
                MovieTimeline.InvalidateVisual();
                return;
            }

            MovieTimeline.SelectedIndex = 0;
            MovieTimeline.CursorTime = 0;
            MovieTimeline.InvalidateVisual();
        }

        private static bool IsTextEditingControl(IInputElement focused)
        {
            return focused is TextBox || focused is PasswordBox || focused is ComboBox;
        }

        /// <summary>Hide arrows, close the movable create prompt, and clear
        /// any pending selected item.</summary>
        private void EndArrowPrompt()
        {
            if (_endingLibraryOperation) return;
            _endingLibraryOperation = true;
            try
            {
                var prompt = _libraryRangeWindow;
                _libraryRangeWindow = null;

                Waveform.EndArrowMode();
                _libraryPrompt = LibraryPrompt.None;
                _pendingLibraryItemPath = null;
                _pendingLibraryItemDescription = null;

                if (prompt?.IsVisible == true)
                    prompt.Close();
            }
            finally
            {
                _endingLibraryOperation = false;
            }
        }

        /// <summary>Library animation items live in Library\Animation
        /// inside the configuration folder (created on demand).</summary>
        private string LibraryFolder()
        {
            string dir = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "Library", "Animation");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>Audio referenced by library items is copied into
        /// Library\Audio inside the configuration folder.</summary>
        private string LibraryAudioFolder()
        {
            string dir = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "Library", "Audio");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>Sequence files normally live in the Projects folder
        /// inside the configuration folder (created on demand).</summary>
        private string ProjectsFolder()
        {
            string dir = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "Projects");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>The next sequence Open/Save As dialog starts in the
        /// folder used by the most recent successful sequence load or save.</summary>
        private string SequenceDialogFolder()
        {
            string remembered = _folders?.LastSequenceFolder;
            return !string.IsNullOrWhiteSpace(remembered) && Directory.Exists(remembered)
                ? remembered : ProjectsFolder();
        }

        private void RememberSequencePath(string sequencePath)
        {
            if (_folders == null || string.IsNullOrWhiteSpace(sequencePath)) return;
            string folder = Path.GetDirectoryName(sequencePath);
            if (string.IsNullOrWhiteSpace(folder)) return;

            _folders.LastSequenceFolder = folder;
            try { _folders.Save(); }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not remember the sequence folder: " + ex.Message);
            }
        }

        // ----- Recent files / startup document restore -----

        private void SaveRecentFiles()
        {
            try
            {
                _recentFiles?.Save(_folders?.ConfigFolderOrDefault);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not save RecentFiles.json: " + ex.Message);
            }
        }

        private void RecordRecentFile(string path, ActiveDocumentKind kind, bool setActive)
        {
            if (string.IsNullOrWhiteSpace(path) || kind == ActiveDocumentKind.None) return;
            _recentFiles ??= new RecentFilesSettings();
            _recentFiles.Touch(path,
                kind == ActiveDocumentKind.Movie ? "Movie" : "Sequence",
                setActive);
            SaveRecentFiles();
            RefreshOpenRecentMenu();
        }

        /// <summary>Persist exactly the logical document that was active at
        /// shutdown. A movie remains the active document even while one of its
        /// child sequences is displayed in the sequence editor.</summary>
        private void SaveLastActiveDocument()
        {
            if (_recentFiles == null) return;

            string path = _activeDocumentKind switch
            {
                ActiveDocumentKind.Movie => _moviePath,
                ActiveDocumentKind.Sequence => _jsonPath,
                _ => null,
            };

            if (_activeDocumentKind == ActiveDocumentKind.None || string.IsNullOrWhiteSpace(path))
                _recentFiles.ClearLastActive();
            else
                _recentFiles.Touch(path,
                    _activeDocumentKind == ActiveDocumentKind.Movie ? "Movie" : "Sequence",
                    setActive: true);

            SaveRecentFiles();
        }

        private bool TryRestoreLastDocument()
        {
            if (_recentFiles == null || string.IsNullOrWhiteSpace(_recentFiles.LastActivePath))
                return false;

            string path = _recentFiles.LastActivePath;
            if (!File.Exists(path))
            {
                _recentFiles.ClearLastActive();
                SaveRecentFiles();
                RefreshOpenRecentMenu();
                ShowStatus($"Last document not found: {Path.GetFileName(path)}");
                return false;
            }

            bool isMovie = string.Equals(_recentFiles.LastActiveKind, "Movie",
                                         StringComparison.OrdinalIgnoreCase);
            return isMovie
                ? LoadMovieFromPath(path, alreadyConfirmed: true)
                : LoadSequenceFromPath(path, 0, fitTimeline: true,
                                       recordRecent: true, setActiveDocument: true);
        }

        private void RefreshOpenRecentMenu()
        {
            if (OpenRecentMenuItem == null) return;
            OpenRecentMenuItem.Items.Clear();

            var entries = (_recentFiles?.Files ?? new List<RecentFileEntry>())
                .OrderByDescending(e => e.LastOpenedUtc)
                .Take(10)
                .ToList();

            if (entries.Count == 0)
            {
                OpenRecentMenuItem.Items.Add(new MenuItem
                {
                    Header = "(No recent files)",
                    IsEnabled = false,
                });
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool exists = File.Exists(entry.Path);
                string fileName = Path.GetFileName(entry.Path)?.Replace("_", "__") ?? entry.Path;
                string kind = string.Equals(entry.Kind, "Movie", StringComparison.OrdinalIgnoreCase)
                    ? "Movie" : "Sequence";
                var item = new MenuItem
                {
                    Header = $"{i + 1}. [{kind}] {fileName}" + (exists ? "" : " (missing)"),
                    ToolTip = entry.Path,
                    Tag = entry,
                    IsEnabled = exists,
                };
                item.Click += OpenRecent_Click;
                OpenRecentMenuItem.Items.Add(item);
            }
        }

        private void OpenRecent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not RecentFileEntry entry ||
                string.IsNullOrWhiteSpace(entry.Path))
                return;

            if (!File.Exists(entry.Path))
            {
                RefreshOpenRecentMenu();
                return;
            }

            if (string.Equals(entry.Kind, "Movie", StringComparison.OrdinalIgnoreCase))
                LoadMovieFromPath(entry.Path, alreadyConfirmed: false);
            else
                LoadSequenceFromPath(entry.Path, 0, fitTimeline: true,
                                     recordRecent: true, setActiveDocument: true);
        }

        /// <summary>Save commands at/between the arrows, rebased so the green
        /// start arrow is time zero, with the entered description header.</summary>
        private void CreateLibraryItem(string description)
        {
            double start = ServoCommand.TimeKey(Waveform.RangeStart);
            double end = ServoCommand.TimeKey(Waveform.RangeEnd);

            var items = _doc.Commands
                .Where(c =>
                {
                    double k = ServoCommand.TimeKey(c.OffsetSeconds);
                    return k >= start && k <= end;
                })
                .Select(c =>
                {
                    var copy = c.Clone();
                    copy.OffsetSeconds = ServoCommand.TimeKey(copy.OffsetSeconds - start);
                    return copy;
                })
                .ToList();

            if (items.Count == 0)
            {
                MessageBox.Show(this, "No commands lie between the arrows.",
                                "Create Library Item", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Save library item JSON",
                Filter = "JSON files (*.json)|*.json",
                FileName = "library-item.json",
                InitialDirectory = LibraryFolder(),
            };
            if (dlg.ShowDialog() != true) return;

            // Audio referenced by the item travels WITH the library.
            var libWarnings = new List<string>();
            foreach (var play in items.Where(c => c.Servo == ServoNames.Play))
            {
                string srcPath = ResolveAudioPath(play.TextValue);
                if (srcPath == null)
                {
                    libWarnings.Add(play.TextValue ?? "(empty path)");
                    continue;
                }
                try
                {
                    string dest = Path.Combine(LibraryAudioFolder(),
                                               Path.GetFileName(srcPath));
                    if (!string.Equals(srcPath, dest,
                                       StringComparison.OrdinalIgnoreCase))
                        File.Copy(srcPath, dest, overwrite: true);
                    play.TextValue = dest;
                }
                catch (Exception ex)
                {
                    libWarnings.Add($"{Path.GetFileName(srcPath)} ({ex.Message})");
                }
            }

            AnimationDocument.SaveCommandsOnly(dlg.FileName, items, description);
            ShowStatus($"Library item saved: {Path.GetFileName(dlg.FileName)}");

            if (libWarnings.Count > 0)
                MessageBox.Show(this,
                    "The item was saved, but these audio files could not " +
                    "be copied into Library\\Audio (their original paths " +
                    "were kept):\n\n  • " + string.Join("\n  • ", libWarnings),
                    "Create Library Item", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }

        /// <summary>Right-click confirmation for a pending blue-arrow insert.</summary>
        private void ConfirmPendingLibraryInsert()
        {
            if (_libraryPrompt != LibraryPrompt.InsertSequence ||
                string.IsNullOrWhiteSpace(_pendingLibraryItemPath))
                return;

            double at = ServoCommand.TimeKey(Waveform.InsertTime);
            string description = string.IsNullOrWhiteSpace(_pendingLibraryItemDescription)
                ? "(No description)" : _pendingLibraryItemDescription;
            string name = Path.GetFileName(_pendingLibraryItemPath);

            var result = MessageBox.Show(this,
                $"Insert '{name}' at {at:F3} seconds?\n\nDescription:\n{description}",
                "Insert Library Item", MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            string path = _pendingLibraryItemPath;
            try { InsertLibrarySequence(path, at); }
            finally { EndArrowPrompt(); }
        }

        /// <summary>Insert the selected library JSON at the blue-arrow time.</summary>
        private void InsertLibrarySequence(string fileName, double at)
        {
            try
            {
                var cmds = AnimationDocument.LoadCommandsOnly(fileName);
                if (cmds.Count == 0)
                {
                    MessageBox.Show(this, "The selected library item has no commands.",
                                    "Insert Library Item", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                // Library audio comes INTO the project.
                var insWarnings = new List<string>();
                foreach (var play in cmds.Where(c => c.Servo == ServoNames.Play))
                {
                    string name = Path.GetFileName(play.TextValue ?? "");
                    if (string.IsNullOrEmpty(name))
                    {
                        insWarnings.Add("(empty path)");
                        continue;
                    }

                    string srcPath =
                        File.Exists(play.TextValue) ? play.TextValue
                        : File.Exists(Path.Combine(LibraryAudioFolder(), name))
                            ? Path.Combine(LibraryAudioFolder(), name)
                            : ResolveAudioPath(play.TextValue);
                    string dest = Path.Combine(
                        _folders?.ProjectFolderOrDefault ?? "", name);

                    try
                    {
                        if (srcPath != null &&
                            !string.Equals(srcPath, dest,
                                           StringComparison.OrdinalIgnoreCase) &&
                            !File.Exists(dest))
                            File.Copy(srcPath, dest);

                        if (File.Exists(dest))
                            play.TextValue = dest;
                        else
                            insWarnings.Add(name);
                    }
                    catch (Exception ex)
                    {
                        insWarnings.Add($"{name} ({ex.Message})");
                    }
                }

                PushUndo();
                foreach (var c in cmds)
                {
                    c.OffsetSeconds = ServoCommand.TimeKey(c.OffsetSeconds + at);
                    _doc.Commands.Add(c);
                }
                RefreshAfterEdit();
                ShowStatus($"Library sequence inserted: {Path.GetFileNameWithoutExtension(fileName)}");

                if (insWarnings.Count > 0)
                    MessageBox.Show(this,
                        "These audio files could not be copied into the " +
                        "Project folder (their commands keep the original " +
                        "paths):\n\n  • " + string.Join("\n  • ", insWarnings),
                        "Insert Library Sequence", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not insert library sequence:\n" + ex.Message,
                                "Insert error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        #endregion

        // ================================================================
        #region 8. Hardware stubs
        // ================================================================

        /// <summary>
        /// STUB — called immediately whenever a NUMERIC value slider/box is
        /// changed with Live Drive on, and while dragging value sliders in
        /// the command editor. Value range depends on the servo (0..2000 for
        /// eye pops, 0..100 for positive controls including NoseBasket, and
        /// -100..100 for centered controls). Replace the body
        /// with real hardware output (serial, CAN, network, ...). Must be
        /// fast/non-blocking: it runs on the UI thread.
        /// </summary>
        public void MoveServoNow(ServoSpeed speed, ServoNames servo, int value)
        {
            // If this servo is mapped to a robot-head part, move that part
            // live while the slider is dragged (grid Live Drive sliders and
            // the sliders in the command editor both route through here).
            ForEachHeadView(v => v.SetServo(servo, value));

            // Physical hardware: drive the ganged servos when Live Drive is
            // on and the devices connected.
            if (LiveDrive && _hw.Connected)
                _hw.DriveGang(servo, speed, value);

            Debug.WriteLine($"MoveServoNow(speed={speed}, servo={servo}, value={value})");
        }

        /// <summary>Jog ONE child servo live (editor rows targeting an
        /// individual control): moves the corresponding robot-head part
        /// (mirrored - robot's left is the viewer's right) and, in Live
        /// Drive with hardware connected, drives that servo through
        /// MapDeltatoServo with its gang-relative direction.</summary>
        public void MoveChildServoNow(ServoSpeed speed, ServoNames gang,
                                      RobotControls control, int value)
        {
            ForEachHeadView(v => v.SetChildServo(gang, control, value));

            bool centered = ServoCommand.RangeFor(gang).Min < 0;
            if (LiveDrive && _hw.Connected)
                _hw.DriveControlValue(gang, control, speed, value, centered);

            Debug.WriteLine($"MoveChildServoNow(gang={gang}, control={control}, value={value})");
        }

        /// <summary>Apply one explicit Maestro speed/acceleration profile
        /// without changing position. Used when Edit Commands changes Speed.</summary>
        public void ConfigureServoSpeedNow(ServoSpeed speed, ServoNames servo)
        {
            if (speed == ServoSpeed.NoChange) return;
            if (LiveDrive && _hw.Connected)
                _hw.ConfigureGangSpeed(servo, speed);
            Debug.WriteLine($"ConfigureServoSpeedNow(servo={servo}, speed={speed})");
        }

        /// <summary>Apply an explicit speed/acceleration profile to one child
        /// servo without changing its position.</summary>
        public void ConfigureChildServoSpeedNow(ServoSpeed speed, ServoNames gang,
                                                RobotControls control)
        {
            if (speed == ServoSpeed.NoChange) return;
            if (LiveDrive && _hw.Connected)
                _hw.ConfigureControlSpeed(control, speed);
            Debug.WriteLine($"ConfigureChildServoSpeedNow(gang={gang}, control={control}, speed={speed})");
        }

        /// <summary>
        /// STUB — drive ONE physical servo channel to a raw PWM value
        /// (microseconds). Called by the verify sliders in the Servo
        /// Configuration window and by the RobotControl sub-row sliders in
        /// the grid (Live Drive). Replace with real Maestro/serial output.
        /// </summary>
        public void MoveRobotControlNow(RobotControls control, int pwm)
        {
            // Raw PWM to one channel (verify sliders). Live Drive gates it.
            if (LiveDrive && _hw.Connected)
                _hw.DriveControlPwm(control, pwm);

            Debug.WriteLine($"MoveRobotControlNow(control={control}, pwm={pwm})");
        }

        /// <summary>
        /// STUB — text-valued overload, used for RGBCommand: called when the
        /// RGB text is committed in the Live Drive grid or edited in the
        /// command editor. Replace with real hardware output.
        /// </summary>
        public void MoveServoNow(ServoSpeed speed, ServoNames servo, string textValue)
        {
            // RGB command text goes verbatim to the Arduino in Live Drive.
            if (LiveDrive && _hw.Connected && servo == ServoNames.RGBCommand)
                _hw.DriveRgb(textValue);

            Debug.WriteLine($"MoveServoNow(speed={speed}, servo={servo}, text=\"{textValue}\")");
        }

        /// <summary>
        /// STUB — called in real time during audio playback, once per unique
        /// time offset, with the array of commands (ServoNames, Speeds and
        /// Values — numeric or text) scheduled at that offset. Replace the
        /// body with real hardware output. Runs on the UI thread from the
        /// playback timer.
        /// </summary>
        public void PlayBackServoValues(ServoCommand[] commandsAtOffset)
        {
            // RGB commands also drive the URDF eye backing color. This is independent
            // of Live Drive/hardware so the preview changes at the exact command
            // boundary during ordinary timeline playback.
            foreach (var c in commandsAtOffset)
                if (c.Servo == ServoNames.RGBCommand && !c.Disable)
                    ForEachHeadView(v => v.SetEyeColor(c.ColorHex));

            // Drive the physical robot during playback when Live Drive is On.
            if (LiveDrive && _hw.Connected)
            {
                foreach (var c in commandsAtOffset)
                {
                    if (c.Servo == ServoNames.Play) continue;   // export-only

                    if (c.Disable)
                    {
                        // Turn the servo(s) OFF instead of moving them.
                        if (c.Control.HasValue)
                            _hw.DisableControl(c.Control.Value);
                        else
                            _hw.DisableGang(c.Servo);
                        continue;
                    }

                    if (c.IsTextServo)
                    {
                        if (c.Servo == ServoNames.RGBCommand)
                            _hw.DriveRgb(c.TextValue);
                    }
                    else if (c.Control.HasValue)
                    {
                        // Individual-control command: just that servo,
                        // with its direction relative to the parent gang.
                        bool centered = ServoCommand.RangeFor(c.Servo).Min < 0;
                        _hw.DriveControlValue(c.Servo, c.Control.Value, c.Speed,
                                              c.NumericValue, centered);
                    }
                    else
                    {
                        _hw.DriveGang(c.Servo, c.Speed, c.NumericValue);
                    }
                }
            }

            Debug.WriteLine($"PlayBackServoValues @ {commandsAtOffset[0].OffsetSeconds:F3}s:");
            foreach (var c in commandsAtOffset)
                Debug.WriteLine($"   {c.Servo}" +
                    (c.Control.HasValue ? $"[{c.Control}]" : "") +
                    $" = {c.ValueDisplay} ({c.SpeedDisplay})");
            // TODO: dispatch this batch of servo moves to the hardware here.
        }

        #endregion
    }

    /// <summary>
    /// One entry of the spline legend: the servo name shown in its line
    /// color next to a colored square, with a checkbox to show/hide the line.
    /// </summary>
    public class SplineLegendItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public ServoNames Servo { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.Brush Brush { get; set; }

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                PropertyChanged?.Invoke(this,
                    new System.ComponentModel.PropertyChangedEventArgs(nameof(Visible)));
            }
        }
        private bool _visible = true;
    }
}
