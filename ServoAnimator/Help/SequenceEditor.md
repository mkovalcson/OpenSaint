# Sequence Editor and Audio Timeline

The Sequence header shows the filename without its `.json` extension, an unsaved `*` directly after the name when modified, and the complete Sequence duration. The description below it is read-only; click the small bold **edit** link immediately after the text to change it in a popup editor. The same link is available beside Sequence descriptions inside Movie blocks.

A Sequence is an animation document containing timed servo, RGB and audio-play commands plus sequence metadata.

## Timeline navigation

Use the picklist immediately left of **?** to choose **Waveform top** (the original arrangement), **Spline top**, or **Combined**. The choice is remembered with your editor layout. In separate layouts the divider still resizes the two views; the Spline pane hides when no servos have Spline enabled.

**Combined** overlays spline curves and value-grid lines on the waveform in one shared timeline. The Spline checkbox legend sits above the command triangles, and all triangles sit above the +100 grid line. Spline points remain editable; blank waveform space retains its normal cursor, marker-selection, zoom and context-menu behavior. The **show all** button follows the final legend item and wraps with the legend when needed.

Playback cursors are lightweight overlays updated on every display frame. Servo/model preview work remains paced near 60 updates per second, and command/audio timing still uses the playback clock.

- Left-click positions the cursor.
- Double-left-click a command triangle to open Edit Commands for its time group.
- Mouse wheel and Zoom +/- expand or contract around the Sequence cursor, keeping it at the same screen position while timeline bounds allow. The mouse pointer's position does not change the zoom anchor.
- Middle-drag pans the visible time range.
- Right-click opens timeline actions.
- Drag a command triangle horizontally to move its complete command group.

Command triangles that overlap are vertically staggered for visibility.

## Selecting and editing command groups

Ctrl-left-click a triangle to select or deselect all commands at that timestamp. Shift-left-click selects every triangle between the selection anchor and the triangle nearest the click, including both endpoints. Selected triangles have a cyan outline.

Drag any selected triangle to move the entire selection together, preserving its internal timing. The group stays selected when you click elsewhere in the waveform; a mouse-button click outside the waveform clears it. Double-click remains available to edit the commands at one triangle.

Right-click while a group is selected for these actions:

- **Uniform offset** shows the mean spacing between selected triangles. Apply the displayed value, enter a nonnegative decimal number of seconds, or leave the field blank to use the mean. The first selected time remains fixed and the remaining times stay in order, evenly spaced.
- **Repeat** asks for the number of additional copies and an offset, defaulting to zero. The first copy starts at the original group's last command plus the offset. Each later copy starts at the preceding copy's last command plus the offset; internal timing is preserved.
- **Copy** remembers the selected commands and their relative timing. Left-click the desired start position in the waveform, then right-click and choose **Paste**. Paste is available in both the selected-group menu and the normal cursor menu.
- **Delete** removes every command at the selected timestamps.
- **Show all modified controls** temporarily replaces Commands at Cursor with a distinct list of controls used by the selected triangles. Select a list entry to highlight every triangle containing that control in purple, including matches outside the selection. A ganged command includes its individual children. Selecting entries in this list keeps the triangle selection active; clicking elsewhere outside the waveform ends the selection and restores the normal Commands list.

Group moves, Uniform offset, Repeat, Paste and Delete can each be undone as one operation. Times use millisecond precision. If a move, zero spacing, repetition or paste creates redundant commands, choose which values to keep in the existing warning; Cancel restores the state before that operation. To return to the normal cursor menu, clear the selection by clicking outside the waveform.

The waveform and Spline share a permanent left inset. At the start of the view, time zero and its command triangle sit just to the right of the green audio grab handle. The inset remains even when the handle is outside the visible area. Zoom and Fit buttons are to the left of the Sequence playback buttons.

Fit still shows the complete Sequence. Zooming out against the start/end of the timeline may move the cursor on screen because scrolling remains within timeline bounds.

## Audio

The primary audio waveform is shown on the timeline. Additional audio clips are represented by `Play` commands and are included in playback. Playback follows the timeline clock and switches audio sources as clip boundaries are crossed.

The Playback Volume control changes audible playback volume. URDF mouth-light response uses the audio amplitude plus the Audio LED Gain configured in URDF Configuration.

## Command markers

A triangle represents the commands at a timeline point. During collision-diagnostic playback a triangle can become bright red when the calibrated URDF pose produced at that point causes one of the configured flap collisions.

Editing commands clears old red collision-marker state. Replay the modified sequence to calculate fresh collision warnings.

## Command and pose drafting

Use Edit Commands to stage servo values and Spline settings, then Apply and Close to commit them. Use the URDF pose editor and Insert Pose to capture a complete pose.


## Insert Pose

When the URDF Pose editor is used to arrange the robot visually, right-click the sequence timeline and choose **Insert Pose**. The editor creates a complete mechanical keyframe at the current cursor from the URDF pose. If existing commands target the same servo/control at that time, a warning shows the earlier and new values so you can choose which to keep. An RGB Command from the pose is compared the same way; unrelated RGB/Play commands remain.
