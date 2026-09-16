# Animation Editor for Stream Deck XL

## Install and use

1. Open the updated Animation Editor. Leave **File → Enable Codex Connection** checked.
2. Double-click `dist/com.johnny5.animationeditor.streamDeckPlugin` and accept installation in the Elgato Stream Deck app.
3. Find **Animation Editor** in the actions list and drag an action onto an XL key.
4. Click that key in the Stream Deck app and choose its Library item in the settings panel. Press the physical key to run it.

Requires Windows 10 or later and Stream Deck 6.6 or later. Node.js is provided by
Stream Deck; you do not need to install development tools to use the plugin.
Other Stream Deck models with standard keys can use the same actions.

| Action | Configure | What the key does |
| --- | --- | --- |
| Apply Library Pose | Library pose | Interrupts playback and immediately applies the pose |
| Play Library Sequence | Library sequence, optional Loop | Replaces playback with that sequence |
| Choose Pose / Sequence | Which Library to open, optional Loop | Opens the editor's selection window; the chosen item runs when selected |
| Stop Playback | Editor instance | Stops Library and timeline playback; cancels a pending Library picker |

Library playback does not insert commands into the timeline, replace the open
document, move its cursor, or save files. Poses and sequences use the existing
controller Library player, including supported Library audio and RGB commands.
A new pose or sequence replaces the previous performance; holding a key does not
repeat key-down actions. An optional button label overrides the Library filename.

**Pose pictures:** Apply Library Pose keys automatically show the image attached
to the selected pose. The image fits above a two-line caption without cropping
or stretching, while the connection dot remains visible. Missing or unreadable
images use the standard pose icon. Original image files are never changed.
Artwork refreshes within 30 seconds, or immediately with **Refresh connection &
Library**. Requires the updated editor API and plugin 1.0.1 or later.

Plugin 1.0.2 automatically drives both the URDF and Physical Model using the
editor's settings. Physical output requires **Drive HW** enabled and connected
hardware. Disconnected hardware does not block URDF playback. Per-button output
selectors have been removed; older saved button output choices are ignored.
Update the editor along with the plugin to enable this routing.
Plugin 1.0.3 also reports live device connections. The editor's **Stream Deck**
button, above **X-Box**, turns on automatically when connected. Click it to disable
Stream Deck control and stop its current Library playback. Click again to enable.
Manual disable remains in effect through reconnects for the current editor session.
X-Box and Steam buttons follow the same automatic-enable behavior. The connection
light reports device presence separately from whether control is enabled.
Background behavior follows **Config → Focus Control → Stream Deck / Library API**,
which has separate URDF and Physical Model permissions, initially enabled.
The plugin uses the local API, so it does not send keystrokes or take arrow keys
away from other apps. Normal editor dialogs temporarily block output.

Green status dots mean connected; amber means a Library sequence is running;
red means the editor/API is unavailable. A running item also has an amber key
border. Click the key in the Stream Deck app for connection/error details.

With one running editor, leave **Animation Editor instance** on Automatic. If
several editors are open, select the intended process explicitly. A selected
process is never silently replaced by another instance; after restarting it,
refresh and select its replacement or return to Automatic.

**Refresh connection & Library** reloads names after changing Library files.
Saved selections use relative paths, so matching filenames in different folders
stay distinct. Unavailable items remain visible as unavailable until replaced.

## Build and validate

Source is in `src/`; the property inspector and manifest are in
`com.johnny5.animationeditor.sdPlugin/`. Dependencies are locked in `pnpm-lock.yaml`.

```powershell
pnpm install --frozen-lockfile
pnpm run build
pnpm test
pnpm run validate
pnpm run pack
```

The editor build uses the normal `ServoAnimator/ServoAnimator.csproj` build and
its daily version stamp. API integration tests run with:

```powershell
# From X:\Johnny5\Software, after building the editor and plugin:
.\ServoAnimator.Checks\bin\Release\net10.0-windows\ServoAnimator.Checks.exe --editor-api
```

Integration tests create isolated temporary Library files and an unshown editor;
they send native named-pipe requests and simulate Stream Deck registration and
XL key events against the packaged plugin. They do not drive physical hardware.

Elgato references:
- https://docs.elgato.com/streamdeck/sdk/references/manifest/
- https://docs.elgato.com/streamdeck/sdk/references/websocket/plugin/
- https://docs.elgato.com/streamdeck/sdk/references/websocket/ui/
- https://docs.elgato.com/streamdeck/sdk/introduction/distribution/
