using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ServoAnimator;

internal static class SequenceTrigger
{
    public static string FromKey(Key key, ModifierKeys modifiers)
    {
        if (key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or
            Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return "";
        // Keep transport and document shortcuts available.
        if (key is Key.Up or Key.Down or Key.Left or Key.Right ||
            (key == Key.Space && modifiers == ModifierKeys.None) ||
            ((key is Key.S or Key.O) && (modifiers == ModifierKeys.Control ||
                modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))) return "";
        string name = key >= Key.D0 && key <= Key.D9 ? ((int)key - (int)Key.D0).ToString() : key.ToString();
        return (modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "") +
            (modifiers.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") +
            (modifiers.HasFlag(ModifierKeys.Shift) ? "Shift+" : "") +
            (modifiers.HasFlag(ModifierKeys.Windows) ? "Win+" : "") + name;
    }
}

internal sealed class SequenceTriggerWindow : Window
{
    public string Trigger { get; private set; }

    public SequenceTriggerWindow(Window owner, string current, Func<string, bool> isDuplicate)
    {
        Owner = owner;
        Title = "Assign trigger";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Trigger = current ?? "";
        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "Click the field and press a key or modifier combination.", TextWrapping = TextWrapping.Wrap });
        var capture = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left, IsReadOnly = true, Text = Trigger, Margin = new Thickness(0, 10, 0, 8), Padding = new Thickness(8) };
        panel.Children.Add(capture);
        var message = new TextBlock { Text = "Arrow keys, Space, and document shortcuts are reserved.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(message);
        capture.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            string candidate = SequenceTrigger.FromKey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
            if (candidate.Length == 0) { message.Text = "Choose a non-reserved key with optional modifiers."; return; }
            if (isDuplicate(candidate)) { message.Text = "That trigger is already assigned to another sequence."; return; }
            Trigger = candidate;
            capture.Text = candidate;
            message.Text = "Click Assign to save this trigger.";
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        foreach (string label in new[] { "Clear", "Assign", "Cancel" })
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0) };
            button.Click += (_, _) =>
            {
                if (label == "Clear") { Trigger = ""; capture.Text = ""; capture.Focus(); }
                else DialogResult = label == "Assign";
            };
            buttons.Children.Add(button);
        }
        panel.Children.Add(buttons);
        Loaded += (_, _) => capture.Focus();
    }
}
