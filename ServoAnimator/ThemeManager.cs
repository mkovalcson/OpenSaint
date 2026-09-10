// Graphite is the editor's fixed palette, defined in App.xaml.
// Custom-drawn timeline elements use this helper to share its colors.
using System.Windows;
using System.Windows.Media;

namespace ServoAnimator
{
    public static class ThemeManager
    {
        public const string DefaultTheme = "Graphite";
        public static string CurrentTheme => DefaultTheme;

        public static void LoadAndApply()
        {
            // App.xaml supplies Graphite. Ignore legacy per-user theme settings.
        }

        public static Color GetColor(string key, Color fallback) =>
            Application.Current?.Resources[key] is SolidColorBrush brush
                ? brush.Color : fallback;
    }
}
