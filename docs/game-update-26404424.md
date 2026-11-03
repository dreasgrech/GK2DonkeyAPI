# Game update 1.011.1 (Steam build 26404424): re-audit

On 2026-11-03 the game was updated on Steam from 26297960 (1.011) to 26404424, which the game calls 1.011.1 (its
`PlayerSettings` version and `GameInfo.Version`, the string a new save is stamped with). The game folder's git has
the update committed ("updated game to build 26404424"); the previous files came from `HEAD~1` ("before update") for
the "old" side of the diff. The mods were audited against the new build before the game was started with it. No
Steam changelog was saved for this one; what it does is read from the code and the data below.

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `36326881-56e1-4dee-a10f-2893840f0687` | `7274b6c4-2447-4669-baff-1ced3407f34e` |
| `LazyBearTechnology` MVID | `46a6f4cd-56c5-410d-993a-de26a488dc60` | the same (not rebuilt) |
| Game and save version | 1.011 | 1.011.1 (`SaveCodeFixes.Apply` gained a `< 1011.1` and a `< 1012` block) |
| Unity, BepInEx | 6000.3.9f1, 5.4.23.5 | the same (nothing under `BepInEx\` or the game root changed) |
| Other game assemblies | | `Assembly-CSharp-firstpass`, `AstarPathfindingProject`, `AstarPathfindingProjectExamples` recompiled (below) |

- **`tests/GameDiff`:** `Assembly-CSharp` 44,935 → 44,947 members; 53 removed (3 not compiler-generated:
  `PickupOrder.HasItemsToPickUp()`, `PickupOrder.PickupInventoryOwner()`, `CraftComponent.TryStartCurCraft(bool)`),
  65 added, 37 changed. `LazyBearTechnology`: identical.
- **The other changed assemblies** (GameDiff now compares every game assembly whose bytes changed, not only the two
  above): `Assembly-CSharp-firstpass` and `AstarPathfindingProjectExamples` are recompiled with every member
  identical. `AstarPathfindingProject` (the A* pathfinding) has 38 members removed, 38 added and 1 changed, all of
  them Unity's generated property bags (`Unity.Properties.Generated.PathRequestSettings_<id>_PropertyBag` and
  `GraphMask_<id>_PropertyBag` under new ids, and `PropertyBagRegistry.Initialize` naming them). No `Pathfinding`
  member changed.
- **13 decompiled types differ, none is new:** `AutoCrafterOrderPlacer`, `CraftComponent`, `CraftInteractionHandler`,
  `CraftSystem`, `DeliveryOrder`, `PickupOrder`, `StartCraftOrder`, `ZombieWgoData`, `WgoData`, `WorldData`,
  `SaveCodeFixes`, `SaveCodeFixes_Helper` and `LazyExpression`. What the hotfix does:
  - **Zombie-worked autocrafts** (1.011's assistants):
    - An autocrafter's delivery orders now ask for what is still missing: counts summed per item, deliveries already
      under way counted, and a pending order of the wrong size replaced (`AutoCrafterOrderPlacer.GetMissingCount`,
      `HasMissingIngredients`).
    - A delivery puts in only the missing count, and a caretaker must hold the item it delivers.
    - Stale workbench deliveries and pickups are recognised and dropped by caretakers and gardeners
      (`DeliveryOrder.IsStaleWorkbenchDelivery`, `PickupOrder.IsStaleWorkbenchPickup`).
    - A start order counts only when nothing is missing and that zombie can start the craft
      (`CraftComponent.GetStartCraftStatusFor(IWorker)`). `TryStartCurCraft(bool, IWorker)` makes the zombie the
      worker for that start.
    - A zombie-serviced queue keeps its index, and goes back to delayed when its started craft is gone.
    - `WgoData.ChangeId` and `DeInit` drop the old order placer.
    - A gardener still carrying cargo goes back to its station instead of taking a new order.
  - **Save fixers:**
    - `< 1011.1`: the first grave module's removal lock is cleared once the "put the sign" quest is done. The module
      is found by its tag near (33.92, 3.6, 16.5), through the new `WorldData.GetWgoDataByCustomTagNear`. The
      `LazyExpressionOnWgo` script function takes that optional position too; the rest of `LazyExpression`'s diff is
      renumbered locals.
    - `< 1012`: a `GameSaveFixer` "1012" script event, and the kitchen ovens' craft inventory expanded to 50.
- **Art and data** (`tools/game-asset-diff.py`, new with this audit; it reads the old files from the game folder's git):
  - **53 changed asset files.** 49 small bundles each have one changed `Sprite`, of the same size: scenery and
    its shadows and ground patches (`common_debris_*`, `common_box_*_sh`, `common_carriage_*`, `common_broken_*_sh`,
    `add_ruins_for_battle_*`, `tree_dead_*_gnd`, `bush_ordinary_*_gnd`, `light_source_light_halo_m` and the like).
    No `Texture2D` changed, and no character atlas is among them, so the donkey's sprite keys the skins draw over are
    untouched (`docs/skins-design.md` section 11).
  - **The two big scene bundles:** four ProBuilder meshes got new ids.
  - **`GameInfo`:** 1.011 → 1.011.1.
  - **`resources.assets`:** `GameBalance` (+660 bytes), `TMP Settings` and `googleToken`. The balance changes:
    - the carousel motors are built as `i_b_carousel_motors` and need reinforced beams and bronze gears;
    - the first-grave lock script carries the module's position;
    - a new factory quest, `201_factory_storage_bigger_order`;
    - everything else is regenerated quest-node ids.
  - **`globalgamemanagers`:** the version string and the resource manager's list.
- **The decompile:** `decomp_26404424` beside `decomp_26297960` in the job's temp folder
  (`C:\Users\User\.claude\jobs\3d9fcfd0\tmp`). The audit's lists and scan logs are in `audit_26404424` there.
  `LazyBearTechnology` is unchanged, so `lbt_26297960` serves both builds.

## Patch targets and references

- **`DepManifest verify`: identical everywhere.**
  - Checked: the deployed `GK2DonkeyAPI.dll`, `dist/`'s, and the API inside every Saddle Up zip built for 1.011
    (0.13.2: 840 entries; 0.14.0: 850; 0.15.0: 866).
  - At start the API writes one info line ("the game was recompiled since this mod was built ... identical") and
    runs. Saddle Up and the three packs reference no game assembly (their references: BepInEx, GK2DonkeyAPI and
    netstandard, Saddle Up also UnityEngine.CoreModule) and follow the API.
  - **Players need nothing: no re-release**, unlike 1.011.
- **None of the changed or removed members is one the API uses**, and none of the changed classes (the orders, the
  autocrafter placer, `CraftSystem`, `SaveCodeFixes`) appears in the API's or Saddle Up's sources. `Corpses` looks up
  `morgue_entrance_door` and `npc_donkey` through `WorldData.GetWgoDataByCustomTag`, which is unchanged (the new
  `...Near` overload sits beside it).
- **The A* pathfinding is outside the manifest.** The API uses it directly in the safe ground, the mount and the
  dev probes (`NearestNodeConstraint`, `NNInfo`, `PathUtilities`, `RecastGraph`, `NavGraph`, `GridGraph`,
  `DistanceMetric`). The dependency manifest's scopes are `Assembly-CSharp` and `LazyBearTechnology` only
  (`src/Shared/DependencyManifest.cs`, `GameScopes`), so a change there would pass the start check unseen. This
  time nothing of it changed (above). The QOL mods reference neither A* nor firstpass.
  - **Closed the same day** (the user's go): the pathfinding is the manifest's third scope, with its module id in
    the fast path and the cache's key (`docs/dependency-check.md`, "Scopes").
- **The donkey's art had no check at all.** `docs/skins-design.md` section 11 described a run-time check of the
  donkey's sprite keys that was never built. Built the same day: the fingerprints of the 48 frames the skins were
  drawn on (`donkey-frames.txt`), checked once a session against the running game (`DonkeyFrames.cs`, section 11).
  `tools/game-asset-diff.py` says "unchanged" for this update.
- **Every project compiles** against the new DLLs: the API, Saddle Up, the three packs, the three examples and the six
  tools. They were built with `-p:Deploy=false`, so nothing was deployed. Three dev projects (`GK2DonkeyHerd`,
  `GK2DonkeyTechGate`, `GK2DonkeyProbe`) showed MSB3277. That warning is the game's own System.IO.Compression
  mismatch, and the API and the other tools already mark it as known; now these three do too.
- **`tools/release-scans.sh`: 26 of 26 green**: CecilScan, ApiSurface, DefaultsScan, SkinsScan, FrameworkScan,
  NoLinqScan, TranslationScan, `check-dist`, the layering scan, every `check_skin` and the PackerScans.
  - The packer review's one "to re-check" mark (log + low crate + slab) is already in the 0.15.0 run's log.
  - The release run's own log in `release/logs` was put back unchanged; this run's is `api-scans-10111.log` in
    `audit_26404424`.

## Near the mods, not breaks

- **Zombie automation:** nothing of ours places, takes or carries out orders.
  - Zombies on the cart are store items, not scene zombies.
  - The portable chest's hidden real chest is no crafter, and rides with its zone set to null.
  - The caretaker and gardener changes are inside their own order loops.
- **Saves:** the two new fixer blocks touch the first grave module and the kitchen ovens. Neither touches a donkey, a
  cart, the side file or the hidden chest.
- **Halloween** (`TemporaryEventDates`) runs from 24 October to 7 November. It is live now, so 1.011's check 5 (the
  keeper's head skin override against the dragon's and the Jester's front layers) can be made in any session before
  7 November.

## In game (2026-11-03, 21:35)

- **Start:** the API, Built Count, Global Storage, Owned Count and Tech Tree Search each wrote the one info line
  ("the game was recompiled since this mod was built ... identical"), and the framework's build contract passed
  for all six (Saddle Up's included). Saddle Up 0.15.0 and the packs loaded; the Jester skin was built.
- **The save** (`Steam_1`, stamped 1.010): `[SaveFixer] Applying code fixes to save [1.010] → [1.011.1]`, the
  first grave module's lock "already 0", `FireEvent [1012]`, then 1.011's fix asset. Our side file:
  "Steam_1: 5 of 5 donkey(s)/cart(s) back".
- **F11: 1790 passed, 0 failed, 0 warnings.** Every warning and error in `LogOutput.log` lies inside the run, each
  announced by its test, and none came after it.
- **`Player.log`:** nothing new.
  - The two `NullReferenceException`s at quit are the game's own bubble teardown (`UIObjectBubble.Hide` from
    `GameScene.OnDestroy` and `Wgo.DeInit`). They were in the 1.011 session's log too, and were traced on
    2026-09-30 (`src/GK2DonkeyAPI/PROBE-FINDINGS.md`).
  - The BoxCollider "negative scale" lines on the fight ruins were in the 1.011 log as well.

## The rebuild of 2026-11-03, 22:07

The API (with Saddle Up and the packs) and the four QOL mods were rebuilt against 1.011.1 and deployed: the pathfinding
in the dependency check, the donkey-art check (above, "Patch targets and references"). So the next start differs
from the 21:35 one:

- **No "recompiled ... identical" line** from any of the five: each is now built against this game, so the fast
  path passes and nothing is logged; the framework's build contract still passes for each.
- **One new info line once a mount is out:** "Looks: the game's donkey frames are the ones the skins were drawn on
  (donkey-frames.txt; 48 frame(s) read in N ms over 48 game frames)".
- **F11:** two new scenarios, "the start's check of the game's code: the A* pathfinding ..." (the core group) and
  "the donkey's art ..." (Skins; it skips without a mount out, so load the save with the donkeys around).

### In game (2026-11-03, 22:42)

- **The start:** no "recompiled" line from any of the five; the framework's build contract passed for each.
- **The donkey-art check is calibrated.** "Looks: the game's donkey frames are the ones the skins were drawn on
  (donkey-frames.txt; 48 frame(s) read in 338 ms over 48 game frames)". The probe's export and the API's own read
  give the same bytes, and `donkey_frames.py check` on the dev dump says 48 the same, 0 changed. The read cost no
  slow frame: the [perf] line after it reads 120 fps, frame 8.3 ms, worst 13.
- **F11: 1799 passed, 0 failed, 0 warnings.** Both new scenarios passed: the start's check found the running
  pathfinding by its name with the id it was built against (identical), and the donkey's art held its four parts.
  Every warning and error in the log lies inside the run, the same test-made kinds as at 21:35.

## In-game checks (pending)

1. 1.011's open checks still stand ([`docs/game-update-26297960.md`](game-update-26297960.md), "In-game checks"),
   check 5 (Halloween) before 7 November.
