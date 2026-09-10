using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ServoAnimator
{
    /// <summary>Retained progress tint: playback only changes a transform, not the Movie cards.</summary>
    internal sealed class MovieProgressPresenter
    {
        private readonly FrameworkElement _target;
        private readonly Func<Rect> _bounds;
        private AdornerLayer _layer;
        private ProgressAdorner _adorner;
        public MovieProgressPresenter(FrameworkElement target, Func<Rect> bounds)
        {
            _target = target; _bounds = bounds;
            target.Loaded += (_, _) =>
            {
                if (_adorner != null) return;
                _layer = AdornerLayer.GetAdornerLayer(target);
                if (_layer == null) return;
                _adorner = new ProgressAdorner(target); _layer.Add(_adorner); Update();
            };
            target.Unloaded += (_, _) => { if (_adorner != null) _layer?.Remove(_adorner); _adorner = null; _layer = null; };
            target.SizeChanged += (_, _) => Update();
        }
        public void Update() => _adorner?.SetBounds(_bounds());
        private sealed class ProgressAdorner : Adorner
        {
            private readonly Rectangle _tint;
            private readonly MatrixTransform _transform = new();
            private readonly SolidColorBrush _brush = new();
            public ProgressAdorner(FrameworkElement target) : base(target)
            {
                IsHitTestVisible = false; ClipToBounds = true;
                _tint = new Rectangle { Fill = _brush, RenderTransform = _transform, IsHitTestVisible = false };
                AddVisualChild(_tint);
            }
            public void SetBounds(Rect bounds)
            {
                Visibility = bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0 ? Visibility.Collapsed : Visibility.Visible;
                if (Visibility != Visibility.Visible) return;
                var color = ThemeManager.GetColor("MovieAccent", Colors.Goldenrod);
                _brush.Color = Color.FromArgb(24, color.R, color.G, color.B);
                _transform.Matrix = new Matrix(bounds.Width, 0, 0, bounds.Height, bounds.X, bounds.Y);
            }
            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => index == 0 ? _tint : throw new ArgumentOutOfRangeException(nameof(index));
            protected override Size MeasureOverride(Size constraint) { _tint.Measure(new Size(1, 1)); return AdornedElement.RenderSize; }
            protected override Size ArrangeOverride(Size size) { _tint.Arrange(new Rect(0, 0, 1, 1)); return size; }
        }
    }
}
