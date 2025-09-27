# Examples for bosses

This mod can be used to make bosses more difficult.

## Two stars on all bosses

Unfortunately not visible because bosses have different UI.

```yaml
- prefab: Eikthyr, gd_king,Bonemass, Dragon, GoblinKing, SeekerQueen
  type: create
  data: int, level, 3
# Inject must be false to refresh the boss health.
  injectData: false
```

## Stronger bosses

10% chance for a much stronger variant:

```yaml
- prefab: Bonemass
  type: create
# Inject is false on default for data entries.
  data: ultra_bonemass
  chance: 0.1
```

`expand_data.yaml`: Changes multiple stats as an example.

```yaml
- name: ultra_bonemass
# Could use level here too.
  strings:
  - Humanoid.m_name, Ultra Bonemass
# Raid event constantly spawns enemies.
  - Humanoid.m_bossEvent, army_bonemass
  floats:
  - Humanoid.m_runSpeed, 50
# 50% more damage.
  - RandomSkillFactor, 1.5
# Slightly different health prevents the star based health (at least until boss is damaged and healed back to full).
  - max_health, 10000
  - health, 10000.1
```

## Bonemass: Summons different enemies

50% chance to summon a different enemy when near Bonemass:

```yaml
- prefab: Blob
  type: create
  chance: 0.5
  swap: BlobElite
  objects:
  - prefab: Bonemass
    maxDistance: 50
- prefab: Skeleton
  type: create
  chance: 0.5
  swap: Draugr
  objects:
  - prefab: Bonemass
    maxDistance: 50
```

50% chance for two stars when near Bonemass:

```yaml
- prefab: Blob, Skeleton
  type: create
  chance: 0.5
  data: int, level, 2
# Inject must be false to refresh the creature health.
  injectData: false
  objects:
  - prefab: Bonemass
    maxDistance: 50
```

## Moder: Ice blast can spawn hatchlings

In the ground:

```yaml
- prefab: IceBlocker
  type: create
  chance: 0.1
  swap: Hatchling
```

In the air:

```yaml
- prefab: dragon_ice_projectile
  type: create
  chance: 0.1
  spawn: Hatchling
```
