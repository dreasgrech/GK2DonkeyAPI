# Release DLLs

Release builds (`-p:ForDist=true`: no "Make these the mod defaults" button, the embedded defaults only).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.1.0 (API 0.13.0) | 25676698 (1.008) |
| `SaddleUp.dll` | 0.1.0 | 25676698 (1.008) |

Players install both into `BepInEx\plugins`. Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.
