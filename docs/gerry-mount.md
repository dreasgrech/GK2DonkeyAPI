# Gerry as a mount: what the game has, and how to make him one

The idea (2026-10-18): a giant Gerry, the talking skull, as a mount, with baby skulls as his foals. This is the
investigation: what sprites the game ships for Gerry, what a mount skin needs, and the ways to get from one to
the other. Nothing is built yet.

## 1. What the game ships

Found offline with a bundle reader (UnityPy, Unity version 6000.3.9f1 set by hand: the game's bundles carry no
version string) over `GraveyardKeeper2_Data\StreamingAssets\aa\StandaloneWindows64\`:

| What | Where | Detail |
|---|---|---|
| The character | `npc_gerry.prefab`, `npc_gerry.asset` (bundle `9afd076d…`) | A character prefab like the donkey's (`npc_donkey`), with its own animation preset `0901_gerry` (the donkey's is `0301_`). |
| His pictures | the character atlas bundle `8f5cc77b…` (the same `Characters` atlas the donkey's frames are in) | 73 sprites named `0901_<part>_<animation>_<frame>`, every one a 96×96 canvas at 50 pixels per unit, pivot centre: **the donkey's base exactly**. |
| A sound | `gerry_somersault.ogg` | For the somersault below. |

The 73 frames:

| Part | Animation | Frames | Drawn size (px) | What it looks like |
|---|---|---|---|---|
| `bdy` | `walk_left` | 15 | 14 wide, 16 to 26 tall | A hop: the skull rises, the jaw drops open mid-air (frames 3 to 7), lands and closes. |
| `bdy` | `walk_down` | 15 | 15 wide, 18 to 28 tall | The same hop seen from the front, jaw open at the top. |
| `bdy` | `walk_up` | 15 | 15 wide, 15 to 25 tall | The same from behind: the back of the skull, the jaw's hinge showing below. |
| `bdy` | `somersault_left` | 16 | 14 to 16 wide, 15 to 29 tall | A flip: he rises, turns over fully, lands. |
| `hed` | `static_left`, `static_down`, `static_up` | 1 each | 14 to 15 wide, 16 to 18 tall | The resting skull, three facings. |
| `hed` | `speak_left`, `speak_down`, `speak_up` | 3 each | as the statics | Mouth frames for talking. |

So Gerry is a small skull, about 15 pixels wide and 17 tall at rest (the donkey's body is 50 wide and 47 tall), drawn
in warm bone colours with dark sockets. He has no legs; walking is hopping with the jaw chattering. He faces three
ways (the donkey only faces left, flipped for right).

To look at the frames: `tools/skins/gerry/gerry_probe.py <bundle folder> <out folder>` exports one PNG per frame (trimmed to
the drawn pixels, as the atlas packs them) and prints the list above; `tools/skins/gerry/gerry_sheet.py <out folder>` draws two
contact sheets next to them (`gerry_sheet.png`, every animation at 2x; `gerry_zoom.png`, the walks, the somersault
and the heads at 8x). Both need `pip install UnityPy` / Pillow. The exports are the game's art: for looking at, not
for the repository.

## 2. What a mount skin needs

A skin on the donkey base is 44 pictures (docs/skins-design.md): `bdy_static_left`, `bdy_walk_left_01..08`, the idle
animations (`ear` 5, `graze` 5, `chew` 3, `lips` 13, `turn` 6) and `hed_static_down` ×3, all left-facing on a 96 (or
larger) canvas, drawn so the rider's seat lands on the back line. The game plays the donkey's clips and the skin
re-points the pictures per frame key, so a Gerry skin needs a picture for each of those keys, with Gerry's hop
spread over the donkey's 8-frame walk and something sensible for the idles.

## 3. Three ways to make him

**A. Scale the game's frames (quick, chunky).** A new skin source that reads the game's `0901_` frames from the atlas
at run time (the recipe machinery already reads the donkey's `0301_` frames back from the atlas: `LooksRecipes.
FindGameFrames(base, prefix)` takes any prefix), maps the donkey's keys onto Gerry's (walk 1..8 ← hop frames 1, 3,
5, 7, 9, 11, 13, 15; static ← `hed_static_left`; the idles ← the somersault and the speak frames; `hed_*` ← empty) and
blows them up ×3 with nearest-neighbour onto the 96 canvas (45 wide, 50 to 87 tall: fits). Zero drawing. The cost:
his pixels would be three times the size of everything else's, which pixel art shows at once. Worth building first
because it is a day's work less than the others and shows at once whether the hop reads as a mount; the rider sits
on top of the cranium.

**B. Own art, Gerry as reference (consistent, more work).** Draw a big skull in `tools/skins/creatures/make_creatures.py` like the
rat, snail, worm and dragon: Gerry's palette and features (the sockets, the nose hole, the teeth, the cracks, the
hinge), the hop as the walk cycle with the jaw as a separate layer (the `hed` part: the donkey's head part is free
for it, so the jaw can animate on its own keys and the rider's near-wing trick isn't needed), the somersault as the
`turn` idle. Pixels at the world's size, so he fits the game. The rider sits on the cranium with the hitch gap and
seat keys set in his mount file.

**C. Both: A now, B when A has been seen.** A's key map and the mount file (seat, hitch gap, foals) carry over to B
unchanged; only the pictures change.

## 4. The babies

Foals are the same skin scaled by the view (0.6): with B, baby skulls come free, ~27 px wide; with A, 0.6 of a ×3
blow-up is ×1.8, closer to Gerry's own size. A mount file says `foals = true`. Their sounds are the donkey's for now
(skin sounds are phase 2). *As built: a mount says its babies' size (`foal_scale`, Gerry 0.33 = his own size), and a
baby spawned without a size is its parent mount's; plain baby donkeys stay 0.6 whatever the parent (the plan's M4).*

## 5. What else Gerry could do (abilities, phase 2)

- **Somersault** as an idle or on a key (`MountAbility.Tick`), with `gerry_somersault.ogg`.
- **Talk**: the speak frames on the jaw layer while the keeper stands next to him.
- **No cart** (`cart = false` in the mount file) unless a skull pulling a cart is the joke; the hitch gap makes
  either work.

## 6. Recommendation, and what is built

Build A as a built-in recipe-style source ("game creature" source: prefix + key map + scale), look at it in game,
then decide on B. The source mount is generic: any character preset in the atlas (`09xx_`, `03xx_`) becomes a
candidate mount the same way, which is the open, scalable path the skin system wants.

**Built 2026-10-18 (A):** `source = game` skins (`LooksGame.cs`, docs/skins.md): the character's frames found in
memory or through a loaded sprite atlas asked by name, mapped by the `[Game]` lines and the rules, scaled by whole
pixels with the feet on the donkey's ground row (`SkinBase.GroundRow`, 28) and the centre on the pivot, into the
skin's sheet like any other picture. Self-test "game creature: Gerry's frames found, scaled and worn".

**As it stands (2026-10-20, after the parity plan):** `src/SaddleUp/assets/skins/gerry/skin.cfg` (Saddle Up's
since the evening, docs/mounts-as-mods.md; `src/GK2DonkeyAPI/assets/skins/gerry/` before) in the canonical
layout: Gerry at 3x on a **160** canvas (a 29-pixel hop at 3x is 87 above the ground row; 128 cut it), `lift = 0.25`
(a quarter of his own hop under a rider), walk 1..8 spread over his 15, the somersault as the turn idle, the speak
frames as lips and chew, the head keys empty; **`seat = picture`** (the rider sits on the skull's own top, per frame:
the seat readout reads 25.5 px in both AutoSeat modes) with the skin's own `[Tuning] SeatAdjustPx = -16` (the skin
owns its drawing's geometry; the mount file's "seat lifted 10 px" is gone); the Gerry mount (`[Mount]`) with cart
and babies, **`foal_scale = 0.33`** (baby skulls his own size), **`noun = skull`** ("a skull can't pull the cart"),
and `[Mount.Tuning] LipsVolume = 0` (a skull does not smack lips; the bray and the hoofsteps stay; since the sound
system his `[Sounds]` says `lips = none`, and since the mount folders, 2026-10-24, the mount's facts and sounds are
`src/SaddleUp/assets/mounts/gerry/mount.cfg` and the skin `skins/gerry/skin.cfg` beside it). **Seen in game**
on 2026-10-18 (the first run: the skin failed to register at first, the babies were huge and the hop too big; the
fixes above came from it) and in the seat readout of 2026-10-20 (25.5 px in both AutoSeat modes, the Gerry scenario
green). Not recorded as settled from that first run: a clipping at the top of the canvas and a dark square behind
the ridden skull (the Instances tab's "Log drawing" probe was added for them); whether the 3x pixels read next to the
world is the user's judgement (B, own art, stays open and is not planned). His `[Colors]` (Dark =
the sockets) is written by `tools/skins/skin_colors.py` from the game's export (`BepInEx\GK2-asset-probe\game-skins\
gerry\`, the Mounts tab's "Export the loaded skins as built") when one exists; until then every colour is Coat.

**Another game character, by hand** (docs/sprite-tools.md "Making a skin"): `python -I tools/skins/gerry/gerry_probe.py
<game folder> <out> <prefix>` finds the addressables bundle holding `<prefix>bdy_walk_left_01` and exports the
sprites; `gerry_sheet.py <out> <prefix>` lays them out to measure the hop and the sizes; copy Gerry's mount folder
(`assets/mounts/gerry/`: its `mount.cfg` and `skins/gerry/skin.cfg`) under the new mount's id, set the prefix,
scale, lift, the frame map and the seat in the skin.cfg and the mount's facts (`id`, `name`, `default_skin`, ...) in
the mount.cfg; `python -I tools/skins/canonical_cfg.py`, the scan, build, look. No pictures of our own, nothing of the game's art in the repo.
