using System.Text;
using GK2DonkeyAPI;

// The translation reader without the game (docs/localization.md, "Translator mistakes"):
// 1. the translator mistake catalogue (TranslationTests.RunCatalogue, the same the in-game self-test runs): every case
//    must show what it should, never a wrong text, and tell the translator exactly when the case says so;
// 2. every shipped Languages/*.csv (src/*, packs/*) read as the game reads it: no warning allowed, and no line the mod
//    would leave out (no English, a key its en.csv hasn't, a key twice).
// 3. the sweep: every shipped translation saved every wrong way (Sweep), never a wrong text; then single lines as a
//    translator's override (SmallFiles), a code-page line in a UTF-8 file (MixedFiles), one edit typed into a line
//    (EditedLines), the game's own tables (Corpus, with GK2_L10N_CORPUS), and the header's columns renamed, reordered
//    and added to (Headers).
// Exit 1 on any failure. Run: dotnet run -c Release (from this folder), after building the API (Deploy=false is
// enough). It runs on .NET, the game on Mono: the in-game self-test (F11, Translations) runs the same catalogue there.
internal static class Program
{
    private static int Main()
    {
        int fails = 0;
        // the API as last built: a source file newer than the DLL means a stale build tested (it said nothing before)
        string api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GK2DonkeyAPI"));
        string dll = typeof(Texts).Assembly.Location;
        DateTime built = File.GetLastWriteTimeUtc(dll);
        foreach (string cs in Directory.GetFiles(api, "*.cs", SearchOption.AllDirectories))
        {
            if (cs.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || cs.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            if (File.GetLastWriteTimeUtc(cs) <= built) continue;
            Console.WriteLine($"STALE: {Path.GetFileName(cs)} is newer than the API's DLL ({dll}): build it first (dotnet build src/GK2DonkeyAPI -c Release -p:Deploy=false)");
            return 1;
        }
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationScan-" + Environment.ProcessId);
        List<(string Name, bool Good, string Why)> results = TranslationTests.RunCatalogue(tmp);
        foreach (var r in results)
        {
            Console.WriteLine($"{(r.Good ? "PASS" : "FAIL")} mistake: {r.Name}{(r.Good ? "" : " -> " + r.Why)}");
            if (!r.Good) fails++;
        }
        Console.WriteLine($"{results.Count - fails} of {results.Count} mistakes handled");

        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var files = new List<string>();
        foreach (string top in new[] { "src", "packs" })
        {
            string dir = Path.Combine(repo, top);
            if (!Directory.Exists(dir)) continue;
            foreach (string lang in Directory.GetDirectories(dir, "Languages", SearchOption.AllDirectories))
            {
                if (lang.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || lang.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                files.AddRange(Directory.GetFiles(lang, "*.csv"));
            }
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        bool quiet = Texts.TestQuiet;
        Texts.TestQuiet = true;
        // the time reading takes (the game reads each mod's file in one frame when a language is read ahead)
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int timed = 0;
        foreach (string f in files) { TranslationFiles.Read(f, Path.GetFileNameWithoutExtension(f)); timed++; }
        watch.Stop();
        Console.WriteLine($"timing: {timed} shipped files read in {watch.ElapsedMilliseconds} ms (the slowest one: see below)");
        long slowest = 0; string slowName = null;
        foreach (string f in files)
        {
            var w = System.Diagnostics.Stopwatch.StartNew();
            for (int k = 0; k < 5; k++) TranslationFiles.Read(f, Path.GetFileNameWithoutExtension(f));
            w.Stop();
            if (w.ElapsedTicks > slowest) { slowest = w.ElapsedTicks; slowName = Path.GetRelativePath(repo, f); }
        }
        Console.WriteLine($"timing: the slowest, {slowName}: {slowest * 1000.0 / System.Diagnostics.Stopwatch.Frequency / 5:0.0} ms a read");
        foreach (string f in files)
        {
            var heard = new List<string>();
            TranslationFiles.TestHeard = heard;
            Texts.TestWarnings = 0;
            string id = Path.GetFileNameWithoutExtension(f);
            TranslationFiles.File read = TranslationFiles.Read(f, id);
            // the lines the mod would leave out: without their English, of a key the English hasn't, a key twice
            var englishOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string en = Path.Combine(Path.GetDirectoryName(f), "en.csv");
            if (File.Exists(en)) foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) englishOf[r.Key] = r.Value;
            var leftOut = new List<string>();
            foreach (TranslationFiles.Row r in read.Rows)
            {
                if (r.English == null) leftOut.Add(r.Key + " (no English)");
                else if (englishOf.Count > 0 && !englishOf.ContainsKey(r.Key)) leftOut.Add(r.Key + " (not in en.csv)");
                else if (englishOf.Count > 0 && r.English != englishOf[r.Key]) leftOut.Add(r.Key + " (its English changed since)");
                else if (englishOf.Count > 0 && !string.IsNullOrEmpty(TranslationFiles.PlaceholdersOf(r.Value)) && !Within(TranslationFiles.PlaceholdersOf(r.Value), TranslationFiles.PlaceholdersOf(englishOf[r.Key])))
                    leftOut.Add(r.Key + " (a {n} its English hasn't)");
            }
            if (read.Duplicates > 0) leftOut.Add(read.Duplicates + " key(s) twice");
            bool good = read.Usable && read.Malformed == 0 && heard.Count == 0 && read.How == null && read.Rows.Count > 0 && leftOut.Count == 0;
            Console.WriteLine($"{(good ? "PASS" : "FAIL")} {Path.GetRelativePath(repo, f)}: {read.Rows.Count} row(s){(read.How != null ? ", read as " + read.How : "")}{(heard.Count > 0 ? "; said: " + string.Join(" | ", heard) : "")}{(leftOut.Count > 0 ? "; left out by the mod: " + string.Join(", ", leftOut) : "")}");
            if (!good) fails++;
        }
        Texts.TestQuiet = quiet;
        TranslationFiles.TestHeard = null;
        fails += Sweep(repo, files);
        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails == 0 ? 0 : 1;
    }

    // Every {n} of have also in need (both as TranslationFiles.PlaceholdersOf writes them: "{0}{1}").
    private static bool Within(string have, string need)
    {
        for (int i = 0; i < have.Length;)
        {
            int end = have.IndexOf('}', i);
            if (end < 0 || !need.Contains(have.Substring(i, end - i + 1))) return false;
            i = end + 1;
        }
        return true;
    }

    // Every row of a file whole in its first cell, quoted, its quotes doubled, the file's own line ends kept between the
    // rows (Excel in a ; country opening a , file and saving it as CSV UTF-8).
    private static string InOneCell(string s)
    {
        string end = s.Contains("\r\n") ? "\r\n" : s.Contains('\r') ? "\r" : "\n";
        string[] rows = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return string.Join(end, rows.Select(l => l.Length == 0 ? "" : "\"" + l.Replace("\"", "\"\"") + "\""));
    }

    // A , file as Excel in a ; country opens and saves it: each row read with ; (a quote opens a quoted cell only at a
    // cell's start), every cell written back quoted when it holds ; or a quote, the rows padded with ; to the widest.
    private static string ExcelSemicolon(string s)
    {
        string end = s.Contains("\r\n") ? "\r\n" : s.Contains('\r') ? "\r" : "\n";
        var rows = new List<List<string>>();
        foreach (string line in s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false, start = true;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                    continue;
                }
                if (c == '"' && start) { quoted = true; start = false; continue; }
                if (c == ';') { cells.Add(sb.ToString()); sb.Clear(); start = true; continue; }
                sb.Append(c);
                start = false;
            }
            cells.Add(sb.ToString());
            rows.Add(cells);
        }
        int widest = rows.Max(r => r.Count);
        return string.Join(end, rows.Select(r => r.Count == 1 && r[0].Length == 0 ? "" : string.Join(";", r.Select(c => c.IndexOfAny(new[] { ';', '"', '\n' }) >= 0 ? "\"" + c.Replace("\"", "\"\"") + "\"" : c).Concat(Enumerable.Repeat("", widest - r.Count)))));
    }

    // ---- 3. the sweep ----

    private sealed class Way
    {
        internal string Name;
        internal Func<string, byte[]> Save; // the file's text (line ends already as wanted) to its bytes
        internal bool MustRead;             // a correct way: every translation must show
        internal bool Bytes;                // the file in a code page (not UTF-8 or UTF-16): the small-file phase saves lines so
        internal bool Garbling;             // UTF-8 garbled through a page and saved again: the small-file phase too
        internal Func<string, string> AsWritten; // a translation as this way writes it into the file (garbled, "?" for letters): shown so is no text the mod made
    }

    // The ways a person's program saves a translation, for a language: correct ones (UTF-8, UTF-16, Excel's plain "CSV"
    // in the language's Windows page) and wrong ones (Excel's CSV (MS-DOS) and CSV (Macintosh), letters lost as "?" in
    // a page without them, a UTF-8 file opened in another page and saved again as UTF-8).
    private static List<Way> WaysFor(string id)
    {
        Encoding E(int cp) => Encoding.GetEncoding(cp, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?"));
        byte[] Garbled(string s, int cp) => new UTF8Encoding(false).GetBytes(E(cp).GetString(Encoding.UTF8.GetBytes(s)));
        Func<string, string> GarbledText(int cp) => s => TranslationFiles.Clean(E(cp).GetString(Encoding.UTF8.GetBytes(s)));
        Func<string, string> LostText(int cp) => s => TranslationFiles.Clean(E(cp).GetString(E(cp).GetBytes(s)));
        int windows = id switch { "pl" => 1250, "ru" => 1251, "tr" => 1254, "ja" => 932, "zh_cn" => 936, "ko" => 949, _ => 1252 };
        int dos = id switch { "pl" => 852, "ru" => 866, "tr" => 857, "ja" => 932, "zh_cn" => 936, "ko" => 949, _ => 850 };
        int mac = id switch { "pl" => 10029, "ru" => 10007, "tr" => 10081, "ja" => 10001, "zh_cn" => 10008, "ko" => 10003, _ => 10000 };
        bool cjk = id == "ja" || id == "zh_cn" || id == "ko";
        bool western = windows == 1252;
        int[] iso = id switch { "pl" => new[] { 28592 }, "ru" => new[] { 28595, 20866 }, "tr" => new[] { 28599 }, "ja" or "zh_cn" or "ko" => new int[0], _ => new[] { 28605 } };
        var ways = new List<Way>
        {
            new Way { Name = "UTF-8 with a BOM", Save = s => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(s)).ToArray(), MustRead = true },
            new Way { Name = "UTF-8", Save = s => new UTF8Encoding(false).GetBytes(s), MustRead = true },
            new Way { Name = "UTF-16 with a BOM", Save = s => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(s)).ToArray(), MustRead = true },
            new Way { Name = "UTF-16 without a BOM", Save = s => Encoding.Unicode.GetBytes(s), MustRead = true },
            new Way { Name = "UTF-16 big-endian without a BOM", Save = s => Encoding.BigEndianUnicode.GetBytes(s), MustRead = true },
            // Excel in a ; country opening a , file and saving it as CSV UTF-8: every row whole in its first cell, quoted, its
            // quotes doubled (code review round 7: the re-split path)
            new Way { Name = "Excel in a ; country: every row whole in one cell", Save = s => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(InOneCell(s))).ToArray(), MustRead = true },
            // as Excel saves it: a row's own ; splits it into cells, short rows padded with ; (a row with ; in its text is
            // left out: never a text the file hasn't)
            new Way { Name = "Excel in a ; country as it saves (split at ;, padded)", Save = s => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ExcelSemicolon(s))).ToArray() },
            // a code-page file isn't read (TranslationFiles.ReadCodePages); one valid UTF-8 after all shows its text as written
            new Way { Name = $"Excel's plain CSV (code page {windows})", Save = s => E(windows).GetBytes(s), Bytes = true, AsWritten = LostText(windows) },
            new Way { Name = $"Excel's CSV (MS-DOS) (code page {dos})", Save = s => E(dos).GetBytes(s), Bytes = true, AsWritten = LostText(dos) },
            new Way { Name = $"Excel's CSV (Macintosh) (code page {mac})", Save = s => E(mac).GetBytes(s), Bytes = true, AsWritten = LostText(mac) },
            new Way { Name = western ? "letters lost as ? (plain CSV on a Russian Windows, 1251)" : "letters lost as ? (plain CSV on a Western Windows, 1252)", Save = s => E(western ? 1251 : 1252).GetBytes(s), AsWritten = LostText(western ? 1251 : 1252) },
            // garbling: never a wrong text; a line two pages repair differently is left out (English), so no must-read
            new Way { Name = $"garbled: opened as {windows}, saved as UTF-8", Save = s => Garbled(s, windows), Garbling = true, AsWritten = GarbledText(windows) },
            new Way { Name = "garbled: opened as 1252, saved as UTF-8", Save = s => Garbled(s, 1252), Garbling = true, AsWritten = GarbledText(1252) },
            new Way { Name = "garbled: opened as Mac Roman, saved as UTF-8", Save = s => Garbled(s, 10000), Garbling = true, AsWritten = GarbledText(10000) },
            new Way { Name = "garbled: opened as ISO-8859-1, saved as UTF-8", Save = s => Garbled(s, 28591), Garbling = true, AsWritten = GarbledText(28591) },
            new Way { Name = "garbled: opened as ISO-8859-15, saved as UTF-8", Save = s => Garbled(s, 28605), Garbling = true, AsWritten = GarbledText(28605) },
            new Way { Name = "garbled: opened as GBK (936), saved as UTF-8", Save = s => Garbled(s, 936), AsWritten = GarbledText(936) },
            new Way { Name = "garbled: opened as Shift-JIS (932), saved as UTF-8", Save = s => Garbled(s, 932), AsWritten = GarbledText(932) },
            new Way { Name = "garbled: opened as Korean (949), saved as UTF-8", Save = s => Garbled(s, 949), AsWritten = GarbledText(949) },
        };
        if (mac != 10000 && !cjk) ways.Add(new Way { Name = $"garbled: opened as its Mac page ({mac}), saved as UTF-8", Save = s => Garbled(s, mac), Garbling = true, AsWritten = GarbledText(mac) });
        foreach (int cp in iso) ways.Add(new Way { Name = $"saved in code page {cp} (ISO/KOI8: Linux, LibreOffice)", Save = s => E(cp).GetBytes(s), Bytes = true, AsWritten = LostText(cp) });
        return ways;
    }

    private static int Sweep(string repo, List<string> files)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationSweep-" + Environment.ProcessId);
        int fails = 0, reads = 0, wrongTotal = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        var limits = new List<string>(); // the documented limits: counted, said, not failed (docs/localization.md section 11)
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            Texts.TestTranslators = new List<string>();
            int folderNo = 0;
            foreach (string folder in files.Select(Path.GetDirectoryName).Distinct())
            {
                string en = Path.Combine(folder, "en.csv");
                if (!File.Exists(en)) continue;
                string owner = "sweep.folder" + (++folderNo);
                var english = new List<(string Key, string English, string What)>();
                foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) english.Add((r.Key, r.Value, null));
                Texts.TestEnglish[owner] = english;
                string dir = Path.Combine(tmp, owner);
                Directory.CreateDirectory(dir);
                Texts.TestBundled[owner] = dir;
                foreach (string f in files.Where(x => Path.GetDirectoryName(x) == folder))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == "en") continue;
                    string text = File.ReadAllText(f, new UTF8Encoding(false)).TrimStart('\uFEFF').Replace("\r\n", "\n");
                    Texts.LanguageOverride = id;
                    // the translations as the correct file shows them
                    File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(text));
                    Texts.Forget();
                    var right = new Dictionary<string, string>();
                    foreach (var x in english) right[x.Key] = Texts.Of(owner, x.Key, x.English);
                    foreach (Way w in WaysFor(id))
                    {
                        foreach (string end in new[] { "\n", "\r\n", "\r" })
                        {
                            File.WriteAllBytes(Path.Combine(dir, id + ".csv"), w.Save(text.Replace("\n", end)));
                            Texts.Forget();
                            reads++;
                            int wrong = 0, englishShown = 0;
                            var wrongs = new List<string>();
                            foreach (var x in english)
                            {
                                string got = Texts.Of(owner, x.Key, x.English);
                                if (got == right[x.Key]) continue;
                                if (got == x.English) { englishShown++; continue; }
                                if (w.AsWritten != null && got == w.AsWritten(right[x.Key])) continue; // the file's own text, as written
                                wrong++;
                                if (wrongs.Count < 3) wrongs.Add($"{x.Key} \"{(got.Length > 30 ? got.Substring(0, 30) + "..." : got)}\" for \"{(right[x.Key].Length > 30 ? right[x.Key].Substring(0, 30) + "..." : right[x.Key])}\"");
                            }
                            string endName = end == "\n" ? "LF" : end == "\r\n" ? "CRLF" : "CR";
                            string where = $"{Path.GetRelativePath(repo, f)}, {w.Name}, {endName}";
                            bool limit = Limit(id, w.Name);
                            if (wrong > 0 && limit) limits.Add($"{where}: {wrong} wrong (a documented limit)");
                            else if (wrong > 0 || (w.MustRead && englishShown > 0))
                            {
                                fails++;
                                wrongTotal += wrong;
                                Console.WriteLine($"FAIL sweep: {where}: {wrong} WRONG, {englishShown} English{(wrongs.Count > 0 ? " (" + string.Join("; ", wrongs) + ")" : "")}");
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (string o in Texts.TestEnglish.Keys.Where(k => k.StartsWith("sweep.")).ToList()) { Texts.TestEnglish.Remove(o); Texts.TestBundled.Remove(o); }
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        foreach (string l in limits) Console.WriteLine("LIMIT sweep: " + l);
        Console.WriteLine($"sweep: {reads} reads of the shipped files saved wrong ways, {fails} failing, {limits.Count} at a documented limit");
        return fails + SmallFiles(repo, files) + MixedFiles(repo, files) + EditedLines(repo, files) + Headers(repo, files) + Corpus();
    }

    // Headers (code review round 9's fuzzer as a test): every shipped translation with its header's columns renamed one at
    // a time (en.csv's names in other cases and spaces; names in other words: the translator's language, a hint added, a
    // note named with a translation word, a name twice), all four in other words, the four columns in all 24 orders with
    // their cells, and a column of the translator's own put in at each place: every key shows its translation or
    // English; with en.csv's names, the key first and the value before the english, every translation shows.
    private static int Headers(string repo, List<string> files)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationHeaders-" + Environment.ProcessId);
        string[] own = { "key", "value", "english", "note" };
        string[][] names =
        {
            new[] { "key", "Key", " KEY ", "clé", "Schlüssel", "キー", "id", "Key (do not change)" },
            new[] { "value", "Value", "VALUE ", "valeur", "Wert", "Value (French)", "translation", "Übersetzung", "french", "{id}", "翻訳", "text" },
            new[] { "english", "English", " ENGLISH", "anglais", "Englisch", "source", "en", "original", "英語", "English (do not change)" },
            new[] { "note", "Note", "NOTE ", "", "notes", "Notes (translator)", "Notes de traduction", "Translator notes", "翻訳メモ", "comment", "todo", "Übersetzungshinweis", "value", "english" },
        };
        string[][] otherWords =
        {
            new[] { "clé", "valeur", "anglais", "note" }, new[] { "Schlüssel", "Wert", "Englisch", "Anmerkung" },
            new[] { "キー", "値", "英語", "メモ" }, new[] { "clave", "valor", "inglés", "nota" },
        };
        string[] extras = { "checked", "todo", "Notes (translator)", "translation", "french", "ok", "", "new english", "value", "english" };
        var orders = new List<int[]>();
        for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
                for (int c = 0; c < 4; c++)
                    for (int d = 0; d < 4; d++)
                        if (a != b && a != c && a != d && b != c && b != d && c != d) orders.Add(new[] { a, b, c, d });
        int fails = 0, reads = 0, allRead = 0, allEnglish = 0, some = 0, controls = 0, caught = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        var failing = new Dictionary<string, int>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            Texts.TestTranslators = new List<string>();
            int folderNo = 0;
            foreach (string folder in files.Select(Path.GetDirectoryName).Distinct())
            {
                string en = Path.Combine(folder, "en.csv");
                if (!File.Exists(en)) continue;
                string owner = "headers.folder" + (++folderNo);
                var english = new List<(string Key, string English, string What)>();
                foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) english.Add((r.Key, r.Value, null));
                Texts.TestEnglish[owner] = english;
                string dir = Path.Combine(tmp, owner);
                Directory.CreateDirectory(dir);
                Texts.TestBundled[owner] = dir;
                foreach (string f in files.Where(x => Path.GetDirectoryName(x) == folder))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == "en") continue;
                    string text = File.ReadAllText(f, new UTF8Encoding(false)).TrimStart('\uFEFF').Replace("\r\n", "\n");
                    List<List<string>> rows = Cells(text);
                    if (rows.Count == 0 || string.Join(",", rows[0]).ToLowerInvariant() != "key,value,english,note")
                    {
                        fails++;
                        Console.WriteLine($"FAIL headers: {Path.GetRelativePath(repo, f)}: its header isn't en.csv's (key,value,english,note)");
                        continue;
                    }
                    foreach (List<string> r in rows) while (r.Count < 4) r.Add("");
                    Texts.LanguageOverride = id;
                    File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(text));
                    Texts.Forget();
                    var right = new Dictionary<string, string>();
                    foreach (var x in english) right[x.Key] = Texts.Of(owner, x.Key, x.English);
                    // one read: the header's names in the template's column order, the columns in an order, maybe one more
                    // (control: every value altered, which the check must see)
                    void Try(string kind, string[] header, int[] order, int extraAt, string extraName, bool control = false)
                    {
                        var copy = new List<List<string>>();
                        for (int i = 0; i < rows.Count; i++)
                        {
                            List<string> r = rows[i];
                            if (i > 0 && r[0].StartsWith("#")) { copy.Add(r); continue; }
                            var row = new List<string>();
                            foreach (int c in order) row.Add(i == 0 ? header[c] : control && c == 1 && r[c].Length > 0 ? "x" + r[c] : r[c]);
                            if (extraAt >= 0) row.Insert(extraAt, i == 0 ? extraName : "ok");
                            copy.Add(row);
                        }
                        File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(Csv(copy)));
                        Texts.Forget();
                        reads++;
                        bool named = true;
                        for (int c = 0; c < 4; c++) named &= header[c].Trim().ToLowerInvariant() == own[c] || (c == 3 && header[c].Trim().Length == 0);
                        // (a column put in under en.csv's name of another: a name twice, not read)
                        bool twice = extraName != null && (extraName.Trim().ToLowerInvariant() == "value" || extraName.Trim().ToLowerInvariant() == "english");
                        bool mustRead = named && !twice && order[0] == 0 && Array.IndexOf(order, 1) < Array.IndexOf(order, 2);
                        int shown = 0, englishShown = 0, translated = 0;
                        foreach (var x in english)
                        {
                            if (right[x.Key] == x.English) continue;
                            translated++;
                            string got = Texts.Of(owner, x.Key, x.English);
                            if (got == right[x.Key]) shown++;
                            else if (got == x.English) englishShown++;
                        }
                        if (control) { } // counted below
                        else if (shown == translated) allRead++;
                        else if (englishShown == translated) allEnglish++;
                        else some++;
                        if (control)
                        {
                            controls++;
                            reads--;
                            bool seen = false;
                            foreach (var x in english) seen |= Texts.Of(owner, x.Key, x.English) != right[x.Key] && Texts.Of(owner, x.Key, x.English) != x.English;
                            if (seen) caught++;
                            else Console.WriteLine($"FAIL headers: {Path.GetRelativePath(repo, f)}, the control (every value altered) shows no wrong text: the check checks nothing");
                            if (!seen) fails++;
                            return;
                        }
                        foreach (var x in english)
                        {
                            string got = Texts.Of(owner, x.Key, x.English);
                            if (got == right[x.Key] || (!mustRead && got == x.English)) continue;
                            fails++;
                            failing[kind] = failing.TryGetValue(kind, out int n) ? n + 1 : 1;
                            if (failing[kind] <= 3)
                                Console.WriteLine($"FAIL headers: {Path.GetRelativePath(repo, f)}, {kind}: \"{Csv(new List<List<string>> { copy[0] }).TrimEnd('\n')}\": {x.Key} \"{got}\" for \"{right[x.Key]}\"");
                            break;
                        }
                    }
                    foreach (int[] order in orders)
                    {
                        string orderName = string.Join(",", order.Select(c => own[c]));
                        Try("en.csv's names, " + orderName, own, order, -1, null);
                        for (int c = 0; c < 4; c++)
                        {
                            foreach (string n in names[c])
                            {
                                if (n == own[c]) continue;
                                var header = (string[])own.Clone();
                                header[c] = n.Replace("{id}", id);
                                Try($"{own[c]} named \"{header[c]}\", {orderName}", header, order, -1, null);
                            }
                        }
                        foreach (string[] words in otherWords) Try($"in other words ({string.Join(",", words)}), {orderName}", words, order, -1, null);
                    }
                    for (int at = 1; at <= 4; at++)
                        foreach (string extra in extras) Try($"a column \"{extra}\" put in at {at}", own, new[] { 0, 1, 2, 3 }, at, extra);
                    Try("control", own, new[] { 0, 1, 2, 3 }, -1, null, control: true);
                }
            }
        }
        finally
        {
            foreach (string o in Texts.TestEnglish.Keys.Where(k => k.StartsWith("headers.")).ToList()) { Texts.TestEnglish.Remove(o); Texts.TestBundled.Remove(o); }
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        watch.Stop();
        Console.WriteLine($"headers: {reads} reads of the shipped files with their header's columns renamed, reordered or added to, {fails} failing ({watch.ElapsedMilliseconds / 1000} s): {allRead} every translation shown, {allEnglish} English (not read), {some} some of each; the control caught on {caught} of {controls} files");
        return fails;
    }

    // A , file's cells, row by row (quotes as a spreadsheet writes them; the text's line ends LF).
    private static List<List<string>> Cells(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false, start = true;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
                continue;
            }
            if (c == '"' && start) { quoted = true; start = false; continue; }
            if (c == ',' || c == '\n')
            {
                row.Add(sb.ToString());
                sb.Clear();
                start = true;
                if (c == '\n') { rows.Add(row); row = new List<string>(); }
                continue;
            }
            sb.Append(c);
            start = false;
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows;
    }

    // Rows as a , file, a cell quoted when it holds a , a quote, a line break or spaces at its ends.
    private static string Csv(List<List<string>> rows) =>
        string.Join("\n", rows.Select(r => string.Join(",", r.Select(c => c.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 || c != c.Trim() ? "\"" + c.Replace("\"", "\"\"") + "\"" : c)))) + "\n";

    // Edited lines (code review rounds 8 and 9's fuzzers as a test): every shipped row, one edit at a time as a text
    // editor makes it: typed into the middle of an unquoted text (a comma, a ;, a quote, a line break, a CR, a literal \n,
    // a brace, a #, a tab), a comma typed into its english, its key's dot deleted, its text's opening or closing quote
    // deleted, a comma or a space typed into its key: every key shows its translation, English, or the edited text as
    // written, and no other key falls to English (the blast). A comment line without # put in after it stops the reading
    // up to a header (a header in other words can't be told from it): counted and said here, failing only on a wrong text.
    private static int EditedLines(string repo, List<string> files)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationEdits-" + Environment.ProcessId);
        string[] typed = { ",", ";", "\"", "\n", "\r", "\\n", "{", "}", "#", "\t" };
        int fails = 0, reads = 0, stops = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        var failing = new Dictionary<string, int>();
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            Texts.TestTranslators = new List<string>();
            int folderNo = 0;
            foreach (string folder in files.Select(Path.GetDirectoryName).Distinct())
            {
                string en = Path.Combine(folder, "en.csv");
                if (!File.Exists(en)) continue;
                string owner = "edits.folder" + (++folderNo);
                var english = new List<(string Key, string English, string What)>();
                foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) english.Add((r.Key, r.Value, null));
                Texts.TestEnglish[owner] = english;
                string dir = Path.Combine(tmp, owner);
                Directory.CreateDirectory(dir);
                Texts.TestBundled[owner] = dir;
                foreach (string f in files.Where(x => Path.GetDirectoryName(x) == folder))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == "en") continue;
                    string text = File.ReadAllText(f, new UTF8Encoding(false)).TrimStart('\uFEFF').Replace("\r\n", "\n");
                    Texts.LanguageOverride = id;
                    File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(text));
                    Texts.Forget();
                    var right = new Dictionary<string, string>();
                    foreach (var x in english) right[x.Key] = Texts.Of(owner, x.Key, x.English);
                    // one read: a wrong text fails, and another key fallen to English (the blast) unless the edit stops the
                    // reading on purpose (stop); the edited row's key may show English, or its edited text as written
                    void Read(string kind, string edited, string key, string editedValue, bool stop)
                    {
                        File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(edited));
                        Texts.Forget();
                        reads++;
                        int lost = 0;
                        string bad = null;
                        foreach (var x in english)
                        {
                            string got = Texts.Of(owner, x.Key, x.English);
                            if (got == right[x.Key]) continue;
                            bool mine = key != null && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase);
                            if (got == x.English)
                            {
                                if (!mine) lost++;
                                continue;
                            }
                            if (mine && editedValue != null && got == EditedAsWritten(editedValue, x.English)) continue;
                            bad ??= $"{x.Key} \"{got}\" for \"{right[x.Key]}\"";
                        }
                        if (bad == null && (lost == 0 || stop))
                        {
                            if (stop && lost > 0) stops++;
                            return;
                        }
                        fails++;
                        failing[kind] = failing.TryGetValue(kind, out int n) ? n + 1 : 1;
                        if (failing[kind] <= 3) Console.WriteLine($"FAIL edits: {Path.GetRelativePath(repo, f)}, {kind}{(key != null ? " at " + key : "")}: {bad ?? lost + " other key(s) fell to English"}");
                    }
                    // the rows by their start: a line's start outside quotes
                    var starts = new List<int>();
                    bool inQuotes = false;
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (i == 0 || (text[i - 1] == '\n' && !inQuotes)) starts.Add(i);
                        if (text[i] == '"') inQuotes = !inQuotes;
                    }
                    for (int s = 1; s < starts.Count; s++)
                    {
                        int a0 = starts[s], b0 = s + 1 < starts.Count ? starts[s + 1] : text.Length;
                        string row = text.Substring(a0, b0 - a0);
                        string end = row.EndsWith("\n") ? "\n" : "";
                        string body = row.Substring(0, row.Length - end.Length);
                        int comma = body.IndexOf(',');
                        if (comma <= 0 || body.StartsWith("#")) continue;
                        string key = body.Substring(0, comma);
                        string before = text.Substring(0, a0), after = text.Substring(b0);
                        if (body.IndexOf('"') < 0)
                        {
                            string[] cells = body.Split(',');
                            if (cells.Length >= 3 && cells[1].Length >= 2)
                            {
                                foreach (string e in typed)
                                {
                                    string value = cells[1].Substring(0, cells[1].Length / 2) + e + cells[1].Substring(cells[1].Length / 2);
                                    string kind = $"{(e == "\n" ? "LF" : e == "\r" ? "CR" : e == "\t" ? "tab" : e)} typed into a text";
                                    Read(kind, before + key + "," + value + "," + string.Join(",", cells.Skip(2)) + end + after, key, value, false);
                                }
                            }
                            if (cells.Length >= 3 && cells[2].Length >= 2)
                            {
                                var copy = (string[])cells.Clone();
                                copy[2] = cells[2].Substring(0, cells[2].Length / 2) + "," + cells[2].Substring(cells[2].Length / 2);
                                Read("a comma typed into the english", before + string.Join(",", copy) + end + after, key, null, false);
                            }
                        }
                        int dot = key.IndexOf('.');
                        if (dot > 0) Read("a key's dot deleted", before + key.Remove(dot, 1) + body.Substring(comma) + end + after, key, null, false);
                        if (key.Length >= 2)
                        {
                            Read("a comma typed into a key", before + key.Insert(key.Length / 2, ",") + body.Substring(comma) + end + after, key, null, false);
                            Read("a space typed into a key", before + key.Insert(key.Length / 2, " ") + body.Substring(comma) + end + after, key, null, false);
                        }
                        int q1 = body.IndexOf('"');
                        if (q1 >= 0)
                        {
                            Read("a text's opening quote deleted", before + body.Remove(q1, 1) + end + after, key, null, false);
                            int q = q1 + 1;
                            while (q < body.Length && !(body[q] == '"' && (q + 1 >= body.Length || body[q + 1] != '"'))) q += body[q] == '"' ? 2 : 1;
                            if (q < body.Length) Read("a text's closing quote deleted", before + body.Remove(q, 1) + end + after, key, null, false);
                        }
                        string line = end.Length > 0 ? "" : "\n";
                        Read("a comment line without # put in after a row", before + row + line + "Checked up to here, the rest later, ok\n" + after, null, null, true);
                        Read("a comment line without # as wide as the header put in after a row", before + row + line + "Checked up to here, the rest later, ok, thanks\n" + after, null, null, true);
                    }
                }
            }
        }
        finally
        {
            foreach (string o in Texts.TestEnglish.Keys.Where(k => k.StartsWith("edits.")).ToList()) { Texts.TestEnglish.Remove(o); Texts.TestBundled.Remove(o); }
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        Console.WriteLine($"edited lines: {reads} reads of a shipped row with one edit (typed into its text or english, its key's dot or a quote deleted, a comment line put in after it), {fails} failing; {stops} stopped at a comment line without # (said: the reading stops at a line that is none of the mod's)");
        return fails;
    }

    // An edited text as the mod shows it when it reads it as written: the literal \n a line break (a space in a one-line
    // text), a tab a space, the ends trimmed.
    private static string EditedAsWritten(string value, string english)
    {
        string v = value.Replace("\\n", "\n").Replace('\t', ' ').Trim();
        return v.IndexOf('\n') >= 0 && english.IndexOf('\n') < 0 ? v.Replace('\n', ' ') : v;
    }

    // The game's own strings tables as a corpus (GK2_L10N_CORPUS: a folder of lng_<id>.tsv, key TAB text): texts with
    // literal \n, capitals, quotes and line breaks the shipped files haven't. Each language's table written as a correct
    // translation file (UTF-8, quoted as a spreadsheet quotes) and read back: every line shows its text as the mod cleans
    // it, or English when a check leaves it out (counted), never anything else; then the file garbled through every page a
    // translator's program may pass it: its text, English, or the file's own text as written.
    private static int Corpus()
    {
        string dir = Environment.GetEnvironmentVariable("GK2_L10N_CORPUS");
        if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "lng_en.tsv")))
        {
            Console.WriteLine("corpus: skipped (GK2_L10N_CORPUS names no folder with the game's lng_<id>.tsv)");
            return 0;
        }
        Encoding E(int cp) => Encoding.GetEncoding(cp, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?"));
        Dictionary<string, string> Table(string id)
        {
            var d = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(Path.Combine(dir, "lng_" + id + ".tsv"), Encoding.UTF8))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0 && tab + 1 < line.Length) d[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return d;
        }
        string Cell(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r', ';', '\t' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
        string Shown(string value, string english)
        {
            string v = TranslationFiles.Clean(value);
            string e = TranslationFiles.Clean(english);
            return v.IndexOf('\n') >= 0 && english.IndexOf('\n') < 0 ? v.Replace('\n', ' ') : v;
        }
        // a text as a reader writes it into the game: the literal \n a line break, a tab a space, the <nobr> tags out, the
        // spaces and invisible characters off its ends; a line break a space in a one-line text (Lookup); nothing else
        string Plain(string value, string english)
        {
            string v = value.Replace("\\n", "\n").Replace('\t', ' ').Replace("\r", "").Replace("<nobr>", "").Replace("</nobr>", "");
            v = v.Trim(' ', '\n', '\u00A0', '\uFEFF', '\u200B', '\u200C', '\u200D', '\u2060', '\u00AD', '\u3000', '\u2002', '\u2003', '\u2009');
            return v.IndexOf('\n') >= 0 && english.IndexOf('\n') < 0 ? v.Replace('\n', ' ') : v;
        }
        Dictionary<string, string> en = Table("en");
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationCorpus-" + Environment.ProcessId);
        int fails = 0, reads = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        const string owner = "corpus.game";
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            Texts.TestTranslators = new List<string>();
            Directory.CreateDirectory(tmp);
            Texts.TestBundled[owner] = tmp;
            foreach (string id in new[] { "de", "fr", "es", "pt-br", "ru", "pl", "tr", "ja", "zh_cn", "ko" })
            {
                if (!File.Exists(Path.Combine(dir, "lng_" + id + ".tsv"))) continue;
                Dictionary<string, string> table = Table(id);
                var english = new List<(string Key, string English, string What)>();
                var value = new Dictionary<string, string>();
                var csv = new StringBuilder("key,value,english\n");
                int n = 0;
                foreach (var kv in table)
                {
                    if (!en.TryGetValue(kv.Key, out string e) || e.Trim().Length == 0 || kv.Value.Trim().Length == 0) continue;
                    string key = "g.line" + (++n);
                    english.Add((key, e, null));
                    value[key] = kv.Value;
                    csv.Append(key).Append(',').Append(Cell(kv.Value)).Append(',').Append(Cell(e)).Append('\n');
                }
                Texts.TestEnglish[owner] = english;
                Texts.LanguageOverride = id;
                int windows = id switch { "pl" => 1250, "ru" => 1251, "tr" => 1254, "ja" => 932, "zh_cn" => 936, "ko" => 949, _ => 1252 };
                var ways = new List<(string Name, byte[] Bytes, Func<string, string> AsWritten)>
                {
                    ("correct (UTF-8)", Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), null),
                };
                foreach (int cp in new[] { windows, 1252, 1250, 1257, 1258, 10000, 28591, 28605 }.Distinct())
                {
                    int page = cp;
                    ways.Add(($"garbled through {page}", new UTF8Encoding(false).GetBytes(E(page).GetString(Encoding.UTF8.GetBytes(csv.ToString()))),
                        s => E(page).GetString(Encoding.UTF8.GetBytes(s))));
                }
                foreach (var (name, bytes, asWritten) in ways)
                {
                    File.WriteAllBytes(Path.Combine(tmp, id + ".csv"), bytes);
                    Texts.Forget();
                    Texts.TestWarnings = 0;
                    reads++;
                    int ok = 0, left = 0, wrong = 0, cleaned = 0;
                    string example = null, cleanedExample = null;
                    foreach (var x in english)
                    {
                        string got = Texts.Of(owner, x.Key, x.English);
                        string expected = Shown(value[x.Key], x.English);
                        if (asWritten == null && got == expected && got != Plain(value[x.Key], x.English))
                        {
                            // the mod's Clean changed the text beyond what a reader writes into it (the literal \n, the tags,
                            // the ends): a character changed silently (code review round 7: ｛ became {)
                            cleaned++;
                            cleanedExample ??= $"{x.Key} \"{got}\" for \"{Plain(value[x.Key], x.English)}\"";
                        }
                        if (got == expected) { ok++; continue; }
                        if (got == x.English) { left++; continue; }
                        if (asWritten != null && got == Shown(asWritten(value[x.Key]), x.English)) { ok++; continue; }
                        wrong++;
                        example ??= $"{x.Key} \"{(got.Length > 40 ? got.Substring(0, 40) + "..." : got)}\" for \"{(expected.Length > 40 ? expected.Substring(0, 40) + "..." : expected)}\"";
                    }
                    bool silent = asWritten == null && left > 0 && Texts.TestWarnings == 0; // a correct file's line left out unsaid
                    Console.WriteLine($"{(wrong == 0 && cleaned == 0 && !silent ? "PASS" : "FAIL")} corpus: {id}, {name}: {ok} as written, {left} left out (English){(silent ? " WITHOUT A WARNING" : "")}, {wrong} WRONG{(example != null ? " (" + example + ")" : "")}{(cleaned > 0 ? $", {cleaned} changed by the cleaning ({cleanedExample})" : "")}");
                    if (wrong > 0 || cleaned > 0 || silent) fails++;
                }
            }
        }
        finally
        {
            Texts.TestEnglish.Remove(owner);
            Texts.TestBundled.Remove(owner);
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        Console.WriteLine($"corpus: {reads} reads of the game's own tables as translation files, {fails} failing");
        return fails;
    }

    // A UTF-8 file with one line saved in a code page (a line edited in another program; code review round 5): each
    // shipped file, each line beyond ASCII in turn in the language's Windows, DOS and Mac page. Never a wrong text.
    private static int MixedFiles(string repo, List<string> files)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationMixed-" + Environment.ProcessId);
        int fails = 0, reads = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            Texts.TestTranslators = new List<string>();
            int folderNo = 0;
            foreach (string folder in files.Select(Path.GetDirectoryName).Distinct())
            {
                string en = Path.Combine(folder, "en.csv");
                if (!File.Exists(en)) continue;
                string owner = "mixed.folder" + (++folderNo);
                var english = new List<(string Key, string English, string What)>();
                foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) english.Add((r.Key, r.Value, null));
                Texts.TestEnglish[owner] = english;
                string dir = Path.Combine(tmp, owner);
                Directory.CreateDirectory(dir);
                Texts.TestBundled[owner] = dir;
                foreach (string f in files.Where(x => Path.GetDirectoryName(x) == folder))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == "en" || id == "ja" || id == "zh_cn" || id == "ko") continue;
                    string text = File.ReadAllText(f, new UTF8Encoding(false)).TrimStart('\uFEFF').Replace("\r\n", "\n");
                    string[] lines = text.Split('\n');
                    Texts.LanguageOverride = id;
                    File.WriteAllBytes(Path.Combine(dir, id + ".csv"), Encoding.UTF8.GetBytes(text));
                    Texts.Forget();
                    var right = new Dictionary<string, string>();
                    foreach (var x in english) right[x.Key] = Texts.Of(owner, x.Key, x.English);
                    List<Way> pages = WaysFor(id).Where(w => w.Bytes && !w.Name.Contains("ISO")).ToList();
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (lines[i].All(c => c < 0x80)) continue;
                        foreach (Way w in pages)
                        {
                            var bytes = new List<byte>(Encoding.UTF8.GetPreamble());
                            for (int k = 0; k < lines.Length; k++)
                            {
                                string l = lines[k] + (k + 1 < lines.Length ? "\n" : "");
                                bytes.AddRange(k == i ? w.Save(l) : Encoding.UTF8.GetBytes(l));
                            }
                            File.WriteAllBytes(Path.Combine(dir, id + ".csv"), bytes.ToArray());
                            Texts.Forget();
                            reads++;
                            foreach (var x in english)
                            {
                                string got = Texts.Of(owner, x.Key, x.English);
                                if (got == right[x.Key] || got == x.English) continue;
                                if (w.AsWritten != null && got == w.AsWritten(right[x.Key])) continue; // the line's own text, as written
                                fails++;
                                Console.WriteLine($"FAIL mixed: {Path.GetRelativePath(repo, f)} line {i + 1} in {w.Name}: {x.Key} \"{got}\" for \"{right[x.Key]}\"");
                                break;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (string o in Texts.TestEnglish.Keys.Where(k => k.StartsWith("mixed.")).ToList()) { Texts.TestEnglish.Remove(o); Texts.TestBundled.Remove(o); }
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        Console.WriteLine($"mixed files: {reads} reads of UTF-8 files with one line in a code page, {fails} failing");
        return fails;
    }

    // Small files (code review round 4: a short file can look plausible in a page it isn't in): every shipped line alone,
    // with its header, saved every code-page and garbled way, as a translator's override (Translations\<owner>\) over the
    // correct bundled file. Every key must show exactly its translation: the override's own when read right, the bundled
    // one when the override is refused; anything else is WRONG.
    private static int SmallFiles(string repo, List<string> files)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "GK2-TranslationSmall-" + Environment.ProcessId);
        int fails = 0, reads = 0, refused = 0, limit = 0;
        bool quiet = Texts.TestQuiet;
        bool? forced = Texts.TestForced;
        string languageWas = Texts.LanguageOverride;
        List<string> rootsWas = Texts.TestTranslators;
        var failing = new Dictionary<string, int>();
        try
        {
            Texts.TestQuiet = true;
            Texts.TestForced = false;
            int folderNo = 0;
            foreach (string folder in files.Select(Path.GetDirectoryName).Distinct())
            {
                string en = Path.Combine(folder, "en.csv");
                if (!File.Exists(en)) continue;
                string owner = "small.folder" + (++folderNo);
                var english = new List<(string Key, string English, string What)>();
                foreach (TranslationFiles.Row r in TranslationFiles.Read(en, "en").Rows) english.Add((r.Key, r.Value, null));
                Texts.TestEnglish[owner] = english;
                string bundled = Path.Combine(tmp, owner, "bundled"), translator = Path.Combine(tmp, owner, "translator");
                string theirs = Path.Combine(translator, "Translations", owner);
                Directory.CreateDirectory(bundled);
                Directory.CreateDirectory(theirs);
                Texts.TestBundled[owner] = bundled;
                Texts.TestTranslators = new List<string> { translator };
                foreach (string f in files.Where(x => Path.GetDirectoryName(x) == folder))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == "en") continue;
                    string text = File.ReadAllText(f, new UTF8Encoding(false)).TrimStart('\uFEFF').Replace("\r\n", "\n");
                    string[] lines = text.Split('\n');
                    Texts.LanguageOverride = id;
                    File.WriteAllBytes(Path.Combine(bundled, id + ".csv"), Encoding.UTF8.GetBytes(text));
                    string over = Path.Combine(theirs, id + ".csv");
                    if (File.Exists(over)) File.Delete(over);
                    Texts.Forget();
                    var right = new Dictionary<string, string>();
                    foreach (var x in english) right[x.Key] = Texts.Of(owner, x.Key, x.English);
                    List<Way> ways = WaysFor(id).Where(w => w.Bytes || w.Garbling).ToList();
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (lines[i].Trim().Length == 0) continue;
                        string small = lines[0] + "\n" + lines[i] + "\n";
                        foreach (Way w in ways)
                        {
                            File.WriteAllBytes(over, w.Save(small));
                            Texts.Forget();
                            reads++;
                            int wrong = 0;
                            string example = null;
                            foreach (var x in english)
                            {
                                string got = Texts.Of(owner, x.Key, x.English);
                                if (got == right[x.Key]) continue;
                                if (w.AsWritten != null && got == w.AsWritten(right[x.Key])) continue; // the override's own text, as written
                                wrong++;
                                example ??= $"{x.Key} \"{(got.Length > 30 ? got.Substring(0, 30) + "..." : got)}\" for \"{(right[x.Key].Length > 30 ? right[x.Key].Substring(0, 30) + "..." : right[x.Key])}\"";
                            }
                            if (wrong == 0) continue;
                            if (Limit(id, w.Name)) { limit++; continue; }
                            fails++;
                            string where = $"{Path.GetRelativePath(repo, f)} line {i + 1} alone, {w.Name}";
                            failing[w.Name + " / " + id] = failing.TryGetValue(w.Name + " / " + id, out int n) ? n + 1 : 1;
                            if (failing[w.Name + " / " + id] <= 3) Console.WriteLine($"FAIL small: {where}: {example}");
                        }
                    }
                    File.Delete(over);
                }
            }
        }
        finally
        {
            foreach (string o in Texts.TestEnglish.Keys.Where(k => k.StartsWith("small.")).ToList()) { Texts.TestEnglish.Remove(o); Texts.TestBundled.Remove(o); }
            Texts.TestQuiet = quiet;
            Texts.TestForced = forced;
            Texts.LanguageOverride = languageWas;
            Texts.TestTranslators = rootsWas;
            Texts.Forget();
            try { Directory.Delete(tmp, true); } catch { }
        }
        foreach (var kv in failing) Console.WriteLine($"FAIL small: {kv.Value} line(s) wrong: {kv.Key}");
        Console.WriteLine($"small files: {reads} reads of single shipped lines as a translator's override, {fails} failing, {limit} at a documented limit");
        return fails;
    }

    // The documented limits (docs/localization.md section 11, decisions the user's): Chinese, Japanese and Korean UTF-8
    // garbled by a re-save through GBK or Shift-JIS (no tables for those pages in the mod); Mac Cyrillic, Mac Central
    // European and Mac Turkish (the Mac pages the mod has no table for, when their letters read as plausible ones).
    private static bool Limit(string id, string way)
    {
        bool cjk = id == "ja" || id == "zh_cn" || id == "ko";
        if (cjk && (way.Contains("GBK") || way.Contains("Shift-JIS") || way.Contains("Korean (949)") || way.Contains("opened as 93") || way.Contains("opened as 949"))) return true;
        return false;
    }
}
