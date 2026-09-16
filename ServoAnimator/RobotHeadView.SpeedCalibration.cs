namespace ServoAnimator;

public sealed partial class RobotHeadView
{
    private SpeedCalibrationData _speedCalibration;
    private sealed class MotionChannel(ServoNames parent, double pulse)
    {
        internal ServoNames Parent = parent;
        internal readonly ServoMotionState Motion = new(pulse);
    }
    private readonly Dictionary<RobotControls, MotionChannel> _motionChannels = new();
    private readonly Dictionary<RobotControls, ServoMotionState> _stepperMotion = new();
    private readonly Dictionary<RobotControls, ServoSpeed> _motionSpeeds = new();
    private readonly Dictionary<(RobotControls Control, ServoSpeed Speed, bool Increasing), MotionLimits> _motionLimits = new();
    private readonly HashSet<RobotControls> _motionDisabled = new();
    private bool _motionRenderingInitialized;
    private TimeSpan? _motionLastFrame;
    private bool? _lastMotionAllowed;
    private bool _renderingCalibrated;
    private bool _calibratedMotionPaused;
    public bool CalibratedMotionPaused
    {
        get => _calibratedMotionPaused;
        set { if (_calibratedMotionPaused != value) _motionLastFrame = null; _calibratedMotionPaused = value; }
    }
    public bool SnapCalibratedMotion { get; set; }
    public Func<bool> CalibratedMotionAllowed { get; set; }
    public bool UsesCalibratedMotion => _speedCalibration?.UseInUrdf == true;

    public void SetSpeedCalibration(SpeedCalibrationData data)
    {
        if (ReferenceEquals(_speedCalibration, data)) return;
        _speedCalibration = data; _motionChannels.Clear(); _stepperMotion.Clear(); _motionLimits.Clear(); _motionLastFrame = null;
        if (!_motionRenderingInitialized)
        {
            _motionRenderingInitialized = true;
            Loaded += (_, _) => StartCalibratedRendering();
            Unloaded += (_, _) => { UrdfRenderLoop.Current.RemoveMotion(RenderCalibratedMotion); _motionLastFrame = null; };
        }
        if (IsLoaded) StartCalibratedRendering();
    }
    private void StartCalibratedRendering()
    {
        _motionLastFrame = null;
        UrdfRenderLoop.Current.AddMotion(RenderCalibratedMotion);
    }
    internal void RefreshCalibratedMotionPermission()
    {
        bool allowed = UsesCalibratedMotion && !CalibratedMotionPaused && !_poseEditEnabled && _urdfDriveEnabled && CalibratedMotionAllowed?.Invoke() != false;
        if (_lastMotionAllowed != allowed) _motionLastFrame = null;
        _lastMotionAllowed = allowed;
    }
    internal void RenderCalibratedMotion(TimeSpan frameTime)
    {
        RefreshCalibratedMotionPermission();
        double elapsed = _motionLastFrame.HasValue ? (frameTime - _motionLastFrame.Value).TotalSeconds : 0;
        _motionLastFrame = frameTime;
        if (_poseEditEnabled) { _motionChannels.Clear(); _stepperMotion.Clear(); return; }
        if (_lastMotionAllowed == true) AdvanceCalibratedMotion(elapsed);
    }
    public void ConfigureCalibratedSpeed(ServoNames parent, RobotControls? child, ServoSpeed speed)
    {
        if (speed == ServoSpeed.NoChange) return;
        foreach (var control in child.HasValue ? new[] { child.Value } : ServoConfiguration.ControlsFor(parent)) _motionSpeeds[control] = speed;
    }
    private MotionLimits CalibratedLimits(ServoConfigEntry entry, ServoSpeed speed, bool increasing)
    {
        var key = (entry.Control, speed, increasing);
        if (!_motionLimits.TryGetValue(key, out var limits)) _motionLimits[key] = limits = _speedCalibration.Limits(entry, speed, increasing);
        return limits;
    }

    // Translate calibrated PWM velocity into native controller travel. This
    // generates targets only: the existing servo/stepper integrator and physical
    // device still apply acceleration and the final motion limits once.
    internal double AdvanceControllerTarget(ServoNames parent, RobotControls? child, double before,
        double input, double seconds, ServoSpeed requestedSpeed)
    {
        if (_speedCalibration == null || !double.IsFinite(before) || !double.IsFinite(input) || !double.IsFinite(seconds) || seconds <= 0 || input == 0) return before;
        var range = ServoCommand.RangeFor(parent);
        before = Math.Clamp(before, range.Min, range.Max);
        input = Math.Clamp(input, -1, 1);
        double endpoint = input > 0 ? range.Max : range.Min;
        double distance = Math.Abs(endpoint - before);
        if (distance == 0) return before;
        double progress = distance;
        bool any = false;
        foreach (var control in child.HasValue ? new[] { child.Value } : ServoConfiguration.ControlsFor(parent))
        {
            any = true;
            if (control is RobotControls.LeftEyePop or RobotControls.RightEyePop)
            {
                progress = Math.Min(progress, Math.Abs(input) * seconds * 2000 / _speedCalibration.StepperSeconds(control, input > 0));
                continue;
            }
            var entry = _servoConfiguration.Get(control);
            if (entry == null) return before;
            double from = ServoPulseMapping.ToPulse(_servoConfiguration, parent, entry, before);
            double to = ServoPulseMapping.ToPulse(_servoConfiguration, parent, entry, endpoint);
            var speed = requestedSpeed == ServoSpeed.NoChange ? _motionSpeeds.GetValueOrDefault(control, ServoSpeed.Default) : requestedSpeed;
            var limits = CalibratedLimits(entry, speed, to >= from);
            // With unlimited configured speed and finite acceleration there
            // is no cruise velocity to scale by stick deflection. Use the
            // full-stroke peak from that same calibrated triangular profile.
            double velocity = double.IsPositiveInfinity(limits.Speed) && double.IsFinite(limits.Acceleration)
                ? Math.Sqrt(limits.Acceleration * Math.Max(0, entry.MaxPwm - entry.MinPwm)) : limits.Speed;
            double advance = velocity * Math.Abs(input) * seconds;
            if (double.IsNaN(advance) || advance < 0) return before;
            double pulse = from + Math.Sign(to - from) * Math.Min(Math.Abs(to - from), advance);
            double native = ServoPulseMapping.ToLogical(_servoConfiguration, parent, entry, pulse);
            // A gang shares one logical target. Its slowest calibrated member
            // determines the common pace; reversals and asymmetric PWM spans
            // are handled per member before comparing native travel.
            progress = Math.Min(progress, Math.Max(0, (native - before) * Math.Sign(input)));
        }
        return any ? before + Math.Sign(input) * progress : before;
    }
    public void SetCalibratedControlEnabled(ServoNames parent, RobotControls? child, bool enabled)
    {
        foreach (var control in child.HasValue ? new[] { child.Value } : ServoConfiguration.ControlsFor(parent))
        {
            if (enabled) _motionDisabled.Remove(control);
            else
            {
                _motionDisabled.Add(control);
                if (_motionChannels.TryGetValue(control, out var channel)) channel.Motion.Reset(channel.Motion.Position);
                if (_stepperMotion.TryGetValue(control, out var stepper)) stepper.Reset(stepper.Position);
            }
        }
    }
    public void StopCalibratedMotion()
    {
        foreach (var channel in _motionChannels.Values) channel.Motion.Reset(channel.Motion.Position);
        foreach (var stepper in _stepperMotion.Values) stepper.Reset(stepper.Position);
        _motionLastFrame = null; CalibratedMotionPaused = false;
    }
    private double CurrentNeckMotion(ServoNames parent, RobotControls control)
    {
        if (!_motionChannels.TryGetValue(control, out var channel)) return 0;
        channel.Parent = parent;
        return ServoPulseMapping.ToLogical(_servoConfiguration, parent, _servoConfiguration.Get(control), channel.Motion.Position);
    }
    private double CalibratedControlValue(ServoNames parent, RobotControls control, double target)
    {
        if (!UsesCalibratedMotion || _poseInternalUpdate || _poseEditEnabled || _renderingCalibrated) return target;
        if (control is RobotControls.LeftEyePop or RobotControls.RightEyePop)
        {
            if (!_stepperMotion.TryGetValue(control, out var stepper))
                _stepperMotion[control] = stepper = new(control == RobotControls.LeftEyePop ? _leftEyePopLogical : _rightEyePopLogical);
            if (!_motionDisabled.Contains(control) || SnapCalibratedMotion) stepper.Target = Math.Clamp(target, 0, 2000);
            if (SnapCalibratedMotion) stepper.Reset(stepper.Target);
            UpdatePoseStateForChild(parent, control, stepper.Position);
            return stepper.Position;
        }
        if ((int)control is < 0 or > 23) return target;
        var entry = _servoConfiguration.Get(control); if (entry == null) return target;
        if (parent is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
        {
            foreach (var other in _motionChannels.Where(p => p.Key is RobotControls.NeckTiltLeft or RobotControls.NeckTiltRight))
                other.Value.Parent = parent;
        }
        double pulse = ServoPulseMapping.ToPulse(_servoConfiguration, parent, entry, target);
        if (!_motionChannels.TryGetValue(control, out var channel))
        {
            double before = control switch { RobotControls.NeckTurn => _lastAppliedNeckTurn, RobotControls.NeckTiltLeft => _lastAppliedNeckLeft,
                RobotControls.NeckTiltRight => _lastAppliedNeckRight, _ => _lastControlValues.GetValueOrDefault((parent, control)) };
            if (!double.IsFinite(before)) before = 0;
            var previousParent = control is RobotControls.NeckTiltLeft or RobotControls.NeckTiltRight ? _lastAppliedNeckMode ?? parent : parent;
            channel = new(parent, ServoPulseMapping.ToPulse(_servoConfiguration, previousParent, entry, before)); _motionChannels[control] = channel;
        }
        channel.Parent = parent;
        if (!_motionDisabled.Contains(control) || SnapCalibratedMotion) channel.Motion.Target = pulse;
        if (SnapCalibratedMotion) channel.Motion.Reset(pulse);
        double value = ServoPulseMapping.ToLogical(_servoConfiguration, parent, entry, channel.Motion.Position);
        UpdatePoseStateForChild(parent, control, value);
        return value;
    }
    internal void AdvanceCalibratedMotion(double seconds)
    {
        if (!UsesCalibratedMotion || CalibratedMotionPaused || _poseEditEnabled || !_urdfDriveEnabled || CalibratedMotionAllowed?.Invoke() == false || seconds <= 0 || _scene == null) return;
        _renderingCalibrated = true; _suppressCollisionRefresh = true;
        try
        {
            var candidates = new List<CollisionMotionTarget>();
            var servoBefore = _motionChannels.ToDictionary(p => p.Key, p => p.Value.Motion.Position);
            var stepperBefore = _stepperMotion.ToDictionary(p => p.Key, p => p.Value.Position);
            foreach (var pair in _stepperMotion)
            {
                if (_motionDisabled.Contains(pair.Key)) continue;
                pair.Value.Advance(seconds, new(2000 / _speedCalibration.StepperSeconds(pair.Key, pair.Value.Target >= pair.Value.Position), double.PositiveInfinity), 0, 2000);
                candidates.Add(new(pair.Key == RobotControls.LeftEyePop ? ServoNames.LeftEyePop : ServoNames.RightEyePop, pair.Key, pair.Value.Position));
            }
            foreach (var pair in _motionChannels)
            {
                var channel = pair.Value; var entry = _servoConfiguration.Get(pair.Key); if (entry == null) continue;
                if (_motionDisabled.Contains(pair.Key)) continue;
                var limits = CalibratedLimits(entry, _motionSpeeds.GetValueOrDefault(pair.Key, ServoSpeed.Default), channel.Motion.Target >= channel.Motion.Position);
                channel.Motion.Advance(seconds, limits, entry.MinPwm, entry.MaxPwm);
                double value = ServoPulseMapping.ToLogical(_servoConfiguration, channel.Parent, entry, channel.Motion.Position);
                candidates.Add(new(channel.Parent, pair.Key, value));
            }
            if (CollisionSafeguardActive?.Invoke() == true && !ControllerMotionPathClear(candidates, out string reason))
            {
                foreach (var pair in servoBefore) _motionChannels[pair.Key].Motion.Reset(pair.Value);
                foreach (var pair in stepperBefore) _stepperMotion[pair.Key].Reset(pair.Value);
                CollisionSafeguardBlocked?.Invoke(reason); return;
            }
            // Render the exact floating-point state after checking the batch.
            foreach (var pair in _stepperMotion)
                SetChildServo(pair.Key == RobotControls.LeftEyePop ? ServoNames.LeftEyePop : ServoNames.RightEyePop, pair.Key, pair.Value.Position);
            foreach (var pair in _motionChannels)
            {
                var entry = _servoConfiguration.Get(pair.Key); if (entry == null) continue;
                double value = ServoPulseMapping.ToLogical(_servoConfiguration, pair.Value.Parent, entry, pair.Value.Motion.Position);
                if (pair.Key == RobotControls.NeckTurn) SetServo(ServoNames.NeckTurn, value); else SetChildServo(pair.Value.Parent, pair.Key, value);
            }
        }
        finally { _renderingCalibrated = false; _suppressCollisionRefresh = false; }
        RefreshCollisionState(allowThrottle: true);
    }
}
