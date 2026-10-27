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
        // Our mounts' speeds and cargo as their packs say them, and Saddle Up's speed rows retired (split from the API's
        // "speed" scenario on 2026-10-27, which keeps the words and a mount of its own).
        internal static IEnumerator ShippedSpeeds()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm", "gerry"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }

            // the built-in mounts each say their own (the shipped skins' [Mount] blocks)
            var said = new List<string>();
            bool allOwn = true;
            // the cargo against the speed (docs/next-update.md section 3): the faster, the lighter; the dragon pulls no cart
            var cargo = new Dictionary<string, int> { ["rat"] = 2, ["snail"] = 6, ["worm"] = 4, ["dragon"] = 0, ["gerry"] = 4 };
            // each names one of the API's words; the number is the API's (2026-10-27)
            var word = new Dictionary<string, SpeedBand> { ["rat"] = SpeedBand.VeryFast, ["snail"] = SpeedBand.VerySlow, ["worm"] = SpeedBand.Slow, ["dragon"] = SpeedBand.Slow, ["gerry"] = SpeedBand.Fast };
            foreach (string id in new[] { "rat", "snail", "worm", "dragon", "gerry" })
            {
                Catalog.Entry k = Catalog.Find(M(id));
                if (k == null) { said.Add(id + " not loaded"); continue; }
                allOwn &= k.Speed == word[id] && Mathf.Approximately(Catalog.SpeedOf(k, MountStyle.Ride), Catalog.SpeedOfBand(word[id], MountStyle.Ride)) && (k.Cart ? Catalog.CargoOf(k) : 0) == cargo[id];
                said.Add($"{id} {(k.Speed.HasValue ? Catalog.WordOf(k.Speed.Value) : "-")} {Catalog.SpeedOf(k, MountStyle.Ride):0.##}, cart {Catalog.SpeedOf(k, MountStyle.Cart):0.##}, cargo {Catalog.CargoOf(k)}");
            }
            Check(allOwn, "every built-in mount names its word and has its cargo (rat Very fast 2, snail Very slow 6, worm Slow 4, dragon Slow no cart, Gerry Fast 4), at the API's speed for it (" + string.Join(", ", said) + ")");
            Check(!Catalog.Donkey.Speed.HasValue && Catalog.CargoOf(Catalog.Donkey) == Catalog.CargoMax, "the donkey says none: it rides at the API cfg's speeds, as it always did");

            // Saddle Up: the two rows are gone, their lines retired (out of the file)
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null) Info("Saddle Up's page isn't shown: its speed rows not checked");
            else
            {
                bool rows = saddle.Rows.Exists(x => x.Entry != null && (x.Entry.Definition.Key == "RideSpeed" || x.Entry.Definition.Key == "CartSpeed"));
                Check(!rows && saddle.IsRetired("Behaviour", "RideSpeed") && saddle.IsRetired("7. Behaviour", "CartSpeed"), "Saddle Up has no Riding speed / Cart speed rows; both keys are retired (under any section number)");
                string text = "";
                try { text = System.IO.File.ReadAllText(saddle.Config.ConfigFilePath); } catch (Exception x) { Info("its cfg can't be read: " + x.Message); }
                Check(text.Length > 0 && text.IndexOf("\nRideSpeed =", StringComparison.Ordinal) < 0 && text.IndexOf("\nCartSpeed =", StringComparison.Ordinal) < 0, "and its cfg file holds no RideSpeed / CartSpeed line");
            }
            yield break;
        }
    }
}
