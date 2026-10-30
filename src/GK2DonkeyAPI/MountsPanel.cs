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
    // (speed, the cart, babies, skins); under them the row that defines its look at a glance, the Skin, as the game's
    // own rows. A "Customize" button opens a second window of the same make with the colour rows in two full columns
    // (the user, 2026-10-21: the journal must not squash the customisation; the colours get a window of their own; the
    // effect rows went on 2026-10-25). Done and Escape keep what was chosen (no Cancel since 2026-10-21; the snapshot below still lets a self-test put back what was set
    // when the window opened). Only in a game.
    //
    // Built by hand at a fixed size (1060x600 on the game's 1280x720 canvas; scaled only on a smaller canvas), no
    // nested auto-sizing. From the game (UiKit): the window frame sprite, its dialog button's face (sliced onto the
    // panel's own buttons), its font (read off one of its labels) and its rows' measures. Every row is our own control
    // (RowControls); the game window under the panel waits switched off (GameWindowAside). If a part can't be found,
    // the panel doesn't open and the log says so.
    internal static class MountsPanel
    {
        private const float W = 1060f, H = 600f, Pad = 20f, Gap = 8f, FaceInset = 13f; // FaceInset: the game's BackMask inside its frame
        // TitleH: from the window's top to the content: the game's title bar (11 in, 26 high: its windows' HeaderGroup, read
        // 2026-10-27 by Dev/UiProbe.cs) and a gap, less Pad and Gap, which the layout adds; HeaderH fits the name and one line
        private const float TitleH = 11f + 26f + 6f - 20f, ListW = 260f, HeaderH = 60f, ButtonsH = 40f, CardH = 44f, ScrollbarW = 6f, ButtonW = 130f, ButtonH = 34f;
        // the stage fills the right side's height under the header (the user, 2026-10-25: "there's empty space"); one size
        // on every mount: nothing moves
        private const float StageW = 440f, StageH = H - (Pad + TitleH + Gap) - HeaderH - Gap - (Gap + ButtonsH + Pad), PictureScale = 3f;
        // the card is as tall as its lines, not as the stage: a heading with a rule under it (as the colours window's), then
        // the lines close together with a faint divider between them (the user, 2026-10-27: "a lot of empty space and gaps")
        private const int StatCount = 3; private const float StatLineH = 26f, StatsTitleH = 32f, StatsCardH = StatsTitleH + StatCount * StatLineH + 8f;
        // the abilities card under it: one line each, an icon, the name and a line about it (as tall as the mount's abilities)
        private const int AbilityMax = 3; private const float AbilityLineH = 44f, AbilityIconPx = 32f;
        private const float IconPx = 36f, HeaderIconPx = 64f;
        private const float SearchH = 26f;
        private const int SearchFrom = 8; // a search box above the list once it has more mounts than this
        // a custom colour's row: the 24 named colours as squares, two rows of twelve, across the controls' span of its
        // column (SwatchRow; laid on the game row's measured lines until 2026-10-26, which stopped measuring true).
        // MeasureRow still logs where the game row's label ends and its "< value >" spans (from its drawn left).
        private const float SwatchGap = 2f;
        private const float JournalCtrlW = 140f; // the journal's wide controls (a list, the numbers, a slider, the name)
        private const float ColoursColW = 369f, ColoursLabelW = 150f; // the colours window: two columns side by side, each its labels then its controls
        // the colours window's columns end this far before its scrollbar (the user, 2026-10-27: the right column's dropdowns and
        // last swatch ran under it)
        private const float ColoursScrollGap = 10f;
        private const float EdgeGap = 24f; // the least room left at the screen's edges before a window is scaled down
        private const float CW = 800f, CH = 640f, CStageW = StageW, CStageH = 236f, CPictureScale = PictureScale; // the colours window and its picture: the journal's stage and size, so the mount is drawn as big in both (the user, 2026-10-22: smaller in the colours window)

        private static GameObject panel, colours;
        private static MountSettingsPage page;
        // what the windows are built with (the game's look, read when the panel opens) and the rows' controls (UiKit, RowControls)
        private static readonly UiKit ui = new UiKit("GK2DonkeyAPI mounts panel ");
        private static readonly RowControls controls = new RowControls(ui, () => colours != null ? colours : panel, Refresh);

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
        private static string builtForSkin;        // and the skin its Skin row said then (the colours window is that skin's, 0.41.0)
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
            internal readonly List<(string Name, string Line, string Icon)> Abilities = new List<(string, string, string)>(); // the abilities card: Flying, Cargo cart, a mod's
            internal bool HasPortrait;
            internal bool Fake; // a made-up entry (Debug.MountsPanelFakeEntries): listed, not pickable
        }

        internal static bool IsOpen => panel != null && (panel.activeSelf || colours != null);
        internal static bool SearchFocused => controls.TypingField() != null; // the search box or a row's text box (the name)
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
                e.Description = !string.IsNullOrEmpty(k.Description) ? k.Description : def?.Description ?? (k == Catalog.Donkey ? "The graveyard's most patient worker. It has carried more corpses than anyone in the village, and has opinions on each." : "");
                e.HasPortrait = k.PortraitTexture != null || !string.IsNullOrEmpty(k.Portrait) || (def != null && (def.PortraitTexture != null || !string.IsNullOrEmpty(def.Portrait)));
                e.Lines.Add(k.Cart ? "Pulls the cart" : "Can't pull the cart");
                e.Lines.Add(k.Foals ? (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f ? $"Has small babies ({k.FoalScale:0.##} of its size)" : "Has babies") : "No babies");
                // jumping and flying: only while they are switched on (Mounts.FlyingEnabled: on unless the dev tab says otherwise)
                if (Flight.Active && k.Flies) e.Lines.Add("Flies");
                if (Flight.Active && !k.Jumps) e.Lines.Add("Doesn't jump");
                // the mount's speed as a word against the donkey's (Catalog.SpeedWord), and what its cart carries (the user,
                // 2026-10-22: cargo, not the cart's speed, which follows the mount's)
                float ride = Catalog.SpeedOf(k, MountStyle.Ride);
                e.Lines.Add("Speed: " + SpeedText(ride, MountStyle.Ride));
                // what its cart holds: the mount's cargo, or less while a mod limits every cart (Behaviour CartSlots, a session
                // override; Cargo.Rig.WantedSlots). The player's Cart capacity row is gone since 0.12 (the user, 2026-10-27).
                int ownCargo = Catalog.CargoOf(k), holds = Mathf.Min(ownCargo, Plugin.CartSlots.GlobalValue);
                string cargoText = holds < ownCargo ? $"{holds} of {ownCargo} items" : holds + " items";
                if (k.Cart) e.Lines.Add("Cargo: " + cargoText + (holds < ownCargo ? $" (a mod limits the cart to {holds})" : ""));
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
                e.Stats.Add(("Cargo", k.Cart ? cargoText : "No cart"));
                e.Stats.Add(("Skins", skins.ToString()));
                // what it can do (the user, 2026-10-27: "an abilities section ... Flying is an ability and Cargo Cart is another"),
                // each with its icon and a line in the players' words; a mod's own abilities after ours
                if (Flight.Active && k.Flies) e.Abilities.Add(("Flying", "It lands wherever you can stand.", "fly"));
                if (k.Cart) e.Abilities.Add(("Cargo cart", $"Pulls a cart that holds {ownCargo} items.", "cart"));
                foreach (MountAbility a in k.Abilities)
                {
                    string an = string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name;
                    e.Abilities.Add((an, a.Description ?? "", "mount"));
                }
                list.Add(e);
            }
            // a mount whose skins are installed but not the mount (its mod is missing): listed, not pickable (the user,
            // 2026-10-26: listed as unavailable); its skins are offered nowhere else (never as the donkey's)
            foreach (var kv in Catalog.WaitingSkins())
            {
                var names = new List<string>();
                foreach (Looks.SkinDef s in kv.Value) names.Add(string.IsNullOrEmpty(s.Name) ? s.LocalId : s.Name);
                list.Add(new Entry
                {
                    Id = kv.Key,
                    Label = NameFromId(kv.Key) + " (not installed)",
                    Description = $"{(names.Count == 1 ? "A skin" : names.Count + " skins")} for this mount {(names.Count == 1 ? "is" : "are")} installed ({string.Join(", ", names)}), but not the mount itself: install the mod that adds it to ride it.",
                    Fake = true,
                });
            }
            int fakes = Plugin.PanelFakeEntries != null ? Mathf.Clamp(Plugin.PanelFakeEntries.Value, 0, 200) : 0;
            for (int i = 1; i <= fakes; i++)
            {
                list.Add(new Entry { Id = "fake-" + i, Label = "Test mount " + i, Description = "A made-up entry (Debug.MountsPanelFakeEntries), to see the list scroll.", Fake = true });
            }
            return list;
        }

        // The chosen mount whose mod or pack is gone (the user, 2026-10-27: a mod alone, a mount of a missing pack chosen, showed the
        // mount's raw id on the right and no entry for it in the list): a greyed entry at the end, named from its id, so the
        // choice shows and another one can be picked. The choice itself is kept (the donkey comes instead).
        private static void AddMissingChosen(List<Entry> list)
        {
            string chosen = page?.MountChosen;
            if (string.IsNullOrEmpty(chosen) || Catalog.Find(chosen) != null) return;
            if (list.Exists(x => x.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase))) return;
            var e = new Entry
            {
                Id = chosen,
                Label = NameFromId(chosen) + " (not installed)",
                Description = "The mod or pack that adds this mount isn't installed. Your choice is kept, and the donkey comes instead until it is back.",
                Fake = true,
            };
            e.Stats.Add(("Status", "Not installed"));
            list.Add(e);
        }

        // The abilities card for the mount picked: its lines filled, the card as tall as they are (hidden with none), the
        // journal's rows starting under it.
        private static void ShowAbilities(Entry e, Catalog.Entry k)
        {
            if (abilitiesCard == null || contentScroll == null) return;
            int n = e != null ? Mathf.Min(AbilityMax, e.Abilities.Count) : 0;
            for (int i = 0; i < abilityLines.Count; i++)
            {
                var line = abilityLines[i];
                bool on = i < n;
                line.Icon.gameObject.SetActive(false);
                line.Name.gameObject.SetActive(on);
                line.Line.gameObject.SetActive(on);
                if (!on) continue;
                var ab = e.Abilities[i];
                line.Name.text = ab.Name;
                line.Line.text = ab.Line;
                MountIcons.Icon icon = ab.Icon == "fly" ? MountIcons.FrameOf(k, "bdy_glide_left_01") : ab.Icon == "cart" ? MountIcons.CartWheel(page?.MountPaper) : MountIcons.Of(k);
                if (icon.Any)
                {
                    line.Icon.gameObject.SetActive(true);
                    ShowCentred(line.Icon, icon, AbilityIconPx, 12f, StatsTitleH + 4f + i * AbilityLineH + (AbilityLineH - AbilityIconPx) / 2f - 2f);
                }
            }
            float h = n > 0 ? StatsTitleH + 4f + n * AbilityLineH + 6f : 0f;
            abilitiesCard.gameObject.SetActive(n > 0);
            if (n > 0) UiKit.Rect(abilitiesCard.gameObject, statsColX, statsColTop + StatsCardH + Gap, statsColW, h);
            float rowsTop = statsColTop + StatsCardH + Gap + (n > 0 ? h + Gap : 0f);
            UiKit.Rect(contentScroll.gameObject, statsColX, rowsTop, statsColW, Mathf.Max(40f, statsColTop + StageH - rowsTop));
        }

        // A mount's or skin's name from its id when nothing else knows it (its pack not installed): "acme-pack.crab"
        // -> "Crab", "my-mod.big-crab" -> "Big crab", "donkey/acme-pack.blaze" -> "Blaze", "donkey/frost" -> "Frost".
        internal static string NameFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            string s = id.Substring(Mathf.Max(id.LastIndexOf('.'), id.LastIndexOf('/')) + 1).Replace('-', ' ').Replace('_', ' ').Trim();
            return s.Length == 0 ? id : char.ToUpperInvariant(s[0]) + s.Substring(1);
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
                        UiKit.FitToScreen(panel, W, H); // the button again: the panel stays as it is
                        if (colours != null) UiKit.FitToScreen(colours, CW, CH);
                    }
                    return;
                }
                Close(keep: true, back: false);
                page = p;
                p.HoldMountForPanel(); // picking a row shows a mount, Ride or Drive the cart makes it his
                Build(settings);
                // the window it was opened from waits underneath, as it is: the options window (its Mounts button), else the
                // game's (if any)
                overOptions = OptionsWindow.IsShown;
                if (overOptions) OptionsWindow.HideUnderPanel();
                else GameWindowAside.SetAside("the Mounts panel");
                GameWindowAside.TakeControls("the Mounts panel");
                Plugin.Log.LogInfo($"Settings: the Mounts panel opened over {(overOptions ? "the options window" : GameWindowAside.UnderText)}.");
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
            ui.Read(settings); // the game's look: its frame, fonts, button face, focus frame (throws when a part is missing)
            panel = ui.Window("mounts panel", W, H, 30000);
            UiProbe.Dump(settings, ui.DialogTemplate, ui.Font, ui.FontMaterial, ui.FontSize); // dev only, Debug.UiProbe (2026-10-27)

            // What was set when the panel opened, for a put-back (the self-tests; no Cancel button since 2026-10-21): every look row of the page (the mount, every mount's
            // skin and colours), not only the chosen mount's, so a put-back after choosing another mount puts
            // the first one back.
            before.Clear();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.IsLook && r.Entry != null) before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
            }

            // the title line
            // the title in the game's own title bar, with its close button (the user, 2026-10-27: "all that gap for just the word
            // Mounts is not worth it"; the count of mounts is gone)
            ui.TitleBar(panel, W, "Mount Journal", () => Close(keep: true));

            // the list, scrolling; a search box above it once it is long
            entries = Entries();
            AddMissingChosen(entries);
            filter = "";
            float listTop = Pad + TitleH + Gap;
            search = null;
            if (entries.Count > SearchFrom)
            {
                search = SearchBox(panel.transform, Pad, listTop, ListW - 4f, SearchH);
                listTop += SearchH + Gap;
            }
            float listH = H - listTop - Gap - ButtonsH - Pad;
            listScroll = ui.ScrollView(panel.transform, "list", Pad, listTop, ListW, listH, out listContent, 4f);
            ui.AddScrollbar(listScroll);

            // the right side: the header, the stage and the stats card beside it, the look rows under them
            float x0 = Pad + ListW + Gap, rw = W - Pad - x0;
            float rightTop = Pad + TitleH + Gap;
            rightArea = UiKit.Rect(ui.Node("right", panel.transform), x0, rightTop, rw, H - rightTop - Gap - ButtonsH - Pad);
            headerIcon = ui.Node("portrait", rightArea, typeof(RawImage)).GetComponent<RawImage>();
            headerIcon.raycastTarget = false;
            UiKit.Rect(headerIcon.gameObject, 0f, 4f, HeaderIconPx, HeaderIconPx);
            headerIcon.gameObject.SetActive(false);
            headerName = ui.Bold(ui.Text(rightArea, "name", "", 1.35f, TextAlignmentOptions.Left, false, HeaderIconPx + 12f, 0f, rw - HeaderIconPx - 12f, 32f));
            headerDesc = ui.Text(rightArea, "description", "", 0.9f, TextAlignmentOptions.TopLeft, true, HeaderIconPx + 12f, 32f, rw - HeaderIconPx - 12f, HeaderH - 32f);
            headerDesc.overflowMode = TextOverflowModes.Ellipsis;
            // one line under the name (HeaderH): a longer line shrinks a little to fit, down to 80%, before it is cut
            headerDesc.enableAutoSizing = true;
            headerDesc.fontSizeMax = headerDesc.fontSize;
            headerDesc.fontSizeMin = headerDesc.fontSize * 0.8f;

            float y = HeaderH + Gap;
            stage = ui.Box(rightArea, "stage", 0f, y, StageW, StageH);
            picture = ui.Node("picture", stage, typeof(RawImage)).GetComponent<RawImage>();
            picture.raycastTarget = false;
            var pic = (RectTransform)picture.transform;
            pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
            pic.pivot = new Vector2(0.5f, 0.5f);
            picture.gameObject.SetActive(false);
            // a chosen mount that isn't installed: no picture (the donkey drawn under another mount's name read as a mix-up,
            // the user 2026-10-27), this line in its place
            stageNote = ui.Text(stage, "not installed note", "Not installed", 1.1f, TextAlignmentOptions.Center, true, 24f, 0f, StageW - 48f, StageH);
            stageNote.gameObject.SetActive(false);

            float statsX = StageW + 2f * Gap, statsW = rw - statsX;
            RectTransform statsCard = ui.Box(rightArea, "stats", statsX, y, statsW, StatsCardH);
            ui.Bold(ui.Text(statsCard, "stats title", "Stats", 1f, TextAlignmentOptions.BottomLeft, false, 12f, 4f, statsW - 24f, StatsTitleH - 10f));
            GameObject rule = ui.Node("stats rule", statsCard, typeof(Image));
            UiKit.Rect(rule, 12f, StatsTitleH - 4f, statsW - 24f, 1f);
            rule.GetComponent<Image>().color = UiKit.EdgeColor;
            rule.GetComponent<Image>().raycastTarget = false;
            statLines.Clear();
            for (int i = 0; i < StatCount; i++)
            {
                float ly = StatsTitleH + i * StatLineH;
                if (i > 0)
                {
                    GameObject div = ui.Node("stat divider " + i, statsCard, typeof(Image));
                    UiKit.Rect(div, 12f, ly, statsW - 24f, 1f);
                    div.GetComponent<Image>().color = new Color(UiKit.EdgeColor.r, UiKit.EdgeColor.g, UiKit.EdgeColor.b, UiKit.EdgeColor.a * 0.5f);
                    div.GetComponent<Image>().raycastTarget = false;
                }
                TextMeshProUGUI l = ui.Text(statsCard, "stat label " + i, "", 0.9f, TextAlignmentOptions.Left, false, 12f, ly, statsW * 0.42f, StatLineH);
                l.color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.7f);
                TextMeshProUGUI v = ui.Text(statsCard, "stat value " + i, "", 0.95f, TextAlignmentOptions.Right, false, statsW * 0.42f, ly, statsW * 0.58f - 12f, StatLineH);
                v.color = ui.ButtonTextColor;
                statLines.Add((l, v));
            }
            // the abilities card: built with room for AbilityMax lines, sized and filled for the mount picked (BuildRight)
            abilitiesCard = ui.Box(rightArea, "abilities", statsX, y + StatsCardH + Gap, statsW, StatsTitleH);
            ui.Bold(ui.Text(abilitiesCard, "abilities title", "Abilities", 1f, TextAlignmentOptions.BottomLeft, false, 12f, 4f, statsW - 24f, StatsTitleH - 10f));
            GameObject arule = ui.Node("abilities rule", abilitiesCard, typeof(Image));
            UiKit.Rect(arule, 12f, StatsTitleH - 4f, statsW - 24f, 1f);
            arule.GetComponent<Image>().color = UiKit.EdgeColor;
            arule.GetComponent<Image>().raycastTarget = false;
            abilityLines.Clear();
            for (int i = 0; i < AbilityMax; i++)
            {
                float ly = StatsTitleH + 4f + i * AbilityLineH;
                RawImage ai = ui.Node("ability icon " + i, abilitiesCard, typeof(RawImage)).GetComponent<RawImage>();
                ai.raycastTarget = false;
                UiKit.Rect(ai.gameObject, 12f, ly + (AbilityLineH - AbilityIconPx) / 2f - 2f, AbilityIconPx, AbilityIconPx);
                TextMeshProUGUI an = ui.Text(abilitiesCard, "ability name " + i, "", 0.95f, TextAlignmentOptions.BottomLeft, false, 12f + AbilityIconPx + 10f, ly, statsW - 34f - AbilityIconPx, 20f);
                ui.Bold(an);
                an.color = ui.ButtonTextColor;
                TextMeshProUGUI al = ui.Text(abilitiesCard, "ability line " + i, "", 0.78f, TextAlignmentOptions.TopLeft, true, 12f + AbilityIconPx + 10f, ly + 20f, statsW - 34f - AbilityIconPx, AbilityLineH - 20f);
                al.color = new Color(ui.FontColor.r, ui.FontColor.g, ui.FontColor.b, 0.75f);
                al.overflowMode = TextOverflowModes.Ellipsis;
                abilityLines.Add((ai, an, al));
            }
            statsColX = statsX; statsColW = statsW; statsColTop = y;

            // the look rows (the Skin): under the stats card, in its column (the user, 2026-10-25: centred under the stage,
            // under a "Skin" heading, they sat off to the right with empty space round them)
            float rowsTop = y + StatsCardH + Gap;
            journalW = statsW;
            contentScroll = ui.ScrollView(rightArea, "content", statsX, rowsTop, statsW, Mathf.Max(40f, y + StageH - rowsTop), out contentRoot, 2f, manual: true);
            ui.AddScrollbar(contentScroll);

            // the buttons: Colours..., Done (no Cancel: Done and Escape both keep what was chosen, the user 2026-10-21)
            GameObject done = ui.OwnButton(panel.transform, "done", "Done", ButtonW, ButtonH, () => Close(keep: true));
            UiKit.Rect(done, W - Pad - ButtonW, H - Pad - ButtonH, ButtonW, ButtonH);
            GameObject customise = coloursButton = ui.OwnButton(panel.transform, "colours", "Customize", 200f, ButtonH, OpenColours);
            UiKit.Rect(customise, W - Pad - ButtonW - 12f - 200f, H - Pad - ButtonH, 200f, ButtonH); // one gap (12) between all four buttons
            // Ride and Drive: the panel closes (keeping what was chosen) and the mount is called, as the keys would
            GameObject ride = rideButton = ui.OwnButton(panel.transform, "ride", "Ride", ButtonW, ButtonH, () => Call(MountStyle.Ride));
            UiKit.Rect(ride, Pad, H - Pad - ButtonH, ButtonW, ButtonH);
            GameObject drive = driveButton = ui.OwnButton(panel.transform, "drive", "Drive the cart", 150f, ButtonH, () => Call(MountStyle.Cart));
            foreach (GameObject bgo in new[] { done, customise, ride, drive }) UiKit.NavGroup(bgo, 2);
            // the page's options (OptionsWindow), left of Customize, one gap (12) between them; from the options themselves
            // (their Mounts button opened the journal), back to them
            if (OptionsWindow.HasOptions(page))
            {
                GameObject opts = ui.OwnButton(panel.transform, "options", "Options", ButtonW, ButtonH, () =>
                {
                    if (overOptions) Close(keep: true);
                    else OptionsWindow.Open(page);
                });
                UiKit.Rect(opts, W - Pad - ButtonW - 12f - 200f - 12f - ButtonW, H - Pad - ButtonH, ButtonW, ButtonH);
                UiKit.NavGroup(opts, 2);
            }
            var jnav = panel.GetComponent<GamepadNavigationController>();
            UiKit.Hand(jnav, 0, GUIDirection.Right, 1); UiKit.Hand(jnav, 0, GUIDirection.Down, 2);  // the list -> the rows, the buttons
            UiKit.Hand(jnav, 1, GUIDirection.Left, 0); UiKit.Hand(jnav, 1, GUIDirection.Down, 2);   // the rows -> the list, the buttons
            UiKit.Hand(jnav, 2, GUIDirection.Up, 1); UiKit.Hand(jnav, 2, GUIDirection.Left, 0);     // the buttons -> the rows, the list
            UiKit.Hand(jnav, 1, GUIDirection.Up, 0);
            UiKit.Rect(drive, Pad + ButtonW + 12f, H - Pad - ButtonH, 150f, ButtonH);

            pictureOff = false;
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Catalog.MountsChanged -= OnRegistryChanged;
            Mounts.Looks.SkinsChanged += OnRegistryChanged;
            Mounts.Catalog.MountsChanged += OnRegistryChanged;
            panel.SetActive(true); // before the list: a layout under an inactive root computes nothing, so the scroll to the chosen mount was lost
            BuildList();
            BuildRight();
            UiKit.FitToScreen(panel, W, H);
            Plugin.Log.LogInfo($"Settings: the Mounts panel of {page.Title} is open ({entries.Count} mount(s), {rows.Count} look rows for {builtFor}{(panel.transform.localScale.x < 1f ? $", scaled to {panel.transform.localScale.x:0.##} to fit the screen" : "")}).");
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
                GameObject go = ui.OwnButton(listContent, "card " + id, e.Label, ListW - 8f - ScrollbarW, CardH, () => { if (!fake) { Select(id); Sounds.Audition(page?.MountPaper); } }, textLeft: IconPx + 14f, flat: true);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = ListW - 8f - ScrollbarW;
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
                    RawImage img = ui.Node("icon", go.transform, typeof(RawImage)).GetComponent<RawImage>();
                    img.raycastTarget = false;
                    Show(img, icon, IconPx);
                    var irt = (RectTransform)img.transform;
                    irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                    irt.pivot = new Vector2(0f, 0.5f);
                    irt.anchoredPosition = new Vector2(8f + (IconPx - irt.sizeDelta.x) / 2f, 0f);
                }
                cards.Add((id, go.GetComponent<Image>(), go.GetComponentInChildren<TextMeshProUGUI>(true)));
                UiKit.NavGroup(go, 0);
                if (fake) { var fi = go.GetComponent<GamepadNavigationItem>(); if (fi != null) fi.Active = false; }
                if (id.Equals(chosen, StringComparison.OrdinalIgnoreCase)) at = shown - 1;
            }
            controls.NavDirty = true;
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
                if (face != null) face.color = on ? UiKit.CardChosen : UiKit.CardBase;
                if (label != null)
                {
                    label.color = on ? ui.ButtonTextColor : ui.FontColor;
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
                nextPicture = 0f; // the picture follows the pick at once (its view may change: cart or alone)
                using (Perf.Measure(Perf.S.PickRows))
                {
                    BuildRight();
                    MarkList();
                }
            }
        }

        // The colours window's title: the mount's name, and the skin's after it when the chosen skin isn't the mount's
        // first ("Colours: Donkey - Blaze": the window's rows are that skin's, 0.41.0).
        private static string ColoursTitle()
        {
            string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
            string skin = page?.SkinChosen(chosen) ?? "";
            string title = "Colours: " + headerName.text;
            if (skin.Length == 0 || skin.Equals(Looks.FirstSkinOf(chosen), StringComparison.OrdinalIgnoreCase)) return title;
            return title + " - " + (Mounts.Looks.Find(skin)?.Name ?? NameFromId(skin));
        }

        // The right side for the chosen mount: the header, the stats card, the look rows; the colours window's groups
        // gathered too (built when it opens).
        private static void BuildRight()
        {
            string chosen = page.MountChosen ?? MountCatalog.DonkeyId;
            builtFor = chosen;
            builtForSkin = page.SkinChosen(chosen);
            Entry e = entries.Find(x => x.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            Catalog.Entry k = Catalog.Find(chosen);
            MountIcons.Icon icon = MountIcons.Of(k);
            // a mount that can't pull the cart has no Drive the cart (the user, 2026-10-25); one not installed: the donkey comes, it can
            bool drives = MountSettingsPage.MountPullsCart(chosen);
            bool missing = k == null;
            drives = drives && !missing;
            if (driveButton != null && driveButton.activeSelf != drives) { driveButton.SetActive(drives); controls.NavDirty = true; }
            if (rideButton != null && rideButton.activeSelf == missing) { rideButton.SetActive(!missing); controls.NavDirty = true; }
            if (coloursButton != null && coloursButton.activeSelf == missing) { coloursButton.SetActive(!missing); controls.NavDirty = true; }
            if (stageNote != null) stageNote.gameObject.SetActive(missing);
            if (missing && picture != null) picture.gameObject.SetActive(false);
            headerIcon.gameObject.SetActive(icon.Any);
            if (icon.Any) ShowCentred(headerIcon, icon, HeaderIconPx, 0f, Mathf.Max(0f, (HeaderH - HeaderIconPx) / 2f));
            float textX = icon.Any ? HeaderIconPx + 12f : 0f;
            UiKit.Rect(headerName.gameObject, textX, 0f, rightArea.rect.width - textX, 32f);
            UiKit.Rect(headerDesc.gameObject, textX, 32f, rightArea.rect.width - textX, HeaderH - 32f);
            for (int i = 0; i < statLines.Count; i++)
            {
                (string label, string value) = e != null && i < e.Stats.Count ? e.Stats[i] : ("", "");
                statLines[i].Label.text = label;
                statLines[i].Value.text = value;
            }
            ShowAbilities(e, k);
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
                headerName.text = k?.Label ?? NameFromId(chosen) + " (not installed)";
                headerDesc.text = k == null ? "Not installed: the mod or pack that had this mount is gone. Your choice is kept; the donkey comes instead."
                    : why != null ? "Set aside: the game no longer has the pictures this mount's skin needs (the log names them). Your choice is kept; the donkey comes instead."
                    : "It has no skin to wear. Your choice is kept; the donkey comes instead.";
            }

            // the page's look rows for this mount, in groups: the journal takes the Mount section's rows (the Skin); the
            // colours window takes every other look section (the colours, under its two headings)
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
                string group = heading != null && r.ShownSection == headingSection ? heading : (mountSection ? "Skin" : r.ShownSection);
                if (mountSection) AddTo(journalGroups, null, r);  // the journal: the Skin, no heading (the user, 2026-10-25)
                else AddTo(colourGroups, group, r);               // the colours window: every colour row
            }
            foreach (var g in journalGroups) JournalOrder(g.Rows, chosen); // the name, the Skin, then the rest: the same on every mount
            journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, journalW - ScrollbarW, rows, out _);
            if (colours != null)
            {
                coloursTitle.text = ColoursTitle();
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, colourRows, out _);
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
            controls.CloseDropdown(refocus: false); // its button goes with the rows
            foreach (Transform c in root) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            into.Clear();
            var sig = new StringBuilder();
            // the journal: one column as wide as the stats card, the labels on its labels' edge, the controls ending on its
            // values' edge (the scrollbar beyond); the colours window: columns as wide as a swatch row, the controls on
            // the swatches' span
            // the colours window's columns are the panel's own numbers: the game row's measured value span (rowCtrlLeft)
            // came out at 12 of 354 on 2026-10-26 (its switch covers the whole row now), which left the labels 10 px
            // (the user's screenshot: "Co / col" under the dropdowns)
            bool journal = root == contentRoot;
            float colGap = 16f, step = ui.RowVisual.height + 4f;
            float colW = journal ? areaW : Mathf.Floor(Math.Min(ColoursColW, (areaW - colGap) / 2f));
            var lay = journal
                ? new RowLayout { LabelX = 12f, CtrlRight = areaW + ScrollbarW - 12f, CtrlLeft = areaW + ScrollbarW - 12f - JournalCtrlW, ColW = colW }
                : new RowLayout { LabelX = 0f, CtrlLeft = ColoursLabelW + 10f, CtrlRight = colW, ColW = colW };
            if (2f * colW + colGap > areaW) colGap = Math.Max(4f, areaW - 2f * colW); // the two columns as close as the area needs
            int perRow = groups.Count > 1 && 2f * colW + colGap <= areaW ? 2 : 1; // side by side only when both fit whole; else stacked
            float y = 4f;
            for (int g0 = 0; g0 < groups.Count; g0 += perRow)
            {
                int cols = Math.Min(perRow, groups.Count - g0);
                float totalW = cols * colW + (cols - 1) * colGap;
                // centred; wider than the area (the journal's Skin row in the stats column): its value's end on the area's right
                // edge, only the label cell's empty left reaching out of it
                float x0 = totalW <= areaW ? (areaW - totalW) / 2f : areaW - totalW;
                float rowTop = y, tallest = 0f;
                for (int ci = 0; ci < cols; ci++)
                {
                    var group = groups[g0 + ci];
                    float cx = x0 + ci * (colW + colGap), cy = rowTop;
                    bool any = false;
                    foreach (MountSettingsPage.Row r in group.Rows) if (r.Visible == null || r.Visible()) { any = true; break; }
                    if (group.Group != null && any) // a heading only over rows that show (a row hidden by its own condition)
                    {
                        // left on the column's start (a game heading row right-aligns on the labels' edge and floated mid-column)
                        TextMeshProUGUI h = ui.Text(root, "heading " + group.Group, group.Group, 1f, TextAlignmentOptions.BottomLeft, false, cx, cy, colW, step - 6f);
                        ui.Bold(h);
                        GameObject line = ui.Node("heading line", root, typeof(Image));
                        UiKit.Rect(line, cx, cy + step - 4f, colW, 1f);
                        line.GetComponent<Image>().color = UiKit.EdgeColor;
                        line.GetComponent<Image>().raycastTarget = false;
                        cy += step + 2f;
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
                            go = SwatchRow(root, r, lay, out refresh, out float sh); // the named colours as squares across the controls' span
                            UiKit.Place(go, cx, cy);
                            cy += sh + 4f;
                        }
                        else if ((go = controls.OwnRow(root, r, lay, out refresh, out float h)) != null)
                        {
                            UiKit.Place(go, cx, cy); // the panel's own controls (RowControls)
                            cy += h + 4f;
                        }
                        else
                        {
                            Plugin.Log.LogInfo($"Settings: {page.Title}'s row '{r.Label}' has nothing to choose from; left out of the Mounts panel.");
                            continue; // a list with no choices (every control is our own: no game row to fall back on)
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
            controls.NavDirty = true;
            return sig.ToString();
        }

        private static bool IsPalette(MountSettingsPage.Row r) => r?.Entry != null && r.Entry.SettingType == typeof(PaletteColor);

        // A custom colour's row: the label where a game row's label sits, then the 24 named colours as squares in two
        // rows of twelve; the chosen one framed; a click sets the row's value (the page applies it, the picture shows it).
        private static GameObject SwatchRow(Transform parent, MountSettingsPage.Row r, RowLayout lay, out Action refresh, out float height)
        {
            var entry = (BepInEx.Configuration.ConfigEntry<PaletteColor>)r.Entry;
            int perRow = 12;
            // the squares across the controls' span, as the other rows' controls (2026-10-26)
            float px = Mathf.Max(8f, Mathf.Floor((lay.CtrlRight - lay.CtrlLeft - (perRow - 1) * SwatchGap) / perRow));
            float left = lay.CtrlRight - (perRow * px + (perRow - 1) * SwatchGap); // the rounding's slack on the left: they end on the value edge
            GameObject go = ui.Node("swatches " + r.Entry.Definition.Key, parent);
            // left on the column's edge, as the own rows' labels and the headings (2026-10-26); wrapping, the row as tall as it needs
            float labelW = Mathf.Max(40f, left - 10f - lay.LabelX);
            TextMeshProUGUI label = ui.Text(go.transform, "label", r.Label, 0.95f, TextAlignmentOptions.Left, true, lay.LabelX, 0f, labelW, 30f);
            height = Mathf.Max(2f * px + SwatchGap + 6f, Mathf.Ceil(label.GetPreferredValues(r.Label ?? "", labelW, 0f).y) + 8f);
            UiKit.Rect(go, 0f, 0f, lay.ColW, height);
            UiKit.Rect(label.gameObject, lay.LabelX, 0f, labelW, height);
            var frames = new List<(PaletteColor Colour, GameObject Frame)>();
            Array values = Enum.GetValues(typeof(PaletteColor));
            float top = Mathf.Round((height - (2f * px + SwatchGap)) / 2f);
            for (int i = 0; i < values.Length; i++)
            {
                var colour = (PaletteColor)values.GetValue(i);
                float sx = left + (i % perRow) * (px + SwatchGap);
                float sy = top + (i / perRow) * (px + SwatchGap);
                GameObject frame = ui.Node("frame " + colour, go.transform, typeof(Image));
                UiKit.Rect(frame, sx - 2f, sy - 2f, px + 4f, px + 4f);
                frame.GetComponent<Image>().color = Color.white;
                frame.GetComponent<Image>().raycastTarget = false;
                frame.SetActive(false);
                GameObject sw = ui.Node("swatch " + colour, go.transform, typeof(Image), typeof(UnityEngine.UI.Button), typeof(GamepadNavigationItem));
                UiKit.Rect(sw, sx, sy, px, px);
                GameObject swFocus = ui.Node("focus", sw.transform, typeof(Image), typeof(Outline));
                UiKit.Rect(swFocus, -3f, -3f, px + 6f, px + 6f);
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

        // The colours of the chosen mount, in a window of their own over the journal (hidden meanwhile): the colour rows
        // in two full columns under their headings. Done keeps,
        // Done or Escape goes back to the journal (no Cancel since 2026-10-21).
        private static void OpenColours()
        {
            if (panel == null || colours != null) return;
            try
            {
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                colours = ui.Window("colours window", CW, CH, 30001);
                coloursTitle = ui.TitleBar(colours, CW, ColoursTitle(), () => CloseColours(keep: true));
                float top = Pad + TitleH + Gap;
                coloursStage = ui.Box(colours.transform, "colours stage", (CW - CStageW) / 2f, top, CStageW, CStageH); // the mount as it is coloured, so one sees what one changes
                coloursPicture = ui.Node("colours picture", coloursStage, typeof(RawImage)).GetComponent<RawImage>();
                coloursPicture.raycastTarget = false;
                var cprt = (RectTransform)coloursPicture.transform;
                cprt.anchorMin = cprt.anchorMax = new Vector2(0.5f, 0.5f);
                cprt.pivot = new Vector2(0.5f, 0.5f);
                coloursPicture.gameObject.SetActive(false);
                top += CStageH + Gap;
                coloursScroll = ui.ScrollView(colours.transform, "colours content", Pad, top, CW - 2f * Pad, CH - top - Gap - ButtonsH - Pad, out coloursRoot, 2f, manual: true);
                ui.AddScrollbar(coloursScroll);
                GameObject done = ui.OwnButton(colours.transform, "colours done", "Done", ButtonW, ButtonH, () => CloseColours(keep: true));
                UiKit.Rect(done, CW - Pad - ButtonW, CH - Pad - ButtonH, ButtonW, ButtonH);
                UiKit.NavGroup(done, 1);
                var cnav = colours.GetComponent<GamepadNavigationController>();
                UiKit.Hand(cnav, 0, GUIDirection.Down, 1); UiKit.Hand(cnav, 1, GUIDirection.Up, 0); // the rows and swatches <-> Done
                coloursBefore.Clear();
                foreach (var g in colourGroups) foreach (MountSettingsPage.Row r in g.Rows) if (r.Entry != null) coloursBefore[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, colourRows, out _);
                panel.SetActive(false); // the journal waits underneath
                controls.NavDirty = true;
                colours.SetActive(true);
                UiKit.FitToScreen(colours, CW, CH);
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
            controls.CloseDropdown(refocus: false);
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
                controls.NavDirty = true;
                Refresh();
            }
        }

        // The panel closed (what was chosen kept) and the companion's mount called in the style, as the ride and cart keys do.
        private static bool committing; // Close from Ride / Drive the cart: the mount picked becomes his
        private static GameObject driveButton; // hidden for a mount that can't pull the cart
        private static RectTransform abilitiesCard;
        private static readonly List<(RawImage Icon, TextMeshProUGUI Name, TextMeshProUGUI Line)> abilityLines = new List<(RawImage, TextMeshProUGUI, TextMeshProUGUI)>();
        private static float statsColX, statsColW, statsColTop;
        private static GameObject rideButton, coloursButton; // hidden, with Drive, while the chosen mount isn't installed
        private static TextMeshProUGUI stageNote; // "Not installed" on the empty stage
        // The chosen mount's mod or pack is gone: nothing to draw, ride or colour here; pick another (the choice is kept).
        private static bool ChosenMissing => page != null && Catalog.Find(page.MountChosen ?? MountCatalog.DonkeyId) == null;
        private static float journalW = 280f;  // the journal's rows' column (the stats card's)

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
            TMP_InputField field = controls.InputBox(parent, "search", x, y, w, h, "Search...", 40);
            field.onValueChanged.AddListener(v =>
            {
                filter = v ?? "";
                if (listContent != null) BuildList();
            });
            return field;
        }

        // ---- the pieces ----

        // An icon shown whole inside a square of px canvas units, pixel art at a whole-number scale when it fits one.
        private static void Show(RawImage img, MountIcons.Icon icon, float px)
        {
            img.texture = icon.Texture;
            img.uvRect = icon.Uv;
            float k = Mathf.Min(px / Mathf.Max(1, icon.W), px / Mathf.Max(1, icon.H));
            if (k >= 1f) k = Mathf.Floor(k);
            ((RectTransform)img.transform).sizeDelta = new Vector2(Mathf.Round(icon.W * k), Mathf.Round(icon.H * k));
        }

        // The same, centred in its px box whose top left is (x, y) (the user, 2026-10-27: a mount sat up by the name: a wide
        // short picture kept the box's top left)
        private static void ShowCentred(RawImage img, MountIcons.Icon icon, float px, float x, float y)
        {
            Show(img, icon, px);
            Vector2 size = ((RectTransform)img.transform).sizeDelta;
            UiKit.Rect(img.gameObject, Mathf.Round(x + (px - size.x) / 2f), Mathf.Round(y + (px - size.y) / 2f), size.x, size.y);
        }


        // ---- the gamepad, every frame (from Tick) ----

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
            UiKit.KeepInView(nav.FocusedItem);
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
            if (controls.DropdownOpen)
            {
                if (controls.DropScroll != null && UiKit.Over(controls.DropScroll.gameObject, at)) target = controls.DropScroll; // an open list takes the wheel alone
            }
            else if (colours != null && colours.activeSelf)
            {
                if (UiKit.Over(colours, at)) target = coloursScroll;
            }
            else if (panel.activeSelf && UiKit.Over(panel, at))
            {
                target = listScroll != null && UiKit.Over(listScroll.gameObject, at) ? listScroll : contentScroll;
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

        // The self-test's view of the panel ("settings: the Mounts panel opens...").
        internal static int TestRows => rows.Count;
        internal static int TestColourRows => colourRows.Count;
        internal static int TestEntries => entries.Count;
        internal static string TestBuiltFor => builtFor;
        internal static void TestSelect(string id) => Select(id);
        internal static bool TestDriveShown => driveButton != null && driveButton.activeSelf;
        internal static Texture TestPicture => picture != null && picture.gameObject.activeSelf ? picture.texture : null;
        internal static bool TestColourGroup(string heading) => colourGroups.Exists(g => g.Group == heading);
        internal static string TestColourGroups()
        {
            var names = new List<string>();
            foreach (var g in colourGroups) names.Add((g.Group ?? "(none)") + " " + g.Rows.Count);
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }
        internal static void TestClose(bool keep, bool commit = false, bool back = true)
        {
            committing = commit; // commit: as Ride and Drive the cart close it (they close with back: false)
            Close(keep, back);
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
            if (!chosen.Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase) || !string.Equals(page?.SkinChosen(chosen) ?? "", builtForSkin ?? "", StringComparison.OrdinalIgnoreCase))
            {
                BuildRight(); // the mount, or its skin, changed under the panel (the cfg, a key, the Skin row): the right side follows; the colours window is the skin's
                MarkList();
                return;
            }
            if (Signature(journalGroups) != journalSignature)
            {
                journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, journalW - ScrollbarW, rows, out _); // a preset chosen or left: its rows come or go
            }
            if (colours != null && Signature(colourGroups) != coloursSignature)
            {
                coloursSignature = BuildRows(colourGroups, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, colourRows, out _);
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
            if (panel != null && (!Mount.IsGameReady || (!panel.activeSelf && colours == null && !OptionsWindow.IsOverPanel)))
            {
                Close(keep: true, back: false); // the game left (a load, the main menu): nothing to go back to (the game window is forgotten)
                return;
            }
            // the rest only while the journal or its colours window is the one shown (the options window over it has its own)
            GameObject shown = colours != null ? colours : panel != null && panel.activeSelf ? panel : null;
            if (shown != null)
            {
                controls.TrackTyping();
                Wheel();
                if (GameWindowAside.ControlsTaken) GameWindowAside.TakeControls("the Mounts panel"); // re-asserted: a game popup closing gives ByUI back
                if (GameWindowAside.ControlsTaken) controls.TickGamepad(shown, nav => FocusStart(nav, shown));
                // Escape steps back: out of the search box first (the field already put its text back), then the colours
                // window to the journal, then the journal closed (keeping what was chosen). The game reads the same press
                // and would open its pause menu: consumed (the user, 2026-10-21: Escape must not fall through to the pause menu).
                if (GameWindowAside.TakeEscape())
                {
                    StepBack();
                    return;
                }
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
                Mount.Instance paper = PaperOf(page);
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
                // switching between the two views (a mount that can't pull the cart and one that can): the photo shown stays
                // until the other view has the new look whole (its stale photo is an older mount's), asked again each frame
                // meanwhile (the user, 2026-10-25: the new mount "large and without a cart" for a few frames)
                else if (!PreviewStage.Failed && picture != null && picture.texture != null && picture.texture != pic && picture.gameObject.activeSelf
                    && !PreviewStage.ShowsLookNow(pic, paper))
                {
                    pic = picture.texture;
                    nextPicture = 0f;
                }
                // as large as the stage allows, up to the scale, centred in it (the stage renders at 3x detail: a fraction of that is a smooth downscale)
                if (panel.activeSelf && !ChosenMissing) Fit(picture, stage, pic, PictureScale);
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

        // One step back (Escape): out of a text box (the search, the name), else a dropdown's list closed, else the colours
        // window to the journal, else the journal closed and the window under it shown again. The self-test walks it too
        // (it can't press a key).
        private static void StepBack()
        {
            if (controls.LeaveTyping()) { }
            else if (controls.DropdownOpen) controls.CloseDropdown(refocus: true);
            else if (colours != null) CloseColours(keep: true);
            else Close(keep: true); // and back to where it was opened from
        }

        internal static void TestStepBack() => StepBack();
        internal static bool TestControlsTaken => GameWindowAside.ControlsTaken;

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
        // the old one's box for a few frames, squeezed smaller (the user's video, 2026-10-22: a mount and its cart
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
                    // the panel's own lists (the Skin rows: never in the options window, InOptions) are read
                    // again, so a skin added or withdrawn shows as it is now
                    if (page != null) foreach (MountSettingsPage.Row r in page.Rows) if (r.IsLook && r.RowKind == MountSettingsPage.RowKind.List) { r.ChoiceLabels = null; r.ChoiceValues = null; }
                    entries = Entries();
                    AddMissingChosen(entries);
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
            try { controls.CommitFields(); } catch (Exception e) { Plugin.Log.LogWarning("Settings: a name being typed could not be kept: " + e.Message); } // a name still being typed is kept
            controls.CloseDropdown(refocus: false);
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
            before.Clear();
            rows.Clear();
            cards.Clear();
            journalGroups.Clear();
            colourGroups.Clear();
            statLines.Clear();
            builtFor = null;
            builtForSkin = null;
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
            stageNote = null;
            abilitiesCard = null;
            abilityLines.Clear();
            rideButton = coloursButton = null;
            headerIcon = null;
            listContent = rightArea = stage = contentRoot = null;
            listScroll = contentScroll = null;
            search = null;
            filter = "";
            titleCount = headerName = headerDesc = null;
            nextPicture = 0f;
            fitted.Clear(); // the pictures go with the windows
            page = null;
            bool fromOptions = overOptions;
            overOptions = false;
            if (!wasOpen) return;
            if (OptionsWindow.IsOverPanel) OptionsWindow.Discard(); // the options opened from the journal go with it (the game left)
            if (fromOptions && OptionsWindow.IsOpen)
            {
                if (back)
                {
                    OptionsWindow.ShowAfterPanel(); // back to the options it was opened from, as they were
                    return;
                }
                OptionsWindow.Discard(); // Ride, Drive, the game left: the options go too, then the game's windows
            }
            if (Mount.IsGameReady) GameWindowAside.Restore(back, "the Mounts panel"); // the game gone (a load, the main menu): its windows went with it
            else GameWindowAside.Forget();
            GameWindowAside.ReleaseControls("the Mounts panel"); // after: a game window shown again keeps its own hold on the controls
        }

        // The journal over the options window (opened from its Mounts button): Close goes back to them.
        private static bool overOptions;

        // The options window opened over the journal (its Options button) or closed back to it.
        internal static void HideUnderOptions()
        {
            if (panel != null) panel.SetActive(false);
        }

        internal static void ShowAfterOptions()
        {
            if (panel == null) return;
            panel.SetActive(true);
            controls.NavDirty = true;
            Refresh();
        }

        // The journal is open over the options window (the options' Mounts button pressed again goes back to it).
        internal static bool IsOverOptions => panel != null && overOptions;

        internal static bool IsShownOnTop => panel != null && (colours != null || panel.activeSelf);

        // The picture's model: the page's companion's paper mount (MountSettingsPage.Preview), else the one the page holds.
        internal static Mount.Instance PaperOf(MountSettingsPage p) => p?.PreviewOf?.Paper ?? p?.MountPaper;

        // The journal's rows in one order on every mount (the user, 2026-10-26: "the order of these are not all the
        // same for all mounts"; "The skin most definitely should be the second in the list after name to show that the
        // name is per mount not per skin"): a text row (its name) first, then the Skin, then the others in the order
        // the page made the donkey's (a mount registered later had its rows made later: page.Rows alone put its Skin
        // last).
        private static void JournalOrder(List<MountSettingsPage.Row> list, string chosen)
        {
            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook || r.Entry == null || !string.Equals(r.ShownSection, "Mount", StringComparison.OrdinalIgnoreCase)) continue;
                string b = BaseKey(r, r.MountId);
                if (!rank.ContainsKey(b)) rank[b] = i;
                i++;
            }
            int Rank(MountSettingsPage.Row r)
            {
                if (r.RowKind == MountSettingsPage.RowKind.Text) return -2;
                string b = BaseKey(r, chosen);
                if (b.Equals("Skin", StringComparison.OrdinalIgnoreCase)) return -1;
                return rank.TryGetValue(b, out int at) ? at : int.MaxValue;
            }
            // a stable insertion sort (few rows; no LINQ in the game)
            for (int a = 1; a < list.Count; a++)
            {
                MountSettingsPage.Row r = list[a];
                int k = Rank(r), j = a - 1;
                while (j >= 0 && Rank(list[j]) > k)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = r;
            }
        }

        // The self-tests' view of the journal: its rows' keys in order (without the mount's part), how many are still the
        // game's rows, and the first part of a row drawn outside the journal's column (its mask would cut it), or null.
        internal static string TestJournalOrder()
        {
            var keys = new List<string>();
            foreach (var x in rows) keys.Add(BaseKey(x.Row, builtFor));
            return string.Join(",", keys);
        }

        internal static int TestJournalGameRows()
        {
            int n = 0;
            foreach (var x in rows) if (x.Go != null && x.Go.GetComponentInChildren<UISwitchButton>(true) != null) n++;
            return n;
        }

        // The first row of a window (the journal, else the colours window) whose label has less than 100 units, or null
        // (the user's screenshot, 2026-10-26: the colours window's labels 10 wide, "Co / col" under the dropdowns).
        internal static string TestNarrowLabel(bool coloursWindow)
        {
            foreach (var x in coloursWindow ? colourRows : rows)
            {
                if (x.Go == null) continue;
                Transform t = x.Go.transform.Find("GK2DonkeyAPI mounts panel label");
                if (t is RectTransform rt && rt.rect.width < 100f) return $"{x.Row.Label}: {rt.rect.width:0}";
            }
            return null;
        }

        // The name's text box takes the typing: activated, it is focused a frame on (built switched on before its text
        // parts were set, it never was: the user, 2026-10-26). Null: no text box in the journal.
        internal static TMP_InputField TestNameField()
        {
            foreach (var x in rows) if (x.Go != null && x.Row.RowKind == MountSettingsPage.RowKind.Text) return x.Go.GetComponentInChildren<TMP_InputField>(true);
            return null;
        }

        internal static string TestJournalOutside()
        {
            if (contentScroll == null || contentScroll.viewport == null) return "no journal";
            RectTransform view = contentScroll.viewport;
            Rect area = view.rect;
            var corners = new Vector3[4];
            foreach (var x in rows)
            {
                if (x.Go == null) continue;
                foreach (RectTransform rt in x.Go.GetComponentsInChildren<RectTransform>(false))
                {
                    if (rt.rect.width <= 0f) continue;
                    rt.GetWorldCorners(corners);
                    float a = view.InverseTransformPoint(corners[0]).x, b = view.InverseTransformPoint(corners[2]).x;
                    if (Math.Min(a, b) < area.xMin - 0.5f || Math.Max(a, b) > area.xMax + 0.5f)
                        return $"{x.Row.Label}: {rt.name} spans {Math.Min(a, b) - area.xMin:0} to {Math.Max(a, b) - area.xMin:0} of 0 to {area.width:0}";
                }
            }
            return null;
        }

        // A row's key without its mount's part: "Foals_acme-pack.crab" and "Foals" are both "Foals", every
        // "Skin_<mount>" is "Skin".
        private static string BaseKey(MountSettingsPage.Row r, string mountId)
        {
            string key = r.Entry?.Definition.Key ?? "";
            if (key.StartsWith("Skin_", StringComparison.OrdinalIgnoreCase)) return "Skin";
            if (!string.IsNullOrEmpty(mountId) && key.EndsWith("_" + mountId, StringComparison.OrdinalIgnoreCase)) return key.Substring(0, key.Length - mountId.Length - 1);
            return key;
        }
    }
}
