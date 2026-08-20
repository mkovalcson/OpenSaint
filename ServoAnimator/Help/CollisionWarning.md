# Collision Warning

Collision Warning is a diagnostic URDF feature. It does not currently clamp, reject or alter commands sent to the physical robot.

## Checked collisions

Collision testing is intentionally limited to flap-related protection areas:

- Upper eye flaps against the outside of the left/right eye tubes.
- Upper eye flaps against the configured top gimbal contact areas and top gimbal bars when the corresponding eye is popped out.
- Lower eye flap panels against the lower gimbal contact areas and bottom gimbal bars when the corresponding eye is popped out.
- Lower eye flap panels against the front of the front lens.

Upper-flap Hitec servo bodies, mounting brackets and carrier hardware are excluded. Lower-flap/servo arms and hardware are also excluded. Those parts neither trigger collisions nor turn red.

## Playback indicators

When a checked collision occurs, only the relevant flap panel and the specific eye object involved are highlighted bright red. The command triangle whose resulting calibrated pose causes the collision is also highlighted bright red.

Editing commands clears the existing red command-triangle warnings so playback can recalculate them from the modified sequence.

## Toggle

Collision Warning can be switched On or Off in the URDF viewer. Turning it Off stops checks and clears collision highlighting/warning markers.
