# URDF Pose Editor — v1.12.8

## Refinements

- Increased the editable NeckTurn degree field from 23 px to 29 px high (approximately 25%).
- The NeckTurn dial remains zero-down, but now behaves as a top-down heading indicator: the pointer represents the front of the head and visually follows the same direction as the head front during yaw.
- Flap Open/Close is anchored to the projected center of the moving NoseBody opening.
- Nose Body is anchored to the projected physical robot-right lip of the NoseBody opening.
- Nose Basket remains half-height and is anchored to the projected physical robot-left lip of the NoseBody opening.
- NeckNod is anchored to the physical front-middle of the Fabco neck assembly, calculated from the projected midpoints of the left and right cylinders.
- NeckTilt is anchored to the physical midpoint of the robot-left Fabco cylinder.
- Existing Vent quarter-arc, iris placement, eye controls, Eye Pop sliders, LR Joined/Split behavior, and Insert Pose behavior are retained.


## v1.12.8 refinements

- NoseBody, NoseBasket, and Flap Open/Close sliders are now anchored to the vertical midpoint of the imported `head_link` visual bounds rather than to the moving NoseBody, so they remain vertically centered on the robot head. Their lateral arrangement remains center / robot-right / robot-left as appropriate.
- Every WPF Pose slider now has a small adjacent **↺** reset button. The custom Vent arc slider also has its own reset button.
- A **Face Reset** button appears immediately to the right of **Pose: On** while Pose mode is active. It resets eye gaze, irises, flap open/close and upper-flap tilt, vents, NoseBody, NoseBasket, and Eye Pop to neutral while preserving all neck controls.
