using System.Windows;

namespace ServoAnimator;

public partial class MainWindow
{
    private SpeedCalibrationData _speedCalibration;
    private string _speedCalibrationRoot;
    private bool _speedCalibrationBusy;
    private bool _calibratedControllerOutput;
    private ServoCommand[] _motionSpeedSource;
    private readonly Dictionary<RobotControls, ServoCommand[]> _motionSpeedIndex = new();
    private readonly Dictionary<RobotControls, ServoCommand[]> _motionTargetIndex = new();

    private void LoadSpeedCalibration(bool force = false)
    {
        if (!force && _speedCalibration != null && _speedCalibrationRoot == ConfigRoot) return;
        _speedCalibrationRoot = ConfigRoot;
        try { _speedCalibration = SpeedCalibrationData.Load(ConfigRoot); _speedCalibration.RegeneratePredictions(_servoConfig); }
        catch (Exception ex)
        {
            _speedCalibration = new() { UseInUrdf = false };
            ShowStatus("Speed calibration could not be loaded; URDF timing is disabled: " + ex.Message);
        }
    }
    private void ServoConfigurationSaved()
    {
        MarkConfigurationSaved();
        RefreshSavedSpeedPredictions();
    }
    private void RefreshSavedSpeedPredictions()
    {
        try
        {
            var data = SpeedCalibrationData.Load(ConfigRoot);
            data.RegeneratePredictions(_servoConfig);
            data.Save(ConfigRoot);
            _speedCalibration = data; _speedCalibrationRoot = ConfigRoot;
            ForEachHeadView(ConfigureMotionView);
        }
        catch (Exception ex) { ShowStatus("Speed predictions could not be saved: " + ex.Message); }
    }
    private void ConfigureMotionView(RobotHeadView view)
    {
        LoadSpeedCalibration();
        view.SetSpeedCalibration(_speedCalibration);
        view.CollisionSafeguardActive = () => ControllerSafeguardEnabled;
        view.CollisionSafeguardBlocked = SafeguardBlocked;
        view.CalibratedMotionAllowed = () => !_speedCalibrationBusy &&
            (IsRunning ? PlaybackOutputAllowed(false) :
                _calibratedControllerOutput ? ControllerOutputAllowed(false) : PlaybackOutputAllowed(false));
    }
    private void SpeedCalibration_Click(object sender, RoutedEventArgs e) => OpenSpeedCalibration(this);
    private void OpenSpeedCalibration(Window owner)
    {
        if (_speedCalibrationBusy) return;
        StopPlayback(); ResetControllerMotion(); EndMovieBackgroundControl();
        try
        {
            // Edit a separate copy. Renderers only receive a new, saved snapshot.
            var data = SpeedCalibrationData.Load(ConfigRoot);
            var window = new SpeedCalibrationWindow(_servoConfig, data, ConfigRoot,
                async (entry, plan, progress, cancellation) =>
                {
                    _speedCalibrationBusy = true;
                    try
                    {
                        StopPlayback(); ResetControllerMotion();
                        RefreshControllerOutputPermissions();
                        _hardwarePlaybackQueue.ClearPending();
                        // Finish any in-flight output before reserving the persistent port.
                        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        _hardwarePlaybackQueue.EnqueueBarrier(() => drained.SetResult());
                        await drained.Task;
                        cancellation.ThrowIfCancellationRequested();
                        await _hw.CalibrateSpeedsAsync(entry, data, plan, progress, cancellation);
                    }
                    finally
                    {
                        _speedCalibrationBusy = false;
                        ResetControllerMotion(); RefreshControllerOutputPermissions();
                    }
                }, () =>
                {
                    LoadSpeedCalibration(force: true);
                    ForEachHeadView(ConfigureMotionView);
                }, _hw.MaestroConnected) { Owner = owner };
            window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Speed Calibration", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ConfigureTimelineMotion(RobotHeadView view)
    {
        ConfigureMotionView(view);
        if (!view.UsesCalibratedMotion) return;
        if (!ReferenceEquals(_motionSpeedSource, _orderedCommands))
        {
            _motionSpeedSource = _orderedCommands;
            _motionSpeedIndex.Clear(); _motionTargetIndex.Clear();
            foreach (var group in _orderedCommands.Where(c => !c.IsTextServo)
                .SelectMany(c => (c.Control.HasValue ? new[] { c.Control.Value } : ServoConfiguration.ControlsFor(c.Servo)).Select(control => (control, command: c)))
                .GroupBy(p => p.control))
            {
                _motionTargetIndex[group.Key] = group.Select(p => p.command).ToArray();
                _motionSpeedIndex[group.Key] = group.Select(p => p.command).Where(c => !c.Disable && c.Speed != ServoSpeed.NoChange).ToArray();
            }
        }
        foreach (var row in _rows.Where(r => !r.IsTextRow && !_manualPoseOverrides.Contains(r.Servo) && !RecordingOwns(r.Servo)))
        foreach (var control in ServoConfiguration.ControlsFor(row.Servo))
        {
            if (RecordingOwns(row.Servo, control)) continue;
            _motionSpeedIndex.TryGetValue(control, out var commands);
            var command = LastCommandAtOrBefore(commands, _cursorTime);
            var speed = command?.Speed ?? (_movieCarryPose?.Speeds.GetValueOrDefault(row.Servo, ServoSpeed.Default) ?? ServoSpeed.Default);
            view.ConfigureCalibratedSpeed(row.Servo, control, speed);
            _motionTargetIndex.TryGetValue(control, out var targets);
            view.SetCalibratedControlEnabled(row.Servo, control, LastCommandAtOrBefore(targets, _cursorTime)?.Disable != true);
        }
    }
    private void PrepareDirectMotion(RobotHeadView view, ServoNames servo, RobotControls? child, ServoSpeed speed, bool controller = false)
    {
        _calibratedControllerOutput = controller;
        ConfigureMotionView(view);
        view.CalibratedMotionPaused = false;
        view.ConfigureCalibratedSpeed(servo, child, speed);
        view.SetCalibratedControlEnabled(servo, child, true);
    }
}
