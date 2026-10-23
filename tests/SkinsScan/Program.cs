// The skins this repo ships (every src/*/assets/skins/<id>/: the API's own and Saddle Up's five mounts, each embedded into
// its DLL; docs/mounts-as-mods.md): every skin.cfg must parse the way
// Looks.cs reads it, every picture the donkey base needs must be there at the canvas size, a mount declared by
// several skins must be declared the same way, a skin's [Tuning] may only hold the picture keys (a skin is the
// drawing, the mount the creature: docs/mounts-design.md §1), and a mount's tuning keys must exist. The frame keys
// and the allowed skin tuning keys are read from Looks.cs, the tuning keys from Plugin.cs, so this stays in step.
// Usage: dotnet run -c Release
using System.Text.RegularExpressions;

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
var skinDirs = Directory.GetDirectories(Path.Combine(repo, "src")).Select(p => Path.Combine(p, "assets", "skins")).Where(Directory.Exists)
    .SelectMany(Directory.GetDirectories).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase).ToList();
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
var tuningKeys = new HashSet<string>(Regex.Matches(pluginCs, @"(?:cfg\.Bind\(section|\b[FBSIC]\(cfg, section),\s*""(\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase); // every bound key, the helper-bound ones (F/B/S) too
// the shuffle's keys (IdleShuffle.cs) are a mount's to pin as well (Looks.Rig.MountMayPin); the lantern's are not (the cart's own)
var shuffleCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "IdleShuffle.cs"));
var shuffleKeys = new HashSet<string>(Regex.Matches(shuffleCs, @"""(Shuffle\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (shuffleKeys.Count < 20) Fail($"IdleShuffle.cs: only {shuffleKeys.Count} shuffle keys read"); else Pass($"IdleShuffle.cs: {shuffleKeys.Count} shuffle keys a mount may pin");
int maxCanvas = int.TryParse(Regex.Match(looksCs, @"MaxCanvas\s*=\s*(\d+)").Groups[1].Value, out int mc) ? mc : 0;
if (maxCanvas <= 0) Fail("Looks.cs MaxCanvas not found"); else Pass($"Looks.cs MaxCanvas: {maxCanvas}");
if (knownKeys.Count != 44) Fail($"Looks.cs KnownKeys: {knownKeys.Count} keys read (44 expected)"); else Pass("Looks.cs KnownKeys: 44 frame keys");
if (skinTuningKeys.Count == 0) Fail("Looks.cs SkinTuningKeys not found"); else Pass($"Looks.cs SkinTuningKeys: {skinTuningKeys.Count} keys a skin may set");
if (tuningKeys.Count < 50) Fail($"Plugin.cs StyleSettings: only {tuningKeys.Count} tuning keys read"); else Pass($"Plugin.cs StyleSettings: {tuningKeys.Count} tuning keys");

var skinKeysAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "format", "needs", "id", "name", "description", "portrait", "author", "version", "base", "colours", "layer", "canvas", "pivot", "mount", "offset", "offset_cart", "sheet", "columns", "frames", "sheet_front", "source", "seat", "erase", "effect", "effect_when" };
// The [Mount] block's keys as Looks.cs keeps them, and the knobs of a [Base.<Region>] section (Colors.RegionTweak)
var mountKeys = StringsIn(looksCs, "MountKeys");
if (mountKeys.Count < 16) Fail($"Looks.cs MountKeys: {mountKeys.Count} keys read (16 expected)"); else Pass("Looks.cs MountKeys: the [Mount] block's keys");
var mountRequired = new[] { "id", "name", "author", "version", "cart", "foals" }; // the canonical block (docs/mounts-foundation-audit.md §4)
var baseKnobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hue", "Saturation", "Brightness", "Contrast", "Colorize", "ColorizeAmount", "Tint", "Overlay", "OverlayAmount" };
var effectWhen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Off", "Night", "Always" };
var effectsCs = File.ReadAllText(Path.Combine(repo, "src", "SaddleUp", "Plugin.cs")); // the shipped effects are Saddle Up's (RegisterHellfire)
var effectIds = new HashSet<string>(Regex.Matches(effectsCs, @"Id\s*=\s*""([\w.-]+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (!effectIds.Contains("hellfire")) Fail($"SaddleUp/Plugin.cs: the shipped effect ids could not be read ({effectIds.Count} found; hellfire expected)"); else Pass($"SaddleUp/Plugin.cs: {effectIds.Count} shipped effect id(s), hellfire among them");
var knownSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Skin", "Tuning", "Tuning.Ride", "Tuning.Cart", "Game", "Colors", "Mount", "Mount.Stats", "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" };
var skinTuningSections = new[] { "Tuning", "Tuning.Ride", "Tuning.Cart" };

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

// The flying experiment's frames (docs/flying.md "the frames"): not the game's, played by the mod while a mount flies.

static bool FlightKey(string key) => System.Text.RegularExpressions.Regex.IsMatch(key, @"^bdy_(fly|flyup[12]|flydown[12]|glide|glidein|glideout[1-4]|takeoff|land)_left_\d\d$");


static (int W, int H) PngSize(string path)
{
    using var f = File.OpenRead(path);
    var b = new byte[24];
    if (f.Read(b, 0, 24) < 24 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return (0, 0);
    int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
    int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
    return (w, h);
}

// Every distinct opaque colour (RRGGBB, any alpha above zero) of the PNGs in a skin folder and its front/ folder.
static HashSet<string> PngColours(string dir)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    IEnumerable<string> files = Directory.GetFiles(dir, "*.png");
    string front = Path.Combine(dir, "front");
    if (Directory.Exists(front)) files = files.Concat(Directory.GetFiles(front, "*.png"));
    foreach (var png in files)
    {
        var (w, h, bpp, px) = PngPixels(png);
        for (int i = 0; i < w * h; i++)
        {
            int o = i * bpp;
            if (bpp == 3 || px[o + 3] != 0) set.Add($"{px[o]:X2}{px[o + 1]:X2}{px[o + 2]:X2}");
        }
    }
    return set;
}

// A PNG's pixels, row by row from the top, bpp bytes each (RGB or RGBA): a small PNG reader of its own (8-bit
// RGB/RGBA, not interlaced: what the art tools write), so the scan needs no package.
static (int W, int H, int Bpp, byte[] Px) PngPixels(string png)
{
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
        var outPx = new byte[stride * h];
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
            Array.Copy(cur, 0, outPx, y * stride, stride);
            (prev, cur) = (cur, prev);
        }
        return (w, h, bpp, outPx);
    }
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
foreach (var dir in skinDirs)
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
    if (skin.TryGetValue("portrait", out var portrait) && !File.Exists(Path.Combine(dir, portrait))) Bad($"[Skin] portrait = {portrait}: no such file in the pack");
    if (!skin.ContainsKey("description")) Bad("[Skin] has no description (the Mounts panel shows it; a line or two)");
    if (cfg.TryGetValue("Mount", out var mountBlock))
    {
        if (mountBlock.TryGetValue("portrait", out var mportrait) && !File.Exists(Path.Combine(dir, mportrait))) Bad($"[Mount] portrait = {mportrait}: no such file in the pack");
        if (!mountBlock.ContainsKey("description")) Bad("[Mount] has no description (the Mounts panel shows it)");
    }
    // the values the loader reads by name (anything else silently means the default in the game)
    if (skin.TryGetValue("layer", out var layer) && layer != "replace" && layer != "over") Bad($"[Skin] layer = {layer}: replace or over");
    if (skin.TryGetValue("colours", out var coloursv) && coloursv != "own" && coloursv != "palette") Bad($"[Skin] colours = {coloursv}: own or palette");
    if (skin.TryGetValue("seat", out var seat) && seat != "picture" && !int.TryParse(seat, out _)) Bad($"[Skin] seat = {seat}: picture, or the canvas row (from the top) the rider's hip sits on");
    int seatRow = -1;
    bool seatRowGiven = skin.TryGetValue("seat", out var seatRowText) && int.TryParse(seatRowText, out seatRow);
    if (skin.TryGetValue("source", out var srcv) && srcv != "pictures" && srcv != "game") Bad($"[Skin] source = {srcv}: pictures or game");
    // the effect a skin comes with (shown under "As the mount or skin"): the mount block's rules
    if (skin.TryGetValue("effect", out var skinEffect))
    {
        if (skinEffect == "none" || skinEffect.Length == 0) Bad("[Skin] effect = none is the default: leave it out");
        else if (!effectIds.Contains(skinEffect)) Bad($"[Skin] effect = {skinEffect}: not a built-in effect ({string.Join(", ", effectIds)})");
        if (!skin.ContainsKey("effect_when")) Bad("[Skin] effect without effect_when (Off, Night or Always)");
    }
    if (skin.TryGetValue("effect_when", out var skinWhen))
    {
        if (!effectWhen.Contains(skinWhen)) Bad($"[Skin] effect_when = {skinWhen}: Off, Night or Always");
        if (!skin.ContainsKey("effect")) Bad("[Skin] effect_when without an effect");
    }
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
        else if (cw < 8 || cw > maxCanvas || ch < 8 || ch > maxCanvas) Bad($"canvas = {canvas}: 8 to {maxCanvas} (Looks.MaxCanvas: the loader refuses more)");
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
    int firstColour = order.FindIndex(s => s.Equals("Colors", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Base.", StringComparison.OrdinalIgnoreCase));
    foreach (var sec in skinTuningSections)
    {
        int at = order.FindIndex(s => s.Equals(sec, StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && firstColour >= 0 && at > firstColour) Bad($"[{sec}] after the colour sections: the skin's tuning sections stand before them");
        if (at >= 0 && gameAt >= 0 && at < gameAt) Bad($"[{sec}] before [Game]: [Game] stands right after [Skin]");
    }
    {
        // among themselves: [Tuning], then [Tuning.Ride], then [Tuning.Cart] (the canonical writer's order)
        int last = -1;
        foreach (var sec in skinTuningSections)
        {
            int at = order.FindIndex(s => s.Equals(sec, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            if (at < last) Bad($"[{sec}] stands before an earlier skin tuning section: the order is [Tuning], [Tuning.Ride], [Tuning.Cart]");
            last = at;
        }
    }
    int firstMount = order.FindIndex(s => s.StartsWith("Mount", StringComparison.OrdinalIgnoreCase));
    if (firstMount >= 0)
    {
        if (!order[firstMount].Equals("Mount", StringComparison.OrdinalIgnoreCase)) Bad($"[{order[firstMount]}] before [Mount]: the mount block starts with [Mount]");
        for (int i = firstMount; i < order.Count; i++) if (!order[i].StartsWith("Mount", StringComparison.OrdinalIgnoreCase)) Bad($"[{order[i]}] after the mount block: the mount block comes last");
    }
    else if (order.Any(s => s.StartsWith("Mount.", StringComparison.OrdinalIgnoreCase))) Bad("a [Mount.*] section without a [Mount] section (ignored in the game)");
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
                else if (!Regex.IsMatch(gameSec[k], @"^(bdy|hed)_\w+$") && !gameSec[k].Equals("none", StringComparison.OrdinalIgnoreCase)) Bad($"[Game] {k} = {gameSec[k]}: the game's frame names start with bdy_ or hed_ (the prefix is added), or none for a cell left empty");
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
            if (!knownKeys.Contains(key) && !FlightKey(key)) Bad($"{key}.png: not a frame key of the donkey base (nor a flying one)");
        }
        string frontDir = Path.Combine(dir, "front");
        if (Directory.Exists(frontDir))
        {
            int front = 0;
            foreach (var png in Directory.GetFiles(frontDir, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(png);
                if (!knownKeys.Contains(key) && !FlightKey(key)) { Bad($"front/{key}.png: not a frame key"); continue; }
                var (w, h) = PngSize(png);
                if (w != cw || h != ch) Bad($"front/{key}.png is {w}x{h}, the canvas is {cw}x{ch}");
                front++;
            }
            pictures += front;
        }
        // the seat row sits on the body: the standing frame is drawn there at the pivot column (the rider's hip on air
        // would be a row typed wrong; every built-in creature has its body there, docs/seat-investigation.md)
        string standing = Path.Combine(dir, "bdy_static_left.png");
        if (seatRowGiven && File.Exists(standing))
        {
            int pivotX = cw / 2;
            if (skin.TryGetValue("pivot", out var pv)) { var pp = pv.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (pp.Length == 2 && int.TryParse(pp[0], out int px0)) pivotX = px0; }
            try
            {
                var (w, h, bpp, px) = PngPixels(standing);
                if (bpp != 4) Bad("bdy_static_left.png has no alpha channel: a skin's pictures are RGBA (transparent around the drawing)");
                else if (w == cw && h == ch && seatRow < h && pivotX < w && px[(seatRow * w + pivotX) * bpp + 3] == 0)
                    Bad($"[Skin] seat = {seatRow}: nothing is drawn on that row at the pivot column ({pivotX}) of bdy_static_left.png; the rider's hip sits on the body");
            }
            catch (InvalidDataException e) { Bad(e.Message); }
        }
    }
    else
    {
        // a sheet of cells (docs/skins.md): a whole number of cells of the canvas size, the front sheet too
        foreach (var (key, label) in new[] { ("sheet", "sheet"), ("sheet_front", "sheet_front") })
        {
            if (!skin.TryGetValue(key, out var file)) continue;
            string png = Path.Combine(dir, file);
            if (!File.Exists(png)) { Bad($"[Skin] {label} = {file}: no such file"); continue; }
            var (w, h) = PngSize(png);
            if (w == 0 || w % cw != 0 || h % ch != 0) Bad($"[Skin] {label} = {file}: {w}x{h} is not a whole number of {cw}x{ch} cells");
            else pictures += (w / cw) * (h / ch);
        }
    }
    bool over = skin.TryGetValue("layer", out var layerValue) && layerValue.Equals("over", StringComparison.OrdinalIgnoreCase);
    HashSet<string>? inPictures = null;
    if (!game && !sheet)
    {
        try { inPictures = PngColours(dir); }
        catch (InvalidDataException e) { Bad(e.Message + " (an indexed or 16-bit PNG: export RGBA)"); }
    }
    // the erase colour is an over skin's: a replace skin drawing it would show magenta in the game
    if (!over && inPictures != null)
    {
        string eraseHex = (skin.GetValueOrDefault("erase") ?? "FF00FF").ToUpperInvariant();
        if (eraseHex != "NONE" && inPictures.Contains(eraseHex)) Bad($"pixels of the erase colour {eraseHex} in a replace skin: only a skin over the donkey erases; they would be drawn as magenta");
    }
    // the skin's own geometry: [Tuning] (both styles), [Tuning.Ride], [Tuning.Cart]; the picture keys only, each in one place
    foreach (var sec in skinTuningSections)
    {
        if (!cfg.TryGetValue(sec, out var tuning)) continue;
        if (tuning.Count == 0) Bad($"[{sec}] is empty: leave it out");
        foreach (var kv in tuning)
        {
            if (!skinTuningKeys.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: a skin may only set the picture keys ({string.Join(", ", skinTuningKeys)}); the mount sets the rest");
            else if (!double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) && kv.Value != "true" && kv.Value != "false") Bad($"[{sec}] {kv.Key} = {kv.Value}: a number or true/false");
            if (kv.Key.Equals("SeatOffset", StringComparison.OrdinalIgnoreCase) && seatRowGiven) Bad($"[{sec}] SeatOffset with a seat row: the row is the seat, SeatOffset is dead on this skin");
        }
    }
    if (cfg.TryGetValue("Tuning", out var bothStyles))
    {
        foreach (var sec in new[] { "Tuning.Ride", "Tuning.Cart" })
            if (cfg.TryGetValue(sec, out var st)) foreach (var key in bothStyles.Keys) if (st.ContainsKey(key)) Bad($"{key} set in [Tuning] and [{sec}]: the style section wins, the other value is dead; keep it in one place");
    }
    // [Colors] (docs/mount-colours.md): regions from the mount vocabulary (Colors.cs MountRegionKeys, never Coat: it is
    // everything not named), every colour RRGGBB, in the pictures, and under one region only. A game-source skin has
    // no pictures here to check against.
    if (cfg.TryGetValue("Colors", out var colorsSec))
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        // which colour path the game takes for it (Colors.cs PaletteMaxColours = 48): the exact swap, or the colour cube,
        // where two colours in one cell change together (anti-aliased edges and the front layer count too)
        if (inPictures != null) Console.WriteLine($"INFO {id}: {inPictures.Count} colour(s) in the pictures: {(inPictures.Count > 48 ? "over 48, the colour cube (a few near-identical colours may change together; fewer colours = the exact swap)" : "the exact swap")}");
    }
    if (cfg.TryGetValue("Mount", out var m))
    {
        string mid = m.GetValueOrDefault("id") ?? "";
        if (mid != mount) Bad($"[Mount] id = {mid} but the skin's mount = {mount}");
        if (mid == "donkey") Bad("[Mount] id = donkey: the donkey is the game's own mount, a skin of it declares no [Mount]");
        foreach (var k in m.Keys) if (!mountKeys.Contains(k)) Bad($"[Mount] {k}: not a key the loader knows ({string.Join(", ", mountKeys)})");
        foreach (var req in mountRequired) if (string.IsNullOrWhiteSpace(m.GetValueOrDefault(req))) Bad($"[Mount] {req} missing (the canonical block always carries it)");
        foreach (var flag in new[] { "cart", "foals", "flies", "jumps" })
        {
            if (m.TryGetValue(flag, out var v) && v != "true" && v != "false") Bad($"[Mount] {flag} = {v}: true or false");
        }
        // flies and jumps are written only when not the default (the loader's SaveCfg does the same)
        if (m.GetValueOrDefault("flies") == "false") Bad("[Mount] flies = false is the default: leave it out");
        if (m.GetValueOrDefault("jumps") == "true") Bad("[Mount] jumps = true is the default: leave it out");
        if (m.TryGetValue("foal_scale", out var fsv))
        {
            if (!double.TryParse(fsv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fs) || fs < 0.2 || fs > 1) Bad($"[Mount] foal_scale = {fsv}: 0.2 to 1");
            else if (Math.Abs(fs - 0.6) < 0.001) Bad("[Mount] foal_scale = 0.6 is the default: leave it out");
        }
        // the speed (times the keeper's walking speed; with the cart times the donkey's ratio): every built-in mount says
        // its own, 0.5 to 4; the cargo (items its cart carries) 1 to 6 on a mount that pulls one, said by every built-in;
        // never a SpeedMultiplier in its tuning or a [Mount.Stats] Speed / Cargo line as well
        if (!m.TryGetValue("speed", out var sv)) Bad("[Mount] speed missing: a built-in mount says its own speed (times walking speed; the donkey's is 2)");
        else if (!double.TryParse(sv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var spd) || spd < 0.5 || spd > 4) Bad($"[Mount] speed = {sv}: a number, 0.5 to 4 (times walking speed)");
        if (m.TryGetValue("cargo", out var cg))
        {
            if (!int.TryParse(cg, out var cgn) || cgn < 1 || cgn > 6) Bad($"[Mount] cargo = {cg}: a whole number, 1 to 6");
            if (m.GetValueOrDefault("cart") == "false") Bad("[Mount] cargo on a mount that can't pull the cart");
        }
        else if (m.GetValueOrDefault("cart") != "false") Bad("[Mount] cargo missing: a built-in mount that pulls the cart says how much it carries (1 to 6)");
        foreach (var sec in new[] { "Mount.Tuning", "Mount.Tuning.Ride", "Mount.Tuning.Cart" })
            if (cfg.TryGetValue(sec, out var st) && st.ContainsKey("SpeedMultiplier")) Bad($"[{sec}] SpeedMultiplier: the mount's speed is [Mount] speed (the cart's follows it)");
        if (cfg.TryGetValue("Mount.Stats", out var stl)) foreach (var key in stl.Keys) if (key.Equals("Speed", StringComparison.OrdinalIgnoreCase) || key.Equals("Cargo", StringComparison.OrdinalIgnoreCase)) Bad($"[Mount.Stats] {key}: the API writes the speed and cargo lines itself (left out in the game)");
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
                if (skinTuningKeys.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: a drawing's key; the skin's [Tuning] (or [Tuning.Ride] / [Tuning.Cart]) is its home, the mount keeps what the creature is (docs/mounts-parity-matrix.md 9.1)");
                if (kv.Key.StartsWith("Lantern", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}] {kv.Key}: the lantern's keys are the cart's own, a mount may not pin them (refused at the merge)");
                else if (!tuningKeys.Contains(kv.Key) && !shuffleKeys.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: not a tuning key of [Ride]/[Cart] or of the shuffle (docs/reference.md)");
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
Console.WriteLine($"INFO {skins} shipped skins (the API's and Saddle Up's), {mountDecl.Count} mounts declared ({string.Join(", ", mountDecl.Keys.OrderBy(k => k))}), {pictures} pictures");
Console.WriteLine(fails == 0 ? $"ALL PASS ({passes})" : $"FAILURES: {fails}");
return fails;
