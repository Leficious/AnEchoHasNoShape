# Spline passageways

Select RoadTest after Unity finishes compiling. Setup adds a Spline Terrain Road component, assigns the active terrain tiles, and selects the existing Road layer. If needed, run Tools > An Echo Has No Shape > Set Up RoadTest. Existing single-terrain components keep their captured baseline when converted.

- Add every terrain tile crossed by a path to Terrains (or click Add All Active Terrains). Each tile must include the selected Road Layer and use its own TerrainData asset. A spline can cross tile boundaries without being split.
- Move spline knots in Y to set the floor elevation. Height Offset defaults to -0.15 metres; Lower Only prevents raising existing terrain.
- Floor Width is the full flat corridor width. Shoulder Width blends each side back into the baseline terrain.
- Paint Width is independent, with Paint Feather blending into the existing layers. The edge uses 16 coverage samples per paint texel. For a visibly soft blend, keep Paint Feather around four alphamap texels wide; the Inspector shows this for the coarsest assigned tile and offers a one-click adjustment. Existing components retain their authored value.
- Rebuild Passageways + Road Paint captures a persistent TerrainData baseline for heights on its first use. Road paint now starts from the terrain's **current** texture layers, never from that baseline or a previous paint snapshot. Only the road footprint is altered, so manual paint elsewhere on the tile stays intact. Before each paint bake, an immutable pre-bake `.bytes` backup is saved in `RoadBaselines`.
- Save the scene to persist tool assignments, and save the project to persist the bake.
- Add more splines to Paths on the same component. Each terrain tile may belong to only one road component; each tile has its own baseline and paint snapshot.
- Restore Baseline Heights Only restores terrain heights but leaves all current texture painting, including road paint, untouched. Restore Last Paint Backup on the component can return a tile's full texture paint to just before its most recent road bake; it first saves another backup of the current paint. Other terrain data (trees, details, holes) is not restored or changed by the tool.
- For new manual sculpt work: restore heights first, make edits, choose Use Current Terrain as New Baseline, then rebuild. Old backup assets are retained. Rebuilding without doing this overwrites post-baseline height edits, including outside the road footprint. Manual paint outside the road footprint can be done before or after any bake.

Road paint is deliberately non-erasing outside the current spline footprint. Moving or removing a spline does **not** automatically remove its old painted road. Clean up the old strip with Unity's terrain paint brush, or restore a paint backup if appropriate. Painting directly within the new road footprint is blended toward the Road layer by design.

Terrain tiles must be unrotated and unit-scale. The sampled center of every road must remain within at least one assigned tile. Intersecting paths should have matching elevations. Terrain resolution still limits edge detail. All baking code and baseline references are editor-only; no runtime road processing is added.

Baseline and pre-bake paint backup assets live in Assets/Game/Terrain/RoadBaselines. They are local and ignored by Git, so copy them separately if you want them on another machine. Do not change terrain size, resolution or layer ordering while an old baseline is attached; restore and begin a new baseline first.
