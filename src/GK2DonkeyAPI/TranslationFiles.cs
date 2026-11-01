using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GK2DonkeyAPI
{
    // A translation file as a person makes it, read whatever they did to it (the user, 2026-11-01: "other people aka
    // users will be adding the translations which means that we need to expect EVERY TYPE OF STUPID mistake they can
    // do"; docs/localization.md, "Translator mistakes", has the catalogue and the self-test checks every entry).
    // The rule: the mod never shows a text that isn't in the file, nor one for an English it wasn't made for; a mistake
    // shows English, and is said. Since code review round 6 (2026-11-01) the mod repairs nothing in a file (six rounds
    // each found a repair that made a wrong text in a corner: RepairGarbled, Texts.RescueByEnglish, Texts.JoinSeparator
    // and ReadCodePages are the user's switches, off); garbled text it detects is left out, said, and garbled text it
    // doesn't is shown as the file has it, for the translator to see.
    // - Found: <id>.csv in any case and with - or _ (pt-br, pt_BR), a language's name (french, français, deutsch, jp),
    //   the base language's or a region of it (pt for pt-br, fr-FR for fr; never zh-TW for zh_cn), a spreadsheet's other
    //   names (fr.csv.txt, fr.txt, fr.tsv: read as CSV, said); a workbook (fr.xlsx), a copy ("fr - Copy.csv", fr_old.csv,
    //   "fr (1).csv") or a file in the wrong folder is said, not read.
    // - Decoded: UTF-16 (Excel's "Unicode Text": a BOM, or zero bytes in one of a pair's places), UTF-8 (with or without
    //   a BOM); any other encoding (Excel's plain "CSV", "CSV (MS-DOS)", "CSV (Macintosh)", a Linux page) isn't read,
    //   said "save it as CSV UTF-8" (ReadCodePages: five review rounds each found a file a code-page reader showed wrong;
    //   the reader, Paged, is kept for the user's decision). UTF-8 that a spreadsheet opened in a code
    //   page and saved again ("Ã©" for "é", "Рџ" for "П", "√©") is repaired line by line when one page alone repairs
    //   it, and a text still garbled, or in a script its language doesn't write, is left out.
    //   (tests/TranslationScan's sweep saves every shipped file every wrong way and reads it back: never a wrong text.)
    // - Parsed: the separator from Excel's sep= line or the first cell of the first rows (comments and blank lines
    //   skipped); read from en.csv's header row on (a file without it isn't read: which column is which only that row
    //   says), the columns by its names, en.csv's own only (key, value, english, note, the english after the value; a
    //   column of the translator's own is not read, a header in other words neither); quotes only at a field's start (a
    //   quote inside a text is
    //   a quote); a quote never closed: the file read again line by line; a row Excel put whole into one cell split
    //   again; # rows are comments.
    // - Cleaned: zero-width spaces and BOMs out of keys and off a text's ends (inside a text they stay: Persian and the
    //   Indic scripts need U+200C and U+200D); a literal \n is a line break; { 0 } and the full-width ｛０｝ of Japanese
    //   input are {0}; an Excel error cell (#NAME?) is no text.
    // Texts.Take does the rest with the owner's English: a line without its English is left out, an unknown key is found
    // again by its english column; columns the other way round or translated twice are said (those lines show English).
    internal static class TranslationFiles
    {
        internal sealed class Row
        {
            internal string Key, Value, English;
            internal int Line;
            internal List<string> Cells;      // the row's fields as written (Texts.Take: a separator inside a text)
            internal int ValueAt, EnglishAt;  // the columns read as the value and the english
            internal char Sep;                // the separator that split this row (a row put whole in one cell: its own)
        }

        internal sealed class File
        {
            internal string Path;
            internal string How;                         // how it was read, when not plain UTF-8 CSV (the log line's note)
            internal readonly List<Row> Rows = new List<Row>();
            internal int Malformed;
            internal bool Usable;                        // false: nothing read (a workbook, a code page we can't read)
            internal string Page;                        // the Windows code page of its language (LegacyText); null: none
            internal string Lang;                        // the language it is read for
            internal int Duplicates;                     // rows of a key it had already (passed on: Texts.Take picks)
            internal bool AnyLost;                       // a line that couldn't be read was left out (said by the decoder)
            internal char Sep;                           // the separator its rows were read with
            internal string OtherColumn;                 // a column beside the value one that may hold a translation, not read
            internal int HeaderWidth;                    // the header's columns (0: no header); a row with more has a separator inside a text
            internal bool NativeLetters;                 // it holds letters a page losing them as "?" hasn't: its "?" are question marks
        }

        private const int MaxWarningsPerFile = 10;

        // Whether a file that isn't UTF-8 or UTF-16 is read through the code pages its language's programs save in (Paged):
        // off since 2026-11-01. Code review rounds 3 to 5 each found a file the reader showed a wrong text for (a short
        // file, a file of two pages, a rare sign: every reader admitting more than one encoding has a corner where two
        // readings are plausible); off, such a file isn't read and the translator is told to save it as "CSV UTF-8". The
        // user's decision (docs/localization.md section 11 has the measured costs): on, it reads most Excel "CSV" files.
        internal static bool ReadCodePages;

        // Whether garbled UTF-8 ("Ã©" for "é") is repaired line by line (Unmangle): off since code review round 6. Its
        // repairs went wrong in corners (a literal \n before a capital, all capitals in Polish, a page outside its list):
        // off, a garbled line it detects (Garbled) is left out, said; one it doesn't is shown as the file has it.
        internal static bool RepairGarbled;
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

        // How well a file's name matches the language: 0 its id, 1 a name of it, 2 its base language or a region of it
        // (pt for pt-br, fr-FR, jp_JP or french_FR for fr or ja); -1 not it (another language; zh-TW, Traditional
        // Chinese, for zh_cn); -2 a copy of it ("fr - Copy", fr_old, "fr (1)", french2): said, never read.
        private static int Match(string stem, string lang)
        {
            string s = Norm(stem), l = Norm(lang);
            if (s.EndsWith("missing")) return -1; // a report copied back (fr.missing.csv): the English, nothing to read
            string id = NameToId.TryGetValue(s, out string named) ? named : s;
            if (id == l) return id == s ? 0 : 1;
            string loc = Locale(id);
            if (loc != null) return BaseOf(loc) == BaseOf(l) && !Texts.IsEnglishId(loc) && Traditional(loc) == Traditional(l) ? 2 : -1;
            // the language's id or one of its names, then something more
            string head = LeadingLetters(s);
            if (head.Length == 0 || head.Length == s.Length) return -1;
            string headId = NameToId.TryGetValue(head, out string h) ? h : head;
            return headId == l || (Locale(headId) != null && BaseOf(headId) == BaseOf(l) && !Texts.IsEnglishId(headId)) ? -2 : -1;
        }

        // The language id a name stands for when it is one, alone or with a region (two letters or three digits) or a
        // script (hans, hant, latn, cyrl): "fr", "fr-fr", "es-419", "zh-hant"; its first part may be a language's name
        // ("jp-jp": ja-jp, "french-fr": fr-fr). Null: no language id ("fr-old", "fr-copy").
        private static string Locale(string id)
        {
            int cut = id.IndexOf('-');
            string b = cut < 0 ? id : id.Substring(0, cut);
            if (NameToId.TryGetValue(b, out string mapped)) b = BaseOf(mapped);
            else if (b.Length < 2 || b.Length > 3 || !AllLetters(b)) return null;
            if (cut < 0) return b;
            string r = id.Substring(cut + 1);
            bool region = r.Length == 2 ? AllLetters(r)
                : r.Length == 3 ? char.IsDigit(r[0]) && char.IsDigit(r[1]) && char.IsDigit(r[2])
                : r == "hans" || r == "hant" || r == "latn" || r == "cyrl";
            return region ? b + "-" + r : null;
        }

        // Chinese in its Traditional letters (Taiwan, Hong Kong, Macau): another language than zh_cn's Simplified.
        private static bool Traditional(string id) => id == "zh-tw" || id == "zh-hk" || id == "zh-mo" || id == "zh-hant";

        private static bool AllLetters(string s)
        {
            foreach (char c in s) if (c < 'a' || c > 'z') return false;
            return s.Length > 0;
        }

        private static string LeadingLetters(string s)
        {
            int n = 0;
            while (n < s.Length && char.IsLetter(s[n])) n++;
            return s.Substring(0, n);
        }

        // The files of the language in a folder, best first (the caller reads a folder's tiers in turn).
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
                if (tier == -1)
                {
                    // the mod's report of the lines still to translate (fr.missing.csv), filled in and kept as it is
                    string s = Norm(stem);
                    if (s.EndsWith("missing") && Match(s.Substring(0, s.Length - "missing".Length).TrimEnd('.', '-', ' '), lang) >= 0 && (ext == ".csv" || ext == ".txt"))
                        Warn(f, 0, $"is the mod's report of the lines still to translate: add its rows to {lang}.csv; not read");
                    continue;
                }
                bool workbook = Array.IndexOf(Workbooks, ext) >= 0;
                if (!workbook && ext.Length > 0 && Array.IndexOf(TextExts, ext) < 0) continue; // no extension: read, said
                if (tier == -2)
                {
                    Warn(f, 0, $"isn't read: only {lang}.csv is (a copy or an old version? rename the one to use {lang}.csv)");
                    continue;
                }
                if (workbook)
                {
                    Warn(f, 0, "is a spreadsheet workbook, not a CSV: in Excel or LibreOffice, Save As \"CSV UTF-8\" (" + lang + ".csv); not read");
                    continue;
                }
                if (ext != ".csv" || !Path.GetFileName(f).Equals(stem + ".csv", StringComparison.OrdinalIgnoreCase) || tier > 0)
                {
                    Warn(f, 0, $"is read as the CSV of {lang}; name it {lang}.csv");
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

        // keys: the owner's English by key, when known (the swallowed-row test asks it); null: the file alone.
        internal static File Read(string path, string lang, Dictionary<string, string> keys = null)
        {
            var file = new File { Path = path, Page = LegacyText.PageFor(lang, out _), Lang = lang };
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                string text = Decode(data, lang, file);
                if (text == null) return file;
                file.Usable = true;
                Parse(text, file, keys);
            }
            catch (Exception e)
            {
                file.Usable = false;
                Warn(path, 0, "can't be read (" + e.Message + ")");
            }
            return file;
        }

        // The file's bytes as text: UTF-16 (Utf16Order), else UTF-8 with its garbled lines repaired (Unmangle), else the
        // language's code page (Paged). Null when nothing can be read (said).
        private static string Decode(byte[] data, string lang, File file)
        {
            if (data.Length == 0) return "";
            if (data.Length >= 4 && data[0] == (byte)'P' && data[1] == (byte)'K' && data[2] == 3 && data[3] == 4)
            {
                Warn(file.Path, 0, "is a spreadsheet workbook saved with a .csv name: in Excel or LibreOffice, Save As \"CSV UTF-8\"; not read");
                return null;
            }
            string text;
            int order = Utf16Order(data, out int skip);
            if (order != 0)
            {
                text = (order == 1 ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(data, skip, data.Length - skip);
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
                    if (ReadCodePages) return Paged(data, start, lang, file); // not UTF-8: an encoding is the file's
                    Warn(file.Path, 0, "isn't saved as UTF-8 (Excel's plain \"CSV\", \"CSV (MS-DOS)\" or \"CSV (Macintosh)\", or another program's own encoding): not read; save it as \"CSV UTF-8\" (in Excel: Save As, CSV UTF-8; in LibreOffice: Character set Unicode (UTF-8))");
                    return null;
                }
            }
            if (text.IndexOf('\0') >= 0) text = text.Replace("\0", "");
            // CR alone ends a line too (an old Mac's, Mac Excel's), also among LF lines; CRLF is one end
            if (text.IndexOf('\r') >= 0) text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            return Unmangle(text, file);
        }

        // 1 UTF-16 little-endian, 2 big-endian, 0 not UTF-16: its BOM, or without one (Excel's "Unicode Text" has it, other
        // programs not) zero bytes in one of a pair's two places and hardly any in the other (UTF-8 and the code pages have
        // none; in UTF-16 every character under U+0100, the keys and the english column, has one).
        private static int Utf16Order(byte[] data, out int skip)
        {
            skip = 0;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) { skip = 2; return 1; }
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) { skip = 2; return 2; }
            int n = Math.Min(data.Length, 4096) & ~1, even = 0, odd = 0;
            for (int i = 0; i < n; i++)
            {
                if (data[i] != 0) continue;
                if ((i & 1) == 0) even++;
                else odd++;
            }
            int pairs = n / 2;
            if (odd >= 2 && odd * 10 >= pairs && even * 10 <= odd) return 1;
            if (even >= 2 && even * 10 >= pairs && odd * 10 <= even) return 2;
            return 0;
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // A file that isn't UTF-8, read through every page its language's programs save in (LegacyText.Candidates: Windows,
        // DOS, Mac, ISO; ReadThrough). A page with a line implausible for the language isn't the file's; the readings of
        // the pages left are merged line by line: a line they all read alike is read, a line they read differently is left
        // out, said (a short file can look right in two pages; one stray byte reads two ways): nothing else may be meant.
        // None plausible: not read, said. A language without candidates (ja, zh_cn, ko, a community language) must be UTF-8.
        // CR alone ends a line when there is no LF (an old Mac's). Null: nothing read (said).
        private static string Paged(byte[] data, int start, string lang, File file)
        {
            List<(string Name, string Page)> candidates = LegacyText.Candidates(lang);
            if (candidates.Count == 0)
            {
                Warn(file.Path, 0, "isn't saved as UTF-8, which a file of this language must be: save it as \"CSV UTF-8\"; not read");
                return null;
            }
            var readings = new List<List<string>>();
            var names = new List<string>();
            int utf8 = 0, paged = 0, firstPaged = 0;
            foreach (var c in candidates)
            {
                List<string> r = ReadThrough(data, start, c.Page, lang, out int u, out int p, out int fp);
                if (r == null) continue;
                if (readings.Count == 0)
                {
                    utf8 = u;
                    paged = p;
                    firstPaged = fp;
                }
                readings.Add(r);
                names.Add(c.Name);
            }
            if (readings.Count == 0)
            {
                Warn(file.Path, 0, "isn't saved as UTF-8, and no code page its language's programs save in reads it plausibly (letters or signs the language doesn't write): not read; save it as \"CSV UTF-8\"");
                return null;
            }
            var sb = new StringBuilder(data.Length);
            int differ = 0, firstDiffer = 0;
            List<string> one = readings[0];
            for (int i = 0; i < one.Count; i++)
            {
                bool alike = true;
                for (int k = 1; k < readings.Count && alike; k++) alike = readings[k][i] == one[i];
                if (alike)
                {
                    sb.Append(one[i]);
                    continue;
                }
                sb.Append('\uFFFD'); // read two ways: left out (Parse skips it, said here)
                if (one[i].Length > 0 && one[i][one[i].Length - 1] == '\n') sb.Append('\n');
                if (differ++ == 0) firstDiffer = i + 1;
            }
            string all = string.Join(", ", names);
            if (differ > 0)
            {
                Warn(file.Path, firstDiffer, $"isn't saved as UTF-8 and reads plausibly in more than one code page ({all}): {differ} line(s) read differently in them are left out (the first: this one); save it as \"CSV UTF-8\"");
                file.How = all;
            }
            else if (utf8 > 0)
            {
                file.How = "UTF-8 and " + names[0];
                Warn(file.Path, firstPaged, $"mixes UTF-8 and {names[0]} ({paged} line(s) in {names[0]}: a line edited in another program?); read; save it again as \"CSV UTF-8\"");
            }
            else
            {
                file.How = names[0];
                Warn(file.Path, 0, $"isn't saved as UTF-8: read as {names[0]}; save it as \"CSV UTF-8\" (in Excel: Save As, CSV UTF-8)");
            }
            return sb.ToString();
        }

        // The file read through one page, line by line (each line with its end): a line of ASCII as it is; a line that is
        // valid UTF-8 and plausible so as UTF-8 (a line edited in another program), unless it is plausible through the
        // page too and reads otherwise (then nothing can tell: not this page); else through the page when plausible so;
        // else, a line of UTF-8 with stray bytes, piecewise (DecodeLine) when that is plausible. Null: a line plausible no
        // way, or two ways: not this page.
        private static List<string> ReadThrough(byte[] data, int start, string page, string lang, out int utf8, out int paged, out int firstPaged)
        {
            utf8 = paged = firstPaged = 0;
            byte cut = Array.IndexOf(data, (byte)'\n', start) >= 0 ? (byte)'\n' : (byte)'\r';
            var lines = new List<string>();
            for (int at = start, line = 1; at < data.Length; line++)
            {
                int end = Array.IndexOf(data, cut, at);
                int stop = end < 0 ? data.Length : end + 1;
                bool high = false;
                for (int i = at; i < stop && !high; i++) high = data[i] >= 0x80;
                string t = PageText(data, at, stop, page);
                if (cut == (byte)'\r') t = t.Replace('\r', '\n');
                if (!high)
                {
                    lines.Add(t);
                    at = stop;
                    continue;
                }
                string u = null;
                if (ValidUtf8(data, at, stop))
                {
                    u = Utf8.GetString(data, at, stop - at);
                    if (cut == (byte)'\r') u = u.Replace('\r', '\n');
                }
                bool tp = Plausible(t, lang), up = u != null && Plausible(u, lang);
                if (up && tp && u != t) return null;
                if (up)
                {
                    lines.Add(u);
                    utf8++;
                }
                else if (tp)
                {
                    lines.Add(t);
                    paged++;
                    if (firstPaged == 0) firstPaged = line;
                }
                else
                {
                    string p = DecodeLine(data, at, stop, page, out int sequences, out int strays);
                    if (cut == (byte)'\r') p = p.Replace('\r', '\n');
                    if (sequences == 0 || strays == 0 || !Plausible(p, lang)) return null;
                    lines.Add(p);
                    utf8++;
                    paged++;
                    if (firstPaged == 0) firstPaged = line;
                }
                at = stop;
            }
            return lines;
        }

        // One line: its valid UTF-8 sequences as UTF-8 (counted), its other bytes above 0x7F through the page (counted;
        // nothing without a page).
        private static string DecodeLine(byte[] data, int from, int to, string page, out int sequences, out int strays)
        {
            sequences = 0;
            strays = 0;
            var sb = new StringBuilder(to - from);
            for (int i = from; i < to;)
            {
                byte b = data[i];
                if (b < 0x80)
                {
                    sb.Append((char)b);
                    i++;
                    continue;
                }
                int n = Sequence(data, i, to);
                if (n > 0)
                {
                    sb.Append(StrictUtf8.GetString(data, i, n));
                    sequences++;
                    i += n;
                    continue;
                }
                strays++;
                if (page != null) sb.Append(page[b - 0x80]);
                i++;
            }
            return sb.ToString();
        }

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false); // after ValidUtf8: never throws

        // Whether bytes are UTF-8 throughout, without a throw (the strict decoder's exception cost some 40 µs a line on
        // a correct Russian file tried through five pages).
        private static bool ValidUtf8(byte[] d, int from, int to)
        {
            for (int i = from; i < to;)
            {
                if (d[i] < 0x80)
                {
                    i++;
                    continue;
                }
                int n = Sequence(d, i, to);
                if (n == 0) return false;
                i += n;
            }
            return true;
        }

        // The length of the valid UTF-8 sequence at i (2 to 4 bytes), 0 when there is none (no overlong form, no surrogate,
        // nothing past U+10FFFF).
        private static int Sequence(byte[] d, int i, int to)
        {
            byte b = d[i];
            int n = b >= 0xC2 && b <= 0xDF ? 2 : b >= 0xE0 && b <= 0xEF ? 3 : b >= 0xF0 && b <= 0xF4 ? 4 : 0;
            if (n == 0 || i + n > to) return 0;
            for (int k = 1; k < n; k++) if (d[i + k] < 0x80 || d[i + k] > 0xBF) return 0;
            byte c = d[i + 1];
            if ((b == 0xE0 && c < 0xA0) || (b == 0xED && c > 0x9F) || (b == 0xF0 && c < 0x90) || (b == 0xF4 && c > 0x8F)) return 0;
            return n;
        }

        // Whether a text read through a code page is plausibly in it: every character beyond ASCII one of the language's
        // letters or the punctuation it writes, and no dash or typographic quote between two letters (Excel's "CSV (MS-DOS)"
        // and "CSV (Macintosh)" read through the Windows page give signs and other letters: "Termin‚", "Hšllenfeuer",
        // "Drag—n", Cyrillic letters Russian hasn't: "ЏаЁўҐв").
        private static bool Plausible(string s, string lang)
        {
            string allowed = Allowed(lang);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '\u0080') continue;
                if (allowed.IndexOf(c) < 0) return false;
                if (BetweenLetters.IndexOf(c) >= 0 && i > 0 && i + 1 < s.Length && char.IsLetter(s[i - 1]) && char.IsLetter(s[i + 1])) return false;
                // a capital right after a small letter: a page that puts a capital where the text has a small one ("Sİfİrla")
                if (char.IsUpper(c) && i > 0 && char.IsLower(s[i - 1])) return false;
            }
            return true;
        }

        private const string BetweenLetters = "–—“”„‚•¡¿";

        private const string Punctuation = "\u00A0’‘“”«»…–—•·°€";
        // every letter of Latin-1 and Windows-1252: a Western text may name a foreign word or person (Café, Müller)
        // (not Ð ð Þ þ, nor Š š Ž ž: no Western text of ours writes them, and they make a Windows-1252 and an ISO-8859-15
        // repair look alike: "ş" garbled is "ž" through the other page)
        private const string Latin1Letters = "ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÑÒÓÔÕÖØÙÚÛÜÝßàáâãäåæçèéêëìíîïñòóôõöøùúûüýÿ";
        private const string Cyrillic = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдежзийклмнопрстуфхцчшщъыьэюяЁё";

        // The letters and punctuation of a language with a code page (LegacyText.PageFor); its page's every character for
        // a language without a list of its own.
        private static string Allowed(string lang)
        {
            switch (BaseOf(Norm(lang)))
            {
                case "fr": return Latin1Letters + "ŒœŸ‹›" + Punctuation;
                case "de": return Latin1Letters + "„‚›‹" + Punctuation;
                case "es": return Latin1Letters + "¿¡ºª" + Punctuation;
                case "pt": return Latin1Letters + "ºª" + Punctuation;
                case "it": return Latin1Letters + "ºª" + Punctuation;
                case "nl": return Latin1Letters + Punctuation;
                case "en": return Latin1Letters + Punctuation;
                case "pl": return "ąćęłńóśźżĄĆĘŁŃÓŚŹŻäöüéáíú„‚" + Punctuation;
                case "cs": return "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽäöü„‚" + Punctuation;
                case "hu": return "áéíóöőúüűÁÉÍÓÖŐÚÜŰä„" + Punctuation;
                case "ru": return Cyrillic + "„№" + Punctuation;
                case "uk": return Cyrillic + "ҐґЄєІіЇї„№" + Punctuation;
                case "be": return Cyrillic + "ЎўІі„№" + Punctuation;
                case "tr": return "çğıöşüâîûÇĞİÖŞÜÂÎÛ" + Latin1Letters + Punctuation;
                default: return LegacyText.PageFor(lang, out _) ?? "";
            }
        }

        private static string PageText(byte[] data, int from, int to, string page)
        {
            var sb = new StringBuilder(to - from);
            for (int i = from; i < to; i++)
            {
                byte b = data[i];
                sb.Append(b < 0x80 ? (char)b : page[b - 0x80]);
            }
            return sb.ToString();
        }

        // UTF-8 that a spreadsheet opened in a Windows code page and saved again as UTF-8: "Ã©" for "é" (Western), "Рџ"
        // for "П" (Cyrillic), "Ä…" for "ą" (Central European). Line by line (a file can be garbled in some lines only): a
        // line's characters go back to bytes through the language's page (then the Western one) and are read as UTF-8
        // again, taken when every byte reads and fewer characters beyond ASCII are left (garbling always makes more; a
        // correct text can't come back: its page's bytes aren't UTF-8), twice for a file garbled twice.
        private static string Unmangle(string text, File file)
        {
            if (!RepairGarbled) return text; // the user's switch: garbled lines are left out by Parse (Garbled), not repaired
            bool any = false;
            for (int i = 0; i < text.Length && !any; i++) any = text[i] >= '\u0080';
            if (!any) return text;
            string[] lines = text.Split('\n');
            int repaired = 0, first = 0, twoWays = 0, firstTwoWays = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string back = Repaired(lines[i], file.Page, file.Lang, out bool ambiguous);
                if (ambiguous)
                {
                    lines[i] = "\uFFFD"; // garbled, and two pages repair it differently: left out (Parse skips it)
                    if (twoWays++ == 0) firstTwoWays = i + 1;
                    continue;
                }
                if (back == null) continue;
                lines[i] = back;
                repaired++;
                if (first == 0) first = i + 1;
            }
            if (twoWays > 0) Warn(file.Path, firstTwoWays, $"has {twoWays} line(s) garbled (\"Ã©\" for \"é\") that repair two ways (the first: this one): left out; save the file as \"CSV UTF-8\" and open it as UTF-8");
            if (repaired == 0) return twoWays > 0 ? string.Join("\n", lines) : text;
            file.How = (file.How != null ? file.How + ", " : "") + "letters repaired";
            Warn(file.Path, first, $"was opened in another encoding and saved again (\"Ã©\" for \"é\"): the letters of {repaired} line(s) (the first: this one) were repaired; save it as \"CSV UTF-8\" and open it as UTF-8");
            return string.Join("\n", lines);
        }

        // A garbled line's repair: through every page UTF-8 may have passed through before it was saved again (the
        // language's own, Windows-1252, ISO-8859-1 and -15, Mac Roman: LegacyText.RepairPages), each tried twice for a text
        // garbled twice; taken when its result fits the language, and only when no other page repairs it otherwise ("赤"
        // through Windows-1252 is "赀" through ISO-8859-15: nothing can tell which, no repair).
        private static string Repaired(string line, string page, string lang) => Repaired(line, page, lang, out _);

        // ambiguous: some page repairs it, and another otherwise (the line is garbled for sure: a correct text never
        // round-trips; but nothing tells which repair).
        private static string Repaired(string line, string page, string lang, out bool ambiguous)
        {
            ambiguous = false;
            if (Beyond(line) == 0) return null;
            string found = null;
            foreach (string use in LegacyText.RepairPages(lang))
            {
                // Mac Roman's 0xBD is Ω, U+03A9; Windows' table for it (code page 10000) gives the ohm sign, U+2126
                string cur = use == LegacyText.MacRoman && line.IndexOf('\u2126') >= 0 ? line.Replace('\u2126', '\u03A9') : line;
                bool changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (!LegacyText.Encode(cur, use, out byte[] bytes) || !ValidUtf8(bytes, 0, bytes.Length)) break;
                    string back = Utf8.GetString(bytes);
                    if (Beyond(back) >= Beyond(cur)) break;
                    cur = back;
                    changed = true;
                }
                if (!changed || !Fits(cur, page, lang)) continue;
                if (found == null) found = cur;
                else if (found != cur)
                {
                    ambiguous = true;
                    return null;
                }
            }
            return found;
        }

        // Whether a repaired text is one the language writes: plausible for a language with a page (a correct Russian
        // "Её" is no garbled "Ÿ"), in its own script and without control characters for the others.
        private static bool Fits(string s, string page, string lang)
        {
            if (page != null) return Plausible(s, lang);
            foreach (char c in s) if (c >= '\u0080' && c <= '\u009F') return false;
            return !WrongScript(s, Norm(lang));
        }

        private static bool InPage(string s, string page)
        {
            foreach (char c in s) if (c >= '\u0080' && page.IndexOf(c) < 0) return false;
            return true;
        }

        private static int Beyond(string s)
        {
            int n = 0;
            foreach (char c in s) if (c >= '\u0080') n++;
            return n;
        }

        // A text still garbled after the repair (a character the page hasn't came in after, so its bytes can't come
        // back), by what a correct text never has: "Ã" or "Â" before a character of a UTF-8 sequence's later bytes (Ã©,
        // Â»), "â€" (’ “ …), Mac Excel's "√" so (√©), "Ð" or "Ñ" so in a file not of a Western language (Ð¿); Japanese
        // read as Shift-JIS (縺 繧 繝, half-width kana among kanji), Chinese read as GBK (kana in Chinese), Korean (kana or
        // kanji in Korean); in a Cyrillic, Central European or Turkish file three or more whole UTF-8 sequences in its
        // own page's characters (Рџ, Ä…: a first byte and the later bytes it says) covering most of its letters beyond
        // ASCII (a correct Russian text has a few: "В" before a no-break space).
        private static bool Garbled(string s, string page, string lang)
        {
            bool western = page == LegacyText.Western;
            int beyond = 0, kanji = 0, kana = 0, halfKana = 0, sjis = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '\u0080') continue;
                if (c <= '\u009F') return true; // a control character: a byte read through ISO-8859-1, never typed
                beyond++;
                char d = i + 1 < s.Length ? s[i + 1] : '\0';
                if ((c == 'Ã' || c == 'Â') && Later(d, LegacyText.Western)) return true;
                if (c == 'â' && d == '€') return true;
                if (c == '√' && Later(d, LegacyText.MacRoman)) return true;
                if (!western && (c == 'Ð' || c == 'Ñ') && Later(d, LegacyText.Western)) return true;
                if (c >= '\u4E00' && c <= '\u9FFF') kanji++;
                else if (c >= '\u3040' && c <= '\u30FF') kana++;
                else if (c >= '\uFF61' && c <= '\uFF9F') halfKana++;
                if (c == '縺' || c == '繧' || c == '繝' || c == '縲') sjis++;
            }
            string l = Norm(lang);
            if (l.StartsWith("ja") && (sjis >= 2 || (halfKana > 0 && kanji > 0))) return true;
            if (l.StartsWith("zh") && kana + halfKana > 0) return true;
            if (l.StartsWith("ko") && kana + halfKana + kanji > 0) return true;
            if (WrongScript(s, l)) return true;
            // Mac Excel's garbling without its "√": the first bytes of UTF-8 sequences as Mac Roman writes them (ƒ ≈ for
            // Polish and Turkish letters, – — for Cyrillic, ‚ for ’ “ …, „ ‰ Â Ê Á Ë È for Chinese, Japanese, Korean)
            if (Marks(s, LegacyText.MacRoman, MacFirsts, out int macCovered) >= 3 && macCovered * 10 >= beyond * 6) return true;
            if (page == null || western) return false;
            if (Marks(s, page, null, out int covered) >= 3 && covered * 10 >= beyond * 6) return true;
            // through the language's Mac page (a Russian or Polish Mac's Excel: "–Я—А–Є")
            List<(string Name, string Page)> pages = LegacyText.Candidates(lang);
            return pages.Count > 2 && Marks(s, pages[2].Page, null, out int ownMacCovered) >= 3 && ownMacCovered * 10 >= beyond * 6;
        }

        private const string MacFirsts = "√ƒ≈–—‚„‰ÂÊÁËÈ";

        // How many whole UTF-8 sequences a text holds in a page's characters (a first byte and the later bytes it says),
        // and how many characters they cover; firsts: the only characters counted as a first byte (null: any).
        private static int Marks(string s, string page, string firsts, out int covered)
        {
            int marks = 0;
            covered = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '\u0080' || (firsts != null && firsts.IndexOf(s[i]) < 0)) continue;
                int b = page.IndexOf(s[i]) + 0x80;
                if (b < 0xC2 || b > 0xF4) continue;
                int need = b < 0xE0 ? 1 : b < 0xF0 ? 2 : 3, k = 1;
                while (k <= need && i + k < s.Length && Later(s[i + k], page)) k++;
                if (k <= need) continue;
                marks++;
                covered += need + 1;
                i += need;
            }
            return marks;
        }

        // A text with a character no text of its language writes (UTF-8 garbled through GBK, Shift-JIS or a Korean page; a
        // code-page file that is valid UTF-8 by accident: Mac Turkish "ıç" are a Syriac letter's bytes, GBK "状态" a Hebrew
        // sign's), by the language's script family: a Latin one Latin letters (and combining accents), punctuation, currency
        // and letterlike signs; a Cyrillic one the Cyrillic block and the same signs, Latin letters beyond ASCII only beside
        // Cyrillic ones ("Ÿ" alone for "Её"); Chinese, Japanese and Korean their own scripts (Korean letters only in
        // Korean), CJK and full-width punctuation, Latin letters beyond ASCII only beside their own ("Ű" for "키").
        // A community language of another script: not checked.
        private static bool WrongScript(string s, string l) => WrongScript(s, l, out _);

        // odd: the first character no text of the language writes.
        private static bool WrongScript(string s, string l, out char odd)
        {
            odd = '\0';
            string b = BaseOf(l);
            bool cyrillicLang = b == "ru" || b == "uk" || b == "be", eastAsian = b == "ja" || b == "zh" || b == "ko";
            bool latinLang = !cyrillicLang && !eastAsian && LegacyText.PageFor(b, out _) != null;
            if (!latinLang && !cyrillicLang && !eastAsian) return false;
            int latin = 0, own = 0;
            foreach (char c in s)
            {
                if (c < '\u00A0') continue;
                bool latinLetter = c >= '\u00C0' && c <= '\u024F' || c >= '\u1E00' && c <= '\u1EFF';
                // punctuation, signs and symbols any language's text may hold: combining accents, general punctuation,
                // currency, letterlike, number forms, arrows, maths, enclosed, geometric, miscellaneous symbols, dingbats,
                // emoji (surrogates) and their variation selectors
                bool common = c <= '\u00BF' || (c >= '\u0300' && c <= '\u036F') || (c >= '\u2000' && c <= '\u22FF') || (c >= '\u2460' && c <= '\u24FF')
                    || (c >= '\u25A0' && c <= '\u27BF') || (c >= '\u2B00' && c <= '\u2BFF') || (c >= '\uD800' && c <= '\uDFFF') || (c >= '\uFE00' && c <= '\uFE0F');
                if (latinLetter) latin++;
                if (latinLang)
                {
                    if (!latinLetter && !common) { odd = c; return true; }
                    continue;
                }
                if (cyrillicLang)
                {
                    if (c >= '\u0400' && c <= '\u052F') own++;
                    else if (!latinLetter && !common) { odd = c; return true; }
                    continue;
                }
                bool hangul = (c >= '\uAC00' && c <= '\uD7AF') || (c >= '\u1100' && c <= '\u11FF') || (c >= '\u3130' && c <= '\u318F');
                bool cjk = (c >= '\u3000' && c <= '\u30FF') || (c >= '\u3400' && c <= '\u4DBF') || (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\uF900' && c <= '\uFAFF') || (c >= '\uFF00' && c <= '\uFFEF');
                if (hangul && b != "ko") { odd = c; return true; }
                if (cjk || hangul) own++;
                else if (!latinLetter && !common) { odd = c; return true; }
            }
            return latin > 0 && own == 0 && !latinLang;
        }

        // A character's byte in the page is 0x80 to 0xBF: one of a UTF-8 sequence's later bytes.
        private static bool Later(char c, string page)
        {
            int at = page.IndexOf(c);
            return at >= 0 && at < 0x40;
        }

        // ---- parsing ----

        private static void Parse(string text, File file, Dictionary<string, string> keys)
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
            else sep = Sniff(text, 0);
            file.Sep = sep;
            file.NativeLetters = NativeLettersIn(text, file.Lang);
            var broken = new HashSet<int>();
            List<(List<string> Fields, int Line)> rows = ParseCsv(text, start, line, sep, out bool unclosed, broken);
            int swallowedAt = -1;
            foreach (var (fields, at) in rows)
            {
                if (fields.Count > 0 && fields[0].IndexOf('\n') >= 0) swallowedAt = at; // a key holds no line break: a quote opened at a row's start
                for (int i = 1; i < fields.Count && swallowedAt < 0; i++) if (Swallowed(fields[i], sep, keys)) swallowedAt = at;
                if (swallowedAt >= 0) break;
            }
            if (unclosed || swallowedAt >= 0)
            {
                Warn(file.Path, swallowedAt >= 0 ? swallowedAt : rows.Count > 0 ? rows[rows.Count - 1].Line : line,
                    "has a quote that runs past its line and takes the lines after it (a \" in a text is written \"\"): the file is read line by line instead");
                broken.Clear();
                rows = ParseByLine(text, start, line, sep, file, broken, keys);
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int valueAt = -1, englishAt = -1; // nothing read up to en.csv's header row
            string valueName = null;
            List<string> header = null;
            int headerCells = 0; // all the header's cells (HeaderWidth: up to its last named one)
            List<int> others = null; // the header's columns en.csv hasn't
            HashSet<string> englishTexts = null; // the owner's English, cleaned (HeaderInOtherWords)
            bool indexColumn = false, headed = false, noValueColumn = true;
            int dataRows = 0, emptyValues = 0, shifted = 0, firstShifted = 0, emptyKey = 0, firstEmptyKey = 0, beyond = 0, firstBeyond = 0;
            int ahead = 0, firstAhead = 0, cut = 0, firstCut = 0;
            string misplaced = null; // a row before the header naming "key" not first, or garbled: said so
            string pendingEnglish = null, pendingHead = null; // the row above cut before its english (or in it): its English, its english's start
            int continued = 0, firstContinued = 0;
            foreach (var (fields0, at) in rows)
            {
                List<string> f = fields0;
                char rowSep = sep;
                string wasEnglish = pendingEnglish, wasHead = pendingHead;
                pendingEnglish = pendingHead = null;
                if (OnlyFirst(f) && f[0].IndexOfAny(new[] { ',', ';', '\t' }) >= 0)
                {
                    // a whole row in one cell (Excel in a ; country opening a , file, then saving): split again
                    char own = SniffLine(f[0]);
                    var innerBroken = new HashSet<int>();
                    var again = ParseCsv(f[0], 0, at, own, out bool innerOpen, innerBroken);
                    if (again.Count == 1 && again[0].Fields.Count >= 2)
                    {
                        f = again[0].Fields;
                        rowSep = own;
                        // its quotes as the main read's: a quote not doubled, or one never closed (a text cut at its line)
                        if (innerOpen || innerBroken.Count > 0) broken.Add(at);
                    }
                }
                // a column before the key (pandas' to_csv, a numbered sheet): its header empty, "Unnamed: 0", "index"...
                bool before = indexColumn;
                if (!indexColumn && f.Count > 2 && CleanKey(f[1]).Equals("key", StringComparison.OrdinalIgnoreCase) && IndexName(CleanKey(f[0])))
                {
                    indexColumn = before = true;
                    Warn(file.Path, at, "has a column before the key (an index or line numbers): read without it");
                }
                else if (indexColumn && CleanKey(f[0]).Equals("key", StringComparison.OrdinalIgnoreCase)) indexColumn = before = false; // a header without it (files put together)
                // (row by row: a row added without its index, a stray separator before the header, keeps its key)
                if (before && f.Count > 1 && (!IsKey(CleanKey(f[0]), keys) || IsKey(CleanKey(f[1]), keys))) f = f.GetRange(1, f.Count - 1);
                string key = CleanKey(f[0]);
                if (key.Equals("key", StringComparison.OrdinalIgnoreCase))
                {
                    Columns(f, out valueAt, out englishAt, out others, out string why); // a header (also a second one, files put together)
                    header = f;
                    headerCells = f.Count;
                    file.HeaderWidth = Width(f);
                    headed = true;
                    noValueColumn = valueAt < 0;
                    if (noValueColumn) Warn(file.Path, at, $"has a header that doesn't name its columns as en.csv does ({why}): not read; copy en.csv's header row as it is: key,value,english,note");
                    else if (englishAt < valueAt)
                    {
                        // a text cut by a separator typed into it would end the row early and nothing after it would move
                        // (in the template's order its tail lands in the english column, which then doesn't match)
                        noValueColumn = true;
                        Warn(file.Path, at, "has its english column before the translation: not read (a text cut by a comma typed into it couldn't be told); put the columns as en.csv has them: key, value, english");
                    }
                    valueName = valueAt >= 0 && valueAt < f.Count ? CleanKey(f[valueAt]) : null;
                    continue;
                }
                // a comment, a line that couldn't be read (said by the decoder)
                if (key.Length > 0 && (key[0] == '#' || key[0] == '\uFFFD' || key.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))) continue;
                if (!headed)
                {
                    // before en.csv's header row: not read (a file read by its columns' places, its header deleted or in
                    // other words, showed a translation tool's note column as the translation)
                    if (HasText(f) && ahead++ == 0) firstAhead = at;
                    if (misplaced == null && NamesKey(f)) misplaced = Shown(f);
                    continue;
                }
                if (Foreign(f, keys, ref englishTexts, englishAt) && wasEnglish != null && Continues(f, wasEnglish, wasHead))
                {
                    // the rest of the row above, cut by a line break typed into its text (or its english): left out with it
                    file.Malformed++;
                    if (continued++ == 0) firstContinued = at;
                    continue;
                }
                if (Foreign(f, keys, ref englishTexts, englishAt))
                {
                    // none of the mod's lines: a header in other words ("clé,valeur,anglais", "ID,,English,Français") with
                    // a section pasted under it, or a comment without #; which it is can't be told by its words or its
                    // width (each such test let a section through, read by the columns above it: a status column showed
                    // as the translation), so the reading stops up to en.csv's header row
                    if (!noValueColumn) Warn(file.Path, at, $"has a line that isn't one of the mod's lines ({Shown(f)}): the lines after it aren't read up to a header of en.csv's (a header in other words can't be told from a note); a comment starts with #, a key keeps its dot, and the header row is en.csv's: key,value,english,note");
                    file.Malformed++;
                    noValueColumn = true;
                    header = f;
                    file.HeaderWidth = Width(f);
                    continue;
                }
                if (key.Length == 0)
                {
                    // an empty key column with text after it (a column put before the keys, a separator typed at the line's
                    // start): said; an empty row: nothing
                    if (HasText(f) && emptyKey++ == 0) firstEmptyKey = at;
                    continue;
                }
                if (noValueColumn) continue;
                if (file.OtherColumn == null && others != null)
                {
                    // a column en.csv hasn't, holding text: said (never read: a reviewer's "ok", a translator's notes)
                    foreach (int i in others)
                    {
                        if (i >= f.Count || Clean(f[i]).Length == 0) continue;
                        file.OtherColumn = CleanKey(header[i]).Length > 0 ? CleanKey(header[i]) : $"column {i + 1}, no name";
                        break;
                    }
                }
                string ownEnglish = null;
                bool known = keys != null && keys.TryGetValue(key, out ownEnglish);
                if (known && (englishAt >= f.Count || Clean(f[englishAt]).Length == 0)) pendingEnglish = Clean(ownEnglish);
                else if (known && Clean(f[englishAt]) != Clean(ownEnglish))
                {
                    pendingEnglish = Clean(ownEnglish);
                    pendingHead = f[englishAt];
                }
                if (TextBeyond(f, headerCells) || (keys != null && KeyBeyond(f, file.HeaderWidth, keys))
                    || (known && englishAt < f.Count && Clean(f[englishAt]) != Clean(ownEnglish) && TextBeyond(f, file.HeaderWidth)))
                {
                    // text in more cells than the header has, one of the mod's keys past its named columns, or text there
                    // and the English not in its place: a separator inside a text without quotes, a stray one, two rows
                    // joined by a deleted line break; which cell is which can't be told
                    file.Malformed++;
                    if (beyond++ == 0) firstBeyond = at;
                    continue;
                }
                if (broken.Contains(at))
                {
                    file.Malformed++;
                    Warn(file.Path, at, "has a quote inside a quoted text that isn't doubled (a \" in a text is written \"\", and the text put in quotes); left out");
                    continue;
                }
                if (valueAt >= f.Count)
                {
                    file.Malformed++;
                    Warn(file.Path, at, "has a key but no translation after it (the columns are key, value, english, separated by " + SepName(sep) + "); left out");
                    continue;
                }
                dataRows++;
                string value = Clean(f[valueAt]);
                if (string.IsNullOrWhiteSpace(value)) // nothing (a lone \n, tags only): the English
                {
                    emptyValues++;
                    // the translation and its English one column further (a separator typed at a text's start): the row's
                    // English one column right of the english column (a row not translated yet has its English in its place)
                    bool moved = known && englishAt + 1 < f.Count && Clean(f[englishAt + 1]) == Clean(ownEnglish) && Clean(f[englishAt]) != Clean(ownEnglish);
                    if (moved && shifted++ == 0) firstShifted = at;
                    continue;
                }
                if (known && englishAt + 1 < f.Count && Clean(f[englishAt]) == Clean(ownEnglish) && Clean(f[englishAt + 1]) == Clean(ownEnglish))
                {
                    // its English twice, in the english column and after it: a separator typed into its text cut it there
                    file.Malformed++;
                    if (cut++ == 0) firstCut = at;
                    continue;
                }
                if (Control(value, out char control))
                {
                    file.Malformed++;
                    Warn(file.Path, at, $"has a control character in its text (U+{(int)control:X4}); left out");
                    continue;
                }
                if (value.IndexOf('\uFFFD') >= 0)
                {
                    file.Malformed++;
                    if (!file.AnyLost) Warn(file.Path, at, "has letters lost (�: a character the program that saved it couldn't read); left out: save the file as \"CSV UTF-8\" and type the line again");
                    continue;
                }
                if (IsSheetError(value))
                {
                    file.Malformed++;
                    Warn(file.Path, at, $"has a spreadsheet error instead of a text ({value}: a text starting with = + - or @ is a formula to Excel; start it with an apostrophe); left out");
                    continue;
                }
                if (!file.NativeLetters && LostLetters(value, file.Lang))
                {
                    file.Malformed++;
                    Warn(file.Path, at, "has letters lost as \"?\" (saved as plain CSV in a code page without them: Excel's \"CSV\" on a Windows set to another language); left out: save the file as \"CSV UTF-8\"");
                    continue;
                }
                if (WrongScript(value, Norm(file.Lang), out char odd) && odd != '\0')
                {
                    file.Malformed++;
                    Warn(file.Path, at, $"has a character this language's text doesn't use (\"{odd}\", U+{(int)odd:X4}: garbled text shows so); left out: save the file as \"CSV UTF-8\"; if it is meant, write it otherwise");
                    continue;
                }
                if (Garbled(value, file.Page, file.Lang))
                {
                    file.Malformed++;
                    Warn(file.Path, at, "has letters garbled (\"Ã©\" for \"é\", or a script this language doesn't write), which couldn't be repaired; left out: save the file as \"CSV UTF-8\" and type the line again");
                    continue;
                }
                if (!seen.Add(key)) file.Duplicates++; // passed on: Texts.Take keeps the current one (the report's rows added under old ones)
                string english = englishAt >= 0 && englishAt < f.Count ? Clean(f[englishAt]) : "";
                file.Rows.Add(new Row { Key = key, Value = value, English = english.Length > 0 ? english : null, Line = at, Cells = f, ValueAt = valueAt, EnglishAt = englishAt, Sep = rowSep });
            }
            if (!headed && ahead > 0 && misplaced != null) Warn(file.Path, firstAhead, $"has its header row with something before \"key\" or garbled ({misplaced}: a stray separator, a column of its own, or letters garbled by a save in another encoding): not read; en.csv's header row starts with key, as it is: key,value,english,note");
            else if (!headed && ahead > 0) Warn(file.Path, firstAhead, "has no header row of en.csv's: not read (which column is which only that row says); its first line is en.csv's header row, as it is: key,value,english,note");
            else if (ahead > 0) Warn(file.Path, firstAhead, $"has {ahead} line(s) before its header row (the first: this one), which aren't read: the lines go under en.csv's header row");
            if (beyond > 0) Warn(file.Path, firstBeyond, $"has {beyond} line(s) with text past its header's columns, or the English out of its place and text after it (a comma inside a text without quotes, a stray one, or two lines joined; the first: this one): left out; put such a text in quotes");
            if (continued > 0) Warn(file.Path, firstContinued, $"has {continued} line(s) holding the rest of the line above (a line break typed into its text; the first: this one): left out with it; a text with a line break goes in quotes, or write \\n");
            if (cut > 0) Warn(file.Path, firstCut, $"has {cut} line(s) with their English twice, in the english column and the one after it (a comma typed into the text, or a column after english holding the English: delete that column; the first: this one): left out; put a text with a comma in quotes");
            if (emptyKey > 0) Warn(file.Path, firstEmptyKey, $"has {emptyKey} line(s) with nothing in the key column but text after it (the first: this one): left out; the columns are key, value, english");
            if (shifted > 0) Warn(file.Path, firstShifted, $"has {shifted} line(s) with an empty column after the key and the text after it (the first: this one): left out; the columns are key, value, english");
            if (dataRows > 0 && emptyValues == dataRows && file.Rows.Count == 0 && !string.IsNullOrEmpty(valueName))
                Warn(file.Path, 0, $"has nothing in the column read as the translation ({(string.IsNullOrEmpty(valueName) ? "the second" : valueName)}): nothing read; the translation goes in the value column");
        }

        // A control character other than a line break (tabs are spaces by then): never typed, a broken file's.
        private static bool Control(string s, out char c)
        {
            foreach (char x in s)
            {
                if ((x < '\u0020' && x != '\n') || x == '\u007F')
                {
                    c = x;
                    return true;
                }
            }
            c = '\0';
            return false;
        }

        // A header's width: up to its last named column (Excel in a ; country pads every row with empty cells; a row with
        // more cells than this whose English is past them had a separator typed into its text: Texts.SeparatorInside).
        private static int Width(List<string> header)
        {
            int width = 0;
            for (int i = 0; i < header.Count; i++) if (Clean(header[i]).Length > 0) width = i + 1;
            return width;
        }

        // A line under a header that is none of the mod's: text in it, its first cell no key (one the owner has, or a dotted
        // word without whitespace) nor a key damaged by one keystroke (Damaged), not nothing or a number before a key (a
        // stray separator, an index), and no English of the owner's where the header has the english column (a line of the
        // mod's with its key deleted). A header in other words can't be told from a comment without # by its words or its
        // width. english: the owner's English, cleaned, made on first need.
        private static bool Foreign(List<string> f, Dictionary<string, string> keys, ref HashSet<string> english, int englishAt)
        {
            if (!HasText(f)) return false;
            string k0 = CleanKey(f[0]);
            if (IsKey(k0, keys) || Damaged(f, keys)) return false;
            if ((k0.Length == 0 || AllDigits(k0)) && f.Count > 1 && IsKey(CleanKey(f[1]), keys)) return false;
            if (keys != null && keys.Count > 0 && english == null)
            {
                english = new HashSet<string>(StringComparer.Ordinal);
                foreach (string e in keys.Values) english.Add(Clean(e));
            }
            return english == null || englishAt < 0 || englishAt >= f.Count || !english.Contains(Clean(f[englishAt]));
        }

        // A key of the owner's damaged by one keystroke: a separator typed into it (its parts in the first two cells, with
        // or without the dot it lost), a space typed into it, its dot deleted; or a line typed with spaces for its
        // separators, or as key = value (its first word a key).
        private static bool Damaged(List<string> f, Dictionary<string, string> keys)
        {
            string k0 = CleanKey(f[0]);
            if (k0.Length == 0) return false;
            int word = 0;
            while (word < k0.Length && !char.IsWhiteSpace(k0[word]) && k0[word] != '=' && k0[word] != ':') word++;
            if (word < k0.Length && IsKey(k0.Substring(0, word), keys)) return true;
            if (keys == null || keys.Count == 0) return false;
            if (f.Count > 1)
            {
                string k1 = CleanKey(f[1]);
                if (k1.Length > 0 && (keys.ContainsKey(k0 + k1) || keys.ContainsKey(k0 + "." + k1))) return true;
            }
            var sb = new StringBuilder(k0.Length);
            foreach (char c in k0) if (!char.IsWhiteSpace(c)) sb.Append(c);
            string tight = sb.ToString();
            if (tight != k0 && keys.ContainsKey(tight)) return true;
            if (tight.Length > 64 || tight.IndexOf('.') >= 0) return false;
            for (int i = 1; i < tight.Length; i++) if (keys.ContainsKey(tight.Substring(0, i) + "." + tight.Substring(i))) return true;
            return false;
        }

        // Whether a line holds the rest of the row above, cut by a line break typed into it: the row's own English in one of
        // its cells (the text cut before it), or its first cell the rest of the row's cut english (head).
        private static bool Continues(List<string> f, string english, string head)
        {
            foreach (string c in f) if (Clean(c) == english) return true;
            return head != null && (Clean(head + CleanKey(f[0])) == english || Clean(head + " " + CleanKey(f[0])) == english || Clean(head + "\n" + CleanKey(f[0])) == english);
        }

        // Whether a row before the header names "key" past its first cell, or ends its first cell with it (a garbled BOM).
        private static bool NamesKey(List<string> f)
        {
            if (HeaderName(f[0]).EndsWith("key", StringComparison.Ordinal)) return true;
            for (int i = 1; i < f.Count; i++) if (HeaderName(f[i]) == "key") return true;
            return false;
        }

        // A key: one the owner has (when known), else a dotted word without spaces that starts with a letter.
        private static bool IsKey(string k, Dictionary<string, string> keys)
        {
            if (k.Length == 0) return false;
            if (keys != null && keys.Count > 0 && keys.ContainsKey(k)) return true;
            if (k.IndexOf('.') < 0 || !char.IsLetter(k[0])) return false;
            foreach (char c in k) if (char.IsWhiteSpace(c)) return false;
            return true;
        }

        // A row's first cells with text, for a message: quoted, cut at 24 characters.
        private static string Shown(List<string> f)
        {
            var shown = new List<string>();
            for (int i = 0; i < f.Count && shown.Count < 4; i++)
            {
                string c = Clean(f[i]).Replace('\n', ' ');
                if (c.Length == 0) continue;
                shown.Add("\"" + (c.Length > 24 ? c.Substring(0, 24) + "..." : c) + "\"");
            }
            return string.Join(", ", shown);
        }

        // Whether a row has text in any cell.
        private static bool HasText(List<string> f)
        {
            foreach (string c in f) if (Clean(c).Length > 0) return true;
            return false;
        }

        // Whether one of the owner's keys sits in a cell past the header's named columns (two rows joined).
        private static bool KeyBeyond(List<string> f, int width, Dictionary<string, string> keys)
        {
            for (int i = width; i < f.Count; i++) if (keys.ContainsKey(CleanKey(f[i]))) return true;
            return false;
        }

        // Whether a row has text in a cell past the header's columns.
        private static bool TextBeyond(List<string> f, int width)
        {
            for (int i = width; i < f.Count; i++) if (Clean(f[i]).Length > 0) return true;
            return false;
        }

        // Whether a row's cells after its first are all empty (a row in one cell, maybe with Excel's padding after it).
        private static bool OnlyFirst(List<string> f)
        {
            for (int i = 1; i < f.Count; i++) if (f[i].Trim().Length > 0) return false;
            return true;
        }

        // A header's name for an index column before the key.
        private static bool IndexName(string h)
        {
            string s = h.ToLowerInvariant();
            return s.Length == 0 || s.StartsWith("unnamed") || s == "index" || s == "#" || s == "line" || s == "no" || s == "nr" || s == "id" || s == "n°" || s == "nº";
        }

        // Whether a text holds letters of its language that a page losing them as "?" hasn't (Windows-1252 for the other
        // languages: Cyrillic, ą ł ś, ğ ı ş, Chinese, Japanese, Korean; Windows-1251 for the Western ones: é ü ñ ã): a
        // file saved through such a page holds none, so a file that does wasn't, and its "?" are question marks (the
        // game's own Korean, Russian and French add questions on 1 to 2.5% of their lines).
        private static bool NativeLettersIn(string text, string lang)
        {
            string b = BaseOf(Norm(lang));
            bool western = LegacyText.PageFor(b, out _) == LegacyText.Western;
            string loss = western ? LegacyText.PageFor("ru", out _) : LegacyText.Western;
            foreach (char c in text)
            {
                if (c < '\u00C0' || !char.IsLetter(c)) continue;
                if (loss.IndexOf(c) < 0) return true;
            }
            return false;
        }

        // Letters a code page couldn't hold, written as "?" by the spreadsheet that saved the file (Excel's plain "CSV" of a
        // Russian or Japanese text on a Western Windows): two together, or one with a letter right after it (a real
        // question mark ends a sentence: a space, a quote or "!" after it; in Japanese and Chinese the next sentence may
        // follow at once). Take also counts them against the English.
        private static bool LostLetters(string v, string lang)
        {
            string b = BaseOf(Norm(lang));
            bool eastAsian = b == "ja" || b == "zh" || b == "ko";
            for (int i = 0; i + 1 < v.Length; i++)
            {
                if (v[i] != '?') continue;
                char d = v[i + 1];
                if (d == '?' || (!eastAsian && char.IsLetter(d))) return true;
            }
            return false;
        }

        // The columns a header names, by en.csv's own names only (key, value, english, note; any case, spaces around): no
        // name in other words is guessed (a notes column, "Notes (translator)", was once read as the translation). valueAt
        // -1 when the header doesn't name value and english once each (why: what it lacks); others: the columns en.csv
        // hasn't (never read; Parse says one holding text).
        private static void Columns(List<string> header, out int valueAt, out int englishAt, out List<int> others, out string why)
        {
            valueAt = -1;
            englishAt = -1;
            others = new List<int>();
            why = null;
            for (int i = 1; i < header.Count; i++)
            {
                string h = HeaderName(header[i]);
                if (h == "value")
                {
                    if (valueAt >= 0) why = "value twice";
                    valueAt = i;
                }
                else if (h == "english")
                {
                    if (englishAt >= 0) why = "english twice";
                    englishAt = i;
                }
                else if (h != "note") others.Add(i); // also one without a name: said when it holds text
            }
            if (why == null && valueAt < 0) why = "no value column";
            else if (why == null && englishAt < 0) why = "no english column";
            if (why == null) return;
            var names = new List<string>();
            foreach (int i in others) if (CleanKey(header[i]).Length > 0) names.Add("\"" + CleanKey(header[i]) + "\"");
            if (names.Count > 0) why += "; not en.csv's: " + string.Join(", ", names);
            valueAt = -1;
        }

        // A header's name to match: lower case, Turkish İ and ı as i (.NET and Mono lower "İ" differently).
        private static string HeaderName(string h) => CleanKey(h).Replace('İ', 'i').Replace('ı', 'i').ToLowerInvariant();

        private static string SepName(char sep) => sep == '\t' ? "a tab" : sep.ToString();

        // The separator of a file without a sep= line: the one right after the first cell of its first rows (comments and
        // blank lines skipped; a key never holds one), the most of them, a tie to tab, then ;, then ,; when no row says, the
        // one the first row has most of (SniffLine).
        private static char Sniff(string text, int start)
        {
            int comma = 0, semi = 0, tab = 0, looked = 0;
            string firstRow = null;
            for (int at = start; at < text.Length && looked < 20; at = NextLine(text, at))
            {
                string s = FirstLine(text, at).TrimStart(' ', '\uFEFF', '\u200B');
                if (s.Length == 0 || s[0] == '#') continue;
                if (firstRow == null) firstRow = s;
                looked++;
                char c = AfterFirstCell(s);
                if (c == ',') comma++;
                else if (c == ';') semi++;
                else if (c == '\t') tab++;
            }
            if (comma + semi + tab == 0) return SniffLine(firstRow ?? "");
            if (tab >= semi && tab >= comma) return '\t';
            return semi >= comma ? ';' : ',';
        }

        // The first , ; or tab after a row's first cell (a quoted cell: after its closing quote); '\0': none.
        private static char AfterFirstCell(string s)
        {
            int i = 0;
            if (s.Length > 0 && s[0] == '"')
            {
                for (i = 1; i < s.Length; i++)
                {
                    if (s[i] != '"') continue;
                    if (i + 1 < s.Length && s[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }
                    i++;
                    break;
                }
            }
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ',' || c == ';' || c == '\t') return c;
            }
            return '\0';
        }

        // One row's separator: after a "key" header's first word, else the one it has most of outside quotes; a tie goes
        // to tab, then ;, then , (a text has commas more often than tabs).
        private static char SniffLine(string firstLine)
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
        // broken: the lines of rows with a quote inside a quoted field that neither doubles nor closes it ("Bonjour" dit-il,
        // puis "salut": the outer quotes lost), when asked.
        private static List<(List<string> Fields, int Line)> ParseCsv(string text, int start, int line, char sep, out bool unclosed, HashSet<int> broken = null)
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
                        else
                        {
                            sb.Append('"'); // a quote inside a quoted text that doesn't end it: the quoting is broken
                            broken?.Add(rowLine);
                        }
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
        // the file: spaces or a quote may come first, maybe an index number and the separator, then a key (one of the
        // mod's when it knows them: a web address in a multi-line text is no row; else key-shaped: a letter, a dot after at
        // least two characters, no dot at the end), maybe a closing quote and spaces, then the separator. "e.g.," is no
        // row; a false alarm costs a read line by line, which is said.
        private static bool Swallowed(string value, char sep, Dictionary<string, string> keys)
        {
            int at = value.IndexOf('\n');
            while (at >= 0)
            {
                int k = at + 1;
                while (k < value.Length && (value[k] == ' ' || value[k] == '\u00A0' || Array.IndexOf(Invisible, value[k]) >= 0)) k++;
                bool quoted = k < value.Length && value[k] == '"';
                if (quoted) k++;
                int j = k;
                while (j < value.Length && (char.IsLetterOrDigit(value[j]) || value[j] == '.' || value[j] == '_' || value[j] == '-' || value[j] == '/')) j++;
                int m = j;
                if (quoted && m < value.Length && value[m] == '"') m++;
                while (m < value.Length && (value[m] == ' ' || value[m] == '\u00A0')) m++;
                // (a quoted key whose own closing quote closed the text: the key ends it)
                if (j - k >= 1 && m < value.Length && value[m] == sep && AllDigits(value.Substring(k, j - k)))
                {
                    // an index number first (pandas' to_csv): the key after it
                    k = m + 1;
                    while (k < value.Length && (value[k] == ' ' || value[k] == '\u00A0')) k++;
                    quoted = k < value.Length && value[k] == '"';
                    if (quoted) k++;
                    j = k;
                    while (j < value.Length && (char.IsLetterOrDigit(value[j]) || value[j] == '.' || value[j] == '_' || value[j] == '-' || value[j] == '/')) j++;
                    m = j;
                    if (quoted && m < value.Length && value[m] == '"') m++;
                    while (m < value.Length && (value[m] == ' ' || value[m] == '\u00A0')) m++;
                }
                if (j - k >= 2 && ((m < value.Length && value[m] == sep) || (quoted && j == value.Length)))
                {
                    string token = value.Substring(k, j - k);
                    bool known = keys != null && keys.Count > 0;
                    if (known ? keys.ContainsKey(token) : KeyShaped(token)) return true;
                }
                at = value.IndexOf('\n', at + 1);
            }
            return false;
        }

        private static bool AllDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return s.Length > 0;
        }

        internal static bool KeyShaped(string token)
        {
            int dot = token.IndexOf('.');
            return token.Length > 0 && char.IsLetter(token[0]) && dot >= 2 && token[token.Length - 1] != '.' && token.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        // A file whose quote never closes: each line on its own (no text across lines), so one bad line costs one line.
        // A line whose own quote never closes is left out (its key shows English) and said, with the lines after it up to
        // one that starts as a row (a key, a header, a comment, a blank line): its text's (a line of a text holding commas
        // would stop the reading as none of the mod's lines).
        private static List<(List<string> Fields, int Line)> ParseByLine(string text, int start, int line, char sep, File file, HashSet<int> broken, Dictionary<string, string> keys)
        {
            var rows = new List<(List<string>, int)>();
            int at = start;
            bool inText = false;
            while (at < text.Length)
            {
                string one = FirstLine(text, at);
                if (inText)
                {
                    var probe = ParseCsv(one, 0, line, sep, out _, new HashSet<int>());
                    string first = probe.Count > 0 && probe[0].Fields.Count > 0 ? CleanKey(probe[0].Fields[0]) : "";
                    bool row = one.Trim().Length == 0 || first.StartsWith("#", StringComparison.Ordinal) || first.Equals("key", StringComparison.OrdinalIgnoreCase) || IsKey(first, keys)
                        || (probe.Count > 0 && Damaged(probe[0].Fields, keys))
                        || ((first.Length == 0 || AllDigits(first)) && probe.Count > 0 && probe[0].Fields.Count > 1 && IsKey(CleanKey(probe[0].Fields[1]), keys));
                    if (!row)
                    {
                        file.Malformed++;
                        at = NextLine(text, at);
                        line++;
                        continue;
                    }
                    inText = false;
                }
                var parsed = ParseCsv(one, 0, line, sep, out bool open, broken);
                if (open)
                {
                    file.Malformed++;
                    inText = true;
                    Warn(file.Path, line, "has a quote that is never closed on this line (a \" in a text is written \"\"); left out, with the lines after it up to the next key's");
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

        // A text as meant: spaces and invisible characters off its ends (inside it they stay), \n a line break, a tab a
        // space, the <nobr> tags out (Texts.Plain), and {0} however it was typed: { 0 }, {０} and ｛０｝ (Japanese and
        // Chinese input), {O} (a letter O).
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = TrimInvisible(s);
            if (s.IndexOf("\\n", StringComparison.Ordinal) >= 0) s = s.Replace("\\n", "\n");
            if (s.IndexOf('\t') >= 0) s = s.Replace('\t', ' ');
            if (s.IndexOf('\r') >= 0) s = s.Replace("\r", "");
            s = TrimInvisible(Texts.Plain(s));
            if (s.IndexOf("{{", StringComparison.Ordinal) >= 0) s = Undoubled(s);
            if (s.IndexOf('{') >= 0 || s.IndexOf('｛') >= 0) s = Placeholders(s);
            return s;
        }

        private static string TrimInvisible(string s)
        {
            int a = 0, b = s.Length;
            while (a < b && (char.IsWhiteSpace(s[a]) || Array.IndexOf(Invisible, s[a]) >= 0)) a++;
            while (b > a && (char.IsWhiteSpace(s[b - 1]) || Array.IndexOf(Invisible, s[b - 1]) >= 0)) b--;
            return a == 0 && b == s.Length ? s : s.Substring(a, b - a);
        }

        // {{0}} as {0}: other translation tools write their placeholders doubled; our texts never hold a brace of their own.
        private static string Undoubled(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '{' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    int j = i + 2;
                    bool number = false;
                    while (j < s.Length && (char.IsDigit(s[j]) || s[j] == ' '))
                    {
                        number |= char.IsDigit(s[j]);
                        j++;
                    }
                    if (number && j + 1 < s.Length && s[j] == '}' && s[j + 1] == '}')
                    {
                        sb.Append('{').Append(s, i + 2, j - i - 2).Append('}');
                        i = j + 1;
                        continue;
                    }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
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
                    sb.Append(c); // no placeholder: the brace as written (｛ stays ｛)
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
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // A spreadsheet's error cell instead of a text, in any of its languages (#NAME?, #VALUE!, #N/A, #ИМЯ?, #¿NOMBRE?,
        // #NUL!, #N/D, #YOK, LibreOffice's Err:502), by its shape: a # and at most 16 characters without a space, ending in
        // ? or !, holding a /, or all capitals.
        private static bool IsSheetError(string value)
        {
            string v = value.Trim();
            if (v.StartsWith("Err:", StringComparison.OrdinalIgnoreCase) && v.Length <= 8) return true; // LibreOffice
            if (v.Length < 3 || v.Length > 16 || v[0] != '#') return false;
            for (int i = 1; i < v.Length; i++) if (char.IsWhiteSpace(v[i])) return false;
            char last = v[v.Length - 1];
            if (last == '?' || last == '!' || v.IndexOf('/') > 0) return true;
            for (int i = 1; i < v.Length; i++) if (!char.IsUpper(v[i]) && v[i] != '_') return false;
            return true;
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
