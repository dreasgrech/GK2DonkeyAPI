# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

Nothing yet.

## 0.9.1 (GK2 Donkey API 0.29.1), 2026-10-16

For players:

- **Smoother frames while playing.** The Mods menu's donkey picture and its lists looked for the Mods window four times
  a second while you played, even with the menu closed: each look took 1-2 ms, so a frame of 2-3 ms extra four times a
  second. It now looks only at the main menu or while the game is paused (the only times the window can be open).
- **No memory held by parked carts.** A cart left standing (H while driving) and hitched again was kept in memory
  afterwards (about 50 objects each, up to about 30 carts). It is now let go at once.

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
