
# Object filtering

## Simple filter

All wolves near a fenring, spawns as a fenring.

```yaml
- prefab: Wolf
  type: create
  swap: Fenring
  objects:
  - prefab: Fenring
    maxDistance: 50
```

## Data filter

Destroying bushes while having a special boar nearby gives a chance to spawn a mushroom.

```yaml
- prefab: Bush01_heath
  type: destroy
  chance: 0.5
  spawn: Mushroom
  objects:
  - prefab: Boar
    maxDistance: 10
    filter: string, Humanoid.m_name, Mushroom Boar
```

## Multiple filters

When stalagmite is destroyed, it spawns a wolf if nearby itemstands have correct items.

Otherwise a lightning is spawned.

```yaml
- prefab: caverock_ice_stalagmite
  type: destroy
  spawn: Wolf
# Removes items from nearby itemstands.
  poke:
  - prefab: itemstandh
    maxDistance: 10
    parameter: remove
# No object limit so each rule must apply at least once.
  objects:
  - prefab: itemstandh
    maxDistance: 10
    filter: string, item, Hammer
  - prefab: itemstandh
    maxDistance: 10
    filter: string, item, Torch

- prefab: caverock_ice_stalagmite
  type: destroy
  fallback: true
  poke:
  - prefab: itemstandh
    maxDistance: 10
    parameter: remove
  spawn: lightningAOE
  objects:
  - prefab: itemstandh
    maxDistance: 10

- prefab: itemstandh
  type: poke, remove
  # See RPCs.md for available RPCs.
  objectRpc:
  - name: RPC_DestroyAttachment
```

## Custom objects limit

If 10 wolves are nearby, the next wolf spawns as a fenring.

```yaml
- prefab: Wolf
  swap: Fenring
  objectsLimit: 10
# Note: If multiple filters match, the first one is used.
  objects:
  - prefab: Wolf
    maxDistance: 50
    filter: int, level, 3
# Two star wolf counts as 4 wolves.
    weight: 4
  - prefab: Wolf
    maxDistance: 50
    filter: int, level, 2
# One star wolf counts as 2 wolves.
    weight: 2
  - prefab: Wolf
    maxDistance: 50
# Only one fenring nearby is allowed.
  bannedObjects:
  - prefab: Fenring
    maxDistance: 50
```
