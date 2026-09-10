# Logging

The `log` field adds text to `BepInEx/config/expand_world/ewp_log.txt` when a rule runs.

```yaml
- prefab: Player
  type: state, join
  log: "<realtime> • <pname> • joined"
```

- Functions and object substitutions are supported.
  - `<biome>` logs alternate biome names when present, or the base biome name otherwise. Overlapping alternate names are separated by commas.
- Logging runs after rule selection and chance checks, before other actions.
  - A record means the rule started its actions. It does not confirm that later actions succeeded.
- Only the server writes records. This includes single player and the host of a local server.
- Existing contents are kept when restarting. EWP does not read, replace or truncate the file.
- Each call adds a record and a newline. Newlines within the text are kept.
- Timestamps are not added automatically. Use `<time>` for game time or `<realtime>` for real time.
- Worlds using the same server installation share the file.

## Configuration

Settings are in `BepInEx/config/expand_world_prefabs.cfg`.

- Rule logging (default: `true`): Enables the `log` action.
  - Can be changed while running. Pending records are written before the file closes.
- Records per second (default: `1000`): Refill rate shared by all rules.
  - Allows a burst of up to `100` records, or the configured rate if lower.
- Records per rule per second (default: `250`): Refill rate shared by all objects using one rule.
  - Allows a burst of up to `25` records, or the configured rate if lower.
- Flush interval milliseconds (default: `1000`): How often pending output is flushed.
  - Output is also flushed after `64 KiB`.
- Rate and flush settings require restarting the game.

## Performance

Text is resolved when the rule runs. A background thread writes and flushes the file, so the rule does not wait for disk access.

- Rate and queue limits are checked before resolving functions.
- The queue holds at most `4096` records and `4 MiB` of charged text storage, including reservations and the record being written.
  - Storage is charged as two bytes per character plus 64 bytes per record. Queue and stream buffers use additional memory.
- A template or resolved record can contain at most `8192` UTF-16 characters. Longer records are skipped.
- Limits skip records instead of delaying other actions or growing the queue indefinitely.
- Functions still run on the calling thread. Expensive functions can affect gameplay even when file writes are buffered.

## Missing records

Logging is intended for development and administration. It does not guarantee that every record reaches disk.

- Skipped records are counted in an `[EWP LOG GAP]` summary, normally at most once every 30 seconds and once when stopping.
  - Summaries are sent to the BepInEx log and the file. They do not mark the exact position of missing records.
  - A busy rule has its own rate limit, but many busy rules can still fill the shared queue.
- If a function throws, that rule's logging is disabled until YAML reload. Other actions can still run.
- A file error disables logging until restart. Toggling the setting or reloading YAML does not retry it.
- Shutdown waits up to one second for the writer. A crash, forced shutdown or stalled disk can lose pending records.
- The file is not rotated or size limited. Remove or archive it yourself while the server is stopped.
