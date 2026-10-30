# GK2 Saddle Up 0.13.1: the game 1.011 build

For players. GK2 Donkey API 0.41.1 is in the download. Nothing new to play with: this is 0.13.0 rebuilt for the
game's 1.011 update (Steam build 26297960, 2026-10-30).

- **If you updated the game to 1.011, you need this one.** 0.13.0 checks the game's code it depends on at start and
  switched itself off on 1.011 ("wait for a re-audited build" in the log, the Mods page said so too). 0.41.1 was
  audited against 1.011: 11 of the 839 game members it uses changed, none went missing, nothing in the mod had to
  change, and this build carries the 1.011 check.
- Everything you set carries over. The packs (Dragon, Critters, Donkey Skins 1.0.0) stay as they are and need no
  update.
- The game now ships BepInEx itself (5.4.23.5, the version these mods were built with), so a fresh Steam install
  needs no BepInEx step any more: extract the zip into the game folder and start.
- Not this mod, but good to know: 1.011 has a Move row in every build desk, so the separate Move Building mod is
  retired; delete its DLL if you had it.
