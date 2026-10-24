# Making a skin pack for GK2 Donkey API

A skin draws the donkey as something else: a giant rat, a snail, a hellfire donkey with horns and a devil's tail. Anyone can make one: a
folder of pictures and one small text file, no code. This page says how. (How the system works inside the mod:
[skins-design.md](skins-design.md).)

## What a pack is

```
BepInEx\plugins\gk2skins\<your skin id>\
    skin.cfg
    bdy_static_left.png
    bdy_walk_left_01.png ... bdy_walk_left_08.png
    bdy_ear_left_01.png ... _05.png
    bdy_graze_left_01.png ... _05.png
    bdy_chew_left_01.png ... _03.png
    bdy_lips_left_01.png ... _13.png
    bdy_turn_left_01.png ... _06.png
    hed_static_down.png, hed_static_down_02.png, hed_static_down_03.png
    sounds\voice_1.ogg, sounds\voice_2.ogg ...   (optional: the skin's own sounds, docs/mount-sounds-design.md)
```

- The `gk2skins` folder can sit straight under `BepInEx\plugins`, or inside any mod's own folder there. Several skins
  can share one `gk2skins` folder, one subfolder each.
- The skin id is the folder's name (letters, digits, dots, dashes, underscores). `donkey` is taken: it's the game's
  own look. **Prefix it with your name** (`anna.sea-horse`): ids are global, two authors' plain names would meet,
  and the log notes an unprefixed id from another mod once. The same id from two places (a folder and a DLL): the
  higher `version` wins, a tie goes to the DLL, and the log names the loser and its folder.
- The pack is found when the game starts. Its pictures are read only when a donkey wears the skin, a few at a time,
  and put together on one sheet in memory; the skin shows once they are all in.
- **One sheet instead of 44 files** works too: put `sheet = sheet.png` in `skin.cfg` and draw every frame as a cell of
  the canvas size in one PNG, left to right then top to bottom, in the order of the list above (`bdy_static_left`
  first, the three heads last). Fewer cells than frames is fine (the rest show the donkey); `columns = 8` says how
  many cells a row holds (by default the sheet's width over the canvas width), and `frames = bdy_static_left,
  bdy_walk_left_01, ...` gives your own order or subset.

## skin.cfg

```
[Skin]
format = 1          # keep 1
# needs = 0.36      # the GK2 Donkey API version the pack was written for: an older API refuses it with the reason
id = me.my-rat      # the same as the folder name; your name in front, so no other author's id meets yours
name = My Rat       # what the settings show
description = A rat the size of a donkey, drawn by me.   # a line or two for the Mounts panel
# portrait = portrait.png   # a PNG next to the frames: the Mounts panel shows it under the mount's name (2x when 48 px or less, at most 96 px a side)
author = me
version = 1.0       # the higher version wins when the same id comes from two places
colours = own          # own: your colours as drawn; the player's coat and harness colours recolour them by region
                    #      ([Colors] below). palette: drawn in the donkey's exact colours, which take the donkey's regions
layer = replace     # replace: instead of the donkey. over: on top of the donkey (a saddle, a hat)
# erase = FF00FF    # over only: pixels of this colour make the donkey transparent there (the ears gone under
                    # horns); FF00FF unless set, "none" switches it off (see "A skin worn over the donkey")
canvas = 96 96      # every picture is this size (see below)
# pivot = 48 48     # where the base's pivot is on the canvas (default: its middle; a bigger canvas keeps the donkey centred)
# effect = hellfire # the effect this skin comes with: shown when the player's Effect row says "As the mount or skin"
# effect_when = Always  # (its default), the skin's first, else its mount's. Off, Night or Always. Hellfire burns by itself.
# seat = 52         # the canvas row (from the top) the rider's hip sits on, every frame, for EVERY player (their AutoSeat
                    # and SeatAdjustPx stop mattering on this skin; only the skin's own [Tuning] SeatAdjustPx nudges from
                    # it); 52 is where the donkey's own seat is on the 96 canvas. Or
                    # "picture" for this frame's picture top (Gerry). Without it the seat is the player's own (docs/seat-investigation.md)
base = donkey       # what the skin is drawn on (the donkey is the only base today)
mount = donkey      # the mount this skin is a look of: donkey, or a mount declared below or by another pack or mod
                    # (the baby donkeys wear the skin when the mount has foals and the player keeps that on)
# sheet = sheet.png # one PNG of cells instead of a file per frame (see above); with it, optionally:
# columns = 8       # cells per row (default: the sheet's width over the canvas width)
# frames = ...      # the keys in cell order (default: the list above)
```

Unknown keys are ignored and named in the log, so a newer pack still loads in an older mod.

**A creature of its own.** A skin can be a look of a *mount* other than the donkey: the giant rat, the dragon,
yours. The mount carries what is the creature's, not the picture's: the rider's seat for it (both styles, or riding
and with the cart apart), whether it pulls the cart, whether baby donkeys can be it, an effect it comes with. Declare
it in the same file; the mount's own skin (the one with the mount's id) owns the declaration, other skins with
`mount = <id>` join it (a second dragon colour, say), and a joiner's own `[Mount]` block holds only until the
mount's own skin is registered:

```
mount = dragon

[Mount]
id = dragon
name = Dragon         # the creature's name: the Mount list, "a dragon can't ..." in the game's sentences
description = A dragon of the keeper's own.   # for the Mounts panel; portrait = <png> shows under its name there
# a skin with a [Mount] block is that mount's own skin: its mount = line is the block's id (anything else is corrected with a log line)
author = me
version = 1.0
cart = true           # may it pull the cart
foals = true          # may the baby donkeys be it
flies = true          # ridden plain, it takes off and flies (off unless said; flying is a dev-only experiment for now)
jumps = false         # ridden plain, it never jumps (on unless said); both written only when not the default
foal_scale = 0.6      # the babies' size against the grown mount, 0.2 to 1 (Gerry: 0.33); it lives on the mount, not the skin
speed = 2.5           # riding, times the keeper's walking speed (1 = walking; the donkey: 2), 0.5 to 4
cargo = 6             # items its cart carries, 1 to 6 (a stat for now); only on a mount that pulls one
effect = none         # an effect it comes with (hellfire, or one a mod added); players see it under "As the mount or skin" (a skin's own effect = wins)
effect_when = Always  # Off, Night or Always (the menu says "Never" for Off)

noun = dragon         # the word the game's sentences use ("a dragon can't pull the cart"); default: the name in lower case

[Mount.Stats]         # lines the Mounts panel shows as given, in this order (optional; Speed and Cargo are the API's own lines)
Diet = keepers

[Mount.Tuning]        # what the creature is, both styles (a sound key's volume or timing; the speed is speed = above); the rider's seat and
WalkCycleSpeed = 1.5  # the cart's place are the SKIN's rows ([Tuning], [Tuning.Ride], [Tuning.Cart] above)
[Mount.Tuning.Ride]   # riding only
[Mount.Tuning.Cart]   # with the cart only
```

The settings show a **Mount** list (Donkey, Dragon, Giant Rat, ...) and under it a **Skin** row with that mount's
skins. The skin whose id is the mount's id is its **Default** (the game's own art for the donkey), listed first by
that word; the others by their `name`. So a mount with one skin shows "Default" only, and a second dragon pack
(`mount = dragon`, no `[Mount]` of its own) shows next to it by its name. Every mount a player can pick exists only
through its skins. Keys the loader doesn't know in `[Mount]` are ignored and named in the log; a bad `foal_scale`,
`speed`, `cargo` or `effect_when` is named too and the default stands (a speed outside 0.5 to 4 is taken to the
nearest end).

**Speed and cargo** (the user, 2026-10-22): a mount's `speed` is how fast the keeper rides it, times his walking
speed (the game's speed is linear in it). With the cart it is that times the donkey's cart speed over its riding
one (1.5 / 2 as shipped): the cart slows every mount alike. The Mounts panel shows it as a word against the
donkey's: under 0.7 of it Very slow, under 0.9 Slow, to 1.1 Normal, to 1.35 Fast, above that Very fast
(`MountInfo.SpeedWord`; the number is `MountInfo.Speed`). A mount without one rides at the donkey's, the API cfg's
`[Ride]` / `[Cart]` `SpeedMultiplier`. Tested in game up to 2.5 riding. `cargo` is how many items its cart carries,
1 to 6, shown as "Cargo: 6 items"; a stat only for now (what a cart holds is the player's Cart capacity), and every
built-in mount says 6. The built-in speeds: donkey 2, rat 2.75 (above the 2.5 seen in game so far), Gerry 2.25, dragon 1.6, worm 1.75, snail 1.25.

Two optional keys move the pictures on the donkey, in art pixels (x towards the head, y up): `offset = 0 0` while
riding and `offset_cart = 0 0` with the cart hitched (a skin that should stand closer to the shafts). And a
`[Tuning]` section moves the rider and the cart to fit these pictures, with the same keys and values as the mod's
cfg (docs/reference.md `[Ride]` and `[Cart]`); `[Tuning.Ride]` and `[Tuning.Cart]` do the same for one style only,
on top of it. **The skin owns its drawing's geometry**: the seat row and these keys live here, on the skin, and the
`[Mount]` block below keeps only what the creature is. Only the picture-dependent keys are taken: `SeatAdjustPx`,
`SeatOffset`, `RiderOffsetPx`, `TorsoOffsetPx`, `LegAdjustXPx`, `LegAdjustYPx`, `LegHeightPx`, `BackSeatExtraPx`,
`HitchGapPx`, `Depth`, `CenterOffset`. Anything about the creature itself (speed, the cart, foals, the effect,
abilities, its sounds) belongs in the `[Mount]` sections and is refused here, said once in the log; a built-in with
a picture key under `[Mount.Tuning]` fails the scan.

```
[Tuning]
SeatAdjustPx = 4      # the rider four pixels higher on these pictures, riding and with the cart
[Tuning.Ride]
RiderOffsetPx = -2    # riding only: two towards the rear
[Tuning.Cart]
HitchGapPx = 9        # with the cart only: the whole cart nine pixels further behind the longer body
```

They apply to the donkey wearing the skin only and come off with the skin; a key in a style section wins over the
same key in `[Tuning]` for that style; a value another mod has set on that donkey wins, and the log says so.
Nothing of it is saved. The Mounts tab (F11) edits these rows live and writes them back into this file.

**Colours.** The player's colour settings mean the same on every mount (docs/mount-colours.md): Coat is the body,
Harness the tack, and Dark, Muzzle and Eye the outlines, a second body tone and the eye. A skin drawn in its own
colours is all Coat until its `[Colors]` section says which of its colours are something else, by hex (the
colours themselves are read from the pictures; the section only sorts them):

```
[Colors]
Dark = 1C060C 2E0C14 280810   # outlines, horns, spikes
Muzzle = A86A28 703E1A DAA042 # a second body tone (the snail's shell, the rat's pink)
Eye = FAD640 F4CE76
```

Coat is never listed (it is everything not named), a colour goes under one region only, and a colour no picture
has is said once in the log and maps nothing. A skin with up to 48 colours is recoloured by the game's exact
colour swap; one with more (shaded art) goes through the game's colour cube, which takes any number. A skin with
`colours = palette` keeps the donkey's regions, since it is drawn in the donkey's colours.

**A skin worn over the donkey** (`layer = over`: a saddle cloth, a hat) is not the donkey's body, so its colours
work the other way round: the donkey under it keeps all its own regions (a player's coat and harness colours still
reach the donkey), the colours `[Colors]` names join those regions (a caparison's reds under `Harness` follow the
player's harness colour), and every colour not named is **kept as drawn** (the gold trim stays gold). Draw it in
colours the donkey doesn't have: a named colour the donkey also has follows the donkey's region, and the log says
so once.

**Erasing from the donkey.** An over skin can take things off the donkey as well as add to it: every pixel of the
**erase colour** (`erase = FF00FF`, magenta, unless the cfg says another colour or `none`) makes the donkey
transparent there. Draw magenta over the ears and horns next to them, and the ears are gone; over the tail and a
devil's tail beside it, the same. A picture with erase pixels is built whole when the skin is first worn: the
donkey's own frame read from the game, those pixels cleared, the picture's other pixels on top, and that whole
picture is drawn in the donkey's place for that frame (the player's coat and harness colours still reach it, it is
the donkey's own pixels). A picture without erase pixels stays a plain overlay. The erase pixels are never drawn;
nothing of the game's art is in the pack. The log's "loaded" line counts the frames built whole and the pixels
erased; `tools/skins/check_skin.py` lists them too.

**Recolouring the donkey under the skin.** An over skin can also change the donkey's own colours under it, region
by region, with the same knobs the player has: a section `[Base.Coat]` (or `[Base.Dark]`, `[Base.Muzzle]`,
`[Base.Eye]`, `[Base.Harness]`) with `Hue` (degrees), `Saturation`, `Brightness`, `Contrast` (multipliers),
`Colorize` (hex) with `ColorizeAmount`, `Tint` (hex), `Overlay` (hex) with `OverlayAmount`. The skin's recolour is
applied first and the player's settings on top of it, so an ash coat under horns is the skin's, and the player's
custom coat still changes it. It holds even with the player's colour settings switched off, since it is the skin's
own look. Nothing of the game's art is in the pack for it.

```ini
[Base.Coat]
Brightness = 0.42
Saturation = 1.2
Colorize = CC1800
ColorizeAmount = 0.6
```

## Worked examples in this repo

Every built-in skin is a pack of exactly this format, kept as the example of one way to make one (the user,
2026-10-19: they are to be examples for mod authors who make their own mounts and skins):

| Example | What it shows | Where |
|---|---|---|
| **Hellfire** | a skin drawn **over** the donkey that also takes things off it: the erase colour under horns and a devil's tail, `[Base.Coat]` for the ash coat, `[Colors]` so the cracks and the glow follow the player's muzzle and eye colours | `src/GK2DonkeyAPI/assets/skins/hellfire/` (generated by `tools/skins/hellfire/make_hellfire.py`) |
| **Royal Parade** | a plain **over** pack: a saddle cloth and a crown, the cloth's reds named `Harness` so the player's harness colour recolours them, the gold kept as drawn | `src/GK2DonkeyAPI/assets/skins/royal-parade/` (generated by `tools/skins/royal-parade/make_royal.py`) |
| **Giant Rat, Giant Snail, Giant Worm, Red Dragon** | a skin that **replaces** the donkey and declares its own **mount** (`[Mount]`, its tuning, the rider's seat); the dragon's `front\` layer drawn above the rider | `src/SaddleUp/assets/skins/<id>/` (drawn by `tools/skins/creatures/make_creatures.py`): Saddle Up's packs, embedded in its DLL and registered with the API at start, the worked example of shipping mounts in a mod (docs/mounts-as-mods.md) |
| **Gerry** | a mount out of the game's own character frames (`source = game`), no pictures of its own; `noun = skull`, babies 0.33, `seat = picture` | `src/SaddleUp/assets/skins/gerry/skin.cfg` |
| **The donkey** | the one mount that is not a file (the game's own art, no `SkinDef`): what every donkey skin's `mount = donkey` names | none: its facts are in the code (`Catalog.Donkey`) |
| **Sepia, Checker** | the same from code: a recipe and a picture source registered through the API | `examples/GK2DonkeySkins/` |

Each `skin.cfg` is commented and in one layout (`tools/skins/canonical_cfg.py` rewrites a built-in into it; its
`--check` in the release scans refuses one that isn't, and `tests/SkinsScan` checks the section order and the key rules): `[Skin]`, then `[Game]` for a game creature, the skin's own geometry
(`[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]`: the seat and the pixel keys this drawing needs), the colour sections,
the skin's sounds (`[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`) and the `[Mount]` block last with its tuning and
sound sections (the creature's facts), every key in one place. The donkey itself, the one mount that is not a file, would read `[Mount] id = donkey, name = Donkey, author =
the game, version = 1.0, cart = true, foals = true` with no tuning and no effect; a donkey skin never declares it. A
mount's sounds live in `[Mount.Sounds]`, and the donkey's own sounds (the bray, the lip smack, the chewing, the
snorts getting on and off) never reach another mount: a creature with no line for one is silent there, and the log
at the game's start says what each skin is missing (docs/mount-sounds.md). A mount or a skin brings **sounds of its
own**: files in the pack's `sounds\` folder, named for their event
(`voice_1.ogg`, `voice_2.ogg`, `step.grass_1.wav`: played by convention with no line) or lined up in a `[Sounds]`
block (`voice = a.ogg, b.ogg`, `voice.volume = 0.8`, `step.grass = step_grass`, `lips = none`; `[Sounds.Ride]` /
`[Sounds.Cart]` for one style) and, for the creature, in `[Mount.Sounds]` of its declaring skin. A skin's line wins
over its mount's, and the player's switches and volumes scale the result (the author's page: docs/mount-sounds.md; the design in
docs/mount-sounds-design.md). The generators show how the pictures were measured
on the donkey's frames (docs/sprite-tools.md). The review page (`tools/skins/skin_preview_html.py`) plays them all
side by side.

## A skin out of the game's own characters (`source = game`)

A skin with no pictures of its own: another character's frames, read out of the game's character atlas at run
time (as the recipes read the donkey's), mapped onto the donkey's frame keys and scaled by whole pixels onto the
canvas. Gerry (the talking skull, preset `0901_`) is built in this way (docs/gerry-mount.md). Nothing of the game's
art is in the pack.

```
[Skin]
id = gerry
name = Gerry
mount = gerry
canvas = 160 160      # room for the scaled frames (a 29-pixel hop at 3x is 87 above the ground row)
source = game
seat = picture        # the rider sits on this skin's own picture (its top at the pivot column, per frame), whatever
                      # the player's seat settings; "seat = <row>" is the same with a fixed row of the canvas (the four
                      # drawn creatures say 52, 55 or 68); without it the seat is the player's (AutoSeat off: a fixed height)

[Game]
prefix = 0901_        # the character's preset prefix on its sprites (the donkey's is 0301_)
scale = 3             # a whole number, 1 to 8: each of its pixels becomes a 3x3 block
lift = 0.25           # 0 to 1: how much of the creature's own jump is kept (a mount under a rider hops a little)
bdy_static_left = bdy_walk_left_01       # donkey key = the character's frame (without the prefix)
bdy_turn_left_01 = bdy_somersault_left_01
bdy_lips_left_01 = hed_speak_left_01
bdy_graze_left_02 = none                 # none: this key stays empty on purpose
```

The creature's feet (the lowest pixel of its rest picture) go on the donkey's ground row and its centre on the
pivot column. A key the `[Game]` section doesn't name follows these rules: the same frame name when the character
has it; a frame of the same clip spread over the character's count (the donkey's `walk 1..8` over a 15-frame walk);
a `hed_*` key stays empty (the character's body frame is the whole creature, so nothing of the donkey's head may
show); every other key shows the rest picture (`bdy_static_left`, else `bdy_walk_left_01`, else `hed_static_left`).
The log says how many of the character's frames were found and where (sprites already in memory, or a sprite atlas
asked by name). Another game character: its prefix, and look at its frames first (`tools/skins/gerry/gerry_probe.py` lists any
prefix).

## The pictures

- **One PNG per donkey frame**, named exactly as above. There are 44: the donkey standing (1), walking (8), its ear
  (5), grazing (5), chewing (3), lips (13), turning its head (6), and the standing head (3).
- **96 by 96 pixels**, facing **left**, transparent background. The donkey stands on row 64 from the top (row 32 from
  the bottom) with its back at row 41 from the top: put your animal's feet and back there and the rider sits right
  without any tuning. The mod draws at 50 pixels per unit and stretches upright pictures 1.667 times in height, as it
  does the game's own art, so draw as the game's frames are drawn.
- **A bigger canvas is fine** (wings above the head, say): set `canvas = 128 128`; the picture's centre stays the
  donkey's centre. The most is 256: a sheet is 44 cells of the canvas (88 with a front layer) in memory per worn
  skin, 12 MB at 256.
- **The head.** The game draws the head *inside* the body frames (the walk, graze and so on all carry the head);
  `hed_static_down*.png` is only the standing head, drawn over the standing body. So draw the head in every body
  frame, and give the standing head picture nothing but the head. A `hed_walk_*.png` is never shown.
- **The right-facing side is the mirror** of your left-facing pictures; the mod mirrors them as the game does.
- **You don't have to draw everything.** A frame you leave out shows the donkey's own picture there, and the log
  lists what is still to draw. Two shortcuts: `bdy_walk_left.png` is used for every walk frame you didn't draw,
  `bdy.png` for every body frame you didn't draw (and `hed.png` for the head).
- **In front of the rider.** Anything that should be drawn over the keeper (the near wing, the near side of a
  saddle) goes in a `front\` subfolder with the same file names; those pictures are drawn in front of the rider
  (above the creature when nobody rides). The same shortcuts apply (`front\bdy.png` for every body frame); a sheet
  pack gives `sheet_front = front.png` with the same cells. What to put in it, how the game places it and the
  recipe that came out of the dragon's wing: docs/front-layer.md.
- **Templates to paint over:** the dev tools export every donkey frame at full size (the asset probe's "Export every
  frame", `BepInEx\GK2-asset-probe\donkey-frames\`). The game's pictures belong to the game: paint your own over them,
  don't ship them.

## Trying it

**In Saddle Up 0.10.0's download the skin packs are switched off**, with every mount and skin: they come in the next
update, and the log says so at start ("Mounts, skins and effects: off in this version"). On a machine with the
sources, the dev key `Debug.MountsPreview` switches them on; the rest of this section is for that state. A mount
from a mod loaded in any order is in Saddle Up's lists (API 0.34.0: the page is shown the frame after Publish).

Start the game and look at `BepInEx\LogOutput.log` for your skin's line: it says how many pictures it found, any
name that matches no frame (a typo), and how many frames are still to draw. Then pick it in Saddle Up's settings
(the Mount section's Mounts button opens the Mounts panel: pick the mount on the left, then its Skin row under the picture) and call your donkey. The pictures are read again every time the game starts, so you can edit them
between runs.

## Sharing it

The one-page guide to shipping a mount or skin from a mod of your own, both as a pack in a DLL and as code with
abilities: [making-a-mount.md](making-a-mount.md).

Zip the `gk2skins\<id>\` folder (with `BepInEx\plugins\` above it, so it extracts into place). Only your own art, or
art you may redistribute, goes in. A pack can also travel inside a mod's DLL with a version and a declared
dependency on the API: Saddle Up's own five mounts travel that way (docs/mounts-as-mods.md 3).
