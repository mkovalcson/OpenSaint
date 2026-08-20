# Troubleshooting

## A configuration change is not affecting hardware

Confirm the change was saved in Servo Configuration and Live Drive is enabled. Physical movement uses Servo Configuration, not URDF Min/Zero/Max calibration.

## A URDF calibration change is not affecting the physical robot

This is expected. URDF Configuration controls model-space visualization. Physical PWM and direction are controlled separately in Servo Configuration.

## A stale URDF calibration appears after changing embedded defaults

An existing `URDFconfig.json` overrides the embedded URDF calibration. Update or remove the override if you want to use the model's embedded values.

## Duplicate Maestro port warning

Port values must be 0 through 23. Two different physical servos using the same port are highlighted bold red. The duplicated NeckTilt views across the two neck gang sections are intentional shared controls and are excluded from the duplicate warning.

## URDF window is missing after undocking

Use the Dock button under Headtop Controls in the expanded two-column Servo Grid. Closing the detached URDF window with its X also docks it back into the editor.

## Recent file is unavailable

Open Recent keeps missing paths visible but disabled. Use the normal Load Sequence or Load Movie command to locate the file again.

## Collision warnings look unexpected

Collision diagnostics intentionally check only the configured flap/eye contact surfaces. Servo bodies, flap arms, brackets and unrelated head/neck parts are excluded. Verify URDF calibration before treating a collision warning as representative of the physical robot.
