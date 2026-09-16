using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ServoAnimator;

public partial class MainWindow
{
    private SdlControllerService _controllers;
    private readonly DispatcherTimer _controllerTimer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly ControllerPreviewUpdates _controllerPreview = new();
    private ControllerMappingWindow _controllerMappingWindow;
    private readonly Dictionary<ControllerKind, ControllerProfile> _controllerProfiles = new();
    private readonly Dictionary<ControllerKind, string> _controllerMappingErrors = new();
    private readonly Dictionary<ControllerKind, ControllerInputEngine> _controllerEngines = Enum.GetValues<ControllerKind>().ToDictionary(k => k, _ => new ControllerInputEngine());
    private readonly Dictionary<ControllerKind, ControlConnectionState> _controllerConnections = Enum.GetValues<ControllerKind>().ToDictionary(k => k, _ => new ControlConnectionState());
    private readonly Dictionary<string, double> _controllerPositions = new();
    private ControllerKind? _enabledController;
    private string _controllerConfigRoot;
    private long _controllerLastTick;
    private ServoSpeed _controllerSpeed = ServoSpeed.NoChange;
    private bool _controllerWasLive;
    private long _controllerOutputEpoch;
    private readonly ConcurrentDictionary<string, Action> _controllerPendingHardware = new();
    private int _controllerWriteQueued;
    private bool _controllerConfigOpen;
    private ControllerSample _lastXboxSample = new(), _lastSteamSample = new();
    private bool _showDisconnectedSteamMapping;

    private void RefreshActiveControllerMapping()
    {
        bool Available(ControllerKind kind) => (_urdfUndocked ? _controllerConnections[kind].Connected : _controllerConnections[kind].Enabled)
            && !_controllerMappingErrors.ContainsKey(kind) && _controllerProfiles.ContainsKey(kind);
        bool steamPreview = _showDisconnectedSteamMapping && !_controllerConnections[ControllerKind.Steam].Connected
            && _controllerProfiles.ContainsKey(ControllerKind.Steam);
        ControllerKind? kind = steamPreview ? ControllerKind.Steam
            : _enabledController.HasValue && Available(_enabledController.Value) ? _enabledController
            : Available(ControllerKind.Xbox) ? ControllerKind.Xbox : Available(ControllerKind.Steam) ? ControllerKind.Steam : null;
        CommandsAtPointContent.Visibility = !_urdfUndocked && kind.HasValue ? Visibility.Collapsed : Visibility.Visible;
        ActiveControllerMapping.Visibility = !_urdfUndocked && kind.HasValue ? Visibility.Visible : Visibility.Collapsed;
        bool separateMapping = _urdfUndocked && kind.HasValue;
        if ((UndockedControllerMappingHost.Visibility == Visibility.Visible) != separateMapping)
        {
            UndockedControllerMappingHost.Visibility = separateMapping ? Visibility.Visible : Visibility.Collapsed;
            if (_urdfUndocked)
            {
                CommandsEditorColumn.Width = separateMapping ? _lastDockedServoColumnWidth : new GridLength(1, GridUnitType.Star);
                UrdfEditorColumn.MinWidth = separateMapping ? 260 : 0;
                UrdfEditorColumn.Width = separateMapping ? _lastDockedUrdfColumnWidth : new GridLength(0);
                UrdfSplitterColumn.Width = new GridLength(separateMapping ? 8 : 0);
                UrdfColumnSplitter.Visibility = separateMapping ? Visibility.Visible : Visibility.Collapsed;
                System.Windows.Controls.Grid.SetColumnSpan(CommandsAtPointPanel, separateMapping ? 1 : 3);
                CommandsAtPointPanel.Margin = new Thickness(6, 5, separateMapping ? 0 : 6, 0);
            }
        }
        UrdfColumnSplitter.ToolTip = _urdfUndocked ? "Drag to resize Commands / controller mapping" : "Drag to resize Commands / URDF model";
        if (kind.HasValue)
            (_urdfUndocked ? UndockedControllerMapping : ActiveControllerMapping).ShowMapping(_controllerProfiles[kind.Value],
                steamPreview ? new ControllerSample() : kind == ControllerKind.Xbox ? _lastXboxSample : _lastSteamSample);
    }

    private void InitializeControllers()
    {
        _controllers = new SdlControllerService();
        LoadControllerProfiles();
        _controllerTimer.Tick += (_, _) => PollControllers();
        _controllerTimer.Start();
        UrdfRenderLoop.Current.AddTargets(RenderControllerPreview);
        PollControllers(true);
    }
    private void RenderControllerPreview(TimeSpan frameTime)
    {
        if (!ControllerOutputAllowed(false)) { _controllerPreview.Clear(); return; }
        try { _controllerPreview.Flush(); }
        catch (Exception ex) { DisableControllerInput(); ShowStatus("Controller preview stopped: " + ex.Message); }
    }
    private void LoadControllerProfiles()
    {
        DisableControllerInput(manual: false);
        _controllerConfigRoot = ConfigRoot;
        LoadFocusControl();
        foreach (ControllerKind kind in Enum.GetValues<ControllerKind>())
        {
            try { _controllerProfiles[kind] = ControllerProfileStore.Load(ConfigRoot, kind); _controllerMappingErrors.Remove(kind); }
            catch (Exception ex) { _controllerProfiles[kind] = ControllerProfile.Empty(kind); _controllerMappingErrors[kind] = ex.Message; }
        }
    }
    private void DisableControllerInput(bool manual = true)
    {
        if (manual) foreach (var state in _controllerConnections.Values) state.SetEnabled(false);
        _enabledController = null; XboxControllerButton.IsChecked = SteamControllerButton.IsChecked = false;
        _controllerPhysicalOutputAllowed = false;
        ResetControllerMotion();
        RefreshActiveControllerMapping();
    }
    private void ResetControllerMotion()
    {
        _safeguardTargets.Clear();
        StopControllerLibrary();
        foreach (var engine in _controllerEngines.Values) engine.Reset();
        _controllerPositions.Clear();
        Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear();
    }
    private void PollControllers(bool force = false)
    {
        if (_controllers == null) return;
        try
        {
            if (_controllerConfigRoot != ConfigRoot) LoadControllerProfiles();
            _controllers.Update(force);
            var xbox = _controllers.Read(ControllerKind.Xbox); var steam = _controllers.Read(ControllerKind.Steam);
            long now = Environment.TickCount64;
            double dt = (now - _controllerLastTick) / 1000.0; _controllerLastTick = now;
            ProcessControllerSamples(xbox, steam, dt);
        }
        catch (Exception ex) { DisableControllerInput(); ShowStatus("Controller input stopped: " + ex.Message); }
    }
    private void ProcessControllerSamples(ControllerSample xbox, ControllerSample steam, double dt)
    {
        _lastXboxSample = xbox; _lastSteamSample = steam;
        try
        {
            if (_speedCalibrationBusy) { foreach (var engine in _controllerEngines.Values) engine.Reset(); return; }
            SetControllerStatus(ControllerKind.Xbox, xbox); SetControllerStatus(ControllerKind.Steam, steam);
            RefreshControllerOutputPermissions();
            if (_controllerConfigOpen) return; // The config dialog owns URDF-only testing.
            if (_enabledController.HasValue && !ControllerFocusAllowed(_enabledController.Value)) ResetControllerMotion();
            if (_controllerWasLive != LiveDrive) { if (!_apiLibraryOwnsOutput) ResetControllerMotion(); _controllerWasLive = LiveDrive; }
            var available = new List<(ControllerKind Kind, ControllerIntent Intent)>();
            foreach (var kind in Enum.GetValues<ControllerKind>())
            {
                if (!_controllerConnections[kind].Enabled || _controllerMappingErrors.ContainsKey(kind) || !ControllerFocusAllowed(kind))
                { _controllerEngines[kind].Reset(); continue; }
                var sample = kind == ControllerKind.Xbox ? xbox : steam;
                available.AddRange(_controllerEngines[kind].Evaluate(_controllerProfiles[kind], sample, dt, ControllerCurrentValue).Select(i => (kind, i)));
            }
            // A fresh Library button takes priority over stick motion and ordinary
            // actions in the same poll. Stop/disable always take first priority.
            foreach (var entry in available.OrderBy(i => i.Intent.Target is "Action:Stop" or "Action:Disable servos" ? 0 : ControllerTargets.IsLibrary(i.Intent.Target) ? 1 : 2))
            {
                var intent = entry.Intent;
                if (!intent.Target.StartsWith("Action:") && !ControllerTargets.IsLibrary(intent.Target) && (IsRunning || _controllerLibraryRun != null)) continue;
                if (_enabledController != entry.Kind || _apiLibraryOwnsOutput)
                {
                    StopControllerLibrary(); _controllerPositions.Clear();
                    Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear();
                    _enabledController = entry.Kind; RefreshControllerOutputPermissions();
                }
                if (ControllerTargets.IsLibrary(intent.Target)) { StartControllerLibrary(intent); break; }
                if (intent.Target.StartsWith("Action:")) { ControllerAction(intent.Target[7..]); if (_enabledController == null || !IsEnabled || IsRunning || intent.Target == "Action:Stop") break; }
                else if (!IsRunning && _controllerLibraryRun == null) ApplyControllerPosition(intent.Target, intent.Value);
            }
            if (!_apiLibraryOwnsOutput) TickControllerLibrary();
        }
        finally { RefreshActiveControllerMapping(); RefreshCollisionSafeguardButton(); }
    }
    private void SetControllerStatus(ControllerKind kind, ControllerSample sample)
    {
        var connection = _controllerConnections[kind];
        if (kind == ControllerKind.Steam && sample.Connected) _showDisconnectedSteamMapping = false;
        if (connection.Connected != sample.Connected) _controllerEngines[kind].Reset();
        connection.Connected = sample.Connected;
        if (!connection.Enabled && _enabledController == kind)
        { ResetControllerMotion(); _enabledController = null; RefreshControllerOutputPermissions(); }
        var dot = kind == ControllerKind.Xbox ? XboxControllerDot : SteamControllerDot;
        var button = kind == ControllerKind.Xbox ? XboxControllerButton : SteamControllerButton;
        button.IsChecked = connection.Enabled && !_controllerMappingErrors.ContainsKey(kind)
            || kind == ControllerKind.Steam && !sample.Connected && _showDisconnectedSteamMapping;
        dot.Fill = sample.Connected ? Brushes.LimeGreen : Brushes.IndianRed;
        string device = _controllers?.Devices.FirstOrDefault(d => d.Id == sample.DeviceId)?.Name;
        string state = sample.Connected ? "Connected: " + device : _controllers?.Error ?? "Not connected";
        bool noMux = _controllerProfiles.TryGetValue(kind, out var profile) && profile.NoMux;
        state += connection.ManuallyDisabled ? " · control disabled" : connection.Enabled ? " · control enabled" : "";
        if (connection.Enabled && profile != null) state += " · " + (noMux ? "No MUX" : ControllerCatalog.LayerNames[profile.ActiveLayer(sample)]);
        dot.ToolTip = state; button.ToolTip = state + "\nURDF preview; Drive HW routes to live hardware. Background destinations are set in Focus Control.\n"
            + (noMux ? "Left and Right Shoulder are mappable buttons." : "Hold shoulder buttons for four mapping layers.");
        if (kind == ControllerKind.Steam && !sample.Connected)
            button.ToolTip = "Steam Controller not connected. Click to toggle its mapping preview. This does not enable controller output.";
    }
    private void ControllerEnable_Click(object sender, RoutedEventArgs e)
    {
        ControllerKind kind = ReferenceEquals(sender, XboxControllerButton) ? ControllerKind.Xbox : ControllerKind.Steam;
        if (kind == ControllerKind.Steam && !_controllerConnections[kind].Connected)
        {
            _showDisconnectedSteamMapping = !_showDisconnectedSteamMapping;
            SteamControllerButton.IsChecked = _showDisconnectedSteamMapping;
            RefreshActiveControllerMapping();
            ShowStatus(_showDisconnectedSteamMapping ? "Steam Controller mapping preview — controller not connected" : "Automatic controller mapping display restored");
            return;
        }
        bool requested = (kind == ControllerKind.Xbox ? XboxControllerButton : SteamControllerButton).IsChecked == true;
        _controllerConnections[kind].SetEnabled(requested);
        _controllerEngines[kind].Reset();
        if (!requested)
        {
            if (_enabledController == kind) { ResetControllerMotion(); _enabledController = null; RefreshControllerOutputPermissions(); }
            RefreshActiveControllerMapping();
            ShowStatus(kind + " controller disabled — click again to enable"); return;
        }
        if (_controllers == null || _controllers.Error != null)
        { _controllers?.Dispose(); _controllers = new SdlControllerService(); }
        PollControllers(true);
        var sample = _controllers.Read(kind);
        if (!sample.Connected) { ShowStatus(kind + " controller not available. " + (_controllers.Error ?? "Connect or pair the controller and try again.")); return; }
        if (_controllerMappingErrors.TryGetValue(kind, out string error))
        { ShowStatus("Open " + kind + " Controller Mapping and save a valid profile: " + error); return; }
        (kind == ControllerKind.Xbox ? XboxControllerButton : SteamControllerButton).IsChecked = true;
        ShowStatus(kind + " controller enabled — release sticks/buttons to neutral, then drive " + (LiveDrive ? "URDF and live hardware" : "URDF preview"));
    }
    private void XboxControllerMapping_Click(object sender, RoutedEventArgs e) => OpenControllerMapping(ControllerKind.Xbox);
    private void SteamControllerMapping_Click(object sender, RoutedEventArgs e) => OpenControllerMapping(ControllerKind.Steam);
    private void OpenControllerMapping(ControllerKind kind)
    {
        if (_controllerProfiles.Count == 0) LoadControllerProfiles();
        ResetControllerMotion();
        var window = new ControllerMappingWindow(_controllerProfiles[kind], () => _controllers?.Read(kind) ?? new ControllerSample(),
            profile => { ControllerProfileStore.Save(ConfigRoot, profile); _controllerProfiles[kind] = profile.Clone(); _controllerMappingErrors.Remove(kind); },
            ChooseControllerLibrary, ControllerCurrentValue, PreviewControllerConfiguration,
            () => _focusControl.AllowsOutput(kind, physical: false), AdvanceControllerRelative) { Owner = this };
        _controllerConfigOpen = true;
        _controllerMappingWindow = window;
        try { window.ShowDialog(); }
        finally { ResetControllerMotion(); _controllerConfigOpen = false; _controllerMappingWindow = null; }
    }
    private void PreviewControllerConfiguration(IReadOnlyList<ControllerIntent> intents, bool testing)
    {
        if (!_controllerConfigOpen) return;
        if (!testing) { StopControllerLibrary(); _controllerPositions.Clear(); return; }
        if (IsRunning) PausePlayback();
        foreach (var intent in intents.OrderBy(i => i.Target == "Action:Stop" ? 0 : ControllerTargets.IsLibrary(i.Target) ? 1 : 2))
        {
            if (intent.Target == "Action:Stop") { StopControllerLibrary(); break; }
            if (ControllerTargets.IsLibrary(intent.Target)) { StartControllerLibrary(intent); break; }
            if (_controllerLibraryRun != null) continue;
            if (ControllerTargets.TryServo(intent.Target, out _, out _)) ApplyControllerPosition(intent.Target, intent.Value);
            else if (intent.Target == "Action:Default pose")
                foreach (var row in _rows.Where(r => !r.IsTextRow)) ApplyControllerPosition("Servo:" + row.Servo, 0);
            else if (intent.Target.StartsWith("Action:Speed ", StringComparison.Ordinal))
                ControllerAction(intent.Target[7..]);
        }
        TickControllerLibrary();
    }
    private double ControllerCurrentValue(string target)
    {
        if (_controllerPositions.TryGetValue(target, out double value)) return value;
        if (!ControllerTargets.TryServo(target, out var servo, out var child)) return 0;
        var row = _rows.FirstOrDefault(r => r.Servo == servo);
        return child.HasValue ? row?.Children.FirstOrDefault(c => c.Control == child)?.Value ?? row?.Value ?? 0 : row?.Value ?? 0;
    }
    private double AdvanceControllerRelative(string target, double before, double input, double seconds)
    {
        if (!ControllerTargets.TryServo(target, out var servo, out var child)) return before;
        var view = (_urdfUndocked ? _head?.HeadView : EmbeddedHeadView) ?? EmbeddedHeadView;
        if (view == null) return before;
        ConfigureMotionView(view);
        return view.AdvanceControllerTarget(servo, child, before, input, seconds, _controllerSpeed);
    }
    private void ApplyControllerPosition(string target, double value)
    {
        if (_speedCalibrationBusy) return;
        if (!ControllerTargets.TryServo(target, out var servo, out var child)) return;
        var speed = _controllerSpeed;
        if (!AllowControllerTargets(new[] { new ServoCommand { Servo = servo, Control = child, NumericValue = (int)Math.Round(value), Speed = speed } })) return;
        _controllerPositions[target] = value;
        int position = (int)Math.Round(value);
        var row = _rows.First(r => r.Servo == servo);
        _manualPoseOverrides.Add(servo);
        if (child.HasValue)
        {
            var childRow = row.Children.FirstOrDefault(c => c.Control == child);
            if (childRow != null) childRow.Value = value;
            QueueControllerVisual(target, v => { PrepareDirectMotion(v, servo, child, speed, controller: true); v.SetChildServo(servo, child.Value, position); });
        }
        else { row.Value = value; QueueControllerVisual(target, v => { PrepareDirectMotion(v, servo, null, speed, controller: true); v.SetServo(servo, position); }); }
        if (!ControllerOutputAllowed(true) || !LiveDrive || !_hw.Connected) return;
        _controllerPendingHardware[target] = child.HasValue
            ? () => _hw.DriveControlValue(servo, child.Value, speed, position, ServoCommand.RangeFor(servo).Min < 0)
            : () => _hw.DriveGang(servo, speed, position);
        if (Interlocked.Exchange(ref _controllerWriteQueued, 1) == 0)
        {
            long epoch = Interlocked.Read(ref _controllerOutputEpoch);
            _hardwarePlaybackQueue.EnqueueBarrier(() =>
            {
                try
                {
                    foreach (var key in _controllerPendingHardware.Keys)
                        if (_controllerPhysicalOutputAllowed && epoch == Interlocked.Read(ref _controllerOutputEpoch) && _controllerPendingHardware.TryRemove(key, out var action)) action();
                }
                finally { Interlocked.Exchange(ref _controllerWriteQueued, 0); }
            });
        }
    }
    private void QueueControllerVisual(string target, Action<RobotHeadView> apply)
    {
        if (ControllerOutputAllowed(false))
            _controllerPreview.Enqueue(target, () => { if (ControllerOutputAllowed(false)) ForEachHeadView(apply); });
    }

    private void ControllerAction(string action)
    {
        if (IsRunning && action is not ("Disable servos" or "Play / pause sequence" or "Stop" or "Play / pause movie" or "Previous sequence" or "Next sequence")) return;
        switch (action)
        {
            case "Disable servos": DisableControllerInput(); DisableAll_Click(this, new RoutedEventArgs()); break;
            case "Snapshot": _controllerPreview.Flush(); InsertPoseAtCursor(); ResetControllerMotion(); break;
            case "Default pose":
                foreach (var row in _rows.Where(r => !r.IsTextRow)) ApplyControllerPosition("Servo:" + row.Servo, 0);
                break;
            case "RGB ClearAll":
                var frame = _rgbSimulator.PreviewCommand("ClearAll");
                QueueControllerVisual("RGB", v => v.SetRgbRingFrame(frame));
                if (ControllerOutputAllowed(true) && LiveDrive && _hw.Connected) _hw.DriveRgb("ClearAll");
                break;
            case "Play / pause sequence": ControllerTransport(() => PlayPause_Click(this, new RoutedEventArgs())); break;
            case "Stop": SequenceStop_Click(this, new RoutedEventArgs()); break;
            case "Play / pause movie": ControllerTransport(() => MoviePlay_Click(this, new RoutedEventArgs())); break;
            case "Previous sequence": ControllerTransport(() => MoviePrevious_Click(this, new RoutedEventArgs())); break;
            case "Next sequence": ControllerTransport(() => MovieNext_Click(this, new RoutedEventArgs())); break;
            default:
                if (action.StartsWith("Speed ") && Enum.TryParse<ServoSpeed>(action[6..], out var speed)) _controllerSpeed = speed;
                break;
        }
    }
}
