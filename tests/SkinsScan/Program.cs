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
var colorsCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Colors.cs"));
var mountRegions = StringsIn(colorsCs, "MountRegionKeys");
if (mountRegions.Count != 5) Fail($"Colors.cs MountRegionKeys: {mountRegions.Count} regions read (5 expected)"); else Pass("Colors.cs MountRegionKeys: 5 mount regions");
var tuningKeys = new HashSet<string>(Regex.Matches(pluginCs, @"cfg\.Bind\(section,\s*""(\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (knownKeys.Count != 44) Fail($"Looks.cs KnownKeys: {knownKeys.Count} keys read (44 expected)"); else Pass("Looks.cs KnownKeys: 44 frame keys");
if (skinTuningKeys.Count == 0) Fail("Looks.cs SkinTuningKeys not found"); else Pass($"Looks.cs SkinTuningKeys: {skinTuningKeys.Count} keys a skin may set");
if (tuningKeys.Count < 50) Fail($"Plugin.cs StyleSettings: only {tuningKeys.Count} tuning keys read"); else Pass($"Plugin.cs StyleSettings: {tuningKeys.Count} tuning keys");

var skinKeysAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "format", "id", "name", "author", "version", "base", "kind", "layer", "canvas", "pivot", "mount", "offset", "offset_cart", "sheet", "columns", "frames", "sheet_front", "source", "seat", "erase" };
// The [Mount] block's keys as Looks.cs keeps them, and the knobs of a [Base.<Region>] section (Colors.RegionTweak)
var mountKeys = StringsIn(looksCs, "MountKeys");
if (mountKeys.Count < 9) Fail($"Looks.cs MountKeys: {mountKeys.Count} keys read (9 expected)"); else Pass("Looks.cs MountKeys: the [Mount] block's keys");
var mountRequired = new[] { "id", "name", "author", "version", "cart", "foals" }; // the canonical block (docs/mounts-foundation-audit.md §4)
var baseKnobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hue", "Saturation", "Brightness", "Contrast", "Colorize", "ColorizeAmount", "Tint", "Overlay", "OverlayAmount" };
var effectWhen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Off", "Night", "Always" };
var effectsCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Effects.cs"));
var effectIds = new HashSet<string>(Regex.Matches(effectsCs, @"Id\s*=\s*""([\w.-]+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (!effectIds.Contains("hellfire")) Fail($"Effects.cs: the built-in effect ids could not be read ({effectIds.Count} found; hellfire expected)"); else Pass($"Effects.cs: {effectIds.Count} built-in effect id(s), hellfire among them");
var knownSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Skin", "Tuning", "Game", "Colors", "Mount", "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" };

// The section headers of a cfg in file order, for the canonical layout: [Skin] first, [Game] right after it, the
// colour sections before the mount block, the mount block last.
static List<string> SectionOrder(string path)
{
    var order = new List<string>();
    foreach (var raw in File.ReadLines(path))
    {
        var line = raw.Trim();
        if (line.StartsWith("[")) order.Add(line.Split('#')[0].Trim().TrimStart('[').TrimEnd(']').Trim());
    }
    return order;
}

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

// Every distinct opaque colour (RRGGBB, any alpha above zero) of the PNGs in a skin folder and its front/ folder: a
// small PNG reader of its own (8-bit RGB/RGBA, not interlaced: what the art tools write), so the scan needs no package.
static HashSet<string> PngColours(string dir)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    IEnumerable<string> files = Directory.GetFiles(dir, "*.png");
    string front = Path.Combine(dir, "front");
    if (Directory.Exists(front)) files = files.Concat(Directory.GetFiles(front, "*.png"));
    foreach (var png in files)
    {
        var bytes = File.ReadAllBytes(png);
        int w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        int h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        int depth = bytes[24], type = bytes[25], interlace = bytes[28];
        if (depth != 8 || (type != 6 && type != 2) || interlace != 0) throw new InvalidDataException($"{png}: only 8-bit RGB/RGBA non-interlaced PNGs are read here (depth {depth}, type {type}, interlace {interlace})");
        int bpp = type == 6 ? 4 : 3;
        var idat = new MemoryStream();
        int pos = 8;
        while (pos + 8 <= bytes.Length)
        {
            int len = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
            string chunk = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
            if (chunk == "IDAT") idat.Write(bytes, pos + 8, len);
            pos += 12 + len;
            if (chunk == "IEND") break;
        }
        idat.Position = 0;
        var raw = new byte[(w * bpp + 1) * h];
        using (var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length) { int n = z.Read(raw, read, raw.Length - read); if (n <= 0) break; read += n; }
        }
        int stride = w * bpp;
        var prev = new byte[stride];
        var cur = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int filter = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, cur, 0, stride);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                int add = filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                };
                cur[i] = (byte)(cur[i] + add);
            }
            for (int x = 0; x < w; x++)
            {
                int o = x * bpp;
                if (bpp == 3 || cur[o + 3] != 0) set.Add($"{cur[o]:X2}{cur[o + 1]:X2}{cur[o + 2]:X2}");
            }
            (prev, cur) = (cur, prev);
        }
    }
    return set;
}

static int Paeth(int a, int b, int c)
{
    int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
    return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
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
    // the values the loader reads by name (anything else silently means the default in the game)
    if (skin.TryGetValue("layer", out var layer) && layer != "replace" && layer != "over") Bad($"[Skin] layer = {layer}: replace or over");
    if (skin.TryGetValue("kind", out var kindv) && kindv != "own" && kindv != "palette") Bad($"[Skin] kind = {kindv}: own or palette");
    if (skin.TryGetValue("seat", out var seat) && seat != "picture" && !int.TryParse(seat, out _)) Bad($"[Skin] seat = {seat}: picture, or the canvas row (from the top) the rider's hip sits on");
    int seatRow = -1;
    bool seatRowGiven = skin.TryGetValue("seat", out var seatRowText) && int.TryParse(seatRowText, out seatRow);
    if (skin.TryGetValue("source", out var srcv) && srcv != "pictures" && srcv != "game") Bad($"[Skin] source = {srcv}: pictures or game");
    if (skin.TryGetValue("erase", out var erase))
    {
        if (erase != "none" && !Regex.IsMatch(erase, "^[0-9A-Fa-f]{6}$")) Bad($"[Skin] erase = {erase}: RRGGBB or none");
        if (!skin.TryGetValue("layer", out var l2) || l2 != "over") Bad("[Skin] erase is for layer = over only");
    }
    int cw = 96, ch = 96;
    if (skin.TryGetValue("canvas", out var canvas))
    {
        var parts = canvas.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out cw) || !int.TryParse(parts[1], out ch)) Bad($"canvas = {canvas}: two numbers expected");
        else if (cw < 8 || cw > 512 || ch < 8 || ch > 512) Bad($"canvas = {canvas}: 8 to 512");
    }
    if (seatRowGiven && (seatRow < 0 || seatRow >= ch)) Bad($"[Skin] seat = {seatRow}: a row of the {cw}x{ch} canvas (0 to {ch - 1}, from the top)");
    foreach (var sec in new[] { "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" })
        if (cfg.TryGetValue(sec, out var st) && st.ContainsKey("SeatOffset")) Bad($"[{sec}] SeatOffset: a mount's seat is the skin's seat row (seat = <row>), which holds for every player; SeatOffset only reaches players with AutoSeat off (docs/seat-investigation.md)");
    // the sections and their order (the canonical layout, tools/skins/canonical_cfg.py)
    var order = SectionOrder(cfgPath);
    foreach (var sec in order)
    {
        if (!knownSections.Contains(sec) && !sec.StartsWith("Base.", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}]: not a section the loader knows");
        if (sec.StartsWith("Base.", StringComparison.OrdinalIgnoreCase))
        {
            string region = sec.Substring(5);
            if (!mountRegions.Contains(region)) Bad($"[{sec}]: not a mount region ({string.Join(", ", mountRegions)})");
            foreach (var kv in cfg[sec]) if (!baseKnobs.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: not a colour knob ({string.Join(", ", baseKnobs)})");
        }
    }
    if (order.Count == 0 || !order[0].Equals("Skin", StringComparison.OrdinalIgnoreCase)) Bad("[Skin] must be the first section");
    int gameAt = order.FindIndex(s => s.Equals("Game", StringComparison.OrdinalIgnoreCase));
    if (gameAt > 1) Bad("[Game] stands right after [Skin]");
    int firstMount = order.FindIndex(s => s.StartsWith("Mount", StringComparison.OrdinalIgnoreCase));
    if (firstMount >= 0)
    {
        if (!order[firstMount].Equals("Mount", StringComparison.OrdinalIgnoreCase)) Bad($"[{order[firstMount]}] before [Mount]: the mount block starts with [Mount]");
        for (int i = firstMount; i < order.Count; i++) if (!order[i].StartsWith("Mount", StringComparison.OrdinalIgnoreCase)) Bad($"[{order[i]}] after the mount block: the mount block comes last");
    }
    else if (order.Any(s => s.StartsWith("Mount.", StringComparison.OrdinalIgnoreCase))) Bad("[Mount.Tuning] without a [Mount] section (ignored in the game)");
    if (order.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) Bad("a section appears twice");
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
                if (k.Equals("lift", StringComparison.OrdinalIgnoreCase))
                {
                    if (!double.TryParse(gameSec[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lift) || lift < 0 || lift > 1) Bad($"[Game] lift = {gameSec[k]}: 0 to 1");
                    continue;
                }
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
    // [Colors] (docs/mount-colours.md): regions from the mount vocabulary (Colors.cs MountRegionKeys, never Coat: it is
    // everything not named), every colour RRGGBB, in the pictures, and under one region only. A game-source skin has
    // no pictures here to check against.
    if (cfg.TryGetValue("Colors", out var colorsSec))
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? inPictures = game || sheet ? null : PngColours(dir);
        int named = 0;
        foreach (var kv in colorsSec)
        {
            if (!mountRegions.Contains(kv.Key)) { Bad($"[Colors] {kv.Key}: not a mount region ({string.Join(", ", mountRegions)})"); continue; }
            if (kv.Key.Equals("Coat", StringComparison.OrdinalIgnoreCase)) { Bad("[Colors] Coat: every colour not named is Coat; don't list it"); continue; }
            foreach (var tok in kv.Value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string hex = tok.TrimStart('#').ToUpperInvariant();
                if (!Regex.IsMatch(hex, "^[0-9A-F]{6}$")) { Bad($"[Colors] {kv.Key}: '{tok}' is not a colour (RRGGBB)"); continue; }
                if (seen.TryGetValue(hex, out var other) && !other.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) Bad($"[Colors] {hex} is under {other} and {kv.Key}; one region per colour");
                seen[hex] = kv.Key;
                if (inPictures != null && !inPictures.Contains(hex)) Bad($"[Colors] {kv.Key} {hex}: no picture of the skin has this colour (a stale list)");
                named++;
            }
        }
        if (ok) Pass($"{id}: [Colors] names {named} colour(s) under {colorsSec.Count} region(s)" + (inPictures != null ? $" of the pictures' {inPictures.Count}" : ""));
    }
    if (cfg.TryGetValue("Mount", out var m))
    {
        string mid = m.GetValueOrDefault("id") ?? "";
        if (mid != mount) Bad($"[Mount] id = {mid} but the skin's mount = {mount}");
        if (mid == "donkey") Bad("[Mount] id = donkey: the donkey is the game's own mount, a skin of it declares no [Mount]");
        foreach (var k in m.Keys) if (!mountKeys.Contains(k)) Bad($"[Mount] {k}: not a key the loader knows ({string.Join(", ", mountKeys)})");
        foreach (var req in mountRequired) if (string.IsNullOrWhiteSpace(m.GetValueOrDefault(req))) Bad($"[Mount] {req} missing (the canonical block always carries it)");
        foreach (var flag in new[] { "cart", "foals" })
        {
            if (m.TryGetValue(flag, out var v) && v != "true" && v != "false") Bad($"[Mount] {flag} = {v}: true or false");
        }
        if (m.TryGetValue("foal_scale", out var fsv))
        {
            if (!double.TryParse(fsv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fs) || fs < 0.2 || fs > 1) Bad($"[Mount] foal_scale = {fsv}: 0.2 to 1");
            else if (Math.Abs(fs - 0.6) < 0.001) Bad("[Mount] foal_scale = 0.6 is the default: leave it out");
        }
        if (m.TryGetValue("effect", out var ef))
        {
            if (ef == "none" || ef.Length == 0) Bad("[Mount] effect = none is the default: leave it out");
            else if (!effectIds.Contains(ef)) Bad($"[Mount] effect = {ef}: not a built-in effect ({string.Join(", ", effectIds)})");
            if (!m.ContainsKey("effect_when")) Bad("[Mount] effect without effect_when (Off, Night or Always)");
        }
        if (m.TryGetValue("effect_when", out var ew))
        {
            if (!effectWhen.Contains(ew)) Bad($"[Mount] effect_when = {ew}: Off, Night or Always (Never is the menu's word, not a value)");
            if (!m.ContainsKey("effect")) Bad("[Mount] effect_when without an effect");
        }
        // every tuning key in one place: a style section always wins over [Mount.Tuning], so a key in both is dead there
        if (cfg.TryGetValue("Mount.Tuning", out var both))
        {
            foreach (var sec in new[] { "Mount.Tuning.Ride", "Mount.Tuning.Cart" })
                if (cfg.TryGetValue(sec, out var st)) foreach (var key in both.Keys) if (st.ContainsKey(key)) Bad($"{key} set in [Mount.Tuning] and [{sec}]: the style section wins, the other value is dead; keep it in one place");
        }
        foreach (var sec in new[] { "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" })
        {
            if (!cfg.TryGetValue(sec, out var t)) continue;
            if (t.Count == 0) Bad($"[{sec}] is empty: leave it out");
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
