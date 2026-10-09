# Handoff from Claude, 2026-10-09

Prepared by **Claude Opus 5.5** (`claude-opus-5-5`); the repo was started by Claude Haiku 5.5 (`claude-haiku-5-5`) the same day. For Astra and whichever assistant updates `astras-pet-apps` next.

## Done

- `findastra/rift-rabbit` created: **private**, topic `pet-app`, status `hatching`. Placeholder page, `pet.json`, `sprite.json` (moods listed, no frames), README with limits, LICENSE, AGENTS.md/CLAUDE.md.
- Notices opened: findastra/astras-pet-apps#2 (register Rift Rabbit) and findastra/github-goldfish#1 (audit it).
- `astras-pet-apps` was **not** changed. Its build was run on a clean clone of GitHub `main` (no content changes; render already current).

## Not done (belongs to the astras-pet-apps update)

1. **Register Rift Rabbit** in `pets.json`. Suggested entry (Astra approves the palette):

   ```json
   {
     "repo": "rift-rabbit",
     "pet": "Rift Rabbit",
     "role": "pet",
     "art": "rift-rabbit",
     "kind": "rabbit",
     "status": "hatching",
     "published": false,
     "job": "Preps and tracks VR software and performance. Placeholder: nothing tracks yet.",
     "added": "2026-10-09",
     "asset_date": "20261009",
     "note": "DRAFT. A white rabbit with neon accents (suggested: ice white with electric violet and teal). Not drawn yet.",
     "visibility": "private"
   }
   ```

   `rabbit` is not in `cage_life.kinds` yet; add `"rabbit": { "zone": "ground", "hops": true }`. Check the palette against every other pet (no two pets share one).
2. **Generate the art.** It costs one image on Astra's OpenAI account, and the last hub handoff says credits ran out. It was not run. After the registry entry: `node scripts/generate-pets.mjs rift-rabbit`, then `npm run build`, `npm run check`, `npm test`.
3. **Copy the frames here**: put the finished frames into this repo, fill `sprite.json`, update the README limits.
4. **Visibility**: the repo is private. Make it public only if Astra says so (most pet repos are public).
5. **Behaviour**: decide which VR software and which performance numbers Rift Rabbit tracks.
