// ---------------------------------------------------------------------------
// ServoStateRow.cs
//
// View-model for one row of the servo status grid shown above the waveform.
// There is exactly one row per ServoNames value. Each row displays the *last*
// command applied to that servo at/before the current cursor/playback time:
//     ServoName | OffsetSeconds | Speed | Value | Slider(range) | TextBox
//
// Value handling differs per servo:
//   * Numeric servos: slider + numeric box, range from ServoCommand.RangeFor
//     (EyePop = 0..2000; NoseBasket and other positive controls = 0..100;
//      centered controls = -100..+100).
//   * RGBCommand (IsTextRow): the slider is replaced by a text box showing
//     the last command text used.
//
// The editors are enabled only while "Live Drive" is on; the actual
// MoveServoNow() call is wired up in MainWindow's event handlers.
// ---------------------------------------------------------------------------

using System.ComponentModel;

namespace ServoAnimator
{
    public class ServoStateRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ServoStateRow(ServoNames servo)
        {
            Servo = servo;
            var (mn, mx) = ServoCommand.RangeFor(servo);
            Min = mn;
            Max = mx;
        }

        /// <summary>Which servo this row represents (fixed).</summary>
        public ServoNames Servo { get; }
        public string ServoName => Servo.ToString();

        /// <summary>Slider range for this servo (fixed per servo).</summary>
        public double Min { get; }
        public double Max { get; }

        /// <summary>True for servos whose value is a text string (RGBCommand):
        /// the row shows a text box instead of a slider.</summary>
        public bool IsTextRow => ServoCommand.IsTextValued(Servo);

        /// <summary>Offset of the last command applied, or null if no command
        /// has occurred yet at the current time.</summary>
        public double? Offset
        {
            get => _offset;
            set { _offset = value; Raise(nameof(OffsetText)); }
        }
        private double? _offset;
        public string OffsetText => _offset.HasValue ? _offset.Value.ToString("F3") : "—";

        /// <summary>Speed of the last command applied (Default when none).
        /// In Live Drive the picklist edits this directly and the choice is
        /// pushed to every servo in the row's gang.</summary>
        public ServoSpeed Speed
        {
            get => _speed;
            set { _speed = value; Raise(nameof(SpeedText)); Raise(nameof(Speed)); }
        }
        private ServoSpeed _speed = ServoSpeed.Default;
        public string SpeedText => _speed.ToString();

        /// <summary>Speed picklist options, in display order.</summary>
        public ServoSpeed[] SpeedOptions { get; } =
            { ServoSpeed.Default, ServoSpeed.Fast, ServoSpeed.Slow, ServoSpeed.Crawl };

        /// <summary>False for servos with no speed concept (RGBCommand and
        /// the Tic-driven eye pops): their Speed column stays empty.</summary>
        public bool ShowSpeed =>
            Servo != ServoNames.RGBCommand && Servo != ServoNames.BothEyePop &&
            Servo != ServoNames.LeftEyePop && Servo != ServoNames.RightEyePop;

        /// <summary>Speed picklist enabled state. Editing is available whether or not physical Live Drive is enabled.</summary>
        public bool SpeedEnabled
        {
            get => _speedEnabled;
            set { _speedEnabled = value; Raise(nameof(SpeedEnabled)); }
        }
        private bool _speedEnabled = true;

        /// <summary>Current numeric value, clamped to this servo's range.
        /// Bound two-way to the slider and the numeric TextBox.
        /// Defaults to 0 until a command is reached.</summary>
        public double Value
        {
            get => _value;
            set
            {
                double v = Math.Clamp(value, Min, Max);
                if (Math.Abs(v - _value) < double.Epsilon) return;
                _value = v;
                Raise(nameof(Value));
                Raise(nameof(ValueText));
            }
        }
        private double _value;

        /// <summary>Palette color of the last RGBCommand command ("#RRGGBB",
        /// empty = none). The grid's Value column shows this as a small
        /// color box instead of a number for the RGBCommand row.</summary>
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                _colorHex = value ?? "";
                Raise(nameof(ColorHex));
                Raise(nameof(ColorBrush));
            }
        }
        private string _colorHex = "";

        /// <summary>Brush for the color box (black when no color chosen).</summary>
        public System.Windows.Media.Brush ColorBrush
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(_colorHex))
                        return (System.Windows.Media.Brush)new System.Windows.Media
                            .BrushConverter().ConvertFromString(_colorHex);
                }
                catch { }
                return System.Windows.Media.Brushes.Black;
            }
        }

        /// <summary>Last command text for RGBCommand rows (two-way bound to
        /// the row's text box).</summary>
        public string TextValue
        {
            get => _textValue;
            set
            {
                _textValue = value ?? "";
                Raise(nameof(TextValue));
                Raise(nameof(ValueText));
            }
        }
        private string _textValue = "";

        /// <summary>Compact value shown in the "Value" column: the number for
        /// numeric servos, the last command text for RGBCommand.</summary>
        public string ValueText =>
            IsTextRow ? _textValue : ((int)Math.Round(_value)).ToString();

        /// <summary>Enables the slider/TextBoxes. UI editing and URDF preview are independent of physical Live Drive.</summary>
        public bool SliderEnabled
        {
            get => _sliderEnabled;
            set { _sliderEnabled = value; Raise(nameof(SliderEnabled)); }
        }
        private bool _sliderEnabled = true;

        /// <summary>Set while one of this row's editors (slider, value box,
        /// or RGB text box) has keyboard focus. UpdateServoGrid() skips rows
        /// being edited in Live Drive so playback refreshes don't overwrite
        /// what the user is typing/dragging.</summary>
        public bool IsEditing { get; set; }


        /// <summary>Functional group used by the UI header/collapse affordance.</summary>
        public string GroupName { get; set; } = "";
        /// <summary>Header text shown only on the first row in each group.</summary>
        public string GroupHeader { get; set; } = "";
        public bool GroupCollapsed
        {
            get => _groupCollapsed;
            set
            {
                if (_groupCollapsed == value) return;
                _groupCollapsed = value;
                Raise(nameof(GroupCollapsed));
                Raise(nameof(RowVisibility));
            }
        }
        private bool _groupCollapsed;
        public System.Windows.Visibility RowVisibility =>
            GroupCollapsed ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        /// <summary>True to draw a group-separator line under this row in the
        /// two-column grid layout (set once by BuildServoGridColumns()).</summary>
        public bool DividerBelow { get; set; }

        /// <summary>True to draw a thin divider inside a larger collapsible
        /// group (used between MFR, Whip Antenna and Microphone inside
        /// Headtop Controls).</summary>
        public bool SubDividerBelow { get; set; }

        // ---- [+/-] expander exposing the ganged RobotControls ----

        /// <summary>The physical RobotControls this ServoName gangs together
        /// (from ServoConfiguration.GangMap; rebuilt when the servo
        /// configuration changes).</summary>
        public System.Collections.ObjectModel.ObservableCollection<RobotControlRow>
            Children { get; } = new();

        public bool HasChildren => Children.Count > 0;
        public void RaiseHasChildren() => Raise(nameof(HasChildren));

        /// <summary>[+/-] state: true shows the RobotControl sub-rows.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; Raise(nameof(IsExpanded)); }
        }
        private bool _isExpanded;

        /// <summary>"Spline" checkbox state. When checked, this servo's
        /// command points are interpolated with a Cubic Hermite spline, the
        /// curve is drawn in the spline area below the waveform, and saving
        /// generates sampled commands along the curve. Not available for
        /// text-valued servos (RGBCommand - the checkbox is hidden).</summary>
        public bool SplineEnabled
        {
            get => _splineEnabled;
            set { _splineEnabled = value; Raise(nameof(SplineEnabled)); }
        }
        private bool _splineEnabled;
    }

    /// <summary>
    /// One RobotControl sub-row under an expanded ganged ServoName in the
    /// grid: the control's name and a slider with the SAME RANGE AS THE
    /// PARENT ganged servo (-100..100 or 0..100). In Live Drive the slider
    /// drives just that physical servo through MapDeltatoServo (with the
    /// control's gang-relative direction). On the timeline, an individual
    /// control's command overrides the gang for just that servo until the
    /// next ganged command.
    /// </summary>
    public class RobotControlRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public RobotControlRow(ServoConfigEntry entry, ServoNames parent)
        {
            Entry = entry;
            Parent = parent;
            (Min, Max) = ServoCommand.RangeFor(parent);
            _value = Math.Clamp(0, Min, Max);
        }

        public ServoConfigEntry Entry { get; }
        public RobotControls Control => Entry.Control;

        /// <summary>The ganged ServoName this control sits under - supplies
        /// the range and the gang-relative direction.</summary>
        public ServoNames Parent { get; }

        public string Name => Entry.Control.ToString();
        public double Min { get; }
        public double Max { get; }

        /// <summary>Slider value in the PARENT's range.</summary>
        public double Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, Min, Max);
                Raise(nameof(Value));
            }
        }
        private double _value;

        public bool SliderEnabled
        {
            get => _sliderEnabled;
            set { _sliderEnabled = value; Raise(nameof(SliderEnabled)); }
        }
        private bool _sliderEnabled = true;
    }
}
