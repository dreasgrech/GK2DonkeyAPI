using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // A mod's options in a window of our own (docs/options-window.md; the user, 2026-10-30: GK2 Mod Framework isn't on the
    // Steam Workshop, so every player gets the same window). A settings page's rows that aren't the Mounts panel's
    // (MountSettingsPage.InOptions): its sections as cards on the left, the chosen section's rows on the right, one line at
    // the bottom saying what the row under the mouse or the gamepad's focus does, Defaults (the section shown, asked
    // first) and Done. Opened from its button in the game's Settings window (SettingsButtons) or the Mounts panel's Options
    // button; at the main menu too, the rows that need a game greyed there.
    //
    // Every row is our own control (RowControls; the user, 2026-10-30: "no compromise on controls ... we write our own"):
    // a switch, number buttons or a slider, a dropdown, a text box, a key box, a button, an info line, a heading. Changes
    // apply at once (the entry is set, its apply runs, BepInEx writes the cfg).
    //
    // The way back: the game window it was opened from waits underneath (GameWindowAside), or the journal when it was
    // opened from there; its Mounts button opens the journal over it, which goes back to it. Escape steps back one level.
    internal static class OptionsWindow
    {
        private const float W = 900f, H = 600f, Pad = 20f, Gap = 8f, ListW = 210f, CardH = 38f, CtrlW = 220f;
        private const float TitleH = 11f + 26f + 6f - 20f, ButtonsH = 40f, ButtonW = 130f, ButtonH = 34f, TitleRowH = 32f; // as the Mounts panel's
        private const float RefreshEvery = 0.5f;
        private const string Who = "the options window";

        private static readonly UiKit ui = new UiKit("GK2DonkeyAPI options ");
        private static readonly RowControls controls = new RowControls(ui, () => window, Refresh, PressRow);

        private static GameObject window, confirm;
        private static MountSettingsPage page;
        private static bool overPanel;      // opened from the journal: the journal waits under it
        private static bool openedInGame;   // it goes when the game does
        private static string section;      // the section shown (its cfg name)
        private static RectTransform listContent, rowsRoot;
        private static ScrollRect listScroll, rowsScroll;
        private static TextMeshProUGUI heading, description;
        private static string signature;
        private static float nextRefresh;
        private static readonly List<(string Section, string Name, Image Face, TextMeshProUGUI Label)> cards = new List<(string, string, Image, TextMeshProUGUI)>();
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> rows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();

        internal static bool IsOpen => window != null;
        // The one shown (not under the journal).
        internal static bool IsShown => window != null && window.activeSelf;
        // Opened from the journal, which waits under it.
        internal static bool IsOverPanel => window != null && overPanel;
        // A text box of the window has the cursor (the mods' keys wait).
        internal static bool Typing => window != null && controls.TypingField() != null;

        internal static bool HasOptions(MountSettingsPage p)
        {
            if (p == null) return false;
            foreach (MountSettingsPage.Row r in p.Rows) if (p.InOptions(r)) return true;
            return false;
        }

        // The sections, in the page's order: (cfg name, shown name).
        private static List<(string Section, string Name)> Sections(MountSettingsPage p)
        {
            var list = new List<(string, string)>();
            foreach (MountSettingsPage.Row r in p.Rows)
            {
                if (!p.InOptions(r)) continue;
                bool known = false;
                foreach ((string s, string _) in list) if (s == r.Section) { known = true; break; }
                if (!known) list.Add((r.Section, r.ShownSection ?? r.Section));
            }
            return list;
        }

        // ---- opening and closing ----

        // The options of a page: from its button in the game's Settings window, or the journal's Options button.
        internal static void Open(MountSettingsPage p)
        {
            try
            {
                if (p == null || window != null) return;
                if (MountsPanel.IsColoursOpen) return; // only the journal has an Options button: from the colours window the options would leave it live under them
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                if (settings == null) throw new InvalidOperationException("the game's Settings window isn't there to copy from");
                page = p;
                overPanel = MountsPanel.IsShownOnTop;
                openedInGame = Mount.IsGameReady;
                ui.Read(settings);
                Build();
                if (overPanel) MountsPanel.HideUnderOptions(); // the journal waits underneath
                else GameWindowAside.SetAside(Who); // the game window it was opened from (the Settings window) waits underneath, as it is
                GameWindowAside.TakeControls(Who);
                Plugin.Log.LogInfo($"Settings: the options of {p.Title} opened over {(overPanel ? "the Mounts panel" : GameWindowAside.UnderText)} ({cards.Count} sections, {rows.Count} rows in the first{(window.transform.localScale.x < 1f ? $"; scaled to {window.transform.localScale.x:0.##} to fit the screen" : "")}).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the options window can't open (" + e.Message + "); the options are still in the cfg (BepInEx\\config).");
                Close(back: true);
            }
        }

        // back: to where it was opened from, as it was. Not back (the game left): only the windows go.
        private static void Close(bool back)
        {
            bool was = window != null;
            bool fromPanel = overPanel;
            string title = page?.Title;
            Teardown();
            if (!was) return;
            if (fromPanel)
            {
                if (back)
                {
                    MountsPanel.ShowAfterOptions(); // the journal again, as it was
                    Plugin.Log.LogInfo($"Settings: the options of {title} closed: back to the Mounts panel.");
                }
                return;
            }
            if (Mount.IsGameReady || !openedInGame) GameWindowAside.Restore(back, $"the options of {title}");
            else GameWindowAside.Forget(); // the game is gone, and its windows with it
            GameWindowAside.ReleaseControls(Who);
        }

        // Closed by the Mounts panel (Ride from the journal over it, the game left): gone, nothing shown again; the panel
        // closes the rest.
        internal static void Discard() => Teardown();

        private static void Teardown()
        {
            if (KeyCapture.Row != null) KeyCapture.Cancel();
            try { controls.CommitFields(); } catch (Exception e) { Plugin.Log.LogWarning("Settings: a text being typed could not be kept: " + e.Message); }
            controls.CloseDropdown(refocus: false);
            if (window != null)
            {
                UnityEngine.Object.Destroy(window);
                window = null;
            }
            confirm = null; // went with the window
            rows.Clear();
            cards.Clear();
            listContent = rowsRoot = null;
            listScroll = rowsScroll = null;
            heading = description = null;
            signature = null;
            section = null;
            page = null;
            overPanel = false;
            described = null;
        }

        // The journal opened over the options (their Mounts button), and closed back to them.
        internal static void HideUnderPanel()
        {
            if (window != null) window.SetActive(false);
        }

        internal static void ShowAfterPanel()
        {
            if (window == null) return;
            window.SetActive(true);
            controls.NavDirty = true;
            Refresh();
        }

        private static void Build()
        {
            window = ui.Window("options window", W, H, 30002);
            ui.TitleBar(window, W, page.Title, () => Close(back: true));
            float top = Pad + TitleH + Gap, bandTop = H - Pad - ButtonsH;

            // the sections, as the journal's cards
            listScroll = ui.ScrollView(window.transform, "sections", Pad, top, ListW, bandTop - Gap - top, out listContent, 4f);
            ui.AddScrollbar(listScroll);
            cards.Clear();
            foreach ((string sec, string name) in Sections(page))
            {
                string s = sec;
                GameObject go = ui.OwnButton(listContent, "section " + name, name, ListW - 8f - UiKit.ScrollbarW, CardH, () => ShowSection(s), textLeft: 14f, flat: true);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = ListW - 8f - UiKit.ScrollbarW;
                le.preferredHeight = CardH;
                le.minHeight = CardH;
                UiKit.NavGroup(go, 0);
                cards.Add((sec, name, go.GetComponent<Image>(), go.GetComponentInChildren<TextMeshProUGUI>(true)));
            }

            // the chosen section: its name over a rule (as the journal's stats card's title), its rows under it
            float x0 = Pad + ListW + Gap, rw = W - Pad - x0;
            heading = ui.Bold(ui.Text(window.transform, "section title", "", 1.1f, TextAlignmentOptions.BottomLeft, false, x0 + 12f, top, rw - 24f, TitleRowH - 6f));
            GameObject rule = ui.Node("section rule", window.transform, typeof(Image));
            UiKit.Rect(rule, x0 + 12f, top + TitleRowH - 2f, rw - 24f, 1f);
            rule.GetComponent<Image>().color = UiKit.EdgeColor;
            rule.GetComponent<Image>().raycastTarget = false;
            float rowsTop = top + TitleRowH + 4f;
            rowsScroll = ui.ScrollView(window.transform, "rows", x0, rowsTop, rw, bandTop - Gap - rowsTop, out rowsRoot, 2f, manual: true);
            ui.AddScrollbar(rowsScroll);

            // the bottom: what the row under the mouse does, Defaults (the section shown, asked first), Done
            float by = bandTop + (ButtonsH - ButtonH) / 2f;
            GameObject done = ui.OwnButton(window.transform, "done", "Done", ButtonW, ButtonH, () => Close(back: true));
            UiKit.Rect(done, W - Pad - ButtonW, by, ButtonW, ButtonH);
            GameObject defaults = ui.OwnButton(window.transform, "defaults", "Defaults", ButtonW, ButtonH, OpenConfirm);
            UiKit.Rect(defaults, W - Pad - 2f * ButtonW - 12f, by, ButtonW, ButtonH);
            UiKit.NavGroup(done, 2);
            UiKit.NavGroup(defaults, 2);
            float descW = W - 2f * Pad - 2f * ButtonW - 12f - 16f;
            description = ui.Text(window.transform, "description", "", 0.85f, TextAlignmentOptions.Left, true, Pad + 4f, bandTop, descW, ButtonsH);
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.enableAutoSizing = true; // two lines; a long one shrinks a little before it is cut
            description.fontSizeMax = description.fontSize;
            description.fontSizeMin = description.fontSize * 0.75f;
            description.color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.85f);

            var nav = window.GetComponent<GamepadNavigationController>();
            UiKit.Hand(nav, 0, GUIDirection.Right, 1); UiKit.Hand(nav, 0, GUIDirection.Down, 2);  // the sections -> the rows, the buttons
            UiKit.Hand(nav, 1, GUIDirection.Left, 0); UiKit.Hand(nav, 1, GUIDirection.Down, 2);   // the rows -> the sections, the buttons
            UiKit.Hand(nav, 2, GUIDirection.Up, 1); UiKit.Hand(nav, 2, GUIDirection.Left, 0);     // the buttons -> the rows, the sections
            UiKit.Hand(nav, 1, GUIDirection.Up, 0);

            window.SetActive(true); // before the rows: a layout under an inactive root computes nothing
            ShowSection(cards.Count > 0 ? cards[0].Section : null);
            UiKit.FitToScreen(window, W, H);
        }

        // A section's rows on the right; its card marked.
        private static void ShowSection(string sec)
        {
            if (window == null || sec == null) return;
            if (KeyCapture.Row != null) KeyCapture.Cancel();
            section = sec;
            foreach ((string s, string name, Image face, TextMeshProUGUI label) in cards)
            {
                bool on = s == sec;
                if (face != null) face.color = on ? UiKit.CardChosen : UiKit.CardBase;
                if (label != null)
                {
                    label.color = on ? ui.ButtonTextColor : ui.FontColor;
                    label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
                if (on && heading != null) heading.text = name;
            }
            BuildRows();
            if (rowsScroll != null) rowsScroll.verticalNormalizedPosition = 1f;
        }

        // The rows of the shown section that show now (a row hidden by its own condition is left out; one greyed by its
        // rule is drawn greyed). Built again when what shows changes (Signature).
        private static void BuildRows()
        {
            if (rowsRoot == null || page == null) return;
            controls.CloseDropdown(refocus: false); // its button goes with the rows
            foreach (Transform c in rowsRoot) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            rows.Clear();
            float areaW = rowsScroll.viewport.rect.width > 0f ? rowsScroll.viewport.rect.width : W - Pad - (Pad + ListW + Gap);
            float colW = areaW - UiKit.ScrollbarW - 4f;
            var lay = new RowLayout { LabelX = 12f, CtrlRight = colW - 12f, CtrlLeft = colW - 12f - CtrlW, ColW = colW };
            float y = 4f;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Section != section || !page.InOptions(r)) continue;
                if (r.Visible != null && !r.Visible()) continue;
                GameObject go;
                Action refresh = null;
                float h;
                if (r.RowKind == MountSettingsPage.RowKind.Note) go = NoteRow(rowsRoot, r, lay, out h);
                else
                {
                    go = controls.OwnRow(rowsRoot, r, lay, out refresh, out h);
                    if (go == null)
                    {
                        Plugin.Log.LogInfo($"Settings: {page.Title}'s option '{r.Label}' has nothing to choose from; left out.");
                        continue;
                    }
                }
                UiKit.Place(go, 0f, y);
                y += h + 4f;
                CanvasGroup grp = r.RowKind != MountSettingsPage.RowKind.Note ? go.AddComponent<CanvasGroup>() : null;
                rows.Add((r, go, refresh, grp));
            }
            rowsRoot.sizeDelta = new Vector2(0f, y + 4f);
            foreach (GamepadNavigationItem ni in rowsRoot.GetComponentsInChildren<GamepadNavigationItem>(true)) ni.group = 1;
            signature = Signature();
            controls.NavDirty = true;
            RefreshRows();
        }

        // A heading inside a section (its label, bold, a faint line under it), or a note's sentence across the column.
        private static GameObject NoteRow(Transform parent, MountSettingsPage.Row r, RowLayout lay, out float height)
        {
            GameObject go = ui.Node("note " + (r.Label ?? ""), parent);
            bool isHeading = string.IsNullOrEmpty(r.Description);
            float w = lay.CtrlRight - lay.LabelX;
            string text = isHeading ? r.Label ?? "" : r.Description;
            TextMeshProUGUI t = ui.Text(go.transform, "label", text, isHeading ? 1f : 0.9f, isHeading ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Left, !isHeading, lay.LabelX, 0f, w, RowControls.OwnRowH);
            if (isHeading) ui.Bold(t);
            else t.color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.8f);
            height = isHeading ? RowControls.OwnRowH + 4f : Mathf.Max(RowControls.OwnRowH, Mathf.Ceil(t.GetPreferredValues(text, w, 0f).y) + 8f);
            UiKit.Rect(go, 0f, 0f, lay.ColW, height);
            UiKit.Rect(t.gameObject, lay.LabelX, 0f, w, isHeading ? height - 6f : height);
            if (isHeading)
            {
                GameObject line = ui.Node("heading line", go.transform, typeof(Image));
                UiKit.Rect(line, lay.LabelX, height - 4f, w, 1f);
                line.GetComponent<Image>().color = UiKit.EdgeColor;
                line.GetComponent<Image>().raycastTarget = false;
            }
            return go;
        }

        // What shows in the section now: each row shown or not, and each key box's clash (its line under the label).
        private static string Signature()
        {
            if (page == null) return "";
            var sig = new StringBuilder();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Section != section || !page.InOptions(r)) continue;
                sig.Append(r.Visible == null || r.Visible() ? '1' : '0');
                if (r.RowKind == MountSettingsPage.RowKind.Key) sig.Append(KeyCapture.Clash(r) ?? "").Append('|');
            }
            return sig.ToString();
        }

        // An option usable now: its own rule (EnableWhen), and a button only in a game (it acts on the keeper or the
        // mount: the Mounts panel, the safe spot).
        internal static bool Usable(MountSettingsPage.Row r)
        {
            if (r.Enabled != null && !r.Enabled()) return false;
            if (r.RowKind == MountSettingsPage.RowKind.Button && !Mount.IsGameReady) return false;
            return true;
        }

        // After a change (a control, a key set, Defaults) and twice a second: what shows built again when it changed,
        // else every row's value and greying.
        private static void Refresh()
        {
            if (window == null) return;
            if (Signature() != signature) BuildRows();
            else RefreshRows();
        }

        private static void RefreshRows()
        {
            foreach (var (row, go, refresh, group) in rows)
            {
                refresh?.Invoke();
                if (group == null) continue;
                bool on = Usable(row);
                if (group.interactable == on && Mathf.Approximately(group.alpha, on ? 1f : 0.4f)) continue;
                group.alpha = on ? 1f : 0.4f;
                group.interactable = on;
                group.blocksRaycasts = on;
                foreach (GamepadNavigationItem n in go.GetComponentsInChildren<GamepadNavigationItem>(true)) n.enabled = on;
                controls.NavDirty = true;
            }
        }

        // A row's button pressed: its action. The Mounts panel's opens the journal over the options; from the journal
        // under them, it goes back to it.
        private static void PressRow(MountSettingsPage.Row r)
        {
            if (r == null || !Usable(r)) return;
            if (r.OpensPanel && overPanel)
            {
                Close(back: true);
                return;
            }
            try
            {
                r.OnClick?.Invoke();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {r.Label} failed: {e.Message}");
            }
        }

        // ---- Defaults: the section shown back to its defaults, asked first ----

        private static void OpenConfirm()
        {
            if (window == null || confirm != null || section == null) return;
            if (KeyCapture.Row != null) KeyCapture.Cancel();
            controls.CloseDropdown(refocus: false);
            string name = heading != null ? heading.text : section;
            confirm = ui.Node("confirm", window.transform, typeof(Image));
            UiKit.Stretch((RectTransform)confirm.transform);
            confirm.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // takes the clicks: nothing under it is live
            const float bw = 460f, bh = 150f;
            RectTransform box = ui.Box(confirm.transform, "confirm box", (W - bw) / 2f, (H - bh) / 2f, bw, bh);
            box.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            ui.Text(box, "confirm text", $"Set every option in {name} back to its default?", 1f, TextAlignmentOptions.Center, true, 20f, 18f, bw - 40f, 60f);
            GameObject yes = ui.OwnButton(box, "confirm yes", "Set back", ButtonW, ButtonH, () => { CloseConfirm(); ResetSection(); });
            UiKit.Rect(yes, bw / 2f - ButtonW - 6f, bh - 20f - ButtonH, ButtonW, ButtonH);
            GameObject no = ui.OwnButton(box, "confirm no", "Cancel", ButtonW, ButtonH, CloseConfirm);
            UiKit.Rect(no, bw / 2f + 6f, bh - 20f - ButtonH, ButtonW, ButtonH);
            UiKit.NavGroup(yes, 4);
            UiKit.NavGroup(no, 4); // on their own while it is open: no way out of group 4 but Select and Back
            controls.FocusNext = no.GetComponent<GamepadNavigationItem>();
            controls.NavDirty = true;
        }

        private static void CloseConfirm()
        {
            if (confirm == null) return;
            confirm.SetActive(false);
            UnityEngine.Object.Destroy(confirm);
            confirm = null;
            controls.NavDirty = true;
        }

        // Every option of the shown section to the value its mod gave it: set through its entry, so its apply runs.
        private static void ResetSection()
        {
            if (page == null || section == null) return;
            int changed = 0;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Section != section || !page.InOptions(r) || r.Entry == null) continue;
                try
                {
                    if (!Equals(r.Entry.BoxedValue, r.Entry.DefaultValue))
                    {
                        r.Entry.BoxedValue = r.Entry.DefaultValue; // its SettingChanged applies it (MountSettingsPage.Wire)
                        changed++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Settings: {r.Label} could not be set back: {e.Message}");
                }
            }
            Plugin.Log.LogInfo($"Settings: {page.Title}'s {heading?.text ?? section} options set back to their defaults ({changed} changed).");
            Refresh();
        }

        // ---- every frame (Plugin.LateUpdate, after KeyCapture, before the mods' keys) ----

        internal static void Tick()
        {
            if (window == null) return;
            if (openedInGame && !Mount.IsGameReady)
            {
                // the game left (a load, the main menu): nothing to go back to
                if (overPanel) Discard(); // the journal under it closes itself and the rest
                else Close(back: false);
                return;
            }
            if (!window.activeSelf) return; // the journal over it: the panel's tick
            controls.TrackTyping();
            Wheel();
            if (GameWindowAside.ControlsTaken) GameWindowAside.TakeControls(Who); // re-asserted: a game popup closing gives ByUI back
            controls.TickGamepad(window, FocusStart);
            if (GameWindowAside.TakeEscape())
            {
                StepBack();
                return;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshEvery;
                try
                {
                    Refresh(); // a value changed elsewhere (the cfg, a key), a rule's condition (a game loaded, flying switched)
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Settings: the options window could not follow a change (" + e.Message + "); closed.");
                    Close(back: true);
                    return;
                }
            }
            Describe();
        }

        // One step back: out of a text box, a dropdown's list closed, the Defaults question closed, else the window closed.
        private static void StepBack()
        {
            if (controls.LeaveTyping()) return;
            if (controls.DropdownOpen) controls.CloseDropdown(refocus: true);
            else if (confirm != null) CloseConfirm();
            else Close(back: true);
        }

        // The first focus: the shown section's card.
        private static void FocusStart(GamepadNavigationController nav)
        {
            GamepadNavigationItem start = null;
            foreach ((string s, string _, Image face, TextMeshProUGUI _) in cards)
            {
                if (s == section && face != null) { start = face.GetComponent<GamepadNavigationItem>(); break; }
            }
            if (start != null) nav.SetFocusedItem(start); else nav.FocusOnFirstActive(0);
            UiKit.KeepInView(nav.FocusedItem);
        }

        // The wheel anywhere on the window: the sections over the sections, else the rows; an open list alone.
        private static void Wheel()
        {
            float d = UnityInput.Current.mouseScrollDelta.y;
            if (Mathf.Approximately(d, 0f) || confirm != null) return;
            Vector2 at = UnityInput.Current.mousePosition;
            ScrollRect target = null;
            if (controls.DropdownOpen) { if (controls.DropScroll != null && UiKit.Over(controls.DropScroll.gameObject, at)) target = controls.DropScroll; }
            else if (UiKit.Over(window, at)) target = listScroll != null && UiKit.Over(listScroll.gameObject, at) ? listScroll : rowsScroll;
            UiKit.Scroll(target, d);
        }

        // ---- the line at the bottom: what the row under the mouse (or the gamepad's focus) does ----

        private static MountSettingsPage.Row described;
        private static bool describedUsable, describedSetting, describedPad;

        private static void Describe()
        {
            if (description == null) return;
            MountSettingsPage.Row r = KeyCapture.Row;
            bool pad = LazyInput.IsGamepadActive;
            if (r == null && confirm == null && !controls.DropdownOpen)
            {
                if (pad)
                {
                    GamepadNavigationItem f = window.GetComponent<GamepadNavigationController>()?.FocusedItem;
                    if (f != null) foreach (var x in rows) if (x.Go != null && f.transform.IsChildOf(x.Go.transform)) { r = x.Row; break; }
                }
                else
                {
                    Vector2 at = UnityInput.Current.mousePosition;
                    if (rowsScroll != null && UiKit.Over(rowsScroll.gameObject, at))
                        foreach (var x in rows) if (x.Go != null && x.Row.RowKind != MountSettingsPage.RowKind.Note && UiKit.Over(x.Go, at)) { r = x.Row; break; }
                }
            }
            bool usable = r != null && Usable(r);
            bool setting = r != null && r == KeyCapture.Row;
            if (r == described && usable == describedUsable && setting == describedSetting && pad == describedPad && description.text.Length > 0) return;
            described = r;
            describedUsable = usable;
            describedSetting = setting;
            describedPad = pad;
            description.text = DescriptionOf(r, usable, setting, pad);
        }

        internal static string DescriptionOf(MountSettingsPage.Row r, bool usable, bool setting, bool gamepad)
        {
            if (r == null) return gamepad ? "Move to an option to see what it does." : "Point at an option to see what it does.";
            if (setting) return $"Press the key for {r.Label}: Shift, Ctrl or Alt on its own, or with another key. Backspace: no key. Esc: cancel.";
            string d = r.Description ?? "";
            if (r.RowKind == MountSettingsPage.RowKind.Key && usable) d += (d.Length > 0 ? " " : "") + (gamepad ? "Select it, then press the key on the keyboard." : "Click it to set the key.");
            if (!usable) d += (d.Length > 0 ? " " : "") + (r.RowKind == MountSettingsPage.RowKind.Button && !Mount.IsGameReady ? "Only in a game." : "Not available right now.");
            return d;
        }

        // ---- the self-tests' view ----

        internal static int TestRowCount => rows.Count;
        internal static string TestSection => section;
        internal static string TestSections()
        {
            var names = new List<string>();
            foreach (var c in cards) names.Add(c.Name);
            return string.Join(", ", names);
        }
        internal static bool TestShowSection(string shownName)
        {
            foreach (var c in cards) if (c.Name == shownName) { ShowSection(c.Section); return true; }
            return false;
        }
        internal static List<MountSettingsPage.Row> TestRows()
        {
            var list = new List<MountSettingsPage.Row>();
            foreach (var x in rows) list.Add(x.Row);
            return list;
        }
        internal static GameObject TestRowObject(MountSettingsPage.Row r)
        {
            foreach (var x in rows) if (x.Row == r) return x.Go;
            return null;
        }
        internal static bool TestRowGreyed(MountSettingsPage.Row r)
        {
            foreach (var x in rows) if (x.Row == r) return x.Group != null && !x.Group.interactable;
            return false;
        }
        // Rows drawn with a game row (a UISwitchButton): none, every control is our own.
        internal static int TestGameRows()
        {
            int n = 0;
            foreach (var x in rows) if (x.Go != null && x.Go.GetComponentInChildren<UISwitchButton>(true) != null) n++;
            return n;
        }
        internal static bool TestSetKey(MountSettingsPage.Row r, BepInEx.Configuration.KeyboardShortcut set) => KeyCapture.TestSet(r, window, Refresh, set);
        internal static void TestStartKey(MountSettingsPage.Row r) => KeyCapture.Start(r, window, Refresh);
        internal static string TestKeyBoxText(MountSettingsPage.Row r)
        {
            GameObject go = TestRowObject(r);
            Transform box = go != null ? go.transform.Find("GK2DonkeyAPI options key") : null;
            TextMeshProUGUI t = box != null ? box.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            return t != null ? t.text : null;
        }
        internal static string TestClashLine(MountSettingsPage.Row r)
        {
            GameObject go = TestRowObject(r);
            Transform t = go != null ? go.transform.Find("GK2DonkeyAPI options clash") : null;
            return t != null ? t.GetComponent<TextMeshProUGUI>()?.text : null;
        }
        internal static void TestPress(MountSettingsPage.Row r) => PressRow(r);
        internal static void TestOpenConfirm() => OpenConfirm();
        internal static bool TestConfirmOpen => confirm != null;
        internal static void TestConfirmYes() { CloseConfirm(); ResetSection(); }
        internal static void TestStepBack() => StepBack();
        internal static void TestClose(bool back) => Close(back);
        internal static void TestRefresh() => Refresh();
        internal static string TestDescription(MountSettingsPage.Row r) => DescriptionOf(r, r == null || Usable(r), false, false);
        internal static float TestScale => window != null ? window.transform.localScale.x : 0f;
        // The first part of a row drawn outside the rows' column (its mask would cut it), or null.
        internal static string TestOutside()
        {
            if (rowsScroll == null || rowsScroll.viewport == null) return "no options window";
            RectTransform view = rowsScroll.viewport;
            Rect area = view.rect;
            var corners = new Vector3[4];
            foreach (var x in rows)
            {
                if (x.Go == null) continue;
                foreach (RectTransform rt in x.Go.GetComponentsInChildren<RectTransform>(false))
                {
                    if (rt.rect.width <= 0f) continue;
                    rt.GetWorldCorners(corners);
                    float a = view.InverseTransformPoint(corners[0]).x, b = view.InverseTransformPoint(corners[2]).x;
                    if (Math.Min(a, b) < area.xMin - 0.5f || Math.Max(a, b) > area.xMax + 0.5f)
                        return $"{x.Row.Label}: {rt.name} spans {Math.Min(a, b) - area.xMin:0} to {Math.Max(a, b) - area.xMin:0} of 0 to {area.width:0}";
                }
            }
            return null;
        }
    }
}
