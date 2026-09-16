using System.Windows;
using System.Windows.Media;

namespace ServoAnimator;

public partial class MainWindow
{
    private long _safeguardBlockedUntil;
    private bool _safeguardHolding;
    private readonly Dictionary<(ServoNames, RobotControls?), ServoCommand> _safeguardTargets = new();
    private bool ControllerSafeguardEnabled => _focusControl.CollisionSafeguard &&
        (!_apiLibraryOwnsOutput && (_enabledController.HasValue || _controllerConfigOpen) || _controllerPlaybackSource.HasValue);

    private void CollisionSafeguard_Click(object sender, RoutedEventArgs e)
    {
        _focusControl.CollisionSafeguard = CollisionSafeguardButton.IsChecked == true;
        ResetControllerMotion();
        try { _focusControl.Save(ConfigRoot); } catch (Exception ex) { ShowStatus("Could not save Collision Safeguard: " + ex.Message); }
        RefreshCollisionSafeguardButton();
    }
    private void RefreshCollisionSafeguardButton()
    {
        CollisionSafeguardButton.IsChecked = _focusControl.CollisionSafeguard;
        CollisionSafeguardLabel.Text = "Collision\nSafeguard: " + (_focusControl.CollisionSafeguard ? "On" : "Off");
        if (_focusControl.CollisionSafeguard && Environment.TickCount64 < _safeguardBlockedUntil)
        { CollisionSafeguardButton.Background = Brushes.DarkOrange; CollisionSafeguardButton.Foreground = Brushes.Black; }
        else
        { CollisionSafeguardButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, _focusControl.CollisionSafeguard ? "SequenceAccentSurface" : "ControlBackground"); CollisionSafeguardButton.ClearValue(System.Windows.Controls.Control.ForegroundProperty); }
    }
    private void SafeguardBlocked(string reason)
    {
        _safeguardBlockedUntil = Environment.TickCount64 + 750;
        Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear(); _hardwarePlaybackQueue.ClearPending();
        _controllerPreview.Clear(); ForEachHeadView(v => v.StopCalibratedMotion());
        _safeguardTargets.Clear();
        if (IsRunning && _controllerPlaybackSource.HasValue) PausePlayback();
        if (!_apiLibraryOwnsOutput && _controllerLibraryRun != null) StopControllerLibrary();
        if (!_safeguardHolding && LiveDrive && _hw.Connected && ControllerOutputAllowed(true)) HoldSafeguardHardware();
        RefreshCollisionSafeguardButton();
        ShowStatus("Collision Safeguard blocked movement: " + reason);
    }
    private bool AllowControllerTargets(IReadOnlyList<ServoCommand> commands)
    {
        if (_safeguardHolding) return false;
        if (!ControllerSafeguardEnabled) return true;
        if (!commands.Any(c => !c.IsTextServo && !c.Disable)) return true;
        var view = _urdfUndocked ? _head?.HeadView : EmbeddedHeadView;
        var proposed = new Dictionary<(ServoNames, RobotControls?), ServoCommand>(_safeguardTargets);
        foreach (var command in commands.Where(c => !c.IsTextServo && !c.Disable))
            foreach (var control in command.Control.HasValue ? new[] { command.Control.Value } : ServoConfiguration.ControlsFor(command.Servo))
            {
                var copy = command.Clone(); copy.Control = control;
                if (control == RobotControls.LeftEyePop) copy.Servo = ServoNames.LeftEyePop;
                if (control == RobotControls.RightEyePop) copy.Servo = ServoNames.RightEyePop;
                proposed[(copy.Servo, control)] = copy;
            }
        string reason = "Collision geometry is unavailable";
        if (view == null || !view.ControllerPathClear(proposed.Values.ToArray(), out reason)) { SafeguardBlocked(reason); return false; }
        _safeguardTargets.Clear(); foreach (var pair in proposed) _safeguardTargets[pair.Key] = pair.Value;
        return true;
    }
    private async void HoldSafeguardHardware()
    {
        _safeguardHolding = true; RefreshControllerOutputPermissions();
        try
        {
            var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _hardwarePlaybackQueue.EnqueueBarrier(() => drained.SetResult());
            await drained.Task;
            await _hw.HoldControllerMotionAsync();
            foreach (var engine in _controllerEngines.Values) engine.Reset();
            ShowStatus("Collision Safeguard held physical motion. Check model alignment before continuing; a stopped eye-pop motor may need re-homing.");
        }
        catch (Exception ex) { DisableControllerInput(); ShowStatus("Collision Safeguard could not confirm a hardware hold: " + ex.Message); }
        finally { _safeguardHolding = false; RefreshControllerOutputPermissions(); }
    }
}
