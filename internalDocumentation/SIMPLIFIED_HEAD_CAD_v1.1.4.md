# SimplifiedHead2 CAD integration — v1.1.4

`Models/SourceCAD/SimplifiedHead2.step` is the authoritative head assembly for the URDF preview in v1.1.4. The existing `URFDNeckAssembly.step` remains the neck source.

## Coordinate mapping

The STEP model remains at native millimetre scale and is converted at runtime with mesh scale `0.001`. The shared CAD placement maps STEP `(X,Y,Z)` to URDF head `(Z,X,Y)` with the existing head offset of `-43.096 mm` along URDF X.

## CAD-derived pivots

- **NoseBody** — rotation axis from the `ASME B18.8.2 - 0.25x0.25` spindle. URDF axis is Y; imported STEP pose is value 0.
- **NoseBasket** — rotation axis from `[HEAD-E-V2-B-02] Pin`. URDF axis is Y.
- **BrowLeftTopTilt / BrowRightTopTilt** — centers come from the output pinions of the two HS-85 servos mounted in the Nose Basket. These become the first stage of each upper-flap assembly.
- **BrowLeftTopOpen / BrowRightTopOpen** — centers come from the HS-85 pinions mounted in the upper-flap assemblies. These are nested below the corresponding tilt stage.
- **BrowLeftBottomOpen / BrowRightBottomOpen** — axes come from the lower-flap servo shaft attachments in the supplied CAD.
- **VentsOpen** — the old synthetic vent shells are gone. Each side now contains the actual Hitec HS-40 output arm/pivot strut plus five CAD vent-fin links. The HS-40 horn and every fin rotate about their own CAD-derived axes.

Exact pivot coordinates used by the generated URDF are retained in `Models/Meshes/SimplifiedHead2/pivots.json`.

## STEP appearance colors

The simplified STEP contains component appearance assignments. Runtime meshes are grouped by those assignments instead of painting the complete head a single application color. The imported palette includes:

- `#020202` near-black
- `#080B4E` deep blue
- `#737373` gray
- `#97A3DA` blue-gray
- `#E7C863` gold
- `#E9E9EB` light silver/gray
- `#F6F4E9` warm polished metal
- fallback `#A0A0A0` for components without an explicit STEP appearance

The 14 voice LEDs are intentionally dynamic rather than fixed to STEP appearance. Their idle state is **light gray `#D3D3D3`**; audio amplitude turns the center pair red first and then expands outward pair-by-pair.

## Runtime mesh size

The simplified assembly tessellates to approximately **238,197 triangles** across all static and articulated head meshes. Every generated STL is binary. The URDF preview also retains the ASCII-STL fallback added in v1.1.3.
