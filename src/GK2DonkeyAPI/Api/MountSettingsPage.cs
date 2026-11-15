using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace GK2DonkeyAPI.Api
{
    // A player mod's settings, defined once and shown where the player looks (Mounts.SettingsPage), the same for every
    // player (since 0.42.0; GK2 Mod Framework's Mods menu before, when installed, else pages in the game's Settings
    // window; the framework isn't on the Steam Workshop, the user 2026-10-30, docs/options-window.md):
    // - the options window (GK2DreasUI.OptionsWindow): every row but the look rows, its sections side by side with their
    //   rows, each our own control (keys set there too); opened from the mod's button in the game's Settings window
    //   (SettingsButtons), the Mounts panel's Options button, and with GK2 Mod Framework installed the Settings button
    //   of the mod's entry in its Mods menu (FrameworkLink: the entry has one setting, a button to it);
    // - the Mounts panel (MountsPanel.cs: the mounts listed, the chosen one drawn with its words and its rows): the look
    //   rows (look: true; the Mount and Skin rows, the colours), opened by PanelButton from the page and OpenPanel from a
    //   mod's key.
    // The page itself (its rows, the cfg binding with the moved sections and keys, the options window) is the window
    // library's SettingsPage (docs/ui-library.md), wrapped here: this class keeps every public method a mod built against
    // it uses, and adds the mount rows (MountRows, FoalRows, the colours, the per-mount and per-skin sets). A row's mount,
    // skin and part are its MountRowInfo (Info), the API's data on the library's row.
    // Each row binds a BepInEx entry in the mod's own cfg, runs its apply action now and on every change (from a window or
    // the cfg), and shows under its section. Call Publish once, after the rows, from the mod's Awake; the page reaches the
    // windows the frame after (Show), when every plugin's Awake has run, so a mount or skin another mod registered in its
    // Awake is in the lists whatever order BepInEx loaded the plugins in (it orders by dependencies only, and a mount mod
    // can't ask to load before the mod that owns the page; docs/mounts-as-mods.md 4).
    public sealed class MountSettingsPage
    {
        // The API's data on a library row: the mount and skin of a row kept per mount or per skin, a skin's own part
        // (ToneRows: the colours window's tab of its own), a cart row (CartRow: the colours window draws the cart only on a
        // tab with such rows), what a colour row colours (a mount region or a skin's own tone: the "Original" square), a
        // picture row's cart.
        internal sealed class MountRowInfo
        {
            internal string MountId;   // a PerMount or PerSkin row's mount (null: a row of no one mount)
            internal string SkinId;    // a PerSkin row's skin (null: a row of no one skin)
            internal bool SkinPart;
            internal bool CartPart;
            internal string ArtRegion, ArtTone;
            internal bool WithCart;    // RowKind.Preview: the donkey with its cart, or alone
        }

        private static readonly MountRowInfo NoInfo = new MountRowInfo();

        // A row's mount data (never null: a row of no mount answers nulls and falses).
        internal static MountRowInfo Info(SettingsPage.Row r) => r?.Tag as MountRowInfo ?? NoInfo;

        private static MountRowInfo Own(SettingsPage.Row r)
        {
            if (!(r.Tag is MountRowInfo i))
            {
                i = new MountRowInfo();
                r.Tag = i;
            }
            return i;
        }

        internal readonly SettingsPage Page;
        internal static readonly List<MountSettingsPage> All = new List<MountSettingsPage>(); // every page made this session (the upgrade self-test)
        internal MountCompanion PreviewOf;

        internal MountSettingsPage(string ownerId, ConfigFile config, string title, string author, string description, Func<string> status)
        {
            Page = new SettingsPage(ownerId, config, title, author, description, status ?? (() => Mounts.StatusText));
            Page.BeforeShow = BeforeShow;
            Page.AfterShow = AfterShow;
            All.Add(this);
        }

        // ---- the library page's own, as this page's (the windows, the tests and the panel read them here) ----

        internal List<SettingsPage.Row> Rows => Page.Rows;
        internal string OwnerId => Page.OwnerId;
        internal string Title => Page.Title;
        internal string Author => Page.Author;
        internal string Description => Page.Description;
        internal ConfigFile Config => Page.Config;
        internal Func<string> StatusValue => Page.StatusValue;
        internal string Version => Page.Version;
        internal bool Shown => Page.Shown;
        internal void FinishBinding() => Page.FinishBinding();
        internal bool InOptions(SettingsPage.Row r) => Page.InOptions(r);
        internal bool IsRetired(string cfgSection, string key) => Page.IsRetired(cfgSection, key);
        internal List<SettingsPage.UpgradeClaim> Upgrade(Dictionary<string, Dictionary<string, string>> file, out List<(string Section, string Key, string Raw)> unclaimed) => Page.Upgrade(file, out unclaimed);
        internal static Dictionary<string, Dictionary<string, string>> ReadSections(string path) => SettingsPage.ReadSections(path);
        internal static string PlainName(string section) => SettingsPage.PlainName(section);
        internal static int SectionNumber(string section) => SettingsPage.SectionNumber(section);
        internal static Dictionary<ConfigDefinition, string> OrphansOf(ConfigFile cfg) => SettingsPage.OrphansOf(cfg);
        internal static void FillList(SettingsPage.Row r) => SettingsPage.FillList(r);
        internal static Func<bool> Guard(Func<bool> f, bool fallback, string where, string what) => SettingsPage.Guard(f, fallback, where, what);
        internal static string LightnessKey(string key) => SettingsPage.LightnessKey(key);
        internal static bool IsClearKey(KeyboardShortcut k) => SettingsPage.IsClearKey(k);
        internal static bool AnyShown => SettingsPage.AnyShown;

        private string Sec(string name) => Page.Sec(name);

        // A key that moved to another section (or name) on the page: the first bind of (section, key) with nothing in
        // the cfg takes the value the cfg has as (oldSection, oldKey), whatever number oldSection had. Call it before
        // the row. Sections that only moved on the page keep their values without this.
        // A key that moved more than once names each old place with a call of its own (0.36.0: one call named one).
        public void MovedKey(string section, string key, string oldSection, string oldKey = null) => Page.MovedKey(section, key, oldSection, oldKey);

        // A setting the mod no longer has, on purpose (0.36.0; a mod's Riding speed and Cart speed, now each mount's
        // own): its line leaves the player's file (under any number of the section) at Publish, and the upgrade
        // self-test counts it as retired, not as a value lost. Call it once per key, any time before Publish.
        public void RetiredKey(string section, string key) => Page.RetiredKey(section, key);

        // A per-mount key with its mount's id from before the prefixes ("Coat_acme.crab" -> "Coat_crab": a dev's
        // cfg of 2026-10-24, mounts never released): its old name, so the value carries over; null for any other key.
        // The library asks for it on every bind (UiOwner.KeyAlias).
        internal static string UnprefixedMountKey(string key)
        {
            int u = key.IndexOf('_');
            if (u <= 0) return null;
            int dot = key.IndexOf('.', u + 1);
            return dot > u + 1 && dot < key.Length - 1 ? key.Substring(0, u + 1) + key.Substring(dot + 1) : null;
        }

        // ---- the mount rows ----

        // The rows every player mod wants for choosing its mount, in one call (docs/mounts-design.md §6), meant for a
        // section of their own ("Mount"): a Mount dropdown (every mount), a Skin dropdown with that mount's skins, and
        // whether the foals are the same mount and skin. Cfg keys: Mount, Skin_<mount id>, FoalsWearSkin in the section
        // given. Mounts and skins registered before the page is shown (the frame after
        // Publish: every plugin's Awake has run by then, CatchUp) appear here with no change to yours; later ones after
        // a restart.
        public void MountRows(string section, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            if (mountRow != null) throw new InvalidOperationException("MountRows is called once per page.");
            mountCompanion = companion;
            mountSection = section;
            // A mount (Donkey, Crab, ...) has skins (the donkey: Default, Frost): a Mount
            // dropdown, then a Skin dropdown with that mount's skins. A dropdown's choices are fixed once built, so there
            // is one Skin row per mount, shown for the chosen mount only (cfg keys Mount and Skin_<mount id>). What the
            // companion gets, and the save keeps, is one thing: the skin id (companion.Skin).
            mountRow = List(section, "Mount", MountCatalog.DonkeyId, Looks.MountChoices, "Mount",
                "The mount you ride.",
                v => ApplyMountRows(), look: true);
            Rows[Rows.Count - 1].Untranslated = true; // the panel's list is the picker: its words aren't the page's to translate
            foreach (MountInfo k in Mounts.Catalog.All) AddSkinRow(k);
            ApplyMountRows();
        }

        private MountCompanion mountCompanion;
        private string mountSection;

        // The Mounts panel open: its list moves the Mount row (the picture and the rows follow), the companion's donkey
        // keeps its mount (MountCompanion.HoldForPanel). Closed with Ride or Drive the cart: the choice is his; closed
        // otherwise: the Mount row goes back to the mount he had (that mount's Skin row keeps what was picked for it).
        private string heldMount;

        internal MountCompanion TestCompanion => mountCompanion; // the self-tests

        internal void HoldMountForPanel()
        {
            if (mountRow == null || mountCompanion == null || heldMount != null) return;
            heldMount = mountRow.Value ?? MountCatalog.DonkeyId;
            mountCompanion.HoldForPanel();
        }

        internal void ReleaseMountForPanel(bool commit)
        {
            if (heldMount == null) return;
            string was = heldMount;
            heldMount = null;
            mountCompanion.ReleasePanel(commit);
            if (commit) { ApplyMountRows(); return; }
            if (!string.Equals(mountRow.Value, was, StringComparison.OrdinalIgnoreCase)) mountRow.Value = was; // its SettingChanged applies it
            else ApplyMountRows();
        }
        private readonly Dictionary<string, ConfigEntry<string>> skinRows = new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase); // the Skin row per mount id
        private readonly List<Action<string, string, Func<bool>>> perMountBuilders = new List<Action<string, string, Func<bool>>>(); // PerMount's callers, for the mounts that come later

        private void AddSkinRow(MountInfo k)
        {
            string id = k.Id;
            if (skinRows.ContainsKey(id)) return;
            string first = Looks.FirstSkinOf(id);
            Page.PendingShown = () => string.Equals(mountRow?.Value, id, StringComparison.OrdinalIgnoreCase); // the chosen mount's Skin row shows (Row.Shown)
            try
            {
                skinRows[id] = List(mountSection, "Skin_" + id, first, () => Looks.SkinChoicesOf(id), UiWords.SkinRow.English,
                    $"How your {k.Name.ToLowerInvariant()} looks.",
                    v => ApplyMountRows(), look: true);
                Page.LastRowSays(UiWords.SkinRow);
            }
            finally
            {
                Page.PendingShown = null;
            }
        }

        private void ApplyMountRows()
        {
            if (mountRow == null) return;
            string mount = mountRow.Value ?? MountCatalog.DonkeyId;
            // a mount's id from before the prefixes ("crab": a dev's cfg, 2026-10-24): its full id (acme.crab), kept
            if (GK2DonkeyAPI.Catalog.Find(mount) == null && GK2DonkeyAPI.Catalog.FindShort(mount) is GK2DonkeyAPI.Catalog.Entry full)
            {
                mountRow.Value = full.Id; // SettingChanged runs Apply again
                return;
            }
            skinRows.TryGetValue(mount, out ConfigEntry<string> row);
            string value = row?.Value;
            // The donkey's own art is no skin of another mount: a cfg holding it for one (written while that
            // mount's skin was missing, seen 2026-10-18) takes the mount's first skin instead, and keeps it.
            bool donkeyMount = mount.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value) || (!donkeyMount && value.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase))) value = Looks.FirstSkinOf(mount);
            // a short name ("crab", "frost": a dev's cfg from before skins were named within their mount, 2026-10-24):
            // the skin's full name ("crab/default"), kept
            if (!donkeyMount || !value.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase))
            {
                Looks.SkinDef named = Looks.Find(value);
                if (named != null && !named.Id.Equals(value, StringComparison.OrdinalIgnoreCase) && named.MountId.Equals(mount, StringComparison.OrdinalIgnoreCase)) value = named.Id;
            }
            if (row != null && row.Value != value)
            {
                row.Value = value; // SettingChanged runs Apply again with the healed value
                return;
            }
            mountCompanion.Skin = value;
        }

        // The rows of the mounts registered after MountRows and PerMount were called: a mod whose Awake ran after the
        // page's mod's (BepInEx orders plugins by their dependencies only; docs/mounts-as-mods.md 4). Run once before
        // the page is shown (Show, the frame after Publish, when every Awake has run); a mount still without a skin is
        // left for the next start (the Mount list leaves it out too). Returns how many mounts got rows (a mount whose
        // late skin got its PerSkin rows counts too).
        internal int CatchUp()
        {
            if (mountRow == null || Page.Shown) return 0;
            int added = 0;
            catchingUp = true;
            bool late = Page.AllowLateRows;
            Page.AllowLateRows = true;
            try
            {
                var lateMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MountInfo k in Mounts.Catalog.All)
                {
                    if (skinRows.ContainsKey(k.Id) || k.Skins.Count == 0) continue;
                    AddSkinRow(k);
                    foreach (Action<string, string, Func<bool>> rows in perMountBuilders) RunPerMount(rows, k.Id);
                    foreach (Action<string, string, string, Func<bool>> rows in perSkinBuilders) RunPerSkinAll(rows, k.Id); // its skins' rows too (0.41.0)
                    lateMounts.Add(k.Id);
                    added++;
                }
                // a skin registered after the page for a mount that had its rows (a skin pack loaded after the page's mod;
                // 0.41.0): its PerSkin rows now
                foreach (MountInfo k in Mounts.Catalog.All)
                {
                    if (!skinRows.ContainsKey(k.Id) || lateMounts.Contains(k.Id)) continue;
                    int made = 0;
                    foreach (Action<string, string, string, Func<bool>> rows in perSkinBuilders) made += RunPerSkinAll(rows, k.Id);
                    if (made > 0) added++;
                }
            }
            finally
            {
                catchingUp = false;
                Page.AllowLateRows = late;
            }
            if (added > 0) ApplyMountRows(); // a cfg holding a late mount with its second skin: the companion wears that skin, not the default (the Skin row exists now)
            return added;
        }

        private bool catchingUp;

        // The library's showing of the page (SettingsPage.Show): the late mounts' rows first, GK2 Mod Framework's entry after.
        private void BeforeShow()
        {
            int late = CatchUp();
            if (late > 0) Plugin.Log.LogInfo($"Settings: {Title}: {late} mount(s) registered after the page was built (a mod loaded after {OwnerId}) got their rows before the page was shown.");
        }

        private void AfterShow()
        {
            if (OptionsWindow.HasOptions(Page) && FrameworkLink.TryRegister(this))
            {
                Plugin.Log.LogInfo($"Settings: {Title} is in GK2 Mod Framework's Mods menu too (its Settings button opens the options window).");
            }
        }

        internal bool HasSkinRow(string mountId) => skinRows.ContainsKey(mountId);

        // The skin a mount's Skin row holds when it is none of the skins offered for it (its pack gone): that id, the
        // dropdown's "not installed" choice (FillList); null when the row holds one of them, or there is no row. The
        // journal's stats card says it (MountsPanel.BuildRight: "Skin: Not installed").
        internal string SkinNotInstalled(string mountId)
        {
            if (mountId == null || !skinRows.TryGetValue(mountId, out ConfigEntry<string> row) || string.IsNullOrEmpty(row.Value)) return null;
            foreach ((string id, string _) in Looks.SkinChoicesOf(mountId))
                if (string.Equals(id, row.Value, StringComparison.OrdinalIgnoreCase)) return null;
            return row.Value;
        }
        internal Mount.Instance MountPaper => mountCompanion?.Paper; // the panel's picture when the page has no Preview() row
        internal MountResult UseMount(MountStyle style) => mountCompanion != null ? mountCompanion.UseFromPanel(style) : MountResult.Refused("No mount rows on this page."); // the panel's Ride and Drive buttons
        internal bool IsCompanion(MountCompanion c) => c != null && ReferenceEquals(c, mountCompanion); // a ride or cart key works in this page's journal (KeyActions)
        internal bool IsMountRow(SettingsPage.Row r) => r != null && r.Entry != null && ReferenceEquals(r.Entry, mountRow); // the Mount dropdown: the panel's list is the picker, the row stays out of its rows
        internal bool HasMountRow => mountRow != null;

        // The rows for the mount's babies (foals), for a section of their own ("Babies"; MountRows' section works too):
        // how many, whether they follow the keeper on foot, whether they come indoors (since 0.40.0 they always look like
        // their parent). Cfg keys: Foals, FoalsFollow, FoalsIndoors in the section given (a mod that had them
        // in another section names it with MovedKey first). The count, FoalsFollow and FoalsWearSkin rows default to
        // the companion's values as they are when this is called (set them first for your own defaults).
        public void FoalRows(string section, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            MountCompanion me = companion;
            // Per mount (the user, 2026-10-26: "make the amount of babies be saved per mount"), in the Mounts panel's journal
            // (look rows of the section; the donkey's keys plain, the others' "_<mount id>"), only for a mount that has
            // babies. A page without MountRows: the donkey's alone (PerMount).
            PerMount((mountId, sfx, shown) =>
            {
                MountOwn l = me.OwnOf(mountId);
                Func<bool> babies = () => Catalog.Find(mountId)?.Foals ?? true;
                Steps(section, "Foals" + sfx, l.FoalCount, 0, MountCompanion.MaxFoals, 1, UiWords.Babies.English, "How many little ones follow your mount around.", v => l.FoalCount = v, v => v == 0 ? UiWords.None.Text : v.ToString(), look: true, visible: babies);
                Page.LastRowSays(UiWords.Babies);
                Toggle(section, "FoalsFollow" + sfx, l.FoalsFollowOnFoot, UiWords.BabiesFollow.English, "When you get off, the babies follow you instead of staying with your mount.", v => l.FoalsFollowOnFoot = v, babies, look: true);
                Page.LastRowSays(UiWords.BabiesFollow);
                Toggle(section, "FoalsIndoors" + sfx, true, UiWords.BabiesIndoors.English, "The babies follow you into buildings and back out.", v => l.FoalsComeIndoors = v, babies, look: true);
                Page.LastRowSays(UiWords.BabiesIndoors);
                // "Babies are the same mount and skin" is gone (the user, 2026-10-27): off made another mount's babies plain
                // donkeys. Babies always look like their parent (MountLook.FoalsWearSkin stays true unless a mod sets it); the
                // old lines leave the file.
                RetiredKey(section, "FoalsWearSkin" + sfx);
            });
        }

        // The signature from before look (added in 0.40.0), kept for mods built against it (every parameter required:
        // a call is never ambiguous between the two).
        public ConfigEntry<bool> Toggle(string section, string key, bool value, string label, string description, Action<bool> apply, Func<bool> visible)
            => Toggle(section, key, value, label, description, apply, visible, look: false);

        // On or off.
        // look: the Mounts panel's row (not the options window's), as Choice's.
        public ConfigEntry<bool> Toggle(string section, string key, bool value, string label, string description, Action<bool> apply = null, Func<bool> visible = null, bool look = false)
            => Page.Toggle(section, key, value, label, description, apply, visible, look);

        // One of an enum's values; labels (one per value, in order) are what players see, else the value names.
        // look: the Mounts panel's row (PanelButton), not the options window's.
        public ConfigEntry<T> Choice<T>(string section, string key, T value, string label, string description, Action<T> apply = null, string[] labels = null, bool look = false, Func<bool> visible = null) where T : struct, Enum
            => Page.Choice(section, key, value, label, description, apply, labels, look, visible);

        // One of a list of ids read when the row is first shown (the skins installed, say): choices gives (id, label)
        // pairs; the id is what the cfg holds, from every menu alike. A stored id that isn't in the list is shown by
        // a name made from the id, "Blaze (not installed)" (MountsPanel.NameFromId; the id itself showed until
        // 2026-10-27), and left alone, so it returns with what provides it. The list is read once per
        // session, when the page is shown (the frame after Publish): anything registered after that appears after a
        // restart. look: the Mounts panel's row, not the options window's.
        public ConfigEntry<string> List(string section, string key, string value, Func<IReadOnlyList<(string Id, string Label)>> choices, string label, string description, Action<string> apply = null, bool look = false, Func<bool> visible = null)
            => Page.List(section, key, value, choices, label, description, apply, look, visible);

        // A whole number from min to max in steps; format turns it into the shown text (e.g. "100%").
        public ConfigEntry<int> Steps(string section, string key, int value, int min, int max, int step, string label, string description, Action<int> apply = null, Func<int, string> format = null, bool look = false, Func<bool> visible = null)
            => Page.Steps(section, key, value, min, max, step, label, description, apply, format, look, visible);

        // A key (with modifiers), set in the options window's key box (KeyCapture: Shift, Ctrl or Alt alone too).
        // pressed: called when the player presses the key (its modifiers held; other keys held, like walking, don't
        // matter), while the API is on. The API polls it: the mod needs no Update of its own. Not while the Mounts panel
        // or its colours window is open (0.36.0), except a key whose pressed is OpenPanel (it closes the panel).
        // Plain Backspace means no key: the key box sets none for it, and a cfg (or an older menu) holding Backspace is
        // cleared at once.
        public ConfigEntry<KeyboardShortcut> Key(string section, string key, KeyboardShortcut value, string label, string description, Action<KeyboardShortcut> apply = null, Action pressed = null)
            => Page.Key(section, key, value, label, description, apply, pressed, overOwnerWindow: pressed != null && pressed == (Action)OpenPanel); // the rest wait while the panel is open

        // The signature from before look (added in 0.40.0), kept for mods built against it (every parameter required:
        // a call is never ambiguous between the two).
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply, Func<bool> visible)
            => Text(section, key, value, label, description, apply, visible, look: false);

        // Free text, in a text box. visible: shown only while it says so.
        // look: the Mounts panel's row, not the options window's.
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply = null, Func<bool> visible = null, bool look = false)
            => Page.Text(section, key, value, label, description, apply, visible, look);

        // A colour a player picks by name (PaletteColor: Chestnut, Sand, Moss, Sky, ...) and makes lighter or darker
        // (the picture, Preview, shows the result; a swatch row did before it existed). Bound as <key> and <key>Lightness; apply gets the colour as hex RRGGBB. visible:
        // shown only while it says so (e.g. only while its part's choice is Custom). A look row: the Mounts panel's.
        public void Color(string section, string key, string label, string description, PaletteColor value, Action<string> apply, Func<bool> visible = null)
        {
            ConfigEntry<PaletteColor> colour = null;
            ConfigEntry<int> lightness = null;
            string Now() => colour != null && lightness != null ? MountPalette.Hex(colour.Value, lightness.Value) : null;
            void Combine()
            {
                string hex = Now();
                if (hex != null)
                {
                    apply?.Invoke(hex);
                }
            }
            colour = Choice(section, key, value, label, description, _ => Combine(), null, look: true, visible: visible);
            string lightnessKey = LightnessKey(key);
            // a colour key moved here from another section (MovedKey before this call) brings its lightness key along
            List<(string Section, string Key)> froms = Page.MovedFrom(section, key);
            if (froms != null)
            {
                foreach ((string Section, string Key) from in froms.ToArray()) MovedKey(section, lightnessKey, from.Section, LightnessKey(from.Key));
            }
            lightness = Steps(section, lightnessKey, 100, 40, 160, 10, label + ": lightness", "100% is the colour itself; lower is darker, higher is lighter.", _ => Combine(), n => n + "%", look: true, visible: visible);
            Combine();
        }

        // The read-only "Status" row: the page's status (Mounts.StatusText unless the page was given its own).
        public void Status(string section, string label = "Status", string description = "What the mod is doing right now.") => Page.Status(section, label, description);

        // A read-only line, e.g. what the mod is doing.
        public void Info(string section, string label, Func<string> value, string description = "") => Page.Info(section, label, value, description);

        // A heading inside a section: a line with the label and no value, before the rows it names ("Mount colours",
        // "Cart colours" in a Colours section). In the Mounts panel the look rows after it, in its section, stand under
        // this heading in a column of their own instead of under the section's name (MountsPanel; for one, the mount's
        // colours left, the cart's right). Not a picture row's label (Add).
        public void Heading(string section, string label, string description = "") => Page.Heading(section, label, description);

        // The companion whose mount the Mounts panel draws (its picture of the mount and cart in their current colours).
        // Since 0.42.0 the options window shows no picture (the panel shows the mount); the row stays on the page, out of
        // every window, so a mod calling it is unchanged. label: the row's name; cart: kept for mods built against it.
        public void Preview(string section, MountCompanion companion, string label = "Your mount", bool cart = true)
        {
            PreviewOf = companion;
            Page.Add(new SettingsPage.Row
            {
                RowKind = SettingsPage.RowKind.Preview, Section = Sec(section), ShownSection = section, Label = label, Description = "",
                Tag = new MountRowInfo { WithCart = cart },
            });
        }

        // A button that opens the Mounts panel over the options window: every mount listed, the chosen one drawn with its
        // description, what it does and its abilities, and the page's look rows for it (its Skin, the colours); its X and
        // Escape keep what was chosen and go back to the options. Only in a game; greyed in the main menu.
        public void PanelButton(string section, string label, string description)
        {
            Page.Add(new SettingsPage.Row { RowKind = SettingsPage.RowKind.Button, Section = Sec(section), ShownSection = section, Label = label, Description = description, ButtonText = UiWords.Open.English, ButtonWord = UiWords.Open, OnClick = () => MountsPanel.Open(this, toggle: false), OpensOwnerWindow = true });
        }

        // The signature from before visible (added 2026-11-05), kept for mods built against it (every parameter required:
        // a call is never ambiguous between the two).
        public void Button(string section, string label, string description, Action onClick, string buttonText, Func<bool> enabled)
            => Button(section, label, description, onClick, buttonText, enabled, visible: null);

        // A button of the mod's own (0.36.0): onClick runs when it is pressed in the options window, only in a game (greyed
        // in the main menu). buttonText: the word on it. enabled: greyed while false. visible: shown only while true, as a
        // Toggle's (2026-11-05: a button with nothing to do while another row is off). No cfg key. A handler that throws is
        // logged.
        public void Button(string section, string label, string description, Action onClick, string buttonText = "Go", Func<bool> enabled = null, Func<bool> visible = null)
            => Page.Button(section, label, description, onClick, buttonText, enabled, visible);

        // The same (the panel grew out of the look bar); kept so a mod written for 0.30 to 0.34 still opens it.
        public void LookButton(string section, string label, string description) => PanelButton(section, label, description);

        // Opens the Mounts panel (a mod's own key, say). Only in a game.
        public void OpenPanel() => MountsPanel.Open(this, toggle: true); // a key: pressed again while the panel is open, it closes (what was chosen stays)

        // The mount the page's Mount row holds (MountRows), and choosing one: the row takes it, its apply heals the
        // skin and sets the companion, and every per-mount row's condition follows (the panel's list calls this).
        internal string MountChosen => mountRow?.Value;

        internal void SelectMount(string mountId)
        {
            if (mountRow == null || string.IsNullOrEmpty(mountId)) return;
            if (string.Equals(mountRow.Value, mountId, StringComparison.OrdinalIgnoreCase)) return;
            // The cfg file is written once the picking is over (SavePicked: the panel closed, or a moment after the last
            // pick), not inside the set: BepInEx writes the whole file on every change, 3.5 to 4.7 ms in the click's frame
            // of every pick (the trace of 2026-10-25 11:03; the user saw a micro freeze at each pick).
            bool save = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                using (Perf.Measure(Perf.S.PickApply)) mountRow.Value = mountId;
            }
            finally
            {
                Config.SaveOnConfigSet = save;
            }
            if (save)
            {
                pickedUnsaved = true;
                saveAfter = UnityEngine.Time.unscaledTime + PickSaveDelay;
            }
        }

        // A pick not yet written to the cfg file (SelectMount), and when it will be.
        private bool pickedUnsaved;
        private float saveAfter;
        private const float PickSaveDelay = 2f;

        // The picked mount written to the cfg file: now (the panel closing, the game quitting), or once the picking has
        // paused (due). Its own frame, after the picking: a frame with nothing else of note.
        // The panel closed: written a moment later, in a frame of its own (in the closing frame, with the panel's own
        // teardown, it made a 21 to 24 ms frame; the trace of 2026-10-25 15:00).
        internal void SavePickedSoon()
        {
            if (pickedUnsaved) saveAfter = UnityEngine.Time.unscaledTime + 0.5f;
        }

        internal void SavePicked(bool now)
        {
            if (!pickedUnsaved || (!now && UnityEngine.Time.unscaledTime < saveAfter)) return;
            pickedUnsaved = false;
            try
            {
                using (Perf.Measure(Perf.S.PickSave)) Config.Save();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {Title}: the picked mount could not be written to its cfg file ({e.Message}); it is still chosen for this session.");
            }
        }

        // The mod is done adding rows. The page is shown the frame after (Show): its button goes into the game's Settings
        // window (SettingsButtons), opening the options window. The frame between is what lets every plugin's Awake run first
        // (the header's note): the mounts registered meanwhile get their rows (CatchUp) and every list is read then.
        public void Publish() => Page.Publish();

        private ConfigEntry<string> mountRow; // MountRows' Mount row: which mount is chosen

        // Rows kept per mount (the colour rows: another mount's look isn't the donkey's; docs/mount-colours.md 2.7): rows is
        // called once per registered mount with the mount's id, the suffix for the cfg keys ("" for the donkey, so a cfg
        // from before keeps its values under the same keys; "_crab" for a crab) and whether that mount is the one
        // chosen in the Mount row. The rows built in the call show for that mount only; a mod's own visible condition
        // on them (a custom colour with its preset) stays its own (Row.Enabled: the Mounts panel leaves such a row out until it holds).
        // Without a Mount row (MountRows not called) the rows are built once, for the donkey, as plain rows.
        // Greys a row (of this page) unless the condition holds: a colour row for a region none of the mount's skins has
        // (a Harness row on a mount without one). The row stays and keeps its value; the windows show it greyed.
        // The mount a companion's chosen skin is a look of pins this tuning key (its [Mount.Tuning] or a code mount's
        // Tuning, either style): its label, else null. A player's row for such a key does nothing on that mount.
        public static string PinnedBy(MountCompanion companion, string tuningKey)
        {
            if (companion == null || string.IsNullOrEmpty(tuningKey)) return null;
            Looks.SkinDef d = Looks.Find(Looks.StandIn(companion.Skin)); // a skin not installed of a mount that is: its own look's (docs/missing-skin.md)
            if (d == null || d.Unavailable != null) return null; // not installed (nor its mount), or set aside: the donkey comes instead, which pins nothing
            Catalog.Entry k = d.Mount;
            if (k == null || k == Catalog.Donkey) return null;
            if (!Looks.Rig.MountMayPin(tuningKey)) return null; // a key a mount may not pin is refused at the merge: the row stays live
            bool Has(Dictionary<string, object> t) => t != null && t.ContainsKey(tuningKey);
            return Has(k.Tuning) || Has(k.TuningRide) || Has(k.TuningCart) ? k.Label : null;
        }

        // Says which tuning key a row sets, so the row is greyed in the options window while the companion's chosen mount
        // pins that key: a speed row does nothing on a mount that sets its own speed
        // (docs/mounts-foundation-2.md 2.1). Call it after the row; the mod's own condition, if any, stays.
        public void TuningKey(string section, string key, string tuningKey, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            if (string.IsNullOrEmpty(tuningKey)) throw new ArgumentException("A tuning key is needed.", nameof(tuningKey));
            EnableWhen(section, key, () => PinnedBy(companion, tuningKey) == null);
        }

        public void EnableWhen(string section, string key, Func<bool> enabled) => Page.EnableWhen(section, key, enabled);

        // Says a row is about the cart (its paint, its wheels, its lantern; 0.39.0): shown only while its mount pulls one
        // (a PerMount row's mount, else the chosen mount): a mount whose [Mount] says cart = false (MountDef.Cart false)
        // has no cart to colour (the user, 2026-10-25: "for mounts that cant pull the cart ... there's still cart
        // customization ... these shouldnt be available"). Left out of the Mounts panel and the options window alike; its
        // value is kept. Call it after the row, for each of its keys (a custom colour row too).
        public void CartRow(string section, string key)
        {
            string cfgSection = Sec(section);
            foreach (SettingsPage.Row r in Rows)
            {
                if (r.Entry == null || r.Entry.Definition.Section != cfgSection || (r.Entry.Definition.Key != key && r.Entry.Definition.Key != LightnessKey(key))) continue;
                Own(r).CartPart = true; // the Mounts panel's colours window draws the cart only on a tab with such rows
                string id = Info(r).MountId;
                Func<bool> pulls = () => MountPullsCart(id ?? mountRow?.Value);
                Func<bool> shown = r.Shown, visible = r.Visible;
                r.Shown = shown == null ? pulls : () => shown() && pulls();
                r.Visible = visible == null ? pulls : () => visible() && pulls();
            }
        }

        // Says rows are a mount region's rows of one skin (docs/second-tone.md 8; the user, 2026-11-04: every row a part
        // of the look by its name): the choice labelled by the look's name for the region's part (SkinRegions: its
        // [Region.<region>] name, its default's, the donkey's under a skin worn over it), its custom colour and lightness
        // rows in the API's templates ("Belly: custom colour"), as a tone's; and all three left out of the windows while the
        // look has no colour of the region (SkinHasRegion; the value is kept). A look naming no part of the region keeps
        // the rows' own words. Call it after the rows, with the choice's key and its custom colour's (null: none).
        public void RegionRows(string section, string skinId, string region, string choiceKey, string customKey)
        {
            string r = Colors.RegionKeyOf((region ?? "").Trim());
            if (r == null) throw new ArgumentException($"'{region}' is not a mount region ({string.Join(", ", Colors.MountRegionKeys)}): RegionRows(section, skinId, region, choiceKey, customKey)", nameof(region));
            if (string.IsNullOrEmpty(choiceKey)) throw new ArgumentNullException(nameof(choiceKey));
            string cfgSection = Sec(section);
            regionKeys[(skinId ?? "") + "\n" + r] = (cfgSection, choiceKey, customKey); // a part that followed the region carries its value over (CarryOver)
            Term name = SkinRegions.NameTerm(skinId, r);
            string skin = skinId;
            Func<bool> has = () => SkinHasRegion(skin, r) && SkinRegions.HasRow(skin, r); // a look may give a region no row (row = none)
            foreach (SettingsPage.Row row in Rows)
            {
                if (row.Entry == null || row.Entry.Definition.Section != cfgSection) continue;
                string k = row.Entry.Definition.Key;
                bool isChoice = k == choiceKey, isCustom = customKey != null && k == customKey, isLight = customKey != null && k == LightnessKey(customKey);
                if (!isChoice && !isCustom && !isLight) continue;
                row.OwnLabelToo = true;
                if (isChoice) Own(row).ArtRegion = r;
                if (name != null)
                {
                    if (isChoice) row.LabelOf = () => name.Text;
                    else if (isCustom) row.LabelOf = () => UiWords.ToneCustom.Format(name);
                    else row.LabelOf = () => UiWords.ToneLightness.Format(name);
                }
                Func<bool> shown = row.Shown, visible = row.Visible;
                row.Shown = shown == null ? has : () => shown() && has();
                row.Visible = visible == null ? has : () => visible() && has();
            }
        }

        // The keys RegionRows was given, by "<skin>\n<region>": the section, the choice's key and its custom colour's.
        private readonly Dictionary<string, (string Section, string Choice, string Custom)> regionKeys = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        // The region presets' nearest part presets (ToneColor), by the region preset's name: a name both have is itself.
        private static readonly Dictionary<string, ToneColor> NearestPart = new Dictionary<string, ToneColor>(StringComparer.Ordinal)
        {
            ["Pale"] = ToneColor.White, ["Cream"] = ToneColor.White, ["Rose"] = ToneColor.Pink, ["Dark"] = ToneColor.Black,
        };

        // A skin's part that followed a mount region before it was a part of its own (follows =; docs/second-tone.md 8)
        // takes the player's saved value of that region as its first one, once: while the file holds none of the part's
        // rows (the first load after the update; tones were never released), so the look stays as it was; from then on
        // it is a part like any other. The region's Custom colour stays the same colour and lightness, its preset becomes
        // the part's of the same name or the nearest (the second tone's Pale White, Rose Pink, Dark Black; the coat's
        // Cream White, Dark Black): a converted preset may come out a shade off (the user, 2026-11-04: as close as can
        // be, not critical). The region's rows must be the page's (RegionRows) before ToneRows runs.
        private void CarryOver(string skinId, string region, ConfigEntry<ToneColor> choice, string customKey, string who)
        {
            string cfgSection = choice.Definition.Section;
            if (Page.InFile(cfgSection, choice.Definition.Key)) return;
            if (!regionKeys.TryGetValue((skinId ?? "") + "\n" + region, out var rk)) return; // no rows of the region on the page
            SettingsPage.Row regionChoice = Page.RowOfKey(rk.Section, rk.Choice);
            string was = regionChoice?.Entry?.BoxedValue?.ToString();
            if (string.IsNullOrEmpty(was) || was == "Game") return; // as drawn there, as drawn here
            ToneColor to;
            if (was == "Custom") to = ToneColor.Custom;
            else if (!NearestPart.TryGetValue(was, out to) && !Enum.TryParse(was, out to)) return;
            if (to == ToneColor.Custom && rk.Custom != null)
            {
                SettingsPage.Row fromColour = Page.RowOfKey(rk.Section, rk.Custom), fromLight = Page.RowOfKey(rk.Section, LightnessKey(rk.Custom));
                SettingsPage.Row toColour = Page.RowOfKey(cfgSection, customKey), toLight = Page.RowOfKey(cfgSection, LightnessKey(customKey));
                if (fromColour?.Entry != null && toColour?.Entry != null) toColour.Entry.BoxedValue = fromColour.Entry.BoxedValue;
                if (fromLight?.Entry != null && toLight?.Entry != null) toLight.Entry.BoxedValue = fromLight.Entry.BoxedValue;
            }
            choice.Value = to;
            Plugin.Log.LogInfo($"Settings: {Title}: {who} takes your saved {region} colour ({was}) as its own ({to}): it was that row's part before it was a part of its own.");
        }

        // Whether the mount may pull the cart (0.39.0; the donkey, an unknown or a missing mount: yes, the donkey comes for it).
        public static bool MountPullsCart(string mountId)
        {
            if (string.IsNullOrEmpty(mountId) || mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return true;
            Catalog.Entry k = Catalog.Find(mountId);
            return k == null || k.Cart;
        }

        // Whether any skin of the mount has the colour region (Coat, Dark, Muzzle, Eye, Harness, Outline), so a page can grey
        // the rows of a region the player's colour would never reach.
        public static bool MountHasRegion(string mountId, string region)
        {
            if (string.IsNullOrEmpty(mountId) || mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (MountSkinInfo s in Mounts.Looks.Skins)
            {
                if (!string.Equals(s.Mount, mountId, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string r in s.Regions) if (string.Equals(r, region, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Whether a skin's look has a colour region (its skin.cfg's [Colors] names; the donkey's own art has the API's
        // six): for a PerSkin row's EnableWhen, so Harness greys on a skin without tack and Second tone on one without
        // a second tone. An over skin of the donkey keeps the donkey's regions under it (docs/skins.md), so those count
        // too. A skin nothing provides (its pack not installed): false.
        public static bool SkinHasRegion(string skinId, string region)
        {
            if (string.IsNullOrEmpty(skinId) || skinId.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return MountHasRegion(MountCatalog.DonkeyId, region);
            MountSkinInfo s = Mounts.Looks.Find(skinId);
            if (s == null) return false;
            foreach (string r in s.Regions) if (string.Equals(r, region, StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Over && string.Equals(s.Mount, MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string r in Colors.MountRegionKeys) if (string.Equals(r, region, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // A skin's own tones (skin.cfg [Tone.<id>], MountSkinDef.Tones; docs/mount-colours.md section 7), for a page's colour
        // rows: call it inside PerSkin's body with its skin id and suffix. Per tone, in the skin's order: a choice of the
        // presets (ToneColor; Game default is the skin as drawn) and its custom colour with the lightness (Color), keys
        // Tone.<id><suffix>, CustomTone.<id><suffix> and CustomTone.<id>Lightness<suffix>, the look's MountLook.Tone. The
        // label is the tone's name, translated by the skin's owner; the other words are the API's. A skin without tones
        // gets no rows; one registered after the page gets them with its other PerSkin rows at CatchUp. A choice's default is
        // the look's value when this is called (MountLook.Tone: set it first for a default of your own), as the other rows'.
        public void ToneRows(string section, string skinId, string suffix, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            MountLook look = companion.LookOf(skinId);
            string sfx = suffix ?? "";
            // the mount's dark parts, eyes and outline first: rows of their own where the look names them
            // (docs/second-tone.md 8, docs/mount-colours.md 7.15), the donkey's own look too (it has no SkinDef)
            RegionPartRows(section, skinId, sfx, "Dark", look.Dark);
            RegionPartRows(section, skinId, sfx, "Eye", look.Eye);
            RegionPartRows(section, skinId, sfx, "Outline", look.Outline);
            Looks.SkinDef def = Looks.Find(skinId);
            if (def == null || !def.OwnColours || def.Tones == null) return;
            foreach (SkinTones.ToneDef t in def.Tones)
            {
                if (!SkinTones.HasColours(def, t.Id)) continue; // no colours (all moved elsewhere by a variant, or none given)
                MountTone tone = look.Tone(t.Id);
                Term name = SkinTones.NameTerm(def, t);
                string who = $"{t.Name} ({def.Name ?? def.Id})";
                Word[] words = SkinTones.Choices();
                var english = new string[words.Length];
                for (int i = 0; i < english.Length; i++) english[i] = words[i].English;
                // the English Label (logs, comparisons; drawn through LabelOf) carries the tone's id: a pack's tone named
                // like the page's picture row would otherwise make Add refuse the page (a picture is found by its label)
                string label = $"{t.Name} (tone {t.Id})";
                ConfigEntry<ToneColor> choice = Choice(section, "Tone." + t.Id + sfx, tone.Color, label, $"A colour of the skin's own: {who}. Game default is the skin as drawn.", v => tone.Color = v, english, look: true);
                string customKey = "CustomTone." + t.Id + sfx;
                Color(section, customKey, label + ": custom colour", $"Used when {who} is Custom.", PaletteColor.Gold, v => tone.Custom = v, () => choice.Value == ToneColor.Custom);
                // a part that was a region's before (follows =): the player's value of the region, once
                if (t.Follows != null) CarryOver(skinId, t.Follows, choice, customKey, who);
                // the three rows by their keys (Color's colour and lightness rows wherever it put them): their words the
                // tone's name, in the API's templates for the custom rows, and the API's presets
                string cfgSection = choice.Definition.Section;
                SettingsPage.Row choiceRow = Page.RowOfKey(cfgSection, choice.Definition.Key), customRow = Page.RowOfKey(cfgSection, customKey), lightRow = Page.RowOfKey(cfgSection, LightnessKey(customKey));
                if (choiceRow != null)
                {
                    choiceRow.LabelOf = () => name.Text;
                    choiceRow.ChoiceWords = words;
                    Own(choiceRow).SkinPart = true;
                    Own(choiceRow).ArtTone = t.Id;
                }
                if (customRow != null)
                {
                    customRow.LabelOf = () => UiWords.ToneCustom.Format(name);
                    Own(customRow).SkinPart = true;
                }
                if (lightRow != null)
                {
                    lightRow.LabelOf = () => UiWords.ToneLightness.Format(name);
                    Own(lightRow).SkinPart = true;
                }
                // greyed once the skin, loaded, turns out to draw none of the tone's colours (a variant drawing every
                // frame of its own over its default's tone: the choice would colour nothing; the tenth code review), live
                // until then (nothing is known before it loads); a row's own condition stays
                string skinKey = def.Id, toneId = t.Id;
                Func<bool> draws = () => SkinTones.Draws(skinKey, toneId);
                EnableWhen(section, choice.Definition.Key, draws);
                EnableWhen(section, customKey, draws); // its lightness row with it
            }
        }

        // A Dark or Eye region's rows, where the look names its part (SkinRegions) and has its colours: a choice of the tone
        // presets, its custom colour and lightness, labelled by the name as a tone's are, under the page's keys
        // <region><suffix> and Custom<region><suffix>.
        private void RegionPartRows(string section, string skinId, string sfx, string region, MountTone part)
        {
            Term name = SkinRegions.NameTerm(skinId, region);
            if (name == null || !SkinHasRegion(skinId, region)) return;
            Word[] words = SkinTones.Choices();
            var english = new string[words.Length];
            for (int i = 0; i < english.Length; i++) english[i] = words[i].English;
            string label = $"{name.English} (region {region})"; // the English for the logs; the window draws the name (LabelOf)
            ConfigEntry<ToneColor> choice = Choice(section, region + sfx, part.Color, label, $"A part of the mount: {name.English}. Game default is as drawn.", v => part.Color = v, english, look: true);
            string customKey = "Custom" + region + sfx;
            Color(section, customKey, label + ": custom colour", $"Used when {name.English} is Custom.", PaletteColor.Gold, v => part.Custom = v, () => choice.Value == ToneColor.Custom);
            string cfgSection = choice.Definition.Section;
            SettingsPage.Row choiceRow = Page.RowOfKey(cfgSection, choice.Definition.Key), customRow = Page.RowOfKey(cfgSection, customKey), lightRow = Page.RowOfKey(cfgSection, LightnessKey(customKey));
            if (choiceRow != null)
            {
                choiceRow.LabelOf = () => name.Text;
                choiceRow.ChoiceWords = words;
                Own(choiceRow).ArtRegion = region;
            }
            if (customRow != null) customRow.LabelOf = () => UiWords.ToneCustom.Format(name);
            if (lightRow != null) lightRow.LabelOf = () => UiWords.ToneLightness.Format(name);
        }

        public void PerMount(Action<string, string, Func<bool>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (mountRow == null)
            {
                rows(MountCatalog.DonkeyId, "", null);
                return;
            }
            perMountBuilders.Add(rows); // a mount registered after this call gets its rows at CatchUp
            foreach (MountInfo k in Mounts.Catalog.All) RunPerMount(rows, k.Id);
        }

        // The rows kept per skin (0.41.0; docs/per-skin-looks.md): the body runs once for every skin of every mount with
        // (skinId, mountId, suffix, shown) and binds that skin's rows. The suffix: a mount's first skin (its Skin row's
        // default) keeps the mount's ("" for the donkey's own art, "_<mount id>" for another mount's default: the keys of
        // 0.12 and before, which were per mount), every other skin "_<skin id>" (Coat_donkey/acme-pack.blaze). shown is
        // true while that mount is chosen and its Skin row says that skin: the Mounts panel's colours window shows that
        // set. A skin registered after this call (a pack loaded later) gets its rows at CatchUp. Without MountRows the
        // body runs once for the donkey's own art.
        // The 0.12 carry-over: a player's per-mount values were set while wearing the mount's chosen skin; when that
        // skin isn't the first and none of its rows is in the file while the mount's are, the mount's values move to it
        // (the first skin's rows go to their defaults), once, said in the log.
        public void PerSkin(Action<string, string, string, Func<bool>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (mountRow == null)
            {
                rows(MountLooks.DonkeyId, MountCatalog.DonkeyId, "", null);
                return;
            }
            perSkinBuilders.Add(rows);
            foreach (MountInfo k in Mounts.Catalog.All) RunPerSkinAll(rows, k.Id);
        }

        private readonly List<Action<string, string, string, Func<bool>>> perSkinBuilders = new List<Action<string, string, string, Func<bool>>>();
        private readonly HashSet<string> perSkinDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "<builder index>|<skin id>"
        private readonly Dictionary<Action<string, string, string, Func<bool>>, SettingsPage.Row> lastSkinRowOf = new Dictionary<Action<string, string, string, Func<bool>>, SettingsPage.Row>();
        internal int SeededSkins => seededSkins.Count; // the skins whose rows took the mount's 0.12 values (the self-test)
        private readonly HashSet<string> seededSkins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Every skin of a mount the builder hasn't made rows for yet (its choices, the first one first).
        private int RunPerSkinAll(Action<string, string, string, Func<bool>> rows, string mountId)
        {
            int made = 0;
            int at = perSkinBuilders.IndexOf(rows);
            foreach ((string id, string label) in Looks.SkinChoicesOf(mountId))
            {
                if (!perSkinDone.Add(at + "|" + id)) continue;
                RunPerSkin(rows, id, mountId);
                made++;
            }
            return made;
        }

        private void RunPerSkin(Action<string, string, string, Func<bool>> rows, string skinId, string mountId)
        {
            bool donkey = mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            string first = Looks.FirstSkinOf(mountId);
            bool isFirst = skinId.Equals(first, StringComparison.OrdinalIgnoreCase);
            string mountSfx = donkey ? "" : "_" + mountId;
            string sfx = isFirst ? mountSfx : "_" + skinId;
            Func<bool> isShown = () => string.Equals(mountRow?.Value ?? MountCatalog.DonkeyId, mountId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SkinChosen(mountId), skinId, StringComparison.OrdinalIgnoreCase);
            Page.PendingShown = isShown;
            Page.PendingTag = () => new MountRowInfo { MountId = mountId, SkinId = skinId }; // one object per row
            Page.PendingKeyTails = new[] { "_" + skinId, "_" + mountId }; // the skin's tail first, then the mount's (Texts.TextKeyOf)
            int before = Rows.Count;
            try
            {
                rows(skinId, mountId, sfx, isShown);
            }
            finally
            {
                Page.PendingShown = null;
                Page.PendingTag = null;
                Page.PendingKeyTails = null;
            }
            int made = Rows.Count - before;
            if (made <= 0) return;
            List<SettingsPage.Row> mine = Rows.GetRange(before, made);
            if (catchingUp && lastSkinRowOf.TryGetValue(rows, out SettingsPage.Row last))
            {
                int at = Rows.IndexOf(last);
                if (at >= 0 && at < before - 1)
                {
                    Rows.RemoveRange(before, made);
                    Rows.InsertRange(at + 1, mine);
                }
            }
            lastSkinRowOf[rows] = mine[mine.Count - 1];
            if (!isFirst) SeedFromMount(mine, skinId, mountId, "_" + skinId, mountSfx);
        }

        // The skin a mount's Skin row stands for (its first skin when the row has none): the look worn for it, so a skin not
        // installed of a mount that is gives that mount's own, whose rows the windows show (docs/missing-skin.md). The row
        // itself keeps the choice.
        internal string SkinChosen(string mountId)
        {
            string id = string.IsNullOrEmpty(mountId) ? MountCatalog.DonkeyId : mountId;
            if (skinRows.TryGetValue(id, out ConfigEntry<string> row) && !string.IsNullOrEmpty(row.Value)) return Looks.StandIn(row.Value);
            return Looks.FirstSkinOf(id);
        }

        // The 0.12 carry-over (PerSkin): the skin's rows just made take the mount's values from the file when the file
        // has the mount's and none of the skin's, and the mount's Skin row in the file says this skin; the mount's rows
        // (its first skin's) go back to their defaults, so the first skin doesn't wear this skin's colours.
        private void SeedFromMount(List<SettingsPage.Row> mine, string skinId, string mountId, string skinSfx, string mountSfx)
        {
            if (Config == null || string.IsNullOrEmpty(Config.ConfigFilePath)) return;
            Dictionary<string, Dictionary<string, string>> fileSections = Page.FileSections;
            if (fileSections.Count == 0) return;
            // the file's own Skin row value for the mount, under any number of its section
            string skinKey = "Skin_" + mountId, chosenInFile = null;
            foreach (KeyValuePair<string, Dictionary<string, string>> s in fileSections)
            {
                if (s.Value.TryGetValue(skinKey, out string v)) { chosenInFile = v; break; }
            }
            if (chosenInFile == null || !chosenInFile.Trim().Equals(skinId, StringComparison.OrdinalIgnoreCase)) return;
            var pairs = new List<(SettingsPage.Row Skin, SettingsPage.Row Mount)>();
            foreach (SettingsPage.Row r in mine)
            {
                if (r.Entry == null) continue;
                string key = r.Entry.Definition.Key;
                if (!key.EndsWith(skinSfx, StringComparison.OrdinalIgnoreCase)) continue;
                if (fileSections.TryGetValue(r.Entry.Definition.Section, out Dictionary<string, string> own) && own.ContainsKey(key)) return; // the skin has values of its own: nothing to seed
                string mountKey = key.Substring(0, key.Length - skinSfx.Length) + mountSfx;
                SettingsPage.Row m = Rows.Find(o => o.Entry != null && o.Entry.Definition.Section == r.Entry.Definition.Section && o.Entry.Definition.Key == mountKey);
                if (m == null) continue;
                if (!fileSections.TryGetValue(m.Entry.Definition.Section, out Dictionary<string, string> ms) || !ms.ContainsKey(mountKey)) continue; // the mount's row isn't in the file either
                pairs.Add((r, m));
            }
            if (pairs.Count == 0) return;
            foreach ((SettingsPage.Row Skin, SettingsPage.Row Mount) pr in pairs)
            {
                pr.Skin.Entry.BoxedValue = pr.Mount.Entry.BoxedValue;
                pr.Mount.Entry.BoxedValue = pr.Mount.Entry.DefaultValue;
            }
            bool first = seededSkins.Add(skinId);
            string name = Mounts.Looks.Find(skinId)?.Name ?? skinId;
            Plugin.Log.LogInfo(first
                ? $"Settings: {Title}: {name}'s look took {pairs.Count} value(s) of {mountId}'s (0.12 kept the colours per mount, and {name} was the skin worn); {mountId}'s first skin is at its defaults for them."
                : $"Settings: {Title}: {name}'s look took {pairs.Count} more value(s) of {mountId}'s.");
        }

        // Where each builder's rows end (its last row so far): a mount caught up later gets its rows put right after them,
        // under the same heading. Appended at the end, a late mount's colour rows sat under the page's last heading (the
        // mount from its own pack, registered after the page was built: its Coat and Harness under "Cart colours", 2026-10-26).
        private readonly Dictionary<Action<string, string, Func<bool>>, SettingsPage.Row> lastRowOf = new Dictionary<Action<string, string, Func<bool>>, SettingsPage.Row>();

        private void RunPerMount(Action<string, string, Func<bool>> rows, string id)
        {
            bool donkey = id.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            Func<bool> isShown = () => string.Equals(mountRow?.Value ?? MountCatalog.DonkeyId, id, StringComparison.OrdinalIgnoreCase);
            Page.PendingShown = isShown;
            Page.PendingTag = () => new MountRowInfo { MountId = id };
            Page.PendingKeyTails = new[] { "_" + id };
            int before = Rows.Count;
            try
            {
                rows(id, donkey ? "" : "_" + id, isShown);
            }
            finally
            {
                Page.PendingShown = null;
                Page.PendingTag = null;
                Page.PendingKeyTails = null;
            }
            int made = Rows.Count - before;
            if (made <= 0) return;
            List<SettingsPage.Row> mine = Rows.GetRange(before, made);
            if (catchingUp && lastRowOf.TryGetValue(rows, out SettingsPage.Row last))
            {
                int at = Rows.IndexOf(last);
                if (at >= 0 && at < before - 1)
                {
                    Rows.RemoveRange(before, made);
                    Rows.InsertRange(at + 1, mine);
                }
            }
            lastRowOf[rows] = mine[mine.Count - 1];
        }
    }
}
