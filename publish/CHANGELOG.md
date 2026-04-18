- v1.51
  - Adds new parameter `<cid>` to get the character id of the client that controls the object.
  - Adds new parameter `<owner>` to spawn data to override the initial owner assignment.
  - Adds new states `join`, `leave` and `respawn` to player triggers.
  - Adds API for developers to register own parameter handlers and to use custom triggers.
  - Obsoletes parameter `<pchar>` as it returned wrong information anyways.

- v1.50
  - Adds formatting support to parameter `<time>`.
  - Adds new parameter `<realtime>` to get the real-world time (can be formatted).
  - Adds new trigger type `realtime` to trigger actions based on real-world time changes.
  - Adds new parameter `<pvisible>` to get whether the player has public visibility enabled.
  - Adds new command `ewp_reload` to manually reload the `ewp_data.yaml` file (if the file has been manually modified).
  - Fixes spawned item drops possibly disappearing (no spawn time was set so clean up for old loot was instantly triggered).
  - Removes file watcher from `ewp_data.yaml` to reduce performance issues.

- v1.49
  - Hotfix to remove debug logging.

- v1.48
  - Adds new parameter `<amount>` to get the amount of poked objects.
  - Adds new parameters`<platform>` to get the platform of the client that controls the object.
  - Breaking change: Changes the parameter `<pid>` to return only the user id instead of the full platform user id.
