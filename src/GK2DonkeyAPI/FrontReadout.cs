using UnityEngine;

namespace GK2DonkeyAPI
{
    internal static partial class Looks
    {
        // The front layer and the rider as the camera sees them, in the mount's own picture (dev, the flying experiment;
        // docs/flying.md "The rider behind the spread wing"). The near wing is split between the body picture and the front
        // picture round the rider's head, so the two must meet on the screen and the hole must be where his head is: a
        // composite of a still capture of him said both were right while the game showed otherwise (the user's video,
        // 2026-10-23 08-07-28). While flying, a few times a second, one line: the frame, which way the mount faces, where
        // the front picture sits against the body picture (art px on the screen, x towards the picture's right, y down)
        // and the box each of the rider's sprites covers in the picture's own pixels (x 0..127 from the left of the
        // picture as drawn, facing left, y from the top), mirrored back when the mount faces right. On the ground, one
        // line when he mounts or lands, for the front picture's offset there.
        internal static class FrontReadout
        {
            internal static bool On; // the Flying tab's "[occlusion] lines"; off by default since the wing over the rider was settled (2026-10-23)
            private static float next;
            private static int budget;
            private static string lastState;

            internal static void Note(Mount.Instance owner, SpriteRenderer part, SpriteRenderer body, SpriteRenderer front, string flyKey)
            {
                if (!On || !Flight.Active || owner == null || body == null || front == null || !front.enabled) return;
                SpriteRenderer[] rider = owner.KeeperRenderers;
                if (owner.KeeperBody == null || rider == null) return;
                // a fresh budget for each state and facing: one flight each way is logged both ways (the first run's
                // budget ran out before the user turned round)
                string state = (flyKey != null ? "flying " : "ground ") + (body.flipX ^ (body.transform.lossyScale.x < 0f) ? "a" : "b");
                if (state != lastState)
                {
                    lastState = state;
                    budget = 80;
                    next = 0f;
                }
                else if (flyKey == null) return; // the ground: once
                if (budget <= 0 || Time.unscaledTime < next) return;
                next = Time.unscaledTime + 0.13f;
                budget--;
                try
                {
                    Camera cam = Camera.main;
                    Sprite sp = body.sprite;
                    if (cam == null || sp == null || front.sprite == null) return;
                    // the picture's pixel (i, j) (j down from the top) on the screen, and back
                    Vector2 Screen(SpriteRenderer r, float i, float j)
                    {
                        Sprite s = r.sprite;
                        float ppu = s.pixelsPerUnit;
                        float x = (i - s.pivot.x) / ppu * (r.flipX ? -1f : 1f), y = (s.rect.height - j - s.pivot.y) / ppu;
                        return cam.WorldToScreenPoint(r.transform.TransformPoint(new Vector3(x, y, 0f)));
                    }
                    Vector2 o = Screen(body, 0f, 0f), ux = Screen(body, 1f, 0f) - o, uy = Screen(body, 0f, 1f) - o;
                    float det = ux.x * uy.y - ux.y * uy.x;
                    if (Mathf.Abs(det) < 1e-6f) return;
                    Vector2 Canvas(Vector2 p)
                    {
                        Vector2 d = p - o;
                        return new Vector2((d.x * uy.y - d.y * uy.x) / det, (ux.x * d.y - ux.y * d.x) / det);
                    }
                    Vector2 fo = Canvas(Screen(front, 0f, 0f));
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[occlusion] {(flyKey ?? "ground " + body.sprite.name)} facing {(ux.x < 0f ? "right" : "left")}: the front picture {fo.x:+0.0;-0.0} x, {fo.y:+0.0;-0.0} y art px from the body picture");
                    sb.Append($" ({ux.magnitude:0.0} screen px an art px); the rider in the picture's pixels:");
                    foreach (SpriteRenderer r in rider)
                    {
                        if (r == null || !r || !r.enabled || r.sprite == null || !r.gameObject.activeInHierarchy) continue;
                        Bounds b = r.sprite.bounds;
                        float x0 = r.flipX ? -b.max.x : b.min.x, x1 = r.flipX ? -b.min.x : b.max.x;
                        float cx0 = float.MaxValue, cx1 = float.MinValue, cy0 = float.MaxValue, cy1 = float.MinValue;
                        foreach (Vector3 c in new[] { new Vector3(x0, b.min.y), new Vector3(x1, b.min.y), new Vector3(x0, b.max.y), new Vector3(x1, b.max.y) })
                        {
                            Vector2 q = Canvas(cam.WorldToScreenPoint(r.transform.TransformPoint(c)));
                            cx0 = Mathf.Min(cx0, q.x); cx1 = Mathf.Max(cx1, q.x); cy0 = Mathf.Min(cy0, q.y); cy1 = Mathf.Max(cy1, q.y);
                        }
                        sb.Append($" {r.name}/{r.sprite.name} x {cx0:0.0}..{cx1:0.0} y {cy0:0.0}..{cy1:0.0};");
                    }
                    Plugin.Log.LogInfo(sb.ToString());
                }
                catch (System.Exception e)
                {
                    budget = 0;
                    Plugin.Log.LogWarning("[occlusion] the readout failed: " + e.Message);
                }
            }
        }
    }
}
