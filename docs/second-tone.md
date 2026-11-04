# The "Second tone" row: what it is, and whether it still makes sense (investigation, 2026-11-04)

The user, after every donkey skin's parts became their own (docs/mount-colours.md 7.10): "what exactly the point of
the 'Second Tone' color which all skins have? ... it's not self explanatory about what it refers to since the skin
now defines every part itself, right? so lets investigate this."

## 1. What it is

"Second tone" is the player's row for one of the five **mount regions** every mount shares: Coat, Dark, **Muzzle**,
Eye, Harness (docs/mount-colours.md 2.1). The regions were the whole colour system before a skin could name parts of
its own (2026-11-03): one vocabulary, the same rows on every mount, so a mod setting `CoatColorize` works on any
mount. Muzzle was defined by its role, "a second body tone: belly, membrane, shell", and its row named for that role
(2026-10-20), because it is a different thing on each mount. The region's name in the code is the donkey's word
(the donkey's muzzle and belly).

Players have rows for three of the five: Coat colour, Harness colour, Second tone. **Dark and Eye have no rows**
(they change only through a mod, or a baby's shade of the dark parts).

## 2. What the row colours today

| Look | The Second tone row reaches |
|---|---|
| The donkey (the game's own look) | its pale muzzle and belly |
| Royal Parade, the Jester | the donkey's muzzle and belly under the skin |
| Armored | the donkey's muzzle and belly, and the Plume and the Skirt (they follow it) |
| Hellfire | the donkey's muzzle and belly, and the Lava cracks (they follow it) |
| Dragon | the golden belly |
| Rat | the pink ears, nose and tail |
| Snail | the shell |
| Worm, Gerry | nothing: the row is greyed |

## 3. Where it lives

- **The rows:** Saddle Up's "Second tone", "Custom second tone" and its lightness, per skin (`Muzzle_<skin>`,
  `CustomMuzzle_<skin>`, `CustomMuzzleLightness_<skin>`), greyed where the look has no Muzzle colours
  (`SkinHasRegion`). The words in all 11 languages.
- **Players' saved values:** released since 0.12.0 (the 0.12.0, 0.13.0 and 0.15.0 fixtures have them; 0.15.0's
  has `Muzzle = Pale`, a custom colour, `Muzzle_saddleup.gerry = Pale`); compatibility needs them kept
  (docs/compatibility.md).
- **The public API:** `MountLook.Muzzle` (`MuzzleColor`: Game, Pale, Rose, Brown, Green, Blue, Dark, Custom),
  `CustomMuzzle`, the `Mounts.Colors` keys `MuzzleColorize` and the rest, `MountSkinInfo.Regions`.
- **Skin packs:** `[Colors] Muzzle =` and a tone's `follows = Muzzle`.
- **Babies:** each baby has its own shade of the second tone (`FoalLooks.MuzzleHue`, `MuzzleBrightness`).

## 4. What is wrong with it now

1. **The name says nothing.** "Second tone" means the muzzle on one mount, the belly on another, a shell on a third;
   the player has to try it to find out. Every other row in the window now names a part.
2. **On a skin worn over the donkey it mixes two things.** The row reaches the donkey's muzzle and belly under the
   skin and, on Armored and Hellfire, the skin's own parts that follow it (the plume and the skirt, the lava). The same
   holds for Harness on Royal Parade: its Harness row colours the donkey's strap and the saddle cloth. The parts
   follow a region only so that a saved look from before the parts carries over (7.9).
3. **"As the second tone", "As the dark parts", "As the eyes"** are the first choice of a part that follows a region.
   The first is vague, and the other two point at rows that don't exist: a player can't set the dark parts or the eyes,
   so on Hellfire "As the dark parts" means "as drawn" in practice.
4. **Greyed rows** on the worm and Gerry: a row for a part the mount doesn't have.

## 5. Options

**A. Name the row per look (the label only).** Each look says what its Muzzle region is: the donkey "Muzzle and
belly", the dragon "Belly", the rat "Ears, nose and tail", the snail "Shell"; a look that says nothing keeps "Second
tone"; a look without the region shows no row instead of a greyed one. Same keys, same saved values, same presets,
same API, same babies. Small. It fixes 1 and 4, not 2 and 3: Armored's plume would read "As the muzzle and belly".

**B. A, and the following parts become ordinary parts** (recommended). On the first load after the update, a part
that follows a region takes the player's saved value of that region as its own (a Custom colour of the same hex and
lightness where the presets differ), once; from then on it is a part like any other, with no "As the ..." choice.
The region row then reaches only what it names: on an over skin the donkey's own muzzle and belly under it, and
Royal Parade's Harness row only the donkey's harness (what the user first asked for, 2026-11-03: "harness color
should only control the color of the harness"). A look the player never coloured converts to as drawn, which is
what it shows now. Fixes 1 to 4. The cost: a one-time conversion (with a "done" mark per skin, and the self-test and
fixture runs that prove it on the released 0.15.0 files), and the presets' small differences: a converted preset
may come out a shade off (the user accepted "as much as possible but not critical"). The API keeps `Muzzle` and
`follows =` for mods and older packs; only the window stops showing them as "As the ...".

**C. No region rows at all:** every look's parts as tones, the donkey's own too ("Muzzle and belly", "Mane and
hooves", "Eyes"). The most uniform; the largest change (every mount's cfg, the donkey's own look in code, the saved
Coat, Harness and Second tone values all converted). The donkey's mane, hooves and outline share their colours in
the game's art, so they would be one part. The Coat and Harness rows are clear as they are, so this buys little
over B.

## 6. Recommendation

B. "Second tone" goes from the window: each look's row names its part, and no part of a skin follows another row
any more. Saved looks carry over as closely as the presets allow. Decisions for the user:

1. B (or A, or C)?
2. The names per look: "Muzzle and belly" (the donkey, and under the over skins), "Belly" (dragon), "Ears, nose and
   tail" (rat), "Shell" (snail)?
3. The same treatment for the Coat row's label on mounts that have no coat (the snail's body, the worm's skin), or
   keep "Coat colour" everywhere?

## 7. The user's pick, and the data for the Coat row (the same day)

The user: "I think we should go with C since that seems like the most complete option. What do you think? Regarding
#3, i dont know, need more data".

**C's goal, by named regions rather than regions turned into tones.** C as written converts every player's saved
Coat, Harness and Second tone into a tone's colour. Read in the code (`MountCompanion.CoatPreset` and the others), the
region presets are not all colours: the coat's Dark is brightness 0.6 only, its Black brightness 0.35 and saturation
0.2, Brown and Cream a colorize at 0.85 and 0.8, the harness's Black brightness and saturation. A tone's value is
always a whole colorize (its hue and saturation replaced) with a lightness, and its choices are the tone presets, so
those saved looks would change and have no row choice that shows them. The same result for the player comes from
naming the regions per look: the five regions stay the engine's vocabulary, the saved keys and the API (mods keep
`MountLook.Coat`); each look names the parts its regions are (the rows show those names, a region the look doesn't
name or have shows no row); Dark and Eye get rows; a part that follows a region becomes an ordinary part (B's
one-time conversion, for the few that follow one). The tuned presets of the three old rows and the babies' own shades
of the coat, the second tone and the dark parts stay as they are.

**What each region covers on each mount's own look** (`tools/skins/part_map.py`, every colour of the region one
colour; the donkey's from its region table over its 44 frames):

| Look | Coat | Dark | Muzzle | Eye |
|---|---|---|---|---|
| Donkey (and under every skin worn over it) | the body | the lower legs, ears, mane, tail and outline (one set of colours in the game's art) | the muzzle and a belly line | the eye (2 px) |
| Dragon | the scales and the wings | the outline, horns, back spikes, wing ribs | a belly strip | the eyes and the claws |
| Rat | the grey fur | the outline | the ears, nose, paws and tail | the eye |
| Snail | its body | the outline and the eye stalks' stripes | the shell | none |
| Worm | its skin | the outline and the eyes | none | none |
| Gerry | everything (his cfg names no region: the game's export of his frames isn't on this machine) | none | none | none |

So "Coat colour" fits the furry mounts (the donkey, the rat, every skin over the donkey) and not the others: the
dragon's scales and wings, the snail's body, the worm's skin, Gerry's bone. With the regions named per look the
Coat row takes the look's word too.

## 8. Decided: every row a named part, by named regions (the user, 2026-11-04: "ok lets begin")

**What the player sees.** Every row of the colours window is a part of the look, by its name: the look's own names
for its regions (Coat, Muzzle, Dark, Eye, Harness), then its tones. No row says "Coat colour" or "Second tone" (but
a look that names nothing, an older pack's: its rows keep the page's words). A region the look doesn't have has no
row (it was greyed). Dark and Eye get rows where the look names them (they had none). No part's first choice is "As
the ..." any more.

**The design.**

- **skin.cfg `[Region.<region>]`, `name =`:** a look names a region's part (`[Region.Muzzle]` / `name = Belly`); the
  name is translated in the skin owner's file as `skin.<skin id>.region.<region>.name` (as a tone's). A variant takes
  its default's names; a skin over the donkey takes the donkey's (below) unless it names its own. The donkey's own
  look names its five in the API's words: Coat, "Legs, mane and ears" (its outline shares their colours in the
  game's art), "Muzzle and belly", Eyes, Harness.
- **The three rows a page already has** (Coat, Harness, the second tone): `MountSettingsPage.RegionRows(section,
  skinId, region, choiceKey, customKey)`, as `CartRow` is: called after the rows, it labels them by the look's name
  (the custom and lightness rows in the API's templates, as a tone's), and leaves them out while the look has no
  colour of the region. Keys, saved values, presets and the babies' shades unchanged.
- **Dark and Eye rows:** built by `ToneRows` before the tones, where the look names the region: the tone presets
  (Game default as drawn, Red ... Custom with its lightness), under the page's keys `Dark<sfx>`, `CustomDark<sfx>`,
  `Eye<sfx>`, `CustomEye<sfx>`; `MountLook.Dark` and `MountLook.Eye` (`MountTone`s) hold them; the companion turns
  them into the region's colorize and lightness as a tone's (the babies: the dark parts' own shade as before).
- **A part that followed a region** (`follows =`): converted once. When its row's key is in no saved file (the first
  load after the update; tones were never released), its value is taken from the player's saved value of that
  region: a Custom colour as it was (its palette colour and lightness), a preset as the tone preset nearest it
  (Harness Black, Red, Blue, Green: the same words; the second tone's Pale White, Rose Pink, Brown, Green, Blue, Dark
  Black). From then on it is a part like any other: no "As the ..." choice, and the engine no longer pairs it with
  the region's map (at Game default it is as drawn, which a region at Game default was). `follows =` stays in the
  format with that meaning ("it was under this region"), and so does the `[Colors]` listing for an older API.
- **The packs' regions refined** where the art allows (the Critters pack is unreleased; Dark and Eye had no player
  rows, so no saved value moves): the snail's eyes (1E1A16) and the worm's eyes (18080C) from Dark to Eye, the worm's
  mouth a Muzzle of its own ("Mouth"; drawn 3C121A, one colour-cube cell with the outline's 36141C, so moved a step in
  red to 42121A: the worm is always on the cube, 720 colours, and the shared cell would have taken the outline's 5896
  pixels with a Mouth colour; the 62 mouth pixels only changed, the generator draws it so). Outlines alone (the rat's Dark, the snail's and the worm's line art)
  get no name, so no row.

**The names:**

| Look | Coat | Muzzle | Dark | Eye | Harness |
|---|---|---|---|---|---|
| Donkey; Royal Parade, Armored, the Jester (the donkey's) | Coat | Muzzle (its nose, since the same evening) | Legs, mane and ears | Eyes | Harness |
| Hellfire | Coat | Muzzle | Legs and mane (its ears are horns) | Eyes | Harness |
| Dragon | Scales and wings | Belly | (outline: no row; its horns and spikes a part of their own since the same evening) | Eyes and claws | - |
| Rat | Fur | Ears, nose, paws and tail | (outline: no row) | Eyes | - |
| Snail | Body | Shell | (outline: no row) | Eyes | - |
| Worm | Skin | Mouth | (outline: no row) | Eyes | - |

The same evening (docs/mount-colours.md 7.11): the outline is never a row's. The donkey's Dark row colours its
mane, ears, legs and hooves, not its outline (on every skin over it too); the dragon's horns and spikes are a part of
their own ("Horns and spikes"), its outline its Dark with no row. Later (7.12): the donkey's Muzzle row is its nose,
"Muzzle", the face's dots and the belly's line its Coat. Later still (7.15): the donkey's outline is a part of its own,
"Outline" (its line, the belly's line with it), on the donkey and every skin over it; the other mounts' outlines no row.
| Gerry | Bone | - | - | - | - |

## 9. Built (2026-11-04, the evening)

As section 8 says, with these facts found on the way:

- **The engine already had every knob:** the five regions' settings are made per region (`DarkColorize`,
  `EyeBrightness` ...), and `ColorsPreview.ArtRange` (the art's average and lightest per region) serves a Dark or Eye
  row as it serves a part.
- **The carry-over's guard** is the page's own snapshot of the player's file (read at its first bind, before any of
  this session's rows bind and save), so "none of the part's rows in the file" means "the first load after the
  update". Proven by the scenario "colours: a look's names for its parts" on three pages over one file.
- **The babies** take their dark shade from the parent's dark colour now (it started from nothing: the dark parts had
  no row).
- **Hellfire's and the Jester's dark parts** are "Legs and mane": their ears are under the horns and the hood.
- **The snail's and the worm's eyes, and the worm's mouth,** had colours of their own a few value steps from the
  outline (each in a cube cell of its own, the worm's mouth once moved: above): `colors.json` names them by hex (a rule key `skin_colors.py` takes since; its keys were the mounts' old ids,
  so no re-run had matched since the packs were renamed).
- **Translations:** every new name in 11 languages; 11 CJK words reworded to characters the game's own text draws
  (`tools/l10n-glyphs.py`): the snail's shell から / 背甲 / 등딱지, the dragon's wings つばさ / 翼, its claws つめ / 脚, the
  belly 肚子, the mane 长毛 and the legs 足, and so on.
- **Not changed:** the public names `MountLook.Coat`, `Harness`, `Muzzle`, the presets of those three rows, the keys
  and the saved values; `follows =` keeps its place in the format ("it was under this region").
