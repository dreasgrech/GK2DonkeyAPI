# Game update 1.009 (Steam builds 25731225 and 25752683): re-audit

## Second build the same day: 25752683

A few hours after 25731225, Steam delivered 25752683 (DLLs of 15:55, same byte sizes). Checked the same way: a
fresh decompile diffed against the 25731225 decompile, the member diff from 1.008, and the scans. **Two lines
differ, nothing else:** `EnvironmentEngine.CustomUpdate` now decides "a new day" (`num2 >= 1f`) *before* clamping
the time of day, and `MathUtilities.ClampCycleWithinRange` is back to a plain `value >= max` (25731225 had
`EqualsOrMore`). It's the finish of the day-rollover fix. The member diff from 1.008 is the 1.009 one plus
`EnvironmentEngine.CustomUpdate` (83 changed). No patch target or referenced member is involved; the director reads
the time through `SetTimeOfDayFake` and `Data`, unchanged.

| | 25731225 | 25752683 |
|---|---|---|
| `Assembly-CSharp` MVID | `6d888e09-1564-414a-8e4f-04e3e6757694` | `44edf1a9-b9b5-4276-a731-a680e9e9c44d` |
| `LazyBearTechnology` MVID | `c51bcee3-42e5-497e-9982-9da1dc3f4ba1` | `02955186-b2b7-474c-a6ff-b860ac92b69c` |

The pins in both repos point at 25752683; everything rebuilt and rescanned (all pass). The decompile of this build
is `decomp_1009b` / `lbt_1009b` beside `decomp_1009` in the job's temp folder.

## First build: 25731225

On 2026-10-06 the game was updated on Steam from 25676698 (1.008) to 25731225, the version the changelog calls
1.009. The game folder's git still has the 1.008 DLLs at `HEAD` (the update is uncommitted there), which gave the
"old" side of the diff. The mods were audited against the new build before the game was started with it.

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `f7d46132-dd54-4296-8f19-917330ceadd9` | `6d888e09-1564-414a-8e4f-04e3e6757694` |
| `LazyBearTechnology` MVID | `f67c6993-6db1-4f08-8352-cf7315263736` | `c51bcee3-42e5-497e-9982-9da1dc3f4ba1` |
| Save version | 1008 | 1009 (`SaveCodeFixes.Apply` gained a `< 1009` block) |

- **`tests/GameDiff`:**
  - `Assembly-CSharp`: 43,102 → 43,238 members; 172 removed (2 not compiler-generated:
    `ChunkSizeCalculator.CalculateChunkBounds`, `FrightenedAnimalBehaviour.Awake`), 308 added, 82 changed.
  - `LazyBearTechnology`: 1 changed (`MathUtilities.ClampCycleWithinRange`, `>` became `>=`: the "day not rolling
    over at exactly the end of the day" fix).
- **20 decompiled types differ**, plus two new ones (`Ps4SaveViewPoolPrewarm`, `WsoSeparableChunksData`): CraftSystem,
  FrightenedAnimalBehaviour, HUD, Inventory, KnowledgeSystem, MainGame, PlayerPhysicalBody,
  ReservoirInteractionHandler, SaveCodeFixes(_Helper), StationInteractionHandler, UICreditsWindow(Element),
  UIMultiInventoryWindow, UIResourceBasedCraftWindowData, UITooltip, WgoData, WgoPartPool, Wso, ZombieHPActivity,
  ZombieWgoData.
- The 1.009 decompile was written to the job's temp folder (`decomp_1009`
  and `lbt_1009`), not next to `decomp_1008` in the scratchpad (that folder is kept read-only). Move it there if it
  should outlive the job.

## Patch targets and references

- **Every project compiles** against the new DLLs (API, Saddle Up, the two probe tools, the director, both examples),
  so nothing the mods reference was renamed or removed.
- **`CecilScan-DonkeyAPI`:** 379 IL references into the game assemblies, 0 unresolved; `ApiSurface`, `DefaultsScan`,
  `FrameworkScan`: pass.
- **None of the 24 patch classes' targets changed**, by the member diff: `WgoDataScriptsManager.CreateScript`,
  `Wgo.CompleteVisualPartsLoad`, `Wgo.OnDestroy`, `PlayerView.LateUpdate`, `PlayerInputHandler.UpdateInput`, the
  `PlayerData` head methods, `SaveSystem.Save/Remove`, the `AnimationComponentBase` methods, the zombie, big-drop and
  interaction-component methods, `Flow_Body.DoAction`: all unchanged.

## Breaks found (fixed)

| Mod | What | Fix |
|---|---|---|
| **Donkey API, zombies on the cart** | `StationInteractionHandler` (the caretaker's station) gained a "[E] Take" while the caretaker is out working ("You can pick up or inspect a caretaker from their station while they are out working"). Its path is the one the seven other station handlers use: `HasInteraction` is gated by `HasFreeOverheadSlot`, `Interact` does `UnAttachFromWgoData()` then `PutZombieFromGameSceneToStoreForPlayer`, which is `AddOverheadItem`. Our `ZombieStations_TakeBack_Patch` marks exactly those handlers so the gate can say no while riding with `CargoZombies` off; this one wasn't marked, so with zombies off the station would have handed the caretaker to the head and the empty-hands rule would have laid it on the ground (the 2026-10-05 bug through a new door). | `StationInteractionHandler` added to the patch's target list (`HeadReroute.cs`). With zombies on, the caretaker goes onto the cart like any other worker taken back. **To check in game** (§ below). |

The other mods needed only the new MVIDs.

## Changes near the mods, not breaks

- **Pooled parts (`WgoPartPool`):** the pool now counts borrowed parts per prefab (`NoteBorrowed`/`NoteReturned`) and
  pre-warms idle parts after loading a save (`Ps4SaveViewPoolPrewarm.PrewarmLoadedScenesAsync`, from
  `MainGame.AfterSceneHasLoaded`). `GetAsync` now sets a reused part's `localScale` from the prefab. Our views borrow
  through `Wgo.Spawn` and return through the game's own release, so the counts stay right; parked views
  (`Tiers.cs`) count as borrowed while parked, which is true. Our scale sits on the view root, never on parts.
- **Keeper physics:** `PlayerPhysicalBody.ResetSimulationState()` (new) is called on return to the menu and on a new
  game, resetting every kinematic flag and the mute. Our cleanup on menu return (`ForceCleanup`) already restores
  what the ride changed; the game's reset comes after it and only widens the reset. Nothing to do.
- **`MainGame.HiddenOptimization`** no longer calls `GC.Collect()`. It runs after a door's scene unload; our
  through-the-door re-summon doesn't depend on it.
- **Zombies (`ZombieWgoData`):** `UnAttachFromWgoData` now unsubscribes the caretaker from the zone's
  `OnOrderRemoved` (it used to end up subscribed twice after a re-attach); `CaretakerState`'s setter redraws the
  station's widgets when the on-station state flips; the caretaker's `MoveAction` picks carrier dock points not
  disabled for caretakers; gardeners give up a harvest after two failures (`failedGardenerHPStrikes`,
  `AbortFailedGardenerHPWork`); `Hand`/`Armor`/`WorkerToolInventory` null-safe; crafters at the sawmill, mine, clay
  and sand pits deliver the result before the next craft. None of it is on our paths; the chest design notes
  (`portable-chest.md` §4a) were written against 1.008 and still hold.
- **`WgoData.TrySetWorker`** replaces a worker that is no longer valid instead of refusing (the "freeze when
  assigning a zombie to a station" fix). We never call it.
- **Save fixes at load (version < 1009):** a negative `food_eng_buff` is reset; the tier-2 kitchen oven tech is hidden
  until the repair. Also added to the `< 1008` block: broken zombie HP activities are dropped. None touch world
  objects, inventories or zones.
- **`Inventory.GetItemsByType`** gained an optional `includeBagContents` parameter (bait in bags). We don't call it,
  and everything was rebuilt anyway.
- **UI:** `UIMultiInventoryWindow` gamepad navigation, `UITooltip` hides a station extension that is already
  attached, `UIResourceBasedCraftWindowData` survey filter, `HUD.ReleaseTransientDisableStates`,
  `KnowledgeSystem.hiddenPurchasedTechs`. Not on our paths (the Tech Search and Owned Count mods are audited in
  the GK2QOLMods repo's copy of this document).

## Mishap: dev tools deployed by the audit build

Building every project of the repo for the audit also deployed two things that had never been in the game before:
`tools/GK2DonkeyProbe` (the first investigation tool; on every start it spawns the game's `npc_donkey`, `cow` and
`npc_donkey_test` for 25 s near the keeper and writes a report) and `examples/GK2DonkeyTechGate` (a sample mod that
allows the cart style only once a tech is unlocked). The user saw the probe's two delivery donkeys standing in the
void beside the house interior. Both `.csproj` files now default to `Deploy=false` like the herd example, and the
two DLLs and their cfgs are removed from the game. `GK2DonkeyAPIProbe` (passive logging) and the showcase director
were already installed before and stay.

## In-game checks (pending)

1. Start the game: no `Game code changed since this mod was audited` error in `BepInEx/LogOutput.log`; the API,
   Saddle Up and the director load.
2. **The caretaker station's new Take**, mounted in Cart style with a caretaker out working: with "Zombies ride on
   the cart" on, "[E] Take" shows and E lays the caretaker on the cart (log: `linked to its copy`); with it off,
   "[E] Take" does not show while mounted, and on foot it works as the game intends.
3. A normal ride, a door, a save: as before.
