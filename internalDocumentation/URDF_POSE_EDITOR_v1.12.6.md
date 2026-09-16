# URDF Pose Editor — v1.12.6

## Refinements

- NeckTurn model motion is reversed from v1.12.5. The editable degree dial uses the same reversed visual-angle mapping, so its handle/readout remains synchronized with the URDF model.
- The Vent control is now a 90-degree quarter-arc on the actual CAD outer robot-left eye tube, running from 12 o'clock down clockwise to the outer side. The top of the arc is fully open and the side is closed.
- The Vent arc is projected from the outer eye-tube CAD geometry (approximately 88.9 mm OD), so it remains anchored to the visible tube during camera/model movement.
- NeckNod is a vertical slider immediately above the bottom-center URDF resize handle.
- NeckTilt is a horizontal slider anchored to the robot-left Fabco neck cylinder.
- Nose Body is a full-height vertical slider on the left side of the Nose body.
- Nose Basket is a half-height vertical slider on the right side of the Nose body.
- Flap Open/Close remains vertical (up=open, down=close) and is positioned with its bottom immediately above the Nose Basket opening. LR Split retains separate left/right sliders.
- Existing joined/split eye, iris, flap-tilt, eye-pop, shared-neck ownership, and Insert Pose behavior are retained.
