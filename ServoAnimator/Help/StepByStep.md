# Step-by-Step Tutorials

These walkthroughs use the current Commands pane beside the URDF model. The former Grid controls have been removed. Work through the first two tutorials to create a simple animation, then choose the other tasks as needed.

## Before you begin

1. Stop playback using the square Sequence Stop button before opening Help. Help is unavailable while playback is running or paused.
2. Leave **Live Drive off** while learning. The URDF can preview your work without sending movements to the physical robot. Default Positions and Disable Servos are separate hardware actions; do not use Default Positions as a preview-only reset.
3. If the application asks for a Configuration folder, select your intended folder. Use **Config > Set Paths** to review the active folders. Audio, Sequence, Movie and Library files must be inside this Configuration folder or its child folders.
4. Find **Commands** beside the docked URDF. Commands and Movie Timeline now remain visible at all times. Drag the horizontal bar below Commands to change its height, or the vertical divider to change its width beside the URDF. Leave the URDF viewer's own **Drive** enabled so its visual preview follows playback; this is separate from physical Live Drive.

Live Drive and the connection indicators are now in the right-hand vertical strip. They stay on the main display when you undock the URDF. Hold the middle mouse button and drag either timeline to pan.

Expected result: you can see the Commands pane, the waveform timeline and the URDF preview, with Live Drive off.

## 1. Create your first Sequence

1. Save any work you want to keep, then choose **File > New**. New clears both the current Sequence and Movie workspace; respond to any unsaved-work prompts. The standalone Sequence header and description appear at the top, and the empty Movie Timeline remains visible below.
2. If you want an audio track, choose **File > Open Audio** and select a file under the Configuration folder. Audio is optional for this example.
3. Move the Sequence cursor to the beginning with its **|◀** button. Click **+ Add** before the Commands heading.
4. In Edit Commands, enter **0.000** for Offset, select **NeckTurn**, and enter **-20** for Value. Leave Disable unchecked and Speed at **N/C**. Keep Live Drive off for this practice animation.
5. Click **Apply and Close**. A command triangle should appear at time zero.
6. Click a later point on the waveform and click **+ Add** again. Set Offset to **1.000**, Servo to **NeckTurn**, and Value to **20**. Click **Apply and Close**.
7. Add a third command the same way, with Offset **2.000**, Servo **NeckTurn**, and Value **0**. Click **Apply and Close**.
8. Click **Fit** in the Sequence transport if necessary to see the complete animation. Hover each triangle to inspect its command list.
9. Click the small bold **edit** after the Sequence description, enter a description such as **Practice head turn**, and save the description in the popup. This changes the document but does not yet save its JSON file.
10. Choose **File > Save Sequence As**, select a location under Configuration, and save a name such as **PracticeHeadTurn.json**.

Expected result: the Sequence contains three NeckTurn commands at 0, 1 and 2 seconds. The unsaved asterisk after its name disappears after saving.

## 2. Make the movement smooth with a Spline

1. Open the three-command Sequence from tutorial 1, or use another Sequence with at least two numeric commands for the same whole-servo target at different times.
2. Double-left-click one of its command triangles to open Edit Commands. You can also select its entry in Commands and click **Edit**, or double-click that entry.
3. Check **Spline** to the left of the Servo picklist for **NeckTurn**, then click **Apply and Close**. The checkbox enables interpolation for that servo throughout the Sequence, not just for the selected command.
4. Confirm that the Spline graph appears. Use the picklist beside **?** to choose **Waveform top**, **Spline top**, or **Combined**. Its colored checkbox legend sits above the graph; those legend checkboxes only show or hide curves and do not change interpolation. If you hide a line, click **show all** to restore every line. Double-click a selected control point to reopen its command editor.
5. Hover a control point to see its exact time and value beside the pointer. The horizontal reference lines represent -100 through +100 in steps of 25 for typical centered servos. Other servo ranges use the same graph height but retain their own actual values.
6. Left-drag a point vertically to adjust its value. Right-drag a point horizontally to adjust its time. Do not move it onto another point for the same servo at the same millisecond.
7. Return the Sequence cursor to the beginning and click **▶ Sequence**. Watch the URDF preview, then click the square Stop button when finished.
8. Choose **File > Save Sequence** to keep the Spline setting and any point edits. Use **Edit > Undo** if you want to reverse a change before saving.

Expected result: the preview interpolates between the control points. Neck Nod and Neck Tilt share a Spline setting. RGB/audio and individual child-only commands do not support independent Spline interpolation.

## 3. Edit, move or remove existing commands

1. Hover a triangle to identify its command group, then click it to place the Sequence cursor at that time.
2. Select an entry in Commands and click **Edit**. Change its Value, Servo, Offset, Speed or Disable state as needed. **Add Command** adds another row in the same editor.
3. Click **Apply and Close** to commit the draft, or **Cancel** to discard it. Numeric edits can preview immediately while the editor is open; keep Live Drive off if you do not want physical movement.
4. If a Redundant Commands warning appears, compare the earlier and later entries and select the value to keep for each conflicting target/time. Click **Keep selected**, or Cancel to return without applying the conflicting edit.
5. Ctrl-left-click triangles to select or deselect multiple time groups; Shift-left-click includes the triangles between the anchor and the click. Drag a selected triangle to move them together. To move only one command within a triangle, edit that command's Offset instead.
6. To remove one command, select it in Commands and click **Delete**. Use **Edit > Undo** to recover an unwanted deletion. **Edit > Undo History** lets you select an older action to undo it and every newer action.
7. Right-click the selected group for **Uniform offset**, **Repeat**, **Copy**, or **Delete**. For Copy, click a destination in the waveform and right-click **Paste**. Uniform offset keeps the first selected time fixed; Repeat appends additional copies after the group's end plus the entered offset.
8. Click outside the waveform to clear the selection. Save the Sequence after reviewing the result.

Expected result: commands for different targets can share a time point, but each individual target has only one retained command at that millisecond.

## 4. Capture a complete pose from the URDF

1. Stop playback, leave Live Drive off, and place the Sequence cursor where the pose should be stored.
2. Click **Pose** in the URDF viewer. Use **LR Joined** for coordinated left/right movement or **LR Split** for independent sides.
3. Adjust the visible pose handles, sliders and dials. Use a control's small reset arrow if you need to return that control to its neutral setting.
4. If the pose needs lighting, enter a **RGB Command** or use **Build** in the Pose editor.
5. Right-click the Sequence timeline and choose **Insert Pose**. If existing commands conflict with the inserted pose, choose the entries to keep in the Redundant Commands warning.
6. Leave Pose mode before reviewing timeline playback. Select the inserted triangle and check its commands, then save the Sequence.

Expected result: the pose becomes authored commands at the cursor. Simply arranging or leaving Pose mode does not insert commands into the Sequence.

## 5. Add an audio clip or RGB command

1. To set the main audio track, choose **File > Open Audio** and select an audio file inside Configuration.
2. To insert an additional clip, place the Sequence cursor at the intended start, right-click the Sequence timeline, and choose **Insert audio file at ...**. Select the audio file and confirm.
3. To add lighting, position the cursor and click **+ Add** in Commands. Select **RGBCommand** from the Servo picklist.
4. Enter the command text or click **Build** to choose and configure a supported lighting command. Click **Apply and Close** when the text is correct.
5. Play from before the new command and review the sound and URDF lighting. Adjust the transport Volume slider for audible playback volume; it does not change the authored command.
6. Stop playback and save the Sequence.

Expected result: the new audio clip or RGB command is associated with the chosen timeline offset. Live Drive must be enabled separately to send lighting to connected hardware.

## 6. Build and save a Movie

1. Create and save the Sequences you want to use. A Movie references Sequence files; it does not replace the need to save each Sequence.
2. Find the always-visible **Movie Timeline** below the Sequence transport. If you need an empty workspace, save first and use **File > New**, which clears both the Sequence and Movie.
3. In an empty Movie timeline, click **Insert First Sequence**. Select a saved Sequence under Configuration.
4. Right-click at the desired boundary and choose **Insert Sequence Here** to add another Sequence. Repeat until all blocks are present.
5. Drag blocks left or right to reorder them. Use the Movie timeline's own **Fit**, plus or minus buttons to adjust its view.
6. Click the small bold **edit** after the Movie description to describe the Movie. The edit link inside a Sequence block edits that Sequence's description instead.
7. Choose **File > Save Movie As** and save a Movie JSON file under Configuration. After later changes, use **File > Save Movie**.
8. To build another Sequence in this Movie, click **New Sequence** after the last block. Use Movie **Fit** or scroll to the end to find it. Enter a name, click **Create**, and respond to any unsaved-work prompt.
9. Build commands/audio in the now-empty Sequence timeline. Choose **File > Save Sequence**, confirm the proposed name and a location under Configuration, and save. The new block is appended automatically only after saving successfully.
10. Choose **File > Save Movie** to preserve the appended block. Canceling the Sequence save leaves a draft; saving it again later will still append only one block.

Expected result: the Movie shows contiguous Sequence blocks and its complete duration. Save Movie also saves the currently edited Sequence first when needed; canceling or failing that Sequence save cancels the Movie save.

## 7. Run a Movie with looping cues

1. Open a Movie containing a normal Sequence, a Sequence intended to loop, and a following normal Sequence. For a predictable first test, use one looping block between two normal blocks.
2. Right-click the middle block and check **Loop Sequence**. Confirm that an infinity symbol appears on it, then save the Movie to retain this setting.
3. Click near the start of the first normal block and click **▶ Movie**. The Movie cursor follows the Sequence being played.
4. Let the first normal block finish. Because the next block is looping, it starts automatically.
5. Let the looping block finish. Immediately after its final command, it restarts at time zero and continues repeating.
6. Press **Right Arrow** to start the next non-looping Sequence. The Movie Next button performs the same cue advance; use it if keyboard focus is in another control. While typing or using a list or slider, arrow keys remain available to that control.
7. At the end of a normal Sequence, playback waits for the next cue unless the following Sequence is looping. Press Right Arrow for the next normal cue. Use **Up Arrow** to pause/resume a movie Sequence and **Left Arrow** to restart it; pressing Left again at its beginning moves to the previous Sequence.
8. Use the square Sequence Stop button to stop playback before returning to Help.

Expected result: looping blocks repeat until advanced. Sequence transitions preserve the prior robot state; the next Sequence changes only the controls it commands. Advancing from a loop skips additional looping blocks to find the next normal Sequence; at the end of the Movie there may be no next normal cue.

## 8. Reuse a Library Pose

1. Arrange a pose in the URDF Pose editor with Live Drive off.
2. Click **Library +**, provide a name and description, and save the Library Pose. A custom image is optional; without one, the application captures a preview image.
3. To reuse it only in the Pose editor, click **Library Load**, choose the pose, and load it. You can adjust it further before inserting it.
4. To insert the saved pose directly into a Sequence, position the Sequence cursor, right-click the timeline, and choose **Insert Library Pose**. Select the saved pose and confirm.
5. Resolve any redundant command choices, review the inserted command group, and save the Sequence.

Expected result: loading into Pose mode changes the draft; inserting on the timeline creates commands at the selected time.

## 9. Save, reopen and check your work

To inspect a group before saving, Ctrl-click its command triangles (or Shift-click an inclusive range), right-click and choose **Show all modified controls**. Click a control in the Commands list to highlight all matching triangles in purple. Click outside the waveform and that inspection list to return to Commands at Cursor.

1. Look for an asterisk directly after the Sequence or Movie name. It means that document has unsaved changes.
2. Use the explicit **File > Save Sequence** and **File > Save Movie** commands for the documents you changed. Ctrl+S saves the active document, so use the explicit menu commands if you are unsure which is active.
3. Keep all referenced audio, Sequence and Library files under the active Configuration folder. Saved file references are relative to that folder.
4. Close the application normally to retain its window placement, pane sizes and URDF camera arrangement. Commands and Movie Timeline remain visible when reopening.
5. Reopen the application and confirm the expected document and layout return. You can also use **File > Open Recent** or the Load Sequence/Load Movie commands to select a different saved document.

Expected result: the saved document reloads without unsaved markers. Recovery snapshots are a backup for interrupted work, not a replacement for saving your files.

## Where to go next

Use the related topics below for detailed controls, file-path rules and hardware configuration. Before using the physical robot, review **Live Drive and Hardware** and **Servo Configuration**, confirm the calibration and clearance, and preview the animation with Live Drive off first.
