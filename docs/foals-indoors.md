# Foals indoors (API 0.18.0, built 2026-10-03, not yet tested in game)

With this on, the baby donkeys come through doors with the keeper, follow him inside, and come back out with him.

- **Code:** `src/GK2DonkeyAPI/FoalsIndoors.cs`, plus the "lead" in `Foals.cs`.
- **API:**
  - the `Mounts.Shuffle` key `ShuffleFollowKeeperIndoors` (bool, off in the core, per foal through
    `MountHandle.Tuning`);
  - `MountHandle.IsWithKeeper`;
  - `MountCompanion.FoalsComeIndoors` (off).
- **Saddle Up:** "Baby donkeys come indoors", under "Baby donkeys" in the Donkey section, in both menus (off).

## How the game's doors work (investigated against 1.008)

- **A door is a teleport:** `PlayerController.Teleport(TeleportDataBase)`, mostly `WgoTeleportData` to a wgo's
  teleport point. Its steps, in order:
  1. Fade to black, wait `delayInFade` (0.3 s).
  2. Another scene only: unload the scene, show the loading overlay, load the new one.
  3. `SetPosition`, then the public static event **`PlayerController.OnPlayerTeleported`**.
  4. `EnvironmentEngine.SetTimeOfDayPreset(teleportData.environmentPreset)`. It's synchronous once the presets are
     preloaded, which they are in a game.
  5. Wait for the parts to load, fade back, then give control back (`TakenControlType.ByTeleport`).
- **A house is usually in the same scene:** its interior is somewhere else in the same scene (`IndoorArea` boxes in
  XZ). "Indoors" is the light preset's `indoorPreset` flag. That covers houses, the church and dungeons.
  `DismountIndoors` uses the same test (`Instance.IsIndoors`).
- So between the event and control coming back the screen is black. The foals are moved in that window, and nobody
  sees them jump.

## Design

- **The lead.** A foal keeps to its parent, or to the keeper while it is with him. `Foals.cs` reads everything about
  the one it keeps to through `LeadNow()`: body, ground height, scene, present, moving, facing, cart and name.
  - **The parent:** exactly the values read before, so the follow, slots, routes and catch-ups are unchanged.
  - **The keeper:** his position and scene, facing from `PlayerData.Direction.x`, no cart. While his control is taken
    (a door, work, a chest), he counts as "moving", so no catch-up happens then.
- **Through a door** (`OnPlayerTeleported`, decided 2 frames later or when control is back, whichever is first):
  - **Who comes:** foals with the flag that stood in the keeper's scene within `ShuffleFollowMaxDistance` (10) of
    where he was before the door, plus every foal already with him.
  - **Indoors after the door:** they join him. Each is placed in its slot next to him, in his scene if that changed,
    while the screen is black.
  - **Outdoors after the door:** the ones with him are placed next to him at the door and leave him. From there they
    go back to their parent the usual way: they trot to it, or catch up out of sight.
- **Every second while the keeper has control:**
  - Outdoors, no foal stays with him (a door the event missed).
  - Indoors, a foal whose flag was switched off leaves him.
  - Right after a load indoors, the flagged foals within reach are with him again. Being with him isn't saved.
- **A load's own teleport is never a door:** a new game resets the door state, including where the keeper stood.

## Measure first (the timing)

Each door writes these log lines:

- `[indoors] the keeper went through a door (event frame N, now M, control back / not back yet): from ... to ..., indoors/outdoors (<preset>)`.
- `[indoors] control back at frame K (... frame(s) after the door)`.
- The foals' `caught up with the keeper (came in with the keeper)` lines.

**What should happen:**
- "control not back yet" on the first line: the foals were moved while the screen was black.
- The right indoors/outdoors call.

**If "control back" shows up there,** the foals jump in view. The fallback is a prefix on the static
`PlayerController.Teleport` that reads `teleportData.environmentPreset` up front.

## Known limits and open questions

- **Dungeons and the church count as indoors,** as for `DismountIndoors`. If foals shouldn't go into dungeons, the
  test needs narrowing (by preset name or scene).
- **Small rooms:** five foals' slots reach about 2 units to the side of the keeper. Furniture blocks straight lines
  and routes, so a foal can't always get to its slot.
  - A lost foal catches up only off screen. Indoors it's nearly always on screen, so it could stand where it lost him
    until he leaves the room.
  - Watch for this in the test. If it happens, either allow an in-view catch-up after a few seconds indoors, or
    shrink the slots while with the keeper.
- **The parked parent:** going in mounted ends the ride (`DismountIndoors`). The donkey is parked where it stood
  when the indoor preset was seen, on the doorstep outside. The foals come back out next to the keeper at that same
  door.

## The door bug (found 2026-10-03, fixed in the same build)

**Symptom** (video `2026-10-03 01-46-20 (Reproduced bug where you get stuck in the door).mp4`):
- You ride into your house and dismount there, so the donkey is parked right in front of the door outside.
- When you come out, the keeper is stuck in the doorway. He can shuffle sideways but can't walk down past the
  donkey and its foals.
- Log: `Dismounted (indoors); ... donkey stays at (-1.57, 6.00, 22.16)`, the doorstep.

**Measured (log, second run, 2026-10-03):**
- The keeper arrives on the ground at the door (y 5.99, only terrain under him).
- Within 13 frames, before control is back, he's at y 6.96 and falling, then hovers at 6.66 above the doorstep. That
  is the physics shoving him out of something he overlapped.
- The parked donkey's restored position (-1.76, 6.00, 21.89) is exactly the door's exit point (-1.82, 5.99, 21.88).
- While he's indoors the donkey and foals sleep, their views parked ("6 in the pool"). When he comes out they wake
  where he stands.
- A parked view has the game's colliders back (`ReleaseView` restores them for the game's pool), including the
  `Obstacle` capsule (r 0.5, h 1.85). `TakeParkedView` switched it on that way, and `PrepareParts` switched the
  colliders off only on the next `LateUpdate`.
- A physics step runs in between, so the capsule threw him up.
- New views have the same gap: from the part loader's `CompleteVisualPartsLoad`, or pooled parts inside `Wgo.Spawn`,
  to `PrepareParts`.
- No navmesh hole and no other object of ours was found (`cut holes` never logged; nothing "(OURS)" under him).

**Fix (the cause):** `MuteColliders` switches every collider and navmesh cut of a view off the moment the view comes
alive:
- in `TakeParkedView`, before `SetActive(true)`;
- right after `Wgo.Spawn`;
- in `OnVisualPartsLoaded`.

The game's own values are still recorded once per view life (`RememberColliders` no longer records over them) and put
back for the game's pool.

**Earlier theories (kept: the measurement code stays until this is confirmed):**
- He walks by physics, not on the navmesh (`PlayerPhysicalBody`). His invisible edge walls rise wherever there's no
  walkable ground (layers 11 and 12) under a point 0.25 to 0.4 beside him (`PlayerInvisibleWalls`). So something
  solid, or a missing patch of ground, at the door holds him.
- **A mod donkey can leave three things behind that the view cleanup never touched:**
  1. **Navmesh cuts inside the view.** `PrepareParts` switched off only `NavMeshCutBoxCustom`. The donkey prefab's
     cuts are two `NavMeshCutSphereCustom`, each with a `SphereCollider`.
  2. **The part's baked cut prefabs.** The game spawns these outside the view, under `GlobalNavigationManager`
     (`GraphCustomNavMeshCutUnit`). They're tracked to the WgoData's position, held by its `UniqueId`, and spawned
     again on every `Wgo.UpdateWgoPartState`.
  3. **A forced navigation hole.** The `WgoData` constructor calls `SetupForcedNavigationHoleIfNeeded`. When the
     definition says `SpawnHoleForce`, a static cut and scene-update box are left where the data was made. Nothing
     removes them, since the game's removal runs only when a wgo is removed from its scene.
- Which of these the `npc_donkey` actually has is game data. The build measures it rather than assuming.

**Also removed (all three, measured, none found so far):**
- **Every in-view cut:** every `CustomNavMeshCutBakeSourceBase` in the view is switched off (box, sphere, mesh and
  planner kinds), and put back before the parts return to the game's pool.
  - Log, first view of a session: `Mount: navmesh cuts in the view of ...: N on (...), switched off`.
- **The cuts and holes outside the view:** `NavHoles.cs` removes them with
  `GlobalNavigationManager.RemoveCutUnit(UniqueId)` and stops the WgoData's cut tracking. This runs after
  `Wgo.Spawn`, in `PrepareParts`, once a second while a view exists, and before a view is destroyed.
  - Log, first 5 views that had any: `Mount: ... cut holes in the keeper's navmesh (cut / scene update / baked cut prefabs; the game's own setting for npc_donkey: ...); removed`.
- **Measurement:** after every door, `[indoors] solid colliders within 2.5 of the keeper at ...` lists what is solid
  around him where he arrives: path, layer and distance.
  - If he's still stuck, that line names what holds him.
  - If the hole lines never appear and he's stuck anyway, the cause is elsewhere: read that line.

## In-game test list

Set "Baby donkeys" to 2 or more and "Baby donkeys come indoors" on.

1. **The door bug:**
   - Ride into the house so the donkey is parked at the door, come back out, and walk away down past the donkey.
     You shouldn't get stuck.
   - Send the log either way. The lines `navmesh cuts in the view`, `cut holes in the keeper's navmesh` and
     `[indoors] solid colliders within 2.5` say what was there.
2. **In on foot:**
   - With the foals around the donkey near the house, walk in. Once the fade clears, the foals are next to you
     inside, not appearing in view.
   - Then walk around the room. They trot after you.
3. **In riding:** ride to the door and go in. The donkey stays at the door outside and the foals come in.
4. **Out:**
   - Go back out. The foals are next to you at the door, then go back to the donkey.
5. **Far away:** with the donkey and foals more than 10 units from the door, go in. The foals stay outside.
6. **Off:**
   - With the setting off, they stay outside.
   - Switching it off while they're inside sends them back to the donkey: they catch up out of sight once you leave
     the room.
7. **Save and load indoors:** after loading, the foals inside are with you again, and they come out with you.
8. **Another scene and the church:** check a door that loads another scene, and the church. Do they come along, and
   does the timing line say "control not back yet"?
9. **Small rooms:** does any foal stand stuck where it lost you? And can five foals box you in?
