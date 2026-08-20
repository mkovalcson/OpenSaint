# Live Drive and Hardware

Live Drive sends editor movement to the physical Johnny 5 hardware. It is independent of the URDF viewer's Drive toggle.

## Maestro servos

Logical servo values are mapped through Servo Configuration to Maestro ports and PWM Min/Default/Max values. Physical Direction and gang-relative Direction are combined so mirrored servos move correctly.

A command with Speed set to N/C sends only position. Default, Slow, Fast or Crawl sends the profile's Maestro Speed and Acceleration before the target. Ganged speed changes are sent to every Maestro child servo.

## Eye Pop

Left and Right Eye Pop use Tic controllers rather than Maestro PWM. Reset commands both Eye Pops to zero.

## Arduino RGB

RGBCommand text is sent through the Arduino lighting path. Reset sends `ClearAll`.

## Live controls

- Live Drive: enables/disables live physical driving from editor movement.
- Disable Servos: disables Maestro PWM so the servos go limp.
- Reset: moves Maestro channels to configured Default PWM, resets Eye Pop and clears Arduino lighting.

Status indicators for Maestro, Arduino and the two Tic controllers appear at the top right.

## Configuration changes

Saving Servo Configuration applies changes immediately to subsequent physical movement. Port, PWM, direction, Speed and Acceleration changes do not require restarting the application.
