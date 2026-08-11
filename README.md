# Expand World Prefabs

Server side scripting engine to easily create custom interactions.

Install on the server (modding [guide](https://youtu.be/L9ljm2eKLrk)).

## Features

Endless possibilities:

- Build immersive, reactive gameplay with custom interactions, events, and progression.
- Create custom objects and creatures with unique behaviors and abilities.
- Spice up regular game play with new challenges, rewards, and surprises.

## Configuration

Config file `expand_prefabs.cfg` is created automatically when the game is started. It has advanced settings that don't have to be changed for normal usage.

- Automatic file reload: If disabled, script files won't be automatically reloaded when changed.
  - This might be needed if the server host constantly triggers file changes.
  - You can use command `ewp_reload` to manually reload the script files.
  - You can use command `ewp_reload_data` to manually reload the storage file.
- Restore scale: If disabled, EWP no longer supports scaling of some non-scalable objects (like creatures).
  - This can be disabled if there already is a client side mod that enables scaling for all objects.
- Object attaching: If disabled, EWP no longer supports attaching objects to other objects.
- Server side data: If disabled, EWP no longer supports server side only data.
  - Server side data is just regular data, but prefixed with `ewp_`.
  - This reduces network traffic because the data is not sent to clients.
- Persist spawned players: If disabled, EWP no longer supports persisting EWP spawned players.
- Custom prefab names: Comma separated list of prefab names that are processed even when server doesn't recognize them.
  - This might be needed if there are modded prefabs that are not registered in ZNetScene.
  - The prefab names must be exact match, including capitalization.

Script file `expand_world/expand_prefabs.yaml` is created automatically. It starts empty, which means the mod doesn't initially do anything.

You can have multiple script files with names `expand_prefabs_*.yaml`. This is useful to organize scripts and also makes it easy to get files from other people.

Script files support [data system](https://github.com/JereKuusela/valheim-world_edit_commands/blob/main/README_data.md) of World Edit Commands. You can freely mix scripts, data and values in the same file.

Storage file `expand_world/ewp_data.yaml` is created automatically if custom keys are saved. This is meant work like global keys but these are never sent to clients.

### Scripting

See [scripting](docs/scripting.md) to get started.

Other documentation:

- [RPCs](docs/rpcs.md): List of available RPCs and their parameters.
  - RPCs are used to send specific data from server to clients.
  - [RPCs_mods](docs/rpcs_mods.md): Lists some RPCs from other mods.
- [Functions](docs/functions.md): List of available functions.
  - Functions are used for dynamic values.
- [Hacks](docs/hacks.md): Advanced explanation of some features and how they work.
- [Legacy features](docs/legacy.md): Some legacy features explained.

## Credits

Thanks for Azumatt for creating the mod icon!

Sources: [GitHub](https://github.com/JereKuusela/valheim-expand_world_prefabs)

Donations: [Buy me a computer](https://www.buymeacoffee.com/jerekuusela)
