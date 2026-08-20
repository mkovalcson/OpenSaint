# Animation Library

The Animation Library stores reusable sequence material that can be inserted into the current animation.

The Animation Library menu provides **Create Library Sequence**, **Insert Library Sequence**, **Manage Library Sequences**, and **Manage Library Commands**. A Library Sequence is reusable multi-time content whose relative command timing is preserved.

When creating or inserting range-based content, use the timeline cursor and selection prompts shown by the editor. Escape cancels an active library arrow/range prompt.

Library insertion changes the current Sequence and should be saved normally afterward.

## Library Commands

A **Library Command** is a reusable group of commands for one timeline time point. It is different from a Library Sequence, which can contain a range of commands spread over time.

To create one, right-click a command marker and open **Edit Commands**, then click **Create Library Command**. Enter a JSON file name and description and optionally attach an image. The image is copied alongside the command JSON and is displayed in the Select Library Command and Manage Library Commands windows. The commands currently shown in Edit Commands are copied to `Library\Commands` and their stored offsets are normalized to zero.

To use one, position the sequence cursor at the desired time, right-click the Audio Timeline, and choose **Insert Library Command**. Select a command from the searchable Library Command list. Every command in the selected file is inserted at exactly the current cursor time.

**Insert commands from JSON file** remains a separate generic importer. It preserves the relative timing in the selected JSON and adds the current cursor time to those offsets, so it can insert a multi-time command pattern or sequence fragment.


## Audio Timeline insertion

At the top of the Audio Timeline right-click menu are **Insert new Command**, **Insert Library Command**, and **Insert Library Sequence**. Library Commands place all stored commands at the selected time point. Library Sequences preserve their stored relative offsets and rebase the sequence start to the selected time.

## Managing library files

The Manage Library Sequences and Manage Library Commands windows show **Folder** and **Filename** separately, support description editing, and allow the selected JSON file to be deleted after confirmation. **Manage Library Commands** also provides **Add Image… / Change Image…**, so an existing Library Command can receive or replace its attached image after it has been created. The image is copied beside the command JSON and the JSON's relative `image` reference is updated. When deleting a Library Command, an attached image stored alongside that command is deleted with it. Library Command browsers split the lower detail area between description text and the saved image.
