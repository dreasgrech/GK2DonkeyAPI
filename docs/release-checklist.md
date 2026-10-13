# Release checklist

What to do before `dist/` goes public, in order. Written 2026-10-13 from the first release audit; keep it current.

## 1. The code

- Every warning in the game's log is fixed (`BepInEx\LogOutput.log` of the last session: no `[Warning` or `[Error`
  lines from our plugins).
- No pre-release migration code is left in (nothing has shipped, so there is nothing to migrate from). Removed
  2026-10-13: the `.gk2mounts.dat` side-file reader and the cargo-layout version-1 reader.
- `Mounts.ApiVersion` (`Api/Mounts.cs`) matches what Saddle Up asks for (`NeedsApi` in `src/SaddleUp/Plugin.cs`)
  and `tests/ApiSurface` is green (its snapshot is the public API).
- The audited game: `AuditedGameMvid` / `AuditedLazyBearMvid` in `src/GK2DonkeyAPI/Plugin.cs` are the installed
  game's (`tests/GameDiff` after any update; the audit notes are `docs/game-update-*.md`).
- The dev tools cannot reach a player by accident: the `[Debug]` keys exist only with the dev tools unlocked
  (`DevTools.cs`: the sources on this machine, or the marker file `BepInEx\config\dreas.gk2.dev`); a player's cfg
  has no `[Debug]` section at all. Unlocked, `Debug.TuningWindow` is off by default and is the only way in;
  nothing else in `Dev/` runs without it (`TuningWindow.Showing()` gates the panel, the zone outlines, the probes;
  F5 and the F11 hide key need the switch too). Checked 2026-10-13: the 31 references from shipped code into
  `Dev/` (11 files) are all passive with the panel off (`CargoSheet.Running` false, `Abort` no-ops, the probe
  overrides null, `ScriptDump.ForceBodyRollUntilFrame` -1, `SelfTest.PollStart` waits for a panel request); the
  `GK2-*-facts.txt` files are written by panel buttons only.
- The first start writes the cfg once (`Plugin.Load` switches `SaveOnConfigSet` off while binding), not once per
  key.

## 2. The offline checks (no game launch)

In `tests/`, each with `dotnet run -c Release`: `CecilScan-DonkeyAPI`, `ApiSurface`, `DefaultsScan`,
`FrameworkScan`, `PackerScan` (plain, `-- --shapes`, `-- --real --thorough`). All must end green; `FrameworkScan`
also fails on any `Plugin` method named after a Unity message (a quit-time save once sat in `OnApplicationQuit`; it
is in the `Application.quitting` handler now).

## 3. The release build

The game may be running: a build is fine, only the deploy copy into the game needs it closed, and `dist/` is filled
from `bin/`, not from the game folder.

```
cd src\GK2DonkeyAPI && dotnet build -c Release --no-incremental -p:ForDist=true -p:Deploy=false
cd src\SaddleUp     && dotnet build -c Release --no-incremental -p:ForDist=true -p:Deploy=false
copy src\GK2DonkeyAPI\bin\Release\netstandard2.1\GK2DonkeyAPI.dll dist\
copy src\SaddleUp\bin\Release\netstandard2.1\SaddleUp.dll dist\
python tools\check-dist.py
```

`-p:ForDist=true` leaves out the debug directory (its PDB path would hold this machine's user folder) and the
`GK2DonkeyAPISourceDir` marker (so the DLL reads its embedded `defaults.cfg`, `cargo-layout.cfg` and
`cargo-shapes.cfg`, and the panel has no "Make these the mod defaults" button). `check-dist.py` fails on any baked
path, a `.pdb` reference, the source-folder marker, or a pinned MVID that is not the one in `Plugin.cs`.
`--no-incremental` matters: after a dev build, a `ForDist` build of the unchanged sources reuses the dev output (the
property change doesn't invalidate the compile), and the check then fails on the PDB path (seen 2026-10-13).

Afterwards build once more without `ForDist` (and with the game closed) so the DLL in the game is the dev build
again; `bin/` holds whichever was built last.

## 4. The texts

- `dist/README.md`: versions, API version, game build and date.
- `README.md` (the table), `src/GK2DonkeyAPI/README.md` (version line, the audited MVIDs, the install step's log
  line), `src/SaddleUp/README.md` (version line, the key table): the API version and the game build.
- `docs/open-items.md`: what is built but not yet seen in game, so the release notes can say so.

## 5. A first run as a player (this machine)

Rehearse the first start on a clean config, with the game closed:

1. Move `BepInEx\config\dreas.gk2.donkeyapi.cfg` and `dreas.gk2.saddleup.cfg` aside (they come back afterwards).
2. Put the two `dist/` DLLs into `BepInEx\plugins` over the dev builds.
3. Start the game, load a save, call the donkey (H), drive the cart (G), load two items, quit.
4. `BepInEx\LogOutput.log`: `GK2 Donkey API 0.1.0 loaded (API x.y.z)`, `GK2 Saddle Up! 0.1.0 loaded`,
   `Cargo layout: ... embedded`, no `Dev tools unlocked` line, and no `[Warning` or `[Error` from either plugin. The
   new API cfg has no `[Debug]` section. A second run with the marker file in `BepInEx\config` shows the panel.
5. Put the config files and the dev DLLs back (or rebuild without `ForDist`).

Run 2026-10-13 with the 0.28.0 dist, without the framework and with a stock `BepInEx.cfg`: all seven plugins loaded,
layout `from embedded`, the save's donkey and foals restored, ride, cart, two logs loaded, a dismount at a build desk;
no warning, error or exception in 101 log lines; the fresh cfgs at defaults; no new file in the BepInEx folder.

## 6. Hand-off

The user commits and tags; the release notes name the game build (`25752683`, 1.009 hotfix as of 2026-10-13) and
what `docs/open-items.md` still lists as unseen in game.
