using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ServoAnimator
{
    public partial class RgbColorPickerWindow : Window
    {
        private double _hue;
        private double _saturation;
        private double _value;
        private bool _dragSv;
        private bool _dragHue;

        public Color SelectedColor { get; private set; }

        public RgbColorPickerWindow(Color initialColor)
        {
            InitializeComponent();
            RgbToHsv(initialColor, out _hue, out _saturation, out _value);
            UpdateVisuals();
        }

        private void SvArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSv = true;
            Mouse.Capture(SvArea);
            UpdateSvFromPoint(e.GetPosition(SvArea));
        }

        private void SvArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSv && e.LeftButton == MouseButtonState.Pressed)
                UpdateSvFromPoint(e.GetPosition(SvArea));
            else if (_dragSv)
                EndSvDrag();
        }

        private void HueBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragHue = true;
            Mouse.Capture(HueBar);
            UpdateHueFromPoint(e.GetPosition(HueBar));
        }

        private void HueBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragHue && e.LeftButton == MouseButtonState.Pressed)
                UpdateHueFromPoint(e.GetPosition(HueBar));
            else if (_dragHue)
                EndHueDrag();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            EndSvDrag();
            EndHueDrag();
            base.OnMouseLeftButtonUp(e);
        }

        private void EndSvDrag()
        {
            if (!_dragSv) return;
            _dragSv = false;
            if (Mouse.Captured == SvArea) Mouse.Capture(null);
        }

        private void EndHueDrag()
        {
            if (!_dragHue) return;
            _dragHue = false;
            if (Mouse.Captured == HueBar) Mouse.Capture(null);
        }

        private void UpdateSvFromPoint(Point p)
        {
            double w = Math.Max(1.0, SvArea.ActualWidth);
            double h = Math.Max(1.0, SvArea.ActualHeight);
            _saturation = Math.Clamp(p.X / w, 0.0, 1.0);
            _value = 1.0 - Math.Clamp(p.Y / h, 0.0, 1.0);
            UpdateVisuals();
        }

        private void UpdateHueFromPoint(Point p)
        {
            double w = Math.Max(1.0, HueBar.ActualWidth);
            _hue = 360.0 * Math.Clamp(p.X / w, 0.0, 1.0);
            if (_hue >= 360.0) _hue = 0.0;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Color hueColor = HsvToRgb(_hue, 1.0, 1.0);
            SvHueBase.Background = new LinearGradientBrush(
                Colors.White, hueColor, new Point(0, 0.5), new Point(1, 0.5));

            SelectedColor = HsvToRgb(_hue, _saturation, _value);
            ColorPreview.Background = new SolidColorBrush(SelectedColor);
            RgbText.Text = $"R {SelectedColor.R}   G {SelectedColor.G}   B {SelectedColor.B}";
            RedValue.Text = SelectedColor.R.ToString();
            GreenValue.Text = SelectedColor.G.ToString();
            BlueValue.Text = SelectedColor.B.ToString();

            double svW = SvArea.ActualWidth > 1 ? SvArea.ActualWidth : 332.0;
            double svH = SvArea.ActualHeight > 1 ? SvArea.ActualHeight : 180.0;
            SvMarker.Margin = new Thickness(
                Math.Clamp(_saturation * svW - SvMarker.Width / 2.0, -SvMarker.Width / 2.0, svW - SvMarker.Width / 2.0),
                Math.Clamp((1.0 - _value) * svH - SvMarker.Height / 2.0, -SvMarker.Height / 2.0, svH - SvMarker.Height / 2.0),
                0, 0);

            double hueW = HueBar.ActualWidth > 1 ? HueBar.ActualWidth : 332.0;
            HueMarker.Margin = new Thickness(
                Math.Clamp((_hue / 360.0) * hueW - HueMarker.Width / 2.0, -HueMarker.Width / 2.0, hueW - HueMarker.Width / 2.0),
                0, 0, 0);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            UpdateVisuals();
        }

        private void ApplyRgb_Click(object sender, RoutedEventArgs e)
        {
            // SelectedColor contains the exact 0-255 R/G/B values shown above.
            // RgbBuilderWindow copies these three channels into its Red, Green
            // and Blue argument fields when this dialog returns true.
            DialogResult = true;
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360.0) + 360.0) % 360.0;
            s = Math.Clamp(s, 0.0, 1.0);
            v = Math.Clamp(v, 0.0, 1.0);
            double c = v * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = v - c;
            (double r, double g, double b) = h switch
            {
                < 60.0 => (c, x, 0.0),
                < 120.0 => (x, c, 0.0),
                < 180.0 => (0.0, c, x),
                < 240.0 => (0.0, x, c),
                < 300.0 => (x, 0.0, c),
                _ => (c, 0.0, x),
            };
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255.0),
                (byte)Math.Round((g + m) * 255.0),
                (byte)Math.Round((b + m) * 255.0));
        }

        private static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;

            if (d <= 1e-9) h = 0.0;
            else if (max == r) h = 60.0 * (((g - b) / d) % 6.0);
            else if (max == g) h = 60.0 * (((b - r) / d) + 2.0);
            else h = 60.0 * (((r - g) / d) + 4.0);
            if (h < 0.0) h += 360.0;

            s = max <= 1e-9 ? 0.0 : d / max;
            v = max;
        }
    }
}
