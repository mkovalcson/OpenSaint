# Johnny 5 model calibration — v1.14.0

This project generation embeds the latest calibrated default extents directly in `ServoAnimator/Models/johnny5_head.urdf`.

- MFR Up/Down: 22.4299065420561 mm minimum, 65.1053864168618 mm maximum, 22.4299065420561 mm zero.
- Microphone Raise/Lower: 0 mm minimum, 10 mm maximum, 0 mm zero.

Both the ServoAnimator calibration metadata and the standard URDF prismatic joint limits carry these values.

The complete `ServoAnimator/Models` directory is intentionally retained, including STEP/source CAD files, even when those files are not runtime URDF dependencies. Future editor/model work may need to identify or reference parts by names or geometry present in the original STEP models.
