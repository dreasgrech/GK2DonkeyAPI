# The rider's seat on a mount: investigation (2026-10-20)

Audit finding 3.1 (docs/mounts-foundation-audit.md): a mount's `SeatAdjustPx` only acts when the player has
`AutoSeat` on or the skin says `seat = picture`. The user: *"I don't know about #1, needs more investigation."*
This is the investigation: how the seat is computed today, what each built-in gets under the shipped defaults,
what a mount author can and cannot express, the options with their costs, and a readout self-test so the numbers
come from the game and not from this text. Nothing of the seat code was changed.

## 1. How the seat is computed (Mount.cs `TickSeat`, 2959-2990)

The rider's torso root is lifted by `seat` world units above its rest place, every frame while riding. Three paths,
the first that applies wins:

| Path | When | `seat` = | `SeatAdjustPx` |
|---|---|---|---|
| **Picture** | the worn skin says `seat = picture` (Gerry) | the skin picture's top row at the pivot column, this frame's, minus the keeper's hip (7 px above his feet) | added |
| **Auto** | the player's `AutoSeat` is on | the donkey body sprite's back line (row 25 from the sprite's bottom, plus the walk bob) minus the hip | added |
| **Manual** | otherwise, **the default for every player** | `SeatOffset` plus the bob | **ignored** |

`BackSeatExtraPx` (armour, facing up) is added after all three. One art pixel along the seat's axis is
`PxWorldY = 0.02 * 1.6667 = 0.0333` world units (Mount.cs:2121).

The Auto path measures the **donkey's** body sprite (`donkeyBodyRenderer`, ComputeAutoSeat 2780-2792), not the
skin's picture: on a replace skin the hidden donkey's back is what the rider sits on. The creatures were drawn to
the donkey's measures (back at row 41, ground at 64, make_creatures.py), so this is consistent for them; a skin
drawn to other measures (a taller dragon) gets the donkey's back under AutoSeat and needs its own seat value.

## 2. The shipped defaults (src/GK2DonkeyAPI/defaults.cfg; the fixtures tests/fixtures/released/0.10.0 agree)

| Key | Ride | Cart | In pixels |
|---|---|---|---|
| `AutoSeat` | false | false | |
| `SeatOffset` | 0.22 | 0.22 | 6.6 px up from the rest place |
| `SeatAdjustPx` | -6 | 0 | only with AutoSeat |
| `RiderOffsetPx` | -6 | -8 | along the body |

docs/reference.md's table says `SeatOffset` 0.32 / 0 and `RiderOffsetPx` -4 / 0: those are the code's fallbacks
in Plugin.cs, not what ships (defaults.cfg overrides them); fixed in this round.

So a default player rides every mount with the Manual path: `seat = 0.22` plus the bob, whatever the mount says in
`SeatAdjustPx`. What the mount can change on that path is `SeatOffset` (world units) and the sideways keys.

## 3. What each built-in gets today

From the cfgs (all in docs/mounts-foundation-audit.md §2) and the paths above. "Default player" = AutoSeat off.

| Mount | Its seat declaration | Default player | AutoSeat on |
|---|---|---|---|
| donkey | none (the base) | 0.22 = 6.6 px | the donkey's back + the player's -6 px |
| hellfire, royal-parade (donkey skins) | none | as the donkey | as the donkey |
| rat, worm | none | as the donkey: drawn to its measures, so the rider sits right | the donkey's back + the player's -6 |
| dragon | none for the seat (`HitchGapPx` only) | as the donkey | the donkey's back; the dragon's own back is not measured |
| snail | `[Mount.Tuning.Ride] SeatOffset = 0.1, RiderOffsetPx = -6` | **works**: 0.1 = 3 px, lower on the shell | **`SeatOffset` is ignored on the Auto path**: the donkey's back + the player's -6; the snail's intent is lost the other way round |
| gerry | `seat = picture`, `[Mount.Tuning] SeatAdjustPx = -16` | **works** (the Picture path ignores the switch) | the same |
| checker-beast (the example mod) | `TuningRide SeatAdjustPx = 2` | **does nothing** | +2 px |

The two self-tests that set `SeatAdjustPx` on a kind (KindPack `[Mount.Tuning.Cart] SeatAdjustPx = 7`, KindAbility
`TuningRide SeatAdjustPx = 4f`) assert the override is on the wearer, not that the rider moved: they pass whatever
the seat does.

**Measured in the game (2026-10-20, the readout self-test, 16 of 16 rides; the user's own cfg has the shipped
defaults):**

| Look | AutoSeat off (every player) | AutoSeat on | What made it |
|---|---|---|---|
| donkey, hellfire, royal-parade | 6.6 px (0.221) | 11.5 px (0.385) | the player's `SeatOffset` 0.22; the donkey's back + the player's -6 |
| rat, worm, dragon | 6.6 px | 11.5 px | the same: no seat of their own |
| snail | **3.0 px** (0.101) | 11.5 px | `SeatOffset = 0.1` (the mount); **lost with AutoSeat on** |
| gerry | **25.5 px** (0.852) | 25.5 px | `seat = picture` + `SeatAdjustPx = -16` (the mount): the same in both |

Two things the table says that the code reading alone did not:

- **The player's own two modes already disagree by 5 pixels on the plain donkey** (6.6 against 11.5). AutoSeat
  on was never tuned to match the manual seat; the shipped manual 0.22 is the baseline every player sees. Any
  mode-independent seat for a mount must be measured against 6.6 px, not against the auto path.
- **Only the picture seat is the same for everyone.** The snail is the one mount that declared a seat the normal
  way, and its intent (3 px, down on the shell) holds for a default player and vanishes for an AutoSeat player.
  The rat, worm and dragon sit at the donkey's 6.6 px because they were drawn to the donkey's back, which is fine
  for the first two by design; whether 6.6 px is right on the dragon is a look judgement, not a seat-code one.

The readout toggles the player's own `AutoSeat` for the run and puts it back; the lines are `[test] INFO` in
`BepInEx\LogOutput.log`.

## 4. What this means for a mount author

- A mount can set the rider's height for every player today in one way: `SeatOffset` in `[Mount.Tuning]` (or per
  style), in world units; `RiderOffsetPx` and `TorsoOffsetPx` work in every mode. The snail does it right.
- `SeatAdjustPx` on a mount is documented as "with AutoSeat" (reference.md) and that is what it does. It is the
  wrong key for a mount unless the skin says `seat = picture`. The example mod and docs/mounts-design.md §5 show it
  as the way to set a mount's seat; that is the misleading part, not the code.
- A mount that sets `SeatOffset` loses it for the players who turned AutoSeat on (the snail): the Auto path never
  reads `SeatOffset`. So no single key works in both modes today; `seat = picture` is the one mode-independent seat.

## 5. The constraint any change must meet

docs/compatibility.md: a player's rider on the plain donkey must not move. Every default player has AutoSeat off,
`SeatOffset = 0.22`, `SeatAdjustPx = -6` in their own cfg. Applying `SeatAdjustPx` on the Manual path at the key
level would drop every default rider 6 px (0.22 - 0.2 = 0.02 world), so that is out. Whatever is built is proven
by the readout above on the plain donkey before and after (the same number to the pixel) and by the upgrade
self-test over the 0.9.1 and 0.10.0 fixtures.

## 6. The options

**A. A declared back row on the skin** (the Picture path generalised). `seat = picture` already seats the rider on
the skin's own drawing in every mode. A skin could instead say `seat = <row>` (the row of its canvas the rider's
hip sits on) or keep `seat = picture`; the mount's `SeatAdjustPx` fine-tunes from there; the donkey's own art has
no `seat` and keeps the player's paths untouched. Mode-independent, pixel-exact, "the skin is the drawing", the
compat rule holds by construction (nothing changes for a skin without `seat`). Cost: a new `seat` value and its
parse, `TrySeatTop` for a fixed row, the Mounts tab showing it, docs, a self-test; the creature cfgs get a `seat`
line each (their back row is 41 on the 96 canvas, the dragon's and Gerry's measured). About a day.

**B. Mounts use `SeatOffset`** (what the snail does), `SeatAdjustPx` documented as "AutoSeat or picture only", the
example mod and the two tests corrected. No code change; the mount's seat is in world units that depend on its
picture, and it still breaks for AutoSeat-on players (§4, third point).

**C. Leave the code**, fix the example and the docs. The gap stays for every mod author who reaches for the pixel
key, as we did.

**D. A value set on the instance applies in every mode.** `Tuned<T>.Value` already reads the ridden instance's
override (Tuned.cs:53-64) and the instance knows who set it (`OverrideOwners`). The Manual path could add
`SeatAdjustPx` only when it is an override on this instance (set by the mount or a mod), never the player's own
cfg value. The default player is untouched (no override); `SeatAdjustPx` from a mount or a mod then means "so many
pixels up from wherever the seat is" in both modes. Cost: one condition in `TickSeat`, a self-test. The catch: the
same key means two things depending on who set it, which is the kind of rule that has to be explained in three
docs. B's AutoSeat-on hole stays for `SeatOffset`.

**Recommendation, with the numbers in (§3):** A, because it is the only option where one declaration gives
the same seat to every player, and it reuses a path that exists and is tested (Gerry). The readout shows the rat, worm and dragon at the donkey's own seat in both modes (right by
construction for the first two), so A changes nothing for them unless they declare a row; the snail and the
example mod are the two that need it, and Gerry already has it.

## 7. What was done in this round

- The readout self-test and `Mount.Instance.TestSeatApplied` (dev only).
- docs/reference.md's seat defaults corrected to the shipped values.
- Nothing else: the seat code, the cfgs' seat values and the example are as they were.
