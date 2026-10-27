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
        // Saddle Up's keys while the Mounts panel is open: only the panel's own key works there (the user, 2026-10-22: H in the
        // panel got him on and off). From the API's "the Mounts panel's Ride and Drive buttons" scenario, 2026-10-27.
        internal static IEnumerator SaddleUpKeysOverPanel()
        {
            // the keys while the panel is open: only the panel's own key works there (the user, 2026-10-22: H in the panel got him on and off)
            bool? ride = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "RideKey"), cart = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "CartKey"), panelKey = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "MountsKey");
            if (ride == null) Info("Saddle Up's keys aren't polled: the over-the-panel check skipped");
            else Check(ride == false && cart == false && panelKey == true, $"with the panel open the ride and cart keys wait, the panel key works (ride {ride}, cart {cart}, panel {panelKey})");
            yield break;
        }
    }
}
