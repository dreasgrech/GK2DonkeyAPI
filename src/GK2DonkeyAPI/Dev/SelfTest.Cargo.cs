using System;
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
        // A cart with a hitch gap (a long mount: 9 px for one; 24 for another, while it pulled the cart) keeps its place as items go on it. Found in game
        // 2026-10-21: each item moved the cart back by one more gap (a cargo change put the parts back to their gapped
        // place, then read that as the prefab's rest and added the gap again).
        internal static IEnumerator CargoKeepsTheGap()
        {
            MountHandle h = Spawn(Spot(home, 4f) ?? home, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            string owner = Mounts.Behaviour.OverriddenBy("CartSlots");
            object was = owner == null ? null : Mounts.Behaviour.Get<object>("CartSlots");
            try
            {
                yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
                if (h.I.CargoRig.Inv == null)
                {
                    Fail("the test cart has no inventory");
                    yield break;
                }
                Mounts.Behaviour.ClearAny("CartSlots");
                Mounts.Behaviour.Set(Owner, "CartSlots", 4);
                h.Tuning.Set(Owner, "HitchGapPx", 20);
                yield return Frames(3);
                float Along()
                {
                    Vector3? cart = h.I.TestCartBodyWorld();
                    if (cart == null) return float.NaN;
                    Vector3 v = cart.Value - h.I.BodyPoint();
                    v.y = 0f;
                    return v.magnitude;
                }
                float start = Along();
                Check(!float.IsNaN(start) && Mathf.Abs(h.I.TestHitchGap() - 0.4f) < 0.001f, $"the cart stands with a 20 px gap ({start:F2} behind the body)");
                float worst = 0f;
                var seen = new List<string>();
                int recollects = h.I.TestCargoRecollects;
                for (int n = 1; n <= 4; n++)
                {
                    h.I.CargoRig.Inv.AddItemToInventory(new Item("wood"));
                    yield return Frames(3);
                    float now = Along();
                    seen.Add(now.ToString("F2"));
                    worst = Mathf.Max(worst, Mathf.Abs(now - start));
                }
                Check(h.I.TestCargoRecollects - recollects >= 4, $"each item re-collected the cart parts ({h.I.TestCargoRecollects - recollects} times: the path the creep came from)");
                Check(worst < 0.03f, $"four items on, one at a time: the cart stays where it was ({start:F2} -> {string.Join(", ", seen)}; the gap is 0.40, a creep shows as 0.40 a step)");
            }
            finally
            {
                Mounts.Behaviour.ClearAny("CartSlots");
                if (owner != null && was != null) Mounts.Behaviour.Set(owner, "CartSlots", was);
                if (h.Exists) h.Remove();
            }
        }

        // Game 1.012 pools the big drop pictures (docs/game-update-26624074.md): a cargo view clones the keeper's overhead
        // atom while the keeper carries something, and must show one picture, the keeper keeping his; an item drawn with
        // a stand-in (marble, the mechanism box) finds it among the pool's names, not the game's placeholder.
        internal static IEnumerator CargoPooledPictures()
        {
            PlayerData pd = MainGame.PlayerData;
            if (pd == null)
            {
                Skip("no game loaded");
                yield break;
            }
            MountHandle h = Spawn(Spot(home, 4f) ?? home, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            Item carried = null;
            try
            {
                yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
                Inventory inv = h.I.CargoRig.Inv;
                if (inv == null)
                {
                    Fail("the test cart has no inventory");
                    yield break;
                }
                DropViewAtomMesh keeper = Cargo.FindSourceAtom();
                if (keeper == null)
                {
                    Fail("the keeper's overhead atom was not found");
                    yield break;
                }
                // the keeper carries a log (the game's own add: not riding, the reroute is off)
                carried = new Item("wood");
                pd.AddOverheadItem(carried);
                yield return Frames(3);
                if (!pd.TryGetOverheadItem(it => it == carried, out _))
                {
                    carried = null;
                    Skip("the keeper did not take the log overhead");
                    yield break;
                }
                DropViewAtomMeshElement shown = keeper.MeshElement;
                Check(shown != null && shown.gameObject.activeInHierarchy, $"the keeper's atom shows the log ({(shown == null ? "nothing" : "'" + shown.name + "'")})");
                // a cargo view made now, cloned from that atom
                h.I.CargoRig.TestForgetViews();
                inv.AddItemToInventory(new Item("wood"));
                yield return Frames(3);
                DropViewAtomMesh view = h.I.CargoRig.PlacedViews.Count == 1 ? h.I.CargoRig.PlacedViews[0] : null;
                int on = view == null ? 0 : view.GetComponentsInChildren<DropViewAtomMeshElement>(includeInactive: false).Length;
                string onNames = view == null ? "" : string.Join(", ", System.Array.ConvertAll(view.GetComponentsInChildren<DropViewAtomMeshElement>(includeInactive: false), e => e.name));
                string logIcon = Cargo.ResolveIcon(GameBalance.Me.GetData<ItemDef>("wood"));
                Check(view != null && on == 1 && view.MeshElement != null && Cargo.PictureName(view.MeshElement.name) == logIcon,
                    $"the cart shows one picture, the log ('{logIcon}'; {h.I.CargoRig.PlacedViews.Count} view(s), {on} picture(s) on: {onNames})");
                Check(keeper.MeshElement == shown && shown != null && shown.gameObject.activeInHierarchy, $"the keeper keeps his ({(keeper.MeshElement == null ? "nothing" : "'" + keeper.MeshElement.name + "'")})");
                // the stand-in (cargo-layout.cfg: marble has no picture of its own and is shown with the slab's) and the
                // mechanism box (its picture was named by a prefix, i_box_engineering_mec, until 1.012 gave it the full
                // name): both found among the pool's names
                foreach (string id in new[] { "marble", "box_engineering_mechanism" })
                {
                    ItemDef def = GameBalance.Me.GetData<ItemDef>(id);
                    if (def == null)
                    {
                        Warn($"no {id} item definition");
                        continue;
                    }
                    string icon = Cargo.ResolveIcon(def);
                    bool borrowed = id == "marble";
                    Check(Cargo.PictureKnown(icon) && (!borrowed || icon != def.iconId), $"{id} resolves to a picture the pool has ('{icon}' for icon '{def.iconId}'{(borrowed ? ", a stand-in" : "")})");
                    inv.AddItemToInventory(new Item(id));
                    yield return Frames(3);
                    DropViewAtomMesh standIn = null;
                    for (int i = 0; i < h.I.CargoRig.Placed.Count && i < h.I.CargoRig.PlacedViews.Count; i++)
                    {
                        if (h.I.CargoRig.Placed[i].Id == id) standIn = h.I.CargoRig.PlacedViews[i];
                    }
                    string standInShown = standIn?.MeshElement == null ? "nothing" : Cargo.PictureName(standIn.MeshElement.name);
                    Check(standIn != null && standInShown == icon, $"the cart draws {id} as that stand-in (shows '{standInShown}')");
                }
                // nails: the first run on 1.012 drew them as the game's placeholder; what they resolve to, for the record
                ItemDef nails = GameBalance.Me.GetData<ItemDef>("nails_bronze");
                if (nails != null)
                {
                    string icon = Cargo.ResolveIcon(nails);
                    Info($"nails_bronze (icon '{nails.iconId}') resolve to '{icon}'{(Cargo.PictureKnown(icon) ? ", a picture the pool has" : ", no picture of that name: the game's placeholder")}");
                }
            }
            finally
            {
                if (carried != null)
                {
                    try { pd.RemoveOverheadItem(carried); } catch (Exception e) { Warn("taking the log off the keeper's head failed: " + e.Message); }
                }
                if (h.Exists) h.Remove();
            }
        }

        internal static IEnumerator CargoLayers()
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
                Mounts.Behaviour.ClearAny("CartSlots"); // a mod may hold it: taken over for the test, given back in the finally
                Mounts.Behaviour.Set(Owner, "CartSlots", 4);
                Check(Inv().Data.InventorySize == 4, $"the override applies to the standing cart at once (4 slots; was {before})");
                int added = 0;
                foreach (string id in new[] { "wood", "wood", "wood", "wood" }) // a pair tuned side by side: two layers of two whatever else the layout says
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
                // The upper layer rests on the lower one: by the boxes within 10 cm of their top; with the measured shapes it
                // may sit lower (on a round log's trunk, under the box's stub), never higher.
                bool shapes = Plugin.CargoSeatOnShapes.Value && CargoShapes.Count > 0;
                Check(l1 > 0 && bottom1 > top0 - (shapes ? 0.35f : 0.1f) && bottom1 < top0 + 0.1f, $"the upper layer stands on the lower one (its bottom {bottom1:F2}, the lower boxes' top {top0:F2}{(shapes ? ", seated on the measured shapes" : "")})");
                // The relations: the bed, a beside partner, an on-top support in the layer below, a beside partner above.
                Check(placed.Count == 4 && placed[0].Relation == "bed" && placed[1].Partner == 0 && (placed[1].Relation == "side" || placed[1].Relation == "along")
                    && placed[2].Relation == "on" && placed[2].Partner >= 0 && placed[2].Partner <= 1 && placed[3].Partner == 2 && (placed[3].Relation == "side" || placed[3].Relation == "along"),
                    $"the relations are recorded (bed; {placed[1].Relation} 0; {placed[2].Relation} {placed[2].Partner}; {placed[3].Relation} {placed[3].Partner})");
                Check(placed.Count == 4 && placed[2].Rel.y > 0.1f && Vector3.Distance(placed[3].LayerPos - placed[2].LayerPos, placed[3].Rel) < 0.001f, "relative places: the upper item is above its support; the beside partner's place relative to the layer agrees");
                // Isolation is the ridden cart's (the panel's Cargo tab): on a standing cart it is let go at the next layout and
                // the whole load stays drawn (IsolationHolds: "a parked cart shows its whole load"). The checks written
                // 2026-10-10 expected a standing cart to isolate; found failing 2026-10-14 and rewritten to the rule.
                h.I.CargoRig.Isolate(placed[0].Guid, placed[2].Guid, "stack");
                yield return Frames(1);
                Check(!h.I.CargoRig.Isolating && h.I.CargoRig.Placed.Count == 4 && h.I.CargoRig.Count == 4, $"isolating on a standing cart is let go: four drawn, four on the cart ({h.I.CargoRig.Placed.Count} placed, isolating {h.I.CargoRig.Isolating})");
                h.I.CargoRig.ClearIsolation();
                yield return Frames(1);
                Check(!h.I.CargoRig.Isolating && h.I.CargoRig.Placed.Count == 4, "clearing a let-go isolation changes nothing: four drawn");

                // The door path: a new view gets a new inventory and the carried-over items go back on. Unhitch and hitch
                // re-make the cart's inventory the same way.
                left = Mounts.Unhitch(h);
                Check(left != null && left.Exists, "unhitched: a free cart");
                yield return Until(() => h.IsReady && (left == null || left.IsReady), 4f);
                if (left != null && left.Exists)
                {
                    int ls = left.I.CargoRig.Inv != null ? left.I.CargoRig.Inv.Data.InventorySize : -1;
                    Check(left.I.CargoRig.Count == 4 && ls == 4, $"the free cart took all four on a 4-slot inventory ({left.I.CargoRig.Count} on it, {ls} slots)");
                    // The events at a hitch: Hitched(donkey, oldCart) first, then the old cart's InstanceRemoved (a listener
                    // that drops its handle on the removal must still recognise the cart in Hitched; 2026-10-14).
                    var order = new List<string>();
                    Action<MountHandle, MountHandle> onHitched = (d, c) => order.Add(d == h && c == left ? "Hitched" : "Hitched(other)");
                    Action<MountHandle> onRemoved = r => order.Add(r == left ? "Removed" : "Removed(other)");
                    Mounts.Hitched += onHitched;
                    Mounts.InstanceRemoved += onRemoved;
                    MountResult back;
                    try
                    {
                        back = Mounts.Hitch(h, left);
                    }
                    finally
                    {
                        Mounts.Hitched -= onHitched;
                        Mounts.InstanceRemoved -= onRemoved;
                    }
                    Check(back.Ok, $"hitched again{(back.Ok ? "" : " (" + back.Reason + ")")}");
                    Check(order.Count == 2 && order[0] == "Hitched" && order[1] == "Removed", $"at a hitch, Hitched comes before the old cart's InstanceRemoved ({string.Join(", ", order)})");
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
                Mounts.Behaviour.ClearAny("CartSlots"); // the test takes the key over from the mod that holds it and gives it back: the dev way past the owner rule
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
