# GK2 Saddle Up: Critters, changes

## Unreleased

Nothing yet.

## 1.0.0, 2026-10-28

- The pack is `GK2SaddleUpCrittersMountPack`: its DLL, plugin folder (`BepInEx\plugins\GK2SaddleUpCrittersMountPack\`), source folder
  (`packs\GK2SaddleUpCrittersMountPack\`) and zip (`GK2-Saddle-Up-Critters-Mount-Pack-1.0.0.zip`) (the user, 2026-10-28; the pack's
  mount and skin ids, its GUID and its name are unchanged).

- The rat, snail and worm as a pack of their own (until 2026-10-26 inside GK2 Saddle Up as `saddleup.rat`,
  `saddleup.snail`, `saddleup.worm`, never released).
- Each pulls its own load (GK2 Donkey API 0.40.0 enforces a mount's cargo): the rat 2 items, the worm 4, the snail 6.
- Each names its speed by the API's words (`speed = Very fast` in its mount.cfg; GK2 Donkey API 0.40.0 says how fast
  each is): the rat Very fast, the worm Slow (1.6, a touch slower than its 1.75 before), the snail Very slow.
