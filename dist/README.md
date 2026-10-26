# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.40.0 (not released yet; the last release is 0.30.0) | 25910882 (1.010), built 2026-10-26 18:40 on commit 1038ae2 (content from many mods; the dragon and the critters as packs of their own; the mounts, skins and effects still switched off for players) plus the self-tests' fixes after it |
| `GK2SaddleUp.dll` | 0.12.0 (needs GK2 Donkey API 0.39.0 or newer; not released yet; the last release is 0.10.0) | 25910882 (1.010), built 2026-10-26 18:40 |
| `SaddleUpDragon.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; never released) | the external pack: the dragon embedded, built 2026-10-26 |
| `SaddleUpCritters.dll` | 1.0.0 (needs GK2 Donkey API 0.40.0; never released) | the external pack: the rat, snail and worm embedded, built 2026-10-26 |

The API DLL carries the list of every game member it uses (840 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

The two pack DLLs ship as zips of their own (`make-release-zip.py`, each with the pack's README.md and CHANGELOG.md):
`GK2-Saddle-Up-Dragon-1.0.0.zip` (`BepInEx\plugins\SaddleUpDragon\`), `GK2-Saddle-Up-Critters-1.0.0.zip`
(`BepInEx\plugins\SaddleUpCritters\`), and `GK2-Saddle-Up-All-Mounts-<Saddle Up's version>.zip` with both.

What is new since 0.10.0 is CHANGELOG.md's Unreleased section. The other mounts, the skins and the effects are
switched off for players (`Mounts.MountsEnabled` false; a dev key no player has): with them off, the packs are ignored.

Sizes 2026-10-26 18:40:
- `GK2-Saddle-Up-0.12.0.zip` 0.9 MB (1.5 MB with the five mounts inside Saddle Up): the API DLL 2,634 KB (851 KB
  zipped), Saddle Up 100 KB (43 KB zipped: Gerry's cfg and the donkey's two skins).
- `GK2-Saddle-Up-Dragon-1.0.0.zip` 440 KB (`SaddleUpDragon.dll` 598 KB), `GK2-Saddle-Up-Critters-1.0.0.zip` 198 KB
  (`SaddleUpCritters.dll` 344 KB), `GK2-Saddle-Up-All-Mounts-0.12.0.zip` 637 KB.

Release scans 22 of 22 green on this build; check-dist checks the four DLLs and both packs' folders. Self-tests on the dev build
(2026-10-26): 1222 of 1223, the one a timing wait in the Mounts panel test (the dragon then read from its pack folder),
fixed, its re-run pending. Before an upload: the "mounts off" gate and the "upgrade" self-test once more, the zip
rehearsal with the All Mounts zip and the dev tools locked (release-checklist.md section 5),
the version numbers and the CHANGELOG heading (section 6); `docs/open-items.md` has what is still unseen in game.
