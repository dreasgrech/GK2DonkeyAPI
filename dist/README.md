# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.41.1 (with Saddle Up 0.13.1; the last release 0.41.0 / 0.13.0 of 2026-10-29) | 26297960 (1.011), built 2026-10-30 13:01 (content from many mods; the dragon and the critters as packs of their own; the mounts and skins on for players, the gate removed; cargo per mount) |
| `GK2SaddleUp.dll` | 0.13.1 (needs GK2 Donkey API 0.41.1 or newer; the last release 0.13.0 of 2026-10-29) | 26297960 (1.011), built 2026-10-30 13:01 |
| `GK2SaddleUpDragonsMountPack.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; public on Nexus 2026-10-29, mods/420; the zip the DLL alone) | the external pack: the dragon embedded, built 2026-10-29 14:37 |
| `GK2SaddleUpCrittersMountPack.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; not released yet as of 2026-10-29) | the external pack: the rat (cargo 2), snail (6) and worm (4) embedded, built 2026-10-29 14:37 |
| `GK2SaddleUpDonkeySkinPack.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; public on Nexus 2026-10-28, mods/419) | the external pack: the donkey's skins Hellfire and Royal Parade and Hellfire's effect embedded, built 2026-10-29 14:37 |

The API DLL carries the list of every game member it uses (840 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout, plus a `modinfo.json` from the release after 0.13.1 on). The zip extracts
into the game folder, or into `Mods\Code\<Name>\` in the save data folder, where game 1.011's Mods window lists
it and installs it (`docs/mods-window-and-workshop.md`). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

The three pack DLLs ship as zips of their own (`make-release-zip.py`, the DLL alone in its folder, as Saddle Up's
zip; until 2026-10-29 the pack's README.md and CHANGELOG.md rode beside it): `GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip` (`BepInEx\plugins\GK2SaddleUpDragonsMountPack\`),
`GK2-Saddle-Up-Critters-Mount-Pack-1.0.0.zip` (`BepInEx\plugins\GK2SaddleUpCrittersMountPack\`) and
`GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip` (`BepInEx\plugins\GK2SaddleUpDonkeySkinPack\`). There is no combined zip: each
pack ships on its own (the user, 2026-10-27).

Players download GK2 Saddle Up (the donkey and Gerry) and, if they want more, the packs: GK2 Saddle Up: Dragon (the
dragon), GK2 Saddle Up: Critters (the rat, snail and worm), GK2 Saddle Up: Donkey Skins (the donkey's Hellfire and
Royal Parade).

What is new since 0.10.0 is CHANGELOG.md's Unreleased section (as of 2026-10-27). The mounts and skins are on for
players since this version (0.10.0 shipped them off); which mounts a player has is which packs they installed.

Sizes 2026-10-30 13:01 (`bash tools/build-dist.sh` for 0.13.1 / API 0.41.1, the 1.011 re-audit, no code change; 24 of 24
release scans green): `GK2-Saddle-Up-0.13.1.zip` 886,911 bytes: `GK2DonkeyAPI.dll` 2,691,072 bytes, `GK2SaddleUp.dll` 34,816.
That zip is the public one (uploaded 2026-10-30 morning). The afternoon's tool run overwrote the file in `release/` in
place with a rebuild carrying `modinfo.json` (887,094 bytes; nobody had said it was uploaded), which was then deleted;
the file now in `release/` is regenerated with the tool as it was at the upload and is byte-identical to the Nexus
download (sha256 compared 2026-10-30; 886,911 bytes). Since then `make-release-zip.py` refuses to overwrite an existing zip.
The next release's zip is the first with `modinfo.json`.
The three pack DLLs here are still the 2026-10-28 release builds (1.0.0): the Donkey Skin Pack's source holds the
unreleased Armored skin, so its dist DLL is restored from git after every build-dist until 1.1.0 ships.

Sizes 2026-10-29 14:37 (`bash tools/build-dist.sh` for 0.13.0 / API 0.41.0 after the journal picture's framing and
lantern fixes of that afternoon, 23 of 23 release scans green, the zips of 14:38; the three pack DLLs unchanged in
content, rebuilt with the rest):
- `GK2-Saddle-Up-0.13.0.zip` 866 KB: `GK2DonkeyAPI.dll` 2,691,072 bytes, `GK2SaddleUp.dll` 34,816 (Gerry's pack only).
- `GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip` 440 KB, `GK2-Saddle-Up-Critters-Mount-Pack-1.0.0.zip` 199 KB,
  `GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip` 34 KB: the packs of 2026-10-28, not re-released with 0.13.0.

Release scans 22 of 22 green on this build; check-dist checks the four DLLs and both packs' folders. Self-tests on the dev build
(2026-10-27, after Freeplay and the speed words): the full run 1324 passed and 2 failed (one older test still gave its
mount a SpeedMultiplier, now refused; the test was fixed), then the 43 scenarios of that group 557 passed, 0 failed,
0 warnings. The runs with each pack left out are from 14:04 (before Freeplay). The zip rehearsal of 15:50 (A, the
first start; B, over the 0.10.0 player files) passed (release-checklist.md section 5); before an upload: the
CHANGELOG heading (section 6); `docs/open-items.md` has what is still unseen
in game.
