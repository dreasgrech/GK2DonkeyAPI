using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The rider's motion while the donkey walks (docs/rider-motion.md). The riding pose only swaps sprites, and keeps
    // every body part still: measured 2026-10-03, the rider moved as one rigid piece, 1 px either way with the donkey's
    // back. Three additions, each a style setting:
    //   - the body bobs BobScale times the back, BobLagFrames walk frames behind it (the weight follows the step);
    //   - the hands (the arm layer) don't follow HandsSteady of that bob, and move HandsSwayPx forward and back with
    //     each stride;
    //   - the head (head, hair, beard, eyes) settles HeadLagFrames after the body.
    // The donkey's walk is a fixed 8-frame cycle, so "a frame late" is a lookup in the back's table, not a history:
    // a stop (a frame outside the walk) ends every offset at once.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            // The donkey's walk frame (1-8) per body sprite, 0 outside the walk cycle. Kept per sprite, as BackHeightPx.
            private static readonly Dictionary<int, int> walkFrames = new Dictionary<int, int>();

            private static int WalkFrameIndex(Sprite donkeyBody)
            {
                if (donkeyBody == null)
                {
                    return 0;
                }
                int id = donkeyBody.GetInstanceID();
                if (!walkFrames.TryGetValue(id, out int f))
                {
                    if (walkFrames.Count > 256)
                    {
                        walkFrames.Clear();
                    }
                    f = WalkFrameIndexByName(donkeyBody.name);
                    walkFrames[id] = f;
                }
                return f;
            }

            private static int WalkFrameIndexByName(string n)
            {
                if (string.IsNullOrEmpty(n))
                {
                    return 0;
                }
                int clone = n.IndexOf("(Clone)", StringComparison.Ordinal);
                if (clone > 0)
                {
                    n = n.Substring(0, clone);
                }
                if (n.IndexOf("_walk_", StringComparison.Ordinal) < 0 || n.Length < 2)
                {
                    return 0;
                }
                return int.TryParse(n.Substring(n.Length - 2), out int f) && f >= 1 && f <= 8 ? f : 0;
            }

            // The back's height above 25 px at walk frame i (wrapped to 1-8): the same table as BackHeightPxByName.
            private static float BackDeltaAt(int i)
            {
                i = ((i - 1) % 8 + 8) % 8 + 1;
                if (i == 1 || i == 5) return -1f;
                if (i == 3 || i == 7) return 1f;
                return 0f;
            }

            private int CurrentWalkFrame()
            {
                return donkeyBodyRenderer != null && donkeyBodyRenderer.sprite != null ? WalkFrameIndex(donkeyBodyRenderer.sprite) : 0;
            }

            // The rider's bob this frame, in art pixels: the back's, BobLagFrames (+ extraLag) frames late, times BobScale.
            // 0 without Bob or outside the walk.
            private float RiderBobPx(int extraLag)
            {
                if (!S.Bob.Value)
                {
                    return 0f;
                }
                int f = CurrentWalkFrame();
                if (f == 0)
                {
                    return 0f;
                }
                return S.BobScale.Value * BackDeltaAt(f - S.BobLagFrames.Value - extraLag);
            }

            // One keeper sprite layer moved by the rider motion. The walk-type clips key some of these positions
            // (head, hair, beard), so the offset goes on top of what the animator wrote this frame: when the position is
            // still the one written last, the animator didn't touch it and the old offset comes off first.
            private sealed class RiderPart
            {
                public Transform T;
                public Vector3 Written;
                public Vector3 Offset;
                public bool Applied;
            }

            private static readonly string[] HeadPartPaths = { "gfx/hed", "gfx/hrs", "gfx/brd", "gfx/eys" };
            private RiderPart armPart;
            private readonly List<RiderPart> headParts = new List<RiderPart>();
            private Transform partsRoot;

            private void FindRiderParts()
            {
                if (partsRoot == seatRoot)
                {
                    return;
                }
                RestoreRiderParts();
                partsRoot = seatRoot;
                if (partsRoot == null || !partsRoot)
                {
                    return;
                }
                Transform arm = partsRoot.Find("gfx/arm");
                armPart = arm != null ? new RiderPart { T = arm } : null;
                foreach (string path in HeadPartPaths)
                {
                    Transform t = partsRoot.Find(path);
                    if (t != null)
                    {
                        headParts.Add(new RiderPart { T = t });
                    }
                }
                if (armPart == null || headParts.Count < HeadPartPaths.Length)
                {
                    Plugin.Log.LogWarning($"Mount: rider motion found {(armPart == null ? "no" : "the")} arm layer and {headParts.Count} of {HeadPartPaths.Length} head layers; the missing ones stay still.");
                }
            }

            // After TickSeat, from the keeper's LateUpdate (after the animator and the skin swap).
            private void TickRiderParts(PlayerController pc)
            {
                if (seatRoot == null || !seatRoot)
                {
                    return;
                }
                FindRiderParts();
                float body = RiderBobPx(0);
                if (armPart != null && (InArmour(pc) || IsAttackState(pc.Ssm?.CurState)))
                {
                    // The weapons (sword, bow, arrow, quiver) are drawn on the tol/* layers, which this motion does not
                    // move: a swaying arm would drift off its weapon by the sway and bob pixels (docs/mounted-combat.md
                    // §9.4). The arm stays where the animator draws it; the head keeps its lag.
                    ApplyPart(armPart, Vector3.zero);
                }
                else if (armPart != null)
                {
                    int f = CurrentWalkFrame();
                    // Forward on frames 2 and 6, back on 4 and 8 (twice a stride, with the donkey's nod). The art faces
                    // left and the keeper's facing mirrors above these layers, so local -x is forward either way.
                    float sway = f == 0 ? 0f : Mathf.Sin((f - 1) * Mathf.PI / 2f);
                    Vector3 off = new Vector3(-S.HandsSwayPx.Value * sway * 0.02f, -S.HandsSteady.Value * body * PxWorldY, 0f);
                    ApplyPart(armPart, off);
                }
                // HeadSettle scales it: the back's cycle is 4 frames, so a head a frame late steps against the body at every
                // change (measured in the user's video 2026-10-03: with all of it, the head jumped about 3 art px against
                // the torso, a bobble head).
                float head = S.HeadLagFrames.Value > 0 ? S.HeadSettle.Value * (RiderBobPx(S.HeadLagFrames.Value) - body) : 0f;
                Vector3 headOff = new Vector3(0f, head * PxWorldY, 0f);
                foreach (RiderPart p in headParts)
                {
                    ApplyPart(p, headOff);
                }
            }

            private static void ApplyPart(RiderPart p, Vector3 offset)
            {
                if (p.T == null || !p.T)
                {
                    return;
                }
                if (!p.Applied && offset == Vector3.zero)
                {
                    return;
                }
                Vector3 cur = p.T.localPosition;
                Vector3 basePos = p.Applied && (cur - p.Written).sqrMagnitude < 1e-12f ? cur - p.Offset : cur;
                Vector3 next = basePos + offset;
                if ((next - cur).sqrMagnitude > 1e-12f)
                {
                    p.T.localPosition = next;
                }
                p.Written = next;
                p.Offset = offset;
                p.Applied = offset != Vector3.zero;
            }

            // With the seat (RestoreSeat): takes each offset off again, unless the animator has written the position since.
            private void RestoreRiderParts()
            {
                try
                {
                    if (armPart != null)
                    {
                        RestorePart(armPart);
                    }
                    foreach (RiderPart p in headParts)
                    {
                        RestorePart(p);
                    }
                }
                catch
                {
                }
                armPart = null;
                headParts.Clear();
                partsRoot = null;
            }

            private static void RestorePart(RiderPart p)
            {
                if (p.Applied && p.T != null && p.T && (p.T.localPosition - p.Written).sqrMagnitude < 1e-12f)
                {
                    p.T.localPosition = p.Written - p.Offset;
                }
                p.Applied = false;
            }
        }
    }
}
