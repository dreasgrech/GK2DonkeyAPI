# The game's Mods window and the Steam Workshop

What game 1.011 (Steam build 26297960, 2026-10-30) does with code mods, why our mods are not in its Mods
window, the two ways in, and what publishing on the Steam Workshop would take. Read from the decompiled
1.011 classes `CodeModHost`, `CodeModLayout`, `BepInExModDetector`, `BepInExBridge`, `WorkshopSyncCore`,
`UICodeModsWindow`, `SteamWorkshopCreatorService` and the shipped `README_MODDING.txt` (the game's code-mod
contract, version 1). Nothing in this report changed any code; the decisions at the end are the user's.
Covers both repos (the QOL repo has a pointer, `docs/mods-window-and-workshop.md` there).

## 1. What the window shows and where the list comes from

The Mods window (main menu, the round "Mods" button; `UIMainMenuWindow.OnModsButtonClicked` opens
`UICodeModsWindow`) lists `CodeModHost.Records`. Records come from exactly two scans (`CodeModHost.Scan`):

- **Local packs**: every folder under `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\`
  (`ModsPaths.CodeRoot`; folders whose name starts with `~` are skipped). Id `local:<modinfo id or folder name>`.
- **Steam Workshop items** the player subscribes to (`steamapps\workshop\content\4358690\<item id>\`). Id = the
  item id; the title comes from Steam unless `modinfo.json` gives a `name`.

Loose DLLs in `<game>\BepInEx\plugins\` are **never scanned**: the game folder is a search root only for
language and voice-over packs (`ModsPaths.GetModSearchRoots` feeds those loaders; `CollectLocal` reads
`CodeRoot` alone). That is the screenshot: "BepInEx 5.4.23.5 found", "No code mods found", while eight of our
plugins are running. The "Game folder" source label in the window is for language packs, not code.

A code tile shows `<title>  v<version>` (version only from `modinfo.json`; the window never reads
`[BepInPlugin]`), a status line (`Enabled`, `Disabled`, `Restart required`, `Load error`, `BepInEx not found`,
`Managed by another loader`, `Missing dependency`), the kind (`BepInEx` or `Native`), and the buttons
**Enable/Disable** and, for Workshop items, **Workshop** (opens the item page). There is no settings button and
no hook for one: `CreateCodeTile` builds the two buttons and nothing else, no event, no registry. Our settings
stay where they are (the pause-menu Mods page through `FrameworkLink`); the two windows do different jobs.

## 2. How a pack becomes a running plugin

`CodeModLayout.Inspect` on each pack folder: `modinfo.json` first (`bepinexPlugin` or `bepinexRoot` make it a
BepInEx mod; `assemblies`/`entryType`/`Assemblies\` a native one), else `FindMirror` looks for a `BepInEx\plugins`
folder holding a DLL at the pack root or one level down, else `FindLoosePlugin` scans every DLL for the strings
`BepInEx` + `BepInPlugin`. Our release zips extracted into a pack folder are the second case
(`BepInEx/plugins/<Mod>/<Mod>.dll` at the root), so **a zip of ours dropped into `Mods\Code\<Name>\` is already
a valid pack without any change**; it would only show the folder name and no version until it has a
`modinfo.json`.

Enabled packs are copied by `WorkshopSyncCore.Apply`: the pack's `BepInEx\` tree over `<game>\BepInEx\`
(skipping `core/`, `cache/`, `patchers/`, `*.log`, `config/BepInEx.cfg`; `config/` files only when the player
does not have them yet). Every copied file is recorded in `BepInEx\GK2.WorkshopSync\state.json` with the
source's size and write time; a later run re-copies only changed sources. **Disable, unsubscribe or delete
the pack folder → the recorded files are deleted** (config files kept). Two moments run it:

- the game's own scan at startup and when Workshop items change (`CodeModHost.RunStartup/RunWorkshop` →
  `SyncBepInEx`); a pack added while the game runs is copied right away and the tile says "Restart required";
- the BepInEx patcher `BepInEx\patchers\GK2.WorkshopSync\GK2.WorkshopSync.dll` at preload, before any plugin
  loads (`WorkshopSyncEntry.Run`: `ReadSources` + `FindNewMods` over the Workshop folder and `Mods\Code`, then
  `Apply`). So a pack dropped in while the game is closed runs on the very next launch.

Local packs work in the GOG and Epic builds too (the README says so; the code has no Steam dependency on that
path), so this is the one way to the Mods window for every store.

## 3. Our mods fit as they are

Nothing in our code assumes where the DLL sits:

- all content is embedded (`Looks.RegisterEmbedded` in Saddle Up and the three packs; the cargo tables as
  manifest resources);
- the API's scan for folder packs uses `BepInEx.Paths.PluginPath`, the sound cache and framing cache write to
  `BepInEx\cache\GK2DonkeyAPI\`, settings go through `Config.Bind` and `Paths.ConfigPath`: none of it is under
  `plugins\`, which the contract says a mod must not write into;
- the copy lands on the same paths the zips use today (`plugins\GK2SaddleUp\GK2SaddleUp.dll`, ...), so the API,
  Saddle Up and the packs find each other exactly as with a loose install, and `[BepInDependency]` is by GUID
  anyway;
- the dependency check (`gk2deps.txt`) is embedded and unaffected.

Two things the official window cannot do for us, stated so nobody expects them: it cannot show our own status
(a mod that stopped itself on a changed game build still reads "Enabled"; our log line and our Mods page say
so), and it cannot open our settings.

## 4. Mixed installs (Nexus loose DLL plus a pack)

- Pack and loose copy on the **same path** (our zips' subfolder layout): the sync overwrites the loose file
  (`File.Copy(..., overwrite: true)`) and from then on owns it; disabling the pack deletes it. No duplicate.
- Loose copy on **another path** (an old flat install, a hand-renamed file): two plugins with one GUID; BepInEx
  5.4.23 loads the higher version and logs `Skipping [X] because a newer version exists (Y)` for the other;
  equal versions: one is skipped with the same line. Not fatal, but a warning, so the page text should tell
  players switching to a pack or the Workshop to delete the loose DLL.
- On this dev machine the deploy from `dotnet build` writes the loose files; a pack of the same mod would own
  the same path, and **Disable in the window deletes the deployed build** (a rebuild puts it back; the state
  record is gone by then so it is not deleted again). Test with Enable only.

## 5. Way in, no Workshop needed: `modinfo.json` in the zips

Add to each release zip a `modinfo.json` at the root:

```json
{
  "schemaVersion": 1,
  "id": "dreas.gk2.saddleup",
  "name": "GK2 Saddle Up",
  "version": "0.13.2"
}
```

`id`: the plugin GUID (the only rule is lowercase, no spaces; the local id becomes `local:dreas.gk2.saddleup`).
`name` and `version` are what the tile shows; `version` must mirror `Plugin.cs`, so `tools/make-release-zip.py`
generates the file from the same Plugin.cs = csproj = changelog check it already does (both repos). The
Saddle Up zip carries the API and Saddle Up as one pack: one `modinfo.json` named for Saddle Up, two plugin
folders; `SyncTree` copies the whole `BepInEx\` tree.

Install texts then offer two places for the same zip:

- the game folder, as today (the stray `modinfo.json` next to the exe is read by nothing; cosmetic only);
- `Mods\Code\<Name>\`: the mod appears in the Mods window with its name and version and an Enable/Disable
  button, starts on the next launch, and works on GOG and Epic.

Whether the Nexus text makes `Mods\Code` the primary instruction (one line for every store, the window as the
on/off switch) or an alternative is the user's call. Either way the loose-DLL warning of section 4 goes in.

## 6. Way in with distribution: the Steam Workshop

Same layout per item (`modinfo.json` + `BepInEx\plugins\<Mod>\<Mod>.dll`, optional `BepInEx\config\<guid>.cfg`
defaults). What the Workshop adds: subscribe-and-play (the game installs, updates, enables and removes the
files), automatic updates (changed files are copied before plugins load; relevant every time the game updates
and we re-audit), the Workshop button on the tile, and Required-item links.

Items, one per shipped zip:

| Item | Content | Required items |
|---|---|---|
| GK2 Saddle Up | `GK2DonkeyAPI` + `GK2SaddleUp` plugin folders | none |
| GK2 Saddle Up: Dragon | the pack DLL alone | GK2 Saddle Up |
| GK2 Saddle Up: Donkey Skins | the pack DLL alone | GK2 Saddle Up |
| GK2 Saddle Up: Critters (when released) | the pack DLL alone | GK2 Saddle Up |
| GK2 Tech Tree Search, Owned Count, Built Count | the DLL | none |

Rules from the contract that matter to us:

- **Packs must not bundle the API.** Items sharing a file path share the file: unsubscribing one deletes it
  for the other. The packs already have `[BepInDependency("dreas.gk2.donkeyapi", "0.40.0")]`, so BepInEx
  refuses them without the API; the Workshop page's Required item is set by hand in Owner Controls.
- Open question (layering rule): the API as its **own** item, so another author's mount pack can require the
  API without Saddle Up. The zips ship them together today; nothing technical stops a split later (the API
  item would own `plugins\GK2DonkeyAPI\`, the Saddle Up item `plugins\GK2SaddleUp\`).
- Steam Deck / Linux players need the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%` (page text).
- No installers, archives or loaders inside an item; we ship none.

Publishing is built into the game: set `"workshopCreatorMode": true` in
`%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\workshop.json`, press Shift+F11 in the
main menu, "Create new workshop item" with Name, Type **Mod** and the linked folder (any folder; it is staged
and uploaded, files starting with `_` and `Thumbnail.*` are not uploaded, `Thumbnail.png` becomes the preview,
`_description.bbcode` the description, `_changenote.txt` the change note). New items are created **Unlisted**
(the visibility is changed on the Steam page); later uploads keep the visibility. The creator keeps its list
in `SteamWorkshop\workshop_items.json` in the save data folder (id, title, tag, linked folder), so the linked
folders should be stable paths, e.g. a `workshop\<Item>\` folder per item that the release tooling fills.
`Type = Mod` warns if the folder has no `modinfo.json` / `BepInEx\`. The game dev's note in the code: an
`InvalidParam` result on upload means the Steamworks tag setup on their side; only a real upload shows it.

A Nexus player who subscribes later: the item lands on the same paths and takes the files over (section 4).

## 7. Status of our tooling for this (this repo; the QOL mods wait, the user, 2026-10-30)

Built 2026-10-30 after the lab run (section 9), bundle decided (section 10):

- `tools/make-release-zip.py`: every item (Saddle Up with the API, each pack) is one folder, `modinfo.json` (id = the
  plugin GUID, name, version from Plugin.cs) + `BepInEx/plugins/<Mod>/<Mod>.dll`; zipped for Nexus, staged as
  `workshop/<Item>/` for the Workshop with `Thumbnail.jpg`, `_description.bbcode` and `_changenote.txt`; the
  zip's `modinfo.json` is read back and checked against the plugin; `--only saddleup` for a release that leaves the
  packs' public numbers alone, and **it refuses to overwrite a zip that exists in `release/`** (`--overwrite` for one
  never uploaded). That guard exists because the first run overwrote the public 0.13.1 zip in place (uploaded that
  morning, which nobody had said) and the rebuilt file was later deleted; the zip was regenerated with the tool as it
  was at the upload and compared with the Nexus download: byte-identical (sha256 equal, 886,911 bytes). The rebuilt one served the rehearsal of section 11 only.
  The first public zip with `modinfo.json` is the next release's (0.13.2 / API 0.41.2), and the Nexus Install text of
  section 5 goes live with that upload (a 0.13.1 zip in `Mods\Code` is listed by folder name, without a version).
- `tools/workshop-thumbnail.py`: the preview pictures, `media/workshop/<Item>/Thumbnail.jpg` (under 1 MB).
- `media/*_Workshop.txt`: the three items' descriptions in Steam's BBCode (h1-h3, b, i, url, img, olist, hr; a
  bare YouTube link makes the widget), each with a subscription Install section, the Deck launch option and the
  "installed from Nexus before" line; `{{ WORKSHOP_URL_* }}` placeholders until the items exist (the tool warns).
- Texts: the Nexus Install sections (Saddle Up and the two released packs), the READMEs, the checklist (§3, §6),
  the 0.13.1 Nexus changelog, CHANGELOG.
- Not done: the Critters pack has no page text yet (its staging folder gets no description, the tool says so); the
  `[img]` tag on Workshop descriptions is not in Steam's formatting help, the first Unlisted upload shows whether
  the pictures render; the pack zips keep their public 1.0.0 content until their next version.

## 8. Trying it on this machine

A test pack is prepared in the scratchpad (`...\scratchpad\mods-window\GK2TechTreeSearch\`: `modinfo.json`
plus the **deployed** Tech Tree Search build in the flat layout `BepInEx\plugins\GK2TechTreeSearch.dll`, the
path the deploy uses here, so the sync's first copy is a byte-identical no-op). Copy that folder into
`Mods\Code\` while the game is closed, launch, open Mods: the tile should read
"GK2 Tech Tree Search  v1.0.1 · Enabled · BepInEx". Player.log gets `[Mods]` lines, `LogOutput.log` the
`[GK2.WorkshopSync]` copy line. Do not press Disable on this machine (section 4); remove the pack folder
afterwards and rebuild Tech Tree Search to put the loose file back.

## 9. Lab run: the game's sync library on our real zips (2026-10-30)

The copy/remove logic ships as `GraveyardKeeper2_Data\StreamingAssets\ModdingTools\BepInEx\GK2.WorkshopSync.Core.dll`
(net472, no Unity reference; the preload patcher loads exactly this file). The session scratchpad's
`mods-window\lab\run.ps1` loads it with `Add-Type` and runs `LocalModsDir`, `FindNewMods` and `Apply` against a
fake `steamapps\common\GK2` game folder, a fake `LocalLow` and a fake `steamapps\workshop\content\4358690`,
fed with the real `GK2-Saddle-Up-0.13.1.zip`, `GK2-Tech-Tree-Search-1.0.1.zip` and the dist Dragon pack DLL.
Nothing real was touched. Results:

| Scenario | Result |
|---|---|
| A. Zips extracted into `Mods\Code\<Name>\` (one with `modinfo.json`, one without, one DLL-only pack) | All three detected as BepInEx packs (`bepinexRoot = BepInEx`). Ids: `local:dreas.gk2.saddleup` (from `modinfo.json`), `local:GK2TechTreeSearch` and `local:Dragon` (folder names). Four files copied to the exact paths the zips use (`plugins\GK2DonkeyAPI\GK2DonkeyAPI.dll`, `plugins\GK2SaddleUp\GK2SaddleUp.dll`, ...). |
| B. Next launch, nothing changed | 0 files touched (size + write time match). |
| C. A loose Nexus-style file already on the pack's path with other bytes | Overwritten by the pack's DLL on the first run; the pack owns the path from then on. |
| D. A pack disabled / its folder removed | Its DLL deleted from the game (config untouched). |
| E. The same mod as a Workshop item and as a local pack (same paths), then the local pack removed | Both ran fine together. Removing the local pack **deleted the files although the Workshop item still ships them**, and the next run copied nothing back: the Workshop item's state record (size + time) still matched, so the mod stayed missing until that item's next upload. |
| F. A `modinfo.json` next to the game exe (a game-folder extract of a `modinfo`-carrying zip) | Never read; no source, no effect. |

E is the one mechanism that can bite: the state is keyed per item and path, deletions run after copies, and a
path two items share is deleted when either goes. It makes two things true:

- a player must never have the same mod as a local pack **and** a Workshop item (page text: "if you switch to
  the Workshop, delete the folder in `Mods\Code` first, then subscribe");
- items must not overlap in files, and that includes **our own future split**: if the Saddle Up item ships the
  API and an API-only item appears later, every player who has both loses the API on the next removal, and
  moving the API out of the Saddle Up item later deletes it for players until the API item re-uploads. So
  bundle-or-split is decided once, before the first upload (section 10).

The Steam side is live: the Graveyard Keeper 2 Workshop already lists code mods (Vanilla+, No More Running
Back, a "GK2 Move Buildings", Recipe Pin, Shared Storage, Better Item Stacks, an Unofficial Patch, and a
third-party "Workshop Loader" that 1.011 now flags as "Managed by another loader"), mostly mis-tagged as
Translation; the `Mod` tag exists (one item under it). Nexus stays the only channel for GOG/Epic players and
for anyone who refuses the Workshop.

## 10. One artifact or two: what the evidence says

- **The Workshop takes a folder, Nexus a zip; the content is the same bytes.** Scenario E ran the Nexus zip's
  extracted contents as Workshop item content unchanged. So the release tool builds one folder per mod
  (`modinfo.json` + `BepInEx\plugins\<Mod>\<Mod>.dll`), zips it for Nexus, and the same folder is the Workshop
  creator's linked folder. No second build, no second variant; the only Workshop-only files are
  `Thumbnail.png`, `_description.bbcode`, `_changenote.txt`, which the uploader skips (section 6).
- **`modinfo.json` in the zip costs nothing on the game-folder path** (F) and is what makes the tile show the
  name and version on the pack path (A).
- **Bundle or split, once.** Two consistent choices; the mixed one is the trap (E):
  - *Bundle*: the Saddle Up item/zip carries the API, as today; a third-party pack requires "GK2 Saddle Up".
    One download for players; the API cannot be had without Saddle Up on the Workshop.
  - *Split*: API its own item/zip, Saddle Up and every pack require it. Two downloads on Nexus (Nexus has a
    Requirements field for exactly this); the API is requirable alone, which is what the layering rule
    ([API knows nothing of Saddle Up]) would suggest if other authors ever build on it.
  - *Never*: bundled on Nexus, split on the Workshop, or the other way round.
- **Install line for Nexus** after this: `Mods\Code\<Name>\` is the universal one (every store, the Mods
  window, Enable/Disable); the game-folder extract keeps working and is the fallback. Page text additions:
  delete the `Mods\Code` folder before subscribing on the Workshop; Steam Deck launch option; delete old
  loose DLLs on another path.
- **What stays unverified without a GOG/Epic build**: that those builds ship BepInEx too (the changelog says
  "BepInEx now ships with the game", the README "the Windows game"); the `Mods\Code` path itself is store
  independent in the code.

## 11. The rehearsal before the first upload: the Workshop content as a local pack

The Workshop item's content runs on this machine without Steam, through the same patcher, copy, Mods window tile
and Enable/Disable as a subscribed item: `workshop\GK2SaddleUp\` copied into `Mods\Code\GK2SaddleUp\` in the save
data folder (the three uploader files ride along and are ignored). `tools/rehearsal.sh stage-pack` and
`restore-pack` do the file moves; the game stays closed for them.

1. **Stage** (`bash tools/rehearsal.sh stage-pack`): the deployed `plugins\GK2DonkeyAPI` and `plugins\GK2SaddleUp`
   go aside into `BepInEx\pack-aside-GK2SaddleUp\`, so the game's copy is the only one; the pack folder is copied in.
   The pack DLLs, the dev plugins, the cfgs and the dev marker stay as they are.
2. **Launch, main menu, Mods.** Expected: the tile "GK2 Saddle Up  v0.13.1", status Enabled, kind BepInEx, a Disable
   button and no Workshop button (a local pack). `BepInEx\LogOutput.log`: two `[GK2.WorkshopSync] Copied
   plugins/...` lines before the plugins load, then `GK2 Donkey API 0.41.1 loaded` and Saddle Up's line as always;
   Player.log: `[Mods] Enabled new code mod 'GK2 Saddle Up' (local:dreas.gk2.saddleup)`. Load a save, ride, drive:
   the packs still work next to it. No warning or error.
3. **Disable** in the window, quit, launch again. Expected: the tile says Disabled; the two folders are gone from
   `plugins\` (`[GK2.WorkshopSync] Removed` lines); the game runs without the mod and the save loads; the packs
   log that the API is missing (BepInEx skips them: their hard dependency). **Enable**, quit, launch: everything
   back, `Copied` lines again.
4. **Restore** (`bash tools/rehearsal.sh restore-pack`, twice): the first run removes the pack folder and asks for one
   launch, in which the patcher deletes the files it copied and drops them from its state; the second run puts the
   dev builds back. The order matters: dev builds put back before that launch are deleted by it (the paths are
   recorded as the pack's). `Mods\code_mods.json` keeps the decision for `local:dreas.gk2.saddleup`, harmless.

What it proves: the content the Workshop would deliver works end to end here, the tile reads our `modinfo.json`,
the switch behaves, and the Nexus `Mods\Code` install line is true. What only Steam shows afterwards: the Mod tag,
the preview picture, and whether `[img]` renders in the description.

**Run 2026-10-30 (the 0.13.1 content, the user at the keyboard).** Launch 1: the patcher's `Found new BepInEx mod
local:dreas.gk2.saddleup` and two `Copied` lines before the plugins, the tile "GK2 Saddle Up  v0.13.1 · Enabled ·
BepInEx" with Disable and no Workshop button, Player.log `[Mods] Enabled new code mod 'GK2 Saddle Up'`; API 0.41.1
and Saddle Up 0.13.1 loaded from the game's copies with Gerry and the three packs; a save loaded with its donkey and
cart restored, the snail ridden and its cart left; 0 warnings or errors in 853 lines. (Player.log had one Unity
"invalid seek position" line at the snail's mount sound: the 0.41.1 bytes predate the sound fix of the same day,
which ships in 0.41.2; it proved the game's copy was the one running.) **Disable** while the game ran: both DLLs
deleted at once ("BepInEx mods changed (2 files); they apply after a restart"), `state.json` emptied,
`code_mods.json` `approved: false`, the id kept in `sources.json`'s known list. Launch 2: the tile Disabled, neither
plugin loaded, BepInEx's five `Could not load ... missing dependencies: dreas.gk2.donkeyapi` lines for the packs
and the two dev tools (their hard dependency; what a subscriber without Saddle Up would see), nothing else.
**Enable**, launch 3: the files copied back at the click, the tile Enabled, everything loaded as in launch 1, 0
warnings in 317 lines. Then `restore-pack` twice (one launch between) and the dev builds back: that launch logged
`BepInEx folder missing for local:dreas.gk2.saddleup` and the two `Removed` lines, the state emptied; the patcher
deletes files only and leaves `plugins\GK2DonkeyAPI\` and `plugins\GK2SaddleUp\` behind empty (a subscriber who
unsubscribes keeps two empty folders, harmless), which `restore-pack` now removes before it puts the dev builds back.

## 12. The first Workshop item (2026-10-30)

Created from the game's creator (Shift+F11, Type Mod, linked folder `workshop\GK2SaddleUp`) as "GK2 Saddle Up - A
Donkey Mount", id 3830136851, https://steamcommunity.com/sharedfiles/filedetails/?id=3830136851, Unlisted. Create
uploaded the content in the same step: the Mod tag was accepted (no `InvalidParam`), 2.7 MB, the preview picture, one
change note, and the description's `[img]` pictures render (not in Steam's formatting help, now seen). A bare YouTube
URL rendered as plain text, not a player: the texts carry it as a link now, and the video goes into the item's media
through Owner Controls, "Add/edit images & videos". Steam's "awaiting analysis" banner is its routine scan of a new
item. The page title is Steam's; the Mods window tile takes its name from `modinfo.json` ("GK2 Saddle Up").

**Subscription run** (the dev builds aside with `tools/rehearsal.sh stage-aside`, subscribed with the game closed, then
launched): Steam placed `steamapps\workshop\content\4358690\3830136851\` with `modinfo.json` and the two DLL folders,
nothing else; the patcher logged `Found new BepInEx mod 3830136851` and two `Copied` lines before the plugins, the
copies byte-identical to the download; API 0.41.1 and Saddle Up 0.13.1 loaded, 0 warnings in 782 lines; the tile
"GK2 Saddle Up  v0.13.1 · Enabled · BepInEx" with Disable and Workshop buttons; `state.json` records the two paths
under the Steam id, `sources.json` the Workshop folder, `code_mods.json` the item approved next to the earlier
`local:` entry. The framework's Mods menu (a plugin list) shows it as well. Still to do: the two pack items, their
Required item, the link placeholders, the dev builds back (`restore-pack` after unsubscribing and one launch).

## 13. The install texts, decided (2026-10-30)

After re-reading README_MODDING.txt with the user: section 6 calls `Mods\Code` "Testing locally", and two install
places invite the doubled install of section 4. So the player texts say: **on Steam, subscribe on the Workshop** (the
README's "supported way": installed, updated and listed by the game), **the Nexus download installs into the game
folder as always** (any store), and one sentence that the game's Mods window lists only Workshop subscriptions, so a
Nexus install not being there is normal. `Mods\Code` stays our rehearsal tool (`rehearsal.sh stage-pack`), out of
every player text. The Workshop pages say "A BepInEx plugin" (README section 5). The release notes:
`docs/nexus/saddleup-0.13.2-changelog.md`, `docs/nexus/dragons-mount-pack-1.0.1-changelog.md` (also the Workshop change
note of the pack: `make-release-zip.py` prefers `docs/nexus/<pack-name>-<version>-changelog.md`).

Revised the same evening (the user: "Why are we linking nexus -> steam? I dont think we should do that"): the
Nexus texts and release notes carry **no link to Steam and no Workshop promotion**; they keep one neutral sentence
that the game's Mods window lists only what the game installed itself from the Steam Workshop, so a Nexus install
missing there is normal. The Workshop change notes speak of the Workshop through notes of their own,
`docs/nexus/<name>-<version>-workshop-changenote.txt`, which `make-release-zip.py` prefers over the Nexus note.
