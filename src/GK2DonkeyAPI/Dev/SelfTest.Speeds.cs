using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Each mount's own speed (the user, 2026-10-22; docs/skins.md "Speeds"): MountDef.Speed and a pack's [Mount] speed,
    // times the keeper's walking speed, with the cart times the donkey's cart-to-riding ratio; its cargo stat. Riding and
    // driving at the mount's values, the donkey at the API cfg's, the panel's card, Saddle Up's retired speed rows.
    internal static partial class SelfTest
    {
        private static IEnumerator MountSpeeds()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): the API keeps mounts out");
                yield break;
            }
            const string owner = Owner + ".speeds";

            // the words, at the band edges (against the donkey's 2 riding, 1.5 with the cart)
            var edges = new (float V, MountStyle S, string W)[]
            {
                (1.38f, MountStyle.Ride, "Very slow"), (1.4f, MountStyle.Ride, "Slow"), (1.8f, MountStyle.Ride, "Normal"), (2f, MountStyle.Ride, "Normal"),
                (2.2f, MountStyle.Ride, "Normal"), (2.25f, MountStyle.Ride, "Fast"), (2.7f, MountStyle.Ride, "Fast"), (2.75f, MountStyle.Ride, "Very fast"),
                (1.5f, MountStyle.Cart, "Normal"), (1f, MountStyle.Cart, "Very slow"), (1.25f, MountStyle.Cart, "Slow"), (1.75f, MountStyle.Cart, "Fast"), (2f, MountStyle.Cart, "Fast"),
            };
            var wrong = new List<string>();
            foreach (var e in edges) if (Catalog.SpeedWord(e.V, e.S) != e.W) wrong.Add($"{e.V} {e.S}: {Catalog.SpeedWord(e.V, e.S)}, not {e.W}");
            Check(wrong.Count == 0, "the panel's words at the band edges" + (wrong.Count > 0 ? ": " + string.Join("; ", wrong) : ""));
            Check(Catalog.SpeedWord(Catalog.DonkeyRideSpeed, MountStyle.Ride) == "Normal" && Catalog.SpeedWord(Catalog.DonkeyCartSpeed, MountStyle.Cart) == "Normal", "the donkey reads Normal both ways");

            // the built-in mounts each say their own (the shipped skins' [Mount] blocks)
            var said = new List<string>();
            bool allOwn = true;
            foreach (string id in new[] { "rat", "snail", "worm", "dragon", "gerry" })
            {
                Catalog.Entry k = Catalog.Find(id);
                if (k == null) { said.Add(id + " not loaded"); continue; }
                allOwn &= k.Speed.HasValue && Catalog.CargoOf(k) == Catalog.CargoMax;
                said.Add($"{id} {k.Speed?.ToString("0.##") ?? "-"} {Catalog.SpeedWord(Catalog.SpeedOf(k, MountStyle.Ride), MountStyle.Ride)}, cart {Catalog.SpeedOf(k, MountStyle.Cart):0.##}, cargo {Catalog.CargoOf(k)}");
            }
            Check(allOwn, "every built-in mount says its own speed and carries the most (for now) (" + string.Join(", ", said) + ")");
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

            // a mount of its own: ridden at its speed, driven at its cart speed; the donkey at the cfg's
            IDisposable reg = null;
            string tempRoot = null;
            MountHandle plain = null, carted = null;
            Info("the next \"is the API's own line\" warning is this test's own: a mount's Speed stat line is left out");
            try
            {
                reg = Mounts.Catalog.Register(owner, new MountDef { Id = "speed-beast", Name = "Speed Beast", Speed = 2.5f, Cargo = 4,
                    TuningRide = new Dictionary<string, object> { ["SpeedMultiplier"] = 1.1f }, Stats = { new MountStat { Label = "Speed", Value = "a made-up line" }, new MountStat { Label = "Diet", Value = "keepers" } } });
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-speed", "speed-test", "[Skin]\nformat = 1\nid = speed-test\nname = Speed Test\nmount = speed-beast\n");
                Looks.RegisterFolder(tempRoot, owner);
            }
            catch (Exception e)
            {
                Fail("the mount or the temp pack could not be made: " + e.Message);
                reg?.Dispose();
                yield break;
            }
            try
            {
                MountInfo info = Mounts.Catalog.Find("speed-beast");
                float wantCart = 2.5f * Catalog.CartFactor;
                Check(info != null && Mathf.Approximately(info.Speed, 2.5f) && Mathf.Abs(info.CartSpeed - wantCart) < 0.001f && info.SpeedWord == "Fast" && info.Cargo == 4, $"MountInfo: {info?.Speed}, with the cart {info?.CartSpeed:0.###} (2.5 x {Catalog.CartFactor:0.###}), \"{info?.SpeedWord}\", cargo {info?.Cargo}");
                Check(info != null && info.Stats.Count == 1 && info.Stats[0].Label == "Diet", $"its own \"Speed\" stat line is left out (the API writes that one): {info?.Stats.Count} line(s)");
                MountsPanel.Entry entry = MountsPanel.Entries().Find(x => x.Id == "speed-beast");
                Check(entry != null && entry.Stats.Count >= 2 && entry.Stats[0] == ("Speed", "Fast") && entry.Stats[1] == ("Cargo", "4 items"), $"the panel's stats card: {(entry == null ? "no entry" : string.Join(", ", entry.Stats))}");
                Check(entry != null && entry.Lines.Contains("Speed: Fast (2.5x walking speed)") && entry.Lines.Contains("Cargo: 4 items"), "and the entry's lines carry the number (for the API; the card shows the word)");
                MountsPanel.Entry donkeyEntry = MountsPanel.Entries().Find(x => x.Id == MountCatalog.DonkeyId);
                float cfgRide = Plugin.For(MountStyle.Ride).SpeedMultiplier.Entry.Value, cfgCart = Plugin.For(MountStyle.Cart).SpeedMultiplier.Entry.Value;
                Check(donkeyEntry != null && donkeyEntry.Stats[0].Value == Catalog.SpeedWord(cfgRide, MountStyle.Ride) && donkeyEntry.Stats[1].Value == Catalog.CargoMax + " items", $"the donkey's card reads the cfg's speed and the most cargo ({cfgRide}: {donkeyEntry?.Stats[0].Value}, {donkeyEntry?.Stats[1].Value})");

                Vector3 near = Spot(home, 3f) ?? home;
                plain = Spawn(near, right: homeFacing > 0f);
                carted = Spawn(near + new Vector3(0f, 0f, 3f), right: homeFacing > 0f, withCart: true);
                if (plain == null || carted == null)
                {
                    Fail("could not spawn");
                    yield break;
                }
                yield return Until(() => plain.IsReady && carted.IsReady, 6f);
                PlayerController pc = MainGame.PlayerController;

                // the donkey first: the cfg's speed (unless a mod holds a session value)
                MountResult r = plain.Ride(showMessage: false);
                Check(r.Ok, $"the donkey ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Frames(3);
                    ITuned rideTuned = Plugin.For(MountStyle.Ride).SpeedMultiplier;
                    if (rideTuned.HasOverride) Info($"the ride's speed is held by {rideTuned.OverrideOwner} this session: the donkey's check reads its value");
                    float want = rideTuned.HasOverride ? (float)rideTuned.Current : cfgRide;
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - want) < 0.01f, $"the donkey rides at the cfg's {want} ({pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }

                plain.Tuning.Set(Owner, "Skin", "speed-test");
                carted.Tuning.Set(Owner, "Skin", "speed-test");
                yield return Until(() => plain.Mount == "speed-beast" && carted.Mount == "speed-beast" && plain.Tuning.IsSet("SpeedMultiplier") && carted.Tuning.IsSet("SpeedMultiplier"), 8f);
                Check(waited && plain.Tuning.SetBy("SpeedMultiplier") == Looks.TuningOwner, $"worn: the mount's speed is on the wearer, owned by the skin system ({plain.Tuning.SetBy("SpeedMultiplier")})");
                Check(Math.Abs(plain.Tuning.Get<float>("SpeedMultiplier") - 2.5f) < 0.001f, $"Speed wins over a SpeedMultiplier in its tuning ({plain.Tuning.Get<float>("SpeedMultiplier")}, not 1.1)");

                r = plain.Ride(showMessage: false);
                Check(r.Ok, $"the speed beast ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Frames(3);
                    Check(Mounts.Style == MountStyle.Ride && Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - 2.5f) < 0.01f, $"riding at its own 2.5 ({Mounts.Style}, {pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                r = carted.Ride(showMessage: false);
                Check(r.Ok, $"the speed beast with its cart driven ({Said(r)})");
                if (r.Ok)
                {
                    yield return Frames(3);
                    Check(Mounts.Style == MountStyle.Cart && Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - wantCart) < 0.01f, $"driving at its speed times the donkey's ratio, {wantCart:0.###} ({Mounts.Style}, {pc.PhysicalBody.SpeedMultiplier:0.###})");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                Check(Mathf.Approximately(pc.PhysicalBody.SpeedMultiplier, 1f), $"off again: the keeper walks at 1 ({pc.PhysicalBody.SpeedMultiplier:0.##})");
            }
            finally
            {
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (plain != null && plain.Exists) plain.Remove(); } catch { }
                try { if (carted != null && carted.Exists) carted.Remove(); } catch { }
                Looks.Unregister("speed-test", owner);
                reg?.Dispose();
                try { if (tempRoot != null) System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}
