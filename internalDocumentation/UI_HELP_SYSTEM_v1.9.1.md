# Help System v1.9.1

- Added native WPF `HelpWindow` with Contents/Search and FlowDocument rendering.
- Added lazy `HelpCatalog`: topic index loads only when Help opens; topic bodies are cached on demand.
- Added application-wide context-sensitive F1 through `HelpSystem.Topic`.
- Main editor routes F1 to Audio Timeline, Servo Grid, Spline Editor, Commands, Movie Timeline, URDF Viewer, and Live Drive topics.
- Configuration/editor dialogs provide window-level F1 topics for Servo Configuration, URDF Configuration, Edit Commands, RGB Builder, Animation Library, Set Paths, export, and detached URDF.
- Help is unavailable while playback is Running or Paused. Starting playback closes any open Help window.
- Bundled 17-topic Markdown help set is copied to build and publish output under `Help\`.
- File > Help now includes Contents & Search, Context Help (F1), Controls & Hotkeys, and About.
