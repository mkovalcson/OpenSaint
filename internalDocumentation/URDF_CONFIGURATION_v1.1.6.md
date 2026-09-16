# URDF Configuration — v1.1.6

`Config > URDF Configuration…` calibrates **visual URDF travel only**. It does not modify physical servo PWM settings and its test sliders never send hardware commands.

Each logical visual servo row contains:

1. **Minimum Extent** slider — lower CAD/URDF travel endpoint.
2. **Maximum Extent** slider — upper CAD/URDF travel endpoint.
3. **Servo Position** slider — live URDF-only preview input (`-100..100` or `0..100`).
4. **Reverse** — swaps the input direction between the configured endpoints.

Centered inputs preserve input `0` as the imported CAD neutral. This allows asymmetric travel such as FlapTiltUp `-30° .. +90°` without moving neutral. Positive inputs map linearly from their minimum to maximum extent. Iris uses a full linear `-100..100` mapping because its extents are aperture percentages. Eye-pop animation values remain `0..2000`; the calibration screen intentionally normalizes the test slider to `0..100`.

**Save Default** writes `URDFconfig.json` to the Configuration folder selected under `Config > Set Paths…`. That file is automatically loaded at application startup and after the Configuration folder changes. If the file does not exist, built-in defaults matching v1.1.5 behavior are used.
