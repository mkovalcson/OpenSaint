using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator;

public partial class MainWindow
{
    private bool _fittingEditorPanels;

    internal static void ConstrainEditorRowHeight(RowDefinition row, double maximum)
    {
        // Star rows already receive the space left by the other rows. A finite
        // maximum equal to that remainder can hit WPF's star-sizing rounding
        // bug (SetFinalSizeMaxDiscrepancy indexes -1 after resolving a max).
        // Keep the drag limit on pixel rows; let star rows absorb the remainder.
        double limit = Math.Max(row.MinHeight, maximum);
        double maxHeight = row.Height.IsAbsolute ? limit : double.PositiveInfinity;
        if (row.MaxHeight != maxHeight) row.MaxHeight = maxHeight;
        if (row.Height.IsAbsolute && row.Height.Value > limit)
            row.Height = new GridLength(limit);
    }

    // Use the parent's viewport, not the grid's desired/row height: an oversized
    // saved pixel row can make those measurements exceed the visible window.
    private double EditorPanelViewportHeight()
    {
        if (MainLayoutRoot == null || MainLayoutRoot.ActualHeight <= 0) return 0;
        double height = MainLayoutRoot.ActualHeight;
        foreach (UIElement child in MainLayoutRoot.Children)
            if (!ReferenceEquals(child, EditorTimelineGrid) && child.Visibility != Visibility.Collapsed &&
                DockPanel.GetDock(child) is Dock.Top or Dock.Bottom)
                height -= child.DesiredSize.Height;
        return Math.Max(0, height);
    }

    private void FitEditorPanels()
    {
        if (_fittingEditorPanels || EditorTimelineGrid == null) return;
        double viewport = EditorPanelViewportHeight();
        if (viewport <= 0) return;
        _fittingEditorPanels = true;
        try
        {
            double headers = EditorTimelineGrid.RowDefinitions[0].ActualHeight + EditorTimelineGrid.RowDefinitions[1].ActualHeight;
            bool separate = SplineArea.Visibility == Visibility.Visible;
            double available = Math.Max(0, viewport - headers - 7 - (separate ? 7 : 0));
            // Keep both timelines usable even at the supported minimum window
            // size, where their full-size minima cannot all fit simultaneously.
            double scale = Math.Min(1, Math.Max(.4, available / (separate ? 280 : 200)));
            TopEditorRow.MinHeight = 80 * scale;
            AudioTimelineRow.MinHeight = 120 * scale;
            SplineTimelineRow.MinHeight = separate ? 80 * scale : 0;
            SplineArea.MinHeight = separate ? 80 * scale : 0;
            double timelineMinimum = AudioTimelineRow.MinHeight + (separate ? SplineTimelineRow.MinHeight : 0);
            double topMax = Math.Max(TopEditorRow.MinHeight, available - timelineMinimum);
            ConstrainEditorRowHeight(TopEditorRow, topMax);

            // Pixel heights left by either splitter must also shrink when the
            // window or monitor gets smaller. Reserve room for both timelines.
            double top = TopEditorRow.Height.IsAbsolute ? TopEditorRow.Height.Value : Math.Min(TopEditorRow.ActualHeight, topMax);
            double audioMax = Math.Max(AudioTimelineRow.MinHeight, available - top - (separate ? SplineTimelineRow.MinHeight : 0));
            ConstrainEditorRowHeight(AudioTimelineRow, audioMax);
            if (separate)
            {
                double audio = AudioTimelineRow.Height.IsAbsolute ? AudioTimelineRow.Height.Value : AudioTimelineRow.MinHeight;
                double splineMax = Math.Max(SplineTimelineRow.MinHeight, available - top - audio);
                ConstrainEditorRowHeight(SplineTimelineRow, splineMax);
            }
            ApplyEmbeddedUrdfHeight();
        }
        finally { _fittingEditorPanels = false; }
    }

    private void ResetEditorLayout_Click(object sender, RoutedEventArgs e) => ResetEditorPanelLayout();

    internal void ResetEditorPanelLayout()
    {
        _startupEditorLayout = null;
        _lastDockedServoColumnWidth = new GridLength(1, GridUnitType.Star);
        _lastDockedUrdfColumnWidth = new GridLength(1, GridUnitType.Star);
        if (!_urdfUndocked)
        {
            CommandsEditorColumn.Width = _lastDockedServoColumnWidth;
            UrdfEditorColumn.Width = _lastDockedUrdfColumnWidth;
        }
        _lastTopEditorHeight = new GridLength(250);
        TopEditorRow.Height = _lastTopEditorHeight;
        _lastSplineTimelineHeight = new GridLength(190);
        _timelineLayout?.RestoreHeights(new GridLength(1, GridUnitType.Star), _lastSplineTimelineHeight);
        TimelineLayoutPicker.SelectedIndex = (int)TimelineLayoutMode.Combined;
        ApplyTimelineLayout();
        _embeddedUrdfHeightPixels = 0;
        FitEditorPanels();
        ApplyEmbeddedUrdfHeight();
    }
}
