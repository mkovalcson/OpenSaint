using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerFeedbackChecks()
    {
        var app = new App(); app.InitializeComponent();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        string previews = Path.Combine(Environment.CurrentDirectory, "controller-previews"); Directory.CreateDirectory(previews);
        foreach (var kind in Enum.GetValues<ControllerKind>())
        {
            var profile = ControllerProfile.Defaults(kind);
            var inputs = ControllerCatalog.Inputs(kind);
            var pressed = new ControllerSample { Connected = true, DeviceId = 11,
                Values = inputs.Where(i => i.Section != "Motion").ToDictionary(i => i.Id, i => i.Analog ? .7 : 1.0) };
            var display = new ControllerMappingDisplay(); display.ShowMapping(profile, pressed);
            Check(pressed.Values.All(p => display.Diagram.LiveReadings[p.Key].Active), $"{kind}: main view highlights every button, axis, trigger and touchpad input even before output is armed");
            display.ShowMapping(profile, new ControllerSample { Connected = true, DeviceId = 11, Values = pressed.Values.ToDictionary(p => p.Key, _ => 0.0) });
            Check(display.Diagram.LiveReadings.Values.All(r => !r.Active), $"{kind}: release clears main-screen highlights");
            display.ShowMapping(profile, new());
            Check(display.Diagram.LiveReadings.Count == 0 && display.Diagram.LiveLayer == -1, $"{kind}: disconnect clears live state");
            var sample = new ControllerSample { Connected = true, DeviceId = 11,
                Supported = inputs.Select(i => i.Id).ToHashSet(),
                Values = inputs.ToDictionary(i => i.Id, _ => 0.0) };
            var window = new ControllerMappingWindow(profile, () => sample, _ => { });
            object Get(string field) => typeof(ControllerMappingWindow).GetField(field, flags).GetValue(window);
            void Call(string method) => typeof(ControllerMappingWindow).GetMethod(method, flags).Invoke(window, null);
            var diagram = (ControllerDiagram)Get("_diagram");
            Check(diagram.ShowLiveValues, $"{kind}: config reserves space for analog values outside mapping cards");
            sample.Values["LeftX"] = .65; sample.Values["RightTrigger"] = .45; sample.Values["A"] = 1;
            if (kind == ControllerKind.Steam)
            {
                sample.Values["LeftPadTouch"] = 1; sample.Values["LeftPadX"] = -.4; sample.Values["LeftPadY"] = .7;
                sample.Values["LeftPadClick"] = 1; sample.Values["LeftPadPressure"] = .02;
                sample.Values["GyroYaw"] = .8;
            }
            Call("UpdateLive");
            Check(sample.Values.Where(p => p.Value != 0).All(p => diagram.LiveReadings[p.Key].Active), $"{kind}: config highlights active inputs, including light trackpad pressure");
            Check(diagram.LiveReadings["LeftX"].Raw == .65, $"{kind}: config keeps raw analog values");
            var top = ((DockPanel)window.Content).Children.OfType<StackPanel>().First();
            Check(!top.Children.OfType<TextBlock>().Any(), $"{kind}: redundant config title and explanatory subtitle are removed");
            RenderControl((FrameworkElement)window.Content, Path.Combine(previews, kind + "LiveFeedback.png"), kind == ControllerKind.Steam ? 1640 : 1440, 980);
            var hits = (Dictionary<Rect, string>)typeof(ControllerDiagram).GetField("_hits", flags).GetValue(diagram);
            Check(hits.Where(p => p.Value == "LeftX").All(p => Equals(typeof(ControllerDiagram).GetMethod("Hit", flags).Invoke(diagram,
                new object[] { new Point(p.Key.X + p.Key.Width / 2, p.Key.Y + p.Key.Height / 2) }), "LeftX")), $"{kind}: diagram inputs stay clickable after adding value margins");
            if (kind == ControllerKind.Steam)
            {
                string banks = JsonSerializer.Serialize(((ControllerProfile)Get("_draft")).Layers);
                ((CheckBox)Get("_noMux")).IsChecked = true;
                var draft = (ControllerProfile)Get("_draft");
                draft.NoMuxMappings["LeftShoulder"] = new() { Target = "Library:pose", LibraryName = "Rest.json" };
                string folder = Path.Combine(previews, "mux-roundtrip");
                ControllerProfileStore.Save(folder, draft);
                var loaded = ControllerProfileStore.Load(folder, ControllerKind.Steam);
                Check(loaded.NoMux && JsonSerializer.Serialize(loaded.Layers) == banks, "Saving No MUX preserves every property in all four MUX banks");
                Check(loaded.NoMuxMappings["LeftShoulder"].LibraryName == "Rest.json" && ((TabControl)Get("_layerTabs")).Visibility == Visibility.Collapsed,
                    "No MUX saves shoulder mappings and makes MUX tabs inaccessible");
                ((CheckBox)Get("_noMux")).IsChecked = false;
                Check(JsonSerializer.Serialize(((ControllerProfile)Get("_draft")).Layers) == banks && ((TabControl)Get("_layerTabs")).Visibility == Visibility.Visible,
                    "Returning to MUX restores the original four banks unchanged");
            }
            sample = new(); Call("UpdateLive");
            Check(diagram.LiveReadings.Count == 0, $"{kind}: disconnect clears config values and highlights");
        }
    }
}
