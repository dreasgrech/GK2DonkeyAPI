# Release checklist

What to do before `dist/` goes public, in order. Written 2026-10-13 from the first release audit; keep it current.

## 1. The code

- Every warning in the game's log is fixed (`BepInEx\LogOutput.log` of the last session: no `[Warning` or `[Error`
  lines from our plugins).
- No pre-release migration code is left in (nothing has shipped, so there is nothing to migrate from). Removed
  2026-10-13: the `.gk2mounts.dat` side-file reader and the cargo-layout version-1 reader.
- `Plugin.Version` is the mod's one version (the plugin's and `Mounts.ApiVersion`, which reads it); bump it with every
  API change. Saddle Up has its own version and asks for the API twice (`NeedsApi` and the `BepInDependency` minimum in
  `src/SaddleUp/Plugin.cs`, kept equal),
  and `tests/ApiSurface` is green (its snapshot is the public API).
- The audited game: the build embeds the dependency manifest against the installed game (`docs/dependency-check.md`;
  no pin to move). After any update `tests/GameDiff` and `DepManifest verify` give the audit its reading list (the
  audit notes are `docs/game-update-*.md`); `tests/CecilScan-DonkeyAPI` must report the manifest identical and every
  class sorted in `src/GK2DonkeyAPI/game-code.txt`.
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

`bash tools/release-scans.sh` (after the release build of section 3: the manifest check reads its DLL) runs them
all and lists each as ok or FAIL, exit 1 on any failure, the output in `release/logs/release-scans.log`: the dependency
manifest verified against the installed game (`tools/DepManifest`), `check-dist.py`, and in `tests/`
`CecilScan-DonkeyAPI`, `ApiSurface`, `DefaultsScan`, `SkinsScan`, `FrameworkScan`, `PackerScan` (plain, `--shapes`,
`--real --thorough`, `--review`, `--review-check`; the last lists the review's oks to look at again in game). All must
end green; `FrameworkScan`
also fails on any `Plugin` method named after a Unity message (a quit-time save once sat in `OnApplicationQuit`; it
is in the `Application.quitting` handler now).

## 3. The release build

The game may be running: a build is fine, only the deploy copy into the game needs it closed, and `dist/` is filled
from `bin/`, not from the game folder.

```
bash tools/build-dist.sh           # GK2DonkeyAPI.dll and GK2SaddleUp.dll into dist/, then check-dist.py
bash tools/release-scans.sh        # section 2
python tools/make-release-zip.py   # release/GK2-Saddle-Up-<version>.zip
```

`build-dist.sh` builds each with `dotnet build -c Release --no-incremental -p:ForDist=true -p:Deploy=false`, stops on
a failed build or any compiler warning, copies the DLLs into `dist/` and runs `check-dist.py` (build output in
`release/logs/build-dist.log`).

`-p:ForDist=true` leaves out the debug directory (its PDB path would hold this machine's user folder) and the
`GK2DonkeyAPISourceDir` marker (so the DLL reads its embedded `defaults.cfg`, `cargo-layout.cfg` and
`cargo-shapes.cfg`, and the panel has no "Make these the mod defaults" button). `check-dist.py` fails on any baked
path, a `.pdb` reference, the source-folder marker, an API manifest whose members changed or vanished in the
installed game, or a Saddle Up DLL that references a game assembly (it carries no manifest).
`--no-incremental` matters: after a dev build, a `ForDist` build of the unchanged sources reuses the dev output (the
property change doesn't invalidate the compile), and the check then fails on the PDB path (seen 2026-10-13).

`make-release-zip.py` (it runs `check-dist.py` first) writes `release/GK2-Saddle-Up-<version>.zip` with
`BepInEx/plugins/GK2DonkeyAPI/GK2DonkeyAPI.dll` and `BepInEx/plugins/GK2SaddleUp/GK2SaddleUp.dll` (a folder per mod),
read back and compared with `dist/`; this zip is
the Nexus upload (extracts into the game folder). `release/` is not kept in git.

Afterwards build once more without `ForDist` (and with the game closed) so the DLL in the game is the dev build
again; `bin/` holds whichever was built last.

## 4. The texts

- `dist/README.md`: versions, API version, game build and date.
- `README.md` (the table), `src/GK2DonkeyAPI/README.md` (version line, the install step's log line),
  `src/SaddleUp/README.md` (version line, the key table): the API version and the game build.
- `docs/open-items.md`: what is built but not yet seen in game, so the release notes can say so.

## 5. A first run as a player (this machine)

Rehearse the first start on a clean config, with the game closed:

1. Move `BepInEx\config\dreas.gk2.donkeyapi.cfg` and `dreas.gk2.saddleup.cfg` aside (they come back afterwards).
   Move the dev tools aside too (a player has none): `plugins\GK2ShowcaseDirector\`, `plugins\GK2PerfRunner\`,
   `plugins\GK2AssetProbe\` and the dev marker `BepInEx\config\dreas.gk2.dev`.
2. Extract the release zip into the game folder: its two DLLs replace the dev builds in their folders
   (`BepInEx\plugins\GK2DonkeyAPI\`, `BepInEx\plugins\GK2SaddleUp\`).
3. Start the game, load a save, call the donkey (H), drive the cart (G), load two items, press H (the cart is left
   standing), ride a few steps, press G: the SAME cart comes back with the two items (one cart in the world, its
   paint still on; the hitch event order bug of 2026-10-14 showed here as a second cart), quit.
4. `BepInEx\LogOutput.log`: `GK2 Donkey API x.y.z loaded`, `GK2 Saddle Up x.y.z loaded`,
   `Cargo layout: ... embedded`, no `Dev tools unlocked` line, and no `[Warning` or `[Error` from either plugin. The
   new API cfg has no `[Debug]` section. A second run with the marker file in `BepInEx\config` shows the panel.
5. Put the config files and the dev DLLs back (or rebuild without `ForDist`).

Run 2026-10-13 with the 0.28.0 dist, without the framework and with a stock `BepInEx.cfg`: all seven plugins loaded,
layout `from embedded`, the save's donkey and foals restored, ride, cart, two logs loaded, a dismount at a build desk;
no warning, error or exception in 101 log lines; the fresh cfgs at defaults; no new file in the BepInEx folder.
Second run the same day with the marker-file lock built: cart, ride, two logs loaded; 132 lines, no warning or error,
no `Dev tools unlocked` line, the fresh API cfg has no `[Debug]` section, no scratch cfg file written. Both runs
were on the 1.009-hotfix dist; the 1.010 dist of the same evening (pins only: `docs/game-update-25910882.md`) has
not been rehearsed, nor the 0.29.0 dist of 2026-10-14 (mounted combat; the API rebuilt 18:08 with the hitch event
order fix).
Run 2026-10-16 14:21 with the 0.29.0 dist of that day (one-pair cargo offsets, 9 embedded) without the dev marker and
the director: H, G, two items, H leaves the cart, G brings the same cart back with both items and its paint, one cart;
all seven plugins loaded, layout `from embedded`, no `Dev tools unlocked`, no warning, error or exception in 387 lines,
the fresh cfgs have no `[Debug]` section.

Run 2026-10-16 18:05 with the release zip itself (`release/GK2-Saddle-Up-0.9.0.zip` extracted into the game folder:
`plugins\GK2DonkeyAPI\`, `plugins\GK2SaddleUp\`), as a true first start: no cfgs, no dev marker, no director, and the
save's side file moved aside with Steam Cloud switched off (with it on, Steam downloads the side file again before the
game starts). No donkey after loading; H spawned it, G the cart, six logs loaded, set down behind the cart, H left the
cart, G hitched the same cart, items taken off on foot; the Mods menu page; 460 lines, no warning, error or exception;
layout from embedded, fresh cfgs at defaults (6 slots, foals indoors on) without `[Debug]`. Nothing saved; every save
file verified identical afterwards.

Run 2026-10-16 23:20 with the zip rebuilt at 23:03 (two performance fixes after the 18:05 one: the Mods-window lookup
and the zone cache, docs/performance.md; self-tests 332/332, scans 12/12 after `ModsWindow` was sorted into
`game-code.txt`), the same true first start (cfgs, marker, director, perf runner, Performance Hog and the side file
aside, Steam Cloud off): no donkey after loading; H, G (cart 2), two items, H left it as 3, G hitched 3 with both items,
H left it again, on foot E loaded a log onto the standing cart and took one off (the zone path of the fix); the Mods
page's picture built; 369 lines, no warning, error or exception; layout from embedded; fresh cfgs without `[Debug]`.
Nothing saved; every save file verified identical; everything restored and the dev builds redeployed.

## 6. Versions, changelog and hand-off

Released versions are never reused (0.9.0 went public 2026-10-16): a release with any change gets new numbers.

- **The numbers**: major.minor.patch. Fixes only: the patch (0.9.0 -> 0.9.1); new features or settings: the minor
  (0.9.x -> 0.10.0). The API's own number moves the same way (`Plugin.Version`, and `<Version>` in its csproj); a
  change to its public surface (`tests/ApiSurface`) is at least a minor. Saddle Up's `Version` in `Plugin.cs` and its
  csproj, kept equal. Then the version lines: `README.md` (the table, the log line), `src/GK2DonkeyAPI/README.md`,
  `src/SaddleUp/README.md`, `docs/api.md` (the version line, the `BepInDependency` example, a history entry),
  `docs/reference.md` (the log line), `dist/README.md`. `git grep` the old numbers: history entries stay.
- **The changelog** (`CHANGELOG.md`): every change goes under **Unreleased** when it is made, not at release. At
  release that heading becomes `<Saddle Up version> (GK2 Donkey API <version>), <date>` and a new empty Unreleased
  goes on top. Its "For players" part is the Nexus changelog text for that version.
- **Hand-off**: the user commits, tags (`saddleup-<version>`; 0.9.0 is commit `6be69a2`) and uploads
  `release/GK2-Saddle-Up-<version>.zip` with the changelog entry; the release notes name the game build (`25910882`,
  1.010 as of 2026-10-13) and what `docs/open-items.md` still lists as unseen in game.
