using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The jump probe (the user, 2026-10-24: "lets get as much data as possible no guessing"; docs/flying.md "The jump
    // probe"). Every jump is traced, frame by frame and physics step by physics step, into a file of its own under
    // BepInEx/GK2-jumps/ (the newest 120 kept); every touch-down judgement writes the landing audit: each rule of
    // WhyCannotLandAt asked on its own, the game's own walk grid asked from the take-off spot and from the body, and
    // every collider near the spot with its shape and heights. The file's name says how the jump ended. No behaviour of
    // the jump changes. Dev only (flying is).
    internal static class JumpTrace
    {
        // never for a player, whatever flying becomes (the files, the strings per frame); for the dev only with
        // Debug.JumpTrace on (off by default since 2026-10-27: it wrote a file per jump on every dev session)
        internal static bool On => DevTools.Unlocked && Plugin.JumpTraceOn != null && Plugin.JumpTraceOn.Value;
        private static readonly StringBuilder sb = new StringBuilder(64 * 1024);
        private static bool open;
        private static int seq, rows;
        private static float t0;
        private static string outcome;

        internal static string Folder => Path.Combine(Paths.BepInExRootPath, "GK2-jumps");

        internal static void Begin(string header)
        {
            if (!On) return;
            sb.Length = 0;
            open = true; rows = 0; outcome = "ok";
            t0 = Time.time;
            seq++;
            sb.Append("# jump ").Append(seq).Append(" at ").Append(System.DateTime.Now.ToString("HH:mm:ss.fff")).Append('\n');
            sb.Append("# ").Append(header).Append('\n');
            sb.Append("# F = a frame (the jump's tick): t;frame;stage;jumpT;arc;height;solid;floor_thing;body_x;body_y;body_z;vel_x;vel_z;free;let_go\n");
            sb.Append("# S = a physics step (the body's move): t;frame;stage;stick_x;stick_y;want_x;want_z;intended_x;intended_z;after_bump_x;after_bump_z;blocker;after_wall_x;after_wall_z;next_x;next_y;next_z;moved\n");
            sb.Append("# N = an event (a judgement, a push, a carry, a way back) and the landing audits under it\n");
        }

        internal static void Outcome(string o) { if (open && (outcome == "ok" || o == "back" || o == "flight")) outcome = o; }

        internal static void Frame(string line)
        {
            if (!open) return;
            if (++rows > 4000) return;
            sb.Append("F;").Append((Time.time - t0).ToString("0.000")).Append(';').Append(Time.frameCount).Append(';').Append(line).Append('\n');
        }

        internal static void Step(string line)
        {
            if (!open) return;
            if (++rows > 4000) return;
            sb.Append("S;").Append((Time.time - t0).ToString("0.000")).Append(';').Append(Time.frameCount).Append(';').Append(line).Append('\n');
        }

        internal static void Note(string text)
        {
            if (!open) return;
            sb.Append("N;").Append((Time.time - t0).ToString("0.000")).Append(';').Append(Time.frameCount).Append(';').Append(text.Replace("\n", "\n    ")).Append('\n');
        }

        internal static void End(string how)
        {
            if (!open) return;
            open = false;
            sb.Append("# ended: ").Append(how).Append('\n');
            try
            {
                Directory.CreateDirectory(Folder);
                string name = $"jump-{System.DateTime.Now:yyyyMMdd-HHmmss}-{seq:000}-{outcome}.txt";
                File.WriteAllText(Path.Combine(Folder, name), sb.ToString());
                var files = new List<string>(Directory.GetFiles(Folder, "jump-*.txt"));
                files.Sort(System.StringComparer.Ordinal);
                for (int k = 0; k < files.Count - 120; k++) File.Delete(files[k]); // 120: a test run makes 30+ and swept the hand jumps away (2026-10-24)
                Plugin.Log.LogInfo($"[jump-trace] {name} ({outcome}, {rows} rows)");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[jump-trace] could not write the trace: " + e.Message);
            }
            sb.Length = 0;
        }

        internal static string V(Vector3 v) => $"{v.x:0.000};{v.y:0.000};{v.z:0.000}";
        internal static string V2(Vector3 v) => $"{v.x:0.000};{v.z:0.000}";
    }

    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            private static readonly Collider[] auditNear = new Collider[128];

            // The landing audit: every rule of WhyCannotLandAt on its own (not the first that fails), and the game's own
            // answers, for the spot a jump comes down to.
            internal static string LandAudit(PlayerController pc, Vector3 spot, Vector3 takeOff)
            {
                var a = new StringBuilder();
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null) return "audit: no keeper's body";
                a.Append($"audit at {spot:F3} (take-off {takeOff:F3}, the body now {rb.position:F3}, its collider {body.GetType().Name} bounds {body.bounds.min:F3}..{body.bounds.max:F3})\n");
                // 1. the ground: the navmesh, the raycast
                float gy = GroundHeight(spot, out string src);
                a.Append($"  1 ground: {(float.IsNaN(gy) ? "none" : gy.ToString("F3"))} from {src ?? "nothing"} (the rule wants the navmesh); dy to the spot {(float.IsNaN(gy) ? float.NaN : gy - spot.y):F3}\n");
                int area = MeshAreaAt(spot, out float off, out Vector3 node), area0 = MeshAreaAt(takeOff, out float off0, out _);
                a.Append($"  2 walkable mesh: the spot's part {area} (its nearest node {node:F3}, {off:F3} off seen from above), the take-off's part {area0} ({off0:F3} off)\n");
                if (Flight.UseDrawnGround)
                {
                    float over = DrawnGround.HighestAt(spot.x, spot.z, float.PositiveInfinity, out _);
                    a.Append($"  3 drawn ground over it: {(float.IsNaN(over) ? "none" : over.ToString("F3"))}\n");
                }
                // 4. the body: every collider it goes into, not just the first
                Vector3 shift = spot - rb.position;
                Transform t = body.transform;
                Vector3 pos = t.position + shift;
                Bounds b = body.bounds;
                bool backfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                try
                {
                    int n = Physics.OverlapBoxNonAlloc(b.center + shift, b.extents + Vector3.one * 0.05f, auditNear, Quaternion.identity, KeeperMask(rb), QueryTriggerInteraction.Ignore);
                    int into = 0;
                    for (int i = 0; i < n && i < auditNear.Length; i++)
                    {
                        Collider c = auditNear[i];
                        if (c == null || !c || c == body || c.attachedRigidbody == rb) continue;
                        bool pen = Physics.ComputePenetration(body, pos, t.rotation, c, c.transform.position, c.transform.rotation, out Vector3 dir, out float depth);
                        if (!pen) continue;
                        into++;
                        bool groundUnder = ((1 << c.gameObject.layer) & GroundLayers) != 0 && c.bounds.max.y <= spot.y + 0.05f;
                        a.Append($"  4 body into {PathOf(c)} (layer {LayerMask.LayerToName(c.gameObject.layer)}, {c.GetType().Name}) by {depth:F3} out {dir:F3}; counts: {(depth > 0.05f && !groundUnder && Blocks(rb, c, dir) ? "YES" : "no")}{(groundUnder ? " (ground under him)" : "")}\n");
                    }
                    if (into == 0) a.Append("  4 body: into nothing\n");
                }
                catch (System.Exception e) { a.Append("  4 body test failed: " + e.Message + "\n"); }
                finally { Physics.queriesHitBackfaces = backfaces; }
                // 5. connected by the NPCs' mesh, 6. the walk-away test
                string cut = WhyNotConnected(takeOff, spot);
                a.Append($"  5 the NPCs' mesh: {cut ?? "connected"}\n");
                // asked only when the mesh says cut off, as the rule does (at once it spreads up to the open-ground size:
                // asked on every touch-down it would be a hitch on every jump)
                if (cut != null)
                {
                    string walked = WhyCannotWalkAway(pc, takeOff, spot, out string how, 0f);
                    a.Append($"  6 walk-away: {walked ?? "yes"} ({how ?? "kept from before"})\n");
                }
                else a.Append("  6 walk-away: not asked (connected)\n");
                // 7. the game's walk grid: from the body (the rule's), and from the take-off spot (the game's own question)
                try
                {
                    PlayerLocalAreaMovement lam = pc.PlayerLocalAreaMovement;
                    Pathfinding.NavGraph[] graphs = AstarPath.active?.data?.graphs;
                    if (lam == null || graphs == null || graphs.Length <= 3 || !(graphs[3] is Pathfinding.GridGraph grid)) a.Append("  7 walk grid: not there\n");
                    else
                    {
                        a.Append($"  7 walk grid: the game {(lam.Status != PlayerLocalAreaMovement.MovementStatus.NotMoving ? "is walking him" : "is not walking him")}\n");
                        lam.RescanPlayerGraph();
                        Pathfinding.NNInfo fromBody = grid.GetNearest(rb.position, Pathfinding.NearestNodeConstraint.Walkable);
                        Pathfinding.NNInfo fromTake = grid.GetNearest(takeOff, Pathfinding.NearestNodeConstraint.Walkable);
                        Pathfinding.NNInfo at = grid.GetNearest(spot);
                        Pathfinding.NNInfo atFree = grid.GetNearest(spot, Pathfinding.NearestNodeConstraint.Walkable);
                        a.Append($"    the spot's cell: {(at.node == null ? "none" : (at.node.Walkable ? "free" : "BLOCKED") + $" at {at.position:F3}, {FlatOff(at.position, spot):F3} off")}; the nearest free cell {(atFree.node == null ? "none" : $"{atFree.position:F3}, {FlatOff(atFree.position, spot):F3} off")}\n");
                        a.Append($"    the grid: center {grid.center:F2}, {grid.width} x {grid.depth} cells of {grid.nodeSize:F2}, collision diameter {grid.collision.diameter:F2} (x the cell)\n");
                        bool noWay = fromTake.node != null && atFree.node != null && !Pathfinding.PathUtilities.IsPathPossible(fromTake.node, atFree.node);
                        if (noWay) a.Append(GridMap(grid, takeOff, spot, fromTake.node));
                        a.Append($"    from the body's cell {(fromBody.node == null ? "none" : fromBody.position.ToString("F3"))}: {(fromBody.node != null && at.node != null ? (Pathfinding.PathUtilities.IsPathPossible(fromBody.node, at.node) ? "a way" : "no way") : "-")}; from the take-off's cell {(fromTake.node == null ? "none" : fromTake.position.ToString("F3"))}: {(fromTake.node != null && at.node != null ? (Pathfinding.PathUtilities.IsPathPossible(fromTake.node, at.node) ? "a way" : "no way") : "-")}{(fromTake.node != null && atFree.node != null ? $"; to the nearest free cell: {(Pathfinding.PathUtilities.IsPathPossible(fromTake.node, atFree.node) ? "a way" : "no way")}" : "")}\n");
                    }
                }
                catch (System.Exception e) { a.Append("  7 walk grid test failed: " + e.Message + "\n"); }
                a.Append($"  => the flight's rule: {WhyCannotLandAtCopy(pc, spot, takeOff, false) ?? "lands"}; the jump's (beside things): {WhyCannotLandAtCopy(pc, spot, takeOff, true) ?? "lands"}\n");
                // 8. every collider near the spot
                int m = Physics.OverlapSphereNonAlloc(spot, 1.2f, auditNear, ~0, QueryTriggerInteraction.Ignore);
                a.Append($"  8 colliders within 1.2 ({m}):\n");
                for (int i = 0; i < m && i < auditNear.Length; i++)
                {
                    Collider c = auditNear[i];
                    if (c == null || !c || c == body || c.attachedRigidbody == rb) continue;
                    Vector3 cp = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(spot) : c.ClosestPoint(spot);
                    float top = ObjectTop(c, out Wgo w);
                    a.Append($"    {PathOf(c)}: layer {LayerMask.LayerToName(c.gameObject.layer)}, {c.GetType().Name}{(c.isTrigger ? " trigger" : "")}, bounds {c.bounds.min:F2}..{c.bounds.max:F2}, drawn up to {(float.IsNegativeInfinity(top) ? "-" : top.ToString("F2"))}{(w != null ? ", wgo " + w.name : "")}, {FlatOff(cp, spot):F3} from the spot{(c.isTrigger ? "" : (KeeperMask(rb) & (1 << c.gameObject.layer)) != 0 ? ", stops him" : ", doesn't stop him")}\n");
                }
                return a.ToString();
            }

            // The walk grid as a map (row = z, north at the top; column = x, west at the left): '.' free and joined to the
            // take-off's cell, ',' free but not joined, '#' blocked, 'T' the take-off, 'S' the spot.
            private static string GridMap(Pathfinding.GridGraph grid, Vector3 takeOff, Vector3 spot, Pathfinding.GraphNode from)
            {
                var m = new StringBuilder("    the grid's map ('.' free, joined to the take-off; ',' free, not joined; '#' blocked; T take-off; S spot), north up:\n");
                int w = grid.width, d = grid.depth;
                Vector3 c0 = (Vector3)grid.nodes[0].position, c1 = w * d > 1 ? (Vector3)grid.nodes[w * d - 1].position : c0;
                m.Append($"    x {c0.x:F2}..{c1.x:F2}, z {c0.z:F2}..{c1.z:F2}\n");
                for (int z = d - 1; z >= 0; z--)
                {
                    var row = new StringBuilder("    ");
                    for (int x = 0; x < w; x++)
                    {
                        Pathfinding.GridNodeBase n = grid.nodes[z * w + x];
                        Vector3 p = (Vector3)n.position;
                        char ch = !n.Walkable ? '#' : Pathfinding.PathUtilities.IsPathPossible(from, n) ? '.' : ',';
                        if (Mathf.Abs(p.x - takeOff.x) <= grid.nodeSize * 0.5f && Mathf.Abs(p.z - takeOff.z) <= grid.nodeSize * 0.5f) ch = 'T';
                        if (Mathf.Abs(p.x - spot.x) <= grid.nodeSize * 0.5f && Mathf.Abs(p.z - spot.z) <= grid.nodeSize * 0.5f) ch = 'S';
                        row.Append(ch);
                    }
                    m.Append(row).Append('\n');
                }
                return m.ToString();
            }

            // the rule as it stands (the walk-away at once, so it gives its answer)
            private static string WhyCannotLandAtCopy(PlayerController pc, Vector3 spot, Vector3 takeOff, bool beside)
            {
                Vector3 s = spot;
                string why = WhyCannotLandAt(pc, ref s, grid: true, connectedTo: takeOff, walkOut: true, walkBudgetMs: 0f, besideThings: beside);
                if (why == null && (s - spot).sqrMagnitude > 1e-6f) return $"lands, moved {FlatOff(s, spot):F3} onto the free cell at {s:F3}";
                return why;
            }

            // A spot of ground at the keeper's level the NPCs' mesh doesn't cover at all (a bridge's deck: its nearest node
            // the ground under the bridge), where he fits and the walk-away test lets him walk on: the self-test's stand for
            // the user's bridge. False when there is none within 'radius'.
            internal static bool FindOffMeshSpot(PlayerController pc, float radius, out Vector3 spot, out string what)
            {
                spot = default; what = null;
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null || !CanJudgeGround) return false;
                Vector3 me = rb.position;
                int walks = 0;
                for (float r = 1f; r <= radius; r += 0.5f)
                {
                    for (int k = 0; k < 24; k++)
                    {
                        Vector3 p = me + Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward * r;
                        float gy = GroundHeight(p, out string src);
                        if (float.IsNaN(gy) || src == "navmesh" || Mathf.Abs(gy - me.y) > 0.6f) continue;
                        p.y = gy;
                        string beside = BesideTheMesh(p, me, gy);
                        if (beside == null) continue; // the mesh close by: that is the beside-things case
                        bool bf = Physics.queriesHitBackfaces;
                        string fit;
                        try { Physics.queriesHitBackfaces = true; fit = BodyDoesNotFit(rb, body, KeeperMask(rb), p); }
                        finally { Physics.queriesHitBackfaces = bf; }
                        if (fit != null) continue;
                        if (++walks > 6) return false;
                        if (WhyCannotWalkAway(pc, me, p, out string how, 0f) != null) continue;
                        spot = p;
                        what = $"ground from {src} at {gy:F2}, the NPCs' mesh: {beside}; the walk-away: {how}";
                        return true;
                    }
                }
                return false;
            }

            // A spot beside a real thing (a workbench, a chest) where the flight's landing rule says "no walkable ground"
            // (the NPCs' mesh cut back from it) and the keeper fits, joined to his own spot by the mesh: the self-test's
            // stand for the user's yard. False when there is none within 'radius'.
            internal static bool FindBesideSpot(PlayerController pc, float radius, out Vector3 spot, out string what)
            {
                spot = default; what = null;
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                Collider body = pc?.PhysicalBody?.MeshCollider;
                if (rb == null || body == null || !CanJudgeGround) return false;
                Vector3 me = rb.position;
                int mask = KeeperMask(rb) & ~GroundLayers & ~(1 << rb.gameObject.layer) & ~(1 << 14);
                int n = Physics.OverlapSphereNonAlloc(me, radius, auditNear, mask, QueryTriggerInteraction.Ignore);
                float half = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z);
                int tried = 0;
                for (int i = 0; i < n && i < auditNear.Length && tried < 400; i++)
                {
                    Collider c = auditNear[i];
                    if (c == null || !c || c.isTrigger || c.attachedRigidbody == rb || IsModView(c)) continue;
                    Bounds b = c.bounds;
                    if (b.size.x > 6f || b.size.z > 6f) continue; // a thing, not a cliff
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                        float ext = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
                        for (float d = 0.04f; d <= 0.16f; d += 0.04f)
                        {
                            tried++;
                            Vector3 p = new Vector3(b.center.x, me.y, b.center.z) + dir * (ext + half + d);
                            float gy = GroundHeight(p, out string src);
                            if (float.IsNaN(gy) || src == "navmesh" || Mathf.Abs(gy - me.y) > 0.3f) continue;
                            p.y = gy;
                            if (BesideTheMesh(p, me, gy) != null) continue;
                            bool bf = Physics.queriesHitBackfaces;
                            string fit;
                            try { Physics.queriesHitBackfaces = true; fit = BodyDoesNotFit(rb, body, KeeperMask(rb), p); }
                            finally { Physics.queriesHitBackfaces = bf; }
                            if (fit != null) continue;
                            spot = p;
                            what = $"{PathOf(c)} ({c.GetType().Name}, bounds {b.min:F2}..{b.max:F2}), {d:F2} beyond its side, the ground from {src}";
                            return true;
                        }
                    }
                }
                return false;
            }

            internal static string PathOf(Component c)
            {
                Transform t = c.transform;
                string p = t.name;
                for (int k = 0; k < 3 && t.parent != null; k++) { t = t.parent; p = t.name + "/" + p; }
                return p;
            }
        }
    }
}
