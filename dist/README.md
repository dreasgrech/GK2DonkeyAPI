# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.29.1 | 25910882 (1.010), built 2026-10-16 (cargo layout of commit cc8a7ea, one-pair cargo offsets, review fingerprint v3; the Mods-window and zone-cache fixes; the stacked-logs tweak of commit 7ac9a6c) |
| `GK2SaddleUp.dll` | 0.9.1 (needs GK2 Donkey API 0.28.0 or newer) | 25910882 (1.010), built 2026-10-16 |

The API DLL carries the list of every game member it uses (693 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both, each in its own folder: `BepInEx\plugins\GK2DonkeyAPI\` and `BepInEx\plugins\GK2SaddleUp\` (the
release zip, `tools/make-release-zip.py`, holds that layout). Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

New since 0.28.0: fights from the saddle (`docs/mounted-combat.md`; seen in game 2026-10-14), a hitched foreign cart
keeps its own look, a chest left standing no longer blocks its cart on load (both seen in game 2026-10-14).

Not yet seen in game in this build: one-pair cargo offsets (`[onpair ...]` in the cargo layout, `docs/cargo-tuning-tools.md`);
the lone-item-on-a-pair placement was tuned in game 2026-10-15, the cart's capacity of six seen in game 2026-10-14. The rest is in `docs/open-items.md`.
