# Audio LED Gain — v1.6.6

URDF Configuration now includes an **Audio LED Gain** control at the top of the window.

- Range: **0.5x to 2.0x**
- Default: **1.0x**
- The value multiplies normalized audio amplitude before it drives either mouth LED display.
- It affects both the 14 orange Lip Light LEDs and the red/green side-mouth LEDs.
- The amplified value is clamped to 0..1 before LED brightness, emissive halos, and dynamic point lights are calculated.
- The setting is serialized as `audioLedGain` in `URDFconfig.json` when **Save Default** is used.
- Older URDF configuration files that do not contain the field load as 1.0x.
