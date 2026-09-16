# Xbox and Steam Controller mapping

Use **Config > X-Box Controller Mapping** or **Steam Controller Mapping**. The
Steam screen is for the new 2026 Steam Controller, not the 2015 model. The screens
can be edited without connected hardware. Click a controller input or its mapping
label around the graphic. Connector lines identify the associated controls, and
the selected mapping's line is highlighted. Hover a label to read its full mapping;
arrow keys move between controls when the graphic has focus. The shoulder-edge
illustration retains right trigger/shoulder on the
right; the Steam screen also shows rear buttons and six motion axes.

## Four independent layers

1. Neither shoulder held: Default.
2. Left shoulder (LB/L1) held: Left.
3. Right shoulder (RB/R1) held: Right.
4. Both shoulders held: Both.

With MUX enabled, shoulders are fixed selectors, absent from the editable input
list, and cannot be remapped even by editing the JSON. Triggers remain ordinary mappable inputs.
Each layer can assign the same input to a completely different group, individual
servo, or editor action. Initial suggestions are inspired by ControllerCheatSheet.xlsx;
unrecognized named poses, RGB effects and physical-angle presets are not invented.
Reset mappings restores just the current layer's suggestions. Cancel discards
the draft; Save mapping writes the complete controller-specific profile.

### Steam: No MUX

After the four layer tabs, check **No MUX, Map Shoulder buttons** to use one
mapping set. The layer tabs disappear; the shoulder controls become clickable
**Left Shoulder** and **Right Shoulder** buttons. They accept Control, Pose, and
Sequence mappings, including looping sequences, and may also serve as motion
trigger buttons. Holding them no longer switches any mappings.

The first switch copies the Default layer into a separate No MUX set. That set
and all four MUX layers are preserved independently, including when toggling the
option or saving/reopening the configuration. Uncheck the option to return to four
layers. Release held inputs after switching modes. Xbox keeps its four MUX layers.

Mappings live in `XboxControllerMapping.json` and `SteamControllerMapping.json`
inside the active Configuration folder. They are saved independently, with atomic
file replacement. Invalid files disable input until corrected and saved from the
mapping screen; they are not silently overwritten.

## Responses and units

- **Relative**: deflect to move using the selected servo's calibrated speed; release to hold. Partial deflection reduces the requested rate.
- **Absolute**: input position maps between Low and High.
- **Set**: a press sets the High position.
- **Toggle**: a press alternates Low and High. Held buttons do not repeat.

Relative inputs use **Speed Calibration** and the servo's active **Default**, **Slow**,
**Fast**, or **Crawl** profile. The mapped Speed actions select those same calibrated
profiles. With no controller speed override, the servo retains its current profile
(Default if none was selected). Eye Pop uses its calibrated extend/retract times.
Ganged controls use their slowest calibrated member's rate so the shared target
does not outrun that member. Acceleration continues through the normal URDF motion
model and hardware settings. There is no separate controller units/second setting;
older saved mappings still load, and their former Rate field is ignored. This also
applies to the mapping screen's URDF test mode.

For a profile with unlimited speed but finite acceleration, proportional jogging
uses the peak speed predicted for a full calibrated stroke. A 0/0 profile uses
the same physical speed estimate as the URDF.

Values are the editor's existing native ranges (for example -100..100 or 0..2000),
not physical degrees. Dead zone filters drift. Reverse flips analog direction.
Gyro full scale is radians/second; accelerometer full scale is m/s². Motion axes
are X/pitch, Y/yaw, Z/roll. Accelerometer neutral/gravity is captured each time
input is enabled or the layer changes: hold the controller still while selecting
the layer. Trackpad position is active only while touched. The live readout shows
raw sensor/input values, including which inputs the device actually exposes.

For any gyro or accelerometer mapping, **Optional trigger · hold button** can
require a chosen button before motion activates its target. Leave it at **None**
for the previous behavior. Rear buttons L4/R4/L5/R5 appear first, followed by grip
touch and the other buttons. Analog triggers engage above half travel; shoulder
MUX selectors remain reserved while MUX is enabled; Steam's shoulders become
available here in No MUX mode. Each axis and mapping set has its own choice.
The selected button retains its own normal mapping.

Raw sensor feedback remains visible while the gate is closed, with **Hold …**
shown in place of a new destination. Release the button to stop new motion output;
absolute mappings hold their last position. For Pose, Sequence, or Action targets,
the button gates activation; an activated sequence follows its usual playback/loop
behavior and can be stopped or replaced using its normal controls.

## Live feedback and URDF testing

Connected inputs highlight green on the controller and their mapping callouts
while pressed or outside the dead zone. Active callouts show raw input → mapped
value; the inspector shows the full destination and active-input readings.
Sticks and triggers use normalized controller values; gyro and accelerometer
readings retain their sensor units. Feedback uses the physically held shoulder
layer, identified at the bottom, even when editing another layer's tab.

Check **Test URDF with these mappings** to drive the visible URDF with the
unsaved configuration. Numeric controls, Default pose, Library poses/sequences
(including loops), and Stop work in this mode. Other editor actions are displayed
but do not execute. Live hardware output is suppressed even if Drive HW is on.
Testing pauses timeline playback and does not insert commands into the timeline.
Move the configuration window aside or detach the URDF view to watch the model.

URDF visuals follow rendering at up to 60 updates/second in normal controller operation, Library
playback, and configuration testing. Newer values replace intermediate visual
updates between frames. Input polling, button actions, audio and hardware timing
remain independent. Stopping or replacing a sequence cancels its queued visuals;
a completed sequence still displays its final pose.

Release controls after enabling testing, changing a mapping, or changing shoulder
layers. Invalid unfinished edits pause testing. Losing config focus pauses testing
only when its background URDF permission is off in Focus Control. Opening a
Library picker, disconnecting, unchecking Test URDF, or closing the window stops
test sequences. Cancel discards mappings but leaves the last previewed pose.
Save mapping persists the draft for normal controller operation.

Steam callouts are 25% narrower than Xbox callouts. Face buttons and the D-pad
have outer columns, with gyro and accelerometer mappings below in those columns.

## Library buttons and looping

Select a button in either mapping screen, then open the **Pose** or **Sequence**
tab at the top of its mapping inspector. Use **Choose Library Pose…** or
**Choose Library Sequence…** to select
an item, or enter its name or relative path. Each shoulder layer keeps its own
selection. Save the mapping when finished. For a sequence, enable **Loop until
stopped or replaced** to repeat it; leave it unchecked to play once.

A press immediately stops any current sequence/movie or controller Library run
and applies the selected pose or starts the selected sequence at time zero.
Another Library button replaces it even if the original button is still held.
Holding a button does not repeatedly restart playback. Library playback uses the
saved commands and their offsets, including individual servo, RGB and audio
commands. It leaves the open timeline, document, cursor and undo history intact.
Unmentioned controls hold their current positions.

The **Stop** action or the editor's Stop button cancels a run, including a loop.
Disabling the controller, disconnecting it, losing editor focus, opening a dialog,
or changing Drive HW stops Library playback and cancels pending output. A device
write already in progress can finish. Missing or invalid replacements stop the
old run and show an error in the status bar. Stick motion is ignored during a
Library sequence. Starting normal timeline playback also ends a Library run.

## Connect and enable

Below the hardware device list are **Stream Deck**, **X-Box**, and **Steam**
buttons with controller graphics and green/red connection lights. Stream Deck
uses a two-line label and an XL graphic so all three buttons keep the same width.
Each button turns on automatically when its device is detected. Click it to
disable that device's control; the connection light stays green while connected.
A manual disable survives polling and reconnects until you click to enable again
or restart the editor. Both gamepads can remain enabled; fresh input determines
which is driving. Connecting alone does not interrupt a performance. With multiple
matching devices, the first detected device of that kind is selected.

With **Drive HW off**, enabled controller inputs drive the URDF preview only.
With **Drive HW on**, position output also follows the normal connected hardware
mapping. No live hardware connection is enabled automatically by a controller.
Default pose sets logical zero through that same route; it does not invoke the
UI's hardware-connect-and-reset command. Disable servos also disables controller
input, so a held stick cannot immediately drive the robot again.

Use **Focus Control** at the bottom of Hardware or **Config > Focus Control** to
choose background permissions. Xbox, Steam, and Movie Playback default to enabled.
Each controller has independent **Drive URDF model** and **Drive Physical Model**
background choices, also enabled by default. You can allow either destination,
both, or neither. The controller's checkbox is the master background permission;
its destination choices are preserved when that checkbox is off. Physical output
still requires **Drive HW** and a connected hardware device.

An enabled controller stays connected when another app takes focus. Background
permissions apply to numeric controls, RGB, Library poses/sequences and timeline
playback started by that controller. Pending controller output for a destination
is canceled when its permission is revoked. Playback started using the editor's
own transport remains independent of controller permissions. Disabling background
control pauses its output until the editor or detached URDF view regains focus;
it does not disconnect the device. Editor dialogs pause normal controller input.
Servo inputs pause during timeline playback;
mapped transport buttons can still pause or stop playback. Timeline commands are
not recorded automatically; a mapped Snapshot inserts the current URDF pose using
the existing editor snapshot/undo behavior.

After enable, a focus change that paused input, or a shoulder-layer change, release buttons and
return sticks/triggers to neutral before using them in the new layer. This avoids
held inputs firing a different action. Disconnect stops further output and clears
pending controller movement. Reconnect automatically restores enabled control
unless you disabled it manually; held controls must return to neutral first.

Focus settings are saved in `FocusControl.json` in the active Configuration folder.

Stream Deck's direct Library API has its own **Stream Deck / Library API** entry
in Focus Control, with independent URDF and Physical Model background choices.
Stream Deck keys use the editor's destinations automatically; physical playback also
requires Drive HW and connected hardware. Starting an API Library item takes
over from enabled gamepads without unchecking their buttons. A new mapped Library
pose/sequence or Stop button can take over; ordinary stick motion waits during a
running sequence. Stream Deck detection requires plugin 1.0.3 or later running in
the Stream Deck app. Disabling its Hardware button stops its running Library item
and cancels its pending picker. Closing the app or disconnecting the device clears
availability, with up to five seconds allowed for a missing plugin heartbeat.

### Movie arrows while using another app

After starting a movie, plain arrow keys continue to control it when the editor
is in the background: **Up** play/pause, **Right** next cue, **Left** restart/previous,
**Down** stop and rewind. The session stays armed while paused and between cues.
These four keys are reserved for the movie while it is in the background; they
will not also navigate the other app. Modified arrows, such as Ctrl+Arrow, remain
available to that app. Foreground editor controls keep their existing keyboard behavior.

The **■ Stop** button, starting standalone sequence or Library playback, closing
the movie, or turning off **Movie Playback** in Focus Control releases background
arrows. Focused editor dialogs also release them temporarily. Movie audio itself
continues when focus changes, regardless of the background arrow setting.
If Windows reports another app already owns an arrow shortcut, all four are
released and the editor shows a status message instead of reserving a partial set.
Settings use Windows RegisterHotKey with autorepeat disabled.

## Device support

The bundled SDL 3.4.16 runtime supports Xbox-class gamepads and native Steam 2026
input, including L4/R4/L5/R5, both trackpads, touch sensing, gyro and accelerometer.
Pair/connect the device in Windows first. Guide/Share and some optional inputs
may be reserved by Windows, Steam or the controller's firmware. The mapping
screen reports unavailable inputs rather than treating them as live data.

If Steam Input presents only an emulated Xbox device, native Steam-only inputs
may be hidden. Use the controller's native connection and close/reconfigure any
software claiming it exclusively. A connected puck without a paired, powered-on
controller does not count as a connected controller.

References:
- https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16
- https://wiki.libsdl.org/SDL3/SDL_SensorType
- https://partner.steamgames.com/doc/api/ISteamInput

Validation covers four banks, save/load, neutral gating, held-button suppression,
dead zones, rates, sensor baseline, native driver loading, and WPF screen layout.
Physical controller behavior still needs verification with the user's devices.

## Live mapping display

An enabled, connected X-Box or Steam controller replaces the command list with its controller graphic and current mappings. Holding/releasing the shoulders changes the displayed MUX bank automatically. Steam No MUX shows its separate shoulder-mappable bank. With both controllers enabled, the controller most recently producing a mapped command owns the display; X-Box is the initial choice. Disabling or disconnecting them restores the command list. Scroll to see the full graphic in a smaller pane; hover over a mapping for its full text.

## Collision Safeguard

The **Collision Safeguard: On / Off** toggle below Focus Control defaults On and is saved with Focus Control settings. It applies to X-Box/Steam motion, controller-started Library playback and timeline playback, and configuration preview testing. The button turns orange briefly when motion is blocked and remains orange during repeated blocked attempts.

The safeguard checks sampled intermediate poses against the existing URDF flap/eye/lens/gimbal collision geometry before controller output is queued. Calibrated URDF steps are also checked before rendering. It works independently of Collision Warning visibility. Missing geometry blocks controller motion. A rejected move clears pending output, stops Library motion or pauses controller-started timeline playback, and requests a hardware hold when physical output is active. Maestro holds preserve the current commanded pulse and torque; Tic motors receive halt-and-hold. A held eye-pop motor may require re-homing because an abrupt halt can lose steps (see the Pololu Tic command reference: https://www.pololu.com/docs/0J71/8#cmd-halt-and-hold).

This is predictive protection for the modeled contact areas, not comprehensive physical collision prevention. It assumes the physical robot matches the displayed pose, calibration, and timing. Geometry omissions, mechanical deflection, communication latency, independently moving actuators and missed steps can defeat those assumptions; sampled paths cannot guarantee every intermediate contact is detected. Check alignment after a physical hold. Unmodeled contacts and pre-existing contacts excluded by the neutral-pose baseline are not protected. If already in a modeled collision, further checked motion is blocked; turn the safeguard off only to deliberately resolve that condition.
