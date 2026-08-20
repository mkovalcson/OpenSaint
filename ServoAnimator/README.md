# Animation Editor & Player (WPF + SkiaSharp + NAudio)

A resizable Windows application for editing servo animation timelines against
an audio waveform, reading and writing JSON files in the `animation.json`
format.

## Build & run

Requires the .NET 10 SDK on Windows (WPF is Windows-only).

```
cd ServoAnimator
dotnet build
dotnet run
```

Or open the folder / `.csproj` in Visual Studio 2022 and press F5. NuGet
restores `NAudio` and `SkiaSharp.Views.WPF` automatically.

The entry point is explicit (`Program.Main` in `Program.cs`, selected via
`<StartupObject>` in the .csproj), so the app builds even in setups where
App.xaml's build action isn't detected as "ApplicationDefinition" — the
cause of the "Program does not contain a static 'Main' method" error. If
you import these files into your own project instead of using this
.csproj, either keep Program.cs + set the StartupObject, or set App.xaml's
Build Action to ApplicationDefinition.




## v1.0.10 top editor layout

The top editor is now a 50/50 split: all servo groups are stacked in the left pane and the live URDF Robot Head is embedded in the right pane. The optional View > Robot Head command opens a detached synchronized copy.

## v1.0.9 layout, paths, and playback controls

- Servo grid is arranged as paired sections: Eye Flaps | Eyes, Nose | Neck, Eye Pop | Lighting & Vents, and Headtop Controls on the lower right.
- MFR, Whip Antenna, and Microphone are combined under one Headtop Controls header, collapsed by default, with thin internal separators.
- Servo group headers are vertically compact.
- Expanded Sequence and Movie descriptions use half the servo-grid height and never show a horizontal scrollbar.
- Set Paths asks only for Configuration; Projects is always Configuration\Projects.
- Popup/menu command text is darkened for readability while top-level menu labels retain the theme foreground.
- Sequence transport includes a 0–100% audio playback volume control that applies to primary and inserted audio clips.

## v1.0.8 polish and color themes

* Corrected the hardware label to **Left Tic 249**.
* Menu commands no longer have visual dividers between individual items; explicit separators remain only between command groups.
* Replaced stock square WPF chrome with shared rounded templates: 6 px controls, 8 px panels, consistent padding and spacing.
* Added **View > Color Theme** with Graphite (default), Steel Blue, Teal, and Violet. The selected theme is stored per Windows user in LocalAppData and restored at startup.
* Theme colors propagate through dialogs and the custom waveform, spline, and movie timeline surfaces.

## v1.0.7 UI and workflow improvements

Sequence and Movie work areas now use distinct accents and explicit labels. The
servo grid has collapsible functional groups, filenames show `*` when Sequence
or Movie changes are unsaved, Commands at Cursor supports direct add/edit/delete,
and waveform markers show command summaries on hover. The spline area displays
exact servo/time/value feedback while hovering or selecting points.

The Movie timeline now has independent zoom, horizontal scrolling, middle-drag
panning, visible drag grips and a gold insertion line during reordering. Hovering
a movie block shows its duration, description, audio files and full pathname.
The Animation Library browser includes live search by pathname, description or
audio filename. A Controls & Hotkeys window documents keyboard and mouse
shortcuts, and successful operations use a transient status line instead of
extra confirmation popups.

## Menus

The menu bar is organized by workflow:

* **File**: New · Open Audio… · Load/Save Sequence · Load/Save Movie · Export Animation JSON… · Exit
* **Edit**: Undo (Ctrl+Z) · Redo (Ctrl+Y) · Clear Timeline…
* **Animation Library**: Create Library Item… · Insert Library Sequence… · Manage Library Items…
* **Tools**: Servo Configuration… · Set Paths…
* **View**: Robot Head · Movie Timeline
* **Help**: Controls & Hotkeys… · About Animation Editor & Player

The top options bar contains a **Description** field instead of an animation
name. It is one line high during normal editing. Click **Expand** to replace the
servo grid with a full-size multiline description editor; **Collapse** returns
to the grid. Sequence JSON files store this text in the top-level
`"description"` field. Older files containing `"name"` still load, but new
saves no longer write `"name"`.

### Project files vs animation exports

**Save Project** writes a project JSON containing the audio file **pathname**
(`audioFilePath`), the spline **sample frequency**, and all the data points
shown on the waveform — the command control points — but **NOT** the
interpolated spline values. Loading a project restores the audio (warning if
the file has moved), sample rate, spline checkboxes, audio offset and all
commands. The project filename shows in the title bar.

**Export Animation JSON** is the playback-ready file: control points PLUS the
spline-sampled commands, as before. Exporting never touches the in-memory
timeline or the project path.

### Undo / Redo

Every timeline mutation (insert, edit, delete, paste, clear, library insert,
spline point add/delete, and point drags — one step per drag) pushes an undo
snapshot. Ctrl+Z undoes, Ctrl+Y (or Edit > Redo) puts the last change back.
Up to 100 steps are kept; a new edit clears the redo history.

### Animation Library

**Create Library Item…** shows a **green start arrow** and a **red end
arrow** pointing down at the top of the timeline. A separate movable window
contains OK, Cancel, and a ten-line description editor prefilled from the
current sequence description. Because the window is modeless, both arrows
remain draggable with the left mouse button. OK saves the description as a
top-level JSON header and writes the selected commands with offsets rebased so
the green arrow is 0 seconds.

**Insert Library Sequence…** first opens a recursive browser of every JSON file
under `Library\Animation`. Rows are alphabetized by relative pathname and show
the modification date, pathname, description, and comma-separated audio files
from Play commands. Selecting an item displays a **blue arrow** centered in the
currently visible timeline. Move it with the left mouse button, then right-click
the timeline to review the selected description and confirm insertion.

**Manage Library Items…** opens the same recursive browser in edit mode. Select
an item, edit its description in the ten-line field, and save it back into the
JSON header without changing its commands.

## Feature guide

| Action | How |
|---|---|
| Load audio (mp3/wav/...) | **Open Audio…** — waveform appears, playback enabled |
| Play / pause / resume | **▶ Play** plays the file selected with **Open Audio…** (the one whose waveform is showing) from the **beginning of the timeline** (t = 0), firing `PlayBackServoValues()` at every command offset; **❚❚ Pause** pauses; **▶ Resume** continues from the pause point (or from a position clicked while paused) |
| Offset the audio start | Drag the small green **handle at the top-left of the waveform** to shift the whole waveform. **All existing command time offsets shift with the audio** (one undo step), so commands stay aligned; '+' markers and spline curves follow live. Commands can then be placed on the timeline *before* the audio starts; during Play the cursor runs (and commands fire) through this pre-roll region, and the audio starts exactly at the offset. The offset is saved as `audioStartOffsetSeconds` in the JSON (files without it load with offset 0) |

### Value ranges per servo

| Servo | Value |
|---|---|
| `LeftEyePop`, `RightEyePop` | numeric, **0 … 2000** |
| `MFR_UpDown`, `Whip_Antenna_RaiseLower`, `VentsOpen`, `Microphone_RaiseLower` | numeric, **0 … 100** |
| `RGBCommand` | **text string** (stored as a JSON string in `"value"`); the grid row shows the last command text in a text field |
| all others | numeric, **-100 … +100** |

Sliders in the grid and the command editor automatically take the right range,
and the `RGBCommand` row shows the last command text used (editable in Live
Drive / the editor) instead of a slider. The JSON `"value"` field is read
polymorphically — number or string — via a custom converter in `Models.cs`.

The servo grid is laid out as **two columns** with divider lines between
functional groups (flaps / eyes / neck / nose-basket on the left; nose-body +
RGB + vents / eye pops / microphone / antenna / MFR on the right). The order
and group breaks live in `BuildServoGridColumns()` in `MainWindow.xaml.cs` and
are easy to rearrange. The separate numeric "Type" entry column has been
removed — the slider is the value editor for numeric servos.

| Action | How |
|---|---|
| Zoom the time axis | Mouse wheel over the waveform (zooms at the mouse), or Zoom −/+/Fit buttons |
| Pan / scroll | Middle-button drag, or the horizontal scrollbar |
| Drag a command group | **Press a `+` marker in the top lane and drag** left/right to move every command at that time to a new offset (one undo step; positions occupied by another `+` are skipped so groups never merge silently). A press-and-release without moving is a normal marker select |
| Move the cursor | **Left-click** the waveform (snaps to a nearby `+` marker); the servo grid updates to the last value of each servo at that time, and all commands at that point are listed at the bottom |
| Edit / insert / delete / copy / paste commands | **Right-click** the waveform — the menu operates at the last selected cursor position |
| Insert commands from another JSON | Right-click → *Insert commands from JSON file* — each command's `offsetSeconds` is incremented by the cursor offset |
| Keyframe all servos | Right-click → *Generate commands from grid values* |
| Clear everything | **Clear** button — asks "Are you sure?" (Yes/No) first |
| Live Drive | Toggle **Live Drive** On — the grid sliders/value boxes become editable and every change calls `MoveServoNow()`; the **RGBCommand text field is editable too** (commit with Enter or by leaving the field) and calls the text overload. While Live Drive is On, grid values **accumulate**: cursor clicks and playback ticks don't overwrite your manual settings, so you can dial in several servos and then right-click → *Generate commands from grid values* to keyframe them all. Turning Live Drive Off resyncs the grid to the cursor |
| Resize grid vs waveform | Drag the **horizontal splitter handle** on the boundary between the servo grid and the waveform to give the grid more height (more rows visible) and shrink the waveform, or vice versa |
| Save / load | **Save JSON / Save JSON As… / Open JSON** — after saving, the filename shows in the window title bar; loading re-places all `+` markers |

`+` markers appear at every unique command offset and disappear automatically
when the last command at that offset is deleted.

## Splines

Each numeric servo row in the grid has a **Spline** checkbox (leftmost
column; `RGBCommand` has none). Checking it:

* still adds/edits that servo's point commands on the timeline exactly like
  any other servo, but
* interprets those points as control points of a **Cubic Hermite spline**
  (Catmull-Rom finite-difference tangents), and
* draws the interpolated curve in a **spline area below the waveform**, one
  color per servo, with control-point dots. The area stays time-synchronized
  with the waveform (zoom, Fit, scroll, cursor); scrolling/zooming/clicking
  in either strip drives both. Each curve is normalized to its servo's own
  value range so -100..100, 0..100 and 0..2000 servos are all readable.

**Editing points directly on the spline:** left-drag a control-point dot up
or down to change its value (clamped to the servo's range; the curve and the
grid follow live). Right-drag a dot left or right to move its time offset —
the '+' marker on the waveform follows, and moves that would collide with
another point of the same servo are refused. **Ctrl + left-click on a line**
creates a new control point on the curve at that time (initially taking the
curve's value there, so the shape doesn't jump) — a new command appears with
a corresponding '+' on the waveform timeline. **Left-click a point and press
Delete** to remove it — the selected point shows a white ring; the '+' leaves
the waveform if no other command remains at that time. A spline-checked servo with no
commands yet is not graphed until its first command exists.

A **legend** (top-left of the spline area) shows each servo name in its line
color with a colored square and a checkbox to show/hide that line. A
**picklist** (top-right) selects the spline sample frequency: 10/20/40/50/60 Hz,
default **50 Hz**.

**On Save**, additional commands are generated along each spline at the
selected frequency (between the servo's first and last control point,
skipping times that already hold a control point; `Speed=Default`,
`reason="spline NNHz"`) and written into the saved file. A sample is only
emitted when the servo's value has **changed since the previous offset** —
flat stretches of a curve produce no redundant commands. The in-memory
timeline keeps only the control points, so editing stays clean and re-saving
regenerates fresh samples instead of accumulating them. The spline checkbox
selection and sample rate are saved as optional `splineServos` /
`splineSampleHz` fields.

## Robot Head preview

A separate moveable, resizable **Robot Head — URDF 3D** window (shown at
startup; closing it hides it — reopen from the Robot Head menu) loads
`Models/johnny5_head.urdf` into a native WPF `Viewport3D`. It is driven by
the same grid and spline-interpolated values as the former 2-D schematic.
Drag the window to orbit the camera, use the mouse wheel to zoom, and
double-click to reset the view. A lower-left **Drive: On / Drive: Off** button
sits above the camera row (**← / Recenter / →**). Drive defaults to **On**. With Drive **Off**,
the URDF model is frozen: servo/timeline pose changes, RGB eye-color changes,
and mouth LED amplitude changes are not applied to the WPF 3-D scene. Camera
orbit, zoom, reset, and recentering remain available. This allows the preview
to stay visible with substantially less continuous 3-D update work on systems
with less GPU headroom. The switch affects only that URDF preview instance; it
does not disable physical robot output.

The neck is a true three-joint hierarchy: **NeckTurn** yaws the entire head
about Z, **NeckNodUp** pitches it about Y, and **NeckTiltRight** rolls it
about X. All eyes, flaps, vents, nose parts, antennas, microphone, mouth,
and RGB eye color are descendants of that chain, so they remain attached
while the complete head moves in 3-D. The old TopNod/BottomNod and
LeftTurn/RightTurn size-changing rectangles are no longer used.

The URDF keeps the original control names for physical child joints
(`LeftLensHorizontal`, `BrowRightTopOpen`, `LeftEyeVent`, etc.) and the
preview preserves the robot-POV mirror: the robot's Left appears on the
viewer's right. Ganged timeline controls still fan out through the existing
ServoConfiguration map. Eye pop, MFRC rotation, and whip rotation are now
represented in the preview in addition to the controls supported by the
old schematic. `Models/servo_joint_map.json` documents the value-to-joint
conversion and coordinate system.

**NeckNodUp and NeckTiltRight remain exclusive** because they use the same
physical servo pair: whichever received the most recent command owns the
preview pose, and driving one live clears the other.

## Audio "Play" command in exports

When exporting an animation JSON with audio loaded, one extra command is
written (in offset order): `offsetSeconds` = the project's
`audioStartOffsetSeconds`, `servo` = `"Play"`, `value` = the full
`audioFilePath`, `speed` = `Default`, `reason` = the `audioFile` filename.
The `Play` pseudo-servo exists only in exports — it never appears in the
grid or the editor.

## RGBCommand colors

RGBCommand rows in the command editor gain a **24-bit color picker**: the
swatch button opens a palette popup that shows the selected color's
**0-255 R/G/B values** (also in the button tooltip). The chosen color is
saved with the command as an optional `"color"` field — part of the
project. In the top grid, the RGBCommand row's Value column shows a small
**color box** instead of a number. In the Robot Head preview, each eye has
a fixed-size RGB backing circle behind a dynamically generated blue iris
annulus. The RGB backing is **black by default** and takes the current
RGBCommand color as the cursor/playback passes its commands. The iris outer
diameter is **1.80 in**. Its inner opening is **1.80 in at -100**, **0.90 in
at the 0/default position**, and **0.30 in at +100**, with piecewise-linear
interpolation and the existing configurable reversal behavior. The front gold lens treatment is now one continuous mesh: it extends from the 24.796 mm inner radius to the 29.9847 mm optical-opening edge, bridges the recessed opening depth, and follows the CAD-matched slope to the 30.7467 mm radius where the Front Lens becomes flat. Its visible surface is moved 0.100 mm forward to remain cleanly visible over the CAD lens surface. The narrow 22.860-24.796 mm band between the 1.80 in blue iris and the gold ring is black. The mouth's
red **talking rectangle** scales with the audio amplitude during playback.

## Sequences, the Project folder, and library audio

The File menu's Load/Save Project items are now **Load Sequence / Save
Sequence / Save Sequence As**. The first dialog defaults to the `Projects`
folder inside the configuration folder. After any successful sequence load or
save, that file's directory is stored in `Paths.json` as `lastSequenceFolder`
and becomes the default for the next sequence load or Save As dialog. The
second configured path remains the **Project folder** used by Open Audio,
Insert-audio-on-timeline, Export Animation JSON, and missing clip resolution.

Library items save by default into **`Library\Animation`** under the
configuration folder; any audio a saved item references is **copied into
`Library\Audio`** and the saved item points at that copy (the timeline's
own commands are untouched). Inserting a library item does the reverse:
its audio is **copied into the Project folder** (never overwriting an
existing file there) and the inserted Play commands point at the
project-folder copies. Copy failures are reported and the original paths
kept.

## Paths & automatic configuration loading

**Config > Set Paths…** (renamed from Set Folders) edits the configuration
and audio folders. At startup — and again after confirming Set Paths — the
app **automatically loads `ServoConfig.json` from the configuration
folder** when it exists: the shared configuration (servo entries, gang
directions, Left Tic SN) updates, the grid sub-rows and connected hardware
refresh, and the Servo Configuration window opens showing those values
with `ServoConfig.json` as its current file (plain Save writes back to
it). Saving from the configuration editor likewise refreshes everything
the animator uses immediately.

**Gang sliders reflect into children**: moving a ganged ServoName's slider
moves its child sliders to their appropriate positions on both displays —
the grid's expanded sub-rows show the parent-range value (negated for
gang-reversed children on centered ranges), and the Servo Configuration
rows' PWM verify sliders show the exact pulse width that gang value maps
to for each servo (hardware Reverse + gang direction), display-only so
nothing double-drives.

## Servo Configuration

**Config > Servo Configuration…** opens an editor over every physical
RobotControls channel, grouped under its ganged ServoName header (gang map:
FlapsOpen → the four brow-open servos; FlapTiltUp → the two brow tilts;
IrisClose → the two irises; VentsOpen → the two eye vents; NeckTiltRight and
NeckNodUp both → the neck-tilt pair; the eye directions → the lens pairs;
single-control names map 1:1). Each individual physical servo row starts
with an editable **Maestro Port** (0–23), followed by Normal/Reversed relative
to the gang, Default/Min/Max PWM (clamped 500–2400), the 4-element speed and
accel arrays ("default,slow,fast,crawl"), and a **verify slider** spanning
Min–Max PWM that calls `MoveRobotControlNow(control, pwm)` live. Gang headers
do not show a Maestro port. Historical `RobotControls` channel values remain
the defaults, so older ServoConfig files that omit `maestroPort` retain their
existing channel assignments. **File > Load / Save / Save As** persists the
configuration to JSON; the active filename and Left Tic serial number remain
visible on the configuration window's top line. **`ServoConfig.default.json`**
ships with the project and matches the compiled startup defaults.

In the display grid, every ServoName has a **[+/-] expander** exposing its
RobotControl sub-rows, each with a PWM verify slider (enabled in Live
Drive). In the command editor, each command has a **Control** column:
"(ganged)" drives the whole ServoName; picking one RobotControl targets
just that servo — saved as an optional `"control"` field. Timeline rule: a
ganged command supersedes the individual controls, and an individual
control's command owns just that servo until the next ganged command (the
grid's ganged rows and splines track ganged commands only; the playback
hardware layer applies the per-servo precedence from the commands it
receives).

## Folders (Paths.json)

On **first run** (no `Paths.json` beside the exe yet), the app first looks for
an `animatorConfig` folder beside the `ServoAnimator` project folder. If found,
it uses that folder automatically and persists the choice to `Paths.json` when
possible. If it is not found, the app prompts for the Configuration folder and
saves the selection to `Paths.json`, read automatically on every
later start; **Config > Set Paths…** can change it at any time. The
**Configuration folder** holds servo configuration JSONs, the `TIC\` folder
with Pololu's `ticcmd`, Library data, and its `Projects\` child folder.
Sequences, movies, source audio, and exported animation JSONs default to the
`Projects\` child, subject to the application's remembered last-used sequence
folder behavior.

## Gangs, directions, and export modes

**BothEyePop** is a ganged ServoName driving both eye-pop Tics together
(the grid row sits with the eye pops; individual LeftEyePop/RightEyePop
rows still work, last command winning per stepper). Only real gangs (more
than one control) show the grid's [+/-] expander and offer individual
Control targets in the editor — single servos are not ganged.

Directions are **per (gang, control)**: the Servo Configuration shows
NeckTiltRight and NeckNodUp as separate groups over the same two servos
with independent Direction selections (tilt moves them opposite, nod the
same way), persisted in the JSON's `gangDirections` section and applied at
drive time. Each real gang's header has a **slider spanning its timeline
range** that drives the whole gang like the display grid. Single servos are
grouped under plain titles (Nose, MFRC, Whip Antenna). Saving OR loading
the configuration refreshes everything that uses it — grid sub-row ranges,
gang directions, and the connected hardware's rebuilt servo objects.

Next to the spline Hz picklist, the **Animate ganged / Animate individual**
picklist (default: individual, saved with the project) controls exports:
individual mode expands every ganged command into one command per child
control with values adjusted for gang-relative reversal (centered ranges
negate, 0..100 becomes 100-v; BothEyePop becomes LeftEyePop+RightEyePop
commands); ganged mode exports ganged values as-is. Individually-added
child commands are included either way.

## Disable commands

Each command row in the editor has a **Disable checkbox** (replacing the
now-redundant Speed picker there — speeds still come from the grid's Speed
picklist and Generate-from-grid). A disabled command turns its servo(s)
OFF instead of moving them: playback sends the Maestro disable per child
servo for gangs or the one channel for child commands, and exports write
the literal string **"Disable"** in the value field — for both ganged and
child-servo commands (individual-mode expansion puts it on every child).
Disable commands don't feed grid values, splines, or the head preview.

## Robot Head child-servo mapping (mirrored)

Individual child servos drive the head preview **from the robot's point of
view looking out** — the robot's Left is the viewer's right: BrowLeftTop/
BottomOpen size the RIGHT flaps (BrowRight* the left), BrowLeft/RightTopTilt
angle the right/left top flap, Left/RightIris the right/left iris,
Left/RightLensHorizontal+Vertical move the right/left eye circle, and
Left/RightEyeVent angle the right/left vent lines. Every head part is now
independent per side: child sliders (grid sub-rows and editor rows
targeting a child) move just their part live, and timeline scrubbing/
playback applies the gang-vs-individual precedence per part — an
individual command owns its part until the next ganged command (ties go to
the gang).

## Child-servo ranges & the merged Servo picklist

Child servos under a ganged ServoName use the **parent's range**
(-100..100 or 0..100) everywhere — the grid's expanded sub-row sliders and
the command editor — and their values map to pulse widths through a
faithful port of `MapDeltatoServo`: the servo's own hardware Reverse picks
the side of Home / span direction, and the gang-relative direction
(`isGangReversed`, edited per gang in the Servo Configuration) negates
centered values — defaults are all Normal except NeckTiltRight-under-
NeckNodUp, which makes the neck pair nod together while the hardware
directions alone make them tilt opposite. In the command editor the second
picklist is gone: the **Servo picklist lists everything in Display Grid
order**, each ganged ServoName followed by its child servos prefixed
" – "; picking a child targets just that servo until the next ganged
command.

## Speed picklist & Disable All

The grid's Speed column is a **picklist (Default, Fast, Slow, Crawl)**,
selectable in Live Drive: changing it pushes the speed/accel pair —
indexed by the enum value into each servo's 4-element arrays — to every
Maestro channel in that row's gang (compact protocol 0x87 speed + 0x89
accel). The column is hidden for RGBCommand and the eye pops, which have
no speed concept. The **Disable All** button beside Live Drive disables
PWM on every Maestro channel (0xAA 0x0C 0x0F per channel) so the servos go
limp; it works whenever hardware is connected. The Left Tic serial number
defaults to **00475552**.

## Physical hardware (Live Drive)

Nothing touches the robot until **Live Drive is turned On** — the first
press scans USB (WMI) for the **Pololu Maestro** servo card, the **two Tic
T249** eye-pop controllers (the Left one identified by the serial number in
the Servo Configuration window; empty = first found is left) and the
**Arduino Nano (CH340)** RGB controller. Anything missing is listed in an
error popup; devices that were found are still driven, and the scan retries
on the next Live Drive press. With hardware connected and Live Drive On:
grid/editor sliders, the PWM verify sliders, RGB command text, **and
playback** all drive the physical robot (ganged commands map through the
configured Home/Min/Max exactly like Servos.cs' MapDeltatoServo; the eye
pops go to the Tics; RGB text goes verbatim to the Arduino). The Tic path
expects Pololu's `ticcmd` under `TIC\` next to the exe.

RGB rows in the command editor have a **Build…** button: pick any command
from the Arduino RGB command set (SetRGBColor, Fade, Pulse, TheaterChase,
Cylon, Rainbow…), fill its arguments, and the generated text is sent verbatim
to the hardware. The old color-patch palette was removed in v1.9.2 because
the URDF now emulates all four physical 16-LED NeoPixel rings directly from
timeline time. LED 0 is assumed at 12 o'clock on every ring.

## Hardware integration points (stubs)

Both stubs live at the bottom of `MainWindow.xaml.cs`:

* `MoveServoNow(ServoSpeed speed, ServoNames servo, int value)`
  Called immediately when a numeric value slider/box is changed (Live Drive
  grid, and the sliders inside the command editor dialog).

* `MoveServoNow(ServoSpeed speed, ServoNames servo, string textValue)`
  Text overload for `RGBCommand`, called when the RGB text is committed in
  the Live Drive grid or edited in the command editor.

* `PlayBackServoValues(ServoCommand[] commandsAtOffset)`
  Called in real time during audio playback, once per unique time offset,
  with the array of commands (servo names, speeds and numeric/text values)
  scheduled at that offset.

Replace the `Debug.WriteLine` bodies with your serial/network output.

## File map

| File | Purpose |
|---|---|
| `Models.cs` | `ServoNames`/`ServoSpeed` enums, `ServoCommand`, `AnimationDocument`, JSON read/write |
| `WaveformView.cs` | SkiaSharp control: waveform, scalable time axis, `+` markers, cursor, zoom/pan/click |
| `MainWindow.xaml(.cs)` | Layout + all application logic (see `#region` sections) |
| `CommandEditorWindow.xaml(.cs)` | Modal per-command editor with slider/typed value, speed, reason, offset, delete |
| `ServoStateRow.cs` | View-model for one row of the servo status grid |

### Movie timeline (v1.0.0)
The optional Movie Timeline is toggled from the sequence transport bar. Movie
projects are JSON files in the configured `Projects` folder containing an
ordered `sequences` array. Sequence block durations are reread from the source
sequence files. Left-click loads/repositions within a sequence, dragging a
block changes its order, and right-click inserts at a sequence boundary or
removes a block. Movie Play plays only the current sequence; the arrow button
loads and plays the next sequence.

Version generation metadata is recorded in `PROJECT_VERSION.json`. The patch
component increments for each generated project on the same day; the minor
component increments on the first generation of a new day and resets patch to
zero; major changes only when explicitly requested.

### Movie timeline layout and keyboard shortcuts (v1.0.1)

When shown, the Movie Timeline is displayed below the normal Sequence Play row. The
movie block timeline comes first, followed by its Movie Play / Next / movie-file
status line. Keyboard **Up Arrow** invokes Movie Play/Pause/Resume and **Right Arrow**
plays the next sequence. These shortcuts are not intercepted while typing in text
editing controls.

## v1.0.11 UI sizing fixes

- Added `using System.ComponentModel;` to `LibraryItemSelectionWindow.xaml.cs`.
- Increased the collapsed Sequence Description field height for easier reading.
- Darkened the Animation Library file-list column header and increased Search field height.
- Widened Servo Configuration Direction, PWM, Speed, and Acceleration fields to prevent clipped values.


## v1.0.13 preview and timeline marker refinements

- Live Drive now gates physical USB output only; the servo controls and URDF preview remain interactive in either state.
- Speed selectors are wider so all speed names display completely.
- Sequence-row action buttons are approximately 30% shorter and the SEQUENCE badge text is vertically centered.
- Sequence and Movie filenames shown in the editor omit their `.json` extension.
- Command offsets are shown as shallow downward-pointing triangles instead of `+` symbols. Nearby command markers are vertically staggered and use their staggered position for hit-testing, hover, selection, and drag operations.

## v1.0.14 URDF camera controls and timeline alignment

- URDF instructions/legend now sit in the lower-right corner of the preview.
- Added a lower-left **Recenter Camera** button that sets camera yaw and pitch to 0° without changing zoom.
- Audio waveform and spline drawing surfaces now share the same horizontal origin/width so their time-grid lines align vertically.


## v1.0.15 URDF viewport background

The embedded and detached URDF Robot Head views now use `#C0EDFC` as the viewport background color.


## v1.1.0 HeadShell STL integration

- Replaced the synthetic URDF head-box geometry and segmented eye tubes with `Models/Meshes/HeadShell/HeadShell.stl`.
- Added direct binary STL support to the native WPF URDF renderer.
- The CAD STL remains at its native millimetre scale in the project and the URDF applies a 0.001 mesh scale to render it in metres.
- STL source axes are mapped into the URDF frame as: STL Z -> head X (fore/aft), STL X -> head Y (left/right), STL Y -> head Z (up/down).
- Eye centers and tube dimensions are measured from the STL rather than inferred from the old model.
- Eyes, irises, gold iris rings, eye-pop travel, vents, STEP flap meshes, nose, mouth, MFR, microphone and whip antenna geometry were resized/repositioned to the new shell.


## v1.1.1 URFD head and neck CAD assemblies

- Replaced the previous `HeadShell.stl` runtime visual with a triangulated runtime mesh generated directly from `Models/SourceCAD/URFDHeadAssembly.step`.
- The head STEP includes the physical mouth/LED assembly and the left/right neck ball cups.
- Removed the synthetic mouth-frame visual; the animated red mouth remains and is centered on the red LED array measured from the STEP assembly.
- Replaced the old neck column and two synthetic gold cylinders with a runtime mesh generated from `Models/SourceCAD/URFDNeckAssembly.step`.
- The neck assembly is attached to `neck_yaw_link`, so it continues to follow `NeckTurn`; head nod and tilt remain in the existing kinematic chain.
- The neck is fitted without scaling. Its two upper 12.7 mm ball centers are aligned to the spherical centers of the two ball cups in the head assembly.
- STEP files are retained in `Models/SourceCAD`; WPF renders triangulated STL derivatives because the lightweight URDF viewer does not contain a STEP/BREP kernel.

## v1.1.2 CAD neck articulation and Lip Light voice display

The STEP-backed Robot Head preview now uses the bottom neck Disc as the yaw center and the actual Solid U-Joint hinge intersection as the nod/tilt center. The upper Delrin balls follow the head while the Fabco K-5-X linkages visually swivel and extend between their fixed lower-ball pivots and moving upper-ball pivots. The Disc, Concave Block and bellows are black; the Fabco assemblies are gold; Delrin balls are white.

The old animated red mouth rectangle has been removed. Voice amplitude now drives the 14 CAD LEDs across the front of the Lip Light Box: the center two turn red first, then additional pairs illuminate outward with increasing sound level.


## v1.1.3 STEP colors and resilient STL loading

- Fixed the URDF preview failure on CAD-generated ASCII STL files. Runtime STL assets are now packaged as binary STL, and `RobotHeadView` can also read ASCII STL as a fallback.
- Re-tessellated the static head and neck from `URFDHeadAssembly.step` and `URFDNeckAssembly.step` into material groups using the STEP AP214 presentation colors.
- The head now uses the STEP metallic-black, glossy-gray, translucent-blue, steel-satin, light opaque, silver and polished-aluminum appearance groups.
- The neck static hardware uses STEP steel-satin, light-opaque and polished-silver groups.
- Fabco cylinder bodies use the STEP Gold - Polished color, pistons use Silver - Polished, and Delrin ball meshes use the STEP Opaque(202,209,238) color.
- Earlier explicit overrides remain authoritative: the neck Disc, Concave Block and bellows remain black, head Detail Plate parts remain black, and the 14 Lip Light LEDs remain dynamic for the audio-amplitude display.


## v1.1.4 SimplifiedHead2 CAD articulation

- Replaced the prior head runtime meshes with the supplied `SimplifiedHead2.step` assembly.
- Preserves the STEP component appearance colors and tessellates the simplified model into compact binary STL runtime meshes.
- `NoseBody` now rotates about the CAD ASME B18.8.2 spindle; `NoseBasket` rotates about `[HEAD-E-V2-B-02] Pin`.
- Upper flap assemblies first tilt about the HS-85 pinions mounted in the nose basket, then open/close about the HS-85 pinions carried by the flap assemblies.
- Lower flap assemblies rotate about their CAD servo-shaft/pinion axes.
- `VentsOpen` now drives the Hitec HS-40 output arm plus the five actual CAD vent fins in each eye tube around their CAD pivot pins.
- Lip voice LEDs are light gray while inactive, then illuminate red from the center pair outward as amplitude increases.
- Runtime head tessellation is approximately 238k triangles; the neck CAD/kinematics from v1.1.3 are retained.

## v1.1.5 SimpleMouth ball-cup alignment and preview lighting

- `Models/SourceCAD/SimpleMouth.step` is restored as the supplemental mouth/underside assembly beneath `SimplifiedHead2.step`.
- Its two `[NECK-C-UPR-COMN-02] Ball Cup (1-Piece)` spherical centers are used as the authoritative registration references. A rigid CAD-to-URDF transform aligns the cups to the existing left/right upper Delrin ball centers, so the mouth and cups move rigidly with the head during nod/tilt.
- The 14 front Lip Light Box LEDs are now extracted from `SimpleMouth.step` itself. Their idle voice-amplitude color remains light gray and the existing center-out red amplitude behavior is retained.
- The remaining SimpleMouth geometry is tessellated into STEP-color material groups; unstyled hardware falls back to steel/satin gray.
- All ten eye-tube vent fins now use the same dark-blue CAD material (`#080B4E`).
- The Robot Head preview includes an additional soft white point light above and to the viewer's left of the neutral forward-facing model.


## v1.1.6 rear cover and URDF range configuration

- Added `Models/SourceCAD/rearCover.STEP` to the back of the articulated head. Its broad flat mounting surface is registered to the SimplifiedHead2 back datum; the remaining cover depth extends behind the head. The runtime mesh retains the STEP `#97A3DA` appearance.
- Renamed the former **Tools** menu to **Config** and placed it before **Animation Library**. Config now contains Servo Configuration, URDF Configuration, and Set Paths.
- Added **Config > URDF Configuration…**. Every visual servo input has three sliders: minimum travel extent, maximum travel extent, and a URDF-only servo-position test slider, plus a Reverse checkbox.
- Centered `-100..100` controls preserve the imported CAD neutral at input 0, including asymmetric ranges such as FlapTiltUp. Positive controls use a `0..100` calibration slider. Eye-pop animation commands remain `0..2000`, but calibration normalizes the test slider to `0..100`.
- **Save Default** writes `URDFconfig.json` into the selected Configuration folder. If present, it automatically loads at startup and whenever Set Paths changes the Configuration folder.
- URDFconfig.json is the authoritative visual motion limiter; ServoConfig.json remains exclusively responsible for physical PWM/hardware calibration.


## v1.1.7 per-child URDF calibration and camera axis

- URDF Configuration now stores visual extents per `(ServoName, RobotControl)` rather than one range per logical gang. Ganged inputs therefore expose every physical child servo separately.
- The Direction/Reverse indicator in URDF Configuration is read-only and inherited from Config > Servo Configuration. `URDFconfig.json` no longer owns a separate reverse flag.
- Existing v1 URDFconfig files are migrated by copying each former gang range onto all of that gang's child rows.
- The shared neck pair is combined mechanically in the preview: NeckNodUp uses the differential component of the two calibrated child mappings; NeckTiltRight uses their common component.
- The microphone neutral joint origin moved 30 mm viewer-left (`Y - 0.030 m`) to sit over the top slot.
- Camera yaw now orbits the vertical line through the CAD NeckTurn joint center, while camera pitch/zoom behavior remains unchanged.


## v1.1.8 URDF calibration/linkage updates

- Microphone neutral position moved another 20 mm viewer-left to Y = -83.909 mm.
- Upper eye-flap CAD geometry is split into a rotating flap-side assembly and a tilt-carrier linkage. The flap plate, flap-mounted HS-85 servo body, mounting bracket, and their mounting screws rotate with FlapsOpen; the inter-servo Servo Arm/rod/collars remain on the tilt carrier and do not rotate with flap opening.
- URDF Configuration Reverse starts from the corresponding Servo Configuration direction but may be overridden per child servo for URDF visuals only. Overrides persist in URDFconfig.json schema v3.
- URDF Configuration is modeless so the Robot Head camera can be orbited, zoomed, recentered, and inspected while adjusting sliders.


## v1.1.9 upper-flap servo correction

- Moved each flap-mounted HS-85 servo body and its mounting bracket into the rotating upper-flap mesh.
- Kept the inter-servo Servo Arm/rod/collars (horn/linkage side) on the tilt carrier so it does not inherit upper-flap open/close rotation.


## v1.1.10 — STEP head-top assemblies
- Replaced the synthetic MFR with `MFRC.STEP`.
- Replaced the synthetic whip antenna/base visuals with `Whip Antenna.STEP`.
- Replaced the synthetic microphone cylinder with `Microphone.STEP`.
- Existing MFR, whip, and microphone motion joints/ranges remain unchanged.
- Imported the STEP-encoded `#CAD1EE` appearance for all three assemblies.


### v1.1.11
- Head-top MFR, whip antenna, and microphone now use per-part colors from the colored STEP files.
- MFR neutral assembly position lowered by 50 mm.


### v1.1.12
- Rear cover appearance now comes from `rearCoverColor.step` (`Paint - Enamel Glossy (Grey)`, `#B3B3B3`).
- Added `EyeMechanism.step` to both eye-pop links.  The CAD Front Lens is centered in each SimplifiedHead2 eye tube and protrudes 2 mm beyond the tube front at neutral eye-pop.
- Repositioned the existing iris/RGB pupil assembly inside the Front Lens opening, with the iris front 1 mm behind the opening plane; the gold ring radius was reduced to clear the CAD opening.
- URDF configuration still loads from the selected Configuration folder on startup/Set Paths. `Save Default` now reloads and reapplies the saved file immediately, and a debounced file watcher live-reloads external saves to `URDFconfig.json`.

- Gimbal refinement: `EyesVerticalUp` now rotates the STEP Gimbal Ring/front-lens assembly about the CAD horizontal screw axis; `EyesHorizontalRight` rotates the Front Lens inner gimbal about the CAD vertical screw axis. These URDF Configuration extents are degrees rather than the old synthetic translation millimeters.
- EyePop defaults to 0..89.951 mm for animation input 0..2000; at 2000 the 3/8-inch Delrin ball front surfaces remain 1 mm inside the eye-tube front edge.


## v1.2.4 URDF drive performance toggle

- Added **Drive: On / Drive: Off** immediately beside **Recenter Camera** in the URDF preview.
- The button defaults to On. Off freezes all servo/timeline/RGB/mouth-driven WPF 3-D model changes while retaining camera controls, reducing continuous preview-update work on less powerful GPUs.


## v1.2.5 — Whip Antenna automatic hinge fold
- The whip antenna is now split at its actual STEP `ASME B18.8.2` hinge rather than rendered as one rigid CAD group.
- The upper linkage plus its stud, skirt, and knob automatically pivots about that pin as the antenna rises.
- Fold starts when the hinge axis reaches the rendered SimplifiedHead2 top surface (54.496999 mm lift), reaches 90° when the lower linkage flat shoulder reaches the surface (57.417999 mm lift), and remains at 90° above that point.
- Existing Whip Antenna Raise/Lower and Rotate controls are unchanged; the fold is mechanically derived from Raise/Lower.


## v1.2.6 — Neck recolors and eye-ring cleanup

- The left and right Fabco body meshes now use the same gold material as the whip antenna top.
- The six neck banjo bolts (`Legris Fitting Banjo Bolt` and `Swivel Tee Fitting Banjo Bolt`) now render as steel, while the air purge tubes are rendered black.
- The gold ring around each eye was rebuilt so the outer gold section reaches the same inner edge as the inner gold section, removing the appearance of two separate gold circles.
- The simplified-head grey head-shell visual now uses the rear-cover grey color.


## v1.3.0 — Replacement neck STEP

`Models/SourceCAD/URFDNeckAssembly.step` was replaced with the supplied `NeckforURDFColors2.step`, and every neck runtime STL was regenerated from that replacement source. Nested XCAF instance transforms are now accumulated correctly when the Fabco assemblies are split into material groups, preventing the duplicated/mislocated geometry seen in v1.2.6. Existing neck yaw/pitch/roll and Fabco closed-linkage animation behavior is unchanged.


## v1.3.1 — Neck Color3 and preview lighting

- `NeckforURDFColor3.step` is now the authoritative neck CAD source. The color changes in that STEP are reflected in the runtime neck material groups while retaining the known-good v1.3.0 geometry placement.
- `Lip Light Box_Standard` and `Front Slide_V2` use the rear-cover grey.
- A second upper-left fill light was added to the URDF preview.


## v1.4.0 — .NET 10 and automatic animatorConfig discovery

- Retargeted the WPF application from `net8.0-windows` to `net10.0-windows`.
- Updated `System.Management` and `System.IO.Ports` package references to 10.0.0.
- On first run, before showing the Set Paths dialog, the application searches upward from the executable for the `ServoAnimator` project folder. If an `animatorConfig` directory exists beside that project folder, it becomes the Configuration folder automatically.
- When possible, the discovered path is persisted to `Paths.json`; an existing `Paths.json` or migrated legacy `Folder.json` still takes precedence.


## v1.5.0 — Editor layout controls and staged grid poses

- `NoseBasket` is now authored as a positive `0..100` servo and defaults to `0`. Its URDF calibration still supports signed mechanical endpoints, so the default `-45..+45` degree visual travel remains valid while logical input 0 maps to the minimum/default basket position.
- Added a **Commands** toggle in the sequence transport to show/hide the Commands-at-cursor inspector.
- Added a vertical `GridSplitter` between the servo grid and the embedded URDF preview so their relative widths are adjustable.
- Added a horizontal `GridSplitter` between the audio waveform and spline timeline; the chosen spline height is retained when the spline panel is hidden and shown again.
- Added a three-state embedded URDF height control: **Normal**, **Audio** (covers the right side of the audio timeline), and **Audio+Spline** (also covers the right side of the spline timeline).
- Manual grid edits now form a staged pose. Multiple servo values/speeds can be changed without earlier rows reverting. Right-click **Generate commands from grid values** captures the complete staged grid pose. Staged overrides are cleared when another timeline time is selected, playback begins, or the grid pose is generated into commands.


## v1.5.1 — Editor layout refinement and persistence

- Moved Live Drive and Disable Servos to the top-right hardware-status area immediately before Maestro/Arduino/Tic status.
- Movie Description now has a taller collapsed editor and expands upward from the bottom Movie Timeline area rather than using the sequence-description overlay.
- Commands now appears directly below the spline/audio editor region and above the movie controls; its toggle reads `Hide Commands` or `Show Commands`.
- The editor saves `EditorLayout.json` in the selected Configuration folder on close and restores window placement, splitter positions, Commands visibility, Movie Timeline visibility, and embedded URDF height stage at startup.


## v1.5.2 — Optional command speed and ganged speed configuration

- `ServoCommand.Speed` now defaults to **N/C** (No Change). N/C position commands do not send Maestro `Set Speed` or `Set Acceleration`; they preserve the profile already active on each physical channel. Existing commands that explicitly contain Default/Slow/Fast/Crawl remain explicit speed-change commands. JSON writes the no-change value as `"N/C"` and accepts `N/C`, `NC`, or `NoChange` when loading.
- **Edit Commands** now has a Speed column with `N/C`, Default, Fast, Slow, and Crawl. Selecting an explicit speed while Live Drive is on sends the speed/acceleration profile immediately without moving the servo; ganged selections fan out to every Maestro child.
- Grid value changes are position-only and no longer re-send speed on every slider movement. Changing the grid Speed selector still sends that profile immediately in Live Drive. **Generate commands from grid values** always stores the grid's explicit speed profile in every generated servo command so the speed state is reproduced during playback.
- Playback sends speed/acceleration only for commands whose Speed is not N/C. Spline-generated movement commands default to N/C so high-frequency spline samples do not continually resend Maestro speed settings.
- HardwareManager tracks the active speed profile per Maestro channel. Saving/loading Servo Configuration rebuilds each channel from the new PWM/direction/settings and re-sends its currently active profile, so edits to the active Speed and Acceleration values take effect immediately on connected hardware.
- Servo Configuration ganged headers now include `Gang Speeds d,s,f,c` and `Gang Accels d,s,f,c`. Editing either copies the four values into every child servo in that gang.
- NeckTiltRight and NeckNodUp retain independent per-gang Direction settings. Their Default/Min/Max PWM, speed, and acceleration fields are shared through the same physical NeckTiltLeft/NeckTiltRight configuration entries, and duplicate UI views refresh together when those shared values change.


## v1.5.3 — Movie save and ganged URDF range calibration

- Added **File > Save Movie**. Loaded movie projects can now be saved back to their current JSON file after sequence reordering/insertion/removal or description edits; an unsaved movie automatically uses Save Movie As.
- Reorganized **URDF Range Calibration** to mirror Servo Configuration grouping. Each logical ServoName has one shared Minimum Extent, Maximum Extent, and Servo Position preview value.
- Ganged child servos retain independent Direction controls and URDF-only direction overrides.
- Older URDF configuration files with different child ranges are migrated to the first child range for that logical gang so the runtime and UI use one authoritative ganged range.


## v1.5.4 — Editable URDF calibration extents

URDF Range Calibration Minimum/Maximum Extent values can now be typed directly in numeric fields or adjusted with their sliders. The controls are synchronized in both directions: typed values update the slider and gang calibration, while slider movement updates the numeric fields. Press Enter, Tab, or move focus away from an extent field to commit typed input.


## v1.6.0 embedded URDF calibration and corrected eye gimbal hierarchy

- `johnny5_head.urdf` now contains a ServoAnimator-specific `<servo_animator_calibration>` block with the calibrated Min/Zero/Max/Direction values. These are the baseline visual settings when no external `URDFconfig.json` is present.
- `URDFconfig.json` remains fully supported as an optional configuration-folder override. Save Default continues to write that override so the model remains configurable without modifying the deployed URDF.
- The eye hierarchy is now `EyePop -> EyesHorizontalRight -> Gimbal Ring/Gimbal Spacers -> EyesVerticalUp -> Wollensak Raptar lens/iris`. Horizontal eye motion therefore turns the complete Gimbal Ring and lens assembly; vertical eye motion pitches only the lens/iris assembly inside the ring.
- The vertical lens axis passes through the two CAD `[HEAD-B-B-01] Gimbal Spacer` components (CAD X approximately +/-36.919 mm), whose centerline maps to local URDF +Y through the gimbal origin.

## v1.6.1 eye Gimbal Ring pivot screws

- The two `SCHCSCREW 0.086-56x0.125x0.125-HX-N` components at the top and bottom of the Wollensak Raptar lens were separated from the vertically moving lens mesh.
- They now belong to the horizontal Gimbal Ring link together with the Gimbal Spacers.
- Eye Pop and horizontal eye motion therefore move these screws with the Gimbal Ring, while vertical eye motion leaves the screws fixed relative to the ring.
- The screws are visual geometry only and are not added to the collision-warning surfaces.

## v1.6.2 lateral Gimbal Ring pivot screws

- The two lateral Gimbal Ring pivot screws at native EyeMechanism CAD X = +/-41.275 mm were separated from the fixed CAD1EE eye-mechanism mesh.
- The source STEP identifies this lateral pair as `SCHCSCREW 0.086-56x0.375x0.375-HX-N`; they are the left/right counterparts to the previously corrected top/bottom Gimbal Ring screws.
- The lateral screws now belong to the horizontal Gimbal Ring link together with the ring, Gimbal Spacers, and top/bottom pivot screws.
- Eye Pop and horizontal eye motion move them with the Gimbal Ring; vertical Wollensak Raptar lens motion no longer changes their position relative to the ring.
- They remain visual-only and are not added to the collision-warning surfaces.


## v1.6.8
- FABK5X Body, Top Cap, Bottom Cap and the existing pale-gold hydraulic fitting group now use the same `neck_gold` material as the iris outer disks.
- FABK5X Body/Top Cap/Bottom Cap were retessellated at approximately 3x circumferential segment density for smoother cylindrical surfaces.


## v1.6.10
- Recolored the four gray hydraulic fitting bodies identified by the user arrows to `neck_gold`: the two upper Legris 3118-54-20 steel bodies and the two lower FN-32 needle-valve steel bodies.
- SFT-10 banjo bolts remain gray/steel. Full original steel meshes are retained for collision geometry.


## v1.8.0 — Configurable Maestro ports and Servo Configuration File menu

- Added an editable **Maestro Port** (0–23) to each individual physical servo row in Servo Configuration. Gang headings do not show a port.
- The existing RobotControls values remain the default port assignments, so older `ServoConfig.json` files without `maestroPort` preserve the current channel mapping.
- Maestro hardware output now uses the configured port number; saving/loading Servo Configuration rebuilds the connected servo objects so port changes take effect through the existing reconfiguration path.
- Moved Load, Save, Save As, and Close under a **File** menu at the top of Servo Configuration. The active configuration filename remains visible to the right of File, followed by the Left Tic serial number.

### Library Commands
Edit Commands now provides **Create Library Command**, which saves the commands in that dialog as a single-time-point JSON item under `Library\Commands` with a name and description. Audio Timeline right-click provides **Insert Library Command**, which browses those items and inserts every selected command at the current cursor time. The older **Insert commands from JSON file** remains a generic relative-time importer.
