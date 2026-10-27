using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Where a flying keeper may come down (Flight.cs, docs/flying.md step 2): the safe-ground rules for a keeper put
    // somewhere (SafeGround.cs), at the spot under him. No way to it is tested: he comes straight down from above.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            // Null when the keeper may land under 'spot' (its x and z; its y the ground he comes down to, coming back as the
            // walkable mesh's there), else why not. The walk grid is asked only on a landing (rescan: the grid is rebuilt
            // around him), not on the 1 s safe-spot check. 'connectedTo' (the take-off spot): the landing must be on the
            // same connected part of the walkable mesh, so never on a rock top or a ledge the keeper can't walk off (the
            // user, 2026-10-22: a landing on a place the keeper never reaches went through its ground). That mesh is the
            // NPCs' (shrunk by their radius from everything, cut by every building), so it falls apart around a home: a
            // spot on another part of it is then judged by the keeper's own ground (WhyCannotWalkAway) when 'walkOut' is
            // set (a landing; the 1 s safe-spot check keeps the mesh's answer, cheap and never wrong when it says yes).
            // besideThings (a jump's touch-down, docs/flying.md "The jump probe: what the 19 jumps showed"): the NPCs' mesh is
            // shrunk back from every object by their radius, so beside a workbench the keeper stands where it isn't (its
            // nearest node 0.055-0.135 away in the user's yard: every jump sent back failed "no walkable ground", the
            // spot he jumped from in place included). Then the ground's raycast at his level counts, when the mesh's
            // nearest node is within BesideMesh on the take-off's own part. And a walk-grid cell "blocked" at the edge of
            // a thing with a free cell within BesideGrid, joined to the take-off's, moves the touch-down onto it (the
            // grid's rounding: the free cell 0.001-0.042 away).
            internal const float BesideMesh = 0.3f, BesideGrid = 0.1f;
            internal static string WhyCannotLandAt(PlayerController pc, ref Vector3 spot, bool grid, Vector3? connectedTo = null, bool walkOut = false, float walkBudgetMs = WalkSliceMs, bool besideThings = false)
            {
                using var perf = Perf.Measure(Perf.S.Landing);
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null) return "the keeper's body isn't there to test";
                if (!CanJudgeGround) return "this scene has no walkable mesh to judge the ground by";
                bool offMesh = false;
                if (!OnNavmesh(spot, out float g))
                {
                    float other = GroundHeight(spot, out string src);
                    if (float.IsNaN(other)) return $"no ground under him ({src ?? "nothing found"})";
                    string beside = besideThings && connectedTo.HasValue ? BesideTheMesh(spot, connectedTo.Value, other) : "not a jump's touch-down";
                    // Ground at his level the NPCs' mesh doesn't cover at all (a bridge's deck: its nearest node the ground
                    // under the bridge, 0.56-0.59 away and 0.5 lower, the user's jumps 2026-10-24): the keeper's own ground
                    // judges it, the walk-away test below, as for any spot the mesh can't vouch for.
                    if (beside != null && besideThings && connectedTo.HasValue && walkOut && Mathf.Abs(other - spot.y) <= MaxRise) offMesh = true;
                    else if (beside != null) return $"no walkable ground under him ({src}; beside the mesh: {beside})";
                    g = other;
                }
                if (Mathf.Abs(g - spot.y) > MaxRise)
                {
                    return $"the walkable mesh under him is at {g:F2}, the ground he comes down to at {spot.y:F2}";
                }
                // Never under the surface he comes down to (the user, 2026-10-24: "extremely important to not have any
                // possibility of us going underground"): a bridge's deck (6.3-6.5) lies over the NPCs' mesh (6.0 on the
                // ground under it), within MaxRise of it, and set to the mesh's height the body went under the deck.
                // The higher of the two: the surface found under the flight, or the mesh where it is the top.
                spot.y = Mathf.Max(g, spot.y);
                if (Flight.UseDrawnGround)
                {
                    // a higher level drawn over the spot: the walkable ground under a rock table, or under a ledge (the
                    // user's landing at 18.71, 6.00, 28.79 put the keeper inside the table, on the 6.0 ground it hides).
                    // The level's outermost tiles may hang over its cliff's foot: that many are allowed.
                    float over = DrawnGround.HighestAt(spot.x, spot.z, float.PositiveInfinity, out _);
                    if (!float.IsNaN(over) && over > g + MaxRise)
                    {
                        int depth = DrawnGround.TilesFromEdge(spot.x, spot.z, over, Flight.CoverEdgeTiles + 1);
                        if (depth >= Flight.CoverEdgeTiles) return $"a higher level is drawn over it ({over:F2}, {depth} tile(s) in from its edge): inside a rock or under a ledge";
                    }
                }
                bool backfaces = Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces = true;
                    string fit = BodyDoesNotFit(rb, body, KeeperMask(rb), spot);
                    if (fit != null) return fit;
                }
                catch (System.Exception e)
                {
                    return "the body test failed: " + e.Message;
                }
                finally
                {
                    Physics.queriesHitBackfaces = backfaces;
                }
                if (connectedTo.HasValue)
                {
                    string cut = offMesh ? "off the NPCs' mesh (ground at his level it doesn't cover)" : WhyNotConnected(connectedTo.Value, spot);
                    if (cut != null)
                    {
                        if (!walkOut) return cut;
                        string walled = WhyCannotWalkAway(pc, connectedTo.Value, spot, out string how, walkBudgetMs);
                        if (walled == WalkPending) return WalkPending; // still judging: asked again next frame
                        if (how != null) Plugin.Log.LogInfo($"[flight-walk] {spot:F2}: the NPCs' mesh says {cut}; the keeper's own ground: {walled ?? how}");
                        if (walled != null) return walled;
                    }
                }
                if (!grid) return null;
                if (!besideThings || !connectedTo.HasValue) return WhyNotOnGameGrid(pc, rb, spot);
                // The game's walk grid is 44 x 44 cells of 0.1 (4.4 x 4.4) round the keeper, rebuilt at each ask
                // (PlayerLocalAreaMovement.RescanPlayerGraph): at a touch-down 2 from the take-off its way round a
                // workbench may not fit in it ("no way" four times over in the user's yard, 2026-10-24, the NPCs' mesh
                // saying both on part 1). Where that mesh vouches for the spot (on it, the take-off's part) its yes
                // stands (never wrong when it says yes) and the grid is asked only for a free cell.
                int spotPart = MeshAreaAt(spot, out _, out _), takePart = MeshAreaAt(connectedTo.Value, out _, out _);
                bool meshVouches = spotPart >= 0 && spotPart == takePart;
                string onGrid = WhyNotOnGameGridBeside(pc, rb, ref spot, connectedTo.Value, needWay: !meshVouches);
                if (onGrid != null) return onGrid;
                // moved onto the free cell: the body there too
                bool bf = Physics.queriesHitBackfaces;
                try { Physics.queriesHitBackfaces = true; return BodyDoesNotFit(rb, body, KeeperMask(rb), spot); }
                finally { Physics.queriesHitBackfaces = bf; }
            }

            // Ground beside the NPCs' mesh: null when the raycast's ground at 'groundY' is at the spot's level and the mesh's
            // nearest node is within BesideMesh on the take-off's part; else why not.
            private static string BesideTheMesh(Vector3 spot, Vector3 takeOff, float groundY)
            {
                if (Mathf.Abs(groundY - spot.y) > MaxRise) return $"the ground at {groundY:F2}, the spot at {spot.y:F2}";
                Pathfinding.RecastGraph graph = MainGame.PlayerController?.SceneRecastGraph;
                if (graph == null) return "no mesh";
                Pathfinding.NearestNodeConstraint c = Pathfinding.NearestNodeConstraint.Walkable;
                c.distanceMetric = Pathfinding.DistanceMetric.ClosestAsSeenFromAbove();
                Pathfinding.NNInfo a = graph.GetNearest(spot, c), t = graph.GetNearest(takeOff, c);
                if (a.node == null || t.node == null) return "no node near";
                float off = FlatOff(a.position, spot);
                if (off > BesideMesh) return $"its nearest node {off:F2} away";
                if (Mathf.Abs(a.position.y - groundY) > MaxRise) return $"its nearest node at {a.position.y:F2}, another level";
                if (a.node.Area != t.node.Area) return $"its nearest node on another part ({a.node.Area}, the take-off's {t.node.Area})";
                return null;
            }

            // The walk-away test (docs/flying.md "Refused landings: cut off from the take-off spot"): from the landing spot,
            // over the keeper's own ground by the game's own rules, can he walk away? Null when he can, else why not; 'how'
            // says what was found (null when the answer was kept from before). The rules, read from the game:
            //   - ground: PlayerInvisibleWalls raises an edge wall wherever a ray 0.6 down from 0.05 over his feet finds no
            //     ground layer (11, 12): he walks where ground lies under him less than 0.55 below his level; up, as far as
            //     the shuffle's measured step (MaxRisePerSample 0.45: a dropped corpse's top, slopes; a level is 1.2);
            //   - his body: PlayerColliderTester's penetration test, his own collider deeper than 0.05 into nothing
            //     (BodyDoesNotFit, against every layer he collides with).
            // A spread over a grid of WalkStep from the spot, breadth first. Yes when a cell lies on the take-off's part of
            // the mesh at its own height (that part is the NPCs', narrower than his: a cell on it is joined to the take-off
            // spot; near the take-off it is found in a few hundred cells), or when his ground from the spot reaches
            // WalkOpenCells (60 square units: open ground, no pocket; a take-off spot on another level far away is never
            // reached by a short spread: the user's home refused from a plateau 35 away, 2026-10-23). No when the spread ends
            // first: a rock top, a ledge (one seen: 69 cells), a closed pocket. Being a stranded keeper there is no worse than
            // a walk he can make: the ground is his, and the mount lands with him.
            // On the main thread (physics queries), so sliced: WalkSliceMs a frame, the landing hovering meanwhile
            // (WalkPending); all at once only for a forced landing (budget 0). The first build ran 6000 cells in one frame,
            // 280-330 ms, a freeze on every Land pressed over open ground (the user, 2026-10-23). Answers kept 5 s per spot.
            internal const string WalkPending = "judging the ground (the walk-away test)";
            internal const float WalkSliceMs = 4f;
            private const float WalkStep = 0.25f, WalkDrop = 0.55f, WalkClimb = 0.45f;
            private const int WalkOpenCells = 1000;
            private static readonly Dictionary<long, (string why, string how, float at)> walkKept = new Dictionary<long, (string, string, float)>();
            private static WalkJob walkJob;

            private static long WalkKey(int x, int z) => ((long)x << 32) ^ (uint)z;

            internal static string WhyCannotWalkAway(PlayerController pc, Vector3 takeOff, Vector3 spot, out string how, float budgetMs = WalkSliceMs)
            {
                how = "not judged";
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null) return null;
                int target = MeshAreaAt(takeOff, out _, out _);
                long kept = WalkKey(Mathf.RoundToInt(spot.x / WalkStep), Mathf.RoundToInt(spot.z / WalkStep)) ^ ((long)target << 48) ^ ((long)Mathf.RoundToInt(spot.y / 0.6f) << 40);
                if (walkKept.TryGetValue(kept, out var k) && Time.unscaledTime - k.at < 5f) { how = null; return k.why; }
                try
                {
                    // a job under way goes on while the spot stays within a unit of where it began, on the same level:
                    // steering while it judged, the spot moved a cell every few frames and a new job began each time
                    // (never done: the landing hovered on), the answer standing for the spot it was asked about
                    bool same = walkJob != null && !walkJob.Done && walkJob.Target == target && FlatOff(walkJob.Spot, spot) < 1f && Mathf.Abs(walkJob.Spot.y - spot.y) < 0.3f;
                    if (!same) walkJob = new WalkJob(kept, rb, body, spot, target);
                    walkJob.Run(budgetMs);
                }
                catch (System.Exception e)
                {
                    walkJob = null;
                    how = "the walk-away test failed (" + e.Message + "): the mesh's answer stands";
                    return WhyNotConnected(takeOff, spot);
                }
                if (!walkJob.Done) return WalkPending;
                how = walkJob.How;
                string why = walkJob.Why;
                walkJob = null;
                if (walkKept.Count > 256) walkKept.Clear();
                walkKept[kept] = (why, how, Time.unscaledTime);
                return why;
            }

            // The spread at once (the self-test): 'target' a part no cell is on judges by the ground's size alone.
            internal static string WalkAway(Rigidbody rb, Collider body, Vector3 spot, int target, out string how)
            {
                walkJob = null; // the cells are shared: a landing's job under way starts again
                var job = new WalkJob(0, rb, body, spot, target);
                job.Run(0f);
                how = job.How;
                return job.Why;
            }

            // The same in slices of 'budgetMs' (the self-test: sliced and at once must agree); 'slices' how many it took.
            internal static string WalkAwaySliced(Rigidbody rb, Collider body, Vector3 spot, int target, float budgetMs, out string how, out int slices)
            {
                walkJob = null;
                var job = new WalkJob(0, rb, body, spot, target);
                slices = 0;
                while (!job.Done && slices < 100000) { job.Run(budgetMs); slices++; }
                how = job.How;
                return job.Why;
            }

            // one job at a time (walkJob), so its cells are shared and kept: no 100 KB of new collections per landing spot
            private static readonly Dictionary<long, bool> walkSeen = new Dictionary<long, bool>();
            private static readonly Queue<(int x, int z, float y)> walkQueue = new Queue<(int, int, float)>();

            private sealed class WalkJob
            {
                internal readonly long Key;
                internal readonly Vector3 Spot;
                internal readonly int Target;
                internal bool Done;
                internal string Why, How;
                private readonly Rigidbody rb;
                private readonly Collider body;
                private readonly int mask;
                private int n, frames;
                private float far;
                private double ms;
                private bool started;
                private Dictionary<long, bool> seen => walkSeen;
                private Queue<(int x, int z, float y)> queue => walkQueue;
                private Vector3 spot => Spot;
                private int target => Target;

                internal WalkJob(long key, Rigidbody rb, Collider body, Vector3 spot, int target)
                {
                    Key = key; this.rb = rb; this.body = body; Spot = spot; Target = target;
                    // every layer he collides with but the edge walls (layer 14, as the flight's own sweeps): the game may
                    // raise them round his body under the flight, beside the spot; the edge rule is GroundStep's
                    mask = KeeperMask(rb) & ~(1 << 14);
                    walkSeen.Clear();
                    walkQueue.Clear();
                }

                // On until done or 'budgetMs' spent (0: to the end).
                internal void Run(float budgetMs)
                {
                    if (Done) return;
                    frames++;
                    var watch = new Clock(System.Diagnostics.Stopwatch.GetTimestamp());
                    bool backfaces = Physics.queriesHitBackfaces;
                    try
                    {
                        Physics.queriesHitBackfaces = true;
                        if (!started)
                        {
                            started = true;
                            if (!GroundStep(spot, spot.y, out float g0)) { Finish("no ground of the keeper's under the spot", watch); return; }
                            queue.Enqueue((0, 0, g0));
                            seen[WalkKey(0, 0)] = true;
                        }
                        while (queue.Count > 0)
                        {
                            if (budgetMs > 0f && watch.Ms >= budgetMs) { ms += watch.Ms; return; }
                            var c = queue.Dequeue();
                            n++;
                            Vector3 p = new Vector3(spot.x + c.x * WalkStep, c.y, spot.z + c.z * WalkStep);
                            far = Mathf.Max(far, FlatOff(p, spot));
                            // on the take-off's part, at this cell's own height: seen from above, a rock top with no mesh of its
                            // own finds the ground's mesh under it (the same part), and would pass at its first cell
                            // (at the game's own limits: no lower than he steps down, no higher than he climbs)
                            if (target >= 0 && MeshAreaAt(p, out _, out Vector3 node) == target && node.y >= p.y - WalkDrop && node.y <= p.y + WalkClimb)
                            {
                                Finish(null, watch, $"walks to the take-off's part of the mesh at {p:F2}, {far:0.0} from the spot");
                                return;
                            }
                            if (n >= WalkOpenCells)
                            {
                                Finish(null, watch, $"open ground: {WalkOpenCells} steps of it, {far:0.0} out from the spot");
                                return;
                            }
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = c.x + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = c.z + (d == 2 ? 1 : d == 3 ? -1 : 0);
                                long key = WalkKey(nx, nz);
                                if (seen.ContainsKey(key)) continue;
                                Vector3 q = new Vector3(spot.x + nx * WalkStep, c.y, spot.z + nz * WalkStep);
                                if (!GroundStep(q, c.y, out float gy)) continue; // an edge: tried again from another side
                                q.y = gy;
                                if (BodyDoesNotFit(rb, body, mask, q) != null) { seen[key] = false; continue; }
                                seen[key] = true;
                                queue.Enqueue((nx, nz, gy));
                            }
                        }
                        Finish($"walled in: the keeper's ground from here is {n} steps, {far:0.0} out at most: a rock top, a ledge or a closed pocket", watch);
                    }
                    finally
                    {
                        Physics.queriesHitBackfaces = backfaces;
                    }
                }

                private void Finish(string why, Clock watch, string how = null)
                {
                    ms += watch.Ms;
                    Done = true;
                    Why = why;
                    How = $"{why ?? how} ({n} steps, {ms:0.0} ms over {frames} frame(s))";
                }
            }

            // A stopwatch without the allocation (Stopwatch.StartNew is a class, one per slice).
            private readonly struct Clock
            {
                private readonly long start;
                internal Clock(long start) { this.start = start; }
                internal double Ms => (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }

            // The game's edge rule at 'p' coming from ground at 'fromY': the highest ground layer under it from a climb's
            // height above, no higher than a climb and no lower than the edge walls' drop; never a lake's bed (the ground
            // raycast goes through the water to it: WaterOver, as GroundHeight; a shore walked into the lake and across).
            private static bool GroundStep(Vector3 p, float fromY, out float y)
            {
                y = float.NaN;
                float top = fromY + WalkClimb + 0.05f;
                Ray ray = new Ray(new Vector3(p.x, top, p.z), Vector3.down);
                if (!Physics.Raycast(ray, out RaycastHit hit, WalkClimb + 0.05f + WalkDrop, GroundLayers, QueryTriggerInteraction.Ignore)) return false;
                if (hit.point.y > fromY + WalkClimb || hit.point.y < fromY - WalkDrop) return false;
                if (WaterOver(ray, hit.point.y, out _)) return false;
                y = hit.point.y;
                return true;
            }

            // The flying body's move this physics step, against what stands taller than the flight (the user, 2026-10-22:
            // "now I'm going through everything ... buildings and trees"). Two sweeps along the move, the nearest hit of
            // either stops it (and the flight slides along it, once):
            //   1. the game's own see-through shapes (layer 15 TRANSPARENCY_OCCLUDER: MeshColliders that are the drawn
            //      meshes of everything tall enough to hide the keeper, trees, houses, big props; Object3DTransparency-
            //      Occluder casts the keeper as a 1.4 tall, 0.24 wide capsule against them), the flying keeper's capsule
            //      at the flight's height: he meets a crown or a wall where it is drawn, and passes over a roof he is above;
            //   2. what his body collides with (the obstacles' footprints, fences, rocks), from the ground under the flight
            //      up: a hit counts only when what is DRAWN there reaches above the flight's feet ('flightY'): the meshes of
            //      its Wgo, or for the scene's own pieces (rocks, cliff walls, ruins: no Wgo) the nearest renderers around
            //      the collider that overlap it from above. Never the collider's own top: the survey (2026-10-22, 198
            //      obstacles at the Inquisition base) found them extruded to a fixed 2 (135 of them), 3 or 1.5 whatever is
            //      drawn, so every rock stopped a flight below 2 units. A collider with nothing drawn over it doesn't stop.
            //      Ground layers only where the hit itself reaches that height (a cliff face), never by their bounds.
            // The world's objects are real 3D meshes (Object3D), so their bounds are their drawn heights (docs/flying.md).
            private const int OccluderLayers = 1 << 15;
            private static readonly RaycastHit[] flyHits = new RaycastHit[32];
            private static readonly Dictionary<int, float> pictureTop = new Dictionary<int, float>();
            internal static string LastBlocker;

            // colliderToo: an obstacle reaches the higher of its picture's top and its collider's (a jump: an undrawn
            // collider, the game's invisible walls, blocks it; free flight goes over what isn't drawn)
            // ignoreThing: a jump carried off a thing (a workbench: a box, a capsule, its picture higher than both) is not
            // blocked by that same thing's colliders (the user's yard, 2026-10-24: every blocker while carried off was the
            // bench it was over; one carry stuck 0.165 from the edge until it was sent back)
            internal static Vector3 FlyMove(PlayerController pc, Vector3 from, Vector3 move, float flightY, bool colliderToo = false, Collider ignoreThing = null)
            {
                LastBlocker = null;
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null || move.sqrMagnitude < 1e-8f) return move;
                Vector3 done = Vector3.zero;
                for (int pass = 0; pass < 2 && move.sqrMagnitude > 1e-8f; pass++)
                {
                    float dist = move.magnitude;
                    Vector3 dir = move / dist;
                    bool a = Nearest(rb, body, from + done, dir, dist, flightY, colliderToo, ignoreThing, out RaycastHit hit, out string what);
                    if (Shapes(from + done, dir, dist, flightY, out RaycastHit sh, out string shWhat) && (!a || sh.distance < hit.distance))
                    {
                        a = true; hit = sh; what = shWhat;
                    }
                    if (!a)
                    {
                        return done + move;
                    }
                    LastBlocker = what;
                    float go = Mathf.Max(0f, hit.distance - 0.02f);
                    done += dir * go;
                    Vector3 n = hit.normal; n.y = 0f;
                    if (n.sqrMagnitude < 1e-6f) break;
                    n.Normalize();
                    Vector3 rest = dir * (dist - go);
                    move = rest - n * Vector3.Dot(rest, n); // slide along it
                }
                return done;
            }

            private static bool Nearest(Rigidbody rb, Collider body, Vector3 at, Vector3 dir, float dist, float flightY, bool colliderToo, Collider ignoreThing, out RaycastHit best, out string what)
            {
                best = default; what = null;
                Bounds b = body.bounds;
                float r = Mathf.Max(0.1f, Mathf.Min(b.extents.x, b.extents.z));
                float bottom = at.y + r + 0.05f, top = Mathf.Max(bottom, flightY + b.size.y - r);
                int mask = KeeperMask(rb) & ~(1 << rb.gameObject.layer) & ~(1 << 14); // not himself, not the edge walls
                int n = Physics.CapsuleCastNonAlloc(new Vector3(at.x, bottom, at.z), new Vector3(at.x, top, at.z), r, dir, flyHits, dist + 0.02f, mask, QueryTriggerInteraction.Ignore);
                bool found = false;
                for (int i = 0; i < n; i++)
                {
                    RaycastHit h = flyHits[i];
                    Collider c = h.collider;
                    if (h.distance <= 0f || !Blocks(rb, c, h.normal)) continue; // inside it already at the start: let him out
                    if (ignoreThing != null && SameThing(c, ignoreThing)) continue;
                    bool ground = ((1 << c.gameObject.layer) & GroundLayers) != 0;
                    float reach = ground ? h.point.y : colliderToo ? Mathf.Max(ObjectTop(c, out _), c.bounds.max.y) : ObjectTop(c, out _);
                    if (reach <= flightY) continue; // under the flight (or nothing drawn there)
                    if (!found || h.distance < best.distance)
                    {
                        best = h; found = true;
                        what = $"{c.name} (layer {LayerMask.LayerToName(c.gameObject.layer)}, {(ground ? "ground face at" : "drawn up to")} {reach:0.00}, its collider {c.bounds.max.y:0.00}; {Where(c)}) above the flight at {flightY:0.00}";
                    }
                }
                return found;
            }

            // What the flight may come down to under 'at' from 'alt' (the mount's feet): the highest of the ground's
            // surface, the see-through shapes' (drawn) surfaces and the drawn tops of the obstacles under it. The floor
            // is the clear height above this, so hovering over a cliff's rim or a rock the mount stays on top of what is
            // drawn there (the user, 2026-10-22: hovering over a cliff after a refused landing, the mount was inside the
            // rim's rocks: the floor counted the ground only). NaN: nothing under it (water).
            private static readonly RaycastHit[] downHits = new RaycastHit[32];

            // r: the cast's width; a jump asks with a thin one (straight under the body): the 0.24 ball touched a potter's
            // wheel beside a jump pressed against it and called it "on top of something 21 px up" (the user, 2026-10-24)
            internal static Collider LastFloorThing;

            // Two colliders of one thing: the same collider, the same game object (its Wgo), the same transform, or two
            // children of one parent (a workbench's Obstacle, Obstacle (3), Obstacle (5) under its Base).
            internal static bool SameThing(Collider a, Collider b)
            {
                if (a == b) return true;
                if (a == null || b == null) return false;
                if (a.transform == b.transform) return true;
                Transform pa = a.transform.parent;
                if (pa != null && pa == b.transform.parent) return true;
                Wgo wa = a.GetComponentInParent<Wgo>();
                return wa != null && wa == b.GetComponentInParent<Wgo>();
            } // the thing (not the ground) the last FloorUnder's answer came from, or null
            internal static float FloorUnder(PlayerController pc, Vector3 at, float alt, float r = 0.24f)
            {
                LastFloorThing = null;
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                if (rb == null) return float.NaN;
                float from = alt + 0.3f;
                int mask = (KeeperMask(rb) & ~(1 << rb.gameObject.layer) & ~(1 << 14)) | OccluderLayers;
                int n = Physics.SphereCastNonAlloc(new Vector3(at.x, from, at.z), r, Vector3.down, downHits, from - at.y + 30f, mask, QueryTriggerInteraction.Ignore);
                float best = float.NaN;
                for (int i = 0; i < n; i++)
                {
                    RaycastHit h = downHits[i];
                    Collider c = h.collider;
                    if (c == null || h.distance <= 0f || IsModView(c)) continue;
                    int bit = 1 << c.gameObject.layer;
                    float y;
                    if ((bit & GroundLayers) != 0 || (bit & OccluderLayers) != 0) y = h.point.y;
                    else if (Blocks(rb, c, Vector3.up)) y = Mathf.Max(h.point.y, ObjectTop(c, out _));
                    else continue;
                    // Only what is under the mount's feet: a thing drawn higher than them (a tree whose trunk the cast met
                    // while the mount flew low past its crown) is beside or around the flight, not a floor; taken as one, it
                    // lifted the flight 134 px in one frame to the tree's top (the log, 2026-10-23: "drawn things up to
                    // 9.77, now 185 px up" over ground at 3.6). The bumping keeps the flight out of such things sideways.
                    if (y > alt + 0.05f) continue;
                    if (float.IsNaN(best) || y > best) { best = y; LastFloorThing = (bit & GroundLayers) != 0 ? null : c; }
                }
                return best;
            }

            // Sweep 1: the keeper's see-through capsule (1.4 tall, 0.24 wide, as the game casts it) at the flight's feet.
            private static bool Shapes(Vector3 at, Vector3 dir, float dist, float flightY, out RaycastHit best, out string what)
            {
                best = default; what = null;
                const float r = 0.24f;
                Vector3 p1 = new Vector3(at.x, flightY + r, at.z), p2 = new Vector3(at.x, flightY + 1.4f - r, at.z);
                bool backfaces = Physics.queriesHitBackfaces;
                int n;
                try
                {
                    Physics.queriesHitBackfaces = true;
                    n = Physics.CapsuleCastNonAlloc(p1, p2, r, dir, flyHits, dist + 0.02f, OccluderLayers, QueryTriggerInteraction.Collide);
                }
                finally
                {
                    Physics.queriesHitBackfaces = backfaces;
                }
                bool found = false;
                for (int i = 0; i < n; i++)
                {
                    RaycastHit h = flyHits[i];
                    if (h.distance <= 0f || h.collider == null || IsModView(h.collider)) continue;
                    if (!found || h.distance < best.distance)
                    {
                        best = h; found = true;
                        Collider c = h.collider;
                        what = $"{c.name} (the game's see-through shape, top {c.bounds.max.y:0.00}) at the flight's height {flightY:0.00}";
                    }
                }
                return found;
            }

            // The top of what is drawn over a collider: its Wgo's Object3D meshes (every part); for the scene's own pieces
            // (no Wgo) the renderers under the nearest of its parents (up to five levels) that has any overlapping the
            // collider seen from above. Shadow-only meshes (*_sh, *-sh), water surfaces (*-ws), blackouts, particles and
            // the scene's huge pieces (over 12 across) left out. Nothing found: -infinity. Cached per collider.
            internal static float ObjectTop(Collider c, out Wgo wgo)
            {
                wgo = c != null ? c.GetComponentInParent<Wgo>() : null;
                int id = c.GetInstanceID();
                if (pictureTop.TryGetValue(id, out float y)) return y;
                y = float.NegativeInfinity;
                if (wgo != null)
                {
                    y = Math.Max(y, PartTop(wgo.MainWgoPart));
                    if (wgo.AdditionalWgoParts != null) foreach (WgoPart part in wgo.AdditionalWgoParts) y = Math.Max(y, PartTop(part));
                }
                if (float.IsNegativeInfinity(y)) y = DrawnOver(c);
                if (pictureTop.Count > 4000) pictureTop.Clear();
                pictureTop[id] = y;
                return y;
            }

            private static float DrawnOver(Collider c)
            {
                Bounds cb = c.bounds;
                cb.Expand(new Vector3(0.1f, 0f, 0.1f));
                Transform t = c.transform;
                for (int level = 0; level < 5 && t.parent != null; level++)
                {
                    t = t.parent;
                    Renderer[] all = t.GetComponentsInChildren<Renderer>(false);
                    if (all.Length > 600) break; // a whole layer of the scene: too far up
                    float y = float.NegativeInfinity;
                    foreach (Renderer rd in all)
                    {
                        if (!Drawn(rd)) continue;
                        Bounds b = rd.bounds;
                        if (b.size.x > 12f || b.size.z > 12f) continue;
                        if (b.max.x < cb.min.x || b.min.x > cb.max.x || b.max.z < cb.min.z || b.min.z > cb.max.z) continue;
                        y = Math.Max(y, b.max.y);
                    }
                    if (!float.IsNegativeInfinity(y)) return y;
                }
                return float.NegativeInfinity;
            }

            private static bool Drawn(Renderer rd)
            {
                if (rd == null || !rd.enabled || rd is ParticleSystemRenderer || !rd.gameObject.activeInHierarchy || rd.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return false;
                string nm = rd.name;
                return !(nm.EndsWith("_sh") || nm.Contains("_sh2") || nm.EndsWith("-sh") || nm.EndsWith("-ws") || nm.Contains("blackout"));
            }

            private static string Where(Collider c)
            {
                Transform t = c.transform;
                string path = t.name;
                for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; path = t.name + "/" + path; }
                return path;
            }

            private static float PartTop(WgoPart part)
            {
                float y = float.NegativeInfinity;
                var meshes = part?.Object3D?.Object3DMeshes;
                if (meshes == null) return y;
                foreach (Object3DMesh m in meshes)
                {
                    Renderer rd = m != null ? m.ObjRenderer : null;
                    if (!Drawn(rd)) continue;
                    y = Math.Max(y, rd.bounds.max.y);
                }
                return y;
            }
        }
    }
}
