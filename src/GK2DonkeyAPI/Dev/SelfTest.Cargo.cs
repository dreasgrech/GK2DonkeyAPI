using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // More than two items on the cart (General.CartSlots, docs/cart-capacity.md): the override applied to a standing
    // cart at once, four items taken and a fifth refused, two layers of two, the re-made inventory after an unhitch and
    // a hitch (the door path: the carried-over items go back on before anything else, nothing dropped), and a lowered
    // setting that never goes below the load and follows once items come off.
    internal static partial class SelfTest
    {
        private static IEnumerator CargoLayers()
        {
            MountHandle h = Spawn(Spot(home, 4f) ?? home, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
            if (h.I.CargoRig.Inv == null)
            {
                Fail("the test cart has no inventory");
                h.Remove();
                yield break;
            }
            string owner = Mounts.Behaviour.OverriddenBy("CartSlots");
            object was = owner == null ? null : Mounts.Behaviour.Get<object>("CartSlots");
            int before = h.I.CargoRig.Inv.Data.InventorySize;
            var guids = new HashSet<string>();
            MountHandle left = null;
            Inventory Inv() => h.I.CargoRig.Inv;
            try
            {
                Mounts.Behaviour.Set(Owner, "CartSlots", 4);
                Check(Inv().Data.InventorySize == 4, $"the override applies to the standing cart at once (4 slots; was {before})");
                int added = 0;
                foreach (string id in new[] { "wood", "stone", "wood", "stone" })
                {
                    if (Inv().AddItemToInventory(new Item(id))) added++;
                }
                foreach (Item it in h.I.CargoRig.ItemsNow()) guids.Add(it.UniqueId.ToString());
                Check(added == 4 && h.I.CargoRig.Count == 4, $"four items taken ({added} added, {h.I.CargoRig.Count} on the cart)");
                Check(!Inv().AddItemToInventory(new Item("wood")), "a fifth is refused");
                yield return Frames(2);
                var placed = new List<Cargo.PlacedItem>(h.I.CargoRig.Placed); // a copy: Isolate below rebuilds the live list
                int l0 = 0, l1 = 0;
                float top0 = float.MinValue, bottom1 = float.MaxValue;
                foreach (Cargo.PlacedItem pl in placed)
                {
                    if (pl.Layer == 0)
                    {
                        l0++;
                        top0 = Mathf.Max(top0, pl.Pos.y + pl.Box.y);
                    }
                    else
                    {
                        l1++;
                        bottom1 = Mathf.Min(bottom1, pl.Pos.y);
                    }
                }
                Check(placed.Count == 4 && l0 == 2 && l1 == 2, $"two layers of two ({placed.Count} placed: {l0} on the bed, {l1} above; {h.I.CargoRig.LastLayoutNote})");
                Check(l1 > 0 && bottom1 > top0 - 0.1f && bottom1 < top0 + 0.1f, $"the upper layer stands on the lower one (its bottom {bottom1:F2}, the lower top {top0:F2})");
                // The relations: the bed, a beside partner, an on-top support in the layer below, a beside partner above.
                Check(placed.Count == 4 && placed[0].Relation == "bed" && placed[1].Partner == 0 && (placed[1].Relation == "side" || placed[1].Relation == "along")
                    && placed[2].Relation == "on" && placed[2].Partner >= 0 && placed[2].Partner <= 1 && placed[3].Partner == 2 && (placed[3].Relation == "side" || placed[3].Relation == "along"),
                    $"the relations are recorded (bed; {placed[1].Relation} 0; {placed[2].Relation} {placed[2].Partner}; {placed[3].Relation} {placed[3].Partner})");
                Check(placed.Count == 4 && placed[2].Rel.y > 0.1f && Vector3.Distance(placed[3].LayerPos - placed[2].LayerPos, placed[3].Rel) < 0.001f, "relative places: the upper item is above its support; the beside partner's place relative to the layer agrees");
                // Isolation: two of the four, laid out as a two-item cart in the asked relation; the load untouched.
                h.I.CargoRig.Isolate(placed[0].Guid, placed[2].Guid, "stack");
                yield return Frames(1);
                Check(h.I.CargoRig.Isolating && h.I.CargoRig.Placed.Count == 2 && h.I.CargoRig.LastPairMode == "stack" && h.I.CargoRig.Count == 4, $"isolated: two drawn as a stack ({h.I.CargoRig.Placed.Count} placed, {h.I.CargoRig.LastPairMode}), four still on the cart");
                h.I.CargoRig.Isolate(placed[0].Guid, placed[1].Guid, "beside");
                yield return Frames(1);
                Check(h.I.CargoRig.Placed.Count == 2 && h.I.CargoRig.LastPairMode != "stack", $"isolated beside: two drawn side by side ({h.I.CargoRig.LastPairMode})");
                h.I.CargoRig.ClearIsolation();
                yield return Frames(1);
                Check(!h.I.CargoRig.Isolating && h.I.CargoRig.Placed.Count == 4, "back to the whole load: four drawn again");

                // The door path: a new view gets a new inventory and the carried-over items go back on. Unhitch and hitch
                // re-make the cart's inventory the same way.
                left = Mounts.Unhitch(h);
                Check(left != null && left.Exists, "unhitched: a free cart");
                yield return Until(() => h.IsReady && (left == null || left.IsReady), 4f);
                if (left != null && left.Exists)
                {
                    int ls = left.I.CargoRig.Inv != null ? left.I.CargoRig.Inv.Data.InventorySize : -1;
                    Check(left.I.CargoRig.Count == 4 && ls == 4, $"the free cart took all four on a 4-slot inventory ({left.I.CargoRig.Count} on it, {ls} slots)");
                    MountResult back = Mounts.Hitch(h, left);
                    Check(back.Ok, $"hitched again{(back.Ok ? "" : " (" + back.Reason + ")")}");
                    yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 4f);
                    Check(h.I.CargoRig.Count == 4 && Inv() != null && Inv().Data.InventorySize == 4, $"all four back on the re-made inventory, nothing dropped ({h.I.CargoRig.Count} on it, {Inv()?.Data.InventorySize} slots)");
                }

                // Lowered with four aboard: the size stays at the load; two off and it follows.
                Mounts.Behaviour.Set(Owner, "CartSlots", 2);
                Check(Inv() != null && Inv().Data.InventorySize == 4, "lowered to 2 with four aboard: the size stays at the load (4)");
                Check(Inv() != null && !Inv().AddItemToInventory(new Item("wood")), "and a fifth is still refused");
                int removed = 0;
                if (Inv() != null)
                {
                    foreach (Item it in new List<Item>(Inv().Data.Inventory))
                    {
                        if (removed < 2 && guids.Contains(it.UniqueId.ToString()))
                        {
                            Inv().RemoveItemFromInventoryByUID(it);
                            removed++;
                        }
                    }
                }
                yield return Frames(1);
                Check(removed == 2 && h.I.CargoRig.Count == 2 && Inv() != null && Inv().Data.InventorySize == 2, $"two off: the size follows the setting ({h.I.CargoRig.Count} on it, {Inv()?.Data.InventorySize} slots)");
            }
            finally
            {
                Mounts.Behaviour.Clear("CartSlots");
                if (owner != null) Mounts.Behaviour.Set(owner, "CartSlots", was);
                // The test items were never in the world: off whichever cart holds them.
                foreach (MountHandle hh in new[] { h, left })
                {
                    if (hh == null || !hh.Exists || hh.I.CargoRig.Inv == null) continue;
                    foreach (Item it in new List<Item>(hh.I.CargoRig.Inv.Data.Inventory))
                    {
                        if (guids.Contains(it.UniqueId.ToString())) hh.I.CargoRig.Inv.RemoveItemFromInventoryByUID(it);
                    }
                }
                if (left != null && left.Exists) left.Remove();
                h.Remove();
            }
        }
    }
}
