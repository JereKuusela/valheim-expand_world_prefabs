- v1.50
  - Adds formatting support to parameter `<time>`.
  - Adds a new parameter `<realtime>` to get the real-world time (can be formatted).
  - Adds new trigger type `realtime` to trigger actions based on real-world time changes.

- v1.49
  - Hotfix to remove debug logging.

- v1.48
  - Adds new parameter `<amount>` to get the amount of poked objects.
  - Adds new parameters`<platform>` to get the platform of the client that controls the object.
  - Breaking change: Changes the parameter `<pid>` to return only the user id instead of the full platform user id.

- v1.47
  - Adds automatic backup for EWP data files (once per day).
  - Adds automatic backup for data files (once per day).
  - Adds some new function parameters (proper, right, search, eq, even, ge, gt, large, le, lt, ne, odd, rank, small, left, findlower, findupper).
  - Adds new field `overwrite` to RPC calls to allow canceling existing delayed calls.
  - Adds default value support to `<par>`.
  - Fixes inconsistent state for repeated pokes (first poke could modify state, now all pokes use the original state).
  - Fixes `type: change` not using field `triggerRules`.
  - Improves support for byte array data type (`type: change` and field `data`).
  - Improves base64 parsing.

- v1.46
  - Fixes delayed objects possibly targeting wrong objects if the targeted object was already removed.
  - Removes `type: damage` support
  - Removes `type: repair` support.
  - Removes deprecated `type: state` triggers.
