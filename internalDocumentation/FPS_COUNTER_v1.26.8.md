# FPS counter — 1.26.8

Replaced the changed-pose/lighting count with a rolling wall-time estimate of unique WPF render callbacks. The old count depended on animation content and whether model-update callbacks ran before the measurement callback. Stationary models and camera-only movement no longer suppress samples. Repeated notifications for the same WPF rendering timestamp are counted once.

The counter uses a fixed 2,048-entry timestamp ring and performs no managed allocations while recording frames. A 500 ms Background-priority timer publishes only changed whole-number values; fixed label width avoids size changes. No model work or explicit render invalidation is performed by the counter. Hidden/unloaded views unsubscribe and reset their readings. The tooltip and Help document that this remains a render-cadence estimate, not confirmed GPU-presented FPS; input activity may still affect the actual WPF callback cadence.

Validation: Release build 1.26.8 succeeded with the existing vent-center warnings. Only 37 focused FPS assertions ran: rates from 10 through 1,000 Hz, duplicate callbacks, startup, stalls, resets and zero bytes allocated across 100,000 recording calls. No full regression suite ran.
