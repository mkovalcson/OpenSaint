# Controller feedback — 1.26.4

Removed the duplicate large heading and its explanatory subtitle from both controller configuration windows. Window titles and mapping tabs remain.

Main-screen mappings now receive current controller samples. Both main and configuration diagrams highlight active buttons, stick axes, triggers, trackpad touch/X/Y/click/pressure, and available Steam controls. Visual activity is independent of mapping assignment or output arming. Axis dead zones suppress noise; triggers and pressure use a small visual threshold. Accelerometers keep a neutral baseline for activity highlighting. Disconnect clears feedback. No input actions are emitted by the visual-feedback path.

Configuration diagrams retain mapping names in their cards. Analog raw values and mapped values appear beside them: toward the controller for inner cards and outward for Steam's outer cards. Added artwork margins accommodate the outer readings while preserving clickable control locations. Mapped output continues to use the existing engine's readings and arming rules.

No MUX already uses its own mapping dictionary, saved alongside all four MUX banks. This structure remains intact. Its tooltip explains preservation; enabling No MUX hides the tabs and exposes shoulder mappings, and disabling it restores access to the original banks.

Validation was deliberately limited: Release build, 21 focused controller-feedback assertions (including save/reload of all four MUX banks), and visual inspection of Xbox/Steam configuration renders. No full regression suite or physical model testing was run. Existing vent-center field warnings remain unchanged.
