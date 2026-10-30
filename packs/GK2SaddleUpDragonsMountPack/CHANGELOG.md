# GK2 Saddle Up: Dragon, changes

## 1.0.1, 2026-10-30

- **On the Steam Workshop** (2026-10-30): the zip carries a `modinfo.json` (id `dreas.gk2.saddleup.dragon`, name,
  version) and its content is the Workshop item's; subscribed, the game's Mods window lists the pack. The zip
  installs into the game folder as always.
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
