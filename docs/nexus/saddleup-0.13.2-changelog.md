# GK2 Saddle Up 0.13.2: sounds, the cart text, and the game's Mods window

For players. GK2 Donkey API 0.41.2 is in the download. Built for game 1.011 (Steam build 26297960).

- **Mount and dismount sounds play whole.** The rat's and the snail's squeaks were cut off (they inherited the donkey's
  0.42 s start offset and are shorter than that), and the dragon's growl lost its first half second. A mount's own
  clips now start from their beginning.
- **The "Allow mounting with the cart indoors" setting** says what really happens: the cart has no collider indoors,
  so it rolls through furniture and door frames instead of getting stuck. It only looks odd.
- **Two ways to install.** Extract the zip into the game folder as before, or into the game's own mod folder
  `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\GK2SaddleUp`: the main menu's Mods window
  then lists GK2 Saddle Up with its version and an Enable/Disable switch, and the game copies it into BepInEx\plugins by
  itself. One place or the other, never both.
- Your settings and saves carry over. The Donkey Skin Pack 1.0.0 needs no update; the Dragons Mount Pack has a 1.0.1
  (its name in the log and the mod lists is now "GK2 Saddle Up: Dragons Mount Pack"; nothing else changed).
