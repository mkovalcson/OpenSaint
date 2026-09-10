using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using ServoAnimator;

internal static partial class Program
{
    private static void LoopAndControlInspection()
    {
        double carried = 37;
        for (int loop = 0; loop < 100; loop++)
        {
            carried = MoviePoseContinuity.ChildValue(0, null, null, carried);
            Check(carried == 37, "Loop reset an untouched individual child to its gang value");
        }
        var child = new ServoCommand { Servo = ServoNames.EyesHorizontalRight,
            Control = RobotControls.LeftLensHorizontal, OffsetSeconds = 2, NumericValue = -26 };
        Check(MoviePoseContinuity.ChildValue(15, 1, child, 37) == -26, "Newer child command lost precedence");
        Check(MoviePoseContinuity.ChildValue(15, 2, child, 37) == 15, "Gang did not win a timestamp tie");
        Check(MoviePoseContinuity.ChildValue(15, 3, child, 37) == 15, "Newer gang command lost precedence");
        Check(MoviePoseContinuity.ChildValue(15, null, child, 37) == -26, "Child command did not replace carry");
        Check(MoviePoseContinuity.ChildValue(15, null, null, null) == 15, "Standalone child fallback changed");

        var commands = new[] { child, child.Clone(), new ServoCommand { Servo = ServoNames.NeckTurn },
            new ServoCommand { Servo = ServoNames.EyesHorizontalRight, OffsetSeconds = 4 } };
        var controls = ModifiedCommandControls.From(commands);
        Check(controls.Length == 3, "Modified controls contain duplicate entries");
        var left = controls.Single(c => c.Control == RobotControls.LeftLensHorizontal);
        Check(left.Includes(commands[3]) && !left.Includes(commands[2]), "Control highlight did not include gang/exclude unrelated servo");
        Check(!left.Includes(new ServoCommand { Servo = child.Servo, Control = RobotControls.RightLensHorizontal }),
            "Individual-control highlight includes the other side");
        var wave = new WaveformView { Markers = new[] { 1.0, 2, 3 } };
        int changes = 0; wave.MarkerSelectionChanged += () => changes++;
        wave.SelectMarker(1, true, false); wave.SelectMarker(3, false, true);
        wave.SetMarkerSelection(wave.SelectedMarkers);
        Check(wave.SelectedMarkers.Count == 3, "Replacing selection with itself cleared it");
        wave.SetMarkerSelection(Array.Empty<double>());
        Check(changes == 4 && wave.SelectedMarkers.Count == 0, "Selection end is not observable by the controls inspector");
    }

    private static void MainDisplayLayout()
    {
        string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../ServoAnimator"));
        var source = XDocument.Load(Path.Combine(project, "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace w = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var markup = new XElement(source.Root.Element(w + "DockPanel"));
        markup.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        markup.SetAttributeValue(XNamespace.Xmlns + "local", "clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer");
        var events = new HashSet<string> { "Click", "Checked", "Unchecked", "Scroll", "SelectionChanged", "SubmenuOpened",
            "ValueChanged", "MouseDoubleClick", "PreviewKeyDown" };
        foreach (var attr in markup.DescendantsAndSelf().Attributes().Where(a => events.Contains(a.Name.LocalName)).ToArray()) attr.Remove();
        foreach (var node in markup.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:ServoAnimator").ToArray())
        {
            if (node.Name.LocalName == "RobotHeadView")
                node.ReplaceWith(new BorderPlaceholder(node.Attribute(x + "Name")?.Value).Markup(w, x));
            else node.Name = XName.Get(node.Name.LocalName, "clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer");
        }
        var resources = XDocument.Load(Path.Combine(project, "App.xaml")).Root.Element(w + "Application.Resources");
        markup.AddFirst(new XElement(w + "DockPanel.Resources", resources.Elements().Select(e => new XElement(e))));
        var root = (DockPanel)System.Windows.Markup.XamlReader.Parse(markup.ToString());
        root.Background = (Brush)root.Resources["AppBackground"];
        T Find<T>(string name) => (T)root.FindName(name);
        using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Main display layout check")
            { Width = 1250, Height = 850, WindowStyle = 0 });
        presentation.RootVisual = root;
        var editor = Find<Grid>("EditorTimelineGrid");
        var urdf = Find<Border>("RobotHeadEmbeddedBorder");
        var live = Find<StackPanel>("LiveControlsPanel");
        var commands = Find<Border>("CommandsAtPointPanel");
        var controller = new TimelineLayoutController(editor, Find<Grid>("AudioTimelineArea"), Find<Grid>("AudioPlotHost"),
            Find<Grid>("SplinePlotHost"), Find<Grid>("SplineAreaGrid"), Find<Grid>("SplineLegendStrip"), Find<Border>("SplineArea"),
            Find<SplineView>("Spline"), Find<RowDefinition>("AudioTimelineRow"), Find<RowDefinition>("SplineTimelineRow"), firstTimelineRow: 4);
        void Layout(int width)
        {
            root.Measure(new Size(width, 850)); root.Arrange(new Rect(0, 0, width, 850)); root.UpdateLayout();
            Grid.SetRowSpan(urdf, editor.RowDefinitions.Count);
            urdf.Height = editor.RowDefinitions.Take(3).Sum(r => r.ActualHeight);
            root.UpdateLayout();
        }
        foreach (int width in new[] { 1250, 800, 1920 })
        foreach (var mode in Enum.GetValues<TimelineLayoutMode>())
        {
            controller.Apply(mode, true); Layout(width);
            Check(urdf.TranslatePoint(new Point(), root).Y < 7, "Docked URDF does not reach the client top");
            Check(live.TranslatePoint(new Point(), root).X >= urdf.TranslatePoint(new Point(urdf.ActualWidth, 0), root).X,
                "Hardware strip is not to the right of URDF");
            double bottom = 0;
            foreach (FrameworkElement control in live.Children)
            {
                var p = control.TranslatePoint(new Point(), live);
                Check(p.Y >= bottom, "Hardware strip controls overlap rather than stack vertically");
                Check(control.ActualWidth <= live.ActualWidth, "Hardware strip clips a button horizontally");
                bottom = p.Y + control.ActualHeight;
            }
            Check(commands.TranslatePoint(new Point(), root).Y >= Find<Border>("MainMenuPanel").ActualHeight,
                "Commands overlap the relocated menu");
            var picker = Find<ComboBox>("TimelineLayoutPicker");
            Check(picker.TranslatePoint(new Point(picker.ActualWidth, 0), root).X < width - 140,
                "Transport layout choice is clipped behind hardware strip");
        }
        Check(Find<object>("StatusText") == null && Find<object>("DirtySummaryText") == null, "Bottom status bar remains");
        Layout(1250);
        var preview = new System.Windows.Media.Imaging.RenderTargetBitmap(1250, 850, 96, 96, PixelFormats.Pbgra32);
        preview.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
        using (var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "main-display-layout-check.png"))) encoder.Save(stream);
        double commandsWidth = commands.ActualWidth;
        urdf.Visibility = Visibility.Collapsed;
        Find<ColumnDefinition>("UrdfEditorColumn").MinWidth = 0;
        Find<ColumnDefinition>("UrdfEditorColumn").Width = new GridLength(0);
        Find<ColumnDefinition>("UrdfSplitterColumn").Width = new GridLength(0);
        Layout(1250);
        Check(live.IsVisible && live.ActualWidth > 100 && commands.ActualWidth > commandsWidth,
            "Undocking removed the hardware strip or did not expand Commands");
        var editMarkup = XDocument.Load(Path.Combine(project, "CommandEditorWindow.xaml"));
        Check(!editMarkup.Descendants().Attributes().Any(a => a.Value.Contains("Reason")), "Edit Commands still exposes Reason");
    }

    // A preview placeholder deliberately avoids URDF startup, model loading and any configuration writes.
    private sealed record BorderPlaceholder(string Name)
    {
        public XElement Markup(XNamespace w, XNamespace x) => new(w + "Border", new XAttribute(x + "Name", Name),
            new XElement(w + "TextBlock", new XAttribute("Text", "URDF preview"), new XAttribute("Foreground", "#E4E7ED"),
                new XAttribute("HorizontalAlignment", "Center"), new XAttribute("VerticalAlignment", "Center")));
    }

    private static void MiddleButtonPanning()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var wave = new WaveformView { Duration = 100 };
        wave.Measure(new Size(800, 200)); wave.Arrange(new Rect(0, 0, 800, 200));
        foreach (bool splineMode in new[] { false, true })
        {
            wave.SetViewStart(20);
            FrameworkElement view = splineMode ? new SplineView { SyncTarget = wave, ViewStart = wave.ViewStart,
                PixelsPerSecond = wave.PixelsPerSecond } : wave;
            view.Measure(new Size(800, 200)); view.Arrange(new Rect(0, 0, 800, 200));
            var type = view.GetType();
            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Middle) { RoutedEvent = Mouse.MouseDownEvent };
            type.GetMethod("OnMouseDown", flags).Invoke(view, new object[] { down });
            Check(down.Handled && (bool)type.GetField("_panning", flags).GetValue(view), "Middle down did not start timeline panning");
            var move = new MouseEventArgs(Mouse.PrimaryDevice, 0);
            var point = move.GetPosition(view);
            type.GetField("_panStart", flags).SetValue(view, new Point(point.X + 100, point.Y));
            double before = wave.ViewStart;
            type.GetMethod("OnMouseMove", flags).Invoke(view, new object[] { move });
            Check(Math.Abs(wave.ViewStart - (before + 100 / wave.PixelsPerSecond)) < 0.001,
                "Middle drag did not pan by its pixel distance");
            type.GetMethod("OnMouseUp", flags).Invoke(view, new object[] { new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Middle) });
            Check(!(bool)type.GetField("_panning", flags).GetValue(view), "Middle release left timeline panning active");
        }
    }
}
