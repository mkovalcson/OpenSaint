# Controller mapping layout — 1.26.3

When URDF is undocked, Commands stays visible on the left. A connected Xbox or Steam controller's current mapping appears in a separate middle pane before Hardware. The middle pane follows connection presence (the green indicator), including controllers whose output is manually disabled. The most recently active connected controller takes precedence; otherwise Xbox precedes Steam. With neither connected, Commands occupies the available width.

While Steam is disconnected, its Hardware button toggles a display-only Steam mapping preview. A second click restores automatic mapping selection. The indicator stays red; the checked button and tooltip identify the preview. This works docked or undocked, uses the default bank (or No MUX), and changes neither controller output ownership nor either controller's manual-enable preference. Connecting Steam clears the offline preview override. Connected controller buttons retain their output enable/disable behavior.

Docking restores the existing embedded URDF layout. The middle pane's splitter stays adjustable; normal controller polling does not reset its widths. Canvas containment prevents the mapping from expanding header rows and pushing the sequence timeline out of reach.

Validation: Release build succeeded with only existing vent-center field warnings. Passed 22 controller layout assertions, 51 layout recovery assertions, and 20 controller display/collision safeguard assertions. Checks instantiate an unshown editor without starting physical hardware output. Rendered preview: `layout-previews/UndockedSteamMapping.png`.
