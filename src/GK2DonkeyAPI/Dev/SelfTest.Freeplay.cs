using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Freeplay (the user, 2026-10-27: a "cheats" section for players who don't want the mounts' differences): the
    // Behaviour keys FreeplayMaxCargo, FreeplaySameSpeed and FreeplaySpeed, the speed words' one table (Catalog.SpeedBands),
    // the cart's slots, the speeds ridden and driven, the panel's card, MountInfo, and Saddle Up's Freeplay rows.
    internal static partial class SelfTest
    {
        private static readonly string[] FreeplayKeys = { "FreeplayMaxCargo", "FreeplaySameSpeed", "FreeplaySpeed" };

        // A Behaviour key's override as it was before a test set its own (one slot per key), put back exactly.
        private readonly struct BehaviourHeld
        {
            public readonly string Key, Owner;
            public readonly object Value;
            public BehaviourHeld(string key) { Key = key; Owner = Mounts.Behaviour.OverriddenBy(key); Value = Owner == null ? null : Mounts.Behaviour.Get<object>(key); }
            public void Restore()
            {
                Mounts.Behaviour.ClearAny(Key);
                if (Owner != null) Mounts.Behaviour.Set(Owner, Key, Value);
            }
        }

        // The run holds Freeplay off (a player's Freeplay would change every cargo and speed the other scenarios check);
        // the Freeplay scenario sets its own and puts this back.
        private static BehaviourHeld[] HoldFreeplayOff()
        {
            var held = new BehaviourHeld[FreeplayKeys.Length];
            for (int i = 0; i < held.Length; i++) held[i] = new BehaviourHeld(FreeplayKeys[i]);
            Mounts.Behaviour.ClearAny("FreeplayMaxCargo");
            Mounts.Behaviour.ClearAny("FreeplaySameSpeed");
            Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", false);
            Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", false);
            if (held[0].Owner != null && (bool)held[0].Value || held[1].Owner != null && (bool)held[1].Value)
            {
                Info($"Freeplay is on for this session ({(held[0].Owner != null && (bool)held[0].Value ? "max cargo" : "")}{(held[1].Owner != null && (bool)held[1].Value ? " same speed" : "")}): held off for the run, back at the end");
            }
            return held;
        }

        private static IEnumerator Freeplay()
        {
            const string owner = Owner + ".freeplay";
            const string beast = "selftest.freeplay-beast", skin = beast + "/selftest.freeplay-test";

            // 1. One table: the words in SpeedBand's order, each band's speed read back as its own word, the bands rising
            IReadOnlyList<string> words = Mounts.Catalog.SpeedWords;
            Array bands = Enum.GetValues(typeof(SpeedBand));
            bool order = words.Count == bands.Length && words.Count == Catalog.SpeedBands.Length;
            for (int i = 0; order && i < words.Count; i++) order = words[i] == Catalog.WordOf((SpeedBand)bands.GetValue(i)) && Catalog.SpeedBands[i].Band == (SpeedBand)bands.GetValue(i);
            Check(order, $"the speed words, one per SpeedBand in its order ({string.Join(", ", words)})");
            Check(words.Count == 5 && words[0] == "Very slow" && words[2] == "Normal" && words[4] == "Very fast", "and they are the panel's: Very slow, Slow, Normal, Fast, Very fast");
            var wrong = new List<string>();
            float last = 0f;
            bool cartShipped = Mathf.Abs(Catalog.CartFactor - Catalog.DonkeyCartSpeed / Catalog.DonkeyRideSpeed) < 0.001f;
            foreach (SpeedBand b in bands)
            {
                float ride = Catalog.SpeedOfBand(b, MountStyle.Ride), cart = Catalog.SpeedOfBand(b, MountStyle.Cart);
                if (Catalog.SpeedWord(ride, MountStyle.Ride) != Catalog.WordOf(b)) wrong.Add($"{b} rides at {ride:0.##}, read as {Catalog.SpeedWord(ride, MountStyle.Ride)}");
                if (cartShipped && Catalog.SpeedWord(cart, MountStyle.Cart) != Catalog.WordOf(b)) wrong.Add($"{b} drives at {cart:0.##}, read as {Catalog.SpeedWord(cart, MountStyle.Cart)}");
                if (Mathf.Abs(cart - ride * Catalog.CartFactor) > 0.001f) wrong.Add($"{b}'s cart speed {cart:0.###} isn't {ride:0.##} x {Catalog.CartFactor:0.###}");
                if (ride <= last) wrong.Add($"{b} at {ride:0.##} doesn't rise from {last:0.##}");
                last = ride;
            }
            // the numbers are the shipped mounts' (the user, 2026-10-27: the speeds were already defined by them)
            string five = "";
            foreach (SpeedBand b in bands) five += (five.Length > 0 ? " " : "") + Catalog.SpeedOfBand(b, MountStyle.Ride).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            Check(five == "1.25 1.6 2 2.25 2.75", $"the five speeds are the snail's, the dragon's, the donkey's, Gerry's and the rat's ({five})");
            Check(Catalog.ParseBand("Very fast") == SpeedBand.VeryFast && Catalog.ParseBand(" very SLOW ") == SpeedBand.VerySlow && Catalog.ParseBand("VeryFast") == SpeedBand.VeryFast && Catalog.ParseBand("2.75") == null && Catalog.ParseBand("") == null,
                "a pack's word is read in any case, as the word or the name; a number is not a speed");
            if (!cartShipped) Info($"the cart-to-riding ratio is the player's {Catalog.CartFactor:0.###}, not the shipped 0.75: the cart words not compared");
            Check(wrong.Count == 0, "each band's speed reads as its own word, riding and with the cart, rising, the cart's times the ratio" + (wrong.Count > 0 ? ": " + string.Join("; ", wrong) : ""));
            Check(Mathf.Approximately(Catalog.SpeedOfBand(SpeedBand.Normal, MountStyle.Ride), Catalog.DonkeyRideSpeed), $"Normal is the donkey's riding speed as shipped ({Catalog.SpeedOfBand(SpeedBand.Normal, MountStyle.Ride)})");

            // a mount of the test's own, light and slow (cargo 2, Very slow: 1.25), a cart and a plain one
            IDisposable reg = null;
            string tempRoot = null;
            MountHandle plain = null, carted = null;
            BehaviourHeld[] held = new BehaviourHeld[FreeplayKeys.Length];
            for (int i = 0; i < held.Length; i++) held[i] = new BehaviourHeld(FreeplayKeys[i]);
            BehaviourHeld slots = new BehaviourHeld("CartSlots");
            try
            {
                reg = Mounts.Catalog.Register(owner, new MountDef { Id = beast, Name = "Freeplay Beast", Speed = SpeedBand.VerySlow, Cargo = 2 });
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-freeplay", "selftest.freeplay-test", "[Skin]\nformat = 1\nname = Freeplay Test\nmount = " + beast + "\n");
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
                foreach (string k in FreeplayKeys) Mounts.Behaviour.ClearAny(k);
                Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", false);
                Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", false);
                Mounts.Behaviour.Set(Owner, "FreeplaySpeed", SpeedBand.Normal);
                Mounts.Behaviour.ClearAny("CartSlots");
                Mounts.Behaviour.Set(Owner, "CartSlots", Catalog.CargoMax); // no mod's limit on the carts while this runs
                Catalog.Entry k0 = Catalog.Find(beast);
                Check(k0 != null && Catalog.CargoOf(k0) == 2 && Mathf.Approximately(Catalog.SpeedOf(k0, MountStyle.Ride), 1.25f), $"Freeplay off: the beast's own cargo 2 and speed 1.25 ({(k0 == null ? "not registered" : Catalog.CargoOf(k0) + ", " + Catalog.SpeedOf(k0, MountStyle.Ride))})");

                // 2. max cargo: every reader (the catalog, MountInfo, the panel's card and abilities) says the most
                Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", true);
                MountInfo info = Mounts.Catalog.Find(beast);
                MountsPanel.Entry entry = MountsPanel.Entries().Find(x => x.Id == beast);
                Check(Catalog.CargoOf(k0) == Catalog.CargoMax && Catalog.CargoOf(Catalog.Donkey) == Catalog.CargoMax && info != null && info.Cargo == Mounts.Catalog.MostCargo && Mounts.Catalog.MostCargo == Catalog.CargoMax,
                    $"FreeplayMaxCargo on: the beast's cargo is the most, {Catalog.CargoMax} (catalog {Catalog.CargoOf(k0)}, MountInfo {info?.Cargo}, MostCargo {Mounts.Catalog.MostCargo})");
                Check(entry != null && entry.Stats.Count >= 2 && entry.Stats[1] == ("Cargo", Catalog.CargoMax + " items") && entry.Abilities.Exists(a => a.Name == "Cargo cart" && a.Line.Contains(Catalog.CargoMax + " items")),
                    $"and the panel's card and its Cargo cart line ({(entry == null ? "no entry" : string.Join(", ", entry.Stats))})");
                Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", false);
                Check(Catalog.CargoOf(k0) == 2 && Mounts.Catalog.Find(beast)?.Cargo == 2, $"off again: its own 2 ({Catalog.CargoOf(k0)})");

                // 3. the one speed: every mount, the donkey too, at the band's
                Mounts.Behaviour.Set(Owner, "FreeplaySpeed", SpeedBand.Slow);
                Check(Mathf.Approximately(Catalog.SpeedOf(k0, MountStyle.Ride), 1.25f), "FreeplaySpeed alone changes nothing (the switch is FreeplaySameSpeed)");
                Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", true);
                float slow = Catalog.SpeedOfBand(SpeedBand.Slow, MountStyle.Ride), slowCart = Catalog.SpeedOfBand(SpeedBand.Slow, MountStyle.Cart);
                var off = new List<string>();
                foreach (Catalog.Entry k in Catalog.All)
                {
                    if (Mathf.Abs(Catalog.SpeedOf(k, MountStyle.Ride) - slow) > 0.001f || Mathf.Abs(Catalog.SpeedOf(k, MountStyle.Cart) - slowCart) > 0.001f) off.Add($"{k.Id} {Catalog.SpeedOf(k, MountStyle.Ride):0.##}/{Catalog.SpeedOf(k, MountStyle.Cart):0.##}");
                }
                Check(off.Count == 0, $"FreeplaySameSpeed at Slow: every mount, the donkey too, at {slow:0.##} riding and {slowCart:0.###} with the cart" + (off.Count > 0 ? ": not " + string.Join(", ", off) : ""));
                info = Mounts.Catalog.Find(beast);
                entry = MountsPanel.Entries().Find(x => x.Id == beast);
                MountsPanel.Entry donkeyEntry = MountsPanel.Entries().Find(x => x.Id == MountCatalog.DonkeyId);
                Check(info != null && Mathf.Approximately(info.Speed, slow) && info.SpeedWord == "Slow" && entry != null && entry.Stats[0] == ("Speed", "Slow") && donkeyEntry != null && donkeyEntry.Stats[0] == ("Speed", "Slow"),
                    $"MountInfo and the panel's cards say Slow (the beast {info?.SpeedWord} {info?.Speed:0.##}, its card {entry?.Stats[0].Value}, the donkey's {donkeyEntry?.Stats[0].Value})");

                // ridden and driven, and changed while ridden
                Vector3 near = Spot(home, 3f) ?? home;
                plain = Spawn(near, right: homeFacing > 0f);
                carted = Spawn(near + new Vector3(0f, 0f, 3f), right: homeFacing > 0f, withCart: true);
                if (plain == null || carted == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => plain.IsReady && carted.IsReady && carted.I.CargoRig.Inv != null, 6f);
                PlayerController pc = MainGame.PlayerController;

                MountResult r = plain.Ride(showMessage: false);
                Check(r.Ok, $"the donkey ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slow) < 0.01f, 3f);
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slow) < 0.01f, $"the donkey rides at Slow's {slow:0.##} ({pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Behaviour.Set(Owner, "FreeplaySpeed", SpeedBand.VeryFast);
                    float vf = Catalog.SpeedOfBand(SpeedBand.VeryFast, MountStyle.Ride);
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - vf) < 0.01f, 3f);
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - vf) < 0.01f, $"Very fast chosen while riding: {vf:0.##} at once ({pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", false);
                    ITuned rideTuned = Plugin.For(MountStyle.Ride).SpeedMultiplier;
                    float cfgRide = (float)rideTuned.Current;
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - cfgRide) < 0.01f, 3f);
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - cfgRide) < 0.01f && !plain.Tuning.IsSet("SpeedMultiplier"), $"switched off while riding: the donkey's own {cfgRide:0.##} again, nothing left on it ({pc.PhysicalBody.SpeedMultiplier:0.##}, {plain.Tuning.SetBy("SpeedMultiplier") ?? "no value set"})");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }

                Mounts.Behaviour.Set(Owner, "FreeplaySpeed", SpeedBand.Slow);
                Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", true);
                plain.Tuning.Set(Owner, "Skin", skin);
                carted.Tuning.Set(Owner, "Skin", skin);
                yield return Until(() => plain.Mount == beast && carted.Mount == beast && carted.I.CargoRig.Inv != null, 8f);
                int Size() => carted.I.CargoRig.Inv != null ? carted.I.CargoRig.Inv.Data.InventorySize : -1;
                yield return Until(() => Size() == 2, 3f);
                Check(Size() == 2, $"the beast's cart: its own 2 slots with max cargo off ({Size()})");
                Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", true);
                yield return Until(() => Size() == Catalog.CargoMax, 3f);
                Check(Size() == Catalog.CargoMax, $"max cargo on: the hitched cart takes {Catalog.CargoMax} at once ({Size()})");
                Mounts.Behaviour.Set(Owner, "FreeplayMaxCargo", false);
                yield return Until(() => Size() == 2, 3f);
                Check(Size() == 2, $"and off: 2 again, empty as it is ({Size()})");

                r = plain.Ride(showMessage: false);
                Check(r.Ok, $"the beast ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slow) < 0.01f, 3f);
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slow) < 0.01f, $"the beast (its own 1.25) rides at Slow's {slow:0.##} ({pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", false);
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - 1.25f) < 0.01f, 3f);
                    Check(Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - 1.25f) < 0.01f, $"switched off while riding: its own 1.25 ({pc.PhysicalBody.SpeedMultiplier:0.##})");
                    Mounts.Behaviour.Set(Owner, "FreeplaySameSpeed", true);
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                r = carted.Ride(showMessage: false);
                Check(r.Ok, $"the beast with its cart driven ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slowCart) < 0.01f, 3f);
                    Check(Mounts.Style == MountStyle.Cart && Mathf.Abs(pc.PhysicalBody.SpeedMultiplier - slowCart) < 0.01f, $"driving at Slow's {slowCart:0.###} (times the ratio) ({Mounts.Style}, {pc.PhysicalBody.SpeedMultiplier:0.###})");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                Check(Mathf.Approximately(pc.PhysicalBody.SpeedMultiplier, 1f), $"off the mount: the keeper walks at 1 ({pc.PhysicalBody.SpeedMultiplier:0.##})");

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
                            Check(!Plugin.FreeplayMaxCargo.Value && !Plugin.FreeplaySameSpeed.Value && Catalog.CargoOf(k0) == 2 && Mathf.Approximately(Catalog.SpeedOf(k0, MountStyle.Ride), 1.25f), "both off: the beast's own cargo and speed again");
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
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                foreach (BehaviourHeld h in held) h.Restore();
                slots.Restore();
                try { if (plain != null && plain.Exists) plain.Remove(); } catch { }
                try { if (carted != null && carted.Exists) carted.Remove(); } catch { }
                Looks.Unregister(skin, owner);
                reg?.Dispose();
                try { if (tempRoot != null) System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}
