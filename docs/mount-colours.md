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
  swap; `ArtLightness` keeps the donkey's averages. Self-test "colours: a skin over the donkey keeps the donkey's
  regions".

### 2.3 Which shader path

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

### 2.6 The look is kept per mount

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
  shell and Hellfire's lava by at least 22, most by 40 and more. The worm has no second tone: its rows are
  left out of the menu and greyed in the Mounts panel.
- **Gerry:** Dark = the sockets and cracks, the bone is Coat, read from the game's export of his frames
  (`BepInEx\GK2-asset-probe\game-skins\saddleup.gerry\default\`) when one exists; without the export the cfg carries no `[Colors]`
  and everything is Coat.
- **Hellfire, Royal Parade** (over the donkey): Hellfire's written once by its generator (`make_hellfire.py`, `--cfg`
  rewrites it; the cracks and the glow as Muzzle and Eye, so the page's Second tone row recolours Hellfire's lava), Royal Parade's by hand in its cfg (the cloth's reds as Harness); a donkey colour drawn by an over skin is kept as the donkey's (its own
  settings and the skin's `[Base.*]` reach it), which is how Hellfire's tuft and rump fill continue the body.

## 3. What is not in this

- No new regions, no per-skin region names, no per-skin settings rows (the rule).
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
