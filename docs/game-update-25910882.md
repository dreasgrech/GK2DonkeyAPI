# Game update 1.010 (Steam build 25910882): re-audit

On 2026-10-13 the game was updated on Steam from 25752683 (the 1.009 hotfix) to 25910882, the version the changelog
calls 1.010. The game folder's git has the update committed ("game version 1.010"); the previous DLLs came from
`HEAD~1` ("before update") for the "old" side of the diff. The mods were audited against the new build before the
game was started with it. The changelog the user pasted is kept beside the decompile
(`changelog_1010.txt` in the job's temp folder).

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `44edf1a9-b9b5-4276-a731-a680e9e9c44d` | `5ec8094e-4c33-4615-9eb8-011501193186` |
| `LazyBearTechnology` MVID | `02955186-b2b7-474c-a6ff-b860ac92b69c` | `6cd86a0b-b1ed-40ba-88b0-a9313146514b` |
| Save version | 1009 | 1010 (`SaveCodeFixes.Apply` gained a `< 1010` and a `<= 1010` block) |

- **`tests/GameDiff`:**
  - `Assembly-CSharp`: 43,238 → 43,415 members; 338 removed (17 not compiler-generated: `ConveyorSoundSystem`'s
    debug fields and `UpdateDebugInfo`, `UITutorialWindow.SetCustomLineSpacing`, a `SkinPresetGK2.TryToApply`
    overload, and the `WgoData(string, Vector3, string, string)` constructor, which became
    `WgoData(string, Vector3, string, string, bool isTempObject = false)`), 515 added, 372 changed.
  - `LazyBearTechnology`: a new MVID, 0 members removed, added or changed (recompiled only).
- **75 decompiled types differ**, plus five new ones: `CrowNest`, `CrowNestInteractionHandler`,
  `TakeOnlySerializedItemProperty`, `DisableOnPlatforms`, `GK2.FlowCanvasNodes.Flow_SetOverheadVisibility`.
  The ones on our paths: PlayerData, PlayerController, SaveSystem, GameSave, WorldZoneData, Wgo, WgoData,
  SaveCodeFixes(_Helper), SaveFixer, SaveFixContext, DropSystem, DropView, DropSearcher, DropData, Inventory, Item,
  UITooltip, UIBaseChestWindowData, ZombieWgoData, WgoPartPool, WgoPart, MainGame, GameScene, Wso, AnimationState,
  TakenControlType, MultiInventory (unchanged, read for the crow nest).
- The 1.010 decompile is `decomp_1010` / `lbt_1010` in the job's temp folder beside `decomp_1009b` (ilspycmd 8.2
  with `DOTNET_ROLL_FORWARD=Major`).

## Patch targets and references

- **Every project compiles** against the new DLLs (API, Saddle Up, the two probe tools, the director, both
  examples; the five QOL mods in their repo).
- **`CecilScan-DonkeyAPI`:** 479 IL references into the game assemblies, 0 unresolved; `ApiSurface` (no change),
  `DefaultsScan`, `FrameworkScan`, `PackerScan` plain / `--shapes` / `--real --thorough`: pass. The QOL repo's eight
  scans pass; its Harmony harness failed only on the old pinned MVID until the pin moved.
- **Patch targets whose body changed** (member diff) and why none of ours minds, all four being prefixes that read
  nothing of the body:
  - `PlayerController.Teleport`: `HiddenOptimization` is skipped on PS4 only. Our two prefixes (the chest set-down,
    the door note) unchanged.
  - `WorldZoneData.TryAddWgoData`: no nav-mesh cut unit for a temp object. Our prefix (a riding chest is in no zone)
    unchanged.
  - `SaveSystem.Save`: an error line when the world data was written but the slot info was not. Our prefix (the
    side file in the success callback) unchanged.
  - `GameSave.PrepareToSave`: `SaveCodeFixes_Helper.PushPlayerOutOfBed` runs first (below). Our prefix (no allowed
    chest hidden in the save) runs before it.
  - `PlayerData.AddOverheadItem` / `InsertOverheadItemTo`: error logging (below); our prefixes unchanged.
- The other 20 patch targets and every reflective lookup are unchanged. `Wgo.TryRegisterWorkbenchExtensionDelayed`
  (Move Building calls it by reflection) and the three `ZombieWgoData.*TryMoveToCurrentTarget` (we read their
  target-id fields) are listed as changed but decompile to identical text.

## Breaks found

None. The pins moved in every `Plugin.cs` (and the probe tool), the READMEs, `dist/README.md` and the checklist.

## Changes near the mods, not breaks

- **Overhead items ("wood disappearing from the player's hands").** `PlayerData.AddOverheadItem` now refuses an
  empty item (`Debug.LogError`) and logs an error when the item is already carried (then still adds it);
  `TryAddOverheadItemNoReplace` logs the empty case; `DropOverheadItem(Item)` skips an empty carried item with an
  error; `InsertOverheadItemTo` logs when the inventory emptied the item without placing it and checks afterwards
  that no empty item stays on the head (`LogErrorIfEmptyOverheadRemains`); new `RemoveEmptyOverheadItems`, run on
  saves older than 1010. Our hand-backs use `TryAddOverheadItemNoReplace`; the one `AddOverheadItem` call
  (`Cargo.DropAtKeeper`, the head as the last resort after the game refused a drop) passes an item that is not
  carried. No new error lines are expected; **check the log in game** after a ride with cargo and a save.
- **Overhead visuals (the sauna fix).** `PlayerController.HideOverhead` / `ShowOverhead` and
  `overheadVisualsSuppressed`; `SetOverheadItems` honours it; a new flow node `Flow_SetOverheadVisibility`. We never
  touch the overhead animation layer (the rider's hands are our own motion), so nothing meets it.
- **Temp objects.** Our donkeys and the hidden chest's temporary copy are `isTempObject` (the build preview
  mechanism). The game now skips every nav-mesh cut for them: `WorldZoneData.TryAddWgoData` adds no cut unit,
  `WgoData.BeginCustomNavMeshCutTracking` / `RefreshCustomNavMeshCutUnit` / `SyncCustomNavMeshCutUnit` /
  `SetupForcedNavigationHoleIfNeeded` return early, and `Wgo.ApplyWgoPartState` disables every `NavmeshCut` in the
  view's children (`DisablePreviewNavmeshCuts`). That is what we did ourselves (Mount.cs switches the donkey's cut
  objects off); the game now agrees.
- **The animation chunk flag.** A `Wgo` whose `ChunkingIgnoreType.Animation` flag is raised now stays active
  whatever its chunk says: `ForceActiveForAnimationChunkFlag` (from `ChunkedObjectUtility.UpdateFlag`) sets it
  visible and active, `UpdateChunkVisibility(false)` and `UpdateChunkVisibilityState` are ignored meanwhile,
  `RefreshVisuals` keeps it on (`RefreshVisualsNow`, `SetRootActive`), and `GameScene.OnDestroy` suppresses the flag
  on unload. The flag is raised by `Wgo.HandleAnimationSetTrigger` / `HandleAnimationSetState` (the data's
  animation events), `FightingAgent` and `Flow_PlayDirector`, cleared by the animator's `IdleSMB`. We drive the
  donkey's `AnimationComponent` directly (`SetDirection`, `SetTrigger`, the walk speed multiplier), never through
  the data's events, and `Tiers` parks views with `SetActive(false)`: the flag never rises on a donkey.
- **Loading.** `TakenControlType.BySceneLoading` (new) is taken for the whole of `MainGame.LoadGameScene` and
  released at the end of `AfterSceneHasLoaded`, before `OnGameStarted` fires, so before `Mounts.GameReady`; the
  `WhyMustDismount` loop over every control type would end a ride during it, but no ride exists then. `SetControlTakenType`
  no longer drops the interacting item for `BySelf` or `BySceneLoading`.
- **`PushPlayerOutOfBed`** (the "stuck in the bed after loading" fix): at load (`MainGame`, not a new game) and in
  `GameSave.PrepareToSave`, a keeper inside the home bed's collider box (0.92 × 0.6 × 1.7 at −95.6, −78.6 of the
  home scene) is moved to the bed's exit point (`PlayerController.SetPosition` in game, else the saved position).
  A mounted keeper in that box at save time is unlikely (the bed stands indoors); our save prefix runs before it,
  and the ride's own positioning or its end would follow the move. Nothing to change; noted.
- **Save fixes at load.** Saves older than 1010: empty overhead items removed; vendor order slots synced to the town
  quality (`VendorSystem.SyncUnlockedOrderSlots`, up to 5); `BrokenDecor_*` WSOs get their separable chunks back
  from the content parts; the crow nest is spawned (`CrowNest.TrySpawn`: a `crow_nest` `WgoData` at the GD point
  `crow_nest_point`, also on a new game). Saves up to and including 1010 (so every load in 1.010): body colours and
  hair catch-up; **orphaned carousel zombies dropped** (`ZombieType.Free`, not interactable, no attached object,
  zone `conveyor`, a dock parent that no longer exists: the body item is dropped where the zombie stood and the data
  leaves the scene for `zombieDrops`); stacked regular mushrooms in `RuinedTemple` removed. Zombies on the cart are
  items in the zombie store, exactly as on the head (`PutZombieFromGameSceneToStoreForPlayer`), not scene
  `ZombieWgoData`, so the orphan rule cannot match them. `ZombieWgoData.PrepareForGame` now drops a zombie whose
  station failed to load (`TryGetAssignedZombieLoadFailure` → `ReleaseFailedAssignedZombie`, with the carried items
  spilled); not our path.
- **The crow nest.** Every new day (`DropSystem.CustomUpdate` → `CrowNest.CollectAgedDrops`) a drop that is a
  *small* item (`ItemDef.itemSize == Small`), has lain two days or more (`DropData.spawnedAtDay`; a drop without one
  gets the current day on first touch), is not a resource drop, has no `NeverAutoDestroyDropSerializedItemProperty`,
  no auto-destroy timer, and is not a body corpse (groups `body` + `corpse`) is moved into the nest's inventory and
  removed from the scene. The nest is a take-only, auto-expanding chest (`TakeOnlySerializedItemProperty`,
  `AutoExpandSerializedItemProperty`; `Inventory.BlocksPlayerItemInsert` stops the chest window's move-all and the
  UI move handler; `Inventory.TakeAllItemsExistingInMeFromOtherInventory` refuses). For us: cargo set down from the
  cart and the hand-back drops at save time are ordinary drops; left lying two days they go to the nest, where the
  player takes them back (the game's design, not a loss). Corpses at the crossroads are excluded by the group rule.
  The game's `MultiInventory` knows nothing of take-only (`TryAddItem` fills any listed inventory with room), so the
  nest presumably is not `OpenInMultiInventory`; as a precaution **Global Storage now excludes take-only
  inventories** (the QOL repo's copy of this document).
- **Drops.** `DropSearcher` unblocks auto-collect once the inventory has room again ("items not being picked up
  after inventory space was freed"); `DropView.TryKickFromPlayerCollision` wakes a small drop the keeper walks into.
  Our set-downs are plain `DropSystem.DropItem` calls; unchanged.
- **Quitting.** `GameShutdown.IsQuitting` guards the addressable releases in `WgoPart.OnDestroy` and `Wso` ("hanging
  on exit"); every release goes through `AddressableUtils.ReleaseHandle`. Our `Application.quitting` handler only
  writes the cargo layout file; the pooled-parts bookkeeping (`WgoPartPool.borrowedCounts`, `pools`) is unchanged.
- **UI.** `UITooltip.ShowAtPosition` skips `ForceUpdateCanvases` on PS4 only; `UIHotBarWidget` redraws at the end
  of the frame; `UIBaseChestWindowData` has a take-only mode; `CharMainPageWidget` gamepad bag navigation;
  `GardenInteractionHandler` refuses a craft with no need items (onion seeds in vineyard plots). Not on our paths.

## In-game checks (pending)

1. Start the game: no `Game code changed since this mod was audited` error in `BepInEx/LogOutput.log`; the API,
   Saddle Up, the director and the five QOL mods load.
2. Load a 1.009 save: the fixer's lines (`GameSaveFixer` 1010, the crow nest spawned); the kept donkeys and carts
   back from the side file; a ride, a door, a save as before.
3. After a ride with cargo and a save: no `AddOverheadItem ... already carried`, `Empty overhead item still
   carried` or `InsertOverheadItemTo emptied carried item` error from the game in the log.
4. Cargo set down and left two days shows up in the crow nest (the game's rule; just to know it).
5. A mounted ride to the morgue door with a corpse (the morgue handover reads the doors from GameBalance, which
   this audit cannot read; the changelog fixes the Doctor's basement door and the barricaded passages at the
   morgue).
6. A scripted moment while riding (a dialogue, a cutscene pose): `AnimationState` gained `Wait = 4` and
   `Flow_SetTriggerToAnimator` can now call `PlayerAnimation.SetState` directly ("the Keeper's arms staying
   raised" fix). Our `SetState` prefix treats any state other than Walk/Idle as a scripted pose that ends the
   ride (no throw on a new value); see that the ride ends cleanly and the pose plays.
7. The safe-ground reach gate on the spots the changelog unstuck (bushes near Jack's Bridge, the fruit garden,
   the church fence): scene colliders and nav mesh are balance/scene data, invisible here.

The first-run rehearsal of `docs/release-checklist.md` §5 was run on the 1.009-hotfix dist; the 1.010 dist built
the same evening has not been rehearsed (only the pins changed in this repo).

## The gate itself

Whether the MVID pin should stay this strict was investigated the same evening, with data from all four updates so far:
`GK2QOLMods/docs/update-gate.md` (recommendation: a dependency fingerprint for the QOL mods, the strict pin for this repo).
The next day the pin went here too, decided per feature: `docs/dependency-check.md`.
