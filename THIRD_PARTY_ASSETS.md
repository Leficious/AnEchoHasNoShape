# Third-party Asset Inventory

These folders are intentionally kept outside `Assets/Game`. Their complete
contents, including scripts and shaders, are excluded by the public repository
policy and must be restored separately on a fresh checkout.

| Folder | Contents |
| --- | --- |
| `_AERO_VOLUMETRIC_FOG` | AERO volumetric fog and mist |
| `_FIRST_PERSON_CONTROLLER` | Modular first-person controller |
| `_LOW_POLY_ROCKS` | Low-poly rock collection |
| `_POLYTOPE_ENVIRONMENTS` | Polytope low-poly environment and village packs |
| `_ROCKS_AND_TERRAINS` | Low-poly rocks and terrain pack |
| `_RUINED_CITY` | Ruined-city environment assets |
| `_STONE_GRAVES` | Stone grave meshes, materials, and prefabs |
| `_STYLIZED_OBJECT_TEXTURES` | Stylized desert, forest, ice, and lava textures |
| `_STYLIZED_TERRAIN_TEXTURES` | Stylized terrain texture collection |
| `_STYLIZED_WATER` | One Click Add Water stylized water shader |

The public snapshot will not be a playable or compile-complete checkout until
these dependencies are restored. In particular, project-owned scripts reference
the first-person controller, and the scene/rendering setup references the AERO
fog package.

## Local foliage shader compatibility fix

The Polytope foliage shader needs opaque alpha-cutout rendering for AERO fog.
Its original transparent queue renders after URP captures the opaque depth;
fog then uses the background distance at leaf pixels and can hide the foliage.
The local shader uses `TransparentCutout` / `AlphaTest`, depth writes, opaque
blending, and its existing alpha clipping in each pass. Forward-only rendering
and its depth/normals prepass preserve the shader's original translucency lighting
in deferred URP. Texture cutouts and wind are preserved. This applies to pine,
fruit-tree, and generic leaf materials.

After restoring/updating the pack or regenerating the shader in Amplify, run
`powershell -ExecutionPolicy Bypass -File Tools/RepairPolytopeFoliageFog.ps1`
from the project root, then allow Unity to reimport. The shader is a licensed
dependency excluded from Git; this repair script preserves the project fix.

## Local AERO sky-depth fix

In `Assets/_AERO_VOLUMETRIC_FOG/Shaders/Volumetric Fog.hlsl`, echo fog
clearing must reject clear-depth sky pixels before evaluating surface reveal.
Use `rawDepth > 0.000001` for reversed Z, or `rawDepth < 0.999999` otherwise.
Keep normal sky fog intact. Reapply this guard after replacing the vendor file;
without it, a short camera far plane produces expanding echo halos in the sky.
Run `powershell -ExecutionPolicy Bypass -File Tools/RepairLocalVendorIntegration.ps1`
to reapply the guard to the echo-integrated fog shader. The same script wraps
`Assets/_ARCHITECT/Scripts/CustomButton.cs` in `UNITY_EDITOR`, retaining its
Inspector functionality while excluding editor APIs from standalone players.

## Grass echo wash

`PT_Grass_02` uses `PT_Grass_Mat`. Run **Tools > An Echo Has No Shape > Set Up
Grass Echo Wash** in Unity after restoring or updating Polytope. The setup adds
the project-owned `GrassEchoWash.hlsl` integration to the local plants shader,
keeps its alpha cutouts, lighting and wind, and enables the grass material's wash.
Other plant materials default to zero wash strength. The material exposes wash
strength (initially 0.25) and fade seconds (1.5 after a four-second hold).
Global echoes hold the wash behind the front and fade with the global event,
matching the accumulated tree reveal lifetime; no permanent grass tint remains.
The player wash finishes its fade independently of the travelling ring ending.

Matching terrain detail prototypes use instanced mesh rendering so Unity uses
the prefab material. Setup preserves their painted density maps and leaves
terrain texture painting alone. The wash uses the existing echo globals without
extra grass objects or overlay draw passes. Loose grass prefabs skip the normal
outline/wireframe installer. Repeated setup preserves nonzero material tuning.

## Folder migration map

| Previous path | Current path |
| --- | --- |
| `Assets/Mirza/AERO - Volumetric Fog and Mist` | `Assets/_AERO_VOLUMETRIC_FOG` |
| `Assets/Houidisoft technology/One Click Add Water -Stylized Water Shader` | `Assets/_STYLIZED_WATER` |
| `Assets/ModularFirstPersonController/FirstPersonController` | `Assets/_FIRST_PERSON_CONTROLLER` |
| `Assets/Polytope Studio` | `Assets/_POLYTOPE_ENVIRONMENTS` |
| `Assets/_ROCKS` | `Assets/_LOW_POLY_ROCKS` |
| `Assets/_ROCKSMAIN` | `Assets/_ROCKS_AND_TERRAINS` |
| `Assets/_RUINS` | `Assets/_RUINED_CITY` |
| `Assets/_SVK` | `Assets/_STONE_GRAVES` |
| `Assets/_TEXTURES1` | `Assets/_STYLIZED_TERRAIN_TEXTURES` |
| `Assets/_TEXTURESOBJ` | `Assets/_STYLIZED_OBJECT_TEXTURES` |

The low-poly-rock and stylized-terrain-texture packs each had one redundant
wrapper removed. The internal hierarchy of larger packs remains intact because
their editor scripts, sample assets, or future package updates may assume it.
