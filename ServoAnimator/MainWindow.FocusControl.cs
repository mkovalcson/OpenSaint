using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ServoAnimator;

public partial class MainWindow
{
    private FocusControlSettings _focusControl = new();
    private BackgroundMovieKeys _backgroundMovieKeys;
    private readonly DispatcherTimer _focusControlTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _movieBackgroundArmed, _handlingMovieArrow;
    private string _movieBackgroundPath, _movieHotkeyError;
    private volatile bool _controllerPhysicalOutputAllowed;
    private volatile bool _controllerPlaybackPhysicalAllowed = true;
    private ControllerKind? _controllerPlaybackSource;
    private bool _controllerTransportInvocation;

    private void InitializeFocusControl()
    {
        _backgroundMovieKeys = new BackgroundMovieKeys(this, BackgroundMovieArrow);
        _focusControlTimer.Tick += (_, _) => { UpdateBackgroundMovieKeys(); RefreshStreamDeckConnection(); };
        Activated += (_, _) => { RefreshControllerOutputPermissions(); UpdateBackgroundMovieKeys(); };
        Deactivated += (_, _) => { RefreshControllerOutputPermissions(); UpdateBackgroundMovieKeys(); };
        _focusControlTimer.Start();
    }
    private void LoadFocusControl()
    {
        EndMovieBackgroundControl();
        try { _focusControl = FocusControlSettings.Load(ConfigRoot); }
        catch (Exception ex)
        {
            _focusControl = new() { XboxController = false, SteamController = false, MoviePlayback = false, StreamDeck = false };
            ShowStatus("Focus Control settings could not be loaded: " + ex.Message);
        }
    }
    private void FocusControl_Click(object sender, RoutedEventArgs e)
    {
        var window = new FocusControlWindow(_focusControl, settings =>
        {
            settings.Save(ConfigRoot); _focusControl = settings.Clone(); ResetControllerMotion();
            RefreshControllerOutputPermissions(); UpdateBackgroundMovieKeys();
        }) { Owner = this };
        window.ShowDialog(); UpdateBackgroundMovieKeys();
    }
    private bool ControllerFocusAllowed(ControllerKind kind) => FocusControlPolicy.Controller(
        ControllerWindowActive, _focusControl.AllowsController(kind),
        !IsEnabled || OwnedWindows.Cast<Window>().Any(ControllerDialogBlocks));
    private bool ControllerOutputAllowed(bool physical)
    {
        if (_recordingWindow != null && !RecordingController && !_recordingReplayOutput) return false;
        if (_safeguardHolding) return false;
        if (_speedCalibrationBusy) return false;
        if (_apiLibraryOwnsOutput) return ApiLibraryOutputAllowed(physical);
        if (_controllerConfigOpen) return !physical && _controllerMappingWindow?.CanTestWithCurrentFocus != false;
        if (!_enabledController.HasValue) return !physical;
        return FocusControlPolicy.Controller(ControllerWindowActive,
            _focusControl.AllowsOutput(_enabledController.Value, physical),
            !IsEnabled || OwnedWindows.Cast<Window>().Any(ControllerDialogBlocks));
    }
    private void RefreshControllerOutputPermissions()
    {
        bool physical = ControllerOutputAllowed(true);
        bool playbackPhysical = PlaybackOutputAllowed(true);
        if ((_controllerPhysicalOutputAllowed && !physical) || (_controllerPlaybackPhysicalAllowed && !playbackPhysical))
        { Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear(); }
        _controllerPhysicalOutputAllowed = physical;
        _controllerPlaybackPhysicalAllowed = playbackPhysical;
        if (!ControllerOutputAllowed(false)) _controllerPreview.Clear();
        // Polling continues when a window is minimized. Remember permission
        // transitions so a later render cannot integrate time spent blocked.
        ForEachHeadView(v => v.RefreshCalibratedMotionPermission());
    }
    private bool PlaybackOutputAllowed(bool physical) => !_speedCalibrationBusy && !_safeguardHolding && (!_controllerPlaybackSource.HasValue || FocusControlPolicy.Controller(
        ControllerWindowActive, _focusControl.AllowsOutput(_controllerPlaybackSource.Value, physical),
        !IsEnabled || OwnedWindows.Cast<Window>().Any(ControllerDialogBlocks)));
    private void SetPlaybackControlSource(ControllerKind? source)
    {
        if (_controllerPlaybackSource != source) Interlocked.Increment(ref _controllerOutputEpoch);
        _controllerPlaybackSource = source; RefreshControllerOutputPermissions();
    }
    private void ControllerTransport(Action action)
    {
        _controllerTransportInvocation = true; SetPlaybackControlSource(_enabledController);
        try { action(); }
        finally { _controllerTransportInvocation = false; }
    }
    private void ArmMovieBackgroundControl()
    { _movieBackgroundArmed = true; _movieBackgroundPath = _moviePath; UpdateBackgroundMovieKeys(); }
    private void EndMovieBackgroundControl()
    { _movieBackgroundArmed = false; _backgroundMovieKeys?.SetEnabled(false); }
    private bool ShouldUseBackgroundMovieKeys() => FocusControlPolicy.Movie(_focusControl.MoviePlayback,
        _movieBackgroundArmed && _recordingWindow == null, BackgroundMovieKeys.IsEditorForeground(),
        !IsEnabled || OwnedWindows.Cast<Window>().Any(ControllerDialogBlocks),
        _movieItems.Count > 0 && MovieTimelinePanel.Visibility == Visibility.Visible);
    private void UpdateBackgroundMovieKeys()
    {
        if (_backgroundMovieKeys == null) return;
        if (_movieBackgroundArmed && (_movieBackgroundPath != _moviePath || _movieItems.Count == 0)) EndMovieBackgroundControl();
        _backgroundMovieKeys.SetEnabled(ShouldUseBackgroundMovieKeys());
        if (_backgroundMovieKeys.Error != null && _backgroundMovieKeys.Error != _movieHotkeyError) ShowStatus(_backgroundMovieKeys.Error);
        _movieHotkeyError = _backgroundMovieKeys.Error;
        FocusControlButton.ToolTip = _backgroundMovieKeys.Enabled ? "Focus Control · movie background arrows are active" : "Configure out-of-focus controller and movie control";
    }
    private void BackgroundMovieArrow(Key key)
    {
        if (_handlingMovieArrow || !ShouldUseBackgroundMovieKeys()) return;
        _handlingMovieArrow = true;
        try { HandleMovieArrow(key); }
        catch (Exception ex) { EndMovieBackgroundControl(); ShowStatus("Movie background control stopped: " + ex.Message); }
        finally { _handlingMovieArrow = false; UpdateBackgroundMovieKeys(); }
    }
    private bool HandleMovieArrow(Key key)
    {
        switch (key)
        {
            case Key.Up: MoviePlay_Click(MoviePlayButton, new RoutedEventArgs()); break;
            case Key.Right: MovieNext_Click(MovieNextButton, new RoutedEventArgs()); break;
            case Key.Left: MoviePreviousOrRestart(); break;
            case Key.Down: MovieGoToBeginning(); break;
            default: return false;
        }
        return true;
    }
}
