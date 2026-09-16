# Animation Editor & Player v1.0.8 UI polish

- Left hardware label now reads `Left Tic 249`.
- File menu keeps group separators only; New/Open Audio/sequence operations are one uninterrupted group.
- Standard WPF square templates were replaced for Button, ToggleButton, TextBox, ComboBox, ListBox, ListBoxItem and ToolTip.
- Standard control radius: 6 px; panel radius: 8 px.
- Major Sequence, Movie, Commands-at-Cursor, servo-grid, description and spline surfaces use rounded panel chrome and consistent inset spacing.
- Four runtime themes are available under View > Color Theme: Graphite (default/current v1.0.7 look), Steel Blue, Teal and Violet.
- Theme choice persists under `%LOCALAPPDATA%\AnimationEditorPlayer\ui-settings.json`.
- Waveform, spline and movie timeline custom rendering reads the current theme on repaint.
