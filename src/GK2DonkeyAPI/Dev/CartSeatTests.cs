using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI
{
    // The cart key while riding where the hitch would seat the keeper on ground he can't stand on (a wall, a fence or a
    // building beside him): refused before the cart moves, so it stays where it stands (2026-11-12: rehearsal A of 0.17.0,
    // just outside a door, the cart came next to him and stayed unhitched; the user: only refuse, never turn him round).
    // The run's own companion; the blocked ground is Mount.Instance.TestHitchSeatBlocked (the hitch seat only, no other
    // check). Every mount of the test goes and the switch is cleared, whatever happened.
    internal static class CartSeatTests
    {
        internal static IEnumerator SeatBlocked()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            MountCompanion c = Mounts.Companion(Owner);
            try
            {
                c.SendAwayAll();
                yield return Frames(1);
                c.TestFamiliesFree = true; // the run holds every companion's mount in place (MountCompanion.HeldForTests): this one's are the test's

                // a cart standing by the ridden donkey: driven, then ridden without it
                MountResult r = c.Use(MountStyle.Cart);
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart && c.Donkey != null && c.Donkey.HasCart, 12f);
                Check(r.Ok && waited, $"the donkey drives the cart ({Said(r)})");
                if (!waited) yield break;
                r = c.Use(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && Mounts.Riding != null && !Mounts.Riding.HasCart && c.Cart != null && c.Cart.Exists && c.Cart.IsReady, 12f);
                Check(r.Ok && waited, $"ridden without it, the cart left standing ({Said(r)})");
                if (!waited) yield break;
                yield return Frames(2);

                // blocked: refused with the reason, the cart where it stands, not hitched
                MountHandle cart = c.Cart;
                Vector3 at = cart.Position;
                Mount.Instance.TestHitchSeatBlocked = true;
                r = c.Use(MountStyle.Cart);
                Mount.Instance.TestHitchSeatBlocked = false;
                yield return Frames(3);
                string noRoom = UiWords.SayNoRoomHere.English;
                Check(!r.Ok && r.Reason == noRoom, $"the cart key refused where the seat is blocked: \"{noRoom}\" ({Said(r)})");
                Check(cart.Exists && c.Cart == cart && Vector3.Distance(cart.Position, at) < 0.01f,
                    $"the cart stays where it stands ({(cart.Exists ? cart.Position.ToString("F2") : "gone")}, was {at:F2})");
                Check(Mounts.Riding != null && !Mounts.Riding.HasCart && Mounts.Style == MountStyle.Ride, "the keeper still rides the donkey, no cart hitched");

                // no cart yet (the first cart key): the one spawned for it goes again
                cart.Remove(Owner);
                yield return Frames(2);
                Mount.Instance.TestHitchSeatBlocked = true;
                r = c.Use(MountStyle.Cart);
                Mount.Instance.TestHitchSeatBlocked = false;
                yield return Frames(3);
                int freeCarts = 0;
                foreach (MountHandle h in Mounts.InstancesOf(Owner))
                {
                    if (h.HasCart && !h.HasDonkey) freeCarts++;
                }
                Check(!r.Ok && r.Reason == noRoom && (c.Cart == null || !c.Cart.Exists) && freeCarts == 0,
                    $"with no cart yet: refused, and no cart left behind ({Said(r)}, {freeCarts} free cart(s))");

                // not blocked: the cart key brings and hitches it as always
                r = c.Use(MountStyle.Cart);
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart && Mounts.Riding != null && Mounts.Riding.HasCart, 12f);
                Check(r.Ok && waited, $"where the seat is free, the cart comes and is hitched ({Said(r)})");
            }
            finally
            {
                Mount.Instance.TestHitchSeatBlocked = false;
                try { if (Mounts.IsMounted) Mounts.Dismount("self-test"); } catch { }
                try { c.SendAwayAll(); } catch (System.Exception e) { Warn("the test's mounts could not all be sent away: " + e.Message); }
                c.TestFamiliesFree = false;
            }
            yield return Frames(3);
            Check(Mounts.InstancesOf(Owner).Count == 0, $"every mount of the test went ({Mounts.InstancesOf(Owner).Count} left)");
        }
    }
}
