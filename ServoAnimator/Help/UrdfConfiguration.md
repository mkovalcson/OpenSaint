# URDF Configuration and Calibration

URDF Configuration controls the visual/model-space calibration. It does not change physical Maestro PWM calibration and does not drive physical hardware by itself.

## Min, Zero and Max

Centered URDF controls use a piecewise mapping:

```text
logical -100 -> Minimum Extent
logical    0 -> Zero Point
logical +100 -> Maximum Extent
```

This allows the zero point to be off-center. A reversed/mirrored servo can therefore have a different zero while remaining mechanically ganged.

Every URDF physical servo has an editable Zero Point slider/value and independent Direction. Normal gangs share Min/Max and test movement. The four flap Open/Close servos retain individual Min/Max/Zero calibration while upper and lower pairs remain coordinated for testing.

## Positive-only controls

Positive-only controls begin at their Zero Point and travel toward the appropriate endpoint according to Direction. The embedded URDF defaults include a 2 mm minimum/zero for both Eye Pop controls.

## Zero Flaps

After setting NoseBody and NoseBasket from the main Grid, Zero Flaps calculates flap zero values intended to make the flap surfaces horizontal for the current nose pose.

## Audio LED Gain

Audio LED Gain ranges from `0.5x` to `2.0x`. It multiplies the audio amplitude before driving the orange lip lights and red/green side-mouth LEDs. `1.0x` preserves the normal response.

## Eye and Vent light intensity

**Eye Light Intensity** and **Vent Light Intensity** independently control only the rendered URDF NeoPixel brightness. Each ranges from `1.0x` to `20.0x`; `1.0x` preserves the v1.9.6 lighting appearance. These multipliers do not change RGBCommand values sent to the Arduino. Higher values also scale WPF light attenuation so the rendered light can continue becoming brighter above RGB saturation, while the configured light range remains fixed to limit spill.

## Embedded defaults and JSON override

Baseline calibration is embedded in the URDF. If `URDFconfig.json` exists in the configuration folder it is layered on top as an override. Save Default writes that override file.

## Window position within the list

During one application session, URDF Configuration remembers its vertical scroll position when the window is closed and restores that position the next time the window is opened.


## Back

**Back** restores every URDF Configuration value to the in-memory values that were present when the configuration window was opened, including Min/Zero/Max calibration, Direction overrides, Audio LED Gain, Eye Light Intensity, and Vent Light Intensity. It updates the URDF preview immediately but does not write `URDFconfig.json`; use **Save Default** if the restored values should be persisted.
