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
    internal static class LookBar
    {
        private static GameObject bar, picture;
        private static RawImage pictureImage;
        private static Texture2D pictureTex;
        private static float nextPicture;
        private static MountSettingsPage page;
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh)> rows = new List<(MountSettingsPage.Row, GameObject, Action)>();
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
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = bar.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 120f); // above the hotbar

            before.Clear();
            rows.Clear();
            // The picture first: it shows the colours wherever the donkey is (indoors it's in another scene).
            picture = new GameObject("GK2DonkeyAPI look bar picture", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            picture.transform.SetParent(bar.transform, false);
            pictureImage = picture.GetComponent<RawImage>();
            pictureImage.raycastTarget = false;
            picture.SetActive(false);
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook)
                {
                    continue;
                }
                if (r.Entry != null)
                {
                    before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
                }
                GameObject go = SettingsRows.MakeRow(templateRow, bar.transform, r, page, out Action refresh);
                rows.Add((r, go, refresh));
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
            Plugin.Log.LogInfo($"Settings: the look bar of {page.Title} is open ({rows.Count} rows).");
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

        // After a change: the rows shown only with Custom, and every row's value (one row's change can show another).
        internal static void Refresh()
        {
            if (bar == null)
            {
                return;
            }
            foreach (var (row, go, refresh) in rows)
            {
                bool on = row.Visible == null || row.Visible();
                if (on)
                {
                    refresh?.Invoke();
                }
                go.SetActive(on);
            }
            ((RectTransform)bar.transform).RefreshContentFitter();
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
                    pictureImage.texture = pic;
                    var le = picture.GetComponent<LayoutElement>();
                    le.preferredWidth = pic.width * 2f;
                    le.preferredHeight = pic.height * 2f;
                    ((RectTransform)picture.transform).sizeDelta = new Vector2(le.preferredWidth, le.preferredHeight);
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
            nextPicture = 0f;
            page = null;
        }
    }
}
