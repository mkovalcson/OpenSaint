# Timeline access and main-window layout recovery — 1.26.2

Built September 16, 2026. Installed in the usual Release directory.

The active Config/EditorLayout.json contained a 1025.14-pixel upper editor row in a 1167-pixel-high window. That fixed row could push the sequence timeline and its divider outside the viewport when restoring or shrinking the window. The row-spanning URDF and Hardware panels also contributed excessive desired height to the automatic header rows, preventing normal recovery by resizing.

The URDF overlay and Hardware panel now occupy viewport-sized Canvas hosts so their contents cannot enlarge those header rows. Upper editor and timeline row bounds are recalculated against the actual parent viewport above the transport/movie controls. Oversized pixel sizes are clamped; pane minima scale down at small supported window sizes. All Hardware controls remain accessible through scrolling. URDF height calculations use the visible viewport rather than oversized row totals.

Config > Reset Layout restores balanced docked columns, a 250-pixel upper panel, a combined timeline, and the default embedded URDF height without changing animation data or the camera. The existing dock/undock choice is preserved.

The editor was closed during recovery. The original saved layout is preserved as Config/EditorLayout.before-recovery-20260916-041555.json. The active layout was repaired to a 250-pixel upper panel and a 1250 × 880 normal window constrained to the current monitor's work area. The user's SplineTop preference, column proportions, camera settings, and animation files were retained.

Validation: Release build succeeded (only the existing vent-center warnings), 51 layout-recovery assertions passed across all timeline modes, controller mapping on/off, large-to-small window transitions, oversized saved pixel panes and reset. The recovered screen was rendered and inspected at 1250 × 800. The 20 controller/safeguard checks also passed. No physical model commands were issued.
