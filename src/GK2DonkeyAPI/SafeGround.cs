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
                groundFail = null;
                bool walkable = LineWalkable(KeeperNow(pc), spot, "keeper to the seat");
                string fail = groundFail;
                groundFail = null; // the shuffle's own log reads it
                if (!walkable)
                {
                    return "the way there: " + (fail ?? "blocked");
                }
                spot.y = Mathf.Max(spot.y, g);
                return null;
            }

            // Park: where the ride left the donkey, checked. Its body (where it stands) off the ground (GroundAt, at the
            // keeper's level), the donkey moves to the nearest spot next to him where it can stand; failing that, onto
            // the keeper's spot.
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
                Vector3 root = Position;
                Vector3 body = root + bodyOffset;
                if (GroundAt(body, keeper.y, out _))
                {
                    return;
                }
                Vector3 to = keeper - bodyOffset; // the last resort: its body (its feet) on the keeper's spot, ground for sure
                string how = "onto the keeper's spot (no free ground next to him)";
                bool found = false;
                for (int ri = 1; ri <= 8 && !found; ri++)
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
                        rt.y = gr;
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
                Plugin.Log.LogInfo($"Mount: {Describe()} was left off walkable ground at {root:F2} (water, a ledge); moved {how}, to {to:F2}.");
                TierNow();
                Position = to;
            }
        }
    }
}
