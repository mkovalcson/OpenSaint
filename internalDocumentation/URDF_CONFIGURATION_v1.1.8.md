# URDF Configuration v1.1.8

Each physical child servo retains independent visual minimum/maximum extents. Direction defaults to the matching Config > Servo Configuration row. Toggling Reverse in URDF Configuration creates a URDF-only `reverseOverride` for that child; Reset Defaults clears overrides and returns to inherited direction.

The URDF Configuration window is modeless. The main editor and both embedded/detached Robot Head views remain interactive while it is open, so the camera may be orbited/zoomed/recentered during calibration. Servo Position test sliders remain visual-only and never drive hardware.

Saved `URDFconfig.json` schema version: 3. Older v1/v2 files load with no reverse overrides and therefore inherit Servo Configuration.
