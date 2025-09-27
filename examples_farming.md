# Examples for farming

This mod can be used to enhance farming by increasing yield or adding new rare drops.

## Increase carrot yield on Plains

`expand_prefabs.yaml`: 99% chance to inject plains_carrot data to carrots in Plains.

```yaml
- prefab: Pickable_Carrot
  type: create
  data: plains_carrot
  # Weight used instead of chance so that either this or rotten is used.
  weight: 0.99
  biomes: Plains
```

`expand_data.yaml`: Changes display name and the doubles the drops.

```yaml
- name: plains_carrot
  ints:
  - Pickable.m_amount, 2
  strings:
  - Pickable.m_overrideName, Big Carrot
```

## Random chance for different drop

`expand_prefabs.yaml`: 1% chance to inject rotten data to carrots.

```yaml
- prefab: Pickable_Carrot
  type: create
  data: rotten
  weight: 0.01
```

`expand_data.yaml`: Changes display name and the dropped item.

```yaml
- name: rotten_carrot
  strings:
  - Pickable.m_overrideName, Rotten Carrot
  - Pickable.m_itemPrefab, Guck
```

## Better yield near windmills

`expand_prefabs.yaml`: 50% chance to inject windmill_crops data to all crops when within 50 meters of a windmill.

```yaml
- prefab: Pickable_Barley, Pickable_Carrot, Pickable_Flax, Pickable_Turnip
  type: create
  chance: 0.5
  data: int, Pickable.m_amount, 2
# Inject must be false to refresh the field.
  injectData: false
  objects:
  - prefab: windmill
    maxDistance: 50
```
