# The donkey's eye and nostril pixels

The user, on the Jester with "Legs and mane" Pink (a screenshot, 2026-11-04): "there are some pixels around the eye
which are affected by the legs and mange group", then "yes lets investigate this further to make sure we get it right
because it looks sloppy otherwise". This page is the investigation, the proposal and the decisions it needs. Nothing
is built yet but a probe.

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
- **What they follow (decisions):** the eye's as drawn (it is the eye's line, as the outline is the drawing's) or the
  Eyes row; the nostril the Muzzle row (the nose's dark spot, a darker shade of the nose's colour) or as drawn.
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

## 6. The probe (built 2026-11-04, deployed; for the next F11)

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

## 7. Next

The user's F11 with the probe, the contact sheet looked at, the two decisions in section 5; then A built once (or B if
the copy doesn't read back), with a scenario that wears the plain donkey and a skin over it with Legs and mane set
and reads the eye and nostril pixels unchanged.
