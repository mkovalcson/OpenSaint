using System.Diagnostics;
using System.Windows.Threading;

namespace ServoAnimator;

public partial class MainWindow
{
    private ControllerRecordingReplay _recordingReplay;
    private bool _recordingReplayOutput;
    private readonly Stopwatch _recordingReplayClock = new();
    private readonly DispatcherTimer _recordingReplayTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Dictionary<(ServoNames, RobotControls?), (int, ServoSpeed)> _recordingReplayLast = new();

    private void ReplayControllerRecording()
    {
        if (_recordingReplayTimer.IsEnabled) { StopRecordingReplay(); return; }
        if (_recordingWindow == null || RecordingController || _processedTake == null || _processedTake.Count == 0) return;
        StopRecordingReplay(); ResetControllerMotion();
        _recordingReplay = new ControllerRecordingReplay(_processedTake, SplineServosEnabled().ToHashSet(), _controllerTake.Start, _controllerTake.End);
        _enabledController = _recordingWindow.Source;
        _recordingReplayOutput = true; RefreshControllerOutputPermissions();
        _recordingReplayClock.Restart();
        _recordingReplayTimer.Tick -= RecordingReplay_Tick; _recordingReplayTimer.Tick += RecordingReplay_Tick;
        _recordingReplayTimer.Start(); _recordingWindow.SetReplayState(true);
        _recordingWindow.SetStatus("Replaying only this take. Existing Drive HW and Focus Control settings apply; the timeline is unchanged.");
        RecordingReplay_Tick(this, EventArgs.Empty);
    }

    private void RecordingReplay_Tick(object sender, EventArgs e)
    {
        if (!_recordingReplayOutput || _recordingReplay == null) return;
        try
        {
            if (!ControllerOutputAllowed(false) && !ControllerOutputAllowed(true)) { StopRecordingReplay(); return; }
            double elapsed = _recordingReplayClock.Elapsed.TotalSeconds;
            var originalSpeed = _controllerSpeed;
            try
            {
                foreach (var command in _recordingReplay.Evaluate(elapsed))
                {
                    var key = (command.Servo, command.Control); var value = (command.NumericValue, command.Speed);
                    if (_recordingReplayLast.TryGetValue(key, out var previous) && previous == value) continue;
                    long blocked = _safeguardBlockedUntil;
                    _controllerSpeed = command.Speed;
                    ApplyControllerPosition(command.Control.HasValue ? $"Child:{command.Servo}:{command.Control}" : "Servo:" + command.Servo, command.NumericValue);
                    if (_safeguardBlockedUntil != blocked || _safeguardHolding)
                    { StopRecordingReplay(); _recordingWindow?.SetStatus("Replay stopped by Collision Safeguard."); return; }
                    _recordingReplayLast[key] = value;
                }
            }
            finally { _controllerSpeed = originalSpeed; }
            if (elapsed >= _recordingReplay.Duration)
            {
                _recordingReplayTimer.Stop(); _recordingWindow?.SetReplayState(false);
                // Leave permission for the final calibrated target to settle.
                // Input remains blocked until this draft is kept or discarded.
                _recordingWindow?.SetStatus("Replay finished. Replay again, adjust smoothing, keep the take, or discard it.");
            }
        }
        catch (Exception ex) { StopRecordingReplay(); _recordingWindow?.SetStatus("Replay stopped: " + ex.Message); }
    }

    private void StopRecordingReplay()
    {
        bool hadOutput = _recordingReplayOutput;
        _recordingReplayTimer.Stop(); _recordingReplayTimer.Tick -= RecordingReplay_Tick;
        _recordingReplayClock.Reset(); _recordingReplayOutput = false; _recordingReplay = null; _recordingReplayLast.Clear();
        _recordingWindow?.SetReplayState(false);
        if (hadOutput) { ResetControllerMotion(); RefreshControllerOutputPermissions(); }
    }
}
