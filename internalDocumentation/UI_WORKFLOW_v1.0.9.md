# Animation Editor & Player v1.0.9

## Servo grid
- Paired layout: Eye Flaps | Eyes; Nose | Neck; Eye Pop | Lighting & Vents; blank | Headtop Controls.
- Headtop Controls contains MFR, Whip Antenna, and Microphone and starts collapsed.
- Thin internal separators divide those three Headtop subgroups.
- Group headers use reduced vertical padding and a compact disclosure button.

## Descriptions
- Expanded Sequence and Movie description panels track one-half of the current servo-grid height.
- Horizontal scrollbars are suppressed in both compact and expanded description editors.

## Paths
- Set Paths requests only the Configuration folder.
- Projects is always derived as `<Configuration>\Projects` and is created when possible.

## Menu readability
- Popup/context menu item text uses a dark foreground for legibility on the native light popup surface.
- Top-level menu labels continue to use the active theme foreground.

## Audio
- Added a Sequence transport volume slider from 0 to 100 percent.
- The volume is applied to every newly opened WaveOutEvent and immediately to the current device, so it affects primary audio and inserted Play clips.
