using System.IO;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerNoMuxChecks()
    {
        var profile = ControllerProfile.Empty(ControllerKind.Steam);
        profile.Layers[0]["A"] = new() { Target = "Servo:NeckTurn", Mode = ControllerMapMode.Set, High = 10 };
        profile.Layers[1]["A"] = new() { Target = "Servo:NeckTurn", Mode = ControllerMapMode.Set, High = 20 };
        profile.NoMux = true; profile.Validate();
        Check(profile.NoMuxMappings["A"].High == 10 && !ReferenceEquals(profile.NoMuxMappings["A"], profile.Layers[0]["A"]), "First No MUX use copies Default without sharing mutable mappings.");
        profile.NoMuxMappings["LeftShoulder"] = new() { Target = "Library:pose", LibraryName = "Rest.json" };
        profile.NoMuxMappings["RightShoulder"] = new() { Target = "Library:sequence", LibraryName = "Wave.json", Loop = true };
        profile.Validate();
        ControllerSample Sample(bool left = false, bool right = false, double a = 0, double gyro = 0) => new()
        { Connected = true, DeviceId = 1, LeftShoulder = left, RightShoulder = right, Values = new() { ["A"] = a, ["GyroYaw"] = gyro } };
        var engine = new ControllerInputEngine { RelativeMotion = (_, before, input, seconds) => before + input * 100 * seconds };
        engine.Evaluate(profile, Sample(), 0.025, _ => 0);
        var left = engine.Evaluate(profile, Sample(left: true, a: 1), 0.025, _ => 0);
        Check(engine.ActiveLayer == 0 && left.Any(i => i.LibraryName == "Rest.json") && left.Any(i => i.Target == "Servo:NeckTurn" && i.Value == 10),
            "Left shoulder fires its mapping without changing the other controls' mapping bank.");
        Check(engine.Evaluate(profile, Sample(left: true, a: 1), 0.025, _ => 0).Count == 0, "A held mapped shoulder does not repeat.");
        var right = engine.Evaluate(profile, Sample(left: true, right: true, a: 1), 0.025, _ => 0);
        Check(right.Single() is { LibraryName: "Wave.json", Loop: true } && engine.ActiveLayer == 0,
            "Right shoulder can replace a pose with a looping sequence while the other shoulder is held.");
        profile.NoMux = false; profile.Validate();
        engine.Evaluate(profile, Sample(left: true), 0.025, _ => 0);
        Check(engine.Evaluate(profile, Sample(left: true, a: 1), 0.025, _ => 0).Single().Value == 20,
            "Re-enabling MUX restores the original independent shoulder layer.");
        profile.NoMux = true;
        Check(engine.Evaluate(profile, Sample(left: true, right: true, a: 1), 0.025, _ => 0).Count == 0,
            "Switching modes with held buttons requires release before a fresh mapping fires.");
        profile.NoMuxMappings["GyroYaw"] = new() { Target = "Servo:NeckTiltRight", TriggerButton = "RightShoulder", DeadZone = 0 };
        profile.Validate(); engine.Reset();
        engine.Evaluate(profile, Sample(), 0.025, _ => 0);
        Check(!engine.Evaluate(profile, Sample(gyro: 1), 0.025, _ => 0).Any(i => i.Target == "Servo:NeckTiltRight"), "A mapped shoulder may gate motion in No MUX mode.");
        Check(engine.Evaluate(profile, Sample(right: true, gyro: 1), 0.025, _ => 0).Any(i => i.Target == "Servo:NeckTiltRight"), "Holding the No MUX shoulder opens its motion gate.");
        string root = Path.Combine(Path.GetTempPath(), "j5-no-mux-" + Guid.NewGuid().ToString("N"));
        try
        {
            ControllerProfileStore.Save(root, profile);
            var saved = ControllerProfileStore.Load(root, ControllerKind.Steam);
            Check(saved.NoMux && saved.NoMuxMappings["RightShoulder"].Loop && saved.Layers[1]["A"].High == 20,
                "No MUX selection, shoulder mappings, and original layers all survive save/load.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
