# Shared Neck Spline — v1.12.0

## Timeline ownership

`NeckNodUp` and `NeckTiltRight` operate the same physical neck servo pair. Their ganged timeline commands are treated as one ordered stream. The most recent neck command at or before the cursor owns the pair until the next command of either type. Exact-time ties retain the established `NeckTiltRight` precedence.

## Spline editing

When either neck Spline checkbox is enabled, both are enabled and one Cubic Hermite curve is drawn from all ganged NeckNodUp/NeckTiltRight control points. The curve changes color at ownership transitions. A new point added with Ctrl+left-click or double-left-click inherits the owner of the preceding point and uses the curve value at the insertion time. Select a shared-neck point and middle-click it to toggle its owner without changing its time or value.

## URDF and export

The URDF preview evaluates the same shared neck curve and ownership stream, so only the active axis contributes at a given timeline position. Generated animation samples also carry the active owner, preserving the same Nod/Tilt handoff during exported playback.
