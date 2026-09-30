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
