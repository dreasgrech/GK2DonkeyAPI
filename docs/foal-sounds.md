# Foal sounds (investigation, 2026-10-03)

Can the baby donkeys make sounds, and how far can their voices be pushed towards "baby"? Investigated against game
1.008 (LazyBearTechnology and Assembly-CSharp decompiles, the AudioConfig dump in PROBE-FINDINGS Round 12, the clip
assets read with UnityPy). Nothing built yet; the pitch choice waits for the audition below.

## What we have today

- **Only the ridden donkey makes mod sounds.** `MountSounds` is one static set of timers for `Mount.Donkey`: hooves,
  wheel, bray, graze, lips, bell. Foals (and every other standing donkey) get nothing from it.
- **Foals probably already snort.** The donkey's own lips animation calls `AnimationComponentBase.PlaySound("donkey_lips")`
  from an animation event, and our prefix reroutes that only for the ridden donkey. A foal's view is the same donkey,
  so when its idle animation picks lips it should play the adult snort at the adult pitch, through the game.
  Expected, not yet seen in game (a log line in the prefix would confirm it).
- **The game has no young-animal sound.** The 310 sound ids have no foal, lamb, kid or other small-animal voice. The
  donkey's are `voice_donkey` (v_donkey_1..4, 0.29–0.54 s brays), `donkey_lips`, `donkey_eating_grass` and
  `donkey_bell`. Anything "baby" has to be made from those.

## What the game's sound system lets us change

Every sound goes through `LazyAudio.PlayAtGameObject(id, transform, …)`, which returns a `SoundHandler`:

| Control | How | Notes |
|---|---|---|
| Pitch | `SetPitch(x)` (x the sample's own pitch) | Unity resamples: higher **and** shorter (x1.6 = 38% shorter). Unity allows −3..3; `MountSounds.Play` clamps 0.1–3. Already used for hooves, wheel and bray. |
| Volume, panning | `SetVolume`, `SetPanning` | Already used. |
| Where in the clip | `SetTime(t)` | Could start a bray part-way (a clipped, shorter call). |
| Mixer group | `SetMixerGroup(group)` | Already used (`BrayInSfxChannel`). |
| End | `OnSoundPlayed` callback | Fires when the pooled player is reset. |
| **Unity's audio filters** | `AudioHighPassFilter`, `AudioLowPassFilter`, `AudioChorusFilter`, `AudioEchoFilter`, `AudioDistortionFilter` added to the player's GameObject | Not exposed by `SoundHandler`. Its `sourceHolder` field is private, but the `SoundController` it points to has a public `audioSource`: one `AccessTools.FieldRefAccess` gets it. |
| **Our own DSP** | A `MonoBehaviour` with `OnAudioFilterRead` on that GameObject | Sees and rewrites the samples as they play. The ceiling: a pitch shift that keeps the length, a warble, anything. |

What we **can't** do:

- **Reshape the clips in advance.** The donkey clips are Vorbis with the Streaming load type (checked:
  `m_LoadType = Streaming` for all eight donkey clips), so `AudioClip.GetData` doesn't work on them. There's no
  "make a baby version of the clip once at load". Live DSP (`OnAudioFilterRead`) is the only way to process them.
- **Add an effect to the game's mixer.** Mixer effects live in the mixer asset, and it exposes nothing we could use.

### Things to design around

- **Pooled players.** `LazyAudio` reuses its `SoundController`s (15 made up front, more on demand). A filter added for
  a foal must be removed again in `OnSoundPlayed`, or the next sound (a footstep, a door) plays through it.
  This depends on a private field in LazyBearTechnology: try/catch, fall back to plain pitch, log once, like the rest
  of the API.
- **The bray is not positional.** `voice_donkey` uses 3D preset `Default` (type 0). `donkey_lips` and
  `donkey_eating_grass` use `Default3D` (1). If `Default` is 2D (likely, since it's the dialogue voice), a foal's bray
  is as loud far away as close by. That's low stakes, since foals stay by the parent, but foal sounds should check
  the distance to the keeper themselves. The preset's `spatialBlend` can be logged once in game to settle it.
- **The game's replay throttle.** With `checkDelay: true`, the same id can't play again within
  `minDelayTimeBetweenSounds` (15 s). The API passes `false` everywhere. A foal *answering* its parent must pass
  `false` too, because the parent's bray has just played.
- **A herd is noisy.** Five foals plus the parent on their own timers would be a racket. The sounds need one shared
  cooldown (one voice at a time), and mostly answers rather than free calls.
- **Smallness should have one source.** The foal's size is `Instance.ViewScale` (0.6). Pitch and volume should come
  from it (and a fixed per-foal offset so siblings sound different), not from a separate number.

## Options, cheapest first

1. **Plain pitch** (no new technique). Foal brays through the existing `Play` with pitch around x1.5–1.8, lower
   volume, and a fixed small offset per foal. Foal footsteps (x1.5, half volume), grazing (x1.3) and lips (x1.5)
   the same way. Per-foal timers live on the `Instance` (`StandTick`), not in `MountSounds`.
2. **Pitch + a built-in filter.** As 1, plus a high-pass (thinner, a smaller chest) or low-pass (takes the hiss off
   a pitched-up bray) on the player while it plays, removed after. Needs the private-field access and the pooled
   clean-up above.
3. **Calls made in code** (no new audio technique, combinable with 1 or 2). A foal call is two or three quick
   pitched brays in a row; a little hee-haw alternates two clips up and down; a foal **answers** its parent's bray
   0.5–1 s later; siblings answer one after another, each at its own pitch.
4. **Own DSP** (`OnAudioFilterRead`). Pitch up while keeping the bray's length, a shaky vibrato. This is the
   furthest we can go with the game's clips, but it means sample-level code running on the audio thread, and the
   offline preview is slightly grainy. Recommended only if 1–3 don't sound young enough.
5. **Own recordings** (outside the game's clips). Real foal sounds shipped with the mod, from a free sound library
   with a licence that allows it. It sounds the most real and needs no tricks, but it adds files to the mod and
   needs `AudioClip` loading from disk. Shipping *modified copies of the game's clips* is a licensing question for
   the user, so nothing here builds towards it.

**Recommendation:** 1 + 3 first: plain pitch, foals answering the parent, footsteps and grazing, one shared
cooldown. Add 2 if the audition shows a filter is worth it. Hold 4 and 5 unless the pitched game clips don't
sound right.

## Audition

`C:\ClaudeCodeTemp\GK2-foal-sound-audition\`, rendered by [`tools/foal-audition/foalaudition.py`](../tools/foal-audition/)
from the clips exported in Round 12 (re-run it to get the files back; its `README.md` lists each file):

- `A_pitch_*`: all four brays at x1.0 (reference), 1.25, 1.4, 1.6, 1.8, 2.0, 2.4. This is exactly what
  `SetPitch` does in game.
- `B_*`: x1.6 with a high-pass (700 / 1200 Hz), a low-pass (3000 Hz) or a chorus. These approximate Unity's filters.
- `C_*`: x1.6 keeping the length, and x1.6 with a vibrato. These approximate the own-DSP option.
- `D_*`: calls (three squeaks, a small hee-haw, the parent then a foal answering, three siblings answering).
- `E_*`: lips, grazing and footsteps, adult then foal.

To decide from the audition: the pitch (or range), whether a filter earns its complexity, and which call patterns
to use.
