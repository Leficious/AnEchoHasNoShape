# Project Backlog

## Iceberg echo artifacts on large central icebergs

**Status:** Fix implemented; awaiting Play Mode visual validation  
**Area:** Glacial Ocean / siren and player echo rendering  
**Reported:** 2026-09-29

### Symptom

Some icebergs—especially the large icebergs near the middle of the glacier route—show strange visual artifacts during echo effects. The behavior may resemble an older siren jitter/flicker effect.

### Current findings

- No script currently moves or jitters iceberg transforms.
- The obsolete siren/player clash flicker has been removed from both mesh and terrain echo paths. Siren overwrite/subtraction remains intact.
- The echo wire pattern now uses smoothly blended triplanar projection instead of abrupt normal-axis switches.
- The accumulated outline remains an extruded mesh shell, but its default width has been reduced from `0.045` to `0.025` to reduce self-intersection on large irregular icebergs.
- Both moving-ring and outline overlays are transparent additive geometry rendered after fog. Closely overlapping surfaces may also show depth-precision or overdraw artifacts on large meshes.

### Reproduction notes for next session

1. Remove the clash-only shader calculation and its C# globals/settings, without changing siren overwrite/subtraction logic.
2. Retest the large central icebergs. If artifacts remain, set global echo wireframe strength to `0` temporarily. If that resolves them, replace or soften the normal-selected projection.
3. Test a problematic iceberg with outline width `0`, then with outline front-face culling disabled. If this resolves it, use a scale-aware outline or a screen-space outline for problematic meshes.
4. If neither isolates it, inspect the source mesh for overlapping shells, inverted normals, open geometry, or multiple active renderers, then test depth bias/ZTest on the two echo overlays.

### Constraints

- Preserve the sirens' deceptive overwrite/subtraction behavior.
- Remove player-versus-siren clash visuals; they no longer match the finalized override mechanic.
- Do not globally weaken the echo aesthetic just to accommodate a few large meshes; prefer a per-object override or a mesh-safe rendering path.
