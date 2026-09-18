using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator;

public partial class MainWindow
{
    private void CommandSpline_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CommandInspectorRow row || !row.CanSpline) return;
        bool enabled = ((CheckBox)sender).IsChecked == true;
        if (!ApplyOpenCommandEditor()) { UpdateCommandsAtPointList(); return; }
        if (IsRunning) PausePlayback();
        PushUndo($"{(enabled ? "Enable" : "Disable")} {row.Command.Servo} spline");
        var splines = SplineServosEnabled().ToHashSet();
        foreach (var servo in Enum.GetValues<ServoNames>().Where(s => SplineBreakOperations.Curve(s) == SplineBreakOperations.Curve(row.Command.Servo)))
            if (enabled) splines.Add(servo); else splines.Remove(servo);
        ApplySplineSettings(splines);
        RefreshAfterEdit();
        e.Handled = true;
    }

    private void CommandBreak_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CommandInspectorRow row)
            SetCommandBreak(row.Command.Servo, ServoCommand.TimeKey(row.Command.OffsetSeconds), ((CheckBox)sender).IsChecked == true);
        e.Handled = true;
    }

    private void SetCommandBreak(ServoNames servo, double time, bool value)
    {
        if (!ApplyOpenCommandEditor()) { UpdateCommandsAtPointList(); return; }
        var command = _doc.Commands.LastOrDefault(c => c.Servo == servo && !c.Control.HasValue &&
            !c.Disable && ServoCommand.TimeKey(c.OffsetSeconds) == time);
        if (command == null || !SplineServosEnabled().Contains(servo) || command.BreakSpline == value) return;
        if (IsRunning) PausePlayback();
        PushUndo($"{(value ? "Set" : "Clear")} {servo} spline break at {time:F3} s");
        command.BreakSpline = value;
        RefreshAfterEdit();
    }

    private void ModifiedBreaks_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ModifiedCommandControl control || !PrepareGroupEdit()) return;
        bool value = (sender as FrameworkElement)?.Tag?.ToString() == "Set";
        var commands = SelectedGroupCommands().Where(c => c.Servo == control.Servo && c.Control == control.Control &&
            SplineBreakOperations.SupportsSpline(c) && !c.Disable && SplineServosEnabled().Contains(c.Servo)).ToArray();
        if (commands.Any(c => c.BreakSpline != value))
            ApplySelectedGroupEdit($"{(value ? "Set" : "Clear")} selected {control.NameText} spline breaks",
                () => { foreach (var c in commands) c.BreakSpline = value; }, Waveform.SelectedMarkers);
        e.Handled = true;
    }

    private void Spline_PointRightClicked(ServoNames servo, double time)
    {
        var command = _doc.Commands.LastOrDefault(c => c.Servo == servo && !c.Control.HasValue &&
            !c.Disable && ServoCommand.TimeKey(c.OffsetSeconds) == time);
        if (command == null) return;
        var menu = new ContextMenu { PlacementTarget = Spline };
        var item = new MenuItem { Header = "Break Spline", IsCheckable = true, IsChecked = command.BreakSpline };
        item.Click += (_, _) => SetCommandBreak(servo, time, item.IsChecked);
        menu.Items.Add(item);
        menu.IsOpen = true;
    }
}
