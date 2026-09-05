# Movie Timeline

A Movie contains an ordered list of Sequence files. The Movie Timeline shows each sequence as a block whose width represents its duration. Each block also shows the Sequence Description in small text. While the Movie Timeline is visible, the separate Sequence Description area at the top of the editor is hidden.

## Editing blocks

- Left-drag a block to reorder it. A gold insertion line shows the destination.
- Right-click a block to remove it.
- Right-click a block and toggle **Loop Sequence** to make it repeat. Looping blocks show an infinity symbol in their upper-right corner.
- Right-click a sequence boundary to insert another sequence.
- Movie blocks are contiguous and cannot overlap.

The Movie Timeline has its own horizontal zoom/pan independent of the Sequence timeline.

## Playback hotkeys

- Up Arrow: play, pause or resume the movie sequence.
- Right Arrow: load and play the next sequence. From a looping block, it advances to the next non-looping sequence.
- Left Arrow: restart the current sequence; when already at the boundary, go to the previous sequence.
- Down Arrow: return to the beginning of the movie.

The **Previous Sequence** button to the left of **Play Movie** performs the same action as Left Arrow. During Sequence or Movie playback, the Movie Timeline cursor follows the current position within the selected sequence block.

At the end of a normal sequence, playback stops for the next cue unless the following block is looping; a following loop starts automatically. A looping sequence restarts at time zero immediately after its final command and repeats until Right Arrow is pressed.

When playback starts the next sequence or repeats a loop, the URDF and Live Head retain the pose, individual-servo state, speed state, neck ownership, and RGB lighting left by the preceding sequence. Commands in the new sequence change that carried state as their timestamps are reached; unspecified controls are not reset.

## Saving

**Save Movie** writes changes back to the currently loaded movie path. If the currently displayed sequence has unsaved edits, that sequence is saved first so the movie references the latest sequence contents and duration. If the sequence save is canceled or fails, the movie save is canceled. If the movie has never been saved, Save Movie invokes Save Movie As.

Movie description, sequence insert/remove/reorder changes, and loop flags are included.
