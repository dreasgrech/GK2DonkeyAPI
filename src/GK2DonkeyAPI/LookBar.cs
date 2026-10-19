using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The look bar (MountSettingsPage.LookButton): the page's look rows at the bottom of the screen, over the game, so
    // the player sees the real donkey, cart and lantern change in the game's own light. Done keeps the choice, Cancel
    // puts back what was set when the bar opened. Only in a game.
    //
    // Built from the game's own parts, copied out of its Settings window: a "< value >" row per setting (SettingsRows),
    // and its dialog button for Done and Cancel. Its own canvas sorts above the HUD. If a part can't be found or copied,
    // the bar doesn't open and the log says so; nothing else changes.
    //
    // Nothing in it ever moves (2026-10-18: with every row in one column, growing from the bottom, the custom colour
    // and lightness rows came and went as a preset changed, the whole stack shifted under the cursor, and 21 rows left
    // the screen at the top): the picture sits in a slot of fixed size, the rows stand in two columns, by section,
    // balanced on their counts, with a heading per section; a row shown only with a preset ("Custom") stays in place
    // greyed instead of hiding; and a bar taller than the screen allows is scaled down as a whole, never cut.
    internal static class LookBar
    {
        private static GameObject bar, picture, pictureSlot;
        private static RawImage pictureImage;
        private static Texture2D pictureTex;
        private static float nextPicture;
        private static MountSettingsPage page;
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> rows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();
        private const float PictureSlotW = 440f, PictureSlotH = 200f, PictureScale = 2f; // the slot in canvas units; pictures up to 2x
        private const float BottomGap = 120f, TopGap = 24f; // above the hotbar; the least room left at the top
        private static readonly Dictionary<ConfigEntryBaseKey, object> before = new Dictionary<ConfigEntryBaseKey, object>();

        // ConfigEntryBase as a key without a BepInEx using here.
        private readonly struct ConfigEntryBaseKey : IEquatable<ConfigEntryBaseKey>
        {
            internal readonly BepInEx.Configuration.ConfigEntryBase E;
            internal ConfigEntryBaseKey(BepInEx.Configuration.ConfigEntryBase e) { E = e; }
            public bool Equals(ConfigEntryBaseKey o) => ReferenceEquals(E, o.E);
            public override bool Equals(object o) => o is ConfigEntryBaseKey k && Equals(k);
            public override int GetHashCode() => E == null ? 0 : E.GetHashCode();
        }

        internal static bool IsOpen => bar != null && bar.activeSelf;

        internal static void Open(MountSettingsPage p)
        {
            try
            {
                if (!Mount.IsGameReady)
                {
                    Mount.Say("Only in a game: load your game first.");
                    return;
                }
                Close(keep: true);
                page = p;
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                if (settings == null)
                {
                    throw new InvalidOperationException("the game's Settings window isn't there to copy from");
                }
                // Out of the way: the Settings window (without reopening the pause menu) and the pause menu.
                if (LazyWindowsStackController.IsWindowOpened(settings))
                {
                    settings.onClosed = null;
                    settings.CloseWithoutCallback();
                }
                UIGamePauseWindow pause = LazyUI.GetWindow<UIGamePauseWindow>();
                if (pause != null && LazyWindowsStackController.IsWindowOpened(pause))
                {
                    pause.Close();
                }
                Build(settings);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the look bar can't open (" + e.Message + "); the colours are still in the settings.");
                Close(keep: true);
            }
        }

        private static void Build(UIGameSettingsWindow settings)
        {
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            UIDialogWindowButton dialog = settings.GetComponentInChildren<UIDialogWindowButton>(true);
            if (template == null || dialog == null)
            {
                throw new InvalidOperationException("no row or button to copy in the Settings window");
            }
            Transform content = SettingsRows.ContentOf(template.transform);
            GameObject templateRow = SettingsRows.RowGameObject(template.transform, content);
            // The window's frame (the path the framework reads too); else a plain dark panel.
            Image frame = settings.transform.Find("GenericWIndowLayout/Frame")?.GetComponent<Image>();

            bar = new GameObject("GK2DonkeyAPI look bar", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            bar.transform.SetParent(settings.transform.parent, false);
            var canvas = bar.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            // A solid panel (the game's frame sprite is only a border), the frame drawn over it.
            var bg = bar.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.15f, 0.97f);
            if (frame != null && frame.sprite != null)
            {
                var border = new GameObject("GK2DonkeyAPI look bar frame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                border.transform.SetParent(bar.transform, false);
                border.GetComponent<LayoutElement>().ignoreLayout = true;
                var bi = border.GetComponent<Image>();
                bi.sprite = frame.sprite;
                bi.type = Image.Type.Sliced;
                bi.raycastTarget = false;
                var brt = (RectTransform)border.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = brt.offsetMax = Vector2.zero;
            }
            var layout = bar.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = bar.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, BottomGap); // above the hotbar

            before.Clear();
            rows.Clear();
            // The picture first, in a slot of fixed size (the bar never changes height with the mount shown): it shows
            // the colours wherever the donkey is (indoors it's in another scene).
            pictureSlot = new GameObject("GK2DonkeyAPI look bar picture slot", typeof(RectTransform), typeof(LayoutElement));
            pictureSlot.transform.SetParent(bar.transform, false);
            var slotLe = pictureSlot.GetComponent<LayoutElement>();
            slotLe.preferredWidth = PictureSlotW;
            slotLe.preferredHeight = PictureSlotH;
            ((RectTransform)pictureSlot.transform).sizeDelta = new Vector2(PictureSlotW, PictureSlotH);
            picture = new GameObject("GK2DonkeyAPI look bar picture", typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(pictureSlot.transform, false);
            var prt = (RectTransform)picture.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            pictureImage = picture.GetComponent<RawImage>();
            pictureImage.raycastTarget = false;
            picture.SetActive(false);

            // The look rows in groups, in page order: a group is a section, or a heading inside one
            // (MountSettingsPage.Heading) with the look rows after it in that section (Saddle Up: Mount; "Mount colours"
            // and "Cart colours" in Colours; Effect). A group's rows are gathered wherever they are in the page (the
            // per-mount sets interleave); total counts one row per per-mount set, the room they take. The groups are
            // dealt into two columns in order, each to the left while that leaves the left closer to half the rows.
            var sections = new List<(string Name, List<MountSettingsPage.Row> Rows)>();
            int total = 0;
            string heading = null, headingSection = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook)
                {
                    continue;
                }
                if (r.Kind == MountSettingsPage.Kind.Note)
                {
                    heading = r.Label;
                    headingSection = r.ShownSection;
                    continue;
                }
                if (r.Entry != null)
                {
                    before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
                }
                string name = heading != null && r.ShownSection == headingSection ? heading : r.ShownSection;
                int at = sections.FindIndex(s => s.Name == name);
                if (at < 0)
                {
                    sections.Add((name, new List<MountSettingsPage.Row>()));
                    at = sections.Count - 1;
                }
                sections[at].Rows.Add(r);
                if (r.Shown == null || r.Shown()) total++;
            }
            var columns = new GameObject("GK2DonkeyAPI look bar columns", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            columns.transform.SetParent(bar.transform, false);
            var ch = columns.GetComponent<HorizontalLayoutGroup>();
            ch.spacing = 32f;
            ch.childAlignment = TextAnchor.UpperCenter;
            ch.childControlWidth = ch.childControlHeight = false;
            ch.childForceExpandWidth = ch.childForceExpandHeight = false;
            var cf = columns.GetComponent<ContentSizeFitter>();
            cf.horizontalFit = cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Transform left = Column(columns.transform, "left"), right = sections.Count > 1 ? Column(columns.transform, "right") : null;
            int inLeft = 0, half = (total + 1) / 2;
            bool rightStarted = false;
            for (int s = 0; s < sections.Count; s++)
            {
                int shown = sections[s].Rows.FindAll(r => r.Shown == null || r.Shown()).Count;
                // left while that leaves the left column closer to half than it is (the last group goes right whenever
                // there is a right column; once a group went right the rest follow, so the page order holds)
                bool toLeft = right == null || (!rightStarted && s < sections.Count - 1 && Math.Abs(inLeft + shown - half) <= Math.Abs(inLeft - half));
                Transform column = toLeft ? left : right;
                if (toLeft) inLeft += shown; else rightStarted = true;
                SettingsRows.MakeHeading(templateRow, column, page, sections[s].Name);
                foreach (MountSettingsPage.Row r in sections[s].Rows)
                {
                    GameObject go = SettingsRows.MakeRow(templateRow, column, r, page, out Action refresh);
                    go.SetActive(true); // a copied row starts switched off (SettingsRows.Copy)
                    // a row of a per-mount set is shown or not by Refresh (the chosen mount's: one of the set at a
                    // time, so the slot keeps its height); a row with a condition of its own (the custom colour with
                    // its preset) stays in place greyed when it is off
                    CanvasGroup group = r.Enabled != null || (r.Shown == null && r.Visible != null) ? go.AddComponent<CanvasGroup>() : null;
                    rows.Add((r, go, refresh, group));
                }
            }
            if (right != null && right.childCount == 0)
            {
                UnityEngine.Object.Destroy(right.gameObject);
            }

            var buttons = new GameObject("GK2DonkeyAPI look bar buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            buttons.transform.SetParent(bar.transform, false);
            var hl = buttons.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 16f;
            hl.childControlWidth = hl.childControlHeight = false;
            var bf = buttons.GetComponent<ContentSizeFitter>();
            bf.horizontalFit = bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Button(dialog, buttons.transform, "Done", () => Close(keep: true));
            Button(dialog, buttons.transform, "Cancel", () => Close(keep: false));

            Refresh();
            bar.SetActive(true);
            FitToScreen();
            Plugin.Log.LogInfo($"Settings: the look bar of {page.Title} is open ({rows.Count} rows in {sections.Count} group(s), {(right != null ? "two columns" : "one column")}{(bar.transform.localScale.x < 1f ? $", scaled to {bar.transform.localScale.x:0.##} to fit the screen" : "")}).");
        }

        private static Transform Column(Transform parent, string name)
        {
            var col = new GameObject("GK2DonkeyAPI look bar " + name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            col.transform.SetParent(parent, false);
            var vl = col.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 4f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = false;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var f = col.GetComponent<ContentSizeFitter>();
            f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return col.transform;
        }

        // The whole bar scaled down when it would reach past the top of the screen (the game's canvas), never cut.
        private static void FitToScreen()
        {
            var rt = (RectTransform)bar.transform;
            rt.RefreshContentFitter();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            var canvasRt = bar.transform.parent as RectTransform;
            if (canvasRt == null) return;
            float room = canvasRt.rect.height - BottomGap - TopGap;
            float height = rt.rect.height;
            float scale = height > room && height > 0f ? Mathf.Max(0.5f, room / height) : 1f;
            bar.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void Button(UIDialogWindowButton template, Transform parent, string text, Action onClick)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = "GK2DonkeyAPI " + text;
            foreach (LocalizedLabel l in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                UnityEngine.Object.DestroyImmediate(l);
            }
            foreach (TextMeshProUGUI t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                t.text = text;
            }
            LazyButton b = go.GetComponentInChildren<LazyButton>(true);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => onClick());
            go.SetActive(true);
        }

        // After a change: every row's value (one row's change can show another), and the rows that go with a preset
        // greyed or live; the layout never moves.
        internal static void Refresh()
        {
            if (bar == null)
            {
                return;
            }
            foreach (var (row, go, refresh, group) in rows)
            {
                refresh?.Invoke();
                if (row.Shown != null)
                {
                    go.SetActive(row.Shown()); // one of the per-mount set at a time: the slot keeps its height
                }
                Func<bool> enabled = row.Enabled ?? (row.Shown == null ? row.Visible : null);
                bool on = enabled == null || enabled();
                if (group != null)
                {
                    group.alpha = on ? 1f : 0.4f;
                    group.interactable = on;
                    group.blocksRaycasts = on;
                    foreach (GamepadNavigationItem n in go.GetComponentsInChildren<GamepadNavigationItem>(true))
                    {
                        n.enabled = on;
                    }
                }
            }
        }

        internal static void Tick()
        {
            if (bar != null && (!Mount.IsGameReady || !bar.activeSelf))
            {
                Close(keep: true);
                return;
            }
            if (bar == null || Time.unscaledTime < nextPicture)
            {
                return;
            }
            nextPicture = Time.unscaledTime + 0.2f;
            try
            {
                Mount.Instance paperDonkey = LookPreview.PaperOf(page);
                Texture pic = PreviewStage.Picture(paperDonkey);
                if (pic == null && PreviewStage.Failed)
                {
                    pictureTex = paperDonkey != null ? Colors.BuildPreview(paperDonkey, pictureTex) : null;
                    pic = pictureTex;
                }
                if (pic != null)
                {
                    // as large as the slot allows, up to PictureScale, centred in it: the slot's size never changes
                    pictureImage.texture = pic;
                    Vector2 art = PreviewStage.ArtSize(pic);
                    float k = Mathf.Min(PictureScale, PictureSlotW / Mathf.Max(1f, art.x), PictureSlotH / Mathf.Max(1f, art.y));
                    ((RectTransform)picture.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
                }
                picture.SetActive(pic != null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Settings: the look bar's picture is off (" + e.Message + ").");
                if (picture != null)
                {
                    picture.SetActive(false);
                }
                nextPicture = float.MaxValue;
            }
        }

        private static void Close(bool keep)
        {
            try
            {
                if (!keep)
                {
                    foreach (var kv in before)
                    {
                        kv.Key.E.BoxedValue = kv.Value; // its SettingChanged applies it
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: putting the colours back failed: " + e.Message);
            }
            before.Clear();
            rows.Clear();
            if (bar != null)
            {
                UnityEngine.Object.Destroy(bar);
                bar = null;
            }
            if (pictureTex != null)
            {
                UnityEngine.Object.Destroy(pictureTex);
                pictureTex = null;
            }
            picture = null;
            pictureSlot = null;
            nextPicture = 0f;
            page = null;
        }
    }
}
