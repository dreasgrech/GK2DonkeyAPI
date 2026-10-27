using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flight's landing judged by the keeper's own ground (FlightGround.WhyCannotWalkAway, docs/flying.md "Refused
    // landings: cut off from the take-off spot"): the walk from the keeper's own spot reaches his own part of the mesh at
    // once; from open ground with no part to find it ends as open ground, the same in 4 ms slices as at once, and its
    // time is read out; a pen of four temporary walls next to him walls a spot in.
    internal static partial class SelfTest
    {
        // The mount's jump (Flight.JumpHeightAt, docs/flying.md "The jump, the platformer way"): the arc by itself at the
        // tab's values (the top held and tapped, a let-go between, continuous, the same top and landing at 30, 60 and 144
        // fps, a heavier fall than rise); then, riding plain with the experiment on, one held and one tapped jump live:
        // their tops, back on the ground, the camera left down, a second press refused mid-jump.
        internal static IEnumerator FlightJump()
        {
            const float never = float.PositiveInfinity;
            Flight.JumpShape js = Flight.DefaultJump; // the arc by itself: the default jump's (the donkey's; a mount's own: FlightJumpPerMount)
            float top = js.Height, tap = Mathf.Min(js.Tap, js.Height);
            float Top(float letGo, float fps, out float doneAt, out int steps)
            {
                float t = 0f, max = -99f, prev = 0f; doneAt = -1f; steps = 0;
                for (int n = 0; n < 2000; n++)
                {
                    t += 1f / fps;
                    float h = Flight.JumpHeightAt(js, t, letGo, out bool done);
                    steps = Mathf.Max(steps, Mathf.RoundToInt(Mathf.Abs(Mathf.Round(h) - Mathf.Round(prev))));
                    prev = h; max = Mathf.Max(max, h);
                    if (done) { doneAt = t; break; }
                }
                return max;
            }
            float held60 = Top(never, 60f, out float heldEnd, out _);
            float tap60 = Top(0.016f, 60f, out float tapEnd, out _);
            float mid60 = Top(0.2f, 60f, out _, out _);
            Check(Mathf.Abs(held60 - top) < 0.1f && heldEnd > 0f, $"held, the top at {held60:0.00} px (the tab's {top:0}), down after {heldEnd:0.00} s");
            Check(Mathf.Abs(tap60 - tap) < 0.1f && tapEnd > 0f && tapEnd < heldEnd, $"tapped, the top at {tap60:0.00} px (the tab's {tap:0}), down after {tapEnd:0.00} s");
            Check(mid60 > tap + 0.1f && mid60 < top - 0.1f || tap >= top - 0.2f, $"let go after 0.2 s, between the two: {mid60:0.00} px");
            float worst = 0f;
            foreach (float letGo in new[] { never, 0f, 0.08f, 0.15f, 0.25f })
                for (int k = 0; k < 8000; k++)
                    worst = Mathf.Max(worst, Mathf.Abs(Flight.JumpHeightAt(js, (k + 1) * 1e-4f, letGo, out _) - Flight.JumpHeightAt(js, k * 1e-4f, letGo, out _)));
            Check(worst < 0.05f, $"continuous: the biggest step in 0.1 ms is {worst:0.000} px (no break between the crouch, rise, hang, fall and squash)");
            float t30 = Top(never, 30f, out float end30, out int s30), t144 = Top(never, 144f, out float end144, out int s144);
            Check(Mathf.Abs(t30 - t144) < 0.6f && Mathf.Abs(end30 - end144) <= 1f / 30f + 0.001f,
                $"the same jump at any frame rate: the top {t30:0.00} px at 30 fps, {t144:0.00} at 144; down after {end30:0.000} s and {end144:0.000} s (biggest step {s30} and {s144} px a frame)");
            // the heavier fall: from the top to the ground faster than from the ground to the top
            float offT = -1f, topT = -1f, hangEnd = -1f, landT = -1f;
            for (int k = 1; k < 20000 && landT < 0f; k++)
            {
                float tk = k * 1e-4f, h = Flight.JumpHeightAt(js, tk, never, out _);
                if (offT < 0f && h > 0f) offT = tk;                                  // leaves the ground (after the crouch)
                if (topT < 0f && h >= top - 0.001f) topT = tk;                       // reaches the top
                if (topT >= 0f && hangEnd < 0f && h < top - 0.001f) hangEnd = tk;    // the hang over
                if (hangEnd >= 0f && h <= 0f) landT = tk;                            // touches the ground
            }
            float upS = topT - offT, fallS = landT - hangEnd;
            Check(fallS > 0f && (fallS < upS || js.RiseShare <= 0.5f), $"up from the ground to the top in {upS:0.000} s, down in {fallS:0.000} s (the rise's share {js.RiseShare:0.00})");

            // live: a donkey of the test's own ridden plain (the experiment on), in place, then at two walls
            if (!Flight.Active)
            {
                Flight.Jump(null);
                Check(Flight.Now == Flight.Phase.Ground, "no mount ridden: the jump refused (the keeper on foot never jumps)");
                Info("the live part wants the flying experiment on: skipped");
                yield break;
            }
            if (Mount.Instance.IsIndoors())
            {
                Info("indoors: the live part wants open ground (a jump indoors stays in place)");
                yield break;
            }
            Flight.Jump(null);
            Check(Flight.Now == Flight.Phase.Ground, "no mount ridden: the jump refused (the keeper on foot never jumps)");
            MountHandle donkey = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
            GameObject walls = null;
            try
            {
                if (donkey == null) { Fail("could not spawn the test's donkey"); yield break; }
                yield return Until(() => donkey.IsReady, 6f);
                MountResult r = donkey.Ride(showMessage: false);
                if (!r.Ok) { Fail($"the test's donkey not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance i = Mount.IsMounted ? Mount.Ridden : null;
                Rigidbody rb = PC?.PhysicalBody?.Rb;
                if (i == null || rb == null) { Fail("riding, but no ridden mount or no keeper's body"); yield break; }

                // a jump: held (or tapped), the stick pushed along x (0: in place); its top, and where he lands. After each
                // one: never underground (nothing of the ground's layers over him within his height, ground under his feet)
                var under = new System.Collections.Generic.List<string>();
                int jumpsSeen = 0, returns0 = SafeSpots.TestReturns;
                void NotUnder(string what)
                {
                    jumpsSeen++;
                    Vector3 at = rb.position;
                    string over = Mount.Instance.WhyUnderground(at);
                    // the walkable layers' surface right under his feet (not GroundHeight: its navmesh lies under a deck)
                    bool hitG = Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out RaycastHit gh, 0.6f, 6144, QueryTriggerInteraction.Ignore);
                    bool grounded = hitG && Mathf.Abs(gh.point.y - at.y) < 0.12f;
                    if (over != null || !grounded) under.Add($"{what} at {at:F2}: {over ?? ""}{(grounded ? "" : $" the ground under him {(hitG ? gh.point.y.ToString("F2") + " (" + gh.collider.name + ")" : "none within 0.3")}, not at his feet")}");
                }
                float max = 0f;
                bool camera = false, refused = true, took = false;
                IEnumerator One(bool held, float push, bool stopOnTop = false, float pushZ = 0f)
                {
                    max = -99f; camera = false; refused = true; took = false;
                    Flight.TestJumpHeld = held;
                    Flight.TestMoveDir = new Vector2(push, pushZ);
                    try
                    {
                        float start = Time.time;
                        Flight.Jump(i);
                        if (Flight.Now != Flight.Phase.Jumping) { Fail("the jump didn't start: " + Flight.Why); yield break; }
                        int frame = 0;
                        while (Flight.Now == Flight.Phase.Jumping && Time.time - start < Flight.J.Time + Flight.JumpPerchS + 3f)
                        {
                            max = Mathf.Max(max, Flight.HeightPx);
                            camera |= Flight.TestCameraLifted;
                            took |= Flight.Free;
                            if (stopOnTop && Flight.TestJumpPerched) Flight.TestMoveDir = Vector2.zero; // he stays on top of it
                            if (++frame == 5) { float hh = Flight.HeightPx; Flight.Jump(i); refused &= Flight.Now == Flight.Phase.Jumping && Flight.HeightPx == hh; }
                            yield return null;
                        }
                    }
                    finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                    yield return Frames(10);
                    NotUnder($"jump {jumpsSeen + 1} (push {push:0.#},{pushZ:0.#})");
                }

                foreach (bool held in new[] { true, false })
                {
                    Vector3 from = rb.position;
                    yield return One(held, 0f);
                    string what = held ? "held" : "tapped";
                    Check(Flight.Now == Flight.Phase.Ground && Flight.HeightPx == 0f && !Flight.Free, $"{what}, in place: back on the ground, the body the game's again");
                    Check(Mathf.Abs(max - (held ? top : tap)) < 1.01f, $"{what}: the top at {max:0.0} px live (wanted {(held ? top : tap):0})");
                    Check(!Flight.JumpOver || took, $"{what}: the body taken while off the ground (jump over things {(Flight.JumpOver ? "on" : "off")})");
                    Check(Flat(rb.position - from) < 0.05f, $"{what}: landed where it jumped ({Flat(rb.position - from):0.000} away)");
                    Check(!camera, $"{what}: the camera stayed on the ground");
                    Check(refused, $"{what}: a second press in the air refused");
                }

                // two walls across the way, 0.7 to his right (on the Obstacle layer, which stops him): a low one is jumped
                // over, a tall one stops the jump on this side
                int layer = LayerMask.NameToLayer("Obstacle");
                if (!Flight.JumpOver) { Info("jump over things is off: the walls not tried"); yield break; }
                if (layer < 0 || (Mount.Instance.KeeperMask(rb) & (1 << layer)) == 0) { Info("no Obstacle layer stopping the keeper here: the walls not tried"); yield break; }
                float px = 2f * 0.01666667f; // one art pixel, world y
                int said0 = Mount.Instance.TestSaidCount;
                // a low wall 0.7 ahead (jumped over), a tall one 0.7 ahead (stops it: down against it, not sent back),
                // a tall one he is pressed against (0.03 from his side: a jump on the spot, never "on top of it", never sent back)
                // ... and a workbench: wide (2.4), 14 px, come down on and stayed on: he slides off its edge and lands,
                // never sent back (the user, 2026-10-24: a jump onto something built sent him back to where he jumped)
                foreach (int kind in new[] { 0, 1, 2, 3 })
                {
                    bool low = kind == 0, pressed = kind == 2, bench = kind == 3;
                    Vector3 me = rb.position;
                    // pressed: the wall's face 0.03 from his body's side (its own half-width, whatever the keeper's collider is)
                    float half = PC.PhysicalBody.MeshCollider != null ? PC.PhysicalBody.MeshCollider.bounds.extents.x : 0.24f;
                    float wallX = me.x + (pressed ? half + 0.075f + 0.03f : bench ? 0.7f + 1.2f : 0.7f), g = Mount.Instance.GroundHeight(new Vector3(wallX, me.y, me.z), out string src);
                    if (float.IsNaN(g)) { Info($"no ground at the wall's spot ({src}): not tried"); continue; }
                    float high = low ? 8f * px : bench ? 14f * px : 2.5f;
                    walls = new GameObject("gk2donkeyapi-selftest-jump-wall");
                    walls.layer = layer;
                    walls.transform.position = new Vector3(wallX, g, me.z);
                    var c = walls.AddComponent<BoxCollider>();
                    c.center = new Vector3(0f, high / 2f, 0f);
                    c.size = new Vector3(bench ? 2.4f : 0.15f, high, 3f);
                    if (bench)
                    {
                        // a workbench's second collider, taller than its box (the yard's: a box to 6.31, a capsule to 6.43,
                        // its picture to 6.51): carried off the bench, the bench's own colliders must not stop it
                        var cap = walls.AddComponent<CapsuleCollider>();
                        cap.direction = 0;
                        cap.radius = 0.12f;
                        cap.height = 2.4f;
                        cap.center = new Vector3(0f, high * 0.8f, 0f);
                    }
                    Physics.SyncTransforms();
                    TestSay(low ? "Jumping an invisible test wall (8 px)" : pressed ? "Jumping pressed against an invisible test wall" : bench ? "Jumping onto an invisible test bench" : "Jumping at an invisible tall test wall");
                    yield return One(true, 1f, stopOnTop: bench);
                    float x = rb.position.x;
                    string where = $"{x - wallX:+0.00;-0.00} from the wall, the top {max:0} px, the wall {high / px:0} px";
                    if (bench && Mathf.Abs(x - me.x) > 2.2f) Pass($"the touch-down {Mathf.Abs(x - me.x):0.00} from the take-off, past the game's walk grid's reach (4.4 square round the keeper): judged by the NPCs' mesh, not sent back");
                    if (bench) Check(!Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && !Flight.Free && Mathf.Abs(x - wallX) > 1.2f - 0.05f,
                        $"a workbench (2.4 wide, 14 px) come down on{(Flight.TestJumpPerched ? " and stayed on" : " (not on top: over it)")}: down off it, not sent back ({x - wallX:+0.00;-0.00} from its middle{(Flight.TestJumpWentBack ? ", sent back" : "")})");
                    else if (low) Check(x > wallX + 0.2f && Flight.Now == Flight.Phase.Ground && !Flight.Free, "a low wall (8 px) jumped over: down on its far side, " + where);
                    else if (!pressed)
                    {
                        Check(x < wallX - 0.1f && Flight.Now == Flight.Phase.Ground && !Flight.Free, "a tall wall stops the jump (a bare collider: the jump counts its collider): down on this side, " + where);
                        Check(!Flight.TestJumpWentBack && x > me.x + 0.15f, $"and it came down against the wall, not sent back to the start ({x - me.x:+0.00;-0.00} from the start{(Flight.TestJumpWentBack ? ", sent back" : "")})");
                    }
                    else if (pressed) Check(Mathf.Abs(x - me.x) < 0.1f && !Flight.TestJumpPerched && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground,
                        $"pressed against a tall wall: a jump on the spot ({x - me.x:+0.00;-0.00} from the start{(Flight.TestJumpPerched ? ", came down \"on top\" of it" : "")}{(Flight.TestJumpWentBack ? ", sent back" : "")}), no surge along it");
                    walls.SetActive(false);
                    Object.Destroy(walls);
                    walls = null;
                    Physics.SyncTransforms();
                    // back to where it jumped from for the next wall
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }
                Check(Mount.Instance.TestSaidCount == said0, $"no bubble from any of these jumps (refused, pushed out or carried off: {Mount.Instance.TestSaidCount - said0} said)");

                // the speed: never faster in the air than he rides on the ground (the user: "you accelerate forward")
                Flight.TestSetRideTop(3f);
                try
                {
                    yield return One(true, 1f);
                    Check(Flight.TestJumpTopAirSpeed <= 3f * 1.05f + 0.01f, $"a running jump never goes faster than the ride (its fastest step {Flight.TestJumpTopAirSpeed:0.00} a second, the ride's top set to 3.00)");
                }
                finally { Flight.TestSetRideTop(0f); } // forgotten again: left at 3 it capped every later jump (the yard's, the player's)
                PutKeeper(rb.position, homeFacing);
                yield return Frames(10);

                GameObject Box(string name, int lay, Vector3 at, Vector3 size, Vector3 centre)
                {
                    var o = new GameObject(name);
                    o.layer = lay;
                    o.transform.position = at;
                    var bc = o.AddComponent<BoxCollider>();
                    bc.size = size;
                    bc.center = centre;
                    return o;
                }
                int groundLay = LayerMask.NameToLayer("Ground");

                // a deck (a bridge's: the ground's layer, 0.3 over the ground, the NPCs' mesh under it): jumped onto, and
                // from on it, he is never put under it (the flight's rule set the touch-down to the mesh's height under it)
                if (groundLay >= 0)
                {
                    Vector3 me = rb.position;
                    float g0 = Mount.Instance.GroundHeight(me, out _);
                    float deckTop = (float.IsNaN(g0) ? me.y : g0) + 0.3f;
                    walls = Box("gk2donkeyapi-selftest-jump-deck", groundLay, new Vector3(me.x + 0.9f + 1.5f, deckTop - 0.15f, me.z), new Vector3(3f, 0.3f, 3f), Vector3.zero);
                    Physics.SyncTransforms();
                    yield return One(true, 1f);
                    Vector3 at = rb.position;
                    bool overDeck = at.x > me.x + 0.9f && at.x < me.x + 3.9f;
                    Check(!overDeck || at.y >= deckTop - 0.05f, $"onto a deck 0.3 high: {(overDeck ? $"on its top ({at.y:F2}, the top {deckTop:F2})" : $"not on it ({at.x - me.x:+0.00;-0.00} from the start)")}, never under it");
                    if (overDeck)
                    {
                        yield return One(true, 0f);
                        Check(rb.position.y >= deckTop - 0.05f, $"a jump from on the deck lands on it again ({rb.position.y:F2}, the top {deckTop:F2})");
                    }
                    walls.SetActive(false); Object.Destroy(walls); walls = null; Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }

                // jump sliding: into a tall wall at an angle, he slides along it, ends beside it, on the ground
                {
                    Vector3 me = rb.position;
                    float wallX = me.x + 0.8f;
                    walls = Box("gk2donkeyapi-selftest-jump-slide", layer, new Vector3(wallX, me.y, me.z), new Vector3(0.15f, 2.5f, 6f), new Vector3(0f, 1.25f, 0f));
                    Physics.SyncTransforms();
                    yield return One(true, 1f, pushZ: 0.8f);
                    Vector3 at = rb.position;
                    Check(at.x < wallX - 0.1f && at.z - me.z > 0.2f && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && !Flight.Free,
                        $"a jump into a wall at an angle slides along it: {at.x - wallX:+0.00;-0.00} from the wall, {at.z - me.z:+0.00;-0.00} along it{(Flight.TestJumpWentBack ? ", sent back" : "")}");
                    walls.SetActive(false); Object.Destroy(walls); walls = null; Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }

                // built things: a building (3 x 3, 2.5 tall) is never jumped into or over; a row of two low things (a
                // fence's rails, 8 px) is jumped across
                {
                    Vector3 me = rb.position;
                    walls = Box("gk2donkeyapi-selftest-jump-building", layer, new Vector3(me.x + 1f + 1.5f, me.y, me.z), new Vector3(3f, 2.5f, 3f), new Vector3(0f, 1.25f, 0f));
                    Physics.SyncTransforms();
                    yield return One(true, 1f);
                    Vector3 at = rb.position;
                    Check(at.x < me.x + 1f - 0.1f && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground, $"a building stops the jump: down before its wall ({at.x - me.x - 1f:+0.00;-0.00} from it)");
                    walls.SetActive(false); Object.Destroy(walls); walls = null; Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                    walls = new GameObject("gk2donkeyapi-selftest-jump-row");
                    walls.layer = layer;
                    walls.transform.position = me;
                    foreach (float rx in new[] { 0.7f, 1.4f })
                    {
                        var rail = walls.AddComponent<BoxCollider>();
                        rail.center = new Vector3(rx, 4f * px, 0f);
                        rail.size = new Vector3(0.12f, 8f * px, 3f);
                    }
                    Physics.SyncTransforms();
                    yield return One(true, 1f);
                    at = rb.position;
                    Check(at.x > me.x + 0.7f + 0.15f && !Flight.TestJumpWentBack && Flight.Now == Flight.Phase.Ground && !Flight.Free,
                        $"a row of two low things jumped across: down {at.x - me.x:+0.00;-0.00} from the start (the rails at +0.70 and +1.40){(Flight.TestJumpWentBack ? ", sent back" : "")}");
                    walls.SetActive(false); Object.Destroy(walls); walls = null; Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }

                // a slope (the bridge's jolts): two ramps of the ground's layer rising away from him both ways, so the
                // ground under the mount's body (half a unit to its side) is higher than under him; a jump in place: the
                // mount's drawn place less the jump's height never steps (it stepped at the take and the touch-down)
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer < 0) Info("no Ground layer: the slope not tried");
                else
                {
                    Vector3 me = rb.position;
                    float g0 = Mount.Instance.GroundHeight(me, out _);
                    walls = new GameObject("gk2donkeyapi-selftest-jump-ramps");
                    walls.layer = groundLayer;
                    walls.transform.position = new Vector3(me.x, float.IsNaN(g0) ? me.y : g0, me.z);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var ramp = new GameObject("ramp");
                        ramp.layer = groundLayer;
                        ramp.transform.SetParent(walls.transform, false);
                        ramp.transform.localRotation = Quaternion.Euler(0f, 0f, 30f * side);
                        // its top face through the ground under him at his feet, rising away from him
                        ramp.transform.localPosition = Quaternion.Euler(0f, 0f, 30f * side) * new Vector3(side * 1.5f, -0.05f, 0f) + new Vector3(side * 0.3f, 0f, 0f);
                        var box = ramp.AddComponent<BoxCollider>();
                        box.size = new Vector3(3f, 0.1f, 2f);
                    }
                    Physics.SyncTransforms();
                    yield return Frames(10);
                    Mount.Instance mi = Mount.Ridden;
                    float last = float.NaN, slopeWorst = 0f, worstAt = 0f;
                    Flight.TestJumpHeld = true;
                    Flight.TestMoveDir = Vector2.zero;
                    try
                    {
                        Flight.Jump(mi);
                        float t0 = Time.time;
                        while (Time.time - t0 < Flight.J.Time + 1.5f)
                        {
                            float drawn = mi.Position.y - Flight.LiftFor(mi);
                            if (!float.IsNaN(last) && Mathf.Abs(drawn - last) > slopeWorst) { slopeWorst = Mathf.Abs(drawn - last); worstAt = Time.time - t0; }
                            last = drawn;
                            if (Flight.Now != Flight.Phase.Jumping && Time.time - t0 > 0.2f) break;
                            yield return null;
                        }
                    }
                    finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                    NotUnder("the jump on the slope");
                    Check(slopeWorst <= 1.5f * px, $"on a slope the mount's ground stays put through the jump: its biggest step {slopeWorst / px:0.0} px (at {worstAt:0.00} s; it stepped at the take and the touch-down on the bridge)");
                    walls.SetActive(false);
                    Object.Destroy(walls);
                    walls = null;
                    Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }
                // downhill (the user's slope jumps, 2026-10-24 03:18: +30 px mid-jump, -51 px at the touch-down): a ramp of the
                // ground's layer from 1.2 up at the keeper down to the ground 5 ahead; a running jump down it: the ground the
                // mount is drawn on never steps, and the body comes down on the ramp, not dropped at the end
                if (groundLayer >= 0)
                {
                    Vector3 me = rb.position;
                    float g0 = Mount.Instance.GroundHeight(me, out _);
                    if (float.IsNaN(g0)) g0 = me.y;
                    const float len = 5f, drop = 1.2f;
                    float ang = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                    walls = new GameObject("gk2donkeyapi-selftest-jump-downhill");
                    walls.layer = groundLayer;
                    walls.transform.position = new Vector3(me.x + len / 2f, g0 + drop / 2f - 0.05f, me.z);
                    walls.transform.rotation = Quaternion.Euler(0f, 0f, -ang); // +x runs down
                    var ramp = walls.AddComponent<BoxCollider>();
                    ramp.size = new Vector3(Mathf.Sqrt(len * len + drop * drop), 0.1f, 2.5f);
                    Physics.SyncTransforms();
                    PutKeeper(new Vector3(me.x + 0.5f, g0 + drop * (1f - 0.5f / len) + 0.03f, me.z), 1f);
                    yield return Frames(12);
                    Mount.Instance mi = Mount.Ridden;
                    float last = float.NaN, worstDown = 0f, worstAt = 0f;
                    Vector3 before = rb.position;
                    Flight.TestJumpHeld = true;
                    Flight.TestMoveDir = new Vector2(1f, 0f);
                    try
                    {
                        Flight.Jump(mi);
                        float t0 = Time.time;
                        while (Time.time - t0 < Flight.J.Time + 2f)
                        {
                            float drawn = mi.Position.y - Flight.LiftFor(mi);
                            if (!float.IsNaN(last) && Mathf.Abs(drawn - last) > worstDown) { worstDown = Mathf.Abs(drawn - last); worstAt = Time.time - t0; }
                            last = drawn;
                            if (Flight.Now != Flight.Phase.Jumping && Time.time - t0 > 0.2f) break;
                            yield return null;
                        }
                    }
                    finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                    yield return Frames(5);
                    Vector3 at = rb.position;
                    bool onRamp = Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out RaycastHit rh, 0.6f, GroundLayersMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(rh.point.y - at.y) < 0.12f;
                    // 4 px: the touch-down frame on this 13-degree ramp read 2.5 (the last kinematic step and the game's walk in one
                    // frame, the ramp's slope under the mount's snap point half a unit ahead); the cut this guards against was 30 and 51
                    Check(worstDown <= 4f * px && onRamp && at.x > before.x + 1f && at.y < before.y - 0.2f,
                        $"a running jump downhill: the ground the mount is drawn on steps at most {worstDown / px:0.0} px a frame (at {worstAt:0.00} s), down the ramp {at.x - before.x:0.00} along and {before.y - at.y:0.00} lower, on its surface{(onRamp ? "" : " (NOT on the ground)")}");
                    NotUnder("the downhill jump");
                    walls.SetActive(false);
                    Object.Destroy(walls);
                    walls = null;
                    Physics.SyncTransforms();
                    PutKeeper(me, homeFacing);
                    yield return Frames(10);
                }
                Check(under.Count == 0, $"never underground: after each of the {jumpsSeen} jumps nothing of the ground's layers over him and ground at his feet" + (under.Count > 0 ? ": " + string.Join("; ", under) : ""));
                Check(SafeSpots.TestReturns == returns0, $"the fall watchdog never fired through these jumps ({SafeSpots.TestReturns - returns0} return(s){(SafeSpots.TestReturns != returns0 ? ": " + SafeSpots.LastReason : "")})");
            }
            finally
            {
                Flight.TestJumpHeld = null; Flight.TestMoveDir = null;
                if (walls != null) { walls.SetActive(false); Object.Destroy(walls); }
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (donkey != null && donkey.Exists) donkey.Remove(); } catch { }
            }
        }

        internal static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // The [Mount] flags flies and jumps (docs/flying.md "Jump and fly on one key"): the donkey's (it jumps and
        // doesn't fly), a pack's own read and listed, written back only when not the default.
        internal static IEnumerator FlightFlags()
        {
            const string owner = Owner + ".flags";
            Check(!Catalog.Donkey.Flies && Catalog.Donkey.Jumps, "the donkey jumps and doesn't fly");

            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-flags", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Flags Test\nmount = selftest.flags-beast\n\n[Mount]\nid = selftest.flags-beast\nname = Flags Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\njumps = false\n");
            }
            catch (System.Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            try
            {
                Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the pack registered");
                MountInfo k = Mounts.Catalog.Find("selftest.flags-beast");
                Check(k != null && k.Flies && !k.Jumps, $"a pack's flies = true and jumps = false are its mount's ({k?.Flies}, {k?.Jumps})");
                Looks.SkinDef def = Looks.Find("selftest.flags-beast/default");
                Catalog.Entry kd = Catalog.Find("selftest.flags-beast");
                if (def != null && kd != null)
                {
                    string file = TempMountCfg(tempRoot, "selftest.flags-beast"); // the mount's values: its mount.cfg
                    kd.Jumps = true; // the Mounts tab's "Jumps" toggle does this
                    string err = Looks.SaveMountCfg(kd);
                    string text = System.IO.File.ReadAllText(file).Replace("\r\n", "\n");
                    Check(err == null && text.Contains("\nflies = true\n") && !text.Contains("jumps ="), "saved: flies = true kept, jumps (now the default) left out" + (err != null ? " (" + err + ")" : ""));
                    kd.Flies = false;
                    err = Looks.SaveMountCfg(kd);
                    text = System.IO.File.ReadAllText(file).Replace("\r\n", "\n");
                    Check(err == null && !text.Contains("flies =") && !text.Contains("jumps ="), "saved with both at the default: neither written");
                    kd.Flies = true;
                    err = Looks.ReloadMountCfg(kd);
                    Check(err == null && !kd.Flies && kd.Jumps, $"reloaded: the file's values are the mount's (flies {kd.Flies}, jumps {kd.Jumps})");
                }
                else Fail("the pack's skin or mount not found");
            }
            finally
            {
                Looks.Unregister("selftest.flags-beast/default", owner);
                try { System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
            yield return Frames(3);
            Check(Mounts.Catalog.Find("selftest.flags-beast") == null, "the mount goes with its pack");
        }

        // One key for jump and fly (the skyriding rule): a press jumps; a second press in the air takes off a mount that
        // flies (by its flag: a pack's mount, not a shipped one), the height and the body carried on, the camera easing up;
        // a press while flying lands. The donkey: the second press does nothing. Two keys: the fly key takes off from
        // the ground, the jump key only jumps. A flier that doesn't jump: one press takes off. Driven through
        // Flight.Press, as the keys would.
        internal static IEnumerator FlightOneKey()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors: flying indoors is off"); yield break; }
            const string owner = Owner + ".onekey";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-onekey", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = One Key Test\nmount = selftest.onekey-beast\n\n[Mount]\nid = selftest.onekey-beast\nname = One Key Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\n");
            }
            catch (System.Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle h = null;
            Catalog.Entry kd = null;
            try
            {
                Looks.RegisterFolder(tempRoot, owner);
                kd = Catalog.Find("selftest.onekey-beast");
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null || kd == null) { Fail("could not spawn, or the pack's mount is missing"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "selftest.onekey-beast/default");
                yield return Until(() => h.Mount == "selftest.onekey-beast", 8f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance i = Mount.Ridden;
                Flight.TestJumpHeld = true;

                IEnumerator Down(string what)
                {
                    float t0 = Time.time;
                    while (Flight.Now != Flight.Phase.Ground && Time.time - t0 < 20f) yield return null;
                    Check(Flight.Now == Flight.Phase.Ground, $"{what}: down again ({Flight.Now}, {Flight.HeightPx:0} px; {Flight.Why})");
                    if (Flight.Now != Flight.Phase.Ground) Flight.Release(i, "self-test");
                    yield return Frames(10);
                }
                IEnumerator InTheAir()
                {
                    float t0 = Time.time;
                    while (Flight.Now == Flight.Phase.Jumping && Flight.HeightPx < 8f && Time.time - t0 < 1f) yield return null;
                }

                // 1. one key, a flier: a press jumps, a second in the air takes off
                Flight.Press(i, true, false, same: true);
                Check(Flight.Now == Flight.Phase.Jumping, $"one key: the first press jumps ({Flight.Now})");
                yield return InTheAir();
                float before = Flight.HeightPx;
                bool freeBefore = Flight.Free;
                Flight.Press(i, true, false, same: true);
                Check(Flight.Now == Flight.Phase.Rising && Mathf.Abs(Flight.HeightPx - before) < 0.01f, $"one key: the second press in the air takes off from the jump's height ({Flight.Now}, {before:0.0} -> {Flight.HeightPx:0.0} px)");
                Check(!Flight.JumpOver || (freeBefore && Flight.Free), $"the body stays the mod's from the jump into the flight (free {freeBefore} -> {Flight.Free})");
                // against the mount, not the screen: the gap between the flight and the camera closes smoothly (a snap
                // would close the jump's whole height in one frame); the take-off's own climb moves both alike
                // the camera's share of the height is the tab's "Camera rises with the height" (CameraFollow, 1 as shipped)
                float share = Mathf.Clamp01(Flight.CameraFollow);
                float lastGap = Flight.HeightPx * share - Flight.TestCameraLiftPx, worstStep = 0f, easeT0 = Time.time;
                int frames = 0;
                while (Time.time - easeT0 < 0.45f && frames++ < 600) // the ease's 0.3 s and some, in time, not frames
                {
                    yield return null;
                    float gap = Flight.HeightPx * share - Flight.TestCameraLiftPx;
                    worstStep = Mathf.Max(worstStep, Mathf.Abs(gap - lastGap));
                    lastGap = gap;
                }
                Check(worstStep <= Mathf.Max(3f, before * 0.35f) && Mathf.Abs(lastGap) < 1.01f,
                    $"the camera eases up to the flight: the gap between them closes by at most {worstStep:0.0} px a frame (it stayed down for the jump, {before:0} px under it at the press), {lastGap:0.0} px after {Time.time - easeT0:0.00} s, {frames} frames (the camera's share {share:0.00}, the ease {Flight.TestCameraEase:0.00}, the flight at {Flight.HeightPx:0} px)");
                Flight.Press(i, true, false, same: true);
                Check(Flight.Now == Flight.Phase.Landing, $"one key: a press while flying lands ({Flight.Now})");
                yield return Down("the flight from a jump");

                // 2. a flier that doesn't jump: one press takes off
                kd.Jumps = false;
                Flight.Press(i, true, false, same: true);
                Check(Flight.Now == Flight.Phase.Rising, $"a flier with jumps = false: one press takes off ({Flight.Now})");
                Flight.Press(i, true, false, same: true);
                kd.Jumps = true;
                yield return Down("its flight");

                // 3. two keys: the fly key takes off from the ground, the jump key only jumps
                Flight.Press(i, false, true, same: false);
                Check(Flight.Now == Flight.Phase.Rising, $"two keys: the fly key takes off from the ground ({Flight.Now})");
                Flight.Press(i, false, true, same: false);
                Check(Flight.Now == Flight.Phase.Landing, $"two keys: the fly key lands ({Flight.Now})");
                yield return Down("the fly key's flight");
                Flight.Press(i, true, false, same: false);
                yield return InTheAir();
                Flight.Press(i, true, false, same: false);
                Check(Flight.Now == Flight.Phase.Jumping, $"two keys: the jump key again in the air does nothing ({Flight.Now})");
                yield return Down("the jump");

                // 4. the API: Mounts.Jump / TakeOff / Land, FlightState, FlightChanged
                var seen = new System.Collections.Generic.List<MountFlight>();
                System.Action<MountFlight> on = st => seen.Add(st);
                Mounts.FlightChanged += on;
                try
                {
                    MountResult no = Mounts.Land();
                    Check(!no.Ok && Mounts.FlightState == MountFlight.Ground, $"API: Land on the ground refused ({Said(no)})");
                    MountResult j = Mounts.Jump();
                    Check(j.Ok && Mounts.FlightState == MountFlight.Jumping, $"API: Jump ({Said(j)}; {Mounts.FlightState})");
                    no = Mounts.Jump();
                    Check(!no.Ok, $"API: a second Jump mid-jump refused ({Said(no)})");
                    yield return InTheAir();
                    MountResult t = Mounts.TakeOff();
                    Check(t.Ok && Mounts.FlightState == MountFlight.TakingOff, $"API: TakeOff in the air turns the jump into the take-off ({Said(t)}; {Mounts.FlightState})");
                    no = Mounts.TakeOff();
                    Check(!no.Ok, $"API: TakeOff while flying refused ({Said(no)})");
                    MountResult l = Mounts.Land();
                    Check(l.Ok && Mounts.FlightState == MountFlight.Landing, $"API: Land ({Said(l)}; {Mounts.FlightState})");
                    yield return Down("the API's flight");
                    Check(Mounts.FlightState == MountFlight.Ground, $"API: FlightState back to Ground ({Mounts.FlightState})");
                    string got = string.Join(", ", seen);
                    Check(seen.Count >= 4 && seen[0] == MountFlight.Jumping && seen[1] == MountFlight.TakingOff && seen.Contains(MountFlight.Landing) && seen[seen.Count - 1] == MountFlight.Ground,
                        $"API: FlightChanged told each step ({got})");
                }
                finally { Mounts.FlightChanged -= on; }

                // 5. the donkey (no flies): the second press does nothing, the jump lands
                h.Tuning.Set(Owner, "Skin", "");
                yield return Until(() => h.Mount == MountCatalog.DonkeyId, 8f);
                yield return Frames(5);
                i = Mount.Ridden;
                Flight.Press(i, true, false, same: true);
                yield return InTheAir();
                Flight.Press(i, true, false, same: true);
                Check(Flight.Now == Flight.Phase.Jumping, $"the donkey: the second press in the air does nothing (it doesn't fly): {Flight.Now}");
                MountResult notOff = Mounts.TakeOff();
                Check(!notOff.Ok && Flight.Now == Flight.Phase.Jumping, $"the donkey: API TakeOff refused with the reason ({Said(notOff)})");
                yield return Down("the donkey's jump");
            }
            finally
            {
                Flight.TestJumpHeld = null;
                if (kd != null) kd.Jumps = true;
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
                Looks.Unregister("selftest.onekey-beast/default", owner);
                try { System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // In the air (docs/flying.md "In the air: no prompts, no work state, parked on the ground"): the game's
        // interaction component is paused through a flight (its prompts and bubbles gone), not through a jump; its work
        // state (F held) is refused while flying (the patched CanEnter); a ride ended in the air (the mod's dismount,
        // as a state change would) parks the mount on the ground under where it flew, the prompts back, nothing fallen.
        internal static IEnumerator FlightInTheAir()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors: flying indoors is off"); yield break; }
            PlayerInteractionComponent ic = PC.PlayerInteractionComponent;
            if (ic == null) { Skip("the keeper has no interaction component"); yield break; }
            if (ic.IsPaused) { Skip("the game has its interactions paused already (a window, a ladder, work)"); yield break; }
            const string owner = Owner + ".inair";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-inair", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = In The Air Test\nmount = selftest.inair-beast\n\n[Mount]\nid = selftest.inair-beast\nname = In The Air Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\n");
            }
            catch (System.Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle h = null;
            int returns = SafeSpots.TestReturns;
            try
            {
                Looks.RegisterFolder(tempRoot, owner);
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null || Catalog.Find("selftest.inair-beast") == null) { Fail("could not spawn, or the pack's mount is missing"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "selftest.inair-beast/default");
                yield return Until(() => h.Mount == "selftest.inair-beast", 8f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance i = Mount.Ridden;
                if (i == null) { Fail("not riding"); yield break; }
                Flight.TestJumpHeld = true;
                Check(!ic.IsPaused && !Flight.TestInteractionPaused, "riding on the ground: the game's interactions are its own (not paused)");
                // the game's bubbles are screen-space UI (placed by CameraSystem.WorldToScreenPoint): over every sprite
                Canvas bubbleCanvas = null;
                try { bubbleCanvas = UIObjectBubbleManager.Instance != null ? UIObjectBubbleManager.Instance.GetComponent<Canvas>() : null; } catch { }
                Canvas rootCanvas = bubbleCanvas != null ? bubbleCanvas.rootCanvas : null;
                Info($"the bubble canvas: {(bubbleCanvas == null ? "not in this scene" : $"{bubbleCanvas.renderMode}, order {bubbleCanvas.sortingOrder}, its root '{rootCanvas?.name}' {rootCanvas?.renderMode} on camera {(rootCanvas != null && rootCanvas.worldCamera != null ? rootCanvas.worldCamera.name : "none")}")}");

                // 1. a jump leaves them alone
                MountResult j = Mounts.Jump();
                if (!j.Ok) { Fail($"no jump ({Said(j)})"); yield break; }
                float t0 = Time.time;
                while (Flight.Now == Flight.Phase.Jumping && Flight.HeightPx < 8f && Time.time - t0 < 1f) yield return null;
                Check(Flight.Now == Flight.Phase.Jumping && !ic.IsPaused && !Flight.TestInteractionPaused, $"a jump: the prompts stay ({Flight.Now}, {Flight.HeightPx:0} px, paused {ic.IsPaused})");
                t0 = Time.time;
                while (Flight.Now != Flight.Phase.Ground && Time.time - t0 < 10f) yield return null;
                if (Flight.Now != Flight.Phase.Ground) { Fail($"the jump never landed ({Flight.Now}: {Flight.Why})"); yield break; }
                yield return Frames(5);

                // 2. a flight pauses them, and the work state is refused
                MountResult t = Mounts.TakeOff();
                if (!t.Ok) { Fail($"no take-off ({Said(t)})"); yield break; }
                yield return Until(() => Flight.Now == Flight.Phase.Airborne || Flight.HeightPx >= 24f, 6f);
                Check(Flight.Flying && ic.IsPaused && Flight.TestInteractionPaused && !ic.HasWgoUnderInteraction && ic.BigDropUnderInteraction == null,
                    $"flying ({Flight.Now}, {Flight.HeightPx:0} px): the game's interactions paused (paused {ic.IsPaused}, a thing under interaction {ic.HasWgoUnderInteraction}, a big drop {(ic.BigDropUnderInteraction != null)})");
                WorkPlayerState ws = null;
                try { ws = PC.Ssm?.GetState<WorkPlayerState>(); } catch (System.Exception e) { Warn("the work state could not be read: " + e.Message); }
                if (ws != null)
                {
                    int blocked = WorkPlayerState_CanEnter_Patch.Blocked;
                    bool can = ws.CanEnter;
                    Check(!can && WorkPlayerState_CanEnter_Patch.Blocked == blocked + 1, $"the work state's CanEnter asked while flying: refused by the patch ({(can ? "it could enter" : "no")}, refusals {blocked} -> {WorkPlayerState_CanEnter_Patch.Blocked})");
                }

                // 3. the ride ended up there (as the game's state change does): the mount parked on the ground under it
                float flewAt = i.Position.y, lift = i.TestLiftDrawn; // the lift in the place (the hover moves between a Follow and now)
                Mount.DismountFromApi("self-test: a ride ended in the air");
                yield return Frames(3);
                string groundSrc;
                float ground = Mount.Instance.GroundHeight(new Vector3(i.Position.x, flewAt, i.Position.z), out groundSrc);
                bool groundKnown = !float.IsNaN(ground) && !float.IsInfinity(ground);
                Check(!Mounts.IsMounted && Flight.Now == Flight.Phase.Ground, $"the ride ended in the air: off, the flight over ({Flight.Now}: {Flight.Why})");
                Check(!ic.IsPaused && !Flight.TestInteractionPaused, $"the prompts are the game's again (paused {ic.IsPaused})");
                Check(lift > 0.3f && i.Position.y < flewAt - lift + 0.1f && (!groundKnown || Mathf.Abs(i.Position.y - ground) <= 0.6f),
                    $"parked under where it flew: at y {i.Position.y:0.00} (it flew at {flewAt:0.00}, the lift {lift:0.00}; the ground there {(groundKnown ? ground.ToString("0.00") + " by " + groundSrc : "unknown")})");
                string under = Mount.Instance.WhyUnderground(PC.PhysicalBody.Rb.position);
                Check(under == null && SafeSpots.TestReturns == returns, $"the keeper on the ground, never under it ({under ?? "fine"}), the fall watchdog silent ({SafeSpots.TestReturns - returns} return(s))");
            }
            finally
            {
                Flight.TestJumpHeld = null;
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
                Looks.Unregister("selftest.inair-beast/default", owner);
                try { System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // A jump beside a real thing (the user's yard, 2026-10-24: every jump sent back was beside a workbench, refused
        // "no walkable ground" where he stood): a spot beside a built or placed thing near the keeper where the flight's
        // rule refuses the ground (Mount.Instance.FindBesideSpot); a jump in place there lands in place.
        internal static IEnumerator FlightJumpBeside()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors: the jump stays in place there"); yield break; }
            MountHandle donkey = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
            try
            {
                if (donkey == null) { Fail("could not spawn the test's donkey"); yield break; }
                yield return Until(() => donkey.IsReady, 6f);
                MountResult r = donkey.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                if (!Mount.Instance.FindBesideSpot(PC, 14f, out Vector3 spot, out string what) && !Mount.Instance.FindOffMeshSpot(PC, 14f, out _, out _))
                {
                    Skip("no thing near the keeper where the flight's rule refuses the ground beside it, and no ground the NPCs' mesh doesn't cover: run this one from the yard or by a bridge");
                    yield break;
                }
                if (what == null) { Info("no thing near here to jump beside (the yard's case not tried)"); spot = PC.PhysicalBody.Rb.position; }
                Info(what != null ? "beside " + what + $" at {spot:F2}" : $"no thing near here: a jump in place where he stands, {spot:F2} (the yard's things: the home-yard scenario)");
                PutKeeper(spot, homeFacing);
                yield return Frames(15);
                Rigidbody rb = PC.PhysicalBody.Rb;
                Vector3 from = rb.position;
                Mount.Instance i = Mount.Ridden;
                Flight.TestJumpHeld = false;
                Flight.TestMoveDir = Vector2.zero;
                try
                {
                    Flight.Jump(i);
                    float t0 = Time.time;
                    while (Flight.Now == Flight.Phase.Jumping && Time.time - t0 < 4f) yield return null;
                }
                finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                yield return Frames(5);
                float moved = new Vector2(rb.position.x - from.x, rb.position.z - from.z).magnitude;
                Check(Flight.Now == Flight.Phase.Ground && !Flight.Free && !Flight.TestJumpWentBack && moved < 0.15f,
                    $"a jump in place beside it lands in place ({moved:0.000} from where it jumped{(Flight.TestJumpWentBack ? ", sent back" : "")}; {Flight.Now}; {Flight.Why})");

                // ground the NPCs' mesh doesn't cover (a bridge's deck)
                if (!Mount.Instance.FindOffMeshSpot(PC, 14f, out Vector3 deck, out string deckWhat))
                {
                    Info("no ground at his level near here that the NPCs' mesh doesn't cover (a bridge's deck): that case not tried; run this one by a bridge");
                    yield break;
                }
                Info("off the NPCs' mesh: " + deckWhat + $" at {deck:F2}");
                PutKeeper(deck, homeFacing);
                yield return Frames(15);
                from = rb.position;
                i = Mount.Ridden;
                Flight.TestJumpHeld = false;
                Flight.TestMoveDir = Vector2.zero;
                try
                {
                    Flight.Jump(i);
                    float t1 = Time.time;
                    while (Flight.Now == Flight.Phase.Jumping && Time.time - t1 < 4f) yield return null;
                }
                finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                yield return Frames(5);
                moved = new Vector2(rb.position.x - from.x, rb.position.z - from.z).magnitude;
                Check(Flight.Now == Flight.Phase.Ground && !Flight.Free && moved < 0.15f,
                    $"a jump in place on ground the NPCs' mesh doesn't cover lands in place ({moved:0.000} from where it jumped{(Flight.TestJumpWentBack ? ", sent back" : "")}; {Flight.Why})");
            }
            finally
            {
                Flight.TestJumpHeld = null; Flight.TestMoveDir = null;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (donkey != null && donkey.Exists) donkey.Remove(); } catch { }
            }
        }

        internal static IEnumerator FlightWalkAway()
        {
            Rigidbody rb = PC?.PhysicalBody?.Rb;
            Collider body = PC?.PhysicalBody?.MeshCollider;
            if (rb == null || body == null || PC.SceneRecastGraph == null)
            {
                Skip("no keeper's body or no walkable mesh in this scene");
                yield break;
            }
            if (Mount.Instance.IsIndoors())
            {
                Skip("indoors: the test wants open ground round the keeper");
                yield break;
            }
            Vector3 me = rb.position;
            int mine = Mount.Instance.MeshAreaAt(me, out float off, out _);
            if (mine < 0)
            {
                Skip($"the keeper stands off the walkable mesh ({off:0.00} from it): stand on open ground");
                yield break;
            }

            // 1. his own spot: on his own part of the mesh at the first cell
            string why = Mount.Instance.WalkAway(rb, body, me, mine, out string how);
            Check(why == null, "from his own spot the walk reaches his own part of the mesh: " + how);

            // 2. the longest judgement: open ground with no part of the mesh to find, the spread runs to the open-ground
            //    size; in game it is sliced 4 ms a frame (the landing hovers meanwhile). Run once before timing it (the
            //    first run pays for the JIT), then at once and in slices: the two must agree.
            Mount.Instance.WalkAway(rb, body, me, int.MinValue, out _);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            why = Mount.Instance.WalkAway(rb, body, me, int.MinValue, out how);
            float ms = (float)watch.Elapsed.TotalMilliseconds;
            Check(why == null && how.StartsWith("open ground", System.StringComparison.Ordinal), "from his spot, with no part of the mesh to find, his ground is open ground: " + (why ?? how));
            Info($"the whole spread took {ms:0.0} ms, about {Mathf.CeilToInt(ms / Mount.Instance.WalkSliceMs)} frame(s) of hovering at {Mount.Instance.WalkSliceMs:0} ms a frame");
            Check(ms < 200f, $"the longest judgement under 200 ms in all ({ms:0.0})");
            string sliced = Mount.Instance.WalkAwaySliced(rb, body, me, int.MinValue, 0.2f, out string slicedHow, out int slices);
            Check(sliced == why && slices > 1 && Steps(slicedHow) == Steps(how), $"in slices of 0.2 ms the same answer and the same steps ({slices} slices: {sliced ?? slicedHow})");

            // 3. a pen: four walls 1.6 apart round a spot 2.5 to his side, on a layer that stops him
            int layer = LayerMask.NameToLayer("Obstacle");
            if (layer < 0 || (Mount.Instance.KeeperMask(rb) & (1 << layer)) == 0)
            {
                Info(layer < 0 ? "no layer called Obstacle: the pen not built" : "the Obstacle layer doesn't stop the keeper here: the pen not built");
                yield break;
            }
            Vector3 at = me + new Vector3(2.5f, 0f, 0f);
            float g = Mount.Instance.GroundHeight(at, out string src);
            if (float.IsNaN(g))
            {
                Info($"no ground at the pen's spot {at:F2} ({src}): the pen not built");
                yield break;
            }
            at.y = g;
            var pen = new GameObject("gk2donkeyapi-selftest-pen");
            try
            {
                pen.layer = layer;
                pen.transform.position = at; // on the ground there, not at his height (a slope)
                const float half = 0.8f, thick = 0.15f, high = 2f;
                void Wall(Vector3 centre, Vector3 size)
                {
                    var c = pen.AddComponent<BoxCollider>();
                    c.center = centre;
                    c.size = size;
                }
                Wall(new Vector3(half, high / 2f, 0f), new Vector3(thick, high, 2f * half + thick));
                Wall(new Vector3(-half, high / 2f, 0f), new Vector3(thick, high, 2f * half + thick));
                Wall(new Vector3(0f, high / 2f, half), new Vector3(2f * half + thick, high, thick));
                Wall(new Vector3(0f, high / 2f, -half), new Vector3(2f * half + thick, high, thick));
                Physics.SyncTransforms();
                why = Mount.Instance.WalkAway(rb, body, at, int.MinValue, out how);
                Check(why != null && why.StartsWith("walled in", System.StringComparison.Ordinal), "the spot inside the pen is walled in: " + (why ?? how));
                why = Mount.Instance.WalkAway(rb, body, at, mine, out how);
                Info("inside the pen, to his own part of the mesh (the NPCs' mesh doesn't know the pen; a cell on his part is a yes): " + (why ?? how));
            }
            finally
            {
                // gone for the tests after this one at once (Destroy waits for the frame's end)
                pen.SetActive(false);
                Object.Destroy(pen);
                Physics.SyncTransforms();
            }
        }

        // "... (536 steps, 18.7 ms over 1 frame(s))" -> 536
        internal static int Steps(string how)
        {
            if (how == null) return -1;
            int at = how.LastIndexOf(" steps, ", System.StringComparison.Ordinal);
            if (at <= 0) return -1;
            int from = how.LastIndexOf('(', at);
            return from >= 0 && int.TryParse(how.Substring(from + 1, at - from - 1), out int n) ? n : -1;
        }
    }
}
