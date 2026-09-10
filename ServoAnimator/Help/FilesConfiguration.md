# Files, Configuration, and Open Recent

## Configuration folder

The deployed application uses the `Config` folder beside `AnimationEditorPlayer.exe`. The live development build retains the existing layout and discovers `animatorConfig` beside the `ServoAnimator` project folder. If neither layout is available, the application prompts for a Configuration folder.

Movie, Sequence, exported animation, Audio, and Library selections are limited to the active Configuration folder and its child folders. References saved in JSON are relative to that folder, so the complete configuration can be moved or deployed without rewriting drive-specific paths. Older absolute references inside the active Configuration folder remain readable and are converted when saved.

Common persistent files include:

- `ServoConfig.json` - physical Maestro/Tic servo configuration override.
- `URDFconfig.json` - optional URDF calibration override.
- `EditorLayout.json` - window placement and size, splitter positions, dock and panel layout, and URDF camera orientation/zoom.
- `RecentFiles.json` - last active Movie/Sequence and the 10-item Open Recent history.
- `EditorRecovery.json` - temporary autosave data for unsaved Sequence, Movie, and configuration work. It is removed when everything is saved.

The editor writes recovery data every 30 seconds while anything is unsaved. After an interruption or a close with unsaved work, the next startup offers to restore it. Autosave never overwrites the selected Sequence, Movie, or configuration files.

## Automatic reopen

When the application closes it remembers the last active Movie or standalone Sequence. The next startup automatically reopens it when the file still exists.

Internal sequences selected while a Movie is open do not replace the Movie as the logical last-open document.

## Open Recent

File > Open Recent lists the last 10 Movie/Sequence files with the most recently opened first. Missing files are shown as unavailable rather than causing an error.

## Repairing missing files

When a loaded Sequence or Movie refers to unavailable audio or sequence files, the repair window opens automatically. It is also available from **File > Repair Missing Files**. Use **Locate**, **Search Folder**, **Replace All Matching Paths**, or **Remove Reference**, then click **Apply**.

## Saving

Opening another Sequence, loading another Movie, or starting a new workspace checks for unsaved documents before replacing them. Choose Save, Discard (No), or Cancel; a canceled or failed save leaves the current document available. Closing the editor retains the existing recovery-snapshot behavior.

Save Sequence and Save Movie write to the current source file. The corresponding Save As command chooses a new path within the active Configuration folder. Save Movie falls through to Save Movie As when a new movie has no path yet.

There is no bottom status bar. An asterisk directly after a Sequence or Movie name indicates unsaved changes, including description edits. Unsaved-change prompts remain active.


**File > New** clears both the current Sequence and the current Movie workspace, including Movie blocks and Movie metadata.
