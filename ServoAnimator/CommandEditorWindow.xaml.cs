// ---------------------------------------------------------------------------
// CommandEditorWindow.xaml.cs
//
// Modal dialog that edits every ServoCommand at one timeline point.
//
// Design:
//   * Each command is wrapped in a CommandVM whose property setters write
//     straight through to the underlying ServoCommand object. Because the
//     command objects live inside AnimationDocument.Commands, every edit is
//     immediately part of the document - MainWindow just refreshes markers
//     and the servo grid when the dialog closes.
//   * Value editing is per-servo:
//       - numeric servos: slider (range from ServoCommand.RangeFor:
//         0..2000 for LeftEyePop/RightEyePop, -100..100 otherwise) plus a
//         numeric TextBox. Every user change calls the numeric
//         MoveServoNow(Speed, Servo, int) so hardware can be jogged live.
//       - RGBCommand: a free-text box; committing text calls the text
//         MoveServoNow(Speed, Servo, string) overload.
//   * Changing the Servo combo re-ranges the slider (Min/Max change) and
//     switches between the slider and the text box automatically, clamping
//     the numeric value into the new servo's range.
//   * "Delete" removes a command from the document; if the last command at
//     this point is deleted, MainWindow's marker refresh removes the '+'.
//   * "Add Command" appends a fresh command at the same time point.
//   * Editing the Offset field moves a command to a different time point
//     (a new '+' will appear there after the dialog closes).
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ServoAnimator
{
    public partial class CommandEditorWindow : Window
    {
        private readonly AnimationDocument _doc;
        private readonly double _timeKey;    // the timeline point being edited
        private readonly Action<ServoSpeed, ServoNames, int> _moveServoNow;
        private readonly Action<ServoSpeed, ServoNames, string> _moveServoNowText;
        private readonly Action<ServoSpeed, ServoNames, RobotControls, int> _moveChildNow;
        private readonly Action<ServoSpeed, ServoNames> _configureGangSpeedNow;
        private readonly Action<ServoSpeed, ServoNames, RobotControls> _configureChildSpeedNow;
        private readonly ObservableCollection<CommandVM> _items = new();

        public CommandEditorWindow(AnimationDocument doc, double time,
                                   Action<ServoSpeed, ServoNames, int> moveServoNow,
                                   Action<ServoSpeed, ServoNames, string> moveServoNowText,
                                   Action<ServoSpeed, ServoNames, RobotControls, int> moveChildNow,
                                   Action<ServoSpeed, ServoNames> configureGangSpeedNow,
                                   Action<ServoSpeed, ServoNames, RobotControls> configureChildSpeedNow,
                                   ServoCommand focusCommand = null)
        {
            InitializeComponent();
            _doc = doc;
            _timeKey = ServoCommand.TimeKey(time);
            _moveServoNow = moveServoNow;
            _moveServoNowText = moveServoNowText;
            _moveChildNow = moveChildNow;
            _configureGangSpeedNow = configureGangSpeedNow;
            _configureChildSpeedNow = configureChildSpeedNow;

            Title = $"Edit Commands @ {_timeKey:F3} s";

            // Wrap every command currently at this time point.
            foreach (var c in _doc.Commands
                         .Where(c => ServoCommand.TimeKey(c.OffsetSeconds) == _timeKey)
                         .OrderBy(c => ReferenceEquals(c, focusCommand) ? 0 : 1)
                         .ThenBy(c => c.Servo.ToString()))
                _items.Add(new CommandVM(c, _moveServoNow, _moveServoNowText, _moveChildNow, _configureGangSpeedNow, _configureChildSpeedNow));

            CmdList.ItemsSource = _items;
        }

        /// <summary>Append a new command at the same time point and show it.</summary>
        private void AddCommand_Click(object sender, RoutedEventArgs e)
        {
            // New rows default to the TOP of the Servo picklist.
            var cmd = new ServoCommand
            {
                OffsetSeconds = _timeKey,
                Servo = CommandVM.ServoPickOptions[0].Servo,
                NumericValue = 0,
                Speed = ServoSpeed.NoChange,
            };
            _doc.Commands.Add(cmd);
            _items.Add(new CommandVM(cmd, _moveServoNow, _moveServoNowText, _moveChildNow, _configureGangSpeedNow, _configureChildSpeedNow));
        }

        /// <summary>Remove one command from the document and from the list.</summary>
        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CommandVM vm) return;
            _doc.Commands.Remove(vm.Command);
            _items.Remove(vm);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Open the RGB command builder for this row: pick one of
        /// the RGBLight.cs commands and fill its arguments; OK writes the
        /// exact formatted text into the command's value.</summary>
        private void BuildRgb_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CommandVM vm) return;

            var builder = new RgbBuilderWindow(vm.ColorHex) { Owner = this };
            if (builder.ShowDialog() == true)
                vm.TextValue = builder.ResultText;
        }

        /// <summary>A palette swatch was clicked: store the 24-bit color on
        /// the row's command and close the popup. The swatch's Tag carries
        /// the row VM (bound in XAML); its Background is the color.</summary>
        private void PaletteSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.Tag is not CommandVM vm) return;
            if ((fe as System.Windows.Controls.Border)?.Background
                    is not SolidColorBrush brush) return;

            var c = brush.Color;
            vm.ColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

            // Close the popup containing the swatch.
            DependencyObject d = fe;
            while (d != null && d is not Popup)
                d = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
            if (d is Popup p) p.IsOpen = false;
        }
    }

    /// <summary>
    /// View-model wrapper for one ServoCommand row in the editor. Setters
    /// write through to the wrapped command immediately.
    /// </summary>
    public class CommandVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ServoCommand Command { get; }
        private readonly Action<ServoSpeed, ServoNames, int> _moveServoNow;
        private readonly Action<ServoSpeed, ServoNames, string> _moveServoNowText;
        private readonly Action<ServoSpeed, ServoNames, RobotControls, int> _moveChildNow;
        private readonly Action<ServoSpeed, ServoNames> _configureGangSpeedNow;
        private readonly Action<ServoSpeed, ServoNames, RobotControls> _configureChildSpeedNow;
        private readonly bool _initializing = true;

        // ---- merged Servo picklist ----
        // Display Grid order: each ganged ServoName followed by its child
        // servos prefixed " – ". Picking a child sets the command's
        // Servo (the parent - supplying the range) AND its Control target.
        // BothEyePop's children are LeftEyePop/RightEyePop, which appear as
        // their own entries right after it, so it gets no child items. The
        // Play pseudo-servo is export-only and never listed.
        public static List<ServoPickItem> ServoPickOptions { get; } = BuildPickOptions();

        private static List<ServoPickItem> BuildPickOptions()
        {
            var list = new List<ServoPickItem>();

            void Add(ServoNames servo, bool withChildren = true)
            {
                list.Add(new ServoPickItem(servo, null));
                if (!withChildren) return;
                var controls = ServoConfiguration.ControlsFor(servo);
                if (controls.Length > 1)
                    foreach (var c in controls)
                        list.Add(new ServoPickItem(servo, c));
            }

            // Display Grid order, with the eye pops moved to just above
            // RGBCommand, and RGBCommand at the very bottom.
            Add(ServoNames.FlapsOpen);
            Add(ServoNames.FlapTiltUp);
            Add(ServoNames.IrisClose);
            Add(ServoNames.EyesVerticalUp);
            Add(ServoNames.EyesHorizontalRight);
            Add(ServoNames.NeckTurn);
            Add(ServoNames.NeckNodUp);
            Add(ServoNames.NeckTiltRight);
            Add(ServoNames.NoseBasket);
            Add(ServoNames.NoseBody);
            Add(ServoNames.VentsOpen);
            Add(ServoNames.Microphone_RaiseLower);
            Add(ServoNames.Whip_Antenna_RaiseLower);
            Add(ServoNames.Whip_Antenna_Rotate);
            Add(ServoNames.MFR_UpDown);
            Add(ServoNames.MFR_Rotate);
            Add(ServoNames.BothEyePop, withChildren: false);
            Add(ServoNames.LeftEyePop);
            Add(ServoNames.RightEyePop);
            Add(ServoNames.RGBCommand);
            return list;
        }

        /// <summary>The picklist selection mapped onto the command's
        /// Servo + Control pair.</summary>
        public ServoPickItem SelectedServoItem
        {
            get => ServoPickOptions.FirstOrDefault(i =>
                       i.Servo == Command.Servo && i.Control == Command.Control)
                   ?? ServoPickOptions.FirstOrDefault(i =>
                       i.Servo == Command.Servo && i.Control == null);
            set
            {
                if (value == null) return;
                Servo = value.Servo;            // re-ranges + clears bad Control
                Command.Control = value.Control;
                Raise(nameof(SelectedServoItem));
            }
        }


        /// <summary>The 24-bit color palette (shared by every RGB row):
        /// a grayscale ramp plus 7 hues x 7 lightness steps.</summary>
        public static List<SolidColorBrush> PaletteBrushes { get; } = BuildPalette();

        private static List<SolidColorBrush> BuildPalette()
        {
            var list = new List<SolidColorBrush>();
            for (int i = 0; i < 8; i++)                      // grayscale row
            {
                byte g = (byte)(i * 255 / 7);
                list.Add(new SolidColorBrush(Color.FromRgb(g, g, g)));
            }
            double[] hues = { 0, 30, 60, 120, 180, 240, 300 };
            double[] lts = { 0.20, 0.32, 0.44, 0.56, 0.68, 0.80, 0.90, 0.96 };
            foreach (double h in hues)
                foreach (double l in lts)
                    list.Add(new SolidColorBrush(HslToRgb(h, 1.0, l)));
            foreach (var b in list) b.Freeze();
            return list;
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            double m = l - c / 2;
            (double r, double g, double b) = h switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x),
            };
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255),
                                 (byte)((b + m) * 255));
        }

        public CommandVM(ServoCommand command,
                         Action<ServoSpeed, ServoNames, int> moveServoNow,
                         Action<ServoSpeed, ServoNames, string> moveServoNowText,
                         Action<ServoSpeed, ServoNames, RobotControls, int> moveChildNow,
                         Action<ServoSpeed, ServoNames> configureGangSpeedNow,
                         Action<ServoSpeed, ServoNames, RobotControls> configureChildSpeedNow)
        {
            Command = command;
            _moveServoNow = moveServoNow;
            _moveServoNowText = moveServoNowText;
            _moveChildNow = moveChildNow;
            _configureGangSpeedNow = configureGangSpeedNow;
            _configureChildSpeedNow = configureChildSpeedNow;
            _initializing = false;
        }

        // ---- per-servo value shape ------------------------------------

        /// <summary>Slider range for the currently selected servo.</summary>
        public double Min => ServoCommand.RangeFor(Command.Servo).Min;
        public double Max => ServoCommand.RangeFor(Command.Servo).Max;

        /// <summary>True when the selected servo takes a text value
        /// (RGBCommand): the row shows a text box instead of the slider.</summary>
        public bool IsTextServo => Command.IsTextServo;

        public bool SupportsSpeed =>
            !Command.IsTextServo &&
            Command.Servo != ServoNames.LeftEyePop &&
            Command.Servo != ServoNames.RightEyePop &&
            Command.Servo != ServoNames.BothEyePop &&
            Command.Servo != ServoNames.Play;

        // ---- editable fields -------------------------------------------

        /// <summary>Offset as editable text; a valid non-negative number moves
        /// the command to that point on the timeline.</summary>
        public string OffsetText
        {
            get => Command.OffsetSeconds.ToString("F3", CultureInfo.InvariantCulture);
            set
            {
                if (double.TryParse(value, NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out double t) && t >= 0)
                    Command.OffsetSeconds = ServoCommand.TimeKey(t);
                Raise(nameof(OffsetText));   // re-format (or revert bad input)
            }
        }

        /// <summary>Changing the servo re-ranges the slider, switches between
        /// slider/text UI, clamps the numeric value into the new range, and
        /// refreshes the individual-control options for the new gang.</summary>
        public ServoNames Servo
        {
            get => Command.Servo;
            set
            {
                Command.Servo = value;
                Command.ClampToRange();

                // If the old individual-control target doesn't belong to the
                // new servo's gang, revert to driving the whole gang.
                if (Command.Control.HasValue &&
                    !ServoConfiguration.ControlsFor(value).Contains(Command.Control.Value))
                    Command.Control = null;

                Raise(nameof(Servo));
                Raise(nameof(Min));
                Raise(nameof(Max));
                Raise(nameof(IsTextServo));
                Raise(nameof(SupportsSpeed));
                Raise(nameof(Value));
                Raise(nameof(SelectedServoItem));
            }
        }

        /// <summary>Numeric value (slider + text box). Setting it jogs the
        /// hardware/head live: the whole gang for a gang selection, or just
        /// the one child servo when an individual control is targeted.</summary>
        public int Value
        {
            get => Command.NumericValue;
            set
            {
                Command.NumericValue = value;
                Command.ClampToRange();
                Raise(nameof(Value));
                if (Command.Control.HasValue)
                    _moveChildNow?.Invoke(Command.Speed, Command.Servo,
                                          Command.Control.Value, Command.NumericValue);
                else
                    _moveServoNow?.Invoke(Command.Speed, Command.Servo,
                                          Command.NumericValue);
            }
        }

        /// <summary>Text value (RGBCommand): jogs the RGB hardware live.</summary>
        public string TextValue
        {
            get => Command.TextValue;
            set
            {
                Command.TextValue = value ?? "";
                Raise(nameof(TextValue));
                _moveServoNowText?.Invoke(Command.Speed, Command.Servo,
                                          Command.TextValue);
            }
        }

        /// <summary>Palette color ("#RRGGBB"). Setting it updates the color
        /// button and the R/G/B readout, and persists with the project.</summary>
        public string ColorHex
        {
            get => Command.ColorHex;
            set
            {
                Command.ColorHex = value ?? "";
                Raise(nameof(ColorHex));
                Raise(nameof(ColorBrush));
                Raise(nameof(RgbText));
            }
        }

        public Brush ColorBrush
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(Command.ColorHex))
                        return (Brush)new BrushConverter()
                            .ConvertFromString(Command.ColorHex);
                }
                catch { }
                return Brushes.Black;
            }
        }

        /// <summary>The 0-255 red/green/blue readout for the chosen color.</summary>
        public string RgbText
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(Command.ColorHex))
                    {
                        var c = (Color)ColorConverter.ConvertFromString(Command.ColorHex);
                        return $"R:{c.R} G:{c.G} B:{c.B}";
                    }
                }
                catch { }
                return "R:0 G:0 B:0";
            }
        }

        /// <summary>Command speed is optional.  N/C means this command
        /// changes position only and leaves the Maestro's current speed and
        /// acceleration profile untouched.</summary>
        public string[] SpeedOptions { get; } =
            { "N/C", "Default", "Fast", "Slow", "Crawl" };

        public string SpeedText
        {
            get => ServoCommand.SpeedToText(Command.Speed);
            set
            {
                if (!ServoCommand.TryParseSpeed(value, out var speed))
                    speed = ServoSpeed.NoChange;
                if (Command.Speed == speed) { Raise(nameof(SpeedText)); return; }

                Command.Speed = speed;
                Raise(nameof(SpeedText));

                // Editing the Speed column can configure the physical robot
                // immediately in Live Drive without moving the servo. For a
                // ganged command every Maestro child receives the profile.
                if (!_initializing && speed != ServoSpeed.NoChange && SupportsSpeed && !Command.Disable)
                {
                    if (Command.Control.HasValue)
                        _configureChildSpeedNow?.Invoke(speed, Command.Servo, Command.Control.Value);
                    else
                        _configureGangSpeedNow?.Invoke(speed, Command.Servo);
                }
            }
        }

        /// <summary>Disable checkbox: a disabled command turns its servo(s)
        /// OFF (PWM disabled) instead of moving them, and exports with the
        /// literal string "Disable" in its value field - for both ganged
        /// and child-servo commands.</summary>
        public bool Disable
        {
            get => Command.Disable;
            set { Command.Disable = value; Raise(nameof(Disable)); }
        }

        public string Reason
        {
            get => Command.Reason;
            set { Command.Reason = value ?? ""; Raise(nameof(Reason)); }
        }
    }

    /// <summary>One entry of the merged Servo picklist: a ganged ServoName
    /// (Control == null) or one of its child servos (" – " prefix).</summary>
    public class ServoPickItem
    {
        public ServoNames Servo { get; }
        public RobotControls? Control { get; }
        private readonly string _display;

        public ServoPickItem(ServoNames servo, RobotControls? control)
        {
            Servo = servo;
            Control = control;
            _display = control.HasValue ? " – " + control.Value : servo.ToString();
        }

        public override string ToString() => _display;
    }
}
