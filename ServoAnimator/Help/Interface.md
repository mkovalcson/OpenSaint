# Editor Interface

The main window is divided into Commands, the URDF preview, timelines, and the Movie Timeline. Commands and Movie Timeline stay visible; their former visibility toggles have been removed.

## Commands

Commands occupies the upper-left pane beside the docked URDF. Add, Edit and Delete appear before the Commands at cursor heading. Double-click an entry to edit its command group. Servo value, speed and Spline settings are edited in Edit Commands; the old Grid controls have been removed.

When the URDF is undocked, Commands expands across the available width. Its Dock URDF button provides a recovery path for the detached preview. Commands remains visible in both arrangements.

## URDF preview

The docked URDF reaches the top of the client area, beside the menus and Sequence header. Live Drive, Disable Servos, Default Positions and all connection indicators occupy a vertical strip to its right. The centered HARDWARE heading and rounded board-status box organize this strip, which stays on the main display when the URDF is undocked.

The URDF viewer can remain docked or open in its own window. A soft studio-gradient backdrop separates the model from the surrounding panels. Library actions, Collision Warning, Drive and UnDock/Dock are stacked at the lower left, followed by the camera row. Pose is between the camera-right button and bottom-center handle, with Face Reset beside it when enabled. The legend is at the lower right. In docked mode the bottom-center handle changes the vertical extent of the URDF pane.

## Timeline areas

The transport toolbar groups Zoom, Playback, Volume and Layout controls. The playback
time has a fixed-width readout; groups wrap together as the editor narrows. Buttons use
scalable icons, hover feedback and visible keyboard-focus rings.

Commands are aligned into Control, Value, Speed and Time columns. Value occupies half
the available row width and wraps long RGB commands and Play paths. The colored marker
beside a control matches its spline color. Hover a row to read its complete details.
In modified-controls inspection mode, the heading shows how many timestamps are selected.

The Hardware strip stops at the bottom of Commands; timelines and lower controls extend
to the right edge. Live Drive ON is identified by both text and green highlighting.

The Audio Timeline shows the waveform, audio clips, cursor and command triangles. Hover a triangle to see its complete command list. Use the picklist immediately left of **?** to choose Waveform top, Spline top, or Combined. Combined overlays the two graphs with the spline checkbox legend above the command triangles and the triangles above +100. The **show all** button follows the last legend item.

The horizontal bar below Commands resizes the upper pane against the timelines. Another horizontal bar resizes Audio/Spline. The vertical bar between Commands and the docked URDF adjusts their relative widths. The Commands list fills its resized pane.

## Descriptions

Sequence and Movie descriptions are shown as read-only text below their document names. The small bold **edit** link immediately after either description opens a popup editor with Save and Cancel actions. Sequence descriptions inside Movie blocks have the same link.

The editor uses the fixed **Graphite** palette. The View menu and theme choices have been removed.

**Combined** is the initial timeline layout, including the first launch after this update.
Later layout selections are still remembered. Sequence zoom uses the same **−**, **+**
and **Fit** buttons as Movie zoom. Spline legend swatches are checkbox-sized colored squares.

Both transport rows put **−**, **+** and **Fit** before a matching vertical divider
and their playback buttons. The Sequence play action is labeled **Sequence**;
Pause and Resume still indicate its active playback state. The redundant MOVIE badge is removed.

When the application closes, `EditorLayout.json` remembers the main window position, size and maximized state; editor splitter positions; the embedded URDF height; and the URDF camera orientation and zoom. The same arrangement is restored at the next launch when it remains visible on the current monitor layout. The timeline layout choice is also remembered. Legacy settings that hid Commands or Movie Timeline are ignored.

Layout is saved before the windows close, including the detached URDF window's bounds,
maximized state and camera zoom. Closing while minimized remembers the previous normal
or maximized state. Splitter sizes retain their pixel/star units, and the hidden
Audio/Spline sizes survive Combined mode. The final layout is restored after the last
document and maximized client area have loaded, so temporary startup sizes do not
overwrite the URDF height preference. If saving fails, the editor warns before closing.
