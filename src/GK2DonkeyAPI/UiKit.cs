using System;
using System.Collections.Generic;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // What our windows (the Mounts panel, the options window) are built from: the game's look, read off its Settings
    // window (the window frame, its title bar, its dialog button's face, the labels' and the buttons' fonts, the focus frame
    // of its rows), and the pieces made with it: windows, title bars, boxes, texts, buttons, scroll views. Laid out by
    // hand from the top left, on the game's canvas (docs/mounts-panel-design.md "The layout").
    // One per window (each reads the look when it opens); RowControls builds the rows' controls with it.
    internal sealed class UiKit
    {
        internal static readonly Color CardBase = new Color(0.13f, 0.14f, 0.18f, 0.9f), CardChosen = new Color(0.42f, 0.11f, 0.11f, 0.95f); // a journal's rows: dark, the chosen one in the game's red
        internal static readonly Color BoxColor = new Color(0f, 0f, 0f, 0.35f), EdgeColor = new Color(1f, 1f, 1f, 0.12f);
        internal const float FaceInset = 13f; // the game's BackMask inside its frame
        internal const float ScrollbarW = 6f;
        private const float EdgeGap = 24f; // the least room left at the screen's edges before a window is scaled down

        private readonly string prefix; // every object's name starts with it ("GK2DonkeyAPI mounts panel ...")

        internal UIGameSettingsWindow Settings;
        internal GameObject TemplateRow;          // the game's "< value >" row: the labels' font is read off it, and where it draws
        internal UIDialogWindowButton DialogTemplate;
        internal Sprite ButtonSprite;             // the game's dialog button face, for our own buttons
        internal Color ButtonTextColor = Color.white;
        internal TMP_FontAsset Font, BoldFont;    // the labels' (small_font) and the game's buttons' and titles' (small_font_bold)
        internal Material FontMaterial, BoldMaterial;
        internal Color FontColor = Color.white;
        internal float FontSize = 18f;
        internal GameObject FocusTemplate;        // the game row's own focus frame, for our buttons
        internal Rect RowVisual = new Rect(0f, 0f, 400f, 33f); // where a game row really draws, relative to its rectangle's top left: x and y its offsets (negative: outside), its drawn width and height
        internal float RowLabelRight = 160f, RowCtrlLeft = 168f, RowCtrlRight = 168f + 12f * 16f;

        internal UiKit(string prefix)
        {
            this.prefix = prefix;
        }

        // The look, read off the game's Settings window. Throws when a part to copy is missing (a game update).
        internal void Read(UIGameSettingsWindow settings)
        {
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            DialogTemplate = settings.GetComponentInChildren<UIDialogWindowButton>(true);
            if (template == null || DialogTemplate == null)
            {
                throw new InvalidOperationException("no row or button to copy in the Settings window");
            }
            Settings = settings;
            Transform content = ContentOf(template.transform);
            TemplateRow = RowGameObject(template.transform, content);
            ReadFont();
            ButtonSprite = null;
            foreach (Image img in DialogTemplate.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null) { ButtonSprite = img.sprite; break; }
            }
            TextMeshProUGUI bt = DialogTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
            if (bt != null) { ButtonTextColor = bt.color; BoldFont = bt.font; BoldMaterial = bt.fontSharedMaterial; }
            MeasureRow();
            FocusTemplate = TemplateRow.GetComponentInChildren<GamepadNavigationItem>(true)?.focusFrame;
        }

        // The window's content: the vertical layout the rows sit in, above a row's UISwitchButton.
        internal static Transform ContentOf(Transform t)
        {
            Transform row = t;
            while (row.parent != null && row.parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                row = row.parent;
            }
            return row.parent;
        }

        // The row (the child of the content) a part belongs to.
        internal static GameObject RowGameObject(Transform t, Transform content)
        {
            while (t.parent != content)
            {
                t = t.parent;
            }
            return t.gameObject;
        }

        // The labels' font: the game row's label (the game's is called LeftName: a text named so, else the first text
        // outside the "< value >").
        private void ReadFont()
        {
            UISwitchButton sw = TemplateRow.GetComponentInChildren<UISwitchButton>(true);
            TextMeshProUGUI t = null;
            foreach (TextMeshProUGUI c in TemplateRow.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (c.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0) { t = c; break; }
                if (t == null && (sw == null || !c.transform.IsChildOf(sw.transform))) t = c;
            }
            if (t != null)
            {
                Font = t.font;
                FontMaterial = t.fontSharedMaterial;
                FontColor = t.color;
                FontSize = t.fontSize > 0f ? t.fontSize : FontSize;
            }
        }

        // Where the game's own row draws, relative to its rectangle's top left: its parts hang outside the rectangle (the
        // label to the left, the value box to the right), so a row is placed by where it draws, not by its rectangle.
        private void MeasureRow()
        {
            try
            {
                var root = (RectTransform)TemplateRow.transform;
                var corners = new Vector3[4];
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (RectTransform r in TemplateRow.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!ActiveUnder(r.transform, root)) continue;
                    if (r.rect.width <= 0f || r.rect.height <= 0f) continue;
                    r.GetWorldCorners(corners);
                    foreach (Vector3 c in corners)
                    {
                        Vector3 l = root.InverseTransformPoint(c);
                        minX = Math.Min(minX, l.x); maxX = Math.Max(maxX, l.x);
                        minY = Math.Min(minY, l.y); maxY = Math.Max(maxY, l.y);
                    }
                }
                if (maxX <= minX || maxY <= minY) return;
                root.GetWorldCorners(corners);
                Vector3 topLeft = root.InverseTransformPoint(corners[1]);
                RowVisual = new Rect(minX - topLeft.x, topLeft.y - maxY, maxX - minX, maxY - minY); // y down: a drawn part above the rectangle gives a negative y
                // where its label ends and its "< value >" spans, from the drawn left (the swatch rows and headings line up on them)
                UISwitchButton sw = TemplateRow.GetComponentInChildren<UISwitchButton>(true);
                TextMeshProUGUI lab = null;
                foreach (TextMeshProUGUI t in TemplateRow.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0) { lab = t; break; }
                    if (lab == null && sw != null && !t.transform.IsChildOf(sw.transform)) lab = t;
                }
                float XRange(RectTransform rt, bool right)
                {
                    rt.GetWorldCorners(corners);
                    float a = root.InverseTransformPoint(corners[0]).x, b = root.InverseTransformPoint(corners[2]).x;
                    return (right ? Math.Max(a, b) : Math.Min(a, b)) - minX;
                }
                if (sw != null && lab != null)
                {
                    var swRt = (RectTransform)sw.transform;
                    RowLabelRight = XRange((RectTransform)lab.transform, true);
                    RowCtrlLeft = XRange(swRt, false);
                    RowCtrlRight = XRange(swRt, true);
                    if (RowLabelRight > RowCtrlLeft) RowLabelRight = RowCtrlLeft - 8f;
                }
                Plugin.Log.LogInfo($"Settings: its label ends at {RowLabelRight:0}, its value spans {RowCtrlLeft:0} to {RowCtrlRight:0} (from its drawn left).");
                Plugin.Log.LogInfo($"Settings: a game row draws {RowVisual.width:0}x{RowVisual.height:0} at {RowVisual.x:0},{RowVisual.y:0} from its rectangle's top left ({root.rect.width:0}x{root.rect.height:0}).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Settings: the game row could not be measured (" + e.Message + "); the panel places rows by their rectangle.");
            }
        }

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                if (!p.gameObject.activeSelf) return false;
            }
            return true;
        }

        // A window of our make: the game's frame, a dark face inside it, its own canvas above the HUD; centred, hidden.
        internal GameObject Window(string name, float w, float h, int sorting)
        {
            var go = new GameObject("GK2DonkeyAPI " + name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            go.transform.SetParent(Settings.transform.parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sorting;
            // no face of our own: the game's frame sprite (sliced) is the window's face and its corners; a dark face under it
            // showed past the frame's rounded corners as a margin the game's windows don't have (the user, 2026-10-27).
            // Transparent, it still takes the clicks (the window blocks the world under it).
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            // the face: the frame sprite's centre is see-through (the game draws its window face apart, inside the frame:
            // BackMask, 13 in), so a face of ours inset as far, under the frame, never past its corners (2026-10-27)
            Image face = Node("face", go.transform, typeof(Image)).GetComponent<Image>();
            face.color = new Color(0.11f, 0.12f, 0.15f, 0.97f);
            face.raycastTarget = false;
            var frt = (RectTransform)face.transform;
            Stretch(frt);
            frt.offsetMin = new Vector2(FaceInset, FaceInset);
            frt.offsetMax = new Vector2(-FaceInset, -FaceInset);
            Image frame = Settings.transform.Find("GenericWIndowLayout/Frame")?.GetComponent<Image>();
            if (frame != null && frame.sprite != null)
            {
                Image bi = Node("frame", go.transform, typeof(Image)).GetComponent<Image>();
                bi.sprite = frame.sprite;
                bi.type = Image.Type.Sliced;
                bi.raycastTarget = false;
                Stretch((RectTransform)bi.transform);
            }
            var nav = go.AddComponent<GamepadNavigationController>(); // the game's gamepad navigation, as every game window has
            nav.loopVerticalNavigation = true;
            nav.restoreLastInGroup = true;
            nav.navigationGroupSources = new List<GamepadNavigationController.NavigationGroupSource>();
            go.SetActive(false);
            return go;
        }

        // The gamepad: from a group of items, a direction hands over to another group.
        internal static void Hand(GamepadNavigationController nav, int from, GUIDirection dir, int to)
        {
            var src = nav.navigationGroupSources.Find(s => s.group == from);
            if (src == null)
            {
                src = new GamepadNavigationController.NavigationGroupSource(from, new List<GamepadNavigationController.NavigationGroupTarget>());
                nav.navigationGroupSources.Add(src);
            }
            src.groupTargets.Add(new GamepadNavigationController.NavigationGroupTarget(to, dir));
        }

        internal GameObject Node(string name, Transform parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var go = new GameObject(prefix + name, types);
            go.transform.SetParent(parent, false);
            return go;
        }

        // A rectangle placed from the top left of its parent.
        internal static RectTransform Rect(GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // A dark box with a faint edge: the stage, the stats card.
        internal RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform rt = Rect(Node(name, parent, typeof(Image), typeof(Outline)), x, y, w, h);
            var img = rt.GetComponent<Image>();
            img.color = BoxColor;
            img.raycastTarget = false;
            var outline = rt.GetComponent<Outline>();
            outline.effectColor = EdgeColor;
            outline.effectDistance = new Vector2(1f, -1f);
            return rt;
        }

        // A row put at x, y from the top left of its parent, its size kept.
        internal static void Place(GameObject go, float x, float y)
        {
            var rt = (RectTransform)go.transform;
            Vector2 size = rt.rect.size;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, -y);
            var le = go.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // The game's bold font on a text (its buttons', titles' and tabs'), else a faux bold of the labels' font.
        internal TextMeshProUGUI Bold(TextMeshProUGUI t)
        {
            if (t == null) return t;
            if (BoldFont != null)
            {
                t.font = BoldFont;
                if (BoldMaterial != null) t.fontSharedMaterial = BoldMaterial;
                t.fontStyle = FontStyles.Normal;
            }
            else t.fontStyle = FontStyles.Bold;
            return t;
        }

        // The game's window title bar (its HeaderGroup, read from the Settings window: the tan bar sliced, an ornament at
        // each end, the title in its font, its close button), 11 in from the top and the sides, 26 high, on a window of
        // ours; null pieces are left out. Returns the title's text.
        internal TextMeshProUGUI TitleBar(GameObject win, float w, string title, Action onClose)
        {
            Transform hg = Settings != null ? Settings.transform.Find("GenericWIndowLayout/Frame/HeaderGroup") : null;
            const float inset = 11f, barH = 26f;
            float bw = w - 2f * inset;
            GameObject bar = Node("title bar", win.transform);
            Rect(bar, inset, inset, bw, barH);
            Image bgSrc = hg != null ? hg.Find("Background")?.GetComponent<Image>() : null;
            if (bgSrc != null && bgSrc.sprite != null)
            {
                Image bg = Node("title back", bar.transform, typeof(Image)).GetComponent<Image>();
                bg.sprite = bgSrc.sprite;
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = false;
                Stretch((RectTransform)bg.transform);
            }
            Image decor = hg != null ? hg.Find("DecorCommonLeft")?.GetComponent<Image>() : null;
            if (decor != null && decor.sprite != null)
            {
                for (int side = 0; side < 2; side++)
                {
                    Image d = Node(side == 0 ? "title decor left" : "title decor right", bar.transform, typeof(Image)).GetComponent<Image>();
                    d.sprite = decor.sprite;
                    d.raycastTarget = false;
                    Rect(d.gameObject, side == 0 ? 0f : bw - 28f, 0f, 28f, barH);
                    if (side == 1) d.transform.localScale = new Vector3(-1f, 1f, 1f); // mirrored on the right
                }
            }
            TextMeshProUGUI head = hg != null ? hg.Find("Header")?.GetComponent<TextMeshProUGUI>() : null;
            TextMeshProUGUI tt = Text(bar.transform, "title", title, 1f, TextAlignmentOptions.Center, false, 34f, 0f, bw - 68f, barH);
            Bold(tt);
            if (head != null) { tt.font = head.font; tt.fontSize = head.fontSize; tt.color = head.color; }
            Image closeBack = hg != null ? hg.Find("CloseButton/Background")?.GetComponent<Image>() : null;
            Image closeIcon = hg != null ? hg.Find("CloseButton/BtnImage")?.GetComponent<Image>() : null;
            if (closeIcon != null && closeIcon.sprite != null && onClose != null)
            {
                float bx = bw - 26f + 1f;
                if (closeBack != null && closeBack.sprite != null)
                {
                    Image cb = Node("close back", bar.transform, typeof(Image)).GetComponent<Image>();
                    cb.sprite = closeBack.sprite;
                    cb.raycastTarget = false;
                    Rect(cb.gameObject, bx - 24f, 0f, 50f, barH);
                }
                GameObject btn = Node("close", bar.transform, typeof(Image), typeof(UnityEngine.UI.Button));
                Image ci = btn.GetComponent<Image>();
                ci.sprite = closeIcon.sprite;
                Rect(btn, bx, 0f, 26f, barH);
                var b = btn.GetComponent<UnityEngine.UI.Button>();
                b.targetGraphic = ci;
                ColorBlock xc = b.colors; // lit under the mouse and darker pressed, as the game's own close button
                xc.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                xc.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                xc.selectedColor = xc.normalColor;
                xc.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                xc.colorMultiplier = 1f;
                xc.fadeDuration = 0.05f;
                b.colors = xc;
                b.onClick.AddListener(() => onClose());
            }
            return tt;
        }

        internal TextMeshProUGUI Text(Transform parent, string name, string text, float sizeMul, TextAlignmentOptions align, bool wrap, float x, float y, float w, float h)
        {
            var t = Node(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            if (FontMaterial != null) t.fontSharedMaterial = FontMaterial;
            t.color = FontColor;
            t.fontSize = FontSize * sizeMul;
            t.enableAutoSizing = false;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            t.text = text;
            Rect(t.gameObject, x, y, w, h);
            return t;
        }

        // A scroll view: the wheel scrolls (the window's own wheel code); a vertical layout of children inside, or
        // (manual) rows placed by hand.
        internal ScrollRect ScrollView(Transform parent, string name, float x, float y, float w, float h, out RectTransform content, float spacing, bool manual = false)
        {
            GameObject go = Node(name, parent, typeof(ScrollRect), typeof(RectMask2D));
            Rect(go, x, y, w, h);
            var sr = go.GetComponent<ScrollRect>();
            GameObject viewport = Node(name + " viewport", go.transform, typeof(RectMask2D));
            Stretch((RectTransform)viewport.transform);
            GameObject c = manual ? Node(name + " content", viewport.transform) : Node(name + " content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt = (RectTransform)c.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(0f, 0f);
            crt.offsetMax = new Vector2(0f, 0f);
            if (!manual)
            {
                var vl = c.GetComponent<VerticalLayoutGroup>();
                vl.spacing = spacing;
                vl.padding = new RectOffset(4, 4, 2, 2);
                vl.childAlignment = TextAnchor.UpperLeft;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childForceExpandWidth = false;
                vl.childForceExpandHeight = false;
                var cf = c.GetComponent<ContentSizeFitter>();
                cf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            sr.content = crt;
            sr.viewport = (RectTransform)viewport.transform;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 0f; // the wheel is the window's (Wheel), anywhere on it (a second scroll here would double it)
            sr.inertia = false;
            content = crt;
            return sr;
        }

        // A thin scrollbar at the right edge of a scroll view, shown while there is more than fits.
        internal void AddScrollbar(ScrollRect sr)
        {
            GameObject track = Node("scrollbar", sr.transform, typeof(Image), typeof(Scrollbar));
            var trt = (RectTransform)track.transform;
            trt.anchorMin = new Vector2(1f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 0f);
            trt.sizeDelta = new Vector2(ScrollbarW, -4f);
            track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            GameObject handle = Node("handle", track.transform, typeof(Image));
            Stretch((RectTransform)handle.transform);
            var hi = handle.GetComponent<Image>();
            hi.color = new Color(1f, 1f, 1f, 0.28f);
            var sb = track.GetComponent<Scrollbar>();
            sb.handleRect = (RectTransform)handle.transform;
            sb.targetGraphic = hi;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // A button of our own: flat (a journal's row: a dark face with a faint edge, hover brightens, the chosen one
        // coloured by its window) or the game's dialog button face sliced to the size (a touch dark at rest so the hover
        // shows), with the game's font.
        internal GameObject OwnButton(Transform parent, string name, string text, float w, float h, Action onClick, float textLeft = 0f, bool flat = false)
        {
            GameObject go = Node(name, parent, typeof(Image), typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
            Rect(go, 0f, 0f, w, h);
            var img = go.GetComponent<Image>();
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            if (flat)
            {
                img.color = CardBase;
                var edge = go.AddComponent<Outline>();
                edge.effectColor = new Color(1f, 1f, 1f, 0.10f);
                edge.effectDistance = new Vector2(1f, -1f);
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                cb.selectedColor = Color.white;
            }
            else
            {
                if (ButtonSprite != null)
                {
                    img.sprite = ButtonSprite;
                    img.type = ButtonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                }
                else
                {
                    img.color = new Color(0.45f, 0.12f, 0.12f, 1f);
                }
                cb.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
                cb.selectedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            }
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            TextMeshProUGUI t = Text(go.transform, "label", text, 1f, textLeft > 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, false, textLeft, 0f, w - textLeft - 6f, h);
            t.color = flat ? FontColor : ButtonTextColor;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (flat) t.fontStyle = FontStyles.Normal; else Bold(t);
            // the gamepad: an item whose select presses the button (as LazyButton.SetCallbacksIntoGamepadNavigationItem),
            // framed while focused with the game row's own focus frame
            var item = go.AddComponent<GamepadNavigationItem>();
            UnityEngine.UI.Button pressed = b;
            item.SetCallbacks(null, null, () => { if (pressed.interactable) pressed.onClick.Invoke(); });
            item.focusFrame = FocusFrame(go.transform);
            return go;
        }

        // A focus frame over a button: a copy of the game row's own, else a bright outline.
        internal GameObject FocusFrame(Transform parent)
        {
            GameObject f;
            if (FocusTemplate != null)
            {
                f = UnityEngine.Object.Instantiate(FocusTemplate, parent, false);
                f.name = "focus";
                foreach (Graphic g in f.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            }
            else
            {
                f = Node("focus", parent, typeof(Image), typeof(Outline));
                f.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
                f.GetComponent<Image>().raycastTarget = false;
                var o = f.GetComponent<Outline>();
                o.effectColor = new Color(1f, 0.85f, 0.3f, 1f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            var rt = (RectTransform)f.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-2f, -2f);
            rt.offsetMax = new Vector2(2f, 2f);
            var le = f.GetComponent<LayoutElement>() ?? f.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            f.SetActive(false);
            return f;
        }

        internal static void NavGroup(GameObject go, int group)
        {
            var item = go.GetComponent<GamepadNavigationItem>();
            if (item != null) item.group = group;
        }

        // A focused item inside a scroll view scrolled into view.
        internal static void KeepInView(GamepadNavigationItem item)
        {
            if (item == null) return;
            ScrollRect sr = item.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.content == null || sr.viewport == null) return;
            var irt = (RectTransform)item.transform;
            Vector3[] c = new Vector3[4];
            irt.GetWorldCorners(c);
            float top = sr.viewport.InverseTransformPoint(c[1]).y, bottom = sr.viewport.InverseTransformPoint(c[0]).y;
            Rect view = sr.viewport.rect;
            float shift = 0f;
            if (top > view.yMax) shift = top - view.yMax + 4f;
            else if (bottom < view.yMin) shift = bottom - view.yMin - 4f;
            if (shift != 0f) sr.content.anchoredPosition -= new Vector2(0f, shift / Mathf.Max(0.0001f, sr.content.lossyScale.y / sr.viewport.lossyScale.y));
        }

        // The screen point is over the object.
        internal static bool Over(GameObject go, Vector2 screen)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return false;
            Canvas c = go.GetComponentInParent<Canvas>();
            Canvas root = c != null ? c.rootCanvas : null;
            Camera cam = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, cam);
        }

        // The wheel scrolls a scroll view by WheelStep a notch, its content's top pinned (up the page is a larger y).
        internal const float WheelStep = 40f;

        internal static bool Scroll(ScrollRect target, float notches)
        {
            if (target == null || target.content == null || target.viewport == null) return false;
            float room = target.content.rect.height - target.viewport.rect.height;
            if (room <= 0f) return false; // all of it fits: nothing to scroll
            Vector2 p = target.content.anchoredPosition;
            float y = Mathf.Clamp(p.y - notches * WheelStep, 0f, room);
            if (Mathf.Approximately(y, p.y)) return false;
            target.StopMovement();
            target.content.anchoredPosition = new Vector2(p.x, y);
            return true;
        }

        // A window scaled down as a whole when the canvas is smaller than it (never cut), centred.
        internal static void FitToScreen(GameObject window, float w, float h)
        {
            if (window == null) return;
            var canvasRt = window.transform.parent as RectTransform;
            if (canvasRt == null) return;
            float roomH = canvasRt.rect.height - 2f * EdgeGap, roomW = canvasRt.rect.width - 2f * EdgeGap;
            float scale = Mathf.Max(0.5f, Mathf.Min(1f, roomW / w, roomH / h));
            window.transform.localScale = new Vector3(scale, scale, 1f);
            // measure, never assume (the user, 2026-10-21): the canvas's units at this screen, its scaler's factor, the screen
            Canvas root = canvasRt.GetComponentInParent<Canvas>();
            Plugin.Log.LogInfo($"Settings: {window.name} {w:0}x{h:0} on a canvas of {canvasRt.rect.width:0}x{canvasRt.rect.height:0} units (scale factor {(root != null ? root.scaleFactor : 0f):0.##}, screen {Screen.width}x{Screen.height}): window scale {scale:0.##}.");
        }

        // The room a window has on its canvas (the self-tests' measure).
        internal static (float RoomW, float RoomH) Room(GameObject window)
        {
            var canvasRt = window != null ? window.transform.parent as RectTransform : null;
            return canvasRt != null ? (canvasRt.rect.width - 2f * EdgeGap, canvasRt.rect.height - 2f * EdgeGap) : (0f, 0f);
        }
    }
}
