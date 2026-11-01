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
    // An owner's translations are CSV files, <id>.csv, where id is the game's language id (de, fr, pt-br, zh_cn, or a
    // community language pack's) (CSV only since 2026-11-01, the user: "less to maintain"; .txt was read too before):
    // - bundled: in its DLL's folder, Languages\;
    // - a translator's own download: in any plugin folder, Translations\<owner guid>\ (it wins over the bundled one).
    // Its columns are key,value,english (a note after them, the English template's context, is not read), the separator
    // that of its header line (, ; or a tab: European Excel saves with ;), quoted as the game's own strings.csv (a field
    // in quotes may hold the separator, line breaks and "" for a quote). The language's id is tried, then the part before
    // its first - or _ (pt-br: pt), then the English in the code.
    // A line whose recorded English isn't the code's any more shows the English until it is translated again; a line
    // left out or empty shows the English. The cfg never changes language: BepInEx writes the descriptions Bind got, in
    // Awake, before any language.
    internal static class Texts
    {
        internal const string Api = Plugin.Guid;
        private const float TestGrowth = 1.6f; // the test language: 1.6 times as long (between German's and French's 90th percentile for short labels)

        internal sealed class Line
        {
            internal string Value, English, Source;
            internal int Template;   // 0 not checked yet, 1 its {n} the English's, 2 not (Fill: the English shown)
        }

        // An owner's translation in one language: its lines by key (any case: a translator's "Options.Done" is
        // "options.done"), the files read and what was wrong with them (TranslationFiles; the log line says it).
        internal sealed class Table
        {
            internal readonly Dictionary<string, Line> Lines = new Dictionary<string, Line>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<string> Files = new List<string>();
            internal readonly List<string> Notes = new List<string>();
            internal int Malformed, Unknown, Rescued;
        }

        // ---- the self-tests' handles ----
        internal static string LanguageOverride;  // a language id in place of the game's
        internal static bool? TestForced;          // the test language on or off whatever Debug.TestLanguage says
        internal static readonly Dictionary<string, string> TestBundled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // owner: its Languages folder
        internal static List<string> TestTranslators;  // plugin folders to look in for Translations\, in place of BepInEx\plugins\*
        internal static readonly Dictionary<string, List<string>> TestNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // a made-up owner's other folder names
        internal static readonly Dictionary<string, List<(string Key, string English, string What)>> TestEnglish = new Dictionary<string, List<(string, string, string)>>(StringComparer.OrdinalIgnoreCase); // a made-up owner's English

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
        internal static string Of(string owner, string key, string english) => Lookup(owner, key, english, out _);

        private static string Lookup(string owner, string key, string english, out Line line)
        {
            line = null;
            if (string.IsNullOrEmpty(english)) return english; // nothing stays nothing (a heading is told from a note by its empty description)
            if (TestOn) return Test(english);
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(key)) return english;
            Table t = TableOf(owner);
            if (t == null || !t.Lines.TryGetValue(key, out Line l)) return english;
            if (l.English != null && !SameEnglish(l.English, english)) return english; // the English changed since it was translated
            line = l;
            string v = l.Value;
            if (v.IndexOf('\n') >= 0 && english.IndexOf('\n') < 0) v = v.Replace('\n', ' '); // a line break typed into a one-line text
            return v;
        }

        // The english column as the mod's English (a translator's copy may differ in spaces, \n or a {0}'s spelling only).
        private static bool SameEnglish(string recorded, string english) =>
            string.Equals(recorded, english, StringComparison.Ordinal) || string.Equals(recorded, TranslationFiles.Clean(english), StringComparison.Ordinal);

        // A template ({0}, {1}) in the language now, filled in; a translation whose template doesn't take the values: the English.
        internal static string Format(string owner, string key, string english, params object[] args) => Fill(owner, key, english, false, args);

        // The same; mayDropValues: a translation may leave a value out (a noun a gendered language can't put in, UiWords).
        // A translation that lost a {n} the English has (the number, the name) shows the English instead.
        internal static string Fill(string owner, string key, string english, bool mayDropValues, object[] args)
        {
            string t = Lookup(owner, key, english, out Line line);
            if (line != null)
            {
                if (line.Template == 0) line.Template = KeepsValues(t, english, mayDropValues) ? 1 : 2;
                if (line.Template == 2)
                {
                    if (TestQuiet) TestWarnings++;
                    else if (badTemplates.Add(owner + "\n" + key))
                        Plugin.Log.LogWarning($"Translations: {owner}'s line {key} in {Language} ({line.Source}) hasn't the {TranslationFiles.PlaceholdersOf(english)} of its English ({t}): the mod fills them in; the English is shown until it has them.");
                    t = english;
                }
            }
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

        // Whether a translation has every {n} of its English (all of them may go when mayDropValues) and no other.
        private static bool KeepsValues(string translation, string english, bool mayDropValues)
        {
            string have = TranslationFiles.PlaceholdersOf(translation), need = TranslationFiles.PlaceholdersOf(english);
            if (have == need) return true;
            if (!mayDropValues) return false;
            for (int i = 0; i < have.Length;)
            {
                int end = have.IndexOf('}', i);
                if (end < 0 || need.IndexOf(have.Substring(i, end - i + 1), StringComparison.Ordinal) < 0) return false;
                i = end + 1;
            }
            return true;
        }

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

        // The tables read ahead, one owner a frame, once a game is ready and again whenever the language changed since:
        // the first prompt or bubble in a language never waits for files in the middle of play (the registry's tick).
        private static string warmedFor;
        private static List<string> warmQueue;

        internal static void TickWarm()
        {
            try
            {
                if (!Mount.IsGameReady) return;
                string lang = Language;
                if (!string.Equals(lang, warmedFor, StringComparison.OrdinalIgnoreCase))
                {
                    warmedFor = lang;
                    warmQueue = IsEnglishId(lang) ? null : Owners();
                }
                if (warmQueue == null || warmQueue.Count == 0) return;
                string owner = warmQueue[warmQueue.Count - 1];
                warmQueue.RemoveAt(warmQueue.Count - 1);
                TableOf(owner);
            }
            catch (Exception e)
            {
                warmQueue = null;
                Plugin.Log.LogWarning("Translations: reading ahead failed (" + e.Message + "); the files are read when first needed.");
            }
        }

        // The tables read again at the next lookup (the self-tests, after writing their files).
        internal static void Forget()
        {
            tables.Clear();
            tablesLanguage = null;
            translatorRoots = null;
            said.Clear();
            badTemplates.Clear();
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
            try
            {
                t = Load(owner, lang);
            }
            catch (Exception e)
            {
                t = null; // whatever a file did: the English
                if (TestQuiet) { TestWarnings++; TranslationFiles.TestHeard?.Add($"{owner} {lang}: {e.GetType().Name}: {e.Message}"); }
                else Plugin.Log.LogWarning($"Translations: {owner}'s {lang} couldn't be read ({e.GetType().Name}: {e.Message}); its English is shown.");
            }
            tables[owner] = t;
            Said(owner, lang, t);
            return t;
        }

        // The owner's translation in lang: its files in each folder (the translators' first, then the bundled one), the
        // language's own files first, then a language name's, then the base language's (TranslationFiles.FilesFor), each
        // read as forgivingly as TranslationFiles can and checked against the owner's English (Take). Null: none.
        private static Table Load(string owner, string lang)
        {
            if (string.IsNullOrEmpty(lang) || IsEnglishId(lang)) return null;
            List<string> folders = FoldersOf(owner);
            LookForMisplaced(owner, lang);
            if (folders.Count == 0) return null;
            var english = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var byEnglish = new Dictionary<string, string>(StringComparer.Ordinal); // an English to its key, when one key has it
            foreach (var x in EnglishOf(owner))
            {
                english[x.Key] = x.English;
                string e = TranslationFiles.Clean(x.English);
                byEnglish[e] = byEnglish.ContainsKey(e) ? null : x.Key;
            }
            var found = new List<List<(string Path, int Tier)>>();
            foreach (string dir in folders) found.Add(TranslationFiles.FilesFor(dir, lang));
            var t = new Table();
            for (int tier = 0; tier <= 2; tier++)
            {
                foreach (var files in found)
                {
                    foreach (var (path, fileTier) in files)
                    {
                        if (fileTier != tier) continue;
                        TranslationFiles.File f = TranslationFiles.Read(path, lang);
                        if (!f.Usable) continue;
                        t.Files.Add(path);
                        t.Malformed += f.Malformed;
                        if (f.How != null) t.Notes.Add(Path.GetFileName(path) + ": " + f.How);
                        Take(f, english, byEnglish, t);
                    }
                }
            }
            if (t.Files.Count > 0) return t;
            EnglishEditedInPlace(lang, folders);
            return null;
        }

        // One file's rows into the table (a key a better file had is kept), checked against the owner's English:
        // - the value and english columns the other way round: swapped back (most rows of the file must say so);
        // - the english column translated too (it equals the value on most rows): the file's English isn't checked;
        // - a key the owner doesn't have: found again by its english column when one key has that English, else left out.
        // A file the English doesn't know yet (an owner whose page isn't built) is taken as it is.
        private static void Take(TranslationFiles.File f, Dictionary<string, string> english, Dictionary<string, string> byEnglish, Table t)
        {
            bool know = english.Count > 0;
            int withEnglish = 0, swapped = 0, doubled = 0;
            if (know)
            {
                foreach (TranslationFiles.Row r in f.Rows)
                {
                    if (r.English == null || !english.TryGetValue(r.Key, out string e)) continue;
                    withEnglish++;
                    string ce = TranslationFiles.Clean(e);
                    if (r.Value == ce && r.English != ce) swapped++;
                    else if (r.English == r.Value && r.Value != ce) doubled++;
                }
            }
            bool swap = swapped >= 3 && swapped * 2 > withEnglish;
            bool trust = !swap && doubled >= 3 && doubled * 2 > withEnglish;
            if (swap) TranslationFiles.Warn(f.Path, 0, "has its value and english columns the other way round (the translations are in the english column): read swapped; keep the columns as key, value, english");
            if (trust) TranslationFiles.Warn(f.Path, 0, "has the translations in its english column too, which tells the mod which English each line translates: read as translated; copy the english column back from en.csv");
            var unknown = new List<string>();
            foreach (TranslationFiles.Row r in f.Rows)
            {
                string key = r.Key, value = swap ? r.English : r.Value, eng = swap ? r.Value : trust ? null : r.English;
                if (string.IsNullOrEmpty(value)) continue;
                if (know && !english.ContainsKey(key))
                {
                    if (key.IndexOf('.') < 0 && r.Line <= 2) continue; // a header in other words ("clé;valeur;anglais")
                    string again = eng != null && byEnglish.TryGetValue(eng, out string k) ? k : null;
                    if (again == null)
                    {
                        unknown.Add(key);
                        continue;
                    }
                    TranslationFiles.Warn(f.Path, r.Line, $"has the key {key}, which the mod doesn't have: read as {again}, found by its English; keep the key column as en.csv has it");
                    key = again;
                    t.Rescued++;
                }
                if (t.Lines.ContainsKey(key)) continue;
                t.Lines[key] = new Line { Value = value, English = eng, Source = Path.GetFileName(f.Path) + ":" + r.Line };
            }
            if (unknown.Count > 0)
            {
                t.Unknown += unknown.Count;
                var shown = new List<string>();
                for (int i = 0; i < unknown.Count && i < 6; i++) shown.Add(unknown[i]);
                TranslationFiles.Warn(f.Path, 0, $"has {unknown.Count} key(s) the mod doesn't have ({string.Join(", ", shown)}{(unknown.Count > 6 ? ", ..." : "")}): those lines are left out; keep the key column as en.csv has it");
            }
        }

        // No file for the language, but an en.csv someone translated where it lies: said (en.csv is the mod's English,
        // never read; the translation belongs in <lang>.csv beside it).
        private static void EnglishEditedInPlace(string lang, List<string> folders)
        {
            foreach (string dir in folders)
            {
                string en = Path.Combine(dir, "en.csv");
                if (!File.Exists(en)) continue;
                TranslationFiles.File f = TranslationFiles.Read(en, "en");
                int changed = 0;
                foreach (TranslationFiles.Row r in f.Rows) if (r.English != null && r.Value != r.English) changed++;
                if (changed >= 3) TranslationFiles.Warn(en, 0, $"has {changed} line(s) whose value isn't the English: en.csv is the mod's English and is never read; save the translation as {lang}.csv beside it");
            }
        }

        internal static bool IsEnglishId(string id)
        {
            string s = (id ?? "").Trim().ToLowerInvariant();
            return s == "en" || s.StartsWith("en-") || s.StartsWith("en_");
        }

        // Where an owner's translations are, in their order: the translators' downloads (Translations\<the mod's GUID>\,
        // or the mod's folder or plugin name, as people write it), then the bundled folder.
        private static List<string> FoldersOf(string owner)
        {
            var list = new List<string>();
            List<string> names = NamesOf(owner);
            foreach (string plugin in TranslatorRoots())
            {
                foreach (string name in names)
                {
                    string d = Path.Combine(Path.Combine(plugin, "Translations"), name);
                    if (!Directory.Exists(d) || list.Contains(d)) continue;
                    list.Add(d);
                    if (!string.Equals(name, owner, StringComparison.OrdinalIgnoreCase) && !TestQuiet && aliasSaid.Add(d))
                        Plugin.Log.LogInfo($"Translations: {d} is read for {owner} (the folder named after the mod; its GUID, {owner}, is the name to use).");
                }
            }
            string bundled = BundledFolder(owner);
            if (bundled != null && Directory.Exists(bundled)) list.Add(bundled);
            return list;
        }

        private static readonly HashSet<string> aliasSaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The names a translator may give the owner's folder under Translations\: its GUID, its DLL's folder and file
        // names, its plugin name.
        private static List<string> NamesOf(string owner)
        {
            var names = new List<string> { owner };
            if (TestNames.TryGetValue(owner, out List<string> more)) names.AddRange(more);
            try
            {
                if (Chainloader.PluginInfos.TryGetValue(owner, out PluginInfo p) && p != null)
                {
                    if (!string.IsNullOrEmpty(p.Location))
                    {
                        string folder = Path.GetFileName(Path.GetDirectoryName(p.Location));
                        if (!string.IsNullOrEmpty(folder) && !folder.Equals("plugins", StringComparison.OrdinalIgnoreCase)) names.Add(folder);
                        names.Add(Path.GetFileNameWithoutExtension(p.Location));
                    }
                    if (!string.IsNullOrEmpty(p.Metadata?.Name)) names.Add(p.Metadata.Name);
                }
            }
            catch
            {
                // the GUID only
            }
            return names;
        }

        // A translation of the language beside the DLL instead of in its Languages\, or straight in a Translations\ folder
        // without the mod's folder: said once each (TranslationFiles.LookForMisplaced).
        private static void LookForMisplaced(string owner, string lang)
        {
            string bundled = BundledFolder(owner);
            if (bundled != null) TranslationFiles.LookForMisplaced(Path.GetDirectoryName(bundled), lang, bundled + "\\");
            foreach (string plugin in TranslatorRoots())
            {
                TranslationFiles.LookForMisplaced(Path.Combine(plugin, "Translations"), lang, "a folder named after the GUID of the mod it translates, inside this Translations folder (the README lists them)");
            }
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

        internal static bool TestQuiet;      // the self-tests' own broken files: counted, not logged
        internal static int TestWarnings;

        // A problem in a translation file: said once per file and line (a broken file is worth fixing: no ignored warnings).
        // ---- what is there to translate, the log line, the English file, the report ----

        // An owner's texts: (key, English, what it is), in the windows' order, each key once. A page's: its title, its
        // sections, and every row's label, description, choices and button word that a window draws (a look row's
        // description is drawn nowhere: left out; the API's builders' rows are the API's words). The API's: UiWords.
        internal static List<(string Key, string English, string What)> EnglishOf(string owner)
        {
            if (TestEnglish.TryGetValue(owner, out var made)) return made;
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
            if (IsEnglishId(lang) || !said.Add(owner + "\n" + lang)) return;
            List<(string Key, string English, string What)> all = EnglishOf(owner);
            var todo = new List<(string Key, string English, string What, string Was)>();
            int done = 0;
            foreach (var x in all)
            {
                if (t != null && t.Lines.TryGetValue(x.Key, out Line l))
                {
                    if (l.English == null || SameEnglish(l.English, x.English)) { done++; continue; }
                    todo.Add((x.Key, x.English, x.What, l.Value));
                }
                else todo.Add((x.Key, x.English, x.What, null));
            }
            if (t != null && !TestQuiet)
            {
                int changed = 0;
                foreach (var x in todo) if (x.Was != null) changed++;
                // an owner without a page or words of its own (a made-up one of the self-tests): only what was read
                string use = all.Count > 0 ? $"; {done} of its {all.Count} lines translated, {todo.Count - changed} missing, {changed} with English that changed since" : "";
                Plugin.Log.LogInfo($"Translations: {owner} in {lang}: {t.Lines.Count} line(s) read from {string.Join(", ", t.Files)}{use}"
                    + (t.Malformed > 0 ? $", {t.Malformed} line(s) left out" : "") + (t.Unknown > 0 ? $", {t.Unknown} unknown key(s) left out" : "")
                    + (t.Rescued > 0 ? $", {t.Rescued} key(s) found again by their English" : "") + (t.Notes.Count > 0 ? $" ({string.Join("; ", t.Notes)})" : "")
                    + (t.Malformed + t.Unknown + t.Rescued > 0 || t.Notes.Count > 0 ? " (the warnings say more)" : "") + ".");
            }
            if (Plugin.TranslationReport == null || !Plugin.TranslationReport.Value || all.Count == 0 || (todo.Count == 0)) return;
            // the rows still to translate, in the English template's columns: the value to replace, the English, the note
            // (and, for a line whose English changed, what it was translated as): ready to fill in and add to <lang>.csv
            var rows = new List<(string Key, string English, string What)>();
            foreach (var x in todo)
            {
                string note = x.Was != null ? "changed: the English is new; it was translated as: " + x.Was + (x.What != null ? " | " + x.What : "") : x.What;
                rows.Add((x.Key, x.English, note));
            }
            string path = Path.Combine(Path.Combine(Path.Combine(Paths.BepInExRootPath, "GK2-l10n"), owner), lang + ".missing.csv");
            if (WriteIfChanged(path, EnglishCsv(rows), bom: true)) Plugin.Log.LogInfo($"Translations: {todo.Count} of {all.Count} line(s) of {owner} ({NameOf(owner)}) still to translate into {lang}: written to {path}.");
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

        // A page shown: with the dev tools, the English of every owner written out (BepInEx\GK2-l10n\<owner>\en.csv: the
        // page's, the API's, every mod's and pack's that registered a mount or a skin; tools/l10n-sync.py copies them into
        // the repository, where they ship as the translators' templates).
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
                if (WriteIfChanged(Path.Combine(dir, "en.csv"), EnglishCsv(lines), bom: true)) Plugin.Log.LogInfo($"Translations: {owner}'s English written ({lines.Count} lines) to {dir} (tools/l10n-sync.py takes it into the repository).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Translations: {owner}'s English couldn't be written ({e.Message}).");
            }
        }

        // key,value,english,note: the value is the English to replace, the english column the English it was translated from
        // (a translation's line whose english isn't the code's any more shows the English), the note where it shows and
        // what its {0} is (not read back). A BOM and CRLF: Excel opens it as UTF-8.
        internal static string EnglishCsv(List<(string Key, string English, string What)> lines)
        {
            var sb = new StringBuilder("key,value,english,note\r\n");
            foreach (var x in lines)
            {
                sb.Append(CsvField(x.Key)).Append(',').Append(CsvField(x.English)).Append(',').Append(CsvField(x.English)).Append(',').Append(CsvField(x.What)).Append("\r\n");
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
