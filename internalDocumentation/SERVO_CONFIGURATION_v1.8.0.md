# Servo Configuration v1.8.0

- Every individual Maestro servo row has an editable `Maestro Port` value from 0 through 23.
- The default port is the historical numeric value of its `RobotControls` entry.
- JSON persists the setting as `maestroPort`. Older files that omit the field automatically use the historical channel.
- `MaestroServo` sends target, speed, acceleration, and disable commands to the configured Maestro port.
- Gang headers do not display a port number.
- File operations are under the top `File` menu: Load, Save, Save As, Close.
- The current configuration filename is displayed to the right of File, followed by the Left Tic serial number.
