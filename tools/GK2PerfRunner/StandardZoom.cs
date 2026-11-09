using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2PerfRunner
{
    // The suites at the game's own zoom (a copy of GK2DonkeyAPI.IntegrationTests/StandardZoom.cs; 2026-11-09, docs/performance.md): Grave Zoom (another author's mod, the
    // dev machine has it) shrinks the camera's view, and the user tests at whatever zoom they last played at (250% that
    // day). Zoomed in, fewer mounts are on screen, so fewer are fully active (Tiers.cs: on screen or within 15), the
    // babies catch up out of sight more often and the game draws less: the costs read lower than at 100%, which most
    // players have. The measurement scenarios set it to 100% for their run, read whatever it was first, and put that back
    // after; without Grave Zoom nothing is touched. Set without writing its cfg (put back with the write, so the file
    // ends as it was). The functional scenarios stay at the player's zoom: several place things at distances meant to be
    // off screen there.
    internal static class StandardZoom
    {
        private const string Guid = "com.gravezoom.mod";
        private static ConfigEntry<float> entry;
        private static bool looked, applied;
        private static float was;

        private static ConfigEntry<float> Entry()
        {
            if (looked) return entry;
            looked = true;
            try
            {
                if (Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info.Instance != null)
                {
                    info.Instance.Config.TryGetEntry("Zoom", "ZoomFactor", out entry);
                }
            }
            catch
            {
                entry = null;
            }
            return entry;
        }

        // What the zoom is now, for the measurement lines.
        internal static string Now
        {
            get
            {
                ConfigEntry<float> e = Entry();
                return e == null ? "the game's own (no Grave Zoom)" : $"{e.Value:0}% (Grave Zoom)";
            }
        }

        // 100% for a measurement; returns what was done, for its first line. Give the camera a moment after (yield).
        internal static string Begin()
        {
            ConfigEntry<float> e = Entry();
            if (e == null) return "the zoom: the game's own (no Grave Zoom installed)";
            if (applied) return $"the zoom: {e.Value:0}% (set already)";
            was = e.Value;
            applied = true;
            if (Mathf.Approximately(was, 100f)) return "the zoom: Grave Zoom at 100% already";
            Set(e, 100f, save: false);
            return $"the zoom: Grave Zoom from {was:0}% to 100% for this measurement (put back after)";
        }

        internal static void End()
        {
            if (!applied) return;
            applied = false;
            ConfigEntry<float> e = Entry();
            if (e != null && !Mathf.Approximately(e.Value, was)) Set(e, was, save: true);
        }

        private static void Set(ConfigEntry<float> e, float value, bool save)
        {
            ConfigFile f = e.ConfigFile;
            bool saveWas = f.SaveOnConfigSet;
            try
            {
                f.SaveOnConfigSet = save && saveWas;
                e.Value = value; // its SettingChanged applies it to the camera (Grave Zoom's own handler)
            }
            finally
            {
                f.SaveOnConfigSet = saveWas;
            }
        }
    }
}
