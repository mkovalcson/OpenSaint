// ---------------------------------------------------------------------------
// RgbBuilderWindow.xaml.cs
//
// Builds RGBCommand text values that match the string.Format outputs in
// RGBLight.cs EXACTLY - same command word, same argument order:
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

namespace ServoAnimator
{
    public partial class RgbBuilderWindow : Window
    {
        /// <summary>The generated command text (valid after OK).</summary>
        public string ResultText { get; private set; } = "";

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

        public RgbBuilderWindow(string prefillColorHex = "")
        {
            InitializeComponent();

            // Prefill r/g/b from the command's palette color when one is set.
            (_r, _g, _b) = (255, 0, 0);
            try
            {
                if (!string.IsNullOrEmpty(prefillColorHex))
                {
                    var c = (System.Windows.Media.Color)System.Windows.Media
                        .ColorConverter.ConvertFromString(prefillColorHex);
                    (_r, _g, _b) = (c.R, c.G, c.B);
                }
            }
            catch { }

            ArgList.ItemsSource = _args;
            foreach (var cmd in Specs.Keys) CommandCombo.Items.Add(cmd);
            CommandCombo.SelectedIndex = 2;   // SetRGBColor
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
                vm.PropertyChanged += (_, _) => UpdatePreview();
                _args.Add(vm);
            }
            UpdatePreview();
        }

        private string Compose()
        {
            string cmd = CommandCombo.SelectedItem as string ?? "";
            return _args.Count == 0
                ? cmd
                : cmd + "," + string.Join(",", _args.Select(a => (a.Text ?? "").Trim()));
        }

        private void UpdatePreview() => PreviewText.Text = Compose();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultText = Compose();
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
