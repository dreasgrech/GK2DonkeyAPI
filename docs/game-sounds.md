# The game's sounds, extracted (2026-10-04)

Every audio clip of the game (Steam build, game 1.008) exported as WAV, to pick sounds for the donkey API and music
for the showcase video ([showcase-video.md](showcase-video.md)). Nothing is built from it yet.

## Where

`C:\ClaudeCodeTemp\GK2-music\` (outside the repo: 2.7 GB, and the sounds are the game makers', not ours to ship).

| Path | What |
|---|---|
| `clips.csv` | Every AudioClip found: name, length (s), load type, source file. 7,261 rows (6,393 distinct; most also sit in `sharedassets0.assets`) |
| `music\` | The 28 longest tracks (40 s and up, minus ambient loops), from the first export |
| `sfx\` | All 6,393 distinct clips, one folder per name prefix (about 420 folders) |
| `sfx\_music\` | Every clip of 100 s or more: the soundtrack |
| `sfx\_quest_voice\` | The numbered quest voice lines (`113_sewers_god_1.wav`, `2_intro_scout_bridge_1.wav`, ...) |
| `sfx\_donkey_shortlist\` | 66 copies picked by name for the donkey API (below) |
| `scan.py`, `export.py`, `export_all.py` | The scripts; run again after a game update |

Short folders with made-up names (`aduda`, `kaka`, `yala`, ...) are the characters' gibberish speech.

## How

- The audio lives in Addressables bundles: `GraveyardKeeper2_Data\StreamingAssets\aa\StandaloneWindows64\*.bundle`
  (hashed names), plus copies in `sharedassets0.assets`.
- Read with UnityPy 1.25.4 (Python 3.12). The bundles carry no Unity version, so the scripts set
  `UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.9f1"` (the game's Unity, from `LogOutput.log`); without it every
  bundle fails to parse.
- `AudioClip.samples` decodes each clip (Vorbis in FSB) to WAV through UnityPy's bundled FMOD: 44.1 kHz, stereo for
  the music. Checked: the lengths match the game's `m_Length`, and the audio is not silent.
- Clips are deduplicated by name and length; a second clip with the same name gets `_2`.

## The sound ids (2026-10-24)

The mod plays game sounds by their **id** (`AudioConfig.sounds`, 311 entries), not by clip name. `dump_audiocfg.py`
(next to `scan.py`; UnityPy with the type-tree generator fed the game, the `AudioConfig` node from
LazyBearTechnology.dll) writes `sound_ids.csv` and `sound_ids.txt`: every id with its 3D preset (0 = 2D, 1 = the
in-world 3D preset, 2 = sound zones, 3 = fights, 4 = stations), volume, loop flag and its clips. Run it from a folder
with no stray `.py` files (a helper named `snappy.py` next to it shadowed a module UnityPy imports, 2026-10-24).
The creature-like ids are few: `garry` (Gerry's voice), `voice_larry` (burps), `larry_steps`, `larry_chatterteeth`,
`gerry_somersault`, `spitter_attack` / `spitter_hit`, `stinker_death`, `zombie_idle` / `zombie_attack` / `zombie_death`,
`wolves_howl`, `plague_doctor_cough`, the `step_*` ids, `item_use_food`, `fishing_blop`, `bush_chop`,
`conveyor_furnace_bellows`. The built-in mounts' first picks are in their skin.cfg `[Mount.Sounds]` blocks
(docs/mount-sounds.md).

## The donkey shortlist

Picked by name only; nobody has listened to them yet.

| Clips | Status | Idea |
|---|---|---|
| `donkey_bell`, `donkey_eating_grass`, `donkey_lips_1/2`, `v_donkey_1..4` | Used by the API already | |
| `donkey_default_*` (20: `hey`, `yes`, `no`, `nope`, `whats_up`, `okay`, `im_going_home`, `not_in_million_years`, ...) | Not used | The game's talking donkey: reactions when you get on, when a ride is refused ("nope"), when it's sent away ("im going home") |
| `grass_`, `road_`, `stone_`, `wood_`, `wet_`, `inside_footstep_*` | Not used (the API uses `step_dirt`) | Hoof steps that change with the ground |
| `potters-wheel_loop`, `spinning_wheel_run`, `ConveyorFurnace_Wheel_*`, `ConveyorAssembly_Wheel_*` | The conveyor furnace wheel is used | Other cart rolling loops |
| `sawhorse_1..4` | Not used | Wood creaks for the cart |
| `Daily Bell Sound`, `BrokenBell` | Not used | Other bells |

**Next:** the user listens and picks; then a plan per sound (where it plays, how often, the setting that turns it off).

## Music for the video

First picks by name: `gameplay_track_calm`, `village`, `location_theme_village`, `heartwelming`. Mod videos with a
game's music are usually tolerated, but YouTube's Content ID may claim the video for the rights holder.
