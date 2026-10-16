# Cargo auto-tune: can the layout be tuned without the eye?

Asked 2026-10-15: with every contact tunable (and one-pair offsets for the exceptions), could the cargo layout values be
found automatically instead of by hand, slider by slider?

## The probe: `PackerScan --tune` (writes nothing)

`tests/PackerScan/Tune.cs`. A cost per pack from the measured shapes (`cargo-shapes.cfg`): cut-ins deeper than the
scan's 3.5 cm (`CargoShapes.Overlap`), the judged float and sink of every upper item past its threshold
(`CargoShapes.Judge`, the thresholds calibrated on 2026-10-12 against the tuned heights), an item with nothing under
it, an item past the bed plus the overhang. Then:

1. **The load the user fixed by eye** (low crate alone on log + low crate, with the one-pair offset
   `[onpair box_appliances_1 on wood+box_appliances_1]`), before and after the fix.
2. **Every tuned contact's landscape:** one axis of its stored value moved from -8 to +8 cm in 2 cm steps, the cost
   summed over its loads of up to four items (at most 40), and whether the stored value is in the lowest band.

If the shapes judged the way the user does, the user's values would sit in the band, and the fixed load would score
better after the fix than before.

## Result (2026-10-15, layout of commit cc8a7ea)

- The fixed load scores barely better after the fix (0.82 -> 0.71) and still "bad": the log and the low crate beside it
  on the bed cut into each other by 17 and 28 cm by the shapes (their boxes overlap 6 cm along the cart: log at +0.42,
  0.82 long; crate at -0.19, 0.52 long), and the log reads 26 cm past the bed (offline bed length 0.90; the game
  measures its own). In the game the same load looks right to the user.
- The stored values sit in the band on **14 of 135 axes**, outside it on 112, flat on 9. The shapes would push almost
  every contact apart and up.

**Conclusion: the 3D shapes do not judge the way the eye does.** The user judges the picture at the game's camera
angle, where an item partly behind or under another hides the overlap the volumes have; the shapes count every
centimetre of volume overlap as wrong. An auto-tuner driven by them would "fix" approved layouts into worse-looking
ones. They remain right for what they were calibrated on: heights (float / sink, Seat it).

## What would work: judging the picture

The judgement has to be made in screen space, on what the player sees:

1. **A render probe in the game:** for a load, one capture with every item in its own flat tint (the panel's per-item
   tint exists), plus one capture per item alone at its place; from those, each item's full outline, the part of it
   that is visible, and which item covers which, in pixels.
2. **Calibration on the user's verdicts:** the 62 ok loads (and the wrong ones, and before/after pairs like the fix
   above) must score as the user judged them. Only a measure that separates those is worth optimising.
3. **Then a tuner** that proposes one-pair offsets for the loads that score badly, never touching tuned shared values
   unless asked, and writes a proposed `cargo-layout.cfg` outside the repo for a `git diff`.

That is an in-game capture over many loads (slow, several seconds per load) and a calibration step whose outcome is
unknown until the ok loads are measured. Not for before the release; the review strip stays the way the layout is
judged.

## The probe run (built 2026-10-15, director only)

`tools/GK2ShowcaseDirector/PictureProbe.cs` and `ProbeRun.cs` (the director never ships in the dist). Buttons in the
director window (F9): **Probe this load** (the load on the cart now) and **Probe run (reviewed + 1-3 items + samples)**,
with **Stop**. The run takes every load with a review mark, every load of one to three items and a fixed random 25 each
of four, five and six items, puts each on the cart as an API test load (`TestLoads.Put`: replaces only test items,
refuses while anything else is aboard; `TakeOff` at the end) and probes it in one frame step:

- a private camera copied from the world camera renders the cargo (and the cart body) with flat unlit colours
  (`Hidden/Internal-Colored`) into a texture of the world camera's own size: everything together, each item alone, each
  pair; every swap (layer, materials, property blocks) is put back in the same step;
- per item: outline pixels, visible pixels, pixels covered by each other item and by the cart, its pixel box, and for
  an upper item the air under it column by column (columns with a gap, largest, sum);
- per pair: pixels of each covered by the other with only the two drawn (both non-zero: a visible cut-in);
- the 3D side: Placed (id, layer, relation, partner, pos, box, rel, tuned), the bed (width, length, floor), each view's
  local and world place and renderer bounds, the camera's world-to-camera and projection matrices, the container's
  matrix;
- loads of up to three items: sweeps, each item but the first moved by -8, -4, -2, +2, +4, +8 cm along each axis (the
  view only, put back), measured again with the mutual cover against every other item.

Output in `<shots folder>/probe-run_<time>/`: per load `NNNN_<key>.txt`, `_normal.png` (the game's look, cropped to the
load) and `_mask.png` (r: owner with the cart drawn, +1, the cart = 7; g: the outlines' bits; b: owner without the
cart, +1), plus `summary.csv`.

Added the same evening, so one run gathers everything:

- **The atlas** (first in every run, and alone with **Atlas only (another facing)**): every carryable item type (the
  size classes' members; the zombie by its representative) alone on the cart: its placement, outline, normal picture
  and **depth maps** of its front and back surfaces, measured by sweeping the camera's clipping planes through it in
  1 cm steps with back or front faces culled; the cart body's outline and depth once. File `atlas/<id>.depth`:
  `GKD1`, the box (x, y, w, h as int32, texture rows upward), then w*h float32 front distances and w*h back ones, NaN
  where nothing. The game's camera is orthographic, so a moved item's outline is its atlas outline shifted and its depth
  the atlas depth plus the move's depth: any layout can be composed offline, and the per-load renders check it.
- **The facing**: which way the container's axes point on the screen (`xr-zu` etc.), in `run.txt`, every load's
  `.txt` and the run folder's name; another facing is a short atlas-only run.
- **"Before" variants**: every lone item on a pair placed by a one-pair offset is measured again where the class's
  every-pair offset would put it (`variant` lines and a `_variantK_before_normal.png`).
- **Sweep pictures**: the normal picture at the +-4 and +-8 cm steps, for the ok / wrong sheet.

Not yet run.

**First run, 2026-10-15 23:57, stopped:** the flat shader `Hidden/Internal-Colored` drew 1 pixel of a corpse (it
multiplies by the vertex colours, which the game's meshes use for other data), and the world camera renders at the
screen's full size (2560x1440 here), not at a low pixel-art size. Rebuilt: each session tests candidate shaders on its
first item and keeps the first whose outline is at least a quarter of the item's screen box: first
`Hidden/Internal-DepthNormalsTexture` (outline and camera distance in one render, decoded from the encoded depth with a
far plane just past the cart, so ~0.2 mm steps; ownership of a pixel is then the nearest item, as the depth buffer
decides; no back surfaces in this mode), then the flat shaders (with the clipping-plane sweep for depth). Only a region
around the cart (all bounds plus 80 px) is read back. Every file records `probe_mode`, the shader, the test's numbers
and the `roi` (all pixel coordinates are inside it, rows upward).

**Second run, 2026-10-16 00:05: every load skipped.** The build holds neither `Hidden/Internal-DepthNormalsTexture` nor
`Unlit/Color`; `Hidden/Internal-Colored` drew 0 px. Rebuilt without probe shaders: first **native** (the items' own
materials drawn into a depth-only render target, copied with the engine's blit into a float texture: outline and
distance in one render; reversed z handled), then **slice** (the items' own materials on a black and on a white
background, a pixel the item covers being the same on both; distance by sweeping the far plane in 1 cm steps; in-game
sweeps skipped in this mode, they come from the atlas offline), then the flat shaders. The test result is in every file.

## Third run, 2026-10-16 00:08: complete

194 loads probed, 0 skipped, no warnings; native mode (the items' own materials into a depth target) passed its test on
every load. `tools/probe/analyze.py <run> validate`: every load composed from the 29 atlas entries alone matches the
game's render: outline IoU median 0.994, 5th percentile 0.960 over 690 items (per type: log 0.997, tall crate 0.996,
corpse 0.994, low crate 0.994, flour sack 0.992, slab 0.988; zombie 0.960, min 0.746: every test zombie gets another
head, so one atlas zombie stands for none of them exactly); the visible part off by 0.1 % of the outline at the median.
So any layout can be judged offline from the atlas. `calibrate`: on the 64 ok loads a pair's mutual cover reaches 509 px
and an upper item's air gap 60 px, while the two "before" states of today's fix measure only 105-140 px of mutual
cover: raw pixel counts do not separate ok from wrong; which pair cuts and where has to be calibrated on the user's
judgement. `tools/probe/sheet.py <run>` writes `label-sheet.html` (60 sweep pictures spread over the measures, the 4
"before" variants, 10 ok loads as controls) for ok / unsure / wrong marks.

## Calibration, round 1 (2026-10-16): 74 single pictures marked

61 ok, 10 wrong, 3 unsure; all 10 controls (ok loads) marked ok. `tools/probe/fit.py` composes every marked state from
the atlas (with the cart body from `atlas/_cart.depth`) and measures pair covers (one-way, mutual, same layer, upper by
lower), the air under upper items, items over the cart and the cart over items, the change of each against the tuned
state, and (separately) how far items hang past the bed. No single measure separates wrong from ok better than 0.887
(calling everything ok scores 0.859): several wrong marks change almost nothing measurable (the same load is ok with
the crate 8 cm lower and wrong with it 4 cm sideways), and 10 examples cannot pin a finer rule. Next: **series** rows
(`tools/probe/series.py` -> `series-sheet.html`): one item moved -8, -4, 0, +4, +8 cm along one axis side by side, the
user marks the wrong ones; 45 rows covering distinct contacts and axes. They give labels near the user's line (for the
fit) and, directly, how far each contact may move per axis (for a tuner).

## Calibration, round 2 (2026-10-16): 45 series rows marked

180 moved states (41 wrong, 139 ok) plus the 45 tuned states. **The tuned place was never marked wrong (45 of 45)**: the
hand-tuned values hold for every contact in the sheet. `tools/probe/series_fit.py <run>` lines each state up against 3D
facts (box overlap and gap with the partner, past the bed's sides and ends) and the game's pixel measures of the sweep
(cover by the cart, mutual cover, air gap). No measure beats calling everything ok (0.772); mutual cover reaches 0.806.
What the marks show instead (+across is toward the far side, away from the camera):

- An upper item ("on") moved 4 cm toward the far side looks wrong on a corpse, slab or zombie (rows 13, 14, 15, 37, 44),
  but 8 cm toward the near side is fine; exceptions: low crate on zombie (17) and slab on log (34) are fine both ways.
  The box overlap does not change at all along these moves.
- Two neighbours end to end ("along"), moved into each other: wrong from +4 cm for crates (1, 10, 35), from +8 cm when a
  flour sack is involved (12, 40), never against a log (25).
- Neighbours out of line: two identical crates are wrong 4 cm out of line sideways (24) or in height (45); low crates
  in height are fine even at 8 cm (31, mostly behind the rails); mixed pairs only at 8 cm or not at all.
- Upper item up / down: the flour sack sinking into a corpse (9) and the low crate rising off a corpse (20) are wrong at
  4 cm; on a slab or zombie 8 cm either way is fine (3, 4, 11, 30).

So the eye judges direction, neatness (lined-up identical crates) and which side is seen, not overlap or pixel counts;
the box sizes in the layout are too coarse for a 3D rule (most tuned pairs already "overlap" by their boxes). The
allowed band is often only 0 to 4 cm, finer than the compositor's measures separate. Conclusion: the probe cannot judge
looks automatically; it stays a tool (the atlas compositor and the series pictures), and tuning stays by eye with the
review strip. The bands above are the useful result: per contact, which directions have room.
