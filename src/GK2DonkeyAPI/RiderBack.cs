using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The armoured rider seen from behind (facing up on the side-on donkey, docs/mounted-combat.md §10.5): drawn whole,
    // his legs and the bottom of his torso hang in front of the donkey's body and he reads as standing on its back. The
    // donkey is drawn behind the keeper (Ride.Depth), so nothing covers them; this cuts them instead. Every frame after
    // the animator and the game's skin changer (OnKeeperLateUpdate), while the keeper is drawn facing up in his armour:
    // the leg renderer is switched off, and the body and arm renderers show a copy of their sprite cropped at the
    // donkey's back line (the line the near-leg overlay uses: the donkey body frame's trimmed bottom + 2 + BackHeightPx,
    // converted into the keeper's sprite rows through the donkey body's local space, as RiderLeg does). Sprites fully
    // below the line switch their renderer off. The cut sprites keep the source's name: the skin changer resolves by
    // the current sprite's name and would otherwise disable a renderer whose sprite it cannot place. As soon as the
    // facing is not up, or at dismount, each renderer gets its source sprite and its enabled flag back; the game's own
    // writes (the animator's sprite, the skin changer's enabled flag) take over again from the next frame anyway.
    internal static class RiderBack
    {
        private const float Px = 0.02f; // one art pixel in sprite-local units (50 px per unit)

        private static readonly Dictionary<Sprite, Sprite> cuts = new Dictionary<Sprite, Sprite>();      // source -> cut, keyed by source and cut row
        private static readonly Dictionary<Sprite, Sprite> sources = new Dictionary<Sprite, Sprite>();   // cut -> source
        private static readonly Dictionary<Sprite, int> cutRows = new Dictionary<Sprite, int>();         // cut -> the row it was cut at
        private static readonly List<SpriteRenderer> touched = new List<SpriteRenderer>(3);
        private static readonly List<bool> wasEnabled = new List<bool>(3);
        private static Transform root;
        private static SpriteRenderer arm, leg;
        private static bool active;
        private static bool loggedOnce;

        internal static bool Active => active;

        // show: the keeper is drawn facing up, mounted, in armour. keeperBody: gfx/bdy. donkeyBody: the donkey's bdy.
        internal static void Update(Transform seatRoot, SpriteRenderer keeperBody, SpriteRenderer donkeyBody, bool show)
        {
            if (!show || seatRoot == null || !seatRoot || keeperBody == null || !keeperBody || donkeyBody == null || !donkeyBody || donkeyBody.sprite == null)
            {
                if (active)
                {
                    Restore();
                }
                return;
            }
            if (root != seatRoot)
            {
                root = seatRoot;
                Transform a = seatRoot.Find("gfx/arm");
                Transform l = seatRoot.Find("gfx/leg");
                arm = a != null ? a.GetComponent<SpriteRenderer>() : null;
                leg = l != null ? l.GetComponent<SpriteRenderer>() : null;
            }
            active = true;

            // The back line in the keeper's own sprite rows (bottom-based, 96-row frame), measured on screen: the donkey
            // stands further from the camera than the keeper (Ride.Depth), so it sits higher on screen than a plain
            // overlay of the two frames would, and the first cut (converted through the donkey body's local space) left
            // a gap of several rows between the rider and the back (the user's video, 12:23). The back line's screen
            // height against the keeper sprite's bottom and top rows gives the row where the two really meet.
            Sprite d = donkeyBody.sprite;
            Sprite k = keeperBody.sprite;
            if (k != null && sources.TryGetValue(k, out Sprite kSrc))
            {
                k = kSrc;
            }
            if (k == null || CameraSystem.Instance == null)
            {
                return;
            }
            float groundPx = d.textureRectOffset.y + 2f;
            float topYPx = groundPx + Mount.BackHeightPx(d) + Mount.S.LegAdjustYPx.Value;
            Vector3 backWorld = donkeyBody.transform.TransformPoint(new Vector3(0f, (topYPx - d.pivot.y) * Px, 0f));
            float kBottomRow = k.textureRectOffset.y;
            float kTopRow = kBottomRow + k.textureRect.height;
            Vector3 kBottomWorld = keeperBody.transform.TransformPoint(new Vector3(0f, (kBottomRow - k.pivot.y) * Px, 0f));
            Vector3 kTopWorld = keeperBody.transform.TransformPoint(new Vector3(0f, (kTopRow - k.pivot.y) * Px, 0f));
            float sBack = CameraSystem.WorldToScreenPoint(backWorld).y;
            float sBottom = CameraSystem.WorldToScreenPoint(kBottomWorld).y;
            float sTop = CameraSystem.WorldToScreenPoint(kTopWorld).y;
            if (Mathf.Abs(sTop - sBottom) < 0.001f)
            {
                return;
            }
            float rowsPerScreenPx = (kTopRow - kBottomRow) / (sTop - sBottom);
            int cutRow = Mathf.RoundToInt(kBottomRow + (sBack - sBottom) * rowsPerScreenPx) + Mount.S.BackCutAdjustPx.Value;

            Apply(keeperBody, cutRow);
            Apply(arm, cutRow);
            if (leg != null && leg)
            {
                Remember(leg);
                if (leg.enabled)
                {
                    leg.enabled = false;
                }
            }
            if (!loggedOnce)
            {
                loggedOnce = true;
                Plugin.Log.LogInfo($"Mount: rider from behind: body and arm cut at frame row {cutRow} (donkey back row {topYPx:F1} at screen y {sBack:F1}; keeper body {k.name} rows {kBottomRow:F0}..{kTopRow:F0} at screen y {sBottom:F1}..{sTop:F1}, {1f / rowsPerScreenPx:F2} screen px per row; adjust {Mount.S.BackCutAdjustPx.Value}), legs hidden.");
            }
        }

        private static void Remember(SpriteRenderer r)
        {
            if (!touched.Contains(r))
            {
                touched.Add(r);
                wasEnabled.Add(r.enabled);
            }
        }

        private static void Apply(SpriteRenderer r, int cutRow)
        {
            if (r == null || !r || r.sprite == null)
            {
                return;
            }
            Remember(r);
            Sprite src = r.sprite;
            if (sources.TryGetValue(src, out Sprite original))
            {
                // Still our cut from last frame (the animator did not rewrite the sprite): cut from the source again.
                if (cutRows.TryGetValue(src, out int row) && row == cutRow)
                {
                    return;
                }
                src = original;
            }
            float bottom = src.textureRectOffset.y;
            float top = bottom + src.textureRect.height;
            if (top <= cutRow)
            {
                if (r.enabled)
                {
                    r.enabled = false; // wholly below the back line
                }
                return;
            }
            if (!r.enabled)
            {
                r.enabled = true;
            }
            int drop = cutRow - Mathf.RoundToInt(bottom);
            if (drop <= 0)
            {
                if (r.sprite != src)
                {
                    r.sprite = src;
                }
                return;
            }
            Sprite cut = CutFor(src, drop, cutRow);
            if (cut != null && r.sprite != cut)
            {
                r.sprite = cut;
            }
        }

        private static Sprite CutFor(Sprite src, int drop, int cutRow)
        {
            if (cuts.TryGetValue(src, out Sprite cached) && cached != null && cutRows.TryGetValue(cached, out int row) && row == cutRow)
            {
                return cached;
            }
            Rect tr = src.textureRect;
            int h = Mathf.RoundToInt(tr.height) - drop;
            if (h <= 0)
            {
                return null;
            }
            Rect rect = new Rect(tr.x, tr.y + drop, tr.width, h);
            Vector2 off = src.textureRectOffset;
            // the frame's pivot in the cut's pixel space: the trimmed offset plus the dropped rows below it
            Vector2 pivotPx = new Vector2(src.pivot.x - off.x, src.pivot.y - off.y - drop);
            Vector2 pivot = new Vector2(pivotPx.x / rect.width, pivotPx.y / rect.height);
            Sprite cut = RiderLeg.CreateSlice(src, rect, pivot, src.name); // the source's name: the skin changer resolves by it
            cuts[src] = cut;
            sources[cut] = src;
            cutRows[cut] = cutRow;
            return cut;
        }

        // Every touched renderer back to its source sprite and its enabled flag; the caches stay (sprites are cheap to
        // keep and the facing comes back often). Called when the facing leaves "up" and at dismount.
        internal static void Restore()
        {
            for (int i = 0; i < touched.Count; i++)
            {
                SpriteRenderer r = touched[i];
                if (r == null || !r)
                {
                    continue;
                }
                if (r.sprite != null && sources.TryGetValue(r.sprite, out Sprite src))
                {
                    r.sprite = src;
                }
                if (r.enabled != wasEnabled[i])
                {
                    r.enabled = wasEnabled[i];
                }
            }
            touched.Clear();
            wasEnabled.Clear();
            active = false;
        }

        // At dismount: restored, and the renderer lookups dropped (the next ride may have another view).
        internal static void Detach()
        {
            Restore();
            root = null;
            arm = null;
            leg = null;
        }
    }
}
