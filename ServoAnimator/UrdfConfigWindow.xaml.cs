// ---------------------------------------------------------------------------
// UrdfConfigWindow.xaml.cs
// ---------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ServoAnimator
{
    public partial class UrdfConfigWindow : Window
    {
        private readonly UrdfConfiguration _config;
        private readonly ServoConfiguration _servoConfig;
        private readonly Action<ServoNames, double> _previewGang;
        private readonly Action<ServoNames, RobotControls, double> _previewChild;
        private readonly Action _zeroFlaps;
        private readonly string _defaultPath;
        private readonly Action _saved;
        private readonly Action _restored;
        private readonly UrdfConfiguration _openingConfig;
        private readonly ObservableCollection<UrdfCalibrationSectionVM> _groups = new();
        private bool _syncingAudioLedGain;
        private bool _syncingLightIntensity;
        private static double _sessionScrollOffset;

        public UrdfConfigWindow(UrdfConfiguration config,
                                ServoConfiguration servoConfig,
                                Action<ServoNames, double> previewGang,
                                Action<ServoNames, RobotControls, double> previewChild,
                                Action zeroFlaps,
                                string configFolder,
                                Action saved = null,
                                Action restored = null)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "urdf-configuration");
            HelpSystem.SetTopic(GroupList, "urdf-configuration");
            HelpSystem.SetTopic(AudioLedGainSlider, "rgb-lighting");
            HelpSystem.SetTopic(AudioLedGainText, "rgb-lighting");
            HelpSystem.SetTopic(EyeLightIntensitySlider, "rgb-lighting");
            HelpSystem.SetTopic(EyeLightIntensityText, "rgb-lighting");
            HelpSystem.SetTopic(VentLightIntensitySlider, "rgb-lighting");
            HelpSystem.SetTopic(VentLightIntensityText, "rgb-lighting");
            HelpSystem.SetTopic(ConfigPathText, "files-configuration");
            _config = config;
            _servoConfig = servoConfig;
            _previewGang = previewGang;
            _previewChild = previewChild;
            _zeroFlaps = zeroFlaps;
            _saved = saved;
            _restored = restored;
            _openingConfig = UrdfConfiguration.CreateDefault();
            _openingConfig.CopyFrom(config);
            _defaultPath = Path.Combine(
                string.IsNullOrWhiteSpace(configFolder) ? AppContext.BaseDirectory : configFolder,
                "URDFconfig.json");
            ConfigPathText.Text = "Default: " + _defaultPath;
            SyncAudioLedGainControls();
            SyncLightIntensityControls();
            BuildGroups();
            Loaded += (_, _) => MainScroll.ScrollToVerticalOffset(_sessionScrollOffset);
            Closed += (_, _) => _sessionScrollOffset = MainScroll.VerticalOffset;
        }

        /// <summary>
        /// Mirror the Servo Configuration screen organization. Normal logical
        /// gangs share Min/Max/Test Position. FlapsOpen is deliberately split
        /// into upper and lower logical/test gangs, while every physical flap
        /// retains its own Min/Max/Zero calibration and Direction.
        /// </summary>
        private void BuildGroups()
        {
            _groups.Clear();

            UrdfMotionGroupVM MakeServo(ServoNames servo)
            {
                var settings = _config.SettingsFor(servo).ToList();
                if (settings.Count == 0) return null;
                return new UrdfMotionGroupVM(servo, settings, _servoConfig,
                    _previewGang, _previewChild, showZeroAdjustments: true);
            }

            void AddGang(ServoNames servo)
            {
                var vm = MakeServo(servo);
                if (vm == null) return;
                _groups.Add(new UrdfCalibrationSectionVM(servo.ToString(), new[] { vm }));
            }

            void AddFlapsOpen()
            {
                var all = _config.SettingsFor(ServoNames.FlapsOpen).ToList();
                var upperControls = new[]
                {
                    RobotControls.BrowLeftTopOpen,
                    RobotControls.BrowRightTopOpen,
                };
                var lowerControls = new[]
                {
                    RobotControls.BrowLeftBottomOpen,
                    RobotControls.BrowRightBottomOpen,
                };

                var upper = all.Where(x => upperControls.Contains(x.Control)).ToList();
                var lower = all.Where(x => lowerControls.Contains(x.Control)).ToList();
                var rows = new List<UrdfMotionGroupVM>();

                if (upper.Count > 0)
                    rows.Add(new UrdfMotionGroupVM(
                        ServoNames.FlapsOpen, upper, _servoConfig,
                        _previewGang, _previewChild,
                        displayName: "Upper Flaps Open/Close",
                        previewChildrenOnly: true,
                        showZeroAdjustments: true,
                        individualRanges: true));

                if (lower.Count > 0)
                    rows.Add(new UrdfMotionGroupVM(
                        ServoNames.FlapsOpen, lower, _servoConfig,
                        _previewGang, _previewChild,
                        displayName: "Lower Flaps Open/Close",
                        previewChildrenOnly: true,
                        showZeroAdjustments: true,
                        individualRanges: true));

                if (rows.Count > 0)
                    _groups.Add(new UrdfCalibrationSectionVM("FlapsOpen", rows));
            }

            void AddTitle(string title, params ServoNames[] servos)
            {
                var rows = servos.Select(s => MakeServo(s)).Where(x => x != null).ToList();
                if (rows.Count > 0)
                    _groups.Add(new UrdfCalibrationSectionVM(title, rows));
            }

            AddGang(ServoNames.NeckTiltRight);
            AddGang(ServoNames.NeckNodUp);
            AddFlapsOpen();
            AddGang(ServoNames.FlapTiltUp);
            AddGang(ServoNames.IrisClose);
            AddGang(ServoNames.EyesHorizontalRight);
            AddGang(ServoNames.EyesVerticalUp);
            AddGang(ServoNames.VentsOpen);

            AddTitle("NeckTurn", ServoNames.NeckTurn);
            AddTitle("Nose", ServoNames.NoseBody, ServoNames.NoseBasket);
            AddTitle("MFRC", ServoNames.MFR_UpDown, ServoNames.MFR_Rotate);
            AddTitle("Whip Antenna", ServoNames.Whip_Antenna_RaiseLower,
                                      ServoNames.Whip_Antenna_Rotate);
            AddTitle("Microphone_RaiseLower", ServoNames.Microphone_RaiseLower);
            AddTitle("Eye Pop", ServoNames.LeftEyePop, ServoNames.RightEyePop);

            GroupList.ItemsSource = _groups;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _config.CopyFrom(_openingConfig);
            SyncAudioLedGainControls();
            SyncLightIntensityControls();
            BuildGroups();
            _restored?.Invoke();
            ConfigPathText.Text = "Restored values from when URDF Configuration was opened. Save Default to persist.";
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            _config.CopyFrom(UrdfConfiguration.CreateDefault());
            SyncAudioLedGainControls();
            SyncLightIntensityControls();
            BuildGroups();
        }

        private void ZeroFlaps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _zeroFlaps?.Invoke();
                BuildGroups();
                ConfigPathText.Text = "Flap zero points updated from the current NoseBody/NoseBasket grid pose. Save Default to persist.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not zero the flap calibration:\n" + ex.Message,
                    "URDF Configuration", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config.Save(_defaultPath);
                _saved?.Invoke();
                ConfigPathText.Text = "Saved + applied: " + _defaultPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save URDFconfig.json:\n" + ex.Message,
                    "URDF Configuration", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ReloadFromSharedConfig()
        {
            SyncAudioLedGainControls();
            SyncLightIntensityControls();
            BuildGroups();
        }

        public void RefreshInheritedDirections()
        {
            foreach (var section in _groups)
                foreach (var servo in section.Servos)
                    servo.RefreshDirections();
        }

        private void SyncAudioLedGainControls()
        {
            if (AudioLedGainSlider == null || AudioLedGainText == null) return;
            _syncingAudioLedGain = true;
            try
            {
                double gain = Math.Clamp(_config?.AudioLedGain ?? 1.0, 0.5, 2.0);
                AudioLedGainSlider.Value = gain;
                AudioLedGainText.Text = gain.ToString("0.00", CultureInfo.InvariantCulture);
            }
            finally
            {
                _syncingAudioLedGain = false;
            }
        }

        private void AudioLedGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingAudioLedGain || _config == null) return;
            double gain = Math.Clamp(e.NewValue, 0.5, 2.0);
            _config.AudioLedGain = gain;

            _syncingAudioLedGain = true;
            try
            {
                AudioLedGainText.Text = gain.ToString("0.00", CultureInfo.InvariantCulture);
            }
            finally
            {
                _syncingAudioLedGain = false;
            }
        }

        private void CommitAudioLedGainText()
        {
            if (_syncingAudioLedGain || _config == null || AudioLedGainText == null) return;
            string text = AudioLedGainText.Text?.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
                          double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!parsed)
            {
                SyncAudioLedGainControls();
                return;
            }

            value = Math.Clamp(value, 0.5, 2.0);
            _config.AudioLedGain = value;
            SyncAudioLedGainControls();
        }

        private void AudioLedGainText_LostFocus(object sender, RoutedEventArgs e) =>
            CommitAudioLedGainText();

        private void AudioLedGainText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitAudioLedGainText();
            AudioLedGainText.SelectAll();
            e.Handled = true;
        }

        private void SyncLightIntensityControls()
        {
            if (EyeLightIntensitySlider == null || EyeLightIntensityText == null ||
                VentLightIntensitySlider == null || VentLightIntensityText == null) return;

            _syncingLightIntensity = true;
            try
            {
                double eye = Math.Clamp(_config?.EyeLightIntensity ?? 1.0, 1.0, 20.0);
                double vent = Math.Clamp(_config?.VentLightIntensity ?? 1.0, 1.0, 20.0);
                EyeLightIntensitySlider.Value = eye;
                EyeLightIntensityText.Text = eye.ToString("0.0", CultureInfo.InvariantCulture);
                VentLightIntensitySlider.Value = vent;
                VentLightIntensityText.Text = vent.ToString("0.0", CultureInfo.InvariantCulture);
            }
            finally
            {
                _syncingLightIntensity = false;
            }
        }

        private void EyeLightIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingLightIntensity || _config == null) return;
            _config.EyeLightIntensity = Math.Clamp(e.NewValue, 1.0, 20.0);
            SyncLightIntensityControls();
        }

        private void VentLightIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingLightIntensity || _config == null) return;
            _config.VentLightIntensity = Math.Clamp(e.NewValue, 1.0, 20.0);
            SyncLightIntensityControls();
        }

        private void CommitLightIntensityText(TextBox textBox, bool eye)
        {
            if (_syncingLightIntensity || _config == null || textBox == null) return;
            string text = textBox.Text?.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
                          double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!parsed)
            {
                SyncLightIntensityControls();
                return;
            }

            value = Math.Clamp(value, 1.0, 20.0);
            if (eye) _config.EyeLightIntensity = value;
            else _config.VentLightIntensity = value;
            SyncLightIntensityControls();
        }

        private void EyeLightIntensityText_LostFocus(object sender, RoutedEventArgs e) =>
            CommitLightIntensityText(EyeLightIntensityText, eye: true);

        private void VentLightIntensityText_LostFocus(object sender, RoutedEventArgs e) =>
            CommitLightIntensityText(VentLightIntensityText, eye: false);

        private void EyeLightIntensityText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitLightIntensityText(EyeLightIntensityText, eye: true);
            EyeLightIntensityText.SelectAll();
            e.Handled = true;
        }

        private void VentLightIntensityText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitLightIntensityText(VentLightIntensityText, eye: false);
            VentLightIntensityText.SelectAll();
            e.Handled = true;
        }

        private void ExtentTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox box) return;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            box.SelectAll();
            e.Handled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    public sealed class UrdfCalibrationSectionVM
    {
        public UrdfCalibrationSectionVM(string groupName, IEnumerable<UrdfMotionGroupVM> servos)
        {
            GroupName = groupName;
            Servos = new ObservableCollection<UrdfMotionGroupVM>(servos);
        }

        public string GroupName { get; }
        public ObservableCollection<UrdfMotionGroupVM> Servos { get; }
    }

    /// <summary>One logical URDF calibration gang. Test Position is shared by
    /// the settings in this row. Normal gangs share Min/Max while every physical
    /// URDF child has an independent Zero Point and Direction. FlapsOpen additionally
    /// keeps independent Min/Max calibration per physical child while upper/lower
    /// pairs remain ganged for test movement.</summary>
    public sealed class UrdfMotionGroupVM : INotifyPropertyChanged
    {
        private readonly ServoNames _servo;
        private readonly List<UrdfMotionSetting> _settings;
        private readonly Action<ServoNames, double> _previewGang;
        private readonly Action<ServoNames, RobotControls, double> _previewChild;
        private readonly bool _previewChildrenOnly;
        private readonly bool _individualRanges;
        private readonly string _displayName;
        private double _testPosition;

        public UrdfMotionGroupVM(ServoNames servo,
                                 IEnumerable<UrdfMotionSetting> settings,
                                 ServoConfiguration servoConfig,
                                 Action<ServoNames, double> previewGang,
                                 Action<ServoNames, RobotControls, double> previewChild,
                                 string displayName = null,
                                 bool previewChildrenOnly = false,
                                 bool showZeroAdjustments = false,
                                 bool individualRanges = false)
        {
            _servo = servo;
            _settings = settings.ToList();
            _previewGang = previewGang;
            _previewChild = previewChild;
            _previewChildrenOnly = previewChildrenOnly;
            _individualRanges = individualRanges;
            _displayName = displayName;

            (InputMin, InputMax) = UrdfConfiguration.TestInputRange(servo);
            (ExtentSliderMin, ExtentSliderMax) = UrdfConfiguration.ExtentEditorRange(servo);
            _testPosition = InputMin < 0 ? 0 : InputMin;

            if (!_individualRanges)
                SynchronizeRangesFromFirst();

            Directions = new ObservableCollection<UrdfDirectionRow>(
                _settings.Select(s => new UrdfDirectionRow(
                    s, servoConfig, this, Preview, showZeroAdjustments, _individualRanges)));
        }

        public string ServoName => _displayName ?? _servo.ToString();
        public string InputRangeText => InputMin < 0 ? "Input -100 … +100" : "Input 0 … 100";
        public string Unit => First.Unit;
        public double InputMin { get; }
        public double InputMax { get; }
        public double ExtentSliderMin { get; }
        public double ExtentSliderMax { get; }
        public bool ShowSharedRange => !_individualRanges;
        public ObservableCollection<UrdfDirectionRow> Directions { get; }

        private UrdfMotionSetting First => _settings[0];

        public double MinExtent
        {
            get => First.MinExtent;
            set
            {
                double v = Math.Clamp(value, ExtentSliderMin, ExtentSliderMax);
                double max = MaxExtent;
                if (v > max) max = v;
                SetRanges(v, max);
                Preview();
            }
        }

        public double MaxExtent
        {
            get => First.MaxExtent;
            set
            {
                double v = Math.Clamp(value, ExtentSliderMin, ExtentSliderMax);
                double min = MinExtent;
                if (v < min) min = v;
                SetRanges(min, v);
                Preview();
            }
        }

        public double TestPosition
        {
            get => _testPosition;
            set
            {
                double v = Math.Clamp(value, InputMin, InputMax);
                if (Math.Abs(_testPosition - v) < 1e-9) return;
                _testPosition = v;
                Raise(nameof(TestPosition));
                Raise(nameof(TestPositionText));
                Preview();
            }
        }

        public string TestPositionText => _testPosition.ToString("0");

        private void SynchronizeRangesFromFirst()
        {
            if (_settings.Count == 0) return;
            double min = Math.Clamp(First.MinExtent, ExtentSliderMin, ExtentSliderMax);
            double max = Math.Clamp(First.MaxExtent, ExtentSliderMin, ExtentSliderMax);
            if (min > max) (min, max) = (max, min);
            foreach (var setting in _settings)
            {
                setting.MinExtent = min;
                setting.MaxExtent = max;
                setting.ZeroExtent = Math.Clamp(setting.ZeroExtent, min, max);
            }
        }

        private void SetRanges(double min, double max)
        {
            foreach (var setting in _settings)
            {
                setting.MinExtent = min;
                setting.MaxExtent = max;
                setting.ZeroExtent = Math.Clamp(setting.ZeroExtent, min, max);
            }
            Raise(nameof(MinExtent));
            Raise(nameof(MaxExtent));
            foreach (var row in Directions)
                row.RefreshZeroRange();
        }

        public void RefreshDirections()
        {
            foreach (var row in Directions)
                row.RefreshDirection();
        }

        private void Preview()
        {
            double value = UrdfConfiguration.TestToServoValue(_servo, _testPosition);
            if (_previewChildrenOnly && _previewChild != null)
            {
                foreach (var setting in _settings)
                    _previewChild(_servo, setting.Control, value);
            }
            else
            {
                _previewGang?.Invoke(_servo, value);
            }
        }

        private void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Independent child calibration within a logical gang. Every URDF
    /// child exposes its own ZeroExtent and Direction; FlapsOpen children additionally
    /// own independent Min/Max extents.</summary>
    public sealed class UrdfDirectionRow : INotifyPropertyChanged
    {
        private readonly UrdfMotionSetting _setting;
        private readonly ServoConfiguration _servoConfig;
        private readonly UrdfMotionGroupVM _parent;
        private readonly Action _preview;

        public UrdfDirectionRow(UrdfMotionSetting setting,
                                ServoConfiguration servoConfig,
                                UrdfMotionGroupVM parent,
                                Action preview,
                                bool showZeroAdjustment,
                                bool showIndividualRangeAdjustment)
        {
            _setting = setting;
            _servoConfig = servoConfig;
            _parent = parent;
            _preview = preview;
            ShowZeroAdjustment = showZeroAdjustment;
            ShowIndividualRangeAdjustment = showIndividualRangeAdjustment;
        }

        public string ChildServoName => _setting.Control.ToString();
        public bool ShowZeroAdjustment { get; }
        public bool ShowIndividualRangeAdjustment { get; }
        public string Unit => _setting.Unit;
        public double ExtentSliderMin => _parent.ExtentSliderMin;
        public double ExtentSliderMax => _parent.ExtentSliderMax;
        public double ZeroSliderMin => ShowIndividualRangeAdjustment ? _setting.MinExtent : _parent.MinExtent;
        public double ZeroSliderMax => ShowIndividualRangeAdjustment ? _setting.MaxExtent : _parent.MaxExtent;

        public double MinExtent
        {
            get => _setting.MinExtent;
            set
            {
                double v = Math.Clamp(value, ExtentSliderMin, ExtentSliderMax);
                double max = _setting.MaxExtent;
                if (v > max) max = v;
                SetIndividualRange(v, max);
            }
        }

        public double MaxExtent
        {
            get => _setting.MaxExtent;
            set
            {
                double v = Math.Clamp(value, ExtentSliderMin, ExtentSliderMax);
                double min = _setting.MinExtent;
                if (v < min) min = v;
                SetIndividualRange(min, v);
            }
        }

        private void SetIndividualRange(double min, double max)
        {
            if (Math.Abs(_setting.MinExtent - min) < 1e-9 &&
                Math.Abs(_setting.MaxExtent - max) < 1e-9) return;
            _setting.MinExtent = min;
            _setting.MaxExtent = max;
            _setting.ZeroExtent = Math.Clamp(_setting.ZeroExtent, min, max);
            Raise(nameof(MinExtent));
            Raise(nameof(MaxExtent));
            Raise(nameof(ZeroSliderMin));
            Raise(nameof(ZeroSliderMax));
            Raise(nameof(ZeroExtent));
            _preview?.Invoke();
        }

        public double ZeroExtent
        {
            get => _setting.ZeroExtent;
            set
            {
                double v = Math.Clamp(value, ZeroSliderMin, ZeroSliderMax);
                if (Math.Abs(_setting.ZeroExtent - v) < 1e-9) return;
                _setting.ZeroExtent = v;
                Raise(nameof(ZeroExtent));
                _preview?.Invoke();
            }
        }

        public bool Reverse
        {
            get => _setting.ReverseOverride ?? UrdfConfiguration.InheritedReverse(
                _setting.Servo, _setting.Control, _servoConfig);
            set
            {
                if (_setting.ReverseOverride.HasValue && _setting.ReverseOverride.Value == value)
                    return;
                _setting.ReverseOverride = value;
                Raise(nameof(Reverse));
                Raise(nameof(ReverseText));
                _preview?.Invoke();
            }
        }

        public string ReverseText => _setting.ReverseOverride.HasValue
            ? (Reverse ? "Override: Rev" : "Override: Normal")
            : (Reverse ? "Inherited: Rev" : "Inherited: Normal");

        public void RefreshZeroRange()
        {
            _setting.ZeroExtent = Math.Clamp(_setting.ZeroExtent, ZeroSliderMin, ZeroSliderMax);
            Raise(nameof(MinExtent));
            Raise(nameof(MaxExtent));
            Raise(nameof(ZeroSliderMin));
            Raise(nameof(ZeroSliderMax));
            Raise(nameof(ZeroExtent));
        }

        public void RefreshDirection()
        {
            Raise(nameof(Reverse));
            Raise(nameof(ReverseText));
        }

        private void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
