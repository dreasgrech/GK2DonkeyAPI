using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GK2DonkeyAPI
{
    // A translation file as a person makes it, read whatever they did to it (the user, 2026-11-01: "other people aka
    // users will be adding the translations which means that we need to expect EVERY TYPE OF STUPID mistake they can
    // do"; docs/localization.md, "Translator mistakes", has the catalogue and the self-test checks every entry).
    // The rule over every repair: a mistake may show English, never a wrong text. A repair that could show a wrong text
    // is file-wide, needs most of the file to agree, and is said once.
    // - Found: <id>.csv in any case and with - or _ (pt-br, pt_BR), a language's name (french, français, deutsch, jp),
    //   the base language's (pt for pt-br, fr-FR for fr), a spreadsheet's other names (fr.csv.txt, fr.txt, fr.tsv: read
    //   as CSV, said); a workbook (fr.xlsx) or a file in the wrong folder is said, not read.
    // - Decoded: UTF-8 (with or without a BOM), UTF-16 (Excel's "Unicode Text"), else the Windows code page of the file's
    //   language (Excel's plain "CSV": LegacyText), else not read; UTF-8 that a spreadsheet opened as ANSI and saved
    //   again ("Ã©" for "é") is repaired.
    // - Parsed: the separator from a "key" header, Excel's sep= line, or counted on the first line (a header missing or
    //   translated); quotes only at a field's start (a quote inside a text is a quote); a quote never closed: the file
    //   read again line by line; a row Excel put whole into one cell split again; # rows are comments.
    // - Cleaned: zero-width spaces and BOMs out of keys; a literal \n is a line break; { 0 } and the full-width ｛０｝ of
    //   Japanese input are {0}; an Excel error cell (#NAME?) is no text.
    // Texts.Load does the rest with the owner's English: an unknown key found again by its english column, the value
    // and english columns swapped, the english column translated too.
    internal static class TranslationFiles
    {
        internal sealed class Row
        {
            internal string Key, Value, English;
            internal int Line;
        }

        internal sealed class File
        {
            internal string Path;
            internal string How;                         // how it was read, when not plain UTF-8 CSV (the log line's note)
            internal readonly List<Row> Rows = new List<Row>();
            internal int Malformed;
            internal bool Usable;                        // false: nothing read (a workbook, a code page we can't read)
        }

        private const int MaxWarningsPerFile = 10;
        internal static List<string> TestHeard; // the self-tests and tests/TranslationScan: the warnings a quiet read would have given
        private static readonly Dictionary<string, int> warningsOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> warnedOnce = new HashSet<string>(StringComparer.Ordinal);

        // A warning for the file's translator: each once, at most 10 a file, then one saying there are more.
        internal static void Warn(string path, int line, string what)
        {
            if (Texts.TestQuiet)
            {
                Texts.TestWarnings++;
                TestHeard?.Add($"{System.IO.Path.GetFileName(path)}{(line > 0 ? " line " + line : "")} {what}");
                return;
            }
            if (!warnedOnce.Add(path + "\n" + line + "\n" + what)) return;
            warningsOf.TryGetValue(path, out int n);
            warningsOf[path] = ++n;
            if (n <= MaxWarningsPerFile) Plugin.Log.LogWarning($"Translations: {path}{(line > 0 ? " line " + line : "")} {what}.");
            else if (n == MaxWarningsPerFile + 1) Plugin.Log.LogWarning($"Translations: {path} has more problems; only the first {MaxWarningsPerFile} are said.");
        }

        internal static void Forget()
        {
            warnedOnce.Clear();
            warningsOf.Clear();
        }

        // ---- which files ----

        // A language's names as people write them in a file's name, by the game's id (normalized: lower case, - for _).
        private static readonly Dictionary<string, string> NameToId = MakeNames();

        private static Dictionary<string, string> MakeNames()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, params string[] names) { foreach (string n in names) d[n] = id; }
            Add("en", "english", "eng");
            Add("fr", "french", "francais", "français", "fra", "fre");
            Add("de", "german", "deutsch", "deu", "ger");
            Add("es", "spanish", "espanol", "español", "castellano", "spa");
            Add("pt-br", "portuguese", "portugues", "português", "brazilian", "brasileiro", "portugues-brasil", "português-brasil", "por", "br");
            Add("ru", "russian", "russkiy", "русский", "rus");
            Add("pl", "polish", "polski", "pol");
            Add("tr", "turkish", "turkce", "türkçe", "tur");
            Add("ja", "japanese", "nihongo", "日本語", "jp", "jpn");
            Add("zh-cn", "chinese", "simplified-chinese", "chinese-simplified", "简体中文", "中文", "zh-hans", "chs", "cn", "zho", "chi");
            Add("ko", "korean", "한국어", "kr", "kor");
            return d;
        }

        private static string Norm(string id) => (id ?? "").Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');

        private static string BaseOf(string id)
        {
            int cut = id.IndexOf('-');
            return cut > 0 ? id.Substring(0, cut) : id;
        }

        // A file's name without what spreadsheets add: "fr.csv.txt" and "fr.csv.csv" are "fr".
        private static string StemOf(string fileName, out string ext)
        {
            ext = Path.GetExtension(fileName).ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(fileName);
            while (true)
            {
                string inner = Path.GetExtension(stem).ToLowerInvariant();
                if (inner != ".csv" && inner != ".txt" && inner != ".tsv") break;
                stem = Path.GetFileNameWithoutExtension(stem);
            }
            return stem;
        }

        private static readonly string[] TextExts = { ".csv", ".txt", ".tsv" };
        private static readonly string[] Workbooks = { ".xlsx", ".xls", ".xlsm", ".ods", ".numbers" };

        // How well a file's name matches the language: 0 its id, 1 a name of it, 2 its base language's; -1 not it.
        private static int Match(string stem, string lang)
        {
            string s = Norm(stem), l = Norm(lang);
            if (s.EndsWith("missing")) return -1; // a report copied back (fr.missing.csv): the English, nothing to read
            string id = NameToId.TryGetValue(s, out string named) ? named : s;
            if (id == l) return id == s ? 0 : 1;
            if (BaseOf(id) == BaseOf(l) && !Texts.IsEnglishId(id)) return 2;
            return -1;
        }

        // The files of the language in a folder, best first (the caller reads the tiers across folders in turn).
        internal static List<(string Path, int Tier)> FilesFor(string folder, string lang)
        {
            var list = new List<(string Path, int Tier)>();
            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch
            {
                return list;
            }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string stem = StemOf(Path.GetFileName(f), out string ext);
                int tier = Match(stem, lang);
                if (tier < 0) continue;
                if (Array.IndexOf(Workbooks, ext) >= 0)
                {
                    Warn(f, 0, "is a spreadsheet workbook, not a CSV: in Excel or LibreOffice, Save As \"CSV UTF-8\" (" + Path.GetFileNameWithoutExtension(f) + ".csv); not read");
                    continue;
                }
                if (Array.IndexOf(TextExts, ext) < 0) continue;
                string want = (Norm(stem) == Norm(lang) ? lang : NameToId.ContainsKey(Norm(stem)) ? NameToId[Norm(stem)].Replace("zh-cn", "zh_cn") : stem) + ".csv";
                if (ext != ".csv" || !Path.GetFileName(f).Equals(stem + ".csv", StringComparison.OrdinalIgnoreCase) || tier == 1)
                {
                    Warn(f, 0, $"is read as the CSV of {lang}; name it {want}");
                }
                list.Add((f, tier));
            }
            return list;
        }

        // Files of the language a translator put where nothing reads them: beside a DLL instead of in its Languages\, or
        // straight in a Translations\ folder without the mod's folder in it. Said once each, never read.
        internal static void LookForMisplaced(string folder, string lang, string rightPlace)
        {
            string[] files;
            try
            {
                if (!Directory.Exists(folder)) return;
                files = Directory.GetFiles(folder);
            }
            catch
            {
                return;
            }
            foreach (string f in files)
            {
                string stem = StemOf(Path.GetFileName(f), out string ext);
                if (Match(stem, lang) < 0 || (Array.IndexOf(TextExts, ext) < 0 && Array.IndexOf(Workbooks, ext) < 0)) continue;
                Warn(f, 0, "is in the wrong folder, where nothing reads it: put it in " + rightPlace);
            }
        }

        // ---- reading one file ----

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static File Read(string path, string lang)
        {
            var file = new File { Path = path };
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                string text = Decode(data, lang, file);
                if (text == null) return file;
                file.Usable = true;
                Parse(text, file);
            }
            catch (Exception e)
            {
                file.Usable = false;
                Warn(path, 0, "can't be read (" + e.Message + ")");
            }
            return file;
        }

        // The file's bytes as text: UTF-8, UTF-16 (a BOM, or zero bytes in every other place), the language's Windows code
        // page; null when none of them fits (said).
        private static string Decode(byte[] data, string lang, File file)
        {
            if (data.Length == 0) return "";
            if (data.Length >= 4 && data[0] == (byte)'P' && data[1] == (byte)'K' && data[2] == 3 && data[3] == 4)
            {
                Warn(file.Path, 0, "is a spreadsheet workbook saved with a .csv name: in Excel or LibreOffice, Save As \"CSV UTF-8\"; not read");
                return null;
            }
            string text;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                text = Encoding.Unicode.GetString(data, 2, data.Length - 2);
                file.How = "UTF-16";
            }
            else if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                text = Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
                file.How = "UTF-16";
            }
            else if (data.Length >= 4 && data[0] != 0 && data[1] == 0 && data[2] != 0 && data[3] == 0)
            {
                text = Encoding.Unicode.GetString(data); // UTF-16 without its BOM
                file.How = "UTF-16";
            }
            else
            {
                int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
                try
                {
                    text = StrictUtf8.GetString(data, start, data.Length - start);
                }
                catch (ArgumentException)
                {
                    string page = LegacyText.PageFor(lang, out string pageName);
                    if (page == null)
                    {
                        Warn(file.Path, 0, "isn't saved as UTF-8 and its letters can't be read for this language: save it as \"CSV UTF-8\"; not read");
                        return null;
                    }
                    text = LegacyText.Decode(data, start, page);
                    file.How = pageName;
                    Warn(file.Path, 0, $"isn't saved as UTF-8: read as {pageName}; save it as \"CSV UTF-8\" (in Excel: Save As, CSV UTF-8)");
                }
            }
            if (text.IndexOf('\0') >= 0) text = text.Replace("\0", "");
            text = Unmangle(text, file);
            if (text.IndexOf('\n') < 0 && text.IndexOf('\r') >= 0) text = text.Replace('\r', '\n'); // an old Mac's line ends
            return text;
        }

        // UTF-8 a spreadsheet opened as Windows-1252 and saved again as UTF-8: "Ã©" for "é", "â€™" for "’". Its characters
        // go back to bytes through the page and are read as UTF-8 again, when that works and leaves fewer of them.
        private static string Unmangle(string text, File file)
        {
            int marks = Mangled(text);
            if (marks < 3) return text;
            if (!LegacyText.Encode(text, LegacyText.Western, out byte[] bytes)) return text;
            string back;
            try
            {
                back = StrictUtf8.GetString(bytes);
            }
            catch (ArgumentException)
            {
                return text;
            }
            if (Mangled(back) >= marks) return text;
            file.How = (file.How != null ? file.How + ", " : "") + "letters repaired";
            Warn(file.Path, 0, "was opened as ANSI and saved again (\"Ã©\" for \"é\"): its letters were repaired; save it as \"CSV UTF-8\" and open it as UTF-8");
            return back;
        }

        private static int Mangled(string s)
        {
            int n = 0;
            for (int i = 0; i + 1 < s.Length; i++)
            {
                char c = s[i], d = s[i + 1];
                if ((c == 'Ã' || c == 'Â' || c == 'Ð' || c == 'Ñ' || c == 'Å' || c == 'Ä') && ((d >= '\u0080' && d <= '¿') || d == '€' || d == '™' || d == 'œ' || d == 'š' || d == 'ž' || d == 'Ÿ' || (d >= '‘' && d <= '„'))) n++;
                else if (c == 'â' && d == '€') n++;
            }
            return n;
        }

        // ---- parsing ----

        private static void Parse(string text, File file)
        {
            int start = 0, line = 1;
            string first = FirstLine(text, 0);
            char sep;
            if (first.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && first.Length >= 5)
            {
                sep = first[4]; // Excel's own line naming the separator
                start = NextLine(text, 0);
                line = 2;
            }
            else sep = Sniff(first);
            List<(List<string> Fields, int Line)> rows = ParseCsv(text, start, line, sep, out bool unclosed);
            int swallowedAt = -1;
            foreach (var (fields, at) in rows)
            {
                if (fields.Count > 1 && Swallowed(fields[1], sep)) { swallowedAt = at; break; }
            }
            if (unclosed || swallowedAt >= 0)
            {
                Warn(file.Path, swallowedAt >= 0 ? swallowedAt : rows.Count > 0 ? rows[rows.Count - 1].Line : line,
                    "has a quote that runs past its line and takes the lines after it (a \" in a text is written \"\"): the file is read line by line instead");
                rows = ParseByLine(text, start, line, sep, file);
            }
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fields0, at) in rows)
            {
                List<string> f = fields0;
                if (f.Count == 1 && f[0].IndexOfAny(new[] { ',', ';', '\t' }) >= 0)
                {
                    // a whole row in one cell (Excel in a ; country opening a , file, then saving): split again
                    var again = ParseCsv(f[0], 0, at, Sniff(f[0]), out _);
                    if (again.Count == 1 && again[0].Fields.Count >= 2) f = again[0].Fields;
                }
                string key = CleanKey(f[0]);
                if (key.Length == 0 || key[0] == '#' || key.Equals("key", StringComparison.OrdinalIgnoreCase) || key.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) continue; // empty, a comment, a header (also a second one, files put together)
                if (f.Count < 2)
                {
                    file.Malformed++;
                    Warn(file.Path, at, "has a key but no translation after it (the columns are key, value, english, separated by " + SepName(sep) + "); left out");
                    continue;
                }
                string value = Clean(f[1]);
                if (IsSheetError(value))
                {
                    file.Malformed++;
                    Warn(file.Path, at, $"has a spreadsheet error instead of a text ({value}: a text starting with = + - or @ is a formula to Excel; start it with an apostrophe); left out");
                    continue;
                }
                if (seen.TryGetValue(key, out int firstAt))
                {
                    Warn(file.Path, at, $"has {key} a second time (first on line {firstAt}): the first is used");
                    continue;
                }
                seen[key] = at;
                string english = f.Count > 2 ? Clean(f[2]) : "";
                file.Rows.Add(new Row { Key = key, Value = value, English = english.Length > 0 ? english : null, Line = at });
            }
        }

        private static string SepName(char sep) => sep == '\t' ? "a tab" : sep.ToString();

        // The separator of a file without a "key" header (missing, or translated: "clé;valeur"): the one the first line
        // has most of outside quotes; a tie goes to tab, then ;, then , (a text has commas more often than tabs).
        private static char Sniff(string firstLine)
        {
            if (firstLine.StartsWith("key", StringComparison.OrdinalIgnoreCase) && firstLine.Length > 3)
            {
                char c = firstLine[3];
                if (c == ',' || c == ';' || c == '\t') return c;
            }
            int comma = 0, semi = 0, tab = 0;
            bool quoted = false;
            for (int i = 0; i < firstLine.Length; i++)
            {
                char c = firstLine[i];
                if (c == '"') quoted = !quoted;
                else if (!quoted)
                {
                    if (c == ',') comma++;
                    else if (c == ';') semi++;
                    else if (c == '\t') tab++;
                }
            }
            if (tab > 0 && tab >= semi && tab >= comma) return '\t';
            if (semi > 0 && semi >= comma) return ';';
            return ',';
        }

        // The game's ModsCsv rules, forgiving: a quote opens a quoted field only at the field's start; in it, "" is a
        // quote and a quote before the separator or the line's end closes it, any other quote is a quote.
        private static List<(List<string> Fields, int Line)> ParseCsv(string text, int start, int line, char sep, out bool unclosed)
        {
            var rows = new List<(List<string>, int)>();
            var row = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false, fieldStart = true;
            int rowLine = line;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        char next = i + 1 < text.Length ? text[i + 1] : '\n';
                        if (next == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else if (ClosesHere(text, i + 1, sep)) quoted = false; // before the separator or the line's end, spaces between allowed
                        else sb.Append('"'); // a quote inside a quoted text that doesn't end it
                    }
                    else
                    {
                        if (c == '\n') line++;
                        if (c != '\r') sb.Append(c);
                    }
                    continue;
                }
                if (c == '"' && fieldStart)
                {
                    quoted = true;
                    fieldStart = false;
                }
                else if (c == sep)
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                    fieldStart = true;
                }
                else if (c == '\n')
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                    if (row.Count > 1 || row[0].Trim().Length > 0) rows.Add((row, rowLine));
                    row = new List<string>();
                    line++;
                    rowLine = line;
                    fieldStart = true;
                }
                else if (c != '\r')
                {
                    if (!(fieldStart && (c == ' ' || c == ' '))) fieldStart = false; // spaces before a quote: still the field's start
                    sb.Append(c);
                }
            }
            unclosed = quoted;
            if (quoted || sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                if (row.Count > 1 || row[0].Trim().Length > 0) rows.Add((row, rowLine));
            }
            return rows;
        }

        // A closing quote: only spaces between it and the separator, the line's end or the text's end.
        private static bool ClosesHere(string text, int from, char sep)
        {
            for (int k = from; k < text.Length; k++)
            {
                char c = text[k];
                if (c == sep || c == '\n' || c == '\r') return true;
                if (c != ' ' && c != '\u00A0') return false;
            }
            return true;
        }

        // A text that swallowed rows: a quote opened and closed later than meant, so lines inside it start like a row of
        // the file (a key, then the separator).
        private static bool Swallowed(string value, char sep)
        {
            int at = value.IndexOf('\n');
            while (at >= 0)
            {
                int k = at + 1;
                if (k < value.Length && char.IsLetter(value[k]))
                {
                    int j = k;
                    while (j < value.Length && (char.IsLetterOrDigit(value[j]) || value[j] == '.' || value[j] == '_' || value[j] == '-' || value[j] == '/')) j++;
                    if (j < value.Length && j - k >= 2 && value[j] == sep && value.Substring(k, j - k).IndexOf('.') > 0) return true;
                }
                at = value.IndexOf('\n', at + 1);
            }
            return false;
        }

        // A file whose quote never closes: each line on its own (no text across lines), so one bad line costs one line.
        // A line whose own quote never closes is left out (its key shows English) and said.
        private static List<(List<string> Fields, int Line)> ParseByLine(string text, int start, int line, char sep, File file)
        {
            var rows = new List<(List<string>, int)>();
            int at = start;
            while (at < text.Length)
            {
                string one = FirstLine(text, at);
                var parsed = ParseCsv(one, 0, line, sep, out bool open);
                if (open)
                {
                    file.Malformed++;
                    Warn(file.Path, line, "has a quote that is never closed on this line (a \" in a text is written \"\"); left out");
                }
                else if (parsed.Count > 0) rows.Add((parsed[0].Fields, line));
                at = NextLine(text, at);
                line++;
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

        // ---- cleaning ----

        private static readonly char[] Invisible = { '﻿', '​', '‌', '‍', '⁠', '­' };

        // A key as written, without the invisible characters a copy from a web page or a second file's BOM leaves in it.
        internal static string CleanKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(Invisible) >= 0)
            {
                var sb = new StringBuilder(s.Length);
                foreach (char c in s) if (Array.IndexOf(Invisible, c) < 0) sb.Append(c);
                s = sb.ToString();
            }
            return s.Trim();
        }

        // A text as meant: trimmed, \n a line break, a tab a space, the <nobr> tags out (Texts.Plain), and {0} however it
        // was typed: { 0 }, {０} and ｛０｝ (Japanese and Chinese input), {O} (a letter O).
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = CleanKey(s);
            if (s.IndexOf("\\n", StringComparison.Ordinal) >= 0) s = s.Replace("\\n", "\n");
            if (s.IndexOf('\t') >= 0) s = s.Replace('\t', ' ');
            if (s.IndexOf('\r') >= 0) s = s.Replace("\r", "");
            s = Texts.Plain(s);
            if (s.IndexOf('{') >= 0 || s.IndexOf('｛') >= 0) s = Placeholders(s);
            return s;
        }

        private static string Placeholders(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '{' && c != '｛')
                {
                    sb.Append(c);
                    continue;
                }
                // what is inside, up to the closing brace on this line
                int end = -1;
                for (int j = i + 1; j < s.Length && j < i + 8; j++)
                {
                    if (s[j] == '}' || s[j] == '｝') { end = j; break; }
                    if (s[j] == '\n') break;
                }
                if (end < 0)
                {
                    sb.Append('{');
                    continue;
                }
                var digits = new StringBuilder();
                bool ok = true;
                for (int j = i + 1; j < end; j++)
                {
                    char d = s[j];
                    if (d >= '０' && d <= '９') d = (char)('0' + (d - '０'));
                    else if (d == 'O' || d == 'o') d = '0';
                    if (d >= '0' && d <= '9') digits.Append(d);
                    else if (d != ' ' && d != ' ' && d != '　') { ok = false; break; }
                }
                if (ok && digits.Length > 0)
                {
                    sb.Append('{').Append(digits).Append('}');
                    i = end;
                }
                else sb.Append('{');
            }
            return sb.ToString();
        }

        private static readonly string[] SheetErrors = { "#NAME?", "#VALUE!", "#REF!", "#DIV/0!", "#N/A", "#NUM!", "#NULL!", "#SPILL!", "#CALC!", "#FIELD!", "#GETTING_DATA", "Err:5", "#NOM?", "#VALEUR!", "#NOMBRE!", "#NV", "#WERT!", "#ZAHL!", "#BEZUG!" };

        private static bool IsSheetError(string value)
        {
            foreach (string e in SheetErrors) if (value.Equals(e, StringComparison.OrdinalIgnoreCase)) return true;
            return value.StartsWith("Err:", StringComparison.OrdinalIgnoreCase) && value.Length <= 8; // LibreOffice
        }

        // The {n} of a text, as a sorted list ("{0}{1}"): a translation must keep the English's.
        internal static string PlaceholdersOf(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return "";
            var found = new List<string>();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '{') continue;
                int j = i + 1;
                while (j < s.Length && s[j] >= '0' && s[j] <= '9') j++;
                if (j > i + 1 && j < s.Length && s[j] == '}')
                {
                    string p = s.Substring(i, j - i + 1);
                    if (!found.Contains(p)) found.Add(p);
                }
            }
            found.Sort(StringComparer.Ordinal);
            return string.Concat(found);
        }
    }
}
