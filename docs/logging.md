# Rule logging

`log:` writes when a selected rule passes its chance checks, before its other actions.
A record means the rule started; it does not confirm that later actions succeeded.
Only the server writes, including singleplayer and a local-server host.

The [logging smoke fixture](logging-smoke.yaml) contains only log actions for
native startup, YAML reload and restart checks in a disposable installation.

Files are UTF-8 text in `BepInEx/config/expand_world/logs/`. Records receive a newline;
existing newlines inside a message are preserved. No timestamp is added automatically.
Use `<time>` for game time or `<realtime>` for real time. Functions and object
substitutions work as before. Worlds sharing an installation share these files.

## Destinations

Existing scripts keep working and write to `logs/ewp_log.txt`:

```yaml
- prefab: Player
  type: state, join
  log: "<pname> joined the server."
```

Choose another file:

```yaml
- prefab: Player
  type: state, join
  logFile: heatmap
  log: "<realtime> • <pname> • joined • <pos>"
```

Copy the same entry to several files. The text is resolved once:

```yaml
- prefab: Player
  type: state, join
  logFile: ewp_log, heatmap
  log: "<realtime> • <pname> • joined • <pos>"
```

Produce different messages from one rule:

```yaml
- prefab: Player
  type: state, join
  log:
  - logFile: ewp_log
    log: "<pname> joined the server."
  - logFile: heatmap
    log: "<realtime> • <pname> • joined • <pos>"
```

A `log:` list of strings produces separate entries. Structured entries can also
use a list of strings. These are separate entries, not one joined multiline message.
An existing multiline string remains one entry. Further structured nesting is rejected.

A top-level `logFile:` is inherited by structured entries without their own
`logFile:`. An explicit entry destination replaces that default. Without either,
use `ewp_log`. Only `logFile:` splits on commas; message commas remain text.
Duplicate destinations within one message are removed. Different messages targeting
the same file remain separate entries.

Names use 1–64 ASCII letters, numbers, underscores or hyphens. Names are lowercased;
EWP adds `.txt`. Paths, extensions, empty names and Windows reserved device names
are rejected. Use comma-separated text for destinations, not a YAML sequence.
Invalid logging values produce a warning and skip the affected logging item;
other messages/actions in valid YAML can still run. Syntax errors in the YAML itself
still use EWP's normal file-loading error handling.

Files are created on first use. Removing a destination from YAML drains its accepted
records and closes the writer, preserving files. The process tracks at most 32
distinct destination names, including `ewp_log` and names used before a YAML reload.
Restart to reset that registry. This also bounds failed-state tracking without
retrying failed files every time YAML is reloaded.

## Rolling retention

Defaults: 256 MiB per segment, four segments per named log **including the active
file**. Up to 1 GiB of output is retained per log under these defaults:

| File | Contents |
|---|---|
| `heatmap.txt` | Current output |
| `heatmap.1.txt` | Most recent completed segment |
| `heatmap.2.txt` | Older segment |
| `heatmap.3.txt` | Oldest retained segment |

Before the next complete record would exceed the active segment, EWP flushes and
closes it, removes the oldest archive, shifts the archives, and starts a fresh file.
Records are never split between files. Rotation starts after the first segment
fills; deleting old history begins once all retained slots are occupied.

Each destination rotates independently. Two logs can retain up to 2 GiB combined
with default settings. Inactive logs remain on disk until manually removed.
There is no shared folder-size cap. Reducing the retained segment count removes
excess EWP archive slots on the destination's next open. Previously oversized
segments, such as after lowering the size setting, age out through rotation;
EWP does not truncate existing records to enforce a newly lowered byte limit.

Restart appends to the active file, rotating on the next write if needed. Missing
archive slots after an interrupted rotation are tolerated. Rotation is not an
atomic transaction; a forced shutdown during rotation can retain fewer segments.
Only exact archive names for that destination are managed. Unrelated files are preserved.

Stop the server before manually removing or archiving files. Live deletion/reopening
is not supported. To clear a log fully, remove its active file and numbered archives.
The old root-level `expand_world/ewp_log.txt` is left untouched when upgrading;
new output starts in `logs/`. Update external download/processing scripts accordingly.

## Configuration

Settings live in `BepInEx/config/expand_world_prefabs.cfg`.

| Setting | Default | Meaning |
|---|---:|---|
| Rule logging | true | Enable output; can change while running. Accepted output drains before close. |
| Maximum file MiB | 256 | Size of each active/archive segment. |
| Retained segments | 4 | Number of segments per log, including the active file. |
| Records per second | 1000 | Global entry refill rate; burst up to 100, or the rate if lower. |
| Records per rule per second | 250 | Shared by every message, action type, player and object using one loaded rule; burst up to 25, or the rate if lower. |
| Flush interval milliseconds | 1000 | Flush deadline per file while output is pending; also flush after 64 KiB. |

Size, retention, rate and flush settings require restart. Size accepts 1–4096 MiB;
retention accepts 1–16 segments. These are configurable limits, not a fixed 1 GiB
maximum after changing defaults. Copying one entry to multiple files consumes one
rate allowance. Separate messages from one rule each consume an allowance.

## Buffering and missing output

One background worker owns all writers and rotation. Substitutions run on the
calling thread, so expensive functions can still affect gameplay. A stalled filesystem
operation can delay every destination. Additional logging threads are not created.

Admission checks happen before formatting, with no queue allocation for rejected
entries. The overall queue retains at most 4096 entries and 4 MiB of charged storage,
including reservations and the entry being written. Text is charged as two bytes
per character plus record/destination-reference overhead. Writer buffers, source
configuration and runtime overhead use additional memory. Limits are shared rather
than multiplied per destination. A template or resolved entry is limited to 8192
UTF-16 characters. Overload/oversized entries are skipped rather than blocking gameplay.

`[EWP LOG GAP]` summaries identify the affected filename and skipped counts for rate,
capacity/shutdown, size and formatting failures. They appear in that log and the
BepInEx diagnostics, normally every 30 seconds and on shutdown. Copies missed by a
shared admission limit are attributed to each affected destination. Summaries are
not the exact position of gaps. A failed destination can only report via diagnostics.

A formatting exception disables that message until YAML reload; other messages and
rule actions remain available. A file/rotation error disables only that destination
until restart and reports its name. Toggling logging or reloading YAML does not retry
failed destinations. Copies are best effort, with no all-or-nothing delivery guarantee.

Shutdown waits at most one second. Crashes, forced shutdown and stalled writes can
lose pending/unflushed output. Separate files protect retention, not delivery of every event.
