using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ServoAnimator;

/// <summary>Controller artwork with clickable mapping callouts and fixed shoulders.</summary>
public sealed partial class ControllerDiagram : FrameworkElement
{
    public ControllerKind Kind { get; }
    public bool NoMux { get; set; }
    public bool ReadOnly { get; set; }
    public string SelectedInput { get; set; }
    public int MappingLayer { get; set; }
    public int LiveLayer { get; set; } = -1;
    public IReadOnlyDictionary<string, ControllerBinding> Mappings { get; set; }
    public IReadOnlyDictionary<string, ControllerReading> LiveReadings { get; set; }
    public event Action<string> InputSelected;
    private readonly Dictionary<Rect, string> _hits = new();
    private readonly Dictionary<string, Point> _anchors = new();
    private readonly Brush _body = new SolidColorBrush(Color.FromRgb(31, 45, 65));
    private readonly Brush _edge = new SolidColorBrush(Color.FromRgb(88, 107, 130));
    private readonly Brush _button = new SolidColorBrush(Color.FromRgb(54, 71, 94));
    private readonly Brush _accent = new SolidColorBrush(Color.FromRgb(0, 135, 135));
    internal double MappingCardWidth => Kind == ControllerKind.Steam ? 109.125 : 145.5;
    private const double MappingCardHeight = 36;
    private double InnerColumnX => 139.625;
    private double OriginX => Kind == ControllerKind.Steam ? 317.25 : 171.5;
    private const double OriginY = 20;
    public ControllerDiagram(ControllerKind kind, bool showLiveValues = false)
    {
        Kind = kind; ShowLiveValues = showLiveValues; Width = ArtworkWidth + LiveMargin * 2; Height = kind == ControllerKind.Steam ? 860 : 610; Focusable = true;
        MouseLeftButtonDown += (_, e) => { if (ReadOnly) return; string id = Hit(e.GetPosition(this)); if (id == null) return; Focus(); InputSelected?.Invoke(id); e.Handled = true; };
        MouseMove += (_, e) =>
        {
            string id = Hit(e.GetPosition(this)); Cursor = id == null || ReadOnly ? Cursors.Arrow : Cursors.Hand;
            string tip = id == null ? null : ControllerCatalog.Inputs(Kind, NoMux).First(i => i.Id == id).Label + "\n" + MappingText(id) + "\n" + LiveText(id);
            if (!Equals(ToolTip, tip)) ToolTip = tip;
        };
        KeyDown += (_, e) =>
        {
            if (ReadOnly || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
            var inputs = ControllerCatalog.Inputs(Kind, NoMux); int at = inputs.ToList().FindIndex(i => i.Id == SelectedInput);
            int step = e.Key is Key.Left or Key.Up ? -1 : 1;
            InputSelected?.Invoke(inputs[(at + step + inputs.Count) % inputs.Count].Id); e.Handled = true;
        };
    }
    private string Hit(Point point) => _hits.FirstOrDefault(h => h.Key.Contains(point)).Value;
    protected override void OnRender(DrawingContext dc)
    {
        _hits.Clear(); _anchors.Clear(); dc.PushTransform(new TranslateTransform(LiveMargin, 0));
        dc.PushTransform(new TranslateTransform(OriginX, OriginY));
        DrawController(dc); dc.Pop(); DrawMappings(dc); dc.Pop();
    }
    private void DrawController(DrawingContext dc)
    {
        Text(dc, "SHOULDER EDGE · RIGHT STAYS RIGHT", 240, 7, 11, Brushes.SlateGray);
        dc.DrawRoundedRectangle(_body, new Pen(_edge, 2), new Rect(65, 30, 350, 70), 24, 24);
        Hot(dc, "LeftTrigger", Kind == ControllerKind.Xbox ? "LT" : "L2", 80, 34, 90, 27);
        Hot(dc, "RightTrigger", Kind == ControllerKind.Xbox ? "RT" : "R2", 310, 34, 90, 27);
        if (Kind == ControllerKind.Steam && NoMux)
        {
            Hot(dc, "LeftShoulder", "Left Shoulder", 82, 68, 112, 25);
            Hot(dc, "RightShoulder", "Right Shoulder", 286, 68, 112, 25);
        }
        else
        {
        bool leftHeld = LiveLayer >= 0 && (LiveLayer & 1) != 0, rightHeld = LiveLayer >= 0 && (LiveLayer & 2) != 0;
        dc.DrawRoundedRectangle((MappingLayer & 1) != 0 ? _accent : _button, new Pen(leftHeld ? Brushes.LimeGreen : _edge, leftHeld ? 2 : 1), new Rect(82, 68, 112, 25), 8, 8);
        dc.DrawRoundedRectangle((MappingLayer & 2) != 0 ? _accent : _button, new Pen(rightHeld ? Brushes.LimeGreen : _edge, rightHeld ? 2 : 1), new Rect(286, 68, 112, 25), 8, 8);
        Text(dc, "Left MUX", 138, 72, 11, Brushes.White); Text(dc, "Right MUX", 342, 72, 11, Brushes.White);
        }
        Text(dc, "TOP / FACE VIEW", 240, 133, 11, Brushes.SlateGray);
        var outline = Geometry.Parse(Kind == ControllerKind.Steam
            ? "M 98,171 C 129,147 157,162 187,170 L 293,170 C 329,156 362,153 385,181 C 410,219 438,411 414,447 C 396,474 365,459 336,446 L 146,446 C 112,465 86,468 66,442 C 45,405 69,210 98,171 Z"
            : "M 98,171 C 129,147 157,162 187,170 L 293,170 C 329,156 362,153 385,181 C 410,219 438,371 414,412 C 395,440 365,410 332,373 L 149,373 C 120,408 86,443 66,411 C 45,376 69,210 98,171 Z");
        dc.DrawGeometry(_body, new Pen(_edge, 2), outline);
        if (Kind == ControllerKind.Xbox) { Stick(dc, "Left", 125, 225); Stick(dc, "Right", 337, 326); Dpad(dc, 132, 324); Face(dc, 350, 221); }
        else { Stick(dc, "Left", 128, 221); Stick(dc, "Right", 352, 221); Dpad(dc, 128, 316); Face(dc, 352, 316); Pad(dc, "Left", 140, 360); Pad(dc, "Right", 250, 360); }
        Hot(dc, "Guide", Kind == ControllerKind.Xbox ? "Xbox" : "Steam", 216, 177, 48, 25);
        Hot(dc, "Back", "View", 190, 226, 43, 24); Hot(dc, "Start", "Menu", 245, 226, 43, 24);
        Hot(dc, "Share", Kind == ControllerKind.Xbox ? "Share" : "…", 215, 263, 50, 24);
        if (Kind == ControllerKind.Xbox) { if (!ReadOnly) Text(dc, "Click a control or its mapping to edit", 240, 465, 12, Brushes.SlateGray); return; }
        Text(dc, "MOTION · GYRO & ACCELEROMETER", 240, 483, 11, Brushes.Teal);
        for (int i = 0; i < 3; i++)
        {
            Hot(dc, new[] { "GyroPitch", "GyroYaw", "GyroRoll" }[i], new[] { "Pitch (X)", "Yaw (Y)", "Roll (Z)" }[i], 103, 507 + i * 30, 114, 24);
            Hot(dc, "Accel" + "XYZ"[i], "Accel " + "XYZ"[i], 263, 507 + i * 30, 114, 24);
        }
        Text(dc, "UNDERSIDE · REAR BUTTONS & GRIP TOUCH", 240, 615, 11, Brushes.SlateGray);
        dc.DrawRoundedRectangle(_body, new Pen(_edge, 2), new Rect(78, 638, 324, 113), 40, 40);
        Hot(dc, "L4", "L4", 118, 650, 80, 25); Hot(dc, "R4", "R4", 282, 650, 80, 25);
        Hot(dc, "L5", "L5", 118, 683, 80, 25); Hot(dc, "R5", "R5", 282, 683, 80, 25);
        Hot(dc, "LeftGripTouch", "Left grip", 102, 718, 104, 22); Hot(dc, "RightGripTouch", "Right grip", 274, 718, 104, 22);
        if (!ReadOnly) Text(dc, "Click a control or its mapping to edit", 240, 775, 12, Brushes.SlateGray);
    }
    private void Stick(DrawingContext dc, string side, double x, double y)
    {
        // Raised stem and elliptical cap show a slight forward tilt.
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(12, 22, 36)), new Pen(_edge, 2), new Point(x, y + 12), 33, 24);
        var stem = new StreamGeometry();
        using (var g = stem.Open()) { g.BeginFigure(new Point(x - 13, y + 13), true, true); g.PolyLineTo(new[] { new Point(x - 17, y - 15), new Point(x + 17, y - 15), new Point(x + 13, y + 13) }, true, false); }
        dc.DrawGeometry(_button, new Pen(_edge, 1), stem);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(70, 91, 117)), new Pen(Brushes.SlateGray, 1.5), new Point(x, y - 15), 31, 18);
        Hot(dc, side + "Click", "click", x - 23, y - 25, 46, 18);
        if (Kind == ControllerKind.Steam) Hot(dc, side + "StickTouch", "touch", x - 25, y - 49, 50, 18);
        var pen = new Pen(Brushes.LightSlateGray, 1.5);
        var center = new Point(x, y + 12);
        dc.DrawLine(pen, center, new Point(side == "Left" ? x - 33 : x + 33, center.Y));
        dc.DrawLine(pen, center, new Point(x, y + 36));
        Hot(dc, side + "X", "X", side == "Left" ? x - 44 : x + 22, y + 2, 22, 20);
        Hot(dc, side + "Y", "Y", x - 11, y + 26, 22, 20);
    }
    private void Dpad(DrawingContext dc, double x, double y)
    {
        Hot(dc, "DpadUp", "↑", x - 11, y - 31, 24, 23); Hot(dc, "DpadDown", "↓", x - 11, y + 10, 24, 23);
        Hot(dc, "DpadLeft", "←", x - 35, y - 10, 24, 23); Hot(dc, "DpadRight", "→", x + 13, y - 10, 24, 23);
    }
    private void Face(DrawingContext dc, double x, double y)
    {
        Hot(dc, "Y", "Y", x - 12, y - 33, 25, 24); Hot(dc, "A", "A", x - 12, y + 13, 25, 24);
        Hot(dc, "X", "X", x - 38, y - 10, 25, 24); Hot(dc, "B", "B", x + 14, y - 10, 25, 24);
    }
    private void Pad(DrawingContext dc, string side, double x, double y)
    {
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(12, 25, 42)), new Pen(_edge, 1), new Rect(x, y, 90, 87), 12, 12);
        Hot(dc, side + "PadTouch", "touch", x + 4, y + 3, 82, 17);
        Hot(dc, side + "PadX", "X", x + 4, y + 24, 39, 18); Hot(dc, side + "PadY", "Y", x + 47, y + 24, 39, 18);
        Hot(dc, side + "PadClick", "click", x + 4, y + 46, 82, 17); Hot(dc, side + "PadPressure", "pressure", x + 4, y + 67, 82, 17);
    }
    private void Hot(DrawingContext dc, string id, string label, double x, double y, double w, double h)
    {
        _hits[new Rect(x + OriginX + LiveMargin, y + OriginY, w, h)] = id;
        _anchors[id] = new Point(x + OriginX + w / 2, y + OriginY + h / 2);
        dc.DrawRoundedRectangle(IsLive(id) ? Brushes.ForestGreen : SelectedInput == id ? _accent : _button, new Pen(IsLive(id) ? Brushes.LimeGreen : SelectedInput == id ? Brushes.Turquoise : _edge, SelectedInput == id || IsLive(id) ? 2 : 1), new Rect(x, y, w, h), 5, 5);
        Text(dc, label, x + w / 2, y + (h - 14) / 2, 11, Brushes.White);
    }
    private static bool OnLeft(string id) => id.StartsWith("Left") || id.StartsWith("Dpad") || id.StartsWith("Gyro") || id is "L4" or "L5" or "Back" or "Guide";
    private bool IsOuter(string id) => Kind == ControllerKind.Steam && (id is "X" or "Y" or "A" or "B" || id.StartsWith("Dpad") || id.StartsWith("Gyro") || id.StartsWith("Accel"));
    private static double OuterWireY(bool motion, int lane) => (motion ? 614 : 342) + lane * 4;
    private bool IsLive(string id) => LiveReadings?.TryGetValue(id, out var reading) == true && reading.Active;
    private string LiveText(string id) => LiveReadings?.TryGetValue(id, out var r) == true
        ? $"{r.Raw:0.###} → " + (r.State.StartsWith("Hold ") ? r.State : r.State == "Release to arm" ? "release" : r.Mapped?.ToString("0.##") ?? r.State) : "";
    private void DrawMappings(DrawingContext dc)
    {
        var cards = new List<(ControllerInput Input, Rect Rect, Point Anchor, bool Left)>();
        foreach (bool left in new[] { true, false })
        foreach (bool outer in Kind == ControllerKind.Steam ? new[] { false, true } : new[] { false })
        {
            var inputs = ControllerCatalog.Inputs(Kind, NoMux).Where(i => OnLeft(i.Id) == left && IsOuter(i.Id) == outer && _anchors.ContainsKey(i.Id)).OrderBy(i => _anchors[i.Id].Y).ToArray();
            double gap = Kind == ControllerKind.Steam ? 39 : 43;
            double[] ys = inputs.Select(i => Math.Clamp(_anchors[i.Id].Y - 16, 10, Height - 44)).ToArray();
            for (int i = 1; i < ys.Length; i++) ys[i] = Math.Max(ys[i], ys[i - 1] + gap);
            if (ys.Length > 0) ys[^1] = Math.Min(ys[^1], Height - 44);
            for (int i = ys.Length - 2; i >= 0; i--) ys[i] = Math.Min(ys[i], ys[i + 1] - gap);
            if (Kind == ControllerKind.Steam && !outer)
            {
                // Keep related inputs together, above their outgoing wire gaps.
                for (int i = 0; i < inputs.Length; i++)
                {
                    ys[i] = inputs[i].Id switch
                    {
                        "LeftTrigger" or "RightTrigger" => NoMux ? 18 : 34,
                        "LeftShoulder" or "RightShoulder" => 56,
                        "Guide" or "Start" => NoMux ? 94 : 82,
                        "LeftStickTouch" or "RightStickTouch" => 144,
                        "LeftClick" or "RightClick" => 182,
                        "LeftX" or "RightX" => 220,
                        "LeftY" or "RightY" => 258,
                        "Back" or "Share" => 296,
                        "LeftPadTouch" or "RightPadTouch" => 380,
                        "LeftPadX" or "RightPadX" => 418,
                        "LeftPadY" or "RightPadY" => 456,
                        "LeftPadClick" or "RightPadClick" => 494,
                        "LeftPadPressure" or "RightPadPressure" => 532,
                        "L4" or "R4" => 704,
                        "L5" or "R5" => 748,
                        "LeftGripTouch" or "RightGripTouch" => 792,
                        _ => ys[i]
                    };
                }
            }
            else if (Kind == ControllerKind.Steam)
            {
                foreach (bool motion in new[] { false, true })
                {
                    int[] group = Enumerable.Range(0, inputs.Length).Where(i => (inputs[i].Section == "Motion") == motion).ToArray();
                    double center = OuterWireY(motion, 0) + (group.Length - 1) * 2;
                    for (int i = 0; i < group.Length; i++) ys[group[i]] = center - 16 + (i - (group.Length - 1) / 2.0) * gap;
                }
            }
            else
            {
                // Xbox callouts follow control groups rather than anchor order.
                // Extra space separates the sticks, face buttons and D-pad.
                for (int i = 0; i < inputs.Length; i++)
                    ys[i] = inputs[i].Id switch
                    {
                        "LeftTrigger" or "RightTrigger" => 51,
                        "Guide" => 132,
                        "LeftClick" => 178,
                        "LeftX" => 221,
                        "LeftY" => 264,
                        "Back" => 322,
                        "DpadUp" => 416,
                        "DpadLeft" => 459,
                        "DpadRight" => 502,
                        "DpadDown" => 545,
                        "Y" => 160,
                        "B" => 203,
                        "X" => 246,
                        "A" => 289,
                        "Start" => 347,
                        "Share" => 390,
                        "RightClick" => 458,
                        "RightX" => 501,
                        "RightY" => 544,
                        _ => ys[i]
                    };
            }
            double width = MappingCardWidth;
            double x = Kind == ControllerKind.Steam ? (outer ? 6 : InnerColumnX) : 6;
            if (!left) x = ArtworkWidth - x - width;
            for (int i = 0; i < inputs.Length; i++) cards.Add((inputs[i], new Rect(x, ys[i], width, MappingCardHeight), _anchors[inputs[i].Id], left));
        }
        foreach (var card in cards.OrderBy(c => c.Input.Id == SelectedInput))
        {
            bool selected = card.Input.Id == SelectedInput;
            var start = new Point(card.Left ? card.Rect.Right : card.Rect.Left, card.Rect.Top + MappingCardHeight / 2);
            var end = new Point(card.Anchor.X + (card.Left ? -12 : 12), card.Anchor.Y);
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(end, false, false);
                if (IsOuter(card.Input.Id))
                {
                    bool motion = card.Input.Section == "Motion";
                    var group = cards.Where(c => c.Left == card.Left && IsOuter(c.Input.Id) && (c.Input.Section == "Motion") == motion).ToList();
                    int lane = group.FindIndex(c => c.Input.Id == card.Input.Id);
                    double y = OuterWireY(motion, lane);
                    // Parallel wires cross the inner column in its reserved gap,
                    // then fan out only after clearing that column.
                    g.PolyLineTo(new[] { new Point(OriginX + (card.Left ? 40 : 440), y), new Point(card.Left ? InnerColumnX - 12 : ArtworkWidth - InnerColumnX + 12, y), start }, true, false);
                }
                else g.PolyLineTo(new[] { new Point(card.Left ? card.Rect.Right + 12 : card.Rect.Left - 12, start.Y), start }, true, false);
            }
            dc.DrawGeometry(null, new Pen(IsLive(card.Input.Id) ? Brushes.LimeGreen : selected ? Brushes.Turquoise : new SolidColorBrush(Color.FromArgb(100, 118, 145, 173)), selected || IsLive(card.Input.Id) ? 1.7 : 0.8), line);
            dc.DrawEllipse(selected ? Brushes.Turquoise : _edge, null, end, 2, 2);
        }
        foreach (var card in cards)
        {
            bool selected = card.Input.Id == SelectedInput;
            _hits[new Rect(card.Rect.X + LiveMargin, card.Rect.Y, card.Rect.Width, card.Rect.Height)] = card.Input.Id;
            bool live = IsLive(card.Input.Id);
            dc.DrawRoundedRectangle(live ? Brushes.DarkGreen : selected ? _body : new SolidColorBrush(Color.FromRgb(27, 35, 46)), new Pen(live ? Brushes.LimeGreen : selected ? Brushes.Turquoise : _edge, selected || live ? 1.5 : 0.6), card.Rect, 5, 5);
            Label(dc, card.Input.Label, card.Rect.X + 5, card.Rect.Y + 1, 12, Brushes.LightSlateGray, card.Rect.Width - 10);
            bool recording = Mappings?.TryGetValue(card.Input.Id, out var mapped) == true && mapped.Target == ControllerCatalog.RecordingTarget;
            Label(dc, MappingText(card.Input.Id), card.Rect.X + 5, card.Rect.Y + 16, 14, recording ? Brushes.Tomato : live ? Brushes.White : selected ? Brushes.Turquoise : Brushes.WhiteSmoke, card.Rect.Width - 10);
            if (ShowLiveValues && card.Input.Analog) DrawLiveValue(dc, card.Input.Id, card.Rect, card.Left);
        }
        if (ReadOnly && cards.Count > 0)
        {
            var title = Format(Kind == ControllerKind.Xbox ? "X-Box Controller Mapping" : "Steam Controller Mapping", 22, Brushes.LightSteelBlue);
            var layer = Format(NoMux ? "No MUX · Shoulders mapped" : ControllerCatalog.LayerNames[MappingLayer], 18, Brushes.Turquoise);
            double bottom = cards.Max(c => c.Rect.Bottom);
            dc.DrawText(layer, new Point((ArtworkWidth - layer.Width) / 2, bottom - layer.Height));
            dc.DrawText(title, new Point((ArtworkWidth - title.Width) / 2, bottom - layer.Height - title.Height - 4));
        }
    }
    private string MappingText(string id)
    {
        if (Mappings == null || !Mappings.TryGetValue(id, out var binding) || string.IsNullOrEmpty(binding.Target)) return "Unassigned";
        string gate = string.IsNullOrEmpty(binding.TriggerButton) ? "" : " · hold " + binding.TriggerButton;
        if (binding.Target == ControllerCatalog.RecordingTarget) return "REC" + gate;
        if (ControllerTargets.IsLibrary(binding.Target)) return (binding.Target == "Library:pose" ? "Pose: " : "Sequence: ") + binding.LibraryName + (binding.Loop ? " ↻" : "") + gate;
        return binding.Target.Replace("Servo:", "").Replace("Action:", "").Replace("Child:", "").Replace(":", " · ") + gate;
    }
    private void Label(DrawingContext dc, string text, double x, double y, double size, Brush brush, double width)
    {
        var f = Format(text, size, brush); f.MaxTextWidth = width; f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis; dc.DrawText(f, new Point(x, y));
    }
    private FormattedText Format(string text, double size, Brush brush) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private void Text(DrawingContext dc, string text, double x, double y, double size, Brush brush) { var f = Format(text, size, brush); dc.DrawText(f, new Point(x - f.Width / 2, y)); }
}
