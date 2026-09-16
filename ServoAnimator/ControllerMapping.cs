using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServoAnimator;

public enum ControllerKind { Xbox, Steam }
public enum ControllerMapMode { Relative, Absolute, Set, Toggle }

public sealed record ControllerInput(string Id, string Label, string Section, bool Analog = false, bool Unipolar = false);

public static class ControllerCatalog
{
    public static readonly string[] LayerNames = { "Default · no shoulders", "Left shoulder", "Right shoulder", "Both shoulders" };
    public static int Layer(bool left, bool right) => (left ? 1 : 0) | (right ? 2 : 0);
    public static IReadOnlyList<ControllerInput> TriggerButtons(ControllerKind kind, bool noMux = false) => Inputs(kind, noMux)
        .Where(i => !i.Analog || i.Section == "Triggers")
        .OrderBy(i => i.Id is "L4" or "R4" or "L5" or "R5" ? 0 : i.Section == "Underside" ? 1 : 2).ToArray();
    public static bool TriggerHeld(ControllerSample sample, string id) => sample.Connected
        && sample.TryValue(id, out double value) && double.IsFinite(value) && value > 0.5;
    public static IReadOnlyList<ControllerInput> Inputs(ControllerKind kind, bool noMux = false)
    {
        var list = new List<ControllerInput> {
            new("LeftX", "Left stick · X", "Sticks", true), new("LeftY", "Left stick · Y", "Sticks", true),
            new("LeftClick", "Left stick · click", "Sticks"),
            new("RightX", "Right stick · X", "Sticks", true), new("RightY", "Right stick · Y", "Sticks", true),
            new("RightClick", "Right stick · click", "Sticks"),
            new("LeftTrigger", "Left trigger · LT / L2", "Triggers", true, true), new("RightTrigger", "Right trigger · RT / R2", "Triggers", true, true),
            new("A", "A", "Face"), new("B", "B", "Face"), new("X", "X", "Face"), new("Y", "Y", "Face"),
            new("DpadUp", "D-pad · Up", "D-pad"), new("DpadDown", "D-pad · Down", "D-pad"),
            new("DpadLeft", "D-pad · Left", "D-pad"), new("DpadRight", "D-pad · Right", "D-pad"),
            new("Back", kind == ControllerKind.Xbox ? "View / Back" : "View", "System"),
            new("Start", "Menu / Start", "System"), new("Guide", kind == ControllerKind.Xbox ? "Xbox button" : "Steam button", "System"),
            new("Share", kind == ControllerKind.Xbox ? "Share (if available)" : "Quick Access · …", "System") };
        if (kind == ControllerKind.Steam)
        {
            if (noMux)
            {
                list.Add(new("LeftShoulder", "Left Shoulder", "Shoulders"));
                list.Add(new("RightShoulder", "Right Shoulder", "Shoulders"));
            }
            foreach (string side in new[] { "Left", "Right" })
            {
                list.Add(new(side + "PadX", side + " trackpad · X", "Trackpads", true));
                list.Add(new(side + "PadY", side + " trackpad · Y", "Trackpads", true));
                list.Add(new(side + "PadTouch", side + " trackpad · touch", "Trackpads"));
                list.Add(new(side + "PadClick", side + " trackpad · click", "Trackpads"));
                list.Add(new(side + "PadPressure", side + " trackpad · pressure", "Trackpads", true, true));
                list.Add(new(side + "StickTouch", side + " stick · touch", "Sticks"));
                list.Add(new(side + "GripTouch", side + " grip · touch", "Underside"));
            }
            foreach (string button in new[] { "L4", "R4", "L5", "R5" }) list.Add(new(button, button + " · rear button", "Underside"));
            list.Add(new("GyroPitch", "Gyro · pitch (X)", "Motion", true));
            list.Add(new("GyroYaw", "Gyro · yaw (Y)", "Motion", true));
            list.Add(new("GyroRoll", "Gyro · roll (Z)", "Motion", true));
            foreach (string axis in new[] { "X", "Y", "Z" }) list.Add(new("Accel" + axis, "Accelerometer · " + axis, "Motion", true));
        }
        return list;
    }
    public static readonly string[] Actions = { "Snapshot", "Disable servos", "Default pose", "Play / pause sequence", "Stop", "Play / pause movie", "Previous sequence", "Next sequence", "Speed Default", "Speed Slow", "Speed Fast", "Speed Crawl", "RGB ClearAll" };
}

public sealed class ControllerBinding
{
    public string Target { get; set; } = ""; // Servo:name, Child:servo:control, Action:name
    public ControllerMapMode Mode { get; set; } = ControllerMapMode.Relative;
    public double Low { get; set; } = -100;
    public double High { get; set; } = 100;
    public double DeadZone { get; set; } = 0.12;
    public double SensorScale { get; set; } = 1;
    public bool Invert { get; set; }
    public string LibraryName { get; set; } = "";
    public bool Loop { get; set; }
    public string TriggerButton { get; set; } = "";
    public ControllerBinding Clone() => (ControllerBinding)MemberwiseClone();
}

public sealed class ControllerProfile
{
    public ControllerKind Kind { get; set; }
    public bool NoMux { get; set; }
    public Dictionary<string, ControllerBinding> NoMuxMappings { get; set; } = new();
    public List<Dictionary<string, ControllerBinding>> Layers { get; set; } = new();
    public int ActiveLayer(ControllerSample sample) => NoMux ? 0 : ControllerCatalog.Layer(sample.LeftShoulder, sample.RightShoulder);
    public Dictionary<string, ControllerBinding> MappingForLayer(int layer) => NoMux ? NoMuxMappings : Layers[layer];
    public void EnsureNoMuxMappings()
    {
        foreach (var input in ControllerCatalog.Inputs(Kind, true))
            if (!NoMuxMappings.ContainsKey(input.Id)) NoMuxMappings[input.Id] = Layers[0].TryGetValue(input.Id, out var binding) ? binding.Clone() : new();
    }
    public static ControllerProfile Empty(ControllerKind kind) => new() { Kind = kind,
        Layers = Enumerable.Range(0, 4).Select(_ => ControllerCatalog.Inputs(kind).ToDictionary(i => i.Id, _ => new ControllerBinding())).ToList() };
    public ControllerProfile Clone() => new() { Kind = Kind, NoMux = NoMux,
        NoMuxMappings = NoMuxMappings.ToDictionary(p => p.Key, p => p.Value.Clone()),
        Layers = Layers.Select(l => l.ToDictionary(p => p.Key, p => p.Value.Clone())).ToList() };
    public static ControllerProfile Defaults(ControllerKind kind)
    {
        var p = Empty(kind);
        void Map(int layer, string input, ServoNames servo, ControllerMapMode mode = ControllerMapMode.Relative, double? high = null)
        {
            var range = ServoCommand.RangeFor(servo);
            p.Layers[layer][input] = new ControllerBinding { Target = "Servo:" + servo, Mode = mode, Low = range.Min, High = high ?? range.Max };
        }
        void Action(int layer, string input, string action) => p.Layers[layer][input] = new ControllerBinding { Target = "Action:" + action, Mode = ControllerMapMode.Set };
        Map(0, "LeftX", ServoNames.NeckTurn); Map(0, "LeftY", ServoNames.NeckNodUp);
        Map(0, "RightX", ServoNames.FlapTiltUp); Map(0, "RightY", ServoNames.FlapsOpen);
        Map(0, "LeftTrigger", ServoNames.VentsOpen, ControllerMapMode.Absolute); Map(0, "RightTrigger", ServoNames.IrisClose, ControllerMapMode.Absolute);
        Map(0, "Y", ServoNames.BothEyePop, ControllerMapMode.Toggle); Map(0, "X", ServoNames.BothEyePop, ControllerMapMode.Toggle, 1000);
        Map(0, "A", ServoNames.FlapsOpen, ControllerMapMode.Set, -100); Map(0, "B", ServoNames.FlapsOpen, ControllerMapMode.Set, 100);
        Map(0, "DpadUp", ServoNames.NoseBody, ControllerMapMode.Toggle); Map(0, "DpadDown", ServoNames.NoseBasket, ControllerMapMode.Toggle);
        Action(0, "DpadLeft", "Default pose"); Action(0, "RightClick", "RGB ClearAll");
        Map(1, "LeftX", ServoNames.IrisClose); Map(1, "LeftY", ServoNames.VentsOpen);
        Map(1, "RightX", ServoNames.EyesHorizontalRight); Map(1, "RightY", ServoNames.EyesVerticalUp);
        Map(2, "LeftX", ServoNames.Whip_Antenna_Rotate); Map(2, "RightX", ServoNames.MFR_Rotate);
        Map(2, "Y", ServoNames.Microphone_RaiseLower, ControllerMapMode.Toggle);
        Map(2, "X", ServoNames.Whip_Antenna_RaiseLower, ControllerMapMode.Toggle);
        Map(2, "B", ServoNames.MFR_UpDown, ControllerMapMode.Toggle); Map(2, "A", ServoNames.VentsOpen, ControllerMapMode.Toggle);
        Action(2, "DpadUp", "Speed Default"); Action(2, "DpadLeft", "Speed Slow"); Action(2, "DpadRight", "Speed Fast"); Action(2, "DpadDown", "Speed Crawl");
        Map(3, "LeftX", ServoNames.NeckTiltRight); Map(3, "RightX", ServoNames.FlapTiltUp);
        Action(3, "DpadDown", "Play / pause movie"); Action(3, "DpadLeft", "Previous sequence"); Action(3, "DpadRight", "Next sequence");
        for (int layer = 0; layer < 4; layer++) { Action(layer, "Back", "Disable servos"); Action(layer, "Start", "Snapshot"); }
        return p;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || Layers?.Count != 4) throw new InvalidDataException("Exactly four shoulder layers are required.");
        if (NoMux && Kind != ControllerKind.Steam) throw new InvalidDataException("No MUX is available for Steam controllers only.");
        if (NoMuxMappings == null) throw new InvalidDataException("Missing No MUX mappings.");
        foreach (var layer in Layers) ValidateBank(layer, false);
        if (NoMux) EnsureNoMuxMappings();
        if (NoMuxMappings.Count > 0) ValidateBank(NoMuxMappings, true);

        void ValidateBank(Dictionary<string, ControllerBinding> layer, bool noMux)
        {
            var inputs = ControllerCatalog.Inputs(Kind, noMux);
            var allowed = inputs.Select(i => i.Id).ToHashSet();
            if (layer == null || layer.Keys.Any(k => !allowed.Contains(k))) throw new InvalidDataException("Unknown input or reserved shoulder mapping.");
            foreach (string id in allowed) if (!layer.ContainsKey(id)) layer[id] = new();
            foreach (var entry in layer)
                if (!string.IsNullOrEmpty(entry.Value?.TriggerButton) &&
                    (!inputs.Any(i => i.Id == entry.Key && i.Section == "Motion") ||
                     !ControllerCatalog.TriggerButtons(Kind, noMux).Any(i => i.Id == entry.Value.TriggerButton)))
                    throw new InvalidDataException("Motion triggers must use an available button; shoulders are reserved while MUX is enabled.");
            foreach (var binding in layer.Values)
            {
                if (binding == null || !Enum.IsDefined(binding.Mode) ||
                    !double.IsFinite(binding.Low) || !double.IsFinite(binding.High) || binding.Low > binding.High ||
                    !double.IsFinite(binding.DeadZone) || binding.DeadZone is < 0 or >= 1 ||
                    !double.IsFinite(binding.SensorScale) || binding.SensorScale is <= 0 or > 1000)
                    throw new InvalidDataException("Invalid mapping range, dead zone, or sensor scale.");
                if (!ControllerTargets.Valid(binding.Target)) throw new InvalidDataException("Unknown mapping target: " + binding.Target);
                if (ControllerTargets.IsLibrary(binding.Target) && string.IsNullOrWhiteSpace(binding.LibraryName))
                    throw new InvalidDataException("Choose a Library pose or sequence for this input.");
                if (binding.Loop && binding.Target != "Library:sequence")
                    throw new InvalidDataException("Loop is available only for Library sequences.");
                if (ControllerTargets.TryServo(binding.Target, out var servo, out _))
                {
                    var range = ServoCommand.RangeFor(servo);
                    if (binding.Low < range.Min || binding.High > range.Max) throw new InvalidDataException($"{servo} values must be in {range.Min}..{range.Max}.");
                }
            }
        }
    }
}

public static class ControllerTargets
{
    public static bool IsLibrary(string target) => target is "Library:pose" or "Library:sequence";
    public static bool TryServo(string target, out ServoNames servo, out RobotControls? child)
    {
        servo = default; child = null;
        string[] parts = (target ?? "").Split(':');
        if (parts.Length < 2 || !Enum.TryParse(parts[1], out servo) || !Enum.IsDefined(servo) || ServoCommand.IsTextValued(servo)) return false;
        if (parts[0] == "Servo" && parts.Length == 2) return true;
        if (parts[0] == "Child" && parts.Length == 3 && Enum.TryParse<RobotControls>(parts[2], out var c) && ServoConfiguration.ControlsFor(servo).Contains(c)) { child = c; return true; }
        return false;
    }
    public static bool Valid(string target) => string.IsNullOrEmpty(target) || IsLibrary(target) || TryServo(target, out _, out _) ||
        (target.StartsWith("Action:") && ControllerCatalog.Actions.Contains(target[7..]));
}

public static class ControllerProfileStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static string PathFor(string root, ControllerKind kind) => Path.Combine(root, kind + "ControllerMapping.json");
    public static ControllerProfile Load(string root, ControllerKind kind)
    {
        string path = PathFor(root, kind);
        if (!File.Exists(path)) return ControllerProfile.Defaults(kind);
        var profile = JsonSerializer.Deserialize<ControllerProfile>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty controller mapping.");
        if (profile.Kind != kind) throw new InvalidDataException("Controller mapping has the wrong device type.");
        profile.Validate(); return profile;
    }
    public static void Save(string root, ControllerProfile profile)
    {
        profile.Validate(); Directory.CreateDirectory(root);
        string path = PathFor(root, profile.Kind), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(profile, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
