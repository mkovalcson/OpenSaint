// ---------------------------------------------------------------------------
// ServoConfigWindow.xaml.cs
//
// The Servo Configuration window (menu: Config > Servo Configuration…).
// Shows every physical RobotControls channel grouped under its ganged
// ServoName header (per ServoConfiguration.GangMap - a control shared by
// two gang names, like the neck-tilt pair, appears under a combined
// header). Everything edits the SHARED ServoConfiguration instance owned
// by MainWindow, so the grid's expanded sub-rows see changes immediately.
//
// Each row: Normal/Reversed (relative to the gang), Default/Min/Max PWM
// (clamped 500..2400), the 4-element speed and accel arrays (comma text,
// "default,slow,fast,crawl"), and a verify SLIDER spanning Min..Max PWM
// that calls MoveRobotControlNow(control, pwm) on every user move so the
// physical servo can be driven to confirm the values.
//
// Load… / Save / Save As… persist the whole configuration to JSON.
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ServoAnimator
{
    public partial class ServoConfigWindow : Window
    {
        private ServoConfiguration _config;
        private readonly Action<RobotControls, int> _drive;
        private readonly Action<ServoNames, int> _driveGang;
        private readonly Action _configChanged;
        private readonly string _configFolder;
        private string _path;
        private readonly List<ServoConfigVM> _allConfigVms = new();

        public ServoConfigWindow(ServoConfiguration config,
                                 Action<RobotControls, int> driveServo,
                                 Action<ServoNames, int> driveGang,
                                 Action configChanged,
                                 string configFolder)
        {
            InitializeComponent();
            _config = config;
            _drive = driveServo;
            _driveGang = driveGang;
            _configChanged = configChanged;
            _configFolder = configFolder;
            LeftTicBox.Text = config.LeftTicSerialNumber ?? "";

            // ServoConfig.json in the configuration folder is auto-loaded
            // by the main window at startup / Set Paths; show it as the
            // current file so plain Save writes back to it.
            string autoPath = Path.Combine(configFolder ?? "", "ServoConfig.json");
            if (!string.IsNullOrEmpty(configFolder) && File.Exists(autoPath))
            {
                _path = autoPath;
                ConfigPathText.Text = Path.GetFileName(autoPath);
            }

            BuildGroups();
        }

        private void LeftTic_LostFocus(object sender, RoutedEventArgs e) =>
            _config.LeftTicSerialNumber = LeftTicBox.Text?.Trim() ?? "";

        /// <summary>
        /// Build the display groups:
        ///   * every GANGED ServoName (more than one control) is its own
        ///     group with a header SLIDER driving the whole gang like the
        ///     display grid, and per-control Direction RELATIVE to that
        ///     gang - so NeckTiltRight and NeckNodUp are separate lines
        ///     over the same two servos with independent directions.
        ///   * single servos are NOT ganged: they get no gang slider, and
        ///     related ones are grouped under plain titles (Nose, MFRC,
        ///     Whip Antenna).
        /// A control that serves two gangs (the neck pair) appears in both
        /// groups; its PWM fields edit the same underlying entry.
        /// </summary>
        private void BuildGroups()
        {
            var groups = new List<GangGroupVM>();
            _allConfigVms.Clear();

            void SharedEntryChanged(ServoConfigEntry entry, string propertyName)
            {
                // The neck pair appears under both NeckTiltRight and NeckNodUp.
                // PWM/speed/accel settings are one shared physical-channel
                // configuration; only each gang's Direction is independent.
                foreach (var vm in _allConfigVms)
                    if (ReferenceEquals(vm.Entry, entry))
                        vm.Refresh(propertyName);
            }

            void AddGang(ServoNames gang)
            {
                var controls = ServoConfiguration.ControlsFor(gang);
                var vms = new ObservableCollection<ServoConfigVM>();
                foreach (var c in controls)
                {
                    var entry = _config.Get(c);
                    if (entry == null) continue;
                    var vm = new ServoConfigVM(entry, gang, controls.Length > 1,
                                               _config, _drive, SharedEntryChanged);
                    vms.Add(vm);
                    _allConfigVms.Add(vm);
                }
                if (vms.Count == 0) return;
                var (min, max) = ServoCommand.RangeFor(gang);
                groups.Add(new GangGroupVM(gang.ToString(), vms,
                    gangServo: controls.Length > 1 ? gang : null,
                    min, max, _driveGang));
            }

            void AddTitle(string title, params ServoNames[] singles)
            {
                var vms = new ObservableCollection<ServoConfigVM>();
                foreach (var s in singles)
                    foreach (var c in ServoConfiguration.ControlsFor(s))
                    {
                        var entry = _config.Get(c);
                        if (entry == null) continue;
                        var vm = new ServoConfigVM(entry, s, false, _config, _drive,
                                                   SharedEntryChanged);
                        vms.Add(vm);
                        _allConfigVms.Add(vm);
                    }
                if (vms.Count > 0)
                    groups.Add(new GangGroupVM(title, vms, null, 0, 0, null));
            }

            // Ganged ServoNames - separate lines for the two neck gangs.
            AddGang(ServoNames.NeckTiltRight);
            AddGang(ServoNames.NeckNodUp);
            AddGang(ServoNames.FlapsOpen);
            AddGang(ServoNames.FlapTiltUp);
            AddGang(ServoNames.IrisClose);
            AddGang(ServoNames.EyesHorizontalRight);
            AddGang(ServoNames.EyesVerticalUp);
            AddGang(ServoNames.VentsOpen);

            // Single servos under plain titles.
            AddTitle("NeckTurn", ServoNames.NeckTurn);
            AddTitle("Nose", ServoNames.NoseBody, ServoNames.NoseBasket);
            AddTitle("MFRC", ServoNames.MFR_UpDown, ServoNames.MFR_Rotate);
            AddTitle("Whip Antenna", ServoNames.Whip_Antenna_RaiseLower,
                                     ServoNames.Whip_Antenna_Rotate);
            AddTitle("Microphone_RaiseLower", ServoNames.Microphone_RaiseLower);

            GroupList.ItemsSource = groups;
        }

        // -------------------- Load / Save / Save As --------------------

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Load servo configuration",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = _configFolder ?? "",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var loaded = ServoConfiguration.Load(dlg.FileName);
                // Copy EVERYTHING into the SHARED instance (MainWindow and
                // the hardware layer keep referencing the same object):
                // servo entries, the per-gang directions, and the Tic SN.
                _config.Servos = loaded.Servos;
                _config.GangDirections = loaded.GangDirections;
                _config.LeftTicSerialNumber = loaded.LeftTicSerialNumber;
                LeftTicBox.Text = _config.LeftTicSerialNumber ?? "";
                _path = dlg.FileName;
                ConfigPathText.Text = Path.GetFileName(_path);

                // Rebuild this window's rows from the loaded values, then
                // refresh everything else that uses the configuration (grid
                // sub-row ranges, gang directions, connected hardware).
                BuildGroups();
                _configChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load configuration:\n" + ex.Message,
                                "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_path)) { SaveAs_Click(sender, e); return; }
            DoSave(_path);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save servo configuration",
                Filter = "JSON files (*.json)|*.json",
                FileName = string.IsNullOrEmpty(_path) ? "ServoConfig.json"
                                                       : Path.GetFileName(_path),
                InitialDirectory = _configFolder ?? "",
            };
            if (dlg.ShowDialog() != true) return;
            DoSave(dlg.FileName);
        }

        private void DoSave(string path)
        {
            try
            {
                _config.Save(path);
                _path = path;
                ConfigPathText.Text = Path.GetFileName(path);
                _configChanged?.Invoke();   // refresh everywhere on save too
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save configuration:\n" + ex.Message,
                                "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _configChanged?.Invoke();   // grid sub-rows pick up edited ranges
            Close();
        }
    }

    /// <summary>One group: a title (gang name or plain heading), its
    /// control rows, and - for real gangs - a header slider spanning the
    /// gang's timeline range that drives every servo in it.</summary>
    public class GangGroupVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ServoNames? _gangServo;
        private readonly Action<ServoNames, int> _driveGang;

        public GangGroupVM(string name, ObservableCollection<ServoConfigVM> controls,
                           ServoNames? gangServo, int min, int max,
                           Action<ServoNames, int> driveGang)
        {
            GangName = name;
            Controls = controls;
            _gangServo = gangServo;
            _driveGang = driveGang;
            GangMin = min;
            GangMax = max;
            _gangValue = Math.Clamp(0, min, max);

            foreach (var vm in Controls)
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ServoConfigVM.SpeedsText))
                        Raise(nameof(GangSpeedsText));
                    else if (e.PropertyName == nameof(ServoConfigVM.AccelsText))
                        Raise(nameof(GangAccelsText));
                };
        }

        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string GangName { get; }
        public ObservableCollection<ServoConfigVM> Controls { get; }
        public bool HasGangSlider => _gangServo.HasValue;
        public bool HasGangSpeedSettings => _gangServo.HasValue && Controls.Count > 0;

        /// <summary>Convenience profile editor at the ganged ServoName level.
        /// Editing either field copies the four values to every physical
        /// child servo in the gang. For NeckTiltRight/NeckNodUp those child
        /// entries are the same shared objects, so both gangs stay in sync.</summary>
        public string GangSpeedsText
        {
            get => Controls.Count == 0 ? "" : Controls[0].SpeedsText;
            set
            {
                foreach (var vm in Controls) vm.SpeedsText = value;
                Raise(nameof(GangSpeedsText));
            }
        }

        public string GangAccelsText
        {
            get => Controls.Count == 0 ? "" : Controls[0].AccelsText;
            set
            {
                foreach (var vm in Controls) vm.AccelsText = value;
                Raise(nameof(GangAccelsText));
            }
        }
        public double GangMin { get; }
        public double GangMax { get; }

        /// <summary>Header slider: drives the whole gang like the display
        /// grid (through the gang-relative directions).</summary>
        public double GangValue
        {
            get => _gangValue;
            set
            {
                _gangValue = Math.Clamp(value, GangMin, GangMax);
                Raise(nameof(GangValue));
                if (_gangServo.HasValue)
                {
                    _driveGang?.Invoke(_gangServo.Value,
                                       (int)Math.Round(_gangValue));

                    // The child rows' PWM verify sliders follow: each shows
                    // the pulse width this gang value maps to for THAT
                    // servo (MapDeltatoServo with its gang-relative
                    // direction) - display only, no re-drive.
                    bool centered = GangMin < 0;
                    foreach (var vm in Controls)
                        vm.ReflectGangValue((int)Math.Round(_gangValue), centered);
                }
            }
        }
        private double _gangValue;
    }

    /// <summary>Editable view-model over one ServoConfigEntry, in the
    /// context of ONE parent gang: the Direction shown/edited is relative
    /// to that gang (per-gang for real gangs, the entry's own flag for
    /// singles), so the same control can show different directions under
    /// NeckTiltRight and NeckNodUp.</summary>
    public class ServoConfigVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly ServoConfigEntry _e;
        private readonly ServoNames _gang;
        private readonly bool _isRealGang;   // >1 control under the gang
        private readonly ServoConfiguration _config;
        private readonly Action<RobotControls, int> _drive;
        private readonly Action<ServoConfigEntry, string> _entryChanged;

        public ServoConfigVM(ServoConfigEntry entry, ServoNames gang,
                             bool isRealGang, ServoConfiguration config,
                             Action<RobotControls, int> drive,
                             Action<ServoConfigEntry, string> entryChanged)
        {
            _e = entry;
            _gang = gang;
            _isRealGang = isRealGang;
            _config = config;
            _drive = drive;
            _entryChanged = entryChanged;
            _sliderPwm = entry.DefaultPwm;
        }

        internal ServoConfigEntry Entry => _e;
        internal void Refresh(string propertyName) => Raise(propertyName);

        public string Name => _e.Control.ToString();
        public string[] DirectionOptions { get; } = { "Normal", "Reversed" };

        /// <summary>Normal/Reversed RELATIVE TO the parent gang.</summary>
        public string Direction
        {
            get => (_isRealGang ? _config.GangReversed(_gang, _e.Control)
                                : _e.Reversed)
                   ? "Reversed" : "Normal";
            set
            {
                bool rev = value == "Reversed";
                if (_isRealGang)
                    _config.SetGangReversed(_gang, _e.Control, rev);
                else
                    _e.Reversed = rev;
                Raise(nameof(Direction));
            }
        }

        public int DefaultPwm
        {
            get => _e.DefaultPwm;
            set { _e.DefaultPwm = value; _entryChanged?.Invoke(_e, nameof(DefaultPwm)); }
        }

        public int MinPwm
        {
            get => _e.MinPwm;
            set { _e.MinPwm = value; _entryChanged?.Invoke(_e, nameof(MinPwm)); Raise(nameof(SliderPwm)); }
        }

        public int MaxPwm
        {
            get => _e.MaxPwm;
            set { _e.MaxPwm = value; _entryChanged?.Invoke(_e, nameof(MaxPwm)); Raise(nameof(SliderPwm)); }
        }

        /// <summary>Speeds as "default,slow,fast,crawl". Bad input reverts.</summary>
        public string SpeedsText
        {
            get => string.Join(",", _e.Speeds);
            set
            {
                if (ParseInto(_e.Speeds, value))
                    _entryChanged?.Invoke(_e, nameof(SpeedsText));
                else
                    Raise(nameof(SpeedsText));
            }
        }

        public string AccelsText
        {
            get => string.Join(",", _e.Accels);
            set
            {
                if (ParseInto(_e.Accels, value))
                    _entryChanged?.Invoke(_e, nameof(AccelsText));
                else
                    Raise(nameof(AccelsText));
            }
        }

        private static bool ParseInto(int[] target, string text)
        {
            var parts = (text ?? "").Split(',');
            if (parts.Length != 4) return false;
            var parsed = new int[4];
            for (int i = 0; i < 4; i++)
                if (!int.TryParse(parts[i].Trim(), out parsed[i])) return false;
            Array.Copy(parsed, target, 4);
            return true;
        }

        /// <summary>Verify slider: every user move drives the servo to the
        /// selected PWM via MoveRobotControlNow(). (Gang-slider reflection
        /// updates it silently through ReflectGangValue instead.)</summary>
        public double SliderPwm
        {
            get => _sliderPwm;
            set
            {
                if (_reflecting) { SetSliderSilently(value); return; }
                _sliderPwm = Math.Clamp(value, MinPwm, MaxPwm);
                Raise(nameof(SliderPwm));
                _drive?.Invoke(_e.Control, (int)Math.Round(_sliderPwm));
            }
        }
        private double _sliderPwm;
        private bool _reflecting;

        private void SetSliderSilently(double pwm)
        {
            _sliderPwm = Math.Clamp(pwm, MinPwm, MaxPwm);
            Raise(nameof(SliderPwm));
        }

        /// <summary>Move this row's PWM slider to where the given GANG
        /// value puts this servo - the same mapping the hardware applies
        /// (MapDeltatoServo: hardware Reverse + gang-relative direction).
        /// Display only; the gang drive already moved the servo.</summary>
        public void ReflectGangValue(int gangValue, bool centered)
        {
            double outMin = _e.MinPwm, outMax = _e.MaxPwm, outHome = _e.DefaultPwm;
            double v = gangValue;
            bool gangRev = _isRealGang && _config.GangReversed(_gang, _e.Control);
            double adjusted;

            if (centered)
            {
                if (gangRev) v = -v;
                if (!_e.Reversed)
                    adjusted = v < 0 ? outHome + v / 100 * (outHome - outMin)
                                     : outHome + v / 100 * (outMax - outHome);
                else
                    adjusted = v > 0 ? outHome + v / 100 * (outMax - outHome)
                                     : outHome + v / 100 * (outHome - outMin);
            }
            else
            {
                adjusted = _e.Reversed
                    ? outMax - v / 100 * (outMax - outMin)
                    : outHome + v / 100 * (outMax - outMin);
            }

            _reflecting = true;
            try { SetSliderSilently(Math.Clamp(adjusted, outMin, outMax)); }
            finally { _reflecting = false; }
        }
    }
}
