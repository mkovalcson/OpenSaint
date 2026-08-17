// ---------------------------------------------------------------------------
// Hardware.cs
//
// The physical output layer, adapted from the uploaded Servos.cs,
// StepperMotors.cs, RGBLight.cs and USBDevices.cs into this project's
// namespace (their file-level RobotControls / ServoSpeed enums collide with
// ours, so ours were aligned to theirs instead - including the Maestro
// channel numbers).
//
//   * MaestroServo - one PWM channel on the Pololu Maestro. The compact-
//     protocol serial commands (Set Target 0x84, Speed 0x87, Accel 0x89,
//     Disable) and the -100..100 / 0..100 -> microseconds mapping
//     (MapDelta) are ported verbatim from Servos.cs.
//   * TicController - the Tic T249 eye-pop steppers, driven via ticcmd.
//   * RGBLight - the Arduino RGB rings; each command opens the port,
//     writes one line, closes (matching RGBLight.cs).
//   * UsbDeviceFinder - WMI scan (Win32_SerialPort / Win32_PnPEntity) that
//     identifies the Maestro, the two Tics and the Arduino, ported from
//     USBDevices.cs but returning PARTIAL results so the caller can report
//     exactly which devices are missing.
// ---------------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace ServoAnimator
{
    // ==================== Maestro PWM servo ====================

    public class MaestroServo
    {
        private const int BaudRate = 115200;

        public RobotControls Name { get; }
        public string UsbPort { get; }
        public int Channel { get; }
        public int HomeValue { get; }
        public int LimitLower { get; }
        public int LimitUpper { get; }
        public bool Reverse { get; }
        public int[] Speed { get; }
        public int[] Accel { get; }
        public int CurrentPosition { get; private set; }

        public MaestroServo(ServoConfigEntry cfg, string usbPort)
        {
            Name = cfg.Control;
            UsbPort = usbPort;
            Channel = (int)cfg.Control;      // enum value IS the channel
            HomeValue = cfg.DefaultPwm;
            LimitLower = cfg.MinPwm;
            LimitUpper = cfg.MaxPwm;
            Reverse = cfg.Reversed;
            Speed = cfg.Speeds;
            Accel = cfg.Accels;
            CurrentPosition = HomeValue;
        }

        /// <summary>
        /// Faithful port of Servo.MapDeltatoServo. TWO direction layers:
        ///   * this servo's own hardware Reverse (from the configuration
        ///     entry) - which side of Home a value lands on / whether the
        ///     0..100 span runs Max->Min;
        ///   * isGangReversed - this servo's direction RELATIVE TO the
        ///     ganged ServoName driving it (negates the input for centered
        ///     ranges), so the neck pair can move the same way under
        ///     NeckNodUp and opposite ways under NeckTiltRight.
        /// isCentered: true = -100..100 (pivots on Home), false = 0..100.
        /// </summary>
        public int MapDelta(int deltaValue, bool isGangReversed, bool isCentered)
        {
            double outMin = LimitLower;
            double outMax = LimitUpper;
            double outHome = HomeValue;
            double value = deltaValue;
            double adjustedValue;

            if (isCentered)  // -100 to 100
            {
                // A centered servo has two independent direction layers:
                // the gang-relative reversal and the physical servo's own
                // Reverse flag.  Each one reverses the logical input; when
                // both are reversed they cancel, which is the intended XOR
                // behavior.  The previous Reverse branch used the same
                // equations as Normal, so reversing a centered single servo
                // (for example NoseBody) had no effect from the Servo Grid.
                if (isGangReversed) value = -value;
                if (Reverse) value = -value;

                adjustedValue = value < 0
                    ? outHome + value / 100 * (outHome - outMin)
                    : outHome + value / 100 * (outMax - outHome);
            }
            else // not centered, only 0-100
            {
                adjustedValue = Reverse
                    ? outMax - value / 100 * (outMax - outMin)
                    : outHome + value / 100 * (outMax - outMin);
            }

            // Safety clamp into the configured limits (the reference relies
            // on Home == Min for its 0..100 servos; the clamp guards odd
            // configurations without changing normal results).
            return (int)Math.Round(Math.Clamp(adjustedValue, outMin, outMax));
        }

        public void GoHome() => GoValue(HomeValue);

        /// <summary>Set Target (compact protocol, quarter-microseconds).</summary>
        public void GoValue(int microseconds)
        {
            microseconds = Math.Clamp(microseconds, 64, 16383);
            CurrentPosition = microseconds;
            int target = microseconds * 4;
            Write(new byte[]
            {
                0x84, (byte)Channel,
                (byte)(target & 0x7F), (byte)((target >> 7) & 0x7F),
            });
        }

        /// <summary>Speed (0x87) + acceleration (0x89) for one ServoSpeed.</summary>
        public void ConfigureSpeed(ServoSpeed pick)
        {
            // N/C is a ServoCommand concept: preserve whatever speed/accel
            // profile is already active on the Maestro channel.
            if (pick == ServoSpeed.NoChange) return;

            int index = (int)pick;
            if (index < 0 || index >= Speed.Length || index >= Accel.Length)
                return;

            int s = Speed[index], a = Accel[index];
            Write(new byte[]
            {
                0x87, (byte)Channel, (byte)(s & 0x7F), (byte)(s >> 7 & 0x7F),
                0x89, (byte)Channel, (byte)(a & 0x7F), (byte)(a >> 7 & 0x7F),
            });
        }

        public void DisableServo() =>
            Write(new byte[] { 0xAA, 0x0C, 0x0F, (byte)Channel });

        private void Write(byte[] cmd)
        {
            using var port = new SerialPort(UsbPort, BaudRate, Parity.None, 8, StopBits.One);
            port.Open();
            port.Write(cmd, 0, cmd.Length);
        }
    }

    // ==================== Tic T249 eye-pop stepper ====================

    /// <summary>Eye-pop stepper driven through Pololu's ticcmd utility
    /// (ported from StepperMotors.cs; the settings-file load is applied on
    /// construction when TIC\tic_settings.txt exists). The TIC\ folder is
    /// looked up in the CONFIG folder (from Paths.json / first-run animatorConfig discovery), not the exe
    /// folder.</summary>
    public class TicController
    {
        public RobotControls Name { get; }
        public string SerialNumber { get; }
        public int MinValue { get; }
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        private readonly string _ticCmd;

        public TicController(string serialNumber, string configFolder,
                             RobotControls name, int minValue, int maxValue)
        {
            Name = name;
            SerialNumber = serialNumber;
            MinValue = minValue;
            MaxValue = maxValue;
            _ticCmd = Path.Combine(configFolder, "TIC", "ticcmd");

            string settings = Path.Combine(configFolder, "TIC", "tic_settings.txt");
            if (File.Exists(settings))
                Run($"--serial {SerialNumber} --settings \"{settings}\"");
        }

        public void MoveToPosition(int position)
        {
            CurrentValue = Math.Clamp(position, MinValue, MaxValue);
            Run($"--serial {SerialNumber} --exit-safe-start --position {CurrentValue}");
        }

        private void Run(string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = _ticCmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (!string.IsNullOrWhiteSpace(err))
                    Debug.WriteLine($"[tic {SerialNumber}] {err}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[tic {SerialNumber}] {ex.Message}");
            }
        }
    }

    // ==================== Arduino RGB rings ====================

    /// <summary>The Arduino RGB ring lights: one text command per line
    /// (open/write/close per command, matching RGBLight.cs). The command
    /// strings are what the RGB command builder produces - e.g.
    /// "SetRGBColor,255,0,64,200,Eyes,LR".</summary>
    public class RGBLight
    {
        private readonly string _portName;
        public RGBLight(string portName) => _portName = portName;

        public void Command(string command)
        {
            using var port = new SerialPort(_portName, 115200);
            port.Open();
            port.WriteLine(command);
        }
    }

    // ==================== USB device discovery ====================

    public class FoundDevices
    {
        public string MaestroPort;      // null when not found
        public RGBLight Lights;         // null when the Arduino is missing
        public TicController LeftTic;   // null when missing
        public TicController RightTic;  // null when missing
    }

    public static class UsbDeviceFinder
    {
        private class Entry
        {
            public string Name, DeviceId, SerialNumber, PortName;
        }

        /// <summary>
        /// WMI scan ported from USBDevices.WindowsFindUSBDevices: matches
        /// COM-port records to Pololu/CH340 PnP entities by serial number,
        /// classifies Maestro / Tic T249 / Arduino, and returns whatever
        /// was found (nulls mark the missing pieces - the caller reports
        /// them in the error popup).
        /// </summary>
        public static FoundDevices Find(string leftTicSerial, string configFolder)
        {
            var result = new FoundDevices();

            // COM ports (Win32_SerialPort) keyed by serial number.
            var comBySerial = new Dictionary<string, string>();
            using (var searcher = new ManagementObjectSearcher(
                       "SELECT * FROM Win32_SerialPort"))
                foreach (ManagementObject port in searcher.Get())
                {
                    string pnp = port["PNPDeviceID"]?.ToString() ?? "";
                    if (!pnp.Contains("_00")) continue;
                    var parts = pnp.Split('\\');
                    if (parts.Length < 3) continue;
                    comBySerial[parts[2]] = port["DeviceID"]?.ToString();
                }

            // Pololu / CH340 PnP entities.
            var entries = new List<Entry>();
            using (var searcher = new ManagementObjectSearcher(
                       "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%' " +
                       "AND (Name LIKE '%Pololu%' OR Name LIKE '%CH340%')"))
                foreach (ManagementObject dev in searcher.Get())
                {
                    string id = dev["DeviceID"]?.ToString() ?? "";
                    var parts = id.Split('\\');
                    entries.Add(new Entry
                    {
                        Name = dev["Name"]?.ToString() ?? "",
                        DeviceId = id,
                        SerialNumber = parts.Length >= 3 ? parts[2] : "",
                        PortName = ExtractComPort(dev["Name"]?.ToString() ?? ""),
                    });
                }

            foreach (var device in entries)
            {
                // Maestro serials differ between the PnP entity and the COM
                // record; normalize as the original code did.
                string sn = device.SerialNumber.Replace("_04", "_00")
                                               .Replace("0004", "0000");
                if (comBySerial.TryGetValue(sn, out string com))
                    device.PortName = com;

                if (device.Name.Contains("Maestro") && device.PortName != null)
                {
                    result.MaestroPort = device.PortName;
                }
                else if (device.Name.Contains("T249"))
                {
                    bool isLeft = string.IsNullOrEmpty(leftTicSerial)
                                  ? result.LeftTic == null   // first found = left
                                  : device.SerialNumber == leftTicSerial;
                    if (isLeft && result.LeftTic == null)
                        result.LeftTic = new TicController(device.SerialNumber,
                            configFolder, RobotControls.LeftEyePop, 0, 2100);
                    else if (result.RightTic == null)
                        result.RightTic = new TicController(device.SerialNumber,
                            configFolder, RobotControls.RightEyePop, 0, 2100);
                }
                else if (device.Name.Contains("CH340") && device.PortName != null)
                {
                    result.Lights = new RGBLight(device.PortName);
                }

                Debug.WriteLine($"[usb] {device.Name}  sn={device.SerialNumber}  " +
                                $"port={device.PortName}");
            }

            return result;
        }

        private static string ExtractComPort(string name)
        {
            var m = Regex.Match(name, @"\(COM\d+\)");
            return m.Success ? m.Value.Trim('(', ')') : null;
        }
    }
}
