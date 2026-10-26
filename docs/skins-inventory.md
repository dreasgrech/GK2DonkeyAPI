# Where every skin lives

**2026-10-25 (API 0.37.0):** no skin names an effect any more (Hellfire's skin.cfg lost its effect lines and its
description no longer mentions its ring of fire); `Effects.cs` keeps the registration and the particle engine, and
Saddle Up still registers the Hellfire effect, which nothing shows for now.

Every skin the API knows, what it is made of, where its source and its tooling are, and what is read from outside
the repo. As of 2026-10-19 evening (after the skin tooling moved into the repo); the paths and ids as of 2026-10-24
(the mount folders: built in, `src/SaddleUp/assets/mounts/<mount>/`; external, `packs/<Pack>/gk2mounts/<mount>/`; each
with `mount.cfg`, `sounds\` and `skins/<skin>/`). The review page
(`tools/skins/skin_preview_html.py` -> `BepInEx\GK2-asset-probe\skins-preview.html`) shows them all, grouped like this.

## The three mounts of skin

| Mount | What ships | How the pictures come to be |
|---|---|---|
| **Recipe** | code in the API | built in the game from the donkey's own frames when first worn; no picture stored anywhere |
| **Pack** | `skin.cfg` + pictures (a PNG per frame, or one sheet of cells) | drawn or generated beforehand; a built-in pack is embedded in the DLL, a loose one sits in `BepInEx\plugins\gk2mounts\<mount>\skins\<skin>\` (its mount's `mount.cfg` two folders up, or none for a skin of a mount registered elsewhere); a mount's own look is its `skins\default\`. **Every skin we ship is a built-in pack made the same way** (`docs/sprite-tools.md` "Making a skin: the one pipeline") |
| **Game creature** (`source = game`) | `skin.cfg` only | the game's own frames of another character, read back and scaled at run time |

The machinery for all three: `src/GK2DonkeyAPI/Looks.cs` (packs, sheets, sources, the overlay drawing, colours by
region), `LooksRecipes.cs` (recipes), `LooksGame.cs` (game creatures), `LookPreview.cs` / `PreviewStage.cs` (the
pictures in the menus), `MountsPanel.cs` (the Mounts panel: the list, the picture, the look rows), `Effects.cs` (particle effects), the public API in
`src/GK2DonkeyAPI/Api/MountLooks.cs`, `MountCatalog.cs`, `MountEffects.cs`. Design: `docs/skins-design.md`; the pack
format for anyone: `docs/skins.md`; colours on every mount: `docs/mount-colours.md`.

## Each skin

### Donkey: the game's own
- Nothing of ours. The 44 frames as the game places them, for the tools: `BepInEx\GK2-asset-probe\donkey-frames\`
  (written by the asset probe, `tools/GK2AssetProbe`, "Export frames"); this is **the canvas** every pack picture is
  measured and drawn on (`docs/sprite-tools.md`, "The canvas, found wrong").
- Outside the repo, game art, never committed: that folder, the only copy any tool reads (the raw export
  `C:\ClaudeCodeTemp\GK2-donkey-frames\0301_*.png` and the top-left-aligned `GK2-aseprite\harness_frames\*.raw`
  were retired 2026-10-19 23:30).

### Hellfire (donkey, pack drawn over the donkey; a recipe in code until 2026-10-19 evening)
- **Pack**: `src/SaddleUp/assets/mounts/donkey/skins/saddleup.hellfire/` (`donkey/saddleup.hellfire`) (`skin.cfg` + a PNG per frame), embedded in the DLL like the
  creatures. The pictures hold only our pixels (horns, the devil's tail, the rump fill, the cracks, the glowing
  eyes) and erase marks (magenta) where the ears and the tail come off; the ash coat is the cfg's `[Base.Coat]`
  recolour. Nothing of the game's art.
- **Generator**: `tools/skins/hellfire/make_hellfire.py`: runs the reviewed prototype (`hellfire_proto.py`, the 14
  rounds, same folder) over the donkey's frames on the game's canvas and keeps the difference as the pack.
  Outputs in `out\` (git-ignored): `pack\` (the same pack plus a sheet for the checker), `strips\`, `preview.png`.
- The recipe code (`LooksHellfire.cs`) is gone; recipes stay for mods through `Mounts.Looks.RegisterRecipe`.
- **Old against new, pixel by pixel**: `tools/skins/hellfire/compare_hellfire.py` -> `out\compare.html`: every frame the
  code drew (the prototype's output on the game's canvas, `C:\ClaudeCodeTemp\GK2-donkey-frames\hellfire-96\`) next to the
  pack over the donkey, with the prototype's coat (identical: 44 of 44 since the tail's phase matched, 2026-10-19 21:00;
  since 22:45 the only difference is intended: the 326 mane pixels the pack draws between the horns so the tuft there
  is the same in every frame, where the code left the donkey's own, open in some frames and filled in others) and
  with the game's coat rule (only coat shades differ), and a difference picture with the count.
- Docs: `docs/sprite-tools.md`, `docs/skins.md` ("Erasing from the donkey", "Recolouring the donkey under the skin").

### Royal Parade (donkey, pack drawn over the donkey; a built-in since 2026-10-19 23:00)
- **Pack**: `src/SaddleUp/assets/mounts/donkey/skins/saddleup.royal-parade/` (`donkey/saddleup.royal-parade`) (`skin.cfg` by hand: `layer = over`, `[Colors] Harness = ...`
  so the cloth's reds follow the harness colour; a PNG per frame generated), embedded in the DLL exactly like
  Hellfire (before: `packs/royal-parade/` as one sheet, copied into the game's `gk2skins\` by the generator).
- **Generator**: `tools/skins/royal-parade/make_royal.py` (the saddle cloth cut from the standing frame and hung from
  the hip in every frame, the crown on the eye), with `contact.py` (the frame loader), `measure.py` -> `anchors.json`,
  `horn_bases.py` -> `horn_bases.json` (the crown's fallback anchors, measured from the Hellfire pack's horns),
  `zoom.py`, `view.py`, `Watch Royal Parade.cmd`. Outputs in `out\` (git-ignored): `royal.aseprite` (a by-hand copy),
  `preview.png`, `zoom.png`, `strips\<animation>.png` (look at every strip before the game), `pack\sheet.png`.
- **In the game**: nothing on disk; the pictures come with the DLL (a build + deploy after every generator run).
  The old loose copy in `gk2skins\royal-parade\` was removed 2026-10-19 23:00 (a second skin with the same id is
  refused at start).
- Docs: `docs/sprite-tools.md` ("Generated, no drawing", "The canvas, found wrong", "The crown"), `docs/open-items.md`.

### Red Dragon, Giant Rat, Giant Snail, Giant Worm (their own mounts; Saddle Up's packs since 2026-10-20)
- **Ids**: the mounts `saddleup-dragon.dragon`, `saddleup-critters.rat`, `saddleup-critters.snail`, `saddleup-critters.worm`; each one's look is its
  default skin, `saddleup-dragon.dragon/default` and so on (the skins were `red-dragon`, `grey-rat`, `garden-snail`,
  `earth-worm` earlier on 2026-10-24, and before that the mounts' plain ids).
- **Pictures and cfg**: `packs/SaddleUpDragon/gk2mounts/saddleup-dragon.dragon/` and
  `packs/SaddleUpCritters/gk2mounts/saddleup-critters.<rat|snail|worm>/` (external packs since 2026-10-26): the mount's `mount.cfg`
  (`[Mount]`, its tuning and sounds; the dragon's `sounds\` holds its flight's twelve `.ogg`) and `skins/default/`
  (44 PNGs each, the dragon's more for its flight and a
  `front\` layer drawn above the rider, and a `skin.cfg` with the seat, the skin's geometry and its `[Colors]`).
  Embedded into Saddle Up's DLL by its csproj (`GK2SaddleUp.mounts.<mount>\mount.cfg`,
  `GK2SaddleUp.mounts.<mount>\skins\<skin>\<file>`: `GK2SaddleUp.mounts.saddleup.dragon\skins\default\...`) and registered through `Mounts.Looks.RegisterEmbedded` in its
  Awake (docs/mounts-as-mods.md).
- **Generator**: `tools/skins/creatures/make_creatures.py` (our own art, drawn by code: a crawl cycle for the walk, idle frames for
  the rest; writes straight into `assets\mounts\<mount>\skins\<skin>\` like every other generator, and the
  mount's `mount.cfg` once, one creature with its name as the argument; the probe's `test\skins\` output is
  gone). Colours per region:
  `tools/skins/skin_colors.py` (writes each `[Colors]` section; run after the art changes, then `tests/SkinsScan`).
- Docs: `docs/mount-looks-design.md` (the creatures), `docs/mount-colours.md`, `docs/mounts-design.md` (mounts).

### Gerry (its own mount, game creature; Saddle Up's pack since 2026-10-20)
- **Cfg only**: `src/SaddleUp/assets/mounts/saddleup.gerry/mount.cfg` (the mount `saddleup.gerry`: `noun = skull`,
  babies 0.33, his sounds) and `skins/default/skin.cfg` (`source = game`, `[Game] prefix = 0901_`, the seat
  from the picture). The frames are the game's talking skull, read back and scaled at run time: `LooksGame.cs`.
- **Tools**: `tools/skins/gerry/gerry_probe.py`, `tools/skins/gerry/gerry_sheet.py` (the exported frames, contact sheets). Docs:
  `docs/gerry-mount.md`.

### Sepia and Checker (example mod, source only)
- `examples/GK2DonkeySkins/Plugin.cs`: `donkey/example.sepia`, a recipe registered from code; `default`, pictures drawn by
  code (`IMountSkinSource`) as the look of a mount of its own, `example.checker-beast` (so
  `example.checker-beast/default`). Built and deployed only when the
  example is being tested (`-p:Deploy=true`); not in the game folder otherwise (taken out 2026-10-19 23:30, with
  the loose `gk2skins\worm-pack\` that tested the folder loader).

### Retired (gone 2026-10-19 23:30)
- The probe's `test\skins\` folders (the five recolour recipes, the blue dragon, two experiments, the creature and
  Royal Parade composites of the old generators) and `tools/skin_recipes.py` that made the recolours. The review
  page's "Experiments" section shows nothing until a folder appears there again.

## The tools around all of them

| Tool | Does |
|---|---|
| `tools/GK2AssetProbe` (dev plugin, Shift+F7) | exports the donkey's frames to `donkey-frames\`, the rider, films to `captures\`; its `test\skins\` tester |
| `tools/skins/skin_preview_html.py` | the review page, every skin by mount, frame strips, all 44 frames; shows the game's own export (`game-skins\`) when there is one and counts where its composite would differ |
| `tools/skins/check_skin.py` | checks a pack folder (frames and the front layer, colours per region, lone pixels over what the game draws beneath, the layout; exit code = errors only); run by the watcher and the release scans on every built-in |
| `tools/skins/new_skin.py` | scaffolds a new skin: `<skin> [--mount <mount>]` puts its `skin.cfg` in `assets/mounts/<mount>/skins/<skin>/` (`--pack <Pack>`: `packs/<Pack>/gk2mounts/<mount>/skins/<skin>/`) in the canonical layout (the donkey's without `--mount`; `default` for a new mount's own look), and a new mount's folder and `mount.cfg`; a generator stub, the Aseprite sidecar; runs the scan |
| `tools/skins/review.py` | the review pictures every generator writes the same way (strips with the front layer and the rider on the seat row, preview, pack sheet) |
| `tools/skins/skinroots.py` | where the mounts and skins are: every `src/<project>/assets/mounts/` and `packs/<Pack>/gk2mounts/` (`roots()`; `kind()`, `pack_of()`), its mount folders (`mount_dirs()`) and their `skins/<skin>/` (`skin_dirs()`; `mount_of(dir)`, `find(skin_id)`, `find_mount(id)`, `root_of(project)`): Saddle Up's built-in Gerry and donkey skins and its two external packs; the API ships none; every tool and scan below walks them through it |
| `tools/skins/canonical_cfg.py` | rewrites every shipped `skin.cfg` and `mount.cfg` into its one layout: a skin's `[Skin]`, `[Game]`, its `[Tuning]` / `[Tuning.Ride]` / `[Tuning.Cart]` (its geometry), its `[Sounds*]`, the colours; a mount's `[Mount]`, `[Stats]`, `[Tuning*]`, `[Sounds*]` (`--check` in the release scans) |
| `tools/skins/creatures/make_creatures.py`, `tools/skins/skin_colors.py`, `tools/skins/creatures/colors.json` | the four drawn creatures into their folders (`mounts/<mount>/skins/<skin>/`, the `mount.cfg` once; strips per creature in `out\<name>\`); their `[Colors]` sections from the pixels (`front\` included, Gerry's from the game's export) by the rules in the json (a block per skin) |
| `tools/skins/gerry/gerry_probe.py`, `gerry_sheet.py` | a game character's frames exported (the bundle found by content, the prefix an argument) and laid out for measuring |
| `tools/skins/watch_skin.py`, `tools/skins/scripts/*.lua` | the by-hand loop: Aseprite -> export -> the sheet split into the built-in's per-frame PNGs (`front\` too) -> check -> previews with the front layer and the rider; the two menu scripts (also installed in `%APPDATA%\Aseprite\scripts\`) |
| `tools/skins/ase_write.py` | writes an `.aseprite` file (layers, frames, tags) from Python |
| `tools/skins/gamedir.py` | the game folder from `Directory.Build.props`, the frames and packs folders in it (`packs_dir()` = `BepInEx\plugins\gk2mounts`) |
| `tools/skins/pngcolors.py` | a plain PNG reader and colour counter |
| `tests/SkinsScan` | every built-in mount and skin: each `mount.cfg` (its keys, no `default_skin`, a `skins\default\skin.cfg` beside it, the folder named `<mod>.<name>`, its tuning keys, its sounds in the mount's `sounds\`, the section order; the donkey's folder without one and without a `default`) and each `skin.cfg` key by key and value by value (no `id =` line, no `[Mount*]` section), the layout, pictures, `[Colors]` against its pixels (in the release scans) |
| Dev panel, Mounts tab (`src/GK2DonkeyAPI/Dev/TuningWindow.Mounts.cs`) | the worn skin's rows, "Reload from the file", "Reload by itself when the pack's files change", "Export the loaded skins as built" (`LooksExport.cs`) |
| Self-tests (`src/GK2DonkeyAPI/Dev/SelfTest.cs`, "Skins and effects") | packs, recipes, over skins with the world readback, auto-reload, mounts |

Outside the repo and never committed, because it is the game's art: the asset probe's `donkey-frames\`, the
captures, and `C:\ClaudeCodeTemp\GK2-donkey-frames\hellfire-96\` (the code version's frames, the compare page's old
side, until Hellfire is confirmed in game). Aseprite
itself: `C:\Users\User\Documents\GitHub\aseprite\build\bin\aseprite.exe` (built from source, personal use).
