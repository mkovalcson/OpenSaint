# URDF Eye Gimbal Pivot Screws — v1.6.2

The lateral Gimbal Ring pivot screws are the two isolated CAD1EE screw solids centered at native EyeMechanism CAD X = +/-41.275 mm. The source STEP identifies this pair as:

`SCHCSCREW 0.086-56x0.375x0.375-HX-N_SCHCSCREW 0.086-56x0.375x0.375-HX-N`

They were previously part of the fixed eye-mechanism CAD1EE mesh, which meant they did not follow the Gimbal Ring correctly. v1.6.2 separates them into:

- `eye_gimbal_side_schcscrew_0086_56x0375_cad1ee.stl` — the two left/right screws, attached to the horizontal Gimbal Ring link.
- `eye_fixed_cad1ee_rest_without_gimbal_spacers_or_side_screws.stl` — the remaining fixed CAD1EE eye geometry.

The eye hierarchy is therefore:

`EyePop -> LensHorizontal -> Gimbal Ring + Gimbal Spacers + top/bottom screws + left/right screws -> LensVertical -> Wollensak Raptar lens/iris`

The lateral screws follow Eye Pop and horizontal gaze, but remain fixed relative to the Gimbal Ring during vertical lens pitch. No collision element is added for the lateral screw mesh.
