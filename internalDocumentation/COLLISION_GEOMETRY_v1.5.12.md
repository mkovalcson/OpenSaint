# Collision Diagnostics — v1.5.12

Collision checking remains diagnostic-only and does not alter Live Drive or hardware commands.

## URDF viewer control

Each URDF viewer has an upper-left toggle:

- `Collision Warning: On`
- `Collision Warning: Off`

The embedded and detached URDF viewers are synchronized. Turning warnings Off clears active URDF collision highlighting and existing red collision command markers.

## Collision pairs evaluated

Only the following flap safety contacts are evaluated:

1. Upper eye flap ↔ outside of left/right fixed eye tube.
2. Upper eye flap ↔ top contact band of the left/right gimbal, only when that eye's Eye Pop logical value is greater than 0.
3. Lower eye flap ↔ bottom contact band of the left/right gimbal, only when that eye's Eye Pop logical value is greater than 0.
4. Lower eye flap ↔ front-most face region of the left/right front lens.

No flap-to-flap, head, neck, nose, vent, antenna, microphone, or other collision pairs are checked.

## Collision geometry refinements

The former combined eye-tube visual was split into independent left/right STL meshes without changing its rendered shape. This allows only the tube actually involved in a collision to highlight red.

The gimbal collision proxy is clipped to narrow top/bottom surface bands, and the front lens proxy is clipped to its front-most face, reducing false positives caused by conservative bounding volumes around hollow CAD geometry.

## Timeline warnings

During playback, a command triangle is marked bright red when the calibrated URDF pose resulting at that command time contains one of the enabled collision pairs above. Editing commands clears the prior warning markers so playback can recalculate them.
