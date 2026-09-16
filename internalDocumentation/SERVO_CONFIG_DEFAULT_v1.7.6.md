# Servo Configuration Default — v1.7.6

The built-in `ServoConfiguration.CreateDefault()` values now match the user-supplied `ServoConfig.json` dated 2026-08-17.

Startup precedence:

1. `ServoConfiguration.CreateDefault()` supplies the built-in fallback.
2. If `<Configuration Folder>/ServoConfig.json` exists, it is loaded and replaces the built-in values.
3. If the external file is absent, the built-in values remain active.

`ServoConfig.default.json` in the source tree is also an exact copy of the supplied configuration for reference.
