# Making a skin pack for GK2 Donkey API

A skin draws the donkey as something else: a giant rat, a snail, a hellfire donkey with horns and a devil's tail. Anyone can make one: a
folder of pictures and one small text file, no code. This page says how. (How the system works inside the mod:
[skins-design.md](skins-design.md).)

## What a pack is

Packs are sorted by mount: a folder per mount, and under it a folder per skin (look) of that mount.

```
BepInEx\plugins\gk2mounts\
  me.giant-rat\                  a mount of your own: its folder is its id
    mount.cfg                    the mount: what the player rides ([Mount], [Stats], [Tuning*], [Sounds*]; see below)
    sounds\takeoff_1.ogg ...     optional: the mount's sounds, for every skin of it (docs/mount-sounds.md)
    portrait.png                 optional: the mount's picture in the Mounts panel ([Mount] portrait =)
    skins\
      me.grey-rat\               one look of the mount: its folder is the skin's id
        skin.cfg
        bdy_static_left.png
        bdy_walk_left_01.png ... bdy_walk_left_08.png
        bdy_ear_left_01.png ... _05.png
        bdy_graze_left_01.png ... _05.png
        bdy_chew_left_01.png ... _03.png
        bdy_lips_left_01.png ... _13.png
        bdy_turn_left_01.png ... _06.png
        hed_static_down.png, hed_static_down_02.png, hed_static_down_03.png
        front\ ...               optional: the parts drawn in front of the rider (see "The pictures")
        sounds\voice_1.ogg ...   optional: this skin's own sounds (docs/mount-sounds.md)
  donkey\                        the game's donkey: no mount.cfg (one there is refused), skins only
    skins\
      me.my-saddle\
        skin.cfg ...
```

- The `gk2mounts` folder can sit straight under `BepInEx\plugins`, or inside any mod's own folder there
  (`BepInEx\plugins\<AnyFolder>\gk2mounts\`). Several mounts can share one `gk2mounts` folder, one subfolder each, and
  a mount several skins, one folder each under its `skins\`.
- **A mount folder without `mount.cfg`** adds its skins to a mount registered elsewhere: another mod's, a mount
  registered in code, or the donkey (`gk2mounts\donkey\skins\<id>\`). So a second look for someone else's dragon is
  `gk2mounts\dragon\skins\<your id>\` and nothing else.
- The skin id is its folder's name (letters, digits, dots, dashes, underscores), the mount id its mount folder's.
  `donkey` is taken: it's the game's own look and mount. **Prefix your ids with your name** (`anna.sea-horse`): skin
  ids are global (two skins of different mounts can't share one), two authors' plain names would meet, and the log
  notes an unprefixed id from another mod once. The same skin id from two places (a folder and a DLL): the higher
  `version` wins, a tie goes to the DLL, and the log names the loser and its folder. The same mount from two places
  (two `mount.cfg` with one id): the higher `[Mount] version` wins and its facts replace the other's in place (the
  other pack's skins keep the mount), a tie goes to the DLL over a loose folder, and otherwise the first stays and the
  later one is refused with its skins, with a warning. A mount registered in code is never replaced by a pack.
- A mount pack whose skins all fail to register is withdrawn: a mount with nothing to wear is not offered.
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
id = me.grey-rat    # the same as the folder name; your name in front, so no other author's id meets yours
name = Grey Rat     # what the settings show
description = A rat the size of a donkey, drawn by me.   # a line or two for the Mounts panel
# portrait = portrait.png   # a PNG next to the frames: the Mounts panel shows it for this skin (2x when 48 px or less, at most 96 px a side)
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
# mount = me.giant-rat  # optional: the mount is the folder the skin sits in (gk2mounts\<mount>\skins\); a line that
                    # names another mount is ignored with a report line (the baby donkeys wear the skin when the
                    # mount has foals and the player keeps that on)
# sheet = sheet.png # one PNG of cells instead of a file per frame (see above); with it, optionally:
# columns = 8       # cells per row (default: the sheet's width over the canvas width)
# frames = ...      # the keys in cell order (default: the list above)
```

Unknown keys are ignored and named in the log, so a newer pack still loads in an older mod.

A `skin.cfg` holds no `[Mount]` or `[Mount.*]` section: the loader ignores one and says in the skin's report that
it goes in `mount.cfg`.

**A creature of its own: `mount.cfg`.** A skin can be a look of a *mount* other than the donkey: the giant rat, the dragon,
yours. The mount carries what is the creature's, not the picture's: whether it pulls the cart, whether baby donkeys
can be it and how big they are, its speed, an effect it comes with, its own tuning and its sounds. It is its own
folder, `gk2mounts\<mount id>\`, with a `mount.cfg` and its skins under `skins\`; every skin in there is a look of it
(a second dragon colour is a second folder under `me.dragon\skins\`). The keys of `[Mount]`, in the order the file keeps
them:

```
[Mount]
id = me.dragon        # the same as the mount folder's name
name = Dragon         # the creature's name: the Mount list, "a dragon can't ..." in the game's sentences
noun = dragon         # the word the game's sentences use ("a dragon can't pull the cart"); default: the name in lower case
description = A dragon of the keeper's own.   # for the Mounts panel
# portrait = portrait.png   # a PNG in the mount folder: the Mounts panel shows it under the mount's name
default_skin = me.red-dragon  # the look a new dragon wears: a skin folder under skins\ (the Skin row's "Default")
author = me
version = 1.0         # the higher version wins when the same mount comes from two places
cart = true           # may it pull the cart
foals = true          # may the baby donkeys be it
flies = true          # ridden plain, it takes off and flies (off unless said; flying is a dev-only experiment for now)
jumps = false         # ridden plain, it never jumps (on unless said); both written only when not the default
foal_scale = 0.6      # the babies' size against the grown mount, 0.2 to 1 (Gerry: 0.33)
speed = 2.5           # riding, times the keeper's walking speed (1 = walking; the donkey: 2), 0.5 to 4
cargo = 6             # items its cart carries, 1 to 6 (a stat for now); only on a mount that pulls one
effect = none         # an effect it comes with (hellfire, or one a mod added); players see it under "As the mount or skin" (a skin's own effect = wins)
effect_when = Always  # Off, Night or Always (the menu says "Never" for Off)

[Stats]               # lines the Mounts panel shows as given, in this order (optional; Speed and Cargo are the API's own lines)
Diet = keepers

[Tuning]              # what the creature is, both styles (a sound key's volume or timing; the speed is speed = above); the rider's seat and
WalkCycleSpeed = 1.5  # the cart's place are the SKIN's rows (its skin.cfg's [Tuning], [Tuning.Ride], [Tuning.Cart])
[Tuning.Ride]         # riding only
[Tuning.Cart]         # with the cart only

[Sounds]              # the mount's sounds, for every skin of it: files of the mount folder's sounds\ (docs/mount-sounds.md)
lips = none           # [Sounds.Ride], [Sounds.Cart] and [Sounds.Baby] as in a skin.cfg
```

The sections of `mount.cfg` have the same plain names as a `skin.cfg`'s own (`[Tuning]`, `[Sounds]`, ...): the
folder the file sits in decides whose they are. The file keeps them in this order: `[Mount]`, `[Stats]`, `[Tuning]`,
`[Tuning.Ride]`, `[Tuning.Cart]`, `[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`. `id`, `name`,
`author`, `version`, `cart` and `foals` are always written.

The settings show a **Mount** list (Donkey, Dragon, Giant Rat, ...) and under it a **Skin** row with that mount's
skins. The mount's `default_skin` is its **Default** (the game's own art for the donkey), listed first by that word,
and the one a new instance of the mount wears; the others by their `name`. So a mount with one skin shows "Default"
only, and a second dragon look (another folder under `dragon\skins\`, from any pack) shows next to it by its name.
Every mount a player can pick has at least one skin. Keys the loader doesn't know in `[Mount]` are ignored and named
in the log; a bad `foal_scale`, `speed`, `cargo` or `effect_when` is named too and the default stands (a speed
outside 0.5 to 4 is taken to the nearest end).

**Speed and cargo** (the user, 2026-10-22): a mount's `speed` is how fast the keeper rides it, times his walking
speed (the game's speed is linear in it). With the cart it is that times the donkey's cart speed over its riding
one (1.5 / 2 as shipped): the cart slows every mount alike. The Mounts panel shows it as a word against the
donkey's: under 0.7 of it Very slow, under 0.9 Slow, to 1.1 Normal, to 1.35 Fast, above that Very fast
(`MountInfo.SpeedWord`; the number is `MountInfo.Speed`). A mount without one rides at the donkey's, the API cfg's
`[Ride]` / `[Cart]` `SpeedMultiplier`. Tested in game up to 2.5 riding. `cargo` is how many items its cart carries,
1 to 6, shown as "Cargo: 6 items"; a stat only for now (what a cart holds is the player's Cart capacity), and every
built-in mount says 6. The built-in speeds: donkey 2, rat 2.75 (above the 2.5 seen in game so far), Gerry 2.25, dragon 1.6, worm 1.75, snail 1.25.

Back in `skin.cfg`: two optional keys move the pictures on the donkey, in art pixels (x towards the head, y up):
`offset = 0 0` while riding and `offset_cart = 0 0` with the cart hitched (a skin that should stand closer to the
shafts). And a `[Tuning]` section moves the rider and the cart to fit these pictures, with the same keys and values
as the mod's cfg (docs/reference.md `[Ride]` and `[Cart]`); `[Tuning.Ride]` and `[Tuning.Cart]` do the same for one
style only, on top of it. **The skin owns its drawing's geometry**: the seat row and these keys live here, on the
skin, and `mount.cfg` keeps only what the creature is. Only the picture-dependent keys are taken: `SeatAdjustPx`,
`SeatOffset`, `RiderOffsetPx`, `TorsoOffsetPx`, `LegAdjustXPx`, `LegAdjustYPx`, `LegHeightPx`, `BackSeatExtraPx`,
`HitchGapPx`, `Depth`, `CenterOffset`. Anything about the creature itself (speed, the cart, foals, the effect,
abilities) belongs in `mount.cfg` and is refused here, said once in the log; a built-in with a picture key in its
`mount.cfg`'s `[Tuning*]` fails the scan.

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
Nothing of it is saved. The Mounts tab (F11) edits these rows live and writes them back into this file (and the
mount's rows into its `mount.cfg`).

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
| **Hellfire** | a skin drawn **over** the donkey that also takes things off it: the erase colour under horns and a devil's tail, `[Base.Coat]` for the ash coat, `[Colors]` so the cracks and the glow follow the player's muzzle and eye colours | `src/SaddleUp/assets/mounts/donkey/skins/hellfire/` (generated by `tools/skins/hellfire/make_hellfire.py`) |
| **Royal Parade** | a plain **over** pack: a saddle cloth and a crown, the cloth's reds named `Harness` so the player's harness colour recolours them, the gold kept as drawn | `src/SaddleUp/assets/mounts/donkey/skins/royal-parade/` (generated by `tools/skins/royal-parade/make_royal.py`) |
| **Giant Rat, Giant Snail, Giant Worm, Dragon** | a **mount** of its own (`mount.cfg`: its facts, speed, sounds) with a skin that **replaces** the donkey (`grey-rat`, `garden-snail`, `earth-worm`, `red-dragon`: the pictures, the rider's seat, the colours); the dragon's `front\` layer drawn above the rider and its flight sounds in the mount's `sounds\` | `src/SaddleUp/assets/mounts/<mount>/` with `mount.cfg` and `skins/<skin>/` (drawn by `tools/skins/creatures/make_creatures.py`): Saddle Up's packs, embedded in its DLL and registered with the API at start, the worked example of shipping mounts in a mod (docs/mounts-as-mods.md) |
| **Gerry** | a mount out of the game's own character frames (`source = game`), no pictures of its own; `noun = skull`, babies 0.33, `seat = picture` | `src/SaddleUp/assets/mounts/gerry/mount.cfg` and `skins/gerry/skin.cfg` |
| **The donkey** | the one mount that is not a file (the game's own art, no `SkinDef`, no `mount.cfg`): what every skin under `donkey\skins\` is a look of | none: its facts are in the code (`Catalog.Donkey`) |
| **Sepia, Checker** | the same from code: a recipe and a picture source registered through the API | `examples/GK2DonkeySkins/` |

Each `skin.cfg` and `mount.cfg` is commented and in one layout (`tools/skins/canonical_cfg.py` rewrites a built-in
into it; its `--check` in the release scans refuses one that isn't, and `tests/SkinsScan` checks the section order and
the key rules). A `skin.cfg`: `[Skin]`, then `[Game]` for a game creature, the skin's own geometry (`[Tuning]`,
`[Tuning.Ride]`, `[Tuning.Cart]`: the seat and the pixel keys this drawing needs), the skin's sounds (`[Sounds]`,
`[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`), then the colour sections. A `mount.cfg`: `[Mount]`, `[Stats]`,
`[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]`, `[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]` (the
creature's facts), every key in one place. The donkey itself, the one mount that is not a file, would read
`[Mount] id = donkey, name = Donkey, author = the game, version = 1.0, cart = true, foals = true` with no tuning and
no effect; its folder holds skins only. A mount's sounds live in its `mount.cfg`'s `[Sounds]`, and the donkey's own
sounds (the bray, the lip smack, the chewing, the snorts getting on and off) never reach another mount: a creature
with no line for one is silent there, and the log at the game's start says what each skin is missing
(docs/mount-sounds.md). A mount or a skin brings **sounds of its own**: files in the skin folder's `sounds\` (the
skin's) or the mount folder's `sounds\` (the mount's, for every skin of it), named for their event (`voice_1.ogg`,
`voice_2.ogg`, `step.grass_1.wav`: played by convention with no line) or lined up in a `[Sounds]` block
(`voice = a.ogg, b.ogg`, `voice.volume = 0.8`, `step.grass = step_grass`, `lips = none`; `[Sounds.Ride]` /
`[Sounds.Cart]` for one style), the skin's in its `skin.cfg`, the creature's in its `mount.cfg`. A skin's line wins
over its mount's, and the player's switches and volumes scale the result (the author's page: docs/mount-sounds.md;
the design in docs/mount-sounds-design.md). The generators show how the pictures were measured
on the donkey's frames (docs/sprite-tools.md). The review page (`tools/skins/skin_preview_html.py`) plays them all
side by side.

## A skin out of the game's own characters (`source = game`)

A skin with no pictures of its own: another character's frames, read out of the game's character atlas at run
time (as the recipes read the donkey's), mapped onto the donkey's frame keys and scaled by whole pixels onto the
canvas. Gerry (the talking skull, preset `0901_`) is built in this way (docs/gerry-mount.md). Nothing of the game's
art is in the pack (its `skin.cfg` in `gk2mounts\gerry\skins\gerry\`, next to the mount's `mount.cfg` two folders up).

```
[Skin]
id = gerry
name = Gerry
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

Zip the `gk2mounts\<mount>\` folder (with `BepInEx\plugins\` above it, so it extracts into place): a mount with its
`mount.cfg`, its `sounds\` and its skins, or, for a new look of a mount that exists already, only
`gk2mounts\<mount>\skins\<your skin id>\` (no `mount.cfg`; `gk2mounts\donkey\skins\<id>\` for the donkey). Only your
own art, or art you may redistribute, goes in. A pack can also travel inside a mod's DLL with a version and a declared
dependency on the API: Saddle Up's own five mounts travel that way (docs/mounts-as-mods.md 3).
