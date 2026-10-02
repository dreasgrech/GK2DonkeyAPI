using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The keys of the mods' settings pages that do something when pressed (MountSettingsPage.Key with pressed:), and
    // the mods that use the API: polled from the plugin's LateUpdate, before the mounts' update, while the API is on.
    // - A key counts when its main key went down this frame with the shortcut's own modifiers held; other keys held
    //   don't matter. Not KeyboardShortcut.IsDown(): that also demands that no other key is held, so it never fired
    //   while the player walks (W/A/S/D down), which is exactly when mounting matters (found in game 2026-09-30).
    // - A mod that is unloaded (its plugin destroyed, e.g. by a script reloader) has its tuning cleared and its keys
    //   dropped: mods don't need an OnDestroy of their own for it.
    internal static class KeyActions
    {
        private sealed class Entry
        {
            internal string Owner;
            internal ConfigEntry<KeyboardShortcut> Key;
            internal Action Pressed;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        // Owners whose plugin was seen alive (true) or not yet (false).
        private static readonly Dictionary<string, bool> owners = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static float nextOwnerCheck;

        internal static void Add(string owner, ConfigEntry<KeyboardShortcut> key, Action pressed)
        {
            Track(owner);
            entries.Add(new Entry { Owner = owner, Key = key, Pressed = pressed });
        }

        internal static void Track(string owner)
        {
            if (!string.IsNullOrEmpty(owner) && !owners.ContainsKey(owner))
            {
                owners[owner] = false;
            }
        }

        internal static void Poll()
        {
            if (Time.unscaledTime >= nextOwnerCheck)
            {
                nextOwnerCheck = Time.unscaledTime + 1f;
                CheckOwners();
            }
            if (SettingsRows.CapturingKey)
            {
                return; // a key is being set in the game's Settings window
            }
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (!Pressed(e.Key.Value))
                {
                    continue;
                }
                try
                {
                    e.Pressed();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"{e.Owner}: the action of its key {e.Key.Definition.Key} failed. {ex}");
                }
            }
        }

        internal static bool Pressed(KeyboardShortcut k)
        {
            if (k.MainKey == KeyCode.None || !UnityInput.Current.GetKeyDown(k.MainKey))
            {
                return false;
            }
            foreach (KeyCode m in k.Modifiers)
            {
                if (!UnityInput.Current.GetKey(m))
                {
                    return false;
                }
            }
            return true;
        }

        private static readonly List<string> gone = new List<string>();

        private static void CheckOwners()
        {
            gone.Clear();
            var seen = new List<string>();
            foreach (var kv in owners)
            {
                BaseUnityPlugin inst = Chainloader.PluginInfos.TryGetValue(kv.Key, out PluginInfo info) ? info.Instance : null;
                bool alive = inst != null && inst; // Unity's null: a destroyed plugin
                if (alive && !kv.Value)
                {
                    seen.Add(kv.Key);
                }
                else if (!alive && kv.Value)
                {
                    gone.Add(kv.Key);
                }
            }
            foreach (string id in seen)
            {
                owners[id] = true;
            }
            foreach (string id in gone)
            {
                owners.Remove(id);
                entries.RemoveAll(e => e.Owner == id);
                try
                {
                    Api.Mounts.ClearTuning(id);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"{id} was unloaded; clearing its tuning failed: {e.Message}");
                }
                Plugin.Log.LogInfo($"{id} was unloaded: its tuning is cleared and its keys dropped.");
            }
        }
    }
}
