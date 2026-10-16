using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GK2PerfRunner
{
    // Dev-only: scripted profiling runs with Performance Hog (docs/performance-hog.md). A small window (Shift+F8; F9 and
    // Ctrl+F9 are Performance Hog's, Shift+F9 the showcase director's). The loop (LoopPath.cs), recorded once by hand
    // and driven by the runner on foot, riding or with the cart; the suite (Suite.cs) runs every profiling run on it and
    // exports Performance Hog's reports. Never touches the save: it doesn't sleep or save, and the game saves only on
    // waking from sleep (EnergySystem.StopSleeping).
    // The window is drawn only while open, and Unity's layout pass is off while it is closed (useGUILayout), so a closed
    // runner adds no garbage to the frames it measures.
    [BepInPlugin(Guid, "GK2 Perf Runner", "0.1.0")]
    [BepInDependency("dreas.gk2.donkeyapi")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.perfrunner";

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private bool open, closeNow; // closeNow: closed from Update (not in the middle of a GUI event)
        // The end of a suite or a series (done or stopped): the window opens by itself and a banner across the top of the
        // screen says so until the window key is pressed. Drawn only after the end: nothing is measured then.
        private bool wasRunning, banner;
        private GUIStyle bannerStyle;
        private Rect window = new Rect(40f, 60f, 420f, 10f);
        private GUIStyle rich;
        private Suite.HoldTime hold = Suite.HoldTime.Current;
        private int inARow = 1;
        private static readonly int[] InARow = { 1, 2, 3, 5 };

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftShift), "Opens and closes the runner's window.");
            LoopPath.Init(Logger, new Harmony(Guid));
            useGUILayout = false;
            Logger.LogInfo($"[perfrunner] ready; {toggleKey.Value} opens the window.");
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                open = !open;
                useGUILayout = open;
                banner = false;
            }
            if (closeNow)
            {
                closeNow = false;
                open = false;
                useGUILayout = false;
            }
            LoopPath.Tick();
            Suite.Current?.Tick();
            bool running = Suite.Current != null;
            if (wasRunning && !running)
            {
                open = true;
                useGUILayout = true;
                banner = true;
            }
            wasRunning = running;
        }

        private void OnGUI()
        {
            if (banner)
            {
                if (bannerStyle == null)
                {
                    bannerStyle = new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                    bannerStyle.normal.textColor = Color.white;
                }
                bool stopped = Suite.LastResult.StartsWith("stopped");
                string text = (stopped ? "Perf runner: STOPPED" : "Perf runner: done") + "\n<size=18>" + Suite.LastResult + " (" + toggleKey.Value + " closes this)</size>";
                bannerStyle.richText = true;
                GUI.Box(new Rect(Screen.width * 0.15f, 20f, Screen.width * 0.7f, 110f), text, bannerStyle);
            }
            if (!open)
            {
                return;
            }
            window = GUILayout.Window(0x5D1EC8, window, DrawWindow, "Perf runner (" + toggleKey.Value + ")");
        }

        private void DrawWindow(int id)
        {
            if (rich == null)
            {
                rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            }
            if (LoopPath.Keeper == null)
            {
                GUILayout.Label("Load a game first.");
                GUI.DragWindow();
                return;
            }
            Suite running = Suite.Current;
            if (running != null)
            {
                GUILayout.Label($"<b>Suite running</b>: {running.Progress}\n{running.Status}\n<i>Close this window: drawing it adds to the frames being measured.</i>", rich);
                if (GUILayout.Button("Stop the suite"))
                {
                    running.Abort("Stop pressed");
                }
                GUI.DragWindow();
                return;
            }
            GUILayout.Label("<b>The loop</b>: record it once by walking it; every run drives it.", rich);
            GUILayout.BeginHorizontal();
            if (!LoopPath.Recording && !LoopPath.Driving && GUILayout.Button("Record loop"))
            {
                LoopPath.StartRecording();
            }
            if (LoopPath.Recording && GUILayout.Button("Stop recording"))
            {
                LoopPath.StopRecording();
            }
            if (!LoopPath.Recording && !LoopPath.Driving && LoopPath.HasPath && GUILayout.Button("Drive loop"))
            {
                LoopPath.StartDriving();
            }
            if (LoopPath.Driving && GUILayout.Button("Stop"))
            {
                LoopPath.Stop("Stop pressed");
            }
            GUILayout.EndHorizontal();
            string info = !LoopPath.HasPath ? "no loop yet"
                : $"loop: {LoopPath.Length:F1} units round in {LoopPath.Scene}"
                  + (LoopPath.Driving ? $"; lap {LoopPath.Laps + 1}" + (LoopPath.Laps > 0 ? $", last lap {LoopPath.LastLapTime:F2} s" : "") : "")
                  + (!LoopPath.Driving && LoopPath.HasPath ? $"; you are {LoopPath.DistanceToLoop():F1} from it" : "");
            GUILayout.Label(info + (LoopPath.Status.Length > 0 ? "\n" + LoopPath.Status : ""), rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>The suite</b> (about 12 min, hands off): walk, ride, cart with six test items, idle, hitch, memory. Esc stops it.", rich);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Time of day held at:", GUILayout.Width(130f));
            foreach (Suite.HoldTime h in new[] { Suite.HoldTime.Current, Suite.HoldTime.Noon, Suite.HoldTime.Night })
            {
                if (GUILayout.Toggle(hold == h, " " + h, GUILayout.Width(80f))) hold = h;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Suites in a row:", GUILayout.Width(130f));
            foreach (int n in InARow)
            {
                if (GUILayout.Toggle(inARow == n, " " + n, GUILayout.Width(50f))) inARow = n;
            }
            GUILayout.EndHorizontal();
            if (inARow > 1)
            {
                GUILayout.Label($"About {inARow * 9.5f:F0} min. The first suite warms the caches up; a leak shows as growth in the later ones.", rich);
            }
            string why = Suite.CantStart();
            if (ApiDev.Find() && ApiDev.TuningWindowOn())
            {
                GUILayout.Label("<color=#ffcc66>The Donkey API's dev panel switch is on: its [perf] logging adds to the frames. For player numbers set [Debug] TuningWindow = false (game closed).</color>", rich);
            }
            GUI.enabled = why == null;
            if (GUILayout.Button(inARow > 1 ? $"Start {inARow} suites" : "Start the suite"))
            {
                Suite.Start(Logger, hold, inARow);
                closeNow = true;
            }
            GUI.enabled = true;
            GUILayout.Label((why != null ? "Can't start: " + why : "Ready.") + (Suite.LastResult.Length > 0 ? "\nLast suite: " + Suite.LastResult : ""), rich);
            GUI.DragWindow();
        }
    }
}
