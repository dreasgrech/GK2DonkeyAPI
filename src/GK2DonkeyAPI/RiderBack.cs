using UnityEngine;

namespace GK2DonkeyAPI
{
    // The armoured rider seen from behind (facing up on the side-on donkey, docs/mounted-combat.md §10.5). A rider who
    // turns his back to the camera on a sideways donkey sits side-saddle with both legs on the far flank: the donkey
    // hides them, and his seat rests on the back line in front of it. So: the leg renderer is switched off, and the seat
    // is lifted by Ride.BackSeatExtraPx (Mount.TickSeat) so the armour skirt's own hem meets the back line instead of
    // hanging ten rows down the flank. Tried and dropped the same day: cropping the body at the back line (a ruler-
    // straight edge across the hips), and pushing the body behind the donkey along the view ray (he looked stuck behind
    // it, the whole flank in front of him). Every frame after the animator and the skin changer (OnKeeperLateUpdate):
    // the skin changer rewrites the leg's enabled flag each LateUpdate, so it is switched off again each frame, and left
    // alone as soon as the facing is not up; at dismount it is given back explicitly.
    internal static class RiderBack
    {
        private static Transform root;
        private static SpriteRenderer leg;
        private static bool legWasEnabled;
        private static bool legTouched;
        private static bool loggedOnce;

        internal static bool Active { get; private set; }

        // show: the keeper is drawn facing up, mounted, in armour.
        internal static void Update(Transform seatRoot, bool show)
        {
            if (!show || seatRoot == null || !seatRoot)
            {
                if (Active)
                {
                    Restore();
                }
                return;
            }
            if (root != seatRoot)
            {
                Detach();
                root = seatRoot;
                Transform l = seatRoot.Find("gfx/leg");
                leg = l != null ? l.GetComponent<SpriteRenderer>() : null;
            }
            Active = true;
            if (leg != null && leg)
            {
                if (!legTouched)
                {
                    legTouched = true;
                    legWasEnabled = leg.enabled;
                }
                if (leg.enabled)
                {
                    leg.enabled = false;
                }
            }
            if (!loggedOnce)
            {
                loggedOnce = true;
                Plugin.Log.LogInfo($"Mount: rider from behind: legs hidden, seat lifted {Mount.S.BackSeatExtraPx.Value} px extra (Ride.BackSeatExtraPx); leg renderer {(leg != null ? "found" : "not found")}.");
            }
        }

        // The legs back on. Called when the facing leaves "up" and at dismount.
        internal static void Restore()
        {
            if (legTouched && leg != null && leg && leg.enabled != legWasEnabled)
            {
                leg.enabled = legWasEnabled;
            }
            legTouched = false;
            Active = false;
        }

        // At dismount: restored, and the lookups dropped (the next ride may have another view).
        internal static void Detach()
        {
            Restore();
            root = null;
            leg = null;
        }
    }
}
