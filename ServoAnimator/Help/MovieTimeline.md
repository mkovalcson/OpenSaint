# Movie Timeline

A Movie contains an ordered list of Sequence files. The Movie Timeline shows each sequence as a block whose width represents its duration.

## Editing blocks

- Left-drag a block to reorder it. A gold insertion line shows the destination.
- Right-click a block to remove it.
- Right-click a sequence boundary to insert another sequence.
- Movie blocks are contiguous and cannot overlap.

The Movie Timeline has its own horizontal zoom/pan independent of the Sequence timeline.

## Playback hotkeys

- Up Arrow: play, pause or resume the movie sequence.
- Right Arrow: load and play the next sequence.
- Left Arrow: restart the current sequence; when already at the boundary, go to the previous sequence.
- Down Arrow: return to the beginning of the movie.

## Saving

**Save Movie** writes changes back to the currently loaded movie path. If the currently displayed sequence has unsaved edits, that sequence is saved first so the movie references the latest sequence contents and duration. If the sequence save is canceled or fails, the movie save is canceled. If the movie has never been saved, Save Movie invokes Save Movie As.

Movie description, sequence insert/remove/reorder changes and other movie-file edits are included.
