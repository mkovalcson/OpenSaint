# Spline Editor

The Spline Editor creates smooth interpolated servo motion between control points. The application uses cubic Hermite interpolation for spline-enabled servo motion.

## Mouse controls

The legend uses compact checkbox chips with servo-color dots. Hidden curves have subdued
chips; **show all** remains directly after the final chip. The zero-value line is stronger
than other reference lines. Combined mode softens the waveform behind the splines.

- Left-drag a control point vertically to change its value.
- Right-drag a point horizontally to change its time.
- Right-click the vertical sequence cursor to open the same cursor actions as the waveform.
- Ctrl+left-click **or double-left-click** a spline line to add a control point.
- Select a point and press Delete to remove it.
- Double-left-click a selected control point to open Edit Commands at that point's time. Double-clicking an empty part of a curve still adds a new point.
- Hold the middle mouse button and drag anywhere in the graph, including over a control point, to pan the timeline. To change a shared neck point between `NeckNodUp` and `NeckTiltRight`, double-click the selected point and choose its Servo in Edit Commands.
- Hover over a point to see the exact servo, time and value just to the right of the mouse pointer. The readout follows the pointer while editing points.
- Selecting a control point also moves the sequence cursor to that point and refreshes the Commands list, showing every command at that exact cursor time.

The spline view shares timeline zoom and pan with the waveform so the same time range remains aligned vertically.

Mouse-wheel zoom in the Spline graph expands around the Sequence cursor, not the mouse pointer. The cursor stays in the same screen position unless a timeline boundary limits scrolling.

The colored show/hide checkbox legend sits immediately above the graph, leaving the curves unobstructed.

If any legend checkbox is unchecked, a small **show all** button appears beside the legend. Click it to check every legend checkbox and restore all enabled Spline lines. It changes graph visibility, not which servos have interpolation enabled.

Enable or disable a servo's spline using the checkbox to the left of its Servo picklist in Edit Commands. This changes interpolation for that servo throughout the sequence; the legend only changes graph visibility.

Horizontal reference lines mark -100, -75, -50, -25, 0, +25, +50, +75 and +100 for typical centered servos. Other servo ranges remain normalized to the graph height; hover a point for its exact value. At small graph heights, some labels are omitted to avoid overlap, but every grid line remains.

## Shared neck spline

`NeckNodUp` and `NeckTiltRight` drive the same physical neck servo pair and therefore share one spline. Their ganged timeline commands are merged into one ordered set of control points. The most recent neck command owns the physical pair until a command of the other neck type takes over. The shared spline line changes color at those ownership transitions. A newly added point inherits the owner of the preceding point so adding a point does not create an unexpected Nod/Tilt hand-off. The URDF uses the same ownership rule on one shared `NeckTiltLeft` / `NeckTiltRight` child-actuator pair; switching between Nod and Tilt changes how that same pair is interpreted, not which child controls are driven.

## Playback

During playback the spline is evaluated at the current timeline time and the resulting values are used for the URDF preview and enabled hardware output. High-frequency spline-generated movement does not repeatedly send Maestro Speed/Acceleration because those generated position updates use N/C speed behavior.
