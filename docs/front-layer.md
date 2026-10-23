# The front layer: a mount's near side drawn in front of the rider

How a skin draws part of itself **over** the rider, so he sits inside the creature instead of on a flat picture:
the dragon's folded near wing is the first use (2026-10-20). Everything here was learned on it, over twenty-two
drafts; the next mount with a near-side part (a saddle's near flap, a howdah's wall, a shoulder plate, a mane) should
take the recipe in section 7 and skip the detours in section 8.

## 1. What it is

A skin is one picture per frame. A **front layer** is a second picture per frame, in `front\` with the same file
names (or `sheet_front = front.png` for a sheet pack, the same cells), on the same canvas. The game draws it above the
rider while he rides and just above the creature when nobody does. Nothing else changes: same frames, same offsets,
same colours and colour rules, same checks.

What it buys: depth. The rider sits **between** the far side of the creature (the body picture) and its near side
(the front picture). The viewer reads the near side as closer than the rider because it covers him.

## 2. How the game draws it (`src/GK2DonkeyAPI/Looks.cs`)

- Each body part slot has an overlay renderer for the skin and a second one, `Front`, for the front picture
  (`Rig.Attach`). `Resolve(game, front: true)` finds the front picture for the game's current frame the same way the
  body picture is found.
- While the keeper rides (`Mount.Instance.KeeperBody` is his `bdy` sprite), the front renderer is placed on **his**
  sorting layer, at **his order + 2** (the near-leg overlay of RiderLeg is his order + 1), and **lifted towards the
  camera past the nearest of his sprites** (`Mount.Instance.KeeperNearestDepth`: his body, head, hands and clothes,
  whichever sits nearest in the body part's space) by `Looks.Rig.FrontLift`, 0.05 world units, 2.5 art pixels along
  the sprite's own facing. Nobody riding: the part's layer, its order + 2, the ordinary `OverLift`.
- The placing runs in `Camera.onPreCull`, after every LateUpdate (the game's seat follow included), so the depth it
  reads is this frame's.

Two bugs taught the lift:

1. **Order alone is not enough.** The game's sprite material draws by distance, and the rider's sprite plane sits in
   front of the mount's. Sorted above him but not lifted, the wing was drawn behind him (the first readback, 2026-10-20).
2. **A fifth of a pixel is not enough.** Lifted by `OverLift` (0.004) past his body plane, the wing flickered with his
   shirt while he moved: the depth buffer cannot tell the two planes apart at the camera's distance, so each pixel
   went to whichever won the rounding that frame. 0.05 is well past the buffer's step.

**Correction (2026-10-23):** the lift is *not* along the view axis. It runs along the part's own -z, and the camera
looks down at an angle, so it also moves the front picture down on the screen:
- **The ground:** 2.7 art px lower than the body picture, in both facings (`[occlusion]` readout). The folded wing that
  was judged good on 2026-10-20 includes this drop. Aligning the ground means drawing the fold about 3 px lower, so
  the look stays.
- **Flight:** 3.1 art px, measured on the user's video. Flying, the lift now runs along the camera's line of sight
  (`Follow(..., alongView)`), and the readout reads +0.0 / +0.0. See section 9.

Neither is visible on the review page or in the Mods picture: only the world draws through the game's camera.
That is why the front-layer self-test reads the world back (section 6).

## 3. The one rule for the art

**Everything in the front picture covers the rider wherever the two overlap.** There is no per-pixel depth: a front
pixel is in front of all of him, a body pixel behind all of him. So the **shape** of the front picture decides what
stays visible, and nothing else does.

The rule that came out of the dragon (the user, 2026-10-20): **no part of the rider may show in front of a near-side
part**. The wing is nearer the camera than he is, so a cut that left his chest over the membrane read as wrong at a
glance, however reasonable it looked in the strips. What shows of him is what the shape leaves out, full stop.

Consequences:

- Draw the creature's near side **as the creature has it**, from references, and let the rider be inside it. Do not
  shape the near side around the rider: a membrane built to cover his lap grew a lobe that stood out like a thumb
  when nobody rode (section 8). The unridden creature is drawn far more often than the ridden one.
- Decide what the shape leaves out of him by **measuring**, not by hoping: his pixels on the canvas (section 4) against
  the front picture's pixels. The dragon's wing leaves 197 of his 702 pixels in view (hair, part of the face, the
  reins arm); that number is in the strips before anyone rides.
- Legs or other body parts under the near side are **not drawn**, and the near side reaches the ground over them, so
  nothing of the body peeks out under its hem in any frame (section 5).

## 4. Where the rider is on the canvas

The rider's hip sits on the skin's `seat` row (docs/seat-investigation.md). The probe's `rider.png`
(`BepInEx\GK2-asset-probe\`, exported by tools/GK2AssetProbe) is his standing frame; `tools/skins/review.py`
places it the way the game does (hip 7 px above his feet, on the seat row, the skin's RiderOffsetPx and SeatAdjustPx
applied). On the donkey's 96 frame with the hip on row 52 (the dragon: 68 on its 128 canvas, the same 16 px in on
both axes), facing left:

| Part | Rows | Columns |
|---|---|---|
| hair and face | 17 to 30 | 50 to 65 |
| shoulders and the reins arm | 31 to 43 | 40 (the hand, forward) to 65 (his back) |
| torso and belt | 44 to 52 | 46 to 63 |
| legs and feet | 53 to 59 | 47 to 62 |

So a near-side part that starts at his back (column 58 and beyond, below row 31) hides his cape and coat; one that
rises above row 31 between columns 50 and 65 takes his head. The dragon's wing peak stands at (52, 26), above the
shoulder where the references put the wrist: it covers his chest, shoulders and the back of his head, and leaves
the hair, the face's front and the arm.

## 5. One piece with the body, and off the ground

The near side **moves with the body as one piece**: every point of it takes the walk bob, none sways on its own.
The dragon's wing once had a peak that bobbed and swayed a pixel over a hem pinned to the ground row: the
membrane and its finger lines were re-rasterised between the two every frame and the whole wing jiggled in the
world (the user's video, 2026-10-20). The measure that catches it before the game: per walk frame, the front
picture's first and last rows and the peak's column against the body's top row; the differences must be the same
in every frame.

And a mount that walks keeps its near side **off the ground**. The references draw the curtain to the ground only
in the big resting pose; the red walk frames and every blue frame tuck the wing against the flank with its lower
edge at the belly line and the legs in view below it. A hem on the ground had to swallow the hind legs (one not
drawn, the other under it), had to be kept out of the bob, and sat in the drawn ground by a pixel or two whatever
row it took. The tucked wing needs none of that: both hind legs drawn, the belly band under the hem, the hem on
row 60 at the hip, and the wing tip pointing back along the tail to column 99 over the rump (the blue
references' long wedge from the wrist to past the hips; the user's mark, 2026-10-20: the wing had looked too
small for the dragon, and growing it backwards keeps it off the ground).

## 6. The tools and the tests

- **Generator** (`tools/skins/creatures/make_creatures.py`): `dragon()` keeps two pixel buffers, `px` (the body
  picture) and `fpx` (the front picture); `put(x, y, colour, dst)` chooses; the outline pass runs round the union of
  both so there is no line along the cut between them. `folded_wing(flap, far)` draws the near wing into `fpx` and the
  far wing into `px`; `legs(far)` skips the near hind leg.
- **Checker** (`tools/skins/check_skin.py`): `front\` cells get the same size, colour and stray checks; it reports how
  many pictures the front layer has.
- **Review** (`tools/skins/review.py`, the strips and `out\<name>\preview.png`; `tools/skins/skin_preview_html.py`,
  the page): the composite is base, skin, rider on the seat row, front picture on top, exactly the game's order.
  **Look before handing over** (docs/sprite-tools.md step 5 and the art hand-over checklist): render standing and a
  walk frame at 1x and the page's 3x, with and without the rider, next to the reference crop at one scale; count
  the rider's pixels under the front picture; name every remaining difference from the reference.
- **Self-test** "Front layer: the dragon's near wing stays in front of the rider (a readback)" (docs/self-tests.md,
  group Looks): the dragon worn and ridden; the front picture, the body overlay and the keeper's body rendered alone
  and in pairs through the game's own camera (`RenderAlone`), the overlap judged by colour; the depths of every sprite
  of his and the front picture's in the body part's space (INFO); then 40 frames with him moved a little each
  frame, the wing read back with him, alternating the shipped lift and the old fifth of a pixel, failing on any
  overlapping pixel that shows him at the shipped lift. It is the only check that sees the world's drawing.

## 7. The recipe for the next mount

1. **References first**, read at pixel size (crop and enlarge them). Decide what the creature's near side is when it
   rests, with no rider in mind.
2. **Measure the rider** on your canvas (section 4, shifted by your seat row and canvas inset) and write down what
   the near side will cover and what it leaves. If it leaves too little (the face), move the near side, not him;
   if you must keep him visible, the honest answer is a shape that does not reach him, not a cut.
3. **Draw the near side into the front buffer** and everything behind him into the body buffer. Parts of the body
   under the near side are not drawn. The hem of anything that hangs ends on the feet's row, solid along the ground.
4. **Scan**: `check_skin.py`, the canonical layout, SkinsScan; a leak scan over every frame if the near side reaches
   the ground (copy the dragon's).
5. **Look** (the checklist): strips, the page, the reference comparison; the rider's visible pixel count.
6. **Build and ride it**: both facings, walking; look for flicker (a lift problem, section 2) and for anything of
   him in front of the near side (a shape problem, section 3). Run the Looks group: the front-layer scenario proves
   the order and the lift in the world.
7. **Record** the numbers and the drafts in docs/mount-looks-design.md with the creature.

## 8. How the dragon got here (2026-10-20)

Twenty-two drafts in one day, each judged done by the person who drew it and sent back by the user, until the
checklist and the measurements replaced the hoping:

- A raised wing towering over the back hid the rider (the user's first note: fold it, occlude the lower body, keep
  the near side visible).
- Flat folds and fans along the flank read as a plank, a line, a blanket, a flag, a hand fan (v1 to v7). The
  references read at pixel size fixed the silhouette: a tall wrist peak above the shoulder with the claw hooking
  forward, fingers running down, a scalloped curtain to the ground behind the hindquarters, wine against the red.
- A curtain at the tail, one too high, a far wing of its own shape, and a root at mid-back ("on his ass") came and
  went (v8 to v11); v12 rooted both wings at the shoulder.
- v12 to v14 cut the near wing: behind him above a row, in front below it, then also in front behind his back. The
  user asked whether the whole wing used one occlusion; the answer was no.
- v15 to v17 put the whole wing in front but shaped it around him (peak behind his head, a lobe over his lap): fine
  ridden, a thumb sticking out of the fold unridden. No reference draws it.
- v18 went back to the reference's silhouette with the cut; the rider's chest showed in front of the membrane.
- v19 is the one that holds: the reference's silhouette, wholly in front, the rider inside it with hair, face front
  and arm out. A lowered-peak variant (whole head out, the claw on his beard) was rendered and not taken.
- Then the world: the wing behind him (no lift), the wing flickering with his shirt (a fifth of a pixel of lift),
  the near hind foot read as part of the wing (the leg drawn in front), the hem sunk into the ground (the hem 2.5 px
  under the feet), then the whole wing jiggling (a bobbing, swaying peak over a hem pinned to the ground). The
  lift past the nearest of his sprites by 2.5 px fixed the first two; the last three went away together when the
  wing was read again from the walking references and tucked against the flank, one piece with the body, its hem
  at the belly line above the legs (section 5).

## 9. Flying: the spread wings (2026-10-23)

The flying frames split the spread near wing between the two pictures. The full story, with every measurement, is
in docs/flying.md ("The rider behind the spread wing"). The short version, for the next pass:

- **This report was not read before that work began.** It cost a day: the box, the band and the head window were
  all cuts, which section 3 already rules out. The composite checks passed while the game was wrong, which section 2
  already warns of.
- **Where it stands.** The near wing is front layer except over the rider's head (`rider_in_view`: x 64-82 up to row
  37, x 64-75 for rows 38-42), so his head and face stay in front while the wing covers the rest of him. That is a
  cut, and section 3 says the honest answer is a shape. With the lift fixed (section 2) and checked on the user's
  video in both facings, it shows no seams and no clipped hair. Whether to keep the cut, or have the raised wing
  cover his head for those frames as the rule says, is the user's call.
- **How to check next time.** Use the world: the front-layer readback self-test (section 6), the `[occlusion]` log
  lines (`Looks.FrontReadout`: the front picture against the body picture, and the facing), or the user's video
  registered at its pixel scale. A composite only checks the frames against my own assumptions.
- **The three choices, measured (2026-10-23; the preview page, the studies' head and body counts per frame):**

  | | his head covered | him covered (worst frame) | the beat |
  |---|---|---|---|
  | A the cut (shipped) | never | 28% | as drawn |
  | B the rule (the whole wing in front) | 7 of 13 spread-wing frames, up to 168 of his 192 head pixels | 52% | as drawn; he nearly vanishes at the top of every beat |
  | C the shape (raised pose leaned back 35 degrees, `DRAGON_RAISED_TILT`) | never (2 px at most) | 33% | the top of the stroke much lower: raised and level look alike |

  The user is to choose by eye.
  - **In game:** the Flying tab's "Wing over the rider (a study)" row (A / B / C, switches at once,
    `Flight.WingLook`).
  - **The frames:** the dragon skin carries B and C as variant frames, `bdy_<clip>_b_left_NN` and
    `bdy_<clip>_c_left_NN` (back and front), made by the generator's `WING_STUDIES`. The scans accept the `_b`/`_c`
    suffix.
  - **Once chosen:** remove the variants (`DRAGON_WING_STUDIES=0`, the suffix in the scans, the row).

Related: docs/skins.md (the pack format), docs/mount-looks-design.md (the dragon), docs/sprite-tools.md (the
pipeline and the hand-over checklist), docs/seat-investigation.md (the seat row), docs/self-tests.md.
