// ---------------------------------------------------------------------------
// EditorLayoutSettings.cs
//
// Persistent main-window arrangement. Stored as EditorLayout.json in the
// selected animator Configuration folder so each deployed/configured robot
// can remember its own editor layout independently of Paths.json.
// ---------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Windows;

namespace ServoAnimator
{
    internal sealed class GridLengthSetting
    {
        public double Value { get; set; }
        public string Unit { get; set; } = nameof(GridUnitType.Pixel);

        public static GridLengthSetting From(GridLength length) => new()
        {
            Value = length.Value,
            Unit = length.GridUnitType.ToString(),
        };

        public GridLength ToGridLength(GridLength fallback)
        {
            if (!double.IsFinite(Value) || Value < 0)
                return fallback;
            return Enum.TryParse<GridUnitType>(Unit, true, out var unit) && Enum.IsDefined(unit)
                ? new GridLength(Value, unit)
                : fallback;
        }
    }

    internal sealed class EditorLayoutSettings
    {
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double WindowWidth { get; set; } = 1250;
        public double WindowHeight { get; set; } = 880;
        public string WindowState { get; set; } = nameof(System.Windows.WindowState.Normal);
        public string TimelineLayout { get; set; } = nameof(TimelineLayoutMode.Combined);
        // Missing in older settings: adopt Combined once, then remember user choices.
        public int TimelineLayoutDefaultsVersion { get; set; }

        // Legacy JSON name retained: this column now contains Commands.
        public GridLengthSetting ServoEditorColumn { get; set; } = new() { Value = 1, Unit = nameof(GridUnitType.Star) };
        public GridLengthSetting UrdfEditorColumn { get; set; } = new() { Value = 1, Unit = nameof(GridUnitType.Star) };
        public GridLengthSetting TopEditorRow { get; set; } = new() { Value = 250, Unit = nameof(GridUnitType.Pixel) };
        public GridLengthSetting AudioTimelineRow { get; set; } = new() { Value = 1, Unit = nameof(GridUnitType.Star) };
        public GridLengthSetting LastSplineTimelineHeight { get; set; } = new() { Value = 190, Unit = nameof(GridUnitType.Pixel) };

        // Legacy stepped height is retained only so old EditorLayout.json files
        // continue to deserialize. v1.7.1+ persists a continuous pixel height.
        public int EmbeddedUrdfHeightStage { get; set; }
        public double EmbeddedUrdfHeightPixels { get; set; }

        // Camera values are shared between the embedded and detached URDF
        // views. Distance is the model's wheel-controlled zoom level.
        public double UrdfCameraYaw { get; set; }
        public double UrdfCameraPitch { get; set; }
        public double UrdfCameraDistance { get; set; } = 1.15;

        // Dock/undock state for the URDF preview. ServoEditorColumn and
        // UrdfEditorColumn always store the last *docked* splitter ratio so
        // undocking does not destroy the user's preferred docked layout.
        public bool UrdfUndocked { get; set; }
        public double UrdfWindowLeft { get; set; } = 120;
        public double UrdfWindowTop { get; set; } = 120;
        public double UrdfWindowWidth { get; set; } = 900;
        public double UrdfWindowHeight { get; set; } = 650;
        // Legacy custom full-screen flag is retained for compatibility but ignored.
        public bool UrdfWindowFullScreen { get; set; }
        public string UrdfWindowState { get; set; } = nameof(System.Windows.WindowState.Normal);

        private static string PathFor(string configFolder) =>
            Path.Combine(configFolder, "EditorLayout.json");

        public static EditorLayoutSettings Load(string configFolder)
        {
            if (string.IsNullOrWhiteSpace(configFolder)) return null;
            try
            {
                string path = PathFor(configFolder);
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<EditorLayoutSettings>(File.ReadAllText(path))
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public void Save(string configFolder)
        {
            if (string.IsNullOrWhiteSpace(configFolder)) return;
            Directory.CreateDirectory(configFolder);
            string path = PathFor(configFolder);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(
                    this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static bool IsVisibleOnVirtualDesktop(double left, double top, double width, double height)
        {
            if (!double.IsFinite(left) || !double.IsFinite(top) ||
                !double.IsFinite(width) || !double.IsFinite(height) ||
                width <= 0 || height <= 0)
                return false;

            var window = new Rect(left, top, width, height);
            var virtualDesktop = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            var intersection = Rect.Intersect(window, virtualDesktop);
            return !intersection.IsEmpty && intersection.Width >= 80 && intersection.Height >= 60;
        }
    }
}
