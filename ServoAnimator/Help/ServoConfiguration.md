# Servo Configuration

Servo Configuration defines how logical animation values map to the physical Maestro servos and stores speed/acceleration profiles.

## Port

Each individual physical Maestro servo has a configurable Port from `0` through `23`. Ganged headings do not have ports.

Two different physical servos assigned the same port are highlighted bold red. Repeated views of the shared NeckTilt servos under NeckTiltRight and NeckNodUp are the same physical controls and are not treated as duplicates.

Changing a shared NeckTilt servo setting in one neck gang updates the corresponding view in the other gang.

## Default, Min and Max PWM

Default is the physical home pulse used by Reset. Min and Max define the physical endpoint mapping for the logical servo range. Direction controls whether the physical mapping is normal or reversed.

## Speed and Acceleration profiles

Each physical Maestro control has four Speed and four Acceleration values:

- Default
- Slow
- Fast
- Crawl

Ganged rows also provide Speed/Acceleration values. Changing a gang value applies it to the physical child servos. Individual child fields can then be fine-tuned.

Timeline commands can select one of these profiles or N/C. N/C leaves the Maestro's currently active speed/acceleration unchanged.

## Save behavior

Saving Servo Configuration applies the new configuration immediately. Connected Maestro servo objects are rebuilt with the updated ports, ranges and direction. The currently active speed profile is re-sent using the new Speed and Acceleration numbers.

The built-in fallback configuration is used only when no `ServoConfig.json` is found in the active configuration folder.

## Window position within the list

During one application session, Servo Configuration remembers its vertical scroll position when the window is closed and restores that position the next time the window is opened.
