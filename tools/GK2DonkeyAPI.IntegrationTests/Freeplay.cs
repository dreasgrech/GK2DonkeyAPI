using System;
using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    internal static partial class Tests
    {
        // Saddle Up's Freeplay rows (from the API's "freeplay" scenario, step 4, 2026-10-27): a section of their own at the
        // end, the dropdown's words the API's, shown only with the switch on, each row driving its Behaviour key, and a
        // mount of the test's own (cargo 2, Very slow) following them.
        internal static IEnumerator SaddleUpFreeplayRows()
        {
            const string owner = Owner + ".freeplayrows", beast = "selftest.freeplayrows-beast";
            IReadOnlyList<string> words = Mounts.Catalog.SpeedWords;
            var held = new BehaviourHeld[FreeplayKeys.Length];
            for (int i = 0; i < held.Length; i++) held[i] = new BehaviourHeld(FreeplayKeys[i]);
            IDisposable reg = Mounts.Catalog.Register(owner, new MountDef { Id = beast, Name = "Freeplay Rows Beast", Speed = SpeedBand.VerySlow, Cargo = 2 });
            Catalog.Entry k0 = Catalog.Find(beast);
            try
            {
                // 4. Saddle Up's rows: a section of their own at the end, the dropdown's words the API's, shown only with the
                // switch on, and each row driving its Behaviour key
                MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
                if (saddle == null) Info("Saddle Up's page isn't shown: its Freeplay rows not checked");
                else
                {
                    MountSettingsPage.Row Row(string key) => saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == key && x.ShownSection == "Freeplay");
                    MountSettingsPage.Row cargoRow = Row("MaxCargo"), sameRow = Row("SameSpeed"), speedRow = Row("Speed");
                    Check(cargoRow != null && sameRow != null && speedRow != null && cargoRow.RowKind == MountSettingsPage.RowKind.Toggle && sameRow.RowKind == MountSettingsPage.RowKind.Toggle && speedRow.RowKind == MountSettingsPage.RowKind.Choice,
                        $"Saddle Up's Freeplay section has the two switches and the speed dropdown ({(cargoRow != null ? "max cargo" : "no max cargo")}, {(sameRow != null ? "same speed" : "no same speed")}, {(speedRow != null ? "speed " + speedRow.RowKind : "no speed")})");
                    MountSettingsPage.Row lastRow = null;
                    foreach (MountSettingsPage.Row x in saddle.Rows) if (!x.IsLook && !string.IsNullOrEmpty(x.ShownSection)) lastRow = x;
                    Check(lastRow != null && lastRow.ShownSection == "Freeplay", $"Freeplay is the page's last section ({lastRow?.ShownSection})");
                    if (cargoRow != null && sameRow != null && speedRow != null)
                    {
                        Check(speedRow.ChoiceLabels != null && speedRow.ChoiceLabels.Length == words.Count && string.Join("|", speedRow.ChoiceLabels) == string.Join("|", words),
                            $"the dropdown's words are the API's ({(speedRow.ChoiceLabels == null ? "none" : string.Join(", ", speedRow.ChoiceLabels))})");
                        var ce = (BepInEx.Configuration.ConfigEntry<bool>)cargoRow.Entry;
                        var se = (BepInEx.Configuration.ConfigEntry<bool>)sameRow.Entry;
                        var be = (BepInEx.Configuration.ConfigEntry<SpeedBand>)speedRow.Entry;
                        bool ceWas = ce.Value, seWas = se.Value;
                        SpeedBand beWas = be.Value;
                        try
                        {
                            foreach (string k in FreeplayKeys) Mounts.Behaviour.ClearAny(k); // the rows' own values from here
                            // each set to the other value first, so the row's apply runs whatever the player had
                            se.Value = true;
                            se.Value = false;
                            yield return null;
                            Check(speedRow.Visible != null && !speedRow.Visible(), "the speed dropdown is hidden while \"All mounts have the same speed\" is off");
                            ce.Value = false;
                            ce.Value = true;
                            se.Value = true;
                            be.Value = SpeedBand.Normal;
                            be.Value = SpeedBand.Fast;
                            yield return null;
                            Check(speedRow.Visible == null || speedRow.Visible(), "and shown with it on");
                            Check(Plugin.FreeplayMaxCargo.Value && Plugin.FreeplaySameSpeed.Value && Plugin.FreeplaySpeed.Value == SpeedBand.Fast && Mounts.Behaviour.OverriddenBy("FreeplaySpeed") == "dreas.gk2.saddleup",
                                $"the rows drive the Behaviour keys (max cargo {Plugin.FreeplayMaxCargo.Value}, same speed {Plugin.FreeplaySameSpeed.Value}, {Plugin.FreeplaySpeed.Value}, held by {Mounts.Behaviour.OverriddenBy("FreeplaySpeed")})");
                            Check(Catalog.CargoOf(k0) == Catalog.CargoMax && Mathf.Approximately(Catalog.SpeedOf(k0, MountStyle.Ride), Catalog.SpeedOfBand(SpeedBand.Fast, MountStyle.Ride)), "and the mounts follow them");
                            ce.Value = false;
                            se.Value = false;
                            yield return null;
                            Check(!Plugin.FreeplayMaxCargo.Value && !Plugin.FreeplaySameSpeed.Value && Catalog.CargoOf(k0) == 2 && Mathf.Approximately(Catalog.SpeedOf(k0, MountStyle.Ride), Catalog.SpeedOfBand(SpeedBand.VerySlow, MountStyle.Ride)), "both off: the beast's own cargo and speed again");
                        }
                        finally
                        {
                            ce.Value = ceWas;
                            se.Value = seWas;
                            be.Value = beWas;
                        }
                    }
                }
            }
            finally
            {
                foreach (BehaviourHeld h in held) h.Restore();
                reg?.Dispose();
            }
        }
    }
}
