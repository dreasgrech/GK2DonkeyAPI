using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI
{
    // A baby with no room beside its lead, and the stuck report on babies that are still by design
    // (docs/foals-catch-up-loop.md; the user, 2026-11-06: "investigate these: ... the catch-up loop, and the stuck report
    // for babies that are asleep", then A + B2). Temporary walls on the Obstacle layer, as the flight tests' pen.
    internal static class FoalRoomTests
    {
        private const int BlockingLayer = 8; // what the shuffle's line test checks (IdleShuffle.cs BlockingLayers)

        // A baby beside its parent, facing away from it, in a U of walls open towards the parent: no step and no turn
        // fits, every other slot is behind a wall, and the spot a catch-up gives it is the one it stands on. Before the
        // fix (rehearsal of 2026-11-05) it was wedged, "followed again" to where it stood, and was put back on its own
        // spot in view, over and over. Now it is wedged, finds nowhere to go, and stands: no rejoin, no catch-up.
        internal static IEnumerator NoRoom()
        {
            if (Mount.Instance.IsIndoors())
            {
                Skip("indoors: the test wants open ground round the keeper");
                yield break;
            }
            if (LayerMask.NameToLayer("Obstacle") != BlockingLayer)
            {
                Skip("layer 8 isn't Obstacle here: the walls would not stop a step");
                yield break;
            }
            // A spot where a catch-up puts the baby on its own slot (FreeSlot's first choice). F11 of 2026-11-06: by the yard
            // the world kept it off its own slot, and the catch-up gave the one beside it. Its own slot was then a real
            // place to go: a route out of the walls, which it took (1 rejoin, moved 0.58). Right, but not this case.
            var tried = new System.Collections.Generic.List<Vector3>();
            MountHandle p = null, f = null;
            for (int attempt = 0; attempt < 4 && f == null && !abort; attempt++)
            {
                Vector3? at = Spot(home, 6f + attempt, v => tried.TrueForAll(t => (t - v).sqrMagnitude > 9f));
                if (!at.HasValue)
                {
                    continue;
                }
                tried.Add(at.Value);
                MountHandle tp = Spawn(at.Value, right: true);
                if (tp == null)
                {
                    continue;
                }
                tp.Tuning.Set(Owner, "IdleShuffle", false); // it stands: its slots stay put
                yield return Until(() => tp.IsReady, 6f);
                MountHandle tf = waited ? Mounts.SpawnFoal(Owner, tp, 0.6f, persist: false) : null;
                if (tf == null)
                {
                    tp.Remove();
                    continue;
                }
                // The walls are measured for the shipped values, whatever the player's cfg says: with a gap over about
                // 1.15 its near side would be a real target to trot to, and steps under about 0.12 would not reach the
                // front wall.
                tf.Tuning.Set(Owner, "ShuffleFollowGap", 0.9f);
                tf.Tuning.Set(Owner, "ShuffleFoalLively", true);
                tf.Tuning.Set(Owner, "ShuffleFoalStepMin", 0.4f);
                tf.Tuning.Set(Owner, "ShuffleFoalStepMax", 1.5f);
                tf.Tuning.Set(Owner, "ShuffleFoalAreaWidth", 2f);
                tf.Tuning.Set(Owner, "ShuffleKeeperDistance", 2f); // it stands 5 to 7 from the keeper: closer than this it holds still
                yield return Until(() => tf.IsReady && tf.I.TestLateral > 0f, 6f);
                if (!waited)
                {
                    Info($"at {at.Value:F2} the baby never got its view (or its body offset): another spot");
                    tp.Remove();
                    continue;
                }
                yield return Seconds(0.5f);
                string why = tf.I.TestWhyNotOwnSlot();
                if (why != null)
                {
                    Info($"at {at.Value:F2} a catch-up wouldn't put it on its own slot ({why}): another spot");
                    tp.Remove(); // its baby with it
                    yield return Frames(2);
                    continue;
                }
                p = tp;
                f = tf;
            }
            if (f == null)
            {
                Skip("no spot round the keeper where a catch-up puts the baby on its own slot (the INFO lines say why)");
                yield break;
            }
            float lat = f.I.TestLateral;
            if (lat < 0.12f)
            {
                Skip($"its body offset {lat:F2} is too small for the front wall to stop its turn");
                yield break;
            }
            GameObject walls = null;
            try
            {
                // In the frame of the placement: the walls stand before its next pick.
                Vector3 s = f.I.TestPlaceFacingAway(out Vector3 forward, out Vector3 depth);
                float h = Vector3.Dot(forward, Vector3.Cross(Vector3.up, depth).normalized) >= 0f ? 1f : -1f;
                walls = new GameObject("gk2donkeyapi-selftest-no-room");
                walls.layer = BlockingLayer;
                walls.transform.position = s;
                walls.transform.rotation = Quaternion.LookRotation(depth, Vector3.up); // local x: the screen's right, z: away
                void Wall(float x, float z, float sx, float sz)
                {
                    var c = walls.AddComponent<BoxCollider>();
                    c.center = new Vector3(x, 1f, z);
                    c.size = new Vector3(sx, 2f, sz);
                }
                Wall(h * 0.425f, 0f, 0.15f, 2f);       // in front, 0.35 from its body: no step forward, no turn
                Wall(h * 0.2f, 0.525f, 0.6f, 0.15f);   // beside it, 0.45 off: the slots behind and in front of it on screen
                Wall(h * 0.2f, -0.525f, 0.6f, 0.15f);  // (the side towards its parent open: a catch-up's line is clear)
                Physics.SyncTransforms();
                int c0 = f.I.CatchUps, r0 = f.I.Rejoins, w0 = f.I.Wedges, s0 = f.I.StoodWithoutRoom;
                float moved = 0f;
                float end = Time.unscaledTime + 30f;
                while (Time.unscaledTime < end && !abort && f.Exists)
                {
                    Vector3 b = f.I.TestBody;
                    moved = Mathf.Max(moved, new Vector2(b.x - s.x, b.z - s.z).magnitude);
                    yield return null;
                }
                int catchUps = f.I.CatchUps - c0, rejoins = f.I.Rejoins - r0, wedges = f.I.Wedges - w0, stood = f.I.StoodWithoutRoom - s0;
                string counts = $"in 30 s: {wedges} wedge(s), {rejoins} rejoin(s), {catchUps} catch-up(s), stood without room {stood} time(s), moved {moved:F2}";
                if (wedges == 0)
                {
                    Warn($"the walls never wedged it, so the case wasn't made ({counts})");
                    yield break;
                }
                Check(rejoins == 0, $"no rejoin to the spot it stands on ({counts})");
                Check(catchUps == 0, "no catch-up onto its own spot, in view or not");
                Check(stood >= 1 && wedges <= 4, $"wedged, nowhere to go, it stands (a new wedge only after three fresh failed picks over 8 s: {wedges} in 30 s)");
                Check(moved < 0.15f, $"it stayed where it was put ({moved:F2} at the most)");
            }
            finally
            {
                if (walls != null)
                {
                    walls.SetActive(false); // gone for what follows at once (Destroy waits for the frame's end)
                    Object.Destroy(walls);
                    Physics.SyncTransforms();
                }
                if (p.Exists) p.Remove(); // its baby with it
            }
        }

        // The stuck report (FoalHops.cs WatchFollowing), with its window shortened to 3 s:
        // - a baby asleep 5 from its asleep parent (both far from the keeper; nothing moves either by design) isn't
        //   reported, and its line would say the parent is asleep without a view (it said "has its view": Lead.Here is
        //   its view or its scene loaded);
        // - an active baby shut in a pen 6 from its parent, which can't get nearer, still is.
        internal static IEnumerator ReportResting()
        {
            float window = Mount.Instance.StuckReportS;
            GameObject pen = null;
            MountHandle p = null, p2 = null;
            bool keeperAway = false;
            try
            {
                Mount.Instance.StuckReportS = 3f;
                Vector3 near = Spot(home, 3f) ?? home;
                Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
                if (!far.HasValue)
                {
                    Skip("no ground 42 to 50 units around the keeper");
                    yield break;
                }
                p = Spawn(near, right: true);
                if (p == null)
                {
                    Fail("could not spawn the parent");
                    yield break;
                }
                p.Tuning.Set(Owner, "IdleShuffle", false);
                yield return Until(() => p.IsReady, 6f);
                MountHandle f = Mounts.SpawnFoal(Owner, p, 0.6f, persist: false);
                yield return Until(() => f != null && f.IsReady, 6f);
                if (f == null || !waited)
                {
                    Fail("the baby never got its view");
                    yield break;
                }

                // 1. both asleep, 5 apart
                keeperAway = true;
                yield return MoveKeeper(far.Value, homeFacing);
                if (!moved) yield break;
                yield return Until(() => p.I.tier == Mount.Tier.Asleep && f.I.tier == Mount.Tier.Asleep, 6f);
                if (!waited)
                {
                    Warn($"not asleep with the keeper far away (levels {p.I.tier} / {f.I.tier}): skipped");
                    yield break;
                }
                Vector3? apart = Spot(p.Position, 5f);
                if (!apart.HasValue)
                {
                    Skip("no ground 5 from the parent");
                    yield break;
                }
                f.MoveTo(apart.Value);
                int r0 = f.I.StuckReports;
                yield return Seconds(5f);
                float d = new Vector2(f.Position.x - p.Position.x, f.Position.z - p.Position.z).magnitude;
                Check(d > 4f && f.I.tier == Mount.Tier.Asleep && !p.I.HasView, $"the case holds: asleep {d:F1} from its parent, which is asleep without a view (level {p.I.tier})");
                Check(f.I.StuckReports == r0 && f.I.TestResting, $"not reported in 5 s with a 3 s window: still by design ({f.I.StuckReports - r0} report(s))");
                string text = f.I.TestStuckText();
                int leadAt = text.IndexOf("the lead", System.StringComparison.Ordinal);
                Check(text.Contains("the lead is asleep, without a view"), "its line would say the parent is asleep without a view: " + (leadAt >= 0 ? text.Substring(leadAt) : text));
                p.Remove();
                p = null;
                PutKeeper(home, homeFacing);
                keeperAway = false;
                yield return Seconds(0.5f);

                // 2. an active baby that can't get nearer: shut in a pen 6 from its parent, on screen (no catch-up out of
                // sight), more than the shuffle's keeper distance from him
                // The pen whole on screen by the babies' own rule (Foals.OffScreen): a fixed 3.5 from the keeper was off
                // screen with the camera zoomed in (2026-11-12: Grave Zoom at 400%, 400 screen px a unit, 3.2 units each
                // side; measured: the pen at x 2680 of 2560). Nearer as the view allows, never within the shuffle's keeper
                // distance; none fits: the camera is too close for the case, skipped and said.
                float keeperDist = IdleShuffle.KeeperDistance.Value;
                bool PenOnScreen(Vector3 v)
                {
                    const float half = 0.75f; // the pen's walls, 1.5 across
                    return !Mount.Instance.TestOffScreen(v) && !Mount.Instance.TestOffScreen(v + Vector3.right * half) && !Mount.Instance.TestOffScreen(v - Vector3.right * half)
                        && !Mount.Instance.TestOffScreen(v + Vector3.forward * half) && !Mount.Instance.TestOffScreen(v - Vector3.forward * half);
                }
                Vector3? c = null;
                float penDist = 0f;
                foreach (float dist in new[] { 3.5f, 3f, 2.6f })
                {
                    if (dist <= keeperDist + 0.25f) break;
                    c = Spot(home, dist, PenOnScreen);
                    penDist = dist;
                    if (c.HasValue) break;
                }
                if (!c.HasValue)
                {
                    Skip($"no spot for the pen on screen more than {keeperDist:F1} from the keeper (the camera zoomed in too close? a zoom mod)");
                    yield break;
                }
                Vector3? pAt = Spot(c.Value, 6f);
                if (!pAt.HasValue)
                {
                    Skip("no open ground for the pen's parent");
                    yield break;
                }
                p2 = Spawn(pAt.Value, right: true);
                p2.Tuning.Set(Owner, "IdleShuffle", false);
                yield return Until(() => p2.IsReady, 6f);
                MountHandle f2 = Mounts.SpawnFoal(Owner, p2, 0.6f, persist: false);
                yield return Until(() => f2 != null && f2.IsReady, 6f);
                if (f2 == null || !waited)
                {
                    Fail("the second baby never got its view");
                    yield break;
                }
                pen = new GameObject("gk2donkeyapi-selftest-report-pen");
                pen.layer = BlockingLayer;
                pen.transform.position = c.Value;
                void Wall(float x, float z, float sx, float sz)
                {
                    var bc = pen.AddComponent<BoxCollider>();
                    bc.center = new Vector3(x, 1f, z);
                    bc.size = new Vector3(sx, 2f, sz);
                }
                Wall(0.675f, 0f, 0.15f, 1.5f);
                Wall(-0.675f, 0f, 0.15f, 1.5f);
                Wall(0f, 0.675f, 1.5f, 0.15f);
                Wall(0f, -0.675f, 1.5f, 0.15f);
                Physics.SyncTransforms();
                // the pen on screen before the baby goes in: off screen, a baby that can't get out rightly catches up
                // out of sight (Foals.cs), and the case isn't made. The keeper was just put back from 42-50 away; on
                // 2026-11-12 the baby caught up at once ("its parent stands again") 0.5 s after he came back, so the
                // camera's way back is waited for, and how long it took is in the check's line.
                float camFrom = Time.unscaledTime;
                yield return Until(() => !Mount.Instance.TestOffScreen(c.Value), 5f);
                float camWait = Time.unscaledTime - camFrom;
                if (!waited)
                {
                    // 2026-11-12, the second run: still off screen after 5 s, so not the camera's way back. What decides it:
                    // where the keeper really is, the camera, and the pen's screen point in the space OffScreen compares
                    // with Screen.width/height (the game draws through a pixel-perfect camera of its own).
                    Vector3 k = Keeper();
                    string screen;
                    try
                    {
                        Vector3 sp = CameraSystem.WorldToScreenPoint(c.Value), sk = CameraSystem.WorldToScreenPoint(k);
                        Camera cam = Camera.main;
                        string main = cam != null
                            ? $"Camera.main {cam.name} at {cam.transform.position:F2}, {cam.pixelWidth}x{cam.pixelHeight} px, the pen at {cam.WorldToScreenPoint(c.Value):F0}"
                            : "no Camera.main";
                        screen = $"CameraSystem: the pen at {sp:F0}, the keeper at {sk:F0}; Screen {Screen.width}x{Screen.height}; {main}";
                    }
                    catch (System.Exception e)
                    {
                        screen = "the screen points threw: " + e.Message;
                    }
                    Fail($"the pen at {c.Value:F2}, {penDist:F1} from home {home:F2}, was still off screen 5 s after the keeper came back; the keeper at {k:F2} ({Vector3.Distance(k, home):F2} from home); {screen}");
                    yield break;
                }
                f2.MoveTo(c.Value);
                int r2 = f2.I.StuckReports;
                yield return Until(() => f2.I.StuckReports > r2, 6f);
                float d2 = new Vector2(f2.Position.x - p2.Position.x, f2.Position.z - p2.Position.z).magnitude;
                Check(waited && f2.I.tier == Mount.Tier.Active, $"an active baby shut in, {d2:F1} from its parent, is still reported ({f2.I.StuckReports - r2} report(s) within 6 s; level {f2.I.tier}; the pen on screen {camWait:F1} s after the keeper came back)");
            }
            finally
            {
                Mount.Instance.StuckReportS = window;
                if (pen != null)
                {
                    pen.SetActive(false);
                    Object.Destroy(pen);
                    Physics.SyncTransforms();
                }
                if (p != null && p.Exists) p.Remove();
                if (p2 != null && p2.Exists) p2.Remove();
                if (keeperAway) PutKeeper(home, homeFacing);
            }
        }
    }
}
