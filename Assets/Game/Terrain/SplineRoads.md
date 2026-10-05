# Spline passageways

Select RoadTest after Unity finishes compiling. Setup adds a Spline Terrain Road component and assigns the terrain under the first knot and the existing Road layer. If needed, run Tools > An Echo Has No Shape > Set Up RoadTest.

- Check the Terrain and Road Layer assignments before baking.
- Move spline knots in Y to set the floor elevation. Height Offset defaults to -0.15 metres; Lower Only prevents raising existing terrain.
- Floor Width is the full flat corridor width. Shoulder Width blends each side back into the baseline terrain.
- Paint Width is independent, with Paint Feather blending into the existing layers.
- Rebuild Passageways + Road Paint captures a persistent TerrainData baseline on its first use, then always rebuilds from that baseline. Repeated builds do not accumulate cuts or paint.
- Save the scene to persist tool assignments, and save the project to persist the bake.
- Add more splines to Paths on the same component. Do not add multiple tools to one terrain.
- Restore Baseline Heights + Paint removes the generated roads. Other terrain data (trees, details, holes) is not restored or changed by the tool.
- For new manual sculpt/paint work: restore first, make edits, choose Use Current Terrain as New Baseline, then rebuild. Old backup assets are retained. Rebuilding without doing this overwrites post-baseline height/paint edits, including outside the road footprint.

This version supports one unrotated, unit-scale terrain per tool; split paths at terrain boundaries. Intersecting paths should have matching elevations. Terrain resolution still limits edge detail. All baking code and baseline references are editor-only; no runtime road processing is added.

Baseline assets live in Assets/Game/Terrain/RoadBaselines. Do not change terrain size, resolution or layer ordering while an old baseline is attached; restore and begin a new baseline first.
