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
    // Where a column's rows put their parts: the label's left, the wide controls' span, the column's width.
    internal struct RowLayout
    {
        internal float LabelX, CtrlLeft, CtrlRight, ColW;
    }

    // The rows' controls of our windows (the Mounts panel, the options window), in place of the game's "< value >" rows
    // (the user, 2026-10-26: the arrows are "way too limiting... not scalable"; the user's pick "mixed by kind"; 2026-10-30:
    // "no compromise on controls", every kind our own): on/off a switch, a small range (the babies, 0-5) a row of number
    // buttons, a large one (a volume) a slider with its value, a list (the skin, a colour preset) a dropdown opening a
    // scrolling list, a text (the name) a text box, a key a key box (KeyCapture), a button the window's button, an
    // info line its value. One row: its label left on the column's label edge (wrapping onto a second line when long),
    // its control ending on the column's value edge, nothing outside the column.
    // One per window: it holds that window's open dropdown, its text boxes, its sliders' gamepad steps and its gamepad
    // focus. The window gives it where a dropdown's list opens (the window shown), what to do after a value changed, and
    // what a button row does.
    internal sealed class RowControls
    {
        internal const float OwnRowH = 30f, CtrlH = 24f, SwitchW = 48f, SwitchH = 24f, NumberGap = 3f, ValueW = 46f, ChoiceH = 26f;
        internal const float KeyW = 170f, ButtonRowW = 150f;
        private const int MaxNumberButtons = 8;  // a range up to this many values as buttons; more: a slider
        private const int DropShown = 7;         // a dropdown's list shows this many choices, then scrolls
        internal const int NameChars = 24;
        private static readonly Color KnobOff = new Color(0.45f, 0.45f, 0.47f, 1f);
        internal static readonly Color ClashColor = new Color(0.95f, 0.62f, 0.35f, 1f); // a key another row has too

        private readonly UiKit ui;
        private readonly Func<GameObject> window;              // the window a dropdown's list opens over: the one shown
        private readonly Action changed;                       // a control set a value: the window follows
        private readonly Action<MountSettingsPage.Row> press;  // a button row pressed

        private GameObject dropdown;              // a dropdown's open list (over the whole window: a click outside closes it)
        private ScrollRect dropScroll;
        private GamepadNavigationItem dropFrom;   // the dropdown it opened from: the focus goes back to it
        private readonly List<TMP_InputField> fields = new List<TMP_InputField>(); // the text boxes (the search box and the rows')
        private readonly Dictionary<GamepadNavigationItem, Action<int>> steppers = new Dictionary<GamepadNavigationItem, Action<int>>(); // a slider steps with Left/Right
        private TMP_InputField typingIn;
        private int typingFrame = -10;

        internal GamepadNavigationItem FocusNext;  // the gamepad's focus once the items are read again (TickGamepad)
        internal bool NavDirty = true;             // the window's items changed: read again

        internal RowControls(UiKit ui, Func<GameObject> window, Action changed, Action<MountSettingsPage.Row> press = null)
        {
            this.ui = ui;
            this.window = window;
            this.changed = changed;
            this.press = press;
        }

        internal bool DropdownOpen => dropdown != null;
        internal ScrollRect DropScroll => dropScroll;

        // The text box being typed in, if any (the mods' keys wait meanwhile: KeyActions).
        internal TMP_InputField TypingField()
        {
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i] == null) { fields.RemoveAt(i); continue; }
                if (fields[i].isFocused) return fields[i];
            }
            return null;
        }

        // Every frame: which box is being typed in (Escape ends the typing first: LeaveTyping).
        internal void TrackTyping()
        {
            TMP_InputField typing = TypingField();
            if (typing != null) { typingIn = typing; typingFrame = Time.frameCount; }
        }

        // Escape while typing (the field already put its text back): out of the box. False: nothing was being typed.
        internal bool LeaveTyping()
        {
            if (typingIn == null || typingFrame < Time.frameCount - 1) return false;
            typingIn.DeactivateInputField();
            return true;
        }

        // What was typed into a text box still being edited is kept when its window closes.
        internal void CommitFields()
        {
            TMP_InputField f = TypingField();
            if (f != null) f.DeactivateInputField(); // its onEndEdit writes it
        }

        // A row of our own for a row of a page, or null for a choice with nothing to choose from.
        internal GameObject OwnRow(Transform parent, MountSettingsPage.Row r, RowLayout lay, out Action refresh, out float height)
        {
            refresh = null;
            height = 0f;
            MountSettingsPage.RowKind kind = r.RowKind;
            bool valued = kind == MountSettingsPage.RowKind.Toggle || kind == MountSettingsPage.RowKind.Steps || kind == MountSettingsPage.RowKind.Choice
                || kind == MountSettingsPage.RowKind.List || kind == MountSettingsPage.RowKind.Text || kind == MountSettingsPage.RowKind.Key;
            bool other = kind == MountSettingsPage.RowKind.Button || kind == MountSettingsPage.RowKind.Info;
            if ((valued && r.Entry == null) || (!valued && !other)) return null;
            if (kind == MountSettingsPage.RowKind.List) MountSettingsPage.FillList(r);
            if ((kind == MountSettingsPage.RowKind.Choice || kind == MountSettingsPage.RowKind.List) && (r.ChoiceLabels == null || r.ChoiceValues == null)) return null;
            float ctrlW = kind == MountSettingsPage.RowKind.Toggle ? SwitchW
                : kind == MountSettingsPage.RowKind.Key ? Mathf.Min(KeyW, lay.CtrlRight - lay.CtrlLeft)
                : kind == MountSettingsPage.RowKind.Button ? Mathf.Min(ButtonRowW, lay.CtrlRight - lay.CtrlLeft)
                : lay.CtrlRight - lay.CtrlLeft;
            float ctrlLeft = lay.CtrlRight - ctrlW;
            float labelW = Mathf.Max(40f, ctrlLeft - 10f - lay.LabelX);
            GameObject go = ui.Node("row " + (r.Entry != null ? r.Entry.Definition.Key : r.Label ?? ""), parent);
            TextMeshProUGUI label = ui.Text(go.transform, "label", r.Label ?? "", 0.95f, TextAlignmentOptions.Left, true, lay.LabelX, 0f, labelW, OwnRowH);
            // as tall as its label needs (a long label wraps onto a second line), the control centred on it
            float labelH = Mathf.Ceil(label.GetPreferredValues(r.Label ?? "", labelW, 0f).y);
            // a key another row has too: said under the label (KeyCapture.Clash), the row as tall as both lines
            string clash = kind == MountSettingsPage.RowKind.Key ? KeyCapture.Clash(r) : null;
            TextMeshProUGUI clashLine = null;
            float clashH = 0f;
            if (clash != null)
            {
                clashLine = ui.Text(go.transform, "clash", clash, 0.8f, TextAlignmentOptions.TopLeft, true, lay.LabelX, 0f, labelW, OwnRowH);
                clashLine.color = ClashColor;
                clashH = Mathf.Ceil(clashLine.GetPreferredValues(clashLine.text, labelW, 0f).y);
            }
            height = Mathf.Max(OwnRowH, labelH + clashH + 8f);
            UiKit.Rect(go, 0f, 0f, lay.ColW, height);
            if (clashLine == null) UiKit.Rect(label.gameObject, lay.LabelX, 0f, labelW, height);
            else
            {
                float top = Mathf.Round((height - labelH - clashH) / 2f);
                label.alignment = TextAlignmentOptions.BottomLeft;
                UiKit.Rect(label.gameObject, lay.LabelX, top, labelW, labelH);
                UiKit.Rect(clashLine.gameObject, lay.LabelX, top + labelH, labelW, clashH);
            }
            float cy = Mathf.Round((height - CtrlH) / 2f);
            switch (kind)
            {
                case MountSettingsPage.RowKind.Toggle:
                    refresh = Switch(go.transform, r, ctrlLeft, Mathf.Round((height - SwitchH) / 2f));
                    break;
                case MountSettingsPage.RowKind.Steps:
                    int count = (r.Max - r.Min) / Math.Max(1, r.Step) + 1;
                    refresh = count <= MaxNumberButtons ? Numbers(go.transform, r, ctrlLeft, cy, ctrlW, count) : SliderRow(go.transform, r, ctrlLeft, height, ctrlW);
                    break;
                case MountSettingsPage.RowKind.Text:
                    refresh = TextRow(go.transform, r, ctrlLeft, cy, ctrlW);
                    break;
                case MountSettingsPage.RowKind.Key:
                    refresh = KeyBox(go.transform, r, ctrlLeft, cy, ctrlW);
                    break;
                case MountSettingsPage.RowKind.Button:
                    RowButton(go.transform, r, ctrlLeft, Mathf.Round((height - ChoiceH - 2f) / 2f), ctrlW);
                    break;
                case MountSettingsPage.RowKind.Info:
                    refresh = InfoValue(go.transform, r, ctrlLeft, height, ctrlW);
                    break;
                default:
                    refresh = Dropdown(go.transform, r, ctrlLeft, cy, ctrlW);
                    break;
            }
            refresh?.Invoke();
            return go;
        }

        // A value set from a control: its SettingChanged applies it (MountSettingsPage.Wire); the window follows (a preset
        // shows its custom colour, the picture its look).
        private void SetValue(MountSettingsPage.Row r, object v)
        {
            try
            {
                if (!Equals(r.Entry.BoxedValue, v)) r.Entry.BoxedValue = v;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {r.Label} could not be set: {e.Message}");
            }
            changed?.Invoke();
        }

        // The gamepad's stop on a control: Select does what a click does, framed while focused.
        private GamepadNavigationItem NavItem(GameObject go, Action select)
        {
            var item = go.GetComponent<GamepadNavigationItem>() ?? go.AddComponent<GamepadNavigationItem>();
            item.SetCallbacks(null, null, () => select());
            item.focusFrame = ui.FocusFrame(go.transform);
            return item;
        }

        private static void Hover(Selectable s, float bright)
        {
            ColorBlock cb = s.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(bright, bright, bright, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.05f;
            s.colors = cb;
            s.navigation = new Navigation { mode = Navigation.Mode.None }; // the gamepad's navigation is the window's (GamepadNavigationController)
        }

        // The game's dialog button face (our buttons' face), sliced; its red when there is none.
        private void Face(Image img)
        {
            if (ui.ButtonSprite != null)
            {
                img.sprite = ui.ButtonSprite;
                img.type = ui.ButtonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            else img.color = new Color(0.45f, 0.12f, 0.12f, 1f);
        }

        // The faint edge of our boxes.
        private static void Edge(GameObject go)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = UiKit.EdgeColor;
            o.effectDistance = new Vector2(1f, -1f);
        }

        // On/off: a switch, its knob left (off) or right (on, in the game's red).
        private Action Switch(Transform parent, MountSettingsPage.Row r, float x, float y)
        {
            GameObject sw = ui.Node("switch", parent, typeof(Image), typeof(UnityEngine.UI.Button));
            UiKit.Rect(sw, x, y, SwitchW, SwitchH);
            // our make (the user, 2026-10-26: the switches and the slider "need to match aesthetic more"): a dark box with
            // the faint edge (the stage's, the stats card's), the knob the game's own button face, red and on the right when
            // on, greyed on the left when off
            var track = sw.GetComponent<Image>();
            track.color = UiKit.BoxColor;
            Edge(sw);
            var b = sw.GetComponent<UnityEngine.UI.Button>();
            GameObject knob = ui.Node("knob", sw.transform, typeof(Image));
            var knobImg = knob.GetComponent<Image>();
            Face(knobImg);
            knobImg.raycastTarget = false;
            b.targetGraphic = knobImg;
            Hover(b, 1.25f);
            Action flip = () => SetValue(r, !(r.Entry.BoxedValue is bool on && on));
            b.onClick.AddListener(() => flip());
            NavItem(sw, flip);
            float kw = Mathf.Round(SwitchW / 2f);
            return () =>
            {
                bool on = r.Entry.BoxedValue is bool v && v;
                track.color = on ? new Color(UiKit.CardChosen.r, UiKit.CardChosen.g, UiKit.CardChosen.b, 0.55f) : UiKit.BoxColor;
                knobImg.color = on ? Color.white : KnobOff;
                UiKit.Rect(knob, on ? SwitchW - kw - 2f : 2f, 2f, kw, SwitchH - 4f);
            };
        }

        // A small range: a button per value in a row, the chosen one in the game's red.
        private Action Numbers(Transform parent, MountSettingsPage.Row r, float x, float y, float w, int count)
        {
            float bw = Mathf.Floor((w - (count - 1) * NumberGap) / count);
            float left = x + (w - (count * bw + (count - 1) * NumberGap)); // the rounding's slack on the left: the row ends on the value edge
            var buttons = new List<(int Value, Image Face, TextMeshProUGUI Label)>();
            for (int i = 0; i < count; i++)
            {
                int v = r.Min + i * r.Step;
                GameObject b = ui.OwnButton(parent, "value " + v, v.ToString(), bw, CtrlH, () => SetValue(r, v), flat: true);
                UiKit.Rect(b, left + i * (bw + NumberGap), y, bw, CtrlH);
                buttons.Add((v, b.GetComponent<Image>(), b.GetComponentInChildren<TextMeshProUGUI>(true)));
            }
            return () =>
            {
                int now = r.Entry.BoxedValue is int n ? n : r.Min;
                foreach ((int value, Image face, TextMeshProUGUI label) in buttons)
                {
                    bool on = value == now;
                    if (face != null) face.color = on ? UiKit.CardChosen : UiKit.CardBase;
                    if (label != null)
                    {
                        label.color = on ? ui.ButtonTextColor : ui.FontColor;
                        label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                    }
                }
            };
        }

        // A large range: a slider, its value written beside it (the row's own words: "80%").
        private Action SliderRow(Transform parent, MountSettingsPage.Row r, float x, float rowH, float w)
        {
            int steps = (r.Max - r.Min) / Math.Max(1, r.Step);
            TextMeshProUGUI value = ui.Text(parent, "value", "", 0.95f, TextAlignmentOptions.Right, false, x + w - ValueW, 0f, ValueW, rowH);
            value.color = ui.ButtonTextColor;
            // our make, as the switch: a dark box with the faint edge, filled in the journal's red up to the handle, the
            // handle the game's button face
            float sw = w - ValueW - 8f, sh = 22f, barH = 10f, hw = 14f;
            GameObject go = ui.Node("slider", parent, typeof(Image), typeof(Slider));
            UiKit.Rect(go, x, Mathf.Round((rowH - sh) / 2f), sw, sh);
            go.GetComponent<Image>().color = Color.clear; // the whole height takes the pointer, not only the track
            GameObject bar = ui.Node("track", go.transform, typeof(Image));
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(0f, barH);
            bar.GetComponent<Image>().color = UiKit.BoxColor;
            bar.GetComponent<Image>().raycastTarget = false;
            Edge(bar);
            GameObject fillArea = ui.Node("fill area", go.transform);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0f, 0.5f);
            fart.anchorMax = new Vector2(1f, 0.5f);
            // inside the track's edge on the left, ending under the handle's middle at any value
            fart.offsetMin = new Vector2(1f, -(barH - 2f) / 2f);
            fart.offsetMax = new Vector2(-hw / 2f, (barH - 2f) / 2f);
            GameObject fill = ui.Node("fill", fillArea.transform, typeof(Image));
            ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = UiKit.CardChosen;
            fill.GetComponent<Image>().raycastTarget = false;
            GameObject handleArea = ui.Node("handle area", go.transform);
            var hart = (RectTransform)handleArea.transform;
            UiKit.Stretch(hart);
            hart.offsetMin = new Vector2(hw / 2f, 0f);
            hart.offsetMax = new Vector2(-hw / 2f, 0f);
            GameObject handle = ui.Node("handle", handleArea.transform, typeof(Image));
            ((RectTransform)handle.transform).sizeDelta = new Vector2(hw, 0f);
            var himg = handle.GetComponent<Image>();
            Face(himg);
            var s = go.GetComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = (RectTransform)handle.transform;
            s.targetGraphic = himg;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = steps;
            s.wholeNumbers = true;
            Hover(s, 1.2f);
            bool syncing = false;
            string Words(int v) => r.Format != null ? r.Format(v) : v.ToString();
            s.onValueChanged.AddListener(f =>
            {
                int v = r.Min + Mathf.RoundToInt(f) * r.Step;
                value.text = Words(v);
                if (!syncing) SetValue(r, v);
            });
            GamepadNavigationItem item = NavItem(go, () => { });
            foreach (GamepadNavigationItem k in new List<GamepadNavigationItem>(steppers.Keys)) if (k == null) steppers.Remove(k);
            steppers[item] = d => s.value = Mathf.Clamp(s.value + d, 0f, steps); // Left/Right on the gamepad (TickGamepad)
            return () =>
            {
                int now = r.Entry.BoxedValue is int n ? n : r.Min;
                syncing = true;
                try { s.value = Mathf.Clamp((now - r.Min) / Math.Max(1, r.Step), 0, steps); }
                finally { syncing = false; }
                value.text = Words(now);
            };
        }

        // A text (the mount's name): a text box, written when the typing ends (Enter, a click elsewhere).
        private Action TextRow(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
        {
            TMP_InputField f = InputBox(parent, "text", x, y, w, CtrlH, "None", NameChars);
            f.text = r.Entry.BoxedValue as string ?? "";
            f.onEndEdit.AddListener(v =>
            {
                string t = (v ?? "").Trim();
                if (!string.Equals(t, r.Entry.BoxedValue as string ?? "", StringComparison.Ordinal)) SetValue(r, t);
            });
            NavItem(f.gameObject, () => f.ActivateInputField());
            return () =>
            {
                string now = r.Entry.BoxedValue as string ?? "";
                if (!f.isFocused && f.text != now) f.text = now;
            };
        }

        // A key: a box with the key in words ("H", "Ctrl + G", "Left Shift", "None"); a click (or Select) sets it, the
        // next key pressed being the key (KeyCapture), the box saying so meanwhile.
        private Action KeyBox(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
        {
            GameObject b = ui.OwnButton(parent, "key", "", w, CtrlH, () => KeyCapture.Start(r, window(), changed), flat: true);
            UiKit.Rect(b, x, y, w, CtrlH);
            Image face = b.GetComponent<Image>();
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            bool clash = KeyCapture.Clash(r) != null;
            return () =>
            {
                bool setting = KeyCapture.Row == r;
                if (face != null) face.color = setting ? UiKit.CardChosen : UiKit.CardBase;
                if (label == null) return;
                label.text = setting ? "Press a key..." : KeyCapture.Words((KeyboardShortcut)r.Entry.BoxedValue);
                label.color = setting ? ui.ButtonTextColor : clash ? ClashColor : ui.FontColor;
                label.fontStyle = setting ? FontStyles.Italic : FontStyles.Normal;
            };
        }

        // A button of the page's (the Mounts panel, the safe spot): the game's button face with its word.
        private void RowButton(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
        {
            GameObject b = ui.OwnButton(parent, "button", r.ButtonText ?? "Open", w, ChoiceH + 2f, () => press?.Invoke(r));
            UiKit.Rect(b, x, y, w, ChoiceH + 2f);
        }

        // An info line: its value as text on the value edge, read again on every refresh.
        private Action InfoValue(Transform parent, MountSettingsPage.Row r, float x, float rowH, float w)
        {
            TextMeshProUGUI value = ui.Text(parent, "value", "", 0.95f, TextAlignmentOptions.Right, true, x, 0f, w, rowH);
            value.color = ui.ButtonTextColor;
            return () =>
            {
                string s = r.InfoValue?.Invoke();
                value.text = string.IsNullOrEmpty(s) ? "-" : s;
            };
        }

        // A list: the chosen one's name on a button, a click opens the list under it.
        private Action Dropdown(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
        {
            GameObject b = null;
            b = ui.OwnButton(parent, "dropdown", "", w, CtrlH, () => OpenDropdown(r, b), textLeft: 8f, flat: true);
            UiKit.Rect(b, x, y, w, CtrlH);
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) UiKit.Rect(label.gameObject, 8f, 0f, w - 8f - 22f, CtrlH);
            Chevron(b.transform, w - 16f, CtrlH / 2f);
            return () =>
            {
                int at = ChoiceIndex(r);
                if (label != null) label.text = at >= 0 ? r.ChoiceLabels[at] : (r.Entry.BoxedValue?.ToString() ?? "");
            };
        }

        // A small "v" drawn from two bars (no glyph the game's font may lack).
        private void Chevron(Transform parent, float cx, float cy)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bar = ui.Node("chevron", parent, typeof(Image));
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(7f, 2f);
                rt.anchoredPosition = new Vector2(cx + side * 2.3f, -cy);
                rt.localRotation = Quaternion.Euler(0f, 0f, side * 45f); // the left bar down to the right, the right one up: a "v" (the other sign drew a "^", the user 2026-10-26)
                bar.GetComponent<Image>().color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.8f);
                bar.GetComponent<Image>().raycastTarget = false;
            }
        }

        private static int ChoiceIndex(MountSettingsPage.Row r)
        {
            if (r.ChoiceValues == null) return -1;
            object now = r.Entry?.BoxedValue;
            for (int i = 0; i < r.ChoiceValues.Length; i++)
            {
                object v = r.ChoiceValues.GetValue(i);
                if (Equals(v, now) || (v is string a && now is string s && a.Equals(s, StringComparison.OrdinalIgnoreCase))) return i;
            }
            return -1;
        }

        // The list of a dropdown, under its button (over it when there's no room below), the chosen one in the game's red,
        // scrolled to it; over everything of the window, a click outside it closes it.
        private void OpenDropdown(MountSettingsPage.Row r, GameObject from)
        {
            CloseDropdown(refocus: false);
            GameObject win = window();
            if (win == null || from == null || r.ChoiceLabels == null || r.ChoiceLabels.Length == 0) return;
            var wrt = (RectTransform)win.transform;
            dropdown = ui.Node("dropdown", win.transform, typeof(Image), typeof(UnityEngine.UI.Button));
            UiKit.Stretch((RectTransform)dropdown.transform);
            dropdown.GetComponent<Image>().color = Color.clear;
            dropdown.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => CloseDropdown(refocus: true));
            Hover(dropdown.GetComponent<UnityEngine.UI.Button>(), 1f);
            // where its button is, from the window's top left (the window may be scaled: FitToScreen)
            var corners = new Vector3[4];
            ((RectTransform)from.transform).GetWorldCorners(corners);
            Vector3 tl = wrt.InverseTransformPoint(corners[1]), br = wrt.InverseTransformPoint(corners[3]);
            float x = tl.x - wrt.rect.xMin, top = wrt.rect.yMax - tl.y, bottom = wrt.rect.yMax - br.y, w = br.x - tl.x;
            int n = r.ChoiceLabels.Length;
            float listH = Mathf.Min(n, DropShown) * (ChoiceH + 2f) + 6f;
            float y = bottom + 2f;
            if (y + listH > wrt.rect.height - 8f && top - 2f - listH >= 8f) y = top - 2f - listH;
            RectTransform box = ui.Box(dropdown.transform, "dropdown list", x, y, w, listH);
            box.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            box.GetComponent<Image>().raycastTarget = true;
            box.gameObject.AddComponent<UnityEngine.UI.Button>().transition = Selectable.Transition.None; // takes a click between its choices: it doesn't reach the backdrop and close the list
            dropScroll = ui.ScrollView(box, "dropdown scroll", 2f, 2f, w - 4f, listH - 4f, out RectTransform content, 2f);
            ui.AddScrollbar(dropScroll);
            int chosen = ChoiceIndex(r);
            GamepadNavigationItem chosenItem = null;
            for (int i = 0; i < n; i++)
            {
                object v = r.ChoiceValues.GetValue(i);
                float iw = w - 4f - UiKit.ScrollbarW - 10f;
                GameObject c = ui.OwnButton(content, "choice " + i, r.ChoiceLabels[i], iw, ChoiceH, () => { CloseDropdown(refocus: true); SetValue(r, v); }, textLeft: 8f, flat: true);
                var le = c.AddComponent<LayoutElement>();
                le.preferredWidth = iw;
                le.preferredHeight = ChoiceH;
                le.minHeight = ChoiceH;
                bool on = i == chosen;
                c.GetComponent<Image>().color = on ? UiKit.CardChosen : UiKit.CardBase;
                TextMeshProUGUI t = c.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null && on) { t.color = ui.ButtonTextColor; t.fontStyle = FontStyles.Bold; }
                UiKit.NavGroup(c, 3); // the list alone while it is open: no way out of group 3 but Select and Back
                if (on || chosenItem == null) chosenItem = c.GetComponent<GamepadNavigationItem>();
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (chosen >= 0 && n > DropShown) dropScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(chosen / (float)(n - 1));
            dropFrom = from.GetComponent<GamepadNavigationItem>();
            FocusNext = chosenItem;
            NavDirty = true;
        }

        internal void CloseDropdown(bool refocus)
        {
            if (dropdown == null) return;
            dropdown.SetActive(false);
            UnityEngine.Object.Destroy(dropdown);
            dropdown = null;
            dropScroll = null;
            if (refocus && dropFrom != null) FocusNext = dropFrom;
            dropFrom = null;
            NavDirty = true;
        }

        // A text box of our make (the search box, a row's name): a dark field, a hint while empty.
        // Built switched off and switched on only once its text parts are set (as GK2 Tech Search's, tested in game): the
        // field sets itself up for typing (its caret, its text's renderer) when it is enabled, and with no text part
        // then it never takes a key (the user, 2026-10-26: "the name field is currently read only").
        internal TMP_InputField InputBox(Transform parent, string name, float x, float y, float w, float h, string hintText, int limit)
        {
            GameObject go = ui.Node(name, parent);
            go.SetActive(false);
            go.AddComponent<Image>();
            UiKit.Rect(go, x, y, w, h);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            var edge = go.AddComponent<Outline>();
            edge.effectColor = UiKit.EdgeColor;
            edge.effectDistance = new Vector2(1f, -1f);
            GameObject area = ui.Node(name + " text area", go.transform, typeof(RectMask2D));
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(8f, 2f);
            art.offsetMax = new Vector2(-8f, -2f);
            TextMeshProUGUI text = ui.Text(area.transform, name + " text", "", 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            UiKit.Stretch((RectTransform)text.transform);
            text.richText = false;
            TextMeshProUGUI hint = ui.Text(area.transform, name + " hint", hintText, 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            UiKit.Stretch((RectTransform)hint.transform);
            hint.color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.45f);
            hint.fontStyle = FontStyles.Italic;
            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = art;
            field.textComponent = text;
            field.placeholder = hint;
            if (ui.Font != null) field.fontAsset = ui.Font;
            field.pointSize = ui.FontSize * 0.9f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = limit;
            field.richText = false;
            field.caretColor = ui.FontColor;
            field.customCaretColor = true;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            go.SetActive(true);
            fields.Add(field);
            return field;
        }

        // ---- the gamepad, every frame while the window is the one shown ----

        // The game's navigation on the window: on while a gamepad is in use (the mouse: no focus frames, as the game's
        // windows), its items read again when they changed. Up/Down move; Left/Right move unless a value control is
        // focused (a slider steps); Select presses. focusStart: the first focus when nothing was focused.
        internal void TickGamepad(GameObject win, Action<GamepadNavigationController> focusStart)
        {
            if (win == null) return;
            var nav = win.GetComponent<GamepadNavigationController>();
            if (nav == null) return;
            if (!LazyInput.IsGamepadActive)
            {
                if (nav.IsEnabled) nav.Disable();
                return;
            }
            if (!nav.IsEnabled || NavDirty)
            {
                GamepadNavigationItem was = nav.IsEnabled ? nav.FocusedItem : null;
                nav.Enable();
                nav.ReinitItems(focusOnFirstActive: false);
                NavDirty = false;
                if (FocusNext != null && FocusNext && FocusNext.isActiveAndEnabled) { nav.SetFocusedItem(FocusNext); UiKit.KeepInView(FocusNext); } // a dropdown opened or closed
                else if (was != null && was && was.isActiveAndEnabled) nav.SetFocusedItem(was);
                else focusStart?.Invoke(nav);
                FocusNext = null;
            }
            if (TypingField() != null) return; // typing: the keys are the text box's
            GamepadNavigationItem focused = nav.FocusedItem;
            bool onValueRow = focused != null && focused.GetComponentInParent<UISwitchButton>() != null; // a game value row steps itself with Left/Right (UISwitchButton.Update)
            if (focused != null && steppers.TryGetValue(focused, out Action<int> stepper)) // a slider: Left/Right move it
            {
                int d = Pressed(GameKey.Left) || Pressed(GameKey.DpadLeft) ? -1 : Pressed(GameKey.Right) || Pressed(GameKey.DpadRight) ? 1 : 0;
                if (d != 0)
                {
                    foreach (GameKey k in new[] { GameKey.Left, GameKey.DpadLeft, GameKey.Right, GameKey.DpadRight }) LazyInput.ClearKeyDown(k);
                    stepper(d);
                    return;
                }
                onValueRow = true; // no way left or right out of it: Up and Down
            }
            if (Pressed(GameKey.Up) || Pressed(GameKey.DpadUp)) Move(nav, GUIDirection.Up);
            else if (Pressed(GameKey.Down) || Pressed(GameKey.DpadDown)) Move(nav, GUIDirection.Down);
            else if (!onValueRow && (Pressed(GameKey.Left) || Pressed(GameKey.DpadLeft))) Move(nav, GUIDirection.Left);
            else if (!onValueRow && (Pressed(GameKey.Right) || Pressed(GameKey.DpadRight))) Move(nav, GUIDirection.Right);
            else if (!onValueRow && Pressed(GameKey.Select)) { LazyInput.ClearKeyDown(GameKey.Select); nav.SelectFocusedItem(); }
        }

        private static bool Pressed(GameKey k) => LazyInput.GetKeyDown(k);

        private static void Move(GamepadNavigationController nav, GUIDirection dir)
        {
            foreach (GameKey k in new[] { GameKey.Up, GameKey.DpadUp, GameKey.Down, GameKey.DpadDown, GameKey.Left, GameKey.DpadLeft, GameKey.Right, GameKey.DpadRight }) LazyInput.ClearKeyDown(k);
            nav.Navigate(dir);
            UiKit.KeepInView(nav.FocusedItem);
        }
    }
}
