using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    internal static partial class Tests
    {
        // Saddle Up's keys while the Mounts panel is open: the panel's own key works there, and the ride and cart keys only as
        // the journal's Ride and Drive the cart buttons (the user, 2026-10-22: H in the panel got him on and off; 2026-11-07:
        // H and G in the journal call the mount picked). From the API's "the Mounts panel's Ride and Drive buttons" scenario, 2026-10-27.
        internal static IEnumerator SaddleUpKeysOverPanel()
        {
            bool? ride = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "RideKey"), cart = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "CartKey"), panelKey = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "MountsKey");
            if (ride == null)
            {
                Info("Saddle Up's keys aren't polled: the over-the-panel check skipped");
                yield break;
            }
            Check(ride == false && cart == false && panelKey == true, $"with the panel open the ride and cart keys don't act as keys, the panel key works (ride {ride}, cart {cart}, panel {panelKey})");
            MountStyle? rideIn = null, cartIn = null;
            bool own = true;
            MountSettingsPage page = null;
            foreach (MountSettingsPage p in MountSettingsPage.All) if (p.OwnerId == "dreas.gk2.saddleup" && p.HasMountRow) { page = p; break; }
            foreach (var k in KeyActions.TestJournalKeys("dreas.gk2.saddleup"))
            {
                if (k.Key == "RideKey") rideIn = k.Style;
                else if (k.Key == "CartKey") cartIn = k.Style;
                if (page == null || !page.IsCompanion(k.Of)) own = false;
            }
            Check(rideIn == MountStyle.Ride && cartIn == MountStyle.Cart && own, $"in the journal they are its Ride and Drive the cart buttons, through Saddle Up's own companion (ride {rideIn?.ToString() ?? "none"}, cart {cartIn?.ToString() ?? "none"}, its companion {own})");
        }
    }
}
