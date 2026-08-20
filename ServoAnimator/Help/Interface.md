# Editor Interface

The main window is divided into the servo editor, URDF preview, timelines, command inspector, and optional Movie Timeline.

## Servo Grid

Servo controls are grouped into sections such as Eye Flaps, Nose, Eyes, Neck, Lighting & Vents, Eye Pop, and Headtop Controls. Ganged controls move their child servos together; expanded child rows allow individual inspection or movement where supported.

When the URDF is undocked, the servo grid expands into two columns. The right column begins with Lighting & Vents and Headtop Controls is automatically expanded. A Dock button under Headtop Controls provides a recovery path if the detached URDF window is hidden or off-screen.

## URDF preview

The URDF viewer can remain docked or open in its own window. The bottom-left controls are stacked as Collision Warning, Drive, UnDock/Dock, and the ← / Recenter / → camera row. In docked mode a small bottom-center handle changes the vertical extent of the URDF pane.

## Timeline areas

The Audio Timeline shows the waveform, audio clips, cursor and command triangles. The Spline Editor sits below it when visible. The Commands view is directly below the Spline area and lists commands at the current cursor position.

A splitter changes the relative Audio/Spline height. Another splitter changes the Servo Grid/URDF width while the URDF is docked.

## Descriptions

Sequence and Movie descriptions have compact editors. Movie Description expands upward from the Movie Timeline area so it does not reuse the Sequence Description overlay.
