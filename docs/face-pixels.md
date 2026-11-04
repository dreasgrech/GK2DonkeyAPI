# The donkey's eye and nostril pixels

The user, on the Jester with "Legs and mane" Pink (a screenshot, 2026-11-04): "there are some pixels around the eye
which are affected by the legs and mange group", then "yes lets investigate this further to make sure we get it right
because it looks sloppy otherwise". This page is the investigation, the decisions and what was built (section 8).

## 1. What the pixels are

Measured on the game's 44 donkey frames (the dev export `BepInEx\GK2-donkey-frames`, the frames the skins are drawn
on; tools in the scratchpad, the rule below to become `tools/skins/donkey/face_pixels.py`):

- **The eye:** the pixel left of the eye's white and the two under it, 3 a frame (5 in the turn's three-quarter view,
  both eyes in the frames facing us), are **292316**, the darker shade of the dark fill. The same colour is the dark
  fill of the legs, hooves, ears and tail: 1907 pixels elsewhere against 110 at the eye. The colour swap goes by colour
  (docs/colors.md: a texel within 0.004 of a source colour takes its replacement), so it can't tell the two apart.
- **The nostril:** one pixel in the nose (two or three in a few frames, the mouth's corner with it) is **352e1e**, the
  mane's lighter shade: it follows "Legs and mane" too.
- **Only the game's art draws them:** no skin over the donkey (Royal Parade, Armored, Hellfire, the Jester) draws
  292316 or 352e1e within 2 pixels of the eye or 4 of the muzzle in its own pictures.

## 2. Which pixels, frame by frame

The eye's white is no anchor: the blink (lips 04-05) draws the closed lid as a line of 292316, and the head lowered to
graze (graze 03-05) shows the eye as a small dark cluster, both without a white. The anchor is `tools/skins/donkey/rig.json`'s
per-frame points, which the user approved in Hellfire's rounds: `eye` (and `eye2` facing us) and `muzzle`, on the
96-px canvas the frames are drawn on (the eye point is the white's pixel where there is one).

- **Eye pixels:** 292316 or 352e1e within 2 pixels (Chebyshev) of an eye point.
- **Nostril pixels:** 352e1e or 292316 with a 4-neighbour of the nose's colours (abb298, 858f6c, 75755a, a9a7a2),
  within 5 of the muzzle point.

On the 44 frames: 3 eye pixels and 1 nostril in every profile frame; the closed lid caught in the blink; the eye in the
grazing frames; both eyes in the frames facing us. The pixels the rule leaves near the eye (the contact sheet's yellow)
are the mane's strip at the top of the head: they stay the row's. Three calls to look at in the sheet:

1. Grazing 03-05: one eye pixel caught, three or four above it left (the forelock of the lowered head).
2. hed_static_down_03: the nostril set takes two mouth pixels under the nose (dark, next to the nose's colours).
3. Turn 03 (three-quarter): five eye pixels.

Also in the sheet: the nostril is two pixels in lips 04 and 09 (the lips moving), and the frames facing us (turn 04-06)
draw no dark nostril: their dark pixels near the muzzle point are the forelegs' (6 and 7 away, beside the outline).

## 3. Where they live

The donkey's 48 sprites (`0301_*`) are in the game's shared sprite atlas "Characters" (5756 sprites, two pages of
2048 x 2048), read offline with UnityPy: **RGBA32, uncompressed, point-filtered, one mip, not readable**. The API reads
a frame by a blit of its page and ReadPixels (`Looks.ReadFrame`: the skins' whole frames, the donkey-art check).

## 4. The ways to give them a colour of their own

- **A. Repaint them in the atlas, on the GPU (recommended if the probe agrees).** Once per page object, a few pixels
  per frame copied in (`Graphics.CopyTexture` from a small RGBA32 texture: the page has no CPU copy, and the copy needs
  none). Every look over the donkey sees them at once: the plain donkey, a skin's overlay frames (the donkey drawn under
  them), a skin's whole frames (built from the page). No frame cost, no new renderer. The game's own donkey (the
  delivery donkey) shows the new colours too, a step or two off the old ones: the same to the eye.
- **B. Draw the plain donkey from a copy of its frames** with the pixels repainted (a sheet, as a skin's), and build
  every frame of every skin over the donkey whole over that copy. The game's donkey untouched; but every donkey, foal
  and paper donkey drawn from our sprites instead of the game's, every skin's overlay frames built whole (more texture
  memory per skin), and the frame-by-frame code that reads the donkey's sprite names (the seat, the bob, the head) on
  our sprites. The fallback if A can't be done.
- **C. A small overlay sprite with those pixels** over every donkey, foal and paper donkey: a renderer and a sprite
  choice per frame on hundreds of donkeys for three pixels. Not proposed.

## 5. A's details

- **The colours:** **292319** for the eye's (+3 blue) and **362d1e** for the nostril's (+1 red, -1 green), each the
  nearest that is more than 0.004 from every colour paired on any look over the donkey (the donkey's 40, every skin's
  named colours and tones: 0.0118 and 0.0055), drawn nowhere (not in the donkey's frames nor in any skin's pictures)
  and in a colour-cube cell of its own (the Lut fallback keeps them apart too).
- **What they follow (decided 2026-11-04):** the eye's as drawn (it is the eye's line, as the outline is the
  drawing's), not the Eyes row; the nostril the Muzzle row (the nose's dark spot; the user: "yes nostril should be the
  same as muzzle"). The three calls of section 2 as proposed.
- **The list:** frame key, canvas pixel and its original colour, made from `rig.json` and the export by the tool and
  embedded in the API (as `donkey-frames.txt` is): one source for the points. A pixel is repainted only when the page
  holds its original colour there, so a game update that redraws the donkey leaves the page alone pixel by pixel.
- **Order:** the donkey-art check (`DonkeyFrames`) reads the frames first, as now, and its export stays the game's
  own art; the patch follows when the check says the frames are the ones the skins were drawn on (a frame the check
  calls changed is left alone). A skin's whole frame built before the patch is repainted the same way from the list
  as it is read (`ReadFrame`'s callers for builds), so the order can't leave a skin with the old colour.
- **The tools' export** (`tools/GK2AssetProbe`): the patch colours are drawn nowhere else, so an export maps them back
  to the originals exactly; the generators keep drawing on the game's art.
- **A page loaded again** (a new object after a scene, if Addressables releases it): repainted when the donkey's
  frames are found on a page object not seen before.

## 6. The probe (2026-11-04; replaced by the tests of section 8)

Two scenarios, nothing kept:

- **Measurements: "the donkey's frames in the game's atlas: pages, packing, a one-pixel copy into the page and back"**
  (`Dev/AtlasPatchProbe.cs`): the GPU and its copy support; each page's format, readability, mips and filter; the
  donkey's frames per page, packed, rotated, tight, a fractional rect; then the standing frame's eye pixel (left of the
  white, 292316) found in its page by reading it, 292319 copied in and read back through `ReadFrame`, 292316 copied
  back and read back, and how many of the frame's pixels differ after both (0 expected).
- **After the run: "the donkey's atlas pages the same objects after the run"**: the run crosses scenes and doors; the
  pages' objects then against the probe's.

**In game 2026-11-04 20:59 (F11 1850 passed, 0 failed, 0 warnings):**

- Direct3D 11, copy support Basic, Copy3D, DifferentTypes, TextureToRT, RTToTexture.
- The 48 donkey frames on the two pages (41 and 7), both 2048 x 2048 RGBA32, not readable, one mip, point-filtered.
  Packed as rectangles: none rotated, none tight; every texture rect fractional (bdy_static_left: textureRect x 656.05,
  y 393.08, offset 23.05, 28.08), the eye pixel's texel found by reading it: (666, 422) of the first page.
- The copy: 292319 in, read back 292319; 292316 back, read back 292316; 0 pixels of the frame different after both.
- After the run (scenes and doors crossed): all 48 frames on the same page objects.
- So A works on this machine: the page takes a one-pixel GPU copy and gives it back to the frame reads the skins'
  builds use, and it stays the same object through a run.
- The run itself was "158 of 159": the run took the After-the-run group's first scenario alone, and the probe's page
  check, put first in that group, left out the world's check ("the world's own donkeys came through unchanged").
  Fixed the same evening (`SelfTest.Run`: every scenario of the group, the world's check last).

## 7. Decided

The probe's F11 passed (section 6), the user decided section 5's two questions, and A was built once (section 8).

## 8. Built (2026-11-04)

- **The list:** `src/GK2DonkeyAPI/face-pixels.txt`, embedded in the DLL, made by `tools/skins/donkey/face_pixels.py`
  from `rig.json` and the asset probe's export: 178 pixels on 44 frames (the eye's line 292316 to 292319: 133; the
  nostril 352e1e to 362d1e: 45). The tool stops on a part's colour it has no new colour for, and checks the new colours
  on every run: drawn nowhere (the donkey's frames, the packs' skins), more than 0.004 from every other paired colour,
  alone in their colour-cube cell, 292319 in no region and 362d1e under Muzzle (`Colors.cs`; the donkey's pairs 41).
  `--check` is in the release scans.
- **The atlas** (`FacePixels.cs`, ticked after `DonkeyFrames`): once the donkey-art check says the frames are the
  ones the skins were drawn on (or differ: its changed frames left alone), one frame a tick. The frame is read as the
  atlas holds it; each listed pixel that holds the game's colour is copied over (a 1x1 RGBA32 texture,
  `Graphics.CopyTexture`) at the texel the frame read takes it from (the texture rect's corner rounded, the rect
  offset floored); the next tick reads it back, and a pixel that doesn't read back puts the frame's game colours back.
  One info line when done ("repainted in the game's atlas: N pixels on N of 44 frames, N pages"), a warning naming
  the frames left alone. Off, with a line, when the check didn't read the frames, the GPU can't copy, or a page isn't
  RGBA32 with one mip.
- **Pages again:** every 3 s the repainted frames' sprites are looked at; gone or on another page (the atlas unloaded),
  the frames are looked for again (`Looks.ForgetGameFrames`: the found frames' cache otherwise kept the unloaded
  sprites) and repainted when they are back. The donkey-art check does the same when a frame goes while it reads.
- **The builds:** `Looks.ReadFrame` repaints the donkey's own frames from the list as it reads them
  (`FacePixels.Read.Repainted`: the recipes, the erase compose, a game-source skin's frames of the donkey), so a skin's
  whole frame has the new colours whichever came first. The donkey-art check and the Mounts tab's export read them as
  drawn (`AsDrawn`), the export's whole frames too, so the review page (built on the asset probe's frames) still
  matches the game's export. The asset probe (`MyArt.ExportFrame`) puts the two colours back by colour: its export
  stays the art as drawn, the art the tools and the skins are made on.
- **Offline:** `check_skin.py` reads the art as drawn, so it counts the nostril with Dark; its notes on what a skin
  covers may be one pixel a frame off there.
- **What the player sees:** "Legs and mane" no longer colours the eye's line; the nostril takes the Muzzle colour; at
  Game default the donkey looks as before (a step of blue on the eye's line, a step of red on the nostril).
- **Tests:** "the donkey's eye and nostril pixels repainted in the game's atlas, nothing else changed" (the Looks
  group: the list's reading and its two ways on a made-up frame; the repaint done with no frame left alone; every
  listed pixel the new colour in the atlas; every frame put back as drawn is donkey-frames.txt's fingerprint), the
  same after the run, and the integration check "colours: the donkey's eye and nostril repainted" (the plain donkey
  and Hellfire; Legs and mane, then Muzzle green: 292319 has no pair, 362d1e follows Muzzle, the dark fill follows
  Legs and mane).
- **In game 2026-11-04 22:18 (F11 1869 passed, 0 failed, 0 warnings):** "repainted in the game's atlas: 178 pixel(s)
  on 44 of 44 frame(s), 2 page(s) (92 ms over 89 game frames)"; the atlas holds the new colours at 178 of 178, and all
  44 frames put back as drawn are donkey-frames.txt's, both at the start and after the run (the frames never
  unloaded); the pairs right on the plain donkey and Hellfire. Left to look at by hand: Legs and mane Pink, then
  Muzzle green, on a donkey in the world; and the Mounts tab's export with the review page rebuilt (Hellfire's whole
  frames "0 px differ").
