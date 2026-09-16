# URFD CAD neck kinematics and lip LEDs — v1.1.2

This revision refines the URFD STEP-based head/neck model.

## Neck structure and materials

- The obsolete standalone pedestal disk is removed.
- `NeckTurn` rotates the complete visible neck/head assembly about the center of the CAD `[NECK-D-V3-COMN-00] Disc` at the bottom of the neck assembly.
- The CAD Disc, Concave Block and rubber bellows are rendered black.
- The two Fabco K-5-X assemblies are rendered gold.
- The four Delrin balls are rendered white.
- Remaining neck CAD is rendered metallic gray.

## Nod and tilt pivot

The Lovejoy Solid U-Joint was analyzed directly from `URFDNeckAssembly.step`. Its perpendicular hinge cylinders intersect at native CAD approximately:

`X = 0.000084 mm, Y = 154.168490 mm, Z = 0.000047 mm`

After the neck CAD is rebased to the bottom-disc center and converted to URDF axes, the hinge intersection is:

`(0, 0, 0.109029515) m`

Both `NeckNodUp` and `NeckTiltRight` pivot at that point, on the two perpendicular U-joint hinge axes.

## Fabco visual linkage

During nod/tilt playback:

- each lower Delrin ball remains at its CAD location in `neck_yaw_link`;
- each upper Delrin ball follows the head and rotates in place with it;
- each Fabco body swivels about its lower ball to track the moving upper ball;
- each piston swivels with the cylinder and translates axially by the change in ball-to-ball distance.

This linkage visualization is calculated in `RobotHeadView.UpdateFabcoKinematics()` from the actual STEP ball centers.

## Head-top controls

The movable microphone, MFR and whip-antenna URDF controls are centered horizontally over the corresponding locations in `URFDHeadAssembly.step`. Their established neutral heights are retained. The fixed CAD copies of these three assemblies are excluded from the runtime head-base mesh so the articulated controls are not duplicated.

## Head detail plates

All components identified as `Detail Plate` in the head STEP hierarchy are extracted into a separate runtime mesh and rendered black.

## Voice amplitude — Lip Light Box LEDs

The old synthetic red mouth rectangle is removed. The 14 actual orange bent LEDs across the front of the CAD Lip Light Box are extracted as individual runtime meshes.

`SetMouth(amplitude)` now lights them bright red in seven symmetric pairs:

1. center two LEDs;
2. next pair outward;
3. next pair outward;
4. next pair outward;
5. next pair outward;
6. next pair outward;
7. outermost pair.

At zero amplitude the 14 LED lenses return to their orange neutral color. Any non-zero amplitude lights at least the center pair, and increasing amplitude progressively lights additional pairs until all 14 are red.
