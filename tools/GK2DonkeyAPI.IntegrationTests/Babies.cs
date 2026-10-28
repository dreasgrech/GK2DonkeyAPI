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
    }
}
