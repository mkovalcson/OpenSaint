# URDF Pose Editor v1.13.4

- **Face Reset** now clears the Pose RGB Command as well as resetting the facial pose controls.
- The URDF lighting preview receives the equivalent of Arduino `ClearAll`.
- If Live Drive is enabled and RGB hardware is connected, `ClearAll` is sent to the hardware too.
- NeckTurn, NeckNod, and NeckTilt remain unchanged by Face Reset.
- The RGB text box remains empty after the reset; `ClearAll` is reset behavior, not a saved Pose RGB command.
