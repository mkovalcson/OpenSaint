# SimpleMouth CAD integration — v1.1.5

Source: `ServoAnimator/Models/SourceCAD/SimpleMouth.step`

## Ball-cup registration

The two `[NECK-C-UPR-COMN-02] Ball Cup (1-Piece)` components contain spherical seat surfaces of radius 6.35 mm. Their native STEP centers are:

- Cup 1: `(-78.298331453, -12.894421133, 69.344831453)` mm
- Cup 2: `( 78.298331453, -12.894421133, 69.344831453)` mm

The mouth assembly uses the same CAD-to-URDF axis rotation as the head (`rpy = 90°, 0°, 90°`). It is then translated in `head_link` by:

`(47.4035435, 0.0000790, -59.9998179)` mm

This maps the cup centers onto the current upper Delrin-ball centers. Per-center registration residual is below 0.00002 mm in the neutral pose.

Because the mouth assembly and upper-ball links are both children of the moving head chain, their alignment is retained during nod and tilt.

## Lip LEDs

The 14 front `Kingbright Orange Diffused LED_Bent` occurrences are extracted individually from this STEP file and sorted across the lip. `SetMouth()` keeps them light gray at silence and lights them red in symmetric center-out pairs as amplitude increases.

## Colors

Runtime static meshes are grouped from STEP presentation colors where present. The assembly includes light metal, gray, PCB green, spacer blue, and static red/green LED groups; otherwise unstyled hardware uses the existing steel/satin fallback.

## Vent color and preview light

All ten vent-fin visuals use the same `#080B4E` material. The WPF preview also adds a soft white point light above-left/front of the neutral model, without changing camera behavior.
