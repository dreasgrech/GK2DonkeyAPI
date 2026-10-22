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
            Help("The ridden mount, its rider and its cart lift off the ground and fly at a height. Step 1: the picture only. The keeper still walks underneath, so the flight goes where walking goes (no water, no cliffs yet). Nothing here is saved.");
            if (OnOffRow("Flying experiment", Flight.Enabled, tip: "On by default on this machine (the dev tools unlocked), never for players. Off: nothing changes. Switching it off while airborne drops to the ground at once.")) Flight.Enabled = !Flight.Enabled;
            if (OnOffRow("Any mount flies (off: the dragon only)", Flight.AnyMount)) Flight.AnyMount = !Flight.AnyMount;

            string why;
            bool can = Flight.CanFly(i, out why);
            GUILayout.Label($"Now: {Flight.Now}, {Flight.HeightPx:0} px up. {(Flight.Why.Length > 0 ? Flight.Why + "." : "")}", label);
            if (!can && Flight.Now == Flight.Phase.Ground) GUILayout.Label("Can't take off: " + why + ".", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = can || Flight.Now != Flight.Phase.Ground;
            if (GUILayout.Button(Flight.Now == Flight.Phase.Ground || Flight.Now == Flight.Phase.Landing ? "Take off" : "Land", Wd(110f))) Flight.TakeOffOrLand(i);
            GUI.enabled = Flight.Now == Flight.Phase.Ground && i != null && i.IsMounted && Flight.Active;
            if (GUILayout.Button("Jump", Wd(110f))) Flight.Jump(i);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Header("Heights and speeds (art pixels)");
            Flight.CruisePx = SessionFloatRow("Height a take-off climbs to", Flight.CruisePx, 4f, 200f, "Art pixels above where the ground puts the mount.");
            Flight.MaxPx = SessionFloatRow("Highest (the climb key stops here)", Flight.MaxPx, 8f, 400f);
            Flight.RisePxPerS = SessionFloatRow("Take-off speed (px per second)", Flight.RisePxPerS, 5f, 200f);
            Flight.ClimbPxPerS = SessionFloatRow("Climb and descend speed (px per second)", Flight.ClimbPxPerS, 5f, 200f);
            Flight.LandPxPerS = SessionFloatRow("Landing speed (px per second)", Flight.LandPxPerS, 5f, 200f);
            Flight.BobPx = SessionFloatRow("Hover: up and down (px)", Flight.BobPx, 0f, 8f, "The mount rises and sinks this much while airborne, eased in near the ground.");
            Flight.BobPeriodS = SessionFloatRow("Hover: one up and down (seconds)", Flight.BobPeriodS, 0.2f, 4f);
            Flight.CameraFollow = SessionFloatRow("Camera rises with the height (0 none, 1 all)", Flight.CameraFollow, 0f, 1f, "The view goes up with the mount, so it stays in the middle of the screen. The hover's bob is left out.");

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
