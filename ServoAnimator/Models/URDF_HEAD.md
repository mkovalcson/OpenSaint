# Animation Editor & Player — Johnny 5 URDF head

`johnny5_head.urdf` is the application's lightweight, animation-ready 3-D model.  The visible head is generated from `Models/SourceCAD/SimplifiedHead2.step`; the neck remains generated from `Models/SourceCAD/URFDNeckAssembly.step`.  STEP/BREP geometry is tessellated to binary STL for the WPF renderer while retaining the CAD component placement and STEP appearance colors.

## Coordinate system

- URDF X: forward through the face
- URDF Y: robot left
- URDF Z: up
- STEP millimetres are rendered with mesh scale `0.001`.
- The head CAD mapping is STEP `(X,Y,Z)` → URDF `(Z,X,Y)`, with the existing `-43.096 mm` head-X placement offset.

## Neck hierarchy and mechanism

`base_link` → `NeckTurn` → articulated neck/head mechanism.

- `NeckTurn` is centered on the CAD Disc at the bottom of the neck assembly.
- `NeckNodUp` and `NeckTiltRight` use the two perpendicular hinge axes of the Solid U-Joint; their axes intersect at the CAD hinge center.
- The top Delrin balls follow head nod/tilt.  The bottom Delrin balls remain at the lower pivots after yaw.
- `RobotHeadView.cs` solves the visual Fabco linkage: each Fabco body swivels about its bottom ball and its piston follows the moving upper-ball endpoint.

## SimplifiedHead2 articulated geometry — v1.1.4

The supplied `SimplifiedHead2.step` is authoritative for the head shell, nose, eye flaps, and eye-tube vent mechanism.

### Nose

- `NoseBody` is a revolute CAD assembly centered on the **ASME B18.8.2** spindle.
- `NoseBasket` is a revolute child of the nose body centered on **[HEAD-E-V2-B-02] Pin**.
- The imported STEP pose is the zero/neutral pose for both controls.

### Upper eye flaps

Each upper flap has two nested rotations matching the real mechanism:

1. `Brow*TopTilt` rotates the complete upper-flap assembly about the HS-85 output pinion mounted in the Nose Basket.
2. `Brow*TopOpen` then rotates the flap assembly about the output pinion of the HS-85 mounted on that upper flap.

The top-flap assemblies are therefore carried by the moving Nose Basket rather than by the fixed head shell.

### Lower eye flaps

`BrowLeftBottomOpen` and `BrowRightBottomOpen` rotate the actual lower-flap CAD assemblies about their servo-shaft/pinion axes measured from the STEP model.

### Eye-tube vents

The previous synthetic vent controls are removed.  `VentsOpen` now controls the Hitec **HS-40** mechanism present in each eye-tube assembly:

- `LeftEyeVent` / `RightEyeVent` rotate the corresponding HS-40 horn/pivot-strut assembly about the CAD output axis.
- Five physical vent-fin links per side rotate about their individual CAD pivot pins.
- The logical 0..100 vent range is visualized as 0..30 degrees of travel.

Exact CAD-derived pivots are stored in `Models/Meshes/SimplifiedHead2/pivots.json`.

## STEP colors

The static and articulated runtime meshes retain the component appearance assignments present in `SimplifiedHead2.step`.  The imported head palette currently includes:

- near-black `#020202`
- deep blue `#080B4E`
- gray `#737373`
- blue-gray `#97A3DA`
- gold `#E7C863`
- light silver/gray `#E9E9EB`
- warm polished metal `#F6F4E9`
- fallback satin gray `#A0A0A0` where the STEP component has no explicit appearance

The neck retains its CAD-derived colors plus the explicit application overrides established in v1.1.2/v1.1.3: the bottom Disc, Concave Block and bellows are black; Fabco bodies are gold; pistons use polished silver.

## Lip Light voice display

The 14 front Lip Light LEDs remain independently recolorable materials.  When voice amplitude is inactive they are **light gray (`#D3D3D3`)**.  As audio amplitude increases, the center pair turns bright red first, followed symmetrically by additional pairs toward the outside until all 14 are red.

## Logical ganged controls

- `EyesHorizontalRight`: `LeftLensHorizontal`, `RightLensHorizontal` — inner Front Lens gimbal about the vertical lens-screw axis
- `EyesVerticalUp`: `LeftLensVertical`, `RightLensVertical` — outer Gimbal Ring about its horizontal screw axis
- `IrisClose`: `LeftIris`, `RightIris`
- `FlapsOpen`: all four `Brow*Open` joints
- `FlapTiltUp`: both `Brow*TopTilt` joints
- `VentsOpen`: both HS-40 vent drives plus their physical vent-fin joints
- `BothEyePop`: `LeftEyePop`, `RightEyePop`

See `servo_joint_map.json` for ranges and mappings and `SIMPLIFIED_HEAD_CAD_v1.1.4.md` at the project root for the CAD-derived pivot details.

## Renderer-specific behavior

`RobotHeadView.cs` supplies behavior that a single-tree URDF cannot express directly:

- dynamic Fabco closed-linkage visualization
- iris aperture scaling
- audio-driven Lip Light LED colors
- the grouped HS-40/vent-fin visual drive
- direct URDF preview motion whether Live Drive is on or off

Live Drive only gates output to the physical robot; it does not gate URDF animation.

## SimpleMouth supplemental assembly — v1.1.5

`SimpleMouth.step` restores the mouth/Lip Light Box and underside ball-cup hardware omitted from the simplified head assembly. Its native ball-cup sphere centers are approximately `(-78.298331, -12.894421, 69.344831)` mm and `(78.298331, -12.894421, 69.344831)` mm. After the shared CAD axis conversion, the assembly is translated by approximately `(47.403543, 0.000079, -59.999818)` mm in `head_link`, aligning both cup centers to the upper Delrin balls in the neutral pose.

The 14 front lip LEDs also come from this assembly and remain individually color-addressable by `RobotHeadView.SetMouth()`.

## v1.1.6 rear cover and configurable visual travel

`rearCover.STEP` is tessellated into `Meshes/RearCover/rear_cover.stl` and attached to `head_link`. Its broad flat mounting surface is registered to the SimplifiedHead2 native rear datum while the cover extends behind the head.

Visual servo travel is no longer hard-coded in `RobotHeadView`. `Config > URDF Configuration…` edits per-logical-servo min/max extents and direction, and `URDFconfig.json` in the selected Configuration folder auto-loads when present. The URDF XML joint limits remain descriptive metadata; application configuration is authoritative for preview travel.


## v1.1.7 calibration/camera updates

- URDF visual travel calibration is per physical `RobotControls` child, keyed together with its logical `ServoNames` parent.
- Reversal is inherited from `ServoConfig.json`: real gangs use the per-gang child Direction; single-servo inputs use the servo entry's Reversed flag.
- Neck child calibrations are resolved into head motion mechanically (nod = differential, tilt = common component).
- Microphone origin is shifted 30 mm toward viewer-left / robot-right in the neutral front view.
- Camera yaw is centered on the same X/Y vertical axis as the `NeckTurn` joint.


## v1.1.8 upper-flap carrier and URDF calibration

The upper eye-flap STEP group is split into a rotating flap-side mesh and a tilt-carrier mesh. The rotating side contains the flap plate, flap-mounted HS-85 servo body, mounting bracket, and their mounting screws. The tilt carrier contains the inter-servo Servo Arm/rod/collars (the horn/linkage side) and does not inherit Brow*TopOpen. Both groups still follow Brow*TopTilt. The microphone neutral Y position is -0.083909 m. URDFconfig.json v3 supports a nullable per-child reverseOverride; null inherits Servo Configuration.


## v1.1.9 upper-flap servo-body correction

The HS-85 servo mounted on each upper flap now rotates with the flap and its mounting bracket about the Brow*TopOpen pinion axis. The Servo Arm/rod/collars stay on the tilt carrier, so the horn/linkage side remains fixed relative to the tilt stage while the servo case travels with the flap.


## v1.1.10 head-top STEP assemblies
The MFR, whip antenna, and microphone are now binary-STL runtime meshes generated from their supplied STEP assemblies. Their existing joints remain unchanged and all three use the source STEP appearance `#CAD1EE`.


### EyeMechanism.step gimbal (v1.1.12)
Each eye tube contains one `EyeMechanism.step`. EyePop=0 is the neutral position with the CAD Front Lens 2 mm ahead of the tube face. EyePop=2000 defaults to 89.951 mm forward travel, putting the front surface of the two 3/8-in Delrin actuator balls 1 mm behind the tube front edge. The outer Gimbal Ring rotates for vertical gaze around the horizontal screw axis; the Front Lens/inner lens assembly rotates for horizontal gaze around the perpendicular vertical screw axis. Existing iris/gold-ring/RGB visuals sit inside the CAD Front Lens opening with the iris front 1 mm behind the opening plane.


## v1.3.0 replacement neck source

The neck source is the user-supplied `NeckforURDFColors2.step`, stored in the project as `Models/SourceCAD/URFDNeckAssembly.step`. All runtime neck meshes were regenerated from that STEP. Nested component transforms are accumulated through the XCAF assembly hierarchy before tessellation, and dynamic Fabco body/piston/ball groups remain attached to the existing linkage transforms in `RobotHeadView.cs`.


## v1.5.0 Nose Basket input

`NoseBasket` logical input is `0..100`, defaulting to `0`. The visual calibration remains endpoint-based and may use signed angular extents (default `-45..+45` degrees), so a positive logical range does not require both CAD angles to be positive.


## v1.6.0 eye gimbal and embedded calibration

The calibrated baseline now lives in the URDF as `<servo_animator_calibration>` metadata. An external `URDFconfig.json` remains an optional higher-precedence override.

The EyeMechanism hierarchy is corrected to match the physical gimbal: `Left/RightLensHorizontal` rotates the outer Gimbal Ring and its Gimbal Spacers about URDF Z. `Left/RightLensVertical` is a child joint inside that ring and rotates the Wollensak Raptar lens/iris assembly about local URDF Y through the two Gimbal Spacers.
