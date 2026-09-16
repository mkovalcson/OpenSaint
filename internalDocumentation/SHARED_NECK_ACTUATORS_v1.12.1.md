# Shared Neck Actuators — v1.12.1

## One physical pair

`NeckNodUp` and `NeckTiltRight` are alternate logical modes of the same physical `NeckTiltLeft` and `NeckTiltRight` servo pair. Hardware routing already uses those same two Maestro controls for both gangs.

## One URDF child state

The URDF preview now mirrors the hardware model. It stores one shared left/right neck child state plus the logical owner. A `NeckNodUp` command takes ownership of that pair until a `NeckTiltRight` command arrives, and vice versa. Ownership is explicit even when the command value is zero.

When `NeckNodUp` owns the pair, its gang-relative child directions are applied and the differential component becomes head pitch. When `NeckTiltRight` owns the same pair, its own gang-relative child directions are applied and the common component becomes head roll. The inactive head axis is returned to zero.

## Editor paths

Grid Live Drive, Edit Commands jogging, timeline cursor preview, shared spline playback, and URDF calibration child testing all use this same ownership/state model.

## Legacy calibration correction

The previous embedded URDF baseline forced `NeckTiltRight / NeckTiltLeft` to Normal while the matching right child inherited the Tilt gang direction. That asymmetric override could cancel roll. The baseline now inherits the configured gang direction for both Tilt children. URDFconfig schema v10 migrates the exact legacy cancellation combination without overwriting other custom direction combinations.
