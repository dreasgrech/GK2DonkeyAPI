using System;
using System.Collections.Generic;
using System.Text;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The Mounts panel (MountSettingsPage.PanelButton / OpenPanel; docs/mounts-panel-design.md): a journal of the mounts
    // a player can ride. A card list on the left (an icon and the name each, scrolling), the chosen mount on the right:
    // its portrait, name and description, the live picture of it with its cart on a dark stage, a line of what it does,
    // and under tabs (Skin, Colours, Effect) the page's look rows for it as the game's own rows. Done keeps what was
    // chosen, Cancel puts back what was set when the panel opened. Only in a game.
    //
    // The first panel (2026-10-20) was built out of nested auto-sizing layout groups and was shrunk to fit at 1080p; this
    // one is a window of a fixed size (1200x660 on the game's 1280x720 canvas) laid out by hand, scaled only on a
    // smaller canvas. The game's parts it uses: the window frame sprite, its dialog button (copied for the cards, the
    // tabs, Done and Cancel), its font (read off one of its labels), and its "< value >" rows (SettingsRows) for the
    // tabs' content. If a part can't be found, the panel doesn't open and the log says so.
    internal static class MountsPanel
    {
        private const float W = 1000f, H = 600f, Pad = 20f, Gap = 8f;
        private const float TitleH = 28f, ListW = 260f, HeaderH = 76f, StatsH = 32f, TabsH = 30f, ButtonsH = 40f, TabW = 110f, CardH = 46f;
        private const float StageMin = 170f, StageMax = 270f, PictureScale = 3f;
        private const float IconPx = 36f, HeaderIconPx = 64f;
        private const float EdgeGap = 24f; // the least room left at the screen's edges before the window is scaled down

        private static GameObject panel;
        private static MountSettingsPage page;
        private static GameObject templateRow;
        private static UIDialogWindowButton dialogTemplate;
        private static Sprite buttonSprite;      // the game's dialog button face, for the panel's own cards and tabs
        private static Color buttonTextColor = Color.white;
        private static TMP_FontAsset font;
        private static Material fontMaterial;
        private static Color fontColor = Color.white;
        private static float fontSize = 18f;
        private static float rowH = 33f;
        private static Rect rowVisual = new Rect(0f, 0f, 400f, 33f); // where a copied game row really draws, relative to its rectangle's top left: x and y its offsets (negative: outside), its drawn width and height

        private static RectTransform listContent, rightArea, stage, tabsBar, contentRoot;
        private static ScrollRect listScroll, contentScroll;
        private static RawImage headerIcon, picture;
        private static TextMeshProUGUI titleCount, headerName, headerDesc, statsText, statsText2;
        private static Texture2D pictureTex;
        private static float nextPicture;
        private static bool pictureOff;

        private static readonly List<(string Id, CanvasGroup Group)> cards = new List<(string, CanvasGroup)>();
        private static readonly List<(string Name, GameObject Go, CanvasGroup Group)> tabButtons = new List<(string, GameObject, CanvasGroup)>();
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> rows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();
        private static readonly List<(string Name, List<(string Group, List<MountSettingsPage.Row> Rows)> Groups)> tabs = new List<(string, List<(string, List<MountSettingsPage.Row>)>)>();
        private static List<Entry> entries = new List<Entry>();
        private static string builtFor;        // the mount the right side was built for
        private static string tab;             // the tab shown, remembered while the game runs
        private static string contentSignature; // which rows the tab's content was built with (a preset shows a row: rebuilt)
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

        // One mount of the list: what the panel says about it. Built from the registry alone (the self-tests read it
        // without a window).
        internal sealed class Entry
        {
            internal string Id, Label, Description;
            internal readonly List<string> Lines = new List<string>();  // what it does: the cart, babies, speed, what it comes with, its skins
            internal readonly List<string> Extra = new List<string>();  // its own stats and abilities
            internal bool HasPortrait;
        }

        internal static bool IsOpen => panel != null && panel.activeSelf;

        // Every mount a player can pick (Looks.KindChoices: the kinds with a skin, the donkey first), with its words.
        internal static List<Entry> Entries()
        {
            var list = new List<Entry>();
            foreach ((string id, string label) in Looks.KindChoices())
            {
                Kinds.KindDef k = Kinds.Find(id);
                if (k == null) continue;
                var e = new Entry { Id = id, Label = label };
                Looks.SkinDef def = Looks.Find(Looks.DefaultSkinOf(k));
                e.Description = !string.IsNullOrEmpty(k.Description) ? k.Description : def?.Description ?? (k == Kinds.Donkey ? "The game's own donkey, as it draws it." : "");
                e.HasPortrait = k.PortraitTexture != null || !string.IsNullOrEmpty(k.Portrait) || (def != null && (def.PortraitTexture != null || !string.IsNullOrEmpty(def.Portrait)));
                e.Lines.Add(k.Cart ? "Pulls the cart" : "Can't pull the cart");
                e.Lines.Add(k.Foals ? (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f ? $"Has small babies ({k.FoalScale:0.##} of its size)" : "Has babies") : "No babies");
                object speed = ValueOf(k, "SpeedMultiplier");
                if (speed != null) e.Lines.Add("Speed: " + SpeedText(speed));
                if (!string.IsNullOrEmpty(k.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(k.Effect)?.Name ?? k.Effect) + (k.EffectWhen == MountEffectWhen.Night ? " (at night)" : ""));
                else if (def != null && !string.IsNullOrEmpty(def.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(def.Effect)?.Name ?? def.Effect));
                int skins = (k == Kinds.Donkey ? 1 : 0);
                foreach (Looks.SkinDef sd in Kinds.SkinsOf(k)) if (sd.Unavailable == null) skins++; // the ones a player can pick
                if (skins > 1) e.Lines.Add(skins + " skins");
                if (k.Stats != null) foreach (var st in k.Stats) e.Extra.Add(string.IsNullOrEmpty(st.Value) ? st.Key : st.Key + ": " + st.Value);
                foreach (MountAbility a in k.Abilities)
                {
                    string name = string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name;
                    e.Extra.Add(string.IsNullOrEmpty(a.Description) ? name : name + ": " + a.Description);
                }
                list.Add(e);
            }
            return list;
        }

        private static object ValueOf(Kinds.KindDef k, string key)
        {
            object v = null;
            if (k.Tuning != null && k.Tuning.TryGetValue(key, out object t)) v = t;
            if (k.TuningRide != null && k.TuningRide.TryGetValue(key, out object r)) v = r;
            return v;
        }

        private static string SpeedText(object v)
        {
            if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) v = f;
            try
            {
                if (v is IConvertible && !(v is string)) v = Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture); // a code kind's int or double
            }
            catch
            {
            }
            return v is float x ? x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " times walking speed" : v.ToString(); // SpeedMultiplier: times the keeper's walking speed (docs/reference.md)
        }

        internal static void Open(MountSettingsPage p, bool toggle)
        {
            try
            {
                if (!Mount.IsGameReady)
                {
                    Plugin.Log.LogInfo("Settings: the Mounts panel opens in a game only (load your game first)."); // no keeper to say it in the main menu
                    return;
                }
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                if (settings == null)
                {
                    throw new InvalidOperationException("the game's Settings window isn't there to copy from");
                }
                if (IsOpen && page == p)
                {
                    if (toggle)
                    {
                        Close(keep: true); // the key again: the panel goes, what was chosen stays
                    }
                    else
                    {
                        OutOfTheWay(settings); // the button again (the menus were over the panel): they go, the panel stays as it is
                        FitToScreen();
                    }
                    return;
                }
                Close(keep: true);
                page = p;
                OutOfTheWay(settings);
                Build(settings);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the Mounts panel can't open (" + e.Message + "); the mount and its colours are still in the cfg.");
                Close(keep: true);
            }
        }

        // Out of the way: the Settings window (without reopening the pause menu) and the pause menu.
        private static void OutOfTheWay(UIGameSettingsWindow settings)
        {
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
        }

        // ---- building ----

        private static void Build(UIGameSettingsWindow settings)
        {
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            dialogTemplate = settings.GetComponentInChildren<UIDialogWindowButton>(true);
            if (template == null || dialogTemplate == null)
            {
                throw new InvalidOperationException("no row or button to copy in the Settings window");
            }
            Transform content = SettingsRows.ContentOf(template.transform);
            templateRow = SettingsRows.RowGameObject(template.transform, content);
            Image frame = settings.transform.Find("GenericWIndowLayout/Frame")?.GetComponent<Image>();

            panel = new GameObject("GK2DonkeyAPI mounts panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            panel.transform.SetParent(settings.transform.parent, false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.15f, 0.97f);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(W, H);
            if (frame != null && frame.sprite != null)
            {
                Image bi = Node("frame", panel.transform, typeof(Image)).GetComponent<Image>();
                bi.sprite = frame.sprite;
                bi.type = Image.Type.Sliced;
                bi.raycastTarget = false;
                Stretch((RectTransform)bi.transform);
            }
            ReadFont();
            buttonSprite = null;
            foreach (Image img in dialogTemplate.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null) { buttonSprite = img.sprite; break; }
            }
            TextMeshProUGUI bt = dialogTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
            if (bt != null) buttonTextColor = bt.color;
            rowH = Mathf.Max(24f, ((RectTransform)templateRow.transform).rect.height);
            MeasureRow();

            // What was set when the panel opened, for Cancel: every look row of the page (the mount, every mount's
            // skin and colours, the effect), not only the chosen mount's, so Cancel after choosing another mount puts
            // the first one back.
            before.Clear();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.IsLook && r.Entry != null) before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
            }

            // the title line
            Text(panel.transform, "title", "Mounts", 1.25f, TextAlignmentOptions.Left, false, Pad, Pad, 400f, TitleH);
            titleCount = Text(panel.transform, "count", "", 0.9f, TextAlignmentOptions.Right, false, W - Pad - 300f, Pad + 2f, 300f, TitleH);

            // the list, scrolling
            float listTop = Pad + TitleH + Gap;
            float listH = H - listTop - Gap - ButtonsH - Pad;
            listScroll = ScrollView(panel.transform, "list", Pad, listTop, ListW, listH, out listContent, 6f);

            // the right side
            float x0 = Pad + ListW + 2f * Gap, rw = W - Pad - x0;
            rightArea = Rect(Node("right", panel.transform), x0, listTop, rw, H - listTop - Gap - ButtonsH - Pad);
            headerIcon = Node("portrait", rightArea, typeof(RawImage)).GetComponent<RawImage>();
            headerIcon.raycastTarget = false;
            Rect(headerIcon.gameObject, 0f, 4f, HeaderIconPx, HeaderIconPx);
            headerIcon.gameObject.SetActive(false);
            headerName = Text(rightArea, "name", "", 1.35f, TextAlignmentOptions.Left, false, HeaderIconPx + 12f, 0f, rw - HeaderIconPx - 12f, 32f);
            headerDesc = Text(rightArea, "description", "", 0.9f, TextAlignmentOptions.TopLeft, true, HeaderIconPx + 12f, 32f, rw - HeaderIconPx - 12f, HeaderH - 32f);
            headerDesc.overflowMode = TextOverflowModes.Ellipsis;

            stage = Rect(Node("stage", rightArea, typeof(Image), typeof(Outline)), 0f, HeaderH + Gap, rw, StageMax);
            var stageImage = stage.GetComponent<Image>();
            stageImage.color = new Color(0f, 0f, 0f, 0.35f);
            stageImage.raycastTarget = false;
            var outline = stage.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
            outline.effectDistance = new Vector2(1f, -1f);
            picture = Node("picture", stage, typeof(RawImage)).GetComponent<RawImage>();
            picture.raycastTarget = false;
            var pic = (RectTransform)picture.transform;
            pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
            pic.pivot = new Vector2(0.5f, 0.5f);
            picture.gameObject.SetActive(false);

            statsText = Text(rightArea, "stats", "", 0.85f, TextAlignmentOptions.Center, true, 0f, 0f, rw, StatsH);
            statsText2 = Text(rightArea, "stats 2", "", 0.8f, TextAlignmentOptions.Center, true, 0f, 0f, rw, StatsH);
            statsText2.color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.8f);
            tabsBar = Rect(Node("tabs", rightArea), 0f, 0f, rw, TabsH);
            contentScroll = ScrollView(rightArea, "content", 0f, 0f, rw, 100f, out contentRoot, 2f, manual: true);

            // Done and Cancel, bottom right
            GameObject cancel = Button(dialogTemplate, panel.transform, "Cancel", () => Close(keep: false));
            BottomRight((RectTransform)cancel.transform, Pad);
            GameObject done = Button(dialogTemplate, panel.transform, "Done", () => Close(keep: true));
            BottomRight((RectTransform)done.transform, Pad + Mathf.Max(140f, ((RectTransform)cancel.transform).rect.width + 16f));

            entries = Entries();
            pictureOff = false;
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Kinds.KindsChanged -= OnRegistryChanged;
            Mounts.Looks.SkinsChanged += OnRegistryChanged;
            Mounts.Kinds.KindsChanged += OnRegistryChanged;
            BuildList();
            BuildRight();
            panel.SetActive(true);
            FitToScreen();
            Plugin.Log.LogInfo($"Settings: the Mounts panel of {page.Title} is open ({entries.Count} mount(s), {rows.Count} rows on the {tab} tab for {builtFor}{(panel.transform.localScale.x < 1f ? $", scaled to {panel.transform.localScale.x:0.##} to fit the screen" : "")}).");
        }

        // The list: a card per mount (its icon and name), the chosen one bright, the others dimmed; scrolled to the chosen one.
        private static void BuildList()
        {
            foreach (Transform c in listContent) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            cards.Clear();
            string chosen = page?.MountChosen ?? MountKinds.DonkeyId;
            int at = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                string id = e.Id;
                GameObject go = OwnButton(listContent, "card " + id, e.Label, ListW - 16f, CardH, () => Select(id), textLeft: IconPx + 14f);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = ListW - 16f;
                le.preferredHeight = CardH;
                le.minHeight = CardH;
                MountIcons.Icon icon = MountIcons.Of(Kinds.Find(id));
                if (icon.Any)
                {
                    RawImage img = Node("icon", go.transform, typeof(RawImage)).GetComponent<RawImage>();
                    img.raycastTarget = false;
                    Show(img, icon, IconPx);
                    var irt = (RectTransform)img.transform;
                    irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                    irt.pivot = new Vector2(0f, 0.5f);
                    irt.anchoredPosition = new Vector2(8f + (IconPx - irt.sizeDelta.x) / 2f, 0f);
                }
                cards.Add((id, go.GetComponent<CanvasGroup>()));
                if (id.Equals(chosen, StringComparison.OrdinalIgnoreCase)) at = i;
            }
            titleCount.text = entries.Count == 1 ? "1 mount" : entries.Count + " mounts";
            MarkList();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (at >= 0 && entries.Count > 1) listScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(at / (float)(entries.Count - 1));
        }

        private static void MarkList()
        {
            string chosen = page?.MountChosen ?? MountKinds.DonkeyId;
            foreach ((string id, CanvasGroup g) in cards)
            {
                if (g != null) g.alpha = id.Equals(chosen, StringComparison.OrdinalIgnoreCase) ? 1f : 0.55f;
            }
        }

        // A mount chosen in the list: the page's Mount row takes it (its apply heals the skin and sets the companion,
        // and every per-mount row's condition flips with it), then the right side is built again for it.
        private static void Select(string id)
        {
            if (page == null) return;
            page.SelectMount(id);
            BuildRight();
            MarkList();
        }

        // The right side for the chosen mount: the header, the stats lines, the tabs (the page's look sections that have
        // rows for this mount), then the tab's content.
        private static void BuildRight()
        {
            string chosen = page.MountChosen ?? MountKinds.DonkeyId;
            builtFor = chosen;
            Entry e = entries.Find(x => x.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            Kinds.KindDef k = Kinds.Find(chosen);
            MountIcons.Icon icon = MountIcons.Of(k);
            headerIcon.gameObject.SetActive(icon.Any);
            if (icon.Any) Show(headerIcon, icon, HeaderIconPx);
            float textX = icon.Any ? HeaderIconPx + 12f : 0f;
            Rect(headerName.gameObject, textX, 0f, rightArea.rect.width - textX, 32f);
            Rect(headerDesc.gameObject, textX, 32f, rightArea.rect.width - textX, HeaderH - 32f);
            if (e != null)
            {
                headerName.text = e.Label;
                headerDesc.text = e.Description;
                statsText.text = string.Join("   ·   ", e.Lines);
                statsText2.text = string.Join("   ·   ", e.Extra);
            }
            else
            {
                // the chosen mount is not in the list: its mod or pack is gone, or its every skin is set aside (a game-source
                // skin whose frames the game lacks); the choice is kept, the donkey comes instead, and the panel says so
                string why = null;
                if (k != null) foreach (Looks.SkinDef sd in Kinds.SkinsOf(k)) if (sd.Unavailable != null) { why = sd.Unavailable; break; }
                headerName.text = k?.Label ?? chosen;
                headerDesc.text = k == null ? "Not installed: the mod or pack that had this mount is gone. Your choice is kept; the donkey comes instead."
                    : why != null ? "Set aside: the game no longer has the pictures this mount's skin needs (the log names them). Your choice is kept; the donkey comes instead."
                    : "It has no skin to wear. Your choice is kept; the donkey comes instead.";
                statsText.text = "";
                statsText2.text = "";
            }
            statsText2.gameObject.SetActive(statsText2.text.Length > 0);

            // the tabs: a look section of the page with rows for this mount; inside a tab, the groups under its look headings
            tabs.Clear();
            string heading = null, headingSection = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook) continue;
                if (r.Kind == MountSettingsPage.Kind.Note)
                {
                    heading = r.Label;
                    headingSection = r.ShownSection;
                    continue;
                }
                if (r.Shown != null && !r.Shown()) continue; // another mount's row
                if (page.IsMountRow(r)) continue; // the list is the picker
                string tabName = TabName(r.ShownSection);
                string group = heading != null && r.ShownSection == headingSection ? heading : null;
                int ti = tabs.FindIndex(t => t.Name == tabName);
                if (ti < 0)
                {
                    tabs.Add((tabName, new List<(string, List<MountSettingsPage.Row>)>()));
                    ti = tabs.Count - 1;
                }
                int gi = tabs[ti].Groups.FindIndex(g => g.Group == group);
                if (gi < 0)
                {
                    tabs[ti].Groups.Add((group, new List<MountSettingsPage.Row>()));
                    gi = tabs[ti].Groups.Count - 1;
                }
                tabs[ti].Groups[gi].Rows.Add(r);
            }
            if (tab == null || !tabs.Exists(t => t.Name == tab)) tab = tabs.Count > 0 ? tabs[0].Name : null;
            foreach (Transform c in tabsBar) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            tabButtons.Clear();
            float tx = 0f;
            foreach (var t in tabs)
            {
                string name = t.Name;
                GameObject go = OwnButton(tabsBar, "tab " + name, name, TabW, TabsH, () => ShowTab(name));
                Rect(go, tx, 0f, TabW, TabsH);
                tx += TabW + 8f;
                tabButtons.Add((name, go, go.GetComponent<CanvasGroup>()));
            }
            BuildTab();
        }

        private static string TabName(string section)
        {
            if (string.Equals(section, "Mount", StringComparison.OrdinalIgnoreCase)) return "Skin";
            return section ?? "Look";
        }

        private static void ShowTab(string name)
        {
            if (tab == name) return;
            tab = name;
            BuildTab();
        }

        // The tab's content: its groups as columns (two side by side, more stacked), each a heading and the rows shown now;
        // a row shown only with a preset (a custom colour) is left out until the preset is chosen. The stage takes the
        // room the rows don't need; the rows scroll when they need more than is left.
        private static void BuildTab()
        {
            foreach (var (name, go, group) in tabButtons)
            {
                if (group != null) group.alpha = name == tab ? 1f : 0.55f;
            }
            foreach (Transform c in contentRoot) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            rows.Clear();
            var sig = new StringBuilder(tab ?? "");
            float needed = 0f;
            int ti = tabs.FindIndex(t => t.Name == tab);
            if (ti >= 0)
            {
                // the groups as columns (two side by side, more in further rows), centred in the tab area; each row and
                // heading placed where its drawing (not its rectangle) lands: rowVisual says where that is
                var groups = tabs[ti].Groups;
                int perRow = groups.Count > 1 ? 2 : 1;
                float colW = rowVisual.width, colGap = 16f, step = rowVisual.height + 4f;
                float areaW = rightArea.rect.width;
                float y = 4f;
                for (int g0 = 0; g0 < groups.Count; g0 += perRow)
                {
                    int cols = Math.Min(perRow, groups.Count - g0);
                    float totalW = cols * colW + (cols - 1) * colGap;
                    float x0 = Math.Max(0f, (areaW - totalW) / 2f);
                    float rowTop = y, tallest = 0f;
                    for (int ci = 0; ci < cols; ci++)
                    {
                        var group = groups[g0 + ci];
                        float cx = x0 + ci * (colW + colGap), cy = rowTop;
                        if (group.Group != null)
                        {
                            GameObject h = SettingsRows.MakeHeading(templateRow, contentRoot, page, group.Group);
                            Place(h, cx - rowVisual.x, cy - rowVisual.y);
                            cy += step;
                        }
                        foreach (MountSettingsPage.Row r in group.Rows)
                        {
                            bool visible = r.Visible == null || r.Visible();
                            sig.Append(visible ? '1' : '0');
                            if (!visible) continue; // shown only with a preset: not now
                            GameObject go = SettingsRows.MakeRow(templateRow, contentRoot, r, page, out Action refresh);
                            go.SetActive(true); // a copied row starts switched off (SettingsRows.Copy)
                            Place(go, cx - rowVisual.x, cy - rowVisual.y);
                            CanvasGroup grp = r.Enabled != null ? go.AddComponent<CanvasGroup>() : null;
                            rows.Add((r, go, refresh, grp));
                            cy += step;
                        }
                        tallest = Math.Max(tallest, cy - rowTop);
                    }
                    y = rowTop + tallest + 8f;
                }
                needed = y;
            }
            contentSignature = sig.ToString();
            contentRoot.sizeDelta = new Vector2(0f, needed);
            Layout(needed);
            Refresh();
        }

        // The right side's vertical layout for a tab whose rows need `needed` units: the stage takes what is left between
        // its bounds, the content scrolls when that is not enough.
        private static void Layout(float needed)
        {
            float rw = rightArea.rect.width;
            float below = Gap + StatsH + (statsText2.gameObject.activeSelf ? StatsH * 0.7f : 0f) + 4f + TabsH + 6f;
            float avail = rightArea.rect.height - (HeaderH + Gap) - below;
            float stageH = Mathf.Clamp(avail - needed - 4f, StageMin, StageMax);
            float contentH = Mathf.Max(60f, avail - stageH);
            float y = HeaderH + Gap;
            Rect(stage.gameObject, 0f, y, rw, stageH);
            y += stageH + Gap;
            Rect(statsText.gameObject, 0f, y, rw, StatsH);
            y += StatsH;
            if (statsText2.gameObject.activeSelf)
            {
                Rect(statsText2.gameObject, 0f, y, rw, StatsH * 0.7f);
                y += StatsH * 0.7f;
            }
            y += 4f;
            Rect(tabsBar.gameObject, 0f, y, rw, TabsH);
            y += TabsH + 6f;
            Rect(contentScroll.gameObject, 0f, y, rw, contentH);
            contentScroll.verticalNormalizedPosition = 1f;
        }

        // ---- the pieces ----

        private static void ReadFont()
        {
            GameObject probe = SettingsRows.MakeHeading(templateRow, panel.transform, page, "probe");
            TextMeshProUGUI t = null;
            foreach (TextMeshProUGUI c in probe.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t == null || c.gameObject.activeInHierarchy) t = c;
            }
            if (t != null)
            {
                font = t.font;
                fontMaterial = t.fontSharedMaterial;
                fontColor = t.color;
                fontSize = t.fontSize > 0f ? t.fontSize : fontSize;
            }
            probe.SetActive(false);
            UnityEngine.Object.Destroy(probe);
        }

        private static GameObject Node(string name, Transform parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var go = new GameObject("GK2DonkeyAPI mounts panel " + name, types);
            go.transform.SetParent(parent, false);
            return go;
        }

        // A rectangle placed from the top left of its parent.
        private static RectTransform Rect(GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // A copied row or heading put at x, y from the top left of its parent, its size kept.
        private static void Place(GameObject go, float x, float y)
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

        // Where the game's own row draws, relative to its rectangle's top left: its parts hang outside the rectangle (the
        // label to the left, the value box to the right), so a row is placed by where it draws, not by its rectangle.
        private static void MeasureRow()
        {
            try
            {
                var root = (RectTransform)templateRow.transform;
                var corners = new Vector3[4];
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (RectTransform r in templateRow.GetComponentsInChildren<RectTransform>(true))
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
                rowVisual = new Rect(minX - topLeft.x, topLeft.y - maxY, maxX - minX, maxY - minY); // y down: a drawn part above the rectangle gives a negative y
                rowH = rowVisual.height;
                Plugin.Log.LogInfo($"Settings: a game row draws {rowVisual.width:0}x{rowVisual.height:0} at {rowVisual.x:0},{rowVisual.y:0} from its rectangle's top left ({root.rect.width:0}x{root.rect.height:0}).");
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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void BottomRight(RectTransform rt, float fromRight)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-fromRight, Pad);
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float sizeMul, TextAlignmentOptions align, bool wrap, float x, float y, float w, float h)
        {
            var t = Node(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
            t.color = fontColor;
            t.fontSize = fontSize * sizeMul;
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

        // A scroll view: the wheel scrolls; a vertical layout of children inside (or rows of columns when horizontalColumns).
        private static ScrollRect ScrollView(Transform parent, string name, float x, float y, float w, float h, out RectTransform content, float spacing, bool manual = false)
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
            sr.scrollSensitivity = 30f;
            sr.inertia = false;
            content = crt;
            return sr;
        }

        private static Transform Column(Transform parent, string name)
        {
            var col = Node(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var vl = col.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 4f;
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlWidth = vl.childControlHeight = false;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var f = col.GetComponent<ContentSizeFitter>();
            f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return col.transform;
        }

        // An icon shown whole inside a square of px canvas units, pixel art at a whole-number scale when it fits one.
        private static void Show(RawImage img, MountIcons.Icon icon, float px)
        {
            img.texture = icon.Texture;
            img.uvRect = icon.Uv;
            float k = Mathf.Min(px / Mathf.Max(1, icon.W), px / Mathf.Max(1, icon.H));
            if (k >= 1f) k = Mathf.Floor(k);
            ((RectTransform)img.transform).sizeDelta = new Vector2(Mathf.Round(icon.W * k), Mathf.Round(icon.H * k));
        }

        // A button of the panel's own, drawn with the game's dialog button face (sliced to any size) and its font: the
        // cards and the tabs. A CanvasGroup on it dims it (the chosen card, the shown tab stay bright).
        private static GameObject OwnButton(Transform parent, string name, string text, float w, float h, Action onClick, float textLeft = 0f)
        {
            GameObject go = Node(name, parent, typeof(Image), typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
            Rect(go, 0f, 0f, w, h);
            var img = go.GetComponent<Image>();
            if (buttonSprite != null)
            {
                img.sprite = buttonSprite;
                img.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                img.color = new Color(0.45f, 0.12f, 0.12f, 1f);
            }
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            TextMeshProUGUI t = Text(go.transform, "label", text, 1f, textLeft > 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, false, textLeft, 0f, w - textLeft - 6f, h);
            t.color = buttonTextColor;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject Button(UIDialogWindowButton template, Transform parent, string text, Action onClick)
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
            foreach (Image img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null && img.sprite.border != Vector4.zero && img.type == Image.Type.Simple) img.type = Image.Type.Sliced; // stretched to a card's size without stretching its art
            }
            LazyButton b = go.GetComponentInChildren<LazyButton>(true);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => onClick());
            go.SetActive(true);
            return go;
        }

        // The window scaled down as a whole when the canvas is smaller than it (never cut), centred.
        private static void FitToScreen()
        {
            if (panel == null) return;
            var canvasRt = panel.transform.parent as RectTransform;
            if (canvasRt == null) return;
            float roomH = canvasRt.rect.height - 2f * EdgeGap, roomW = canvasRt.rect.width - 2f * EdgeGap;
            float scale = Mathf.Min(1f, roomW / W, roomH / H);
            scale = Mathf.Max(0.5f, scale);
            panel.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // The self-test's view of the panel ("settings: the Mounts panel opens...").
        internal static int TestRows => rows.Count;
        internal static int TestEntries => entries.Count;
        internal static string TestBuiltFor => builtFor;
        internal static void TestSelect(string id) => Select(id);
        internal static void TestClose(bool keep) => Close(keep);
        internal static (float W, float H, float Scale, float RoomW, float RoomH) TestMeasure()
        {
            if (panel == null) return default;
            var canvasRt = panel.transform.parent as RectTransform;
            return (W, H, panel.transform.localScale.x, canvasRt != null ? canvasRt.rect.width - 2f * EdgeGap : 0f, canvasRt != null ? canvasRt.rect.height - 2f * EdgeGap : 0f);
        }

        // After a change: every row's value (one row's change can show another), the rows a rule greys; the tab's
        // content built again when a preset shows or hides a row; the right side built again when the mount changed.
        internal static void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            string chosen = page?.MountChosen ?? MountKinds.DonkeyId;
            if (!chosen.Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
            {
                BuildRight(); // the mount changed under the panel (the cfg, a key): the right side follows
                MarkList();
                return;
            }
            var sig = new StringBuilder(tab ?? "");
            int ti = tabs.FindIndex(t => t.Name == tab);
            if (ti >= 0) foreach (var g in tabs[ti].Groups) foreach (MountSettingsPage.Row r in g.Rows) sig.Append(r.Visible == null || r.Visible() ? '1' : '0');
            if (sig.ToString() != contentSignature)
            {
                BuildTab(); // a preset chosen or left: its rows come or go
                return;
            }
            foreach (var (row, go, refresh, group) in rows)
            {
                refresh?.Invoke();
                if (group != null)
                {
                    bool on = row.Enabled == null || row.Enabled();
                    group.alpha = on ? 1f : 0.4f;
                    group.interactable = on;
                    group.blocksRaycasts = on;
                    foreach (GamepadNavigationItem n in go.GetComponentsInChildren<GamepadNavigationItem>(true))
                    {
                        n.enabled = on;
                    }
                }
            }
            MarkList();
        }

        internal static void Tick()
        {
            if (panel != null && (!Mount.IsGameReady || !panel.activeSelf))
            {
                Close(keep: true);
                return;
            }
            if (panel == null || Time.unscaledTime < nextPicture)
            {
                return;
            }
            nextPicture = Time.unscaledTime + 0.2f;
            if (page != null && !(page.MountChosen ?? MountKinds.DonkeyId).Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Refresh(); // the mount changed under the panel without a row click (the cfg): the right side follows
                }
                catch (Exception e)
                {
                    // never out of here: this runs inside Mount.Tick, whose catch dismounts the rider
                    Plugin.Log.LogWarning("Settings: the Mounts panel could not follow the mount change (" + e.Message + "); closed.");
                    Close(keep: true);
                    return;
                }
            }
            if (pictureOff) return;
            try
            {
                Mount.Instance paper = LookPreview.PaperOf(page);
                Texture pic = PreviewStage.Picture(paper);
                if (pic == null && PreviewStage.Failed)
                {
                    pictureTex = paper != null ? Colors.BuildPreview(paper, pictureTex) : null;
                    pic = pictureTex;
                }
                if (pic != null)
                {
                    // as large as the stage allows, up to PictureScale, centred in it
                    picture.texture = pic;
                    Vector2 art = PreviewStage.ArtSize(pic);
                    float k = Mathf.Min(PictureScale, (stage.rect.width - 16f) / Mathf.Max(1f, art.x), (stage.rect.height - 16f) / Mathf.Max(1f, art.y)); // the stage renders at 3x detail: a fraction of that is a smooth downscale
                    ((RectTransform)picture.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
                }
                picture.gameObject.SetActive(pic != null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Settings: the Mounts panel's picture is off (" + e.Message + ").");
                if (picture != null)
                {
                    picture.gameObject.SetActive(false);
                }
                pictureOff = true; // the picture alone: the panel keeps following the mount
            }
        }

        // A skin or mount registered, reloaded or withdrawn while the panel is open (the Mounts tab, the pack watch): the
        // list and the right side are built again a frame later (the registry may be mid-change when the event comes).
        private static bool registryQueued;

        private static void OnRegistryChanged()
        {
            if (!IsOpen || registryQueued) return;
            registryQueued = true;
            Plugin.NextFrame(() =>
            {
                registryQueued = false;
                if (!IsOpen) return;
                try
                {
                    entries = Entries();
                    BuildList();
                    BuildRight();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Settings: the Mounts panel could not follow a change of the mounts (" + e.Message + "); closed.");
                    Close(keep: true);
                }
            });
        }

        private static void Close(bool keep)
        {
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Kinds.KindsChanged -= OnRegistryChanged;
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
                Plugin.Log.LogWarning("Settings: putting the mount and its colours back failed: " + e.Message);
            }
            try
            {
                page?.RefreshMenu?.Invoke(); // the Mods menu re-reads its conditions: a row the chosen mount pins or leaves out
            }
            catch
            {
            }
            before.Clear();
            rows.Clear();
            cards.Clear();
            tabButtons.Clear();
            tabs.Clear();
            builtFor = null;
            contentSignature = null;
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
                panel = null;
            }
            if (pictureTex != null)
            {
                UnityEngine.Object.Destroy(pictureTex);
                pictureTex = null;
            }
            picture = null;
            headerIcon = null;
            listContent = rightArea = stage = tabsBar = contentRoot = null;
            listScroll = contentScroll = null;
            titleCount = headerName = headerDesc = statsText = statsText2 = null;
            templateRow = null;
            dialogTemplate = null;
            font = null;
            fontMaterial = null;
            nextPicture = 0f;
            page = null;
        }
    }
}
