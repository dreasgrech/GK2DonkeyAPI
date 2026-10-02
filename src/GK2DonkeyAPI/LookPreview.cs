using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The donkey's picture next to a mod's settings (MountSettingsPage.Preview), drawn by PreviewStage:
    // - in GK2 Mod Framework's Mods menu, on the page whose title is the mod's ("<Title> — ..."): above the window if
    //   it fits there, else in the page's own rows in the list (MountSettingsPage.Preview, one per section asked for);
    // - in the game's Settings window: above the window, while one of the mod's pages is shown (SettingsRows).
    // Above the window only at MinScale or larger.
    // An extra on top of other UI: found by name and text, never by the framework's types (tests/FrameworkScan), and
    // everything is in try/catch. If anything fails it's switched off for the session and logged once; the settings
    // themselves are never touched.
    internal static class LookPreview
    {
        private const float Every = 0.25f;
        private const string FrameworkWindow = "GK2ModsMenuWindow";

        private static readonly List<MountSettingsPage> pages = new List<MountSettingsPage>();
        private static float next;
        private static bool failed, captured;

        // The real donkey of the page's companion (to refresh the art from, and for "is it here"), or null.
        internal static MountHandle Who(MountSettingsPage p)
        {
            MountCompanion c = p?.PreviewOf;
            MountHandle h = c?.Donkey ?? c?.Foal;
            return h != null && h.Exists ? h : null;
        }

        // What the picture is drawn from: the companion's donkey on paper (its look, no game needed).
        internal static Mount.Instance PaperOf(MountSettingsPage p) => p?.PreviewOf?.Paper;

        private sealed class Shown
        {
            internal GameObject Go;
            internal RawImage Image;
            internal Texture2D Tex;
            internal string Logged;
        }

        private static Shown inMenu, inSettings;

        internal static void Watch(MountSettingsPage page)
        {
            if (page.PreviewOf != null)
            {
                pages.Add(page);
            }
        }

        internal static void Tick()
        {
            if (failed || pages.Count == 0 || Time.unscaledTime < next)
            {
                return;
            }
            next = Time.unscaledTime + Every;
            try
            {
                // The picture's base: from the game's files (Colors.PollBase); only if they failed, from a donkey of
                // ours the first time one is drawn.
                Colors.PollBase();
                if (!captured && !Colors.HasPreviewBase)
                {
                    foreach (MountSettingsPage p in pages)
                    {
                        MountHandle d = Who(p);
                        if (d != null && d.Exists && d.IsReady && Colors.CaptureBase(d.I))
                        {
                            captured = true;
                            break;
                        }
                    }
                }
                MountSettingsPage menuPage = null;
                RectTransform menuWindow = FindMenuWindow(out menuPage, out TextMeshProUGUI menuTitle);
                Show(ref inMenu, menuWindow, menuPage, menuTitle, menu: true);
                MountSettingsPage rowsPage = SettingsRows.ShownPage;
                Show(ref inSettings, rowsPage != null ? SettingsRows.WindowFrame : null, rowsPage, null, menu: false);
            }
            catch (Exception e)
            {
                failed = true;
                foreach (MountSettingsPage p in pages)
                {
                    SetRowWanted(p, false);
                }
                Plugin.Log.LogWarning("Settings: the donkey's picture is off for this session (" + e.Message + "); the settings work without it.");
                Hide(inMenu);
                Hide(inSettings);
                HideRows();
            }
        }

        // The Mods window showing a page of ours (titled "<Title> - ..."). Null when none is shown; title is set when
        // the page is shown even if the window itself isn't found (then the picture goes into the page's row).
        private static RectTransform FindMenuWindow(out MountSettingsPage page, out TextMeshProUGUI title)
        {
            page = null;
            title = null;
            if (!FrameworkLink.Installed)
            {
                return null;
            }
            GameObject w = GameObject.Find(FrameworkWindow);
            if (w == null)
            {
                return null;
            }
            foreach (TextMeshProUGUI t in w.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (string.IsNullOrEmpty(t.text))
                {
                    continue;
                }
                foreach (MountSettingsPage p in pages)
                {
                    if (t.text.StartsWith(p.Title + " \u2014", StringComparison.Ordinal))
                    {
                        page = p;
                        title = t;
                        return WindowOf(t.rectTransform);
                    }
                }
            }
            return null;
        }

        // The window: the framework names it "Panel", a child of "GK2ModsMenuWindow" (ModsMenuWindow.BuildUi, 760 x
        // 500, scaled down by localScale when the screen is too small). If that isn't found (another framework
        // version): the widest of the title's parents that isn't the whole screen, measured on screen (rect.width
        // ignores the window's scale: at 1440x1080 the 760-unit window, scaled to 0.9, was wider than 95% of the 720-
        // unit screen, so nothing was found).
        private static RectTransform WindowOf(RectTransform title)
        {
            for (Transform t = title.parent; t != null && t.parent != null; t = t.parent)
            {
                if (t.name == "Panel" && t.parent.name == FrameworkWindow && t is RectTransform panel)
                {
                    return panel;
                }
            }
            Canvas root = title.GetComponentInParent<Canvas>()?.rootCanvas;
            float screenW = root != null ? ScreenWidth((RectTransform)root.transform) : float.MaxValue;
            RectTransform best = null;
            float bestW = 0f;
            for (Transform t = title.parent; t != null && t.parent != null; t = t.parent)
            {
                if (t is RectTransform r)
                {
                    float w = ScreenWidth(r);
                    if (w < screenW * 0.95f && w >= bestW)
                    {
                        best = r;
                        bestW = w;
                    }
                }
            }
            return best;
        }

        private static float ScreenWidth(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Vector3.Distance(c[1], c[2]);
        }

        // Where the picture goes, as large as fits there (K: canvas units per art pixel, at most MaxScale).
        private struct Spot
        {
            internal float CentreX, Y, K, RoomW, RoomH;
        }

        // MinScale: below it a spot is too small (at 1920x1080 the canvas is about 2 screen pixels a unit, so 0.75 is
        // 1.5 screen pixels per art pixel).
        private const float MinScale = 0.75f, MaxScale = 3f, Margin = 8f;

        // Pages whose picture has no room outside the list, so it's a row of the page (MountSettingsPage.Preview).
        private static readonly HashSet<MountSettingsPage> rowWanted = new HashSet<MountSettingsPage>();

        internal static bool RowWanted(MountSettingsPage page) => rowWanted.Contains(page);

        private static void SetRowWanted(MountSettingsPage page, bool wanted)
        {
            if (page == null || (wanted ? !rowWanted.Add(page) : !rowWanted.Remove(page)))
            {
                return;
            }
            Plugin.Log.LogInfo($"[preview] {page.Title}: the picture's row in the list is {(wanted ? "shown (no room outside the list)" : "hidden")}.");
            page.RefreshMenu?.Invoke(); // the Mods menu re-reads the row's visibility
        }

        private static Shown NewShown()
        {
            var s = new Shown();
            s.Go = new GameObject("GK2DonkeyAPI preview", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            s.Go.GetComponent<LayoutElement>().ignoreLayout = true;
            s.Image = s.Go.GetComponent<RawImage>();
            s.Image.raycastTarget = false;
            return s;
        }

        private static void Show(ref Shown s, RectTransform window, MountSettingsPage page, TextMeshProUGUI title, bool menu)
        {
            // measured: the window was found, so the room above it can be measured. Not measured but the page is shown
            // (title): the page's rows.
            Transform under = window != null ? window.parent : null;
            bool measured = under != null;
            Mount.Instance paperDonkey = measured || title != null ? PaperOf(page) : null;
            if (paperDonkey == null)
            {
                Hide(s);
                if (menu)
                {
                    HideRows();
                }
                return;
            }
            string where = measured ? $"'{window.name}'" : "the Mods menu (its window wasn't found, so nothing could be measured)";
            if (s == null || s.Go == null)
            {
                s = NewShown();
            }
            // With its cart, drawn by the game (PreviewStage); the donkey alone only if that failed (while it's being
            // made, nothing: the stand-in flashed for a moment before the cart picture took over).
            Texture pic = PreviewStage.Picture(paperDonkey);
            if (pic == null && PreviewStage.Failed)
            {
                s.Tex = Colors.BuildPreview(paperDonkey, s.Tex);
                pic = s.Tex;
            }
            if (pic == null)
            {
                Hide(s);
                if (menu)
                {
                    HideRows();
                }
                return;
            }
            // Above the window when it fits there (MinScale); else the page's own rows, in the Mods menu (the
            // framework's page has them); else nowhere. (Also tried: the page's header, at most 0.53x at any of the
            // game's resolutions, and beside the window, which none of them needs.)
            Vector2 art = PreviewStage.ArtSize(pic);
            Spot above = measured ? Above(window, under, art) : default;
            bool useRows = (!measured || above.K < MinScale) && title != null && page.HasPreviewRows;
            SetRowWanted(page, useRows);
            List<RectTransform> rows = useRows ? FindRows(title, page) : null;
            string rooms = (measured ? $"above {above.RoomW:0} x {above.RoomH:0} -> {above.K:0.##}x; " : "")
                           + $"picture {pic.width} x {pic.height}, {art.x:0} x {art.y:0} art pixels; screen {Screen.width} x {Screen.height}"
                           + (measured ? $", window scale {window.localScale.x:0.###}" : "");
            string line;
            if (useRows)
            {
                line = rows.Count == 0
                    ? $"[preview] waiting for its rows in {where} ({rooms})."
                    : $"[preview] shown in its rows ({rows.Count}), in {where}, at {RowScale(rows[0], art):0.##}x ({rooms}).";
            }
            else
            {
                line = above.K >= MinScale
                    ? $"[preview] shown above, in {where}, at {above.K:0.##}x ({rooms})."
                    : $"[preview] not shown in {where}: no room above it ({rooms}; needs {MinScale}x) and no row for it.";
            }
            if (s.Logged != line)
            {
                s.Logged = line;
                Plugin.Log.LogInfo(line);
            }
            if (useRows)
            {
                Hide(s);
                ShowRows(rows, pic, art);
                return;
            }
            if (menu)
            {
                HideRows();
            }
            if (above.K < MinScale)
            {
                Hide(s);
                return;
            }
            Place(s, under, pic, art, above.K);
            var rt = (RectTransform)s.Go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localPosition = new Vector3(above.CentreX, above.Y, 0f);
        }

        private static void Place(Shown s, Transform parent, Texture pic, Vector2 art, float k)
        {
            if (s.Go.transform.parent != parent)
            {
                s.Go.transform.SetParent(parent, false);
            }
            s.Image.texture = pic;
            ((RectTransform)s.Go.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
            s.Go.transform.SetAsLastSibling();
            s.Go.SetActive(true);
        }

        // One picture per row, right of the row's middle (its label is on the left); each scrolls with the list.
        private static readonly List<Shown> rowPics = new List<Shown>();

        private static float RowScale(RectTransform row, Vector2 art)
        {
            Rect r = row.rect;
            return Fit(r.width * 0.45f, r.height - 12f, art);
        }

        private static void ShowRows(List<RectTransform> rows, Texture pic, Vector2 art)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == rowPics.Count || rowPics[i] == null || rowPics[i].Go == null)
                {
                    Shown n = NewShown();
                    if (i == rowPics.Count)
                    {
                        rowPics.Add(n);
                    }
                    else
                    {
                        rowPics[i] = n;
                    }
                }
                Place(rowPics[i], rows[i], pic, art, RowScale(rows[i], art));
                var rt = (RectTransform)rowPics[i].Go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.62f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
            for (int i = rows.Count; i < rowPics.Count; i++)
            {
                Hide(rowPics[i]);
            }
        }

        private static void HideRows()
        {
            foreach (Shown r in rowPics)
            {
                Hide(r);
            }
        }

        private static float Fit(float roomW, float roomH, Vector2 art)
        {
            return roomW <= 0f || roomH <= 0f ? 0f : Mathf.Min(MaxScale, roomW / art.x, roomH / art.y);
        }

        // Centred above the window: the screen between the window's top edge and the screen's.
        private static Spot Above(RectTransform window, Transform under, Vector2 art)
        {
            var w = new Vector3[4];
            window.GetWorldCorners(w); // bottom left, top left, top right, bottom right
            for (int i = 0; i < 4; i++)
            {
                w[i] = under.InverseTransformPoint(w[i]);
            }
            float screenTop = w[1].y;
            Canvas root = window.GetComponentInParent<Canvas>()?.rootCanvas;
            if (root != null)
            {
                var rc = new Vector3[4];
                ((RectTransform)root.transform).GetWorldCorners(rc);
                screenTop = under.InverseTransformPoint(rc[1]).y;
            }
            float roomH = screenTop - w[1].y - 2f * Margin;
            float roomW = w[2].x - w[1].x;
            return new Spot
            {
                CentreX = (w[1].x + w[2].x) / 2f, Y = w[1].y + Margin,
                RoomW = roomW, RoomH = roomH, K = Fit(roomW, roomH, art),
            };
        }

        // The page's picture rows on screen: the rows around the labels of its Preview rows (under the page that holds
        // the title), top to bottom. Empty while they aren't there yet.
        private static List<RectTransform> FindRows(TextMeshProUGUI title, MountSettingsPage page)
        {
            var found = new List<RectTransform>();
            Transform under = title.rectTransform.parent;
            if (under == null)
            {
                return found;
            }
            foreach (TextMeshProUGUI t in under.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (t.text != null && page.IsPreviewLabel(t.text) && t.transform.parent is RectTransform r && !found.Contains(r))
                {
                    found.Add(r);
                }
            }
            return found;
        }

        private static void Hide(Shown s)
        {
            if (s?.Go != null && s.Go.activeSelf)
            {
                s.Go.SetActive(false);
            }
        }
    }
}
