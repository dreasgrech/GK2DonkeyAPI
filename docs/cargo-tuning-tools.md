# Cargo tuning tools: investigation and plan

2026-10-12. Follows [cargo-coverage.md](cargo-coverage.md) (contacts, status pages, test loads). The question: what
else can make tuning 49 contacts across 1,715 loads faster and safer. Each idea below was checked against the code and
the game before planning it.

## Findings

### 1. A tuned on-top contact ignores a taller neighbour (packer model)

`CargoPacker.Pack`, on-top branch: an untuned layer sits on the **shelf** (the highest point of the whole layer below,
less the nestle); a tuned one is placed from its **support** alone (`origin = support + (b - a)`), the layer below's
first item. A contact tuned where the support stands alone (its smallest load is often just the two items) is then
applied unchanged when the support is half of a pair; if the other half is taller, the upper item is pushed into it.
The coverage analysis lists 20 on-top contacts that occur both on a single support and on a pair, for example slab on
low crate, slab on corpse, flour sack on low crate and low crate on corpse.

**Fix (no file change):** a tuned layer rises by how much the shelf stands above its support's top
(`max(0, shelf - support top)`). On a single support, or a pair of equal height, that is 0, so every load already
looked at stays as it is. The panel shows and writes the stored relation (the lift is taken off what it shows), so
the Tune loop stays a fixed point (PackerScan).

### 2. Boxes are too crude to judge a load; the meshes are needed

`PackerScan --real` (new) runs the repo's layout file with the game's seven size classes offline. Its box-level
problem scan reports 1,458 of 1,715 loads with an item "cutting into" one below, almost all of it false: a log's box
is 0.57 high but the log is round, so logs nest in the groove of two below (reported as 0.40 into it); the flour
sack's own correction lowers it 0.22 below its box top because its mesh is not box-shaped. So an automatic judge (and
an automatic "seat it here") needs the real surfaces.

**The meshes cannot be read from the CPU:** all 47 cargo meshes report `isReadable = false` (`GK2-carry-facts.txt`).
Two routes, both to probe in game:

- **GPU readback.** The game runs Unity 6 (6000.3.9): `Mesh.GetVertexBuffer` / `GetIndexBuffer` hand back the GPU
  buffers of non-readable meshes, and `GraphicsBuffer.GetData` copies them back. Exact triangles if it works.
- **Rendering.** The mod already photographs a copy of the donkey far below the world with its own camera into a
  RenderTexture (`PreviewStage`). Each class's mesh can be photographed straight down and straight up in thin depth
  slices (the camera's clip planes), giving a height map of its top and bottom surface on a 1 cm grid. Slower, but
  works whatever the buffers allow.

Either way the result is measured **once** and saved to the repo (`cargo-shapes.cfg`: each class's top and bottom
height maps), so it is used offline as well (PackerScan, the status page) and the game never measures again.

With the height maps:

- **Seat it**: for an on-top contact, the height at which the upper item's underside touches the layer below's top
  (plus a small sink), and for a beside contact, the spacing at which the two just touch. A button per contact box and
  "seat every untuned contact" as a starting point. Tuning becomes the sideways nudges.
- **The problem scan, meshes**: every load scored for items cutting into each other, floating, and past the bed; the
  status page lists the worst loads first, with their pictures in the coverage run.
- **Mesh-aware fallbacks** (an option): untuned contacts seated on the meshes instead of the boxes, so most look right
  before they are tuned.

### 3. "Looks right": accepting a contact as it is

A contact counts as tuned only after a slider moves. Accepting it as it is uses the existing freezes
(`FreezeOnTop`, `FreezeBeside`, an item's `HasAlone`). The one gap is "alone on a pair": tuned means a non-zero
`onpair` offset, so accepting it at zero needs a flag (`HasOnPair`, written like `alone_*`: all three keys, zeros
included).

### 4. Keyboard nudges

The game reads its keys through its own binding asset (`LazyInput`, `KeyboardController`), not fixed key codes, so a
nudge key could also move the keeper. The numpad is the safe choice (the mod's tuning keys already read raw keys
through `UnityInput.Current`); nudges only while the Cargo tab is open and a box is selected.

### 5. Close-up camera

`CameraSystem.SetOrthographicSize` zooms (it sets the Cinemachine lenses); `OnResolutionChanged` puts it back. The user
already plays with Grave Zoom at 2x, which owns the same lens, so a second zoom would fight it. Not planned; Grave Zoom
does the job.

### 6. Undo and "before today"

`CargoLayout.Save` already writes the whole layout as text, and `Parse` reads it. Undo is a stack of those texts, one
per finished edit (on mouse release, not every dragged frame); restoring is `Parse` plus a redraw. The text at session
start allows "compare with how it was" (a toggle on the cart) and "this contact back to how it was".

### 7. What stays as is

Copy from a similar contact and the before/after picture diff are cheap once the above exists; a rolling / slope
check is low value next to the rest.

## Plan

**Phase A (no probe needed):**

1. The shelf lift (finding 1), with PackerScan.
2. "Looks right" per contact box, "everything in this load looks right", and **Next**: the next untuned contact's
   load onto the cart. `HasOnPair` for the alone-on-a-pair case.
3. Undo / redo, and the session-start compare and revert.
4. Numpad nudges for the selected box.

**Phase B (one in-game probe):** measure the class shapes (GPU readback first, rendering if it fails) into
`cargo-shapes.cfg`.

**Phase C (on B):** Seat it / seat all, the mesh problem scan on the status page, mesh-aware fallbacks as an option.

## Build log

- 2026-10-12: investigation written; `PackerScan --real` added (the real layout and classes offline: the coverage
  report as the game shows it, and the box-level scan).
- 2026-10-12, phase A built (not yet seen in game):
  - **Shelf lift** in `CargoPacker` (`Result.Lift`, taken off in `Rel`). PackerScan: a box on a body tuned on a lone
    body sits as high above a taller log beside the body as above the lone body, and the panel shows the same stored
    relation in both loads; every earlier check unchanged (14,013 passed).
  - **Looks right** on every box of the Cargo tab (the alone box, the two-item view's arrangement row, alone on a pair,
    each contact box), drawn always and greyed when tuned (the slider rule). **Everything here looks right**,
    **... and load the next** (accepts the load, redoes the analysis, puts the most used untuned contact's smallest
    load on the cart) and **Next untuned**. The alone box no longer tunes an item by being looked at (it set
    `HasAlone` when drawn, which is why 6 of 7 alone contacts counted as tuned). `HasOnPair` (all three `onpair_*`
    written when set, zeros included) so alone on a pair can be accepted at zero.
  - **Undo / Redo / Compare with the session start** (`CargoLayout.Checkpoint/Undo/Redo/Compare`, snapshots are
    `ToText()`): a checkpoint after every finished edit (on release); compare saves the current layout first, holds
    saving off and locks editing, and ends by itself when the tab closes. PackerScan --real: the real file's text
    reads back unchanged, and an alone-on-a-pair accepted at zero reads back as tuned.
  - **Numpad + / -** move the last slider dragged in the Cargo tab by 0.01 (Shift 0.05), marked in gold; forgotten
    when the load changes shape; a press with no slider marked is dropped.
- 2026-10-12, phase B probe built: Debug tab, Cargo tools, **Measure the item shapes** (`ShapeProbe`): reads each
  class's triangles back from the GPU and writes `src/GK2DonkeyAPI/cargo-shapes.cfg` and `shape-heights.html` (top,
  bottom and side profile per class). Waiting for the first press in game: whether the GPU readback works decides
  between it and the rendering route.

- 2026-10-12, phase B measured: the GPU readback works; all 7 classes in `cargo-shapes.cfg` (embedded, loaded at start
  as `CargoShapes`; the triangles span exactly the class boxes). What the shapes show: the log is a round trunk about
  0.37 high with one stub up to 0.57 (its box height), so anything placed by the log's box floats about 0.2 over the
  trunk except at the stub; the corpse's underside is 0.09 above its box bottom in the middle; the low crate has a
  raised piece along one edge; the tall crate and the sack are rounded on top.
- The first shape scan (`PackerScan --real`) is not usable yet: 1,628 of 1,715 loads "show something", and the
  contacts the user tuned to look right measure anything from 3 to 30 cm sunk in (slab on log 0.30, log on log 0.29),
  which cannot be how they look. Two causes, both to settle before seating is built on the shapes: (a) the saved
  shapes may be mirrored against how the cart draws the views (the cargo meshes have a lossy scale of -1 along x);
  (b) at this camera angle (53 degrees down) overlap in depth is hidden by the nearer item, so "cuts in" across is
  not "looks wrong". For (a): Debug tab, Cargo tools, **Check the saved shapes against the items on the cart**
  measures the items drawn on the cart again from their own views, in the cargo holder's space, and says per item
  whether the saved shape matches as saved, mirrored along, mirrored across or turned half round.

- 2026-10-12, the orientation settled: **Check the saved shapes** on a cart with 2 logs, a slab and a low crate: all
  four match the saved shapes exactly as saved (mean difference 0.000; mirrored or turned 0.006 to 0.044). So the
  shapes are right, and the big sink numbers are the shapes themselves: the log is a round trunk about 0.37 high with
  a branch stub about 12 x 12 cm up to 0.57, and the user tuned items to rest on the trunk with the stub poking in.
- **Calibrated on the tuned contacts** (the per-cell gaps under the upper item in its smallest load, sorted): the user
  does not tune to physical resting; the median gap of tuned contacts runs from 9 cm in (flour sack on corpse) to
  13 cm up (log on corpse), a few centimetres in reads as resting at this camera angle. So the judge
  (`CargoShapes.Judge`) flags only what is clearly off: **floats** when even the lowest tenth of the underside is more
  than 4 cm up, **sinks** when half of it is more than 12 cm in. None of the tuned contacts is flagged in its own
  load. **Seat it** (`SeatDelta`): the lowest quarter at a 3 cm sink, so a small bump is ignored; a starting point.
  PackerScan --real: Seat it on all 28 on-top contacts in their smallest loads, each rests at the seat sink after the
  same freeze-and-write the tab uses (PASS).
- **The old class on-top offsets removed** from cargo-layout.cfg (the user's decision): `[item box_flour] top_up =
  -0.22` and `[item stone] top_up = -0.14`. Made before contacts could be tuned (probably against the round log), they
  sank every untuned "flour sack on ..." 25 cm and "slab on ..." 17 cm into flat supports. Tuned contacts do not use
  them. Loads with something floating or sinking: 1,045 before, 831 after; every sack and slab sink gone.
- **Built on it:** the status pages' "Looks wrong (by the measured shapes)" table (per contact: floats / sinks, in
  how many loads, the worst and its load); the Cargo tab's verdict line under the load ("slab sinks 17 cm into
  corpse", or "nothing floats or sinks here"); **Seat it** on each on-top contact box, on alone-on-a-pair, on the
  upper item of a stacked two-item cart and on each item of an upper layer's beside box (drawn always, greyed where
  it does not apply). What remains flagged is mostly untuned contacts floating on zombies and corpses (their tops are
  rounded, lower than their boxes), the low crates' alone-on-a-pair offset (+0.13, floats 10 cm on crates) and second
  items of upper layers floating over a gap.

- 2026-10-12, **full review** (the user kept finding issues). Found and fixed:
  - "... and load the next" greyed out after "Everything here looks right": renamed **Accept this load** (stays),
    **Accept and next** (always works), **Skip to next** (accepts nothing), with a one-line legend.
  - Skipping reloaded the same load (it holds the most used untuned contact): skipped contacts go to the back of the
    queue for the session (marked in Tune next); one shared `Queue()` for the list and the buttons (they disagreed on
    a tie of two contacts in 330 loads each).
  - The compare banner read the live state mid-pass (an extra line in an event pass); read once at the pass start.
  - The coverage refresh had no stuck-mouse guard; it uses the same "held" as the frozen view.
  - The shapes' cell lookup rounded exactly half-way positions (common: places in half centimetres) with float noise,
    a whole row of cells in or out between two packs of one load (accepting moved an item 2 mm); now always upward.
  - The shelf lift was measured on boxes. Checked against no lift: no difference in what the scan flags, but the real
    cause of most floats was the same thing one level up: **untuned layers rested on the tallest box below** (the
    log's box includes its 20 cm stub). Built the **seat pass** (`CargoPacker.SeatOnShapes`, with `Lookup.ShapeOf`):
    after the pack, bottom up, an untuned layer's origin rests at the nestle on the measured surfaces below (its
    items keep their heights in the layer), an untuned second item is seated on its own, a tuned layer keeps its
    values and is lifted only by a neighbour's real surface, layers above move along. Lift and Rel stay exact.
    Switch: `[General] CargoSeatOnShapes` (Cargo tab, Options, Fallbacks; in [Debug] until 2026-10-13: it changes every player's packing, so it isn't a dev setting). The coverage analysis stays on boxes (same
    contacts, fast); the scan packs as the cart does.
  - A first version seated the first ITEM; with a pair layer (crates tuned 12 cm up on the bed) and a lone layer of
    the same contact in one load, the two origins differed by 12 cm, so accepting one moved the other (5 crates).
    PackerScan --real --thorough now accepts every contact of all 1,708 multi-item loads and repacks: nothing moves.
  - The cargo self-test assumed slab + slab lie side by side (the user set them stacked): it loads four logs now, and
    the "stands on the lower layer" check allows the seat pass to sit lower than the boxes (never higher).
  - Verified fine: taking test items off only re-syncs the cart (nothing dropped); EditItem resolves to the class
    entry; undo/compare file safety, quitting mid-compare included; positions are floor-relative throughout.
  - Results (real layout, offline): loads with something floating or sinking 701 (boxes) -> 452 (seat pass). What is
    left: the low crate's alone-on-a-pair offset (+0.13, floats 10 cm on any pair), and **beside heights tuned on the
    bed carried into upper layers** (low crate + low crate +12 cm, corpse beside log +11 cm, low crate beside tall
    crate): on the bed they correct for the floor, on top of other items they float. Open question for the user.
  - Known: test-load tracking is per session; after a restart, test items left on the cart are ordinary cargo (take
    the test load off before closing the game). Numpad + / - may be bound by Grave Zoom.

- 2026-10-12, **second full review** (with an independent code review in parallel). Found and fixed:
  - **Speed of the seat pass:** 1.8 ms and 250 KB per six-item pack (every pack, every frame of a drag). Now the
    underside is sampled every 2 cm, both gap sets in one sweep, percentiles by selection, buffers reused, shapes
    resolved once per pack, items whose footprints cannot meet skipped: 0.08 ms and no extra allocation. The judge
    (status page, verdict line) uses the same path.
  - **A layer moved as one only when automatic:** with the seat pass an untuned second item re-seated on its own, so
    dragging a tuned layer's Height moved only its first item. Now the second item is seated on its own only while
    the whole layer is automatic, and tuning a layer (slider, Looks right, Seat it) freezes its second item's place
    with it (`CargoPacker.FreezeLayerArrangement`), so nothing jumps. PackerScan: the whole synthetic suite runs a
    second time with the seat pass on (`--shapes`, solid-box shapes): 13,991 passed; `--real --thorough`: touching
    one on-top box (3,284 rows) or one beside box moves nothing, accepting every load (1,708) moves nothing.
  - **Test loads, items never lost:** a test zombie was removed on Take off whenever it was not on the ground, a cart
    or in the hands, so one set down and put to work at a station would have been deleted; now a zombie is removed
    only together with its own item just taken off this cart. A failed add removes the zombie made for it at once. A
    test item that leaves the ridden cart is forgotten at once (`TestLoads.CartChanged` from `Rig.OnChanged`), so one
    set down, used and loaded again is ordinary cargo. The test items are kept in `BepInEx/GK2-test-loads.txt`, so a
    test load left on the cart can be taken off after a restart.
  - Compare mode: a right-click reset on a greyed slider still edited (and the edit was then dropped); resets need an
    enabled control now.
  - The numpad target survived a change of load with the same first and last items; the key is every item's id,
    layer, partner and relation (and isolation); a slider no longer drawn is forgotten with any pending press.
  - Two members of one size class (tall crate box_beer / box_clothes_1) got two boxes for one contact in a 3+ load;
    the boxes are keyed by class.
  - The verdict named a beside item's neighbour as what it sinks into; it names the item most of it lies over.
  - The coverage analysis (about 60 ms) ran after every finished edit, every numpad press; it waits until edits
    settle for half a second, and Accept and next no longer analyses twice. The queue and the fully tuned count are
    cached per analysis.
  - A layout edit made just before quitting is saved at quit (`OnApplicationQuit`).
  - Nudge keys are settings (`[Debug] CargoNudgeUp/Down`, numpad + / - by default), in case another mod uses them.
  - The photo runs' refusal on a cart with only a test load says to press Take the test load off.

- 2026-10-12, **third pass (fresh read of the whole packer and tab)**:
  - **Bed heights no longer carry into upper layers** (the open question, answered in the packer rather than the
    file): above the bed the layer's FIRST item sits at the origin and the second keeps only its height relative to
    the first (`Result.LayerBase`, the beside branch). A pair tuned 12 cm up on the bed (crates over the rails) or a
    class bed offset (logs 9 cm down) is how it sits on the flat bed; in an upper layer the first item rests by the
    seat pass and the second follows relative to it. Nothing stored changes meaning: every beside freeze and write
    goes through `Result.BesideStored` (the place in the layer plus the base), so tuning from an upper layer keeps
    the pair's bed heights. The tab greys the first item's Height slider in an upper layer (its height is the
    layer's: the on-top box) and labels the second's "relative to <first>". A first version rested the layer on its
    LOWEST item: that made the origin depend on the arrangement, and one on-top relation could not hold a pair layer
    and a lone layer of the same contact (12 loads moved on accept); the first-item rule keeps the origin
    arrangement-independent and every invariant holds (touch one box 3,284 + 27 rows, accept 1,708 loads: nothing
    moves; both synthetic suites pass with and without the seat pass).
  - An untuned second item is seated on its own in every layer (also under a tuned on-top relation); once tuned
    beside it moves with the layer, and the panel freezes it with the layer's on-top relation.
  - Seat it measures as the seat pass does (2 cm sampling): on an item the seat pass already rested it changes nothing
    instead of writing a few millimetres. The Seat it test measures the same way (it had re-measured at 1 cm).
  - Numpad nudges within two seconds of each other are one undo step (`Checkpoint(merge)`).
  - The six-logs test checks the layer origin (its untuned second log rests on its own with the seat pass; in game
    the first slider move freezes it with the layer).
  - Loads with something floating or sinking: 452 -> 385. The remainder is above all untuned contacts on zombies and
    corpses (rounded tops) and the low crate's alone-on-a-pair +0.13.

- 2026-10-12 (evening), **built from the investigation below**, all offline suites pass (14,013 / 13,991 /
  `--real --thorough`: touch one box, accept 1,708 loads, Seat it 28/28, plus two new checks):
  - Packer: an untuned layer over a pair starts at the layer below's origin (+ layer shift), same-class and mixed
    pairs alike; Accept from a + a + a now writes the straight-over value (check: 4 beside-pairs, a + a + a + a straight,
    the three-item load unmoved). A lone item on a pair sits over the middle of the two below plus its class's
    `onpair_*` correction (check: 866 lone-on-a-pair cases in the 1,715 loads, all at the middle). `Origin` and `Rel`
    unchanged, so every stored on-top relation and every tuned pair-on-pair load stays where it was.
  - Layout file: the sideways `onpair_along/across` values (tall crate -0.34 / 0.02, log 0.01, low crate 0.02) set to
    0; the two `onpair_up` values kept. Judged scan 385 -> 379 loads.
  - Coverage / tab: `Contact.SmallestReady` (the smallest load where it is the only untuned contact) and `Ready`;
    Tune next orders ready first, then up the ladder (`KindRank`: alone, beside, on, alone on a pair), then by use;
    Load / next put `LoadToTune` on the cart. The "1 item / 2 items / 3 or more" chips are the ladder with this
    load's kinds lit; a box whose load has an untuned contact lower on the ladder says "tune first: ...".
  - PackerScan: `--show "a,b;c,d,e"` prints packs item by item; `--cfg <path>` runs the real scan on another file.

## Investigation 2026-10-12 (evening): a lone item on a pair, and the order of tuning

Asked: three tall crates (a pair with one on top) should show the top crate between the two below, four crates should
stack straight over the pair, and moving either box for the lone crate seemed to spoil the other case. Also: in
which order should contacts be tuned, and does work on big loads change small ones?

Tools used: `PackerScan --real --show "a,b,c;d,e,f"` (new) prints the pack of named loads item by item (layer,
relation, partner, target, layer origin, lift); `--cfg <path>` (new) runs the real scan on another layout file (a
scratch copy for an experiment; the shapes file is looked for next to it). The game saves straight into the repo's
`cargo-layout.cfg`, so the repo copy is the live layout.

### What the packer does with the stored values (tall crates)

- `beside box_beer+box_beer`: a 0.28, b -0.18 along (spread 0.46). `on box_beer+box_beer`: top_b - top_a =
  (-0.28, 0.37, 0). `onpair box_beer`: (-0.34, 0.03, 0.02).
- Four crates: layer 2 origin = support 0.28 + (-0.28) = 0.000 = the bed origin, and above the bed a pair is arranged
  around its layer's origin exactly as on the bed, so the upper pair lands at 0.28 / -0.18: straight over. The "on"
  box at -0.28 IS the straight-over value for this pair (it equals minus the support's beside offset).
- Three crates: the lone crate sits at the layer origin (0.000) plus its class's onpair offset: x = -0.34, i.e.
  16 cm behind the rear crate (-0.18). Centred between 0.28 and -0.18 would be onpair x = +0.05, z = 0.
- The same onpair offset is applied on every pair the crate lies alone on: corpse + log + crate puts it at x -0.37
  (that pair's midpoint is -0.01), flour + flour + crate at -0.04 (midpoint 0.075). One per-class value cannot
  centre over pairs whose midpoints differ. This, not the slider the user moved, is the defect.

### The trap: Accept writes the pair's midpoint into the shared "on" relation

With `top_*` of the crate pair removed (scratch copy): the untuned layer over a same-class pair starts at the pair's
midpoint plus LayerShift, 0.05 - 0.03 = 0.020, for the lone crate AND for the pair on the pair. Accepting from either
load writes Rel = 0.02 - 0.28 = -0.26 into `on box_beer+box_beer`, so four crates come out 2 cm off straight
(0.30 / -0.16) until the slider is dragged to -0.28 by eye, and the lone crate is then placed from the straight-over
origin and must be centred by the onpair box. Over a mixed pair the untuned origin is over the SUPPORT, not the pair:
corpse + log + crate + crate puts the crate pair's origin at across 0.13 (the corpse's place in that pair), centred on
the corpse, not on the cart. So "one set affects the other" is true today in exactly one way: the on relation holds
the layer origin, and the untuned default for that origin depends on which load it was accepted from.

The onpair sliders and Seat it write only the item's `OnPair` (`TuningWindow.Cargo.cs`, the alone-on-a-pair box); a
second item joining the layer re-places the layer from its origin, so that box never moves a pair on a pair.

### Proposed change (small; not built, waiting for the decision)

1. The untuned layer origin over a pair = the layer below's origin (+ LayerShift), for same-class and mixed pairs
   alike. Accept then writes the straight-over value, and a pair on any pair is centred over the cart like the bed.
2. A lone item on a pair starts at the footprint midpoint of the two items below (x, z), at the height the on relation
   gives; `OnPair` becomes a per-class correction on top of that. `Origin` and `Rel` keep their meaning, so every
   stored "on" relation and every tuned pair-on-pair load stays where it is.
3. LayerShift (-0.03 along) contradicts "directly on top" and should be 0 for this layout.

Cost: the 5 onpair tunings change meaning (they should all end near zero; retune by eye or Reset), the seat pass
measures at the new x/z, so `PackerScan --real --thorough`, the synthetic suites and a new all.html run are needed.
A later, larger step would make the "on" relation's horizontal part relative to the layer below's origin instead of
the support (28 relations, exact migration only in each contact's smallest load); not proposed now.

### The order of tuning

Effects run upward only. A beside relation moves every layer above it in every load it occurs in; an on relation moves
its layer and the layers above; an onpair offset moves one item; alone (one item on the cart) touches nothing else
(the bottom of a stack takes the pair's own place, not the alone offset). Re-touching a lower kind later can move
tuned upper kinds along, so they want a glance afterwards; the reverse never happens. The number of items is not the
order: a six-item load adds no slider, it is these relations stacked.

The ladder: alone (7) -> beside (9) -> on (28; mostly heights once the origin default is fixed) -> alone on a pair
(5; corrections). Today the Tune next queue is most-used-first, which can offer `log on corpse` (smallest load
corpse + log + log) before `corpse beside log` is tuned, and `flour sack alone on a pair` (corpse + sack + log)
before `sack on corpse`. Proposed: order by readiness first (contacts whose smallest load has no other untuned
contact), then by kind up the ladder, then by use; choose each contact's Smallest as the smallest load where it is the
only untuned contact when one exists; a box header says "needs X first" when a lower-kind contact of the same load is
untuned; the "1 item / 2 items / 3 or more" chips become the ladder's step. Not built yet.

