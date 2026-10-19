# Where every skin lives

Every skin the API knows, what it is made of, where its source and its tooling are, and what is read from outside
the repo. As of 2026-10-19 evening (after the skin tooling moved into the repo). The review page
(`tools/skin_preview_html.py` -> `BepInEx\GK2-asset-probe\skins-preview.html`) shows them all, grouped like this.

## The three kinds of skin

| Kind | What ships | How the pictures come to be |
|---|---|---|
| **Recipe** | code in the API | built in the game from the donkey's own frames when first worn; no picture stored anywhere |
| **Pack** | `skin.cfg` + pictures (a PNG per frame, or one sheet of cells) | drawn or generated beforehand; a built-in pack is embedded in the DLL, a loose one sits in `BepInEx\plugins\gk2skins\<id>\`. **Every skin we ship is a built-in pack made the same way** (`docs/sprite-tools.md` "Making a skin: the one pipeline") |
| **Game creature** (`source = game`) | `skin.cfg` only | the game's own frames of another character, read back and scaled at run time |

The machinery for all three: `src/GK2DonkeyAPI/Looks.cs` (packs, sheets, sources, the overlay drawing, colours by
region), `LooksRecipes.cs` (recipes), `LooksGame.cs` (game creatures), `LookPreview.cs` / `PreviewStage.cs` (the
pictures in the menus), `LookBar.cs` (the colours window), `Effects.cs` (particle effects), the public API in
`src/GK2DonkeyAPI/Api/MountLooks.cs`, `MountKinds.cs`, `MountEffects.cs`. Design: `docs/skins-design.md`; the pack
format for anyone: `docs/skins.md`; colours on every mount: `docs/mount-colours.md`.

## Each skin

### Donkey: the game's own
- Nothing of ours. The 44 frames as the game places them, for the tools: `BepInEx\GK2-asset-probe\donkey-frames\`
  (written by the asset probe, `tools/GK2AssetProbe`, "Export frames"); this is **the canvas** every pack picture is
  measured and drawn on (`docs/sprite-tools.md`, "The canvas, found wrong").
- Outside the repo, game art, never committed: that folder; `C:\ClaudeCodeTemp\GK2-donkey-frames\` (the raw export,
  `0301_*.png`); `C:\ClaudeCodeTemp\GK2-aseprite\harness_frames\*.raw` (top-left aligned, only for moving old
  measurements).

### Hellfire (donkey, pack drawn over the donkey; a recipe in code until 2026-10-19 evening)
- **Pack**: `src/GK2DonkeyAPI/assets/skins/hellfire/` (`skin.cfg` + a PNG per frame), embedded in the DLL like the
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
- **Pack**: `src/GK2DonkeyAPI/assets/skins/royal-parade/` (`skin.cfg` by hand: `layer = over`, `[Colors] Harness = ...`
  so the cloth's reds follow the harness colour; a PNG per frame generated), embedded in the DLL exactly like
  Hellfire (before: `packs/royal-parade/` as one sheet, copied into the game's `gk2skins\` by the generator).
- **Generator**: `tools/skins/royal-parade/make_royal.py` (the saddle cloth cut from the standing frame and hung from
  the hip in every frame, the crown on the eye), with `contact.py` (the frame loader), `measure.py` -> `anchors.json`,
  `horn_bases.py` (`horn_bases_raw.json` -> `horn_bases.json`, the crown's fallback anchors from Hellfire's horns),
  `zoom.py`, `view.py`, `Watch Royal Parade.cmd`. Outputs in `out\` (git-ignored): `royal.aseprite` (a by-hand copy),
  `preview.png`, `zoom.png`, `strips\<animation>.png` (look at every strip before the game), `pack\sheet.png`.
- **In the game**: nothing on disk; the pictures come with the DLL (a build + deploy after every generator run).
  The old loose copy in `gk2skins\royal-parade\` was removed 2026-10-19 23:00 (a second skin with the same id is
  refused at start).
- Docs: `docs/sprite-tools.md` ("Generated, no drawing", "The canvas, found wrong", "The crown"), `docs/open-items.md`.

### Red Dragon, Giant Rat, Giant Snail, Giant Worm (their own mounts, built-in packs)
- **Pictures and cfg**: `src/GK2DonkeyAPI/assets/skins/<dragon|rat|snail|worm>/` (44 PNGs each; the dragon also a
  `front\` layer of 41 pictures drawn above the rider), embedded into the DLL by the csproj
  (`GK2DonkeyAPI.skins.<id>.<file>`). Each `skin.cfg` declares the mount (`[Mount]`, its tuning) and its `[Colors]`.
- **Generator**: `tools/creature_sprites.py` (our own art, drawn by code: a crawl cycle for the walk, idle frames for
  the rest; writes to the probe's `test\skins\` and, with a second argument, to `assets\skins\`). Colours per region:
  `tools/skin_colors.py` (writes each `[Colors]` section; run after the art changes, then `tests/SkinsScan`).
- Docs: `docs/mount-looks-design.md` (the creatures), `docs/mount-colours.md`, `docs/mounts-design.md` (kinds).

### Gerry (its own mount, game creature)
- **Cfg only**: `src/GK2DonkeyAPI/assets/skins/gerry/skin.cfg` (`source = game`, `[Game] prefix = 0901_`, the seat
  from the picture). The frames are the game's talking skull, read back and scaled at run time: `LooksGame.cs`.
- **Tools**: `tools/gerry_probe.py`, `tools/gerry_sheet.py` (the exported frames, contact sheets). Docs:
  `docs/gerry-mount.md`.

### Worm (from a pack) (worm mount, loose pack, test)
- `BepInEx\plugins\gk2skins\worm-pack\`: the worm's 44 pictures copied as a loose pack with `mount = worm`, to test
  the folder loader and a pack joining a built-in mount. No source of its own in the repo (the pictures are the
  built-in worm's). Checklist: taken out of `plugins\` before a release rehearsal.

### Sepia and Checker (example mod)
- `examples/GK2DonkeySkins/Plugin.cs`: "sepia", a recipe registered from code; "checker", pictures drawn by code
  (`IMountSkinSource`) as the look of a mount kind of its own, `checker-beast`. Deployed as
  `BepInEx\plugins\GK2DonkeySkins\GK2DonkeySkins.dll` on the dev machine (taken out before a release rehearsal).

### Retired experiments (not known to the API)
- `BepInEx\GK2-asset-probe\test\skins\{ember, ghost, golden, shadow, zebra, dragon-blue, dragon_high, zombie}`: the
  five recolour recipes replaced by the colour settings, the blue dragon replaced by a custom coat, two experiments.
  `tools/skin_recipes.py` made the recolours. The review page shows them collapsed at the end.

## The tools around all of them

| Tool | Does |
|---|---|
| `tools/GK2AssetProbe` (dev plugin, Shift+F7) | exports the donkey's frames to `donkey-frames\`, the rider, films to `captures\`; its `test\skins\` tester |
| `tools/skin_preview_html.py` | the review page, every skin by mount, frame strips, all 44 frames; shows the game's own export (`game-skins\`) when there is one and counts where its composite would differ |
| `tools/skins/check_skin.py` | checks a pack folder (frames, colours vs the donkey's, lone pixels); run by the watcher |
| `tools/skins/watch_skin.py`, `tools/skins/scripts/*.lua` | the by-hand loop: Aseprite -> export -> check -> previews; the two menu scripts (also installed in `%APPDATA%\Aseprite\scripts\`) |
| `tools/skins/ase_write.py` | writes an `.aseprite` file (layers, frames, tags) from Python |
| `tools/skins/gamedir.py` | the game folder from `Directory.Build.props`, the frames and packs folders in it |
| `tools/pngcolors.py` | a plain PNG reader and colour counter |
| `tests/SkinsScan` | every built-in skin: its cfg, pictures, `[Colors]` against its pixels (in the release scans) |
| Dev panel, Mounts tab (`src/GK2DonkeyAPI/Dev/TuningWindow.Mounts.cs`) | the worn skin's rows, "Reload from the file", "Reload by itself when the pack's files change", "Export the loaded skins as built" (`LooksExport.cs`) |
| Self-tests (`src/GK2DonkeyAPI/Dev/SelfTest.cs`, "Skins and effects") | packs, recipes, over skins with the world readback, auto-reload, kinds |

Outside the repo and never committed, because it is the game's art: the asset probe's `donkey-frames\`, the
captures, `C:\ClaudeCodeTemp\GK2-donkey-frames\` and `C:\ClaudeCodeTemp\GK2-aseprite\harness_frames\`. Aseprite
itself: `C:\Users\User\Documents\GitHub\aseprite\build\bin\aseprite.exe` (built from source, personal use).
