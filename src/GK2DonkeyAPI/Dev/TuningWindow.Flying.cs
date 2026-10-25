using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Flying tab: the flying experiment (Flight.cs, docs/flying.md). Session-only: nothing here is saved, and the
    // experiment starts off with every game start.
    internal static partial class TuningWindow
    {
        private static void DrawFlying()
        {
            Mount.Instance i = Mount.Ridden;
            Header("Flying (experiment)");
            Help("The ridden mount, its rider and its cart lift off the ground and fly at a height. From the clear height up (free flight) the keeper's body leaves the game's physics: over water, fences and roofs; it lands only where he can stand. Nothing here is saved.");
            if (OnOffRow("Flying experiment", Flight.Enabled, tip: "On for everyone since 2026-10-25 (API 0.38.0); this switch is the session's, dev only. Off: nothing changes. Switching it off while airborne drops to the ground at once.")) Flight.Enabled = !Flight.Enabled;
            if (OnOffRow("Any mount flies (off: those whose [Mount] says flies)", Flight.AnyMount)) Flight.AnyMount = !Flight.AnyMount;

            string why;
            bool can = Flight.CanFly(i, out why);
            GUILayout.Label($"Now: {Flight.Now}, {Flight.HeightPx:0} px up{(Flight.Free ? ", flying free (the body is the mod's)" : "")}. {(Flight.Why.Length > 0 ? Flight.Why + "." : "")}", label);
            if (Flight.Free) GUILayout.Label($"Last spot he could land on: {Flight.LastSafe:F1}", small);
            if (!can && Flight.Now == Flight.Phase.Ground) GUILayout.Label("Can't take off: " + why + ".", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = can || Flight.Now != Flight.Phase.Ground;
            if (GUILayout.Button(Flight.Now == Flight.Phase.Ground || Flight.Now == Flight.Phase.Landing ? "Take off" : "Land", Wd(110f))) Flight.TakeOffOrLand(i);
            GUI.enabled = Flight.Now == Flight.Phase.Ground && i != null && i.IsMounted && Flight.Active;
            if (GUILayout.Button("Jump", Wd(110f))) Flight.Jump(i);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Header("Free flight (over everything)");
            if (OnOffRow("Free flight from the clear height", Flight.FreeFlight, tip: "On: from the clear height up, the keeper's body is moved by the mod, through fences and over water, roofs and cliffs; down only where he can stand. Off: the lift only (the keeper walks underneath).")) Flight.FreeFlight = !Flight.FreeFlight;
            if (OnOffRow("Bump into what's taller than the flight", Flight.BumpTall, tip: "On: buildings, trees and walls whose collider or picture reaches above the flight's feet stop it (it slides along them); fences, rocks and water below pass under. Off: through everything.")) Flight.BumpTall = !Flight.BumpTall;
            Flight.ClearancePx = SessionFloatRow("Clearance under the flight (px)", Flight.ClearancePx, 0f, 32f, "A top this close under the flight's feet still stops it.");
            if (Flight.Free && Flight.Blocker != null) GUILayout.Label("Bumping into: " + Flight.Blocker, small);
            Flight.ClearPx = SessionFloatRow("Clear height (px): free above it", Flight.ClearPx, 4f, 64f, "Below it the ground's physics hold him again; coming down, the mount holds this height until the ground under him can take him.");
            Flight.FloorRisePxPerS = SessionFloatRow("Floor rising: up to it (px per second)", Flight.FloorRisePxPerS, 20f, 2000f, "When the floor rises (a higher level under him, a treetop), the flight climbs to it at this speed; it is never drawn below what is there, only inside the clear height over it.");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Survey what's around (12)", "Every collider within 12 of the keeper, with its top, the pictures beside it and its place on a screenshot: BepInEx/GK2-flight-survey.csv and .png (how tall the game's things are, for the bumping)."), Wd(200f))) FlightSurvey.Run(12f);
            GUILayout.EndHorizontal();
            if (OnOffRow("Per-frame log lines ([flight-probe])", Flight.ProbeOn, tip: "Where the mount, the keeper and the camera are every frame while off the ground: for measuring a shake or a jump. Many lines.")) Flight.ProbeOn = !Flight.ProbeOn;
            if (OnOffRow("Front layer readout ([occlusion] lines)", Looks.FrontReadout.On, tip: "While flying, a few times a second: where the front picture (the near wing) and the rider's sprites are in the mount's picture, both facings (docs/front-layer.md section 9). Off by default.")) Looks.FrontReadout.On = !Looks.FrontReadout.On;
            if (GUILayout.Button(Tip("Survey the whole map (loaded scenes)", "Every terrain chunk, baked prop, world object, ground, water and zone collider of the loaded scenes: BepInEx/GK2-map-survey-HHmmss/ (tools/flying/map_survey.py draws it)."), Wd(260f))) MapSurvey.Run();
            if (MapSurvey.LastResult.Length > 0) GUILayout.Label(MapSurvey.LastResult, small);

            Header("The drawn ground (step 1: the map and its checks; the flight doesn't use it yet)");
            Help("The game's ground is flat levels drawn tile by tile; the mod reads each terrain mesh's tiles just before the game frees them. The check compares the drawn level with the walkable ground wherever both exist (they must agree) and lists the levels under the keeper (hover over a rock table: its top level shows as drawn).");
            GUILayout.Label("Under the keeper: " + DrawnGround.Here(), small);
            if (GUILayout.Button(Tip("Check the drawn ground (radius 30)", "Rays down on a 0.75 grid around the keeper: where one meets walkable ground, the drawn level there must be at its height. Written to the log ([drawn-ground] check)."), Wd(260f))) DrawnGround.Check();
            if (DrawnGround.CheckResult.Length > 0) GUILayout.Label(DrawnGround.CheckResult, small);
            if (GUILayout.Button(Tip("Cover check (radius 30)", "Where walkable ground has a higher level drawn over it (a rock table over the ground it hides, a rim's edge over its cliff's foot), and how many tiles in from that level's edge. Written to the log ([drawn-ground] cover check)."), Wd(260f))) DrawnGround.CoverCheck();
            if (DrawnGround.CoverResult.Length > 0) GUILayout.Label(DrawnGround.CoverResult, small);
            if (OnOffRow("The flight uses the drawn ground (step 2)", Flight.UseDrawnGround, tip: "On: the floor stays above the drawn ground too, a higher level ahead stops the flight like a wall, and no landing where a higher level is drawn over the spot. Off: walkable ground and objects only, as before.")) Flight.UseDrawnGround = !Flight.UseDrawnGround;
            Flight.CoverEdgeTiles = Mathf.RoundToInt(SessionFloatRow("Landing under a level's edge tiles allowed (tiles)", Flight.CoverEdgeTiles, 0f, 3f, "A higher level's outermost tiles can hang over the foot of its cliff: a landing under that many edge tiles is still allowed (0: none). Deeper under a level: never."));
            if (FlightSurvey.LastResult.Length > 0) GUILayout.Label(FlightSurvey.LastResult, small);

            Header("Heights and speeds (art pixels)");
            Flight.CruisePx = SessionFloatRow("Height a take-off climbs to", Flight.CruisePx, 4f, 320f, "Art pixels above where the ground puts the mount.");
            Flight.MaxPx = SessionFloatRow("Highest (the climb key stops here)", Flight.MaxPx, 8f, 400f);
            Flight.ClimbPxPerS = SessionFloatRow("Climb and descend speed (px per second)", Flight.ClimbPxPerS, 5f, 200f);
            Flight.BobPx = SessionFloatRow("Hover: up and down (px)", Flight.BobPx, 0f, 8f, "The mount rises and sinks this much while airborne, eased in near the ground.");
            Flight.BobPeriodS = SessionFloatRow("Hover: one up and down (seconds)", Flight.BobPeriodS, 0.2f, 4f);
            Flight.CameraFollow = SessionFloatRow("Camera rises with the height (0 none, 1 all)", Flight.CameraFollow, 0f, 1f, "The view goes up with the mount, so it stays in the middle of the screen. The hover's bob is left out.");

            if (OnOffRow("Flying frames (the skin's own, when it has them)", Flight.FlyFrames, tip: "The dragon's spread wings while airborne, its take-off and landing poses. Off: its walk and idle frames in the air.")) Flight.FlyFrames = !Flight.FlyFrames;
            Flight.WingBeatS = SessionFloatRow("One wing beat (seconds)", Flight.WingBeatS, 0.2f, 2f, "The eight flying frames once round.");
            Flight.BubbleS = SessionFloatRow("Bubble time (seconds)", Flight.BubbleS, 0.5f, 6f, "How long \"Nowhere to land here.\" and \"Land first.\" stay over the rider (the game's own time for them was about 5 s).");
            Flight.BeatBobPx = SessionFloatRow("Rise on each downstroke (px)", Flight.BeatBobPx, 0f, 8f, "With the flying frames: the body and the rider rise as the wings come down, sink on the upstroke (in place of the hover).");
            GUILayout.BeginHorizontal();
            if (OnOffRow("Glide now and then, flying along", Flight.Glide, tip: "Flying in one direction for a while, the wings sometimes stop and stay open (the skin's glide frames). Stopping, turning or climbing ends it.")) Flight.Glide = !Flight.Glide;
            GUI.enabled = Flight.Now == Flight.Phase.Airborne && Flight.Glide && !Flight.Gliding;
            if (GUILayout.Button(Flight.Gliding ? "Gliding" : "Glide now", Wd(110f))) Flight.GlideNow();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            Flight.GlideAfterS = SessionFloatRow("Glide: flying along first (seconds)", Flight.GlideAfterS, 0f, 10f, "How long he beats along in one direction before a glide may start, and again after each glide.");
            Flight.GlideChance = SessionFloatRow("Glide: chance each beat", Flight.GlideChance, 0f, 1f, "After that, the chance on each wing beat (1: always the first beat).");
            Flight.GlideMinS = SessionFloatRow("Glide: shortest (seconds)", Flight.GlideMinS, 0.3f, 8f);
            Flight.GlideMaxS = SessionFloatRow("Glide: longest (seconds)", Flight.GlideMaxS, 0.3f, 8f);
            Flight.GlideLoopS = SessionFloatRow("Glide: its slow loop (seconds)", Flight.GlideLoopS, 0.4f, 4f, "The eight glide frames once round: the wings flexing, the tail's wave and the legs.");
            Flight.GlideBobPx = SessionFloatRow("Glide: the body's swell (px)", Flight.GlideBobPx, 0f, 4f, "Gliding, the body rises and sinks this much with the wings' flex.");
            if (OnOffRow("Climbing and diving shown (the pitched beats)", Flight.PitchFrames, tip: "Climbing, the neck arches up and the tail hangs low; descending (or a landing coming down fast), the neck reaches down and the tail rises. Off: level.")) Flight.PitchFrames = !Flight.PitchFrames;
            Flight.PitchPerS = SessionFloatRow("Pitch: easing in and out (per second)", Flight.PitchPerS, 0.5f, 10f, "How fast the dragon tilts into a climb or a dive and back: 3 = a full tilt in a third of a second.");

            Header("Take-off (a launch, not a lift)");
            Flight.CrouchPx = SessionFloatRow("Crouch before the launch (px)", Flight.CrouchPx, 0f, 8f);
            Flight.CrouchS = SessionFloatRow("Crouch time (seconds)", Flight.CrouchS, 0f, 0.4f);
            Flight.TakeoffTimeS = SessionFloatRow("Launch: time to the height (seconds)", Flight.TakeoffTimeS, 0.15f, 2f, "A spring: fast at the start, slowing into the height.");
            Flight.TakeoffBounce = SessionFloatRow("Launch: overshoot (share of the height)", Flight.TakeoffBounce, 0f, 0.5f, "How far past the height the launch carries before it settles.");
            Flight.Beats = Mathf.RoundToInt(SessionFloatRow("Wing beats in the launch", Flight.Beats, 0f, 8f, "Each beat a surge upwards."));
            Flight.BeatKickPxPerS = SessionFloatRow("Wing beat: kick (px per second)", Flight.BeatKickPxPerS, 0f, 200f);
            Flight.BeatS = SessionFloatRow("Wing beat: every (seconds)", Flight.BeatS, 0.05f, 0.6f);

            Header("Landing (a dive, a flare, a thud)");
            Flight.DiveSpeedPxPerS = SessionFloatRow("Dive: fastest (px per second)", Flight.DiveSpeedPxPerS, 20f, 600f);
            Flight.DiveAccelPxPerS2 = SessionFloatRow("Dive: speeding up (px per second, per second)", Flight.DiveAccelPxPerS2, 50f, 3000f);
            Flight.FlarePx = SessionFloatRow("Flare from (px above the ground)", Flight.FlarePx, 0f, 80f, "From here down it brakes to the touch speed.");
            Flight.TouchSpeedPxPerS = SessionFloatRow("Touching down at (px per second)", Flight.TouchSpeedPxPerS, 5f, 200f);
            Flight.ThudPx = SessionFloatRow("Thud: squat on touching down (px)", Flight.ThudPx, 0f, 6f);

            Header("Jump (the mount's, riding plain: a crouch, a fast rise, a hang, a heavier fall, a squash)");
            if (OnOffRow("Every mount jumps, also those that don't (off: as their [Mount] says)", Flight.JumpAnyMount)) Flight.JumpAnyMount = !Flight.JumpAnyMount;
            Flight.JumpPx = SessionFloatRow("Jump height, the key held (px)", Flight.JumpPx, 2f, 60f);
            Flight.JumpTapPx = SessionFloatRow("Jump height, the key tapped (px)", Flight.JumpTapPx, 1f, 60f, "Let go of the key on the way up and the rise brakes hard, down to this at the least.");
            Flight.JumpHoldMinS = SessionFloatRow("Jump: a press counts as at least (seconds)", Flight.JumpHoldMinS, 0f, 0.3f, "A shorter press jumps as if held this long: an ordinary press is a jump, a flick a hop.");
            if (OnOffRow("Jump over things (the body leaves the ground)", Flight.JumpOver, tip: "On: the keeper's body passes over what is lower than the jump and lands where a flight could; a taller thing stops it. Off: the jump is drawn only.")) Flight.JumpOver = !Flight.JumpOver;
            Flight.JumpPerchS = SessionFloatRow("Jump: on top of something, at most (seconds)", Flight.JumpPerchS, 0f, 2f, "Come down on a wall's top or a rock: ride off it within this, or he goes back to where he jumped from.");
            Flight.JumpTimeS = SessionFloatRow("Jump time in the air, held (seconds)", Flight.JumpTimeS, 0.15f, 1.5f);
            Flight.JumpRiseShare = SessionFloatRow("Jump: the rise's share of that time", Flight.JumpRiseShare, 0.2f, 0.8f, "0.5: as fast down as up (floaty). Above 0.5: a slower rise and a heavier, quicker fall (the platformer feel).");
            Flight.JumpHangS = SessionFloatRow("Jump: held at the top (seconds)", Flight.JumpHangS, 0f, 0.2f);
            Flight.JumpCrouchPx = SessionFloatRow("Jump: crouch before (px)", Flight.JumpCrouchPx, 0f, 6f);
            Flight.JumpCrouchS = SessionFloatRow("Jump: crouch time (seconds)", Flight.JumpCrouchS, 0f, 0.2f);
            Flight.JumpSquashPx = SessionFloatRow("Jump: squash on landing (px)", Flight.JumpSquashPx, 0f, 6f);

            Header("Keys (click to change)");
            Help("Read only while riding with the experiment on, never while the Mounts panel or a text box has the keys, the controls are taken or the game is paused.");
            GUILayout.Label($"Jump: {Flight.JumpKeyNow}, fly: {Flight.FlyKeyNow} (the Behaviour keys JumpKey and FlyKey: the API cfg, or Saddle Up's Keys rows). " +
                (Flight.SameKey(Flight.JumpKeyNow, Flight.FlyKeyNow) ? "One key: a press jumps, a second press in the air takes off a mount that flies, a press while flying lands." : "Two keys: the jump key jumps, the fly key takes off and lands."), small);
            GUILayout.Label($"Climb (hold): {Flight.UpKey}, descend (hold; at the ground: landed): {Flight.DownKey} (the Behaviour keys ClimbKey and DescendKey: the API cfg, or Saddle Up's Keys rows).", small);
        }
    }
}
