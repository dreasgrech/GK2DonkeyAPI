using UnityEngine;

namespace GK2DonkeyAPI
{
    // Ground the keeper can stand on, for the two moments the mod puts someone somewhere (docs/safe-ground.md).
    //
    // While ridden, the donkey is drawn next to the keeper wherever he goes, and that place is never checked: at the end
    // of a jetty it is over the water. Found 2026-10-03 (the user's videos): the game walked the mounted keeper to the
    // fishing spot at the jetty's tip, the ride ended there and the donkey was left standing in the lake; getting on it
    // again put the keeper at its seat, over the water, and he fell through the map.
    //   - Park (every dismount in the world): a donkey left off the ground is moved to the nearest spot it can stand on
    //     next to the keeper (KeepOnGround).
    //   - Getting on (RideFromWorld, Hitch): the keeper is moved to the seat only when it is on the navmesh at the
    //     seat's height and he can walk there; otherwise the donkey comes to him instead (or the hitch is refused).
    // For the donkey "ground" is the navmesh, or the ground raycast at about the keeper's level (GroundAt): the navmesh
    // alone is cut back along walls, and the raycast alone also finds a lake bed. For the keeper only the navmesh
    // counts. A scene without a navmesh can't be judged: nothing changes there.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            private static bool CanJudgeGround => MainGame.PlayerController?.SceneRecastGraph != null;

            // The navmesh's height under a point (navmesh only, see above).
            private static bool OnNavmesh(Vector3 p, out float y)
            {
                y = GroundHeight(p, out string src);
                return !float.IsNaN(y) && src == "navmesh";
            }

            // Ground at a point, judged against a height that is known to be ground (the keeper's): the navmesh, or the
            // ground raycast at about that height. Along a wall or a fence the navmesh is cut back, but the ground is
            // still there at the keeper's level; over water there is no navmesh, and the raycast finds nothing or the
            // bed well below.
            private static bool GroundAt(Vector3 p, float levelY, out float y)
            {
                y = GroundHeight(p, out string src);
                return !float.IsNaN(y) && (src == "navmesh" || Mathf.Abs(y - levelY) <= MaxRise);
            }

            private static Vector3 KeeperNow(PlayerController pc)
            {
                Rigidbody rb = pc.PhysicalBody?.Rb;
                return rb != null ? rb.transform.position : pc.PlayerData.position.Value;
            }

            // Null when the keeper may be put at 'spot' (the seat of a donkey he gets on), else why not. 'spot' comes back
            // never below the navmesh: PlayerController.SetPosition puts his body at exactly that height, and under the
            // ground's surface nothing holds him.
            private string WhyKeeperCannotStandAt(PlayerController pc, ref Vector3 spot)
            {
                if (!CanJudgeGround)
                {
                    return null;
                }
                // The navmesh only: the keeper falls where nothing holds him, and the ground raycast is no proof of that
                // (a water surface on a ground layer would pass). Too strict at worst: then the donkey comes to him.
                if (!OnNavmesh(spot, out float g))
                {
                    return $"no navmesh under {spot:F2} ({(float.IsNaN(g) ? "no ground found" : "other ground at " + g.ToString("F2"))})";
                }
                if (Mathf.Abs(g - spot.y) > MaxRise)
                {
                    return $"the walkable ground there is at {g:F2}, the seat at {spot.y:F2}";
                }
                // Ground the whole way from the keeper (no ledge, no water), not LineWalkable: its collider test called a
                // keeper riding along a fence "blocked" at his own feet, and every hitch there was refused (the cart G
                // brought stayed by the fence, the user's video 2026-10-03). Colliders were never the danger here.
                string gap = GroundGap(KeeperNow(pc), spot);
                if (gap != null)
                {
                    return "the way there: " + gap;
                }
                spot.y = Mathf.Max(spot.y, g);
                return null;
            }

            // Null when there is ground all along a line (the navmesh or the ground raycast, water excluded), with no step
            // between samples higher than the shuffle allows; else where it isn't.
            private static string GroundGap(Vector3 a, Vector3 b)
            {
                float len = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                int samples = Mathf.Clamp(Mathf.CeilToInt(len / 0.2f), 5, 80);
                float prev = float.NaN;
                Vector3 prevP = a;
                for (int i = 0; i <= samples; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)samples);
                    float g = GroundHeight(p, out string src);
                    if (float.IsNaN(g))
                    {
                        return $"no ground at {p:F2} ({src})";
                    }
                    if (i > 0 && !StepFits(prevP, prev, p, g, MaxRisePerSample))
                    {
                        return $"a step from {prev:F2} to {g:F2} at {p:F2} ({src})";
                    }
                    prev = g;
                    prevP = p;
                }
                return null;
            }

            // Park: where the ride left the donkey, checked. Its body (where it stands) or its cart off the ground
            // (GroundAt, at the keeper's level), the donkey moves to the nearest spot next to him where both stand on
            // ground; failing that, its body onto the keeper's spot.
            private void KeepOnGround(PlayerController pc)
            {
                if (!CanJudgeGround || donkey == null || !donkey)
                {
                    return;
                }
                Vector3 keeper = KeeperNow(pc);
                Vector3 right = Vector3.Cross(Vector3.up, AwayFromCamera()).normalized;
                Vector3 bodyOffset = bodySprite != null && bodySprite ? bodySprite.position - donkey.transform.position : right * LateralFor(riderFacingX);
                bodyOffset.y = 0f;
                // The cart stands about a unit behind the donkey: on a jetty or a bank it can be over the water with the
                // donkey on land (the cargo is set down at it too, CartPoint).
                Vector3 cartOffset = Vector3.zero;
                if (HasCart)
                {
                    cartOffset = CartPoint() - Position;
                    cartOffset.y = 0f;
                }
                Vector3 root = Position;
                Vector3 body = root + bodyOffset;
                bool bodyOk = GroundAt(body, keeper.y, out _);
                bool cartOk = !HasCart || GroundAt(root + cartOffset, keeper.y, out _);
                if (bodyOk && cartOk)
                {
                    return;
                }
                Vector3 to = keeper - bodyOffset; // the last resort: its body (its feet) on the keeper's spot, ground for sure
                string how = "onto the keeper's spot (no free ground next to him)";
                bool found = false;
                int rings = HasCart ? 12 : 8; // out to 3 units with a cart, 2 without
                for (int ri = 1; ri <= rings && !found; ri++)
                {
                    float r = ri * 0.25f;
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI / 6f;
                        Vector3 b = body + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        if (!OnNavmesh(b, out float gb))
                        {
                            continue;
                        }
                        b.y = gb;
                        Vector3 rt = b - bodyOffset;
                        if (!GroundAt(rt, gb, out float gr))
                        {
                            continue;
                        }
                        float stand = StandHeight(rt); // on a deck over the navmesh, not inside it
                        rt.y = float.IsNaN(stand) ? gr : stand;
                        if (HasCart && !GroundAt(rt + cartOffset, gb, out _))
                        {
                            continue;
                        }
                        if (!FreeAt(b, out _) || !FreeAt(rt, out _))
                        {
                            continue;
                        }
                        groundFail = null;
                        if (!LineWalkable(keeper, b, "park"))
                        {
                            continue;
                        }
                        to = rt;
                        how = $"{r:F2} from where it was";
                        found = true;
                        break;
                    }
                }
                groundFail = null; // the shuffle's own log reads it
                string what = !bodyOk ? "off walkable ground" : "with its cart off walkable ground";
                Plugin.Log.LogInfo($"Mount: {Describe()} was left {what} at {root:F2} (water, a ledge); moved {how}, to {to:F2}.");
                TierNow();
                Position = to;
            }

            // Leaving the cart (Unhitch): null when its cart stands on ground (GroundAt, at the keeper's level), else why
            // not. The free cart stays exactly where it is drawn now, and its cargo is set down there when it goes.
            private string WhyCartCannotStayHere(PlayerController pc)
            {
                if (!CanJudgeGround || !HasCart || donkey == null || !donkey)
                {
                    return null;
                }
                Vector3 c = CartPoint();
                if (GroundAt(c, KeeperNow(pc).y, out float g))
                {
                    return null;
                }
                GroundHeight(c, out string src); // the source, for the log: water:<collider> (the water tag), none, raycast
                return $"no ground under the cart at {c:F2} ({(float.IsNaN(g) ? src : "ground at " + g.ToString("F2") + ", " + src)})";
            }
        }
    }
}
