# Getting Started

Animation Editor & Player edits timed Johnny 5 servo motion against audio, previews the calibrated motion in the URDF model, and can optionally drive the physical hardware through Live Drive.

For numbered walkthroughs, select **Step-by-Step Tutorials** in the Help contents or search for **tutorial**. Start with creating a three-command Sequence, then add smooth Spline motion. Additional tutorials cover command edits, poses, audio/RGB, Movies, looping cues, Library Poses and saving.

## Typical sequence workflow

- Open or create a Sequence.
- Load the primary audio if the sequence uses one.
- Place the timeline cursor where motion should occur.
- Click Add in Commands and choose a servo, value and speed.
- Apply the command edits, or arrange a pose in the URDF pose editor and use Insert Pose.
- Use the Spline Editor when a servo should interpolate smoothly between control points.
- Preview with the URDF model and playback.
- Save the Sequence.

Edit Commands stages all row changes until Apply and Close. Cancel discards the drafts; hardware and URDF preview remain available while editing.

## Movie workflow

A Movie is an ordered list of Sequence files. In the always-visible Movie Timeline, insert sequences as blocks, drag blocks to reorder them, and use Save Movie to write changes back to the current movie file.

## Physical hardware

Live Drive is separate from URDF preview. The Servo Configuration determines Maestro channel, PWM calibration, direction, speed and acceleration profiles. Eye Pop uses Tic controllers and RGB lighting uses the Arduino path.

Use **Default Positions** to return Maestro servos to their configured Default PWM values, set both Eye Pops to zero, and send `ClearAll` to the Arduino.

## Help

Press **F1** while a control or editor section has focus to open the most relevant Help topic. Help is intentionally unavailable while playback is running or paused so opening a documentation window cannot interrupt playback.
