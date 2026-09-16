# Collision Geometry v1.5.13

Collision diagnostics remain URDF-only and do not alter physical Live Drive commands.

## Added Eye Gimbal Bars

The two fixed `[HEAD-B-B-02] Gimbal Bar` components from `EyeMechanism.step`
are now split into independent top and bottom render/collision meshes:

- `eye_gimbal_bar_top_cad1ee.stl`
- `eye_gimbal_bar_bottom_cad1ee.stl`

They move with the Eye Pop assembly. During collision diagnostics:

- Upper eye flaps can collide with the **top Gimbal Bar** only while that eye is popped out.
- Lower eye-flap edges can collide with the **bottom Gimbal Bar** only while that eye is popped out.
- Only the individual Gimbal Bar involved in a collision is highlighted red.

Existing moving-gimbal top/bottom contact-band checks remain active.

## Lower Eye Flap Collision Edge

Each lower flap CAD assembly is split for rendering into:

- the actual lower flap panel;
- the lower flap arm and attached hardware.

Only the flap panel participates in collision highlighting. A narrow 3 mm leading-edge
collision proxy is generated at the edge farthest from the hinge. The lower flap arm and
hardware have no collision proxy and therefore:

- cannot trigger a collision warning;
- are never colored red by collision diagnostics.

The lower flap leading edge is checked against:

- the bottom moving-gimbal contact band when the corresponding eye is popped out;
- the fixed bottom Gimbal Bar when the corresponding eye is popped out;
- the front-most face of the front lens.

Upper-flap eye-tube and moving-gimbal checks from v1.5.12 remain unchanged.
