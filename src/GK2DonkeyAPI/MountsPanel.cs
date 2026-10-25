using System;
using System.Collections.Generic;
using System.Text;
using GK2DonkeyAPI.Api;
using BepInEx;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The Mounts panel (MountSettingsPage.PanelButton / OpenPanel; docs/mounts-panel-design.md): a journal of the mounts
    // a player can ride. A list on the left (an icon and the name each, scrolling), the chosen mount on the right: its
    // portrait, name and description; the live picture of it with its cart on a dark stage, a stats card beside it
    // (speed, the cart, babies, skins); under them the rows that define its look at a glance, Skin and Effect, as the
    // game's own rows. A "Colours and effect..." button opens a second window of the same make with the colour rows
    // in two full columns and the effect's toggles (the user, 2026-10-21: the journal must not squash the
    // customisation; the colours get a window of their own). Done and Escape keep what was chosen (no Cancel since 2026-10-21; the snapshot below still lets a self-test put back what was set
    // when the window opened). Only in a game.
    //
    // Built by hand at a fixed size (1060x600 on the game's 1280x720 canvas; scaled only on a smaller canvas), no
    // nested auto-sizing. From the game: the window frame sprite, its dialog button's face (sliced onto the panel's own
    // buttons), its font (read off one of its labels), and its "< value >" rows (SettingsRows), placed where they draw
    // (a game row draws wider than its rectangle: MeasureRow). If a part can't be found, the panel doesn't open and
    // the log says so.
    internal static class MountsPanel
    {
        private const float W = 1060f, H = 600f, Pad = 20f, Gap = 8f;
        private const float TitleH = 28f, ListW = 260f, HeaderH = 76f, ButtonsH = 40f, CardH = 44f, ScrollbarW = 6f, ButtonW = 130f, ButtonH = 34f;
        private const float StageW = 440f, StageH = 236f, PictureScale = 3f; // the stage is one size on every mount: nothing moves
        private const int StatCount = 3; private const float StatLineH = 30f, StatsCardH = 44f + StatCount * StatLineH + 8f; // the card is as tall as its lines, not as the stage
        private const float IconPx = 36f, HeaderIconPx = 64f;
        private const float SearchH = 26f;
        private const int SearchFrom = 8; // a search box above the list once it has more mounts than this
        private const float SwatchPx = 14f, SwatchGap = 2f, SwatchRowH = 2f * SwatchPx + SwatchGap + 6f, SwatchLabelW = 160f; // a custom colour's row: the 24 named colours as squares, two rows of twelve
        private const float EdgeGap = 24f; // the least room left at the screen's edges before a window is scaled down
        private const float CW = 800f, CH = 640f, CStageW = StageW, CStageH = StageH, CPictureScale = PictureScale; // the colours window and its picture: the journal's stage and size, so the mount is drawn as big in both (the user, 2026-10-22: smaller in the colours window)
        private static readonly Color CardBase = new Color(0.13f, 0.14f, 0.18f, 0.9f), CardChosen = new Color(0.42f, 0.11f, 0.11f, 0.95f); // a journal's rows: dark, the chosen one in the game's red
        private static readonly Color BoxColor = new Color(0f, 0f, 0f, 0.35f), EdgeColor = new Color(1f, 1f, 1f, 0.12f);

        private static GameObject panel, colours;
        private static MountSettingsPage page;
        private static GameObject templateRow;
        private static UIDialogWindowButton dialogTemplate;
        private static Sprite buttonSprite;      // the game's dialog button face, for the panel's own buttons
        private static Color buttonTextColor = Color.white;
        private static TMP_FontAsset font;
        private static Material fontMaterial;
        private static Color fontColor = Color.white;
        private static float fontSize = 18f;
        private static Rect rowVisual = new Rect(0f, 0f, 400f, 33f); // where a copied game row really draws, relative to its rectangle's top left: x and y its offsets (negative: outside), its drawn width and height

        private static RectTransform listContent, rightArea, stage, contentRoot, coloursRoot, coloursStage;
        private static ScrollRect listScroll, contentScroll, coloursScroll;
        private static RawImage headerIcon, picture, coloursPicture;
        private static TextMeshProUGUI titleCount, headerName, headerDesc, coloursTitle;
        private static TMP_InputField search;
        private static string filter = "";
        private static readonly List<(TextMeshProUGUI Label, TextMeshProUGUI Value)> statLines = new List<(TextMeshProUGUI, TextMeshProUGUI)>();
        private static Texture2D pictureTex;
        private static float nextPicture;
        private static bool pictureOff;

        private static readonly List<(string Id, Image Face, TextMeshProUGUI Label)> cards = new List<(string, Image, TextMeshProUGUI)>();
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> rows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> colourRows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();
        private static readonly List<(string Group, List<MountSettingsPage.Row> Rows)> journalGroups = new List<(string, List<MountSettingsPage.Row>)>();
        private static readonly List<(string Group, List<MountSettingsPage.Row> Rows)> colourGroups = new List<(string, List<MountSettingsPage.Row>)>();
        private static List<Entry> entries = new List<Entry>();
        private static string builtFor;            // the mount the right side was built for
        private static string journalSignature, coloursSignature; // which rows were built (a preset shows a row: rebuilt)
        private static readonly Dictionary<ConfigEntryBaseKey, object> before = new Dictionary<ConfigEntryBaseKey, object>();
        private static readonly Dictionary<ConfigEntryBaseKey, object> coloursBefore = new Dictionary<ConfigEntryBaseKey, object>();

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
            internal readonly List<string> Lines = new List<string>();  // what it does, in words: the cart, babies, speed, what it comes with, its skins
            internal readonly List<string> Extra = new List<string>();  // its own stats and abilities
            internal readonly List<(string Label, string Value)> Stats = new List<(string, string)>(); // the stats card: Speed, Cargo, Skins
            internal bool HasPortrait;
            internal bool Fake; // a made-up entry (Debug.MountsPanelFakeEntries): listed, not pickable
        }

        internal static bool IsOpen => panel != null && (panel.activeSelf || colours != null);
        internal static bool SearchFocused => search != null && search.isFocused;
        internal static bool IsColoursOpen => colours != null;

        // Every mount a player can pick (Looks.MountChoices: the mounts with a skin, the donkey first), with its words.
        internal static List<Entry> Entries()
        {
            var list = new List<Entry>();
            foreach ((string id, string label) in Looks.MountChoices())
            {
                Catalog.Entry k = Catalog.Find(id);
                if (k == null) continue;
                var e = new Entry { Id = id, Label = label };
                Looks.SkinDef def = Looks.Find(Looks.DefaultSkinOf(k));
                e.Description = !string.IsNullOrEmpty(k.Description) ? k.Description : def?.Description ?? (k == Catalog.Donkey ? "The game's own donkey, as it draws it." : "");
                e.HasPortrait = k.PortraitTexture != null || !string.IsNullOrEmpty(k.Portrait) || (def != null && (def.PortraitTexture != null || !string.IsNullOrEmpty(def.Portrait)));
                e.Lines.Add(k.Cart ? "Pulls the cart" : "Can't pull the cart");
                e.Lines.Add(k.Foals ? (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f ? $"Has small babies ({k.FoalScale:0.##} of its size)" : "Has babies") : "No babies");
                // jumping and flying: only while they are switched on (a dev-only experiment for now, Mounts.FlyingEnabled)
                if (Flight.Active && k.Flies) e.Lines.Add("Flies");
                if (Flight.Active && !k.Jumps) e.Lines.Add("Doesn't jump");
                // the mount's speed as a word against the donkey's (Catalog.SpeedWord), and what its cart carries (the user,
                // 2026-10-22: cargo, not the cart's speed, which follows the mount's)
                float ride = Catalog.SpeedOf(k, MountStyle.Ride);
                e.Lines.Add("Speed: " + SpeedText(ride, MountStyle.Ride));
                if (k.Cart) e.Lines.Add("Cargo: " + Catalog.CargoOf(k) + " items");
                if (!string.IsNullOrEmpty(k.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(k.Effect)?.Name ?? k.Effect) + (k.EffectWhen == MountEffectWhen.Night ? " (at night)" : ""));
                else if (def != null && !string.IsNullOrEmpty(def.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(def.Effect)?.Name ?? def.Effect));
                int skins = (k == Catalog.Donkey ? 1 : 0);
                foreach (Looks.SkinDef sd in Catalog.SkinsOf(k)) if (sd.Unavailable == null) skins++; // the ones a player can pick
                if (skins > 1) e.Lines.Add(skins + " skins");
                if (k.Stats != null) foreach (var st in k.Stats) e.Extra.Add(string.IsNullOrEmpty(st.Value) ? st.Key : st.Key + ": " + st.Value);
                foreach (MountAbility a in k.Abilities)
                {
                    string name = string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name;
                    e.Extra.Add(string.IsNullOrEmpty(a.Description) ? name : name + ": " + a.Description);
                }
                // the stats card (the user's pick, 2026-10-21; cargo for the cart's speed and no babies line, 2026-10-22): speed,
                // what its cart carries (or no cart), skins
                e.Stats.Add(("Speed", Catalog.SpeedWord(ride, MountStyle.Ride)));
                e.Stats.Add(("Cargo", k.Cart ? Catalog.CargoOf(k) + " items" : "no cart"));
                e.Stats.Add(("Skins", skins.ToString()));
                list.Add(e);
            }
            int fakes = Plugin.PanelFakeEntries != null ? Mathf.Clamp(Plugin.PanelFakeEntries.Value, 0, 200) : 0;
            for (int i = 1; i <= fakes; i++)
            {
                list.Add(new Entry { Id = "fake-" + i, Label = "Test mount " + i, Description = "A made-up entry (Debug.MountsPanelFakeEntries), to see the list scroll.", Fake = true });
            }
            return list;
        }

        // "Fast (2.25x walking speed)": SpeedMultiplier is times the keeper's walking speed (docs/reference.md).
        private static string SpeedText(float v, MountStyle style) =>
            Catalog.SpeedWord(v, style) + " (" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x walking speed)";

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
                        FitToScreen(panel, W, H); // the button again: the panel stays as it is
                        if (colours != null) FitToScreen(colours, CW, CH);
                    }
                    return;
                }
                Close(keep: true, back: false);
                page = p;
                p.HoldMountForPanel(); // picking a row shows a mount, Ride or Drive the cart makes it his
                Build(settings);
                HideUnder(); // the game window it was opened from (if any) waits underneath, as it is
                TakeControls();
                Plugin.Log.LogInfo($"Settings: the Mounts panel opened over {UnderText()}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the Mounts panel can't open (" + e.Message + "); the mount and its colours are still in the cfg.");
                Close(keep: true);
            }
        }

        // ---- the journal ----

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

            panel = Window("mounts panel", settings, W, H, 30000);
            ReadFont();
            buttonSprite = null;
            foreach (Image img in dialogTemplate.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null) { buttonSprite = img.sprite; break; }
            }
            TextMeshProUGUI bt = dialogTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
            if (bt != null) buttonTextColor = bt.color;
            MeasureRow();
            focusTemplate = templateRow.GetComponentInChildren<GamepadNavigationItem>(true)?.focusFrame; // the game row's own focus frame, for the panel's buttons

            // What was set when the panel opened, for a put-back (the self-tests; no Cancel button since 2026-10-21): every look row of the page (the mount, every mount's
            // skin and colours, the effect), not only the chosen mount's, so a put-back after choosing another mount puts
            // the first one back.
            before.Clear();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.IsLook && r.Entry != null) before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
            }

            // the title line
            Text(panel.transform, "title", "Mounts", 1.25f, TextAlignmentOptions.Left, false, Pad, Pad, 400f, TitleH);
            titleCount = Text(panel.transform, "count", "", 0.9f, TextAlignmentOptions.Right, false, W - Pad - 300f, Pad + 2f, 300f, TitleH);

            // the list, scrolling; a search box above it once it is long
            entries = Entries();
            filter = "";
            float listTop = Pad + TitleH + Gap;
            search = null;
            if (entries.Count > SearchFrom)
            {
                search = SearchBox(panel.transform, Pad, listTop, ListW - 4f, SearchH);
                listTop += SearchH + Gap;
            }
            float listH = H - listTop - Gap - ButtonsH - Pad;
            listScroll = ScrollView(panel.transform, "list", Pad, listTop, ListW, listH, out listContent, 4f);
            AddScrollbar(listScroll);

            // the right side: the header, the stage and the stats card beside it, the look rows under them
            float x0 = Pad + ListW + 2f * Gap, rw = W - Pad - x0;
            float rightTop = Pad + TitleH + Gap;
            rightArea = Rect(Node("right", panel.transform), x0, rightTop, rw, H - rightTop - Gap - ButtonsH - Pad);
            headerIcon = Node("portrait", rightArea, typeof(RawImage)).GetComponent<RawImage>();
            headerIcon.raycastTarget = false;
            Rect(headerIcon.gameObject, 0f, 4f, HeaderIconPx, HeaderIconPx);
            headerIcon.gameObject.SetActive(false);
            headerName = Text(rightArea, "name", "", 1.35f, TextAlignmentOptions.Left, false, HeaderIconPx + 12f, 0f, rw - HeaderIconPx - 12f, 32f);
            headerDesc = Text(rightArea, "description", "", 0.9f, TextAlignmentOptions.TopLeft, true, HeaderIconPx + 12f, 32f, rw - HeaderIconPx - 12f, HeaderH - 32f);
            headerDesc.overflowMode = TextOverflowModes.Ellipsis;

            float y = HeaderH + Gap;
            stage = Box(rightArea, "stage", 0f, y, StageW, StageH);
            picture = Node("picture", stage, typeof(RawImage)).GetComponent<RawImage>();
            picture.raycastTarget = false;
            var pic = (RectTransform)picture.transform;
            pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
            pic.pivot = new Vector2(0.5f, 0.5f);
            picture.gameObject.SetActive(false);

            float statsX = StageW + 2f * Gap, statsW = rw - statsX;
            RectTransform statsCard = Box(rightArea, "stats", statsX, y, statsW, StatsCardH);
            Text(statsCard, "stats title", "Stats", 1f, TextAlignmentOptions.Left, false, 12f, 8f, statsW - 24f, 26f).fontStyle = FontStyles.Bold;
            statLines.Clear();
            for (int i = 0; i < StatCount; i++)
            {
                float ly = 44f + i * StatLineH;
                TextMeshProUGUI l = Text(statsCard, "stat label " + i, "", 0.9f, TextAlignmentOptions.Left, false, 12f, ly, statsW * 0.42f, StatLineH);
                l.color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.7f);
                TextMeshProUGUI v = Text(statsCard, "stat value " + i, "", 0.95f, TextAlignmentOptions.Right, false, statsW * 0.42f, ly, statsW * 0.58f - 12f, StatLineH);
                v.color = buttonTextColor;
                statLines.Add((l, v));
            }
            y += StageH + Gap;
            contentScroll = ScrollView(rightArea, "content", 0f, y, rw, Mathf.Max(60f, rightArea.rect.height - y), out contentRoot, 2f, manual: true);
            AddScrollbar(contentScroll);

            // the buttons: Colours and effect..., Done (no Cancel: Done and Escape both keep what was chosen, the user 2026-10-21)
            GameObject done = OwnButton(panel.transform, "done", "Done", ButtonW, ButtonH, () => Close(keep: true));
            Rect(done, W - Pad - ButtonW, H - Pad - ButtonH, ButtonW, ButtonH);
            GameObject customise = OwnButton(panel.transform, "colours", "Colours and effect...", 200f, ButtonH, OpenColours);
            Rect(customise, W - Pad - ButtonW - 16f - 200f, H - Pad - ButtonH, 200f, ButtonH);
            // Ride and Drive: the panel closes (keeping what was chosen) and the mount is called, as the keys would
            GameObject ride = OwnButton(panel.transform, "ride", "Ride", ButtonW, ButtonH, () => Call(MountStyle.Ride));
            Rect(ride, Pad, H - Pad - ButtonH, ButtonW, ButtonH);
            GameObject drive = OwnButton(panel.transform, "drive", "Drive the cart", 150f, ButtonH, () => Call(MountStyle.Cart));
            foreach (GameObject bgo in new[] { done, customise, ride, drive }) NavGroup(bgo, 2);
            var jnav = panel.GetComponent<GamepadNavigationController>();
            Hand(jnav, 0, GUIDirection.Right, 1); Hand(jnav, 0, GUIDirection.Down, 2);  // the list -> the rows, the buttons
            Hand(jnav, 1, GUIDirection.Left, 0); Hand(jnav, 1, GUIDirection.Down, 2);   // the rows -> the list, the buttons
            Hand(jnav, 2, GUIDirection.Up, 1); Hand(jnav, 2, GUIDirection.Left, 0);     // the buttons -> the rows, the list
            Hand(jnav, 1, GUIDirection.Up, 0);
            Rect(drive, Pad + ButtonW + 12f, H - Pad - ButtonH, 150f, ButtonH);

            pictureOff = false;
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Catalog.MountsChanged -= OnRegistryChanged;
            Mounts.Looks.SkinsChanged += OnRegistryChanged;
            Mounts.Catalog.MountsChanged += OnRegistryChanged;
            panel.SetActive(true); // before the list: a layout under an inactive root computes nothing, so the scroll to the chosen mount was lost
            BuildList();
            BuildRight();
            FitToScreen(panel, W, H);
            Plugin.Log.LogInfo($"Settings: the Mounts panel of {page.Title} is open ({entries.Count} mount(s), {rows.Count} look rows for {builtFor}{(panel.transform.localScale.x < 1f ? $", scaled to {panel.transform.localScale.x:0.##} to fit the screen" : "")}).");
        }

        // A window of the panel's make: a dark face, the game's frame, its own canvas above the HUD; centred, hidden.
        private static GameObject Window(string name, UIGameSettingsWindow settings, float w, float h, int sorting)
        {
            var go = new GameObject("GK2DonkeyAPI " + name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            go.transform.SetParent(settings.transform.parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sorting;
            go.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f, 0.97f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            Image frame = settings.transform.Find("GenericWIndowLayout/Frame")?.GetComponent<Image>();
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

        private static void Hand(GamepadNavigationController nav, int from, GUIDirection dir, int to)
        {
            var src = nav.navigationGroupSources.Find(s => s.group == from);
            if (src == null)
            {
                src = new GamepadNavigationController.NavigationGroupSource(from, new List<GamepadNavigationController.NavigationGroupTarget>());
                nav.navigationGroupSources.Add(src);
            }
            src.groupTargets.Add(new GamepadNavigationController.NavigationGroupTarget(to, dir));
        }

        // The list: a row per mount (its icon and name), the chosen one in red; scrolled to the chosen one.
        private static void BuildList()
        {
            foreach (Transform c in listContent) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            cards.Clear();
            string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
            int at = -1; // the chosen card's place among the cards shown (the search filters)
            int shown = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (filter.Length > 0 && (e.Label ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && (e.Id ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue; // the search box
                shown++;
                string id = e.Id;
                bool fake = e.Fake;
                GameObject go = OwnButton(listContent, "card " + id, e.Label, ListW - 16f - ScrollbarW, CardH, () => { if (!fake) Select(id); }, textLeft: IconPx + 14f, flat: true);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = ListW - 16f - ScrollbarW;
                le.preferredHeight = CardH;
                le.minHeight = CardH;
                if (fake)
                {
                    go.GetComponent<UnityEngine.UI.Button>().interactable = false;
                    go.GetComponent<CanvasGroup>().alpha = 0.5f;
                }
                MountIcons.Icon icon = MountIcons.Of(Catalog.Find(id));
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
                cards.Add((id, go.GetComponent<Image>(), go.GetComponentInChildren<TextMeshProUGUI>(true)));
                NavGroup(go, 0);
                if (fake) { var fi = go.GetComponent<GamepadNavigationItem>(); if (fi != null) fi.Active = false; }
                if (id.Equals(chosen, StringComparison.OrdinalIgnoreCase)) at = shown - 1;
            }
            titleCount.text = filter.Length > 0 ? $"{shown} of {entries.Count} mounts" : entries.Count == 1 ? "1 mount" : entries.Count + " mounts";
            navDirty = true;
            MarkList();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (at >= 0 && shown > 1) listScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(at / (float)(shown - 1));
        }

        private static void MarkList()
        {
            string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
            foreach ((string id, Image face, TextMeshProUGUI label) in cards)
            {
                bool on = id.Equals(chosen, StringComparison.OrdinalIgnoreCase);
                if (face != null) face.color = on ? CardChosen : CardBase;
                if (label != null)
                {
                    label.color = on ? buttonTextColor : fontColor;
                    label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        // A mount chosen in the list: the page's Mount row takes it (its apply heals the skin and sets the companion,
        // and every per-mount row's condition flips with it), then the right side is built again for it.
        private static void Select(string id)
        {
            if (page == null) return;
            Perf.TracePick(id);
            using (Perf.Measure(Perf.S.Pick))
            {
                page.SelectMount(id);
                using (Perf.Measure(Perf.S.PickRows))
                {
                    BuildRight();
                    MarkList();
                }
            }
        }

        // The right side for the chosen mount: the header, the stats card, the look rows; the colours window's groups
        // gathered too (built when it opens).
        private static void BuildRight()
        {
            string chosen = page.MountChosen ?? MountCatalog.DonkeyId;
            builtFor = chosen;
            Entry e = entries.Find(x => x.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            Catalog.Entry k = Catalog.Find(chosen);
            MountIcons.Icon icon = MountIcons.Of(k);
            headerIcon.gameObject.SetActive(icon.Any);
            if (icon.Any) Show(headerIcon, icon, HeaderIconPx);
            float textX = icon.Any ? HeaderIconPx + 12f : 0f;
            Rect(headerName.gameObject, textX, 0f, rightArea.rect.width - textX, 32f);
            Rect(headerDesc.gameObject, textX, 32f, rightArea.rect.width - textX, HeaderH - 32f);
            for (int i = 0; i < statLines.Count; i++)
            {
                (string label, string value) = e != null && i < e.Stats.Count ? e.Stats[i] : ("", "");
                statLines[i].Label.text = label;
                statLines[i].Value.text = value;
            }
            if (e != null)
            {
                headerName.text = e.Label;
                headerDesc.text = e.Description;
            }
            else
            {
                // the chosen mount is not in the list: its mod or pack is gone, or its every skin is set aside (a game-source
                // skin whose frames the game lacks); the choice is kept, the donkey comes instead, and the panel says so
                string why = null;
                if (k != null) foreach (Looks.SkinDef sd in Catalog.SkinsOf(k)) if (sd.Unavailable != null) { why = sd.Unavailable; break; }
                headerName.text = k?.Label ?? chosen;
                headerDesc.text = k == null ? "Not installed: the mod or pack that had this mount is gone. Your choice is kept; the donkey comes instead."
                    : why != null ? "Set aside: the game no longer has the pictures this mount's skin needs (the log names them). Your choice is kept; the donkey comes instead."
                    : "It has no skin to wear. Your choice is kept; the donkey comes instead.";
            }

            // the page's look rows for this mount, in groups: the journal takes the Mount section's rows (the Skin) and the
            // Effect section's choices (Effect, Effect shown); the colours window takes every other look section (the
            // colours, under its two headings) and the Effect section's toggles under an "Effect" heading
            journalGroups.Clear();
            colourGroups.Clear();
            string heading = null, headingSection = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook) continue;
                if (r.RowKind == MountSettingsPage.RowKind.Note)
                {
                    heading = r.Label;
                    headingSection = r.ShownSection;
                    continue;
                }
                if (r.Shown != null && !r.Shown()) continue; // another mount's row
                if (page.IsMountRow(r)) continue; // the list is the picker
                bool mountSection = string.Equals(r.ShownSection, "Mount", StringComparison.OrdinalIgnoreCase);
                bool effectSection = string.Equals(r.ShownSection, "Effect", StringComparison.OrdinalIgnoreCase);
                bool toggle = r.RowKind == MountSettingsPage.RowKind.Toggle;
                string group = heading != null && r.ShownSection == headingSection ? heading : (mountSection ? "Skin" : r.ShownSection);
                if (mountSection || (effectSection && !toggle)) AddTo(journalGroups, group, r); // the journal: the Skin, the effect's choices
                if (!mountSection) AddTo(colourGroups, group, r);                           // the colours window: every colour row, every effect row
            }
            journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, rightArea.rect.width - ScrollbarW, rows, out _);
            if (colours != null)
            {
                coloursTitle.text = "Colours and effect: " + headerName.text;
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW, colourRows, out _);
            }
            Refresh();
        }

        private static void AddTo(List<(string Group, List<MountSettingsPage.Row> Rows)> groups, string group, MountSettingsPage.Row r)
        {
            int gi = groups.FindIndex(g => g.Group == group);
            if (gi < 0)
            {
                groups.Add((group, new List<MountSettingsPage.Row>()));
                gi = groups.Count - 1;
            }
            groups[gi].Rows.Add(r);
        }

        // Rows in groups as columns (two side by side, more in further rows), centred in the area; each row and heading
        // placed where its drawing (not its rectangle) lands: rowVisual says where that is. A row shown only with a
        // preset (a custom colour) is left out until the preset is chosen. Returns the signature of what was built.
        private static string BuildRows(List<(string Group, List<MountSettingsPage.Row> Rows)> groups, RectTransform root, ScrollRect scroll, float areaW,
            List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> into, out float needed)
        {
            foreach (Transform c in root) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            into.Clear();
            var sig = new StringBuilder();
            float colW = rowVisual.width, colGap = 16f, step = rowVisual.height + 4f;
            if (2f * colW + colGap > areaW) colGap = Math.Max(4f, areaW - 2f * colW); // the two columns as close as the area needs
            int perRow = groups.Count > 1 && 2f * colW + colGap <= areaW ? 2 : 1; // side by side only when both fit whole; else stacked
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
                    bool any = false;
                    foreach (MountSettingsPage.Row r in group.Rows) if (r.Visible == null || r.Visible()) { any = true; break; }
                    if (group.Group != null && any) // a heading only over rows that show (the effect's toggles hide while the effect is None)
                    {
                        GameObject h = SettingsRows.MakeHeading(templateRow, root, page, group.Group);
                        Place(h, cx - rowVisual.x, cy - rowVisual.y);
                        cy += step;
                    }
                    foreach (MountSettingsPage.Row r in group.Rows)
                    {
                        bool visible = r.Visible == null || r.Visible();
                        sig.Append(visible ? '1' : '0');
                        if (!visible) continue; // shown only with a preset: not now
                        GameObject go;
                        Action refresh;
                        if (IsPalette(r))
                        {
                            go = SwatchRow(root, r, out refresh); // the named colours as squares, where the game's "< Chestnut >" row would be
                            Place(go, cx, cy);
                            cy += SwatchRowH + 4f;
                        }
                        else
                        {
                            go = SettingsRows.MakeRow(templateRow, root, r, page, out refresh);
                            go.SetActive(true); // a copied row starts switched off (SettingsRows.Copy)
                            Place(go, cx - rowVisual.x, cy - rowVisual.y);
                            cy += step;
                        }
                        CanvasGroup grp = r.Enabled != null ? go.AddComponent<CanvasGroup>() : null;
                        into.Add((r, go, refresh, grp));
                    }
                    tallest = Math.Max(tallest, cy - rowTop);
                }
                y = rowTop + tallest + 8f;
            }
            needed = y;
            root.sizeDelta = new Vector2(0f, needed);
            scroll.verticalNormalizedPosition = 1f;
            int navGroup = root == contentRoot ? 1 : 0;
            foreach (GamepadNavigationItem ni in root.GetComponentsInChildren<GamepadNavigationItem>(true)) ni.group = navGroup;
            navDirty = true;
            return sig.ToString();
        }

        private static bool IsPalette(MountSettingsPage.Row r) => r?.Entry != null && r.Entry.SettingType == typeof(PaletteColor);

        // A custom colour's row: the label where a game row's label sits, then the 24 named colours as squares in two
        // rows of twelve; the chosen one framed; a click sets the row's value (the page applies it, the picture shows it).
        private static GameObject SwatchRow(Transform parent, MountSettingsPage.Row r, out Action refresh)
        {
            var entry = (BepInEx.Configuration.ConfigEntry<PaletteColor>)r.Entry;
            GameObject go = Node("swatches " + r.Entry.Definition.Key, parent);
            Rect(go, 0f, 0f, rowVisual.width, SwatchRowH);
            TextMeshProUGUI label = Text(go.transform, "label", r.Label, 0.95f, TextAlignmentOptions.Right, false, 0f, 0f, SwatchLabelW, SwatchRowH);
            label.overflowMode = TextOverflowModes.Ellipsis;
            var frames = new List<(PaletteColor Colour, GameObject Frame)>();
            Array values = Enum.GetValues(typeof(PaletteColor));
            int perRow = 12;
            for (int i = 0; i < values.Length; i++)
            {
                var colour = (PaletteColor)values.GetValue(i);
                float sx = SwatchLabelW + 8f + (i % perRow) * (SwatchPx + SwatchGap);
                float sy = 3f + (i / perRow) * (SwatchPx + SwatchGap);
                GameObject frame = Node("frame " + colour, go.transform, typeof(Image));
                Rect(frame, sx - 2f, sy - 2f, SwatchPx + 4f, SwatchPx + 4f);
                frame.GetComponent<Image>().color = Color.white;
                frame.GetComponent<Image>().raycastTarget = false;
                frame.SetActive(false);
                GameObject sw = Node("swatch " + colour, go.transform, typeof(Image), typeof(UnityEngine.UI.Button), typeof(GamepadNavigationItem));
                Rect(sw, sx, sy, SwatchPx, SwatchPx);
                GameObject swFocus = Node("focus", sw.transform, typeof(Image), typeof(Outline));
                Rect(swFocus, -3f, -3f, SwatchPx + 6f, SwatchPx + 6f);
                swFocus.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
                swFocus.GetComponent<Image>().raycastTarget = false;
                var swo = swFocus.GetComponent<Outline>();
                swo.effectColor = new Color(1f, 0.85f, 0.3f, 1f);
                swo.effectDistance = new Vector2(2f, -2f);
                swFocus.SetActive(false);
                sw.GetComponent<GamepadNavigationItem>().focusFrame = swFocus;
                var img = sw.GetComponent<Image>();
                ColorUtility.TryParseHtmlString("#" + MountPalette.Hex(colour), out Color c);
                img.color = c;
                var b = sw.GetComponent<UnityEngine.UI.Button>();
                b.targetGraphic = img;
                ColorBlock cb = b.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                cb.selectedColor = Color.white;
                cb.fadeDuration = 0.05f;
                b.colors = cb;
                PaletteColor pick = colour;
                b.onClick.AddListener(() =>
                {
                    if (entry.Value != pick)
                    {
                        entry.Value = pick; // its SettingChanged applies it (the page's Color: colour and lightness combined)
                        Refresh();
                    }
                });
                frames.Add((colour, frame));
                UnityEngine.UI.Button swButton = b;
                sw.GetComponent<GamepadNavigationItem>().SetCallbacks(null, null, () => swButton.onClick.Invoke());
            }
            refresh = () =>
            {
                PaletteColor now = entry.Value;
                foreach ((PaletteColor colour, GameObject frame) in frames) if (frame != null) frame.SetActive(colour == now);
            };
            refresh();
            return go;
        }

        private static string Signature(List<(string Group, List<MountSettingsPage.Row> Rows)> groups)
        {
            var sig = new StringBuilder();
            foreach (var g in groups) foreach (MountSettingsPage.Row r in g.Rows) sig.Append(r.Visible == null || r.Visible() ? '1' : '0');
            return sig.ToString();
        }

        // ---- the colours window ----

        // The colours and the effect's toggles for the chosen mount, in a window of their own over the journal (hidden
        // meanwhile): the colour rows in two full columns under their headings, the toggles under "Effect". Done keeps,
        // Done or Escape goes back to the journal (no Cancel since 2026-10-21).
        private static void OpenColours()
        {
            if (panel == null || colours != null) return;
            try
            {
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                colours = Window("colours window", settings, CW, CH, 30001);
                coloursTitle = Text(colours.transform, "title", "Colours and effect: " + headerName.text, 1.25f, TextAlignmentOptions.Left, false, Pad, Pad, CW - 2f * Pad, TitleH);
                float top = Pad + TitleH + Gap;
                coloursStage = Box(colours.transform, "colours stage", (CW - CStageW) / 2f, top, CStageW, CStageH); // the mount as it is coloured, so one sees what one changes
                coloursPicture = Node("colours picture", coloursStage, typeof(RawImage)).GetComponent<RawImage>();
                coloursPicture.raycastTarget = false;
                var cprt = (RectTransform)coloursPicture.transform;
                cprt.anchorMin = cprt.anchorMax = new Vector2(0.5f, 0.5f);
                cprt.pivot = new Vector2(0.5f, 0.5f);
                coloursPicture.gameObject.SetActive(false);
                top += CStageH + Gap;
                coloursScroll = ScrollView(colours.transform, "colours content", Pad, top, CW - 2f * Pad, CH - top - Gap - ButtonsH - Pad, out coloursRoot, 2f, manual: true);
                AddScrollbar(coloursScroll);
                GameObject done = OwnButton(colours.transform, "colours done", "Done", ButtonW, ButtonH, () => CloseColours(keep: true));
                Rect(done, CW - Pad - ButtonW, CH - Pad - ButtonH, ButtonW, ButtonH);
                NavGroup(done, 1);
                var cnav = colours.GetComponent<GamepadNavigationController>();
                Hand(cnav, 0, GUIDirection.Down, 1); Hand(cnav, 1, GUIDirection.Up, 0); // the rows and swatches <-> Done
                coloursBefore.Clear();
                foreach (var g in colourGroups) foreach (MountSettingsPage.Row r in g.Rows) if (r.Entry != null) coloursBefore[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW, colourRows, out _);
                panel.SetActive(false); // the journal waits underneath
                navDirty = true;
                colours.SetActive(true);
                FitToScreen(colours, CW, CH);
                Refresh();
                Plugin.Log.LogInfo($"Settings: the colours window is open for {builtFor} ({colourRows.Count} rows).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the colours window can't open (" + e.Message + ").");
                CloseColours(keep: true);
            }
        }

        private static void CloseColours(bool keep, bool reshow = true)
        {
            try
            {
                if (!keep)
                {
                    foreach (var kv in coloursBefore)
                    {
                        kv.Key.E.BoxedValue = kv.Value; // its SettingChanged applies it
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: putting the colours back failed: " + e.Message);
            }
            coloursBefore.Clear();
            colourRows.Clear();
            if (colours != null)
            {
                UnityEngine.Object.Destroy(colours);
                colours = null;
            }
            coloursRoot = null;
            coloursScroll = null;
            coloursTitle = null;
            coloursStage = null;
            coloursPicture = null;
            if (panel != null && reshow)
            {
                panel.SetActive(true);
                navDirty = true;
                Refresh();
            }
        }

        // The panel closed (what was chosen kept) and the companion's mount called in the style, as the ride and cart keys do.
        private static bool committing; // Close from Ride / Drive the cart: the mount picked becomes his

        private static void Call(MountStyle style)
        {
            MountSettingsPage p = page;
            committing = true;
            Close(keep: true, back: false); // straight into the game: the windows under it closed, the mount comes
            try
            {
                MountResult r = p?.UseMount(style) ?? default;
                if (!r.Ok) Plugin.Log.LogInfo($"Settings: the Mounts panel's {(style == MountStyle.Cart ? "Drive" : "Ride")} button: {r.Reason}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: calling the mount from the panel failed: " + e.Message);
            }
        }

        // A search box: typing filters the list by name; the mods' keys wait while it has focus (KeyActions).
        private static TMP_InputField SearchBox(Transform parent, float x, float y, float w, float h)
        {
            GameObject go = Node("search", parent, typeof(Image), typeof(TMP_InputField));
            Rect(go, x, y, w, h);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            var edge = go.AddComponent<Outline>();
            edge.effectColor = EdgeColor;
            edge.effectDistance = new Vector2(1f, -1f);
            GameObject area = Node("search text area", go.transform, typeof(RectMask2D));
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(8f, 2f);
            art.offsetMax = new Vector2(-8f, -2f);
            TextMeshProUGUI text = Text(area.transform, "search text", "", 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            Stretch((RectTransform)text.transform);
            text.richText = false;
            TextMeshProUGUI hint = Text(area.transform, "search hint", "Search...", 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, w, h);
            Stretch((RectTransform)hint.transform);
            hint.color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.45f);
            hint.fontStyle = FontStyles.Italic;
            var field = go.GetComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = art;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = 40;
            field.richText = false;
            field.caretColor = fontColor;
            field.customCaretColor = true;
            field.onValueChanged.AddListener(v =>
            {
                filter = v ?? "";
                if (listContent != null) BuildList();
            });
            return field;
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

        // A dark box with a faint edge: the stage, the stats card.
        private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
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

        // A scroll view: the wheel scrolls; a vertical layout of children inside, or (manual) rows placed by hand.
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
            sr.scrollSensitivity = 0f; // the wheel is Wheel()'s, anywhere on the window (a second scroll here would double it)
            sr.inertia = false;
            content = crt;
            return sr;
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

        // A button of the panel's own: flat (a journal's row: a dark face with a faint edge, hover brightens, the chosen
        // one coloured by MarkList) or the game's dialog button face sliced to the size (a touch dark at rest so the
        // hover shows), with the game's font.
        private static GameObject OwnButton(Transform parent, string name, string text, float w, float h, Action onClick, float textLeft = 0f, bool flat = false)
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
                if (buttonSprite != null)
                {
                    img.sprite = buttonSprite;
                    img.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
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
            t.color = flat ? fontColor : buttonTextColor;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.fontStyle = flat ? FontStyles.Normal : FontStyles.Bold;
            // the gamepad: an item whose select presses the button (as LazyButton.SetCallbacksIntoGamepadNavigationItem),
            // framed while focused with the game row's own focus frame
            var item = go.AddComponent<GamepadNavigationItem>();
            UnityEngine.UI.Button pressed = b;
            item.SetCallbacks(null, null, () => { if (pressed.interactable) pressed.onClick.Invoke(); });
            item.focusFrame = FocusFrame(go.transform);
            return go;
        }

        private static GameObject focusTemplate;

        // A focus frame over a button: a copy of the game row's own, else a bright outline.
        private static GameObject FocusFrame(Transform parent)
        {
            GameObject f;
            if (focusTemplate != null)
            {
                f = UnityEngine.Object.Instantiate(focusTemplate, parent, false);
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

        private static void NavGroup(GameObject go, int group)
        {
            var item = go.GetComponent<GamepadNavigationItem>();
            if (item != null) item.group = group;
        }

        // ---- the gamepad, every frame (from Tick) ----
        private static bool navDirty = true;

        private static void TickGamepad()
        {
            GameObject window = colours != null ? colours : panel;
            if (window == null) return;
            var nav = window.GetComponent<GamepadNavigationController>();
            if (nav == null) return;
            if (!LazyInput.IsGamepadActive)
            {
                if (nav.IsEnabled) nav.Disable(); // the mouse: no focus frames, as the game's windows
                return;
            }
            if (!nav.IsEnabled || navDirty)
            {
                GamepadNavigationItem was = nav.IsEnabled ? nav.FocusedItem : null;
                nav.Enable();
                nav.ReinitItems(focusOnFirstActive: false);
                navDirty = false;
                if (was != null && was && was.isActiveAndEnabled) nav.SetFocusedItem(was);
                else FocusStart(nav, window);
            }
            GamepadNavigationItem focused = nav.FocusedItem;
            bool onValueRow = focused != null && focused.GetComponentInParent<UISwitchButton>() != null; // a value row steps itself with Left/Right (UISwitchButton.Update)
            if (Pressed(GameKey.Up) || Pressed(GameKey.DpadUp)) Move(nav, GUIDirection.Up);
            else if (Pressed(GameKey.Down) || Pressed(GameKey.DpadDown)) Move(nav, GUIDirection.Down);
            else if (!onValueRow && (Pressed(GameKey.Left) || Pressed(GameKey.DpadLeft))) Move(nav, GUIDirection.Left);
            else if (!onValueRow && (Pressed(GameKey.Right) || Pressed(GameKey.DpadRight))) Move(nav, GUIDirection.Right);
            else if (!onValueRow && Pressed(GameKey.Select)) { LazyInput.ClearKeyDown(GameKey.Select); nav.SelectFocusedItem(); }
        }

        private static bool Pressed(GameKey k) => LazyInput.GetKeyDown(k);

        private static void Move(GamepadNavigationController nav, GUIDirection dir)
        {
            foreach (GameKey k in new[] { GameKey.Up, GameKey.DpadUp, GameKey.Down, GameKey.DpadDown, GameKey.Left, GameKey.DpadLeft, GameKey.Right, GameKey.DpadRight }) LazyInput.ClearKeyDown(k);
            nav.Navigate(dir);
            KeepInView(nav.FocusedItem);
        }

        // The first focus: the chosen mount's row in the journal, the first row in the colours window.
        private static void FocusStart(GamepadNavigationController nav, GameObject window)
        {
            GamepadNavigationItem start = null;
            if (window == panel)
            {
                string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
                foreach ((string id, Image face, TextMeshProUGUI label) in cards)
                {
                    if (face != null && id.Equals(chosen, StringComparison.OrdinalIgnoreCase)) { start = face.GetComponent<GamepadNavigationItem>(); break; }
                }
            }
            if (start != null) nav.SetFocusedItem(start); else nav.FocusOnFirstActive(0);
            KeepInView(nav.FocusedItem);
        }

        // A focused item inside a scroll view scrolled into view.
        private static void KeepInView(GamepadNavigationItem item)
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

        // The mouse wheel scrolls anywhere on a window, not only over a row (the user, 2026-10-22: the scrollbars could
        // only be dragged). The colours window, while open, takes it whole; on the journal, the list over the list and
        // the right side's rows everywhere else. Read here rather than through the scroll views' own event: that one
        // reaches a view only over something that takes the pointer, and the stage, the margins and the gaps between
        // rows don't.
        private const float WheelStep = 40f; // canvas units per notch
        private static bool wheelSaid;

        private static void Wheel()
        {
            float d = UnityInput.Current.mouseScrollDelta.y;
            if (Mathf.Approximately(d, 0f)) return;
            Vector2 at = UnityInput.Current.mousePosition;
            ScrollRect target = null;
            if (colours != null && colours.activeSelf)
            {
                if (Over(colours, at)) target = coloursScroll;
            }
            else if (panel.activeSelf && Over(panel, at))
            {
                target = listScroll != null && Over(listScroll.gameObject, at) ? listScroll : contentScroll;
            }
            if (target == null || target.content == null || target.viewport == null) return;
            float room = target.content.rect.height - target.viewport.rect.height;
            if (room <= 0f) return; // all of it fits: nothing to scroll
            Vector2 p = target.content.anchoredPosition;
            float y = Mathf.Clamp(p.y - d * WheelStep, 0f, room); // the content's top is pinned: up the page is a larger y
            if (!wheelSaid)
            {
                wheelSaid = true;
                Plugin.Log.LogInfo($"Settings: the mouse wheel scrolls {target.name} ({d:0.##} a notch, {room:0} units to scroll). Said once.");
            }
            if (Mathf.Approximately(y, p.y)) return;
            target.StopMovement();
            target.content.anchoredPosition = new Vector2(p.x, y);
        }

        private static bool Over(GameObject go, Vector2 screen)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return false;
            Canvas c = go.GetComponentInParent<Canvas>();
            Canvas root = c != null ? c.rootCanvas : null;
            Camera cam = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, cam);
        }

        // A thin scrollbar at the right edge of a scroll view, shown while there is more than fits.
        private static void AddScrollbar(ScrollRect sr)
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

        // A window scaled down as a whole when the canvas is smaller than it (never cut), centred.
        private static void FitToScreen(GameObject window, float w, float h)
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

        // The self-test's view of the panel ("settings: the Mounts panel opens...").
        internal static int TestRows => rows.Count;
        internal static int TestColourRows => colourRows.Count;
        internal static int TestEntries => entries.Count;
        internal static string TestBuiltFor => builtFor;
        internal static void TestSelect(string id) => Select(id);
        internal static void TestClose(bool keep, bool commit = false)
        {
            committing = commit; // commit: as Ride and Drive the cart close it
            Close(keep);
        }
        internal static void TestOpenColours() => OpenColours();
        internal static void TestCloseColours(bool keep) => CloseColours(keep);
        internal static (float W, float H, float Scale, float RoomW, float RoomH) TestMeasure()
        {
            if (panel == null) return default;
            var canvasRt = panel.transform.parent as RectTransform;
            return (W, H, panel.transform.localScale.x, canvasRt != null ? canvasRt.rect.width - 2f * EdgeGap : 0f, canvasRt != null ? canvasRt.rect.height - 2f * EdgeGap : 0f);
        }

        // After a change: every row's value (one row's change can show another), the rows a rule greys; a window's rows
        // built again when a preset shows or hides a row; the right side built again when the mount changed.
        internal static void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
            if (!chosen.Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
            {
                BuildRight(); // the mount changed under the panel (the cfg, a key): the right side follows
                MarkList();
                return;
            }
            if (Signature(journalGroups) != journalSignature)
            {
                journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, rightArea.rect.width - ScrollbarW, rows, out _); // a preset chosen or left: its rows come or go
            }
            if (colours != null && Signature(colourGroups) != coloursSignature)
            {
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW, colourRows, out _);
            }
            RefreshRows(rows);
            if (colours != null) RefreshRows(colourRows);
            MarkList();
        }

        private static void RefreshRows(List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> list)
        {
            foreach (var (row, go, refresh, group) in list)
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
        }

        internal static void SavePickedNow() { page?.SavePicked(now: true); lastPage?.SavePicked(now: true); }

        private static MountSettingsPage lastPage; // the page of the panel last closed: its pick written half a second on

        internal static void Tick()
        {
            page?.SavePicked(now: false);
            if (lastPage != null && lastPage != page) lastPage.SavePicked(now: false); // a pick written to the cfg file once the picking pauses (MountSettingsPage.SelectMount)
            if (panel != null && (!Mount.IsGameReady || (!panel.activeSelf && colours == null)))
            {
                Close(keep: true, back: false); // the game left (a load, the main menu): nothing to go back to (RestoreUnder is skipped)
                return;
            }
            if (search != null && search.isFocused) searchFocusFrame = Time.frameCount;
            if (panel != null) Wheel();
            if (panel != null && controlsTaken) TakeControls(); // re-asserted: a game popup closing gives ByUI back
            if (panel != null && controlsTaken) TickGamepad();
            if (panel != null && (UnityInput.Current.GetKeyDown(KeyCode.Escape) || (LazyInput.IsGamepadActive && LazyInput.GetKeyDown(GameKey.Back)))) // the gamepad's Back is the game windows' close too
            {
                // Escape steps back: out of the search box first (the field already put its text back), then the colours
                // window to the journal, then the journal closed (keeping what was chosen). The game reads the same press
                // and opens its pause menu: closed again now and a frame later (the order of the two Updates isn't fixed;
                // the user, 2026-10-21: Escape must not fall through to the pause menu).
                // consumed: nothing else of the game's reads this press (the controls are the panel's anyway)
                try { LazyInput.ClearKeyDown(GameKey.InGameMenu); LazyInput.ClearKeyDown(GameKey.Back); } catch { }
                StepBack();
                return;
            }
            if (panel == null || Time.unscaledTime < nextPicture)
            {
                return;
            }
            nextPicture = Time.unscaledTime + 0.2f;
            if (page != null && !(page.MountChosen ?? MountCatalog.DonkeyId).Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
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
                Texture pic;
                using (Perf.Measure(Perf.S.Preview)) pic = PreviewStage.Picture(paper);
                if (paper != null && !paper.LooksRig.Settling)
                {
                    Looks.HoldForPanel(paper.LooksRig.Wearing); // picked again: there at once
                    paper.SoundsRig.Preload(); // its sounds loading meanwhile, one file a frame: ready when the world's mount turns into it
                }
                if (pic == null && PreviewStage.Failed)
                {
                    pictureTex = paper != null ? Colors.BuildPreview(paper, pictureTex) : null;
                    pic = pictureTex;
                }
                // as large as the stage allows, up to the scale, centred in it (the stage renders at 3x detail: a fraction of that is a smooth downscale)
                if (panel.activeSelf) Fit(picture, stage, pic, PictureScale);
                if (colours != null && coloursPicture != null) Fit(coloursPicture, coloursStage, pic, CPictureScale);
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

        private static int searchFocusFrame = -10;

        private static bool controlsTaken;

        // One step back (Escape): out of the search box, else the colours window to the journal, else the journal closed
        // and the window under it shown again. The self-test walks it too (it can't press a key).
        private static void StepBack()
        {
            if (search != null && searchFocusFrame >= Time.frameCount - 1) search.DeactivateInputField();
            else if (colours != null) CloseColours(keep: true);
            else Close(keep: true); // and back to where it was opened from
        }

        internal static void TestStepBack() => StepBack();
        internal static bool TestControlsTaken => controlsTaken;

        // The way back, a stack: the game's own windows below (the pause menu, the Mods menu on a mod's page, the Settings
        // window on a page...), then the journal, then the colours window. The game window on top when the panel opens
        // is not closed: it stays in the game's window stack as it is (modal: the game stays paused) with its GameObject
        // switched off, so its own Escape handling doesn't run under the panel; the panel closing switches it back on,
        // same page and state, and its own Escape goes on from there. Closing and reopening it would not do: the Mods
        // menu's Close reopens the pause menu and forgets where it came from, and its Open starts on the mod list
        // (GK2 Mod Framework's ModsMenuWindow, read 2026-10-21).
        private static LazyWindow<LazyWidgetDataBase> under;

        private static void HideUnder()
        {
            under = null;
            try
            {
                if (LazyWindowsStackController.ActiveWindow is LazyWindow<LazyWidgetDataBase> top && top != null && top.gameObject.activeSelf)
                {
                    under = top;
                    top.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Settings: the window under the Mounts panel could not be set aside (" + e.Message + ").");
            }
        }

        // back: the window under it shown again as it was. Not back (Ride, Drive): every game window closed, into the game.
        private static void RestoreUnder(bool back)
        {
            LazyWindow<LazyWidgetDataBase> w = under;
            under = null;
            if (w == null) return; // opened from the game: nothing to show again
            try
            {
                w.gameObject.SetActive(true);
                if (back)
                {
                    Plugin.Log.LogInfo($"Settings: the Mounts panel closed: back to {w.name}.");
                    return;
                }
                // into the game: close the chain the game and the framework built (each Close may open the one before it)
                for (int i = 0; i < 8 && LazyWindowsStackController.ActiveWindow is LazyWindow<LazyWidgetDataBase> top && top != null; i++)
                {
                    if (!top.gameObject.activeSelf) top.gameObject.SetActive(true);
                    top.Close();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the window under the Mounts panel could not be shown again (" + e.Message + ").");
            }
        }

        private static string UnderText() => under != null ? under.name : "the game";

        // The player's controls taken while the panel is open, as the game's own modal windows do (ByUI: the ride goes
        // on, as with a chest or the inventory): the game's own key reader (PlayerInputHandler, the pause menu on Escape,
        // E, I, movement) does not run under the panel. Re-asserted every tick: the game gives ByUI back when its last
        // modal window closes, which a popup over the panel would do.
        private static void TakeControls()
        {
            try
            {
                PlayerController pc = MainGame.PlayerController;
                if (pc == null) return;
                if (pc.IsControlEnabledByType(TakenControlType.ByUI)) pc.SetControlTakenType(TakenControlType.ByUI, isEnabled: false);
                controlsTaken = true;
            }
            catch (Exception e)
            {
                if (!controlsTaken) Plugin.Log.LogInfo("Settings: the Mounts panel could not take the controls (" + e.Message + "); the game's keys work under it.");
            }
        }

        private static void ReleaseControls()
        {
            if (!controlsTaken) return;
            controlsTaken = false;
            try
            {
                PlayerController pc = MainGame.PlayerController;
                if (pc != null && !LazyWindowsStackController.HasAnyModalWindowOpened) pc.SetControlTakenType(TakenControlType.ByUI, isEnabled: true); // a game window open meanwhile keeps its own hold
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the Mounts panel could not give the controls back (" + e.Message + ").");
            }
        }

        private static void Fit(RawImage img, RectTransform box, Texture pic, float maxScale)
        {
            if (img == null || box == null) return;
            if (pic != null)
            {
                img.texture = pic;
                fitted[img] = (pic.width, pic.height);
                if (!refitHooked)
                {
                    refitHooked = true;
                    Canvas.willRenderCanvases += Refit;
                }
                Vector2 art = PreviewStage.ArtSize(pic);
                float k = Mathf.Min(maxScale, (box.rect.width - 16f) / Mathf.Max(1f, art.x), (box.rect.height - 16f) / Mathf.Max(1f, art.y));
                if (k >= 1f) k = Mathf.Floor(k); // the stage is point-filtered: a whole number of screen pixels per art pixel, no shimmer
                ((RectTransform)img.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
            }
            img.gameObject.SetActive(pic != null);
        }

        // The stage frames a new mount's picture in the same frame the mount changes and resizes its texture in place
        // (PreviewStage.NewTexture): the picture's box, fitted only on the 0.2 s picture timer, showed the new mount in
        // the old one's box for a few frames, squeezed smaller (the user's video, 2026-10-22: the dragon and its cart
        // small for 4 frames, then their size). So every frame, just before the canvases draw, a picture whose texture
        // is no longer the size it was fitted at is fitted again.
        private static readonly Dictionary<RawImage, (int W, int H)> fitted = new Dictionary<RawImage, (int, int)>();
        private static bool refitHooked;

        private static void Refit()
        {
            try
            {
                if (picture != null && stage != null) RefitOne(picture, stage, PictureScale);
                if (coloursPicture != null && coloursStage != null) RefitOne(coloursPicture, coloursStage, CPictureScale);
            }
            catch (Exception e)
            {
                Canvas.willRenderCanvases -= Refit; // never every frame: off for the session, said once
                Plugin.Log.LogWarning("Settings: the Mounts panel's picture refit is off (" + e.Message + ").");
            }
        }

        private static void RefitOne(RawImage img, RectTransform box, float maxScale)
        {
            if (!img || !box || !img.gameObject.activeInHierarchy) return;
            Texture t = img.texture;
            if (t == null || !fitted.TryGetValue(img, out (int W, int H) was) || (was.W == t.width && was.H == t.height)) return;
            Fit(img, box, t, maxScale);
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

        private static void Close(bool keep, bool back = true)
        {
            bool wasOpen = panel != null;
            MountSettingsPage closing = page;
            if (wasOpen) Perf.TracePick("the panel closed, " + (closing?.MountChosen ?? MountCatalog.DonkeyId) + " chosen"); // the world's mount turning into it
            closing?.SavePickedSoon(); // the picking is over: the chosen mount to the cfg file half a second on (put back below when not kept: written again by its set)
            if (closing != null) lastPage = closing; // its pending write is done by Tick after the panel is gone
            Looks.ReleasePanelHolds(); // the looks shown while it was open (the worn ones stay with their wearers)
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Catalog.MountsChanged -= OnRegistryChanged;
            if (colours != null) CloseColours(keep: true, reshow: false); // its rows are the journal's snapshot's too; the journal goes next
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
                bool commit = committing;
                committing = false;
                closing?.ReleaseMountForPanel(commit); // Ride / Drive: the mount picked is his; else his mount stays
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the mount held for the Mounts panel could not be let go: " + e.Message);
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
            journalGroups.Clear();
            colourGroups.Clear();
            statLines.Clear();
            builtFor = null;
            journalSignature = coloursSignature = null;
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
            listContent = rightArea = stage = contentRoot = null;
            listScroll = contentScroll = null;
            search = null;
            filter = "";
            titleCount = headerName = headerDesc = null;
            templateRow = null;
            dialogTemplate = null;
            font = null;
            fontMaterial = null;
            nextPicture = 0f;
            fitted.Clear(); // the pictures go with the windows
            page = null;
            if (wasOpen)
            {
                if (Mount.IsGameReady) RestoreUnder(back); // the game gone (a load, the main menu): its windows went with it
                else under = null;
                ReleaseControls(); // after: a game window shown again keeps its own hold on the controls
            }
        }
    }
}
