// ---------------------------------------------------------------------------
// UrdfConfiguration.cs
//
// Persistent URDF range calibration for the preview.
// johnny5_head.urdf carries the model's embedded baseline calibration.
// ServoConfig.json supplies inherited physical direction/reversal and an optional
// URDFconfig.json in the Configuration folder remains a compatible external override.
// Normal logical gangs share synchronized visual Min/Max ranges.
// FlapsOpen is intentionally different: each of its four physical Open/Close
// flap servos retains independent Min/Max/Zero extents while upper/lower pairs
// remain logical preview gangs. Direction remains independently overridable per child.
// ---------------------------------------------------------------------------

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.Json.Serialization;

namespace ServoAnimator
{
    public sealed class UrdfConfiguration
    {
        public int Version { get; set; } = 8;

        /// <summary>Multiplier applied to the normalized audio amplitude before
        /// it drives the URDF mouth LEDs. 1.0 preserves the raw audio level;
        /// the calibration screen constrains this to 0.5x..2.0x.</summary>
        public double AudioLedGain { get; set; } = 1.0;

        public List<UrdfMotionSetting> Motions { get; set; } = new();

        [JsonIgnore]
        private Dictionary<(ServoNames Servo, RobotControls Control), UrdfMotionSetting> _byKey;

        public static UrdfConfiguration CreateDefault()
        {
            var c = new UrdfConfiguration();

            void AddGang(ServoNames servo, double min, double max, string unit)
            {
                foreach (var control in ServoConfiguration.ControlsFor(servo))
                {
                    // BothEyePop is an authoring alias; its two physical child
                    // controls already have independent Left/RightEyePop rows.
                    if (servo == ServoNames.BothEyePop) continue;
                    c.Motions.Add(new UrdfMotionSetting
                    {
                        Servo = servo,
                        Control = control,
                        MinExtent = min,
                        MaxExtent = max,
                        ZeroExtent = DefaultZeroExtent(servo, min, max),
                        Unit = unit,
                        ReverseOverride = null,
                    });
                }
            }

            AddGang(ServoNames.NeckTurn,                 -90,   90, "deg");
            AddGang(ServoNames.NeckNodUp,                -20,   20, "deg");
            AddGang(ServoNames.NeckTiltRight,            -15,   15, "deg");
            AddGang(ServoNames.FlapsOpen,                -90,   90, "deg");
            AddGang(ServoNames.FlapTiltUp,               -30,   90, "deg");
            AddGang(ServoNames.IrisClose,                 10,  100, "%");
            AddGang(ServoNames.EyesVerticalUp,           -20,   20, "deg");
            AddGang(ServoNames.EyesHorizontalRight,      -20,   20, "deg");
            AddGang(ServoNames.VentsOpen,                  0,   30, "deg");
            AddGang(ServoNames.NoseBody,                 -45,   45, "deg");
            AddGang(ServoNames.NoseBasket,               -45,   45, "deg");
            AddGang(ServoNames.MFR_UpDown,                 0,  100, "mm");
            AddGang(ServoNames.MFR_Rotate,               -90,   90, "deg");
            AddGang(ServoNames.Microphone_RaiseLower,      0, 21.6, "mm");
            AddGang(ServoNames.Whip_Antenna_RaiseLower,    0,  250, "mm");
            AddGang(ServoNames.Whip_Antenna_Rotate,      -90,   90, "deg");
            AddGang(ServoNames.LeftEyePop,                 0, 89.951, "mm");
            AddGang(ServoNames.RightEyePop,                0, 89.951, "mm");
            c.Reindex();
            ApplyEmbeddedUrdfCalibration(c);
            c.Normalize();
            c.Reindex();
            return c;
        }

        /// <summary>
        /// Apply ServoAnimator-specific baseline calibration embedded in the URDF.
        /// Standard URDF joint limits cannot represent the logical ZeroExtent or
        /// per-control direction override, so johnny5_head.urdf carries those values
        /// in a top-level servo_animator_calibration block.  An external
        /// URDFconfig.json, when present, is layered on top by Load().
        /// </summary>
        private static void ApplyEmbeddedUrdfCalibration(UrdfConfiguration config)
        {
            string path = ResolveUrdfPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            try
            {
                var doc = XDocument.Load(path);
                var calibration = doc.Root?.Element("servo_animator_calibration");
                if (calibration == null) return;

                foreach (var motion in calibration.Elements("motion"))
                {
                    if (!Enum.TryParse(motion.Attribute("servo")?.Value, out ServoNames servo) ||
                        !Enum.TryParse(motion.Attribute("control")?.Value, out RobotControls control))
                        continue;

                    var target = config.Motions.FirstOrDefault(m =>
                        m.Servo == servo && m.Control == control);
                    if (target == null) continue;

                    if (TryDouble(motion, "minExtent", out double min)) target.MinExtent = min;
                    if (TryDouble(motion, "maxExtent", out double max)) target.MaxExtent = max;
                    if (TryDouble(motion, "zeroExtent", out double zero)) target.ZeroExtent = zero;

                    string reverse = motion.Attribute("reverseOverride")?.Value?.Trim();
                    if (bool.TryParse(reverse, out bool reversed))
                        target.ReverseOverride = reversed;
                    else if (string.Equals(reverse, "inherit", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(reverse, "null", StringComparison.OrdinalIgnoreCase) ||
                             string.IsNullOrWhiteSpace(reverse))
                        target.ReverseOverride = null;
                }
            }
            catch
            {
                // A malformed or older URDF must not prevent the editor from starting;
                // fall back to the compiled defaults and allow URDFconfig.json to load.
            }
        }

        private static bool TryDouble(XElement element, string attribute, out double value) =>
            double.TryParse(element.Attribute(attribute)?.Value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out value);

        private static string ResolveUrdfPath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Models", "johnny5_head.urdf"),
                Path.Combine(AppContext.BaseDirectory, "johnny5_head.urdf"),
                Path.Combine(Environment.CurrentDirectory, "Models", "johnny5_head.urdf"),
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static double DefaultZeroExtent(ServoNames servo, double min, double max)
        {
            // Preserve the model's historical logical-zero pose when zero calibration
            // is first introduced for mappings that previously bypassed ZeroExtent.
            if (servo == ServoNames.IrisClose)
                return (min + max) / 2.0;
            if (IsPositiveInput(servo))
                return min;
            return Math.Clamp(0.0, min, max);
        }

        /// <summary>
        /// Reads URDFconfig.json as an optional override layered over the baseline
        /// calibration embedded in johnny5_head.urdf. Existing JSON files therefore
        /// remain valid and retain their previous precedence. v1 files were keyed only by ServoName;
        /// those values are migrated onto every child of that logical input.
        /// v2 files did not contain ReverseOverride, so they naturally retain
        /// Servo Configuration as the inherited direction.
        /// </summary>
        public static UrdfConfiguration Load(string path)
        {
            var defaults = CreateDefault();
            var saved = JsonSerializer.Deserialize<UrdfConfiguration>(
                File.ReadAllText(path), JsonOptions());
            if (saved?.Motions == null) return defaults;

            foreach (var incoming in saved.Motions)
            {
                if (incoming.Control != RobotControls.None)
                {
                    var target = defaults.Get(incoming.Servo, incoming.Control);
                    if (target == null) continue;
                    target.MinExtent = incoming.MinExtent;
                    target.MaxExtent = incoming.MaxExtent;
                    target.ZeroExtent = incoming.ZeroExtent;
                    target.ReverseOverride = incoming.ReverseOverride;
                }
                else
                {
                    // v1.1.6 migration: one logical range becomes the starting
                    // range for every physical child in that gang.
                    foreach (var target in defaults.Motions.Where(x => x.Servo == incoming.Servo))
                    {
                        target.MinExtent = incoming.MinExtent;
                        target.MaxExtent = incoming.MaxExtent;
                    }
                }
            }

            // Version 7 makes ZeroExtent active for every URDF servo. Prior files
            // already used it for centered controls, but positive-input controls
            // and Iris bypassed it. Initialize those zeroes to their historical
            // logical-zero pose so upgrading does not unexpectedly move the model.
            if (saved.Version < 7)
            {
                foreach (var target in defaults.Motions)
                {
                    if (target.Servo == ServoNames.IrisClose)
                        target.ZeroExtent = (target.MinExtent + target.MaxExtent) / 2.0;
                    else if (IsPositiveInput(target.Servo))
                        target.ZeroExtent = target.MinExtent;
                }
            }

            defaults.AudioLedGain = Math.Clamp(saved.AudioLedGain, 0.5, 2.0);
            defaults.Version = 8;
            defaults.Normalize();
            defaults.Reindex();
            return defaults;
        }

        public void Save(string path)
        {
            Version = 8;
            Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions()));
        }

        public void CopyFrom(UrdfConfiguration other)
        {
            if (other == null) return;
            AudioLedGain = Math.Clamp(other.AudioLedGain, 0.5, 2.0);
            foreach (var source in other.Motions)
            {
                var target = Get(source.Servo, source.Control);
                if (target == null) continue;
                target.MinExtent = source.MinExtent;
                target.MaxExtent = source.MaxExtent;
                target.ZeroExtent = source.ZeroExtent;
                target.ReverseOverride = source.ReverseOverride;
            }
            Version = 8;
            Normalize();
            Reindex();
        }

        public UrdfMotionSetting Get(ServoNames servo, RobotControls control)
        {
            _byKey ??= Motions.ToDictionary(x => (x.Servo, x.Control));
            return _byKey.TryGetValue((servo, control), out var s) ? s : null;
        }

        public IEnumerable<UrdfMotionSetting> SettingsFor(ServoNames servo) =>
            Motions.Where(x => x.Servo == servo);

        /// <summary>Set the physical URDF zero extents that make all flap panels
        /// horizontal when their logical Open/Close and Tilt inputs are 0. The
        /// upper flaps are children of NoseBody -> NoseBasket, so their mirrored
        /// open joints cancel the current combined nose pitch. Lower flaps are
        /// attached directly to the head and the tilt links are horizontal at
        /// their CAD joint zero.</summary>
        public void SetHorizontalFlapZeroes(double combinedNosePitchDegrees)
        {
            SetZero(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, -combinedNosePitchDegrees);
            SetZero(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, combinedNosePitchDegrees);
            SetZero(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, 0);
            SetZero(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, 0);
            SetZero(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, 0);
            SetZero(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, 0);
            Normalize();
            Reindex();
        }

        private void SetZero(ServoNames servo, RobotControls control, double value)
        {
            var setting = Get(servo, control);
            if (setting == null) return;
            setting.ZeroExtent = Math.Clamp(value, setting.MinExtent, setting.MaxExtent);
        }

        /// <summary>
        /// Direction comes directly from Servo Configuration. For real gangs,
        /// this is the same per-(gang, child) Direction shown in that screen;
        /// for single-servo inputs it is the physical servo's Reversed flag.
        /// </summary>
        public static bool InheritedReverse(ServoNames servo, RobotControls control,
                                            ServoConfiguration servoConfiguration)
        {
            if (servoConfiguration == null) return false;
            var controls = ServoConfiguration.ControlsFor(servo);
            if (controls.Length > 1)
                return servoConfiguration.GangReversed(servo, control);
            return servoConfiguration.Get(control)?.Reversed ?? false;
        }

        /// <summary>Map one child servo's normal animation input to its visual
        /// extent. Centered controls keep 0 at CAD neutral. Direction normally
        /// inherits ServoConfig.json but may be overridden for URDF visuals.</summary>
        public double Map(ServoNames servo, RobotControls control, double input,
                          ServoConfiguration servoConfiguration)
        {
            var s = Get(servo, control) ?? CreateDefault().Get(servo, control);
            if (s == null) return input;

            bool reverse = s.ReverseOverride ??
                           InheritedReverse(servo, control, servoConfiguration);

            double zero = Math.Clamp(s.ZeroExtent, s.MinExtent, s.MaxExtent);

            // IrisClose's semantic input is -100=open and +100=closed, so its
            // visual direction is inverted before applying the same Min/Zero/Max
            // calibration used by the other centered URDF servos.
            if (servo == ServoNames.IrisClose)
            {
                double v = -Math.Clamp(input, -100, 100);
                if (reverse) v = -v;
                return MapAroundZero(s.MinExtent, zero, s.MaxExtent, v);
            }

            // Positive-input controls have no negative authoring side. Logical 0
            // is the calibrated ZeroExtent. +100 (or EyePop 2000) moves toward Max
            // in Normal direction and toward Min in Reversed direction.
            if (IsPositiveInput(servo))
            {
                double maxInput = IsEyePop(servo) ? 2000.0 : 100.0;
                double v = Math.Clamp(input, 0, maxInput) / maxInput * 100.0;
                double endpoint = reverse ? s.MinExtent : s.MaxExtent;
                return zero + (v / 100.0) * (endpoint - zero);
            }

            double centered = Math.Clamp(input, -100, 100);
            if (reverse) centered = -centered;
            return MapAroundZero(s.MinExtent, zero, s.MaxExtent, centered);
        }

        private static double MapAroundZero(double min, double zero, double max, double value)
        {
            double v = Math.Clamp(value, -100, 100);
            return v < 0
                ? zero + ((-v) / 100.0) * (min - zero)
                : zero + (  v  / 100.0) * (max - zero);
        }

        /// <summary>The calibration window presents the normal authoring span:
        /// -100..100 or 0..100. Eye-pop animation JSON remains 0..2000, while
        /// its calibration/test slider is intentionally normalized to 0..100.</summary>
        public static (double Min, double Max) TestInputRange(ServoNames servo) =>
            IsPositiveInput(servo) ? (0, 100) : (-100, 100);

        public static double TestToServoValue(ServoNames servo, double testValue) =>
            IsEyePop(servo) ? Math.Clamp(testValue, 0, 100) * 20.0 : testValue;

        public static (double Min, double Max) ExtentEditorRange(ServoNames servo)
        {
            string unit = UnitFor(servo);
            if (unit == "deg")
            {
                // NoseBasket uses a positive 0..100 input but its calibrated
                // mechanical endpoints can straddle CAD zero (for example
                // -45..+45 degrees), so do not constrain its extents to positive angles.
                if (servo == ServoNames.NoseBasket) return (-180, 180);
                return IsPositiveInput(servo) ? (0, 180) : (-180, 180);
            }
            if (unit == "mm") return IsPositiveInput(servo) ? (0, 400) : (-100, 100);
            if (unit == "%") return (5, 100);
            return (-200, 200);
        }

        public static string UnitFor(ServoNames servo) => servo switch
        {
            ServoNames.IrisClose => "%",
            ServoNames.MFR_UpDown or ServoNames.Microphone_RaiseLower or
            ServoNames.Whip_Antenna_RaiseLower or ServoNames.LeftEyePop or
            ServoNames.RightEyePop => "mm",
            _ => "deg",
        };

        public static bool IsPositiveInput(ServoNames servo) => servo is
            ServoNames.NoseBasket or ServoNames.VentsOpen or ServoNames.MFR_UpDown or
            ServoNames.Microphone_RaiseLower or ServoNames.Whip_Antenna_RaiseLower or
            ServoNames.LeftEyePop or ServoNames.RightEyePop;

        private static bool IsEyePop(ServoNames servo) =>
            servo is ServoNames.LeftEyePop or ServoNames.RightEyePop;

        private void Normalize()
        {
            AudioLedGain = Math.Clamp(AudioLedGain, 0.5, 2.0);

            foreach (var m in Motions)
            {
                m.Unit = UnitFor(m.Servo);
                var (lo, hi) = ExtentEditorRange(m.Servo);
                m.MinExtent = Math.Clamp(m.MinExtent, lo, hi);
                m.MaxExtent = Math.Clamp(m.MaxExtent, lo, hi);
                if (m.MinExtent > m.MaxExtent)
                    (m.MinExtent, m.MaxExtent) = (m.MaxExtent, m.MinExtent);
                m.ZeroExtent = Math.Clamp(m.ZeroExtent, m.MinExtent, m.MaxExtent);
            }

            // Most Min/Max calibration is owned by the logical ServoName, so every
            // physical child in a gang shares the first child's range. Every child
            // keeps its own ZeroExtent and Direction. FlapsOpen is the Min/Max
            // exception: all four physical Open/Close flap servos keep independent
            // ranges while upper/lower pairs remain ganged for preview/test movement.
            foreach (var gang in Motions.GroupBy(m => m.Servo))
            {
                if (gang.Key == ServoNames.FlapsOpen)
                {
                    foreach (var child in gang)
                        child.ZeroExtent = Math.Clamp(child.ZeroExtent, child.MinExtent, child.MaxExtent);
                    continue;
                }

                var first = gang.First();
                foreach (var child in gang.Skip(1))
                {
                    child.MinExtent = first.MinExtent;
                    child.MaxExtent = first.MaxExtent;
                    child.ZeroExtent = Math.Clamp(child.ZeroExtent, child.MinExtent, child.MaxExtent);
                }
            }
        }

        private void Reindex() => _byKey = Motions.ToDictionary(x => (x.Servo, x.Control));

        private static JsonSerializerOptions JsonOptions()
        {
            var o = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            o.Converters.Add(new JsonStringEnumConverter());
            return o;
        }
    }

    public sealed class UrdfMotionSetting
    {
        public ServoNames Servo { get; set; }

        // Initialized to None so v1 URDFconfig.json files that lack this field
        // can be detected and migrated safely during Load().
        public RobotControls Control { get; set; } = RobotControls.None;
        public double MinExtent { get; set; }
        public double MaxExtent { get; set; }

        /// <summary>Physical URDF extent corresponding to logical input 0.
        /// Every URDF servo has an independently editable zero. Centered controls
        /// interpolate Min -> Zero -> Max; positive-input controls start at Zero
        /// and move toward the Direction-selected endpoint.</summary>
        public double ZeroExtent { get; set; } = 0;

        // Emitted for readability but reconstructed from Servo during load.
        public string Unit { get; set; } = "deg";

        /// <summary>Null = inherit direction from Servo Configuration.  When
        /// set, the URDF preview uses this value instead without altering the
        /// physical servo configuration.</summary>
        public bool? ReverseOverride { get; set; }
    }
}
