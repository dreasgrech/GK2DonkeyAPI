// The built-in skins (src/GK2DonkeyAPI/assets/skins/<id>/, embedded into the DLL): every skin.cfg must parse the way
// Looks.cs reads it, every picture the donkey base needs must be there at the canvas size, a mount declared by
// several skins must be declared the same way, a skin's [Tuning] may only hold the picture keys (a skin is the
// drawing, the mount the creature: docs/mounts-design.md §1), and a mount's tuning keys must exist. The frame keys
// and the allowed skin tuning keys are read from Looks.cs, the tuning keys from Plugin.cs, so this stays in step.
// Usage: dotnet run -c Release
using System.Text.RegularExpressions;

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
var skinsDir = Path.Combine(repo, "src", "GK2DonkeyAPI", "assets", "skins");
var looksCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Looks.cs"));
var pluginCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Plugin.cs"));
int fails = 0, passes = 0;
void Fail(string what) { Console.WriteLine("FAIL " + what); fails++; }
void Pass(string what) { Console.WriteLine("PASS " + what); passes++; }

// The lists in the sources: the donkey base's frame keys (KnownKeys) and the keys a skin's [Tuning] may set.
static HashSet<string> StringsIn(string source, string arrayName)
{
    var m = Regex.Match(source, arrayName + @"\s*=\s*(?:new\s+HashSet<string>\([^)]*\))?\s*\{(.*?)\};", RegexOptions.Singleline);
    if (!m.Success) return new HashSet<string>();
    return new HashSet<string>(Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
}
var knownKeys = StringsIn(looksCs, "KnownKeys");
var skinTuningKeys = StringsIn(looksCs, "SkinTuningKeys");
var tuningKeys = new HashSet<string>(Regex.Matches(pluginCs, @"cfg\.Bind\(section,\s*""(\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (knownKeys.Count != 44) Fail($"Looks.cs KnownKeys: {knownKeys.Count} keys read (44 expected)"); else Pass("Looks.cs KnownKeys: 44 frame keys");
if (skinTuningKeys.Count == 0) Fail("Looks.cs SkinTuningKeys not found"); else Pass($"Looks.cs SkinTuningKeys: {skinTuningKeys.Count} keys a skin may set");
if (tuningKeys.Count < 50) Fail($"Plugin.cs StyleSettings: only {tuningKeys.Count} tuning keys read"); else Pass($"Plugin.cs StyleSettings: {tuningKeys.Count} tuning keys");

var skinKeysAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "format", "id", "name", "author", "version", "base", "kind", "layer", "canvas", "pivot", "mount", "offset", "offset_cart", "sheet", "columns", "frames", "sheet_front", "source" };

static Dictionary<string, Dictionary<string, string>> ReadCfg(string path)
{
    var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string>? section = null;
    foreach (var raw in File.ReadLines(path))
    {
        var line = raw.Trim();
        int hash = line.IndexOf('#');
        if (hash >= 0) line = line[..hash].Trim();
        if (line.Length == 0) continue;
        if (line.StartsWith("[") && line.EndsWith("]"))
        {
            var name = line[1..^1].Trim();
            if (!result.TryGetValue(name, out section)) result[name] = section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            continue;
        }
        int eq = line.IndexOf('=');
        if (section == null || eq <= 0) continue;
        section[line[..eq].Trim()] = line[(eq + 1)..].Trim();
    }
    return result;
}

static (int W, int H) PngSize(string path)
{
    using var f = File.OpenRead(path);
    var b = new byte[24];
    if (f.Read(b, 0, 24) < 24 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return (0, 0);
    int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
    int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
    return (w, h);
}

static string SectionText(Dictionary<string, Dictionary<string, string>> cfg, string name)
    => cfg.TryGetValue(name, out var s) ? string.Join("\n", s.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key.ToLowerInvariant() + "=" + kv.Value)) : "";

var mountDecl = new Dictionary<string, (string Skin, string Text)>(StringComparer.OrdinalIgnoreCase);
int skins = 0, pictures = 0;
foreach (var dir in Directory.GetDirectories(skinsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
{
    string id = Path.GetFileName(dir);
    string cfgPath = Path.Combine(dir, "skin.cfg");
    if (!File.Exists(cfgPath)) { Fail($"{id}: no skin.cfg"); continue; }
    skins++;
    var cfg = ReadCfg(cfgPath);
    if (!cfg.TryGetValue("Skin", out var skin)) { Fail($"{id}: no [Skin] section"); continue; }
    bool ok = true;
    void Bad(string what) { Fail($"{id}: {what}"); ok = false; }
    if (!skin.TryGetValue("format", out var format) || format != "1") Bad("format = 1 missing");
    if (!skin.TryGetValue("id", out var sid) || sid != id) Bad($"id = {skin.GetValueOrDefault("id") ?? "(none)"} should be the folder name");
    if (string.IsNullOrWhiteSpace(skin.GetValueOrDefault("name"))) Bad("name missing");
    string mount = skin.GetValueOrDefault("mount") ?? "";
    if (mount.Length == 0) Bad("mount missing (a built-in names its mount)");
    if (skin.TryGetValue("base", out var b) && b != "donkey") Bad($"base = {b}: the donkey is the only base");
    foreach (var k in skin.Keys) if (!skinKeysAllowed.Contains(k)) Bad($"[Skin] {k}: not a key the loader knows (a skin has no '{k}')");
    int cw = 96, ch = 96;
    if (skin.TryGetValue("canvas", out var canvas))
    {
        var parts = canvas.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out cw) || !int.TryParse(parts[1], out ch)) Bad($"canvas = {canvas}: two numbers expected");
    }
    bool sheet = skin.ContainsKey("sheet");
    bool game = skin.TryGetValue("source", out var src) && src.Equals("game", StringComparison.OrdinalIgnoreCase);
    if (game)
    {
        // another game character's frames (docs/skins.md, source = game): no pictures of its own; [Game] says whose
        if (!cfg.TryGetValue("Game", out var gameSec)) Bad("source = game without a [Game] section");
        else
        {
            if (string.IsNullOrWhiteSpace(gameSec.GetValueOrDefault("prefix"))) Bad("[Game] prefix missing (the character's preset prefix, like 0901_)");
            if (!int.TryParse(gameSec.GetValueOrDefault("scale") ?? "1", out int scale) || scale < 1 || scale > 8) Bad($"[Game] scale = {gameSec.GetValueOrDefault("scale")}: a whole number from 1 to 8");
            foreach (var k in gameSec.Keys)
            {
                if (k.Equals("prefix", StringComparison.OrdinalIgnoreCase) || k.Equals("scale", StringComparison.OrdinalIgnoreCase)) continue;
                if (!knownKeys.Contains(k)) Bad($"[Game] {k}: not a frame key of the donkey base");
            }
        }
        if (Directory.GetFiles(dir, "*.png").Length > 0) Bad("source = game but PNGs are in the folder (they would be ignored)");
    }
    else if (!sheet)
    {
        int found = 0;
        foreach (var key in knownKeys)
        {
            string png = Path.Combine(dir, key + ".png");
            if (!File.Exists(png)) { Bad($"picture {key}.png missing"); continue; }
            found++;
            var (w, h) = PngSize(png);
            if (w != cw || h != ch) Bad($"{key}.png is {w}x{h}, the canvas is {cw}x{ch}");
        }
        pictures += found;
        foreach (var png in Directory.GetFiles(dir, "*.png"))
        {
            string key = Path.GetFileNameWithoutExtension(png);
            if (!knownKeys.Contains(key)) Bad($"{key}.png: not a frame key of the donkey base");
        }
        string frontDir = Path.Combine(dir, "front");
        if (Directory.Exists(frontDir))
        {
            int front = 0;
            foreach (var png in Directory.GetFiles(frontDir, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(png);
                if (!knownKeys.Contains(key)) { Bad($"front/{key}.png: not a frame key"); continue; }
                var (w, h) = PngSize(png);
                if (w != cw || h != ch) Bad($"front/{key}.png is {w}x{h}, the canvas is {cw}x{ch}");
                front++;
            }
            pictures += front;
        }
    }
    if (cfg.TryGetValue("Tuning", out var tuning))
    {
        foreach (var k in tuning.Keys) if (!skinTuningKeys.Contains(k)) Bad($"[Tuning] {k}: a skin may only set the picture keys ({string.Join(", ", skinTuningKeys)}); the mount sets the rest");
    }
    if (cfg.TryGetValue("Mount", out var m))
    {
        string mid = m.GetValueOrDefault("id") ?? "";
        if (mid != mount) Bad($"[Mount] id = {mid} but the skin's mount = {mount}");
        if (string.IsNullOrWhiteSpace(m.GetValueOrDefault("name"))) Bad("[Mount] name missing");
        foreach (var flag in new[] { "cart", "foals" })
        {
            if (m.TryGetValue(flag, out var v) && v != "true" && v != "false") Bad($"[Mount] {flag} = {v}: true or false");
        }
        foreach (var sec in new[] { "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" })
        {
            if (!cfg.TryGetValue(sec, out var t)) continue;
            foreach (var kv in t)
            {
                if (!tuningKeys.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: not a tuning key of [Ride]/[Cart] (docs/reference.md)");
                else if (!double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) && kv.Value != "true" && kv.Value != "false" && !Regex.IsMatch(kv.Value, @"^[A-Za-z]\w*$")) Bad($"[{sec}] {kv.Key} = {kv.Value}: not a number, true/false or a name");
            }
        }
        string text = string.Join("\n---\n", new[] { "Mount", "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" }.Select(s => SectionText(cfg, s)));
        if (mountDecl.TryGetValue(mid, out var first))
        {
            if (first.Text != text) Bad($"declares the mount '{mid}' differently from '{first.Skin}' (the first declaration wins in the game; keep them the same: the Mounts tab's Save writes every declaring file)");
        }
        else mountDecl[mid] = (id, text);
    }
    else if (mount != "donkey") Bad($"names the mount '{mount}' but declares no [Mount] section (a built-in declares its own)");
    if (ok) Pass($"{id}: skin.cfg, {(sheet ? "a sheet" : game ? "the game's own frames (source = game)" : "44 pictures")}{(Directory.Exists(Path.Combine(dir, "front")) ? " + a front layer" : "")}, mount '{mount}'");
}
Console.WriteLine($"INFO {skins} built-in skins, {mountDecl.Count} mounts declared ({string.Join(", ", mountDecl.Keys.OrderBy(k => k))}), {pictures} pictures");
Console.WriteLine(fails == 0 ? $"ALL PASS ({passes})" : $"FAILURES: {fails}");
return fails;
