# Animation Library

The Animation Library stores reusable sequence material that can be inserted into the current animation.

The Animation Library menu provides **Create Library Sequence**, **Insert Library Sequence**, **Manage Library Sequences**, and **Manage Library Poses**. A Library Sequence is reusable multi-time content whose relative command timing is preserved.

When creating a Library Sequence, the green start arrow defaults to the first command and the red end arrow to the last command. Drag either arrow to snap it to a command time; the arrows cannot cross. Use Fit or scroll if an endpoint is outside the current view. Escape cancels an active library arrow/range prompt.

Library insertion changes the current Sequence and should be saved normally afterward.

## Library Poses

A **Library Pose** is a reusable group of commands for one timeline time point. It is different from a Library Sequence, which can contain a range of commands spread over time.

To create one, double-click a command marker to open **Edit Commands**, then click **Create Library Pose**. Enter a JSON file name and description and optionally attach an image. The image is copied alongside the command JSON and is displayed in the Select Library Pose and Manage Library Poses windows. The commands currently shown in Edit Commands are copied to `Library\Commands` and their stored offsets are normalized to zero.

To use one, position the sequence cursor at the desired time, right-click the Audio Timeline, and choose **Insert Library Pose**. Select a command from the searchable Library Pose list. Every command in the selected file is inserted at exactly the current cursor time.

**Insert commands from JSON file** remains a separate generic importer. It preserves the relative timing in the selected JSON and adds the current cursor time to those offsets, so it can insert a multi-time command pattern or sequence fragment.


## Audio Timeline insertion

At the top of the Audio Timeline right-click menu are **Insert new Command**, **Insert Library Pose**, and **Insert Library Sequence**. Library Poses place all stored commands at the selected time point. Library Sequences preserve their stored relative offsets and rebase the sequence start to the selected time.

## Managing library files

The **Category** column follows **Folder**. Folder is hidden when no items are in child folders. Lists default to Category, then Filename order; unassigned items use **none**, and search includes categories.

Select an item, choose a category in the picker, and click **Set Category**. **Manage Categories…** lets you add names, rename a category for all its items, or delete it. Deleting a category assigns its items to **none**; it does not delete their files. The default **none** category cannot be renamed or deleted. Assignments are saved in each item's JSON; each Library root also keeps a `.library-categories` catalog so unused category names survive reopening.

Saving a Library Pose, either from Edit Commands or the URDF's Library + button, displays an animated busy indicator until saving finishes. The main editing window is unavailable during the save to prevent conflicting changes.

The Manage Library Sequences and Manage Library Poses windows edit descriptions directly in the table:

1. Double-click an item's **Description** cell (or select it and press F2).
2. Edit the text, then click another row to continue editing other items.
3. Close the window to automatically save all changed descriptions, including items hidden by a search filter.

If a description cannot be saved, the window stays open with the edits intact and identifies the file that failed. Resolve the problem and close again to retry. Selection-only Library browsers remain read-only.

In Library Pose browsers, selecting a row shows its attached image in the **Image** column immediately after Description. Attach an image when creating a Library Pose; the manager no longer has separate description/image panels or Save Description and Change Image buttons. Deleting a Library Pose still asks for confirmation and removes an attached image stored alongside its JSON.

## Pose editor Library buttons

While the URDF **Pose** editor is active, **Library +** appears above Collision Warning and saves the complete current URDF pose as a Library Pose. **Library Load** appears immediately below it and loads a selected Library Pose directly into the URDF model for reuse or further editing. A Pose RGB command is saved and restored with the mechanical pose. If you do not attach a custom image in the save dialog, Library + automatically creates a PNG from the current URDF pose using a clean straight-on camera, hides Pose/editor controls, and crops the image around the robot head, flaps, and neck. A custom attached image always overrides this automatic capture. The existing `Library\Commands` folder remains the storage location so older single-time-point library JSON files stay compatible.
