# Head-top CAD replacement — v1.1.10

The synthetic MFR, whip antenna, and microphone visuals were replaced by the supplied SolidWorks STEP assemblies:

- `MFRC.STEP`
- `Whip Antenna.STEP`
- `Microphone.STEP`

## Coordinate registration

All three source assemblies use the same CAD-to-URDF basis conversion for their generated runtime meshes:

`(X,Y,Z) CAD -> (Z,X,Y) URDF`

This maps CAD +Y to URDF +Z for the vertical MFR/whip assemblies and maps the microphone's CAD +Z long axis to the existing URDF +X microphone axis. The CAD origin remains the local URDF joint origin, preserving the existing motion pivots and neutral mounting locations.

Existing joints/ranges are unchanged:

- `MFR_UpDown`
- `MFR_Rotate`
- `Whip_Antenna_RaiseLower`
- `Whip_Antenna_Rotate`
- `Microphone_RaiseLower`

The microphone therefore also retains the previously calibrated leftward mounting position.

## STEP appearance colors

Each of the three supplied STEP files contains one explicit AP214 `COLOUR_RGB` appearance:

- RGB: `(202, 209, 238)`
- Hex: `#CAD1EE`
- URDF material: `step_opaque_202_209_238`

That encoded CAD appearance is used directly for the three replacement assemblies. No synthetic silver/gold/red colors from the previous primitive head-top models remain on these assemblies.

## Runtime meshes

The WPF preview uses binary STL runtime meshes under `Models/Meshes/HeadTopCAD`. The original STEP files are retained under `Models/SourceCAD`.
