using BepInEx;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flying experiment (docs/flying.md). Step 1, the lift: the ridden mount, its rider and its cart rise above
    // where the ground puts them: Follow adds LiftFor to the mount's height, the seat follows through its own measuring
    // (AutoSeat, a skin's seat) or LiftFor (the manual seat). A jump is the same height on a ballistic path.
    // Step 2, free flight: from the clear height up the keeper's body leaves the game's physics (kinematic, as the
    // game's unused ladder did; the edge walls down) and we move it from the input ourselves (FixedStep), over water,
    // fences and roofs. It comes down only where he can stand (WhyCannotLandAt); every way out lands first (a save, a
    // dismount or a style change by a key), or, forced (a door, a scene change), gives the body back as it is.
    // Everything here is session-only, set on the Flying tab of the tuning window; nothing is saved, and with the
    // experiment off LiftFor is 0, Free is false and nothing changes.
    internal static class Flight
    {
        // the Flying tab's values (art pixels: one is a pixel of the picture on screen)
        // the experiment on: by default where the dev tools are unlocked (the machine with the sources, the user
        // 2026-10-22: the keys did nothing with it off); never for a player (Active)
        internal static bool Enabled = true;
        internal static bool Active => Enabled && DevTools.Unlocked;
        internal static bool AnyMount;             // off: the dragon only
        internal static float CruisePx = 200f;     // the height a take-off climbs to (48 until 2026-10-23, the user)
        internal static float MaxPx = 320f;        // the climb key stops here (above the take-off height)
        // The take-off and the landing as a dragon's, not an elevator's (the user, 2026-10-22: "it feels like a slow
        // elevator"): a crouch, then a spring launch with wing-beat surges that overshoots a little and settles; down, a
        // dive that speeds up, a flare near the ground and a thud on touching it.
        internal static float CrouchPx = 3f;       // the crouch before the launch
        internal static float CrouchS = 0.09f;
        internal static float TakeoffTimeS = 0.45f; // the launch's rise to the take-off height
        internal static float TakeoffBounce = 0.12f; // how far past it the launch carries, as a share (0: none)
        internal static int Beats = 3;             // wing beats in the launch, each a surge
        internal static float BeatKickPxPerS = 30f;
        internal static float BeatS = 0.16f;
        internal static float DiveSpeedPxPerS = 200f; // the landing's fastest fall
        internal static float DiveAccelPxPerS2 = 700f;
        internal static float FlarePx = 18f;       // from here down the dragon brakes
        internal static float TouchSpeedPxPerS = 40f; // its speed when it touches the ground
        internal static float ThudPx = 2f;         // the squat on touching the ground
        internal static float ClimbPxPerS = 60f;   // the climb and descend keys
        internal static float BobPx = 2f;          // the hover's up and down while airborne
        internal static float BobPeriodS = 1.6f;
        internal static bool JumpAnyMount = true;  // off: the jump only on the mounts that fly
        internal static float JumpPx = 14f;        // the top of a jump
        internal static float JumpTimeS = 0.45f;   // from leaving the ground to landing
        internal static float CameraFollow = 1f;   // how much of the height the camera rises with (0: stays on the ground)
        internal static bool FreeFlight = true;    // step 2: from the clear height up, over everything (off: the lift only)
        internal static float ClearPx = 16f;       // the free flight's floor: below it he is on the ground's physics again
        internal static bool BumpTall = true;      // free flight bumps into what stands taller than it (off: through everything)
        internal static bool UseDrawnGround = true; // step 2 of the drawn ground (DrawnGround.cs): floor, walls, landings
        internal static int CoverEdgeTiles = 1;    // a landing under this many edge tiles of a higher level is still allowed
        internal static float ClearancePx = 2f;    // how far under the flight's feet a top still counts as in the way
        internal static float WingBeatS = 0.6f;    // one wing beat (the six flying frames)
        internal static float BeatBobPx = 3f;      // the body's rise on each downstroke, with the flying frames
        internal static bool FlyFrames = true;     // the skin's flying frames while airborne (off: its walk and idle frames)
        internal static KeyCode TakeOffKey = KeyCode.Space, UpKey = KeyCode.LeftShift, DownKey = KeyCode.LeftControl, JumpKey = KeyCode.J; // climb Left Shift, descend Left Ctrl (the user, 2026-10-23)

        internal enum Phase { Ground, Rising, Airborne, Landing, Jumping }

        // one art pixel up the screen, in world y: two steps of the camera's y grid (CameraController.
        // GetRoundedCameraPosition, 0.01666667 at pixel size 2), so a lift of whole pixels keeps the camera on it
        private const float PxWorldY = 2f * 0.01666667f;
        private const float StepUp = 0.5f;   // the highest rise of the ground the flying body follows in one step (a slope, not a cliff)
        private const int GroundMask = 6144; // layers 11 and 12, what the game walks on
        internal static bool ProbeOn;        // the per-frame [flight-probe] lines (the tab; off: 70 000 lines filled the log)
        private static Mount.Instance owner;
        private static float heightPx, jumpVel, bobT, beatT;
        private static float groundBelowPx;          // flying free: how far the ground lies under the body (the body keeps its height off a plateau)
        private static bool bodyMoved;               // the body was put somewhere this frame (a landing): the camera catches up at once

        // The flight's height above the real ground under it: above the body, plus how far the ground lies under the
        // body. The flare and the landing frames go by it: by the height above the body, a dragon 87 px over the ground
        // (2.4 under the plateau it took off from) flared and showed its landing legs, then flew on down with the wings
        // beating again (the user's cut, 2026-10-23: "frame bdy_land_left_01 -> bdy_fly_left_04" in the jump lines).
        // Both ways: a plateau higher than the body (landing on it from lower ground) counts too, or the dragon dove at
        // full speed to the floor 16 px over the plateau, was set down there, and touched down at once from its dive
        // (the user's second cut, 2026-10-23: "the body down -2.40", frame fly_04 -> land_01, landed the same moment).
        internal static float AboveGroundPx => Mathf.Max(0f, heightPx + (Free ? groundBelowPx : 0f));
        private static Mount.Instance lastOwner;
        private static float vel, phaseT, nextBeat, thudT = -1f; // the launch's and the landing's motion
        private static int beatsLeft;
        private static bool launched;
        private static bool cameraLifted, cameraSaid;

        // step 2: the keeper's body is ours (kinematic, moved by FixedStep)
        internal static bool Free { get; private set; }
        private static Vector3 lastSafe, takeOffSpot;
        private static bool descending, refusalSaidThisLanding;
        private static float nextSafeCheck, nextLandTry, lastRefusalSaid;
        private static string lastRefusal, lastBlocker;
        private static float lastBlockerSaid;
        internal static string Blocker { get; private set; }
        internal static Vector3 LastSafe => lastSafe;
        internal static bool FreeFor(Mount.Instance i) => Free && i != null && i == owner;
        internal static Phase Now { get; private set; }
        internal static float HeightPx => heightPx;
        internal static string Why { get; private set; } = "";

        // The mount's lift this frame, world y: whole art pixels (the world is drawn pixel-snapped; a fraction shimmers).
        internal static float LiftFor(Mount.Instance i)
        {
            if (i == null || i != owner || Now == Phase.Ground) return 0f;
            float bob = Now == Phase.Jumping || BobPeriodS <= 0.01f ? 0f : Mathf.Sin(bobT * Mathf.PI * 2f / BobPeriodS) * BobPx * Mathf.Clamp01(heightPx / Mathf.Max(1f, BobPx * 4f));
            // with the flying frames the hover is the wing beat's: the body (and the rider) rises through the downstroke,
            // highest as the wings come down (frame 4), sinking on the upstroke (the reference's beat; the frames
            // themselves can't move the body, the seat being a fixed row of the picture)
            if (FlyFrames && Now != Phase.Jumping && FrameFor(i) is string fk && fk.StartsWith("bdy_fly_"))
                bob = BeatBobPx * 0.5f * (1f - Mathf.Cos(beatT / Mathf.Max(0.1f, WingBeatS) * Mathf.PI * 2f)) * Mathf.Clamp01(heightPx / 12f);
            // the height and the hover rounded each on its own: the camera follows the rounded height (FollowCamera), and
            // a mount rounded as height + hover stepped a frame or two before or after the camera while climbing, so it
            // shook a pixel against the screen once the hover had eased in (the user's video 2026-10-22 14-05-15: the
            // world moving smoothly, the dragon flipping 1 px back and forth on the screen, from about 8 px up)
            // below 0: the crouch before a launch and the thud of a landing (a few pixels into the ground: a squat)
            return Mathf.Max(-8f, Mathf.Round(heightPx) + Mathf.Round(bob)) * PxWorldY;
        }

        // The skin frame to show for this mount now (Looks.Rig.ApplyPart), or null: the game's own. The take-off frames
        // through the crouch and the launch's first part, then the wing beat; landing, the beat until the flare, the
        // reaching frames through it, the touch on the thud, the folding a moment after landing. Frame names a skin
        // lacks fall back to the game's (the rig checks), so a mount without them just walks in the air as before.
        private static float landedAt = -10f;
        private static readonly string[] FlyNames = { "bdy_fly_left_01", "bdy_fly_left_02", "bdy_fly_left_03", "bdy_fly_left_04", "bdy_fly_left_05", "bdy_fly_left_06" };
        private static readonly string[] TakeoffNames = { "bdy_takeoff_left_01", "bdy_takeoff_left_02", "bdy_takeoff_left_03", "bdy_takeoff_left_04" };
        internal static string FrameFor(Mount.Instance i)
        {
            if (!FlyFrames || i == null) return null;
            if (i != owner)
            {
                if (i == lastOwner && Time.unscaledTime - landedAt < 0.18f) return "bdy_land_left_04"; // folding the wings
                return null;
            }
            float beat = Mathf.Max(0.1f, WingBeatS);
            string Beat() => FlyNames[Mathf.FloorToInt(beatT / beat * 6f) % 6];
            switch (Now)
            {
                case Phase.Rising:
                    if (!launched) return "bdy_takeoff_left_01";                       // the crouch
                    if (phaseT < 0.24f) return TakeoffNames[1 + Mathf.Min(2, Mathf.FloorToInt(phaseT / 0.08f))];
                    return Beat();
                case Phase.Airborne:
                    return Beat();
                case Phase.Landing:
                    if (thudT >= 0f) return "bdy_land_left_03";                         // touching down
                    if (AboveGroundPx <= FlarePx) return AboveGroundPx <= FlarePx * 0.5f ? "bdy_land_left_02" : "bdy_land_left_01";
                    return Beat();
                default:
                    return null;
            }
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
                    descending = input.GetKey(DownKey);
                    if (descending)
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
                    Launch(Mathf.Min(dt, 0.05f));
                    break;
                case Phase.Landing:
                    Descend(Mathf.Min(dt, 0.05f));
                    break;
                case Phase.Jumping:
                    float t = Mathf.Max(0.1f, JumpTimeS);
                    heightPx += jumpVel * dt;
                    jumpVel -= 8f * JumpPx / (t * t) * dt;
                    if (heightPx <= 0f) Land("jump landed");
                    break;
            }
            if (Now != Phase.Ground) { bobT += dt; beatT += dt; }
            FreeTick(i);
            FollowCamera();
            if (bodyMoved)
            {
                // The body was put down on the ground under it this frame and the lift grew by the drop, so the mount's
                // height is the same; the camera's offset grew at once, but its follow of the body eases down over many
                // frames: the view jumped up 2.3 and came back down (the user's cut while landing, 2026-10-23: "on the
                // screen 972 -> 684, camera y 14.583 -> 16.841"). The camera catches up with the body at once instead.
                bodyMoved = false;
                CameraController cc = CameraSystem.Instance != null ? CameraSystem.Instance.ActiveCameraController : null;
                if (cc != null) { cc.UpdateTargetPosInstant(); Plugin.Log.LogInfo("[flight] the body was set down: the camera caught up with it at once"); }
            }
            LiftBubble();
            if (ProbeOn) Probe.AtTick(i);
            Jumps.AtTick(i);
        }

        // ------------------------------------------------------------------ step 2: free flight

        private static PlayerPhysicalBody Body => MainGame.Instance != null ? MainGame.PlayerController?.PhysicalBody : null;

        // Every frame after the height's update: take the body at the clear height on the way up, hold the clear
        // height on the way down until he may land, land, and keep the last spot he could have landed on.
        private static void FreeTick(Mount.Instance i)
        {
            PlayerController pc = MainGame.PlayerController;
            if (pc?.PhysicalBody?.Rb == null) return;
            if (!Free && FreeFlight && heightPx >= ClearPx && (Now == Phase.Rising || Now == Phase.Airborne))
            {
                if (Mount.Instance.IsIndoors()) { Why = "indoors: the lift only (no flying through walls)"; return; }
                EnterFree(pc);
            }
            if (!Free) return;
            if (Mount.Instance.IsIndoors())
            {
                // through a door with riding indoors allowed (no dismount there): the game has placed him; his body is its own
                ForceDown(pc, "indoors", move: false);
                Set(Phase.Landing, "landing: indoors");
                return;
            }
            // The floor: the clear height above the real ground under the flight, not above the body. The body crosses a
            // higher plateau at its own height (FixedStep follows slopes only), so measured from the body the mount came
            // down into a raised place's ground and through it (the user's video 2026-10-22 15-24-40: an unreachable
            // spot, "no ground under him (none)": the ground raycast started under its surface).
            Rigidbody rb = pc.PhysicalBody.Rb;
            float under = GroundUnderFlight(rb.position); // the ground a landing comes down to
            float alt = rb.position.y + Mathf.Max(0f, heightPx) * PxWorldY;
            float top = Mount.Instance.FloorUnder(pc, rb.position, alt); // and what is drawn on it: rocks, a cliff's rim
            // the drawn ground under the flight (a level drawn with no collider: a rock table's top, a ledge): ground too
            float drawn = UseDrawnGround ? DrawnGround.HighestAt(rb.position.x, rb.position.z, alt + 0.05f, out _) : float.NaN;
            float groundTop = float.IsNaN(drawn) ? under : float.IsNaN(under) ? drawn : Mathf.Max(under, drawn);
            groundBelowPx = float.IsNaN(groundTop) ? 0f : (rb.position.y - groundTop) / PxWorldY; // the ground this far under the body (a plateau left behind)
            // the ground: the clear height above it always; what is drawn on it: never inside it, and the clear height
            // above it only coming down (flying on over a rock, the bumping already keeps the flight above its top)
            float floorPx = ClearPx + (!float.IsNaN(groundTop) && groundTop > rb.position.y ? (groundTop - rb.position.y) / PxWorldY : 0f);
            if (!float.IsNaN(top)) floorPx = Mathf.Max(floorPx, (top - rb.position.y) / PxWorldY + (Now == Phase.Landing || descending ? ClearPx : 0f));
            Jumps.Floor(under, drawn, top, floorPx);
            thingTop = top;
            if (heightPx < floorPx)
            {
                // a landing (or the descend key) asks the ground; flying low over a rise just keeps above it
                if ((Now == Phase.Landing || descending) && Time.unscaledTime >= nextLandTry && TryLand(pc, under)) return;
                if (floorPx - heightPx > 2f) Plugin.Log.LogInfo($"[flight] the floor lifts the flight {floorPx - heightPx:0} px: walkable ground under it {under:0.00}, drawn ground {drawn:0.00}, drawn things up to {top:0.00}, the body at {rb.position.y:0.00}, now {floorPx:0} px up");
                heightPx = floorPx; vel = 0f; // not here: hold the clear height
            }
            if (Time.unscaledTime >= nextSafeCheck)
            {
                nextSafeCheck = Time.unscaledTime + 1f;
                Vector3 spot = rb.position;
                if (!float.IsNaN(under)) spot.y = under;
                if (Mount.Instance.WhyCannotLandAt(pc, ref spot, grid: false, connectedTo: takeOffSpot) == null) lastSafe = spot;
            }
        }

        // The ground's surface under the flight: the ground layers' raycast down from the mount's height (its feet), so a
        // plateau higher than the body is found; NaN over water or nothing.
        private static float GroundUnderFlight(Vector3 body)
        {
            float alt = body.y + Mathf.Max(0f, heightPx) * PxWorldY + 0.05f;
            return Physics.Raycast(new Vector3(body.x, alt, body.z), Vector3.down, out RaycastHit hit, alt - body.y + 30f, GroundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN;
        }

        private static void EnterFree(PlayerController pc)
        {
            PlayerPhysicalBody pb = pc.PhysicalBody;
            lastSafe = takeOffSpot = pb.Rb.position; // the take-off spot: he stood there (every landing must connect to it)
            pb.SetNonKinematicFlag(PlayerDynamicType.ByLadder, false); // the one type the game never sets (its ladder is unused)
            WallsDown(pb);
            Free = true;
            Plugin.Log.LogInfo($"[flight] free flight from {heightPx:0} px: the keeper's body is moved by the mod (kinematic {pb.Rb.isKinematic}), over everything; take-off spot {lastSafe:F2}");
        }

        private static void WallsDown(PlayerPhysicalBody pb)
        {
            try { HarmonyLib.Traverse.Create(pb).Field("invisibleWalls").GetValue<PlayerInvisibleWalls>()?.ChangeWallsVisibility(false); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[flight] the edge walls could not be put down: " + e.Message); }
        }

        // A landing under him: the ground must hold him (SafeGround's rules). The body comes down to it and the picture
        // keeps its place (the height grows by the drop), then descends as any landing.
        private static float thingTop = float.NaN; // what the floor found drawn under the flight this frame (FloorUnder)

        private static bool TryLand(PlayerController pc, float under)
        {
            nextLandTry = Time.unscaledTime + 0.25f;
            Rigidbody rb = pc.PhysicalBody.Rb;
            Vector3 spot = rb.position;
            if (!float.IsNaN(under)) spot.y = under; // judged at the ground under the flight, not at the body's height
            string why = Mount.Instance.WhyCannotLandAt(pc, ref spot, grid: true, connectedTo: takeOffSpot);
            // Something drawn stands under the flight higher than the ground the landing would put him on (a rock, a
            // roof, a cart): no landing through it. Seen 2026-10-23 at 40.62, 19.68: the floor found a thing drawn up to
            // 6.72 over ground at 3.60, the landing went on, put the body on the ground under it and the dragon came
            // down through it. The dragon stays on top of it instead ("Nowhere to land here.").
            if (why == null && !float.IsNaN(thingTop) && thingTop > spot.y + 0.6f)
                why = $"something drawn stands there, up to {thingTop:F2}, over the ground at {spot.y:F2}";
            if (why != null)
            {
                Why = "can't land here: " + why;
                if (why != lastRefusal || !refusalSaidThisLanding) Mount.Say("Nowhere to land here.");
                refusalSaidThisLanding = true;
                if (why != lastRefusal || Time.unscaledTime - lastRefusalSaid > 3f)
                {
                    lastRefusal = why; lastRefusalSaid = Time.unscaledTime;
                    Plugin.Log.LogInfo("[flight] " + Why + (Now == Phase.Landing ? " (the landing is called off: hovering here)" : " (holding the clear height)"));
                }
                // A landing refused is called off: the dragon hovers where it was refused, at the floor's height, and waits
                // for the next Land (the user, 2026-10-22: after one refusal it kept trying everywhere it went until a spot
                // took it; 2026-10-23: pulled back up to the flying height it "shoots back up" instead of hovering there).
                // The descend key held down stays at the floor: letting go of it is the player's call.
                if (Now == Phase.Landing) { vel = 0f; Set(Phase.Airborne, "hovering: nowhere to land here"); }
                return false;
            }
            float drop = rb.position.y - spot.y;
            heightPx = Mathf.Max(0f, heightPx + drop / PxWorldY); // the picture keeps its place (a plateau above: the body goes up)
            GiveBack(pc, spot, $"landing at {spot:F2} (the body down {drop:0.00})");
            Set(Phase.Landing, "landing");
            return true;
        }

        private static void GiveBack(PlayerController pc, Vector3? put, string what)
        {
            Free = false;
            PlayerPhysicalBody pb = pc?.PhysicalBody;
            if (pb == null) return;
            try
            {
                if (put.HasValue)
                {
                    bodyMoved = (put.Value - pb.Rb.position).sqrMagnitude > 0.0025f;
                    pb.SetPosition(put.Value);
                }
                pb.SetNonKinematicFlag(PlayerDynamicType.ByLadder, true);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[flight] giving the body back failed: " + e.Message); }
            Plugin.Log.LogInfo($"[flight] the body is the game's again: {what} (kinematic {pb.Rb?.isKinematic})");
        }

        // Down now, the body the game's: on the spot under him when he can stand there, else back at the last spot he
        // could have (move); without moving him (a door or a scene change: the game has placed him already).
        private static void ForceDown(PlayerController pc, string reason, bool move)
        {
            if (!Free) return;
            if (!move || pc?.PhysicalBody?.Rb == null) { GiveBack(pc, null, "where he is: " + reason); return; }
            Vector3 spot = pc.PhysicalBody.Rb.position;
            float under = GroundUnderFlight(spot);
            if (!float.IsNaN(under)) spot.y = under;
            string why = Mount.Instance.WhyCannotLandAt(pc, ref spot, grid: false, connectedTo: takeOffSpot);
            GiveBack(pc, why == null ? spot : lastSafe, why == null ? $"down under him: {reason}" : $"back to the last safe spot {lastSafe:F2} ({why}): {reason}");
        }

        // The move against the drawn ground: blocked where the drawn level ahead (the body's edge in the move's way) is
        // above the flight's feet, unless it is above them right here too (already under it: let him out). Blocked, the
        // move slides along x or along z when one of them is free.
        private static float lastWallSaid;

        private static Vector3 DrawnWall(Vector3 at, Vector3 move, float feet)
        {
            const float r = 0.25f;
            float here = DrawnGround.HighestAt(at.x, at.z, float.PositiveInfinity, out _);
            if (!float.IsNaN(here) && here > feet) return move; // inside or under it already
            bool Free(Vector3 m)
            {
                if (m.sqrMagnitude < 1e-10f) return true;
                Vector3 p = at + m + m.normalized * r;
                float y = DrawnGround.HighestAt(p.x, p.z, float.PositiveInfinity, out _);
                return float.IsNaN(y) || y <= feet;
            }
            if (Free(move)) return move;
            Vector3 mx = new Vector3(move.x, 0f, 0f), mz = new Vector3(0f, 0f, move.z);
            Vector3 slid = Free(mx) ? mx : Free(mz) ? mz : Vector3.zero;
            if (Time.unscaledTime - lastWallSaid > 2f)
            {
                lastWallSaid = Time.unscaledTime;
                Vector3 p = at + move + move.normalized * r;
                Plugin.Log.LogInfo($"[flight] the drawn ground ahead stops the flight: level {DrawnGround.HighestAt(p.x, p.z, float.PositiveInfinity, out _):0.00} at {p.x:0.00},{p.z:0.00}, the flight's feet at {feet:0.00}{(slid != Vector3.zero ? ", sliding" : "")}");
            }
            Blocker = "the drawn ground (a higher level)";
            return slid;
        }

        // The game is about to write a save: never one with the keeper in the air (his place is saved).
        internal static void BeforeSave()
        {
            if (!Free) return;
            ForceDown(MainGame.PlayerController, "a save", move: true);
            Set(Phase.Landing, "landing for the save");
        }

        // A dismount or a style change asked by a key, the panel or a mod while flying free: land first.
        internal static bool HoldForLanding(string what)
        {
            if (!Free) return false;
            if (Now != Phase.Landing) Set(Phase.Landing, $"landing first ({what})");
            Mount.Say("Land first.");
            return true;
        }

        // Plugin.FixedUpdate: the body moved from the input, at the walking speed the game would give it.
        internal static void FixedStep()
        {
            if (!Free) return;
            PlayerController pc = MainGame.PlayerController;
            PlayerPhysicalBody pb = pc?.PhysicalBody;
            Rigidbody rb = pb?.Rb;
            if (rb == null) return;
            if (!rb.isKinematic) rb.isKinematic = true; // a flow's LockMovement(false) writes it past the flags
            Vector2 d = pc.IsControlsEnabled && !MainGame.IsGamePaused && !(HarmonyLib.Traverse.Create(pb).Field("isMovementLocked").GetValue() is bool locked && locked) ? LazyBearTechnology.LazyInput.GetDirection() : Vector2.zero;
            if (d.sqrMagnitude > 1f) d.Normalize();
            float dt = Time.fixedDeltaTime, c = Mathf.Max(0.01f, rb.linearDamping);
            float speed = (pb.PhysicsConfig != null ? pb.PhysicsConfig.speed : 50f) * pb.SpeedMultiplier * (1f + c * dt) / c; // the top speed walking reaches against the damping
            Vector3 move = new Vector3(d.x, 0f, d.y * 0.8f) * speed * dt; // the game's 0.8 along z (MoveByDirection)
            Blocker = null;
            if (BumpTall)
            {
                // the flight's feet: the body plus the lift (the hover left out), a clearance under them
                float flightY = rb.position.y + Mathf.Round(heightPx) * PxWorldY - ClearancePx * PxWorldY;
                move = Mount.Instance.FlyMove(pc, rb.position, move, flightY);
                string b = Mount.Instance.LastBlocker;
                if (b != null && (b != lastBlocker || Time.unscaledTime - lastBlockerSaid > 2f))
                {
                    lastBlocker = b; lastBlockerSaid = Time.unscaledTime;
                    Plugin.Log.LogInfo("[flight] bumps into " + b);
                }
                Blocker = b;
            }
            if (UseDrawnGround && move.sqrMagnitude > 1e-8f)
            {
                // a higher drawn level ahead (a rock table's side, the next level up) is a wall to a flight below its top
                float feet = rb.position.y + Mathf.Round(heightPx) * PxWorldY - ClearancePx * PxWorldY;
                move = DrawnWall(rb.position, move, feet);
            }
            Vector3 next = rb.position + move;
            // Up the hills: the walkable surface right under him (the ground layers' raycast from half a unit above his
            // body), never higher. Not GroundHeight: its navmesh also lies on top of rocks and on the plateau above a
            // cliff (seen from above, the nearest node), and touching a cliff's rim lifted the body 2.3 at once onto it
            // (the user's video 2026-10-22 15-15-26; [flight-probe] body y 6.12 -> 8.41, 6.01 -> 3.60 and back). A
            // plateau higher than that is crossed at the body's height; the flight's height carries the picture over it,
            // and a landing puts the body on it. Over water, cliffs and roofs the height is kept.
            if (Physics.Raycast(next + Vector3.up * StepUp, Vector3.down, out RaycastHit under, StepUp + 0.5f, GroundMask, QueryTriggerInteraction.Ignore)
                && under.point.y > next.y)
            {
                if (under.point.y - next.y > 0.1f) Plugin.Log.LogInfo($"[flight] the body steps up {under.point.y - next.y:0.00} onto {under.collider.name} at {next.x:0.00},{next.z:0.00}");
                next.y = under.point.y;
            }
            rb.MovePosition(next);
            pc.PlayerData.position.Value = next;
        }

        // The jump detector (the user, 2026-10-23: "flying across the land the height sometimes jumps from one height
        // to another at once"): every frame while off the ground, where the mount is drawn (its root, set by Follow in
        // the frame before the draw) against the frame before; a step of more than 3 art px in one frame is logged with
        // everything the height is made of, then and before: the body's height (its interpolated transform and its
        // physics position), the flight's height, the lift, the phase, free or not, and the floor's four reads (walkable
        // ground, drawn ground, drawn things, the floor). `[flight] jump`. Always on with the experiment: it is a check.
        private static class Jumps
        {
            private struct Snap
            {
                internal int Frame;
                internal float Mount, BodyT, BodyP, H, Lift, Under, Drawn, Top, FloorPx;
                internal Phase Phase;
                internal bool Free;
                internal Vector3 At;
                internal float ScreenY, CamY, ScreenX;
                internal Vector3 Cam;
                internal string Frame_;
            }

            private static Snap last, now;
            private static bool hooked, have;
            private static Mount.Instance who;
            private static float under, drawn, top, floorPx;
            private static int said;

            internal static void Floor(float u, float d, float t, float f) { under = u; drawn = d; top = t; floorPx = f; }

            private static string Clip(string frame) => frame == null || !frame.StartsWith("bdy_") ? "game" : frame.Substring(4, frame.IndexOf('_', 4) - 4);

            internal static void AtTick(Mount.Instance i)
            {
                if (!hooked) { hooked = true; Camera.onPreCull += PreCull; }
                who = Now != Phase.Ground ? i : null;
                if (who == null) { have = false; said = 0; } // a fresh budget every flight
            }

            private static void PreCull(Camera cam)
            {
                if (who == null || CameraSystem.Instance == null || cam != CameraSystem.Instance.WorldCamera) return;
                if (now.Frame == Time.frameCount) return;
                Rigidbody rb = MainGame.PlayerController?.PhysicalBody?.Rb;
                if (rb == null) return;
                now = new Snap
                {
                    Frame = Time.frameCount, Mount = who.Position.y, BodyT = rb.transform.position.y, BodyP = rb.position.y,
                    H = heightPx, Lift = LiftFor(who), Phase = Now, Free = Free, Under = under, Drawn = drawn, Top = top, FloorPx = floorPx,
                    At = rb.position,
                    ScreenY = cam.WorldToScreenPoint(who.Position).y, CamY = cam.transform.position.y, Frame_ = FrameFor(who) ?? "the game's",
                    ScreenX = cam.WorldToScreenPoint(who.Position).x, Cam = cam.transform.position,
                };
                // Logged: a step of the drawn mount that the flight's own height doesn't explain (the mount moved more than
                // the body plus the lift did, by 2 px or more), or any step of 12 px or more. A launch to 200 px climbs
                // 5 px a frame by itself: logged as any step over 3 px, it filled the 200 lines in one take-off.
                float step = now.Mount - last.Mount;
                float unexplained = step - ((now.BodyT - last.BodyT) + (now.Lift - last.Lift));
                // ... or the mount's place on the screen stepping by more than 3 art pixels (6 screen px a pixel at the
                // smallest zoom is the floor; the camera follows the lift, so a climb holds it still) or its frame cutting
                // between the flying clips (the beat, take-off, landing)
                float screenStep = now.ScreenY - last.ScreenY;
                bool clipCut = now.Frame_ != last.Frame_ && Clip(now.Frame_) != Clip(last.Frame_);
                if (have && now.Frame == last.Frame + 1 && (Mathf.Abs(unexplained) >= 2f * PxWorldY || Mathf.Abs(step) >= 12f * PxWorldY || Mathf.Abs(screenStep) >= 18f || clipCut) && said < 200)
                {
                    said++;
                    Plugin.Log.LogInfo($"[flight] jump of {step / PxWorldY:+0;-0} px in one frame ({unexplained / PxWorldY:+0;-0} px not from the body or the lift) at {now.At.x:0.00},{now.At.z:0.00}: " +
                        $"mount {last.Mount:0.000} -> {now.Mount:0.000}; body (drawn) {last.BodyT:0.000} -> {now.BodyT:0.000}, (physics) {last.BodyP:0.000} -> {now.BodyP:0.000}; " +
                        $"height {last.H:0.0} -> {now.H:0.0} px, lift {last.Lift:0.000} -> {now.Lift:0.000}; {last.Phase}{(last.Free ? " free" : "")} -> {now.Phase}{(now.Free ? " free" : "")}; " +
                        $"floor: walkable {last.Under:0.00} -> {now.Under:0.00}, drawn ground {last.Drawn:0.00} -> {now.Drawn:0.00}, drawn things {last.Top:0.00} -> {now.Top:0.00}, floor {last.FloorPx:0} -> {now.FloorPx:0} px; " +
                        $"on the screen {last.ScreenX:0},{last.ScreenY:0} -> {now.ScreenX:0},{now.ScreenY:0} (camera {last.Cam.x:0.000},{last.Cam.y:0.000},{last.Cam.z:0.000} -> {now.Cam.x:0.000},{now.Cam.y:0.000},{now.Cam.z:0.000}; the body {last.At.x:0.000},{last.At.z:0.000} -> {now.At.x:0.000},{now.At.z:0.000}); frame {last.Frame_} -> {now.Frame_}");
                }
                last = now;
                have = true;
            }
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
                Plugin.Log.LogInfo($"[flight-probe] f{Time.frameCount} {Now}{(Free ? " free" : "")} body y {(rb != null ? rb.position.y : 0f):0.000} kin {(rb != null && rb.isKinematic)} h{heightPx:0.0} lift {liftAtTick:0.0000} | mount y {m.y:0.0000} screen {ms.x:0.00},{ms.y:0.00} | keeper screen {ks.x:0.00},{ks.y:0.00} | cam tick {camAtTick.x:0.0000},{camAtTick.y:0.0000},{camAtTick.z:0.0000} draw {c.x:0.0000},{c.y:0.0000},{c.z:0.0000} | offset {offsetAtTick.x:0.0000},{offsetAtTick.y:0.0000},{offsetAtTick.z:0.0000}");
            }
        }

        // The keeper's speech bubble (the game puts it at PlayerView's BubblePoint, over his body on the ground) rises
        // with the flight, so "Nowhere to land here." shows over the rider, not under the dragon (the user's video
        // 2026-10-22 15-32-47). Put back once on the ground.
        private static Transform bubblePoint;
        private static Vector3 bubbleRest;
        private static bool bubbleLifted;

        private static void LiftBubble()
        {
            float lift = Now == Phase.Ground ? 0f : Mathf.Max(0f, Mathf.Round(heightPx)) * PxWorldY;
            if (lift <= 0f && !bubbleLifted) return;
            Transform bp = MainGame.Instance != null ? MainGame.PlayerController?.BubblePoint : null;
            if (bp == null) return;
            if (bp != bubblePoint) { bubblePoint = bp; bubbleRest = bp.localPosition; }
            bp.localPosition = bubbleRest + Vector3.up * lift;
            bubbleLifted = lift > 0f;
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
            float lift = Now == Phase.Ground ? 0f : Mathf.Max(0f, Mathf.Round(heightPx)) * PxWorldY * Mathf.Clamp01(CameraFollow); // the crouch and the thud: not the camera
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
            if (i != owner || Now == Phase.Ground && heightPx == 0f && !Free) return;
            bool door = reason.Contains("indoors") || reason.Contains("scene changed");
            ForceDown(MainGame.Instance != null ? MainGame.PlayerController : null, reason, move: !door);
            Land("down at once: " + reason);
        }

        private static void Land(string what)
        {
            if (Free) ForceDown(MainGame.PlayerController, what, move: true);
            if (owner != null && Now != Phase.Jumping) { lastOwner = owner; landedAt = Time.unscaledTime; }
            heightPx = 0f; jumpVel = 0f; bobT = 0f; beatT = 0f; vel = 0f; thudT = -1f;
            Set(Phase.Ground, what);
            FollowCamera();
            LiftBubble();
            owner = null;
        }

        // The launch: a crouch, then a spring towards the take-off height (its stiffness from the rise time, its damping
        // from the overshoot asked), each wing beat a kick upwards; airborne once it has settled.
        private static void Launch(float dt)
        {
            phaseT += dt;
            float h = Mathf.Max(1f, CruisePx);
            if (!launched)
            {
                if (phaseT < CrouchS && heightPx <= 0.5f)
                {
                    heightPx = -CrouchPx * (phaseT / Mathf.Max(0.01f, CrouchS));
                    return;
                }
                launched = true;
                beatsLeft = Beats;
                nextBeat = phaseT;
                phaseT = 0f;
            }
            float os = Mathf.Clamp(TakeoffBounce, 0.001f, 0.6f);
            float ln = Mathf.Log(os);
            float zeta = -ln / Mathf.Sqrt(Mathf.PI * Mathf.PI + ln * ln);
            float wd = Mathf.Sqrt(1f - zeta * zeta);
            float omega = (Mathf.PI - Mathf.Acos(zeta)) / (Mathf.Max(0.1f, TakeoffTimeS) * wd); // reaches the target in the rise time
            if (beatsLeft > 0 && phaseT >= nextBeat)
            {
                vel += BeatKickPxPerS;
                beatsLeft--;
                nextBeat += Mathf.Max(0.05f, BeatS);
            }
            float a = omega * omega * (h - heightPx) - 2f * zeta * omega * vel;
            vel += a * dt;
            heightPx += vel * dt;
            if (phaseT > TakeoffTimeS && beatsLeft == 0 && Mathf.Abs(h - heightPx) < 0.5f && Mathf.Abs(vel) < 4f)
            {
                heightPx = h; vel = 0f;
                Set(Phase.Airborne, $"airborne at {h:0} px");
            }
        }

        // The landing: a dive that speeds up to its fastest, a flare from the flare height (braking to the touch speed),
        // then the thud: a squat into the ground and back.
        private static void Descend(float dt)
        {
            if (thudT >= 0f)
            {
                thudT += dt;
                const float thudS = 0.14f;
                heightPx = -ThudPx * Mathf.Sin(Mathf.Clamp01(thudT / thudS) * Mathf.PI);
                if (thudT >= thudS) Land("landed");
                return;
            }
            if (AboveGroundPx > FlarePx) vel = Mathf.Max(vel - DiveAccelPxPerS2 * dt, -DiveSpeedPxPerS);
            else vel = Mathf.MoveTowards(vel, -TouchSpeedPxPerS, DiveAccelPxPerS2 * 1.5f * dt);
            heightPx += vel * dt;
            if (heightPx <= 0f)
            {
                heightPx = 0f; vel = 0f;
                if (Free) return; // FreeTick lands the body first (or holds the clear height)
                if (ThudPx <= 0f) Land("landed");
                else thudT = 0f;
            }
        }

        private static void Set(Phase p, string what)
        {
            if (Now == p) return;
            if (p == Phase.Rising) { phaseT = 0f; launched = heightPx > 0.5f; beatsLeft = launched ? 0 : Beats; nextBeat = 0f; if (launched) vel = Mathf.Max(vel, 0f); }
            if (p == Phase.Landing) { thudT = -1f; vel = Mathf.Min(vel, 0f); refusalSaidThisLanding = false; }
            if (p == Phase.Airborne || p == Phase.Ground) vel = 0f;
            Now = p;
            Why = what;
            Plugin.Log.LogInfo($"[flight] {what} (height {heightPx:0} px)");
        }
    }
}
