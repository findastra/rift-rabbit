# Wonderland Launchpad's own server (2026-11-21)

Astra, 2026-11-20: "every pet gets its own server ... They should all be named after their pet program, and then dash
server dash the date it was updated" (the hub's Flash Audit check A72, "Its own server"). On 2026-11-21: "Yes,
Wonderland".

- The launcher behind the Launch buttons is now `bin/wonderland-launchpad-server-20261121.exe`, Wonderland Launchpad's
  server. It was `rift-rabbit-20261024.exe`. It has no window, so it needs no third copy: the build writes the server and
  the console copy for checks, `bin/rift-rabbit-check-20261024.exe`, which keeps its name.
- Nothing in the program used its own file name: one copy at a time (its named lock), its Quit and its menu are
  unchanged.
- Self-test: three new checks (the name, the server beside the check program, the usage).
- README: the two programs, how to install a build where Startup Star runs it, and quirk 2 (Startup Star has started
  the launcher at sign-in since 2026-11-01).
- `pet.json`: `runs_on`.

Goes with the hub's pull request that writes `"server": "wonderland-launchpad-server-20261121.exe"` in Rift Rabbit's
entry in `pets.json`, where Startup Star reads it.

Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-21.
