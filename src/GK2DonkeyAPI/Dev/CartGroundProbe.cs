using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Investigation (Debug tab, session only): does the ridden cart touch the ground on a hill? The user's video
    // (2026-10-03 11-48-07 PM): going down a hill the cart seemed to float, going back up a wheel went into the ground.
    // Five times a second while driving, one [cartground] line: the keeper's level, the donkey root, the cart's lift and
    // pitch, the ground under the root's spot and under the cart body (the lift is that against the root's height), and for every wheel part
    // its drawn bottom against the ground under it, both by the navmesh and by the ground raycast (the surface the keeper
    // walks on), and the keeper's body against the ground under him. gap > 0: the wheel floats above that ground; gap < 0:
    // it is inside it. Frame by frame the video showed the wheel's shadow part from it as the slope down begins (on the
    // flat it touches), i.e. the wheel lifted off the drawn ground.
    internal static partial class Mount
    {
        internal static bool CartGroundProbeOn;

        internal sealed partial class Instance
        {
            private float nextCartGroundProbe;

            private void ProbeCartGround(Vector3 refPoint, Vector3 cartPoint, float levelY)
            {
                if (!CartGroundProbeOn || !mounted || Time.unscaledTime < nextCartGroundProbe || donkey == null || !donkey)
                {
                    return;
                }
                nextCartGroundProbe = Time.unscaledTime + 0.2f;
                var sb = new StringBuilder("[cartground] ");
                sb.Append($"keeper {levelY:F3} root {donkey.transform.position.y:F3} facing {(riderFacingX > 0f ? "right" : "left")} ");
                sb.Append($"style {S.CartSlope.Value} pitch {lastPitchDeg:F1} lift target {cartShiftTarget:F3} applied {cartShiftApplied:F3} ({cartBodyPart?.LastSource})");
                // The keeper's own body against the ground under him: the ride's heights all hang from his (a body in the
                // air over a crest would lift the donkey and the cart with it).
                Rigidbody rb = MainGame.PlayerController?.PhysicalBody?.Rb;
                if (rb != null)
                {
                    Vector3 k = rb.transform.position;
                    sb.Append($" | keeper body {k.y:F3} over {Heights(k, k.y)}");
                }
                sb.Append($" | root spot {Heights(refPoint, levelY)} | cart point {Heights(cartPoint, levelY)}");
                foreach (CartPart p in cartParts)
                {
                    if (p.T == null || !p.T || p.T.name.IndexOf("wheel", System.StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }
                    Renderer r = p.T.GetComponentInChildren<Renderer>();
                    if (r == null || !r.enabled)
                    {
                        continue;
                    }
                    Bounds b = r.bounds;
                    Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
                    float nav = NavmeshHeight(bottom, levelY);
                    float ray = HighestHitUnder(bottom, levelY, out _);
                    sb.Append($" | {p.T.name} ({(r is SpriteRenderer ? "sprite" : "mesh")}) bottom {b.min.y:F3} at ({b.center.x:F2}, {b.center.z:F2}): gap {b.min.y - ray:F3} to the raycast {ray:F3}, {b.min.y - nav:F3} to the navmesh {nav:F3}");
                }
                Plugin.Log.LogInfo(sb.ToString());
            }

            private static string Heights(Vector3 p, float levelY)
            {
                float nav = NavmeshHeight(p, levelY);
                float ray = HighestHitUnder(p, levelY, out Collider c);
                return $"({p.x:F2}, {p.z:F2}) navmesh {nav:F3} raycast {ray:F3} ({(c != null ? c.name : "none")})";
            }
        }
    }
}
