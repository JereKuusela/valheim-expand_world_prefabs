# Rule logging

`log:` writes a line when the rule triggers (before any other actions).
Only the server writes (including singleplayer and a local-server host).

Files are UTF-8 text in `BepInEx/config/expand_world/logs/`. Each message gets a newline;
no timestamp is added. Use `<time>` (game time) or `<realtime>` (real time).
Functions and object substitutions work as usual.

## Destinations

By default, messages go to `logs/ewp_log.txt`:

```yaml
- prefab: Player
  type: state, join
  log: "<pname> joined the server."
```

Use `logFile` to pick one or more files (comma-separated). The text is resolved once:

```yaml
- prefab: Player
  type: state, join
  logFile: ewp_log, heatmap
  log: "<realtime> • <pname> • joined • <pos>"
```

Use a list for several messages. Entries can set their own `logFile`, otherwise they inherit the top-level one:

```yaml
- prefab: Player
  type: state, join
  logFile: ewp_log
  log:
  - "<pname> joined the server."
  - logFile: heatmap
    log: "<realtime> • <pname> • joined • <pos>"
```

Notes:

- Each list item is a separate entry. A multiline string is one entry.
- Names are 1–64 ASCII letters, numbers, `_` or `-`, lowercased. EWP adds `.txt`. Paths and reserved Windows names are rejected.
- Invalid values give a warning and skip that log item; the rest of the rule still runs.
- At most 32 distinct file names are tracked until restart.

## Rotation

Each log keeps up to 4 segments of 256 MiB (the active file plus archives):
`heatmap.txt`, `heatmap.1.txt`, `heatmap.2.txt`, `heatmap.3.txt` (oldest).
Records are never split. Logs rotate independently and there is no shared folder cap.
Stop the server before manually deleting or archiving files.

The old `expand_world/ewp_log.txt` is left untouched; new output goes to `logs/`.

## Configuration

Settings are in `BepInEx/config/expand_world_prefabs.cfg`. All except the first require a restart.

| Setting | Default | Meaning |
| --- | ---: | --- |
| Rule logging | true | Enable output. |
| Maximum file MiB | 256 | Size of each segment (1–4096). |
| Retained segments | 4 | Segments per log, including the active one (1–16). |
| Records per second | 1000 | Global rate limit (burst up to 100). |
| Records per rule per second | 250 | Per-rule rate limit (burst up to 25). |
| Flush interval milliseconds | 1000 | Maximum delay before pending output is flushed. |

## Dropped output

Logging never blocks gameplay. A single background worker writes all files.
Entries are skipped when rate limits are hit, the queue is full (4096 entries / 4 MiB),
or a message exceeds 8192 characters. Skips are reported as `[EWP LOG GAP]` summaries
in the affected log and in the BepInEx log, about every 30 seconds and on shutdown.

A formatting error disables that message until YAML reload. A file error disables
that destination until restart. Shutdown waits at most one second, so crashes
can lose unflushed output.
