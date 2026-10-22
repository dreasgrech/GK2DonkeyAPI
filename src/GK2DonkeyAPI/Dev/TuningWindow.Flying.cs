using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Flying tab: the flying experiment (Flight.cs, docs/flying.md). Session-only: nothing here is saved, and the
    // experiment starts off with every game start.
    internal static partial class TuningWindow
    {
        // the keys a row cycles through (none of them the game's walking keys)
        private static readonly KeyCode[] FlightKeyChoices =
        {
            KeyCode.Space, KeyCode.R, KeyCode.F, KeyCode.J, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V,
            KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Keypad8, KeyCode.Keypad2, KeyCode.Keypad5,
        };

        private static void DrawFlying()
        {
            Mount.Instance i = Mount.Ridden;
            Header("Flying (experiment)");
            Help("The ridden mount, its rider and its cart lift off the ground and fly at a height. From the clear height up (free flight) the keeper's body leaves the game's physics: over water, fences and roofs; it lands only where he can stand. Nothing here is saved.");
            if (OnOffRow("Flying experiment", Flight.Enabled, tip: "On by default on this machine (the dev tools unlocked), never for players. Off: nothing changes. Switching it off while airborne drops to the ground at once.")) Flight.Enabled = !Flight.Enabled;
            if (OnOffRow("Any mount flies (off: the dragon only)", Flight.AnyMount)) Flight.AnyMount = !Flight.AnyMount;

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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Survey what's around (12)", "Every collider within 12 of the keeper, with its top, the pictures beside it and its place on a screenshot: BepInEx/GK2-flight-survey.csv and .png (how tall the game's things are, for the bumping)."), Wd(200f))) FlightSurvey.Run(12f);
            GUILayout.EndHorizontal();
            if (OnOffRow("Per-frame log lines ([flight-probe])", Flight.ProbeOn, tip: "Where the mount, the keeper and the camera are every frame while off the ground: for measuring a shake or a jump. Many lines.")) Flight.ProbeOn = !Flight.ProbeOn;
            if (FlightSurvey.LastResult.Length > 0) GUILayout.Label(FlightSurvey.LastResult, small);

            Header("Heights and speeds (art pixels)");
            Flight.CruisePx = SessionFloatRow("Height a take-off climbs to", Flight.CruisePx, 4f, 200f, "Art pixels above where the ground puts the mount.");
            Flight.MaxPx = SessionFloatRow("Highest (the climb key stops here)", Flight.MaxPx, 8f, 400f);
            Flight.ClimbPxPerS = SessionFloatRow("Climb and descend speed (px per second)", Flight.ClimbPxPerS, 5f, 200f);
            Flight.BobPx = SessionFloatRow("Hover: up and down (px)", Flight.BobPx, 0f, 8f, "The mount rises and sinks this much while airborne, eased in near the ground.");
            Flight.BobPeriodS = SessionFloatRow("Hover: one up and down (seconds)", Flight.BobPeriodS, 0.2f, 4f);
            Flight.CameraFollow = SessionFloatRow("Camera rises with the height (0 none, 1 all)", Flight.CameraFollow, 0f, 1f, "The view goes up with the mount, so it stays in the middle of the screen. The hover's bob is left out.");

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

            Header("Jump (the same lift on an arc)");
            if (OnOffRow("Every mount jumps (off: the flying ones only)", Flight.JumpAnyMount)) Flight.JumpAnyMount = !Flight.JumpAnyMount;
            Flight.JumpPx = SessionFloatRow("Jump height (px)", Flight.JumpPx, 2f, 60f);
            Flight.JumpTimeS = SessionFloatRow("Jump time, up and down (seconds)", Flight.JumpTimeS, 0.15f, 1.5f);

            Header("Keys (click to change)");
            Help("Read only while riding with the experiment on, never while the Mounts panel or a text box has the keys.");
            Flight.TakeOffKey = FlightKeyRow("Take off / land", Flight.TakeOffKey);
            Flight.UpKey = FlightKeyRow("Climb (hold)", Flight.UpKey);
            Flight.DownKey = FlightKeyRow("Descend (hold; at the ground: landed)", Flight.DownKey);
            Flight.JumpKey = FlightKeyRow("Jump", Flight.JumpKey);
        }

        private static KeyCode FlightKeyRow(string name, KeyCode key)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, label, Wd(LabelW));
            if (GUILayout.Button(key.ToString(), Wd(110f)))
            {
                int at = System.Array.IndexOf(FlightKeyChoices, key);
                key = FlightKeyChoices[(at + (Event.current.button == 1 ? FlightKeyChoices.Length - 1 : 1)) % FlightKeyChoices.Length];
            }
            GUILayout.EndHorizontal();
            return key;
        }
    }
}
