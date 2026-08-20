# Spline Editor

The Spline Editor creates smooth interpolated servo motion between control points. The application uses cubic Hermite interpolation for spline-enabled servo motion.

## Mouse controls

- Left-drag a control point vertically to change its value.
- Right-drag a point horizontally to change its time.
- Ctrl+left-click a spline line to add a control point.
- Select a point and press Delete to remove it.
- Hover over a point to see the exact servo, time and value.

The spline view shares timeline zoom and pan with the waveform so the same time range remains aligned vertically.

## Playback

During playback the spline is evaluated at the current timeline time and the resulting values are pushed to the Grid and URDF preview. High-frequency spline-generated movement does not repeatedly send Maestro Speed/Acceleration because those generated position updates use N/C speed behavior.
