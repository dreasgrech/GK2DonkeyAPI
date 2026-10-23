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
            // user, 2026-10-22: a landing on a place the keeper never reaches went through its ground).
            internal static string WhyCannotLandAt(PlayerController pc, ref Vector3 spot, bool grid, Vector3? connectedTo = null)
            {
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null) return "the keeper's body isn't there to test";
                if (!CanJudgeGround) return "this scene has no walkable mesh to judge the ground by";
                if (!OnNavmesh(spot, out float g))
                {
                    float other = GroundHeight(spot, out string src);
                    return float.IsNaN(other) ? $"no ground under him ({src ?? "nothing found"})" : $"no walkable ground under him ({src})";
                }
                if (Mathf.Abs(g - spot.y) > MaxRise)
                {
                    return $"the walkable mesh under him is at {g:F2}, the ground he comes down to at {spot.y:F2}";
                }
                spot.y = g;
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
                if (connectedTo.HasValue)
                {
                    string cut = WhyNotConnected(connectedTo.Value, spot);
                    if (cut != null) return cut;
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
                return grid ? WhyNotOnGameGrid(pc, rb, spot) : null;
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

            internal static Vector3 FlyMove(PlayerController pc, Vector3 from, Vector3 move, float flightY)
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
                    bool a = Nearest(rb, body, from + done, dir, dist, flightY, out RaycastHit hit, out string what);
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

            private static bool Nearest(Rigidbody rb, Collider body, Vector3 at, Vector3 dir, float dist, float flightY, out RaycastHit best, out string what)
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
                    bool ground = ((1 << c.gameObject.layer) & GroundLayers) != 0;
                    float reach = ground ? h.point.y : ObjectTop(c, out _);
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
            // drawn there (the user, 2026-10-22: hovering over a cliff after a refused landing, the dragon was inside the
            // rim's rocks: the floor counted the ground only). NaN: nothing under it (water).
            private static readonly RaycastHit[] downHits = new RaycastHit[32];

            internal static float FloorUnder(PlayerController pc, Vector3 at, float alt)
            {
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                if (rb == null) return float.NaN;
                const float r = 0.24f;
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
                    if (float.IsNaN(best) || y > best) best = y;
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
