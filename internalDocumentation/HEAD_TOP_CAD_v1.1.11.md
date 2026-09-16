# Head-top CAD colors and MFR placement — v1.1.11

The MFR, whip antenna, and microphone runtime meshes are generated from the colored STEP assemblies supplied for this revision. XCAF component colors are preserved as separate binary STL meshes and URDF materials.

## MFR

Source: `MFRCColor.step`

- `#F6F4E9`: Outer Tube, Wideband Dish, Shortband Dish, Upper Tube
- `#97A3DA`: Inner Tube, Feedhorn
- `#404040`: Top Hat
- Neutral MFR mount moved 50 mm lower (URDF Z origin 52.917 mm -> 2.917 mm).

## Whip antenna

Source: `Whip AntennaColor.step`

- `#080B4E`: Skirt
- `#E7C863`: Whip knob
- `#F6F4E9`: upper/lower linkage
- `#97A3DA`: stud, pins, spring, and associated hardware

## Microphone

Source: `MicrophoneColor.step`

- `#E9E9EB`: tube, rear cap, hinges
- `#B5AFA6`: front cap and mesh
- `#97A3DA`: electret microphone, passthrough, spacers

All three assemblies retain the same existing motion joints and servo mappings.
