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
| `refused` | a ride or a hitch refused at the mount | silent unless a layer gives a sound (the game's `donkey_default_nope` is a fit) |
| `mount`, `dismount` | the keeper gets on / off | not at doors, scene changes, failed re-summons, style changes |
| `hitch`, `unhitch` | a cart hitched / left standing | |
| `cargo` | an item lands on the cart | |
| `bell` | the bell | on setting off after standing (`on_set_off`, `set_off_idle`, `only_with_cargo`), the bell key, `Mounts.RingBell()` |
| `sent`, `called` | sent away / called over | silent unless a layer gives a sound |

Flying events (take-off, wing beats, the glide, the landing, the jump) come with phase 2; a standing herd's calls
and the babies' answers with phase 3 (the design's sections 3.2 and 3.3).

## A pack's sounds

Put the files in the pack's `sounds\` folder (next to `skin.cfg`), `.ogg` (Vorbis, the game's own format, small)
or `.wav` (PCM 8/16/24/32-bit or float, mono or stereo, any rate); a few seconds each.

**By name, with no line in the cfg:** `sounds\<event>[.<variant>][_<n>].ogg|wav`. `voice_1.ogg`, `voice_2.ogg`
and `voice_3.ogg` are the skin's `voice` (one at random per call, never the same twice in a row);
`step.grass_1.wav` its step on grass; `graze.ogg` its grazing. A file named like no event is listed in the log
and never played.

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
```

The sections stand after the skin's `[Tuning]` sections and before `[Colors]` (the canonical layout;
`tools/skins/canonical_cfg.py` and `tests/SkinsScan` refuse a built-in that differs). Unknown events, variants and
attributes are named in the skin's report in the log and ignored; a file a line names that is not in `sounds\` is
named and left out; a line with none of its files left falls through to the layer below.

**A mount's sounds**, shared by every skin of it, go in `[Mount.Sounds]` (and `[Mount.Sounds.Ride]`,
`[Mount.Sounds.Cart]`) of the skin that declares the mount (its `[Mount]` block), after `[Mount.Tuning*]`. Their
files are that skin's `sounds\` folder. A second skin of the mount with no `[Sounds]` sounds like the mount; its own
`[Sounds]` lines win over the mount's, event by event.

```
[Mount]
id = dragon
...

[Mount.Sounds]
voice = roar_1.ogg, roar_2.ogg
voice.idle_every = 60
step = thud.ogg
lips = none
```

The snail's and the worm's existing silence is the older form, `[Mount.Tuning] BrayVolume = 0` and friends: it
still works (those keys are the base layer, section below); `[Mount.Sounds] voice = none` says the same.

## The layers, and the player

For every event on every instance, the first layer that gives a **source** wins; the layers, top down:

1. a mod's `Mounts.Sounds.Set` on that instance (the session only);
2. the skin's `[Sounds.Ride]` or `[Sounds.Cart]` (the style the mount is in), then its `[Sounds]`;
3. the mount's `[Mount.Sounds.Ride]` / `[Mount.Sounds.Cart]`, then its `[Mount.Sounds]`;
4. the **base**: the player's `[Ride]` / `[Cart]` keys of the API cfg (reference.md "Sounds": `HoofSoundId`,
   `BraySoundId`, `GrazeSoundId`, ... with their volumes, pitches and numbers), which are the donkey's sounds as
   shipped and as the player tuned them, and which a mount's `[Mount.Tuning]` may pin.

A layer's `volume` and `pitch` multiply the base's (the topmost layer that says one wins among the layers); its
other attributes and numbers replace the base's. The player's switches stay on top of every layer: `SoundsOn`,
`SoundVolume`, the per-event `...On` switches, Saddle Up's Bell, Brays and Volume rows, the game's Sounds slider.
A skin can give the rat a squeak; the player can still turn the brays off.

Standing mounts and babies play their animator's events too (steps, grazing, the lip smack), within the game's
hearing distance, under the API cfg's `[General] StandingSounds` (on). A baby is pitched up and softer by its size.

## How it plays

A layer's files are registered as **game sounds** under an id of the API's own (`gk2.<owner>.<skin>.<section>:<event>`)
with the game's `Default3D` preset (3D, 1 to 14 units) and the in-world sfx mixer group, so they play through the
game's own pooled player, follow its Sounds slider and pause with it. The files are decoded when the event is first
needed (an embedded pack's `.ogg` through a cache file under `BepInEx\cache\GK2DonkeyAPI\sounds\`); the event is
silent until they are in (one log line per line of files: "n file(s) loaded for ..."). A game id plays as the game
plays it. The game's own clips are streamed and cannot be reshaped: a deeper bray on a game clip is a `pitch`, a
different one is a file of your own.

**Licensing:** a pack's files are the pack author's to license. The game's clips are used by id, never shipped;
shipping modified copies of them is a licensing question for whoever ships them.

## Checking a pack

The F11 dev window's Sounds tab lists every event of the ridden mount with what plays and from which layer, a Play
button each, and the standing switch; its Mounts tab has one row per event for the skin's block and the mount's
(the line's source and its volume), with the auto-save writing the lines to the skin.cfg. The log says what the
loader found for the pack ("n sound line(s)", the problems by name) and which files loaded. `tests/SkinsScan` checks
a built-in pack's lines and files; the self-tests of group Sounds cover the system (self-tests.md).

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

`MountSkinDef.Sounds` / `SoundsRide` / `SoundsCart` and `MountDef.Sounds` / `SoundsRide` / `SoundsCart` take the
same dictionaries. `Mounts.Sounds` (api.md "Sounds") answers what plays on an instance (`Resolved`, with the
layer), sets a mod's own sound over everything (`Set`, back on Dispose), plays and stops an event (`Play`, `Stop`),
and raises `Played` after every play.
