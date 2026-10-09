using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // While mounted in Cart style the cart is the keeper's hands: what the game would put on his head lands on the
    // cart, and what the game would take from his head it finds on the cart. Round 9 of the findings (three source
    // passes and the in-game probe GK2-head-facts.txt) established the exact points:
    //   sink      PlayerData.AddOverheadItem            every pickup (ground, piles, scripts, tables, zombies)
    //   gate      PlayerData.HasFreeOverheadSlot        every "may I take?" check (piles, scripts, stations, tables)
    //   lookup    PlayerData.TryGetOverheadItem         every consumer (piles, tables, graves, quests, scripts)
    //   mutation  PlayerData.RemoveOverheadItem(Item), PlayerData.InsertOverheadItemTo(WgoData, Item),
    //             LazyExpressionEvaluationScope.GetOverheadItemForMutation (script executions)
    // Everything that draws the head or drops what is held (HasOverheadItem, the counts, OverheadItems) keeps
    // reading the real head, which stays empty while riding, so nothing is drawn on the keeper and nothing is dumped
    // when he starts work. Zombies go onto the cart like the rest (CargoZombies; docs/zombies-on-cart.md), relinked to
    // the copy the cart stores (RelinkZombie). With CargoZombies off the cart refuses them and they keep the head, with
    // its own limit honoured because the gate otherwise answers for the cart.
    //
    // Item identity (probe section E): adding to an inventory stores a NEW object with the same GUID and empties the
    // source. Take-from-table paths add first and remove from the table by GUID second, so the source shell gets its
    // count back after the split (the table's removal then fires its event). Consumers that add the cart's object to
    // their own inventory before removing it leave the cart entry at count 0, and the cart's removal is then silent,
    // so the cart is resynced after every removal.
    internal static class HeadReroute
    {
        private static readonly MethodInfo PlayTakeSound = AccessTools.Method(typeof(PlayerData), "PlayOverheadItemTakeSound");
        private static Item lastCartMatch;

        // The reroute is on only while the cart is the keeper's hands. During the mod's own unload the head is the
        // destination again. Off while the cargo sheet runs: its test items are on the cart then, and they must not be
        // found by a pile or a script, nor a real pickup land among them (the sheet's abort clears the cart).
        internal static bool Active => Mount.IsMounted && Mount.CurrentStyle == MountStyle.Cart && Cargo.Attached && !Cargo.Unloading
            && MountRules.Allowed(MountFeature.Cargo) && !CargoSheet.Running;

        // Inside a zombie station's own take-back (ZombieStations_TakeBack_Patch): its HasFreeOverheadSlot asks for room
        // for its worker only. A counter, as the game's calls can nest.
        internal static int InZombieStation;

        // While the game's own AddOverheadItem runs for an item the cart refuses (a zombie with CargoZombies off), the gate answers for the
        // real head, so the original drops for the head's limit and not for the cart's.
        internal static bool GateAnswersForHead;

        // While mounted the keeper's hands stay empty: only the cart carries (the user's rule, 2026-09-30). The riding
        // pose has no clip on the animator's Overhead layer, and that layer's clips are what switch the held-item view
        // on, so anything held while riding would be invisible (measured with HeldItemProbe: the view 'overhead/item'
        // inactive, the Overhead layer at weight 1 with no clip). Locked whenever mounted and the cart is not the
        // hands: Ride style, Cargo switched off by a rule. Not during the mod's own unload (the save hand-back).
        internal static bool HandsLocked => Mount.IsMounted && !Active && !Cargo.Unloading && !GateAnswersForHead;

        // A ground item the keeper may not take while mounted: anything while the hands are locked, and what the cart
        // refuses (zombies with CargoZombies off) in either style.
        internal static bool RefuseGroundItem(DropView drop)
        {
            if (!Mount.IsMounted || drop == null)
            {
                return false;
            }
            if (HandsLocked)
            {
                return true;
            }
            Item item = drop.Data?.Item;
            return item != null && !item.IsEmpty && !Cargo.Allowed(item);
        }

        // A zombie standing in the world (at its job or free) the keeper may not take while mounted: whenever the cart
        // can't take it right now. Taking one has no slot check in the game (ZombieInteractionHandler.Interact): it
        // finishes and empties its station, unassigns it and frees its dock first, and only then hands the item to
        // AddOverheadItem, so a refusal there comes too late (found in game 2026-10-05: in Ride style the zombie was
        // laid on the ground, off its job). Refused here, before any of that runs.
        internal static bool RefuseWorldZombie(Item zombieItem)
        {
            if (!Mount.IsMounted)
            {
                return false;
            }
            if (HandsLocked || !Active || zombieItem == null || zombieItem.IsEmpty || !Cargo.Allowed(zombieItem))
            {
                return true;
            }
            Inventory inv = Cargo.Inv;
            return inv == null || !inv.CanAddItemToInventory(zombieItem);
        }

        // Puts an item the game wanted on the head onto the cart, with the pickup's side effects. False when the cart
        // is full or refuses the item (the caller then falls back).
        internal static bool TakeToCart(PlayerData pd, Item item)
        {
            Inventory inv = Cargo.Inv;
            if (inv == null || !Cargo.Allowed(item) || !inv.CanAddItemToInventory(item))
            {
                return false;
            }
            int count = item.Count;
            if (!inv.AddItemToInventory(item))
            {
                return false;
            }
            RelinkZombie(inv, item, "the cart");
            item.Count = count; // the source shell: callers that still hold it remove it from its table by GUID next
            if (pd.HasInteractingItem)
            {
                pd.RemoveInteractingItem();
            }
            GlobalEventsSystem.FireTrigger(GlobalEventsSystem.Event.Type.AddOverhead, item.id);
            try
            {
                PlayTakeSound?.Invoke(pd, new object[] { item });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo: take sound failed: " + e.Message);
            }
            MountSounds.CargoLanded();
            Plugin.Log.LogInfo($"Cargo: picked up {Cargo.Name(item)} straight onto the cart ({inv.Data.Inventory.Count}/{inv.Data.InventorySize}).");
            return true;
        }

        // A zombie's item must be the very object its ZombieWgoData holds (zombieItem): the zombie's organs and gear are
        // that object's nested inventory, and the game builds a placed zombie from it. An inventory add of a single
        // item stores a NEW object and moves the nested inventory into it (Item.AddItemToInventory -> Split(1,
        // transferInternalData)), leaving the source empty with the same GUID; on the game's head no copy is made,
        // so the game never needs this. Called right after each of the mod's own adds, with the inventory added to;
        // never from the change events, which also fire on removals (a station's own add has made its own copy then).
        // The game's own relinks do the same (ZombieDropInteractionHandler.LinkItemToZombieData, the choir, autopsy).
        internal static void RelinkZombie(Inventory inv, Item added, string where)
        {
            try
            {
                if (inv == null || added == null || added.Definition == null || !Cargo.IsLinked(added.Definition))
                {
                    return;
                }
                Guid g = added.UniqueId.Guid;
                Item stored = null;
                foreach (Item it in inv.Data.Inventory)
                {
                    if (it != null && !it.IsEmpty && it.UniqueId.Guid == g)
                    {
                        stored = it;
                        break;
                    }
                }
                if (stored == null)
                {
                    return;
                }
                ZombieWgoData z = MainGame.ZombieSystemData?.GetZombie(stored.UniqueId);
                if (z == null)
                {
                    if (Cargo.IsZombie(stored.Definition))
                    {
                        // A zombie item with no zombie: never made by the game (every zombie item is created with one).
                        Plugin.Log.LogWarning($"Cargo: {stored.id} on {where} has no zombie behind it ({g}); nothing to link.");
                    }
                    return; // another linked item (a wild zombie body, if the game marks it linked): nothing to keep in step
                }
                if (ReferenceEquals(z.ZombieItem, stored))
                {
                    return;
                }
                z.SetZombieItem(stored);
                Plugin.Log.LogInfo($"Cargo: zombie {z.Name} linked to its copy on {where} ({stored.Inventory.Count} item(s) inside: organs and gear).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Cargo: linking the zombie to its copy on {where} failed: " + e);
            }
        }

        internal static Item FindOnCart(Item item)
        {
            Inventory inv = Cargo.Inv;
            if (inv == null || item == null)
            {
                return null;
            }
            Guid g = item.UniqueId.Guid;
            List<Item> list = inv.Data.Inventory;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Item it = list[i];
                if (it != null && it.UniqueId.Guid == g)
                {
                    return it;
                }
            }
            return null;
        }

        // Removes a cart entry by GUID. The inventory stays silent when the entry was already emptied by a split
        // elsewhere, so the cart is resynced whatever the inventory reported.
        internal static bool RemoveFromCart(Item stored)
        {
            Inventory inv = Cargo.Inv;
            if (inv == null)
            {
                return false;
            }
            bool announced = inv.RemoveItemFromInventoryByUID(stored);
            bool gone = FindOnCart(stored) == null;
            if (!announced)
            {
                Cargo.Resync(gone ? "silent removal" : "removal failed");
            }
            return gone;
        }

        internal static Item LastCartMatch => lastCartMatch != null && FindOnCart(lastCartMatch) != null ? lastCartMatch : null;

        internal static void RememberMatch(Item item)
        {
            lastCartMatch = item;
        }
    }

    // ---------------------------------------------------------------------- the sink

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.AddOverheadItem))]
    internal static class PlayerData_AddOverheadItem_Patch
    {
        private static bool Prefix(PlayerData __instance, Item item)
        {
            try
            {
                if (HeadReroute.HandsLocked && item != null && !item.IsEmpty)
                {
                    // The item has already left wherever it was, so it goes on the ground in front of the keeper.
                    if (Cargo.DropAtKeeper(__instance, item))
                    {
                        Plugin.Log.LogInfo($"Mount: {item.id} set down in front of the keeper (the hands stay empty while riding).");
                        return false;
                    }
                    return true; // the game refused the drop: the head, rather than losing it (Mount.Tick sets it down later)
                }
                if (!HeadReroute.Active || HeadReroute.GateAnswersForHead || item == null || item.IsEmpty)
                {
                    return true; // off, or a deliberate head add from the mod's own fallback: the game's path
                }
                if (!Cargo.Allowed(item))
                {
                    // A zombie with CargoZombies off keeps the head: the original runs, with the gate answering for the head (its own
                    // limit) instead of the cart, so it drops exactly what a full head would drop and nothing more.
                    HeadReroute.GateAnswersForHead = true;
                    return true;
                }
                if (HeadReroute.TakeToCart(__instance, item))
                {
                    return false;
                }
                // Cart full: the item has already left wherever it was (the drop is gone, the pile removed it), so it
                // reappears in front of the keeper instead of vanishing or landing on the head.
                Cargo.DropAtKeeper(__instance, item);
                Mount.Say("The cart is full.");
                Plugin.Log.LogInfo($"Cargo: cart full, {Cargo.Name(item)} dropped in front of the keeper.");
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo: pickup reroute failed, the game's own path runs: " + e.Message);
                return true;
            }
        }

        // A finalizer, not a postfix: it must run when the original throws as well, or the gate would stay on the head.
        private static void Finalizer()
        {
            HeadReroute.GateAnswersForHead = false;
        }
    }

    // ---------------------------------------------------------------------- ground items

    // Picking an item up from the ground (BigDropInteractionHandler.Interact: remove the drop, AddOverheadItem) asks
    // no gate, so while the hands are locked the ground item offers no interaction at all: no prompt, E does nothing.
    // Measured 2026-09-30: without this the item was picked up and the AddOverheadItem sink set it straight down
    // again. A zombie on the ground is refused while mounted in either style when the cart takes no zombies.
    // Judged by the item (Cargo.Allowed), not the handler: the game gives ZombieDropInteractionHandler to corpses too
    // (DropView: DropType != 0 or item group "body"), and corpses do go on the cart (found in game 2026-09-30).
    [HarmonyPatch(typeof(BigDropInteractionHandler), nameof(BigDropInteractionHandler.HasInteraction))]
    internal static class BigDropInteractionHandler_HasInteraction_Patch
    {
        private static readonly AccessTools.FieldRef<BigDropInteractionHandler, DropView> DropRef =
            AccessTools.FieldRefAccess<BigDropInteractionHandler, DropView>("drop");

        private static void Postfix(BigDropInteractionHandler __instance, ref bool __result)
        {
            if (__result && HeadReroute.RefuseGroundItem(DropRef(__instance)))
            {
                __result = false;
            }
        }
    }

    // The yellow outline on a ground item (PlayerInteractionComponent.HandleDropInteractionEnter ->
    // SetInteractionVisualState) is the game's only sign that it can be picked up; ground items have no text hint.
    // The selection behind it asks only CanInteractWithDrop (exists, not despawning, not in the river), not
    // HasInteraction, so the same refusal goes here as well: no outline for what the donkey cannot take.
    [HarmonyPatch(typeof(PlayerInteractionComponent), "CanInteractWithDrop")]
    internal static class PlayerInteractionComponent_CanInteractWithDrop_Patch
    {
        private static void Postfix(DropView dropView, ref bool __result)
        {
            if (__result && HeadReroute.RefuseGroundItem(dropView))
            {
                __result = false;
            }
        }
    }

    // A zombie standing in the world: no "take" while mounted when the cart can't take it (HeadReroute.RefuseWorldZombie).
    // HasInteraction is what the game's E press asks (PlayerInputHandler.UpdateInput, and our own prefix of it); the
    // hint asks HasInteraction OR HasInteraction2, and inspect (F) stays, so its "[E] Take" row is removed separately.
    [HarmonyPatch(typeof(ZombieInteractionHandler), nameof(ZombieInteractionHandler.HasInteraction))]
    internal static class ZombieInteractionHandler_HasInteraction_Patch
    {
        internal static readonly AccessTools.FieldRef<WGOInteractionHandlerBase, Wgo> AssignedWgo =
            AccessTools.FieldRefAccess<WGOInteractionHandlerBase, Wgo>("assignedWgo");

        internal static bool Refused(ZombieInteractionHandler h)
        {
            if (!Mount.IsMounted)
            {
                return false;
            }
            Wgo w = AssignedWgo(h);
            return w != null && w.Data is ZombieWgoData z && HeadReroute.RefuseWorldZombie(z.ZombieItem);
        }

        private static bool warned;

        private static void Postfix(ZombieInteractionHandler __instance, ref bool __result)
        {
            try
            {
                if (__result && Refused(__instance))
                {
                    __result = false;
                }
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true; // asked every frame for the hint: once is enough
                    Plugin.Log.LogWarning("Mount: zombie take check failed (the game's answer stands): " + e);
                }
            }
        }
    }

    // The zombie's hint rows are built without asking HasInteraction ("[E] Take" and "[F] Inspect" always), so the
    // take row goes here while it is refused. Matched by the exact text the handler built (the same call, before the
    // game's FixSpace runs in GetInteractionInfos). Rows from events or quests (the base's) are left alone.
    [HarmonyPatch(typeof(ZombieInteractionHandler), "FormInteractionInfo")]
    internal static class ZombieInteractionHandler_FormInteractionInfo_Patch
    {
        private static readonly MethodInfo Localize = AccessTools.Method(typeof(WGOInteractionHandlerBase), "LocalizeHintWithActionIcon",
            new[] { typeof(string), typeof(GameKey) });

        private static bool warned;

        private static void Postfix(ZombieInteractionHandler __instance, InteractionInfos __result)
        {
            try
            {
                if (__result == null || __result.list.Count == 0 || !ZombieInteractionHandler_HasInteraction_Patch.Refused(__instance))
                {
                    return;
                }
                string take = (string)Localize.Invoke(__instance, new object[] { "hint_take", GameKey.Interaction });
                __result.list.RemoveAll(r => r != null && r.text == take);
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true; // built every frame while the zombie is the target: once is enough
                    Plugin.Log.LogWarning("Mount: hiding the zombie's take hint failed: " + e);
                }
            }
        }
    }

    // Taking a worker back from its station (porter, power source, carrier, clay, mine, sand, sawmill, and since game
    // 1.009 the caretaker's station while the caretaker is out working: every caller of
    // PutZombieFromGameSceneToStoreForPlayer besides the walking zombie) is gated by HasFreeOverheadSlot alone, which
    // answers the cart's room while riding. With zombies off the cart (CargoZombies off) the station would then
    // unassign its worker and hand it to AddOverheadItem, the head, and the empty-hands rule would lay it on the ground
    // (the bug of 2026-10-05 through another door). Marked here, so the gate can say no for these calls only.
    // Interact2 (the window) is left alone. With zombies on, the gate's cart answer is right: room means it fits.
    [HarmonyPatch]
    internal static class ZombieStations_TakeBack_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var types = new[]
            {
                typeof(PorterStationInteractionHandler), typeof(PowerSourceInteractionHandler), typeof(ZombieCarrierInteractionHandler),
                typeof(ZombieClayInteractionHandler), typeof(ZombieMineInteractionHandler), typeof(ZombieSandInteractionHandler),
                typeof(ZombieSawmillInteractionHandler), typeof(StationInteractionHandler),
            };
            foreach (Type t in types)
            {
                foreach (string name in new[] { "Interact", "HasInteraction", "FormInteractionInfo" })
                {
                    MethodInfo m = AccessTools.DeclaredMethod(t, name);
                    if (m != null)
                    {
                        yield return m;
                    }
                }
            }
        }

        private static void Prefix()
        {
            HeadReroute.InZombieStation++;
        }

        // A finalizer, so the mark goes even when the original throws.
        private static void Finalizer()
        {
            if (HeadReroute.InZombieStation > 0)
            {
                HeadReroute.InZombieStation--;
            }
        }
    }

    // ---------------------------------------------------------------------- the gate

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.HasFreeOverheadSlot), MethodType.Getter)]
    internal static class PlayerData_HasFreeOverheadSlot_Patch
    {
        private static bool Prefix(ref bool __result)
        {
            if (HeadProbe.ForceNoFreeSlot)
            {
                __result = false;
                return false;
            }
            if (HeadReroute.HandsLocked)
            {
                __result = false;
                return false;
            }
            if (HeadReroute.InZombieStation > 0 && Mount.IsMounted && !Plugin.CargoZombies.Value)
            {
                __result = false; // a station asking for room for its worker, and the cart takes no zombies
                return false;
            }
            if (!HeadReroute.Active || HeadReroute.GateAnswersForHead)
            {
                return true;
            }
            Inventory inv = Cargo.Inv;
            __result = inv != null && inv.Data.Inventory.Count < inv.Data.InventorySize;
            return false;
        }
    }

    // ---------------------------------------------------------------------- the lookup

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TryGetOverheadItem))]
    internal static class PlayerData_TryGetOverheadItem_Patch
    {
        private static bool Prefix(Predicate<Item> match, ref Item item, ref bool __result)
        {
            Item o = HeadProbe.LookupOverride;
            if (o != null && match != null && match(o))
            {
                item = o;
                __result = true;
                return false;
            }
            return true;
        }

        // The head first (the original), then the cart, top item first, as the head list is scanned.
        private static void Postfix(Predicate<Item> match, ref Item item, ref bool __result)
        {
            if (__result || match == null || !HeadReroute.Active)
            {
                return;
            }
            try
            {
                List<Item> list = Cargo.Inv.Data.Inventory;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Item it = list[i];
                    if (it != null && !it.IsEmpty && match(it))
                    {
                        item = it;
                        __result = true;
                        HeadReroute.RememberMatch(it);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo: cart lookup failed: " + e.Message);
            }
        }
    }

    // ---------------------------------------------------------------------- the mutations

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.RemoveOverheadItem), typeof(Item))]
    internal static class PlayerData_RemoveOverheadItem_Patch
    {
        private static bool Prefix(PlayerData __instance, Item item)
        {
            try
            {
                if (!Cargo.Attached || item == null)
                {
                    return true;
                }
                // An item that is on the head is the head's business, even when the cart already holds an object
                // with its GUID: the game's own InsertOverheadItemTo (used by the mod to move hand items onto the
                // cart) adds to the cart first and then removes from the head, and taking the cart copy here instead
                // left an emptied entry on the head (seen in game 2026-09-28).
                foreach (Item h in __instance.OverheadItems)
                {
                    if (h != null && (ReferenceEquals(h, item) || (h.UniqueId.Guid == item.UniqueId.Guid && h.id == item.id)))
                    {
                        return true;
                    }
                }
                Item stored = HeadReroute.FindOnCart(item);
                if (stored == null)
                {
                    return true;
                }
                HeadReroute.RemoveFromCart(stored);
                Plugin.Log.LogInfo($"Cargo: {Cargo.Name(item)} taken off the cart by the game ({Cargo.Count} left).");
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo: cart removal failed, the game's own path runs: " + e.Message);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.InsertOverheadItemTo), typeof(WgoData), typeof(Item))]
    internal static class PlayerData_InsertOverheadItemTo_Patch
    {
        private static bool Prefix(WgoData wgoData, Item item)
        {
            try
            {
                if (!Cargo.Attached || wgoData == null || item == null)
                {
                    return true;
                }
                Item stored = HeadReroute.FindOnCart(item);
                if (stored == null)
                {
                    return true; // from the head (the mod's own load, or a zombie with CargoZombies off): the game's path
                }
                if (wgoData == Cargo.DonkeyData)
                {
                    return false; // already there
                }
                if (!wgoData.Inventory.CanAddItemToInventory(stored))
                {
                    Plugin.Log.LogInfo($"Cargo: {wgoData.id} cannot take {item.id}; it stays on the cart.");
                    return false;
                }
                // Cart first, target second: the target's add splits the object, and a removal after that would be
                // silent (probe E2).
                string id = stored.id;
                HeadReroute.RemoveFromCart(stored);
                if (!wgoData.Inventory.AddItemToInventory(stored))
                {
                    // Refused after all (CanAdd said yes a moment ago): back onto the cart, never into thin air.
                    Cargo.Inv.AddItemToInventory(stored);
                    HeadReroute.RelinkZombie(Cargo.Inv, stored, "the cart");
                    Cargo.Resync("insert refused");
                    Plugin.Log.LogWarning($"Cargo: {wgoData.id} refused {id} after all; it is back on the cart.");
                    return false;
                }
                HeadReroute.RelinkZombie(wgoData.Inventory, stored, wgoData.id); // the game leaves this one stale; its own takes relink later
                GlobalEventsSystem.FireTrigger(GlobalEventsSystem.Event.Type.PlayerInsertOverheadToWgoAnItem, id);
                Plugin.Log.LogInfo($"Cargo: {Cargo.Name(stored)} put into {wgoData.id} from the cart ({Cargo.Count} left).");
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo: insert from the cart failed, the game's own path runs: " + e.Message);
                return true;
            }
        }
    }

    // Setting an item down. On foot the game drops the held item when interact is pressed with nothing to interact
    // with: the last step of the Interaction branch of PlayerInputHandler.UpdateInput, guarded by HasOverheadItem.
    // While riding the cart the real head is empty, so that press did nothing. This prefix takes over the
    // Interaction branch only while riding the cart and runs it in the game's own order (a big drop in front, then
    // the world object's events and interaction), with the cart's top item as the fallback instead of the head's.
    // The branch ends in a return in the original, so skipping the original on that frame skips nothing else.
    // UpdateInputInteractionOnly has the same fallback but is only called while planting, which dismounts.
    [HarmonyPatch(typeof(PlayerInputHandler), nameof(PlayerInputHandler.UpdateInput))]
    internal static class PlayerInputHandler_UpdateInput_Patch
    {
        private static readonly AccessTools.FieldRef<PlayerInputHandler, PlayerController> Controller =
            AccessTools.FieldRefAccess<PlayerInputHandler, PlayerController>("playerController");
        private static readonly AccessTools.FieldRef<PlayerInputHandler, IWGOInteractionHandler> CurHandler =
            AccessTools.FieldRefAccess<PlayerInputHandler, IWGOInteractionHandler>("curInteractionHandler");
        private static readonly MethodInfo TryCallFireEvent = AccessTools.Method(typeof(PlayerInputHandler), "TryCallFireEvent");
        private static readonly object Interaction1 = Enum.ToObject(AccessTools.Inner(typeof(PlayerInputHandler), "InteractionType"), 0);

        private static bool Prefix(PlayerInputHandler __instance)
        {
            // Donkeys and carts standing in the world (InstanceControls): riding one, or hitching a free cart, when the
            // game has nothing of its own to do with the key. On foot and in Ride style too, so asked first.
            if (!CargoSheet.Running && LazyInput.GetKeyDown(GameKey.Interaction))
            {
                try
                {
                    if (InstanceControls.TryInteract(Controller(__instance)))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Mount: the interact key for a standing donkey failed: " + e.Message);
                }
            }
            if (!HeadReroute.Active || CargoSheet.Running || !LazyInput.GetKeyDown(GameKey.Interaction))
            {
                return true;
            }
            try
            {
                PlayerController pc = Controller(__instance);
                PlayerInteractionComponent ic = pc.PlayerInteractionComponent;
                DropView drop = ic.BigDropUnderInteraction;
                if (drop != null && drop.InteractionHandler.HasInteraction())
                {
                    drop.InteractionHandler.Interact();
                    return false;
                }
                Wgo wgo = ic.WgoUnderInteraction;
                if (wgo != null)
                {
                    IWGOInteractionHandler h = wgo.InteractionHandler;
                    CurHandler(__instance) = h;
                    if ((bool)TryCallFireEvent.Invoke(__instance, new[] { Interaction1 }) || (h.HasInteraction() && h.Interact(pc)))
                    {
                        return false;
                    }
                }
                PlayerData pd = pc.PlayerData;
                if (pd.HasOverheadItem)
                {
                    pd.DropOverheadItem(); // something really on the head (a zombie with CargoZombies off): the game's own drop
                }
                else if (Mount.Ridden != null && Mount.Ridden.ChestRig.Aboard)
                {
                    if (!Mount.Ridden.ChestRig.SetDown(pc, out string why) && why != null)
                    {
                        Mount.Say(why);
                    }
                }
                else
                {
                    Cargo.SetDownLast(pd);
                }
                return false;
            }
            catch (Exception e)
            {
                // Not handed to the original: part of the press may already have run, and running it again
                // could interact twice. The press is lost for this frame only.
                Plugin.Log.LogWarning("Cargo: interact press failed: " + e.Message);
                return false;
            }
        }
    }

    // Script executions (InsertOvrhdItem and friends) take "the held item" from here; with one head item that is the
    // one their condition matched. With two different cart items it must be the matched one, which the lookup
    // remembered a moment earlier in the same interaction.
    [HarmonyPatch(typeof(LazyExpressionEvaluationScope), nameof(LazyExpressionEvaluationScope.GetOverheadItemForMutation))]
    internal static class LazyExpressionEvaluationScope_GetOverheadItemForMutation_Patch
    {
        private static void Postfix(ref Item __result)
        {
            if (HeadProbe.MutationOverride != null)
            {
                __result = HeadProbe.MutationOverride;
                return;
            }
            if (__result != null || !HeadReroute.Active)
            {
                return;
            }
            Item m = HeadReroute.LastCartMatch;
            if (m == null)
            {
                List<Item> list = Cargo.Inv.Data.Inventory;
                m = list.Count > 0 ? list[list.Count - 1] : null;
            }
            __result = m;
        }
    }
}
