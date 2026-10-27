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
        // Saddle Up's two indoors rows (read, not changed: they are the player's): the second greys while the first is off,
        // the first reaches the API (from the API's "indoors: the two switches" scenario, 2026-10-27).
        internal static IEnumerator SaddleUpIndoorsRows()
        {
            MountSettingsPage saddle0 = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle0 == null) { Skip("GK2 Saddle Up's page isn't shown"); yield break; }
            // Saddle Up's two rows (read, not changed: they are the player's): the second greys while the first is off
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle != null)
            {
                MountSettingsPage.Row ride = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "MountIndoors");
                MountSettingsPage.Row cartRow = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "CartIndoors");
                bool rideOn = ride?.Entry is BepInEx.Configuration.ConfigEntry<bool> re && re.Value;
                Check(ride != null && cartRow != null && cartRow.Enabled != null && cartRow.Enabled() == rideOn,
                    $"Saddle Up's rows: \"Allow mounting indoors\" ({(ride == null ? "missing" : rideOn ? "on" : "off")}) and \"... with the cart\" ({(cartRow == null ? "missing" : cartRow.Enabled != null && cartRow.Enabled() ? "live" : "greyed")})");
                Check(Mounts.Behaviour.Get<bool>("DismountIndoors") == !rideOn, $"the first row reaches the API (DismountIndoors {Mounts.Behaviour.Get<bool>("DismountIndoors")})");
            }
            yield break;
        }
    }
}
