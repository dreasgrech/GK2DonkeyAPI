using UnityEngine;

namespace GK2DonkeyAPI
{
    // The rider against his mount, as drawn (dev, off by default; the Flying tab's "[rider] lines"; docs/rider-motion.md
    // "In flight"). The user, 2026-11-07: flying a winged mount, the keeper rocks up and down as he does walking, where a
    // flying mount should carry him steady. Two things to measure before a fix:
    //   - what moves him: the walk bob reads the game's own donkey sprite under the skin's picture (RiderMotion.cs), and
    //     the game keeps that walking while a direction is held, in the air too (FreePlayerState -> MoveByDirection ->
    //     charState Walk, mirrored to the mount);
    //   - whether his seat follows the mount in the same frame: the seat is worked out in the keeper's LateUpdate, from
    //     the mount's picture, and the mount is placed (with the flight's lift) in the plugin's LateUpdate; Unity doesn't
    //     fix which of the two runs first. Seated before the mount moved, he would trail every lift change by a frame.
    // Per frame (a budget of lines each on the ground and in the air), at the camera's pre-cull, when everything is where
    // it is drawn: the phase, the picture shown and the game's sprite under it, the keeper's state, the walk bob, the
    // seat, the lift, which LateUpdate ran first, and his hip's height over the seat row in art px ("gap"). The gap less
    // the bob is what nothing intends: steady at the seat adjustment, or he moved on the mount. Every 2 s a summary.
    internal static class RiderReadout
    {
        private static bool on;
        internal static bool On
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                airLines = 300;
                groundLines = 120;
                Reset();
            }
        }

        private static int airLines, groundLines;
        private static float nextSummary;
        private static int frames, airFrames, placedFirst, seatedFirst, noSeat, airBobbing;
        private static float airMin, airMax, groundMin, groundMax;

        private static void Reset()
        {
            frames = airFrames = placedFirst = seatedFirst = noSeat = airBobbing = 0;
            airMin = groundMin = float.MaxValue;
            airMax = groundMax = float.MinValue;
            nextSummary = Time.unscaledTime + 2f;
        }

        // One frame, from the ridden mount at the camera's pre-cull (Mount.Instance.ReadoutDrawn).
        internal static void Frame(string phase, bool air, string shown, string under, string state, int walkFrame, float bob, float seatPx, float liftPx, bool seated, bool placedBeforeSeat, float gap)
        {
            frames++;
            float off = gap - bob;
            if (!seated) noSeat++;
            else if (placedBeforeSeat) placedFirst++;
            else seatedFirst++;
            if (air)
            {
                airFrames++;
                if (Mathf.Abs(bob) > 0.001f) airBobbing++;
                airMin = Mathf.Min(airMin, off);
                airMax = Mathf.Max(airMax, off);
            }
            else
            {
                groundMin = Mathf.Min(groundMin, off);
                groundMax = Mathf.Max(groundMax, off);
            }
            if (air ? airLines-- > 0 : groundLines-- > 0)
            {
                Plugin.Log.LogInfo($"[rider] f{Time.frameCount} {phase} shown {shown} under {under} {state} walk {walkFrame} bob {bob:+0.00;-0.00} seat {seatPx:0.00} lift {liftPx:0} {(seated ? (placedBeforeSeat ? "placed>seat" : "SEAT>placed") : "no seat")} gap {gap:+0.00;-0.00} gap-bob {off:+0.00;-0.00}");
            }
            if (Time.unscaledTime >= nextSummary)
            {
                Plugin.Log.LogInfo($"[rider] summary {frames} frames ({airFrames} in the air): the mount placed before the seat {placedFirst}, the seat first {seatedFirst}, no seat {noSeat}; " +
                    $"in the air the walk bob on in {airBobbing}, gap-bob {(airFrames > 0 ? $"{airMin:+0.00;-0.00}..{airMax:+0.00;-0.00}" : "-")}; " +
                    $"on the ground gap-bob {(frames > airFrames ? $"{groundMin:+0.00;-0.00}..{groundMax:+0.00;-0.00}" : "-")}");
                Reset();
            }
        }
    }
}
