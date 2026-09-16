using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerSafeguardChecks()
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MainWindow).GetField(name, flags).GetValue(window);
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, args);
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        head.SetServoConfiguration(ServoConfiguration.CreateDefault()); head.SetSpeedCalibration(new() { UseInUrdf = false });
        var profiles = (Dictionary<ControllerKind, ControllerProfile>)Get("_controllerProfiles");
        foreach (var kind in Enum.GetValues<ControllerKind>()) profiles[kind] = ControllerProfile.Defaults(kind);
        var connections = (Dictionary<ControllerKind, ControlConnectionState>)Get("_controllerConnections");
        var display = (ControllerMappingDisplay)window.FindName("ActiveControllerMapping");
        var commands = (DockPanel)window.FindName("CommandsAtPointContent");
        Call("RefreshActiveControllerMapping");
        Check(commands.Visibility == Visibility.Visible && display.Visibility == Visibility.Collapsed, "Command list remains when no controller is connected");
        connections[ControllerKind.Xbox].Connected = true;
        for (int layer = 0; layer < 4; layer++)
        {
            Set("_lastXboxSample", new ControllerSample { Connected = true, LeftShoulder = (layer & 1) != 0, RightShoulder = (layer & 2) != 0 });
            Call("RefreshActiveControllerMapping");
            Check(commands.Visibility == Visibility.Collapsed && display.Diagram.ReadOnly && display.Diagram.MappingLayer == layer && ReferenceEquals(display.Diagram.Mappings, profiles[ControllerKind.Xbox].Layers[layer]), "Enabled Xbox replaces commands with the actual shoulder-selected bank");
        }
        connections[ControllerKind.Steam].Connected = true; Set("_enabledController", ControllerKind.Steam);
        Set("_lastSteamSample", new ControllerSample { Connected = true, RightShoulder = true }); Call("RefreshActiveControllerMapping");
        Check(display.Diagram.Kind == ControllerKind.Steam && display.Diagram.MappingLayer == 2, "Most recently active Steam controller owns the mapping display when both are enabled");
        profiles[ControllerKind.Steam].NoMux = true; profiles[ControllerKind.Steam].Validate(); Call("RefreshActiveControllerMapping");
        Check(display.Diagram.NoMux && ReferenceEquals(display.Diagram.Mappings, profiles[ControllerKind.Steam].NoMuxMappings) && display.LayerText.StartsWith("No MUX"), "Steam No MUX displays the shoulder-mappable bank");
        string previews = Path.Combine(Environment.CurrentDirectory, "controller-previews"); Directory.CreateDirectory(previews);
        RenderControl(display, Path.Combine(previews, "ActiveSteamMapping.png"), 1060, 620);
        RenderControl(display.Diagram, Path.Combine(previews, "ActiveSteamMappingFull.png"), (int)Math.Ceiling(display.Diagram.Width), (int)display.Diagram.Height);
        connections[ControllerKind.Steam].SetEnabled(false); Call("RefreshActiveControllerMapping");
        Check(display.Diagram.Kind == ControllerKind.Xbox, "Disabling Steam restores the other enabled controller mapping");
        RenderControl(display, Path.Combine(previews, "ActiveXboxMapping.png"), 900, 550);
        RenderControl(display.Diagram, Path.Combine(previews, "ActiveXboxMappingFull.png"), (int)Math.Ceiling(display.Diagram.Width), (int)display.Diagram.Height);
        connections[ControllerKind.Xbox].Connected = false; Call("RefreshActiveControllerMapping");
        Check(commands.Visibility == Visibility.Visible && display.Visibility == Visibility.Collapsed, "Disconnect restores commands without discarding the list");

        head.SetCollisionWarningsEnabled(false);
        var before = System.Text.Json.JsonSerializer.Serialize(head.CapturePose());
        ServoCommand blocked = null;
        foreach (var servo in new[] { ServoNames.FlapTiltUp, ServoNames.FlapsOpen, ServoNames.NoseBody, ServoNames.NoseBasket, ServoNames.BothEyePop })
        foreach (int value in new[] { ServoCommand.RangeFor(servo).Min, ServoCommand.RangeFor(servo).Max })
        {
            var candidate = new ServoCommand { Servo = servo, NumericValue = value };
            if (!head.ControllerPathClear(new[] { candidate }, out string reason))
            {
                Check(reason.Contains("collision"), "Modeled collision geometry is available for safeguard checks"); blocked = candidate; break;
            }
        }
        Check(blocked != null, "Real URDF collision geometry rejects at least one interfering flap path");
        Check(before == System.Text.Json.JsonSerializer.Serialize(head.CapturePose()) && !head.CollisionWarningsEnabled, "Trial collision checks preserve displayed pose and warning preference");
        Set("_enabledController", ControllerKind.Xbox);
        Check(!(bool)Call("AllowControllerTargets", (object)new[] { blocked }), "Controller collision preflight rejects output before queueing");
        Check(((ControllerPreviewUpdates)Get("_controllerPreview")).Count == 0 && (long)Get("_safeguardBlockedUntil") > Environment.TickCount64, "Blocked output clears pending preview and lights the safeguard button");
        var safeguardButton = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("CollisionSafeguardButton");
        RenderControl(safeguardButton, Path.Combine(previews, "CollisionSafeguardBlocked.png"), 130, 50);
        Check(((Border)safeguardButton.Template.FindName("SafeguardChrome", safeguardButton)).Background == Brushes.DarkOrange, "The checked button actually renders orange while preventing a collision");
        bool stopped = false;
        head.SetSpeedCalibration(new()); head.CollisionSafeguardActive = () => true; head.CollisionSafeguardBlocked = _ => stopped = true;
        var motionScene = (UrdfScene)typeof(RobotHeadView).GetField("_scene", flags).GetValue(head);
        var jointsBefore = motionScene.CaptureMotionState().JointPositions;
        head.SetServo(blocked.Servo, blocked.NumericValue); head.AdvanceCalibratedMotion(60);
        Check(stopped && jointsBefore.All(p => Math.Abs(motionScene.CaptureMotionState().JointPositions[p.Key] - p.Value) < 1e-9), "Calibrated motion rejects a colliding step before rendering any changed joint");
        head.CollisionSafeguardActive = null; head.CollisionSafeguardBlocked = null;
        var settings = (FocusControlSettings)Get("_focusControl"); settings.CollisionSafeguard = false;
        Check((bool)Call("AllowControllerTargets", (object)new[] { blocked }), "Safeguard Off permits the same command");
        settings.CollisionSafeguard = true;
        var scene = typeof(RobotHeadView).GetField("_scene", flags).GetValue(head);
        typeof(RobotHeadView).GetField("_scene", flags).SetValue(head, null);
        Check(!(bool)Call("AllowControllerTargets", (object)new[] { blocked }), "Missing collision model fails closed for controller output");
        typeof(RobotHeadView).GetField("_scene", flags).SetValue(head, scene);
    }
    private static void RenderControl(FrameworkElement control, string path, int width, int height)
    {
        control.Measure(new Size(width, height)); control.Arrange(new Rect(0, 0, width, height)); control.UpdateLayout();
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(control);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file);
    }
}
