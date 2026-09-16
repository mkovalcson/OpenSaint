# URDF Pose Editor — v1.13.1

## Changes
- Eye Pop vertical controls remain at the bottom-front height but are horizontally anchored 10 mm outside the physical sides of the head. Joined mode uses one outside control; LR Split exposes both sides.
- Vent posing remains a 90° outer-eye-tube quarter arc. LR Split adds a mirrored arc on the opposite eye so Left and Right vents can be adjusted independently.
- Pose slider travel is explicitly tied to the same normalized authoring ranges used by `UrdfConfiguration.Map`; the physical endpoints therefore remain the current calibrated URDF Min/Max/Zero endpoints. Eye Pop retains its 0–2000 timeline authoring range mapped onto the calibrated mm travel.
- Added an editable MFRC rotation dial immediately to the right of the MFRC Up/Down slider. Its degree limits and inverse mapping use current URDF calibration.
- Added an RGB Command text field with a Build button above the LR Joined/LR Split control. Build opens the existing Build RGB Command window. The result previews immediately in the URDF and follows Live Drive for physical RGB output.
- RGB text is stored in the in-progress Pose draft and follows the draft between docked and undocked URDF views.
- Insert Pose writes every mechanical pose position back to timeline commands and, when present, writes the Pose RGB command at the same timestamp. An existing RGB command at that exact timestamp is replaced only when the inserted Pose contains RGB text.
