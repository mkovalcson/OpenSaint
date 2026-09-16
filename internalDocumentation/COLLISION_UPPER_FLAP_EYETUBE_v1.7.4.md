# v1.7.4 Upper Flap Collision / Eye Tube Smoothing

- Upper flap meshes were split into the actual flap panel and the Hitec HS-85BB servo/bracket/hardware geometry.
- Only the flap panel has collision geometry and can receive collision-red highlighting.
- Servo/bracket/hardware remains visible and moves exactly as before, but is diagnostic-excluded.
- Left/right eye tubes were re-tessellated from the exact SimplifiedHead2 STEP solids at approximately 3x the prior visual density.
- Eye-tube dimensions, placement, material, and collision role are unchanged.
