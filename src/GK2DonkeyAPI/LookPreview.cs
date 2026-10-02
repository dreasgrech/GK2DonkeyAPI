using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The donkey's picture next to a mod's settings (MountSettingsPage.Preview), drawn by Colors.BuildPreview:
    // - in GK2 Mod Framework's Mods menu: at the top right of the page whose title is the mod's ("<Title> — ...");
    // - in the game's Settings window: beside the window, while one of the mod's pages is shown (SettingsRows).
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
            internal Transform LoggedUnder;
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
                RectTransform menuWindow = FindMenuWindow(out menuPage);
                Show(ref inMenu, menuWindow, menuPage);
                MountSettingsPage rowsPage = SettingsRows.ShownPage;
                Show(ref inSettings, rowsPage != null ? SettingsRows.WindowFrame : null, rowsPage);
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogWarning("Settings: the donkey's picture is off for this session (" + e.Message + "); the settings work without it.");
                Hide(inMenu);
                Hide(inSettings);
            }
        }

        // The Mods window showing a page of ours (titled "<Title> - ..."): the widest of the title's parents that isn't
        // the whole screen. Null when none is shown.
        private static RectTransform FindMenuWindow(out MountSettingsPage page)
        {
            page = null;
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
                        return WindowOf(t.rectTransform);
                    }
                }
            }
            return null;
        }

        private static RectTransform WindowOf(RectTransform title)
        {
            Canvas root = title.GetComponentInParent<Canvas>()?.rootCanvas;
            float screenW = root != null ? ((RectTransform)root.transform).rect.width : float.MaxValue;
            RectTransform best = null;
            for (Transform t = title.parent; t != null && t.parent != null; t = t.parent)
            {
                if (t is RectTransform r && r.rect.width < screenW * 0.95f && (best == null || r.rect.width >= best.rect.width))
                {
                    best = r;
                }
            }
            return best;
        }

        // Centred above the window, in the screen above it, as large as fits (at most three canvas units per art pixel).
        private static void Show(ref Shown s, RectTransform window, MountSettingsPage page)
        {
            Transform under = window != null ? window.parent : null;
            Mount.Instance paperDonkey = under != null ? PaperOf(page) : null;
            if (under == null || paperDonkey == null)
            {
                Hide(s);
                return;
            }
            if (s == null || s.Go == null)
            {
                s = new Shown();
                s.Go = new GameObject("GK2DonkeyAPI preview", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
                s.Go.GetComponent<LayoutElement>().ignoreLayout = true;
                s.Image = s.Go.GetComponent<RawImage>();
                s.Image.raycastTarget = false;
            }
            if (s.Go.transform.parent != under)
            {
                s.Go.transform.SetParent(under, false);
            }
            // With its cart, drawn by the game (PreviewStage); until that's ready, or if it fails, the donkey alone.
            // The cart picture; the donkey alone only if that failed (while it's being made, nothing: the stand-in
            // flashed for a moment before the cart picture took over).
            Texture pic = PreviewStage.Picture(paperDonkey);
            if (pic == null && PreviewStage.Failed)
            {
                s.Tex = Colors.BuildPreview(paperDonkey, s.Tex);
                pic = s.Tex;
            }
            if (pic == null)
            {
                Hide(s);
                return;
            }
            s.Image.texture = pic;
            var rt = (RectTransform)s.Go.transform;
            var wc = new Vector3[4];
            window.GetWorldCorners(wc); // bottom left, top left, top right, bottom right
            Vector3 topLeft = under.InverseTransformPoint(wc[1]), topRight = under.InverseTransformPoint(wc[2]);
            float screenTop = topLeft.y + 200f;
            Canvas root = window.GetComponentInParent<Canvas>()?.rootCanvas;
            if (root != null)
            {
                var rc = new Vector3[4];
                ((RectTransform)root.transform).GetWorldCorners(rc);
                screenTop = under.InverseTransformPoint(rc[1]).y;
            }
            const float margin = 8f;
            float roomH = Mathf.Max(24f, screenTop - topLeft.y - 2f * margin);
            float roomW = Mathf.Max(24f, topRight.x - topLeft.x);
            Vector2 art = PreviewStage.ArtSize(pic); // k: canvas units per art pixel
            float k = Mathf.Max(0.5f, Mathf.Min(3f, roomH / art.y, roomW / art.x));
            rt.sizeDelta = new Vector2(art.x * k, art.y * k);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localPosition = new Vector3((topLeft.x + topRight.x) / 2f, topLeft.y + margin, 0f);
            if (s.LoggedUnder != window)
            {
                s.LoggedUnder = window;
                Plugin.Log.LogInfo($"[preview] shown above '{window.name}': room {roomW:0} x {roomH:0}, picture {pic.width} x {pic.height} ({art.x:0} x {art.y:0} art pixels) at {k:0.##}x.");
            }
            s.Go.transform.SetAsLastSibling();
            s.Go.SetActive(true);
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
