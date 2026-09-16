using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerChecks(string previewFolder)
    {
        FocusControlSettingsChecks();
        ControllerPreviewChecks();
        ControllerMotionTriggerChecks();
        ControllerNoMuxChecks();
        Check(Enumerable.Range(0, 4).All(i => ControllerCatalog.Layer((i & 1) != 0, (i & 2) != 0) == i), "All four shoulder combinations select independent banks.");
        foreach (var kind in Enum.GetValues<ControllerKind>())
        {
            var defaults = ControllerProfile.Defaults(kind); defaults.Validate();
            Check(defaults.Layers.Count == 4 && !ControllerCatalog.Inputs(kind).Any(i => i.Id.Contains("Shoulder")), "Shoulders cannot be remapped.");
            Check(!ReferenceEquals(defaults.Layers[0]["A"], defaults.Layers[1]["A"]), "Layer bindings must not share mutable state.");
            defaults.Layers[0]["LeftShoulder"] = new();
            bool rejected = false;
            try { defaults.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Persisted shoulder remaps must be rejected.");
        }
        string root = Path.Combine(Path.GetTempPath(), "j5-controller-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var p = ControllerProfile.Empty(ControllerKind.Steam);
            p.Layers[3]["GyroYaw"] = new() { Target = "Servo:NeckTurn", SensorScale = 2, DeadZone = 0.2, Invert = true };
            ControllerProfileStore.Save(root, p);
            var loaded = ControllerProfileStore.Load(root, ControllerKind.Steam);
            Check(loaded.Layers[3]["GyroYaw"].Invert && loaded.Layers[3]["GyroYaw"].SensorScale == 2 && loaded.Layers[0]["GyroYaw"].Target == "", "Persistence retains independent motion mapping banks.");
            Check(!File.Exists(ControllerProfileStore.PathFor(root, ControllerKind.Xbox)), "Steam save must not change Xbox settings.");
        }
        finally { Directory.Delete(root, true); }
        ControllerLibraryChecks();
        var profile = ControllerProfile.Empty(ControllerKind.Xbox);
        for (int layer = 0; layer < 4; layer++) profile.Layers[layer]["A"] = new() { Target = "Servo:NeckTurn", Mode = ControllerMapMode.Set, High = 10 + layer };
        profile.Layers[0]["LeftX"] = new() { Target = "Servo:NeckTurn", Mode = ControllerMapMode.Relative, DeadZone = 0.1 };
        var engine = new ControllerInputEngine { RelativeMotion = (_, before, input, seconds) => before + input * 100 * seconds };
        ControllerSample Sample(double a = 0, double x = 0, int layer = 0, bool connected = true) => new() { DeviceId = 1, Connected = connected,
            LeftShoulder = (layer & 1) != 0, RightShoulder = (layer & 2) != 0, Values = new() { ["A"] = a, ["LeftX"] = x } };
        Check(engine.Evaluate(profile, Sample(a: 1), 0.025, _ => 0).Count == 0, "Enabling with held button must not fire.");
        engine.Evaluate(profile, Sample(), 0.025, _ => 0);
        Check(engine.Evaluate(profile, Sample(a: 1), 0.025, _ => 0).Single().Value == 10, "Button fires its selected bank once.");
        Check(engine.Evaluate(profile, Sample(a: 1), 0.025, _ => 0).Count == 0, "Held button must not repeat.");
        Check(engine.Evaluate(profile, Sample(a: 1, layer: 2), 0.025, _ => 0).Count == 0, "Layer change with held button must not fire another mapping.");
        engine.Evaluate(profile, Sample(layer: 2), 0.025, _ => 0);
        Check(engine.Evaluate(profile, Sample(a: 1, layer: 2), 0.025, _ => 0).Single().Value == 12, "Right shoulder selects its completely independent mapping.");
        engine.Reset(); engine.Evaluate(profile, Sample(), 0.025, _ => 0);
        Check(engine.Evaluate(profile, Sample(x: 0.05), 0.025, _ => 0).Count == 0, "Stick dead zone suppresses drift.");
        Check(Math.Abs(engine.Evaluate(profile, Sample(x: 1), 0.025, _ => 0).Single().Value - 2.5) < 0.001, "Relative mapping uses elapsed time.");
        Check(engine.Evaluate(profile, Sample(connected: false), 0.025, _ => 0).Count == 0, "Disconnect stops outputs.");
        Check(engine.Evaluate(profile, Sample(x: 1), 0.025, _ => 0).Count == 0, "Reconnect with held axis waits for neutral.");
        Check(engine.Readings["LeftX"] is { Raw: 1, Active: true, State: "Release to arm" }, "Live feedback distinguishes a held input from an armed output.");
        engine.Evaluate(profile, Sample(), 0.025, _ => 0);
        var movement = engine.Evaluate(profile, Sample(x: 0.55), 0.025, _ => 10).Single();
        Check(engine.Readings["LeftX"].Raw == 0.55 && Math.Abs(engine.Readings["LeftX"].Mapped.Value - movement.Value) < 0.0001,
            "Raw input is preserved and the displayed mapping equals the actual emitted destination.");
        engine.Evaluate(profile, Sample(x: 0.05), 0.025, _ => movement.Value);
        Check(!engine.Readings["LeftX"].Active, "Dead-zone noise does not light up the control.");
        engine.Evaluate(profile, Sample(connected: false), 0.025, _ => 0);
        Check(engine.Readings.Count == 0, "Disconnect clears stale live highlights and values.");
        var steam = ControllerProfile.Empty(ControllerKind.Steam);
        steam.Layers[0]["AccelY"] = new() { Target = "Servo:NeckNodUp", Mode = ControllerMapMode.Relative, SensorScale = 10 };
        var stationary = new ControllerSample { DeviceId = 2, Connected = true, Values = new() { ["AccelY"] = 9.81 } };
        engine.Reset(); engine.Evaluate(steam, stationary, 0.025, _ => 0);
        Check(engine.Evaluate(steam, stationary, 0.025, _ => 0).Count == 0, "Gravity baseline must not move a stationary controller mapping.");
        Check(engine.Readings["AccelY"].Raw == 9.81 && !engine.Readings["AccelY"].Active, "Accelerometer feedback retains raw gravity without highlighting stationary motion.");
        Check(ControllerCatalog.Inputs(ControllerKind.Steam).Count(i => i.Section == "Motion") == 6, "All six gyro/accelerometer axes are mappable.");
        // Real driver smoke check: absence of hardware is valid, native loading errors are not.
        using (var service = new SdlControllerService())
        {
            service.Update(true);
            Check(service.Error == null, "Bundled SDL must initialize: " + service.Error);
            Console.WriteLine("Controller detection: " + (service.Devices.Count == 0 ? "no supported controllers attached" : string.Join(", ", service.Devices.Select(d => d.Kind + ": " + d.Name))));
        }
        if (!string.IsNullOrWhiteSpace(previewFolder))
        {
            Directory.CreateDirectory(previewFolder);
            foreach (ControllerKind kind in Enum.GetValues<ControllerKind>())
            {
                var draft = ControllerProfile.Defaults(kind);
                draft.Layers[0]["A"] = new() { Target = "Library:sequence", LibraryName = "Wave.json", Loop = true };
                if (kind == ControllerKind.Steam) draft.Layers[0]["GyroYaw"] = new() { Target = "Servo:NeckTurn", TriggerButton = "L4", SensorScale = 2 };
                var window = new ControllerMappingWindow(draft, () => new ControllerSample(), _ => { }, (_, _) => "Wave.json");
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(ControllerMappingWindow).GetMethod("SelectInput", flags).Invoke(window, new object[] { "A" });
                Check((bool)typeof(ControllerMappingWindow).GetMethod("Commit", flags).Invoke(window, null), "Library mapping loads and commits through the real inspector.");
                if (kind == ControllerKind.Steam)
                {
                    typeof(ControllerMappingWindow).GetMethod("SelectInput", flags).Invoke(window, new object[] { "GyroYaw" });
                    var trigger = (System.Windows.Controls.ComboBox)typeof(ControllerMappingWindow).GetField("_motionTrigger", flags).GetValue(window);
                    Check(trigger.Visibility == Visibility.Visible && trigger.SelectedIndex == 1 && trigger.Items[1].ToString().StartsWith("L4"),
                        "Motion inspector exposes its saved trigger and puts rear buttons first after None.");
                    trigger.SelectedIndex = 2;
                    Check((bool)typeof(ControllerMappingWindow).GetMethod("Commit", flags).Invoke(window, null)
                        && ((ControllerProfile)typeof(ControllerMappingWindow).GetField("_draft", flags).GetValue(window)).Layers[0]["GyroYaw"].TriggerButton == "R4",
                        "The real motion inspector persists a changed trigger selection.");
                }
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(1400, 940)); content.Arrange(new Rect(0, 0, 1400, 940)); content.UpdateLayout();
                // Flush deferred template rendering before capturing the offscreen window.
                content.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                var image = new RenderTargetBitmap(1440, 980, 96, 96, PixelFormats.Pbgra32); image.Render(content);
                var diagram = (ControllerDiagram)typeof(ControllerMappingWindow).GetField("_diagram", flags).GetValue(window);
                var hits = (Dictionary<Rect, string>)typeof(ControllerDiagram).GetField("_hits", flags).GetValue(diagram);
                Check(ControllerCatalog.Inputs(kind).All(input => hits.Values.Count(id => id == input.Id) == 2), "Every controller input has both a graphic hotspot and mapping callout after removing the list.");
                var hitMethod = typeof(ControllerDiagram).GetMethod("Hit", flags);
                Check(hits.All(hit => (string)hitMethod.Invoke(diagram, new object[] { new Point(hit.Key.X + hit.Key.Width / 2, hit.Key.Y + hit.Key.Height / 2) }) == hit.Value), "No control or mapping callout is obscured by another selectable region.");
                if (kind == ControllerKind.Steam)
                {
                    Rect Card(string id) => hits.Single(h => h.Value == id && h.Key.Width == diagram.MappingCardWidth).Key;
                    Check(Card("A").Left > Card("RightX").Right && Card("DpadUp").Right < Card("LeftX").Left,
                        "Steam face buttons and D-pad occupy narrower outer columns.");
                    Check(Card("GyroYaw").Left == Card("DpadUp").Left && Card("AccelY").Left == Card("A").Left,
                        "Steam motion mappings use the outer columns.");
                }
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(previewFolder, kind + "ControllerMapping.png")); encoder.Save(file);
                Check(content.ActualWidth > 1000, "Mapping screen lays out at desktop size.");
                if (kind == ControllerKind.Steam)
                {
                    var noMux = (System.Windows.Controls.CheckBox)typeof(ControllerMappingWindow).GetField("_noMux", flags).GetValue(window);
                    var layers = (System.Windows.Controls.TabControl)typeof(ControllerMappingWindow).GetField("_layerTabs", flags).GetValue(window);
                    noMux.IsChecked = true;
                    Check(layers.Visibility == Visibility.Collapsed && diagram.NoMux, "No MUX hides the four layer tabs and changes the controller artwork.");
                    typeof(ControllerMappingWindow).GetMethod("SelectInput", flags).Invoke(window, new object[] { "LeftShoulder" });
                    var tabs = (System.Windows.Controls.TabControl)typeof(ControllerMappingWindow).GetField("_mappingTabs", flags).GetValue(window);
                    tabs.SelectedIndex = 1;
                    ((System.Windows.Controls.TextBox)typeof(ControllerMappingWindow).GetField("_libraryName", flags).GetValue(window)).Text = "Rest.json";
                    Check((bool)typeof(ControllerMappingWindow).GetMethod("Commit", flags).Invoke(window, null), "A shoulder Library pose is editable through the actual mapping inspector.");
                    noMux.IsChecked = false;
                    Check(layers.Visibility == Visibility.Visible && !diagram.NoMux, "Unchecking No MUX restores the layer tabs.");
                    noMux.IsChecked = true;
                    typeof(ControllerMappingWindow).GetMethod("SelectInput", flags).Invoke(window, new object[] { "LeftShoulder" });
                    Check(((ControllerProfile)typeof(ControllerMappingWindow).GetField("_draft", flags).GetValue(window)).NoMuxMappings["LeftShoulder"].LibraryName == "Rest.json",
                        "Switching modes preserves the separate shoulder assignment.");
                    content.UpdateLayout(); content.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    var noMuxImage = new RenderTargetBitmap(1440, 980, 96, 96, PixelFormats.Pbgra32); noMuxImage.Render(content);
                    Check(ControllerCatalog.Inputs(kind, true).All(input => hits.Values.Count(id => id == input.Id) == 2)
                        && hits.All(hit => (string)hitMethod.Invoke(diagram, new object[] { new Point(hit.Key.X + hit.Key.Width / 2, hit.Key.Y + hit.Key.Height / 2) }) == hit.Value),
                        "Both new shoulder buttons and every No MUX callout are independently clickable.");
                    var noMuxEncoder = new PngBitmapEncoder(); noMuxEncoder.Frames.Add(BitmapFrame.Create(noMuxImage));
                    using var noMuxFile = File.Create(Path.Combine(previewFolder, "SteamControllerNoMuxMapping.png")); noMuxEncoder.Save(noMuxFile);
                }
            }
        }
    }

    private static void ControllerPreviewChecks()
    {
        var preview = new ControllerPreviewUpdates();
        var loop = new UrdfRenderLoop(attachToRendering: false);
        loop.AddTargets(_ => preview.Flush());
        var applied = new List<string>();
        preview.Enqueue("group", () => applied.Add("old group"));
        preview.Enqueue("child", () => applied.Add("child"));
        preview.Enqueue("group", () => applied.Add("latest group"));
        Check(preview.Count == 2 && loop.Advance(TimeSpan.Zero), "Visual queue coalesces pending values and presents its first frame.");
        Check(applied.SequenceEqual(new[] { "child", "latest group" }), "Coalescing preserves the order of group and child overrides.");
        preview.Enqueue("group", () => applied.Add("next"));
        Check(!loop.Advance(TimeSpan.FromSeconds(1.0 / 120)) && preview.Count == 1, "An intervening input poll does not force a faster visual frame.");
        Check(loop.Advance(TimeSpan.FromSeconds(1.0 / 60)) && applied.Last() == "next", "Pending controller values reach the next 60 Hz visual frame.");
        preview.Enqueue("old sequence", () => applied.Add("stale"));
        preview.Clear();
        preview.Enqueue("pose", () => applied.Add("replacement"));
        loop.Advance(TimeSpan.FromSeconds(1));
        Check(applied.Last() == "replacement" && !applied.Contains("stale"), "Stopped sequence visuals cannot overwrite a replacement pose.");
        foreach (int inputRate in new[] { 25, 40, 120 })
        {
            preview = new ControllerPreviewUpdates();
            int polls = 0, frames = 0, shown = -1;
            loop = new UrdfRenderLoop(attachToRendering: false);
            loop.AddTargets(_ => { if (preview.Count > 0) frames++; preview.Flush(); });
            for (int tick = 0; tick < 600 * 5; tick++)
            {
                if (tick % (600 / inputRate) == 0)
                {
                    int value = polls++; preview.Enqueue("stick", () => shown = value);
                }
                if (tick % 10 == 0) loop.Advance(TimeSpan.FromSeconds(tick / 600.0));
            }
            Check(polls == inputRate * 5 && frames <= 300 && frames >= Math.Min(inputRate, 60) * 5 - 1,
                "Controller visual cap preserves independent input polling at " + inputRate + " Hz.");
            loop.Advance(TimeSpan.FromSeconds(5));
            Check(shown == polls - 1, "The final controller value is displayed even without another input poll.");
        }
    }
}
