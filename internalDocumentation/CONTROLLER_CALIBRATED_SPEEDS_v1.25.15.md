# Shared controller speed calibration — 1.25.15

Built September 15, 2026 into the normal Release output.

Xbox and Steam relative mappings now obtain target travel from the same cached calibrated motion limits used by the URDF, replacing their independent `Rate` setting. Full input uses the selected Default/Slow/Fast/Crawl profile; partial input scales the requested travel after dead-zone and inversion processing. With the existing N/C selection, each servo retains its current profile, defaulting to Default when none has been selected. Mapped Speed actions select the shared profiles, including during configuration testing.

Conversion occurs in PWM space before returning to native editor units, preserving asymmetric center calibration, child/gang reversal, and direction-specific measured speeds. A gang's common native target uses the slowest member's calibrated progress. Eye Pop uses its configured extension/retraction times (the slower channel for a shared Both Eye Pop target). For unlimited configured speed with finite acceleration, proportional jogging uses the full-stroke triangular profile's predicted peak; 0/0 uses the normal physical speed estimate. Existing downstream URDF and hardware acceleration limits remain responsible for actual motion.

The mapping inspector no longer exposes a separate units/second rate. Old JSON mappings still load; their obsolete Rate fields are ignored and are omitted when the user next saves the mapping. No user configuration files or model assets were rewritten during this change. Absolute, Set, Toggle and Library command semantics remain as before. Missing calibration providers hold relative input.

The normal controller engine and mapping-window test engine receive the same calibration resolver. Cached limits are shared with calibrated rendering and invalidated by existing servo/configuration updates, avoiding calibration fitting or fingerprint calculation on each input poll.

## Validation

- Release build succeeded with only the existing unused vent-center field warnings.
- 37 shared-controller-speed assertions: profiles, half input, N/C, limits, asymmetric neutral, reversal, direction-specific evidence, cache invalidation, ganged/individual channels, Eye Pop direction timings, both production engines, mapping testing, legacy mapping migration, acceleration-only profiles, and downstream acceleration.
- 208 controller regression assertions.
- 289 speed-calibration assertions.
- 2,205 render-loop assertions.
- 20 controller safeguard assertions.

Checks did not command the physical model. Installed assembly version verified as 1.25.15.
