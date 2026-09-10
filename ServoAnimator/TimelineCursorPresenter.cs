using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ServoAnimator
{
    /// <summary>Retained cursor: moving its transform never repaints the timeline surface.</summary>
    internal sealed class TimelineCursorPresenter
    {
        private readonly FrameworkElement _target;
        private readonly Func<double> _position;
        private readonly Func<bool> _visible;
        private readonly double _bottomInset;
        private readonly Color _color;
        private AdornerLayer _layer;
        private CursorAdorner _adorner;
        public bool IsAttached => _adorner != null;

        public TimelineCursorPresenter(FrameworkElement target, Func<double> position, Color color,
            double bottomInset = 0, Func<bool> visible = null)
        {
            _target = target; _position = position; _color = color; _bottomInset = bottomInset;
            _visible = visible ?? (() => true);
            target.Loaded += (_, _) => Attach();
            target.Unloaded += (_, _) =>
            {
                if (_adorner != null) _layer?.Remove(_adorner);
                _adorner = null; _layer = null;
            };
            target.SizeChanged += (_, _) => Update();
        }
        private void Attach()
        {
            if (_adorner != null) return;
            _layer = AdornerLayer.GetAdornerLayer(_target);
            if (_layer == null) return;
            _adorner = new CursorAdorner(_target, _color, _bottomInset);
            _layer.Add(_adorner);
            Update();
            _target.InvalidateVisual(); // Remove the fallback cursor painted before Loaded.
        }
        public bool Update()
        {
            if (_adorner == null) return false;
            _adorner.SetPosition(_position(), _visible());
            return true;
        }
        internal sealed class CursorAdorner : Adorner
        {
            private readonly Rectangle _line;
            private readonly TranslateTransform _offset = new();
            private readonly double _bottomInset;
            public double CursorX => _offset.X + 1;
            public CursorAdorner(FrameworkElement target, Color color, double bottomInset) : base(target)
            {
                IsHitTestVisible = false; ClipToBounds = true; _bottomInset = bottomInset;
                var brush = new SolidColorBrush(color); brush.Freeze();
                _line = new Rectangle { Fill = brush, Width = 2, RenderTransform = _offset, IsHitTestVisible = false };
                AddVisualChild(_line);
            }
            public void SetPosition(double x, bool visible)
            {
                _offset.X = double.IsFinite(x) ? x - 1 : -10;
                Visibility = visible && double.IsFinite(x) && x >= -2 && x <= AdornedElement.RenderSize.Width + 2
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => index == 0 ? _line : throw new ArgumentOutOfRangeException(nameof(index));
            protected override Size MeasureOverride(Size constraint)
            {
                var size = AdornedElement.RenderSize;
                _line.Measure(new Size(2, Math.Max(0, size.Height - _bottomInset)));
                return size;
            }
            protected override Size ArrangeOverride(Size finalSize)
            {
                _line.Arrange(new Rect(0, 0, 2, Math.Max(0, finalSize.Height - _bottomInset)));
                return finalSize;
            }
        }
    }
}
