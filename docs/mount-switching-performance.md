# Switching mounts: what it costs, and what scales

Investigation of 2026-10-25. The user picked mounts in the Mounts panel ("the dragon ... is doing the micro freeze when
selecting it from the journal"), and asked for the result to serve as a base for future mounts: other mods will add
mounts and skins, so every cost below has to be judged per mount, not for Saddle Up's six.

The measurements come from the `[pick]` trace (`Perf.TracePick`: the 120 frames after a click, every frame over 12 ms
or with the mod's work over 2 ms, with the measured parts and notes) and each skin's "loaded" line (`Looks.BuildCost`).
The game runs at 120 fps here: a normal frame is 8.3 ms, and one over about 12 ms shows as a jolt.

## 1. Fixed on 2026-10-25

| What | Before | After |
|---|---|---|
| The click (the Mount row set) | 6 ms: the whole cfg file written on every pick | 2.2 ms: written once the picking pauses, or when the panel closes |
| A look built | 6 pictures a frame: 4 to 17 ms a frame (Gerry, Hellfire: 17 to 44 ms) | 2 ms a frame; its sheet in a frame of its own; nothing in the click's frame |
| The dragon's sheet made | 2.4 to 14.7 ms (Unity zero-fills 15.6 MB) | 3.4 ms (made uninitialized; every cell is written anyway) |
| Gerry's frames looked for | 6.5 ms on each first pick (every sprite in memory scanned) | once a session (the game-ready check finds them) |
| A look picked again | built again (the dragon: 40 frames) | shown in the click's frame (held while the panel is open) |
| The switch to a finished look | in the frame its sheet was uploaded | the frame after |

Result (log 2026-10-25 11:43): every switch to a look already shown: the click's frame 9 to 12 ms, 0 frames over 12 ms.
A first pick: the click's frame 10 to 15 ms; what remains is in section 2.

## 2. What remains

### 2.1 The picture's first framing of each look, once a session (the first-pick jolt)

Each look's picture is framed by measuring what it draws over 2 s of its walk: 20 photos, one a frame, each read back
from the graphics card (`PreviewStage.TightenStep`). It is remembered for the session (`framings`), so only the first
pick of a look per session measures. Cost seen: the first step 5.8 ms (Hellfire) to 9.7 ms (the dragon), in the frame
the look appears (an 18 ms frame); the 19 steps after, 2.2 to 3.6 ms each (some frames 12 to 16 ms).

Per mount, this is one measure per look per session, so it grows with the number of looks a player browses.

Options:

- **A. Keep the framings on disk** (recommended now). A small file under `BepInEx\cache\GK2DonkeyAPI\`, one entry per
  look and picture, keyed by what decides the framing: the look's content stamp (its pictures and tuning: canvas,
  pivot, scale, hitch gap, its default's stamp when built on it), the game build, and a framing version number. A look
  is measured once ever per version. A changed look or a new one measures as today. The edge check still catches a
  framing something outgrows. A stale framing that is too wide isn't caught, which is why the key must cover every
  input.
- **B. Compute the framing without photos.** The mount's drawn pixels per walk frame are known from the build
  (`ColumnsByKey` has the columns; rows would be added). The cart, lamp and shafts are the same 3D parts for every
  mount, offset by the hitch gap. Their union, projected at the game's camera angle, gives the frame. No read-backs at
  all, for any mount, first time included. More work, and the projection of the 3D parts has to match the photos.
  This is the end state if A isn't enough.
- **C. Make the first step cheaper.** It is unmeasured inside (a new scratch texture each time the size differs, the
  first render, a read-back). A scratch texture kept at a bucketed size would remove the re-creation. Measure first.

Also: the edge check reads back the whole picture once a second while the panel shows it (2 to 3.6 ms, the
`Preview` frames every second in the traces). With A, it could run only for a few seconds after a framing, or after a
tuning change.

### 2.2 The world's mount changed (closing the panel with another mount chosen): a 60 ms frame

When the panel closed with the dragon chosen, the ridden mount became the dragon in one frame of 60 ms:

- **Sounds: 15.6 ms.** The dragon's 13 sound files (take-off, flap, glide, land) were all started in that frame. For
  each one: its bytes read from the embedded pack, the cache file checked on disk (and written when missing), and a
  `UnityWebRequest` created. Their decoding (`DownloadHandlerAudioClip.GetContent`) lands a few frames later, all
  together.
- **The view: about 10 ms** (Spawn 4.6, ColorBuild 3.9, Prepare 1.4), plus the free cart left behind (its lantern and
  lamp pieces split: `LampHang`, 18 pieces).
- **About 32 ms outside every measured part.** That's the game's own instantiation of the new view and cart, or
  something not yet measured. A trace of the world change (like the pick's) is needed to split it.

Per mount, a mount with more or longer sound files costs more, and every mount change pays it again.

Options:

- **Sounds loaded ahead and spread.** Start a mount's files when it is chosen, as its look is built ahead
  (`Looks.TickWarm`): one file a frame, the cache files checked once a session. Consider keeping clips compressed in
  memory (`DownloadHandlerAudioClip.compressed`), so a load doesn't decode the whole file at once.
- **Trace the world change** (a `[switch]` trace from the mount change: spawn, view, cart, sounds, and the frames
  after), then decide on the view and cart part.

### 2.3 A sound played by a standing mount: 2.5 ms each

In every trace, a `Sounds 2.4-2.9` inside `Stand` appears every few frames with mounts standing nearby (the foals'
steps and calls). The mod's own code around it is small, so the cost is most likely inside the game's play call
(`LazyAudio.PlayAtGameObject`), or in our clip switching around it (`PlayClip` toggles every sample's `isEnabled`).

Per mount, this is 2.5 ms per sound played, so a yard of mounts walking costs a frame's budget in steps alone.

Next: measure the play call by itself (a `SoundPlay` part), then decide: pool, rate-limit by distance, or skip the
clip toggling when a sound has one clip.

### 2.4 A look's build, by its size

| Look | Pictures | Sheet | Build (2 ms a frame) |
|---|---|---|---|
| Rat, snail, worm | 44, 96x96 | 680x680, 1.8 MB | 4 frames |
| Gerry (from the game's sprites) | 44, 160x160, read back through the graphics card | 1128x1128, 5 MB | 14 to 38 frames |
| Hellfire (over the donkey's frames) | 44 | 680x680, 1.8 MB | 14 to 31 frames |
| Dragon | 237, 128x128 | 2065x1936, 15.6 MB | 18 to 28 frames |

For scale:

- **Trim the cells to what is drawn.** Most of a 128x128 cell is empty. Trimmed cells, with the pivot moved to match,
  would roughly halve the dragon's memory and upload, for every mount.
- **A pack may ship its sheet ready-made** (`LoadSheetPng` exists: one decode instead of one per picture). The repo's
  tools could pack Saddle Up's.
- **Game-source looks** (Gerry) read every picture back through the graphics card, 2 to 3.5 ms each and more when the
  card is busy. Reading the character's atlas once, and cutting the frames from it on the CPU, would be one read-back
  instead of 44.

### 2.5 The panel's held looks have no cap

While the panel is open, every look it showed stays loaded. With Saddle Up's six that is about 30 MB, but with other
mods' mounts it is unbounded. Add a cap (about 64 MB of sheets): beyond it, the least recently shown look is let go.

## 3. Suggested order

1. 2.2 sounds ahead and spread, and the `[switch]` trace (the largest single frame left, paid on every mount change).
2. 2.1 A, framings on disk (the first-pick jolt; small).
3. 2.3 measure the play call (affects every mount standing in the world).
4. 2.5 the cap (small, needed before other mods add mounts).
5. 2.4 trimmed cells and the atlas read for game-source looks (memory and first builds; larger).
6. 2.1 B, framing without photos, if A is not enough.
