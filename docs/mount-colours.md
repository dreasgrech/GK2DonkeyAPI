# Colours on every mount: one scheme, no special donkey (design, 2026-10-18)

The user's rule for this: **keep things standard and common between the mounts.** The colour settings a player has
(Coat colour, Harness colour, Second tone, custom colours with lightness, and the API's region settings) must mean the same thing
on the donkey, the dragon, the worm and Gerry, with the donkey being one look among others, not the model the rest
are bolted onto.

## 1. What exists, and why it stops at the donkey

Measured 2026-10-18 (`Colors.cs`, `docs/colors.md`, the built-in skins' PNGs):

- **The engine is an exact colour swap.** Each region (Coat, Dark, Muzzle, Eye, Harness for the mount; CartBody,
  Shafts, Straps, Hooks, Wheels, Hubs, Pole, Lamp, Glass for the cart and lantern) is a *list of the art's own
  colours*, embedded in `Colors.cs` and read from the game's atlas by hand. A region's settings are a function of a
  colour (`RegionMap.Apply`: hue, saturation, brightness, contrast, colorize, tint, overlay, swaps). For every listed
  colour the mod computes its replacement and builds, per sprite group, an N×2 palette texture the game's sprite
  shader loops over (`USE_PALETTE_COLOR_REPLACE`, a texel within 0.004 of a source colour is swapped; no cap on N,
  the keeper's own palette is 32 wide), or a 32³ colour cube (`USE_LUT_COLOR_REPLACE`, one lookup, 8 levels per
  channel). The cart's meshes always use the cube.
- **The donkey's five regions are the donkey's atlas colours.** A skin drawn in its own colours (every built-in
  creature, every pack with `colours = own`) has none of those colours in its pictures, so nothing matches and nothing
  changes. The recipe skins of 2026-10-17 (zebra, ghost, ember, golden, shadow) were recolours and patterns: the
  colour settings do that now, so they are gone (2026-10-18), as is Blue Dragon. Hellfire, the one recipe left then
  (`LooksHellfire.cs`; a pack drawn over the donkey since 2026-10-19 evening, docs/skins.md), changes the drawing (horns, a devil's tail, glowing eyes, lava cracks) and declares its own
  colours by region in code (the lava as Muzzle, the bone and outline as Dark, the glow as Eye; the ash coat is
  Coat). The cart's regions work for every mount because the cart is the same cart.
- **A skin's pictures share one material path with the donkey's.** The overlay copies the body part's
  `sharedMaterial` (Colors' per-instance copy, with the palette or cube and the keyword), so whatever pairs the
  engine builds reach the skin's sprites already. The sheet texture is shared by every wearer; the material is per
  instance; so two donkeys in the same skin can have different colours. Nothing in the drawing path needs to change.
- **The custom colours' lightness is anchored to the art.** "Olive 70%" sets Colorize = olive and Brightness =
  0.7 × (olive's value / the region's average art value) (`MountCompanion.Lightness`, `Colors.ArtLightness`), so the
  same picked colour comes out the same brightness whatever the art's own. Per look, this anchor must come from
  that look's colours.
- **How many colours a look has:**

  | Look | Distinct opaque colours | Mount of art |
  |---|---|---|
  | Donkey (the game's atlas) | 41 in five regions | palette art |
  | Dragon | 21 | palette art (our tool quantised it) |
  | Giant Rat | 17 | palette art |
  | Gerry (the game's skull frames) | a few dozen | palette art |
  | Giant Snail | 300 | shaded art |
  | Giant Worm | 720 | shaded art |

  A swap palette of 720 entries is 720 texture samples per pixel of the worm: not acceptable. The cube takes any
  number at one sample, with colours closer than 1/32 per channel changing together, which on shaded art is invisible.

## 2. The scheme

**Every look is pictures plus a region map, and the engine knows nothing else.** A region map says which of the
look's colours belong to which region, over one fixed vocabulary of mount regions. The donkey's map is the one
embedded today. A skin's map is read from its `skin.cfg`, and without one every colour of the skin is its Coat.

### 2.1 The vocabulary is fixed and shared

**Amended 2026-11-03** (section 7): the five regions stay the shared vocabulary, and a skin may add **tones of its
own** beside them. The reason below ("then it would exist for one mount only") stopped holding on 2026-10-28, when
the looks became per skin (docs/per-skin-looks.md): the Colours window shows the chosen skin's rows only. The text
below is the 2026-10-18 decision, kept for the five regions.

The mount regions stay the five that exist: **Coat** (the body), **Dark** (outlines, hooves, horn, bone edges),
**Muzzle** (a second body tone: belly, membrane, shell), **Eye**, **Harness** (tack: straps, saddle, reins). No
skin adds a region of its own: a new region would need a settings row, a cfg section, an API entry and a panel
button, and then it would exist for one mount only. The names are the donkey's words; the meaning is the role. A
skin author maps "the shell" to Muzzle, "the wing membrane" to Muzzle, "the straps" to Harness. The player sees the
same rows on every mount, and a mod using `Mounts.Colors` sets `CoatColorize` on every mount.

The cart and lantern regions are untouched by this: they are the cart's, and every mount pulls the same cart.

### 2.2 Where a look's colours come from

- **The donkey:** the embedded lists, as today.
- **A skin:** its colours are collected while its sheet is built (`Loaded.Put` has every cell's pixels before the
  sheet is uploaded and made unreadable), as the set of distinct opaque colours. No author lists them, and the
  list can't go stale against the pictures.
- **A skin's `[Colors]` section** moves colours out of Coat into the other regions, by hex:

  ```
  [Colors]
  Dark = 1c060c 080a22          # outlines
  Muzzle = d0463e ac282e        # the wing membrane
  Eye = ffffff
  Harness =                     # none on this mount (the default when the key is absent)
  ```

  A colour named that the pictures don't have is a warning (and a SkinsScan failure for the built-ins); a colour
  in two regions is an error. Everything not named is Coat. A pack with `colours = palette` keeps the donkey's map,
  since it is drawn in the donkey's colours (a recipe is an own-colour look, see below).
- **The game-source skin (Gerry):** the same as any skin; its colours are the game's skull colours, collected from
  the frames at build.
- **A recipe skin** (a mod's, through the API; Hellfire was one until 2026-10-19) is an own-colour look too: all Coat but the regions it declares in code. A pack with
  `colours = palette` is the one case that keeps the donkey's map, because its pictures are in the donkey's colours.
- **A skin worn over the donkey** (`layer = over`, own colours; built 2026-10-19 for Royal Parade): the donkey's
  map stays (the donkey is still drawn under it), the skin's named colours are added after it (`Colors.Rebuild`;
  one the donkey has already stays the donkey's, logged once), and its unnamed colours belong to no region: kept as
  drawn (`Looks.SortColours`). The swap count is the donkey's colours plus the named ones, so it stays on the exact
  swap; `ArtLightness` keeps the donkey's averages, but for a region the skin names colours of its own under (since
  2026-11-08: the Jester's harness, drawn by the skin with the donkey's hidden; against the donkey's dark harness a
  picked pink lifted its shades all to white): those colours' average. A following tone's colours leave the region's
  list (`SkinTones.Add`), so Royal Parade keeps the donkey's. Self-test "colours: a skin over the donkey keeps the
  donkey's regions" (its lightness checks too).

### 2.3 Which shader path

**Amended 2026-11-04:** for a skin over the donkey the threshold is 96 pairs, the donkey's 42 (40 since 7.11) and the skin's named
colours (`PaletteMaxColoursOver`; 64 that morning, 72 for the Jester's six parts, 7.6, then 96 for every donkey
skin's parts their own, 7.10: the colour swap's cost measured in game showed none up to 96, `MeasureTests.PaletteCost`,
docs/performance.md; section 7.9: on the cube, two pairs of the donkey's own
colours share a cell, so a skin over the donkey that crossed 48 turned some of its outline and coat pixels the
harness colour). A skin in its own colours keeps 48 (`PaletteMaxColours`; the sixth review: one of 49 to 64 colours
keeps the path and the look it had). The text below is the 2026-10-18 decision.

Per look, by its colour count: **up to 48 colours, the exact swap palette; more, the cube.** The threshold is the
keeper's own palette (32) with room; the donkey, the dragons, the rat and Gerry stay on the exact swap, the snail and
the worm go through the cube. The `SpriteMethod` setting keeps its meaning for looks under the threshold (Palette or
Lut, the player's choice); above it the cube is the only path and the setting is not consulted, which the setting's
description says. No cfg key changes, so nothing a 0.9.1 player has moves (docs/compatibility.md).

### 2.4 Lightness anchored per look

`ArtLightness(region)` becomes `ArtLightness(look, region)`: the average value of that look's colours in that
region. "Olive 70%" is then as light on the worm as on the donkey. For an empty region (no Harness on the worm) the
anchor is unused because nothing is mapped.

### 2.5 The settings rows

Rows don't change, and none hides (a row that vanishes shifts the list and the player wonders where it went). An
empty region maps nothing; the row's description says so ("on the mounts whose look has any; the others ignore
it"). A "(none on this mount)" note after the value is a later step: a `Choice` row has no dynamic suffix, and in
the Mods menu the framework draws the row, so it would go through `MenuLabels`.

### 2.6 The look is kept per mount (per skin since 0.41.0)

**Amended 2026-10-28** (the user, the day the Donkey Skin Pack went public: "skins look different so the colors
customization needs to apply for each"): the look is kept **per skin** now, everything in the Colours window
included (the lantern toggles too); a mount's first skin keeps the mount's keys, the others carry the skin's id;
the mount's own (name, babies, volumes) stays per mount. docs/per-skin-looks.md has the design, the seed from a 0.12
file and the tests. The text below is the 2026-10-18 decision it replaces.

What the colour rows set belongs to the mount they were set on (the user, 2026-10-18: "we're not saving these
values per skin/per mount"). Per mount, not per skin: the dragon is green whichever of its skins is worn, which is
the definition (the skin is the drawing, the mount the creature), and it keeps one set per mount rather than per
skin. The cart's colours go with the mount too, since they are set in the same window for that mount.

- `MountSettingsPage.PerMount(rows)` builds a mod's look rows once per registered mount: the donkey's rows keep
  their keys (`Coat`, `CustomCoat`...: a cfg from 0.9.1 keeps its values untouched), the others get the mount's id
  (`Coat_dragon`, `CustomCoatLightness_dragon`). The chosen mount's set shows (`Row.Shown`); a row's own condition
  (the custom colour with its preset) is kept apart as `Row.Enabled`, which the Mounts panel greys rather than hides.
- `MountCompanion.LookOf(mountId)` is one mount's `MountLook`; `Coat`, `Harness`, `Paint`... on the companion are
  the current mount's (`MountId`, the mount of its skin). A value set on another mount's look waits until that
  mount is chosen.
- A mount registered after the page was built has no rows (the same limit as the Skin rows); its look stays the
  game's colours.

### 2.7 What the built-in creatures declare (as of 2026-10-20)

Written into each `skin.cfg` by `tools/skins/skin_colors.py` from the pictures themselves (the body and the `front\`
layer), by rules that are data in `tools/skins/creatures/colors.json` (a block per skin: per region, in order, hue,
saturation and value thresholds; the first match wins; everything else is Coat). Re-run after the art changes, then
`tests/SkinsScan` (which checks every listed colour against the pixels):

- **Red Dragon:** Coat = the red scales and the wing; Muzzle = the golden belly, the page's Second tone row (it
  spent an afternoon as Coat, 2026-10-20: a gold belly under a green coat read as wood and no row reached it; with
  the Second tone row the belly is a colour of its own again, tweakable apart from the scales); Dark = the
  outlines, the horns and the back spikes (bone); Eye = the yellow eye.
- **Giant Rat:** Coat = the grey fur; Muzzle = the pink tail, ears and nose; Dark = the outline; Eye = the red eye.
- **Giant Snail:** Coat = the green body; Muzzle = the brown shell; Dark = the outline (300 colours: the cube path).
- **Giant Worm:** Coat = the pink skin; Dark = the outline (720 colours: the cube path).

  Audited 2026-10-20 (every standing frame classed by its cfg, the share of the drawn pixels per region): the
  dragon Coat 60, Muzzle 10 (the belly), Dark 29 (its outline is Dark), Eye under 1; the rat Coat 79, Dark 13, Muzzle 7; the snail Muzzle
  41 (the shell), Coat 38, Dark 19; the worm Coat 84, Dark 15; Royal Parade Harness 61, the rest kept (the gold
  trim and the blue, unnamed on an over skin, are drawn as they are); Hellfire's own colours all named. So a
  player's Coat turns the body of every creature; the rat's pink parts, the snail's shell and the dragon's belly
  are the Second tone row's; bone, horns and outlines stay. The self-test "colours: the Coat row reaches every body colour of every built-in
  creature, and no other" proves it in the game on all four (first run 2026-10-20: rat 10 of 10 Coat colours
  changed, snail 127 of 127, worm 717 of 717, dragon 10 of 10, none of another region).

  The Second tone presets (`MuzzleColor`) were chosen by measuring each one on every second-tone part (the
  colorize keeps a pixel's value and takes the preset's hue and saturation, so a preset of a part's own hue does
  nothing to it: the first Pink moved the rat's pink by 7 to 10 of 255 and the first Brown the snail's shell by
  5 to 18). Pale, Rose, Brown, Green, Blue and Dark each move the donkey's muzzle, the rat's pink, the snail's
  shell and Hellfire's lava by at least 22, most by 40 and more. The worm had no second tone: its rows were
  left out of the menu and greyed in the Mounts panel (since 2026-11-04 its mouth is its Muzzle, the "Mouth" row:
  docs/second-tone.md 8).
- **Gerry:** Dark = the sockets and cracks, the bone is Coat, read from the game's export of his frames
  (`BepInEx\GK2-asset-probe\game-skins\saddleup.gerry\default\`) when one exists; without the export the cfg carries no `[Colors]`
  and everything is Coat.
- **Hellfire, Royal Parade** (over the donkey): Hellfire's written once by its generator (`make_hellfire.py`, `--cfg`
  rewrites it; the cracks and the glow as Muzzle and Eye, so the page's Second tone row recolours Hellfire's lava), Royal Parade's by hand in its cfg (the cloth's reds as Harness); a donkey colour drawn by an over skin is kept as the donkey's (its own
  settings and the skin's `[Base.*]` reach it), which is how Hellfire's tuft and rump fill continue the body.

## 3. What is not in this

- No new regions, no per-skin region names, no per-skin settings rows (the rule). Replaced 2026-11-03 by a skin's
  own tones (section 7); the five regions are still the only ones every mount shares.
- No automatic clustering of a skin's colours into regions by hue or value: it would guess, and a wrong guess is
  worse than all-Coat. Authors name the exceptions; the default is honest.
- No recolouring of a skin's `front` layer differently from its body: the front layer is part of the same sheet
  and gets the same map.
- The dev panel's Colors tab swatches show the donkey's colours (`MountColorRegion.SourceColors`) whatever is
  worn: a dev tool, left as is.

## 6. Built 2026-10-18 (uncommitted at the time of writing)

Everything in section 4 but the row note: `Looks.Loaded` collects a skin's colours as its sheet is built and sorts
them by region from `[Colors]` (`SkinDef.ColorRegions`, parsed and validated in `FromCfg`); `Colors.Rig` takes the
worn look's lists for the mount's sprite group, re-maps when the worn skin changes, and picks the cube per group
over `PaletteMaxColours` (48); `ArtLightness` anchors to the look; a companion re-applies its look when its skin's
sheet becomes whole (`Looks.LoadedReady`); the creature skins carry generated `[Colors]` lists
(`tools/skins/skin_colors.py`: rules read off the enlarged standing frames), Hellfire and Royal Parade hand-written ones; SkinsScan validates the
lists against the PNGs with a PNG reader of its own; the self-test "colours: the player's regions on a mount in
its own colours" renders the worm on the settings picture, reads the pixels back (pink before, green after a custom
coat), checks the cube path and that the rat's Muzzle changes its pink colours only. Saddle Up's Coat and
Harness descriptions say what they do on every mount.

## 4. Where the work is

| Piece | Where | Size |
|---|---|---|
| A look's colour set collected at build, per-region map, `[Colors]` parsing and validation | `Looks.cs` (`Loaded`, `FromCfg`), `LooksGame.cs` | medium |
| Engine takes the worn look's map for the sprite group instead of the embedded donkey lists; rebuilds on a skin change; cube above the threshold | `Colors.cs` (`Regions`, the pairs loop, `ApplyMethod`), `ColorsPreview.cs` (`ArtLightness`) | medium |
| "(none on this mount)" on an empty region's row | `MountSettingsPage`, `MountCompanion` | small |
| Built-in `[Colors]` lists | `tools/skins/skin_colors.py` with `tools/skins/creatures/colors.json`, the seven `skin.cfg` | small |
| Checks: SkinsScan validates `[Colors]` against the PNGs; a self-test wears the worm, shifts its Coat hue and samples a sheet colour through the pairs; the dragon's Muzzle moves the membrane only | `tests/SkinsScan`, `Dev/SelfTest.cs` | medium |
| Docs: `docs/skins.md` (`[Colors]`), `docs/colors.md`, `docs/api.md`, `docs/reference.md` | | small |

API: `MountColorRegion.SourceColors` is the donkey's list today and public; it stays (the donkey's), and the per-look
sets are reachable through `MountSkinInfo` only if a mod ever asks. `Mounts.ColorRegions` is the vocabulary and
doesn't change. No public surface change is needed for the first build.

## 5. Decisions taken by the rule, to confirm

1. Five fixed regions for every mount; skin authors map into them; nothing per mount.
2. All of a skin is Coat unless its `[Colors]` says otherwise.
3. The shader path is chosen by colour count, not by a new setting.
4. Empty regions say so on the row; rows never hide.

## 7. A skin's own tones (design, 2026-11-03)

The user, 2026-11-03, on the Jester's Colours window: "the harness color controls part of the hat, the harness and
part of the cloth but harness color should only control the color of the harness and then the skin can introduce its
own color tones depending on what it needs so that we have full control of what can be colored instead of just
constraining ourselves to two colours." Decided the same day: the three standard rows keep their meaning, the
released skins keep their mappings, the Jester is the first skin with tones.

### 7.1 What a tone is

- **The standard rows mean the mount's own parts again.** Coat is the body, Harness the tack, Second tone (Muzzle)
  the second body tone: on a skin worn over the donkey, the donkey's under it. A skin no longer has to borrow one of
  them for a part of its own drawing (the Jester's motley under Harness made the Harness row reach the cap, the
  strap and the cloth at once).
- **A tone is a part of the skin's drawing the player can colour on its own:** an id, a name for its row, and the
  exact colours of its pictures that belong to it. A skin has as many as it needs, or none.
- **Found by exact colour, like the regions.** Two parts drawn in the same colours are one tone; to colour them
  apart, one is drawn a shade apart (the Jester's cap and cloth share their five reds).
- **One place per colour:** a colour is in one tone, or under one `[Colors]` region, or neither (Coat on a skin of
  its own, kept as drawn on an over skin).

### 7.2 skin.cfg

```
[Tone.cap-front]
name = Cap, front half
colours = C14859 A03444 862633 641A2A 3E0A14
```

- The id is the section's name after `Tone.`: lower case letters, digits and `-` (it is part of the cfg keys).
- **A tone the loader can't take is left out, the skin loads** (amended 2026-11-04, the sixth review): a bad id, a
  `follows =` that is no mount region, a colour that isn't RRGGBB or that another place names (another tone, a
  `[Colors]` region but the one it follows) leaves that tone out, said in the skin's report, checked before anything
  moves (`SkinTones.ColourProblem`). A pack's typo in the new section never costs a player the skin, as an unknown
  section never did on 0.43; a colour under two `[Colors]` regions still refuses it as in 0.43, and SkinsScan and
  `check_skin.py` still fail every one of these for the packs we ship.
- `name` is the row's label (default: the id); `colours` (or `colors`) the hex list, spaces or commas, on as many
  lines as wanted (every line's colours count, here and in `[Colors]`; the checkers read them so too).
- A palette skin's tones are not read at all (reported), so a colour they share with its ignored `[Colors]` doesn't
  refuse it.
- A colour the pictures don't have is said once, as for `[Colors]`. A tone without colours of its own is said in the
  report and colours nothing (no row), unless it is a variant's renaming its default's tone (below). `Custom` without a
  usable colour is the tone as drawn (not the region it follows).
- Palette skins (`colours = palette`) take the donkey's regions: their tones are reported and ignored; a variant that
  takes palette colours from its default says so once in the log (`LooksInherit.Resolve`).
- A variant (a skin built on its mount's default) takes its default's tones. One of its own with a default's id adds
  its colours to that tone; with a `name` of its own it renames it (colours not needed), without one the default's
  name and translation stay. A colour it names elsewhere leaves the default's tone (`SkinTones.Merge`,
  `LooksInherit.MergeRegions`, the same as `[Colors]`). A row's label is translated by the skin whose name it shows.
- A code skin says the same with `MountSkinDef.Tones` (a `MountSkinTone` per tone).
- An older API doesn't know the section: it says "unknown sections ignored" and draws the tone's colours as drawn
  (over) or as Coat (own). A pack with tones says `needs =` the API version that brings them.

### 7.3 The engine

- A tone is a region of the skin's own: its colours are collected with the rest (`Loaded.SortColours`, under
  `tone.<id>`), and the mount sprite group's pairs take them after the five regions (`Colors.Rig.Rebuild`, both the
  own-colour and the over branch). The shader path doesn't change: the pairs count only grows by a colour that was
  kept as drawn before and is now a tone's (the Jester's ten were named already: 52 pairs with both its tones set;
  its eight parts since make 78 with every one set, 7.6, on the exact swap, which holds 96 over the donkey, 7.9).
- A tone's value is the instance's (`Mount.Instance.Tones`: id to a colorize colour and a brightness), set by the
  companion at every apply (and on every load, `OnGameReady`), as drawn when unset. Not tuning keys and not in the
  side file: the companion holds the look, and a tone id is a skin's, not a key every instance knows. They belong
  to the owner that set them, as an override does (`TonesOwner`): another owner neither replaces nor clears them
  (said once), and they go with their owner's overrides (`InstanceTuning.ClearOwner`, `Reset`, so
  `Mounts.ClearTuning` too). They are kept with the skin they were made for (`TonesSkin`) and colour only that skin
  (`Colors.Rig.NamedMaps`): a skin changing under them (a sheet still building, another look) takes none of another
  skin's colours. A baby in its parent's skin takes the parent's; one not in it is a plain donkey and takes none. On
  a baby a following tone left on its first choice takes the baby's own shade of its region, as the region's pixels
  do (the coat, the second tone and the dark parts are shaded per baby). A tone given a colour takes the baby's shade
  too, its coat's (2026-11-04, the user: "ideally yes unless it's a performance cost we can't afford"): as
  `MountCompanion.Shade` shades a coat's colorize colour, its hue turned and its saturation scaled (`FoalLooks.ShiftHex`)
  and its brightness stepped (`FoalLooks.StepBrightness`, shared; `SkinTones.ValuesOf` with the baby's look), so a baby
  reads as one animal, its trappings with its coat. A tone is colorized whole, so its colour carries the hue and the
  saturation (a saturation value of its own would be painted over: `Colors.RegionMap.Apply` colorizes last).
  A preset stays under the cap its lightness keeps to (`SkinTones.BrightnessCap`: a lighter baby would clip the
  tone's lit shades to one flat colour, so it comes out darker, never lighter than the cap); a Custom colour takes the
  whole step. Nothing new is drawn for it: a baby has colours of its own already (its coat), so palette textures of
  its own, and a tone's shade changes a pair's colour, not the number of pairs. A tone on Game default stays as drawn
  on every baby, as the harness does.
- **Presets** (`ToneColor`): Game default (as drawn), Red, Gold, Green, Blue, Purple, Pink, Brown, Black, White,
  Custom. Each is a colour: the tone takes its hue and saturation and keeps each pixel's shading, and its
  brightness is the colour's own against the tone's average (`SkinTones.Lightness`, the custom colours' rule:
  `MountCompanion.LightnessAgainst`), so Gold reads gold on a navy tone as on a pale one. The average is of the
  colours the loaded skin draws (a listed colour no picture has, or a default's a variant doesn't draw, doesn't
  count), of its list while it loads; the companion applies the look again when the sheet is whole (`LoadedReady`,
  for the worn skin and the Mounts panel picture's), and the drawn colours are read from that frame on
  (`Looks.LoadedOwnColours` asks for the colours sorted, not for the sheet worn, which is a frame later). Custom is
  the palette with its lightness, as the other rows.
- **The colour cube.** Over 96 pairs (48, then 64 until 2026-11-04; the donkey's 40 since 7.11 plus an over skin's named colours) the
  mount takes the cube, where two colours in one cell change together, and on the cube a colour that isn't paired
  takes its cell's colour too. Measured 2026-11-03 on the four over skins: the only shared cells are two pairs of the
  game's own donkey colours (Dark 292316 with Harness 2D2114, Coat 362815 with Harness 3C2A14), which on the cube take
  the harness colour (the last pair written; the fifth review); no skin colour shares one. With 96 every skin over the
  donkey stays on the exact swap, all its tones set (since every part is its own, 7.10: Royal Parade 56, Hellfire
  55, Armored 70, the Jester 78).
  SkinsScan fails a tone colour that shares a cell with any other colour of the look.
- **The exact swap's reach.** The palette shader gives a texel the replacement of every source colour within 0.004
  of it, and one step in one channel is 1/255 (0.0039): two colours of the look one step apart in one channel are one
  colour to it, so one takes the other's colour. SkinsScan and `check_skin.py` fail a tone colour one step from a
  colour of another place (a region, another tone, the donkey's, one kept as drawn) while the skin can be on the
  swap; the shipped skins have no such pair (2026-11-04, the eighth review).

### 7.4 The rows and the API

- `MountSettingsPage.ToneRows(section, skinId, suffix, companion)`, called inside `PerSkin`'s body: per tone of the
  skin, in its file's order, a choice (the presets) and its custom colour with the lightness. Keys
  `Tone.<id><suffix>`, `CustomTone.<id><suffix>` and `CustomTone.<id>Lightness<suffix>`. A skin registered later gets
  them at `CatchUp`, as every `PerSkin` row. Saddle Up calls it after its Second tone rows and names no tone (the
  layering rule).
- The rows are made from the skin's tones as they are when its rows are built (a page's rows are bound once). A
  variant whose default registers after that (a mod registering a mount's default once the page is shown), or a
  pack edited while the game runs (the dev tools' reload), shows the change in its rows at the next start, the
  limit every row of something registered late has (a List row's choices, docs/per-skin-looks.md); registered in
  plugins' Awake, before the page shows, everything has them.
- A tone's rows are greyed once the skin, loaded, turns out to draw none of its colours (`SkinTones.Draws`: a
  variant drawing every frame of its own over its default's tone, whose choice would colour nothing; the tenth
  review). Until the skin is loaded nothing is known, and the rows stay live; the Colours window's picture wears the
  skin it shows, so its rows are right there.
- The words: the row's label is the tone's name, the skin's owner's text (`skin.<skin id>.tone.<id>.name`, step 3
  of docs/localization.md); the presets and the two custom labels ("{0}: custom colour", "{0}: lightness") are the
  API's. The row's English label (logs, comparisons) is "<name> (tone <id>)", so a pack's tone named like the page's
  picture row can't make the page refuse its rows (a picture is found by its label). A default's tone name that
  only its variants show is in the default's English file all the same.
- `MountLook.Tone(id)`: a `MountTone` (`Color`, `Custom`), per skin like the rest of the look. `MountSkinInfo.Tones`
  lists a skin's tones (id, name, colours). `MountSkinInfo.Regions` keeps the five regions only.

### 7.5 The released skins (amended 2026-11-04)

The 2026-11-03 design left them as they were; the user, 2026-11-04: an update must keep a player's look ("from his
own perspective, it would look like nothing or almost nothing changed but now there's more customization
available"), as much as possible without holding the new system back. So their parts are tones now, each part that
was under a region following that region (section 7.9), and the new parts as drawn:

Every part its own since the same day (7.10):

| Skin | Tone | Its colours | First choice |
|---|---|---|---|
| Royal Parade | Saddle cloth | the cloth's 4 reds (were Harness) | As the harness |
| Royal Parade | Crown | the crown's 3 golds (kept as drawn) | as drawn |
| Royal Parade | Trim and fringe | the cloth's border, hem band and fringe: 3 golds of their own (kept as drawn) | as drawn |
| Royal Parade | Emblem | the cloth's fleur: 2 golds of its own (kept as drawn) | as drawn |
| Royal Parade | Jewels | the crown's red and blue (kept as drawn) | as drawn |
| Armored | Helmet | 8 greys of its own (its outline 281B26 stays as drawn) | as drawn |
| Armored | Plume | 5 reds of its own (were Muzzle) | As the second tone |
| Armored | Body armour | 7 greys (were kept as drawn) | as drawn |
| Armored | Skirt | 4 reds (were Muzzle) | As the second tone |
| Armored | Brass | 4 golds | as drawn |
| Hellfire | Horns | the 3 bone shades (were Dark) | As the dark parts |
| Hellfire | Glowing eyes | the glow and its rim (were Eye) | As the eyes |
| Hellfire | Lava cracks | 3 (were Muzzle) | As the second tone |
| Hellfire | Tail | the tail and its underside (were Dark) | As the dark parts |
| Hellfire | Tail tip | the spade and its dark (were Eye) | As the eyes |

Hellfire's outline (1E0E0C, its horns' and the rump's where the tail was) stays under `[Colors] Dark` alone, the Dark
row's as the donkey's own outline is. **Amended the same evening** (docs/second-tone.md 8): no part's first choice is
"As the ..." any more; a part the table says follows a region took the player's saved value of that region once (the
update's first load) and is a part of its own since; Royal Parade's Harness row reaches the donkey's harness only. No key moves and no saved value changes; every look is the same until the
player picks a colour in a new row. Dark and Eye still have no player rows (Hellfire's horns and glow follow them, so
they are as drawn, and a baby's shade of its dark parts reaches its horns as before); the dev panel's Colors tab still
shows the donkey's colours.

### 7.6 The Jester

**Amended 2026-11-08: the harness is the Jester's own.** With a Harness colour picked, the user saw the collar stay
red and the donkey's strap turn the colour ("the original harness underneath, which should be invisible since we're
using our own custom harness, is getting the actual cream colour"). The collar band's shades are listed under
`[Colors] Harness` now (no `collar` tone), and the band runs from the crest to the chest and tapers to a
point at its end (the same shape and colours, no line between: a point of its own colours read as detached), its
bell under it, edged in the donkey's outline colour (a light harness colour turned a burgundy edge tan).
`draw_jester.py` (`strap_hidden`) hides the donkey's strap in every frame, drawn over as the neck and chest it lies
on (each pixel the nearest coat, mane or muzzle colour, the outline's at the edge), so the Harness row colours the
Jester's harness and none of the donkey's shows (16 px of it did standing, up to 39 in the walk, 59 in turn 04-06).
A first try drew the strap over in the band's colours: the strap's shape in the harness colour read as "the original
harness ... shown instead of our custom one", broken at the chest under the bell. The collar's points are two tones: `collar-points` (burgundy: the point over the chest, the
front view's middle one; shades from `part_palette.py`) and `collar-points-second` (navy: the front view's outer two).
`[Base.Harness]` stays for a pixel a frame might leave. `check_skin.py` no longer warns that a covered region's row
colours nothing when the skin lists colours of its own under it.

Six tones since 2026-11-04 (the user: "yes so that we start making use of our new multi colour tone system"), every
part of the motley its own row: `cap-front` "Cap, front half" and `cap-back` "Cap, back half" (each point with its
half of the hood), `collar` "Collar" (the band and the middle point) and `collar-points` "Collar points" (the two
outer ones), `caparison` "Caparison" and `caparison-second` "Caparison, second colour" (its two pairs of quarters);
since the same day also `cap-band` "Cap band" and `bells` "Bells" (every bell: the cap's, the collar's, the hem's),
the gold that was drawn as it is (7.10).
To the eye the motley is still one burgundy and one navy: the parts shared those two ramps, and a colour is one tone's
only, so `tools/skins/part_palette.py` gave the cap and the collar shades of their own, one to four steps off the
caparison's, each in cube cells no other place of the look has (`draw_jester.py` draws them; `check_skin.py` passes).
Before, two tones, `motley1` (the burgundy) and `motley2` (the navy), each reaching every part. Harness reaches the
donkey's own strap only (still tinted the burgundy under the skin, `[Base.Harness]`), Second tone the donkey's muzzle
only. 42 pairs with no part set, 71 with all six: over the 64 the limit was that morning, where on the cube the
always-tinted strap would turn the donkey's two shared cells (7.3) the harness colour, so the limit went to 72 (2.3),
then to 96 when its cap band and bells became parts too (78 pairs; 7.10).

### 7.7 Checks

- SkinsScan: `[Tone.*]` read like `[Colors]` (the id, the colours in the pictures, one place per colour), the cube
  check and the exact swap's one-step check above; `tools/skins/check_skin.py` the same while drawing (and the
  one-step pairs of two other places for the eye). A variant is read as the game builds it: its default's places with
  the compatible listing the tone's, and of the default's pictures only those it doesn't draw itself.
- Self-test "colours: a skin's own tones": a temp over skin with two tones and a Harness colour; one tone set, only
  its colours change, the other tone and the Harness colours don't, the world draws them so; the rows: two per tone
  plus the lightness, under the skin's keys, the label the tone's name.
- `tools/page-carryover.py` knows `ToneRows` (the shipped skins' tones), so a later file with tone keys is checked.

### 7.8 Built (2026-11-03, the evening)

- `SkinTones.cs` (the parse, the presets' table, the lightness, the look's values, onto an instance),
  `Api/MountTones.cs` (`ToneColor`, `MountSkinTone`, `MountTone`); `Looks.FromCfg` reads `[Tone.<id>]` and hands each
  to `SkinTones.Add`, whose `ColourProblem` is the tones' place-per-colour check (a tone it can't take is left out;
  `AddRegionColours` is `[Colors]`' own, throwing as in 0.43), `SortColours` keeps a list per tone, `LooksInherit`
  merges a variant's; `Colors.Rig.Rebuild` pairs the five regions then the tones (`NamedMaps`) in both branches;
  `Mount.Instance.Tones`; the companion applies them to the mount, the babies and the picture; `MountSkinInfo.Tones`,
  `Regions` without them; `MountSettingsPage.ToneRows` with two `Row` fields (`LabelOf`, `ChoiceWords`) that `Texts`
  draws and the English dump skips (the tone names go in their skin owner's file); 18 API words in all 11 languages
  (13, and the five follow words of the fourth review).
- Saddle Up: one line, `p.ToneRows("Colours", skinId, sfx, me)` after its Second tone rows.
- The Jester (`tools/skins/jester/skin.cfg`): `[Tone.motley1]` "First motley colour" and `[Tone.motley2]` "Second
  motley colour" in place of `[Colors]` Harness and Muzzle; still loose in `BepInEx\plugins\gk2mounts\`, in no pack.
  Six parts since 2026-11-04 (7.6).
- Checks: `tools/skins/check_skin.py` and `tests/SkinsScan` (both proven failing on a probe pixel 736C58 named as a
  tone beside the donkey's coat 736C57, and passing on the Jester); `canonical_cfg.py` keeps `[Tone.*]` among the
  colour sections; self-test "colours: a skin's own tones, each its own colour and its own rows" (the Skins group).
- On the way: `tools/page-carryover.py` read its skins from the folders (a hand list missed Armored: the 0.15.0
  fixture's 23 Armored values read as lost), and SkinsScan's pack checks count (three "FAIL pack ...: Languages\"
  lines printed under an ALL PASS since 2026-11-01).

### 7.9 A tone that follows a region: the players' looks kept (2026-11-04)

**Amended the same evening** (docs/second-tone.md 8, the regions named per look): a tone with `follows =` is a part of
its own, its first choice Game default (as drawn); the engine no longer pairs it with its region's map. Its first
value is the player's saved value of that region, taken once by `MountSettingsPage.ToneRows` while the file holds none
of its rows (the first load after the update), the region's Custom colour as it was, a preset as the part's nearest.
So a player's look carries over as before, without a row that colours two things (the Harness row the donkey's strap
and Royal Parade's cloth). The text below is the earlier design.

A part of a released skin that was under a region (Royal Parade's cloth under Harness) becomes a tone with
`follows = Harness`. Copying the player's saved Harness choice into the new tone's row would not give the same look:
the Harness presets are colours of their own (Black also takes saturation away), and a custom tone colour is a
palette swatch with a 10% lightness step. Following gives it exactly:

- **The first choice of a following tone is the region** ("As the harness", in place of Game default): with it the
  tone's colours are paired with that region's own map (`Colors.Rig.NamedMaps`), the very function that coloured
  them when they were the region's colours. A player's saved Harness colour reaches the cloth as before; one who
  never set one sees the cloth as drawn. Any other choice gives the tone its own colour; the first brings the
  following back.
- **The rest of the look is as it was:** the region's custom colour still anchors its lightness to the colours of the
  tones that follow it (`ArtLightness`, a skin in its own colours), a baby's shade of the region reaches the tone,
  and `MountSkinInfo.Regions` lists the followed region (the row isn't greyed).
- **An older API reads it as before.** A following tone's colours may stay listed under its region in `[Colors]`
  (the compatible listing): this API moves them to the tone (`SkinTones.Add`), one without tones ignores
  `[Tone.*]` (said once as unknown sections) and colours them under the region as before. So a player who updates
  the skin pack but not GK2 Donkey API sees no change either, and the pack needs no `needs =`.
- **A tone nobody set doesn't move a skin to the cube.** A tone that follows nothing and has no colour of its own
  isn't paired on a skin over the donkey (its colours drawn as they are, as before they were a tone), so Royal
  Parade stays at 46 colours and Armored at 47 until a player gives its crown or its steel a colour (then 49 and
  59). With 48 that would have been the cube, where the donkey's own shared cells (above) turned some of its
  outline and coat pixels the harness colour (the fifth review): the exact swap holds 64 now (96 since every donkey
  skin's parts are their own, 7.10), so every skin over the
  donkey stays on it, and Hellfire (55) and the Jester (52) left the cube for it (their colours exactly as drawn).
  A skin over the limit is still checked for shared cells by SkinsScan and `check_skin.py`.
- **A variant listing its shades of a default's following tone under the region too** (for an older API) says
  `follows =` itself: the loader moves the listed colours when the tone's own section says what it follows (the
  default's isn't known yet when the variant's file is read), else the variant's tone is left out (said in the
  report): those shades stay under the region and the default's tone keeps its own.
- **The API version at release:** Saddle Up calls `ToneRows`, so its `NeedsApi` and `[BepInDependency]` minimum go
  to the API version that brings tones (a minor: 0.44.0) when the versions are bumped. Until then Saddle Up calls it
  through a wrapper kept out of line (the seventh review) that catches `MissingMethodException`: on an API of 0.41 to
  0.43 its colours window builds without the parts' rows and the log says so once (0.40 and older get the "too old"
  message: the gate was 0.40 though Saddle Up calls 0.41's looks per skin, raised by the eleventh review). The
  wrapper goes with the bump (docs/release-checklist.md), so a later change to `ToneRows` can't hide behind it.
- **A preset's lightness stops where the tone's lightest shade reaches white** (the fourth review): a preset's
  brightness multiplies each shade's value, so on a dark tone a light preset (Gold, White) clipped the lit shades to
  one flat colour; it lifts the tone only until its lightest shade is white now. The regions' custom colours keep
  their rule: changing it would change players' saved looks.
- The skin generators write the same tone sections (`make_hellfire.py --cfg`, `make_armored.py --cfg`; checked equal
  to the pack files); Royal Parade's cfg is by hand.

### 7.10 Every part its own (2026-11-04)

The user: "now lets make sure that all the donkey skins are using the new multi tone system where every part is named
and is able to be coloured". Found by `tools/skins/part_map.py` (each part's pixels painted one colour, the colours no
row reaches white): Royal Parade's crown jewels had no row, and one row each covered its crown with the cloth's trim
and emblem, Armored's helmet with the body armour and its plume with the skirt, Hellfire's horns with the tail and the
eyes with the tail's spade; the Jester's gold (the cap's band, every bell) was drawn as it is. Now every part has a
row of its own (the table in 7.5; the Jester in 7.6). Not parts: the line art (Armored's 281B26; Hellfire's outline,
under Dark then, in no region since 7.11) and the donkey's own colours a skin draws (they follow the donkey's rows).

- **Parts sharing a ramp got shades of their own** (a colour is one tone's only): `tools/skins/part_palette.py` finds
  each part the nearest shades the checks take (a cube cell of their own, not one step from another place's colour),
  one to four steps off, the same colour to the eye. The part the eye knows best keeps its colours: the crown, the
  body armour, the skirt, the bells. Hellfire's parts had colours of their own already: its pictures are unchanged,
  only its cfg's tones split.
- **In the generators, not by hand:** `make_royal.py` (the trim's and the emblem's golds), `make_armored.py` (the
  helmet's and the plume's palettes, `HELM_PAL` and `PLUME_PAL`; the helmet's mouth fill its steel), `draw_jester.py`
  (the band's gold), `make_hellfire.py` (the tones). Each was proven to reproduce the pack before the change (Royal
  Parade but one pinhole pixel the stamp tool fills since, the donkey's own colour), and each change to recolour
  only: every new picture, its colours mapped back, is the old one pixel for pixel.
- **Saved looks:** a part split from one that followed a region follows it too (Armored's plume and skirt the Second
  tone, Hellfire's horns and tail the dark parts, its eyes and tail tip the eyes), and its colours stay listed under
  that region for an older API; a part that was drawn as it is starts as drawn. The tones weren't released, so their
  ids are free (Armored's `steel` is `armour` and `helmet` now).
- **The pairs with every part set:** Royal Parade 56, Hellfire 55, Armored 70, the Jester 78: the exact swap's limit
  for a skin over the donkey is 96 since (`PaletteMaxColoursOver`; 2.3, docs/performance.md "The colour swap's
  cost": none measured up to 96).
- **The words:** the pack's new part names in all 11 languages (a split part takes its half of the old name's reviewed
  translation; Helmet, Body armour, Tail tip, Trim and fringe, Emblem and Jewels are new), checked with
  `tools/l10n-glyphs.py`: no character the game's own text never draws but those the Fonts check found already.

### 7.11 The outline is the drawing's line (2026-11-04)

The user, on Hellfire with its dark parts coloured: "one thing that i dont like is that that outline is sharing the
same colour as the legs. is that the "mane"? I dont like that the mane is that partial outline and i think we have it
in all skins?". Measured on the game's 44 donkey frames (the probe's export, `BepInEx\GK2-donkey-frames`; a pixel is on
the silhouette's edge when a 4-neighbour is transparent):

| Dark colour | On the edge | Inside | What it is |
|---|---|---|---|
| 1e190e | 3067 | 1550 | the outline of the legs, ears and tail (inside: the lines between them) |
| 301f0f | 1619 | 1162 | the outline of the head, neck and back: the "mane" line |
| 292316 | 0 | 1972 | the fill of the legs, hooves, ears and the tail's tuft |
| 352e1e | 0 | 1620 | the same, lighter; the mane's strip at the withers |
| 130a0c, 2f2c1f | 0, 2 | 0, 1 | barely drawn |

The belly's edge is drawn in coat and muzzle colours, so the Dark row recoloured a part of the outline only: a line
round the head, the back and the legs. The same in every skin over the donkey (its own pixels show), in Hellfire's own
horns' outline (1E0E0C, under its Dark), and on the dragon, whose "Horns and spikes" row reached its whole outline
(1C060C, 81 % on the edge; its lit top edge 2E0C14, 93 %), its body's deepest shade (280810) and its wings' ribs
(260828). Already so: Armored's steel line (281B26, in no region) and the critters' outlines (their Dark, unnamed).

- **Built: the outline is the drawing's line, no colour row reaches it, on every look.** (Since 7.15 the donkey's
  outline is a part of its own with its Outline row; at Game default it is drawn as here.)
  - The donkey, so every skin over it: 1e190e and 301f0f out of `Colors.cs`'s Dark (its name there "Donkey mane,
    legs, ears and hooves"). In no region, they have no pair and are drawn as they are, on the colour cube too (each
    in a cell of its own). The donkey's pairs: 40.
  - Hellfire: its horns' outline out of its `[Colors] Dark` (`make_hellfire.py`; the pack reproduced byte for byte).
  - The dragon: in its own colours a colour named nowhere is Coat, so its outline stays its Dark (1C060C, 2E0C14 and
    its one dark pixel 1E0A0A), now named nowhere: no row, as the rat's, the snail's and the worm's. Its horns and back
    spikes (52402E 928062 D4C6A8) are a part of their own, `[Tone.horns]` "Horns and spikes" (the name and its 11
    translations moved from the region to the part); its body's deepest shade and its wings' ribs, Dark by value until
    then, are its Coat ("Scales and wings"). `colors.json`: its Dark by hex, the bone golds under Coat, which keeps
    the belly's hue range off them.
- **The babies:** each takes its own shade of the parent's dark parts (`MountCompanion.ApplyLook`, `Shade`) only where
  the look it wears names its Dark (the donkey's, its skins'); where it names none (the rat, the snail, the worm, the
  dragon), its Dark is the outline and the baby has the parent's: the shade ran on every baby until then, a small hue
  and brightness step on the line art (found by the review of this change).
- **For a skin's author** (docs/skins.md): line art in no region on a skin over the donkey; in a skin's own colours in
  a Dark it doesn't name.
- **Nothing released changes:** the Dark rows and the dragon's part names are of the same day. A Dark value set since
  reads a little lighter: its lightness is measured on the fill alone (`Colors.ArtRange`).
- **Test:** the integration scenario "colours: the outline is the drawing's line: no colour row reaches it"
  (docs/self-tests.md): the Dark keys set on the donkey's own look and on Hellfire, the outlines without a pair, the
  fill coloured; the dragon's data.
- **In game 2026-11-04 19:32: F11 1848 passed, 0 failed, 0 warnings.** The scenario: the donkey's own look and Hellfire with
  the Dark row green, 1e190e, 301f0f (and Hellfire's 1e0e0c) no pair, 292316 and 352e1e changed; the dragon's Dark its
  three outline colours, unnamed, its horns and spikes a part of their own, 280810 and 260828 its Coat. The Coat
  scenario on the dragon: Coat 9 (7 and the two), 9 changed, none of another region. The colours window (TSV and
  picture): the dragon's rows Scales and wings, Belly, Eyes and claws, Horns and spikes, no Dark row. `l10n-sync --check`
  the same on all five templates (the hand-moved key matched the export).

### 7.12 The donkey's muzzle its nose; a picked colour over a skin's pre-colour (2026-11-04)

The user, on Hellfire with Coat Cream, Muzzle and belly Green (a screenshot): "1) why is the coat appearing dark like
that when its colour is selected as Cream? 2) The muzzle and belly part, remove those individual pixels on the face
from the muzzle colours and leave it as just the nose, and also remove the belly part and leave it as just the muzzle
so that its nose is coloured".

**The muzzle.** The donkey's Muzzle region, measured on its 44 frames (the probe's export):

| Colour | Where | Now |
|---|---|---|
| abb298, 858f6c | the nose's fill | Muzzle |
| 75755a, a9a7a2 | a few pixels of the mouth, chewing (14) and grazing (3) | Muzzle |
| 362d1e | the nostril (the game's 352e1e, repainted by place since 2026-11-04: 7.14) | Muzzle |
| 858e6b | dots on the face round the eye and the cheek (369) | Coat |
| 42422c | a light line along the nose's edge, the chin, the throat and the belly's bottom (1282, 89 % on the silhouette's edge) | Coat; the Outline since the same evening (7.15) |

- The row is "Muzzle" (`UiWords.RegionDonkeyMuzzle`; each language's word the first half of its "Muzzle and belly")
  on the donkey and every skin over it. The nose's own rim is the belly's line, one colour: it followed the coat with it,
  and is the outline's since 7.15.
- 858e6b (Coat) and the nose's 858f6c are a step apart in two channels: 0.0055 in RGB, outside the exact swap's reach
  (0.004, docs/colors.md), so they are coloured apart; on the colour cube (the Lut fallback) they share a cell.
- **A released change:** a 0.15.0 player's Second tone on the donkey reached the belly's line and the face's dots too;
  those follow the coat now.

**A picked colour over a skin's pre-colour.** A skin over the donkey may recolour the donkey's region under it
(`[Base.<Region>]`: Hellfire's ash coat, brightness 0.41 with a red colorize at 0.6; the Jester's wine harness), and the
player's map went on top of it (`Colors.cs`: `map.Apply(under.Apply(src))`). Every coat colour but Dark and Black is a
colorize, which keeps a pixel's value: Cream (E0CFA8 at 0.8, brightness 1.15) on the ash stayed dark. Now the skin's
recolour is the region's look while the player gives it no colour: Game default, a shade (Dark: brightness 0.6; Black:
brightness 0.35, saturation 0.2) and a baby's own step go on top of it; a colour the player picks (a colorize: Cream,
Brown, Custom ...) replaces it and is applied to the donkey's own colours (`!(on && map.HasColorize)`). No new key: a
colour is told from a shade by its colorize. A baby of a parent with a colour picked shades that colour, one at Game
default the skin's ash.

- **Tests:** the integration scenarios "colours: the donkey's Muzzle row reaches its nose only; the face's dots and the
  belly's line are its Coat" and "colours: a colour picked for a part a skin pre-colours replaces the pre-colour"
  (docs/self-tests.md).

### 7.13 A row taken away (2026-11-04)

The user, on Hellfire's two eye rows: "whats the difference between eyes and glowing eyes? the fact that theyre not
underneath each other is weird", then "skins should have the ability to override the default skin's color fields in
that if they don't want any fields, they must have the ability to remove them". Measured: the donkey's eye white is
under Hellfire's glow (FFAA28, DC3C14) in every frame it is drawn (113 of the eye's pixels covered, the white in all
of them), so its Eyes row coloured nothing you could see; on Armored (an eye hole in the helmet), Royal Parade and the
Jester the white shows in every frame.

- **`[Region.<region>]` `row = none`** (`SkinRegions.Read`, `SkinDef.RegionsWithoutRow`; code skins
  `MountSkinDef.RegionsWithoutRow`, with `MountSkinDef.RegionNames` for the names a pack gives): the look gives the
  region no row. `SkinRegions.HasRow` walks a variant's defaults, the nearest that says deciding (a name brings a row
  back); `NameTerm` gives no name for it, `Declared` no translation.
- **The page:** `RegionRows` hides the region's rows (`SkinHasRegion && HasRow`); `ToneRows` makes no Dark or Eye row
  for it (no name).
- **The engine:** in `Colors.Rig.NamedMaps` the region gets no map (a look in its own colours: a pair to itself; over
  the donkey: no pair, as drawn), and the donkey's regions under a skin over it get only the skin's `[Base.<Region>]`.
  So a saved value stays in the player's file and isn't applied, and nothing is coloured that has no row.
- **Hellfire:** `[Region.Eye]` `row = none` (`make_hellfire.py`, the pack reproduced byte for byte). Its rows: Coat,
  Harness, Muzzle, Legs and mane, Outline (since 7.15), Horns, Glowing eyes, Lava cracks, Tail, Tail tip.
- **The checks:** SkinsScan and `check_skin.py` take `row = none` and refuse it with a name; `check_skin.py` warns when
  a skin over the donkey covers one of the donkey's regions in every frame and gives it a row.
- **Tests:** the scenario "colours: a look's names for its parts" (a temp skin without Harness and Eye rows: read, a
  variant's, the page, the engine) and the integration check "Hellfire gives the donkey's eye no row".

### 7.14 The eye's line and the nostril (2026-11-04)

The user, on the Jester with "Legs and mane" Pink: "there are some pixels around the eye which are affected by the legs
and mange group", then "yes nostril should be the same as muzzle". The game draws the eye's line and the nostril in the
dark fill's own colours (292316, 352e1e), and a colour swap can't tell them from the legs and the mane. The API
repaints them in the game's atlas by place (`FacePixels.cs`, docs/face-pixels.md): the eye's line 292319, in no
region (as drawn, the eye's own drawing), the nostril 362d1e, under Muzzle. The donkey's pairs were 41 (43 since 7.15). A skin
drawing over the donkey draws on the asset probe's frames, which keep the game's colours.

### 7.15 The outline, a part of its own (2026-11-04)

The user, on Hellfire with a white coat (a screenshot): "why is it that the donkey's outline border in hellfire changing
with the coat color? didnt we already decide that this outline border is not changeable?", then, shown the measure
below: "i think we should keep the outline border always like this ... OR even better would be if we are able to set
its color by itself as part of an individual option", and "lets keep for donkey for now and game default being game's
border but we have it as an individual option".

**Measured** (the 44 frames; a pixel on the edge has a transparent 4-neighbour): the donkey's border is 6550 pixels,
4823 of them (74 %) the drawing's line 1e190e and 301f0f (as drawn since 7.11), 1145 (17.5 %) the lighter line
**42422c** along the nose's front and bottom, the chin, the throat and the belly (the Coat's since 7.12, the
Muzzle's in 0.15.0; inside the head it is the mouth's line, 2 to 9 pixels a frame), 577 (9 %) the harness's own
darks where the collar and the girth cross it. So a white coat took the border under the belly and the nose away; every
skin over the donkey showed it (about 1000 border pixels on Royal Parade and Hellfire).

**Built:**
- **The region:** `Colors.cs` `Outline` ("Donkey outline"): 1e190e, 301f0f and 42422c (out of Coat), the sixth of
  `MountRegionKeys`. The inner lines drawn in those colours (between the legs, the mouth's line) go with it: the
  swap goes by colour. The donkey's pairs: 43 (the exact swap's 48); each colour in a colour-cube cell of its own.
- **The row:** `MountSettingsPage.ToneRows` makes it after the Dark and Eye rows (`Outline<sfx>`, `CustomOutline<sfx>`),
  where the look names the region: the donkey names it "Outline" (`UiWords.RegionOutline`, in 11 languages: Umriss,
  Contorno, Contour, ふちどり, 테두리, Kontur, Contorno, Контур, Dış çizgi, 描边; the CJK words of characters the
  game's own text draws, `tools/l10n-glyphs.py`), and a skin over the donkey takes the donkey's names. Held by
  `MountLook.Outline` (a `MountTone`, a public addition); the companion turns it into the region's colorize and
  lightness as a part's (`SkinTones.RegionValues`), on the worn donkey, the paper donkey and the babies (the parent's,
  no shade of their own). Game default is as drawn: nothing changes until a player picks a colour.
- **A skin decides:** `[Region.Outline]` `row = none` keeps its border as drawn, `name =` renames the row,
  `[Base.Outline]` gives the border a look of its own that a player's colour replaces (7.12).
- **A skin's own line art** joins with `[Region.<region>]` `colours =` (new: the skin's own colours in the region,
  read as a `[Colors]` line, the same refusals; `SkinRegions.Read`). Not `[Colors] Outline =`, which an API without the
  region refuses the whole skin for (`Looks.ColorRegionLine`); a `[Region.*]` section such an API skips (its report
  line "unknown sections ignored"), so the pack reads as before on it. Hellfire: its horns' and rump's outline
  1E0E0C; Armored: its steel's line 281B26 (both by their generators, the pictures unchanged). Armored's steel line is
  also the seams between its plates (2334 border pixels, 3759 inside), so the Outline row colours the seams too.
- **Not in it:** the eye's line (292319, as drawn: docs/face-pixels.md), the harness's own edge (the Harness row's:
  its darks are as much inside it as on the border), the other mounts (their outlines stay as drawn with no row: the
  user, "lets keep for donkey for now"), and the skins' costume parts whose edges are drawn in the part's own darkest
  shade, which is also its shading inside (Royal Parade's cloth 3a0a12, Armored's plume 381218 and skirt 38121a, the
  Jester's bells 5a3a0a, cap, collar and caparison): they follow their part's row. On the Jester the donkey's outline is
  43 % of the border (3016 of 6989 pixels); joining the rest would mean drawing those edges in a line colour of their
  own (an art change, the skin's generator).
- **The pairs with every part set:** Royal Parade 57, Hellfire 56, Armored 72 (96 the limit over the donkey).
- **The checks:** SkinsScan and `check_skin.py` take `[Region.<region>] colours =` (Coat refused, as in `[Colors]`) and
  count them with the skin's named colours; `face_pixels.py`'s colour check reads them; `part_map.py` shows the
  Outline part; the review page lists it.
- **Saved files:** the new rows start at Game default; nothing a player saved changes. On a 0.15.0 look the belly's
  line followed "Muzzle and belly"; it is the Outline's now.
- **Tests:** the integration scenario "colours: the outline is a part of its own" (the donkey and Hellfire: Legs and
  mane green, the outline kept and the fill coloured; Outline green, 1e190e, 301f0f, 42422c and Hellfire's 1E0E0C
  coloured, the fill and the coat not; Coat green, the coat coloured and 42422c not; the row named Outline), "the
  donkey's Muzzle row reaches its nose only" (the belly's line kept under both), and the regions scenario (the
  donkey's six names; `[Region.Outline] colours =` read, Coat and a colour under two regions refused).
- **In game 2026-11-04 23:31 (F11 1873 passed, 1 failed, 0 warnings):** every check of this section passed but one:
  on Hellfire with the Outline row green the test called the coat 736c57 changed. That was the test's yardstick, not
  the engine: Hellfire's coat is its ash at Game default (`[Base.Coat]`, 736C57 to 2F1A16, the same run's "a colour
  picked for a part a skin pre-colours" scenario), and the test compared with the drawn colour. Fixed: "kept" and
  "changed" are against the look with every row at Game default. The fonts of all eleven languages have every
  character of the new words; `l10n-sync --check` the same on all five templates. The run of 2026-11-05 00:10 with the
  test fixed: 1875 passed, 0 failed, 0 warnings.
