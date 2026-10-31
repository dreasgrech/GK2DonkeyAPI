using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;

namespace GK2DonkeyAPI
{
    // Translations (docs/localization.md, step 1, 2026-10-31): the text our windows draw, in the player's game language,
    // looked up when it is drawn (the game sets its language after the plugins start: in Awake it is always English).
    // A text belongs to a plugin, its owner by GUID: a page's rows and sections to the mod that made the page, the API's
    // window words (UiWords) and the rows its builders add (the Skin row, the babies rows) to the API.
    // An owner's translations are files, <id>.txt and <id>.csv, where id is the game's language id (de, fr, pt-br, zh_cn,
    // or a community language pack's):
    // - bundled: in its DLL's folder, Languages\;
    // - a translator's own download: in any plugin folder, Translations\<owner guid>\ (it wins over the bundled one).
    // .txt is "key = text" lines; "# en: <English>" just above an entry is the English it was translated from; any other
    // # line is a comment; \n is a line break. .csv is key,value,english with the separator of its header line (, ; or a
    // tab: European Excel saves with ;), quoted as the game's own strings.csv. In one folder a .txt line beats a .csv
    // line. The language's id is tried, then the part before its first - or _ (pt-br: pt), then the English in the code.
    // A line whose recorded English isn't the code's any more shows the English until it is translated again; a line
    // left out or empty shows the English. The cfg never changes language: BepInEx writes the descriptions Bind got, in
    // Awake, before any language.
    internal static class Texts
    {
        internal const string Api = Plugin.Guid;
        private const float TestGrowth = 1.6f; // the test language: 1.6 times as long (between German's and French's 90th percentile for short labels)

        internal struct Line
        {
            internal string Value, English, Source;
        }

        internal sealed class Table
        {
            internal readonly Dictionary<string, Line> Lines = new Dictionary<string, Line>(StringComparer.Ordinal);
            internal readonly List<string> Files = new List<string>();
            internal int Malformed;
        }

        // ---- the self-tests' handles ----
        internal static string LanguageOverride;  // a language id in place of the game's
        internal static bool? TestForced;          // the test language on or off whatever Debug.TestLanguage says
        internal static readonly Dictionary<string, string> TestBundled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // owner: its Languages folder
        internal static List<string> TestTranslators;  // plugin folders to look in for Translations\, in place of BepInEx\plugins\*

        // The language now: the game's (LLBase.CurrentLang: "en" until the game has loaded one).
        internal static string Language
        {
            get
            {
                if (LanguageOverride != null) return LanguageOverride;
                try { return GameLanguage() ?? "en"; }
                catch { return "en"; }
            }
        }

        // The game's own, in a method of its own: a game update that renamed it fails when this one is compiled, inside
        // the try above (in the getter itself it would fail before the try; game-code.txt warns, our windows stay English).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameLanguage() => LLBase.CurrentLang;

        // The test language (dev only, Debug.TestLanguage): every text 1.6 times as long and in brackets.
        internal static bool TestOn => TestForced ?? (DevTools.Unlocked && Plugin.TestLanguageOn != null && Plugin.TestLanguageOn.Value);

        // ---- the lookup ----

        // The owner's text for key in the language now, else the English.
        internal static string Of(string owner, string key, string english)
        {
            if (string.IsNullOrEmpty(english)) return english; // nothing stays nothing (a heading is told from a note by its empty description)
            if (TestOn) return Test(english);
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(key)) return english;
            Table t = TableOf(owner);
            if (t == null || !t.Lines.TryGetValue(key, out Line l)) return english;
            if (l.English != null && !string.Equals(l.English, english, StringComparison.Ordinal)) return english; // the English changed since it was translated
            return l.Value;
        }

        // A template ({0}, {1}) in the language now, filled in; a translation whose template doesn't take the values: the English.
        internal static string Format(string owner, string key, string english, params object[] args)
        {
            string t = Of(owner, key, english);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, t, args);
            }
            catch (FormatException)
            {
                bool englishItself = t == english; // the code's own template is wrong: a bug of the mod's, not the translator's
                if (TestQuiet) TestWarnings++;
                else if (badTemplates.Add(owner + "\n" + key))
                {
                    Plugin.Log.LogWarning(englishItself
                        ? $"Translations: {owner}'s English {key} doesn't take its values ({english}): a bug in the mod."
                        : $"Translations: {owner}'s line {key} in {Language} doesn't take its values ({t}); the English is shown.");
                }
                try { return string.Format(CultureInfo.InvariantCulture, english, args); }
                catch (FormatException) { return english; }
            }
        }

        private static readonly HashSet<string> badTemplates = new HashSet<string>(StringComparer.Ordinal);

        // The English made 1.6 times as long with its own letters (never a brace: a {0} stays one), in brackets.
        internal static string Test(string english)
        {
            int target = (int)Math.Round(english.Length * TestGrowth);
            var sb = new StringBuilder(target + 4);
            sb.Append('[').Append(english);
            if (english.Length < target) sb.Append(' ');
            for (int i = 0; sb.Length - 1 < target && i < english.Length * 4; i++)
            {
                char c = english[i % english.Length];
                if (c != '{' && c != '}') sb.Append(c);
            }
            sb.Append(']');
            return sb.ToString();
        }

        // ---- the rows of a page (MountSettingsPage.Row: its Owner, TextKey, LabelWord, ButtonWord) ----

        internal static string Label(MountSettingsPage.Row r) => r.LabelWord != null ? r.LabelWord.Text : Of(r.Owner, r.TextKey + ".label", r.Label);

        internal static string Desc(MountSettingsPage.Row r) => Of(r.Owner, r.TextKey + ".desc", r.Description);

        // A dropdown's choice: a Choice row's words are the page's; a List row's are the API's for "Default" and the notes
        // FillList marked (not installed, set aside, an empty list), a named skin's its owner's (step 3), the rest as they are.
        internal static string Choice(MountSettingsPage.Row r, int i)
        {
            string english = r.ChoiceLabels[i];
            if (r.RowKind == MountSettingsPage.RowKind.List)
            {
                MountSettingsPage.ChoiceNote note = r.ChoiceNotes != null && i < r.ChoiceNotes.Length ? r.ChoiceNotes[i] : MountSettingsPage.ChoiceNote.None;
                string name = r.ChoiceNames != null && i < r.ChoiceNames.Length ? r.ChoiceNames[i] : english;
                switch (note)
                {
                    case MountSettingsPage.ChoiceNote.NotInstalled: return UiWords.NameNotInstalled.Format(name);
                    case MountSettingsPage.ChoiceNote.SetAside: return UiWords.NameSetAside.Format(name);
                    case MountSettingsPage.ChoiceNote.Empty: return UiWords.NoChoice.Text;
                }
                if (english == Looks.DefaultLabel) return UiWords.DefaultSkin.Text;
                Looks.SkinDef s = r.ChoiceValues != null && i < r.ChoiceValues.Length ? Looks.Find(r.ChoiceValues.GetValue(i) as string) : null;
                return s != null && !string.IsNullOrEmpty(s.Name) && english == s.Name ? Looks.NameTerm(s).Text : english;
            }
            return Of(r.Owner, r.TextKey + ".choice." + i.ToString(CultureInfo.InvariantCulture), english);
        }

        internal static string Button(MountSettingsPage.Row r) => r.ButtonWord != null ? r.ButtonWord.Text : Of(r.Owner, r.TextKey + ".button", r.ButtonText ?? UiWords.Open.English);

        internal static string Section(MountSettingsPage p, string shown) => Of(p.OwnerId, "section." + Slug(shown), shown);

        internal static string Title(MountSettingsPage p) => Of(p.OwnerId, "page.title", p.Title);

        // A row's key without its mount or skin: "Coat_donkey/acme.blaze" and "Coat_crab" are "Coat" (the same text for
        // every mount and skin); a row with no cfg entry by its kind and English ("heading.mount_colours").
        internal static string TextKeyOf(MountSettingsPage.Row r)
        {
            if (r.Entry != null)
            {
                string key = r.Entry.Definition.Key;
                if (r.SkinId != null && key.EndsWith("_" + r.SkinId, StringComparison.OrdinalIgnoreCase)) return key.Substring(0, key.Length - r.SkinId.Length - 1);
                if (r.MountId != null && key.EndsWith("_" + r.MountId, StringComparison.OrdinalIgnoreCase)) return key.Substring(0, key.Length - r.MountId.Length - 1);
                return key;
            }
            string kind = r.RowKind == MountSettingsPage.RowKind.Note ? "heading"
                : r.RowKind == MountSettingsPage.RowKind.Button ? (r.OpensPanel ? "panel" : "button")
                : r.RowKind == MountSettingsPage.RowKind.Info ? "info"
                : "row";
            return kind + "." + Slug(r.Label);
        }

        // "Mount colours" -> "mount_colours": letters and digits, the rest one underscore.
        internal static string Slug(string s)
        {
            var sb = new StringBuilder();
            bool gap = false;
            foreach (char c in s ?? "")
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (gap && sb.Length > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                    gap = false;
                }
                else gap = true;
            }
            return sb.Length > 0 ? sb.ToString() : "_";
        }

        // ---- the files ----

        private static readonly Dictionary<string, Table> tables = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
        private static string tablesLanguage;
        private static List<string> translatorRoots;

        // The tables read again at the next lookup (the self-tests, after writing their files).
        internal static void Forget()
        {
            tables.Clear();
            tablesLanguage = null;
            translatorRoots = null;
        }

        internal static Table TableOf(string owner)
        {
            string lang = Language;
            if (!string.Equals(lang, tablesLanguage, StringComparison.OrdinalIgnoreCase))
            {
                tables.Clear();
                tablesLanguage = lang;
            }
            if (tables.TryGetValue(owner, out Table t)) return t;
            t = Load(owner, lang);
            tables[owner] = t;
            Said(owner, lang, t);
            return t;
        }

        private static Table Load(string owner, string lang)
        {
            if (string.IsNullOrEmpty(lang) || IsEnglish(lang)) return null;
            var ids = new List<string> { lang };
            int cut = lang.IndexOfAny(new[] { '-', '_' });
            if (cut > 0 && !IsEnglish(lang.Substring(0, cut))) ids.Add(lang.Substring(0, cut));
            List<string> folders = FoldersOf(owner);
            if (folders.Count == 0) return null;
            var t = new Table();
            foreach (string id in ids)
            {
                foreach (string dir in folders)
                {
                    ReadTxt(Path.Combine(dir, id + ".txt"), t);
                    ReadCsv(Path.Combine(dir, id + ".csv"), t);
                }
            }
            return t.Files.Count > 0 ? t : null;
        }

        private static bool IsEnglish(string id) => id.Equals("en", StringComparison.OrdinalIgnoreCase);

        // Where an owner's translations are, in their order: the translators' downloads, then the bundled folder.
        private static List<string> FoldersOf(string owner)
        {
            var list = new List<string>();
            foreach (string plugin in TranslatorRoots())
            {
                string d = Path.Combine(Path.Combine(plugin, "Translations"), owner);
                if (Directory.Exists(d)) list.Add(d);
            }
            string bundled = BundledFolder(owner);
            if (bundled != null && Directory.Exists(bundled)) list.Add(bundled);
            return list;
        }

        private static List<string> TranslatorRoots()
        {
            if (TestTranslators != null) return TestTranslators;
            if (translatorRoots != null) return translatorRoots;
            translatorRoots = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(Paths.PluginPath))
                {
                    if (Directory.Exists(Path.Combine(d, "Translations"))) translatorRoots.Add(d);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Translations: the plugin folders couldn't be read for translators' files (" + e.Message + ").");
            }
            return translatorRoots;
        }

        // The owner's bundled folder: Languages\ beside its DLL; null when the owner isn't a loaded plugin.
        internal static string BundledFolder(string owner)
        {
            if (TestBundled.TryGetValue(owner, out string f)) return f;
            try
            {
                if (Chainloader.PluginInfos.TryGetValue(owner, out PluginInfo p) && !string.IsNullOrEmpty(p?.Location))
                {
                    return Path.Combine(Path.GetDirectoryName(p.Location), "Languages");
                }
            }
            catch
            {
                // no folder: no bundled translations
            }
            return null;
        }

        // "key = text" lines (the header's note on the format). A line already read (from a file before) is kept.
        internal static void ReadTxt(string path, Table t)
        {
            if (!File.Exists(path)) return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Warn(path, 0, "can't be read (" + e.Message + ")");
                return;
            }
            t.Files.Add(path);
            if (NotUtf8(lines)) Warn(path, 0, "isn't saved as UTF-8: its letters outside English may be wrong (save it as UTF-8)");
            string english = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                if (s.Length == 0)
                {
                    english = null;
                    continue;
                }
                if (s[0] == '#')
                {
                    string c = s.Substring(1).TrimStart();
                    if (c.StartsWith("en:", StringComparison.OrdinalIgnoreCase)) english = Unescape(c.Substring(3).Trim());
                    continue;
                }
                int eq = s.IndexOf('=');
                if (eq <= 0)
                {
                    t.Malformed++;
                    Warn(path, i + 1, "has no \"key = text\"; left out");
                    english = null;
                    continue;
                }
                string key = s.Substring(0, eq).Trim(), value = Plain(Unescape(s.Substring(eq + 1).Trim()));
                if (value.Length > 0 && !t.Lines.ContainsKey(key)) t.Lines[key] = new Line { Value = value, English = english, Source = Path.GetFileName(path) + ":" + (i + 1) };
                english = null;
            }
        }

        // key,value,english rows (the header's note on the format). A line already read is kept.
        internal static void ReadCsv(string path, Table t)
        {
            if (!File.Exists(path)) return;
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Warn(path, 0, "can't be read (" + e.Message + ")");
                return;
            }
            t.Files.Add(path);
            if (text.IndexOf('�') >= 0) Warn(path, 0, "isn't saved as UTF-8: its letters outside English may be wrong (in Excel: Save as \"CSV UTF-8\")");
            int start = 0, line = 1;
            char sep = ',';
            string first = FirstLine(text, 0);
            if (first.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && first.Length >= 5)
            {
                sep = first[4]; // Excel's own first line naming the separator
                start = NextLine(text, 0);
                line = 2;
                first = FirstLine(text, start);
            }
            else if (first.StartsWith("key;", StringComparison.OrdinalIgnoreCase)) sep = ';';
            else if (first.StartsWith("key\t", StringComparison.OrdinalIgnoreCase)) sep = '\t';
            bool unclosed;
            List<(List<string> Fields, int Line)> rows = ParseCsv(text, start, line, sep, out unclosed);
            for (int n = 0; n < rows.Count; n++)
            {
                List<string> f = rows[n].Fields;
                if (n == 0 && f.Count >= 2 && f[0].Trim().Equals("key", StringComparison.OrdinalIgnoreCase)) continue; // the header
                if (f.Count < 2)
                {
                    t.Malformed++;
                    Warn(path, rows[n].Line, "has no key and value (separated by " + (sep == '\t' ? "a tab" : sep.ToString()) + "); left out");
                    continue;
                }
                string key = f[0].Trim(), value = Plain(f[1].Trim());
                if (value.IndexOf('\n') >= 0 && value.IndexOf(sep) >= 0) Warn(path, rows[n].Line, "has a quote that seems to swallow the lines after it (a \" in a text is written \"\")");
                string english = f.Count > 2 && f[2].Trim().Length > 0 ? f[2].Trim() : null; // trimmed as the .txt's: a space after a separator is no part of it
                if (key.Length > 0 && value.Length > 0 && !t.Lines.ContainsKey(key)) t.Lines[key] = new Line { Value = value, English = english, Source = Path.GetFileName(path) + ":" + rows[n].Line };
            }
            if (unclosed) Warn(path, rows.Count > 0 ? rows[rows.Count - 1].Line : line, "has a quote that is never closed: the lines after it are lost (a \" in a text is written \"\")");
        }

        // The game's ModsCsv rules: a field in quotes may hold the separator, line breaks and "" for a quote.
        private static List<(List<string> Fields, int Line)> ParseCsv(string text, int start, int line, char sep, out bool unclosed)
        {
            var rows = new List<(List<string>, int)>();
            var row = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            int rowLine = line;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else quoted = false;
                    }
                    else
                    {
                        if (c == '\n') line++;
                        if (c != '\r') sb.Append(c);
                    }
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == sep)
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (c == '\n')
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                    if (row.Count > 1 || row[0].Length > 0) rows.Add((row, rowLine));
                    row = new List<string>();
                    line++;
                    rowLine = line;
                }
                else if (c != '\r') sb.Append(c);
            }
            unclosed = quoted;
            if (quoted || sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                if (row.Count > 1 || row[0].Length > 0) rows.Add((row, rowLine));
            }
            return rows;
        }

        private static string FirstLine(string text, int start)
        {
            int end = text.IndexOf('\n', start);
            string s = end < 0 ? text.Substring(start) : text.Substring(start, end - start);
            return s.TrimEnd('\r');
        }

        private static int NextLine(string text, int start)
        {
            int end = text.IndexOf('\n', start);
            return end < 0 ? text.Length : end + 1;
        }

        private static bool NotUtf8(string[] lines)
        {
            foreach (string l in lines) if (l.IndexOf('�') >= 0) return true;
            return false;
        }

        private static string Unescape(string s) => s.Replace("\\n", "\n");

        // Our labels draw no rich text (UiKit.Text): the game's own Japanese and Chinese wrap their lines in <nobr>, and a
        // translator copying that habit would see the tags; they are taken out (where a line breaks is TMP's then).
        internal static string Plain(string s)
        {
            if (s.IndexOf('<') < 0) return s;
            foreach (string tag in new[] { "<nobr>", "</nobr>" })
            {
                int i;
                while ((i = s.IndexOf(tag, StringComparison.OrdinalIgnoreCase)) >= 0) s = s.Remove(i, tag.Length);
            }
            return s;
        }

        private static string Escape(string s) => (s ?? "").Replace("\r", "").Replace("\n", "\\n");

        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static bool TestQuiet;      // the self-tests' own broken files: counted, not logged
        internal static int TestWarnings;

        // A problem in a translation file: said once per file and line (a broken file is worth fixing: no ignored warnings).
        private static void Warn(string path, int line, string what)
        {
            if (TestQuiet)
            {
                TestWarnings++;
                return;
            }
            if (!warned.Add(path + "\n" + line + "\n" + what)) return;
            Plugin.Log.LogWarning($"Translations: {path}{(line > 0 ? " line " + line : "")} {what}.");
        }

        // ---- what is there to translate, the log line, the English file, the report ----

        // An owner's texts: (key, English, what it is), in the windows' order, each key once. A page's: its title, its
        // sections, and every row's label, description, choices and button word that a window draws (a look row's
        // description is drawn nowhere: left out; the API's builders' rows are the API's words). The API's: UiWords.
        internal static List<(string Key, string English, string What)> EnglishOf(string owner)
        {
            var list = new List<(string, string, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Put(string key, string english, string what)
            {
                if (string.IsNullOrEmpty(english) || !seen.Add(key)) return;
                list.Add((key, english, what));
            }
            foreach (MountSettingsPage p in MountSettingsPage.All)
            {
                if (!string.Equals(p.OwnerId, owner, StringComparison.OrdinalIgnoreCase)) continue;
                Put("page.title", p.Title, "the page's title: the options window's title and the Settings window's button");
                foreach (MountSettingsPage.Row r in p.Rows)
                {
                    if (r.RowKind == MountSettingsPage.RowKind.Preview || p.IsMountRow(r)) continue;
                    Put("section." + Slug(r.ShownSection), r.ShownSection, "a section");
                }
                foreach (MountSettingsPage.Row r in p.Rows)
                {
                    if (r.RowKind == MountSettingsPage.RowKind.Preview || p.IsMountRow(r)) continue;
                    string where = r.ShownSection + ", " + KindWord(r);
                    if (r.LabelWord == null) Put(r.TextKey + ".label", r.Label, where);
                    if (p.InOptions(r)) Put(r.TextKey + ".desc", r.Description, where + ": its description");
                    // a custom colour's choices are the palette's names, drawn as swatches only (MountsPanel.SwatchRow): no words to translate
                    bool palette = r.Entry != null && r.Entry.SettingType == typeof(PaletteColor);
                    if (r.RowKind == MountSettingsPage.RowKind.Choice && r.ChoiceLabels != null && !palette)
                    {
                        for (int i = 0; i < r.ChoiceLabels.Length; i++) Put(r.TextKey + ".choice." + i.ToString(CultureInfo.InvariantCulture), r.ChoiceLabels[i], where + ": a choice");
                    }
                    if (r.RowKind == MountSettingsPage.RowKind.Button && r.ButtonWord == null) Put(r.TextKey + ".button", r.ButtonText, where + ": the word on its button");
                }
            }
            if (string.Equals(owner, Api, StringComparison.OrdinalIgnoreCase))
            {
                foreach (Word w in UiWords.All) Put(w.Key, w.English, w.What);
            }
            // the mounts and skins it registered (step 3), by id: the names and descriptions the journal, the colours
            // window and the Skin list show (Catalog.NameTerm, Looks.NameTerm); the donkey's are the API's words
            var mounts = new List<Catalog.Entry>();
            foreach (Catalog.Entry k in Catalog.All) if (k != Catalog.Donkey && string.Equals(k.Owner, owner, StringComparison.OrdinalIgnoreCase)) mounts.Add(k);
            mounts.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (Catalog.Entry k in mounts)
            {
                if (!string.IsNullOrEmpty(k.Name)) Put("mount." + k.Id + ".name", k.Name, "a mount's name (the Mount Journal; the keeper's bubbles)");
                Put("mount." + k.Id + ".description", k.Description, "a mount's description in the Mount Journal");
                foreach (MountAbility a in k.Abilities)
                {
                    if (!string.IsNullOrWhiteSpace(a.Name)) Put("mount." + k.Id + ".ability." + a.Id + ".name", a.Name, "an ability of the mount, in the Mount Journal");
                    Put("mount." + k.Id + ".ability." + a.Id + ".description", a.Description, "what the ability does, in the Mount Journal");
                }
            }
            var skins = new List<Looks.SkinDef>();
            foreach (Looks.SkinDef s in Looks.All) if (string.Equals(s.Owner, owner, StringComparison.OrdinalIgnoreCase)) skins.Add(s);
            skins.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (Looks.SkinDef s in skins)
            {
                // a mount's own look ("default") shows as the API's "Default" in the Skin list and nowhere by its name
                if (!string.IsNullOrEmpty(s.Name) && !s.LocalId.Equals(Looks.DefaultSkinName, StringComparison.OrdinalIgnoreCase)) Put("skin." + s.Id + ".name", s.Name, "a skin's name (the Skin list, the colours window's title)");
                // a skin's description shows only as its mount's, for a mount with none of its own, from its own look (the journal's
                // header, MountsPanel.Entries); any other is drawn nowhere (the first dump listed 8 such, 2026-11-01)
                Catalog.Entry m = Looks.MountOf(s);
                if (m != null && string.IsNullOrEmpty(m.Description) && string.Equals(Looks.DefaultSkinOf(m), s.Id, StringComparison.OrdinalIgnoreCase))
                    Put("skin." + s.Id + ".description", s.Description, "the mount's description in the Mount Journal (its own look's: the mount has none of its own)");
            }
            return list;
        }

        // Every owner with words to translate: the pages' and the API's, and every loaded mod or pack that registered a mount
        // or a skin (a self-test's made-up owner, not a plugin, has no file to ship).
        internal static List<string> Owners()
        {
            var list = new List<string> { Api };
            void Add(string o, bool plugin)
            {
                if (string.IsNullOrEmpty(o) || (plugin && !IsPlugin(o))) return;
                foreach (string have in list) if (string.Equals(have, o, StringComparison.OrdinalIgnoreCase)) return;
                list.Add(o);
            }
            foreach (MountSettingsPage p in MountSettingsPage.All) Add(p.OwnerId, plugin: false);
            foreach (Catalog.Entry k in Catalog.All) Add(k.Owner, plugin: true);
            foreach (Looks.SkinDef s in Looks.All) Add(s.Owner, plugin: true);
            return list;
        }

        private static bool IsPlugin(string owner)
        {
            try { return Chainloader.PluginInfos.ContainsKey(owner); }
            catch { return false; }
        }

        private static string KindWord(MountSettingsPage.Row r)
        {
            switch (r.RowKind)
            {
                case MountSettingsPage.RowKind.Toggle: return "a switch";
                case MountSettingsPage.RowKind.Choice: return "a choice";
                case MountSettingsPage.RowKind.List: return "a list";
                case MountSettingsPage.RowKind.Steps: return "a slider";
                case MountSettingsPage.RowKind.Key: return "a key";
                case MountSettingsPage.RowKind.Text: return "a text box";
                case MountSettingsPage.RowKind.Button: return "a button";
                case MountSettingsPage.RowKind.Info: return "an info line";
                case MountSettingsPage.RowKind.Note: return "a heading";
                default: return "a row";
            }
        }

        private static readonly HashSet<string> said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Once per owner and language: what was found, in the log; with [Translations] Report on, the lines still to
        // translate written out for the translator.
        private static void Said(string owner, string lang, Table t)
        {
            if (IsEnglish(lang) || !said.Add(owner + "\n" + lang)) return;
            List<(string Key, string English, string What)> all = EnglishOf(owner);
            var todo = new List<(string Key, string English, string What, string Was)>();
            int done = 0;
            foreach (var x in all)
            {
                if (t != null && t.Lines.TryGetValue(x.Key, out Line l))
                {
                    if (l.English == null || string.Equals(l.English, x.English, StringComparison.Ordinal)) { done++; continue; }
                    todo.Add((x.Key, x.English, x.What, l.Value));
                }
                else todo.Add((x.Key, x.English, x.What, null));
            }
            if (t != null)
            {
                int changed = 0;
                foreach (var x in todo) if (x.Was != null) changed++;
                // an owner without a page or words of its own (a made-up one of the self-tests): only what was read
                string use = all.Count > 0 ? $"; {done} of its {all.Count} lines translated, {todo.Count - changed} missing, {changed} with English that changed since" : "";
                Plugin.Log.LogInfo($"Translations: {owner} in {lang}: {t.Lines.Count} line(s) read from {string.Join(", ", t.Files)}{use}{(t.Malformed > 0 ? $", {t.Malformed} line(s) left out (the warnings say which)" : "")}.");
            }
            if (Plugin.TranslationReport == null || !Plugin.TranslationReport.Value || all.Count == 0 || (todo.Count == 0)) return;
            var sb = new StringBuilder();
            sb.Append("# ").Append(NameOf(owner)).Append(" (").Append(owner).Append("): the lines not yet translated into ").Append(lang).Append(", ")
              .Append(todo.Count).Append(" of ").Append(all.Count).Append(".\n");
            sb.Append("# Translate them and add them to ").Append(lang).Append(".txt (the README's Translations has where it goes). Keep the keys and the \"# en:\" lines.\n");
            foreach (var x in todo)
            {
                sb.Append('\n');
                if (x.What != null) sb.Append("# ").Append(x.What).Append('\n');
                if (x.Was != null) sb.Append("# changed: the English is new; it was translated as: ").Append(Escape(x.Was)).Append('\n');
                sb.Append("# en: ").Append(Escape(x.English)).Append('\n');
                sb.Append(x.Key).Append(" = ").Append(Escape(x.English)).Append('\n');
            }
            string path = Path.Combine(Path.Combine(Path.Combine(Paths.BepInExRootPath, "GK2-l10n"), owner), lang + ".missing.txt");
            if (WriteIfChanged(path, sb.ToString(), bom: false)) Plugin.Log.LogInfo($"Translations: {todo.Count} line(s) of {owner} still to translate into {lang}: written to {path}.");
        }

        private static string NameOf(string owner)
        {
            foreach (MountSettingsPage p in MountSettingsPage.All) if (string.Equals(p.OwnerId, owner, StringComparison.OrdinalIgnoreCase)) return p.Title;
            if (string.Equals(owner, Api, StringComparison.OrdinalIgnoreCase)) return "GK2 Donkey API";
            try
            {
                if (Chainloader.PluginInfos.TryGetValue(owner, out PluginInfo info) && !string.IsNullOrEmpty(info?.Metadata?.Name)) return info.Metadata.Name; // a pack: its plugin's name
            }
            catch
            {
                // the GUID then
            }
            return owner;
        }

        // A page shown: with the dev tools, the English of every owner written out (BepInEx\GK2-l10n\<owner>\en.txt and
        // en.csv: the page's, the API's, every mod's and pack's that registered a mount or a skin; tools/l10n-sync.py
        // copies them into the repository, where they ship as the translators' templates).
        internal static void Published(MountSettingsPage p)
        {
            if (!DevTools.Unlocked) return;
            foreach (string owner in Owners()) WriteEnglish(owner);
        }

        internal static void WriteEnglish(string owner)
        {
            try
            {
                List<(string Key, string English, string What)> lines = EnglishOf(owner);
                if (lines.Count == 0) return;
                string dir = Path.Combine(Path.Combine(Paths.BepInExRootPath, "GK2-l10n"), owner);
                bool txt = WriteIfChanged(Path.Combine(dir, "en.txt"), EnglishTxt(owner, lines), bom: false);
                bool csv = WriteIfChanged(Path.Combine(dir, "en.csv"), EnglishCsv(lines), bom: true);
                if (txt || csv) Plugin.Log.LogInfo($"Translations: {owner}'s English written ({lines.Count} lines) to {dir} (tools/l10n-sync.py takes it into the repository).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Translations: {owner}'s English couldn't be written ({e.Message}).");
            }
        }

        private static string EnglishTxt(string owner, List<(string Key, string English, string What)> lines)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(NameOf(owner)).Append(" (").Append(owner).Append("): its English, to translate.\n");
            sb.Append("# Copy this file to <language id>.txt beside it: de.txt, fr.txt, es.txt, pt-br.txt, ru.txt, pl.txt, tr.txt, ja.txt,\n");
            sb.Append("# zh_cn.txt or ko.txt (the game's own ids). Change the text after each \"=\"; keep the keys and the \"# en:\" lines\n");
            sb.Append("# (a line whose \"# en:\" isn't the mod's English any more shows in English until it is translated again). A line\n");
            sb.Append("# left out or left empty shows in English. Keep {0} and {1}: the mod fills them in. \\n is a line break.\n");
            sb.Append("# Save as UTF-8. The same lines are in en.csv, for a spreadsheet: either file works.\n");
            foreach (var x in lines)
            {
                sb.Append('\n');
                if (x.What != null) sb.Append("# ").Append(x.What).Append('\n');
                sb.Append("# en: ").Append(Escape(x.English)).Append('\n');
                sb.Append(x.Key).Append(" = ").Append(Escape(x.English)).Append('\n');
            }
            return sb.ToString();
        }

        private static string EnglishCsv(List<(string Key, string English, string What)> lines)
        {
            var sb = new StringBuilder("key,value,english\r\n");
            foreach (var x in lines)
            {
                sb.Append(CsvField(x.Key)).Append(',').Append(CsvField(x.English)).Append(',').Append(CsvField(x.English)).Append("\r\n");
            }
            return sb.ToString();
        }

        private static string CsvField(string s)
        {
            s = s ?? "";
            return s.IndexOfAny(new[] { ',', ';', '"', '\n', '\r' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        // Writes the file when its text differs from what is there (a dev run every start writes nothing new). True: written.
        private static bool WriteIfChanged(string path, string text, bool bom)
        {
            try
            {
                if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(bom));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Translations: {path} couldn't be written ({e.Message}).");
                return false;
            }
        }
    }
}
