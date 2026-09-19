# Break Spline — 1.27.0

Generated September 17, 2026. Edit Commands now offers a per-command Break Spline
checkbox next to Spline, enabled for supported numeric commands with Spline on.
It hides only the outgoing rendered segment, preserving both editable command
points. Playback and exported interpolation are unchanged. Shared Neck Nod/Tilt
uses its next shared spline point.

The optional JSON field `breakSpline` defaults to false for existing files.
Command cloning and draft comparison preserve break-only edits through saving,
undo, copying, and Library operations. Hidden segments are excluded from line
hit testing. Turning Spline off retains the flag for later use.

Validation: Release build succeeded with the existing vent-center field warnings.
All 17 focused `--break-spline` assertions passed, covering persistence, drafts,
checkbox eligibility, ordinary/shared curve rebuilding, rendered gaps, retained
points, and line hit testing. The command editor image was visually inspected.
No full test suite or physical hardware operation was performed.

Installed executable:
`ServoAnimator/bin/Release/net10.0-windows/AnimationEditorPlayer.exe` (1.27.0).
