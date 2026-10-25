# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.38.0 (not released yet; the last release is 0.30.0) | 25910882 (1.010), built 2026-10-25 22:23 on commit b104f62 (flying for everyone, the dragon pulls no cart, the mounts' own sounds levelled with the game's; the mounts, skins and effects in the DLL but still switched off for players) |
| `GK2SaddleUp.dll` | 0.12.0 (needs GK2 Donkey API 0.38.0 or newer; not released yet; the last release is 0.10.0) | 25910882 (1.010), built 2026-10-25 22:23 |

The API DLL carries the list of every game member it uses (840 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

What is new since 0.10.0 is CHANGELOG.md's Unreleased section. The other mounts, the skins and the effects are in the
DLL and switched off for players (`Mounts.MountsEnabled` false; a dev key no player has).

Sizes 2026-10-25 (`release/GK2-Saddle-Up-0.12.0.zip`, 1.5 MB; 0.10.0 was 0.75 MB): the API DLL 2,540 KB (820 KB zipped;
its embedded files are 258 KB, the rest is code), Saddle Up 1,026 KB (670 KB zipped): its five mounts' files are
947 KB, the dragon 567 KB of them (its 16 sounds 324 KB), all sounds 438 KB, all pictures 491 KB. Release scans
22 of 22 green on this build.

Checked 2026-10-24 on the dev build: self-tests 1111 of 1112 with the mounts on (the one failure a timing race in the
safe-spot test, fixed and its group re-run green), the release scans all green (DepManifest, check-dist, CecilScan,
ApiSurface, DefaultsScan, SkinsScan, FrameworkScan, the skin checks, PackerScan). Before an upload: the "mounts off"
gate and the "upgrade" self-test once more, the zip rehearsal with the dev tools locked (release-checklist.md section
5), the version numbers and the CHANGELOG heading (section 6); `docs/open-items.md` has what is still unseen in game.
