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
        // Saddle Up's Safe spot rows: the toggle drives FallGuard, the button brings him back, the key reaches ReturnKey (from
        // the API's "back to the last safe spot" scenario, step 5, 2026-10-27). Its own start: this spot recorded, open ground
        // 7-10 away to be put at.
        internal static IEnumerator SaddleUpSafeSpotRows()
        {
            Vector3 me = Keeper();
            yield return Seconds(1.2f); // this spot recorded
            Vector3? away = Spot(me, 10f) ?? Spot(me, 7f);
            if (!away.HasValue) { Skip("no open ground 7-10 from here to be put at"); yield break; }
            int returns;
            Vector3 now;
            // 5. Saddle Up's rows: the toggle, the button, the key, in a section of their own at the end ("Safe spot", 2026-10-27)
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null) Info("Saddle Up's page isn't shown: its rows not checked");
            else
            {
                MountSettingsPage.Row guard = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "FallGuard" && x.ShownSection == "Safe spot");
                MountSettingsPage.Row button = saddle.Rows.Find(x => x.RowKind == MountSettingsPage.RowKind.Button && x.ShownSection == "Safe spot" && x.Label == "Back to the last safe spot");
                MountSettingsPage.Row key = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "ReturnKey" && x.ShownSection == "Safe spot");
                Check(guard != null && button != null && key != null, $"Saddle Up has the FallGuard toggle, the button and the ReturnKey row ({(guard != null ? "toggle" : "no toggle")}, {(button != null ? "button \"" + button.ButtonText + "\"" : "no button")}, {(key != null ? "key" : "no key")})");
                if (guard != null)
                {
                    var ge = (BepInEx.Configuration.ConfigEntry<bool>)guard.Entry;
                    bool was = ge.Value;
                    try
                    {
                        ge.Value = false;
                        yield return null;
                        Check(Plugin.FallGuard != null && !Plugin.FallGuard.Value, "the toggle off: the Behaviour key FallGuard follows");
                    }
                    finally { ge.Value = was; }
                    yield return null;
                    Check(Plugin.FallGuard != null && Plugin.FallGuard.Value == was, "and back");
                }
                if (button != null)
                {
                    returns = SafeSpots.TestReturns;
                    PutKeeper(away.Value, homeFacing);
                    yield return Frames(3);
                    button.OnClick();
                    yield return Frames(3);
                    now = Keeper();
                    Check(SafeSpots.TestReturns == returns + 1 && Vector3.Distance(now, Mounts.LastSafeSpot ?? me) < 0.5f, $"the button brings him back from 10 away ({Vector3.Distance(now, Mounts.LastSafeSpot ?? me):0.00} from the spot)");
                }
                if (key != null)
                {
                    var ke = (BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut>)key.Entry;
                    var was = ke.Value;
                    try
                    {
                        var ctrlK = new BepInEx.Configuration.KeyboardShortcut(KeyCode.K, KeyCode.LeftControl);
                        ke.Value = ctrlK;
                        yield return null;
                        Check(Plugin.ReturnKey != null && Flight.SameKey(Plugin.ReturnKey.Value, ctrlK), $"the ReturnKey row set to Ctrl+K: the Behaviour key follows ({Plugin.ReturnKey?.Value})");
                    }
                    finally { ke.Value = was; }
                }
            }

            yield break;
        }
    }
}
