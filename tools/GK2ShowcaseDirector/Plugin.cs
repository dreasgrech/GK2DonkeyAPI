using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using NodeCanvas.Framework;
using NodeCanvas.StateMachines;
using UnityEngine;

namespace GK2ShowcaseDirector
{
    // Dev-only director for the showcase video (docs/showcase-video.md, "Automation"). A small window (F9) that sets the
    // look of a shot without touching the save:
    //   Time of day: EnvironmentEngine.SetTimeOfDayFake, the game's own "show this time" call. It changes the light, the
    //     HUD's sun-and-moon wheel and the town's ambient sounds, and pauses the clock; the save's time and day don't
    //     move, and the game's time-triggered scripts ignore it (Flow_OnTimeOfDayEvent skips fake times).
    //     ResumeTimeOfDayFromData puts the real time back where it was.
    //   Weather: WeatherSystem.SetWeatherState(name, force: true) holds a weather (the game stops rolling new weather
    //     while one is forced). "Back" restores the weather from before the first change, rolling again. A weather the
    //     game holds (a quest or event set it) is left alone, and the quests' event weathers and the state without a
    //     weather aren't offered (entering a state runs its scripts). The weather IS part of the save: a save made while
    //     one is held keeps it.
    //   While a time is held, the few things that read the shown time see it: fishing's day/night fish, the scripts'
    //   "time between" and "time of day" checks, fog and lighting. Nothing is saved from it.
    // Every change is logged with the prefix [director].
    [BepInPlugin(Guid, "GK2 Showcase Director", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.showcasedirector";

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private bool open;
        private Rect window = new Rect(40f, 120f, 360f, 10f);
        private Vector2 weatherScroll;

        private bool timeHeld;
        private float shownTime = 0.5f;
        private List<string> weathers;

        // The weather as it was before the director's first change (always a rolling one: a weather the game holds is
        // never changed), put back by "back". Null while the director holds none.
        private string weatherBefore;

        private static readonly (string Name, float Time)[] Presets =
        {
            ("Dawn", 0.27f), ("Morning", 0.35f), ("Noon", 0.5f), ("Afternoon", 0.62f), ("Dusk", 0.74f), ("Night", 0.9f), ("Midnight", 0f),
        };

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F9), "Opens and closes the director's window.");
            Logger.LogInfo($"[director] ready; {toggleKey.Value} opens the window.");
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                open = !open;
                if (open)
                {
                    weathers = null; // read again: the weather system exists only in a loaded game
                }
            }
            // Back at the menu (or loading another save): what the director remembered belongs to the game that was.
            if (!InGame && (timeHeld || weatherBefore != null))
            {
                timeHeld = false;
                weatherBefore = null;
            }
            // Something of the game's own (sleeping, a cutscene) may have resumed the real time: the window follows.
            if (timeHeld && EnvironmentEngine.Instance != null && !EnvironmentEngine.Instance.IsPaused)
            {
                timeHeld = false;
                Logger.LogInfo("[director] the game resumed the real time itself.");
            }
        }

        private void OnGUI()
        {
            if (!open)
            {
                return;
            }
            window = GUILayout.Window(0x5D1EC7, window, DrawWindow, "Showcase director (" + toggleKey.Value + ")");
        }

        private static bool InGame => EnvironmentEngine.Instance != null && MainGame.Instance != null && MainGame.Instance.GameSave != null;

        private void DrawWindow(int id)
        {
            if (!InGame)
            {
                GUILayout.Label("Load a game first.");
                GUI.DragWindow();
                return;
            }
            DrawTime();
            GUILayout.Space(8f);
            DrawWeather();
            GUILayout.Space(8f);
            // While a time is held, a few game checks see it (fishing, the scripts' time checks, fog and light) and the rest
            // the real time, which stands still: fine for filming, not for playing. The weather is the game's real one,
            // held, and part of the save.
            GUILayout.Label("<b>Film, don't play, while a time is held</b>: no fishing or quest steps; back to the real time before playing on.\n<b>Before saving</b>: back to the weather before, or the held weather stays in the save.", Rich);
            GUI.DragWindow();
        }

        // ------------------------------------------------------------------ time of day

        private void DrawTime()
        {
            EnvironmentEngine env = EnvironmentEngine.Instance;
            GUILayout.Label("<b>Time of day</b>", Rich);
            GUILayout.Label($"Real: {Clock(env.Data.TimeOfDay)}, day {env.Data.CurrentDayNumber}. Shown: {(timeHeld ? Clock(shownTime) + " (held, the clock is paused)" : "the real time")}.");
            float t = GUILayout.HorizontalSlider(timeHeld ? shownTime : env.Data.TimeOfDay, 0f, 0.999f);
            if (Mathf.Abs(t - (timeHeld ? shownTime : env.Data.TimeOfDay)) > 0.0005f)
            {
                ShowTime(t, log: false);
            }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Presets.Length; i++)
            {
                if (i == 4)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(Presets[i].Name))
                {
                    ShowTime(Presets[i].Time, log: true);
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = timeHeld;
            if (GUILayout.Button("Back to the real time"))
            {
                env.ResumeTimeOfDayFromData();
                timeHeld = false;
                Logger.LogInfo($"[director] back to the real time ({Clock(env.Data.TimeOfDay)}).");
            }
            GUI.enabled = true;
        }

        private void ShowTime(float t, bool log)
        {
            EnvironmentEngine.Instance.SetTimeOfDayFake(t);
            if (!timeHeld || log)
            {
                Logger.LogInfo($"[director] showing {Clock(t)} (real time {Clock(EnvironmentEngine.Instance.Data.TimeOfDay)} paused).");
            }
            shownTime = t;
            timeHeld = true;
        }

        // The game's day runs 0..1 from midnight: 0.25 is 6:00, 0.5 noon, 0.75 18:00 (its day is 0.25 to 0.75).
        private static string Clock(float t)
        {
            int minutes = Mathf.RoundToInt(Mathf.Repeat(t, 1f) * 24f * 60f) % (24 * 60);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        // ------------------------------------------------------------------ weather

        private void DrawWeather()
        {
            WeatherData wd = MainGame.Instance.GameSave.weatherData;
            GUILayout.Label("<b>Weather</b>", Rich);
            GUILayout.Label($"Now: {(string.IsNullOrEmpty(wd?.stateName) ? "?" : wd.stateName)}{(wd != null && wd.hasForceState ? " (held)" : " (rolling as normal)")}.");
            if (weathers == null)
            {
                weathers = ReadWeathers();
            }
            // Held by the game (a quest or event set it): left alone. Switching away would run that state's exit script
            // and coming back its enter script again, either of which may push the quest it belongs to.
            bool gameHolds = wd != null && wd.hasForceState && weatherBefore == null;
            if (gameHolds)
            {
                GUILayout.Label("The game holds this weather (a quest or event set it), so the director leaves it alone.");
            }
            GUI.enabled = !gameHolds;
            if (weathers.Count == 0)
            {
                GUILayout.Label("No weather states found (see the log).");
            }
            else
            {
                weatherScroll = GUILayout.BeginScrollView(weatherScroll, GUILayout.Height(Mathf.Min(220f, 26f * Mathf.Ceil(weathers.Count / 2f) + 8f)));
                for (int i = 0; i < weathers.Count; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int k = i; k < Mathf.Min(i + 2, weathers.Count); k++)
                    {
                        if (GUILayout.Button(weathers[k]))
                        {
                            SetWeather(weathers[k]);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUI.enabled = weatherBefore != null;
            if (GUILayout.Button(weatherBefore == null ? "Back to the weather before" : $"Back to the weather before ({weatherBefore})"))
            {
                RestoreWeather();
            }
            GUI.enabled = true;
        }

        private void SetWeather(string name)
        {
            try
            {
                WeatherData wd = MainGame.Instance.GameSave.weatherData;
                if (weatherBefore == null && wd != null && wd.hasForceState)
                {
                    Logger.LogInfo($"[director] the game holds the weather ({wd.stateName}); not changed.");
                    return;
                }
                if (weatherBefore == null && wd != null)
                {
                    weatherBefore = string.IsNullOrEmpty(wd.stateName) ? WeatherSystem.CLEAN_WEATHER_NAME : wd.stateName;
                    Logger.LogInfo($"[director] the weather before: {weatherBefore}.");
                }
                WeatherSystem.Instance.SetWeatherState(name, force: true);
                Logger.LogInfo($"[director] weather held at {name}.");
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[director] setting the weather to {name} failed: {e.Message}");
            }
        }

        // Back to what it was, rolling again from there (as the game's ResetWeatherState leaves it, but to the weather
        // that was, not always clear).
        private void RestoreWeather()
        {
            try
            {
                WeatherSystem.Instance.SetWeatherState(weatherBefore);
                MainGame.Instance.GameSave.weatherData.hasForceState = false;
                Logger.LogInfo($"[director] weather back to {weatherBefore}, rolling again.");
            }
            catch (Exception e)
            {
                Logger.LogWarning("[director] putting the weather back failed: " + e.Message);
            }
            weatherBefore = null;
        }

        // The weather states of the game's weather state machine (FSMWeatherState nodes; their names are the weather
        // components' names, which SetWeatherState looks up). They live in the game's assets, so they are read here.
        private List<string> ReadWeathers()
        {
            var names = new List<string>();
            try
            {
                WeatherSystem ws = WeatherSystem.Instance;
                FSMOwner owner = ws != null ? ws.GetComponent<FSMOwner>() : null;
                FSMState current = owner != null ? owner.GetCurrentState() as FSMState : null;
                if (current == null)
                {
                    Logger.LogWarning("[director] no weather state machine found (is a game loaded?).");
                    return names;
                }
                // Left out: a state without a weather ("NULL Weather", a router: entering it jumps on at once, and without
                // an exit it enters itself again and again), and the quests' event weathers ("Event…"): entering any state
                // runs its own enter script and leaving it its exit script (FSMWeatherState.OnEnter / OnExit), which for
                // the ordinary weathers is what the game's rolling does all day, but for an event weather may push the
                // quest it belongs to.
                var skipped = new List<string>();
                foreach (Node n in current.FSM.allNodes)
                {
                    if (!(n is FSMWeatherState w))
                    {
                        continue;
                    }
                    string name = w.name;
                    bool skip = w.weather == null || name.StartsWith("Event", StringComparison.OrdinalIgnoreCase);
                    List<string> into = skip ? skipped : names;
                    if (!into.Contains(name))
                    {
                        into.Add(name);
                    }
                }
                names.Sort(StringComparer.Ordinal);
                Logger.LogInfo($"[director] {names.Count} weather states offered: {string.Join(", ", names)}; left out (no weather, or a quest event's): {string.Join(", ", skipped)}.");
            }
            catch (Exception e)
            {
                Logger.LogWarning("[director] reading the weather states failed: " + e.Message);
            }
            return names;
        }

        private static GUIStyle rich;
        private static GUIStyle Rich => rich ?? (rich = new GUIStyle(GUI.skin.label) { richText = true });
    }
}
