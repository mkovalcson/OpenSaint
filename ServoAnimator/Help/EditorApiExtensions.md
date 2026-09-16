# Library, playback, and URDF API commands

## Direct Library playback / Stream Deck

`get_library_thumbnail` is read-only: supply `libraryKind: "pose"` and `name`
(prefer the exact relative path from `list_library`). It returns `ok: true` and
`images` with `connected`, `disconnected`, `running`, and `active` PNG data URLs,
or `images: null` when the pose has no usable image. Each image is a 144×144
button background: the pose fits within 120×78 pixels above a clear lower
56-pixel caption area, preserving its proportions. No token is required.
The editor caches rendered artwork by image path, modification time, and size;
the plugin refreshes within 30 seconds or on manual Library refresh. Original
files are unchanged, and unreadable or oversized images fall back to an icon.

`apply_library_pose` applies a named Library pose immediately.
`play_library_sequence` plays a named Library sequence; set `loop: true` to repeat.
Both replace existing Library/timeline playback without modifying the document,
timeline commands, or cursor. Names use the same exact/relative-path resolution
as `list_library`. Invalid names reject before stopping current playback.
Invalid file contents stop playback and return a failed receipt.

`driveUrdf` defaults to true and `drivePhysical` defaults to false. Select either
or both; physical requests require Drive HW enabled and connected hardware.
Alternatively, set `useEditorOutputs: true` to request both destinations using
the editor's current settings. This is the Stream Deck plugin's mode: missing
hardware never rejects URDF playback, and physical output is checked against
Drive HW, connection state, and Focus Control at runtime. This mode overrides
explicit output flags. `get_status.state.capabilities` advertises
`editor_configured_library_outputs` when supported.
Unlike timeline-edit APIs, these direct performance commands may run while Live
Drive is on. Background output follows **Focus Control → Stream Deck / Library API**.
The API takes over from active gamepad control while connected gamepads remain
ready for fresh mapped Library/Stop commands. Actual device disconnects and Drive HW off prevent further
physical output; a running Library sequence's clock/audio can continue.

Read `get_status` for lightweight polling: its `state` contains editor version,
generation date, process ID, `libraryToken`, `transportToken`, hardware status,
and a `library` object with name, kind, running, loop, positionSeconds, selecting,
and current output permissions. `get_state` also includes `libraryToken`.
Direct Library mutations require a fresh `libraryToken` as `expectedToken`
(the full edit token is also accepted) and a unique `requestId`.

```json
{
  "method": "play_library_sequence",
  "name": "Greetings/Wave.json",
  "loop": true,
  "useEditorOutputs": true,
  "requestId": "unique-id-for-this-button-press",
  "expectedToken": "libraryToken-from-get_status"
}
```

Omit `name` to open the corresponding Library picker. The initial receipt is
`awaiting_selection`; poll `get_request` until completed, cancelled, or failed.
A newer direct Library command or Stop cancels pending selection. Changes to the
document or Library root while choosing reject the result. Identical retries
return the same receipt and never create another picker or restart playback.

`stop_library` stops the shared Library player and cancels its picker. Existing
`stop` stops both Library and timeline playback and releases movie arrow capture;
it accepts the `transportToken`. The Stream Deck Stop action uses `stop`.

Plugin 1.0.3 sends `stream_deck_status` every second with a unique
`streamDeckSession` and `enabled` reflecting live device connection events. This
updates presence only, never the user's manual enable/disable choice. Sessions
expire after five seconds without a heartbeat. The reply contains `state` with
`streamDeck.connected` and `streamDeck.enabled`. Plugin commands include the same
session ID; a disabled or disconnected session cannot operate the editor. The
Hardware Stream Deck button gates these plugin commands independently of ordinary
API clients. `stream_deck_connection` is advertised in status capabilities.

The installer and setup guide are in `../StreamDeckXL` relative to the application
source project. The plugin connects directly to the same-user Windows named pipe,
not through keyboard shortcuts, an HTTP server, or a Codex task.

## Movie sequence order and membership

`get_state.movieSequences` lists each entry's zero-based index, path, description,
duration, loop/trigger settings, and selection. Movie changes invalidate the edit
token. All operations require a loaded/saved movie, fresh expectedToken and unique
requestId. Pause playback and turn Live Drive off first.

- `movie_move`: supply `index` and `toIndex` (the final position after the move).
- `movie_remove`: supply `index`. Detaches this occurrence only; never deletes its file.
- `movie_insert`: supply `sequencePath` (absolute or relative to Configuration),
  and optional `index` (insertion boundary, defaults to append).
- `movie_create_sequence`: supply `name` and optional `index`. Creates a blank
  sequence JSON beside the movie and inserts it. Existing files cannot be overwritten.

These operations preserve the open sequence and unsaved command edits. Removing
its movie entry leaves that sequence open but detached. The new movie order is
unsaved until **Save Movie** in the UI. Movie-list changes do not use the sequence
command undo stack; reverse a move or reinsert a removed file to restore it.

These commands use the same compiled client described in EditorApi.md. All
mutations require a unique requestId and expectedToken from get_state.

## Library insertion

Read `{"method":"list_library","libraryKind":"pose"}` (or `sequence`) to list
valid item names and relative paths. Insert by supplying `method: "insert_library"`,
`libraryKind: "pose"` or `"sequence"`, and `name: "Greeting"` (with or without
`.json`). Duplicate basenames require the relative path from list_library.

Omit `name` to open the existing Library Pose/Sequence selection window. The
initial response has `status: "awaiting_selection"`. Query
`{"method":"get_request","requestId":"the-original-request-id"}` to obtain
`completed`, `cancelled`, or `failed`. Selection is anchored to the cursor and
document at the original request. Cancel changes nothing. Retrying the identical
request never opens a second picker.

Poses put every command at the cursor; sequences preserve relative offsets.
Both are one undo step. Conflicts are rejected without opening a conflict dialog.
Library audio is referenced within Configuration rather than copied into the
Project folder; missing audio rejects insertion. Original library files are not
changed. These references are portable with the Configuration folder.

## Playback

Methods: `play`, `pause`, `stop`, and `seek` (with `seconds` as an absolute timeline
time). Play uses the Sequence UI behavior: fresh play begins at zero, paused play
resumes. To play from a selected time, pause, seek, then play. Repeated play while
running does not toggle it off. Movie transport is not included in this version.

For these methods, expectedToken may be the `transportToken` from get_state.
Unlike the edit token it stays valid as playback advances, but changing the active
document invalidates it. API play/seek require Live Drive off. Pause/stop remain
available with Live Drive on. The detached URDF window may stay open.

## Collision warnings

`set_collision_warnings` with `enabled: true` or `false` updates the existing
URDF warning toggle. `get_collisions` reads current status without a token:
enabled, modelAvailable, urdfDriveEnabled, cursorSeconds, current collision pair
IDs, and collision command-marker times observed during playback. This information
also appears in get_state. Enable URDF Drive in the UI to track timeline motion.

An empty pair list when no model is loaded, warnings are disabled, or URDF Drive
is off does not establish collision-free animation. These are the existing URDF
preview diagnostics, not a complete swept-motion or physical-robot safety test.
