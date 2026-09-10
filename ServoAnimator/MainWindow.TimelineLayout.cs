using System.Windows.Controls;

namespace ServoAnimator
{
    public partial class MainWindow
    {
        private TimelineLayoutController _timelineLayout;
        private void InitializeTimelineLayout()
        {
            _timelineLayout = new TimelineLayoutController(EditorTimelineGrid, AudioTimelineArea, AudioPlotHost,
                SplinePlotHost, SplineAreaGrid, SplineLegendStrip, SplineArea, Spline, AudioTimelineRow, SplineTimelineRow, firstTimelineRow: 4);
            ApplyTimelineLayout();
        }
        private void TimelineLayout_Changed(object sender, SelectionChangedEventArgs e) => ApplyTimelineLayout();
        private void ApplyTimelineLayout()
        {
            if (_timelineLayout == null) return;
            var mode = (TimelineLayoutMode)Math.Clamp(TimelineLayoutPicker.SelectedIndex, 0, 2);
            _timelineLayout.LastSplineHeight = _lastSplineTimelineHeight;
            _timelineLayout.Apply(mode, SplineServosEnabled().Count > 0);
            _lastSplineTimelineHeight = _timelineLayout.LastSplineHeight;
            SyncSplineView();
        }
    }
}
