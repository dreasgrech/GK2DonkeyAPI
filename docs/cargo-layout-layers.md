# Tuning the cargo layout for more than two items

Investigation, 2026-10-10, after the first six-item loads in game ([cart-capacity.md](cart-capacity.md) section 9).
The question: with three to six items on the cart, the panel's corrections section shows no sliders, so how are
the positions of such loads to be tuned by hand, and what is the smartest way to make that tractable? Nothing in
this doc is built yet.

## 1. What the layout system is today

Read from `CargoLayout.cs`, `cargo-layout.cfg` and `Cargo.Rig.Pack`:

- **The packer** measures every item's mesh box, sorts by footprint (largest first, ties by id then load order) and
  places by rule: the first item on the floor; the second beside it across the bed when the widths fit
  (`CargoSideGap`, `CargoOverhang`), else end to end when the lengths fit, else on top, sunk by `CargoNestle`.
  Since 0.28.0 further items go on in layers of up to two by the same test, each layer over the middle of the one
  below at its top less the nestle, moved by `CargoLayerShift` per layer.
- **Corrections** live in `cargo-layout.cfg`, keyed by item id and resolved by *size class* (boxes equal within
  0.05): eight classes today (log, flour sack, tall crate, low crate, body, slab, zombie; the chests are classes of
  their own and ride alone).
  - `[item <id>]`: offsets for three roles: `along/up/across` (bottom or beside), `top_*` (stacked on another item),
    `alone_*` (the only item).
  - `[pair <a>+<b>]`: **one** `mode` (`auto | side | along | stack`) and **one** set of exact positions `a_*` /
    `b_*` (footprint centre along and across, bottom height above the cart floor), written when the panel moves
    either item. A tuned pair is frozen against the class values, the gap, the overhang, the nestle and the load
    order; only the floor moves it. Applied only when exactly two items are on the cart.
- **The panel** tunes what is on the cart: one item gives the alone sliders, two items the pair's arrangement buttons
  and per-item sliders. Edits are saved to the repo file. The **cargo sheet** photographs every class alone and every
  ordered pair, 72 pictures, which is the visual check for all of it.
- **What is tuned now** (the file): 7 item entries, 14 pair entries, 8 of them with exact positions. Of the tuned
  pairs, 5 are `stack` (bodies on bodies, flour on a body, zombie on zombie, ...), 3 are beside (`side` / `along`).

**The one fact that decides the design:** a pair entry describes exactly one relation between two classes, either
*beside* or *on top*, never both. `wood+wood` is `side` and stores no on-top relation at all. In a six-log load both
relations happen at once: logs beside each other in every layer, and logs on logs between layers. The flour sack
with a crate on it (the screenshot of 2026-10-10) is a plain two-item stack and is tunable today; three logs are
not.

## 2. Why per-configuration tuning is out

Eight classes give 36 unordered class pairs. A three-item load is a pair plus a single: 36 × 8 = 288 configurations.
Four items as a pair over a pair: 36 × 36 = 1,296. Six items: well over 40,000. The sheet's 72 pictures already take a
few minutes to shoot and an afternoon to judge. Whatever the panel offers for a stack, it must edit something that
is shared by every load containing the same situation, or the work never ends.

## 3. What actually differs between a pair on the bed and the same pair higher up

Nothing in the pair itself. Two logs side by side look the same at any height. What differs is how the layer *sits
on what is below*: its height (a crate on two round logs sinks differently than on two flat crates), its lateral
centring, and the sink. Those are relations between the item above and the item(s) under it, which is exactly what
the five tuned `stack` pairs already describe for two items (`body_corpse+box_flour`: the sack sits 0.19 above the
body's bottom, 0.04 across). So the stack already knows how to say "X on Y"; it just cannot say it for a pair that
is also tuned beside, and it cannot apply it above the first layer.

## 4. Options

**A. Per-layer reuse of today's pair tuning.** Apply a tuned pair's positions relative to each layer's origin, not
only on a two-item cart. One packer change and a panel block per layer. It carries the *horizontal* relation up the
stack and leaves every *vertical* relation to the global nestle and the class `top_*` offsets. Cheap; half the
problem. A fallback if something is wanted quickly.

**B. Pairwise relations as the unit of tuning.** Every placement in any load is one of two relations between two
classes: *beside* (side or along) or *on top*. The file stores both per pair: the existing `a_*/b_*` become the
beside positions, a second set holds the on-top positions, and `mode` only says which of the two a two-item cart
shows. The packer composes N items from these relations: beside within a layer, on top between layers. Tuning either
relation is done in the familiar two-item view (the panel's "start over as" buttons already switch a pair between
the arrangements; they would switch which relation is being edited instead of forgetting the other). Every bigger
load inherits every relation it contains. 36 pairs × 2 relations = 72 things to tune, and the sheet can picture all
of them. This is the natural generalisation of what the file already does for `stack` pairs.

**C. Support-aware placement, on top of B.** An upper item sits on the item below with the largest footprint overlap
(its *support*), and the on-top relation of that pair gives its offset, instead of a flat layer floor from the
layer's highest point. Needed for mixed layers: a crate over a log and a slab should sit on the log. Over a
supporting pair of one class (a third log over two logs) the rule is the layer's centroid with the class relation
applied; whether that third log should rather lie in the valley between the two is the one open visual question,
decided in game.

**D. The panel for more than two items: one block per relation in play.** The packer records, for every placed
item, which relation put it there (beside its partner, or on top of its support). The panel lists them, in layer
order, each with the existing arrangement buttons and sliders, and edits the pair entry's relation set. Positions
are shown relative to the relation's origin (the partner or the support), not the cart floor, which is meaningless
for a third layer. An **"isolate these two"** button swaps the cart to test copies of the relation's two items, lets
you tune in the plain two-item view, and swaps the load back: the cargo sheet already has that machinery (the saved
list, test copies, `Abort` on a dismount or a save).

**E. The sheet pictures both relations.** Every pair photographed twice, beside and stacked: 36 more pictures, and
every relation the stacks are composed of has its picture. A layered sheet (pairs over pairs) stays out.

## 5. Constraints the build must respect (from the code)

- **Heights are absolute today.** A tuned pair's `a_up` / `b_up` are heights above the cart floor. An on-top
  relation must be stored, or at least applied, as *b relative to a* (along, up, across from a's place), or it
  cannot move with the layer. The beside relation can stay as it is (relative to the layer's origin, which is the
  floor for two items).
- **`FirstIsA`**: for a same-class pair the first-placed item is A. For log on log that still works (A is the
  support, B the one on top); the sort puts equal footprints by id then load order, so same-class layers are
  deterministic; mixed ones depend on the measured boxes, which are stable.
- **`Placed` carries no support.** Add the partner or support index and the relation kind, for the panel.
- **Frozen means frozen.** A tuned relation must ignore nestle, gap, overhang and layer shift, as tuned pairs do
  today, or tuning in isolation will not reproduce inside a stack. `CargoLayerShift` then only moves untuned
  relations; it can stay as the default lean or go once every relation is tuned.
- **Migration of the file.** `version = 2`: today's `a_*/b_*` of a `side`/`along` pair become its beside set, those
  of a `stack` pair its on-top set (converted to b-relative-to-a). Eight tuned pairs and 7 item entries; a one-off
  script, checked by loading each pair and comparing the layout note before and after. Nothing is shipped, so names
  are free, but the tuning done so far must come through unchanged.
- **One and two items never move.** Every change is checked against the current 72 pictures.

## 6. Recommendation

Build B with C and D, in that order, each step usable on its own:

1. The file format (both relation sets per pair, b relative to a for on-top), the migration, the packer composing
   from relations with the support rule and the per-item record of what placed it. Two-item loads unchanged.
2. The sheet's second picture per pair.
3. The panel: relation blocks for more than two items, "isolate these two", positions relative to the relation.
4. Docs: reference.md's placement paragraph, the panel text, this doc's "built" section.

Done means: every relation is tunable in the two-item view, every load of any size is composed only of tuned or
default relations, and the panel shows which relations a load is made of and lets you jump into any of them.
Option A alone is the fallback for a quick result; it would be thrown away by B.

The height question (three layers of crates stand above the rider's head) is separate from tuning and is a
capacity decision: four for the tall classes, six for the flat ones, or a per-class cap. Decide after the stacks
look right.

## 7. Built so far

**Step 1 (2026-10-10): the file format, the migration, the packer composing from relations.**

- `cargo-layout.cfg` is version 2: a `[pair a+b]` holds `mode` and two relation sets, `beside_a_*/beside_b_*` (both
  places in the layer's frame) and `top_a_*/top_b_*` (b on a, both places as a two-item cart shows them). The repo
  file was migrated by script (the eight tuned pairs: three became beside sets, five on-top sets, by their mode);
  a version 1 file is still read, its one set becoming the relation of the pair's mode, and is written in the new
  form at the next save.
- `Pack` composes any load from the two relations. Beside: within a layer, the tuned set applied around the layer's
  origin (the floor origin for the bed, the layer's centre above). On top: the new layer's first item over its
  *support*, the item below it overlaps most when placed over the layer's middle (over the middle of a same-class
  pair), at the highest overlapped top less `CargoNestle`, with its on-top class offsets and `CargoLayerShift`
  (only when the layer below is a pair, so the two-item stack is unchanged); a tuned on-top relation puts it at the
  support plus (b minus a), and when the support is alone on the bed it also takes its own tuned place, exactly as
  the two-item cart was tuned. One and two items reproduce the old layouts: a two-item cart shows the relation of
  the pair's mode.
- `PlacedItem` records, per item, the relation that placed it (`bed`, `side`, `along`, `on`), the partner (the
  layer's first item, or the support), its place relative to that partner, and whether the relation was tuned:
  what the per-relation panel (step 3) will list.
- The two-item panel edits the relation the cart shows. The arrangement buttons switch between the relations and
  keep both tunings; the lit button again (or side <-> along) starts that relation over. The label says whether the
  other relation is tuned too. The sheet's tuned-pair list says which relation(s) are tuned.
- Not yet: the sheet's second picture per pair (step 2), the relation blocks and "isolate these two" for more than
  two items (step 3), the reference.md paragraph (step 4). To check in game before step 2: the 72 two-item pictures
  unchanged (the sheet), six logs, four crates, a crate over a log and a slab (the support rule), three logs.

**Steps 2 to 4 (2026-10-10, same day).**

- **The sheet** pictures every pair twice: beside each other (its own arrangement, or the packer's) and the
  second on the first (`CargoLayout.ForcedRelation` holds the packer to one relation while it shoots; cleared when
  the sheet ends or aborts). `index.html` has two grids of 8 × 8 ordered pairs (64 each, a+b and b+a being the same layout) plus the alone column: 136 pictures, where the old sheet had 72.
- **The panel with more than two items** lists the relations the load is composed of, one row per placed item
  that is not the first on the bed: `Layer n: X on Y` or `Y | X beside (side/along)`, tuned or automatic, and X's
  place relative to Y. Each row has **Isolate**: the cart draws just those two, laid out as that relation (the
  rig's own forced relation), with the usual two-item sliders; the real load stays on the inventory untouched, so a
  dismount, a door or a save need nothing special. "Back to the whole load" (or either item leaving the cart, or
  the view going) ends it.
- **The two-item editor** edits the relation on screen (what the packer did: side / along is the beside set,
  stack the on-top set), so isolating an on-top relation edits the on-top set and a two-item cart edits the set of
  its mode. While isolating, the arrangement row is hidden and the pair's mode is left alone.
- **Recorded per placed item** for all this: the relation, the partner, the place relative to the partner, the
  place relative to the layer's origin, the item's GUID, and whether the relation was tuned.
- **Self-test** extended: the relations and relative places of a four-item load, isolation as a stack and as a
  beside pair with the four still aboard, and back.
- **Tune in context (2026-10-10, after the first hand test):** isolating a relation shows it as a two-item cart, on
  the bed, which is the right picture for the sheet but not for judging how a layer sits in the stack. Each
  relation row now has **Tune**: sliders for that relation, live, with the whole load on the cart. An on-top
  relation is edited as the upper item relative to its support (frozen on first touch: the support's own bed
  place plus the current offset, so a two-item cart shows the same); a beside relation as both places in the
  layer's frame. The same relation follows everywhere it occurs. Isolate is renamed **Alone** (the two-item
  picture with the same values).
- **The first tuning session (2026-10-10, recorded):** two findings. The layer floor depended on which items
  below the new layer's footprint overlapped, so dragging 'along' across that threshold made the upper layers
  jump between two heights every frame: the floor is now the highest point of the whole layer below, a shelf,
  no threshold (a crate over a log and a slab rests at the log's height even over the slab; accepted). And
  the beside relation's 'up' sliders lifted both logs inside their layer, which raised every layer above into
  a floating tower: the labels now say what each slider is for (the on-top 'up' is the stacking slider; the
  beside 'up' floats an item within its layer and should stay 0 unless one item rides on the other), each
  tuned row has **Reset**, and the log pair was put back to `mode = side` only.
- **The runaway sliders (2026-10-10, second recording):** the on-top relation placed the layer's first item and
  the beside relation then moved that same item again, so the Tune sliders read a place that already held the
  beside offset and wrote it back as the relation, once more every frame (0.36, -2.59, -66). Now the on-top
  relation places the *layer* (its origin relative to the support) and the beside relation arranges the items
  inside it; `PlacedItem.Rel` of an `on` item is that origin offset. An untuned relation is frozen where the
  item is (origin plus its on-top class offsets), so nothing jumps on the first touch.
- **PackerScan (2026-10-10, after problem after problem in game):** the packer is now `CargoPacker.cs`, pure arithmetic
  with the layout lookups injected, and `tests/PackerScan` runs it without the game (`dotnet run -c Release` after a
  Release build of the mod): one and two items against a port of the pre-layer algorithm (1,296 cases: every class
  pair, every mode, untuned / beside tuned / on-top tuned, every forced relation); the Tune loop as a fixed point
  (read a relation, freeze, write it back: nothing may move; 1,280 loops over 38 loads, half of them with every
  pair pre-tuned at random); a slider delta moving its relation by exactly that wherever the pair occurs and no
  other tuned relation; determinism; forced relations; sanity. It found three more defects on its first run: the
  upper layers used the on-top class offsets for a beside pair (so freezing the bottom pair moved them; pairs
  now use the base offsets in every layer), the on-top class offsets were on the item instead of on the layer
  origin (so freezing a relation shifted the second item of that layer; the origin now carries them and the first
  item sits exactly on it), and the panel added those offsets to the shown value. The panel edits go through the
  same helpers the test calls (`FreezeOnTop`, `WriteOnTop`, `FreezeBeside`, `WriteBeside`).
- **The support is the layer below's first item (2026-10-10, third recording):** the support had been chosen by
  footprint overlap, a tie for two logs side by side that the float noise of the live measurement broke one way
  or the other every frame, so a tuned on-top relation measured against one log was applied against the other
  on alternate frames (a 0.37 jump across the bed: the flicker while dragging, and the jump when Tune froze the
  relation). PackerScan reproduced the jump from the exact file state once that case was added (`Mixed.cs`:
  beside tuned, on-top not). The support is now always the first item of the layer below; no overlap test.
- **The Cargo tab (2026-10-10, evening):** the tuning moved from the Riding tab's corrections section to its own tab,
  laid out as a workflow with one line of guidance per step and every knob kept: the load on the cart (its layers,
  top to bottom); step 1 one item alone; step 2 two items together (arrangement buttons, each item's place,
  Reset); step 3 stacks (one box per kind of contact in the load, sliders always open, Alone and Reset per
  box); step 4 the packer's rules (folded); step 5 behaviour (folded); step 6 how to check; a folded help of six
  lines. Slider names: Height, Along the cart (- back, + donkey), Across the cart (- camera, + away). Colour:
  section headers gold on every tab, steps blue, 'tuned' green, 'by the rules' grey (rich text in the styles).
  The Riding tab keeps the cart's speed, seat and slope; Debug keeps the probes and the sheet.
- **Tinted items (2026-10-10, night):** while the Cargo tab is open, the ridden cart's items are tinted by their
  role through the game's own selection tint on the item meshes (`Object3DMesh.SetSelectionTint`): the first
  item of a layer blue, the second (or an item alone on top of a layer) orange; the tab's labels carry the same
  colours, so a slider block and the item it moves match by colour. Cleared when the tab closes (a frame stamp
  the tab refreshes while drawn; `Cargo.Rig.TickTint` from the instance's Tick) and re-applied after every pack.
- **Show on the cart (2026-10-10, night):** the Cargo tab has toggles to hide blue items, orange items or whole layers
  from view while it is open, so one part can be tuned without the others in the way. View only: the hidden items
  stay in the layout (packed, then their views switched off), so nothing moves; everything shows again when the tab
  closes. The tint is now hue by role and shade by layer, with a switch (Debug.CargoTabTint).
- **Alone on top of a pair (2026-10-10, night):** a lone item on an upper layer (five logs: the top one) was placed by
  the same on-top contact as a whole pair layer, at the layer's origin without the beside offsets its neighbours
  get, so it floated and could not be moved alone. Each class now has `onpair_along/up/across` in its `[item]`
  entry, applied only to an item lying alone on top of a pair; step 3 shows a box for it. Never on a two-item cart.
  Since 2026-10-12 (evening) the lone item starts over the MIDDLE of the two below (footprint centres) and the
  offset is a correction on that; before, it started at the layer origin and one per-class offset had to centre it
  over every pair it could lie on (docs/cargo-tuning-tools.md, the evening investigation).
- **The rules, traced (2026-10-10, night):** the floor moves everything (cart calibration); gap and overhang decide
  whether a pair set to 'auto' lies beside or stacks, tuned or not, and space untuned beside pairs; nestle and layer
  shift only touch untuned on-top contacts. So a beside contact tuned from step 3 with its pair still on 'auto'
  could have been flipped by an overhang change: freezing a beside contact now pins the pair's arrangement to the
  way it lies (`FreezeBeside(..., arrangement)`), and PackerScan checks it survives a smaller overhang. The tab
  shows the floor as step 0 (set first) and step 4 as 'Fallbacks for contacts not tuned yet', grouped by what they
  decide, with how many contacts of the load still follow them.
- **No more numbered steps (2026-10-10, night):** the numbers 0 to 6 read as 'step 4 is for four items'. The tab is
  now three parts: 'The cart' (the floor, set once), 'Tune this load' with three views (1 item, 2 items, 3 or more)
  of which the one matching the load is lit and shown, and 'Options' (fallbacks, behaviour, help with how to check),
  folded.
- **Drag jumping to another slider (2026-10-10, night):** a box's Reset button appeared only once the box was tuned,
  so the first move of an untuned slider added a control above the sliders; IMGUI knows controls by their order in
  the frame, and the held drag went to the slider below (the dragged one would not go left). Reset is now always
  drawn, greyed when there is nothing to reset (`ResetButton`). The video frame by frame: Height -0.01, Reset appears,
  the slider sticks, the value falls back to 0 and wood (1) Along follows the mouse.
  The full review of the tab for this class of fault, and what else was fixed:
  - The tab draws from a view of the load frozen while any panel control is held (`GUIUtility.hotControl`):
    dragging the overhang or the gap re-arranges the load, which added or removed contact boxes above those sliders
    (the same jump), and a pack in between could do the same. The view follows on release. Held means hotControl set
    and a mouse button really down (`UnityInput`), so a release outside the game can't leave the view frozen. The sliders take their
    values live from the last pack (`LiveOf`, only while the load has the same shape), because a slider compares its
    result with the value it was given and a frozen value would drop a drag back to the starting point.
  - Every state a click changes is read once before the click: tint, hide filters, the three folds, set down behind
    the cart, and the isolation (Alone / Show the whole load). Reading the live state after the click drew more
    controls than the layout pass had in that event.
  - PackerScan sweeps every contact slider and every alone-on-a-pair offset to both ends of its panel range on 38
    loads: the load's shape (order, relation, partner, layer) never changes. So in 'Tune this load' only the
    fallbacks (overhang, gap) can change which boxes show, and those are covered by the frozen view. The scan's
    beside freeze now passes the arrangement, as the tab does. 14001 passed, 0 failed.
  - No panel aborts and no GUILayout errors in either log for the session in the video.
- **Coverage tools (2026-10-12):** see [cargo-coverage.md](cargo-coverage.md): every load of up to six items analysed
  by contacts, a status line in the Cargo tab, status / coverage / every-load pages from the Debug tab.
- **The fit rule made deterministic (2026-10-10):** comparing the new sheet with the one from 2026-10-05 (every
  other picture unchanged beyond lighting), two pairs had flipped from stacked to side by side: a crate beside a
  slab, and beside a log. Their widths sum to exactly the limit (0.50 + 0.02 + 0.35 = 0.87 against 0.75 + 0.12),
  and the live boxes are measured on a cart that may stand on a slope, so noise decided. The decision now uses the
  size classes' boxes (`CargoLayout.BoxOf`, measured once) in whole centimetres, and equal means no room, which
  reproduces the old pictures. The live boxes still place the items.
- **Open:** in game, the pictures (one- and two-item pictures unchanged against the previous sheet), then
  a first calibration of the on-top relations (log on log, crate on log, crate on crate, body on log), the
  third-log-over-two question, and the capacity default.
