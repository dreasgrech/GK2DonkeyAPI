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
        // Saddle Up's babies rows in the Mounts panel's journal, the new one among them, each reaching the companion (from the
        // API's "foals: follow the keeper on foot" scenario, 2026-10-27).
        internal static IEnumerator SaddleUpBabiesRows()
        {
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null)
            {
                Info("Saddle Up's page isn't shown: its babies rows not checked");
                yield break;
            }
            var keys = new[] { "Foals", "FoalsFollow", "FoalsIndoors" }; // "FoalsWearSkin" retired 2026-10-27: babies always look like their parent
            var where = new List<string>();
            bool all = true;
            foreach (string k in keys)
            {
                MountSettingsPage.Row row = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == k);
                all &= row != null && row.ShownSection == "Mount" && row.IsLook; // the mount's own, in the Mounts panel's journal (2026-10-26)
                where.Add(k + " " + (row == null ? "missing" : row.ShownSection));
            }
            Check(all, $"Saddle Up's babies rows are the mount's, in the Mounts panel's journal ({string.Join(", ", where)})");
            Check(!saddle.Rows.Exists(x => x.Entry != null && x.Entry.Definition.Key.StartsWith("FoalsWearSkin", System.StringComparison.Ordinal)) && saddle.IsRetired("Mount", "FoalsWearSkin"),
                "no \"Babies are the same mount and skin\" row; its key is retired (the babies always look like their parent)");
            MountSettingsPage.Row follow = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "FoalsFollow");
            MountCompanion me = Mounts.Companion("dreas.gk2.saddleup");
            bool on = follow?.Entry is BepInEx.Configuration.ConfigEntry<bool> fe && fe.Value;
            Check(follow != null && me.OwnOf(MountCatalog.DonkeyId).FoalsFollowOnFoot == on, $"\"Babies follow you on foot\" reaches the donkey's own ({(on ? "on" : "off")})");
            Check(follow?.Entry != null && Equals(follow.Entry.DefaultValue, true), $"and it is on by default in Saddle Up ({follow?.Entry?.DefaultValue})");
            yield break;
        }

        // Saddle Up's own babies out in the world: each its own temperament (from the API's "foals: each baby its own size,
        // shade and temperament" scenario, 2026-10-27, which keeps the API's own checks).
        internal static IEnumerator SaddleUpFoalTempers()
        {
            MountCompanion me = Mounts.Companion("dreas.gk2.saddleup");
            var sizes = new List<string>();
            var mine = new HashSet<float>();
            var tempers = new HashSet<float>();
            for (int i = 0; me != null && i < me.Foals.Count; i++)
            {
                MountHandle f = me.Foals[i];
                if (f == null || !f.Exists) continue;
                sizes.Add($"{f.Scale:0.00} ({f.I.Temper.Energy:+0.00;-0.00})");
                mine.Add(f.Scale);
                tempers.Add(f.I.Temper.Energy);
            }
            if (sizes.Count >= 2)
            {
                Check(tempers.Count == sizes.Count, $"Saddle Up's babies: each its own temperament; sizes and energy {string.Join(", ", sizes)} ({mine.Count} different sizes; two can share one in 0.02 steps)");
            }
            else
            {
                Info($"Saddle Up has {sizes.Count} baby out: its own babies not compared");
            }

            yield break;
        }

        // Saddle Up's "Leave your mounts in the world" (the user, 2026-11-05; the API's MountCompanion.LeaveMountsInWorld,
        // whose own scenarios run its switching): an options row in the Mount section, off by default, reaching the
        // companion both ways (put back as it was). The player's own mounts are not switched here: they stay as he left them.
        internal static IEnumerator SaddleUpLeaveMountsRow()
        {
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null)
            {
                Info("Saddle Up's page isn't shown: its Leave your mounts row not checked");
                yield break;
            }
            MountSettingsPage.Row row = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "LeaveMounts");
            Check(row != null && row.ShownSection == "Mount" && !row.IsLook && saddle.InOptions(row), $"the row is in the options window's Mount section ({(row == null ? "missing" : row.ShownSection + (row.IsLook ? ", a look row" : ""))})");
            if (row?.Entry is ConfigEntry<bool> e)
            {
                MountCompanion me = Mounts.Companion("dreas.gk2.saddleup");
                Check(Equals(e.DefaultValue, false), $"off by default ({e.DefaultValue})");
                Check(me.LeaveMountsInWorld == e.Value, $"the companion has the row's value ({e.Value})");
                bool was = e.Value;
                MountSettingsPage.Row home = saddle.Rows.Find(x => x.RowKind == MountSettingsPage.RowKind.Button && x.Label == "Send your other mounts home");
                Check(home != null && home.ShownSection == "Mount" && saddle.InOptions(home), $"the button \"Send your other mounts home\" is in the Mount section ({home?.ShownSection ?? "missing"})");
                // the button shows only while the row is on (the user, 2026-11-05: off, no mount stays left)
                bool Shows() => home?.Visible != null && home.Visible();
                try
                {
                    e.Value = !was;
                    Check(me.LeaveMountsInWorld == !was, $"switched: the companion follows ({me.LeaveMountsInWorld})");
                    Check(home != null && Shows() == !was, $"and the button {(Shows() ? "shows" : "is hidden")} with the row {(!was ? "on" : "off")}");
                }
                finally
                {
                    e.Value = was;
                }
                Check(me.LeaveMountsInWorld == was, "put back");
                Check(home != null && Shows() == was, $"the button {(Shows() ? "shows" : "is hidden")} again with the row {(was ? "on" : "off")}");
                // (read as it is now: no mount of the player's is left or sent home to see it change; the API's own scenario
                // sends a test companion's left mounts home)
                Check(home?.Enabled != null && home.Enabled() == (me.LeftMounts.Count > 0), $"and its greying follows his mounts left in the world ({me.LeftMounts.Count} now, the button {(home?.Enabled != null && home.Enabled() ? "usable" : "greyed")})");
            }
            yield break;
        }
    }
}
