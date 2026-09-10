# Movie Timeline

A Movie contains an ordered list of Sequence files. The Movie Timeline remains visible at all times and shows each sequence as a block whose width represents its duration, with a minimum width of 100 pixels. Short sequences retain that minimum at every zoom level; the cursor still tracks the correct time within each block. Each block also shows the Sequence Description in small text. The separate Sequence Description area is hidden when the current Sequence is represented by a Movie block; standalone Sequences retain their header and description editor.

Below the timeline, the Movie name is followed by an unsaved `*` when modified and then the Movie's complete duration. Its description is read-only; click the small bold **edit** link immediately after it to change the text in a popup editor. The Created Date is not displayed, but remains saved in the movie file. Movie transport buttons match the Sequence transport height, and the controls area grows or shrinks to fit the description.

## Editing blocks

When a Movie is open, **New Sequence** appears just to the right of its last block (or at the start of an empty Movie). Use Movie **Fit** or scroll to the end if the button is offscreen.

Click **New Sequence**, enter a name, and choose **Create**. Resolve any unsaved-sequence prompt. The Sequence timeline and audio are cleared, but existing Movie blocks stay intact. The new name appears with an unsaved asterisk while you build the Sequence.

Choose **File > Save Sequence** (or Ctrl+S while this new Sequence is active), select a location under Configuration, and save. Only a successful save appends the new block at the current end of the Movie; canceling a save leaves it as a draft, and later saves do not append another copy. Finally, **Save Movie** to retain the updated sequence list. **Save Movie** can also save the draft and append it before saving the Movie in one operation.

- Click the small bold **edit** link after a block's Sequence description to open its description editor. Switching to another sequence first checks for unsaved changes. Long descriptions are shortened while keeping the link visible; zoom in if a block is too narrow to show it.
- Left-drag a block to reorder it. A gold insertion line shows the destination.
- Right-click a block to remove it.
- Right-click a block and choose **Assign trigger**, press a key or modifier combination (such as **Ctrl+Shift+5**), then click **Assign**. Use **Clear**, then **Assign**, to remove it. Assignments are saved with the Movie and stay with blocks when reordered. Duplicate triggers, arrows, unmodified Space, and document shortcuts are reserved.
- Triggers appear as compact key badges at the top right of each block, with the infinity symbol below for loops. Long triggers expand the minimum block width.
- Right-click a block and toggle **Loop Sequence** to make it repeat. Looping blocks show an infinity symbol in their upper-right corner.
- Right-click a sequence boundary to insert another sequence.
- Movie blocks are contiguous and cannot overlap.

The Movie Timeline has its own horizontal zoom/pan independent of the Sequence timeline.

Mouse-wheel and +/- zoom are anchored to the Movie cursor so its screen position stays fixed while timeline bounds allow. Fit shows the complete Movie and New Sequence button when space allows. If the minimum-width blocks cannot fit, scroll horizontally to reach the rest; Fit never shrinks a block below 100 pixels. Zooming out to a timeline boundary can move the cursor on screen.

## Playback hotkeys

- Up Arrow: play, pause or resume the movie sequence.
- Right Arrow: load and play the next sequence. From a looping block, it advances to the next non-looping sequence.
- Left Arrow: restart the current sequence; when already at the boundary, go to the previous sequence.
- Down Arrow: return to the beginning of the movie.
- Assigned triggers immediately start their sequence from the beginning while Movie playback is active or the Movie area has keyboard focus. They use the same pose continuity and subsequent loop/cue rules as Right Arrow. Held keys do not repeatedly restart playback; editable controls keep their normal keyboard input.

The **Previous Sequence** button to the left of **Play Movie** performs the same action as Left Arrow. During Sequence or Movie playback, the Movie Timeline cursor follows the current position within the selected sequence block.

At the end of a normal sequence, playback stops for the next cue unless the following block is looping; a following loop starts automatically. A looping sequence restarts at time zero immediately after its final command and repeats until Right Arrow is pressed.

When playback starts the next sequence or repeats a loop, the URDF and Live Head retain the pose, individual-servo state, speed state, neck ownership, and RGB lighting left by the preceding sequence. Commands in the new sequence change that carried state as their timestamps are reached; unspecified controls are not reset.

This also applies to individual children within a servo group: a loop that commands one child leaves its uncommanded siblings at their carried positions, even after repeated restarts. A command targeting the whole group intentionally affects all of its children.

## Saving

Sequence cards use an accent outline for the selected block and a subtle progress tint
as its cursor advances. Titles are emphasized, durations are quieter, and looping cards
show an infinity badge. The tint does not change Movie timing or sequence selection.

**Save Movie** writes changes back to the currently loaded movie path. If the currently displayed sequence has unsaved edits, that sequence is saved first so the movie references the latest sequence contents and duration. If the sequence save is canceled or fails, the movie save is canceled. If the movie has never been saved, Save Movie invokes Save Movie As.

Movie description, sequence insert/remove/reorder changes, and loop flags are included.
