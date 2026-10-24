# Mount sounds: a sound system of our own (design, 2026-10-24)

The user's brief: *build a custom sound system to plug in our own sounds at certain events; first define every event
where a mount emits audio, ground mounts first, then flying; study the donkey's sound system and have one of our own
that eventually replaces it and works for all skins; it must be skin-specific, since a mount's skins can have
different audio; solid and scalable.*

Status: **the probe passed in game 2026-10-24** (section 5: `SoundBank.cs`, the self-test "the probe" in group Sounds
15/0, the dev Sounds tab's button; the results under section 5); **phase 1 step 2 built 2026-10-24** (the catalogue
`SoundEvents.cs`, a `Sounds.Rig` per instance in `Sounds.cs` replacing `MountSounds.cs`, the base layer from the
player's keys, standing instances and babies under `StandingSounds`, the view map for the lips event, the self-test
"the donkey unchanged"; F11 29/3 then the test's waits fixed); **step 3 built 2026-10-24** (packs and layers:
`SoundBlock.cs` parses `[Sounds]` / `[Mount.Sounds]` lines and the convention names, `LooksSounds.cs` keeps the
parsed blocks, `SoundBank.ForSkin` loads a pack's files as one game sound per line, the rig resolves skin over mount
over the base, `SaveCfg` writes the sections, `SkinsScan` and `canonical_cfg.py` know them, the self-test "a pack's
own sounds"; F11: 49/2, the two the rig's own Resolve not binding, fixed); **step 4 built 2026-10-24** (the API
`Mounts.Sounds` / `MountSound` / `MountSoundEvent`, the defs' `Sounds` dictionaries, the Mounts tab's sound rows, the
self-test "the API"); **phase 1 complete: F11 group Sounds 67/0 on 2026-10-24, the author's page is
[mount-sounds.md](mount-sounds.md)**; **phase 2 built 2026-10-24** (the flying and jumping events: `takeoff`, `flap`,
`glide`, `fly`, `land`, `touchdown`, `landed`, `refused_takeoff`, `jump`, `jumpland`, `perch`; the self-test "the flying
events"), with the embedded-pack route proved by the API's own test pack (an `.ogg` through the cache file, a `.wav` in
place, a loop, a reload after a file changed); phase 3 open; the build plan is in the session's plan file
(the probe, then phase 1 in four steps). Corrections from the code read for the plan are marked *(plan)* below. The
facts below are from the game's code (LazyBearTechnology and
Assembly-CSharp decompiles of game 1.010, `lbt_1010` and `decomp_1010`), the mod's sources, and the earlier reports
[game-sounds.md](game-sounds.md), [foal-sounds.md](foal-sounds.md), [external-assets.md](external-assets.md) (sound
from outside the game, proven in the probe) and PROBE-FINDINGS Round 12. One thing is a hypothesis and is named as
the first probe (section 5).

**What this amends.** [mounts-design.md](mounts-design.md) §1 says a mount's sounds are the creature's, "never the
skin's", and [mounts-parity-matrix.md](mounts-parity-matrix.md) 2.6 closed the subject with "phase 2 (a sounds API)
not planned". Both change: a mount declares its sound set (the creature's voice, shared by its skins) **and a skin
may override any event of it** (a red dragon and a blue dragon may roar differently). The order is the one tuning
already has (section 2). The snail's, the worm's and Gerry's `[Mount.Tuning]` sound pins keep working as mount-level
data.

## 1. What exists

### 1.1 The game's player (`LazyAudio`, LazyBearTechnology)

- Everything is a **string id**. `LazyAudio.PlayAtGameObject(id, transform, SpatialType, checkDelay)` returns a
  `SoundHandler`: `SetVolume`, `SetPitch` (both multiply the sample's own values), `SetTime`, `SetPanning`,
  `SetMixerGroup`, `SetFadeInFadeOut`, `Stop`, `Pause`, `IsActive`, `OnSoundPlayed` (fires when the pooled player is
  reset). `checkDelay: true` refuses the same id within `minDelayTimeBetweenSounds` (15 s); the mod passes `false`.
- **The pool:** 15 `SoundController`s made up front (one `AudioSource` each, children of the LazyAudio object), more
  on demand (`dynamic`). Each frame `Update` moves every active one to its target (`sound3D`: the target's position)
  and applies the fades; a finished one is `Reset` (target, handler and id cleared) and pushed back.
  `SoundController.Reset` does not clear the fade times, which is why the mod's `Play` sets zero fades every time.
- **`AudioConfig`** (a `LazySingletonSO` asset, 310 ids) holds `public List<Sound> sounds`, `audioMixer`,
  `voiceOverGroup`, `mumbleGroup`, `defaultClip`. `AudioConfig.Get(id)` is a linear `sounds.Find`.
- **`Sound`** = `id`, `audioSettings3DType`, `volume` (0..1), `panning`, `loop`, `group` (an `AudioMixerGroup`),
  `samples: List<Sample>`, `randomWithoutRepeat` (true: no sample twice in a row when there are more than two).
  **`Sample`** = `isEnabled`, `clip` (an `AudioClip`), `volume`, `pitch` (-3..3), `panning`, `pitchVariation`,
  `filename`, `weight`. `Sample.Pitch` rolls the variation at every play.
- **The play call** (`LazyAudio.PlaySound`): `audioConfig.Get(id)`; an unknown id plays `defaultClip` with a warning
  (the mod refuses unknown ids before this); the 3D preset for `sound.audioSettings3DType` is looked up in a
  dictionary keyed by that type (`Default` 0, `Default3D` 1, `SoundZone3D` 2, `Fight3D3D` 3, `Workbench3D` 4; an
  `Enumeration` whose equality and hash are **by value**, so an entry made in code finds the preset); the preset's
  rolloff, min and max distance, doppler, spread, spatial blend, reverb and curves are copied onto the pooled
  AudioSource, then the sample's clip, volume x the sound's, pitch, the sound's loop flag and mixer group.
- **The mixer** (`AudioConfig.audioMixer`, 14 groups): `Master`; `sfx_root`, `sfx_ui`, `sfx_cinematic_1..3`,
  `sfx_inworld`, `sfx`, `sfx_weather`; `music_root`, `music`; `speech_root`, `speech`, `speech_mumble`. A sound in a
  sfx group follows the game's Sounds slider (confirmed by ear 2026-10-17); `voice_donkey` sits in the speech group
  (the mod's `BrayInSfxChannel` moves it to `donkey_bell`'s group).
- **Characters' footsteps:** a walk clip's animation event ends in `AnimationComponentBase.OnAnimationStepCompleted`
  (public virtual): a ray 1 unit down (mask 2060) for the `IOrderedSurface` with the lowest depth, then
  `SurfaceStepSoundSettings.GetStepSoundIdForSurfaceType` (`step_grass`, `step_dirt`, `step_wood`, `step_stone`),
  played 3D at the character, plus an optional `AdditionalStepSoundId`. `AnimationComponentBase.PlaySound(string)`
  is the generic animation-event sound: `PlayAtGameObject(sound, transform, sound3D)`.
- **Outside clips** (external-assets.md, probe 11): a `.wav` or `.ogg` file loads with
  `UnityWebRequestMultimedia.GetAudioClip(file URI, AudioType)` and plays; through a sfx mixer group it follows the
  Sounds slider. Proven with an `AudioSource` of the probe's own, not yet through `LazyAudio`.
- **What can't be done** (foal-sounds.md): the game's own donkey clips are `Streaming`, so `AudioClip.GetData` fails
  on them: no reshaped copies, only `SetPitch` and live filters. Shipping modified copies of the game's clips is the
  user's licensing call; nothing here builds towards it. The game's clips are referenced by id, never copied.

### 1.2 The game's donkey

Silent except for three things: the `lips_*` clips fire `PlaySound("donkey_lips")` at t = 0; the delivery flow
script rings `donkey_bell` twice; `voice_donkey` (`v_donkey_1..4`) is its dialogue voice. `walk_*` and `graze_*`
have no events; `donkey_eating_grass` (2.89 s, exactly the graze clip's length) exists and is never played. The
cart has no sound at all. Every other donkey sound a player hears today is the mod's.

### 1.3 The mod today (`MountSounds.cs`, 595 lines)

- **One static set of timers for the ridden mount only** (`Mount.Tick` -> `MountSounds.Tick(donkey, walking)`).
  Standing donkeys, foals and herds make no mod sound of their own (the hitch, unhitch and cargo thump moments
  already play for a standing instance and the standing cart loaded on foot *(plan)*); their lips animation plays `donkey_lips` through the game at
  the game's volume (the `PlaySound` prefix reroutes only the ridden one, matched by its `AnimationComponentBase`).
- **Timing from the animator** (layer 0: the clip's name and `normalizedTime`): footfall k = floor(t x
  `HoofStepsPerCycle` + `HoofPhase`), a step on every change of k in either direction (the walk runs backwards in a
  focus stance); the wheel the same with `WheelEveryCycles`; `graze` is the graze clip starting (stopped if the clip
  ends early); `lips` is the game's animation event rerouted; brays are a chance at mounting and an idle timer
  (`BrayIdleEverySeconds` x 0.5..1.5, not mid-stride unless `BrayWhileWalking`); the bell rings on setting off
  after standing `BellSetOffIdleSeconds` (optionally only with cargo), on the bell key and on `Mounts.RingBell()`;
  the cargo thump on an item landing on the bound cart; mount, dismount, hitch, unhitch from `Mount` /
  `MountInstance` (`MountSounds.Moment`).
- **Playback:** `Play(id, at, volume, pitch, startAt)`: `id#n` plays only clip n (the other samples disabled for the
  call), `PlayAtGameObject(id, at, sound3D, checkDelay: false)`, `SetVolume(v x SoundVolume)`, `SetPitch`
  (clamped 0.1..3), `SetTime(startAt)`, zero fades; an unknown id is logged once and refused.
- **Settings:** about 60 keys per style in `StyleSettings` (`[Ride]` / `[Cart]`, docs/reference.md "Sounds"): per
  sound an on switch, a game id, volume, pitch, and the timing numbers; `SoundsOn` and `SoundVolume` over all. The
  Mounts feature rules `Sounds` and `Bell` gate everything; `CargoSheet.Running` silences the moments. Saddle Up
  shows three rows (Bell, Brays, Volume). The dev window's Sounds tab edits every key with a play button each.
- **A mount pins sound keys** in `[Mount.Tuning]` (the snail and the worm: `BrayVolume = 0`, `HoofVolume = 0`,
  `LipsVolume = 0`): they become instance overrides owned by `Looks.TuningOwner`, read by `Tuned<T>.Value` while the
  instance is bound. No skin can touch them (`Looks.SkinTuningKeys` is the picture keys only).
- **Two Harmony patches** (both stay): `OnAnimationStepCompleted` prefix skips the keeper's own step events while
  mounted; `PlaySound(string)` prefix reroutes the ridden donkey's `donkey_lips` through `LipsVolume`.

### 1.4 The layering machinery (reused as is)

`Looks.LayeredValue(i, style, key)` answers a tuning key for an instance in this order: another owner's override on
the instance -> the skin's `[Tuning.Ride]` / `[Tuning.Cart]` -> the skin's `[Tuning]` -> the mount's per-style tuning ->
the mount's both-styles tuning -> the player's cfg -> the mod defaults. `Rig.ApplyTuning` applies the merged set as
instance overrides and re-merges only when the skin, the mount or the style changes (`RetuneNow`). Packs are read by
`Looks.FromCfg` (skin.cfg sections `[Tuning*]`, `[Mount*]`, the pictures by name through a `Bytes(file)` reader that
is the same for a folder and for an embedded pack); Saddle Up's built-in skins are embedded by
`assets\skins\**\*` (every file under a skin folder, so a `sounds\` subfolder embeds like `front\` does).

## 2. The rule: who owns a sound

**A sound is the mount's unless its skin says otherwise.** Per instance, per event, per style, the first that has
one wins:

1. another mod's override on this instance (`handle.Sounds.Set(owner, event, ...)`, the API);
2. the skin's `[Sounds.Ride]` / `[Sounds.Cart]` (one style);
3. the skin's `[Sounds]` (both styles);
4. the mount's `[Mount.Sounds.Ride]` / `[Mount.Sounds.Cart]`;
5. the mount's `[Mount.Sounds]`;
6. **the base's defaults as the player's cfg has them**: today's `[Ride]` / `[Cart]` sound keys (`BraySoundId`,
   `HoofVolume`, ...), with a mount's `[Mount.Tuning]` pins on them as today;
7. the mod defaults (what a default player's donkey plays).

The player's switches and volumes (`SoundsOn`, `SoundVolume`, the per-event `...On` switches, Saddle Up's Bell,
Brays and Volume rows, the game's Sounds slider) always apply **on top** of whatever layer won: a skin can give the
rat a squeak, a player can still turn brays off. A skin's or mount's own volume and pitch multiply the player's,
they never replace them. (This is the same shape as the locked, orange rows of today: the creature's value shows,
the player's row scales it.)

Why a skin may override: the user's requirement, and the one case that needs it (two drawings of one creature with
different voices). Why the mount still declares first: a creature's voice is shared by its skins by default, so a
second dragon pack with no `[Sounds]` sounds like the dragon, not like a donkey.

## 3. The events

A fixed set of event ids, one table row each. Ids are stable names other mods and packs will use; adding one is a
row in the catalogue (`SoundEvents`, section 7), nowhere else. Surface variants are sub-keys of one event.

### 3.1 Ground mounts

| Event | Trigger (where in the code) | Who emits | Kind | The donkey today |
|---|---|---|---|---|
| `step` (`step.grass`, `step.dirt`, `step.wood`, `step.stone`) | each footfall of the walk clip (floor(t x per_cycle + phase) changes, either direction) | every instance whose walk clip runs: ridden, the idle shuffle's steps, foals following | one-shot, surface-aware | the game's step id for the ground under it, 1.3 x, pitch 0.8 +/- 0.06, 4 per cycle |
| `wheel` | once per `every_cycles` walk cycles with the cart | the instance pulling a cart | one-shot | `conveyor_furnace_wheel` 0.6 x; pitch optionally follows `walk_speed` |
| `setoff` | walking starts after standing (today's `setOff`) | every instance | one-shot | none (the bell's set-off rule is a *behaviour* that emits `bell`) |
| `stop` | walking ends | every instance | one-shot | none |
| `graze` | the graze clip starts (the chew frames are inside it: one clip, 2.89 s) | every instance that grazes | stoppable: cut when the clip ends early | `donkey_eating_grass` |
| `lips` | the game's animation event `PlaySound("donkey_lips")` on the lips clip (the prefix, per instance) | every instance | one-shot | `donkey_lips` x `LipsVolume` |
| `ear` | the ear clip starts | every instance | one-shot | none |
| `turn` | the turning-head clip starts | every instance | one-shot | none |
| `voice` | the creature's call: the idle timer (`voice.idle_every`), a chance at mounting (`voice.mount_chance`, 0.3..0.8 s after), `Mounts.Bray()`, a foal answering (phase 3) | every instance (standing ones under the herd rule, section 7) | one-shot | `voice_donkey`, in the sfx group when `BrayInSfxChannel` |
| `refused` | a ride or a hitch refused at the mount (`MountResult` refused with the mount near) | the mount refused | one-shot | none (the game's `donkey_default_nope` is the obvious pick: game-sounds.md) |
| `mount` | the keeper gets on (`Mount.Mount` / `MountInstance.Ride`) | the ridden one | one-shot | Ride `donkey_lips#1` from 0.42 s, Cart `ladder_climb_start` |
| `dismount` | he gets off (not at doors, scene changes, failed re-summons, style changes) | the ridden one | one-shot | Ride `donkey_lips#2` from 0.42 s, Cart `ladder_climb_finish` |
| `hitch` / `unhitch` | a cart hitched / left standing | the instance | one-shot | `equip_tool` / `unequip_tool` |
| `cargo` | an item lands on the bound cart (not the reload after a save) | the cart's instance | one-shot | `oh_wood_container_drop` |
| `bell` | the set-off rule, the bell key, `Mounts.RingBell()` | the ridden one | one-shot | `donkey_bell` 0.7 x |
| `sent` / `called` | sent away / called over (Saddle Up's keys, `MountCompanion`) | the instance | one-shot | none (`donkey_default_im_going_home`, `okay` are candidates) |

Not events: the keeper's own sounds (his steps are skipped while mounted and stay so), the game's pickup and drop
sounds (they play as they do), the cart chest's open sound (the game's), anything in a fight (the mount does not
act; a later `hit` event is a row away).

### 3.2 Flying mounts (phase 2: **built 2026-10-24**, the hooks in `Flight.cs`; the self-test "the flying events")

| Event | Trigger | Kind | Notes |
|---|---|---|---|
| `takeoff` | `Set(Phase.Rising)` (the crouch begins; also a jump turning into a take-off) | one-shot | |
| `flap` | each wing beat: the launch's `nextBeat` kicks (Flight.cs `Launch`) and, airborne, a change of `floor(beatT / WingBeatS)` (the clock never wraps: `Land` resets it) *(plan)* | one-shot | with the pitched beats (climb/dive) a pitch offset per set |
| `glide` | the glide stage starts (`glide != None`) | stoppable: a loop while gliding, cut at the way out | a wind or wings-open sound |
| `fly` | airborne (`Set(Phase.Airborne)`) | stoppable: a loop through the flight | optional ambience (wind); off by default |
| `land` | `Set(Phase.Landing)` | one-shot | the flare has no own event: it is the dive's end |
| `touchdown` | the thud (`thudT` -1 -> 0) | one-shot | the heavy landing |
| `landed` | `Land("landed")` (the fold) | one-shot | default none |
| `refused_takeoff` | `CanFly` false at the key (`Why = "can't take off: ..."`) | one-shot | default none (`refused` could serve) |
| `jump` | the body leaves the ground (`jumpTook = true` in `JumpTick`; `Set(Phase.Jumping)` is the crouch's start) *(plan)* | one-shot | ground mounts that jump too |
| `jumpland` | the jump's touch-down judged "lands" (the squash begins) | one-shot | `step` of the surface by default |
| `perch` | the jump lands on top of something (`JumpStage.Perch`) | one-shot | default `jumpland` |

### 3.3 Standing mounts, babies, herds (phase 1 for standing ones; phase 3 for the calls)

Every instance gets its own emitter (section 7), so a standing donkey grazes audibly and a foal's steps are heard.
Rules so a yard is not a racket: only instances in the Active tier (Tiers.cs) emit; `voice` has **one shared
cooldown per scene** (one voice at a time, a few seconds apart) and standing ones call less often than the ridden
one (`voice.idle_every` x 3); a baby's pitch and volume come from `ViewScale` (foal-sounds.md option 1: pitch about
1 / scale^0.7, a fixed small offset per foal so siblings differ) unless its skin's `[Sounds]` gives the babies their
own (`[Sounds.Baby]`, phase 3). Foals answering the parent (`call` / `answer`) is phase 3 from foal-sounds.md.

## 4. Where a sound comes from: one syntax for three sources

Per event, in a `[Sounds]` block (a skin's) or `[Mount.Sounds]` (a mount's), with `.Ride` / `.Cart` variants for one
style; the same keys in code (`MountSkinDef.Sounds`, `MountDef.Sounds`: event -> `MountSound`).

```
[Sounds]
voice = squeak_1.ogg, squeak_2.ogg, squeak_3.ogg   # files in the pack's sounds\ folder: one at random, never twice in a row
voice.volume = 0.8                                 # x the player's (1 = as the file is)
voice.pitch = 1.1
voice.pitch_variation = 0.05                       # +/- per play
voice.idle_every = 25                              # the event's own numbers, where it has them (seconds; 0 = never)
voice.mount_chance = 0.5
voice.channel = sfx                                # sfx (the default for own files) or speech
step = step_dirt                                   # a game id: as today (id#n = only clip n)
step.grass = step_grass                            # a surface variant; unset variants fall back to step
step.per_cycle = 6
step.pitch = 1.4
lips = none                                        # silent, whatever the layer below says
graze = chew.ogg
graze.start = 0.2                                  # seconds into the clip
graze.loop = true                                  # a stoppable event: loop the clip until it ends
```

- **A value is a game id, a list of files, or `none`.** A name ending in `.ogg` or `.wav` is a file of the pack's
  `sounds\` folder; anything else is a game id (checked at registration against `AudioConfig`, logged once if
  unknown, as today). A missing file is logged once and the layer below applies.
- **Convention without a line:** files named `<event>_<n>.ogg` (`voice_1.ogg`, `step_grass_1.ogg`) in `sounds\` are
  that event's with no cfg line, so an artist can drop files in; a cfg line replaces the convention for its event.
- **Unknown events and keys** are named in the log and ignored (a newer pack in an older mod), as every pack section
  does today.
- **The numbers that are the event's** (`per_cycle`, `phase`, `every_cycles`, `idle_every`, `mount_chance`,
  `while_walking`, `set_off_idle`, `only_with_cargo`) live in the same block as `event.key`: they are creature facts
  (a snail has no footfalls, a rat has six). They map onto today's cfg keys for the base layer.
- **Formats:** `.ogg` (Vorbis, the game's own format) and `.wav` (PCM). Mono, 44.1 or 48 kHz; a few seconds each.
  Loaded when a skin is first worn or a mount's instance first spawns, like the pictures; freed with the skin.
- **Licensing:** a pack's files are the pack author's; the built-in skins ship only what we made or licensed (the
  game's own clips are used by id, never shipped). The game's `donkey_default_*` lines (`hey`, `nope`, `okay`,
  `im_going_home`, ...) are game ids and free to use that way.

## 5. Playback: one path, the game's (the probe)

**The design's key decision, and the one hypothesis:** our own clips become **game sounds**. At registration the
mod adds a `Sound` to `AudioConfig.sounds` with a namespaced id (`gk2.<owner>.<skin>.<event>[.<variant>]`), the
loaded `AudioClip`s as its samples (`randomWithoutRepeat` on, the file's volume 1, pitch 1, the variation from the
cfg), `audioSettings3DType = Default3D` (what `donkey_lips` and `donkey_eating_grass` use), `group = sfx_inworld`
(or the speech group for `channel = speech`), `loop` from the cfg. From then on **everything is as it is today**:
`PlayAtGameObject(id, at, sound3D, checkDelay: false)`, the pool, the 3D preset copied onto the player, the mixer
volume, pausing with the game, `SetVolume` / `SetPitch` / `SetTime` / zero fades, the `#n` clip pick, the one
`Play` helper. A skin's event that resolves to a game id needs no registration at all.

Why this and not an `AudioSource` of our own per instance (the probe's path): the pool, the presets (distance
rolloff that matches every other sound in the world), the pause, the mixer and `SoundHandler` all come for free,
and the emitter has one code path for a game id and for a file. The cost is a dependency on a public list and a
public class of LazyBearTechnology, which `DepManifest verify` watches like the rest.

**The probe (the first thing built, in the "sounds" self-test and by ear):**
1. a generated 0.5 s WAV registered as `gk2.test.sounds.voice` -> `AudioConfig.Get` finds it, `PlayAtGameObject`
   returns a live handler whose source has our clip, the `Default3D` preset's spatial blend (1 expected) and
   distances, and the `sfx_inworld` group; the preset's values are logged once (they are on the LazyAudio object's
   children, not in the decompile);
2. it is heard 3D at the mount (quieter far away), follows the game's Sounds slider, and pauses with the pause menu;
3. `defaultClip` never plays (no "Audio System Warning" in the log), `RuntimeAudioEditor` and `CreateConfigData`
   are not disturbed (they read `sounds` by iteration; our entries have clips and ids like any other);
4. unregistering removes the entry and destroys the clips; a second registration of the same id replaces it.

**Fallback if the probe fails:** one `AudioSource` per emitting instance on the `sfx_inworld` group with the
preset's values copied by hand (the probe's path), losing the pool and the game's pause; the emitter's interface is
the same either way, so nothing above it changes.

A re-bind of the config (`LazyAudio.BindAudioConfig`) could replace the list under us: the bank keeps the `Sound`
objects it made and re-adds any that `Get` no longer finds, once per play attempt (one `Find` per play is what the
game does for every sound; the resolved `Sound` is cached per emitter, never looked up per frame).

### Probe results (2026-10-24, F11 group Sounds: 15 passed, 0 failed)

The hypothesis holds. A 0.5 s tone parsed from WAV bytes by the mod and the same bytes decoded by Unity from a
file (`UnityWebRequestMultimedia`) were registered as `gk2.selftest.probe`: the game's list went from 311 to 312
sounds and `AudioConfig.Get` found it; LazyAudio's own config reference is that same object. Played through
`PlayAtGameObject`: a live handler whose pooled player held our clip, in the game's `sfx` mixer group (the group of
`donkey_lips`), with the Default3D preset copied onto it (spatial blend 1, distance 1..14, a custom rolloff), sitting
at the target; it ended by itself; unregistered, the list was 311 again and the clips were destroyed. No "Audio
System Warning" in the log (the placeholder clip never played).

The game's five 3D presets (`AudioSettings3DPreset` components under the LazyAudio object), for the record:

| Type | Spatial blend | Distance | Rolloff | Used by |
|---|---|---|---|---|
| 0 Default | 0 (2D) | 1..500 | Logarithmic | `voice_donkey` (the dialogue voice): as loud far away as near |
| 1 Default3D | 1 | 1..14 | Custom | `donkey_lips`, `donkey_eating_grass`; the bank's entries |
| 2 SoundZone3D | 0.05 | 1..20 | Custom | ambient zones |
| 3 Fight3D3D | 1 | 1..20 | Custom | fights |
| 4 Workbench3D | 1 | 1..12 | Custom | stations |

One trap met on the way: `Enumeration` (the base of `AudioSettings3DType`) overloads `==` and `!=` to answer false
whenever either side is null, so a null test on a preset's `type` must cast to `object` first.

The by-ear part (the dev Sounds tab's "Play the probe tone at the mount": quieter walking away, the game's Sounds
slider, the pause menu) is the user's; its outcome goes here when heard.

## 6. Loading

- **A pack folder on disk:** `UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS
  | WAV)` (proven in the probe), awaited on a coroutine of `Plugin.Instance` (the pictures load by a per-frame
  budget, `Looks.AdvanceBuilds`, not a coroutine *(plan)*); the event is silent until its clips are in (logged once per skin: "sounds: n file(s) loaded").
- **An embedded pack** (Saddle Up's built-in skins, a mod's DLL): bytes, not files. `.wav` PCM (8/16-bit, mono or
  stereo) is parsed by the mod into `AudioClip.Create` + `SetData` (synchronous, no file); `.ogg` bytes are written
  once to `BepInEx\cache\GK2DonkeyAPI\sounds\<owner>\<skin>\<file>` (skipped when the size and a hash match) and
  loaded through the file path. Built-in packs should ship `.ogg` (small) or short `.wav` (no cache file); the docs
  say which.
- **Code-registered skins and mounts** may give `AudioClip`s they loaded themselves (`MountSound.Clips`), or file
  paths, or game ids.
- Clips are shared by every instance wearing the skin, freed when the skin is unregistered or the mod unloads
  (`Object.Destroy(clip)`), and never while a player of them is live (`OnSoundPlayed` counts them down).
- Memory: a 1 s mono Vorbis decoded is about 170 KB; a skin with ten short sounds is under 2 MB.

## 7. The runtime

**`SoundEvents` (the catalogue, one static table):** for every event id its description, kind (one-shot /
stoppable), the surface variants, the event's own numbers and their defaults, which base cfg keys feed the base
layer (section 8), and whether every instance emits it or only the ridden one. The dev window, the docs table, the
pack parser and the API all read this table; a new event is one row.

**`Sounds.Rig` per instance** (the `CargoRig` / `LooksRig` / `EffectsRig` pattern, created in the `Instance`
constructor): the resolved set (event -> the winning `MountSound` and which layer gave it; one `Sound` reference per
event, so a play is one dictionary lookup and no `Find`), re-resolved when the skin, the mount or the style changes
(the same trigger as `RetuneNow` / `ApplyTuning`) or another mod sets an override; the timers and clip-tracking
state that are static in `MountSounds` today (`inWalkClip`, `lastHoofK`, `lastWheelK`, `inGrazeClip`,
`grazeHandler`, `nextBray`, `mountBrayAt`, `wasWalking`, `idleSince`) become fields; `Emit(event, variant)`,
`Loop(event, on)`, `StopAll()`; `LastEvent` per rig for the dev window. The ridden instance ticks it from
`Mount.Tick` where `MountSounds.Tick` is today; a standing instance from `StandTick` while Active (the idle
shuffle's walk clip and the foals' following then sound their steps by themselves, since the rig reads the
animator).

**Gates, in one place** (`Rig.May(event)`): `SoundsOn`, `MountRules.Allowed(Sounds)` (and `Bell` for `bell`),
`CargoSheet.Running`, the player's per-event switch, the resolved source not `none`, the instance's tier, and the
herd rule for `voice` (a scene-wide `nextVoiceAt`). `Mounts.Bray()` and `RingBell()` keep their names and emit
`voice` and `bell` on the ridden rig.

**The two patches, widened:** the `PlaySound` prefix maps the `AnimationComponentBase` to its instance (a lookup
the registry keeps: view -> instance) for every instance, so a skin's `lips` replacement works standing too, and
the game's own line is skipped only when the instance's rig takes the event; the keeper's step skip is unchanged.
No new patch: every trigger is a call from code the mod already runs.

**Performance:** one dictionary lookup per emit; the resolve only on a change; standing rigs tick only in the Active
tier; a `Perf.S.Sounds` scope measures the lot.

## 8. The player's layer and compatibility

The `[Ride]` / `[Cart]` sound keys are public since 0.9.1 (docs/compatibility.md) and **stay bound with their
meanings**: they are the base layer's definition of the donkey's sounds and the player's switches and scales. Mapped
onto the catalogue: `HoofOn` / `HoofSoundId` / `HoofSurfaceAware` / `HoofVolume` / `HoofPitch` /
`HoofPitchVariation` / `HoofStepsPerCycle` / `HoofPhase` -> `step` and its numbers; `GrazeOn` / `GrazeSoundId` /
`GrazeVolume` -> `graze`; `LipsVolume` -> `lips`; `BraySoundId` / `BrayOnMountChance` / `BrayIdleEverySeconds` /
`BrayWhileWalking` / `BrayVolume` / `BrayPitch` / `BrayInSfxChannel` -> `voice`; `Wheel*` -> `wheel`; `CargoThump*` ->
`cargo`; `Mount*` / `Dismount*` / `Hitch*` / `Unhitch*` -> those; `Bell*` -> `bell` and the set-off behaviour. The
`...On` switches and `...Volume` / `...Pitch` scale whatever layer won; the `...SoundId` keys and the timing numbers are the
base's values, overridden by a mount's or skin's block. A mount's `[Mount.Tuning] BrayVolume = 0` keeps silencing
the snail (it is the base layer's value for that instance, as today); the same in the new syntax is
`[Mount.Sounds] voice = none`, and the built-in packs move to it when they get sounds of their own.

**A default player's donkey must not change.** The self-test proves it: with no skin, mount or mod setting anything,
every event resolves to today's id, volume, pitch, start and numbers (a table in the test against the `StyleSettings`
defaults), and the emitter fires the same events at the same animator times (the step index rule and the graze
start are the same code, moved). Saddle Up's three rows are unchanged. The old `MountSounds` statics go; the
`Preview` buttons become "play this event on the ridden mount".

The Mounts tab (F11) gets a Sounds group: every event of the ridden instance, the source it resolved to and the
layer that gave it (skin / mount / base / a mod), a play button each, and the rows to set the skin's or the mount's
line, written back to the skin.cfg like the tuning rows are.

## 9. API (sketch)

- `MountSkinDef.Sounds` and `MountDef.Sounds`: `Dictionary<string, MountSound>` by event id (`"voice"`,
  `"step.grass"`), each `MountSound { string GameId; string[] Files; AudioClip[] Clips; bool Silent; float Volume =
  1, Pitch = 1, PitchVariation, Start; bool Loop; MountSoundChannel Channel; Dictionary<string, float> Numbers }`,
  plus `SoundsRide` / `SoundsCart`.
- `Mounts.Sounds`: `IReadOnlyList<string> Events`; `MountResult Play(MountHandle, string eventId)`; `Stop(handle,
  eventId)`; `MountSound Resolved(handle, eventId, out string layer)`; `IDisposable Set(ownerId, handle, eventId,
  MountSound)` (the override layer, session-only); `event Action<MountHandle, string> Played` (after a play, for
  mods that react: a dragon's roar lights its fire).
- Scans: `tests/ApiSurface --update` (the new members), `DepManifest verify` (it reads the DLL's references into the
  game's assemblies by itself: `AudioConfig.sounds`, `Sound`, `Sample`, `AudioSettings3DType`, `LazyAudio`; Unity's own
  types such as `UnityWebRequestMultimedia` are outside its scope *(plan)*; `game-code.txt` names the new classes),
  `tests/SkinsScan` (`[Sounds]` joins the canonical skin.cfg layout; a built-in with a picture key or a sound key in
  the wrong block fails), `tools/page-carryover.py` nothing (no new player cfg key), FrameworkScan nothing (no new
  patch class; the widened prefix is the same class).

## 10. Tests (F11, a new group "Sounds")

1. **The probe** (section 5, four checks) with a generated WAV; the preset values logged.
2. **A temp pack** with `[Sounds]` for `voice` (two files), `step.grass` (a game id), `lips = none`, and the
   convention file `graze_1.wav` with no line: the skin registers with "4 sound(s)" in its report; worn, the rig
   resolves `voice` to the pack's `Sound` (two samples), `step.grass` to the game id, `step.dirt` to the base's,
   `lips` to silent, `graze` to the convention file.
3. **Two skins of one mount** resolve `voice` differently (the second has no `[Sounds]` and gets the mount's
   `[Mount.Sounds]`); the mount's line over the base's; a per-style line over the both-styles line; a missing file
   logs once and falls back; an unknown event is named once.
4. **Emitting:** `Play(handle, "voice")` returns a live handler on our clip at the mount's transform; `graze` loops
   and `Stop` cuts it; `step` picks the surface variant on the yard's grass and the base's on a road (the surface
   probe as today); the ridden walk fires 4 steps per cycle (the counter), the rat's pack 6.
5. **The player's layer:** `SoundsOn` off silences a skin's `voice`; `BrayVolume` 0.5 halves it (the handler's
   volume x the file's); Saddle Up's Brays row off -> no idle `voice`; the snail's `[Mount.Tuning]` pin still silent.
6. **Standing and babies:** a standing instance in the Active tier grazes audibly and one in the Idle tier not; a
   foal's `step` plays at the scaled pitch; two standing donkeys share the voice cooldown (one `voice` in 5 s).
7. **The default donkey unchanged:** the resolved table equals the `StyleSettings` defaults for every event, both
   styles; the API's `Bray()` and `RingBell()` still play `voice_donkey` and `donkey_bell`.
8. **Clean-up:** unregistering the pack removes its `Sound` entries from `AudioConfig.sounds` and destroys the
   clips; `DonkeyPrefab`'s audit sees no leftover; the game's own sound count is what it was (310).
9. **The watchdog of the rest:** every other group's sound-related checks (the snail's pin in the Looks group, the
   settings-page rows) unchanged.

## 11. Phases

1. **Ground, the base and packs** (API 0.37.0): the catalogue (3.1), the rig per instance, the bank (game ids,
   pack files, embedded WAV, cached OGG), `[Sounds]` / `[Mount.Sounds]` with styles, the two patches widened,
   standing instances' steps / graze / lips, the player's layer mapped, the dev tab, the API, the tests, the docs
   (docs/mount-sounds.md for authors, reference.md, api.md, skins.md, making-a-mount.md, the amendments to
   mounts-design.md and the parity matrix). Not in it: any new audio file (the built-in skins keep the donkey's
   sounds until the user picks or makes theirs), `voice` for standing ones (only the ridden one calls), `refused`,
   `sent`, `called`, flying.
2. **Flying** (3.2) and `refused` / `sent` / `called`; the dragon's pack gets its first own sounds (what they are
   is the user's: made, licensed or the game's by id).
3. **Babies and herds** (3.3, foal-sounds.md): standing `voice` under the herd rule, `[Sounds.Baby]`, calls and
   answers, the pitch from `ViewScale`.

## 12. Decisions for the user

1. **Skin over mount** (section 2): a skin's `[Sounds]` wins over its mount's `[Mount.Sounds]`, the player's
   switches and volumes scale the result. (Recommended; the alternative, sounds on the mount only, does not meet
   the brief.)
2. **Event names:** `voice` for the creature's call (the cfg keys and Saddle Up's row keep saying Bray for the
   donkey); `step`, not `hoof`, since not every mount has hooves. Say if you want the donkey's words kept.
3. **Build order:** the probe first (one afternoon: the bank and the four checks, no events moved yet), then phase 1
   on its result; or the whole of phase 1 in one go with the fallback ready.
4. **`.ogg` from embedded packs through a cache file** (section 6) is acceptable, or built-in packs ship `.wav` only.

## 13. Constraints to keep in mind

- The game's clips are Streaming: a skin's "deeper bray" is `SetPitch` on the game's clip or a file of its own,
  never a reshaped copy (foal-sounds.md).
- `AudioConfig.Get` is a linear `Find` over 310 + ours per play: fine at these rates; never per frame.
- `voice_donkey`'s preset is `Default` (type 0, probably 2D as a dialogue voice) while the others are `Default3D`:
  a file-backed `voice` is registered as `Default3D`, so a skin's own voice is positional where the donkey's bray
  is not. The probe logs both presets' values.
- The pooled players keep the fade times of their last use (`SetFadeInFadeOut(0, 0)` every play stays).
- Nothing here is saved: sounds are resolved from the skin and the mount an instance already has.
