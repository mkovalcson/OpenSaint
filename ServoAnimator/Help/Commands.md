# Commands and Edit Commands

Commands are the authored changes that occur at a specific timeline offset.

The Commands list remains visible beside the docked URDF, replacing the old Grid controls. Add, Edit and Delete are immediately before the cursor heading. Drag the horizontal bar below this pane to resize its height, or the vertical divider to adjust its width beside the URDF. There is no Commands visibility toggle.

## Edit Commands

Double-click a command in Commands at Cursor, a waveform command triangle, or a selected Spline control point to edit the commands at that time point. A command can contain:

- Offset
- Servo or individual child control
- Spline checkbox (to the left of the Servo picklist)
- Break Spline checkbox (available when Spline is checked for a numeric group command)
- Value
- Disable state
- Speed

The Reason field is no longer shown in this editor. Existing Reason metadata remains preserved in saved files.

Changes are staged while the editor is open. **Apply and Close** commits them;
**Cancel** discards the drafts. Saving or closing the application also asks the
editor to apply its pending changes. Servo and URDF previews still respond while editing.

Spline is a sequence-wide setting, not a property of one command. All rows for the same servo show the same state. Neck Nod and Neck Tilt share one spline setting. Spline changes are saved with the sequence and participate in Undo/Redo. The checkbox is disabled for RGB/audio and individual child-only targets, which the current spline engine does not interpolate.

**Break Spline** belongs to the individual command. It hides the outgoing line to
the next point on that control's spline; both command dots remain visible and
editable. Later segments display normally unless their starting command also has
Break Spline checked. The shared Neck Nod/Tilt spline follows its next shared
point. A break on the final point takes effect if another point is added after it.
This is a display-only break: playback and exported interpolation remain unchanged.
The flag is saved with the command and retained by copy/paste, Library operations,
and Undo/Redo. Uncheck it to restore the line.

The Command List also has **Spline** and **Break Spline** checkboxes immediately
after each control name. Spline affects the whole control's sequence; Break
Spline affects just that command. Right-click a spline control point without
dragging to toggle **Break Spline**. Right-drag still moves the point in time.

For selected command triangles, choose **Show all modified controls**. Splined
controls offer **Set Breaks**, plus **Clear Breaks** when any matching selected
command has a break. These buttons affect only commands of that exact control
type at the selected times. Each bulk change is one undoable edit.

**Insert Pose** asks **Break Preceding Splines?**. Yes marks the last enabled
point strictly before insertion on every active spline, even if the new pose
does not contain that control. Disabled and individual child-only commands are
skipped; Neck Nod/Tilt share one preceding point. No leaves existing breaks
unchanged. The pose and preceding breaks undo together.

## Redundant commands

Only one command for the same **Servo + individual Control** may be kept at a
given time point (rounded to the nearest millisecond). Different servos and
different child controls may share a time point; a ganged command and an
individual child override are distinct targets.

If an insertion, paste, pose, edit, time move, or loaded sequence contains
duplicates, a warning lists each conflicting group. Candidates appear in the
order they were added, earliest first and most recent last, with their values,
speed, Disable state, color and Reason. Identical values are also duplicates.

Select one command per group and click **Keep selected**. The other commands in
those groups are removed; unrelated commands are retained. **Cancel** cancels
the pending insertion/move/load, or returns to the command editor without
applying its drafts. Undo restores the state before an insertion or edit,
including the commands replaced by your selections.

Resolving conflicts in an existing sequence does not overwrite its file;
save the sequence to retain your choices. Export checks its generated command
list too, including conflicts introduced by expanding ganged commands.

## Command Speed

Speed defaults to **N/C** meaning No Change. An N/C position command does not send Maestro Speed or Acceleration; it only sends the target position and leaves the channel's active speed/acceleration profile unchanged.

An explicit Default, Slow, Fast or Crawl selection sends the matching configured Speed and Acceleration before the target. For ganged commands the profile is sent to every physical child servo in the gang.

At the top of **Edit Commands**, choose **Speed for all commands** and click **Set All Speeds** to update every speed-capable command currently in that window. Choices are N/C, Default, Fast, Slow and Crawl. Individual speeds can still be adjusted afterward. Apply and Close saves the draft changes; Cancel discards them. Like individual speed edits, explicit profiles can preview immediately when Live Drive is enabled. RGB, audio and Tic eye-pop commands are skipped.

Use Insert Pose from the timeline menu to capture a pose arranged in the URDF pose editor.

## Individual child commands

A command may target a child control inside a gang. It then affects only that child until a later ganged command supplies a new gang value.

## RGB commands

RGBCommand uses text rather than a numeric servo value. The RGB Builder can create supported Arduino command strings such as `ClearAll`, `SetRGBColor`, fades, pulses, theater chase, Cylon and rainbow effects. The Edit Commands list no longer uses a separate color patch; the URDF viewer shows the actual four 16-LED eye/vent ring state.

## Create Library Pose

The **Create Library Pose** button in Edit Commands saves the command rows currently shown as a reusable single-time-point command group. You provide a JSON file name and description. The file is stored in `Library\Commands`. When inserted later with **Insert Library Pose**, all saved commands are placed at the selected timeline time.
