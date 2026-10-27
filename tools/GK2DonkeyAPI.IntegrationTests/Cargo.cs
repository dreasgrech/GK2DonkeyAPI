using GK2DonkeyAPI.Api;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.Cargo.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // Cargo per mount (docs/next-update.md section 3, the user's go of 2026-10-27): the cart holds the smaller of the
        // player's Cart capacity and its mount's [Mount] cargo (the rat 2, Gerry 4, the donkey 6), re-applied when the
        // mount changes under a hitched cart; a lighter mount with more aboard leaves the cart standing (2026-10-27).
        internal static IEnumerator CargoPerMount()
        {
            string noPack = MissingPack("rat"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Catalog.Entry rat = Catalog.Find(M("rat")), gerry = Catalog.Find(M("gerry"));
            if (rat == null || gerry == null || Looks.Find(Sk("rat")) == null || Looks.Find(Sk("gerry")) == null)
            {
                Warn("the built-in rat or Gerry isn't registered: skipped");
                yield break;
            }
            Check(Catalog.CargoOf(rat) == 2 && Catalog.CargoOf(gerry) == 4 && Catalog.CargoOf(Catalog.Donkey) == 6, $"the shipped cargo: rat {Catalog.CargoOf(rat)}, Gerry {Catalog.CargoOf(gerry)}, donkey {Catalog.CargoOf(Catalog.Donkey)} (2, 4, 6)");
            MountHandle h = Spawn(Spot(home, 4f) ?? home, true, withCart: true);
            if (h == null) { Fail("could not spawn a donkey with a cart"); yield break; }
            yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
            if (h.I.CargoRig.Inv == null) { Fail("the test cart has no inventory"); h.Remove(); yield break; }
            string owner = Mounts.Behaviour.OverriddenBy("CartSlots");
            object was = owner == null ? null : Mounts.Behaviour.Get<object>("CartSlots");
            var guids = new HashSet<string>();
            Inventory Inv() => h.I.CargoRig.Inv;
            int Size() => Inv() != null ? Inv().Data.InventorySize : -1;
            MountHandle left = null;
            try
            {
                Mounts.Behaviour.ClearAny("CartSlots"); // Saddle Up holds it: taken over for the test, given back in the finally
                Mounts.Behaviour.Set(Owner, "CartSlots", 6);
                Check(Size() == 6, $"the donkey's cart at Cart capacity 6: 6 slots ({Size()})");
                for (int k = 0; k < 4; k++) Inv().AddItemToInventory(new Item("wood"));
                foreach (Item it in h.I.CargoRig.ItemsNow()) guids.Add(it.UniqueId.ToString());
                Check(h.I.CargoRig.Count == 4, $"four items on the donkey's cart ({h.I.CargoRig.Count})");

                // a lighter mount with more aboard than it pulls (the user, 2026-10-27: Gerry hitched to the donkey's six): the
                // cart is left standing with its load, and hitching it is refused until it holds no more than the mount pulls
                h.Tuning.Set(Owner, "Skin", Sk("rat"));
                yield return Until(() => Looks.MountWanted(h.I) == rat && !h.HasCart, 10f);
                yield return Frames(2);
                foreach (MountHandle x in Mounts.InstancesOf(Owner)) if (x.Exists && x.HasCart && !x.HasDonkey) left = x;
                Check(Looks.MountWanted(h.I) == rat && !h.HasCart && left != null && left.I.CargoRig.Count == 4,
                    $"switched to the rat with four aboard: the cart is left standing with all four ({(h.HasCart ? "still hitched" : "unhitched")}, the free cart {(left == null ? "missing" : left.I.CargoRig.Count + " on it")})");
                if (left == null) yield break;
                yield return Until(() => h.IsReady && left.IsReady, 6f);
                MountResult refused = Mounts.Hitch(h, left);
                Check(!refused.Ok && (refused.Reason ?? "").Contains("2 items at most") && (refused.Reason ?? "").Contains("Take 2 off"), $"hitching it to the rat is refused with why (\"{refused.Reason}\")");
                int removed = 0;
                foreach (Item it in new List<Item>(left.I.CargoRig.Inv.Data.Inventory))
                {
                    if (removed < 2 && guids.Contains(it.UniqueId.ToString())) { left.I.CargoRig.Inv.RemoveItemFromInventoryByUID(it); removed++; }
                }
                yield return Frames(1);
                MountResult back = Mounts.Hitch(h, left);
                Check(back.Ok, $"two off the standing cart: the rat takes it ({(back.Ok ? "hitched" : back.Reason)})");
                yield return Until(() => h.IsReady && h.HasCart && h.I.CargoRig.Inv != null, 6f);
                Check(h.HasCart && h.I.CargoRig.Count == 2 && Size() == 2, $"the rat's two ({h.I.CargoRig.Count} on it, {Size()} slots)");
                Check(Inv() != null && !Inv().AddItemToInventory(new Item("wood")), "the rat's cart full at two");

                h.Tuning.Set(Owner, "Skin", Sk("gerry"));
                yield return Until(() => Looks.MountWanted(h.I) == gerry && Size() == 4, 10f);
                Check(Looks.MountWanted(h.I) == gerry && Size() == 4, $"Gerry: four ({Size()} slots)");
                Mounts.Behaviour.Set(Owner, "CartSlots", 2);
                Check(Size() == 2, $"Cart capacity 2 on Gerry: the player's smaller number wins ({Size()} slots)");
                Mounts.Behaviour.Set(Owner, "CartSlots", 6);

                h.Tuning.Set(Owner, "Skin", "");
                yield return Until(() => Looks.MountWanted(h.I) == Catalog.Donkey && Size() == 6, 10f);
                Check(Looks.MountWanted(h.I) == Catalog.Donkey && Size() == 6 && h.I.CargoRig.Count == 2, $"back to the donkey: six slots, the two still on ({Size()} slots, {h.I.CargoRig.Count} on it)");
            }
            finally
            {
                Mounts.Behaviour.ClearAny("CartSlots");
                if (owner != null) Mounts.Behaviour.Set(owner, "CartSlots", was);
                if (h.Exists && h.I.CargoRig.Inv != null)
                {
                    foreach (Item it in new List<Item>(h.I.CargoRig.Inv.Data.Inventory))
                    {
                        if (guids.Contains(it.UniqueId.ToString())) h.I.CargoRig.Inv.RemoveItemFromInventoryByUID(it);
                    }
                }
                if (left != null && left.Exists && left.I.CargoRig.Inv != null)
                {
                    foreach (Item it in new List<Item>(left.I.CargoRig.Inv.Data.Inventory))
                    {
                        if (guids.Contains(it.UniqueId.ToString())) left.I.CargoRig.Inv.RemoveItemFromInventoryByUID(it);
                    }
                    left.Remove();
                }
                if (h.Exists) h.Remove();
            }
        }
    }
}
