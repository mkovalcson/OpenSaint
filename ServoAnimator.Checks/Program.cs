using System.Collections.Concurrent;
using System.IO;
using NAudio.Wave;
using ServoAnimator;
using SkiaSharp;

internal static partial class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Contains("--playback-controls"))
            {
                PlaybackControlsChecks();
                Console.WriteLine($"PASS: {_checks} playback control assertions.");
            }
            else if (args.Contains("--mapping-files"))
            {
                MappingFileChecks();
                Console.WriteLine($"PASS: {_checks} controller mapping file assertions.");
            }
            else if (args.Contains("--persistent-groups"))
            {
                PersistentCommandGroupChecks();
                Console.WriteLine($"PASS: {_checks} persistent command group assertions.");
            }
            else if (args.Contains("--controller-recording"))
            {
                ControllerRecordingChecks();
                Console.WriteLine($"PASS: {_checks} controller recording assertions.");
            }
            else if (args.Contains("--splitter-rounding"))
            {
                SplitterRoundingChecks(args.Contains("--baseline"));
                Console.WriteLine($"PASS: {_checks} splitter rounding assertions.");
            }
            else if (args.Contains("--break-spline"))
            {
                BreakSplineChecks();
                Console.WriteLine($"PASS: {_checks} focused Break Spline assertions.");
            }
            else if (args.Contains("--fps-counter"))
            {
                FrameRateCounterChecks();
                Console.WriteLine($"PASS: {_checks} focused FPS counter assertions.");
            }
            else if (args.Contains("--playback-performance"))
            {
                PlaybackPerformanceChecks(args.Contains("--baseline"));
                Console.WriteLine($"PASS: {_checks} focused playback performance assertions.");
            }
            else if (args.Contains("--controller-feedback"))
            {
                ControllerFeedbackChecks();
                Console.WriteLine($"PASS: {_checks} focused controller feedback assertions.");
            }
            else if (args.Contains("--controller-layout"))
            {
                ControllerLayoutChecks();
                Console.WriteLine($"PASS: {_checks} controller layout assertions.");
            }
            else if (args.Contains("--layout-recovery"))
            {
                LayoutRecoveryChecks();
                Console.WriteLine($"PASS: {_checks} layout recovery assertions.");
            }
            else if (args.Contains("--controller-speeds"))
            {
                ControllerCalibratedSpeedChecks();
                Console.WriteLine($"PASS: {_checks} shared controller speed assertions.");
            }
            else if (args.Contains("--render-loop"))
            {
                UrdfRenderLoopChecks();
                Console.WriteLine($"PASS: {_checks} synchronized URDF render assertions.");
            }
            else if (args.Contains("--collision-performance"))
            {
                CollisionPerformanceChecks();
                Console.WriteLine($"PASS: {_checks} cached collision model assertions.");
            }
            else if (args.Contains("--controller-safeguard"))
            {
                ControllerSafeguardChecks();
                Console.WriteLine($"PASS: {_checks} controller display and collision safeguard assertions.");
            }
            else if (args.Contains("--speed-calibration"))
            {
                SpeedCalibrationChecks();
                Console.WriteLine($"PASS: {_checks} servo speed calibration assertions.");
            }
            else if (args.Contains("--library-images"))
            {
                LibraryPoseImageChecks();
                Console.WriteLine($"PASS: {_checks} Library pose image assertions.");
            }
            else if (args.Length == 3 && args[0] == "--pose-button-preview")
            {
                PoseButtonPreview(args[1], args[2]);
            }
            else if (args.Contains("--preview-cadence"))
            {
                SmoothCursorPresentation(); ClockContinuity(); QueueCancellation();
                Console.WriteLine($"PASS: {_checks} preview cadence and playback timing assertions.");
            }
            else if (args.Contains("--urdf-exterior") || args.Contains("--urdf-exterior-assets"))
            {
                UrdfExteriorChecks(render: !args.Contains("--urdf-exterior-assets"));
                Console.WriteLine($"PASS: {_checks} URDF exterior assertions.");
            }
            else if (args.Contains("--controllers"))
            {
                int preview = Array.IndexOf(args, "--previews");
                ControllerChecks(preview >= 0 && preview + 1 < args.Length ? args[preview + 1] : null);
                Console.WriteLine($"PASS: {_checks} controller assertions.");
            }
            else if (args.Contains("--editor-api"))
            {
                EditorApiChecks();
                Console.WriteLine($"PASS: {_checks} editor API assertions.");
            }
            else if (args.Contains("--audio-movie"))
            {
                AudioMovieCreation();
                Console.WriteLine($"PASS: {_checks} audio movie assertions.");
            }
            else if (args.Contains("--sequence-triggers"))
            {
                SequenceTriggers();
                Console.WriteLine($"PASS: {_checks} sequence trigger assertions.");
            }
            else RunChecks();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.ExitCode = 1;
        }
    }

    private static void RunChecks()
    {
        AudioMovieCreation();
        QueueCancellation();
        ClockContinuity();
        EnvelopeQueries();
        DrawingCache();
        KeyboardFocus();
        SequenceTriggers();
        MovieDescriptionLinks();
        NewMovieSequences();
        MinimumMovieBlockWidths();
        LibraryCategoriesAndRanges();
        CommandGroupEditing();
        CompactTimelineLayout();
        SmoothCursorPresentation();
        TimelineLayouts();
        LoopAndControlInspection();
        MainDisplayLayout();
        MiddleButtonPanning();
        SplineDraftsAndGrid();
        CommandMarkerHits();
        CursorAnchoredZoom();
        TimelineInteractionChanges();
        HelpTutorials();
        CommandConflictChoices();
        CommandDrafts();
        MediaFiles().GetAwaiter().GetResult();
        Console.WriteLine($"PASS: {_checks} assertions (Library categories/ranges, command groups, minimum Movie block widths, new Movie sequences, zoom, help, layout, conflicts, playback and caches).");
    }

    private sealed class CountingWaveform : WaveformView
    {
        public int PaintCount { get; private set; }
        protected override void OnPaintSurface(SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            PaintCount++; base.OnPaintSurface(e);
        }
    }

    private static void SmoothCursorPresentation()
    {
        foreach (int refresh in new[] { 15, 30, 60, 75, 120, 144, 240 })
        {
            var cadence = new PlaybackFrameCadence();
            int updates = 0;
            for (int i = 0; i < refresh * 5; i++)
                if (cadence.IsDue(TimeSpan.FromSeconds(i / (double)refresh))) updates++;
            Check(Math.Abs(updates - Math.Min(refresh, 30) * 5) <= 1,
                $"Preview cadence drifted at {refresh} Hz: {updates} updates in five seconds");
            cadence.Reset();
            Check(cadence.IsDue(TimeSpan.Zero), "Resuming playback delayed the first preview frame");
            Check(cadence.IsDue(TimeSpan.FromSeconds(2)), "A slow frame prevents the next preview update");
            Check(!cadence.IsDue(TimeSpan.FromSeconds(2)), "A delayed frame causes duplicate catch-up preview updates");
        }
        var wave = new CountingWaveform { Duration = 10, ContentDuration = 10 };
        var host = new System.Windows.Documents.AdornerDecorator { Child = wave };
        // A hidden presentation source activates WPF rendering without showing the app.
        using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Cursor rendering check")
            { Width = 600, Height = 180, WindowStyle = 0 });
        presentation.RootVisual = host;
        host.Measure(new System.Windows.Size(600, 180));
        host.Arrange(new System.Windows.Rect(0, 0, 600, 180)); host.UpdateLayout();
        wave.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(wave);
        var cursor = layer.GetAdorners(wave)?.OfType<TimelineCursorPresenter.CursorAdorner>().SingleOrDefault();
        Check(cursor != null && !cursor.IsHitTestVisible && cursor.ClipToBounds, "Cursor is missing, intercepts input, or can escape its timeline");
        void Render()
        {
            host.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(600, 180, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(host);
        }
        Render();
        Render();
        int paints = wave.PaintCount;
        for (int i = 0; i < 20; i++)
        {
            wave.CursorTime = 0.0125 * i;
            wave.InvalidateCursor(); Render();
            Check(Math.Abs(cursor.CursorX - wave.XAtTime(wave.CursorTime)) < 1e-6, "Retained cursor lost subpixel position");
        }
        Check(paints > 0 && wave.PaintCount == paints, "Moving cursor repainted the waveform surface");
        wave.CursorTime = 1000; wave.InvalidateCursor();
        Check(cursor.Visibility == System.Windows.Visibility.Collapsed, "Offscreen cursor painted outside viewport");
        wave.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        Check(layer.GetAdorners(wave) == null || layer.GetAdorners(wave).Length == 0, "Unloading a timeline leaked its cursor overlay");
    }

    private static void TimelineLayouts()
    {
        var source = System.Xml.Linq.XDocument.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../ServoAnimator/MainWindow.xaml")));
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        System.Xml.Linq.XElement Fragment(string name)
        {
            var element = new System.Xml.Linq.XElement(source.Descendants().Single(e => (string)e.Attribute(x + "Name") == name));
            foreach (var attr in element.DescendantsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.Name.LocalName is "Click" or "Scroll").ToArray()) attr.Remove();
            foreach (var node in element.DescendantsAndSelf().Where(e => e.Name.NamespaceName == "clr-namespace:ServoAnimator"))
                node.Name = System.Xml.Linq.XName.Get(node.Name.LocalName, "clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer");
            return element;
        }
        var markup = System.Xml.Linq.XElement.Parse("""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:local="clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer">
                <Grid.Resources><CornerRadius x:Key="PanelCornerRadius">4</CornerRadius></Grid.Resources>
                <Grid.RowDefinitions>
                    <RowDefinition Height="80"/><RowDefinition Height="8"/>
                    <RowDefinition x:Name="AudioTimelineRow" Height="*" MinHeight="120"/>
                    <RowDefinition Height="7"/><RowDefinition x:Name="SplineTimelineRow" Height="190"/>
                </Grid.RowDefinitions>
            </Grid>
            """);
        markup.Add(Fragment("AudioTimelineArea"), Fragment("SplineArea"));
        var root = (System.Windows.Controls.Grid)System.Windows.Markup.XamlReader.Parse(markup.ToString());
        using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Timeline layout rendering check")
            { Width = 800, Height = 650, WindowStyle = 0 });
        presentation.RootVisual = root;
        root.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 33, 38));
        T Find<T>(string name) => (T)root.FindName(name);
        var audioArea = Find<System.Windows.Controls.Grid>("AudioTimelineArea");
        var audioHost = Find<System.Windows.Controls.Grid>("AudioPlotHost");
        var splineHost = Find<System.Windows.Controls.Grid>("SplinePlotHost");
        var splineArea = Find<System.Windows.Controls.Border>("SplineArea");
        var legendStrip = Find<System.Windows.Controls.Grid>("SplineLegendStrip");
        var legend = Find<System.Windows.Controls.ItemsControl>("SplineLegend");
        var showAll = Find<System.Windows.Controls.Button>("SplineShowAllButton");
        var wave = Find<WaveformView>("Waveform"); var spline = Find<SplineView>("Spline");
        var items = Enumerable.Range(0, 7).Select(i => new SplineLegendItem { Name = "Servo " + i, Visible = true, Brush = System.Windows.Media.Brushes.Red }).ToArray();
        ((System.Windows.Data.CompositeCollection)legend.ItemsSource).OfType<System.Windows.Data.CollectionContainer>().Single().Collection = items;
        showAll.Visibility = System.Windows.Visibility.Visible;
        Find<System.Windows.Controls.Border>("SequenceEmptyStatePanel").Visibility = System.Windows.Visibility.Collapsed;
        wave.Duration = wave.ContentDuration = 8; wave.Markers = new[] { 0.0, 2, 4, 6 };
        float[] peaks = Enumerable.Range(0, 160).Select(i => (float)(0.2 + 0.6 * Math.Abs(Math.Sin(i * 0.17)))).ToArray();
        wave.SetAudio(peaks.Select(p => -p).ToArray(), peaks, 0.05, 8);
        wave.SetMarkerSelection(new[] { 2.0, 4 });
        spline.Curves = new() { new SplineCurve { Servo = ServoNames.NeckTurn, T = new[] { 0.0, 6 }, V = new[] { -100.0, 100 },
            M = new[] { 200.0 / 6, 200.0 / 6 }, Min = -100, Max = 100, Color = SKColors.Red, Visible = true } };
        var controller = new TimelineLayoutController(root, audioArea, audioHost, splineHost, Find<System.Windows.Controls.Grid>("SplineAreaGrid"),
            legendStrip, splineArea, spline, Find<System.Windows.Controls.RowDefinition>("AudioTimelineRow"), Find<System.Windows.Controls.RowDefinition>("SplineTimelineRow"));
        void Layout(double width)
        {
            root.Measure(new System.Windows.Size(width, 650)); root.Arrange(new System.Windows.Rect(0, 0, width, 650)); root.UpdateLayout();
            wave.ZoomToFit(); spline.PixelsPerSecond = wave.PixelsPerSecond; spline.ViewStart = wave.ViewStart;
        }
        foreach (var mode in new[] { TimelineLayoutMode.WaveformTop, TimelineLayoutMode.SplineTop, TimelineLayoutMode.Combined,
            TimelineLayoutMode.SplineTop, TimelineLayoutMode.WaveformTop, TimelineLayoutMode.Combined })
        foreach (double width in new[] { 800.0, 460.0 })
        {
            controller.Apply(mode, true); Layout(width);
            Check(wave.SelectedMarkers.OrderBy(t => t).SequenceEqual(new[] { 2.0, 4 }), "Layout switching discarded triangle selection");
            if (mode == TimelineLayoutMode.Combined)
            {
                Check(ReferenceEquals(spline.Parent, audioHost) && ReferenceEquals(legendStrip.Parent, audioArea), "Combined layout did not reuse the spline and legend");
                double waveformTop = wave.TranslatePoint(new System.Windows.Point(), root).Y;
                Check(legendStrip.TranslatePoint(new System.Windows.Point(0, legendStrip.ActualHeight), root).Y <= waveformTop + 0.001,
                    "Combined legend is not above command triangles");
                double hundredY = spline.TranslatePoint(new System.Windows.Point(0, SplineView.StandardValueY(100, (float)spline.ActualHeight)), root).Y;
                Check(hundredY > waveformTop + 38, "Combined command triangles overlap the +100 grid line");
                Check(Math.Abs(spline.TranslatePoint(new System.Windows.Point(spline.XAtTime(2), 0), root).X - wave.TranslatePoint(new System.Windows.Point(wave.XAtTime(2), 0), root).X) < 0.001,
                    "Combined spline and waveform have different time coordinates");
                Check(spline.WantsCombinedInput(new System.Windows.Point(spline.XAtTime(0), spline.ActualHeight - 8)), "Combined spline points are not editable");
                Check(!spline.WantsCombinedInput(new System.Windows.Point(spline.XAtTime(0), 8)), "Combined blank space blocks waveform input");
            }
            else
            {
                Check(ReferenceEquals(spline.Parent, splineHost) && !spline.CombinedMode, "Separate layout retained a combined spline");
                Check(System.Windows.Controls.Grid.GetRow(splineArea) == (mode == TimelineLayoutMode.SplineTop ? 2 : 4), "Separate timelines are in the wrong order");
            }
            var last = (System.Windows.FrameworkElement)legend.ItemContainerGenerator.ContainerFromIndex(items.Length - 1);
            var lastEnd = last.TranslatePoint(new System.Windows.Point(last.ActualWidth, last.ActualHeight), legend);
            var buttonStart = showAll.TranslatePoint(new System.Windows.Point(), legend);
            Check(buttonStart.X >= lastEnd.X - 0.001 || buttonStart.Y >= lastEnd.Y - 0.001,
                "Show all is not immediately after the final legend item in wrapping order");
        }
        controller.Apply(TimelineLayoutMode.Combined, true); Layout(800);
        var preview = new System.Windows.Media.Imaging.RenderTargetBitmap(800, 650, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        preview.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
        using (var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "combined-layout-check.png"))) encoder.Save(stream);
        controller.Apply(TimelineLayoutMode.SplineTop, false); Layout(800);
        Check(splineArea.Visibility == System.Windows.Visibility.Collapsed && System.Windows.Controls.Grid.GetRow(audioArea) == 2,
            "Spline-top layout without curves leaves an empty pane above the waveform");
        var settings = System.Text.Json.JsonSerializer.Deserialize<EditorLayoutSettings>(System.Text.Json.JsonSerializer.Serialize(
            new EditorLayoutSettings { TimelineLayout = "Combined" }));
        Check(settings.TimelineLayout == "Combined" && new EditorLayoutSettings().TimelineLayout == "WaveformTop", "Timeline layout choice is not persistent/backward compatible");
        var picker = source.Descendants().Single(e => (string)e.Attribute(x + "Name") == "TimelineLayoutPicker");
        Check((string)picker.ElementsAfterSelf().First().Attribute("Content") == "?", "Layout picker is not immediately left of Help");
    }

    private static void LibraryCategoriesAndRanges()
    {
        var wave = new WaveformView { Duration = 100, Markers = new[] { 2.0, 5, 17, 90 } };
        wave.Measure(new System.Windows.Size(800, 200));
        wave.Arrange(new System.Windows.Rect(0, 0, 800, 200));
        wave.SetViewStart(30);
        wave.BeginRangeSelect();
        Check(wave.RangeStart == 2 && wave.RangeEnd == 90, "Library range does not default to first/last commands");
        wave.MoveRangeArrow(1, 15);
        wave.MoveRangeArrow(2, 21);
        Check(wave.RangeStart == 17 && wave.RangeEnd == 17, "Library arrows did not snap to command times");
        wave.MoveRangeArrow(1, 90);
        wave.MoveRangeArrow(2, 0);
        Check(wave.RangeStart == 17 && wave.RangeEnd == 17, "Library arrows crossed");
        wave.Markers = Array.Empty<double>(); wave.BeginRangeSelect();
        Check(wave.RangeStart == 0 && wave.RangeEnd == 0, "Empty Library range is not safe");
        wave.Markers = new[] { 7.0 }; wave.BeginRangeSelect();
        Check(wave.RangeStart == 7 && wave.RangeEnd == 7, "Single-command Library range excludes its command");

        string folder = Path.Combine(Path.GetTempPath(), "ServoAnimator-categories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string a = Path.Combine(folder, "z.json"), b = Path.Combine(folder, "a.json");
            File.WriteAllText(a, "{\"description\":\"Test\",\"commands\":[],\"image\":\"ref.png\",\"futureField\":42}");
            File.WriteAllText(b, "[]");
            var items = LibraryItemInfo.Scan(folder);
            Check(items.All(i => i.Category == "none"), "Legacy items did not default to none");
            var categories = new LibraryCategories(folder, items);
            categories.Add("Greetings"); categories.Add("Unused");
            categories.Assign(items.Single(i => i.FullPath == a), "Greetings");
            var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(a));
            Check((string)saved["category"] == "Greetings" && (string)saved["image"] == "ref.png" && (int)saved["futureField"] == 42,
                "Setting a Library category lost existing fields");
            Check(LibraryItemInfo.Scan(folder)[0].FileName == "z.json", "Library items were not sorted by category before filename");
            categories.Assign(items.Single(i => i.FullPath == b), "Greetings");
            Check(LibraryItemInfo.Scan(folder).Select(i => i.FileName).SequenceEqual(new[] { "a.json", "z.json" }),
                "Category ties did not sort by filename");
            Check(AnimationDocument.LoadLibraryItem(b).Category == "Greetings", "Legacy array could not receive a category");
            categories.RenameOrDelete("Greetings", "Welcome");
            Check(items.All(i => i.Category == "Welcome") && AnimationDocument.LoadLibraryItem(a).Category == "Welcome",
                "Renaming a category did not update its items and JSON");
            var reloaded = new LibraryCategories(folder, LibraryItemInfo.Scan(folder));
            Check(reloaded.Names.Contains("Unused") && reloaded.Names.Contains("Welcome") && !reloaded.Names.Contains("Greetings"),
                "Category names did not persist or empty categories were lost");
            AnimationDocument.UpdateLibraryDescription(a, "Updated");
            AnimationDocument.UpdateLibraryImage(a, "other.png");
            Check(AnimationDocument.LoadLibraryItem(a).Category == "Welcome", "Editing metadata lost a category");
            AnimationDocument.SaveLibraryCommand(a, Array.Empty<ServoCommand>());
            Check(AnimationDocument.LoadLibraryItem(a).Category == "Welcome", "Overwriting a Library Pose reset its category");
            AnimationDocument.SaveCommandsOnly(a, new());
            Check(AnimationDocument.LoadLibraryItem(a).Category == "Welcome", "Overwriting a Library Sequence reset its category");
            categories.RenameOrDelete("Welcome", null);
            Check(LibraryItemInfo.Scan(folder).All(i => i.Category == "none") && File.Exists(a) && File.Exists(b),
                "Deleting a category did not reset assignments or deleted item files");
            bool protectedNone = false;
            try { categories.RenameOrDelete("none", null); } catch (ArgumentException) { protectedNone = true; }
            Check(protectedNone, "Default category can be deleted");
            Check(LibraryItemInfo.Scan(folder).Count == 2, "Category catalog appeared as a Library item");
            foreach (string label in new[] { "Library Sequence", "Library Pose" })
            {
                var window = new LibraryItemSelectionWindow(folder, true, label);
                try
                {
                    var grid = (System.Windows.Controls.DataGrid)window.FindName("ItemsGrid");
                    Check(grid.Columns[1].Visibility == System.Windows.Visibility.Collapsed && (string)grid.Columns[2].Header == "Category",
                        "Folder column wasn't hidden or Category doesn't follow Folder");
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource);
                    Check(view.SortDescriptions.Select(s => s.PropertyName).SequenceEqual(new[] { "Category", "FileName" }),
                        "Library editor doesn't apply category/filename sorting");
                }
                finally { window.Close(); }
            }
            string child = Path.Combine(folder, "Child"); Directory.CreateDirectory(child);
            File.WriteAllText(Path.Combine(child, "child.json"), "[]");
            var nested = new LibraryItemSelectionWindow(folder, true);
            try
            {
                Check(((System.Windows.Controls.DataGrid)nested.FindName("ItemsGrid")).Columns[1].Visibility == System.Windows.Visibility.Visible,
                    "Library child items did not reveal Folder column");
            }
            finally { nested.Close(); }
            var image = new System.Windows.Media.Imaging.RenderTargetBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            image.Freeze();
            string png = Path.Combine(folder, "pose.png");
            Task.Run(() => RobotHeadView.SaveLibraryPoseBitmap(image, png)).GetAwaiter().GetResult();
            Check(File.Exists(png) && new FileInfo(png).Length > 0, "Pose image cannot be encoded on the busy-window worker");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void CommandGroupEditing()
    {
        var wave = new WaveformView { Duration = 30, Markers = new[] { 1.0, 3, 5, 9, 14 }, CursorTime = 1 };
        wave.SelectMarker(3, true, false); wave.SelectMarker(9, true, false);
        Check(wave.SelectedMarkers.OrderBy(t => t).SequenceEqual(new[] { 3.0, 9 }), "Ctrl selection didn't add separate triangles");
        wave.SelectMarker(3, true, false);
        Check(wave.SelectedMarkers.SequenceEqual(new[] { 9.0 }), "Ctrl selection didn't deselect a triangle");
        wave.SelectMarker(14, false, true);
        Check(wave.SelectedMarkers.OrderBy(t => t).SequenceEqual(new[] { 9.0, 14 }), "Shift selection anchored on a deselected triangle");
        wave.SelectMarker(3, false, true);
        Check(wave.SelectedMarkers.OrderBy(t => t).SequenceEqual(new[] { 3.0, 5, 9, 14 }), "Shift selection is not an inclusive range from the anchor");
        wave.SetMarkerSelection(new[] { 3.0, 9 });
        wave.CursorTime = 20;
        Check(wave.SelectedMarkers.Count == 2, "Moving cursor cleared group selection");
        wave.PreviewSelectedMarkerMove(-100);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        double Delta() => (double)typeof(WaveformView).GetField("_groupDragOffset", flags).GetValue(wave);
        Check(Delta() == -3, "Group drag allowed commands before zero");
        wave.PreviewSelectedMarkerMove(100);
        Check(Delta() == 21, "Group drag distorted timing at the right boundary");
        wave.PreviewSelectedMarkerMove(1.25);
        double moved = 0; wave.SelectedMarkersMoved += delta => moved = delta;
        typeof(WaveformView).GetField("_draggingMarker", flags).SetValue(wave, true);
        var up = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
        typeof(WaveformView).GetMethod("OnMouseUp", flags).Invoke(wave, new object[] { up });
        Check(moved == 1.25 && Delta() == 0 && wave.SelectedMarkers.Count == 2, "Group drag didn't commit once while preserving selection");
        Check(CommandGroupOperations.MeanSpacing(new[] { 2.0, 4, 10 }) == 4, "Mean command spacing is wrong");
        var uniform = CommandGroupOperations.UniformTimes(new[] { 10.0, 2, 4, 4 }, 1.5);
        Check(uniform[2] == 2 && uniform[4] == 3.5 && uniform[10] == 5, "Uniform spacing moved first marker or changed ordering");
        var commands = new List<ServoCommand>
        {
            new() { OffsetSeconds = 2, Servo = ServoNames.NeckTurn, NumericValue = 10 },
            new() { OffsetSeconds = 4, Servo = ServoNames.NeckTurn, NumericValue = 20 },
            new() { OffsetSeconds = 10, Servo = ServoNames.NeckTurn, NumericValue = 30 },
        };
        var copies = CommandGroupOperations.Repeat(commands, 2, 0.5);
        Check(copies.Select(c => c.OffsetSeconds).SequenceEqual(new[] { 10.5, 12.5, 18.5, 19.0, 21, 27 }),
            "Repeat did not start after last selection with the requested gap between copies");
        Check(commands.Select(c => c.OffsetSeconds).SequenceEqual(new[] { 2.0, 4, 10 }) && !ReferenceEquals(copies[0], commands[0]),
            "Repeat changed the originals or reused command objects");
        var zeroGap = commands.Concat(CommandGroupOperations.Repeat(commands, 1, 0)).ToList();
        Check(CommandConflicts.Find(zeroGap).Count == 1, "Zero-gap repeat bypassed the redundant-command checker");
        var pasted = CommandGroupOperations.CopyAt(commands, 20);
        Check(pasted.Select(c => c.OffsetSeconds).SequenceEqual(new[] { 20.0, 22, 28 }), "Copy/paste flattened group timing");
        bool guarded = false;
        try { CommandGroupOperations.Repeat(commands, 10001, 0); } catch (ArgumentException) { guarded = true; }
        Check(guarded, "Repeat has no guard against excessive copies");
    }

    private static void MinimumMovieBlockWidths()
    {
        var items = new[]
        {
            new MovieSequenceItem { FilePath = "tiny.json", DurationSeconds = 0.001 },
            new MovieSequenceItem { FilePath = "long.json", DurationSeconds = 3600 },
            new MovieSequenceItem { FilePath = "short.json", DurationSeconds = 0.1 },
            new MovieSequenceItem { FilePath = "medium.json", DurationSeconds = 20 },
        };
        foreach (double scale in new[] { 0.000001, 0.01, 1, 50, 1000 })
        {
            var geometry = new MovieTimelineGeometry(items, scale);
            double time = 0;
            foreach (var item in items)
            {
                double start = geometry.PixelAtTime(time);
                double end = geometry.PixelAtTime(time + item.DurationSeconds);
                Check(end - start - MovieTimelineGeometry.BlockGap >= 100 - 1e-6,
                    "Movie block shrank below 100 pixels");
                foreach (double fraction in new[] { 0.0, 0.1, 0.5, 0.9, 1.0 })
                {
                    double target = time + item.DurationSeconds * fraction;
                    Check(Math.Abs(geometry.PixelAtTime(target) - (start + (end - start) * fraction)) < 1e-6,
                        "Playback cursor does not progress within a minimum-width block");
                    Check(Math.Abs(geometry.TimeAtPixel(geometry.PixelAtTime(target)) - target) < 1e-7,
                        "Movie click position no longer maps to the correct time");
                }
                time += item.DurationSeconds;
            }
        }

        var movie = new MovieTimelineView { CanCreateSequence = true };
        movie.SetItems(items);
        var button = movie.Children.OfType<System.Windows.Controls.Button>().Single();
        void Layout(double width)
        {
            movie.Measure(new System.Windows.Size(width, 80));
            movie.Arrange(new System.Windows.Rect(0, 0, width, 80));
            movie.UpdateLayout();
        }
        void CheckWidths()
        {
            for (int i = 0; i < movie.Items.Count; i++)
                Check(movie.XAtTime(movie.StartOf(i + 1)) - movie.XAtTime(movie.StartOf(i)) - 2 >= 100 - 1e-6,
                    "Movie view violated minimum block width");
        }
        Layout(800);
        movie.ZoomToFit();
        Layout(800);
        CheckWidths();
        Check(Math.Abs(button.TranslatePoint(new System.Windows.Point(), movie).X - movie.XAtTime(movie.TotalDuration) - 8) < 1e-6,
            "Minimum block width detached the New Sequence button");
        Check(button.TranslatePoint(new System.Windows.Point(button.ActualWidth, 0), movie).X <= 800 + 1e-6,
            "Fit failed to account for minimum-width blocks and the New Sequence button");
        movie.CursorTime = 3600.051;
        double anchor = movie.XAtTime(movie.CursorTime);
        movie.ZoomBy(4);
        Layout(800);
        Check(Math.Abs(movie.XAtTime(movie.CursorTime) - anchor) < 1e-6,
            "Zoom moved a cursor inside a minimum-width sequence");
        movie.ZoomBy(0.25);
        Layout(800);
        Check(Math.Abs(movie.XAtTime(movie.CursorTime) - anchor) < 1e-6,
            "Zoom round trip drifted the minimum-width block cursor");
        Layout(320);
        movie.ZoomToFit();
        Layout(320);
        CheckWidths();
        Check(movie.ScrollableDuration > movie.VisibleSeconds, "Fit hid blocks that need horizontal scrolling");
        movie.PanTo(double.MaxValue);
        Layout(320);
        Check(button.TranslatePoint(new System.Windows.Point(), movie).X >= 0 &&
            button.TranslatePoint(new System.Windows.Point(button.ActualWidth, 0), movie).X <= 320 + 1e-6,
            "Scroll end cannot reach the button after minimum-width blocks");
        movie.EnsureVisible(0);
        Check(Math.Abs(movie.XAtTime(0)) < 1e-6, "EnsureVisible did not return to the first minimum-width block");
        movie.EnsureVisible(movie.TotalDuration);
        Check(movie.XAtTime(movie.TotalDuration) >= 0 && movie.XAtTime(movie.TotalDuration) <= 320,
            "Playback autoscroll used real seconds instead of the display geometry");
        for (int i = 0; i < items.Length; i++)
        {
            double midpoint = movie.StartOf(i) + items[i].DurationSeconds / 2;
            Check(movie.IndexAtTime(movie.TimeAtX(movie.XAtTime(midpoint))) == i,
                "Hit testing chose the wrong minimum-width block");
        }
        items[0].DurationSeconds = 5;
        movie.SetItems(items.Reverse());
        CheckWidths();
        Check(Math.Abs(movie.TimeAtX(movie.XAtTime(10)) - 10) < 1e-6,
            "Reorder/duration edits left stale Movie geometry");
    }

    private static void NewMovieSequences()
    {
        var document = new AnimationDocument { Description = "New description" };
        string moviePath = @"C:\Config\movie.json";
        var draft = new PendingMovieSequence("Finale", moviePath, document);
        var first = new MovieSequenceItem { FilePath = "first.json", DurationSeconds = 10, IsLooping = true };
        var items = new List<MovieSequenceItem> { first };
        Check(items.Count == 1 && draft.IsFor(document, moviePath), "Starting a draft must not add a block");
        Check(!draft.AppendAfterSave(new AnimationDocument(), moviePath, "wrong.json", 5, items), "A replaced document was appended");
        Check(!draft.AppendAfterSave(document, @"C:\Config\other.json", "wrong.json", 5, items), "Draft leaked into another Movie");
        Check(!draft.AppendAfterSave(document, moviePath, "", 5, items) && items.Count == 1, "Missing save path appended a block");
        items.Add(new MovieSequenceItem { FilePath = "inserted.json", DurationSeconds = 4 });
        Check(draft.AppendAfterSave(document, moviePath.ToUpperInvariant(), "actual-save-as-name.json", 7.5, items), "First successful save did not append");
        Check(items.Count == 3 && ReferenceEquals(items[0], first) && items[0].IsLooping && items[1].FilePath == "inserted.json",
            "Appending changed the pre-existing Movie blocks or order");
        Check(items[^1].FilePath == "actual-save-as-name.json" && items[^1].DurationSeconds == 7.5 &&
            items[^1].Description == document.Description && !items[^1].IsLooping, "New block did not use saved Sequence metadata");
        Check(!draft.AppendAfterSave(document, moviePath, "second-save.json", 8, items) && items.Count == 3,
            "Repeated save appended a duplicate block");
        foreach (string valid in new[] { "Greeting", "Act 2", "Finale.json", "Robot — wave", "Scene.v2", "CONcert" })
            Check(PendingMovieSequence.TryName(valid, out string name) && !name.EndsWith(".json"), "Valid Sequence name was rejected: " + valid);
        foreach (string invalid in new[] { "", " ", ".", "..", ".json", "../escape", "sub\\name", "C:\\file", "what?", "CON", "nul.json", "COM1", "LPT2.txt", "end.", new string('x', 151) })
            Check(!PendingMovieSequence.TryName(invalid, out _), "Invalid Sequence filename was accepted: " + invalid);

        var movie = new MovieTimelineView();
        var button = movie.Children.OfType<System.Windows.Controls.Button>().Single();
        Check(button.Visibility == System.Windows.Visibility.Collapsed, "New Sequence is shown without an open Movie");
        void Layout(double width = 800)
        {
            movie.Measure(new System.Windows.Size(width, 80));
            movie.Arrange(new System.Windows.Rect(0, 0, width, 80));
            movie.UpdateLayout();
        }
        double ButtonX() => button.TranslatePoint(new System.Windows.Point(), movie).X;
        movie.CanCreateSequence = true;
        Layout();
        Check(button.Visibility == System.Windows.Visibility.Visible && Math.Abs(ButtonX() - 8) < 0.001,
            "Empty open Movie has no reachable New Sequence button");
        int requests = 0;
        movie.NewSequenceRequested += () => requests++;
        button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(requests == 1, "New Sequence button did not issue exactly one request");
        movie.SetItems(items);
        movie.ZoomToFit();
        Layout();
        double duration = movie.TotalDuration;
        Check(Math.Abs(ButtonX() - (duration * movie.PixelsPerSecond + 8)) < 0.001,
            "New Sequence button is not after the final block");
        Check(ButtonX() + button.ActualWidth <= movie.ActualWidth && movie.TotalDuration == 21.5,
            "Movie Fit clips the button or includes it in the playback duration");
        movie.CursorTime = 10;
        double before = (movie.CursorTime - movie.ViewStart) * movie.PixelsPerSecond;
        movie.ZoomBy(4);
        Layout();
        Check(Math.Abs((movie.CursorTime - movie.ViewStart) * movie.PixelsPerSecond - before) < 0.001,
            "Adding the Movie tail broke cursor-anchored zoom");
        movie.PanTo(double.MaxValue);
        Layout();
        Check(ButtonX() >= 0 && ButtonX() + button.ActualWidth <= movie.ActualWidth &&
            Math.Abs(movie.ViewStart - (movie.ScrollableDuration - movie.VisibleSeconds)) < 0.001,
            "Movie scroll end cannot reach the New Sequence button");
        movie.ZoomToFit();
        Layout(480);
        Check(ButtonX() >= 0 && ButtonX() + button.ActualWidth <= movie.ActualWidth,
            "Resizing a fitted Movie clips the New Sequence button");
        movie.CanCreateSequence = false;
        Layout();
        Check(button.Visibility == System.Windows.Visibility.Collapsed && movie.ScrollableDuration == duration,
            "Closing Movie leaves a button or extra scroll space");

        string recoveryFolder = Path.Combine(Path.GetTempPath(), "ServoAnimator-draft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recoveryFolder);
        try
        {
            var snapshot = new EditorRecoverySnapshot
            {
                Sequence = document, MoviePath = Path.Combine(recoveryFolder, "movie.json"),
                PendingMovieSequenceName = "Finale", SequenceWasDirty = true,
            };
            snapshot.Save(recoveryFolder);
            var recovered = EditorRecoverySnapshot.Load(recoveryFolder);
            Check(recovered.PendingMovieSequenceName == "Finale" && recovered.SequenceWasDirty && recovered.SequencePath == "",
                "Recovery lost the named unsaved Movie draft");
            var pending = new PendingMovieSequence(recovered.PendingMovieSequenceName, recovered.MoviePath, recovered.Sequence);
            Check(pending.AppendAfterSave(recovered.Sequence, recovered.MoviePath, Path.Combine(recoveryFolder, "Finale.json"), 2, new()),
                "Recovered draft cannot append after saving");
        }
        finally
        {
            EditorRecoverySnapshot.Delete(recoveryFolder);
            Directory.Delete(recoveryFolder);
        }
    }

    private static void CursorAnchoredZoom()
    {
        void Layout(System.Windows.FrameworkElement view)
        {
            view.Measure(new System.Windows.Size(800, 200));
            view.Arrange(new System.Windows.Rect(0, 0, 800, 200));
        }
        void Wheel(System.Windows.FrameworkElement view, int delta)
        {
            var e = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, delta)
                { RoutedEvent = System.Windows.Input.Mouse.MouseWheelEvent };
            view.GetType().GetMethod("OnMouseWheel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(view, new object[] { e });
            Check(e.Handled, "Timeline wheel event was not consumed");
        }
        foreach (double fraction in new[] { 0.0, 0.17, 0.5, 0.83, 1.0 })
        {
            var wave = new WaveformView { Duration = 120, ContentDuration = 120, CursorTime = 60 };
            Layout(wave);
            wave.ZoomToFit();
            wave.ZoomBy(4);
            wave.SetViewStart(20);
            wave.CursorTime = wave.ViewStart + fraction * wave.VisibleSeconds;
            double cursorTime = wave.CursorTime;
            double WaveX() => (wave.CursorTime - wave.ViewStart) * wave.PixelsPerSecond;
            double expectedX = WaveX();
            for (int repeat = 0; repeat < 20; repeat++)
            {
                wave.ZoomBy(1.5);
                Check(Math.Abs(WaveX() - expectedX) < 1e-6, "Waveform button zoom moved the cursor on screen");
                wave.ZoomBy(1 / 1.5);
                Check(Math.Abs(WaveX() - expectedX) < 1e-6 && wave.CursorTime == cursorTime,
                    "Waveform zoom-out drifted the cursor or changed playback time");
            }
            Wheel(wave, 120);
            Check(Math.Abs(WaveX() - expectedX) < 1e-6, "Waveform wheel zoom used a different anchor");
            var spline = new SplineView { SyncTarget = wave };
            Layout(spline);
            wave.ViewChanged += () =>
            {
                spline.ViewStart = wave.ViewStart;
                spline.PixelsPerSecond = wave.PixelsPerSecond;
                spline.CursorTime = wave.CursorTime;
            };
            Wheel(spline, 120);
            Check(Math.Abs((spline.CursorTime - spline.ViewStart) * spline.PixelsPerSecond - expectedX) < 1e-6,
                "Spline wheel zoom did not preserve the synchronized cursor position");
            Wheel(spline, -120);
            Check(Math.Abs(WaveX() - expectedX) < 1e-6, "Spline wheel zoom-out moved the shared cursor");

            var movie = new MovieTimelineView();
            movie.SetItems(new[] { new MovieSequenceItem { FilePath = "zoom-test.json", DurationSeconds = 120 } });
            Layout(movie);
            movie.ZoomToFit();
            movie.CursorTime = 60;
            movie.ZoomBy(4);
            movie.PanTo(20);
            movie.CursorTime = movie.ViewStart + fraction * movie.VisibleSeconds;
            double movieTime = movie.CursorTime;
            double MovieX() => (movie.CursorTime - movie.ViewStart) * movie.PixelsPerSecond;
            double movieX = MovieX();
            for (int repeat = 0; repeat < 20; repeat++)
            {
                movie.ZoomBy(1.5);
                Check(Math.Abs(MovieX() - movieX) < 1e-6, "Movie button zoom moved the cursor on screen");
                movie.ZoomBy(1 / 1.5);
                Check(Math.Abs(MovieX() - movieX) < 1e-6 && movie.CursorTime == movieTime,
                    "Movie zoom-out drifted the cursor or changed playback time");
            }
            Wheel(movie, 120);
            Check(Math.Abs(MovieX() - movieX) < 1e-6, "Movie wheel zoom used a different anchor");
            foreach (double invalid in new[] { 0.0, -1, double.NaN, double.PositiveInfinity })
            {
                double wavePps = wave.PixelsPerSecond, moviePps = movie.PixelsPerSecond;
                wave.ZoomBy(invalid);
                movie.ZoomBy(invalid);
                Check(wave.PixelsPerSecond == wavePps && movie.PixelsPerSecond == moviePps,
                    "Invalid zoom factor corrupted timeline scale");
            }
            wave.SetViewStart(0);
            wave.CursorTime = 0;
            wave.ZoomBy(2);
            Check(WaveX() == 0, "Zooming at the timeline start moved its cursor");
            movie.PanTo(120);
            movie.CursorTime = 120;
            double endX = MovieX();
            movie.ZoomBy(2);
            Check(Math.Abs(MovieX() - endX) < 1e-6, "Zooming at the movie end moved its cursor");
            wave.ZoomBy(0.00001);
            movie.ZoomBy(0.00001);
            Check(wave.ViewStart >= 0 && movie.ViewStart >= 0, "Zoom-out scrolled before the timeline start");
            wave.ZoomToFit();
            movie.ZoomToFit();
            Check(wave.ViewStart == 0 && movie.ViewStart == 0 &&
                Math.Abs(wave.VisibleSeconds - 120) < 1e-6 && Math.Abs(movie.VisibleSeconds - 120) < 1e-6,
                "Explicit Fit no longer shows the entire timeline");
        }
    }

    private static void TimelineInteractionChanges()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var wave = new WaveformView { Duration = 120, ContentDuration = 120, Markers = new[] { 0.0, 1.0 } };
        var spline = new SplineView { Duration = 120 };
        foreach (var view in new System.Windows.FrameworkElement[] { wave, spline })
        {
            view.Measure(new System.Windows.Size(800, 200));
            view.Arrange(new System.Windows.Rect(0, 0, 800, 200));
        }
        wave.ZoomToFit();
        spline.PixelsPerSecond = wave.PixelsPerSecond;
        var handle = (SKRect)typeof(WaveformView).GetMethod("OffsetHandleRect", flags).Invoke(wave, null);
        Check(wave.XAtTime(0) - 6 > handle.Right + 3, "Zero-time triangle is obscured by the audio grab handle or its hit target");
        Check(wave.XAtTime(0) == WaveformView.TimelineLeftInset && spline.XAtTime(0) == wave.XAtTime(0),
            "Waveform and Spline do not share their permanent left inset");
        Check(wave.TimeAtX(wave.XAtTime(0)) == 0 && spline.TimeAtX(spline.XAtTime(0)) == 0,
            "Timeline inset changed the time at the origin");
        Check(Math.Abs(wave.XAtTime(120) - wave.ActualWidth) < 0.001,
            "Fit clipped the end of the Sequence after adding the left inset");
        wave.AudioOffset = 200;
        Check(wave.XAtTime(0) == WaveformView.TimelineLeftInset, "Left inset depends on audio-handle visibility");
        wave.AudioOffset = 0;

        var edits = new List<double>();
        wave.CommandsEditRequested += edits.Add;
        var zeroTriangle = new System.Windows.Point(wave.XAtTime(0), 4.5);
        Check(!wave.TryEditMarker(zeroTriangle, 1) && edits.Count == 0, "Single-click triangle incorrectly opened Edit Commands");
        Check(wave.TryEditMarker(zeroTriangle, 2) && edits.SequenceEqual(new[] { 0.0 }),
            "Double-clicking the zero-time triangle did not request its command editor");
        Check(!wave.TryEditMarker(new System.Windows.Point(24, 80), 2), "Double-clicking blank waveform opened a command editor");
        wave.SetViewStart(20);
        wave.ZoomBy(4);
        spline.ViewStart = wave.ViewStart;
        spline.PixelsPerSecond = wave.PixelsPerSecond;
        foreach (double time in new[] { 0.0, 1, 20, 40, 120 })
        {
            Check(Math.Abs(wave.XAtTime(time) - spline.XAtTime(time)) < 0.001,
                "Panned/zoomed Spline and waveform coordinates diverged");
            Check(Math.Abs(wave.TimeAtX(wave.XAtTime(time)) - time) < 0.0001,
                "Inset time/pixel conversion does not round-trip");
        }

        spline.ViewStart = 0;
        spline.Curves = new() { new SplineCurve
        {
            Servo = ServoNames.NeckTurn, T = new[] { 0.0, 1 }, V = new[] { 0.0, 50 },
            M = new[] { 50.0, 50 }, Min = -100, Max = 100,
        } };
        var point = new System.Windows.Point(spline.XAtTime(0), 100);
        int splineEdits = 0;
        spline.CommandsEditRequested += time => { Check(time == 0, "Spline edited the wrong time group"); splineEdits++; };
        Check(!spline.TryEditSelectedPoint(point, 2), "Unselected point incorrectly triggered the selected-point editing gesture");
        typeof(SplineView).GetField("_hasSelection", flags).SetValue(spline, true);
        typeof(SplineView).GetField("_selServo", flags).SetValue(spline, ServoNames.NeckTurn);
        typeof(SplineView).GetField("_selKey", flags).SetValue(spline, 0.0);
        Check(!spline.TryEditSelectedPoint(point, 1), "Single-click selected point opened the editor");
        Check(spline.TryEditSelectedPoint(point, 2) && splineEdits == 1, "Selected spline point did not open its command group");
        spline.Curves[0].Visible = false;
        Check(!spline.TryEditSelectedPoint(point, 2), "Hidden spline point remained double-clickable");

        var legend = new[]
        {
            new SplineLegendItem { Servo = ServoNames.NeckNodUp, Visible = false },
            new SplineLegendItem { Servo = ServoNames.NeckTiltRight, Visible = false },
            new SplineLegendItem { Servo = ServoNames.NeckTurn, Visible = true },
        };
        var visibility = legend.ToDictionary(item => item.Servo, item => item.Visible);
        MainWindow.RevealSplineLines(legend, visibility);
        Check(legend.All(item => item.Visible) && visibility.Values.All(value => value),
            "Show all did not restore every checkbox and its remembered visibility");

        var source = System.Xml.Linq.XDocument.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../ServoAnimator/MainWindow.xaml")));
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        System.Xml.Linq.XElement Find(string name) => source.Descendants().Single(e => (string)e.Attribute(x + "Name") == name);
        Check((string)Find("CommandsAtPointPanel").Attribute("Visibility") != "Collapsed" &&
            (string)Find("MovieTimelinePanel").Attribute("Visibility") != "Collapsed", "Permanent timeline panels start hidden");
        Check(!source.Descendants().Any(e => (string)e.Attribute(x + "Name") is "CommandsListToggle" or "MovieTimelineToggle" or "ViewMovieTimelineMenuItem"),
            "A removed visibility toggle remains in the UI");
        Check(Find("PlayPauseBtn").Parent.Elements().Take(3).Select(e => (string)e.Attribute("Click"))
            .SequenceEqual(new[] { "ZoomOut_Click", "ZoomIn_Click", "ZoomFit_Click" }), "Sequence zoom controls do not precede playback controls");
        Check((string)Find("SplineShowAllButton").Attribute("Click") == "SplineShowAll_Click", "Show all is not wired up");
        Check((string)Find("PlayPauseBtn").Parent.Elements().ElementAt(2).Attribute("Margin") == "2,0,28,0",
            "Sequence zoom/Fit has lost its spacing before playback");
        Check(typeof(EditorLayoutSettings).GetProperty("CommandsVisible") == null &&
            typeof(EditorLayoutSettings).GetProperty("MovieTimelineVisible") == null, "Legacy visibility settings can still hide permanent panels");
    }

    private static void HelpTutorials()
    {
        var catalog = HelpCatalog.Current;
        var topic = catalog.Find("step-by-step");
        Check(topic?.Id == "step-by-step", "Step-by-step tutorial is missing from the Help contents");
        Check(catalog.Search("tutorial").Any(t => t.Id == topic.Id), "Tutorial is not searchable");
        var text = catalog.Read(topic);
        Check(text.Contains("## 9. Save, reopen") && text.Contains("Config > Set Paths"),
            "Published walkthrough is missing content or names the wrong menu");
        Check(catalog.Topics.Select(t => t.Id).Distinct().Count() == catalog.Topics.Count,
            "Duplicate Help topic IDs");
        foreach (var item in catalog.Topics)
        {
            Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "Help", item.File)), "Missing Help file: " + item.File);
            Check(item.Related.All(id => catalog.Topics.Any(t => t.Id == id)), "Broken related Help topic: " + item.Id);
        }

        // Construct without showing a window: exercise the real native Help renderer.
        var window = new HelpWindow();
        try
        {
            var document = window.MarkdownToFlowDocument("# Test\n\n1. **First** step\n2. Second step\nAfter the list\n\n- Bullet\n1) Numbered again\n\n3. Continue here");
            var blocks = document.Blocks.Cast<System.Windows.Documents.Block>().ToList();
            var lists = blocks.OfType<System.Windows.Documents.List>().ToList();
            Check(lists.Count == 4, "Help merged numbered and bulleted lists");
            Check(lists[0].MarkerStyle == System.Windows.TextMarkerStyle.Decimal && lists[0].ListItems.Count == 2,
                "Numbered instructions were not rendered as separate steps");
            Check(lists[1].MarkerStyle == System.Windows.TextMarkerStyle.Disc && lists[2].StartIndex == 1 && lists[3].StartIndex == 3,
                "Help list markers or starting numbers are incorrect");
            Check(blocks[2] is System.Windows.Documents.Paragraph,
                "Paragraph after a list appeared before its steps");
            var tutorial = window.MarkdownToFlowDocument(text);
            Check(tutorial.Blocks.OfType<System.Windows.Documents.List>().Count(l => l.MarkerStyle == System.Windows.TextMarkerStyle.Decimal) == 10,
                "One or more tutorial workflows lost their numbered steps");
        }
        finally { window.Close(); }
    }

    private static void CommandConflictChoices()
    {
        ServoCommand Cmd(double time, int value) => new()
        {
            OffsetSeconds = time, Servo = ServoNames.NeckTurn, NumericValue = value,
        };
        var first = Cmd(1.0001, 10);
        var middle = Cmd(1.0002, 20);
        middle.Speed = ServoSpeed.Slow;
        var last = Cmd(1.0003, 30);
        last.Disable = true;
        var otherTime = Cmd(1.002, 40);
        var otherServo = new ServoCommand { OffsetSeconds = 1, Servo = ServoNames.NoseBody };
        var childControl = Enum.GetValues<RobotControls>()[0];
        var child = Cmd(1, 50);
        child.Control = childControl;
        var all = new List<ServoCommand> { first, otherTime, otherServo, middle, child, last };
        var groups = CommandConflicts.Find(all);
        Check(groups.Count == 1 && groups[0].Commands.SequenceEqual(new[] { first, middle, last }),
            "Conflicts lost insertion order, millisecond grouping, or distinct targets");
        foreach (var keep in new[] { first, middle, last })
        {
            var result = all.ToList();
            CommandConflicts.KeepSelected(result, groups, new[] { keep });
            Check(result.SequenceEqual(all.Where(c => c == keep || c == otherTime || c == otherServo || c == child)),
                "Choosing a candidate removed an unrelated command or changed survivor order");
            Check(CommandConflicts.Find(result).Count == 0, "A redundant command survived selection");
        }
        var invalid = all.ToList();
        bool rejected = false;
        try { CommandConflicts.KeepSelected(invalid, groups, new[] { otherTime }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected && invalid.SequenceEqual(all), "Invalid selection partially deleted commands");
        var identical = new List<ServoCommand> { first, first.Clone() };
        Check(CommandConflicts.Find(identical).Count == 1, "Identical values were allowed as duplicate commands");
        var text = new List<ServoCommand>
        {
            new() { Servo = ServoNames.RGBCommand, TextValue = "ClearAll" },
            new() { Servo = ServoNames.RGBCommand, TextValue = "SetRGBColor 255 0 0" },
            new() { Servo = ServoNames.Play, TextValue = "first.wav" },
            new() { Servo = ServoNames.Play, TextValue = "second.wav" },
        };
        var textGroups = CommandConflicts.Find(text);
        Check(textGroups.Count == 2, "RGB and audio conflicts were not grouped independently");
        CommandConflicts.KeepSelected(text, textGroups, new[] { text[1], text[2] });
        Check(text.Count == 2 && text[0].TextValue.Contains("255") && text[1].TextValue == "first.wav",
            "Independent conflict selections did not keep the requested text values");
    }

    private static void CommandDrafts()
    {
        var original = new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = 10 };
        var live = new List<ServoCommand> { original };
        var session = new CommandEditSession(live, 0);
        var added = new ServoCommand { Servo = ServoNames.NeckTurn, NumericValue = 80 };
        session.Commands.Add(added);
        var merged = session.Merge(live);
        Check(live.Count == 1 && live[0].NumericValue == 10, "Draft insertion modified the playable document");
        Check(CommandConflicts.Find(merged)[0].Commands.Select(c => c.NumericValue).SequenceEqual(new[] { 10, 80 }),
            "Draft insertion did not present the existing value before the new value");
        // Cancel needs only discard the merged candidate list.
        Check(session.Commands.Count == 2 && live.Count == 1, "Merging consumed the editable drafts");

        var edit = new CommandEditSession(live, 0);
        edit.Commands[0].NumericValue = 25;
        var concurrent = new ServoCommand { Servo = ServoNames.NoseBody, NumericValue = 40 };
        live.Add(concurrent);
        var applied = edit.Merge(live);
        Check(applied.Count == 2 && applied.Contains(concurrent) && applied.Any(c => c.NumericValue == 25),
            "Applying an editor session lost a concurrent main-window insertion");
        Check(original.NumericValue == 10, "Draft editing modified the live command before applying");
        original.NumericValue = 15;
        var conflict = edit.Merge(live);
        Check(CommandConflicts.Find(conflict)[0].Commands.Select(c => c.NumericValue).SequenceEqual(new[] { 15, 25 }),
            "Concurrent edits silently replaced the existing command instead of allowing a choice");

        var moving = new CommandEditSession(live, 0);
        moving.Commands[0].OffsetSeconds = 2;
        var destination = new ServoCommand { OffsetSeconds = 2, Servo = ServoNames.NeckTurn, NumericValue = 90 };
        live.Add(destination);
        var moved = moving.Merge(live);
        Check(CommandConflicts.Find(moved)[0].Commands.Select(c => c.NumericValue).SequenceEqual(new[] { 90, 15 }),
            "Moving a draft to an occupied point skipped conflict detection");
        live.Remove(original);
        Check(!edit.Merge(live).Any(c => c.Servo == ServoNames.NeckTurn && c.OffsetSeconds == 0),
            "Applying a stale draft resurrected a command deleted in the main window");
    }

    private static void QueueCancellation()
    {
        using var queue = new OrderedActionQueue("queue checks");
        using var active = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var complete = new ManualResetEventSlim();
        var output = new ConcurrentQueue<string>();
        queue.Enqueue(() => { active.Set(); release.Wait(TimeSpan.FromSeconds(5)); output.Enqueue("in flight"); });
        Check(active.Wait(TimeSpan.FromSeconds(5)), "Worker did not start");
        for (int i = 0; i < 100; i++) queue.Enqueue(() => output.Enqueue("stale"));
        queue.ClearPending();
        queue.EnqueueBarrier(() => output.Enqueue("disable"));
        queue.ClearPending(); // a fast restart must not cancel the disable barrier
        queue.Enqueue(() => { output.Enqueue("new run"); complete.Set(); });
        release.Set();
        Check(complete.Wait(TimeSpan.FromSeconds(5)), "Worker did not finish");
        Check(output.SequenceEqual(new[] { "in flight", "disable", "new run" }),
            "Old playback commands ran after cancellation or crossed the disable barrier");
        using var final = new ManualResetEventSlim();
        for (int i = 0; i < 10; i++) queue.Enqueue(() => output.Enqueue("natural end"));
        queue.Enqueue(final.Set);
        Check(final.Wait(TimeSpan.FromSeconds(5)), "Natural ending did not drain");
        Check(output.Count(s => s == "natural end") == 10, "A normal finish lost commands");
    }

    private static void ClockContinuity()
    {
        var clock = new PlaybackClock();
        clock.Start(0);
        double previous = 0;
        for (int frame = 1; frame <= 3600; frame++)
        {
            double elapsed = frame / 60.0;
            double? coarseDevice = frame is > 600 and < 660 ? null : Math.Floor(elapsed * 10) / 10;
            double time = clock.AdvanceTo(elapsed, coarseDevice);
            Check(time > previous && time - previous <= 1.25 / 60 + 1e-8,
                "Clock froze, reversed, or jumped on a repeated device position");
            previous = time;
        }
        Check(Math.Abs(previous - 60) < 0.12, "Audio correction drifted excessively");
        clock.Start(2.5);
        Check(Math.Abs(clock.AdvanceTo(0, null) - 2.5) < 1e-9, "Seek did not reset clock");
    }

    private static void EnvelopeQueries()
    {
        var random = new Random(421);
        foreach (int count in new[] { 1, 3, 64, 127, 10001 })
        {
            var min = Enumerable.Range(0, count).Select(_ => -(float)random.NextDouble()).ToArray();
            var max = Enumerable.Range(0, count).Select(_ => (float)random.NextDouble()).ToArray();
            var envelope = PeakEnvelope.For(min, max);
            for (int test = 0; test < 300; test++)
            {
                int first = random.Next(count), last = random.Next(first, count);
                var actual = envelope.Query(first, last);
                Check(actual.Min == min[first..(last + 1)].Min() && actual.Max == max[first..(last + 1)].Max(),
                    "Zoomed-out waveform summary changed an audio extremum");
            }
        }
    }

    private static void DrawingCache()
    {
        var info = new SKImageInfo(64, 32);
        using var target = SKSurface.Create(info);
        using var cache = new TimelineDrawingCache();
        int draws = 0;
        void Draw(SKCanvas canvas) { draws++; canvas.Clear(SKColors.Green); }
        cache.Draw(target.Canvas, info, (64, 32, 0.0, "Dark"), Draw);
        for (int frame = 0; frame < 120; frame++)
            cache.Draw(target.Canvas, info, (64, 32, 0.0, "Dark"), Draw);
        Check(draws == 1, "Cursor frames rebuilt static content");
        cache.Draw(target.Canvas, info, (64, 32, 1.0, "Dark"), Draw);
        cache.Draw(target.Canvas, info, (64, 32, 1.0, "Light"), Draw);
        cache.Invalidate();
        cache.Draw(target.Canvas, info, (64, 32, 1.0, "Light"), Draw);
        Check(draws == 4, "Pan/theme/edit did not invalidate drawing cache");
    }

    private static async Task MediaFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ServoAnimator-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string audio = Path.Combine(folder, "stereo.wav");
            void WriteAudio(float amplitude)
            {
                using var writer = new WaveFileWriter(audio, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                for (int frame = 0; frame < 44100; frame++)
                {
                    writer.WriteSample(0);
                    writer.WriteSample(amplitude); // signal exclusively on the right channel
                }
            }
            WriteAudio(0.5f);
            var cache = new MediaAssetCache();
            var first = await cache.Audio(audio);
            Check(first.Max.Length == 1000 && first.Max.All(v => v == 0.5f),
                "44.1 kHz stereo waveform lost a channel or drifted");
            Check(ReferenceEquals(first, await cache.Audio(audio)), "Unchanged audio was decoded again");
            WriteAudio(0.25f);
            File.SetLastWriteTimeUtc(audio, DateTime.UtcNow.AddSeconds(2));
            var edited = await cache.Audio(audio);
            Check(!ReferenceEquals(first, edited) && edited.Max.All(v => v == 0.25f),
                "Edited audio reused stale peaks");

            string sequence = Path.Combine(folder, "cue.json");
            var doc = new AnimationDocument { AudioFilePath = "stereo.wav", AudioStartOffsetSeconds = 2 };
            doc.Save(sequence);
            var info = await cache.DescribeSequence(sequence, folder);
            Check(Math.Abs(info.Duration - 3) < 0.001, "Sequence duration ignored audio offset");
            var clone = (await cache.Sequence(sequence)).Clone();
            clone.Description = "unsaved edit";
            Check((await cache.Sequence(sequence)).Description != clone.Description, "Editor mutated cached document");
            doc.Description = "external edit";
            doc.Save(sequence);
            File.SetLastWriteTimeUtc(sequence, DateTime.UtcNow.AddSeconds(3));
            Check((await cache.Sequence(sequence)).Description == "external edit", "External sequence edit was not reloaded");
            Check(!ConfigPathService.TryResolve(folder, "..\\outside.wav", out _), "Path escaped Config");

            // Repeated save/load must preserve the order used by the conflict
            // chooser, even for groups large enough to trigger unstable sorts.
            var ordered = Enumerable.Range(0, 50).Select(i => new ServoCommand
            {
                Servo = ServoNames.NeckTurn, OffsetSeconds = i % 2,
                NumericValue = i, Reason = $"added {i}",
            }).ToList();
            var orderingDoc = new AnimationDocument { Commands = ordered.ToList() };
            orderingDoc.Save(sequence);
            Check(orderingDoc.Commands.SequenceEqual(ordered), "Saving reordered the in-memory command list");
            var reloaded = AnimationDocument.Load(sequence);
            Check(reloaded.Commands.Select(c => c.Reason).SequenceEqual(ordered.OrderBy(c => c.OffsetSeconds).Select(c => c.Reason)),
                "Saving changed the relative order of same-time commands");
            string library = Path.Combine(folder, "library.json");
            AnimationDocument.SaveCommandsOnly(library, ordered);
            Check(ordered.Select(c => c.NumericValue).SequenceEqual(Enumerable.Range(0, 50)),
                "Saving a library sequence mutated its caller's command order");
            Check(AnimationDocument.LoadLibraryItem(library).Commands.Where(c => c.OffsetSeconds == 0)
                .Select(c => c.NumericValue).SequenceEqual(Enumerable.Range(0, 50).Where(i => i % 2 == 0)),
                "Library save lost oldest-to-newest order within a time point");
        }
        finally
        {
            // Delete only the unique test directory created above.
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static void MovieDescriptionLinks()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var render = typeof(MovieTimelineView).GetMethod("OnRender", flags);
        var regionsField = typeof(MovieTimelineView).GetField("_descriptionEditRegions", flags);
        var hitTest = typeof(MovieTimelineView).GetMethod("DescriptionEditAt", flags);
        foreach (double width in new[] { 48.0, 90.0, 320.0 })
        foreach (string description in new[] { "", "Head nod", string.Concat(Enumerable.Repeat("Long description with wrapping. ", 50)), "First line\nSecond line\nThird line\nFourth line" })
        {
            var view = new MovieTimelineView();
            view.SetItems(new[] { new MovieSequenceItem { FilePath = "test.json", DurationSeconds = 2, Description = description } });
            view.Measure(new System.Windows.Size(width, 64));
            view.Arrange(new System.Windows.Rect(0, 0, width, 64));
            var drawing = new System.Windows.Media.DrawingGroup();
            using (var dc = drawing.Open()) render.Invoke(view, new object[] { dc });
            var regions = (Dictionary<int, System.Windows.Media.Geometry>)regionsField.GetValue(view);
            Check(regions.Count == 1, "A blank or truncated movie description lost its edit link");
            var bounds = regions[0].Bounds;
            Check(!bounds.IsEmpty && bounds.Left >= 7 && bounds.Right <= view.XAtTime(view.TotalDuration) - 10 && bounds.Bottom <= 54,
                "Movie description edit link escaped the block's description area");
            var center = new System.Windows.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            Check((int)hitTest.Invoke(view, new object[] { center }) == 0, "Movie description edit text is not clickable");
            Check((int)hitTest.Invoke(view, new object[] { new System.Windows.Point(5, 5) }) == -1,
                "Clicking a movie block title incorrectly edits its description");
            view.SetItems(Array.Empty<MovieSequenceItem>());
            Check(regions.Count == 0, "Removed movie blocks retained clickable description regions");
        }
    }

    private static void CompactTimelineLayout()
    {
        // Load only the layout fragments: no MainWindow startup, hardware or user config writes.
        var source = System.Xml.Linq.XDocument.Load(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../ServoAnimator/MainWindow.xaml")));
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        System.Xml.Linq.XElement Fragment(string name, int row)
        {
            var element = new System.Xml.Linq.XElement(source.Descendants().Single(e => (string)e.Attribute(x + "Name") == name));
            foreach (var attr in element.DescendantsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.Name.LocalName is "Click" or "Scroll").ToArray()) attr.Remove();
            foreach (var node in element.DescendantsAndSelf().Where(e => e.Name.NamespaceName == "clr-namespace:ServoAnimator"))
                node.Name = System.Xml.Linq.XName.Get(node.Name.LocalName, "clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer");
            element.SetAttributeValue("Visibility", "Visible");
            element.SetAttributeValue("Grid.Row", row);
            return element;
        }
        var markup = System.Xml.Linq.XElement.Parse("""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:local="clr-namespace:ServoAnimator;assembly=AnimationEditorPlayer">
              <Grid.Resources>
                <CornerRadius x:Key="PanelCornerRadius">4</CornerRadius>
                <CornerRadius x:Key="ControlCornerRadius">4</CornerRadius>
              </Grid.Resources>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="160"/>
              </Grid.RowDefinitions>
              <Button x:Name="PlayPauseBtn" Height="28" Content="Sequence"/>
            </Grid>
            """);
        markup.Add(Fragment("MovieTimelinePanel", 1), Fragment("SequenceDescriptionPanel", 2), Fragment("SplineArea", 3));
        var commandsFragment = Fragment("CommandsAtPointPanel", 4);
        foreach (var attr in commandsFragment.DescendantsAndSelf().SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName is "MouseDoubleClick" or "PreviewKeyDown" or "SelectionChanged").ToArray()) attr.Remove();
        markup.Add(commandsFragment);
        var grid = (System.Windows.Controls.Grid)System.Windows.Markup.XamlReader.Parse(markup.ToString());
        T Find<T>(string name) => (T)grid.FindName(name);
        void Layout()
        {
            grid.Measure(new System.Windows.Size(1000, 650));
            grid.Arrange(new System.Windows.Rect(0, 0, 1000, 650));
            grid.UpdateLayout();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            grid.UpdateLayout();
        }
        ((System.Windows.Data.CompositeCollection)Find<System.Windows.Controls.ItemsControl>("SplineLegend").ItemsSource)
            .OfType<System.Windows.Data.CollectionContainer>().Single().Collection = new[]
            { new SplineLegendItem { Name = "Neck turn", Visible = true, Brush = System.Windows.Media.Brushes.Black } };
        var movieDescription = Find<System.Windows.Documents.Run>("MovieDescriptionRun");
        movieDescription.Text = "Short movie description";
        Layout();
        var moviePanel = Find<System.Windows.Controls.Border>("MovieTimelinePanel");
        double compactHeight = moviePanel.ActualHeight;
        Check(double.IsNaN(moviePanel.Height) && compactHeight < 144, "Movie panel retained fixed-height empty space");
        Check(Find<System.Windows.Controls.Button>("MoviePlayButton").ActualHeight == 28,
            "Movie buttons do not match the Sequence button height");
        movieDescription.Text = string.Concat(Enumerable.Repeat("Long wrapping movie description. ", 20));
        Layout();
        Check(moviePanel.ActualHeight > compactHeight, "Movie panel did not grow for a wrapping description");
        movieDescription.Text = "";
        Layout();
        Check(moviePanel.ActualHeight <= compactHeight, "Movie panel did not shrink after shortening its description");
        foreach (string prefix in new[] { "Movie", "Sequence" })
        {
            var block = Find<System.Windows.Controls.TextBlock>(prefix + "DescriptionText");
            var edit = Find<System.Windows.Documents.Hyperlink>(prefix + "DescriptionEditButton");
            Check(block.Inlines.LastInline == edit && edit.FontWeight == System.Windows.FontWeights.Bold,
                "Description edit link is not bold inline text following the description");
        }
        Check(Find<object>("MovieCreatedText") == null, "Movie Created Date is still in the UI");
        var spline = Find<SplineView>("Spline");
        var legend = Find<System.Windows.Controls.ItemsControl>("SplineLegend");
        var area = Find<System.Windows.Controls.Border>("SplineArea");
        double legendBottom = legend.TranslatePoint(new System.Windows.Point(0, legend.ActualHeight), area).Y;
        Check(spline.TranslatePoint(new System.Windows.Point(), area).Y >= legendBottom,
            "Spline legend overlaps the graph instead of sitting above it");
        var commands = Find<System.Windows.Controls.ListBox>("CommandsAtPointList");
        double listHeight = commands.ActualHeight;
        grid.RowDefinitions[4].Height = new System.Windows.GridLength(220);
        Layout();
        Check(commands.ActualHeight > listHeight + 50 && double.IsNaN(commands.Height),
            "Commands list does not stretch when its pane is resized");
        var header = Find<System.Windows.Controls.TextBlock>("CommandsAtPointHeader");
        Check(System.Windows.Controls.Grid.GetColumn(header) == 3, "Commands heading still precedes its action buttons");
        Check(!source.Descendants().Any(e => ((string)e.Attribute(x + "Name"))?.Contains("ServoGrid") == true),
            "Obsolete Servo Grid controls remain in the UI");
        var commandPanel = source.Descendants().Single(e => (string)e.Attribute(x + "Name") == "CommandsAtPointPanel");
        Check((string)commandPanel.Attribute("Grid.Row") == "2" && (string)commandPanel.Attribute("Grid.Column") == "0",
            "Commands did not replace the old Grid pane beside the URDF");
    }

    private static void SplineDraftsAndGrid()
    {
        var initial = new List<ServoNames> { ServoNames.NeckTurn };
        var draft = new CommandSplineDraft(initial);
        CommandVM Row(ServoNames servo) => new(new ServoCommand { Servo = servo }, null, null, null, null, null, draft);
        var first = Row(ServoNames.NeckTurn);
        var second = Row(ServoNames.NeckTurn);
        int notices = 0;
        second.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CommandVM.SplineEnabled)) notices++; };
        Check(first.SplineEnabled && first.SupportsSpline, "Spline checkbox did not load the current setting");
        first.SplineEnabled = false;
        Check(!second.SplineEnabled && notices > 0, "Rows for the same servo lost synchronized spline checkboxes");
        Check(initial.SequenceEqual(new[] { ServoNames.NeckTurn }) && draft.Changes[ServoNames.NeckTurn] == false,
            "Canceled spline drafts would modify the source setting");
        first.SplineEnabled = true;
        Check(draft.Changes.Count == 0, "Reverted spline checkbox still counts as a change");
        var nod = Row(ServoNames.NeckNodUp);
        var tilt = Row(ServoNames.NeckTiltRight);
        nod.SplineEnabled = true;
        Check(tilt.SplineEnabled && draft.Changes.Count == 2, "Shared Neck Nod/Tilt spline settings diverged");
        tilt.SplineEnabled = false;
        Check(!nod.SplineEnabled && draft.Changes.Count == 0, "Disabling shared neck spline did not synchronize");
        first.SelectedServoItem = CommandVM.ServoPickOptions.First(i => i.Servo == ServoNames.RGBCommand);
        first.SplineEnabled = true;
        Check(!first.SupportsSpline && !first.SplineEnabled, "RGB unexpectedly enabled a numeric spline");
        var child = CommandVM.ServoPickOptions.First(i => i.Control.HasValue);
        first.SelectedServoItem = child;
        Check(!first.SupportsSpline, "Child-only command implied unsupported independent spline interpolation");
        first.SelectedServoItem = CommandVM.ServoPickOptions.First(i => i.Servo == ServoNames.NeckTurn && !i.Control.HasValue);
        Check(first.SplineEnabled, "Changing the servo picklist did not refresh the spline setting");
        foreach (float height in new[] { 80f, 200f, 500f })
        {
            float previous = float.PositiveInfinity;
            for (int value = -100; value <= 100; value += 25)
            {
                float y = SplineView.StandardValueY(value, height);
                Check(y >= 8 && y <= height - 8 && y < previous, "Horizontal value grid is out of range or inverted");
                previous = y;
            }
            Check(SplineView.StandardValueY(0, height) == height / 2, "Zero grid line is not centered");
        }
    }

    private static void CommandMarkerHits()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var view = new WaveformView { Markers = new[] { 1.0, 1.01, 1.02, 1.03, 1.04 } };
        view.Measure(new System.Windows.Size(400, 200));
        view.Arrange(new System.Windows.Rect(0, 0, 400, 200));
        var visuals = (System.Collections.IEnumerable)typeof(WaveformView).GetMethod("BuildMarkerVisuals", flags).Invoke(view, new object[] { 400f });
        var hit = typeof(WaveformView).GetMethod("TryHitMarker", flags);
        foreach (object marker in visuals)
        {
            double Read(string name) => Convert.ToDouble(marker.GetType().GetProperty(name).GetValue(marker));
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var args = new object[] { new System.Windows.Point(Read("X"), (Read("BaseY") + Read("TipY")) / 2), 0.0 };
                Check((bool)hit.Invoke(view, args) && (double)args[1] == Read("Time"),
                    "Repeated hover over a staggered triangle selected the wrong command group");
            }
        }
        Check(!(bool)hit.Invoke(view, new object[] { new System.Windows.Point(100, 80), 0.0 }),
            "Hovering the waveform below the triangles selected a command marker");
        var hover = typeof(WaveformView).GetField("_hoverMarker", flags);
        hover.SetValue(view, (double?)1);
        view.Markers = new[] { 2.0 };
        Check(hover.GetValue(view) == null, "Refreshing commands retained a stale hover group");
        hover.SetValue(view, (double?)2);
        view.SetViewStart(0);
        Check(hover.GetValue(view) == null, "Panning retained stale command hover information");
    }

    private static void KeyboardFocus()
    {
        foreach (var control in new System.Windows.DependencyObject[]
        {
            new System.Windows.Controls.Slider(), new System.Windows.Controls.ListBox(),
            new System.Windows.Controls.DataGrid(), new System.Windows.Controls.ComboBox(),
            new System.Windows.Controls.TextBox(), new System.Windows.Controls.PasswordBox(),
            new System.Windows.Controls.Menu(), new System.Windows.Controls.TreeView(),
        })
            Check(MainWindow.FocusNeedsNavigationKeys(control), "Movie shortcuts stole a control's navigation keys");
        Check(!MainWindow.FocusNeedsNavigationKeys(new MovieTimelineView()), "Movie timeline could not own arrow keys");
        Check(!MainWindow.FocusNeedsNavigationKeys(null), "Empty focus blocked timeline shortcuts");
    }
}
