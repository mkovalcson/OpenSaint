using System.IO;
using ServoAnimator;

internal static partial class Program
{
    private static void MappingFileChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "AnimationMappingChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var xbox = ControllerProfile.Defaults(ControllerKind.Xbox);
            ControllerProfileStore.Save(root, xbox);
            string original = File.ReadAllText(ControllerProfileStore.PathFor(root, ControllerKind.Xbox));
            string alternate = Path.Combine(root, "alternate.json");
            xbox.MappingForLayer(0)["LeftX"].Target = ControllerCatalog.RecordingTarget;
            ControllerProfileStore.SaveSelected(root, xbox, alternate);
            Check(ControllerProfileStore.ActivePath(root, ControllerKind.Xbox) == alternate, "Last used file is remembered");
            Check(ControllerProfileStore.Load(root, ControllerKind.Xbox).MappingForLayer(0)["LeftX"].Target == ControllerCatalog.RecordingTarget, "Startup loads selected mapping");
            Check(File.ReadAllText(ControllerProfileStore.PathFor(root, ControllerKind.Xbox)) == original, "Save as preserves original mapping");
            Check(ControllerProfileStore.ActivePath(root, ControllerKind.Steam) == ControllerProfileStore.PathFor(root, ControllerKind.Steam), "Controller selections are independent");
            bool rejected = false;
            try { ControllerProfileStore.LoadFile(alternate, ControllerKind.Steam); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Wrong controller type is rejected");
            File.Delete(alternate);
            Check(ControllerProfileStore.ActivePath(root, ControllerKind.Xbox) == ControllerProfileStore.PathFor(root, ControllerKind.Xbox), "Missing selected file falls back to original");
        }
        finally { Directory.Delete(root, true); }
    }
}
