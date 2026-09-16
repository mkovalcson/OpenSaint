# URDF Calibration Visual Editor — v1.11.0

## Rotational bodies (`deg`)

The URDF Configuration screen now represents rotational calibration with a circular angular editor:

- Zero is a fixed horizontal-right reference line.
- Minimum and Maximum are thick radial lines with draggable endpoint handles.
- Normal direction displays Minimum below Zero and Maximum above Zero.
- Reversed direction swaps those display sides.
- Minimum, Zero, and Maximum retain exact editable degree values.
- Zero retains a horizontal slider followed by the Reversed checkbox.
- Servo Position Preview is aligned with the Maximum-value row to the right of the calibration values.

The circular display uses each endpoint's angular offset from the actual numeric ZeroExtent; changing ZeroExtent changes the offsets without rotating the visual zero reference.

## Linear bodies (`mm`)

Linear calibration now uses a single two-handle line:

- Minimum editable value on the left.
- Maximum editable value on the right.
- Both endpoints are draggable on the same track.
- Normal direction enforces ZeroExtent = MinExtent.
- Reversed direction enforces ZeroExtent = MaxExtent, so positive logical travel runs toward the numeric minimum.

MinExtent and MaxExtent remain numerically ordered in the persisted URDF configuration so existing mapping and JSON serialization remain compatible.

## Existing gang behavior retained

Normal gangs still share Minimum/Maximum ranges. Each physical child still has independent direction/zero behavior. FlapsOpen remains the exception with independent physical Min/Max extents for all four Open/Close flap servos, while upper/lower pairs remain ganged for preview movement.
