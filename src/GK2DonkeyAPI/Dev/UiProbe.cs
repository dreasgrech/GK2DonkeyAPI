using System.IO;
using System.Text;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The UI probe (the user, 2026-10-27: our Mounts panel's frame shows a margin the game's windows don't, and our buttons'
    // font differs from theirs; "investigate this"). Dev only and off by default (Debug.UiProbe): when the Mounts panel is
    // built it writes BepInEx\GK2-ui-probe.txt with the game's Settings window layout (its GenericWIndowLayout tree: every
    // node's rect, image sprite and slicing, text font and material), the dialog button the panel copies, the font the panel
    // reads, and the buttons of the pause menu if it is loaded. Measures only; nothing is changed.
    internal static class UiProbe
    {
        internal static bool On => DevTools.Unlocked && Plugin.UiProbeOn != null && Plugin.UiProbeOn.Value;

        internal static void Dump(Component settings, Component dialogButton, TMP_FontAsset panelFont, Material panelMaterial, float panelFontSize)
        {
            if (!On) return;
            var sb = new StringBuilder(32 * 1024);
            sb.Append("# GK2 UI probe ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            sb.Append("# the panel reads: font ").Append(panelFont != null ? panelFont.name : "none").Append(", material ")
              .Append(panelMaterial != null ? panelMaterial.name : "none").Append(", size ").Append(panelFontSize).Append('\n');
            if (settings != null)
            {
                sb.Append("\n== the Settings window: ").Append(settings.name).Append('\n');
                Transform layout = settings.transform.Find("GenericWIndowLayout");
                Tree(sb, layout != null ? layout : settings.transform, 0, 4);
            }
            if (dialogButton != null)
            {
                sb.Append("\n== the dialog button the panel copies: ").Append(dialogButton.name).Append('\n');
                Tree(sb, dialogButton.transform, 0, 3);
            }
            sb.Append("\n== buttons with text anywhere loaded (the pause menu's among them when it was opened)\n");
            int n = 0;
            foreach (TextMeshProUGUI t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                if (t == null || n >= 40) continue;
                if (t.GetComponentInParent<Button>(true) == null && t.GetComponentInParent<Selectable>(true) == null) continue;
                string s = t.text ?? "";
                if (s.Length == 0 || s.Length > 30) continue;
                n++;
                sb.Append(Path(t.transform)).Append(" \"").Append(s).Append("\" ").Append(TextInfo(t)).Append('\n');
            }
            sb.Append("\n== windows loaded with a title-bar-like child (Header / Title / Top)\n");
            int w = 0;
            foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rt == null || w >= 20) continue;
                string nm = rt.name;
                if (nm != "Header" && nm != "Title" && nm != "TitleBar" && nm != "Top" && nm != "Caption") continue;
                if (rt.GetComponentInParent<Canvas>(true) == null) continue;
                w++;
                sb.Append("\n-- ").Append(Path(rt)).Append('\n');
                Tree(sb, rt, 0, 3);
            }
            try
            {
                string file = System.IO.Path.Combine(Paths.BepInExRootPath, "GK2-ui-probe.txt");
                File.WriteAllText(file, sb.ToString());
                Plugin.Log.LogInfo("[ui-probe] written: " + file);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[ui-probe] could not write: " + e.Message);
            }
        }

        private static void Tree(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (t == null) return;
            sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (inactive)");
            if (t is RectTransform rt)
            {
                sb.Append(" rect ").Append(F(rt.rect.width)).Append('x').Append(F(rt.rect.height))
                  .Append(" anchors ").Append(V(rt.anchorMin)).Append('-').Append(V(rt.anchorMax))
                  .Append(" offsets min ").Append(V(rt.offsetMin)).Append(" max ").Append(V(rt.offsetMax))
                  .Append(" pivot ").Append(V(rt.pivot));
            }
            Image img = t.GetComponent<Image>();
            if (img != null)
            {
                Sprite s = img.sprite;
                sb.Append(" | image ").Append(s != null ? s.name : "none").Append(' ').Append(img.type)
                  .Append(" colour ").Append(ColorUtility.ToHtmlStringRGBA(img.color));
                if (s != null) sb.Append(" sprite ").Append(F(s.rect.width)).Append('x').Append(F(s.rect.height)).Append(" border ").Append(s.border).Append(" ppu ").Append(F(s.pixelsPerUnit));
                sb.Append(" ppuMult ").Append(F(img.pixelsPerUnitMultiplier));
            }
            TextMeshProUGUI tx = t.GetComponent<TextMeshProUGUI>();
            if (tx != null) sb.Append(" | text \"").Append(Short(tx.text)).Append("\" ").Append(TextInfo(tx));
            if (t.GetComponent<Button>() != null) sb.Append(" | button");
            if (t.GetComponent<LayoutGroup>() is LayoutGroup lg) sb.Append(" | layout ").Append(lg.GetType().Name).Append(" padding ").Append(lg.padding.left).Append(',').Append(lg.padding.top).Append(',').Append(lg.padding.right).Append(',').Append(lg.padding.bottom);
            sb.Append('\n');
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) Tree(sb, t.GetChild(i), depth + 1, maxDepth);
        }

        private static string TextInfo(TextMeshProUGUI t) =>
            $"font {(t.font != null ? t.font.name : "none")} material {(t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "none")} size {F(t.fontSize)} style {t.fontStyle} colour {ColorUtility.ToHtmlStringRGBA(t.color)} outline {F(t.outlineWidth)} {ColorUtility.ToHtmlStringRGBA(t.outlineColor)} spacing {F(t.characterSpacing)}";

        private static string Path(Transform t)
        {
            string p = t.name;
            for (Transform x = t.parent; x != null && p.Length < 200; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 40 ? s.Substring(0, 40) + "..." : s).Replace("\n", " ");
        private static string F(float f) => f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        private static string V(Vector2 v) => "(" + F(v.x) + "," + F(v.y) + ")";
    }
}
