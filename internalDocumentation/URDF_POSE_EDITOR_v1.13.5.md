# URDF Pose Editor v1.13.5

## Automatic Library Pose image

When **Library +** saves a pose from the URDF Pose editor and no custom image was attached in the save dialog, the application automatically creates `<pose-name>_image.png` beside the Library Pose JSON.

The image is generated from the current mechanical pose. For capture, the URDF camera is temporarily changed to a clean straight-on view and fitted to the current robot head/flap/neck bounds. Pose controls, the button stack, legend, and resize handle are excluded. The rendered image is then cropped with a small margin around the head, flaps, and neck. Whip, MFRC, and microphone links are not used to expand the crop.

After the PNG is written, the user's camera orientation, zoom, and Pose UI are restored. If a custom image is attached before saving, that image is copied as before and automatic URDF capture is skipped.
