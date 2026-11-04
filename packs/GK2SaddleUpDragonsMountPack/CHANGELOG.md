# GK2 Saddle Up: Dragon, changes

## Unreleased

- **The dragon's colour rows name its parts** (2026-11-04; the new names need GK2 Donkey API with them, an older one
  shows the plain rows): Scales and wings, Belly, Horns and spikes, Eyes and claws, in all 11 languages; the horns and
  spikes are a part of their own, and the outline is never recoloured. The belly keeps
  the second tone's saved colour (the same row, named).

## 1.1.0, 2026-11-01

- **Translatable** (2026-11-01; GK2 Donkey API's translations, its docs/localization.md): the pack ships its English
  as `Languages\en.csv` beside its DLL: the dragon's name and description (the Mount Journal, the keeper's bubbles).
  A translator copies it to `de.csv`, `fr.csv`, ... (the game's language ids; GK2 Saddle Up's README says how).
  It shows with a GK2 Donkey API that reads translations (the next release); an older one ignores the folder.
  No code change.
- **French** (2026-11-01): `Languages\fr.csv`, the dragon in French when the game is in French.
- **Every language the game has** (2026-11-01): German, Spanish, Brazilian Portuguese, Russian, Polish, Turkish,
  Japanese, Simplified Chinese and Korean too (`Languages\<id>.csv`).

## 1.0.1, 2026-10-30

- **On the Steam Workshop** (2026-10-30): the Workshop item holds the same DLL and a `modinfo.json` (id
  `dreas.gk2.saddleup.dragon`, name, version); subscribed, the game's Mods window lists the pack. The zip installs
  into the game folder as always.
- **The plugin is named "GK2 Saddle Up: Dragons Mount Pack"** (2026-10-30; it was "GK2 Saddle Up: Dragon"): the name BepInEx logs and the framework's Mods
  menu shows is the pack's public name, the one of its Nexus page, its Workshop item and the game's own Mods window
  (which reads it from the zip's `modinfo.json`). The id and the mounts are unchanged.
- The zip holds the DLL alone in its folder, as Saddle Up's zip does; the README.md and CHANGELOG.md that rode
  beside it in 1.0.0 stay in the repo (the user, 2026-10-29).

## 1.0.0, 2026-10-28

- The pack is `GK2SaddleUpDragonsMountPack`: its DLL, plugin folder (`BepInEx\plugins\GK2SaddleUpDragonsMountPack\`), source folder
  (`packs\GK2SaddleUpDragonsMountPack\`) and zip (`GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip`) (the user, 2026-10-28; the pack's
  mount and skin ids, its GUID and its name are unchanged).

- The dragon as a pack of its own (until 2026-10-26 inside GK2 Saddle Up as `saddleup.dragon`, never released): it
  flies, jumps with a small take-off and landing on its wings, has young and pulls no cart.
- Its speed is the API's Slow (`speed = Slow` in its mount.cfg; GK2 Donkey API 0.40.0 says how fast that is).
