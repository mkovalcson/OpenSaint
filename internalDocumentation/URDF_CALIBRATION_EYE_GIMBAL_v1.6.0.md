# URDF calibration + eye gimbal correction — v1.6.0

## Embedded calibration
The user-supplied `URDFconfig.json` v7 values were copied into `ServoAnimator/Models/johnny5_head.urdf` under the application-specific `<servo_animator_calibration>` element. `UrdfConfiguration.CreateDefault()` reads that block. If `animatorConfig/URDFconfig.json` exists, it is loaded afterward as an override, preserving compatibility and editability.

## EyeMechanism hierarchy
The source `EyeMechanism.step` contains two `[HEAD-B-B-01] Gimbal Spacer` components centered at native CAD X approximately +36.9189 mm and -36.9189 mm. Their common centerline is the native X axis through the CAD origin, which maps to local URDF +Y.

Runtime hierarchy:

`EyePop -> LensHorizontal (URDF Z) -> Gimbal Ring + Gimbal Spacers -> LensVertical (URDF Y through spacers) -> Wollensak Raptar lens + iris/pupil`

The Gimbal Spacers were separated from the formerly fixed CAD-color mesh and attached to the moving Gimbal Ring link, so the visible pivot hardware follows horizontal gaze while the lens pitches inside it.
