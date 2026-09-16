using System.IO;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerMotionTriggerChecks()
    {
        var buttons = ControllerCatalog.TriggerButtons(ControllerKind.Steam);
        Check(buttons.Take(4).Select(i => i.Id).SequenceEqual(new[] { "L4", "R4", "L5", "R5" }), "Rear buttons lead the motion trigger choices.");
        Check(!buttons.Any(i => i.Section == "Motion" || i.Id.Contains("Shoulder")), "Sensor axes and fixed shoulder selectors cannot be motion triggers.");
        foreach (var axis in ControllerCatalog.Inputs(ControllerKind.Steam).Where(i => i.Section == "Motion"))
        {
            var profile = ControllerProfile.Empty(ControllerKind.Steam);
            profile.Layers[0][axis.Id] = new() { Target = "Servo:NeckTurn", TriggerButton = "L4", DeadZone = 0 };
            profile.Validate();
            var engine = new ControllerInputEngine { RelativeMotion = (_, before, input, seconds) => before + input * 100 * seconds };
            ControllerSample Sample(double raw, double? held) => new() { Connected = true, DeviceId = 1,
                Values = held.HasValue ? new() { [axis.Id] = raw, ["L4"] = held.Value } : new() { [axis.Id] = raw } };
            engine.Evaluate(profile, Sample(0, 0), 0.025, _ => 0);
            Check(engine.Evaluate(profile, Sample(1, 0), 0.025, _ => 0).Count == 0 && engine.Readings[axis.Id].State.StartsWith("Hold "), "Ungated motion stays visible but cannot drive " + axis.Id);
            Check(engine.Evaluate(profile, Sample(1, 1), 0.025, _ => 0).Single().Value == 2.5, "Holding the chosen button admits current motion for " + axis.Id);
            Check(engine.Evaluate(profile, Sample(1, 0), 0.025, _ => 2.5).Count == 0, "Releasing the trigger immediately blocks new motion for " + axis.Id);
            Check(engine.Evaluate(profile, Sample(1, null), 0.025, _ => 2.5).Count == 0, "Missing trigger input cannot enable " + axis.Id);
            profile.Layers[0][axis.Id].Mode = ControllerMapMode.Absolute;
            engine.Evaluate(profile, Sample(1, 1), 0.025, _ => 0);
            Check(engine.Evaluate(profile, Sample(0, 0), 0.025, _ => 100).Count == 0, "Trigger release does not center an absolute mapping for " + axis.Id);
        }
        var gated = ControllerProfile.Empty(ControllerKind.Steam);
        gated.Layers[0]["GyroYaw"] = new() { Target = "Library:sequence", LibraryName = "Wave.json", TriggerButton = "R5" };
        var edges = new ControllerInputEngine();
        ControllerSample Press(double sensor, double button) => new() { Connected = true, Values = new() { ["GyroYaw"] = sensor, ["R5"] = button } };
        Check(edges.Evaluate(gated, Press(1, 0), 0.025, _ => 0).Count == 0, "A Library motion mapping also requires its trigger button.");
        Check(edges.Evaluate(gated, Press(1, 1), 0.025, _ => 0).Single().LibraryName == "Wave.json", "Triggered motion can activate its Library mapping.");
        Check(edges.Evaluate(gated, Press(1, 1), 0.025, _ => 0).Count == 0, "A held motion trigger does not repeatedly activate a Library mapping.");
        string root = Path.Combine(Path.GetTempPath(), "j5-motion-trigger-" + Guid.NewGuid().ToString("N"));
        try
        {
            for (int layer = 0; layer < 4; layer++) gated.Layers[layer]["AccelX"].TriggerButton = buttons[layer].Id;
            ControllerProfileStore.Save(root, gated);
            var loaded = ControllerProfileStore.Load(root, ControllerKind.Steam);
            Check(Enumerable.Range(0, 4).All(i => loaded.Layers[i]["AccelX"].TriggerButton == buttons[i].Id), "Motion trigger choices persist independently in all four banks.");
            loaded.Layers[0]["GyroYaw"].TriggerButton = "GyroPitch";
            bool rejected = false; try { loaded.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "An invalid sensor-as-button trigger is rejected.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
