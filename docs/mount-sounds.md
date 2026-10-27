# Mount sounds: giving a skin or a mount its own sounds

Every mount GK2 Donkey API draws plays its sounds through one system (built 2026-10-24; the design and the facts
behind it are in [mount-sounds-design.md](mount-sounds-design.md)): a fixed catalogue of **events** (a footfall, the
creature's call, grazing, getting on and off, the cart's sounds), and for each event **layers** that say what plays.
A skin pack can bring its own `.ogg` or `.wav` files, a mount can share a voice among its skins, a mod can set a
sound from code, and the player's own settings always scale the result. This page is for pack authors and mod
authors; the player-facing keys are in [reference.md](reference.md) "Sounds".

## The events

| Event | When | Notes |
|---|---|---|
| `step` | each footfall of the walk cycle | variants `step.grass`, `step.dirt`, `step.wood`, `step.stone` (the ground under the mount); `ground` as a source means the game's step sound for that ground; numbers `per_cycle` (4), `phase`, `surface_aware` |
| `wheel` | once per walk cycle while the cart moves | numbers `every_cycles`, `phase`, `follows_speed` |
| `setoff`, `stop` | walking starts after standing / ends | silent unless a layer gives a sound |
| `graze` | the graze clip starts | stoppable: cut when the clip ends early; `loop` keeps the clip going |
| `lips` | the game's lip-smack animation event | the game's `donkey_lips` by default |
| `ear`, `turn` | the ear clip / the turning-head clip starts | silent unless a layer gives a sound |
| `voice` | the creature's call | the idle timer (`idle_every` seconds, 0 = never; `while_walking`), a chance at mounting (`mount_chance`), `Mounts.Bray()`; the donkey's `voice_donkey` |
| `answer` | a baby's reply to its parent's call: once it has ended, siblings in a new order each call with uneven gaps (now and then two at once, sometimes a pause), each a touch higher or lower | the mount's own voice pitched up by the baby's size unless `[Sounds.Baby]` gives one (the donkey's bray for the donkey; silent only when its voice is) |
| `refused` | a ride or a hitch refused at the mount | silent unless a layer gives a sound (the game's `donkey_default_nope` is a fit) |
| `mount`, `dismount` | the keeper gets on / off | not at doors, scene changes, failed re-summons, style changes |
| `hitch`, `unhitch` | a cart hitched / left standing | |
| `cargo` | an item lands on the cart | |
| `bell` | the bell | on setting off after standing (`on_set_off`, `set_off_idle`, `only_with_cargo`), the bell key, `Mounts.RingBell()` |
| `sent`, `called` | sent away / called over | silent unless a layer gives a sound |

**Flying and jumping** (the flying experiment, docs/flying.md; a mount with `flies = true`, and every mount's jump),
all silent unless a layer gives a sound:

| Event | When | Notes |
|---|---|---|
| `takeoff` | the take-off begins (the crouch; a jump turning into a take-off) | |
| `flap` | each wing beat: the launch's kicks, then every beat airborne | not while gliding |
| `glide` | the glide begins | stoppable: `loop = true` for a sound through the glide, cut at its way out |
| `fly` | airborne | stoppable: a loop through the flight, stopped on the ground (wind) |
| `land` | the landing begins (the dive) | |
| `touchdown` | the thud on touching the ground | |
| `landed` | the flight is over, on the ground | |
| `refused_takeoff` | a take-off refused (indoors, with the cart, a mount that doesn't fly) | |
| `jump` | a jump leaves the ground | ground mounts too |
| `jumpland` | a jump comes down (the squash) | |
| `perch` | a jump comes down on top of something | |

**The herd.** A grown mount standing near the keeper calls by itself, three times rarer than the ridden one
(`voice.idle_every` times 3), never mid-step, and no standing call comes within four seconds of any other voice in
the scene: one voice at a time, a yard of them is no racket. Babies never call by themselves; when their parent calls
(ridden or standing, or `Mounts.Bray()`) each answers in turn once the call has ended (0.15 s on, then a third of a
second apart; before 2026-10-25 half a second after the call began, under a long roar), with
the `answer` event. All of it is under the player's `StandingSounds` switch (Saddle Up's "Standing mounts and babies
sound" row).

## A pack's sounds

Two folders, one for each cfg with sound sections:

```
gk2mounts\saddleup-dragon.dragon\
  mount.cfg           [Sounds*]   the mount's: every skin of it
  sounds\             the mount's files (mount.cfg's [Sounds*] lines, and its name rule)
    takeoff_1.ogg
    flap_1.ogg
  skins\
    default\
      skin.cfg        [Sounds*]   the skin's own: this look only
      sounds\         the skin's own files (skin.cfg's [Sounds*] lines, and its name rule)
        roar_1.ogg
```

`.ogg` (Vorbis, the game's own format, small) or `.wav` (PCM 8/16/24/32-bit or float, mono or stereo, any rate); a
few seconds each. A `skin.cfg`'s `[Sounds]` line reads the skin folder's `sounds\` only and a `mount.cfg`'s the mount
folder's `sounds\` only: a line naming a file of the other folder is named in the log and left out, and the layer
below applies. A folder inside `sounds\` is never read. (Before the mount folders, 2026-10-24, a mount's files sat in
the declaring skin's `sounds\mount\`, and earlier still in its `sounds\` with the skin's own: there a file named after
its event was also the declaring skin's own by the name rule, the dragon's flight sounds loaded twice, and the red
dragon's copy won over the mount's.)

**By name, with no line in the cfg:** `sounds\<event>[.<variant>][_<n>].ogg|wav`. In a skin's folder,
`voice_1.ogg`, `voice_2.ogg` and `voice_3.ogg` are the skin's `voice` (one at random per call, never the same twice in
a row); `step.grass_1.wav` its step on grass; `graze.ogg` its grazing. The same rule in the mount folder's `sounds\`
gives the mount's: `gk2mounts\saddleup-dragon.dragon\sounds\takeoff_1.ogg` is every dragon skin's take-off. Each folder's rule reads
its own files only. A file named like no event is listed in the log and never played.

**Or in a `[Sounds]` block of skin.cfg**, which also takes a game sound id, silence, the ground, and the attributes:

```
[Sounds]
voice = squeak_1.ogg, squeak_2.ogg   # files of sounds\ (a line replaces the convention for its event)
voice.volume = 0.8                   # x the player's volume for this event (0 to 3)
voice.pitch = 1.1                    # x the player's pitch (0.1 to 3)
voice.pitch_variation = 0.05         # +/- per play, as a fraction of the pitch
voice.idle_every = 25                # the event's own numbers (the table above)
voice.channel = sfx                  # sfx (the in-world group, the default for own files) or speech (the dialogue voices')
step = ground                        # the game's step sound for the ground under the mount
step.grass = step_grass              # a variant: a game sound id (id#n = only clip n of that sound)
lips = none                          # silent, whatever the layer below says
graze = chew.ogg
graze.start = 0.2                    # seconds into the clip
graze.loop = true                    # a stoppable event: the clip loops until it ends

[Sounds.Cart]                        # with the cart only, over [Sounds]; [Sounds.Ride] for riding plain
voice = grunt.ogg

[Sounds.Baby]                        # the babies (drawn smaller than the grown one), over the blocks above for them
answer = squeak_1.ogg, squeak_2.ogg  # a squeak where the grown one roars; without it: the grown one's sound pitched up
step = patter.ogg
```

The sections stand after the skin's `[Tuning]` sections and before `[Colors]` (the canonical layout;
`tools/skins/canonical_cfg.py` and `tests/SkinsScan` refuse a built-in that differs). Unknown events, variants and
attributes are named in the skin's report in the log and ignored; a file a line names that is not in `sounds\` is
named and left out; a line with none of its files left falls through to the layer below.

**A mount's sounds**, shared by every skin of it, go in the `[Sounds]` (and `[Sounds.Ride]`, `[Sounds.Cart]`,
`[Sounds.Baby]`) of its `mount.cfg`, after its `[Tuning*]`: the same section names as a skin's, the file decides whose
they are. Their files are the mount folder's `sounds\`, loaded once for every skin of the mount. A skin of the mount
with no `[Sounds]` sounds like the mount; its own `[Sounds]` lines (files in its own `sounds\`) win over the mount's,
event by event. A mount's default look (its `skins\default\`) usually has no `[Sounds]` of its own: its sounds are
the mount's.

```
# gk2mounts\saddleup-dragon.dragon\mount.cfg
[Mount]
id = saddleup-dragon.dragon
...

[Sounds]
voice = roar_1.ogg, roar_2.ogg   # files of gk2mounts\saddleup-dragon.dragon\sounds\
voice.idle_every = 60
step = thud.ogg
lips = none
```

The snail's, the worm's and Gerry's older volume pins in the mount's tuning are gone: the rule above silences the
donkey's own sounds on them by itself, and their `mount.cfg`'s `[Sounds]` lines say `none` where that is meant. (A pin
still works: those keys, in a `mount.cfg`'s `[Tuning]`, are the base layer.)

## What a skin is missing

Three places say it, for the same question ("which of its sounds has this skin still no sound for"):

- **The log at the game's start** (once a session, when the game is ready): one line per skin, `Sounds: skin
  'saddleup-critters.rat/default' (mount saddleup-critters.rat): its own: ...; its mount's: voice (spitter_attack), step (by the ground), ...; MISSING (the donkey's
  own, silent here): answer, mount, dismount; the donkey's neutral ones 6; silent by default 18.`
- **The Mounts tab** (F11): the first line of "Sounds of this skin" lists the missing events in orange, or says
  nothing is missing.
- **`tests/SkinsScan`**: an `INFO <skin>: sounds MISSING ...` line per built-in creature.

"Missing" means exactly the donkey's own events with no line in the skin's or its mount's blocks, riding plain. An
explicit `none` counts as given (silent by choice). The neutral events and the ones no mount sounds by default
(`setoff`, `ear`, the flying ones, ...) are never "missing".

## The layers, and the player

For every event on every instance, the first layer that gives a **source** wins; the layers, top down:

1. a mod's `Mounts.Sounds.Set` on that instance (the session only);
2. the skin's `[Sounds.Baby]` for a baby, then its `[Sounds.Ride]` or `[Sounds.Cart]` (the style the mount is in),
   then its `[Sounds]`;
3. the mount's (its `mount.cfg`): `[Sounds.Baby]` for a baby, then `[Sounds.Ride]` / `[Sounds.Cart]`, then its
   `[Sounds]`;
4. the **base**: the player's `[Ride]` / `[Cart]` keys of the API cfg (reference.md "Sounds": `HoofSoundId`,
   `BraySoundId`, `GrazeSoundId`, ... with their volumes, pitches and numbers), which are the donkey's sounds as
   shipped and as the player tuned them, and which a mount's `[Tuning]` (in its `mount.cfg`) may pin.

**The donkey's own sounds are the donkey's.** Its voice, its lip smack, its chewing, its getting-on and getting-off
snorts riding plain (`voice`, `answer`, `lips`, `graze`, and `mount` / `dismount` riding plain) never reach another
mount: on any other creature they are **silent until its pack gives them a sound** (a crab never brays by default;
decided 2026-10-24). The neutral sounds are everyone's: the step by the ground, the cart's wheel and thump, hitching
and unhitching, the bell, and the cart style's ladder sounds getting on and off. The donkey mount itself, Hellfire and
Royal Parade included, is untouched.

A layer's `volume` and `pitch` multiply the base's (the topmost layer that says one wins among the layers); its
other attributes and numbers replace the base's. The player's switches stay on top of every layer: `SoundsOn`,
`SoundVolume`, the per-event `...On` switches, Saddle Up's Bell, Brays and Volume rows, the game's Sounds slider.
A skin can give the rat a squeak; the player can still turn the brays off.

Standing mounts and babies play their animator's events too (steps, grazing, the lip smack), a grown one calls now
and then and the babies answer it (the herd, above), within the game's hearing distance, under the API cfg's
`[General] StandingSounds` (on; Saddle Up's "Standing mounts and babies sound" row). A baby is pitched up and softer
by its size.

## How it plays

A layer's files are registered as **game sounds** under an id of the API's own (`gk2.<owner>.<skin>.<section>:<event>`,
`<skin>` the skin's full name; `gk2.<owner>.mount-<mount>.<section>:<event>` for a mount's, e.g.
`mount-saddleup.dragon`)
with the game's `Default3D` preset (3D, 1 to 14 units) and the in-world sfx mixer group, so they play through the
game's own pooled player, follow its Sounds slider and pause with it. The files are decoded when the event is first
needed (an embedded pack's `.ogg` through a cache file under `BepInEx\cache\GK2DonkeyAPI\sounds\<owner>\<skin>\` for a
skin's, the full name with `/` as `_` (`saddleup.dragon_default`), `...\<owner>\mount-<mount>\` for a mount's); the
event is
silent until they are in (one log line per line of files: "n file(s) loaded for ..."). A game id plays as the game
plays it. The game's own clips are streamed and cannot be reshaped: a deeper bray on a game clip is a `pitch`, a
different one is a file of your own.

**Licensing:** a pack's files are the pack author's to license. The game's clips are used by id, never shipped;
shipping modified copies of them is a licensing question for whoever ships them.

## Checking a pack

The F11 dev window's Sounds tab lists every event of the ridden mount with what plays and from which layer, a Play
button each, and the standing switch; its Mounts tab has one row per event for the skin's block and the mount's
(the line's source and its volume), with the auto-save writing the skin's lines to its `skin.cfg` and the mount's to
its `mount.cfg`. The log says what the loader found for the pack ("n sound line(s)", the problems by name) and which
files loaded. `tests/SkinsScan` checks a built-in pack's lines and files (a skin's in its own `sounds\`, a mount's in
the mount folder's); the self-tests of group Sounds cover the system (self-tests.md).

## From code

A skin or a mount registered in code gives its sounds in its def, by event key:

```csharp
new MountDef
{
    Id = "me.crab", Name = "Giant Crab",
    Sounds = new Dictionary<string, MountSound>
    {
        ["voice"] = MountSound.Own(clickClip1, clickClip2),  // your AudioClips: registered as a game sound, never destroyed by the API
        ["step"] = MountSound.Game("step_stone"),            // a game sound id
        ["lips"] = MountSound.None,
    },
}
```

`MountSkinDef.Sounds` / `SoundsRide` / `SoundsCart` / `SoundsBaby` and `MountDef.Sounds` / `SoundsRide` / `SoundsCart` /
`SoundsBaby` take the same dictionaries. `Mounts.Sounds` (api.md "Sounds") answers what plays on an instance (`Resolved`, with the
layer), sets a mod's own sound over everything (`Set`, back on Dispose), plays and stops an event (`Play`, `Stop`),
and raises `Played` after every play.
