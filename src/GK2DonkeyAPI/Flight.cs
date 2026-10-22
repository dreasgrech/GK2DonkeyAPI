using BepInEx;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flying experiment (docs/flying.md, step 1: the lift only). The ridden mount, its rider and its cart rise above
    // where the ground puts them: Follow adds LiftFor to the mount's height, the seat follows through its own measuring
    // (AutoSeat, a skin's seat) or LiftFor (the manual seat). The keeper's physical body stays on the ground and moves as
    // on foot, so the flight goes where walking goes (no water, no cliffs yet). A jump is the same height on a ballistic
    // path. Everything here is session-only, set on the Flying tab of the tuning window; nothing is saved, and with the
    // experiment off (the default) LiftFor is 0 and nothing changes.
    internal static class Flight
    {
        // the Flying tab's values (art pixels: one is a pixel of the picture on screen)
        // the experiment on: by default where the dev tools are unlocked (the machine with the sources, the user
        // 2026-10-22: the keys did nothing with it off); never for a player (Active)
        internal static bool Enabled = true;
        internal static bool Active => Enabled && DevTools.Unlocked;
        internal static bool AnyMount;             // off: the dragon only
        internal static float CruisePx = 48f;      // the height a take-off climbs to
        internal static float MaxPx = 160f;        // the climb key stops here
        internal static float RisePxPerS = 40f;    // take-off speed
        internal static float ClimbPxPerS = 60f;   // the climb and descend keys
        internal static float LandPxPerS = 50f;    // landing speed
        internal static float BobPx = 2f;          // the hover's up and down while airborne
        internal static float BobPeriodS = 1.6f;
        internal static bool JumpAnyMount = true;  // off: the jump only on the mounts that fly
        internal static float JumpPx = 14f;        // the top of a jump
        internal static float JumpTimeS = 0.45f;   // from leaving the ground to landing
        internal static float CameraFollow = 1f;   // how much of the height the camera rises with (0: stays on the ground)
        internal static KeyCode TakeOffKey = KeyCode.Space, UpKey = KeyCode.R, DownKey = KeyCode.F, JumpKey = KeyCode.J;

        internal enum Phase { Ground, Rising, Airborne, Landing, Jumping }

        // one art pixel up the screen, in world y: two steps of the camera's y grid (CameraController.
        // GetRoundedCameraPosition, 0.01666667 at pixel size 2), so a lift of whole pixels keeps the camera on it
        private const float PxWorldY = 2f * 0.01666667f;
        private static Mount.Instance owner;
        private static float heightPx, jumpVel, bobT;
        private static bool cameraLifted, cameraSaid;
        internal static Phase Now { get; private set; }
        internal static float HeightPx => heightPx;
        internal static string Why { get; private set; } = "";

        // The mount's lift this frame, world y: whole art pixels (the world is drawn pixel-snapped; a fraction shimmers).
        internal static float LiftFor(Mount.Instance i)
        {
            if (i == null || i != owner || Now == Phase.Ground) return 0f;
            float bob = Now == Phase.Jumping || BobPeriodS <= 0.01f ? 0f : Mathf.Sin(bobT * Mathf.PI * 2f / BobPeriodS) * BobPx * Mathf.Clamp01(heightPx / Mathf.Max(1f, BobPx * 4f));
            // the height and the hover rounded each on its own: the camera follows the rounded height (FollowCamera), and
            // a mount rounded as height + hover stepped a frame or two before or after the camera while climbing, so it
            // shook a pixel against the screen once the hover had eased in (the user's video 2026-10-22 14-05-15: the
            // world moving smoothly, the dragon flipping 1 px back and forth on the screen, from about 8 px up)
            return Mathf.Max(0f, Mathf.Round(heightPx) + Mathf.Round(bob)) * PxWorldY;
        }

        // This mount may fly: the experiment on, riding plain (not the cart), the dragon or any mount.
        internal static bool CanFly(Mount.Instance i, out string why)
        {
            why = null;
            if (!Active) why = "the experiment is off";
            else if (i == null || !i.IsMounted) why = "not riding";
            else if (i.CurrentStyle != MountStyle.Ride) why = "drives the cart (flying is for riding plain)";
            else if (!AnyMount && !string.Equals(i.LooksRig?.MountNow?.Id, "dragon", System.StringComparison.OrdinalIgnoreCase)) why = $"rides {i.LooksRig?.MountNow?.Name ?? "the donkey"}, only the dragon flies (or \"any mount\")";
            return why == null;
        }

        private static bool CanJump(Mount.Instance i) => Active && i != null && i.IsMounted && i.CurrentStyle == MountStyle.Ride && (JumpAnyMount || CanFly(i, out _));

        // The tab's buttons and the keys.
        internal static void TakeOffOrLand(Mount.Instance i)
        {
            if (Now == Phase.Ground || Now == Phase.Landing)
            {
                if (!CanFly(i, out string why)) { Why = "can't take off: " + why; Plugin.Log.LogInfo("[flight] " + Why); return; }
                owner = i;
                Set(Phase.Rising, $"take off to {CruisePx:0} px");
            }
            else if (Now != Phase.Jumping)
            {
                Set(Phase.Landing, "landing");
            }
        }

        internal static void Jump(Mount.Instance i)
        {
            if (Now != Phase.Ground || !CanJump(i)) { Plugin.Log.LogInfo($"[flight] no jump: {(Now != Phase.Ground ? Now.ToString().ToLower() : "this mount doesn't jump (riding plain, the experiment on, 'every mount jumps' or a flying mount)")}"); return; }
            owner = i;
            float t = Mathf.Max(0.1f, JumpTimeS);
            jumpVel = 4f * JumpPx / t; // up at v, down under g = 8H/T^2: the top at H after T/2
            Set(Phase.Jumping, $"jump {JumpPx:0} px in {t:0.##} s");
        }

        // Every frame for the ridden mount, before Follow.
        internal static void Tick(Mount.Instance i)
        {
            if (owner != null && owner != i) Release(owner, "another mount ridden");
            if (!Active || i == null || !i.IsMounted || i.CurrentStyle != MountStyle.Ride)
            {
                if (Now != Phase.Ground) Release(i, !Active ? "the experiment switched off" : "no longer riding plain");
                return;
            }
            float dt = Time.deltaTime;
            if (!MountsPanel.IsOpen && GUIUtility.keyboardControl == 0)
            {
                var input = UnityInput.Current;
                if (input.GetKeyDown(TakeOffKey)) TakeOffOrLand(i);
                if (input.GetKeyDown(JumpKey)) Jump(i);
                if (Now == Phase.Airborne || Now == Phase.Rising)
                {
                    if (input.GetKey(UpKey)) { heightPx = Mathf.Min(MaxPx, heightPx + ClimbPxPerS * dt); Now = Phase.Airborne; }
                    if (input.GetKey(DownKey))
                    {
                        heightPx -= ClimbPxPerS * dt;
                        Now = Phase.Airborne;
                        if (heightPx <= 0f) Land("descended to the ground");
                    }
                }
            }
            if (Now != Phase.Ground && Now != Phase.Jumping && !CanFly(i, out string why)) Set(Phase.Landing, "landing: " + why);
            switch (Now)
            {
                case Phase.Rising:
                    heightPx = Mathf.MoveTowards(heightPx, CruisePx, RisePxPerS * dt);
                    if (Mathf.Approximately(heightPx, CruisePx)) Set(Phase.Airborne, $"airborne at {CruisePx:0} px");
                    break;
                case Phase.Landing:
                    heightPx = Mathf.MoveTowards(heightPx, 0f, LandPxPerS * dt);
                    if (heightPx <= 0f) Land("landed");
                    break;
                case Phase.Jumping:
                    float t = Mathf.Max(0.1f, JumpTimeS);
                    heightPx += jumpVel * dt;
                    jumpVel -= 8f * JumpPx / (t * t) * dt;
                    if (heightPx <= 0f) Land("jump landed");
                    break;
            }
            if (Now != Phase.Ground) bobT += dt;
            FollowCamera();
            Probe.AtTick(i);
        }

        // The shake while climbing (the user, 2026-10-22: still there with the camera moved straight up): per frame,
        // while off the ground and a second after, where the camera and the mount are at our tick (LateUpdate) and
        // again just before the world camera draws (onPreCull: after Cinemachine and every LateUpdate), and the
        // mount's and the keeper's places on the screen at the draw. "[flight-probe]" lines, at most 900 per flight.
        private static class Probe
        {
            private static bool hooked;
            private static int budget, lastFrame = -1, tailUntil;
            private static Mount.Instance who;
            private static Vector3 camAtTick, offsetAtTick;
            private static float liftAtTick;

            internal static void AtTick(Mount.Instance i)
            {
                if (!hooked) { hooked = true; Camera.onPreCull += PreCull; }
                if (Now != Phase.Ground) { if (budget <= 0 && who == null) budget = 900; who = i; tailUntil = Time.frameCount + 120; }
                if (who == null) return;
                Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
                camAtTick = cam != null ? cam.transform.position : Vector3.zero;
                liftAtTick = LiftFor(i);
                offsetAtTick = HarmonyLib.Traverse.Create(CameraSystem.Instance?.ActiveCameraController).Property("CameraSpaceOffset").Field("m_Offset").GetValue() is Vector3 o ? o : Vector3.zero;
                lastFrame = Time.frameCount;
            }

            private static void PreCull(Camera cam)
            {
                if (who == null || lastFrame != Time.frameCount || CameraSystem.Instance == null || cam != CameraSystem.Instance.WorldCamera) return;
                if (budget <= 0 || Time.frameCount > tailUntil) { who = null; budget = 0; return; }
                budget--;
                lastFrame = -1; // once per frame
                Vector3 m = who.Position;
                Vector3 ms = cam.WorldToScreenPoint(m);
                Rigidbody rb = MainGame.PlayerController?.PhysicalBody?.Rb;
                Vector3 ks = rb != null ? cam.WorldToScreenPoint(rb.transform.position) : Vector3.zero;
                Vector3 c = cam.transform.position;
                Plugin.Log.LogInfo($"[flight-probe] f{Time.frameCount} {Now} h{heightPx:0.0} lift {liftAtTick:0.0000} | mount y {m.y:0.0000} screen {ms.x:0.00},{ms.y:0.00} | keeper screen {ks.x:0.00},{ks.y:0.00} | cam tick {camAtTick.x:0.0000},{camAtTick.y:0.0000},{camAtTick.z:0.0000} draw {c.x:0.0000},{c.y:0.0000},{c.z:0.0000} | offset {offsetAtTick.x:0.0000},{offsetAtTick.y:0.0000},{offsetAtTick.z:0.0000}");
            }
        }

        // The camera rises with the mount (the user, 2026-10-22: it stayed on the ground and the mount flew off the
        // screen's middle): the game's camera-space offset on its follow camera (CameraController.SetCameraSpaceOffset,
        // otherwise used only by the credits), the height without the hover so the view doesn't bob. Put back to zero
        // once landed, only if we set it.
        // The offset is the mount's own world move, straight up by the lift (in camera space: up and back), not a move
        // along the screen's up: the offset comes after the game's pixel rounding of the camera, and a move along the
        // screen's up (0.36 world y, 0.48 world z per unit of lift) left the camera off its grid, so the whole picture
        // shook a screen pixel back and forth with every pixel of climb (the user's video 2026-10-22 13-50-54: the
        // mount and the ground moved together, 1 px back and forth every 3 frames while rising, still at a height).
        // Straight up by whole art pixels is whole steps of the camera's y grid.
        private static void FollowCamera()
        {
            float lift = Now == Phase.Ground ? 0f : Mathf.Round(heightPx) * PxWorldY * Mathf.Clamp01(CameraFollow);
            if (lift <= 0f && !cameraLifted) return;
            CameraController cc = CameraSystem.Instance != null ? CameraSystem.Instance.ActiveCameraController : null;
            if (cc == null) return;
            Camera cam = CameraSystem.Instance.WorldCamera != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            Quaternion rot = cam != null ? cam.transform.rotation : Quaternion.Euler(53.13f, 0f, 0f);
            Vector3 offset = Quaternion.Inverse(rot) * (Vector3.up * lift);
            if (!cameraSaid)
            {
                cameraSaid = true;
                bool has = HarmonyLib.Traverse.Create(cc).Property("CameraSpaceOffset").GetValue() is Object o && o != null; // no Cinemachine reference: the game's own lookup
                Plugin.Log.LogInfo($"[flight] the camera follows the height: camera-space offset on {cc.name} ({(has ? "found" : "NOT found: the camera will not move")}), one unit of lift = {(Quaternion.Inverse(rot) * Vector3.up).ToString("F3")} in camera space");
            }
            cc.SetCameraSpaceOffset(offset);
            cameraLifted = lift > 0f;
        }

        // Down at once: a dismount, a style change, the experiment off. (Step 2's flight over water will have to find
        // ground first; step 1 never leaves the walkable ground, so dropping is always safe.)
        internal static void Release(Mount.Instance i, string reason)
        {
            if (i != owner || Now == Phase.Ground && heightPx == 0f) return;
            Land("down at once: " + reason);
        }

        private static void Land(string what)
        {
            heightPx = 0f; jumpVel = 0f; bobT = 0f;
            Set(Phase.Ground, what);
            FollowCamera();
            owner = null;
        }

        private static void Set(Phase p, string what)
        {
            if (Now == p) return;
            Now = p;
            Why = what;
            Plugin.Log.LogInfo($"[flight] {what} (height {heightPx:0} px)");
        }
    }
}
