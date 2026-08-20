# URDF 3D Viewer

The URDF viewer renders the calibrated Johnny 5 head and neck and follows the logical values produced by the Grid, timeline and playback.

## Bottom-left controls

The controls are stacked vertically:

- Collision Warning: On/Off
- Drive: On/Off
- UnDock/Dock
- ← / Recenter / → camera row
- -90° / +90° camera turns

Drive controls only visual URDF updates. Turning URDF Drive off freezes the model pose to reduce rendering work; it does not disable physical Live Drive.

## Docking

UnDock opens the URDF in a separate window and removes its pane from the editor. The Servo Grid expands into two columns. The detached window uses the standard Windows maximize/restore controls.

If the detached window is not visible, use the Dock button under Headtop Controls in the expanded Servo Grid.

## Docked height

A small handle centered at the bottom of the docked URDF view can be dragged vertically. The URDF can extend downward through the audio/spline region while the Commands row remains uncovered. The selected layout is saved in `EditorLayout.json`.

## Camera

Use normal mouse controls to orbit and zoom. **Recenter** returns yaw and pitch to straight-on while preserving zoom. The **←** and **→** buttons immediately to either side rotate camera yaw by exactly 90 degrees. The default framing places the neck base close to the bottom controls. When the model is undocked, the current docked camera view is carried into the detached window; docking carries the current view back.

The URDF camera and its on-screen controls remain operational while the modeless **Edit Commands** window is open.
