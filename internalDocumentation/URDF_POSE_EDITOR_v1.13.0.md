# URDF Pose Editor — v1.13.0

## Direct-manipulation layout updates

- The editable **NeckTurn** dial is 50% larger than v1.12.8 and is positioned 75% of the way from the bottom-center resize-handle reference toward the URDF legend. Its 0° pointer remains straight down and a **↺** reset button is centered inside the dial.
- **Eye Pop** is now a vertical control anchored at the bottom-front of the robot head. Joined mode uses one centered control; LR Split exposes left/right controls at the same bottom-front region.
- A vertical **Whip antenna Up/Down** slider is pinned to the top of the URDF display. Its horizontal anchor is projected 30 mm farther outside the robot than the whip base.
- An editable **Whip Rotate** dial sits immediately outside the Whip Up/Down slider and follows the same calibrated degree mapping as the URDF mechanism.
- A vertical **MFRC Up/Down** slider is pinned to the top of the URDF display and uses a projected 50 mm outward offset from the MFRC antenna.
- A vertical **Microphone Up/Down** slider is pinned to the top of the URDF display while following the microphone hardware horizontally.
- In **LR Split**, Nose Body and Nose Basket retain the same locations used in Joined mode. The left/right Flap Open/Close sliders are side-by-side around the center face control.
- Slider reset buttons are placed on the side away from the robot model whenever possible. Dial resets remain centered inside their respective dials.

These controls remain draft-only while Pose mode is active. Use **Insert Pose** on the sequence timeline to translate the current URDF pose into commands.
