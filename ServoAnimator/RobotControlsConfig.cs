// ---------------------------------------------------------------------------
// RobotControlsConfig.cs
//
// The individual hardware servo channels (RobotControls), their PWM
// configuration (default/min/max pulse widths, direction, 4-element speed
// and acceleration arrays indexed Default/Slow/Fast/Crawl), JSON load/save
// for the Servo Configuration window, and the GANG MAP tying the logical
// ServoNames used on the timeline to the physical controls they drive:
//
//   FlapsOpen      -> BrowLeftTopOpen, BrowRightTopOpen,
//                     BrowLeftBottomOpen, BrowRightBottomOpen
//   FlapTiltUp     -> BrowLeftTopTilt, BrowRightTopTilt
//   IrisClose      -> LeftIris, RightIris
//   VentsOpen      -> LeftEyeVent, RightEyeVent
//   NeckTiltRight  -> NeckTiltLeft, NeckTiltRight   (same pair as NeckNodUp)
//   NeckNodUp      -> NeckTiltLeft, NeckTiltRight
//   EyesHorizontalRight -> LeftLensHorizontal, RightLensHorizontal
//   EyesVerticalUp      -> LeftLensVertical, RightLensVertical
//   (single-control names map 1:1; RGBCommand and the eye pops map to none)
//
// The default configuration values were scraped from ConfigureServos.cs.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServoAnimator
{
    /// <summary>
    /// The physical channels, with values matching Servos.cs: 0..23 map
    /// directly to the 24-port Maestro card's channels ((int)control is the
    /// channel number), 200s are the Tic T249 eye-pop steppers, and 100s
    /// identify the Arduino RGB rings.
    /// </summary>
    public enum RobotControls
    {
        None = 1000,

        // Left
        LeftEyeVent = 0,
        LeftIris = 1,
        LeftLensHorizontal = 2,
        LeftLensVertical = 3,
        NeckTiltLeft = 4,
        BrowLeftBottomOpen = 5,

        // Nose
        BrowLeftTopOpen = 6,
        BrowLeftTopTilt = 7,
        NoseBody = 8,
        NoseBasket = 9,
        BrowRightTopTilt = 10,
        BrowRightTopOpen = 11,

        // Right
        BrowRightBottomOpen = 12,
        NeckTiltRight = 13,
        RightLensVertical = 14,
        RightLensHorizontal = 15,
        RightIris = 16,
        RightEyeVent = 17,

        // Misc
        Whip_Antenna_RaiseLower = 18,
        Whip_Antenna_Rotate = 19,
        MFR_UpDown = 20,
        MFR_Rotate = 21,
        NeckTurn = 22,
        Microphone_RaiseLower = 23,

        // Tic T249 eye-pop steppers (not Maestro PWM channels)
        LeftEyePop = 200,
        RightEyePop = 201,
        BothEyePop = 203,

        // Arduino RGB rings (identification only)
        LeftEyeRGBLightFront = 101,
        LeftEyeRGBLightBack = 102,
        RightEyeRGBLightFront = 103,
        RightEyeRGBLightBack = 104,
    }

    /// <summary>Configuration of one physical servo channel.</summary>
    public class ServoConfigEntry
    {
        public const int PwmFloor = 500;
        public const int PwmCeiling = 2400;

        [JsonPropertyName("control")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RobotControls Control { get; set; }

        /// <summary>Direction relative to its ganged ServoName: false =
        /// Normal, true = Reversed.</summary>
        [JsonPropertyName("reversed")]
        public bool Reversed { get; set; }

        [JsonPropertyName("defaultPwm")]
        public int DefaultPwm { get => _def; set => _def = ClampPwm(value); }
        private int _def = 1500;

        [JsonPropertyName("minPwm")]
        public int MinPwm { get => _min; set => _min = ClampPwm(value); }
        private int _min = PwmFloor;

        [JsonPropertyName("maxPwm")]
        public int MaxPwm { get => _max; set => _max = ClampPwm(value); }
        private int _max = PwmCeiling;

        /// <summary>Speed per ServoSpeed: [Default, Slow, Fast, Crawl].</summary>
        [JsonPropertyName("speeds")]
        public int[] Speeds { get; set; } = new int[4];

        /// <summary>Acceleration per ServoSpeed: [Default, Slow, Fast, Crawl].</summary>
        [JsonPropertyName("accels")]
        public int[] Accels { get; set; } = new int[4];

        public static int ClampPwm(int v) => Math.Clamp(v, PwmFloor, PwmCeiling);
    }

    /// <summary>The whole servo configuration document (JSON root).</summary>
    public class ServoConfiguration
    {
        [JsonPropertyName("servos")]
        public List<ServoConfigEntry> Servos { get; set; } = new();

        /// <summary>
        /// Direction of one control RELATIVE TO one ganged ServoName. This
        /// is per-(gang, control) because a control can serve two gangs
        /// differently: NeckNodUp moves the neck-tilt pair the SAME way,
        /// NeckTiltRight moves them OPPOSITE each other.
        /// </summary>
        public class GangDirectionEntry
        {
            [JsonPropertyName("servo")]
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public ServoNames Servo { get; set; }

            [JsonPropertyName("control")]
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public RobotControls Control { get; set; }

            [JsonPropertyName("reversed")]
            public bool Reversed { get; set; }
        }

        [JsonPropertyName("gangDirections")]
        public List<GangDirectionEntry> GangDirections { get; set; } = new();

        /// <summary>Is this control reversed RELATIVE TO this gang? This
        /// is a separate layer ON TOP of the control's own hardware
        /// Reverse flag (which stays in its ServoConfigEntry): absent
        /// entries mean "not gang-reversed". The hardware directions
        /// already make NeckTiltRight move the pair opposite each other;
        /// NeckNodUp adds a gang reversal on one servo to move them the
        /// same way instead.</summary>
        public bool GangReversed(ServoNames servo, RobotControls control)
        {
            var e = GangDirections.FirstOrDefault(
                        g => g.Servo == servo && g.Control == control);
            return e?.Reversed ?? false;
        }

        public void SetGangReversed(ServoNames servo, RobotControls control, bool reversed)
        {
            var e = GangDirections.FirstOrDefault(
                        g => g.Servo == servo && g.Control == control);
            if (e == null)
                GangDirections.Add(new GangDirectionEntry
                { Servo = servo, Control = control, Reversed = reversed });
            else
                e.Reversed = reversed;
        }

        /// <summary>Serial number of the LEFT Tic T249 eye-pop controller
        /// (the finder uses it to tell left from right; empty = the first
        /// Tic found is treated as left).</summary>
        [JsonPropertyName("leftTicSerialNumber")]
        public string LeftTicSerialNumber { get; set; } = "00475552";

        private static readonly JsonSerializerOptions Opts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public ServoConfigEntry Get(RobotControls c) =>
            Servos.FirstOrDefault(s => s.Control == c);

        public static ServoConfiguration Load(string path)
        {
            var cfg = JsonSerializer.Deserialize<ServoConfiguration>(
                          File.ReadAllText(path), Opts) ?? new ServoConfiguration();
            // Backfill any controls missing from the file with defaults so
            // the window always shows every channel.
            var defaults = CreateDefault();
            foreach (var d in defaults.Servos)
                if (cfg.Get(d.Control) == null) cfg.Servos.Add(d);
            foreach (var d in defaults.GangDirections)
                if (!cfg.GangDirections.Any(g => g.Servo == d.Servo &&
                                                 g.Control == d.Control))
                    cfg.GangDirections.Add(d);
            return cfg;
        }

        public void Save(string path) =>
            File.WriteAllText(path, JsonSerializer.Serialize(this, Opts));

        /// <summary>Gang map: which physical controls each logical
        /// ServoName drives. Empty = no hardware mapping (RGBCommand,
        /// eye pops, and the export-only Play).</summary>
        public static readonly Dictionary<ServoNames, RobotControls[]> GangMap = new()
        {
            [ServoNames.NeckTurn] = new[] { RobotControls.NeckTurn },
            [ServoNames.NeckNodUp] = new[] { RobotControls.NeckTiltLeft, RobotControls.NeckTiltRight },
            [ServoNames.NeckTiltRight] = new[] { RobotControls.NeckTiltLeft, RobotControls.NeckTiltRight },
            [ServoNames.FlapsOpen] = new[] { RobotControls.BrowLeftTopOpen, RobotControls.BrowRightTopOpen,
                                             RobotControls.BrowLeftBottomOpen, RobotControls.BrowRightBottomOpen },
            [ServoNames.FlapTiltUp] = new[] { RobotControls.BrowLeftTopTilt, RobotControls.BrowRightTopTilt },
            [ServoNames.IrisClose] = new[] { RobotControls.LeftIris, RobotControls.RightIris },
            [ServoNames.EyesVerticalUp] = new[] { RobotControls.LeftLensVertical, RobotControls.RightLensVertical },
            [ServoNames.EyesHorizontalRight] = new[] { RobotControls.LeftLensHorizontal, RobotControls.RightLensHorizontal },
            [ServoNames.VentsOpen] = new[] { RobotControls.LeftEyeVent, RobotControls.RightEyeVent },
            [ServoNames.NoseBody] = new[] { RobotControls.NoseBody },
            [ServoNames.NoseBasket] = new[] { RobotControls.NoseBasket },
            [ServoNames.MFR_UpDown] = new[] { RobotControls.MFR_UpDown },
            [ServoNames.MFR_Rotate] = new[] { RobotControls.MFR_Rotate },
            [ServoNames.Microphone_RaiseLower] = new[] { RobotControls.Microphone_RaiseLower },
            [ServoNames.Whip_Antenna_RaiseLower] = new[] { RobotControls.Whip_Antenna_RaiseLower },
            [ServoNames.Whip_Antenna_Rotate] = new[] { RobotControls.Whip_Antenna_Rotate },
            [ServoNames.LeftEyePop] = new[] { RobotControls.LeftEyePop },
            [ServoNames.RightEyePop] = new[] { RobotControls.RightEyePop },
            [ServoNames.BothEyePop] = new[] { RobotControls.LeftEyePop,
                                              RobotControls.RightEyePop },
            [ServoNames.RGBCommand] = Array.Empty<RobotControls>(),
            [ServoNames.Play] = Array.Empty<RobotControls>(),
        };

        public static RobotControls[] ControlsFor(ServoNames s) =>
            GangMap.TryGetValue(s, out var c) ? c : Array.Empty<RobotControls>();

        /// <summary>Default configuration scraped from ConfigureServos.cs:
        /// home/min/max pulse widths, direction, and the Default/Slow/Fast/
        /// Crawl speed and acceleration arrays.</summary>
        public static ServoConfiguration CreateDefault()
        {
            ServoConfigEntry E(RobotControls c, bool rev, int def, int min, int max,
                               int[] sp, int[] ac) => new()
            { Control = c, Reversed = rev, DefaultPwm = def, MinPwm = min, MaxPwm = max,
              Speeds = (int[])sp.Clone(), Accels = (int[])ac.Clone() };

            int[] nose = { 100, 50, 0, 50 }, noseA = { 30, 15, 0, 15 };
            int[] neck = { 20, 15, 0, 10 }, neckA = { 10, 6, 0, 5 };
            int[] neckR = { 90, 30, 0, 45 }, neckRA = { 10, 10, 0, 15 };
            int[] gaze = { 0, 60, 0, 20 }, gazeA = { 0, 25, 0, 5 };
            int[] iris = { 90, 90, 0, 10 }, irisA = { 15, 15, 0, 5 };
            int[] brows = { 60, 10, 0, 2 }, browsA = { 15, 5, 0, 3 };
            int[] bBrows = { 40, 10, 0, 2 }, bBrowsA = { 10, 5, 0, 3 };
            int[] vent = { 40, 10, 0, 10 }, ventA = { 20, 5, 0, 5 };
            int[] mfrH = { 0, 1, 0, 10 }, mfrHA = { 0, 1, 0, 20 };
            int[] mfrV = { 100, 50, 0, 50 }, mfrVA = { 30, 25, 0, 25 };
            int[] whipV = { 100, 20, 0, 50 }, whipVA = { 30, 5, 0, 25 };
            int[] whipH = { 0, 0, 0, 10 }, whipHA = { 0, 0, 0, 20 };
            int[] mic = { 0, 0, 0, 50 }, micA = { 0, 0, 0, 25 };

            ServoConfiguration.GangDirectionEntry G(ServoNames s,
                RobotControls c, bool rev) => new() { Servo = s, Control = c, Reversed = rev };

            return new ServoConfiguration
            {
                // Direction of each control RELATIVE to its gang. The neck
                // pair differs by gang: NeckTiltRight moves them opposite
                // (scraped directions), NeckNodUp moves them the same way.
                GangDirections = new List<GangDirectionEntry>
                {
                    G(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, false),
                    G(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, false),
                    G(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, false),
                    G(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, false),
                    G(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, false),
                    G(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, false),
                    G(ServoNames.IrisClose, RobotControls.LeftIris, false),
                    G(ServoNames.IrisClose, RobotControls.RightIris, false),
                    G(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal, false),
                    G(ServoNames.EyesHorizontalRight, RobotControls.RightLensHorizontal, false),
                    G(ServoNames.EyesVerticalUp, RobotControls.LeftLensVertical, false),
                    G(ServoNames.EyesVerticalUp, RobotControls.RightLensVertical, false),
                    G(ServoNames.VentsOpen, RobotControls.LeftEyeVent, false),
                    G(ServoNames.VentsOpen, RobotControls.RightEyeVent, false),
                    G(ServoNames.NeckTiltRight, RobotControls.NeckTiltLeft, false),
                    G(ServoNames.NeckTiltRight, RobotControls.NeckTiltRight, false),
                    G(ServoNames.NeckNodUp, RobotControls.NeckTiltLeft, false),
                    G(ServoNames.NeckNodUp, RobotControls.NeckTiltRight, true),
                },
                Servos = new List<ServoConfigEntry>
                {
                    E(RobotControls.NoseBasket, false, 850, 850, 1150, nose, noseA),
                    E(RobotControls.NoseBody, true, 1200, 1000, 1500, nose, noseA),
                    E(RobotControls.NeckTiltLeft, false, 1533, 1445, 1594, neck, neckA),
                    E(RobotControls.NeckTiltRight, true, 1480, 1400, 1564, neck, neckA),
                    E(RobotControls.NeckTurn, false, 1325, 850, 1740, neckR, neckRA),
                    E(RobotControls.LeftLensHorizontal, false, 1450, 650, 2250, gaze, gazeA),
                    E(RobotControls.RightLensHorizontal, false, 1450, 650, 2250, gaze, gazeA),
                    E(RobotControls.LeftLensVertical, true, 1400, 600, 2200, gaze, gazeA),
                    E(RobotControls.RightLensVertical, false, 1520, 720, 2320, gaze, gazeA),
                    E(RobotControls.LeftIris, false, 1575, 1350, 1950, iris, irisA),
                    E(RobotControls.RightIris, false, 975, 750, 1350, iris, irisA),
                    E(RobotControls.BrowLeftTopOpen, true, 1523, 920, 1770, brows, browsA),
                    E(RobotControls.BrowRightTopOpen, false, 866, 637, 1417, brows, browsA),
                    E(RobotControls.BrowLeftTopTilt, true, 1800, 910, 1900, bBrows, bBrowsA),
                    E(RobotControls.BrowRightTopTilt, false, 1100, 1000, 1990, bBrows, bBrowsA),
                    E(RobotControls.BrowLeftBottomOpen, false, 1750, 940, 2050, brows, browsA),
                    E(RobotControls.BrowRightBottomOpen, true, 1100, 700, 1910, brows, browsA),
                    E(RobotControls.LeftEyeVent, false, 1765, 1765, 2086, vent, ventA),
                    E(RobotControls.RightEyeVent, true, 1125, 835, 1125, vent, ventA),
                    E(RobotControls.MFR_Rotate, true, 1320, 800, 1865, mfrH, mfrHA),
                    E(RobotControls.MFR_UpDown, true, 550, 550, 1070, mfrV, mfrVA),
                    E(RobotControls.Whip_Antenna_RaiseLower, true, 2200, 1620, 2200, whipV, whipVA),
                    E(RobotControls.Whip_Antenna_Rotate, false, 1400, 500, 2300, whipH, whipHA),
                    E(RobotControls.Microphone_RaiseLower, false, 1625, 1625, 2298, mic, micA),
                },
            };
        }
    }
}
