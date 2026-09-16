# URDF Configuration v1.1.7

`URDFconfig.json` stores only visual motion extents. Direction/reversal is inherited from `ServoConfig.json`.

## Per-child rows

Every moving physical child servo has its own row identified by `(servo, control)`. A ganged input such as `FlapsOpen` therefore has four independent rows for its four brow controls. The two neck logical inputs each expose both `NeckTiltLeft` and `NeckTiltRight`, because the same physical pair produces both nod and tilt.

Each row provides:

- Minimum Extent
- Maximum Extent
- Servo Position test slider (`-100..100` or `0..100`; eye pop is normalized to `0..100` for calibration)
- Read-only Direction indicator inherited from Servo Configuration

The test slider affects the URDF preview only and never drives physical hardware.

## Direction inheritance

For logical gangs containing more than one physical child, the URDF preview uses `ServoConfiguration.GangReversed(servo, control)`, exactly matching the Direction shown for that child under the same gang in Servo Configuration. For single-servo inputs, it uses that servo's `ServoConfigEntry.Reversed` flag.

Changing Servo Configuration immediately changes the URDF mapping; there is no duplicate Reverse value saved in `URDFconfig.json`.

## Neck pair

The two physical neck servos are calibrated separately for both neck logical inputs. Their visual values are combined as:

- `NeckNodUp`: differential component `(left - right) / 2`, then mapped to the URDF pitch sign.
- `NeckTiltRight`: common component `(left + right) / 2`.

This preserves the two-servo Fabco linkage while allowing independent endpoint calibration.

## Backward compatibility

Version-1 `URDFconfig.json` files from v1.1.6 contain only a logical servo name. On load, each saved logical range is copied to every physical child of that logical input, then the document is treated as version 2.
