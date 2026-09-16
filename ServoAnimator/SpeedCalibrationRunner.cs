using System.Diagnostics;
using System.IO;
using System.IO.Ports;

namespace ServoAnimator;

internal interface IMaestroCalibrationPort
{
    void Configure(int channel, int speed, int acceleration);
    void Target(int channel, int quarterMicroseconds);
    int Position(int channel);
}

internal sealed class MaestroCalibrationPort(SerialPort port) : IMaestroCalibrationPort
{
    internal static byte[] Command(byte command, int channel, int value) => new[] { command, (byte)channel, (byte)(value & 127), (byte)((value >> 7) & 127) };
    public void Configure(int channel, int speed, int acceleration)
    { Write(Command(0x87, channel, speed)); Write(Command(0x89, channel, acceleration)); }
    public void Target(int channel, int quarterMicroseconds) => Write(Command(0x84, channel, quarterMicroseconds));
    private void Write(byte[] bytes) => port.Write(bytes, 0, bytes.Length);
    public int Position(int channel)
    {
        Write(new byte[] { 0x90, (byte)channel });
        // ReadByte honors the serial timeout and accepts all eight response bits.
        int lo = port.ReadByte(), hi = port.ReadByte();
        if (lo < 0 || hi < 0) throw new IOException("Incomplete Maestro position response.");
        return lo | hi << 8;
    }
}

public sealed record SpeedCalibrationPlan(int FromPwm, int ToPwm, int Repeats, bool IncludeShortMove,
    IReadOnlyList<ServoSpeed> Settings, bool DisableOnAbort, bool PreparedAtStart, int SettleMilliseconds = 1000);
public sealed record SpeedCalibrationProgress(string Stage, double Pulse, SpeedCalibrationResult Completed = null);

internal static class SpeedCalibrationRunner
{
    internal static void Validate(ServoConfigEntry entry, SpeedCalibrationPlan plan)
    {
        if ((int)entry.Control is < 0 or > 23 || entry.MaestroPort is < 0 or > 23) throw new InvalidOperationException("Select a Maestro servo.");
        if (entry.Speeds?.Length != 4 || entry.Accels?.Length != 4 || entry.Speeds.Any(s => s is < 0 or > 16383) || entry.Accels.Any(a => a is < 0 or > 255))
            throw new InvalidOperationException("Maestro profiles require four speeds (0–16383) and four accelerations (0–255).");
        if (plan.FromPwm < entry.MinPwm || plan.ToPwm > entry.MaxPwm || plan.ToPwm - plan.FromPwm < 10)
            throw new InvalidOperationException("The test range must be at least 10 µs and remain inside the servo limits.");
        if (!plan.PreparedAtStart) throw new InvalidOperationException("Confirm the mechanism is positioned at the test start with servo pulses enabled.");
        if (plan.Repeats is < 1 or > 5 || plan.SettleMilliseconds is < 200 or > 10000 || plan.Settings.Count == 0 || plan.Settings.Any(s => (int)s is < 0 or > 3))
            throw new InvalidOperationException("Choose at least one speed and one to five repeats.");
    }
    internal static List<SpeedCalibrationResult> Run(IMaestroCalibrationPort port, ServoConfigEntry entry, SpeedCalibrationData data,
        SpeedCalibrationPlan plan, string device, ServoSpeed restore, IProgress<SpeedCalibrationProgress> progress, CancellationToken cancellation,
        Func<double> time = null, Action<int> sleep = null)
    {
        Validate(entry, plan);
        if (!data.MiniMaestro && entry.MaestroPort > 5) throw new InvalidOperationException("Micro Maestro channels are 0–5. Check the selected controller type and servo port.");
        var clock = Stopwatch.StartNew(); time ??= () => clock.Elapsed.TotalSeconds;
        sleep ??= ms => { if (cancellation.WaitHandle.WaitOne(ms)) cancellation.ThrowIfCancellationRequested(); };
        var results = new List<SpeedCalibrationResult>();
        int channel = entry.MaestroPort;
        bool success = false, engaged = false;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            int initial = port.Position(channel);
            if (initial == 0 || Math.Abs(initial / 4d - plan.FromPwm) > 2)
                throw new InvalidOperationException("Position the mechanism at the test start using Servo Configuration Verify before starting. Calibration will not initialize an unpowered or displaced servo.");
            engaged = true;
            foreach (var setting in plan.Settings)
            {
                int index = (int)setting;
                port.Configure(channel, entry.Speeds[index], entry.Accels[index]);
                for (int repeat = 0; repeat < plan.Repeats; repeat++)
                foreach (int end in plan.IncludeShortMove ? new[] { plan.ToPwm, plan.FromPwm + (plan.ToPwm - plan.FromPwm) / 4 } : new[] { plan.ToPwm })
                foreach (bool forward in new[] { true, false })
                {
                    int from = forward ? plan.FromPwm : end, to = forward ? end : plan.FromPwm;
                    cancellation.ThrowIfCancellationRequested();
                    sleep(plan.SettleMilliseconds);
                    double started = time();
                    var samples = new List<RampSample> { new(0, from) };
                    port.Target(channel, to * 4);
                    double expected = data.ConfiguredLimits(entry, setting).TraverseSeconds(Math.Abs(to - from));
                    double timeout = Math.Max(10, 10 + expected * 2);
                    while (true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        int raw = port.Position(channel);
                        double elapsed = time() - started, pulse = raw / 4d;
                        if (raw == 0 || pulse < entry.MinPwm - 1 || pulse > entry.MaxPwm + 1) throw new IOException("Unexpected Maestro position; calibration aborted.");
                        samples.Add(new(elapsed, pulse));
                        progress?.Report(new($"{entry.Control} · {setting} · {from} → {to} µs · repeat {repeat + 1}", pulse));
                        if (raw == to * 4) break;
                        if (elapsed > timeout) throw new TimeoutException("The Maestro did not reach the test target in time.");
                        sleep(10);
                    }
                    var result = new SpeedCalibrationResult { Control = entry.Control, Setting = setting, Fingerprint = data.Fingerprint(entry),
                        Port = device, MeasuredUtc = DateTime.UtcNow, FromPwm = from, ToPwm = to, Seconds = samples[^1].Seconds,
                        ResolutionLimited = samples.Count < 4 || samples[^1].Seconds < .03, Samples = samples };
                    results.Add(result); progress?.Report(new("Completed traverse", to, result));
                }
            }
            success = true; return results;
        }
        finally
        {
            if (engaged)
            {
            // A run ends back at its reviewed start, never at an unreviewed home position.
            // Abort releases torque only when explicitly selected; hold requires a working reply.
            try
            {
                if (!success)
                {
                    if (plan.DisableOnAbort) port.Target(channel, 0);
                    else
                    {
                        int current = port.Position(channel);
                        if (current > 0)
                        {
                            port.Configure(channel, 0, 0); port.Target(channel, current);
                            // Let the hold target take effect before restoring a limited
                            // profile. Cancellation must not interrupt this stop step.
                            Thread.Sleep(Math.Max(20, (int)Math.Ceiling(data.PeriodMs * 2)));
                        }
                    }
                }
            }
            finally
            {
                int previous = restore == ServoSpeed.NoChange ? 0 : (int)restore;
                port.Configure(channel, entry.Speeds[previous], entry.Accels[previous]);
            }
            }
        }
    }
}
