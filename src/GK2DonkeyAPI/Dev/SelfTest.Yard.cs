using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The jump at the real things of the player's yard (the user, 2026-10-24: "the jump tests are done in an empty area
    // instead of the home yard where there are many things to jump and slide over"). The test runner moves to its
    // remembered spot (Debug.SelfTestSpot), so this scenario goes to the yard itself: the game object "World/yard(Clone)"
    // (its BuildArea zones give its bounds), every built thing and fence in it (the Obstacle layer, grouped by the thing
    // they belong to), a running jump at each from the side with room, and one along the fence line (a slide). For every
    // jump: down on the ground, nothing over him, never sent back to the take-off spot, the fall watchdog silent; what
    // the jump did (over it, stopped by it, down beside it, carried off its top, slid along it) is said, not judged.
    internal static partial class SelfTest
    {
        private const int MaxYardThings = 12;

        private sealed class YardThing
        {
            internal string Name;
            internal Bounds B;
            internal Collider Any;
            internal Transform Root;
            internal float TopPx;
        }

        // the thing a collider belongs to: the nearest parent whose name ends in "(Clone)" (a built thing's prefab), else
        // the nearest with a Wgo, else the collider's parent (a fence piece), else itself
        private static Transform ThingRoot(Collider c)
        {
            for (Transform t = c.transform; t != null; t = t.parent) if (t.name.EndsWith("(Clone)", System.StringComparison.Ordinal)) return t;
            Wgo w = c.GetComponentInParent<Wgo>();
            if (w != null) return w.transform;
            return c.transform.parent != null ? c.transform.parent : c.transform;
        }

        private static IEnumerator FlightJumpYard()
        {
            if (!Flight.Active || !Plugin.MountsOn) { Skip(!Flight.Active ? "the flying experiment is off" : "the mounts are off (Debug.MountsPreview)"); yield break; }
            GameObject yard = GameObject.Find("World/yard(Clone)");
            if (yard == null) { Skip("no \"World/yard(Clone)\" in this scene: the player's yard is elsewhere (run this one from its scene)"); yield break; }
            Bounds area = default;
            bool any = false;
            foreach (Collider c in yard.GetComponentsInChildren<Collider>(true))
            {
                if (!any) { area = c.bounds; any = true; } else area.Encapsulate(c.bounds);
            }
            if (!any) { Skip("the yard has no area colliders to measure it by"); yield break; }
            area.Expand(new Vector3(2f, 4f, 2f));
            Info($"the yard: {area.min:F1} .. {area.max:F1}");

            // into the yard first: the game streams and optimises the world's things by distance, and a scan from the
            // runner's spot 40 away found 2 things of a full yard (F11 2026-10-24); then a moment for its stages to load
            Vector3 from = Keeper();
            Vector3? first = YardFreeSpot(area, new List<YardThing>());
            if (!first.HasValue) { Skip("no spot in the yard to stand on"); yield break; }
            yield return MoveKeeper(first.Value, homeFacing);
            if (!moved) { Skip("the keeper could not be put into the yard"); yield break; }
            yield return Seconds(1.5f);

            // the things in it: Obstacle-layer colliders that stop the keeper, grouped by the thing they belong to (its
            // prefab root, the "(Clone)" object; the Wgo component sits on a shared parent of every built thing here)
            Rigidbody rb = PC.PhysicalBody.Rb;
            int obstacle = LayerMask.NameToLayer("Obstacle");
            if (obstacle < 0) { Skip("no Obstacle layer"); yield break; }
            var found = new Collider[512];
            int n = Physics.OverlapBoxNonAlloc(area.center, area.extents, found, Quaternion.identity, 1 << obstacle, QueryTriggerInteraction.Ignore);
            var things = new List<YardThing>();
            for (int i = 0; i < n && i < found.Length; i++)
            {
                Collider c = found[i];
                if (c == null || !c || c.isTrigger || c.attachedRigidbody == rb) continue;
                if (c.bounds.size.x > 8f || c.bounds.size.z > 8f) continue; // the area's own walls, not a thing
                Transform root = ThingRoot(c);
                YardThing t = things.Find(x => x.Root == root);
                if (t == null) things.Add(t = new YardThing { Name = root.name.Replace("(Clone)", ""), B = c.bounds, Any = c, Root = root });
                else t.B.Encapsulate(c.bounds);
            }
            Info($"{n} obstacle collider(s) in the yard's box, {things.Count} thing(s)");
            if (things.Count == 0) { Skip("no built things or fences in the yard"); yield break; }
            float px = 2f * 0.01666667f;
            foreach (YardThing t in things)
            {
                float top = Mount.Instance.ObjectTop(t.Any, out _);
                // its height over the ground under it (a collider may dip under the ground: the chopping spot's read 30 px for 15)
                float groundY = Physics.Raycast(new Vector3(t.B.center.x, t.B.max.y + 1f, t.B.center.z), Vector3.down, out RaycastHit gh, t.B.size.y + 3f, GroundLayersMask, QueryTriggerInteraction.Ignore) ? gh.point.y : t.B.min.y;
                t.TopPx = (Mathf.Max(t.B.max.y, float.IsNegativeInfinity(top) ? t.B.max.y : top) - groundY) / px;
            }
            things.Sort((a, b) => Vector3.Distance(a.B.center, area.center).CompareTo(Vector3.Distance(b.B.center, area.center)));
            Info($"{things.Count} thing(s) in the yard: " + string.Join(", ", things.ConvertAll(t => $"{t.Name} ({t.B.size.x:0.0}x{t.B.size.z:0.0})")));

            // a spot clear of the things, on a donkey of the test's own
            Vector3? into = YardFreeSpot(area, things) ?? first;
            if (Vector3.Distance(into.Value, Keeper()) > 0.5f)
            {
                yield return MoveKeeper(into.Value, homeFacing);
                if (!moved) { Skip("no free spot clear of the things to start from"); yield break; }
            }
            MountHandle donkey = Spawn(into.Value, right: true);
            try
            {
                if (donkey == null) { Fail("could not spawn the test's donkey"); yield break; }
                yield return Until(() => donkey.IsReady, 6f);
                MountResult r = donkey.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance mi = Mount.Ridden;
                // the cap applies from 0.5 a second up (Flight.FixedStep); a standing keeper's drift (0.03) is recorded too
                Check(Flight.TestRideTop <= 0.5f || Flight.TestRideTop >= 5f, $"the jump's speed cap is the ride's own, not a test's leftover (the ride's top {(Flight.TestRideTop <= 0.5f ? "not measured yet: no cap" : Flight.TestRideTop.ToString("0.0"))})");
                int returns0 = SafeSpots.TestReturns, tried = 0, over = 0, stopped = 0, carried = 0, beside = 0;
                var bad = new List<string>();
                foreach (YardThing t in things)
                {
                    if (tried >= MaxYardThings) break;
                    if (!ApproachTo(t, things, out Vector3 start, out Vector3 dir)) { Info($"{t.Name}: no side with room to run at it from"); continue; }
                    tried++;
                    PutKeeper(start, dir.x >= 0f ? 1f : -1f);
                    TestSay($"Jumping at {t.Name} ({t.TopPx:0} px)"); // the watcher sees which thing (the user, 2026-10-24: "jump over nothing")
                    yield return Frames(6);
                    Vector3 before = Keeper();
                    yield return JumpWith(mi, new Vector2(dir.x, dir.z), held: true);
                    Vector3 at = Keeper();
                    float along = Vector3.Dot(at - before, dir);
                    bool grounded = SurfaceAtFeet(at), under = Mount.Instance.WhyUnderground(at) != null;
                    bool ok = grounded && !under && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && !Flight.Free && SafeSpots.TestReturns == returns0;
                    // where it ended against the thing, along the run
                    float near = Vector3.Dot(t.B.min - before, dir), far = Vector3.Dot(t.B.max - before, dir);
                    if (near > far) { float s = near; near = far; far = s; }
                    string what = along > far ? "over it" : along < near - 0.1f ? "stopped before it" : "down beside or against it";
                    if (Flight.TestJumpPerched) { what = "came down over it, carried off"; carried++; }
                    else if (along > far) over++;
                    else if (along < near - 0.1f) stopped++;
                    else beside++;
                    string line = $"{t.Name} ({t.TopPx:0} px tall, {t.B.size.x:0.0}x{t.B.size.z:0.0}): {what}, {along:0.00} along the run{(Flight.TestJumpWentBack ? ", SENT BACK" : "")}{(grounded ? "" : ", NOT ON THE GROUND")}{(under ? ", UNDERGROUND" : "")}{(SafeSpots.TestReturns != returns0 ? ", THE WATCHDOG FIRED" : "")}";
                    Info(line);
                    if (!ok) bad.Add(line);
                    returns0 = SafeSpots.TestReturns;
                }
                Check(bad.Count == 0, $"{tried} thing(s) jumped at: every jump ended on the ground, nothing over him, never sent back, the watchdog silent ({over} over, {stopped} stopped, {beside} beside, {carried} carried off a top)" + (bad.Count > 0 ? "; failed: " + string.Join(" | ", bad) : ""));

                // along the longest thing (a fence, a wall), approached at an angle to its long side: a slide
                YardThing fence = null;
                foreach (YardThing t in things) if (Mathf.Max(t.B.size.x, t.B.size.z) > 2f && (fence == null || Mathf.Max(t.B.size.x, t.B.size.z) > Mathf.Max(fence.B.size.x, fence.B.size.z))) fence = t;
                if (fence != null && ApproachTo(fence, things, out Vector3 fs, out Vector3 fd, longSide: true))
                {
                    Vector3 side = Mathf.Abs(fd.x) > 0.5f ? Vector3.forward : Vector3.right;
                    Vector3 diag = (fd + side * 0.8f).normalized;
                    PutKeeper(fs, fd.x >= 0f ? 1f : -1f);
                    TestSay($"Sliding along {fence.Name}");
                    yield return Frames(6);
                    Vector3 before = Keeper();
                    int ret = SafeSpots.TestReturns;
                    yield return JumpWith(mi, new Vector2(diag.x, diag.z), held: true);
                    Vector3 at = Keeper();
                    float slid = Vector3.Dot(at - before, side), into2 = Vector3.Dot(at - before, fd);
                    Check(SurfaceAtFeet(at) && Mount.Instance.WhyUnderground(at) == null && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && SafeSpots.TestReturns == ret,
                        $"along {fence.Name} at an angle: {slid:0.00} along it, {into2:0.00} towards it, down on the ground, not sent back");
                }
                else Info("nothing longer than 2 in the yard (a fence, a wall): the slide along one not tried");
            }
            finally
            {
                Flight.TestJumpHeld = null; Flight.TestMoveDir = null;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (donkey != null && donkey.Exists) donkey.Remove(); } catch { }
            }
        }

        // one jump of the ridden mount: held, the stick pushed 'push' (as the keys would), waited out
        private static IEnumerator JumpWith(Mount.Instance i, Vector2 push, bool held)
        {
            Flight.TestJumpHeld = held;
            Flight.TestMoveDir = push;
            try
            {
                Flight.Jump(i);
                float start = Time.time;
                while (Flight.Now == Flight.Phase.Jumping && Time.time - start < Flight.JumpTimeS + Flight.JumpPerchS + 3f) yield return null;
            }
            finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
            yield return Frames(8);
        }

        // a spot in the yard the keeper can stand on, away from every thing
        private static Vector3? YardFreeSpot(Bounds area, List<YardThing> things)
        {
            for (float r = 0f; r < Mathf.Max(area.extents.x, area.extents.z); r += 0.6f)
            {
                for (int k = 0; k < 16; k++)
                {
                    Vector3 p = area.center + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r;
                    if (!area.Contains(new Vector3(p.x, area.center.y, p.z))) continue;
                    if (!StandSpot(ref p, things, 0.6f)) continue;
                    return p;
                }
            }
            return null;
        }

        // p put on the ground and judged: ground at his level, his body fitting, no thing within 'clear'
        private static bool StandSpot(ref Vector3 p, List<YardThing> things, float clear)
        {
            if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, GroundLayersMask, QueryTriggerInteraction.Ignore)) return false;
            p.y = hit.point.y;
            if (Mount.Instance.KeeperFits(PC, p) != null) return false;
            foreach (YardThing t in things)
            {
                Bounds b = t.B; b.Expand(new Vector3(clear * 2f, 10f, clear * 2f));
                if (b.Contains(p)) return false;
            }
            return true;
        }

        // the side of a thing with room to run at it from: 1.7 before its face, the run straight at its middle
        private static bool ApproachTo(YardThing t, List<YardThing> things, out Vector3 start, out Vector3 dir, bool longSide = false)
        {
            start = default; dir = default;
            // longSide: run at its long side (across its short axis), for a slide along it
            Vector3[] sides = longSide
                ? (t.B.size.x >= t.B.size.z ? new[] { Vector3.back, Vector3.forward } : new[] { Vector3.left, Vector3.right })
                : new[] { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            foreach (Vector3 d in sides)
            {
                float ext = Mathf.Abs(d.x) * t.B.extents.x + Mathf.Abs(d.z) * t.B.extents.z;
                Vector3 p = new Vector3(t.B.center.x, t.B.center.y, t.B.center.z) - d * (ext + 1.7f);
                if (!StandSpot(ref p, things, 0.35f)) continue;
                start = p; dir = d;
                return true;
            }
            return false;
        }
    }
}
