# URDF Pose Editor — v1.12.4

This revision refines the direct-manipulation URDF Pose overlay introduced in v1.12.2.

## Eye controls
- Joined gaze control is now shown on the robot-right eye; the shared iris slider is under the robot-left eye.
- LR Split continues to show independent controls for both eyes.
- Horizontal bullseye movement is reversed relative to the logical `EyesHorizontalRight` command sign, both while dragging and when displaying existing values, so visual handle direction and model response remain synchronized.

## Neck controls
- A 190-pixel horizontal NeckTurn slider is positioned at the neck base.
- NeckNod uses a vertical slider between the two front Fabco cylinders.
- NeckTilt uses a horizontal slider directly above the NeckNod slider.
- Moving Nod or Tilt makes that logical control the active owner of the shared left/right neck actuators.
- A read-only circular dial to the screen-right of the neck shows the calibrated physical NeckTurn angle in degrees.

## Other pose controls
- Eye Pop is controlled by vertical slider(s) on the outer edge of the head: one joined slider or independent left/right sliders in LR Split.
- The Vent handle is moved beyond the outside edge of the vent geometry.
- Microphone, whip-antenna, and MFR direct-manipulation controls are removed from the Pose overlay. Their underlying model/timeline support is unchanged.
