using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ServoAnimator;

internal static partial class Program
{
    private static void LayoutRecoveryChecks()
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var root = (DockPanel)window.Content;
        window.Content = null; // Attach the visuals without running MainWindow's hardware startup.
        using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Layout checks")
        { Width = 1250, Height = 800, WindowStyle = unchecked((int)0x80000000) });
        presentation.RootVisual = root;
        root.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var grid = (Grid)window.FindName("EditorTimelineGrid");
        var top = (RowDefinition)window.FindName("TopEditorRow");
        var audio = (RowDefinition)window.FindName("AudioTimelineRow");
        var spline = (RowDefinition)window.FindName("SplineTimelineRow");
        var audioArea = (Grid)window.FindName("AudioTimelineArea");
        var divider = (GridSplitter)window.FindName("CommandsTimelineSplitter");
        var robot = (Border)window.FindName("RobotHeadEmbeddedBorder");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var doc = new AnimationDocument { Commands = Enum.GetValues<ServoNames>()
            .Select(servo => new ServoCommand { Servo = servo, OffsetSeconds = 0, NumericValue = 0 }).ToList() };
        typeof(MainWindow).GetField("_doc", flags).SetValue(window, doc);
        typeof(MainWindow).GetMethod("UpdateCommandsAtPointList", flags).Invoke(window, null);
        typeof(MainWindow).GetMethod("ApplySplineSettings", flags).Invoke(window, new object[] { new[] { ServoNames.NeckTurn } });
        typeof(MainWindow).GetMethod("RebuildSplineData", flags).Invoke(window, null);
        var wave = (WaveformView)window.FindName("Waveform");
        var peaks = Enumerable.Range(0, 1000).Select(i => (float)Math.Sin(i * .03)).ToArray();
        wave.SetAudio(peaks.Select(v => -Math.Abs(v)).ToArray(), peaks.Select(Math.Abs).ToArray(), .01, 10);
        var fit = typeof(MainWindow).GetMethod("FitEditorPanels", flags);
        var controller = (TimelineLayoutController)typeof(MainWindow).GetField("_timelineLayout", flags).GetValue(window);
        void Arrange(int width, int height)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                fit.Invoke(window, null);
            }
            root.UpdateLayout();
        }
        var mapping = (ControllerMappingDisplay)window.FindName("ActiveControllerMapping");
        foreach (var mode in Enum.GetValues<TimelineLayoutMode>())
        foreach (bool activeController in new[] { false, true })
        {
            mapping.Visibility = activeController ? Visibility.Visible : Visibility.Collapsed;
            ((DockPanel)window.FindName("CommandsAtPointContent")).Visibility = activeController ? Visibility.Collapsed : Visibility.Visible;
            if (activeController) mapping.ShowMapping(ControllerProfile.Defaults(ControllerKind.Steam), new() { Connected = true });
            controller.Apply(mode, hasSplines: true);
            top.Height = new GridLength(1025.14); audio.Height = new GridLength(1, GridUnitType.Star);
            controller.RestoreHeights(audio.Height, new GridLength(163));
            foreach (var size in new[] { (1250, 800), (1000, 650), (1600, 1000), (800, 500) })
            {
                Arrange(size.Item1, size.Item2);
                double bottom = audioArea.TranslatePoint(new Point(0, audioArea.ActualHeight), root).Y;
                double splitterBottom = divider.TranslatePoint(new Point(0, divider.ActualHeight), root).Y;
                double viewport = (double)typeof(MainWindow).GetMethod("EditorPanelViewportHeight", flags).Invoke(window, null);
                Check(bottom <= viewport + 1 && audioArea.ActualHeight >= audio.MinHeight - 1,
                    $"Sequence timeline fits the usable viewport: {mode}, controller={activeController}, {size}, bottom={bottom}, viewport={viewport}");
                Check(splitterBottom <= size.Item2 && divider.ActualHeight >= 7, "Upper-panel resize divider stays reachable");
                foreach (double delta in new[] { 10.0, 100, 1000, -10, -1000, 50 })
                {
                    typeof(GridSplitter).GetMethod("InitializeData", flags).Invoke(divider, new object[] { false });
                    for (int step = 0; step < 3; step++)
                    {
                        typeof(GridSplitter).GetMethod("MoveSplitter", flags).Invoke(divider, new object[] { 0.0, delta });
                        Arrange(size.Item1, size.Item2);
                        root.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                    }
                    Check(top.ActualHeight >= top.MinHeight - 1 && top.ActualHeight <= top.MaxHeight + 1,
                        $"Dragging the command divider stays bounded: {mode}, {size}, delta={delta}");
                }
            }
        }
        top.Height = new GridLength(1500); audio.Height = new GridLength(1200); spline.Height = new GridLength(900);
        Arrange(1250, 800);
        Check(audioArea.TranslatePoint(new Point(0, audioArea.ActualHeight), root).Y <= 801, "Oversized pixel heights in multiple panes recover together");
        window.ResetEditorPanelLayout(); Arrange(1250, 800);
        Check(((ComboBox)window.FindName("TimelineLayoutPicker")).SelectedIndex == (int)TimelineLayoutMode.Combined && top.ActualHeight <= 250.01,
            "Reset Layout returns a usable combined timeline");
        Check(robot.ActualHeight <= grid.ActualHeight + 1, "Docked URDF height stays within the editor viewport");
        string folder = Path.Combine(Environment.CurrentDirectory, "layout-previews"); Directory.CreateDirectory(folder);
        RenderControl(root, Path.Combine(folder, "RecoveredLayout.png"), 1250, 800);
    }
}
