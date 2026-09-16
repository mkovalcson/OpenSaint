using System.Windows;
using System.Windows.Media;

namespace ServoAnimator;

public sealed partial class ControllerDiagram
{
    internal bool ShowLiveValues { get; }
    private double ArtworkWidth => Kind == ControllerKind.Steam ? 1114.5 : 823;
    private double LiveMargin => ShowLiveValues && Kind == ControllerKind.Steam ? 108 : 0;
    private readonly Dictionary<string, double> _liveSensorZero = new();
    private uint? _liveDevice;

    // Visual feedback is independent of output arming, focus, and whether an
    // input has a mapping. Reading a diagram can never emit controller actions.
    internal void SetLiveInput(ControllerSample sample, IReadOnlyDictionary<string, ControllerReading> mapped = null)
    {
        int layer = sample.Connected ? ControllerCatalog.Layer(sample.LeftShoulder, sample.RightShoulder) : -1;
        if (!sample.Connected || _liveDevice != sample.DeviceId) _liveSensorZero.Clear();
        _liveDevice = sample.Connected ? sample.DeviceId : null;
        var readings = new Dictionary<string, ControllerReading>();
        if (sample.Connected)
        foreach (var input in ControllerCatalog.Inputs(Kind, NoMux))
        {
            bool available = sample.TryValue(input.Id, out double raw);
            if (!available && !sample.Supported.Contains(input.Id)) continue;
            if (!double.IsFinite(raw)) continue;
            ControllerBinding binding = null;
            Mappings?.TryGetValue(input.Id, out binding);
            double value = raw;
            if (input.Id.StartsWith("Accel"))
            {
                if (!_liveSensorZero.ContainsKey(input.Id)) _liveSensorZero[input.Id] = raw;
                value -= _liveSensorZero[input.Id];
            }
            if (input.Section == "Motion") value /= binding?.SensorScale ?? 1;
            double deadZone = input.Section == "Triggers" || input.Id.EndsWith("Pressure") ? .01 : binding?.DeadZone ?? .12;
            bool active = available && (input.Analog ? Math.Abs(value) > deadZone : raw > .5);
            var reading = new ControllerReading(raw, active, null, available ? "" : "Not touched");
            if (mapped?.TryGetValue(input.Id, out var actual) == true)
                reading = actual with { Raw = raw, Active = active };
            readings[input.Id] = reading;
        }
        bool changed = LiveLayer != layer || LiveReadings == null || LiveReadings.Count != readings.Count
            || readings.Any(p => !LiveReadings.TryGetValue(p.Key, out var previous) || previous != p.Value);
        LiveLayer = layer; LiveReadings = readings;
        if (changed) InvalidateVisual();
    }

    private void DrawLiveValue(DrawingContext dc, string id, Rect card, bool left)
    {
        // Inner-column values face the controller; outer-column values face
        // away from it. Reserve margins so neither outer column is clipped.
        bool onRight = left != IsOuter(id);
        double width = Kind == ControllerKind.Steam ? 100 : 78;
        double x = onRight ? card.Right + 5 : card.Left - width - 5;
        var box = new Rect(x, card.Y + 1, width, MappingCardHeight - 2);
        bool available = LiveReadings?.TryGetValue(id, out _) == true;
        var reading = available ? LiveReadings[id] : null;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20, 30, 42)), null, box, 3, 3);
        Brush brush = reading?.Active == true ? Brushes.LightGreen : Brushes.LightSlateGray;
        Label(dc, reading == null ? "Raw: —" : $"Raw: {reading.Raw:0.000}", x + 3, box.Y, 12, brush, width - 6);
        Label(dc, reading?.Mapped is double value ? $"→ {value:0.##}" : "→ —", x + 3, box.Y + 16, 12, brush, width - 6);
    }
}
