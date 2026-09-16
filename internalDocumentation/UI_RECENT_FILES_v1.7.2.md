# Recent Files / Startup Restore — v1.7.2

- `RecentFiles.json` is stored in the active Configuration folder.
- It records the last 10 successfully opened/saved Movie and Sequence JSON files, newest first.
- `File > Open Recent` distinguishes Movie and Sequence entries and disables entries whose files are missing.
- The logical Movie remains the active document while its child sequences are displayed.
- On startup, the last active Movie or standalone Sequence is reopened automatically when the file still exists.
- Starting a new blank sequence clears the startup-reopen target without clearing history.

URDF controls are stacked vertically at the lower-left in this order: Collision Warning, Drive, UnDock/Dock, Recenter Camera. Button padding is half the previous v1.7.1 values.
