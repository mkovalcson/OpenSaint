# URDF Docking / Editor Layout — v1.7.0

- Removed the legacy **View > Robot Head** menu action.
- The URDF lower-left controls are now two rows:
  - Collision Warning + Undock/Dock
  - Recenter Camera + Drive
- The docked **URDF Height** button is located immediately above the lower-right URDF legend.
- **Undock** opens the URDF in its dedicated window and completely collapses the URDF pane from the main editor.
- While undocked, the main servo grid spans the former URDF area and changes to two section columns:
  - Left: Eye Flaps, Nose, Eyes, Neck
  - Right: Lighting & Vents, Eye Pop, Headtop Controls
- In the detached URDF window, the lower-right URDF Height control becomes **Full Screen: Off/On**.
- **Dock** (or closing the detached window with X) returns the URDF to the main editor.
- `EditorLayout.json` persists dock state, detached window bounds/full-screen state, the docked grid/URDF splitter, and the undocked two-column splitter.
