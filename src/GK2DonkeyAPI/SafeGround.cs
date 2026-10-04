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
                return !float.IsNaN(y) && NearLevel(y, levelY, src);
            }

            // The navmesh also lies on top of rocks, and under a rock the floor is cut out of it: there the nearest navmesh
            // (seen from above) is the rock's top. Found 2026-10-04: a donkey parked by an ore was put on the rock, 2.4
            // above the keeper (log: "moved 0.25 ... to (-16.01, 8.41, 23.18)"). So any ground counts only near the level it
            // is judged against: the raycast within MaxRise, the navmesh within MaxRise plus a slope's rise (0.6 per unit
            // of distance, steeper than any road) to the point. Callers pass the distance through the level they give.
            private static bool NearLevel(float y, float levelY, string src) => Mathf.Abs(y - levelY) <= (src == "navmesh" ? MaxRise + 0.6f : MaxRise);

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
                    // No ground to judge here, but walls still are: the line and the body test (WhyKeeperCannotReach).
                    return WhyKeeperCannotReach(pc, KeeperNow(pc), spot);
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
                // Ground is not enough: the keeper must also be able to walk there himself. Found 2026-10-04 (the user,
                // RuinedTemple): the ride ended at an ore, the donkey drawn into the rock stepped out of it on the far
                // side (StepOutIfInside), and getting on put the keeper at its seat, in a pocket behind the rock face with
                // no way out. Ground, navmesh and height were all fine there. A move of the keeper is never allowed
                // without this check.
                string reach = WhyKeeperCannotReach(pc, KeeperNow(pc), spot);
                if (reach != null)
                {
                    return reach;
                }
                Plugin.Log.LogInfo($"Mount: the keeper may move from {KeeperNow(pc):F2} to {spot:F2} (ground, his body's way clear, room for him there, the game's walk grid and the walkable mesh connect).");
                return null;
            }

            // Null when the keeper could walk from 'from' to 'to' himself, else why not. What stops him in the game is
            // physics (his convex body, layer Player, against every collider his layer collides with, and the invisible
            // edge walls); the recast navmesh only sets his height. So every test is about colliders, each from another
            // angle, and all must pass (reviewed 2026-10-04 after the lock-in; docs/safe-ground.md):
            //   1. not far: at most MaxKeeperMove (every seat is within reach of the keeper anyway);
            //   2. his body's way: his shape (a capsule a little thinner than him) swept from 'from' to 'to', and rays at
            //      three heights both ways (a one-sided mesh blocks a ray from one side only), back faces included;
            //   3. his body fits at 'to': his own collider tested there (ComputePenetration, as the game's
            //      PlayerColliderTester does, with the game's 0.05 tolerance);
            //   4. the game's own walk grid (PlayerLocalAreaMovement: rescanned around him against the colliders, as the
            //      game does before every walk to a work spot) connects him to 'to';
            //   5. the scene's walkable mesh connects them too, where both lie on it.
            // A collider he starts inside doesn't count (casts and rays don't report it): the fence he rides along
            // (2026-10-03). Too strict at worst: the donkey comes to him, or a hitch is refused. Never a keeper behind a wall.
            internal const float MaxKeeperMove = 2f;
            private static readonly int DropLayer = LayerMask.NameToLayer("Drop");
            private const int GroundLayers = 6144; // 11 and 12, what the game walks on (PlayerPhysicalBody.IsCollisionValid)
            private static readonly RaycastHit[] reachHits = new RaycastHit[64];
            private static readonly Collider[] reachOverlaps = new Collider[64];

            internal static string WhyKeeperCannotReach(PlayerController pc, Vector3 from, Vector3 to)
            {
                float flat = FlatOff(from, to);
                if (flat > MaxKeeperMove)
                {
                    return $"{to:F2} is {flat:F1} from the keeper (the mod moves him {MaxKeeperMove:F0} at most)";
                }
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null)
                {
                    return "the keeper's body isn't there to test";
                }
                bool backfaces = Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces = true;
                    int mask = KeeperMask(rb);
                    string way = BodyWayBlocked(rb, mask, from, to);
                    if (way != null)
                    {
                        return "the way there is blocked: " + way;
                    }
                    string fit = BodyDoesNotFit(rb, body, mask, to);
                    if (fit != null)
                    {
                        return fit;
                    }
                }
                catch (System.Exception e)
                {
                    return "the body test failed: " + e.Message;
                }
                finally
                {
                    Physics.queriesHitBackfaces = backfaces;
                }
                return WhyNotOnGameGrid(pc, rb, to) ?? WhyNotConnected(from, to);
            }

            // A donkey's spot on the keeper's side of every wall: his body's way to it clear (as for moving him, but any
            // distance; placing the donkey never moves him).
            internal static string PlaceBlocked(PlayerController pc, Vector3 keeper, Vector3 spot)
            {
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                if (rb == null)
                {
                    return null;
                }
                bool backfaces = Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces = true;
                    return BodyWayBlocked(rb, KeeperMask(rb), keeper, spot);
                }
                finally
                {
                    Physics.queriesHitBackfaces = backfaces;
                }
            }

            // Every layer the keeper's body collides with.
            private static int KeeperMask(Rigidbody rb)
            {
                int layer = rb.gameObject.layer, mask = 0;
                for (int l = 0; l < 32; l++)
                {
                    if (!Physics.GetIgnoreLayerCollision(layer, l))
                    {
                        mask |= 1 << l;
                    }
                }
                return mask;
            }

            // A hit that stops him: not himself, not our views, not a walkable floor (a ground layer facing up).
            private static bool Blocks(Rigidbody rb, Collider c, Vector3 normal)
            {
                if (c == null || !c || c.attachedRigidbody == rb || IsModView(c))
                {
                    return false;
                }
                // Items lying on the ground (layer Drop: picked up when he walks over them) and anything he pushes away
                // can't wall him in. Found 2026-10-04: an ore's drop ("SmallItemTriggerCollider (layer Drop)") refused a seat.
                if (c.gameObject.layer == DropLayer || c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                {
                    return false;
                }
                return !(((1 << c.gameObject.layer) & GroundLayers) != 0 && normal.y > 0.5f);
            }

            private static string Describe(Collider c, Vector3 at) => $"{c.name} (layer {LayerMask.LayerToName(c.gameObject.layer)}) at {at:F2}";

            private static string BodyWayBlocked(Rigidbody rb, int mask, Vector3 from, Vector3 to)
            {
                Vector3 d = to - from;
                float len = d.magnitude;
                if (len < 0.001f)
                {
                    return null;
                }
                Vector3 dir = d / len;
                const float r = 0.12f; // his body is 0.30 deep and 0.50 wide
                int n = Physics.CapsuleCastNonAlloc(from + Vector3.up * 0.2f, from + Vector3.up * 0.95f, r, dir, reachHits, len, mask, QueryTriggerInteraction.Ignore);
                if (n >= reachHits.Length)
                {
                    return "too many colliders on the way to judge";
                }
                for (int i = 0; i < n; i++)
                {
                    RaycastHit h = reachHits[i];
                    if (h.distance <= 0f) // started inside it (the fence he rides along)
                    {
                        continue;
                    }
                    if (Blocks(rb, h.collider, h.normal))
                    {
                        return "his body hits " + Describe(h.collider, h.point);
                    }
                }
                foreach (float y in new[] { 0.15f, 0.5f, 0.9f })
                {
                    foreach (bool back in new[] { false, true })
                    {
                        Vector3 a = (back ? to : from) + Vector3.up * y;
                        int m = Physics.RaycastNonAlloc(new Ray(a, back ? -dir : dir), reachHits, len, mask, QueryTriggerInteraction.Ignore);
                        if (m >= reachHits.Length)
                        {
                            return "too many colliders on the way to judge";
                        }
                        for (int i = 0; i < m; i++)
                        {
                            if (Blocks(rb, reachHits[i].collider, reachHits[i].normal))
                            {
                                return $"{Describe(reachHits[i].collider, reachHits[i].point)}, {y:F2} above the ground{(back ? " (seen from the far end)" : "")}";
                            }
                        }
                    }
                }
                return null;
            }

            // His own collider placed at 'to' (as the game's PlayerColliderTester, but at the spot's height and against
            // every layer he collides with): a penetration deeper than 0.05 into anything but the floor under him.
            private static string BodyDoesNotFit(Rigidbody rb, Collider body, int mask, Vector3 to)
            {
                Vector3 shift = to - rb.position;
                Transform t = body.transform;
                Vector3 pos = t.position + shift;
                Bounds b = body.bounds;
                int n = Physics.OverlapBoxNonAlloc(b.center + shift, b.extents + Vector3.one * 0.05f, reachOverlaps, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                if (n >= reachOverlaps.Length)
                {
                    return $"too many colliders at {to:F2} to judge";
                }
                for (int i = 0; i < n; i++)
                {
                    Collider c = reachOverlaps[i];
                    if (c == null || !c || c == body || c.attachedRigidbody == rb || IsModView(c))
                    {
                        continue;
                    }
                    if (Physics.ComputePenetration(body, pos, t.rotation, c, c.transform.position, c.transform.rotation, out Vector3 outDir, out float depth)
                        && depth > 0.05f && Blocks(rb, c, outDir))
                    {
                        return $"his body doesn't fit at {to:F2}: inside {Describe(c, c.transform.position)} by {depth:F2}";
                    }
                }
                return null;
            }

            // The game's own walk grid: graph 3, the grid PlayerLocalAreaMovement rescans around the keeper against the
            // colliders before every walk it gives him (StartMovement, PlayerWorkComponent.FindNearestDockPoint), and whose
            // IsReachable is the game's "can he get there". Not while the game is walking him (it rescans for itself then).
            private static bool gridMissingLogged;

            private static string WhyNotOnGameGrid(PlayerController pc, Rigidbody rb, Vector3 to, bool rescan = true)
            {
                PlayerLocalAreaMovement lam = pc.PlayerLocalAreaMovement;
                Pathfinding.NavGraph[] graphs = AstarPath.active?.data?.graphs;
                if (lam == null || graphs == null || graphs.Length <= 3 || !(graphs[3] is Pathfinding.GridGraph grid))
                {
                    if (!gridMissingLogged)
                    {
                        gridMissingLogged = true;
                        Plugin.Log.LogInfo("Mount: the game's walk grid isn't there (no PlayerLocalAreaMovement or graph 3); the other tests judge moves of the keeper.");
                    }
                    return null;
                }
                if (lam.Status != PlayerLocalAreaMovement.MovementStatus.NotMoving)
                {
                    return "the game is walking the keeper";
                }
                try
                {
                    if (rescan)
                    {
                        lam.RescanPlayerGraph();
                    }
                    Pathfinding.NNInfo a = grid.GetNearest(rb.position, Pathfinding.NearestNodeConstraint.Walkable);
                    Pathfinding.NNInfo b = grid.GetNearest(to);
                    if (a.node == null)
                    {
                        return "the keeper isn't on the game's walk grid";
                    }
                    if (b.node == null || !b.node.Walkable || FlatOff(b.position, to) > 0.15f)
                    {
                        return $"the game's walk grid has no free cell at {to:F2}";
                    }
                    if (!Pathfinding.PathUtilities.IsPathPossible(a.node, b.node))
                    {
                        return $"the game's walk grid has no way from the keeper to {to:F2}";
                    }
                }
                catch (System.Exception e)
                {
                    return "the game's walk grid test failed: " + e.Message;
                }
                return null;
            }

            // The donkey's whole body standing at 'body' (its ground point under the middle): a box about its size, 0.9
            // long along the screen, 0.3 deep, from 0.1 to 0.9 up. Null when nothing the keeper is stopped by is in it,
            // else what is. FreeAt (one thin capsule at the middle) let a donkey stand half inside an ore 0.25 from it.
            internal static string FootprintBlocked(PlayerController pc, Vector3 body, Vector3 right)
            {
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                if (rb == null)
                {
                    return null;
                }
                int mask = KeeperMask(rb) & ~GroundLayers;
                Quaternion q = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
                int n = Physics.OverlapBoxNonAlloc(body + Vector3.up * 0.5f, new Vector3(0.45f, 0.4f, 0.15f), reachOverlaps, q, mask, QueryTriggerInteraction.Ignore);
                if (n >= reachOverlaps.Length)
                {
                    return "too many colliders to judge";
                }
                for (int i = 0; i < n; i++)
                {
                    Collider c = reachOverlaps[i];
                    if (Blocks(rb, c, Vector3.zero))
                    {
                        return Describe(c, c.transform.position);
                    }
                }
                return null;
            }

            private static bool IsModView(Collider c)
            {
                foreach (Instance i in All)
                {
                    if (i.donkey != null && i.donkey && c.transform.IsChildOf(i.donkey.transform))
                    {
                        return true;
                    }
                }
                return false;
            }

            // Null when 'to' is on the same connected part of the scene's walkable mesh as 'from', else why not. Judged only
            // where both lie on the mesh (within 0.25 seen from above): the nearest node of a point off it can be anywhere.
            internal static string WhyNotConnected(Vector3 from, Vector3 to)
            {
                Pathfinding.RecastGraph graph = MainGame.PlayerController?.SceneRecastGraph;
                if (graph == null)
                {
                    return null;
                }
                try
                {
                    Pathfinding.NearestNodeConstraint c = Pathfinding.NearestNodeConstraint.Walkable;
                    c.distanceMetric = Pathfinding.DistanceMetric.ClosestAsSeenFromAbove();
                    Pathfinding.NNInfo a = graph.GetNearest(from, c);
                    Pathfinding.NNInfo b = graph.GetNearest(to, c);
                    if (a.node == null || b.node == null || FlatOff(a.position, from) > 0.25f || FlatOff(b.position, to) > 0.25f)
                    {
                        return null;
                    }
                    if (!Pathfinding.PathUtilities.IsPathPossible(a.node, b.node))
                    {
                        return $"{to:F2} is cut off from {from:F2} (separate parts of the walkable mesh, areas {a.node.Area} and {b.node.Area})";
                    }
                }
                catch (System.Exception e)
                {
                    return "the walkable mesh test failed: " + e.Message;
                }
                return null;
            }

            private static float FlatOff(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

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
                // Judged against the keeper only while he is on it. A door takes him elsewhere first (SetPosition, then the
                // indoor light, in one call; the ride ends on the next frame with the donkey still on the doorstep): the
                // house's inside lies about 100 away and 6 lower in RuinedTemple, so nothing outside was "his ground" and
                // the last resort put the donkey, cart and all, onto his spot in the house and the church (the user's
                // screenshots, 2026-10-04). It stays where it walked under him; getting on later goes through the gate.
                if (FlatOff(keeper, Position) > MaxKeeperMove || Mathf.Abs(keeper.y - Position.y) > 2f * MaxRise)
                {
                    Plugin.Log.LogInfo($"Mount: {Describe()} stays where it stood at {Position:F2}: the keeper is not on it (at {keeper:F2}, a door).");
                    return;
                }
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
                // Ground alone let a donkey drawn into a rock stay there (2026-10-04, an ore in RuinedTemple): its body
                // must be free, on the keeper's side of any wall, and on his part of the walkable mesh too.
                string cutOff = null;
                if (bodyOk)
                {
                    string inside = FootprintBlocked(pc, body, right);
                    cutOff = inside != null ? "its body in " + inside : PlaceBlocked(pc, keeper, body) ?? WhyNotConnected(keeper, body);
                }
                if (bodyOk && cartOk && cutOff == null)
                {
                    return;
                }
                Vector3 to = keeper - bodyOffset; // the last resort: its body (its feet) on the keeper's spot, ground for sure
                string how = "onto the keeper's spot (no free ground next to him)";
                bool found = false;
                int rings = HasCart ? 12 : 8; // out to 3 units with a cart, 2 without
                bool gridScanned = false;
                for (int ri = 1; ri <= rings && !found; ri++)
                {
                    float r = ri * 0.25f;
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI / 6f;
                        Vector3 b = body + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        if (!OnNavmesh(b, out float gb) || Mathf.Abs(gb - keeper.y) > MaxRise + 0.6f * FlatOff(b, keeper))
                        {
                            continue; // no ground, or another level (a rock's top over a floor cut out of the navmesh)
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
                        if (FootprintBlocked(pc, b, right) != null || !FreeAt(rt, out _))
                        {
                            continue;
                        }
                        if (PlaceBlocked(pc, keeper, b) != null || WhyNotConnected(keeper, b) != null)
                        {
                            continue; // behind a wall from him: the nearest free spot can be in a closed-off pocket
                        }
                        // Where the keeper himself could stand: the game's walk grid around him (rescanned once per search;
                        // spots beyond it, about 2 units, are refused and the search goes on or ends at his spot).
                        Rigidbody krb = pc.PhysicalBody?.Rb;
                        if (krb != null)
                        {
                            if (WhyNotOnGameGrid(pc, krb, b, rescan: !gridScanned) != null)
                            {
                                gridScanned = true;
                                continue;
                            }
                            gridScanned = true;
                        }
                        // Ground all the way from him (LineWalkable's capsule also refused his own spot beside a fence,
                        // so every candidate failed there; the body's way above covers the colliders).
                        if (GroundGap(keeper, b) != null)
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
                string what = !bodyOk ? "off walkable ground" : cutOff != null ? $"where the keeper can't reach it ({cutOff})" : "with its cart off walkable ground";
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
