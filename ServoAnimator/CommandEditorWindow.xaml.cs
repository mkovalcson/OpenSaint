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
using System.IO;
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
        private readonly string _libraryCommandsFolder;

        public CommandEditorWindow(AnimationDocument doc, double time,
                                   Action<ServoSpeed, ServoNames, int> moveServoNow,
                                   Action<ServoSpeed, ServoNames, string> moveServoNowText,
                                   Action<ServoSpeed, ServoNames, RobotControls, int> moveChildNow,
                                   Action<ServoSpeed, ServoNames> configureGangSpeedNow,
                                   Action<ServoSpeed, ServoNames, RobotControls> configureChildSpeedNow,
                                   string libraryCommandsFolder,
                                   ServoCommand focusCommand = null)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "commands");
            HelpSystem.SetTopic(CmdList, "commands");
            _doc = doc;
            _timeKey = ServoCommand.TimeKey(time);
            _moveServoNow = moveServoNow;
            _moveServoNowText = moveServoNowText;
            _moveChildNow = moveChildNow;
            _configureGangSpeedNow = configureGangSpeedNow;
            _configureChildSpeedNow = configureChildSpeedNow;
            _libraryCommandsFolder = libraryCommandsFolder ?? "";

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


        /// <summary>Save every command currently shown in this Edit Commands
        /// window as one single-time-point Library Command. All saved offsets
        /// are normalized to zero so insertion always places the entire group
        /// at the selected timeline point.</summary>
        private void CreateLibraryCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show(this, "There are no commands in this window to save.",
                                "Create Library Command", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            try { Directory.CreateDirectory(_libraryCommandsFolder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not create the Library\\Commands folder:\n" + ex.Message,
                                "Create Library Command", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            var prompt = new LibraryCommandSaveWindow { Owner = this };
            if (prompt.ShowDialog() != true) return;

            string path = Path.Combine(_libraryCommandsFolder, prompt.FileNameText);
            if (File.Exists(path))
            {
                var overwrite = MessageBox.Show(this,
                    $"'{prompt.FileNameText}' already exists. Replace it?",
                    "Create Library Command", MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (overwrite != MessageBoxResult.Yes) return;
            }

            var commands = _items.Select(vm =>
            {
                var copy = vm.Command.Clone();
                copy.OffsetSeconds = 0.0;
                return copy;
            }).ToList();

            try
            {
                string imageFile = null;
                string oldImagePath = null;
                if (File.Exists(path))
                {
                    try
                    {
                        var old = AnimationDocument.LoadLibraryItem(path);
                        if (!string.IsNullOrWhiteSpace(old.ImageFile))
                        {
                            imageFile = old.ImageFile;
                            oldImagePath = Path.IsPathRooted(old.ImageFile)
                                ? old.ImageFile
                                : Path.Combine(Path.GetDirectoryName(path) ?? "", old.ImageFile);
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(prompt.ImageSourcePath))
                {
                    string ext = Path.GetExtension(prompt.ImageSourcePath);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                    imageFile = Path.GetFileNameWithoutExtension(path) + "_image" + ext.ToLowerInvariant();
                    string destination = Path.Combine(Path.GetDirectoryName(path) ?? _libraryCommandsFolder, imageFile);
                    if (!Path.GetFullPath(prompt.ImageSourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                        File.Copy(prompt.ImageSourcePath, destination, overwrite: true);

                    if (!string.IsNullOrWhiteSpace(oldImagePath) &&
                        !Path.GetFullPath(oldImagePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(oldImagePath))
                    {
                        try { File.Delete(oldImagePath); } catch { }
                    }
                }

                AnimationDocument.SaveLibraryCommand(path, commands, prompt.DescriptionText, imageFile);
                MessageBox.Show(this,
                    $"Library Command saved:\n{path}",
                    "Create Library Command", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the Library Command:\n" + ex.Message,
                                "Create Library Command", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Open the RGB command builder for this row: pick one of
        /// the RGBLight.cs commands and fill its arguments; OK writes the
        /// exact formatted text into the command's value.</summary>
        private void BuildRgb_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CommandVM vm) return;

            var builder = new RgbBuilderWindow(vm.TextValue, vm.ColorHex) { Owner = this };
            if (builder.ShowDialog() == true)
            {
                vm.TextValue = builder.ResultText;
                if (!string.IsNullOrWhiteSpace(builder.ResultColorHex))
                    vm.ColorHex = builder.ResultColorHex; // legacy file compatibility
            }
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
