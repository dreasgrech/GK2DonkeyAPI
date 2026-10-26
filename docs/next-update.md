# The next update: what is left, what each mount is for, what each lacks in sound (2026-10-25)

The user: *"see what is actually remaining from it to release our next major update ... remove the ability for the
dragon to haul the cart ... decide actual gameplay functionalities and differences between the actual mounts so that
they are balanced ... a list of important missing audio files from our custom mounts ... every mount needs to have
something which makes it useful or something which other mounts aren't as good at."*

Four parts: what still stands between the repo and the release (1), the mounts as they are built (2), what each could
be for and what that costs (3), the sounds each lacks (4). The dragon's cart was taken out today (5). This page
supersedes `saddleup-scope.md` sections 5 and 6 for the mounts; the release mechanics stay in
[release-checklist.md](release-checklist.md) and the in-game checks in [open-items.md](open-items.md).

## 1. What is left before the release

The next release is the one that switches mounts and skins on for players (0.10.0 and 0.10.x shipped the donkey alone
behind `Debug.MountsPreview`). Grouped by kind; nothing here is new work beyond what the checklists already say, except
the decisions in B.

**A. The switch and the texts**

- `Debug.MountsPreview` flipped (or removed) so the download offers every mount (release-checklist.md section 1, last
  bullet); the self-test "mounts off" then runs with the key on, or goes.
- The CHANGELOG's 0.10.0 block "Built in this version, switched on in the next update" moved up into Unreleased's
  "For players" and corrected. Stale in it: the pack folder (`gk2skins\<id>\`; it is `gk2mounts\<mount>\skins\<skin>\`
  since 2026-10-24), "a red and a blue dragon" (one dragon), Hellfire "drawn from the donkey's own frames the first time
  it is worn" and its fire with the Effect rows (nothing shows an effect since API 0.37.0), no Royal Parade, the
  "Effect" row's "As the mount", and now the dragon's cart (it says a mount can refuse the cart; the dragon
  is the one that does). Unreleased's own "What the next update brings, corrected" entry is the newer text to merge
  it with.
- Saddle Up's texts that say "switched off until the next update": the plugin description and the Mount section's
  row (`src/SaddleUp/Plugin.cs` lines 76 to 77 and 129), the README's intro, its Mount and Skin rows and its Behaviour
  note "its cargo, 6 items for every mount for now" (the dragon has no cart now), the root
  README's mount table (updated today for the dragon), `docs/api.md`'s `MountsEnabled` note, `dist/README.md`.
- The version lines (release-checklist.md section 6): Saddle Up's number (0.12.0 now, never released; a new minor
  at least), the API's (0.37.0), the BepInDependency minimum, the README tables, api.md's history entry.

**B. Decisions that change what ships** (the user's)

1. **Flying: decided, it ships** (the user, 2026-10-25: "make flying not dev only"; API 0.38.0). It is the
   dragon's one ability; with its cart gone, flight is what the dragon is for (section 3). What it still needs, from
   docs/flying.md and open-items.md: the open "through the ground on the high rock table" question (studying, nothing
   built; the fall guard catches a fall there), the hand tests of free flight listed under step 2 (cross water and
   back, over a fence and a building, off a cliff, a refused landing over water, a save in the air, H in the air, a
   door while flying), the sections marked "not seen in game" (the jump against things, the floor risen to, jump and
   fly on one key, the platformer jump, climbing and diving shown), and Space in a fight (in armour Space is left to
   the game's attack, so no jump or take-off during a fight, logged once a fight: as designed, to be said in the
   texts).
2. **The donkey's two skins as a mod of their own** (docs/donkey-skins-pack.md): in Saddle Up or a pack mod; if the
   latter, before or after the gate flip.
3. **How a skin comes with an effect** (open-items.md "Effects on skins"): Hellfire ships without its fire until this
   is designed; fine for the release if the texts say so.
4. **The mounts' gameplay** (section 3): the cheap trade (speed against cargo) can be in this update; the rest is a
   ranking.
5. **The shipped defaults**: `BackSeatExtraPx` (0 or 5, mounted-combat.md), the `needs =` lines in the shipped cfgs
   (task 52), and the standing question "changed defaults never reach existing players" (open-items.md Undecided):
   no 0.10.0 default has moved so far, so nothing to build unless one does.

**C. Still to see in game** (the headlines; open-items.md "To check in game" has each with its log lines)

- The five self-tests changed today (the list in section 5) in the next F11 run.
- The Mounts panel with a gamepad (never tried with a controller); the paging without the framework after the Effect
  section's removal (open-items 10); the panel's first row group heading (cosmetic).
- Skins built on their default, by eye (no shipped mount has a second skin yet).
- The babies of the ridden mount through a door, Gerry's (fixed 2026-10-24).
- Switching mounts, the rest: the compressed flight clips by ear, the 2.5 ms sound-rig tick, the 55 ms frame at game
  load, the panel-close trace.
- Sounds: the by-ear checks of the probe tone, the dragon's flight clips, the herd's calls and the babies' answers
  (phase 3), the standing sounds.
- The 0.11 regroup's pages without the framework; the shadow and grass bend on the dragon and Gerry (parity 2.5).

**D. The release itself** (release-checklist.md sections 2 to 6, nothing new): the release build and the offline
scans (`tools/release-scans.sh`), **step 3b this time** (the fixture capture was skipped at 0.10.0 and rebuilt from a
rehearsal's cfgs afterwards), the first run as a player with mounts on (add to the usual H/G/cart round: the Mounts
panel open, each mount picked and ridden once, G on the dragon shows "A dragon can't pull the cart.", a skin picked,
babies on, the upgrade rehearsal over the 0.10.0 fixture), `dist/README.md`, the Nexus page text (its 15 placeholders:
screenshots, the trailer, the repo URL), the open-items hand-off list, the commit and the tag.

**E. Uncommitted**: everything since the last commit (the effects removal, the skins-pack investigation, today's
dragon change and this page). The user commits.

## 2. The mounts as built (the baseline)

From the five `mount.cfg` files, `Catalog.cs` and `tests/SkinsScan` (2026-10-25, after the dragon's change). The
cart speed is the riding speed times 0.75 (the donkey's 1.5 over 2). The panel's word for a riding speed: below 1.4
Very slow, to 1.8 Slow, to 2.2 Normal, to 2.7 Fast, from 2.75 Very fast.

| Mount | Riding | With the cart | Cart | Cargo shown | Babies | Flies / jumps | Voice | Steps | Sounds missing (silent) |
|---|---|---|---|---|---|---|---|---|---|
| Donkey | 2 (Normal) | 1.5 | yes | 6 | 0.6 | no / yes | the game's bray | by the ground | none (the game's own) |
| Giant Rat | 2.75 (Very fast) | 2.06 | yes | 6 | 0.6 | no / yes | `spitter_attack` pitched up (a placeholder) | the ground's, six a cycle | mount, dismount, answer |
| Gerry | 2.25 (Fast) | 1.69 | yes | 6 | 0.33 | no / yes | the game's `garry` lines (his own) | `larry_steps` (bone) | mount, dismount, answer |
| Giant Worm | 1.75 (Slow) | 1.31 | yes | 6 | 0.6 | no / yes | none (by choice) | `bush_chop` (a rustle) | answer |
| Dragon | 1.6 (Slow) | no cart | **no** (today) | no cart | 0.6 | **yes** / yes | `zombie_attack` pitched down (a placeholder) | `step_stone` | mount, dismount, answer |
| Giant Snail | 1.25 (Very slow) | 0.94 | yes | 6 | 0.6 | no / yes | none (by choice) | none | answer |

What a player can feel today, with mounts on: the riding and cart speeds, whether a mount pulls the cart, the babies
(and their size), the voice and the other sounds, the colours and skins. That is the whole list. `cargo =` is a label:
`Catalog.Entry.Cargo` is "shown only, for now" (`Catalog.cs:34`), the panel prints "Cargo: 6 items" as if it were
live, and every cart holds the player's Cart capacity (`CartSlots`). Flying and jumping are on for everyone since
API 0.38.0 (the same day, after this was written): the dragon's flight is the one ability a player feels beyond
speed. So five of the six mounts differ by speed alone, and the speed ladder is the only balance there is:
faster is simply better, since the cart carries the same six items at every speed.

## 3. What each mount could be for

**The rule to keep.** A difference counts only if a player feels it in this update's build. A mount's file states
facts about the creature (it pulls no cart, it flies); the player's switches stay on top of every fact (Allow mounting
indoors, Cart capacity, Babies). A fact may refuse something or give the creature something of its own; it never turns
a player's switch on for them. That is the pattern `cart = false` already follows, and the one any new fact should.

**The levers, by what they cost.**

| Bin | Lever | Where it is | Cost |
|---|---|---|---|
| 1. Data today | riding speed (and the cart speed with it), cart yes/no, babies yes/no and their size, the voice and every sound, the colours and skins, the description and `[Stats]` lines | `mount.cfg` | none |
| 2. One small API change | **cargo enforced per mount**: the cart's slot count becomes the smaller of the player's Cart capacity and the mount's `cargo =`; the key, the panel line and the `CartSlots` plumbing (`MountRegistry.ApplyCartSlots`, `Cargo.ApplySlots`) exist | `Cargo.cs`, `MountRegistry.cs`; re-applied when a cart changes hands (a hitch), never dropping a load (the existing rule); `MountInfo.Cargo` already public | small; one self-test; the "speed: each mount's own" test asserts every built-in "carries the most (for now)" and changes with the values |
| 3. A feature | a mount that can't come indoors (`indoors = false`: a refusal at the door and inside, like the cart's; the dragon and a 3x Gerry are too big) | `Mount.WhyMustDismount` / `WhyNotIndoors`, the panel line, the pack and code defs | small |
| 3. A feature | a light on the mount at night (`light =`: ride without the cart's lantern and still see; proposal 1b of mount-looks-design.md; `Lantern.cs` has the light, the dragon's fire or Hellfire's would be the look) | a light source placed on the mount's view, the day/night rule the lantern already has | small to medium |
| 3. A feature | a game resource while riding: energy (`PlayerEnergyGameResSystem.Add`), stamina (`PlayerStaminaGameResSystem.Add`; the game spends it on attacks only, `StaminaSystem.ConsumeStamina` with each weapon's `staminaCost`, and regenerates it by the ConstDefs `stamina_regeneration` and `stamina_regeneration_delay`), insanity (`PlayerInsanityGameResSystem`; the perk `lack_of_sleep_debuff` adds insanity for energy spent) | an ability's tick (`MountAbility`) on the ridden instance | small to build; **the numbers are the work**: size them against what the game spends first (a dev probe logging the three resources over a day's work and a fight), else the mount is either pointless or a cheat |
| 3. A feature | crossing water (the free-flight machinery moves the body off the navmesh; at ground height over water tiles it is a swim) | `Flight.cs`, `FlightGround.cs`, the landing rule | medium; its own investigation like flying's |
| 3. A feature | a weapon on the mount (Hellfire's ring burning enemies, proposal 1c; the game's `AOEDamageSource` with a team filter) | the particle engine kept in `Effects.cs`, the fight rules of mounted-combat.md | medium to large; the game's balance |
| 3. A feature | burrow travel (the worm between known spots; the game has a `TeleportPointGraph`) | unknown | large; an investigation first |
| 1. Data today | flying (the dragon's `flies = true`) and jumping (every mount): on for everyone since API 0.38.0 | `mount.cfg`, the four keys | the hand tests in 1.B.1 |

**The recommendation: two dimensions a player feels on every haul, in this update.** Speed is one; make cargo the
other (bin 2) and set the two against each other, so the fastest hauler carries the least and the slowest the most.
Then no mount is strictly better than another for a player who hauls, and the dragon's trade is the cart itself.

| Mount | Riding | Cart | Cargo | What it is for | Beyond this update (bin 3 options, the user ranks) |
|---|---|---|---|---|---|
| Donkey | 2 (Normal) | yes | 6 | the all-rounder: a full cart at a normal pace, the game's own look and sounds, the two skins | nothing needed |
| Giant Rat | 2.75 (Very fast) | yes | **2** | the courier: the fastest thing on four legs, a light cart (two items, the game's own delivery cart's load) | nothing needed; it is the small mount that goes everywhere the switches allow |
| Gerry | 2.25 (Fast) | yes | **4** | fast with a middling load, and company: the one mount that talks (his own lines on the idle timer) | insanity eased while riding him (the keeper's friend; size it first); too big for doors (`indoors = false`, he is drawn 3x) is a restriction, not a use |
| Giant Worm | 1.75 (Slow) | yes | **4** | the middle of the ladder, a silent hauler | the worm has no natural use yet: crossing water, or burrow travel, would be its own; decide after a look at either |
| Giant Snail | 1.25 (Very slow) | yes | **6** | the slow full hauler; silent | a restful ride: energy restored while riding it (size it first: the game's energy is spent by work and restored by sleep; a snail that pays for a day's work is a cheat, one that pays for the walk home is a use) |
| Dragon | 1.6 (Slow) | **no** | no cart | **flight** (crosses water, fences, cliffs and roofs; the fastest way across the map), on since API 0.38.0 | a light at night (its fire) is the cheapest visible addition; a fight role (stamina back while riding it, or the burning ring) is the thematic one and the costliest |

Why these cargo numbers: the game's own delivery cart carries two, so the rat's two is a load the player knows; four
is the two-layer cart (Cart capacity's middle step); six is the full cart. A player who sets Cart capacity to 2 gets 2
on every mount (the smaller of the two wins), so the setting keeps its meaning. The snail and the donkey both carry six:
the snail is the choice when the cart matters more than the time, and the donkey's edge is the pace; if that is not
enough of a difference, the snail is the place for the first bin-3 ability.

What this does not solve: a player who never hauls sees speed alone, and there the rat wins outright. The bin-3
abilities are what would change that (a light, a resource, a crossing), one per mount at most, each sized in game
before it is promised. None of them is needed for the release if the texts say what each mount is for.

**What a new fact costs in the code**, so the estimates above hold: a `[Mount]` key parsed in `MountPacks.MountFromCfg`
and written by `SaveMountCfg`, a field on `Catalog.Entry`, `MountDef` and `MountInfo` (the public API snapshot), a line
on the panel's card (`MountsPanel.cs`), the scans (`tests/SkinsScan`, `canonical_cfg.py`'s `MOUNT_ORDER`), the docs
(making-a-mount.md, skins.md, api.md) and a self-test. Cargo enforced needs none of the parsing: the key exists.

## 4. The sounds each mount lacks

Three sources, merged per mount: `tests/SkinsScan`'s MISSING lines (the donkey's own events a mount is silent on until
its pack gives them), the placeholders the cfgs mark themselves (a game sound chosen as a first pick, with "wants a
file of our own" in the comment), and the events silent by default on every mount that the creature's character calls
for. Ranked by how often a player hears them: the voice (every 30 to 60 s standing or riding, and at mounting),
getting on and off (every ride), the steps (always), the babies' answers (only with babies on), the flight (the dragon,
only if flight ships). The event names are docs/mount-sounds.md's; a file goes in the mount's `sounds\` as
`<event>_<n>.ogg` with no cfg line, or on a `[Sounds]` line.

| Mount | Missing (silent now) | Placeholders to replace | Silent by default, wanted | Files to make |
|---|---|---|---|---|
| Dragon | `mount`, `dismount`, `answer` | `voice` (the zombie's cry pitched down; the roar "wants a file of our own"), `step` (the stone step), `graze` (a bellows) | `fly` (a wind loop while airborne), `touchdown` (the thud), `landed`, `refused_takeoff` | roar x2-3, a grunt on and off x2, a baby's chirp x2, wind loop x1, thud x1 (the take-off, landing, flaps and glide are the user's own already: 12 files) |
| Giant Rat | `mount`, `dismount`, `answer` | `voice` (the spitter's cry pitched up; "a real squeak wants a file of our own"), `step` (the ground's, light), `graze` (nibbling, fine) | | squeak x3, a scrabble on and off x2, a baby squeak x2, a patter step x1 (optional) |
| Gerry | `mount`, `dismount`, `answer` | none (his voice and Larry's bone steps are the game's own, by id) | | a bone clack on and off x2, a small skull's chatter for the babies x2 |
| Giant Worm | `answer` | `step` (a bush rustle), `graze` (a shovel), `mount` and `dismount` (the spitter's hit) | | a baby's answer x1 (or `none`: the worm is silent by choice, its babies could be too), a wet slap of its own x1 (optional) |
| Giant Snail | `answer` | `mount` and `dismount` (the fishing blop) | | a baby's answer x1 (or `none`) |
| All | | | `refused` (the game's `donkey_default_nope` fits by id, no file), `setoff` / `stop` (nothing needed); `jump`, `jumpland`, `perch` only when jumping ships (a thud per mount, or one shared) | |

About twenty files, five of them for the first dragon roar and rat squeak that a player hears most. The MISSING
entries can also be closed with `none` where silence is the character (the snail's and worm's babies); SkinsScan then
stops listing them. The dragon's flight clips the user supplied are kept compressed since the switching work; hearing
them in game is still open (section 1.C).

**The user's files of 2026-10-25** (`Downloads\MountSounds`, 17 files; surveyed and trimmed for listening in
`C:\ClaudeCodeTemp\GK2-mount-sounds\`, its OVERVIEW.md; nothing in the mod yet). Ten are the dragon's sounds already
shipped (take-off, landing, flaps, glide). New: three dragon growls (the roar, `voice`; one can be getting on, and
pitched up the babies' answer), a landing thud (`touchdown`), a sixth wing beat; a 34 s rat recording with about a
dozen distinct squeaks (its middle repeats), cut into eleven squeaks and a scrabble (voice, getting on and off, the
babies' answer); a 40 s snail slime texture, cut into six squelches and a slither (getting on and off, the babies'
answer). Still without a file: Gerry's getting on and off and babies, the worm's babies, the dragon's wind loop.

**Filled the same day** (the user: "can we fill some missing slots"): the dragon's voice, getting on and off,
touchdown and babies' answer; the rat's voice, getting on and off and babies' answer; the snail's getting on and off
and babies' answer. SkinsScan lists nothing missing for those three. Left: Gerry (getting on and off, the babies'
answer), the worm's babies' answer, the dragon's wind loop (`fly`), the sixth wing beat (not used: shorter and
softer than the five), and the placeholders of steps and grazing.

**Loudness, matched to the game** (2026-10-25; `C:\ClaudeCodeTemp\GK2-mount-sounds\loudness.py` and its
`loudness.txt`): each sound's EBU R128 loudness times the game sound's own volume (the game's clips from its
`sound_ids.csv`; ours register at 1), the cfg line's and a baby's 0.46. Before: the dragon's roar 9.7 dB and the
rat's squeaks 12.4 dB over the donkey's bray. Now every `.volume` line puts a cry at the bray, getting on and off at
the donkey's lip smack, an answer 6 dB under the bray. Gerry and the donkey are the game's own sounds and keep the
game's level (Gerry's lines measure 7 dB under the bray, in the speech group).

**Why the babies' answers were not heard** (the trace of 2026-10-25: four roars, 20 answers, all played): the
answers start 0.5 to 2.1 s after the call (`ScheduleAnswers`), and the dragon's roars last 1.9 to 4.2 s, so they
play under the parent's roar; they were also 10 dB under it and pitched up 2.3 times. Built the same day: the answers
start 0.15 s after the parent's call has ended (the game's play handle says when), one after another; Gerry's voice
levelled with the bray (2.25).

## 5. Built today: the dragon pulls no cart

- `saddleup.dragon/mount.cfg`: `cart = false`, no `cargo` line, the description says so. The API's cart rule does the
  rest (docs/mounts-design.md section 3): G and the panel's Drive the cart say "A dragon can't pull the cart.", a
  spawn with a cart wearing it is refused, a cart hitched when the mount becomes the dragon is left standing where it
  is. The panel's card says "Can't pull the cart" and "Cargo: no cart".
- Its skin's `[Tuning.Cart] HitchGapPx = 24` is gone (dead on a mount with no cart), and the generator
  (`make_creatures.py`) writes the dragon's `mount.cfg` with `cart = false` and no gap, so a `--cfg` rerun keeps it so.
- Five self-tests hitched the dragon; they use the rat (its `HitchGapPx` 9) or check the refusal: the side-file
  round trip, the Mounts panel's card ("Can't pull the cart"), the cart readout (the five mounts read out: four from their packs since 2026-10-26; the dragon's
  spawn with a cart must be refused), the preview gap (the rat to the worm and back; its gap 0.18 world units, the
  threshold 0.1), the walk survey (the cart pass skipped on a mount that pulls none).
- `canonical_cfg.py --check` and `tests/SkinsScan` green (29); the dev build deployed with the game closed. To see:
  the next F11 run, and in game the dragon picked in the panel with a cart hitched (the cart stays behind), G on the
  dragon (the bubble).
