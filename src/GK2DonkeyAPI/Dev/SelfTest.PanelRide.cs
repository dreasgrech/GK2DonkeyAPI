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
        private static IEnumerator PanelRideButtons()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            // the keys while the panel is open: only the panel's own key works there (the user, 2026-10-22: H in the panel got him on and off)
            bool? ride = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "RideKey"), cart = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "CartKey"), panelKey = KeyActions.WorksOverPanel("dreas.gk2.saddleup", "MountsKey");
            if (ride == null) Info("Saddle Up's keys aren't polled: the over-the-panel check skipped");
            else Check(ride == false && cart == false && panelKey == true, $"with the panel open the ride and cart keys wait, the panel key works (ride {ride}, cart {cart}, panel {panelKey})");
            MountCompanion c = Mounts.Companion(Owner + ".panelride");
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
    }
}
