using GK2DonkeyAPI;

// The translation reader without the game (docs/localization.md, "Translator mistakes"):
// 1. the translator mistake catalogue (TranslationTests.RunCatalogue, the same the in-game self-test runs): every case
//    must show what it should, never a wrong text, and tell the translator exactly when the case says so;
// 2. every shipped Languages/*.csv (src/*, packs/*) read as the game reads it: no warning allowed.
// Exit 1 on any failure. Run: dotnet run -c Release (from this folder).
internal static class Program
{
    private static int Main()
    {
        int fails = 0;
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
        foreach (string f in files)
        {
            var heard = new List<string>();
            TranslationFiles.TestHeard = heard;
            Texts.TestWarnings = 0;
            string id = Path.GetFileNameWithoutExtension(f);
            TranslationFiles.File read = TranslationFiles.Read(f, id);
            bool good = read.Usable && read.Malformed == 0 && heard.Count == 0 && read.How == null && read.Rows.Count > 0;
            Console.WriteLine($"{(good ? "PASS" : "FAIL")} {Path.GetRelativePath(repo, f)}: {read.Rows.Count} row(s){(read.How != null ? ", read as " + read.How : "")}{(heard.Count > 0 ? "; said: " + string.Join(" | ", heard) : "")}");
            if (!good) fails++;
        }
        Texts.TestQuiet = quiet;
        TranslationFiles.TestHeard = null;
        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails == 0 ? 0 : 1;
    }
}
