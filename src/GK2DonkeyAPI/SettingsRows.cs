using System;
using System.Collections.Generic;
using BepInEx;
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
    // between the game's own rows and pages of the mods' rows, each as many rows as the game's own page has (its
    // setting rows counted when the window is first built, at least MinRowsPerPage), so the window keeps its size.
    // What isn't a setting row of the game's (the OK button) stays on every page, below the rows. Every row is a copy of the
    // game's own row, so it has the game's look, sounds, keys and gamepad navigation.
    //
    // Found through LazyWindowsStackController.OnWindowOpened (no patch): the first time the window opens its rows are
    // added; every open starts on the game's page. A key row is set by capture: either arrow starts it ("Press a key..."),
    // the next key pressed (with its Shift/Ctrl/Alt) is the key, Backspace clears it, Esc / another page / closing the
    // window / 10 s cancel (Tick). While it waits, the window's own input is off (the game's LazyWindowInputController,
    // as when another window covers it): Esc then cancels instead of closing the window (as in GK2 Mod Framework's
    // Mods menu), and the arrow keys can be chosen too. Text can't be edited with "< value >": it shows read-only.
    // Guarded: if the window or a row to copy isn't found (a game update changed the window), nothing is added, the
    // cfg still works, and the log says so once.
    internal static class SettingsRows
    {
        private const int MinRowsPerPage = 10;
        private static int rowsPerPage = MinRowsPerPage;
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
            internal MountSettingsPage Page;
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
        private static readonly List<GameObject> keptRows = new List<GameObject>(); // the OK button: on every page
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
            keptRows.Clear();
            // The row to copy: the first "< value >" row, by its component (a name could change with an update).
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            Transform content = template != null ? RowOf(template.transform) : null;
            if (template == null || content == null)
            {
                Fail("no row to copy in the window");
                return;
            }
            GameObject templateRow = RowGameObject(template.transform, content);
            int gameSettingRows = 0;
            foreach (Transform child in content)
            {
                if (child.name.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                // A setting row has a "< value >" switch or a volume slider (UIGameSettingsWindow's fields); anything
                // else (the OK button) is kept on the mods' pages too.
                bool settingRow = child.GetComponentInChildren<UISwitchButton>(true) != null || child.GetComponentInChildren<UISlider>(true) != null;
                if (settingRow)
                {
                    gameRows.Add(child.gameObject);
                    if (child.gameObject.activeSelf)
                    {
                        gameSettingRows++;
                    }
                }
                else
                {
                    keptRows.Add(child.gameObject);
                }
            }
            rowsPerPage = Math.Max(MinRowsPerPage, gameSettingRows);

            // The pages: each mod's rows by section, in their order, cut at rowsPerPage (a section heading counts).
            foreach (MountSettingsPage page in pages)
            {
                var bySection = new List<KeyValuePair<string, List<MountSettingsPage.Row>>>();
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    if (r.Kind == MountSettingsPage.Kind.Preview)
                    {
                        continue; // the picture goes above this window (LookPreview)
                    }
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
                    string heading = section.Value[0].ShownSection ?? section.Key;
                    // A new page when the whole section doesn't fit; a section longer than a page goes on over the
                    // next, its heading repeated.
                    if (cur == null || cur.Rows.Count + 1 + section.Value.Count > rowsPerPage && cur.Rows.Count > 0)
                    {
                        cur = new Shown();
                        mine.Add(cur);
                    }
                    cur.Rows.Add(Make(templateRow, content, null, page, heading));
                    foreach (MountSettingsPage.Row r in section.Value)
                    {
                        if (cur.Rows.Count >= rowsPerPage)
                        {
                            cur = new Shown();
                            mine.Add(cur);
                            cur.Rows.Add(Make(templateRow, content, null, page, heading + " (more)"));
                        }
                        cur.Rows.Add(Make(templateRow, content, r, page, null));
                    }
                }
                for (int i = 0; i < mine.Count; i++)
                {
                    mine[i].Page = page;
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
            Plugin.Log.LogInfo($"Settings: added {rows} rows on {shown.Count} page(s) to the game's Settings window ({rowsPerPage} a page, as the game's own; {keptRows.Count} other element(s) of the window kept on every page).");
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

        internal static Transform ContentOf(Transform t) => RowOf(t);

        // The page shown now, and the window (for the donkey's picture beside it); null on the game's page.
        internal static MountSettingsPage ShownPage => window != null && current > 0 && current <= shown.Count && LazyWindowsStackController.IsWindowOpened(window) ? shown[current - 1].Page : null;
        internal static Transform WindowRect => window != null ? window.transform : null;

        // The Settings window's own frame (for the picture above it), or the window.
        internal static RectTransform WindowFrame => window == null ? null : (window.transform.Find("GenericWIndowLayout") as RectTransform) ?? (RectTransform)window.transform;

        // One row under any parent (the look bar's too), and how to bring its value up to date.
        internal static GameObject MakeRow(GameObject template, Transform parent, MountSettingsPage.Row r, MountSettingsPage page, out Action refresh)
        {
            Built b = Make(template, parent, r, page, null);
            refresh = () => Sync(b);
            return b.Go;
        }

        // A row with a label only (a heading or a note), shown.
        internal static GameObject MakeHeading(GameObject template, Transform parent, MountSettingsPage page, string text)
        {
            Built b = Make(template, parent, null, page, text);
            b.Go.SetActive(true);
            return b.Go;
        }

        internal static void SetHeading(GameObject row, string text)
        {
            UISwitchButton sw = row != null ? row.GetComponentInChildren<UISwitchButton>(true) : null;
            if (sw != null)
            {
                SetLabel(row, sw, text);
            }
        }

        internal static GameObject RowGameObject(Transform t, Transform content)
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
        // A note's text across the whole row, centred: a copy of the row's label (its font and colour) laid over the
        // row outside its layout, the label itself hidden; smaller if the sentence doesn't fit.
        private static void WidenToRow(GameObject row, TextMeshProUGUI label)
        {
            GameObject copy = UnityEngine.Object.Instantiate(label.gameObject, row.transform, false);
            copy.name = "Note";
            var le = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            ContentSizeFitter fit = copy.GetComponent<ContentSizeFitter>();
            if (fit != null)
            {
                UnityEngine.Object.Destroy(fit); // it would size the copy to its text again
            }
            var rt = (RectTransform)copy.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            TextMeshProUGUI t = copy.GetComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = label.fontSize;
            t.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.6f);
            label.gameObject.SetActive(false);
            copy.SetActive(true);
        }

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
            bool note = r != null && r.Kind == MountSettingsPage.Kind.Note;
            if (r == null || note)
            {
                // A section heading, or a note: the label only, everything else of the row hidden.
                TextMeshProUGUI label = SetLabel(b.Go, b.Switch, note ? r.Description : heading);
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
                if (note && label != null)
                {
                    WidenToRow(b.Go, label);
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
                case MountSettingsPage.Kind.Button:
                    // Either arrow (or the gamepad's left/right) presses it.
                    b.Fields = new[] { "Open", "Open " };
                    break;
                case MountSettingsPage.Kind.Key:
                    // Either arrow starts the capture (Tick): two fields, so either arrow is a change.
                    b.Fields = KeyFields(KeyText(r));
                    break;
                default:
                    b.Fields = new[] { ReadOnlyText(r) };
                    break;
            }
            Built captured = b;
            b.Switch.Initialize(i => Changed(captured, i), b.Fields, IndexOf(b), "", null, null, loopNavigation: r.Kind != MountSettingsPage.Kind.Steps);
            b.Switch.IsInteractable = b.Values != null || r.Kind == MountSettingsPage.Kind.Button || r.Kind == MountSettingsPage.Kind.Key;
            return b;
        }

        private static string ReadOnlyText(MountSettingsPage.Row r)
        {
            string s;
            switch (r.Kind)
            {
                case MountSettingsPage.Kind.Info:
                    s = r.InfoValue?.Invoke();
                    if (string.IsNullOrEmpty(s))
                    {
                        return "-";
                    }
                    break;
                case MountSettingsPage.Kind.Key: return KeyText(r);
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
            if (b.Row?.Kind == MountSettingsPage.Kind.Key)
            {
                StartCapture(b);
                return;
            }
            if (b.Row?.Kind == MountSettingsPage.Kind.Button)
            {
                b.Switch.UpdateField(0, fireCallback: false);
                try
                {
                    b.Row.OnClick?.Invoke();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Settings: {b.Row.Label} failed: {e.Message}");
                }
                return;
            }
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
            // One change can show or hide others (Custom) and change a swatch.
            if (current > 0)
            {
                Show(current);
            }
            LookBar.Refresh();
        }

        // Its value up to date (the cfg, the Mods menu or another row may have changed it).
        private static void Sync(Built b)
        {
            if (b.Row == null || b.Row.Kind == MountSettingsPage.Kind.Button || b.Row.Kind == MountSettingsPage.Kind.Note)
            {
                return;
            }
            if (b.Values != null)
            {
                b.Switch.UpdateField(IndexOf(b), fireCallback: false);
            }
            else if (b.Row.Kind == MountSettingsPage.Kind.Key)
            {
                b.Switch.ReinitLabels(KeyFields(capturing == b ? "Press a key (Esc cancels)" : KeyText(b.Row)));
                b.Switch.UpdateField(0, fireCallback: false);
            }
            else
            {
                b.Switch.ReinitLabels(new[] { ReadOnlyText(b.Row) });
                b.Switch.UpdateField(0, fireCallback: false);
            }
        }

        // ------------------------------------------------------------------ setting a key

        private static Built capturing;
        private static int captureFrom, captureEndedFrame = -1, capturePage;
        private static float captureUntil;
        private static KeyCode[] keyboardKeys;
        private const float CaptureSeconds = 10f;

        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        // A key is being captured, or was this frame: the mods' key actions wait (KeyActions), so pressing G to set a
        // key doesn't also bring the cart.
        internal static bool CapturingKey => capturing != null || Time.frameCount == captureEndedFrame;

        private static string KeyText(MountSettingsPage.Row r)
        {
            var k = (KeyboardShortcut)r.Entry.BoxedValue;
            return k.MainKey == KeyCode.None ? "Not set" : k.ToString();
        }

        private static string[] KeyFields(string text) => new[] { text, text + " " };

        private static void StartCapture(Built b)
        {
            Built was = capturing;
            if (was == null)
            {
                WindowInput(on: false);
            }
            capturing = b;
            captureFrom = Time.frameCount + 1; // not the press that started it (an arrow, Enter, a click)
            captureUntil = Time.unscaledTime + CaptureSeconds;
            capturePage = current;
            if (was != null && was != b)
            {
                Sync(was);
            }
            Sync(b);
        }

        private static void EndCapture()
        {
            Built b = capturing;
            if (b != null)
            {
                WindowInput(on: true);
            }
            capturing = null;
            captureEndedFrame = Time.frameCount;
            if (b != null)
            {
                Sync(b);
            }
        }

        private static System.Reflection.FieldInfo inputField;
        private static bool inputFieldMissing;

        // The Settings window's own keys (Esc closes it, the arrows step the focused row) off while a key is captured,
        // and on again: the game's own Disable / Enable pair for a window covered by another. Not found (a game
        // update): the capture works without it, and Esc may close the window.
        private static void WindowInput(bool on)
        {
            if (window == null || inputFieldMissing)
            {
                return;
            }
            try
            {
                if (inputField == null)
                {
                    for (Type t = window.GetType(); t != null && inputField == null; t = t.BaseType)
                    {
                        inputField = t.GetField("lazyWindowInputController", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly); // LazyWindow<T>
                    }
                    if (inputField == null)
                    {
                        inputFieldMissing = true;
                        Plugin.Log.LogInfo("Settings: the Settings window's input controller isn't found; Esc may close the window while a key is being set.");
                        return;
                    }
                }
                if (!(inputField.GetValue(window) is LazyWindowInputController input))
                {
                    return;
                }
                if (on)
                {
                    if (LazyWindowsStackController.IsWindowOpened(window))
                    {
                        input.Enable(restoreFocused: true);
                    }
                }
                else
                {
                    input.Disable(rememberFocused: true);
                }
            }
            catch (Exception e)
            {
                inputFieldMissing = true;
                Plugin.Log.LogWarning("Settings: switching the Settings window's keys failed (" + e.Message + "); Esc may close the window while a key is being set.");
            }
        }

        // Every frame (Plugin.LateUpdate, before the mods' key actions): the key being captured.
        internal static void Tick()
        {
            if (capturing == null)
            {
                return;
            }
            if (window == null || !LazyWindowsStackController.IsWindowOpened(window) || current != capturePage || Time.unscaledTime > captureUntil)
            {
                EndCapture(); // cancelled
                return;
            }
            if (Time.frameCount < captureFrom)
            {
                return;
            }
            if (keyboardKeys == null)
            {
                var list = new List<KeyCode>();
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    // The keyboard only: no mouse or joystick buttons; the modifiers come with a key.
                    if (k != KeyCode.None && k < KeyCode.Mouse0 && Array.IndexOf(Modifiers, k) < 0 && !list.Contains(k))
                    {
                        list.Add(k);
                    }
                }
                keyboardKeys = list.ToArray();
            }
            foreach (KeyCode k in keyboardKeys)
            {
                if (!UnityInput.Current.GetKeyDown(k))
                {
                    continue;
                }
                if (k == KeyCode.Escape)
                {
                    EndCapture();
                    return;
                }
                KeyboardShortcut set;
                if (k == KeyCode.Backspace)
                {
                    set = KeyboardShortcut.Empty;
                }
                else
                {
                    var held = new List<KeyCode>();
                    foreach (KeyCode m in Modifiers)
                    {
                        if (UnityInput.Current.GetKey(m))
                        {
                            held.Add(m);
                        }
                    }
                    set = new KeyboardShortcut(k, held.ToArray());
                }
                Built b = capturing;
                try
                {
                    b.Row.Entry.BoxedValue = set; // its SettingChanged applies it (MountSettingsPage.Wire)
                    Plugin.Log.LogInfo($"Settings: {b.Row.Label} set to {(set.MainKey == KeyCode.None ? "nothing" : set.ToString())} in the game's Settings window.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Settings: {b.Row.Label} could not be set: {e.Message}");
                }
                EndCapture();
                return;
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
            if (capturing != null && capturePage != page)
            {
                EndCapture();
            }
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
                    if (on)
                    {
                        Sync(b);
                    }
                    b.Go.SetActive(on);
                }
            }
            foreach (GameObject g in keptRows)
            {
                if (g != null)
                {
                    g.transform.SetAsLastSibling(); // below the mods' rows, which are added after it
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
