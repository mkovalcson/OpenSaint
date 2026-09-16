# Codex connection

See **EditorApiExtensions.md** for library pose/sequence insertion, playback,
and URDF collision warning commands.

The editor provides a local, same-Windows-user API. You keep using the editor UI;
Codex reads its current cursor and sequence, then submits a batch of commands.
The batch appears on the timeline immediately and is one Ctrl+Z undo step.

Run the updated editor. **File > Enable Codex Connection** is checked by default;
uncheck it to stop the connection. The connection uses a Windows named pipe,
`Johnny5.AnimationEditor.<process ID>`, not a network port. No plugin installation
or separate server process is required. Codex calls the executable's client mode
through its terminal tool; this is not a registered MCP server. Client mode never
opens an editor window and does not require permission to run unsigned scripts.

## Read current context

From `X:\Johnny5\Software`:

```powershell
./ServoAnimator/bin/Release/net10.0-windows/AnimationEditorPlayer.exe --editor-api
```

If multiple editors are running, specify `--process 1234`. This ID must
identify the editor you intend to change. The client connects only to an existing
instance; it never starts a new editor or loads a different project.

The response contains `ok` and `state`: a context `token`, sequence/movie paths,
cursor time, selected marker times, commands, valid controls and ranges, enabled
splines, playback state, and Live Drive state. Times are seconds and command times
are rounded to milliseconds. Values use the editor's native numeric ranges, not
degrees. Use configuration information or ask the user when physical direction
or a desired angle is ambiguous.

## Apply commands

Read state first. Capture its token and create a request JSON file:

```json
{
  "method": "insert_commands",
  "requestId": "a-new-unique-ID-for-this-edit",
  "expectedToken": "token-from-get_state",
  "description": "Head turn and return",
  "enableSplines": ["NeckTurn"],
  "commands": [
    {"servo": "NeckTurn", "afterSeconds": 0, "value": 0},
    {"servo": "NeckTurn", "afterSeconds": 2, "value": -30},
    {"servo": "NeckTurn", "afterSeconds": 3, "value": -30},
    {"servo": "NeckTurn", "afterSeconds": 5, "value": 0}
  ]
}
```

```powershell
./ServoAnimator/bin/Release/net10.0-windows/AnimationEditorPlayer.exe --editor-api --request ./request.json
```

`afterSeconds` is relative to the cursor captured by the state token. Optional
`control` selects a child listed under that servo; omit it to target the group.
Optional `speed` is `N/C` (default), `Default`, `Slow`, `Fast`, or `Crawl`.
Optional `reason` describes an individual command. `enableSplines` enables the
editor's existing interpolation for those servos; it can also affect existing
points elsewhere on that servo's track. Preview the resulting motion in the UI.

For a request to spline from the cursor to a destination over a duration, include
the servo in `enableSplines` and set the destination command's `afterSeconds` to
that duration. Reuse the current cursor's starting command, or add a start anchor
using the evaluated current position when needed. Submit the start, destination,
and spline setting together so they form one undo step.

All commands are validated before insertion. Conflicting commands at the same
target/millisecond, invalid ranges, stale state, playback, Live Drive, or open
owned windows reject the request without inserting commands. Pause playback,
turn Live Drive off, and close editor dialogs/auxiliary windows before editing.
The API supports numeric servo commands, including child controls. It does not
yet directly insert RGB/audio commands, delete/replace individual commands, save
files, or switch documents. Library items may include RGB/audio commands.

A successful response includes the resulting state. A failure has `ok: false`
and an error. If you moved the cursor or changed the document, Codex must read
state again and reconsider the request. Do not automatically reuse old offsets
in a different sequence. Retries after a timeout must use the **identical request
and requestId**: successful edits are remembered for the lifetime of the editor
process, so an uncertain reply cannot cause duplicate insertion. After a restart,
inspect the document before deciding whether an edit needs to be resubmitted.

## Undo

Use Ctrl+Z in the editor, or send `method: "undo"` with a new `requestId` and a
fresh `expectedToken`. This undoes the latest editor action, including a manual
action if one has occurred since the API edit. Read the current context first.

## Verification

```powershell
dotnet run --project ServoAnimator.Checks/ServoAnimator.Checks.csproj -c Release -- --editor-api
```

The checks instantiate the real editor without showing it or loading a user
project, and verify live insertion, spline settings, undo, stale requests, retry
deduplication, validation, and named-pipe transport.
