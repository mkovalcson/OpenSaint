using System.Windows;
using System.Windows.Controls;
using ServoAnimator;

internal static partial class Program
{
    private static void SplitterRoundingChecks(bool baseline)
    {
        var random = new Random(918);
        for (int i = 0; i < 10000; i++)
        {
            double height = 400 + random.NextDouble() * 700;
            double top = 80 + random.NextDouble() * (height - 220);
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(top) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 120, MaxHeight = Math.BitDecrement(height - top) });
            if (!baseline)
                MainWindow.ConstrainEditorRowHeight(grid.RowDefinitions[1], grid.RowDefinitions[1].MaxHeight);
            try { grid.Measure(new Size(800, height)); grid.Arrange(new Rect(0, 0, 800, height)); }
            catch { Console.WriteLine($"Reproduction: height={height:R}, top={top:R}, max={grid.RowDefinitions[1].MaxHeight:R}"); throw; }
        }
        Check(true, "10,000 fractional row constraints arrange without crashing");
        var row = new RowDefinition { Height = new GridLength(900), MinHeight = 80 };
        MainWindow.ConstrainEditorRowHeight(row, 250);
        Check(row.Height.Value == 250 && row.MaxHeight == 250, "Pixel rows retain drag and viewport limits");
        row.Height = new GridLength(1, GridUnitType.Star);
        MainWindow.ConstrainEditorRowHeight(row, 250);
        Check(double.IsPositiveInfinity(row.MaxHeight), "Restoring star sizing clears a previous pixel limit");
    }
}
