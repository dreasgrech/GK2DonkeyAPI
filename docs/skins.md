# Making a skin pack for GK2 Donkey API

A skin draws the donkey as something else: a giant rat, a snail, a hellfire donkey with horns and a devil's tail. Anyone can make one: a
folder of pictures and one small text file, no code. This page says how. (How the system works inside the mod:
[skins-design.md](skins-design.md).)

## What a pack is

Packs are sorted by mount: a folder per mount, and under it a folder per skin (look) of that mount.

```
BepInEx\plugins\gk2mounts\
  me.giant-rat\                  a mount of your own: its folder is its id, "<mod>.<name>"
    mount.cfg                    the mount: what the player rides ([Mount], [Stats], [Tuning*], [Sounds*]; see below)
    sounds\takeoff_1.ogg ...     optional: the mount's sounds, for every skin of it (docs/mount-sounds.md)
    portrait.png                 optional: the mount's picture in the Mounts panel ([Mount] portrait =)
    skins\
      default\                  the mount's own look (required): what a new giant rat wears; me.giant-rat/default
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
      me.my-saddle\              a look of the donkey: its full name is donkey/me.my-saddle
        skin.cfg ...
```

- The `gk2mounts` folder can sit straight under `BepInEx\plugins`, or inside any mod's own folder there
  (`BepInEx\plugins\<AnyFolder>\gk2mounts\`). Several mounts can share one `gk2mounts` folder, one subfolder each, and
  a mount several skins, one folder each under its `skins\`.
- **A mount folder without `mount.cfg`** adds its skins to a mount registered elsewhere: another mod's, a mount
  registered in code, or the donkey (`gk2mounts\donkey\skins\<mod>.<skin>\`). So a second look for Saddle Up's dragon
  is `gk2mounts\saddleup-dragon.dragon\skins\<mod>.<your skin>\` and nothing else (its name carries your prefix: below). Such a folder may not bring a `default` (that is
  the mount's own look, and it comes with the `mount.cfg`): one is left out with a warning.
- **A mount's id is `<mod>.<name>`**: your mod's prefix, a dot, the mount's name (`anna.sea-horse`; Saddle Up's
  dragon is `saddleup-dragon.dragon`). A mount id without a dot is refused. Only the game's `donkey` is plain. So two mods'
  dragons are two mounts, and a saved dragon never turns into another mod's (docs/mount-id-collisions.md). The
  mount folder's name is the mount's id.
- **A skin is named within its mount.** Its folder's name is its name (letters, digits, dots, dashes, underscores),
  and its full name is `<mount id>/<skin>`: `saddleup-dragon.dragon/default`, `donkey/saddleup.hellfire`. Two mounts may each
  have a skin called `fire`.
- **A skin added to a mount another mod owns is named `<mod>.<name>`**: your mod's prefix, a dot, the skin's name
  (`saddleup-dragon.dragon\skins\anna.frost\` is `saddleup-dragon.dragon/anna.frost`), so two authors' additions never meet and a
  saved one never turns into another mod's. That is every skin in a mount folder without `mount.cfg`, every skin of
  the donkey (the donkey is the game's: Saddle Up's are `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`),
  and a code skin or recipe on a mount another mod registered. A skin without the prefix there is left out with a
  warning (a pack) or refused with an `ArgumentException` (code). On your own mount (a folder with its `mount.cfg`, or
  a mount your mod registered in code) any name will do: `saddleup-dragon.dragon/default`, `acme.crab/fire`.
  The same skin from two places (a folder and a DLL): the higher `version` wins, a tie goes to the DLL, and the log names the loser and its folder. The same mount from two places
  (two `mount.cfg` with one id): the higher `[Mount] version` wins and its facts replace the other's in place (the
  other pack's skins keep the mount), a tie goes to the DLL over a loose folder, and otherwise the first stays and the
  later one is refused with its skins, with a warning. A mount registered in code is never replaced by a pack.
- **Every mount has exactly one default look: its `skins\default\` folder.** It is the look a new one wears and the
  Skin row's "Default". A pack `mount.cfg` without `skins\default\skin.cfg` is refused with its skins (one warning),
  and a mount whose `default` fails to register is withdrawn with its skins. The donkey has no `default` folder: its
  own look is the game's art, and its skins have names of their own with their mod's prefix (`donkey/saddleup.hellfire`).
- Today a skin is a complete set of pictures of its own: a mount's other skins are not layered on its default's
  pictures (only a `layer = over` skin draws over the game's donkey). Layering skins on a mount's default is not
  built.
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
name = Grey Rat     # what the settings show (the skin's own name is its folder's: no id = line; one is ignored)
description = A rat the size of a donkey, drawn by me.   # a line or two for the Mounts panel
# portrait = portrait.png   # a PNG next to the frames: the Mounts panel shows it for this skin (2x when 48 px or less, at most 96 px a side)
author = me
version = 1.0       # the higher version wins when the same skin comes from two places
colours = own          # own: your colours as drawn; the player's coat and harness colours recolour them by region
                    #      ([Colors] below). palette: drawn in the donkey's exact colours, which take the donkey's regions
layer = replace     # replace: instead of the donkey. over: on top of the donkey (a saddle, a hat)
# erase = FF00FF    # over only: pixels of this colour make the donkey transparent there (the ears gone under
                    # horns); FF00FF unless set, "none" switches it off (see "A skin worn over the donkey")
canvas = 96 96      # every picture is this size (see below)
# pivot = 48 48     # where the base's pivot is on the canvas (default: its middle; a bigger canvas keeps the donkey centred)
# seat = 52         # the canvas row (from the top) the rider's hip sits on, every frame, for EVERY player (their AutoSeat
                    # and SeatAdjustPx stop mattering on this skin; only the skin's own [Tuning] SeatAdjustPx nudges from
                    # it); 52 is where the donkey's own seat is on the 96 canvas. Or
                    # "picture" for this frame's picture top (Gerry). Without it the seat is the player's own (docs/seat-investigation.md)
                    # A skin built on its mount's default (see "Variants") takes the default's seat, portrait,
                    # offset, canvas and description when it leaves them out; seat = none, portrait = none
                    # say "not the default's"
base = donkey       # what the skin is drawn on (the donkey is the only base today)
# mount = me.giant-rat  # optional: the mount is the folder the skin sits in (gk2mounts\<mount>\skins\); a line that
                    # names another mount is ignored with a report line (the baby donkeys wear the skin when the
                    # mount has foals and the player keeps that on)
# sheet = sheet.png # one PNG of cells instead of a file per frame (see above); with it, optionally:
# columns = 8       # cells per row (default: the sheet's width over the canvas width)
# frames = ...      # the keys in cell order (default: the list above)
```

Unknown keys are ignored and named in the log, so a newer pack still loads in an older mod.

Since API 0.37.0 (2026-10-25) a skin names no effect: `effect` and `effect_when` are no longer read, in `skin.cfg`
nor in `mount.cfg`, and an old file's lines are named in the log as unknown keys. Effects are registered in the API
(docs/api.md "Effects"); how a skin will use one is still to be designed (docs/open-items.md).

A `skin.cfg` holds no `[Mount]` or `[Mount.*]` section: the loader ignores one and says in the skin's report that
it goes in `mount.cfg`.

**A creature of its own: `mount.cfg`.** A skin can be a look of a *mount* other than the donkey: the giant rat, the dragon,
yours. The mount carries what is the creature's, not the picture's: whether it pulls the cart, whether baby donkeys
can be it and how big they are, its speed, its own tuning and its sounds. It is its own
folder, `gk2mounts\<mount id>\`, with a `mount.cfg` and its skins under `skins\`; every skin in there is a look of it
(a second dragon colour is a second folder under `me.dragon\skins\`, beside its `default\`). The keys of `[Mount]`,
in the order the file keeps them:

```
[Mount]
id = me.dragon        # the same as the mount folder's name: "<mod>.<name>", a dot required
name = Dragon         # the creature's name: the Mount list, "a dragon can't ..." in the game's sentences
noun = dragon         # the word the game's sentences use ("a dragon can't pull the cart"); default: the name in lower case
description = A dragon of the keeper's own.   # for the Mounts panel
# portrait = portrait.png   # a PNG in the mount folder: the Mounts panel shows it under the mount's name
author = me
version = 1.0         # the higher version wins when the same mount comes from two places
cart = true           # may it pull the cart
foals = true          # may the baby donkeys be it
flies = true          # ridden plain, it takes off and flies (off unless said; flying is a dev-only experiment for now)
jumps = false         # ridden plain, it never jumps (on unless said); both written only when not the default
foal_scale = 0.6      # the babies' size against the grown mount, 0.2 to 1 (Gerry: 0.33)
speed = 2.5           # riding, times the keeper's walking speed (1 = walking; the donkey: 2), 0.5 to 4
cargo = 6             # items its cart carries, 1 to 6 (a stat for now); only on a mount that pulls one

[Stats]               # lines the Mounts panel shows as given, in this order (optional; Speed and Cargo are the API's own lines)
Diet = keepers

[Tuning]              # what the creature is, both styles (a sound key's volume or timing; the speed is speed = above); the rider's seat and
WalkCycleSpeed = 1.5  # the cart's place are the SKIN's rows (its skin.cfg's [Tuning], [Tuning.Ride], [Tuning.Cart])
[Tuning.Ride]         # riding only
[Tuning.Cart]         # with the cart only

[Jump]                # its own jump, ridden plain (optional; a key left out is the default jump's, the donkey's)
height = 40           # the top, the key held, px (1 to 120; the donkey: 28); tap_height: the key let go at once (16)
time = 0.8            # seconds in the air, held (0.15 to 2; 0.45); rise_share (0.55), hang (0.04), hold_min (0.1),
                      # crouch (2 px), crouch_time (0.05), squash (2 px), fall_ease (0: a stone's fall; 1: braked),
                      # wings (false; true: its flying frames as a small take-off and landing): docs/flying.md "Each mount its own jump"

[Sounds]              # the mount's sounds, for every skin of it: files of the mount folder's sounds\ (docs/mount-sounds.md)
lips = none           # [Sounds.Ride], [Sounds.Cart] and [Sounds.Baby] as in a skin.cfg
```

The sections of `mount.cfg` have the same plain names as a `skin.cfg`'s own (`[Tuning]`, `[Sounds]`, ...): the
folder the file sits in decides whose they are. The file keeps them in this order: `[Mount]`, `[Stats]`, `[Tuning]`,
`[Tuning.Ride]`, `[Tuning.Cart]`, `[Jump]`, `[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`. `id`, `name`,
`author`, `version`, `cart` and `foals` are always written. `[Jump]` is the mount's alone (API 0.39.0): in a
`skin.cfg` it is ignored and the report says it goes in `mount.cfg`; a bad number or an unknown key is named in the
report and the default jump's value stands, a number out of its range is taken to the nearest end.

The settings show a **Mount** list (Donkey, Dragon, Giant Rat, ...) and under it a **Skin** row with that mount's
skins. The mount's `skins\default\` is its **Default** (the game's own art for the donkey), listed first by that
word, and the one a new instance of the mount wears; the others by their `name`. So a mount with one skin shows
"Default" only, and a second dragon look (another folder under `saddleup-dragon.dragon\skins\`, from any pack) shows next
to it by its name. There is no `default_skin` key: one in `[Mount]` is reported as ignored.
Every mount a player can pick has at least one skin. Keys the loader doesn't know in `[Mount]` are ignored and named
in the log; a bad `foal_scale`, `speed` or `cargo` is named too and the default stands (a speed
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
`HitchGapPx`, `Depth`, `CenterOffset`. Anything about the creature itself (speed, the cart, foals,
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

## Variants: built on the default

Every mount but the donkey has one own look, its `skins\default\`. Every **other** skin of that mount is built on
it (the user, 2026-10-24: "all other skins are built on top of it"): it starts as a copy of the default, and only
what it gives replaces the default's. A variant can be one picture and three lines.

- **Pictures.** A frame the variant has no PNG for is the default's. A frame it draws is its own (an all-transparent
  picture too: a frame blanked on purpose). A picture of the wrong size, or one that can't be read, is said in the
  log and the default's is drawn there. With `sheet =`, its sheet's cells are its own and loose PNGs beside it are
  not read; cells past the sheet's end are the default's. The `front\` layer the same, per frame.
- **Settings.** What its `[Skin]` doesn't say comes from the default: `canvas` (and `pivot`), `colours`, `seat`,
  `offset` / `offset_cart`, `description`, `portrait`. Its own `[Colors]` lines go over the
  default's (a colour it names leaves the default's region). Its `[Tuning]`, `[Tuning.Ride]` and `[Tuning.Cart]` go
  over the default's key by key, and its `[Sounds*]` lines over the default's (which go over the mount's): a variant
  with no `[Sounds]` sounds like its default. A variant's file lines read its own `sounds\` only.
- **Saying "not the default's".** `seat = none` (no seat: the player's own),
  `portrait = none` (the mount drawn in the list), `offset = 0 0` (no offset). Never taken from the default: `name`,
  `author`, `version`, `format`, `needs`.
- **`layer = over`** on a variant draws it **over the default**, as Hellfire is drawn over the game's donkey: per
  frame the default's picture, the variant's erase colour (FF00FF unless `erase =` says another, or `none`) clears it
  there, the variant's other pixels on top. A colour of the default's pixels keeps the default's region (Coat when it
  names none); a colour only in the variant's pixels is its `[Colors]` region, or kept as drawn. `[Base.*]` is for a
  skin over the donkey and is ignored here (said in the report).
- **The canvas.** Layering needs the default's canvas and pivot: leave `canvas` out. A variant with a canvas of its
  own takes the default's settings but none of its pictures (and not its seat row unless the height and pivot row
  match): said once in the log.
- **Order and changes.** The loaders take `skins\default\` first. A skin added to the mount from another mod's folder
  (a folder without `mount.cfg`) is built on the default too, whenever the default arrives; until then it stands
  alone, and if the default never comes it stands alone for the session (said at the game's start). The default read
  again or replaced: its variants follow and their wearers rebuild; withdrawn: they stand alone. The Mounts tab (F11)
  shows "built on ...: N own pictures, M from it", a row a variant doesn't set as "(from the default)", and a
  right-click on a row it sets takes the default's again; its save writes only what the variant says itself.
- **Code skins.** A `MountSkinDef` on a mount other than the donkey, not that mount's `default`, takes the default's
  values for what it leaves at its "not said" value (`SeatRow` -1 with `SeatFromPicture` false, `Offset` zero and
  `CartOffset` null, `Description`, `Portrait` and `Regions` null, the canvas 96x96 and pivot 48,48). Its
  sprites are its own: pictures of or into code sprites are not layered yet, and `Over` on it is refused (drawn in
  the default's place, said in its report).
- **The donkey** has no default skin (its own look is the game's art): its skins are exactly as before.

A worked example: a red variant of the rat with its own standing frame and walk, a darker eye, and the rider a
pixel lower. Everything else (the other 35 frames, the seat row, the description, the sounds) is the rat's default:

```
gk2mounts\saddleup-critters.rat\skins\me.red\    (in your mod's folder, no mount.cfg: a skin added to Saddle Up's rat, named <mod>.<name>)
    skin.cfg
    bdy_static_left.png
    bdy_walk_left_01.png ... bdy_walk_left_08.png
```

```ini
[Skin]
format = 1
name = Red Rat
author = me
version = 1.0

[Tuning]
SeatAdjustPx = -1

[Colors]
Eye = 600000
```

The same as horns on the rat's head, drawn over its default instead (`layer = over`): only the frames the horns
show in, each a picture of the horns alone, with FF00FF where the rat's ears should go.

## Worked examples in this repo

Every built-in skin is a pack of exactly this format, kept as the example of one way to make one (the user,
2026-10-19: they are to be examples for mod authors who make their own mounts and skins):

| Example | What it shows | Where |
|---|---|---|
| **Hellfire** | a skin drawn **over** the donkey that also takes things off it: the erase colour under horns and a devil's tail, `[Base.Coat]` for the ash coat, `[Colors]` so the cracks and the glow follow the player's muzzle and eye colours | `src/SaddleUp/assets/mounts/donkey/skins/saddleup.hellfire/` (generated by `tools/skins/hellfire/make_hellfire.py`) |
| **Royal Parade** | a plain **over** pack: a saddle cloth and a crown, the cloth's reds named `Harness` so the player's harness colour recolours them, the gold kept as drawn | `src/SaddleUp/assets/mounts/donkey/skins/saddleup.royal-parade/` (generated by `tools/skins/royal-parade/make_royal.py`) |
| **Giant Rat, Giant Snail, Giant Worm, Dragon** | a **mount** of its own (`saddleup-critters.rat`, `saddleup-critters.snail`, `saddleup-critters.worm`, `saddleup-dragon.dragon`; `mount.cfg`: its facts, speed, sounds) with its default look, which **replaces** the donkey (`skins/default/`: the pictures, the rider's seat, the colours); the dragon's `front\` layer drawn above the rider and its flight sounds in the mount's `sounds\` | `packs/SaddleUpDragon/gk2mounts/<mount>/`, `packs/SaddleUpCritters/gk2mounts/<mount>/` with `mount.cfg` and `skins/default/` (drawn by `tools/skins/creatures/make_creatures.py`): external packs, each its own download (docs/making-a-mount.md "Built in or external"); before 2026-10-26 embedded in DLL and registered with the API at start, the worked example of shipping mounts in a mod (docs/mounts-as-mods.md) |
| **Gerry** | a mount (`saddleup.gerry`) out of the game's own character frames (`source = game`), no pictures of its own; `noun = skull`, babies 0.33, `seat = picture` | `src/SaddleUp/assets/mounts/saddleup.gerry/mount.cfg` and `skins/default/skin.cfg` |
| **The donkey** | the one mount that is not a file (the game's own art, no `SkinDef`, no `mount.cfg`): what every skin under `donkey\skins\` is a look of | none: its facts are in the code (`Catalog.Donkey`) |
| **Sepia, Checker** | the same from code: a recipe (`donkey/example.sepia`) and a mount `example.checker-beast` whose `default` skin is a picture source, registered through the API | `examples/GK2DonkeySkins/` |

Each `skin.cfg` and `mount.cfg` is commented and in one layout (`tools/skins/canonical_cfg.py` rewrites a built-in
into it; its `--check` in the release scans refuses one that isn't, and `tests/SkinsScan` checks the section order and
the key rules). A `skin.cfg`: `[Skin]`, then `[Game]` for a game creature, the skin's own geometry (`[Tuning]`,
`[Tuning.Ride]`, `[Tuning.Cart]`: the seat and the pixel keys this drawing needs), the skin's sounds (`[Sounds]`,
`[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`), then the colour sections. A `mount.cfg`: `[Mount]`, `[Stats]`,
`[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]`, `[Jump]` (its keys in one order), `[Sounds]`, `[Sounds.Ride]`,
`[Sounds.Cart]`, `[Sounds.Baby]` (the creature's facts), every key in one place. The donkey itself, the one mount that is not a file, would read
`[Mount] id = donkey, name = Donkey, author = the game, version = 1.0, cart = true, foals = true` with no tuning;
its folder holds skins only. A mount's sounds live in its `mount.cfg`'s `[Sounds]`, and the donkey's own
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
art is in the pack (its `skin.cfg` in `gk2mounts\saddleup.gerry\skins\default\`, next to the mount's `mount.cfg` two
folders up).

```
[Skin]
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
  (5), grazing (5), chewing (3), lips (13), turning its head (6), and the standing head (3). A mount's `default`
  draws them all; its other skins only the ones they change (the rest are the default's: "Variants: built on the
  default").
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

## What the loader refuses

A pack is someone else's files loaded into a player's game, so the loader checks them before they cost anything
(docs/multi-mod-loading.md, API 0.40.0), and says what it left out in the log:

- a picture is read from its PNG header first: a frame larger than the skin's canvas, a sheet larger than its cells,
  a portrait over 512x512 are refused without being decoded; a file that isn't a PNG too;
- a sound file over 16 MB, and a WAV with a chunk whose length doesn't fit the file;
- `NaN` and `Infinity` (and a number with a decimal comma: `1,5`) wherever a number goes; `pivot`, `offset` and
  `offset_cart` are kept within the canvas range;
- a file name (`sheet =`, `sheet_front =`, `portrait =`, a sound) outside the pack's own folder: `..`, a drive, a
  network share;
- a sheet's `frames =` name that isn't a frame key (letters, digits, `_`, `-`);
- a `mount.cfg` whose `[Mount] id` isn't its folder's name, or whose `needs =` is newer than the API;
- a skin of a mount that isn't installed is kept, but offered nowhere (the Mounts panel lists its mount as not
  installed); it is worn once its mount comes.

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
`gk2mounts\<mount>\skins\<mod>.<your skin>\` (no `mount.cfg`, no `default`, your prefix on the name;
`gk2mounts\donkey\skins\<mod>.<skin>\` for the donkey). Only your
own art, or art you may redistribute, goes in. A pack can also travel inside a mod's DLL with a version and a declared
dependency on the API: Saddle Up's built-in Gerry and donkey skins travel that way; so do its dragon, rat, snail and worm,
in small DLLs of their own: its external packs (docs/making-a-mount.md "Built in or external").
