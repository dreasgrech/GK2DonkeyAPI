using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;

namespace GK2DonkeyAPI.Api
{
    // A player mod's settings, defined once and shown where the player looks (Mounts.SettingsPage):
    // - with GK2 Mod Framework installed: in its Mods menu (the mod's own entry, its sections, every mount of row);
    // - without it: in the game's own Settings window, on pages of the mod's own after the game's settings, made of
    //   copies of the game's own rows (SettingsRows.cs). Keys and text there show read-only; they're set in the cfg.
    // Each row binds a BepInEx entry in the mod's own cfg, runs its apply action now and on every change (from the menu,
    // the Settings window or the cfg), and shows under its section. Call Publish once, after the rows, from the mod's
    // Awake; the page reaches the menus the frame after (Show), when every plugin's Awake has run, so a mount or skin
    // another mod registered in its Awake is in the lists whatever order BepInEx loaded the plugins in (it orders by
    // dependencies only, and a mount mod can't ask to load before the mod that owns the page; docs/mounts-as-mods.md 4).
    // Sections keep the order they're first used in: the cfg section is numbered ("1. Keys", "2. Behaviour"), because
    // the framework's Mods menu sorts sections by name and has no other way to order them. The game's Settings window
    // shows the plain names.
    // Looks: Preview shows a picture of the mod's donkey in its current colours next to the settings (LookPreview.cs);
    // the look rows (look: true; the Mount and Skin rows, the colours) are not in the menus at all: they
    // live in the Mounts panel (MountsPanel.cs: the mounts listed, the chosen one drawn with its words and its rows),
    // which PanelButton opens from the page and OpenPanel from a mod's key. Both are extras: if anything about them
    // fails, they don't show, and the settings work through the cfg as before.
    public sealed class MountSettingsPage
    {
        internal enum RowKind { Toggle, Choice, Steps, Key, Text, Info, Button, Preview, Note, List }

        internal sealed class Row
        {
            internal RowKind RowKind;
            internal ConfigEntryBase Entry;
            internal string Section, Label, Description;
            internal string ShownSection; // the plain name, without the number
            internal string[] ChoiceLabels;
            internal Array ChoiceValues;
            internal Func<IReadOnlyList<(string Id, string Label)>> Choices; // RowKind.List: read when the row is first shown
            internal int Min, Max, Step;
            internal Func<int, string> Format;
            internal Func<string> InfoValue;
            internal Func<bool> Visible;     // shown at all (Shown and Enabled together, for the menus)
            internal Func<bool> Shown;       // a row of a set kept per mount (PerMount, the Skin rows): the chosen mount's shows, the others don't
            internal Func<bool> Enabled;     // the mod's own condition (a custom colour with its preset on Custom): the Mounts panel leaves the row out while false (a custom colour shows with its preset), the Settings window greys it
            internal bool IsLook;           // the Mounts panel's, not the menus' (InMenu); a Note with it is a heading there (Heading)
            internal Action OnClick;         // RowKind.Button
            internal string ButtonText;      // RowKind.Button: the word on it ("Open", "Go")
            internal bool WithCart;          // RowKind.Preview: the donkey with its cart, or alone
            internal string MountId;         // a PerMount or PerSkin row's mount (null: a row of no one mount)
            internal string SkinId;          // a PerSkin row's skin (null: a row of no one skin)
        }

        internal readonly List<Row> Rows = new List<Row>();
        internal static readonly List<MountSettingsPage> All = new List<MountSettingsPage>(); // every page made this session (the upgrade self-test)
        private readonly List<string> sections = new List<string>();
        internal MountCompanion PreviewOf;
        internal Action RefreshMenu; // the Mods menu re-reads the rows' visibility (set by FrameworkLink)

        // "Keys" -> "1. Keys", numbered in the order sections are first used.
        private string Sec(string name)
        {
            int at = sections.IndexOf(name);
            if (at < 0)
            {
                sections.Add(name);
                at = sections.Count - 1;
            }
            return (at + 1) + ". " + name;
        }

        // Every row's cfg entry comes through here. A section's number is its place on the page, so when a mod moves a
        // section (a mod put Mount right after Keys: Donkey went from "3. Donkey" to "5. Donkey"), a cfg from
        // before has the values under the old number, and BepInEx would bind the defaults and leave the old lines as
        // orphans: every player's settings reset on an update. So a key the file doesn't have under its section takes
        // the value the file has for it under the same name with another number, or under a place the mod says the
        // key moved from (MovedKey). The file's text is read once per page. The old copy is then dropped from
        // BepInEx's orphans, so the file holds each value in one place: BepInEx writes orphans back on every save, and
        // a stale copy under a third number would otherwise be found first the next time the section moves.
        private ConfigEntry<T> Bind<T>(string cfgSection, string key, T value, string description) => Bind(cfgSection, key, value, new ConfigDescription(description));

        private ConfigEntry<T> Bind<T>(string cfgSection, string key, T value, ConfigDescription description)
        {
            ConfigEntry<T> e = Config.Bind(cfgSection, key, value, description);
            string moved = MovedValue(cfgSection, key, out string from, out string fromKey);
            if (moved != null)
            {
                try
                {
                    e.SetSerializedValue(moved);
                    bool keyMove = fromKey != key || PlainName(from) != PlainName(cfgSection); // MovedKey, not a renumbered section
                    if (movedNoted.Add(keyMove ? from + "|" + cfgSection + "|" + key : from + "|" + cfgSection)) // once per moved section, once per moved key
                    {
                        Plugin.Log.LogInfo(keyMove
                            ? $"Settings: {Title}: [{from}] {fromKey} is [{cfgSection}] {key} now; the value carried over."
                            : $"Settings: {Title}: the section \"{from}\" is \"{cfgSection}\" now; its values carried over.");
                    }
                }
                catch (Exception x)
                {
                    Plugin.Log.LogWarning($"Settings: {Title}: [{from}] {fromKey} = {moved} can't be read as {typeof(T).Name} ({x.Message}); the default applies.");
                }
            }
            DropOldCopies(cfgSection, key);
            return e;
        }

        // A key that moved to another section (or name) on the page: the first bind of (section, key) with nothing in
        // the cfg takes the value the cfg has as (oldSection, oldKey), whatever number oldSection had. Call it before
        // the row. Sections that only moved on the page keep their values without this.
        // A key that moved more than once names each old place with a call of its own (0.36.0: one call named one).
        public void MovedKey(string section, string key, string oldSection, string oldKey = null)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(oldSection)) throw new ArgumentException("section, key and oldSection are needed.");
            string at = section + "\n" + key;
            if (!movedKeys.TryGetValue(at, out List<(string Section, string Key)> places)) movedKeys[at] = places = new List<(string, string)>();
            if (!places.Contains((oldSection, oldKey ?? key))) places.Add((oldSection, oldKey ?? key));
        }

        private readonly Dictionary<string, List<(string Section, string Key)>> movedKeys = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        // A setting the mod no longer has, on purpose (0.36.0; a mod's Riding speed and Cart speed, now each mount's
        // own): its line leaves the player's file (under any number of the section) at Publish, and the upgrade
        // self-test counts it as retired, not as a value lost. Call it once per key, any time before Publish.
        public void RetiredKey(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) throw new ArgumentException("section and key are needed.");
            retiredKeys.Add(PlainName(section) + "\n" + key);
        }

        private readonly HashSet<string> retiredKeys = new HashSet<string>(StringComparer.Ordinal);
        internal bool IsRetired(string cfgSection, string key) => retiredKeys.Contains(PlainName(cfgSection) + "\n" + key);

        // Every retired line of the file out of BepInEx's orphans, so the save at Publish leaves them out.
        private void DropRetired()
        {
            if (retiredKeys.Count == 0) return;
            if (fileSections == null) fileSections = ReadSections(Config.ConfigFilePath);
            foreach (KeyValuePair<string, Dictionary<string, string>> s in fileSections)
            {
                foreach (string key in s.Value.Keys)
                {
                    if (!IsRetired(s.Key, key)) continue;
                    Dictionary<ConfigDefinition, string> orphans = OrphansOf(Config);
                    if (orphans == null) return;
                    if (orphans.Remove(new ConfigDefinition(s.Key, key))) retiredDropped++;
                    else dropsFailed++;
                }
            }
        }
        private Dictionary<string, Dictionary<string, string>> fileSections; // the cfg file's sections and their keys, raw
        private readonly HashSet<string> movedNoted = new HashSet<string>();

        // Every section in the file that may hold a copy of (cfgSection, key): the same plain name under another
        // number, and the place MovedKey named, under any number. (section, key) pairs.
        private IEnumerable<(string Section, string Key)> OldPlaces(string cfgSection, string key) => OldPlaces(cfgSection, key, fileSections);

        private IEnumerable<(string Section, string Key)> OldPlaces(string cfgSection, string key, Dictionary<string, Dictionary<string, string>> file)
        {
            string plain = PlainName(cfgSection);
            movedKeys.TryGetValue(PlainName(cfgSection) + "\n" + key, out List<(string Section, string Key)> aliases);
            string unprefixed = UnprefixedMountKey(key);
            if (unprefixed != null && file.ContainsKey(cfgSection)) yield return (cfgSection, unprefixed);
            foreach (string s in file.Keys)
            {
                if (s == cfgSection) continue;
                string p = PlainName(s);
                if (p == plain)
                {
                    yield return (s, key);
                    if (unprefixed != null) yield return (s, unprefixed);
                }
                else if (aliases != null)
                {
                    foreach ((string Section, string Key) alias in aliases)
                    {
                        if (p == alias.Section) yield return (s, alias.Key);
                    }
                }
            }
        }

        // A per-mount key with its mount's id from before the prefixes ("Coat_acme.crab" -> "Coat_crab": a dev's
        // cfg of 2026-10-24, mounts never released): its old name, so the value carries over; null for any other key.
        internal static string UnprefixedMountKey(string key)
        {
            int u = key.IndexOf('_');
            if (u <= 0) return null;
            int dot = key.IndexOf('.', u + 1);
            return dot > u + 1 && dot < key.Length - 1 ? key.Substring(0, u + 1) + key.Substring(dot + 1) : null;
        }

        // The file's value for key from an old place (OldPlaces) when the file has none under cfgSection itself; null
        // otherwise. Several old copies: the section with the most keys wins (a newer version binds at least as many),
        // then the highest number.
        private string MovedValue(string cfgSection, string key, out string from, out string fromKey)
        {
            from = null;
            fromKey = null;
            if (fileSections == null) fileSections = ReadSections(Config.ConfigFilePath);
            if (fileSections.Count == 0) return null;
            if (fileSections.TryGetValue(cfgSection, out Dictionary<string, string> own) && own.ContainsKey(key)) return null;
            return BestOldValue(cfgSection, key, fileSections, out from, out fromKey);
        }

        private string BestOldValue(string cfgSection, string key, Dictionary<string, Dictionary<string, string>> file, out string from, out string fromKey)
        {
            from = null;
            fromKey = null;
            string best = null;
            int bestKeys = -1, bestNumber = -1;
            foreach ((string Section, string Key) place in OldPlaces(cfgSection, key, file))
            {
                Dictionary<string, string> s = file[place.Section];
                if (!s.TryGetValue(place.Key, out string raw)) continue;
                int number = SectionNumber(place.Section);
                if (s.Count > bestKeys || (s.Count == bestKeys && number > bestNumber))
                {
                    best = raw;
                    bestKeys = s.Count;
                    bestNumber = number;
                    from = place.Section;
                    fromKey = place.Key;
                }
            }
            return best;
        }

        // Where each row's value would come from in a cfg file of another version (ReadSections of it): the file's own
        // line for the row, else the best old place (a renumbered section, a MovedKey), else nothing (the row is new;
        // its default applies). And the file's lines no row claims: a player's value this page would lose. The upgrade
        // self-test runs it over the released files in tests/fixtures/released (docs/compatibility.md); nothing is bound
        // or written.
        internal sealed class UpgradeClaim
        {
            internal Row Row;
            internal string Section, Key, Raw; // null: the file has no value for the row
            internal bool Moved => Raw != null && Section != Row.Entry.Definition.Section;
        }

        internal List<UpgradeClaim> Upgrade(Dictionary<string, Dictionary<string, string>> file, out List<(string Section, string Key, string Raw)> unclaimed)
        {
            var claims = new List<UpgradeClaim>();
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (Row r in Rows)
            {
                if (r.Entry == null) continue;
                string sec = r.Entry.Definition.Section, key = r.Entry.Definition.Key;
                var c = new UpgradeClaim { Row = r };
                if (file.TryGetValue(sec, out Dictionary<string, string> own) && own.TryGetValue(key, out string raw))
                {
                    c.Section = sec;
                    c.Key = key;
                    c.Raw = raw;
                }
                else
                {
                    c.Raw = BestOldValue(sec, key, file, out c.Section, out c.Key);
                }
                if (c.Raw != null) claimed.Add(c.Section + "\n" + c.Key);
                claims.Add(c);
            }
            unclaimed = new List<(string, string, string)>();
            foreach (KeyValuePair<string, Dictionary<string, string>> s in file)
            {
                foreach (KeyValuePair<string, string> kv in s.Value)
                {
                    if (!claimed.Contains(s.Key + "\n" + kv.Key) && !IsRetired(s.Key, kv.Key)) unclaimed.Add((s.Key, kv.Key, kv.Value)); // a retired line is left out on purpose
                }
            }
            return claims;
        }

        // Every old copy of (cfgSection, key) out of BepInEx's orphans (the entries it read from the file and nothing
        // bound), so the next save drops those lines. The orphans are BepInEx's own (ConfigFile.OrphanedEntries, not
        // public); without them the file merely keeps the old lines.
        private void DropOldCopies(string cfgSection, string key)
        {
            if (fileSections == null || fileSections.Count == 0) return;
            foreach ((string Section, string Key) place in OldPlaces(cfgSection, key))
            {
                if (!fileSections[place.Section].ContainsKey(place.Key)) continue;
                System.Collections.Generic.Dictionary<ConfigDefinition, string> orphans = OrphansOf(Config);
                if (orphans == null) return;
                if (orphans.Remove(new ConfigDefinition(place.Section, place.Key))) droppedOldCopies++;
                else dropsFailed++;
            }
        }

        private static System.Reflection.PropertyInfo orphansProperty;
        private static bool orphansLooked;

        internal static Dictionary<ConfigDefinition, string> OrphansOf(ConfigFile cfg)
        {
            if (!orphansLooked)
            {
                orphansLooked = true;
                orphansProperty = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (orphansProperty == null || orphansProperty.PropertyType != typeof(Dictionary<ConfigDefinition, string>))
                {
                    orphansProperty = null;
                    Plugin.Log.LogWarning("Settings: this BepInEx has no ConfigFile.OrphanedEntries dictionary; old copies of moved settings stay in the cfg files (harmless).");
                }
            }
            return orphansProperty != null ? orphansProperty.GetValue(cfg) as Dictionary<ConfigDefinition, string> : null;
        }

        // "5. Donkey" -> 5; -1 without a number.
        internal static int SectionNumber(string section)
        {
            int dot = section.IndexOf(". ", StringComparison.Ordinal);
            if (dot <= 0) return -1;
            int n = 0;
            for (int i = 0; i < dot; i++)
            {
                if (!char.IsDigit(section[i])) return -1;
                n = n * 10 + (section[i] - '0');
            }
            return n;
        }

        // "5. Donkey" -> "Donkey"; a section without a number is its own name.
        internal static string PlainName(string section)
        {
            int dot = section.IndexOf(". ", StringComparison.Ordinal);
            if (dot <= 0) return section;
            for (int i = 0; i < dot; i++) if (!char.IsDigit(section[i])) return section;
            return section.Substring(dot + 2);
        }

        // The cfg as BepInEx writes it: "[Section]" lines, "Key = Value" lines, "#" comments; the raw values as text.
        internal static Dictionary<string, Dictionary<string, string>> ReadSections(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return result;
                Dictionary<string, string> section = null;
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        string name = line.Substring(1, line.Length - 2).Trim();
                        if (!result.TryGetValue(name, out section)) result[name] = section = new Dictionary<string, string>(StringComparer.Ordinal);
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (section == null || eq <= 0) continue;
                    section[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception x)
            {
                Plugin.Log.LogWarning($"Settings: {path} couldn't be read for moved sections ({x.Message}).");
            }
            return result;
        }

        internal readonly string OwnerId, Title, Author, Description;
        internal readonly ConfigFile Config;
        internal readonly Func<string> StatusValue;
        private bool published;

        internal MountSettingsPage(string ownerId, ConfigFile config, string title, string author, string description, Func<string> status)
        {
            OwnerId = ownerId;
            Config = config;
            Title = title;
            Author = author;
            Description = description;
            StatusValue = status ?? (() => Mounts.StatusText);
            KeyActions.Track(ownerId);
            All.Add(this);
            // One write of the cfg at Publish instead of one per row: BepInEx saves inside every Bind by default,
            // which also wrote the file before Bind below could drop an old copy of a moved setting, so the last row's
            // old line stayed in the file (seen 2026-10-18 with a 0.9.1 cfg: "[5. Sound] Volume" next to "[7. Sound]").
            saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
        }

        private readonly bool saveOnSet; // the mod's own setting, put back at Publish
        private int droppedOldCopies, dropsFailed, retiredDropped;

        // After the last row: the mod's save-on-set back as it was, and the one save that writes every value and
        // leaves out the old copies dropped while binding. Publish calls it; a page never published keeps its
        // values in memory only, as "Add every row before Publish" says.
        internal void FinishBinding()
        {
            if (Config.SaveOnConfigSet != saveOnSet) Config.SaveOnConfigSet = saveOnSet;
            DropRetired();
            if (saveOnSet || droppedOldCopies > 0 || retiredDropped > 0) Config.Save();
            if (retiredDropped > 0) Plugin.Log.LogInfo($"Settings: {Title}: {retiredDropped} line(s) of settings it no longer has left out of the file.");
            if (droppedOldCopies > 0) Plugin.Log.LogInfo($"Settings: {Title}: {droppedOldCopies} old line(s) of moved settings left out of the file.");
            if (dropsFailed > 0) Plugin.Log.LogWarning($"Settings: {Title}: {dropsFailed} old line(s) of moved settings could not be dropped from BepInEx's orphans; they stay in the file (harmless) and this is worth a look.");
        }

        internal string Version
        {
            get
            {
                return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(OwnerId, out PluginInfo p) && p?.Metadata != null
                    ? p.Metadata.Version.ToString()
                    : "0.0.0";
            }
        }

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
            pendingShown = () => string.Equals(mountRow?.Value, id, StringComparison.OrdinalIgnoreCase); // the chosen mount's Skin row shows (Row.Shown)
            try
            {
                skinRows[id] = List(mountSection, "Skin_" + id, first, () => Looks.SkinChoicesOf(id), "Skin",
                    $"How your {k.Name.ToLowerInvariant()} looks.",
                    v => ApplyMountRows(), look: true);
            }
            finally
            {
                pendingShown = null;
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
            if (mountRow == null || shown) return 0;
            int added = 0;
            catchingUp = true;
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
            }
            if (added > 0) ApplyMountRows(); // a cfg holding a late mount with its second skin: the companion wears that skin, not the default (the Skin row exists now)
            return added;
        }

        internal bool HasSkinRow(string mountId) => skinRows.ContainsKey(mountId);
        internal Mount.Instance MountPaper => mountCompanion?.Paper; // the panel's picture when the page has no Preview() row
        internal MountResult UseMount(MountStyle style) => mountCompanion != null ? mountCompanion.UseFromPanel(style) : MountResult.Refused("No mount rows on this page."); // the panel's Ride and Drive buttons
        internal bool IsMountRow(Row r) => r != null && r.Entry != null && ReferenceEquals(r.Entry, mountRow); // the Mount dropdown: the panel's list is the picker, the row stays out of its rows

        // Whether a row is the menus' (the Mods menu, the game's Settings window, the menu labels) or the Mounts panel's:
        // a look row is the panel's; a heading (Heading) is the panel's only when the rows after it in its section are
        // look rows, so a mod's heading over plain rows stays in the menus.
        internal bool InMenu(Row r)
        {
            if (!r.IsLook) return true;
            if (r.RowKind != RowKind.Note) return false;
            int at = Rows.IndexOf(r);
            for (int i = at + 1; i < Rows.Count; i++)
            {
                Row n = Rows[i];
                if (n.Section != r.Section) continue;
                if (n.RowKind == RowKind.Note) break; // the next heading: nothing of this one's
                return !n.IsLook;
            }
            return false; // a heading with nothing after it: not worth a menu line
        }
        internal bool HasMountRow => mountRow != null;
        internal bool Shown => shown;

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
                Steps(section, "Foals" + sfx, l.FoalCount, 0, MountCompanion.MaxFoals, 1, "Babies", "How many little ones follow your mount around.", v => l.FoalCount = v, v => v == 0 ? "None" : v.ToString(), look: true, visible: babies);
                Toggle(section, "FoalsFollow" + sfx, l.FoalsFollowOnFoot, "Babies follow you on foot", "When you get off, the babies follow you instead of staying with your mount.", v => l.FoalsFollowOnFoot = v, babies, look: true);
                Toggle(section, "FoalsIndoors" + sfx, true, "Babies come indoors", "The babies follow you into buildings and back out.", v => l.FoalsComeIndoors = v, babies, look: true);
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
        // look: the Mounts panel's row (not the menus'), as Choice's.
        public ConfigEntry<bool> Toggle(string section, string key, bool value, string label, string description, Action<bool> apply = null, Func<bool> visible = null, bool look = false)
        {
            string cfgSection = Sec(section);
            ConfigEntry<bool> e = Bind(cfgSection, key, value, description);
            Add(new Row { RowKind = RowKind.Toggle, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Visible = visible, IsLook = look });
            Wire(e, apply);
            return e;
        }

        // One of an enum's values; labels (one per value, in order) are what players see, else the value names.
        // look: the Mounts panel's row (PanelButton), not the menus'.
        public ConfigEntry<T> Choice<T>(string section, string key, T value, string label, string description, Action<T> apply = null, string[] labels = null, bool look = false, Func<bool> visible = null) where T : struct, Enum
        {
            string cfgSection = Sec(section);
            ConfigEntry<T> e = Bind(cfgSection, key, value, description);
            Array values = Enum.GetValues(typeof(T));
            string[] shown = new string[values.Length];
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = labels != null && i < labels.Length ? labels[i] : values.GetValue(i).ToString();
            }
            Add(new Row { RowKind = RowKind.Choice, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, ChoiceLabels = shown, ChoiceValues = values, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // One of a list of ids read when the row is first shown (the skins installed, say): choices gives (id, label)
        // pairs; the id is what the cfg holds, from every menu alike. A stored id that isn't in the list is shown by
        // a name made from the id, "Blaze (not installed)" (MountsPanel.NameFromId; the id itself showed until
        // 2026-10-27), and left alone, so it returns with what provides it. The list is read once per
        // session, when the page is shown (the frame after Publish): anything registered after that appears after a
        // restart. look: the Mounts panel's row, not the menus'.
        public ConfigEntry<string> List(string section, string key, string value, Func<IReadOnlyList<(string Id, string Label)>> choices, string label, string description, Action<string> apply = null, bool look = false, Func<bool> visible = null)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            string cfgSection = Sec(section);
            ConfigEntry<string> e = Bind(cfgSection, key, value, description);
            // an empty id can't be a dropdown choice (FillList drops it): a cfg holding "" takes the row's default
            if (string.IsNullOrEmpty(e.Value) && !string.IsNullOrEmpty(value)) e.Value = value;
            Add(new Row { RowKind = RowKind.List, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Choices = choices, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A List row's labels and ids, filled once from its choices (plus the stored id if it isn't among them), so the
        // Mods menu's dropdown, its labels and the Settings window agree for the session.
        internal static void FillList(Row r)
        {
            if (r.RowKind != RowKind.List || r.ChoiceLabels != null) return;
            var ids = new List<string>();
            var labels = new List<string>();
            try
            {
                foreach ((string id, string label) in r.Choices() ?? Array.Empty<(string, string)>())
                {
                    if (string.IsNullOrEmpty(id) || ids.Contains(id)) continue;
                    ids.Add(id);
                    labels.Add(string.IsNullOrEmpty(label) ? id : label);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Settings: the choices of {r.Section}.{r.Entry.Definition.Key} could not be read: {ex.Message}");
            }
            string now = r.Entry.BoxedValue as string ?? "";
            if (now.Length > 0 && !ids.Contains(now))
            {
                ids.Add(now);
                labels.Add(MountsPanel.NameFromId(now) + (Looks.Find(now)?.Unavailable != null ? " (set aside)" : " (not installed)"));
            }
            if (ids.Count == 0)
            {
                ids.Add(now);
                labels.Add(now.Length > 0 ? now : "(none)");
            }
            r.ChoiceValues = ids.ToArray();
            r.ChoiceLabels = labels.ToArray();
        }

        // A whole number from min to max in steps; format turns it into the shown text (e.g. "100%").
        public ConfigEntry<int> Steps(string section, string key, int value, int min, int max, int step, string label, string description, Action<int> apply = null, Func<int, string> format = null, bool look = false, Func<bool> visible = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<int> e = Bind(cfgSection, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
            Add(new Row { RowKind = RowKind.Steps, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Min = min, Max = max, Step = Math.Max(1, step), Format = format, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A key (with modifiers). Read-only in the game's Settings window.
        // pressed: called when the player presses the key (its modifiers held; other keys held, like walking, don't
        // matter), while the API is on. The API polls it: the mod needs no Update of its own. Not while the Mounts panel
        // or its colours window is open (0.36.0), except a key whose pressed is OpenPanel (it closes the panel).
        // Plain Backspace means no key: set from either menu or the cfg, it's cleared at once (GK2 Mod Framework's key
        // capture has no way to clear a key and takes Backspace as one; the game's Settings window does the same on
        // its own). Publish adds a row saying so before the page's first key.
        public ConfigEntry<KeyboardShortcut> Key(string section, string key, KeyboardShortcut value, string label, string description, Action<KeyboardShortcut> apply = null, Action pressed = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<KeyboardShortcut> e = Bind(cfgSection, key, value, description);
            Add(new Row { RowKind = RowKind.Key, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description });
            ClearIfBackspace(e);
            e.SettingChanged += (_, __) => ClearIfBackspace(e); // before apply: apply sees the cleared key
            Wire(e, apply);
            if (pressed != null)
            {
                KeyActions.Add(OwnerId, e, pressed, overPanel: pressed == (Action)OpenPanel); // the rest wait while the panel is open
            }
            return e;
        }

        // The signature from before look (added in 0.40.0), kept for mods built against it (every parameter required:
        // a call is never ambiguous between the two).
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply, Func<bool> visible)
            => Text(section, key, value, label, description, apply, visible, look: false);

        // Free text. Read-only in the game's Settings window. visible: shown only while it says so.
        // look: the Mounts panel's row (its own text box there; the menus show none).
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply = null, Func<bool> visible = null, bool look = false)
        {
            string cfgSection = Sec(section);
            ConfigEntry<string> e = Bind(cfgSection, key, value, description);
            Add(new Row { RowKind = RowKind.Text, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Visible = visible, IsLook = look });
            Wire(e, apply);
            return e;
        }

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
            if (movedKeys.TryGetValue(section + "\n" + key, out List<(string Section, string Key)> froms))
            {
                foreach ((string Section, string Key) from in froms.ToArray()) MovedKey(section, lightnessKey, from.Section, LightnessKey(from.Key));
            }
            lightness = Steps(section, lightnessKey, 100, 40, 160, 10, label + ": lightness", "100% is the colour itself; lower is darker, higher is lighter.", _ => Combine(), n => n + "%", look: true, visible: visible);
            Combine();
        }

        // The lightness key of a Color row's key: "CustomCoatLightness", and with a per-mount suffix
        // "CustomCoatLightness_crab".
        internal static string LightnessKey(string key)
        {
            int suffix = key.IndexOf('_');
            return suffix < 0 ? key + "Lightness" : key.Substring(0, suffix) + "Lightness" + key.Substring(suffix);
        }

        // The read-only "Status" row: the page's status (Mounts.StatusText unless the page was given its own).
        public void Status(string section, string label = "Status", string description = "What the mod is doing right now.")
        {
            Info(section, label, () => StatusValue(), description);
        }

        // A read-only line, e.g. what the mod is doing.
        public void Info(string section, string label, Func<string> value, string description = "")
        {
            Add(new Row { RowKind = RowKind.Info, Section = Sec(section), ShownSection = section, Label = label, Description = description, InfoValue = value });
        }

        // A heading inside a section: a line with the label and no value, before the rows it names ("Mount colours",
        // "Cart colours" in a Colours section). In the Mounts panel the look rows after it, in its section, stand under
        // this heading in a column of their own instead of under the section's name (MountsPanel; for one, the mount's
        // colours left, the cart's right). Not a picture row's label (Add).
        public void Heading(string section, string label, string description = "")
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("label is needed.");
            Add(new Row { RowKind = RowKind.Note, Section = Sec(section), ShownSection = section, Label = label, Description = description, IsLook = true });
        }

        // A picture of the companion's donkey and cart in their current colours, walking on the spot: a row of the page,
        // here in this section, in the order called (LookPreview); call it once per section that should show it (e.g.
        // at the top of each section with look rows, so it's in view from each). Also above the Mods window, where it
        // stays in view whatever row is scrolled to, when there's room there (1440p and up, not 1080p).
        // label: the row's name; cart: the row's picture is the donkey with its cart, or the donkey alone (above the
        // window it's always with the cart). Above the game's Settings window without the Mods menu; the Mounts panel
        // too. It needs no donkey in the game: drawn from the game's own files in the companion's colours, so it shows
        // in the main menu and indoors.
        public void Preview(string section, MountCompanion companion, string label = "Your mount", bool cart = true)
        {
            PreviewOf = companion;
            Add(new Row
            {
                RowKind = RowKind.Preview, Section = Sec(section), ShownSection = section, Label = label, Description = PreviewRoom,
                Visible = () => LookPreview.RowsShown, WithCart = cart,
            });
        }

        // The rows are found on screen by their labels (LookPreview), and the Mods menu sizes a row by its description
        // (only line breaks count as empty: the minimum height), so the picture's room is eight lines of one fully
        // transparent dot each (a zero-width space didn't count).
        internal bool HasPreviewRows => Rows.Exists(r => r.RowKind == RowKind.Preview);

        internal Row PreviewRowLabelled(string text) => Rows.Find(r => r.RowKind == RowKind.Preview && r.Label == text);
        internal const string PreviewRoom = "<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>";

        // A button that closes the menu and opens the Mounts panel over the game: every mount listed, the chosen one
        // drawn with its description, what it does and its abilities, and the page's look rows for it (its Skin, the
        // colours), with Done (Escape too; both keep what was chosen). Only in a game; in the main menu it says so.
        public void PanelButton(string section, string label, string description)
        {
            Add(new Row { RowKind = RowKind.Button, Section = Sec(section), ShownSection = section, Label = label, Description = description, ButtonText = "Open", OnClick = () => MountsPanel.Open(this, toggle: false) });
        }

        // A button of the mod's own (0.36.0): onClick runs when it is pressed, in the game's Settings window and in the
        // Mods menu (there only in a game). buttonText: the word on it. enabled: the Settings window greys it and the Mods
        // menu leaves it out while false. No cfg key. A handler that throws is logged.
        public void Button(string section, string label, string description, Action onClick, string buttonText = "Go", Func<bool> enabled = null)
        {
            if (onClick == null) throw new ArgumentNullException(nameof(onClick));
            Add(new Row { RowKind = RowKind.Button, Section = Sec(section), ShownSection = section, Label = label, Description = description, ButtonText = string.IsNullOrEmpty(buttonText) ? "Go" : buttonText, OnClick = onClick, Enabled = enabled });
        }

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

        // The mod is done adding rows. The page is shown the frame after (Show): in the framework's Mods menu when it's
        // installed, else in the game's Settings window. The frame between is what lets every plugin's Awake run first
        // (the header's note): the mounts registered meanwhile get their rows (CatchUp) and every list is read then.
        public void Publish()
        {
            if (published)
            {
                return;
            }
            published = true;
            Plugin.NextFrame(Show);
        }

        private bool shown, catchingUp;

        // A page has been shown: its lists are read (the window for registering mounts and skins is over, Catalog.Late).
        internal static bool AnyShown { get; private set; }

        private void Show()
        {
            if (shown)
            {
                return;
            }
            int late = CatchUp();
            if (late > 0) Plugin.Log.LogInfo($"Settings: {Title}: {late} mount(s) registered after the page was built (a mod loaded after {OwnerId}) got their rows before the page was shown.");
            shown = true;
            AnyShown = true;
            AddKeyHelp();
            FinishBinding();
            GuardCallbacks();
            LookPreview.Watch(this);
            if (FrameworkLink.TryRegister(this))
            {
                MenuLabels.Add(this); // the choices' labels over the framework's plain names
                Plugin.Log.LogInfo($"Settings: {Title} is in GK2 Mod Framework's Mods menu ({Rows.Count} rows).");
                return;
            }
            SettingsRows.Add(this);
        }

        // The mod's own functions on its rows (shown, enabled, an info line, a value's words) run in shared windows: the
        // game's Settings, the Mods menu, the Mounts panel with every mod's rows. One that throws would close the panel
        // or stop the page for every mod (docs/multi-mod-loading.md 3): each is wrapped once, a throw logged once with
        // the page's mod and the row, and the row then shown, enabled, "-" or the plain number.
        private void GuardCallbacks()
        {
            foreach (Row r in Rows)
            {
                string where = $"{OwnerId}'s row '{r.Label}'";
                r.Visible = Guard(r.Visible, true, where, "Visible");
                r.Shown = Guard(r.Shown, true, where, "Shown");
                r.Enabled = Guard(r.Enabled, true, where, "Enabled");
                if (r.InfoValue != null)
                {
                    Func<string> info = r.InfoValue;
                    bool said = false;
                    r.InfoValue = () =>
                    {
                        try { return info(); }
                        catch (Exception e) { if (!said) { said = true; Plugin.Log.LogWarning($"Settings: {where}: its info line threw ({e.Message}); it says '-'."); } return "-"; }
                    };
                }
                if (r.Format != null)
                {
                    Func<int, string> format = r.Format;
                    bool said = false;
                    r.Format = v =>
                    {
                        try { return format(v); }
                        catch (Exception e) { if (!said) { said = true; Plugin.Log.LogWarning($"Settings: {where}: its words for a value threw ({e.Message}); the number is shown."); } return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    };
                }
            }
        }

        internal static Func<bool> Guard(Func<bool> f, bool fallback, string where, string what)
        {
            if (f == null) return null;
            bool said = false;
            return () =>
            {
                try { return f(); }
                catch (Exception e) { if (!said) { said = true; Plugin.Log.LogWarning($"Settings: {where}: its {what} threw ({e.Message}); taken as {fallback.ToString().ToLowerInvariant()}."); } return fallback; }
            };
        }

        private void Add(Row r)
        {
            if (shown || (published && !catchingUp))
            {
                throw new InvalidOperationException("Add every row before Publish.");
            }
            // A picture row is found on screen by its label (LookPreview.FindRows), so no other row may share it: a
            // heading "Your mount" next to a picture "Your mount" got a picture of its own (seen 2026-10-19).
            if (!string.IsNullOrEmpty(r.Label) && Rows.Exists(o => (o.RowKind == RowKind.Preview || r.RowKind == RowKind.Preview) && o.Label == r.Label))
            {
                throw new ArgumentException($"the label \"{r.Label}\" is already a row's on this page, and one of the two is a picture row (Preview): a picture is found on screen by its label, so give it a label of its own.");
            }
            r.MountId = pendingMountId;
            r.SkinId = pendingSkinId;
            if (pendingShown != null)
            {
                Func<bool> shown = pendingShown, own = r.Visible;
                r.Shown = shown;
                r.Enabled = own;
                r.Visible = own == null ? shown : () => shown() && own();
            }
            Rows.Add(r);
        }

        private Func<bool> pendingShown; // set while PerMount (or MountRows) builds one mount's rows
        private string pendingMountId;   // set while PerMount (or PerSkin) builds one mount's rows
        private string pendingSkinId;    // set while PerSkin builds one skin's rows
        private ConfigEntry<string> mountRow; // MountRows' Mount row: which mount is chosen

        // Rows kept per mount (the colour rows: another mount's look isn't the donkey's; docs/mount-colours.md 2.7): rows is
        // called once per registered mount with the mount's id, the suffix for the cfg keys ("" for the donkey, so a cfg
        // from before keeps its values under the same keys; "_crab" for a crab) and whether that mount is the one
        // chosen in the Mount row. The rows built in the call show for that mount only; a mod's own visible condition
        // on them (a custom colour with its preset) stays its own (Row.Enabled: the Mounts panel leaves such a row out until it holds, the menus hide it).
        // Without a Mount row (MountRows not called) the rows are built once, for the donkey, as plain rows.
        // Greys a row (of this page) unless the condition holds: a colour row for a region none of the mount's skins has
        // (a Harness row on a mount without one). The row stays and keeps its value; the Mounts panel shows it greyed, the Mods menu
        // (which knows only visibility) leaves it out for that mount.
        // The mount a companion's chosen skin is a look of pins this tuning key (its [Mount.Tuning] or a code mount's
        // Tuning, either style): its label, else null. A player's row for such a key does nothing on that mount.
        public static string PinnedBy(MountCompanion companion, string tuningKey)
        {
            if (companion == null || string.IsNullOrEmpty(tuningKey)) return null;
            Looks.SkinDef d = Looks.Find(companion.Skin);
            if (d == null || d.Unavailable != null) return null; // not installed, or set aside: the donkey comes instead, which pins nothing
            Catalog.Entry k = d.Mount;
            if (k == null || k == Catalog.Donkey) return null;
            if (!Looks.Rig.MountMayPin(tuningKey)) return null; // a key a mount may not pin is refused at the merge: the row stays live
            bool Has(Dictionary<string, object> t) => t != null && t.ContainsKey(tuningKey);
            return Has(k.Tuning) || Has(k.TuningRide) || Has(k.TuningCart) ? k.Label : null;
        }

        // Says which tuning key a row sets, so the row is left out of the Mods menu and the game's Settings window while the
        // companion's chosen mount pins that key: a speed row does nothing on a mount that sets its own speed
        // (docs/mounts-foundation-2.md 2.1). Call it after the row; the mod's own condition, if any, stays.
        public void TuningKey(string section, string key, string tuningKey, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            if (string.IsNullOrEmpty(tuningKey)) throw new ArgumentException("A tuning key is needed.", nameof(tuningKey));
            EnableWhen(section, key, () => PinnedBy(companion, tuningKey) == null);
        }

        public void EnableWhen(string section, string key, Func<bool> enabled)
        {
            if (enabled == null) throw new ArgumentNullException(nameof(enabled));
            string cfgSection = Sec(section);
            foreach (Row r in Rows)
            {
                if (r.Entry != null && r.Entry.Definition.Section == cfgSection && (r.Entry.Definition.Key == key || r.Entry.Definition.Key == LightnessKey(key)))
                {
                    // a Color row's lightness row (LightnessKey) goes with it: greyed together, never live on its own
                    Func<bool> own = r.Enabled; // a row's own condition (a custom colour row: its preset is Custom) stays
                    r.Enabled = own == null ? enabled : () => own() && enabled();
                }
            }
        }

        // Says a row is about the cart (its paint, its wheels, its lantern; 0.39.0): shown only while its mount pulls one
        // (a PerMount row's mount, else the chosen mount): a mount whose [Mount] says cart = false (MountDef.Cart false)
        // has no cart to colour (the user, 2026-10-25: "for mounts that cant pull the cart ... there's still cart
        // customization ... these shouldnt be available"). Left out of the Mounts panel, the Mods menu and the Settings
        // window alike; its value is kept. Call it after the row, for each of its keys (a custom colour row too).
        public void CartRow(string section, string key)
        {
            string cfgSection = Sec(section);
            foreach (Row r in Rows)
            {
                if (r.Entry == null || r.Entry.Definition.Section != cfgSection || (r.Entry.Definition.Key != key && r.Entry.Definition.Key != LightnessKey(key))) continue;
                string id = r.MountId;
                Func<bool> pulls = () => MountPullsCart(id ?? mountRow?.Value);
                Func<bool> shown = r.Shown, visible = r.Visible;
                r.Shown = shown == null ? pulls : () => shown() && pulls();
                r.Visible = visible == null ? pulls : () => visible() && pulls();
            }
        }

        // Whether the mount may pull the cart (0.39.0; the donkey, an unknown or a missing mount: yes, the donkey comes for it).
        public static bool MountPullsCart(string mountId)
        {
            if (string.IsNullOrEmpty(mountId) || mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return true;
            Catalog.Entry k = Catalog.Find(mountId);
            return k == null || k.Cart;
        }

        // Whether any skin of the mount has the colour region (Coat, Dark, Muzzle, Eye, Harness), so a page can grey
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
        // five): for a PerSkin row's EnableWhen, so Harness greys on a skin without tack and Second tone on one without
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
        private readonly Dictionary<Action<string, string, string, Func<bool>>, Row> lastSkinRowOf = new Dictionary<Action<string, string, string, Func<bool>>, Row>();
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
            pendingShown = isShown;
            pendingMountId = mountId;
            pendingSkinId = skinId;
            int before = Rows.Count;
            try
            {
                rows(skinId, mountId, sfx, isShown);
            }
            finally
            {
                pendingShown = null;
                pendingMountId = null;
                pendingSkinId = null;
            }
            int made = Rows.Count - before;
            if (made <= 0) return;
            List<Row> mine = Rows.GetRange(before, made);
            if (catchingUp && lastSkinRowOf.TryGetValue(rows, out Row last))
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

        // The Skin row's value for a mount (its first skin when the row has none).
        internal string SkinChosen(string mountId)
        {
            string id = string.IsNullOrEmpty(mountId) ? MountCatalog.DonkeyId : mountId;
            if (skinRows.TryGetValue(id, out ConfigEntry<string> row) && !string.IsNullOrEmpty(row.Value)) return row.Value;
            return Looks.FirstSkinOf(id);
        }

        // The 0.12 carry-over (PerSkin): the skin's rows just made take the mount's values from the file when the file
        // has the mount's and none of the skin's, and the mount's Skin row in the file says this skin; the mount's rows
        // (its first skin's) go back to their defaults, so the first skin doesn't wear this skin's colours.
        private void SeedFromMount(List<Row> mine, string skinId, string mountId, string skinSfx, string mountSfx)
        {
            if (Config == null || string.IsNullOrEmpty(Config.ConfigFilePath)) return;
            if (fileSections == null) fileSections = ReadSections(Config.ConfigFilePath);
            if (fileSections.Count == 0) return;
            // the file's own Skin row value for the mount, under any number of its section
            string skinKey = "Skin_" + mountId, chosenInFile = null;
            foreach (KeyValuePair<string, Dictionary<string, string>> s in fileSections)
            {
                if (s.Value.TryGetValue(skinKey, out string v)) { chosenInFile = v; break; }
            }
            if (chosenInFile == null || !chosenInFile.Trim().Equals(skinId, StringComparison.OrdinalIgnoreCase)) return;
            var pairs = new List<(Row Skin, Row Mount)>();
            foreach (Row r in mine)
            {
                if (r.Entry == null) continue;
                string key = r.Entry.Definition.Key;
                if (!key.EndsWith(skinSfx, StringComparison.OrdinalIgnoreCase)) continue;
                if (fileSections.TryGetValue(r.Entry.Definition.Section, out Dictionary<string, string> own) && own.ContainsKey(key)) return; // the skin has values of its own: nothing to seed
                string mountKey = key.Substring(0, key.Length - skinSfx.Length) + mountSfx;
                Row m = Rows.Find(o => o.Entry != null && o.Entry.Definition.Section == r.Entry.Definition.Section && o.Entry.Definition.Key == mountKey);
                if (m == null) continue;
                if (!fileSections.TryGetValue(m.Entry.Definition.Section, out Dictionary<string, string> ms) || !ms.ContainsKey(mountKey)) continue; // the mount's row isn't in the file either
                pairs.Add((r, m));
            }
            if (pairs.Count == 0) return;
            foreach ((Row Skin, Row Mount) pr in pairs)
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
        private readonly Dictionary<Action<string, string, Func<bool>>, Row> lastRowOf = new Dictionary<Action<string, string, Func<bool>>, Row>();

        private void RunPerMount(Action<string, string, Func<bool>> rows, string id)
        {
            bool donkey = id.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            Func<bool> isShown = () => string.Equals(mountRow?.Value ?? MountCatalog.DonkeyId, id, StringComparison.OrdinalIgnoreCase);
            pendingShown = isShown;
            pendingMountId = id;
            int before = Rows.Count;
            try
            {
                rows(id, donkey ? "" : "_" + id, isShown);
            }
            finally
            {
                pendingShown = null;
                pendingMountId = null;
            }
            int made = Rows.Count - before;
            if (made <= 0) return;
            List<Row> mine = Rows.GetRange(before, made);
            if (catchingUp && lastRowOf.TryGetValue(rows, out Row last))
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

        internal const string KeyHelp = "While setting a key, Backspace clears it and Esc cancels.";

        internal static bool IsClearKey(KeyboardShortcut k)
        {
            if (k.MainKey != UnityEngine.KeyCode.Backspace) return false;
            foreach (UnityEngine.KeyCode m in k.Modifiers) return false; // any modifier: not the clearing key
            return true;
        }

        private void ClearIfBackspace(ConfigEntry<KeyboardShortcut> e)
        {
            if (IsClearKey(e.Value))
            {
                e.Value = KeyboardShortcut.Empty;
                Plugin.Log.LogInfo($"Settings: {OwnerId} {e.Definition.Key} cleared (Backspace means no key).");
            }
        }

        // Before the page's first key row, in its section: how to clear a key and cancel setting one. A note: in the Mods
        // menu a row whose description is the sentence, in the game's Settings window the sentence across the row.
        private void AddKeyHelp()
        {
            int first = Rows.FindIndex(r => r.RowKind == RowKind.Key);
            if (first < 0)
            {
                return;
            }
            Row k = Rows[first];
            Rows.Insert(first, new Row
            {
                RowKind = RowKind.Note, Section = k.Section, ShownSection = k.ShownSection, Label = "Setting a key",
                Description = KeyHelp,
            });
        }

        private void Wire<T>(ConfigEntry<T> e, Action<T> apply)
        {
            if (apply == null)
            {
                return;
            }
            e.SettingChanged += (_, __) => Run(e, apply);
            Run(e, apply);
        }

        private void Run<T>(ConfigEntry<T> e, Action<T> apply)
        {
            try
            {
                apply(e.Value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Settings: {OwnerId} {e.Definition.Section}.{e.Definition.Key} could not be applied: {ex.Message}");
            }
        }
    }
}
