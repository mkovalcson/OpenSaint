using System.IO;
using System.Text.Json;

namespace ServoAnimator;

public sealed class FocusControlSettings
{
    public bool CollisionSafeguard { get; set; } = true;
    public bool XboxController { get; set; } = true;
    public bool SteamController { get; set; } = true;
    public bool MoviePlayback { get; set; } = true;
    public bool XboxUrdf { get; set; } = true;
    public bool XboxPhysical { get; set; } = true;
    public bool SteamUrdf { get; set; } = true;
    public bool SteamPhysical { get; set; } = true;
    public bool StreamDeck { get; set; } = true;
    public bool StreamDeckUrdf { get; set; } = true;
    public bool StreamDeckPhysical { get; set; } = true;
    public bool AllowsController(ControllerKind kind) => kind == ControllerKind.Xbox ? XboxController : SteamController;
    public bool AllowsOutput(ControllerKind kind, bool physical) => AllowsController(kind) &&
        (kind == ControllerKind.Xbox ? physical ? XboxPhysical : XboxUrdf : physical ? SteamPhysical : SteamUrdf);
    public FocusControlSettings Clone() => (FocusControlSettings)MemberwiseClone();
    public static FocusControlSettings Load(string root)
    {
        string path = Path.Combine(root, "FocusControl.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<FocusControlSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty Focus Control settings.") : new();
    }
    public void Save(string root)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "FocusControl.json"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal static class FocusControlPolicy
{
    public static bool Controller(bool focused, bool backgroundEnabled, bool blockedByDialog)
        => !blockedByDialog && (focused || backgroundEnabled);
    public static bool Movie(bool enabled, bool sessionStarted, bool editorForeground, bool blockedByDialog, bool hasMovie)
        => enabled && sessionStarted && !editorForeground && !blockedByDialog && hasMovie;
}
