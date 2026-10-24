using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Never lost (docs/never-lost.md): the safe-spot record, the fall watchdog and the return by hand, each put through
    // what it is for: the keeper placed under the ground and far below the world is back on the last ground he stood
    // on within moments; a drop onto the ground and standing under a bridge are left alone; the API call, Saddle Up's
    // rows and button, the refusals with their reasons.
    internal static partial class SelfTest
    {
        private const int GroundLayersMask = 6144;

        // the test's own narration in the keeper's bubble ("Jumping at ..."): not counted as the mod talking (the jump
        // scenario's "no bubble" check counts every Say)
        private static void TestSay(string text)
        {
            Mount.Say(text, 2f);
            Mount.Instance.TestSaidCount--;
        }

        private static bool SurfaceAtFeet(Vector3 p, float within = 0.3f) => Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, within + 0.05f, GroundLayersMask, QueryTriggerInteraction.Ignore);

        private static IEnumerator SafeRecord()
        {
            Check(Mounts.LastSafeSpot.HasValue, $"a safe spot exists after the load (the game's own place is a seed): {(Mounts.LastSafeSpot is Vector3 s0 ? s0.ToString("F2") : "none")}");
            Vector3 me = Keeper();
            yield return Seconds(1.6f);
            Check(Mounts.LastSafeSpot is Vector3 s1 && Flat(s1 - me) < 0.3f, $"standing still records this spot ({(Mounts.LastSafeSpot is Vector3 s2 ? Flat(s2 - me).ToString("0.00") + " from him" : "none")})");

            // under the ground: never recorded (the watchdog held off for this one)
            SafeSpots.TestGuardOff = true;
            try
            {
                int recorded = SafeSpots.TestRecorded, returns = SafeSpots.TestReturns;
                Vector3 under = me - Vector3.up * 1.0f;
                PutKeeper(under, homeFacing);
                yield return Seconds(1.8f);
                Vector3 now = Keeper();
                Check(SafeSpots.TestRecorded == recorded, $"put a unit under the ground, nothing is recorded ({SafeSpots.TestRecorded - recorded} recorded; he is at {now:F2}, {me.y - now.y:0.0} under the surface)");
                Check(SafeSpots.TestReturns == returns, "and with the watchdog held off nothing put him back");
                Check(Mounts.LastSafeSpot is Vector3 s3 && s3.y > under.y + 0.5f, "the last safe spot is still the one on the ground");
            }
            finally
            {
                SafeSpots.TestGuardOff = false;
                PutKeeper(me, homeFacing);
            }
            yield return Frames(5);
            Check(SurfaceAtFeet(Keeper()), "put back on the ground by the test");
        }

        private static IEnumerator SafeWatchdog()
        {
            if (Plugin.FallGuard != null && !Plugin.FallGuard.Value) { Skip("FallGuard is off in the cfg"); yield break; }
            Vector3 me = Keeper();
            Vector3 safe = Mounts.LastSafeSpot ?? me;

            // 1. under the ground
            int returns = SafeSpots.TestReturns;
            PutKeeper(me - Vector3.up * 1.0f, homeFacing);
            float t0 = Time.unscaledTime;
            yield return Until(() => SafeSpots.TestReturns > returns, 3f);
            float took = Time.unscaledTime - t0;
            yield return Frames(3);
            Vector3 now = Keeper();
            Check(SafeSpots.TestReturns == returns + 1, $"a unit under the ground: put back once, after {took:0.0} s ({SafeSpots.LastReason})");
            Check(SurfaceAtFeet(now) && Mount.Instance.WhyUnderground(now) == null && Vector3.Distance(now, SafeSpots.LastReturnedTo) < 0.5f, $"and he stands on the ground, nothing over him, at the spot ({now:F2}, the spot {SafeSpots.LastReturnedTo:F2})");
            yield return Seconds(0.5f);

            // 2. far below the world
            returns = SafeSpots.TestReturns;
            PutKeeper(me - Vector3.up * 50f, homeFacing);
            t0 = Time.unscaledTime;
            yield return Until(() => SafeSpots.TestReturns > returns, 3f);
            took = Time.unscaledTime - t0;
            yield return Frames(3);
            now = Keeper();
            Check(SafeSpots.TestReturns == returns + 1 && SurfaceAtFeet(now), $"fifty under the world: put back once, after {took:0.0} s, on the ground ({SafeSpots.LastReason})");
            yield return Seconds(0.5f);

            // 3. a drop onto the ground: nothing fires, he lands by himself
            returns = SafeSpots.TestReturns;
            PutKeeper(me + Vector3.up * 25f, homeFacing);
            t0 = Time.unscaledTime;
            yield return Until(() => SurfaceAtFeet(Keeper(), 0.3f) && Time.unscaledTime - t0 > 0.5f, 8f);
            took = Time.unscaledTime - t0;
            yield return Seconds(1.5f); // the clocks' longest hold, and some
            now = Keeper();
            Check(waited && SafeSpots.TestReturns == returns, $"dropped from 25 up: he landed by himself after {took:0.0} s and the watchdog left him alone ({SafeSpots.TestReturns - returns} return(s); at {now:F2})");
            if (Flat(now - me) > 1f) PutKeeper(me, homeFacing);

            // 4. under a bridge (ground over him, ground at his feet): left alone
            if (FindUnderBridge(me, 14f, out Vector3 bridge, out string what))
            {
                Info("under something: " + what);
                returns = SafeSpots.TestReturns;
                PutKeeper(bridge, homeFacing);
                yield return Seconds(2f);
                Check(SafeSpots.TestReturns == returns, $"standing under it for 2 s: left alone ({SafeSpots.TestReturns - returns} return(s))");
                PutKeeper(me, homeFacing);
                yield return Frames(5);
            }
            else Info("nothing near here to stand under (a bridge): that case not tried; run this one by the broken bridge");
        }

        // a spot within 'radius' with the walkable ground at his feet and the ground's layers over him within 3 (a bridge's deck)
        private static bool FindUnderBridge(Vector3 me, float radius, out Vector3 spot, out string what)
        {
            spot = default; what = null;
            bool bf = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                for (float r = 1f; r <= radius; r += 0.5f)
                {
                    for (int k = 0; k < 24; k++)
                    {
                        Vector3 p = me + Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward * r;
                        if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit feet, 1.5f, GroundLayersMask, QueryTriggerInteraction.Ignore)) continue;
                        Vector3 at = feet.point;
                        if (!Physics.Raycast(at + Vector3.up * 0.3f, Vector3.up, out RaycastHit over, 3f, GroundLayersMask, QueryTriggerInteraction.Ignore)) continue;
                        if (over.distance < 0.9f) continue; // too low to stand
                        if (Mount.Instance.KeeperFits(PC, at) != null) continue;
                        spot = at;
                        what = $"{over.collider.name} {over.distance + 0.3f:0.0} over the ground at {at:F2} ({feet.collider.name})";
                        return true;
                    }
                }
            }
            finally { Physics.queriesHitBackfaces = bf; }
            return false;
        }

        private static IEnumerator SafeReturn()
        {
            Vector3 me = Keeper();
            yield return Seconds(1.2f); // this spot recorded
            Vector3 spot = Mounts.LastSafeSpot ?? me;
            Vector3? away = Spot(me, 10f) ?? Spot(me, 7f);
            if (!away.HasValue) { Skip("no open ground 7-10 from here to be put at"); yield break; }

            // 1. on foot, by the API
            int returns = SafeSpots.TestReturns;
            PutKeeper(away.Value, homeFacing);
            yield return Frames(3);
            MountResult r = Mounts.ReturnToSafeSpot("the self-test");
            yield return Frames(3);
            Vector3 now = Keeper();
            Check(r.Ok && Vector3.Distance(now, spot) < 0.5f && SafeSpots.TestReturns == returns + 1, $"on foot, put 10 away: ReturnToSafeSpot brings him back ({Said(r)}; {Vector3.Distance(now, spot):0.00} from the spot)");

            // 2. riding: the ride goes on, the mount at the keeper
            MountHandle donkey = Spawn(Spot(me, 3f) ?? me, right: homeFacing > 0f);
            try
            {
                if (donkey == null) Fail("could not spawn the test's donkey");
                else
                {
                    yield return Until(() => donkey.IsReady, 6f);
                    MountResult ride = donkey.Ride(showMessage: false);
                    if (!ride.Ok) Fail($"not ridden ({Said(ride)})");
                    else
                    {
                        yield return Until(() => Mounts.IsMounted, 5f);
                        yield return Seconds(1.2f); // the ride's spot recorded
                        Vector3 rideSpot = Mounts.LastSafeSpot ?? me;
                        PutKeeper(away.Value, homeFacing);
                        yield return Frames(3);
                        r = Mounts.ReturnToSafeSpot("the self-test, riding");
                        yield return Frames(5);
                        now = Keeper();
                        Mount.Instance ridden = Mount.Ridden;
                        Check(r.Ok && Mounts.IsMounted && Vector3.Distance(now, rideSpot) < 0.5f && ridden != null && Flat(ridden.Position - now) < 2.5f,
                            $"riding, put 10 away: back, still riding, the mount at the keeper ({Said(r)}; {(ridden != null ? Flat(ridden.Position - now).ToString("0.0") : "-")} apart)");
                        Mounts.Dismount("self-test");
                        yield return Until(() => !Mounts.IsMounted, 5f);
                    }
                }
            }
            finally
            {
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (donkey != null && donkey.Exists) donkey.Remove(); } catch { }
            }

            // 3. refused: the controls taken (a UI window, a door, a cutscene)
            PC.SetControlTakenType(TakenControlType.ByUI, false);
            try
            {
                r = Mounts.ReturnToSafeSpot("the self-test, controls taken");
                Check(!r.Ok && r.Reason.StartsWith("Not now", System.StringComparison.Ordinal), $"with the controls taken: refused ({Said(r)})");
            }
            finally { PC.SetControlTakenType(TakenControlType.ByUI, true); }
            yield return Frames(2);

            // 4. refused: a scene with no spot yet
            object ring = SafeSpots.TestTakeRing();
            try
            {
                r = Mounts.ReturnToSafeSpot("the self-test, no spot");
                Check(!r.Ok && r.Reason.StartsWith("No safe spot", System.StringComparison.Ordinal), $"in a scene with no spot yet: refused ({Said(r)})");
            }
            finally { SafeSpots.TestGiveRing(ring); }

            // 5. Saddle Up's rows: the toggle, the button, the key
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null) Info("Saddle Up's page isn't shown: its rows not checked");
            else
            {
                MountSettingsPage.Row guard = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "FallGuard" && x.ShownSection == "Behaviour");
                MountSettingsPage.Row button = saddle.Rows.Find(x => x.RowKind == MountSettingsPage.RowKind.Button && x.ShownSection == "Behaviour" && x.Label == "Back to the last safe spot");
                MountSettingsPage.Row key = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "ReturnKey" && x.ShownSection == "Keys");
                Check(guard != null && button != null && key != null, $"Saddle Up has the FallGuard toggle, the button and the ReturnKey row ({(guard != null ? "toggle" : "no toggle")}, {(button != null ? "button \"" + button.ButtonText + "\"" : "no button")}, {(key != null ? "key" : "no key")})");
                if (guard != null)
                {
                    var ge = (BepInEx.Configuration.ConfigEntry<bool>)guard.Entry;
                    bool was = ge.Value;
                    try
                    {
                        ge.Value = false;
                        yield return null;
                        Check(Plugin.FallGuard != null && !Plugin.FallGuard.Value, "the toggle off: the Behaviour key FallGuard follows");
                    }
                    finally { ge.Value = was; }
                    yield return null;
                    Check(Plugin.FallGuard != null && Plugin.FallGuard.Value == was, "and back");
                }
                if (button != null)
                {
                    returns = SafeSpots.TestReturns;
                    PutKeeper(away.Value, homeFacing);
                    yield return Frames(3);
                    button.OnClick();
                    yield return Frames(3);
                    now = Keeper();
                    Check(SafeSpots.TestReturns == returns + 1 && Vector3.Distance(now, Mounts.LastSafeSpot ?? me) < 0.5f, $"the button brings him back from 10 away ({Vector3.Distance(now, Mounts.LastSafeSpot ?? me):0.00} from the spot)");
                }
                if (key != null)
                {
                    var ke = (BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut>)key.Entry;
                    var was = ke.Value;
                    try
                    {
                        var ctrlK = new BepInEx.Configuration.KeyboardShortcut(KeyCode.K, KeyCode.LeftControl);
                        ke.Value = ctrlK;
                        yield return null;
                        Check(Plugin.ReturnKey != null && Flight.SameKey(Plugin.ReturnKey.Value, ctrlK), $"the ReturnKey row set to Ctrl+K: the Behaviour key follows ({Plugin.ReturnKey?.Value})");
                    }
                    finally { ke.Value = was; }
                }
            }

            // 6. the watchdog off: under the ground stays (then back by hand). The button's arrival check runs first: it
            // judges where he is when it runs, and moved under the ground before it, he was sent to an older spot
            // (seen 2026-10-24: "failed on arrival", counted as a watchdog return)
            yield return Until(() => SafeSpots.TestChecksPending == 0, 2f);
            SafeSpots.TestGuardOff = true;
            try
            {
                returns = SafeSpots.TestReturns;
                PutKeeper(me - Vector3.up * 1.0f, homeFacing);
                yield return Seconds(2.5f);
                Check(SafeSpots.TestReturns == returns, $"the watchdog off: a unit under the ground is not caught ({SafeSpots.TestReturns - returns} return(s))");
                r = Mounts.ReturnToSafeSpot("the self-test, by hand");
                yield return Frames(3);
                Check(r.Ok && SurfaceAtFeet(Keeper()), $"and the return by hand still works ({Said(r)})");
            }
            finally { SafeSpots.TestGuardOff = false; }
        }
    }
}
