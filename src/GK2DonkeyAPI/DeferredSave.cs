using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A value set from the journal's or the options window's controls, written to its cfg file once the changing has
    // paused instead of inside the set. BepInEx writes the whole file on every change when the mod saves on set
    // (SaveOnConfigSet, its default): 3.5 to 4.7 ms a write measured for the mount pick (MountSettingsPage.SelectMount
    // already waits for the same reason), and a slider dragged across made one write per step, 13 to 16 in a row
    // (2026-11-09, docs/performance.md). The value itself takes at once (its SettingChanged runs as before); only the
    // file waits: written SaveDelay after the last change, when a window closes, and when the game quits.
    internal static class DeferredSave
    {
        private const float SaveDelay = 1.5f;
        private static readonly Dictionary<ConfigFile, float> due = new Dictionary<ConfigFile, float>();
        private static readonly List<ConfigFile> ready = new List<ConfigFile>();

        // Sets the entry; a file that saves on set is written later, one that doesn't keeps its own way (not written).
        internal static void Set(ConfigEntryBase e, object value)
        {
            ConfigFile f = e.ConfigFile;
            bool save = f != null && f.SaveOnConfigSet;
            if (!save)
            {
                e.BoxedValue = value;
                return;
            }
            try
            {
                f.SaveOnConfigSet = false;
                e.BoxedValue = value;
            }
            finally
            {
                f.SaveOnConfigSet = true;
            }
            due[f] = Time.unscaledTime + SaveDelay;
        }

        // Every frame (MountRegistry.Tick): the files whose changing has paused.
        internal static void Tick()
        {
            if (due.Count == 0) return;
            float now = Time.unscaledTime;
            ready.Clear();
            foreach (KeyValuePair<ConfigFile, float> kv in due) if (now >= kv.Value) ready.Add(kv.Key);
            foreach (ConfigFile f in ready) Write(f);
        }

        // A window closed: everything waiting written half a second on, in a frame of its own (written in the closing
        // frame, the pick's save made it a 21 to 24 ms frame; the trace of 2026-10-25 15:00).
        internal static void Soon()
        {
            if (due.Count == 0) return;
            float at = Time.unscaledTime + 0.5f;
            ready.Clear();
            foreach (KeyValuePair<ConfigFile, float> kv in due) if (kv.Value > at) ready.Add(kv.Key);
            foreach (ConfigFile f in ready) due[f] = at;
        }

        // The game quitting: everything waiting, now.
        internal static void FlushAll()
        {
            if (due.Count == 0) return;
            ready.Clear();
            foreach (KeyValuePair<ConfigFile, float> kv in due) ready.Add(kv.Key);
            foreach (ConfigFile f in ready) Write(f);
        }

        private static void Write(ConfigFile f)
        {
            due.Remove(f);
            try
            {
                f.Save();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {System.IO.Path.GetFileName(f.ConfigFilePath)} could not be written ({e.Message}); the changed values hold for this session.");
            }
        }
    }
}
