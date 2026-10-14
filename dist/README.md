# Release DLLs

Release builds (`-p:ForDist=true`: no debug directory, the embedded defaults and cargo layout only, no "Make these
the mod defaults" button), checked with `python tools/check-dist.py`. The steps are in
[`docs/release-checklist.md`](../docs/release-checklist.md).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.29.0 | 25910882 (1.010), built 2026-10-14 15:25 |
| `SaddleUp.dll` | 0.9.0 (needs GK2 Donkey API 0.28.0 or newer; unchanged, rebuilt) | 25910882 (1.010), built 2026-10-14 15:25 |

The API DLL carries the list of every game member it uses (692 entries against this build); at start it compares
them with the installed game and decides per feature what a changed one means (`docs/dependency-check.md`).

Players install both into `BepInEx\plugins`. Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.

New since 0.28.0: fights from the saddle (`docs/mounted-combat.md`; seen in game 2026-10-14), a hitched foreign cart
keeps its own look, a chest left standing no longer blocks its cart on load (both seen in game 2026-10-14).

Not yet seen in game in this build: the cart's capacity beyond two items (`CartSlots` 6: four crates, six logs, a door
and a save with four aboard, the relation sheet; `docs/cart-capacity.md`) and the cargo packer's lone-item-on-a-pair
placement (`docs/cargo-tuning-tools.md`); the rest is in `docs/open-items.md`.
