namespace ServoAnimator;

public sealed class ControllerSample
{
    public uint DeviceId { get; init; }
    public bool Connected { get; init; }
    public bool LeftShoulder { get; init; }
    public bool RightShoulder { get; init; }
    // Missing keys mean an input is unavailable or a touch is no longer down.
    public Dictionary<string, double> Values { get; init; } = new();
    public HashSet<string> Supported { get; init; } = new();
    public bool TryValue(string id, out double value)
    {
        if (id is "LeftShoulder" or "RightShoulder") { value = (id == "LeftShoulder" ? LeftShoulder : RightShoulder) ? 1 : 0; return true; }
        return Values.TryGetValue(id, out value);
    }
}

public sealed record ControllerIntent(string Target, double Value, string LibraryName = "", bool Loop = false);
public sealed record ControllerReading(double Raw, bool Active, double? Mapped, string State);

/// <summary>Four independent banks. A layer/device change waits for neutral before
/// accepting each input. Held buttons cannot become fresh presses in another bank.</summary>
public sealed class ControllerInputEngine
{
    // Supplied by the editor's shared calibration model. Without a model,
    // relative motion holds rather than inventing an independent speed.
    public Func<string, double, double, double, double> RelativeMotion { get; set; }
    private int _layer = -1;
    private uint _device;
    private bool _noMux;
    private readonly HashSet<string> _armed = new();
    private readonly HashSet<string> _down = new();
    private readonly HashSet<string> _activeAxes = new();
    private readonly Dictionary<string, double> _sensorZero = new();
    public Dictionary<string, ControllerReading> Readings { get; } = new();
    public void Reset() { _layer = -1; _armed.Clear(); _down.Clear(); _activeAxes.Clear(); _sensorZero.Clear(); Readings.Clear(); }
    public int ActiveLayer => _layer;
    public IReadOnlyList<ControllerIntent> Evaluate(ControllerProfile profile, ControllerSample sample,
        double elapsedSeconds, Func<string, double> current)
    {
        var output = new List<ControllerIntent>();
        Readings.Clear();
        if (!sample.Connected) { Reset(); return output; }
        int layer = profile.ActiveLayer(sample);
        if (_device != sample.DeviceId || _layer != layer || _noMux != profile.NoMux)
        {
            Reset(); _layer = layer; _device = sample.DeviceId; _noMux = profile.NoMux;
            foreach (var p in sample.Values.Where(p => p.Key.StartsWith("Accel"))) _sensorZero[p.Key] = p.Value;
        }
        double dt = Math.Clamp(elapsedSeconds, 0, 0.05);
        foreach (var input in ControllerCatalog.Inputs(profile.Kind, profile.NoMux))
        {
            if (!sample.TryValue(input.Id, out double value) || !double.IsFinite(value))
            { _down.Remove(input.Id); _activeAxes.Remove(input.Id); _armed.Add(input.Id); continue; }
            if (!profile.MappingForLayer(layer).TryGetValue(input.Id, out var binding)) continue;
            double raw = value;
            if (input.Id.StartsWith("Accel")) value -= _sensorZero.GetValueOrDefault(input.Id);
            if (input.Section == "Motion") value /= binding.SensorScale;
            value = Math.Clamp(value, input.Unipolar ? 0 : -1, 1);
            bool pressed = value > 0.5;
            bool neutral = input.Analog ? Math.Abs(value) <= binding.DeadZone : !pressed;
            double? mapped = ControllerTargets.TryServo(binding.Target, out _, out _) ? current(binding.Target) : null;
            Readings[input.Id] = new(raw, !neutral, mapped, string.IsNullOrEmpty(binding.Target) ? "Unassigned" : "Ready");
            if (input.Section == "Motion" && !string.IsNullOrEmpty(binding.TriggerButton)
                && !ControllerCatalog.TriggerHeld(sample, binding.TriggerButton))
            {
                // A released gate is an explicit neutral state: the user may
                // hold its button while the sensor is already sending motion.
                // Drop edge/absolute-axis history so release never snaps a target
                // back to center or leaves a previous press latched.
                _armed.Add(input.Id); _down.Remove(input.Id); _activeAxes.Remove(input.Id);
                string label = ControllerCatalog.TriggerButtons(profile.Kind, profile.NoMux).FirstOrDefault(i => i.Id == binding.TriggerButton)?.Label ?? binding.TriggerButton;
                Readings[input.Id] = Readings[input.Id] with { State = "Hold " + label };
                continue;
            }
            if (!_armed.Contains(input.Id))
            {
                if (neutral) _armed.Add(input.Id);
                else Readings[input.Id] = Readings[input.Id] with { State = "Release to arm" };
                continue;
            }
            bool rising = pressed && !_down.Contains(input.Id);
            if (pressed) _down.Add(input.Id); else _down.Remove(input.Id);
            if (string.IsNullOrEmpty(binding.Target)) continue;
            if (binding.Target.StartsWith("Action:") || ControllerTargets.IsLibrary(binding.Target))
            {
                Readings[input.Id] = Readings[input.Id] with { State = ControllerTargets.IsLibrary(binding.Target)
                    ? binding.LibraryName + (binding.Loop ? " ↻" : "") : binding.Target[7..] };
                if (rising) output.Add(new(binding.Target, 0, binding.LibraryName, binding.Loop));
                continue;
            }
            double before = current(binding.Target), result;
            if (!input.Analog || binding.Mode is ControllerMapMode.Set or ControllerMapMode.Toggle)
            {
                if (!rising) continue;
                result = binding.Mode == ControllerMapMode.Toggle && Math.Abs(before - binding.High) < Math.Abs(before - binding.Low)
                    ? binding.Low : binding.High;
            }
            else
            {
                if (neutral)
                {
                    value = 0;
                    if (binding.Mode == ControllerMapMode.Relative || !_activeAxes.Remove(input.Id)) continue;
                }
                else { _activeAxes.Add(input.Id); value = Math.Sign(value) * (Math.Abs(value) - binding.DeadZone) / (1 - binding.DeadZone); }
                if (binding.Invert) value = input.Unipolar && binding.Mode == ControllerMapMode.Absolute ? 1 - value : -value;
                result = binding.Mode == ControllerMapMode.Relative ? RelativeMotion?.Invoke(binding.Target, before, value, dt) ?? before
                    : binding.Low + (input.Unipolar ? value : (value + 1) / 2) * (binding.High - binding.Low);
            }
            if (!double.IsFinite(result)) continue;
            result = Math.Clamp(result, binding.Low, binding.High);
            Readings[input.Id] = Readings[input.Id] with { Mapped = result };
            if (Math.Abs(result - before) > 0.0001) output.Add(new(binding.Target, result));
        }
        return output;
    }
}
