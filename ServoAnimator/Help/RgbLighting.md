# RGB and Mouth Lighting

RGBCommand is the timeline command type used for Arduino-driven eye/vent lighting. The RGB Builder creates command strings in the format expected by the hardware code.

Supported command families include clear, static color, color wipe, fade, pulse, theater chase, Cylon and rainbow effects.

## URDF eye and vent NeoPixel simulation

When a command exposes Red, Green and Blue arguments, **Build RGB Command** also shows **Select Color…**. The visual hue/saturation/brightness picker displays the selected Red, Green and Blue values separately as 0-255 numbers. Click **Apply RGB** to copy those three values into the Build RGB Command Red, Green and Blue fields. Cancel leaves the existing Build RGB Command values unchanged; those fields remain directly editable as well.

The URDF preview emulates the Arduino lighting sketch from sequence timeline time. Each eye assembly contains two 16-LED rings: a forward-facing Eye ring and a rear-facing Vent ring. Left Eye, Left Vent, Right Eye and Right Vent are simulated independently, including per-pixel color, brightness and animation timing.

LED 0 is treated as the LED at 12 o'clock on all four rings. Pixel numbering advances clockwise when viewed from the emitting side. Front Eye rings are deliberately confined to the iris center. Each front ring is displayed as 16 annular illuminated segments, one segment per Arduino pixel, and does not cast WPF PointLight/SpotLight onto surrounding geometry. The rear Vent rings retain their individual NeoPixel package visualization and tube-wash lighting. Rear Vent rings use a continuous emissive 360-degree inner-tube wash, one broad rear-facing output cone, and sixteen short-range radial flood lights so the complete inside circumference of the eye tube glows while substantial light is directed through the vents.

The circular backing behind each iris is opaque black while its front Eye ring is dark. When that Eye ring lights, the backing becomes a dark translucent diffuser with a blended emissive tint so the individual NeoPixel colors soften together through the iris opening.

The simulation is playhead-based rather than driven by a separate timer, so pause, seek and timeline scrubbing reproduce the corresponding lighting state. Live Drive still sends the original RGBCommand text to the physical Arduino.

## Eye and Vent rendered intensity

URDF Configuration provides separate **Eye Light Intensity** and **Vent Light Intensity** multipliers from `1.0x` through `20.0x`. `1.0x` is the normal calibrated appearance. These controls affect only URDF rendering; they do not alter the RGB command sent to the physical Arduino. Light range remains constrained while attenuation is scaled, so increased intensity is concentrated in the existing eye/vent lighting regions.


## URDF mouth lighting

The URDF visual model has audio-reactive mouth lighting separate from RGBCommand hardware commands.

The 14 Lip Light LEDs are dull orange when inactive and brighten from the center outward using emissive surfaces, halos and dynamic orange point lights.

The red and green side-mouth LEDs illuminate from the front of the mouth toward the rear as audio level increases. Their emissive surfaces, halos and dynamic point-light spill follow the audio response.

Audio LED Gain in URDF Configuration changes the sensitivity of both mouth-light systems from `0.5x` to `2.0x`.

## Arduino color argument order

The RGB Builder and saved sequence commands keep explicit color fields in the normal **Red, Green, Blue** order. Immediately before those commands are sent to the physical Arduino or evaluated by the Arduino emulator, ServoAnimator automatically rotates the three color arguments to **Green, Red, Blue** to match `ArduinoOpenSaintRGB.ino`. Clear, Cylon and rainbow commands have no explicit RGB triplet and are not changed.

When **Build RGB Command** is opened for an RGB command that is already configured, the builder parses the existing Text Value and restores the command type plus all available argument fields (RGB values, brightness, Eyes/Vents/Both, Left/Right/LR, delay, fade direction, pulse/cycle settings, and similar fields for that command).
