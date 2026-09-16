# Collision geometry cache and numerical motion model — 1.25.10

Generated September 15, 2026. Installed in the normal `ServoAnimator/bin/Release/net10.0-windows` output.

## Changes

- Cache the selected collision envelopes, clipped gimbal/lens contact bands, joint hierarchy, permitted shape pairs, eye-pop gating masks, and calibrated baseline exclusions.
- Capture current joint positions and link matrices once for each controller path check. All intermediate poses are evaluated in a separate numerical session; they never modify or restore WPF transforms, logical pose state, motion targets, or controller caches.
- Compute each affected link's world matrix once per sample and reuse boxes for unchanged branches. Reuse box arrays and use stack memory for separating-axis calculations.
- Use the same numerical engine for collision warning highlights, with a reusable warning session. A guard query exits at the first collision.
- Preserve the original contact regions, pair exclusions, collision tolerance, and path resolution (0.4 native servo units / 4 eye-pop units). Native motion snapshots retain asymmetric Min/Zero/Max calibration and reversal.
- Rebuilding the calibrated neutral baseline refreshes cached exclusions while retaining static geometry. Loading a new URDF creates a new scene and cache.
- Reject non-finite or excessive path lengths before converting the sample count to an integer. Missing joint geometry fails closed.

The captured numerical session can execute on another thread without accessing WPF objects. This release still validates controller output synchronously; asynchronous dispatch and stale-result handling are separate future work. Model mesh assets were not edited.

## Verification

- `--collision-performance`: 606 assertions. Compares the retained reference checker against the numerical engine for 48 varied poses, all four eye-pop states, with and without neutral-contact exclusions; checks complete hit sets, early-exit results, warning results, transformed links, baseline rebuilding, worker-thread isolation, asymmetric/reversed native controller paths, invalid input, and preservation of rendered state.
- `--controller-safeguard`: 20 assertions, including rejection before output, blocked-button state, missing-model rejection, and calibrated motion stopping before rendering a colliding step.
- `--speed-calibration`: 289 assertions.
- `--controllers`: 208 assertions.
- Release build succeeded with the two pre-existing unused vent-center field warnings (also emitted by WPF's temporary project).

An identical warmed 501-position scan, collecting complete collision sets rather than exiting early, measured 751.9 ms / 265,030 KiB allocated with the reference checker versus 3.4 ms / 195 KiB with the numerical engine (218x). Both reported 162 colliding positions. This is a collision-check microbenchmark, not a measurement of interactive rendering FPS; reference reflection and result collection are included. No physical model motion was used during testing.

## Main files

- `ServoAnimator/UrdfScene.CollisionModel.cs`: cached numerical model, independent sessions, warning bridge.
- `ServoAnimator/RobotHeadView.Safeguard.cs`: native-unit path validation using the numerical model.
- `ServoAnimator/RobotHeadView.cs`: cached shape definitions, joint origin values, baseline invalidation, reusable box fields, stack-based SAT; the old `DetectCollisionPairsReference` remains for regression comparison only.
- `ServoAnimator.Checks/CollisionPerformanceChecks.cs`: parity and performance checks.
