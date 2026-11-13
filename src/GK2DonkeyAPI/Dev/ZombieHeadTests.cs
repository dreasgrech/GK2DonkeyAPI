using System;
using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A zombie with the game's event head on the cart (docs/game-update-26404424.md "Halloween"; the user, 2026-11-04:
    // "build them now"). The game's Halloween table draws head 1051 as 1060 from 24 October to 7 November; the cart's
    // views drew the item's icon instead, with a warning, their check comparing the picture with the stored head. A test
    // zombie with head 1051 on a test cart, the swap forced off and then on (HalloweenSwap, any date), the views drawn
    // afresh each time: a lying zombie switched on, the stored head's picture off and the table's on, never the icon.
    internal static class ZombieHeadTests
    {
        private const int Head = 1051;

        internal static IEnumerator EventHeadOnTheCart()
        {
            PlayerData pd = MainGame.PlayerData;
            if (pd == null || MainGame.ZombieSystemData == null)
            {
                SelfTest.Skip("no game loaded");
                yield break;
            }
            if (!HalloweenSwap.Begin(out string error))
            {
                SelfTest.Skip(error);
                yield break;
            }
            MountHandle h = null;
            ZombieWgoData z = null;
            Inventory inv = null;
            try
            {
                HalloweenSwap.Set(true);
                int eventHead = HalloweenHeadSkinOverride.Resolve(Head);
                HalloweenSwap.Set(false);
                if (eventHead == Head)
                {
                    SelfTest.Skip($"the game's table swaps head {Head} for none now (a game update): nothing to draw");
                    yield break;
                }
                h = SelfTest.Spawn(SelfTest.Spot(SelfTest.home, 4f) ?? SelfTest.home, true, withCart: true);
                if (h == null)
                {
                    SelfTest.Fail("could not spawn a donkey with a cart");
                    yield break;
                }
                yield return SelfTest.Until(() => h.IsReady && h.I.CargoRig.Inv != null, 6f);
                inv = h.I.CargoRig.Inv;
                if (inv == null)
                {
                    SelfTest.Fail("the test cart has no inventory");
                    yield break;
                }
                // a real zombie, made as the game's Drop Zombie node makes one (CarryProbe), its head set as the sheet does
                Item body = CarryProbe.NewZombieBody(out string how);
                if (body == null)
                {
                    SelfTest.Skip("no body definition to make a zombie from");
                    yield break;
                }
                z = MainGame.ZombieSystemData.CreateZombieDrop("zombie", pd.position.Value, pd.currentGameSceneId, body);
                ZombieSkinHelper.ApplySkinToZombieWgoData(z, z.GetGameResInt("zombie_body_id"), Head, z.GameResStr.Get("zombie_body_lut"), z.GameResStr.Get("zombie_head_lut"));
                bool added = inv.AddItemToInventory(z.ZombieItem);
                if (added) HeadReroute.RelinkZombie(inv, z.ZombieItem, "a self-test");
                SelfTest.Check(added && z.GetGameResInt("zombie_head_id") == Head, $"a test zombie with head {Head} on the test cart (made from {how}; head {z.GetGameResInt("zombie_head_id")})");
                if (!added) yield break;
                foreach (bool on in new[] { false, true })
                {
                    HalloweenSwap.Set(on);
                    h.I.CargoRig.TestForgetViews();
                    h.I.CargoRig.RefreshViews();
                    yield return SelfTest.Frames(2);
                    string want = "drops_item_zombie_pose2_t" + (on ? eventHead : Head).ToString("D4");
                    DropViewAtomMesh view = h.I.CargoRig.PlacedViews.Count == 1 ? h.I.CargoRig.PlacedViews[0] : null;
                    DropViewAtomMeshElement shown = view?.MeshElement;
                    bool drawn = shown != null && Drawn(shown.transform, view.transform);
                    // the picture is the pool's instance since game 1.012, named "<prototype>(Clone)"
                    SelfTest.Check(shown != null && Cargo.PictureName(shown.name) == want && drawn,
                        $"the swap {(on ? $"on: the game's head {eventHead}" : "off: its own head")} drawn lying on the cart ({want}; shows {(shown == null ? "nothing" : "'" + shown.name + "' " + (drawn ? "drawn" : "switched off"))}, {h.I.CargoRig.PlacedViews.Count} view(s)), not the item's icon");
                }
            }
            finally
            {
                HalloweenSwap.End();
                // the zombie off the cart and gone before the donkey (removed with its cart, the item would go and leave
                // the zombie without it)
                try
                {
                    if (z != null)
                    {
                        if (inv != null)
                        {
                            foreach (Item it in new System.Collections.Generic.List<Item>(inv.Data.Inventory))
                            {
                                if (it != null && !it.IsEmpty && it.UniqueId.Guid == z.UniqueId.Guid) inv.RemoveItemFromInventoryByUID(it);
                            }
                        }
                        MainGame.ZombieSystemData.RemoveZombie(z.UniqueId);
                    }
                }
                catch (Exception e)
                {
                    SelfTest.Warn("removing the test zombie failed: " + e.Message);
                }
                h?.Remove();
            }
        }

        private static bool Drawn(Transform t, Transform view)
        {
            for (; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == view) return true;
            }
            return false;
        }
    }
}
