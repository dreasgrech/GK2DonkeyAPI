using System;
using System.Collections;
using System.Collections.Generic;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The mods' rows in the game's Settings window at an exaggerated number of mods (the user, 2026-11-18, on picking
    // the rows over nine other layouts: "we need to test how it would look with an exagerated amount of items to test
    // its scalability"). The scenario makes the mods up itself (SettingsRows.SetExampleRows: rows named "Example Mod
    // n" that open nothing) beside whatever real ones are installed, at 8, 9, 20 and 40 rows in all, checks at each
    // that the window still stands on the screen, that there is one strip, in order, as high as it should be, and
    // saves a picture (BepInEx\GK2-panel-shots\settings-rows-<n>.png) for the look, which no check can judge. At the
    // largest it scrolls the strip a row, to its end and back. It puts back the number of made-up rows it found.
    internal static class SettingsRowsTests
    {
        private static readonly int[] Totals = { 8, 9, 20, 40 };

        // A rectangle's edges on the screen, in pixels (bottom, top).
        private static void OnScreen(RectTransform rt, out float bottom, out float top)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera cam = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            bottom = float.MaxValue;
            top = float.MinValue;
            foreach (Vector3 w in c)
            {
                float y = RectTransformUtility.WorldToScreenPoint(cam, w).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        }

        internal static IEnumerator ManyRows()
        {
            if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: true, back: false);
            if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
            yield return SelfTest.Until(() => LazyWindowsStackController.ActiveWindow == null, 10f);
            if (LazyWindowsStackController.ActiveWindow != null)
            {
                SelfTest.Skip($"a window of the game's is open and stayed open for 10 s ({LazyWindowsStackController.ActiveWindow.name})");
                yield break;
            }
            UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
            if (settings == null)
            {
                SelfTest.Skip("no Settings window");
                yield break;
            }
            int was = SettingsRows.TestExampleRows;
            try
            {
                SettingsRows.SetExampleRows(0);
                settings.onClosed = null;
                settings.Open(null);
                yield return SelfTest.Frames(3);
                if (SettingsRows.TestFailed)
                {
                    SelfTest.Check(false, "the mods' rows can be made in the game's Settings window (they couldn't: the buttons above OK are used instead; the log says why)");
                    yield break;
                }
                List<GameObject> rows = SettingsRows.TestRows(settings);
                int real = rows.Count;
                var names = new List<string>();
                foreach (GameObject r in rows) names.Add(SettingsRows.LabelOf(r));
                SelfTest.Info($"the mods' rows as installed: {real} ({string.Join(", ", names)}); {(SettingsRows.TestKeepsStrip ? "this mod keeps the strip" : "another mod keeps the strip: its height, order, bar and wheel are that mod's doing")}; at most {SettingsRows.MaxRows} rows in view");
                SelfTest.Check(real >= 1 && SettingsRows.TestStripCount(settings) == 1, $"every mod's row stands in one strip ({SettingsRows.TestStripCount(settings)} strip(s), {real} row(s))");
                var frame = settings.transform.Find("GenericWIndowLayout") as RectTransform ?? (RectTransform)settings.transform;
                yield return SelfTest.PanelShot("settings-rows-" + real + "-as-installed");

                float heightAtCap = -1f;
                foreach (int total in Totals)
                {
                    if (total < real) continue;
                    SettingsRows.SetExampleRows(total - real);
                    yield return SelfTest.Frames(3);
                    rows = SettingsRows.TestRows(settings);
                    GameObject strip = SettingsRows.TestStrip(settings);
                    if (strip == null || rows.Count == 0)
                    {
                        SelfTest.Check(false, $"{total} rows: the strip and its rows are there ({rows.Count} row(s))");
                        continue;
                    }
                    int shown = Mathf.Min(total, SettingsRows.MaxRows);
                    // read from the scene, whichever mod's copy keeps the strip
                    var holderGroup = rows[0].transform.parent != null ? rows[0].transform.parent.GetComponent<VerticalLayoutGroup>() : null;
                    float rowH = ((RectTransform)rows[0].transform).rect.height, gap = holderGroup != null ? holderGroup.spacing : 0f;
                    float want = shown * rowH + (shown - 1) * gap, has = ((RectTransform)strip.transform).rect.height;
                    OnScreen(frame, out float bottom, out float top);
                    bool ordered = true;
                    for (int k = 1; k < rows.Count; k++)
                    {
                        if (string.Compare(SettingsRows.LabelOf(rows[k - 1]), SettingsRows.LabelOf(rows[k]), StringComparison.OrdinalIgnoreCase) > 0) ordered = false;
                    }
                    SelfTest.Check(rows.Count == total && SettingsRows.TestStripCount(settings) == 1, $"{total} rows: all there, in one strip ({rows.Count})");
                    SelfTest.Check(bottom >= -0.5f && top <= Screen.height + 0.5f, $"{total} rows: the whole Settings window is on the screen (its frame from {bottom:0} to {top:0} of {Screen.height} px, {top - bottom:0} high)");
                    SelfTest.Check(Mathf.Abs(has - want) < 0.6f, $"{total} rows: the strip is as high as {shown} row(s) ({has:0.#} units; {shown} x {rowH:0.#} and {shown - 1} gap(s) of {gap:0.#}: {want:0.#})");
                    SelfTest.Check(ordered, $"{total} rows: in the order of their names");
                    ScrollRect scroll = strip.GetComponent<ScrollRect>();
                    bool bar = scroll != null && scroll.verticalScrollbar != null && scroll.verticalScrollbar.gameObject.activeSelf;
                    SelfTest.Check(scroll != null && bar == (total > SettingsRows.MaxRows), $"{total} rows: the scroll bar is {(total > SettingsRows.MaxRows ? "shown" : "not shown")} ({(scroll == null ? "the strip has no scroll view" : bar ? "shown" : "not shown")})");
                    if (total == SettingsRows.MaxRows) heightAtCap = top - bottom;
                    else if (total > SettingsRows.MaxRows && heightAtCap > 0f)
                    {
                        SelfTest.Check(Mathf.Abs((top - bottom) - heightAtCap) < 1.5f, $"{total} rows: the window is as high as with {SettingsRows.MaxRows} ({top - bottom:0} px; {heightAtCap:0} then): it has stopped growing");
                    }
                    yield return SelfTest.PanelShot("settings-rows-" + total);

                    if (total == Totals[Totals.Length - 1] && !SettingsRows.TestKeepsStrip)
                    {
                        SelfTest.Info("the wheel (a row a notch, to the end and back) was NOT exercised in this run: another mod's copy keeps the strip and scrolls it, and this test can only drive this mod's copy");
                    }
                    if (total == Totals[Totals.Length - 1] && SettingsRows.TestKeepsStrip)
                    {
                        // the wheel: a row a notch, to the end and no further, and back
                        SettingsRows.TestStep(1);
                        yield return SelfTest.Frames(1);
                        SelfTest.Check(Mathf.Abs(SettingsRows.TestScrolledRows - 1f) < 0.01f, $"a wheel notch down scrolls one row ({SettingsRows.TestScrolledRows:0.##})");
                        SettingsRows.TestStep(1000);
                        yield return SelfTest.Frames(2);
                        float end = SettingsRows.TestScrolledRows;
                        GameObject last = rows[rows.Count - 1];
                        OnScreen((RectTransform)last.transform, out float lastBottom, out float lastTop);
                        OnScreen((RectTransform)strip.transform, out float stripBottom, out float stripTop);
                        SelfTest.Check(Mathf.Abs(end - (total - SettingsRows.MaxRows)) < 0.6f && lastBottom >= stripBottom - 1f && lastTop <= stripTop + 1f,
                            $"scrolled to its end: {end:0.##} rows down of {total - SettingsRows.MaxRows}, the last row (\"{SettingsRows.LabelOf(last)}\") inside the strip");
                        yield return SelfTest.PanelShot("settings-rows-" + total + "-end");
                        SettingsRows.TestStep(-1000);
                        yield return SelfTest.Frames(1);
                        SelfTest.Check(Mathf.Abs(SettingsRows.TestScrolledRows) < 0.01f, "and back to the top");
                        // a made-up row pressed opens nothing
                        GameObject made = null;
                        foreach (GameObject r in rows) if ((SettingsRows.LabelOf(r) ?? "").StartsWith("Example Mod", StringComparison.Ordinal)) { made = r; break; }
                        bool pressed = made != null && SettingsRows.TestClickArrow(made);
                        yield return SelfTest.Frames(2);
                        SelfTest.Check(pressed && !OptionsWindow.IsOpen && LazyWindowsStackController.ActiveWindow == settings, "a made-up row's arrow is pressed and opens nothing: the Settings window is still the window on top");
                    }
                }
                SettingsRows.SetExampleRows(0);
                yield return SelfTest.Frames(3);
                rows = SettingsRows.TestRows(settings);
                SelfTest.Check(rows.Count == real, $"the made-up rows taken away: the {real} installed row(s) are left ({rows.Count})");
            }
            finally
            {
                SettingsRows.SetExampleRows(was);
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                if (LazyWindowsStackController.IsWindowOpened(settings)) settings.Close();
            }
        }
    }
}
