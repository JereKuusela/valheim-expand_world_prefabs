# Expand World Prefabs: RPCs from mods

This contains some RPCs that mods use.

See [RPCs](RPCs.md) for explanation how RPCs work.

## Client rpcs

Player scaling mod.

```yaml
# Scales a player.
  clientRpc:
  - name: ScalePlayer
    target: all
    1: zdo, <zdo>
    2: vec, "scale"
    3: float, "meters"
```
