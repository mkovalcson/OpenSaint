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
using System.Text.Json;
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
        private PendingMovieSequence _pendingMovieSequence;
        private PendingMovieSequence CurrentMovieDraft =>
            _pendingMovieSequence?.IsFor(_doc, _moviePath) == true ? _pendingMovieSequence : null;

        private string _jsonPath;    // path of the loaded/saved JSON (title bar)
        private string _audioPath;   // path of the loaded audio file

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

        /// <summary>Playback visuals are advanced from WPF's render pulse so
        /// cursor, sliders and 3-D motion land on display frames instead of a
        /// competing 33 ms dispatcher timer.</summary>
        private bool _playbackRenderingSubscribed;
        private TimeSpan _lastPlaybackRenderingTime;
        private readonly PlaybackFrameCadence _previewFrameCadence = new();
        private bool _refreshPreviewThisFrame = true;
        private long _lastInformationalRefreshMs;

        /// <summary>Slow serial/ticcmd writes are ordered on a background
        /// worker and never block animation or mouse input.</summary>
        private readonly OrderedActionQueue _hardwarePlaybackQueue =
            new("ServoAnimator hardware playback");

        // Pre-indexed timeline data used by playback/grid evaluation. These
        // replace repeated full command-list scans on every rendered frame.
        private ServoCommand[] _orderedCommands = Array.Empty<ServoCommand>();
        private readonly Dictionary<ServoNames, ServoCommand[]> _gangCommandIndex = new();
        private readonly Dictionary<ServoNames, ServoCommand[]> _gangSpeedCommandIndex = new();
        private readonly Dictionary<(ServoNames Servo, RobotControls Control), ServoCommand[]>
            _childCommandIndex = new();
        private readonly Dictionary<ServoNames, SplineCurve> _splineCurveIndex = new();
        private double[] _sharedNeckTimes = Array.Empty<double>();
        private double[] _sharedNeckValues = Array.Empty<double>();
        private double[] _sharedNeckTangents = Array.Empty<double>();
        private ServoNames[] _sharedNeckOwners = Array.Empty<ServoNames>();

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
        private readonly PlaybackClock _playbackClock = new();
        private double _outputTimelineOrigin;

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
        private readonly MediaAssetCache _mediaCache = new();
        private FileSystemWatcher _movieMetadataWatcher;
        private readonly HashSet<string> _changedMovieFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _movieMetadataRefreshTimer = new()
        { Interval = TimeSpan.FromMilliseconds(250) };

        /// <summary>Where the audio starts on the timeline (seconds). Set by
        /// dragging the handle at the top-left of the waveform. Commands can
        /// be placed on the timeline before this point.</summary>
        private double _audioOffset;

        /// <summary>Copy/paste buffer for command groups (deep copies).</summary>
        private readonly List<ServoCommand> _clipboard = new();

        // ----- undo / redo (Edit menu, Ctrl+Z / Ctrl+Y) -----
        // Snapshot-based: before every mutating timeline operation the whole
        // command list and a user-facing action description are stored. Undo
        // swaps the current list with the top snapshot (pushing the current
        // one and its description onto redo); any NEW edit clears redo.
        private sealed record UndoEntry(List<ServoCommand> Commands, string Description, double AudioOffset, List<ServoNames> Splines);
        private readonly List<UndoEntry> _undoStack = new();
        private readonly List<UndoEntry> _redoStack = new();
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

        private sealed class MovieCarryPose
        {
            public Dictionary<ServoNames, double> Values { get; } = new();
            public Dictionary<ServoNames, ServoSpeed> Speeds { get; } = new();
            public Dictionary<ServoNames, string> TextValues { get; } = new();
            public Dictionary<ServoNames, string> Colors { get; } = new();
            public Dictionary<(ServoNames Servo, RobotControls Control), double> ChildValues { get; } = new();
            public ServoNames? NeckOwner { get; set; }
            public RgbRingFrame RgbFrame { get; set; }
        }

        private MovieCarryPose _movieCarryPose;

        private enum ActiveDocumentKind { None, Sequence, Movie }
        private ActiveDocumentKind _activeDocumentKind = ActiveDocumentKind.None;
        private RecentFilesSettings _recentFiles = new();

        // Fingerprint of the sequence as it last existed on disk. Movie
        // sequence switching compares this with the live editor state so a
        // single save/discard/cancel prompt covers every kind of edit.
        private string _savedSequenceFingerprint = "";
        private string _savedMovieFingerprint = "";
        private string _savedConfigurationFingerprint = "";
        private readonly DispatcherTimer _recoveryTimer;
        private bool _repairWindowOpen;
        private bool _repairOfferQueued;

        // Both values come from the build, so About agrees with the executable
        // and never substitutes the date the user happens to launch it.
        private const string AppDisplayName = "Animation Editor & Player";
        private static string AppVersion => typeof(MainWindow).Assembly.GetName().Version.ToString(3);
        private static string AppGenerationDate => typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "GenerationDate")?.Value ?? "Unknown";

        /// <summary>Detached URDF preview used when the user presses Undock.
        /// The old View > Robot Head entry has been removed; docking is now
        /// controlled directly from the URDF view.</summary>
        private RobotHeadWindow _head;
        private CommandEditorWindow _commandEditorWindow;
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

        /// <summary>Deterministic emulator of ArduinoOpenSaintRGB.ino used by
        /// the URDF preview. It is timeline-time based, so pause/seek/scrub
        /// reproduce the same four 16-LED ring state without a second timer.</summary>
        private readonly ArduinoRgbTimelineSimulator _rgbSimulator = new();

        /// <summary>Configuration/project folder locations, normally from
        /// Paths.json beside the exe. A deployed child Config folder is preferred;
        /// the live tree's sibling animatorConfig layout remains supported.</summary>
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

        /// <summary>
        /// Servo state explicitly reset at the current cursor remains staged until
        /// cursor movement or playback resumes normal timeline tracking.
        /// </summary>
        private readonly HashSet<ServoNames> _manualPoseOverrides = new();

        /// <summary>Timeline command groups whose resulting calibrated URDF
        /// pose was colliding during playback. WaveformView renders these command
        /// triangles bright red until any command edit invalidates the result.</summary>
        private readonly HashSet<double> _collisionCommandMarkers = new();
        private bool _syncingCollisionWarningToggle;

        /// <summary>Last user-selected spline-area height so hiding/re-showing
        /// the spline does not discard the audio/spline GridSplitter ratio.</summary>
        private GridLength _lastSplineTimelineHeight = new(190, GridUnitType.Pixel);
        private GridLength _lastTopEditorHeight = new(250, GridUnitType.Pixel);
        private EditorLayoutSettings _startupEditorLayout;
        private WindowState _lastNonMinimizedWindowState = WindowState.Normal;

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

        /// <summary>A looping movie cue cycles at its final authored command,
        /// even when its audio/document duration extends farther. All other
        /// playback retains the normal complete-content boundary.</summary>
        private double CurrentPlaybackEnd
        {
            get
            {
                if (_moviePlaybackActive && _moviePlaybackIndex >= 0 &&
                    _moviePlaybackIndex < _movieItems.Count &&
                    _movieItems[_moviePlaybackIndex].IsLooping &&
                    (_doc?.Commands?.Count ?? 0) > 0)
                {
                    double lastCommand = _doc.Commands.Max(c => c.OffsetSeconds);
                    // Avoid a zero-duration hot loop for sequences whose only
                    // command is at time zero.
                    if (lastCommand > 0.01)
                        return Math.Min(ContentEnd, lastCommand);
                }
                return ContentEnd;
            }
        }

        /// <summary>Timeline extent for scrolling/clicking/inserting: the
        /// content plus the editable tail.</summary>
        private double TimelineDuration => ContentEnd + TimelineTailSeconds;

        public MainWindow()
        {
            InitializeComponent();
            EditorTimelineGrid.LayoutUpdated += (_, _) => FitEditorPanels();
            foreach (var engine in _controllerEngines.Values) engine.RelativeMotion = AdvanceControllerRelative;
            StateChanged += (_, _) =>
            {
                if (WindowState != WindowState.Minimized)
                    _lastNonMinimizedWindowState = WindowState;
            };

            // Native searchable/context-sensitive Help is completely lazy.
            // MainWindow owns the playback policy used by every child dialog:
            // help can open only while transport is fully stopped.
            HelpSystem.IsHelpAvailable = () => _mode == PlayMode.Stopped;
            HelpSystem.EnableContextHelp(this, "getting-started");
            ConfigureContextHelpTopics();
            UpdateHelpAvailability();

            if (EmbeddedHeadView != null)
            {
                EmbeddedHeadView.CollisionWarningEnabledChanged += HeadView_CollisionWarningEnabledChanged;
                EmbeddedHeadView.PoseModeChanged += HeadView_PoseModeChanged;
                EmbeddedHeadView.PoseRgbCommandChanged += HeadView_PoseRgbCommandChanged;
                EmbeddedHeadView.LibraryPoseSaveRequested += HeadView_LibraryPoseSaveRequested;
                EmbeddedHeadView.LibraryPoseLoadRequested += HeadView_LibraryPoseLoadRequested;
                EmbeddedHeadView.DockToggleRequested += EmbeddedHeadView_DockToggleRequested;
                EmbeddedHeadView.VerticalResizeDeltaRequested += EmbeddedHeadView_VerticalResizeDeltaRequested;
                EmbeddedHeadView.SetDockedHostState();
            }
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            _recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _movieMetadataRefreshTimer.Tick += RefreshChangedMovieMetadata;
            _recoveryTimer.Tick += (_, _) => SaveRecoverySnapshotIfNeeded();

            // One row object per servo in the enum. _rows stays the master
            // collection used by all update logic; the two on-screen columns
            // are ordered views over the same row objects.
            foreach (ServoNames s in Enum.GetValues<ServoNames>())
            {
                if (s == ServoNames.Play) continue;   // export-only pseudo-servo
                _rows.Add(new ServoStateRow(s));
            }
            RefreshGridChildren();   // populate the [+/-] RobotControl sub-rows

            // Waveform events.
            Waveform.TimeClicked += Waveform_TimeClicked;
            Waveform.CommandsEditRequested += Timeline_EditCommandsRequested;
            Spline.CommandsEditRequested += Timeline_EditCommandsRequested;
            Waveform.RightClicked += Waveform_RightClicked;
            Waveform.ViewChanged += SyncScrollBar;
            Waveform.ViewChanged += SyncSplineView;   // keep spline zoom/scroll matched
            Waveform.AudioOffsetChanged += Waveform_AudioOffsetChanged;
            Waveform.AudioOffsetDragEnded += Waveform_AudioOffsetDragEnded;
            Waveform.MarkerDragged += Waveform_MarkerDragged;
            Waveform.MarkerDragCompleted += Waveform_MarkerDragCompleted;
            InitializeCommandGroups();
            Loaded += (_, _) => InitializeEditorApi();
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
            MovieTimeline.LoopToggleRequested += MovieTimeline_LoopToggleRequested;
            MovieTimeline.AssignTriggerRequested += MovieTimeline_AssignTriggerRequested;
            MovieTimeline.DescriptionEditRequested += index =>
            {
                if (index < 0 || index >= _movieItems.Count) return;
                // Editing the loaded sequence must not reload/discard its unsaved changes.
                if (!PathsEqual(_movieItems[index].FilePath, _jsonPath) && !SelectMovieSequence(index, 0)) return;
                SequenceDescriptionEdit_Click(MovieTimeline, new RoutedEventArgs());
            };
            MovieTimeline.ViewChanged += SyncMovieScroll;
            MovieTimeline.NewSequenceRequested += NewMovieSequence;
            MovieTimeline.BlockToolTipProvider = MovieBlockToolTip;
            MovieTimeline.SetItems(_movieItems);
            SetMovieDescriptionText(_movieDescription);
            UpdateEmptyStates();

            // Spline area: forwards zoom/pan to the waveform, clicks move the
            // cursor just like clicking the waveform.
            Spline.SyncTarget = Waveform;
            Spline.TimeClicked += Waveform_TimeClicked;
            Spline.RightClicked += Spline_RightClicked;
            Spline.PointValueChanged += Spline_PointValueChanged;
            Spline.PointTimeChanged += Spline_PointTimeChanged;
            Spline.PointAdded += Spline_PointAdded;
            Spline.PointDeleted += Spline_PointDeleted;
            Spline.DragCompleted += () => { _dragUndoPushed = false; RefreshAfterEdit(); };
            ((System.Windows.Data.CompositeCollection)SplineLegend.ItemsSource)
                .OfType<System.Windows.Data.CollectionContainer>().Single().Collection = _legend;
            InitializeTimelineLayout();

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
                // First run: use a deployed child Config folder when present;
                // otherwise retain the live project's sibling animatorConfig
                // discovery behavior.
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
                WatchMovieMetadata();
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
                _savedConfigurationFingerprint = CurrentConfigurationFingerprint();

                // Reopen the movie or standalone sequence that was active when
                // the editor was last closed. Missing files are skipped without
                // preventing the application from starting.
                bool restoredDocument = TryRestoreRecovery();
                if (!restoredDocument)
                    restoredDocument = TryRestoreLastDocument();
                if (!restoredDocument)
                {
                    Waveform.ZoomToFit();
                    SyncScrollBar();
                }

                // The URDF starts docked unless EditorLayout.json restores an
                // undocked layout. Dock/Undock is controlled directly from the
                // URDF view rather than from the View menu.
                PushHeadPose();
                Dispatcher.BeginInvoke(new Action(FinishRestoringEditorLayout),
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
                _recoveryTimer.Start();
                InitializeFocusControl();
                InitializeControllers();
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!ApplyOpenCommandEditor()) e.Cancel = true;
            base.OnClosing(e);
            // Window/owned-window bounds must be captured before WPF tears down
            // their native handles; OnClosed is too late for RestoreBounds.
            if (!e.Cancel && !SaveEditorLayout())
                e.Cancel = MessageBox.Show(this,
                    "The window layout could not be saved. Close anyway?\n\n" +
                    "Choose No to keep the editor open and retry after fixing access to the Config folder.",
                    "Layout not saved", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes;
        }

        protected override void OnClosed(EventArgs e)
        {
            _focusControlTimer.Stop();
            _backgroundMovieKeys?.Dispose();
            _controllerTimer.Stop();
            UrdfRenderLoop.Current.RemoveTargets(RenderControllerPreview);
            DisableControllerInput();
            _controllers?.Dispose();
            _editorApi?.Dispose();
            _apiLibraryTimer.Stop();
            StopPlaybackRendering();
            _recoveryTimer.Stop();
            SaveRecoverySnapshotIfNeeded();
            SaveLastActiveDocument();
            if (_head != null) { _head.ForceClose = true; _head.Close(); }
            _urdfConfigReloadTimer.Stop();
            _urdfConfigWatcher?.Dispose();
            _urdfConfigWatcher = null;
            _movieMetadataWatcher?.Dispose();
            _movieMetadataWatcher = null;
            _movieMetadataRefreshTimer.Stop();
            DisposeAudioDevice();
            _reader?.Dispose();
            _hardwarePlaybackQueue.Dispose(_hw.Dispose);
            base.OnClosed(e);
        }

        private void SaveRecoverySnapshotIfNeeded()
        {
            if (_folders == null) return;
            try
            {
                bool sequenceDirty = SequenceHasUnsavedChanges();
                bool movieDirty = MovieHasUnsavedChanges();
                bool configurationDirty = ConfigurationHasUnsavedChanges();
                if (!sequenceDirty && !movieDirty && !configurationDirty)
                {
                    EditorRecoverySnapshot.Delete(_folders.ConfigFolderOrDefault);
                    return;
                }

                SyncDocMetadata();
                new EditorRecoverySnapshot
                {
                    SavedUtc = DateTime.UtcNow,
                    SequencePath = _jsonPath ?? "",
                    PendingMovieSequenceName = CurrentMovieDraft?.Name ?? "",
                    Sequence = _doc,
                    MoviePath = _moviePath ?? "",
                    MovieDescription = _movieDescription ?? "",
                    MovieCreatedDate = _movieCreatedDate ?? "",
                    MovieItems = _movieItems.Select(i => new MovieSequenceItem
                    {
                        FilePath = i.FilePath,
                        DurationSeconds = i.DurationSeconds,
                        Description = i.Description,
                        IsLooping = i.IsLooping,
                        Trigger = i.Trigger,
                    }).ToList(),
                    MovieSelectedIndex = _movieSelectedIndex,
                    SequenceCursorTime = _cursorTime,
                    MovieCursorTime = MovieTimeline?.CursorTime ?? 0,
                    ActiveDocumentKind = _activeDocumentKind.ToString(),
                    SequenceWasDirty = sequenceDirty,
                    MovieWasDirty = movieDirty,
                    ConfigurationWasDirty = configurationDirty,
                    ServoConfiguration = _servoConfig,
                    UrdfConfiguration = _urdfConfig,
                }.Save(_folders.ConfigFolderOrDefault);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not write recovery snapshot: " + ex.Message);
            }
        }

        private bool TryRestoreRecovery()
        {
            EditorRecoverySnapshot snapshot;
            try { snapshot = EditorRecoverySnapshot.Load(_folders?.ConfigFolderOrDefault); }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not read recovery snapshot: " + ex.Message);
                return false;
            }
            if (snapshot == null) return false;

            DateTime localTime = snapshot.SavedUtc.Kind == DateTimeKind.Unspecified
                ? snapshot.SavedUtc : snapshot.SavedUtc.ToLocalTime();
            var answer = MessageBox.Show(this,
                $"Unsaved work was recovered from {localTime:g}.\n\nRestore it now?",
                "Restore autosaved work", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                EditorRecoverySnapshot.Delete(_folders?.ConfigFolderOrDefault);
                return false;
            }

            try
            {
                if (snapshot.ServoConfiguration != null)
                {
                    _servoConfig.Servos = snapshot.ServoConfiguration.Servos ?? new();
                    _servoConfig.GangDirections = snapshot.ServoConfiguration.GangDirections ?? new();
                    _servoConfig.LeftTicSerialNumber = snapshot.ServoConfiguration.LeftTicSerialNumber;
                    OnServoConfigChanged();
                }
                if (snapshot.UrdfConfiguration != null)
                {
                    _urdfConfig.CopyFrom(snapshot.UrdfConfiguration);
                    ApplyUrdfConfigurationToViews();
                }

                _movieItems.Clear();
                _movieItems.AddRange(snapshot.MovieItems ?? new List<MovieSequenceItem>());
                _moviePath = string.IsNullOrWhiteSpace(snapshot.MoviePath) ? null : snapshot.MoviePath;
                _movieDescription = snapshot.MovieDescription ?? "";
                _movieCreatedDate = string.IsNullOrWhiteSpace(snapshot.MovieCreatedDate)
                    ? DateTime.Today.ToString("yyyy-MM-dd") : snapshot.MovieCreatedDate;
                _movieSelectedIndex = Math.Clamp(snapshot.MovieSelectedIndex, -1, _movieItems.Count - 1);
                SetMovieDescriptionText(_movieDescription);

                if (snapshot.Sequence != null)
                    ApplyRecoveredSequence(snapshot.Sequence, snapshot.SequencePath,
                                           snapshot.SequenceCursorTime);

                _pendingMovieSequence = snapshot.Sequence != null &&
                    string.IsNullOrWhiteSpace(snapshot.SequencePath) &&
                    !string.IsNullOrWhiteSpace(_moviePath) &&
                    PendingMovieSequence.TryName(snapshot.PendingMovieSequenceName, out string draftName)
                        ? new PendingMovieSequence(draftName, _moviePath, _doc) : null;

                if (Enum.TryParse(snapshot.ActiveDocumentKind, out ActiveDocumentKind active))
                    _activeDocumentKind = active;

                MovieTimeline.SetItems(_movieItems);
                MovieTimeline.CursorTime = Math.Clamp(snapshot.MovieCursorTime, 0, MovieTimeline.TotalDuration);
                MovieTimeline.SelectedIndex = _movieSelectedIndex;
                _savedSequenceFingerprint = snapshot.SequenceWasDirty
                    ? "<recovered-sequence>" : CurrentSequenceFingerprint();
                _savedMovieFingerprint = snapshot.MovieWasDirty
                    ? "<recovered-movie>" : CurrentMovieFingerprint();
                _savedConfigurationFingerprint = snapshot.ConfigurationWasDirty
                    ? "<recovered-configuration>" : CurrentConfigurationFingerprint();
                RefreshMovieTimelineView();
                UpdateDocumentStatusIndicators();
                ShowStatus("Recovered autosaved work");
                ScheduleMissingFileRepair();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The recovery data could not be restored:\n" + ex.Message,
                    "Recovery error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ApplyRecoveredSequence(AnimationDocument document, string logicalPath,
                                            double initialCursor)
        {
            document ??= new AnimationDocument();
            if (!CommandConflictWindow.Resolve(this, document.Commands))
                throw new OperationCanceledException("Recovery canceled while resolving redundant commands.");
            StopPlayback();
            _reader?.Dispose();
            _reader = null;
            _audioPath = null;
            _primaryDuration = 0;
            _activeSource = null;
            _lastDesiredKey = null;
            _doc = document ?? new AnimationDocument();
            _doc.Commands ??= new List<ServoCommand>();
            RebuildPlaybackIndexes();
            _jsonPath = string.IsNullOrWhiteSpace(logicalPath) ? null : logicalPath;
            _rgbSimulator.Invalidate();
            _undoStack.Clear();
            _redoStack.Clear();
            SetDescriptionText(_doc.Description);

            foreach (var row in _rows)
                row.SplineEnabled = !row.IsTextRow &&
                    (_doc.SplineServos?.Contains(row.Servo.ToString()) ?? false);
            var nodSpline = _rows.First(r => r.Servo == ServoNames.NeckNodUp);
            var tiltSpline = _rows.First(r => r.Servo == ServoNames.NeckTiltRight);
            if (nodSpline.SplineEnabled || tiltSpline.SplineEnabled)
                nodSpline.SplineEnabled = tiltSpline.SplineEnabled = true;
            _splineHz = Array.IndexOf(SplineHzOptions, _doc.SplineSampleHz) >= 0
                ? _doc.SplineSampleHz : 50;

            _audioOffset = Math.Max(0, _doc.AudioStartOffsetSeconds);
            Waveform.PrimaryAudioName = "";
            Waveform.AudioOffset = _audioOffset;
            Waveform.SetAudio(null, null, 0.001, 0);
            RefreshAudioClips();
            string stored = !string.IsNullOrWhiteSpace(_doc.AudioFilePath)
                ? _doc.AudioFilePath : _doc.AudioFile;
            string primary = ResolveSequenceAudioPath(_doc, _jsonPath ?? "", stored);
            if (primary != null) LoadAudio(primary);

            Waveform.AudioOffset = _audioOffset;
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            SetCursor(Math.Clamp(initialCursor, 0, ContentEnd));
            RefreshAfterEdit();
            SyncScrollBar();
            UpdateTitle();
        }

        /// <summary>Apply one preview update to the embedded URDF view and,
        /// when created, the detachable URDF window.</summary>
        private void ForEachHeadView(Action<RobotHeadView> action)
        {
            if (EmbeddedHeadView != null) action(EmbeddedHeadView);
            if (_head?.HeadView != null) action(_head.HeadView);
        }

        private void HeadView_PoseModeChanged(bool enabled)
        {
            // Pose mode is a non-destructive draft.  When the user leaves it,
            // immediately restore the model to the timeline/cursor state unless
            // Insert Pose was used to commit the draft first.
            if (!enabled)
                PushHeadPose();
        }

        private void HeadView_PoseRgbCommandChanged(string command)
        {
            command = (command ?? string.Empty).Trim();

            // An empty Pose RGB value means "clear the Pose lighting".  Keep the
            // editable Pose command blank, but execute the Arduino ClearAll command
            // against the simulator and Live Drive hardware so Face Reset behaves
            // exactly like an RGB ClearAll without persisting ClearAll into the pose.
            if (string.IsNullOrWhiteSpace(command))
            {
                ForEachHeadView(v => v.SetPoseRgbCommand(string.Empty));
                var clearPreview = _rgbSimulator.PreviewCommand("ClearAll");
                ForEachHeadView(v => v.SetRgbRingFrame(clearPreview));
                if (LiveDrive && _hw.Connected)
                    _hw.DriveRgb("ClearAll");
                return;
            }

            // Keep docked/undocked Pose drafts synchronized without recursively
            // firing the event, then make the command take effect immediately.
            ForEachHeadView(v => v.SetPoseRgbCommand(command));
            var preview = _rgbSimulator.PreviewCommand(command);
            ForEachHeadView(v => v.SetRgbRingFrame(preview));

            // Pose mode follows the same Live Drive rule as the normal RGB grid:
            // preview is always visible in the URDF, physical Arduino output only
            // occurs when Live Drive and hardware connectivity allow it.
            if (LiveDrive && _hw.Connected)
                _hw.DriveRgb(command);
        }

        private void HeadView_LibraryPoseSaveRequested(RobotHeadView view) => SaveLibraryPose(view);

        private void HeadView_LibraryPoseLoadRequested(RobotHeadView view) => LoadLibraryPose(view);

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
            if (!RequireConfigPath(dlg.FileName, "audio file")) return;
            LoadAudio(dlg.FileName);
        }

        /// <summary>
        /// Opens an audio file, decodes the entire stream once to build the
        /// min/max peak envelope for drawing, then rewinds it and hands it to
        /// a WaveOutEvent for playback.
        /// </summary>
        private void LoadAudio(string path, bool stopPlayback = true)
        {
            try
            {
                if (stopPlayback) StopPlayback();
                var peaks = LoadingWindow.Wait(this, _mediaCache.Audio(path), "Preparing audio waveform…");
                _reader?.Dispose();

                _reader = new AudioFileReader(path);   // decodes to 32-bit float
                _audioPath = path;

                _primaryDuration = peaks.Duration;
                Waveform.AudioOffset = _audioOffset;
                Waveform.SetAudio(peaks.Min, peaks.Max, 0.001, peaks.Duration);

                _doc.AudioFile = Path.GetFileName(path);
                _doc.DurationSeconds = Math.Round(_audioOffset + peaks.Duration, 2);

                // The audio's name is drawn at the lower-left of where it
                // starts on the waveform (replacing the old top-bar label).
                Waveform.PrimaryAudioName = Path.GetFileName(path);

                SetCursor(0);
                SyncScrollBar();
                UpdateEmptyStates();
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
            if (!_controllerTransportInvocation) SetPlaybackControlSource(null);
            EndMovieBackgroundControl();
            // The ordinary sequence transport is independent of the movie
            // transport. Pressing it takes ownership of playback.
            if (_moviePlaybackActive)
            {
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                if (MoviePlayButton != null) MoviePlayButton.Content = "▶ Movie";
            }

            RefreshSelectedMovieBlockFromEditor();

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
            _outputTimelineOrigin = src.Start + _reader.CurrentTime.TotalSeconds;

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
        private void StartPlaybackAt(double t, bool preservePending = false)
        {
            if (_speedCalibrationBusy) return;
            StopControllerLibrary();
            ForEachHeadView(v => v.CalibratedMotionPaused = false);
            if (!preservePending) _hardwarePlaybackQueue.ClearPending();
            // Playback always returns the grid to the authored timeline pose;
            // any manually staged multi-row values are intentionally discarded.
            ClearManualPoseOverrides();

            // Rebuild immediately before every run so the preview evaluates
            // the latest spline control points, including edits made since
            // the last save/load.  UpdateServoState() also pushes that exact
            // evaluated pose into the URDF view before the first timer tick.
            RebuildSplineData();

            _cursorTime = Math.Clamp(t, 0, TimelineDuration);
            _lastFiredTime = _cursorTime;
            _playbackClock.Start(_cursorTime);
            _audioStartAttempts = 0;   // fresh retry budget for this run
            _lastDesiredKey = null;

            // Start (or park in silence for pre-roll/gaps) on whichever
            // source owns this position; Timer_Tick keeps switching as clip
            // boundaries are crossed.
            try { SwitchAudioTo(DesiredSourceAt(_cursorTime), _cursorTime); }
            catch { /* the tick retries */ }

            UpdateServoState(_cursorTime);

            // Commands authored exactly at zero are the first changes applied
            // to a carried movie pose. The normal (from,to] timer window cannot
            // include zero, so dispatch this initial group explicitly.
            if (_cursorTime <= 1e-9)
                FireCommandsBetween(-1e-9, 0);

            HelpSystem.CloseHelpWindow();
            _mode = PlayMode.Running;
            UpdateHelpAvailability();
            StartPlaybackRendering();
            PlayPauseBtn.Content = "❚❚ Pause";
        }

        private void StartPlaybackRendering()
        {
            _lastPlaybackRenderingTime = TimeSpan.MinValue;
            _previewFrameCadence.Reset();
            _lastInformationalRefreshMs = 0;
            if (_playbackRenderingSubscribed) return;
            System.Windows.Media.CompositionTarget.Rendering += Playback_Rendering;
            _playbackRenderingSubscribed = true;
        }

        private void StopPlaybackRendering()
        {
            if (!_playbackRenderingSubscribed) return;
            System.Windows.Media.CompositionTarget.Rendering -= Playback_Rendering;
            _playbackRenderingSubscribed = false;
        }

        private void Playback_Rendering(object sender, EventArgs e)
        {
            // Advance clock, command dispatch and cursor transforms on every display
            // frame. Only the heavier model/slider work is paced near 30 Hz.
            if (e is System.Windows.Media.RenderingEventArgs rendering)
            {
                if (_lastPlaybackRenderingTime == rendering.RenderingTime) return;
                _lastPlaybackRenderingTime = rendering.RenderingTime;
                _refreshPreviewThisFrame = _previewFrameCadence.IsDue(rendering.RenderingTime);
            }
            Timer_Tick(sender, e);
        }

        private void PausePlayback()
        {
            ForEachHeadView(v => v.CalibratedMotionPaused = true);
            _hardwarePlaybackQueue.ClearPending();
            // Resume always rebuilds a fresh device at the right source and
            // position, so the paused device is simply torn down.
            DisposeAudioDevice();
            _activeSource = null;
            StopPlaybackRendering();
            _mode = PlayMode.Paused;
            UpdateHelpAvailability();
            if (PlaybackOutputAllowed(false)) ForEachHeadView(v => v.SetMouth(0));
            PlayPauseBtn.Content = "▶ Resume";
        }

        private void StopPlayback(bool cancelPending = true)
        {
            StopControllerLibrary(cancelPending);
            if (cancelPending) _hardwarePlaybackQueue.ClearPending();
            StopPlaybackRendering();
            DisposeAudioDevice();
            _activeSource = null;
            _mode = PlayMode.Stopped;
            UpdateHelpAvailability();
            if (PlaybackOutputAllowed(false)) ForEachHeadView(v => v.SetMouth(0));
            PlayPauseBtn.Content = "▶ Sequence";
            if (_moviePlaybackActive)
            {
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                if (MoviePlayButton != null) MoviePlayButton.Content = "▶ Movie";
            }
        }

        /// <summary>
        /// Playback heartbeat (every display frame, with heavy preview work paced near 30 fps). The wall clock is the master:
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

            double? outputTime = null;
            if (_activeSource != null && _waveOut?.PlaybackState == PlaybackState.Playing)
            {
                try
                {
                    outputTime = _outputTimelineOrigin +
                        _waveOut.GetPosition() / (double)_waveOut.OutputWaveFormat.AverageBytesPerSecond;
                }
                catch { /* The monotonic clock continues if the driver is unavailable. */ }
            }
            double t = _playbackClock.Advance(outputTime);
            double playbackEnd = CurrentPlaybackEnd;

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
                t < playbackEnd - 0.05)
            {
                _audioStartAttempts++;
                try { SwitchAudioTo(desired, t); }
                catch { /* retry next tick */ }
                playing = _waveOut?.PlaybackState == PlaybackState.Playing;
            }

            if (_activeSource != null && playing)
            {
                _audioStartAttempts = 0;
            }
            // else: wall clock carries through silence/gaps.

            // End of this playback cycle. Normal sequences use the complete
            // content end; looping movie cues use their last command time.
            if (t >= playbackEnd - 0.005)
            {
                FireCommandsBetween(_lastFiredTime, playbackEnd);
                _cursorTime = playbackEnd;
                Waveform.CursorTime = _cursorTime;
                Waveform.InvalidateCursor();
                SyncSplineView();
                UpdateTimeText();
                UpdateServoState(_cursorTime);
                SyncMovieCursorToSequencePlayback(_cursorTime);
                if (!ContinueMoviePlaybackAfterSequenceEnd())
                    StopPlayback(cancelPending: false);
                return;
            }

            FireCommandsBetween(_lastFiredTime, t);
            _lastFiredTime = t;

            _cursorTime = t;
            SyncMovieCursorToSequencePlayback(t);
            Waveform.CursorTime = t;
            Waveform.EnsureVisible(t);
            Waveform.InvalidateCursor();
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
            if (_refreshPreviewThisFrame && PlaybackOutputAllowed(false)) ForEachHeadView(v => v.SetMouth(amp));

            // Text formatting/layout is informational; 15 Hz is responsive
            // while leaving render frames for cursors, sliders, and the model.
            long nowMs = Environment.TickCount64;
            if (nowMs - _lastInformationalRefreshMs >= 66)
            {
                UpdateTimeText();
                _lastInformationalRefreshMs = nowMs;
            }

            // This is the authoritative playback-to-preview path.  The grid
            // evaluates spline-enabled servos at the exact timeline time and
            // PushHeadPose() sends those values to every URDF joint each tick.
            // Non-spline servos continue to hold their latest command value.
            if (_refreshPreviewThisFrame) UpdateServoState(t);
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

            int first = FirstCommandAfter(_orderedCommands, from);
            int lastExclusive = FirstCommandAfter(_orderedCommands, to);
            int index = first;
            while (index < lastExclusive)
            {
                double timeKey = ServoCommand.TimeKey(_orderedCommands[index].OffsetSeconds);
                int groupEnd = index + 1;
                while (groupEnd < lastExclusive &&
                       ServoCommand.TimeKey(_orderedCommands[groupEnd].OffsetSeconds) == timeKey)
                    groupEnd++;

                ServoCommand[] commands = _orderedCommands[index..groupEnd];
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
                    HashSet<string> after = EvaluateUrdfCollisionPairsAt(timeKey);
                    if (after.Count > 0)
                        MarkCollisionCommand(timeKey);
                }
                index = groupEnd;
            }
        }

        private static int FirstCommandAfter(ServoCommand[] commands, double time)
        {
            int lo = 0, hi = commands.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (commands[mid].OffsetSeconds <= time) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Evaluate the calibrated URDF at an exact timeline time and
        /// return its active collision-pair IDs. During playback this is used
        /// at each geometry-affecting command group so the command triangle can
        /// be marked red when that command-time pose is unsafe.</summary>
        private HashSet<string> EvaluateUrdfCollisionPairsAt(double time)
        {
            if (!PlaybackOutputAllowed(false)) return new HashSet<string>(StringComparer.Ordinal);
            RobotHeadView view = _urdfUndocked ? _head?.HeadView : EmbeddedHeadView;
            // Warning annotations are optional. Do not evaluate and push the
            // entire timeline pose for every command when warnings are off.
            if (view?.UrdfDriveEnabled != true || !view.CollisionWarningsEnabled)
                return new HashSet<string>(StringComparer.Ordinal);
            double oldCursor = _cursorTime;
            _cursorTime = time;
            try
            {
                UpdateServoState(time);
                view.RefreshCollisionNow();
                return view.CollisionPairKeys.ToHashSet(StringComparer.Ordinal);
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
                if (!_offsetDragUndoPushed) { PushUndo("Move primary audio and following commands"); _offsetDragUndoPushed = true; }
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
            UpdateServoState(_cursorTime);
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
            if (!_markerDragUndoPushed) { PushUndo($"Move command group at {oldKey:F3} s"); _markerDragUndoPushed = true; }

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
            var peaks = LoadingWindow.Wait(this, _mediaCache.Audio(path), "Preparing audio waveform…");
            return (peaks.Min, peaks.Max, peaks.Duration);
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

        /// <summary>Resolve a Config-relative clip path. Legacy filename-only
        /// references are also searched beside the sequence and in Projects.</summary>
        private string ResolveAudioPath(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (ConfigPathService.TryResolve(ConfigRoot, stored, out string resolved) &&
                File.Exists(resolved))
                return resolved;
            string name = Path.GetFileName(stored);
            string p = Path.Combine(_folders?.ProjectFolderOrDefault ?? "", name);
            if (ConfigPathService.IsWithin(ConfigRoot, p) && File.Exists(p)) return p;
            if (!string.IsNullOrEmpty(_jsonPath))
            {
                p = Path.Combine(Path.GetDirectoryName(_jsonPath) ?? "", name);
                if (ConfigPathService.IsWithin(ConfigRoot, p) && File.Exists(p)) return p;
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
                        var pk = BuildPeaks(path);
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

            if (!_clipDragUndoPushed) { PushUndo($"Move audio clip {Path.GetFileName(clip.Command.TextValue)}"); _clipDragUndoPushed = true; }

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
            var box = new TextBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Text = current.ToString("F3"), Margin = new Thickness(0, 6, 0, 10) };
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
        /// a "Play" command (value = Config-relative path) whose clip then renders
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
            if (!RequireConfigPath(dlg.FileName, "audio file")) return;

            PushUndo($"Insert audio {Path.GetFileName(dlg.FileName)} at {_cursorTime:F3} s");
            _doc.Commands.Add(new ServoCommand
            {
                OffsetSeconds = ServoCommand.TimeKey(_cursorTime),
                Servo = ServoNames.Play,
                TextValue = ConfigPathService.ToRelative(ConfigRoot, dlg.FileName),
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
        #region 4. Servo playback state and hardware preview
        // ================================================================

        /// <summary>Recovery Dock button shown in Commands while the
        /// URDF preview is undocked. Uses the same docking path as the detached
        /// URDF window so all window/layout state stays synchronized.</summary>
        private void DockUrdf_Click(object sender, RoutedEventArgs e)
        {
            if (_urdfUndocked)
                SetUrdfUndocked(false);
        }

        /// <summary>
        /// Recomputes the cached state from the most recent command for
        /// that servo at or before time <paramref name="t"/>. Servos with no
        /// command yet show Value 0 / Speed Default / Offset "—".
        /// Called on every left click, every playback tick, and after edits.
        /// </summary>
        private void UpdateServoState(double t)
        {
            // The cached servo state and URDF preview represent the timeline
            // state at the current cursor, regardless of Live Drive. Live Drive
            // only gates whether user/playback movements are also sent to the
            // physical robot.
            foreach (var row in _rows)
            {
                // Never clobber a row the user is actively editing or
                // one that has been manually staged at this cursor time.
                // Staged values remain together until another time is
                // selected, playback begins, or they are generated into
                // timeline commands.
                if (row.IsEditing || _manualPoseOverrides.Contains(row.Servo)) continue;

                _gangCommandIndex.TryGetValue(row.Servo, out ServoCommand[] positions);
                _gangSpeedCommandIndex.TryGetValue(row.Servo, out ServoCommand[] speeds);
                ServoCommand last = LastCommandAtOrBefore(positions, t);
                ServoCommand lastSpeed = LastCommandAtOrBefore(speeds, t);

                // The grid represents the active speed state. Before any
                // explicit speed command, Maestro channels start in the
                // configured Default profile.
                row.Speed = lastSpeed?.Speed ??
                    (_movieCarryPose?.Speeds.TryGetValue(row.Servo, out ServoSpeed carriedSpeed) == true
                        ? carriedSpeed : ServoSpeed.Default);

                if (last == null)
                {
                    row.Offset = null;
                    if (_movieCarryPose?.Values.TryGetValue(row.Servo, out double carriedValue) == true)
                    {
                        row.Value = Math.Clamp(carriedValue, row.Min, row.Max);
                        row.TextValue = _movieCarryPose.TextValues.GetValueOrDefault(row.Servo, "");
                        row.ColorHex = _movieCarryPose.Colors.GetValueOrDefault(row.Servo, "");
                    }
                    else
                    {
                        row.Value = row.Min <= 0 ? 0 : row.Min;   // default 0 (or range floor)
                        row.TextValue = "";
                        row.ColorHex = "";
                    }
                }
                else
                {
                    row.Offset = last.OffsetSeconds;
                    if (row.IsTextRow)
                    {
                        row.TextValue = last.TextValue;        // last command text used
                        row.ColorHex = last.ColorHex;          // legacy RGB metadata
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
                if (row.SplineEnabled && !row.IsTextRow &&
                    !IsSharedNeckServo(row.Servo))
                {
                    _splineCurveIndex.TryGetValue(row.Servo, out SplineCurve curve);
                    if (curve?.T?.Length >= 2 &&
                        t >= curve.T[0] - 1e-9)
                        row.Value = Math.Clamp(
                            SplineUtil.Eval(curve.T, curve.V, curve.M, t),
                            curve.Min, curve.Max);
                }
            }

            // The neck pair is one mutually-exclusive physical control.
            // In spline mode show the common interpolated value only on
            // the row that currently owns the neck; the other row is zero.
            var nodSplineRow = _rows.First(r => r.Servo == ServoNames.NeckNodUp);
            var tiltSplineRow = _rows.First(r => r.Servo == ServoNames.NeckTiltRight);
            if ((nodSplineRow.SplineEnabled || tiltSplineRow.SplineEnabled) &&
                !_manualPoseOverrides.Contains(ServoNames.NeckNodUp) &&
                !_manualPoseOverrides.Contains(ServoNames.NeckTiltRight))
            {
                var neckState = SharedNeckStateAt(t);
                if (neckState.Owner.HasValue)
                {
                    nodSplineRow.Value = neckState.Owner.Value == ServoNames.NeckNodUp
                        ? neckState.Value : 0;
                    tiltSplineRow.Value = neckState.Owner.Value == ServoNames.NeckTiltRight
                        ? neckState.Value : 0;
                }
            }

            // The head preview mirrors the grid: same values, including
            // spline interpolation, so moving the timeline cursor (or
            // playback) animates the head.
            PushHeadPose();
        }

        private static ServoCommand LastCommandAtOrBefore(ServoCommand[] commands, double time)
        {
            if (commands == null || commands.Length == 0) return null;
            int index = FirstCommandAfter(commands, time) - 1;
            return index >= 0 ? commands[index] : null;
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

        /// <summary>"Disable All": disable PWM on every Maestro servo
        /// channel so the servos go limp (safety / rest). Works whenever
        /// hardware is connected, regardless of the Live Drive state.</summary>
        private void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            DisableControllerInput();
            StopPlayback();
            if (_hw.Connected)
                _hardwarePlaybackQueue.EnqueueBarrier(_hw.DisableAll);
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
            _manualPoseOverrides.Clear();
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
                _manualPoseOverrides.Add(row.Servo);
            }

            PushHeadPose();
            ShowStatus(_hw.Connected
                ? "Robot reset to servo defaults; Eye Pop = 0; Arduino ClearAll"
                : "Reset preview applied; hardware not connected");
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
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                ServoConfigurationSaved, OpenSpeedCalibration)
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
                    MarkConfigurationSaved();
                },
                () =>
                {
                    // URDF Configuration Back restores the in-memory snapshot
                    // from when the window opened. Reapply it immediately without
                    // writing URDFconfig.json.
                    ApplyUrdfConfigurationToViews();
                    PushHeadPose();
                })
            { Owner = this };

            _urdfConfigWindow = win;
            win.Closed += (_, _) =>
            {
                _urdfConfigWindow = null;
                UpdateDocumentStatusIndicators();
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
                ConfigureMotionView(h);
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
            if (!PlaybackOutputAllowed(false)) return;
            if (EmbeddedHeadView == null && _head?.HeadView == null) return;
            ServoStateRow Row(ServoNames s) => _rows.First(r => r.Servo == s);
            double RV(ServoNames s) => Row(s).Value;

            var nodRow = Row(ServoNames.NeckNodUp);
            var tiltRow = Row(ServoNames.NeckTiltRight);
            double nod = 0, tilt = 0;
            ServoNames? neckOwner = null;

            // Manual grid staging must remain immediately visible. Otherwise
            // use the shared time-ordered neck stream. This fixes the previous
            // mismatch where two separately evaluated spline rows could leave
            // NeckTiltRight visually inactive even though it owned the neck.
            bool nodManual = _manualPoseOverrides.Contains(ServoNames.NeckNodUp);
            bool tiltManual = _manualPoseOverrides.Contains(ServoNames.NeckTiltRight);
            if (nodManual && !tiltManual)
            {
                neckOwner = ServoNames.NeckNodUp;
                nod = nodRow.Value;
            }
            else if (tiltManual)
            {
                neckOwner = ServoNames.NeckTiltRight;
                tilt = tiltRow.Value;              // tie: Tilt owns, as on timeline
            }
            else
            {
                var neckState = SharedNeckStateAt(_cursorTime);
                neckOwner = neckState.Owner;
                if (neckState.Owner == ServoNames.NeckNodUp)
                    nod = neckState.Value;
                else if (neckState.Owner == ServoNames.NeckTiltRight)
                    tilt = neckState.Value;
            }

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
                if (_manualPoseOverrides.Contains(gang)) return row.Value;

                _childCommandIndex.TryGetValue((gang, control),
                    out ServoCommand[] childCommands);
                ServoCommand last = LastCommandAtOrBefore(childCommands,
                    _cursorTime + 1e-9);

                return MoviePoseContinuity.ChildValue(row.Value, row.Offset, last,
                    _movieCarryPose?.ChildValues.TryGetValue((gang, control), out double carriedChild) == true
                        ? carriedChild : null);
            }

            void ApplyPose(RobotHeadView headView)
            {
                ConfigureTimelineMotion(headView);
                headView.SetPose(
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
                neckOwner: neckOwner,
                neckTurn: RV(ServoNames.NeckTurn),
                whip: RV(ServoNames.Whip_Antenna_RaiseLower),
                mic: RV(ServoNames.Microphone_RaiseLower),
                mfr: RV(ServoNames.MFR_UpDown),
                noseBody: RV(ServoNames.NoseBody),
                noseBasket: noseBasketVal,
                leftEyePop: EyePopValue(leftPop),
                rightEyePop: EyePopValue(rightPop),
                whipRotate: RV(ServoNames.Whip_Antenna_Rotate),
                mfrRotate: RV(ServoNames.MFR_Rotate));
            }

            if (EmbeddedHeadView != null) ApplyPose(EmbeddedHeadView);
            if (_head?.HeadView != null) ApplyPose(_head.HeadView);

            // RGB rings are evaluated from the sequence playhead rather than a
            // separate wall-clock timer. This makes the URDF match Arduino
            // timing during playback and remain deterministic when scrubbing.
            var rgbFrame = _rgbSimulator.Evaluate(_doc.Commands, _cursorTime);
            ForEachHeadView(v => v.SetRgbRingFrame(rgbFrame));
        }

        private void LiveDrive_Changed(object sender, RoutedEventArgs e)
        {
            ResetControllerMotion();
            if (!LiveDrive)
                _hardwarePlaybackQueue.ClearPending();

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
                LiveDriveBtn.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty,
                    LiveDrive ? "LiveDriveText" : "PrimaryText");
                LiveDriveBtn.ToolTip = LiveDrive
                    ? "Live Drive ON — editor movements are sent to connected hardware"
                    : "Live Drive OFF — edit and preview without driving hardware";
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

        private void ClearManualPoseOverrides()
        {
            _manualPoseOverrides.Clear();
            foreach (var row in _rows) row.IsEditing = false;
        }

        #endregion

        // ================================================================
        #region 5. Timeline interaction (cursor, zoom, scrollbar)
        // ================================================================

        /// <summary>Left click on the waveform: move the cursor there (the view
        /// already snapped to a nearby command marker) and show what lives there.</summary>
        private void Waveform_TimeClicked(double t) => SetCursor(t);

        private void Timeline_EditCommandsRequested(double time)
        {
            if (IsRunning) PausePlayback();
            SetCursor(time);
            EditCommandsAtCursor();
        }

        /// <summary>
        /// Central "move the cursor" routine. Clamps, seeks the audio if it is
        /// currently playing, updates the time readout, the servo grid (last
        /// value of every servo up to this time) and the commands-at-point list.
        /// </summary>
        private void SetCursor(double t)
        {
            double newTime = Math.Clamp(t, 0, TimelineDuration);
            if (Math.Abs(newTime - _cursorTime) > 1e-9)
                ClearManualPoseOverrides();
            _cursorTime = newTime;

            if (IsRunning || _reader != null)
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
            Waveform.InvalidateCursor();
            SyncSplineView();
            UpdateTimeText();
            ForEachHeadView(v => v.SnapCalibratedMotion = true);
            try { UpdateServoState(_cursorTime); }
            finally { ForEachHeadView(v => v.SnapCalibratedMotion = false); }
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
            CommandsAtPointEditButton.IsEnabled = CommandsAtPointDeleteButton.IsEnabled = !_showModifiedControls;
            if (_showModifiedControls)
            {
                UpdateModifiedControlsList();
                return;
            }
            CommandsAtPointList.ToolTip = "Double-click to edit; Delete removes the selected command";
            CommandsAtPointHeader.ToolTip = "Commands at the current sequence cursor";
            var cmds = CommandsAt(_cursorTime);
            int keep = CommandsAtPointList.SelectedIndex;
            CommandsAtPointList.Items.Clear();

            CommandsAtPointHeader.Text = cmds.Count == 0
                ? $"Commands at cursor {_cursorTime:F3} s: (none)"
                : $"Commands at cursor {_cursorTime:F3} s: {cmds.Count} command(s)";

            foreach (var c in cmds)
                CommandsAtPointList.Items.Add(new CommandInspectorRow(
                    c.Control.HasValue ? $"{c.Servo} [{c.Control}]" : c.Servo.ToString(),
                    c.ValueDisplay, c.SpeedDisplay, $"{c.OffsetSeconds:0.000}", BrushFor(c.Servo),
                    $"{c.Servo}{(c.Control.HasValue ? $" [{c.Control}]" : "")}\n{c.ValueDisplay} · {c.SpeedDisplay} · {c.OffsetSeconds:F3} s" +
                    (string.IsNullOrWhiteSpace(c.Reason) ? "" : $"\n{c.Reason}")));
            if (CommandsAtPointList.Items.Count > 0)
                CommandsAtPointList.SelectedIndex = Math.Clamp(keep, 0, CommandsAtPointList.Items.Count - 1);
        }

        private string MarkerSummaryAt(double time)
        {
            var cmds = CommandsAt(time);
            if (cmds.Count == 0) return $"No commands at {time:F3} s";
            var lines = cmds.Select(c =>
                $"{c.Servo}{(c.Control.HasValue ? $"[{c.Control}]" : "")}: {c.ValueDisplay}");
            string text = $"{cmds.Count} command{(cmds.Count == 1 ? "" : "s")} @ {time:F3} s\n" +
                          string.Join("\n", lines);

            return text;
        }

        private void CommandsAtPointAdd_Click(object sender, RoutedEventArgs e) => InsertNewCommand();
        private void CommandsAtPointEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_showModifiedControls) return;
            var cmds = CommandsAt(_cursorTime);
            if (cmds.Count == 0) return;
            int i = CommandsAtPointList.SelectedIndex;
            EditCommandsAtCursor(i >= 0 && i < cmds.Count ? cmds[i] : null);
        }
        private void CommandsAtPointDelete_Click(object sender, RoutedEventArgs e) => DeleteSelectedCommandAtCursor();
        private void CommandsAtPointList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_showModifiedControls) return;
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
            if (_showModifiedControls) return;
            var cmds = CommandsAt(_cursorTime);
            int i = CommandsAtPointList.SelectedIndex;
            if (i < 0 || i >= cmds.Count) return;
            PushUndo($"Delete {cmds[i].Servo} command at {_cursorTime:F3} s");
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
            SyncCommandGroupSelection();
            Waveform.CollisionMarkers = _collisionCommandMarkers
                .Where(liveMarkers.Contains)
                .OrderBy(t => t)
                .ToList();
            Waveform.InvalidateVisual();
        }

        /// <summary>One-stop refresh after any timeline edit. Spline curves
        /// are rebuilt FIRST because the grid now reads interpolated values
        /// from them for spline-checked servos.</summary>
        private bool RefreshAfterEdit()
        {
            _apiEditGeneration++;
            bool accepted = true;
            if (CommandConflicts.Find(_doc.Commands).Count > 0)
            {
                if (IsRunning) PausePlayback();
                if (!CommandConflictWindow.Resolve(this, _doc.Commands))
                {
                    // Every timeline insertion/drag has a pre-operation undo
                    // snapshot. Cancel restores the entire pending operation.
                    if (_undoStack.Count == 0)
                        throw new InvalidOperationException("Cannot cancel a command change without its original snapshot.");
                    var before = _undoStack[^1];
                    _undoStack.RemoveAt(_undoStack.Count - 1);
                    RestoreSnapshot(before.Commands, refresh: false, audioOffset: before.AudioOffset, splines: before.Splines);
                    accepted = false;
                    ShowStatus("Command change canceled");
                }
            }
            MovieTimeline.RefreshBlockToolTip();
            // Collision warnings describe the exact command values that were
            // previously played. Any command modification invalidates them;
            // playback will repopulate red markers from the edited sequence.
            ClearCollisionCommandWarnings();
            _rgbSimulator.Invalidate();
            RebuildSplineData();   // control points may have changed
            RefreshMarkers();
            RefreshAudioClips();   // "Play" clips are derived from commands

            // Commands or clips may now extend past the old content end:
            // roll the editable tail forward and keep views in sync.
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            Spline.Duration = TimelineDuration;
            SyncScrollBar();
            UpdateServoState(_cursorTime);
            UpdateCommandsAtPointList();
            UpdateDocumentStatusIndicators();
            UpdateEmptyStates();
            return accepted;
        }

        private void UpdateEmptyStates()
        {
            if (SequenceEmptyStatePanel != null)
            {
                bool emptySequence = _primaryDuration <= 0 &&
                    !(_doc?.Commands?.Any() ?? false);
                SequenceEmptyStatePanel.Visibility = emptySequence
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            if (MovieEmptyStatePanel != null)
                MovieEmptyStatePanel.Visibility = _movieItems.Count == 0 && string.IsNullOrWhiteSpace(_moviePath)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmptyOpenSequence_Click(object sender, RoutedEventArgs e) =>
            LoadProject_Click(sender, e);

        private void EmptyInsertAudio_Click(object sender, RoutedEventArgs e) =>
            OpenAudio_Click(sender, e);

        private void EmptyLoadMovie_Click(object sender, RoutedEventArgs e) =>
            LoadMovie_Click(sender, e);

        private void EmptyInsertFirstSequence_Click(object sender, RoutedEventArgs e) =>
            MovieTimeline_InsertRequested(0);

        // --- zoom buttons + scrollbar sync -----------------------------

        private void SequenceRestart_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            SetCursor(0);
            ShowStatus("Sequence cursor moved to beginning");
        }

        private void SequenceStop_Click(object sender, RoutedEventArgs e)
        {
            CancelApiLibraryPicker();
            EndMovieBackgroundControl();
            StopPlayback();
            SetPlaybackControlSource(null);
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
            if (_libraryPrompt != LibraryPrompt.InsertSequence && Waveform.SelectedMarkers.Count > 0)
                ShowSelectedCommandMenu();
            else ShowCursorContextMenu(Waveform);
        }

        private void Spline_RightClicked(double clickTime)
        {
            if (Spline.CombinedMode && Waveform.SelectedMarkers.Count > 0) ShowSelectedCommandMenu();
            else ShowCursorContextMenu(Spline);
        }

        private void ShowCursorContextMenu(UIElement placementTarget)
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

            Item($"Insert new Command at {_cursorTime:F3} s…", (_, _) => InsertNewCommand());

            Item($"Insert Pose at {_cursorTime:F3} s",
                 (_, _) => InsertPoseAtCursor(),
                 (_urdfUndocked ? _head?.HeadView : EmbeddedHeadView) != null);

            Item($"Insert Library Pose at {_cursorTime:F3} s…",
                 (_, _) => InsertLibraryCommandAtCursor());

            Item($"Insert Library Sequence at {_cursorTime:F3} s…",
                 (_, _) => InsertLibrarySequenceAtCursor());

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

            menu.PlacementTarget = placementTarget;
            menu.IsOpen = true;
        }

        /// <summary>Create one new command at the cursor and open the editor
        /// so its fields can be filled in.</summary>
        private void InsertNewCommand()
        {
            var cmd = new ServoCommand
            {
                OffsetSeconds = ServoCommand.TimeKey(_cursorTime),
                Servo = ServoNames.NeckTurn,
                NumericValue = 0,
                Speed = ServoSpeed.NoChange,
            };
            EditCommandsAtCursor(cmd);
        }

        /// <summary>Open the modeless editor for every command at the cursor.
        /// Keeping this window modeless leaves the URDF camera/buttons fully
        /// operational while commands are being edited. Draft rows are merged
        /// and checked for conflicts before they enter the playable timeline.</summary>
        private void EditCommandsAtCursor(ServoCommand focusCommand = null)
        {
            if (_commandEditorWindow != null)
            {
                if (_commandEditorWindow.WindowState == WindowState.Minimized)
                    _commandEditorWindow.WindowState = WindowState.Normal;
                _commandEditorWindow.Activate();
                return;
            }

            AnimationDocument source = _doc;
            double editTime = _cursorTime;
            var editor = new CommandEditorWindow(_doc, _cursorTime,
                                                 MoveServoNow,      // numeric variant
                                                 MoveServoNow,      // text variant (RGBCommand)
                                                 MoveChildServoNow, // individual-control variant
                                                 ConfigureServoSpeedNow,
                                                 ConfigureChildServoSpeedNow,
                                                 LibraryCommandsFolder(),
                                                 SplineServosEnabled(),
                                                 (commands, splineChanges) =>
                                                 {
                                                     if (!ReferenceEquals(source, _doc))
                                                     {
                                                         MessageBox.Show(this, "A different sequence is now open. Cancel this editor and reopen Edit Commands for the current sequence.",
                                                             "Sequence changed", MessageBoxButton.OK, MessageBoxImage.Information);
                                                         return false;
                                                     }
                                                     if (!CommandConflictWindow.Resolve(_commandEditorWindow ?? (Window)this, commands)) return false;
                                                     var splines = SplineServosEnabled().ToHashSet();
                                                     foreach (var change in splineChanges)
                                                         if (change.Value) splines.Add(change.Key); else splines.Remove(change.Key);
                                                     if (splines.SetEquals(SplineServosEnabled()) && _doc.Commands.Count == commands.Count &&
                                                         _doc.Commands.Zip(commands).All(p => CommandConflicts.SameContent(p.First, p.Second)))
                                                         return true;
                                                     PushUndo($"Edit commands at {editTime:F3} s");
                                                     _doc.Commands = commands;
                                                     ApplySplineSettings(splines);
                                                     return true;
                                                 },
                                                 focusCommand)
            {
                Owner = this,
            };
            _commandEditorWindow = editor;
            editor.Closed += (_, _) =>
            {
                if (ReferenceEquals(_commandEditorWindow, editor))
                    _commandEditorWindow = null;
                RefreshAfterEdit();
            };
            editor.Show();
            editor.Activate();
        }

        private bool ApplyOpenCommandEditor()
        {
            _commandEditorWindow?.Close();
            return _commandEditorWindow == null;
        }

        /// <summary>Delete every command at the cursor; the command marker is
        /// removed automatically because RefreshMarkers() rebuilds from the
        /// remaining commands.</summary>
        private void DeleteCommandsAtCursor()
        {
            PushUndo($"Delete commands at {_cursorTime:F3} s");
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
            if (_clipboard.Count == 0 || !ApplyOpenCommandEditor()) return;
            if (IsRunning) PausePlayback();
            PushUndo($"Paste {_clipboard.Count} command(s) at {_cursorTime:F3} s");
            double t = ServoCommand.TimeKey(_cursorTime);
            _doc.Commands.AddRange(CommandGroupOperations.CopyAt(_clipboard, t));
            if (RefreshAfterEdit())
                ShowStatus($"Paste completed at {_cursorTime:F3} s");
        }

        /// <summary>Open the Library\Commands browser and insert the
        /// selected single-time-point command group at the current cursor.
        /// Every stored offset is intentionally ignored: all commands land at
        /// exactly the selected timeline point.</summary>
        private void InsertLibraryCommandAtCursor()
        {
            var win = new LibraryItemSelectionWindow(
                LibraryCommandsFolder(), manageMode: false,
                itemLabel: "Library Pose", showAudioFiles: false)
            {
                Owner = this,
            };
            if (win.ShowDialog() != true || win.SelectedLibraryItem == null)
                return;

            try
            {
                var cmds = AnimationDocument.LoadCommandsOnly(
                    win.SelectedLibraryItem.FullPath);
                if (cmds.Count == 0)
                {
                    MessageBox.Show(this, "The selected Library Pose contains no commands.",
                                    "Insert Library Pose", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                PushUndo($"Insert Library Pose {Path.GetFileNameWithoutExtension(win.SelectedLibraryItem.FullPath)}");
                double at = ServoCommand.TimeKey(_cursorTime);
                foreach (var c in cmds)
                {
                    var copy = c.Clone();
                    copy.OffsetSeconds = at;
                    _doc.Commands.Add(copy);
                }
                if (RefreshAfterEdit())
                    ShowStatus($"Library Pose inserted at {at:F3} s");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not insert the Library Pose:\n" + ex.Message,
                                "Insert Library Pose", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        /// <summary>Open the Library Sequence browser and insert the
        /// selected multi-time sequence directly at the current audio-timeline
        /// cursor. Relative command offsets inside the Library Sequence are
        /// preserved and rebased from the selected cursor time.</summary>
        private void InsertLibrarySequenceAtCursor()
        {
            EndArrowPrompt();
            var win = new LibraryItemSelectionWindow(
                LibraryFolder(), manageMode: false,
                itemLabel: "Library Sequence", showAudioFiles: true)
            {
                Owner = this,
            };
            if (win.ShowDialog() != true || win.SelectedLibraryItem == null)
                return;

            InsertLibrarySequence(win.SelectedLibraryItem.FullPath,
                                  ServoCommand.TimeKey(_cursorTime));
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
                InitialDirectory = LibraryFolder(),
            };
            if (dlg.ShowDialog() != true) return;
            if (!RequireConfigPath(dlg.FileName, "library item or sequence")) return;

            try
            {
                var cmds = AnimationDocument.LoadCommandsOnly(dlg.FileName);
                PushUndo($"Insert commands from {Path.GetFileName(dlg.FileName)}");
                foreach (var c in cmds)
                {
                    c.OffsetSeconds = ServoCommand.TimeKey(c.OffsetSeconds + _cursorTime);
                    _doc.Commands.Add(c);
                }
                if (RefreshAfterEdit())
                    ShowStatus($"Command insertion completed from {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read commands:\n" + ex.Message,
                                "Insert error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Translate a logical URDF pose into the same normal ServoCommand
        /// representation used by the timeline and reusable Library Poses.</summary>
        private List<ServoCommand> BuildCommandsFromPose(RobotPoseSnapshot pose, double t)
        {
            var commands = new List<ServoCommand>();

            ServoCommand Cmd(ServoNames servo, double value, RobotControls? control = null) => new()
            {
                OffsetSeconds = t,
                Servo = servo,
                Control = control,
                NumericValue = (int)Math.Round(value),
                Speed = ServoSpeed.NoChange,
                Reason = "pose",
            };

            ServoCommand TextCmd(ServoNames servo, string value) => new()
            {
                OffsetSeconds = t,
                Servo = servo,
                TextValue = value ?? string.Empty,
                Speed = ServoSpeed.NoChange,
                Reason = "pose",
            };

            bool Same(params double[] values) => values.Length < 2 ||
                values.Skip(1).All(v => Math.Abs(v - values[0]) < 0.5);

            void TwoSide(ServoNames servo, RobotControls leftControl, double left,
                         RobotControls rightControl, double right)
            {
                if (Same(left, right)) commands.Add(Cmd(servo, left));
                else
                {
                    commands.Add(Cmd(servo, left, leftControl));
                    commands.Add(Cmd(servo, right, rightControl));
                }
            }

            TwoSide(ServoNames.EyesHorizontalRight,
                RobotControls.LeftLensHorizontal, pose.LeftEyeHorizontal,
                RobotControls.RightLensHorizontal, pose.RightEyeHorizontal);
            TwoSide(ServoNames.EyesVerticalUp,
                RobotControls.LeftLensVertical, pose.LeftEyeVertical,
                RobotControls.RightLensVertical, pose.RightEyeVertical);
            TwoSide(ServoNames.IrisClose,
                RobotControls.LeftIris, pose.LeftIris,
                RobotControls.RightIris, pose.RightIris);

            if (Same(pose.LeftTopFlapOpen, pose.RightTopFlapOpen,
                     pose.LeftBottomFlapOpen, pose.RightBottomFlapOpen))
            {
                commands.Add(Cmd(ServoNames.FlapsOpen, pose.LeftTopFlapOpen));
            }
            else
            {
                commands.Add(Cmd(ServoNames.FlapsOpen, pose.LeftTopFlapOpen, RobotControls.BrowLeftTopOpen));
                commands.Add(Cmd(ServoNames.FlapsOpen, pose.RightTopFlapOpen, RobotControls.BrowRightTopOpen));
                commands.Add(Cmd(ServoNames.FlapsOpen, pose.LeftBottomFlapOpen, RobotControls.BrowLeftBottomOpen));
                commands.Add(Cmd(ServoNames.FlapsOpen, pose.RightBottomFlapOpen, RobotControls.BrowRightBottomOpen));
            }

            TwoSide(ServoNames.FlapTiltUp,
                RobotControls.BrowLeftTopTilt, pose.LeftTopFlapTilt,
                RobotControls.BrowRightTopTilt, pose.RightTopFlapTilt);
            TwoSide(ServoNames.VentsOpen,
                RobotControls.LeftEyeVent, pose.LeftVent,
                RobotControls.RightEyeVent, pose.RightVent);

            commands.Add(Cmd(ServoNames.NeckTurn, pose.NeckTurn));
            if (pose.NeckOwner == ServoNames.NeckTiltRight)
                commands.Add(Cmd(ServoNames.NeckTiltRight, pose.NeckTilt));
            else
                commands.Add(Cmd(ServoNames.NeckNodUp, pose.NeckNod));

            commands.Add(Cmd(ServoNames.NoseBody, pose.NoseBody));
            commands.Add(Cmd(ServoNames.NoseBasket, pose.NoseBasket));

            if (Same(pose.LeftEyePop, pose.RightEyePop))
                commands.Add(Cmd(ServoNames.BothEyePop, pose.LeftEyePop));
            else
            {
                commands.Add(Cmd(ServoNames.LeftEyePop, pose.LeftEyePop));
                commands.Add(Cmd(ServoNames.RightEyePop, pose.RightEyePop));
            }

            commands.Add(Cmd(ServoNames.Whip_Antenna_RaiseLower, pose.WhipRaiseLower));
            commands.Add(Cmd(ServoNames.Whip_Antenna_Rotate, pose.WhipRotate));
            commands.Add(Cmd(ServoNames.MFR_UpDown, pose.MfrUpDown));
            commands.Add(Cmd(ServoNames.MFR_Rotate, pose.MfrRotate));
            commands.Add(Cmd(ServoNames.Microphone_RaiseLower, pose.MicrophoneRaiseLower));

            string poseRgb = (pose.RgbCommand ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(poseRgb))
                commands.Add(TextCmd(ServoNames.RGBCommand, poseRgb));

            return commands;
        }

        /// <summary>Insert the exact logical pose currently visible in the active
        /// URDF viewer. Joined values are emitted as compact ganged commands;
        /// asymmetric/split values are emitted as individual RobotControl commands
        /// so the pose survives round-tripping through the normal timeline model.</summary>
        private void InsertPoseAtCursor()
        {
            RobotHeadView view = _urdfUndocked ? _head?.HeadView : EmbeddedHeadView;
            if (view == null) return;

            RobotPoseSnapshot pose = view.CapturePose();
            double t = ServoCommand.TimeKey(_cursorTime);
            var commands = BuildCommandsFromPose(pose, t);

            PushUndo($"Insert URDF pose at {t:F3} s");

            // Keep earlier candidates until the conflict chooser has shown
            // their values alongside the newly inserted pose commands.
            _doc.Commands.AddRange(commands);
            ClearManualPoseOverrides();
            if (!RefreshAfterEdit()) return;
            SetCursor(t);
            ShowStatus($"URDF pose insertion completed at {t:F3} s");
        }

        private void SaveLibraryPose(RobotHeadView view)
        {
            if (view == null || !view.PoseEditorActive) return;

            string folder = LibraryCommandsFolder();
            try { Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not create the Library\\Commands folder:\n" + ex.Message,
                                "Create Library Pose", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var prompt = new LibraryCommandSaveWindow { Owner = this };
            if (prompt.ShowDialog() != true) return;

            string path = Path.Combine(folder, prompt.FileNameText);
            if (File.Exists(path))
            {
                var overwrite = MessageBox.Show(this,
                    $"'{prompt.FileNameText}' already exists. Replace it?",
                    "Create Library Pose", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (overwrite != MessageBoxResult.Yes) return;
            }

            try
            {
                LoadingWindow.Run(this, async () =>
                {
                    var commands = BuildCommandsFromPose(view.CapturePose(), 0.0);
                    var image = string.IsNullOrWhiteSpace(prompt.ImageSourcePath)
                        ? view.CaptureCenteredLibraryPoseImage() : null;
                    await Task.Run(() =>
                    {
                        string imageFile;
                        string oldImagePath = null;
                        if (File.Exists(path))
                        {
                            try
                            {
                                var old = AnimationDocument.LoadLibraryItem(path);
                                if (!string.IsNullOrWhiteSpace(old.ImageFile))
                                {
                                    oldImagePath = Path.IsPathRooted(old.ImageFile)
                                        ? old.ImageFile
                                        : Path.Combine(Path.GetDirectoryName(path) ?? "", old.ImageFile);
                                }
                            }
                            catch { }
                        }

                        string destination;
                        if (!string.IsNullOrWhiteSpace(prompt.ImageSourcePath))
                        {
                            // A user-supplied image always wins. Automatic URDF capture is
                            // intentionally skipped when Attach Image was used in the save dialog.
                            string ext = Path.GetExtension(prompt.ImageSourcePath);
                            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                            imageFile = Path.GetFileNameWithoutExtension(path) + "_image" + ext.ToLowerInvariant();
                            destination = Path.Combine(Path.GetDirectoryName(path) ?? folder, imageFile);
                            if (!Path.GetFullPath(prompt.ImageSourcePath).Equals(
                                    Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                                File.Copy(prompt.ImageSourcePath, destination, overwrite: true);
                        }
                        else
                        {
                            // Library + automatically creates a clean visual reference of
                            // the current Pose. The RobotHeadView temporarily centers its
                            // camera, hides editor chrome, crops to the head/flaps/neck,
                            // saves the PNG, and restores the user's live view.
                            imageFile = Path.GetFileNameWithoutExtension(path) + "_image.png";
                            destination = Path.Combine(Path.GetDirectoryName(path) ?? folder, imageFile);
                            RobotHeadView.SaveLibraryPoseBitmap(image, destination);
                        }

                        if (!string.IsNullOrWhiteSpace(oldImagePath) &&
                            string.Equals(Path.GetDirectoryName(Path.GetFullPath(oldImagePath)), Path.GetDirectoryName(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFullPath(oldImagePath).Equals(
                                Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(oldImagePath))
                        {
                            try { File.Delete(oldImagePath); } catch { }
                        }

                        AnimationDocument.SaveLibraryCommand(path, commands, prompt.DescriptionText, imageFile);
                    });
                }, "Saving Library Pose and image…");
                ShowStatus($"Library Pose saved: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the Library Pose:\n" + ex.Message,
                                "Create Library Pose", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLibraryPose(RobotHeadView view)
        {
            if (view == null) return;
            var win = new LibraryItemSelectionWindow(
                LibraryCommandsFolder(), manageMode: false,
                itemLabel: "Library Pose", showAudioFiles: false,
                selectActionText: "Load Selected Pose")
            {
                Owner = this,
            };
            if (win.ShowDialog() != true || win.SelectedLibraryItem == null) return;

            try
            {
                var commands = AnimationDocument.LoadCommandsOnly(win.SelectedLibraryItem.FullPath);
                if (commands.Count == 0)
                {
                    MessageBox.Show(this, "The selected Library Pose contains no commands.",
                                    "Load Library Pose", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                RobotPoseSnapshot pose = PoseFromLibraryCommands(commands);
                ForEachHeadView(v => v.LoadPoseSnapshot(pose));
                if (!string.IsNullOrWhiteSpace(pose.RgbCommand))
                    HeadView_PoseRgbCommandChanged(pose.RgbCommand);
                ShowStatus($"Loaded Library Pose '{Path.GetFileName(win.SelectedLibraryItem.FullPath)}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load the Library Pose:\n" + ex.Message,
                                "Load Library Pose", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static RobotPoseSnapshot PoseFromLibraryCommands(IEnumerable<ServoCommand> commands)
        {
            var pose = new RobotPoseSnapshot { LRJoined = true };
            bool split = false;

            foreach (var c in commands)
            {
                if (c.Disable) continue;
                double v = c.NumericValue;
                switch (c.Servo)
                {
                    case ServoNames.EyesHorizontalRight:
                        if (c.Control == null) pose.LeftEyeHorizontal = pose.RightEyeHorizontal = v;
                        else if (c.Control == RobotControls.LeftLensHorizontal) { pose.LeftEyeHorizontal = v; split = true; }
                        else if (c.Control == RobotControls.RightLensHorizontal) { pose.RightEyeHorizontal = v; split = true; }
                        break;
                    case ServoNames.EyesVerticalUp:
                        if (c.Control == null) pose.LeftEyeVertical = pose.RightEyeVertical = v;
                        else if (c.Control == RobotControls.LeftLensVertical) { pose.LeftEyeVertical = v; split = true; }
                        else if (c.Control == RobotControls.RightLensVertical) { pose.RightEyeVertical = v; split = true; }
                        break;
                    case ServoNames.IrisClose:
                        if (c.Control == null) pose.LeftIris = pose.RightIris = v;
                        else if (c.Control == RobotControls.LeftIris) { pose.LeftIris = v; split = true; }
                        else if (c.Control == RobotControls.RightIris) { pose.RightIris = v; split = true; }
                        break;
                    case ServoNames.FlapsOpen:
                        if (c.Control == null)
                            pose.LeftTopFlapOpen = pose.RightTopFlapOpen =
                                pose.LeftBottomFlapOpen = pose.RightBottomFlapOpen = v;
                        else
                        {
                            split = true;
                            if (c.Control == RobotControls.BrowLeftTopOpen) pose.LeftTopFlapOpen = v;
                            else if (c.Control == RobotControls.BrowRightTopOpen) pose.RightTopFlapOpen = v;
                            else if (c.Control == RobotControls.BrowLeftBottomOpen) pose.LeftBottomFlapOpen = v;
                            else if (c.Control == RobotControls.BrowRightBottomOpen) pose.RightBottomFlapOpen = v;
                        }
                        break;
                    case ServoNames.FlapTiltUp:
                        if (c.Control == null) pose.LeftTopFlapTilt = pose.RightTopFlapTilt = v;
                        else if (c.Control == RobotControls.BrowLeftTopTilt) { pose.LeftTopFlapTilt = v; split = true; }
                        else if (c.Control == RobotControls.BrowRightTopTilt) { pose.RightTopFlapTilt = v; split = true; }
                        break;
                    case ServoNames.VentsOpen:
                        if (c.Control == null) pose.LeftVent = pose.RightVent = v;
                        else if (c.Control == RobotControls.LeftEyeVent) { pose.LeftVent = v; split = true; }
                        else if (c.Control == RobotControls.RightEyeVent) { pose.RightVent = v; split = true; }
                        break;
                    case ServoNames.NeckTurn: pose.NeckTurn = v; break;
                    case ServoNames.NeckNodUp: pose.NeckOwner = ServoNames.NeckNodUp; pose.NeckNod = v; break;
                    case ServoNames.NeckTiltRight: pose.NeckOwner = ServoNames.NeckTiltRight; pose.NeckTilt = v; break;
                    case ServoNames.NoseBody: pose.NoseBody = v; break;
                    case ServoNames.NoseBasket: pose.NoseBasket = v; break;
                    case ServoNames.BothEyePop: pose.LeftEyePop = pose.RightEyePop = v; break;
                    case ServoNames.LeftEyePop: pose.LeftEyePop = v; split = true; break;
                    case ServoNames.RightEyePop: pose.RightEyePop = v; split = true; break;
                    case ServoNames.Whip_Antenna_RaiseLower: pose.WhipRaiseLower = v; break;
                    case ServoNames.Whip_Antenna_Rotate: pose.WhipRotate = v; break;
                    case ServoNames.MFR_UpDown: pose.MfrUpDown = v; break;
                    case ServoNames.MFR_Rotate: pose.MfrRotate = v; break;
                    case ServoNames.Microphone_RaiseLower: pose.MicrophoneRaiseLower = v; break;
                    case ServoNames.RGBCommand: pose.RgbCommand = c.TextValue ?? string.Empty; break;
                }
            }

            pose.LRJoined = !split;
            return pose;
        }

        #endregion

        // ================================================================
        #region 6b. Spline system
        // ================================================================

        private static bool IsSharedNeckServo(ServoNames servo) =>
            servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight;

        /// <summary>Legend show/hide checkbox toggled: remember and redraw.</summary>
        private void SplineShowAll_Click(object sender, RoutedEventArgs e)
        {
            RevealSplineLines(_legend, _lineVisible);
            RebuildSplineData();
        }

        internal static void RevealSplineLines(IEnumerable<SplineLegendItem> legend, IDictionary<ServoNames, bool> visibility)
        {
            foreach (var item in legend)
            {
                item.Visible = true;
                visibility[item.Servo] = true;
            }
        }

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
            if (!_dragUndoPushed) { PushUndo($"Change {servo} spline value at {timeKey:F3} s"); _dragUndoPushed = true; }
            foreach (var c in _doc.Commands.Where(c =>
                         c.Servo == servo &&
                         ServoCommand.TimeKey(c.OffsetSeconds) == timeKey))
                c.NumericValue = value;   // clamped per-servo by the model

            RebuildSplineData();
            UpdateServoState(_cursorTime);
        }

        /// <summary>
        /// RIGHT-drag on a spline point: move the command(s) at oldKey to
        /// newKey (the SplineView already refused collisions with another
        /// point of the same servo). Markers are refreshed live so the '+'
        /// follows the drag on the waveform.
        /// </summary>
        private void Spline_PointTimeChanged(ServoNames servo, double oldKey, double newKey)
        {
            if (!_dragUndoPushed) { PushUndo($"Move {servo} spline point at {oldKey:F3} s"); _dragUndoPushed = true; }
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
            // Retain the shared-neck ownership rule. Same-target duplicates
            // are instead offered to the normal conflict chooser below.
            if (IsSharedNeckServo(servo) && _doc.Commands.Any(c =>
                IsSharedNeckServo(c.Servo) && c.Servo != servo &&
                !c.Control.HasValue && !c.Disable &&
                ServoCommand.TimeKey(c.OffsetSeconds) == timeKey)) return;

            PushUndo($"Add {servo} spline point at {timeKey:F3} s");

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
            PushUndo($"Delete {servo} spline point at {timeKey:F3} s");
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

        private void ApplySplineSettings(IEnumerable<ServoNames> servos)
        {
            var enabled = servos.ToHashSet();
            if (enabled.Contains(ServoNames.NeckNodUp) || enabled.Contains(ServoNames.NeckTiltRight))
            {
                enabled.Add(ServoNames.NeckNodUp);
                enabled.Add(ServoNames.NeckTiltRight);
            }
            foreach (var row in _rows) row.SplineEnabled = !row.IsTextRow && enabled.Contains(row.Servo);
            _doc.SplineServos = SplineServosEnabled().Select(s => s.ToString()).ToList();
        }

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

        /// <summary>NeckNodUp and NeckTiltRight are two logical modes of the
        /// same physical servo pair. Their ganged commands therefore form one
        /// time-ordered spline. If legacy data contains both at the exact same
        /// time, NeckTiltRight wins, matching the established timeline rule.</summary>
        private (double[] T, double[] V, ServoNames[] Owners) SharedNeckSplinePoints()
        {
            return (_sharedNeckTimes, _sharedNeckValues, _sharedNeckOwners);
        }

        /// <summary>Evaluate the shared neck stream at an arbitrary timeline
        /// time. Ownership is the most recent neck command at/before the time;
        /// when spline mode is enabled the value comes from the one common
        /// Hermite curve rather than from separate Nod/Tilt curves.</summary>
        private (ServoNames? Owner, double Value) SharedNeckStateAt(double time)
        {
            var (t, v, owners) = SharedNeckSplinePoints();
            if (t.Length == 0)
            {
                if (_movieCarryPose?.NeckOwner is ServoNames carriedOwner &&
                    _movieCarryPose.Values.TryGetValue(carriedOwner, out double carriedValue))
                    return (carriedOwner, carriedValue);
                return (null, 0);
            }

            int ownerIndex = UpperBound(t, time + 1e-9) - 1;
            if (ownerIndex < 0)
            {
                if (_movieCarryPose?.NeckOwner is ServoNames carriedOwner &&
                    _movieCarryPose.Values.TryGetValue(carriedOwner, out double carriedValue))
                    return (carriedOwner, carriedValue);
                return (null, 0);
            }

            double value = v[ownerIndex];
            bool splineEnabled = _rows.First(r => r.Servo == ServoNames.NeckNodUp).SplineEnabled ||
                                 _rows.First(r => r.Servo == ServoNames.NeckTiltRight).SplineEnabled;
            if (splineEnabled && t.Length >= 2)
            {
                value = Math.Clamp(SplineUtil.Eval(t, v, _sharedNeckTangents, time), -100, 100);
            }
            return (owners[ownerIndex], value);
        }

        private static int UpperBound(double[] values, double value)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (values[mid] <= value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        private void RebuildPlaybackIndexes()
        {
            _orderedCommands = _doc.Commands
                .Select((command, index) => (command, index))
                .OrderBy(x => x.command.OffsetSeconds)
                .ThenBy(x => x.index)
                .Select(x => x.command)
                .ToArray();

            _gangCommandIndex.Clear();
            _gangSpeedCommandIndex.Clear();
            _childCommandIndex.Clear();

            foreach (var group in _orderedCommands
                .Where(c => !c.Disable && !c.Control.HasValue)
                .GroupBy(c => c.Servo))
            {
                ServoCommand[] commands = group.ToArray();
                _gangCommandIndex[group.Key] = commands;
                _gangSpeedCommandIndex[group.Key] = commands
                    .Where(c => c.Speed != ServoSpeed.NoChange)
                    .ToArray();
            }

            foreach (var group in _orderedCommands
                .Where(c => !c.Disable && c.Control.HasValue)
                .GroupBy(c => (c.Servo, c.Control.Value)))
                _childCommandIndex[group.Key] = group.ToArray();

            var neckPoints = _orderedCommands
                .Where(c => IsSharedNeckServo(c.Servo) &&
                            !c.Control.HasValue && !c.Disable)
                .GroupBy(c => ServoCommand.TimeKey(c.OffsetSeconds))
                .Select(g =>
                {
                    ServoCommand chosen = g.LastOrDefault(c =>
                        c.Servo == ServoNames.NeckTiltRight) ?? g.Last();
                    return (Time: ServoCommand.TimeKey(chosen.OffsetSeconds),
                            Value: (double)chosen.NumericValue,
                            Owner: chosen.Servo);
                })
                .OrderBy(p => p.Time)
                .ToArray();
            _sharedNeckTimes = neckPoints.Select(p => p.Time).ToArray();
            _sharedNeckValues = neckPoints.Select(p => p.Value).ToArray();
            _sharedNeckOwners = neckPoints.Select(p => p.Owner).ToArray();
            _sharedNeckTangents = _sharedNeckTimes.Length >= 2
                ? SplineUtil.Tangents(_sharedNeckTimes, _sharedNeckValues)
                : Array.Empty<double>();
        }

        internal static System.Windows.Media.Brush BrushFor(ServoNames s) =>
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
            RebuildPlaybackIndexes();
            var enabled = SplineServosEnabled();
            ApplyTimelineLayout();

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
            // checked state is visible. NeckNodUp/NeckTiltRight are the one
            // exception: they are rendered as ONE shared curve whose segment
            // color identifies the current logical owner.
            SplineShowAllButton.Visibility = _legend.Any(item => !item.Visible)
                ? Visibility.Visible : Visibility.Collapsed;

            var curves = new List<SplineCurve>();

            bool neckEnabled = enabled.Any(IsSharedNeckServo);
            if (neckEnabled)
            {
                var (t, v, owners) = SharedNeckSplinePoints();
                if (t.Length > 0)
                {
                    var (mn, mx) = ServoCommand.RangeFor(ServoNames.NeckNodUp);
                    bool nodVisible = !_lineVisible.TryGetValue(ServoNames.NeckNodUp, out bool nv) || nv;
                    bool tiltVisible = !_lineVisible.TryGetValue(ServoNames.NeckTiltRight, out bool tv) || tv;
                    curves.Add(new SplineCurve
                    {
                        Servo = ServoNames.NeckNodUp,
                        Color = SkColorFor(ServoNames.NeckNodUp),
                        T = t,
                        V = v,
                        M = SplineUtil.Tangents(t, v),
                        Min = mn,
                        Max = mx,
                        Visible = nodVisible || tiltVisible,
                        IsSharedNeck = true,
                        Owners = owners,
                        PointColors = owners.Select(SkColorFor).ToArray(),
                        PointVisible = owners.Select(o => o == ServoNames.NeckNodUp
                            ? nodVisible : tiltVisible).ToArray(),
                    });
                }
            }

            foreach (var s in enabled.Where(s => !IsSharedNeckServo(s)))
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
            Spline.InvalidateVisual();
            _splineCurveIndex.Clear();
            foreach (SplineCurve curve in curves)
                _splineCurveIndex[curve.Servo] = curve;
            SyncSplineView();
        }

        /// <summary>Copy the waveform's zoom/scroll/cursor into the spline
        /// view so the two strips always line up.</summary>
        private void SyncSplineView()
        {
            if (Spline == null) return;
            bool viewportChanged = Spline.ViewStart != Waveform.ViewStart ||
                Spline.PixelsPerSecond != Waveform.PixelsPerSecond || Spline.Duration != TimelineDuration;
            Spline.ViewStart = Waveform.ViewStart;
            Spline.PixelsPerSecond = Waveform.PixelsPerSecond;
            Spline.CursorTime = _cursorTime;
            Spline.Duration = TimelineDuration;
            if (viewportChanged) Spline.InvalidateVisual();
            Spline.InvalidateCursor();
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

        /// <summary>Save-time sampler for the one shared NeckNodUp /
        /// NeckTiltRight spline. Every generated sample is emitted under the
        /// logical owner that was most recently selected by a control point,
        /// so exported playback observes the exact same hand-off rule as the
        /// editor and URDF preview.</summary>
        private IEnumerable<ServoCommand> GenerateSharedNeckSplineSamples()
        {
            var (t, v, owners) = SharedNeckSplinePoints();
            if (t.Length < 2) yield break;

            var m = SplineUtil.Tangents(t, v);
            var controlKeys = new HashSet<double>(t.Select(ServoCommand.TimeKey));
            double dt = 1.0 / _splineHz;
            int nextControl = 1;
            int lastValue = (int)Math.Round(v[0]);

            for (double x = t[0] + dt; x < t[^1] - 1e-9; x += dt)
            {
                while (nextControl < t.Length && t[nextControl] <= x + 1e-9)
                {
                    lastValue = (int)Math.Round(v[nextControl]);
                    nextControl++;
                }

                double key = ServoCommand.TimeKey(x);
                if (controlKeys.Contains(key)) continue;

                int value = (int)Math.Round(Math.Clamp(
                    SplineUtil.Eval(t, v, m, x), -100, 100));
                if (value == lastValue) continue;

                int ownerIndex = Math.Clamp(nextControl - 1, 0, owners.Length - 1);
                lastValue = value;
                yield return new ServoCommand
                {
                    OffsetSeconds = key,
                    Servo = owners[ownerIndex],
                    NumericValue = value,
                    Speed = ServoSpeed.NoChange,
                    Reason = $"shared neck spline {_splineHz}Hz",
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
        /// files, resolving all persisted references relative to Config.
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
            if (!RequireConfigPath(dlg.FileName, "sequence")) return;
            LoadSequenceFromPath(dlg.FileName, 0, fitTimeline: true, recordRecent: true, setActiveDocument: true);
        }

        /// <summary>Loads a sequence from a known pathname. Movie selection
        /// uses the same path, checking file stamps so external edits invalidate
        /// cached data while unchanged sequences reuse prepared assets.</summary>
        private bool LoadSequenceFromPath(string path, double initialCursor,
                                          bool fitTimeline,
                                          bool recordRecent = true,
                                          bool setActiveDocument = true,
                                          bool preserveMoviePose = false,
                                          bool preservePending = false)
        {
            if (!RequireConfigPath(path, "sequence")) return false;
            if (!ApplyOpenCommandEditor()) return false;
            if (setActiveDocument && !ConfirmDocumentSwitch(replaceMovie: false)) return false;
            try
            {
                StopPlayback(cancelPending: !preservePending);
                // Prepare before replacing the editor document; a failed load
                // leaves the current sequence and its undo history intact.
                var prepared = LoadingWindow.Wait(this, _mediaCache.PrepareSequence(path, ConfigRoot),
                    "Preparing sequence and audio…").Clone();
                bool resolvedConflicts = CommandConflicts.Find(prepared.Commands).Count > 0;
                if (!CommandConflictWindow.Resolve(this, prepared.Commands)) return false;
                _reader?.Dispose();
                _reader = null;
                _audioPath = null;
                _primaryDuration = 0;
                _activeSource = null;
                _lastDesiredKey = null;

                if (!preserveMoviePose)
                {
                    _movieCarryPose = null;
                    _rgbSimulator.SetInitialFrame(null);
                }

                _doc = prepared;
                _pendingMovieSequence = null;
                RebuildPlaybackIndexes();
                _rgbSimulator.Invalidate();
                _jsonPath = path;
                RememberSequencePath(path);
                _undoStack.Clear();
                _redoStack.Clear();

                SetDescriptionText(_doc.Description);

                foreach (var row in _rows)
                    row.SplineEnabled = !row.IsTextRow &&
                        (_doc.SplineServos?.Contains(row.Servo.ToString()) ?? false);

                // Backward compatibility: an older project may have enabled
                // only one of the two neck splines. They are one shared curve
                // now, so loading either name enables both rows.
                var nodSpline = _rows.First(r => r.Servo == ServoNames.NeckNodUp);
                var tiltSpline = _rows.First(r => r.Servo == ServoNames.NeckTiltRight);
                if (nodSpline.SplineEnabled || tiltSpline.SplineEnabled)
                    nodSpline.SplineEnabled = tiltSpline.SplineEnabled = true;

                _splineHz = Array.IndexOf(SplineHzOptions, _doc.SplineSampleHz) >= 0
                    ? _doc.SplineSampleHz : 50;

                _audioOffset = Math.Max(0, _doc.AudioStartOffsetSeconds);
                Waveform.PrimaryAudioName = "";
                Waveform.AudioOffset = _audioOffset;
                Waveform.SetAudio(null, null, 0.001, 0);

                var missingClips = RefreshAudioClips();
                bool hasMissingFiles = missingClips.Count > 0;

                string storedAudio = !string.IsNullOrWhiteSpace(_doc.AudioFilePath)
                    ? _doc.AudioFilePath : _doc.AudioFile;
                string candidate = ResolveSequenceAudioPath(_doc, path, storedAudio);

                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    LoadAudio(candidate, stopPlayback: false);
                }
                else if (!string.IsNullOrWhiteSpace(_doc.AudioFilePath) ||
                         !string.IsNullOrWhiteSpace(_doc.AudioFile))
                {
                    hasMissingFiles = true;
                }

                Waveform.AudioOffset = _audioOffset;
                Waveform.Duration = TimelineDuration;
                Waveform.ContentDuration = ContentEnd;

                SetCursor(Math.Clamp(initialCursor, 0, ContentEnd));
                RefreshAfterEdit();
                if (fitTimeline) Waveform.ZoomToFit();
                SyncScrollBar();
                UpdateTitle();
                _savedSequenceFingerprint = resolvedConflicts ? "<resolved-command-conflicts>" : CurrentSequenceFingerprint();
                UpdateDocumentStatusIndicators();
                if (setActiveDocument)
                    _activeDocumentKind = ActiveDocumentKind.Sequence;
                if (recordRecent)
                    RecordRecentFile(path, ActiveDocumentKind.Sequence, setActiveDocument);
                ShowStatus($"Sequence loaded: {Path.GetFileName(path)}");
                if (hasMissingFiles)
                    ScheduleMissingFileRepair();
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
            else if (CurrentMovieDraft != null)
                projDefault = CurrentMovieDraft.Name + ".json";
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
            if (!RequireConfigPath(dlg.FileName, "sequence")) return false;
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
                _doc.ScaleValues, defaultPath, ConfigRoot)
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
            _doc.Description ??= "";
            _doc.AudioStartOffsetSeconds = _audioOffset;
            _doc.SplineServos = SplineServosEnabled().Select(s => s.ToString()).ToList();
            _doc.SplineSampleHz = _splineHz;
            if (_primaryDuration > 0)
            {
                _doc.AudioFilePath = ConfigPathService.ToRelative(ConfigRoot, _audioPath);
                _doc.AudioFile = Path.GetFileName(_audioPath);
                // Total timeline length = pre-roll offset + audio length.
                _doc.DurationSeconds = Math.Round(
                    _audioOffset + _primaryDuration, 2);
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
            if (!ApplyOpenCommandEditor()) return false;
            if (!RequireConfigPath(path, "sequence")) return false;
            _doc.AudioFiles = BuildAudioFilesHeader();
            string oldPath = _jsonPath;

            try
            {
                SyncDocMetadata();
                ConfigPathService.MakeDocumentPathsRelative(_doc, ConfigRoot);
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
                bool appended = CurrentMovieDraft?.AppendAfterSave(_doc, _moviePath, path, ContentEnd, _movieItems) == true;
                if (appended)
                {
                    _pendingMovieSequence = null;
                    _movieSelectedIndex = _movieItems.Count - 1;
                }
                RefreshMovieDurationForPath(path);
                if (appended)
                {
                    RefreshMovieTimelineView();
                    MovieTimeline.CursorTime = MovieTimeline.StartOf(_movieSelectedIndex) + _cursorTime;
                    MovieTimeline.EnsureVisible(MovieTimeline.CursorTime);
                    MovieTimeline.InvalidateVisual();
                }
                UpdateDocumentStatusIndicators();
                if (_activeDocumentKind != ActiveDocumentKind.Movie)
                {
                    _activeDocumentKind = ActiveDocumentKind.Sequence;
                    RecordRecentFile(path, ActiveDocumentKind.Sequence, setActive: true);
                }
                ShowStatus(appended
                    ? $"Sequence saved and added to Movie: {Path.GetFileName(path)} — save the Movie to keep its updated sequence list"
                    : $"Sequence saved: {Path.GetFileName(path)}");
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
            if (!ApplyOpenCommandEditor()) return;
            if (!RequireConfigPath(path, "animation")) return;
            try
            {
                SyncDocMetadata();
                ConfigPathService.MakeDocumentPathsRelative(_doc, ConfigRoot);

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

                bool sharedNeckSampled = false;
                foreach (var servo in SplineServosEnabled())
                {
                    if (IsSharedNeckServo(servo))
                    {
                        if (!sharedNeckSampled)
                        {
                            export.Commands.AddRange(GenerateSharedNeckSplineSamples());
                            sharedNeckSampled = true;
                        }
                        continue;
                    }
                    export.Commands.AddRange(GenerateSplineSamples(servo));
                }

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

                if (!CommandConflictWindow.Resolve(this, export.Commands)) return;

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

            PushUndo("Clear timeline");
            _doc.Commands.Clear();
            RefreshAfterEdit();
        }

        /// <summary>Update the authored Sequence description and its read-only header.</summary>
        private void SetDescriptionText(string text)
        {
            string value = text ?? "";
            if (_doc != null) _doc.Description = value;
            if (SequenceDescriptionRun != null) SequenceDescriptionRun.Text = value;
        }

        private void SequenceDescriptionEdit_Click(object sender, RoutedEventArgs e)
        {
            var editor = new DescriptionEditWindow(this, "Sequence", _doc?.Description ?? "");
            if (editor.ShowDialog() != true ||
                string.Equals(editor.DescriptionText, _doc?.Description ?? "", StringComparison.Ordinal)) return;
            SetDescriptionText(editor.DescriptionText);
            RefreshMovieDescriptionForPath(_jsonPath, editor.DescriptionText);
            UpdateDocumentStatusIndicators();
        }

        /// <summary>Window title and compact file/dirty indicators.</summary>
        private void UpdateTitle()
        {
            string file = string.IsNullOrEmpty(_jsonPath) ? "" : $" — {Path.GetFileName(_jsonPath)}";
            string dirty = SequenceHasUnsavedChanges() ? "*" : "";
            Title = AppDisplayName + file + dirty;
        }

        private string CurrentMovieFingerprint()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_movieDescription ?? "");
            sb.AppendLine(_movieCreatedDate ?? "");
            foreach (var item in _movieItems)
                sb.AppendLine((item.FilePath ?? "") + "\t" + (item.IsLooping ? "loop" : "once") + "\t" + (item.Trigger ?? ""));
            return sb.ToString();
        }

        private bool MovieHasUnsavedChanges() =>
            !string.Equals(_savedMovieFingerprint ?? "", CurrentMovieFingerprint(), StringComparison.Ordinal);

        private string CurrentConfigurationFingerprint()
        {
            try
            {
                return JsonSerializer.Serialize(_servoConfig) + "\n" +
                       JsonSerializer.Serialize(_urdfConfig);
            }
            catch { return ""; }
        }

        private bool ConfigurationHasUnsavedChanges() =>
            !string.Equals(_savedConfigurationFingerprint ?? "",
                           CurrentConfigurationFingerprint(), StringComparison.Ordinal);

        private void MarkConfigurationSaved()
        {
            _savedConfigurationFingerprint = CurrentConfigurationFingerprint();
            UpdateDocumentStatusIndicators();
        }

        private void UpdateDocumentStatusIndicators()
        {
            bool sequenceDirty = SequenceHasUnsavedChanges();
            // A new draft has no Movie block yet, so keep its header and editor visible.
            if (SequenceDescriptionPanel != null)
                SequenceDescriptionPanel.Visibility = _movieItems.Any(item => PathsEqual(item.FilePath, _jsonPath))
                    ? Visibility.Collapsed : Visibility.Visible;
            if (SequenceFileText != null)
            {
                string name = string.IsNullOrWhiteSpace(_jsonPath) ? CurrentMovieDraft?.Name ?? "(unsaved)" : Path.GetFileNameWithoutExtension(_jsonPath);
                SequenceFileText.Text = $"Sequence: {name}{(sequenceDirty ? "*" : "")} — {ContentEnd:0.###} s";
                SequenceFileText.ToolTip = string.IsNullOrWhiteSpace(_jsonPath) ? "Unsaved sequence" : _jsonPath;
            }
            bool blockStatusChanged = false;
            foreach (var item in _movieItems)
            {
                bool modified = sequenceDirty && PathsEqual(item.FilePath, _jsonPath);
                if (item.IsModified == modified) continue;
                item.IsModified = modified;
                blockStatusChanged = true;
            }
            if (blockStatusChanged) MovieTimeline?.InvalidateVisual();
            if (MovieFileText != null)
            {
                string name = string.IsNullOrWhiteSpace(_moviePath) ? "(unsaved)" : Path.GetFileNameWithoutExtension(_moviePath);
                MovieFileText.Text = $"{name}{(MovieHasUnsavedChanges() ? "*" : "")} — {MovieTimeline.TotalDuration:0.###} s";
                MovieFileText.ToolTip = string.IsNullOrWhiteSpace(_moviePath) ? "Unsaved movie" : _moviePath;
            }
            UpdateTitle();
        }

        private void ShowStatus(string text)
        {
            Debug.WriteLine(text);
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
        private void PushUndo(string description)
        {
            _undoStack.Add(new UndoEntry(Snapshot(),
                string.IsNullOrWhiteSpace(description) ? "Edit timeline" : description.Trim(), _audioOffset, SplineServosEnabled()));
            if (_undoStack.Count > UndoLimit) _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        private void RestoreSnapshot(List<ServoCommand> snap, bool refresh = true, double? audioOffset = null, List<ServoNames> splines = null)
        {
            _doc.Commands = snap.Select(c => c.Clone()).ToList();
            if (splines != null) ApplySplineSettings(splines);
            if (audioOffset.HasValue)
            {
                _audioOffset = audioOffset.Value;
                _doc.AudioStartOffsetSeconds = _audioOffset;
                Waveform.AudioOffset = _audioOffset;
            }
            if (refresh) RefreshAfterEdit();
        }

        private void Undo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
            => e.CanExecute = _undoStack.Count > 0;

        private void Redo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
            => e.CanExecute = _redoStack.Count > 0;

        /// <summary>Undo: current state goes to the redo stack, the top undo
        /// snapshot becomes the timeline.</summary>
        private void Undo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            UndoSteps(1);
        }

        private void UndoSteps(int requestedSteps)
        {
            int steps = Math.Clamp(requestedSteps, 0, _undoStack.Count);
            if (steps == 0) return;

            string oldestDescription = _undoStack[^steps].Description;
            for (int i = 0; i < steps; i++)
            {
                UndoEntry entry = _undoStack[^1];
                _undoStack.RemoveAt(_undoStack.Count - 1);
                _redoStack.Add(new UndoEntry(Snapshot(), entry.Description, _audioOffset, SplineServosEnabled()));
                RestoreSnapshot(entry.Commands, refresh: false, audioOffset: entry.AudioOffset, splines: entry.Splines);
            }

            RefreshAfterEdit();
            CommandManager.InvalidateRequerySuggested();
            ShowStatus(steps == 1
                ? $"Undid: {oldestDescription}"
                : $"Undid {steps} actions through: {oldestDescription}");
        }

        private void UndoHistory_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            UndoHistoryMenuItem.Items.Clear();
            int count = Math.Min(10, _undoStack.Count);
            if (count == 0)
            {
                UndoHistoryMenuItem.Items.Add(new MenuItem
                {
                    Header = "No actions to undo",
                    IsEnabled = false,
                });
                return;
            }

            for (int steps = 1; steps <= count; steps++)
            {
                UndoEntry entry = _undoStack[^steps];
                var item = new MenuItem
                {
                    Header = $"{steps}. {entry.Description.Replace("_", "__")}",
                    ToolTip = steps == 1
                        ? "Undo this action"
                        : $"Undo this action and the {steps - 1} newer action(s)",
                    Tag = steps,
                };
                item.Click += UndoHistoryItem_Click;
                UndoHistoryMenuItem.Items.Add(item);
            }
        }

        private void UndoHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: int steps })
                UndoSteps(steps);
        }

        /// <summary>Redo: puts the last undone change back.</summary>
        private void Redo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            UndoEntry entry = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(new UndoEntry(Snapshot(), entry.Description, _audioOffset, SplineServosEnabled()));
            if (_undoStack.Count > UndoLimit) _undoStack.RemoveAt(0);
            RestoreSnapshot(entry.Commands, audioOffset: entry.AudioOffset, splines: entry.Splines);
            CommandManager.InvalidateRequerySuggested();
            ShowStatus($"Redid: {entry.Description}");
        }

        #endregion

        // ================================================================
        #region 9. File menu (New / Exit) + About
        // ================================================================

        /// <summary>File > New: clears the current Sequence and Movie -
        /// commands, audio, descriptions, movie blocks, offsets, spline state
        /// and undo history - after confirmation.</summary>
        private void FileNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDocumentSwitch(replaceMovie: true)) return;
            var answer = MessageBox.Show(this,
                "Start a new project? This clears the current Sequence, Movie, timeline and audio.",
                "New project", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            EndMovieBackgroundControl(); SetPlaybackControlSource(null);
            StopPlayback();            // disposes the output device
            _reader?.Dispose(); _reader = null;
            _audioPath = null;
            _jsonPath = null;
            _activeDocumentKind = ActiveDocumentKind.None;
            _recentFiles?.ClearLastActive();
            SaveRecentFiles();

            _doc = new AnimationDocument();
            RebuildPlaybackIndexes();
            _pendingMovieSequence = null;
            _movieCarryPose = null;
            _rgbSimulator.SetInitialFrame(null);
            _rgbSimulator.Invalidate();
            SetDescriptionText(_doc.Description);

            // File > New starts a genuinely blank workspace. Do not leave the
            // newly created Sequence attached to the previously loaded Movie.
            _movieItems.Clear();
            _moviePath = null;
            _movieSelectedIndex = -1;
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            _movieDescription = "";
            _movieCreatedDate = DateTime.Today.ToString("yyyy-MM-dd");
            SetMovieDescriptionText("");
            if (MovieTimeline != null)
            {
                MovieTimeline.CursorTime = 0;
                MovieTimeline.SelectedIndex = -1;
            }
            if (MoviePlayButton != null)
                MoviePlayButton.Content = "▶ Movie";
            RefreshMovieTimelineView();

            Waveform.PrimaryAudioName = "";
            _primaryDuration = 0;
            _activeSource = null;
            _mediaCache.Clear();
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
            _savedMovieFingerprint = CurrentMovieFingerprint();
            UpdateDocumentStatusIndicators();
            ShowStatus("New Sequence and Movie workspace created");
        }

        /// <summary>Restore the last editor/window arrangement saved in the
        /// active Configuration folder. Document content is not part of this file.</summary>
        private void LoadEditorLayout()
        {
            var layout = EditorLayoutSettings.Load(_folders?.ConfigFolderOrDefault);
            _startupEditorLayout = layout;
            if (layout == null)
            {
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

                if (string.Equals(layout.WindowState, nameof(WindowState.Maximized), StringComparison.OrdinalIgnoreCase))
                    WindowState = WindowState.Maximized;

                _lastDockedServoColumnWidth = layout.ServoEditorColumn?.ToGridLength(new GridLength(1, GridUnitType.Star))
                    ?? new GridLength(1, GridUnitType.Star);
                _lastDockedUrdfColumnWidth = layout.UrdfEditorColumn?.ToGridLength(new GridLength(1, GridUnitType.Star))
                    ?? new GridLength(1, GridUnitType.Star);
                CommandsEditorColumn.Width = _lastDockedServoColumnWidth;
                UrdfEditorColumn.Width = _lastDockedUrdfColumnWidth;
                TopEditorRow.Height = layout.TopEditorRow?.ToGridLength(new GridLength(250))
                    ?? new GridLength(250);
                AudioTimelineRow.Height = layout.AudioTimelineRow?.ToGridLength(new GridLength(1, GridUnitType.Star))
                    ?? new GridLength(1, GridUnitType.Star);
                _lastSplineTimelineHeight = layout.LastSplineTimelineHeight?.ToGridLength(new GridLength(190))
                    ?? new GridLength(190);
                TimelineLayoutPicker.SelectedIndex = layout.TimelineLayoutDefaultsVersion >= 1 &&
                    Enum.TryParse<TimelineLayoutMode>(layout.TimelineLayout, out var timelineMode) &&
                    Enum.IsDefined(timelineMode) ? (int)timelineMode : (int)TimelineLayoutMode.Combined;
                ApplyTimelineLayout();

                if (EditorLayoutSettings.IsVisibleOnVirtualDesktop(
                    layout.UrdfWindowLeft, layout.UrdfWindowTop,
                    layout.UrdfWindowWidth, layout.UrdfWindowHeight))
                {
                    _savedUrdfWindowBounds = new Rect(
                        layout.UrdfWindowLeft, layout.UrdfWindowTop,
                        layout.UrdfWindowWidth, layout.UrdfWindowHeight);
                }
                _savedUrdfWindowState = string.Equals(layout.UrdfWindowState, nameof(WindowState.Maximized),
                    StringComparison.OrdinalIgnoreCase) ? WindowState.Maximized : WindowState.Normal;



                _embeddedUrdfHeightPixels = layout.EmbeddedUrdfHeightPixels > 0
                    ? layout.EmbeddedUrdfHeightPixels
                    : 0;
                ApplyEmbeddedUrdfHeight();
                EmbeddedHeadView?.ApplyCameraState(
                    layout.UrdfCameraYaw,
                    layout.UrdfCameraPitch,
                    layout.UrdfCameraDistance);
                SetUrdfUndocked(layout.UrdfUndocked);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[layout] Could not apply EditorLayout.json: {ex.Message}");
            }
        }

        private void FinishRestoringEditorLayout()
        {
            var layout = _startupEditorLayout;
            _startupEditorLayout = null;
            if (layout == null || !IsLoaded) return;

            // Wait until the document headers, spline visibility, maximized
            // client area, and docked/undocked hosts have their final arrangement.
            _lastTopEditorHeight = layout.TopEditorRow?.ToGridLength(new GridLength(250))
                ?? new GridLength(250);
            TopEditorRow.Height = _lastTopEditorHeight;
            if (!_urdfUndocked)
            {
                CommandsEditorColumn.Width = _lastDockedServoColumnWidth;
                UrdfEditorColumn.Width = _lastDockedUrdfColumnWidth;
            }
            _lastSplineTimelineHeight = layout.LastSplineTimelineHeight?.ToGridLength(new GridLength(190))
                ?? new GridLength(190);
            _timelineLayout?.RestoreHeights(
                layout.AudioTimelineRow?.ToGridLength(new GridLength(1, GridUnitType.Star))
                    ?? new GridLength(1, GridUnitType.Star),
                _lastSplineTimelineHeight);
            EditorTimelineGrid.UpdateLayout();
            ApplyEmbeddedUrdfHeight();

            EmbeddedHeadView?.ApplyCameraState(layout.UrdfCameraYaw, layout.UrdfCameraPitch, layout.UrdfCameraDistance);
            if (_urdfUndocked)
                _head?.HeadView.ApplyCameraState(layout.UrdfCameraYaw, layout.UrdfCameraPitch, layout.UrdfCameraDistance);
        }

        /// <summary>Save splitter positions, window placement and user-selectable
        /// editor panels into EditorLayout.json in the active Configuration folder.</summary>
        private bool SaveEditorLayout()
        {
            if (_folders == null) return true;

            try
            {
                Rect bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, ActualWidth, ActualHeight)
                    : RestoreBounds;

                if (_urdfUndocked && _head != null)
                {
                    _savedUrdfWindowBounds = _head.GetNormalBounds();
                    _savedUrdfWindowState = _head.LastNonMinimizedWindowState;
                }

                var dockedServoWidth = _urdfUndocked
                    ? _lastDockedServoColumnWidth
                    : CommandsEditorColumn.Width;
                var dockedUrdfWidth = _urdfUndocked
                    ? _lastDockedUrdfColumnWidth
                    : UrdfEditorColumn.Width;
                RobotHeadView cameraView = _urdfUndocked && _head != null
                    ? _head.HeadView
                    : EmbeddedHeadView;
                var camera = cameraView?.GetCameraState() ?? (0.0, 0.0, 1.15);

                var layout = new EditorLayoutSettings
                {
                    WindowLeft = bounds.Left,
                    WindowTop = bounds.Top,
                    WindowWidth = Math.Max(MinWidth, bounds.Width),
                    WindowHeight = Math.Max(MinHeight, bounds.Height),
                    WindowState = _lastNonMinimizedWindowState == WindowState.Maximized
                        ? nameof(WindowState.Maximized)
                        : nameof(WindowState.Normal),
                    ServoEditorColumn = GridLengthSetting.From(dockedServoWidth),
                    UrdfEditorColumn = GridLengthSetting.From(dockedUrdfWidth),
                    TopEditorRow = GridLengthSetting.From(TopEditorRow.Height.Value > 0 ? TopEditorRow.Height : _lastTopEditorHeight),
                    AudioTimelineRow = GridLengthSetting.From(_timelineLayout?.SavedAudioHeight ?? AudioTimelineRow.Height),
                    TimelineLayout = (_timelineLayout?.Mode ?? TimelineLayoutMode.Combined).ToString(),
                    TimelineLayoutDefaultsVersion = 1,
                    LastSplineTimelineHeight = GridLengthSetting.From(
                        _timelineLayout?.SavedSplineHeight ?? _lastSplineTimelineHeight),
                    EmbeddedUrdfHeightPixels = _embeddedUrdfHeightPixels,
                    UrdfCameraYaw = camera.Item1,
                    UrdfCameraPitch = camera.Item2,
                    UrdfCameraDistance = camera.Item3,
                    UrdfUndocked = _urdfUndocked,
                    UrdfWindowLeft = _savedUrdfWindowBounds.IsEmpty ? 120 : _savedUrdfWindowBounds.Left,
                    UrdfWindowTop = _savedUrdfWindowBounds.IsEmpty ? 120 : _savedUrdfWindowBounds.Top,
                    UrdfWindowWidth = _savedUrdfWindowBounds.IsEmpty ? 900 : _savedUrdfWindowBounds.Width,
                    UrdfWindowHeight = _savedUrdfWindowBounds.IsEmpty ? 650 : _savedUrdfWindowBounds.Height,
                    UrdfWindowState = _savedUrdfWindowState.ToString(),
                };
                layout.Save(_folders.ConfigFolderOrDefault);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[layout] Could not save EditorLayout.json: {ex.Message}");
                return false;
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
                WatchMovieMetadata();
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
                RefreshSavedSpeedPredictions();
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
            _head.HeadView.PoseModeChanged += HeadView_PoseModeChanged;
            _head.HeadView.PoseRgbCommandChanged += HeadView_PoseRgbCommandChanged;
            _head.HeadView.LibraryPoseSaveRequested += HeadView_LibraryPoseSaveRequested;
            _head.HeadView.LibraryPoseLoadRequested += HeadView_LibraryPoseLoadRequested;
            _head.DockRequested += () => SetUrdfUndocked(false);

            if (EmbeddedHeadView != null)
            {
                _head.HeadView.SetCollisionWarningsEnabled(EmbeddedHeadView.CollisionWarningsEnabled);
                _head.HeadView.SetUrdfDriveEnabled(EmbeddedHeadView.UrdfDriveEnabled);
            }
            _head.HeadView.SetUrdfConfiguration(_urdfConfig);
            _head.HeadView.SetServoConfiguration(_servoConfig);
            ConfigureMotionView(_head.HeadView);

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

        /// <summary>Switch between embedded and detached URDF; Commands fills the freed width.</summary>
        private void SetUrdfUndocked(bool undocked)
        {
            if (_urdfUndocked == undocked)
            {
                ApplyUrdfDockLayout();
                return;
            }

            if (undocked)
            {

                // Remember the docked splitter ratio before collapsing the URDF
                // columns; restoring later should reproduce the user's layout.
                if (CommandsEditorColumn.Width.Value > 0)
                    _lastDockedServoColumnWidth = CommandsEditorColumn.Width;
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
                {
                    window.HeadView.SetUrdfDriveEnabled(EmbeddedHeadView.UrdfDriveEnabled);
                    window.HeadView.CopyCameraFrom(EmbeddedHeadView, makeOpeningView: true);
                    window.HeadView.CopyPoseEditorFrom(EmbeddedHeadView);
                }
                window.HeadView.SetDetachedHostState();
                if (!window.IsVisible)
                    window.Show();
                window.WindowState = _savedUrdfWindowState;
                window.Activate();
                if (!window.HeadView.PoseEditorActive)
                    PushHeadPose();
            }
            else if (_head != null)
            {
                _savedUrdfWindowBounds = _head.GetNormalBounds();
                _savedUrdfWindowState = _head.LastNonMinimizedWindowState;
                if (EmbeddedHeadView != null)
                {
                    EmbeddedHeadView.SetUrdfDriveEnabled(_head.HeadView.UrdfDriveEnabled);
                    EmbeddedHeadView.CopyCameraFrom(_head.HeadView);
                    EmbeddedHeadView.CopyPoseEditorFrom(_head.HeadView);
                }
                _head.Hide();
                EmbeddedHeadView?.SetUrdfConfiguration(_urdfConfig);
                EmbeddedHeadView?.SetServoConfiguration(_servoConfig);
                if (EmbeddedHeadView?.PoseEditorActive != true)
                    PushHeadPose();
            }
        }

        /// <summary>Commands occupy the former grid pane; keep its URDF divider and saved ratio.</summary>
        private void ApplyUrdfDockLayout()
        {
            if (CommandsAtPointPanel == null || RobotHeadEmbeddedBorder == null) return;
            TopEditorRow.MinHeight = 80;
            if (TopEditorRow.Height.Value <= 0) TopEditorRow.Height = _lastTopEditorHeight;
            CommandsTimelineSplitter.Visibility = Visibility.Visible;
            CommandsEditorColumn.MinWidth = 280;
            CommandsEditorColumn.Width = _urdfUndocked ? new GridLength(1, GridUnitType.Star) : _lastDockedServoColumnWidth;
            UrdfSplitterColumn.Width = new GridLength(_urdfUndocked ? 0 : 8);
            UrdfColumnSplitter.Visibility = _urdfUndocked ? Visibility.Collapsed : Visibility.Visible;
            UrdfEditorColumn.MinWidth = _urdfUndocked ? 0 : 260;
            UrdfEditorColumn.Width = _urdfUndocked ? new GridLength(0) : _lastDockedUrdfColumnWidth;
            Grid.SetColumnSpan(CommandsAtPointPanel, _urdfUndocked ? 3 : 1);
            CommandsAtPointPanel.Margin = new Thickness(6, 5, _urdfUndocked ? 6 : 0, 0);
            DockUrdfButton.Visibility = _urdfUndocked ? Visibility.Visible : Visibility.Collapsed;
            RobotHeadEmbeddedBorder.Visibility = _urdfUndocked ? Visibility.Collapsed : Visibility.Visible;
            UrdfOverlayHost.Visibility = _urdfUndocked ? Visibility.Collapsed : Visibility.Visible;
            // Recompute the separate mapping pane after resetting the dock columns.
            UndockedControllerMappingHost.Visibility = Visibility.Collapsed;
            RefreshActiveControllerMapping();
            if (_urdfUndocked) RobotHeadEmbeddedBorder.Height = double.NaN;
            else
            {
                ApplyEmbeddedUrdfHeight();
                EmbeddedHeadView?.SetDockedHostState();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(this,
                $"{AppDisplayName}\nVersion {AppVersion}   Date {AppGenerationDate}\nDesigned by Mark Kovalcson\n\n" +
                "Edits servo animation timelines against an audio waveform.\n" +
                "Cubic Hermite spline interpolation, live drive, an animation\n" +
                "library, and a movie timeline for ordered sequence projects.\n\n" +
                "Built with WPF, SkiaSharp and NAudio.",
                $"About {AppDisplayName}", MessageBoxButton.OK, MessageBoxImage.Information);


        private void HelpContents_Click(object sender, RoutedEventArgs e) =>
            HelpSystem.ShowContents(this);

        private void HelpContext_Click(object sender, RoutedEventArgs e) =>
            HelpSystem.ShowContextHelp(this);

        private void HelpControls_Click(object sender, RoutedEventArgs e) =>
            HelpSystem.ShowHelp(this, "controls-hotkeys");

        private void UpdateHelpAvailability()
        {
            bool enabled = _mode == PlayMode.Stopped;
            if (HelpContentsMenuItem != null) HelpContentsMenuItem.IsEnabled = enabled;
            if (HelpContextMenuItem != null) HelpContextMenuItem.IsEnabled = enabled;
            if (HelpControlsMenuItem != null) HelpControlsMenuItem.IsEnabled = enabled;
        }

        /// <summary>Attach F1 topics to major editor regions. Descendant controls
        /// inherit the nearest topic unless a child window supplies a narrower one.</summary>
        private void ConfigureContextHelpTopics()
        {
            HelpSystem.SetTopic(Waveform, "sequence-editor");
            HelpSystem.SetTopic(VolumeSlider, "sequence-editor");
            HelpSystem.SetTopic(SequenceDescriptionText, "sequence-editor");
            HelpSystem.SetTopic(SequenceDescriptionEditButton, "sequence-editor");


            HelpSystem.SetTopic(SplineArea, "spline-editor");
            HelpSystem.SetTopic(Spline, "spline-editor");
            HelpSystem.SetTopic(CommandsAtPointPanel, "commands");
            HelpSystem.SetTopic(CommandsAtPointList, "commands");

            HelpSystem.SetTopic(MovieTimelinePanel, "movie-timeline");
            HelpSystem.SetTopic(MovieTimeline, "movie-timeline");
            HelpSystem.SetTopic(MovieDescriptionText, "movie-timeline");
            HelpSystem.SetTopic(MovieDescriptionEditButton, "movie-timeline");

            HelpSystem.SetTopic(LiveDriveBtn, "live-drive-hardware");
            HelpSystem.SetTopic(MaestroStatusDot, "live-drive-hardware");
            HelpSystem.SetTopic(ArduinoStatusDot, "live-drive-hardware");
            HelpSystem.SetTopic(LeftTicStatusDot, "live-drive-hardware");
            HelpSystem.SetTopic(RightTicStatusDot, "live-drive-hardware");

            HelpSystem.SetTopic(RobotHeadEmbeddedBorder, "urdf-viewer");
            HelpSystem.SetTopic(EmbeddedHeadView, "urdf-viewer");
            HelpSystem.SetTopic(DockUrdfButton, "urdf-viewer");
        }

        #endregion

        // ================================================================
        #region 10. Movie timeline
        // ================================================================





        private double GetEmbeddedUrdfMinimumHeight()
        {
            double headerHeight = (MainMenuPanel?.ActualHeight ?? 0) + (SequenceDescriptionPanel?.ActualHeight ?? 0) + (SequenceDescriptionPanel?.Margin.Top ?? 0);
            double actual = (TopEditorRow?.ActualHeight ?? 0) + headerHeight;
            if (actual > 1) return actual;
            if (TopEditorRow != null && TopEditorRow.Height.IsAbsolute)
                return Math.Max(80.0, TopEditorRow.Height.Value) + headerHeight;
            return 250.0;
        }

        private double GetEmbeddedUrdfMaximumHeight()
        {
            if (EditorTimelineGrid == null) return GetEmbeddedUrdfMinimumHeight();
            double available = EditorPanelViewportHeight();
            if (available > 0) return Math.Max(GetEmbeddedUrdfMinimumHeight(), available - RobotHeadEmbeddedBorder.Margin.Top);
            double total = 0;
            // The URDF may extend through rows 0..4 (Commands, audio and spline).
            for (int i = 0; i < EditorTimelineGrid.RowDefinitions.Count; i++)
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

            Grid.SetRowSpan(RobotHeadEmbeddedBorder, EditorTimelineGrid.RowDefinitions.Count);
            RobotHeadEmbeddedBorder.VerticalAlignment = VerticalAlignment.Top;
            double min = GetEmbeddedUrdfMinimumHeight();
            double max = GetEmbeddedUrdfMaximumHeight();
            double target = _embeddedUrdfHeightPixels > 0 ? _embeddedUrdfHeightPixels : min;
            target = Math.Clamp(target, min, max);
            // Clamp the rendered height, not the user's saved preference.
            // Startup, restoring from maximized, or a temporary narrow layout
            // must not permanently shrink the last dragged URDF height.
            RobotHeadEmbeddedBorder.Height = target;
            EmbeddedHeadView?.SetDockedHostState();
        }

        private void SetMovieDescriptionText(string text)
        {
            _movieDescription = text ?? "";
            if (MovieDescriptionRun != null) MovieDescriptionRun.Text = _movieDescription;
        }

        private void MovieDescriptionEdit_Click(object sender, RoutedEventArgs e)
        {
            var editor = new DescriptionEditWindow(this, "Movie", _movieDescription);
            if (editor.ShowDialog() != true ||
                string.Equals(editor.DescriptionText, _movieDescription, StringComparison.Ordinal)) return;
            SetMovieDescriptionText(editor.DescriptionText);
            UpdateDocumentStatusIndicators();
        }

        /// <summary>A stable representation of every editable sequence value.
        /// It intentionally includes UI-only project settings that are copied
        /// into the JSON only at save time, such as spline checkboxes.</summary>
        private string CurrentSequenceFingerprint()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_doc?.Description ?? "");
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
            if (IsRunning) PausePlayback();

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

        private bool ConfirmDocumentSwitch(bool replaceMovie)
        {
            if (!ApplyOpenCommandEditor()) return false;
            if (!ConfirmSequenceSwitch()) return false;
            if (!replaceMovie || !MovieHasUnsavedChanges()) return true;
            if (IsRunning) PausePlayback();
            var answer = MessageBox.Show(this,
                "The current movie has unsaved changes.\n\nSave them before continuing?",
                "Unsaved movie changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.No) return true;
            return string.IsNullOrWhiteSpace(_moviePath)
                ? SaveMovieAsInteractive(saveSequence: false)
                : SaveMovieToPath(_moviePath);
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
            if (ConfigPathService.TryResolve(ConfigRoot, stored, out string resolved) &&
                File.Exists(resolved))
                return resolved;

            string seqDir = Path.GetDirectoryName(sequencePath) ?? "";
            string p = Path.Combine(seqDir, Path.GetFileName(stored));
            if (ConfigPathService.IsWithin(ConfigRoot, p) && File.Exists(p)) return p;

            p = Path.Combine(_folders?.ProjectFolderOrDefault ?? "", Path.GetFileName(stored));
            if (ConfigPathService.IsWithin(ConfigRoot, p) && File.Exists(p)) return p;
            return null;
        }

        private List<MissingFileReference> FindMissingFileReferences()
        {
            var result = new List<MissingFileReference>();
            string primaryStored = !string.IsNullOrWhiteSpace(_doc?.AudioFilePath)
                ? _doc.AudioFilePath : _doc?.AudioFile;
            if (!string.IsNullOrWhiteSpace(primaryStored) &&
                ResolveSequenceAudioPath(_doc, _jsonPath ?? "", primaryStored) == null)
            {
                result.Add(new MissingFileReference
                {
                    Kind = MissingFileKind.PrimaryAudio,
                    MissingPath = primaryStored,
                    Owner = _doc,
                });
            }

            foreach (var command in _doc?.Commands?.Where(c => c.Servo == ServoNames.Play)
                         ?? Enumerable.Empty<ServoCommand>())
            {
                if (ResolveAudioPath(command.TextValue) != null) continue;
                result.Add(new MissingFileReference
                {
                    Kind = MissingFileKind.InsertedAudio,
                    MissingPath = command.TextValue ?? "",
                    Owner = command,
                });
            }

            foreach (var item in _movieItems)
            {
                if (File.Exists(item.FilePath)) continue;
                result.Add(new MissingFileReference
                {
                    Kind = MissingFileKind.MovieSequence,
                    MissingPath = item.FilePath ?? "",
                    Owner = item,
                });
            }
            return result;
        }

        private void RepairMissingFiles_Click(object sender, RoutedEventArgs e) =>
            OpenMissingFileRepair(showWhenEmpty: true);

        private void OfferMissingFileRepair() => OpenMissingFileRepair(showWhenEmpty: false);

        private void ScheduleMissingFileRepair()
        {
            if (_repairOfferQueued || _repairWindowOpen) return;
            _repairOfferQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _repairOfferQueued = false;
                OfferMissingFileRepair();
            }));
        }

        private void OpenMissingFileRepair(bool showWhenEmpty)
        {
            if (_repairWindowOpen) return;
            var missing = FindMissingFileReferences();
            if (missing.Count == 0)
            {
                if (showWhenEmpty)
                    MessageBox.Show(this, "No missing sequence or audio files were found.",
                        "Repair Missing Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _repairWindowOpen = true;
            try
            {
                var window = new MissingFileRepairWindow(missing, ConfigRoot) { Owner = this };
                if (window.ShowDialog() != true) return;
                ApplyMissingFileRepairs(window.Files);
            }
            finally { _repairWindowOpen = false; }
        }

        private void ApplyMissingFileRepairs(IEnumerable<MissingFileReference> repairs)
        {
            bool sequenceChanged = false;
            bool movieChanged = false;
            bool primaryRemoved = false;

            foreach (var repair in repairs)
            {
                if (!repair.Remove && (string.IsNullOrWhiteSpace(repair.ReplacementPath) ||
                                       !File.Exists(repair.ReplacementPath)))
                    continue;

                if (repair.Kind == MissingFileKind.PrimaryAudio)
                {
                    sequenceChanged = true;
                    if (repair.Remove)
                    {
                        primaryRemoved = true;
                        _doc.AudioFile = "";
                        _doc.AudioFilePath = "";
                    }
                    else
                    {
                        LoadAudio(repair.ReplacementPath);
                        _doc.AudioFilePath = ConfigPathService.ToRelative(
                            ConfigRoot, repair.ReplacementPath);
                        _doc.AudioFile = Path.GetFileName(repair.ReplacementPath);
                    }
                }
                else if (repair.Kind == MissingFileKind.InsertedAudio &&
                         repair.Owner is ServoCommand command)
                {
                    sequenceChanged = true;
                    if (repair.Remove) _doc.Commands.Remove(command);
                    else command.TextValue = ConfigPathService.ToRelative(
                        ConfigRoot, repair.ReplacementPath);
                }
                else if (repair.Kind == MissingFileKind.MovieSequence &&
                         repair.Owner is MovieSequenceItem item)
                {
                    movieChanged = true;
                    if (repair.Remove)
                    {
                        int index = _movieItems.IndexOf(item);
                        _movieItems.Remove(item);
                        if (_movieSelectedIndex == index) _movieSelectedIndex = -1;
                        else if (index >= 0 && _movieSelectedIndex > index) _movieSelectedIndex--;
                    }
                    else
                    {
                        // Validate that the selected JSON is a sequence before
                        // accepting it as a movie block replacement.
                        AnimationDocument.Load(repair.ReplacementPath);
                        item.FilePath = repair.ReplacementPath;
                        item.DurationSeconds = SequenceDurationFromPath(repair.ReplacementPath);
                        item.Description = SequenceDescriptionFromPath(repair.ReplacementPath);
                    }
                }
            }

            if (primaryRemoved)
            {
                StopPlayback();
                _reader?.Dispose();
                _reader = null;
                _audioPath = null;
                _primaryDuration = 0;
                Waveform.PrimaryAudioName = "";
                Waveform.SetAudio(null, null, 0.001, 0);
            }
            if (sequenceChanged) RefreshAfterEdit();
            if (movieChanged) RefreshMovieTimelineView();
            ShowStatus("Missing-file repairs applied");
        }

        /// <summary>Determine the actual sequence length for a movie block.
        /// The saved duration and last command are considered, and available
        /// primary/Play audio files extend the block to their real end.</summary>
        private readonly Dictionary<string, SequenceInfo> _movieMetadata = new(StringComparer.OrdinalIgnoreCase);

        private void WatchMovieMetadata()
        {
            _movieMetadataWatcher?.Dispose();
            _movieMetadataWatcher = null;
            _movieMetadataRefreshTimer.Stop();
            _changedMovieFiles.Clear();
            if (!Directory.Exists(ConfigRoot)) return;
            try
            {
                var watcher = new FileSystemWatcher(ConfigRoot, "*.json")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                };
                FileSystemEventHandler changed = (_, args) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_movieMetadataWatcher != watcher ||
                        !_movieItems.Any(i => PathsEqual(i.FilePath, args.FullPath))) return;
                    _changedMovieFiles.Add(args.FullPath);
                    _movieMetadataRefreshTimer.Stop();
                    _movieMetadataRefreshTimer.Start();
                }));
                watcher.Changed += changed;
                watcher.Created += changed;
                watcher.Deleted += changed;
                watcher.Renamed += (_, args) =>
                {
                    changed(watcher, new FileSystemEventArgs(WatcherChangeTypes.Deleted,
                        Path.GetDirectoryName(args.OldFullPath), Path.GetFileName(args.OldFullPath)));
                    changed(watcher, args);
                };
                _movieMetadataWatcher = watcher;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) { Debug.WriteLine("Movie metadata watcher: " + ex.Message); }
        }

        private async void RefreshChangedMovieMetadata(object sender, EventArgs e)
        {
            _movieMetadataRefreshTimer.Stop();
            string root = ConfigRoot;
            var paths = _changedMovieFiles.ToArray();
            _changedMovieFiles.Clear();
            foreach (string path in paths)
            {
                SequenceInfo info = null;
                try { info = await _mediaCache.DescribeSequence(path, root); }
                catch { /* Deleted or invalid files have no cached tooltip metadata. */ }
                if (_movieMetadataWatcher == null || !PathsEqual(root, ConfigRoot)) return;
                if (info == null) _movieMetadata.Remove(path);
                else _movieMetadata[path] = info;
                foreach (var item in _movieItems.Where(i => PathsEqual(i.FilePath, path)))
                    if (!PathsEqual(_jsonPath, path)) item.Description = info?.Document.Description ?? "";
            }
            MovieTimeline.RefreshBlockToolTip();
            MovieTimeline.InvalidateVisual();
        }

        private double SequenceDurationFromPath(string path)
        {
            try
            {
                var info = LoadingWindow.Wait(this, _mediaCache.DescribeSequence(path, ConfigRoot),
                    "Reading sequence information…");
                _movieMetadata[path] = info;
                return info.Duration;
            }
            catch { return 1.0; }
        }

        private void RefreshMovieDurationForPath(string path)
        {
            bool changed = false;
            string description = SequenceDescriptionFromPath(path);
            foreach (var item in _movieItems)
            {
                if (!PathsEqual(item.FilePath, path)) continue;
                item.DurationSeconds = SequenceDurationFromPath(path);
                item.Description = description;
                changed = true;
            }
            if (changed) RefreshMovieTimelineView();
        }

        private string SequenceDescriptionFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                return LoadingWindow.Wait(this, _mediaCache.Sequence(path),
                    "Reading sequence information…").Description ?? "";
            }
            catch { return ""; }
        }

        private void RefreshMovieDescriptionForPath(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            bool changed = false;
            string value = description ?? "";
            foreach (var item in _movieItems)
            {
                if (!PathsEqual(item.FilePath, path) || item.Description == value) continue;
                item.Description = value;
                changed = true;
            }
            if (changed) MovieTimeline?.InvalidateVisual();
        }

        private void NewMovieSequence()
        {
            if (string.IsNullOrWhiteSpace(_moviePath)) return;
            var dialog = new NewSequenceWindow(this);
            if (dialog.ShowDialog() != true) return;
            if (!ConfirmDocumentSwitch(replaceMovie: false)) return;

            // Only reset the Sequence editor: existing Movie blocks and metadata stay intact.
            StopPlayback();
            _reader?.Dispose(); _reader = null;
            _audioPath = null;
            _jsonPath = null;
            _doc = new AnimationDocument();
            _pendingMovieSequence = new PendingMovieSequence(dialog.SequenceName, _moviePath, _doc);
            _activeDocumentKind = ActiveDocumentKind.Sequence;
            _movieSelectedIndex = -1;
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            _movieCarryPose = null;
            _rgbSimulator.SetInitialFrame(null);
            _rgbSimulator.Invalidate();
            _primaryDuration = 0;
            _activeSource = null;
            _lastDesiredKey = null;
            _audioOffset = 0;
            _mediaCache.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _clipboard.Clear();
            foreach (var row in _rows) row.SplineEnabled = false;
            _splineHz = 50;
            SetDescriptionText(_doc.Description);
            RebuildPlaybackIndexes();
            Waveform.PrimaryAudioName = "";
            Waveform.AudioOffset = 0;
            Waveform.SetAudio(null, null, 0.001, 0);
            Waveform.Duration = TimelineDuration;
            Waveform.ContentDuration = ContentEnd;
            EndArrowPrompt();
            SetCursor(0);
            // An empty named draft is unsaved too; navigation/close must still prompt.
            _savedSequenceFingerprint = "<new-movie-sequence>";
            RefreshAfterEdit();
            Waveform.ZoomToFit();
            SyncScrollBar();
            MovieTimeline.CursorTime = MovieTimeline.TotalDuration;
            MoviePlayButton.Content = "▶ Movie";
            RefreshMovieTimelineView();
            UpdateTitle();
            ShowStatus($"New Sequence: {dialog.SequenceName} — save it to add it at the end of the Movie");
        }

        private void RefreshMovieTimelineView()
        {
            MovieTimeline.CanCreateSequence = !string.IsNullOrWhiteSpace(_moviePath);
            MovieTimeline.SetItems(_movieItems);
            MovieTimeline.SelectedIndex = _movieSelectedIndex;
            MovieTimeline.CursorTime = Math.Clamp(MovieTimeline.CursorTime, 0, MovieTimeline.TotalDuration);
            MovieTimeline.EnsureVisible(MovieTimeline.CursorTime);
            MovieTimeline.InvalidateVisual();
            SyncMovieScroll();
            UpdateDocumentStatusIndicators();
            UpdateEmptyStates();
        }

        private string MovieBlockToolTip(MovieSequenceItem item)
        {
            if (item == null) return "";
            string description = item.Description ?? "";
            string audio = _movieMetadata.TryGetValue(item.FilePath ?? "", out var info)
                ? info.AudioFiles : "";
            bool current = PathsEqual(_jsonPath, item.FilePath);
            if (current)
            {
                description = _doc?.Description ?? description;
                audio = BuildAudioFilesHeader().Replace("|", ", ");
            }
            string tip = $"{Path.GetFileName(item.FilePath)}\nDuration: {item.DurationSeconds:0.###} s";
            if (!string.IsNullOrWhiteSpace(description)) tip += $"\n{description}";
            if (!string.IsNullOrWhiteSpace(audio)) tip += $"\nAudio: {audio}";
            if (current) tip += SequenceHasUnsavedChanges()
                ? "\nCurrent sequence — unsaved edits" : "\nCurrent sequence";
            return tip + $"\n{item.FilePath}";
        }

        private void SyncMovieScroll()
        {
            if (MovieScroll == null || MovieTimeline == null) return;
            double visible = Math.Max(0.001, MovieTimeline.VisibleSeconds);
            MovieScroll.Minimum = 0;
            MovieScroll.Maximum = Math.Max(0, MovieTimeline.ScrollableDuration - visible);
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
            return ConfigPathService.ToRelative(ConfigRoot, fullPath);
        }

        private string ResolveMovieSequencePath(string stored, string moviePath)
        {
            if (string.IsNullOrWhiteSpace(stored)) return stored;
            if (ConfigPathService.TryResolve(ConfigRoot, stored, out string current) &&
                File.Exists(current))
                return current;

            // Backward compatibility for older movies whose paths were relative
            // to Projects rather than to the Configuration folder.
            string p = Path.Combine(ProjectsFolder(), stored);
            if (ConfigPathService.IsWithin(ConfigRoot, p) && File.Exists(p)) return p;

            p = Path.Combine(Path.GetDirectoryName(moviePath) ?? ProjectsFolder(), stored);
            return ConfigPathService.IsWithin(ConfigRoot, p) ? Path.GetFullPath(p) : "";
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
            if (!RequireConfigPath(dlg.FileName, "movie")) return;
            LoadMovieFromPath(dlg.FileName, alreadyConfirmed: false);
        }

        /// <summary>Load a movie from a known pathname. Used by File > Load
        /// Movie, File > Open Recent and startup document restoration.</summary>
        private bool LoadMovieFromPath(string moviePath, bool alreadyConfirmed)
        {
            if (!RequireConfigPath(moviePath, "movie")) return false;
            try
            {
                var movie = MovieDocument.Load(moviePath);
                if (!alreadyConfirmed && !ConfirmDocumentSwitch(replaceMovie: true)) return false;
                EndMovieBackgroundControl();
                SetPlaybackControlSource(null);
                StopPlayback();
                string root = ConfigRoot;
                var sequencePaths = movie.Sequences.Select(p => ResolveMovieSequencePath(p, moviePath))
                    .Where(p => ConfigPathService.IsWithin(root, p) && File.Exists(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                LoadingWindow.Wait(this, Task.WhenAll(sequencePaths.Select(async p =>
                {
                    try { return await _mediaCache.DescribeSequence(p, root); }
                    catch { return null; }
                })), "Preparing movie sequences…");
                var loaded = new List<MovieSequenceItem>();
                var missing = new List<string>();
                for (int i = 0; i < movie.Sequences.Count; i++)
                {
                    string stored = movie.Sequences[i];
                    string path = ResolveMovieSequencePath(stored, moviePath);
                    if (!File.Exists(path)) missing.Add(stored);
                    loaded.Add(new MovieSequenceItem
                    {
                        FilePath = path,
                        DurationSeconds = File.Exists(path) ? SequenceDurationFromPath(path) : 1.0,
                        Description = SequenceDescriptionFromPath(path),
                        IsLooping = i < movie.SequenceLoops.Count && movie.SequenceLoops[i],
                        Trigger = i < movie.SequenceTriggers.Count ? movie.SequenceTriggers[i] ?? "" : "",
                    });
                }

                StopPlayback();
                _moviePlaybackActive = false;
                _moviePlaybackIndex = -1;
                _movieItems.Clear();
                _movieItems.AddRange(loaded);
                _moviePath = moviePath;
                _pendingMovieSequence = null;
                _activeDocumentKind = ActiveDocumentKind.Movie;
                _movieDescription = movie.Description ?? "";
                _movieCreatedDate = string.IsNullOrWhiteSpace(movie.CreatedDate)
                    ? DateTime.Today.ToString("yyyy-MM-dd") : movie.CreatedDate;
                SetMovieDescriptionText(_movieDescription);
                _movieSelectedIndex = -1;
                MovieTimeline.CursorTime = 0;
                _savedMovieFingerprint = CurrentMovieFingerprint();
                RefreshMovieTimelineView();
                RecordRecentFile(moviePath, ActiveDocumentKind.Movie, setActive: true);
                ShowStatus($"Movie loaded: {Path.GetFileName(_moviePath)}");

                if (_movieItems.Count > 0 && File.Exists(_movieItems[0].FilePath))
                    SelectMovieSequence(0, 0, alreadyConfirmed: true);
                if (missing.Count > 0 || FindMissingFileReferences().Count > 0)
                    ScheduleMissingFileRepair();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load movie:\n" + ex.Message,
                    "Load movie", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>Save a dirty sequence before writing its containing movie.
        /// This keeps the movie block and the sequence JSON synchronized. If the
        /// current sequence has no path, Save As is requested; canceling or a save
        /// failure cancels the movie save as well.</summary>
        private bool SaveModifiedSequenceBeforeMovie()
        {
            if (!SequenceHasUnsavedChanges()) return true;
            return !string.IsNullOrWhiteSpace(_jsonPath)
                ? SaveProjectTo(_jsonPath)
                : SaveProjectAsInteractive();
        }

        /// <summary>File > Save Movie: save a modified current sequence first,
        /// then save edits back to the currently loaded movie file. A movie that
        /// has never been saved falls through to Save As.</summary>
        private void SaveMovie_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveModifiedSequenceBeforeMovie()) return;

            if (string.IsNullOrWhiteSpace(_moviePath))
            {
                SaveMovieAs_Click(sender, e);
                return;
            }

            SaveMovieToPath(_moviePath);
        }

        private void SaveMovieAs_Click(object sender, RoutedEventArgs e) =>
            SaveMovieAsInteractive();

        private bool SaveMovieAsInteractive(bool saveSequence = true)
        {
            if (saveSequence && !SaveModifiedSequenceBeforeMovie()) return false;

            var dlg = new SaveFileDialog
            {
                Title = "Save movie JSON",
                Filter = "Movie JSON files (*.json)|*.json",
                InitialDirectory = ProjectsFolder(),
                FileName = string.IsNullOrWhiteSpace(_moviePath)
                    ? "movie.json" : Path.GetFileName(_moviePath),
            };
            if (dlg.ShowDialog() != true) return false;

            if (!RequireConfigPath(dlg.FileName, "movie")) return false;
            string savePath = dlg.FileName;
            return SaveMovieToPath(savePath);
        }

        /// <summary>Write the complete in-memory movie state to one pathname.
        /// This is shared by Save Movie and Save Movie As so reordered/inserted/
        /// removed sequences and edited description text are all persisted.</summary>
        private bool SaveMovieToPath(string savePath)
        {
            if (!RequireConfigPath(savePath, "movie")) return false;
            try
            {
                _movieDescription ??= "";
                if (string.IsNullOrWhiteSpace(_movieCreatedDate))
                    _movieCreatedDate = DateTime.Today.ToString("yyyy-MM-dd");

                var movie = new MovieDocument
                {
                    Description = _movieDescription,
                    CreatedDate = _movieCreatedDate,
                    Sequences = _movieItems.Select(i => MovieStoredSequencePath(i.FilePath)).ToList(),
                    SequenceLoops = _movieItems.Select(i => i.IsLooping).ToList(),
                    SequenceTriggers = _movieItems.Select(i => i.Trigger).ToList(),
                };

                movie.Save(savePath);
                if (CurrentMovieDraft != null) CurrentMovieDraft.MoviePath = savePath;
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
            if (!RequireConfigPath(dlg.FileName, "sequence")) return;

            try
            {
                // Parse now so a non-sequence JSON produces an immediate error.
                AnimationDocument.Load(dlg.FileName);
                if (!ConfirmSequenceSwitch()) return;
                var item = new MovieSequenceItem
                {
                    FilePath = dlg.FileName,
                    DurationSeconds = SequenceDurationFromPath(dlg.FileName),
                    Description = SequenceDescriptionFromPath(dlg.FileName),
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

        private void MovieTimeline_AssignTriggerRequested(int index)
        {
            if (index < 0 || index >= _movieItems.Count) return;
            var dialog = new SequenceTriggerWindow(this, _movieItems[index].Trigger,
                trigger => _movieItems.Where((item, i) => i != index).Any(item => item.Trigger == trigger));
            if (dialog.ShowDialog() != true) return;
            _movieItems[index].Trigger = dialog.Trigger;
            RefreshMovieTimelineView();
        }

        private void MovieTimeline_LoopToggleRequested(int index)
        {
            if (index < 0 || index >= _movieItems.Count) return;
            _movieItems[index].IsLooping = !_movieItems[index].IsLooping;
            RefreshMovieTimelineView();
            string name = Path.GetFileNameWithoutExtension(_movieItems[index].FilePath);
            ShowStatus($"{name}: looping {(_movieItems[index].IsLooping ? "enabled" : "disabled")}");
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

        /// <summary>Capture the exact pose at the outgoing movie cursor. This
        /// becomes the implicit pre-time-zero state of the next sequence;
        /// only commands reached in that sequence replace carried values.</summary>
        private void CaptureMovieCarryPose()
        {
            UpdateServoState(_cursorTime);
            var pose = new MovieCarryPose();
            foreach (var row in _rows)
            {
                pose.Values[row.Servo] = row.Value;
                pose.Speeds[row.Servo] = row.Speed;
                pose.TextValues[row.Servo] = row.TextValue ?? "";
                pose.Colors[row.Servo] = row.ColorHex ?? "";

                foreach (var control in ServoConfiguration.ControlsFor(row.Servo))
                {
                    _childCommandIndex.TryGetValue((row.Servo, control), out ServoCommand[] childCommands);
                    ServoCommand child = LastCommandAtOrBefore(childCommands, _cursorTime + 1e-9);
                    double value = MoviePoseContinuity.ChildValue(row.Value, row.Offset, child,
                        _movieCarryPose?.ChildValues.TryGetValue((row.Servo, control), out double carried) == true
                            ? carried : null);
                    pose.ChildValues[(row.Servo, control)] = value;
                }
            }

            var both = _rows.First(r => r.Servo == ServoNames.BothEyePop);
            foreach (var side in new[] { ServoNames.LeftEyePop, ServoNames.RightEyePop })
            {
                var individual = _rows.First(r => r.Servo == side);
                pose.Values[side] = both.Offset.HasValue &&
                    (!individual.Offset.HasValue || both.Offset.Value >= individual.Offset.Value)
                    ? both.Value : individual.Value;
            }

            var neck = SharedNeckStateAt(_cursorTime);
            pose.NeckOwner = neck.Owner ?? _movieCarryPose?.NeckOwner;
            pose.RgbFrame = _rgbSimulator.Evaluate(_doc.Commands, _cursorTime);
            _movieCarryPose = pose;
            _rgbSimulator.SetInitialFrame(pose.RgbFrame);
        }

        private bool SelectMovieSequence(int index, double localTime,
                                         bool alreadyConfirmed = false,
                                         bool preservePose = false,
                                         bool preservePending = false)
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

            if (preservePose)
                CaptureMovieCarryPose();

            StopPlayback(cancelPending: !preservePending);
            _moviePlaybackActive = false;
            _moviePlaybackIndex = -1;
            MoviePlayButton.Content = "▶ Movie";

            if (!LoadSequenceFromPath(path, localTime, fitTimeline: true,
                                      recordRecent: false, setActiveDocument: false,
                                      preserveMoviePose: preservePose,
                                      preservePending: preservePending)) return false;

            _movieSelectedIndex = index;
            _movieItems[index].DurationSeconds = SequenceDurationFromPath(path);
            _movieItems[index].Description = _doc?.Description ?? SequenceDescriptionFromPath(path);
            MovieTimeline.SelectedIndex = index;
            MovieTimeline.CursorTime = MovieTimeline.StartOf(index) +
                                       Math.Clamp(localTime, 0, _movieItems[index].DurationSeconds);
            RefreshMovieTimelineView();
            PrefetchNextMovieSequences();
            return true;
        }

        private void MoviePlay_Click(object sender, RoutedEventArgs e)
        {
            if (!_controllerTransportInvocation && !_handlingMovieArrow) SetPlaybackControlSource(null);
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
                ArmMovieBackgroundControl();
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
            ArmMovieBackgroundControl();
            PrefetchNextMovieSequences();
        }

        private void MoviePrevious_Click(object sender, RoutedEventArgs e) =>
            MoviePreviousOrRestart();

        private void MovieNext_Click(object sender, RoutedEventArgs e)
        {
            if (_movieItems.Count == 0) return;
            int current = _movieSelectedIndex >= 0
                ? _movieSelectedIndex
                : MovieTimeline.IndexAtTime(MovieTimeline.CursorTime, boundaryChoosesNext: false);
            int next = current + 1;
            // Right Arrow exits a looping block and advances to the next
            // non-looping cue. Consecutive loop blocks are therefore skipped.
            if (current >= 0 && current < _movieItems.Count &&
                _movieItems[current].IsLooping)
            {
                while (next < _movieItems.Count && _movieItems[next].IsLooping)
                    next++;
            }
            if (next < 0 || next >= _movieItems.Count) return;

            StartMovieCue(next);
        }

        private void StartMovieCue(int next)
        {
            if (!_controllerTransportInvocation && !_handlingMovieArrow) SetPlaybackControlSource(null);
            double start = MovieTimeline.StartOf(next);
            MovieTimeline.CursorTime = start;
            if (!SelectMovieSequence(next, 0, preservePose: true)) return;

            _moviePlaybackActive = true;
            _moviePlaybackIndex = next;
            MoviePlayButton.Content = "❚❚ Pause";
            StartPlaybackAt(0);
            ArmMovieBackgroundControl();
            PrefetchNextMovieSequences();
        }

        /// <summary>Movie cue policy at the end of a loaded sequence. A loop
        /// repeats immediately; a one-shot automatically enters a following
        /// loop, but otherwise stops and waits for the next Right Arrow cue.</summary>
        private bool ContinueMoviePlaybackAfterSequenceEnd()
        {
            if (!_moviePlaybackActive || _moviePlaybackIndex < 0 ||
                _moviePlaybackIndex >= _movieItems.Count)
                return false;

            int target = -1;
            if (_movieItems[_moviePlaybackIndex].IsLooping)
                target = _moviePlaybackIndex;
            else
            {
                int next = _moviePlaybackIndex + 1;
                if (next < _movieItems.Count && _movieItems[next].IsLooping)
                    target = next;
            }

            if (target < 0) return false;

            if (target != _moviePlaybackIndex)
            {
                if (!SelectMovieSequence(target, 0,
                                         preservePose: true, preservePending: true))
                    return false;
            }
            else
            {
                CaptureMovieCarryPose();
                DisposeAudioDevice();
                _cursorTime = 0;
                MovieTimeline.CursorTime = MovieTimeline.StartOf(target);
                MovieTimeline.SelectedIndex = target;
                MovieTimeline.InvalidateVisual();
            }

            _moviePlaybackActive = true;
            _moviePlaybackIndex = target;
            _movieSelectedIndex = target;
            MoviePlayButton.Content = "❚❚ Pause";
            StartPlaybackAt(0, preservePending: true);
            PrefetchNextMovieSequences();
            return true;
        }

        private async void PrefetchNextMovieSequences()
        {
            string root = ConfigRoot;
            var paths = _movieItems.Skip(Math.Max(0, _movieSelectedIndex + 1)).Take(2)
                .Select(i => i.FilePath).ToList();
            // A loop can skip adjacent looping blocks when Right is pressed.
            var nextCue = _movieItems.Skip(Math.Max(0, _movieSelectedIndex + 1))
                .FirstOrDefault(i => !i.IsLooping);
            if (nextCue != null) paths.Add(nextCue.FilePath);
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!ConfigPathService.IsWithin(root, path)) continue;
                try { await _mediaCache.PrepareSequence(path, root); }
                catch (Exception ex) { Debug.WriteLine("Cue preparation: " + ex.Message); }
            }
        }

        #endregion

        // ================================================================
        #region 11. Animation Library
        // ================================================================

        /// <summary>
        /// Begin a Library Sequence range selection. The movable modeless window keeps
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
                    try { CreateLibrarySequence(description); }
                    finally { EndArrowPrompt(); }
                },
                EndArrowPrompt)
            {
                Owner = this,
            };
            _libraryRangeWindow.Show();
        }

        /// <summary>
        /// Select the Library Sequence first. Once selected, display a blue arrow
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
        /// mode, where the selected Library Sequence's description can be edited.</summary>
        private void LibraryManage_Click(object sender, RoutedEventArgs e)
        {
            EndArrowPrompt();
            new LibraryItemSelectionWindow(LibraryFolder(), manageMode: true)
            {
                Owner = this,
            }.ShowDialog();
        }

        /// <summary>Manage single-time-point Library Poses, including
        /// descriptions, image previews and file deletion.</summary>
        private void LibraryCommandsManage_Click(object sender, RoutedEventArgs e)
        {
            EndArrowPrompt();
            new LibraryItemSelectionWindow(LibraryCommandsFolder(), manageMode: true,
                                           itemLabel: "Library Pose",
                                           showAudioFiles: false)
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

            if ((Keyboard.Modifiers == ModifierKeys.Control ||
                 Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) &&
                (e.Key == Key.S || e.Key == Key.O))
            {
                if (e.IsRepeat) { e.Handled = true; return; }
                bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                if (e.Key == Key.O)
                {
                    if (shift) LoadMovie_Click(this, new RoutedEventArgs());
                    else LoadProject_Click(this, new RoutedEventArgs());
                }
                else if (_activeDocumentKind == ActiveDocumentKind.Movie)
                {
                    if (shift) SaveMovieAsInteractive();
                    else SaveMovie_Click(this, new RoutedEventArgs());
                }
                else
                {
                    if (shift) SaveProjectAsInteractive();
                    else SaveProject_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
                return;
            }

            if (!FocusNeedsNavigationKeys(Keyboard.FocusedElement as DependencyObject) &&
                MovieTimelinePanel.Visibility == Visibility.Visible &&
                (MovieTimelinePanel.IsKeyboardFocusWithin || _moviePlaybackActive))
            {
                string trigger = SequenceTrigger.FromKey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
                int target = string.IsNullOrEmpty(trigger) ? -1 : _movieItems.FindIndex(item => item.Trigger == trigger);
                if (target >= 0)
                {
                    e.Handled = true;
                    if (!e.IsRepeat) StartMovieCue(target);
                    return;
                }
            }

            if (Keyboard.Modifiers != ModifierKeys.None ||
                FocusNeedsNavigationKeys(Keyboard.FocusedElement as DependencyObject))
                return;

            if (e.Key == Key.Space && Keyboard.FocusedElement is not ButtonBase)
            {
                if (!e.IsRepeat) PlayPause_Click(PlayPauseBtn, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Movie transport owns arrows only in the movie area or during
            // movie playback. Sliders, lists and text retain their native keys.
            if (MovieTimelinePanel.Visibility != Visibility.Visible ||
                (!MovieTimelinePanel.IsKeyboardFocusWithin && !_moviePlaybackActive)) return;
            if (e.IsRepeat && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right))
            { e.Handled = true; return; }

            if (HandleMovieArrow(e.Key)) e.Handled = true;
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
            bool atStart = local <= 0.001;

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
                MovieTimeline.EnsureVisible(start);
                MovieTimeline.InvalidateVisual();
                return;
            }

            if (index == 0)
            {
                SetCursor(0);
                MovieTimeline.SelectedIndex = 0;
                MovieTimeline.CursorTime = 0;
                MovieTimeline.EnsureVisible(0);
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
            MovieTimeline.EnsureVisible(previousStart);
            MovieTimeline.InvalidateVisual();
        }

        /// <summary>Keep the movie cursor aligned with the local sequence
        /// cursor for both Movie transport and ordinary Sequence transport.
        /// Standalone sequences do not affect the movie timeline.</summary>
        private void SyncMovieCursorToSequencePlayback(double localTime)
        {
            int index = _moviePlaybackActive ? _moviePlaybackIndex : _movieSelectedIndex;
            if (index < 0 || index >= _movieItems.Count) return;
            if (!_moviePlaybackActive &&
                (string.IsNullOrWhiteSpace(_jsonPath) ||
                 !PathsEqual(_movieItems[index].FilePath, _jsonPath))) return;

            double movieTime = MovieTimeline.StartOf(index) +
                Math.Clamp(localTime, 0, _movieItems[index].DurationSeconds);
            MovieTimeline.CursorTime = movieTime;
            bool selectionChanged = MovieTimeline.SelectedIndex != index;
            MovieTimeline.SelectedIndex = index;
            MovieTimeline.EnsureVisible(movieTime);
            if (selectionChanged) MovieTimeline.InvalidateVisual();
            MovieTimeline.InvalidateCursor();
        }

        private void RefreshSelectedMovieBlockFromEditor()
        {
            if (_movieSelectedIndex < 0 || _movieSelectedIndex >= _movieItems.Count ||
                string.IsNullOrWhiteSpace(_jsonPath) ||
                !PathsEqual(_movieItems[_movieSelectedIndex].FilePath, _jsonPath)) return;

            var item = _movieItems[_movieSelectedIndex];
            item.DurationSeconds = Math.Max(0.05, ContentEnd);
            item.Description = _doc?.Description ?? "";
            RefreshMovieTimelineView();
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

        internal static bool FocusNeedsNavigationKeys(DependencyObject focused)
        {
            for (DependencyObject node = focused;
                 node != null;)
            {
                if (node is TextBoxBase || node is PasswordBox || node is Selector ||
                    node is RangeBase || node is MenuBase || node is TreeView) return true;
                node = node is System.Windows.Media.Visual || node is System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        /// <summary>Hide arrows, close the movable create prompt, and clear
        /// any pending selected Library Sequence.</summary>
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

        private string ConfigRoot =>
            _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory;

        /// <summary>Reject a file chosen outside the active Configuration
        /// folder. This keeps every portable document reference deployable.</summary>
        private bool RequireConfigPath(string path, string itemName)
        {
            if (ConfigPathService.TryResolve(ConfigRoot, path, out _)) return true;
            MessageBox.Show(this,
                $"The {itemName} must be inside the Configuration folder or one of its child folders:\n\n{ConfigRoot}",
                "File outside Configuration folder", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        /// <summary>Library Sequences live in Library\Animation
        /// inside the configuration folder (created on demand).</summary>
        private string LibraryFolder()
        {
            string dir = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "Library", "Animation");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>Single-time-point Library Poses live in
        /// Library\Commands inside the configuration folder.</summary>
        private string LibraryCommandsFolder()
        {
            string dir = Path.Combine(
                _folders?.ConfigFolderOrDefault ?? AppContext.BaseDirectory,
                "Library", "Commands");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>Audio referenced by Library Sequences is copied into
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
                setActive, ConfigRoot);
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
                    setActive: true, configFolder: ConfigRoot);

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
        private void CreateLibrarySequence(string description)
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
                                "Create Library Sequence", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Save Library Sequence JSON",
                Filter = "JSON files (*.json)|*.json",
                FileName = "library-sequence.json",
                InitialDirectory = LibraryFolder(),
            };
            if (dlg.ShowDialog() != true) return;
            if (!RequireConfigPath(dlg.FileName, "Library Sequence")) return;

            // Audio referenced by the Library Sequence travels WITH the library.
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
                    play.TextValue = ConfigPathService.ToRelative(ConfigRoot, dest);
                }
                catch (Exception ex)
                {
                    libWarnings.Add($"{Path.GetFileName(srcPath)} ({ex.Message})");
                }
            }

            AnimationDocument.SaveCommandsOnly(dlg.FileName, items, description);
            ShowStatus($"Library Sequence saved: {Path.GetFileName(dlg.FileName)}");

            if (libWarnings.Count > 0)
                MessageBox.Show(this,
                    "The Library Sequence was saved, but these audio files could not " +
                    "be copied into Library\\Audio (their original paths " +
                    "were kept):\n\n  • " + string.Join("\n  • ", libWarnings),
                    "Create Library Sequence", MessageBoxButton.OK,
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
                "Insert Library Sequence", MessageBoxButton.OKCancel,
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
                    MessageBox.Show(this, "The selected Library Sequence has no commands.",
                                    "Insert Library Sequence", MessageBoxButton.OK,
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
                        (ConfigPathService.TryResolve(ConfigRoot, play.TextValue,
                            out string storedAudio) && File.Exists(storedAudio)) ? storedAudio
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
                            play.TextValue = ConfigPathService.ToRelative(ConfigRoot, dest);
                        else
                            insWarnings.Add(name);
                    }
                    catch (Exception ex)
                    {
                        insWarnings.Add($"{name} ({ex.Message})");
                    }
                }

                PushUndo($"Insert Library Sequence {Path.GetFileNameWithoutExtension(fileName)}");
                foreach (var c in cmds)
                {
                    c.OffsetSeconds = ServoCommand.TimeKey(c.OffsetSeconds + at);
                    _doc.Commands.Add(c);
                }
                if (!RefreshAfterEdit()) return;
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
            if (_speedCalibrationBusy) return;
            // If this servo is mapped to a robot-head part, move that part
            // live while the slider is dragged (grid Live Drive sliders and
            // the sliders in the command editor both route through here).
            ForEachHeadView(v => { PrepareDirectMotion(v, servo, null, speed); v.SetServo(servo, value); });

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
            if (_speedCalibrationBusy) return;
            ForEachHeadView(v => { PrepareDirectMotion(v, gang, control, speed); v.SetChildServo(gang, control, value); });

            bool centered = ServoCommand.RangeFor(gang).Min < 0;
            if (LiveDrive && _hw.Connected)
                _hw.DriveControlValue(gang, control, speed, value, centered);

            Debug.WriteLine($"MoveChildServoNow(gang={gang}, control={control}, value={value})");
        }

        /// <summary>Apply one explicit Maestro speed/acceleration profile
        /// without changing position. Used when Edit Commands changes Speed.</summary>
        public void ConfigureServoSpeedNow(ServoSpeed speed, ServoNames servo)
        {
            if (_speedCalibrationBusy) return;
            ForEachHeadView(v => { ConfigureMotionView(v); v.ConfigureCalibratedSpeed(servo, null, speed); });
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
            if (_speedCalibrationBusy) return;
            ForEachHeadView(v => { ConfigureMotionView(v); v.ConfigureCalibratedSpeed(gang, control, speed); });
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
            if (_speedCalibrationBusy) return;
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
            if (_speedCalibrationBusy) return;
            // RGB editor text remains Red,Green,Blue. HardwareManager rotates
            // color-bearing commands to Green,Red,Blue on the Arduino wire.
            if (LiveDrive && _hw.Connected && servo == ServoNames.RGBCommand)
                _hw.DriveRgb(textValue);

            if (servo == ServoNames.RGBCommand)
            {
                _rgbSimulator.Invalidate();
                var frame = _rgbSimulator.Evaluate(_doc.Commands, _cursorTime);
                ForEachHeadView(v => v.SetRgbRingFrame(frame));
            }

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
            if (_controllerPlaybackSource.HasValue && !AllowControllerTargets(commandsAtOffset)) { PausePlayback(); return; }
            // Drive the physical robot during playback when Live Drive is On.
            if (LiveDrive && _hw.Connected && PlaybackOutputAllowed(true))
            {
                // Commands are immutable for the device worker even if the
                // editor is modified while an output batch is pending.
                foreach (var command in commandsAtOffset)
                {
                    var copy = command.Clone();
                    bool controllerOwned = _controllerPlaybackSource.HasValue;
                    long epoch = Interlocked.Read(ref _controllerOutputEpoch);
                    _hardwarePlaybackQueue.Enqueue(() =>
                    {
                        if (!controllerOwned || (_controllerPlaybackPhysicalAllowed && epoch == Interlocked.Read(ref _controllerOutputEpoch)))
                            DispatchHardwareBatch(new[] { copy });
                    });
                }
            }

            Debug.WriteLine($"PlayBackServoValues @ {commandsAtOffset[0].OffsetSeconds:F3}s:");
            foreach (var c in commandsAtOffset)
                Debug.WriteLine($"   {c.Servo}" +
                    (c.Control.HasValue ? $"[{c.Control}]" : "") +
                    $" = {c.ValueDisplay} ({c.SpeedDisplay})");
        }

        private void DispatchHardwareBatch(ServoCommand[] commandsAtOffset)
        {
            foreach (var c in commandsAtOffset)
            {
                if (c.Servo == ServoNames.Play) continue;   // export-only

                if (c.Disable)
                {
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
                    bool centered = ServoCommand.RangeFor(c.Servo).Min < 0;
                    _hw.DriveControlValue(c.Servo, c.Control.Value, c.Speed,
                                          c.NumericValue, centered);
                }
                else
                    _hw.DriveGang(c.Servo, c.Speed, c.NumericValue);
            }
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
