using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // A corpse into the morgue (MorgueHandover.cs): the door rule, and the hand-over off a parked cart (the switch, the
    // hands, the bodies' count). The door itself is a hand test: drive the cart into the morgue with a body aboard.
    internal static partial class SelfTest
    {
        internal static IEnumerator MorgueDecisions()
        {
            string places = "morgue, tp_RT_morgue_exit, tp_morgue_to_graveyard";
            Check(Mount.MorgueHandover.Decide("morgue", null, places, out string w1) && w1 == "the morgue area", "the morgue area decides");
            Check(Mount.MorgueHandover.Decide(null, "tp_RT_morgue_exit", places, out string w2) && w2 == "the door to tp_RT_morgue_exit", "the morgue door decides before the area is known");
            Check(!Mount.MorgueHandover.Decide("graveyard", "tp_RT_home_exit", places, out _), "another area and door: no");
            Check(!Mount.MorgueHandover.Decide(null, null, places, out _), "nothing known yet: no");
            Check(Mount.MorgueHandover.Decide("Morgue", null, " MORGUE ,", out _), "case and spaces don't matter");
            Check(!Mount.MorgueHandover.Decide("morgue", "tp_RT_morgue_exit", "", out _), "an empty list matches nowhere");
            Check(Mount.MorgueHandover.Decide("morgue", null, Plugin.CorpseIntoMorguePlaces.Value, out _), $"the setting in effect names the morgue area ({Plugin.CorpseIntoMorguePlaces.Value})");
            yield break;
        }

        internal static IEnumerator MorgueHandOver()
        {
            PlayerData pd = PC.PlayerData;
            if (pd.HasOverheadItem)
            {
                Warn("the keeper holds something: skipped (run it with empty hands)");
                yield break;
            }
            MountHandle h = Spawn(Spot(home, 4f) ?? home, true, withCart: true);
            if (h == null)
            {
                Fail("could not spawn a donkey with a cart");
                yield break;
            }
            yield return Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
            Mount.Instance i = h.I;
            if (i.CargoRig.Inv == null)
            {
                Fail("the test cart has no inventory");
                h.Remove();
                yield break;
            }
            int bodies0 = MainGame.PlayerData.GetResInt("cur_bodies_count");
            string owner = Mounts.Behaviour.OverriddenBy("CorpseIntoMorgue");
            object was = owner == null ? null : Mounts.Behaviour.Get<object>("CorpseIntoMorgue");
            // The cart's bodies are matched by GUID, read from the cart: the game's inventory holds a split-off instance
            // of what was added, a new Item with its own GUID (Item.Split; run 1 of 2026-10-10: the hand-over worked,
            // the test's reference check failed and its cleanup missed, so the body stayed in the keeper's hands).
            var handsBefore = new HashSet<string>();
            foreach (Item it in pd.OverheadItems) handsBefore.Add(it.UniqueId.ToString());
            string g1 = null, g2 = null;
            bool Held(string g) { foreach (Item it in pd.OverheadItems) if (it.UniqueId.ToString() == g) return true; return false; }
            string CorpseOnCart(string not) { foreach (Item it in i.CargoRig.ItemsNow()) if (it.id == Cargo.CorpseId && it.UniqueId.ToString() != not) return it.UniqueId.ToString(); return null; }
            try
            {
                Check(i.CargoRig.Inv.AddItemToInventory(NewBody()), "a body put on the parked test cart");
                yield return Frames(1);
                g1 = CorpseOnCart(null);
                Check(g1 != null && Mount.MorgueHandover.HasCorpse(i), "the cart reports a corpse aboard");
                Mounts.Behaviour.ClearAny("CorpseIntoMorgue"); // a mod may hold it: taken over for the test, given back in the finally
                Mounts.Behaviour.Set(Owner, "CorpseIntoMorgue", false);
                int n0 = Mount.MorgueHandover.HandOver(i, pd, "the test");
                Check(n0 == 0 && i.CargoRig.Count == 1 && !pd.HasOverheadItem, "switched off: the body stays on the cart");
                Mounts.Behaviour.Set(Owner, "CorpseIntoMorgue", true);
                int n1 = Mount.MorgueHandover.HandOver(i, pd, "the test");
                Check(n1 == 1 && Held(g1) && i.CargoRig.Count == 0, $"switched on: the body is in the keeper's hands and off the cart ({n1} handed, cart {i.CargoRig.Count}, held {pd.OverheadCount})");
                // A second body with the hands full stays on the cart (the game's one slot; more with extra_overhead).
                Check(i.CargoRig.Inv.AddItemToInventory(NewBody()), "a second body put on the cart");
                yield return Frames(1);
                g2 = CorpseOnCart(g1);
                int n2 = Mount.MorgueHandover.HandOver(i, pd, "the test");
                if (pd.OverheadStackLimit == 1)
                {
                    Check(n2 == 0 && i.CargoRig.Count == 1, "hands full: the second body stays on the cart");
                }
                else
                {
                    Info($"the keeper has {pd.OverheadStackLimit} hand slots: {n2} more handed over, cart {i.CargoRig.Count}");
                }
                Check(MainGame.PlayerData.GetResInt("cur_bodies_count") == bodies0, "the bodies count is unchanged");
            }
            finally
            {
                Mounts.Behaviour.ClearAny("CorpseIntoMorgue");
                if (owner != null) Mounts.Behaviour.Set(owner, "CorpseIntoMorgue", was);
                // The test bodies away: out of the hands (anything held now that was not held before the test) and off
                // the cart (they were never in the world).
                int cleared = 0;
                foreach (Item it in new List<Item>(pd.OverheadItems))
                {
                    if (!handsBefore.Contains(it.UniqueId.ToString()))
                    {
                        pd.RemoveOverheadItem(it);
                        cleared++;
                    }
                }
                if (i.CargoRig.Inv != null)
                {
                    foreach (Item it in new List<Item>(i.CargoRig.Inv.Data.Inventory))
                    {
                        string g = it.UniqueId.ToString();
                        if (g == g1 || g == g2) i.CargoRig.Inv.RemoveItemFromInventoryByUID(it);
                    }
                }
                Check(pd.OverheadCount == handsBefore.Count, $"the keeper's hands are as they were ({cleared} test body(ies) taken out of them)");
                h.Remove();
            }
        }
    }
}
