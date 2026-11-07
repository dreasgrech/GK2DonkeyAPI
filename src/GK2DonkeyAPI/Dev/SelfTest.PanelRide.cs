using System;
using System.Collections;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The Mounts panel's Ride and Drive the cart buttons while riding (the user, 2026-10-22: pressed while on his mount,
    // they got him off). Through a test companion (MountCompanion.UseFromPanel, what the buttons call): on its own mount
    // the same style keeps the ride, the other style switches it (the cart brought, then left); on someone else's
    // donkey the companion's mount comes and is ridden.
    internal static partial class SelfTest
    {
        internal static IEnumerator PanelRideButtons()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            MountCompanion c = Mounts.Companion(Owner); // the run's own GUID: a made-up one is warned about as a typo at every spawn
            c.KeepWithSaves = false;
            c.FoalCount = 0;
            MountHandle other = null;
            try
            {
                MountResult r = c.Use(MountStyle.Ride);
                Check(r.Ok, $"the companion's mount called to ride ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Until(() => Mounts.IsMounted && Mounts.Riding != null && c.Donkey != null && Mounts.Riding.I == c.Donkey.I, 12f);
                Check(waited, "riding the companion's mount");
                if (!waited) yield break;
                MountHandle mine = c.Donkey;

                r = c.UseFromPanel(MountStyle.Ride);
                yield return Seconds(0.5f);
                Check(r.Ok && Mounts.IsMounted && Mounts.Style == MountStyle.Ride && Mounts.Riding?.I == mine.I, $"Ride pressed while riding it: still riding it ({Said(r)}, mounted {Mounts.IsMounted})");

                r = c.UseFromPanel(MountStyle.Cart);
                Check(r.Ok, $"Drive the cart pressed while riding: the cart brought ({Said(r)})");
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart, 12f);
                Check(waited, $"driving its cart (style {Mounts.Style}, mounted {Mounts.IsMounted})");
                if (waited)
                {
                    r = c.UseFromPanel(MountStyle.Cart);
                    yield return Seconds(0.5f);
                    Check(r.Ok && Mounts.IsMounted && Mounts.Style == MountStyle.Cart, $"Drive the cart pressed while driving: still driving ({Said(r)}, mounted {Mounts.IsMounted})");
                    r = c.UseFromPanel(MountStyle.Ride);
                    yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                    Check(r.Ok && waited && Mounts.Riding?.I == mine.I, $"Ride pressed while driving: the cart left standing, riding on ({Said(r)}, style {Mounts.Style})");
                }

                // on someone else's donkey: the companion's mount comes
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
                other = Spawn(Spot(Keeper(), 2f) ?? Keeper(), right: homeFacing > 0f);
                yield return Until(() => other != null && other.IsReady, 6f);
                r = other.Ride(showMessage: false);
                Check(r.Ok, $"another donkey ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => Mounts.IsMounted, 5f);
                    r = c.UseFromPanel(MountStyle.Ride);
                    Check(r.Ok, $"Ride pressed on it ({Said(r)})");
                    yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == mine.I, 12f);
                    Check(waited, $"riding the companion's mount instead (riding {Mounts.Riding?.FullId ?? "nothing"})");
                }
            }
            finally
            {
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (other != null && other.Exists) other.Remove(); } catch { }
                try { c.SendAway(); } catch { }
            }
        }

        // The ride and cart keys in the journal (the user, 2026-11-07): a companion's Ride and Drive given as they are
        // are journal keys, a lambda round Use is not; every shown page's journal keys are its own companion's. On a test
        // page over the test companion: a key that isn't the page's, or pressed with the colours window on top, does
        // nothing; the ride key closes the journal and the mount picked comes and is ridden; the cart key in the journal
        // while riding brings the cart. (The key press itself is the poll's: CallFromKey is what it calls.)
        internal static IEnumerator PanelRideKeys()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            MountCompanion c = Mounts.Companion(Owner);
            c.KeepWithSaves = false;
            c.FoalCount = 0;
            MountStyle? s = KeyActions.JournalStyleOf(c.Ride, out MountCompanion of);
            Check(s == MountStyle.Ride && ReferenceEquals(of, c), $"a companion's Ride given as it is: the journal's Ride ({s?.ToString() ?? "none"})");
            s = KeyActions.JournalStyleOf(c.Drive, out of);
            Check(s == MountStyle.Cart && ReferenceEquals(of, c), $"its Drive: the journal's Drive the cart ({s?.ToString() ?? "none"})");
            MountStyle? lambda = KeyActions.JournalStyleOf(() => c.Use(MountStyle.Ride), out _);
            MountStyle? away = KeyActions.JournalStyleOf(c.SendAway, out _);
            Check(lambda == null && away == null, $"a lambda round Use and SendAway are no journal keys: they wait in the journal ({lambda?.ToString() ?? "none"}, {away?.ToString() ?? "none"})");
            foreach (MountSettingsPage live in MountSettingsPage.All)
            {
                if (!live.Shown || !live.HasMountRow) continue;
                var keys = KeyActions.TestJournalKeys(live.OwnerId);
                bool own = true;
                string list = "";
                foreach (var k in keys)
                {
                    if (!live.IsCompanion(k.Of)) own = false;
                    list += (list.Length > 0 ? ", " : "") + k.Key + " (" + k.Style + ")";
                }
                Check(own, $"{live.Title}: its journal keys are its own companion's ({(list.Length > 0 ? list : "none")})");
            }

            string cfgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-journalkeys.cfg");
            try { System.IO.File.Delete(cfgPath); } catch { }
            try
            {
                if (Mounts.IsMounted)
                {
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                var cfg = new BepInEx.Configuration.ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage(Owner + ".journalkeys", cfg, "Journal keys test", "test", "", null);
                page.MountRows("Mount", c);
                MountsPanel.Open(page, toggle: false);
                yield return Frames(2);
                Check(MountsPanel.IsOpen, "the journal opens on a test page with mount rows");
                if (!MountsPanel.IsOpen) yield break;

                bool took = MountsPanel.CallFromKey(null, MountStyle.Ride);
                Check(!took && MountsPanel.IsOpen && !Mounts.IsMounted, "a ride key that isn't the page's companion's does nothing: the journal stays");
                MountsPanel.TestOpenColours();
                yield return Frames(2);
                took = MountsPanel.CallFromKey(c, MountStyle.Ride);
                Check(MountsPanel.IsColoursOpen && !took && !Mounts.IsMounted, $"with the colours window on top the ride key waits (colours open {MountsPanel.IsColoursOpen})");
                MountsPanel.TestCloseColours(keep: true);
                yield return Frames(2);

                took = MountsPanel.CallFromKey(c, MountStyle.Ride);
                yield return Frames(2);
                Check(took && !MountsPanel.IsOpen, $"the ride key in the journal: it closes (taken {took}, open {MountsPanel.IsOpen})");
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && Mounts.Style == MountStyle.Ride, 12f);
                Check(waited, $"and the mount picked comes and is ridden (riding {Mounts.Riding?.FullId ?? "nothing"}, style {Mounts.Style})");
                if (!waited) yield break;

                MountsPanel.Open(page, toggle: false);
                yield return Frames(2);
                if (!MountsPanel.TestDriveShown)
                {
                    Info("the mount picked pulls no cart: the cart key's part skipped");
                    took = MountsPanel.CallFromKey(c, MountStyle.Cart);
                    Check(!took && MountsPanel.IsOpen, "with no Drive button the cart key does nothing");
                    yield break;
                }
                took = MountsPanel.CallFromKey(c, MountStyle.Cart);
                Check(took && !MountsPanel.IsOpen, $"the cart key in the journal while riding: it closes (taken {took})");
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart, 12f);
                Check(waited, $"and the cart is brought: driving (style {Mounts.Style}, mounted {Mounts.IsMounted})");
            }
            finally
            {
                try { if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: false); } catch { }
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { c.SendAway(); } catch { }
                try { System.IO.File.Delete(cfgPath); } catch { }
            }
        }
    }
}
