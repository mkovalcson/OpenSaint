using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void FocusControlSettingsChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "j5-focus-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = FocusControlSettings.Load(root);
            Check(settings.MoviePlayback && Enum.GetValues<ControllerKind>().All(k => settings.AllowsOutput(k, false) && settings.AllowsOutput(k, true)),
                "All background permissions default to enabled.");
            settings.XboxUrdf = false; settings.SteamPhysical = false; settings.MoviePlayback = false; settings.Save(root);
            var saved = FocusControlSettings.Load(root);
            Check(!saved.XboxUrdf && saved.XboxPhysical && saved.SteamUrdf && !saved.SteamPhysical && !saved.MoviePlayback,
                "Independent destination and movie settings survive save/load.");
            File.WriteAllText(Path.Combine(root, "FocusControl.json"), "{\"XboxController\":false}");
            saved = FocusControlSettings.Load(root);
            Check(!saved.AllowsOutput(ControllerKind.Xbox, false) && !saved.AllowsOutput(ControllerKind.Xbox, true)
                && saved.AllowsOutput(ControllerKind.Steam, false) && saved.MoviePlayback,
                "Absent settings keep enabled defaults without overriding a saved controller opt-out.");
            File.WriteAllText(Path.Combine(root, "FocusControl.json"), "broken");
            bool rejected = false; try { FocusControlSettings.Load(root); } catch (System.Text.Json.JsonException) { rejected = true; }
            Check(rejected, "Corrupt settings are reported instead of silently enabling controls.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        foreach (bool focused in new[] { false, true })
        foreach (bool background in new[] { false, true })
        {
            Check(FocusControlPolicy.Controller(focused, background, false) == (focused || background), "Focus or the background permission enables controller input.");
            Check(!FocusControlPolicy.Controller(focused, background, true), "Editor dialogs block normal controller output.");
        }
        Check(!FocusControlPolicy.Movie(true, false, false, false, true), "Movie keys are not captured before a movie is started.");
        Check(FocusControlPolicy.Movie(true, true, false, false, true), "An armed background movie session retains arrows between cues and pauses.");
        Check(!FocusControlPolicy.Movie(false, true, false, false, true) && !FocusControlPolicy.Movie(true, true, true, false, true)
            && !FocusControlPolicy.Movie(true, true, false, true, true) && !FocusControlPolicy.Movie(true, true, false, false, false),
            "Disabled, foreground, dialog and closed movie states release global arrows.");
    }

    private static void FocusControlRoutingChecks(MainWindow window)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        var previousSettings = Get("_focusControl");
        var preview = (ControllerPreviewUpdates)Get("_controllerPreview");
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        var settings = new FocusControlSettings(); Set("_focusControl", settings);
        try
        {
            foreach (var kind in Enum.GetValues<ControllerKind>())
            foreach (bool urdf in new[] { false, true })
            foreach (bool physical in new[] { false, true })
            {
                Set("_enabledController", (ControllerKind?)kind);
                settings.XboxUrdf = settings.SteamUrdf = urdf; settings.XboxPhysical = settings.SteamPhysical = physical;
                Call("RefreshControllerOutputPermissions");
                Check((bool)Call("ControllerOutputAllowed", false) == urdf && (bool)Call("ControllerOutputAllowed", true) == physical,
                    $"{kind} independently routes URDF={urdf}, physical={physical} while unfocused.");
                Call("SetPlaybackControlSource", (ControllerKind?)kind);
                Check((bool)Call("PlaybackOutputAllowed", false) == urdf && (bool)Call("PlaybackOutputAllowed", true) == physical,
                    "Playback started by a controller inherits both of its background destination choices.");
                double before = head.CapturePose().NeckTurn;
                double target = before == 37 ? -37 : 37;
                Call("ApplyControllerPosition", "Servo:NeckTurn", target); preview.Flush();
                Check(head.CapturePose().NeckTurn == (urdf ? target : before), "Real controller URDF output respects the destination permission.");
                Call("StartControllerLibrary", new ControllerIntent("Library:pose", 0, "Rest.json")); preview.Flush();
                Check(head.CapturePose().NeckTurn == (urdf ? -25 : before), "Library pose buttons use the same background destination permissions.");
            }
            settings.XboxUrdf = settings.XboxPhysical = true; Set("_enabledController", (ControllerKind?)ControllerKind.Xbox);
            Call("RefreshControllerOutputPermissions");
            long epoch = (long)Get("_controllerOutputEpoch");
            Call("ApplyControllerPosition", "Servo:NeckTurn", 55d);
            settings.XboxUrdf = settings.XboxPhysical = false;
            Call("RefreshControllerOutputPermissions");
            Check(preview.Count == 0 && (long)Get("_controllerOutputEpoch") > epoch && !(bool)Get("_controllerPhysicalOutputAllowed"),
                "Revoking background outputs drops queued URDF work and invalidates pending hardware work.");
            settings.XboxUrdf = settings.XboxPhysical = true; settings.XboxController = false;
            Check(!(bool)Call("ControllerFocusAllowed", ControllerKind.Xbox) && !(bool)Call("ControllerOutputAllowed", false)
                && !(bool)Call("ControllerOutputAllowed", true), "Controller master opt-out blocks both background destinations.");
            Call("SetPlaybackControlSource", (ControllerKind?)ControllerKind.Xbox);
            Check(!(bool)Call("PlaybackOutputAllowed", true), "Controller master opt-out also blocks controller-started physical playback.");
            Call("SetPlaybackControlSource", new object[] { null });
            Check((bool)Call("PlaybackOutputAllowed", true), "Playback explicitly started in the editor remains independent of controller permissions.");
            settings.XboxController = true; window.IsEnabled = false;
            Check(!(bool)Call("ControllerOutputAllowed", false) && !(bool)Call("ControllerOutputAllowed", true), "Modal editor state blocks both normal controller destinations.");
            window.IsEnabled = true;
            using var config = new ConfigWindowLifetime(new ControllerMappingWindow(ControllerProfile.Empty(ControllerKind.Steam), () => new(), _ => { }, allowBackground: () => settings.SteamUrdf));
            settings.SteamUrdf = true;
            Check(config.Window.CanTestWithCurrentFocus, "URDF mapping tests can run in the background when permitted.");
            settings.SteamUrdf = false;
            Check(!config.Window.CanTestWithCurrentFocus, "Background mapping tests stop when URDF permission is disabled.");
            Call("ArmMovieBackgroundControl");
            Check((bool)Get("_movieBackgroundArmed"), "Movie playback can arm background transport.");
            Call("SequenceStop_Click", window, new RoutedEventArgs());
            Check(!(bool)Get("_movieBackgroundArmed"), "Explicit Stop releases the movie session.");
            Call("ArmMovieBackgroundControl"); Call("StartControllerLibrary", new ControllerIntent("Library:pose", 0, "Rest.json"));
            Check(!(bool)Get("_movieBackgroundArmed"), "Starting Library control releases movie hotkeys.");
        }
        finally { window.IsEnabled = true; Set("_focusControl", previousSettings); Set("_enabledController", null); Call("SetPlaybackControlSource", new object[] { null }); Call("ResetControllerMotion"); }

        // Exercise real movie transport without showing the editor or opening hardware.
        string root = ((FolderSettings)Get("_folders")).ConfigFolder;
        string sequencePath = Path.Combine(root, "FocusMovieSequence.json");
        var document = new AnimationDocument { DurationSeconds = 2, Commands = new()
        { new() { Servo = ServoNames.NeckTurn, NumericValue = 10, OffsetSeconds = 0 },
          new() { Servo = ServoNames.NeckTurn, NumericValue = 20, OffsetSeconds = 2 } } };
        document.Save(sequencePath); Set("_doc", document); Set("_jsonPath", sequencePath);
        Set("_moviePath", Path.Combine(root, "FocusMovie.json")); Set("_movieSelectedIndex", 0);
        var items = (List<MovieSequenceItem>)Get("_movieItems");
        items.Clear(); items.Add(new() { FilePath = sequencePath, DurationSeconds = 2 });
        var timeline = (MovieTimelineView)window.FindName("MovieTimeline");
        timeline.SetItems(items); timeline.CursorTime = 0;
        Call("RefreshAfterEdit");
        try
        {
            Call("HandleMovieArrow", Key.Up);
            Check(Get("_mode").ToString() == "Running" && (bool)Get("_movieBackgroundArmed"), "Movie Play arms background arrows and starts playback.");
            Call("HandleMovieArrow", Key.Up);
            Check(Get("_mode").ToString() == "Paused" && (bool)Get("_movieBackgroundArmed"), "Up pauses without losing the background movie session.");
            Call("HandleMovieArrow", Key.Down);
            Check(Get("_mode").ToString() == "Stopped" && timeline.CursorTime == 0 && (bool)Get("_movieBackgroundArmed"), "Down rewinds while retaining background cue control.");
            Call("HandleMovieArrow", Key.Up); Call("StopPlayback", false);
            Check((bool)Get("_movieBackgroundArmed"), "Natural playback completion keeps background cue control armed.");
            Call("SequenceStop_Click", window, new RoutedEventArgs());
            Check(!(bool)Get("_movieBackgroundArmed"), "The explicit Stop button releases background cue control.");
        }
        finally { Call("SequenceStop_Click", window, new RoutedEventArgs()); }

        string folder = Path.Combine(Environment.CurrentDirectory, "controller-previews"); Directory.CreateDirectory(folder);
        var focus = new FocusControlWindow(new(), _ => { });
        var content = (FrameworkElement)focus.Content;
        content.Measure(new Size(540, double.PositiveInfinity));
        content.Arrange(new Rect(0, 0, 540, content.DesiredSize.Height)); content.UpdateLayout();
        var image = new RenderTargetBitmap(540, (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var file = File.Create(Path.Combine(folder, "FocusControl.png"))) encoder.Save(file);
        focus.Close();
        BackgroundMovieKeyChecks();
    }

    private sealed class ConfigWindowLifetime(ControllerMappingWindow window) : IDisposable
    { public ControllerMappingWindow Window => window; public void Dispose() => window.Close(); }

    private static void BackgroundMovieKeyChecks()
    {
        // A hidden native window; no editor startup or real keyboard input is sent.
        var host = new Window(); nint handle = new WindowInteropHelper(host).EnsureHandle();
        var pressed = new List<Key>();
        using var keys = new BackgroundMovieKeys(host, pressed.Add);
        try
        {
            Check(TestRegisterHotKey(handle, 0x4B00, 0x4000, 0x25), "Reserve Left temporarily to exercise a hotkey conflict.");
            keys.SetEnabled(true);
            Check(!keys.Enabled && keys.Error != null, "A conflicting arrow rejects the entire background key set.");
            Check(TestRegisterHotKey(handle, 0x4B01, 0x4000, 0x26), "Hotkey failure rolls back arrows already reserved by the attempted registration.");
            TestUnregisterHotKey(handle, 0x4B00); TestUnregisterHotKey(handle, 0x4B01);
            keys.SetEnabled(false); keys.SetEnabled(true);
            Check(keys.Enabled && keys.Error == null, "All four background movie arrows register successfully.");
            for (int i = 0; i < 4; i++) TestSendMessage(handle, 0x0312, 0x4A00 + i, 0);
            Check(pressed.SequenceEqual(new[] { Key.Up, Key.Right, Key.Left, Key.Down }), "Native WM_HOTKEY dispatch delivers the correct movie arrows.");
            keys.SetEnabled(false); TestSendMessage(handle, 0x0312, 0x4A00, 0);
            Check(pressed.Count == 4 && !keys.Enabled, "Late hotkey messages are ignored after background control is disabled.");
        }
        finally { keys.Dispose(); TestUnregisterHotKey(handle, 0x4B00); TestUnregisterHotKey(handle, 0x4B01); host.Close(); }
    }
    [DllImport("user32.dll", EntryPoint = "RegisterHotKey")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TestRegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")]
    private static extern bool TestUnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint TestSendMessage(nint window, int message, nint wParam, nint lParam);
}
