using System;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The still head over the walking body (the "double donkey", docs/pooled-parts.md) came back on 2026-10-04 with
    // no [pool] or [view] line: whatever bound the animator's default head this time, it wasn't a part reused without
    // a reset. Measured rather than guessed: the walk clips never draw the head layer ("hed": the walking body carries
    // its own head), so a hed sprite while every playing clip is a walk is that bug, whatever caused it. Each view is
    // checked a few times a second; a hit is logged with where the view came from and what happened to it since.
    //
    // Measured 2026-10-04 (the [heads] lines): only views from the mod's own pool, and neither the prefab's sprites nor
    // a cleared head before Animator.Rebind changed it: the head was back within 12 frames after each try, so the
    // value the walk falls back to isn't one the mod can set by binding again. So the walk's head layer is emptied
    // every frame instead (HeadGuardFrame), after the animator has written it (the mounts update in the plugin's
    // LateUpdate): a walk never shows the head layer, whatever its default is.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            // Where the current view came from and what was done to it since (for the [heads] line).
            internal string viewOrigin = "none";
            internal int viewOriginFrame = -1;
            internal int lastRebindFrame = -1;
            internal string viewEvents = "";

            private SpriteRenderer headRenderer;
            private Wgo headRendererOf;
            private int headHits;
            private int nextHeadCheck;
            private static int headsLogged;
            private static int ridesLogged;
            private string lastBaseClip, lastHead;
            private bool headLoggedForView;         // the warning once per view
            private Animator headAnimator;

            // The base layer's walk states (their short name hashes), learnt from the clip names a few times a second, so
            // the per-frame guard needs no clip lookup (GetCurrentAnimatorClipInfo allocates).
            private static readonly System.Collections.Generic.HashSet<int> WalkStates = new System.Collections.Generic.HashSet<int>();

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
                string baseClip = BaseClip(a);
                string headNow = h.sprite != null && h.enabled && h.gameObject.activeInHierarchy ? h.sprite.name : "none";
                if (mounted && ridesLogged < 40 && (baseClip != lastBaseClip || headNow != lastHead))
                {
                    // While ridden: each change of the base layer's clip or of the head layer's sprite (measurement; the
                    // first version's walk test never matched: the animator has 14 layers, most never a walk).
                    ridesLogged++;
                    Plugin.Log.LogInfo($"[heads] ridden {Describe()}: base clip {baseClip}, head layer {headNow}, state {lastMirrored}; view {viewOrigin} {Time.frameCount - viewOriginFrame} frame(s) ago, since: {(viewEvents.Length > 0 ? viewEvents : "nothing")}.");
                }
                lastBaseClip = baseClip;
                lastHead = headNow;
                if (headNow == "none")
                {
                    headHits = 0;
                    return;
                }
                bool walk = baseClip.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
                if (walk && !a.IsInTransition(0))
                {
                    WalkStates.Add(a.GetCurrentAnimatorStateInfo(0).shortNameHash);
                }
                if (!walk)
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
                        + $"its view: {viewOrigin} {Time.frameCount - viewOriginFrame} frame(s) ago, animator last bound by the mod {(lastRebindFrame < 0 ? "never" : (Time.frameCount - lastRebindFrame) + " frame(s) ago")}, "
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
