# URDF docking / resize UI — v1.7.1

- Undocking automatically expands **Headtop Controls** in the two-column servo grid.
- Removed the custom Full Screen button; the detached WPF window uses its standard window controls.
- Removed the stepped URDF Height button.
- URDF controls are a single bottom-left row: **Collision Warning**, **Drive**, **UnDock/Dock**, **Recenter Camera**.
- A small bottom-center vertical drag handle continuously resizes the embedded URDF pane from the normal editor-row height down through the audio/spline region, stopping above the Commands row.
- The continuous embedded URDF height and detached native window state are persisted in `EditorLayout.json`.
