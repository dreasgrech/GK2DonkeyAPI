# Mounts and skins: the foundation audit (2026-10-20)

**2026-10-26 (built in or external):** the dragon, rat, snail and worm are no longer in Saddle Up's DLL but external
packs of their own in the repo's `packs/` (`GK2SaddleUpDragonsMountPack`, `GK2SaddleUpCrittersMountPack`), ids by the pack
(`saddleup-dragon.dragon`, `saddleup-critters.rat` ...); Saddle Up keeps Gerry and the donkey's two skins (2026-10-27: the skins too are a pack now, GK2 Saddle Up: Donkey Skins, ids `donkey/saddleup-donkey-skins.<skin>`; Saddle Up keeps Gerry alone). Paths and ids
below are of their day; docs/making-a-mount.md "Built in or external" is current.

**2026-10-25 (API 0.37.0):** the `effect` / `effect_when` keys of `[Mount]` (and of a skin's `[Skin]`) discussed
below are no longer read (an old file's lines are reported as unknown keys); a mount has no default effect.

The user, after Hellfire and Royal Parade went onto one pipeline: *"take a look at the mounts and how we're defining
them so that we have the same parity between them, so that our foundation is perfect before we continue building"*,
then *"another full investigation to make sure we get mount definitions right in conjunction with their skin system
so that all folders are structured the same"*. This is that investigation: what the code does today, where the
seven built-in definitions and the tooling differ, the canonical shape they should all have, and the plan. Nothing
was changed while writing it. Line numbers are of 2026-10-19 night (commit ea0ef64 plus the uncommitted cleanup).

**Amended 2026-10-24 (the mount folders):** a mount is no longer a `[Mount]` block inside its default skin's
skin.cfg. A pack is `gk2mounts\<mount>\` with `mount.cfg` (`[Mount]` with `default_skin`, `[Stats]`, `[Tuning*]`,
`[Sounds*]`: plain names), the mount's `sounds\`, and `skins\<skin>\` for each look (skin.cfg, pictures, `front\`,
its own `sounds\`); the donkey's folder holds skins only. The "declaring skin", joiner and Default-by-id rules
below are history; docs/skins.md "What a pack is" and docs/making-a-mount.md are current (MountPacks.cs).

**Amended 2026-10-24, later (the ids):** a mount's id is `<mod>.<name>` (`saddleup.dragon`; only the game's `donkey`
is plain; one without a dot is refused). A skin is named within its mount, full name `<mount id>/<skin>`
(`saddleup.dragon/default`, `donkey/saddleup.hellfire`; skin.cfg has no `id =`). Every mount but the donkey has exactly one
default look, its `skins\default\`; `default_skin` is gone. Why: docs/mount-id-collisions.md.

**Status 2026-10-20:** the user chose the tools move (3.8, done) and asked for more investigation on the seat (3.1,
open: see docs/seat-investigation.md once written). Everything in §5 "safe" is built: the five cfgs in §4's layout
(`tools/skins/canonical_cfg.py`, checked by `tests/SkinsScan` and the release scans), Save keeps comments (3.4),
the loader and mount fixes (3.5), the scan rules (3.6, probed with a wrong cfg: 11 findings), the docs (3.7), the
tests (§5.6). Line numbers below are from before those changes.

## 1. The model, as built

| Thing | What it is | Defined by | Today |
|---|---|---|---|
| **Base** (`Looks.SkinBase`, internal) | the game view a skin is drawn on: parts, frame keys, canvas, pivot, atlas | code only | the donkey, the only one |
| **Mount** (`Catalog.Entry` / `MountDef`) | what the player rides: identity, cart and babies rules, the babies' size, a default effect, the rider's seat per style (values of existing tuning keys), abilities (code) | the API for the donkey (code, `Mounts.Bind`); a pack's `[Mount]` sections; a mod's `Mounts.Catalog.Register` | donkey, dragon, gerry, rat, snail, worm; the example's checker-beast |
| **Skin** (`Looks.SkinDef` / `MountSkinDef`) | how it is drawn: pictures for the frame keys, canvas, offsets, colour regions, and only picture-dependent tuning | a pack folder (`skin.cfg` + pictures), embedded or loose; a mod's `Register` / `RegisterRecipe` | 7 built-in packs; the game's own donkey art as the donkey's first skin |

The rules the code implies, written down once:

- **A mount has skins; the skin names its mount** (`[Skin] mount =`). A donkey skin never declares `[Mount]`. A skin of
  its own mount always declares it, with `[Mount] id` equal to its `mount`.
- **The skin whose id equals the mount's id is that mount's Default** (`Looks.DefaultSkinOf`, Looks.cs:1024); the
  donkey's Default is the game's own art. The rat, snail, worm, dragon and gerry are each the Default of their mount.
- **The mount owns the creature, the skin owns the drawing.** A skin's `[Tuning]` may set only the picture keys
  (`Looks.SkinTuningKeys`, Looks.cs:970); speed, cart, foals, effect, abilities live on the mount.
- **One built-in = one folder** `src/GK2DonkeyAPI/assets/skins/<id>/` (the five mounts: `src/SaddleUp/assets/skins/<id>/` since 2026-10-20 evening, docs/mounts-as-mods.md) with `skin.cfg` and a PNG per frame key
  (plus `front/`), embedded by the csproj (line 31), generated by a script in the repo (docs/sprite-tools.md
  "Making a skin: the one pipeline"). Gerry alone has no pictures (`source = game`).
- **A pack declares a mount once; the first declaration wins** (`Catalog.Declare`, Catalog.cs:69). No two built-ins
  declare the same mount today.

## 2. The seven definitions side by side

*A snapshot of 2026-10-20, before the parity work: superseded. Every file is in the canonical layout now (section 4,
`tools/skins/canonical_cfg.py`), the drawing's keys sit on the skin, every mount declares its facts the same way;
docs/mounts-parity-matrix.md section 11 says what was done with each finding.*

| | dragon | gerry | rat | snail | worm | hellfire | royal-parade |
|---|---|---|---|---|---|---|---|
| `[Skin]` layer / canvas | replace / 128 | replace / 160, `source = game`, `seat = picture` | replace / 96 | replace / 96 | replace / 96 | over / 96, `erase` | over / 96 |
| `[Skin] mount` | dragon | gerry | rat | snail | worm | donkey | donkey |
| `[Mount]` id, name | dragon, "Dragon" | gerry, "Gerry" | rat, "Giant Rat" | snail, "Giant Snail" | worm, "Giant Worm" | none | none |
| `[Mount]` cart / foals | **absent** | true / true | true / true | true / true | **absent** | | |
| `[Mount]` foal_scale | (0.6) | 0.33 | (0.6) | (0.6) | (0.6) | | |
| `[Mount]` author / version / effect | none | none | none | none | none | | |
| `[Mount.Tuning]` | | `SeatAdjustPx = 10` (**dead**) | | | | | |
| `[Mount.Tuning.Ride]` | | `SeatAdjustPx = -16` | | `RiderOffsetPx = -6`, `SeatOffset = 0.1` | | | |
| `[Mount.Tuning.Cart]` | `HitchGapPx = 24` | `SeatAdjustPx = -16` | `HitchGapPx = 9` | | | | |
| `[Colors]` | Dark, Muzzle, Eye | none | Dark, Muzzle, Eye | Dark, Muzzle | Dark | Muzzle, Dark, Eye | Harness |
| section order | Skin, Mount, Mount.Tuning.Cart, Colors | Skin, Game, Mount, Mount.Tuning x3 | Skin, Mount, Mount.Tuning.Cart, Colors | Skin, Mount, Mount.Tuning.Ride, Colors | Skin, Mount, Colors | Skin, Colors, Base.Coat | Skin, Colors |
| generator | `tools/creature_sprites.py` | none (`tools/gerry_*.py` measured it) | `tools/creature_sprites.py` | same | same | `tools/skins/hellfire/make_hellfire.py` | `tools/skins/royal-parade/make_royal.py` |

What differs is not design but accident: which files the Mounts tab has saved (its Save writes `cart` and `foals`
always), which seat key each author reached for, and where the `[Colors]` comment landed.

## 3. Findings, worst first

### 3.1 A mount's seat depends on the player's own switch (behaviour)

The rider's height has three paths (Mount.cs:2959-2990): `seat = picture` (the skin's picture top plus
`SeatAdjustPx`), else `AutoSeat` on (computed from the sprites plus `SeatAdjustPx`), else `SeatOffset` plus the bob,
where **`SeatAdjustPx` is ignored**. `AutoSeat` is off by default for every player (defaults.cfg). So:

- the snail's `SeatOffset = 0.1` works for everyone;
- Gerry's `SeatAdjustPx = -16` works only because of `seat = picture`;
- the example mod's checker-beast (`TuningRide SeatAdjustPx = 2`, "sits the rider two pixels higher", Plugin.cs:31)
  and the two self-tests that set `SeatAdjustPx` (SkinPack, MountFromCode) move nothing at the shipped defaults; the
  tests only check the override is set, not that the rider moved.

The Mounts tab knows (its note: "Your Riding tab has AutoSeat off (the default for everyone) ... Set the mount's
torso height here, or give its skin seat = picture"), so this is a known gap, not a choice. **It needs a decision,
with a constraint:** a default player's rider on the plain donkey must not move by one pixel (docs/compatibility.md:
changed defaults never reach existing players; the player's own Ride default is `SeatAdjustPx = -6` with `AutoSeat`
off, so "SeatAdjustPx applies in both modes" at the key level would drop every default rider 6 px). The options:

- **A. A declared seat on the skin, mode-independent** (recommended): generalise what `seat = picture` already does.
  A skin says where its back is (`seat = picture` for the picture's top row, or `seat = <row>` for a drawn back row
  on its canvas), the rider sits there in every mode, the mount's `SeatAdjustPx` fine-tunes from it, and the player's
  own keys stay untouched. Consistent with "the skin is the drawing" and with the compat rule. One seat path for
  every mount we ship; the donkey's own art keeps the player's keys as today.
- **B. Mounts declare `SeatOffset`** (world units) like the snail does, and `SeatAdjustPx` is documented as
  "with AutoSeat or seat = picture only". No code change; every creature carries a world-unit number that depends on
  its picture, which is what the pixel keys were meant to avoid.
- **C. Leave it**, document the rule, fix the example and the tests to use what works. The gap stays for mod authors.

Proof for whichever: the upgrade self-test over the 0.9.1 and 0.10.0 fixtures must show the plain donkey's seat
unchanged, and the GameCreature / MountFromPack tests must assert the rider's measured height, not just the key.

### 3.2 The declarations are written in three shapes (parity)

Dragon and worm give only id and name; rat, snail, gerry spell out `cart` and `foals`; only gerry has `foal_scale`;
none has `author`, `version`, `effect`. The parser accepts all of them (Looks.cs:562-575) and the Mounts tab's Save
writes a fixed shape (Looks.cs:806-816: id, name, author and version when set, cart and foals always, foal_scale
when not 0.6, effect with effect_when when set, then the three tuning sections). The canonical shape is in §4.

### 3.3 Gerry's seat tuning contradicts itself (data)

`[Mount.Tuning] SeatAdjustPx = 10` and `SeatAdjustPx = -16` in both style sections. A style section always overrides
the both-styles value (Looks.cs:1821-1822), so the 10 never applies. Nothing checks for a key set in both places.

### 3.4 Comments die on Save (tooling)

`SaveCfg` regenerates every section whose name starts with `mount` from the mount and drops the comment lines inside
them, and moves `[Mount]` to the end of the file (Looks.cs:766-816). The "# The skin's colours by mount region"
comment in dragon, rat, snail and worm sits textually inside `[Mount.Tuning.Cart]` / `[Mount]` and would vanish on
the first Save; the files would also be reordered. The cfgs must not be normalised by round-tripping them through
Save until Save passes comments through and keeps the order.

### 3.5 Small code gaps that the shapes above expose

- A pack mount with no `name` has `Name == null`: `Label` falls back to the id, but `Instance.Noun`
  (MountInstance.cs:79) uses `Name` and would say "donkey", and the dev spawn row (TuningWindow.Instances.cs:394)
  would throw. Latent (all five set a name). Fix: `Noun` and the spawn row use `Label`; `FromCfg` defaults Name to Id.
- `Catalog.Declare` never sets `BuiltIn`, so `MountInfo.IsBuiltIn` is false for the five built-in mounts while
  their skins say true (Catalog.cs:78, 41).
- `Catalog.Same` and `Redeclare` ignore `Author` and `Version` (Catalog.cs:86-90, 133-145); a reload keeps the old ones.
- Unknown `[Mount]` keys are not reported (every other section reports them); `foal_scale` and `effect_when` fail
  silently; `effect` is never checked against the registered effects.
- `Noun` and the Skin row's description ("How the gerry is drawn") read oddly for Gerry; a `noun` key, or the name
  "Gerry the skull", fixes the wording.
- The `offset_cart` default is decided by `text.Contains("offset_cart")` over the whole file, comments included
  (Looks.cs:529).

### 3.6 SkinsScan does not enforce the shape (tests)

It validates `[Skin]` keys, pictures, `[Tuning]` keys, `[Colors]`, and in `[Mount]` only id, name, cart and foals as
booleans, plus identical declarations across skins of one mount (tests/SkinsScan/Program.cs:250-275). It does not
check `foal_scale` (0.2-1), `effect_when` (Off, Night, Always), `effect` (a known id), author and version, a key set
in both `[Mount.Tuning]` and a style section, the canonical shape, `[Base.*]`, `erase`, `source`, canvas range, or
a `[Mount.Tuning]` without `[Mount]`. The runtime refuses or warns on several of those; the scan passes them.

### 3.7 The docs describe three generations of the system (docs)

- `effect_when = Never` in docs/skins.md:81 is not an enum value (`Off, Night, Always`, Api/MountEffects.cs:120);
  it parses to nothing and stays Always. `pivot` and `[Mount] author` / `version` are undocumented.
- docs/skins-design.md still carries the five recipes, `ISkinSource` with textures, `GK2SaddleUp.skins.*` resource
  names, "one section", a skin `foals` flag, and "Donkey" for a missing skin (lines 7, 14, 72-74, 91, 116-126,
  134-138, 148, 259, 299-313, 341, 396, 414-415).
- docs/mounts-design.md:20 lists the blue dragon and the recipes as skins, omits Royal Parade and Gerry, says a skin
  has `foals =` (:85), says "withdrawn, the mount goes" (:193, the test asserts the opposite), and claims the throwing
  ability is tested (:196, it is not).
- docs/api.md:642, :757, :786, :861 and docs/reference.md:239-249 still say "Saddle Up's Donkey rows" and list
  ghost/ember/golden/shadow; docs/mount-colours.md:83 and :162 contradict themselves on recipes and the count.
- docs/sprite-tools.md's pipeline step 2 never mentions `mount =` / `[Mount]`; step 3 overclaims what SkinsScan checks.
- The example mod's strings: "Saddle Up's Donkey settings" (:51), api.md "Looks" (:9), and the seat comment (3.1).

### 3.8 The tooling folders are not structured the same (folders)

The two donkey skins have `tools/skins/<id>/make_<id>.py`. The creatures' generator, Gerry's measurement scripts,
the colour-region writer, the review page and the PNG reader sit in `tools/`: `creature_sprites.py`,
`gerry_probe.py`, `gerry_sheet.py`, `skin_colors.py`, `skin_preview_html.py`, `pngcolors.py`. One folder per
generator under `tools/skins/` (`creatures/`, `gerry/`) with the shared scripts at `tools/skins/` is the same
structure for all seven. It is a move plus a find-and-replace over about 23 doc mentions and the self-test paths.
**Needs a yes.**

## 4. The canonical skin.cfg

Every built-in follows this layout; SkinsScan checks it; the Mounts tab's Save writes it (after 3.4 is fixed).
Comments are free text above the section they describe.

```
# <What this skin is, one or two lines; the generator that makes its pictures.>
[Skin]
format = 1
id = <folder name>
name = <shown name>
author = dreas
version = <x.y>
base = donkey
colours = own
layer = replace | over
erase = FF00FF              # over skins that take parts off the donkey only; otherwise absent
canvas = 96 96
source = game               # Gerry only, with its [Game] section right after [Skin]
seat = picture              # when the rider sits on the picture (see 3.1)
mount = <mount id>          # donkey for a donkey skin

[Game]                      # source = game only
...

# <Why these colours are these regions.>
[Colors]
Dark = ...
Muzzle = ...
Eye = ...

[Base.<Region>]             # over skins that recolour the donkey under them only
...

# <The mount this skin is the Default of; absent on a donkey skin.>
[Mount]
id = <mount id>
name = <creature name>
author = dreas
version = <x.y>
cart = true | false
foals = true | false
foal_scale = <0.2..1>       # only when not 0.6
effect = <id>               # only when the mount comes with one
effect_when = Off | Night | Always

[Mount.Tuning]              # both styles; a key appears in exactly one of the three sections
[Mount.Tuning.Ride]
[Mount.Tuning.Cart]
HitchGapPx = <px>           # cart only
```

Three rules a mount must obey, enforced by the scan: **every key in one place** (never in `[Mount.Tuning]` and a
style section); **the seat declared so it works for every player** (3.1's decision says how); **id, name, author,
version, cart, foals always present**. The `[Mount]` block stands after `[Colors]`, so the `[Colors]` comment is
never inside a mount section and Save's order equals the file's order.

The donkey, for an author reading the examples, is the same block registered in code (Catalog.cs:41):
`id = donkey, name = Donkey, author = the game, version = 1.0, cart = true, foals = true, foal_scale = 0.6`, no
tuning, no effect. docs/skins.md shows it next to the five.

## 5. The plan

**Safe, no behaviour change** (do after the go):

1. Rewrite the five creature cfgs by hand into §4's shape and order: explicit cart/foals/author/version everywhere,
   Gerry's seat value in one section, the `[Colors]` comment outside the mount sections, the stale sheet comment out
   of royal-parade, `erase = FF00FF` kept on Hellfire as the example of the key.
2. `SaveCfg` keeps comments inside mount sections and writes `[Mount]` where the file had it; a self-test row: save
   and reload a cfg with comments in every section, text identical.
3. SkinsScan: the `[Mount]` key list and values (foal_scale 0.2-1, effect_when Off/Night/Always, effect a known
   id or absent, author and version present), one place per key, the canonical shape and order, `erase` hex, `source`
   values, canvas range, `[Base.*]` regions and knobs, a `[Mount.Tuning]` without `[Mount]`.
4. Code: `Noun` and the spawn row via `Label`; `FromCfg` defaults a mount's Name to its Id; `Declare` marks a
   built-in skin's mount `BuiltIn`; `Same` and `Redeclare` carry Author and Version; unknown `[Mount]` keys, a bad
   `foal_scale` and a bad `effect_when` reported like every other section.
5. Docs: skins.md (`Off`, `pivot`, author/version, the donkey block, the Default rule, the Mount dropdown wording),
   skins-design.md and mounts-design.md status notes brought to today, api.md / reference.md / mount-colours.md
   stale lines, sprite-tools.md pipeline step 2 and 3, the example mod's strings.
6. Tests: GameCreature asserts which SeatAdjustPx applies; MountFromPack covers a key in both places (refused by the scan,
   last-wins at runtime, said in the log); the throwing ability.

**Needs the user's decision first:**

- 3.1 the seat: A, B or C. A is recommended; it is a day's work with the fixture proof.
- 3.8 the tools move under `tools/skins/`.
