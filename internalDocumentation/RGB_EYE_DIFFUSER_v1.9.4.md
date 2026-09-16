# RGB Eye Lighting / Diffuser — v1.9.4

- Front-facing NeoPixel rings retain the Arduino timeline simulation introduced in v1.9.2.
- Rendered front-eye LED emission is boosted without changing physical Arduino command values.
- Four pooled WPF SpotLights per front eye are positioned around the NeoPixel ring and aimed toward the iris center.
- The broad front-eye PointLight is also stronger and has a longer effective range.
- Rear-facing vent-ring lighting is unchanged.
- When a front eye ring is dark, its pupil/backing disk is fully opaque black.
- When lit, the disk becomes a smoky dark translucent diffuser and receives a subtle emissive tint from the average active NeoPixel color.
- This is a URDF/rendering-only change; physical Arduino traffic and RGB command semantics are unchanged.
