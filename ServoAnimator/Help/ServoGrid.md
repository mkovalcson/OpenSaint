# Servo Grid

The Servo Grid shows the effective servo values at the current timeline position and provides manual controls for staging motion.

## Ganged and individual controls

Ganged ServoNames represent coordinated physical controls such as eye motion, flaps, irises and vents. Expanding a gang shows its physical child controls where individual movement is supported.

The main logical ranges are generally `-100..+100`. Positive-only controls use their own ranges; NoseBasket is `0..100` and Eye Pop uses its configured numeric range.

## Staging several values

Changing one Grid value does not force previously changed Grid rows back to their timeline values. This lets you stage an entire expression or pose.

Right-click the timeline and choose the command-generation action to create commands from the staged Grid values. Grid-generated servo commands include the current Speed selection rather than using N/C.

Staged overrides are cleared when:

- You select another timeline time.
- Playback starts or resumes.
- The staged values are committed as commands.

## Speed selection

Changing a Grid Speed immediately sends the selected Maestro speed/acceleration profile to the physical child servo channels when Live Drive is enabled. Moving the position slider itself does not repeatedly resend speed configuration.

## URDF preview

Grid movement also updates the URDF preview. URDF motion uses URDF calibration and is independent of the physical PWM calibration used by Live Drive.
