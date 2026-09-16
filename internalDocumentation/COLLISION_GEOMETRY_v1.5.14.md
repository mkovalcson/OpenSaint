# Collision Geometry v1.5.14

Collision diagnostics remain URDF-only and do not alter physical Live Drive commands.

## Lower Eye Flap Panel Collision

The lower eye-flap CAD remains split into two render groups:

- the complete flap panel;
- the flap/servo arm and attached hardware.

The **entire lower flap panel** now participates in collision detection. The previous
3 mm leading-edge-only proxy is no longer used by the URDF.

The separately rendered lower flap/servo arms and hardware still have no collision
geometry, so they:

- cannot trigger a collision warning;
- do not affect collision decisions when touching another component;
- are never highlighted red by collision diagnostics.

The full lower flap panel is checked against:

- the bottom moving-gimbal contact band when the corresponding eye is popped out;
- the fixed bottom Gimbal Bar when the corresponding eye is popped out;
- the front-most face of the front lens.

Upper-flap collision behavior remains unchanged.
