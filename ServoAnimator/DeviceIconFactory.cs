using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ServoAnimator;

/// <summary>Compact, code-native device icons used beside settings labels.</summary>
internal static class DeviceIconFactory
{
    public static FrameworkElement Xbox(double width = 34, double height = 24)
    {
        var canvas = Canvas(48, 32);
        canvas.Children.Add(new Path { Fill = Brush("#48586D"), Stroke = Brush("#C2CFDF"), StrokeThickness = 1.5,
            Data = Geometry.Parse("M 11,3 Q 5,4 3,15 L 1,25 Q 1,33 8,29 L 16,22 L 32,22 L 40,29 Q 47,33 47,25 L 45,15 Q 43,4 37,3 Z") });
        AddEllipse(canvas, 10, 7, 8, 8, "#B7CADF");
        AddEllipse(canvas, 26, 17, 7, 7, "#B7CADF");
        canvas.Children.Add(new Path { Stroke = Brush("#EAF0F7"), StrokeThickness = 2,
            Data = Geometry.Parse("M 19,17 L 19,25 M 15,21 L 23,21") });
        AddEllipse(canvas, 35, 8, 3, 3, "#E9D365");
        AddEllipse(canvas, 39, 11, 3, 3, "#DA7777");
        AddEllipse(canvas, 31, 11, 3, 3, "#78A9E0");
        AddEllipse(canvas, 35, 15, 3, 3, "#93D895");
        return View(canvas, width, height);
    }

    public static FrameworkElement Steam(double width = 34, double height = 24)
    {
        var canvas = Canvas(48, 32);
        canvas.Children.Add(new Path { Fill = Brush("#334653"), Stroke = Brush("#B8D5DC"), StrokeThickness = 1.5,
            Data = Geometry.Parse("M 10,2 Q 4,2 3,12 L 1,25 Q 1,33 9,30 L 16,26 L 32,26 L 39,30 Q 47,33 47,25 L 45,12 Q 44,2 38,2 L 30,4 L 18,4 Z") });
        AddEllipse(canvas, 8, 6, 7, 7, "#AACCD3");
        AddEllipse(canvas, 33, 6, 7, 7, "#AACCD3");
        AddRect(canvas, 13, 18, 10, 8, "#617E89", "#D4EDF0", 2);
        AddRect(canvas, 26, 18, 10, 8, "#617E89", "#D4EDF0", 2);
        return View(canvas, width, height);
    }

    public static FrameworkElement StreamDeck(double width = 34, double height = 24)
    {
        var canvas = Canvas(58, 34);
        AddRect(canvas, 1, 1, 56, 32, "#263341", "#C2CFDF", 4, 1.5);
        for (int row = 0; row < 4; row++)
        for (int column = 0; column < 8; column++)
            AddRect(canvas, 6 + column * 6, 6 + row * 6, 4, 4, "#B7CADF", null, 0);
        return View(canvas, width, height);
    }

    public static FrameworkElement Keyboard(double width = 34, double height = 22)
    {
        var canvas = Canvas(58, 32);
        AddRect(canvas, 1, 3, 56, 27, "#263341", "#C2CFDF", 3, 1.5);
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 9; column++)
            AddRect(canvas, 5 + column * 5.4, 7 + row * 6, 4, 4, row == 2 && column is >= 3 and <= 5 ? "#70C5CE" : "#B7CADF", null, .7);
        return View(canvas, width, height);
    }

    private static Canvas Canvas(double width, double height) => new() { Width = width, Height = height };

    private static Viewbox View(Canvas canvas, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Child = canvas,
        Stretch = Stretch.Uniform,
        IsHitTestVisible = false
    };

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));

    private static void AddEllipse(Canvas canvas, double left, double top, double width, double height, string fill)
    {
        var shape = new Ellipse { Width = width, Height = height, Fill = Brush(fill) };
        System.Windows.Controls.Canvas.SetLeft(shape, left);
        System.Windows.Controls.Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
    }

    private static void AddRect(Canvas canvas, double left, double top, double width, double height,
        string fill, string stroke, double radius, double strokeThickness = 1)
    {
        var shape = new Rectangle { Width = width, Height = height, Fill = Brush(fill),
            RadiusX = radius, RadiusY = radius, StrokeThickness = strokeThickness };
        if (stroke != null) shape.Stroke = Brush(stroke);
        System.Windows.Controls.Canvas.SetLeft(shape, left);
        System.Windows.Controls.Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
    }
}
