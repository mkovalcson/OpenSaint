# rearCover STEP integration — v1.1.6

Source: `ServoAnimator/Models/SourceCAD/rearCover.STEP`

The rear-cover STEP is a single styled CAD solid with presentation color `#97A3DA`. Its broad mounting plane is the large planar face centered near native `(-0.587, 7.203, -1.587) mm`. The runtime mesh is rotated 180° about native Y so the cover depth points behind the head, then translated so that mounting face lies on the `SimplifiedHead2.step` native back datum `Z=0`.

The resulting pre-transformed mesh bounds in SimplifiedHead2 native coordinates are approximately:

- X: `-139.113 .. +137.889 mm`
- Y: `-54.869 .. +52.567 mm`
- Z: `-74.327 .. +0.350 mm`

The small +0.350 mm excursion is edge/fillet geometry; the broad mounting plane itself is registered to Z=0. The mesh is attached to `head_link`, so it follows NeckTurn, NeckNodUp and NeckTiltRight with the complete head.


## v1.1.12 color update
`rearCoverColor.step` is now the appearance authority. Its Body1 `STYLED_ITEM` uses `Paint - Enamel Glossy (Grey)`, RGB 179/179/179 (`#B3B3B3`). The existing registered rear-cover geometry is retained; only its material source changed.
