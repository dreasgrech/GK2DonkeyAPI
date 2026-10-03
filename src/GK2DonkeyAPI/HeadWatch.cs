using System;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The still head over the walking body (the "double donkey", docs/pooled-parts.md). The walk clips never draw the
    // head layer ("hed": the walking body carries its own head), so a hed sprite during a walk is that bug, whatever
    // caused it.
    //   - The cause (measured 2026-10-04, the [heads] lines): a view switched on while its head layer held a sprite (the
    //     prefab's own 0301_hed_static_down, put back by the pool reset) builds its animator with that as the walk's
    //     fallback; binding again later (Rebind) did not change it. Fixed where views are switched on (EmptyHeadLayer:
    //     TakeParkedView, the end of idle, ReleaseView for the game's pool).
    //   - The backup: HeadGuardFrame empties the head layer on every frame of a walk state, after the animator (the
    //     mounts update in the plugin's LateUpdate).
    //   - The check (HeadWatch, a few times a second): a view whose walk falls back to a head is logged once, with where
    //     the view came from and what happened to it since. No such line: the root fix held and the backup did nothing.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            // Where the current view came from and what was done to it since (for the [heads] line).
            internal string viewOrigin = "none";
            internal int viewOriginFrame = -1;
            internal string viewEvents = "";

            private SpriteRenderer headRenderer;
            private Wgo headRendererOf;
            private int headHits;
            private int nextHeadCheck;
            private static int headsLogged;
            private bool headLoggedForView;         // the warning once per view
            private Animator headAnimator;

            // The base layer's states (short name hashes), sorted into walks and others by their clip's name the first time
            // each is seen, so neither check needs a clip lookup after that (GetCurrentAnimatorClipInfo allocates).
            private static readonly System.Collections.Generic.HashSet<int> WalkStates = new System.Collections.Generic.HashSet<int>();
            private static readonly System.Collections.Generic.HashSet<int> OtherStates = new System.Collections.Generic.HashSet<int>();

            internal void NoteView(string origin)
            {
                viewOrigin = origin;
                viewOriginFrame = Time.frameCount;
                viewEvents = "";
                headLoggedForView = false;
            }

            internal void NoteViewEvent(string what)
            {
                if (viewEvents.Length < 300)
                {
                    viewEvents += (viewEvents.Length > 0 ? ", " : "") + what + " @" + (Time.frameCount - viewOriginFrame);
                }
            }

            // Every instance, every frame (MountRegistry.Tick); the work a few times a second.
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
                if (a == null || !a.isActiveAndEnabled)
                {
                    headHits = 0;
                    return;
                }
                if (!ReferenceEquals(headRendererOf, donkey) || headRenderer == null)
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
                if (h == null)
                {
                    headHits = 0;
                    return;
                }
                // The base layer's state, learnt once per state from its clip's name (the clip lookup allocates).
                int state = a.GetCurrentAnimatorStateInfo(0).shortNameHash;
                bool walk = WalkStates.Contains(state);
                if (!walk && !a.IsInTransition(0) && !OtherStates.Contains(state))
                {
                    walk = BaseClip(a).IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
                    (walk ? WalkStates : OtherStates).Add(state);
                }
                if (!walk || h.sprite == null || !h.enabled || !h.gameObject.activeInHierarchy)
                {
                    headHits = 0;
                    return;
                }
                headHits++;
                if (headHits < 2)
                {
                    return; // a walk starting from idle may show one frame of the old head
                }
                headHits = 0;
                if (!headLoggedForView && headsLogged < 20)
                {
                    headLoggedForView = true;
                    headsLogged++;
                    Plugin.Log.LogInfo($"[heads] {Describe()}{(mounted ? " (ridden)" : "")}: the walk's head layer falls back to {h.sprite.name} ({PlayingClips(a)}); "
                        + $"its view: {viewOrigin} {Time.frameCount - viewOriginFrame} frame(s) ago, "
                        + $"since then: {(viewEvents.Length > 0 ? viewEvents : "nothing")}. Emptied every walk frame (HeadGuardFrame), so it isn't seen.");
                }
            }

            // Every frame, after the animator (MountRegistry.Tick, in the plugin's LateUpdate): in a walk state of the base
            // layer the head layer is emptied (the walking body draws its own head). Not during a transition, so the idle
            // head stays until the walk has really started.
            internal void HeadGuardFrame()
            {
                SpriteRenderer h = headRenderer;
                if (h == null || h.sprite == null || donkey == null || !donkey || !ReferenceEquals(headRendererOf, donkey))
                {
                    return;
                }
                Animator a = headAnimator;
                if (a == null || !ReferenceEquals(headAnimatorOf, donkey))
                {
                    a = headAnimator = donkey.MainWgoPart?.AnimationComponent?.Animator;
                    headAnimatorOf = donkey;
                }
                if (a == null || !a.isActiveAndEnabled || a.layerCount == 0 || a.IsInTransition(0))
                {
                    return;
                }
                if (WalkStates.Contains(a.GetCurrentAnimatorStateInfo(0).shortNameHash))
                {
                    h.sprite = null;
                }
            }

            private Wgo headAnimatorOf;

            // The base layer's strongest clip: the body's state (static_*, walk_*); the other layers are the game's
            // generic ones (overhead, armour, talking), whatever the body does.
            private static string BaseClip(Animator a)
            {
                string best = "none";
                float w = 0f;
                if (a.layerCount > 0)
                {
                    foreach (AnimatorClipInfo c in a.GetCurrentAnimatorClipInfo(0))
                    {
                        if (c.clip != null && c.weight > w)
                        {
                            w = c.weight;
                            best = c.clip.name;
                        }
                    }
                }
                return best;
            }

            // Every layer's clips, with the layer's index (for the log).
            private static string PlayingClips(Animator a)
            {
                var sb = new StringBuilder();
                for (int layer = 0; layer < a.layerCount; layer++)
                {
                    foreach (AnimatorClipInfo c in a.GetCurrentAnimatorClipInfo(layer))
                    {
                        if (c.clip == null || c.weight < 0.01f)
                        {
                            continue;
                        }
                        sb.Append(sb.Length > 0 ? ", " : "").Append(layer).Append(':').Append(c.clip.name);
                    }
                }
                return sb.ToString();
            }
        }
    }
}
