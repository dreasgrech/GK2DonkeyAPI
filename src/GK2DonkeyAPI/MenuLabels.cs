using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using TMPro;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Choice rows' labels in GK2 Mod Framework's Mods menu. The framework fills a dropdown with each value's own
    // name (Convert.ToString of the enum: "Game", "Normal") and has no way to give it a label, so the labels the page
    // was given ("Game default", "Normal (x1.75 walking speed)") are written over its options once it has made them,
    // matched by position (the options are the enum's values in order, as the labels are).
    // Found by name, never by the framework's types (tests/FrameworkScan): the row is "Setting_<section>.<key>"
    // (ModsMenuSettingsPage.CreateSettingRow, ConfigSetting.UniqueKey). Anything unexpected (another framework
    // version): the plain names stay, logged once; the settings work the same.
    internal static class MenuLabels
    {
        private const float Every = 0.25f;
        private const string RowPrefix = "Setting_";

        // Unique key ("<section>.<key>") -> the labels, for every Choice and List row of the pages in the Mods menu (a
        // List row's are filled when it is first shown).
        private static readonly Dictionary<string, Func<string[]>> labels = new Dictionary<string, Func<string[]>>(StringComparer.Ordinal);

        // Every row of our pages ("<section>.<key>"): their dropdowns' lists are kept to MaxListRows. The framework makes
        // a list 30 px per choice with no limit, and the 24 palette colours ran off the top of the screen (2026-10-03);
        // the list already scrolls, so a shorter one only scrolls.
        private static readonly HashSet<string> ourRows = new HashSet<string>(StringComparer.Ordinal);
        private const int MaxListRows = 8;
        private const float ItemHeight = 30f;
        private static float next;
        private static bool failed, loggedMismatch;

        internal static void Add(MountSettingsPage page)
        {
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Entry != null)
                {
                    ourRows.Add(r.Section + "." + r.Entry.Definition.Key);
                }
                if (r.Kind == MountSettingsPage.Kind.Choice && r.Entry != null && r.ChoiceLabels != null)
                {
                    string[] fixedLabels = r.ChoiceLabels;
                    labels[r.Section + "." + r.Entry.Definition.Key] = () => fixedLabels;
                }
                else if (r.Kind == MountSettingsPage.Kind.List && r.Entry != null)
                {
                    MountSettingsPage.Row row = r;
                    labels[r.Section + "." + r.Entry.Definition.Key] = () =>
                    {
                        MountSettingsPage.FillList(row);
                        return row.ChoiceLabels;
                    };
                }
            }
        }

        internal static void Tick()
        {
            if (failed || ourRows.Count == 0 || Time.unscaledTime < next)
            {
                return;
            }
            next = Time.unscaledTime + Every;
            try
            {
                GameObject w = ModsWindow.Showing();
                if (w == null)
                {
                    return;
                }
                foreach (TMP_Dropdown d in w.GetComponentsInChildren<TMP_Dropdown>(false))
                {
                    string key = RowKey(d.transform);
                    if (key == null || !ourRows.Contains(key))
                    {
                        continue;
                    }
                    if (d.template != null)
                    {
                        float most = MaxListRows * ItemHeight;
                        Vector2 size = d.template.sizeDelta;
                        if (size.y > most + 0.5f)
                        {
                            d.template.sizeDelta = new Vector2(size.x, most); // the framework's template: the next list opens this tall
                        }
                    }
                    if (!labels.TryGetValue(key, out Func<string[]> wantOf))
                    {
                        continue;
                    }
                    string[] want = wantOf();
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
