# Collision Geometry v1.6.3

The Hitec HS-85BB servo/carrier assemblies attached to the upper eye flaps are visual-only for collision diagnostics.

- `left_top_carrier_link` has no URDF `<collision>` element.
- `right_top_carrier_link` has no URDF `<collision>` element.
- The actual upper flap panels (`left_top_flap_link`, `right_top_flap_link`) remain collision-active.
- The collision highlighter contains a defensive exclusion so the upper carrier visuals cannot be painted red by a collision warning.
- No hardware or Live Drive behavior is changed.
