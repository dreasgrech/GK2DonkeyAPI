# Game update 1.012.9 (Steam build 26734308): re-audit

On 2026-11-18 the game was updated on Steam from 26624074 (1.012.7) to 26734308, which the game calls 1.012.9 (its
`GameInfo` version, the string a new save is stamped with). The game folder's git has the update committed ("update
to game build 26734308", `57612742`, on top of "before update", `a027fcfe`); the previous `Managed` folder was taken
from that commit into `C:\ClaudeCodeTemp\GK2-prev-managed\26624074`. Steam has no news post for it (the newest is
"Update 1.012" of 2026-11-13; the news feed read once on 2026-11-18): a hotfix without notes, so what it does is
read from the code below.

**This update again disables every build of GK2 Donkey API made before it**, the public Saddle Up 0.17.1 (API
0.45.1) among them: the game changed `SaveSystem.Save`, which the API patches, and that is a core member, so the
dependency gate stops the mod at start ("Mod is DISABLED; the game runs unmodified"). The change itself asks nothing
of our code (below). A re-release is needed. The QOL mods are untouched: every member they use is identical.

Everything was rebuilt against the new build and deployed the same day (12:47, the game closed). The first run in
the game on it (13:14): every mod's check passed at the start, and the API's 187 scenarios ran green, 2141 passed,
0 failed, 0 warnings ("In the game" below).

## What changed

| | Before | After |
|---|---|---|
| `Assembly-CSharp` MVID | `c69d845c-001c-4bd4-9adf-7b288a697087` | `ff3601f5-c129-4b53-9ebb-78e5d294090e` |
| `LazyBearTechnology` MVID | `53b976b3-a36d-477a-bea4-e43c6634914e` | `982a437a-540c-4521-ba19-c9efaa43dec1` |
| `AstarPathfindingProject` MVID | `dbd7899c-f996-4d27-931b-a86b6fb8e156` | `f8f74564-0e08-4aac-9375-1b09ad38c3a0` |
| Game and save version | 1.012.7 | 1.012.9 (`SaveCodeFixes.Apply` gained a `< 1012.8` block) |
| Unity, BepInEx | | the same (nothing under `BepInEx\core` or the game root changed) |

- **`tests/GameDiff`:** `Assembly-CSharp` 45,732 → 45,814 members; 164 removed (3 not compiler-generated), 246
  added, 56 changed. `LazyBearTechnology`: 1 added, 1 changed (`LazyGamepadDependentElement`: Switch gamepads).
  `AstarPathfindingProject`: Unity's generated property bags renamed again (38 removed, 38 added, 1 changed), no
  `Pathfinding` member. Every other assembly in `Managed` was recompiled with every member identical.
- **The 3 removed named members:** `FightingGameController.OnFightRewardTeleportReady`,
  `PlayerInvisibleWalls.UpdateEdgeWallsState(Vector3)` (it takes the velocity too now) and
  `SaveCodeFixes_Helper.SyncExcessiveZombieDebuff(SaveFixContext)` (it takes a reason now). None is used by the API,
  Saddle Up, the packs, the tools or the QOL mods.
- **One new type:** `LazyBearTechnology.BepInExPluginHealth` (the Mods window's check of BepInEx plugins, below).
- **21 decompiled files differ** (20 changed, 1 new): `SaveSystem`, `PlayerPhysicalBody`, `PlayerInvisibleWalls`,
  `MovementComponent`, `PlayerController`, `MainGame`, `Wgo`, `CraftComponent`, `ZombieSystem`, `ZombieSystemData`,
  `ZombieWgoData`, `FightingGameController`, `FightingLevel`, `AlliesSpawn`, `SaveCodeFixes`,
  `SaveCodeFixes_Helper`, `CodeModHost`, `CodeModLoc`, `WorkshopSyncCore`, `BepInExPluginHealth` and Unity's
  generated script table; in LazyBear, `LazyGamepadDependentElement`.
- **Art and data** (`tools/game-asset-diff.py`): 14 changed asset files, almost all of it 118 changed
  `MonoBehaviour` objects (the game info, dialogue data, speaker aliases, the font assets) and the script list.
  **The donkey's art is unchanged** (its `0301_` sprites and the character atlases). `GameBalance`: no change that
  is not a regenerated id. The font assets `small_font` and `small_font_bold` changed, but **the characters the
  fonts hold did not**: the library's `l10n-fonts.py` on the new files gives the same seven character files, byte
  for byte, as the ones the translations were checked against (2026-11-17).
- **The decompile:** `decomp_26734308` and `lbt_26734308` in this job's temp folder
  (`C:\Users\User\.claude\jobs\0ecc038a\tmp`), beside the old `decomp_26624074` and `lbt_26624074`. The audit's
  lists, the diff of each changed file and the logs are in `audit_26734308` there.

## Patch targets and references

**`DepManifest verify` on the deployed API (built for 1.012.7) and on `dist/`'s: changed, 1 of 922 entries**, used
by a core class (`game-code.txt`), so the gate stops the mod:

| Member | What the game changed | Our use | Effect on us |
|---|---|---|---|
| `SaveSystem.Save(SaveSlotData, GameSave, ...)` | One line, `RedirectSaveFromBackupSlot(slotData)`, before the save is prepared: a save made from a loaded backup slot (`<slot>_backup_<n>`) no longer writes over the backup. It goes to the slot the backup belongs to when that slot is gone, else to a new slot; `slotData.slotName` is changed in place | our prefix (the cargo lent to the save, the keeper landed, the side file written from the game's own "saved" callback) | none in the code: the side file is written from the callback with the same `slotData` object, so it takes the name the game redirected to. To see once in the game (below) |

- **Every project compiles** against the new DLLs (the API, Saddle Up, the three packs, the six tools; in the QOL
  repository Built Count, Global Storage, Owned Count, Tech Tree Search, Event Decorations and their tests; the test
  bench), 0 warnings. Move Building does not, as since 1.011 (retired then; the game has its own Move).
- **The QOL mods:** the deployed builds are identical on every member (Built Count 35, Global Storage 43, Owned
  Count 61, Tech Tree Search 141, Event Decorations 124). The public ones in `dist/`: Built Count, Global Storage and
  Tech Tree Search 1.0.2 identical; Owned Count with its one changed member, as since 1.012 (it runs with a warning).
- **Release scans:** the API's 31 of 32 green, the one failure `check-dist` (the committed `dist/` DLL carries the
  manifest of 1.012.7: changed 1; the re-release makes it right). The QOL repository's 22 of 23, its one failure
  `check-dist` too, and not the update's: there is no `dist/` DLL of Event Decorations (never released).
- **After the rebuild:** `DepManifest verify` on every deployed DLL with a manifest says "the same build, identical"
  (the API now 924 entries).

## Near the mods, to check in the game

- **Saving from a backup slot** (`SaveSystem`, above). The side file follows the redirected name by construction.
  What stays as it was before this update: a loaded backup has no side file of its own (`<slot>_backup_<n>
  .gk2donkeyapi.dat` is never written, and the root slot's side file is of another save time, which the load
  refuses by design), so the kept donkeys and carts don't come back with a backup; their cargo lies on the ground as
  the save holds it. New with the update: when the root slot is gone, the next save takes the root's name, and our
  side file for it is written with what is kept then (the one before it stays as `.bak`). Nothing to build now; a
  side file kept with each backup is a possible later round.
- **The Mods window judges BepInEx plugins now** (`BepInExPluginHealth`, called once when the main menu comes up).
  For a code mod it tracks (one synced by the game: a Workshop item or a pack in `Mods\Code`, by the DLLs its sync
  state lists under `plugins\`), the window shows in red:
  - "BepInEx did not load this mod. See BepInEx/LogOutput.log." when none of its DLLs has a loaded plugin;
  - "Failed to start: <the line>" when, up to that moment, the game's own log (`Player.log`) holds a line of level
    Error or Fatal whose source is the plugin's name, or an exception whose text names one of the plugin's types.
    The first such line is shown, cut at 300 characters.

  **Ours:** Saddle Up and Tech Tree Search are on the Workshop. The API's gate writes its "Mod is DISABLED" line at
  Error level in `Awake`, so on this update a Workshop player of 0.17.1 should see "Failed to start: ... the game
  changed the code of 1 of the 922 game members this mod uses ... Mod is DISABLED" in the Mods window: the right
  message, without our doing. The other side of it: any Error line of ours before the main menu marks the mod as
  failed though it runs. Two are written at Error level while the mod runs: "Chest: the chest on the cart is
  switched off for this session" (`CartChest.cs` 188, a frozen feature) and "Corpses: unavailable, the game's code
  changed" (`Corpses.cs` 69). The QOL mods' "running on changed code" line is a warning, so they stay "Running".
  **To decide** (not changed): keep Error for "the mod does not run" alone and make those two warnings.
  On this machine the plugins are deployed by the build, not synced by the game, so the window tracks them only if
  the Workshop items are subscribed too; then the game's copy and ours are the same plugin twice, and the one
  BepInEx skips would read "did not load".
- **The keeper's edge walls** (`PlayerInvisibleWalls`, `PlayerPhysicalBody`): the walls that stop him at a ledge are
  probed ahead of him by his speed (up to 1 unit further in the direction he moves, 1.5 physics steps of travel),
  from the body's physics position, and the check stays on for 5 physics steps after he loses the ground (unless
  the game's own jump is on). **Ours:** `FlightGround`'s walk-away test models the old rule (a ray 0.6 down from
  0.05 over his feet, no look-ahead), and `Flight` puts the walls down when it takes the body for a jump or a
  flight (`WallsDown`, by the field's name: still there). A faster mount now meets the wall earlier at an edge, and
  for the first 5 steps after a take-off the game may put walls up again; the body is the mod's then (kinematic), so
  they shouldn't hold it. The jump and flying scenarios measure exactly this ("never sent back", the hillside, the
  home yard); by hand: ride fast along a ledge and jump at an edge.
- **The game's systems are un-paused later at a load** (`MainGame`: `SetSystemsPauseState(false)` moved from the
  start of the game to after the scene has loaded). The mounts come back at the API's own "game ready"; the bench's
  runs and a load of a save with donkeys kept show whether anything of ours ran while the systems were paused.
- **`PlayerController.GetRes`** no longer throws without player data (falls back to the game's, then the default).
  Nothing of ours calls it (the dev script dump and the self-tests read `PlayerData` directly).
- **Paths on the GD graph** (`MovementComponent.FindPathGDGraph`): the nearest node may no longer be a transit
  point, and the path's first point is no longer added twice. The corpses' carrier is sent with `StartPath` on the
  recast graph (unchanged, identical in the manifest); the foals and the standing mounts move by the mod's own code.
- **Zombies:** a removed station releases every zombie attached to it (`ZombieSystemData
  .ReleaseZombiesFromRemovedStation`, from `Wgo.DoBuildRemove` and `CraftComponent.AddDestroyCraft`), and
  `ZombieSystem` skips a zombie on the scene without data instead of throwing (one error line). Zombies on the cart
  go through `GetZombie`, unchanged.
- **Fights:** the rewards of a chained fight are given when the keeper is free again (a coroutine, not the
  teleport's callback), and allies go back to their spawn points between stages. Mounted combat's patches are on the
  player states and the projectile launch, both identical.
- **Save fixers:** `< 1012.8`: `FixHypnoResurrectionBonusLeak` (the PR machine's bonus). It touches no donkey, cart,
  side file or hidden chest.

## In the game (2026-11-18, 13:14): the first run on 1.012.9

The user started the game on the new build with the rebuilt mods and ran the suite "GK2 Donkey API" in the bench.

- **The start:** every mod's own check passed (the API, Saddle Up, Built Count, Global Storage, Owned Count, Tech
  Tree Search, Event Decorations: "every game member it uses is identical to the audited build"); no warning or
  error before the run. The save loaded with its mounts: "Persistence: Steam_1: 15 of 15 donkey(s)/cart(s) back, 1
  cargo item(s) back on their carts" (the later un-pause at a load changes nothing for the restore).
- **The run:** `done (187 of 204 tests): 2141 passed, 0 failed, 0 warning(s)`, all 187 scenarios in the list's
  order. No warning or error after it; the 55 during it are the tests' own.
- **Against the run before the update** (12:04 the same day, 2137, the same code on 1.012.7): 185 of 187 scenarios
  make the same number of checks; 147 say every check in the very same words and 29 more but for measured numbers.
  The three that differ, each read, none the update's:
  - the start's check of the game's code: the module ids in its text are the new build's;
  - "colours: the player's regions on a mount in its own colours", 7 → 6: it was night in the game, and the scenario
    leaves out its as-drawn hue check by night (an INFO line says so: `Looks.cs` 1170);
  - "the mount crowd (measures only)", 1 → 6: the world's own mounts had as many views at the end as at the start
    this time, so the scenario compared its five counters (all back); at 12:04 the counts differed by one and it
    said so instead (`Crowd.cs` 153).
- **The edge walls** (above): the jump and flying scenarios make the same checks as before the update and pass,
  the home yard, the hillside and "never sent back" among them. The hillside wrote no frame-by-frame lines (no step
  past 2.5 px).
- **The bench's panel** was hidden before each of the six measuring scenarios' first line (one "hidden" line each,
  "shown again" after the last): the fix of the morning, seen.

## In the game: still owed

1. **The other suites** on the new build (the bench's own 5 tests, Tech Tree Search's 7, Event Decorations' 5: this
   run was the API's suite alone), and with them Tech Tree Search's names-against-unlocks timing line.
2. **By hand:** load a backup slot and save (which slot the save lands in, and the side file's name in the log);
   ride fast along a ledge and jump at an edge; the Mods window's line for the Workshop items.

## Release consequences

- **The public Saddle Up 0.17.1 (API 0.45.1) is disabled on 1.012.9**, on Nexus and on the Workshop, until a build
  made for it is up. The packs and Saddle Up follow the API (they use no game member themselves). Tech Tree Search
  1.0.2, Built Count and Global Storage run as they are; Owned Count as since 1.012.
- **What the re-release is made of is the user's decision.** The repository's head is well past 0.17.1 (everything
  under Unreleased in `CHANGELOG.md`: the journal picture fix, the window library, the tests' move), with hand
  checks open (`docs/pending-2026-11-18.md`). The two ways: (a) 0.17.2 / API 0.45.2 from the 0.17.1 commit, rebuilt
  for 1.012.9 and nothing else, out the same day; (b) the head as the next minor, after its open checks.
- **The pins** move with the release, not before: `tools/release-versions.py` (`GAME_BUILD` 26624074, `GAME_VERSION`
  1.012.7), the API's and Saddle Up's READMEs, `dist/README.md`, `docs/release-checklist.md`, `docs/api.md`, the
  Nexus and Workshop texts in `media/` and `docs/nexus/`, the probe's pinned MVID (`tools/GK2DonkeyProbe`); in the
  QOL repository the checklist, `dist/README.md`, three READMEs and Tech Tree Search's store texts.
