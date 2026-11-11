using System;
using System.Collections.Generic;
using System.Globalization;
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
    // effect rows went on 2026-10-25). Its X and Escape keep what was chosen (no Done since 2026-10-30, no Cancel since 2026-10-21; the snapshot below still lets a self-test put back what was set
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
        private const float CW = 800f, CH = 660f, CStageW = StageW, CStageH = 256f, CPictureScale = PictureScale; // the colours window and its picture: the journal's stage and size, so the mount is drawn as big in both (the user, 2026-10-22: smaller in the colours window); 256 tall (236 until 2026-11-10): a tall mount's picture, 78 art pixels, fits at 3 as in the journal (2.5 at 236, the user's screenshots)
        // the mount alone in the colours window (a tab without the cart's rows) up to 4 per art pixel where the stage has room: the
        // user, 2026-11-10, "its image is larger and you can see it better without the cart" (at 3 the donkey alone was
        // drawn 224x147 in the 440x236 stage, as big as with the cart). Each art pixel is 3 of the picture's, so at 4 it
        // is still 4 whole units; capped, so a small mount isn't blown up 6 or 7 times.
        private const float CAloneScale = 4f;
        private static float ColoursScale(Texture pic) => PreviewStage.IsAloneView(pic) ? CAloneScale : CPictureScale;

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
        // each built row's tie to its page row (RowControls.Bound), and last build's rows waiting to be pointed at the next
        // build's (BuildRows)
        private static readonly Dictionary<GameObject, RowControls.Bound> rowBound = new Dictionary<GameObject, RowControls.Bound>();
        private static readonly List<(GameObject Go, RowControls.Bound B)> spareRows = new List<(GameObject, RowControls.Bound)>();
        internal static int LastRowsReused { get; private set; }  // the last BuildRows: rows kept and pointed at new rows
        internal static int LastRowsMade { get; private set; }    // ... and rows made
        private static readonly List<(string Group, List<MountSettingsPage.Row> Rows)> journalGroups = new List<(string, List<MountSettingsPage.Row>)>();
        private static readonly List<(string Group, List<MountSettingsPage.Row> Rows)> colourGroups = new List<(string, List<MountSettingsPage.Row>)>();
        private static readonly Dictionary<string, string> groupShown = new Dictionary<string, string>(StringComparer.Ordinal); // a colours group's heading in the player's language (Texts), by its English
        // The colours window's tabs (2026-11-08, the user: a skin's many colours apart from the mount's): a tab per heading's
        // rows, named by the heading, and a skin's own parts (Row.SkinPart) on a tab of their own after the rows they came
        // with, named by the skin; the chosen tab's rows in the two columns. Tabs: (key, name drawn, rows).
        private static readonly List<(string Key, string Name, List<MountSettingsPage.Row> Rows)> colourTabs = new List<(string, string, List<MountSettingsPage.Row>)>();
        private static readonly List<(string Key, Image Face, TextMeshProUGUI Label)> tabButtons = new List<(string, Image, TextMeshProUGUI)>();
        private static string colourTab;            // the chosen tab's key
        private static string tabsBuilt;            // the tabs the strip was built for (their keys and names)
        private static RectTransform coloursTabs;   // the strip
        private const float TabH = 34f, TabGap = 6f, TabMinW = 120f, TabMaxW = 260f;
        private const string PartsTab = "\u0001parts"; // the skin's parts' tab (no heading is named so)
        private static List<Entry> entries = new List<Entry>();
        private static string builtFor;            // the mount the right side was built for
        private static string builtForSkin;        // and the skin its Skin row said then (the colours window is that skin's, 0.41.0)
        private static string builtForMissing;     // and the skin not installed its row held then, if any (the stats card says it)
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
        internal static bool Exists => panel != null; // open, shown or waiting under the options window
        private static GameObject optionsButton;

        // The self-test's view of the buttons against the list and the stage (panel units from the left): Options' centre
        // and width, Customize's centre, Drive the cart's left edge, Ride's right edge; the list's edges, the stage's, the
        // window's right margin; NaN for a button not there; whether a Done button is there (none since 2026-10-30).
        internal static (float OptionsCentre, float OptionsWidth, float CustomizeCentre, float DriveLeft, float RideRight, float ListLeft, float ListRight, float StageLeft, float StageRight, float RightMargin, bool Done) TestButtonEdges()
        {
            float X(GameObject go, float along)
            {
                if (go == null) return float.NaN;
                var rt = (RectTransform)go.transform;
                return rt.anchoredPosition.x + rt.sizeDelta.x * along;
            }
            float left = Pad + ListW + Gap;
            bool done = panel != null && panel.transform.Find("GK2DonkeyAPI mounts panel done") != null;
            return (X(optionsButton, 0.5f), X(optionsButton, 1f) - X(optionsButton, 0f), X(coloursButton, 0.5f), X(driveButton, 0f), X(rideButton, 1f),
                Pad, Pad + ListW, left, left + StageW, W - Pad, done);
        }

        // A game window opened over the panel (GameWindowAside.Covered): the journal and its colours window step aside,
        // invisible and taking no clicks or keys, until it closes.
        private static bool covered;

        private static void FollowCover()
        {
            bool now = GameWindowAside.Covered;
            if (now == covered) return;
            covered = now;
            foreach (GameObject w in new[] { panel, colours })
            {
                Canvas c = w != null ? w.GetComponent<Canvas>() : null;
                if (c != null) c.enabled = !now;
            }
            if (now) controls.CloseDropdown(refocus: false);
            else controls.NavDirty = true;
        }

        // Every mount a player can pick (Looks.MountChoices: the mounts with a skin, the donkey first), with its words.
        internal static List<Entry> Entries()
        {
            var list = new List<Entry>();
            foreach ((string id, _) in Looks.MountChoices())
            {
                Catalog.Entry k = Catalog.Find(id);
                if (k == null) continue;
                // the words in the player's language (Texts; built on every open): the donkey's are the API's, another mount's
                // its owner's (the mod or pack that registered it, step 3)
                var e = new Entry { Id = id, Label = Catalog.NameTerm(k).Text };
                Looks.SkinDef def = Looks.Find(Looks.DefaultSkinOf(k));
                e.Description = !string.IsNullOrEmpty(k.Description) ? Catalog.ShownDescription(k) : def?.Description != null ? Looks.ShownDescription(def) : (k == Catalog.Donkey ? UiWords.DonkeyDescription.Text : "");
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
                // a number, never "1 items" (the user's yes, 2026-11-01: no plural chosen in code); "2 of 4" while limited
                string cargoText = holds < ownCargo ? UiWords.CargoOf.Format(holds, ownCargo) : holds.ToString(CultureInfo.InvariantCulture);
                if (k.Cart) e.Lines.Add("Cargo: " + (holds < ownCargo ? $"{holds} of {ownCargo}" : holds.ToString(CultureInfo.InvariantCulture)) + (holds < ownCargo ? $" (a mod limits the cart to {holds})" : ""));
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
                e.Stats.Add((UiWords.StatSpeed.Text, UiWords.Speed(Catalog.SpeedWord(ride, MountStyle.Ride))));
                e.Stats.Add((UiWords.StatCargo.Text, k.Cart ? cargoText : UiWords.NoCart.Text));
                e.Stats.Add((UiWords.StatSkins.Text, skins.ToString()));
                // what it can do (the user, 2026-10-27: "an abilities section ... Flying is an ability and Cargo Cart is another"),
                // each with its icon and a line in the players' words; a mod's own abilities after ours
                if (Flight.Active && k.Flies) e.Abilities.Add((UiWords.Flying.Text, UiWords.FlyingLine.Text, "fly"));
                if (k.Cart) e.Abilities.Add((UiWords.CargoCart.Text, UiWords.CargoCartLine.Format(ownCargo), "cart"));
                foreach (MountAbility a in k.Abilities) e.Abilities.Add((Catalog.AbilityName(k, a), Catalog.AbilityDescription(k, a), "mount"));
                list.Add(e);
            }
            // a mount whose skins are installed but not the mount (its mod is missing): listed, not pickable (the user,
            // 2026-10-26: listed as unavailable); its skins are offered nowhere else (never as the donkey's)
            foreach (var kv in Catalog.WaitingSkins())
            {
                var names = new List<string>();
                foreach (Looks.SkinDef s in kv.Value) names.Add(string.IsNullOrEmpty(s.Name) ? s.LocalId : Looks.NameTerm(s).Text);
                list.Add(new Entry
                {
                    Id = kv.Key,
                    Label = UiWords.NameNotInstalled.Format(NameFromId(kv.Key)),
                    Description = UiWords.WaitingSkins.Format(string.Join(", ", names)), // one form for one skin or many (the user's yes, 2026-11-01)
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
                Label = UiWords.NameNotInstalled.Format(NameFromId(chosen)),
                Description = UiWords.MissingNote.Text,
                Fake = true,
            };
            e.Stats.Add((UiWords.StatStatus.Text, UiWords.NotInstalled.Text));
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
                StepTrace.Begin(IsOpen && page == p ? "the journal key again" : "the journal opened"); // dev only (Dev/StepTrace.cs)
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                StepTrace.Mark("the game's Settings window found");
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
                Close(keep: true, back: false, sound: false); // another page's journal open: the one opening is the sound
                page = p;
                p.HoldMountForPanel(); // picking a row shows a mount, Ride or Drive the cart makes it his
                StepTrace.Mark("the last one closed, the mount held");
                Build(settings);
                StepTrace.Mark("built");
                // the window it was opened from waits underneath, as it is: the options window (its Mounts button), else the
                // game's (if any)
                overOptions = OptionsWindow.IsShown;
                if (overOptions) OptionsWindow.HideUnderPanel();
                else GameWindowAside.SetAside("the Mounts panel");
                GameWindowAside.TakeControls("the Mounts panel");
                Plugin.Log.LogInfo($"Settings: the Mounts panel opened over {(overOptions ? "the options window" : GameWindowAside.UnderText)}.");
                StepTrace.Mark("the window underneath set aside");
                UiSounds.WindowOpened(); // as the game's windows (the Settings window's own win_open)
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the Mounts panel can't open (" + e.Message + "); the mount and its colours are still in the cfg.");
                Close(keep: true, sound: false);
            }
            finally
            {
                StepTrace.End();
            }
        }

        // ---- the journal ----

        private static string builtLanguage; // the language the journal was built in (Tick closes it when it changes)

        private static void Build(UIGameSettingsWindow settings)
        {
            builtLanguage = Texts.Language;
            ui.Read(settings); // the game's look: its frame, fonts, button face, focus frame (throws when a part is missing)
            StepTrace.Mark("build: the game's look read");
            panel = ui.Window("mounts panel", W, H, 30000);
            UiProbe.Dump(settings, ui.DialogTemplate, ui.Font, ui.FontMaterial, ui.FontSize); // dev only, Debug.UiProbe (2026-10-27)
            StepTrace.Mark("build: the window made");

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
            StepTrace.Mark("build: the look rows remembered");
            ui.TitleBar(panel, W, UiWords.JournalTitle.Text, () => Close(keep: true));
            StepTrace.Mark("build: the title bar");

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
            StepTrace.Mark("build: the list's frame");

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

            StepTrace.Mark("build: the header");
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
            stageNote = ui.Text(stage, "not installed note", UiWords.NotInstalled.Text, 1.1f, TextAlignmentOptions.Center, true, 24f, 0f, StageW - 48f, StageH);
            stageNote.gameObject.SetActive(false);

            StepTrace.Mark("build: the stage");
            float statsX = StageW + 2f * Gap, statsW = rw - statsX;
            RectTransform statsCard = ui.Box(rightArea, "stats", statsX, y, statsW, StatsCardH);
            ui.Bold(ui.Text(statsCard, "stats title", UiWords.Stats.Text, 1f, TextAlignmentOptions.BottomLeft, false, 12f, 4f, statsW - 24f, StatsTitleH - 10f));
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
            StepTrace.Mark("build: the stats card");
            // the abilities card: built with room for AbilityMax lines, sized and filled for the mount picked (BuildRight)
            abilitiesCard = ui.Box(rightArea, "abilities", statsX, y + StatsCardH + Gap, statsW, StatsTitleH);
            ui.Bold(ui.Text(abilitiesCard, "abilities title", UiWords.Abilities.Text, 1f, TextAlignmentOptions.BottomLeft, false, 12f, 4f, statsW - 24f, StatsTitleH - 10f));
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
            StepTrace.Mark("build: the abilities card");

            // the look rows (the Skin): under the stats card, in its column (the user, 2026-10-25: centred under the stage,
            // under a "Skin" heading, they sat off to the right with empty space round them)
            float rowsTop = y + StatsCardH + Gap;
            journalW = statsW;
            contentScroll = ui.ScrollView(rightArea, "content", statsX, rowsTop, statsW, Mathf.Max(40f, y + StageH - rowsTop), out contentRoot, 2f, manual: true);
            ui.AddScrollbar(contentScroll);
            StepTrace.Mark("build: the rows' frame");

            // the buttons, on the list's and the stage's edges (the user's screenshots, 2026-10-30): Options centred under
            // the mount list, the buttons' own width; Customize centred under the stage (it colours what the stage shows); Drive the cart and Ride
            // on the right, Drive from the stage's right edge, Ride ending on the window's margin, one gap (12) between
            // them. No Done (the user, 2026-10-30): the title bar's X and Escape close the journal, keeping what was chosen
            // (no Cancel since 2026-10-21).
            float stageLeft = Pad + ListW + Gap, stageRight = stageLeft + StageW, buttonsY = H - Pad - ButtonH;
            float callW = Mathf.Floor((W - Pad - stageRight - 12f) / 2f);
            GameObject customise = coloursButton = ui.OwnButton(panel.transform, "colours", UiWords.Customize.Text, ButtonW, ButtonH, OpenColours);
            UiKit.Rect(customise, Mathf.Round(stageLeft + (StageW - ButtonW) / 2f), buttonsY, ButtonW, ButtonH);
            // Ride and Drive: the panel closes (keeping what was chosen) and the mount is called, as the keys would
            GameObject ride = rideButton = ui.OwnButton(panel.transform, "ride", UiWords.Ride.Text, callW, ButtonH, () => Call(MountStyle.Ride));
            UiKit.Rect(ride, W - Pad - callW, buttonsY, callW, ButtonH);
            GameObject drive = driveButton = ui.OwnButton(panel.transform, "drive", UiWords.DriveCart.Text, callW, ButtonH, () => Call(MountStyle.Cart));
            foreach (GameObject bgo in new[] { customise, ride, drive }) UiKit.NavGroup(bgo, 2);
            // the page's options (OptionsWindow), under the mount list; from the options themselves (their Mounts button
            // opened the journal), back to them
            optionsButton = null;
            if (OptionsWindow.HasOptions(page))
            {
                GameObject opts = optionsButton = ui.OwnButton(panel.transform, "options", UiWords.Options.Text, ButtonW, ButtonH, () =>
                {
                    if (overOptions) Close(keep: true);
                    else OptionsWindow.Open(page);
                });
                UiKit.Rect(opts, Mathf.Round(Pad + (ListW - ButtonW) / 2f), buttonsY, ButtonW, ButtonH);
                UiKit.NavGroup(opts, 2);
            }
            var jnav = panel.GetComponent<GamepadNavigationController>();
            UiKit.Hand(jnav, 0, GUIDirection.Right, 1); UiKit.Hand(jnav, 0, GUIDirection.Down, 2);  // the list -> the rows, the buttons
            UiKit.Hand(jnav, 1, GUIDirection.Left, 0); UiKit.Hand(jnav, 1, GUIDirection.Down, 2);   // the rows -> the list, the buttons
            UiKit.Hand(jnav, 2, GUIDirection.Up, 1); UiKit.Hand(jnav, 2, GUIDirection.Left, 0);     // the buttons -> the rows, the list
            UiKit.Hand(jnav, 1, GUIDirection.Up, 0);
            UiKit.Rect(drive, stageRight, buttonsY, callW, ButtonH);

            StepTrace.Mark("build: the buttons");
            pictureOff = false;
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Catalog.MountsChanged -= OnRegistryChanged;
            Mounts.Looks.SkinsChanged += OnRegistryChanged;
            Mounts.Catalog.MountsChanged += OnRegistryChanged;
            panel.SetActive(true); // before the list: a layout under an inactive root computes nothing, so the scroll to the chosen mount was lost
            StepTrace.Mark("build: switched on");
            BuildList();
            StepTrace.Mark("build: the list's cards");
            BuildRight();
            StepTrace.Mark("build: the right side (the chosen mount's rows)");
            UiKit.FitToScreen(panel, W, H);
            StepTrace.Mark("build: fitted to the screen");
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
                GameObject go = ui.OwnButton(listContent, "card " + id, e.Label, ListW - 8f - ScrollbarW, CardH, () => { if (!fake) { Select(id); Sounds.Audition(page?.MountPaper); } }, textLeft: IconPx + 14f, flat: true, sound: UiSounds.Kind.Cell);
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
            StepTrace.Begin("a mount picked in the journal"); // dev only
            try
            {
                using (Perf.Measure(Perf.S.Pick))
                {
                    page.SelectMount(id);
                    nextPicture = 0f; // the picture follows the pick at once (its view may change: cart or alone)
                    StepTrace.Mark("the choice applied (the Mount row set)");
                    using (Perf.Measure(Perf.S.PickRows))
                    {
                        BuildRight();
                        MarkList();
                    }
                }
            }
            finally
            {
                StepTrace.End();
            }
        }

        // The colours window's title: the mount's name, and the skin's after it when the chosen skin isn't the mount's
        // first ("Colours: Donkey - Blaze": the window's rows are that skin's, 0.41.0).
        private static string ColoursTitle()
        {
            string chosen = page?.MountChosen ?? MountCatalog.DonkeyId;
            string skin = page?.SkinChosen(chosen) ?? "";
            string title = UiWords.ColoursTitle.Format(headerName.text);
            if (skin.Length == 0 || skin.Equals(Looks.FirstSkinOf(chosen), StringComparison.OrdinalIgnoreCase)) return title;
            Looks.SkinDef sd = Looks.Find(skin);
            return UiWords.ColoursTitleSkin.Format(title, sd != null && !string.IsNullOrEmpty(sd.Name) ? Looks.NameTerm(sd).Text : Mounts.Looks.Find(skin)?.Name ?? NameFromId(skin));
        }

        // The right side for the chosen mount: the header, the stats card, the look rows; the colours window's groups
        // gathered too (built when it opens).
        private static void BuildRight()
        {
            string chosen = page.MountChosen ?? MountCatalog.DonkeyId;
            builtFor = chosen;
            builtForSkin = page.SkinChosen(chosen);
            builtForMissing = page.SkinNotInstalled(chosen);
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
            // its chosen skin not installed (its pack gone): the last line says so in place of the skins' count; the Skin
            // row shows the skin by its name alone, dimmed (the user's pick, 2026-11-07: "<name> (не установлено)" didn't
            // fit the row; both words already in every language; docs/missing-skin.md section 9)
            if (k != null && statLines.Count > 0 && page.SkinNotInstalled(chosen) != null)
            {
                statLines[statLines.Count - 1].Label.text = UiWords.SkinRow.Text;
                statLines[statLines.Count - 1].Value.text = UiWords.NotInstalled.Text;
            }
            StepTrace.Mark("right: header, picture, stats");
            ShowAbilities(e, k);
            StepTrace.Mark("right: abilities");
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
                headerName.text = k != null ? Catalog.NameTerm(k).Text : UiWords.NameNotInstalled.Format(NameFromId(chosen));
                headerDesc.text = k == null ? UiWords.GoneNote.Text
                    : why != null ? UiWords.AsideNote.Text
                    : UiWords.NoSkinNote.Text;
            }

            StepTrace.Mark("right: name and description");
            // the page's look rows for this mount, in groups: the journal takes the Mount section's rows (the Skin); the
            // colours window takes every other look section (the colours, under its two headings)
            journalGroups.Clear();
            colourGroups.Clear();
            groupShown.Clear();
            string heading = null, headingSection = null, headingShown = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook) continue;
                if (r.RowKind == MountSettingsPage.RowKind.Note)
                {
                    heading = r.Label;
                    headingSection = r.ShownSection;
                    headingShown = Texts.Label(r);
                    continue;
                }
                if (r.Shown != null && !r.Shown()) continue; // another mount's row
                if (page.IsMountRow(r)) continue; // the list is the picker
                bool mountSection = string.Equals(r.ShownSection, "Mount", StringComparison.OrdinalIgnoreCase);
                bool underHeading = heading != null && r.ShownSection == headingSection;
                string group = underHeading ? heading : (mountSection ? "Skin" : r.ShownSection);
                // the group stays its English (its key: rows are gathered by it); its heading is drawn in the player's language
                if (group != null && !groupShown.ContainsKey(group)) groupShown[group] = underHeading ? headingShown : mountSection ? UiWords.SkinRow.Text : Texts.Section(page, r.ShownSection);
                if (mountSection) AddTo(journalGroups, null, r);  // the journal: the Skin, no heading (the user, 2026-10-25)
                else AddTo(colourGroups, group, r);               // the colours window: every colour row
            }
            if (StepTrace.On) StepTrace.Mark("right: the page's " + page.Rows.Count + " rows sorted into groups");
            foreach (var g in journalGroups) JournalOrder(g.Rows, chosen); // the name, the Skin, then the rest: the same on every mount
            journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, journalW - ScrollbarW, rows, out _);
            if (StepTrace.On) StepTrace.Mark("right: the journal's " + rows.Count + " rows built");
            MakeColourTabs();
            StepTrace.Mark("right: the colour tabs");
            if (colours != null)
            {
                coloursTitle.text = ColoursTitle();
                BuildColourTabStrip();
                BuildColourTab();
                StepTrace.Mark("right: the colours window's rows");
            }
            Refresh();
            StepTrace.Mark("right: refreshed (every row's value)");
        }

        // The colours window's tabs from its groups (colourTabs): a group's own rows a tab named by its heading; its skin's
        // parts, if any, a tab after it named by the skin. The chosen tab stays if it is still there (the parts' tab from
        // one skin to the next), else the first.
        private static void MakeColourTabs()
        {
            colourTabs.Clear();
            foreach (var g in colourGroups)
            {
                var own = new List<MountSettingsPage.Row>();
                var parts = new List<MountSettingsPage.Row>();
                foreach (MountSettingsPage.Row r in g.Rows) (r.SkinPart ? parts : own).Add(r);
                string name = g.Group != null && groupShown.TryGetValue(g.Group, out string shown) ? shown : g.Group ?? "";
                if (own.Count > 0) colourTabs.Add((g.Group ?? "", name, own));
                if (parts.Count > 0)
                {
                    Looks.SkinDef sd = Looks.Find(parts[0].SkinId);
                    colourTabs.Add((PartsTab, sd != null ? Looks.NameTerm(sd).Text : parts[0].SkinId ?? "", parts));
                }
            }
            if (colourTab == null || colourTabs.FindIndex(t => t.Key == colourTab) < 0) colourTab = colourTabs.Count > 0 ? colourTabs[0].Key : null;
        }

        // The chosen tab's rows (none: no tabs).
        private static List<MountSettingsPage.Row> ColourTabRows()
        {
            int i = colourTabs.FindIndex(t => t.Key == colourTab);
            return i >= 0 ? colourTabs[i].Rows : null;
        }

        // A tab's rows in two columns, the order kept (down the left, then down the right), split where the two come out
        // nearest in height: a choice with its custom colour's swatches and the lightness row after them stays whole, in
        // one column. Heights by each unit's first row as the mount and skin show it, never by a value (below). One
        // column for one such unit. A unit
        // starts at every row but a swatch row and the slider right after one: not by Visible, which every row a page
        // keeps per skin has (its skin shown), so the whole tab was one unit in one column (the user's screenshot,
        // 2026-11-08).
        private static List<(string Group, List<MountSettingsPage.Row> Rows)> TabColumns(List<MountSettingsPage.Row> tabRows)
        {
            var units = new List<List<MountSettingsPage.Row>>();
            var weights = new List<int>();
            MountSettingsPage.Row prev = null;
            foreach (MountSettingsPage.Row r in tabRows)
            {
                bool follows = IsPalette(r) || (r.RowKind == MountSettingsPage.RowKind.Steps && prev != null && IsPalette(prev));
                prev = r;
                if (!follows || units.Count == 0)
                {
                    units.Add(new List<MountSettingsPage.Row>());
                    weights.Add(0);
                }
                units[units.Count - 1].Add(r);
                // weighed by its first row alone, by what the mount and skin show (the user, 2026-11-10: a row went from the
                // left column to the right when another was set to Custom): the swatches and lightness a choice opens weigh
                // nothing, so no value moves the split; the column that opens them grows instead
                if (units[units.Count - 1].Count == 1 && (IsPalette(r) ? r.Shown == null || r.Shown() : r.Visible == null || r.Visible()))
                {
                    weights[weights.Count - 1] += IsPalette(r) ? 2 : 1;
                }
            }
            var cols = new List<(string Group, List<MountSettingsPage.Row> Rows)>();
            if (units.Count == 0) return cols;
            int total = 0;
            foreach (int w in weights) total += w;
            int split = units.Count, best = int.MaxValue, left = 0;
            for (int k = 1; k < units.Count; k++)
            {
                left += weights[k - 1];
                int worst = Math.Max(left, total - left);
                if (worst < best || (worst == best && left >= total - left)) { best = worst; split = k; } // a tie: the longer side on the left
            }
            var a = new List<MountSettingsPage.Row>();
            var b = new List<MountSettingsPage.Row>();
            for (int k = 0; k < units.Count; k++) (k < split ? a : b).AddRange(units[k]);
            cols.Add((null, a));
            if (b.Count > 0) cols.Add((null, b));
            return cols;
        }

        // The chosen tab's rows built into the colours window (its signature kept: a preset showing a row builds it again).
        private static void BuildColourTab()
        {
            if (colours == null) return;
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (HasColourParts(tabRows))
            {
                BuildPartsTab(tabRows); // its parts on the left, one palette on the right (direction E)
                coloursSignature = ColourTabSignature();
                MarkColourTabs();
                return;
            }
            parts.Clear();
            partPanel = null;
            partPanelRefresh = null;
            var cols = tabRows != null ? TabColumns(tabRows) : new List<(string Group, List<MountSettingsPage.Row> Rows)>();
            coloursSignature = BuildRows(cols, coloursRoot, coloursScroll, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, colourRows, out _);
            MarkColourTabs();
        }

        // What the chosen tab shows now, as BuildRows writes it (each row shown or not, in order).
        private static string ColourTabSignature()
        {
            var sig = new StringBuilder();
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (tabRows == null) return "";
            bool partsTab = HasColourParts(tabRows);
            for (int i = 0; i < tabRows.Count; i++)
            {
                MountSettingsPage.Row r = tabRows[i];
                sig.Append(r.Visible == null || r.Visible() ? '1' : '0');
                // a colour part's own rows (its custom colour, its lightness) are always drawn: no pick rebuilds the tab
                if (!partsTab || i + 1 >= tabRows.Count || !IsColourPart(r, tabRows[i + 1], out _)) continue;
                i++;
                if (i + 1 < tabRows.Count && tabRows[i + 1].RowKind == MountSettingsPage.RowKind.Steps) i++;
            }
            return sig.ToString();
        }

        // The strip of tabs over the colours rows, as the options window's section cards (flat, the chosen one in the
        // game's red, its name bold), each as wide as its name; none for a single tab. Built again when the tabs change.
        private static void BuildColourTabStrip()
        {
            if (coloursTabs == null) return;
            var sig = new StringBuilder();
            foreach (var t in colourTabs) sig.Append(t.Key).Append('\u0002').Append(t.Name).Append('\u0003');
            if (tabsBuilt == sig.ToString()) return;
            tabsBuilt = sig.ToString();
            foreach (Transform c in coloursTabs) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            tabButtons.Clear();
            coloursTabs.gameObject.SetActive(colourTabs.Count > 1);
            if (colourTabs.Count < 2) return;
            float x = 0f;
            foreach (var t in colourTabs)
            {
                string key = t.Key;
                GameObject go = ui.OwnButton(coloursTabs, "tab " + t.Name, t.Name, TabMaxW, TabH, () => ShowColourTab(key), flat: true, sound: UiSounds.Kind.Tab);
                TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
                float w = Mathf.Clamp(Mathf.Ceil(label.GetPreferredValues(t.Name).x) + 36f, TabMinW, TabMaxW);
                UiKit.Rect(go, x, 0f, w, TabH);
                UiKit.Rect(label.gameObject, 3f, 0f, w - 6f, TabH);
                UiKit.NavGroup(go, 2);
                tabButtons.Add((key, go.GetComponent<Image>(), label));
                x += w + TabGap;
            }
            controls.NavDirty = true;
        }

        // The chosen tab marked as the options window marks its section.
        private static void MarkColourTabs()
        {
            foreach ((string key, Image face, TextMeshProUGUI label) in tabButtons)
            {
                bool on = key == colourTab;
                if (face != null) face.color = on ? UiKit.CardChosen : UiKit.CardBase;
                if (label != null)
                {
                    label.color = on ? ui.ButtonTextColor : ui.FontColor;
                    label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private static void ShowColourTab(string key)
        {
            if (colours == null || key == null || key == colourTab) return;
            colourTab = key;
            BuildColourTab();
            if (coloursScroll != null) coloursScroll.verticalNormalizedPosition = 1f;
            Refresh();
            nextPicture = 0f; // the picture follows the tab at once (with the cart or the mount alone)
        }

        // The picture with the cart, or the mount alone (the user, 2026-11-10: "only show cart in preview when the cart tab
        // is selected ... its image is larger and you can see it better without the cart"): the colours window shows the
        // cart only on a tab with a row the page marked as the cart's (CartRow); the journal always. A page that marks no
        // row of its tabs as the cart's keeps the cart on every tab, as before.
        private static bool ColoursWantCart()
        {
            if (colours == null) return true;
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (tabRows == null) return true;
            if (HasCartRow(tabRows)) return true;
            for (int i = 0; i < colourTabs.Count; i++) if (HasCartRow(colourTabs[i].Rows)) return false;
            return true;
        }

        private static bool HasCartRow(List<MountSettingsPage.Row> rows)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].CartPart) return true;
            return false;
        }

        // The picture on screen: the colours window's while it is open (the journal waits hidden under it).
        private static string coloursLook; // the look the colours window's photo shows (null: none yet)

        private static RawImage ShownPicture() => colours != null && coloursPicture != null ? coloursPicture : picture;

        internal static bool TestColourTabHasCart => ColoursWantCart();
        // the colours picture's drawn size per art pixel, and the most the stage has room for (the self-tests)
        internal static (float Scale, float Room, float Px) TestColoursScale()
        {
            if (coloursPicture == null || coloursPicture.texture == null || coloursStage == null) return (0f, 0f, 0f);
            Vector2 art = PreviewStage.ArtSize(coloursPicture.texture);
            float room = Mathf.Min((coloursStage.rect.width - 16f) / Mathf.Max(1f, art.x), (coloursStage.rect.height - 16f) / Mathf.Max(1f, art.y));
            return (((RectTransform)coloursPicture.transform).sizeDelta.x / Mathf.Max(1f, art.x), room, ScreenPixelsPerUnit(coloursStage));
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
            controls.CommitFields(); // a name being typed is written to its own row before the rows are pointed at others
            // the rows built last time are kept by shape and pointed at this time's rows of that shape (a pick: the same 8
            // rows for the next mount; 2.4-3.8 ms and 300-360 KB when they were made again, docs/performance.md); every
            // other part (the headings, their lines, the swatch rows) goes and is made again
            spareRows.Clear();
            foreach (var e in into)
            {
                if (e.Go != null && rowBound.TryGetValue(e.Go, out RowControls.Bound eb) && eb.Shape != null) spareRows.Add((e.Go, eb));
            }
            foreach (Transform c in root)
            {
                if (SpareIndex(c.gameObject) >= 0) continue;
                rowBound.Remove(c.gameObject);
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
            into.Clear();
            int reused = 0, made = 0;
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
                        TextMeshProUGUI h = ui.Text(root, "heading " + group.Group, groupShown.TryGetValue(group.Group, out string shownName) ? shownName : group.Group, 1f, TextAlignmentOptions.BottomLeft, false, cx, cy, colW, step - 6f);
                        ui.Bold(h);
                        GameObject line = ui.Node("heading line", root, typeof(Image));
                        UiKit.Rect(line, cx, cy + step - 4f, colW, 1f);
                        line.GetComponent<Image>().color = UiKit.EdgeColor;
                        line.GetComponent<Image>().raycastTarget = false;
                        cy += step + 2f;
                    }
                    for (int ri = 0; ri < group.Rows.Count; ri++)
                    {
                        MountSettingsPage.Row r = group.Rows[ri];
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
                        else if (TakeSpare(r, lay, out go, out RowControls.Bound sb))
                        {
                            refresh = sb.Refresh; // last time's row of this shape, now showing this row
                            UiKit.Place(go, cx, cy);
                            cy += sb.Height + 4f;
                            reused++;
                        }
                        else if ((go = controls.OwnRow(root, r, lay, out refresh, out float h, out RowControls.Bound nb)) != null)
                        {
                            UiKit.Place(go, cx, cy); // the panel's own controls (RowControls)
                            cy += h + 4f;
                            if (nb != null) rowBound[go] = nb;
                            made++;
                        }
                        else
                        {
                            Plugin.Log.LogInfo($"Settings: {page.Title}'s row '{r.Label}' has nothing to choose from; left out of the Mounts panel.");
                            continue; // a list with no choices (every control is our own: no game row to fall back on)
                        }
                        CanvasGroup grp = null;
                        if (r.Enabled != null)
                        {
                            grp = go.GetComponent<CanvasGroup>(); // a row kept: its own (the shape says it has one)
                            if (grp == null) grp = go.AddComponent<CanvasGroup>();
                        }
                        into.Add((r, go, refresh, grp));
                    }
                    tallest = Math.Max(tallest, cy - rowTop);
                }
                y = rowTop + tallest + 8f;
            }
            // last time's rows no row of this time's took
            foreach ((GameObject sg, RowControls.Bound _) in spareRows)
            {
                rowBound.Remove(sg);
                if (sg == null) continue;
                sg.SetActive(false);
                UnityEngine.Object.Destroy(sg);
            }
            spareRows.Clear();
            LastRowsReused = reused;
            LastRowsMade = made;
            needed = y;
            root.sizeDelta = new Vector2(0f, needed);
            scroll.verticalNormalizedPosition = 1f;
            int navGroup = root == contentRoot ? 1 : 0;
            foreach (GamepadNavigationItem ni in root.GetComponentsInChildren<GamepadNavigationItem>(true)) ni.group = navGroup;
            controls.NavDirty = true;
            return sig.ToString();
        }

        private static int SpareIndex(GameObject go)
        {
            for (int i = 0; i < spareRows.Count; i++) if (spareRows[i].Go == go) return i;
            return -1;
        }

        // Last build's row of r's shape, pointed at r (its value shown); false: none, or r can't be built.
        private static bool TakeSpare(MountSettingsPage.Row r, RowLayout lay, out GameObject go, out RowControls.Bound b)
        {
            go = null;
            b = null;
            if (spareRows.Count == 0) return false;
            string shape = RowControls.Shape(r, lay);
            if (shape == null) return false;
            for (int i = 0; i < spareRows.Count; i++)
            {
                if (!string.Equals(spareRows[i].B.Shape, shape, StringComparison.Ordinal)) continue;
                if (spareRows[i].Go == null || !controls.Rebind(spareRows[i].B, r)) return false;
                go = spareRows[i].Go;
                b = spareRows[i].B;
                spareRows.RemoveAt(i);
                go.name = "row " + (r.Entry != null ? r.Entry.Definition.Key : r.Label ?? ""); // as OwnRow names it
                return true;
            }
            return false;
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
            string labelText = Texts.Label(r) ?? ""; // in the player's language (Texts)
            TextMeshProUGUI label = ui.Text(go.transform, "label", labelText, 0.95f, TextAlignmentOptions.Left, true, lay.LabelX, 0f, labelW, 30f);
            height = Mathf.Max(2f * px + SwatchGap + 6f, Mathf.Ceil(label.GetPreferredValues(labelText, labelW, 0f).y) + 8f);
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
                UiSounds.Add(sw, UiSounds.Kind.Cell); // a palette square: as the game's item cells
            }
            refresh = () =>
            {
                PaletteColor now = entry.Value;
                foreach ((PaletteColor colour, GameObject frame) in frames) if (frame != null) frame.SetActive(colour == now);
            };
            refresh();
            return go;
        }

        // A colour choice drawn as swatches (ColourPart): a choice whose next row is its custom colour (a PaletteColor row)
        // and with a value named Custom; the first value is the choice's default (Game default: as the look draws it).
        // Anything else stays a dropdown (another mod's page made otherwise).
        private static bool IsColourPart(MountSettingsPage.Row r, MountSettingsPage.Row next, out object customValue)
        {
            customValue = null;
            if (r == null || r.RowKind != MountSettingsPage.RowKind.Choice || r.Entry == null || r.ChoiceValues == null || r.ChoiceValues.Length < 2 || !IsPalette(next)) return false;
            foreach (object v in r.ChoiceValues)
            {
                if (v != null && v.ToString() == "Custom") { customValue = v; return true; }
            }
            return false;
        }

        // The named colour each colour choice had when the colours window first showed it (null: none, Game default or
        // Custom), kept in a slot of its own until the window closes, so a player can go back to it (the user, 2026-11-10:
        // the swatches can't make Dark, Black or Dark wood, which darken the drawn art; a player's stays theirs).
        private static readonly Dictionary<BepInEx.Configuration.ConfigEntryBase, object> keptNamed = new Dictionary<BepInEx.Configuration.ConfigEntryBase, object>();

        // ------------------------------------------------------------------ the colours window's parts (direction E)

        // A tab with colour parts (the user, 2026-11-10: direction E of the board "Colours window directions", from the game's
        // own Customization window and zombie screen): on the left the tab's parts, each a row with the game's ◀ ▶ arrows
        // around a field in its colour (named while it is Original or a kept named colour), the tab's other rows under
        // them; on the right one palette for the chosen part: Original and the kept named colour as wide slots, the 24
        // colours as slots, the chosen one in the game's yellow brackets, and the part's lightness, always there, greyed
        // while the part isn't a colour of the palette (the user: "i prefer disabling the control instead of hiding it
        // because it moves stuff up and down"). Nothing moves on a pick: a part's own rows don't count in the tab's
        // signature (ColourTabSignature). Nothing is written by drawing or refreshing; the values stay as saved.
        private sealed class PartView
        {
            internal MountSettingsPage.Row Choice, Pal, Light;
            internal object Custom, Default, Kept;
            internal string KeptName, KeptHex, OrigHex;
            internal GameObject Go;
            internal Image Bg, Field, PrevIcon, NextIcon;
            internal TextMeshProUGUI FieldText;
            internal UnityEngine.UI.Button Prev, Next;
            internal float Height;
        }

        private static readonly List<PartView> parts = new List<PartView>();
        private static string partSel;              // the chosen part (its choice's section/key); kept while it is on the tab
        private static RectTransform partPanel;     // the right column: the chosen part's palette, made again on another part
        private static float partPanelX, partColW;
        private static Action partPanelRefresh;
        private const float PartRowH = 32f, SlotPx = 34f, SlotGap = 8f, ArrowW = 24f, FieldH = 24f, FieldW = 108f, PartListW = 330f, PaletteSlot = 38f;
        private static readonly Color SlotOuter = new Color(0.23f, 0.25f, 0.30f, 1f), SlotInner = new Color(0.15f, 0.16f, 0.21f, 1f);
        private static readonly Color BracketColour = new Color(0.886f, 0.8f, 0.27f, 1f), ArrowColour = new Color(0.95f, 0.76f, 0.32f, 1f), ArrowOff = new Color(0.38f, 0.39f, 0.43f, 1f);
        private static readonly Color ArrowFace = new Color(0.235f, 0.255f, 0.306f, 1f), ArrowShade = new Color(0.165f, 0.18f, 0.22f, 1f);
        private static readonly Color LanternOriginal = new Color(0.95f, 0.71f, 0.36f, 1f); // the lamp's warm light, as drawn

        private static bool HasColourParts(List<MountSettingsPage.Row> rows)
        {
            if (rows == null) return false;
            for (int i = 0; i + 1 < rows.Count; i++) if (IsColourPart(rows[i], rows[i + 1], out _)) return true;
            return false;
        }

        private static string PartKey(MountSettingsPage.Row choice) => choice.Entry.Definition.Section + "/" + choice.Entry.Definition.Key;

        // The colour a part is drawn in (its "Original"): its region's or its skin part's art, as the lightness rule reads
        // it; for the page's own colour kinds without a mark, the region the kind colours.
        private static string OriginalHex(MountSettingsPage.Row choice)
        {
            try
            {
                string skin = choice.SkinId;
                if (choice.ArtTone != null)
                {
                    Looks.SkinDef d = Looks.Find(skin);
                    List<string> hexes = d != null ? SkinTones.ColoursOf(d, choice.ArtTone) : null;
                    return hexes != null && hexes.Count > 0 ? Colors.SampleOf(Colors.ColoursOfHexes(hexes)) : null;
                }
                string region = choice.ArtRegion;
                if (region == null)
                {
                    Type kind = choice.Entry.SettingType;
                    region = kind == typeof(CoatColor) ? "Coat" : kind == typeof(HarnessColor) ? "Harness" : kind == typeof(MuzzleColor) ? "Muzzle"
                        : kind == typeof(CartPaint) ? "CartBody" : kind == typeof(WheelColor) ? "Wheels" : kind == typeof(HubColor) ? "Hubs" : null;
                    if (kind == typeof(LanternColor)) return ColorUtility.ToHtmlStringRGB(LanternOriginal);
                }
                return region != null ? Colors.ArtSampleHex(region, skin) : null;
            }
            catch
            {
                return null; // no sample: the field and the square in the slots' own grey
            }
        }

        // A part's own colour as a named choice that darkens or fades the art leaves it (Dark, Black, Dark wood: the
        // preset's Brightness and Saturation); null for one that paints a colour.
        private static string Shaded(string hex, object named)
        {
            if (hex == null || !MountCompanion.PresetShade(named, out float b, out float s)) return null;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color c)) return null;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            return ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, Mathf.Clamp01(sat * s), Mathf.Clamp01(v * b)));
        }

        // The tab's rows: its parts on the left (their arrows), its other rows under them, the chosen part's palette on
        // the right.
        private static void BuildPartsTab(List<MountSettingsPage.Row> tabRows)
        {
            controls.CloseDropdown(refocus: false);
            controls.CommitFields();
            foreach (Transform c in coloursRoot)
            {
                rowBound.Remove(c.gameObject);
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
            colourRows.Clear();
            parts.Clear();
            partPanel = null;
            partPanelRefresh = null;
            float areaW = CW - 2f * Pad - ScrollbarW - ColoursScrollGap, colGap = 16f;
            // the board's proportions (direction E): the parts' list narrower, the palette wider
            float colW = Mathf.Min(PartListW, Mathf.Floor((areaW - colGap) / 2f)), x0 = 0f;
            partColW = areaW - colW - colGap;
            partPanelX = colW + colGap;
            var lay = new RowLayout { LabelX = 0f, CtrlLeft = ColoursLabelW + 10f, CtrlRight = colW, ColW = colW };
            float y = 4f;
            for (int i = 0; i < tabRows.Count; i++)
            {
                MountSettingsPage.Row r = tabRows[i];
                MountSettingsPage.Row next = i + 1 < tabRows.Count ? tabRows[i + 1] : null;
                if (IsColourPart(r, next, out object custom))
                {
                    MountSettingsPage.Row light = i + 2 < tabRows.Count && tabRows[i + 2].RowKind == MountSettingsPage.RowKind.Steps ? tabRows[i + 2] : null;
                    i += light != null ? 2 : 1;
                    if (r.Visible != null && !r.Visible()) continue; // another skin's, a cart part of a mount without one
                    PartView p = PartRow(r, next, light, custom, lay);
                    UiKit.Place(p.Go, x0, y);
                    y += p.Height + 4f;
                    CanvasGroup cg = r.Enabled != null ? p.Go.AddComponent<CanvasGroup>() : null; // a skin without the region: greyed
                    colourRows.Add((r, p.Go, () => RefreshPart(p), cg));
                    colourRows.Add((next, p.Go, null, null));
                    parts.Add(p);
                    continue;
                }
                if (r.Visible != null && !r.Visible()) continue;
                GameObject go;
                Action refresh;
                float h;
                if (IsPalette(r)) go = SwatchRow(coloursRoot, r, lay, out refresh, out h); // a custom colour of no choice
                else if ((go = controls.OwnRow(coloursRoot, r, lay, out refresh, out h)) == null) continue;
                if (y > 4f && parts.Count > 0 && colourRows.Count == 2 * parts.Count) y += 4f; // a little room between the parts and the rest
                UiKit.Place(go, x0, y);
                y += h + 4f;
                CanvasGroup grp = null;
                if (r.Enabled != null)
                {
                    grp = go.GetComponent<CanvasGroup>();
                    if (grp == null) grp = go.AddComponent<CanvasGroup>();
                }
                colourRows.Add((r, go, refresh, grp));
            }
            if (parts.Count > 0 && FindPart(partSel) == null) partSel = PartKey(parts[0].Choice);
            float panelH = BuildPartPanel();
            coloursRoot.sizeDelta = new Vector2(0f, Math.Max(y, 4f + panelH) + 2f);
            coloursScroll.verticalNormalizedPosition = 1f;
            foreach (PartView p in parts) RefreshPart(p);
            NavColours();
        }

        private static PartView FindPart(string key)
        {
            if (key == null) return null;
            foreach (PartView p in parts) if (PartKey(p.Choice) == key) return p;
            return null;
        }

        private static void NavColours()
        {
            if (coloursRoot == null) return;
            foreach (GamepadNavigationItem ni in coloursRoot.GetComponentsInChildren<GamepadNavigationItem>(true)) ni.group = 0;
            controls.NavDirty = true;
        }

        // A part's row: the row itself a button (it chooses the part), its label, and on the controls' span the arrows
        // around the field in its colour.
        private static PartView PartRow(MountSettingsPage.Row choice, MountSettingsPage.Row pal, MountSettingsPage.Row light, object custom, RowLayout lay)
        {
            var p = new PartView { Choice = choice, Pal = pal, Light = light, Custom = custom, Default = choice.ChoiceValues.GetValue(0) };
            if (!keptNamed.TryGetValue(choice.Entry, out object kept))
            {
                object now = choice.Entry.BoxedValue;
                kept = Equals(now, p.Default) || Equals(now, custom) ? null : now;
                keptNamed[choice.Entry] = kept;
            }
            p.Kept = kept;
            if (kept != null)
            {
                int ki = Array.IndexOf(choice.ChoiceValues, kept);
                p.KeptName = ki >= 0 ? Texts.Choice(choice, ki) : kept.ToString();
            }
            p.OrigHex = OriginalHex(choice);
            if (kept != null) p.KeptHex = MountCompanion.PresetSwatchHex(kept) ?? Shaded(p.OrigHex, kept);
            // the board's row: the label on the left (two lines when long), the stepper on the right, compact
            float stepperW = 2f * ArrowW + 8f + FieldW, stepperX = lay.ColW - 6f - stepperW;
            float labelW = Mathf.Max(40f, stepperX - 10f - 10f);
            GameObject go = ui.Node("colour " + choice.Entry.Definition.Key, coloursRoot, typeof(Image), typeof(UnityEngine.UI.Button), typeof(GamepadNavigationItem));
            string labelText = Texts.Label(choice) ?? "";
            TextMeshProUGUI label = ui.Text(go.transform, "label", labelText, 0.95f, TextAlignmentOptions.Left, true, 10f, 0f, labelW, PartRowH);
            p.Height = Mathf.Max(PartRowH, Mathf.Ceil(label.GetPreferredValues(labelText, labelW, 0f).y) + 10f);
            UiKit.Rect(go, 0f, 0f, lay.ColW, p.Height);
            UiKit.Rect(label.gameObject, 10f, 0f, labelW, p.Height);
            p.Go = go;
            p.Bg = go.GetComponent<Image>();
            p.Bg.color = UiKit.CardBase;
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = p.Bg;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.05f;
            b.colors = cb;
            b.onClick.AddListener(() => ChoosePart(p));
            FocusFrame(go, lay.ColW, p.Height);
            go.GetComponent<GamepadNavigationItem>().SetCallbacks(null, null, () => ChoosePart(p));
            UiSounds.Add(go, UiSounds.Kind.Cell); // a part's row: a list's cell
            float top = Mathf.Round((p.Height - FieldH) / 2f);
            p.Prev = Arrow(go.transform, "arrow prev", stepperX, top, left: true, () => Step(p, -1), out p.PrevIcon);
            // the field: a dark frame, the colour inside it, its name over it
            GameObject frame = ui.Node("field", go.transform, typeof(Image));
            UiKit.Rect(frame, stepperX + ArrowW + 4f, top, FieldW, FieldH);
            frame.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            frame.GetComponent<Image>().raycastTarget = false;
            GameObject fill = ui.Node("colour", frame.transform, typeof(Image));
            UiKit.Rect(fill, 1f, 1f, FieldW - 2f, FieldH - 2f);
            p.Field = fill.GetComponent<Image>();
            p.Field.raycastTarget = false;
            GameObject sheen = ui.Node("sheen", fill.transform, typeof(Image));
            UiKit.Rect(sheen, 0f, 0f, FieldW - 2f, 1f);
            sheen.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
            sheen.GetComponent<Image>().raycastTarget = false;
            p.FieldText = ui.Text(frame.transform, "value", "", 0.85f, TextAlignmentOptions.Center, false, 2f, 0f, FieldW - 4f, FieldH);
            p.FieldText.enableAutoSizing = true; // a long name in another language: smaller, never cut
            p.FieldText.fontSizeMax = p.FieldText.fontSize;
            p.FieldText.fontSizeMin = Mathf.Max(8f, p.FieldText.fontSize * 0.6f);
            p.Next = Arrow(go.transform, "arrow next", stepperX + ArrowW + 4f + FieldW + 4f, top, left: false, () => Step(p, 1), out p.NextIcon);
            return p;
        }

        // The gamepad's focus frame on a control (gold, as the swatches' and the rows').
        private static void FocusFrame(GameObject go, float w, float h)
        {
            GameObject f = ui.Node("focus", go.transform, typeof(Image), typeof(Outline));
            UiKit.Rect(f, -3f, -3f, w + 6f, h + 6f);
            f.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            f.GetComponent<Image>().raycastTarget = false;
            var o = f.GetComponent<Outline>();
            o.effectColor = new Color(1f, 0.85f, 0.3f, 1f);
            o.effectDistance = new Vector2(2f, -2f);
            f.SetActive(false);
            go.GetComponent<GamepadNavigationItem>().focusFrame = f;
        }

        // One of the game's square arrow buttons (its Customization window's): a face with a darker foot, the arrow a
        // pixel triangle; greyed at the end of the range (never hidden).
        private static UnityEngine.UI.Button Arrow(Transform parent, string name, float x, float y, bool left, Action click, out Image icon)
        {
            GameObject go = ui.Node(name, parent, typeof(Image), typeof(UnityEngine.UI.Button), typeof(GamepadNavigationItem));
            UiKit.Rect(go, x, y, ArrowW, FieldH);
            Image face = go.GetComponent<Image>();
            face.color = ArrowFace;
            Outline edge = go.AddComponent<Outline>();
            edge.effectColor = new Color(0.36f, 0.38f, 0.45f, 1f);
            edge.effectDistance = new Vector2(1f, -1f);
            GameObject shade = ui.Node("foot", go.transform, typeof(Image));
            UiKit.Rect(shade, 0f, FieldH - 3f, ArrowW, 3f);
            shade.GetComponent<Image>().color = ArrowShade;
            shade.GetComponent<Image>().raycastTarget = false;
            GameObject tri = ui.Node("arrow", go.transform, typeof(Image));
            // mirrored for the left one around its left edge (the pivot): placed one width further right, so both sit
            // in the middle of their buttons (the user's screenshot, 2026-11-10: the left arrows off to the left)
            UiKit.Rect(tri, Mathf.Round((ArrowW - 6f) / 2f) + (left ? 6f : 0f), Mathf.Round((FieldH - 3f - 11f) / 2f), 6f, 11f);
            icon = tri.GetComponent<Image>();
            icon.sprite = ArrowSprite();
            icon.color = ArrowColour;
            icon.raycastTarget = false;
            if (left) tri.transform.localScale = new Vector3(-1f, 1f, 1f);
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = face;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.05f;
            b.colors = cb;
            b.onClick.AddListener(() => click());
            FocusFrame(go, ArrowW, FieldH);
            go.GetComponent<GamepadNavigationItem>().SetCallbacks(null, null, () => { if (b.interactable) click(); });
            UiSounds.Add(go, UiSounds.Kind.Cell); // a palette slot: as the game's item cells
            return b;
        }

        private static Sprite arrowSprite;

        // A right-pointing triangle five pixels wide and nine tall, point-filtered (the game's arrows are pixel art).
        private static Sprite ArrowSprite()
        {
            if (arrowSprite != null && arrowSprite) return arrowSprite;
            const int w = 5, h = 9;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "GK2DonkeyAPI arrow", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = x <= 4 - Mathf.Abs(y - 4) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 1f);
            return arrowSprite;
        }

        // A part's choices in the arrows' order: Original, its named colour from before while it still is that colour (the
        // user, 2026-11-10: an old "Rose" beside Original read as one colour too many; once another is picked it is gone,
        // Cancel or Escape still bring it back), the 24 colours.
        private static bool KeptNow(PartView p) => p.Kept != null && Equals(p.Choice.Entry.BoxedValue, p.Kept);

        private static int PartIndex(PartView p, out int count)
        {
            int named = KeptNow(p) ? 1 : 0;
            count = 1 + named + Enum.GetValues(typeof(PaletteColor)).Length;
            object now = p.Choice.Entry.BoxedValue;
            if (Equals(now, p.Custom)) return 1 + named + Array.IndexOf(Enum.GetValues(typeof(PaletteColor)), ((BepInEx.Configuration.ConfigEntry<PaletteColor>)p.Pal.Entry).Value);
            if (p.Kept != null && Equals(now, p.Kept)) return 1;
            return 0; // Original (or a named colour set elsewhere while the window is open: from the start)
        }

        private static void Step(PartView p, int by)
        {
            int named = KeptNow(p) ? 1 : 0;
            int at = PartIndex(p, out int count), to = Mathf.Clamp(at + by, 0, count - 1);
            if (to == 0) SetPart(p, 0, default);
            else if (to == 1 && named == 1) SetPart(p, 2, default);
            else SetPart(p, 1, (PaletteColor)Enum.GetValues(typeof(PaletteColor)).GetValue(to - 1 - named));
        }

        // A part set to Original (0), a palette colour (1: the colour first, then the choice to Custom, which applies it)
        // or its kept named colour (2); it becomes the chosen part.
        private static void SetPart(PartView p, int kind, PaletteColor colour)
        {
            var colourEntry = (BepInEx.Configuration.ConfigEntry<PaletteColor>)p.Pal.Entry;
            if (kind == 1)
            {
                if (colourEntry.Value != colour) colourEntry.Value = colour;
                if (!Equals(p.Choice.Entry.BoxedValue, p.Custom)) p.Choice.Entry.BoxedValue = p.Custom;
            }
            else
            {
                object want = kind == 0 ? p.Default : p.Kept;
                if (want != null && !Equals(p.Choice.Entry.BoxedValue, want)) p.Choice.Entry.BoxedValue = want;
            }
            if (PartKey(p.Choice) != partSel) ChoosePart(p);
            Refresh();
        }

        private static void ChoosePart(PartView p)
        {
            if (p == null) return;
            string key = PartKey(p.Choice);
            if (key != partSel)
            {
                partSel = key;
                BuildPartPanel();
                NavColours();
            }
            foreach (PartView q in parts) RefreshPart(q);
            partPanelRefresh?.Invoke();
        }

        // A part's row as its value is now: the field's colour and name, the arrows at the ends greyed, the chosen one red.
        private static void RefreshPart(PartView p)
        {
            if (p.Go == null) return;
            object now = p.Choice.Entry.BoxedValue;
            string hex, name = null;
            if (Equals(now, p.Custom))
            {
                PaletteColor pc = ((BepInEx.Configuration.ConfigEntry<PaletteColor>)p.Pal.Entry).Value;
                hex = MountPalette.Hex(pc);
                int pi = Array.IndexOf(Enum.GetValues(typeof(PaletteColor)), pc);
                name = pi >= 0 && pi < UiWords.PaletteNames.Length ? UiWords.PaletteNames[pi].Text : pc.ToString();
            }
            else if (Equals(now, p.Default)) { hex = p.OrigHex; name = UiWords.ColourOriginal.Text; }
            else
            {
                int i = Array.IndexOf(p.Choice.ChoiceValues, now);
                name = i >= 0 ? Texts.Choice(p.Choice, i) : now?.ToString();
                hex = MountCompanion.PresetSwatchHex(now) ?? Shaded(p.OrigHex, now) ?? p.OrigHex;
            }
            Color c = SlotInner;
            if (hex != null) ColorUtility.TryParseHtmlString("#" + hex, out c);
            if (p.Field != null) p.Field.color = c;
            if (p.FieldText != null)
            {
                p.FieldText.text = name ?? "";
                float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                // light on every colour: the game's font draws its own dark outline, which keeps it readable on a light
                // field (dark letters ran into that outline, 2026-11-10)
                p.FieldText.color = new Color(0.96f, 0.93f, 0.86f, 1f);
            }
            int at = PartIndex(p, out int count);
            if (p.Prev != null) { p.Prev.interactable = at > 0; p.PrevIcon.color = at > 0 ? ArrowColour : ArrowOff; }
            if (p.Next != null) { p.Next.interactable = at < count - 1; p.NextIcon.color = at < count - 1 ? ArrowColour : ArrowOff; }
            if (p.Bg != null) p.Bg.color = PartKey(p.Choice) == partSel ? UiKit.CardChosen : UiKit.CardBase;
        }

        // The right column: the chosen part's palette. Returns its height.
        private static float BuildPartPanel()
        {
            if (partPanel != null)
            {
                partPanel.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(partPanel.gameObject);
            }
            partPanel = null;
            partPanelRefresh = null;
            PartView p = FindPart(partSel);
            if (p == null || coloursRoot == null) return 0f;
            GameObject host = ui.Node("palette", coloursRoot);
            UiKit.Rect(host, partPanelX, 4f, partColW, 10f);
            partPanel = (RectTransform)host.transform;
            var slots = new List<(int Kind, PaletteColor Colour, float X, float Y, float W, float H, Image Outer)>();
            GameObject keptSlot = null; // shown only while the part is still that colour (KeptNow)
            float y = 0f, x = 0f;
            // no part name over it (the user, 2026-11-10: the chosen row on the left says it): Original first
            Image outer = WideSlot(host.transform, "swatch default", p.OrigHex, UiWords.ColourOriginal.Text, x, y, () => SetPart(p, 0, default), out float w);
            slots.Add((0, default, x, y, w, SlotPx, outer));
            x += w + SlotGap;
            if (p.Kept != null)
            {
                outer = WideSlot(host.transform, "swatch kept", p.KeptHex, p.KeptName, x, y, () => SetPart(p, 2, default), out w);
                keptSlot = outer.gameObject;
                slots.Add((2, default, x, y, w, SlotPx, outer));
            }
            y += SlotPx + SlotGap + 2f;
            Array colours = Enum.GetValues(typeof(PaletteColor));
            const int perRow = 8; // the 24 in three even rows (the board: never a short row)
            float sp = Mathf.Min(PaletteSlot, Mathf.Floor((partColW - (perRow - 1) * SlotGap) / perRow));
            float gridW = perRow * sp + (perRow - 1) * SlotGap;
            for (int i = 0; i < colours.Length; i++)
            {
                var colour = (PaletteColor)colours.GetValue(i);
                float sx = (i % perRow) * (sp + SlotGap), sy = y + (i / perRow) * (sp + SlotGap);
                PaletteColor pick = colour;
                outer = Slot(host.transform, "swatch " + colour, MountPalette.Hex(colour), sx, sy, sp, sp, () => SetPart(p, 1, pick));
                slots.Add((1, colour, sx, sy, sp, sp, outer));
            }
            y += ((colours.Length + perRow - 1) / perRow) * (sp + SlotGap) + 2f;
            // the lightness: always there, greyed while the part is Original or a kept named colour
            GameObject lightGo = null;
            CanvasGroup lightGroup = null;
            Action lightRefresh = null;
            if (p.Light != null)
            {
                var lay = new RowLayout { LabelX = 0f, CtrlLeft = 110f, CtrlRight = gridW, ColW = gridW };
                lightGo = controls.OwnRow(host.transform, p.Light, lay, out lightRefresh, out float lh);
                if (lightGo != null)
                {
                    // the plain word (the board): the part's name is over the palette already
                    foreach (TextMeshProUGUI lt in lightGo.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (!lt.name.EndsWith("label", StringComparison.Ordinal)) continue;
                        lt.text = UiWords.ColourLightness.Text;
                        break;
                    }
                    UiKit.Place(lightGo, 0f, y);
                    y += lh;
                    lightGroup = lightGo.GetComponent<CanvasGroup>();
                    if (lightGroup == null) lightGroup = lightGo.AddComponent<CanvasGroup>();
                }
            }
            partPanel.sizeDelta = new Vector2(partColW, y);
            partPanelRefresh = () =>
            {
                if (host == null) return;
                object now = p.Choice.Entry.BoxedValue;
                bool isCustom = Equals(now, p.Custom);
                PaletteColor picked = ((BepInEx.Configuration.ConfigEntry<PaletteColor>)p.Pal.Entry).Value;
                // the chosen slot's frame in the game's yellow, nothing round it (the user, 2026-11-10: the corner brackets
                // too, "probably one is enough", and cut off at the palette's top)
                foreach (var s in slots)
                {
                    bool on = s.Kind == 0 ? Equals(now, p.Default) : s.Kind == 2 ? p.Kept != null && Equals(now, p.Kept) : isCustom && s.Colour == picked;
                    if (s.Outer != null) s.Outer.color = on ? BracketColour : SlotOuter;
                }
                if (keptSlot != null) keptSlot.SetActive(KeptNow(p));
                lightRefresh?.Invoke();
                if (lightGroup != null)
                {
                    lightGroup.alpha = isCustom ? 1f : 0.4f;
                    lightGroup.interactable = isCustom;
                    lightGroup.blocksRaycasts = isCustom;
                    foreach (GamepadNavigationItem n in lightGroup.GetComponentsInChildren<GamepadNavigationItem>(true)) n.enabled = isCustom;
                }
            };
            partPanelRefresh();
            return y;
        }

        // A slot as the game's (its zombie screen's): a dark frame, a darker inside, the colour in it. The frame turns
        // the brackets' yellow when chosen.
        private static Image Slot(Transform parent, string name, string hex, float x, float y, float w, float h, Action click)
        {
            GameObject go = ui.Node(name, parent, typeof(Image), typeof(UnityEngine.UI.Button), typeof(GamepadNavigationItem));
            UiKit.Rect(go, x, y, w, h);
            Image outer = go.GetComponent<Image>();
            outer.color = SlotOuter;
            GameObject inner = ui.Node("inner", go.transform, typeof(Image));
            UiKit.Rect(inner, 2f, 2f, w - 4f, h - 4f);
            inner.GetComponent<Image>().color = SlotInner;
            inner.GetComponent<Image>().raycastTarget = false;
            GameObject col = ui.Node("colour", go.transform, typeof(Image));
            UiKit.Rect(col, 5f, 5f, h - 10f, h - 10f);
            Color c = SlotInner;
            if (hex != null) ColorUtility.TryParseHtmlString("#" + hex, out c);
            col.GetComponent<Image>().color = c;
            col.GetComponent<Image>().raycastTarget = false;
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = outer;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.05f;
            b.colors = cb;
            b.onClick.AddListener(() => click());
            FocusFrame(go, w, h);
            go.GetComponent<GamepadNavigationItem>().SetCallbacks(null, null, () => click());
            UiSounds.Add(go, UiSounds.Kind.Button); // an arrow: as the game's arrow buttons
            return outer;
        }

        // A wide slot: the colour square, then its name (Original, a kept named colour).
        private static Image WideSlot(Transform parent, string name, string hex, string text, float x, float y, Action click, out float w)
        {
            TextMeshProUGUI probe = ui.Text(parent, "probe", text ?? "", 0.9f, TextAlignmentOptions.Left, false, 0f, 0f, 200f, SlotPx);
            float tw = Mathf.Ceil(probe.GetPreferredValues(text ?? "").x);
            probe.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(probe.gameObject);
            w = Mathf.Min(partColW, SlotPx + tw + 12f);
            Image outer = Slot(parent, name, hex, x, y, w, SlotPx, click);
            ui.Text(outer.transform, "label", text ?? "", 0.9f, TextAlignmentOptions.Left, false, SlotPx, 0f, w - SlotPx - 6f, SlotPx);
            return outer;
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
            keptNamed.Clear(); // each opening keeps what the choices are then (ColourPart)
            coloursLook = null;
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
                // the tabs across the window, a rule under them (the place of the two headings before, 2026-11-08)
                coloursTabs = (RectTransform)ui.Node("colours tabs", colours.transform).transform;
                UiKit.Rect(coloursTabs.gameObject, Pad, top, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, TabH);
                GameObject tabRule = ui.Node("colours tabs rule", colours.transform, typeof(Image));
                UiKit.Rect(tabRule, Pad, top + TabH + 3f, CW - 2f * Pad - ScrollbarW - ColoursScrollGap, 1f);
                tabRule.GetComponent<Image>().color = UiKit.EdgeColor;
                tabRule.GetComponent<Image>().raycastTarget = false;
                top += TabH + 8f;
                colourTab = colourTabs.Count > 0 ? colourTabs[0].Key : null; // each opening on the first tab
                tabsBuilt = null;
                BuildColourTabStrip();
                coloursScroll = ui.ScrollView(colours.transform, "colours content", Pad, top, CW - 2f * Pad, CH - top - Gap - ButtonsH - Pad, out coloursRoot, 2f, manual: true);
                ui.AddScrollbar(coloursScroll);
                GameObject done = ui.OwnButton(colours.transform, "colours done", UiWords.Done.Text, ButtonW, ButtonH, () => CloseColours(keep: true));
                UiKit.Rect(done, CW - Pad - ButtonW, CH - Pad - ButtonH, ButtonW, ButtonH);
                UiKit.NavGroup(done, 1);
                var cnav = colours.GetComponent<GamepadNavigationController>();
                UiKit.Hand(cnav, 0, GUIDirection.Down, 1); UiKit.Hand(cnav, 1, GUIDirection.Up, 0); // the rows and swatches <-> Done
                UiKit.Hand(cnav, 0, GUIDirection.Up, 2); UiKit.Hand(cnav, 2, GUIDirection.Down, 0); // the tabs over the rows
                coloursBefore.Clear();
                foreach (var g in colourGroups) foreach (MountSettingsPage.Row r in g.Rows) if (r.Entry != null) coloursBefore[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
                BuildColourTab();
                panel.SetActive(false); // the journal waits underneath
                controls.NavDirty = true;
                colours.SetActive(true);
                UiKit.FitToScreen(colours, CW, CH);
                Refresh();
                // its picture at the next frame, not at the picture timer's next turn (up to 0.2 s of an empty box: the
                // colours-window shots of 2026-11-04, the box empty for each mount's first look, its picture already built)
                nextPicture = 0f;
                Plugin.Log.LogInfo($"Settings: the colours window is open for {builtFor} ({colourRows.Count} rows on its first tab of {colourTabs.Count}).");
                UiSounds.WindowOpened(); // a window over the journal, as the game's smaller windows (UiSounds)
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the colours window can't open (" + e.Message + ").");
                CloseColours(keep: true, sound: false);
            }
        }

        // reshow false: the journal closes with it, and its close is the sound
        private static void CloseColours(bool keep, bool reshow = true, bool sound = true)
        {
            if (colours != null && reshow && sound) UiSounds.WindowClosed();
            keptNamed.Clear();
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
            tabButtons.Clear();
            tabsBuilt = null;
            coloursTabs = null;
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

        // A companion's ride or cart key pressed while the journal is open (KeyActions; the user, 2026-11-07): what the
        // journal's Ride or Drive the cart button does (Call), when the key is its page's companion's (MountCompanion.Ride
        // / Drive), the journal itself is on top (not its colours window, not the options over it) and that button is
        // shown (a mount that pulls no cart has no Drive). False: nothing done, the key waits as the others do.
        internal static bool CallFromKey(MountCompanion companion, MountStyle style)
        {
            if (panel == null || !panel.activeSelf || colours != null || OptionsWindow.IsShown || page == null || !page.IsCompanion(companion)) return false;
            GameObject button = style == MountStyle.Cart ? driveButton : rideButton;
            if (button == null || !button.activeSelf) return false;
            Plugin.Log.LogInfo($"Settings: the {(style == MountStyle.Cart ? "cart" : "ride")} key in the Mounts panel: as its {(style == MountStyle.Cart ? "Drive the cart" : "Ride")} button.");
            Call(style);
            return true;
        }

        // A search box: typing filters the list by name; the mods' keys wait while it has focus (KeyActions).
        private static TMP_InputField SearchBox(Transform parent, float x, float y, float w, float h)
        {
            TMP_InputField field = controls.InputBox(parent, "search", x, y, w, h, UiWords.Search.Text, 40);
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

        // TickGamepad's first-focus callback, made once: the window shown this frame in a field (a lambda capturing it made
        // a closure on every Tick, the panel closed too; docs/performance.md row 22).
        private static GameObject focusWindow;
        private static readonly Action<GamepadNavigationController> focusShown = nav => FocusStart(nav, focusWindow);

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
        // every journal row's controls read the row it is listed for (a kept row pointed at the new mount's row); the count wrong
        internal static int TestRowsBoundWrong()
        {
            int wrong = 0;
            foreach (var e in rows) if (e.Go != null && rowBound.TryGetValue(e.Go, out RowControls.Bound b) && b.Row != e.Row) wrong++;
            return wrong;
        }
        internal static int TestColourRows => colourRows.Count;
        internal static Transform TestRoot(bool coloursWindow) => coloursWindow ? (colours != null ? colours.transform : null) : (panel != null ? panel.transform : null);
        internal static int TestEntries => entries.Count;
        internal static string TestBuiltFor => builtFor;
        internal static void TestSelect(string id) => Select(id);
        internal static bool TestDriveShown => driveButton != null && driveButton.activeSelf;
        internal static Texture TestPicture => picture != null && picture.gameObject.activeSelf ? picture.texture : null;
        internal static Texture TestShownPicture => ShownPicture() != null && ShownPicture().gameObject.activeSelf ? ShownPicture().texture : null; // the colours window's when it is open (MeasureTests.SkinOffset)
        internal static bool TestColourGroup(string heading) => colourGroups.Exists(g => g.Group == heading);
        internal static string TestColourGroups()
        {
            var names = new List<string>();
            foreach (var g in colourGroups) names.Add((g.Group ?? "(none)") + " " + g.Rows.Count);
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }
        // the colours window's tabs: their names as drawn with their rows' count, the chosen one marked with a star
        internal static string TestColourTabs()
        {
            var names = new List<string>();
            foreach (var t in colourTabs) names.Add((t.Key == colourTab ? "*" : "") + t.Name + " " + t.Rows.Count);
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }
        internal static int TestColourTabCount => colourTabs.Count;
        internal static int TestColourTabButtons => tabButtons.Count;
        internal static bool TestColourTabIsParts(int i) => i >= 0 && i < colourTabs.Count && colourTabs[i].Key == PartsTab;
        // the i-th tab chosen as its button would (false: no such tab or no window)
        internal static bool TestShowColourTab(int i)
        {
            if (colours == null || i < 0 || i >= colourTabs.Count) return false;
            ShowColourTab(colourTabs[i].Key);
            return colourTab == colourTabs[i].Key;
        }
        // the colours window's colour parts (ColourPart) on the chosen tab, in order: each its choice row and its part
        private static List<(MountSettingsPage.Row Choice, GameObject Go)> TestParts()
        {
            var parts = new List<(MountSettingsPage.Row, GameObject)>();
            foreach (var x in colourRows)
            {
                if (x.Go != null && x.Row.RowKind == MountSettingsPage.RowKind.Choice && x.Row.Entry != null && x.Go.name.EndsWith("colour " + x.Row.Entry.Definition.Key, StringComparison.Ordinal)) parts.Add((x.Row, x.Go));
            }
            return parts;
        }
        internal static int TestColourPartCount => TestParts().Count;
        internal static MountSettingsPage.Row TestColourPartRow(int i) { var p = TestParts(); return i >= 0 && i < p.Count ? p[i].Choice : null; }
        internal static MountSettingsPage.Row TestColourPartPalette(int i)
        {
            MountSettingsPage.Row c = TestColourPartRow(i);
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            int at = c != null && tabRows != null ? tabRows.IndexOf(c) : -1;
            return at >= 0 && at + 1 < tabRows.Count ? tabRows[at + 1] : null;
        }
        // a square of the i-th part pressed as a click or the gamepad's select would ("swatch default", "swatch kept",
        // "swatch <PaletteColor>"); false: no such part or square
        internal static bool TestPressSwatch(int i, string square)
        {
            if (i < 0 || i >= parts.Count) return false;
            ChoosePart(parts[i]); // its palette on the right, as a click on its row
            UnityEngine.UI.Button b = null; // by the end of its name (the panel puts its prefix before every name)
            foreach (UnityEngine.UI.Button c in parts[i].Go.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (c.name.EndsWith(square, StringComparison.Ordinal)) { b = c; break; }
            if (b == null && partPanel != null) foreach (UnityEngine.UI.Button c in partPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (c.name.EndsWith(square, StringComparison.Ordinal)) { b = c; break; }
            if (b == null || !b.interactable || !b.gameObject.activeInHierarchy) return false;
            b.onClick.Invoke();
            return true;
        }
        // the i-th part's field as drawn: its value's name (Original, a kept named colour; none for a palette colour)
        internal static string TestColourPartValue(int i) => i >= 0 && i < parts.Count && parts[i].FieldText != null ? parts[i].FieldText.text : null;
        // where every row of the colours window stands (the parts' rows, the others, the palette's slots): the same
        // before and after a pick (the user, 2026-11-10: "it moves stuff up and down")
        internal static string TestColourLayout()
        {
            var sb = new StringBuilder();
            foreach (var x in colourRows) if (x.Go != null) sb.Append(Mathf.RoundToInt(((RectTransform)x.Go.transform).anchoredPosition.y)).Append(',');
            if (partPanel != null) sb.Append('|').Append(Mathf.RoundToInt(partPanel.sizeDelta.y)).Append('|').Append(partPanel.childCount);
            return sb.ToString();
        }
        // whether the i-th part's lightness is live (a palette colour) or greyed (Original, a kept named colour)
        internal static bool TestColourLightLive(int i)
        {
            if (i < 0 || i >= parts.Count) return false;
            ChoosePart(parts[i]);
            CanvasGroup g = partPanel != null ? partPanel.GetComponentInChildren<CanvasGroup>(true) : null;
            return g != null && g.interactable && g.alpha > 0.9f;
        }
        // the chosen tab's rows by column as TabColumns splits them ('L' or 'R' a row, shown or not): the same whatever a
        // value opens (the user, 2026-11-10: a row moved from one column to the other when another was set to Custom)
        internal static string TestColourColumns()
        {
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (tabRows == null) return "";
            var cols = TabColumns(tabRows);
            var sb = new StringBuilder(tabRows.Count);
            foreach (MountSettingsPage.Row r in tabRows) sb.Append(cols.Count > 1 && cols[1].Rows.Contains(r) ? 'R' : 'L');
            return sb.ToString();
        }
        // every choice of the chosen tab that has a custom colour after it, shown now, set to its "Custom" value (on), or
        // each put back as it was (off); how many were set or put back
        private static readonly List<(BepInEx.Configuration.ConfigEntryBase Entry, object Was)> testCustomWas = new List<(BepInEx.Configuration.ConfigEntryBase, object)>();
        internal static int TestCustomAll(bool on)
        {
            int n = 0;
            if (!on)
            {
                foreach (var w in testCustomWas) { w.Entry.BoxedValue = w.Was; n++; }
                testCustomWas.Clear();
                if (n > 0) Refresh(); // as the row's control does after a pick
                return n;
            }
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (tabRows == null) return 0;
            for (int i = 0; i + 1 < tabRows.Count; i++)
            {
                MountSettingsPage.Row r = tabRows[i];
                if (r.RowKind != MountSettingsPage.RowKind.Choice || r.Entry == null || r.ChoiceValues == null || !IsPalette(tabRows[i + 1])) continue;
                if (r.Visible != null && !r.Visible()) continue;
                foreach (object v in r.ChoiceValues)
                {
                    if (v == null || v.ToString() != "Custom") continue;
                    if (!Equals(r.Entry.BoxedValue, v))
                    {
                        testCustomWas.Add((r.Entry, r.Entry.BoxedValue));
                        r.Entry.BoxedValue = v;
                        n++;
                    }
                    break;
                }
            }
            if (n > 0) Refresh(); // as the row's control does after a pick: the preset's rows come
            return n;
        }
        // whether every row of the chosen tab that shows was built, and only those (the two columns hold the tab whole)
        internal static bool TestColourTabWhole()
        {
            List<MountSettingsPage.Row> tabRows = ColourTabRows();
            if (tabRows == null) return colourRows.Count == 0;
            if (HasColourParts(tabRows))
            {
                int want = 0, built = 0;
                for (int i = 0; i < tabRows.Count; i++)
                {
                    MountSettingsPage.Row r = tabRows[i];
                    bool part = i + 1 < tabRows.Count && IsColourPart(r, tabRows[i + 1], out _);
                    bool shows = r.Visible == null || r.Visible();
                    if (part) { i++; if (i + 1 < tabRows.Count && tabRows[i + 1].RowKind == MountSettingsPage.RowKind.Steps) i++; }
                    if (!shows) continue;
                    want++;
                    foreach (var x in colourRows) if (x.Row == r) { built++; break; }
                }
                foreach (var x in colourRows) if (!tabRows.Contains(x.Row)) return false;
                return want == built && parts.Count > 0 && partPanel != null;
            }
            int shown = 0;
            foreach (MountSettingsPage.Row r in tabRows) if (r.Visible == null || r.Visible()) shown++;
            foreach (var x in colourRows) if (!tabRows.Contains(x.Row)) return false;
            return colourRows.Count == shown;
        }
        internal static void TestClose(bool keep, bool commit = false, bool back = true)
        {
            committing = commit; // commit: as Ride and Drive the cart close it (they close with back: false)
            Close(keep, back);
        }
        internal static void TestOpenColours() => OpenColours();
        internal static void TestCloseColours(bool keep) => CloseColours(keep);
        // the colours window's rows taller than its view (they scroll), and a scroll to the top (1) or the end (0)
        internal static bool TestColoursScroll => coloursScroll != null && coloursScroll.content != null && coloursScroll.viewport != null
            && coloursScroll.content.rect.height > coloursScroll.viewport.rect.height + 1f;
        internal static void TestScrollColours(float to) { if (coloursScroll != null) coloursScroll.verticalNormalizedPosition = to; }
        // the colours window's picture as it is drawn (the measurements: its box empty for every mount but the donkey in the
        // shots since 2026-10-30, the journal's picture drawn): shown, its texture and which view, its size against the box's
        internal static Texture TestColoursTexture => coloursPicture != null && coloursPicture.gameObject.activeInHierarchy ? coloursPicture.texture : null;
        internal static string TestColoursPicture()
        {
            if (coloursPicture == null) return "no colours picture";
            var rt = (RectTransform)coloursPicture.transform;
            Texture t = coloursPicture.texture;
            Vector2 art = t != null ? PreviewStage.ArtSize(t) : Vector2.zero;
            string box = coloursStage != null ? coloursStage.rect.width.ToString("0") + "x" + coloursStage.rect.height.ToString("0") : "none";
            return "shown " + coloursPicture.gameObject.activeInHierarchy + ", " + PreviewStage.TestViewOf(t) + (t != null ? " " + t.width + "x" + t.height : "")
                + (picture != null && picture.texture == t ? " (the journal's too)" : " (the journal shows another)") + ", art " + art.x.ToString("0") + "x" + art.y.ToString("0")
                + ", drawn " + rt.sizeDelta.x.ToString("0") + "x" + rt.sizeDelta.y.ToString("0") + " at " + rt.anchoredPosition.x.ToString("0") + "," + rt.anchoredPosition.y.ToString("0")
                + " in a box " + box + ", alpha " + coloursPicture.color.a.ToString("0.00");
        }
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
            if (!chosen.Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase) || !string.Equals(page?.SkinChosen(chosen) ?? "", builtForSkin ?? "", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(page?.SkinNotInstalled(chosen), builtForMissing, StringComparison.OrdinalIgnoreCase)) // a skin not installed stands in as the default: its own change too
            {
                BuildRight(); // the mount, or its skin, changed under the panel (the cfg, a key, the Skin row): the right side follows; the colours window is the skin's
                MarkList();
                return;
            }
            if (Signature(journalGroups) != journalSignature)
            {
                journalSignature = BuildRows(journalGroups, contentRoot, contentScroll, journalW - ScrollbarW, rows, out _); // a preset chosen or left: its rows come or go
            }
            if (colours != null && ColourTabSignature() != coloursSignature)
            {
                BuildColourTab(); // the chosen tab's rows, a preset having shown or hidden one
            }
            RefreshRows(rows);
            if (colours != null) RefreshRows(colourRows);
            if (colours != null) partPanelRefresh?.Invoke(); // the chosen part's palette (its slot, its lightness)
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
                Close(keep: true, back: false, sound: false); // the game left (a load, the main menu): nothing to go back to (the game window is forgotten)
                return;
            }
            // the game's language changed under the journal (normal play can't: the game's Settings window doesn't open over
            // ours): closed, what was chosen kept; opened again, its words and its font are the new language's
            if (panel != null && builtLanguage != null && !string.Equals(builtLanguage, Texts.Language, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.LogInfo($"Settings: the game's language changed to {Texts.Language} while the Mounts panel was open: closed (it opens again in that language).");
                builtLanguage = null;
                Close(keep: true);
                return;
            }
            // the rest only while the journal or its colours window is the one shown (the options window over it has its own)
            GameObject shown = colours != null ? colours : panel != null && panel.activeSelf ? panel : null;
            if (shown != null) FollowCover();
            if (shown != null && !covered)
            {
                controls.TrackTyping();
                Wheel();
                if (GameWindowAside.ControlsTaken) GameWindowAside.TakeControls("the Mounts panel"); // re-asserted: a game popup closing gives ByUI back
                if (GameWindowAside.ControlsTaken)
                {
                    focusWindow = shown;
                    controls.TickGamepad(shown, focusShown);
                }
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
                using (Perf.Measure(Perf.S.Preview)) pic = PreviewStage.Picture(paper, withCart: ColoursWantCart());
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
                // (and the colours window's tabs, 2026-11-10: the mount alone on a tab without cart rows; its photo is the one shown there)
                else if (!PreviewStage.Failed && ShownPicture() is RawImage held && held.texture != null && held.texture != pic && held.gameObject.activeSelf
                    && !PreviewStage.ShowsLookNow(pic, paper))
                {
                    pic = held.texture;
                    nextPicture = 0f;
                }
                // as large as the stage allows, up to the scale, centred in it (the stage renders at 3x detail: a fraction of that is a smooth downscale)
                if (panel.activeSelf && !ChosenMissing) Fit(picture, stage, pic, PictureScale);
                // the colours window shows only a photo of the look it colours (2026-11-10: one mount's colours opened over the
                // last mount's photo for the 2 s its alone picture was first framed, the journal having shown that one): its
                // own as soon as it is whole; another tab of the same look keeps the photo shown meanwhile; else nothing
                if (colours != null && coloursPicture != null)
                {
                    string look = paper != null ? paper.LooksRig.Wearing ?? "" : "";
                    if (PreviewStage.Failed || PreviewStage.ShowsLookNow(pic, paper))
                    {
                        coloursLook = look;
                        Fit(coloursPicture, coloursStage, pic, ColoursScale(pic), screenWhole: true);
                    }
                    else
                    {
                        if (coloursLook != look) Fit(coloursPicture, coloursStage, null, CPictureScale);
                        nextPicture = 0f; // asked again each frame until it is
                    }
                }
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

        // screenWhole: a whole number of screen pixels per art pixel (the colours window, the user, 2026-11-10: a tall mount
        // drawn at 2 where the stage had room for 2.82, half the donkey's 4), else of the panel's own units (the journal).
        // A unit is about 2 screen pixels at 2560x1440 and 1.5 at 1920x1080, so whole units were not whole pixels there.
        private static void Fit(RawImage img, RectTransform box, Texture pic, float maxScale, bool screenWhole = false)
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
                // the stage is point-filtered: a whole number of pixels per art pixel, no shimmer
                float px = screenWhole ? ScreenPixelsPerUnit(box) : 0f;
                float whole = px > 0.01f ? Mathf.Floor(k * px + 0.001f) : 0f;
                if (whole >= 1f) k = whole / px;
                else if (k >= 1f) k = Mathf.Floor(k);
                ((RectTransform)img.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
            }
            img.gameObject.SetActive(pic != null);
        }

        // Screen pixels per unit of the panel at the box (the canvas's scale and the window's own, when it is scaled down to
        // fit a small screen); 0 when there is no canvas.
        private static float ScreenPixelsPerUnit(RectTransform box)
        {
            Canvas c = box.GetComponentInParent<Canvas>();
            if (c == null) return 0f;
            Canvas root = c.rootCanvas;
            float rootScale = root.transform.lossyScale.x;
            return rootScale > 0.0001f ? root.scaleFactor * box.lossyScale.x / rootScale : 0f;
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
                if (coloursPicture != null && coloursStage != null) RefitOne(coloursPicture, coloursStage, ColoursScale(coloursPicture.texture), screenWhole: true);
            }
            catch (Exception e)
            {
                Canvas.willRenderCanvases -= Refit; // never every frame: off for the session, said once
                Plugin.Log.LogWarning("Settings: the Mounts panel's picture refit is off (" + e.Message + ").");
            }
        }

        private static void RefitOne(RawImage img, RectTransform box, float maxScale, bool screenWhole = false)
        {
            if (!img || !box || !img.gameObject.activeInHierarchy) return;
            Texture t = img.texture;
            if (t == null || !fitted.TryGetValue(img, out (int W, int H) was) || (was.W == t.width && was.H == t.height)) return;
            Fit(img, box, t, maxScale, screenWhole);
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

        // sound: the game's win_close (UiSounds), unless the game window under it closes with its own (Ride, Drive)
        private static void Close(bool keep, bool back = true, bool sound = true)
        {
            bool wasOpen = panel != null;
            try { controls.CommitFields(); } catch (Exception e) { Plugin.Log.LogWarning("Settings: a name being typed could not be kept: " + e.Message); } // a name still being typed is kept
            controls.CloseDropdown(refocus: false);
            MountSettingsPage closing = page;
            if (wasOpen) Perf.TracePick("the panel closed, " + (closing?.MountChosen ?? MountCatalog.DonkeyId) + " chosen"); // the world's mount turning into it
            closing?.SavePickedSoon(); // the picking is over: the chosen mount to the cfg file half a second on (put back below when not kept: written again by its set)
            DeferredSave.Soon(); // the rows changed in it too
            if (closing != null) lastPage = closing; // its pending write is done by Tick after the panel is gone
            Looks.KeepPanelHoldsAWhile(); // the looks shown while it was open, kept a while (picked again soon: no build); the worn ones stay with their wearers
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
            rowBound.Clear(); // the rows go with the panel
            spareRows.Clear();
            cards.Clear();
            journalGroups.Clear();
            colourGroups.Clear();
            colourTabs.Clear();
            colourTab = null;
            statLines.Clear();
            builtFor = null;
            builtForSkin = null;
            builtForMissing = null;
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
            covered = false;
            bool fromOptions = overOptions;
            overOptions = false;
            if (!wasOpen) return;
            if (sound && Mount.IsGameReady && (back || !GameWindowAside.HasUnder)) UiSounds.WindowClosed();
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
