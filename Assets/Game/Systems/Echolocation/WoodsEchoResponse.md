# Woods response

At runtime, `WoodsEchoSource` receives `WoodsEchoResponse` automatically if it has
no authored component. It can also be attached in Edit Mode to tune timing,
range, colour, and audio. All active `WoodsInnerBounds*` colliders count.

Each player echo inside the inner bounds restarts a three-second reply timer.
Leaving the inner bounds cancels a pending reply. An active reply fades normally;
new player pulses erase it spatially, without reviving erased green behind them.
The default audio falls back to the existing player echo clip at lower pitch.

`Fake_PineTree` in the local Polytope pack uses `WoodsIllusionTree` instead of
`IgnoreEcholocation`. The licensed prefab is ignored by Git: reapply that component
swap if restoring the pack into a fresh checkout. The terrain proxy builder copies
the marker to its generated meshes. General ignored trees still skip proxies.

Illusion trees respond normally to player echoes but remain silent to the woods
reply. The reply front clears older player highlights even on illusion trees;
a subsequent player echo retakes ownership gradually and highlights them again.

The first completed reading of `WoodsTabletEnd` triggers a global echo, records one
region completion, and calls `WoodsIllusionTree.HideAllIllusions()` in Play Mode.
It hides marked scene/proxy renderers and removes matching terrain tree instances
from runtime TerrainData clones only, preserving other prototype indices and all
original painted assets. Terrain/collider references are restored on destruction.
This can allocate substantial memory once, proportional to affected TerrainData.
Removal follows the completion pulse from its captured origin and speed, and cancels further woods replies. Re-reading does
not replay the completion. Hiding currently affects all enabled-marker
instances across loaded terrains, not just those inside the woods bounds.

Completion also reveals all splines in the container named `WoodsGuideSpline`.
`WoodsGuideShimmer` is attached automatically on completion if absent; attach it
manually to that object to author width, speed, pulse spacing, colour, fade-in,
and height offset before playing. The runtime-only ribbon follows the authored
spline elevation, with a small upward offset, and remains visible after completion.
Each section fades in only after the golden completion wave reaches it. Illusion
echo proxies are permanently clipped behind that same wave so subsequent pulses
cannot reveal removed trees. Terrain instances are removed in 0.1-second batches
as the wave reaches their positions; only runtime clones are changed.
Shimmer travels from knot start toward knot end using distance-based UVs. Reverse
the spline to reverse its flow. This does not carve or paint terrain.
