using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerLayoutChecks()
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow(); // Never shown: no SDL polling or physical output.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        var profiles = (Dictionary<ControllerKind, ControllerProfile>)Get("_controllerProfiles");
        foreach (var kind in Enum.GetValues<ControllerKind>()) profiles[kind] = ControllerProfile.Defaults(kind);
        var connections = (Dictionary<ControllerKind, ControlConnectionState>)Get("_controllerConnections");
        var commands = (DockPanel)window.FindName("CommandsAtPointContent");
        var docked = (ControllerMappingDisplay)window.FindName("ActiveControllerMapping");
        var separate = (ControllerMappingDisplay)window.FindName("UndockedControllerMapping");
        var host = (Canvas)window.FindName("UndockedControllerMappingHost");
        var panel = (Border)window.FindName("CommandsAtPointPanel");
        var column = (ColumnDefinition)window.FindName("UrdfEditorColumn");
        var steamButton = (ToggleButton)window.FindName("SteamControllerButton");
        void ClickSteam() => Call("ControllerEnable_Click", steamButton, new RoutedEventArgs());
        void Dock(bool undocked) { Set("_urdfUndocked", undocked); Call("ApplyUrdfDockLayout"); }
        Dock(true);
        Check(commands.Visibility == Visibility.Visible && host.Visibility == Visibility.Collapsed && Grid.GetColumnSpan(panel) == 3,
            "Undocked with no controller leaves full-width Commands");
        connections[ControllerKind.Xbox].Connected = true;
        Set("_lastXboxSample", new ControllerSample { Connected = true, LeftShoulder = true });
        Call("RefreshActiveControllerMapping");
        Check(commands.Visibility == Visibility.Visible && docked.Visibility == Visibility.Collapsed && host.Visibility == Visibility.Visible,
            "Undocked Xbox displays Commands and a separate mapping together");
        Check(Grid.GetColumn(host) == 2 && Grid.GetColumnSpan(panel) == 1 && separate.Diagram.Kind == ControllerKind.Xbox && separate.Diagram.MappingLayer == 1,
            "Middle pane displays the current Xbox MUX bank");
        column.Width = new GridLength(2, GridUnitType.Star); Call("RefreshActiveControllerMapping");
        Check(column.Width.Value == 2, "Controller polls preserve resized pane widths");
        connections[ControllerKind.Xbox].SetEnabled(false); Call("RefreshActiveControllerMapping");
        Check(host.Visibility == Visibility.Visible, "A green connected controller keeps its mapping when output is disabled");
        ClickSteam();
        Check(separate.Diagram.Kind == ControllerKind.Steam && commands.Visibility == Visibility.Visible && steamButton.IsChecked == true,
            "Disconnected Steam button selects the Steam mapping beside Commands");
        Check(!connections[ControllerKind.Steam].Enabled && !connections[ControllerKind.Xbox].Enabled && connections[ControllerKind.Xbox].ManuallyDisabled
            && Get("_enabledController") == null && Get("_controllers") == null,
            "Mapping preview neither enables output nor opens controller hardware");
        Call("SetControllerStatus", ControllerKind.Steam, new ControllerSample());
        Check(steamButton.IsChecked == true, "Polling retains the disconnected preview button selection");
        ClickSteam();
        Check(separate.Diagram.Kind == ControllerKind.Xbox && steamButton.IsChecked == false, "Second Steam click restores the connected Xbox mapping");
        connections[ControllerKind.Xbox].SetEnabled(true);
        Dock(false);
        Check(host.Visibility == Visibility.Collapsed && docked.Visibility == Visibility.Visible && commands.Visibility == Visibility.Collapsed
            && ((Canvas)window.FindName("UrdfOverlayHost")).Visibility == Visibility.Visible, "Redocking restores the original mapping and URDF layout");
        ClickSteam();
        Check(docked.Diagram.Kind == ControllerKind.Steam, "Disconnected Steam preview also works in the docked layout");
        ClickSteam(); Dock(true);
        connections[ControllerKind.Steam].Connected = true; Set("_enabledController", ControllerKind.Steam); Call("RefreshActiveControllerMapping");
        Check(separate.Diagram.Kind == ControllerKind.Steam, "The active connected Steam controller selects the middle mapping");
        steamButton.IsChecked = false; ClickSteam();
        Check(connections[ControllerKind.Steam].ManuallyDisabled && connections[ControllerKind.Xbox].Enabled, "Connected Steam click still disables only Steam output");
        connections[ControllerKind.Steam].Connected = false;
        connections[ControllerKind.Xbox].Connected = false; Call("RefreshActiveControllerMapping");
        Check(host.Visibility == Visibility.Collapsed && Grid.GetColumnSpan(panel) == 3 && column.Width.Value == 0,
            "Disconnecting all controllers reclaims the mapping pane");
        ClickSteam();
        Check(host.Visibility == Visibility.Visible && separate.Diagram.Kind == ControllerKind.Steam, "Disconnected Steam can open a mapping even without Xbox");
        var root = (DockPanel)window.Content;
        foreach (var size in new[] { (1800, 1000), (1000, 650), (800, 500) })
        {
            for (int pass = 0; pass < 8; pass++)
            {
                root.Measure(new Size(size.Item1, size.Item2)); root.Arrange(new Rect(0, 0, size.Item1, size.Item2)); root.UpdateLayout();
                Call("FitEditorPanels");
            }
            var audio = (Grid)window.FindName("AudioTimelineArea");
            Check(audio.TranslatePoint(new Point(0, audio.ActualHeight), root).Y <= (double)typeof(MainWindow).GetMethod("EditorPanelViewportHeight", flags).Invoke(window, null) + 1,
                $"Undocked mapping keeps the sequence timeline accessible at {size}");
            Check(host.ActualWidth >= 260 && panel.ActualWidth >= 270, "Commands and mapping remain independently accessible");
        }
        string folder = Path.Combine(Environment.CurrentDirectory, "layout-previews"); Directory.CreateDirectory(folder);
        root.Measure(new Size(1800, 1000)); root.Arrange(new Rect(0, 0, 1800, 1000)); root.UpdateLayout(); Call("FitEditorPanels");
        ((RowDefinition)window.FindName("TopEditorRow")).Height = new GridLength(500);
        RenderControl(root, Path.Combine(folder, "UndockedSteamMapping.png"), 1800, 1000);
        ClickSteam();
        Check(host.Visibility == Visibility.Collapsed && commands.Visibility == Visibility.Visible, "Closing offline Steam preview restores Commands alone");
    }
}
