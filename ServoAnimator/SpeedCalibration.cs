using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServoAnimator;

public sealed record RampSample(double Seconds, double Pulse);
public sealed class SpeedCalibrationResult
{
    public bool IsPredicted { get; set; }
    public string PredictionBasis { get; set; } = "";
    public RobotControls Control { get; set; }
    public ServoSpeed Setting { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Port { get; set; } = "";
    public DateTime MeasuredUtc { get; set; }
    public int FromPwm { get; set; }
    public int ToPwm { get; set; }
    public double Seconds { get; set; }
    public bool ResolutionLimited { get; set; }
    public double? PhysicalSeconds { get; set; }
    public List<RampSample> Samples { get; set; } = new();
    [JsonIgnore] public double Rate => Seconds > 0 ? Math.Abs(ToPwm - FromPwm) / Seconds : 0;
    [JsonIgnore] public string Direction => ToPwm > FromPwm ? "Increasing PWM" : "Decreasing PWM";
    [JsonIgnore] public string Evidence => PhysicalSeconds > 0 ? "Physically timed (user)" : IsPredicted ? (string.IsNullOrEmpty(PredictionBasis) ? "Predicted from configuration" : PredictionBasis) : ResolutionLimited ? "Below polling resolution" : "Controller measured";
    [JsonIgnore] public string SecondsDisplay => IsPredicted && Seconds == 0 ? "Immediate" : Seconds.ToString("0.000");
    [JsonIgnore] public string RateDisplay => Seconds == 0 ? (IsPredicted ? "Unlimited" : "Unresolved") : Rate.ToString("0.0");
}

public sealed class ServoPhysicalTiming
{
    public RobotControls Control { get; set; }
    public double? CeilingUsPerSecond { get; set; }
    public double AssumedTravelDegrees { get; set; } = 180;
    public string Notes { get; set; } = "";
}

public sealed class StepperTravelTiming
{
    public RobotControls Control { get; set; }
    public double ExtendSeconds { get; set; } = 1.1;
    public double RetractSeconds { get; set; } = 1.1;
}

public sealed class SpeedCalibrationData
{
    public static double DefaultSecondsPer60Degrees(RobotControls control) => control switch
    {
        RobotControls.NoseBody or RobotControls.NoseBasket => .10,
        RobotControls.LeftIris or RobotControls.RightIris or RobotControls.LeftEyeVent or RobotControls.RightEyeVent => .11,
        _ => .15
    };
    public static string DefaultSpeedDescription(RobotControls control) =>
        DefaultSecondsPer60Degrees(control).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s / 60°";
    public int SchemaVersion { get; set; } = 1;
    public bool UseInUrdf { get; set; } = true;
    public bool MiniMaestro { get; set; } = true;
    public double PeriodMs { get; set; } = 20;
    public List<ServoPhysicalTiming> Physical { get; set; } = new();
    public List<SpeedCalibrationResult> Results { get; set; } = new();
    public List<StepperTravelTiming> Steppers { get; set; } = new()
    {
        new() { Control = RobotControls.LeftEyePop }, new() { Control = RobotControls.RightEyePop }
    };
    public double StepperSeconds(RobotControls control, bool extending)
    {
        var timing = Steppers.FirstOrDefault(s => s.Control == control);
        return timing == null ? 1.1 : extending ? timing.ExtendSeconds : timing.RetractSeconds;
    }
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static SpeedCalibrationData Load(string root)
    {
        string path = Path.Combine(root, "SpeedCalibration.json");
        var data = File.Exists(path) ? JsonSerializer.Deserialize<SpeedCalibrationData>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty speed calibration file.") : new();
        data.Validate(); return data;
    }
    public void Validate()
    {
        if (Steppers == null || Steppers.Any(s => s == null || s.Control is not (RobotControls.LeftEyePop or RobotControls.RightEyePop)
            || !double.IsFinite(s.ExtendSeconds) || s.ExtendSeconds <= 0 || !double.IsFinite(s.RetractSeconds) || s.RetractSeconds <= 0)
            || Steppers.Select(s => s.Control).Distinct().Count() != Steppers.Count)
            throw new InvalidDataException("Eye Pop extend and retract times must be positive, finite seconds with one row per channel.");
        foreach (var control in new[] { RobotControls.LeftEyePop, RobotControls.RightEyePop })
            if (!Steppers.Any(s => s.Control == control)) Steppers.Add(new() { Control = control });
        if (SchemaVersion != 1 || !double.IsFinite(PeriodMs) || PeriodMs < 3 || PeriodMs > 100 || Results == null || Physical == null)
            throw new InvalidDataException("Unsupported or invalid speed calibration settings.");
        if (Physical.Any(p => p == null || (int)p.Control is < 0 or > 23 || !double.IsFinite(p.AssumedTravelDegrees) || p.AssumedTravelDegrees <= 0 || p.AssumedTravelDegrees > 3600 || p.CeilingUsPerSecond.HasValue && (!double.IsFinite(p.CeilingUsPerSecond.Value) || p.CeilingUsPerSecond <= 0))
            || Results.Any(r => r == null || (int)r.Control is < 0 or > 23 || (int)r.Setting is < 0 or > 3
                || r.FromPwm is < 500 or > 2400 || r.ToPwm is < 500 or > 2400 || r.FromPwm == r.ToPwm
                || !double.IsFinite(r.Seconds) || r.Seconds < 0 || r.Samples == null || r.Samples.Any(s => s == null || !double.IsFinite(s.Seconds) || s.Seconds < 0 || !double.IsFinite(s.Pulse))
                || r.PhysicalSeconds.HasValue && (!double.IsFinite(r.PhysicalSeconds.Value) || r.PhysicalSeconds <= 0)))
            throw new InvalidDataException("Calibration times and rates must be finite positive numbers.");
    }
    public void Save(string root)
    {
        Validate(); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "SpeedCalibration.json"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public string Fingerprint(ServoConfigEntry entry) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { entry.Control, entry.MaestroPort, entry.MinPwm, entry.MaxPwm, entry.DefaultPwm, entry.Reversed, entry.Speeds, entry.Accels, MiniMaestro, PeriodMs }))));
    public MotionLimits ConfiguredLimits(ServoConfigEntry entry, ServoSpeed setting)
    {
        int i = setting == ServoSpeed.NoChange ? 0 : (int)setting;
        // A malformed editable profile must not throw on the WPF render timer
        // or move backwards through a negative speed. Hold until corrected.
        if (i is < 0 or > 3 || entry.Speeds?.Length != 4 || entry.Accels?.Length != 4 ||
            entry.Speeds[i] is < 0 or > 16383 || entry.Accels[i] is < 0 or > 255) return new(0, double.PositiveInfinity);
        double tick = !MiniMaestro ? .01 : PeriodMs < 20 ? PeriodMs / 1000 : PeriodMs / 2000;
        return new(entry.Speeds[i] == 0 ? double.PositiveInfinity : entry.Speeds[i] * .25 / tick,
            entry.Accels[i] == 0 ? double.PositiveInfinity : entry.Accels[i] * .25 / (8 * tick * tick));
    }
    public MotionLimits Limits(ServoConfigEntry entry, ServoSpeed setting, bool increasing)
    {
        var limits = ConfiguredLimits(entry, setting);
        if (limits.Speed == 0) return limits;
        string stamp = Fingerprint(entry);
        var matching = Results.Where(r => r.Control == entry.Control && r.Setting == setting && r.Fingerprint == stamp && (r.ToPwm > r.FromPwm) == increasing).ToList();
        // Fit speed to repeatable traverse times with the configured acceleration.
        var measured = matching.Where(r => !r.IsPredicted && !r.ResolutionLimited && r.Seconds > .04).OrderByDescending(r => Math.Abs(r.ToPwm - r.FromPwm)).ThenByDescending(r => r.MeasuredUtc).Take(3)
            .Select(r => MotionLimits.FitSpeed(Math.Abs(r.ToPwm - r.FromPwm), r.Seconds, limits.Acceleration)).Order().ToArray();
        if (measured.Length > 0 && double.IsFinite(measured[measured.Length / 2])) limits = limits with { Speed = measured[measured.Length / 2] };
        return ApplyPhysicalLimits(entry, setting, limits, matching);
    }
    private MotionLimits ApplyPhysicalLimits(ServoConfigEntry entry, ServoSpeed setting, MotionLimits limits, List<SpeedCalibrationResult> matching)
    {
        var physical = Physical.FirstOrDefault(p => p.Control == entry.Control);
        double? ceiling = physical?.CeilingUsPerSecond;
        var timed = matching.Where(r => r.PhysicalSeconds > 0).OrderByDescending(r => r.MeasuredUtc).FirstOrDefault();
        var configured = ConfiguredLimits(entry, setting);
        // 0/0 means unlimited Maestro output, but the shaft still takes time.
        // The angle is a servo-horn assumption, independent of URDF linkage extents.
        if (double.IsPositiveInfinity(configured.Speed) && double.IsPositiveInfinity(configured.Acceleration) && !(ceiling > 0) && timed == null)
        {
            double degrees = physical?.AssumedTravelDegrees ?? 180;
            ceiling = Math.Max(0, entry.MaxPwm - entry.MinPwm) / (degrees * DefaultSecondsPer60Degrees(entry.Control) / 60);
        }
        if (ceiling > 0) limits = limits with { Speed = Math.Min(limits.Speed, ceiling.Value) };
        if (timed != null) limits = limits with { Speed = Math.Min(limits.Speed, MotionLimits.FitSpeed(Math.Abs(timed.ToPwm - timed.FromPwm), timed.PhysicalSeconds.Value, limits.Acceleration)) };
        return limits;
    }

    /// <summary>Refresh rest-to-rest, full-range predictions without touching
    /// hardware. Measurements and user-entered physical evidence are retained.</summary>
    public bool RegeneratePredictions(ServoConfiguration config)
    {
        var predicted = new List<SpeedCalibrationResult>();
        foreach (var entry in config.Servos.Where(e => (int)e.Control is >= 0 and <= 23 && e.MaxPwm > e.MinPwm))
        {
            string stamp = Fingerprint(entry);
            foreach (var setting in new[] { ServoSpeed.Default, ServoSpeed.Slow, ServoSpeed.Fast, ServoSpeed.Crawl })
            {
                var limits = ConfiguredLimits(entry, setting);
                if (limits.Speed <= 0) continue;
                foreach (bool increasing in new[] { true, false })
                {
                    var predictedLimits = limits;
                    string basis = "";
                    if (double.IsPositiveInfinity(limits.Speed) && double.IsPositiveInfinity(limits.Acceleration))
                    {
                        var matching = Results.Where(r => r.Control == entry.Control && r.Setting == setting && r.Fingerprint == stamp && (r.ToPwm > r.FromPwm) == increasing).ToList();
                        predictedLimits = ApplyPhysicalLimits(entry, setting, limits, matching);
                        basis = Physical.Any(p => p.Control == entry.Control && p.CeilingUsPerSecond > 0) || matching.Any(r => r.PhysicalSeconds > 0)
                            ? "Predicted from physical estimate" : "Estimated " + DefaultSpeedDescription(entry.Control);
                    }
                    double span = entry.MaxPwm - entry.MinPwm, duration = predictedLimits.TraverseSeconds(span);
                    int from = increasing ? entry.MinPwm : entry.MaxPwm, to = increasing ? entry.MaxPwm : entry.MinPwm;
                    var existing = Results.FirstOrDefault(r => r.IsPredicted && r.Control == entry.Control && r.Setting == setting && r.Fingerprint == stamp && r.FromPwm == from && r.ToPwm == to && r.Seconds == duration && r.PredictionBasis == basis);
                    if (existing != null) { predicted.Add(existing); continue; }
                    var samples = new List<RampSample>();
                    if (duration == 0) { samples.Add(new(0, from)); samples.Add(new(0, to)); }
                    else for (int i = 0; i <= 32; i++)
                    {
                        double t = duration * i / 32;
                        double distance;
                        if (double.IsPositiveInfinity(limits.Acceleration)) distance = span * i / 32;
                        else
                        {
                            double ramp = Math.Min(limits.Speed / limits.Acceleration, Math.Sqrt(span / limits.Acceleration));
                            distance = t < ramp ? .5 * limits.Acceleration * t * t : t > duration - ramp
                                ? span - .5 * limits.Acceleration * (duration - t) * (duration - t)
                                : .5 * limits.Acceleration * ramp * ramp + limits.Acceleration * ramp * (t - ramp);
                        }
                        samples.Add(new(t, from + (increasing ? distance : -distance)));
                    }
                    predicted.Add(new() { IsPredicted = true, PredictionBasis = basis, Control = entry.Control, Setting = setting, Fingerprint = stamp,
                        MeasuredUtc = DateTime.UtcNow, FromPwm = from, ToPwm = to, Seconds = duration, Samples = samples });
                }
            }
        }
        var updated = predicted.Concat(Results.Where(r => !r.IsPredicted || r.PhysicalSeconds.HasValue && !predicted.Contains(r))).ToList();
        bool changed = !Results.SequenceEqual(updated);
        Results = updated;
        return changed;
    }
}

public readonly record struct MotionLimits(double Speed, double Acceleration)
{
    public double TraverseSeconds(double distance)
    {
        if (distance <= 0) return 0;
        if (double.IsPositiveInfinity(Acceleration)) return distance / Speed;
        if (double.IsPositiveInfinity(Speed) || distance <= Speed * Speed / Acceleration) return 2 * Math.Sqrt(distance / Acceleration);
        return distance / Speed + Speed / Acceleration;
    }
    public static double FitSpeed(double span, double seconds, double acceleration)
    {
        if (seconds <= 0 || span <= 0) return double.PositiveInfinity;
        if (double.IsPositiveInfinity(acceleration)) return span / seconds;
        if (seconds <= 2 * Math.Sqrt(span / acceleration)) return double.PositiveInfinity;
        // Smaller root of t = d/v + v/a, expressed without subtractive cancellation.
        return 2 * span / (seconds + Math.Sqrt(seconds * seconds - 4 * span / acceleration));
    }
}

public sealed class ServoMotionState
{
    public double Position { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; set; }
    public ServoMotionState(double position) { Position = Target = position; }
    public void Reset(double position) { Position = Target = position; Velocity = 0; }
    public void Advance(double seconds, MotionLimits limits, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        // Small physical steps keep acceleration consistent across display frame rates.
        for (double remaining = seconds; remaining > 1e-9;)
        {
            double dt = Math.Min(.001, remaining); remaining -= dt;
            double delta = Target - Position;
            if (Math.Abs(delta) < 1e-7) { Position = Target; Velocity = 0; break; }
            if (double.IsPositiveInfinity(limits.Acceleration))
            { double step = Math.Min(Math.Abs(delta), limits.Speed * dt); Position += Math.Sign(delta) * step; Velocity = step < Math.Abs(delta) ? Math.Sign(delta) * limits.Speed : 0; continue; }
            double desired = Math.Sign(delta) * Math.Min(limits.Speed, Math.Sqrt(2 * limits.Acceleration * Math.Abs(delta)));
            double velocity = Velocity + Math.Clamp(desired - Velocity, -limits.Acceleration * dt, limits.Acceleration * dt);
            double stepDistance = (Velocity + velocity) * .5 * dt;
            if (Math.Sign(stepDistance) == Math.Sign(delta) && Math.Abs(stepDistance) >= Math.Abs(delta)) { Position = Target; Velocity = 0; }
            else
            {
                Position += stepDistance; Velocity = velocity;
                if (Position < min || Position > max) { Position = Math.Clamp(Position, min, max); Velocity = 0; }
            }
        }
    }
}
