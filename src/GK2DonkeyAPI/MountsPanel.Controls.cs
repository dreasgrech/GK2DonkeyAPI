using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using BepInEx.Configuration;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The panel's own row controls, in place of the game's "< value >" rows (the user, 2026-10-26: the arrows are "way
    // too limiting... not scalable"; the user's pick "mixed by kind"): on/off a switch, a small range (the babies,
    // 0-5) a row of number buttons, a large one (a volume) a slider with its value, a list (the skin, a colour preset)
    // a dropdown opening a scrolling list, a text (the name) a text box. One row: its label left on the column's label
    // edge (wrapping onto a second line when long), its control ending on the column's value edge, nothing outside the
    // column (a game row draws wider than the journal's column: its labels were cut on the left). The journal and the
    // colours window use the same controls (the user, 2026-10-26: "Yes, same controls").
    internal static partial class MountsPanel
    {
        private const float OwnRowH = 30f, CtrlH = 24f, SwitchW = 48f, SwitchH = 24f, NumberGap = 3f, ValueW = 46f, ChoiceH = 26f;
        private const float JournalCtrlW = 140f; // the journal's wide controls (a list, the numbers, a slider, the name)
        private const float ColoursColW = 369f, ColoursLabelW = 150f; // the colours window: two columns side by side, each its labels then its controls
        private const int MaxNumberButtons = 8;  // a range up to this many values as buttons; more: a slider
        private const int DropShown = 7;         // a dropdown's list shows this many choices, then scrolls
        private const int NameChars = 24;
        private static readonly Color KnobOff = new Color(0.45f, 0.45f, 0.47f, 1f);

        // The game's dialog button face (the panel's buttons' face), sliced; its red when there is none.
        private static void Face(Image img)
        {
            if (buttonSprite != null)
            {
                img.sprite = buttonSprite;
                img.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            else img.color = new Color(0.45f, 0.12f, 0.12f, 1f);
        }

        // The faint edge of the panel's boxes.
        private static void Edge(GameObject go)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = EdgeColor;
            o.effectDistance = new Vector2(1f, -1f);
        }

        // Where a column's rows put their parts: the label's left, the wide controls' span, the column's width.
        private struct RowLayout
        {
            internal float LabelX, CtrlLeft, CtrlRight, ColW;
        }

        private static GameObject dropdown;              // a dropdown's open list (over the whole window: a click outside closes it)
        private static ScrollRect dropScroll;
        private static GamepadNavigationItem dropFrom;   // the dropdown it opened from: the focus goes back to it
        private static GamepadNavigationItem focusNext;  // the gamepad's focus once the items are read again (TickGamepad)
        private static readonly List<TMP_InputField> fields = new List<TMP_InputField>(); // the text boxes (the search box and the rows')
        private static readonly Dictionary<GamepadNavigationItem, Action<int>> steppers = new Dictionary<GamepadNavigationItem, Action<int>>(); // a slider steps with Left/Right
        private static TMP_InputField typingIn;
        private static int typingFrame = -10;

        // The text box being typed in, if any (the mods' keys wait meanwhile: KeyActions).
        private static TMP_InputField TypingField()
        {
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i] == null) { fields.RemoveAt(i); continue; }
                if (fields[i].isFocused) return fields[i];
            }
            return null;
        }

        // What was typed into a text box still being edited is kept when its window closes.
        private static void CommitFields()
        {
            TMP_InputField f = TypingField();
            if (f != null) f.DeactivateInputField(); // its onEndEdit writes it
        }

        // A row of the panel's own for a look row, or null for a kind it has no control for (the game's row then).
        private static GameObject OwnRow(Transform parent, MountSettingsPage.Row r, RowLayout lay, out Action refresh, out float height)
        {
            refresh = null;
            height = 0f;
            MountSettingsPage.RowKind kind = r.RowKind;
            if (r.Entry == null || (kind != MountSettingsPage.RowKind.Toggle && kind != MountSettingsPage.RowKind.Steps && kind != MountSettingsPage.RowKind.Choice
                && kind != MountSettingsPage.RowKind.List && kind != MountSettingsPage.RowKind.Text)) return null;
            if (kind == MountSettingsPage.RowKind.List) MountSettingsPage.FillList(r);
            if ((kind == MountSettingsPage.RowKind.Choice || kind == MountSettingsPage.RowKind.List) && (r.ChoiceLabels == null || r.ChoiceValues == null)) return null;
            float ctrlW = kind == MountSettingsPage.RowKind.Toggle ? SwitchW : lay.CtrlRight - lay.CtrlLeft;
            float ctrlLeft = lay.CtrlRight - ctrlW;
            float labelW = Mathf.Max(40f, ctrlLeft - 10f - lay.LabelX);
            GameObject go = Node("row " + r.Entry.Definition.Key, parent);
            TextMeshProUGUI label = Text(go.transform, "label", r.Label ?? "", 0.95f, TextAlignmentOptions.Left, true, lay.LabelX, 0f, labelW, OwnRowH);
            // as tall as its label needs (a long label wraps onto a second line), the control centred on it
            height = Mathf.Max(OwnRowH, Mathf.Ceil(label.GetPreferredValues(r.Label ?? "", labelW, 0f).y) + 8f);
            Rect(go, 0f, 0f, lay.ColW, height);
            Rect(label.gameObject, lay.LabelX, 0f, labelW, height);
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
                default:
                    refresh = Dropdown(go.transform, r, ctrlLeft, cy, ctrlW);
                    break;
            }
            refresh?.Invoke();
            return go;
        }

        // A value set from a control: its SettingChanged applies it (MountSettingsPage.Wire); the panel follows (a preset
        // shows its custom colour, the picture its look).
        private static void SetValue(MountSettingsPage.Row r, object v)
        {
            try
            {
                if (!Equals(r.Entry.BoxedValue, v)) r.Entry.BoxedValue = v;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {r.Label} could not be set: {e.Message}");
            }
            Refresh();
        }

        // The gamepad's stop on a control: Select does what a click does, framed while focused.
        private static GamepadNavigationItem NavItem(GameObject go, Action select)
        {
            var item = go.GetComponent<GamepadNavigationItem>() ?? go.AddComponent<GamepadNavigationItem>();
            item.SetCallbacks(null, null, () => select());
            item.focusFrame = FocusFrame(go.transform);
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

        // On/off: a switch, its knob left (off) or right (on, in the game's red).
        private static Action Switch(Transform parent, MountSettingsPage.Row r, float x, float y)
        {
            GameObject sw = Node("switch", parent, typeof(Image), typeof(UnityEngine.UI.Button));
            Rect(sw, x, y, SwitchW, SwitchH);
            // the panel's make (the user, 2026-10-26: the switches and the slider "need to match aesthetic more"): a dark
            // box with the faint edge (the stage's, the stats card's), the knob the game's own button face, red and on the
            // right when on, greyed on the left when off
            var track = sw.GetComponent<Image>();
            track.color = BoxColor;
            Edge(sw);
            var b = sw.GetComponent<UnityEngine.UI.Button>();
            GameObject knob = Node("knob", sw.transform, typeof(Image));
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
                track.color = on ? new Color(CardChosen.r, CardChosen.g, CardChosen.b, 0.55f) : BoxColor;
                knobImg.color = on ? Color.white : KnobOff;
                Rect(knob, on ? SwitchW - kw - 2f : 2f, 2f, kw, SwitchH - 4f);
            };
        }

        // A small range: a button per value in a row, the chosen one in the game's red.
        private static Action Numbers(Transform parent, MountSettingsPage.Row r, float x, float y, float w, int count)
        {
            float bw = Mathf.Floor((w - (count - 1) * NumberGap) / count);
            float left = x + (w - (count * bw + (count - 1) * NumberGap)); // the rounding's slack on the left: the row ends on the value edge
            var buttons = new List<(int Value, Image Face, TextMeshProUGUI Label)>();
            for (int i = 0; i < count; i++)
            {
                int v = r.Min + i * r.Step;
                GameObject b = OwnButton(parent, "value " + v, v.ToString(), bw, CtrlH, () => SetValue(r, v), flat: true);
                Rect(b, left + i * (bw + NumberGap), y, bw, CtrlH);
                buttons.Add((v, b.GetComponent<Image>(), b.GetComponentInChildren<TextMeshProUGUI>(true)));
            }
            return () =>
            {
                int now = r.Entry.BoxedValue is int n ? n : r.Min;
                foreach ((int value, Image face, TextMeshProUGUI label) in buttons)
                {
                    bool on = value == now;
                    if (face != null) face.color = on ? CardChosen : CardBase;
                    if (label != null)
                    {
                        label.color = on ? buttonTextColor : fontColor;
                        label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                    }
                }
            };
        }

        // A large range: a slider, its value written beside it (the row's own words: "80%").
        private static Action SliderRow(Transform parent, MountSettingsPage.Row r, float x, float rowH, float w)
        {
            int steps = (r.Max - r.Min) / Math.Max(1, r.Step);
            TextMeshProUGUI value = Text(parent, "value", "", 0.95f, TextAlignmentOptions.Right, false, x + w - ValueW, 0f, ValueW, rowH);
            value.color = buttonTextColor;
            // the panel's make, as the switch: a dark box with the faint edge, filled in the journal's red up to the handle,
            // the handle the game's button face
            float sw = w - ValueW - 8f, sh = 22f, barH = 10f, hw = 14f;
            GameObject go = Node("slider", parent, typeof(Image), typeof(Slider));
            Rect(go, x, Mathf.Round((rowH - sh) / 2f), sw, sh);
            go.GetComponent<Image>().color = Color.clear; // the whole height takes the pointer, not only the track
            GameObject bar = Node("track", go.transform, typeof(Image));
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(0f, barH);
            bar.GetComponent<Image>().color = BoxColor;
            bar.GetComponent<Image>().raycastTarget = false;
            Edge(bar);
            GameObject fillArea = Node("fill area", go.transform);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0f, 0.5f);
            fart.anchorMax = new Vector2(1f, 0.5f);
            // inside the track's edge on the left, ending under the handle's middle at any value
            fart.offsetMin = new Vector2(1f, -(barH - 2f) / 2f);
            fart.offsetMax = new Vector2(-hw / 2f, (barH - 2f) / 2f);
            GameObject fill = Node("fill", fillArea.transform, typeof(Image));
            ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = CardChosen;
            fill.GetComponent<Image>().raycastTarget = false;
            GameObject handleArea = Node("handle area", go.transform);
            var hart = (RectTransform)handleArea.transform;
            Stretch(hart);
            hart.offsetMin = new Vector2(hw / 2f, 0f);
            hart.offsetMax = new Vector2(-hw / 2f, 0f);
            GameObject handle = Node("handle", handleArea.transform, typeof(Image));
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
        private static Action TextRow(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
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

        // A list: the chosen one's name on a button, a click opens the list under it.
        private static Action Dropdown(Transform parent, MountSettingsPage.Row r, float x, float y, float w)
        {
            GameObject b = null;
            b = OwnButton(parent, "dropdown", "", w, CtrlH, () => OpenDropdown(r, b), textLeft: 8f, flat: true);
            Rect(b, x, y, w, CtrlH);
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) Rect(label.gameObject, 8f, 0f, w - 8f - 22f, CtrlH);
            Chevron(b.transform, w - 16f, CtrlH / 2f);
            return () =>
            {
                int at = ChoiceIndex(r);
                if (label != null) label.text = at >= 0 ? r.ChoiceLabels[at] : (r.Entry.BoxedValue?.ToString() ?? "");
            };
        }

        // A small "v" drawn from two bars (no glyph the game's font may lack).
        private static void Chevron(Transform parent, float cx, float cy)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bar = Node("chevron", parent, typeof(Image));
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(7f, 2f);
                rt.anchoredPosition = new Vector2(cx + side * 2.3f, -cy);
                rt.localRotation = Quaternion.Euler(0f, 0f, side * 45f); // the left bar down to the right, the right one up: a "v" (the other sign drew a "^", the user 2026-10-26)
                bar.GetComponent<Image>().color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.8f);
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
        private static void OpenDropdown(MountSettingsPage.Row r, GameObject from)
        {
            CloseDropdown(refocus: false);
            GameObject window = colours != null ? colours : panel;
            if (window == null || from == null || r.ChoiceLabels == null || r.ChoiceLabels.Length == 0) return;
            var wrt = (RectTransform)window.transform;
            dropdown = Node("dropdown", window.transform, typeof(Image), typeof(UnityEngine.UI.Button));
            Stretch((RectTransform)dropdown.transform);
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
            RectTransform box = Box(dropdown.transform, "dropdown list", x, y, w, listH);
            box.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            box.GetComponent<Image>().raycastTarget = true;
            box.gameObject.AddComponent<UnityEngine.UI.Button>().transition = Selectable.Transition.None; // takes a click between its choices: it doesn't reach the backdrop and close the list
            dropScroll = ScrollView(box, "dropdown scroll", 2f, 2f, w - 4f, listH - 4f, out RectTransform content, 2f);
            AddScrollbar(dropScroll);
            int chosen = ChoiceIndex(r);
            GamepadNavigationItem chosenItem = null;
            for (int i = 0; i < n; i++)
            {
                object v = r.ChoiceValues.GetValue(i);
                float iw = w - 4f - ScrollbarW - 10f;
                GameObject c = OwnButton(content, "choice " + i, r.ChoiceLabels[i], iw, ChoiceH, () => { CloseDropdown(refocus: true); SetValue(r, v); }, textLeft: 8f, flat: true);
                var le = c.AddComponent<LayoutElement>();
                le.preferredWidth = iw;
                le.preferredHeight = ChoiceH;
                le.minHeight = ChoiceH;
                bool on = i == chosen;
                c.GetComponent<Image>().color = on ? CardChosen : CardBase;
                TextMeshProUGUI t = c.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null && on) { t.color = buttonTextColor; t.fontStyle = FontStyles.Bold; }
                NavGroup(c, 3); // the list alone while it is open: no way out of group 3 but Select and Back
                if (on || chosenItem == null) chosenItem = c.GetComponent<GamepadNavigationItem>();
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (chosen >= 0 && n > DropShown) dropScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(chosen / (float)(n - 1));
            dropFrom = from.GetComponent<GamepadNavigationItem>();
            focusNext = chosenItem;
            navDirty = true;
        }

        private static void CloseDropdown(bool refocus)
        {
            if (dropdown == null) return;
            dropdown.SetActive(false);
            UnityEngine.Object.Destroy(dropdown);
            dropdown = null;
            dropScroll = null;
            if (refocus && dropFrom != null) focusNext = dropFrom;
            dropFrom = null;
            navDirty = true;
        }

        // A text box of the panel's make (the search box, a row's name): a dark field, a hint while empty.
        // Built switched off and switched on only once its text parts are set (as GK2 Tech Search's, tested in game): the
        // field sets itself up for typing (its caret, its text's renderer) when it is enabled, and with no text part
        // then it never takes a key (the user, 2026-10-26: "the name field is currently read only").
        private static TMP_InputField InputBox(Transform parent, string name, float x, float y, float w, float h, string hintText, int limit)
        {
            GameObject go = Node(name, parent);
            go.SetActive(false);
            go.AddComponent<Image>();
            Rect(go, x, y, w, h);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            var edge = go.AddComponent<Outline>();
            edge.effectColor = EdgeColor;
            edge.effectDistance = new Vector2(1f, -1f);
            GameObject area = Node(name + " text area", go.transform, typeof(RectMask2D));
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(8f, 2f);
            art.offsetMax = new Vector2(-8f, -2f);
            TextMeshProUGUI text = Text(area.transform, name + " text", "", 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            Stretch((RectTransform)text.transform);
            text.richText = false;
            TextMeshProUGUI hint = Text(area.transform, name + " hint", hintText, 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            Stretch((RectTransform)hint.transform);
            hint.color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.45f);
            hint.fontStyle = FontStyles.Italic;
            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = art;
            field.textComponent = text;
            field.placeholder = hint;
            if (font != null) field.fontAsset = font;
            field.pointSize = fontSize * 0.9f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = limit;
            field.richText = false;
            field.caretColor = fontColor;
            field.customCaretColor = true;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            go.SetActive(true);
            fields.Add(field);
            return field;
        }

        // The journal's rows in one order on every mount (the user, 2026-10-26: "the order of these are not all the
        // same for all mounts"; "The skin most definitely should be the second in the list after name to show that the
        // name is per mount not per skin"): a text row (its name) first, then the Skin, then the others in the order
        // the page made the donkey's (a mount registered later had its rows made later: page.Rows alone put its Skin
        // last).
        private static void JournalOrder(List<MountSettingsPage.Row> list, string chosen)
        {
            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook || r.Entry == null || !string.Equals(r.ShownSection, "Mount", StringComparison.OrdinalIgnoreCase)) continue;
                string b = BaseKey(r, r.MountId);
                if (!rank.ContainsKey(b)) rank[b] = i;
                i++;
            }
            int Rank(MountSettingsPage.Row r)
            {
                if (r.RowKind == MountSettingsPage.RowKind.Text) return -2;
                string b = BaseKey(r, chosen);
                if (b.Equals("Skin", StringComparison.OrdinalIgnoreCase)) return -1;
                return rank.TryGetValue(b, out int at) ? at : int.MaxValue;
            }
            // a stable insertion sort (few rows; no LINQ in the game)
            for (int a = 1; a < list.Count; a++)
            {
                MountSettingsPage.Row r = list[a];
                int k = Rank(r), j = a - 1;
                while (j >= 0 && Rank(list[j]) > k)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = r;
            }
        }

        // The self-tests' view of the journal: its rows' keys in order (without the mount's part), how many are still the
        // game's rows, and the first part of a row drawn outside the journal's column (its mask would cut it), or null.
        internal static string TestJournalOrder()
        {
            var keys = new List<string>();
            foreach (var x in rows) keys.Add(BaseKey(x.Row, builtFor));
            return string.Join(",", keys);
        }

        internal static int TestJournalGameRows()
        {
            int n = 0;
            foreach (var x in rows) if (x.Go != null && x.Go.GetComponentInChildren<UISwitchButton>(true) != null) n++;
            return n;
        }

        // The first row of a window (the journal, else the colours window) whose label has less than 100 units, or null
        // (the user's screenshot, 2026-10-26: the colours window's labels 10 wide, "Co / col" under the dropdowns).
        internal static string TestNarrowLabel(bool coloursWindow)
        {
            foreach (var x in coloursWindow ? colourRows : rows)
            {
                if (x.Go == null) continue;
                Transform t = x.Go.transform.Find("GK2DonkeyAPI mounts panel label");
                if (t is RectTransform rt && rt.rect.width < 100f) return $"{x.Row.Label}: {rt.rect.width:0}";
            }
            return null;
        }

        // The name's text box takes the typing: activated, it is focused a frame on (built switched on before its text
        // parts were set, it never was: the user, 2026-10-26). Null: no text box in the journal.
        internal static TMP_InputField TestNameField()
        {
            foreach (var x in rows) if (x.Go != null && x.Row.RowKind == MountSettingsPage.RowKind.Text) return x.Go.GetComponentInChildren<TMP_InputField>(true);
            return null;
        }

        internal static string TestJournalOutside()
        {
            if (contentScroll == null || contentScroll.viewport == null) return "no journal";
            RectTransform view = contentScroll.viewport;
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

        // A row's key without its mount's part: "Foals_saddleup-dragon.dragon" and "Foals" are both "Foals", every
        // "Skin_<mount>" is "Skin".
        private static string BaseKey(MountSettingsPage.Row r, string mountId)
        {
            string key = r.Entry?.Definition.Key ?? "";
            if (key.StartsWith("Skin_", StringComparison.OrdinalIgnoreCase)) return "Skin";
            if (!string.IsNullOrEmpty(mountId) && key.EndsWith("_" + mountId, StringComparison.OrdinalIgnoreCase)) return key.Substring(0, key.Length - mountId.Length - 1);
            return key;
        }
    }
}
