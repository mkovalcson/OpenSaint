# URDF Configuration and Calibration

URDF Configuration controls the visual/model-space calibration. It does not change physical Maestro PWM calibration and does not drive physical hardware by itself.

## Rotational calibration in degrees

Degree-based rotating bodies use a circular calibration control.

- The outlined circle represents the angular calibration reference.
- The **Zero** line is always horizontal and points to the right. Its on-screen direction does not rotate when the numeric Zero value changes.
- **Maximum** is shown above the Zero line in normal direction.
- **Minimum** is shown below the Zero line in normal direction.
- Checking **Reversed** swaps those displayed sides: Minimum appears above Zero and Maximum below Zero.
- Minimum and Maximum have draggable endpoint handles. Drag a handle around the circle to change that angular extent relative to Zero.
- The exact Minimum, Zero and Maximum degree values remain editable as text.
- Zero also retains an ordinary slider for fine adjustment.
- Servo Position Preview is displayed to the right of the Maximum-value row and moves only the URDF preview.

The circular lines visualize offsets from the calibrated Zero point. The numeric values remain the actual URDF angles.

Centered URDF controls continue to use the piecewise mapping:

```text
logical -100 -> Minimum Extent
logical    0 -> Zero Point
logical +100 -> Maximum Extent
```

This allows the physical Zero value to be off-center while the visual Zero reference remains horizontal.

## Linear calibration in millimetres

Millimetre-based motion uses one horizontal line with two draggable endpoint handles.

- **Minimum** has an editable value on the left.
- **Maximum** has an editable value on the right.
- Both endpoints can also be changed by dragging their handles on the same line.
- In normal direction, **Zero = Minimum**.
- When **Reversed** is checked, **Zero = Maximum** and positive logical travel runs back toward Minimum.
- Zero is therefore derived for linear motion rather than being a third independent calibration point.

The stored numeric extents remain ordered low-to-high. Reversed changes which endpoint acts as logical zero and the direction of travel; this preserves the existing URDF mapping and JSON format.

## Shared and individual ranges

Every physical URDF servo retains its own Direction and Zero behavior. Normal gangs share Minimum/Maximum extents and Servo Position test movement. The four flap Open/Close servos retain independent Minimum/Maximum calibration while their upper and lower pairs remain coordinated for test movement.

## Positive-only controls

Positive-only controls begin at their Zero point and travel toward the appropriate endpoint according to Direction. For linear positive-only controls, normal direction begins at Minimum and reversed direction begins at Maximum. The embedded URDF defaults include a 2 mm minimum/zero for both Eye Pop controls before reversal is applied.

## Zero Flaps

After setting NoseBody and NoseBasket from the main Grid, Zero Flaps calculates flap zero values intended to make the flap surfaces horizontal for the current nose pose.

## Audio LED Gain

Audio LED Gain ranges from `0.5x` to `2.0x`. It multiplies the audio amplitude before driving the orange lip lights and red/green side-mouth LEDs. `1.0x` preserves the normal response.

## Eye and Vent light intensity

**Eye Light Intensity** and **Vent Light Intensity** independently control only the rendered URDF NeoPixel brightness. Each ranges from `1.0x` to `20.0x`; `1.0x` preserves the calibrated lighting appearance. These multipliers do not change RGBCommand values sent to the Arduino. Higher values also scale WPF light attenuation so the rendered light can continue becoming brighter above RGB saturation, while the configured light range remains fixed to limit spill.

## Embedded defaults and JSON override

Baseline calibration is embedded in the URDF. If `URDFconfig.json` exists in the configuration folder it is layered on top as an override. Save Default writes that override file.

## Window position within the list

During one application session, URDF Configuration remembers its vertical scroll position when the window is closed and restores that position the next time the window is opened.

## Back

**Back** restores every URDF Configuration value to the in-memory values that were present when the configuration window was opened, including Min/Zero/Max calibration, Direction overrides, Audio LED Gain, Eye Light Intensity, and Vent Light Intensity. It updates the URDF preview immediately but does not write `URDFconfig.json`; use **Save Default** if the restored values should be persisted.
