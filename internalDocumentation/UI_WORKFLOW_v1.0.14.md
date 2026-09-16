# Animation Editor & Player v1.0.14

## URDF preview
- Moved the URDF instruction legend to the lower-right corner.
- Added a lower-left Recenter Camera button.
- Recenter Camera sets yaw and pitch to 0 degrees and preserves the current zoom distance.
- Double-click still performs the full camera reset, including default zoom.

## Timeline alignment
- Removed the spline panel's horizontal content inset.
- The waveform and spline Skia drawing surfaces now share the same horizontal origin and width.
- Because both already share ViewStart and PixelsPerSecond, vertical time-grid lines now coincide exactly.
