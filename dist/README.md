# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.28.0 | 25752683 (1.009 hotfix), built 2026-10-13 |
| `SaddleUp.dll` | 0.9.0 (needs GK2 Donkey API 0.28.0 or newer) | 25752683 (1.009 hotfix), built 2026-10-13 |

Players install both into `BepInEx\plugins`. Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

Not yet seen in game in this build: the cargo packer's lone-item-on-a-pair placement and the ladder order of the
tuning tab (2026-10-12 evening, `docs/cargo-tuning-tools.md`); the rest is in `docs/open-items.md`.
