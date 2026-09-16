# Eye Mechanism CAD integration — v1.1.12

Source: `ServoAnimator/Models/SourceCAD/EyeMechanism.step`

## Placement
- SimplifiedHead2 eye tube CAD centers: X = ±99.822 mm.
- Eye tube front: head-link URDF X = 185.250 mm.
- At EyePop = 0, the CAD Front Lens is centered in the tube and its front face is exactly 2.000 mm ahead of the tube front.
- Front Lens optical opening radius: 29.9847 mm.
- Front Lens angled-to-flat transition radius: 30.7467 mm.
- The iris plane remains recessed behind the Front Lens opening. The gold treatment is now one continuous mesh: its flat inner band reaches the 29.9847 mm opening edge, a gold transition bridges the recessed opening depth, and the same mesh follows the angled Front Lens surface to the 30.7467 mm flat-face transition. The visible gold surface is offset 0.100 mm forward to prevent CAD-surface occlusion/z-fighting. The 22.860-24.796 mm radial band between the 1.80 in iris and the gold ring is black.

## Gimbal kinematics
The STEP gimbal-axis intersection is the CAD origin. With the import mapping `(X,Y,Z) -> URDF (Y,Z,X)` for vectors / `(Z,X,Y)` for points:
- `EyesVerticalUp`: the outer **Gimbal Ring** rotates about the horizontal screw axis (native X -> URDF Y).
- `EyesHorizontalRight`: the **Front Lens / inner lens section** rotates about the vertical screw axis through the lens (native Y -> URDF Z).
- The lens, its internal iris-rotator CAD, and the existing iris/RGB visuals inherit both rotations.
- The rear XY Eye Articulator/rack/servo mechanism remains rigid to the eye-pop carriage.

## Eye pop
- EyePop input `0` is the neutral/retracted position above.
- EyePop input `2000` maps by default to **89.951 mm** forward travel.
- At 2000, the front surface of each 3/8-inch Delrin actuator ball is 1.000 mm behind the front edge of the eye tube ("almost on the front edge").
- Left and right mechanisms travel independently with `LeftEyePop` / `RightEyePop`.

Runtime STL meshes are split into fixed-carriage, outer-gimbal, and inner-lens groups, each further split by STEP appearance color.
