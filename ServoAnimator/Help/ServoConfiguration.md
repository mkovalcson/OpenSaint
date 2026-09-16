# Servo Configuration

Servo Configuration defines how logical animation values map to the physical Maestro servos and stores speed/acceleration profiles.

## Port

Each individual physical Maestro servo has a configurable Port from `0` through `23`. Ganged headings do not have ports.

Two different physical servos assigned the same port are highlighted bold red. Repeated views of the shared NeckTilt servos under NeckTiltRight and NeckNodUp are the same physical controls and are not treated as duplicates.

Changing a shared NeckTilt servo setting in one neck gang updates the corresponding view in the other gang.

## Default, Min and Max PWM

Default is the physical home pulse used by Default Positions. Min and Max define the physical endpoint mapping for the logical servo range. Direction controls whether the physical mapping is normal or reversed.

## Speed and Acceleration profiles

Each physical Maestro control has four Speed and four Acceleration values:

- Default
- Slow
- Fast
- Crawl

Ganged rows also provide Speed/Acceleration values. Changing a gang value applies it to the physical child servos. Individual child fields can then be fine-tuned.

Timeline commands can select one of these profiles or N/C. N/C leaves the Maestro's currently active speed/acceleration unchanged.

## Save behavior

Saving Servo Configuration applies the new configuration immediately. Connected Maestro servo objects are rebuilt with the updated ports, ranges and direction. The currently active speed profile is re-sent using the new Speed and Acceleration numbers.

The built-in fallback configuration is used only when no `ServoConfig.json` is found in the active configuration folder.

## Speed Calibration

Open **Config > Speed Calibration…**, or **Calibrate Speeds…** in Servo Configuration. Calibration settings and samples are saved separately in `SpeedCalibration.json` in the active Configuration folder.

The table is automatically populated with predicted rows for every Maestro servo: Default, Slow, Fast and Crawl, in both Min-to-Max and Max-to-Min directions. Times include acceleration and deceleration for a move starting and ending at rest. Profiles with both Speed and Acceleration zero use estimated maximum physical speeds of **0.10 seconds per 60° for Nose Body and Nose Basket**, **0.11 seconds per 60° for both iris and vent servos**, and **0.15 seconds per 60° for all other servos**. Prediction plots are calculated curves, not recorded measurements.

The zero/zero estimate assumes **180° of servo shaft travel across configured Min–Max PWM**, giving full-travel times of 0.30 seconds for the nose servos, 0.33 seconds for the iris/vent servos, and 0.45 seconds for other servos. Adjust **Assumed servo Min–Max travel (°)** for each servo in this window if its shaft sweep differs. This is the servo horn angle, not a URDF joint or linkage angle. The fallback label and prediction evidence display the rate for the selected servo. The estimate limits URDF motion and populates predictions; it does not change Speed/Acceleration commands sent to the Maestro, whose zero/zero output remains unlimited.

**Use calibrated motion in URDF** is checked by default. No hardware calibration run is required to use predictions. Saving Servo Configuration automatically regenerates predictions from its current speed/acceleration arrays and PWM limits and refreshes the URDF timing. Startup also refreshes predictions from the saved configuration. Changing the controller type or period in this window and saving recalculates predictions. Explicitly unchecking and saving the URDF option remains respected on later loads and regeneration.

Actual measurements, physical time entries and physical ceiling estimates are retained. Evidence tied to previous servo settings remains visible as stale and is excluded from current timing. Generated rows are never used as measured evidence.

1. Connect the Maestro using Drive HW. In Servo Configuration, review the channel, pulse limits and speed/acceleration profiles. Use Verify to position the selected physical servo at the proposed Test start with pulses enabled, then let all existing moves finish.
2. Open calibration, choose the servo, and review Test start/end. The initial suggestion is a restricted part of the configured range; you can enter the full range after reviewing clearance. Check coupled linkages, particularly the shared neck actuators, before testing individual channels.
3. Select Default/Slow/Fast/Crawl profiles, repeats, and optional quarter-distance moves. Verify Mini versus Micro Maestro and the configured **base period**. The Mini period multiplier is not part of this value. Set settle time long enough for the physical mechanism to finish before the next traverse.
4. Confirm preparation and press **Start calibration**. This explicitly moves the physical servo, in both directions, even if Drive HW has subsequently been turned off. It does not initialize an unpowered servo, automatically home the mechanism, or run other servos. Playback, API mutations and controllers are suspended while it owns the serial connection.
5. Use **Abort** to hold the last reported pulse, or select the release-torque option before starting. The previous speed/acceleration profile is restored after completion or abort. A communication failure may prevent the requested stop action. Completed traverses are saved as they finish; successful runs end at Test start.

The results and plot measure the **Maestro output pulse**, not the physical horn. Controller-measured results are not physical feedback. Very fast traverses are marked below polling resolution. Speed zero means unlimited speed; nonzero acceleration can still produce a measurable ramp.

You can enter a physical ceiling override in µs/s, or edit **Physical seconds** for a traverse using observation/video of that same movement. Either overrides the zero/zero default assumption; a physical time applies only to its profile and direction. With neither supplied, the servo-specific fallback applies only when both Speed and Acceleration are zero. Notes can record servo type, voltage and load. Save settings/physical times to apply edits and refresh predictions. Recalibrate or replace physical timings when hardware, voltage or load changes.

Enable **Use calibrated motion in URDF** and save to apply speed/acceleration timing to timeline targets, live jogs, controller input, and Library poses/sequences including Stream Deck playback. Unmeasured channels use their configured Maestro limits; physical estimates/times can cap the predicted speed. The model updates at 30 fps using elapsed time. N/C preserves the active profile. Pause freezes preview motion; Stop cancels its remaining target. Spline targets are followed subject to the same limits, so an overly fast spline can produce lag. A seek snaps to the authored pose at the selected time, then subsequent playback resumes motion from there. The timeline and exported commands are unchanged. Eye-pop stepper timing is configured separately on the Stepper Motors tab.

Changes to a servo's port, PWM mapping, direction, speed/acceleration arrays, or the Maestro period/type make its existing measurements stale. Stale rows remain visible but are excluded from URDF timing. Turn the URDF option off to restore immediate preview positioning. Collision warnings continue to inspect the displayed geometry; calibration does not provide physical collision feedback.

Profiles must contain four speeds from 0–16383 and four accelerations from 0–255. Calibration rejects invalid profiles; the calibrated preview holds any channel with an invalid active profile until it is corrected.

## Window position within the list

During one application session, Servo Configuration remembers its vertical scroll position when the window is closed and restores that position the next time the window is opened.

## Stepper Motors

Speed Calibration has **Servos** and **Stepper Motors** tabs. The Stepper Motors table has selectable LeftEyePop and RightEyePop rows with independent full-extension and full-retraction times. Both default to **1.1 seconds** across the editor range 0–2000. Partial moves take the corresponding fraction of that time. Save timing to apply the settings; they share the Use calibrated motion in URDF option with servos.

These are URDF travel estimates. They do not change the Tic speed, acceleration, homing, or position settings. Seeking snaps to the authored position, while normal playback, controller input, and Library motion use elapsed time. Stop cancels remaining simulated movement.
