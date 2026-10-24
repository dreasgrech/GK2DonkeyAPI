using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Never lost (docs/never-lost.md; the user, 2026-10-24: "just in case something happens and we do fall off the map,
    // that we reset back ... this is critical"). The game has no protection of its own: off the ground the keeper falls
    // for ever, a door places him raw, the save stores his position as is. So:
    //  - a record of the spots he stood on (a ring of 8 per scene, one every 0.5 s while he really stands on the walkable
    //    ground, nothing over him, his body fitting; seeds: where the game put him at a load and after each door);
    //  - a watchdog: under the ground (ground over him and none at his feet), falling into nothing (off the ground, going
    //    down, no ground within 30 under him) or far below the world (5 under this scene's lowest spot, no ground near
    //    under him), each held for a moment, puts him back on the newest spot that still holds (FallGuard, on);
    //  - the same by hand: Mounts.ReturnToSafeSpot (Saddle Up's button and ReturnKey).
    // Never while a door, a cutscene, a dialogue or a load has the keeper (IsControlsEnabled is false through all of
    // them), never for a moment after any placement (ours or the game's: PlayerController.SetPosition is patched to say),
    // never while the flight has the body (its own guards). Standing under a bridge is legal (the NPCs' mesh runs under
    // the deck): "ground over him" alone never counts, only with no ground at his feet.
    internal static class SafeSpots
    {
        internal const int Keep = 8;
        internal const float RecordEveryS = 0.5f, WatchEveryS = 0.1f, NewSpotDist = 0.5f;
        // AfterReturnQuietS: 3 s in the first build held the watchdog off through F11's second fall (caught after 2.8 s
        // instead of 0.6); the arrival check (ReturnCheck) is what guards against a bad spot, so 1 s is enough
        internal const float DoorQuietS = 2f, PlacedRecordQuietS = 1f, PlacedWatchQuietS = 0.3f, AfterReturnQuietS = 1f;
        internal const float UndergroundS = 0.5f, FallingS = 1.2f, BelowS = 0.3f, BelowMargin = 5f;
        internal const float FeetSurface = 0.3f, FallingNoGround = 30f, BelowNoGround = 2f, FallingSpeed = -2f, OverReach = 5f;
        internal static bool TestGuardOff; // the self-test: the watchdog held off (a put under the ground that must stay)
        private const int GroundLayers = 6144; // 11 and 12, what the game walks on

        private struct Spot
        {
            internal Vector3 P;
            internal float At;
            internal bool Seed;
        }

        private sealed class Ring
        {
            internal readonly List<Spot> Spots = new List<Spot>(); // newest first
            internal float LowestY = float.PositiveInfinity;
        }

        private static readonly Dictionary<string, Ring> rings = new Dictionary<string, Ring>(StringComparer.Ordinal);
        private static float nextRecord, nextWatch, doorAt = -999f, placedAt = -999f, quietUntil;
        private static bool doorSeeded = true;
        private static float undergroundSince = -1f, fallingSince = -1f, belowSince = -1f;
        private static KeyboardShortcut keySeen;
        private static bool keyWaitRelease;
        private static int said;

        // the self-tests
        internal static int TestReturns, TestRecorded;
        internal static string LastReason { get; private set; } = "";
        internal static Vector3 LastReturnedTo { get; private set; }

        // The game's own "on the ground" (PlayerPhysicalBody.isOnGround: a supporting contact or the ground's ray under
        // him, not jumping). A game update renaming it: null, and the walkable surface within 0.15 under his feet stands in.
        private static readonly AccessTools.FieldRef<PlayerPhysicalBody, bool> onGroundRef = MakeOnGround();

        private static AccessTools.FieldRef<PlayerPhysicalBody, bool> MakeOnGround()
        {
            try { return AccessTools.FieldRefAccess<PlayerPhysicalBody, bool>("isOnGround"); }
            catch (Exception e) { Plugin.Log.LogWarning("[safe-spot] PlayerPhysicalBody.isOnGround isn't there (" + e.Message + "): the ground's ray stands in"); return null; }
        }

        private static bool OnGround(PlayerPhysicalBody pb)
        {
            if (onGroundRef != null)
            {
                try { return onGroundRef(pb); } catch { }
            }
            return SurfaceUnder(pb.Rb.position, 0.15f);
        }

        // a walkable surface within 'dist' under the feet at p
        private static bool SurfaceUnder(Vector3 p, float dist) => Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, dist + 0.05f, GroundLayers, QueryTriggerInteraction.Ignore);

        // ---- hooks

        internal static void NewGame()
        {
            rings.Clear();
            doorAt = -999f; placedAt = -999f; quietUntil = 0f; doorSeeded = true;
            ResetClocks();
        }

        // PlayerController.OnPlayerTeleported: a door, the map, a script. Nothing is judged for DoorQuietS; then the
        // destination is a seed of its scene.
        internal static void NoteTeleport()
        {
            doorAt = Time.unscaledTime;
            doorSeeded = false;
            ResetClocks();
        }

        // PlayerController.SetPosition (patched): anyone placed him. The clocks start anew (a bad place is caught after
        // PlacedWatchQuietS), nothing is recorded for PlacedRecordQuietS (a bad place is never a safe spot).
        internal static void NotePlaced()
        {
            placedAt = Time.unscaledTime;
            ResetClocks();
        }

        private static void ResetClocks() { undergroundSince = fallingSince = belowSince = -1f; }

        // Each registry frame once the game is ready (MountRegistry.TickInner), mounts on or off.
        internal static void Tick(PlayerController pc)
        {
            PlayerPhysicalBody pb = pc?.PhysicalBody;
            Rigidbody rb = pb?.Rb;
            if (rb == null) return;
            using var perf = Perf.Measure(Perf.S.SafeSpots);
            float now = Time.unscaledTime;
            bool controls = pc.IsControlsEnabled && !MainGame.IsGamePaused;
            bool quiet = now - doorAt < DoorQuietS || now < quietUntil;
            string scene = pc.PlayerData?.currentGameSceneId;
            if (string.IsNullOrEmpty(scene)) return;

            // the door's destination, a seed once the door has settled
            if (!doorSeeded && now - doorAt >= DoorQuietS)
            {
                doorSeeded = true;
                if (controls && !rb.isKinematic && !Flight.Free && Verify(pc, rb.position, out _)) Record(scene, rb.position, seed: true);
            }

            // the record
            if (now >= nextRecord)
            {
                nextRecord = now + RecordEveryS;
                if (controls && !quiet && !rb.isKinematic && !Flight.Free && now - placedAt >= PlacedRecordQuietS && OnGround(pb) && Verify(pc, rb.position, out _))
                    Record(scene, rb.position, seed: !rings.TryGetValue(scene, out Ring r0) || r0.Spots.Count == 0);
            }

            // the key
            KeyboardShortcut key = Plugin.ReturnKey != null ? Plugin.ReturnKey.Value : KeyboardShortcut.Empty;
            if (!Flight.SameKey(key, keySeen)) { keySeen = key; keyWaitRelease = key.MainKey != KeyCode.None; }
            if (keyWaitRelease && (key.MainKey == KeyCode.None || !UnityInput.Current.GetKey(key.MainKey))) keyWaitRelease = false;
            if (!keyWaitRelease && key.MainKey != KeyCode.None && controls && !MountsPanel.IsOpen && GUIUtility.keyboardControl == 0 && !SettingsRows.CapturingKey && KeyActions.Pressed(key))
            {
                MountResult r = Return("the key");
                if (!r.Ok) Mount.Say(r.Reason, 3f);
            }

            // the watchdog
            if (now < nextWatch) return;
            nextWatch = now + WatchEveryS;
            if (TestGuardOff || (Plugin.FallGuard != null && !Plugin.FallGuard.Value)) { ResetClocks(); return; }
            if (!controls || quiet || rb.isKinematic || Flight.Free || now - placedAt < PlacedWatchQuietS) { ResetClocks(); return; }
            Vector3 p = rb.position;
            bool feet = SurfaceUnder(p, FeetSurface);
            string rule = null;
            // under the ground: ground over him and none at his feet (under a bridge there is ground at his feet)
            bool under = !feet && Mount.Instance.WhyUnderground(p, OverReach) != null;
            undergroundSince = under ? (undergroundSince < 0f ? now : undergroundSince) : -1f;
            if (under && now - undergroundSince >= UndergroundS) rule = "underground (ground over him, none at his feet)";
            // falling into nothing
            bool falling = !OnGround(pb) && rb.linearVelocity.y < FallingSpeed && !SurfaceUnder(p, FallingNoGround);
            fallingSince = falling ? (fallingSince < 0f ? now : fallingSince) : -1f;
            if (rule == null && falling && now - fallingSince >= FallingS) rule = $"falling into nothing ({rb.linearVelocity.y:0.0} a second, no ground within {FallingNoGround:0} under him)";
            // far below the world
            bool below = rings.TryGetValue(scene, out Ring ring) && !float.IsPositiveInfinity(ring.LowestY) && p.y < ring.LowestY - BelowMargin && !SurfaceUnder(p, BelowNoGround);
            belowSince = below ? (belowSince < 0f ? now : belowSince) : -1f;
            if (rule == null && below && now - belowSince >= BelowS) rule = $"below the world ({p.y:0.0}, the scene's lowest safe spot {ring.LowestY:0.0})";
            if (rule == null) return;
            ResetClocks();
            MountResult res = Return("fell out of the world: " + rule);
            if (!res.Ok)
            {
                quietUntil = now + AfterReturnQuietS; // said; asked again in a while (a scene with no spot yet: the fall goes on, the game's)
                if (said++ < 20) Plugin.Log.LogWarning($"[safe-spot] he fell out of the world ({rule}) and there is nowhere to put him back: {res.Reason}");
            }
        }

        private static void Record(string scene, Vector3 p, bool seed)
        {
            if (!rings.TryGetValue(scene, out Ring ring)) rings[scene] = ring = new Ring();
            if (ring.Spots.Count > 0 && !seed && Vector3.Distance(ring.Spots[0].P, p) < NewSpotDist)
            {
                ring.Spots[0] = new Spot { P = p, At = Time.unscaledTime, Seed = ring.Spots[0].Seed };
                return;
            }
            ring.Spots.Insert(0, new Spot { P = p, At = Time.unscaledTime, Seed = seed });
            while (ring.Spots.Count > Keep)
            {
                // the oldest goes, a seed last of all
                int drop = ring.Spots.Count - 1;
                while (drop > 0 && ring.Spots[drop].Seed) drop--;
                ring.Spots.RemoveAt(drop);
            }
            ring.LowestY = Mathf.Min(ring.LowestY, p.y);
            TestRecorded++;
        }

        // A spot he can stand on: the walkable surface right under the feet, nothing of the ground's layers over him,
        // his body fitting there. Cheap (three casts and an overlap box); no grid rescan.
        internal static bool Verify(PlayerController pc, Vector3 p, out string why)
        {
            why = null;
            if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.6f, GroundLayers, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - p.y) > 0.15f)
            {
                why = "no walkable surface at his feet";
                return false;
            }
            why = Mount.Instance.WhyUnderground(p);
            if (why != null) return false;
            why = Mount.Instance.KeeperFits(pc, p);
            return why == null;
        }

        internal static bool Newest(out Vector3 p)
        {
            p = default;
            PlayerController pc = MainGame.Instance != null ? MainGame.PlayerController : null;
            string scene = pc?.PlayerData?.currentGameSceneId;
            if (scene == null || !rings.TryGetValue(scene, out Ring ring) || ring.Spots.Count == 0) return false;
            p = ring.Spots[0].P;
            return true;
        }

        // Back on the newest safe spot of this scene that still holds; "Back on solid ground." in the bubble (a refusal is
        // the caller's to say). skip: spots already tried this time (ReturnCheck).
        internal static MountResult Return(string reason, int skip = 0)
        {
            if (!Mount.IsGameReady) return MountResult.Refused("Not in a game.");
            PlayerController pc = MainGame.PlayerController;
            PlayerPhysicalBody pb = pc?.PhysicalBody;
            Rigidbody rb = pb?.Rb;
            if (rb == null) return MountResult.Refused("The keeper's body isn't there.");
            if (!pc.IsControlsEnabled) return MountResult.Refused("Not now: a door, a scene or a cutscene has the keeper.");
            string scene = pc.PlayerData?.currentGameSceneId;
            if (scene == null || !rings.TryGetValue(scene, out Ring ring) || ring.Spots.Count == 0) return MountResult.Refused("No safe spot in this scene yet (stand on the ground a moment).");
            Vector3 was = rb.position;
            int tried = 0;
            string lastWhy = null;
            for (int k = 0; k < ring.Spots.Count; k++)
            {
                Spot s = ring.Spots[k];
                if (tried++ < skip) continue;
                if (!Verify(pc, s.P, out string why)) { lastWhy = $"{s.P:F1}: {why}"; continue; }
                if (Flight.InAir && Mount.Ridden != null) Flight.Release(Mount.Ridden, "back to the last safe spot");
                pc.SetPosition(s.P); // the body, its data and the camera at once (NotePlaced through the patch)
                if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                quietUntil = Time.unscaledTime + AfterReturnQuietS;
                ResetClocks();
                LastReason = reason;
                LastReturnedTo = s.P;
                TestReturns++;
                Plugin.Log.LogWarning($"[safe-spot] back to {s.P:F2} from {was:F2} ({Vector3.Distance(s.P, was):0.0} away, {was.y - s.P.y:+0.0;-0.0} in height): {reason}; the spot {(s.Seed ? "the game's own (a seed)" : "stood on")} {Time.unscaledTime - s.At:0} s ago, number {k + 1} of {ring.Spots.Count}{(k > 0 ? "; skipped: " + lastWhy : "")}{(Mount.IsMounted ? "; riding, the mount follows" : "")}");
                Mount.Say("Back on solid ground.", 3f);
                Mounts.RaiseReturnedToSafeSpot(s.P);
                Plugin.Instance.StartCoroutine(ReturnCheck(pc, s.P, k + 1));
                return MountResult.Success;
            }
            return MountResult.Refused(skip > 0 ? "No safe spot of this scene holds any more." : $"No safe spot of this scene still holds ({lastWhy}).");
        }

        // Two physics steps after a return: still standing there? Else the next older spot.
        private static IEnumerator ReturnCheck(PlayerController pc, Vector3 put, int skip)
        {
            TestChecksPending++;
            try
            {
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                yield return null;
                Rigidbody rb = pc?.PhysicalBody?.Rb;
                if (rb == null || !Mount.IsGameReady) yield break;
                Vector3 p = rb.position;
                bool ok = Vector3.Distance(p, put) < 0.5f && SurfaceUnder(p, FeetSurface) && Mount.Instance.WhyUnderground(p) == null;
                if (ok) yield break;
                Plugin.Log.LogWarning($"[safe-spot] the spot {put:F2} failed on arrival (he is at {p:F2}{(SurfaceUnder(p, FeetSurface) ? "" : ", no ground at his feet")}): the next older spot");
                if (skip < Keep) Return("the spot failed on arrival", skip);
            }
            finally
            {
                TestChecksPending--;
            }
        }

        // Arrival checks still to run (the self-tests wait for none before moving the keeper again: a check judges where
        // he is when it runs, so a step that moves him inside its two physics steps was taken for a bad spot, 2026-10-24).
        internal static int TestChecksPending;

        // ---- the self-tests: this scene's ring taken out and put back (a scene with no spot)
        internal static object TestTakeRing()
        {
            string scene = MainGame.PlayerController?.PlayerData?.currentGameSceneId;
            if (scene == null || !rings.TryGetValue(scene, out Ring ring)) return null;
            rings.Remove(scene);
            return ring;
        }

        internal static void TestGiveRing(object ring)
        {
            string scene = MainGame.PlayerController?.PlayerData?.currentGameSceneId;
            if (scene != null && ring is Ring r) rings[scene] = r;
        }

        internal static int TestCount()
        {
            string scene = MainGame.PlayerController?.PlayerData?.currentGameSceneId;
            return scene != null && rings.TryGetValue(scene, out Ring ring) ? ring.Spots.Count : 0;
        }
    }
}
