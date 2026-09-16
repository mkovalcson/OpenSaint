# URFD Head / Neck CAD integration — v1.1.1

Source CAD:

- `ServoAnimator/Models/SourceCAD/URFDHeadAssembly.step`
- `ServoAnimator/Models/SourceCAD/URFDNeckAssembly.step`

Runtime meshes:

- `ServoAnimator/Models/Meshes/HeadAssembly/URFDHeadAssembly.stl`
- `ServoAnimator/Models/Meshes/NeckAssembly/URFDNeckAssembly.stl`

The runtime STL files are generated from the supplied STEP assemblies; they are not the earlier `HeadShell.stl`. The STEP sources are retained alongside the project for future geometry updates.

## Coordinate system

The supplied CAD assemblies use the same native orientation as the prior head-shell geometry. The URDF conversion is:

- CAD Z -> URDF X (forward)
- CAD X -> URDF Y (left/right)
- CAD Y -> URDF Z (vertical)

implemented with `rpy="1.570796 0 1.570796"` and a 0.001 scale from millimeters to meters.

## Head placement

The head assembly retains the v1.1.0 shell transform:

`origin xyz="-0.043096 0 0"` relative to `head_link`.

This preserves the eye/flap/vent/accessory alignment established against the earlier shell because the supplied head STEP uses the matching shell coordinate system.

## Neck fit

The head STEP ball-cup spherical centers were measured from the actual spherical face of `[NECK-C-UPR-COMN-02] Ball Cup (1-Piece)`:

- left/right: X = +/-78.298331 mm
- Y = -72.894421 mm
- Z = 159.844831 mm

The neck STEP upper Delrin ball centers are:

- left:  X = +78.298405, Y = 157.796753, Z = 65.090372 mm
- right: X = -78.298248, Y = 157.796769, Z = 65.090378 mm

No scale or rotation correction is required between the two CAD files. The neck mesh is translated so these ball centers coincide with the head cup centers at the neutral pose. In `neck_yaw_link` coordinates the runtime visual origin is approximately:

`xyz="0.123658 0 -0.230691"`

with the same CAD-to-URDF RPY conversion used by the head. The fitted center residual is below 0.001 mm at each upper ball.

## Mouth center

The 48 red mouth LED occurrences in the head STEP have a bounding/mean center of:

`(-0.127, -66.4125, 85.039) mm` in head CAD coordinates.

After the head CAD-to-URDF transform, the animated `mouth_talk_link` is placed at:

`xyz="0.041943 -0.000127 -0.066413"`

relative to `head_link`, centering the animated red mouth on the physical LED array.

## Kinematics

- The complete neck CAD mesh is a visual on `neck_yaw_link`, replacing the old neck cylinder and two synthetic gold cylinders.
- It therefore rotates with `NeckTurn`.
- `NeckNodUp` and `NeckTiltRight` continue to transform the head assembly through the existing head joint chain.
