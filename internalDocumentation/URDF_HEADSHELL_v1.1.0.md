# HeadShell STL integration — v1.1.0

The previous synthetic head shell (box/radius primitives plus 72-segment eye sleeves) is replaced by the supplied `HeadShell.stl`.

## Measured STL references

- Native STL units: millimetres
- Overall bounds: approximately 317.524 mm wide × 142.113 mm tall × 228.346 mm fore/aft
- Eye tube centers: X = ±99.822 mm, Y = 0 mm in the STL source frame
- Eye tube outer radius: 44.450 mm
- Eye tube inner radius: 42.799 mm
- Eye tube axial span: 133.096–228.346 mm (95.250 mm long)

## URDF transform

The STL is used directly by the renderer with `scale=0.001 0.001 0.001` and visual RPY `1.570796 0 1.570796`. The axial offset places the back of the STL eye tubes at the old URDF face reference X=0.090 m.

Moving facial components are resized around the STL tube radius. The STEP upper/lower flap meshes use one common scale so their outer edges align to the STL tube outer edges.
