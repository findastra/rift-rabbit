# Rift Rabbit

*An Intelligent App, or Pet, by Astra.*

A white rabbit that preps and tracks VR software and performance. Part of [Astra's PetOS](https://github.com/findastra/astras-pet-os).

## Pictures

![Rift Rabbit's program page: the L3 activity bubble (disconnected), tutorials and a notes box; the app is a placeholder](docs/images/rift-rabbit-program-page-20261017.png)

Rift Rabbit's program page (`index.html`) as it looks on OpenAI Codex's `codex/l3-activity-20261016` branch, which adds the L3 activity bubble, tutorials and notes and is not merged into `main` yet. The app itself is a placeholder.

Screenshots taken 2026-10-17 with GitHub Goldfish's [project-image-generator](https://github.com/findastra/github-goldfish#project-image-generator); added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-17.

## Status

Hatching. Its first native piece is built: the launcher behind the Launch buttons on its VRChat setups (2026-10-24).
The rabbit has no finished art yet.

## The launcher

Astra, 2026-10-24: "Maybe i need to turn off all of the launch dependencies so this can be the main control hub", then
"Launching is done by Rift Rabbit". Rift Rabbit's pet program page (in the hub, `apps/pet-programs/rift-rabbit-20261018.html`)
shows her VRChat setups: two headsets, five modes each (1.0 to 1.4 on the Quest Pro, 2.0 to 2.4 on the Quest 3; x.4 is
QuestLHSync, for a headset rooted with Magisk: its cameras see the base stations, so no tracker rides on it). A web
page cannot start programs, so each **Launch** button asks Rift Rabbit's launcher, `rift-rabbit-20261024.exe`
(`wonderland-launchpad-v1.0-vr-launch-v1.0`), and the launcher does it in her order:

0. VRChat's launch options in Steam (Properties > General > Launch Options) get this launch's `--ignore-trackers=` list
   (Astra, 2026-10-25: "the codes here go into the steam vrchat launch properties entry here, that is what this process
   has to change"). DoubleHip DoubleChest Tracker joins each pair into one tracker for VRChat, so VRChat ignores both of
   that pair: Double Hip [W] [C], Double Chest [ ] [M], Both all four, plus [H] in the modes with a head tracker. Steam
   writes its settings over when it closes, so when the list must change the launcher closes Steam, writes it, and
   opens Steam again ("close Steam, write, reopen"); it stops instead when SteamVR or VRChat is open. Her other launch
   options stay.
1. SteamVR's add-ons for the mode: Space Override on only in x.2, Space Sync on only in x.3, QuestLHSync
   ([CreoleVR/QuestLHSync](https://github.com/CreoleVR/QuestLHSync)) on only in x.4, so no two of them move the
   Lighthouse devices at once. SteamVR reads them when it starts, so this happens before anything can start SteamVR.
2. The streamer (Virtual Desktop, Meta Horizon Link, or Steam for Steam Link).
3. The headset and the streamer on it are her side: the step waits until SteamVR sees the headset, or until she presses
   **Next**.
4. SteamVR, then the programs SteamVR also auto-starts (she keeps that on: "fix the steam auto launch settings", so
   the launcher finds them already open and opens any that are not): VRCFaceTracking (Quest Pro, when its face tracking box is ticked), OVR Advanced
   Settings, OVR Dynamic Resolution, and the mode's own program: Space
   Calibrator in x.1 (closed the way its X does in the other modes), Space Override's overlay in x.2, Space Sync's in
   x.3. In x.4 QuestLHSync runs inside SteamVR; she looks at each base station once so the headset's cameras find
   them.
5. DoubleHip DoubleChest Tracker (its Enable Hip / Enable Chest set for her choice first), Object Track VR (not out yet,
   so its step only says so), OVR Smooth Tracking, Base Station FOV Viewer and VR Pole Overlay when ticked (their ticks also open or close them at any moment, through `/overlay`), OSCGoesBrrr, and for the Woojer vest, Voicemeeter (the vest is a
   Bluetooth audio device that Voicemeeter plays to; whether it is connected is not checked).
6. A light for each thing **Start VRChat** waits for: the headset, the Index controllers on her hands (none for hand
   tracking), every tracker on one of her spots (24 Head only in the modes with one, an object's only when it is
   chosen), and every program the launch opened. She presses it once the trackers look smooth.
7. VRChat, through a shortcut to Steam (`-applaunch 438100`); Steam adds its launch options from step 0.

Every program opens through explorer.exe. The launcher knows her VR programs and reads which VR devices SteamVR has on
from SteamVR's own log (`Steam\logs\vrserver.txt`), read only.

- **This PC only.** It listens on 127.0.0.1 (a loopback socket, so nothing off the PC can reach it), refuses a request
  from any web site, and needs the code from its settings file. It takes only the page's fixed values (headset, mode,
  streamer, Double choice, Object Track VR object, OSCGoesBrrr, Woojer vest), never a program name or a command.
- **Its settings**, never committed: `Documents\Astra's PetOS Data\rift-rabbit\vr-launch-settings-20261024.json`: the
  port (47621), the code, which device is on each of her 27 spots, the two Double pairs, and where DoubleHip DoubleChest
  Tracker is. The spots are hers (2026-10-25 drawing: 1 Right Foot [RF] to 27 Little Dance Hoop [LDH]); she sets them
  with tracker-strapper on Rift Rabbit's page opened on this PC, which sends them to `/spots`. Only her spots, only a
  device that fits each (23 Face a headset, 21 and 22 an Index controller or the headset's hand tracking, the rest a
  Vive Tracker 3.0), each tracker once, Double Hip's pair any two of the eight waist spots (usually 7 FW + 8 BW) and
  Double Chest's any two of the four chest spots (usually 17 FC + 18 BC). From them come the head tracker, both pairs
  and VRChat's ignore lists. A file from before the spots gets today's spots once.
- **A launch needs its spots**: the headset on 23 Face, a tracker on 24 Head for x.1 to x.3, both trackers of each
  Double pair it uses, and the tracker on the Object Track VR object chosen. Otherwise it is refused and says what is
  missing.
- **The page's file**, `vr-launch-page-data.js` in the same folder, says where to ask, the code, which modes are
  installed, and the spots. Without it (another PC, the public site, the launcher stopped) the buttons stay off and the
  page shows today's spots.
- What it changes outside its own folders, and only for a launch: VRChat's `LaunchOptions` in Steam's
  `userdata\<account>\config\localconfig.vdf` (a copy is kept once as `steam-localconfig-before-rift-rabbit-20261025.vdf`
  in its settings folder; nothing else in the file changes), the two add-ons' `enable` values in SteamVR's
  `steamvr.vrsettings` (a copy is kept once as `steamvr-before-rift-rabbit-20261024.vrsettings` in its settings folder),
  and Enable Hip / Enable Chest and the pairs in DoubleHip DoubleChest Tracker's `user_config.json` (its own settings
  kept once beside it as `user_config.before-rift-rabbit-20261024.json`).
- No tray icon and no window (Astra, 2026-10-21: only Startup Star is in the tray). Its menu (the tool's ON/OFF, quit)
  goes to `menu-20261021.json` and its mood to `mood-20261021.json` in `%LOCALAPPDATA%\astras-pet-os\rift-rabbit`,
  with Log Den's background-pet contract.

## The dongle map

Astra, 2026-10-26: "Can you add USBDeview ... and then also make a tool for us to run it and show a branch where each
dongle is? There are so many ways to plug them in, we want to optimize it". The page's **dongle map** asks the launcher
(`/usb-map`), which runs NirSoft's [USBDeview](https://www.nirsoft.net/utils/usb_devices_view.html) 3.10 from
`Documents\USBDeview-3.10` (downloaded 2026-10-26; its SHA-256 matches NirSoft's) to list what is plugged in, and walks
Windows' device tree from each device up to its USB controller. The page draws them as branches: controller, root hub,
hubs, then each Watchman dongle (one per tracker or controller), Valve VR Radio and other device, with its port. When
SteamVR's log names a dongle and a tracker on one line, the dongle shows its tracker. Read only.

## How to run it

The page: double-click `index.html`. It needs no install and no build step.

The launcher: Windows 10 or 11, nothing to download. Clone [log-den](https://github.com/findastra/log-den) next to this
folder first (it owns the shared farm event format and background-pet contract), then build it on your PC with the C#
compiler that ships with Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-rift-rabbit-20261024.ps1
bin\rift-rabbit-20261024.exe --startup
```

| Command | What it does |
|---|---|
| `bin\rift-rabbit-20261024.exe` (`--startup`, `--watch`) | keep the launcher listening; a second copy exits quietly |
| `--quit` | stop the running launcher |
| `--tool wonderland-launchpad-v1.0-vr-launch-v1.0 on\|off` | switch the launcher ON or OFF |
| `bin\rift-rabbit-check-20261024.exe --programs` | which of her VR programs are open and which VR devices SteamVR has on |
| `bin\rift-rabbit-check-20261024.exe --self-test` | the built-in checks (56) |

## Next steps

See [docs/handoff-claude-20261009.md](docs/handoff-claude-20261009.md).

## Limits

- No VR performance tracking yet. The launcher starts her VR software; it does not measure anything.
- Startup Star does not start the launcher at sign-in yet; until it does, start `bin\rift-rabbit-20261024.exe`.
- No pixel art yet. `sprite.json` only lists the nine moods.
- In the hub's registry as a pipeline pet ([pets.json](https://github.com/findastra/astras-pet-os/blob/main/pets.json)): it has its own repo and is not registered yet.

Desktop appearance draft updated 2026-10-11: see [desktop details](https://raw.githubusercontent.com/findastra/astras-pet-os/main/desktop-art/rift-rabbit/desktop-pet-20261011.json). This changes the desktop pet, not application features.
