using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The chest-on-the-cart probe as self-tests (docs/portable-chest.md §6): each scenario builds its own empty
    // chest_rough beside the keeper the way the game builds one (a WgoData added to the scene), runs one of the
    // three moves the design rests on through ChestProbe's pieces, checks the state that decides what is seen and
    // saved, and removes the chest again (empty, so the game's plain removal loses nothing). Nothing is saved.
    internal static partial class SelfTest
    {
        internal const string TestChestId = "chest_rough";
        internal const string TestItemId = "nails_bronze";

        internal static WgoData chest;
        internal static Wgo chestView;

        // A chest at a free spot near the keeper, with its view. False (and the reason logged) when it can't be made.
        internal static IEnumerator MakeChest()
        {
            chest = null;
            chestView = null;
            if (!Mount.Instance.IsInGame(out PlayerController pc, out GameScene scene))
            {
                Fail("not in a game");
                yield break;
            }
            if (GameBalance.Me.GetDataOrNull<WGODef>(TestChestId) == null)
            {
                Fail($"the game has no '{TestChestId}'");
                yield break;
            }
            // A spot inside a building zone first (the keeper may stand at a zone's edge: run 2026-10-10), else any
            // free spot, which then gives the "in no zone" warning below.
            Vector3? spot = Spot(Keeper(), 3f, p => ZoneAt(p, scene) != null) ?? Spot(Keeper(), 3f);
            if (spot == null)
            {
                Warn("no free spot near the keeper for a test chest; skipped");
                yield break;
            }
            WgoData d = new WgoData(TestChestId, spot.Value, scene.Id);
            Wgo v = scene.AddWgoData(d);
            yield return Until(() => v != null && v.MainWgoPart != null, 5f);
            if (!waited)
            {
                Fail("the test chest never got its view");
                MainGame.WorldData.RemoveWgoDataFromGameScene(d);
                yield break;
            }
            if (d.WorldZoneData == null)
            {
                Warn($"the spot {spot.Value:F1} is in no zone; the zone checks need one; skipped");
                MainGame.WorldData.RemoveWgoDataFromGameScene(d);
                yield break;
            }
            chest = d;
            chestView = v;
            Info($"test chest {d.UniqueId} at {d.Position:F2} in zone '{d.WorldZoneData.id}', {d.Inventory.Data.InventorySize} slots");
        }

        internal static void RemoveChest()
        {
            if (chest == null)
            {
                return;
            }
            try
            {
                // Still on a test cart (a failed step): set down first, so the removal below acts on a shown, zoned chest
                // and the later CleanUp of the cart finds nothing aboard.
                foreach (Mount.Instance i in Mount.All)
                {
                    if (i.ChestRig.Chest == chest)
                    {
                        i.ChestRig.SetDownAt(i.CartPoint(), "self-test cleanup");
                    }
                }
                chest.Inventory.Clear(); // the game's removal drops nothing, so nothing may be inside
                MainGame.WorldData.RemoveWgoDataFromGameScene(chest);
                Check(ChestProbe.ZoneEntries(chest) == 0 && GameScene.GetWgoViewGlobal(chest.UniqueId) == null, "the test chest is gone from the zone lists and has no view");
            }
            catch (Exception e)
            {
                Fail("removing the test chest threw: " + e.Message);
            }
            chest = null;
            chestView = null;
        }

        // The view's private state, for a failed drawn check (docs/measure-before-visual-fixes): what the game's own
        // visibility pass left it in.
        internal static string ViewState(Wgo v)
        {
            if (v == null || !v)
            {
                return "no view";
            }
            string F(string name)
            {
                try
                {
                    // System.Type's reflection: a diagnostic over names given at the call, not a dependency the manifest lists.
                    return typeof(Wgo).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(v)?.ToString() ?? "?";
                }
                catch
                {
                    return "?";
                }
            }
            int on = 0, all = 0;
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>(true))
            {
                all++;
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    on++;
                }
            }
            return $"active {v.gameObject.activeSelf}, isVisible {F("isVisible")}, chunk state {F("chunkVisibilityState")}, spawnCompleted {F("spawnCompleted")}, partsLoaded {F("wgoPartsLoaded")}, partsLoading {F("wgoPartsLoading")}, hidden {v.Data?.IsHidden}, registered {v.RegisteredInChunker}, ignoreChunkVisibility {((IChunkableObject)v).IgnoreChunkVisibility}, renderers on {on}/{all}, at {v.transform.position:F2}";
        }

        // What the camera and the chunk system make of a view: the camera (position, angles), where the view's spot
        // lands on screen (viewport 0..1 is on screen), whether the view's chunk bounds pass Unity's frustum test
        // against that camera, and the verdict of each chunk holding it.
        internal static string CameraFacts(Wgo v)
        {
            try
            {
                Camera cam = CameraSystem.Instance?.MainCamera?.Camera ?? Camera.main;
                if (cam == null)
                {
                    return "no camera";
                }
                Bounds b = ChestProbe.ViewBounds(v);
                Vector3 vp = cam.WorldToViewportPoint(v.transform.position);
                bool inFrustum = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
                return $"camera '{cam.name}' at {cam.transform.position:F1} angles {cam.transform.eulerAngles:F0} {(cam.orthographic ? $"ortho {cam.orthographicSize:F1}" : $"fov {cam.fieldOfView:F0}")} near {cam.nearClipPlane:F1} far {cam.farClipPlane:F0}; the spot on screen at viewport ({vp.x:F2}, {vp.y:F2}, depth {vp.z:F1}); view bounds centre {b.center:F1} size {b.size:F1} {(inFrustum ? "pass" : "FAIL")} the frustum test; {ChestProbe.ChunkStates(v)}";
            }
            catch (Exception e)
            {
                return "camera facts failed: " + e.Message;
            }
        }

        // Inside the camera's viewport with a margin (the chunk system culls what is outside it).
        internal static bool OnScreen(Vector3 p)
        {
            Camera cam = CameraSystem.Instance?.MainCamera?.Camera ?? Camera.main;
            if (cam == null)
            {
                return false;
            }
            Vector3 vp = cam.WorldToViewportPoint(p);
            return vp.x > 0.15f && vp.x < 0.85f && vp.y > 0.15f && vp.y < 0.85f;
        }

        internal static bool Drawn(Wgo v)
        {
            if (v == null || !v.gameObject.activeSelf || v.MainWgoPart == null)
            {
                return false;
            }
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>())
            {
                if (r.enabled)
                {
                    return true;
                }
            }
            return false;
        }

        // Hide, move 4 m, show, chunk re-registration: the view drawn at the new spot and held by a chunk around it;
        // then the keeper far away and back (chunk culling), and the chest moved back.
        internal static IEnumerator ChestHideMoveShow()
        {
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            // The new spot must be ON SCREEN for the drawn checks to mean anything: the game's camera is orthographic
            // with a half-height of 1.8 units at a 53° tilt (measured 2026-10-09), so the test chest 3 m beside the
            // keeper already sits at the screen's edge and 4 m further is below it, culled by the chunk system as it
            // should be (two runs failed on exactly that). So: half-way to the keeper, then on past the keeper, whichever
            // is free ground and lands inside the viewport.
            Vector3 from = chest.Position;
            Vector3 keeper = Keeper();
            Vector3? pick = null;
            foreach (Vector3 c in new[] { keeper + (from - keeper) * 0.5f, keeper + (keeper - from) * 0.5f, keeper + (from - keeper) * 0.3f })
            {
                Vector3 q = c;
                float g = Mount.Instance.GroundHeight(q, out _);
                if (float.IsNaN(g))
                {
                    continue;
                }
                q.y = g;
                if (FreeSpot(q) && OnScreen(q))
                {
                    pick = q;
                    break;
                }
            }
            if (pick == null)
            {
                Warn("no free spot on screen near the keeper to move the chest to; skipped");
                RemoveChest();
                yield break;
            }
            Vector3 to = pick.Value;
            var sb = new StringBuilder();
            Info("at the old spot: " + ViewState(chestView) + " | " + CameraFacts(chestView));
            ChestProbe.Relocate(chest, chestView, to, sb);
            yield return Frames(2);
            Check(chest.Position == to && chestView.transform.position == to, $"data and view at the new spot {to:F2}");
            if (!Drawn(chestView))
            {
                Info("not drawn 2 frames after the show: " + ViewState(chestView) + " | " + CameraFacts(chestView));
                yield return Seconds(1f);
                Info("1 s later: " + ViewState(chestView));
                LazySingleton<ChunkManager>.Instance?.RequestVisibilityRecheck(chestView);
                yield return Frames(3);
                Info("after asking the chunk system for a recheck: " + ViewState(chestView) + " | " + CameraFacts(chestView));
            }
            Check(!chest.IsHidden && Drawn(chestView), "the view is active with its parts drawn after hide and show");
            Check(chestView.RegisteredInChunker, "registered in the chunk system");
            Vector3 bc = ChestProbe.ViewBoundsCenter(chestView);
            bool around = ChestProbe.ChunkHolds(chestView, bc, out int chunks, out string where);
            Check(around, chunks > 0 ? $"held by {chunks} chunk(s), one of them around the new spot ({where}; view bounds centre {bc:F1})" : "held by no chunk");
            Check(chest.WorldZoneData != null && ChestProbe.ZoneEntries(chest) == 1, $"zone membership untouched by the move (still '{chest.WorldZoneData?.id}', one entry)");

            // Off screen and back. The keeper's own spot is kept; a blocked far spot skips this part.
            Vector3 here = Keeper();
            Vector3? far = Spot(here, 45f);
            if (far == null)
            {
                Info("no free spot 45 m away; the off-screen part skipped");
            }
            else
            {
                yield return MoveKeeper(far.Value, homeFacing);
                if (moved)
                {
                    yield return Seconds(1.5f);
                    Info($"with the keeper 45 m away the view is {(chestView.gameObject.activeSelf ? "still active" : "culled")}");
                    yield return MoveKeeper(here, homeFacing);
                    yield return Until(() => Drawn(chestView), 5f);
                    if (!waited)
                    {
                        Info("not drawn back near it: " + ViewState(chestView) + " | " + CameraFacts(chestView));
                    }
                    Check(waited, "back near it, the view is drawn again at the new spot");
                }
            }

            ChestProbe.Relocate(chest, chestView, from, sb);
            yield return Frames(2);
            Check(chest.Position == from && Drawn(chestView) && chestView.RegisteredInChunker, "moved back: drawn and registered at the old spot");
            RemoveChest();
        }

        // Out of every zone and back in through the game's own order: the lists, the crafting source and the
        // navigation hole follow, with no duplicate entry, and an item inside stays the same object.
        internal static IEnumerator ChestZoneRoundTrip()
        {
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            // A stackable item is stored as a fresh object by the game's own add (Item.AddItemToInventory splits or
            // recreates it), so the identity that must hold across the moves is the stored object's, taken after the add.
            bool added = chest.Inventory.AddItemToInventory(new Item(TestItemId));
            Check(added && chest.Inventory.Data.Inventory.Count == 1, $"one {TestItemId} put into the chest");
            Item it = chest.Inventory.Data.Inventory.Count > 0 ? chest.Inventory.Data.Inventory[0] : null;
            WorldZoneData zone = chest.WorldZoneData;
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(chest.WorldId);
            Check(new MultiInventory(zone).inventoryList.Contains(chest.Inventory) && ChestProbe.HasNavCut(chest), "before: a crafting source of its zone, with a navigation hole");

            ChestProbe.ZoneRemoveAll(sd, chest);
            yield return null;
            Check(ChestProbe.ZoneEntries(chest) == 0 && chest.WorldZoneData == null, "out: in no zone list");
            Check(!new MultiInventory(zone).inventoryList.Contains(chest.Inventory), "out: not a crafting source");
            Check(!ChestProbe.HasNavCut(chest), "out: no navigation hole");
            Check(chest.Inventory.Data.Inventory.Count == 1 && ReferenceEquals(chest.Inventory.Data.Inventory[0], it), "out: the item is still there, the same object");

            WorldZoneData back = ChestProbe.ZoneAddByPriority(sd, chest);
            yield return null;
            Check(ReferenceEquals(back, zone), $"in: the game's order puts it back into '{back?.id}' (was '{zone.id}')");
            Check(ChestProbe.ZoneEntries(chest) == 1 && ReferenceEquals(chest.WorldZoneData, zone), "in: exactly one list entry, link set");
            Check(new MultiInventory(zone).inventoryList.Contains(chest.Inventory), "in: a crafting source again");
            Check(ChestProbe.HasNavCut(chest), "in: the navigation hole is back");
            Check(chest.Inventory.Data.Inventory.Count == 1 && ReferenceEquals(chest.Inventory.Data.Inventory[0], it), "in: the item is still there, the same object");

            // A second add without a remove would duplicate: the design always removes first. Shown, then undone. The
            // game's RemoveWgoData takes one entry per call, so ZoneRemoveAll asks until none is left.
            zone.TryAddWgoData(chest);
            int dup = ChestProbe.ZoneEntries(chest);
            ChestProbe.ZoneRemoveAll(sd, chest);
            int afterRemove = ChestProbe.ZoneEntries(chest);
            ChestProbe.ZoneAddByPriority(sd, chest);
            Check(dup == 2 && afterRemove == 0 && ChestProbe.ZoneEntries(chest) == 1, $"a bare second add does duplicate ({dup} entries), remove-all leaves {afterRemove}, then add gives {ChestProbe.ZoneEntries(chest)}");

            if (it != null) chest.Inventory.RemoveItemFromInventoryByUID(it);
            RemoveChest();
        }

        // Ten stand-ins made and destroyed: never in the scene, no collider active, not interactable, drawn; the town
        // quality and the pool's idle parts unchanged. The pool's borrowed count is reported, not judged: a temp
        // object's parts are destroyed, not returned, by the game's own build preview too.
        internal static IEnumerator ChestStandIns()
        {
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            if (!Mount.Instance.IsInGame(out PlayerController pc, out GameScene scene))
            {
                Fail("not in a game");
                RemoveChest();
                yield break;
            }
            Vector3? spot = Spot(Keeper(), 5f) ?? Spot(Keeper(), 2f);
            if (spot == null)
            {
                Warn("no free spot for a stand-in; skipped");
                RemoveChest();
                yield break;
            }
            int q0 = MainGame.Instance.GameSave.townSystem.Quality;
            ChestProbe.PoolCounts(chest, out string key, out int idle0, out int borrowed0);
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(chest.WorldId);
            int bad = 0;
            var why = new List<string>();
            for (int k = 0; k < 10 && !abort; k++)
            {
                Wgo w = ChestProbe.SpawnStandIn(chest, spot.Value, scene.Id, scene.transform, out WgoData temp, out int cols);
                yield return Until(() => w != null && w.MainWgoPart != null, 3f);
                string problem = null;
                if (!waited) problem = "no parts";
                else if (sd.wgoDataList.Contains(temp)) problem = "in the scene list";
                else if (temp.IsInteractable) problem = "interactable";
                else if (!Drawn(w)) problem = "not drawn";
                else
                {
                    foreach (Collider c in w.GetComponentsInChildren<Collider>(includeInactive: true))
                    {
                        if (c.gameObject.activeInHierarchy && c.enabled)
                        {
                            problem = "a collider is active: " + c.name;
                            break;
                        }
                    }
                }
                if (problem != null)
                {
                    bad++;
                    if (why.Count < 3) why.Add($"#{k + 1}: {problem}");
                }
                if (w != null)
                {
                    UnityEngine.Object.Destroy(w.gameObject);
                }
                yield return null;
            }
            Check(bad == 0, bad == 0 ? "ten stand-ins: drawn, no collider, not interactable, never in the scene list" : $"{bad} stand-in(s) wrong: {string.Join("; ", why)}");
            int q1 = MainGame.Instance.GameSave.townSystem.Quality;
            Check(q0 == q1, $"town quality unchanged ({q0})");
            ChestProbe.PoolCounts(chest, out _, out int idle1, out int borrowed1);
            // A temp object destroys its parts even when they came from the pool (Wgo.DestroyParts: pooled key but
            // isTempObject), so idle parts are taken when the pool has some (seen 2026-10-09: 2 -> 0 after an earlier
            // scenario left two in the pool); the game's own build preview does the same. What must never happen is a
            // stand-in's parts, with their collider overrides, going INTO the pool.
            Check(idle1 <= idle0, $"no stand-in part went into the pool (idle parts for '{key}': {idle0} -> {idle1}, borrowed {borrowed0} -> {borrowed1})");
            Info($"the pool's borrowed count for '{key}': {borrowed0} -> {borrowed1} (a temp object's parts are destroyed, not returned; the game's build preview does the same)");
            Info(ChestProbe.Colliders(chest));
            RemoveChest();
        }

        // The ride (step 1): a test donkey with its cart, ridden; the chest's handler offers the action key and the press
        // lifts the chest; aboard it is hidden, zone-less, following the cart, the cart full for everything else and the
        // contents untouched; refusals for cargo aboard and for a chest that isn't a plain storage chest; set down where
        // cargo goes, shown and zoned, the cart open again; the chest follows the cart through unhitch and hitch, stays
        // on a parked cart, is set down at the cart by the save hook and when the cart is removed.
        internal static IEnumerator ChestRide()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle h = Spawn(near, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            yield return Until(() => h.IsReady, 6f);
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            WgoData other = null;
            try
            {
                h.ComeToKeeper(true, showMessage: false);
                yield return Until(() => h.IsReady, 4f);
                MountResult ride = h.Ride(showMessage: false);
                Check(ride.Ok && Mount.Ridden == h.I, $"riding the test cart{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok)
                {
                    yield break;
                }
                yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 4f);
                Mount.Instance r = h.I;
                CartChest.Rig rig = r.ChestRig;
                Inventory cartInv = r.CargoRig.Inv;
                int slots0 = cartInv != null ? cartInv.Data.InventorySize : -1;
                Wgo v = GameScene.GetWgoViewGlobal(chest.UniqueId);
                IWGOInteractionHandler hnd = v.InteractionHandler;
                Check(hnd.HasInteraction2(), "the chest offers the action key while riding the cart");
                string rows = string.Join(" | ", ((hnd as WGOInteractionHandlerBase)?.GetInteractionInfos()?.list ?? new List<InteractionInfo>()).ConvertAll(x => x.text));
                Check(rows.Contains("Load onto the cart"), $"its hint rows name the load ({rows})");

                // Refusals.
                Check(cartInv != null && cartInv.AddItemToInventory(new Item(TestItemId)), "a nail put on the cart");
                Check(rig.WhyNotPickUp(chest, PC) == "Set the cargo down first.", "refused while cargo is on the cart");
                if (cartInv != null && cartInv.Data.Inventory.Count > 0)
                {
                    cartInv.RemoveItemFromInventoryByUID(cartInv.Data.Inventory[0]);
                }
                if (Mount.Instance.IsInGame(out _, out GameScene scene) && GameBalance.Me.GetDataOrNull<WGODef>("sawmill_chest") != null)
                {
                    Vector3? s2 = Spot(Keeper(), 6f);
                    if (s2 != null)
                    {
                        other = new WgoData("sawmill_chest", s2.Value, scene.Id);
                        scene.AddWgoData(other);
                        yield return Frames(2);
                        Check(rig.WhyNotPickUp(other, PC) == "Only a plain storage chest goes on the cart.", "a sawmill chest is refused");
                        // Its work done: gone before the set-down, so it never stands in the keeper's way to the spot.
                        MainGame.WorldData.RemoveWgoDataFromGameScene(other);
                        other = null;
                    }
                }

                // Pick up through the handler, as the key does; the API events along the way.
                int loadedEvents = 0, unloadedEvents = 0;
                string lastEvent = null;
                Action<ChestInfo> onLoaded = c => { loadedEvents++; lastEvent = "loaded " + c; };
                Action<ChestInfo> onUnloaded = c => { unloadedEvents++; lastEvent = "unloaded " + c; };
                Mounts.ChestLoaded += onLoaded;
                Mounts.ChestUnloaded += onUnloaded;
                chest.Inventory.AddItemToInventory(new Item(TestItemId));
                chest.Inventory.AddItemToInventory(new Item(TestItemId));
                Inventory inv0 = chest.Inventory;
                int count0 = inv0.Data.Inventory.Count, total0 = 0;
                foreach (Item it in inv0.Data.Inventory) total0 += it.Count;
                bool pressed = hnd.Interact2(PC);
                Check(pressed && rig.Aboard && ReferenceEquals(rig.Chest, chest), "the action press lifts the chest onto the cart");
                Check(loadedEvents == 1 && h.Chest != null && h.Chest.UniqueId == chest.UniqueId.Id && h.Chest.Items == count0 && ReferenceEquals(h.Chest.Cart, h), $"ChestLoaded raised once and the handle reports the chest ({lastEvent})");
                if (!rig.Aboard)
                {
                    yield break;
                }
                Check(chest.IsHidden && ChestProbe.ZoneEntries(chest) == 0 && chest.WorldZoneData == null && !ChestProbe.HasNavCut(chest), "aboard: hidden, in no zone, no navigation hole");
                // The zone guard: the game's own re-zoning (a fighting level's content load does this to every object) can't take it.
                WorldZoneData yard = MainGame.WorldData.GetGameSceneDataById(chest.WorldId)?.worldZones.Find(z => z.wgoDataList.Count > 0 && z.IsContainer);
                Check(yard != null && !yard.TryAddWgoData(chest) && ChestProbe.ZoneEntries(chest) == 0, "aboard: a zone's TryAddWgoData is refused for the riding chest");
                Check(cartInv != null && cartInv.Data.InventorySize == 0 && !PC.PlayerData.HasFreeOverheadSlot, "aboard: the cart is full for everything else (0 slots, the head gate says no)");
                Check(ReferenceEquals(chest.Inventory, inv0) && Same(inv0, count0, total0), "aboard: the contents are the same object, untouched");
                // The stand-in (step 2): under the cargo holder, drawn, no collider, measured and listed for the panel.
                yield return Until(() => rig.StandIn != null && rig.StandIn.MainWgoPart != null && Drawn(rig.StandIn), 4f);
                Check(waited, "aboard: a stand-in of the chest is drawn");
                yield return Frames(2);
                Wgo si = rig.StandIn;
                Check(si != null && si.transform.parent != null && si.transform.parent.name == Cargo.ContainerName, "aboard: the stand-in sits under the cargo holder");
                // Step 3: only the interactable trigger is live on the stand-in (for "[E] Open" / "[F] Set down" on foot); the
                // solid, build-area and dock-point colliders stay off.
                bool solid = false, trigger = false;
                if (si != null)
                {
                    foreach (Collider c in si.GetComponentsInChildren<Collider>(includeInactive: true))
                    {
                        if (!c.gameObject.activeInHierarchy || !c.enabled) continue;
                        if (si.MainWgoPart != null && si.MainWgoPart.InteractableColliders != null && si.MainWgoPart.InteractableColliders.Contains(c)) trigger = true; else solid = true;
                    }
                }
                Check(si != null && !solid && trigger && si.Data.isTempObject && si.Data.IsInteractable, "aboard: the stand-in has its interactable trigger on and no other collider, a temp object the scan can take");
                Check(si != null && si.InteractionHandler is ChestInteractionHandler && si.InteractionHandler.HasInteraction2() && string.Join("|", ((si.InteractionHandler as WGOInteractionHandlerBase)?.GetInteractionInfos()?.list ?? new List<InteractionInfo>()).ConvertAll(x => x.text)).Contains("Set down"), "aboard: the stand-in's handler offers the action key with a \"Set down\" row");
                List<Cargo.PlacedItem> placed = r.CargoRig.Placed;
                Check(placed.Count == 1 && placed[0].Id == chest.id && placed[0].Alone && placed[0].Box.x > 0.2f && placed[0].Box.y > 0.1f, placed.Count == 1 ? $"aboard: the layout lists the chest alone, box {placed[0].Box:F2} at {placed[0].Pos:F2}" : $"aboard: the layout lists {placed.Count} item(s), not the chest alone");
                Vector3 k = Keeper();
                yield return MoveKeeper(k + new Vector3(2f, 0f, 0f), homeFacing);
                yield return Frames(3);
                Check(Vector3.Distance(chest.Position, r.CartPoint()) < 0.05f, $"aboard: the chest's position follows the cart ({chest.Position:F2} vs {r.CartPoint():F2})");

                // Set down. The spot is behind the cart, and the yard has fences, buildings and the keeper's own test
                // objects, so a stand can be refused by the reach gate (seen 2026-10-09: "I can't reach that spot", the
                // feature working as designed); then the keeper rides to another stand and tries again, a few times.
                bool down = false;
                string why = null;
                foreach (Vector3 stand in new[] { Keeper(), k, k + new Vector3(-2f, 0f, 0f), k + new Vector3(0f, 0f, 2f), k + new Vector3(0f, 0f, -2f) })
                {
                    if (Vector3.Distance(Keeper(), stand) > 0.1f)
                    {
                        yield return MoveKeeper(stand, homeFacing);
                        yield return Frames(3);
                    }
                    down = rig.SetDown(PC, out why);
                    if (down)
                    {
                        break;
                    }
                    Info($"set-down refused from the stand {Keeper():F1} ({why}); trying another stand");
                }
                Check(down, $"set down where cargo goes{(down ? "" : " (" + why + ")")}");
                yield return Frames(2);
                Check(!rig.Aboard && !chest.IsHidden && ChestProbe.ZoneEntries(chest) == 1 && chest.WorldZoneData != null && ChestProbe.HasNavCut(chest), $"down: shown, in zone '{chest.WorldZoneData?.id}' with its navigation hole");
                Check(Drawn(GameScene.GetWgoViewGlobal(chest.UniqueId)), "down: drawn");
                Check(rig.StandIn == null && r.CargoRig.Placed.Count == 0, "down: the stand-in is gone and the layout is empty");
                Check(unloadedEvents == 1 && h.Chest == null, $"ChestUnloaded raised once and the handle reports no chest ({lastEvent})");
                Mounts.ChestLoaded -= onLoaded;
                Mounts.ChestUnloaded -= onUnloaded;
                Check(cartInv != null && cartInv.Data.InventorySize == slots0 && PC.PlayerData.HasFreeOverheadSlot, $"down: the cart has its {slots0} slots again");
                Check(ReferenceEquals(chest.Inventory, inv0) && Same(inv0, count0, total0), "down: the contents are the same object, untouched");

                // Parked, handed over with the cart, back.
                Check(rig.PickUp(chest, PC, out why), $"lifted again{(why != null ? " (" + why + ")" : "")}");
                Mounts.Dismount("self-test");
                yield return Frames(2);
                Check(rig.Aboard && chest.IsHidden, "dismounted: the chest stays on the parked cart");
                MountHandle left = Mounts.Unhitch(h);
                Check(left != null && left.Exists && !rig.Aboard && left.I.ChestRig.Aboard, "unhitched: the chest went with the free cart");
                yield return Until(() => h.IsReady && (left == null || left.IsReady), 4f);
                if (left != null)
                {
                    MountResult back = Mounts.Hitch(h, left);
                    Check(back.Ok && rig.Aboard, $"hitched again: the chest came back with the cart{(back.Ok ? "" : " (" + back.Reason + ")")}");
                    yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 4f);
                    Check(h.I.CargoRig.Inv != null && h.I.CargoRig.Inv.Data.InventorySize == 0, "the re-made cart inventory has 0 slots while the chest rides");
                    yield return Until(() => rig.StandIn != null && Drawn(rig.StandIn), 4f);
                    Check(waited, "the re-made view draws the stand-in again");
                }

                // The save hook: the chest's data stands at the cart, shown and zoned, named in the side file; hidden and
                // zone-less again after the save; the rig and the stand-in untouched throughout.
                Wgo siBefore = rig.StandIn;
                Mount.LendCargoToSave();
                yield return Frames(2);
                Check(rig.Aboard && !chest.IsHidden && chest.WorldZoneData != null && Vector3.Distance(chest.Position, h.I.CartPoint()) < 1.5f, "for the save the chest stands at the cart, shown and zoned, still the cart's");
                string side = Mount.BuildSideText("selftest", "saved at test time", 42, new List<Mount.Instance> { h.I }, false, out _);
                Check(side.Contains(chest.UniqueId.Id), "the side file names the chest standing at the cart");
                Mount.TakeCargoBackFromSave();
                yield return Frames(2);
                Check(rig.Aboard && chest.IsHidden && ChestProbe.ZoneEntries(chest) == 0 && ReferenceEquals(rig.StandIn, siBefore) && h.I.CargoRig.Inv.Data.InventorySize == 0, "after the save the chest rides again, hidden and zone-less, the same stand-in, the cart still at 0 slots");

                // Removed with the chest aboard: set down at the cart.
                h.ComeToKeeper(true, showMessage: false);
                yield return Until(() => h.IsReady, 4f);
                if (!rig.Aboard)
                {
                    Check(rig.PickUp(chest, PC, out why), $"lifted once more{(why != null ? " (" + why + ")" : "")}");
                }
                Vector3 cartAt = h.I.CartPoint();
                h.Remove();
                yield return Frames(2);
                Check(!chest.IsHidden && chest.WorldZoneData != null && Vector3.Distance(chest.Position, cartAt) < 1.5f, "the cart removed: the chest stands where it was, shown and zoned");
                Check(ReferenceEquals(chest.Inventory, inv0) && Same(inv0, count0, total0), "after everything: the contents are the same object, untouched");
            }
            finally
            {
                if (other != null)
                {
                    try { MainGame.WorldData.RemoveWgoDataFromGameScene(other); } catch (Exception e) { Warn("removing the sawmill chest threw: " + e.Message); }
                }
                RemoveChest(); // also on an early exit: the test chest never stays in the player's world
            }
        }

        internal static bool Same(Inventory inv, int count, int total)
        {
            if (inv.Data.Inventory.Count != count)
            {
                return false;
            }
            int t = 0;
            foreach (Item it in inv.Data.Inventory) t += it.Count;
            return t == total;
        }

        // The backstops (docs/portable-chest.md §4, "a hidden chest in a save is the one unrecoverable state"): a chest
        // left hidden and zone-less with no cart owning it, as some failure the code didn't foresee would leave it, is
        // shown and zoned again by the pre-save pass and by the after-load repair. In normal play neither ever runs.
        internal static IEnumerator ChestBackstops()
        {
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            try
            {
                chest.Inventory.AddItemToInventory(new Item(TestItemId));
                Inventory inv0 = chest.Inventory;
                int count0 = inv0.Data.Inventory.Count, total0 = 0;
                foreach (Item it in inv0.Data.Inventory) total0 += it.Count;
                GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(chest.WorldId);
                Wgo v = GameScene.GetWgoViewGlobal(chest.UniqueId);

                // Broken state 1: hidden and zone-less, nobody's.
                Info("the next \"Please report this line\" warnings are this test's own: it breaks a chest on purpose");
                ChestMoves.ZoneRemoveAll(sd, chest);
                ChestMoves.Hide(chest, v);
                yield return Frames(2);
                Check(chest.IsHidden && ChestProbe.ZoneEntries(chest) == 0 && CartChest.RigOf(chest) == null, "set up: a hidden, zone-less chest no cart owns");
                // In the game's own order: the save hook first (a player's chest riding a cart stands at its cart through
                // its own path), then the pass; then the take-back. The pass alone would meet that chest still hidden
                // and warn about it (seen 2026-10-09 with the player's chest on a free cart).
                Mount.LendCargoToSave();
                CartChest.EnsureNoneHiddenForSave();
                Mount.TakeCargoBackFromSave();
                yield return Frames(2);
                Check(!chest.IsHidden && ChestProbe.ZoneEntries(chest) == 1 && chest.WorldZoneData != null, "the pre-save pass shows and zones it");
                yield return Until(() => Drawn(GameScene.GetWgoViewGlobal(chest.UniqueId)), 5f);
                Check(waited, "and it is drawn again");
                Check(ReferenceEquals(chest.Inventory, inv0) && Same(inv0, count0, total0), "contents the same object, untouched");

                // Broken state 2: the same, repaired after a load.
                ChestMoves.ZoneRemoveAll(sd, chest);
                ChestMoves.Hide(chest, v);
                yield return Frames(2);
                int repaired = CartChest.RepairHiddenChests();
                yield return Frames(2);
                Check(repaired >= 1 && !chest.IsHidden && ChestProbe.ZoneEntries(chest) == 1 && chest.WorldZoneData != null, $"the after-load repair shows and zones it ({repaired} repaired)");
                yield return Until(() => Drawn(GameScene.GetWgoViewGlobal(chest.UniqueId)), 5f);
                Check(waited, "and it is drawn again");
                Check(ReferenceEquals(chest.Inventory, inv0) && Same(inv0, count0, total0), "contents the same object, untouched");

                // A shown chest is left alone by both.
                int before = ChestProbe.ZoneEntries(chest);
                Mount.LendCargoToSave();
                CartChest.EnsureNoneHiddenForSave();
                Mount.TakeCargoBackFromSave();
                int repaired2 = CartChest.RepairHiddenChests();
                Check(repaired2 == 0 && !chest.IsHidden && ChestProbe.ZoneEntries(chest) == before, "a chest standing normally is left alone by both");
                chest.Inventory.RemoveItemFromInventoryByUID(chest.Inventory.Data.Inventory[0]);
            }
            finally
            {
                RemoveChest();
            }
        }

        // The save as the game writes it (docs/portable-chest.md §7a): with a chest riding a test cart, the real save
        // steps run (the chests stood at their carts, the pre-serialization pass), the game's own save object is
        // serialized with the game's own serializer, in memory, and deserialized into a separate copy; the chest in that
        // copy is what a game WITHOUT the mod would load: visible, zoned, interactable, standing by the cart, the same
        // items. Then the take-back, and the chest rides again. Also the fail-safe: locking the feature with a chest
        // aboard sets it down at the cart and refuses further pickups; the lock is undone for the session afterwards.
        internal static IEnumerator ChestSaveImage()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle h = Spawn(near, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            yield return Until(() => h.IsReady, 6f);
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            try
            {
                h.ComeToKeeper(true, showMessage: false);
                yield return Until(() => h.IsReady, 4f);
                MountResult ride = h.Ride(showMessage: false);
                if (!ride.Ok)
                {
                    Fail("riding the test cart: " + ride.Reason);
                    yield break;
                }
                yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 4f);
                CartChest.Rig rig = h.I.ChestRig;
                chest.Inventory.AddItemToInventory(new Item(TestItemId));
                chest.Inventory.AddItemToInventory(new Item(TestItemId));
                int count0 = chest.Inventory.Data.Inventory.Count, total0 = 0;
                foreach (Item it in chest.Inventory.Data.Inventory) total0 += it.Count;
                Check(rig.PickUp(chest, PC, out string why), $"lifted{(why != null ? " (" + why + ")" : "")}");
                if (!rig.Aboard)
                {
                    yield break;
                }
                yield return Frames(2);

                // The real save steps, then the game's serializer on the game's save object, in memory.
                Mount.LendCargoToSave();
                CartChest.EnsureNoneHiddenForSave();
                GameSave live = MainGame.Instance.GameSave;
                byte[] bytes = null;
                GameSave copy = null;
                string error = null;
                try
                {
                    object serializer = AccessTools.Property(typeof(SaveSystem), "OdinBinaryFileSerializer")?.GetValue(null)
                        ?? AccessTools.Field(typeof(SaveSystem), "odinBinaryFileSerializer")?.GetValue(null);
                    var odin = serializer as OdinBinaryFileSerializer;
                    if (odin == null)
                    {
                        error = "the game's serializer was not found";
                    }
                    else
                    {
                        live.OnBeforeSerialize();
                        bytes = odin.Serialize(live);
                        copy = odin.Deserialize<GameSave>(bytes);
                    }
                }
                catch (Exception e)
                {
                    error = e.GetType().Name + ": " + e.Message;
                }
                Check(error == null && bytes != null && bytes.Length > 0 && copy != null, error == null ? $"the save object serialized and read back ({(bytes?.Length ?? 0) / 1024} KB)" : "serializing the save object: " + error);
                if (copy != null)
                {
                    WgoData saved = null;
                    string savedScene = null;
                    foreach (GameSceneData sd in copy.worldData.gameSceneDataList)
                    {
                        foreach (WgoData d in sd.wgoDataList)
                        {
                            if (d != null && d.UniqueId != null && d.UniqueId.Id == chest.UniqueId.Id)
                            {
                                saved = d;
                                savedScene = sd.id;
                            }
                        }
                    }
                    Check(saved != null, "the chest is in the save's world (its scene's list)");
                    if (saved != null)
                    {
                        Check(!saved.IsHidden, "in the save it is not hidden");
                        Check(saved.IsInteractable, "in the save it is interactable");
                        int savedCount = saved.Inventory?.Data?.Inventory?.Count ?? -1, savedTotal = 0;
                        if (saved.Inventory?.Data?.Inventory != null) foreach (Item it in saved.Inventory.Data.Inventory) savedTotal += it.Count;
                        Check(savedCount == count0 && savedTotal == total0, $"in the save it holds the same items ({savedCount} stack(s), {savedTotal} in all)");
                        Check(Vector3.Distance(saved.Position, h.I.CartPoint()) < 1.5f, $"in the save it stands by the cart ({saved.Position:F2} vs cart {h.I.CartPoint():F2})");
                        bool zoned = false;
                        foreach (GameSceneData sd in copy.worldData.gameSceneDataList)
                        {
                            if (sd.id != savedScene) continue;
                            foreach (WorldZoneData z in sd.worldZones)
                            {
                                foreach (SGuid g in z.wgoDataList) if (g == saved.UniqueId) zoned = true;
                            }
                        }
                        Check(zoned, "in the save it is in a zone's list (a crafting source, a navigation hole, as any built chest)");
                        float ground = Mount.Instance.GroundHeight(saved.Position, out string src);
                        Check(!float.IsNaN(ground) && Mathf.Abs(ground - saved.Position.y) < 0.5f, $"in the save it stands on ground ({src}, {ground:F2} vs {saved.Position.y:F2})");
                    }
                }
                string side = Mount.BuildSideText("selftest", "saved at test time", 42, new List<Mount.Instance> { h.I }, false, out _);
                Check(side.Contains(chest.UniqueId.Id), "the side file names the chest");
                Mount.TakeCargoBackFromSave();
                yield return Frames(2);
                Check(rig.Aboard && chest.IsHidden && ChestProbe.ZoneEntries(chest) == 0, "after the save it rides again, hidden and zone-less");

                // The fail-safe. It sets down EVERY riding chest, the player's included (seen 2026-10-09: the player's
                // chest stood by its free cart after the run), so those are noted first and lifted back afterwards
                // through the load's own path (Rig.Restore).
                var others = new List<KeyValuePair<CartChest.Rig, WgoData>>();
                foreach (Mount.Instance i in Mount.All)
                {
                    if (i.ChestRig.Aboard && !ReferenceEquals(i.ChestRig, rig))
                    {
                        others.Add(new KeyValuePair<CartChest.Rig, WgoData>(i.ChestRig, i.ChestRig.Chest));
                    }
                }
                if (others.Count > 0)
                {
                    Info($"{others.Count} other chest(s) ride in the world (yours): the lock sets them down at their carts and this test lifts them back");
                }
                Info("the next \"switched off for this session\" error is this test's own: it trips the lock on purpose");
                CartChest.Lock("self-test");
                yield return Frames(2);
                Check(!rig.Aboard && !chest.IsHidden && chest.WorldZoneData != null && Vector3.Distance(chest.Position, h.I.CartPoint()) < 1.5f, "locked: the riding chest was set down at the cart, shown and zoned");
                Check(!CartChest.On && rig.WhyNotPickUp(chest, PC) == "Chests don't ride on the cart.", "locked: no chest is lifted any more");
                CartChest.UnlockForTest();
                Check(CartChest.On, "unlocked again for the session");
                foreach (KeyValuePair<CartChest.Rig, WgoData> o in others)
                {
                    bool back = o.Key.Restore(o.Value, "back on the cart after the self-test");
                    Check(back && o.Key.Aboard && o.Value.IsHidden, $"your chest is back on {o.Key.owner.Describe()}");
                }
                Check(Same(chest.Inventory, count0, total0), "after everything: the same items");
                Mounts.Dismount("self-test");
                yield return Frames(2);
            }
            finally
            {
                CartChest.UnlockForTest();
                RemoveChest();
            }
        }

        // The zombie guard (docs/portable-chest.md §4a, §7a.2): a caretaker on its way to or from a chest keeps it as a
        // target by id and finishes the trip wherever the chest is, so the pickup must wait while one targets it. No
        // test save has a caretaker (a zombie handed to a zombie_supplier_station), so one is staged by data: a
        // ZombieWgoData that exists only in the zombie system's lookup (never in a scene, never saved), typed Caretaker
        // with the test chest as its target, for a few synchronous checks; then as a conveyor transporter. It is gone
        // again before the frame ends, so no save can ever see it.
        internal static IEnumerator ChestZombieGuard()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle h = Spawn(near, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            yield return Until(() => h.IsReady, 6f);
            yield return MakeChest();
            if (chest == null)
            {
                yield break;
            }
            try
            {
                h.ComeToKeeper(true, showMessage: false);
                yield return Until(() => h.IsReady, 4f);
                MountResult ride = h.Ride(showMessage: false);
                if (!ride.Ok)
                {
                    Fail("riding the test cart: " + ride.Reason);
                    yield break;
                }
                yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 4f);
                CartChest.Rig rig = h.I.ChestRig;
                Check(rig.WhyNotPickUp(chest, PC) == null, "before: the chest can be lifted");
                ZombieSystemData zs = MainGame.ZombieSystemData;
                if (zs == null)
                {
                    Fail("the game has no zombie system data");
                    yield break;
                }
                FieldInfo typeField = AccessTools.Field(typeof(ZombieWgoData), "zombieType");
                FieldInfo careField = AccessTools.Field(typeof(ZombieWgoData), "caretakerCurrentTargetUniqueId");
                FieldInfo convField = AccessTools.Field(typeof(ZombieWgoData), "conveyorTransporterCurrentTargetUniqueId");
                if (typeField == null || careField == null || convField == null)
                {
                    Fail("the zombie's fields moved (zombieType, caretakerCurrentTargetUniqueId, conveyorTransporterCurrentTargetUniqueId): the guard reads their public getters; check them");
                    yield break;
                }
                int zombiesBefore = zs.zombieOnSceneWgoIds.Count;
                ZombieWgoData z = null;
                Mount.Instance.SuppressScripts = true;
                try
                {
                    z = new ZombieWgoData("zombie", chest.Position, chest.WorldId) { isTempObject = true, IsInteractable = false };
                }
                catch (Exception e)
                {
                    Fail("making a zombie by data: " + e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    Mount.Instance.SuppressScripts = false;
                }
                if (z == null)
                {
                    yield break;
                }
                string why;
                try
                {
                    zs.Cache[z.UniqueId.Guid] = z;
                    zs.zombieOnSceneWgoIds.Add(z.UniqueId);
                    typeField.SetValue(z, ZombieType.Caretaker);
                    careField.SetValue(z, chest.UniqueId);
                    Check(z.ZombieType == ZombieType.Caretaker && z.CaretakerCurrentTargetUniqueId == chest.UniqueId && zs.GetZombie(z.UniqueId) == z, "staged: a caretaker in the zombie system with the chest as its target");
                    Check(rig.WhyNotPickUp(chest, PC) == "A zombie is using this chest.", "a caretaker heading for the chest: refused with the zombie message");
                    bool lifted = rig.PickUp(chest, PC, out why);
                    Check(!lifted && why == "A zombie is using this chest." && !rig.Aboard && !chest.IsHidden && chest.WorldZoneData != null, $"the press refuses too ({why}); the chest stands shown and zoned");
                    careField.SetValue(z, SGuid.Empty);
                    Check(rig.WhyNotPickUp(chest, PC) == null, "the caretaker done with it: the chest can be lifted again");
                    typeField.SetValue(z, ZombieType.ConveyorTransporter);
                    convField.SetValue(z, chest.UniqueId);
                    Check(rig.WhyNotPickUp(chest, PC) == "A zombie is using this chest.", "a conveyor transporter heading for the chest: refused");
                    convField.SetValue(z, SGuid.Empty);
                    Check(rig.WhyNotPickUp(chest, PC) == null, "the transporter done with it: the chest can be lifted again");
                }
                finally
                {
                    zs.zombieOnSceneWgoIds.Remove(z.UniqueId);
                    zs.Cache.Remove(z.UniqueId.Guid);
                }
                Check(zs.zombieOnSceneWgoIds.Count == zombiesBefore && zs.GetZombie(z.UniqueId) == null, "the staged zombie is gone from the zombie system again");
                Check(rig.PickUp(chest, PC, out why), $"after it: lifted{(why != null ? " (" + why + ")" : "")}");
                yield return Frames(2);
                Check(rig.SetDown(PC, out why), $"set down again{(why != null ? " (" + why + ")" : "")}");
                yield return Frames(2);
                Mounts.Dismount("self-test");
                yield return Frames(2);
            }
            finally
            {
                RemoveChest();
            }
        }
    }
}
