# Files, Configuration, and Open Recent

## Configuration folder

The application uses an `animatorConfig` folder for persistent configuration when available. On first run it looks for that folder beside the ServoAnimator project/deployment folder before prompting for paths.

Common persistent files include:

- `ServoConfig.json` - physical Maestro/Tic servo configuration override.
- `URDFconfig.json` - optional URDF calibration override.
- `EditorLayout.json` - window, splitter, dock and panel layout.
- `RecentFiles.json` - last active Movie/Sequence and the 10-item Open Recent history.

## Automatic reopen

When the application closes it remembers the last active Movie or standalone Sequence. The next startup automatically reopens it when the file still exists.

Internal sequences selected while a Movie is open do not replace the Movie as the logical last-open document.

## Open Recent

File > Open Recent lists the last 10 Movie/Sequence files with the most recently opened first. Missing files are shown as unavailable rather than causing an error.

## Saving

Save Sequence and Save Movie write to the current source file. The corresponding Save As command chooses a new path. Save Movie falls through to Save Movie As when a new movie has no path yet.


**File > New** clears both the current Sequence and the current Movie workspace, including Movie blocks and Movie metadata.
