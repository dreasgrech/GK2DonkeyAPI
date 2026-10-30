using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The options window's self-tests (docs/options-window.md; SelfTest runs them in "Skins and effects"). A self-test
    // can't press a key or click: a control is pressed through its own button, a key is set through KeyCapture's own entry
    // point (TestSet, as a press would), a step back through the window's StepBack (what Escape calls). Every option the
    // tests change is put back as it was, in the end, whatever happened.
    internal static class OptionsTests
    {
        private static MountSettingsPage PageWithOptions()
        {
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && OptionsWindow.HasOptions(live)) return live;
            return null;
        }

        // Every option of the page and its value now, to put back.
        private static Dictionary<ConfigEntryBase, object> Snapshot(MountSettingsPage page)
        {
            var was = new Dictionary<ConfigEntryBase, object>();
            foreach (MountSettingsPage.Row r in page.Rows) if (page.InOptions(r) && r.Entry != null) was[r.Entry] = r.Entry.BoxedValue;
            return was;
        }

        private static void PutBack(Dictionary<ConfigEntryBase, object> was)
        {
            foreach (var kv in was)
            {
                try { if (!Equals(kv.Key.BoxedValue, kv.Value)) kv.Key.BoxedValue = kv.Value; }
                catch (Exception e) { SelfTest.Warn($"{kv.Key.Definition.Key} could not be put back: {e.Message}"); }
            }
        }

        private static MountSettingsPage.Row RowWhere(MountSettingsPage page, Func<MountSettingsPage.Row, bool> what)
        {
            foreach (MountSettingsPage.Row r in page.Rows) if (page.InOptions(r) && what(r)) return r;
            return null;
        }

        private static string ShownName(MountSettingsPage.Row r) => r.ShownSection ?? r.Section;

        // A switch the tests may press: shown, usable, and not in the section with the page's Mounts button (the mount's
        // own section: keeping the mount with the save, say, which a flip would apply to the player's mount). allDefault:
        // its section's other options are all at their defaults (Defaults then changes only this switch).
        private static MountSettingsPage.Row SafeToggle(MountSettingsPage page, bool allDefault)
        {
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || r.RowKind != MountSettingsPage.RowKind.Toggle || r.Entry == null) continue;
                if ((r.Visible != null && !r.Visible()) || !OptionsWindow.Usable(r)) continue;
                if (RowWhere(page, o => o.OpensPanel && o.Section == r.Section) != null) continue;
                if (allDefault && RowWhere(page, o => o != r && o.Section == r.Section && o.Entry != null && !Equals(o.Entry.BoxedValue, o.Entry.DefaultValue)) != null) continue;
                return r;
            }
            return null;
        }

        // The window over the game: it fits, has the page's sections, every row of every section is our own control and
        // inside its column; a switch, a key (set, said in words, two pressed keys alike are a clash on both rows), the
        // description line, Defaults (asked; Cancel keeps, Set back sets the section back); a step back closes it.
        internal static IEnumerator Smoke()
        {
            MountSettingsPage page = PageWithOptions();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page with options is shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            Dictionary<ConfigEntryBase, object> was = Snapshot(page);
            try
            {
                OptionsWindow.Open(page);
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown && GameWindowAside.ControlsTaken, "the options window opens over the game and holds the controls");
                if (!OptionsWindow.IsShown) yield break;
                SelfTest.Check(OptionsWindow.TestScale >= 0.5f, $"it fits the screen (scale {OptionsWindow.TestScale:0.##})");
                // GK2 Mod Framework installed: the page is in its Mods menu, and its Settings button opens this window
                if (!FrameworkLink.Installed) SelfTest.Info("GK2 Mod Framework isn't installed: its Mods menu check skipped");
                else SelfTest.Check(FrameworkLink.AnyRegistered && FrameworkLink.SettingsButtonPointed,
                    $"GK2 Mod Framework is installed: the page is in its Mods menu ({FrameworkLink.AnyRegistered}) and its Settings button opens the options window ({FrameworkLink.SettingsButtonPointed})");

                // the sections: the page's, in its order
                var expected = new List<string>();
                foreach (MountSettingsPage.Row r in page.Rows) if (page.InOptions(r) && !expected.Contains(ShownName(r))) expected.Add(ShownName(r));
                string sections = OptionsWindow.TestSections();
                SelfTest.Check(sections == string.Join(", ", expected), $"its sections are the page's, in order ({sections})");

                // every section: its shown rows, each our own control, inside the column
                var kinds = new Dictionary<MountSettingsPage.RowKind, int>();
                int total = 0;
                foreach (string name in expected)
                {
                    OptionsWindow.TestShowSection(name);
                    yield return SelfTest.Frames(1);
                    int shown = 0;
                    foreach (MountSettingsPage.Row r in page.Rows)
                    {
                        if (!page.InOptions(r) || ShownName(r) != name || (r.Visible != null && !r.Visible())) continue;
                        shown++;
                        kinds[r.RowKind] = kinds.TryGetValue(r.RowKind, out int n) ? n + 1 : 1;
                        if (OptionsWindow.TestRowObject(r) == null) SelfTest.Check(false, $"{name}: '{r.Label}' is drawn");
                    }
                    total += shown;
                    string outside = OptionsWindow.TestOutside();
                    SelfTest.Check(OptionsWindow.TestRowCount == shown && OptionsWindow.TestGameRows() == 0 && outside == null,
                        $"{name}: its {shown} rows are drawn, every one our own control ({OptionsWindow.TestGameRows()} game rows), inside the column ({outside ?? "nothing outside"})");
                }
                var kindWords = new List<string>();
                foreach (var kv in kinds) kindWords.Add(kv.Key + " " + kv.Value);
                SelfTest.Info($"{total} options shown in {expected.Count} sections: {string.Join(", ", kindWords)}");

                // a switch: its own button flips the value, and the value is the entry's
                MountSettingsPage.Row toggle = SafeToggle(page, allDefault: false);
                if (toggle == null) SelfTest.Info("no switch to press: the switch check skipped");
                else
                {
                    OptionsWindow.TestShowSection(ShownName(toggle));
                    yield return SelfTest.Frames(1);
                    bool before = (bool)toggle.Entry.BoxedValue;
                    Transform sw = OptionsWindow.TestRowObject(toggle)?.transform.Find("GK2DonkeyAPI options switch");
                    sw?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
                    yield return SelfTest.Frames(1);
                    SelfTest.Check(sw != null && (bool)toggle.Entry.BoxedValue == !before, $"'{toggle.Label}': its switch pressed sets the option ({before} -> {toggle.Entry.BoxedValue})");
                    sw = OptionsWindow.TestRowObject(toggle)?.transform.Find("GK2DonkeyAPI options switch"); // the row may have been built again
                    sw?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
                    yield return SelfTest.Frames(1);
                    SelfTest.Check((bool)toggle.Entry.BoxedValue == before, $"pressed again: back to {before}");
                }

                // keys: in words; set as a press would; two pressed keys alike are a clash on both rows
                SelfTest.Check(KeyCapture.Words(new KeyboardShortcut(KeyCode.LeftShift)) == "Left Shift" && KeyCapture.Words(new KeyboardShortcut(KeyCode.G, KeyCode.LeftControl)) == "Ctrl + G"
                    && KeyCapture.Words(KeyboardShortcut.Empty) == "None" && KeyCapture.Words(new KeyboardShortcut(KeyCode.Alpha1)) == "1",
                    $"keys in words: {KeyCapture.Words(new KeyboardShortcut(KeyCode.LeftShift))}, {KeyCapture.Words(new KeyboardShortcut(KeyCode.G, KeyCode.LeftControl))}, {KeyCapture.Words(KeyboardShortcut.Empty)}, {KeyCapture.Words(new KeyboardShortcut(KeyCode.Alpha1))}");
                MountSettingsPage.Row k1 = RowWhere(page, r => r.RowKind == MountSettingsPage.RowKind.Key && r.HasPress && OptionsWindow.Usable(r));
                MountSettingsPage.Row k2 = k1 == null ? null : RowWhere(page, r => r != k1 && r.RowKind == MountSettingsPage.RowKind.Key && r.HasPress && ShownName(r) == ShownName(k1) && OptionsWindow.Usable(r));
                if (k1 == null || k2 == null) SelfTest.Info("not two keys that act on their press in one section: the key checks skipped");
                else
                {
                    OptionsWindow.TestShowSection(ShownName(k1));
                    yield return SelfTest.Frames(1);
                    OptionsWindow.TestStartKey(k1);
                    yield return SelfTest.Frames(1);
                    SelfTest.Check(KeyCapture.Row == k1 && OptionsWindow.TestKeyBoxText(k1) == "Press a key..." && KeyActions.WaitReason() == "a key is being set",
                        $"'{k1.Label}' clicked: its box waits for a key (\"{OptionsWindow.TestKeyBoxText(k1)}\") and the mods' keys wait ({KeyActions.WaitReason() ?? "they don't"})");
                    KeyCapture.Cancel();
                    yield return SelfTest.Frames(2);
                    SelfTest.Check(KeyCapture.Row == null && ((KeyboardShortcut)k1.Entry.BoxedValue).ToString() == ((KeyboardShortcut)was[k1.Entry]).ToString() && KeyActions.WaitReason() == null,
                        "cancelled: the key unchanged, the mods' keys work again");
                    var f9 = new KeyboardShortcut(KeyCode.F9);
                    bool set1 = OptionsWindow.TestSetKey(k1, f9);
                    yield return SelfTest.Frames(1);
                    SelfTest.Check(set1 && KeyActions.SameKey((KeyboardShortcut)k1.Entry.BoxedValue, f9) && OptionsWindow.TestKeyBoxText(k1) == "F9",
                        $"'{k1.Label}' set to F9: the option holds it and the box says \"{OptionsWindow.TestKeyBoxText(k1)}\"");
                    bool set2 = OptionsWindow.TestSetKey(k2, f9);
                    yield return SelfTest.Frames(1);
                    OptionsWindow.TestRefresh();
                    yield return SelfTest.Frames(1);
                    string c1 = OptionsWindow.TestClashLine(k1), c2 = OptionsWindow.TestClashLine(k2);
                    SelfTest.Check(set2 && c1 == "Same key as " + k2.Label && c2 == "Same key as " + k1.Label, $"'{k2.Label}' set to F9 too: both rows say so (\"{c1}\" / \"{c2}\")");
                    k2.Entry.BoxedValue = was[k2.Entry];
                    OptionsWindow.TestRefresh();
                    yield return SelfTest.Frames(1);
                    SelfTest.Check(OptionsWindow.TestClashLine(k1) == null, "set back: no clash line");
                }

                // the description line: the row's description, and for a key how to set it
                MountSettingsPage.Row described = k1 ?? toggle;
                if (described != null)
                {
                    string d = OptionsWindow.TestDescription(described);
                    SelfTest.Check(!string.IsNullOrEmpty(described.Description) && d.StartsWith(described.Description, StringComparison.Ordinal), $"the line at the bottom says what '{described.Label}' does (\"{d}\")");
                }

                // Defaults: asked first; Cancel keeps, Set back sets the section back
                MountSettingsPage.Row changed = SafeToggle(page, allDefault: true);
                if (changed == null) SelfTest.Info("no section away from the mount's own with every option at its default: the Defaults check skipped (the player's options are left as they are)");
                else
                {
                    OptionsWindow.TestShowSection(ShownName(changed));
                    yield return SelfTest.Frames(1);
                    changed.Entry.BoxedValue = !(bool)changed.Entry.DefaultValue;
                    OptionsWindow.TestOpenConfirm();
                    yield return SelfTest.Frames(1);
                    bool asked = OptionsWindow.TestConfirmOpen;
                    OptionsWindow.TestStepBack(); // Escape: the question goes, nothing changes
                    yield return SelfTest.Frames(1);
                    SelfTest.Check(asked && !OptionsWindow.TestConfirmOpen && OptionsWindow.IsShown && !Equals(changed.Entry.BoxedValue, changed.Entry.DefaultValue),
                        $"Defaults asks first; a step back from the question keeps '{changed.Label}' as set and the window open");
                    OptionsWindow.TestOpenConfirm();
                    yield return SelfTest.Frames(1);
                    OptionsWindow.TestConfirmYes();
                    yield return SelfTest.Frames(1);
                    bool allDefault = true;
                    foreach (MountSettingsPage.Row r in page.Rows)
                        if (page.InOptions(r) && r.Entry != null && r.Section == changed.Section && !Equals(r.Entry.BoxedValue, r.Entry.DefaultValue)) allDefault = false;
                    SelfTest.Check(allDefault, $"Set back: every option of {ShownName(changed)} is its default");
                }

                // a step back closes it into the game
                OptionsWindow.TestStepBack();
                yield return SelfTest.Frames(2);
                SelfTest.Check(!OptionsWindow.IsOpen && LazyWindowsStackController.ActiveWindow == null && MainGame.PlayerController.IsControlEnabledByType(TakenControlType.ByUI),
                    "a step back: closed into the game, the controls given back");
            }
            finally
            {
                if (KeyCapture.Row != null) KeyCapture.Cancel();
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                PutBack(was);
            }
        }

        // The way back: the game's Settings window gets the page's button, which opens the options over it (set aside,
        // still open); the Mounts button opens the journal over the options and a step back returns to them; a step back
        // from the options shows the Settings window again. The journal's Options button opens them over the journal and
        // their Mounts button goes back to it. Ride from the journal over the options closes every window into the game.
        internal static IEnumerator WayBack()
        {
            MountSettingsPage page = PageWithOptions();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page with options is shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
            if (settings == null)
            {
                SelfTest.Skip("no Settings window");
                yield break;
            }
            MountSettingsPage.Row mounts = page.HasMountRow ? RowWhere(page, r => r.OpensPanel) : null;
            try
            {
                // 1. the Settings window's button
                settings.onClosed = null;
                settings.Open(null);
                yield return SelfTest.Frames(2);
                UIDialogWindowButton button = SettingsButtons.ButtonOf(settings, page);
                string text = SettingsButtons.TextOf(button);
                SelfTest.Check(button != null && button.gameObject.activeInHierarchy && text == page.Title + " options", $"the Settings window has the button \"{text}\"");
                if (button == null) yield break;
                button.LazyButton.onClick.Invoke();
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown && LazyWindowsStackController.IsWindowOpened(settings) && !settings.gameObject.activeSelf,
                    "pressed: the options open over the Settings window, which waits underneath, still open, switched off");

                // 2. the Mounts button and back
                if (mounts == null) SelfTest.Info("the page has no Mounts button: that part skipped");
                else
                {
                    OptionsWindow.TestShowSection(ShownName(mounts));
                    yield return SelfTest.Frames(1);
                    OptionsWindow.TestPress(mounts);
                    yield return SelfTest.Frames(2);
                    SelfTest.Check(MountsPanel.IsOpen && MountsPanel.IsOverOptions && OptionsWindow.IsOpen && !OptionsWindow.IsShown && !settings.gameObject.activeSelf,
                        "the Mounts button: the journal opens over the options, which wait under it");
                    MountsPanel.TestStepBack();
                    yield return SelfTest.Frames(2);
                    SelfTest.Check(!MountsPanel.IsOpen && OptionsWindow.IsShown && !settings.gameObject.activeSelf, "a step back from the journal: the options again");
                }
                OptionsWindow.TestStepBack();
                yield return SelfTest.Frames(2);
                SelfTest.Check(!OptionsWindow.IsOpen && LazyWindowsStackController.ActiveWindow == settings && settings.gameObject.activeSelf,
                    "a step back from the options: the Settings window shown again, on top");
                settings.Close();
                yield return SelfTest.Frames(2);

                if (mounts == null) yield break;
                // 3. the journal's Options button and the options' Mounts button back to it
                MountsPanel.Open(page, toggle: false);
                yield return SelfTest.Frames(2);
                OptionsWindow.Open(page); // what the journal's Options button does
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown && OptionsWindow.IsOverPanel && !MountsPanel.IsOpen, "the journal's Options button: the options open over the journal, which waits under them");
                OptionsWindow.TestShowSection(ShownName(mounts));
                yield return SelfTest.Frames(1);
                OptionsWindow.TestPress(mounts);
                yield return SelfTest.Frames(2);
                SelfTest.Check(!OptionsWindow.IsOpen && MountsPanel.IsOpen && MountsPanel.IsShownOnTop, "their Mounts button: back to the journal under them");
                MountsPanel.TestStepBack();
                yield return SelfTest.Frames(2);
                SelfTest.Check(!MountsPanel.IsOpen && LazyWindowsStackController.ActiveWindow == null, "a step back: into the game");

                // 4. Ride from the journal over the options over the Settings window: every window closed
                settings.onClosed = null;
                settings.Open(null);
                yield return SelfTest.Frames(2);
                OptionsWindow.Open(page);
                yield return SelfTest.Frames(2);
                OptionsWindow.TestShowSection(ShownName(mounts));
                yield return SelfTest.Frames(1);
                OptionsWindow.TestPress(mounts);
                yield return SelfTest.Frames(2);
                MountsPanel.TestClose(keep: true, commit: true, back: false); // as Ride and Drive the cart close it
                yield return SelfTest.Frames(2);
                SelfTest.Check(!MountsPanel.IsOpen && !OptionsWindow.IsOpen && !LazyWindowsStackController.IsWindowOpened(settings) && LazyWindowsStackController.ActiveWindow == null,
                    "Ride from the journal over the options: the options and the Settings window closed, straight into the game");
            }
            finally
            {
                if (MountsPanel.IsOpen || MountsPanel.IsOverOptions) MountsPanel.TestClose(keep: true, back: false);
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: false);
                try { if (LazyWindowsStackController.IsWindowOpened(settings)) { settings.gameObject.SetActive(true); settings.Close(); } } catch { }
            }
            yield return SelfTest.Frames(2);
            SelfTest.Check(LazyWindowsStackController.ActiveWindow == null && MainGame.PlayerController.IsControlEnabledByType(TakenControlType.ByUI), "cleaned up: no window, the controls free");
        }
    }
}
