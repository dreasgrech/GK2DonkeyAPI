# Sprite tools: getting off the pixel-by-pixel loop

Investigation of 2026-10-19, after Hellfire took 14 rounds of frames (v1 to v14) to get one look right. This page is a
decision page: what slowed us down, what the mod already offers, which outside tools fit, and what is still open.
Anything marked *unverified* comes from the tool makers' own pages and has not been tried here.

## Why Hellfire took 14 rounds

Hellfire is a **recipe**: the art is code (`src/GK2DonkeyAPI/LooksHellfire.cs`; its reviewed Python prototype is kept as `tools/skins/hellfire/hellfire_proto.py`, since 2026-10-19 evening). Every
change went the same long way: you describe what is wrong in words, I change numbers in code, render 44 frames,
you look at PNGs, you describe the next thing. Nobody ever drew a pixel and saw the result. The slow part was not
the drawing; it was that every fix crossed from picture to words to code and back. Horns that "should sit where
they are in ear 02 to 04" are one drag in an editor and three rounds in code.

So the fix is to put the art where you can see what you change, and keep code only for what has to be code.

## What the mod already has (no new tools needed)

- **Packs of pictures** (docs/skins.md): a PNG per frame (44, named `bdy_walk_left_01.png` and so on) or one
  `sheet.png`, 96x96, facing left, feet on row 64, back on row 41. A frame left out shows the donkey's own, and
  `bdy.png` / `bdy_walk_left.png` fill all the undrawn body or walk frames.
- **`layer = over`**: pictures drawn **on top of** the donkey (a saddle, a hat). Horns, a devil tail, cracks and
  glowing eyes are additions, so they fit here, and an overlay has no game pixels in it, so it can be shipped.
- **Templates**: the 44 game frames exported at full size (`C:\ClaudeCodeTemp\GK2-donkey-frames\`, or the asset
  probe's "Export every frame"). Paint on a layer over them; export only your layer.
- **Reload in game** (dev panel, the skin's rows, "Reload from the file", `Dev/TuningWindow.Mounts.cs:403`): reads
  `skin.cfg` and **the pictures again from disk** (checked in code: the pack's reader opens each file fresh). The
  list of file names is taken when the game starts (packs are scanned only then, `Looks.cs:345`; the "reload
  folders" of skins-design.md was never built), so a frame file added after start needs a restart; a pack with one
  `sheet.png` never has that problem.

Draw, save, press reload, look: seconds per round instead of a round of messages.

## The one gap: taking pixels away

`layer = over` only adds. Hellfire also **erases** the donkey's tail, **refills** the rump curve and **recolours** the
coat to ash. An overlay can't do the first two, and `layer = replace` means repainting the whole donkey, which can't
be shipped because the donkey's pixels are the game's.

How the work would split:

| Part of Hellfire | Paintable as an overlay today | Needs code |
|---|---|---|
| Horns, devil tail, lava cracks, glowing eyes | yes | |
| Ash coat (recolour) | | yes, but it is one colour rule, not shape work (it never needed a round) |
| Donkey tail erased, rump curve filled | | yes: this was the shape work |

**Decision for you:** a small extension would close the gap, for example an overlay pixel of one reserved colour
(say pure magenta) meaning "clear the donkey here", with the rump fill painted as normal overlay pixels. Rough cost:
a short addition in the pack loader and the sheet builder, a self-test, a skins.md section. Not built; your call.
Without it, the split is: shapes painted, erase and recolour stay in the recipe.

## Outside tools

Two different users: **you drawing** (an editor), or **me drawing** (a tool I can drive instead of pixel maths).

### Editors for you

| Tool | Cost | What matters for us |
|---|---|---|
| **Aseprite** | about $20 (Steam or aseprite.org) | The reference pixel editor. Layers (template locked underneath), **onion skin** (see the frames before and after), **tags** per animation (walk, ear, graze...) with a looping preview, **linked cels** (draw a horn once, it is in every frame; move it per frame where the head moves). Command line and Lua scripting for batch export ([CLI docs](https://www.aseprite.org/cli/)), so export to the pack folder can be one command. Most AI plug-ins target it. |
| **Pixelorama** | free (MIT) | Layers, onion skin, cel linking, timeline ([review](https://scribe.disroot.org/post/2025114)). The free choice; PixelLab's extension supports it too. *Unverified here.* |
| **LibreSprite** | free | Fork of an old Aseprite; core features close to it, onion skin not confirmed in what I found ([alternatives](https://www.alternativeto.net/software/aseprite/)). Behind Pixelorama for us. |

None of these was found in the usual places on this machine (Program Files, Steam, PATH; an itch.io or portable
copy under your profile would not show).

**The Aseprite trial (tested 2026-10-19, v1.3.18.6-trial):** opens and edits files, but every save and export is
refused ("Save operation is not supported in trial version"; tried from the command line, where a
`--sheet` export writes the JSON data but not the PNG), and the `--script` option is missing. So the trial is for
judging the editing, not for making a pack. A starting file for that, built by script without Aseprite's help:
`C:\ClaudeCodeTemp\GK2-aseprite\hellfire.aseprite` (96x96, 44 frames in pack order; layers "donkey (game)" and
"Hellfire v14" locked, "draw here" open; tags static, walk, ear, graze, chew, lips, turn, head; built from the
harness frames by `C:\ClaudeCodeTemp\GK2-aseprite\make_ase.py`). Aseprite read it back with the right layers and tags.

**Built from source (2026-10-19):** the EULA allows compiling for your own use (EULA 2(g); not to be passed on). Built with VS 2022
Build Tools at `C:\Users\User\Documents\GitHub\aseprite\build\bin\aseprite.exe`: the full version, with saving, export, the command line
and Lua scripting. Exporting the "Hellfire v14" layer of `hellfire.aseprite` as a sheet gives the game's Hellfire frames
exactly (44 frames, 0 pixel differences). Command-line note: `--layer` goes before the file name. What was installed
where: `C:\ClaudeCodeTemp\GK2-aseprite\INSTALL-LOG.md`.

Recommendation: **Aseprite** if $20 is fine (linked cels plus the command line are exactly what a 44-frame overlay
needs); **Pixelorama** if it should be free.

### Tools for me

- **Aseprite MCP servers**: several community projects let Claude Code drive a licensed Aseprite through its Lua API
  (layers, frames, cels, export): [rkdfx/aseprite-mcp](https://github.com/rkdfx/aseprite-mcp),
  [ext-sakamoro/AsepriteMCP](https://glama.ai/mcp/servers/@ext-sakamoro/AsepriteMCP),
  [willibrandon/pixel-mcp](https://tomevault.io/tome/willibrandon/pixel-mcp). *Unverified; community-made, check
  activity first.* Honest note: I would still be placing pixels blind, so this saves plumbing, not rounds. What
  helps more is me preparing files for you to edit (below), not me drawing in an editor.
- **Plain Aseprite command line** (no MCP needed): I can script building the starting file and the export.

### AI generators (only worth a small test)

- **PixelLab** ([site](https://www.pixellab.ai/)): AI pixel art with "animate with skeleton", "animation to
  animation" and an "Edit animation (Pro)" tool that needs at least 2 frames and runs only inside the Aseprite and
  Pixelorama extensions ([docs](https://www.pixellab.ai/docs/tools/edit-animation-pro)). Its consistency claims are the
  makers'. Our frames carry the head inside the body and must keep feet and back on fixed rows, which is exactly where
  such tools slip. If tried: the 8 walk frames only, checked with the compare scripts, not all 44.
- **Retro Diffusion** ([itch.io](https://astropulse.itch.io/retrodiffusion)): image generation inside Aseprite,
  $20 lite / $65 full, one-time. Better for new creatures from scratch than for editing ours. Noted, not suggested.

## Proposed workflow

1. You pick an editor (Aseprite or Pixelorama).
2. I build the starting file: the 44 game frames as a locked bottom layer, grouped into tags per animation, and
   Hellfire's current horns, tail and cracks (from the v14 output, game pixels removed) as editable layers. Fixing
   Hellfire then starts from what you reviewed, not from scratch.
3. You draw and move things by eye, with onion skin and the loop preview.
4. Export into `BepInEx\plugins\gk2skins\<id>\` as one `sheet.png` (so reload always works); in Aseprite I can make
   that one command or a button.
5. In game: dev panel, "Reload from the file", look.
6. I check what you exported before you look in game: sizes, feet and back rows, transparency, stray colours, no game
   pixels left in the overlay. That catches the mechanical errors, so your rounds are only about looks.

The erase and recolour stay in code until you decide on the extension above.

## The workflow as built (2026-10-19, first used for Royal Parade)

The tooling is in the repo since 2026-10-19 evening (the user, after a commit: "where are these changes being made exactly?");
only the game's frames stay outside it (the asset probe's `donkey-frames\` in `BepInEx\`, read by path):

| What | Where |
|---|---|
| Aseprite, the full build | `C:\Users\User\Documents\GitHub\aseprite\build\bin\aseprite.exe` |
| The two menu scripts (File > Scripts) | `tools/skins/scripts/`, installed in `%APPDATA%\Aseprite\scripts\` |
| The checker, the watcher, the .aseprite writer, the game-folder helper | `tools/skins/check_skin.py`, `watch_skin.py`, `ase_write.py`, `gamedir.py` (the game folder from Directory.Build.props) |
| The donkey's 44 frames as the game places them | `BepInEx\GK2-asset-probe\donkey-frames\` (the asset probe writes them) |
| Royal Parade: the generator, its measurements, its outputs | `tools/skins/royal-parade/` (`make_royal.py`, `contact.py`, `measure.py` -> `anchors.json`, `horn_bases.py` -> `horn_bases.json`); `out\` (git-ignored): `royal.aseprite`, `preview.png`, `zoom.png`, `strips\` |
| Royal Parade: the pack's source | `packs/royal-parade/` (`skin.cfg` by hand, `sheet.png` generated); the generator copies both into the game's `gk2skins\royal-parade\` |
| The old frames (top-left aligned, see "The canvas, found wrong") | `C:\ClaudeCodeTemp\GK2-aseprite\harness_frames\*.raw`, read only by `contact.load_raw` |

A round:

1. Open the skin's `.aseprite` (Royal Parade: `tools/skins/royal-parade/out/royal.aseprite`, regenerated by every `make_royal.py` run). Layers: `donkey (game)` locked at the bottom (the reference, never exported),
   then the skin's parts, one layer each (Royal Parade: `caparison`, `crown`); a tag per animation.
2. Fix what is wrong by eye: Enter plays the tag, F3 shows the frames before and after, Move (V) drags a part in
   one frame. A part redrawn once goes to the rest of its tag with **File > Scripts > GK2 Part to all frames**
   (each frame keeps where its own copy sat, lined up by the bottom centre).
3. **File > Scripts > GK2 Export skin**: every visible layer except the donkey into the pack's `sheet.png`
   (layers named `front...` into `front.png`), then the checker's verdict in the same window. The pack folder and
   the check command are in `<name>.gk2export.txt` next to the `.aseprite` file. The first time, Aseprite asks
   whether the script may save a file and run a command: choose "Give full trust to this script" both times. New
   scripts show in the menu after File > Scripts > Rescan Scripts Folder (or a restart).
4. In game: dev panel, the skin's rows, "Reload from the file". Look.

**Hands off (built 2026-10-19 03:00):** steps 3 and 4 run by themselves.

- `tools/skins/royal-parade/Watch Royal Parade.cmd` (double-click; `tools/skins/watch_skin.py <file.aseprite>` for any skin) watches the file: on
  every save it runs the export from the command line (no trust prompt there), the checker, and writes previews
  into `preview\` next to the file: `contact.png` (all 44 frames over the donkey, 4x) and a looping GIF per
  animation at game speed. Leave its window open while drawing; Ctrl+C stops it.
- In game, the dev panel's Mounts tab, under "Reload from the file": **Reload by itself when the pack's files
  change** (`Debug.SkinPackAutoReload`, off by default, only with the dev tools unlocked). A pack in use is read
  again a second or two after its files settle. Pictures added after the game started still need a restart.

So a round is: draw, Ctrl+S, look at the game (or the GIFs). Nothing else to press.

**Generated, no drawing (2026-10-19 18:10, the user: "automate as much as possible, I'm not a sprite designer"):**
`tools/skins/royal-parade/make_royal.py` makes the whole skin from the donkey's own frames and writes the pack's `sheet.png` into `packs/royal-parade/` and,
with `skin.cfg`, into the game (`--no-pack` leaves the game alone), plus `royal.aseprite` for a by-hand touch, `preview.png` (all
frames, 4x), `zoom.png` (six frames, 8x, the grid) and **`strips\<animation>.png`, every animation as a strip of
its frames over the donkey: look at every strip before the game does.** The cloth is one shape measured on the
standing frame (its width the back from the collar strap to the hip plus `FRONT`, its depth per column the back to
`HEM_ABOVE` rows above the belly, smoothed) and in every frame it hangs from the **hip**, the rump's end at back
level, the one point that stays put while the head, neck and legs move: the same width back from the hip, each
column's top on that frame's own back line, held down to the back's level where the neck starts to climb (the
first hip-anchored cut let the front columns follow the neck up, so in the game the cloth's front corner stood
above the back and sank in a graze: seen in the Mods picture frames of the user's second video, 18:03). So it
rides the walk's bob and the graze's dip without changing size or sliding (the first cut measured the collar and
belly per frame and jumped in the graze clip, seen in the user's first video). The same frames, composited, go to
the asset probe's `test\skins\royal-parade\` and `skins-preview.html` (tools/skin_preview_html.py), the page that
plays every skin's clips next to the plain donkey. A frame with a shorter back (the turn clips) gets it cut shorter. Then a dark outline, a gold border, light
red under the back, dark red above a gold hem row, a fringe, the fleur in the middle of the red, the crown at the
Hellfire horn bases. The first draft (a rectangle hanging under the belly) was seen in game as one yellow plate: the
user's Custom Mustard harness recolours the cloth's reds (the pack's `[Colors]`), which then match the gold trim.
Changing the cut is editing the numbers at the top of the script and running it; the checker runs on the result;
the game reloads the pack by itself (the tick above) or on "Reload from the file".

**The canvas, found wrong 2026-10-19 18:30 (the root of every placement fault seen that day):** the 44 `.raw` frames
in `harness_frames\` put every frame's top-left corner at (25, 21). The game stands every frame on one ground row,
with the feet at y 66 and the x shifting with the pose: the asset probe's `donkey-frames\*.png` are that placement.
The two agreed on no frame: 2 px off standing, up to 8 in the walk, up to 17 and sideways in the graze. So a cloth cut
on the raw canvas lifted off the back in the walk, climbed in the graze and the crown drifted, in the game and in the
review page (which composites over the probe's frames), while every strip made on the raw canvas looked right.
`contact.load(k)` reads the probe's PNGs now; `load_raw(k)` and `raw_to_game_shift(k)` exist only to move things
measured on the old canvas (the Hellfire horn bases: `horn_bases_raw.json` -> `horn_bases.json`; the Hellfire review
frames `hellfire-96\`). `check_skin.py` reads the probe's frames too. **Rule: everything drawn for a pack is measured
and drawn on the probe's frames, never on `harness_frames\*.raw`.**

**The crown (2026-10-19 18:45, the user: "a bit more forward on his head"):** anchored on the eye where the frame
has one (`anchors.json`, measured on the game's canvas): its centre `CROWN_BEHIND_EYE` columns behind the eye, its
bottom row `CROWN_ABOVE_EYE` rows above it (2 and 3 after the user's "closer to the eye, barely on his head": the crown's
last two rows overlap the head's top, so it sits); the eye is the one landmark of the head no ear flick
moves (resting it on the outline under its own columns put it on a forward-leaning ear). Frames without a found eye
(graze 3-5, lips 4-5, turn 4-6): a head facing the camera keeps it between the ears two rows down, else it rests on
the forehead's outline under its three front columns, `CROWN_FORWARD` columns ahead of the ear bases.

**What the previews cannot show:** they composite the pack's pixels in Python; the game draws the overlay through
its own material and colour-swap shader. Royal Parade showed in the world without its red (the Mods picture had it),
and no preview could have caught that. The self-test "colours: a skin over the donkey keeps the donkey's regions"
now reads the drawn overlay back from a camera (docs/self-tests.md) and logs the same for the player's worn skin.

The checker reports: frames drawn, every colour with its region (`kept` = as drawn), colours the donkey itself has,
half-transparent pixels, lone pixels, more than 48 colours to swap, and frames where the donkey is drawn the same
but the skin differs (a frame missed in an edit).

Tested 2026-10-19: the export equals the layers exactly (44 frames, 0 pixel differences); "Part to all frames" on
the walk tag kept each frame's crown bottom where it was (the bob kept) and left the other tags alone; the checker
caught a planted lone pixel, a donkey colour and a half-transparent pixel.

## Open

- Which editor: decided 2026-10-19, Aseprite built from source.
- The erase-colour extension: build it or not.
- Whether Hellfire moves to "overlay pack plus a small recipe" or stays a recipe now that v14 is right. Future skins
  are where this pays most.
