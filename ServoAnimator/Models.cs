// ---------------------------------------------------------------------------
// Models.cs
//
// The data model for the animation file plus all JSON serialization logic.
// The JSON layout matches animation.json:
//
//   {
//     "description":     "What this sequence does",
//     "audioFile":       "AudioFileName.mp3",
//     "durationSeconds": 91.22,
//     "commands": [
//        { "offsetSeconds": 0.069, "servo": "EyesHorizontalRight",
//          "value": -42, "speed": "Fast", "reason": "..." },
//        { "offsetSeconds": 2.5,   "servo": "LeftEyePop",
//          "value": 1500, "speed": "Default", "reason": "0..2000 range" },
//        { "offsetSeconds": 3.0,   "servo": "RGBCommand",
//          "value": "255,0,64 pulse", "speed": "Default", "reason": "text" },
//        ...
//     ]
//   }
//
// VALUE RANGES PER SERVO:
//   * LeftEyePop / RightEyePop : numeric, 0 .. 2000
//   * RGBCommand               : free-form TEXT string
//   * everything else          : numeric, -100 .. +100
//
// Because "value" can be a number or a string depending on the servo, the
// ServoCommand class uses a custom JsonConverter that inspects the JSON
// token type on read, and picks number vs. string on write.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace ServoAnimator
{
    /// <summary>All servos that can be driven. The JSON "servo" field is the
    /// string name of one of these values.</summary>
    public enum ServoNames
    {
        NeckTurn,
        NeckNodUp,
        NeckTiltRight,
        FlapsOpen,
        FlapTiltUp,
        IrisClose,
        EyesVerticalUp,
        EyesHorizontalRight,
        VentsOpen,
        NoseBody,
        NoseBasket,
        MFR_UpDown,
        MFR_Rotate,
        Microphone_RaiseLower,
        Whip_Antenna_RaiseLower,
        Whip_Antenna_Rotate,
        LeftEyePop,
        RightEyePop,

        /// <summary>Ganged eye pop: drives LeftEyePop AND RightEyePop
        /// together (both Tic steppers).</summary>
        BothEyePop,

        RGBCommand,

        /// <summary>Pseudo-servo used ONLY in exported animation files: a
        /// "Play" command carries the audio file path so the playback
        /// hardware knows what to start and when. It never appears in the
        /// servo grid or the command editor.</summary>
        Play,
    }

    /// <summary>Optional movement-speed profile for a command ("speed" in JSON).
    /// Default/Slow/Fast/Crawl index the hardware speed/accel arrays; NoChange
    /// is the N/C command value and deliberately sends no Maestro speed update.</summary>
    public enum ServoSpeed
    {
        // Stored as "N/C" in ServoCommand JSON.  This deliberately sits
        // outside the 0..3 indices used by ServoConfigEntry speed/accel
        // arrays so it can never be mistaken for a hardware profile.
        NoChange = 4,
        Default = 0,
        Slow = 1,
        Fast = 2,
        Crawl = 3,
    }

    /// <summary>
    /// One servo command on the timeline. Instances of this class are the
    /// single source of truth - the waveform markers, the editor dialog and
    /// the servo status grid all read/write these objects directly.
    ///
    /// The "value" is stored two ways:
    ///   * NumericValue - used by every servo except RGBCommand
    ///   * TextValue    - used only when Servo == RGBCommand
    /// IsTextServo tells you which one is meaningful for this command.
    /// </summary>
    [JsonConverter(typeof(ServoCommandJsonConverter))]
    public class ServoCommand
    {
        public double OffsetSeconds { get; set; }
        public ServoNames Servo { get; set; }

        /// <summary>Numeric value; range depends on the servo (see RangeFor).
        /// Ignored when the servo is RGBCommand.</summary>
        public int NumericValue { get; set; }

        /// <summary>Text value used only for text-valued servos
        /// (RGBCommand text; the audio path for the exported Play command).</summary>
        public string TextValue { get; set; } = "";

        /// <summary>EXPORT-ONLY: when set, the JSON writer emits this
        /// scaled value (rounded to 3 decimals, e.g. -1.000..1.000) in the
        /// value field instead of the raw NumericValue. Set on cloned
        /// commands during export when the "Scale ±1" option is checked;
        /// never set on in-memory/project commands.</summary>
        public double? ScaledExportValue { get; set; }

        /// <summary>Disable command: instead of moving, this command turns
        /// its servo(s) OFF (Maestro PWM disabled). Serialized as the
        /// literal string "Disable" in the value field - for both ganged
        /// and child-servo commands - and parsed back from it.</summary>
        public bool Disable { get; set; }

        /// <summary>
        /// Optional INDIVIDUAL control target. Null (the default) means the
        /// command drives the whole ganged ServoName. When set, the command
        /// drives just that one physical servo - and per the timeline rule,
        /// it stays in control of that servo until the next GANGED command
        /// for the same ServoName supersedes it (the playback hardware layer
        /// applies this precedence). Saved as an optional "control" field.
        /// </summary>
        public RobotControls? Control { get; set; }

        /// <summary>24-bit color for RGBCommand commands, as "#RRGGBB".
        /// Selected from the palette in the command editor; empty when no
        /// color has been chosen. Saved as an optional "color" field in the
        /// JSON (part of the project).</summary>
        public string ColorHex { get; set; } = "";

        public ServoSpeed Speed { get; set; } = ServoSpeed.NoChange;

        /// <summary>User-facing command speed.  NoChange is displayed and
        /// serialized as N/C so a command can move a servo without changing
        /// the Maestro speed/acceleration profile already in effect.</summary>
        public string SpeedDisplay => SpeedToText(Speed);

        public static string SpeedToText(ServoSpeed speed) =>
            speed == ServoSpeed.NoChange ? "N/C" : speed.ToString();

        public static bool TryParseSpeed(string text, out ServoSpeed speed)
        {
            string value = (text ?? "").Trim();
            if (string.Equals(value, "N/C", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "NC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "NoChange", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "No Change", StringComparison.OrdinalIgnoreCase))
            {
                speed = ServoSpeed.NoChange;
                return true;
            }
            return Enum.TryParse(value, true, out speed);
        }

        /// <summary>Optional free-text description of why the command exists.</summary>
        public string Reason { get; set; } = "";

        /// <summary>True when this command's value is a text string (RGBCommand).</summary>
        public bool IsTextServo => IsTextValued(Servo);

        /// <summary>Which servos carry a text value instead of a number.</summary>
        public static bool IsTextValued(ServoNames s) =>
            s == ServoNames.RGBCommand || s == ServoNames.Play;

        /// <summary>Numeric value range for a servo:
        ///   * LeftEyePop / RightEyePop                       : 0 .. 2000
        ///   * NoseBasket / MFR_UpDown / Whip_Antenna_RaiseLower /
        ///     VentsOpen / Microphone_RaiseLower              : 0 .. 100
        ///   * all other numeric servos                       : -100 .. +100</summary>
        public static (int Min, int Max) RangeFor(ServoNames s) => s switch
        {
            ServoNames.LeftEyePop or ServoNames.RightEyePop
                or ServoNames.BothEyePop => (0, 2000),
            ServoNames.NoseBasket or ServoNames.MFR_UpDown
                or ServoNames.Whip_Antenna_RaiseLower or ServoNames.VentsOpen
                or ServoNames.Microphone_RaiseLower => (0, 100),
            _ => (-100, 100),
        };

        /// <summary>Clamp NumericValue into this command's servo range.
        /// Called after JSON load and whenever the servo selection changes.</summary>
        public void ClampToRange()
        {
            var (mn, mx) = RangeFor(Servo);
            NumericValue = Math.Clamp(NumericValue, mn, mx);
        }

        /// <summary>Human-readable value for lists/debug output.</summary>
        public string ValueDisplay => IsTextServo ? $"\"{TextValue}\"" : NumericValue.ToString();

        /// <summary>Deep copy, used by the copy/paste clipboard.</summary>
        public ServoCommand Clone() => new()
        {
            OffsetSeconds = OffsetSeconds,
            Servo = Servo,
            NumericValue = NumericValue,
            TextValue = TextValue,
            ScaledExportValue = ScaledExportValue,
            Disable = Disable,
            Control = Control,
            ColorHex = ColorHex,
            Speed = Speed,
            Reason = Reason,
        };

        /// <summary>
        /// Timeline offsets are doubles, so two commands "at the same time"
        /// are grouped by rounding to the millisecond. Every piece of code
        /// that asks "which commands live at time t?" uses this key.
        /// </summary>
        public static double TimeKey(double seconds) => Math.Round(seconds, 3);
    }

    /// <summary>
    /// Custom JSON (de)serializer for ServoCommand. Needed because the
    /// "value" field is polymorphic: a number for ordinary servos, a string
    /// for RGBCommand. On read the JSON token type decides which field is
    /// filled; on write the servo type decides which representation is used.
    /// Property names/order match the sample animation.json.
    /// </summary>
    public class ServoCommandJsonConverter : JsonConverter<ServoCommand>
    {
        public override ServoCommand Read(ref Utf8JsonReader reader,
            Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected a command object");

            var cmd = new ServoCommand();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                string prop = reader.GetString();
                reader.Read();

                switch (prop)
                {
                    case "offsetSeconds":
                        cmd.OffsetSeconds = reader.GetDouble();
                        break;

                    case "servo":
                        if (Enum.TryParse(reader.GetString(), true, out ServoNames s))
                            cmd.Servo = s;
                        break;

                    case "value":
                        // Polymorphic: a string (RGBCommand text, or the
                        // literal "Disable" on any servo) or a number.
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string s2 = reader.GetString() ?? "";
                            if (string.Equals(s2, "Disable",
                                              StringComparison.OrdinalIgnoreCase))
                                cmd.Disable = true;
                            else
                                cmd.TextValue = s2;
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                            cmd.NumericValue = (int)Math.Round(reader.GetDouble());
                        break;

                    case "speed":
                        if (ServoCommand.TryParseSpeed(reader.GetString(), out ServoSpeed sp))
                            cmd.Speed = sp;
                        break;

                    case "reason":
                        cmd.Reason = reader.GetString() ?? "";
                        break;

                    case "color":
                        cmd.ColorHex = reader.GetString() ?? "";
                        break;

                    case "control":
                        if (Enum.TryParse(reader.GetString(), true,
                                          out RobotControls rc))
                            cmd.Control = rc;
                        break;

                    default:
                        reader.Skip();   // tolerate unknown fields
                        break;
                }
            }

            cmd.ClampToRange();   // enforce the per-servo numeric range on load
            return cmd;
        }

        public override void Write(Utf8JsonWriter writer, ServoCommand cmd,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("offsetSeconds", cmd.OffsetSeconds);
            writer.WriteString("servo", cmd.Servo.ToString());

            if (cmd.IsTextServo)
                writer.WriteString("value", cmd.Disable ? "Disable"
                                                         : cmd.TextValue ?? "");
            else
            {
                if (cmd.Disable)
                    writer.WriteString("value", "Disable");
                else if (cmd.ScaledExportValue.HasValue)
                    writer.WriteNumber("value",
                        Math.Round(cmd.ScaledExportValue.Value, 3));
                else
                    writer.WriteNumber("value", cmd.NumericValue);
            }

            writer.WriteString("speed", ServoCommand.SpeedToText(cmd.Speed));
            writer.WriteString("reason", cmd.Reason ?? "");
            if (cmd.Control.HasValue)
                writer.WriteString("control", cmd.Control.Value.ToString());
            if (!string.IsNullOrEmpty(cmd.ColorHex))
                writer.WriteString("color", cmd.ColorHex);   // RGBCommand palette color
            writer.WriteEndObject();
        }
    }


    /// <summary>Compact JSON shape used by files in Library\Animation.
    /// Description is deliberately first so it acts as a readable header.</summary>
    public class LibraryItemDocument
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyName
        {
            get => null;
            set
            {
                if (string.IsNullOrWhiteSpace(Description))
                    Description = value ?? "";
            }
        }

        [JsonPropertyName("image")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ImageFile { get; set; }

        [JsonPropertyName("commands")]
        public List<ServoCommand> Commands { get; set; } = new();
    }


    /// <summary>One sequence block on the movie timeline. FilePath is kept
    /// as a full path in memory; only the ordered pathnames are persisted
    /// by MovieDocument.</summary>
    public class MovieSequenceItem
    {
        public string FilePath { get; set; } = "";
        public double DurationSeconds { get; set; }
    }

    /// <summary>Movie project format: an ordered list of sequence filenames/pathnames.
    /// Timing is deliberately not duplicated here; block durations are reread from
    /// the sequence files when the movie is loaded.</summary>
    public class MovieDocument
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        /// <summary>Calendar date on which this movie project was created.
        /// Stored as yyyy-MM-dd and retained across later Save As operations.</summary>
        [JsonPropertyName("createdDate")]
        public string CreatedDate { get; set; } = "";

        [JsonPropertyName("sequences")]
        public List<string> Sequences { get; set; } = new();

        private static readonly JsonSerializerOptions MovieJsonOpts = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static MovieDocument Load(string path)
        {
            var movie = JsonSerializer.Deserialize<MovieDocument>(
                            File.ReadAllText(path), MovieJsonOpts)
                        ?? new MovieDocument();
            movie.Description ??= "";
            movie.Sequences ??= new List<string>();
            if (string.IsNullOrWhiteSpace(movie.CreatedDate))
            {
                DateTime created = File.Exists(path) ? File.GetCreationTime(path) : DateTime.Today;
                movie.CreatedDate = created.ToString("yyyy-MM-dd");
            }
            return movie;
        }

        public void Save(string path)
        {
            Description ??= "";
            Sequences ??= new List<string>();
            if (string.IsNullOrWhiteSpace(CreatedDate))
                CreatedDate = DateTime.Today.ToString("yyyy-MM-dd");
            File.WriteAllText(path, JsonSerializer.Serialize(this, MovieJsonOpts));
        }
    }

    /// <summary>The whole animation file (root JSON object).</summary>
    public class AnimationDocument
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        /// <summary>Backward-compatible reader for sequence files created
        /// before the top-level "name" field was replaced by "description".
        /// The getter intentionally returns null, so newly saved files contain
        /// only "description" and never write the legacy "name" field.</summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyName
        {
            get => null;
            set
            {
                if (string.IsNullOrWhiteSpace(Description))
                    Description = value ?? "";
            }
        }

        /// <summary>'|'-delimited list of every audio file on the timeline
        /// (the primary audio first, then each additional "Play" clip in
        /// offset order). Computed when saving/exporting; a header for
        /// consumers of the Exported Animation JSON.</summary>
        [JsonPropertyName("audioFiles")]
        public string AudioFiles { get; set; } = "";

        [JsonPropertyName("audioFile")]
        public string AudioFile { get; set; } = "";

        /// <summary>Full pathname of the audio file (PROJECT files). Load
        /// Project reads the audio from here; plain animation exports may
        /// leave it empty and rely on "audioFile" next to the JSON.</summary>
        [JsonPropertyName("audioFilePath")]
        public string AudioFilePath { get; set; } = "";

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Where the audio starts on the timeline (seconds). Dragging the
        /// handle at the top-left of the waveform sets this, letting commands
        /// be placed BEFORE the audio begins. Optional in the JSON: files
        /// without this field load with offset 0 (audio starts at t=0).
        /// </summary>
        [JsonPropertyName("audioStartOffsetSeconds")]
        public double AudioStartOffsetSeconds { get; set; }

        /// <summary>Servos whose "Spline" checkbox is ticked (names). Optional
        /// in the JSON; files without it load with no spline servos.</summary>
        [JsonPropertyName("splineServos")]
        public List<string> SplineServos { get; set; } = new();

        /// <summary>Spline sample rate (Hz) used when saving generates
        /// commands along each spline. Optional; defaults to 50.</summary>
        [JsonPropertyName("splineSampleHz")]
        public int SplineSampleHz { get; set; } = 50;

        /// <summary>Export mode: "Individual" (default) expands every
        /// ganged command into one command per child control with values
        /// adjusted for gang-relative reversal; "Ganged" exports ganged
        /// commands as-is.</summary>
        [JsonPropertyName("animateMode")]
        public string AnimateMode { get; set; } = "Individual";

        /// <summary>Export option: scale numeric values to ±1.000 (or
        /// 0..1.000) instead of ±100 / 0..100 / 0..2000.</summary>
        [JsonPropertyName("scaleValues")]
        public bool ScaleValues { get; set; }

        [JsonPropertyName("commands")]
        public List<ServoCommand> Commands { get; set; } = new();

        // Shared serializer options: pretty printed, enums written as strings.
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Read a full animation document from disk.</summary>
        public static AnimationDocument Load(string path)
        {
            var doc = JsonSerializer.Deserialize<AnimationDocument>(
                          File.ReadAllText(path), JsonOpts)
                      ?? new AnimationDocument();
            doc.Commands ??= new List<ServoCommand>();
            return doc;
        }

        /// <summary>Write this document to disk (sorted by time for readability).</summary>
        public void Save(string path)
        {
            Commands.Sort((a, b) => a.OffsetSeconds.CompareTo(b.OffsetSeconds));
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }

        /// <summary>
        /// Writes a LIBRARY SEQUENCE file. The top-level description is a short
        /// human-readable header used by the library browser; commands are
        /// the selected timeline range rebased to zero.
        /// </summary>
        public static void SaveCommandsOnly(string path, List<ServoCommand> commands,
                                            string description = "")
        {
            commands.Sort((a, b) => a.OffsetSeconds.CompareTo(b.OffsetSeconds));
            var wrapper = new LibraryItemDocument
            {
                Description = description ?? "",
                Commands = commands,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(wrapper, JsonOpts));
        }

        /// <summary>Write a single-time-point Library Command using the
        /// same description + commands JSON shape as a Library Sequence. Command
        /// order is preserved because same-time command ordering can matter for
        /// ganged/child overrides.</summary>
        public static void SaveLibraryCommand(string path, IEnumerable<ServoCommand> commands,
                                              string description = "", string imageFile = null)
        {
            var wrapper = new LibraryItemDocument
            {
                Description = description ?? "",
                ImageFile = string.IsNullOrWhiteSpace(imageFile) ? null : imageFile,
                Commands = commands?.Select(c => c.Clone()).ToList()
                           ?? new List<ServoCommand>(),
            };
            foreach (var command in wrapper.Commands)
                command.OffsetSeconds = 0.0;
            File.WriteAllText(path, JsonSerializer.Serialize(wrapper, JsonOpts));
        }

        /// <summary>Reads a Library Sequence/Command, including its description header.
        /// Older command-only objects, full animation documents and bare
        /// command arrays are all accepted for backward compatibility.</summary>
        public static LibraryItemDocument LoadLibraryItem(string path)
        {
            string text = File.ReadAllText(path).TrimStart();

            if (text.StartsWith("["))
            {
                return new LibraryItemDocument
                {
                    Commands = JsonSerializer.Deserialize<List<ServoCommand>>(text, JsonOpts)
                               ?? new List<ServoCommand>(),
                };
            }

            var item = JsonSerializer.Deserialize<LibraryItemDocument>(text, JsonOpts)
                       ?? new LibraryItemDocument();
            item.Commands ??= new List<ServoCommand>();
            return item;
        }

        /// <summary>Update only the description header of an existing
        /// Library Sequence/Command while retaining its commands.</summary>
        public static void UpdateLibraryDescription(string path, string description)
        {
            string text = File.ReadAllText(path).TrimStart();
            JsonNode node = JsonNode.Parse(text, null, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (node is JsonObject obj)
            {
                // Rebuild the object so description stays at the top as a
                // readable header, while preserving any additional fields.
                var updated = new JsonObject
                {
                    ["description"] = description ?? "",
                };
                foreach (var pair in obj)
                {
                    if (pair.Key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                        pair.Key.Equals("name", StringComparison.OrdinalIgnoreCase))
                        continue;
                    updated[pair.Key] = pair.Value?.DeepClone();
                }
                File.WriteAllText(path, updated.ToJsonString(JsonOpts));
                return;
            }

            // A legacy bare command array is upgraded to the current library
            // sequence object when its description is first edited.
            var item = LoadLibraryItem(path);
            SaveCommandsOnly(path, item.Commands, description);
        }

        /// <summary>Update the optional image reference on a Library Command
        /// while preserving its description, commands, and any future fields.
        /// The image path is normally stored relative to the JSON file.</summary>
        public static void UpdateLibraryImage(string path, string imageFile)
        {
            string text = File.ReadAllText(path).TrimStart();
            JsonNode node = JsonNode.Parse(text, null, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (node is JsonObject obj)
            {
                if (string.IsNullOrWhiteSpace(imageFile))
                    obj.Remove("image");
                else
                    obj["image"] = imageFile;
                File.WriteAllText(path, obj.ToJsonString(JsonOpts));
                return;
            }

            // Upgrade a legacy bare command array to the current library object.
            var item = LoadLibraryItem(path);
            SaveLibraryCommand(path, item.Commands, item.Description, imageFile);
        }

        /// <summary>
        /// Reads *just* the commands from a JSON file, used by the
        /// "Insert commands from JSON file" menu item and by Animation
        /// Library > Insert Library Sequence.
        /// </summary>
        public static List<ServoCommand> LoadCommandsOnly(string path) =>
            LoadLibraryItem(path).Commands;
    }
}
