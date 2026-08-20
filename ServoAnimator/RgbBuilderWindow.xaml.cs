// ---------------------------------------------------------------------------
// RgbBuilderWindow.xaml.cs
//
// Builds RGBCommand text values in editor/storage order. Explicit colors are
// kept as Red,Green,Blue here for readability and existing sequence
// compatibility. RgbCommandWireFormat rotates them to Green,Red,Blue only
// when passed to the Arduino or Arduino emulator:
//
//   ClearAll
//   Clear,{ring},{side}
//   SetRGBColor,{r},{g},{b},{brightness},{ring},{side}
//   ColorWipeEyes,{r},{g},{b},{brightness},{ring},{side},{delayMs}
//   Fade,{r},{g},{b},{brightness},{ring},{side},{delayMs},{fade},{step},{lowestBrightness}
//   Pulse,{r},{g},{b},{brightness},{ring},{side},{delayMs},{numberPulses},{brightnessStep},{lowestBrightness}
//   TheaterChase,{r},{g},{b},{brightness},{ring},{side},{delayMs},{cycles}
//   Rainbow,{brightness},{side},{delayMs}
//   RainbowWipe,{brightness},{side},{delayMs}
//       (RGBLight.cs takes a cycles argument but its format string only has
//        three placeholders, so cycles never appears in the output - this
//        builder reproduces that output faithfully and omits the field)
//   RAINBOWCYCLE,{brightness},{side},{delayMs}
//   RainbowChase,{brightness},{side},{delayMs}
//
// Enum arguments (Ring: Eyes/Vents/Both, Side: Left/Right/LR,
// Fade: In/Out) render as combo boxes; numeric arguments are text boxes.
// A live preview shows the exact text OK will commit.
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator
{
    public partial class RgbBuilderWindow : Window
    {
        /// <summary>The generated command text (valid after OK).</summary>
        public string ResultText { get; private set; } = "";
        public string ResultColorHex { get; private set; } = "";

        private readonly ObservableCollection<RgbArgVM> _args = new();
        private readonly int _r, _g, _b;   // palette prefill for r/g/b args

        private static readonly string[] RingOptions = { "Eyes", "Vents", "Both" };
        private static readonly string[] SideOptions = { "Left", "Right", "LR" };
        private static readonly string[] FadeOptions = { "In", "Out" };

        /// <summary>Argument specs per command, in format-string order.
        /// (name, enum options or null for numeric, default value)</summary>
        private static readonly Dictionary<string, (string Name, string[] Options, string Default)[]>
            Specs = new()
        {
            ["ClearAll"] = Array.Empty<(string, string[], string)>(),
            ["Clear"] = new[] { ("ring", RingOptions, "Both"), ("side", SideOptions, "LR") },
            ["SetRGBColor"] = Rgb(),
            ["ColorWipeEyes"] = Rgb(("delayMs", null, "20")),
            ["Fade"] = Rgb(("delayMs", null, "20"), ("fade", FadeOptions, "In"),
                           ("step", null, "5"), ("lowestBrightness", null, "0")),
            ["Pulse"] = Rgb(("delayMs", null, "20"), ("numberPulses", null, "3"),
                            ("brightnessStep", null, "5"), ("lowestBrightness", null, "0")),
            ["TheaterChase"] = Rgb(("delayMs", null, "50"), ("cycles", null, "10")),
            ["Rainbow"] = Bsd(),
            ["RainbowWipe"] = Bsd(),          // cycles omitted: see header note
            ["RAINBOWCYCLE"] = Bsd(),
            ["RainbowChase"] = Bsd(),
        };

        private static (string, string[], string)[] Rgb(
            params (string, string[], string)[] extra)
        {
            var list = new List<(string, string[], string)>
            {
                ("red", null, "255"), ("green", null, "0"), ("blue", null, "0"),
                ("brightness", null, "200"),
                ("ring", RingOptions, "Eyes"), ("side", SideOptions, "LR"),
            };
            list.AddRange(extra);
            return list.ToArray();
        }

        private static (string, string[], string)[] Bsd() => new[]
        {
            ("brightness", (string[])null, "200"),
            ("side", SideOptions, "LR"),
            ("delayMs", (string[])null, "20"),
        };

        public RgbBuilderWindow(string prefillCommandText = "", string prefillColorHex = "")
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "rgb-lighting");
            HelpSystem.SetTopic(CommandCombo, "rgb-lighting");
            HelpSystem.SetTopic(ArgList, "rgb-lighting");
            HelpSystem.SetTopic(PreviewText, "rgb-lighting");

            // Parse the existing command text so reopening Build RGB Command
            // restores BOTH the command picklist and all of its argument values.
            // Editor/storage text remains Red,Green,Blue; only the Arduino
            // transport layer rotates those channels to Green,Red,Blue.
            (_r, _g, _b) = (255, 0, 0);
            string[] prefillParts = (prefillCommandText ?? "")
                .Split(',', StringSplitOptions.None)
                .Select(p => p.Trim())
                .ToArray();
            string prefillCommand = prefillParts.Length > 0
                ? Specs.Keys.FirstOrDefault(k =>
                    k.Equals(prefillParts[0], StringComparison.OrdinalIgnoreCase))
                : null;

            bool parsedTextColor = false;
            if (prefillCommand != null && Specs.TryGetValue(prefillCommand, out var prefillSpec))
            {
                int redIndex = Array.FindIndex(prefillSpec, a =>
                    a.Name.Equals("red", StringComparison.OrdinalIgnoreCase));
                int greenIndex = Array.FindIndex(prefillSpec, a =>
                    a.Name.Equals("green", StringComparison.OrdinalIgnoreCase));
                int blueIndex = Array.FindIndex(prefillSpec, a =>
                    a.Name.Equals("blue", StringComparison.OrdinalIgnoreCase));
                if (redIndex >= 0 && greenIndex >= 0 && blueIndex >= 0 &&
                    prefillParts.Length > 1 + Math.Max(redIndex, Math.Max(greenIndex, blueIndex)) &&
                    int.TryParse(prefillParts[redIndex + 1], out int pr) &&
                    int.TryParse(prefillParts[greenIndex + 1], out int pg) &&
                    int.TryParse(prefillParts[blueIndex + 1], out int pb))
                {
                    (_r, _g, _b) = (Math.Clamp(pr, 0, 255),
                                     Math.Clamp(pg, 0, 255),
                                     Math.Clamp(pb, 0, 255));
                    parsedTextColor = true;
                }
            }

            try
            {
                if (!parsedTextColor && !string.IsNullOrEmpty(prefillColorHex))
                {
                    var c = (System.Windows.Media.Color)System.Windows.Media
                        .ColorConverter.ConvertFromString(prefillColorHex);
                    (_r, _g, _b) = (c.R, c.G, c.B);
                }
            }
            catch { }

            ArgList.ItemsSource = _args;
            foreach (var cmd in Specs.Keys) CommandCombo.Items.Add(cmd);

            // Setting SelectedItem invokes CommandCombo_SelectionChanged,
            // which creates the correct argument controls for this command.
            CommandCombo.SelectedItem = prefillCommand ?? "SetRGBColor";

            // Replace the defaults with every value parsed from the existing
            // Text Value. This restores ring/side, brightness, delays, fade
            // direction, pulse counts, etc., not just the RGB channels.
            if (prefillCommand != null && Specs.TryGetValue(prefillCommand, out var existingSpec))
            {
                int count = Math.Min(existingSpec.Length, Math.Max(0, prefillParts.Length - 1));
                for (int i = 0; i < count && i < _args.Count; i++)
                    _args[i].Text = prefillParts[i + 1];
                UpdatePreview();
                UpdateColorSelector();
            }
        }

        private void CommandCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _args.Clear();
            string cmd = CommandCombo.SelectedItem as string;
            if (cmd == null || !Specs.TryGetValue(cmd, out var spec)) return;

            foreach (var (name, options, def) in spec)
            {
                string value = name switch
                {
                    "red" => _r.ToString(),
                    "green" => _g.ToString(),
                    "blue" => _b.ToString(),
                    _ => def,
                };
                var vm = new RgbArgVM { Label = name, EnumOptions = options, Text = value };
                vm.PropertyChanged += (_, _) =>
                {
                    UpdatePreview();
                    UpdateColorSelector();
                };
                _args.Add(vm);
            }
            UpdatePreview();
            UpdateColorSelector();
        }

        private string Compose()
        {
            string cmd = CommandCombo.SelectedItem as string ?? "";
            return _args.Count == 0
                ? cmd
                : cmd + "," + string.Join(",", _args.Select(a => (a.Text ?? "").Trim()));
        }

        private void UpdatePreview() => PreviewText.Text = Compose();

        private void UpdateColorSelector()
        {
            bool hasRgb = TryGetRgbArgs(out var red, out var green, out var blue);
            ColorPickerButton.Visibility = hasRgb ? Visibility.Visible : Visibility.Collapsed;
            if (!hasRgb) return;

            if (int.TryParse(red.Text, out int r) && int.TryParse(green.Text, out int g) &&
                int.TryParse(blue.Text, out int b))
            {
                var color = Color.FromRgb(
                    (byte)Math.Clamp(r, 0, 255),
                    (byte)Math.Clamp(g, 0, 255),
                    (byte)Math.Clamp(b, 0, 255));
                SelectedColorSwatch.Background = new SolidColorBrush(color);
            }
        }

        private bool TryGetRgbArgs(out RgbArgVM red, out RgbArgVM green, out RgbArgVM blue)
        {
            red = _args.FirstOrDefault(a => a.Label.Equals("red", StringComparison.OrdinalIgnoreCase));
            green = _args.FirstOrDefault(a => a.Label.Equals("green", StringComparison.OrdinalIgnoreCase));
            blue = _args.FirstOrDefault(a => a.Label.Equals("blue", StringComparison.OrdinalIgnoreCase));
            return red != null && green != null && blue != null;
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetRgbArgs(out var red, out var green, out var blue)) return;

            int.TryParse(red.Text, out int r);
            int.TryParse(green.Text, out int g);
            int.TryParse(blue.Text, out int b);
            var initial = Color.FromRgb(
                (byte)Math.Clamp(r, 0, 255),
                (byte)Math.Clamp(g, 0, 255),
                (byte)Math.Clamp(b, 0, 255));

            var picker = new RgbColorPickerWindow(initial) { Owner = this };
            if (picker.ShowDialog() != true) return;

            Color chosen = picker.SelectedColor;
            red.Text = chosen.R.ToString();
            green.Text = chosen.G.ToString();
            blue.Text = chosen.B.ToString();
            UpdatePreview();
            UpdateColorSelector();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultText = Compose();
            // Preserve ColorHex only as backward-compatible metadata. The URDF
            // preview now derives its real color from the Arduino command text.
            var args = _args.ToDictionary(a => a.Label, a => a.Text ?? "",
                                          StringComparer.OrdinalIgnoreCase);
            if (args.TryGetValue("red", out string rs) &&
                args.TryGetValue("green", out string gs) &&
                args.TryGetValue("blue", out string bs) &&
                int.TryParse(rs, out int r) && int.TryParse(gs, out int g) &&
                int.TryParse(bs, out int b))
            {
                ResultColorHex = $"#{Math.Clamp(r,0,255):X2}{Math.Clamp(g,0,255):X2}{Math.Clamp(b,0,255):X2}";
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }

    public class RgbArgVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Label { get; set; }
        public string[] EnumOptions { get; set; }
        public bool IsEnum => EnumOptions != null;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
        private string _text;
    }
}
