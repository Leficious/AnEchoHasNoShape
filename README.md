# An Echo Has No Shape

An atmospheric first-person exploration game about echolocation, unreliable
perception, and the difference between the world and what returns to us.

## Project

- Unity `6000.5.10f1`
- Universal Render Pipeline
- Main scene: `Assets/Game/Scenes/Main.unity`

Project-owned code and configuration live under `Assets/Game`. Downloaded Asset
Store packages use top-level `_ALL_CAPS` folders. See `PROJECT_STRUCTURE.md` and
`THIRD_PARTY_ASSETS.md` for the complete layout and package inventory.

## Restoring a checkout

This public repository is a lightweight source and configuration snapshot, not
a full copy of the game. Large meshes, textures, audio, terrain data,
third-party font binaries, and all third-party Asset Store package content are
intentionally excluded from Git.

To restore a playable checkout:

1. Install the Unity version listed above.
2. Restore ignored project art and terrain data from the separate project backup.
3. Restore the Asset Store packages listed in `THIRD_PARTY_ASSETS.md`, retaining
   their original `.meta` files whenever possible.
4. Restore the project's fonts and other ignored media from the Perforce workspace
   or project backup.
5. Open `Assets/Game/Scenes/Main.unity` and allow Unity to finish importing.

Missing ignored assets will leave unresolved references even though the scripts,
shaders, scenes, prefabs, and project settings are present.
