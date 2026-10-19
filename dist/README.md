# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.30.0 | 25910882 (1.010), built 2026-10-19 15:15 (commit ab208b9: the GK2 Mod Framework 0.1.19+ fix, the Mount and Effect settings sections, the colours window layout, the side-file set-aside; the mounts, skins and effects in the DLL but switched off for players) |
| `GK2SaddleUp.dll` | 0.10.0 (needs GK2 Donkey API 0.30.0 or newer) | 25910882 (1.010), built 2026-10-19 15:15 |

The API DLL carries the list of every game member it uses (693 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

New since 0.29.1 (CHANGELOG 0.10.0, "For players"): Saddle Up's page in the Mods menu works with GK2 Mod Framework
0.1.19 and newer (it was "Faulted": AmbiguousMatchException; seen fixed in game with 0.1.23 on 2026-10-19); a Mount
section and an Effect section in the settings, holding the donkey alone with "Coming in the next update" rows; the
colours window that stays put; a side file of an unknown format set aside instead of overwritten. The other mounts,
the skins and the effects are in the DLL and switched off (`Mounts.MountsEnabled` false; a dev key no player has).

Checked 2026-10-19 on the dev build of this commit: the 0.9.1 player files carried over whole (0 of 485 values lost),
self-tests 490/490 with the mounts on and the gate green with them off, the 13 release scans green. The zip rehearsal
with the dev tools locked (release-checklist.md section 5) is the last step before upload; `docs/open-items.md` has the rest.
