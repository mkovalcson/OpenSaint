# Timeline collision/rendering performance

## Findings

Collision Warning Off skipped the actual collision detector but still cleared collision state and allocated/reassigned a new legend foreground brush on every refresh. Enabled warnings throttle normal motion refreshes to roughly 15 Hz; the disabled path bypassed that throttle and repeatedly invalidated the UI. Command-time collision annotation also evaluated the full timeline pose before checking whether warnings were disabled.

Repeated enabled warnings restored and reapplied identical highlight materials even when the colliding meshes had not changed. Timeline playback initializes calibrated channels for the entire model; the motion renderer converted and resubmitted every channel on every render tick, including settled channels. Controller use normally populates fewer channels.

## Changes

- Turning warnings off clears collision state once and cancels any deferred refresh. Subsequent warning refreshes return immediately.
- Normal/collision status text reuses frozen brushes; collision highlights only change when a mesh enters or leaves the highlighted set.
- Disabled command-time warnings return before evaluating or pushing any timeline pose.
- Calibrated motion skips settled/disabled channels, renders only channels whose position advanced, and allocates rollback data only for movement that needs collision protection. Moving channels keep the existing elapsed-time integration and pre-render collision validation.
- Playback target cadence remains 30 Hz and calibrated motion remains display-synchronized at up to 60 Hz. Audio, command dispatch, calibration values and motion limits are unchanged.

## Focused verification

No full test suite was run. An unshown editor benchmark (no audio/controller/hardware startup) compared the same paths before and after:

| Work | Before | After |
| --- | ---: | ---: |
| 5,000 disabled-warning refreshes | 5.66 ms; 1,200,040 bytes | 0.09 ms; 40 bytes |
| 1,000 disabled command-warning evaluations | 32.73 ms; 11,212,048 bytes | 0.08 ms; 120,040 bytes |
| 5,000 full-model settled-motion updates | 127.83 ms; 53,240,040 bytes | 11.58 ms; 40 bytes |
| Legend brush replacements / 100 disabled refreshes | 100 | 0 |
| Material notifications / unchanged collision refresh | 8 | 0 |

Passed 8 focused playback-performance assertions, 37 shared calibrated-speed assertions, and 20 controller/safeguard assertions. The checks cover unchanged poses, calibrated stepper travel, warning clearing, retained collision pairs and rejection of colliding motion. These are processing-cost measurements, not GPU or real-time FPS measurements. The user's warning-on/off frame-rate difference still needs a same-sequence visual comparison on the updated build.
