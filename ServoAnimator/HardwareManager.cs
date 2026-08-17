// ---------------------------------------------------------------------------
// HardwareManager.cs
//
// Owns the physical devices and the routing from timeline/grid values to
// them. Nothing is touched until Live Drive is turned ON - Connect() runs
// then (and is retried on each later toggle if devices were missing).
// Missing devices are reported as a list of messages for diagnostics and are
// shown in MainWindow by red/green status indicators rather than a popup.
// Whatever WAS found is driven; missing pieces are silently skipped afterwards,
// so a partial rig still works.
//
// Value routing (all guarded - a serial hiccup never crashes the UI):
//   * ganged ServoNames  -> every MaestroServo in the gang, mapped through
//     MaestroServo.MapDelta (centered for -100..100 servos, 0..100 spans)
//     after configuring the command's ServoSpeed only when it is explicit;
//     N/C commands preserve the speed/accel profile already active
//   * individual control -> that one MaestroServo (same mapping), or raw
//     PWM for the verify sliders
//   * LeftEyePop / RightEyePop -> the Tic steppers (0..2000 clamped into
//     the Tic's 0..2100 range)
//   * RGBCommand         -> the Arduino (the command TEXT is sent verbatim,
//     matching the RGBLight.cs string.Format outputs)
// ---------------------------------------------------------------------------

using System.Diagnostics;

namespace ServoAnimator
{
    public class HardwareManager
    {
        /// <summary>True when at least one supported hardware device was found.
        /// Existing drive paths use this to allow a partially connected rig.</summary>
        public bool Connected { get; private set; }

        /// <summary>Per-device status used by the Live Drive indicators.</summary>
        public bool MaestroConnected => _maestroPort != null;
        public bool ArduinoConnected => _lights != null;
        public bool LeftTicConnected => _leftTic != null;
        public bool RightTicConnected => _rightTic != null;
        public bool AllConnected => MaestroConnected && ArduinoConnected &&
                                    LeftTicConnected && RightTicConnected;

        private ServoConfiguration _config;
        private string _maestroPort;
        private RGBLight _lights;
        private TicController _leftTic, _rightTic;
        private readonly Dictionary<RobotControls, MaestroServo> _servos = new();

        // Which of the four configured speed/accel profiles is currently
        // active on each Maestro channel.  Position-only (N/C) commands do
        // not alter this state.  Reconfigure() re-sends the active profile
        // so saved speed/acceleration edits take effect immediately.
        private readonly Dictionary<RobotControls, ServoSpeed> _activeSpeedByControl = new();

        /// <summary>
        /// Find the devices and build the servo objects from the current
        /// configuration. Returns a list of problems (empty = everything
        /// found) for diagnostics; MainWindow represents device availability
        /// with status indicators. Safe to call repeatedly - each call rescans.
        /// </summary>
        public List<string> Connect(ServoConfiguration config, string configFolder)
        {
            _config = config;
            var problems = new List<string>();
            _servos.Clear();
            _activeSpeedByControl.Clear();
            _maestroPort = null;
            _lights = null;
            _leftTic = _rightTic = null;
            Connected = false;

            FoundDevices found;
            try
            {
                // The TIC\ folder (ticcmd + settings) lives in the CONFIG
                // folder, not the exe folder.
                found = UsbDeviceFinder.Find(config.LeftTicSerialNumber,
                                             configFolder);
            }
            catch (Exception ex)
            {
                problems.Add("USB scan failed: " + ex.Message);
                return problems;
            }

            _maestroPort = found.MaestroPort;
            _lights = found.Lights;
            _leftTic = found.LeftTic;
            _rightTic = found.RightTic;

            if (_maestroPort == null)
                problems.Add("Pololu Maestro servo card not found (no matching COM port).");
            if (_lights == null)
                problems.Add("Arduino Nano (CH340) RGB controller not found.");
            if (_leftTic == null)
                problems.Add("Left Tic T249 eye-pop controller not found.");
            if (_rightTic == null)
                problems.Add("Right Tic T249 eye-pop controller not found.");

            // Build a MaestroServo per configured PWM channel (eye pops and
            // RGB ids have no ServoConfigEntry and are skipped naturally).
            if (_maestroPort != null)
            {
                foreach (var entry in config.Servos)
                {
                    try
                    {
                        var s = new MaestroServo(entry, _maestroPort);
                        s.ConfigureSpeed(ServoSpeed.Default);
                        _servos[entry.Control] = s;
                        _activeSpeedByControl[entry.Control] = ServoSpeed.Default;
                    }
                    catch (Exception ex)
                    {
                        problems.Add($"Servo {entry.Control}: {ex.Message}");
                    }
                }
            }

            Connected = _maestroPort != null || _lights != null ||
                        _leftTic != null || _rightTic != null;
            return problems;
        }

        /// <summary>The configuration changed (Servo Configuration window
        /// saved/loaded): rebuild the servo objects from the new ranges and
        /// re-apply each channel's currently active speed/accel profile.
        /// Gang-relative directions take effect
        /// immediately since they're looked up at drive time.</summary>
        public void Reconfigure(ServoConfiguration config)
        {
            _config = config;
            if (_maestroPort == null) return;

            // Keep the profile that is currently active on each physical
            // channel.  Rebuilding the MaestroServo picks up PWM ranges,
            // direction, and the newly saved speed/accel arrays; re-sending
            // the active profile applies speed/acceleration edits immediately.
            var active = new Dictionary<RobotControls, ServoSpeed>(_activeSpeedByControl);
            _servos.Clear();
            foreach (var entry in config.Servos)
            {
                try
                {
                    var s = new MaestroServo(entry, _maestroPort);
                    ServoSpeed pick = active.TryGetValue(entry.Control, out var previous) &&
                                      previous != ServoSpeed.NoChange
                        ? previous : ServoSpeed.Default;
                    s.ConfigureSpeed(pick);
                    _servos[entry.Control] = s;
                    _activeSpeedByControl[entry.Control] = pick;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[hw reconfigure {entry.Control}] {ex.Message}");
                }
            }
        }

        // -------------------- drive routing --------------------

        /// <summary>Ganged command: drive every physical servo the
        /// ServoName gangs together.</summary>
        public void DriveGang(ServoNames servo, ServoSpeed speed, int value)
        {
            if (servo == ServoNames.LeftEyePop) { DriveEyePop(_leftTic, value); return; }
            if (servo == ServoNames.RightEyePop) { DriveEyePop(_rightTic, value); return; }
            if (servo == ServoNames.BothEyePop)
            {
                DriveEyePop(_leftTic, value);
                DriveEyePop(_rightTic, value);
                return;
            }

            bool centered = ServoCommand.RangeFor(servo).Min < 0;
            foreach (var control in ServoConfiguration.ControlsFor(servo))
                DriveControlValue(servo, control, speed, value, centered);
        }

        /// <summary>Individual-control command (same -100..100 / 0..100
        /// scale as its gang). The parent ServoName supplies the
        /// GANG-RELATIVE direction for this control.</summary>
        public void DriveControlValue(ServoNames gang, RobotControls control,
                                      ServoSpeed speed, int value, bool centered)
        {
            // Eye-pop controls route to the Tics, not the Maestro.
            if (control == RobotControls.LeftEyePop) { DriveEyePop(_leftTic, value); return; }
            if (control == RobotControls.RightEyePop) { DriveEyePop(_rightTic, value); return; }

            if (!_servos.TryGetValue(control, out var s)) return;
            bool gangReversed = _config?.GangReversed(gang, control) ?? false;
            Guard(() =>
            {
                if (speed != ServoSpeed.NoChange)
                {
                    s.ConfigureSpeed(speed);
                    _activeSpeedByControl[control] = speed;
                }
                s.GoValue(s.MapDelta(value, gangReversed, centered));
            }, control.ToString());
        }

        /// <summary>Push the speed/accel pair (indexed by the ServoSpeed
        /// enum) to every Maestro servo in a gang - the grid's Speed
        /// picklist in Live Drive. Compact protocol 0x87 (speed) + 0x89
        /// (accel) per channel.</summary>
        public void ConfigureGangSpeed(ServoNames servo, ServoSpeed speed)
        {
            if (speed == ServoSpeed.NoChange) return;
            foreach (var control in ServoConfiguration.ControlsFor(servo))
                ConfigureControlSpeed(control, speed);
        }

        /// <summary>Push one explicit speed/accel profile to a single Maestro
        /// child servo.  Used by individual-control commands in Edit Commands.</summary>
        public void ConfigureControlSpeed(RobotControls control, ServoSpeed speed)
        {
            if (speed == ServoSpeed.NoChange) return;
            if (_servos.TryGetValue(control, out var s))
                Guard(() =>
                {
                    s.ConfigureSpeed(speed);
                    _activeSpeedByControl[control] = speed;
                }, control.ToString());
        }

        /// <summary>Disable command targeting a whole gang: turn OFF every
        /// Maestro servo the ServoName drives (the Tic eye pops have no
        /// disable path and are skipped).</summary>
        public void DisableGang(ServoNames servo)
        {
            foreach (var control in ServoConfiguration.ControlsFor(servo))
                DisableControl(control);
        }

        /// <summary>Disable command targeting one child servo.</summary>
        public void DisableControl(RobotControls control)
        {
            if (_servos.TryGetValue(control, out var s))
                Guard(s.DisableServo, control.ToString());
        }

        /// <summary>Disable PWM on EVERY Maestro servo channel (the
        /// "Disable All" button): 0xAA 0x0C 0x0F channel per servo -
        /// the servos go limp until next driven.</summary>
        public void DisableAll()
        {
            foreach (var s in _servos.Values)
                Guard(s.DisableServo, s.Name.ToString());
        }

        /// <summary>Return the physical robot to its configured home state.
        /// Maestro channels go directly to each Servo Configuration Default PWM
        /// without changing their active speed/accel profile; both Tic eye pops
        /// go to zero; and the Arduino receives ClearAll.</summary>
        public void ResetAll()
        {
            foreach (var s in _servos.Values)
                Guard(s.GoHome, s.Name.ToString());

            DriveEyePop(_leftTic, 0);
            DriveEyePop(_rightTic, 0);
            DriveRgb("ClearAll");
        }

        /// <summary>Raw PWM (the verify sliders).</summary>
        public void DriveControlPwm(RobotControls control, int pwm)
        {
            if (!_servos.TryGetValue(control, out var s)) return;
            Guard(() => s.GoValue(pwm), control.ToString());
        }

        /// <summary>RGB command text, sent verbatim to the Arduino.</summary>
        public void DriveRgb(string commandText)
        {
            if (_lights == null || string.IsNullOrWhiteSpace(commandText)) return;
            Guard(() => _lights.Command(commandText), "RGB");
        }

        private static void DriveEyePop(TicController tic, int value)
        {
            if (tic == null) return;
            try { tic.MoveToPosition(value); }
            catch (Exception ex) { Debug.WriteLine("[hw eyepop] " + ex.Message); }
        }

        private static void Guard(Action act, string what)
        {
            try { act(); }
            catch (Exception ex) { Debug.WriteLine($"[hw {what}] {ex.Message}"); }
        }
    }
}
