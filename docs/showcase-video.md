# Showcase video (plan, revised 2026-10-04; not shot yet)

A short video showing as many Saddle Up features as possible, about 75 seconds. Each shot covers two or three
features. The setup and the edit are automated (see "Automation" below); the action is played by hand.

## Shot list

| # | Time | Shot | Features | Caption idea |
|---|---|---|---|---|
| 1 | 0–5 | Cold open: the keeper at the graveyard, facing away from the camera's edge, presses **H** and turns: the donkey is there with its foals; he jumps on. (H puts it next to him at once, it doesn't trot in: the turn or a cut on the key press hides that.) | Calling the donkey, mounting, foals | "Your own donkey." |
| 2 | 5–12 | Ride fast across a hill; a short bray. The rider bobs, his hands and head move with the walk. | Riding speed, the donkey on the ground surface and the cart pitch on slopes, rider motion, bray sounds | "Ride it." |
| 3 | 12–20 | Press **G** mid-ride: the cart rolls up and is hitched; drive off with the **bell**. | Switching H/G, hitching, the bell | "Hitch the cart." |
| 4 | 20–32 | Drive past a log pile and pick up a log (it lands on the cart), then a stone. Pull up at the workbench pile and hand them in from the cart. | The cart carries two items; piles take from the cart | "Haul logs, stone…" |
| 5 | 32–38 | A corpse onto the cart, drive to a grave, bury it from the cart. | Corpses, grave and quest hand-ins | "…and the dead." |
| 6 | 38–48 | **H** leaves the cart standing with its load; ride off. Walk back on foot: "[E] Ride Bella" shows at the donkey; carrying a stone, **E** puts it on the cart; at the back, **E** takes the top item; ride up and **E** hitches it. | The standing cart, the name, loading and taking on foot, hitching with E | "Leave it loaded." |
| 7 | 48–54 | Ride to the house door and go in: the donkey stays on the doorstep, the foals come in with you and trot after you. Walk back out. | Getting off at doors, foals indoors, lively foals | "They follow you home." |
| 8 | 54–64 | **Choose colours**: click through coat, harness, paint, wheels and hubs while the real donkey and cart change live. Quick cut to the picture in the Mods menu. | The live colour picker, wheel and hub colours | "Make it yours." |
| 9 | 64–70 | Night: the cart's lantern lit, the hanging lamp swinging; the light switches warm, then green. | Lantern, light colour | "Light the way." |
| 10 | 70–75 | Hard cut from day to night (or after a reload), the donkey and its load in the same spot. End card: logo, "Requires GK2 Donkey API". | Kept with the save | "It's still there." |

Changes from the first plan (2026-10-03): shot 1 no longer has the donkey trot in from off screen; shot 2's "tilt"
became the ground surface and the cart pitch (the slanted donkey was rejected, [slopes.md](slopes.md)); the old shots 6
and 8 are one take, with loading on foot and hitching with E added; lively foals and rider motion added; shot 11's
loading screen replaced by a hard cut.

**Before shooting shot 7:** confirm in game that riding through a door leaves the donkey on the doorstep
([open-items.md](open-items.md) 9n, last check; fixed 2026-10-04).

## Ways to tighten it

- **An optional ending for mod authors (+5 s):** a herd of donkeys with carts across a field (the herd test mod),
  "Built on GK2 Donkey API". Only if the video should pitch the API too.
- **Drop first if it runs long:** shot 5, the menu half of shot 8, shot 9.

## Still to decide

- Where it goes: the Nexus page and YouTube (landscape), or a vertical short (the timings change).
- Music only, or the game's audio with the brays and the bell (they sell the feel). The game's soundtrack is
  extracted to `C:\ClaudeCodeTemp\GK2-music\music\` (and every sound to `...\sfx\`); first picks by name:
  `gameplay_track_calm`, `village`, `location_theme_village`, `heartwelming`. The music is the game makers': mod
  videos with it are usually tolerated, but YouTube's Content ID may claim the video.
- Saddle Up only, or the API pitched too.

## Automation

The split: everything around the action is scripted (setup, recording, key overlay, edit); the action itself is
played by hand, because scripted walking looks robotic and the game's own work actions (picking up, handing in,
burying) would each need new research. A bad take then costs seconds, not a reset. A shot is scripted further only
if it keeps going wrong by hand.

### 1. The director (a dev plugin, not shipped)

**Built 2026-10-05, first part: time of day and weather** (`tools/GK2ShowcaseDirector`, F9; untested in game):
- **Time:** a slider and presets (dawn, morning, noon, afternoon, dusk, night, midnight) through the game's
  `EnvironmentEngine.SetTimeOfDayFake`. It changes the light, the HUD's sun-and-moon wheel (`UIHUDWheel` follows fake
  times) and the town's ambient sounds. The clock pauses; the save's time and day don't move, and the game's
  time-triggered scripts ignore fake times (`Flow_OnTimeOfDayEvent`). "Back to the real time" calls
  `ResumeTimeOfDayFromData`. A real new day isn't offered: that is what sleeping does (decay, crops, quests).
- **Weather:** one button per weather state, read at run time from the weather state machine (the names are in the
  game's assets). `SetWeatherState(name, force: true)` holds it, and the game stops rolling new weather.
  "Back to the weather before" restores what was there before the first change, rolling again.
  - A weather the game holds (a quest or event set it) is left alone: entering a weather state runs its enter script
    and leaving it its exit script (`FSMWeatherState.OnEnter` / `OnExit`).
  - For the same reason the quests' event weathers ("Event…") aren't offered, nor "NULL Weather", a router with no
    weather that can re-enter itself endlessly.
  - Offered: the 9 ordinary weathers the game rolls between anyway (clear, five rains, weak wind, light and strong
    mist). The weather is saved
  with the game, so set it back before saving.
- **While a time is held**, the few things that read the shown time see it: fishing (day or night fish), the scripts'
  "time between" and "time of day" checks (quests, NPCs), fog and lighting. Don't fish or do time-dependent quest
  steps while holding. The game's clock is paused, so time-scheduled things wait. A cutscene or sleep takes the time
  over (the game's `Flow_TimeController`), and the director notices and lets go.
- Its own window, not a tab of the donkey API's panel (one feature per plugin). Log prefix `[director]`.

Its own plugin (`dreas.gk2.showcasedirector`, one feature per plugin), using the public API wherever it can. A
number key per shot (or a panel row) puts the game in that shot's starting state:

| Setup step | How (decomp 1.008) |
|---|---|
| The keeper at the spot | `PlayerController.SetPosition` (same scene), on the ground at a saved spot |
| Time of day | `EnvironmentEngine.SetTimeOfDay(float)` |
| Weather | `WeatherSystem.SetWeatherState(name, force: true)` / `ResetWeatherState()` (what `Flow_SetWeatherState` does) |
| Donkey, foals, cart, load | The API: place the companion (`MountHandle` / `MountCompanion`), foals, the cart's cargo |
| Name and colours | The API's companion settings (name, coat, harness, paint, wheels, hubs, lantern light) |
| Items along a route (log, stone, corpse) | The game's `dropSystem.DropItem(item, scene, at)`, as the cart's set-down does |
| No HUD | `HUD.SetMode` / `HUD.SetDisableState(HudStateType, ...)`: which mode hides it all is still to be found |
| Key overlay | Draws the pressed key (H, G, E, J, B) big at the bottom for a second, and logs `[shot] key X at t` |

Spots are saved in the director's own cfg with a "Save this spot for shot N" button, so they're chosen by walking
there once. Each shot's setup is logged (`[shot] N ready`) so the recording script can wait for it.

**Optional, per shot:** a cue list fired on timers (for example shot 3: G at 2.0 s, bell at 3.5 s; shot 9: light warm,
then green). Only for the timed shots, if they keep going wrong by hand.

**Riding along a route (only if needed):** waypoints fed into the movement input through the input hook the mod
already has (HeadReroute's `PlayerInputHandler.UpdateInput` prefix), with eased turns. Not the game's walk-to-point:
that ends a ride.

### 2. Recording (OBS)

- OBS Studio is installed, with its websocket (port and password in
  `%APPDATA%\obs-studio\plugin_config\obs-websocket\config.json`).
- A small script (Python, `obsws-python`, not installed yet) per take: start recording, wait for `[shot] N ready` in
  `LogOutput.log`, record the shot's length plus a second either side, stop, and rename the file `shotN_takeM.mkv`.
- One OBS scene: the game window only, 1080p or 1440p, 60 fps.

### 3. The edit (ffmpeg)

ffmpeg 8.1 is installed. A script builds the video from the shot list and a chosen take per shot:

1. Cut each take to its slot (`-ss`/`-t`), with a short crossfade between shots (`xfade`).
2. Captions with `drawtext` in the game's font, at the times in the shot list.
3. Key presses: either the director's overlay (recorded in the video), or drawn by ffmpeg from the `[shot] key`
   log lines.
4. The music under the game's sound (`amix`, music at about 30%, faded out at the end), or the music alone.
5. The end card (logo and "Requires GK2 Donkey API") as the last 3 seconds.

A change to the shot list means running the script again, not editing again.

### Order of work

1. The director's setup steps and spot saving (most of the time saved), and finding the HUD mode.
2. The key overlay.
3. The OBS and ffmpeg scripts.
4. Shoot by hand; script only the shots that keep failing.
