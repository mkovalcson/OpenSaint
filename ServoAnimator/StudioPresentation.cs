using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ServoAnimator
{
    /// <summary>Vector transport symbols remain sharp at every display scale.</summary>
    public sealed class StudioTransportConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString() ?? "";
            if (parameter?.ToString() == "Label")
                return text.Replace("▶|", "").Replace("▶", "").Replace("❚❚", "").Replace("■", "").Replace("|◀", "").Trim();
            string path = text.Contains("❚❚") ? "M4,3 L7,3 7,13 4,13 Z M10,3 L13,3 13,13 10,13 Z" :
                text.Contains("|◀") ? "M3,3 L5,3 5,13 3,13 Z M13,3 L6,8 13,13 Z" :
                text.Contains("▶|") ? "M11,3 L13,3 13,13 11,13 Z M3,3 L10,8 3,13 Z" :
                text.Contains("■") ? "M4,4 L12,4 12,12 4,12 Z" :
                text.Contains("Fit") ? "M2,2 L7,2 7,4 4,4 4,7 2,7 Z M9,2 L14,2 14,7 12,7 12,4 9,4 Z M2,9 L4,9 4,12 7,12 7,14 2,14 Z M12,9 L14,9 14,14 9,14 9,12 12,12 Z" :
                text.Contains("Zoom") ? (text.Contains('+') ? "M2,7 L7,7 7,2 9,2 9,7 14,7 14,9 9,9 9,14 7,14 7,9 2,9 Z" : "M2,7 L14,7 14,9 2,9 Z") :
                "M4,2 L14,8 4,14 Z";
            var geometry = Geometry.Parse(path); geometry.Freeze(); return geometry;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    internal sealed record CommandInspectorRow(string NameText, string ValueText, string SpeedText,
        string TimeText, Brush AccentBrush, string Details)
    {
        public ServoCommand Command { get; init; }
        public bool IsCommandRow => true;
        public bool SplineEnabled { get; init; }
        public bool CanSpline => SplineBreakOperations.SupportsSpline(Command);
        public bool BreakSpline => Command?.BreakSpline == true;
        public bool CanBreak => CanSpline && SplineEnabled && !Command.Disable;
        public bool CanSetBreaks => false;
        public bool CanClearBreaks => false;
    }
}
