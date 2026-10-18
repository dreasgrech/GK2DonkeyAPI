using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;

namespace GK2DonkeyAPI.Api
{
    // A player mod's settings, defined once and shown where the player looks (Mounts.SettingsPage):
    // - with GK2 Mod Framework installed: in its Mods menu (the mod's own entry, its sections, every kind of row);
    // - without it: in the game's own Settings window, on pages of the mod's own after the game's settings, made of
    //   copies of the game's own rows (SettingsRows.cs). Keys and text there show read-only; they're set in the cfg.
    // Each row binds a BepInEx entry in the mod's own cfg, runs its apply action now and on every change (from the menu,
    // the Settings window or the cfg), and shows under its section. Call Publish once, after the rows, from the mod's
    // Awake.
    // Sections keep the order they're first used in: the cfg section is numbered ("1. Keys", "2. Behaviour"), because
    // the framework's Mods menu sorts sections by name and has no other way to order them. The game's Settings window
    // shows the plain names.
    // Looks: Preview shows a picture of the mod's donkey in its current colours next to the settings (LookPreview.cs),
    // and LookButton opens a bar of the look rows over the game, so the player sees the real donkey change (LookBar.cs).
    // Both are extras: if anything about them fails, they don't show, and the settings work as before.
    public sealed class MountSettingsPage
    {
        internal enum Kind { Toggle, Choice, Steps, Key, Text, Info, Button, Preview, Note, List }

        internal sealed class Row
        {
            internal Kind Kind;
            internal ConfigEntryBase Entry;
            internal string Section, Label, Description;
            internal string ShownSection; // the plain name, without the number
            internal string[] ChoiceLabels;
            internal Array ChoiceValues;
            internal Func<IReadOnlyList<(string Id, string Label)>> Choices; // Kind.List: read when the row is first shown
            internal int Min, Max, Step;
            internal Func<int, string> Format;
            internal Func<string> InfoValue;
            internal Func<bool> Visible;     // shown at all (Shown and Enabled together, for the menus)
            internal Func<bool> Shown;       // a row of a set kept per mount (PerMount, the Skin rows): the chosen mount's shows, the others don't
            internal Func<bool> Enabled;     // the mod's own condition (a custom colour with its preset on Custom): the look bar greys the row when false
            internal bool IsLook;           // shown in the look bar (LookButton)
            internal Action OnClick;         // Kind.Button
            internal bool WithCart;          // Kind.Preview: the donkey with its cart, or alone
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
        // section (Saddle Up 0.11 put Mount right after Keys: Donkey went from "3. Donkey" to "5. Donkey"), a cfg from
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
        public void MovedKey(string section, string key, string oldSection, string oldKey = null)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(oldSection)) throw new ArgumentException("section, key and oldSection are needed.");
            movedKeys[section + "\n" + key] = (oldSection, oldKey ?? key);
        }

        private readonly Dictionary<string, (string Section, string Key)> movedKeys = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        private Dictionary<string, Dictionary<string, string>> fileSections; // the cfg file's sections and their keys, raw
        private readonly HashSet<string> movedNoted = new HashSet<string>();

        // Every section in the file that may hold a copy of (cfgSection, key): the same plain name under another
        // number, and the place MovedKey named, under any number. (section, key) pairs.
        private IEnumerable<(string Section, string Key)> OldPlaces(string cfgSection, string key) => OldPlaces(cfgSection, key, fileSections);

        private IEnumerable<(string Section, string Key)> OldPlaces(string cfgSection, string key, Dictionary<string, Dictionary<string, string>> file)
        {
            string plain = PlainName(cfgSection);
            movedKeys.TryGetValue(PlainName(cfgSection) + "\n" + key, out (string Section, string Key) alias);
            foreach (string s in file.Keys)
            {
                if (s == cfgSection) continue;
                string p = PlainName(s);
                if (p == plain) yield return (s, key);
                else if (alias.Section != null && p == alias.Section) yield return (s, alias.Key);
            }
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
                    if (!claimed.Contains(s.Key + "\n" + kv.Key)) unclaimed.Add((s.Key, kv.Key, kv.Value));
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
        private int droppedOldCopies, dropsFailed;

        // After the last row: the mod's save-on-set back as it was, and the one save that writes every value and
        // leaves out the old copies dropped while binding. Publish calls it; a page never published keeps its
        // values in memory only, as "Add every row before Publish" says.
        internal void FinishBinding()
        {
            if (Config.SaveOnConfigSet != saveOnSet) Config.SaveOnConfigSet = saveOnSet;
            if (saveOnSet || droppedOldCopies > 0) Config.Save();
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
        // given. The effect rows are EffectRows. Mounts and skins registered before the page is built appear here with
        // no change to yours; later ones after a restart.
        public void MountRows(string section, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            MountCompanion me = companion;
            // A mount (Donkey, Giant Rat, Dragon) has skins (the donkey: Default, Hellfire): a Mount
            // dropdown, then a Skin dropdown with that mount's skins. A dropdown's choices are fixed once built, so there
            // is one Skin row per mount, shown for the chosen mount only (cfg keys Mount and Skin_<mount id>). What the
            // companion gets, and the save keeps, is one thing: the skin id (companion.Skin).
            ConfigEntry<string> mountE = null;
            var skinRows = new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);
            void Apply()
            {
                if (mountE == null) return;
                string kind = mountE.Value ?? MountKinds.DonkeyId;
                skinRows.TryGetValue(kind, out ConfigEntry<string> row);
                string value = row?.Value;
                // The donkey's own art is no skin of another mount: a cfg holding it for one (written while that
                // mount's skin was missing, seen 2026-10-18) takes the mount's first skin instead, and keeps it.
                bool donkeyKind = kind.Equals(MountKinds.DonkeyId, StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(value) || (!donkeyKind && value.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase))) value = Looks.FirstSkinOf(kind);
                if (row != null && row.Value != value)
                {
                    row.Value = value; // SettingChanged runs Apply again with the healed value
                    return;
                }
                me.Skin = value;
            }
            mountE = List(section, "Mount", MountKinds.DonkeyId, Looks.KindChoices, "Mount",
                "What you ride: the donkey, or another mount (the giant rat, the dragon, ... built in, or from a skin pack you installed or another mod). Each mount has its own skins: the next row.",
                v => Apply(), look: true);
            mountRow = mountE;
            foreach (MountKindInfo k in Mounts.Kinds.Kinds)
            {
                string id = k.Id;
                string first = Looks.FirstSkinOf(id);
                pendingShown = () => string.Equals(mountE?.Value, id, StringComparison.OrdinalIgnoreCase); // the chosen mount's Skin row shows (Row.Shown)
                try
                {
                    ConfigEntry<string> row = List(section, "Skin_" + id, first, () => Looks.SkinChoicesOf(id), "Skin",
                        $"How the {k.Name.ToLowerInvariant()} is drawn: one of its skins.",
                        v => Apply(), look: true);
                    skinRows[id] = row;
                }
                finally
                {
                    pendingShown = null;
                }
            }
            Apply();
        }

        // The rows for the mount's babies (foals), for the same section as MountRows: how many, whether they come
        // indoors, whether they are the same mount and skin. Cfg keys: Foals, FoalsIndoors, FoalsWearSkin in the section
        // given (a mod that had them in another section names it with MovedKey first).
        public void FoalRows(string section, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            MountCompanion me = companion;
            Steps(section, "Foals", me.FoalCount, 0, MountCompanion.MaxFoals, 1, "Babies", "Little ones of your mount that stay with it and follow it around.", v => me.FoalCount = v, v => v == 0 ? "None" : v.ToString());
            Toggle(section, "FoalsIndoors", true, "Babies come indoors", "They come through doors with you into buildings, the church and dungeons, follow you inside and come back out with you.", v => me.FoalsComeIndoors = v);
            Toggle(section, "FoalsWearSkin", me.FoalsWearSkin, "Babies are the same mount and skin", "Off: they stay plain baby donkeys whatever yours is.", v => me.FoalsWearSkin = v);
        }

        // The effect rows (docs/api.md "Effects"), for a section of their own ("Effect"): the effect (the mount's own,
        // none, every registered one), when it shows, and how. Cfg keys: Effect, EffectWhen, EffectTrail, EffectWithCart,
        // EffectInFights, EffectOnFoals in the section given.
        public void EffectRows(string section, MountCompanion companion)
        {
            if (companion == null) throw new ArgumentNullException(nameof(companion));
            MountCompanion me = companion;
            ConfigEntry<string> effect = List(section, "Effect", MountEffects.KindId, GK2DonkeyAPI.Effects.EffectChoices, "Effect",
                "A particle effect on your mount: As the mount (what the creature comes with, if anything), None, Hellfire (the game's lamp fire in a ring round it), or one a mod added. Never indoors.",
                v => me.Effect = v, look: true);
            Func<bool> on = () => !MountEffects.IsNone(effect.Value);
            Choice(section, "EffectWhen", me.EffectWhen, "Effect shown", "", v => me.EffectWhen = v, new[] { "Never", "At night", "Always" }, look: true, visible: on);
            Toggle(section, "EffectTrail", true, "The effect leaves a trail", "Hellfire: flames left behind while it walks, burning out.", v => Mounts.EffectSettings.Set(OwnerId, "EffectTrail", v), on);
            Toggle(section, "EffectWithCart", false, "The effect goes round the cart too", "With the cart hitched, one wider ring round the mount and the cart.", v => Mounts.EffectSettings.Set(OwnerId, "EffectWithCart", v), on);
            Toggle(section, "EffectInFights", true, "The effect shows in fights", "Off: it goes out while you fight in armour.", v => Mounts.EffectSettings.Set(OwnerId, "EffectInFights", v), on);
            Toggle(section, "EffectOnFoals", false, "Babies have it too", "A smaller one round each of your mount's babies.", v => Mounts.EffectSettings.Set(OwnerId, "EffectOnFoals", v), on);
        }

        // On or off.
        public ConfigEntry<bool> Toggle(string section, string key, bool value, string label, string description, Action<bool> apply = null, Func<bool> visible = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<bool> e = Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Toggle, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // One of an enum's values; labels (one per value, in order) are what players see, else the value names.
        // look: also shown in the look bar (LookButton).
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
            Add(new Row { Kind = Kind.Choice, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, ChoiceLabels = shown, ChoiceValues = values, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // One of a list of ids read when the row is first shown (the skins installed, say): choices gives (id, label)
        // pairs; the id is what the cfg holds, from every menu alike. A stored id that isn't in the list is shown as
        // "<id> (not installed)" and left alone, so it returns with what provides it. The list is read once per
        // session: anything registered after the row is shown appears after a restart. look: in the look bar too.
        public ConfigEntry<string> List(string section, string key, string value, Func<IReadOnlyList<(string Id, string Label)>> choices, string label, string description, Action<string> apply = null, bool look = false, Func<bool> visible = null)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            string cfgSection = Sec(section);
            ConfigEntry<string> e = Bind(cfgSection, key, value, description);
            // an empty id can't be a dropdown choice (FillList drops it): a cfg holding "" takes the row's default
            if (string.IsNullOrEmpty(e.Value) && !string.IsNullOrEmpty(value)) e.Value = value;
            Add(new Row { Kind = Kind.List, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Choices = choices, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A List row's labels and ids, filled once from its choices (plus the stored id if it isn't among them), so the
        // Mods menu's dropdown, its labels and the Settings window agree for the session.
        internal static void FillList(Row r)
        {
            if (r.Kind != Kind.List || r.ChoiceLabels != null) return;
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
                labels.Add(now + " (not installed)");
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
            Add(new Row { Kind = Kind.Steps, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Min = min, Max = max, Step = Math.Max(1, step), Format = format, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A key (with modifiers). Read-only in the game's Settings window.
        // pressed: called when the player presses the key (its modifiers held; other keys held, like walking, don't
        // matter), while the API is on. The API polls it: the mod needs no Update of its own.
        // Plain Backspace means no key: set from either menu or the cfg, it's cleared at once (GK2 Mod Framework's key
        // capture has no way to clear a key and takes Backspace as one; the game's Settings window does the same on
        // its own). Publish adds a row saying so before the page's first key.
        public ConfigEntry<KeyboardShortcut> Key(string section, string key, KeyboardShortcut value, string label, string description, Action<KeyboardShortcut> apply = null, Action pressed = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<KeyboardShortcut> e = Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Key, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description });
            ClearIfBackspace(e);
            e.SettingChanged += (_, __) => ClearIfBackspace(e); // before apply: apply sees the cleared key
            Wire(e, apply);
            if (pressed != null)
            {
                KeyActions.Add(OwnerId, e, pressed);
            }
            return e;
        }

        // Free text. Read-only in the game's Settings window. visible: shown only while it says so.
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply = null, Func<bool> visible = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<string> e = Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Text, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A colour a player picks by name (PaletteColor: Chestnut, Sand, Moss, Sky, ...) and makes lighter or darker
        // (the picture, Preview, shows the result; a swatch row did before it existed). Bound as <key> and <key>Lightness; apply gets the colour as hex RRGGBB. visible:
        // shown only while it says so (e.g. only while its part's choice is Custom). In the look bar too.
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
            // the lightness key: "CustomCoatLightness", and with a per-mount suffix "CustomCoatLightness_dragon"
            int suffix = key.IndexOf('_');
            string lightnessKey = suffix < 0 ? key + "Lightness" : key.Substring(0, suffix) + "Lightness" + key.Substring(suffix);
            lightness = Steps(section, lightnessKey, 100, 40, 160, 10, label + ": lightness", "100% is the colour itself; lower is darker, higher is lighter.", _ => Combine(), n => n + "%", look: true, visible: visible);
            Combine();
        }

        // The read-only "Status" row: the page's status (Mounts.StatusText unless the page was given its own).
        public void Status(string section, string label = "Status", string description = "What the mod is doing right now.")
        {
            Info(section, label, () => StatusValue(), description);
        }

        // A read-only line, e.g. what the mod is doing.
        public void Info(string section, string label, Func<string> value, string description = "")
        {
            Add(new Row { Kind = Kind.Info, Section = Sec(section), ShownSection = section, Label = label, Description = description, InfoValue = value });
        }

        // A picture of the companion's donkey and cart in their current colours, walking on the spot: a row of the page,
        // here in this section, in the order called (LookPreview); call it once per section that should show it (e.g.
        // at the top of each section with look rows, so it's in view from each). Also above the Mods window, where it
        // stays in view whatever row is scrolled to, when there's room there (1440p and up, not 1080p).
        // label: the row's name; cart: the row's picture is the donkey with its cart, or the donkey alone (above the
        // window it's always with the cart). Above the game's Settings window without the Mods menu; in the look bar
        // too. It needs no donkey in the game: drawn from the game's own files in the companion's colours, so it shows
        // in the main menu and indoors.
        public void Preview(string section, MountCompanion companion, string label = "Your mount", bool cart = true)
        {
            PreviewOf = companion;
            Add(new Row
            {
                Kind = Kind.Preview, Section = Sec(section), ShownSection = section, Label = label, Description = PreviewRoom,
                Visible = () => LookPreview.RowsShown, WithCart = cart,
            });
        }

        // The rows are found on screen by their labels (LookPreview), and the Mods menu sizes a row by its description
        // (only line breaks count as empty: the minimum height), so the picture's room is eight lines of one fully
        // transparent dot each (a zero-width space didn't count).
        internal bool HasPreviewRows => Rows.Exists(r => r.Kind == Kind.Preview);

        internal Row PreviewRowLabelled(string text) => Rows.Find(r => r.Kind == Kind.Preview && r.Label == text);
        internal const string PreviewRoom = "<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>";

        // A button that closes the menu and opens the look bar over the game: the look rows (Choice and Steps with look,
        // and Color) at the bottom of the screen, the real donkey in view, with Done (keep) and Cancel (put back).
        // Only in a game; in the main menu it says so.
        public void LookButton(string section, string label, string description)
        {
            Add(new Row { Kind = Kind.Button, Section = Sec(section), ShownSection = section, Label = label, Description = description, OnClick = () => LookBar.Open(this) });
        }

        // Shows the rows: in the framework's Mods menu when it's installed, else in the game's Settings window.
        public void Publish()
        {
            if (published)
            {
                return;
            }
            published = true;
            AddKeyHelp();
            FinishBinding();
            LookPreview.Watch(this);
            if (FrameworkLink.TryRegister(this))
            {
                MenuLabels.Add(this); // the choices' labels over the framework's plain names
                Plugin.Log.LogInfo($"Settings: {Title} is in GK2 Mod Framework's Mods menu ({Rows.Count} rows).");
                return;
            }
            SettingsRows.Add(this);
        }

        private void Add(Row r)
        {
            if (published)
            {
                throw new InvalidOperationException("Add every row before Publish.");
            }
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
        private ConfigEntry<string> mountRow; // MountRows' Mount row: which mount is chosen

        // Rows kept per mount (the colour rows: a dragon's look isn't the donkey's; docs/mount-colours.md 2.7): rows is
        // called once per registered mount with the mount's id, the suffix for the cfg keys ("" for the donkey, so a cfg
        // from before keeps its values under the same keys; "_dragon" for the dragon) and whether that mount is the one
        // chosen in the Mount row. The rows built in the call show for that mount only; a mod's own visible condition
        // on them (a custom colour with its preset) stays its own (Row.Enabled: the look bar greys, never hides).
        // Without a Mount row (MountRows not called) the rows are built once, for the donkey, as plain rows.
        public void PerMount(Action<string, string, Func<bool>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (mountRow == null)
            {
                rows(MountKinds.DonkeyId, "", null);
                return;
            }
            foreach (MountKindInfo k in Mounts.Kinds.Kinds)
            {
                string id = k.Id;
                bool donkey = id.Equals(MountKinds.DonkeyId, StringComparison.OrdinalIgnoreCase);
                Func<bool> shown = () => string.Equals(mountRow?.Value ?? MountKinds.DonkeyId, id, StringComparison.OrdinalIgnoreCase);
                pendingShown = shown;
                try
                {
                    rows(id, donkey ? "" : "_" + id, shown);
                }
                finally
                {
                    pendingShown = null;
                }
            }
        }

        internal const string KeyHelp = "While setting a key, Backspace clears it and Esc cancels.";

        internal static bool IsClearKey(KeyboardShortcut k)
        {
            return k.MainKey == UnityEngine.KeyCode.Backspace && !k.Modifiers.Any();
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
            int first = Rows.FindIndex(r => r.Kind == Kind.Key);
            if (first < 0)
            {
                return;
            }
            Row k = Rows[first];
            Rows.Insert(first, new Row
            {
                Kind = Kind.Note, Section = k.Section, ShownSection = k.ShownSection, Label = "Setting a key",
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
