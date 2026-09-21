using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator;

public sealed class FocusControlWindow : Window
{
    public FocusControlWindow(FocusControlSettings settings, Action<FocusControlSettings> save)
    {
        Title = "Focus Control"; Width = 540; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 48);
        SetResourceReference(BackgroundProperty, "AppBackground"); SetResourceReference(ForegroundProperty, "PrimaryText");
        var panel = new StackPanel { Margin = new Thickness(24) };
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(BackgroundProperty, "AppBackground"); Content = scroll;
        panel.Children.Add(new TextBlock { Text = "Focus Control", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Allow control while the Animation Editor is out of focus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 18) });
        CheckBox Choice(string label, bool value, FrameworkElement icon = null)
        {
            object content = label;
            if (icon != null)
            {
                icon.Margin = new Thickness(0, 0, 8, 0);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(icon);
                row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                content = row;
            }
            var box = new CheckBox { Content = content, IsChecked = value, FontSize = 16, Margin = new Thickness(0, 6, 0, 6) };
            box.SetResourceReference(ForegroundProperty, "PrimaryText"); panel.Children.Add(box); return box;
        }
        var xbox = Choice("X-Box Controller", settings.XboxController, DeviceIconFactory.Xbox());
        var xboxUrdf = Choice("Drive URDF model", settings.XboxUrdf); xboxUrdf.Margin = new Thickness(26, 4, 0, 4);
        var xboxPhysical = Choice("Drive Physical Model", settings.XboxPhysical); xboxPhysical.Margin = new Thickness(26, 4, 0, 12);
        var steam = Choice("Steam Controller", settings.SteamController, DeviceIconFactory.Steam());
        var steamUrdf = Choice("Drive URDF model", settings.SteamUrdf); steamUrdf.Margin = new Thickness(26, 4, 0, 4);
        var steamPhysical = Choice("Drive Physical Model", settings.SteamPhysical); steamPhysical.Margin = new Thickness(26, 4, 0, 12);
        void RefreshChoices()
        {
            xboxUrdf.IsEnabled = xboxPhysical.IsEnabled = xbox.IsChecked == true;
            steamUrdf.IsEnabled = steamPhysical.IsEnabled = steam.IsChecked == true;
        }
        xbox.Checked += (_, _) => RefreshChoices(); xbox.Unchecked += (_, _) => RefreshChoices();
        steam.Checked += (_, _) => RefreshChoices(); steam.Unchecked += (_, _) => RefreshChoices(); RefreshChoices();
        var movie = Choice("Movie Playback", settings.MoviePlayback, DeviceIconFactory.Keyboard());
        var deck = Choice("Stream Deck / Library API", settings.StreamDeck, DeviceIconFactory.StreamDeck());
        var deckUrdf = Choice("Drive URDF model", settings.StreamDeckUrdf); deckUrdf.Margin = new Thickness(26, 4, 0, 4);
        var deckPhysical = Choice("Drive Physical Model", settings.StreamDeckPhysical); deckPhysical.Margin = new Thickness(26, 4, 0, 12);
        void RefreshDeck() => deckUrdf.IsEnabled = deckPhysical.IsEnabled = deck.IsChecked == true;
        deck.Checked += (_, _) => RefreshDeck(); deck.Unchecked += (_, _) => RefreshDeck(); RefreshDeck();
        panel.Children.Add(new TextBlock { Text = "Choose each controller's background destinations independently. Physical output also requires Drive HW to be on. Connections stay open when focus changes. Editor dialogs pause normal controller output; controller configuration has its own Test URDF mode.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "After starting a movie, plain arrow keys control it from other apps, including while paused or waiting between cues. Those keys are reserved for the movie while it is in the background.\n\n↑ Play/pause   → Next cue\n← Restart/previous   ↓ Stop and rewind\n\nThe ■ Stop button, starting standalone sequence/Library playback, closing the movie, or disabling Movie Playback here releases background arrows. Audio playback itself continues when focus changes.", TextWrapping = TextWrapping.Wrap });
        var error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) }; panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) }; panel.Children.Add(buttons);
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false; buttons.Children.Add(cancel);
        var apply = new Button { Content = "Save", IsDefault = true, Padding = new Thickness(14, 7, 14, 7) }; buttons.Children.Add(apply);
        apply.Click += (_, _) =>
        {
            try { save(new() { XboxController = xbox.IsChecked == true, SteamController = steam.IsChecked == true, MoviePlayback = movie.IsChecked == true,
                XboxUrdf = xboxUrdf.IsChecked == true, XboxPhysical = xboxPhysical.IsChecked == true,
                SteamUrdf = steamUrdf.IsChecked == true, SteamPhysical = steamPhysical.IsChecked == true,
                StreamDeck = deck.IsChecked == true, StreamDeckUrdf = deckUrdf.IsChecked == true, StreamDeckPhysical = deckPhysical.IsChecked == true }); DialogResult = true; }
            catch (Exception ex) { error.Text = "Could not save Focus Control: " + ex.Message; }
        };
    }
}
