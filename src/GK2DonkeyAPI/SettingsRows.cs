using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // Player mods' settings pages (MountSettingsPage) in the game's own Settings window, for players without GK2 Mod
    // Framework. The window is the game's: a fixed list of rows, each a UISwitchButton ("< value >") with its label.
    // The game's window can't grow by twenty rows (it would leave the screen), so a "Showing" row at the top switches
    // between the game's own rows and pages of the mods' rows, at most RowsPerPage each. Every row is a copy of the
    // game's own row, so it has the game's look, sounds, keys and gamepad navigation.
    //
    // Found through LazyWindowsStackController.OnWindowOpened (no patch): the first time the window opens its rows are
    // added; every open starts on the game's page. Keys and text can't be edited with "< value >": they show read-only.
    // Guarded: if the window or a row to copy isn't found (a game update changed the window), nothing is added, the
    // cfg still works, and the log says so once.
    internal static class SettingsRows
    {
        private const int RowsPerPage = 10;
        private const string Prefix = "GK2DonkeyAPI ";

        private static readonly List<MountSettingsPage> pages = new List<MountSettingsPage>();
        private static bool subscribed, failed;

        internal static void Add(MountSettingsPage page)
        {
            pages.Add(page);
            if (!subscribed)
            {
                subscribed = true;
                LazyWindowsStackController.OnWindowOpened += OnOpened;
            }
            Plugin.Log.LogInfo($"Settings: {page.Title} goes into the game's Settings window ({page.Rows.Count} rows; GK2 Mod Framework isn't installed).");
        }

        // ------------------------------------------------------------------ the window

        private sealed class Shown
        {
            internal string Name;              // in the "Showing" row
            internal readonly List<Built> Rows = new List<Built>();
        }

        private sealed class Built
        {
            internal MountSettingsPage.Row Row;
            internal GameObject Go;
            internal UISwitchButton Switch;
            internal object[] Values;          // what each field index sets (null: read-only)
            internal string[] Fields;
        }

        private static UIGameSettingsWindow window;
        private static GameObject showing;
        private static UISwitchButton showingSwitch;
        private static readonly List<GameObject> gameRows = new List<GameObject>();
        private static readonly Dictionary<GameObject, bool> gameRowsWereActive = new Dictionary<GameObject, bool>();
        private static readonly List<Shown> shown = new List<Shown>();
        private static int current;

        private static void OnOpened(LazyWidgetBase w)
        {
            if (failed || !(w is UIGameSettingsWindow settings))
            {
                return;
            }
            try
            {
                if (window != settings || showing == null)
                {
                    Build(settings);
                }
                if (showing != null)
                {
                    Show(0);
                }
            }
            catch (Exception e)
            {
                Fail("adding the rows failed: " + e.Message);
            }
        }

        private static void Fail(string why)
        {
            failed = true;
            Plugin.Log.LogWarning($"Settings: the mods' settings can't go into the game's Settings window ({why}). They're in their cfg files (BepInEx\\config).");
        }

        private static void Build(UIGameSettingsWindow settings)
        {
            window = settings;
            shown.Clear();
            gameRows.Clear();
            // The row to copy: the first "< value >" row, by its component (a name could change with an update).
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            Transform content = template != null ? RowOf(template.transform) : null;
            if (template == null || content == null)
            {
                Fail("no row to copy in the window");
                return;
            }
            GameObject templateRow = RowGameObject(template.transform, content);
            foreach (Transform child in content)
            {
                if (!child.name.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    gameRows.Add(child.gameObject);
                }
            }

            // The pages: each mod's rows by section, in their order, cut at RowsPerPage (a section heading counts).
            foreach (MountSettingsPage page in pages)
            {
                var bySection = new List<KeyValuePair<string, List<MountSettingsPage.Row>>>();
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    int at = bySection.FindIndex(kv => kv.Key == r.Section);
                    if (at < 0)
                    {
                        bySection.Add(new KeyValuePair<string, List<MountSettingsPage.Row>>(r.Section, new List<MountSettingsPage.Row>()));
                        at = bySection.Count - 1;
                    }
                    bySection[at].Value.Add(r);
                }
                var mine = new List<Shown>();
                Shown cur = null;
                foreach (var section in bySection)
                {
                    if (cur == null || cur.Rows.Count + 1 + section.Value.Count > RowsPerPage && cur.Rows.Count > 0)
                    {
                        cur = new Shown();
                        mine.Add(cur);
                    }
                    cur.Rows.Add(Make(templateRow, content, null, page, section.Key));
                    foreach (MountSettingsPage.Row r in section.Value)
                    {
                        cur.Rows.Add(Make(templateRow, content, r, page, null));
                    }
                }
                for (int i = 0; i < mine.Count; i++)
                {
                    mine[i].Name = mine.Count == 1 ? page.Title : $"{page.Title} {i + 1}/{mine.Count}";
                    shown.Add(mine[i]);
                }
            }

            // The "Showing" row, first in the window.
            var names = new List<string> { "Game settings" };
            foreach (Shown s in shown)
            {
                names.Add(s.Name);
            }
            showing = Copy(templateRow, content, Prefix + "Showing");
            showing.transform.SetSiblingIndex(0);
            showingSwitch = showing.GetComponentInChildren<UISwitchButton>(true);
            SetLabel(showing, showingSwitch, "Showing");
            showingSwitch.Initialize(Show, names.ToArray(), 0);
            showing.SetActive(true);
            int rows = 0;
            foreach (Shown s in shown)
            {
                rows += s.Rows.Count;
            }
            Plugin.Log.LogInfo($"Settings: added {rows} rows on {shown.Count} page(s) to the game's Settings window.");
        }

        // The window's content: the vertical layout the rows sit in, above the UISwitchButton.
        private static Transform RowOf(Transform t)
        {
            Transform row = t;
            while (row.parent != null && row.parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                row = row.parent;
            }
            return row.parent;
        }

        private static GameObject RowGameObject(Transform t, Transform content)
        {
            while (t.parent != content)
            {
                t = t.parent;
            }
            return t.gameObject;
        }

        private static GameObject Copy(GameObject template, Transform content, string name)
        {
            GameObject go = UnityEngine.Object.Instantiate(template, content);
            go.name = name;
            // The game's labels fill themselves from translation keys on Start; ours are set here.
            foreach (LocalizedLabel l in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                UnityEngine.Object.DestroyImmediate(l);
            }
            go.SetActive(false);
            return go;
        }

        // The label left of the "< value >" (the game's is called LeftName): a text named so, else the first text
        // outside the switch.
        private static TextMeshProUGUI SetLabel(GameObject go, UISwitchButton sw, string text)
        {
            TextMeshProUGUI found = null;
            foreach (TextMeshProUGUI t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = t;
                    break;
                }
                if (found == null && !t.transform.IsChildOf(sw.transform))
                {
                    found = t;
                }
            }
            if (found != null)
            {
                found.text = text;
            }
            return found;
        }

        private static Built Make(GameObject template, Transform content, MountSettingsPage.Row r, MountSettingsPage page, string heading)
        {
            var b = new Built { Row = r };
            b.Go = Copy(template, content, Prefix + page.OwnerId + " " + (r?.Entry?.Definition.Key ?? r?.Label ?? heading));
            b.Switch = b.Go.GetComponentInChildren<UISwitchButton>(true);
            if (r == null)
            {
                // A section heading: the label only, everything else of the row hidden.
                TextMeshProUGUI label = SetLabel(b.Go, b.Switch, heading);
                b.Fields = new[] { "" };
                b.Switch.Initialize(_ => { }, b.Fields, 0);
                b.Switch.IsInteractable = false;
                foreach (Transform c in b.Go.GetComponentsInChildren<Transform>(true))
                {
                    if (c != b.Go.transform && label != null && !label.transform.IsChildOf(c) && !c.IsChildOf(label.transform))
                    {
                        c.gameObject.SetActive(false);
                    }
                }
                foreach (GamepadNavigationItem n in b.Go.GetComponentsInChildren<GamepadNavigationItem>(true))
                {
                    n.enabled = false; // not a stop for the gamepad
                }
                return b;
            }
            SetLabel(b.Go, b.Switch, r.Label);
            switch (r.Kind)
            {
                case MountSettingsPage.Kind.Toggle:
                    b.Fields = new[] { "Off", "On" };
                    b.Values = new object[] { false, true };
                    break;
                case MountSettingsPage.Kind.Choice:
                    b.Fields = r.ChoiceLabels;
                    b.Values = new object[r.ChoiceValues.Length];
                    for (int i = 0; i < b.Values.Length; i++)
                    {
                        b.Values[i] = r.ChoiceValues.GetValue(i);
                    }
                    break;
                case MountSettingsPage.Kind.Steps:
                    var f = new List<string>();
                    var v = new List<object>();
                    for (int n = r.Min; n <= r.Max; n += r.Step)
                    {
                        f.Add(r.Format != null ? r.Format(n) : n.ToString());
                        v.Add(n);
                    }
                    b.Fields = f.ToArray();
                    b.Values = v.ToArray();
                    break;
                default:
                    b.Fields = new[] { ReadOnlyText(r) };
                    break;
            }
            Built captured = b;
            b.Switch.Initialize(i => Changed(captured, i), b.Fields, IndexOf(b), "", null, null, loopNavigation: r.Kind != MountSettingsPage.Kind.Steps);
            b.Switch.IsInteractable = b.Values != null;
            return b;
        }

        private static string ReadOnlyText(MountSettingsPage.Row r)
        {
            string s;
            switch (r.Kind)
            {
                case MountSettingsPage.Kind.Info: s = r.InfoValue?.Invoke(); break;
                case MountSettingsPage.Kind.Key: s = ((KeyboardShortcut)r.Entry.BoxedValue).ToString(); break;
                default: s = r.Entry?.BoxedValue as string; break;
            }
            return string.IsNullOrEmpty(s) ? "(none; set in the cfg)" : s;
        }

        private static int IndexOf(Built b)
        {
            if (b.Values == null)
            {
                return 0;
            }
            object now = b.Row.Entry.BoxedValue;
            for (int i = 0; i < b.Values.Length; i++)
            {
                if (Equals(b.Values[i], now))
                {
                    return i;
                }
            }
            return 0;
        }

        private static void Changed(Built b, int index)
        {
            if (b.Values == null || index < 0 || index >= b.Values.Length)
            {
                return;
            }
            try
            {
                b.Row.Entry.BoxedValue = b.Values[index]; // its SettingChanged applies it (MountSettingsPage.Wire)
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {b.Row.Label} could not be set: {e.Message}");
            }
        }

        // 0: the game's own rows; 1..n: the mods' pages.
        private static void Show(int page)
        {
            if (showing == null)
            {
                return;
            }
            if (current == 0 && page != 0)
            {
                gameRowsWereActive.Clear();
                foreach (GameObject g in gameRows)
                {
                    if (g != null)
                    {
                        gameRowsWereActive[g] = g.activeSelf;
                    }
                }
            }
            current = page;
            foreach (GameObject g in gameRows)
            {
                if (g != null)
                {
                    g.SetActive(page == 0 && (!gameRowsWereActive.TryGetValue(g, out bool was) || was));
                }
            }
            for (int i = 0; i < shown.Count; i++)
            {
                foreach (Built b in shown[i].Rows)
                {
                    bool on = i + 1 == page && (b.Row?.Visible == null || b.Row.Visible());
                    if (on && b.Row != null)
                    {
                        // The cfg or the Mods menu may have changed it since.
                        if (b.Values != null)
                        {
                            b.Switch.UpdateField(IndexOf(b), fireCallback: false);
                        }
                        else
                        {
                            b.Switch.ReinitLabels(new[] { ReadOnlyText(b.Row) });
                            b.Switch.UpdateField(0, fireCallback: false);
                        }
                    }
                    b.Go.SetActive(on);
                }
            }
            if (showingSwitch != null && showingSwitch.CurrentFieldIndex != page)
            {
                showingSwitch.UpdateField(page, fireCallback: false);
            }
            if (window != null)
            {
                ((RectTransform)window.transform).RefreshContentFitter();
                window.GetComponent<GamepadNavigationController>()?.ReinitItems(focusOnFirstActive: false);
            }
        }
    }
}
