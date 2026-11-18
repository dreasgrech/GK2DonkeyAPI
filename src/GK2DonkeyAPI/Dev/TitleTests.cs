using System;
using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // How the texts of our windows are drawn, against the game's own (the user, 2026-11-18, a day after 0.17.2 went
    // out: the Mount Journal's title was drawn thin where the release screenshot of 2026-10-31 has it bold; "we broke
    // something there, and no test picked it up"; then, of the test bench's panel: "all the fonts ... look weird and
    // extremely aliased", and "make sure we have tests that can catch these issues too").
    //
    // What was measured (the first form of this scenario, run alone on a fresh start, 2026-11-18 18:14):
    // - The game gives a text its font, material and colour when the text is switched on (TextStyleComponent.OnEnable).
    //   Before the Settings window was shown for the first time in a session, its header text and its OK button's text
    //   have no font at all.
    // - The window code's title bar copies the header's font: none, and a text given no font is drawn in TextMeshPro's
    //   default one, tiny_font. Both our titles were in tiny_font.
    // - Its bold font is the OK button's text's: none too, so every tab, heading and button is the labels' font widened.
    // - The game's fonts (small_font, small_font_bold, tiny_font) are pixel fonts made for one size, 16, in an atlas
    //   drawn without smoothing (read from the game's files): at any other size a letter's pixels are unevenly wide.
    //
    // All of it depends on what happened earlier in the session (once the Settings window was shown, the title and the
    // bold font are right), so a test that looks at the session as it finds it passes or fails by chance. This one
    // makes the worst case itself: it takes the fonts off the game's header and OK button text, as before their first
    // showing, opens our windows on that, looks, and puts back what it took.
    internal static class TitleTests
    {
        private static MountSettingsPage Page()
        {
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && OptionsWindow.HasOptions(live.Page)) return live;
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown) return live;
            return null;
        }

        private static string Look(TMP_Text t)
        {
            if (t == null) return "none";
            Material m = t.fontSharedMaterial;
            return $"font {(t.font != null ? t.font.name : "none")}, material {(m != null ? m.name : "none")}, size {t.fontSize:0.#}, style {t.fontStyle}, colour {ColorUtility.ToHtmlStringRGBA(t.color)}" +
                   $", {(t.gameObject.activeInHierarchy ? "shown" : "not shown")}";
        }

        // ---- the game's Settings window, as never shown ----

        // TextMeshPro's own two fields (a text's font and material as they are saved, before anything asked for them).
        private static readonly AccessTools.FieldRef<TMP_Text, TMP_FontAsset> FontOf = FontField();
        private static readonly AccessTools.FieldRef<TMP_Text, Material> MaterialOf = MaterialField();

        private static AccessTools.FieldRef<TMP_Text, TMP_FontAsset> FontField()
        {
            try { return AccessTools.FieldRefAccess<TMP_Text, TMP_FontAsset>("m_fontAsset"); }
            catch { return null; }
        }

        private static AccessTools.FieldRef<TMP_Text, Material> MaterialField()
        {
            try { return AccessTools.FieldRefAccess<TMP_Text, Material>("m_sharedMaterial"); }
            catch { return null; }
        }

        // The styles a button of the game's gives its texts by its state (LazyButton's own list).
        private static readonly AccessTools.FieldRef<LazyButton, List<TextTransition>> TransitionsOf = TransitionsField();

        private static AccessTools.FieldRef<LazyButton, List<TextTransition>> TransitionsField()
        {
            try { return AccessTools.FieldRefAccess<LazyButton, List<TextTransition>>("textTransitions"); }
            catch { return null; }
        }

        // The font the game's own style gives a text for the language now (what the game draws it in once it is shown).
        // A text has its style on itself (TextStyleComponent: the Settings header), or its button gives it one by the
        // button's state when the button is live (LazyButton's text transitions: the OK button's label has no style of
        // its own, the runs of 2026-11-18 18:53). button: the text's button, asked when the text has none itself.
        private static TMP_FontAsset StyleFont(TMP_Text t, out string style, LazyButton button = null)
        {
            style = "no text style";
            if (t == null) return null;
            try
            {
                TextStyleComponent c = t.GetComponent<TextStyleComponent>();
                TextStyle s = c != null ? c.CurrentTextStyle : null;
                bool buttons = false;
                if (s == null && button != null && TransitionsOf != null)
                {
                    List<TextTransition> transitions = TransitionsOf(button);
                    for (int i = 0; transitions != null && i < transitions.Count && s == null; i++)
                    {
                        if (transitions[i] != null && transitions[i].targetLabel == t) s = transitions[i].defaultStyle;
                    }
                    buttons = s != null;
                }
                if (s == null) return null;
                style = s.name + (buttons ? " (given by its button, at rest)" : "");
                return s.Font?.GetFontAssetFor(LLBase.CurrentLang, false);
            }
            catch (Exception e)
            {
                style = "its style couldn't be asked: " + e.Message;
                return null;
            }
        }

        // ---- what a window's texts are ----

        private sealed class Seen
        {
            internal string Window;
            internal bool HasTitle, TitleOnlyItsFont;
            internal string TitleText, TitleLook, TitleDrawn;
            internal TMP_FontAsset TitleFont;
            internal Material TitleMaterial;
            internal int Texts, PixelTexts, InBold;
            internal readonly List<string> NoFont = new List<string>(), OffSize = new List<string>(), MadeUpBold = new List<string>(), Shrunk = new List<string>();
            internal readonly List<string> Fonts = new List<string>();
        }

        // The fonts a text's visible characters are really drawn from ("small_font_bold x13"; two names: a stand-in).
        private static string DrawnFrom(TMP_Text t, out bool onlyItsFont)
        {
            t.ForceMeshUpdate();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            int other = 0, visible = 0;
            TMP_TextInfo info = t.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;
                visible++;
                string name = c.fontAsset != null ? c.fontAsset.name : "none";
                if (c.fontAsset != t.font) other++;
                if (!counts.ContainsKey(name)) { counts[name] = 0; order.Add(name); }
                counts[name]++;
            }
            onlyItsFont = visible > 0 && other == 0;
            var parts = new List<string>();
            foreach (string n in order) parts.Add(n + " x" + counts[n]);
            return visible == 0 ? "no visible character" : string.Join(", ", parts);
        }

        private static string Short(string s)
        {
            s = s.Replace('\n', ' ');
            return s.Length <= 28 ? s : s.Substring(0, 28) + "...";
        }

        private static Seen See(string window, Transform root, TMP_FontAsset bold)
        {
            var s = new Seen { Window = window };
            if (root == null) return s;
            var fonts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                s.Texts++;
                string what = $"\"{t.name}\"" + (string.IsNullOrEmpty(t.text) ? "" : $" ({Short(t.text)})");
                // the window's title: the text named "... title" inside its "... title bar" (UiKit.TitleBar)
                if (!s.HasTitle && t.name.EndsWith(" title", StringComparison.Ordinal) && t.transform.parent != null && t.transform.parent.name.EndsWith(" title bar", StringComparison.Ordinal))
                {
                    s.HasTitle = true;
                    s.TitleText = t.text;
                    s.TitleLook = Look(t);
                    s.TitleFont = t.font;
                    s.TitleMaterial = t.fontSharedMaterial;
                    s.TitleDrawn = DrawnFrom(t, out s.TitleOnlyItsFont);
                }
                if (t.font == null)
                {
                    s.NoFont.Add(what);
                    continue;
                }
                if (!fonts.ContainsKey(t.font.name)) { fonts[t.font.name] = 0; order.Add(t.font.name); }
                fonts[t.font.name]++;
                if (bold != null && t.font == bold) s.InBold++;
                if ((t.fontStyle & (FontStyles.Bold | FontStyles.Italic)) != 0) s.MadeUpBold.Add(what + " (" + t.fontStyle + ")");
                // a pixel font: its atlas is drawn point by point; any size but its own (or a whole multiple) is uneven
                Texture atlas = t.font.atlasTexture;
                float made = t.font.faceInfo.pointSize;
                if (atlas == null || atlas.filterMode != FilterMode.Point || made <= 0f) continue;
                s.PixelTexts++;
                // A text that sizes itself to fit (the long-text rule, docs/localization.md: a translation longer than
                // its room shrinks before it is cut) is judged by the size it has when its words fit; shrunk, it is
                // said, not failed: uneven pixels there are the price of not cutting the words.
                bool shrunk = t.enableAutoSizing && t.fontSizeMax > 0f && t.fontSize < t.fontSizeMax - 0.001f;
                float size = shrunk ? t.fontSizeMax : t.fontSize;
                float times = size / made;
                if (times < 0.999f || Mathf.Abs(times - Mathf.Round(times)) > 0.001f)
                {
                    s.OffSize.Add($"{what} at {size:0.##} in {t.font.name} (made for {made:0.##})");
                }
                else if (shrunk)
                {
                    s.Shrunk.Add($"{what} at {t.fontSize:0.##} of {size:0.##}");
                }
            }
            foreach (string n in order) s.Fonts.Add($"{n} x{fonts[n]}");
            return s;
        }

        private static string Some(List<string> items)
        {
            if (items.Count == 0) return "";
            var first = new List<string>();
            for (int i = 0; i < items.Count && i < 10; i++) first.Add(items[i]);
            return $": {items.Count} not so: {string.Join("; ", first)}{(items.Count > first.Count ? "; ..." : "")}";
        }

        // What one look at our windows gives both scenarios.
        private sealed class Looked
        {
            internal bool Done;
            internal string Language, HeadStyle, OkStyle;
            internal TMP_FontAsset TitleFont, BoldFont, HeldBold;
            internal Material WantMaterial;
            internal readonly List<Seen> Windows = new List<Seen>();
        }

        // Our two windows opened and looked at. neverShown: on the game's header and OK button text made as before
        // the Settings window's first showing (put back afterwards).
        private static IEnumerator LookAt(Looked l, bool neverShown)
        {
            MountSettingsPage page = Page();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page is shown");
                yield break;
            }
            // ours closed, the game's waited for: the scenario needs no window open and asks nobody to see to it
            if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: true, back: false);
            if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
            yield return SelfTest.Until(() => LazyWindowsStackController.ActiveWindow == null, 10f);
            if (LazyWindowsStackController.ActiveWindow != null)
            {
                SelfTest.Skip($"a window of the game's is open and stayed open for 10 s ({LazyWindowsStackController.ActiveWindow.name}): ours can't be opened over it here");
                yield break;
            }
            UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
            Transform hg = settings != null ? settings.transform.Find("GenericWIndowLayout/Frame/HeaderGroup") : null;
            TextMeshProUGUI head = hg != null ? hg.Find("Header")?.GetComponent<TextMeshProUGUI>() : null;
            if (head == null)
            {
                SelfTest.Check(false, $"the game's Settings window has its header text at GenericWIndowLayout/Frame/HeaderGroup/Header ({(settings == null ? "no Settings window" : hg == null ? "no HeaderGroup" : "no Header text")}): our title bars read their font there");
                yield break;
            }
            UIDialogWindowButton okButton = UiKit.OkButtonOf(settings);
            TextMeshProUGUI ok = okButton != null ? okButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            // 1. the game's own texts as the session left them, and what its own styles give them
            l.Language = LLBase.CurrentLang;
            l.TitleFont = StyleFont(head, out l.HeadStyle);
            l.BoldFont = StyleFont(ok, out l.OkStyle, okButton != null ? okButton.LazyButton : null);
            SelfTest.Info($"the game's Settings header as this session left it: {Look(head)}; its text style: {l.HeadStyle}, which gives {(l.TitleFont != null ? l.TitleFont.name : "no font")} for {l.Language}");
            SelfTest.Info($"the game's Settings OK button's text as this session left it: {Look(ok)}; its text style: {l.OkStyle}, which gives {(l.BoldFont != null ? l.BoldFont.name : "no font")} for {l.Language}");

            var took = new List<TMP_Text>();
            var tookFonts = new List<TMP_FontAsset>();
            var tookMaterials = new List<Material>();
            try
            {
                // 2. the worst case, made here: both without a font, as before the Settings window's first showing
                if (!neverShown)
                {
                    // the sizes don't depend on it
                }
                else if (FontOf == null || MaterialOf == null)
                {
                    SelfTest.Info("TextMeshPro keeps a text's font elsewhere than it did (m_fontAsset, m_sharedMaterial): the game's texts are left as they are, and this run shows only this session's case");
                }
                else if (settings.gameObject.activeInHierarchy)
                {
                    SelfTest.Info("the game's Settings window is shown: its texts are left as they are, and this run shows only this session's case");
                }
                else
                {
                    foreach (TextMeshProUGUI t in new[] { head, ok })
                    {
                        if (t == null) continue;
                        took.Add(t);
                        tookFonts.Add(FontOf(t));
                        tookMaterials.Add(MaterialOf(t));
                        FontOf(t) = null;
                        MaterialOf(t) = null;
                    }
                    SelfTest.Info($"the game's Settings header and OK button's text made as before their first showing: {Look(head)}");
                }

                // 3. our windows, opened on that
                MountsPanel.Open(page, toggle: false);
                yield return SelfTest.Frames(2);
                UiKit ui = MountsPanel.TestUi;
                l.HeldBold = ui?.BoldFont;
                SelfTest.Info($"the journal's window code holds: labels' font {(ui?.Font != null ? ui.Font.name : "none")} (material {(ui?.FontMaterial != null ? ui.FontMaterial.name : "none")}, size {(ui != null ? ui.FontSize : 0f):0.##}), bold font {(ui?.BoldFont != null ? ui.BoldFont.name : "none")} (material {(ui?.BoldMaterial != null ? ui.BoldMaterial.name : "none")})");
                l.Windows.Add(See("the Mount Journal", MountsPanel.TestRoot(false), l.BoldFont));
                MountsPanel.TestClose(keep: true, back: true);
                yield return SelfTest.Frames(2);
                if (OptionsWindow.HasOptions(page.Page))
                {
                    OptionsWindow.Open(page.Page);
                    yield return SelfTest.Frames(2);
                    l.Windows.Add(See("the options window", OptionsWindow.TestRoot, l.BoldFont));
                    OptionsWindow.TestClose(back: true);
                    yield return SelfTest.Frames(2);
                }

                // 4. the game's title material: the header with its own style applied, as the game does when the window is
                // switched on
                if (neverShown)
                {
                    try { head.GetComponent<TextStyleComponent>()?.ApplyStyle(); }
                    catch (Exception e) { SelfTest.Info("the header's style couldn't be applied: " + e.Message); }
                    l.WantMaterial = head.fontSharedMaterial;
                    SelfTest.Info($"the game's Settings header with its own style applied ({l.Language}): {Look(head)}");
                }
                l.Done = true;
            }
            finally
            {
                if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: true, back: false);
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                // what was taken is put back, whatever was done to the texts meanwhile (the game styles a text again when
                // it is shown)
                for (int i = 0; i < took.Count; i++)
                {
                    if (took[i] == null) continue;
                    FontOf(took[i]) = tookFonts[i];
                    MaterialOf(took[i]) = tookMaterials[i];
                }
            }
        }

        // The titles and the bold font: the window code's, and the fault of the released 0.17.2.
        internal static IEnumerator Titles()
        {
            var l = new Looked();
            yield return LookAt(l, neverShown: true);
            if (!l.Done) yield break;
            SelfTest.Check(l.TitleFont != null, $"the game's window title style names a font for the language ({l.HeadStyle}: {(l.TitleFont != null ? l.TitleFont.name : "none")})");
            if (l.BoldFont != null)
            {
                SelfTest.Check(l.HeldBold == l.BoldFont, $"the window code's bold font is the game's own for its buttons ({(l.HeldBold != null ? l.HeldBold.name : "none: the labels' font is widened instead")}; the game's: {l.BoldFont.name})");
            }
            else
            {
                SelfTest.Check(l.HeldBold != null, $"the window code holds a bold font ({(l.HeldBold != null ? l.HeldBold.name : "none: the labels' font is widened instead")}; the game's OK button's text has no style to ask which it should be)");
            }
            foreach (Seen s in l.Windows)
            {
                SelfTest.Info($"{s.Window}: {s.Texts} text(s) in {string.Join(", ", s.Fonts)}");
                if (!s.HasTitle)
                {
                    SelfTest.Check(false, $"{s.Window} has a title text in a title bar");
                    continue;
                }
                SelfTest.Info($"{s.Window}'s title \"{s.TitleText}\": {s.TitleLook}; its characters drawn from: {s.TitleDrawn}");
                SelfTest.Check(s.TitleFont != null && s.TitleFont == l.TitleFont, $"{s.Window}'s title is in the game's window title font ({(s.TitleFont != null ? s.TitleFont.name : "none")}; the game's: {(l.TitleFont != null ? l.TitleFont.name : "none")})");
                SelfTest.Check(s.TitleOnlyItsFont, $"every character of it is drawn from that font, none from a stand-in ({s.TitleDrawn})");
                SelfTest.Check(s.TitleMaterial != null && l.WantMaterial != null && s.TitleMaterial.name == l.WantMaterial.name,
                    $"and with the game's title material ({(s.TitleMaterial != null ? s.TitleMaterial.name : "none")}; the game's: {(l.WantMaterial != null ? l.WantMaterial.name : "none")})");
                SelfTest.Check(s.MadeUpBold.Count == 0, $"no text of {s.Window} is a pixel font widened or slanted by a text style (TextMeshPro's bold or italic){Some(s.MadeUpBold)}");
                if (l.BoldFont != null) SelfTest.Check(s.InBold > 0, $"{s.Window}'s tabs, headings and buttons are in the game's bold font, {l.BoldFont.name} ({s.InBold} texts in it)");
            }
        }

        // The sizes: every text in one of the game's pixel fonts at the size the font is made for. Our windows' own
        // (the size factors of their texts), whatever the session did before.
        internal static IEnumerator Sizes()
        {
            var l = new Looked();
            yield return LookAt(l, neverShown: false);
            if (!l.Done) yield break;
            foreach (Seen s in l.Windows)
            {
                SelfTest.Info($"{s.Window}: {s.Texts} text(s) in {string.Join(", ", s.Fonts)}; {s.PixelTexts} of them in a pixel font (the others are smoothed: any size is fine there)");
                SelfTest.Check(s.Texts > 0 && s.NoFont.Count == 0, $"every text of {s.Window} has a font ({s.Texts} texts){Some(s.NoFont)}");
                SelfTest.Check(s.OffSize.Count == 0, $"every text of {s.Window} in a pixel font is at the size the font is made for, or a whole multiple of it ({s.PixelTexts} such texts){Some(s.OffSize)}");
                if (s.Shrunk.Count > 0) SelfTest.Info($"{s.Window}: {s.Shrunk.Count} text(s) are smaller only because their words don't fit at the font's size (the long-text rule){Some(s.Shrunk)}");
            }
        }
    }
}
