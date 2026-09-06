# Spline Editor

The Spline Editor creates smooth interpolated servo motion between control points. The application uses cubic Hermite interpolation for spline-enabled servo motion.

## Mouse controls

- Left-drag a control point vertically to change its value.
- Right-drag a point horizontally to change its time.
- Right-click the vertical sequence cursor to open the same cursor actions as the waveform.
- Ctrl+left-click **or double-left-click** a spline line to add a control point.
- Select a point and press Delete to remove it.
- For the shared `NeckNodUp` / `NeckTiltRight` spline, middle-click an already-selected control point to toggle which neck control owns that point.
- Hover over a point to see the exact servo, time and value.
- Selecting a control point also moves the sequence cursor to that point and refreshes the Commands list, showing every command at that exact cursor time.

The spline view shares timeline zoom and pan with the waveform so the same time range remains aligned vertically.

## Shared neck spline

`NeckNodUp` and `NeckTiltRight` drive the same physical neck servo pair and therefore share one spline. Their ganged timeline commands are merged into one ordered set of control points. The most recent neck command owns the physical pair until a command of the other neck type takes over. The shared spline line changes color at those ownership transitions. A newly added point inherits the owner of the preceding point so adding a point does not create an unexpected Nod/Tilt hand-off. The URDF uses the same ownership rule on one shared `NeckTiltLeft` / `NeckTiltRight` child-actuator pair; switching between Nod and Tilt changes how that same pair is interpreted, not which child controls are driven.

## Playback

During playback the spline is evaluated at the current timeline time and the resulting values are pushed to the Grid and URDF preview. High-frequency spline-generated movement does not repeatedly send Maestro Speed/Acceleration because those generated position updates use N/C speed behavior.
