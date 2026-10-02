using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using TMPro;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Choice rows' labels in GK2 Mod Framework's Mods menu. The framework fills a dropdown with each value's own
    // name (Convert.ToString of the enum: "Game", "Normal") and has no way to give it a label, so the labels the page
    // was given ("The game's", "Normal (x1.75 walking speed)") are written over its options once it has made them,
    // matched by position (the options are the enum's values in order, as the labels are).
    // Found by name, never by the framework's types (tests/FrameworkScan): the row is "Setting_<section>.<key>"
    // (ModsMenuSettingsPage.CreateSettingRow, ConfigSetting.UniqueKey). Anything unexpected (another framework
    // version): the plain names stay, logged once; the settings work the same.
    internal static class MenuLabels
    {
        private const float Every = 0.25f;
        private const string RowPrefix = "Setting_";

        // Unique key ("<section>.<key>") -> the labels, for every Choice row of the pages in the Mods menu.
        private static readonly Dictionary<string, string[]> labels = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static float next;
        private static bool failed, loggedMismatch;

        internal static void Add(MountSettingsPage page)
        {
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Kind == MountSettingsPage.Kind.Choice && r.Entry != null && r.ChoiceLabels != null)
                {
                    labels[r.Section + "." + r.Entry.Definition.Key] = r.ChoiceLabels;
                }
            }
        }

        internal static void Tick()
        {
            if (failed || labels.Count == 0 || Time.unscaledTime < next)
            {
                return;
            }
            next = Time.unscaledTime + Every;
            try
            {
                GameObject w = GameObject.Find("GK2ModsMenuWindow");
                if (w == null)
                {
                    return;
                }
                foreach (TMP_Dropdown d in w.GetComponentsInChildren<TMP_Dropdown>(false))
                {
                    string key = RowKey(d.transform);
                    if (key == null || !labels.TryGetValue(key, out string[] want))
                    {
                        continue;
                    }
                    if (d.options.Count != want.Length)
                    {
                        if (!loggedMismatch)
                        {
                            loggedMismatch = true;
                            Plugin.Log.LogInfo($"Settings: the Mods menu's {key} has {d.options.Count} choices, not {want.Length}; its plain names are kept.");
                        }
                        continue;
                    }
                    bool changed = false;
                    for (int i = 0; i < want.Length; i++)
                    {
                        if (d.options[i].text != want[i])
                        {
                            d.options[i].text = want[i];
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        d.RefreshShownValue();
                    }
                }
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogWarning("Settings: the Mods menu's choice labels are off for this session (" + e.Message + "); it shows the plain names.");
            }
        }

        // The framework row around a dropdown: its name without the prefix.
        private static string RowKey(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.StartsWith(RowPrefix, StringComparison.Ordinal))
                {
                    return t.name.Substring(RowPrefix.Length);
                }
            }
            return null;
        }
    }
}
