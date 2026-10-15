# Cargo coverage: how far the layout is tuned, over every load

Built 2026-10-12 (API 0.28.0, dev tools only). Code: `src/GK2DonkeyAPI/CargoCoverage.cs` (the analysis, scene-free),
`src/GK2DonkeyAPI/Dev/CargoSheet.Coverage.cs` (the pages and photo runs), the Cargo tab's status line
(`Dev/TuningWindow.Cargo.cs`), the Debug tab's buttons. Tested offline in `tests/PackerScan`.

## Why

With up to six items on the cart, the loads are too many to judge one by one. The packer sorts the items, so the
order they were loaded in never changes how a load looks: a load is a multiset of size classes. With the game's 7
classes:

| Items | 1 | 2 | 3 | 4 | 5 | 6 | all |
|---|---|---|---|---|---|---|---|
| Loads | 7 | 28 | 84 | 210 | 462 | 924 | 1,715 |

But every load is built from **contacts**, and each contact is one tuning in the Cargo tab, used by every load it
occurs in:

- `x on y`: an item (and its layer) on the layer below; y is the support, the layer below's first item.
- `x beside y`: two items in one layer (across, or end to end).
- `x alone on a pair`: an item lying alone on top of a pair: over the middle of the two below, plus its class's
  `onpair_*` offset.
- `x alone`: the only item on the cart (`alone_*`).

So the work is the contacts (a few dozen), not the loads. The tools count them.

## The analysis (CargoCoverage.Analyse)

Every load of 1 to 6 items, one item per class (`CargoSheet.ClassReps()`, the sheet's representatives plus the zombie),
goes through `CargoPacker.Pack` with the layout file and the fallback settings as they are now. The contacts of each
pack are collected with the Cargo tab's own keys (`on:a+b`, `beside:a+b` with the classes sorted, `onpair:c`,
`alone:c`; the tab's "alone on top of a pair" condition: alone in its layer, a pair below). Per contact: tuned or
still on the fallbacks (the pair entry's `TopTuned` / `BesideTuned`, the item entry's `onpair` / `alone`), how many
loads use it, the smallest load that shows it, and for an on-top contact the smallest load where it rests on a pair
(the shelf is the pair's top then, so it can look different with the same tuning).

Then:

- **The covering set**: the fewest loads that together show every contact (greedy: the load adding the most contacts
  not shown yet, ties to fewer items). On the six test classes: 9 loads for 37 contacts.
- **Tune next**: the untuned contacts in the order to tune them: *ready* ones first (a load exists where it is the
  only untuned contact, `SmallestReady`; Load puts that load on the cart, so each step tunes one thing on top of tuned
  ones), then up the ladder alone, beside, on, alone on a pair (effects run upward only: a beside relation moves every
  layer above it, an on relation its layer and above, an alone-on-a-pair offset one item, alone nothing else), then the
  most used; with how many loads each one *completes* (loads where it is the last untuned contact). The Cargo tab
  shows the ladder over "Tune this load" with this load's kinds lit, and a box whose load still has an untuned
  contact lower on the ladder says "tune first: ...".
- **Loads fully tuned**: loads with no untuned contact, overall and per size.
- **Tunings no load uses**: a tuned on-top or beside relation of a pair whose classes never meet that way, an onpair
  offset on a class that never lies alone on a pair (a zombie: everything under a zombie is a zombie, and zombies
  never pair), an entry matching no carryable class. Harmless, but changing them shows nothing.

It is exact about **which** contacts occur. The packer is fed the classes' boxes (`CargoLayout.BoxOf`), the same the
fit test uses; where items end up also depends on the boxes measured on the cart, which only the pictures show. The
photo runs check every picture: the contacts the cart really built must be the ones predicted, and any difference is
listed under "Notes from the run".

Not covered: the chests (a chest on the cart is the portable chest, a class of its own).

## Where to see it

- **Cargo tab, top line** (always): "Whole layout: 12 of 40 contacts tuned, 300 of 1,715 loads fully tuned. Next:
  wood on box (in 154 loads; load wood + box)". Every box in "Tune this load" says how many loads its contact is used
  in. Recomputed when the layout or a fallback changes, never while a control is held, and only in a Layout event.
- **Debug tab, "Cargo coverage"**, writing into the sheet folder (`tools/cargo-sheet/out` on the machine with the
  sources, else `BepInEx/GK2-cargo-sheet`; each run clears only its own old files):
  - *Tuning status, instant* (`status.html`, `status.txt`): the analysis, no pictures, no ride needed.
  - *Photograph the coverage set* (`coverage.html`, `coverage.txt`, `cov__*.png`): the same, with pictures: every
    contact in its smallest load with its two items tinted (blue the lower / first item, orange the one on top /
    second, everything else untinted), an on-top contact also resting on a pair, every class alone, and the covering
    loads in the Cargo tab's colours. Roughly 100 pictures.
  - *Photograph every load* (`all.html`, `all__*.jpg`): all 1,715 loads in the Cargo tab's colours, each with its
    contacts as green (tuned) / red (fallbacks) chips; filters: number of items, contains a class, contains a
    contact, only loads with an untuned contact; number of columns (the grid fills the window). The page keeps
    its filters, columns, the folded legend and the scroll position in the browser (local storage), so a refresh
    after a new run shows the same view. A few minutes.

The photo runs need Cart style and an empty cart (like the pair sheet), raise the cart to six slots for the run, make
up to six stand-in zombies (removed again at the end; loads needing more than were made are left out and noted),
clear the panel's Alone isolation and hide filters first, size each crop to the stack's drawn bounds, and put
everything back.

## Readable class names

The pages and the Cargo tab name classes by what they are, not by the representative item the sheet happens to pick
(`CargoSheet.ClassLabel`, from a member the class holds): log (wood), flour sack (box_flour), tall crate (box_beer,
box_clothes_1/2, box_fabric, box_vine), low crate (the 16 low boxes, box_appliances_1 first), slab (stone, iron ore,
marble), corpse (body_corpse, body_wild_zombie), zombie. The pages open with a table of every class, its box and its
items. Loads read "2 × log + corpse".

## Test loads (Dev/TestLoads.cs)

Real items put straight onto the ridden cart, so a contact can be tuned without finding its items in the world: a
corpse from the delivery script's body definition (body_0_1, organs included), a zombie with the game's own zombie
drop and linked like any carried zombie, everything else from its id. They are real cargo: set down they are normal
world items, and a save keeps them.

- **Cargo tab**, under the whole-layout line: *Tune next* (the five most used untuned contacts, each with **Load**,
  which puts its smallest load on the cart), a stepper through the covering loads (`<` `>` **Load**), and **Take the
  test load off**.
- **Debug tab, Cargo tools, Test loads on the cart**: any load by how many of each class (up to six), **Put these on
  the cart**, **Take the test load off**.

Safety: a test load only ever replaces a test load. While the cart holds anything not made here, Load is refused
("set it down first"), so no one's real cargo is ever removed. Taking it off removes only the test items still on
this cart and then the test zombies that are nowhere else; items or zombies already set down are left alone. The cart
gets room for the whole load whatever `CartSlots` says (`Cargo.SlotFloor`), back to the setting when the test load is
taken off. Tracking lasts for the session: after a restart, test items left on the cart are ordinary cargo.

## The Debug tab

Regrouped (2026-10-12) into four groups, one shown at a time, with the explanations hidden unless *explanations* is
ticked: **Cargo tools** (tuning status and coverage, test loads, pair and shape pictures, drop an item), **Panel &
levels**, **Logs & checks** (value and geometry lines, ground probe, keeper moves, held item), **Investigations**
(lantern, game donkey, cart cargo, chest probe).

## How to use it

1. Cargo tab: read *Tune next* (or Debug tab, *Tuning status*, for the whole list).
2. Press **Load** on a row: its smallest load goes on the cart. Tune its box (the box says "in N loads"), then the
   next row's Load replaces the test load.
3. Repeat. The Cargo tab's top line counts up as you go; nothing needs re-running for the numbers.
4. Now and then, *Photograph the coverage set* and look at the covering loads: they are every contact of every load,
   in a couple of dozen pictures.
5. Before calling it done: *Photograph every load* and filter by class or contact to look for anything odd.
6. Then look at every load for real, one by one, with the Cargo tab's **review strip** ([cargo-review.md](cargo-review.md)):
   step, judge, mark; the marks are kept in `cargo-review.cfg` and an ok is tied to the load's tuning, so a later edit
   flags the loads it touched for a re-check.

## Tests

PackerScan (offline): the analysis visits every multiset (923 for six classes up to six items); each load's contacts
are the packer's with the tab's keys; every contact's smallest load shows it and no smaller load does; load counts
are right; the covering loads show every contact; tuned states match the layout; the planted dead tuning (a zombie's
onpair offset) is reported; deterministic; a class cap (the stand-in zombies) is kept.
`dotnet run -c Release -- --coverage` prints the text report.

## Build log

- 2026-10-12: built. Fixed on the way: the pair sheet's cleanup deleted every file that was not a shapes file (it would
  have removed the coverage pictures); files are now sorted by run (`KindOfFile`). The pair grid's zombie fallback
  could have created a zombie item without its world data if only one stand-in was made; `AddCell` now refuses and
  the grid needs both stand-ins.
