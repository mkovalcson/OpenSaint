# URDF Eye Gimbal Pivot Screws — v1.6.1

The two EyeMechanism components named:

`SCHCSCREW 0.086-56x0.125x0.125-HX-N_SCHCSCREW 0.086-56x0.125x0.125-HX-N`

are the top and bottom pivot screws associated with the Gimbal Ring. In v1.6.0 they were contained in the CAD1EE-colored Wollensak Raptar lens mesh and therefore incorrectly pitched with `Left/RightLensVertical`.

v1.6.1 separates those two connected screw solids from `eye_lens_cad1ee.stl`:

- `eye_lens_cad1ee_without_gimbal_screws.stl` remains on the vertical lens link.
- `eye_gimbal_schcscrew_0086_56x0125_cad1ee.stl` is attached to the horizontal Gimbal Ring link.

Result:

`EyePop -> LensHorizontal -> Gimbal Ring + Gimbal Spacers + two 0.125 in SCHCSCREWs -> LensVertical -> Wollensak Raptar lens/iris`

The screws therefore follow horizontal gaze and Eye Pop, but remain fixed relative to the Gimbal Ring during vertical lens pitch.

No collision element was added for these screw meshes.
