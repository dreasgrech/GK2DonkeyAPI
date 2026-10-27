using UnityEngine;

namespace GK2DonkeyAPI
{
    // Foals indoors (docs/foals-indoors.md): a foal with ShuffleFollowKeeperIndoors on comes through a door with the keeper
    // into a building, the church or a dungeon (the game's indoor light presets, Instance.IsIndoors), follows him there
    // instead of its parent, and comes back out with him, then returns to its parent.
    //
    // Doors are the game's teleports (PlayerController.Teleport): fade to black, SetPosition, OnPlayerTeleported, the new
    // place's light preset (set at once once the presets are loaded), the parts load, fade back, control returns. The
    // foals are moved in the frames after the event, while the screen is still black, so nobody sees them jump. Who
    // comes: the flagged foals within ShuffleFollowMaxDistance of where the keeper stood before the door (in his scene
    // then), and every one already with him.
    internal static partial class Mount
    {
        // Where the keeper was the last frame the registry ran (a teleport has moved him before anyone looks).
        private static Vector3 keeperWasAt;
        private static string keeperWasIn;

        private static int doorFrame = -1;
        private static bool doorPending;
        private static Vector3 doorFrom;
        private static string doorFromScene;
        private static float nextIndoorsCheck;
        private static object indoorsSave;     // the game these were recorded in
        private static bool loadedCheckDue;     // the first steady check of a newly loaded game
        private static bool placeAgainDue;      // a foal placed at a door without a clear slot tries again until control is back

        // The door the keeper went through within the last 'frames' frames, if any: where he came from. A quest script
        // teleports him to a fight's point and puts his armour on in the same breath (seen in the log 2026-10-14: the
        // door line, then the armour's first frame); what must happen "at the talk" happens here instead.
        internal static bool CameThroughDoorJustNow(int frames, out Vector3 from, out string fromScene)
        {
            from = doorFrom;
            fromScene = doorFromScene;
            return doorFrame >= 0 && Time.frameCount - doorFrame <= frames && !string.IsNullOrEmpty(doorFromScene);
        }

        // PlayerController.OnPlayerTeleported (subscribed by Plugin): every door, the map's fast travel, a script's move.
        // A self-test's door (SelfTest.Indoors.cs): the player's own foals stay where they are.
        internal static bool TestDoorQuiet;
        // ... except the foals of the mount ridden through it (the self-test's own mount's): they come along, nothing else moves.
        internal static bool TestDoorRiddenFoalsOnly;

        internal static void OnKeeperTeleported()
        {
            SafeSpots.NoteTeleport(); // every door, the self-test's too: nothing judged until it has settled
            if (TestDoorQuiet && !TestDoorRiddenFoalsOnly)
            {
                return;
            }
            doorFrame = Time.frameCount;
            doorPending = true;
            doorFrom = keeperWasAt;
            doorFromScene = keeperWasIn;
        }

        // Each registry frame, before the instances' own frames (MountRegistry.Tick).
        internal static void IndoorsTick(PlayerController pc)
        {
            PlayerData pd = pc.PlayerData;
            bool controls = pc.IsControlsEnabled;
            if (!ReferenceEquals(indoorsSave, readySave))
            {
                // Another game: where the keeper stood belongs to the old one (a load's own teleport is no door).
                indoorsSave = readySave;
                doorPending = false;
                keeperWasIn = null;
                loadedCheckDue = true;
            }
            if (placeAgainDue)
            {
                // The new place's parts (and its floor) load after the door's event: a foal placed in the first frames
                // found no clear slot and stood on a free one by the keeper; each frame until the screen fades back it
                // looks again (log 2026-10-03: three foals indoors, all on the last resort).
                if (controls)
                {
                    placeAgainDue = false;
                    ForEachInstance(i => i.placeAgain = false, bind: false); // or the next door would move it again
                }
                else
                {
                    bool any = false;
                    ForEachInstance(i =>
                    {
                        if (i.placeAgain)
                        {
                            i.PlaceAgain();
                            any |= i.placeAgain;
                        }
                    });
                    placeAgainDue = any;
                }
            }
            if (doorPending && (Time.frameCount >= doorFrame + 2 || controls))
            {
                doorPending = false;
                bool inside = Instance.IsIndoors();
                Plugin.Log.LogInfo($"[indoors] the keeper went through a door (event frame {doorFrame}, now {Time.frameCount}, control {(controls ? "back" : "not back yet")}): from {doorFrom:F1} in {doorFromScene} to {pd.position.Value:F1} in {pd.currentGameSceneId}, {(inside ? "indoors" : "outdoors")} ({PresetName()}).");
                ThroughDoor(pd, inside);
            }
            else if (!doorPending && controls && Time.unscaledTime >= nextIndoorsCheck)
            {
                nextIndoorsCheck = Time.unscaledTime + 1f;
                SteadyIndoors(pd);
            }
            keeperWasAt = pd.position.Value;
            keeperWasIn = pd.currentGameSceneId;
        }

        private static void ThroughDoor(PlayerData pd, bool inside)
        {
            int came = 0, back = 0;
            int stayed = 0, rode = 0;
            ForEachInstance(i =>
            {
                bool near = i.SceneId == doorFromScene && FlatDistanceStatic(i.Position, doorFrom) <= IdleShuffle.FollowMaxDistance.Value;
                if (Ridden != null && i.Parent == Ridden && !i.IsRidden && !i.withKeeper && near)
                {
                    // a foal of the mount he rides through the door: it comes along with its parent, placed by it while the
                    // screen is black, in or out. It stayed behind (the user, 2026-10-24: a mount chosen inside the
                    // house and ridden out left its babies inside): the house and the yard are one
                    // scene, so it waited for its parent to stand still out of reach, and a ridden parent never does.
                    i.FollowParentThroughDoor("came through the door with its parent");
                    rode++;
                    return;
                }
                if (TestDoorRiddenFoalsOnly) return; // a self-test's door: the player's own foals stay put
                if (!inside && i.FollowsOnFoot() && Ridden == null && (i.withKeeper || near))
                {
                    // outdoors on the far side, on foot: it comes along (placed by him while the screen is black)
                    i.JoinKeeper(i.withKeeper ? "went on with the keeper" : "came along with the keeper");
                    stayed++;
                    return;
                }
                if (!i.ComesIndoors())
                {
                    if (i.withKeeper)
                    {
                        // into a building without "come indoors": back to its parent outside (it joins him again when he
                        // is out, on foot); out of one: as before
                        i.LeaveKeeper(inside ? "it doesn't come indoors" : "it no longer comes indoors", place: false);
                    }
                    return;
                }
                if (!i.withKeeper && !near)
                {
                    return;
                }
                if (inside)
                {
                    i.JoinKeeper(i.withKeeper ? "went on with the keeper" : "came in with the keeper");
                    came++;
                }
                else if (i.withKeeper)
                {
                    i.LeaveKeeper("came out with the keeper", place: true);
                    back++;
                }
            });
            if (stayed > 0)
            {
                Plugin.Log.LogInfo($"[indoors] {stayed} foal(s) came along with the keeper on foot.");
            }
            if (rode > 0)
            {
                Plugin.Log.LogInfo($"[indoors] {rode} foal(s) came through the door with the mount he rides ({Ridden.FullId}).");
            }
            if (came > 0 || back > 0)
            {
                Plugin.Log.LogInfo($"[indoors] {(inside ? came + " foal(s) came in with the keeper" : back + " foal(s) came out with the keeper and go back to their parents")}.");
            }
        }

        // Once a second while the keeper has control: outdoors, a foal that follows him on foot (FollowsOnFoot) is with
        // him while he walks in its scene, joined by trotting over (not placed), and no other foal stays with him (a door
        // missed, a load, he got on a mount); right after a load indoors, the flagged foals near him are with him again
        // (being with him isn't saved).
        private static void SteadyIndoors(PlayerData pd)
        {
            bool inside = Instance.IsIndoors();
            bool loaded = loadedCheckDue;
            loadedCheckDue = false;
            Vector3 k = pd.position.Value;
            string scene = pd.currentGameSceneId;
            ForEachInstance(i =>
            {
                if (!inside)
                {
                    bool onFoot = i.FollowsOnFoot() && Ridden == null && i.SceneId == scene;
                    if (onFoot && !i.withKeeper)
                    {
                        i.JoinKeeper("the keeper walks on foot", place: false);
                    }
                    else if (!onFoot && i.withKeeper)
                    {
                        i.LeaveKeeper(Ridden != null ? "the keeper got on a mount" : i.FollowsOnFoot() ? "the keeper is in another place" : "the keeper is outdoors", place: false);
                    }
                    return;
                }
                if (i.withKeeper && !i.ComesIndoors())
                {
                    i.LeaveKeeper("it no longer comes indoors", place: false);
                }
                else if (loaded && inside && !i.withKeeper && i.ComesIndoors() && i.SceneId == scene
                    && FlatDistanceStatic(i.Position, k) <= IdleShuffle.FollowMaxDistance.Value)
                {
                    i.withKeeper = true;
                    DiagLog($"[foal] {i.Describe()} is indoors with the keeper again (after a load).");
                }
            });
        }

        private static string PresetName()
        {
            try
            {
                return EnvironmentEngine.Instance?.TimesOfDay?.name ?? "no preset";
            }
            catch
            {
                return "?";
            }
        }

        private static float FlatDistanceStatic(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        internal sealed partial class Instance
        {
            // With the keeper (indoors, or on foot outdoors): he is the one it follows (Foals.cs, LeadNow). Not saved.
            internal bool withKeeper;
            internal bool placeAgain;    // placed at a door without a clear slot (FreeSlot), the screen still black

            // A foal (with a parent, not ridden) whose ShuffleFollowKeeperIndoors is on. Read while bound.
            internal bool ComesIndoors()
            {
                return Parent != null && IsRegistered(Parent) && !IsRidden && IdleShuffle.FollowKeeperIndoors != null
                    && IdleShuffle.FollowKeeperIndoors.Value;
            }

            // A foal (with a parent, not ridden) whose ShuffleFollowKeeperOnFoot is on: it follows the keeper while he
            // walks outdoors on foot. Read while bound. Only a mod's own foals have it (off in the core).
            internal bool FollowsOnFoot()
            {
                return Parent != null && IsRegistered(Parent) && !IsRidden && IdleShuffle.FollowKeeperOnFoot != null
                    && IdleShuffle.FollowKeeperOnFoot.Value;
            }

            // Following the keeper from now on. place: next to him where he arrived (into his scene if need be: a door,
            // the screen black); not: it trots over from where it is, or catches up out of sight (on foot outdoors).
            internal void JoinKeeper(string why, bool place = true)
            {
                withKeeper = true;
                foalSide = 0f; // the side of the keeper it is on, picked again where it lands
                if (!place)
                {
                    followLost = false;
                    followTargetSlot = NoSlot;
                    DiagLog($"[foal] {Describe()} follows the keeper ({why}).");
                    return;
                }
                StopShuffle();
                PlaceAtDoor(why);
                WakeNow();
            }

            private void PlaceAtDoor(string why)
            {
                placeAgain = !CatchUp(why);
                if (placeAgain)
                {
                    placeAgainDue = true;
                }
            }

            // Again, once a slot with a clear line is there (quietly until then).
            internal void PlaceAgain()
            {
                Lead p = LeadNow();
                if (!p.Here)
                {
                    return;
                }
                FreeSlot(p, out bool clear);
                groundFail = null;
                if (clear)
                {
                    placeAgain = false;
                    CatchUp("placed again, its slot clear now");
                }
            }

            // Its view at once where it landed: far from the keeper a moment ago, it may have been put to sleep (no view)
            // and would otherwise be spawned on the next half-second tick, maybe after the fade (Tiers.cs).
            private void WakeNow()
            {
                TierNow();
                nextViewlessTick = 0f;
                nextSpawnTry = 0f;
            }

            // Through a door with its parent, the mount the keeper rides: placed by it (the screen black; again until a slot
            // is clear), then it follows it as usual.
            internal void FollowParentThroughDoor(string why)
            {
                withKeeper = false;
                foalSide = 0f;
                followLost = false;
                followTargetSlot = NoSlot;
                StopShuffle();
                PlaceAtDoor(why);
                WakeNow();
            }

            // Back to its parent: placed next to the keeper first when it comes out through the door with him, then it
            // follows its parent as usual (trots after it, or catches up out of sight).
            internal void LeaveKeeper(string why, bool place)
            {
                if (place)
                {
                    foalSide = 0f;
                    StopShuffle();
                    PlaceAtDoor(why); // a try again (screen still black) places it by its parent, which it goes back to anyway
                    WakeNow();
                }
                withKeeper = false;
                foalSide = 0f;
                followLost = false;
                followTargetSlot = NoSlot;
                DiagLog($"[foal] {Describe()} leaves the keeper ({why}) and goes back to its parent {Parent?.FullId}.");
            }
        }
    }
}
