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
                RectTransform menuRow = FindMenuRow(out menuPage);
                Show(ref inMenu, menuRow, menuPage, rightOf: false, titleWidth: 0f);
                MountSettingsPage rowsPage = SettingsRows.ShownPage;
                Show(ref inSettings, SettingsRows.PreviewRowOf(rowsPage), rowsPage, rightOf: false, titleWidth: 0f);
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogWarning("Settings: the donkey's picture is off for this session (" + e.Message + "); the settings work without it.");
                Hide(inMenu);
                Hide(inSettings);
            }
        }

        // The framework's row for one of ours: the picture's row (found by its label, under a page titled
        // "<Title> - ..."). Its label's parent is the row. Null when none is shown.
        private static RectTransform FindMenuRow(out MountSettingsPage page)
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
            TextMeshProUGUI[] texts = w.GetComponentsInChildren<TextMeshProUGUI>(false);
            foreach (MountSettingsPage p in pages)
            {
                MountSettingsPage.Row row = p.PreviewRow;
                if (row == null)
                {
                    continue;
                }
                bool titled = false;
                foreach (TextMeshProUGUI t in texts)
                {
                    if (t.text != null && t.text.StartsWith(p.Title + " \u2014", StringComparison.Ordinal))
                    {
                        titled = true;
                        break;
                    }
                }
                if (!titled)
                {
                    continue;
                }
                foreach (TextMeshProUGUI t in texts)
                {
                    if (t.text == row.Label && t.transform.parent is RectTransform r)
                    {
                        page = p;
                        return r;
                    }
                }
            }
            return null;
        }

        private static void Show(ref Shown s, Transform under, MountSettingsPage page, bool rightOf, float titleWidth)
        {
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
            Texture pic = PreviewStage.Picture(paperDonkey);
            if (pic == null)
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
            // In the row, a little right of its middle (the row's label is on the left), as large as fits: at most
            // three canvas units per art pixel.
            Rect room = ((RectTransform)under).rect;
            if (s.LoggedUnder != under)
            {
                s.LoggedUnder = under;
                Plugin.Log.LogInfo($"[preview] shown in '{under.name}' (parent '{under.parent?.name}'), {room.width:0} x {room.height:0} canvas units; picture {pic.width} x {pic.height}.");
            }
            float k = Mathf.Min(3f, (room.width * 0.45f) / pic.width, (room.height - 12f) / pic.height);
            k = Mathf.Max(0.5f, k);
            rt.sizeDelta = new Vector2(pic.width * k, pic.height * k);
            rt.anchorMin = rt.anchorMax = new Vector2(0.62f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
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
