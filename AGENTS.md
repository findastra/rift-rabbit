# AGENTS.md: Rift Rabbit

Rift Rabbit is a pet by Astra (repo `findastra/rift-rabbit`). It is a **hatching** placeholder.

Rules for any assistant working here:

- Follow the pet file rules (https://github.com/findastra/astras-pet-os/blob/main/PET-FILES.md) and the pet words (https://github.com/findastra/astras-pet-os/blob/main/PET-WORDS.md). `pet.json` must stay valid, `sprite` and `entry` must be real files, and `status` is hatching, growing or grown.
- Work left for someone else is a card in a `handoffs/` folder. Registering this pet in the Cage is card 008 in `findastra/astras-pet-os`. Do not edit the Cage from this repo.
- Do not add keys, `.env` files or personal data. Public credit is Astra.
- Record the model and date of any change in `pet.json` `built_with`.
- **Launching is Rift Rabbit's** (Astra, 2026-10-24: "Launching is done by Rift Rabbit"). The launcher
  (`rift-rabbit-20261024.cs`, `vr-launch-20261024.cs`) listens on 127.0.0.1 only, checks its code and where the page was
  opened from, and takes only the page's fixed values: never let a request name a program, a path or a command. Every
  program opens through explorer.exe, and it never stops a program by force. Outside its own folders it changes only
  the two SteamVR add-ons' `enable` values and DoubleHip DoubleChest Tracker's Hip / Chest and pairs, for a launch.
- The lights on the pet program pages (which programs are open, which VR devices are on) are Inference Iguana's
  (Benchmark Rock's program-watch and device-watch). The launcher keeps its own short list of her VR programs and reads
  SteamVR's log for itself; it never writes the lights' file.
- No tray icon, no window: its menu and mood go to Log Den's background-pet files, built from `..\log-den\contracts`.
  `--self-test` checks each rule; add a check when you add one.

Handoff and suggested hub registry entry written by Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09: see `docs/handoff-claude-20261009.md`.

Desktop appearance revision by OpenAI Codex (GPT-6), 2026-10-11. See https://raw.githubusercontent.com/findastra/astras-pet-os/main/desktop-art/rift-rabbit/desktop-pet-20261011.json; runtime behavior is in astras-pet-os.

Pictures moved to the hub (one art source, Flash Audit A58, Astra 2026-10-18): every picture of Rift Rabbit lives only in [findastra/astras-pet-os](https://github.com/findastra/astras-pet-os) (`desktop-art/rift-rabbit/`); this repo links to them and keeps no copy, so change a picture there, never here. By Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
The launcher (`rift-rabbit-20261024.cs`, `vr-launch-20261024.cs`), built by Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-24.
