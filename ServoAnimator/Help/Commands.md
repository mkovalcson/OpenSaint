# Commands and Edit Commands

Commands are the authored changes that occur at a specific timeline offset.

## Edit Commands

Double-click a command in Commands at Cursor, or use the timeline command editor, to edit all commands at a time point. A command can contain:

- Offset
- Servo or individual child control
- Value
- Disable state
- Speed
- Optional Reason

## Command Speed

Speed defaults to **N/C** meaning No Change. An N/C position command does not send Maestro Speed or Acceleration; it only sends the target position and leaves the channel's active speed/acceleration profile unchanged.

An explicit Default, Slow, Fast or Crawl selection sends the matching configured Speed and Acceleration before the target. For ganged commands the profile is sent to every physical child servo in the gang.

Grid-generated commands deliberately record all current Speed values instead of N/C.

## Individual child commands

A command may target a child control inside a gang. It then affects only that child until a later ganged command supplies a new gang value.

## RGB commands

RGBCommand uses text rather than a numeric servo value. The RGB Builder can create supported Arduino command strings such as `ClearAll`, `SetRGBColor`, fades, pulses, theater chase, Cylon and rainbow effects. The Edit Commands list no longer uses a separate color patch; the URDF viewer shows the actual four 16-LED eye/vent ring state.

## Create Library Command

The **Create Library Command** button in Edit Commands saves the command rows currently shown as a reusable single-time-point command group. You provide a JSON file name and description. The file is stored in `Library\Commands`. When inserted later with **Insert Library Command**, all saved commands are placed at the selected timeline time.
