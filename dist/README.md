# Release DLLs

Release builds (`-p:ForDist=true`: no "Make these the mod defaults" button, the embedded defaults only).

| File | Version | Game build |
|---|---|---|
| `GK2DonkeyAPI.dll` | 0.1.0 (API 0.27.0) | 25752683 (1.009 hotfix), built 2026-10-10 |
| `SaddleUp.dll` | 0.1.0 (needs API 0.27.0 or newer) | 25752683 (1.009 hotfix), built 2026-10-10 |

Players install both into `BepInEx\plugins`. Mod authors reference `GK2DonkeyAPI.dll` and ship only their own DLL.
