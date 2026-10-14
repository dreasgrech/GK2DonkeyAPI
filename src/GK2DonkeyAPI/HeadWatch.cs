using System;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The still head over the walking body (the "double donkey", docs/pooled-parts.md).
    //
    // The walk clips don't draw the head layer ("hed": the walking body carries its own head), so during a walk that
    // layer shows its fallback: the sprite it held when the animator started running (write defaults). Measured
    // 2026-10-03 with Animator.WriteDefaultValues ([defaults] lines):
    //   - the game starts a new view's animator after its skin swap has put a copy of the still head there: every view
    //     the game hands over falls back to 0301_hed_static_down(Clone);
    //   - Animator.Rebind and switching the Animator component off and on leave that fallback unchanged;
    //   - switching the animator's object off and on with the head layer empty sets it to none, and the walk is clean.
    // So the head layer is emptied wherever a donkey's animator starts: once per view in PrepareParts
    // (RestartWithoutHead), and before an idle view is switched back on (Tiers.cs). HeadWatch only checks.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            // The head layer ("hed") emptied, for an animator about to start.
            private static void EmptyHeadLayer(Wgo v)
            {
                foreach (SpriteRenderer sr in v.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
                {
                    if (sr.name == "hed")
                    {
                        sr.sprite = null;
                    }
                }
            }

            // PrepareParts, first thing: the animator started again with the head layer empty, by switching its object off
            // and on (nothing less resets the fallback, see above). The view is hidden until PrepareParts is done, its
            // cart parts are set after this, and the pose and facing are sent again.
            private void RestartWithoutHead()
            {
                try
                {
                    Animator a = donkey.MainWgoPart?.AnimationComponent?.Animator;
                    if (a == null)
                    {
                        return;
                    }
                    EmptyHeadLayer(donkey);
                    a.gameObject.SetActive(false);
                    a.gameObject.SetActive(true);
                    lastMirrored = (AnimationState)(-1);
                    lastWalkMultiplier = 0f;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Mount: {Describe()}: starting the animator again without a head failed: {e.Message}");
                }
            }

            // Where the current view came from (for the [heads] line).
            internal string viewOrigin = "none";

            private SpriteRenderer headRenderer;
            private Wgo headRendererOf;
            private int headHits;
            private int nextHeadCheck;
            private static int headsLogged;
            private Wgo headLoggedFor;

            // The base layer's states (short name hashes), sorted into walks and others by their clip's name the first time
            // each is seen (GetCurrentAnimatorClipInfo allocates; the hash doesn't).
            private static readonly System.Collections.Generic.HashSet<int> WalkStates = new System.Collections.Generic.HashSet<int>();
            private static readonly System.Collections.Generic.HashSet<int> OtherStates = new System.Collections.Generic.HashSet<int>();

            // The tripwire, a few times a second for every view (MountRegistry.Tick): a head sprite during a walk means an
            // animator started with a head on that layer somewhere the fix doesn't reach. Logged once per view, never put
            // right here (a hidden fix would hide the next cause too).
            internal void HeadWatch()
            {
                if (Time.frameCount < nextHeadCheck)
                {
                    return;
                }
                nextHeadCheck = Time.frameCount + 6;
                if (donkey == null || !donkey || !partsReady || !HasDonkey)
                {
                    headHits = 0;
                    return;
                }
                Animator a = donkey.MainWgoPart?.AnimationComponent?.Animator;
                if (a == null || !a.isActiveAndEnabled || a.layerCount == 0)
                {
                    headHits = 0;
                    return;
                }
                if (!ReferenceEquals(headRendererOf, donkey))
                {
                    headRendererOf = donkey;
                    headRenderer = null;
                    foreach (SpriteRenderer sr in donkey.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
                    {
                        if (sr.name == "hed")
                        {
                            headRenderer = sr;
                            break;
                        }
                    }
                }
                SpriteRenderer h = headRenderer;
                if (h == null || a.IsInTransition(0))
                {
                    headHits = 0;
                    return;
                }
                int state = a.GetCurrentAnimatorStateInfo(0).shortNameHash;
                bool walk = WalkStates.Contains(state);
                if (!walk && !OtherStates.Contains(state))
                {
                    walk = BaseClip(a).IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
                    (walk ? WalkStates : OtherStates).Add(state);
                }
                if (!walk || h.sprite == null || !h.enabled || !h.gameObject.activeInHierarchy)
                {
                    headHits = 0;
                    return;
                }
                if (++headHits < 2)
                {
                    return; // two checks in a row (12 frames): not a walk's first frame
                }
                headHits = 0;
                if (!ReferenceEquals(headLoggedFor, donkey) && headsLogged < 20)
                {
                    headLoggedFor = donkey;
                    headsLogged++;
                    Plugin.Log.LogWarning($"[heads] {Describe()}{(mounted ? " (ridden)" : "")}: a still head ({h.sprite.name}) over the walk; its view was {viewOrigin}. "
                        + "An animator started with a head on that layer where the fix doesn't reach (docs/pooled-parts.md); please report this line.");
                }
            }

            // The base layer's strongest clip: the body's state (static_*, walk_*); the other 18 layers are the game's
            // generic ones (overhead, armour, talking), whatever the body does.
            private static string BaseClip(Animator a)
            {
                string best = "none";
                float w = 0f;
                foreach (AnimatorClipInfo c in a.GetCurrentAnimatorClipInfo(0))
                {
                    if (c.clip != null && c.weight > w)
                    {
                        w = c.weight;
                        best = c.clip.name;
                    }
                }
                return best;
            }
        }
    }
}
