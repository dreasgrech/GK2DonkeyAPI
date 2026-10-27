# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

For players:

**The mounts are here.** 0.10.0 had them built in but switched off; this version switches them on. In short:

- **Six mounts to ride**, picked in the Mounts panel (the Mount section's Mounts button, or its key):

  | Mount | Download | Riding | Cart | Its thing |
  |---|---|---|---|---|
  | Donkey | GK2 Saddle Up | Normal | 6 items | the all-rounder; skins Hellfire and Royal Parade |
  | Gerry | GK2 Saddle Up | Fast | 4 items | the game's talking skull, three times his size; he talks |
  | Giant Rat | GK2 Saddle Up: Critters | Very fast | 2 items | the courier: quick, a light cart |
  | Giant Worm | GK2 Saddle Up: Critters | Slow | 4 items | a quiet hauler |
  | Giant Snail | GK2 Saddle Up: Critters | Very slow | 6 items | a slow, full cart |
  | Dragon | GK2 Saddle Up: Dragon | Slow | no cart | **flies**: over water, fences, cliffs and roofs |

- **Each mount pulls its own load** (the user, 2026-10-27): the faster it is, the less its cart holds. A cart holds
  the mount's number or your Cart capacity, whichever is smaller; switching to a lighter mount never drops anything
  (the cart stays full until you unload it). The Mounts panel shows each mount's speed and cargo.
- **Skins:** the donkey has Hellfire (horns, a devil's tail, glowing eyes, lava cracks; its ring of fire comes in a
  later update) and Royal Parade (a saddle cloth and a crown; the cloth follows your harness colour). Other mods and
  skin packs can add mounts and skins (a folder `gk2mounts\<mount>\` with a `mount.cfg` and `skins\<skin>\`;
  docs/making-a-mount.md).
- **Everything is kept per mount:** its colours (coat, harness, second tone, the cart's paint, wheels, hubs and
  lantern), its name, its babies and its voice, all set in the Mounts panel. Your 0.10.0 donkey keeps everything it
  had.
- **Babies are the same mount and skin** (a setting): a dragon's young are little dragons, Gerry's are skulls their
  own size, each baby a little different from its siblings.
- **Every mount jumps** (Space); the dragon takes off with a second press in the air. Not in a fight, where Space is
  your attack. The silent ones (the snail, the worm) are silent by choice; the dragon's, the rat's and Gerry's own
  get-on, get-off and answer sounds come later.

Everything else in this version, in detail:

- **Each mount has its own name, babies and voice, set in the Mounts panel** (the user, 2026-10-26: "make the amount
  of babies be saved per mount"; "users need to set their mount names from inside here"). The journal page lists, for
  the mount picked: its Name (a text box: type it there, no more editing the cfg), its Skin, Babies (0 to 5), Babies
  follow you on foot, Babies come indoors, Babies are the same mount and skin, and Voice and steps (its sounds'
  volume); the babies rows only for a mount that has babies. They are in the same order on every mount. Lantern and
  Lantern lit by day moved into the colours window's Cart colours, per mount too. These left the options pages (Sound
  and Cart lose a row each; the Babies section is gone). What you had set carries over to the donkey; the other mounts
  start from it.
- **New controls in the Mounts panel** (the user, 2026-10-26: the "< >" arrows are "way too limiting"): a switch for
  on/off, a row of number buttons for the babies, a slider with its % for the volume, a dropdown with a scrolling list
  for the Skin and every colour preset, a text box for the name. The switches and the slider are drawn like the rest
  of the panel (its dark boxes with the faint edge, the knob and handle the game's red button face). The labels line
  up with the stats card's and are no longer cut off on the left; in the colours window each column has its labels
  on the left and its controls and colour squares on the right; the four buttons at the bottom have one gap between
  them.
- **Babies hop** (the user, 2026-10-26: "make the babies jump occasionally as part of their movement"): now and then
  in a step, every few seconds while they trot after you or their parent, and on the spot while they stand about. The
  hop is their mount's own jump, small. Each hop "holds the key" for its own time: mostly little hops, now and then a
  big jump. When you jump on their parent, the babies following it mostly jump too, each a moment after you, usually
  a big one. `ShuffleFoalHopChance` (0.2; 0: no hops) and `ShuffleFoalJumpAlongChance` (0.75) in the API's cfg.
- **Each baby is its own** (the user, 2026-10-26: "they all look alike ... Persisted randomness though so that they
  wont constantly change"): its own size (a little bigger or smaller than its mount's babies), its own shade of the
  parent's colours (lighter or darker, warmer or cooler, more or less saturated, the second tone and the dark parts
  such as the dragon's wings too; on a near-black coat some are charcoal and some have a red or wine cast), its own
  temperament (lively ones roam further, step faster, rest less and hop more; calm ones stay close and rest), its own
  trot, walk and voice. The same baby after every load; the donkey's babies come back the same after a while on the
  dragon. `ShuffleFoalVariety` in the API's cfg (1; 0.5 subtle, 2 strong, 0 all alike).
- **The babies' answers to a call sound less mechanical** (the user, 2026-10-26: they answered "sequentially one after
  the other with no variation at all"): a new order every call, uneven gaps (now and then two at once, sometimes a
  pause), and each voice a touch higher or lower.
- **Babies no longer stop following you for good** (the user, 2026-10-26: "sometimes they just stop following you
  completely after a while"): a baby following you on foot that fell behind, with its parent left far off, was frozen
  by the performance levels, and nothing woke it to follow again. A baby with you now always stays awake.
- **Babies left with a parent far away no longer wait there for good**: when their parent stood so far off that it
  was put to sleep (no picture), the babies took it for somewhere else and waited where they were until it woke.
  They now go to it, or catch up with it out of sight.
- **Switching to a mount with another baby size keeps the babies** (Gerry's are a third the size of the donkey's):
  they change size where they stand instead of vanishing and new ones appearing beside the parent.
- **Babies get stuck less** (docs/foals.md "Hops, getting stuck, and stopping for good"): a way round an obstacle is
  now one the baby can really walk (it was planned through grid centres and then refused, and the baby gave up at
  once); a refused way is planned again from where it stands; a lost baby looks for a way round from further off;
  one wedged where it stands still tries to rejoin; one lost for 10 s out of sight is brought to you even while you
  walk about. A baby that hasn't got nearer you for 20 s says why in the log (`[foal] ... has not got nearer`): send
  that line if one still gets stuck.
- **The cart key no longer brings the cart to a mount that can't pull it** (the user, 2026-10-26: "any mount that
  doesnt have the ability to pull the cart shouldnt bring it with g"): with the dragon (or any mount whose `cart =
  false`), G says it can't pull the cart and leaves the cart where it stands; before, the cart came to you and was
  refused only then.
- **The mounts are downloads of their own** (the user, 2026-10-26): GK2 Saddle Up itself ships the donkey, Gerry and
  the donkey's Hellfire and Royal Parade; the Dragon (GK2 Saddle Up: Dragon) and the giant rat, snail and worm (GK2
  Saddle Up: Critters) are mount packs to add, each a folder for `BepInEx\plugins\`, or all of them in one zip.
- **Kept donkeys and carts are never lost because something went wrong** (docs/multi-mod-loading.md): after an error
  took them away, the side file is left as it was instead of being rewritten or deleted by the next save; a side file
  older than its save is kept aside instead of being overwritten; the last one is kept as a backup; one that can't
  be read whole no longer loses the rest.
- **The Mounts panel lines up** (the user, 2026-10-25): no "Skin" heading over the Skin row, which sits under the
  stats card now; the mount's picture uses the whole height; in the colours window the custom colour squares and
  their labels line up with the other rows, and the headings sit on the left with a line under them.
- **No flash of the new mount when leaving the dragon in the Mounts panel** (the user: "that mount appears large and
  without a cart and then the cart appears"): the dragon's picture stays until the new mount's is ready, then it
  shows whole with its cart.
- **No cart options for a mount that can't pull one** (the user, 2026-10-25: "for mounts that cant pull the cart, the
  drive the cart button still appears in teh journal and there's still cart customization"): with the dragon picked,
  the Mounts panel has no Drive the cart button and its colours window no Cart colours. Your cart colours for it are
  kept; every mount that pulls the cart shows both as before.
- **The dragon jumps like a dragon** (the user, 2026-10-25: "the dragon feels like a plompling rock with the jump
  ... maybe it can have something akin to takeoff and landing kinda thing so it doesnt look like a super mario
  thing"): it gathers itself, pushes off on its wings, holds them open at the top and sinks onto the ground braking,
  then folds them, on its own take-off and landing pictures, with a flap. Higher and slower than the donkey's hop
  (36 px in a second against 28 px in under half). The other mounts jump as before.
- **The babies' answers are as loud as their parent's call** (the user, 2026-10-25: "i still cant hear the babies
  ever replying"; the log showed every answer playing, about 10 dB under the call once the babies' smaller size and
  their distance were counted). The dragon's babies answer with a lower growl too, not a chirp.
- **The giant snail squelches as it slides along** (the user, 2026-10-25: "sound those slime sounds while walking"):
  three more squelches from the slime recording, two a stride, as quiet as the donkey's hooves on grass. It was
  silent while moving.
- **The babies answer after their parent's call, so you hear them** (the user, 2026-10-25: "are we sure the foals
  are replying at all?"). They did answer, every time, but half a second after the call began, and the dragon's
  roars last two to four seconds: every answer played under the roar. Now they answer one after another once the
  call has ended. Gerry's lines are level with the donkey's bray too (they were 7 dB under it).
- **The new mount sounds sit at the game's own loudness** (the user, 2026-10-25: "the mouse squeaks sound so much
  louder than the dragon ones ... we should use the ingame sounds obviously"). Measured as the ear hears it (LUFS),
  every sound added today played about 10 dB over the game's donkey, and the rat's squeaks 3 dB over the dragon's
  roar: the game plays its own sounds at a volume of their own (0.375 for the bray), ours went in at full. Now each
  mount's cry is as loud as the donkey's bray, getting on and off as its lip smack, the babies' answers 6 dB under
  the bray, and the dragon's flight sounds keep their place against its roar.
- **New key: Call out (V)** (the user, 2026-10-25: "the donkey would bray, the dragon would roar"). While mounted,
  your mount gives its own cry on demand: the donkey brays, the dragon roars, the rat squeaks, Gerry talks; its
  babies answer. The snail and the worm have no voice and stay silent. The cries that come by themselves now and then
  are unchanged (the Bray sounds row switches those off; the key still works).
- **The dragon, the giant rat and the giant snail have sounds of their own** (the user's recordings, 2026-10-25:
  "can we fill some missing slots with our new sounds?"). The dragon roars with three growls (it used the zombie's
  cry pitched down), growls as you get on and off, thuds as it touches down after a flight, and its babies answer
  with the growls pitched up. The rat squeaks (it used the spitter's cry pitched up), squeaks as you get on and off,
  and its babies answer with small squeaks. The snail squelches as you get on and off (it used the fishing blop), its
  babies with a small squelch. Gerry's getting on and off and the worm's and Gerry's babies are still silent.
- **The dragon flies, for everyone** (the user, 2026-10-25: "make flying not dev only"). Riding it plain, outdoors:
  Space jumps, Space again in the air takes off, Left Shift climbs, Left Ctrl descends, Space while flying lands. It
  flies over water, fences, cliffs and roofs and comes down only where you can stand ("Nowhere to land here." while it
  can't); a save, H or a door lands it first. Every mount jumps with Space (hold for a higher jump, over what is lower
  than the jump). Not in a fight, where Space is your attack. Four rows in Keys: Jump, Fly, Climb (hold) and Descend
  (hold); Climb and Descend are new. Flying was built as a dev-only experiment since 2026-10-22; what is still to see in
  game is listed in docs/open-items.md ("Flying for players").
- **The dragon pulls no cart any more** (the user, 2026-10-25: "remove the ability for the dragon to haul the cart").
  Its card in the Mounts panel says "Can't pull the cart" and its Cargo "no cart"; G on the dragon (or Drive the
  cart in the panel) says "A dragon can't pull the cart.", and a cart hitched when your mount becomes the dragon is
  left standing where it is. What each mount is for beyond that is being decided (docs/next-update.md).
- **The Effect section is gone, and so is the effect in the Mounts panel** (the user, 2026-10-25: "remove all the
  stuff we're exposing regarding the effects"; particle effects will come back on specific skins, designed anew). The
  Effect, Effect shown and four toggle rows, the "Comes with" line and the effect half of the panel's
  "Colours and effect..." window (now "Colours...") are removed; the Hellfire skin no longer says it comes with its
  fire. The six Effect lines and the GK2 Donkey API's seven `[Effects]` lines leave your cfg files at the first start
  (no other setting changes).
- **The mount's picture in the Mods menu sits in its row again** (the user, 2026-10-25: "the mount is being offsetted
  out of the panel"). GK2 Mod Framework 0.1.24 moved a row's field from the row's middle to its top; the picture was
  centred on the field, so it stood over the section's heading. It is now level with the row's middle, across from
  the field.
- **Picking a mount in the Mounts panel no longer freezes the game for a moment** (the user, 2026-10-24: "when
  selecting the dragon from the mount journal, the game freezes for some frames"). The picture is framed on what the
  mount draws over two seconds of its walk: twenty photos, each read back from the graphics card, all in one frame (40
  to 60 ms and 19 to 26 MB a pick; the dragon's picture is the biggest). Now one photo a frame (the last picture stays
  until the new one is framed, so it never visibly resizes), each look's framing is remembered (picked again: framed at
  once), and one read-back buffer per picture replaces a new texture and array per photo.
  After that a micro freeze stayed (the user, 2026-10-25: "its the dragon which is doing the micro freeze when
  selecting it from the journal"), traced frame by frame: the click's frame took 13 to 20 ms against 8.3 (the cfg
  file written whole, 3.5 to 4.7 ms; the dragon's 15.6 MB sheet made and its first six pictures, 4.5 to 5; the picture
  re-posed, 2 to 4.5), the frame the dragon appeared up to 20 ms, and Gerry's and Hellfire's builds 17 to 44 ms a
  frame for eight frames (their pictures are read back from the game's sprites through the graphics card). Now the cfg
  file is written once the picking pauses or the panel closes; a look builds for 2 ms a frame, its sheet in a frame of
  its own; a finished look is worn from the frame after its upload; and every look the panel showed stays ready while
  it is open, so picking it again shows it at once. A first pick still had one slow frame (the dragon 25 ms: Unity
  zero-filling its new 15.6 MB sheet, 14.7 ms; Gerry 16 to 19 ms: every sprite in memory looked through for his frames,
  6.5 ms, and his first picture). The sheet is made without the fill (every cell is written anyway), Gerry's frames are
  found once a session (the game-ready check finds them; looked for again only when one is gone), his rest picture is
  measured in a frame of its own, and nothing of a build runs in the frame it was asked for.
- **Mounts scale better: switching, sounds and the panel's pictures** (docs/mount-switching-performance.md). The
  panel's picture of a look is framed once ever, not once a session: the framings are kept in
  `BepInEx\cache\GK2DonkeyAPI\framings.txt` under the look's pictures and the game's version, and a changed look is
  measured again. The picture's edge check (a whole read-back) runs for 5 s after a framing or a setting change instead of
  every second. A mount's own sound files load one a frame, are checked on disk once a session, and stay compressed in
  memory (they were all decoded in the frame the world's mount turned into the dragon: 15.6 ms of a 60 ms frame); a
  mount shown in the Mounts panel starts loading its sounds then. The looks the panel keeps ready are capped at 64 MB of
  sheets, the least recently shown going first. Closing the panel writes the chosen mount half a second later, not in
  the closing frame (that frame was 21 to 24 ms with the 4 ms write in it).
- **The babies of the mount you ride come through a door with it** (the user, 2026-10-24: "i selected gerry when
  inside the house and rode him out, and the babies didnt follow me outside"). They stayed: the house and the yard are
  one place to the game, so they waited for their parent to stand still out of reach, and a ridden parent never does.
  Now the ridden mount's babies near the door are placed beside it on the other side while the screen is black, in or
  out.
- **A skin of a mount is built on its default** (the user, 2026-10-24: "all other skins are built on top of it";
  docs/skin-layering-plan.md, docs/skins.md "Variants: built on the default"). Every skin of a mount but the donkey
  starts as a copy of that mount's `default` look: the pictures it doesn't draw, and its settings it doesn't give
  (seat, offsets, effect, colours, description, portrait, the skin's tuning and sounds), come from the default and
  follow it when it changes, so a new look can be one picture and a few lines. `layer = over` on such a skin draws it
  over the default (holes where its erase colour is, its pixels on top), the way Hellfire is drawn over the game's
  donkey. The donkey's own skins (Hellfire, Royal Parade) are exactly as before; Saddle Up's mounts ship only their
  defaults, so nothing a player has looks different. The loaders take each mount's `default` first; the Mounts tab
  (F11) says what a skin takes from its default and saves only its own values.

- **Packs are sorted by mount: a folder per mount, its skins inside it** (the user, 2026-10-24: "i want to see things
  sorted by mount ... a skin can only belong to one mount ... where are the sounds? Shouldn't we have some root mounts
  folder"). A pack is now `gk2mounts\<mount>\` (players: under `BepInEx\plugins\` or a mod's folder there; Saddle Up:
  `src\SaddleUp\assets\mounts\`): `mount.cfg` (the mount: `[Mount]`, `[Stats]`, `[Tuning]`, `[Tuning.Ride]`,
  `[Tuning.Cart]`, `[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`, plain names), its `sounds\` (the
  mount's files, for every skin of it) and `skins\<skin>\` (each look: `skin.cfg`, its pictures, `front\`, its own
  `sounds\`). The donkey's folder holds skins only (Hellfire and Royal Parade: `mounts\donkey\skins\`). Before, a mount was a `[Mount]` block inside its default skin's `skin.cfg`
  (the "declaring skin", the joiner and Default rules) and its sounds sat in that skin's `sounds\mount\`: a mount's file
  named after its event was also the skin's own, and the dragon's flight sounds loaded twice. Now a `skin.cfg` holds
  no `[Mount*]` section (said in its report), a skin's mount is its folder's, a mount folder without `mount.cfg` adds
  its skins to a mount registered elsewhere, two `mount.cfg` of one id meet the skins' version rule, and a pack mount
  goes with its last skin. The Mounts tab (F11) saves the mount's rows into `mount.cfg`. Nothing a player has changes (mounts and packs were off in every release). Tools (`skinroots.py`, `canonical_cfg.py`
  for both files, `new_skin.py --mount`, the generators, the preview page by mount), `tests/SkinsScan` (a pass per
  mount folder) and the self-tests (the temp packs, the id rules for mount packs, the sound folders) follow the tree;
  the embedded resources are `GK2SaddleUp.mounts.<mount>\...` and the dev write-back root `GK2MountsSourceDir`.
- **Every mount has one default look, `skins\default\`; skins are named within their mount; mount ids carry their
  mod's prefix** (the user, 2026-10-24: "every default skin directory called 'default' ... a mount must only have one
  and only one default skin"; "I dont think the skin ids being global is a good idea ... other mods adding more mounts";
  "saddleup specifically would have its own modifier"; docs/mount-id-collisions.md). A mount's id is `<mod>.<name>`:
  Saddle Up's are `saddleup.dragon`, `saddleup.rat`, `saddleup.snail`, `saddleup.worm`, `saddleup.gerry` (their
  folders too); an id without a prefix is refused, only the game's `donkey` is plain, so two mods' dragons are two
  mounts and a saved one never turns into another mod's. A skin's full name is `<mount>/<skin>`
  (`saddleup.dragon/default`, `donkey/saddleup.hellfire`): its folder's name, within its mount (`skin.cfg` has no `id =` any more),
  so two mounts may each have a `fire`. A mount's own look is its `skins\default\` (`[Mount] default_skin` is gone): a
  pack `mount.cfg` without one is refused with its skins, a folder adding skins to another mod's mount may not bring a
  `default`, the donkey has none (its own look is the game's). A skin added to a mount another mod owns (every
  donkey skin, a folder without `mount.cfg`, a code skin on another's mount) is named `<mod>.<name>` or refused, on
  your own mount any name: Saddle Up's donkey skins are `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`
  (the old names `hellfire` and `royal-parade` still find them). The settings rows hold the full names (`Mount =
  saddleup.dragon`, `Skin_saddleup.dragon = saddleup.dragon/default`); a dev's cfg or save from before that names a mount (`dragon`) finds its mount's default and is rewritten; the
  looks' names of the mount folders (`red-dragon`, `grey-rat`, `garden-snail`, `earth-worm`: a few hours old, never
  released) still find their mount's default (the user: "also fix in the old names thing"), and a per-mount settings row
  under the mount's old id (`Coat_dragon`) carries its value to the new one (`Coat_saddleup.dragon`) when that has none.
- **Hellfire's first spawn after a start has its holes again**: the companion's skin is built ahead at the main menu,
  before the game has the donkey's own frames (0 of 44 found), and Hellfire (drawn over the donkey, erasing its ears
  under the horns) was built without them ("its erase colour can't clear the donkey", 2026-10-24 log). A skin built
  from the donkey's frames (an over skin that erases, a recipe) now waits until the frames are in memory (looked for
  every 3 s, no log line until found).
- **The dragon walks like a heavy beast** (the user, 2026-10-24: "his legs ... too dinky", "walks like a lady", "like
  someone modelling stilettos on a catwalk", "a stick stuck up his arse"; the reference's Walking studied, docs/flying.md
  "The walk, studied"): thicker legs with no thin ankle, the forelegs built like the hind (a shoulder mass, a thick upper
  arm and forearm, a short wrist), broad flat paws, the hind feet flat (no tiptoe), long low strides (25 px a cycle,
  was 17), the far legs apart and darker (the four feet were on one line), the shoulders and haunch
  rocking in turn with the folded wings, the head trailing the body a little and the motion running up the neck. The
  seat under the rider moves as before. The flying frames keep their poses with the thicker legs. On the ground no leg
  goes below the paws' row any more (frames 2, 4, 6 and 8 put one 1-2 px under the ground). Standing and baby mounts
  walking about now follow their mount's stride too (the donkey's unchanged).
  In the air the forelegs keep their earlier shape, only thicker: the heavy build (a shoulder mass, three bones) is
  for the legs on the ground only (in flight it folded into one lump under the chest that changed shape between the
  beat's, the glide's and the climb's frames and hid the far paw: the user, "the front leg is jumping from one place to
  the other"). Still open: the steps against the ground speed (the user: "the legs pacing dont match with the speed
  we're moving"); the stride was set from the donkey's frames, not measured: the dev stride probe (`Dev/StrideProbe.cs`,
  `[stride]` log lines) measures it in game first: every walking mount (ridden, standing, babies, with a cart), a row
  per walk cycle in `BepInEx/GK2-strides/`: the clip and the animator's speeds, the multiplier set, the speed keys, the
  cycle's time, the ground covered in world units and the picture's art px, the body frames shown and each footstep
  with the frame it played on. The F11 group **Walk** ("the walk survey") drives it: every mount ridden and with the
  cart, steered right and left by the test (the keeper's stick answered for the run only), a standing dragon and a baby
  left to shuffle near. First reading (the dragon, by hand): 287 art px a cycle in 0.91 s, the drawn steps cover 25.
  The survey (198 cycles): at `WalkCycleSpeed` 1.5 every mount covers the same ground a cycle, about 190 art px (the
  game divides the multiplier by 1.5: the clip plays at the mount's speed multiplier), whatever its speed; the dragon's
  1 (set earlier the same day from a guess) made it 278 px in 0.91 s, its legs at half the donkey's pace for its speed;
  and its footsteps (two a cycle, frames 1 and 5) missed two of its four paw landings (frames 1, 3, 5, 7). **Fixed:** the
  dragon's `[Mount.Tuning] WalkCycleSpeed` line is gone (1.5, as every mount: 0.61 s a cycle riding, 0.81 with the
  cart) and its step sounds four a cycle, one on each paw's landing (the donkey's timing). Every footstep was within
  0.02 of a cycle of its frame: the timing itself is exact.
- **Standing mounts and babies now make their sounds near you**: a parked donkey grazes and smacks its lips at the
  mount sounds' volume, calls now and then (three times rarer than the one you ride, and never two voices within four
  seconds in the scene), its babies answer it one after another, a baby's steps are higher and softer, a herd standing
  in the yard is heard as the ridden one is (within the game's hearing distance). Saddle Up's Sound section has the
  switch, "Standing mounts and babies sound" (on). The mount you ride sounds exactly as before. The API cfg's `[General]
  StandingSounds` (on) turns it off, which also hands a standing donkey's lip smack back to the game (its own
  volume, as before); the dev Sounds tab has the switch.
- **Never lost.** If you ever fall out of the world (under the ground, into nothing, far below it) you are put back on
  the last ground you stood on, with "Back on solid ground." (the game itself has no protection: off the ground the
  keeper falls for ever, and a door places him without a check). Saddle Up's Behaviour section has the switch ("Bring
  me back if I fall out of the world", on), a button "Back to the last safe spot" for when you are stuck, and the Keys
  section a key for it (none by default). Never during a door, a cutscene or a dialogue; standing under a bridge is
  fine. docs/never-lost.md.
- **Fixed: the mod's keys no longer act while the Mounts panel is open.** H, G, the bell and the send-away key
  pressed in the panel or its colours window got you on and off your mount behind it; now they wait until the
  panel is closed, as the game's own keys do. The Mounts panel key still closes it.
- **The mouse wheel scrolls the Mounts panel and the colours window anywhere on them**, not only by dragging the
  scrollbar: over the mount list it scrolls the list, elsewhere on the panel the rows on the right; on the colours
  window, its rows.
- **Fixed: Ride and Drive the cart in the Mounts panel no longer get you off.** Pressed while riding, they ended the
  ride (as the keys do for the way you're riding). Now you stay on: pick another mount and press Ride, and you ride
  on as the new one; Drive the cart brings the cart, Ride while driving leaves it standing; on someone else's
  donkey, yours comes and you get on it.
- **Each mount has its own speed now.** How fast you ride belongs to the mount you pick, and the cart slows every
  mount the same way (to three quarters of its riding speed, as it always did the donkey). The Mounts panel shows it:
  "Speed: Fast" (against the donkey), and a new stat, "Cargo: 6 items", what its cart carries (every mount the most
  for now; your Cart capacity setting still decides what a cart holds). The donkey is as fast as ever (twice walking
  speed riding, 1.5 times with the cart); the giant rat is very fast, Gerry a little faster, the dragon and the worm a
  little slower and the snail slowest. **The Riding speed and Cart speed rows are gone**, and a Faster or Fastest you
  had picked no longer applies: the mounts set the speeds now. Their old lines leave your settings file by themselves.
- **The colours window draws the mount as big as the Mounts panel does** (it was smaller there).
- **Fixed: a mount picked in the Mounts panel no longer shows small or half-changed for a moment** (its cart's
  colours first, then the new mount in the old one's frame and spacing): the picture keeps the last one until the
  new mount is ready, then changes in one go.
- **Picking a mount in the Mounts panel no longer changes your mount.** The list shows it; Ride or Drive the cart
  makes it yours. Done (or Escape) keeps the mount you had; a skin or colours picked for a mount stay for when you
  pick it.
- **The mount pictures are always lit as outdoors at midday**, not tinted by the hour (tan at dusk, blue at
  night) or the room, and never washed by the game's fog, its lamps' light or its cloud shadows.
- **New: babies follow you on foot.** A new row, "Babies follow you on foot", on: off your mount, outdoors, your
  babies come with you instead of staying by it (turn it off for the old way). They trot after you,
  and catch up out of sight when you're far ahead; when you get on a mount they go back to yours. Into buildings
  they still come only with "Babies come indoors". The babies rows have a section of their own now, Babies, under
  Mount; your values are the same.
- **Fixed: the settings page's picture of your mount kept the donkey** after a mount drawn from the game's own
  pictures (Gerry) became available again a scene later, while your mount in the world already showed it. The
  picture (and the Mounts panel's) now follows at once, as it does for a skin added, replaced or removed.
- **New: mounting indoors** (a player asked). Behaviour has two new rows, both off, so nothing changes until you
  turn them on: "Allow mounting indoors" rides into houses, the church and the morgue and lets you get on inside;
  "Allow mounting with the cart indoors" (only with the first on) brings the cart in too, a corpse on it staying
  on it. With the cart not allowed, driving through a door leaves the cart standing at the door with its load and
  you ride on inside; at the morgue you get off inside carrying the corpse, your mount in there with you. A door
  with a loading screen still ends the ride. The cart can get caught on furniture and door frames inside. Where the cart isn't
  allowed (indoors, in a fight), the cart key now says so without calling your mount over first.
- **Fixed: a loaded cart crept back behind a long mount.** With the rat (or the dragon, which pulled the cart then) hitched, every item put on
  the cart moved the whole cart one more hitch gap away; a loaded cart made again (through a door, switching from
  riding to the cart) came back too far as well. The cart keeps its place now. The donkey has no gap, so it never
  showed there.
- **The Mounts panel is a mount journal now.** The list of your mounts on the left with a picture of each; on the
  right the chosen one walking with its cart, a card with its speed, whether it pulls the cart, its babies and how
  many skins it has, and its Skin and Effect rows. "Colours and effect..." opens a second window with the mount's
  picture and every colour row, a custom colour picked from 24 coloured squares. Ride and Drive the cart call the
  mount straight from the panel; a search box appears when you have more than eight mounts; Escape steps back and
  never opens the pause menu. Hellfire and Royal Parade now come with Saddle Up (nothing changes for you).
- **Binding a key no longer presses it.** Setting the Mounts panel key in the Mods menu opened the panel on the
  spot, and the ride and cart keys had the same flaw (binding G hitched): the key you pressed to set the binding
  counted as a press of it. A key just bound now waits until it has been let go.
- **Where the panel lives** (Saddle Up 0.12.0): the Mounts button in the Mount section, or a key of your choosing
  (Keys: Mounts panel, none by default). The Mount, Skin, effect and colour rows are no longer in the Mods menu or
  the Settings window: they are chosen in the panel, so the settings page stays the same size however many mounts
  you install. Your saved choices are the same cfg entries as before. The "Choose colours" bar is gone into it.
- **A mount's legs match its speed.** The walk animation's rate per unit of speed was the donkey's constant on
  every mount; it is a value now (`WalkCycleSpeed`, 1.5 = the donkey's), so a mount with a longer stride, or a
  scurrying one, sets its own in its file.
- **A mount asleep far away frees its pictures** and takes them again when it wakes; a skin's canvas is at most
  256 (a 512 one held 49 MB). A mount built from the game's own character frames (Gerry) is checked when the game
  is up: if a game update took its frames away, the log says which, the mount is left out of the lists and your
  donkey comes instead of an invisible mount.
- **A mount can't take your settings unseen any more.** A mount that sets its own speed or sounds (its own file
  says so) used to win over your Speed, Brays and Bell rows while you rode it, and the rows did nothing; now such a
  row is left out of the menu for that mount and your value still applies to every other one. A mount from a mod
  may set its speed, seat, sounds and the like, never your behaviour switches, the lantern, your skin, effect or
  colours. A mod's ability can't take your Effect choice either: a value belongs to whoever set it (below).
- **Two copies of one skin: the newer wins.** A skin pack dropped into `plugins` and the same skin in a mod's
  DLL (an old folder copy left behind, say): the higher version is the one you get, a tie goes to the DLL, and
  the log names the loser and its folder so you can delete it. Before, the folder always won, silently.
- **Every mount and skin carries a description** (the dragon, the rat, the snail, the worm, Gerry, Hellfire,
  Royal Parade), shown in the Mounts panel.
- **The five mounts are Saddle Up's.** The Red Dragon, the Giant Rat, the Giant Snail, the Giant Worm and Gerry
  moved out of GK2 Donkey API into GK2 Saddle Up (five skin packs in its DLL, registered with the API at start,
  the same way anyone else's mod ships a mount). Nothing changes for you: the same download, still switched
  off until the next update. Since the same night the donkey's two skins, Hellfire and Royal Parade, and the Hellfire
  effect are Saddle Up's as well: the API ships the donkey alone, as the backend. A mount from any
  mod now shows in Saddle Up's settings whatever order the mods loaded in (below, for mod authors).
- **The dragon's belly has its own colour**: the Second tone row (below). A green coat had left a gold belly that
  no row could reach; for an afternoon the belly followed the Coat, now it
  is a tone of its own again, set apart from the scales. Its plate seams are every four rows instead of three.
- **A "Second tone" colour row** under the mount colours: the donkey's muzzle, the rat's tail, ears and nose, the
  snail's shell, the dragon's belly, Hellfire's lava (the API's Muzzle region), with Pale, Rose, Brown, Green, Blue, Dark and Custom
  (each preset measured to move every one of those parts by a clear step: a pink on the rat's pink and a brown
  on the snail's shell showed nothing at first); it greys on a mount whose look
  has no such part (the worm). Every drawn part of every skin a player may want to colour is now on
  the page: Coat is the body, Harness the tack, Second tone the rest of the skin; bone, horns, outlines and eyes
  stay as drawn. A colour row for a part the chosen mount has no such part of (Harness and
  Second tone on the worm) is greyed in the Mounts panel for that mount instead of sitting there doing
  nothing (since 0.12 the colour rows are the panel's, not the menus').
- **The Tests tab folds its groups**: each group's header carries its count, its last results and the Run group
  button, and a click on the name unfolds the scenarios (60 scenarios made the tab a long scroll).
- **The settings picture's cart moves to the new mount's hitch gap at once** when the Mount row changes. It slid
  into place up to a second later: the picture's paper mount never counted its
  tuning re-merges, so the picture only picked the gap up on its once-a-second fallback. The preview self-test
  measured it (the gap 54 frames after the skin) and now guards it.
- **The cart no longer sinks into a hill behind the mount.** Its ground is sampled under the cart body, up to a
  unit and more behind the mount's root, and at the edge of a raised path that sample lands on the level below:
  0.77 lower by the Inquisition Base rocks under the dragon's cart, within the
  slope limit, so the cart followed it down into the drawn ground. A target that jumps by more than 0.15 between
  two frames without the cart having swapped sides is a ledge, not a slope (walking moves the sample 0.12 a
  frame at most): it is held off and logged, and the cart keeps the mount's level until the sample agrees again,
  which it does once the mount has walked the step itself. Before, the jump was logged and applied.
- **The settings page regrouped** (Saddle Up 0.11.0): seven sections in a new order, each holding one kind of thing.
  Keys. Mount: its picture (with the cart now), Name, Mount, Skin, the babies rows, and "Keep your mount with the
  save" (was in Behaviour). Colours: the picture, then every colour under two headings (since 0.12 in the Mounts panel), "Mount
  colours" (coat and harness, the old Donkey section) and "Cart colours" (paint, wheels, wheel hubs, lantern light,
  from Cart). Effect. Behaviour: the two speeds, "Say why mounting is refused", "Shuffles while standing". Cart: "Cart
  capacity", "Zombies ride on the cart", "A storage chest rides on the cart", "Carry the corpse into the morgue",
  "Donkey leaves corpses at the crossroads" and "Calling your mount leaves the cart parked" (all were in Behaviour),
  then "Lantern" and "Lantern lit by day". Sound. The Donkey section is gone. Every value carries over: a moved row
  reads its old place on the first start (the log says which) and the old lines are cleaned out of the cfg.
- **The Mounts panel** puts the colour rows under the same two headings, "Mount colours" in the left column and "Cart
  colours" in the right, with the Skin row above the first and Effect below the second (0.12; 0.11's colours window did the same with the Mount row too).

For mod authors:

- **`Mounts.MountsEnabled` is always true** (it was false in 0.10.0's download): your mounts, skins and effects
  register and are offered to players. The switch behind it, the dev key `Debug.MountsPreview`, is gone (the user,
  2026-10-27: the mounts are mods of their own now, so which ones a player has is which packs they installed). The
  property stays for mods written against 0.10.0. A registration before the API has started still throws, saying to
  add `[BepInDependency]`.
- **A mount's `cargo` is enforced** (`MountDef.Cargo`, `[Mount] cargo`, 1 to 6, `MountInfo.Cargo`): its cart holds
  that many, or the player's `CartSlots` when smaller, re-applied when the mount under a hitched cart changes; never
  fewer slots than the cart holds. Until now it was shown in the Mounts panel only. A mount that leaves `cargo` out
  carries 6, as before.
- **Per-mount companion settings** (2026-10-26): `MountLook` (`companion.LookOf(mountId)`) has the mount's own
  `FoalCount`, `FoalsFollowOnFoot`, `FoalsComeIndoors`, `FoalsWearSkin`, `Name`, `SoundVolume`, `Lantern` and
  `LanternByDay`; the companion's properties of the same names read and set the current mount's (unchanged for a mod
  with one mount). A mount change applies its own name, babies, volume and lantern. `MountSettingsPage.FoalRows` makes
  its four rows per mount, as look rows (in the Mounts panel's journal, not the menus). `Toggle` and `Text` take
  `look: true` like `Choice`: a look `Text` row is a text box in the panel. The panel's journal orders its rows the
  same on every mount: a text row first, the Skin second, the rest in the order the page made them.
- **A mount from a mod loaded after Saddle Up gets its colour rows in their place** (found with our own pack DLLs,
  which load after Saddle Up, 2026-10-26): the settings page's catch-up appended a late mount's rows at the end, so in
  the Mounts panel its Coat, Harness and Muzzle rows sat under "Cart colours" and it had no "Mount colours"; now each
  builder's rows for it follow that builder's rows for the mounts before it (`MountSettingsPage.RunPerMount`).
- **Our mounts are worked examples of a pack without a DLL**: `packs/SaddleUpDragon/` and `packs/SaddleUpCritters/` in
  the repo, each exactly the folder a player puts into `BepInEx\plugins\` (docs/making-a-mount.md "Built in or
  external"). Their ids changed with the move (`saddleup-dragon.dragon`, `saddleup-critters.rat/.snail/.worm`; never
  released).
- **GK2 Donkey API 0.40.0: content from many mods** (docs/multi-mod-loading.md, docs/making-a-mount.md "Living next to
  other mods"). `MountHandle.Remove(ownerId)` (another mod's instance is refused) and limits on `SetData`. An effect's
  id is `<mod>.<name>` (Saddle Up's Hellfire is `saddleup.hellfire`) and meets the version rule; a code skin or effect
  that loses it no longer throws into your Awake; a handle withdraws only what it registered; the first mod's sound for
  an event stays. A mount.cfg may say `needs =` / `format =`; one whose id isn't its folder's is refused; ids are ASCII,
  at most 64, no dot at an end. A newer pack of a mount brings its pictures with its facts (the old pack's skins go).
  Skins of a mount that isn't installed are offered nowhere. Pictures are checked from their header (a frame at most
  its canvas, a portrait 512x512), sounds at most 16 MB, no NaN, no file outside the pack. Register in Awake or Start.
- **Cart rows follow the mount (API 0.39.0)**: `MountSettingsPage.CartRow(section, key)` marks a row as the cart's: it
  shows only while its mount pulls the cart (`MountSettingsPage.MountPullsCart`). Saddle Up's eight cart colour rows
  use it. The Mounts panel hides Drive the cart for a mount that can't pull one.
- **Each mount its own jump (API 0.39.0)** (the user, 2026-10-25: "the arc the donkey makes while jumping looks silly
  for a dragon ... first lets make sure that we have the ability to specify per mount"): `mount.cfg` `[Jump]`
  (`height`, `tap_height`, `hold_min`, `time`, `rise_share`, `hang`, `crouch`, `crouch_time`, `squash`) and
  `MountDef.Jump` (`MountJump`, every value `float?`); a key left out is the default jump's (the donkey's: 28 px held,
  16 tapped, 0.45 s in the air). `MountInfo.Jump` (filled) and `MountInfo.OwnJump`. `[Jump]` in a `skin.cfg` is
  ignored and reported. A jump takes its mount's shape at the press. Two more keys: `fall_ease` (0 to 1: the fall
  braked to a soft touch-down) and `wings` (`MountJump.Wings`: the skin's flying frames as a small take-off and
  landing). The dragon's `mount.cfg` sets them; Saddle Up needs API 0.39.0.
- **Flying and jumping are on for everyone (API 0.38.0)**: `Mounts.FlyingEnabled` is true unless the dev Flying tab
  switched it off (before, it needed the dev tools unlocked). New Behaviour keys `ClimbKey` (Left Shift) and
  `DescendKey` (Left Ctrl): `KeyboardShortcut`s held in the air, the main key alone counts (a modifier can be the key;
  the game's Settings window can't capture a lone modifier, the Mods menu and the cfg can). The dragon's card says
  "Flies". Saddle Up needs 0.38.0 (its Climb and Descend rows set the keys); its own version stays 0.12.0 (unreleased).
  Dev-only still: the per-frame `[flight-probe]` lines (off) and the jump probe's files (the dev tools).
- **Nothing picks or shows a particle effect any more (API 0.37.0)**; making one stays (the user, 2026-10-25:
  "the ability would be available from the api and then we will consume it from the skin consumer"; how a skin
  comes with its effect is being designed). Kept: `Mounts.Effects` (`Effects`, `Find`, `EffectsChanged`, `Register`,
  `Unregister`, `GameParticles`), `MountEffectDef`, `MountEffectPart`, `MountEffectEmitter`, `MountEffectPlacement`,
  `IMountEffectSource`, `MountEffectInfo`: a registered effect is listed and ready, and shows nowhere yet. Removed:
  `MountEffects.NoneId`, `FromMount`, `IsNone`; `MountEffectWhen`; `Mounts.EffectSettings`;
  `MountSettingsPage.EffectRows`; `Effect` and `EffectWhen` on `MountCompanion`, `MountDef`, `MountInfo`,
  `MountSkinDef` and `MountSkinInfo`; the instance tuning keys `Effect` and `EffectWhen` (a side file holding them
  drops them on load); the API cfg's `[Effects]` section; the pack keys `effect =` and `effect_when =` in skin.cfg
  and mount.cfg (an old file's lines are reported as unknown keys). This reverses, for choosing an effect, the
  2026-10-17 rule that the player's effect is chosen through the API (docs/mount-looks-design.md). Saddle Up needs
  0.36.0 or newer as before (it calls nothing removed; 0.38.0 since the flying switch-on, above).
- **Skins built on their mount's default** (see For players): `MountSkinInfo.BuiltOn` (the default's id, or `null`)
  and `MountSkinInfo.OverDefault` (drawn over that default; `Over` stays "over the game's donkey"). A code skin's
  sentinels now mean "the default's" on such a skin: `SeatRow` -1 with `SeatFromPicture` false, `Offset` zero and
  `CartOffset` null, `Effect`, `Description`, `Portrait` and `Regions` null, the canvas 96x96 and pivot 48,48;
  `Effect = "none"` is none. A pack says "not the default's" with `seat = none`, `effect = none`, `portrait = none`.
  `Over` on a code skin of a mount other than the donkey is refused (drawn in its place, said in its report). A
  resolved sound's layer can be `"default"`.
- **`Mounts.Catalog.Register` no longer throws when another mod has the id**: the version rule decides (the higher
  version; at a tie code or a DLL over a loose folder; else the first stays), the loser gets a warning and a handle
  that does nothing; a code mount meets packs by the same rule (a newer `mount.cfg` replaces it, an older one is
  refused with its skins); code mount and code skin clips are banked under their mod. An embedded pack whose
  `mount.cfg` is refused now drops its skins as a loose folder does (docs/mount-id-collisions.md "Decided").
- **Mount ids are `<mod>.<name>`, skin ids `<mount>/<skin>`** (see For players; docs/skins.md, docs/making-a-mount.md):
  `Mounts.Catalog.Register` refuses an id without a prefix; `MountSkinDef.Id` is the name within `Mount` ("default" for
  your mount's own look) and the registered skin's `Id` (`MountSkinInfo.Id`, the "Skin" tuning value) is
  `<Mount>/<name>`; a recipe is a donkey skin (`donkey/<id>`). A code skin on a mount another owner registered and
  every donkey skin and recipe are named `<mod>.<name>` (`Mounts.Looks.RegisterRecipe(Guid, "example.sepia", ...)` is
  `donkey/example.sepia`): without the prefix `Register` / `RegisterRecipe` throw an `ArgumentException`; a code skin
  on your own mount takes any name. `Mounts.Looks.Find` takes the full name and a clear short one. No signature
  changed.
- **The dev Sounds tab's events are grouped by status**: what is playing on top, then its own, the donkey's, loading,
  MISSING, silent by choice and no sound, each group under its coloured heading, the catalogue's order inside one.
- **The flaps follow the drawn wing beat from the start of the climb** (the user, 2026-10-24: the flapping began
  late and missed the first flaps): only the airborne beats sounded, once the climb had settled at the top, and the
  launch's three surges fired three flaps in 0.32 s that blurred into one; and a session's first flaps were silent
  while their files decoded. Now a flap plays on every beat the picture shows, from the climb's first beat (after the
  crouch and the take-off frames) through the flight to the dive, and a skin's sound files start loading as soon as a
  mount wears it. The take-off files have no silence at their ends (their 60 ms fades only; checked).
- **No footsteps in the air** (the user, 2026-10-24: "when we're flying, the mount step is also being played"): the
  view's animator runs its walk clip under the flying frames whenever the keeper steers, and every footfall of it
  played a step; heard once a mount's sounds played at the ground point under it. Off the ground (a flight or a jump)
  no footfall, wheel or grazing sound plays now. The flying self-test steers in the air and checks no step plays.
- **Flying sounds are heard at full strength, babies' steps are toned down** (the user, 2026-10-24: "the takeoff
  sounds are still barely audible compared to the landing sounds"; "the baby steps are much louder than all our other
  sounds"). Measured first: the game's listener stays at ground level by the keeper's body (it stays on the ground
  while flying), and a mount's sound followed its drawn height: a take-off was heard at 87% at its start, 30% a quarter
  second on and 12-14% from half a second (7 units up), a landing the other way round (14% up to 93%). A mount's
  sounds now play at the ground point under it (a sound anchor of the rig's own, following it), so the rider hears
  his mount as he flies; on the ground nothing changes. A baby's steps: every other footfall (its legs cycle 1.7
  times faster than a grown one's and several follow at once), and a baby's volume falls with its size as size^1.5
  (0.46 at 0.6, was 0.77). Note: the player's own `[Ride] HoofVolume` here is 3 (the default 1.3), which every step
  is scaled by.
- **The dev Sounds tab shows what is missing and what is playing, in colour**: each event's name and source in its
  colour (red MISSING: the donkey's own sound, silent on this mount until its pack gives one; green its own, from the
  mount or the skin; white the donkey's; blue silent by choice; yellow loading; grey no sound), a key above the table,
  and a cyan ▶ with "playing" while an event's sound plays ("played 12s ago" after). The Mounts tab's sound rows
  take the same colours. A [sound] log line per play gives the sound's place, its distance from the game's listener and
  what it is heard at over its life (the flying events always, everything with the tab's switch): the measurement for
  the lift-off sounds heard much quieter than the landing ones.
- **The dragon takes off and lands with sounds of its own**: six sounds the user supplied, trimmed of their silence
  (0.05 to 0.25 s in front, 0.1 to 0.43 s behind), levelled to the same loudness without clipping and made mono OGG, in
  the dragon skin's `sounds` folder: four lift-offs (`takeoff_1.ogg` to `takeoff_4.ogg`, its `[Mount.Sounds] takeoff`)
  and two landings (`land_1.ogg`, `land_2.ogg`, its `land`, from the start of the dive); one at random per flight,
  never the same twice in a row, shared by every dragon skin. Its voice stays the game's growl placeholder.
  Then its wing beats and glide: the user's two flapping clips held 2 and 3 wing beats, cut at their quiet dips into
  five single flaps (`flap_1.ogg` to `flap_5.ogg`, the `flap` event, one per 0.6 s beat in step with the picture,
  volume 0.6), and a glide whoosh (`glide_1.ogg`, with the second landing clip, which came in the glide folder too; cut
  when the glide ends). The always-on flight trace leaves the flaps out (one every 0.6 s). The first clip's two beats overlap (the
  first one's tail still at -35 dB when the second starts): `flap_1` is cut at the second beat's onset (0.81 s) with a
  fade over the whole tail, not 50 ms earlier with a short fade (it cut out audibly, the user). The first sound files the mod ships. The trimming tool,
  for the next batch: `trim_sounds.py <folder> <out> [prefix]` (kept with the work copies in
  `C:\ClaudeCodeTemp\GK2-dragon-sounds`, with WAV previews and a README of what was cut).
- **The donkey's own sounds stay the donkey's** (docs/mount-sounds.md "The layers"): a mount other than the donkey
  no longer inherits the bray, the lip smack, the chewing or the getting-on and getting-off snorts riding plain; they
  are silent on it until its pack gives them a sound (a new creature is silent by default where it matters). The
  neutral sounds stay everyone's: the step by the ground, the cart's, the bell, the cart style's ladder. The snail's,
  the worm's and Gerry's old volume pins are gone. **What a skin is missing** is said in three places: one log line
  per skin when the game is ready, the first line of the Mounts tab's "Sounds of this skin", and an INFO line per
  built-in in `tests/SkinsScan`.
- **The herd and the babies' own sounds** (docs/mount-sounds.md; phase 3 of the sound system): the `answer` event
  (a baby's reply to its parent's call, the donkey's voice pitched up by its size unless given one), `[Sounds.Baby]`
  and `[Mount.Sounds.Baby]` blocks (`MountSkinDef.SoundsBaby`, `MountDef.SoundsBaby`, `MountInfo.SoundsBaby`) for the
  babies' own sounds over the grown one's. Self-test "herds and babies" (group Sounds).
  Found by that test: the idle call's timer was armed once, at an instance's first tick, with the interval of that
  moment (a plain donkey's 40 s), and a skin worn later with its own interval never re-armed it; the rig now re-arms
  whenever the resolved interval changes, ridden or standing. The sound self-tests drive the standing switch through
  Saddle Up's row, since its session override masks the cfg entry.
- **The sounds API** (docs/mount-sounds-design.md, phase 1 step 4): `Mounts.Sounds` with the catalogue of events,
  `Resolved` (what plays on an instance and from which layer), `Set` (a mod's own sound over everything, a game id
  or its own clips, back on Dispose), `Play` and `Stop`, the `Played` event; `MountSkinDef.Sounds` and
  `MountDef.Sounds` (and the per-style pair) for skins and mounts registered in code; `MountInfo.Sounds`. The Mounts
  tab (F11) edits a pack's sound lines: one row per event for the skin's block and for the mount's, the line's
  source and its volume, with what the ridden mount plays and from which layer next to it; the auto-save writes
  them to the skin.cfg. Self-test "the API" (group Sounds).
- **A skin pack brings its own sounds** (docs/mount-sounds.md, the author's page; the design in docs/mount-sounds-design.md): `.ogg` or `.wav` files in the pack's `sounds\` folder, named for their event by convention
  (`voice_1.ogg`, `voice_2.ogg`, `step.grass_1.wav`) or lined up in a `[Sounds]` block of skin.cfg (`voice = a.ogg,
  b.ogg`, `voice.volume = 0.8`, `step.grass = step_grass`, `lips = none`, `step = ground`; `[Sounds.Ride]` and
  `[Sounds.Cart]` for one style), and a mount's own in `[Mount.Sounds]` (and its style sections) of the declaring
  skin's cfg, shared by every skin of the mount. A skin's line wins over its mount's, a style block over the
  both-styles one, and the player's switches and volumes scale whatever won. The files are loaded when first needed
  (an embedded `.ogg` through a cache file under BepInEx\cache) and registered as game sounds, so they play through
  the game's own player and follow its Sounds slider; a missing or bad file is named once and the layer below
  applies. The Mounts tab's auto-save keeps the sections; `tests/SkinsScan` and `canonical_cfg.py` know them.
  Self-test "a pack's own sounds" (group Sounds).
- **Each mount's own speed and cargo** (breaking, pre-release): `MountDef.Speed` (`float?`, times the keeper's
  walking speed, 0.5 to 4; null = the donkey's, the API cfg's `SpeedMultiplier`) and a pack's `[Mount] speed =`, over
  a `SpeedMultiplier` in the mount's tuning; with the cart it is times the donkey's cart-to-riding ratio (1.5 / 2 as
  shipped). `MountDef.Cargo` / `[Mount] cargo =` (`int?`, 1 to 6, null = 6): what its cart carries, a stat only for
  now. `MountInfo.Speed`, `CartSpeed` (the derived one), `SpeedWord` (Very slow, Slow, Normal, Fast, Very fast against
  the donkey's 2), `Cargo`. A `[Mount.Stats]` or `MountStat` line named Speed or Cargo is left out (the API writes
  those). **Removed:** `MountCompanion.RideSpeed`,
  `CartSpeed`, `SpeedLabels`, `SpeedLabelsFor` and the `MountSpeed` enum. New `MountSettingsPage.RetiredKey(section,
  key)`: a setting dropped on purpose leaves the player's file at Publish and the upgrade check counts it as retired,
  not lost (Saddle Up's RideSpeed and CartSpeed). The built-in mounts' speeds: rat 2.75, Gerry 2.25, dragon 1.6,
  worm 1.75, snail 1.25, every one cargo 6; SkinsScan wants both on every built-in mount and refuses a
  `SpeedMultiplier` in its tuning. docs/skins.md, docs/api.md.
- **Babies on foot:** the Shuffle key `ShuffleFollowKeeperOnFoot` (off in the core; a companion sets it on its own
  foals only), `MountCompanion.FoalsFollowOnFoot`, and `FoalRows` adds `FoalsFollow` ("Babies follow you on foot").
  The row defaults to the companion's `FoalsFollowOnFoot` (Saddle Up sets it on first; the core stays off).
  `MovedKey` takes several old places for one key, a call each (before, the last call won): Saddle Up's babies name
  both Mount and Donkey. docs/foals.md "Following the keeper on foot".
- **The Behaviour key `FlyIndoors`** (API 0.36.0, off): on, a flying mount may take off and fly indoors, as a lift
  only; off, no take-off indoors and a flight through a building's door lands there. Saddle Up keeps it off, with no row.
- **Saddle Up needs GK2 Donkey API 0.36.0** (was 0.35.0: it uses `CartIndoors`, `Mounts.Catalog` and registers
  Hellfire's effect itself); an older API gets the clear "too old" message instead of failing part-way.
- **`CartIndoors`** (a Behaviour key, off): with `DismountIndoors` off, the cart comes indoors only with it on; off,
  a cart ride through a building's door leaves the cart standing at the door (an unhitch, `Unhitched` raised) and
  goes on as a ride; at the morgue with a corpse the keeper gets off inside and takes it into his hands
  (`Dismounted` with "morgue"). The cart is refused inside ("Not the cart indoors."). A summoned mount
  (`TryMount`), or a cart that can't be left there, gets off at the door as before.
  Before, `DismountIndoors` off let the cart in too: set both for that. Saddle Up no longer pins `DismountIndoors`:
  its new rows set both (docs/indoors.md).
- **The API ships no skin** (the user, 2026-10-21: the backend, not bloated with skins). Hellfire, Royal Parade and the
  Hellfire effect moved from GK2 Donkey API to GK2 Saddle Up (`src/SaddleUp/assets/skins`, the effect registered in
  Saddle Up's Awake through the public API, the same definition). Same ids, so a player's `Skin_donkey = hellfire`
  still resolves; the owner is Saddle Up's GUID now. The self-tests borrow Royal Parade's pictures from Saddle Up's
  DLL; the skins scan reads the shipped effect ids from Saddle Up; the generators write into Saddle Up's folder; a
  new skin goes to Saddle Up by default (`new_skin.py`).
- **docs/making-a-mount.md**: the one-page guide to a mod's own mount, both shapes (a pack embedded in a DLL with
  one line in Awake; a mount in code with an ability), what the API does for it unasked, and the repo's own build
  steps; linked from the READMEs, api.md, skins.md and mounts-as-mods.md.
- **One word for one thing: a mount is a mount** (API 0.36.0; the user, 2026-10-21: the code said "kind"). Every
  name that meant the mount says mount now: `MountDef`, `MountInfo`, `Mounts.Catalog` (`All`, `Find`, `Register`,
  `MountsChanged`), `MountHandle.Mount`, `MountEffects.FromMount`; inside, the registry is `Catalog` with its
  `Entry` per mount, and every `kind` in a name, a comment or a log line is `mount`. The settings rows' type
  is `RowKind` (Toggle, Choice, Key...), so the word has one meaning left. In a skin.cfg the colour mode is
  `colours = own|palette` (was `kind =`; not public yet, the seven cfgs and the tools follow). Cargo's item
  kinds and the dependency manifest's member kinds are other things and keep their word. A pre-1.0 breaking
  change for mods on the API (docs/api-design.md); Saddle Up and the example rebuilt on it.
- **The Mounts panel is the API's**: `MountSettingsPage.PanelButton(section, label, description)` (`LookButton`
  is the same) and `OpenPanel()` for a key of your own. Rows marked `look: true` (the Mount and Skin rows, the
  colours, the effect) are no longer handed to the Mods menu, the game's Settings window or the menu labels: the
  panel shows them for the chosen mount, built on choice (thirty installed mounts cost thirty list entries, not
  thirty sets of rows). The panel reads the registry: a mount's and skin's `description`, `portrait` (drawn under
  the mount's name), `[Mount.Stats]`, its cart, babies, speed and effect, and its abilities by `Name` and `Description`.
- **`WalkCycleSpeed`** (`[Ride]` / `[Cart]`, 1.5): the walk clip's rate per unit of walking speed, a mount key;
  **the canvas is at most 256**; a mount asleep releases its sheet (`Looks.Rig.Sleep`) and sheets build from the
  API's own tick, so a sheet whose wearers all sleep no longer stalls; **game-source skins are verified at
  game-ready** (`Looks.VerifyGameSources`: the frames the `[Game]` section names must be there, else the skin is set
  aside with a warning; memory and the sprite atlases are searched together; no frames at all refuses the sheet
  so the donkey shows instead of an invisible mount).
- **A value on an instance belongs to the owner that set it** (API 0.35.0, docs/mounts-foundation-2.md 2.1):
  `handle.Tuning.Set` by another owner is refused (the value in effect is returned, `TrySet` says so, one Info
  line), `Clear` takes the owner (`Clear(ownerId, key)`) and clears only its own; the mount's own values (the mount's
  merge) stay anyone's to replace, and return when cleared. The session layer (`Mounts.Tuning(style).Set`,
  `Behaviour.Set`...) follows the same rule. What a mount may pin through `[Mount.Tuning]` or a code mount's
  `Tuning` is now a rule: the ride and cart families (speed, seat, pose, rider motion, cart geometry, sounds) and
  the shuffle's values, never a behaviour switch, the lantern or a look key (refused, said once). An ability's
  values are set under an owner the API gives it (`MountAbility.Owner`, "<the mount's owner>.ability.<mount id>")
  and are cleared when the instance stops being that mount, whether the ability cleared them or not. A page row
  can say which tuning key it sets (`MountSettingsPage.TuningKey`, `PinnedBy`): it is left out of the Mods menu
  and the game's Settings window while the companion's chosen mount pins that key. Overrides in the side file whose
  owner isn't loaded are set aside and written back, not applied. The example ability gallops instead of
  burning (an effect is the player's choice; a mount that burns declares `effect = hellfire`).
- **Ids and a mount's facts** (docs/mounts-foundation-2.md 2.2): the skin whose id is the mount's (its Default)
  owns the `[Mount]` declaration and re-declares it over a pack that declared the id first (the first pack's skin
  joins); a later declaration that differs says its facts and tuning are not used; the same skin id from two
  places: the higher `version` wins, a tie goes to the DLL over a folder, the log names the loser; each
  `gk2skins` folder is an owner of its own, scanned in name order; an id from another mod without an author
  prefix (`author.name`) is noted once; a reload whose `[Mount] id` changed withdraws the old mount; the Mounts
  tab saves the mount's rows into the declaring skin's file only, and stamps the file so the auto-reload doesn't
  read it back.
- **The pack format for the Mounts panel** (docs/mounts-foundation-2.md 3): `[Skin] description` and `portrait`
  (a PNG next to the frames), `[Mount] description` and `portrait`, a `[Mount.Stats]` section of lines shown as
  given, `MountAbility.Name` and `Description`; on the API `MountSkinDef.Description` / `Portrait`,
  `MountDef.Description` / `Portrait` / `Stats` (`MountStat`), `MountSkinInfo.Description` / `HasPortrait`,
  `MountInfo.Description` / `HasPortrait` / `Stats` / `AbilityInfos` (`MountAbilityInfo`). `[Skin] needs =
  <API version>`: an older API refuses the pack with the reason instead of drawing it with the keys it doesn't
  know left out; a `format` that is not a number is refused; a `[Game]` line that names no frame key is reported.
  The scan requires a description on every shipped skin and mount.
- **A mount or skin registered in any plugin's Awake reaches a settings page** (API 0.34.0, docs/mounts-as-mods.md
  4). `MountSettingsPage.Publish` now shows the page the frame after it is called, when every plugin's Awake has
  run: the Mount and Skin rows and the per-mount colour rows (`MountRows`, `PerMount`) are made for the mounts
  registered meanwhile too (BepInEx orders plugins by their dependencies only, and a mount mod can't ask to load
  before the mod that owns the page). Before, a mount from a mod loaded after Saddle Up had no rows for the whole
  session and no restart changed it. Self-tested: a pack declaring a mount after the rows were built gets its
  Skin row and coat row at the catch-up; every page shown this session has a Skin row for every mount.
- **A set skin or effect that isn't installed is an Info line, not a warning** ("its mod or pack isn't loaded;
  the choice is kept and comes back with it"): a player who removed a mount mod has nothing to fix.
- **Shipping a mount in a mod**: Saddle Up is the worked example (docs/mounts-as-mods.md): its five mounts are
  packs embedded by one csproj line and registered by one `Mounts.Looks.RegisterEmbedded` call in Awake, no other
  code; a loose `gk2skins\<id>\` folder under `plugins\` needs no code at all. A dev build may record the packs'
  source folder as the DLL's `GK2SkinsSourceDir` assembly metadata, and the F11 Mounts tab then writes a skin's
  tuned values back into its skin.cfg as it does for the API's own.
- **Skin packs: an erase colour for skins drawn over the donkey** (docs/skins.md "Erasing from the donkey"). A
  pixel of `erase = FF00FF` (magenta by default for `layer = over`; `none` switches it off) makes the donkey
  transparent there, so an over pack can take the ears off under horns or the tail off under another. Such a picture
  is built whole when first worn (the donkey's frame from the game with the holes, the picture's pixels on top) and
  drawn in the body's place; pictures without it stay plain overlays. Nothing of the game's art goes into the pack.
  The pack checker and the review page honour it; a self-test wears a pack that erases the ears. The step towards
  every skin being a pack (Hellfire next: the user, "for the concept of a mount skin we should do this drawing-over
  thing").
- **The skin tools, one pipeline** (the plan's M6; docs/sprite-tools.md "Making a skin: the one pipeline"):
  `tools/skins/new_skin.py` scaffolds a skin or a mount (the cfg in the canonical layout, a generator stub, the
  Aseprite sidecar, the scan); every generator writes its strips, preview and sheet through one module
  (`tools/skins/review.py`: the front layer composited, the rider on the seat row), so the four creatures have strips
  too; the colour rules of `skin_colors.py` are data (`tools/skins/creatures/colors.json`), the front layer counts and
  Gerry's `[Colors]` comes from the game's export; the Aseprite watcher splits the exported sheet into the built-in's
  per-frame PNGs; the checker knows the front layer and `source = game`, reads the donkey only under an over skin,
  judges lone pixels over what the game draws beneath (Hellfire's one-pixel lava crack), says a kept donkey colour
  and the colour cube instead of warning, and returns an exit code, so it runs in the release scans on every
  built-in; the scan reads pixels (the seat row must be drawn at the pivot column of the standing frame, the erase
  colour is refused in a replace skin, a sheet is a whole number of cells, the `[Game]` map's targets are frame
  names); the review page draws the front layer in front of the rider, the rider on the seat row with the skin's
  offset and the seat line, and the card's facts (seat, hitch gap, babies, colour regions); the Gerry scripts take
  the prefix and find the bundle by content; the creature generator wrote into `tools/src/...` since its move (fixed).
  Wording: the Riding tab says "mount"; the `Debug.MountsPreview` description, the csproj's skins comment and the
  export button's tooltip say what is there now.
- **After the review of the parity work** (three independent reads of the diff, 2026-10-20): the side file's record
  label read the owner column instead of the key, so every kept mount was labelled "donkey" in the log (the data was
  right); the Gerry self-test still expected "gerry" as the noun; a refused unhitch of a mount that became a no-cart
  mount was tried again every tick with two log lines each (now once a second, said once, and run after the rig's own
  work); the layered value a hitch reads took the wanted skin's geometry while the drawing applied the worn one (both
  worn now); a mount without babies of its own gave its `foal_scale` to plain baby donkeys (0.6 now); spawning a
  mount by name that has no skin spawned a donkey (refused); the noun reaches every refusal and log sentence; a code
  skin's sprite colours copied its texture once per sprite (once per texture); the settings picture read the hitch
  gap from the layers every frame (when they change); a page's grey condition composes with a row's own, and Saddle
  Up greys the custom harness row with the harness row. Tools: the checker counts magenta in a replace skin (it
  never did), survives a cfg the canonical writer refuses, says that a front picture can't erase; the canonical
  writer refuses `[Mount.Tuning]` without `[Mount]` (it dropped it) and leaves an empty `[Tuning]` out; the colours
  writer no longer eats the comment of the section after `[Colors]`; the shared review module clears erase holes at
  the right place on a bigger canvas, lays the sheet out by frame order, and puts the rider where the skin's own
  `RiderOffsetPx` / `SeatAdjustPx` put him (the page too, and it re-places him when the scale changes); the watcher
  splits by the sidecar's columns and removes a front picture whose cell is empty; the scan says which PNG it cannot
  read instead of aborting, wants RGBA, and checks the order among the three skin tuning sections; `make_hellfire.py
  --cfg` keeps the effect keys; a fresh creature cfg is canonical; Royal Parade's generator writes its strips through
  the shared module (its Aseprite sidecar keeps naming its `out\pack`: the generator is its source); `new_skin.py`
  takes its flags in any order and refuses `--over` with `--mount`.
- **After the second run** (2026-10-20): two findings in the game, both fixed. The cart-style seat sat 6 px higher
  than the ride seat on every drawn creature: a declared seat row still added the player's own `SeatAdjustPx`, whose
  mod default is -6 riding and 0 with the cart. A declared seat now ignores the player's own value (only the skin's
  own `[Tuning] SeatAdjustPx` or a mod's override nudges from it, in full), and the built-in rows moved by those 6
  (rat and worm 52, the snail 55, the dragon 68; the donkey's own seat is row 52 on the 96 canvas; the scaffold, the
  review tools and the example say 52), so the riding look is exactly as calibrated and the cart look matches it. The
  dragon's near wing (the front layer) was drawn behind the rider in the world: it was sorted above him but not lifted
  past his sprite's plane, and the game draws by distance; the front picture is now lifted past the nearest of the
  keeper's sprites (measured each frame in the body part's space) by `Looks.Rig.FrontLift`, 0.05 world units (2.5
  art pixels; the first lift was a fifth of a pixel, which tied in the depth buffer and flickered his shirt through
  the wing while he moved, seen 2026-10-20; the front-layer test now moves him 40 frames and counts the pixels lost
  at the shipped lift and at the old one). A scenario watches the settings picture after a mount switch frame by
  frame (when the worn skin flips, when the stage applies the new hitch gap, when the picture is reframed) and
  fails if the gap lags the skin by more than two frames: the user saw the cart take many frames to settle
  (2026-10-20). Another wears every built-in creature with the Coat colorized and checks that every Coat colour
  changed and no colour of another region did (the audit of every skin's regions is in docs/mount-colours.md). The cart readout's rule was wrong, not the gaps:
  the rat's and the dragon's declared gaps (judged by eye) leave their carts about 10 px further from the body than
  the donkey's, and the snail, worm and Gerry stand further still without any gap (narrower bodies), so the rule
  asserted is "no cart closer to its body than the donkey's"; the three declare no gap and their cfgs say why
  (docs/seat-investigation.md 9, with the measured table). The Riding tab no longer offers the player's nudge on a
  declared seat. (Third run: Gerry sat at 41.5 px instead of 25.5: a skin's own nudge is an override on the instance,
  which the global override flag did not see; the seat reads the instance's override owners too, and the readout
  now guards Gerry's 25.5. Fourth run: all green but the "pictures loaded and freed" scenario, which assumed nobody
  wears the rat: the player's own mount was the rat, whose one sheet is shared; it now picks a built-in creature
  nobody wears.)
- **The dragon's wings are folded** (the user, 2026-10-20, after riding it: the raised near wing, drawn in front
  of him, hid the rider), drawn as the user's reference sheets draw a resting dragon: the wrist is a tall peak above
  the back with the thumb claw hooking towards the head, the fingers run down from the peak as red lines, and the
  membrane hangs between them like a curtain to the ground behind the hindquarters, scalloped along its hem, wine
  against the body's red and outlined. Both wings root at the shoulder, where wings attach and where the rider
  sits: the peak stands above the shoulder and behind his head, the curtain hangs from it back over the flank, its
  lower edge along the belly line with the belly band and both hind legs in view under it, its tip pointing back
  along the tail over the rump (the blue references' long wedge; the user's mark), nothing on the ground
  (the references tuck the wing so when the dragon walks; a curtain to the ground, the big idle pose's, had to
  swallow the hind legs and sat in the drawn ground, and once its hem was pinned to the ground row under a
  bobbing, swaying peak the whole wing jiggled, the user's video 2026-10-20: now every point of the wing takes
  the body's bob and nothing sways), nothing of the wing forward of the peak. The whole
  method, the game's placing of the layer, the rider's measures on the canvas and the recipe for the next mount
  are in docs/front-layer.md. The tail is redrawn as the references trail it: tapering steadily from the hips to
  the ground, lying low with its tip on the feet's row, tapering on into a spike that ends two columns in from
  the canvas edge (the old tail kept one thickness to a kink up into an arrowhead that was widest at its far end
  and ran past the edge: a sausage with a cut-off tip; a diamond spade in between read as a hanging lantern), and
  its spines seated on the drawn top edge, two columns wide
  like the ridge's (placed from the curve's radius they floated a row above the edge and read as notches with a
  hole under each). The whole near wing is the front layer, over the
  rider wherever the two overlap: he sits inside the fold with the top of his head, part of his face and the
  reins arm out of it. (Tried on the way and rejected: a cut that kept his chest in view by drawing the membrane
  behind him there, and a one-layer wing shaped around him, which grew a lobe under his seat that stood out
  like a thumb when nobody rode.)
  The far wing is the same fold four pixels back and five up (the perspective), drawn behind the
  body, which hides all but its peak and the top of its curtain behind the near one's. (The user's corrections on
  the way: a curtain at the tail, one too high, a far wing of its own shape, and one "back on his ass" when the root
  sat at the mid-back.) (Eight drafts went before it: flat folds and fans along the flank read as a plank, a
  line, a blanket, a flag, a hand fan; the reference read at pixel size was what was missing; the dragon is compared
  with the reference crop at one scale before each hand-over now.) The front-layer readback takes a curtain that
  hangs behind the rider (the order is judged on the body). Two readouts joined the self-tests after that look: the rider's hip as a row of
  the worn skin's canvas (asserted on the dragon's declared 68, which the review strips draw him on) and every cart
  part's shift along the hitch direction (a rigid cart: the far wheel seen beside the near one in the Mods picture
  is to be judged with it). The settings picture no longer moves a cart part's mesh child a second time (the far
  wheel's child is named like its part; the world's cart skipped them already).
- **After the first in-game run of the new tests** (2026-10-20): three probes were wrong, the game was right. The
  cart readout asked a cart part's transform for its renderer (the mesh is a child, as the mount code reads it); the
  front-layer readback counted every drawn pixel, so "the body with the rider" could never be smaller than "the body
  alone" (it now compares the overlap pixel by pixel at one camera spot: whose colour shows); the over-colours test
  classed the scene-lit gold as "red" at dusk (it now checks that the lit red colour is gone, whatever class the
  light gives the gold).
- **The tests nobody had** (the plan's M5, docs/mounts-parity-matrix.md §6): the dragon's front layer read back
  in front of the rider (where they overlap, the pair shows the keeper's colour over the body overlay and the wing's over the keeper); Hellfire and Royal Parade
  on the built-ins themselves (whole frames with the erase marks, the ash coat, the cloth's reds under the harness
  colour, both walking); the snail's own rider offset on its wearer; the cart-style seat and the hitch gap of every
  mount (M2's readout).
- **The code that only knew the donkey** (the plan's M4, docs/mounts-parity-matrix.md §2): the rider's leg overlay
  (NearLeg) hangs from the skin's declared seat as the torso does, not from the donkey's back line; a baby spawned
  without a size is its parent mount's own (`SpawnFoal`'s default), and plain baby donkeys are 0.6 whatever the
  parent is; `Mounts.Spawn` with a cart as a no-cart mount is refused, and a hitched mount that becomes one leaves
  its cart standing where it is (the user's decision; before, the cart stayed hitched as data); `[Base.*]` on a
  replace skin and `[Colors]` on a palette skin are named in the skin's report instead of ignored silently; a mount
  may say `noun = ` ("a skull can't pull the cart"; Gerry says skull) and the save file's and the dormant list's
  labels say the mount instead of "donkey"; a mount with no skin is not offered; `MountSpawn.Mount` spawns a mount
  by name in its Default skin. The API records say more: `MountSkinInfo.Regions` (the colour regions the player's
  colours reach), `MountInfo.EffectWhen`, `Noun` and its tuning, `Skins` with the Default first; a code skin
  can declare its colour regions, an erase colour, a base recolour and the picture seat (`MountSkinDef.Regions`,
  `Erase`, `BaseRegions`, `SeatFromPicture`), and a readable sprite's colours join the look (an unreadable one is
  said once). `MountSettingsPage.EnableWhen` and `MountHasRegion` grey a colour row for a region a mount never has.
  Sounds, phase one: every sound key is documented as a mount's to set; the snail and the worm are silent (bray,
  hoofsteps, lip smack off), Gerry's lip smack is off; the scan reads every bound key (the helper-bound ones were
  invisible to it). Self-tests: the two cart-rule doors, the noun, the silent snail and skull, the baby's default
  size, the leg overlay on the snail's seat row, the snail's regions.
- **A skin comes with its effect; the ring fits the creature** (the plan's M3; the user's decision 9.5 (a)): a
  skin may say `effect = ` / `effect_when = ` (`MountSkinDef.Effect` / `EffectWhen` for code), shown when the
  player's Effect row says "As the mount or skin" (its default; the entry was "As the mount"): the skin's first,
  else the mount's. Hellfire says `effect = hellfire`, so a player who picks the skin sees the horns and the fire
  together without opening the Effect section. The effect ring's radius scales by the worn skin's standing-frame
  width over the donkey's 46 px (the rat's 85: almost twice the ring; an over skin or a code-sprite skin keeps
  1.0), measured at the sheet build. The Mounts tab has the skin's effect row; the scan and the layout know the
  two keys; `make_hellfire.py` writes the built-in's cfg once (`--cfg` rewrites it), so a regeneration keeps the
  hand-written lines. Self-test: Hellfire lit under the default row, Royal Parade not, the rat's ring 1.48.
- **The Mounts tab tunes the skin** (the plan's M2): a "The rider's seat on this skin" row (the picture row, the
  picture top, or none; right-click = none) and the rider, leg and cart pixel rows per style under "Rider on this
  skin" (a value already in `[Tuning]` is changed there, a new one goes into the style's section; right-click = not
  set), saved by the same auto-save into the skin's file; the mount's group keeps the speed, NearLeg and its
  other keys. The Riding tab shows the pixel nudge alone while riding a skin that declares its seat, and says why.
  The settings picture places the cart at the mount's hitch gap (the dragon's and rat's cart stood at the
  donkey's distance) and shows no cart for a mount that cannot pull one. Creature facts (the mount's cart rule,
  babies, effect, abilities) follow the wanted skin at once while the drawing's geometry follows the worn one, so
  a rider on the old picture never takes the new picture's numbers. A "cart readout" self-test measures, for every
  built-in mount with a cart, the distance from the body's rear edge to the cart body and the cart-style seat,
  and asserts the rat's and dragon's declared gaps; the snail's, worm's and Gerry's gaps are declared from its
  numbers next.
- **The skin owns its drawing's geometry** (API 0.33.0; the user's decision of 2026-10-20, docs/mounts-parity-matrix.md
  9.1): a skin's `[Tuning]` has `[Tuning.Ride]` and `[Tuning.Cart]` beside it for one style, merged after it and
  before nothing (the merge order is in docs/mounts-design.md §2; `Looks.LayeredValue` reads the same order for the
  hitch); `MountSkinDef.TuningRide` / `TuningCart` for code skins. The three pixel values that sat on mounts moved
  to their skins: the snail's `RiderOffsetPx` into `[Tuning.Ride]`, Gerry's `SeatAdjustPx` into `[Tuning]`, the rat's
  and the dragon's `HitchGapPx` into `[Tuning.Cart]`; the creature generator writes a creature's gap; the scan
  refuses a picture key under `[Mount.Tuning*]`, a key in `[Tuning]` and a style section, `SeatOffset` on a skin
  with a seat row, an empty section, and places the sections after `[Game]`. The Mounts tab's Save writes the three
  sections with their comments and regenerates the `seat` line with its remark. `MountSkinInfo` says the seat row,
  the picture seat, a front layer and a game source. Self-tests: Gerry's value comes from its skin and the mount
  holds no pixel key after a reload; a temp pack's `[Tuning.Ride]` value is the riding value on the wearer.
- **A mount's seat is its skin's row now** (API 0.32.0; the user: "ok let's go for it since it's part of the
  foundation for more mounts"; docs/seat-investigation.md §8). A skin may say `seat = <row>`, the canvas row (from
  the top) the rider's hip sits on; `MountSkinDef.SeatRow` for code skins. The rider sits there in every frame for
  every player, whatever their AutoSeat setting (the picture path Gerry uses, generalised, with the walk bob), and
  the mount's `SeatAdjustPx` nudges from it, so one declaration gives every player the same seat; before, a mount's
  `SeatOffset` reached only AutoSeat-off players and its `SeatAdjustPx` only AutoSeat-on ones. The rat and the worm
  say 46 (the donkey's own seat on the 96 canvas, measured in game: the first guess of 49 sat 3 px low, with
  both modes already equal), the snail 49 (3 px lower, its `SeatOffset` gone; a stray `SeatAdjustPx = -7` from a panel test, committed by accident, hid one pixel in the second run and is gone), the dragon 62 (the 128 canvas); the creature generator writes the row; the scan refuses a row outside the
  canvas and a mount that sets `SeatOffset`; the example mod's checker says 46. The game's own donkey art declares
  no row: a default player's seat is untouched (the readout self-test asserts 6.6 px). The Mounts tab says when a
  skin declares its row.
- **A built-in's reload and undo on the Mounts tab never took** (the user, 2026-10-20: "I changed torso down to
  -4 and pressed undo and it stayed yellow"; the note said "Not reloaded: Value cannot be null. Parameter name:
  path1"): the reload stamps the pack's folder for the auto-reload watch, and an embedded built-in has no folder,
  so it threw before the file was read and the mount kept the panel's value. The stamp is 0 for a built-in or a
  code skin now; the Gerry self-test reloads a built-in from the repo's cfg; the Undo and Reload buttons log
  their outcome, so a failure is in the log and not only in a five-second note. Undo is one step per gesture now:
  saves within four seconds of each other on the same file share the earlier restore point (a slider dragged in
  two moves with a pause took two presses).
- **The mount definitions brought to one shape** (the user: "take a look at the mounts and how we're defining
  them so that we have the same parity"; the investigation is `docs/mounts-foundation-audit.md`). Every built-in
  `skin.cfg` is in one layout now, written by `tools/skins/canonical_cfg.py` and refused otherwise by
  `tests/SkinsScan` and the release scans: `[Skin]`, `[Game]` for a game creature, the colour sections, and the
  `[Mount]` block last with id, name, author, version, cart and foals always (foal_scale, effect, effect_when when
  not the default), every tuning key in one section. Gerry's dead `SeatAdjustPx = 10` is gone (its one value, -16,
  in `[Mount.Tuning]`); the dragon and the worm say `cart` and `foals`; every mount has an author and a version.
  The loader: a `[Mount]` without a name takes its id (sentences and the dev spawn row use the mount's label, so a
  nameless mount never says "donkey" or throws); a built-in skin's mount is built in (`MountInfo.IsBuiltIn`);
  author and version survive a reload and count for "the same declaration"; unknown `[Mount]` keys, a bad
  `foal_scale` and a bad `effect_when` are named in the skin's report like every other section's. The Mounts tab's
  Save keeps the comments inside the mount sections (above a key, on its line, before and after the keys) and
  writes the block in the file's order. SkinsScan checks every `[Skin]` value the loader reads by name (layer, mount,
  seat, source, erase, the canvas range), the section order, `[Base.*]` regions and knobs, the `[Mount]` key list and
  values (foal_scale 0.2-1 and not the default, effect a built-in id with its effect_when, `Off`/`Night`/`Always`),
  a key set in both `[Mount.Tuning]` and a style section, an empty tuning section, a `[Mount.Tuning]` without
  `[Mount]`; probed with a wrong cfg (11 findings). Self-tests: Gerry's one seat value and its built-in mount with
  author and version; the pack round trip keeps four comments in the mount sections and the author; an ability that
  throws is off after its first tick while the mount's other goes on. Docs: `effect_when` takes `Off`, not "Never";
  `pivot` and the `[Mount]` author/version documented; the donkey's own block written out next to the five; the
  Mount list and "Default" rule; the stale "Saddle Up's Donkey rows", recipe and blue-dragon lines in api.md,
  reference.md, mount-colours.md, mounts-design.md and skins-design.md marked or fixed. The skin tooling is all
  under `tools/skins/` now (`creatures/make_creatures.py`, `gerry/`, `skin_colors.py`, `skin_preview_html.py`,
  `pngcolors.py`). Open: a mount's seat with the player's AutoSeat off (audit 3.1): `docs/seat-investigation.md`
  has the three seat paths, what every built-in gets under the shipped defaults (the snail's `SeatOffset` works for
  a default player and is lost with AutoSeat on; a mount's `SeatAdjustPx` does nothing for a default player unless
  the skin says `seat = picture`), the options and the compat constraint; a readout self-test ("seat: the rider's
  height on every built-in mount, AutoSeat off and on") logs the seat in pixels for each of the eight looks in both
  modes; docs/reference.md's seat defaults corrected to the shipped values (`SeatOffset` 0.22 both styles,
  `RiderOffsetPx` -6 / -8). Measured in game 2026-10-20: every look sits at 6.6 px with AutoSeat off and 11.5 px
  with it on (the player's two modes disagree by 5 px on the plain donkey), the snail at 3.0 px off and 11.5 on
  (its seat lost in auto mode), Gerry at 25.5 px in both (the picture seat). The seat code is unchanged; the user
  decides. The whole "Skins and effects" group passed in game the same day (16 scenarios, the three warnings the
  tests announce themselves).
- **The creature generator on the same pipeline** (the user: "so now all the skins are on the same level?"):
  `tools/creature_sprites.py` writes the rat, snail, worm and dragon straight into `assets/skins/<name>/` (one name
  as the argument redraws one; the probe's `test\skins\` output and the Blue Dragon entry are gone); all 85 + 3 x 44
  pictures came out byte-identical. Every built-in skin now comes from a generator in the repo writing into its
  `assets/skins/<id>/` folder; Gerry alone has no pictures of its own.
- **Skin leftovers cleared** (the user: "check dirs to see if there's anything obsolete ... so we keep parity"):
  `tools/skin_recipes.py` (the retired recolour recipes' prototype) and `horn_bases_raw.json` are gone from the
  repo; `contact.py` lost `load_raw` / `raw_to_game_shift` and `hellfire_proto.py` reads the probe's frames, so no
  tool reads the old top-left-aligned canvas any more (`horn_bases.py` measures the Hellfire horn bases from the
  Hellfire pack's own bone pixels: 44 of 44 frames the same as the moved v14 positions). Off the dev machine: the
  probe's `test\skins\` experiments, the example mod and the `worm-pack` test pack in the game's plugins, the
  Hellfire review rounds, the raw frame exports, the starter `.aseprite`. `pngcolors.py`'s note names its real user.
- **One pipeline for every skin** (the user: "we need to make them the same since they will both be included",
  "once we set up this pipeline we can keep following it to create more skins for other mounts too"): Royal Parade
  is a built-in pack like Hellfire now, `src/GK2DonkeyAPI/assets/skins/royal-parade/` with `skin.cfg` by hand and
  a PNG per frame written by `tools/skins/royal-parade/make_royal.py` (`--no-assets` for a dry run); the sheet
  form, the `packs/` folder and the copy into the game's `gk2skins\` are gone (the loose copy there was removed:
  two skins with one id are refused). The pipeline is written down in `docs/sprite-tools.md` "Making a skin: the
  one pipeline": generator folder, pack folder, checker + scan + review page, build and deploy, the in-game export.
  `tools/skins/check_skin.py` reads a PNG per frame as well as a sheet. The headless scan passes 17 of 17 with
  Royal Parade among the built-ins.
- **Hellfire is a pack now, not code** (the user: "for the concept of a mount skin we should do this drawing-over
  thing"): `src/GK2DonkeyAPI/assets/skins/hellfire/`, generated by `tools/skins/hellfire/make_hellfire.py` from the
  reviewed prototype: the prototype runs over each of the donkey's frames on the game's canvas and the pack keeps
  only what it changed (horns, the devil's tail, the rump fill, the cracks, the glowing eyes) plus erase marks where
  it took the ears and the tail off; the ash coat is the pack's `[Base.Coat]` recolour. `LooksHellfire.cs` (614
  lines) is gone; the recipe machinery stays for mods (the self-test registers a recipe of its own through the API).
  Same colour regions as before (the cracks follow the muzzle colour, horns and tail the dark parts, the glow the eye).
  Proven the same: `tools/skins/hellfire/compare_hellfire.py` puts every code-drawn frame next to the pack over the
  donkey with a difference picture and count; 44 of 44 identical with the prototype's coat (the devil's tail's sway
  per walk frame matched to the reviewed frames), and with the game's coat rule only coat shades differ: the rule's
  knobs were fitted to the code's darkening formula over the donkey's 14 coat colours (Brightness 0.41, Colorize
  FF4747 at 0.6), the largest difference left 5 of 255 on one channel. The ear remnant the user saw between the
  horns on the page was the horns' own outline, drawn the same by the code; the head pixels are identical.
  What did differ frame to frame (the user: "in some frames there's a gap between the horns, in others it's
  filled") was the mane's tuft that stood between the ears: one column in the standing frames, a block three wide
  where the head bobs (walk 3/4/7/8, the turn). Erasing it left a hole in his head (the user: "it should be
  filled"), so the generator now draws the same mane tuft between the horns in every frame: the four rows above the
  horns' bases, in the donkey's own outline and mane shades (region Dark, so the player's colour reaches them as it
  reaches the mane), 326 pixels added and nothing erased; the compare page shows exactly those 326 pixels as the
  only difference from the code's output, checked on a zoom of all 44 heads through the review page's path.
- **Skin packs: a recolour of the donkey under an over skin** (docs/skins.md "Recolouring the donkey under the
  skin"): `[Base.Coat]`, `[Base.Eye]` ... sections with the player's own knobs (Brightness, Saturation, Colorize,
  Tint, Overlay ...), applied before the player's settings and holding even with them switched off. An ash coat
  under horns without a pixel of the game's art. The erase self-test's pack declares a blue coat and the readback
  checks it.
- GK2 Donkey API 0.31.0: `MountSettingsPage.Heading(section, label, description = "")`, a heading row inside a
  section (a line with no value); in the look bar the look rows after it in that section stand under it in a group
  of their own. A `Color` row whose colour key has a `MovedKey` carries the alias to its `<key>Lightness` key by
  itself, so one `MovedKey` per colour is enough. The look bar deals its groups to the column that leaves the left
  closest to half the rows (before: to the left until the left held half, which with one big section put nearly
  everything there). A row may not share a picture row's (`Preview`) label: the picture is found on screen by it,
  and a heading with the same label got a picture of its own; the page refuses the row with a clear message.

Development:

- **The mounts gate is removed:** `Debug.MountsPreview`, `Plugin.MountsOn`, `Mounts.KeptOut` and every "switched off"
  text (Saddle Up's description, its "Other mounts and skins" row, the Mount and Skin rows' descriptions, the log
  lines) are gone; the built-ins always register. A dev cfg's `MountsPreview` line leaves the file at start (one
  "retired [Debug] line" log line). The self-test "mounts off" is gone, and the skips "the mounts are off" with it.
- **Self-tests:** "cargo: each mount carries its own" (a cart switched donkey, rat, Gerry, donkey: 6, held at 4 with
  four aboard, 2, 4, Cart capacity 2 wins, 6, nothing dropped); the speeds test checks each built-in's cargo.
- **Built in or external** (docs/making-a-mount.md): the repo's `packs/<Pack>/` laid out as `BepInEx\plugins\<Pack>\`
  (the dragon in `SaddleUpDragon`, the rat, snail and worm in `SaddleUpCritters`, ids by the pack:
  `saddleup-dragon.dragon`, `saddleup-critters.rat`; moved out of `src/SaddleUp/assets/mounts/`), each a small plugin
  of its own with its `gk2mounts\` embedded (`<Pack>.csproj`, `Plugin.cs`: `dreas.gk2.saddleup.dragon`,
  `dreas.gk2.saddleup.critters`, needing the API 0.40.0; one file, so an update leaves no file of an older version
  behind; first tried as plain folders linked into the game as junctions, the same day), deployed by its build,
  built into `dist/` by `build-dist.sh` (the Mounts tab saves into the repo through `GK2MountsSourceDir`); `skinroots.py` and every tool and scan walk both kinds (`kind()`, `pack_of()`), SkinsScan checks a pack's
  prefix, its `needs`, docs, project and version; `new_skin.py --pack` (with the plugin); NoLinqScan scans `packs/`; `make-release-zip.py` writes a zip per pack and an All Mounts
  bundle, `check-dist.py` checks the packs; a dev-only rename table (`Catalog.renamedMounts`) heals the old ids in a dev
  world and cfg; the self-tests' `M()`/`Sk()` map the new ids, the dragon's sound test reads its pack, and a new
  scenario "built in or external"; `.gitattributes` marks the art and sounds binary.
  After the first run with the packs (1222 of 1223): the Mounts panel test waits for the dragon's picture to be whole
  before switching (read from its pack folder it took longer than the fixed 30 frames); the hostile-files test builds
  its rooted path at run time (a `C:\...` literal sat in the DLL and check-dist refused it). dist/ rebuilt (the API
  0.40.0, Saddle Up 100 KB without the four mounts), the pack zips built and read back; the docs that still placed
  the four inside Saddle Up's DLL brought to the packs, dated notes on the design docs.
  The over-skin colour tests (an over skin keeps the donkey's regions, the erase colour) take their own colours only
  (`OwnLook`): the player's chosen mount's colours reached their test donkeys (a Red harness and a Black coat under the
  test's blue, with the dragon chosen); the Mounts panel test says which of its cart checks failed.
  The run's own checks (the world's own donkeys unchanged, no test donkey left behind) are a scenario of their own in
  a group "After the run", the last of every run, so a failure there has a red header (it showed under all-green ones).
- **Content from many mods** (docs/multi-mod-loading.md, investigated and built 2026-10-26): five new self-tests
  (Never lost: the held and set-aside side file, records carried forward, handle ownership; Skins and effects: hostile
  files, no throw between mods, order and identity, versions and ids); the "ids" scenario follows the new replace
  rule; SkinsScan refuses NaN and Infinity, file names outside a pack and an effect id without its prefix;
  `canonical_cfg.py` knows `needs` and `format` in `[Mount]`; tests/ApiSurface records sealed, abstract, virtual, base
  types, interfaces and nested types and marks a breaking addition.
  After the first run (1202 passed, 3 failed): the pack-sounds test expects the new `|`-joined file sound id; the
  dragon's jump waits for its pictures to be built (the jump began while they built, its first frame the walk); the
  herd test waits up to 30 s for a second standing call (1.5 to 4.5 s apart, put off by a step, never within 4 s: one
  call in 12 s happened). `Plugin.Version` is 0.40.0 (the csproj alone had it).
- **Each mount's jump in the dev tools**: the Mounts tab's "Its jump" rows (saved into the mount's `mount.cfg`,
  right-click back to the default's), the Flying tab's rows renamed "The default jump", the jump's log line and trace
  say whose shape it was. A new self-test in the Flying group ("jump: each mount its own"); `canonical_cfg.py` and
  `tests/SkinsScan` know `[Jump]` (its key order and ranges; refused in a skin.cfg); `tests/ApiSurface` snapshot
  updated (additions only). docs/flying.md "Each mount its own jump".
- **The sound coverage counts a babies' answer** (`Sounds.Coverage`, `tests/SkinsScan`): `answer` plays on babies
  only, so a `[Sounds.Baby]` line now covers it in the start-up log's line, the Mounts tab and the scan; before, every
  mount that gave its babies an answer was still listed as missing it. The new files are named after their events in
  each mount's `sounds\` (`voice_1.ogg`, `mount_1.ogg`, `answer_1.ogg`, ...), picked from the trimmed candidates in
  `C:\ClaudeCodeTemp\GK2-mount-sounds\` (its OVERVIEW.md says which file is which).
- **Surveyed: the user's new mount sounds** (docs/next-update.md section 4; trimmed candidates outside the repo in
  `C:\ClaudeCodeTemp\GK2-mount-sounds\`, with `trim_whole.py`, which cuts only the silence at the ends, never a gap
  inside a chatter). Three dragon growls, a landing thud, a wing beat, eleven rat squeaks, six snail squelches;
  nothing wired in yet.
- **Investigated: what the next update still needs, how the mounts differ in play, and which sounds they lack**
  (docs/next-update.md; the user, 2026-10-25: "see what is actually remaining ... every mount needs to have something
  which makes it useful"). Nothing built but the dragon's cart: its dead `[Tuning.Cart]` and the generator's gap for
  it are gone, and the five self-tests that hitched the dragon (the side-file round trip, the panel's card, the cart
  readout, the preview gap, the walk survey) use the rat or check the refusal.
- **Investigated: the donkey's two skins as a mod of their own** (docs/donkey-skins-pack.md; the user, 2026-10-25:
  "investigate ... GK2 Saddle Up Donkey Skins Pack"). Nothing built: the runtime needs no change; the open
  points are Hellfire's effect, the self-tests' sample picture and the release scripts.
- **The API retires cfg keys too** (`Plugin.DropRetired`, the analogue of `MountSettingsPage.RetiredKey`): a
  released key this version no longer binds leaves the player's file at the start, saved once, and the upgrade
  self-test counts it as retired, not as a value lost (first use: the seven `[Effects]` keys of 0.10.0). The effect
  self-tests drive the rig through a test-only `Rig.TestWant`; how an effect shows (trail on, round the mount only,
  in fights, not on babies, night below 0.35) is fixed in Effects.cs until the skin design. The dev panel's two
  effect editors, SkinsScan's and canonical_cfg.py's effect keys and page-carryover.py's EffectRows went with them;
  the public API snapshot updated (19 lines fewer).
- **No LINQ in anything that runs in the game** (the user, 2026-10-25: "get rid of ALL our LINQ usage ... and disallow
  ALL LINQ usage going forward"; every call allocates garbage). The last uses are gone (the settings page's clearing
  key, the chest's zone walk and dock check, the dependency manifest's sort and lookups, the dev chest probe and
  self-tests). The build refuses it from now on: `Directory.Build.targets` adds the banned-API analyzer to every
  netstandard2.1 project, with `BannedSymbols.txt` banning `System.Linq.Enumerable`, `Queryable` and
  `ParallelEnumerable`, as error RS0030 at the line that uses it. `tests/NoLinqScan` checks every built DLL for any
  reference into `System.Linq` and runs with the release scans. The .NET console tests and tools may still use it.
- **The probe audit** (the user, 2026-10-25: "check if we also have any hanging probes running which can skew our
  performance or even worse, we ship them by mistake"). Every dev tool is behind the dev tools' lock (the sources on the
  machine or the marker file; a player's [Debug] keys bind into a config that is never written), and the dev window
  and the perf logging default to off. Three things still ran for a player and are dev only now: the step-by-step
  diagnostic lines (a shuffle's turns and frames, a pose, a wake, a foal's route search: their strings were built on
  every call even when the 4-a-second budget dropped them; the lines a player's report needs, a spawn or a foal stuck,
  stay), the `[pick]` trace (only with the perf logging), and the Mounts panel picture's set-up dump (some 45 lines).
  Saddle Up and the other mods were checked too: nothing runs for a player that shouldn't (Owned Count's item dump is
  a Debug switch, off by default).
- **A look built ahead for a mount made from the game's own character (Gerry) waits for the game to be loaded**: at
  the main menu his sprites aren't in memory, so every sprite was scanned, the look refused with an exception and set
  aside, then offered again after the load, at every start with Gerry chosen (Performance Hog, 2026-10-25 13:43).
- **The dev stride probe is off until a survey turns it on** (the walk survey, the garbage survey's probe phase). On
  whenever the dev tools were, it appended a row to its CSV every walk cycle of every walking mount, each a file opened
  on the main thread: 1033 times in a 17 s ride on Gerry with his babies, 5 to 11 ms a time (Performance Hog,
  2026-10-25 13:38: 84 frames of 5 ms or more, all marked "file write"). Every dev measurement carried it, and it was
  the sound tick's 2.5 ms.
- **A skin's "loaded" line says what building it cost**, step by step: the sheet made, the pictures made (files read,
  decoded), put in the sheet, uploaded, sprites, colours; the frames it took, the most work in one frame, the game's
  slowest frame meanwhile, and the memory allocated. It was added to measure the hitch when the dragon is picked again
  in the Mounts panel (the user, 2026-10-25) before choosing a cure.
- **A pick in the Mounts panel is traced** (`[pick]` lines): the 120 frames after the click, every frame over 12 ms or
  with the mod's work over 2 ms, with what the mod did in it and what happened then (a look's build started or done, the
  picture showing the new look). The pick itself, the panel's rows built again, the panel's frame and its pictures are
  measured parts now (Pick, PickRows, PanelTick, Preview), and the pick's own two halves (PickApply: the Mount row's
  handlers; PickSave: the cfg file written). The once-a-second slow-frame line and the summary could not show these
  frames. The click's work is counted in the frame it happened in (it ran before the frame's bookkeeping and was counted
  in the frame before). Closing the panel is traced the same way (the world's mount turning into the chosen one), and
  a sound's play (SndLookup, SndPlay) and a sound file's load (SndStart, SndDecode) are measured parts, and inside a
  sound rig's tick an event's resolve (SndResolve), a whole emit (SndEmit) and the ground check (SndSurface). The play
  call was under 0.5 ms; the 2.5 ms in the sound tick every few frames is elsewhere, these find it. The self-test of an
  embedded pack's .wav waits for its turn in the one-a-frame queue instead of expecting it at once. The dev-only stride
  probe inside the sound tick is a measured part (DevProbe), and every 10 s the log has the garbage each part made over
  those 10 s (`[perf] garbage over the last N frames, KB a frame ...`; with the dev panel on), to find where the mod's
  20 to 50 KB a frame comes from. A self-test, "the garbage survey" (group Garbage), runs the cases by itself: on foot
  standing and walking, riding a dragon with two babies walking and standing, the dev stride probe on, the Mounts panel
  picking; one `[garbage]` line per phase with the garbage and time by part. The mods' keys polled each frame (before
  the API's tick, so its garbage went uncounted) are a measured part (Keys) and count in the mod's garbage. The survey
  hides the dev window while it runs: its first run found the window itself made 43 to 61 KB of the 49 to 68 KB a frame.
- **Less garbage from following babies**: a foal built its leader's name ("its parent dreas.gk2.saddleup#1") five times
  a frame for log lines that rarely run; now made only when a line reads it, and a mount's full id is made once. It was
  most of the mod's steady garbage (about 2 KB a frame of 2.1 with the dev window left out, the garbage survey).
  Then the mods' keys: each key's binding was turned into text every frame to see whether it changed (0.22 to 0.44 KB
  a frame, half of what was left); a key now hears its own setting change. Key combinations are compared and their
  modifiers checked without LINQ (the flight's jump and fly keys three times a ridden frame, the safe-spot key every
  frame).
- **The safe-spot self-test waits for the last return's arrival check** before it buries the keeper for "the watchdog
  off": the check runs two physics steps after a return and judges where he is then, so a step that moved him inside
  that window was sent to an older spot and counted as a watchdog return (1 of 1112 failed, 2026-10-24; timing only).
- **Skin layering's tools and tests**: `LooksInherit.cs` (what a skin gives, its default, the resolve), the producers
  split out of `Acquire` (`MakeProducer`, the variant's sheet in `VariantProducer`); `tests/SkinsScan`,
  `tools/skins/check_skin.py` and `tools/skins/canonical_cfg.py` read a variant against its `../default` (its canvas,
  the pictures it takes, its colours; `canonical_cfg.py --check` notes a line that only repeats the default's); four
  new F11 scenarios in the "Skins and effects" group (a variant built on its default, one drawn over it, a variant's lifecycle, a
  code mount against the version rule); the sounds scenario's second skin now takes its default's sounds (layer
  `"default"`); the default-looks scenario's cleanup withdraws the variants before the defaults.
- **The hillside jump test, run and corrected** (the flying experiment): the streaming fix found the slope and both
  jumps landed; its two failures were the test's own: the downhill start had been placed at the foot's height inside
  the ramp (a 17 px jolt as the physics pushed the body out), now both starts sit on the ground's surface; and a
  24-degree hillside climbs 3.2 px a physics step by itself, so the cap is the ramp rule's 4 px, not the flat yard's
  2 (docs/flying.md "The hillside, run"). The embedded-pack sound test waits for its graze line's own bank entry.
- **The flying and jumping sound events** (docs/mount-sounds.md; phase 2 of the sound system): `takeoff`, `flap`
  (the launch's kicks, then every wing beat airborne, not while gliding), `glide` and `fly` (stoppable: loops through
  the glide and the flight, cut at the way out and on the ground), `land`, `touchdown` (the thud), `landed`,
  `refused_takeoff`, `jump`, `jumpland` (the squash, or a jump in place coming down), `perch`. All silent unless a
  mount's or skin's sounds give them one. Self-test "the flying events" (group Sounds).
- **The route the built-in skins' sounds take is proved**: the API's DLL carries a test pack of its own
  (`assets\testskin`, never registered at start) with an `.ogg` and a `.wav`; the self-test "an embedded pack's
  sounds" registers it, decodes the `.ogg` through the cache file under BepInEx\cache, parses the `.wav` in place,
  plays a looping event and stops it. "A pack's own sounds" now also changes a file on disk, reads the pack again and
  hears the new file. What to hand over for the creatures' own sounds: OGG Vorbis or WAV, mono, 44.1 or 48 kHz, a few
  seconds, in the skin's `sounds` folder, named for the event (`voice_1.ogg`, `voice_2.ogg`, `flap_1.ogg`).
- **First sounds for the built-in creatures, from the game's own ids** (`[Mount.Sounds]` in the five skin.cfgs, to
  audition on the Mounts tab): the dragon growls (the zombie's attack cry pitched down), treads heavily (the stone
  step, two a cycle) and breathes at the grass (the furnace bellows); the giant rat squeals (the spitter's cry pitched
  up), patters (six light steps a cycle) and nibbles (the food sound); the snail is silent but for a wet blop getting
  on and off; the worm rustles as it drags (two a cycle), eats dirt (the shovel) and slaps wetly getting on and off;
  Gerry speaks in his own dialogue voice (the game's "garry" clips, in the speech channel) and clatters on bone steps
  (Larry's). The snail's, the worm's and Gerry's older `[Mount.Tuning]` pins stay. These are first picks by name:
  what to keep is the user's ear's call; a real roar and a real squeak want files of our own. The game's 311 sound
  ids with their clips are listed in `C:\ClaudeCodeTemp\GK2-music\sound_ids.txt` (`dump_audiocfg.py` there).
- **A sound rig per mount instance** (docs/mount-sounds-design.md, phase 1 step 2): `MountSounds.cs` (one static
  set of timers for the ridden mount) is now `Sounds.cs`: every instance has a `Sounds.Rig` with its own timers and
  animator reading, so the ridden one, a standing one in the Active tier and a foal all sound; every event is a row
  of the catalogue `SoundEvents.cs` (`step` with its surface variants, `wheel`, `graze`, `lips`, `voice`, `mount`,
  `dismount`, `hitch`, `unhitch`, `cargo`, `bell`, and the silent-by-default `setoff`, `stop`, `ear`, `turn`,
  `refused`, `sent`, `called`), resolved per instance into a `SoundSpec` (the source, volume, pitch, start, the
  event's own numbers, the layer it came from: the player's `[Ride]` / `[Cart]` keys are the base layer; a mount's
  and a skin's blocks come in the next step). Every rig entry point binds its instance, so a mount's pins reach a
  standing wearer too (the snail stays silent standing); `Resolve` binds as well, since the dev tab and the tests
  call it directly and its half-second cache must never hold an unbound reading (found by the test 2026-10-24). The lips animation event finds its instance through a view
  map (`Mount.ByAnimation`) instead of the ridden view only; a tier change to Idle stops a rig's loops. The dev
  Sounds tab lists every event of the ridden mount with its resolved source and layer and a Play button; `Perf.S.Sounds`
  measures the rigs. Self-test "the donkey unchanged" (group Sounds): every event resolves to the keys of both
  styles, the API's voice and bell play the game's sounds, standing and foal under the switch, the snail's pins.
- **Own sound clips as game sounds (the probe for a sound system of our own)**: `SoundBank` registers a clip of the
  mod's own (a `.wav` parsed by the mod, a `.ogg` or `.wav` file decoded by Unity) as an entry of the game's
  `AudioConfig` under a namespaced id, so it plays through the game's own pooled player with its 3D preset and the
  in-world mixer group (the Sounds slider, the pause). Self-test "the probe" (group Sounds), the dev Sounds tab's
  "Play the probe tone at the mount" button; the design, every mount sound event and the plan in
  docs/mount-sounds-design.md. The game-code map (game-code.txt) now sorts the never-lost classes (warned) and
  the flight's terrain and work-state patches (dev), which the Cecil scan had been failing on.
- **The dragon's legs redrawn** (mounts and skins are still off for players in this version): long, muscled legs that
  step properly when walking, paws with gold claws, the body 6 px higher on them (its seat row 62).
  The skin preview page takes `--study <folder>` to show a skin being drawn next to the shipped one.
- **The dragon flies with its wings spread** (the flying experiment): 14 new frames drawn by the creature generator
  (the wing beat, the take-off, the landing), played while the dragon is in the air; the skin scans accept them.
  The wing beat is now eight frames in which the body moves part after part, not all at once (it "danced"): the
  wing tips trail the arm, the head rides steady a moment behind the body, the legs stay tucked, each swaying a little
  on its own (all four in step paddled like a swimming dog), the tail whips from root to tip (docs/flying.md "The beat in time").
  In the air the near wing now covers the rider as the folded wing does on the ground: the whole near wing is in
  front of him wherever it crosses him (before, the wing went behind him wherever he sat). The raised wings
  are a little lower, so their tops are no longer cut off by the edge of the picture. In flight the front layer is
  now lifted towards the camera along its line of sight: the lift along the sprite's own axis had moved it 3 art px
  down on the screen, so the two halves of the wing didn't meet round the rider. A dev readout (`[occlusion]` log
  lines) reports where the front layer and the rider's sprites really are in the dragon's picture.
  The spread wings are now drawn like the folded ones: the same dark wine membrane and dark edge, and thin red veins
  fanning from the wrist as densely as the fold's (they had few veins, a lighter purple and thick bones). The far wing
  now rises from the dragon's back instead of floating a few pixels above it.
  The dragon now glides: flying along in one direction for a while, it sometimes stops beating and holds its wings
  open for a moment, held low over the rider's legs, flexing gently in the air and the body rising and sinking with them. It eases into the glide
  out of an upstroke and leaves it with a downstroke back into the beat. Stopping, turning or climbing ends the
  glide. The Flying tab has the timings and a "Glide now" button.
- **The gliding dragon's head nods gently** instead of holding perfectly still.
- **Never lost, for mods** (API 0.36.0): `Mounts.ReturnToSafeSpot(reason)`, `Mounts.LastSafeSpot`, the event
  `ReturnedToSafeSpot`, the Behaviour keys `FallGuard` (on) and `ReturnKey` (None), and a button row of a mod's own on
  its settings page, `MountSettingsPage.Button(section, label, description, onClick, buttonText, enabled)`.
- **The Tests tab's group titles in colour** (dev): white with no result, green when every scenario run passed, yellow
  when one warned, red when one failed; a run resets the groups it runs. A new scenario jumps at the real things of the
  player's yard (`World/yard(Clone)`): every built thing and fence from the side with room, and a slide along the
  fence, each landing on the ground, never underground, never sent back.
- **The jump probe** (dev): every jump traced into `BepInEx/GK2-jumps/` (the newest 30), frame by frame and physics
  step by physics step, with a landing audit at each touch-down (every rule of the landing check on its own, the game's
  walk grid from the take-off spot, every collider near the spot). The file's name says how the jump ended.
- **Jump and Fly keys, Space both** (dev-only for now, like flying): Saddle Up's Keys section has a Jump and a Fly
  row, shown only while flying is on. With one key for both (the default): a press jumps on every mount, a second
  press in the air takes off on a mount that flies (the wings take over from the jump, the camera eases up), a press
  while flying lands. With two keys each does its own (Fly takes off from the ground). Space does nothing for the
  mount during a dialogue, a menu, a fight in armour (it is the game's attack there) or while a key is being set in
  the Settings window, and a key just rebound counts only once let go. The keys are whole combinations (Ctrl+Space is
  Ctrl+Space). The API has them as the Behaviour keys `JumpKey` and `FlyKey` (0.36.0), and for mods
  `Mounts.Jump()`, `TakeOff()`, `Land()`, `Mounts.FlightState`, the event `FlightChanged` and `Mounts.FlyingEnabled`.
- **A mount says whether it flies and jumps**: `[Mount] flies = true` and `jumps = false` (written only when not the
  default), `MountDef.Flies` / `Jumps`, `MountInfo.Flies` / `Jumps`; the Mounts tab has the toggles and the panel the
  lines (while flying is on). The dragon flies by its flag: no code asks for the mount named dragon any more.
- **In the air the game's E and F prompts are off** (dev-only flying): the keeper's body stays on the ground under the
  flying mount, so the game offered the things under it (the smelter's F and contents drawn over the dragon), and F
  held there ended the ride as the work state and put the keeper down on that spot from the air, the mount left parked
  up there. Now a flight pauses the game's interactions (its own ladder pause; a jump doesn't), the work state is
  refused while flying, and a ride ended in the air parks the mount on the ground under where it flew. The game's own
  bubbles (a station's ready output, a quest mark) are screen-space UI drawn over everything in the world, the keeper on
  foot included, and stay so over a flying mount (an order shift and a fade were tried and taken out: docs/flying.md).
  Self-test "in the air" (group Flying); docs/flying.md "In the air: no prompts, no work state, parked on the ground,
  the bubbles".
- **The mount's jump feels like a platformer's** (dev-only, Space while riding plain; the keeper on foot doesn't jump):
  a short crouch, a quick rise, a moment at the top, a heavier fall and a squash on landing. Hold the key for the full
  height (28 px), flick it for a hop (16 px). The camera no longer bounces with a jump, and the jump is the same at any
  frame rate. **The mount jumps over things**: the keeper's body leaves the ground for the jump, passes over what is
  lower than the jump and is stopped by what is taller; it lands only where a flight could (never somewhere he'd be
  locked in). A jump keeps the speed you had and steers a little in the air, never faster than the mount really
  rides on the ground (no surge along a wall you were pressed against, none from running into a jump); a jump that
  comes down over something built (a workbench) carries on off it the way it was going, and lands past it; a jump
  lands beside built things where the keeper can stand (it was sent back there) and on a bridge's deck; no more
  jolts at the start and end of a jump on a slope or a bridge; one that can't touch down where it ends lands at the nearest good spot back along its way, the take-off
  spot only as the last resort, and says nothing (no bubble). Invisible walls stop a jump. The Flying tab has rows for each part; a new self-test
  "jump: the mount's arc" checks the arc and jumps live over a low wall and against a tall one.
- **The flying dragon's neck is shorter and steadier.** In the air its neck was long for its body and the head bobbed
  against the body on every wing beat (like a dancer's head); the neck is a fifth shorter and the head now moves
  with the body, a moment behind it.
- **The dragon shows climbing and diving.** Climbing, it arches its neck up and its tail hangs low; descending, or
  coming down to land, it reaches its head forward and down and its tail rises; it eases in and out of each.
- **Your mount appears as itself at once.** The first time you called it in a session, you saw the game's donkey for
  a moment before it turned into your mount (its pictures were being prepared, a few a frame). The mount you have
  chosen is now prepared in the background beforehand, and again whenever you choose another.
- **The dragon's tail is no longer cut off** at the edge of its picture while flying, and its folded near wing
  sits the same whether ridden or not (it moved a few pixels when the rider got on or off, and at take-off and landing).
- **Flying: no more hops over trees and ledges.** Flying low over a wood, or climbing out from under a ledge's edge,
  the dragon no longer jumps up 12-20 pixels at once: it rises smoothly to its height over what is under it, and is
  never drawn inside it.
- **Flying: two jolts gone.** Flying over a raised piece of ground (a cliff's top) no longer lifts the dragon about
  12 pixels in a jolt; it keeps its height in the world. And the start of a landing no longer drops it a few pixels in
  one frame: the body's up and down through the landing frames now carries on from the wing beat's.
- **Flying: landing by your home.** Landings near buildings were refused as "Nowhere to land here." though the
  ground was open: the check asked the game's navigation mesh for its NPCs, which buildings cut into pieces. A
  landing the mesh refuses is now judged by the keeper's own ground, by the game's own rules for where he can walk:
  allowed when he could walk from it to where he took off or it is open ground, refused on a rock top, a ledge or a
  closed pocket. The judging is spread over a few frames while the dragon hovers, so pressing land never freezes the
  game. A dev readout (`[flight-walk]` log lines) shows each judgement (docs/flying.md "Refused landings").
  The `[occlusion]` readout is now off unless switched on in the Flying tab; the landing's checks are timed in the
  `[perf]` lines (`Landing`, `LandGrid`).
- **The drawn ground, read from the game's terrain** (the flying experiment, docs/flying.md): the tiles of every
  terrain mesh read just before the game frees them, the highest drawn level at any point, checked on the Flying
  tab against the walkable ground; the flight's floor, walls and landings use it (no landing inside a rock table); a whole-map survey and a map drawing tool (tools/flying/map_survey.py).
- **The tuning keys moved to keypad 8/2 (torso) and 4/6 (legs sideways)**, off PageUp/PageDown and Home/End,
  which the Grave Zoom mod uses for its zoom.
- **A flying experiment (the tuning window's new Flying tab; docs/flying.md)**: the ridden mount (the dragon, or
  any mount with a switch) takes off, flies at a height with a hover, climbs and descends on keys and lands, the
  camera rising with it; a jump on the same lift. Free flight from a clear height: the keeper's body leaves the
  game's physics and flies over water, fences and low things, bumps into houses and trees taller than the
  flight, and comes down only where he can stand (a save or a
  dismount lands him first). Session values; on by default only where the dev tools are
  unlocked, never for players (with it off nothing changes).
  Landing cuts fixed: the body no longer flashes from its old spot for a frame when a landing sets it down (its
  smoothing is off for two physics steps), and the camera moves with the set-down instead of snapping to the body
  (which threw away its sideways trail: the view jumped across the screen). The API references the game's
  Cinemachine.dll for it. The flight's bubbles ("Nowhere to land here.", "Land first.") stay 1.5 s, not about 5
  (the Flying tab's "Bubble time").
- **The three pack scenarios clean up whatever stops them** (mount from a pack, skin pack, skin pack auto-reload;
  left from the first review pass): their donkeys and carts, the temp pack and the mount it declared, the temp folder
  and the auto-reload switch go in a `finally`, so a check that throws or a stopped run leaves nothing behind for
  the scenarios after it.
- **Self-test "speed: each mount's own"** (Looks group): the words at the band edges, every built-in mount's
  speed and cargo, Saddle Up's retired rows and its file, a code mount ridden (2.5, over a 1.1 in its tuning) and
  driven (2.5 times the donkey's ratio), the donkey at the cfg's speed, the panel's card (Speed, Cargo); the format
  scenario reads `speed` / `cargo` and leaves a `Speed` stat line out. `tools/page-carryover.py` reads `RetiredKey` calls.
- **`tools/skins/creatures/make_creatures.py` runs again:** the mount rename ("kind" to "mount") had put a real
  line break inside its `[Mount]` template string, a syntax error; fixed, and the template writes `speed` and
  `cargo` (as does `tools/skins/new_skin.py`).
- **The run makes its own starting state, wherever it is started** (2026-10-21: a run started in a house failed 29
  times, every ride "Not indoors.": the move to the test spot by SetPosition kept the house's light). Before the
  first scenario: the keeper free (waited for up to 15 s; a script or conversation that doesn't let go is the one
  case it doesn't start, and it says so), out of the armour view, outside every building (the remembered spot only
  when it's in none; else a spot in the nearest container zone that is in none), the light outdoor. At the end his
  spot, his armour view and his own light come back.
- **Indoors self-tests** (group Indoors): the two switches under the game's indoor light, and a real door (the
  game's own teleport into one of the scene's buildings) riding and driving, and back out (docs/self-tests.md).
  Safe from any start: the start's own light preset put back by name, only buildings whose zones are visited and
  wait for no quest, the player's foals left out of the test's doors, the keeper always brought back out
  (docs/indoors.md).
- **The first F11 run of the night** (2026-10-21): the sprite manifest check demanded a frame literally named
  `bdy_static_left`, which Gerry maps to his walk frame as the rules allow, so Gerry was set aside at game-ready
  and every Gerry scenario failed from that (three readouts, the game-creature scenario, the new heal check). The
  manifest is the frames the `[Game]` map names; the build keeps its own rest-picture fallbacks. The heal scenario
  runs after the Gerry scenarios now. Everything else in the group passed (25 of 29), the core group's new
  set-aside-lines scenario passed. The second run, the full set: 697 passed, 0 failed, 0 warnings, 1 skipped;
  the panel's fit measured the same a frame later (the nested layout settles in one pass). The mount-pin
  refusal warning said itself twice (keyed on the skin, before and after one was worn): once per mount and key.
- **The self-tests clean up after a scenario that threw**: between scenarios the runner withdraws any skin or mount a
  test owner left registered, puts the pack-watch switch back and deletes the temp pack folders, and says so in the
  log (a passing scenario leaves nothing). Before, a throw in the mount-pack, skin-pack or auto-reload scenario left
  its pack registered for the rest of the run.
- **The Mounts panel, second design** (2026-10-21, docs/mounts-panel-design.md; the user: a game's mount journal,
  not a wall of rows): a window of a fixed size that fits 1080p without shrinking; a card list on the left that
  scrolls with the wheel, each card with the mount's icon (its portrait, else its standing picture, made for
  every mount automatically); the chosen mount's portrait, name and description at the top right; its picture
  with the cart up to 3x on a dark stage; one line of what it does; and its rows under tabs (Skin, Colours,
  Effect), so only one tab's rows are on screen; a custom colour's rows appear when the preset is Custom instead
  of sitting there greyed. Done and Cancel as before. Later: colour swatches, a search box, a Call button.
- **The Mounts panel on a gamepad**, the game's way (its windows read first): the d-pad and stick move the focus
  through the list, the rows and the buttons, Left and Right step a focused setting's value as in the game's
  Settings window, Select presses, Back steps back like Escape. A focus frame like the game's marks what is focused;
  the focused row is kept in view. The mod's rows in the game's own Settings window step with Left and Right on a
  gamepad too now, as the game's rows do (they answered only the shoulder keys). The search box is mouse and
  keyboard only.
- **The Colours and Effect sections leave the Mods menu**: their picture and their "In the Mounts panel" lines were all
  that was left there, so the sections are gone from the menus (the cfg keeps them, as before). With the mounts
  switched off the Effect section's "coming in the next update" line stays.
- **Escape and the way back, done properly** (the user, 2026-10-21: opened while playing, Escape showed the pause
  menu; pause, Mods, Saddle Up, Mounts, colours must step back through every one). Measured in the game's and the Mod
  Framework's code first. The panel takes the player's controls while open, as the game's own windows do (the UI
  flag: a ride goes on), so the game's keys (Escape's pause menu, movement, E, the inventory) don't act under it,
  and it consumes Escape. The window it was opened from is not closed any more: it waits underneath exactly as it
  was, switched off, and comes back when the panel closes. So Escape steps back one level at a time: the search box,
  the colours window to the journal, the journal to the window under it (the Mods menu on Saddle Up's page, the
  Settings window on its page, the pause menu) or to the game, and from there that window's own Escape carries on.
  Done steps back the same way; Ride and Drive close everything and go into the game. Before, the panel closed the
  Mods menu through the framework's toggle, which reopens the pause menu and forgets where it came from.
- **A review of the journal's code** (one reader, the panel files whole): the list now scrolls to the chosen mount when
  the panel opens (it was laid out while hidden); Escape in the search box leaves the box instead of closing the
  journal; the pause menu is closed again a frame later too (the game's and the mod's Updates run in no fixed
  order); the pictures sit at whole-number scales (the stage is point-filtered: a fraction shimmered); a missing icon
  sprite is not searched for again per card; the scroll maths counts the filtered list; the Speed stat reads the
  Default skin's pin first; stale Cancel comments gone. To probe in game: typing in the search box must not walk
  the keeper or open the inventory (the game reads its own keys).
- **The Mounts panel, third cut: the journal and a colours window** (2026-10-21; the user: the second cut had a lot of
  empty space and squashed the customisation). The stage sits beside a stats card (Speed, Cargo, Skins since 2026-10-22) at
  one size on every mount; under them the Skin and Effect rows; the colours and the effect's four toggles have a
  window of their own ("Colours and effect...") with the mount's picture at its top and both colour columns at full
  height. The list is flat journal rows (the chosen one in red, hover brightens) with a scrollbar; the tabs are gone.
  No Cancel buttons: Done keeps, and Escape steps back (the colours window to the journal, the journal closed) without
  reaching the game's pause menu. In the colours window a custom colour is a row of the 24 named colours as squares
  (the chosen one framed, a click picks) instead of a "< Chestnut >" dropdown; its lightness row stays. Ride and
  Drive the cart buttons on the journal call the chosen mount as the keys do (the panel closes first). A search box
  above the list once it holds more than eight mounts; the mods' keys wait while it has focus.
- **A third review pass** (four readers: the second pass's own fixes, the panel and the registry read whole
  rather than as a diff, the tests and release data). Fixed: a skin with a `[Mount]` block is that mount's own
  skin whatever its `mount =` line says (before, renaming the block's id on a reload withdrew the new mount and
  kept the stale one, and removing the block kept the mount alive); a game-source skin whose frames were not
  there was worn as a blank sheet (the player's colours off the donkey, the skin's geometry on it): set aside
  now, the donkey shows, checked again while chosen and once a minute for every set-aside skin nobody wears,
  offered again when the frames arrive; the ten-second recheck never ran on its own (the rig did not stay
  dirty); an ability could take a look key (skin, effect, colours) while it was free (refused now, held or
  free); a data-only unhitch of a sleeping carted mount used the cfg's hitch gap, not the skin's; a set-aside
  skin still answered "can't pull the cart" and drew the paper without its cart; a colour or mount change while
  the same skin was building threw the half-built sheet away and started over; a skin registered late (or
  again) was never worn by an instance already wanting it; a free cart carried an ability's cart keys; the pack
  watch could reload a withdrawn or replaced skin held by a sleeper; set-aside skins sat in the dev lists and
  the save's record noun; a replaced skin's portraits were forgotten after its declaration was cleared (a
  joiner's stand-in leaked); the panel's follow-the-mount rebuild ran where a throw dismounts the rider (its own
  try now); with the mounts gate off the six effect rows were live in the panel with nothing to pick (shown
  nowhere); a custom colour's lightness row escaped the region greying (the worm's harness); opening the panel
  from the main menu asked a keeper who isn't there to speak (a log line); the panel's list did not follow a
  skin or mount registered, reloaded or withdrawn while open; one picture failure silenced the follow check; a
  page without `Preview()` had a blank picture; "Has babies, 0.4 of its size" and the set-aside line reworded;
  the sleep scenario counted every sheet (other sleepers and wakers move the count; it watches its own now). The
  fit-to-screen writes its measure to the log (two lines per open) so the nested layout's settling can be read
  before anything is changed there. `MountSkinInfo.IsAvailable` / `SetAsideWhy` added (API surface updated).
  Three more scenarios so the automated set covers what only eyes did: the Mounts panel itself (opens, fits the
  screen, lists, a pick follows, Cancel, the key toggles, the button brings forward), a game-source skin set
  aside and healed, the side file's set-aside lines. From the fourth reader: the skins scan let a canvas up to 512 through (the loader refuses past 256; it reads
  `MaxCanvas` now), refused the shuffle keys a mount may pin and let the lantern's through (refused at the
  merge), and refused `none` in `[Game]` (documented, honoured by the loader); docs/api.md and two code comments
  still said the first declaration wins and late skins need a restart; the CHANGELOG said the dragon has no
  second tone; the Saddle Up README lacked the panel marker on seven colour rows and the two Info rows; the
  settings scenario's mount count did not honour set-aside skins; the ownership scenario's session-layer check
  assumed nobody holds `HeadSettle`. Then two gaps in the pass's own edits: a healed (or newly set-aside) game-source
  skin now marks every rig dirty, else a wearer that had given up stayed a donkey until a door; the
  still-building shortcut compares the skin's definition, not its id, so a replaced or reloaded skin is switched
  to. Left for the release step: dist/README.md (rebuilt with the dist), the
  CHANGELOG's For players block still carries development asides to move under Development, `needs =` in the
  shipped cfgs (none has it: decide), try/finally in the mount-pack, skin-pack and auto-reload scenarios.
- **A second review pass** (four readers again, over the first pass's fixes, the end-to-end cycles, every other
  self-test scenario under the new rules, the docs and the panel against Unity). Fixed: the sheet was freed on
  every view rebuild, so an unhitch or a door rebuilt the skin over a dozen frames with the plain donkey showing
  meanwhile (freed on the sleep tier only now); a set-aside skin still governed the instance's mount (its tuning,
  babies' size, noun, abilities) while the donkey was drawn (the donkey comes instead, in mount too), and the
  "offered again" heal for it could never run (a set-aside skin someone has chosen is checked again every ten
  seconds); a renamed mount's old mount was never withdrawn on reload (the new mount shadowed it in the loop); a
  removed mod's set-aside value was dropped from the side file when the mount's merge or an ability held the key;
  a withdrawn skin left the mount it declared behind (withdrawn with it unless another skin declares it; the
  mount-pack scenario says so now); a joiner's stand-in portrait was never forgotten; a winner without a `[Mount]`
  block left the loser's mount pointing at it; the panel never drew a portrait (drawn under the mount's name now),
  said "times the donkey's" for a speed that is times walking speed, left the right side empty for a set-aside
  mount (it says why now), rebuilt its list from inside the clicked paging button (a frame later now), measured
  its nested layout once (fitted again a frame later), could lose its dim marker to a CanvasGroup the copied
  button carries, and let its lines wrap inside a fixed-height row; the Mounts button closed the panel like the
  key (it brings it forward, the menus out of the way); the four effect toggles stayed in the Mods menu without
  their Effect row (the panel's now); Saddle Up's Colours and Effect sections were empty in the menus (a line in
  each says where the rows went); a sleeper whose view is destroyed rather than parked (with a cart, mounted)
  kept its sheet (let go of either way); a
  Note row's visibility key named a Button; the round-trip scenario's rule for unwritten values lagged the
  writer's. Listed for later: a code mount against a loose pack's same-id mount (docs/open-items.md).
- **A full review pass over the night's work** (four readers over the diff, every finding checked against the
  code; the user: "this was big and complex"). Fixed: a removed mod's saved values counted as loaded when its GUID
  extended a loaded one (now exact GUIDs, and an ability owner only with its mod); an ability's values were saved
  into the side file and never forgotten on the first attach (session-only now, forgotten before Attached); the
  version rule replaced a skin but left the mount's declaration with the loser (the winner's own skin re-declares);
  the sprite-atlas copies of a game-source check lived to the frame's end, where a build in the same frame could
  take them for the game's own (destroyed at once); the Mounts tab's save was read back by the auto-reload (the
  watch is stamped too); a reload dropped the live mount off the def, so a renamed mount was never withdrawn from the
  second reload on; the panel drew the page's Mount dropdown inside itself and its right side did not follow a
  choice made there or elsewhere (the list is the picker, the right side follows the chosen mount); old rows
  were measured by the layout before their deferred Destroy (off first); a row a mount pins stayed live in the
  game's own Settings window (left out there too); a late mount's remembered skin was lost to the default at the
  catch-up; a mod's heading over plain rows vanished from the menus with the look rows (only a heading over look
  rows is the panel's); the Mounts key reopened the panel instead of closing it; loose packs counted as first
  party for the prefix note; the portrait cache never forgot a skin; 1.0 and 1.0.0 were different versions; an
  unavailable Default was still offered; an aside line could overwrite another mod's live value on the next load;
  one ability object in two mounts; and in the tests a ride's arrival unchecked, a scenario without a cleanup, a
  near-tautological count. Listed for later: the Mounts tab's save overwrites a joiner's own [Mount] block with
  the live mount's facts (dev-only, no shipped joiner).
- **The look bar became the Mounts panel** (`LookBar.cs` -> `MountsPanel.cs`); Idle rigs are skipped before the
  cameras; a `Set` of the same value by the same owner is a no-op; `Perf.S.LooksTick` measures the rigs' tick;
  the skins scan says which colour path each skin takes (the exact swap, or the cube over 48 colours: the snail
  and the worm). Saddle Up 0.12.0 on API 0.35.0; the API surface snapshot updated.
- **Foundation, built** (docs/mounts-foundation-2.md 6, the decisions of the night): the ownership rule, the id
  rule and the format are in (above), with four new self-tests ("tuning: a value belongs to the owner that set
  it", "ids: the mount's own skin owns its declaration; the same skin id twice, the newer wins", "format:
  description, portrait, stats, ability names, needs, a bad format, unknown [Game] keys", "skin: kept through a
  hitch, freed asleep far away, worn again awake", both sleep branches) and the settings scenario's panel checks; the cargo,
  morgue and corpse scenarios take a key over through the dev-only `ClearAny` instead of a silent Set). The seven
  cfgs carry descriptions; the generators write one into a new cfg. The Mounts panel follows.
- **The foundation for mounts from many hands, investigated** (docs/mounts-foundation-2.md): three code
  read-throughs with the sharpest claims checked by hand. Five findings that change the design (a value on an
  instance has no owner rule, the last writer wins silently; ids and a mount's facts go to whoever comes first,
  and a loose pack comes before every mod; the settings page grows 22 cfg keys per mount, which the user's
  Mounts panel absorbs; every mount rides the donkey's skeleton, a fast mount's legs at the donkey's stride;
  skin sheets have no cap and sleeping instances keep theirs), fifteen smaller fixes with lines, and the
  decisions left. One line fixed on the way: a reloaded embedded skin kept its DLL (`SourceAssembly`), so the
  Mounts tab can save Saddle Up's mounts after a reload.
- **Mounts as separate mods, investigated and done** (docs/mounts-as-mods.md): the runtime needed nothing (every
  mount is a pack the API already loads from a folder or another mod's DLL; no shipped code names a mount); what
  stood in the way is fixed above (the load order, the Info line) and here: the five mount folders moved from
  `src/GK2DonkeyAPI/assets/skins/` to `src/SaddleUp/assets/skins/` (Saddle Up's csproj embeds them, its Awake
  registers them: the content in the player mod, the backend in the API; a third project was tried first the
  same evening and folded back in, the user: "have them be in Saddle Up with the backend all working in the
  api"); the tools and scans walk every `src/*/assets/skins` through `tools/skins/skinroots.py`
  (`canonical_cfg.py`, `skin_colors.py`, `new_skin.py` with `--mod <project>`, the review page,
  `make_creatures.py`'s default, `tests/SkinsScan`, the `check_skin` loop in `release-scans.sh`). The sixteen
  self-tests that name the five mounts keep their subjects; the readout and Coat scenarios say "every shipped
  mount"; the Gerry test expects its mount from a pack, not built into the API; the temp packs' sample picture
  comes from Royal Parade now. Saddle Up needs API 0.34.0.
- `tests/fixtures/released/0.10.0/`: the player files as 0.10.0 wrote them, captured from the release zip's
  rehearsal run (checklist step 3b had been skipped at the release): the two cfgs with the keys new in 0.10.0 set off
  their defaults, 0.30.0's `defaults.cfg`, a real format-2 side file. The upgrade self-test runs over 0.9.1 and
  0.10.0 now.
- `tools/page-carryover.py`: an offline dry run of the page's carry-over, reading the rows and `MovedKey` aliases
  out of Saddle Up's Plugin.cs and checking an older cfg key by key (the fixtures by default, any cfg as an
  argument). Run it before the in-game upgrade self-test whenever the page's sections or keys move.
- The skin tooling is in the repo now (it lived in `C:\ClaudeCodeTemp\GK2-aseprite`, which a commit never saw):
  `tools/skins/` (the pack checker, the watcher, the two Aseprite menu scripts, the .aseprite writer, `gamedir.py`
  for the game folder from Directory.Build.props), `tools/skins/royal-parade/` (the generator and its measurements,
  outputs in a git-ignored `out\`) and `packs/royal-parade/` (the pack's source: `skin.cfg` and the generated
  `sheet.png`, copied into the game by the generator; moved again the same night, see "One pipeline for every
  skin" below). Only the game's frames stay outside, read from the asset
  probe's folder. Verified: the generator run from the repo writes a sheet identical to the game's, the measurement
  scripts reproduce their data files. Hellfire's reviewed Python prototype (the 14 rounds of 2026-10-18/19, the
  rules that `LooksHellfire.cs` carries in C#) is kept as `tools/skins/hellfire/hellfire_proto.py`.
  `docs/skins-inventory.md`: every skin, what it is made of, where its source, its tooling and its game-side files
  live, and what stays outside the repo.
- Royal Parade (the test over-skin): its generator makes the cloth from the donkey's frames and
  writes the pack into the game itself, no drawing step (docs/sprite-tools.md "Generated, no drawing"). The cloth is
  one shape measured on the standing frame, hung from the hip in every frame with each column on that frame's back
  line, so it rides the walk's bob and the graze's dip without changing size (the first cut, measured per frame,
  jumped in the graze clip). The generator writes a frame strip per animation (`strips\`) to look at before the game.
  Then the root of every placement fault: the generator's donkey frames (`harness_frames\*.raw`) had every frame's
  top-left corner at one spot, while the game stands each frame on one ground row, 2 to 17 px apart per frame; the
  generator, the checker and the crown anchors now use the asset probe's frames, the game's own placement
  (docs/sprite-tools.md "The canvas, found wrong").
- **The review page shows what the game built, not its own guess** (the user: "make sure what we see on those pages
  is the same we see in game"): the dev panel's Mounts tab gained "Export the loaded skins as built"
  (`Looks.ExportBuilt`): every skin worn since the start, its pictures read back from the texture the game draws
  from (after the erase compose, whole frames and all), and the donkey's frames as the game reads them, to
  `BepInEx\GK2-asset-probe\game-skins\`. The page prefers that export, says so on the card, and counts the pixels
  where its own composite would have differed (0 is the expectation; a number is a divergence to look at). It also
  gained an "all 44 frames" view: every picture of every skin, named, for a frame-by-frame look.
- `tools/skin_preview_html.py` rebuilt as the skin review page (the user: "we will use it a lot"): every skin the
  API knows from its real sources (the game's donkey frames, the built-in skins in the repo, the packs in the game,
  recipes drawn to pictures such as Hellfire), grouped by mount with the mount's own look first, an over skin shown
  composited over the donkey, the old test folders collapsed at the end; a sticky bar with the clip, a frame
  scrubber and step buttons (arrows, space), a frame-strip view (every frame of the clip side by side), scale, facing,
  night, rider. Output: `BepInEx\GK2-asset-probe\skins-preview.html` (local only: the game's art).
- Self-test "colours: a skin over the donkey keeps the donkey's regions" now reads back what the world draws: the
  overlay renderer through its real material and shader, photographed into a texture, with the swap on, with it
  off (the same picture lit by the scene, dark at night: colours are judged against that render, never against the
  picture's own values) and together with the body part. Royal Parade was seen in the world without its red while
  the Mods picture had it; this check tells the two paths apart. The player's own mount's worn skin is read back
  the same way, for the log.
- **Fixed: a skin drawn over the body (`layer = over`) sorted at the body part's own order**, so the two coplanar
  sprites left the depth test to decide and the overlay showed only outside the body's silhouette in some frames
  (the gold hem below the belly and the border above the back, the red cloth gone: the user's video of 2026-10-19
  17:08). Found by the readback above (the overlay drawn alone was whole; with the body it was not). The over
  overlay now sorts one above the part, the front layer two above when no keeper rides. Not in any public release:
  the skins are switched off for players until the next update.

## 0.10.0 (GK2 Donkey API 0.30.0), 2026-10-19

For players:

- **Only the donkey, for now.** This version's settings have a Mount row and a Skin row, and both hold one
  choice: the donkey, drawn as the game draws it. The other mounts (a dragon, a giant rat, a giant snail, a giant
  worm, Gerry the skull), the skins (Hellfire, skin packs) and the particle effects are built and in this download,
  but switched off until the next update, which switches them on; the settings say so in two "Coming in the next
  update" rows. Nothing you have changes: your donkey, cart, colours and saves carry over as before.
- **Fixed: with GK2 Mod Framework 0.1.19 or newer, Saddle Up's page in the Mods menu was "Faulted"** ("OnRegister
  failed: AmbiguousMatchException", reported 2026-10-19). The newer framework added a second `AddEnum`, and the page
  looked it up by name. It now calls the one it means directly; checked against framework 0.1.16, 0.1.19 and 0.1.23.
  The mod itself kept working; only its settings page in the Mods menu was missing.
- **A Mount section and an Effect section.** Right after Keys: your mount's picture, its Name, the Mount and Skin
  rows (the donkey alone in this version, see above), then the babies rows ("Babies", "Babies come indoors",
  "Babies are the same mount and skin"); after it an Effect section ("Effect": None or As the mount, with the rows
  for when and how it shows; nothing to show in this version). Your Behaviour, Donkey, Cart and Sound settings stay
  as they were, though their sections moved down in the cfg (the old copies are cleaned out of the file on the first
  save); the Name and the babies rows live in the Mount section now (your values move with them).
- **Mount, not donkey.** Saddle Up's settings say "mount" wherever yours may not be a donkey ("Ride your mount",
  "Send your mount away", "Keep your mount with the save", "Mount and cart sounds") and "babies" for the young
  ("Babies", "Babies come indoors", "Babies are the same mount and skin", "Babies have it too"); the Name and the
  babies rows live in the Mount section now (your values move with them); the Donkey section keeps the donkey's own
  coat and harness colours. The keeper's refusals name the mount ("This dragon already pulls a cart", "That is a
  cart without a mount", "Your mount is nowhere to be found"). The F11 panel's Mounts tab says "mount" instead of
  "creature". The game's delivery donkey is still a donkey.
- **The colours window stays put.** "Choose colours" now shows the picture in a slot of fixed size, the rows in two
  columns by section (your mount on the left, the cart on the right) with a heading each, and the custom colour and
  lightness rows always in place, greyed until the preset is Custom. Nothing moves when a value changes (before,
  those rows came and went and the whole stack shifted under the cursor), and a window taller than the screen is
  scaled down whole instead of running off the top. The picture no longer keeps the previous mount's lantern where
  it hung (the preview copy is undressed before it is dressed again), so the mount sits centred.
- **A side file this version can't read is kept, not lost.** If a save's `<save>.gk2donkeyapi.dat` is of a format
  this version doesn't read (a newer one, carried over by Steam Cloud from another machine), it is set aside as
  `<save>.gk2donkeyapi.format<N>.dat` and the log says so; before, the next save deleted or overwrote it, and a
  parked cart's load with it. Updating from 0.9.1: every setting, the custom colours and the kept donkeys and carts
  carry over (docs/compatibility.md).

Built in this version, switched on in the next update (players don't see any of it yet; the Development notes
say how it is switched off):

- **Mounts and skins.** A mount (Donkey, Giant Rat, Giant Snail, Giant Worm, Dragon) has skins (the donkey: the game's
  own and Hellfire; the dragon: its own look): the Mount row lists every mount, the Skin row that mount's skins (cfg
  keys Mount, Skin_<mount>, FoalsWearSkin, Effect...). A mount carries its own seat for the rider and its rules (a mount
  can refuse the cart), and mods can give one abilities. The "Effect" row gains "As the mount".
- **Skins.** Your donkey can be drawn as something else: the giant rat, the giant snail, the giant worm and a red and a blue dragon (their wings raised above the rider) are built in
  (our own art). Donkey settings: "Skin", a list to pick from (the donkey, the built-in skins, every pack installed;
  in the Mods menu, the game's Settings window and the colours bar), and "Babies are the same mount and skin". The skin is
  kept with the save. Skins from other mods come later.
- **Hellfire, a skin of the donkey that changes the drawing, not the colours:** horns where the ears were, a long
  devil's tail with a spade where the tail was, eyes that glow, lava cracks across an ash coat. Drawn from the
  donkey's own frames the first time it is worn, a few frames at a time, so every clip has it and nothing of
  the game's art ships. The earlier five recolour-and-pattern skins (Zebra, Ghost, Ember, Golden, Shadow)
  are gone: the colour settings do that now. Blue Dragon is gone the same way: a blue dragon is the red one
  with a custom coat.
- **Skin packs.** A skin from anyone, no code needed: a folder `gk2skins\<id>\` with a `skin.cfg` and a PNG per donkey
  frame, dropped into `BepInEx\plugins` (or into any mod's folder there). The log says what it found: pictures that
  match no frame, frames still to draw. A pack that goes missing leaves the donkey as the game draws it, and the choice
  is kept for when it comes back (the list shows it as "not installed"). How to make one: docs/skins.md. A pack can be
  one sheet PNG instead of a file per frame.
- **Gerry.** The game's talking skull as a mount (Mount: Gerry): his own frames out of the game, three times his
  size, hopping along with the jaw chattering, a somersault for an idle; cart and babies allowed. A skin pack can do
  the same with any character of the game (`source = game`, docs/skins.md): no pictures to draw, just the
  character's preset prefix and which frame goes where. A mount's babies have their own size (`foal_scale` in the
  mount file, the Mounts tab's "Babies' size"; Gerry's are his own size, the donkey's foals stay 0.6), and Saddle
  Up's babies take it within a second. The rider sits on Gerry's skull and rides its (much reduced) hop: a skin may
  say `seat = picture` (the rider's seat is the picture's own top, per frame) and a game-creature skin `lift` (how
  much of the creature's jump is kept). The Mounts tab says when the player's AutoSeat (off by default) makes the
  torso rows moot, and lets a mount set the torso height players actually use; the Instances tab's "Log drawing"
  writes every renderer around a mount into the log. The Mounts tab saves by itself, like the Cargo tab: a moment
  after the last change the values are in the skin's file (and the mount's declaring files), with the time shown,
  "Undo the last change" to put a file back, and no Save button to forget.
- **The cart sits behind a longer mount.** A mount file can set `HitchGapPx` (the whole cart, shafts included, that
  many art pixels further behind than behind the donkey); the dragons set 24, so their body isn't drawn inside the
  cart. A cart left standing stays where it was drawn, and a mount taking a cart stands its gap ahead of it. The F11
  panel's Mounts tab has the row for the cart style; the "hitch and unhitch" self-test checks it on both facings and
  across leaving and taking the cart.
- **Hellfire.** Donkey settings: "Effect" (None or Hellfire: the game's own lamp fire in a ring round your donkey, with
  its sparks and a trail of flames that burn out behind it; mods can add other effects), "Effect shown" (never, at
  night, always), and whether it leaves the trail, goes round the cart too, shows in fights and on the baby donkeys.
  Kept with the save. Never indoors. (First build 2026-10-17 19:36: the Effect list had no "None" entry because an empty id can't be a dropdown choice, so a pick never stuck and the cfg stayed empty; the id is `none` now and a cfg holding "" takes the row's default.)
- **Your colours are kept per mount.** The coat, harness, paint, wheel, hub and lantern-light choices belong to the
  mount you set them on: a green dragon and a brown donkey, each as you left it, and the cart's paint with each.
  Switching the mount brings its own colours back. The donkey's values from before are its own unchanged; every
  other mount starts with the game's colours. Every mount's default skin is listed as "Default".
- **Your colours on every mount.** Coat colour, harness colour and the custom colours with their lightness now
  recolour any mount the same way they recolour the donkey: a dragon's scales, the rat's fur, the worm's skin.
  Each built-in creature says which of its colours are its outlines, its second tone (the dragon's belly, the
  rat's tail and ears, the snail's shell) and its eye, so those follow the matching settings and the rest is the
  coat. A creature without a harness ignores the harness rows. Before, the colour rows did nothing on any mount
  but the donkey. (docs/mount-colours.md)
- **The settings picture changes mount in one step.** Picking another mount or skin re-frames the picture at once,
  on what the new mount draws over two seconds of its walk (a hopping skull included); before, the new mount was
  drawn inside the old one's frame until the once-a-second edge check caught it, then framed on one frame the hop
  overshot, so the picture changed size two or three times in the first second (the video of 2026-10-18 21-07). The
  picture's texture is resized in place, so the displays never go blank between refreshes.
- **A mount asleep far away is still that mount.** Its kind (what its babies' size, its name in messages and its
  effect follow) came from the skin drawn on it, and a mount asleep out of sight has nothing drawn, so it counted
  as a donkey: Gerry's babies stayed donkey-sized (0.6) until he was called and woke (seen 2026-10-18 indoors, four
  big skulls). The kind now follows the mount's skin setting whether or not it is drawn; the self-test "game
  creature" checks a sleeping Gerry.

For mod authors:

- **The mounts, skins and effects API is in this build but switched off** (`Mounts.MountsEnabled`, false in Saddle
  Up 0.10.0's download): `Mounts.Kinds.Kinds` lists the donkey alone, `Mounts.Looks.Skins` and `Mounts.Effects.Effects`
  are empty, and `Register`, `RegisterRecipe`, `RegisterFolder` and `RegisterEmbedded` on them return a handle that
  does nothing (0 for the counts) with one log line per owner, nothing thrown. Register in Awake as usual and check
  `MountsEnabled` to tell your player; the next update switches it all on with no change on your side. The rest of
  0.30.0 below describes the API as it works then.
- **A skin worn over the donkey keeps the donkey's colours** (`layer = over` in its own colours): before, the
  worn skin's colours replaced the donkey's whole colour map, so the donkey under a saddle cloth lost the player's
  coat and harness colours. Now the donkey's regions stay, the colours the skin's `[Colors]` names join them, and
  the colours it doesn't name are kept as drawn (not Coat). A named colour the donkey also has is the donkey's, said
  once in the log. docs/skins.md, docs/mount-colours.md.
- **A skin is the drawing, a mount the creature** (docs/mounts-design.md §1): a skin's `[Tuning]` / `MountSkinDef.Tuning`
  takes only the picture-dependent keys (seat, torso, legs, hitch gap, depth, centring) and refuses the rest with one
  log line; the per-skin foals flag is gone (`skin.cfg foals`, `MountSkinDef.Foals`, `MountSkinInfo.Foals`, the
  `foals` argument of `RegisterRecipe`): whether foals wear a skin follows its mount's `Foals` and the player's
  `FoalsWearSkin`. `HitchGapPx` is a tuning key of the cart style (docs/reference.md); `Looks.LayeredValue` gives a
  creature's value for a style it isn't in yet.
- `MountSettingsPage`: a section moved to another place on the page (its cfg number changes) keeps a player's values:
  a key the cfg lacks under its section takes the value under the same name with another number (of several old
  copies the fullest, then the highest-numbered), and the old copy is dropped from BepInEx's orphans so the file holds
  each value once; `MovedKey(section, key, oldSection, oldKey)` does the same for a key moved to another section or
  name (docs/api.md, "Sections keep your order"); self-test "settings: a moved section keeps a player's values".
  `MountRows` lost its `legacySection` argument (it served one development cfg). `MountRows` is the Mount and Skin
  dropdowns; the babies rows are `FoalRows(section, companion)` (Foals, FoalsIndoors, FoalsWearSkin); `Preview`'s
  default label is "Your mount". Refusal texts name the instance's mount (`Instance.Noun`, internal). Skin packs:
  `source = game` with a `[Game]` section (prefix, scale, key lines) makes a skin out of another game character's
  frames (docs/skins.md); a skin.cfg's other sections survive the Mounts tab's Save. `MountSpawn.Skin`:
  the spawned one wears that skin (and is that mount) from the first frame, as `handle.Tuning.Set(owner, "Skin", id)`
  would; a handle's description says "dragon with cart" rather than "donkey with cart".
- `MountCompanion.Skin` and `FoalsWearSkin`; the instance tuning key `Skin` (`handle.Tuning.Set(owner, "Skin", id)`,
  saved with the instance; "donkey" or "" is the game's own look); `Mounts.Looks` (`Skins`, `Find`, `Report`,
  `MountSkinInfo`); the settings row kind `MountSettingsPage.List` (a pick list of ids with labels, read when first
  shown); skins from mods: `Mounts.Looks.Register` (a `MountSkinDef` with an `IMountSkinSource`), `RegisterRecipe` (a rule
  over the donkey's own frames, `MountSkinFrame`), `RegisterFolder`, `RegisterEmbedded`, `Unregister`, `FrameKeys`,
  `SkinsChanged`; docs/api.md "Skins"; examples/GK2DonkeySkins. API 0.30.0, Saddle Up 0.10.0 (needs 0.30.0).
  `IMountSkinSource.TryGetFrame` gives a `Sprite` (from any texture of yours), not a `Texture2D`.
- Mount kinds (docs/mounts-design.md): what is ridden as a creature type against a skin (how it is drawn). `Mounts.Kinds`
  (`Kinds`, `Find`, `Register` with a `MountKindDef`: the rider's seat for it as values of existing tuning keys for
  both styles or one, `Cart`, `Foals`, a default `Effect`, `Abilities`; `Unregister`, `KindsChanged`), `MountAbility`
  (Attached / Detached / Mounted / Dismounted / Tick hooks, one object per kind), `MountHandle.Kind`,
  `MountSkinDef.Mount` (a skin names its kind; `Foals` nullable), a pack's `[Mount]` and `[Mount.Tuning*]` sections (no
  code), `MountEffects.KindId` ("As the mount"), `MountSettingsPage.MountRows` and `EffectRows` (the rows in one call
  each; Saddle Up uses them). The built-in creatures are kinds declared by their own skin.cfg.
- A skin's front layer: pictures drawn in front of the rider (`front/` in a pack, `sheet_front`, `MountSkinDef.FrontSource`),
  sorted above the keeper's body while he rides, as the leg overlay is; the dragons' near wing uses it.
- A skin's own rider settings and placement: `MountSkinDef.Tuning` (a pack's `[Tuning]` section: existing tuning keys
  set on the wearer as overrides owned by the skin while worn, cleared when it comes off, never saved) and
  `Offset` / `CartOffset` (`offset` / `offset_cart`: the pictures moved on the donkey, riding and hitched).
- Effects: `Mounts.Effects` (`Effects`, `Find`, `Register` with a `MountEffectDef` of parts, sources and emitters,
  `Unregister`, `GameParticles` for the game's own particle systems, `EffectsChanged`), `IMountEffectSource`,
  `MountEffectEmitter` (ring, body, trail), `MountEffectWhen`; the instance keys `Effect` and `EffectWhen`,
  `MountCompanion.Effect` / `EffectWhen`, `Mounts.EffectSettings`; `MountSettingsPage.Toggle` takes `visible`;
  docs/api.md "Effects". Hellfire is defined through this API.

Development:

- Self-test fixes from the first full run with the mounts on after the gate (2026-10-19): "settings: the Mount, Skin,
  babies and effect rows" left the dragon chosen before its per-mount checks that assume the donkey (a test bug since
  it was written); "recipe: built from the donkey's frames" can't watch the build when a donkey of the world already
  wears Hellfire (said as INFO now); "colours: the player's regions" reads the as-drawn hue only by day (at night the
  preview is blue-lit: an open item). The pack auto-reload compared against the files at its first check rather than
  as read, so a change made before the switch went on was missed: fixed, the scenario is green.
- `tests/fixtures/released/0.9.1/dreas.gk2.saddleup.cfg` gained the six `Custom*` palette keys (CustomCoat = Plum ...)
  that 0.9.1 wrote and the reconstruction had left out, so the upgrade self-test covers the custom colours too. The
  0.9.1 files were run live through the 0.10.0 build on 2026-10-19 (release-checklist.md section 5): 0 lost of 485 values.
- **How the mounts are switched off for players**: the dev key `Debug.MountsPreview` (off by default; a player's
  cfg has no [Debug] keys, so it answers false there). Off, `Looks.RegisterBuiltIns` and `Effects.RegisterBuiltIn`
  don't run (nothing but the donkey kind exists, so every list and the colours per mount hold the donkey alone) and
  the public API keeps registrations out (`Mounts.KeptOut`: a handle to nothing, one info line per owner); a skin
  that is set but missing is an info line, not a warning, while off. On, everything as before. The F11 panel's Debug
  tab has the switch (a change takes the next start) and the log's first lines say which state the game is in.
  Self-tests: a `SKIP` state (counted apart from warnings; the panel shows it), the scenario "mounts off: only the
  donkey offered, registrations kept out", and the eleven scenarios that need the built-ins skip while off (run the
  suite with the key on for them). The flip for the next release is one change: the key's default to true, or the
  key gone and the two `RegisterBuiltIns` calls unconditional.
- `tests/FrameworkScan` check 5: no GK2.Framework member looked up by name (a newer framework's overload makes such
  a lookup throw); it fails on the 0.9.1 DLL and passes on the fix.
- **Skin packs reload by themselves while drawing** (dev only, `Debug.SkinPackAutoReload`, off by default; a switch
  under the Mounts tab's "Reload from the file"): a pack in use is read again a second or two after its files change
  on disk. With the watcher (`watch_skin.py`, every save in Aseprite exported, checked and previewed as GIFs), a
  round is draw, save, look. Self-test "skin pack: read again by itself when its files change (dev switch)".
- **Sprite tools** (docs/sprite-tools.md): after Hellfire took 14 rounds of frames, skins move to a draw-and-look
  loop in Aseprite, built from source for our own use. Two Aseprite scripts, "GK2 Export skin" (every visible layer
  but the donkey reference into the pack's sheet.png, an optional check run after) and "GK2 Part to all frames" (a
  part redrawn once, sent to the other frames at each frame's own place), and a draft generator for the Royal Parade
  skin (a caparison and a crown; the crown placed from the reviewed Hellfire horn positions). Self-test "colours: a
  skin over the donkey keeps the donkey's regions".
- The F11 panel's Instances tab spawns any mount with any of its skins ("Mount and skin" cycler above the buttons;
  "with its cart" and the family button greyed with the reason when the mount refuses the cart or has no babies; the
  babies wear the parent's skin; rows name the mount and skin). Its wording says mount, not donkey.
- Tests of the mounts and skins system (docs/self-tests.md): a headless `tests/SkinsScan` (every built-in skin.cfg
  parses as the loader reads it, all 44 pictures per skin at the canvas size, the front layer, a skin's `[Tuning]`
  holds picture keys only, a mount's tuning keys exist, a mount declared by several skins is declared the same way;
  in `release-scans.sh`); self-tests "settings: the Mount, Skin, babies and effect rows" and "skins and kinds: the
  changed events"; "hitch and unhitch" checks the hitch gap on both facings and across leaving and taking the cart;
  "kept-with-save text" keeps the skin id and leaves the mount's own values out; "settings: a moved section keeps a
  player's values" covers several old copies, `MovedKey` and the orphan cleanup; the pack tests announce the warning
  they cause; "mount kind from a pack" carries `foal_scale` through the pack, the save and the reload, and "game
  creature" checks the seat on the skull's own picture and the babies' size per mount (Gerry 0.33, donkey 0.6).
  The eight looks scenarios are in docs/self-tests.md now. docs/gerry-mount.md: Gerry's sprites (73
  frames, 96 canvas, a hopping skull) and three ways to make him a mount.
- Hellfire is `LooksHellfire.cs` (a recipe over the donkey's frames in top-down canvas coordinates; its pieces placed
  from the eye, the rump, the ear and the torso's box; prototyped in Python over exported frames first). The five
  recolour recipes, Blue Dragon and their tests are gone; `Colors.RegionSourceColours` gives a recipe the donkey's
  coat and outline colours; a code recipe can declare colour regions (`SkinDef.ColorRegions`).
  The game's Hellfire was checked against the prototype over all 44 frames, at the game's own canvas offset: the same
  pixels. To get there, everything that rounds now rounds the same way in both and relative to its own box (half up;
  the game's half-to-even and absolute positions moved a crack by a pixel with the frame's place on the canvas), in
  double precision, and the torso's edge is measured again after the tail is gone in the head-down frames. The hand
  tables (ears, horns, eyes) are relative to the drawn area, the horns sit on the eye as in the ear-twitch frames.
- `MountSettingsPage.PerMount(rows)`: a row set per registered mount (the donkey's keys plain, the others suffixed
  `_<mount>`), the chosen mount's shown (`Row.Shown`), a row's own condition apart (`Row.Enabled`: the look bar
  greys instead of hiding); `MountCompanion.LookOf(mount)`, `MountId` and the `MountLook` type; the companion's
  `Coat`, `Harness`... are the current mount's. The self-test "settings: the Mount, Skin, babies and effect rows"
  covers it. `Looks.DefaultSkinOf`: a mount's default skin, labelled "Default".
- Colours per look (docs/mount-colours.md): a skin's colours are collected as its sheet is built and sorted by
  region from its `[Colors]` section (loader, SkinsScan with a PNG reader of its own); the colour engine maps the
  worn look's colours per sprite group and uses the colour cube over 48 colours; the lightness anchor is per look
  and a companion re-applies its look when the skin's sheet is whole; self-test "colours: the player's regions on
  a mount in its own colours" (the settings picture read back). The `[Colors]` lists of the five picture skins.
- **Compatibility with released versions** (docs/compatibility.md): the four stores a player owns and the rule per
  store (page keys and enum names permanent, sections free to move, `MovedKey` for moved keys; API cfg names
  permanent; every released side-file format stays readable). `tests/fixtures/released/0.9.1/`: the player files as
  the public release wrote them (reconstructed from commit 7ac9a6c), with every value off its default; later versions
  are captured at release (checklist 5, steps 3b and 3c). The self-test "upgrade: a released version's player files
  are all read" runs Saddle Up's cfg through the live page's own carry-over (`MountSettingsPage.Upgrade`: every old
  line lands on a row and reads as its type), checks every old API key is still bound, reads the side file through
  the real reader, and the set-aside of an unreadable one. The release checklist's "nothing shipped" bullet is
  replaced by this check. Open: changed defaults never reach existing players (BepInEx writes every value); the
  stamp-and-table mechanism is described, decision pending. The live run with the 0.9.1 file (2026-10-18) found the
  last row's old line still in the cfg: BepInEx saves inside every Bind, before the page drops the old copy. A page
  now holds the mod's save-on-set while it binds and writes the file once at Publish (`FinishBinding`; one write on
  a first start instead of one per row, as the API's own cfg already did), and logs how many old lines it left out.
- Looks.cs: the skin rig (docs/skins-design.md step 1): overlays under bdy/hed, the hide re-asserted per frame, the apply
  in Camera.onPreCull, attach/detach at the view's lifecycle points, refcounted pictures; the self-test "skin: on, off,
  removed"; the built-in skins under src/GK2DonkeyAPI/assets/skins (tools/creature_sprites.py writes them).
- Step 2: the pack loader (Looks.RegisterFolder/RegisterPack/Unregister/Validate), the self-tests "skin pack: a folder
  read, validated, worn, withdrawn" and "skins: no leaks over spawn and remove".
- Step 5: the public registration API (a mod's textures stay its own; the same owner replaces, another is refused).
- Step 4: recipe skins (LooksRecipes.cs: the frames read back from the character atlas onto the 96x96 canvas, a pixel
  rule per recipe, 6 frames a tick, the switch whole); the self-test "recipe: built from the donkey's frames, worn whole".
- Step 3: the List row across FrameworkLink (AddDropdown with the ids; MenuLabels writes the labels), SettingsRows and
  the look bar; docs/skins.md for pack authors.
- Step 6: Effects.cs, the particle effects system (templates copied from the game's particle systems by material, the
  materials held through Addressables; a holder per instance that follows the body; ring, body and trail emitters;
  the night test on the `_SunLight` shader global, no game member; the trail's pieces pooled and replayed in place,
  nothing made or destroyed while walking), Hellfire registered through the public API; the
  self-test "effect: hellfire lit, follows, out, gone with the donkey"; `Perf.S.Effects`.
- The layers kept apart on the panel: rows the ridden creature sets are orange and locked on the Riding tab (tooltip
  says where to tune them), orange in a donkey's edit mode (drag = that donkey's own, right-click = the creature's
  back), owners named plainly; a cleared instance key returns to the creature's value (mods' Clear too).
- The tuning panel's Mounts tab (TuningWindow.Mounts.cs): a creature's own values edited live and saved into its
  skin.cfg (Looks.SaveCfg / ReloadCfg, Kinds.Touch / Redeclare); the creature generator leaves an existing skin.cfg alone.
- Kinds.cs: the mount kind registry (code and pack declarations, the donkey kind), the kind resolved on the skin with a
  version counter, the tuning merge per style on the skin rig, the cart rule in TryMount / Hitch / SwitchStyle, the
  abilities ticked on the rig (`Perf.S.Abilities`); self-tests "mount kind from a pack" and "mount kind from code".
- Looks.cs reworked for scale: a `SkinBase` record (the donkey's keys, parts, canvas, pivot, ppu, sprite prefix,
  atlas, back line; everything reads it), one sheet texture per skin filled a few frames a tick from any source (PNGs,
  a sheet PNG, a recipe; `Loaded.Produce`), sprite sources for code; the rig's parts as slots per base part; the
  self-tests count one texture per skin. The per-frame apply allocates nothing (Perf.S.Looks shows bytes).

- Investigation: can the mods bring art from outside the game (another animal, flames, fiery steps)?
  docs/external-assets.md, and tools/GK2AssetProbe (dev only, never shipped; Shift+F7) to answer it in game.
- Design draft for "Looks" (Hellfire, skins in an open folder format, sounds of a skin, an optional "Earn it"):
  docs/mount-looks-design.md. Not built.
- The skinning system designed in full against the code (lifecycle, drawing, formats, persistence, the public API, the
  settings row, validation, game updates, tests, build order): docs/skins-design.md. Not built.
- tools/skin_recipes.py (with tools/pngcolors.py): prototype "recipe" skins made from the donkey's own frames, for
  judging in game with the asset probe's test skins (local previews, never shipped).
- tools/skin_preview_html.py: a local HTML page animating every test skin next to the plain donkey (every clip, both
  facings, day or night); it embeds the game's frames, so it stays on the dev machine.
- The asset probe 0.2.0: the window in tabs (Skins, Fire, My files, Capture, Game, Old tests), and a Capture tab that
  films the donkey and its rider for 4 s, played on the preview page; and "Export the rider": the keeper alone
  (rider.png), which the preview page can lay over every skin.
- tools/creature_sprites.py: a giant worm, a giant snail (with a saddle), a giant rat and a dragon in two palettes, red and blue (128 canvas: body on the seat line, a bat wing raised over the back so it shows above the rider, a scalloped membrane, spines, belly plates; from the user's reference sheets, our own drawing) drawn by code (our own art, no game pixels), a picture for
  every donkey frame, fitted to the donkey's seat and ground; and a high-wing dragon recipe (dragon_high) whose wings
  show above the rider.

## 0.9.1 (GK2 Donkey API 0.29.1), 2026-10-16

For players:

- **Smoother frames while playing.** The Mods menu's donkey picture and its lists looked for the Mods window four times
  a second while you played, even with the menu closed: each look took 1-2 ms, so a frame of 2-3 ms extra four times a
  second. It now looks only at the main menu or while the game is paused (the only times the window can be open).
- **No memory held by parked carts.** A cart left standing (H while driving) and hitched again was kept in memory
  afterwards (about 50 objects each, up to about 30 carts). It is now let go at once.
- **Two logs stacked on the cart** line up better: the top log is placed slightly differently along the cart.

For mod authors:

- No API change. `Mounts.ApiVersion` is 0.29.1; mods asking for 0.28.0 or 0.29.0 load as before.

Development:

- Profiling with Performance Hog (a per-mod profiler for GK2, used on the dev machine only): docs/performance-hog.md.
- tools/GK2PerfRunner (dev only, never shipped): drives the keeper round a recorded loop and runs scripted profiling
  suites (on foot, riding, the cart with six items, standing, calling and leaving the cart), several in a row, with a
  count of the API's caches to find leaks. tools/perfhog-report.py reads the reports (`--suite`, `--series N`).
- The showcase director (dev only) opens with Shift+F9 now (F9 and Ctrl+F9 are Performance Hog's), and no longer
  floods the log with the game's "Cannot find instance of EnvironmentEngine" at the main menu.
- Results and baseline numbers: docs/performance.md.

## 0.9.0 (GK2 Donkey API 0.29.0), 2026-10-16

The first public release (sources as committed in `6be69a2`).
