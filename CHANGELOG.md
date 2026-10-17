# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

Development:

- Investigation: can the mods bring art from outside the game (another animal, flames, fiery steps)?
  docs/external-assets.md, and tools/GK2AssetProbe (dev only, never shipped; Shift+F7) to answer it in game.
- Design draft for "Looks" (Hellfire, skins in an open folder format, sounds of a skin, an optional "Earn it"):
  docs/mount-looks-design.md. Not built.
- tools/skin_recipes.py (with tools/pngcolors.py): prototype "recipe" skins made from the donkey's own frames, for
  judging in game with the asset probe's test skins (local previews, never shipped).
- tools/skin_preview_html.py: a local HTML page animating every test skin next to the plain donkey (every clip, both
  facings, day or night); it embeds the game's frames, so it stays on the dev machine.
- The asset probe 0.2.0: the window in tabs (Skins, Fire, My files, Capture, Game, Old tests), and a Capture tab that
  films the donkey and its rider for 4 s, played on the preview page.
- tools/creature_sprites.py: a giant worm and a giant snail drawn by code (our own art, no game pixels), a picture for
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
