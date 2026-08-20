# Getting Started

Animation Editor & Player edits timed Johnny 5 servo motion against audio, previews the calibrated motion in the URDF model, and can optionally drive the physical hardware through Live Drive.

## Typical sequence workflow

- Open or create a Sequence.
- Load the primary audio if the sequence uses one.
- Place the timeline cursor where motion should occur.
- Adjust one or more values in the Servo Grid.
- Right-click the timeline and generate commands from the staged Grid values, or edit commands directly.
- Use the Spline Editor when a servo should interpolate smoothly between control points.
- Preview with the URDF model and playback.
- Save the Sequence.

The grid can hold several manual changes at once. Those staged values remain together until you generate commands, move to another timeline time, or begin playback.

## Movie workflow

A Movie is an ordered list of Sequence files. Show the Movie Timeline, insert sequences as blocks, drag blocks to reorder them, and use Save Movie to write changes back to the current movie file.

## Physical hardware

Live Drive is separate from URDF preview. The Servo Configuration determines Maestro channel, PWM calibration, direction, speed and acceleration profiles. Eye Pop uses Tic controllers and RGB lighting uses the Arduino path.

Use **Reset** to return Maestro servos to their configured Default PWM values, set both Eye Pops to zero, and send `ClearAll` to the Arduino.

## Help

Press **F1** while a control or editor section has focus to open the most relevant Help topic. Help is intentionally unavailable while playback is running or paused so opening a documentation window cannot interrupt playback.
