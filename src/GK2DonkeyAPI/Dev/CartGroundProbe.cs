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

            // The donkey ridden without a cart ([rideground], same toggle): its root and its body sprite's ground line (the
            // bottom row of the frame's art, where the hooves stand) against the ground under the body and half a body
            // ahead and behind it (front and back hooves). The root's height in Ride style is the keeper's plus the ground
            // difference between the body point and the keeper (SnapToGround, GroundHeightPair); the line says which source.
            private float nextRideGroundProbe;

            private void ProbeRideGround(Vector3 keeper, Vector3 right)
            {
                if (!CartGroundProbeOn || !mounted || Time.unscaledTime < nextRideGroundProbe || donkey == null || !donkey)
                {
                    return;
                }
                nextRideGroundProbe = Time.unscaledTime + 0.2f;
                var sb = new StringBuilder("[rideground] ");
                float level = keeper.y;
                sb.Append($"keeper {keeper.y:F3} over {Heights(keeper, level)} | root {donkey.transform.position.y:F3} at ({donkey.transform.position.x:F2}, {donkey.transform.position.z:F2}) facing {(riderFacingX > 0f ? "right" : "left")}");
                sb.Append($" | snap {S.SnapToGround.Value} delta {groundDelta:F3} ({lastDonkeyGroundSource})");
                if (donkeyBodyRenderer != null && donkeyBodyRenderer.sprite != null)
                {
                    Sprite sp = donkeyBodyRenderer.sprite;
                    float ppu = sp.pixelsPerUnit > 0f ? sp.pixelsPerUnit : 50f;
                    // The frames carry a 2 px transparent border; the art's bottom row is the ground line.
                    float lineLocalY = (sp.textureRectOffset.y + 2f - sp.pivot.y) / ppu;
                    Vector3 line = donkeyBodyRenderer.transform.TransformPoint(new Vector3(0f, lineLocalY, 0f));
                    Vector3 body = donkeyBodyRenderer.bounds.center;
                    body.y = line.y;
                    sb.Append($" | body line {line.y:F3} (frame {sp.name})");
                    foreach (var (name, at) in new[] { ("under the body", body), ("front", body + right * (0.4f * riderFacingX)), ("back", body - right * (0.4f * riderFacingX)) })
                    {
                        float ray = HighestHitUnder(at, level, out _);
                        float nav = NavmeshHeight(at, level);
                        sb.Append($" | {name} ({at.x:F2}, {at.z:F2}): raycast {ray:F3} gap {line.y - ray:F3}, navmesh {nav:F3}");
                    }
                }
                sb.Append(ScreenPlacement());
                Plugin.Log.LogInfo(sb.ToString());
            }

            // Where the camera draws the keeper's and the donkey's body frames, in art pixels (for mock-ups that match the
            // game): each sprite's pivot projected to the screen, the frame's pivot in the art, and the screen pixels per art
            // pixel across and up. Keeper minus donkey: + right, + up.
            private string ScreenPlacement()
            {
                Camera cam = Camera.main;
                if (cam == null || keeperBodyRenderer == null || keeperBodyRenderer.sprite == null || donkeyBodyRenderer == null || donkeyBodyRenderer.sprite == null)
                {
                    return " | screen: not available";
                }
                Vector3 k = cam.WorldToScreenPoint(keeperBodyRenderer.transform.position);
                Vector3 d = cam.WorldToScreenPoint(donkeyBodyRenderer.transform.position);
                Vector3 x1 = cam.WorldToScreenPoint(donkeyBodyRenderer.transform.position + Vector3.right * 0.02f);
                Vector3 y1 = cam.WorldToScreenPoint(donkeyBodyRenderer.transform.position + Vector3.up * PxWorldY);
                float pxX = Mathf.Abs(x1.x - d.x), pxY = Mathf.Abs(y1.y - d.y);
                Sprite ks = keeperBodyRenderer.sprite, ds = donkeyBodyRenderer.sprite;
                return $" | screen: art px = {pxX:F2} x {pxY:F2} screen px; keeper pivot - donkey pivot = ({(k.x - d.x) / pxX:F2}, {(k.y - d.y) / pxY:F2}) art px; keeper {ks.name} pivot ({ks.pivot.x:F1}, {ks.pivot.y:F1}) rect {ks.rect.width}x{ks.rect.height}, donkey {ds.name} pivot ({ds.pivot.x:F1}, {ds.pivot.y:F1}) rect {ds.rect.width}x{ds.rect.height}; keeper drawn {(keeperBodyRenderer.transform.position.z < donkeyBodyRenderer.transform.position.z ? "nearer the camera" : "further")} (z {keeperBodyRenderer.transform.position.z:F3} vs {donkeyBodyRenderer.transform.position.z:F3}); seat {seatApplied:F3}, torso shift {TorsoShiftWorld.x:F3}";
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
