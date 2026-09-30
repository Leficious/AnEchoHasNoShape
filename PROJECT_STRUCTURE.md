# Project Structure

## Project-owned content

- `Assets/Game/Core` — global game state, bootstrapping, respawning, and debug tools.
- `Assets/Game/Systems` — gameplay and rendering systems grouped by feature.
- `Assets/Game/UI` — menus and reusable interface systems.
- `Assets/Game/Art` — project-authored fonts, icons, sky assets, textures, and props.
- `Assets/Game/Audio` — project-authored sound files.
- `Assets/Game/Terrain` — terrain data, paint layers, and generated terrain layers.
- `Assets/Game/Config` — input and render-pipeline configuration.
- `Assets/Game/Resources` — runtime-loaded materials and assets.
- `Assets/Game/Scenes` — playable scenes.

## Third-party content

Downloaded packages remain at the top of `Assets` and use `_ALL_CAPS` names so
they are visually distinct from project-owned work. Do not place original game
scripts or art inside these folders; package reimports may overwrite them.

## Moving Unity assets

Always move an asset together with its `.meta` file. The `.meta` GUID is what
keeps scene, prefab, material, and script references intact. After a large move,
open Unity once, allow the Asset Database refresh to finish, and check the
Console before saving scenes or prefabs.

## Repository policy

The root `.gitignore` keeps Unity-generated state out of Git, excludes downloaded
third-party art, and excludes large media/terrain binaries. Scripts, shaders,
scenes, prefabs, materials, render settings, and project configuration remain
eligible for source control. See `THIRD_PARTY_ASSETS.md` for packages that must be
restored separately on another machine.

This repository policy is intentionally a lightweight code/configuration
snapshot, not a complete portable copy of the game. A fresh checkout must
restore the ignored art, audio, terrain data, and matching `.meta` files from a
separate backup before scene references can be expected to resolve.
