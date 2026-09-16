# RGB NeoPixel Simulation — v1.9.2

- Emulates `ArduinoOpenSaintRGB.ino` from sequence timeline time.
- Four independent 16-pixel rings: Left Eye, Left Vent, Right Eye, Right Vent.
- LED 0 assumed at 12 o'clock; numbering proceeds clockwise viewed from the emitting side.
- EyeMechanism CAD ring radius/placement used for URDF LED overlays.
- Per-LED emissive surfaces and halos plus one dynamic PointLight per ring.
- Left/right iris backing circles are opaque black when their Eye ring is dark and transparent while lit.
- Edit Commands RGB color patch/palette removed; Build… and command text remain.
- Physical Arduino output is unchanged.
