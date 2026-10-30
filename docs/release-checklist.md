# Release checklist

What to do before `dist/` goes public, in order. Written 2026-10-13 from the first release audit; keep it current.

## 1. The code

- Every warning in the game's log is fixed (`BepInEx\LogOutput.log` of the last session: no `[Warning` or `[Error`
  lines from our plugins).
- Nothing a player has on disk from a released version is lost: `docs/compatibility.md` (the four stores and the
  rules per store). The self-test "upgrade: a released version's player files are all read" (the API's files) and the
  integration tests' "upgrade: Saddle Up's cfg as every released version wrote it ..." (since 2026-10-27,
  `tools/GK2DonkeyAPI.IntegrationTests`, deployed for the run with `-p:Deploy=true`) are green against every
  folder in `tests/fixtures/released/`, and any `MovedKey` a released layout needs is still in place. (Pre-release
  migration code was removed 2026-10-13; compatibility code is for released data only.)
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
- **The mounts, skins and effects are on for players** since 0.12.0 (0.10.0 shipped them off by the dev key
  `Debug.MountsPreview`, removed 2026-10-27): which mounts a player has is which packs they installed. The release
  rehearsal runs with and without the pack zips.

## 2. The offline checks (no game launch)

`bash tools/release-scans.sh` (after the release build of section 3: the manifest check reads its DLL) runs them
all and lists each as ok or FAIL, exit 1 on any failure, the output in `release/logs/release-scans.log`: the dependency
manifest verified against the installed game (`tools/DepManifest`), `check-dist.py`, the layering scan
(`tools/layering-scan.py --dist`, since 2026-10-27: no name of Saddle Up or its packs in `src/GK2DonkeyAPI`, Dev
included, or in the API DLL's strings, and no pack's name in `src/SaddleUp`), and in `tests/`
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
`modinfo.json` (id `dreas.gk2.saddleup`, name, version: the game's Mods window reads them; since 2026-10-30,
`docs/mods-window-and-workshop.md`), `BepInEx/plugins/GK2DonkeyAPI/GK2DonkeyAPI.dll` and
`BepInEx/plugins/GK2SaddleUp/GK2SaddleUp.dll` (a folder per mod; Saddle Up's built-in Gerry is a pack inside its DLL),
read back and compared with `dist/`; this zip is the Nexus upload. It extracts into the game folder as before, or
into `Mods\Code\<Name>\` in the save data folder, where the game's Mods window lists it with Enable/Disable and
copies the plugin folders in itself. Besides it, one zip per external pack (`packs/<Pack>/`, the pack's name after
`GK2SaddleUp` with its words dashed: `GK2-Saddle-Up-Dragons-Mount-Pack-<v>.zip`, `GK2-Saddle-Up-Critters-Mount-Pack-<v>.zip`,
`GK2-Saddle-Up-Donkey-Skin-Pack-<v>.zip`, its `modinfo.json` with the pack's GUID, the version from the pack's
CHANGELOG.md, raised there when the pack changed): each its own Nexus file. There is no bundle of the packs (the user,
2026-10-27: "ill ship individuals"). `--only saddleup` (or a pack's folder name) writes one item's zip: a pack whose
number is public is not rebuilt. `release/` is not kept in git.

The same run stages each item for the Steam Workshop in `workshop/<Item>/` (not in git; `--no-workshop` skips it):
the zip's content as a folder plus `Thumbnail.jpg` (from `media/workshop/<Item>/`, made by
`tools/workshop-thumbnail.py <picture> <Item>`), `_description.bbcode` (from `media/..._Workshop.txt`, Steam's BBCode;
the tool warns while a `{{ }}` placeholder is left or the text is over Steam's 8,000 characters) and `_changenote.txt`
(the item's "For players" entry of its changelog). The game's Workshop creator (Shift+F11 in the main menu with
`"workshopCreatorMode": true` in `Mods\workshop.json` in the save data folder) uploads that folder; the uploader
itself skips the three files. One item per zip, the same bytes on both channels; the Saddle Up item carries the API
(bundle, decided 2026-10-30: two items sharing a file path delete it for each other, so the API never ships twice).

Afterwards build once more without `ForDist` (and with the game closed) so the DLL in the game is the dev build
again; `bin/` holds whichever was built last.

## 4. The texts

- `dist/README.md`: versions, API version, game build and date.
- `README.md` (the table), `src/GK2DonkeyAPI/README.md` (version line, the install step's log line),
  `src/SaddleUp/README.md` (version line, the key table): the API version and the game build.
- `docs/open-items.md`: what is built but not yet seen in game, so the release notes can say so.

## 5. A first run as a player (this machine)

Since 2026-10-30 there is a second rehearsal, of the Workshop content as a local pack in `Mods\Code` (the game's
own install path, the same files the Workshop delivers): `bash tools/rehearsal.sh stage-pack` / `restore-pack`,
the run and its expected lines in `docs/mods-window-and-workshop.md` section 11.

Rehearse the first start on a clean config, with the game closed. `tools/rehearsal.sh` does the staging (`stage-a`,
`capture-a`, `stage-b <saddleup.cfg>`, `restore`; it refuses while the game runs, and refuses a cfg that would switch
the API off or has numpad keys); `tools/fixture-live.py` makes a fixture's Saddle Up cfg fit to stage (keys L/H/G, a
real skin worn with `--skin`); `tools/log-scenarios.py` lists the scenarios an F11 run left no line for. By hand:

1. Move `BepInEx\config\dreas.gk2.donkeyapi.cfg` and `dreas.gk2.saddleup.cfg` aside (they come back afterwards).
   Move the dev tools aside too (a player has none): `plugins\GK2ShowcaseDirector\`, `plugins\GK2PerfRunner\`,
   `plugins\GK2AssetProbe\`, `plugins\GK2DonkeyAPI.IntegrationTests\` (the integration tests, when deployed) and the
   dev marker `BepInEx\config\dreas.gk2.dev`.
2. Extract the release zip into the game folder: its two DLLs replace the dev builds in their folders
   (`BepInEx\plugins\GK2DonkeyAPI\`, `BepInEx\plugins\GK2SaddleUp\`); then each pack's zip: `GK2SaddleUpDragonsMountPack.dll`,
   `GK2SaddleUpCrittersMountPack.dll` and `GK2SaddleUpDonkeySkinPack.dll` replace theirs in `plugins\GK2SaddleUpDragonsMountPack\`,
   `plugins\GK2SaddleUpCrittersMountPack\` and `plugins\GK2SaddleUpDonkeySkinPack\` (the DLL alone since 2026-10-29; the
   1.0.0 zips of 2026-10-28 also carried the pack's README and CHANGELOG, which nothing reads).
3. Start the game, load a save, call the donkey (H), drive the cart (G), load two items, press H (the cart is left
   standing), ride a few steps, press G: the SAME cart comes back with the two items (one cart in the world, its
   paint still on; the hitch event order bug of 2026-10-14 showed here as a second cart), quit.
3b. **Capture the released files** for `tests/fixtures/released/<version>/` (docs/compatibility.md section 4): copy
   the two cfgs BepInEx just wrote (then set every value in the copies to something other than its default, so a
   lost value shows: `python -I tools/fixture-flip.py`), the save's `<save>.gk2donkeyapi.dat` with the parked loaded cart, and `src/GK2DonkeyAPI/defaults.cfg`.
3c. **The upgrade rehearsal**: put the previous release's two cfgs back (a player's: the ones moved aside in step 1
   are fine when the previous release wrote them, else the previous fixture's), with a side file holding a parked
   loaded cart, start the new zip, load: the colours and keys as before, the cart where it stood with its load, the
   log's "Settings: ... carried over" lines and no warning; quit. **Never stage a fixture's API cfg live**: a fixture
   has every value off its default, the API's `[General] Enabled = false` among them, so the API is off in game and
   nothing works (seen 2026-10-29); stage Saddle Up's fixture cfg (its keys are on the numpad: set L/H/G first) with
   the API cfg rehearsal A wrote.
4. `BepInEx\LogOutput.log`: `GK2 Donkey API x.y.z loaded`, `GK2 Saddle Up x.y.z loaded (GK2 Donkey API x.y.z): ride H,
   cart G`, each installed pack's own line (`GK2 Saddle Up: Dragon 1.0.0: 1 skin(s) registered (the mount
   saddleup-dragon.dragon)`, `GK2 Saddle Up: Critters 1.0.0: 3 skin(s) registered (the mounts ...)`, `GK2 Saddle Up:
   Donkey Skins 1.0.0: 2 skin(s) registered for the donkey, and the effect saddleup-donkey-skins.hellfire`; the former
   `Mounts, skins and effects: on.` went with the 0.10.0 gate on 2026-10-27: the mounts are always on, which ones a
   player has is which packs they installed; the Mounts panel lists the donkey, Gerry and every installed pack's
   mounts, and the donkey's Skin list Hellfire and Royal Parade with the Donkey Skins pack),
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
Run 2026-10-19 14:15, the 0.10.0 dev build (mounts switched off) over the **0.9.1 player files** (the fixture's two
cfgs staged in BepInEx\config with KeepWithSaves on, the user's own side file): the Mods menu showed every 0.9.1 value
(R/T keys, Bella, plum custom coat, red harness, blue paint, black wheels, brass hubs, green light, 3 babies, 4 slots,
every toggle off, volume 120) and the Mounts panel the same; 5 of 5 kept donkeys and carts restored; R brought the
donkey and the cart unhitched (CartStaysParked off in 0.9.1's file: as documented); 674 log lines, 0 warnings, the
seven "carried over" lines and "31 old line(s) left out"; the rewritten cfgs hold all 35 Saddle Up and 450 API
values of the 0.9.1 files (a key-by-key comparison, 0 lost; copies in `backup-dev-2026-10-19\after-upgrade-0.9.1`);
every save file byte-identical. The fixture gained the six `Custom*` palette keys 0.9.1 wrote (they were missing).
Final readiness check 2026-10-19 15:30: the fixture's key set compared with the 0.9.1 source itself (every `p.X(section,
key)` call of Saddle Up's Plugin.cs at 7ac9a6c: 33 keys plus the two speed rows, all in the fixture; the API's 118 literal
`Bind` keys: all but the 16 [Debug] ones in the fixture, as a player's file has them; the computed families, 14 colour
regions x 10, Ride and Cart 123 each, Shuffle 30, all present); the side file's reader unchanged since 0.9.1 and its writer
only leaves a skin's own tuning out; no debug marker in shipped code (TEMPORARY, FIXME, HACK, Console, Debug.Log: none;
0 TODOs); CecilScan (693 manifest entries identical, 67 classes sorted), DefaultsScan, SkinsScan, FrameworkScan and
ApiSurface green on this build. Before the rehearsal, out of `plugins\`: GK2DonkeySkins (the example mod) and gk2skins\
(worm-pack, royal-parade) if they were put back for a test (gone since 2026-10-19 23:30; Royal Parade is built in),
GK2AssetProbe, GK2PerfRunner, GK2ShowcaseDirector, and the dev marker.
Run 2026-10-19 15:45 with the release zip itself (`release/GK2-Saddle-Up-0.10.0.zip`, commit ab208b9) extracted into
the game folder, the dev marker, the dev DLLs and the dev-only plugins aside, over the 0.9.1 player cfgs (the fixture,
KeepWithSaves on) and the user's side file: the Mods menu with every 0.9.1 value, Mount = Donkey and Skin = Default
alone, the two "Coming in the next update" rows, Saddle Up and the API Compatible on framework 0.1.23; 5 of 5 kept
donkeys and carts restored; R, E to hitch, two items, R leaves the cart, E brings the same cart with both; the colours
bar; F11 nothing. 538 log lines, 0 warnings, no `Dev tools unlocked`, "Mounts, skins and effects: off in this version",
layout from embedded, the seven carry-over lines; the rewritten cfgs hold all 35 + 450 values of the 0.9.1 files, no
[Debug] section, no scratch cfg; every save file byte-identical. Nothing saved; everything restored, the dev builds
redeployed.
Step 3b was skipped at the 0.10.0 release; `tests/fixtures/released/0.10.0/` was made on 2026-10-19 16:30 from the
cfgs that rehearsal run wrote (kept in `BepInEx\rehearsal-aside-2026-10-19\after-rehearsal`), the keys new in 0.10.0
then set off their defaults, plus a real side file of the 0.30.0 build. Next release: capture at step 3b itself.
Run 2026-10-19 17:20, the 0.11 page regroup (Saddle Up 0.11.0 / API 0.31.0, dev build, mounts on) over the user's own
0.10.0-dev cfg: the seven sections in the new order with every value, 134 "is [...] now" lines (every colour key of six
mount sets, KeepWithSaves, the cart's six rows), Effect and Behaviour renumbered, 143 old lines left out, the rewritten
cfg equal to its backup key by key (164 keys, 0 lost, 0 changed; `backup-dev-2026-10-19-regroup`); self-tests 498/498
with the upgrade scenario green over the 0.9.1 and the 0.10.0 fixtures; no warning but the tests' own. Found and fixed
in that run: a heading with a picture row's label got a picture of its own (the headings are "Mount colours" and
"Cart colours" now; the page refuses such a row).

Run 2026-10-27 with the release zips of 0.12.0 / API 0.40.0 (`GK2-Saddle-Up-0.12.0.zip` and the combined packs zip of that day, since dropped), the dev
marker, the dev DLLs and the dev-only plugins aside (staged by a script: stage-a, capture-a, stage-b, restore).
**A, a first start** (no cfgs): 392 log lines, no warning, error or exception, no `Dev tools unlocked`, the four pack
mounts and Gerry declared with their cargo (Gerry 4, rat 2, snail 6, worm 4, the dragon no cart), layout from embedded;
H, G, two items, H left the cart, G brought the same cart with both; the Mounts panel listed six mounts; the fresh API
cfg has no `[Debug]`. Its cfgs are the `0.12.0` fixture (every value then set off its default). Found in it: the Mounts
panel key had no default (now L, the first Keys row; rebuilt and re-extracted before B). **B, the upgrade** over the
0.10.0 fixture's cfgs: 430 lines, 0 warnings; every moved row logged "carried over", the 7 [Effects] lines retired;
key by key 42 of 42 Saddle Up values and 453 of 453 API values kept (the 15 others the retired Effect and speed rows);
in the panel Bella, 3 babies, the 0.10.0 colours, the lantern off (the fixture's `Lantern = false`). Saves byte-identical
before and after; everything restored and the dev builds redeployed (`--no-incremental`).

Run 2026-10-27 14:20, again with the zips rebuilt at 14:08 (after the day's changes: Cart capacity retired, the Safe spot
section, the bell and the parked cart rows retired, the babies' look row retired, three volume sliders, the Mount Journal's
title bar, the pack DLLs named GK2SaddleUpDragonsMountPack and GK2SaddleUpCrittersMountPack). **A**: 377 lines, no warning, error or
exception, no `Dev tools unlocked`; the five pack and built-in mounts declared; H, G, two items, H, G: the same cart with
both; L the Mount Journal with six mounts; F11 nothing; no `[Debug]`, no `CartSlots` in either cfg. Its cfgs are the
`0.12.0` fixture again (210 + 473 values off their defaults). **B** over the 0.10.0 cfgs: 431 lines, 0 warnings, 29
carried over; key by key 38 of 38 Saddle Up values and 452 of 452 API values kept, the 20 others the rows retired on
purpose (Cart capacity in both, the babies' look, the parked cart, the bell, the speed and Effect rows). R indoors did
nothing (that file predates Allow mounting indoors and says no messages) and T opened the game's Tech Tree too (the
fixture binds the cart key to the game's own T on purpose). Saves byte-identical; everything restored.

Run 2026-10-27 15:50, with the zips rebuilt at 15:44 (after Freeplay and the speed words: a mount's speed is one of the
API's words). **A**: 392 lines, no warning, error or exception, no `Dev tools unlocked`; every mount declared with its
word (Gerry Fast, the rat Very fast, the snail Very slow, the worm and the dragon Slow); H, G, two items, H, G: the same
cart hitched again with both; L the Mount Journal with six mounts; Freeplay the Saddle Up page's last section, its
speed dropdown shown with the switch on; no `[Debug]` in the API cfg. Its cfgs are the `0.12.0` fixture again (476 +
213 values off their defaults; only the Freeplay lines are new). **B** over the 0.10.0 cfgs: 427 lines, 0 warnings, 29
carried over, the old keys kept ("ride R, cart T"); key by key 38 of 38 Saddle Up values and 452 of 452 API values
kept, the 20 others the rows retired on purpose. The ride on R is in the log, a drive on T is not (the fixture's T is
the game's Tech Tree too). Saves byte-identical; everything restored.

Run 2026-10-28 00:00-00:40 with the four zips of 2026-10-27 23:56 (the 23:55 dist: Saddle Up with Gerry alone, the
Donkey Skins pack's zip of its own, 23 of 23 scans green): A on fresh cfgs with all three packs: API 0.40.0 and
Saddle Up 0.12.0 loaded (ride H, cart G), each pack's own line, cargo layout embedded, no `[Debug]` section, no
`Dev tools` line, 0 warnings in 472 lines; H, G, items, H leaves the cart, G brings the same cart back (four
times, the last with 5 items); the Skin list Default / Hellfire / Royal Parade, Hellfire picked; a restart: the skin
"built ahead (the companion's choice)" at load and worn on the spawn, 0 warnings in 325 lines. The fixture
`tests/fixtures/released/0.12.0/` remade from A's cfgs (213 + 476 values, the same values as the 15:50 one: the
skins' move changed no key); its `Steam_1.gk2donkeyapi.dat` is still the 2026-10-22 file (no save in either
rehearsal, so step 3b's side file with a parked loaded cart written by 0.12.0 wasn't captured). B with the 0.10.0 fixture cfgs over the same DLLs: every moved key "carried over"
(KeepWithSaves, Volume, the Donkey and Cart colours into Colours), 1 retired General and 7 retired Effects lines
left in the file, ride R, cart T, the cart left and hitched back, 0 warnings in 474 lines; the saves byte-identical
after both. Earlier that night the full F11 without the Donkey Skins folder: 1293 passed, 0 failed, its five
scenarios skipped with the reason, the donkey's Skin list Default only (the kept choice shown as "Hellfire (not
installed)" since that run: the raw id showed). Not rehearsed: Saddle Up with no pack at all as a zip install (its
F11 equivalent, the pack-less setups, ran 2026-10-27 14:04).
Then the full F11 with all three packs on that code: 1331 passed, 0 failed, 0 skipped. At 00:29 the pack DLLs were
renamed (`GK2SaddleUpDragonsMountPack.dll`, `GK2SaddleUpCrittersMountPack.dll`, `GK2SaddleUpDonkeySkinPack.dll`; the
user), at 00:35 their source folders, namespaces, resource prefixes and zip names too (`packs/<DLL name>/`,
`GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip` and so on; the ids and GUIDs unchanged, the id-prefix rule keyed on the
GUID now), and the dist, the scans (23 of 23) and the four zips remade from otherwise unchanged sources; the zips were
not rehearsed again (the same layout, a folder named after each DLL). The full F11 on the renamed build (01:10, night,
the donkey and cart chosen): 1331 passed, 0 failed, 0 skipped, every pack's content from its renamed DLL; the one
fault on the way was the integration test's own ("built in or external" prepended GK2 to the folder name).
Run 2026-10-29 for 0.13.0 / API 0.41.0 (per-skin looks): dist 12:08, 23 of 23 scans, A on fresh cfgs with the three
packs (0.41.0 / 0.13.0 loaded, no `[Debug]`, no Dev tools line, 0 warnings in 383 lines; the per-skin keys written for
three skins; the fixture 0.13.0 made from it, 259 keys); B with the 0.12.0 fixture's Saddle Up cfg and Hellfire worn
(`tools/fixture-live.py`) over A's API cfg: the seed lines ("Hellfire's look took 9 value(s) of donkey's", "14 more"),
Hellfire in the fixture's colours, the plain donkey at defaults. B's screenshots showed the journal's picture off
centre for the skins (docs/per-skin-looks.md §7): fixed the same afternoon, full F11 1356 passed, 0 failed, 0 skipped;
the dist, scans and zip remade from the final sources after that (the zip not rehearsed again: the change is the
picture's framing and the stage's lantern, nothing in the cfgs or the install).

## 6. Versions, changelog and hand-off

Released versions are never reused (0.9.0 went public 2026-10-16): a release with any change gets new numbers.

- **The numbers**: major.minor.patch. Fixes only: the patch (0.9.0 -> 0.9.1); new features or settings: the minor
  (0.9.x -> 0.10.0). The API's own number moves the same way (`Plugin.Version`, and `<Version>` in its csproj); a
  change to its public surface (`tests/ApiSurface`) is at least a minor. Saddle Up's `Version` in `Plugin.cs` and its
  csproj, kept equal. Then the version lines: `README.md` (the table, the log line), `src/GK2DonkeyAPI/README.md`,
  `src/SaddleUp/README.md`, `docs/api.md` (the version line, the `BepInDependency` example, a history entry),
  `docs/reference.md` (the log line), `dist/README.md`. `git grep` the old numbers: history entries stay.
  Since 2026-10-30 `python tools/release-versions.py set saddleup <v> --api <v>` (and `set <Pack> <v>`) moves the
  mechanical lines and `python tools/release-versions.py check` (run by `make-release-zip.py` before anything else)
  fails on any line left behind, the missing CHANGELOG heading, docs/api.md history entry or Nexus note included.
- **The changelog** (`CHANGELOG.md`): every change goes under **Unreleased** when it is made, not at release. At
  release that heading becomes `<Saddle Up version> (GK2 Donkey API <version>), <date>` and a new empty Unreleased
  goes on top. Its "For players" part is the Nexus changelog text for that version.
- **Hand-off**: the user commits, tags (`saddleup-<version>`; 0.9.0 is commit `6be69a2`) and uploads
  `release/GK2-Saddle-Up-<version>.zip` with the changelog entry; the release notes name the game build (`26297960`,
  1.011 as of 2026-10-30) and what `docs/open-items.md` still lists as unseen in game. On the Steam Workshop (once
  the items exist): Shift+F11, the item's Upload button with `workshop/<Item>/` as its linked folder; the change
  note comes from `_changenote.txt`; a pack's Required item (GK2 Saddle Up) is set once on its Steam page.
