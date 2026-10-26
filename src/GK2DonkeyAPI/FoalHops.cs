using UnityEngine;

namespace GK2DonkeyAPI
{
    // Babies hop now and then (the user, 2026-10-26: "make the babies jump occasionally as part of their movement";
    // docs/foals.md "Hops, getting stuck, and stopping for good"). A lively foal hops in some of its steps, every few
    // seconds while it trots after its lead, and now and then on the spot while it stands about. The arc is its mount's
    // own jump (mount.cfg [Jump], JumpShapes), tapped (the tap height), times the foal's size; a mount whose jumps are off
    // doesn't hop. Drawn only: Stand() adds the lift to where the view is written, Position stays on the ground (the
    // shuffle re-homes whenever Position changes behind its back, and the slots and spacing read Position.y).
    //
    // Also here: the report of a baby that stopped getting nearer its lead (WatchFollowing).
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            private const float HopPxWorldY = 2f * 0.01666667f; // one art pixel up the screen (Flight.PxWorldY)
            private float hopT = -1f;           // seconds into the hop in progress (below 0: none)
            private float hopAt = -1f;          // a hop due in the step it walks, at this time
            private float nextFreeHopAt = -1f;  // the next hop of its own, trotting or standing
            private ShufflePhase freeHopPhase;  // the phase that hop was timed for
            private Flight.JumpShape hopShape;
            private Catalog.Entry hopShapeOf;
            private float hopLetGo;             // how long it "holds the key": a flick a hop, held the mount's full jump
            private float answerAt = -1f;       // a hop with the keeper's jump, due at this time
            private float hopLiftY;             // how far above its place the view is drawn this frame, world y
            internal int HopsStarted;           // the self-test's count
            internal float HopLiftY => hopLiftY;
            internal bool Hopping => hopT >= 0f;

            private static float HopChance => IdleShuffle.FoalHopChance != null ? IdleShuffle.FoalHopChance.Value : 0f;

            // A lively foal of a mount that jumps. Read while bound.
            private bool CanHop => Lively && HopChance > 0f && Looks.MountWanted(this).Jumps;

            // A step just picked (TryStartStep), taking about 'seconds': maybe a hop in it.
            private void PlanStepHop(float seconds)
            {
                hopAt = -1f;
                if (hopT < 0f && CanHop && Random.value < HopChance)
                {
                    hopAt = Time.time + Random.Range(0.1f, 0.6f) * Mathf.Max(0.2f, seconds);
                }
            }

            // Each active frame, before Stand(): the hop in progress, or one due.
            private void HopTick()
            {
                if (MainGame.UpdateManager != null && !MainGame.UpdateManager.IsActive)
                {
                    return; // paused: held where it is
                }
                if (hopT >= 0f)
                {
                    hopT += Mathf.Min(Time.deltaTime, 0.1f);
                    float h = Flight.JumpHeightAt(hopShape, hopT, hopLetGo, out bool done);
                    if (done)
                    {
                        hopT = -1f;
                        hopLiftY = 0f;
                    }
                    else
                    {
                        hopLiftY = Mathf.Round(h * ViewScale) * HopPxWorldY; // whole art pixels (a fraction shimmers)
                    }
                    return;
                }
                hopLiftY = 0f;
                float now = Time.time;
                if (answerAt >= 0f)
                {
                    if (now >= answerAt)
                    {
                        answerAt = -1f;
                        StartHop(big: true);
                        return;
                    }
                }
                if (!CanHop)
                {
                    hopAt = nextFreeHopAt = -1f;
                    return;
                }
                if (hopAt >= 0f)
                {
                    if (shufflePhase != ShufflePhase.Stepping && shufflePhase != ShufflePhase.Turning)
                    {
                        hopAt = -1f; // the step ended first
                    }
                    else if (now >= hopAt)
                    {
                        hopAt = -1f;
                        StartHop();
                    }
                    return;
                }
                bool trotting = shufflePhase == ShufflePhase.Following && !RoutePlanning;
                bool standing = shufflePhase == ShufflePhase.Waiting;
                if (!trotting && !standing)
                {
                    nextFreeHopAt = -1f;
                    return;
                }
                ShufflePhase phase = trotting ? ShufflePhase.Following : ShufflePhase.Waiting;
                if (nextFreeHopAt < 0f || freeHopPhase != phase)
                {
                    TimeFreeHop(phase);
                    return;
                }
                if (now >= nextFreeHopAt)
                {
                    TimeFreeHop(phase);
                    StartHop();
                }
            }

            // At the default chance (0.2): every 4-10 s trotting, 8-20 s standing; more often at a higher chance.
            private void TimeFreeHop(ShufflePhase phase)
            {
                freeHopPhase = phase;
                float scale = 0.2f / Mathf.Max(0.02f, HopChance);
                nextFreeHopAt = Time.time + (phase == ShufflePhase.Following ? Random.Range(4f, 10f) : Random.Range(8f, 20f)) * scale;
            }

            // How long it holds the key, as the keeper's own jump reads it (Flight.JumpHeightAt): from a flick (the tap
            // height) to past the rise (the mount's full height). Mostly little hops, now and then a big jump (the user,
            // 2026-10-26: "sometimes they do little hops and sometimes they do big jumps"); a jump with the keeper's
            // leans big.
            private void StartHop(bool big = false)
            {
                Catalog.Entry k = Looks.MountWanted(this);
                if (!ReferenceEquals(k, hopShapeOf))
                {
                    hopShapeOf = k;
                    hopShape = JumpShapes.Of(k);
                }
                float u = Random.value;
                u = big ? Mathf.Sqrt(u) : u * u;
                float rise = Mathf.Max(0.05f, hopShape.Time) * Mathf.Clamp(hopShape.RiseShare, 0.2f, 0.8f);
                hopLetGo = Mathf.Max(0f, hopShape.CrouchTime) + u * rise * 1.05f;
                hopT = 0f;
                HopsStarted++;
                LastHopLetGo = hopLetGo;
            }

            internal float LastHopLetGo;        // the self-test's

            // ------------------------------------------------------------------ jumping along with the keeper

            private static float AlongChance => IdleShuffle.FoalJumpAlongChance != null ? IdleShuffle.FoalJumpAlongChance.Value : 0f;

            // The keeper jumped on a mount (Flight.Jump): the babies following it (its own, trotting after it or about
            // its slots) mostly jump too, each a moment later on its own (a ripple, not in step). On foot the keeper
            // never jumps (the game has no jump, the mod's is for riding).
            internal static void KeeperJumped(Instance ridden)
            {
                if (ridden == null)
                {
                    return;
                }
                Vector3 at = ridden.Position;
                string scene = ridden.SceneId;
                ForEachInstance(i =>
                {
                    if (i.Parent != ridden || i.withKeeper || i.followLost || i.SceneId != scene || !i.HasView)
                    {
                        return;
                    }
                    if (!i.Lively || !Looks.MountWanted(i).Jumps || FlatDistance(i.Position, at) > 10f)
                    {
                        return;
                    }
                    if (i.hopT < 0f && i.answerAt < 0f && Random.value < AlongChance)
                    {
                        i.answerAt = Time.time + Random.Range(0.05f, 0.4f);
                    }
                });
            }

            // Stopped (held, a tier change, a placement): back on the ground.
            private void ResetHop()
            {
                hopT = -1f;
                hopAt = -1f;
                answerAt = -1f;
                nextFreeHopAt = -1f;
                hopLiftY = 0f;
            }

            // The self-test: a hop now, whatever the timing.
            internal bool TestHopNow()
            {
                if (!CanHop)
                {
                    return false;
                }
                StartHop();
                return true;
            }

            // ------------------------------------------------------------------ a baby that stopped following

            // A foal that hasn't got nearer its lead for StuckReportS while the lead is in its scene and not riding
            // away or going through a door: one line with everything that decides its following, once until it is near
            // again. The user, 2026-10-26: a baby stopped following and walking back to it didn't wake it; this makes
            // the next one a measurement. Once a second, at every level (a frozen or sleeping baby doesn't shuffle).
            internal const float StuckReportS = 20f;
            private float watchNextAt, watchBest = float.MaxValue, watchSince;
            private bool watchSaid;
            internal int StuckReports;

            internal void WatchFollowing()
            {
                Instance p = Parent;
                if (p == null || Time.unscaledTime < watchNextAt)
                {
                    return;
                }
                watchNextAt = Time.unscaledTime + 1f;
                if (!IsRegistered(p))
                {
                    return;
                }
                Lead l = LeadNow();
                float near = Mathf.Max(AreaW, AreaD) + 1.6f;
                float d = l.SceneId == SceneId ? FlatDistance(Position, l.Body) : float.MaxValue;
                if (d <= near || l.Moving || l.SceneId != SceneId)
                {
                    watchBest = float.MaxValue;
                    watchSince = Time.time;
                    if (d <= near)
                    {
                        watchSaid = false;
                    }
                    return;
                }
                if (d < watchBest - 0.5f)
                {
                    watchBest = d;
                    watchSince = Time.time;
                    return;
                }
                if (watchSaid || Time.time - watchSince < StuckReportS)
                {
                    return;
                }
                watchSaid = true;
                StuckReports++;
                string routeNow = routeSearch != null ? "planning" : route != null ? $"corner {routeIndex + 1} of {route.Count}" : "none";
                Plugin.Log.LogInfo($"[foal] {Describe()} has not got nearer {l.Name} for {StuckReportS:F0} s ({d:F1} away, at {Position:F2}): {LevelText()} " +
                    $"Shuffle: {ShuffleStatusText()}. Phase {shufflePhase}, lost {followLost}, wedged {wedged}, with the keeper {withKeeper}, " +
                    $"picks failed in a row {pickFailsInRow}, quick re-plans {quickReplans}, route {routeNow}, rejoin wait step {rejoinBackoff}, " +
                    $"idle shuffle {(IdleShuffle.On != null && IdleShuffle.On.Value ? "on" : "off")}, follows on foot {FollowsOnFoot()}; " +
                    $"the lead {(l.Here ? "has its view" : "has no view")}, in {l.SceneId}; it in {SceneId}.");
            }
        }
    }
}
