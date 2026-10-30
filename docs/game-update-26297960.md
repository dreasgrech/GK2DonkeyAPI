# Game update 1.011 (Steam build 26297960): re-audit

On 2026-10-30 the game was updated on Steam from 25910882 (1.010) to 26297960, the version the changelog calls
1.011. The game folder's git has the update committed ("Updated game to build 26297960"); the previous DLLs came
from `HEAD~1` ("before game update") for the "old" side of the diff. The mods were audited against the new build
before the game was started with it. The Steam changelog the user saved is kept beside the decompile
(`changelog_1011.txt` in the job's temp folder).

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `5ec8094e-4c33-4615-9eb8-011501193186` | `36326881-56e1-4dee-a10f-2893840f0687` |
| `LazyBearTechnology` MVID | `6cd86a0b-b1ed-40ba-88b0-a9313146514b` | `46a6f4cd-56c5-410d-993a-de26a488dc60` |
| Save version | 1010 | 1011 (`SaveCodeFixes.Apply` gained a `< 1011` block of ten fixers) |
| BepInEx | 5.4.23.5 installed by us | the same files, now shipped by the game (below) |

- **`tests/GameDiff`:** `Assembly-CSharp` 43,415 → 44,935 members; 783 removed (86 not compiler-generated:
  `RemovePointer`'s whole private surface, `ResolutionConfig`'s fake-resolution fields, `CrematoriumInteractionHandler`'s
  overhead-corpse helpers, `WorldZoneData.GetOrderForCaretaker(Item)`, `WgoData.DropStoredTechPoints()`,
  `CraftComponent.TryContinueFromQueue` / `TryStartCurCraft`, `ZombieWgoData.CrafterStartCraftActivity` /
  `SpillPorterInventory`, `ConveyorPickupOrder.TargetWgoData`), 2,303 added, 692 changed. `LazyBearTechnology`:
  2 removed (compiler-generated), 19 added (`VoiceOverModLoader.PackInfo`, `OdinBinaryFileSerializer`'s payload
  checks, two `GameKey`s), 7 changed.
- **213 decompiled types differ, 49 are new.** The new ones: the game's own **building move** (`BuildMoveData`,
  `BuildMoveHelper`, `BuildMoveGroup`, `BuildMoveMember`, `BuildMovePlacement`, `BuildMoveZombie`,
  `BuildMoveExtensionLink`, `BuildObjectSelectionPointer`, `MoveSelectPointer`, `MovePlacePointer`,
  `MoveConveyorPlacePointer`), the **code-mod loader** (`LazyBearTechnology.CodeModHost`, `CodeModLoader`,
  `CodeModLayout`, `CodeModRecord`, `CodeModStatus`, `CodeModKind`, `CodeModLoadPhase`, `CodeModLoc`,
  `CodeModApprovals`, `ModInfo`, `ModsFeatures`, `UICodeModsWindow`, `UIModsAttentionBadge`, `BepInExBridge`,
  `BepInExModDetector`, `WorkshopSyncCore`, `WorkshopSyncSource`, `WorkshopSyncFileRecord`), zombie assistants at
  autocrafts (`AutoCrafterOrderPlacer`, `StartCraftOrder`, `FinishCraftOrder`, `IZombieOrderExecutor`,
  `CraftQueueOutputIndicator`), the Halloween event (`Flow_IsHalloweenEvent`, `HalloweenConditionalLoadContentPart`,
  `HalloweenHeadSkinOverride`, `TemporaryEventDates`, `ConditionalLoadContentPart`), `CrematoriumCremation`,
  `WgoPartPersistentAnimationTriggers`, `MainThreadWatchdog`, `FightPerfDebug`, the vendor-orders world widget and the
  map's day-activity and fight icons.
- The 1.011 decompile is `decomp_26297960` / `lbt_26297960` in the job's temp folder
  (`C:\Users\User\.claude\jobs\3d9fcfd0\tmp`), the 1.010 one `decomp_1010` / `lbt_1010` beside the earlier job's
  (ilspycmd 8.2 with `DOTNET_ROLL_FORWARD=Major`). The WorkshopSync patcher and its core are decompiled in `wsync`.

## Patch targets and references

- **Every project compiles** against the new DLLs (the API, Saddle Up, the three packs, the five tools, the three
  examples; in the QOL repo four of the five mods, not Move Building: that repo's report).
- **`DepManifest verify` on the deployed `GK2DonkeyAPI.dll`:** 839 entries, 11 changed, none missing. The changed
  members and the classes that use them, read in the decompile:
  - `PowerSourceInteractionHandler.Interact` / `HasInteraction` / `FormInteractionInfo` and
    `ZombieCarrierInteractionHandler.Interact` (`ZombieStations_TakeBack_Patch`): the power source refuses a zombie
    while it has a workbench extension; the carrier attaches a zombie with `AttachToQuarryWgoData` and no longer
    starts the craft itself. Our prefix and finalizer only count the station call (`HeadReroute.InZombieStation`);
    nothing of the body is read.
  - `PlayerInputHandler.UpdateInput` (`PlayerInputHandler_UpdateInput_Patch`, `Access`): the melee and ranged attack
    branches ask the new `PlayerController.IsWeaponAttackAllowed` (no sword in the final cutscene). Our prefix runs
    before it and reads the handler's fields (`playerController`, `curInteractionHandler`), which are unchanged.
  - `Wgo.CompleteVisualPartsLoad` (`Wgo_CompleteVisualPartsLoad_Patch`): a view lifted for a move is not shown. Our
    postfix prepares the donkey's parts after the game's own work; a donkey is never lifted (not buildable).
  - `WgoData.set_WorldZoneData` (`ChestMoves`): the autocrafter order placer re-evaluates its craft when the zone
    changes. We set the hidden chest's zone to null while it rides; no crafter sits on it.
  - `ZombieWgoData..ctor` (`SelfTest` only): the quarryman and gardener-cargo fields.
  - `CameraSystem.WorldToScreenPoint` (`Mount`, `TuningWindow`, `SelfTest`): the point is rounded to whole screen
    pixels, mapped through the upscaled render target when the ultrawide upscale is on, and gets a 0.25 px bias.
    Used for the hint bubbles over standing donkeys and the dev panel's markers: a sub-pixel shift at most.
  - `OdinBinaryFileSerializer.Serialize` / `Deserialize` (`SelfTest` only): an out-of-memory during serialisation is
    now an error instead of a silent short payload; a payload of 5 bytes or less is refused; the `.new` file's
    length is checked and a missing save is restored from the first backup. Our side file is written by our own
    code; the self-test uses the game's serializer to read a save copy, and reads it the same way.
- `tests/CecilScan-DonkeyAPI`, `ApiSurface`, `DefaultsScan`, `SkinsScan`, `FrameworkScan`, `NoLinqScan`, the
  canonical cfg check, every `check_skin`, the PackerScans: pass (the release scans' log of this audit is
  `api-scans-1011.log` in the job's temp folder).
- The other 828 game members the API uses are byte-identical in IL.

## What the deployed DLLs did before this audit's rebuild

- **GK2DonkeyAPI.dll stopped itself at start**: three of the changed members belong to `core` classes
  (`ZombieStations_TakeBack_Patch`, `PlayerInputHandler_UpdateInput_Patch`, `Wgo_CompleteVisualPartsLoad_Patch`),
  so `GameCode` gave the "wait for a re-audited build" verdict; Saddle Up and the packs then had no API. **Every
  Nexus player on 1.011 is in that state until 0.41.x / Saddle Up 0.13.x are re-released from this audit.**
- The rebuild of this audit embeds the new manifest (the hashes of 1.011), so the rebuilt DLL reads "identical" and
  runs; nothing in the code had to change.

## Breaks found

None in this repo. The build pin moved in `tools/GK2DonkeyProbe/Plugin.cs` (a plain pin by design), the audited
build in the READMEs and the release checklist.

## Changes near the mods, not breaks

- **BepInEx shipped by the game.** The changelog: "BepInEx now ships with the game, so players can install and share
  mods and the game loads them automatically". The game's depot carries BepInEx 5.4.23.5 with Doorstop 4.5.0,
  which is what this machine already had (file versions `BepInEx.dll` 5.4.23.5, `winhttp.dll` 4.5.0.0, both dated
  2026-09-23; git shows `BepInEx/core` and `winhttp.dll` unchanged by the update). What arrived: BepInEx's own
  `changelog.txt` and `ThirdPartyLicenses.txt` at the game root, `README_MODDING.txt` (the "code mod contract",
  244 lines), and a **preloader patcher `BepInEx\patchers\GK2.WorkshopSync\GK2.WorkshopSync.dll`** (6 KB) that loads
  `GraveyardKeeper2_Data\StreamingAssets\ModdingTools\BepInEx\GK2.WorkshopSync.Core.dll` and runs
  `WorkshopSyncEntry.Run` before any plugin: it reads `BepInEx\GK2.WorkshopSync\sources.json`, scans
  `steamapps\workshop\content\4358690\<item>\` and `%LocalAppData%Low\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\`
  for BepInEx-shaped items, copies their `BepInEx\` folders over the game's (never `core`, `cache`, `patchers`,
  `*.log`, `BepInEx.cfg`; `config\` files only when absent) and removes files it copied earlier whose item is gone,
  by its own `state.json`. **Loose plugins it never copied are not touched**: our `BepInEx\plugins\GK2*` folders,
  the configs, the asset probe folders stay. The scan is skipped when a third-party Workshop loader sits in
  `patchers` (name markers `WorkshopLoader`, `GK2ModAutoUpdate`, `Timbn`, ...); the PerformanceHog patcher matches
  none. Expect `BepInEx\GK2.WorkshopSync\sources.json` and `state.json` after the first launch, and
  `[GK2.WorkshopSync]` lines on the console. `BepInExBridge.InstallStub` re-copies the patcher when it differs from
  the shipped one, `MoveNestedInstallUp` only moves a BepInEx that sits in a subfolder (ours does not).
- **The contract and our zips.** Section 2 of `README_MODDING.txt` is our layout exactly (`BepInEx\plugins\<Mod>\`,
  settings in `BepInEx\config`, dependencies by `[BepInDependency]`, netstandard2.0/net472, no BepInEx or Harmony
  shipped): a Workshop item would be the zip's content plus a `modinfo.json` (`schemaVersion`, `id`, `name`,
  `version`). The game then installs, updates and removes it for subscribers; the game's Mods window
  (`UICodeModsWindow`) lists Workshop and local code mods, not loose plugins. The Nexus pages' "install BepInEx 5
  first" step is now redundant for Steam players (the GOG and Epic builds are not covered by the changelog line).
  Both are the user's calls.
- **`ModLoaderDetection`** (new): the bug reporter records that a mod loader is present (`loader: ...` in
  `Player.log`) and lists the subscribed Workshop mods after the first scene. Informational.
- **The game's own building move** (`BuildingDef.BuildingMode.Move`): a Move row in every build desk tab
  (`BuildData.GetDataForMove`, not on fight builders), `MoveSelectPointer` picks a building whose `Wgo.IsBuildMovable`
  (not temp, not despawning, not already lifted, no destroying craft, custom tag not `alchemy_chest`, no
  `lock_building_move` game res, the balance's `moveMod` not `DoNotMove`), `BuildMoveHelper.TryLift` lifts it with
  its full-cover extensions, suspends its orders and multi-inventory, pauses zombies heading to it and hides the
  view (`Wgo.SetLiftedForMove`), `MovePlacePointer` places it and `TryPlace` relocates the **same `WgoData`**
  (`WorldData.RelocateWgoData`; `GameScene.HandleWgoRelocated` moves the view), relinks extensions and conveyors
  and resumes the zombies. A `WorldZoneData.IsOrderSuspended` / `SetMultiInventorySuspended` pair keeps lifted
  buildings out of orders and crafting. Our donkeys and carts are temp objects and never buildable: nothing of this
  meets them. (For the QOL repo's Move Building mod it is the end: that repo's report.)
- **Overhead items.** `PlayerData.DropAllOverheadItems` (new) loops `while (HasOverheadItem) DropOverheadItem(...)`
  from the flow node `Flow_PlayerDropOverhead` with `dropAll` ("pray and sleep while carrying a body" fixed).
  `HasOverheadItem` and `overheadItem` read the real head list (unpatched); `DropOverheadItem` drops the item and
  calls `RemoveOverheadItem(item)`, whose prefix of ours lets the original run for an item that is on the head (it
  only intercepts cart copies). So each pass removes one real head item and the loop ends; a mounted keeper cannot
  pray or sleep anyway (the control-type loop ends the ride first). The game's own loop would spin on an *empty*
  carried item (dropping skips it), which 1.010's `RemoveEmptyOverheadItems` fixer removes at load and which our
  hand-backs never leave (`TryAddOverheadItemNoReplace`). No change.
- **Crematorium.** `CrematoriumInteractionHandler` lost its overhead-corpse helpers to the new `CrematoriumCremation`
  (zombie cremation finishes in `WgoData.OnCraftEnd`). The corpse feature hands the body over at the morgue door and
  never touches the crematorium's handler; an in-game check below.
- **Zombies.** New `ZombieType.Quarryman` (brings stone and marble to chests itself), the gardener carries a
  4-slot `GardenerCargo` instead of its body item, assistants start and finish autocrafts through
  `AutoCrafterOrderPlacer` (`StartCraftOrder` / `FinishCraftOrder`), a fighter released on pickup
  (`ReleaseFighterRole`). Zombies on the cart are store items, not scene zombies; `ZombieInteractionHandler`'s two
  patched methods are unchanged; the carrier handler change is read above.
- **Saves.** Saves older than 1011 get: carousels inside each other removed, vendor tier products topped up, the
  missing plague doctor and survey table restored, crafters migrated from order workbenches and quarries, garden
  items moved from bodies to the gardener cargo, the prayer writer's goal sum remapped, unstarted conveyor craft
  requirements refreshed, map day icons unlocked from passed quests, the excessive-zombie debuff refreshed after the
  sister's corpse. None touches a donkey, a cart, the side file or the hidden chest.
- **Camera.** Ultrawide support: `MainCamera` can render to an upscaled target (`ShouldUpscale`, when the pixel
  size is above 2 and the dev flag allows it), `CameraSystem.ResolutionPiexelSize` is then 2; `WorldToScreenPoint` /
  `ScreenPointToRay` map through it. `Mount.PixelSize()` mirrors `PlayerView`'s rounding by the render mode
  (`PlayerView` is unchanged); the preview stage and the colour previews render into their own textures. The
  look bar and the panel are screen-space UI through the game's canvas. An in-game look at the hint bubbles below.
- **`PlayerPhysicalBody`** keeps its contacts per physics step and requires ground under the feet for a supporting
  contact (the "stuck behind the bed / in clutter" fixes). Our safe-ground and flight patches use members the
  manifest reports unchanged; the ride moves the keeper through the game's own body.
- **`MainGame`** replays the after-sleep trigger on the next load instead of at once; `GoToMenu` is marked by the new
  `MainThreadWatchdog`. Not on our paths.
- **GK2 Mod Framework** (SuperMan4eg's, which draws our Mods-menu pages) is not ours to audit; `UIGamePauseWindow`
  only gained a gamepad re-init. The Mods menu is on the in-game list.

## In-game checks (pending)

1. Start the game: the API, Saddle Up and the packs "Active" (the rebuilt manifests identical), the four QOL mods
   loaded, Move Building "Off" with its missing-member message; `[GK2.WorkshopSync]` lines harmless; no
   `[Mods]` warning about our folders.
2. Load the 1.010 save: the fixers' lines; the kept donkeys, carts and skins back from the side file; a ride, a
   door, a save.
3. A corpse to the morgue door while mounted, and the crematorium unchanged for the keeper.
4. The hint bubbles over standing donkeys and carts, the look bar and the Mounts panel at the usual place (the
   rounded `WorldToScreenPoint`).
5. If the Halloween event is live (`TemporaryEventDates`): the keeper's head skin override against the dragon's front
   layer (the occlusion counts assume the normal head).
6. The Mods menu (the framework) still shows our pages.
7. Zombies on the cart taken from and returned to a power source with a workbench extension (the refused case).

## The gate itself

The dependency check did what it is for: before this audit the deployed API refused to run on 1.011 with the
"re-audited build" message and the QOL mods answered per mod (Global Storage warned and switched its extension off,
Move Building stopped on five missing members, the other three ran as identical). The rebuilt DLLs carry 1.011's
hashes.
