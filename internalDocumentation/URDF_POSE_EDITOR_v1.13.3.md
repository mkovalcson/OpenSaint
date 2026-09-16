# URDF Pose Editor — v1.13.3

## Changes
- Added **Library +** and **Library Load** above Collision Warning while Pose mode is active. Library + saves the complete current pose as a reusable Library Pose; Library Load applies a selected Library Pose directly to the URDF model.
- Updated all user-facing single-time-point library terminology to **Library Pose** while retaining the existing Library\Commands storage folder and internal serialization methods for backward compatibility.
- Moved the microphone URDF mount another 4 mm rearward.
- Reworked Vent quarter-arcs to project directly from points on the physical 44.45 mm-radius outer eye-tube edge; dragging now snaps to the nearest point on that true projected 3-D arc.
- Added eye-gimbal reset buttons just outside the inner-facing edge of each visible eye target circle. Joined mode resets both eyes; LR Split resets each eye independently.
