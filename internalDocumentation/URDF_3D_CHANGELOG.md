# v1.5.4 Editable URDF calibration extents

- Minimum Extent and Maximum Extent in URDF Range Calibration are now editable numeric fields as well as sliders.
- Typing an extent and pressing Enter/Tab or leaving the field updates the corresponding slider, the shared ganged range, and the URDF preview.
- Dragging an extent slider immediately updates its editable numeric value.
- Existing extent units and clamping/range rules remain unchanged.

# v1.5.3 Ganged URDF range calibration

- URDF Range Calibration now groups logical servos like Servo Configuration.
- Min/Max travel and Servo Position preview are shared across every child in a gang; Direction remains independent per child.
- Existing per-child range files are normalized to one range per logical ServoName.

# v1.5.1 Editor layout persistence and control placement

- Moved Live Drive and Disable Servos beside the top-right hardware status indicators.
- Commands inspector now sits below the spline/audio area and remains outside the URDF vertical overlay stages.
- Movie Description expands upward inside the bottom movie area.
- Editor splitter/window layout is persisted in `EditorLayout.json` under the active Configuration folder.

# v1.5.0 Nose Basket range and embedded URDF layout

- Changed `NoseBasket` logical input to 0..100 with default 0 while retaining configurable signed URDF angular endpoints.
- Added three embedded URDF height states: normal, over the audio timeline, and over both audio+spline timelines.
- Added adjustable grid/URDF width and audio/spline height splitters.

# v1.3.1 Neck Color3 update, mouth-part grey overrides, and upper-left fill light

- Replaced `Models/SourceCAD/URFDNeckAssembly.step` with the supplied `NeckforURDFColor3.step`.
- The new STEP changes the ten fitting leaves previously assigned CAD color `#D89D8B` to the same CAD gold `#E7C863`; the existing validated v1.3.0 mesh placement was preserved and those meshes were merged into the left/right gold color groups.
- `[NECK-COVR-00] Lip Light Box_Standard` and `[NECK-E-COMN-01] Front Slide_V2` are the only two parts in `SimpleMouth/static_737373.stl`; that visual now uses the rear-cover grey `#B3B3B3`.
- Added a second static upper-left/front PointLight in `RobotHeadView` at `(0.45, -0.90, 1.15)` in model coordinates, supplementing the existing upper-left key light.

# v1.3.0 Replacement neck STEP and clean runtime regeneration

- Replaced `Models/SourceCAD/URFDNeckAssembly.step` with the supplied `NeckforURDFColors2.step`.
- Deleted and regenerated the complete `Models/Meshes/NeckAssembly` runtime mesh set from the replacement STEP; no v1.2.6 neck overlay meshes are retained.
- Fixed nested XCAF assembly placement during color splitting by accumulating the outer Fabco assembly transform before tessellating child components. This removes the duplicated/mislocated geometry that made the v1.2.6 neck appear corrupted.
- Preserved the existing Fabco closed-linkage kinematics. The replacement STEP has the same critical left/right cylinder and ball geometry as the prior good source.
- The replacement STEP's explicit colors are retained for hydraulic fittings, including black purge tubes and steel banjo bolts; colorless hardware follows the project's documented steel/satin fallback, with the established black/gold/polished-silver neck overrides retained.

# v1.2.6 Neck recolors, continuous eye gold ring, and head grey update

- Changed the neck hydraulic Fabco body meshes to the same gold material used on the whip antenna top (applies to the modeled body/top-cap/bottom-cap assemblies on both cylinders).
- Re-grouped the neck silver static mesh so the six banjo bolts render as steel while the remaining silver hardware stays silver.
- Added an explicit black air-purge-tube visual derived from the source CAD.
- Rebuilt `iris_gold_continuous.stl` so the outer gold ring now extends inward to the same inner edge as the inner gold section, eliminating the visible two-ring split.
- Replaced the simplified-head `Paint - Enamel Glossy (Grey)` visual on the head shell with the rear-cover grey color.

# v1.2.5 Whip Antenna CAD hinge folding

- Split `Whip AntennaColor.step` into lower and upper runtime groups at the actual `ASME B18.8.2 - 0.0627x0.25` hinge.
- The lower linkage, hinge pin, perforator pin, spring, and lower screw remain on the existing lift/rotate link.
- The upper linkage, threaded stud, blue skirt, and gold knob now live on a derived `Whip_Antenna_Fold` link and remain rigidly connected as they pivot.
- The ASME hinge axis is CAD X, which maps to URDF +Y; its local center is Z = 2.921 mm.
- The rendered flat surface of `[HEAD-A-B-02] Whip Antenna Top` is at head Z = 59.181999 mm. With the existing lift-joint origin at Z = 1.764 mm, automatic fold begins at 54.496999 mm of actual lift, exactly when the hinge center reaches the head top.
- The flat shoulder at the top of the lower linkage is local Z = 0.000 mm, so it reaches the head top at 57.417999 mm of actual lift. The upper assembly ramps linearly from 0° to 90° over that 2.921 mm interval and remains at 90° for further lift.
- Folding is derived from the configured physical lift in millimetres, not from raw servo percentage, so URDF lift calibration/reversal remains authoritative.

# v1.2.4 URDF drive On/Off control

- Added a lower-left **Drive: On / Drive: Off** button immediately beside **Recenter Camera** in every `RobotHeadView`.
- Drive defaults to **On** whenever a URDF preview is created.
- With Drive **Off**, servo, child-servo, timeline pose, RGB eye-color, and mouth LED amplitude updates return before touching WPF 3-D joints, materials, or generated iris meshes. This freezes the 3-D model to reduce continuous GPU update load on less powerful computers.
- Camera orbit, mouse-wheel zoom, double-click reset, and **Recenter Camera** remain available while Drive is Off.
- Physical hardware drive and the rest of the Animation Editor/Player are unaffected; this switch gates only that `RobotHeadView` instance.

# v1.2.3 continuous front-lens gold ring and black inner band

- Replaced the separate segmented flat gold ring and separate `iris_gold_bevel.stl` overlay with one watertight `iris_gold_continuous.stl` mesh per eye.
- The continuous gold profile spans the 24.796 mm inner radius through the 29.9847 mm optical-opening edge, bridges the recessed opening depth, and follows the angled Front Lens surface to the 30.7467 mm flat-face transition.
- Shifted the visible gold surface 0.100 mm forward to prevent the CAD Front Lens from hiding or z-fighting with the gold overlay.
- Added `iris_inner_black_band.stl` from the 22.860 mm iris radius (1.80 in diameter) to the 24.796 mm inner gold radius, making the smaller-diameter band between the blue iris and gold ring black.
- IrisClose dimensions remain unchanged from v1.2.2: -100 = 1.80 in opening, 0 = 0.90 in, +100 = 0.30 in.

# v1.2.2 iris and front-lens gold-ring revision

- Increased both blue iris outer diameters from 1.60 in to 1.80 in.
- Changed the iris opening mapping to 1.80 in at -100, 0.90 in at 0/default,
  and 0.30 in at +100 using the existing piecewise-linear interpolation.
- Increased both fixed `pupil_dynamic` RGB backing circles to 1.80 in.
- Extended the existing flat segmented gold ring outward to the CAD Front Lens
  optical-opening radius of 29.9847 mm while preserving its inner edge.
- Added `iris_gold_bevel.stl`, a thin gold annular bevel that follows the measured
  Front Lens angled surface from 29.9847 mm radius to the 30.7467 mm radius
  where the outer front face becomes flat.
- Preserved RGBCommand color control and the existing URDF iris reversal/extents.

# v1.2.1 iris diameter revision

- Reduced both blue iris outer diameters from 1.95 in to 1.60 in.
- Changed the iris opening mapping to 1.60 in at -100, 0.80 in at 0/default,
  and 0.40 in at +100 using the existing piecewise-linear interpolation.
- Reduced both fixed `pupil_dynamic` RGB backing circles to 1.60 in so they
  remain fully behind the smaller iris at every opening size.
- Preserved RGBCommand color control and the existing URDF iris reversal/extents.

# v1.2.0 iris / RGB eye revision

- Reduced both blue iris outer diameters from 2.25 in to 1.95 in.
- Replaced the opaque blue iris discs in `RobotHeadView` with dynamically
  generated annular-cylinder meshes so the inner opening is a real hole.
- Iris opening diameter now maps to 1.95 in at -100, 1.65 in at 0/default,
  and 0.20 in at +100 using piecewise-linear interpolation.
- Preserved the existing URDF Configuration percentage extents and reversal
  behavior.
- Kept `pupil_dynamic` as a fixed-size 1.95 in RGB backing circle behind each
  iris. `RGBCommand` changes this material color during editing and playback;
  iris motion no longer scales the RGB backing.
- Added visual-name lookup to the WPF URDF scene so dynamic primitive geometry
  can be replaced without changing the joint/link tree.

# v17 changes

- `NeckTurn` now maps the authored `-100..+100` range to `-90..+90` degrees.
- `Whip_Antenna_Rotate` is limited and mapped to `-90..+90` degrees.
- Starting or resuming timeline playback rebuilds spline curves before motion begins.
- The initial playback pose and every playback timer tick update the URDF model.
- For spline-enabled servos with at least two points, the URDF model uses the Cubic-Hermite spline value at the exact timeline time. Other servos use the latest command value.

# URDF 3-D Robot Head changes

This project is based on the uploaded ServoAnimator project.

## Added

- `ServoAnimator/Models/johnny5_head.urdf`
- `ServoAnimator/Models/servo_joint_map.json`
- `ServoAnimator/Models/URDF_HEAD.md`
- `ServoAnimator/RobotHeadView.cs`

## Replaced

- The SkiaSharp 2-D `RobotHeadView` with a native WPF `Viewport3D` URDF
  renderer.
- `RobotHeadWindow.xaml` now opens a larger orbitable 3-D window.

## Behavior changes

- `NeckTurn` is a yaw joint around Z.
- `NeckNodUp` is a pitch joint around Y.
- `NeckTiltRight` is a roll joint around X.
- The complete `head_link` subtree moves through all three joints.
- Eye pop, MFRC rotation, and whip rotation are included in the preview.
- Existing ganged controls and individual child-servo controls retain their
  original names and robot-left/viewer-right mirroring.
- Iris aperture and audio-mouth width remain renderer-specific dynamic scale
  effects because URDF has no general visual scaling joint.

## Files modified

- `ServoAnimator/MainWindow.xaml.cs`
- `ServoAnimator/RobotHeadWindow.xaml`
- `ServoAnimator/RobotHeadWindow.xaml.cs`
- `ServoAnimator/ServoAnimator.csproj`
- `ServoAnimator/README.md`

## Verification

The URDF XML was parsed and its tree was validated: 38 links, 37 joints, one
`base_link` root, no missing links, duplicate parents, unreachable links, or
cycles. A neutral-pose GLB preview was also generated from the URDF primitives.

A Windows/.NET build could not be run in the generation environment because it
does not contain the .NET SDK. Build and run the project on Windows with the
.NET 8 SDK before connecting hardware.

## Geometry revision

- Doubled head depth to 0.36 m while retaining the original face plane.
- Shifted both eye and flap assemblies outward by 0.04 m.
- Added segmented silver thin-wall eye sleeves.
- Moved flaps to the sleeve front plane.
- Increased eye-pop travel from 0.06 m to 0.24 m.
- Changed lens horizontal/vertical motion to move the iris/pupil while leaving each eye sphere fixed.
- Recessed the whip antenna at zero and increased lift travel to 0.25 m.
- Replaced the microphone sphere with a 0.18 m horizontal cylinder.
- Replaced each vent bank with five individually top-hinged rectangular panels driven together.
- Limited `MFR_Rotate` to ±90°.

## Geometry revision 2

- Increased each vent panel height from 18 mm to 27 mm, producing 2 mm overlap at the existing 25 mm spacing.
- Retained mirrored left/right vent axes, URDF mimic joints, and explicit ServoAnimator five-panel bank synchronization.
- Reduced microphone full-scale vertical travel from 36 mm to 21.6 mm.
- Increased nose-body, nose-basket, and proportional inner-slider widths by 30%.
- Re-parented both `FlapTiltUp` pivots to the moving nose basket and located them at its projected top corners while preserving the zero-tilt flap positions.


## Geometry revision 3

- Extended each segmented eye tube from 101 mm to 151.5 mm while retaining the rear face-plane location.
- Increased each eye tube from 24 to 72 silver wall segments.
- Shifted both moving eye assemblies and all four flap hinge assemblies 50.5 mm forward to preserve their prior placement relative to the tube front.
- Reassembled the MFRC rectangle and disc so they touch, aligned the robot-right rectangle edge with the right eye-tube outer edge, and placed the rectangle bottom on the head top at neutral.
- Rebuilt the shell from primitive sections to create an approximately 30 mm segmented robot-right upper-corner radius. The radius tangent begins one 6 mm eye-tube wall thickness beyond the tube outer edge.
- Added a real 12 mm-deep front notch directly above the mouth. The notch is 37.5 mm high, 1.5 times the 25 mm mouth-frame height.
- Reduced both mouth-frame and moving-mouth widths by 20% and placed their top edge at the notch bottom.


## Geometry revision 4

- Restored/retained the original mouth heights: 25 mm outer frame and 22.5 mm moving red panel.
- Moved the complete mouth assembly upward by 25 mm into the lower portion of the 37.5 mm indentation.
- The mouth frame now spans Z = -100 mm to -75 mm, leaving 12.5 mm of visible indentation above it.


## Geometry revision 5

- Added a matching segmented radius to the robot-left upper head edge, producing symmetric rounded top corners.
- Moved the complete MFR lift/rotation assembly from robot-right to robot-left while retaining its eye-edge alignment.
- Added `head_side_gray` (`0.64 0.67 0.70`) for the front, back, side, and rounded shell surfaces.
- Preserved the original `head_metal` color on a separate flat top-surface cap.


## Geometry revision 6

- Extended NoseBody downward so the nose-basket bottom aligns with the eye-tube lowest outer edge at -100.
- Raised the neutral whip-antenna top by one-third of the whip-base height.
- Moved the unchanged-height mouth upward until its top is flush with the recess ceiling.
- Added a 72 mm fixed head-forward offset after the neck roll joint, equal to 20% of the 360 mm head depth.
- Added two gold neck cylinders at 30 degrees from vertical; each is one-third the neck-column diameter and enters halfway into the head at neutral.


## Geometry revision 7

- Moved the bottoms of both gold neck cylinders to the front edge of the dark pedestal base.
- Reoriented the cylinders to splay 35 degrees toward robot left and robot right.
- Removed their fore/aft lean; each cylinder is 90 degrees to the base when viewed from the side.
- Preserved the prior cylinder diameter and top elevation inside the head.


## Geometry revision 8

- Removed the five rectangular side-vent panels from each side of the head.
- Added one curved blue eye-tube shell per side, centered at the 45-degree upper-outboard position.
- Each shell uses 24 primitive box segments over a 60-degree arc and remains controlled by LeftEyeVent or RightEyeVent.
- VentsOpen now drives only the two curved-shell joints; both rotate outward by up to 20 degrees.


## Geometry revision 9

- Moved both curved eye vents to the positions previously reached at control value 100.
- Relocated each vent joint to the curved shell’s inner/top longitudinal edge.
- Positive vent motion now swings the shell away from the eye tube around that hinge by up to 20 degrees.
- Changed the whip antenna base from edge metal to flap blue.
- Replaced the MFR top rectangle with a centered black disk; retained the lower MFR disk in silver.


## Geometry revision 10

- Moved the microphone neutral centerline to 40 mm below the 100 mm head top (Z = 60 mm).
- Lowered the black MFR upper disk so its centerline lies on the head-top plane; the shell hides its lower half and leaves a flat semicircular baseline level with the top.
- Kept the black MFR disk centered directly above the silver lower disk.
- Added a 21.6 mm diameter gold cap to the moving whip-antenna link, 20% wider than the 18 mm antenna shaft.
- Positioned the cap with its lower face on the antenna tip so it follows whip lift and rotation.


## Geometry revision 11

- Raised the neutral microphone centerline to Z = 112 mm so its 12 mm-radius lower edge rests on the Z = 100 mm head top.
- Rotated the black upper MFR disk 90 degrees around its head-top diameter axis, laying it horizontally while retaining the existing MFR rotation control.
- Reduced the black MFR disk thickness from 35 mm to 17.5 mm while preserving its 120 mm diameter.
- Doubled the gold whip-antenna cap height from 6 mm to 12 mm, extending it downward while preserving its previous top surface.



## Geometry revision 12

- Changed all four eye flaps from `flap_blue` to `black`.
- Reduced each flap thickness from 8 mm to 4 mm while preserving its width, length, joint origins, pivots, and motion limits.


## Geometry revision 13

- Moved the complete MFR assembly rearward to X = -90 mm, centered between the head front (+90 mm) and back (-270 mm).
- Reduced the black MFR top-disk height from 17.5 mm to 8.75 mm without changing its diameter.
- Reduced the silver MFR disk depth from 18 mm to 9 mm.
- Added a 6 mm diameter by 36 mm long silver cylinder to the front of the silver MFR disk.
- Extended the blue whip base outward to Y = -270 mm, the start of the nearest rounded top corner.
- Kept each gold neck-cylinder top endpoint fixed, moved its bottom rearward by one radius (18.333 mm), and extended it downward by one diameter (36.667 mm).

## Geometry revision 14

- Added a thin red rectangular rotation marker to the front face of the gold whip-antenna cap.
- The marker is 21.6 mm long, matching the gold cap diameter, 3 mm high, and 1.5 mm thick.
- The marker is part of `whip_rotate_link`, so it follows `Whip_Antenna_Rotate`, whip raising/lowering, and all head/neck motion.

## Geometry revision 15

- Rotated the red whip-antenna rotation marker into a vertical orientation.
- Extended the marker 18 mm forward from the gold cap, equal to the 18 mm whip-antenna diameter, while retaining its 21.6 mm vertical length.
- Added a segmented gold ring around each 54 mm-radius iris with a 1 mm radial gap.
- The eye-tube inner radius is 78 mm, leaving 24 mm between the iris and tube; each gold ring is 8 mm radially thick, one-third of that space.
- Each ring uses 72 primitive box segments and belongs to its pupil link, so it follows eye-pop, horizontal, vertical, and iris motion.

## Geometry revision 16

- Reversed `Whip_Antenna_Rotate` by changing its URDF axis from +Z to -Z.
- Reversed `MFR_Rotate` by changing its URDF axis from +Z to -Z.
- Reworked `FlapTiltUp` so both top-flap controls use the same semantic joint value.
- `FlapTiltUp = +100` places both upper flaps vertically inward toward the nose.
- `FlapTiltUp = -100` places both upper flaps 30 degrees below horizontal.
- Mirrored the right top-flap tilt axis so equal logical values produce symmetrical motion.



## Geometry revision 18

- Reparented both gold neck-cylinder visuals from `base_link` to `neck_yaw_link`.
- Adjusted their local Z coordinates by the 320 mm `NeckTurn` joint offset so their neutral world positions are unchanged.
- The gold cylinders now rotate with `NeckTurn` while remaining independent of neck nod and tilt.


## v19 — Description editor and viewport-aware library markers

- Replaced Animation Name with a sequence Description field.
- Added a one-line collapsed editor and an expanded multiline editor occupying the servo-grid row.
- Sequence JSON now writes `description`; legacy `name` remains load-compatible.
- Create Library Item arrows start inside the current visible viewport.
- Insert Library Sequence starts centered in the current visible viewport.

## v20 — Persistent sequence paths and library browser

- Persisted the directory of the most recently loaded or saved sequence as `lastSequenceFolder` in `Paths.json`.
- Load Sequence and Save Sequence As now reopen in that remembered directory.
- Added a top-level `description` header to Animation Library JSON files while retaining compatibility with older command-only files and legacy `name` fields.
- Replaced the fixed Create Library Item overlay with a movable modeless window containing OK, Cancel, and a ten-line description editor.
- Added a recursive Animation Library selection window listing modification date, relative pathname, description, and Play-command audio filenames.
- Insert Library Sequence now selects the item first, centers the blue marker in the visible timeline, and uses a right-click confirmation that displays the selected description.
- Added Animation Library > Manage Library Items for editing library descriptions without changing the remaining JSON fields.

## ServoAnimator v1.0.0 — Movie Timeline (2026-08-07)
- Added application version 1.0.0 to About Servo Animator and project metadata.
- Added hidden-by-default Movie Timeline beneath the sequence scrollbar.
- Movie blocks are contiguous, show sequence filenames, scale to sequence duration,
  can be reordered by dragging, and expose right-click insert/remove commands.
- Added movie cursor-to-sequence loading with timeline Fit and save/discard/cancel
  protection for unsaved sequence edits.
- Added Movie Play and Next Sequence controls. Movie playback stops after the
  selected sequence rather than automatically chaining to the following block.
- Added File > Load Movie and File > Save Movie As. Movie JSON stores only the
  ordered sequence pathnames and is saved in the configured Projects folder.

## Servo Animator v1.0.1 — Movie timeline layout and hotkeys

- Moved the movie timeline below the Sequence Play transport row.
- Moved the Movie Play/Next/file/status line below the movie timeline.
- Added Up Arrow as the Movie Play/Pause/Resume hotkey.
- Added Right Arrow as the Play Next Sequence hotkey.
- Arrow movie hotkeys are ignored while a text-editing control has keyboard focus.
- Updated application/project version metadata to 1.0.1.

## Servo Animator v1.0.2 — Movie navigation hotkeys

- Added Left Arrow movie navigation.
  - If movie playback is active, it stops and returns to the beginning of the current sequence without resuming playback.
  - If already stopped at a sequence boundary, it loads the previous sequence at its beginning.
  - If stopped within a sequence, it returns to that sequence's beginning.
- Added Down Arrow to stop playback and move to the beginning of the movie (first sequence at t=0).
- Sequence switches triggered by these hotkeys retain the existing save/discard/cancel protection for unsaved edits.
- Updated application/project version metadata to 1.0.2.

## v1.0.4 - STEP eye flap geometry

- Replaced the four box eye-flap visuals with geometry derived from the supplied SolidWorks STEP files.
- Added `Models/SourceCAD/EyeFlaps` containing the source STEP solids.
- Added triangulated runtime OBJ meshes under `Models/Meshes/EyeFlaps`.
- Added Wavefront OBJ support to the lightweight WPF URDF renderer.
- All four STEP parts use one common scale factor (1.7211487244).
- Upper flap outer edges and lower flap-arm outer edges align at Y = +/-0.276 m.
- Upper flap inner edges leave a 0.012 m center gap.
- Upper flaps hinge on the longest horizontal edge of each STEP profile.
- Lower flap arms hinge on the shorter of the two horizontal edges of each STEP profile.

## v1.0.5 - Correct lower eye flap STEP geometry

- Replaced the incorrect lower eye flap arm STEP geometry with `[HEAD-D-LWR-COMN-01-L] Lower Eye Flap (Left).STEP`.
- Generated the robot-right lower flap by mirroring the supplied robot-left lower flap.
- Applied the same common 1.7211487243652173 scale used by the upper eye flaps.
- Used the shortest straight horizontal edge (0.700 in native) as the lower flap hinge.
- Aligned both lower flap outer edges with the upper flap outer edges at Y = +/-0.276 m.
- Upper flap meshes and placement were left unchanged.

## v1.1.1 — URFD head and neck STEP assemblies
- Replaced the v1.1.0 HeadShell STL visual with a runtime STL triangulated from `URFDHeadAssembly.step`.
- Removed the old primitive neck column and paired gold-cylinder visuals.
- Added the complete `URFDNeckAssembly.step` as a STEP-derived runtime STL on `neck_yaw_link`.
- Aligned the neck's two upper ball centers to the actual spherical centers of the head assembly's two ball cups with no scaling.
- Removed the synthetic mouth frame and centered the animated red mouth on the 48 red mouth LEDs in the head CAD assembly.

## v1.1.2 — articulated CAD neck and lip-LED voice display

- Removed the obsolete large pedestal disk below the CAD neck.
- Rebased `NeckTurn` to the exact center of the bottom CAD rotation Disc.
- Rebased nod/tilt to the intersection of the two Solid U-Joint hinge axes.
- Split the neck STEP into independently renderable black base/bellows, gold Fabco assemblies, white Delrin balls and metallic static hardware.
- Added visual Fabco linkage solving so the cylinders swivel about the lower balls while the pistons extend/retract toward the upper balls as the head nods/tilts.
- Centered the existing microphone, MFR and whip-antenna controls over their CAD head-top locations while retaining their established neutral heights.
- Rendered all head Detail Plate components black.
- Removed the synthetic red mouth rectangle.
- Extracted the 14 front Lip Light Box LEDs from the head STEP and use them as the voice-amplitude display, illuminating red from the center pair outward.


## v1.1.3 — STEP appearance colors and STL compatibility

- Converted all runtime CAD STL meshes to binary STL.
- Added ASCII STL fallback parsing in `RobotHeadView`.
- Split static head/neck CAD into STEP presentation-color mesh groups and added exact URDF RGB materials.
- Preserved explicit user color overrides and dynamic Lip Light LED colors.


## v1.1.4 — SimplifiedHead2 articulated head

- `SimplifiedHead2.step` replaces the previous head STEP as the authoritative head geometry.
- Imported STEP colors are retained by material-grouped binary STL meshes.
- Nose body and basket are revolute CAD assemblies using the ASME B18.8.2 spindle and `[HEAD-E-V2-B-02] Pin` respectively.
- Upper and lower eye flap pivots now come from the actual servo/pinion axes in the simplified assembly.
- The old synthetic curved vent controls were removed. Hitec HS-40 output axes and all ten physical vent-fin pivot axes now drive the CAD vent geometry.
- Lip LED idle color changed to light gray (`#D3D3D3`); audio amplitude still lights pairs bright red from the center outward.

## v1.1.5 — SimpleMouth restoration and lighting

- Added `SimpleMouth.step` beneath the simplified head, registered by its two spherical ball cups to the neck's upper Delrin balls.
- Re-extracted the 14 voice-amplitude lip LEDs from `SimpleMouth.step`; idle state remains light gray.
- Preserved STEP-derived mouth assembly colors with a steel/satin fallback for unstyled hardware.
- Unified all left/right vent fins to the same `#080B4E` blue material.
- Added a soft point/key light above-left of the model in the WPF URDF preview.

## v1.1.6
- Added `rearCover.STEP`, mounted to the head back datum and extending rearward.
- Added persistent `URDFconfig.json` visual motion calibration.
- Visual range endpoints and reversal now supersede static URDF `<limit>` clamping in the WPF preview.


## v1.1.8
- Microphone moved 20 mm farther viewer-left.
- Upper eye-flap plates separated from their tilt-carrier hardware so FlapsOpen no longer rotates the inter-servo linkage or open/close servo body.
- URDF per-child Reverse may override inherited Servo Configuration direction.
- URDF Configuration window changed to modeless for simultaneous 3-D camera/model inspection.


## v1.1.9
- Corrected the upper eye-flap split so each flap-mounted HS-85 servo body and mounting bracket rotate with the flap.
- Kept the inter-servo Servo Arm/rod/collars on the tilt carrier, so the horn/linkage side remains attached but does not rotate with BrowLeftTopOpen/BrowRightTopOpen.


## v1.1.10 — STEP head-top assemblies
- Replaced the synthetic MFR with `MFRC.STEP`.
- Replaced the synthetic whip antenna/base visuals with `Whip Antenna.STEP`.
- Replaced the synthetic microphone cylinder with `Microphone.STEP`.
- Existing MFR, whip, and microphone motion joints/ranges remain unchanged.
- Imported the STEP-encoded `#CAD1EE` appearance for all three assemblies.


## v1.1.11 — Colored head-top STEP assemblies
- Regenerated MFR, whip antenna, and microphone from the colored STEP exports, splitting runtime STL meshes by STEP appearance.
- Added the per-part STEP colors to the URDF instead of the prior single `#CAD1EE` material.
- Lowered the MFR neutral mounting origin by 50 mm while preserving its existing raise/lower and rotate controls.


## v1.1.12 — Eye mechanism CAD + live URDF config reload
- Imported `EyeMechanism.step` once per eye tube, parented to each EyePop link.
- CAD alignment: tube front X = 185.250 mm; Front Lens front X = 187.250 mm (2 mm protrusion).
- Front Lens opening radius = 29.9847 mm; iris front is 1 mm behind its front opening plane.
- Rear cover material changed to STEP `Paint - Enamel Glossy (Grey)` (`#B3B3B3`).
- `URDFconfig.json` is now reapplied immediately after Save Default and live-reloaded after external file saves.

- CAD two-axis eye gimbal: vertical gaze rotates the outer Gimbal Ring on its horizontal screw axis; horizontal gaze rotates the inner Front Lens on its vertical screw axis.
- EyePop 0 is the CAD-neutral/retracted position; 2000 defaults to 89.951 mm, leaving the front surface of the actuator Delrin balls 1 mm behind the eye-tube front edge.

## v1.6.5
- Split the Simple Mouth red/green side LED CAD color meshes into 72 independently addressable LED visuals (48 red, 24 green) without changing their geometry.
- Side-mouth audio meter now fills through 12 physical stations from front to rear as amplitude increases.
- Added dull inactive red/green lens colors, bright emissive active colors, per-LED translucent halos, and four localized red/green point lights whose weighted positions move rearward with the active stations.
- Existing 14 orange Lip Light Box voice LEDs remain center-out and unchanged.

## v1.6.7 - Eye lens circumference materials
- Changed only the outermost cylindrical circumference surface of `[HEAD-B-A-B-00] Front Lens_Modified` to black.
- Changed only the outermost cylindrical circumference surface of `[HEAD-B-A-A-00] Barrel_Modified` to black.
- Preserved the remaining front/back/angled surfaces with their prior materials.
- Preserved full unsplit lens meshes for collision geometry so the material-only change does not change collision diagnostics.


## v1.6.10
- Recolored the four arrow-identified gray fitting bodies next to the SFT-10 tees to `neck_gold`; SFT-10 banjo bolts remain gray.

## v1.9.4 — Brighter front-eye NeoPixel lighting and diffuser
- Increased rendered brightness of the front-facing 16-LED NeoPixel rings without changing physical Arduino command values.
- Added four pooled SpotLights per front eye aimed from the ring quadrants toward the iris center.
- Strengthened the broad front-eye PointLight; rear vent-ring illumination remains unchanged.
- Front pupil/backing disks are opaque black when dark and become smoky translucent diffusion disks while their eye ring is lit.
- Lit diffusion disks receive a subtle averaged emissive tint to visually blend the individual NeoPixel colors.

## v1.9.5 — NeoPixel hue normalization compile fix
- Added the missing `NormalizeHue(Color)` helper used by the brighter front-eye NeoPixel renderer.
- Hue normalization preserves RGB channel ratios while scaling the strongest channel to full intensity; Arduino brightness remains unchanged.
- No URDF geometry, RGB command parsing, physical Arduino behavior, collision behavior, or Live Drive behavior changed.
## v1.10.5 — Screen-anchored neck-base framing
- Replaced the fixed URDF camera-target height with a screen-space neck-base anchor.
- The physical bottom of the neck now remains just above the bottom-left URDF button stack during mouse-wheel zooming.
- Window, docked-pane, and detached-window resizing recalculate the camera target so the neck base does not drift vertically.
- Camera yaw, pitch, Recenter, ±90° turns, and existing zoom-distance limits are preserved.



## v1.10.6 — Fixed 35-pixel neck-base viewport anchor
- Changed the neck-base screen anchor to exactly 35 pixels above the bottom edge of the URDF viewport.
- The anchor no longer depends on the bottom-left button stack height or margin.
- The same viewport-relative anchor is used in docked and undocked modes and is recalculated during zooming and resizing.

## v1.11.0 — Visual URDF calibration controls
- Replaced degree-based Min/Max sliders with a circular angular calibration dial.
- Zero is always drawn horizontal-right; normal Min is below and Max above, with sides swapped when Reversed.
- Added draggable radial Min/Max handles while retaining exact editable degree values and the Zero slider.
- Moved Servo Position Preview beside the Maximum-value level for rotational controls.
- Replaced millimetre calibration with a single two-handle linear track and left/right editable endpoint values.
- Linear Zero is now derived from direction: Zero = Minimum normally and Zero = Maximum when Reversed.
- Preserved shared gang ranges and independent FlapsOpen physical ranges.


## v1.12.0 — Shared neck spline ownership + NeckTiltRight URDF drive
- NeckNodUp and NeckTiltRight now form one mutually-exclusive time-ordered neck control stream in the editor and URDF preview.
- When spline-enabled they share one Cubic Hermite curve; each segment uses the color of the neck control that currently owns the physical pair.
- Ownership changes exactly at NeckNodUp/NeckTiltRight control points and remains active until the next command of either neck type.
- New shared-neck spline points inherit the preceding point's owner; middle-clicking an already-selected neck point toggles it between NeckNodUp and NeckTiltRight.
- Double-left-clicking any spline line now adds a control point, equivalent to Ctrl+left-click.
- Shared-neck animation export emits generated samples under the active owner so hardware playback follows the same handoff semantics.
- URDF preview now resolves the neck from this same shared ownership stream, ensuring NeckTiltRight drives the model when it owns the neck.


## v1.12.1 — Shared neck child actuators
- NeckNodUp and NeckTiltRight now take turns owning one shared `NeckTiltLeft` / `NeckTiltRight` child-actuator state in the URDF preview instead of maintaining separate Nod and Tilt child-state buckets.
- Grid jogging, Edit Commands jogging, timeline playback preview, spline preview, and URDF calibration child tests all route through that same shared pair.
- A zero-valued neck command still transfers ownership, so subsequent preview refreshes preserve which logical neck mode is active.
- The active owner determines how the shared pair is interpreted mechanically: NeckNodUp drives pitch from the differential component; NeckTiltRight drives roll from the common component.
- Corrected the embedded `NeckTiltRight/NeckTiltLeft` URDF direction override from forced Normal to `inherit`, preventing the two Tilt child mappings from cancelling each other.
- URDFconfig schema v10 migrates only the exact legacy cancellation combination to inherited direction.


## v1.13.0 — Expanded Pose accessory controls and dial layout
- Enlarged the editable NeckTurn dial by 50%, moved it 75% toward the lower-right legend, and added a center reset button.
- Moved Eye Pop controls to a bottom-front head anchor.
- Restored Whip Up/Down, MFRC Up/Down, and Microphone Up/Down as top-pinned Pose sliders with model-space horizontal anchors.
- Added an editable Whip rotation dial immediately outside the Whip slider.
- Kept Nose Body/Basket fixed through LR Split and centered the split flap Open/Close controls side-by-side.
- Repositioned slider reset buttons outward from the model.

## v1.12.8 — Pose slider reset controls + head-centered face sliders
- Re-anchored NoseBody, NoseBasket, and Flap Open/Close pose sliders to the robot-head frame at the face vertical midline instead of the moving NoseBody.
- Added a compact per-control reset button beside every Pose slider and the Vent arc slider.
- Added a Face Reset button immediately to the right of Pose while Pose mode is active; it neutralizes face controls without changing NeckTurn, NeckNod, or NeckTilt.

## v1.13.1 — Split vent posing, calibrated ranges, MFRC dial, and Pose RGB
- Eye Pop moved 10 mm outside the head sides horizontally.
- LR Split mirrors the Vent quarter-arc onto the opposite eye and controls vents independently.
- Pose control endpoints are explicitly governed by URDF calibration mapping.
- Added MFRC rotation dial to the right of MFRC Up/Down.
- Added Pose RGB Command text + Build workflow with immediate preview/Live Drive.
- Insert Pose now includes optional RGB at the same keyframe.


## v1.13.2 — Pose control alignment and mirrored Vent fix
- Increased the Pose RGB Command field vertical height by 25%.
- Fixed the LR-Split robot-right Vent quarter-arc drag at the +180°/-180° angle boundary.
- Moved the microphone URDF mount 7 mm rearward and 3 mm downward.
- Moved Eye Pop sliders to 15 mm outside the head and vertically aligned their bottoms to the projected top of the mouth.


## v1.13.3 — Library Poses, outer-tube Vent arcs, and eye-gimbal reset
- Added Pose-mode Library + / Library Load controls above Collision Warning.
- Updated all user-facing single-time-point library terminology to Library Pose.
- Library + serializes the complete current mechanical/RGB pose; Library Load applies a saved pose directly to the URDF editor.
- Moved the microphone mount another 4 mm rearward.
- Anchored Vent arcs to projected 3-D points on the physical outer eye-tube edge.
- Added inner-facing eye-gimbal reset buttons for Joined and LR Split operation.

## v1.13.4 — Face Reset clears RGB
- Face Reset now clears the Pose RGB Command text/state in addition to neutralizing the facial mechanisms.
- Clearing RGB through Face Reset applies Arduino `ClearAll` to the URDF RGB simulator and, when Live Drive hardware is connected, to the physical RGB controller.
- The Pose RGB field remains blank after reset; `ClearAll` is executed as reset behavior rather than stored as the pose command.

## v1.13.5 — Automatic Library Pose URDF image
- **Library +** now automatically captures a PNG when no custom image is attached.
- Capture uses the current mechanical pose with a clean straight-on fitted camera and temporarily hides Pose/editor chrome.
- The image is cropped around the robot head, flaps, and neck; tall Whip/MFRC/Microphone accessories are excluded from the framing calculation.
- The user's camera/orbit/zoom and Pose UI are restored immediately after capture.
- A custom image attached in the save dialog still takes precedence and suppresses automatic capture.

## v1.14.0 — Full project with calibrated model defaults
- Regenerated the complete Animation Editor project from the rollback-based v1.13.5 baseline.
- Replaced the project URDF with the latest calibrated Johnny 5 model.
- Embedded MFR Up/Down defaults of 22.4299065420561–65.1053864168618 mm with zero at 22.4299065420561 mm.
- Embedded Microphone Raise/Lower defaults of 0–10 mm with zero at 0 mm.
- Updated the corresponding standard URDF prismatic joint limits as well as ServoAnimator calibration metadata.
- Preserved the complete ServoAnimator/Models tree, including all STEP/source CAD, meshes, manifests, pivots, and reference assets for future part-name/geometry lookup.

