
# Legacy features

Legacy ways will be supported but they may miss some features.

Old way of object filtering. When using both old and new format, entries with the old format must be before the new format.

- objects (P): List of required nearby objects. Format is `- id, distance, data, weight, height`:
  - id: Object id.
  - distance: Distance to the object (`max` or `min;max`). Default is up to 100 meters.
    - All objects are searched if the max distance is more than 10000 meters.
  - data: Optional. Entry in the `data.yaml` to be used as filter. All data entries must match.
  - weight: Optional. How much tis match counts towards the `objectsLimit`. Default is 1.
  - height: Optional. Height difference to the object  (`max` or `min;max`).'
- bannedObjects (P): List of banned nearby objects.

Old way of poking.

- pokeDelay: Delay in seconds for poking.
- pokeParameter: Custom value used as the parameter for the `poke` type.
- pokeLimit: Maximum amount of poked objects.
  - If not set, all matching objects are poked.
- pokes: List of object information. Format is `- id, distance, data`:
  - id: Object id or value group.
  - distance: Distance to the object (`max` or `min;max`). Default is up to 100 meters.
  - data: Optional. Entry in the `data.yaml` to be used as filter. All data entries must match.

Old way of spawning.

- spawns: Short-format for spawns without parameter support.
  - Format is `id, posX,posZ,posY, rotY,rotX,rotZ, data, delay, triggerRules`.
  - Most parts are optional. For example following formats are valid:
    - `id, posX,posZ,posY, rotY,rotX,rotZ, delay`
    - `id, posX,posZ,posY, data, triggerRules`
    - `id, data, delay`
    - `id, triggerRules`
  - Id is required and supports parameters.
  - Position must be set before rotation.
  - PosY can be `snap` to snap to the ground.
- spawn: Single line short-format for spawns without parameter support.
- spawnDelay: Delay in seconds for spawns and swaps.
- swaps: Short-format for swaps without parameter support.
- swap: Single line short-format for swaps without parameter support.
