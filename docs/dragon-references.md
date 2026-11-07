# A new dragon skin or a new dragon mount? The nine references (2026-11-07)

The user asked whether to buff up the Dragons Mount Pack with a new skin of the dragon or a new dragon mount. They
put nine references in `Pictures\GK2 Saddle Up Mounts References\Dragon Types`. This file covers:

- what each reference shows, close up and at the players' size;
- what the pack and the API allow;
- what each path costs;
- the recommendation.

No pictures were drawn. The reference images stay out of the repo.

## 1. What decides it: the wings

A skin is a look, and a mount is the facts: speed, cart, cargo, flying, babies, jump and sounds
(docs/making-a-mount.md "Two things, not one"). The dragon's `mount.cfg` says `flies = true`. Its default skin has
120 main pictures and 120 front pictures, and 76 of the 120 are flight frames that are mostly wing.

- **A skin can't take the wings away.** The flight frames still play whenever the dragon flies.
- **A winged dragon** is therefore a skin candidate.
- **A dragon with no wings** can only be a new mount, with `flies = false`.
- **A second winged dragon with the same facts** is a skin by definition. With different facts, it becomes a new
  mount that sits on the speed ladder.

| Reference | Wings | So it is a candidate for |
|---|---|---|
| command-dragon2 (sketch, with a head study) | none shown | a new mount only |
| steelshard-adult-illo | none shown. The same creature as command-dragon2 (horns, mane and crystals): its finished painting, cropped | a new mount only |
| summer-season-zamrok | bat wings | a skin |
| wooly | bat wings | a skin |
| lunar-dragon | feathered wings | a skin |
| desert-dragon | bat wings | a skin |
| forest-dragon2 | bat wings | a skin |
| psychic-dragon-adult | bat wings | a skin |
| psychic | bat wings, folded (sitting) | a skin |

**To confirm:** that command-dragon2 and steelshard really are wingless. Neither picture shows wings, but the
painting is cropped.

## 2. Each reference, close up

The crops were made at 2.5-3x for the 600 px images and at 0.6x for the 2500 px sketches.

- **command-dragon2.** Its features:
  - a long, low, wingless quadruped;
  - two long horns sweeping forward, with glowing cyan tips;
  - a shaggy blue mane from the crown down the back of the neck;
  - a blue throat and belly under dark brown-black scales;
  - dark spikes on the back, with a run of glowing cyan crystal shards over the hips;
  - a long thick tail with dark spots and small spikes;
  - heavy clawed paws.

  The sketch has a tiny figure for scale, so this is a huge animal.
- **steelshard-adult-illo.** The same design, painted:
  - scaled brown armour;
  - a blue belly with pale blue spots;
  - crystal blades on the back and shoulders;
  - the mane.
- **summer-season-zamrok.** An armoured war dragon with silver plate barding:
  - a skull helm;
  - overlapping plates down the neck and back;
  - a chest plate;
  - leg greaves;
  - bands on the tail.

  It also has a dark blue body, curled black horns, bat wings with tan membranes and claws on the joints, and a mace
  of spikes on the tail. Its title says War Dragons: it is that game's character.
- **wooly.** A gold dragon with:
  - a thick shaggy brown mane over the head, neck and shoulders;
  - a yellow beak-like face;
  - grey ringed ram horns;
  - a white scaled throat;
  - brown spots on the haunch;
  - fur along the back and tail;
  - orange bat wings.
- **lunar-dragon.** A pale silver dragon with:
  - big feathered violet wings (a fan of long feathers, like a griffin's);
  - one crescent horn sweeping up and back;
  - violet fins on the head and neck;
  - violet claws and tail plates;
  - a long thin tail ending in a curl.
- **desert-dragon.** A sand-coloured dragon with:
  - a long drooping snout and a chain hanging from it;
  - blue riding tack: a breast plate, saddle rings and bracers;
  - a red-brown underside and wings with pale spots.
- **forest-dragon2.** A green dragon with:
  - gold ornaments on the head and chest;
  - antler-like branches;
  - a floating flame-shaped ornament over the head;
  - swirls on the wings;
  - a tail ending in a spiral curl.
- **psychic-dragon-adult / psychic.** Two takes on one idea:
  - a cobra hood behind the head;
  - a banded belly up the neck (green or yellow);
  - round gems down the chest (pink or cyan);
  - a purple body;
  - thin membrane wings;
  - a tail with a gem or eye spot.

  The sitting take has blue flame wisps at the head.

These are other artists' and games' characters. What we take from them is the type (shaggy, armoured, crystal,
feathered, hooded), drawn our own way, never one of their designs redrawn.

## 3. At the players' size

Each creature was cut out and shrunk to our dragon's width: 107 px on the 128 canvas, its standing frame. The
results are shown at 3x next to the Red Dragon (the scratchpad sheet `refs\gamesize.png` of this session).

**These features survive:**

- **The mane (wooly, command):** a big dark mass round the head and neck, the strongest change of silhouette.
- **The cobra hood (psychic):** a broad shape behind the head that no other dragon has.
- **The feathered wings (lunar):** the fan of feather tips reads, and the violet shows against the pale body.
- **The silver barding (zamrok):** pale plates on a dark body, the spiked tail mace, the helm.
- **The forward horns and the cyan crystals (command, steelshard):** they read as bright dots and blades.

**These vanish:**

- forest's ornaments and wing swirls (only the green and the curled tail stay);
- desert's chain and tack details (a blue patch on tan);
- psychic's gems (dots, which read only next to the hood).

**A rule from our dragon's poses** (graze, turn, take-off, climb, dive, landing): the head and neck go from upright
(standing) to low and forward (grazing, flying).

- Anything on the head or neck has to follow that in all 120 frames.
- Anything on the back sits under the rider while he rides (seat row 62). Back spines and crystals are a standing,
  journal and baby look. The head, neck, tail and wings are what a rider shows off.

## 4. What the pack and the API allow

**A skin of the dragon:**

- It is a variant, `saddleup-dragon.dragon\skins\<name>\`, built on `default`.
- It draws only what it changes. With `layer = over`, it draws its additions over the Red Dragon's pictures.
- It can have its own `[Sounds]`, its own named parts (`[Tone.*]`, `[Region.*]`) and its own seat.
- It keeps every mount fact: Slow, flies, no cart, babies.
- The babies wear it.
- No shipped mount has a variant yet. docs/open-items.md "Skins built on their default, by eye" asks for exactly
  this, on the dragon. So a dragon skin also tests the variant system for real.

**A recolour alone is no skin.** The Blue Dragon was dropped on 2026-10-19 because the player's colours do that
(make_creatures.py, beside `CREATURES`). Every candidate has to add a shape the Colours window can't make.

**A new mount** can differ only in the `[Mount]` keys `MountPacks.cs` reads:

- `cart`, `cargo`, `speed`;
- `flies`, `jumps`, `foal_scale`, `foals`;
- its `[Jump]`, `[Stats]`, `[Tuning]` and `[Sounds]`.

There is no key yet for indoors, light, water or any ability. Those are code (docs/next-update.md section 3, bin 3).

Today's ladder (docs/next-update.md):

| Mount | Speed | Cart | Cargo |
|---|---|---|---|
| Donkey | Normal | yes | 6 |
| Gerry | Fast | yes | 4 |
| Giant Rat | Very fast | yes | 2 |
| Giant Worm | Slow | yes | 4 |
| Giant Snail | Very slow | yes | 6 |
| Dragon | Slow | no | no cart; it flies |

A wingless drake's natural place is **the dragon that pulls the cart**, the one thing the Red Dragon refuses. But
every open slot either duplicates a mount or beats one:

- Normal with 6 is the donkey;
- Fast with 6 beats the donkey;
- Fast with 4 is Gerry;
- Slow with 6 beats the snail and the worm.

**The niche is the user's decision.** Without a new ability, the drake is a look with a speed. With one (say a
light from its crystals at night), it is a code feature first.

## 5. What each path costs

**A skin** (the pack's 1.3.0):

- **The drawing:**
  - Up to 120 main and 120 front pictures: the 44 ground frames plus the 76 flight frames.
  - The donkey's draw-once tools (`stamp.py`, `rig.json`) know only the donkey, and the dragon has no rig. The
    dragon was drawn by code, `dragon()` in tools/skins/creatures/make_creatures.py, which knows every part's place in
    every frame. A dragon skin is therefore a feature of that function (a mane, plates, a hood: drawn along the
    neck, the back and the tail it already computes), written as a variant folder.
  - The quality bar is the Red Dragon itself (19 colours, clean bands, no dithering) next to the references at game
    size.
- **The parts:** named parts as tones, every part its own (docs/mount-colours.md 7.10), e.g. "Mane", "Armour".
- **Texts:** the skin's name, its description and its part names in the pack's `Languages\` (11 languages).
- **Checks:** SkinsScan, check_skin, the preview page and the art hand-over gate (docs/sprite-tools.md step 5).
- **Release:** the pack's CHANGELOG, its README, and a Nexus and Workshop note.
- **The skin's local id is permanent in saves.** It is chosen with the user.

**A new mount** (also the pack's 1.3.0):

- **The id:** a new `saddleup-dragon.<name>` folder, permanent in saves.
- **The files:** its `mount.cfg`, and a default skin of 44 ground frames (no flight frames if it doesn't fly), its
  own body drawn by code.
- **The cart:** its geometry, `[Tuning.Cart] HitchGapPx` and the "cart readout" self-test, and a long tail that
  clears the cart behind it.
- **Sounds:** the dragon's growls copied into its own `sounds\` (pitched) or new files.
- **Colours and texts:**
  - its regions and tones, and `colors.json`;
  - its name, description and parts in 11 languages;
  - its journal card and stats.
- **Checks and release:** SkinsScan, check_skin, the pack's CHANGELOG and README, the Nexus and Workshop texts, and
  media.

That is several times a skin's work before the niche question, and the niche has no answer that doesn't duplicate
or beat a mount.

## 6. The skin candidates, ranked

Ranked by four things: what reads at game size, how far the shape differs, how well it fits the game, and the cost.

1. **Armoured (from zamrok, with desert's tack):**
   - **What it adds:** a helm, plates down the neck and back, a chest plate, tail bands and a spiked tail end.
   - **Reads:** strongly (pale steel on a dark body).
   - **Fits:** the game's Inquisition and medieval setting. It is a matched set with the Donkey Skin Pack's Armored.
   - **Cost:** plates along the neck and tail curves in all 120 frames; the back plates sit under the rider.
2. **Shaggy (from wooly, with command's mane):**
   - **What it adds:** a mane round the head, neck and shoulders, ram horns, and fur tufts on the legs and tail.
   - **Reads:** the strongest silhouette change, and it stays in view under the rider (head and neck).
   - **Fits:** a highland beast, warm and a little funny: it suits the game's humour.
   - **Cost:** the mane follows the neck from upright to low.
3. **Hooded (from the two psychic pictures):**
   - **What it adds:** a cobra hood behind the head, a banded throat and gems down the chest.
   - **Reads:** strongly, the hood above all.
   - **Fits:** stranger, an occult feel, which suits a graveyard.
   - **Cost:** the hood's shape changes with the neck's angle: the hardest head piece.
4. **Feathered (from lunar):**
   - **What it adds:** feathered wings and a crescent horn.
   - **Reads:** clearly, as a different animal.
   - **Cost:** the wings in all 76 flight frames, a second wing system. The first wings took twelve drafts
     (art-handover memory, 2026-10-20), so this is the costliest and riskiest.
5. **Crystal (steelshard's crystals and mane on our winged body):**
   - **What it adds:** cyan shards and a blue mane.
   - **Reads:** the mane carries it, and the shards are mostly hidden by the rider. It is close to Shaggy with
     glints.
   - **Best saved for:** the wingless mount, if there is one.
6. **Not on their own:** forest (only green and a curled tail survive: a recolour plus a tail) and desert (the tack
   is Armoured's idea, lighter).

## 7. The recommendation

**Make a skin first: Armoured or Shaggy.**

- It adds a real new look to the pack with no new facts and no ladder problem.
- It is the pack's first variant, which the open items have asked for.
- The generator already knows every frame.

Keep the **wingless drake** (command and steelshard) as a later new mount, once the user picks its niche: a cart
puller at a speed and cargo of their choosing, or a code ability first.

**The next step, once the user picks skin or mount:** two or three rough directions as single frames (standing,
flying, grazing) at 1x and 3x next to the Red Dragon. The user picks one before any 120-frame work starts. This
follows the references-before-drawing rule.

## 8. The user's pick and the rough directions (2026-11-07)

**The user's choice:** "ok lets do armored skin for the dragon". They also confirmed that command-dragon2 and
steelshard are wingless, which keeps them for a later mount.

**The rough directions** were drawn over the Red Dragon's standing and flying frames. They follow the Armored
donkey's rules (its `bdy_static_left` at 8x):

- big plates, each with the steel line `281B26` round it;
- a lit top row, then a row of rivet dashes;
- a darker lower band;
- a brass rim, with a cloth hem under the body plates;
- lames overlapping down the neck, the one nearer the head lying over the next.

The three directions:

- **A, knight's barding** (the Armored donkey's set):
  - a helm with an eye slit;
  - a crinet of lames down the neck;
  - a chest plate with a navy hem (a red hem vanishes on the red dragon);
  - a side plate with one seam and a hem (in flight);
  - a rump plate and a tail guard.
- **B, war plate:**
  - blackened steel, no cloth;
  - blades swept up off the neck's lames;
  - a beak spike;
  - a blade on the wing's wrist;
  - a longer banded tail guard with a fan of blades at its end.
- **C, light:** the helm, the crinet and a short tail guard only; the red dragon shows.

**What the frames show:**

- **Standing:** the folded wing covers the body, so the head, neck, chest and tail carry the armour.
- **Flying:** the side plate shows round the rider.

**The grazing frame is left out of the roughs.** Lines picked by hand on its short, thick neck didn't sit cleanly.
In the skin, the armour will follow `dragon()`'s own skeleton in every frame.

The study script, `armour_rough.py`, and the boards are in the session's scratchpad. They are not shipped.

## 9. The second direction: radically different (2026-11-07)

**The user, on the first roughs:** "none are really that different from original default skin so i think we have to
also modify the horns and definitely also the wings ... They need to be radically different than the default skin
otherwise whats the point."

**Why the first roughs failed:** plates painted over the finished frames left the Red Dragon's outline, wings,
horns and colours as they were, so only a grey coat changed.

**What changed in the approach:** the skin is now drawn by the dragon's own generator, as
`dragon(style="armored")` in `tools/skins/creatures/make_creatures.py`. The creature is `armored-dragon`, drawn only
when it is named, into a folder of its own. Every frame changes, flight included:

- **Body:** dark indigo scales under steel plate (the Armored donkey's steel, its line `281B26`, and its brass). The
  plates are:
  - a helm over the skull and the snout, with an eye slit, an ember eye, a gilt brow band and a spike on the snout;
  - a crinet of lames down the neck, with the throat left in scales;
  - a chest plate, a flank plate and a crupper, with seams between them, rivets along their tops and a brass rim at
    the belly.
- **Horns:** an open ram's curl, black and ridged, instead of the two straight bone horns.
- **Wings:**
  - leather instead of wine;
  - the trailing edge cut deeper (a ragged war wing);
  - a steel-clad leading edge with seams at the joints;
  - a steel spike off the wrist;
  - hooked black claws at the finger tips, only where the membrane reaches the tip;
  - on the folded wing, a steel arm and a spike standing off its peak.
- **Ridge:** steel blades, fewer and bolder, instead of bone spines.
- **Tail:** steel bands, blades along its top and a spiked mace at its end.
- **Legs:** steel greaves on the forearms and shins, and black claws.

**Checks:**

- **The Red Dragon is unchanged.** It was regenerated into a scratch folder and compared: 237 of 237 pictures equal
  to the pack's.
- **The Armored Dragon's 237 pictures are clean:** no loose pieces and nothing touching the picture's edge. The
  check is the scratchpad `islands.py`.
- **Spikes and claws stop two px short of the edge,** measured where `put()` lands them (the pitched frames shift
  columns).

**Not a skin yet:** no skin.cfg values, colour regions, tones or texts, and it is in no pack. It is a direction for the
user to judge.

**Study 3 (the same day):** a fresh reviewer, given only the board and the user's words, said study 2 was still a
recolour. The wings kept the Red Dragon's outline and fan of ribs, the plates read as pale patches, and the one horn
read as a handle. Changed after that:

- **The spread wings have a new outline:** four heavy two-px fingers with deep cuts between them, and no fan of veins.
- **The folded wing:** three heavy fingers and two deep cuts in its hem.
- **The plates are banded the Armored donkey's way:** a lit top row, a light row, rivets, a darker body and the steel
  line. Dark hide shows between plates and between the neck's lames.
- **A red accent:** a brass rim and a red skirt with a dagged hem under the flank and hip plates, and a red plume on a
  crested helm (the Armored donkey's skirt cloth, 38121A to B23A32).
- **Horns:** two swept-back horns with a gap between them.
- **The tail:** bands and a three-spike mace; the blades along it are gone.

The Red Dragon is still 237 of 237 equal. The Armored Dragon has no loose pieces and nothing at the edge, in 26
colours.

**Study 5 (the same day):** the user, on study 3: the red skirt and the red on the head "look out of place", and "we
need to make the armor actually look like armor, not looking like a painted skin". Studies 3 and 4 recoloured the
body's own pixels with the body's own shading, which is why it read as paint. Changed after that:

- **No red:** brass rivets and trim are the accent.
- **The armour is pieces of its own,** drawn over the scales (`plate()` in `dragon()`). Each is a polygon on the
  skeleton, standing a px proud of the body's outline, with:
  - its own flat metal shading: a near-white edge, a light row, the face, a darker band and a dark bevel;
  - a hard line round it where it meets the hide or another plate;
  - a shadow on the hide under it.
- **The pieces:**
  - a face plate (chanfron) over the snout and brow, its lower edge under the eye, the jaw in scales;
  - a crinet of overlapping lames, the throat in scales;
  - a chest plate with a lit ridge and a brass rim;
  - two flank lames, the upper with brass rivets;
  - a crupper;
  - tail rings wider than the tail;
  - greaves on the near forearm and shin.
- **Colours:** the steel is brighter at the top (near-white edges) and the hide darker, so the plates stand off it.

The Red Dragon is still 237 of 237 equal. The Armored Dragon has no loose pieces and nothing at the edge.

## 10. Head and wing shapes to pick from, and the game's own armour (2026-11-07)

**The user, on study 5:** "let's work more on the shape of the head because i dont like it and let's also work on
the shape of the wing", and "look at how armor looks in game ... so that we make it match the game's art style".

**Shapes as switches** (`DRAGON_ARM_HEAD`, `DRAGON_ARM_WING` in make_creatures.py; the board is the scratchpad's
`heads_board.png` and `wings_board.png`):

- **Heads:**
  - **beak:** a beaked war helm hooked down past the snout, a dark socket round the eye, a crown of three blades
    fanning back;
  - **greathelm:** a closed knight's helm with a flat front, the visor slit, breaths, a crest and horns through it;
  - **wedge:** a long flat head, a narrow ridged face plate, fangs, horns swept back flat.
- **Wings:**
  - **bat:** study 5;
  - **pointed:** a long swept first finger, hooked claws and a taller folded peak;
  - **saw:** five fingers, the edge cut in straight notches, and steel tips.

**The game's own armour** is in the `BepInEx\GK2-sprites` dump: `sprites.csv` plus the atlases. Its rects are
bottom-up (Unity): measured on 429 sprites, cut as given 33% of the crop's border was opaque, flipped 2%. Two sources
show metal:

- **The keeper's knight armour `9019_`** (the MainCharacter atlas, in real colours; the loose `9019_*.png` are
  palette masks):
  - its steel is a cool green-teal, by count `1E3737 32443F 565B4E 6A7062 7A8778 9AB5A6`;
  - a mint glint `C1E6D9`;
  - a dark teal line `192C2D`;
  - gold trim, the exact four colours of the Armored donkey's brass (`7D5308 A4800B CFA90F F1D274`) with brown darks
    `461D04 512706 623707`;
  - chainmail as a two-tone checker;
  - a quartered red and blue tabard.
- **Soldier `0351`:** blue-grey iron with rust specks and a kettle hat (`0628`, `0725` and `0726` are such hats).

**What was taken:** the Armored Dragon's steel is now the knight's ramp and line, and its gold was already the
game's.

**What the game's metal has that ours doesn't yet:**

- glints on rounded parts;
- chainmail;
- rust, for an iron variant.

**Round 2 of the shapes (the user: "make more variations of b and c", and the wings "C wise for details but we need
more variation to it because right now it looks too similar to default skin"):**

- **B heads (closed helms):**
  - **B1, greathelm:** last time's.
  - **B2, greathelm-cross:** the game knight's helm, with a rounded dome, a gold band down its front and across the
    brow, and a mint glint.
  - **B3, greathelm-bull:** a boxy bucket helm with a grille and big bull horns curving forward.
  - **B4, greathelm-sallet:** a rounded helm whose tail flares back over the neck, a blade crest and a gold band.
- **C heads (the long head):**
  - **C1, wedge:** last time's.
  - **C2, wedge-jaw:** a jaw guard, a crest of spikes and short horns.
  - **C3, wedge-snarl:** the jaw dropped open, fangs top and bottom, and horns curling down.
  - **C4, wedge-rhino:** a horn on the nose.
- **Wings,** the saw's details (five fingers, straight notches, steel tips) on new outlines; the fingers are turned
  about the wrist:
  - **saw-long:** closed up and longer, a long narrow wing swept back, with a fold swept up;
  - **saw-claw:** the notches almost to the wrist, a hand of talons;
  - **saw-blade:** spread wide, with blades along the arm and the folded arm.
- **A clean-up step:** a scrap under 12 px not joined to the dragon is dropped (the slivers notches leave at tips).
- **Checks:** the Red Dragon is still 237 of 237 equal, and all eleven variants are clean.
- **The boards** (scratchpad): `heads_B.png`, `heads_C.png` and `wings_board2.png`.

## 11. The pick, the wing's detail, and a test version in the game (2026-11-07)

**The user's pick:** head B1 (`greathelm`) and the saw-blade wing, adding "the wing need a lot more on the detail
because theres a lot of blank wing space. And let's work on the shape more. And then put a version of it in the
game".

**The wing's detail** (the saw wings, `make_creatures.py`):

- **The membrane in pockets:** lit along each finger, darker between two and out to the edge, with stretch lines
  across it (rings about the wrist, a full shade lighter so they read).
- **Veins:** a thin vein to every notch's inner point, and two inner veins to the back.
- **Battle wear:** two tears through the membrane, one through the fold.
- **Steel:** a band with a brass rivet round each finger, and brass rivets along the steel forearm.

**The shape:**

- the notches alternately deep and shallow (a war-torn edge);
- longer steel finger tips;
- a big hooked thumb claw off the wrist.

**The test version** is a loose folder in the game install only. It is not in the pack, the repo or a zip:

- **Where:** `<game>\BepInEx\plugins\gk2mounts\saddleup-dragon.dragon\skins\saddleup-dragon.armored\`, beside the
  Jester's. It holds the 237 pictures of `DRAGON_ARM_HEAD=greathelm DRAGON_ARM_WING=saw-blade make_creatures.py
  armored-dragon <folder>` and a hand-written `skin.cfg`.
- **What the skin.cfg sets:**
  - the skin is built on the default (its canvas, seat and tuning), every picture its own;
  - tones for Armour, Gold trim, Wings, and Horns and claws;
  - the regions Scales (Coat), Belly (Muzzle) and Eyes (Eye), the outline as Dark with no row.
- **The id** carries the pack's prefix, so a saved choice survives its move into the pack.
- **check_skin:** 44 of 44 frames and 117 front pictures. One note, a false alarm: `161218` is drawn only in flight
  frames, which it doesn't read.
- **After every rebuild,** copy the whole folder again with the game closed (delete it first: no stale files).
  Remove it if the skin is dropped.

**On the preview page (the user, the same day):**

- **"weird gray artifacts on the wing":** the steel finger bands and their rivets read as scraps at this size. Removed
  from the spread wing and the fold.
- **"gold pixels sometimes flashing" on the helmet:** the great helm's brass rim. Its bottom row came and went with
  the head's half-pixel moves: gold in walk 02 and 04 only. The helm has no rim now, and every other rim is one solid
  gold, not two alternating, which shimmered as the plates moved.
- **"two spike black looking things at the back":** slivers of the fold one or two px wide, left by the notch beside
  its tip, which were all outline. There is no notch beside the tip now, and the membrane's spurs (a cell with under
  two of its four neighbours) are trimmed twice, in the fold and the spread wing.

**Checks:** the Red Dragon is still 237 of 237 equal. The study, the loose copy in the game and the page were all
refreshed.

**The frame-by-frame pass** (the user: "a full frame by frame check to eliminate all artifacts and moving parts",
then "make sure to always look at the final frame by frame result"). The tools are two scratchpad scripts:

- `strip.py`: a clip's frames side by side, and under each, in magenta, what changed from the frame before that is not
  a part moving (each 6x6 block matched at its best shift, up to 3 px).
- `flicker.py`: per clip, the pixels that blink (A B A).

What was wrong, and what was done:

- **The ridge blades jumped along the neck:** they were picked by their column's distance from the head's column,
  which moves. They now sit at fixed columns of the body (two on the neck's root, one behind the saddle).
- **Spikes and claws changed shape:** they were drawn from fractional positions. They now sit on whole pixels.
- **The blues alternated:** the bob and heave, the fold's rock, the neck's nod and the tail's sway were fractions of
  a pixel, so the shading bands slid every frame.
  - Everything now moves by whole pixels.
  - In the walk the body is one rigid piece with one bob and no heave of the shoulders against the hips, which
    reshaped it.
  - The neck and head move as one piece, by the head's rounded nod.
  - In flight the neck, the tail, the hump, the chest's lift, the haunch, the legs' sway and the spread wing's joints
    are all on whole pixels.
- **The white line read the two folds as one wing:** the folded arm is a mid steel now, the far fold darker.
- **A greave vanished in walk 06:** it was the forearm less 2 px, nothing on the swing's foreshortened forearm. It is
  now a fixed length, centred on the bone.
- **The stretch lines on a wing seen edge-on were noise:** they are now drawn only 2 px inside the membrane, and
  tears only where the wing shows enough of it.

**Measured** (strip.py, changed px with motion taken out):

| Clip | Before | After | Notes |
|---|---|---|---|
| walk | 5242 | 3185 | the rest is the legs stepping |
| lips | 28 | 0 | |
| chew | 78 | 0 | |
| ear, turn | 104 | 104 to 134 | the tail tip's flick |
| graze | | 1996 | the head lowering |
| glide | 2799 | 2086 | the near wing's own flex |
| fly, takeoff, land | | | the wing beat |

**Checks:** the Red Dragon is still 237 of 237 equal. The study, the game's loose copy and the page were all
refreshed.

**The cost:** the walk's shoulder and hip heave is gone, for a steady body.

## 12. The second frame-by-frame pass (2026-11-07)

The user, on walk 01 and 02 in the preview page: "that example still has the flashing armor part between frames
there so its definitely not good. did u do the frame by frame analysis? this would have been immediately spotted".

**Why the first pass missed it:** `strip.py` takes motion out, and the walk's leftover was put down to "the legs"
from its count, without looking at where it was. The lowest neck plate showing and hiding sat in the same count.

**How it was checked this time:** close-ups of every clip, the main and front layers apart, looked at frame by
frame; `shiftmatch.py` (a box around a part against frame 1 at its best whole-pixel shift: 0 px left means a rigid
move) to tell a part that moves from one that changes; a measurement run that prints each frame's neck length and
plate count (`measure_neck.py`, the generator run with one print added); then an independent review by a fresh
agent with the same tools.

What was wrong, and what was done:

- **The neck's lowest plate came and went** (walk 5 4 4 5 5 4 4; graze 3 to 5; flyup2 4 3 4; takeoff and landing
  2 to 4). The plates were counted from each frame's neck length, a plate every 5.5 px while one fitted, and the
  walk's bob stretched the neck's root by a pixel. Now always four: 5.5 px apart on the ground and 4.4 in the air,
  where the neck is shorter, closer only on the in-between frames of take-off and landing.
- **The neck shimmered in flight and in the glide:** each neck point followed the beat at its own lag and was
  rounded on its own, so the neck bent by a pixel differently every frame and the plates along it turned. Now, as
  in the walk, the neck and head move as one piece by the head's whole-pixel move, and the plates are laid out on
  the neck's resting shape and moved with it.
- **The greaves' gold blinked** in the walk: a brass rim on a small plate that turns with the leg is 0 to 3 px
  from frame to frame. The rim is gone, as the helm's was.
- **Rivets jumped along their plate:** they were picked by `(x + y) % 3` on the canvas, so a plate that moved a
  pixel moved its rivets along its row (the flank's front rivet blinked beside the wing). They now count from the
  plate's own corner.
- **The helm's breathing holes jumped a row** in the glide: they sit half a pixel under the head's row, and
  Python's `round()` takes a half to the even number, so with the head one row lower they landed a row lower on
  the helm. For the Armored Dragon `put()`, `get()` and the spikes' and claws' bases now round a half up, the same
  offset on every row. The Red Dragon keeps its rounding.

**Measured:** four neck plates in all 117 body frames. The helm is the same picture, only moved, in every frame
of fly, glide, flyup2 and flydown2 (`shiftmatch.py`, 0 px left). The walk's leftover is the legs stepping and the
tail's tip; the glide's is the far wing's flex.

**The colours:** `skin.cfg`'s lists follow the pictures again: the gold is one colour (CFA90F), the far fold's
dark steel (32443F) is armour, not scales, and 161218 is no longer drawn. `check_skin.py`, beside the dragon's
default and its `mount.cfg`: all good.

**The independent review** (a fresh agent, the same tools, not told what had been fixed) then found more, fixed:

- **Gold on the wing's steel edge blinked** in flight (fly 04 and 07, the glide's way in and out, takeoff, landing):
  the forearm's rivets were cells within half a pixel of the arm's middle line, which only some angles have. They
  are gone. Now every frame of every clip has the same 12 gold pixels, the body plates' rims and rivets.
- **The tail's rings came and went** (3 or 4 in flight) and slid along it: counted in three quarters of each
  frame's tail length and placed at a share of it. Now always four, 8, 14, 20 and 26 px from the tail's root, each
  its fixed size.
- **The wing's steel edge crawled in the glide:** the glide's flex moved the arm a pixel at a time and re-laid the
  whole edge. The arm now holds the glide's pose and the fingers flex on from it, so the wing still bends like a sail.

Kept, and said here so they are a choice:

- **The hip plate rises 2 px with the downstroke** in the beat (the hump, the Red Dragon's too), against the saddle,
  which holds still.
- **The helm's crest changes once** between take-off 01 and 02 (and landing 01 and 02), the frame the head moves
  4 px from the ground pose to the flying one.
- **The folded wing's tear** shows the body moving behind it, 2 or 3 px in the walk.

**Checks:** the Red Dragon is still 237 of 237 equal. `check_skin.py` all good. The study, the game's loose copy and
the page were all refreshed, and the page holds all 237 of the game copy's pictures.

**Then the user, on the page's walk:** "the head and neck ... are now constantly moving up and down in a disconnected
way from the body, it looks like theyre not attached", and the ridge blade on the neck "moving all over the place
during animation again". The cause was the fix for the shimmer. The head kept the Red Dragon's walk (it holds its
height and nods while the body bobs under it), and the neck, one stiff piece with the head since the tenth pass, slid
against the chest instead of bending, by a pixel each way in a little circle every step. The blade, standing on the
neck's top at a fixed column, moved against the neck's plates. Now, in the walk, the neck and head ride the body's
bob with no move of their own. Measured with `tools/skins/frame_check.py attach` (the head's move against the
body's, per frame): before, up to 1 px each way every frame; after, 0 in walk, ear, turn and lips. The head, the neck
plates and the blade are one picture in every walk frame, moved with the bob (`frame_check.py shift`: 0 px left).

**Kept, a choice:** in flight and the glide the head still nods 1 px against the body, held for 1 to 4 frames: the
glide's nod is the user's ask of 2026-10-23 (the head looked static).

**The tools** are now in the repo, `tools/skins/frame_check.py`: `closeup` (a clip's frames enlarged, main and front
layers apart), `shift` (a part only moved, or changed), `attach` (a part's move against the body's) and `gold` (a
colour's pixels per frame).

## 13. Into the Dragons pack (2026-11-07)

The user, after the in-game look (the colours window with every row): "works in game. Let's have it in the mod now
so that we can commit it before continuing."

- **Where:** `packs/GK2SaddleUpDragonsMountPack/gk2mounts/saddleup-dragon.dragon/skins/armored/`, skin id
  `saddleup-dragon.dragon/armored`, a variant built on the Red Dragon (docs/skins.md "Variants"): 237 pictures,
  byte for byte the build seen in game, and the `skin.cfg` tested there (version 1.0, its header updated,
  canonical). The pack's DLL embeds it with the rest of `gk2mounts\`.
- **The generator:** `DRAGON_ARM_HEAD` and `DRAGON_ARM_WING` default to greathelm and saw-blade, and a plain
  `make_creatures.py` run draws the Armored Dragon with the others (`ONLY_WHEN_ASKED` is empty), so the pack's
  pictures always follow `dragon()`.
- **Words:** 8 lines in each of the pack's 11 `Languages\*.csv` (the skin's name, its 4 tones, its 3 regions), in
  the order the dev run writes them. The translations reuse the packs' own words (the donkey's Armored, the dragon's
  Belly). `tools/l10n-glyphs.py` flagged 爪 and 鳞 for Chinese: reworded as the dragon's default already says them
  (角和脚, 身体). `l10n-sync --check` differs until a dev run with the new pack writes its English.
- **Checks:** SkinsScan ALL PASS (44), `canonical_cfg.py --check` clean, the pack builds with no warnings.
- **Gone:** the loose copy in the game (`plugins\gk2mounts\saddleup-dragon.dragon\`, deleted once the game closed)
  and the study folder (the page shows the pack's skin). A save that wore the loose copy shows the Red Dragon until
  the Armored Dragon is picked again (its id changed).

## 14. The polish pass (2026-11-07)

The user, after the commit: "take a good look at the skin and make sure its consistent between frames. we need to
polish the armored skin as much as possible before releasing. analyze well".

**The method:** an inventory instead of samples. `frame_check.py sweep` takes every frame pair of every clip (the
loop's seam too), takes the motion out block by block, and lists each group of pixels still changed with the shift
its block took: moved 0 or 1 px yet changed is SHIMMER (a part changing in place), moved more is a part moving and
bending. `frame_check.py heat` draws it per clip. Each place it shows was then looked at close up.

**What the inventory showed:**

| Clip | Where the leftover is | Read |
|---|---|---|
| lips, chew | nothing | the same picture every frame |
| ear, turn | the tail's tip and its rings | the rings shimmered as the tail flicked |
| walk | the legs, the tail | the legs step; the rings and greaves were re-laid every few degrees |
| graze | the head lowering | a pose change |
| fly, climb, dive | the wing beat; the tucked legs' small loops | motion |
| glide | the far and near wings' flex, the tail | the near wing's veins re-laid as its fingers flex |
| take-off, landing | the wings opening and folding | motion |

The seams of the loops have nothing the middle pairs don't. The near shoulder's light band slides as the upper arm
swings: a turning limb's shading, left.

**Fixed: plates drawn as stamps.** A thin plate laid along a bone that turns a few degrees was re-laid in other
pixels every frame. Measured first (each ring's angle per frame): in ear and turn each ring stays within 4 degrees
(24.7-24.8, 36.6-39.9, 41.5-45.5, 27.9-29.0); in the walk they span 18-49.
- **The tail's rings:** the tail's direction at each ring snapped to 15-degree steps whose edges (10.5, 25.5, 40.5,
  ...) no standing frame crosses, the anchor on a whole pixel. The rings of ear and turn are now the same picture,
  only moved; the walk's change shape twice a cycle, at the tail's fastest frames.
- **The greaves:** the bone's direction snapped to 22.5-degree steps, the middle on a whole pixel, and lit from above
  like every other plate (they were lit along their bone: the light row jumped from the plate's end to its side as
  the forearm swung level). The swing shows a few fixed greaves.

**Measured:** ear and turn's in-place shimmer 8 groups to 6 (what is left is the tail's tip moving); the gold steady
(the same visible count every frame of walk, ear and glide; in the beat the wing covers more or less of it). The
other mounts' pictures unchanged: a plain `make_creatures.py` run changes only the Armored Dragon's 141 pictures.
SkinsScan ALL PASS (44).

**Offered, not changed (the user's motion asks):** the glide's near-wing flex (its veins re-laid as the fingers bend:
less flex would calm it, the user asked for the sail's bend on 2026-10-23); the tucked legs' small loops in the beat
(asked for after held legs looked dead); and the standing clips now nearly still (lips and chew identical every
frame, ear and turn only the tail's tip), a result of the whole-pixel work.

**The independent review** of the pack's pictures (a fresh agent with `frame_check.py`, told the known items, not
the fixes) then found, and these were fixed:
- **The helm's crest changed for a frame in the climb** (flyup1 04: its light top turned to its dark edge; flyup2 04
  one pixel). Measured, not guessed: the head point is a whole pixel in every frame (17,25 then 17,26), and a log of
  every pixel written there showed only the crest plate itself, with one cell more in that frame. Its top edge runs
  exactly through a cell's centre (x 1.5 from the head, y -9.5), and in floating point 25 - 9.6 and 26 - 9.6 round
  differently. Now `plate()` puts its corners on a 1/64 px grid first, so such a tie falls the same way on every row:
  the helm is the same picture in every frame of all six beat clips.
- **Grass pockets between the wing and the hip** at the top of the upstroke (fly, flyup1, flydown1 08; land 03;
  glidein 02; 5 to 35 px): the saw wing's notch on its last span, by the body, opened one; with it gone, the wing's
  edge still lay across the tail rings' tops and shut pockets in between. The notch is gone, and a pocket of 40 px or
  less that the near wing borders is the darkest hide behind it, a shadow under the wing. The single-pixel gaps between
  the legs and claws are left: they are the drawing's.
- **Not changed:** walk 07's single grass pixel between the forelegs (a gap closing).

**Checks:** a plain `make_creatures.py` run changes only the Armored Dragon's pictures; SkinsScan ALL PASS (44);
`canonical_cfg.py --check` clean; the visible gold the same in every frame of walk, ear and glide; deployed; the page
holds all 237 pictures.

**Then the user, on the page:** "how consistent are those three vertical rectangles on the front leg regarding
shading between the frames? ... are they alternating? also those three white lines on the tail are moving a lot".
- **The tail's rings:** they were. Snapped to 15-degree steps, the walk's sway carried a ring's angle across a step's
  edge and back (ring 4: 28, 23, 35, 37, 28, 18, 23, 23 degrees across 25.5), a 15-degree flip several times a
  cycle. Now on the ground each ring has one fixed angle, the resting tail's at its place (sway 0), in every
  standing and walking frame, and rides along with the tail; in the air the tail whips through 30 degrees, and the
  rings follow its own angle there, unsnapped.
- **The leg's three bands** (light, mid, dark along the bone): not alternating. In every frame the light band is on
  the lit side and the dark on the far side; they turn with the arm. Measured between neighbouring frames on the
  upper arm, with each frame's best whole-pixel shift: 14 to 33 of its ~170 blue pixels change where it turns a little
  and 55 to 94 where it swings. The bones now start and end on whole pixels (the solver's fractions re-laid the
  bands a little every frame: 04->05 went from 30 to 18).
