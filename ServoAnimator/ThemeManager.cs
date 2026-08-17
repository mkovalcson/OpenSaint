// ---------------------------------------------------------------------------
// ThemeManager.cs
//
// Small runtime theme service for Animation Editor & Player.  Themes change
// the shared SolidColorBrush resources declared in App.xaml, so controls that
// already reference those brush instances update immediately without a window
// restart.  The selected theme is persisted per-user under LocalAppData.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ServoAnimator
{
    public static class ThemeManager
    {
        public const string DefaultTheme = "Graphite";

        public static readonly string[] ThemeNames =
        {
            "Graphite",
            "Steel Blue",
            "Teal",
            "Violet",
        };

        public static string CurrentTheme { get; private set; } = DefaultTheme;

        private sealed class ThemeSettings
        {
            public string Theme { get; set; } = DefaultTheme;
        }

        private sealed record Palette(
            string AppBackground,
            string PanelBackground,
            string ElevatedPanelBackground,
            string MenuBackground,
            string InputBackground,
            string ControlBackground,
            string ControlHover,
            string ControlPressed,
            string ControlBorder,
            string DividerBrush,
            string PrimaryText,
            string SecondaryText,
            string HeaderText,
            string SequenceAccent,
            string SequenceAccentSurface,
            string SequencePanelBackground,
            string SequenceBadgeBackground,
            string MovieAccent,
            string MovieAccentSurface,
            string MoviePanelBackground,
            string MovieBadgeBackground,
            string SelectionBackground,
            string FocusBorder,
            string StatusBackground,
            string InspectorBackground,
            string WarningText);

        private static readonly Dictionary<string, Palette> Palettes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Original v1.0.7 palette.  This intentionally remains the
                // default so an existing installation looks the same until a
                // user selects another theme.
                ["Graphite"] = new(
                    "#1E2126", "#23262C", "#282C33", "#2A2E35", "#1C1F24",
                    "#30353D", "#3B424C", "#47515D", "#4A5160", "#3A3F48",
                    "#E4E7ED", "#AAB1BC", "#8FA3BF",
                    "#5F8FC4", "#40566E", "#272D35", "#334B65",
                    "#C09655", "#5A482C", "#292820", "#5A482C",
                    "#40566E", "#5A80A8", "#181B1F", "#20242A", "#FFD37A"),

                ["Steel Blue"] = new(
                    "#19212B", "#202A35", "#26323E", "#222D39", "#17202A",
                    "#2B3947", "#35485A", "#40576D", "#50657A", "#354555",
                    "#E8EEF5", "#AAB8C7", "#91A9C0",
                    "#6CA8E6", "#355B7F", "#202E3C", "#315677",
                    "#D2A45E", "#674F2D", "#2B2922", "#604A2B",
                    "#355B7F", "#75A8D8", "#141B23", "#1B2733", "#FFD27A"),

                ["Teal"] = new(
                    "#192321", "#202C2A", "#263432", "#22302E", "#17211F",
                    "#2A3A37", "#354A46", "#405B55", "#4F6A64", "#354944",
                    "#E7F0EE", "#A9BBB7", "#8FB4AD",
                    "#56B8AA", "#315F59", "#20312E", "#2C5B54",
                    "#D0A15D", "#654E2C", "#2B2921", "#5E492A",
                    "#315F59", "#63B2A6", "#141C1A", "#1B2926", "#FFD27A"),

                ["Violet"] = new(
                    "#211D29", "#292431", "#302A39", "#2C2734", "#1D1924",
                    "#383141", "#473D52", "#584B66", "#655A72", "#4A414F",
                    "#EEEAF3", "#BAB0C5", "#AE9FBE",
                    "#9B82D4", "#51446F", "#30283A", "#4E4168",
                    "#D1A05B", "#66502E", "#302B24", "#604B2C",
                    "#51446F", "#A48AD9", "#19151F", "#27202F", "#FFD27A"),
            };

        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AnimationEditorPlayer");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "ui-settings.json");
            }
        }

        public static void LoadAndApply()
        {
            string theme = DefaultTheme;
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = JsonSerializer.Deserialize<ThemeSettings>(
                        File.ReadAllText(SettingsPath));
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.Theme))
                        theme = settings.Theme;
                }
            }
            catch
            {
                // A corrupt/unreadable preference must never block application startup.
            }

            Apply(theme, persist: false);
        }

        public static void Apply(string themeName, bool persist = true)
        {
            if (!Palettes.TryGetValue(themeName ?? "", out var palette))
            {
                themeName = DefaultTheme;
                palette = Palettes[DefaultTheme];
            }

            SetBrush("AppBackground", palette.AppBackground);
            SetBrush("PanelBackground", palette.PanelBackground);
            SetBrush("ElevatedPanelBackground", palette.ElevatedPanelBackground);
            SetBrush("MenuBackground", palette.MenuBackground);
            SetBrush("InputBackground", palette.InputBackground);
            SetBrush("ControlBackground", palette.ControlBackground);
            SetBrush("ControlHover", palette.ControlHover);
            SetBrush("ControlPressed", palette.ControlPressed);
            SetBrush("ControlBorder", palette.ControlBorder);
            SetBrush("DividerBrush", palette.DividerBrush);
            SetBrush("PrimaryText", palette.PrimaryText);
            SetBrush("SecondaryText", palette.SecondaryText);
            SetBrush("HeaderText", palette.HeaderText);
            SetBrush("SequenceAccent", palette.SequenceAccent);
            SetBrush("SequenceAccentSurface", palette.SequenceAccentSurface);
            SetBrush("SequencePanelBackground", palette.SequencePanelBackground);
            SetBrush("SequenceBadgeBackground", palette.SequenceBadgeBackground);
            SetBrush("MovieAccent", palette.MovieAccent);
            SetBrush("MovieAccentSurface", palette.MovieAccentSurface);
            SetBrush("MoviePanelBackground", palette.MoviePanelBackground);
            SetBrush("MovieBadgeBackground", palette.MovieBadgeBackground);
            SetBrush("SelectionBackground", palette.SelectionBackground);
            SetBrush("FocusBorder", palette.FocusBorder);
            SetBrush("StatusBackground", palette.StatusBackground);
            SetBrush("InspectorBackground", palette.InspectorBackground);
            SetBrush("WarningText", palette.WarningText);

            CurrentTheme = themeName;

            if (!persist) return;
            try
            {
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(new ThemeSettings { Theme = themeName },
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Theme selection still applies for this session if persistence fails.
            }
        }

        public static Color GetColor(string key, Color fallback)
        {
            return Application.Current?.Resources[key] is SolidColorBrush brush
                ? brush.Color
                : fallback;
        }

        private static void SetBrush(string key, string colorText)
        {
            if (Application.Current?.Resources[key] is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                {
                    var replacement = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(colorText));
                    Application.Current.Resources[key] = replacement;
                }
                else
                {
                    brush.Color = (Color)ColorConverter.ConvertFromString(colorText);
                }
            }
        }
    }
}
