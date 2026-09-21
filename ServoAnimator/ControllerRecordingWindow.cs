using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator;

internal sealed class ControllerRecordingWindow : Window
{
    private readonly StackPanel _setup = new(), _processing = new();
    private readonly ComboBox _source = new() { ItemsSource = new[] { "X-Box Controller", "Steam Controller" }, SelectedIndex = 0, MinWidth = 210 };
    private readonly CheckBox _overdub = new() { Content = "Overdub selected controls", IsChecked = true, Margin = new Thickness(0, 12, 0, 8) };
    private readonly CheckBox _groupRecorded = new() { Content = "Group Recorded Commands", IsChecked = true, Margin = new Thickness(0, 8, 0, 8) };
    internal bool GroupRecordedCommands => _groupRecorded.IsChecked == true;
    private readonly TextBox _duration = new() { Text = "60", Width = 75 };
    private readonly Slider _smooth = new() { Minimum = 0, Maximum = 300, Value = 60, TickFrequency = 10, IsSnapToTickEnabled = true };
    private readonly Slider _tolerance = new() { Minimum = 0, Maximum = 5, Value = 1, TickFrequency = .25, IsSnapToTickEnabled = true };
    private readonly TextBlock _smoothLabel = new(), _toleranceLabel = new(), _status = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 48 };
    private readonly Button _record = new() { Content = "Ready to Record", MinWidth = 125 }, _replay = new() { Content = "Replay Recording", Visibility = Visibility.Collapsed }, _keep = new() { Content = "Keep Take", MinWidth = 100, IsEnabled = false, Visibility = Visibility.Collapsed };
    private readonly Dictionary<ServoNames, CheckBox> _controls = new();
    private readonly Dictionary<(ServoNames Servo, RobotControls Control), CheckBox> _children = new();
    internal HashSet<(ServoNames Servo, RobotControls Control)> ArmedChildren { get; private set; } = new();
    private bool _hasTake, _accepted;
    internal bool Recording { get; private set; }
    internal bool Armed { get; private set; }
    private HashSet<ServoNames> _armedControls;
    private double _armedDuration;
    internal bool CanStart => !Recording && !_hasTake;
    internal void StartFromController(ControllerKind source)
    {
        if (!Armed || source != Source || !CanStart) return;
        StartRequested?.Invoke(Source, _armedControls.ToHashSet(), _armedDuration);
    }
    internal ControllerKind Source => _source.SelectedIndex == 0 ? ControllerKind.Xbox : ControllerKind.Steam;
    internal double Smoothing => _smooth.Value;
    internal double Tolerance => _tolerance.Value;
    internal event Action<ControllerKind, HashSet<ServoNames>, double> StartRequested;
    internal event Action ReadyRequested, ReplayRequested, StopRequested, ProcessingChanged, KeepRequested;

    internal ControllerRecordingWindow(double cursor)
    {
        Title = "Record from Controller"; Width = 600; Height = 800; MinWidth = 500; MinHeight = 560; FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "AppBackground"); SetResourceReference(ForegroundProperty, "PrimaryText");
        var root = new DockPanel { Margin = new Thickness(18) }; root.SetResourceReference(Panel.BackgroundProperty, "AppBackground"); Content = root;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_status);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var b in new[] { _record, _keep, _replay }) { b.Margin = new Thickness(0, 0, 10, 0); b.Padding = new Thickness(9, 7, 9, 7); buttons.Children.Add(b); }
        footer.Children.Add(buttons);
        var discard = new Button { Content = "Discard / close", Padding = new Thickness(9, 7, 9, 7) };
        discard.Click += (_, _) => Close(); buttons.Children.Add(discard);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(new TextBlock { Text = $"Record from {cursor:F3} seconds", FontSize = 21, Margin = new Thickness(0, 0, 0, 8) });
        body.Children.Add(_setup); _setup.Children.Add(_source); _setup.Children.Add(_overdub);
        _overdub.SetResourceReference(Control.ForegroundProperty, "PrimaryText");
        var selectionButtons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (bool all in new[] { true, false })
        {
            var select = new Button { Content = all ? "Select all" : "Clear selection", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(7, 3, 7, 3) };
            select.Click += (_, _) => { foreach (var control in _controls.Values) control.IsChecked = all; foreach (var child in _children.Values) child.IsChecked = false; };
            selectionButtons.Children.Add(select);
        }
        _setup.Children.Add(selectionButtons);
        var list = new WrapPanel();
        var childPanels = new List<StackPanel>();
        var selectChildren = new Button { Content = "Select child controls", Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(7, 3, 7, 3) };
        selectionButtons.Children.Add(selectChildren);
        selectChildren.Click += (_, _) =>
        {
            bool show = childPanels.Any(panel => panel.Visibility != Visibility.Visible);
            foreach (var panel in childPanels) panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        };
        foreach (var servo in Enum.GetValues<ServoNames>().Where(s => !ServoCommand.IsTextValued(s) && s is not (ServoNames.NeckTiltRight or ServoNames.LeftEyePop or ServoNames.RightEyePop)))
        {
            string label = servo == ServoNames.NeckNodUp ? "Neck nod / tilt" : servo == ServoNames.BothEyePop ? "Eye pop (both sides)" : servo.ToString();
            var check = new CheckBox { Content = ServoLabel(servo, label), IsChecked = true, Width = 245, Margin = new Thickness(0, 3, 0, 3) };
            check.SetResourceReference(Control.ForegroundProperty, "PrimaryText");
            _controls[servo] = check;
            var group = new StackPanel { Width = 245, Margin = new Thickness(0, 0, 0, 5) }; group.Children.Add(check); list.Children.Add(group);
            var childPanel = new StackPanel { Margin = new Thickness(18, 0, 0, 4), Visibility = Visibility.Collapsed };
            if (ServoConfiguration.ControlsFor(servo).Distinct().Count() > 1) childPanels.Add(childPanel);
            group.Children.Add(childPanel);
            foreach (var control in ServoConfiguration.ControlsFor(servo).Distinct().Where(_ => ServoConfiguration.ControlsFor(servo).Distinct().Count() > 1))
            {
                var child = new CheckBox { Content = ServoLabel(servo, control.ToString()), Margin = new Thickness(0, 3, 0, 3) };
                child.SetResourceReference(Control.ForegroundProperty, "PrimaryText");
                _children[(servo, control)] = child; childPanel.Children.Add(child);
                child.Checked += (_, _) => check.IsChecked = false;
            }
            check.Checked += (_, _) => { foreach (var child in _children.Where(p => p.Key.Servo == servo)) child.Value.IsChecked = false; };
        }
        _setup.Children.Add(list);
        _overdub.Checked += (_, _) => { list.IsEnabled = true; selectionButtons.IsEnabled = true; };
        _overdub.Unchecked += (_, _) => { list.IsEnabled = false; selectionButtons.IsEnabled = false; };
        var limit = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
        limit.Children.Add(new TextBlock { Text = "Maximum take length (1–300 seconds): ", VerticalAlignment = VerticalAlignment.Center }); limit.Children.Add(_duration); _setup.Children.Add(limit);
        body.Children.Add(_processing);
        _groupRecorded.SetResourceReference(Control.ForegroundProperty, "PrimaryText");
        _processing.Children.Add(_groupRecorded);
        _processing.Children.Add(_smoothLabel); _processing.Children.Add(_smooth); _processing.Children.Add(_toleranceLabel); _processing.Children.Add(_tolerance);
        body.Children.Add(new TextBlock { Text = "Reduction tolerance is a percentage of each control’s full range. Existing spline settings are preserved; child controls use held commands.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 12) });
        void Changed() { _smoothLabel.Text = $"Smoothing: {_smooth.Value:F0} ms"; _toleranceLabel.Text = $"Keyframe reduction tolerance: {_tolerance.Value:F2}%"; if (_hasTake) ProcessingChanged?.Invoke(); }
        _smooth.ValueChanged += (_, _) => Changed(); _tolerance.ValueChanged += (_, _) => Changed(); Changed();
        _record.Click += (_, _) =>
        {
            if (!double.TryParse(_duration.Text, out var length) || !double.IsFinite(length) || length < 1 || length > 300) { SetStatus("Enter a take length between 1 and 300 seconds."); return; }
            var selected = _controls.Where(p => _overdub.IsChecked != true || p.Value.IsChecked == true).Select(p => p.Key).ToHashSet();
            ArmedChildren = _overdub.IsChecked == true ? _children.Where(p => p.Value.IsChecked == true && !selected.Contains(p.Key.Servo)).Select(p => p.Key).ToHashSet() : new();
            if (selected.Count == 0 && ArmedChildren.Count == 0) { SetStatus("Arm at least one control."); return; }
            _armedControls = selected; _armedDuration = length; Armed = true;
            ReadyRequested?.Invoke();
        };
        _replay.Click += (_, _) => ReplayRequested?.Invoke();
        _keep.Click += (_, _) => KeepRequested?.Invoke();
        Closing += (_, e) =>
        {
            if (Recording) StopRequested?.Invoke();
            if (_hasTake && !_accepted && MessageBox.Show(this, "Discard this recorded take?", "Controller recording", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) e.Cancel = true;
        };
        SetStatus("");
    }
    private static FrameworkElement ServoLabel(ServoNames servo, string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Image { Source = ServoIconProvider.For(servo), Width = 18, Height = 18,
            Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 6, 0) });
        content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return content;
    }
    internal void Begin()
    { Armed = false; Recording = true; _setup.IsEnabled = false; _processing.IsEnabled = false; _record.IsEnabled = false; _keep.IsEnabled = false; }
    internal void Review(bool hasTake)
    { Armed = false; Recording = false; _hasTake = hasTake; _processing.IsEnabled = true; _keep.IsEnabled = hasTake; _setup.IsEnabled = !hasTake; _record.IsEnabled = !hasTake;
        _record.Visibility = hasTake ? Visibility.Collapsed : Visibility.Visible;
        _keep.Visibility = _replay.Visibility = hasTake ? Visibility.Visible : Visibility.Collapsed; }
    internal void ShowConfiguration() { if (CanStart) Armed = false; ShowReview(); }
    internal void ShowReview() { if (Owner != null) { Show(); Activate(); } }
    internal void SetReplayState(bool playing) => _replay.Content = playing ? "Stop Replay" : "Replay Recording";
    internal void SetStatus(string text) => _status.Text = text;
    internal void SetProcessing(bool busy) => _keep.IsEnabled = _replay.IsEnabled = !busy && _hasTake;
    internal void AcceptAndClose() { _accepted = true; Close(); }
}
