# Synchronized controller preview and URDF motion — 1.25.11

Generated September 15, 2026. Built into the normal `ServoAnimator/bin/Release/net10.0-windows` output after the editor was closed.

Controller preview and calibrated servo/stepper motion now share a WPF rendering callback with a 60 Hz target. Each eligible display frame first applies the latest queued controller targets, then advances all registered URDF views. Registration order does not change this ordering. The two independent 30 Hz dispatcher timers and the controller queue's additional 30 Hz gate were removed.

The queue retains the newest value per target while preserving the order of group and child overrides. Duplicate notifications for the same rendered frame are ignored. A delayed frame performs one visual update without replaying missed frames; calibrated motion uses actual elapsed rendering time. Pause, stop, calibration replacement and focus-permission changes reset the motion clock as appropriate so forbidden or paused time is not applied on resume. Background controller polling observes permission transitions even if rendering is suspended.

Each loaded calibrated view registers with the shared render loop and unregisters on unload. The controller producer unregisters when its editor closes. Repeated load/dock operations cannot create duplicate motion subscriptions.

Controller polling remains at its existing 25 ms interval, independent of rendering and physical hardware output. The existing timeline target/slider cadence remains 30 Hz, and its cursor/transport continue to follow display frames. Calibrated model motion uses the shared 60 Hz render cadence. Collision preflight, speed limits, and the cached numerical collision engine remain in the output path. Model assets were not edited.

## Validation

- Release build succeeded; only the two pre-existing unused vent-center field warnings appeared, also repeated by WPF's temporary build project.
- `--render-loop`: 2,205 assertions covering 15–240 Hz display rates, ordering, duplicate notifications, delayed frames, target coalescing, calibrated travel, pause/resume, focus transitions, stop, load/unload, and collision rejection on a late frame.
- `--preview-cadence`: 3,659 assertions.
- `--controllers`: 208 assertions.
- `--speed-calibration`: 289 assertions.
- `--controller-safeguard`: 20 assertions.

Tests exercised numerical/model behavior without commanding physical hardware. Interactive FPS improvement is not claimed from these checks; it should be assessed with the user's normal controller movements.
