# URDF Pose Editor — v1.12.7

## Refinements

- Increased the editable NeckTurn degree field from 23 px to 29 px high (approximately 25%).
- The NeckTurn dial remains zero-down, but now behaves as a top-down heading indicator: the pointer represents the front of the head and visually follows the same direction as the head front during yaw.
- Flap Open/Close is anchored to the projected center of the moving NoseBody opening.
- Nose Body is anchored to the projected physical robot-right lip of the NoseBody opening.
- Nose Basket remains half-height and is anchored to the projected physical robot-left lip of the NoseBody opening.
- NeckNod is anchored to the physical front-middle of the Fabco neck assembly, calculated from the projected midpoints of the left and right cylinders.
- NeckTilt is anchored to the physical midpoint of the robot-left Fabco cylinder.
- Existing Vent quarter-arc, iris placement, eye controls, Eye Pop sliders, LR Joined/Split behavior, and Insert Pose behavior are retained.
