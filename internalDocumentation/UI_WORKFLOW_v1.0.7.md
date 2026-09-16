# Animation Editor & Player v1.0.7 — UI / Workflow Pass

This generation implements the UI/workflow recommendations from the v1.0.6 review, except the two items explicitly excluded by the user: a docked command inspector replacing the Edit Commands dialog, and clickable servo selection inside the 3D Robot Head.

Implemented:

- Sequence and Movie work areas use separate visual accents and labels.
- Shared dark WPF styling for common buttons, toggles, text boxes, combo boxes, list selections, tooltips, and separators.
- Sequence and Movie transport controls use recognizable transport glyphs and tooltips.
- Controls & Hotkeys help window plus a ? shortcut button.
- Waveform command-marker hover summaries.
- Commands-at-Cursor supports Add, Edit, selected Delete, Delete key, and double-click editing.
- Sequence and Movie filename indicators show `*` for unsaved changes.
- Servo grid is grouped into collapsible functional sections.
- Live Drive has an obvious active visual state in addition to USB status lights.
- Movie timeline has independent zoom, fit, horizontal scrolling and middle-button panning.
- Movie block drag displays a gold insertion line and each block has a visual drag grip.
- Movie block tooltips show filename, duration, sequence description, audio files, full path, and current/dirty status.
- Animation Library browser has live search across pathname, description and audio filenames; DataGrid column sorting remains available.
- Spline hover/selection inspector shows exact servo, time and value.
- Menus reorganized into File, Edit, Animation Library, Tools, View and Help.
- Non-modal bottom status line reports successful save/load/insert/copy/export operations.

Version: 1.0.7
Generated: 2026-08-07
