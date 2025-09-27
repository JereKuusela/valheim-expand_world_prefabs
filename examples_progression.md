# Examples for progression

Wiki has list of vanilla global keys: <https://valheim.fandom.com/wiki/Global_Keys>

## Remove night time spawns

On vanilla, global keys can enable extra night time spawn.

`expand_prefab.yaml`: Fulings are removed on Meadows.

```yaml
- prefab: Goblin
  type: create
  biomes: Meadows
  remove: true
```

Another option is to replace with another monster.

## Weaken night time spawns

`expand_prefab.yaml`: Fulings are weakened on Meadows.

```yaml
- prefab: Goblin
  type: create
  biomes: Meadows
  data: weak_fuling
```

`expand_data.yaml`: Halves damage dealt and removes hunt mode.

```yaml
- name: weak_fuling
  ints:
  - huntplayer, 0
  strings:
  - Humanoid.m_name, Fuling Patrol
  floats:
  - RandomSkillFactor, 0.5
```

### Swap 33% of wolves to fenrings in high mountains

```yaml
- prefab: Wolf
  type: create
  swap: Fenring
  biomes: Mountain
  minAltitude: 100
  chance: 0.33
```

Note: When swapping creatures, the spawn limit still checks the amount of original creature. This can lead to very high amount of creatures. Recommended to only swap some of the creatures.

## Harder early biomes after defeating a boss

This can be used to keep lower level areas challenging and give more loot.

`expand_prefab.yaml`: Greydwarves become stronger after defeating Bonemass.

```yaml
- prefab: Greydwarf
  type: create
  data: two_star
  globalKeys: defeated_bonemass
```

`expand_data.yaml`: Changes level to 3 (2 stars).

```yaml
- name: two_star
  ints:
  - level, 3
```

## Set a custom key when defeating a special enemy

This custom key can then be used in other entries (for example farming, spawns or bosses).

Custom keys are only saved in the server, so they don't cause network traffic.

`expand_prefab.yaml`: 1% chance for a special wolf on tall mountains.

```yaml
- prefab: Wolf
  type: create
  biomes: Mountains
  minAltitude: 100
  chance: 0.01
  data: special_wolf
# Only spawns if this key is set (perhaps activated by another quest or by admin).
# Remove this line to be always available.
  keys: specialWolf
# Custom keys can't use underscores as that is a reserved character.
  exec: <clear_specialWolf>
# Global key version. These can use underscores.
# globalKeys: special_wolf
# command: removekey special_wolf


- prefab: Wolf
  type: remove
  data: special_wolf
  exec: <save_defeatedSpecialWolf_true>
# Global key version
# command: setkey defeated_special_wolf
```

`expand_data.yaml`: 3 stars with boss UI.

```yaml
- name: special_wolf
  ints:
# 3 stars for a lot more loot (not shown on UI).
  - level, 4
  - Humanoid.m_boss, 1
# Hunt is needed to prevent taming.
  - huntplayer, 1
  strings:
  - Humanoid.m_name, Strong Wolf
# Other fields also available, check example_bosses.md.
```

## Set a custom key when building a structure

This probably needs some lore or instructions to make sense.

`expand_prefab.yaml`: Set a key when building a windmill on a tall mountain.

```yaml
- prefab: windmill
  type: create
  biomes: Mountain
  minAltitude: 100
# Can only be triggered after killing Moder.
  globalKeys: defeated_dragon
# Triggers only once.
  bannedKeys: windmillOnMountain
  exec: <save_windmillOnMountain_true>
# Server Devcommands is needed on server for these commands.
  commands:
  - broadcast center "Someone put a windmill on a mountain!"
  - event wolves {x} {z}
```
