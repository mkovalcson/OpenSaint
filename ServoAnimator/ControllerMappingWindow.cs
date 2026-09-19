using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ServoAnimator;

public sealed class ControllerMappingWindow : Window
{
    private ControllerProfile _draft;
    private readonly string _configRoot;
    private string _mappingPath;
    private readonly TextBlock _mappingFile = new() { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
    private readonly Func<ControllerSample> _read;
    private readonly Action<ControllerProfile> _save;
    private readonly ComboBox _target = new() { IsTextSearchEnabled = true, MaxDropDownHeight = 420 };
    private readonly ComboBox _mode = new() { ItemsSource = Enum.GetValues<ControllerMapMode>() };
    private readonly ComboBox _motionTrigger = new() { MaxDropDownHeight = 350, IsTextSearchEnabled = true };
    private readonly CheckBox _noMux = new() { Content = "No MUX, Map Shoulder buttons", Margin = new Thickness(12, 10, 0, 12),
        ToolTip = "Use a separate mapping set with mappable shoulders. All four MUX banks stay saved; uncheck to access them again." };
    private readonly TabControl _layerTabs = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _testHelp = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.SlateGray };
    private readonly TextBox _low = new(), _high = new(), _deadzone = new(), _scale = new();
    private readonly CheckBox _invert = new() { Content = "Reverse input direction" };
    private readonly TextBox _libraryName = new();
    private readonly CheckBox _loop = new() { Content = "Loop until stopped or replaced" };
    private readonly StackPanel _libraryPanel = new();
    private readonly TabControl _mappingTabs = new() { Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBlock _libraryCaption = new() { Margin = new Thickness(0, 8, 0, 4) };
    private readonly Button _chooseLibraryButton = new() { Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8) };
    private int _mappingTab;
    private string _poseName = "", _sequenceName = "";
    private readonly Func<string, Window, string> _chooseLibrary;
    private readonly List<(FrameworkElement Label, FrameworkElement Control)> _fields = new();
    private readonly TextBlock _responseHelp = new();
    private readonly TextBlock _selected = new() { FontSize = 19, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _live = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ControllerDiagram _diagram;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly ControllerInputEngine _liveEngine = new();
    private readonly CheckBox _testUrdf = new() { Content = "Test URDF with these mappings", Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBlock _activity = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.MediumTurquoise, FontSize = 12 };
    private readonly Func<string, double> _currentValue;
    private readonly Action<IReadOnlyList<ControllerIntent>, bool> _preview;
    private readonly Func<bool> _allowBackground;
    public ControllerKind Kind => _draft.Kind;
    internal bool CanTestWithCurrentFocus => (IsActive || _allowBackground()) && IsEnabled
        && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible);
    private long _lastLiveTick;
    private int _layer;
    private string _inputId;
    private bool _loading;
    private sealed record TargetItem(string Id, string Label)
    {
        public Brush Foreground => Id == ControllerCatalog.RecordingTarget ? Brushes.Tomato : (Brush)Application.Current.FindResource("PrimaryText");
        public override string ToString() => Label;
    }

    public ControllerMappingWindow(ControllerProfile profile, Func<ControllerSample> read, Action<ControllerProfile> save,
        Func<string, Window, string> chooseLibrary = null, Func<string, double> currentValue = null,
        Action<IReadOnlyList<ControllerIntent>, bool> preview = null, Func<bool> allowBackground = null,
        Func<string, double, double, double, double> relativeMotion = null, string configRoot = null)
    {
        _draft = profile.Clone(); _draft.Validate(); _read = read; _save = save; _chooseLibrary = chooseLibrary;
        _configRoot = configRoot;
        if (configRoot != null) _mappingPath = ControllerProfileStore.ActivePath(configRoot, profile.Kind);
        _currentValue = currentValue ?? (_ => 0); _preview = preview;
        _liveEngine.RelativeMotion = relativeMotion;
        _allowBackground = allowBackground ?? (() => false);
        Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/AnimationEditorPlayer;component/ControllerMappingStyles.xaml", UriKind.Relative) });
        Title = profile.Kind == ControllerKind.Xbox ? "X-Box Controller Mapping" : "Steam Controller Mapping · 2026";
        Width = profile.Kind == ControllerKind.Steam ? 1640 : 1440; Height = 1020; MinWidth = 1180; MinHeight = 740;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "AppBackground");
        SetResourceReference(ForegroundProperty, "PrimaryText");
        var root = new DockPanel { Margin = new Thickness(20), Background = Background }; Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        if (_configRoot != null) { top.Children.Add(_mappingFile); UpdateMappingFile(); }
        var layers = _layerTabs;
        for (int i = 0; i < 4; i++) layers.Items.Add(new TabItem { Header = $"{i + 1}  {ControllerCatalog.LayerNames[i]}" });
        layers.SelectedIndex = 0;
        layers.SelectionChanged += (_, e) =>
        {
            if (e.Source != layers || _loading) return;
            if (!Commit()) { _loading = true; layers.SelectedIndex = _layer; _loading = false; return; }
            _layer = layers.SelectedIndex; RefreshMappings(); SelectInput(_inputId ?? "LeftX");
        };
        var modeRow = new WrapPanel(); modeRow.Children.Add(layers);
        _noMux.IsChecked = _draft.NoMux;
        _noMux.Visibility = _draft.Kind == ControllerKind.Steam ? Visibility.Visible : Visibility.Collapsed;
        _noMux.SetResourceReference(ForegroundProperty, "PrimaryText"); modeRow.Children.Add(_noMux); top.Children.Add(modeRow);
        var bottom = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var note = new StackPanel(); note.Children.Add(_status); note.Children.Add(_error); bottom.Children.Add(note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(buttons, 1); bottom.Children.Add(buttons);
        Button AddButton(string label, Action action) { var b = new Button { Content = label, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(6, 0, 0, 0) }; b.Click += (_, _) => action(); buttons.Children.Add(b); return b; }
        AddButton("Reset mappings", () =>
        {
            var defaults = ControllerProfile.Defaults(_draft.Kind); defaults.NoMux = _draft.NoMux; defaults.Validate();
            var map = _draft.MappingForLayer(_layer); map.Clear();
            foreach (var entry in defaults.MappingForLayer(_layer)) map[entry.Key] = entry.Value.Clone();
            ResetLiveDraft(); RefreshMappings(); LoadInput();
        });
        AddButton("Save mapping", Save).IsDefault = true;
        if (_configRoot != null)
        {
            AddButton("Open mapping…", OpenMapping);
            AddButton("Save mapping as…", SaveMappingAs);
        }
        AddButton("Cancel", () => DialogResult = false).IsCancel = true;
        var grid = new Grid(); root.Children.Add(grid);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 740 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        _diagram = new ControllerDiagram(_draft.Kind, showLiveValues: true);
        _diagram.InputSelected += id => { if (Commit()) SelectInput(id); };
        var artwork = new Viewbox { Child = _diagram, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 10, 0) };
        grid.Children.Add(artwork);
        var inspector = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        var scroll = new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 1); grid.Children.Add(scroll);
        foreach (string label in new[] { "Control", "Pose", "Sequence" }) _mappingTabs.Items.Add(new TabItem { Header = label });
        _mappingTabs.SelectedIndex = 0;
        _mappingTabs.SelectionChanged += (_, e) =>
        {
            if (_loading || e.Source != _mappingTabs) return;
            if (_mappingTab == 1) _poseName = _libraryName.Text;
            if (_mappingTab == 2) _sequenceName = _libraryName.Text;
            _mappingTab = _mappingTabs.SelectedIndex;
            _libraryName.Text = _mappingTab == 1 ? _poseName : _sequenceName;
            _error.Text = ""; UpdateLibraryFields(); PreviewMapping();
        };
        inspector.Children.Add(_mappingTabs);
        inspector.Children.Add(_selected);
        _live.Margin = new Thickness(0, 8, 0, 18); _live.Foreground = Brushes.MediumTurquoise; inspector.Children.Add(_live);
        _testUrdf.IsEnabled = preview != null; _testUrdf.SetResourceReference(ForegroundProperty, "PrimaryText");
        _testUrdf.Checked += (_, _) => { _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false); };
        _testUrdf.Unchecked += (_, _) => { _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false); };
        inspector.Children.Add(_testUrdf);
        inspector.Children.Add(_testHelp);
        inspector.Children.Add(_activity);
        void Field(string label, FrameworkElement control)
        {
            var caption = new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap };
            inspector.Children.Add(caption); control.Margin = new Thickness(0, 0, 0, 2); inspector.Children.Add(control);
            _fields.Add((caption, control));
        }
        _target.Items.Add(new TargetItem("", "Unassigned"));
        var targetText = new FrameworkElementFactory(typeof(TextBlock));
        targetText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Label"));
        targetText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground"));
        _target.ItemTemplate = new DataTemplate { VisualTree = targetText };
        foreach (var servo in Enum.GetValues<ServoNames>().Where(s => !ServoCommand.IsTextValued(s)))
        {
            _target.Items.Add(new TargetItem("Servo:" + servo, servo + " · group"));
            foreach (var child in ServoConfiguration.ControlsFor(servo)) _target.Items.Add(new TargetItem($"Child:{servo}:{child}", $"{servo} → {child}"));
        }
        foreach (var action in ControllerCatalog.Actions) _target.Items.Add(new TargetItem("Action:" + action, action == ControllerCatalog.RecordingAction ? "REC · Start / Stop recording" : "Action · " + action));
        Field("Target control or action", _target);
        RebuildTriggerButtons();
        Field("Optional trigger · hold button", _motionTrigger);
        inspector.Children.Add(_libraryPanel);
        _libraryPanel.Children.Add(_libraryCaption);
        _libraryPanel.Children.Add(_libraryName);
        _chooseLibraryButton.Click += (_, _) =>
        {
            string kind = _mappingTabs.SelectedIndex == 1 ? "pose" : "sequence";
            string name = _chooseLibrary?.Invoke(kind, this);
            if (!string.IsNullOrEmpty(name)) _libraryName.Text = name;
        };
        _chooseLibraryButton.IsEnabled = _chooseLibrary != null; _libraryPanel.Children.Add(_chooseLibraryButton);
        _loop.SetResourceReference(ForegroundProperty, "PrimaryText"); _libraryPanel.Children.Add(_loop);
        _libraryPanel.Children.Add(new TextBlock { Text = "Press once to run. Another Library button immediately replaces playback. The open timeline is preserved.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        Field("Response", _mode);
        _target.SelectionChanged += (_, _) =>
        {
            if (_loading || _target.SelectedItem is not TargetItem target) return;
            if (ControllerTargets.TryServo(target.Id, out var servo, out _))
            { var range = ServoCommand.RangeFor(servo); _low.Text = range.Min.ToString(); _high.Text = range.Max.ToString(); }
            else if (target.Id.StartsWith("Action:")) _mode.SelectedItem = ControllerMapMode.Set;
            PreviewMapping();
        };
        Field("Low value / toggle first position", _low); Field("High value / pressed destination", _high);
        Field("Dead zone · 0 to less than 1", _deadzone);
        Field("Sensor full scale · rad/s or m/s²", _scale);
        _invert.Margin = new Thickness(0, 12, 0, 10); inspector.Children.Add(_invert);
        _invert.SetResourceReference(ForegroundProperty, "PrimaryText");
        _responseHelp.Text = "Relative: move at the servo's calibrated speed; partial deflection reduces speed. Release to hold.\nAbsolute: input position maps between Low and High.\nSet: press sets High. Toggle: press alternates Low / High.\n\nSpeeds come from Speed Calibration and the selected Default / Slow / Fast / Crawl profile. Eye Pop uses its calibrated extend/retract times.\n\nMotion sensors use a neutral baseline on enable/layer change.";
        _responseHelp.TextWrapping = TextWrapping.Wrap; _responseHelp.FontSize = 12; _responseHelp.Foreground = Brushes.SlateGray;
        inspector.Children.Add(_responseHelp);
        var clear = new Button { Content = "Clear this input", Padding = new Thickness(8), Margin = new Thickness(0, 16, 0, 0) };
        clear.Click += (_, _) => { if (_inputId != null) { _draft.MappingForLayer(_layer)[_inputId] = new(); ResetLiveDraft(); LoadInput(); RefreshMappings(); } }; inspector.Children.Add(clear);
        _libraryName.TextChanged += (_, _) => PreviewMapping();
        _loop.Checked += (_, _) => PreviewMapping(); _loop.Unchecked += (_, _) => PreviewMapping();
        UpdateMuxUi(); RefreshMappings(); SelectInput("LeftX");
        void ResetLiveDraft()
        {
            if (_loading) return;
            _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false);
        }
        foreach (var box in new[] { _low, _high, _deadzone, _scale, _libraryName }) box.TextChanged += (_, _) => ResetLiveDraft();
        _mode.SelectionChanged += (_, _) => ResetLiveDraft(); _target.SelectionChanged += (_, _) => ResetLiveDraft();
        _invert.Checked += (_, _) => ResetLiveDraft(); _invert.Unchecked += (_, _) => ResetLiveDraft();
        _loop.Checked += (_, _) => ResetLiveDraft(); _loop.Unchecked += (_, _) => ResetLiveDraft();
        _mappingTabs.SelectionChanged += (_, _) => ResetLiveDraft();
        _motionTrigger.SelectionChanged += (_, _) => { ResetLiveDraft(); PreviewMapping(); };
        _noMux.Checked += (_, _) => ChangeMux(); _noMux.Unchecked += (_, _) => ChangeMux();
        _timer.Tick += (_, _) => UpdateLive(); Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => { _timer.Stop(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false); };
    }
    private void RebuildTriggerButtons()
    {
        _motionTrigger.Items.Clear(); _motionTrigger.Items.Add(new TargetItem("", "None · no button required"));
        foreach (var button in ControllerCatalog.TriggerButtons(_draft.Kind, _draft.NoMux)) _motionTrigger.Items.Add(new TargetItem(button.Id, button.Label));
        _motionTrigger.ToolTip = "Hold this button to activate the motion mapping. The button retains its own mapping. Analog triggers engage above half travel.";
    }
    private void UpdateMuxUi()
    {
        _layerTabs.Visibility = _draft.NoMux ? Visibility.Collapsed : Visibility.Visible;
        _testHelp.Text = (_draft.NoMux ? "Uses unsaved No MUX mappings." : "Uses unsaved mappings and the held shoulder layer.")
            + " URDF only. Release controls after enabling. Other editor actions are shown but not executed.";
    }
    private void ChangeMux()
    {
        if (_loading) return;
        if (!Commit()) { _loading = true; _noMux.IsChecked = _draft.NoMux; _loading = false; return; }
        _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false);
        _draft.NoMux = _noMux.IsChecked == true; _draft.Validate();
        _layer = 0;
        if (!ControllerCatalog.Inputs(_draft.Kind, _draft.NoMux).Any(i => i.Id == _inputId)) _inputId = "LeftX";
        _loading = true; _layerTabs.SelectedIndex = 0; RebuildTriggerButtons(); _loading = false;
        UpdateMuxUi(); RefreshMappings(); LoadInput();
    }
    private ControllerProfile ReadDraft()
    {
        double Number(TextBox box) => double.Parse(box.Text, CultureInfo.CurrentCulture);
        var binding = new ControllerBinding { Target = SelectedTarget,
            Mode = _mode.SelectedItem is ControllerMapMode mode ? mode : ControllerMapMode.Relative,
            Low = Number(_low), High = Number(_high), DeadZone = Number(_deadzone), SensorScale = Number(_scale), Invert = _invert.IsChecked == true,
            TriggerButton = ControllerCatalog.Inputs(_draft.Kind, _draft.NoMux).First(i => i.Id == _inputId).Section == "Motion" ? (_motionTrigger.SelectedItem as TargetItem)?.Id ?? "" : "",
            LibraryName = _mappingTabs.SelectedIndex == 0 ? "" : _libraryName.Text.Trim(), Loop = _mappingTabs.SelectedIndex == 2 && _loop.IsChecked == true };
        var candidate = _draft.Clone(); candidate.MappingForLayer(_layer)[_inputId] = binding; candidate.Validate();
        return candidate;
    }
    private bool Commit()
    {
        if (_loading || _inputId == null) return true;
        try
        {
            _draft = ReadDraft(); _error.Text = ""; RefreshMappings(); return true;
        }
        catch (Exception ex) { _error.Text = "Fix this input before continuing: " + ex.Message; return false; }
    }
    private void RefreshMappings()
    {
        _diagram.NoMux = _draft.NoMux; _diagram.Mappings = _draft.MappingForLayer(_layer); _diagram.MappingLayer = _layer; _diagram.InvalidateVisual();
    }
    private void PreviewMapping()
    {
        if (_loading || _inputId == null) return;
        var map = new Dictionary<string, ControllerBinding>(_draft.MappingForLayer(_layer));
        var binding = map[_inputId].Clone(); binding.Target = SelectedTarget;
        binding.LibraryName = _libraryName.Text.Trim(); binding.Loop = _mappingTabs.SelectedIndex == 2 && _loop.IsChecked == true;
        binding.TriggerButton = (_motionTrigger.SelectedItem as TargetItem)?.Id ?? "";
        map[_inputId] = binding; _diagram.Mappings = map; _diagram.InvalidateVisual();
    }
    private void SelectInput(string id)
    {
        _inputId = id; LoadInput();
    }
    private void LoadInput()
    {
        if (_inputId == null) return;
        _loading = true;
        var input = ControllerCatalog.Inputs(_draft.Kind, _draft.NoMux).First(i => i.Id == _inputId);
        var binding = _draft.MappingForLayer(_layer)[_inputId]; _selected.Text = input.Label;
        _target.SelectedItem = _target.Items.Cast<TargetItem>().FirstOrDefault(t => t.Id == binding.Target) ?? _target.Items[0];
        _mappingTab = binding.Target == "Library:pose" ? 1 : binding.Target == "Library:sequence" ? 2 : 0;
        _mappingTabs.SelectedIndex = _mappingTab;
        _poseName = _mappingTab == 1 ? binding.LibraryName : "";
        _sequenceName = _mappingTab == 2 ? binding.LibraryName : "";
        _mode.ItemsSource = input.Analog ? Enum.GetValues<ControllerMapMode>() : new[] { ControllerMapMode.Set, ControllerMapMode.Toggle };
        _mode.SelectedItem = input.Analog || binding.Mode is ControllerMapMode.Set or ControllerMapMode.Toggle ? binding.Mode : ControllerMapMode.Set;
        _low.Text = binding.Low.ToString(CultureInfo.CurrentCulture); _high.Text = binding.High.ToString(CultureInfo.CurrentCulture);
        _deadzone.Text = binding.DeadZone.ToString(CultureInfo.CurrentCulture);
        _scale.Text = binding.SensorScale.ToString(CultureInfo.CurrentCulture); _scale.IsEnabled = input.Section == "Motion";
        _invert.IsChecked = binding.Invert;
        _motionTrigger.SelectedItem = _motionTrigger.Items.Cast<TargetItem>().FirstOrDefault(t => t.Id == binding.TriggerButton) ?? _motionTrigger.Items[0];
        _libraryName.Text = binding.LibraryName; _loop.IsChecked = binding.Loop; UpdateLibraryFields();
        _diagram.SelectedInput = _inputId; _diagram.MappingLayer = _layer; _diagram.InvalidateVisual();
        _loading = false; UpdateLive();
    }
    private void UpdateLive()
    {
        var sample = _read();
        _diagram.SetLiveInput(sample);
        _status.Text = sample.Connected ? "● Connected · " + (_draft.NoMux ? "No MUX · shoulders are mappable" : "held layer: " + ControllerCatalog.LayerNames[_draft.ActiveLayer(sample)]) : "● Not connected · mappings can be edited offline";
        _status.Foreground = sample.Connected ? Brushes.ForestGreen : Brushes.Firebrick;
        _live.Text = !sample.Connected ? "Live input unavailable" : !sample.Supported.Contains(_inputId ?? "") ? "Not exposed by this device / driver" :
            sample.Values.TryGetValue(_inputId, out double value) ? $"Live input: {value:0.000}" : "Available · not touched";
        if (_loading || _inputId == null) return;
        long now = Environment.TickCount64; double dt = (now - _lastLiveTick) / 1000.0; _lastLiveTick = now;
        bool testing = _testUrdf.IsChecked == true && CanTestWithCurrentFocus && sample.Connected;
        try
        {
            var candidate = ReadDraft();
            if (!CanTestWithCurrentFocus) _liveEngine.Reset();
            var intents = _liveEngine.Evaluate(candidate, sample, dt, _currentValue);
            _preview?.Invoke(intents, testing);
            _diagram.SetLiveInput(sample, _liveEngine.Readings);
            string Describe(string id, ControllerReading r)
            {
                var binding = candidate.MappingForLayer(candidate.ActiveLayer(sample))[id];
                string destination = r.State.StartsWith("Hold ") ? r.State : r.Mapped?.ToString("0.###") ?? r.State;
                return $"Raw: {r.Raw:0.000} → {destination}" + (r.State == "Release to arm" ? " · release to arm" : "")
                    + "\n" + (ControllerTargets.IsLibrary(binding.Target) ? binding.Target[8..] + ": " + binding.LibraryName : binding.Target.Replace("Servo:", "").Replace("Child:", "").Replace("Action:", ""));
            }
            if (_liveEngine.Readings.TryGetValue(_inputId, out var reading)) _live.Text = Describe(_inputId, reading);
            _activity.Text = string.Join("\n", _liveEngine.Readings.Where(p => p.Value.Active).Select(p =>
                ControllerCatalog.Inputs(_draft.Kind, _draft.NoMux).First(i => i.Id == p.Key).Label + " · " + Describe(p.Key, p.Value)));
        }
        catch (Exception ex)
        {
            _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false);
            _diagram.SetLiveInput(sample);
            _activity.Text = "Live test paused: " + ex.Message;
        }
    }
    private string SelectedTarget => _mappingTabs.SelectedIndex switch
    {
        1 => "Library:pose", 2 => "Library:sequence", _ => (_target.SelectedItem as TargetItem)?.Id ?? ""
    };
    private void UpdateLibraryFields()
    {
        string target = SelectedTarget;
        bool library = ControllerTargets.IsLibrary(target);
        var input = ControllerCatalog.Inputs(_draft.Kind, _draft.NoMux).FirstOrDefault(i => i.Id == _inputId);
        foreach (var field in _fields)
        {
            bool visible = field.Control == _motionTrigger ? input?.Section == "Motion" :
                !library || (field.Control == _deadzone && input?.Analog == true) || (field.Control == _scale && input?.Section == "Motion");
            field.Label.Visibility = field.Control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        _invert.Visibility = _responseHelp.Visibility = library ? Visibility.Collapsed : Visibility.Visible;
        _libraryPanel.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
        _loop.IsEnabled = target == "Library:sequence";
        _loop.Visibility = _loop.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        _libraryCaption.Text = target == "Library:pose" ? "Pose name or relative path" : "Sequence name or relative path";
        _chooseLibraryButton.Content = target == "Library:pose" ? "Choose Library Pose…" : "Choose Library Sequence…";
        _mode.IsEnabled = _low.IsEnabled = _high.IsEnabled = _invert.IsEnabled = !library;
    }
    private void Save()
    {
        if (!Commit()) return;
        try
        {
            if (_configRoot != null) ControllerProfileStore.SaveSelected(_configRoot, _draft, _mappingPath);
            _save(_draft); DialogResult = true;
        }
        catch (Exception ex) { _error.Text = "Could not save mapping: " + ex.Message; }
    }
    private void UpdateMappingFile()
    {
        _mappingFile.Text = "Mapping: " + System.IO.Path.GetFileNameWithoutExtension(_mappingPath);
        _mappingFile.ToolTip = _mappingPath;
    }
    private void SaveMappingAs()
    {
        if (!Commit()) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save " + Kind + " controller mapping", Filter = "Controller mapping (*.json)|*.json",
            DefaultExt = ".json", FileName = Kind + " mapping.json",
            InitialDirectory = System.IO.Path.GetDirectoryName(_mappingPath)
        };
        if (dialog.ShowDialog(this) != true) return;
        _mappingPath = dialog.FileName; UpdateMappingFile(); Save();
    }
    private void OpenMapping()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open " + Kind + " controller mapping", Filter = "Controller mapping (*.json)|*.json",
            InitialDirectory = System.IO.Path.GetDirectoryName(_mappingPath)
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var profile = ControllerProfileStore.LoadFile(dialog.FileName, Kind);
            if (MessageBox.Show(this, "Replace the current draft with this mapping? Unsaved edits will be discarded.",
                "Open mapping", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            _liveEngine.Reset(); _preview?.Invoke(Array.Empty<ControllerIntent>(), false);
            _loading = true;
            try { _draft = profile; _layer = 0; _layerTabs.SelectedIndex = 0; _noMux.IsChecked = profile.NoMux; RebuildTriggerButtons(); }
            finally { _loading = false; }
            _mappingPath = dialog.FileName; UpdateMappingFile();
            UpdateMuxUi(); RefreshMappings(); SelectInput("LeftX");
            _error.Text = "";
        }
        catch (Exception ex) { _error.Text = "Could not open mapping: " + ex.Message; }
    }
}
