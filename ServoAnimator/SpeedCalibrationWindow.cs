using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ServoAnimator;

public sealed class SpeedCalibrationWindow : Window
{
    private readonly ServoConfiguration _config;
    private readonly SpeedCalibrationData _data;
    private readonly string _root;
    private readonly Func<ServoConfigEntry, SpeedCalibrationPlan, IProgress<SpeedCalibrationProgress>, CancellationToken, Task> _run;
    private readonly Action _saved;
    private readonly ComboBox _servo = new() { Width = 225 };
    private readonly ComboBox _abortMode = new() { Width = 235, ItemsSource = new[] { "Hold last reported pulse", "Disable active servo (release torque)" }, SelectedIndex = 0 };
    private readonly CheckBox _use = new() { Content = "Use calibrated motion in URDF" }, _mini = new() { Content = "Mini Maestro" };
    private readonly CheckBox _prepared = new() { Content = "Mechanism is at Test start with pulses enabled; range and coupled linkages have been reviewed" };
    private readonly CheckBox _short = new() { Content = "Also test quarter-distance moves", IsChecked = true };
    private readonly TextBox _from = Box(), _to = Box(), _repeats = Box("2"), _period = Box("20"), _ceiling = Box(""), _travel = Box("180"), _settle = Box("1000"), _notes = new() { MinWidth = 320 };
    private readonly Dictionary<ServoSpeed, CheckBox> _speeds = new();
    private readonly StackPanel _options = new();
    private readonly DataGrid _results = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly Canvas _plot = new() { Height = 135, Background = new SolidColorBrush(Color.FromRgb(21, 26, 32)), ClipToBounds = true };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _fallback = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _start = new() { Content = "Start calibration", Padding = new Thickness(14, 6, 14, 6) }, _abort = new() { Content = "Abort", IsEnabled = false, Padding = new Thickness(18, 6, 18, 6) }, _save = new() { Content = "Save settings / physical times", Padding = new Thickness(12, 6, 12, 6) };
    private CancellationTokenSource _cancel;
    private bool _closePending;
    private string _saveError;
    private ServoConfigEntry Selected => _servo.SelectedItem is RobotControls control ? _config.Get(control) : null;
    private ServoConfigEntry _editingServo;
    private bool _restoringSelection;
    private readonly DataGrid _steppers = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly TabControl _tabs = new();

    internal SpeedCalibrationWindow(ServoConfiguration config, SpeedCalibrationData data, string root,
        Func<ServoConfigEntry, SpeedCalibrationPlan, IProgress<SpeedCalibrationProgress>, CancellationToken, Task> run, Action saved, bool connected)
    {
        _config = config; _data = data; _root = root; _run = run; _saved = saved;
        _data.RegeneratePredictions(config);
        Title = "Speed Calibration"; Width = 1130; Height = 900; MinWidth = 880; MinHeight = 700; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontSize = 14;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/AnimationEditorPlayer;component/SpeedCalibrationStyles.xaml", UriKind.Relative) });
        HelpSystem.EnableContextHelp(this, "servo-configuration");
        SetResourceReference(BackgroundProperty, "AppBackground"); SetResourceReference(ForegroundProperty, "PrimaryText");
        var layout = new Grid { Margin = new Thickness(16) }; Content = _tabs;
        _tabs.Items.Add(new TabItem { Header = "Servos", Content = layout, FontSize = 14 });
        layout.SetResourceReference(Panel.BackgroundProperty, "AppBackground");
        foreach (var check in new[] { _use, _mini, _prepared, _short }) check.SetResourceReference(ForegroundProperty, "PrimaryText");
        _results.SetResourceReference(DataGrid.RowBackgroundProperty, "ControlBackground");
        _results.SetResourceReference(ForegroundProperty, "PrimaryText");
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("PrimaryText")));
        cellStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        var selectedCell = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedCell.Setters.Add(new Setter(BackgroundProperty, new DynamicResourceExtension("SelectionBackground")));
        cellStyle.Triggers.Add(selectedCell); _results.CellStyle = cellStyle;
        _steppers.CellStyle = cellStyle;
        BuildStepperTab();
        _prepared.Content = new TextBlock { Text = "Mechanism is at Test start with pulses enabled; range and coupled linkages have been reviewed", TextWrapping = TextWrapping.Wrap };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto }) layout.RowDefinitions.Add(new() { Height = height });
        layout.Children.Add(_options);
        AddRow(_options, _use, new TextBlock { Text = "Display updates remain at 30 fps; motion uses elapsed time.", VerticalAlignment = VerticalAlignment.Center });
        AddRow(_options, Label("Servo"), _servo, _mini, Label("Period (ms)"), _period);
        AddRow(_options, Label("Test start (µs)"), _from, Label("Test end (µs)"), _to, Label("Repeats"), _repeats, Label("Settle (ms)"), _settle);
        var speeds = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        foreach (var speed in new[] { ServoSpeed.Default, ServoSpeed.Slow, ServoSpeed.Fast, ServoSpeed.Crawl })
        { var check = new CheckBox { Content = speed.ToString(), IsChecked = true, Margin = new Thickness(0, 0, 18, 0) }; check.SetResourceReference(ForegroundProperty, "PrimaryText"); _speeds[speed] = check; speeds.Children.Add(check); }
        speeds.Children.Add(_short); _options.Children.Add(speeds);
        AddRow(_options, Label("Physical ceiling override (µs/s; optional)"), _ceiling);
        AddRow(_options, Label("Assumed servo Min–Max travel (°)"), _travel, _fallback);
        AddRow(_options, Label("Servo / voltage / load notes"), _notes);
        _options.Children.Add(new TextBlock { Text = "Start moves the selected physical servo. First use Servo Configuration Verify to position it at Test start; no automatic home or initial jump is performed. For coupled neck actuators, isolate or support the linkage before testing an individual servo. Settle time must allow physical movement to finish after the output pulse reaches the target.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 7) });
        _options.Children.Add(_prepared); AddRow(_options, Label("Abort behavior"), _abortMode);
        AddColumn("Setting", "Result.Setting", 75); AddColumn("Direction", "Result.Direction", 120); AddColumn("From µs", "Result.FromPwm", 75); AddColumn("To µs", "Result.ToPwm", 75);
        AddColumn("Seconds", "Result.SecondsDisplay", 85); AddColumn("µs/s", "Result.RateDisplay", 85);
        AddColumn("Physical seconds", "Result.PhysicalSeconds", 115, true, "0.000"); AddColumn("Evidence / validity", "Status", 245);
        Grid.SetRow(_results, 1); layout.Children.Add(_results);
        Grid.SetRow(_plot, 2); _plot.Margin = new Thickness(0, 8, 0, 0); layout.Children.Add(_plot);
        var footer = new StackPanel(); Grid.SetRow(footer, 3); layout.Children.Add(footer);
        footer.Children.Add(new TextBlock { Text = "Predicted rows show full Min ↔ Max travel from configured speed/acceleration; saving Servo Configuration refreshes them automatically. Measured rows report Maestro output, not horn position. Physical seconds can be entered from observation/video. Stale evidence is retained but excluded from timing.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) }); footer.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetRow(buttons, 4); layout.Children.Add(buttons);
        foreach (var button in new[] { _start, _abort, _save }) { button.Margin = new Thickness(8, 0, 0, 0); buttons.Children.Add(button); }
        var close = new Button { Content = "Close", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0) }; close.Click += (_, _) => Close(); buttons.Children.Add(close);
        _use.IsChecked = data.UseInUrdf; _mini.IsChecked = data.MiniMaestro; _period.Text = data.PeriodMs.ToString(CultureInfo.InvariantCulture);
        _servo.ItemsSource = config.Servos.Where(s => (int)s.Control is >= 0 and <= 23).Select(s => s.Control).OrderBy(s => s.ToString()).ToList();
        _servo.SelectionChanged += (_, _) =>
        {
            if (_restoringSelection) return;
            try { CommitPhysicalTiming(); SelectServo(); }
            catch (Exception ex) { _status.Text = ex.Message; _restoringSelection = true; _servo.SelectedItem = _editingServo?.Control; _restoringSelection = false; }
        }; _servo.SelectedIndex = 0;
        _from.TextChanged += (_, _) => _prepared.IsChecked = false;
        _to.TextChanged += (_, _) => _prepared.IsChecked = false;
        _results.SelectionChanged += (_, _) => DrawPlot(); _plot.SizeChanged += (_, _) => DrawPlot();
        _start.IsEnabled = connected; _start.Click += Start; _abort.Click += (_, _) => { _cancel?.Cancel(); _status.Text = "Abort requested; waiting for the current serial read and selected stop action."; };
        _save.Click += (_, _) => { try { ReadSettings(); _data.Save(_root); _saved(); RefreshRows(); _status.Text = "Settings and physical times saved."; } catch (Exception ex) { _status.Text = ex.Message; } };
        if (!connected) _status.Text = "Maestro not connected. You can review results and edit physical estimates; connect the Maestro before opening a calibration run.";
        Closing += (_, e) => { if (_cancel != null) { e.Cancel = true; _closePending = true; _cancel.Cancel(); _status.Text = "Stopping calibration before closing…"; } };
    }
    private static TextBox Box(string text = "") => new() { Text = text, Width = 76, VerticalContentAlignment = VerticalAlignment.Center };
    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
    private static void AddRow(Panel panel, params UIElement[] controls)
    { var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) }; foreach (var control in controls) { if (control is FrameworkElement f) f.Margin = new Thickness(0, 0, 12, 0); row.Children.Add(control); } panel.Children.Add(row); }
    private void AddColumn(string title, string path, double width, bool edit = false, string format = null) => _results.Columns.Add(new DataGridTextColumn { Header = title, Width = width, IsReadOnly = !edit, Binding = new Binding(path) { Mode = edit ? BindingMode.TwoWay : BindingMode.OneWay, StringFormat = format, ValidatesOnExceptions = true } });
    private static double Number(TextBox box, double min, double max)
    { if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number) && !double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || !double.IsFinite(number) || number < min || number > max) throw new InvalidOperationException($"Enter a number between {min} and {max}."); return number; }
    private static int Integer(TextBox box, int min, int max)
    {
        double value = Number(box, min, max);
        if (value != Math.Truncate(value)) throw new InvalidOperationException("Test pulses, repeats and settle milliseconds must be whole numbers.");
        return (int)value;
    }
    private void CommitPhysicalTiming()
    {
        if (!_results.CommitEdit(DataGridEditingUnit.Cell, true) || !_results.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidOperationException("Correct the physical time before changing servo or saving.");
        if (_editingServo == null) return;
        double? ceiling = string.IsNullOrWhiteSpace(_ceiling.Text) ? null : Number(_ceiling, .01, 1000000);
        var timing = _data.Physical.FirstOrDefault(p => p.Control == _editingServo.Control);
        if (timing == null) { timing = new() { Control = _editingServo.Control }; _data.Physical.Add(timing); }
        timing.AssumedTravelDegrees = Number(_travel, .01, 3600);
        timing.CeilingUsPerSecond = ceiling; timing.Notes = _notes.Text;
    }
    private void ReadSettings()
    {
        if (!_steppers.CommitEdit(DataGridEditingUnit.Cell, true) || !_steppers.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidOperationException("Correct the stepper travel time before saving.");
        if (!_results.CommitEdit(DataGridEditingUnit.Cell, true) || !_results.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidOperationException("Correct the physical time before saving.");
        _data.UseInUrdf = _use.IsChecked == true; _data.MiniMaestro = _mini.IsChecked == true; _data.PeriodMs = Number(_period, 3, 100);
        CommitPhysicalTiming();
        _data.Validate();
        _data.RegeneratePredictions(_config);
    }
    private void SelectServo()
    {
        if (Selected == null) return;
        _editingServo = Selected;
        int span = Math.Max(10, (Selected.MaxPwm - Selected.MinPwm) / 5);
        int from = Math.Clamp(Selected.DefaultPwm, Selected.MinPwm, Math.Max(Selected.MinPwm, Selected.MaxPwm - span));
        _from.Text = from.ToString(); _to.Text = Math.Min(Selected.MaxPwm, from + span).ToString(); _prepared.IsChecked = false;
        var timing = _data.Physical.FirstOrDefault(p => p.Control == Selected.Control);
        _ceiling.Text = timing?.CeilingUsPerSecond?.ToString(CultureInfo.InvariantCulture) ?? "";
        _travel.Text = (timing?.AssumedTravelDegrees ?? 180).ToString(CultureInfo.InvariantCulture);
        _fallback.Text = "Speed 0 / Accel 0 fallback: " + SpeedCalibrationData.DefaultSpeedDescription(Selected.Control);
        _notes.Text = timing?.Notes ?? ""; RefreshRows();
    }
    private sealed record ResultRow(SpeedCalibrationResult Result, string Status);
    private void RefreshRows()
    {
        if (Selected == null) return;
        string stamp = _data.Fingerprint(Selected);
        double? ceiling = _data.Physical.FirstOrDefault(p => p.Control == Selected.Control)?.CeilingUsPerSecond;
        _results.ItemsSource = _data.Results.Where(r => r.Control == Selected.Control).OrderBy(r => r.Fingerprint != stamp).ThenByDescending(r => r.IsPredicted).ThenBy(r => r.IsPredicted ? (int)r.Setting : 0).ThenByDescending(r => r.MeasuredUtc)
            .Select(r => new ResultRow(r, r.Fingerprint != stamp ? "STALE — settings changed" : r.Evidence + (ceiling > 0 && r.Rate > ceiling ? " · above estimate" : ""))).ToList();
        if (_results.Items.Count > 0) _results.SelectedIndex = 0;
    }
    private void DrawPlot()
    {
        _plot.Children.Clear();
        if (_results.SelectedItem is not ResultRow row || row.Result.Samples.Count < 2 || _plot.ActualWidth <= 0) return;
        var points = row.Result.Samples; double time = Math.Max(.001, points[^1].Seconds), min = points.Min(p => p.Pulse), span = Math.Max(1, points.Max(p => p.Pulse) - min);
        var line = new Polyline { Stroke = Brushes.LightSkyBlue, StrokeThickness = 2 };
        var caption = new TextBlock { Text = $"{min:0.##}–{min + span:0.##} µs  ·  {points[^1].Seconds:0.000} s", FontSize = 12, Foreground = Brushes.LightSkyBlue };
        Canvas.SetLeft(caption, 8); Canvas.SetTop(caption, 2); _plot.Children.Add(caption);
        foreach (var p in points) line.Points.Add(new Point(8 + p.Seconds / time * (_plot.ActualWidth - 16), 125 - (p.Pulse - min) / span * 105));
        _plot.Children.Add(line);
    }
    private async void Start(object sender, RoutedEventArgs e)
    {
        if (Selected == null || _cancel != null) return;
        try
        {
            ReadSettings();
            var plan = new SpeedCalibrationPlan(Integer(_from, 500, 2400), Integer(_to, 500, 2400), Integer(_repeats, 1, 5), _short.IsChecked == true,
                _speeds.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToArray(), _abortMode.SelectedIndex == 1, _prepared.IsChecked == true, Integer(_settle, 200, 10000));
            SpeedCalibrationRunner.Validate(Selected, plan); _data.Save(_root);
            _cancel = new(); _saveError = null; _options.IsEnabled = _save.IsEnabled = _start.IsEnabled = false; _results.IsReadOnly = true; _abort.IsEnabled = true;
            ((TabItem)_tabs.Items[1]).IsEnabled = false;
            var progress = new Progress<SpeedCalibrationProgress>(p =>
            {
                _status.Text = p.Stage + $" · output {p.Pulse:0.00} µs";
                if (p.Completed != null)
                {
                    _data.Results.Add(p.Completed);
                    try { _data.Save(_root); RefreshRows(); }
                    catch (Exception ex) { _saveError = ex.Message; _cancel?.Cancel(); }
                }
            });
            await _run(Selected, plan, progress, _cancel.Token);
            _status.Text = "Calibration finished at the reviewed test start. Completed traverses were saved; enable calibrated URDF motion and save to use them.";
        }
        catch (OperationCanceledException) { _status.Text = "Calibration aborted. Completed traverses are retained; no return-home move was sent."; }
        catch (Exception ex) { _status.Text = "Calibration stopped: " + ex.Message + " Check the mechanism; a communication failure can prevent the requested stop action."; }
        finally
        {
            _cancel?.Dispose(); _cancel = null; _options.IsEnabled = _save.IsEnabled = _start.IsEnabled = true; _results.IsReadOnly = false; _abort.IsEnabled = false;
            ((TabItem)_tabs.Items[1]).IsEnabled = true;
            if (_saveError != null) _status.Text = "Could not save calibration: " + _saveError;
            _saved(); if (_closePending) Close();
        }
    }

    private void BuildStepperTab()
    {
        var panel = new DockPanel { Margin = new Thickness(16) };
        panel.SetResourceReference(Panel.BackgroundProperty, "AppBackground");
        var help = new TextBlock { Text = "Eye Pop travel timing · default 1.1 seconds to fully extend or retract (0–2000). Select a channel row and edit either time. Partial moves use the matching fraction of full travel. These estimates control URDF motion; Tic hardware settings are unchanged.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        help.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
        DockPanel.SetDock(help, Dock.Top); panel.Children.Add(help);
        var use = new CheckBox { Content = "Use calibrated motion in URDF", Margin = new Thickness(0, 0, 0, 12) };
        use.SetResourceReference(ForegroundProperty, "PrimaryText");
        use.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = _use, Mode = BindingMode.TwoWay });
        DockPanel.SetDock(use, Dock.Top); panel.Children.Add(use);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var save = new Button { Content = "Save timing", Padding = new Thickness(14, 6, 14, 6) };
        var status = new TextBlock { Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        save.Click += (_, _) => { try { ReadSettings(); _data.Save(_root); _saved(); status.Text = "Timing saved."; } catch (Exception ex) { status.Text = ex.Message; } };
        var close = new Button { Content = "Close", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 6, 14, 6) }; close.Click += (_, _) => Close();
        buttons.Children.Add(status); buttons.Children.Add(save); buttons.Children.Add(close); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        foreach (var (label, property, readOnly) in new[] { ("Eye Pop channel", "Control", true), ("Full extension (s)", "ExtendSeconds", false), ("Full retraction (s)", "RetractSeconds", false) })
            _steppers.Columns.Add(new DataGridTextColumn { Header = label, IsReadOnly = readOnly, MinWidth = 230, Width = new DataGridLength(1, DataGridLengthUnitType.Star), Binding = new Binding(property) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, ValidatesOnExceptions = true } });
        _steppers.SetResourceReference(DataGrid.RowBackgroundProperty, "ControlBackground"); _steppers.SetResourceReference(ForegroundProperty, "PrimaryText");
        _steppers.ItemsSource = _data.Steppers; _steppers.SelectedIndex = 0; panel.Children.Add(_steppers);
        _tabs.Items.Add(new TabItem { Header = "Stepper Motors", Content = panel, FontSize = 14 });
    }
}
