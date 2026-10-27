# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.40.0 (not released yet; the last release is 0.30.0) | 25910882 (1.010), built 2026-10-27 15:44 (content from many mods; the dragon and the critters as packs of their own; the mounts and skins on for players, the gate removed; cargo per mount) |
| `GK2SaddleUp.dll` | 0.12.0 (needs GK2 Donkey API 0.40.0 or newer; not released yet; the last release is 0.10.0) | 25910882 (1.010), built 2026-10-27 15:44 |
| `GK2SaddleUpDragon.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; never released) | the external pack: the dragon embedded, built 2026-10-27 15:44 |
| `GK2SaddleUpCritters.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; never released) | the external pack: the rat (cargo 2), snail (6) and worm (4) embedded, built 2026-10-27 15:44 |

The API DLL carries the list of every game member it uses (840 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

The two pack DLLs ship as zips of their own (`make-release-zip.py`, each with the pack's README.md and CHANGELOG.md):
`GK2-Saddle-Up-Dragon-1.0.0.zip` (`BepInEx\plugins\GK2SaddleUpDragon\`), `GK2-Saddle-Up-Critters-1.0.0.zip`
(`BepInEx\plugins\GK2SaddleUpCritters\`), and `GK2-Saddle-Up-All-Mounts-<Saddle Up's version>.zip` with both.

Players download GK2 Saddle Up (the donkey with its skins Hellfire and Royal Parade, and Gerry) and, if they want
more mounts, the packs: GK2 Saddle Up: Dragon (the dragon), GK2 Saddle Up: Critters (the rat, snail and worm),
or the All Mounts zip with both.

What is new since 0.10.0 is CHANGELOG.md's Unreleased section (as of 2026-10-27). The mounts and skins are on for
players since this version (0.10.0 shipped them off); which mounts a player has is which packs they installed.

Sizes 2026-10-27 15:44:
- `GK2-Saddle-Up-0.12.0.zip` 926 KB: the API DLL about 2.7 MB, Saddle Up 100 KB (Gerry's cfg and the donkey's two skins).
- `GK2-Saddle-Up-Dragon-1.0.0.zip` 440 KB (`GK2SaddleUpDragon.dll` 598 KB), `GK2-Saddle-Up-Critters-1.0.0.zip` 198 KB
  (`GK2SaddleUpCritters.dll` 344 KB), `GK2-Saddle-Up-All-Mounts-0.12.0.zip` 638 KB.

Release scans 22 of 22 green on this build; check-dist checks the four DLLs and both packs' folders. Self-tests on the dev build
(2026-10-27, after Freeplay and the speed words): the full run 1324 passed and 2 failed (one older test still gave its
mount a SpeedMultiplier, now refused; the test was fixed), then the 43 scenarios of that group 557 passed, 0 failed,
0 warnings. The runs with each pack left out are from 14:04 (before Freeplay). The zip rehearsal of 15:50 (A, the
first start; B, over the 0.10.0 player files) passed (release-checklist.md section 5); before an upload: the
CHANGELOG heading (section 6); `docs/open-items.md` has what is still unseen
in game.
