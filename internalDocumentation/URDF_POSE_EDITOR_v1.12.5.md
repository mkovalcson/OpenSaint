# URDF Pose Editor — v1.12.5

## Refinements

- NeckTurn is now an editable calibrated degree dial instead of a separate slider/read-only dial. Zero points straight down. Drag the dial handle or enter a degree value.
- The NeckTurn dial is positioned at the geometric midpoint between the bottom-center URDF resize-handle location and the lower-right URDF legend in docked and undocked views.
- The Vent pose control is a curved slider following the outside edge of the robot-left eye tube; it remains projected/anchored as the model and camera move.
- Nose Basket handle is projected to the middle of the front of the basket.
- Nose Body handle is projected onto the nose body immediately below the basket opening.
- Flap Open/Close is a vertical slider centered above the nose basket (up=open, down=close). LR Split exposes matching left/right sliders.
- Iris slider placement is tightened to sit immediately below the eye tube(s).
- NeckNod and NeckTilt retain their dedicated vertical/horizontal sliders and shared-actuator ownership behavior.
