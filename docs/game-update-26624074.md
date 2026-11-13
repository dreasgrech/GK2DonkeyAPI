# Game update 1.012 (Steam build 26624074): re-audit

On 2026-11-13 the game was updated on Steam from 26404424 (1.011.1) to 26624074, which the game calls 1.012.7 (its
`PlayerSettings` version, the string a new save is stamped with; Steam's news post calls it 1.012). The game folder's
git has the update committed ("updating game to build 26624074"); the previous files came from `HEAD~1` for the "old"
side of the diff, and a copy of the previous `Managed` folder was kept aside at
`C:\ClaudeCodeTemp\GK2-prev-managed\26404424` before the update. The mods were audited against the new build before
the game was started with it. The Steam dev notes (the user pasted them) are kept with the audit's lists in the job's
temp folder (`audit_26624074\steam-notes-1.012.txt`).

**This update breaks every build of GK2 Donkey API made so far**, as 1.011 did: eight game members the API uses have
new code, and all eight are used by core classes, so the dependency gate disables the mod at start ("Mod is DISABLED;
the game runs unmodified"). Saddle Up and the packs follow the API. A re-release is needed (below). Of the QOL mods,
Owned Count runs with one warning line; the other three are identical.

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `7274b6c4-2447-4669-baff-1ced3407f34e` | `c69d845c-001c-4bd4-9adf-7b288a697087` |
| `LazyBearTechnology` MVID | `46a6f4cd-56c5-410d-993a-de26a488dc60` | `53b976b3-a36d-477a-bea4-e43c6634914e` (rebuilt this time) |
| `AstarPathfindingProject` MVID | `00e07f99-c81a-4f80-94f3-4235c88ed4df` | `dbd7899c-f996-4d27-931b-a86b6fb8e156` |
| Game and save version | 1.011.1 | 1.012.7 (`SaveCodeFixes.Apply` gained `< 1012` and `< 1012.2` blocks) |
| Unity, BepInEx | 6000.3.9f1, 5.4.23.5 | the same (nothing under `BepInEx\` or the game root changed) |
| `resources.assets` | 59.2 MB | 28.7 MB ("memory usage optimization": sprites moved into the addressable bundles) |

- **`tests/GameDiff`:** `Assembly-CSharp` 44,947 → 45,732 members; 687 removed (42 not compiler-generated), 1,472
  added, 403 changed. `LazyBearTechnology`: 12 removed (all compiler-generated), 31 added, 11 changed.
  `Assembly-CSharp-firstpass`, `AstarPathfindingProjectExamples` and `HBAO.Runtime` are recompiled with every member
  identical; `AstarPathfindingProject` has only Unity's generated property bags renamed again (38 removed, 38 added,
  `PropertyBagRegistry.Initialize` changed), no `Pathfinding` member.
- **The 42 removed named members** are the game's own dev windows (`Dev_ButtonsPressTestWindow`, `Dev_UIFramesWindow`,
  `UIDebug*`, replaced by `RuntimeDevApi`), `MovementComponent.DoPathCalculations` / `FindPathRecastGraph` /
  `TryMoveWithoutFullGDPath` (folded into `StartCalculatedPath` and graph anchors), `WgoPart.UpdateDropView(ItemDef,
  ...)` (now takes a count), `DropView.GetSpawnPosFromSample`, three `IgnoreChunkVisibility` getters,
  `AutomaticTurret`'s wait fields and `PanicReductionMachineInteractionHandler`'s craft field. None is used by the
  API, Saddle Up, the packs, the tools or the QOL mods (`DepManifest verify` lists no REMOVED member; the sources
  name none of them).
- **19 new types:** the PR machine rework (`PrMachineDef`, `UIPanicReductionMachineWindow(+Data)`), resting on the
  bench (`Flow_StartResting`, `UIRestingExitWidget(+Data)`), the map (`MapDayType`, `UIMapActivityIconHint`),
  mercenaries (`MercenaryPayments`), pooled drop views (`BigDropViewPools`, `SmallDropTextPool`), sprite atlases
  (`SpriteAtlasLateBinding`, `PaletteAtlas`, `AlchemyAtlasSprite`, `AlchemyWindowAtlas`, `UIItemCellOptionalBank`),
  `CombatPauseClock`, `FightEndingAnimation`, `RuntimeDevApi`; in LazyBear `DevMode`, `LazyAtlasTmpSpriteGroup`,
  `KeepStaticAcrossPlayModeAttribute`.
- **Art and data** (`tools/game-asset-diff.py`): 15 changed asset files with 2,948 objects differing in
  `resources.assets` and 1,938 in `sharedassets0.assets`, mostly sprites moved out of `resources.assets` (1,265 removed
  there, 579 added elsewhere) and 248 textures re-imported (zombie parts, graveyard decor, gamepad hints, the
  Japanese and Korean font pages). The `Icons` sprite atlas is gone, `UIAtlas` and `UIAtlasCompressed` are new; two
  Chinese fonts were dropped and `small_font` / `small_font_bold` materials added. **The donkey's art is unchanged**
  (its `0301_` sprites and the character atlases; `donkey-frames.txt` still matches, the API's own check at run time
  says the same). The balance changes are the PR machine (its five crafts gone, `pr_machine_logic_update` daily,
  zombie-cap raises per upgrade), the rest bench (`rest_bench`), mercenaries' pay, the coal shed and the conveyor
  storage quests (201-204), beeswax at the apothecary, the Monk's sword, seasonal cookies and pies, crematorium II's
  unlock, and villagers' `someone_inside` flags on their houses.
- **The decompile:** `decomp_26624074` and `lbt_26624074` in this job's temp folder
  (`C:\Users\User\.claude\jobs\0ecc038a\tmp`), the old ones in job `3d9fcfd0`'s (`decomp_26404424`, `lbt_26297960`).
  The audit's lists, the method diffs and the verify logs are in `audit_26624074` there.

## Patch targets and references

**`DepManifest verify` on the deployed API and on `dist/`'s: changed, 8 of 922 entries**, every one of them used by
a core class (`game-code.txt`), so the gate stops the mod:

| Member | What the game changed | Our use | Effect on us |
|---|---|---|---|
| `MovementComponent.StartPath(Vector3, string, string, ...)` | Takes a graph anchor for GD-graph moves; the path calculation and `OnPathStart` moved into `StartCalculatedPath`; same result codes | `Corpses` sends the body carrier (recast) | none |
| `DropSystem.DropItem(Item, string, Vector3, List<Item>)` | Small items of any count are split into stacks (was only above 5), and a small drop merges into a same-item drop within 0.75 (`TryAbsorbIntoNearbySmallDrop`); still returns true | `Cargo` set-down and unload, `Corpses`, the probes | a set-down small item may join a pile beside the cart instead of lying where we put it; the item is still in the world, and the return value still says so |
| `SpecialPhysicsCastUtils.GetPlayerDropPosition(Vector3, Vector2, out Vector3)` | Rewritten: the cast hits drops (layer 16) too and **stacks on them** (`GetStackedDropPosition`, the "stacked zombies" fix), checks the landing against blockers, and falls back to a ground point under the keeper (`TryGetFallbackDropPosition`) before the old "2 above the keeper"; same rings and fan | `Cargo` (unload in front, set-down rings), `CartChest` | a spot may be on top of a drop now, as the game's own drops are; the comment in `Cargo.cs` about the rings still holds |
| `ZombieSystemData.Cache` getter | Tolerates a zombie listed twice (logs and drops the repeat) | `Cargo`, `CartChest` through `GetZombie` | none |
| `PlayerInputHandler.UpdateInput()` | One line: `RuntimeDevApi.TryHandleDebugOverlayEscape()` before the menu key | our prefix (`HeadReroute`) | none |
| `SaveSystem.Save(...)` | The in-memory save cache is console-only (`IsInMemorySaveCacheEnabled`) | our prefix (the cargo before a save) | none |
| `LazyUI.GetWindow<T>()` | A destroyed cached window is dropped from the cache and looked up again | `CartChest` (is a chest window open) | none, better |
| `EasySpritesCollection.GetSprite(string, string)` | Sprites cached per atlas (`GetCachedSprite`), cleared on unload | `Corpses` (the body icon) | none |

- **The dependency gate works as designed**: it reports these eight and stops. The user asked to launch the game once
  with the deployed (1.011.1) build to see the gate act before any fix is applied; see "In game" below.
- **Every project compiles** against the new DLLs with `-p:Deploy=false` (nothing deployed): the API, Saddle Up, the
  three packs, the six tools, the four QOL mods and Event Decorations.
- **Owned Count** (`UITooltip.AddItemWidgets`): the sermon branch's early return became an `if`, and
  `TryAddPrMachineLoadDaysWidget` adds a line for the PR machine's items. Its postfix still fits; the QOL mods run on
  a changed member with a warning line (`Safety.StopOnChangedGameCode` false). Built Count, Global Storage and Tech
  Tree Search: identical.

## Near the mods, to check in game after the rebuild

- **Big drop views are pooled now** (`BigDropViewPools`, `DropViewAtomMesh.useSharedViewPools`): a drop atom no
  longer owns every item picture; it borrows one from a shared pool on `Activate` and gives it back on `Deactivate`
  (`ReleaseBorrowed`). `ActivateZombie` borrows the pose container and the head's picture the same way when the atom's
  own containers are gone. The game itself now calls `Deactivate()` on the keeper's overhead atom before it clones it
  for a stack (`AnimationComponentBase.EnsureExtraOverheadViews`), so the clone holds no borrowed picture.
  **Ours:** `Cargo.MakeView` clones the keeper's overhead atom (`FindSourceAtom`) with `Instantiate` and reads its
  `zombieWorkerDropPrefabs` for the head catalogue, and `LyingZombieShown` reads `MeshElement` and whether it is
  switched on under the view.
  **Measured in the first F11 on 1.012 (2125 passed, 2 failed, 0 warnings):** the keeper's atom is on the pool
  (the Player.log: `[BigDropViewPools] Views=28, zombies=22`; the cart's zombie picture came as
  `drops_item_zombie_pose2_t1051(Clone)`, a pool instance). The two failures were the zombie-head scenario's exact
  name compare (the picture itself was right both ways). Two real consequences: the stand-in rule (`ResolveIcon`:
  marble shown with the slab's picture; an item whose picture is named by a prefix, which the mechanism box was
  until 1.012 gave its picture the full name) read the picture names from the atom's children, empty on a pooled
  atom, so those items would get the game's placeholder (nails on
  the cart did show it, with the game's own error line "Not found element for item icon id: i_nail_bronze"; whether
  nails ever had a picture is not known, the scenario logs what they resolve to); and a clone made while the keeper
  carries something copies his borrowed picture as a plain child, which would stay on under the one the clone borrows
  (the game empties its own atom before cloning it). A cart's views were also destroyed with their borrowed pictures
  (a leak, not a corruption: the pool makes new ones).
  **Fixed the same day** (`Cargo.cs`): the stand-in names and the element lookup read the pool's table and prototype
  holder (private, by name, required at start by `Access.Verify`), a new cargo view is stripped of copied pictures
  (`StripCopiedPictures`: anything no list of the atom's names), the views give their pictures back before the
  holder goes (`Detach`), names are compared without the pool's "(Clone)" (`PictureName`), and the scenario "cargo:
  the pictures from the game's shared pool" measures all of it (docs/self-tests.md).
- **Drop stacking** (above): the set-down ring positions and the unload spot can now land on top of a drop. The
  cargo scenarios and the rehearsal's cart rounds measure where the items go.
- **Resting on the bench** (`EnergySystem.StartResting`): control taken with a new `TakenControlType.ByRest`, the
  keeper in `AnimationState.Wait`, time at the sleep multiplier. `Mount`'s control-type loop ends the ride for any
  type it does not exempt (UI, teleport, self, flow, attack), so sitting down dismounts like sleep does. To see once:
  sit while mounted (if the bench can be used from the saddle at all) and ride after standing up.
- **`PlayerWorkComponent`** holds the body at the dock point (`HoldAtDock` / `HoldWorkBody`, kinematic while
  working) and `PlayerPhysicalBody` got dev-only jump and speed (`RuntimeDevApi`). The work states end the ride
  already; the lock flags are the game's own `PlayerDynamicType.ByWork`, not ours.
- **`WgoPartPool` / `Wgo` / `WgoPartLoadManager`:** parts are stripped of editor-only components on creation
  (`EditorOnlyComponentStripper.StripCreated`) and a destroyed world object's parts are released; the members we
  use are unchanged. The pooled-parts audit (`[pool]` leftovers) runs as before.
- **Small-drop merging:** a dropped small item may merge into a pile within 0.75; the corpses' bodies are big.
- **Save fixers:** `< 1012`: crematorium II's unlock caught up, the PR machine migrated (`MigratePanicReductionMachine`,
  its zombie-cap raise), quests missing from the balance removed; `< 1012.2`: a chained fight's pre-battle stage
  restored. None touches a donkey, a cart, the side file or the hidden chest.
- **Fights:** `FightingGameController` (23 members), `FightingAgent`, `FightingLevel`, `CombatPauseClock` (archers no
  longer fire while paused), `MilitaryBaseData` (mercenaries unpaid can still join). Mounted combat's patches are on
  the player states and the projectile launch, both unchanged.
- **`LazyExpression`:** 65 members changed, new script functions `direct`, `gdgraph`, `recast`, `recastgraph`
  (movement types by name); the overhead-item mutation scope we patch is unchanged.

## In game (2026-11-13)

1. **The gate, with the deployed 1.011.1 build** (the user's choice, before any fix): the API logged "the game changed
   the code of 8 of the 922 game members this mod uses since it was built ... Core members: DropItem ...,
   GetPlayerDropPosition ..., UpdateInput ..., Save ... Mod is DISABLED; the game runs unmodified" (the other four
   are used by frozen features only and are not named), no patch went in, no exception; Saddle Up and the packs
   registered their mounts and skins and the options window opened (the settings UI touches nothing shared, by
   design); the journal was off (the user: "mount journal isn't working"). Owned Count: one warning, running; Built
   Count, Global Storage and Tech Tree Search identical (one info line each).
2. **Rebuilt against 1.012.7 (13:35):** the start clean, the donkey-art check "48 the same" in 72 ms. F11 full:
   2125 passed, 2 failed, 0 warnings; the two the zombie-head scenario's name compare (above). With the cargo fix
   (17:07): 2137/2/0, the two my new scenario's own expectations, corrected; the cargo group alone (17:4x): 7
   scenarios, 45 passed, 0 failed, 0 warnings, the pool scenario green on every line (the copy dropped from the
   cloned view, the keeper keeping his picture, marble as the slab, the mechanism box as itself; nails resolve to
   no picture and show the game's placeholder, as the first run showed).
3. **Hand checks still open:** the bench while mounted and after standing up; cargo set down where drops lie
   (stacking). 1.011's open checks 3, 4, 6, 7 still stand (`docs/game-update-26297960.md`).

## Release consequences

- Every Saddle Up build so far (0.16.0 public, 0.17.0 prepared) is disabled on 1.012. Players on 0.16.0 see the
  gate's message until the new release is up, so it goes out the same day (the user).
- The 0.17.0 and Donkey Skin Pack 1.3.1 zips were committed with their DLLs on 2026-11-12 and never uploaded. The
  release rule counts a committed zip as published (`release-versions.py published`: `--rebuild` refused), so the
  release is **Saddle Up 0.17.1 with API 0.45.1 and Donkey Skin Pack 1.3.2** (the user, 2026-11-13: a new version
  rather than overwriting), Dragons 1.3.0 kept as committed (its DLL is unchanged and asks nothing of the game). The
  pins moved (`release-versions.py` GAME_BUILD 26624074 / 1.012.7, the READMEs, both checklists, the probe's MVIDs,
  the Nexus and Workshop notes, which name the game version and say that an earlier version turns itself off).
