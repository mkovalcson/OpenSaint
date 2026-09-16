using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using ServoAnimator;

internal static partial class Program
{
    private static void ConnectedControlChecks(MainWindow window, Func<EditorApiRequest, object> call)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        object Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, EditorApiServer.Json);
        JsonElement Presence(bool connected) => Json(call(new() { Method = "stream_deck_status", StreamDeckSession = "test-deck", Enabled = connected })).GetProperty("state").GetProperty("streamDeck");
        EditorApiRequest Request(string method, bool deck = true) => new() { Method = method, Name = "DirectWave.json", Loop = true,
            UseEditorOutputs = true, StreamDeckSession = deck ? "test-deck" : null, RequestId = Guid.NewGuid().ToString("N"),
            ExpectedToken = Json(call(new() { Method = "get_status" })).GetProperty("state").GetProperty("libraryToken").GetString() };
        var button = (ToggleButton)window.FindName("StreamDeckButton");
        Check(Presence(true).GetProperty("enabled").GetBoolean() && button.IsChecked == true, "A live Stream Deck connection automatically enables its Hardware button.");
        call(Request("play_library_sequence"));
        button.IsChecked = false; button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(Get("_controllerLibraryRun") == null && !Presence(true).GetProperty("enabled").GetBoolean(), "Disabling Stream Deck stops its sequence, and polling cannot reenable it.");
        Presence(false);
        Check(!Presence(true).GetProperty("enabled").GetBoolean(), "Stream Deck reconnect preserves a manual disable.");
        bool rejected = false; try { call(Request("play_library_sequence")); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Disabled Stream Deck keys cannot start playback.");
        call(Request("play_library_sequence", false));
        Check(Get("_controllerLibraryRun") != null, "Disabling Stream Deck does not disable other editor API clients.");
        call(Request("stop_library", false));
        button.IsChecked = true; button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        call(Request("play_library_sequence")); Presence(false);
        Check(Get("_controllerLibraryRun") == null && button.IsChecked == false, "Disconnecting Stream Deck stops its output and clears the button.");
        Check(Presence(true).GetProperty("enabled").GetBoolean(), "A connection loss without manual disable reenables on reconnect.");
        ((Dictionary<string, long>)Get("_streamDeckClients"))["test-deck"] = Environment.TickCount64 - 6000;
        Invoke("RefreshStreamDeckConnection");
        Check(button.IsChecked == false, "An expired plugin heartbeat clears Stream Deck availability.");

        var profiles = (Dictionary<ControllerKind, ControllerProfile>)Get("_controllerProfiles");
        foreach (var kind in Enum.GetValues<ControllerKind>())
        {
            profiles[kind] = ControllerProfile.Empty(kind);
            profiles[kind].Layers[0]["A"] = new() { Target = "Servo:NeckTurn", Mode = ControllerMapMode.Set, High = kind == ControllerKind.Xbox ? 15 : -15 };
        }
        ControllerSample Sample(uint id, double value = 0, bool connected = true) => new() { DeviceId = id, Connected = connected, Values = new() { ["A"] = value } };
        void Poll(ControllerSample xbox, ControllerSample steam) => Invoke("ProcessControllerSamples", xbox, steam, 0.025);
        var xboxButton = (ToggleButton)window.FindName("XboxControllerButton");
        var steamButton = (ToggleButton)window.FindName("SteamControllerButton");
        var preview = (ControllerPreviewUpdates)Get("_controllerPreview");
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        Poll(Sample(10), Sample(20));
        Check(xboxButton.IsChecked == true && steamButton.IsChecked == true, "Both gamepads automatically become ready when connected.");
        Poll(Sample(10, 1), Sample(20)); preview.Flush();
        Check(head.CapturePose().NeckTurn == 15, "An automatically enabled Xbox controller drives the URDF.");
        Poll(Sample(10), Sample(20, 1)); preview.Flush();
        Check(head.CapturePose().NeckTurn == -15, "Steam input takes control without disabling Xbox readiness.");
        steamButton.IsChecked = false; steamButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Poll(Sample(10), Sample(20, connected: false)); Poll(Sample(10), Sample(20));
        Check(xboxButton.IsChecked == true && steamButton.IsChecked == false, "Manually disabling Steam survives reconnect and leaves Xbox enabled.");
        Poll(Sample(10, connected: false), Sample(20)); Poll(Sample(10, 1), Sample(20)); preview.Flush();
        Check(xboxButton.IsChecked == true && head.CapturePose().NeckTurn == -15, "Auto-reconnected gamepads wait for neutral before accepting held controls.");
        Poll(Sample(10), Sample(20)); Poll(Sample(10, 1), Sample(20)); preview.Flush();
        Check(head.CapturePose().NeckTurn == 15, "A neutral release rearms an auto-reconnected controller.");
        Invoke("DisableControllerInput", true);
        Poll(Sample(10), Sample(20));
        Check(xboxButton.IsChecked == false && steamButton.IsChecked == false, "Disable servos and input error shutdowns cannot be undone by the next poll.");
    }
}
