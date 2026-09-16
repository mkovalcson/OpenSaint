# Whip Antenna hinge kinematics — v1.2.5

Source assembly: `ServoAnimator/Models/SourceCAD/Whip AntennaColor.step`

## CAD hinge and split

The STEP occurrence named `ASME B18.8.2 - 0.0627x0.25` is the upper-linkage hinge. Its center in the whip assembly is CAD `(X,Y,Z)=(-3.175, 2.921, 0)` mm, with its long axis along CAD X. Under the established head-top mapping `(X,Y,Z) CAD -> (Z,X,Y) URDF`, the hinge axis becomes URDF +Y and the hinge center is local URDF Z = 2.921 mm. The lateral component location is already embodied by the pin mesh; the revolute pivot is the linkage centerline at local `(0,0,2.921)` mm.

The original color-group meshes combined moving and nonmoving linkage components. v1.2.5 therefore extracts individual STEP occurrences. The upper linkage, threaded stud, skirt, and knob are recentered at the hinge and attached to `whip_fold_link`; the lower linkage and remaining lower hardware stay on `whip_rotate_link`. At fold angle 0°, their combined bounds match the previous color-group runtime meshes.

## Head-top trigger geometry

The visible flat top surrounding the whip opening in `[HEAD-A-B-02] Whip Antenna Top` tessellates at CAD Y = 59.181999 mm, mapping directly to head-local URDF Z = 59.181999 mm. The existing `Whip_Antenna_RaiseLower` joint origin is head Z = 1.764 mm.

The hinge therefore reaches the head top at:

`59.181999 - 1.764 - 2.921 = 54.496999 mm` lift.

The horizontal flat shoulder at the top of `[HEAD-H-B-00-LWR] Linkage (Lower)` is CAD Y / local URDF Z = 0.000 mm. It reaches the same head-top plane at:

`59.181999 - 1.764 - 0.000 = 57.417999 mm` lift.

`RobotHeadView.ApplyWhipRaiseLower()` maps actual configured lift linearly from 0° at 54.496999 mm to 90° at 57.417999 mm, clamps below to 0°, and clamps above to 90°. Because this uses `Motion(...)` output, URDF Configuration lift extents and direction continue to apply before the mechanical fold calculation.
