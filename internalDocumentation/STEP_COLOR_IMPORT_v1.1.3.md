# STEP color import — v1.1.3

The runtime URDF appearance is now derived from the AP214 presentation styles stored in the supplied STEP assemblies.

## Head STEP colors used

- Steel - Satin: `0.627451 0.627451 0.627451`
- Paint - Metallic (Black): `0.098039 0.098039 0.098039`
- Paint - Enamel Glossy (Grey): `0.701961 0.701961 0.701961`
- Plastic - Translucent Glossy (Blue): `0.188235 0.231373 0.588235`
- Opaque(202,209,238): `0.792157 0.819608 0.933333`
- Silver - Polished: `0.984314 0.980392 0.960784`
- Silver - Satin: `0.984314 0.980392 0.960784`
- Aluminum - Polished: `0.960784 0.960784 0.964706`

The two eye-tube component aliases (`TightTube` and `leftTube`) are assigned the STEP Aluminum - Polished appearance. Components without an explicit STEP style fall back to the STEP Steel - Satin color.

## Neck STEP colors used

- Steel - Satin: `0.627451 0.627451 0.627451`
- Opaque(202,209,238): `0.792157 0.819608 0.933333`
- Silver - Polished: `0.984314 0.980392 0.960784`
- Gold - Polished: `0.956863 0.898039 0.654902`

## Explicit overrides retained

These earlier user-directed colors intentionally override the STEP appearance when there is a conflict:

- Neck bottom Disc: black
- Concave Block: black
- Rubber bellows: black
- Head Detail Plate parts: black
- Fabco cylinder bodies: Gold - Polished
- Lip Light LEDs: dynamic orange/red, because their color is animated from audio amplitude

## STL compatibility

All packaged runtime STL files are binary. The WPF loader also accepts ASCII STL, so a future CAD export will not fail simply because its STL writer chose text format.
