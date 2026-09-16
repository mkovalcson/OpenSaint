# URDF 3D Viewer

The URDF viewer renders the calibrated Johnny 5 head and neck and follows the logical values produced by command editing, the timeline and playback.

## Preview controls

The studio-gradient viewport keeps the familiar lower-left vertical control stack,
with camera controls in a separate row beneath it and the legend at lower right.
Pose and Face Reset sit near the bottom-center handle. Library Pose actions appear
above the preview controls while Pose is enabled:

- Collision Warning: On/Off
- Drive: On/Off
- UnDock/Dock
- ← / Recenter / → camera row
- -90° / +90° camera turns

Recenter stretches so the right camera arrow lines up with the right edge of UnDock.
The NeckTurn circle is offset 50 pixels left from its previous position near the legend.

Drive controls only visual URDF updates. Turning URDF Drive off freezes the model pose to reduce rendering work; it does not disable physical Live Drive.

## Docking

UnDock opens the URDF in a separate window and removes its pane from the editor. Commands expands across the freed width. The detached window uses the standard Windows maximize/restore controls.

If the detached window is not visible, use Dock URDF in the Commands header.

## Docked height

A small handle centered at the bottom of the docked URDF view can be dragged vertically. The URDF can extend downward through the audio/spline region while the Commands row remains uncovered. The selected layout is saved in `EditorLayout.json`.

## Camera

Use normal mouse controls to orbit and zoom. **Recenter** returns yaw and pitch to straight-on while preserving zoom. The **←** and **→** buttons immediately to either side rotate camera yaw by exactly 90 degrees. The neck base is screen-anchored 35 pixels above the bottom of the URDF viewport while zooming or resizing. When the model is undocked, the current docked camera view is carried into the detached window; docking carries the current view back. Camera orientation and zoom are saved in `EditorLayout.json` and restored at the next launch.

The URDF camera and its on-screen controls remain operational while the modeless **Edit Commands** window is open.


## Pose editor

The **Pose** button is positioned between the camera-right control and the bottom-center resize handle. Pose mode is a non-destructive URDF drafting mode: timeline refreshes do not overwrite the draft while Pose is active. Leaving Pose mode without inserting it restores the current timeline pose.

- The eye bullseye is constrained to the eye-tube target circle. Center is centered eyes; an axis at the circle edge is 100% of that eye travel. Diagonal positions mix horizontal and vertical movement. Horizontal bullseye motion uses the visually reversed command-axis mapping required by the eye mechanism.
- In **LR Joined**, the gaze bullseye is on the robot's right eye and the shared iris slider sits immediately below the robot's left eye tube. **LR Split** exposes both eye targets and places an iris slider immediately below each eye tube.
- Flap Open/Close, Nose Body, and Nose Basket are vertical sliders centered on the overall robot-head frame rather than the moving NoseBody. Flap Open/Close is centered, Nose Body is on the robot-right side, and the half-height Nose Basket slider is on the robot-left side. In **LR Split**, Nose Body/Basket stay in those same locations while the left/right Flap Open/Close sliders appear side-by-side at the center. **T** handles still control upper-flap tilt. The Vent control is a 90° slider arc following the CAD outer edge of the robot-left eye tube from the top down to the side. In **LR Split**, a mirrored arc appears on the opposite eye and the two vents are controlled independently.
- Eye Pop is anchored 15 mm outside the physical head sides. The bottom of each vertical Eye Pop slider is aligned with the projected top of the mouth, so that relationship follows the model through head/camera motion. Joined mode uses one outside slider; Split mode exposes independent left/right sliders.
- NeckTurn uses a 50%-larger editable circular dial positioned 75% of the way from the bottom-center resize-handle reference toward the lower-right legend. Its zero pointer is straight down and acts as a top-down heading indicator for the front of the head. A **↺** reset is centered inside the dial. NeckNod remains anchored at the physical front-middle of the Fabco neck assembly, while NeckTilt remains anchored to the physical robot-left Fabco cylinder midpoint; moving Nod or Tilt transfers ownership of the shared neck actuators to that mode.
- Whip antenna height and rotation controls are anchored at the top-left edge of the URDF viewport; MFRC height and rotation controls are anchored at the top-right. Both groups keep their 8-pixel top inset even in narrow panes. Microphone Up/Down sits just left of the RGB Command box. Antenna height-reset buttons sit beneath their sliders.
- Eye Pop sliders are offset 50 pixels left from their projected side positions, in both Joined and Split modes. Their reset buttons follow the shifted sliders.
- Straight pose sliders use pale rounded tracks and blue, white-bordered handles without numeric labels. Their existing ranges, dragging, keyboard controls and reset actions are unchanged.
- Every Pose slider has a small **↺** reset button positioned toward the outside of the robot relative to that control; each visible Vent arc has one as well. The NeckTurn, Whip rotation, and MFRC rotation dials have reset buttons centered inside their circles. Resetting NeckNod or NeckTilt also makes that mode the active shared-neck owner.
- While Pose mode is on, **Face Reset** appears immediately to the right of the Pose button. It neutralizes eye gaze, irises, flap open/close and tilt, vents, Nose Body, Nose Basket, and Eye Pop without changing NeckTurn, NeckNod, or NeckTilt. Face Reset also clears the Pose RGB Command and applies the same lighting-off result as `ClearAll` to the URDF preview and Live Drive hardware.
- **LR Joined** is the default. **LR Split** exposes separate eye targets/iris sliders, eye-pop sliders, mirrored left/right Vent arcs, and left/right flap controls so asymmetric poses can be created.
- The taller **RGB Command** text field and **Build** button above LR Joined/Split use the normal Build RGB Command window. The result previews immediately on the URDF and follows Live Drive for hardware output.
- Pose controls use the same normalized authoring limits and calibrated URDF endpoints as the rest of the application; their handles cannot drive the URDF past its configured range.
- Right-click the sequence timeline and choose **Insert Pose** to commit the complete visible URDF pose at the cursor. Split values are written as individual child-servo commands when required, and a Pose RGB command is inserted at the same timestamp when one has been added.

- While Pose mode is active, **Library +** and **Library Load** appear above Collision Warning. **Library +** saves the complete current URDF pose (including an optional Pose RGB command) as a reusable Library Pose. Unless you attach a custom image, it also automatically captures a clean centered URDF PNG with Pose controls hidden and crops it around the head, flaps, and neck. **Library Load** selects a saved Library Pose and applies it directly to the URDF Pose editor without inserting anything on the timeline.
- Each visible eye-gimbal target circle has a small **↺** reset just outside the circle on its inner-facing edge. Joined mode resets both eye axes together; LR Split provides an independent reset for each eye.
- Vent quarter-arcs are projected from the actual 44.45 mm-radius CAD outer eye-tube edge, so the arc and handle remain attached to the tube under camera orbit and head motion.
## Rendering performance

The viewer skips the hidden back sides of closed, opaque CAD parts. Exterior
surfaces and moving parts retain their original detail. Open and transparent
surfaces keep both sides, and collision checks continue to use the original meshes.
The optimization is automatic; changed model files fall back to full-detail rendering.

Timeline playback and controller-driven previews target **30 URDF pose updates per second**. Audio, timeline
cursor updates, controller polling, and hardware command dispatch retain their
independent timing. Delayed preview frames skip ahead to the current playback
position rather than replaying a backlog. Command-time collision checks are retained.
Controller visuals coalesce the latest values between frames, including Library
poses/sequences and config testing. Controller polling and hardware dispatch are
not throttled by the visual update queue.

The `fps:` indicator above the legend counts rendered frames with changed URDF
poses or lighting, rather than unrelated WPF redraws. It shows a whole-number
rate over the last second and refreshes every quarter second. A stationary model
can read zero; this measures model animation updates, not the monitor refresh rate.
If rendering stalls, the next refresh includes that delay.
Actual speed depends on the graphics hardware,
window size, and other editor activity.
