using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A player-built chest riding on the cart (docs/portable-chest.md). The chest stays the same world object in its
    // scene throughout: never cloned, never removed from the scene, its inventory never replaced, so its contents are
    // always in the game's own save. Riding, it is out of every zone (no crafting source, no navigation hole) and
    // hidden (no colliders anywhere: the interaction scan, remove mode and physics can't see it), and its position
    // follows the cart. Set down, it is shown, re-registered in the chunk system at the new spot and put back into the
    // zone the game picks, the way Move Building moves a standing building (GK2QOLMods, Mover.cs).
    //
    // Built 2026-10-06 in five steps (docs/portable-chest.md §8): pick up (the action key on a chest while driving) and
    // set down (the interact key with nothing in front) with every guard of §4; a stand-in of the chest drawn on the
    // bed, placed by the cargo layout; on foot at the standing cart the stand-in offers the game's "[E] Open" (the real
    // chest's inventory) and "[F] Set down"; for a save the chest is shown and zoned at the cart for the moment of
    // writing and taken back right after, the side file names it and the restore after a load lifts it again; a door
    // to another scene is refused while it rides. A removal, a lost view or a scene change set it down at the cart.
    // The chest takes the whole cart: no other cargo while it rides (the cart's inventory is shrunk to 0 slots), and no
    // chest while cargo is on the cart.
    internal static class ChestMoves
    {
        // The game's own private pair, which picks the static or dynamic chunk layer itself (Move Building uses them).
        private static readonly Action<Wgo> RegisterInChunks = AccessTools.MethodDelegate<Action<Wgo>>(AccessTools.Method(typeof(Wgo), "TryRegisterInChunkManager"));
        private static readonly Action<Wgo> UnregisterFromChunks = AccessTools.MethodDelegate<Action<Wgo>>(AccessTools.Method(typeof(Wgo), "TryUnregisterInChunkManager"));

        // Out of the chunk system (found from the view's bounds, so before any move) and hidden: the view releases its
        // parts to the pool untouched and goes inactive (Wgo.RefreshVisuals), colliders and all.
        internal static void Hide(WgoData d, Wgo v)
        {
            if (v != null && v && v.RegisteredInChunker)
            {
                UnregisterFromChunks(v);
            }
            d.IsHidden = true;
        }

        // At 'at', shown (the parts load at the root, which followed Position all along) and registered in the chunk
        // system at the new spot, the navmesh cut refreshed as Move Building does after a move. A view that is still
        // registered (spawned hidden after the chest was lifted at a load, so Hide never saw it) is re-bucketed: the
        // game's unregister finds an object in whatever chunk holds it, wherever it is now.
        internal static void Show(WgoData d, Wgo v, Vector3 at)
        {
            d.Position = at;
            d.IsHidden = false;
            if (v != null && v)
            {
                d.RefreshCustomNavMeshCutUnit();
                Physics.SyncTransforms();
                if (v.RegisteredInChunker)
                {
                    UnregisterFromChunks(v);
                }
                RegisterInChunks(v);
            }
        }

        // Out of every zone of its scene (the game's own removal does all zones), the link cleared. Each call of the
        // game's RemoveWgoData takes one list entry only (List.Remove), so a zone is asked until it holds none: a
        // duplicate entry would otherwise survive a remove-then-add (the self-test showed it, 2026-10-06).
        internal static void ZoneRemoveAll(GameSceneData sd, WgoData d)
        {
            foreach (WorldZoneData z in sd.worldZones)
            {
                int guard = 0;
                while (z.wgoDataList.Contains(d.UniqueId) && guard++ < 16)
                {
                    z.RemoveWgoData(d);
                }
            }
            d.WorldZoneData = null;
        }

        // Into the zone the game would pick: by descending processing priority, the first that accepts
        // (GameSceneData.HandleWgoDataAddWorldZone, private). Null when none accepts the position. This also cuts the
        // navigation hole at the position.
        internal static WorldZoneData ZoneAddByPriority(GameSceneData sd, WgoData d)
        {
            WgoData was = Adding;
            Adding = d; // our own add of a riding chest passes WorldZoneData_TryAddWgoData_Patch
            try
            {
                foreach (KeyValuePair<int, List<WorldZoneData>> kv in sd.WorldZonesByProcessingPriority.Reverse())
                {
                    foreach (WorldZoneData z in kv.Value)
                    {
                        if (z.TryAddWgoData(d))
                        {
                            return z;
                        }
                    }
                }
                return null;
            }
            finally
            {
                Adding = was;
            }
        }

        internal static WgoData Adding;

        // A stand-in view of a chest: a temp object, not in the scene, no chunk registration, collider objects off, the
        // game's build-preview technique (BuildController.CreateFullCoverSoftHint) and how our donkey itself exists.
        // Its parts are destroyed, not pooled, when it goes (Wgo.DestroyParts for a temp object).
        internal static Wgo SpawnStandIn(WgoData src, Vector3 pos, string sceneId, Transform parent, out WgoData temp, out int collidersOff)
        {
            Mount.Instance.SuppressScripts = true;
            try
            {
                temp = new WgoData(src.id, pos, sceneId) { isTempObject = true, IsInteractable = false };
                temp.MainWgoPartData.variationId = src.MainWgoPartData.variationId;
                temp.MainWgoPartData.rotationIndex = src.MainWgoPartData.rotationIndex;
            }
            finally
            {
                Mount.Instance.SuppressScripts = false;
            }
            Wgo w = Wgo.Spawn(temp, parent, registerInChunkManagerIfStatic: true, ignoreChunkRegistration: true, applyDefaultWgoPartState: true);
            w.UpdateChunkVisibility(isVisible: true);
            collidersOff = 0;
            foreach (Collider c in w.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                c.gameObject.SetActive(false);
                collidersOff++;
            }
            return w;
        }

        // The navigation manager's cut unit for a world object: present or not.
        internal static bool HasNavCut(WgoData d)
        {
            GlobalNavigationManager nav = LazySingleton<GlobalNavigationManager>.Instance;
            if (nav == null)
            {
                return false;
            }
            var used = AccessTools.Field(typeof(GlobalNavigationManager), "usedUnits").GetValue(nav) as System.Collections.IDictionary;
            return used != null && used.Contains(d.UniqueId);
        }
    }

    internal static class CartChest
    {
        // The finished player chests (the probe's definitions dump, 2026-10-06): 20, 30 and 40 slots, every flag the
        // design depends on clean. The "_place" ids are their construction sites, turned into these by the game.
        internal static readonly HashSet<string> Allowed = new HashSet<string> { "chest_rough", "chest", "chest_good" };

        internal static bool IsChest(WgoData d) => d?.Definition != null && d.Definition.interactionType == WGODef.InteractionType.Chest && Allowed.Contains(d.id);

        internal static bool On => Plugin.CargoChest != null && Plugin.CargoChest.Value && !Locked;

        // The feature's own fail-safe. Locked, no chest is lifted any more this session; whatever rides is set down
        // at its cart at once. Set when the game is not the audited build and the player forced the mod on anyway
        // (this is the one feature that touches saved state), and when any chest operation throws (whatever the
        // cause: a broken mod can then hurt at most once, loudly). The after-load repair and the pre-save pass keep
        // running regardless: they use only the game's most basic calls.
        internal static bool Locked { get; private set; }
        internal static string LockReason { get; private set; }

        // The self-tests only: the lock is a session state, and a test that trips it must not switch the feature off
        // for the player's session.
        internal static void UnlockForTest()
        {
            Locked = false;
            LockReason = null;
        }

        internal static void Lock(string reason)
        {
            if (Locked)
            {
                return;
            }
            Locked = true;
            LockReason = reason;
            Plugin.Log.LogError($"Chest: the chest on the cart is switched off for this session: {reason}. Any chest riding is set down at its cart now.");
            try
            {
                foreach (Mount.Instance i in Mount.All.ToArray())
                {
                    if (i.ChestRig.Aboard)
                    {
                        i.ChestRig.SetDownAt(i.CartPoint(), "locked: " + reason);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Chest: setting the riding chests down after the lock failed; the pre-save pass and the after-load repair cover them: " + e.Message);
            }
            try
            {
                if (Mount.Instance.IsInGame(out _, out _))
                {
                    Mount.Say("The chest on the cart is off for now (see the log).");
                }
            }
            catch
            {
            }
        }

        // The stand-ins on the carts, by their temp data: the chest handler patches turn "[E] Open" on a stand-in into
        // the real chest's window and offer "[F] Set down" on it.
        private static readonly Dictionary<WgoData, Rig> standIns = new Dictionary<WgoData, Rig>();

        internal static Rig RigOfStandIn(WgoData d) => d != null && standIns.TryGetValue(d, out Rig r) ? r : null;

        // The chests riding right now (every rig's chest): the zone guard and the serialization pass read it.
        internal static readonly HashSet<WgoData> Riding = new HashSet<WgoData>();

        // Right before the game serializes (GameSave.PrepareToSave): no allowed chest may be hidden in the save,
        // whatever happened before. The save hook has shown the riding ones (SetDownForSave); anything still hidden
        // here is a path the code didn't foresee: shown and zoned where it stands, loudly. The second, independent
        // line under "a save always holds a normal chest".
        internal static void EnsureNoneHiddenForSave()
        {
            try
            {
                WorldData world = MainGame.WorldData;
                if (world == null)
                {
                    return;
                }
                foreach (GameSceneData sd in world.gameSceneDataList)
                {
                    foreach (WgoData d in sd.wgoDataList.ToArray())
                    {
                        if (d == null || !d.IsHidden || !IsChest(d))
                        {
                            continue;
                        }
                        Rig rig = RigOf(d);
                        if (rig == null && ZoneEntries(sd, d) > 0)
                        {
                            continue; // hidden by the game itself (a fight stage, a script): it stays in its zone lists; ours never do
                        }
                        if (rig != null)
                        {
                            // A riding chest the save hook missed (it threw): through the rig's own save path, so the
                            // take-back after the save knows about it.
                            rig.SetDownForSave();
                            Plugin.Log.LogWarning($"Chest: {d.id} {d.UniqueId} was still hidden as the game saved though {rig.owner.Describe()} carries it; stood at the cart for the save now. Please report this line.");
                            continue;
                        }
                        ChestMoves.Show(d, GameScene.GetWgoViewGlobal(d.UniqueId), d.Position);
                        ChestMoves.ZoneRemoveAll(sd, d);
                        WorldZoneData zone = ChestMoves.ZoneAddByPriority(sd, d);
                        Plugin.Log.LogWarning($"Chest: {d.id} {d.UniqueId} ({d.Inventory.Data.Inventory.Count} item(s) inside) was still hidden as the game saved with no cart carrying it; shown at {d.Position:F2} in {sd.id}{(zone != null ? $", zone '{zone.id}'" : ", no zone")} so the save holds a normal chest. Please report this line.");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Chest: the pre-save check failed: " + e);
            }
        }

        // How many zone lists of the scene hold this object (a riding chest: none; the game's own hidden objects keep theirs).
        internal static int ZoneEntries(GameSceneData sd, WgoData d)
        {
            int n = 0;
            foreach (WorldZoneData z in sd.worldZones)
            {
                foreach (SGuid g in z.wgoDataList)
                {
                    if (g == d.UniqueId)
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        // The rig carrying this chest, or null.
        internal static Rig RigOf(WgoData d)
        {
            if (d == null)
            {
                return null;
            }
            foreach (Mount.Instance i in Mount.All)
            {
                if (ReferenceEquals(i.ChestRig.Chest, d))
                {
                    return i.ChestRig;
                }
            }
            return null;
        }

        // After a load (the restore is done): any allowed chest still hidden in the save that no cart owns is a ride
        // that never ended (a crash between a pickup and a save hook, a failure the code didn't foresee): shown and
        // zoned where it stands, so it can be opened again. Never silent.
        internal static int RepairHiddenChests()
        {
            int fixedCount = 0;
            try
            {
                if (MainGame.WorldData == null)
                {
                    return 0;
                }
                var owned = new HashSet<WgoData>();
                foreach (Mount.Instance i in Mount.All)
                {
                    if (i.ChestRig.Chest != null)
                    {
                        owned.Add(i.ChestRig.Chest);
                    }
                }
                foreach (GameSceneData sd in MainGame.WorldData.gameSceneDataList)
                {
                    foreach (WgoData d in sd.wgoDataList.ToArray())
                    {
                        if (d == null || !d.IsHidden || !IsChest(d) || owned.Contains(d) || ZoneEntries(sd, d) > 0)
                        {
                            continue; // in a zone list while hidden: the game's own staging, not a ride that never ended
                        }
                        ChestMoves.Show(d, GameScene.GetWgoViewGlobal(d.UniqueId), d.Position);
                        ChestMoves.ZoneRemoveAll(sd, d);
                        WorldZoneData zone = ChestMoves.ZoneAddByPriority(sd, d);
                        fixedCount++;
                        Plugin.Log.LogWarning($"Chest: {d.id} {d.UniqueId} ({d.Inventory.Data.Inventory.Count} item(s) inside) was hidden in the save with no cart carrying it; shown again at {d.Position:F2} in {sd.id}{(zone != null ? $", zone '{zone.id}'" : ", no zone")}. Please report this line.");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Chest: the hidden-chest repair failed: " + e.Message);
            }
            return fixedCount;
        }

        // A zombie on its way to or from this chest (caretakers both ways, conveyor transporters to it) keeps it as a
        // target by id and finishes the trip wherever the chest is (docs/portable-chest.md §4a): the pickup waits.
        private static bool ZombieTargets(WgoData d)
        {
            ZombieSystemData zs = MainGame.ZombieSystemData;
            if (zs == null)
            {
                return false;
            }
            foreach (SGuid id in zs.zombieOnSceneWgoIds)
            {
                ZombieWgoData z = zs.GetZombie(id);
                if (z == null)
                {
                    continue;
                }
                if (z.ZombieType == ZombieType.Caretaker && z.CaretakerCurrentTargetUniqueId == d.UniqueId)
                {
                    return true;
                }
                if (z.ZombieType == ZombieType.ConveyorTransporter && z.ConveyorTransporterCurrentTargetUniqueId == d.UniqueId)
                {
                    return true;
                }
            }
            return false;
        }

        // Build mode and the building and chest windows hold the zone's inventories for as long as they are open
        // (BuildManager.EnableBuildMode, UIBuildingWindow, ChestInteractionHandler): a chest leaving the zone then
        // would still be spent from.
        private static bool ZoneInventoriesHeld()
        {
            try
            {
                if (BuildController.Instance != null && BuildController.Instance.IsBuildModeActive)
                {
                    return true;
                }
                return (LazyUI.GetWindow<UIBuildingWindow>()?.IsShown ?? false) || (LazyUI.GetWindow<UIChestWindow>()?.IsShown ?? false)
                    || (LazyUI.GetWindow<UIConveyorChestWindow>()?.IsShown ?? false);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal sealed class Rig
        {
            internal readonly Mount.Instance owner;
            private WgoData chest;
            private int savedSize = -1;

            // The stand-in drawn on the bed while the chest rides (step 2): a copy of the chest's look under the cargo
            // holder, so it sways and pitches with the cart and goes with the holder when the view is released. One per
            // ride (each leaves the part pool's borrowed count one higher). Placed by the layout like a lone cargo item,
            // with the chest id's [item] entry for tuning (CargoLayout: its own class, matched by id).
            private Wgo standIn;
            private WgoData standInData;
            private Bounds standBox;
            private bool standMeasured;
            private bool standWaiting;

            internal Wgo StandIn => standIn != null && standIn ? standIn : null;

            internal Rig(Mount.Instance owner)
            {
                this.owner = owner;
            }

            internal bool Aboard => chest != null;
            internal WgoData Chest => chest;

            internal Api.ChestInfo Info(Api.MountHandle cart) => chest == null ? null : InfoOf(chest, cart);

            private static Api.ChestInfo InfoOf(WgoData d, Api.MountHandle cart) => new Api.ChestInfo(cart, d.id, d.UniqueId?.Id, d.Inventory?.Data?.Inventory?.Count ?? 0);

            // Why this chest can't be loaded now, or null. Every refusal is something that would leave the game
            // inconsistent or lose or move items behind the player's back (docs/portable-chest.md §4).
            internal string WhyNotPickUp(WgoData d, PlayerController pc)
            {
                if (!On)
                {
                    return "Chests don't ride on the cart.";
                }
                if (!owner.HasCart)
                {
                    return "That needs the cart.";
                }
                if (!IsChest(d))
                {
                    return "Only a plain storage chest goes on the cart.";
                }
                if (Aboard)
                {
                    return "There's a chest on the cart already.";
                }
                if (owner.CargoRig.Count > 0)
                {
                    return "Set the cargo down first.";
                }
                if (d.IsHidden || d.isTempObject || pc == null || d.WorldId != pc.PlayerData.currentGameSceneId || GameScene.GetWgoViewGlobal(d.UniqueId) == null)
                {
                    return "This can't be moved.";
                }
                if (Mount.Instance.IsCoop())
                {
                    return "Not in co-op.";
                }
                CraftComponent cc = d.CraftComponent;
                if (cc != null && cc.IsDestroyingCraftActive)
                {
                    return "It's marked for taking down.";
                }
                if (cc != null && (cc.IsStarted || cc.HasCraftsInQueue))
                {
                    return "It's busy.";
                }
                if ((d.WorkbenchParents?.Count ?? 0) > 0 || (d.AttachedWorkbenchExtensions?.Count ?? 0) > 0)
                {
                    return "It's attached to a workbench.";
                }
                if (d.Worker != null)
                {
                    return "Someone works at it.";
                }
                WgoPartData part = d.MainWgoPartData;
                if (part?.DockPointDataList != null && part.DockPointDataList.Any(p => p != null && p.IsOccupied))
                {
                    return "Someone is using it right now.";
                }
                if (ZoneInventoriesHeld())
                {
                    return "Close the building window first.";
                }
                if (ZombieTargets(d))
                {
                    return "A zombie is using this chest.";
                }
                return null;
            }

            // Lifts the chest onto the cart: out of every zone, hidden, following the cart; the cart's inventory shrunk
            // to 0 slots so every "room on the cart" question says full (the gate, the stations, the hint).
            internal bool PickUp(WgoData d, PlayerController pc, out string reason)
            {
                reason = WhyNotPickUp(d, pc);
                if (reason != null)
                {
                    return false;
                }
                if (Lift(d, "lifted"))
                {
                    Api.Mounts.RaiseChestLoaded(InfoOf(d, owner.Handle));
                    return true;
                }
                reason = "That didn't work.";
                return false;
            }

            // Back onto the cart without the player's guards (the save's take-back, the restore after a load): the
            // chest the save hook set down at the cart a moment ago, or the one the side file names standing at the
            // cart. Only what would be unsafe is still refused: not one of the three, hidden, a pending destroy craft.
            internal bool Restore(WgoData d, string why)
            {
                if (chest != null || d == null || !IsChest(d) || (d.CraftComponent != null && d.CraftComponent.IsDestroyingCraftActive))
                {
                    return false;
                }
                if (!owner.HasCart)
                {
                    Plugin.Log.LogInfo($"Chest: {d.id} stays where it is: {owner.Describe()} has no cart.");
                    return false;
                }
                Rig other = RigOf(d);
                if (other != null || Riding.Contains(d))
                {
                    // Two records naming one chest (a dormant one from an uninstalled mod and a live one): the first
                    // cart that claimed it keeps it; never two owners.
                    Plugin.Log.LogInfo($"Chest: {d.id} is already carried by {other?.owner.Describe() ?? "another cart"}; {owner.Describe()} does not take it.");
                    return false;
                }
                if (d.IsHidden)
                {
                    Plugin.Log.LogWarning($"Chest: {d.id} was hidden in the save (a ride that never ended); lifted onto {owner.Describe()} as a repair.");
                }
                if (owner.CargoRig.Count > 0)
                {
                    // Cargo came back onto this cart too (both were at the cart in the save): the chest rides alone, so it
                    // stays standing rather than shrinking an inventory with items in it.
                    Plugin.Log.LogInfo($"Chest: {d.id} stays at the cart: {owner.Describe()} has {owner.CargoRig.Count} cargo item(s) back on it.");
                    return false;
                }
                return Lift(d, why);
            }

            // The rig owns the chest before anything is changed, so a failure half-way can never leave a hidden chest
            // that nobody owns: on a throw the chest is shown and zoned again where it stands and the rig lets go.
            private bool Lift(WgoData d, string why)
            {
                chest = d;
                Riding.Add(d);
                Vector3 stood = d.Position; // the rollback's spot: Follow moves the data to the cart inside the try
                GameSceneData sd = null;
                Wgo v = null;
                try
                {
                    sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                    v = GameScene.GetWgoViewGlobal(d.UniqueId);
                    string zone = d.WorldZoneData?.id;
                    ChestMoves.ZoneRemoveAll(sd, d);
                    ChestMoves.Hide(d, v);
                    Inventory inv = owner.CargoRig.Inv;
                    if (inv != null)
                    {
                        savedSize = inv.Data.InventorySize;
                        inv.Data.InventorySize = 0;
                    }
                    Follow();
                    owner.OnCargoChanged();
                    Plugin.Log.LogInfo($"Chest: {d.id} {d.UniqueId} ({d.Inventory.Data.Inventory.Count} item(s) inside) {why} onto {owner.Describe()} from zone '{zone}' at {d.Position:F2}; the cart takes no cargo while it rides.");
                    return true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Chest: lifting {d.id} failed; it is put back where it stands: {e}");
                    try
                    {
                        ChestMoves.Show(d, v, stood);
                        if (sd != null)
                        {
                            ChestMoves.ZoneRemoveAll(sd, d);
                            ChestMoves.ZoneAddByPriority(sd, d);
                        }
                    }
                    catch (Exception e2)
                    {
                        Plugin.Log.LogError($"Chest: putting {d.id} back failed too; it is repaired at the next load (RepairHiddenChests): {e2.Message}");
                    }
                    RestoreCartSize();
                    Riding.Remove(d);
                    chest = null;
                    Lock("lifting a chest threw");
                    return false;
                }
            }

            // The save (step 4): for the moment the game writes, the chest's DATA is a normal chest standing at the cart
            // (shown, zoned where a zone takes the spot); nothing else changes: the rig keeps it, the cart stays at 0
            // slots, the stand-in stays. The side file names it ("chest <guid>"); the take-back after the save hides it
            // and takes it out of the zone again; after a load, the restore finds it by that GUID standing at the cart.
            // Should the take-back never run (the game's save threw: a postfix doesn't run then), the next tick does it.
            private bool lentForSave;

            internal string LentChestGuid => lentForSave && chest != null ? chest.UniqueId?.Id : null;

            internal void SetDownForSave()
            {
                if (chest == null)
                {
                    return;
                }
                try
                {
                    WgoData d = chest;
                    GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                    Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
                    Vector3 at = FallbackSpot(owner.CartPoint());
                    lentForSave = true; // before anything can throw: the take-back after the save must always run
                    ChestMoves.Show(d, v, at);
                    ChestMoves.ZoneRemoveAll(sd, d);
                    WorldZoneData zone = ChestMoves.ZoneAddByPriority(sd, d);
                    Plugin.Log.LogInfo($"Chest: {d.id} ({d.Inventory.Data.Inventory.Count} item(s) inside) stands at {owner.Describe()} for the save{(zone != null ? $" in zone '{zone.id}'" : " in no zone")}.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Chest: standing {chest?.id} at the cart for the save failed (the pre-save pass is the next line): {e}");
                    Lock("standing a chest at the cart for the save threw");
                }
            }

            internal void TakeBackAfterSave()
            {
                if (!lentForSave)
                {
                    return;
                }
                lentForSave = false;
                if (chest == null)
                {
                    return;
                }
                try
                {
                    WgoData d = chest;
                    GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                    ChestMoves.ZoneRemoveAll(sd, d);
                    ChestMoves.Hide(d, GameScene.GetWgoViewGlobal(d.UniqueId));
                    Follow();
                    Plugin.Log.LogInfo($"Chest: {d.id} rides on {owner.Describe()} again after the save.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Chest: taking {chest?.id} back after the save failed: {e}");
                    Lock("taking a chest back after the save threw");
                }
            }

            // Each frame while it rides: the data sits where the cart is, so a save or a fallback finds it there.
            internal void Follow()
            {
                if (chest == null)
                {
                    return;
                }
                if (lentForSave)
                {
                    TakeBackAfterSave(); // the save's postfix never ran (the game's save threw): back to riding now
                    return;
                }
                if (!chest.IsHidden)
                {
                    // Shown while the rig holds it and no save is in progress: a half-done step somewhere. Riding means
                    // hidden and zone-less; put it back to that rather than drag a visible chest along.
                    Plugin.Log.LogWarning($"Chest: {chest.id} was shown while riding on {owner.Describe()}; hidden again. Please report this line.");
                    try
                    {
                        ChestMoves.ZoneRemoveAll(MainGame.WorldData.GetGameSceneDataById(chest.WorldId), chest);
                        ChestMoves.Hide(chest, GameScene.GetWgoViewGlobal(chest.UniqueId));
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError("Chest: hiding it again failed: " + e.Message);
                        Lock("a riding chest could not be hidden again");
                        return;
                    }
                }
                chest.Position = owner.CartPoint();
            }

            // Called by the cargo packer (Cargo.Rig.RefreshViews) with the holder it placed its items in: the chest
            // alone on the bed, bottom on the floor, at its [item] entry's alone place (else its base place), and a
            // PlacedItem for the panel's alone sliders.
            internal void Layout(Transform container, float floor, List<Cargo.PlacedItem> placed, out string note)
            {
                note = null;
                if (chest == null || container == null)
                {
                    DropStandIn();
                    return;
                }
                if (standIn == null || !standIn)
                {
                    standIn = ChestMoves.SpawnStandIn(chest, container.position, chest.WorldId, container, out standInData, out _);
                    standIn.UpdatePosByData = false;
                    Transform t = standIn.transform;
                    t.SetParent(container, worldPositionStays: false);
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                    t.localPosition = Vector3.zero;
                    standMeasured = false;
                    standIns[standInData] = this;
                }
                if (!standMeasured && !standWaiting)
                {
                    // Parts still loading, or loaded but not drawn yet: measured again a frame at a time until it is (a
                    // parked cart has no tick of its own to notice).
                    standWaiting = true;
                    Plugin.Instance?.StartCoroutine(RelayoutWhenLoaded(standIn));
                }
                if (standIn.MainWgoPart != null)
                {
                    if (!standMeasured)
                    {
                        standMeasured = Measure();
                    }
                    // Once its parts are there: the interactable trigger (layer 6, what the keeper's interaction scan
                    // hits) back on, so on foot at the standing cart the chest can be opened and set down from the cart
                    // (step 3); the solid, build-area and dock-point colliders stay off. Interactable so the scan takes it
                    // (a saved flag, but the stand-in's data is temp and never saved).
                    if (!standInData.IsInteractable && standIn.MainWgoPart.InteractableColliders != null)
                    {
                        foreach (Collider c in standIn.MainWgoPart.InteractableColliders)
                        {
                            if (c != null)
                            {
                                c.gameObject.SetActive(true);
                            }
                        }
                        standInData.IsInteractable = true;
                    }
                }
                Bounds b = standMeasured ? standBox : new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.6f));
                Vector3 target = new Vector3(0f, floor, 0f);
                CargoLayout.ItemEntry e = CargoLayout.ForItem(chest.id);
                if (e != null)
                {
                    target += e.HasAlone ? e.Alone : e.Base;
                }
                standIn.transform.localPosition = new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
                placed.Add(new Cargo.PlacedItem { Id = chest.id, OnTop = false, Alone = true, Box = b.size, Pos = target - new Vector3(0f, floor, 0f) });
                note = chest.id + " (the chest, alone" + (e != null && e.HasAlone ? ", own alone correction" : "") + (standMeasured ? "" : ", not measured yet") + ")";
            }

            private System.Collections.IEnumerator RelayoutWhenLoaded(Wgo w)
            {
                float end = Time.unscaledTime + 5f;
                while (w != null && w && ReferenceEquals(w, standIn) && !standMeasured && Time.unscaledTime < end)
                {
                    yield return null;
                    if (w != null && w && ReferenceEquals(w, standIn) && !standMeasured && w.MainWgoPart != null)
                    {
                        owner.CargoRig.RefreshViews(); // measures and places when the parts are drawn
                    }
                }
                standWaiting = false;
            }

            // The stand-in's drawn size in its own space: its meshes, else its renderers' bounds brought into its space.
            private bool Measure()
            {
                try
                {
                    if (Cargo.MeasureMeshes(standIn.transform, standIn.transform, out standBox, includeInactive: false) && standBox.size.sqrMagnitude > 0.0001f)
                    {
                        return true;
                    }
                    bool any = false;
                    Bounds wb = default;
                    foreach (Renderer r in standIn.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled)
                        {
                            continue;
                        }
                        if (!any)
                        {
                            wb = r.bounds;
                            any = true;
                        }
                        else
                        {
                            wb.Encapsulate(r.bounds);
                        }
                    }
                    if (!any)
                    {
                        return false;
                    }
                    Transform t = standIn.transform;
                    Vector3 ls = t.lossyScale;
                    standBox = new Bounds(t.InverseTransformPoint(wb.center), new Vector3(wb.size.x / Mathf.Max(0.01f, Mathf.Abs(ls.x)), wb.size.y / Mathf.Max(0.01f, Mathf.Abs(ls.y)), wb.size.z / Mathf.Max(0.01f, Mathf.Abs(ls.z))));
                    return true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Chest: measuring the stand-in failed: " + e.Message);
                    return false;
                }
            }

            internal void DropStandIn()
            {
                if (standIn != null && standIn)
                {
                    if (standInData != null)
                    {
                        standInData.IsInteractable = false; // Destroy waits for the frame's end; the scan must not take it meanwhile
                    }
                    standIn.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(standIn.gameObject);
                }
                Forget();
            }

            // The cargo holder (and the stand-in under it) is gone with the view: nothing left to destroy.
            internal void OnHolderGone() => Forget();

            private void Forget()
            {
                if (standInData != null)
                {
                    standIns.Remove(standInData);
                }
                standIn = null;
                standInData = null;
                standMeasured = false;
                standWaiting = false;
            }

            // Sets the chest down where the keeper would set cargo down (behind the cart with CargoSetDownBehindCart,
            // else in front of him), when the spot is in his sight and reach and some zone accepts it. Otherwise the
            // chest stays on the cart and the reason is told.
            internal bool SetDown(PlayerController pc, out string reason)
            {
                reason = null;
                if (chest == null)
                {
                    return false;
                }
                PlayerData pd = pc.PlayerData;
                Vector3 spot;
                string how;
                // The cargo rig's own spot search (the same as for every other item): behind the cart with the stand-in's
                // measured length, else in front of the keeper; both in his sight.
                Vector3 box = standMeasured ? standBox.size : new Vector3(0.9f, 0.6f, 0.6f);
                if (owner.S.CargoSetDownBehindCart.Value && owner.CargoRig.TrySpotBehindCart(pd, box, out spot))
                {
                    how = "behind the cart";
                }
                else if (SpecialPhysicsCastUtils.GetPlayerDropPosition(pd.position.Value, pd.Direction, out spot) && Cargo.Rig.InKeepersSight(pd, spot))
                {
                    how = "in front of the keeper";
                }
                else
                {
                    reason = "There's no room for it here.";
                    return false;
                }
                // His body's way to it clear too, so it never lands where he can't get to it (SafeGround.cs).
                string blocked = Mount.Instance.PlaceBlocked(pc, pd.position.Value, spot);
                if (blocked != null)
                {
                    reason = "I can't reach that spot.";
                    return false;
                }
                float g = Mount.Instance.GroundHeight(spot, out _);
                if (!float.IsNaN(g))
                {
                    spot.y = g;
                }
                return Place(spot, how, out reason, strict: true);
            }

            // The spot a fallback stands the chest on: the cart body's point, on its ground; without ground there (the
            // body hangs behind the donkey and can overhang a bank), the donkey's root, which SafeGround keeps on walkable
            // ground. Ground is asked only in the keeper's scene: another scene's ground isn't here to ask.
            private Vector3 FallbackSpot(Vector3 at)
            {
                if (chest == null || chest.WorldId != MainGame.PlayerData?.currentGameSceneId)
                {
                    return at;
                }
                float g = Mount.Instance.GroundHeight(at, out _);
                if (!float.IsNaN(g))
                {
                    at.y = g;
                    return at;
                }
                Vector3 root = owner.Position;
                float gr = Mount.Instance.GroundHeight(root, out _);
                if (!float.IsNaN(gr))
                {
                    root.y = gr;
                    Plugin.Log.LogInfo($"Chest: no ground under the cart's point {at:F2}; {chest.id} stands at the donkey's root {root:F2} instead.");
                    return root;
                }
                // Neither on ground (riding along a bank, the ride ended at a door before the park check could move the
                // donkey): the game's own drop-spot search from the root, which only returns ground (no water, no other
                // drops), as cargo set down behind the cart uses it.
                Vector3 dir = at - root;
                dir.y = 0f;
                Vector2 face = dir.sqrMagnitude > 0.0001f ? new Vector2(dir.x, dir.z).normalized : Vector2.down;
                if (SpecialPhysicsCastUtils.GetPlayerDropPosition(root, face, out Vector3 found))
                {
                    float gf = Mount.Instance.GroundHeight(found, out _);
                    if (!float.IsNaN(gf))
                    {
                        found.y = gf;
                    }
                    Plugin.Log.LogInfo($"Chest: no ground under the cart's point {at:F2} nor the donkey's root {root:F2}; {chest.id} stands at the nearest drop spot {found:F2}.");
                    return found;
                }
                Plugin.Log.LogWarning($"Chest: no ground found near the cart for {chest.id}; it stands at the cart's point {at:F2} anyway. Please report this line.");
                return at;
            }

            // Where the cart is, no questions asked: a save being written, the cart removed or lost, a scene change.
            // The chest is shown and zoned if any zone takes the spot, else shown zone-less (a normal chest the game's
            // remove mode still accepts) and logged.
            internal void SetDownAt(Vector3 at, string why)
            {
                if (chest == null)
                {
                    return;
                }
                at = FallbackSpot(at);
                if (!Place(at, "at the cart (" + why + ")", out string reason, strict: false))
                {
                    Plugin.Log.LogWarning($"Chest: {chest?.id} could not be set down at the cart ({why}): {reason}");
                }
            }

            // The chest at 'at', shown, in the chunk system and in the zone the game picks. Strict: no zone takes the
            // spot means the chest stays on the cart (hidden again, following).
            private bool Place(Vector3 at, string how, out string reason, bool strict)
            {
                reason = null;
                WgoData d = chest;
                try
                {
                    GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                    Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
                    // Out of any zone that grabbed it meanwhile (a fight level's content), then the zone first: it reads
                    // only the position, so a refused spot changes nothing but that, which Follow puts back the next frame.
                    ChestMoves.ZoneRemoveAll(sd, d);
                    d.Position = at;
                    WorldZoneData zone = ChestMoves.ZoneAddByPriority(sd, d);
                    if (zone == null && strict)
                    {
                        Follow();
                        reason = "That spot is outside any building area.";
                        return false;
                    }
                    ChestMoves.Show(d, v, at);
                    LetGo();
                    owner.OnCargoChanged();
                    Plugin.Log.LogInfo($"Chest: {d.id} {d.UniqueId} ({d.Inventory.Data.Inventory.Count} item(s) inside) set down {how} at {at:F2}{(zone != null ? $" in zone '{zone.id}'" : " in NO zone (it stands outside every building area)")}; the cart takes cargo again.");
                    Api.Mounts.RaiseChestUnloaded(InfoOf(d, owner.Handle));
                    return true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Chest: setting {d.id} down failed: {e}");
                    if (!d.IsHidden)
                    {
                        // Shown already: it stands where it is now; the rig lets go so it is never dragged along visible.
                        LetGo();
                        try { owner.OnCargoChanged(); } catch { }
                        Plugin.Log.LogWarning($"Chest: {d.id} stands at {d.Position:F2} (set down half-way); the cart takes cargo again.");
                        Lock("setting a chest down threw");
                        return true;
                    }
                    reason = "That didn't work.";
                    Lock("setting a chest down threw");
                    return false;
                }
            }

            // The rig's hold on the chest ends: the stand-in goes, the cart's slots come back.
            private void LetGo()
            {
                DropStandIn();
                RestoreCartSize();
                if (chest != null)
                {
                    Riding.Remove(chest);
                }
                chest = null;
                lentForSave = false;
            }

            private void RestoreCartSize()
            {
                Inventory inv = owner.CargoRig.Inv;
                if (inv != null && savedSize >= 0)
                {
                    inv.Data.InventorySize = savedSize;
                }
                savedSize = -1;
            }

            // A new view (hitching, unhitching, a re-summon) gets a new inventory: the 0-slot rule is applied to it.
            internal void OnCartInventory(Inventory inv)
            {
                if (chest != null && inv != null)
                {
                    if (savedSize < 0)
                    {
                        savedSize = inv.Data.InventorySize;
                    }
                    inv.Data.InventorySize = 0;
                    owner.CargoRig.RefreshViews(); // the stand-in on the new view
                }
            }

            // The chest goes with the cart when it is hitched or unhitched: the other instance takes it over.
            internal void TransferTo(Rig other)
            {
                if (chest == null || other == null || other == this)
                {
                    return;
                }
                if (other.chest != null || other.owner.CargoRig.Count > 0)
                {
                    // Never two chests, never a chest with cargo: this one is set down where its cart is instead.
                    Plugin.Log.LogWarning($"Chest: {other.owner.Describe()} already carries {other.Describe()} or cargo; {chest.id} is set down at the cart instead of handed over.");
                    SetDownAt(owner.CartPoint(), "hand-over refused");
                    return;
                }
                DropStandIn();
                RestoreCartSize();
                other.chest = chest;
                chest = null;
                Inventory inv = other.owner.CargoRig.Inv;
                if (inv != null)
                {
                    other.savedSize = inv.Data.InventorySize;
                    inv.Data.InventorySize = 0;
                }
                other.Follow();
                other.owner.OnCargoChanged(); // its stand-in, when its view is ready
            }

            internal string Describe() => chest == null ? "no chest" : $"{chest.id} ({chest.Inventory.Data.Inventory.Count} item(s))";
        }
    }

    // ---------------------------------------------------------------------- the chest's handler while riding

    // The Action key on a chest: while riding the cart and the chest can be taken, "[F] Load onto the cart", and the
    // press lifts it. On foot, or when it can't be taken, the game's own behaviour (nothing), and the reason is told
    // on the press only when the chest is one that could ride. ChestInteractionHandler doesn't declare HasInteraction2
    // or Interact2 (it inherits the base's, which say no and do nothing), so the base's are patched, for chest
    // handlers only; FormInteractionInfo is the chest's own.
    // "[E] Open" on a stand-in (on foot at the standing cart, step 3): the real chest's window, over its own
    // inventory, with no zone list (the chest is in no zone while it rides). The game's handler would open the
    // stand-in's empty temp inventory.
    [HarmonyPatch(typeof(ChestInteractionHandler), "Interact")]
    internal static class ChestInteractionHandler_Interact_Patch
    {
        private static bool Prefix(ChestInteractionHandler __instance, PlayerController interactor, ref bool __result)
        {
            try
            {
                WgoData d = ChestInteractionHandler_HasInteraction2_Patch.Assigned(__instance)?.Data;
                CartChest.Rig rig = CartChest.RigOfStandIn(d);
                if (rig == null || rig.Chest == null || interactor == null)
                {
                    return true;
                }
                GlobalEventsSystem.FireTrigger(GlobalEventsSystem.Event.Type.Interaction, rig.Chest.id ?? ""); // as the base handler does, for the real chest
                WorldZoneData keeperZone = interactor.PlayerData.CurrentWorldZoneData;
                LazyUI.GetWindow<UIChestWindow>().Open(new UIBaseChestWindowData(interactor.PlayerData.inventory, keeperZone == null ? null : new MultiInventory(keeperZone, rig.Chest), rig.Chest));
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                // Never the game's handler on a stand-in: it would open the stand-in's own empty inventory, and anything
                // put there would be nobody's.
                Plugin.Log.LogWarning("Chest: opening the chest on the cart failed: " + e.Message);
                bool standIn = false;
                try { standIn = CartChest.RigOfStandIn(ChestInteractionHandler_HasInteraction2_Patch.Assigned(__instance)?.Data) != null; } catch { }
                __result = standIn;
                return !standIn;
            }
        }
    }

    [HarmonyPatch(typeof(WGOInteractionHandlerBase), "HasInteraction2")]
    internal static class ChestInteractionHandler_HasInteraction2_Patch
    {
        internal static readonly AccessTools.FieldRef<WGOInteractionHandlerBase, Wgo> Assigned = AccessTools.FieldRefAccess<WGOInteractionHandlerBase, Wgo>("assignedWgo");

        internal static Mount.Instance Rider => Mount.IsMounted ? Mount.Ridden : null;

        private static void Postfix(WGOInteractionHandlerBase __instance, ref bool __result)
        {
            if (__result || !(__instance is ChestInteractionHandler))
            {
                return;
            }
            try
            {
                WgoData d = Assigned(__instance)?.Data;
                if (CartChest.RigOfStandIn(d) != null)
                {
                    __result = true; // "[F] Set down" on the chest riding a standing cart
                    return;
                }
                Mount.Instance r = Rider;
                if (r != null && d != null && CartChest.IsChest(d) && CartChest.On && r.HasCart)
                {
                    __result = true; // the press tells why when it can't
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Chest: the take check failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(WGOInteractionHandlerBase), "Interact2")]
    internal static class ChestInteractionHandler_Interact2_Patch
    {
        private static bool Prefix(WGOInteractionHandlerBase __instance, PlayerController interactor, ref bool __result)
        {
            if (!(__instance is ChestInteractionHandler))
            {
                return true;
            }
            try
            {
                WgoData d = ChestInteractionHandler_HasInteraction2_Patch.Assigned(__instance)?.Data;
                CartChest.Rig onCart = CartChest.RigOfStandIn(d);
                if (onCart != null)
                {
                    if (!onCart.SetDown(interactor, out string why) && why != null)
                    {
                        Mount.Say(why);
                    }
                    __result = true;
                    return false;
                }
                Mount.Instance r = ChestInteractionHandler_HasInteraction2_Patch.Rider;
                if (r == null || d == null || !CartChest.IsChest(d) || !CartChest.On || !r.HasCart)
                {
                    return true;
                }
                if (!r.ChestRig.PickUp(d, interactor, out string reason))
                {
                    Mount.Say(reason);
                }
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Chest: the take press failed: " + e.Message);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ChestInteractionHandler), "FormInteractionInfo")]
    internal static class ChestInteractionHandler_FormInteractionInfo_Patch
    {
        private static bool warned;

        // Asked every frame while a chest is in reach: the verdict (zombies, windows, the chest's state) is kept a
        // quarter second, as the hint's key icon is.
        private static WgoData verdictFor;
        private static bool verdictOk;
        private static float verdictUntil;

        private static bool CanLoad(Mount.Instance r, WgoData d)
        {
            if (!ReferenceEquals(verdictFor, d) || Time.unscaledTime >= verdictUntil)
            {
                verdictFor = d;
                verdictOk = r.ChestRig.WhyNotPickUp(d, MainGame.PlayerController) == null;
                verdictUntil = Time.unscaledTime + 0.25f;
            }
            return verdictOk;
        }

        private static void Postfix(ChestInteractionHandler __instance, InteractionInfos __result)
        {
            try
            {
                WgoData d = ChestInteractionHandler_HasInteraction2_Patch.Assigned(__instance)?.Data;
                if (__result == null || d == null)
                {
                    return;
                }
                if (CartChest.RigOfStandIn(d) != null)
                {
                    __result.Add(new InteractionInfo(InstanceControls.HintFor(GameKey.Action, "Set down")));
                    return;
                }
                Mount.Instance r = ChestInteractionHandler_HasInteraction2_Patch.Rider;
                if (r == null || !CartChest.IsChest(d) || !CartChest.On || !r.HasCart)
                {
                    return;
                }
                __result.Add(new InteractionInfo(InstanceControls.HintFor(GameKey.Action, CanLoad(r, d) ? "Load onto the cart" : "Load onto the cart (not now)")));
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true; // built every frame while the chest is the target
                    Plugin.Log.LogWarning("Chest: adding the load hint failed: " + e);
                }
            }
        }
    }

    // ---------------------------------------------------------------------- doors (step 5)

    // A chest on the cart belongs to the scene it is in; a teleport to another scene (a door, map travel, a cutscene)
    // would leave it behind hidden where the cart stood. So at the one entry point of every cross-scene teleport,
    // before anything has faded or moved, the chest is set down at the cart, shown and zoned, and the keeper is told.
    // Same-scene teleports (the house) pass: the chest rides on with the parked cart. (docs/portable-chest.md §4b)
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Teleport))]
    internal static class PlayerController_Teleport_Patch
    {
        private static bool Prefix(TeleportDataBase teleportData, ref bool __result)
        {
            try
            {
                Mount.Instance r = Mount.IsMounted ? Mount.Ridden : null;
                if (r == null || !r.ChestRig.Aboard || teleportData == null)
                {
                    return true;
                }
                string to = teleportData.GetDestinationSceneData()?.id;
                string here = MainGame.PlayerData?.currentGameSceneId;
                if (to == null || to == here || !teleportData.CanTeleport(out _))
                {
                    return true; // same scene, or the game refuses the teleport itself: nothing to do
                }
                // The chest stays with the cart: set down where the cart is, and the teleport goes ahead. (The game's
                // doors are scripted teleports that take control before calling this, so a refusal here could not be
                // told from a cutscene's teleport and would leave the keeper frozen; and the house is a same-scene
                // teleport, where nothing needs doing: the chest rides on with the parked cart.)
                r.ChestRig.SetDownAt(r.CartPoint(), "door to " + to);
                Mount.Say("The chest stays by the cart.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Chest: the door check failed: " + e.Message);
                return true;
            }
        }
    }

    // ---------------------------------------------------------------------- the zone guard and the save's last line

    // A riding chest is in no zone. The game re-zones every object of a scene when a fighting level's content loads
    // (GameSceneData.AddWorldZoneData → TryAddWgoData on all of them), and TryAddWgoData asks only the position:
    // refused for a riding chest, except for the mod's own adds (ChestMoves.Adding).
    [HarmonyPatch(typeof(WorldZoneData), nameof(WorldZoneData.TryAddWgoData))]
    internal static class WorldZoneData_TryAddWgoData_Patch
    {
        private static bool Prefix(WgoData wgoData, ref bool __result)
        {
            if (wgoData != null && CartChest.Riding.Contains(wgoData) && !ReferenceEquals(ChestMoves.Adding, wgoData))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Right before the game serializes: no allowed chest hidden in the save, whatever came before (CartChest
    // .EnsureNoneHiddenForSave). Runs inside SaveSystem.Save after the mod's own save prefix.
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.PrepareToSave))]
    internal static class GameSave_PrepareToSave_Patch
    {
        private static void Prefix()
        {
            CartChest.EnsureNoneHiddenForSave();
        }
    }
}
