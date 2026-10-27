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
        internal const int MaxYardThings = 12;

        internal sealed class YardThing
        {
            internal string Name;
            internal Bounds B;
            internal Collider Any;
            internal Transform Root;
            internal float TopPx;
        }

        // the thing a collider belongs to: the nearest parent whose name ends in "(Clone)" (a built thing's prefab), else
        // the nearest with a Wgo, else the collider's parent (a fence piece), else itself
        internal static Transform ThingRoot(Collider c)
        {
            for (Transform t = c.transform; t != null; t = t.parent) if (t.name.EndsWith("(Clone)", System.StringComparison.Ordinal)) return t;
            Wgo w = c.GetComponentInParent<Wgo>();
            if (w != null) return w.transform;
            return c.transform.parent != null ? c.transform.parent : c.transform;
        }

        internal static IEnumerator FlightJumpYard()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
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
            // several spots tried: the game pushes him off one now and then (a zombie at work there, a thing with a collider
            // the finder doesn't know: "that spot is blocked", F11 2026-10-24 03:42)
            Vector3? first = null;
            foreach (Vector3 cand in YardFreeSpots(area, new List<YardThing>(), 6))
            {
                PutKeeper(cand, homeFacing);
                yield return Frames(3);
                if (Flat(Keeper() - cand) < 0.6f) { first = Keeper(); break; }
            }
            if (!first.HasValue) { Skip("no spot in the yard the keeper stays on (each tried pushed him off)"); yield break; }
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
                    string line = $"{t.Name} ({t.TopPx:0} px tall, {t.B.size.x:0.0}x{t.B.size.z:0.0}): {what}, {along:0.00} along the run, body {rb.interpolation}{(Flight.TestJumpWentBack ? ", SENT BACK" : "")}{(grounded ? "" : ", NOT ON THE GROUND")}{(under ? ", UNDERGROUND" : "")}{(SafeSpots.TestReturns != returns0 ? ", THE WATCHDOG FIRED" : "")}";
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

        // A real slope (the user, 2026-10-24: "going uphill, sometimes when jumping, you get stuck like jumping against a
        // wall"): a hillside between two terrain levels near the keeper (the drawn ground's levels are flat, so only the
        // real terrain shows the fault; a test ramp never could), a running jump up it and one down it: the body moves
        // in the air (not held), lands on the ground, never sent back; the ground the mount is drawn on never steps.
        internal static IEnumerator FlightJumpSlope()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors"); yield break; }
            Vector3 me = Keeper();
            bool found = FindSlope(me, 30f, out Vector3 foot, out Vector3 up, out float rise, out float run);
            if (!found && KnownSlopeNear(out Vector3 near))
            {
                // the world streams by distance (the yard's lesson): from the runner's spot the road 17 to 23 away read flat
                // at 3.6 and then no ground at all where the user's traces had the body standing at 6.0 (F11 2026-10-24
                // 11:17, Profiles); in close and loaded first, the finder again from there, then the known road itself
                yield return MoveKeeper(near, -1f);
                if (moved)
                {
                    yield return Seconds(1.5f);
                    found = FindSlope(Keeper(), 12f, out foot, out up, out rise, out run) || KnownSlope(out foot, out up, out rise, out run);
                    if (found) Info($"found from next to the road (from the runner's spot {Vector3.Distance(me, near):0} away it had read flat: the world streams by distance)");
                    else Info("still none from next to the road: " + Profiles(Keeper(), 10));
                }
            }
            if (!found)
            {
                Info($"no hillside within 30 (the ground rising 0.35 to 1.6 over 2.5 of walkable ground): {slopeSeen}; run this one near a slope");
                Info("the ground along each line from the keeper, a reading every unit: " + Profiles(me, 30));
                Skip("no slope near here");
                yield break;
            }
            Info($"a slope at {foot:F2}, rising {rise:0.00} over {run:0.0} towards {up:F1}");
            yield return MoveKeeper(foot - up * 1.2f, up.x >= 0f ? 1f : -1f);
            if (!moved) { Skip("the keeper could not be put at the slope's foot"); yield break; }
            MountHandle donkey = Spawn(Keeper(), right: up.x >= 0f);
            try
            {
                if (donkey == null) { Fail("could not spawn the test's donkey"); yield break; }
                yield return Until(() => donkey.IsReady, 6f);
                MountResult r = donkey.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Rigidbody rb = PC.PhysicalBody.Rb;
                Mount.Instance mi = Mount.Ridden;
                float px = 2f * 0.01666667f;
                foreach (bool uphill in new[] { true, false })
                {
                    Vector3 start = uphill ? foot - up * 1.0f : foot + up * (run + 1.0f);
                    // on the ground's surface there: the downhill start is up the slope, where the foot's height is inside
                    // the ramp (the body put there was pushed out by the physics in one step: a 17 px jolt, F11 2026-10-24)
                    if (Physics.Raycast(start + Vector3.up * 4f, Vector3.down, out RaycastHit sh, 10f, GroundLayersMask, QueryTriggerInteraction.Ignore)) start.y = sh.point.y;
                    Vector3 dir = uphill ? up : -up;
                    PutKeeper(start, dir.x >= 0f ? 1f : -1f);
                    TestSay(uphill ? "Jumping up the slope" : "Jumping down the slope");
                    Vector3 placed = rb.position;
                    yield return Frames(12);
                    Vector3 before = rb.position;
                    // the probe (2026-10-27, the hillside's 4.5 px steps): what the body did after being placed, and every frame of the jump
                    if (Plugin.JumpTraceOn != null && Plugin.JumpTraceOn.Value) Info($"{(uphill ? "uphill" : "downhill")} probe: placed at {placed:F3}, after 12 frames at {before:F3} (moved {(before - placed).magnitude * 30f:0.0} px), body {rb.interpolation}, kinematic {rb.isKinematic}, speed {rb.linearVelocity.magnitude:0.00}, Flight {Flight.Now}, fixed step {Time.fixedDeltaTime * 1000f:0.0} ms, mount at {mi.Position:F3}, lift {Flight.LiftFor(mi) * 30f:0.0} px");
                    var probe = new System.Collections.Generic.List<string>();
                    Mount.Instance.TestGroundProbe = new System.Collections.Generic.List<string>(); // the deep probe: every Follow's ground sampling (Mount.cs)
                    int returns = SafeSpots.TestReturns;
                    float last = float.NaN, worst = 0f, worstDt = 0f, slowest = 0f; // the biggest step, the frame it came in, the slowest frame
                    Flight.TestJumpHeld = true;
                    Flight.TestMoveDir = new Vector2(dir.x, dir.z);
                    try
                    {
                        Flight.Jump(mi);
                        float t0 = Time.time;
                        while (Time.time - t0 < Flight.J.Time + 2f)
                        {
                            float drawn = mi.Position.y - Flight.LiftFor(mi);
                            probe.Add($"f{Time.frameCount} {Time.unscaledDeltaTime * 1000f:0}ms {Flight.Now} m{mi.Position.y:0.000} L{Flight.LiftFor(mi) * 30f:0.0} rb{rb.position.y:0.000} tf{rb.transform.position.y:0.000} {(rb.interpolation == RigidbodyInterpolation.None ? "N" : "I")} s{(float.IsNaN(last) ? 0f : (drawn - last) * 30f):+0.0;-0.0}");
                            if (!float.IsNaN(last) && Mathf.Abs(drawn - last) > worst) { worst = Mathf.Abs(drawn - last); worstDt = Time.unscaledDeltaTime; }
                            slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
                            last = drawn;
                            if (Flight.Now != Flight.Phase.Jumping && Time.time - t0 > 0.2f) break;
                            yield return null;
                        }
                    }
                    finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                    System.Collections.Generic.List<string> deep = Mount.Instance.TestGroundProbe;
                    Mount.Instance.TestGroundProbe = null;
                    // the frames, written out only when a step went past 2.5 px (a normal jump's biggest, a 14 ms frame's): the
                    // touch-down's cut of 2026-10-27 was found in them (docs/flying.md "The landing cuts")
                    if (worst > 2.5f * px)
                    {
                        Info($"{(uphill ? "uphill" : "downhill")} frames (dt, phase, mount y, lift px, body y, body transform y, interpolation, the drawn ground's step px): {string.Join(" | ", probe)}");
                        for (int di = 0; deep != null && di < deep.Count; di += 12) Info($"{(uphill ? "uphill" : "downhill")} ground sampling {di / 12 + 1}: {string.Join(" | ", deep.GetRange(di, Mathf.Min(12, deep.Count - di)))}");
                    }
                    yield return Frames(6);
                    Vector3 at = rb.position;
                    float along = Vector3.Dot(at - before, dir);
                    bool grounded = SurfaceAtFeet(at);
                    // the step cap is the ramp rule's 4 px (docs/flying.md "running downhill"): a 24-degree hillside at the
                    // mover's speed climbs 0.11 a physics step, 3.2 px, by itself; the flat yard's 2 px can't hold here
                    Check(along > 1.0f && grounded && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && !Flight.Free && SafeSpots.TestReturns == returns && worst <= 4f * px,
                        $"a running jump {(uphill ? "up" : "down")} the slope: {along:0.00} along it{(along > 1.0f ? "" : " (HELD in place: over 1 wanted)")}, {at.y - before.y:+0.00;-0.00} in height, on the ground{(grounded ? "" : " NOT")}, the drawn ground stepping at most {worst / px:0.0} px a frame{(worst <= 4f * px ? "" : " (OVER the 4 allowed on a slope)")}, in a frame of {worstDt * 1000f:0} ms (the slowest {slowest * 1000f:0} ms){(Flight.TestJumpWentBack ? ", SENT BACK" : "")}{(Flight.Now == Flight.Phase.Ground ? "" : ", not back on the ground (" + Flight.Now + ")")}{(SafeSpots.TestReturns == returns ? "" : ", the watchdog put him back")}");
                }
            }
            finally
            {
                Flight.TestJumpHeld = null; Flight.TestMoveDir = null;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (donkey != null && donkey.Exists) donkey.Remove(); } catch { }
            }
        }

        // A hillside near 'me': walkable ground (the ground layers' raycast) rising 0.4 to 1.3 over 2.5 along one of
        // eight directions, with ground at both ends and no obstacle in the way; 'foot' its lower end, 'up' the way up.
        internal static string slopeSeen = "none";

        internal static bool FindSlope(Vector3 me, float radius, out Vector3 foot, out Vector3 up, out float rise, out float run)
        {
            foot = default; up = default; rise = 0f; run = 2.5f;
            // what the finder saw, for the INFO when nothing is taken: how many runs rose within the window, and why the
            // first few were rejected (the steepest run alone said nothing: a cliff two levels up, F11 2026-10-24 03:50)
            int inWindow = 0, pairs = 0;
            var rejected = new List<string>();
            slopeSeen = "none";
            for (float r = 0f; r <= radius; r += 1.5f)
            {
                for (int k = 0; k < 16; k++)
                {
                    Vector3 c = me + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r;
                    if (!GroundAt(c, out float cy)) continue;
                    foreach (int d in new[] { 0, 2, 4, 6, 1, 3, 5, 7 })
                    {
                        Vector3 dir = Quaternion.Euler(0f, d * 45f, 0f) * Vector3.forward;
                        Vector3 top = c + dir * run;
                        if (!GroundAt(top, out float ty)) continue;
                        float dy = ty - cy;
                        pairs++;
                        if (dy < 0.35f || dy > 1.6f) continue;
                        inWindow++;
                        string cand = $"{dy:0.00} at ({c.x:0},{c.z:0})>{(dir.x > 0.5f ? "+x" : dir.x < -0.5f ? "-x" : "")}{(dir.z > 0.5f ? "+z" : dir.z < -0.5f ? "-z" : "")}";
                        void Reject(string why) { if (rejected.Count < 20) rejected.Add(cand + ": " + why); slopeSeen = $"{pairs} runs sampled, {inWindow} rose within the window, none taken: " + string.Join("; ", rejected); }
                        // a slope, not a step: the middle about half way up
                        if (!GroundAt(c + dir * (run * 0.5f), out float my)) { Reject("no ground in the middle"); continue; }
                        if (Mathf.Abs((my - cy) - dy * 0.5f) > dy * 0.4f) { Reject($"a step, not a slope (the middle {my - cy:0.00} up of {dy:0.00})"); continue; }
                        // room to run at it, and to land past its top: ground there he fits on (any height short of a cliff: a
                        // long hillside has lower ground behind every foot and higher beyond every top; the first finder
                        // wanted them flat and found no slope within 30 of the runner's spot, the user's 19 away, F11 2026-10-24)
                        Vector3 before = c - dir * 1.2f, after = top + dir * 1.5f;
                        if (!GroundAt(before, out float by) || !GroundAt(after, out float ay)) { Reject("no ground before or after"); continue; }
                        if (Mathf.Abs(by - cy) > 1.3f || Mathf.Abs(ay - ty) > 1.3f) { Reject($"a cliff before ({by - cy:+0.00;-0.00}) or after ({ay - ty:+0.00;-0.00})"); continue; }
                        string fitB = Mount.Instance.KeeperFits(PC, new Vector3(before.x, by, before.z)), fitA = Mount.Instance.KeeperFits(PC, new Vector3(after.x, ay, after.z));
                        if (fitB != null || fitA != null) { Reject("no room: " + (fitB ?? fitA)); continue; }
                        foot = new Vector3(c.x, cy, c.z); up = dir; rise = dy;
                        return true;
                    }
                }
            }
            if (inWindow == 0) slopeSeen = $"{pairs} runs sampled, none rose 0.35 to 1.6 over 2.5";
            return false;
        }

        // The road up to the yard in the temple's scene, as the user's jump traces measured it (2026-10-24: the ground 4.05 at
        // x 21.4, 4.75 at 20.0, 5.35 at 18.5, z 15.3, rising towards -x): the fallback when the finder takes nothing near the
        // runner's spot, checked live (the ground there must still rise within the window).
        // Next to the known road (RuinedTemple), where the finder is run again with the ground loaded.
        internal static bool KnownSlopeNear(out Vector3 near)
        {
            near = new Vector3(24.5f, 3.6f, 15.7f);
            string scene = PC?.PlayerData?.currentGameSceneId;
            return scene != null && scene.IndexOf("RuinedTemple", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool KnownSlope(out Vector3 foot, out Vector3 up, out float rise, out float run)
        {
            foot = default; up = Vector3.left; rise = 0f; run = 2.5f;
            string scene = PC?.PlayerData?.currentGameSceneId;
            if (scene == null || scene.IndexOf("RuinedTemple", System.StringComparison.OrdinalIgnoreCase) < 0) return false;
            Vector3 c = new Vector3(21.6f, 4f, 15.3f);
            if (!GroundAt(c, out float cy) || !GroundAt(c + up * run, out float ty)) return false;
            rise = ty - cy;
            if (rise < 0.35f || rise > 1.6f) { Info($"the known road slope reads {rise:0.00} over {run:0.0} now: not taken"); return false; }
            foot = new Vector3(c.x, cy, c.z);
            Info($"the known road slope taken ({rise:0.00} over {run:0.0} towards -x from {foot:F1})");
            return true;
        }

        // the ground's height every unit along +x, -x, +z and -z from p, for the INFO when no slope is found
        internal static string Profiles(Vector3 p, int units)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, dir) in new[] { ("+x", Vector3.right), ("-x", Vector3.left), ("+z", Vector3.forward), ("-z", Vector3.back) })
            {
                sb.Append(name).Append(':');
                for (int i = 0; i <= units; i++) sb.Append(' ').Append(GroundAt(p + dir * i, out float y) ? y.ToString("0.0") : "-");
                sb.Append(" | ");
            }
            return sb.ToString();
        }

        internal static bool GroundAt(Vector3 p, out float y)
        {
            y = 0f;
            if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, GroundLayersMask, QueryTriggerInteraction.Ignore)) return false;
            y = hit.point.y;
            return true;
        }

        // one jump of the ridden mount: held, the stick pushed 'push' (as the keys would), waited out
        internal static IEnumerator JumpWith(Mount.Instance i, Vector2 push, bool held)
        {
            Flight.TestJumpHeld = held;
            Flight.TestMoveDir = push;
            try
            {
                Flight.Jump(i);
                float start = Time.time;
                while (Flight.Now == Flight.Phase.Jumping && Time.time - start < Flight.J.Time + Flight.JumpPerchS + 3f) yield return null;
            }
            finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
            yield return Frames(8);
        }

        // a spot in the yard the keeper can stand on, away from every thing (the first of YardFreeSpots)
        internal static Vector3? YardFreeSpot(Bounds area, List<YardThing> things)
        {
            foreach (Vector3 p in YardFreeSpots(area, things, 1)) return p;
            return null;
        }

        // up to 'count' spots in the yard the keeper can stand on, away from every thing, the middle first, spread out
        internal static List<Vector3> YardFreeSpots(Bounds area, List<YardThing> things, int count)
        {
            var found = new List<Vector3>();
            for (float r = 0f; r < Mathf.Max(area.extents.x, area.extents.z) && found.Count < count; r += 0.9f)
            {
                for (int k = 0; k < 16 && found.Count < count; k++)
                {
                    Vector3 p = area.center + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r;
                    if (!area.Contains(new Vector3(p.x, area.center.y, p.z))) continue;
                    if (!StandSpot(ref p, things, 0.6f)) continue;
                    if (found.Exists(q => Flat(q - p) < 1.5f)) continue;
                    found.Add(p);
                }
            }
            return found;
        }

        // p put on the ground and judged: ground at his level, his body fitting, no thing within 'clear'
        internal static bool StandSpot(ref Vector3 p, List<YardThing> things, float clear)
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
        internal static bool ApproachTo(YardThing t, List<YardThing> things, out Vector3 start, out Vector3 dir, bool longSide = false)
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
