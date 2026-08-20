# Sequence Editor and Audio Timeline

A Sequence is an animation document containing timed servo, RGB and audio-play commands plus sequence metadata.

## Timeline navigation

- Left-click positions the cursor.
- Mouse wheel zooms the timeline.
- Middle-drag pans the visible time range.
- Right-click opens timeline actions.
- Drag a command triangle horizontally to move its complete command group.

Command triangles that overlap are vertically staggered for visibility.

## Audio

The primary audio waveform is shown on the timeline. Additional audio clips are represented by `Play` commands and are included in playback. Playback follows the timeline clock and switches audio sources as clip boundaries are crossed.

The Playback Volume control changes audible playback volume. URDF mouth-light response uses the audio amplitude plus the Audio LED Gain configured in URDF Configuration.

## Command markers

A triangle represents the commands at a timeline point. During collision-diagnostic playback a triangle can become bright red when the calibrated URDF pose produced at that point causes one of the configured flap collisions.

Editing commands clears old red collision-marker state. Replay the modified sequence to calculate fresh collision warnings.

## Manual grid staging

Several Servo Grid values can be changed before commands are generated. The staged values are discarded when another timeline time is selected or playback begins, returning the grid to the authored values for that point.
