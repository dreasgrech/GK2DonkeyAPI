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
        internal static float FloorRisePxPerS = 240f; // a floor that rises (a ledge's level, a treetop) is risen to at this speed, never below what is there
        internal static bool BumpTall = true;      // free flight bumps into what stands taller than it (off: through everything)
        internal static bool UseDrawnGround = true; // step 2 of the drawn ground (DrawnGround.cs): floor, walls, landings
        internal static int CoverEdgeTiles = 1;    // a landing under this many edge tiles of a higher level is still allowed
        internal static float ClearancePx = 2f;    // how far under the flight's feet a top still counts as in the way
        internal static float BubbleS = 1.5f;      // how long the flight's bubbles stay ("Nowhere to land here.": the game's own time was about 5 s)
        internal static float WingBeatS = 0.6f;    // one wing beat (the eight flying frames)
        internal static float BeatBobPx = 3f;      // the body's rise on each downstroke, with the flying frames
        internal static bool FlyFrames = true;     // the skin's flying frames while airborne (off: its walk and idle frames)
        // The glide (docs/flying.md "the glide"; the user, 2026-10-23: "a glide animation where the wings are just open"):
        // flying along in one direction, now and then the beat stops and the wings stay open a while, flexing in the air
        // (the skin's bdy_glide_left_NN, one slow loop), with a way in (bdy_glidein_left_NN) and a way out
        // (bdy_glideoutQ_left_NN). Airborne only (not the launch or the landing), not while climbing; stopping, turning
        // or climbing ends it, by its way out. A skin without the frames never glides.
        internal static bool Glide = true;
        internal static float GlideAfterS = 2f;    // flying along this long (and again after each glide) before one may start
        internal static float GlideChance = 0.35f; // then the chance, once each beat
        internal static float GlideMinS = 1.5f, GlideMaxS = 3f; // how long one lasts, at random between
        internal static float GlideLoopS = 1.6f;   // the eight glide frames once round
        internal static float GlideBobPx = 1f;     // gliding, the body rises and sinks this much as the wings press the air
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
        private static Vector3 bodyMove;             // the body was put somewhere this frame (a landing) by this much: the camera is told
        private static RigidbodyInterpolation smoothWas;
        private static int smoothOffSteps;           // physics steps left with the body's smoothing off after a landing's move
        private static float floorWasPx;             // the floor over the body, as FreeTick last found it (the step-up keeps above it)
        private static float floorGapWas;            // how far under the floor the flight was the frame before (the floor's line once per rise)
        // the game's movement lock on the keeper's body, read every physics step (a Traverse allocated each time)
        private static readonly HarmonyLib.AccessTools.FieldRef<PlayerPhysicalBody, bool> movementLocked = MovementLockedRef();

        private static HarmonyLib.AccessTools.FieldRef<PlayerPhysicalBody, bool> MovementLockedRef()
        {
            try { return HarmonyLib.AccessTools.FieldRefAccess<PlayerPhysicalBody, bool>("isMovementLocked"); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[flight] the game's movement lock can't be read (" + e.Message + "): the flight moves while it is on"); return null; }
        }
        private enum GlideStage { None, In, Loop, Out }
        private static GlideStage glide;
        private static float alongT, glideT, glideLen, glideAll; // flying along for; the time in the stage; the loop's length; the whole glide's
        private static int glideQ, glideEndQ;       // the quarter of the loop the way out starts from; the quarter to leave at
        private static string glideEndWhy;          // why it ends (null: it goes on)
        private static Vector2 alongDir;
        private static int beatRolled = -1;          // the beat the glide's chance was last rolled on
        private static bool glideAsked, glideForced; // the tab's "Glide now": the next chance is a yes, and that glide runs its time moving or not
        internal static bool Gliding => glide != GlideStage.None;

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
        internal static bool InAir => owner != null && Now != Phase.Ground; // off the ground: flying, taking off, landing, a jump
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
            // Every flying frame the skin has (the beat, the take-off, the landing) takes the beat's rise, never the hover:
            // the landing's first frame switched from the beat's +3 px to the hover's -2 in one frame, the dragon dropping
            // 7 px with the height's own 2 (the log 2026-10-23: "jump of -7 px ... frame bdy_fly_left_04 -> bdy_land_left_01",
            // lift 0.733 -> 0.500), and back up 3 px when a refused landing went back to the beat. Through the landing's
            // frames the beat's clock stands still (Tick), so the rise holds and fades with the height.
            if (FlyFrames && Now != Phase.Jumping && Gliding)
                bob = GlideBob() * Mathf.Clamp01(heightPx / 12f);
            else if (FlyFrames && Now != Phase.Jumping && FrameFor(i) is string fk && IsFlightFrame(fk) && HasFlightFrame(i))
                bob = BeatBobPx * 0.5f * (1f - Mathf.Cos(beatT / Mathf.Max(0.1f, WingBeatS) * Mathf.PI * 2f)) * Mathf.Clamp01(heightPx / 12f);
            // the height and the hover rounded each on its own: the camera follows the rounded height (FollowCamera), and
            // a mount rounded as height + hover stepped a frame or two before or after the camera while climbing, so it
            // shook a pixel against the screen once the hover had eased in (the user's video 2026-10-22 14-05-15: the
            // world moving smoothly, the dragon flipping 1 px back and forth on the screen, from about 8 px up)
            // below 0: the crouch before a launch and the thud of a landing (a few pixels into the ground: a squat)
            return Mathf.Max(-8f, Mathf.Round(heightPx) + Mathf.Round(bob)) * PxWorldY;
        }

        private static readonly System.Text.RegularExpressions.Regex SkinFrameName = new System.Text.RegularExpressions.Regex(@"^bdy_(fly|glide|glidein|glideout[1-4]|takeoff|land)_left_\d\d$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        internal static bool IsSkinFrame(string key) => key != null && SkinFrameName.IsMatch(key); // a picture the flight plays (the skin's load line, the scans' pattern)
        private static bool IsFlightFrame(string key) => key.StartsWith("bdy_fly_", System.StringComparison.Ordinal) || key.StartsWith("bdy_takeoff_", System.StringComparison.Ordinal) || key.StartsWith("bdy_land_", System.StringComparison.Ordinal);
        // the hover stays for a skin without the beat (the mount walks in the air on its own frames, the hover moving it);
        // a skin with the beat keeps its rise through every flight frame, the ones it lacks too (shown as the game's)
        private static bool HasFlightFrame(Mount.Instance i) => i.LooksRig != null && i.LooksRig.HasFrame(FlyNames[0]);

        // The skin frame to show for this mount now (Looks.Rig.ApplyPart), or null: the game's own. The take-off frames
        // through the crouch and the launch's first part, then the wing beat; landing, the beat until the flare, the
        // reaching frames through it, the touch on the thud, the folding a moment after landing. Frame names a skin
        // lacks fall back to the game's (the rig checks), so a mount without them just walks in the air as before.
        private static float landedAt = -10f;
        // eight moments of one wing beat, each part of the body at its own lag (docs/flying.md "the beat in time")
        private static readonly string[] FlyNames = { "bdy_fly_left_01", "bdy_fly_left_02", "bdy_fly_left_03", "bdy_fly_left_04", "bdy_fly_left_05", "bdy_fly_left_06", "bdy_fly_left_07", "bdy_fly_left_08" };
        private static readonly string[] GlideNames = { "bdy_glide_left_01", "bdy_glide_left_02", "bdy_glide_left_03", "bdy_glide_left_04", "bdy_glide_left_05", "bdy_glide_left_06", "bdy_glide_left_07", "bdy_glide_left_08" };
        private static readonly string[] GlideInNames = { "bdy_glidein_left_01", "bdy_glidein_left_02", "bdy_glidein_left_03", "bdy_glidein_left_04" };
        private static readonly string[][] GlideOutNames = OutNames();
        private static string[][] OutNames()
        {
            var all = new string[4][];
            for (int q = 0; q < 4; q++)
            {
                all[q] = new string[4];
                for (int k = 0; k < 4; k++) all[q][k] = $"bdy_glideout{q + 1}_left_{k + 1:00}";
            }
            return all;
        }
        // each in-between frame's time at the 0.6 s beat (the beat's own frames are 75 ms; scaled with WingBeatS): the way
        // in slowing into the glide, the way out speeding up into the beat
        private static readonly float[] GlideInS = { 0.085f, 0.1f, 0.12f, 0.15f };
        private static readonly float[] GlideOutS = { 0.13f, 0.11f, 0.09f, 0.08f };
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
            string Beat() => FlyNames[Mathf.FloorToInt(beatT / beat * FlyNames.Length) % FlyNames.Length];
            switch (Now)
            {
                case Phase.Rising:
                    if (!launched) return "bdy_takeoff_left_01";                       // the crouch
                    if (phaseT < 0.24f) return TakeoffNames[1 + Mathf.Min(2, Mathf.FloorToInt(phaseT / 0.08f))];
                    return Beat();
                case Phase.Airborne:
                    return Gliding ? GlideFrame() : Beat();
                case Phase.Landing:
                    if (thudT >= 0f) return "bdy_land_left_03";                         // touching down
                    if (AboveGroundPx <= FlarePx) return AboveGroundPx <= FlarePx * 0.5f ? "bdy_land_left_02" : "bdy_land_left_01";
                    return Gliding ? GlideFrame() : Beat();                             // a glide's way out, then the beat
                default:
                    return null;
            }
        }

        // A readout for the landing rule (docs/flying.md "Refused landings near home"): every change of the walkable
        // mesh's part ("area") under the keeper riding on the ground, and every stretch off the mesh, one line each. The
        // user rode between two parts the mesh says are not joined (areas 1 and 434, 2026-10-23): where they split.
        private static int areaWas = -3;
        private static float areaNext;
        private static Vector3 areaWasAt;

        private static void AreaWatch()
        {
            if (Time.unscaledTime < areaNext) return;
            areaNext = Time.unscaledTime + 0.2f;
            Rigidbody rb = MainGame.PlayerController?.PhysicalBody?.Rb;
            if (rb == null) return;
            Vector3 p = rb.position;
            int a = Mount.Instance.MeshAreaAt(p, out float off, out Vector3 node);
            if (a == areaWas) { areaWasAt = p; return; }
            if (areaWas != -3)
                Plugin.Log.LogInfo($"[flight-area] the walkable mesh under the keeper: {AreaName(areaWas)} -> {AreaName(a)} at {p:F2} (last {areaWasAt:F2}; the nearest node {off:0.00} away at {node:F2})");
            areaWas = a; areaWasAt = p;
        }

        private static string AreaName(int a) => a == -1 ? "off the mesh" : a == -2 ? "no mesh" : "area " + a;

        // The glide's stages (the frames: GlideFrame; the body's rise: GlideBob). It starts as the beat reaches its frame 7
        // (th 0.75, the wings coming up through the glide's pose) and the beat's clock stops there; the way in (4 frames,
        // slowing) blends the rest of that upstroke into the open wings, then the loop runs its length. Ending (its time,
        // stopping, turning, climbing, landing), it waits for the next quarter of the loop (the way out is drawn from
        // each), then the way out (4 frames, speeding up) pushes off with a downstroke and hands over to the beat at its
        // frame 7 again. The generator draws each in-between frame from the same moments (make_creatures.py GLIDE_IN/OUT).
        private static float StageScale => Mathf.Max(0.1f, WingBeatS) / 0.6f;
        private static float LoopS => Mathf.Max(0.2f, GlideLoopS);

        private static float StageLength(float[] times)
        {
            float sum = 0f;
            foreach (float t in times) sum += t * StageScale;
            return sum;
        }

        private static int StageFrame(float[] times, float t)
        {
            for (int k = 0; k < times.Length; k++)
            {
                t -= times[k] * StageScale;
                if (t < 0f) return k;
            }
            return times.Length - 1;
        }

        private static string GlideFrame()
        {
            switch (glide)
            {
                case GlideStage.In: return GlideInNames[StageFrame(GlideInS, glideT)];
                case GlideStage.Out: return GlideOutNames[glideQ][StageFrame(GlideOutS, glideT)];
                default: return GlideNames[Mathf.FloorToInt(glideT / LoopS * GlideNames.Length) % GlideNames.Length];
            }
        }

        // The body's rise while gliding, art px: the beat's at each in-between frame's moment of the beat, blended as the
        // frame is into the glide's: half the beat's rise (where it hangs on average) and a slow swell of GlideBobPx as
        // the wings press the air (their lowest at a quarter of the loop, the body highest a little after)
        private static float GlideBob()
        {
            float BeatRise(float th) => BeatBobPx * 0.5f * (1f - Mathf.Cos(th * Mathf.PI * 2f));
            float GlideRise(float g) => BeatBobPx * 0.5f + GlideBobPx * Mathf.Sin((g - 0.1f) * Mathf.PI * 2f);
            switch (glide)
            {
                case GlideStage.In:
                    int k = StageFrame(GlideInS, glideT);
                    return Mathf.Lerp(BeatRise(0.75f + k / 8f), GlideRise((k - 4) / 8f), (k + 1) / 5f);
                case GlideStage.Out:
                    int j = StageFrame(GlideOutS, glideT);
                    return Mathf.Lerp(BeatRise(0.25f + j / 8f), GlideRise(glideQ / 4f + j / 8f), 1f - (j + 1) / 5f);
                default:
                    return GlideRise(glideT / LoopS);
            }
        }

        private static bool HasGlideFrames(Mount.Instance i) =>
            i.LooksRig != null && i.LooksRig.HasFrame(GlideNames[0]) && i.LooksRig.HasFrame(GlideInNames[0]) && i.LooksRig.HasFrame(GlideOutNames[3][3]);

        private static void GlideTick(Mount.Instance i, float dt, bool climbing)
        {
            PlayerController pc = MainGame.PlayerController;
            Vector2 d = pc != null && pc.IsControlsEnabled && !MainGame.IsGamePaused ? LazyBearTechnology.LazyInput.GetDirection() : Vector2.zero;
            bool can = Glide && FlyFrames && Now == Phase.Airborne && !climbing && HasGlideFrames(i);
            bool moving = d.sqrMagnitude >= 0.25f;
            bool turned = moving && alongT > 0f && Vector2.Dot(d.normalized, alongDir) < 0.7f; // more than 45 degrees off the way he was going
            if (!Gliding)
            {
                if (!can || (!glideAsked && (!moving || turned)))
                {
                    alongT = 0f;
                    if (moving) alongDir = d.normalized;
                    glideAsked &= can;
                    return;
                }
                if (alongT == 0f && moving) alongDir = d.normalized;
                alongT += dt;
                float beat = Mathf.Max(0.1f, WingBeatS);
                int n = Mathf.FloorToInt(beatT / beat);
                float th = beatT / beat - n;
                if (th < 6f / 8f || th >= 7f / 8f || n == beatRolled || (!glideAsked && alongT < GlideAfterS)) return;
                beatRolled = n;
                if (!glideAsked && UnityEngine.Random.value >= GlideChance) return;
                beatT = (n + 0.75f) * beat; // the clock stops on frame 7, where the way in and the way out meet the beat
                glide = GlideStage.In;
                glideT = glideAll = 0f;
                glideLen = UnityEngine.Random.Range(Mathf.Min(GlideMinS, GlideMaxS), Mathf.Max(GlideMinS, GlideMaxS));
                glideEndWhy = null;
                glideForced = glideAsked;
                Plugin.Log.LogInfo($"[flight] glide for {glideLen:0.0} s ({(glideAsked ? "asked for" : $"flying along {alongT:0.0} s")})");
                glideAsked = false;
                return;
            }
            glideT += dt;
            glideAll += dt;
            if (glideEndWhy == null)
            {
                string why = !can ? (Now != Phase.Airborne ? "now " + Now : climbing ? "climbing" : "switched off")
                    : glideForced ? null : !moving ? "stopped" : turned ? "turned" : null;
                if (why == null && glide == GlideStage.Loop && glideT >= glideLen) why = $"its {glideLen:0.0} s";
                if (why != null)
                {
                    glideEndWhy = why;
                    if (glide == GlideStage.Loop) glideEndQ = Mathf.FloorToInt(glideT / LoopS * 4f) + 1; // the next quarter
                }
            }
            switch (glide)
            {
                case GlideStage.In:
                    float inS = StageLength(GlideInS);
                    if (glideT < inS) break;
                    if (glideEndWhy != null) { glide = GlideStage.Out; glideQ = 0; } // the way in ends where the loop begins
                    else glide = GlideStage.Loop;
                    glideT -= inS;
                    break;
                case GlideStage.Loop:
                    if (glideEndWhy == null || glideT / LoopS * 4f < glideEndQ) break;
                    glide = GlideStage.Out;
                    glideQ = glideEndQ % 4;
                    glideT = Mathf.Clamp(glideT - glideEndQ * LoopS / 4f, 0f, GlideOutS[0] * StageScale * 0.5f);
                    break;
                case GlideStage.Out:
                    if (glideT >= StageLength(GlideOutS)) EndGlide();
                    break;
            }
        }

        private static void EndGlide()
        {
            float beat = Mathf.Max(0.1f, WingBeatS);
            beatT = (Mathf.FloorToInt(beatT / beat) + 0.75f) * beat; // the beat goes on from its frame 7
            Plugin.Log.LogInfo($"[flight] glide ends: {glideEndWhy ?? "done"} (after {glideAll:0.0} s)");
            StopGlide();
        }

        private static void StopGlide()
        {
            glide = GlideStage.None;
            alongT = 0f; // a while of beating again before the next
            glideAsked = glideForced = false;
        }

        // The tab's "Glide now": the next time the beat reaches its frame 7.
        internal static void GlideNow() { if (Now == Phase.Airborne) glideAsked = true; }

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
            if (Now == Phase.Ground) AreaWatch();
            bool climbing = false;
            if (!MountsPanel.IsOpen && GUIUtility.keyboardControl == 0)
            {
                var input = UnityInput.Current;
                if (input.GetKeyDown(TakeOffKey)) TakeOffOrLand(i);
                if (input.GetKeyDown(JumpKey)) Jump(i);
                if (Now == Phase.Airborne || Now == Phase.Rising)
                {
                    if (input.GetKey(UpKey)) climbing = true;
                    if (climbing) { heightPx = Mathf.Min(MaxPx, heightPx + ClimbPxPerS * dt); Now = Phase.Airborne; }
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
            GlideTick(i, dt, climbing);
            // the beat's clock: still while gliding (the glide's own stages) and through the landing's frames (cut 2 above)
            if (Now != Phase.Ground) { bobT += dt; if (!Gliding && !(FrameFor(i) is string lk && lk.StartsWith("bdy_land_", System.StringComparison.Ordinal))) beatT += dt; }
            FreeTick(i);
            FollowCamera();
            ApplyBodyMove();
            LiftBubble();
            if (ProbeOn) Probe.AtTick(i);
            Jumps.AtTick(i);
        }

        private static void ApplyBodyMove()
        {
            if (bodyMove != Vector3.zero)
            {
                // The body was put down on the ground under it this frame and the lift grew by the drop, so the mount's
                // height is the same; the camera's offset grew at once, but its follow of the body eases down over many
                // frames: the view jumped up 2.3 and came back down (the user's cut while landing, 2026-10-23: "on the
                // screen 972 -> 684, camera y 14.583 -> 16.841"). Snapping the camera to the body (UpdateTargetPosInstant,
                // CancelDamping) fixed that but threw away the camera's trail behind a moving flight too: x 3.50 -> 5.31
                // in one frame, the dragon 367 px across the screen (the user's cut, the same day). So the camera is told
                // the body moved by exactly this much (Cinemachine's OnTargetObjectWarped: its smoothed position shifts
                // with the follow target), keeping any trail it had. Up and down by the camera's share of the height
                // (CameraFollow): the height that made up for the move moved the camera by that share, the rest of the
                // body's move the camera follows as it follows the body (warped by all of it, the view jumped by the rest).
                Vector3 move = bodyMove;
                bodyMove = Vector3.zero;
                move.y *= Mathf.Clamp01(CameraFollow);
                CameraController cc = CameraSystem.Instance != null ? CameraSystem.Instance.ActiveCameraController : null;
                try
                {
                    string follow = cc != null ? MoveCameraWith(cc, move) : null;
                    if (follow != null) Plugin.Log.LogInfo($"[flight] the body was moved by {move.y:0.00} (x {move.x:0.00}, z {move.z:0.00}): the camera moved with it (following {follow})");
                }
                catch (System.Exception e)
                {
                    // Cinemachine is the game's camera library, outside the dependency manifest's scopes: if it ever
                    // changes, the old snap (which loses the camera's trail) rather than a broken landing
                    Plugin.Log.LogWarning("[flight] could not move the camera with the body (" + e.Message + "): snapping it to the body instead");
                    cc?.UpdateTargetPosInstant();
                }
            }
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
            // what is really there, without the clear height over it: the flight is never drawn below it
            float solidPx = Mathf.Max(0f, !float.IsNaN(groundTop) && groundTop > rb.position.y ? (groundTop - rb.position.y) / PxWorldY : 0f);
            if (!float.IsNaN(top)) floorPx = Mathf.Max(floorPx, (top - rb.position.y) / PxWorldY + (Now == Phase.Landing || descending ? ClearPx : 0f));
            if (!float.IsNaN(top)) solidPx = Mathf.Max(solidPx, (top - rb.position.y) / PxWorldY);
            floorWasPx = floorPx;
            Jumps.Floor(under, drawn, top, floorPx);
            thingTop = top;
            if (heightPx < floorPx)
            {
                // a landing (or the descend key) asks the ground; flying low over a rise just keeps above it
                if ((Now == Phase.Landing || descending) && Time.unscaledTime >= nextLandTry && TryLand(pc, under)) return;
                // Up to the floor smoothly: at once only as far as what is there (the ground, a rock, a treetop), the clear
                // height over it at FloorRisePxPerS. Taken at once the whole way, the floor's own steps were the flight's:
                // +20 px in one frame climbing out from under a ledge's edge (the higher level counting once the flight
                // was above it, the floor 30 -> 88 px) and 12-17 px a tree hovering low over a wood (the log 2026-10-23,
                // "the floor lifts the flight 16 px", "jump of +16 px in one frame").
                float gap = floorPx - heightPx;
                if (gap > 2f && floorGapWas <= 2f) Plugin.Log.LogInfo($"[flight] the floor lifts the flight {gap:0} px ({Mathf.Max(0f, solidPx - heightPx):0} at once): walkable ground under it {under:0.00}, drawn ground {drawn:0.00}, drawn things up to {top:0.00}, the body at {rb.position.y:0.00}, now {floorPx:0} px up");
                heightPx = Mathf.Max(solidPx, Mathf.MoveTowards(heightPx, floorPx, Mathf.Max(1f, FloorRisePxPerS) * Time.deltaTime));
                // not here: hold the clear height (a launch keeps rising: its spring stopped each frame under the floor,
                // the climb out from under a ledge stalled while the floor was risen to)
                vel = Now == Phase.Rising ? Mathf.Max(0f, vel) : 0f;
                floorGapWas = floorPx - heightPx;
            }
            else floorGapWas = 0f;
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
            // Something drawn stands under the flight higher than the ground the landing would put him on (a rock, a
            // roof, a cart): no landing through it. Seen 2026-10-23 at 40.62, 19.68: the floor found a thing drawn up to
            // 6.72 over ground at 3.60, the landing went on, put the body on the ground under it and the dragon came
            // down through it. The dragon stays on top of it instead ("Nowhere to land here."). Asked first: the
            // walk-away test (up to 30 ms over a few frames) ran for a spot under a tree and was refused after it anyway.
            string why = !float.IsNaN(thingTop) && thingTop > spot.y + 0.6f ? $"something drawn stands there, up to {thingTop:F2}, over the ground at {spot.y:F2}" : null;
            if (why == null) why = Mount.Instance.WhyCannotLandAt(pc, ref spot, grid: true, connectedTo: takeOffSpot, walkOut: true);
            // the ground still being judged (a few frames, Mount.Instance.WalkSliceMs each): hover, ask again next frame
            if (why == Mount.Instance.WalkPending) { nextLandTry = Time.unscaledTime; return false; }
            if (why != null)
            {
                Why = "can't land here: " + why;
                if (why != lastRefusal || !refusalSaidThisLanding) Mount.Say("Nowhere to land here.", BubbleS);
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

        // The camera's smoothed position shifted with its follow target by the body's move; the follow target's name, or
        // null without one. A method of its own: a missing Cinemachine fails here, where the caller catches it.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string MoveCameraWith(CameraController cc, Vector3 move)
        {
            Transform follow = cc.VirtualCamera != null ? cc.VirtualCamera.Follow : null;
            if (follow == null) return null;
            cc.VirtualCamera.OnTargetObjectWarped(follow, move);
            return follow.name;
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
                    Vector3 move = put.Value - pb.Rb.position;
                    if (move.sqrMagnitude > 0.0025f)
                    {
                        bodyMove += move; // with a step-up of the same frame's physics (FixedStep)
                        // The game smooths the drawn body between physics steps from the pose before; after a move the
                        // next frames blended from the old spot: the dragon showed 4.80, then 3.75, with the body at
                        // 3.60 (put down 2.54 off a plateau; a 34 px flash up and back), and a 6 px dip landing up
                        // onto one (the user's cuts, 2026-10-23). The smoothing is off until two physics steps have
                        // run from the new spot, then back as it was.
                        Rigidbody rb = pb.Rb;
                        if (smoothOffSteps == 0) smoothWas = rb.interpolation;
                        rb.interpolation = RigidbodyInterpolation.None;
                        smoothOffSteps = 2;
                    }
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
            string why = Mount.Instance.WhyCannotLandAt(pc, ref spot, grid: false, connectedTo: takeOffSpot, walkOut: true, walkBudgetMs: 0f); // down now: judged at once
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
            Mount.Say("Land first.", BubbleS);
            return true;
        }

        // Plugin.FixedUpdate: the body moved from the input, at the walking speed the game would give it.
        internal static void FixedStep()
        {
            if (smoothOffSteps > 0 && --smoothOffSteps == 0)
            {
                Rigidbody body = MainGame.PlayerController?.PhysicalBody?.Rb;
                if (body != null) body.interpolation = smoothWas;
            }
            if (!Free) return;
            PlayerController pc = MainGame.PlayerController;
            PlayerPhysicalBody pb = pc?.PhysicalBody;
            Rigidbody rb = pb?.Rb;
            if (rb == null) return;
            if (!rb.isKinematic) rb.isKinematic = true; // a flow's LockMovement(false) writes it past the flags
            Vector2 d = pc.IsControlsEnabled && !MainGame.IsGamePaused && !(movementLocked != null && movementLocked(pb)) ? LazyBearTechnology.LazyInput.GetDirection() : Vector2.zero;
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
            // The flight keeps its place in the world as the body steps up under it: its height above the body shrinks by
            // the step (the floor still holds it the clear height above the new ground), the camera is told the body
            // moved, and the body's smoothing is off for two physics steps, as for a landing's move (GiveBack). Without
            // that the whole dragon rose with the body: 0.39 onto BigDropTopSurface, 12 art px in three frames, 88
            // screen px, and it stayed that much higher until it landed (the log 2026-10-23, "jump of +4/+5/+3 px").
            if (Physics.Raycast(next + Vector3.up * StepUp, Vector3.down, out RaycastHit under, StepUp + 0.5f, GroundMask, QueryTriggerInteraction.Ignore)
                && under.point.y > next.y)
            {
                float step = under.point.y - next.y;
                next.y = under.point.y;
                if (step > 0.1f)
                {
                    // only the height above the floor makes up for it (the floor over the new ground: the old one less
                    // the step, the clear height at least): flying low the flight rises with the ground, as it must, and
                    // made up below the floor it popped back up the same frame
                    float stepPx = step / PxWorldY, was = heightPx;
                    float floorAfter = Mathf.Max(ClearPx, floorWasPx - stepPx);
                    float keep = Mathf.Clamp(heightPx - floorAfter, 0f, stepPx);
                    if (keep > 0f)
                    {
                        heightPx -= keep;
                        bodyMove += new Vector3(0f, keep * PxWorldY, 0f);
                        if (smoothOffSteps == 0) smoothWas = rb.interpolation;
                        rb.interpolation = RigidbodyInterpolation.None;
                        smoothOffSteps = 2;
                    }
                    Plugin.Log.LogInfo($"[flight] the body steps up {step:0.00} onto {under.collider.name} at {next.x:0.00},{next.z:0.00}: " +
                        (keep >= stepPx - 0.01f ? $"the flight keeps its place ({was:0} -> {heightPx:0} px over the body)" : $"the flight rises {stepPx - keep:0} px with the ground (its floor), {was:0} -> {heightPx:0} px over the body"));
                }
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

            private static string Clip(string frame) => frame == null || !frame.StartsWith("bdy_", System.StringComparison.Ordinal) ? "game" : frame.Substring(4, frame.IndexOf('_', 4) - 4);

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
            ApplyBodyMove();
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
            // landing, a glide leaves by its way out (GlideTick); any other phase stops it at once
            if (Gliding && p != Phase.Airborne && p != Phase.Landing)
            {
                Plugin.Log.LogInfo($"[flight] glide stopped: now {p} (after {glideAll:0.0} s)");
                StopGlide();
            }
            if (p == Phase.Rising) { phaseT = 0f; launched = heightPx > 0.5f; beatsLeft = launched ? 0 : Beats; nextBeat = 0f; if (launched) vel = Mathf.Max(vel, 0f); }
            if (p == Phase.Landing) { thudT = -1f; vel = Mathf.Min(vel, 0f); refusalSaidThisLanding = false; }
            if (p == Phase.Airborne || p == Phase.Ground) vel = 0f;
            Now = p;
            Why = what;
            Plugin.Log.LogInfo($"[flight] {what} (height {heightPx:0} px)");
        }
    }
}
