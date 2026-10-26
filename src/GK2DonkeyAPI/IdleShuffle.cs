using System;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Idle shuffle (docs/roaming.md): a donkey standing in the world without a cart takes a few slow steps now
    // and then, inside a small box around its home spot, so it does not stand perfectly still. Off in the core
    // (General.IdleShuffle); a mod switches it on through Mounts.Behaviour, for everyone or per instance.
    //
    // The mod moves the donkey itself, as Follow does for the ridden one: the instance's Position along a short straight
    // step, ground-snapped, the walk animation slowed to match, the facing always the step's side. The game's
    // MovementComponent is not used: every arrival fires GlobalEventsSystem WgoGoToFinished with the definition id
    // (npc_donkey), which quest checks listen to. No route finding: a step is checked for level ground and nothing in
    // the way along its line (the game's own test for where the keeper can stand: no collider of the keeper-blocking
    // layer 8 there, PlayerColliderTester), and is never steeper than ShuffleMaxAngle from the screen's horizontal, so
    // the donkey never walks straight up or down (its art is side views only). The NPC navmesh is not required: it does
    // not cover everywhere the keeper walks (found 2026-09-30: every step was refused where the keeper left it).
    internal static class IdleShuffle
    {
        internal const string Section = "Shuffle";

        // [General]: the switch, a behaviour key (Mounts.Behaviour).
        internal static Tuned<bool> On;

        // [Shuffle]: how (Mounts.Shuffle).
        internal static Tuned<float> AreaWidth, AreaDepth, StepMin, StepMax, Speed, AnimSpeed, PauseMin, PauseMax,
            MaxAngle, TurnChance, KeeperDistance, FollowSpeed, FollowMaxDistance, FollowGap, RouteRange,
            FoalAreaWidth, FoalAreaDepth, FoalStepMin, FoalStepMax, FoalSpeedMin, FoalSpeedMax, FoalPauseMin, FoalPauseMax,
            FoalChainChance, FoalBurstChance, FoalBurstSpeed, FoalHopChance, FoalJumpAlongChance, FoalVariety;
        internal static Tuned<bool> FollowAlways, FollowRoutes, FollowKeeperIndoors, FollowKeeperOnFoot, FoalLively;

        internal static void Bind(ConfigFile cfg)
        {
            On = cfg.Bind("General", "IdleShuffle", Defaults.Get("General", "IdleShuffle", false),
                "Donkeys standing in the world without a cart (Mounts.SpawnDonkey, or left by a dismount) take a few slow steps now and then, inside a small area around where they were left, so they do not stand perfectly still. Never with a cart, never while ridden, and not while the keeper is close. Off in GK2 Donkey API itself; a mod switches it on (Saddle Up does).");
            AreaWidth = F(cfg, "ShuffleAreaWidth", 1f, 0.1f, 4f,
                "How far the donkey may wander from its home spot sideways (along the screen's horizontal), either way, world units. Its home is where it was left, spawned or moved to.");
            AreaDepth = F(cfg, "ShuffleAreaDepth", 0.5f, 0f, 2f,
                "How far the donkey may wander from its home spot towards or away from the camera (the screen's up/down), either way, world units.");
            StepMin = F(cfg, "ShuffleStepMin", 0.3f, 0.05f, 2f,
                "Shortest step, world units.");
            StepMax = F(cfg, "ShuffleStepMax", 0.9f, 0.1f, 3f,
                "Longest step, world units.");
            Speed = F(cfg, "ShuffleSpeed", 0.5f, 0.1f, 2f,
                "Walking speed of a step, world units per second (the keeper walks about 3.3; the game's donkey walk clip is made for 1.5).");
            AnimSpeed = F(cfg, "ShuffleAnimSpeed", 1f, 0.2f, 3f,
                "Walk animation rate during a step, relative to the rate that matches ShuffleSpeed (1 = the hooves match the ground). Above 1 if a very slow walk looks like a stutter.");
            PauseMin = F(cfg, "ShufflePauseMin", 5f, 0.5f, 120f,
                "Shortest pause between two steps, seconds.");
            PauseMax = F(cfg, "ShufflePauseMax", 15f, 0.5f, 300f,
                "Longest pause between two steps, seconds.");
            MaxAngle = F(cfg, "ShuffleMaxAngle", 35f, 0f, 60f,
                "Steepest step, in degrees from the screen's horizontal. The donkey has only side views, so it never steps straight up or down the screen.");
            TurnChance = F(cfg, "ShuffleTurnChance", 0.25f, 0f, 1f,
                "Chance that a step starts with turning around (the donkey always walks the way it faces). It also turns when there is no room ahead.");
            KeeperDistance = F(cfg, "ShuffleKeeperDistance", 2f, 0f, 10f,
                "The donkey holds still while the keeper is closer than this, world units, so riding it does not chase a moving target. Donkeys that can't be ridden (foals) ignore the keeper.");
            FollowSpeed = F(cfg, "ShuffleFollowSpeed", 4f, 0.3f, 8f,
                "A donkey with a parent (a foal): how fast it trots after its parent when the parent moves away, world units per second.");
            FollowAlways = cfg.Bind(Section, "ShuffleFollowAlways", Defaults.Get(Section, "ShuffleFollowAlways", false),
                "A donkey with a parent (a foal): keep following the parent however far it goes (ShuffleFollowMaxDistance is ignored); it stops only when its way is blocked, and then follows again as soon as a straight way to the parent is clear, or catches up out of sight once the parent stands.");
            FollowRoutes = cfg.Bind(Section, "ShuffleFollowRoutes", Defaults.Get(Section, "ShuffleFollowRoutes", true),
                "A donkey with a parent (a foal): when a fence, a building or anything else the keeper can't walk through is in the straight way to the parent, work out a way round it (a small grid search) and follow that. Off: straight lines only; a blocked foal waits and catches up out of sight.");
            FollowKeeperIndoors = cfg.Bind(Section, "ShuffleFollowKeeperIndoors", Defaults.Get(Section, "ShuffleFollowKeeperIndoors", false),
                "A donkey with a parent (a foal): when the keeper goes through a door into a building, the church or a dungeon (the game's indoor places) while it is within ShuffleFollowMaxDistance of him, it comes in with him and follows him there; when he goes back out it comes out with him and returns to its parent. Off: it stays with its parent outside.");
            FollowKeeperOnFoot = cfg.Bind(Section, "ShuffleFollowKeeperOnFoot", Defaults.Get(Section, "ShuffleFollowKeeperOnFoot", false),
                "A donkey with a parent (a foal): while the keeper walks outdoors on foot in its scene, it follows him instead of its parent (it trots over, or catches up out of sight), and through a door outdoors it stays with him. When he gets on a mount it goes back to its parent (following it when that is the one he rides). Into a building only with ShuffleFollowKeeperIndoors. Off: it stays with its parent. Off in the core: a mod switches it on for its own foals.");
            RouteRange = F(cfg, "ShuffleRouteRange", 8f, 2f, 8f,
                "A donkey with a parent (a foal), with ShuffleFollowRoutes: how far from the foal a way round may reach, world units either way (the search's area). Beyond it the route leads towards the parent and the next one goes on from there.");
            FollowGap = F(cfg, "ShuffleFollowGap", 0.9f, 0.3f, 4f,
                "A donkey with a parent (a foal): how close to the parent's body it settles after following, world units (the nearest of the siblings' spots; the others are a little further out). With a cart, the foals on the cart's side settle alongside the cart, level with the donkey.");
            FollowMaxDistance = F(cfg, "ShuffleFollowMaxDistance", 10f, 1f, 30f,
                "A donkey with a parent (a foal): it trots after its parent only while it is closer than this, world units. Further (or blocked, or in another place), it waits, shuffling where it is, and catches up out of sight once the parent stands again.");
            FoalLively = cfg.Bind(Section, "ShuffleFoalLively", Defaults.Get(Section, "ShuffleFoalLively", true),
                "A donkey with a parent (a foal) moves like a young one around its spot: shorter pauses, a wider area, steps of different lengths and speeds, often one after another, and now and then a quick trot. It uses the ShuffleFoal* values instead of ShuffleAreaWidth/Depth, ShuffleStepMin/Max, ShuffleSpeed and ShufflePauseMin/Max. Off: foals shuffle like grown donkeys.");
            FoalAreaWidth = F(cfg, "ShuffleFoalAreaWidth", 2f, 0.1f, 5f,
                "A lively foal (ShuffleFoalLively): how far it may wander from its spot next to its parent sideways, either way, world units.");
            FoalAreaDepth = F(cfg, "ShuffleFoalAreaDepth", 1f, 0f, 3f,
                "A lively foal: how far it may wander from its spot towards or away from the camera, either way, world units.");
            FoalStepMin = F(cfg, "ShuffleFoalStepMin", 0.4f, 0.05f, 3f,
                "A lively foal: shortest step, world units.");
            FoalStepMax = F(cfg, "ShuffleFoalStepMax", 1.5f, 0.1f, 4f,
                "A lively foal: longest step, world units (a quick trot goes up to half again as far).");
            FoalSpeedMin = F(cfg, "ShuffleFoalSpeedMin", 0.5f, 0.1f, 3f,
                "A lively foal: slowest walk of a step, world units per second; each step picks its own speed up to ShuffleFoalSpeedMax.");
            FoalSpeedMax = F(cfg, "ShuffleFoalSpeedMax", 1.2f, 0.1f, 3f,
                "A lively foal: fastest walk of a step, world units per second.");
            FoalPauseMin = F(cfg, "ShuffleFoalPauseMin", 1f, 0.2f, 60f,
                "A lively foal: shortest pause between two steps, seconds.");
            FoalPauseMax = F(cfg, "ShuffleFoalPauseMax", 4f, 0.2f, 120f,
                "A lively foal: longest pause between two steps, seconds.");
            FoalChainChance = F(cfg, "ShuffleFoalChainChance", 0.5f, 0f, 1f,
                "A lively foal: chance that a step is followed at once (within 0.4 s) by another in a new direction, so it wanders in a few strides instead of stopping after each.");
            FoalBurstChance = F(cfg, "ShuffleFoalBurstChance", 0.12f, 0f, 1f,
                "A lively foal: chance that a step is a quick trot (ShuffleFoalBurstSpeed), longer than a walk.");
            FoalBurstSpeed = F(cfg, "ShuffleFoalBurstSpeed", 2.5f, 0.5f, 5f,
                "A lively foal: speed of a quick trot, world units per second.");
            FoalHopChance = F(cfg, "ShuffleFoalHopChance", 0.2f, 0f, 1f,
                "A lively foal: chance that a step has a little hop in it (its mount's own jump, tapped, at the foal's size). It also hops now and then while it trots after its parent or the keeper (every 4-10 s at 0.2) and on the spot while it stands about (every 8-20 s at 0.2); a higher chance hops more often. 0: no hops. A mount that doesn't jump ([Mount] jumps = false) never hops.");
            FoalJumpAlongChance = F(cfg, "ShuffleFoalJumpAlongChance", 0.75f, 0f, 1f,
                "A lively foal following the mount the keeper rides: chance that it jumps too when he jumps (Space), a moment after him, usually a big one. 0: never.");
            FoalVariety = F(cfg, "ShuffleFoalVariety", 1f, 0f, 2f,
                "How different a mount's babies are from each other (a companion's babies: Saddle Up's): each its own size, its own shade of the parent's colours, its own temperament (lively and roaming, or calm and staying close), trot, walk and voice. The same baby every load (from a number kept with its parent). 1: noticeable (size about 12 % up or down, brightness 15 %, hue 10 degrees); 0.5 subtle; 2 strong; 0: all alike.");
        }

        private static ConfigEntry<float> F(ConfigFile cfg, string key, float def, float min, float max, string desc)
        {
            return cfg.Bind(Section, key, Defaults.Get(Section, key, def), new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));
        }

        // Mounts.Shuffle, the panel and the defaults, in this order.
        internal static ITuned[] Settings => new ITuned[]
        {
            AreaWidth, AreaDepth, StepMin, StepMax, Speed, AnimSpeed, PauseMin, PauseMax, MaxAngle, TurnChance, KeeperDistance,
            FollowSpeed, FollowMaxDistance, FollowGap, FollowAlways, FollowRoutes, RouteRange, FollowKeeperIndoors, FollowKeeperOnFoot,
            FoalLively, FoalAreaWidth, FoalAreaDepth, FoalStepMin, FoalStepMax, FoalSpeedMin, FoalSpeedMax, FoalPauseMin,
            FoalPauseMax, FoalChainChance, FoalBurstChance, FoalBurstSpeed, FoalHopChance, FoalJumpAlongChance, FoalVariety,
        };

        internal static ConfigEntryBase[] Entries
        {
            get
            {
                ITuned[] s = Settings;
                var e = new ConfigEntryBase[s.Length];
                for (int i = 0; i < s.Length; i++)
                {
                    e[i] = s[i].EntryBase;
                }
                return e;
            }
        }
    }

    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            private enum ShufflePhase
            {
                Off,      // not shuffling (switched off, ridden, a cart, no view)
                Waiting,  // standing, until shuffleAt
                Turning,  // the facing is flipped this frame; the root moves next frame, when the view shows it
                Stepping, // walking from stepFrom to stepTo
                Following, // a foal trotting after its parent (Foals.cs)
            }

            private ShufflePhase afterTurn = ShufflePhase.Stepping; // what a turn goes on with

            private ShufflePhase shufflePhase;
            private float shuffleAt;
            private bool shuffleHomeSet;
            private Vector3 shuffleHome;                     // the body point it wanders around
            private Vector3 shuffleWrote;                    // the Position the shuffle last set: any other means it was moved
            private float shuffleLateral;                    // the body's offset from the root along 'right', times the facing
            private bool shuffleLateralKnown;
            private int shuffleTurnFrame;
            private float shuffleFacingSeen;                 // the facing last seen, and the frame it last changed:
            private int shuffleFacingFrame;                  // the view shows a new facing only a frame later
            private Vector3 turnRoot;
            private Vector3 stepFrom, stepTo;
            private float stepLength, stepDone;
            private float stepSpeed = 0.5f;                  // this step's walk (a lively foal picks one per step)
            private bool stepBurst;                          // this step is a lively foal's quick trot

            // A foal moving like a young one (ShuffleFoalLively, read while bound): its own area, steps, speeds and
            // pauses, steps often chained, now and then a quick trot. Grown donkeys: the ordinary values.
            private bool Lively => Parent != null && IdleShuffle.FoalLively != null && IdleShuffle.FoalLively.Value;
            // A lively foal's values times its own temperament (FoalVariety.cs; all 1 for a baby without one).
            private float AreaW => Lively ? IdleShuffle.FoalAreaWidth.Value * Temper.Area : IdleShuffle.AreaWidth.Value;
            private float AreaD => Lively ? IdleShuffle.FoalAreaDepth.Value * Temper.Area : IdleShuffle.AreaDepth.Value;
            private float PauseLo => Lively ? IdleShuffle.FoalPauseMin.Value * Temper.Pause : IdleShuffle.PauseMin.Value;
            private float PauseHi => Mathf.Max(PauseLo, Lively ? IdleShuffle.FoalPauseMax.Value * Temper.Pause : IdleShuffle.PauseMax.Value);
            private int turnLogFrames;
            private bool shuffleAnnounced;                   // the "shuffles around" log line, once per instance
            private string shuffleHold;                      // why it is not shuffling right now (null: it is), for the panel
            private string groundFail;                       // the first ground check that failed since the last log line
            private string lastPickFail;                     // the last "found no step" summary, for the panel
            private float shuffleFailLogAt;
            private int failBox, failGround, failNear, failTurnRoom, failNoOffset;   // since the last log line (once a minute)
            private int pickWorld, pickCrowd;   // this pick only: rejected by the world (ground, room to turn) / by the area or others

            // Walking a step right now (MountHandle.IsStepping).
            internal bool IsStepping => shufflePhase == ShufflePhase.Stepping || shufflePhase == ShufflePhase.Turning || shufflePhase == ShufflePhase.Following;

            private const float StepEase = 0.12f;        // world units eased in and out at both ends of a step
            private const int PickTries = 12;
            // Ground height change allowed between two samples of a step's line, and over the whole step. A dropped corpse
            // (and any big drop) has a walkable top 0.4 above the ground that the keeper walks over; with 0.12 the foals
            // climbed onto one and could never step down again, and ordinary slopes the keeper rides were refused too
            // (found 2026-09-30). Cliffs are far higher, and walls have keeper-blocking colliders.
            private const float MaxRisePerSample = 0.45f;
            private const float MaxRise = 0.6f;           // stepping out of an obstacle: the free spot's height against its own
            private const float InstanceGap = 1.1f;       // no step ends this close to another donkey or cart

            // A frame of a standing instance, before Stand(): the shuffle moves Position; Stand() draws it.
            private void ShuffleTick(PlayerController pc)
            {
                shuffleHold = !HasDonkey ? "never: a cart on its own"
                    : Ephemeral ? "never: the summoned donkey"
                    : mounted ? "off: ridden"
                    : HasCart ? "off: it pulls a cart"
                    : IdleShuffle.On == null || !IdleShuffle.On.Value ? "off: Idle shuffle is off for it"
                    : null;
                if (shuffleHold != null)
                {
                    StopShuffle();
                    return;
                }
                if (MainGame.UpdateManager != null && !MainGame.UpdateManager.IsActive)
                {
                    shuffleHold = "holding: the game is paused";
                    return; // the game is paused (its own movement system holds too)
                }
                if (riderFacingX != shuffleFacingSeen)
                {
                    shuffleFacingSeen = riderFacingX;
                    shuffleFacingFrame = Time.frameCount;
                }
                MeasureLateral();
                if (Time.time >= nextInsideCheck && !IsStepping)
                {
                    nextInsideCheck = Time.time + 1f;
                    StepOutIfInside();
                }
                // A foal's home is its parent's body, or the keeper it is with indoors (while it can follow; Foals.cs).
                bool parentHome = Parent != null && !followLost && IsRegistered(Parent) && LeadIsHere();
                if (!shuffleHomeSet || (Position - shuffleWrote).sqrMagnitude > 0.000001f)
                {
                    // First frame, or moved by something else (MoveTo, ComeToKeeper, a dismount, a hitch): home is here.
                    followLost = false;
                    shuffleHome = parentHome ? SlotNearParent(SiblingIndex()) : BodyAt(Position, riderFacingX);
                    shuffleHomeSet = true;
                    shuffleWrote = Position;
                    if (shufflePhase == ShufflePhase.Turning)
                    {
                        shufflePhase = ShufflePhase.Waiting; // moved by someone else mid-turn: their place wins
                    }
                    EndStep(firstPause: true);
                    if (!shuffleAnnounced && shuffleLateralKnown)
                    {
                        shuffleAnnounced = true;
                        if (DevDiag) DiagLog($"[shuffle] {Describe()} shuffles around {shuffleHome:F2} in {SceneId} (body offset {shuffleLateral:F2}, navmesh {(MainGame.PlayerController?.SceneRecastGraph != null ? "yes" : "no")}, first step in {shuffleAt - Time.time:F1} s).");
                    }
                }
                if (parentHome && !followLost)
                {
                    shuffleHome = SlotNearParent(SiblingIndex()); // its own spot next to the parent or keeper (Foals.cs)
                }
                bool following;
                using (Perf.Measure(Perf.S.Follow))
                {
                    following = FollowParentTick(pc);
                }
                if (following)
                {
                    LogTurn();
                    return;
                }
                if (KeeperClose(pc, BodyAt(Position, riderFacingX)))
                {
                    shuffleHold = HoldingText(); // the same string while the distance is (it ran every frame for every near donkey)
                    if (IsStepping)
                    {
                        EndStep(firstPause: false);
                    }
                    shuffleAt = Mathf.Max(shuffleAt, Time.time + 1f);
                    return;
                }
                switch (shufflePhase)
                {
                    case ShufflePhase.Off:
                        EndStep(firstPause: true);
                        break;
                    case ShufflePhase.Waiting:
                        if (Time.time >= shuffleAt && !TryStartStep(pc))
                        {
                            NotePickFailed(pickWorld > pickCrowd);
                            LogFailedPick();
                            shuffleAt = Time.time + UnityEngine.Random.Range(PauseLo, PauseHi) * 0.5f;
                        }
                        break;
                    case ShufflePhase.Turning:
                        if (Time.frameCount > shuffleTurnFrame)
                        {
                            SetShufflePosition(turnRoot);
                            stepFrom = Position;
                            shufflePhase = afterTurn;
                            afterTurn = ShufflePhase.Stepping;
                        }
                        break;
                    case ShufflePhase.Stepping:
                        Advance(Mathf.Min(Time.deltaTime, 0.1f));
                        break;
                }
                LogTurn();
            }

            private static float holdingFor = float.NaN;
            private static string holdingText;

            private static string HoldingText()
            {
                float d = IdleShuffle.KeeperDistance.Value;
                if (d != holdingFor || holdingText == null)
                {
                    holdingFor = d;
                    holdingText = $"holding: the keeper is within {d:F1}";
                }
                return holdingText;
            }

            private void StopShuffle()
            {
                FinishTurn(); // not if something else just moved it (a hitch): its place wins
                route = null;
                routeSearch = null; // its tests use the view, which may be gone
                ResetHop();
                shufflePhase = ShufflePhase.Off;
                shuffleHomeSet = false;
            }

            // The step ends where the donkey is; the next one after a pause.
            private void EndStep(bool firstPause)
            {
                FinishTurn();
                float min = PauseLo;
                float max = PauseHi;
                // The first pause after being left is a random part of a full one, so donkeys left together do not step
                // in time.
                shuffleAt = Time.time + UnityEngine.Random.Range(firstPause ? 0.3f * min : min, max);
                // A lively foal often goes straight on with another step (a fresh direction, maybe a turn): a wander of a
                // few strides, not a stop after every one (the user, 2026-10-03: "go from point a to b, then stop, repeat").
                if (!firstPause && shufflePhase == ShufflePhase.Stepping && Lively && UnityEngine.Random.value < Mathf.Min(0.95f, IdleShuffle.FoalChainChance.Value * Temper.Chain))
                {
                    shuffleAt = Time.time + UnityEngine.Random.Range(0.05f, 0.4f);
                }
                shufflePhase = ShufflePhase.Waiting;
            }

            private void Advance(float dt)
            {
                // Eased in and out over the ends of the step (never below a quarter speed, so it does not creep).
                // A faster step eases over a longer stretch, so a quick trot doesn't start or stop dead.
                float fromStart = stepDone, toEnd = stepLength - stepDone;
                float easeOver = Mathf.Min(Mathf.Clamp(stepSpeed * 0.25f, StepEase, 0.5f), stepLength * 0.5f);
                float ease = Mathf.Clamp01(Mathf.Min(fromStart, toEnd) / Mathf.Max(0.01f, easeOver));
                float speed = stepSpeed * Mathf.Lerp(0.25f, 1f, ease * ease * (3f - 2f * ease));
                stepDone = Mathf.Min(stepLength, stepDone + speed * dt);
                Vector3 p = Vector3.Lerp(stepFrom, stepTo, stepLength > 0.0001f ? stepDone / stepLength : 1f);
                // The height of the ground under the body, not under the root half a unit beside it: that is how Follow
                // places the ridden donkey (ground under the body sprite), and on a slope the root's ground put the body
                // into the hill (found 2026-09-30).
                float g = StandHeight(BodyAt(p, riderFacingX));
                p.y = float.IsNaN(g) ? Position.y : g;
                SetShufflePosition(p);
                if (stepDone >= stepLength - 0.0001f)
                {
                    EndStep(firstPause: false);
                }
            }

            // A turn cut short (keeper close, switched off): the facing is already flipped, so the root must go to the
            // body's other side too, or the body would stand a unit off.
            private void FinishTurn()
            {
                if (shufflePhase == ShufflePhase.Turning && (Position - shuffleWrote).sqrMagnitude < 0.000001f)
                {
                    SetShufflePosition(turnRoot);
                    shufflePhase = ShufflePhase.Waiting;
                }
            }

            private void SetShufflePosition(Vector3 p)
            {
                Position = p;
                shuffleWrote = p;
            }

            // Picks a step that stays in the box around home and on walkable, level ground, forward in the facing (or
            // after turning around); false when none of the tries fits.
            private bool TryStartStep(PlayerController pc)
            {
                pickWorld = pickCrowd = 0;
                if (!shuffleLateralKnown)
                {
                    failNoOffset++;
                    return false; // the view's body offset is not measured yet (a turn would jump the body)
                }
                Vector3 away = AwayFromCamera();
                Vector3 right = Vector3.Cross(Vector3.up, away).normalized;
                float worldToRight = Vector3.Dot(Vector3.right, right) >= 0f ? 1f : -1f; // facing is the sign of world x
                float f = riderFacingX;
                Vector3 body = BodyAt(Position, f);
                // How far outside its area it is now (0 inside), in area units: a step that brings it closer is allowed
                // even if it doesn't end inside. A foal half a unit out (its spot moved) had no step at all.
                float OutBy(Vector3 p, Vector3 rgt, Vector3 awy, float aw, float ad)
                {
                    Vector3 rel0 = p - shuffleHome;
                    float ox = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(rel0, rgt)) - aw) / Mathf.Max(aw, 0.05f);
                    float oz = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(rel0, awy)) - ad) / Mathf.Max(ad, 0.05f);
                    return Mathf.Max(ox, oz);
                }
                bool turnFirst = UnityEngine.Random.value < IdleShuffle.TurnChance.Value;
                bool lively = Lively;
                float sMin = lively ? IdleShuffle.FoalStepMin.Value : IdleShuffle.StepMin.Value;
                float sMax = lively ? IdleShuffle.FoalStepMax.Value : IdleShuffle.StepMax.Value;
                float lenMin = Mathf.Min(sMin, sMax);
                float lenMax = Mathf.Max(sMin, sMax);
                float speed = IdleShuffle.Speed.Value;
                bool burst = false;
                if (lively)
                {
                    float vMin = IdleShuffle.FoalSpeedMin.Value, vMax = IdleShuffle.FoalSpeedMax.Value;
                    speed = UnityEngine.Random.Range(Mathf.Min(vMin, vMax), Mathf.Max(vMin, vMax)) * Temper.Speed;
                    if (UnityEngine.Random.value < IdleShuffle.FoalBurstChance.Value)
                    {
                        // A quick trot: faster, and longer (the far half of the step range and up to half again).
                        burst = true;
                        speed = IdleShuffle.FoalBurstSpeed.Value * Temper.Speed;
                        lenMin = Mathf.Lerp(lenMin, lenMax, 0.5f);
                        lenMax *= 1.5f;
                    }
                }
                float maxAngle = IdleShuffle.MaxAngle.Value * Mathf.Deg2Rad;
                float w = AreaW, d = AreaD;
                for (int pass = 0; pass < 2; pass++)
                {
                    bool turn = (pass == 0) == turnFirst;
                    float nf = turn ? -f : f;
                    // Turning keeps the body where it is: the root goes to the body's other side.
                    Vector3 root = turn ? body - right * LateralFor(nf) : Position;
                    if (turn && !LineWalkable(Position, root, "turn"))
                    {
                        failTurnRoom++;
                        pickWorld++;
                        continue;
                    }
                    for (int t = 0; t < PickTries; t++)
                    {
                        float len = UnityEngine.Random.Range(lenMin, lenMax);
                        float a = UnityEngine.Random.Range(-maxAngle, maxAngle);
                        Vector3 dir = right * (nf * worldToRight * Mathf.Cos(a)) + away * Mathf.Sin(a);
                        Vector3 bodyTo = body + dir * len;
                        float outTo = OutBy(bodyTo, right, away, w, d);
                        if (outTo > 0f && outTo >= OutBy(body, right, away, w, d) - 0.01f)
                        {
                            failBox++;
                            pickCrowd++;
                            continue;
                        }
                        Vector3 rootTo = root + dir * len;
                        if (NearOther(bodyTo) || KeeperClose(pc, bodyTo))
                        {
                            failNear++;
                            pickCrowd++;
                            continue;
                        }
                        if (!LineWalkable(root, rootTo, "root") || !LineWalkable(body, bodyTo, "body"))
                        {
                            failGround++;
                            pickWorld++;
                            continue;
                        }
                        stepTo = rootTo;
                        stepLength = len;
                        stepSpeed = speed;
                        stepBurst = burst;
                        stepDone = 0f;
                        stepsTaken++;
                        lastStepAt = Time.time;
                        NotePickOk();
                        PlanStepHop(len / Mathf.Max(0.1f, speed)); // FoalHops.cs
                        if (turn)
                        {
                            riderFacingX = nf; // Stand() shows the new facing this frame; the view flips in the next
                            turnRoot = root;
                            afterTurn = ShufflePhase.Stepping;
                            shuffleTurnFrame = Time.frameCount;
                            shufflePhase = ShufflePhase.Turning;
                            if (Plugin.TuningWindowEnabled.Value)
                            {
                                turnLogFrames = 3;
                                if (DevDiag) DiagLog($"[shuffle] {Describe()} turns to face {(nf > 0f ? "right" : "left")}: body {BodyNowForLog()}, root {Position:F3} -> {root:F3}.");
                            }
                        }
                        else
                        {
                            stepFrom = Position;
                            shufflePhase = ShufflePhase.Stepping;
                        }
                        return true;
                    }
                }
                return false;
            }

            // The body point of the donkey with its root at 'root' facing 'facing' (the view draws the body half a unit to
            // its facing side of the root).
            private Vector3 BodyAt(Vector3 root, float facing)
            {
                Vector3 right = Vector3.Cross(Vector3.up, AwayFromCamera()).normalized;
                return root + right * LateralFor(facing);
            }

            private float LateralFor(float facing) => shuffleLateralKnown ? shuffleLateral * facing : 0f;

            // The body's offset in this view, while the view shows the facing it was given (not right after a turn).
            private void MeasureLateral()
            {
                // Not while the view may still show the previous facing (a turn of ours, or MoveTo / ComeToKeeper with a
                // facing): the offset would be taken with the wrong sign.
                if (bodySprite == null || !bodySprite || donkey == null || !donkey || shufflePhase == ShufflePhase.Turning
                    || Time.frameCount <= shuffleTurnFrame + 2 || Time.frameCount <= shuffleFacingFrame + 2)
                {
                    return;
                }
                Vector3 right = Vector3.Cross(Vector3.up, AwayFromCamera()).normalized;
                float lat = Vector3.Dot(bodySprite.position - donkey.transform.position, right);
                if (!shuffleLateralKnown || Mathf.Abs(lat * riderFacingX - shuffleLateral) > 0.01f)
                {
                    bool first = !shuffleLateralKnown;
                    shuffleLateral = lat * riderFacingX;
                    shuffleLateralKnown = true;
                    if (first)
                    {
                        // Home was taken without the offset: take it again with it.
                        shuffleHomeSet = false;
                    }
                }
            }

            // Where the keeper could stand along a line: ground under every sample (navmesh or ground raycast), level enough
            // (no step off a ledge or up a wall), and no collider of the keeper-blocking layer in the way (a building, a
            // fence, a tree; the donkey's own colliders are off). The first failure is kept for the log.
            private const int BlockingLayers = 256;          // layer 8: what PlayerColliderTester checks the keeper against
            private const float BodyRadius = 0.25f;
            private static readonly Collider[] blockers = new Collider[8];
            private static Rigidbody keeperBody;

            private bool LineWalkable(Vector3 a, Vector3 b, string what)
            {
                using var perf = Perf.Measure(Perf.S.Line);
                keeperBody = MainGame.PlayerController?.PhysicalBody?.Rb;
                float prev = float.NaN;
                Vector3 prevP = a;
                float start = float.NaN;
                // At least 5 intervals, and no more than 0.2 apart on long lines (a foal rejoining from far away), up to 80.
                float len = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                int Samples = Mathf.Clamp(Mathf.CeilToInt(len / 0.2f), 5, 80);
                for (int i = 0; i <= Samples; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)Samples);
                    float g = GroundHeight(p, out string src);
                    if (float.IsNaN(g))
                    {
                        if (groundFail == null) NoteGroundFail($"{what} sample {i} at {p:F2}: no ground ({src})");
                        return false;
                    }
                    if (i == 0)
                    {
                        start = g;
                    }
                    // Only the change between neighbouring samples (a ledge, a wall): a limit on the whole line's change
                    // refused every longer slope (a trotting foal checks up to 1.8 ahead, and down a ramp that dropped
                    // more than 0.6 in small steps, found 2026-09-30).
                    else if (!StepFits(prevP, prev, p, g, MaxRisePerSample))
                    {
                        if (groundFail == null) NoteGroundFail($"{what} sample {i} at {p:F2}: ground {g:F2} ({src}) against {prev:F2} before, {start:F2} at the start");
                        return false;
                    }
                    prev = g;
                    prevP = p;
                    Vector3 foot = new Vector3(p.x, g, p.z);
                    int n = Physics.OverlapCapsuleNonAlloc(foot + Vector3.up * (BodyRadius + 0.1f), foot + Vector3.up * 0.9f, BodyRadius,
                        blockers, BlockingLayers, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        Collider c = blockers[k];
                        if (c == null || !c || donkey != null && donkey && c.transform.IsChildOf(donkey.transform)
                            || keeperBody != null && c.attachedRigidbody == keeperBody)
                        {
                            continue; // its own view, or the keeper (KeeperClose handles him; a foal trots right behind him)
                        }
                        if (groundFail == null) NoteGroundFail($"{what} sample {i} at {foot:F2}: blocked by {c.name} (layer {LayerMask.LayerToName(c.gameObject.layer)})");
                        return false;
                    }
                }
                return true;
            }

            // Standing inside something the keeper can't walk through (spawned or left there, or walked in before this was
            // guarded): every step's first sample fails there, so it could never move again. It moves to the nearest free
            // spot within about a unit (a small jump, only in that case).
            private float nextInsideCheck;

            private void StepOutIfInside()
            {
                Vector3 body = BodyAt(Position, riderFacingX);
                if (FreeAt(body, out Collider inside))
                {
                    return;
                }
                Vector3 right = Vector3.Cross(Vector3.up, AwayFromCamera()).normalized;
                for (float r = 0.3f; r <= 1.21f; r += 0.3f)
                {
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI / 6f;
                        Vector3 p = body + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        float g = StandHeight(p);
                        if (float.IsNaN(g) || Mathf.Abs(g - body.y) > MaxRise)
                        {
                            continue;
                        }
                        p.y = g;
                        if (!FreeAt(p, out _) || !FreeAt(p - right * LateralFor(riderFacingX), out _))
                        {
                            continue;
                        }
                        // A donkey someone rides: its whole body out of it, not just its middle (SafeGround.FootprintBlocked).
                        if (Rideable && FootprintBlocked(MainGame.PlayerController, p, right) != null)
                        {
                            continue;
                        }
                        // Not out on the far side of the wall it stands in: 2026-10-04 a donkey left in a rock stepped out
                        // into a closed-off pocket behind it, and getting on it put the keeper there (SafeGround.cs). On the
                        // keeper's part of the walkable mesh, and in a clear line from him when he is close.
                        PlayerController kpc = MainGame.PlayerController;
                        if (kpc != null && kpc.PlayerData != null && kpc.PlayerData.currentGameSceneId == SceneId)
                        {
                            Vector3 kp = KeeperNow(kpc);
                            Vector2 flat = new Vector2(p.x - kp.x, p.z - kp.z);
                            if (WhyNotConnected(kp, p) != null || flat.sqrMagnitude < 64f && PlaceBlocked(kpc, kp, p) != null)
                            {
                                continue;
                            }
                        }
                        Vector3 root = p - right * LateralFor(riderFacingX);
                        root.y = g;
                        SetShufflePosition(root);
                        if (Parent == null)
                        {
                            shuffleHome = p; // a donkey's home moves with it; a foal's is its parent
                        }
                        followLost = wedged = false;
                        pickFailsInRow = 0;
                        if (DevDiag) DiagLog($"[shuffle] {Describe()} stood inside {inside?.name} ({(inside != null ? LayerMask.LayerToName(inside.gameObject.layer) : "?")}); moved {r:F1} out to {p:F2}.");
                        return;
                    }
                }
            }

            // Nothing keeper-blocking at a ground point (the capsule LineWalkable uses).
            private bool FreeAt(Vector3 foot, out Collider hit)
            {
                using var perf = Perf.Measure(Perf.S.Free);
                hit = null;
                keeperBody = MainGame.PlayerController?.PhysicalBody?.Rb;
                int n = Physics.OverlapCapsuleNonAlloc(foot + Vector3.up * (BodyRadius + 0.1f), foot + Vector3.up * 0.9f, BodyRadius,
                    blockers, BlockingLayers, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    Collider c = blockers[k];
                    if (c == null || !c || donkey != null && donkey && c.transform.IsChildOf(donkey.transform)
                        || keeperBody != null && c.attachedRigidbody == keeperBody)
                    {
                        continue;
                    }
                    hit = c;
                    return false;
                }
                return true;
            }

            // Only the first failure is kept: callers test groundFail == null before building the text, since a rejected
            // line is common (every pick tries several) and the formatted text is garbage once one is kept.
            private void NoteGroundFail(string s)
            {
                if (groundFail == null)
                {
                    groundFail = s;
                }
            }

            private bool KeeperClose(PlayerController pc, Vector3 point)
            {
                if (!Rideable)
                {
                    return false; // nobody gets on a foal, so it needs no stillness for the keeper
                }
                float dist = IdleShuffle.KeeperDistance.Value;
                if (dist <= 0f || pc == null || pc.PlayerData.currentGameSceneId != SceneId)
                {
                    return false;
                }
                Vector3 k = pc.PlayerData.position.Value;
                float dx = k.x - point.x, dz = k.z - point.z;
                return dx * dx + dz * dz < dist * dist;
            }

            private bool NearOther(Vector3 point)
            {
                foreach (Instance o in InScene(SceneId))
                {
                    if (o == this)
                    {
                        continue;
                    }
                    Vector3 b = o.BodyPoint();
                    float dx = b.x - point.x, dz = b.z - point.z;
                    float gap = InstanceGap * 0.5f * (ViewScale + o.ViewScale); // two full donkeys 1.1, a foal and its parent less
                    if (o.Parent != null && Lively)
                    {
                        // Lively foals play close to each other, never inside each other (Foals.cs, MinApart).
                        gap = Mathf.Max(MinApart + 0.05f, gap * 0.8f);
                    }
                    if (dx * dx + dz * dz < gap * gap)
                    {
                        return true;
                    }
                }
                return false;
            }

            // Why no step could be found, at most once a minute per instance (counts since the last line).
            private void LogFailedPick()
            {
                lastPickFailAt = Time.time;
                if (Time.time < shuffleFailLogAt)
                {
                    return;
                }
                shuffleFailLogAt = Time.time + 60f;
                lastPickFail = $"outside the area {failBox}, near the keeper or another donkey {failNear}, ground {failGround}, no room to turn {failTurnRoom}, body offset not measured {failNoOffset}";
                if (DevDiag) DiagLog($"[shuffle] {Describe()} found no step at {BodyAt(Position, riderFacingX):F2} (home {shuffleHome:F2}): rejected {lastPickFail}.{(groundFail != null ? " First ground failure: " + groundFail + "." : "")}");
                failBox = failGround = failNear = failTurnRoom = failNoOffset = 0;
                groundFail = null;
            }

            // Dev log (with the tuning panel on): where the body is drawn for a few frames after a turn, to see whether
            // the one-frame hold keeps it in place.
            private void LogTurn()
            {
                if (turnLogFrames <= 0 || shufflePhase == ShufflePhase.Turning && Time.frameCount == shuffleTurnFrame)
                {
                    return;
                }
                turnLogFrames--;
                if (DevDiag) DiagLog($"[shuffle] {Describe()} frame +{Time.frameCount - shuffleTurnFrame}: body {BodyNowForLog()}, root {Position:F3}, {shufflePhase}.");
            }

            private float lastPickFailAt = -1000f;
            private float lastStepAt = -1000f;
            private int stepsTaken;

            // One line for the panel's Instances tab: what the idle shuffle is doing with this donkey right now.
            internal string ShuffleStatusText()
            {
                if (mounted)
                {
                    return "off: ridden";
                }
                if (donkey == null || !donkey || !partsReady)
                {
                    return "waiting for its view (its scene isn't loaded)";
                }
                string s;
                if (shuffleHold != null)
                {
                    s = shuffleHold;
                }
                else
                {
                    switch (shufflePhase)
                    {
                        case ShufflePhase.Waiting:
                            s = $"next step in {Mathf.Max(0f, shuffleAt - Time.time):F1} s";
                            break;
                        case ShufflePhase.Turning:
                            s = "turning around";
                            break;
                        case ShufflePhase.Stepping:
                            s = $"{(stepBurst ? "trotting" : "stepping")} {(riderFacingX > 0f ? "right" : "left")} at {stepSpeed:F1}/s, {stepDone:F2} of {stepLength:F2}";
                            break;
                        case ShufflePhase.Following:
                            string to = Parent != null ? LeadNow().Name : "its parent";
                            s = RoutePlanning ? $"working out a way round to {to}"
                                : route != null ? $"trotting round an obstacle to {to}, corner {routeIndex + 1} of {route.Count}"
                                : $"trotting after {to} at {followSpeedNow:F1}/s";
                            break;
                        default:
                            s = "starting";
                            break;
                    }
                }
                if (shuffleHomeSet && shuffleHold == null)
                {
                    Vector3 away = AwayFromCamera();
                    Vector3 right = Vector3.Cross(Vector3.up, away).normalized;
                    Vector3 rel = BodyAt(Position, riderFacingX) - shuffleHome;
                    s += $"; {Vector3.Dot(rel, right):+0.00;-0.00} sideways, {Vector3.Dot(rel, away):+0.00;-0.00} up/down from home";
                }
                if (followLost && Parent != null)
                {
                    s += $"; lost {LeadNow().Name}, catches up out of sight";
                }
                if (withKeeper)
                {
                    s += "; indoors with the keeper";
                }
                if (stepsTaken > 0)
                {
                    s += $"; {stepsTaken} step(s), the last {Time.time - lastStepAt:F0} s ago";
                }
                if (lastPickFail != null && Time.time - lastPickFailAt < 90f && shuffleHold == null)
                {
                    s += $"; last try found no step ({lastPickFail})";
                }
                return s;
            }

            private string BodyNowForLog() => bodySprite != null && bodySprite ? bodySprite.position.ToString("F3") : "?";

            // The animator state Stand() keeps: Walk during a step, Idle otherwise.
            // Walk while stepping or trotting; a foal working out a route stands for those frames.
            private AnimationState StandState => shufflePhase == ShufflePhase.Stepping || shufflePhase == ShufflePhase.Following && !RoutePlanning ? AnimationState.Walk : AnimationState.Idle;

            // The walk cycle's rate for the speed it moves at: shorter legs (ViewScale) step faster for the same ground,
            // and a mount's own stride (WalkCycleSpeed against the donkey's 1.5: the dragon's longer steps, 2026-10-24)
            // plays its cycle slower for the same ground, as it does ridden (the donkey: x 1, unchanged)
            private float StandWalkSpeed => (shufflePhase == ShufflePhase.Following ? followSpeedNow : stepSpeed)
                * IdleShuffle.AnimSpeed.Value / Mathf.Max(0.1f, ViewScale) * (S.WalkCycleSpeed.Value / DonkeyWalkCycleSpeed) * Temper.Walk;
            private const float DonkeyWalkCycleSpeed = 1.5f;

            private float walkSpeedApplied = -1f;
            internal float StandWalkSpeedApplied => walkSpeedApplied; // the stride probe (Dev/StrideProbe.cs)

            private void ApplyStandState()
            {
                AnimationState want = StandState;
                if (lastMirrored == want)
                {
                    if (want == AnimationState.Walk && Mathf.Abs(StandWalkSpeed - walkSpeedApplied) > 0.15f)
                    {
                        walkSpeedApplied = StandWalkSpeed;
                        donkey.MainWgoPart?.AnimationComponent?.SetWalkAnimationSpeedMultiplier(walkSpeedApplied);
                    }
                    return;
                }
                lastMirrored = want;
                AnimationComponentBase anim = donkey.MainWgoPart?.AnimationComponent;
                if (want == AnimationState.Walk && anim != null)
                {
                    // As in Follow: the idle one-shots (graze, ear, lips) would finish before the walk starts.
                    anim.SetTrigger(AnimationComponentBase.RESET_TO_IDLE_TRIGGER);
                }
                donkey.Data.SetStateToAnimator(want);
                if (anim != null)
                {
                    walkSpeedApplied = want == AnimationState.Walk ? StandWalkSpeed : S.WalkCycleSpeed.Value;
                    anim.SetWalkAnimationSpeedMultiplier(walkSpeedApplied);
                }
            }
        }
    }
}
