# URDF Collision Geometry — v1.5.11

- The URDF retains the collision geometry added in v1.5.10; calibration remains fully configurable through `URDFconfig.json`.
- Runtime collision diagnostics are now intentionally limited to flap safety checks:
  - flap ↔ flap
  - flap ↔ fixed left/right eye tube
  - flap ↔ moving eye assembly, but only once that calibrated Eye Pop pose has brought the assembly to the eye-tube front or farther forward
- The fixed eye tubes are identified from the two components of `SimplifiedHead2/static_e9e9eb.stl`; the rest of the fixed head geometry is ignored by collision checking.
- Eye-front eligibility is derived from the current transformed collision geometry with a 0.5 mm touching tolerance rather than a hard-coded Eye Pop command value, so it follows URDF calibration.
- Other `<collision>` elements remain available in the URDF but are not evaluated by the real-time checker.
- Active flap collisions still highlight the involved URDF components bright red. During playback, command triangles still turn bright red when the resulting pose produces one of these allowed collision categories. Editing commands clears the diagnostic triangle state as before.
- Collision diagnostics remain visualization-only and do not clamp, reject, or otherwise modify physical Live Drive commands.
