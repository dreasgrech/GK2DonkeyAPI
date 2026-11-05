# GK2 Saddle Up: Critters, changes

## Unreleased

- **Translation fixes** (2026-11-05): the snail's shell in German (Schneckenhaus) and Japanese (カラ).
- **The critters' colour rows name their parts** (2026-11-04): the rat's Fur, Ears nose paws and tail, Eyes; the
  snail's Body, Shell, Eyes; the worm's Skin, Mouth, Eyes, in all 11 languages. The snail's eyes and the worm's eyes
  and mouth are parts of their own (they were the outline's colours); the worm's mouth is drawn a shade redder, so its
  colour reaches the mouth only.
- **The plugin is named "GK2 Saddle Up: Critters Mount Pack"** (2026-10-30; it was "GK2 Saddle Up: Critters"): the name BepInEx logs and the framework's Mods
  menu shows is the pack's public name, the one of its Nexus page, its Workshop item and the game's own Mods window
  (which reads it from the zip's `modinfo.json`). The id and the mounts are unchanged.
- The zip holds the DLL alone in its folder, as Saddle Up's zip does; the README.md and CHANGELOG.md that rode
  beside it in 1.0.0 stay in the repo (the user, 2026-10-29).
- **Translatable** (2026-11-01; GK2 Donkey API's translations, its docs/localization.md): the pack ships its English
  as `Languages\en.csv` beside its DLL: its three mounts' names and descriptions (the Mount Journal, the keeper's bubbles).
  A translator copies it to `de.csv`, `fr.csv`, ... (the game's language ids; GK2 Saddle Up's README says how).
  It shows with a GK2 Donkey API that reads translations (the next release); an older one ignores the folder.
  No code change.
- **French** (2026-11-01): `Languages\fr.csv`, the rat, the snail and the worm in French when the game is in French.
- **Every language the game has** (2026-11-01): German, Spanish, Brazilian Portuguese, Russian, Polish, Turkish,
  Japanese, Simplified Chinese and Korean too (`Languages\<id>.csv`).

## 1.0.0, 2026-10-28

- The pack is `GK2SaddleUpCrittersMountPack`: its DLL, plugin folder (`BepInEx\plugins\GK2SaddleUpCrittersMountPack\`), source folder
  (`packs\GK2SaddleUpCrittersMountPack\`) and zip (`GK2-Saddle-Up-Critters-Mount-Pack-1.0.0.zip`) (the user, 2026-10-28; the pack's
  mount and skin ids, its GUID and its name are unchanged).

- The rat, snail and worm as a pack of their own (until 2026-10-26 inside GK2 Saddle Up as `saddleup.rat`,
  `saddleup.snail`, `saddleup.worm`, never released).
- Each pulls its own load (GK2 Donkey API 0.40.0 enforces a mount's cargo): the rat 2 items, the worm 4, the snail 6.
- Each names its speed by the API's words (`speed = Very fast` in its mount.cfg; GK2 Donkey API 0.40.0 says how fast
  each is): the rat Very fast, the worm Slow (1.6, a touch slower than its 1.75 before), the snail Very slow.
