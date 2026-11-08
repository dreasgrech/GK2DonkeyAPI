// The mounts and skins this repo ships, sorted by mount (every src/*/assets/mounts/<mount>/: its mount.cfg and sounds\,
// and skins/<skin>/ with each skin.cfg; Saddle Up's Larry, and every packs/<Pack>/gk2mounts/ (the Dragon, the Critters,
// the Donkey Skins), each embedded into its DLL;
// docs/skins.md "What a pack is", docs/mounts-as-mods.md): every mount.cfg and skin.cfg must parse the way the loader
// reads them (MountPacks.cs, Looks.cs), every picture the donkey base needs must be there at the canvas size, a
// skin's [Tuning] may only hold the picture keys (a skin is the drawing, the mount the creature: docs/mounts-design.md
// §1), and a mount's tuning keys must exist. The names (2026-10-24): a mount's folder is its id, "<mod>.<name>"
// (saddleup.dragon; only the donkey's is plain), its own look is skins\default\ (no default_skin; the donkey has none),
// and a skin is named by its folder, its full name "<mount id>/<skin>" (no id = in skin.cfg). A skin added to a mount
// another mod owns (a mount folder without mount.cfg, so every donkey skin) is named "<mod>.<name>"
// (donkey/saddleup-donkey-skins.hellfire; Looks.Register refuses it otherwise); on its own mount any name. The frame keys
// and the allowed skin tuning keys are read from Looks.cs, the tuning keys from Plugin.cs, so this stays in step.
// A skin of a mount other than the donkey that isn't its "default" is built on that default (docs/skins.md "Variants:
// built on the default", LooksInherit.cs): it is read against ../default, whose pictures, canvas, description and
// sounds it takes where it gives none.
// Usage: dotnet run -c Release
using System.Text.RegularExpressions;

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
// two kinds (2026-10-26, tools/skins/skinroots.py): built in, src/<project>/assets/mounts/<mount>/ (embedded in its DLL);
// external, packs/<Pack>/gk2mounts/<mount>/ (each pack its own download, laid out as BepInEx/plugins/<Pack>/)
var packsDir = Path.Combine(repo, "packs");
var mountRoots = Directory.GetDirectories(Path.Combine(repo, "src")).Select(p => Path.Combine(p, "assets", "mounts"))
    .Concat(Directory.Exists(packsDir) ? Directory.GetDirectories(packsDir).Select(p => Path.Combine(p, "gk2mounts")) : Enumerable.Empty<string>())
    .Where(Directory.Exists).ToList();
var mountDirs = mountRoots.SelectMany(Directory.GetDirectories).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase).ToList();
// the pack a mount folder is in (null: built in)
string? PackOf(string dir)
{
    string rel = Path.GetRelativePath(packsDir, dir);
    return rel.StartsWith("..") || Path.IsPathRooted(rel) ? null : rel.Split(Path.DirectorySeparatorChar)[0];
}
// a pack's ids carry its prefix: its plugin GUID's part after dreas.gk2. with the dots out, dashes free (the GUID
// dreas.gk2.saddleup.dragon: saddleup-dragon.dragon; the folder and DLL names are the user's, GK2SaddleUpDragonsMountPack
// since 2026-10-28, and say nothing about the ids)
string PackKey(string pack)
{
    string pc = Path.Combine(packsDir, pack, "Plugin.cs");
    Match g = File.Exists(pc) ? Regex.Match(File.ReadAllText(pc), @"Guid\s*=\s*""dreas\.gk2\.([\w.]+)""") : Match.Empty;
    return g.Success ? g.Groups[1].Value.Replace(".", "") : pack.ToLowerInvariant();
}
bool PrefixOk(string pack, string id) => id.IndexOf('.') > 0 && id.Substring(0, id.IndexOf('.')).Replace("-", "").Equals(PackKey(pack), StringComparison.OrdinalIgnoreCase);
// a pack's layout: its failures count with the rest (they were printed and not counted until 2026-11-03, so the packs'
// Languages\ folders of 2026-11-01 printed three FAIL lines under an ALL PASS)
int packFails = 0;
void PackFail(string what) { Console.WriteLine("FAIL pack " + what); packFails++; }
if (Directory.Exists(packsDir))
{
    foreach (var pack in Directory.GetDirectories(packsDir))
    {
        string pn = Path.GetFileName(pack);
        if (!Directory.Exists(Path.Combine(pack, "gk2mounts"))) PackFail($"{pn}: no gk2mounts\\ folder (a pack is laid out as BepInEx\\plugins\\{pn}\\gk2mounts\\<mount>\\)");
        foreach (var doc in new[] { "README.md", "CHANGELOG.md", pn + ".csproj", "Plugin.cs" }) if (!File.Exists(Path.Combine(pack, doc))) PackFail($"{pn}: no {doc} (each pack is its own download: a small plugin with its folder embedded)");
        foreach (var stray in Directory.GetDirectories(pack).Select(Path.GetFileName).Where(d => d != "gk2mounts" && d != "Languages" && d != "bin" && d != "obj")) PackFail($"{pn}: {stray}\\ (a pack holds gk2mounts\\, Languages\\ (its translations), its project, README.md and CHANGELOG.md)");
        // the plugin's version is the CHANGELOG's first heading (the zip's name and the log carry it)
        string pc = Path.Combine(pack, "Plugin.cs"), cl = Path.Combine(pack, "CHANGELOG.md");
        if (File.Exists(pc) && File.Exists(cl))
        {
            var pv = Regex.Match(File.ReadAllText(pc), "public const string Version = \"([0-9.]+)\"");
            var cv = Regex.Match(File.ReadAllText(cl), @"(?m)^## (?:Unreleased \()?([0-9]+\.[0-9]+(?:\.[0-9]+)?)");
            if (!pv.Success || !cv.Success || pv.Groups[1].Value != cv.Groups[1].Value) PackFail($"{pn}: Plugin.cs Version {(pv.Success ? pv.Groups[1].Value : "?")} and CHANGELOG.md's first heading {(cv.Success ? cv.Groups[1].Value : "?")} differ");
            var csproj = Path.Combine(pack, pn + ".csproj");
            if (File.Exists(csproj) && !File.ReadAllText(csproj).Contains("LogicalName=\"" + pn + ".mounts.%(RecursiveDir)")) PackFail($"{pn}: its csproj embeds gk2mounts\\** as {pn}.mounts.%(RecursiveDir)... (the prefix Plugin.cs hands to RegisterEmbedded)");
        }
    }
}
var skinDirs = mountDirs.Select(m => Path.Combine(m, "skins")).Where(Directory.Exists).SelectMany(Directory.GetDirectories)
    .OrderBy(d => Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(d))) + "/" + Path.GetFileName(d), StringComparer.OrdinalIgnoreCase).ToList(); // by full name, <mount>/<skin>
foreach (var stray in Directory.GetDirectories(Path.Combine(repo, "src")).Select(p => Path.Combine(p, "assets", "skins")).Where(Directory.Exists))
    Console.WriteLine($"FAIL {stray}: the flat skins folder is gone (2026-10-24): a skin sits in assets/mounts/<mount>/skins/<skin>/");
var looksCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Looks.cs"));
var pluginCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Plugin.cs"));
int fails = packFails, passes = 0;
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
if (mountRegions.Count != 6) Fail($"Colors.cs MountRegionKeys: {mountRegions.Count} regions read (6 expected)"); else Pass("Colors.cs MountRegionKeys: 6 mount regions");
// the exact swap's limit (Colors.PaletteMaxColours): the colour cube over it
var paletteMaxMatch = Regex.Match(colorsCs, @"PaletteMaxColours = (\d+);");
int paletteMax = paletteMaxMatch.Success ? int.Parse(paletteMaxMatch.Groups[1].Value) : 0;
if (paletteMax == 0) Fail("Colors.cs PaletteMaxColours not read");
// a skin over the donkey's (Colors.PaletteMaxColoursOver): its count is its pairs, the donkey's colours and its own named ones
var paletteMaxOverMatch = Regex.Match(colorsCs, @"PaletteMaxColoursOver = (\d+);");
int paletteMaxOver = paletteMaxOverMatch.Success ? int.Parse(paletteMaxOverMatch.Groups[1].Value) : 0;
if (paletteMaxOver == 0) Fail("Colors.cs PaletteMaxColoursOver not read");
// the donkey's own colours by region (Colors.cs Regions, the mount's sprites): under an over skin they are the look's too
var donkeyColours = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (Match m in Regex.Matches(colorsCs, @"R\(""(Coat|Dark|Muzzle|Eye|Harness|Outline)"", [^)]*?DonkeySprites, ""([0-9a-fA-F ]+)"""))
    foreach (var hex in m.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)) donkeyColours[hex.ToUpperInvariant()] = m.Groups[1].Value;
if (donkeyColours.Count == 0) Fail("Colors.cs Regions: the donkey's own colours not read"); else Pass($"Colors.cs Regions: the donkey's {donkeyColours.Count} own colours");
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

var skinKeysAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "format", "needs", "name", "description", "portrait", "author", "version", "base", "colours", "layer", "canvas", "pivot", "mount", "offset", "offset_cart", "sheet", "columns", "frames", "sheet_front", "source", "seat", "erase" };
// The [Mount] block's keys as MountPacks.cs keeps them, and the knobs of a [Base.<Region>] section (Colors.RegionTweak)
var mountPacksCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "MountPacks.cs"));
var mountKeys = StringsIn(mountPacksCs, "MountKeys");
if (mountKeys.Count < 14) Fail($"MountPacks.cs MountKeys: {mountKeys.Count} keys read (14 expected)"); else Pass("MountPacks.cs MountKeys: the [Mount] block's keys");
if (mountKeys.Contains("effect") || mountKeys.Contains("effect_when")) Fail("MountPacks.cs MountKeys still has effect / effect_when (nothing picks an effect since 2026-10-25)");
if (mountKeys.Contains("default_skin")) Fail("MountPacks.cs MountKeys still has default_skin (a mount's own look is its skins\\default\\ since 2026-10-24)");
// The speeds a [Mount] speed may name: the words of the API's one table (Catalog.cs SpeedBands), never a number (2026-10-27)
var catalogCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "Catalog.cs"));
var speedTable = Regex.Match(catalogCs, @"SpeedBands\s*=\s*\{(.*?)\};", RegexOptions.Singleline);
var speedWords = new HashSet<string>(Regex.Matches(speedTable.Groups[1].Value, @"\(SpeedBand\.\w+,\s*""([^""]+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (speedWords.Count != 5) Fail($"Catalog.cs SpeedBands: {speedWords.Count} speed words read (5 expected)"); else Pass("Catalog.cs SpeedBands: " + string.Join(", ", speedWords));
var mountRequired = new[] { "id", "name", "author", "version", "cart", "foals" }; // the canonical block (docs/mounts-foundation-audit.md §4)
var baseKnobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hue", "Saturation", "Brightness", "Contrast", "Colorize", "ColorizeAmount", "Tint", "Overlay", "OverlayAmount" };
// The shipped effects: every `new MountEffectDef { Id = "..." }` in a mod's or a pack's Plugin.cs (the Donkey Skins pack's
// Hellfire since 2026-10-27). Each id carries a mod prefix; a pack's, the pack's own (as its skins and mounts).
var effectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (string pc in Directory.GetFiles(Path.Combine(repo, "src"), "Plugin.cs", SearchOption.AllDirectories).Concat(Directory.Exists(Path.Combine(repo, "packs")) ? Directory.GetFiles(Path.Combine(repo, "packs"), "Plugin.cs", SearchOption.AllDirectories) : Array.Empty<string>()))
{
    if (pc.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || pc.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
    string rel = Path.GetRelativePath(repo, pc).Replace('\\', '/');
    string? pack = rel.StartsWith("packs/") ? rel.Split('/')[1] : null;
    foreach (Match fm in Regex.Matches(File.ReadAllText(pc), @"new\s+MountEffectDef\s*\{\s*Id\s*=\s*""([\w.-]+)"""))
    {
        string fx = fm.Groups[1].Value;
        effectIds.Add(fx);
        int dot = fx.IndexOf('.');
        if (dot <= 0 || dot == fx.Length - 1) Fail($"{rel}: effect id '{fx}' has no mod prefix (<mod>.<name>; Effects.Register refuses it)");
        else if (pack != null && !PrefixOk(pack, fx)) Fail($"{rel}: effect id '{fx}' doesn't carry the pack's prefix ({PackKey(pack)}, its GUID's part after dreas.gk2., dashes free)");
        else Pass($"{rel}: shipped effect {fx}");
    }
}
if (effectIds.Count == 0) Fail("no shipped effect found in any Plugin.cs (Hellfire's is the Donkey Skins pack's)");
var knownSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Skin", "Tuning", "Tuning.Ride", "Tuning.Cart", "Sounds", "Sounds.Ride", "Sounds.Cart", "Sounds.Baby", "Game", "Colors" };
var mountSections = new[] { "Mount", "Stats", "Tuning", "Tuning.Ride", "Tuning.Cart", "Jump", "Sounds", "Sounds.Ride", "Sounds.Cart", "Sounds.Baby" }; // mount.cfg, in its canonical order
// mount.cfg [Jump] (JumpShapes.Keys in the API): each key's range, in the file's order
var jumpKeys = new (string Key, double Min, double Max)[] { ("height", 1, 120), ("tap_height", 1, 120), ("hold_min", 0, 0.3), ("time", 0.15, 2), ("rise_share", 0.2, 0.8), ("hang", 0, 0.5), ("crouch", 0, 12), ("crouch_time", 0, 0.3), ("squash", 0, 12), ("fall_ease", 0, 1), ("wings", 0, 1) }; // wings: true or false
var skinTuningSections = new[] { "Tuning", "Tuning.Ride", "Tuning.Cart" };
var skinOwnSections = new[] { "Tuning", "Tuning.Ride", "Tuning.Cart", "Sounds", "Sounds.Ride", "Sounds.Cart", "Sounds.Baby" }; // the skin's own sections, in the canonical order, before the colour sections
// The sound catalogue (docs/mount-sounds-design.md): the events, their numbers and variants (SoundEvents.cs), the line attributes (SoundBlock.cs)
var soundEventsCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "SoundEvents.cs"));
var soundBlockCs = File.ReadAllText(Path.Combine(repo, "src", "GK2DonkeyAPI", "SoundBlock.cs"));
var soundEvents = new HashSet<string>(Regex.Matches(soundEventsCs, @"Id = ""(\w+)""").Select(m => m.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
var soundNumbers = new HashSet<string>(Regex.Matches(soundEventsCs, @"Numbers = new\[\] \{([^}]*)\}").SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""(\w+)""").Select(x => x.Groups[1].Value)), StringComparer.OrdinalIgnoreCase);
var soundVariants = new HashSet<string>(Regex.Matches(Regex.Match(soundEventsCs, @"Surfaces = \{([^}]*)\}").Groups[1].Value, @"""(\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
var soundAttrs = new HashSet<string>(Regex.Matches(Regex.Match(soundBlockCs, @"Attributes = \{([^}]*)\}").Groups[1].Value, @"""(\w+)""").Select(x => x.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (soundEvents.Count < 10 || soundAttrs.Count < 5 || soundVariants.Count != 4) Fail($"SoundEvents.cs / SoundBlock.cs: the sound catalogue could not be read ({soundEvents.Count} events, {soundAttrs.Count} attributes, {soundVariants.Count} variants)"); else Pass($"SoundEvents.cs: {soundEvents.Count} sound events, {soundNumbers.Count} numbers, {soundVariants.Count} variants; SoundBlock.cs: {soundAttrs.Count} line attributes");
var soundSections = new[] { "Sounds", "Sounds.Ride", "Sounds.Cart", "Sounds.Baby" }; // a skin.cfg's and a mount.cfg's alike
// the donkey's own events (DonkeyOwn = true in SoundEvents.cs): silent on another mount unless its pack gives them a sound
var donkeyOwn = new HashSet<string>(Regex.Matches(soundEventsCs, @"Id = ""(\w+)""(?:(?!Id = "")[\s\S])*?DonkeyOwn = true").Select(m => m.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
if (donkeyOwn.Count < 4) Fail($"SoundEvents.cs: the donkey's own events could not be read ({donkeyOwn.Count})"); else Pass($"SoundEvents.cs: {donkeyOwn.Count} of the donkey's own events ({string.Join(", ", donkeyOwn.OrderBy(e => e))})");

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

// The runtime's rules for what another mod's file may hold (docs/multi-mod-loading.md section 2): no number that is
// NaN or Infinity, no file name outside its folder (rooted, a network share, "..").
static IEnumerable<string> HostileValues(Dictionary<string, Dictionary<string, string>> cfg)
{
    foreach (var sec in cfg)
    {
        foreach (var kv in sec.Value)
        {
            bool words = kv.Key.Equals("name", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("description", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("author", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("noun", StringComparison.OrdinalIgnoreCase);
            if (!words) foreach (string tok in kv.Value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = tok.TrimStart('+', '-');
                if (t.Equals("NaN", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Infinity", StringComparison.OrdinalIgnoreCase) || t == "\u221E")
                    yield return $"[{sec.Key}] {kv.Key} = {kv.Value}: NaN and Infinity are not numbers";
            }
            if (kv.Key.Equals("portrait", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("sheet", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("sheet_front", StringComparison.OrdinalIgnoreCase))
            {
                string v = kv.Value.Replace('/', '\\');
                if (Path.IsPathRooted(v) || v.Contains(':') || v.Split('\\').Contains(".."))
                    yield return $"[{sec.Key}] {kv.Key} = {kv.Value}: a file of the pack's own folder (the runtime refuses a name outside it)";
            }
        }
    }
}

static Dictionary<string, Dictionary<string, string>> ReadCfg(string path)
{
    var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string>? section = null;
    // a colour list given twice is read as the loader reads it, both lines' colours: any key of [Colors], a tone's
    // colours; a tone's name and follows keep their last line, as the loader does
    bool regions = false, tone = false;
    foreach (var raw in File.ReadLines(path))
    {
        var line = raw.Trim();
        int hash = line.IndexOf('#');
        if (hash >= 0) line = line[..hash].Trim();
        if (line.Length == 0) continue;
        if (line.StartsWith("[") && line.EndsWith("]"))
        {
            var name = line[1..^1].Trim();
            regions = name.Equals("Colors", StringComparison.OrdinalIgnoreCase);
            tone = name.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Region.", StringComparison.OrdinalIgnoreCase); // a list key: colours
            if (!result.TryGetValue(name, out section)) result[name] = section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            continue;
        }
        int eq = line.IndexOf('=');
        if (section == null || eq <= 0) continue;
        string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
        if (tone && key.Equals("colors", StringComparison.OrdinalIgnoreCase)) key = "colours"; // a tone's two spellings are one list
        bool list = regions || (tone && key.Equals("colours", StringComparison.OrdinalIgnoreCase));
        section[key] = list && section.TryGetValue(key, out var before) ? before + " " + value : value;
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

// A colour's cell in the game's colour cube (Colors.Rig.LutIndex): red and green rounded to 32 steps, blue floored.
static int LutCell(string hex)
{
    float r = Convert.ToInt32(hex[..2], 16), g = Convert.ToInt32(hex[2..4], 16), b = Convert.ToInt32(hex[4..6], 16);
    int ri = Math.Clamp((int)MathF.Floor(r / 255f * 31f + 0.5f), 0, 31), gi = Math.Clamp((int)MathF.Floor(g / 255f * 31f + 0.5f), 0, 31), bi = Math.Clamp((int)MathF.Floor(b / 255f * 31f), 0, 31);
    return gi * 1024 + bi * 32 + ri;
}

// The colours one step above hex in one channel (the exact swap's reach: Colors.cs, the palette shader's 0.004).
static IEnumerable<string> StepNeighbours(string hex)
{
    int v = Convert.ToInt32(hex, 16);
    for (int shift = 16; shift >= 0; shift -= 8)
        if (((v >> shift) & 0xFF) < 0xFF) yield return (v + (1 << shift)).ToString("X6");
}

// Every colour a skin draws: its sheet and front sheet (`sheet =`, `sheet_front =`), or its PNGs (PngColours).
static HashSet<string> SkinColours(string dir, Dictionary<string, string>? skinSection)
{
    if (skinSection == null || !skinSection.TryGetValue("sheet", out var sheetFile)) return PngColours(dir);
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var f in new[] { sheetFile, skinSection.GetValueOrDefault("sheet_front") })
    {
        if (string.IsNullOrEmpty(f) || !File.Exists(Path.Combine(dir, f))) continue;
        var (w, h, bpp, px) = PngPixels(Path.Combine(dir, f));
        for (int i = 0; i < w * h; i++)
        {
            int o = i * bpp;
            if (bpp == 3 || px[o + 3] != 0) set.Add($"{px[o]:X2}{px[o + 1]:X2}{px[o + 2]:X2}");
        }
    }
    return set;
}

// Every distinct opaque colour (RRGGBB, any alpha above zero) of the PNGs in a skin folder and its front/ folder.
static HashSet<string> PngColours(string dir)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var png in PngFiles(dir)) AddPngColours(set, png);
    return set;
}

// A skin folder's pictures: its PNGs and its front/ folder's.
static IEnumerable<string> PngFiles(string dir)
{
    IEnumerable<string> files = Directory.GetFiles(dir, "*.png");
    string front = Path.Combine(dir, "front");
    if (Directory.Exists(front)) files = files.Concat(Directory.GetFiles(front, "*.png"));
    return files;
}

static void AddPngColours(HashSet<string> set, string png)
{
    var (w, h, bpp, px) = PngPixels(png);
    for (int i = 0; i < w * h; i++)
    {
        int o = i * bpp;
        if (bpp == 3 || px[o + 3] != 0) set.Add($"{px[o]:X2}{px[o + 1]:X2}{px[o + 2]:X2}");
    }
}

// The colours a variant's look takes from its default (Looks.VariantProduce): drawn over it (layer = over), every
// picture of the default's, under its own; else only the default's pictures it has none of its own for (a picture of
// its own replaces the default's whole). A sheet on either side: every colour of the default's (cells aren't matched here).
static HashSet<string> InheritedColours(string dir, string defaultDir, Dictionary<string, string>? skin, Dictionary<string, string>? defaultSkin, bool over)
{
    if (over || (skin?.ContainsKey("sheet") ?? false) || (defaultSkin?.ContainsKey("sheet") ?? false)) return SkinColours(defaultDir, defaultSkin);
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var png in PngFiles(defaultDir))
        if (!File.Exists(Path.Combine(dir, Path.GetRelativePath(defaultDir, png)))) AddPngColours(set, png);
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

// The sound lines of a skin's or a mount's cfg (docs/mount-sounds.md "A pack's sounds"): every line an event, an
// event.variant, or an event's attribute or number; every file named is in the sounds\ folder next to that cfg; every
// file there is named by a line or by the name rule.
void CheckSounds(Dictionary<string, Dictionary<string, string>> cfg, string dir, Action<string> Bad)
{
    foreach (var sec in soundSections)
    {
        if (!cfg.TryGetValue(sec, out var snd)) continue;
        if (snd.Count == 0) Bad($"[{sec}] is empty: leave it out");
        foreach (var kv in snd)
        {
            var parts = kv.Key.Split('.');
            if (!soundEvents.Contains(parts[0])) { Bad($"[{sec}] {kv.Key}: '{parts[0]}' is no sound event ({string.Join(", ", soundEvents.OrderBy(e => e))})"); continue; }
            bool variant = parts.Length >= 2 && soundVariants.Contains(parts[1]);
            string? attr = parts.Length == 1 ? null : parts.Length == 2 ? (variant ? null : parts[1]) : parts.Length == 3 && variant ? parts[2] : "?";
            if (attr == "?") { Bad($"[{sec}] {kv.Key}: not an event, event.variant or event.attribute"); continue; }
            if (attr != null && !soundAttrs.Contains(attr) && !soundNumbers.Contains(attr)) Bad($"[{sec}] {kv.Key}: '{attr}' is neither a line attribute ({string.Join(", ", soundAttrs)}) nor a number of a sound event ({string.Join(", ", soundNumbers.OrderBy(n => n))})");
            if (attr == null)
                foreach (var tok in kv.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()))
                    if (Regex.IsMatch(tok, @"\.(ogg|wav|mp3)$", RegexOptions.IgnoreCase) && !File.Exists(Path.Combine(dir, "sounds", tok))) Bad($"[{sec}] {kv.Key}: no sounds\\{tok} next to this cfg");
        }
    }
    if (!Directory.Exists(Path.Combine(dir, "sounds"))) return;
    foreach (var sub in Directory.GetDirectories(Path.Combine(dir, "sounds"))) Bad($"sounds\\{Path.GetFileName(sub)}\\: a folder in sounds\\ is never read (a mount's files are in the mount folder's own sounds\\)");
    foreach (var f in Directory.GetFiles(Path.Combine(dir, "sounds")))
    {
        string fileName = Path.GetFileName(f);
        if (!Regex.IsMatch(fileName, @"\.(ogg|wav)$", RegexOptions.IgnoreCase)) { Bad($"sounds\\{fileName}: not an .ogg or .wav file"); continue; }
        string stem = Path.GetFileNameWithoutExtension(f);
        int us = stem.LastIndexOf('_');
        if (us > 0 && int.TryParse(stem[(us + 1)..], out _)) stem = stem[..us];
        var sp = stem.Split('.');
        bool named = soundSections.Any(sec => cfg.TryGetValue(sec, out var s2) && s2.Values.Any(v => v.Split(new[] { ',', ';' }).Select(t => t.Trim()).Contains(fileName, StringComparer.OrdinalIgnoreCase)));
        bool convention = soundEvents.Contains(sp[0]) && (sp.Length == 1 || sp.Length == 2 && soundVariants.Contains(sp[1]));
        if (!named && !convention) Bad($"sounds\\{fileName}: named like no event (<event>[.<variant>][_<n>].ogg) and lined up in no [Sounds*] section: it is never played");
    }
}

// The mounts: a mount.cfg per folder (the donkey's holds skins only)
var mountCfgs = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
foreach (var mdir in mountDirs)
{
    string mid = Path.GetFileName(mdir);
    string mcfgPath = Path.Combine(mdir, "mount.cfg");
    bool mok = true;
    void BadM(string what) { Fail($"mount {mid}: {what}"); mok = false; }
    string? inPack = PackOf(mdir);
    if (inPack != null)
    {
        // an external pack (the user, 2026-10-26: "ids need to be depending on pack"): its mounts carry its prefix, a
        // skin it adds to another's mount too; its mount.cfg says the API it needs and its version
        if (File.Exists(mcfgPath) && !PrefixOk(inPack, mid)) BadM($"in the pack {inPack}: its id carries the pack's prefix ({PackKey(inPack)}.<name>, its GUID's part after dreas.gk2., dashes where wanted: saddleup-dragon.dragon)");
        if (!File.Exists(mcfgPath))
            foreach (var sk in Directory.Exists(Path.Combine(mdir, "skins")) ? Directory.GetDirectories(Path.Combine(mdir, "skins")).Select(Path.GetFileName) : Enumerable.Empty<string?>())
                if (!PrefixOk(inPack, sk!)) BadM($"skins\\{sk}\\ in the pack {inPack}: a skin it adds to another's mount carries the pack's prefix");
        if (File.Exists(mcfgPath))
        {
            var pm = ReadCfg(mcfgPath).GetValueOrDefault("Mount");
            if (pm == null || string.IsNullOrWhiteSpace(pm.GetValueOrDefault("needs"))) BadM($"in the pack {inPack}: [Mount] needs = <the API version> (an older API refuses the pack instead of loading it half)");
        }
    }
    else if (mid.StartsWith("saddleup-", StringComparison.OrdinalIgnoreCase)) BadM("a pack's id among the built-in mounts: an external mount sits in packs\\<Pack>\\gk2mounts\\");
    var skinsHere = Directory.Exists(Path.Combine(mdir, "skins")) ? Directory.GetDirectories(Path.Combine(mdir, "skins")).Select(Path.GetFileName).ToList() : new List<string?>();
    if (skinsHere.Count == 0) BadM("no skins\\<skin>\\ folder: a mount with nothing to wear is withdrawn at the start");
    // a folder without mount.cfg adds its skins to a mount another mod owns (the donkey is the game's): each is named
    // "<mod>.<name>", a dot neither first nor last (Looks.AddsToOthers; the loader leaves the others out)
    if (!File.Exists(mcfgPath))
        foreach (var s in skinsHere.Where(s => s!.IndexOf('.') <= 0 || s.IndexOf('.') == s.Length - 1))
            if (!s!.Equals("default", StringComparison.OrdinalIgnoreCase)) BadM($"skins\\{s}\\: a skin added to a mount another mod owns is named '<mod>.<name>' ({mid}/yourmod.{s}); the loader leaves it out");
    foreach (var stray in Directory.GetFiles(mdir).Select(Path.GetFileName).Where(f => !f!.Equals("mount.cfg", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        BadM($"{stray}: a mount folder holds mount.cfg, sounds\\, skins\\ and its portrait PNG");
    foreach (var sub in Directory.GetDirectories(mdir).Select(Path.GetFileName).Where(d => d != "skins" && d != "sounds")) BadM($"{sub}\\: a mount folder holds sounds\\ and skins\\ only");
    if (mid.Equals("donkey", StringComparison.OrdinalIgnoreCase))
    {
        if (File.Exists(mcfgPath)) BadM("mount.cfg in donkey\\: the donkey is the game's own mount, its folder holds skins only");
        if (Directory.Exists(Path.Combine(mdir, "sounds"))) BadM("sounds\\ in donkey\\: the donkey's sounds are the game's (a skin's own go in its folder)");
        if (skinsHere.Contains("default", StringComparer.OrdinalIgnoreCase)) BadM("skins\\default\\ in donkey\\: the donkey's own look is the game's art; a donkey skin needs a name of its own (the loader refuses it)");
        if (mok) Pass($"mount donkey: the game's own, {skinsHere.Count} skin(s) of it here, each named <mod>.<name> ({string.Join(", ", skinsHere)})");
        continue;
    }
    if (!File.Exists(mcfgPath)) { BadM("no mount.cfg (a mount folder of this repo declares its mount)"); continue; }
    var cfg = ReadCfg(mcfgPath);
    mountCfgs[mid] = cfg;
    foreach (string h in HostileValues(cfg)) BadM(h);
    var order = SectionOrder(mcfgPath);
    foreach (var sec in order) if (!mountSections.Contains(sec, StringComparer.OrdinalIgnoreCase)) BadM($"[{sec}]: not a section of mount.cfg ({string.Join(", ", mountSections)})");
    {
        int last = -1;
        foreach (var sec in mountSections)
        {
            int at = order.FindIndex(x => x.Equals(sec, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            if (at < last) BadM($"[{sec}] out of order: {string.Join(", ", mountSections)}");
            last = at;
        }
    }
    if (order.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) BadM("a section appears twice");
    if (!cfg.TryGetValue("Mount", out var m)) { BadM("no [Mount] section"); continue; }
    if ((m.GetValueOrDefault("id") ?? "") != mid) BadM($"[Mount] id = {m.GetValueOrDefault("id")}: the folder's name ({mid})");
    {
        // a mount's id is "<mod>.<name>" (Catalog.CheckId; only the game's donkey is plain): the folder is named so too
        int dot = mid.IndexOf('.');
        if (dot <= 0 || dot == mid.Length - 1) BadM($"the folder (the mount's id) '{mid}' is not '<mod>.<name>' (Saddle Up's dragon is saddleup.dragon; the loader refuses a plain id)");
    }
    foreach (var k in m.Keys) if (!mountKeys.Contains(k) && !k.Equals("default_skin", StringComparison.OrdinalIgnoreCase)) BadM($"[Mount] {k}: not a key the loader knows ({string.Join(", ", mountKeys)})");
    foreach (var req in mountRequired) if (string.IsNullOrWhiteSpace(m.GetValueOrDefault(req))) BadM($"[Mount] {req} missing (the canonical block always carries it)");
    if (!m.ContainsKey("description")) BadM("[Mount] has no description (the Mounts panel shows it)");
    if (m.TryGetValue("portrait", out var mportrait) && !File.Exists(Path.Combine(mdir, mportrait))) BadM($"[Mount] portrait = {mportrait}: no such file in the mount folder");
    if (m.ContainsKey("default_skin")) BadM("[Mount] default_skin: gone (the loader ignores it); a mount's own look is its skins\\default\\ folder");
    if (!File.Exists(Path.Combine(mdir, "skins", "default", "skin.cfg"))) BadM("no skins\\default\\skin.cfg: a mount's own look (the one a new one wears) is its skins\\default\\; the loader refuses the mount without it");
    foreach (var flag in new[] { "cart", "foals", "flies", "jumps" })
        if (m.TryGetValue(flag, out var v) && v != "true" && v != "false") BadM($"[Mount] {flag} = {v}: true or false");
    if (m.GetValueOrDefault("flies") == "false") BadM("[Mount] flies = false is the default: leave it out");
    if (m.GetValueOrDefault("jumps") == "true") BadM("[Mount] jumps = true is the default: leave it out");
    if (m.TryGetValue("foal_scale", out var fsv))
    {
        if (!double.TryParse(fsv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fs) || fs < 0.2 || fs > 1) BadM($"[Mount] foal_scale = {fsv}: 0.2 to 1");
        else if (Math.Abs(fs - 0.6) < 0.001) BadM("[Mount] foal_scale = 0.6 is the default: leave it out");
    }
    if (!m.TryGetValue("speed", out var sv)) BadM("[Mount] speed missing: a built-in mount names its speed (" + string.Join(", ", speedWords) + "; the donkey is Normal)");
    else if (!speedWords.Contains(sv.Trim())) BadM($"[Mount] speed = {sv}: one of the API's speeds, by its word ({string.Join(", ", speedWords)})");
    if (m.TryGetValue("cargo", out var cg))
    {
        if (!int.TryParse(cg, out var cgn) || cgn < 1 || cgn > 6) BadM($"[Mount] cargo = {cg}: a whole number, 1 to 6");
        if (m.GetValueOrDefault("cart") == "false") BadM("[Mount] cargo on a mount that can't pull the cart");
    }
    else if (m.GetValueOrDefault("cart") != "false") BadM("[Mount] cargo missing: a built-in mount that pulls the cart says how much it carries (1 to 6)");
    if (cfg.TryGetValue("Stats", out var stl)) foreach (var key in stl.Keys) if (key.Equals("Speed", StringComparison.OrdinalIgnoreCase) || key.Equals("Cargo", StringComparison.OrdinalIgnoreCase)) BadM($"[Stats] {key}: the API writes the speed and cargo lines itself (left out in the game)");
    if (cfg.TryGetValue("Tuning", out var both))
        foreach (var sec in new[] { "Tuning.Ride", "Tuning.Cart" })
            if (cfg.TryGetValue(sec, out var st)) foreach (var key in both.Keys) if (st.ContainsKey(key)) BadM($"{key} set in [Tuning] and [{sec}]: the style section wins, the other value is dead; keep it in one place");
    foreach (var sec in new[] { "Tuning", "Tuning.Ride", "Tuning.Cart" })
    {
        if (!cfg.TryGetValue(sec, out var t)) continue;
        if (t.Count == 0) BadM($"[{sec}] is empty: leave it out");
        foreach (var kv in t)
        {
            if (kv.Key.Equals("SpeedMultiplier", StringComparison.OrdinalIgnoreCase)) BadM($"[{sec}] SpeedMultiplier: the mount's speed is [Mount] speed (the cart's follows it)");
            if (kv.Key.Equals("SeatOffset", StringComparison.OrdinalIgnoreCase)) BadM($"[{sec}] SeatOffset: a mount's seat is its skins' seat row (seat = <row>), which holds for every player; SeatOffset only reaches players with AutoSeat off (docs/seat-investigation.md)");
            if (skinTuningKeys.Contains(kv.Key)) BadM($"[{sec}] {kv.Key}: a drawing's key; a skin's [Tuning] (or [Tuning.Ride] / [Tuning.Cart]) is its home, the mount keeps what the creature is (docs/mounts-parity-matrix.md 9.1)");
            if (kv.Key.StartsWith("Lantern", StringComparison.OrdinalIgnoreCase)) BadM($"[{sec}] {kv.Key}: the lantern's keys are the cart's own, a mount may not pin them (refused at the merge)");
            else if (!tuningKeys.Contains(kv.Key) && !shuffleKeys.Contains(kv.Key)) BadM($"[{sec}] {kv.Key}: not a tuning key of [Ride]/[Cart] or of the shuffle (docs/reference.md)");
            else if (!double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) && kv.Value != "true" && kv.Value != "false" && !Regex.IsMatch(kv.Value, @"^[A-Za-z]\w*$")) BadM($"[{sec}] {kv.Key} = {kv.Value}: not a number, true/false or a name");
        }
    }
    if (cfg.TryGetValue("Jump", out var jump))
    {
        if (jump.Count == 0) BadM("[Jump] is empty: leave it out");
        int lastAt = -1;
        foreach (var kv in jump)
        {
            int at = Array.FindIndex(jumpKeys, j => j.Key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
            if (at < 0) { BadM($"[Jump] {kv.Key}: not a jump key ({string.Join(", ", Array.ConvertAll(jumpKeys, j => j.Key))})"); continue; }
            if (at < lastAt) BadM($"[Jump] {kv.Key} stands after a later key: the order is {string.Join(", ", Array.ConvertAll(jumpKeys, j => j.Key))}");
            lastAt = Math.Max(lastAt, at);
            if (jumpKeys[at].Key == "wings") { if (kv.Value != "true" && kv.Value != "false") BadM($"[Jump] wings = {kv.Value}: true or false"); continue; }
            if (!double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var jv) || jv < jumpKeys[at].Min || jv > jumpKeys[at].Max)
                BadM($"[Jump] {kv.Key} = {kv.Value}: a number, {jumpKeys[at].Min} to {jumpKeys[at].Max} (the loader clamps it)");
        }
    }
    CheckSounds(cfg, mdir, BadM);
    if (mok) Pass($"mount {mid}: mount.cfg, own look skins\\default\\, {skinsHere.Count} skin(s){(Directory.Exists(Path.Combine(mdir, "sounds")) ? ", " + Directory.GetFiles(Path.Combine(mdir, "sounds")).Length + " sound file(s)" : "")}");
}

int skins = 0, pictures = 0;
foreach (var dir in skinDirs)
{
    string folderMount = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(dir))!)!;
    string id = folderMount + "/" + Path.GetFileName(dir); // a skin's full name: its mount and its folder (Looks.SkinKey)
    string cfgPath = Path.Combine(dir, "skin.cfg");
    if (!File.Exists(cfgPath)) { Fail($"{id}: no skin.cfg"); continue; }
    skins++;
    var cfg = ReadCfg(cfgPath);
    if (!cfg.TryGetValue("Skin", out var skin)) { Fail($"{id}: no [Skin] section"); continue; }
    bool ok = true;
    void Bad(string what) { Fail($"{id}: {what}"); ok = false; }
    foreach (string h in HostileValues(cfg)) Bad(h);
    // built on the mount's default: a skin of a mount folder with mount.cfg (not the donkey's) that isn't "default"
    string mountDir = Path.GetDirectoryName(Path.GetDirectoryName(dir))!;
    string defaultDir = Path.Combine(mountDir, "skins", "default");
    bool variant = File.Exists(Path.Combine(mountDir, "mount.cfg")) && !Path.GetFileName(dir).Equals("default", StringComparison.OrdinalIgnoreCase) && !folderMount.Equals("donkey", StringComparison.OrdinalIgnoreCase);
    var defaultCfg = variant && File.Exists(Path.Combine(defaultDir, "skin.cfg")) ? ReadCfg(Path.Combine(defaultDir, "skin.cfg")) : null;
    var defaultSkin = defaultCfg != null && defaultCfg.TryGetValue("Skin", out var ds) ? ds : null;
    if (variant && defaultSkin == null) Bad("no ../default/skin.cfg to build on (a mount's other skins are built on its own look)");
    // whether the default has a picture for a key (a game source has them all; a sheet its cells)
    bool DefaultHas(string key)
    {
        if (defaultSkin == null) return false;
        if (defaultSkin.TryGetValue("source", out var dsrc) && dsrc.Equals("game", StringComparison.OrdinalIgnoreCase)) return true;
        if (defaultSkin.TryGetValue("sheet", out _)) return !defaultSkin.TryGetValue("frames", out var fr) || fr.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(key);
        return File.Exists(Path.Combine(defaultDir, key.Replace('/', Path.DirectorySeparatorChar) + ".png"));
    }
    if (!skin.TryGetValue("format", out var format) || format != "1") Bad("format = 1 missing");
    if (skin.ContainsKey("id")) Bad($"[Skin] id = {skin["id"]}: id = is not used: the folder's name is the skin's name (leave the line out)");
    if (string.IsNullOrWhiteSpace(skin.GetValueOrDefault("name"))) Bad("name missing");
    string mount = folderMount;
    if (skin.TryGetValue("mount", out var mountLine) && !mountLine.Equals(folderMount, StringComparison.OrdinalIgnoreCase)) Bad($"[Skin] mount = {mountLine}: the skin sits in the folder of the mount '{folderMount}' (leave the line out: the folder says it)");
    if (skin.TryGetValue("base", out var b) && b != "donkey") Bad($"base = {b}: the donkey is the only base");
    foreach (var k in skin.Keys) if (!skinKeysAllowed.Contains(k) && !k.Equals("id", StringComparison.OrdinalIgnoreCase)) Bad($"[Skin] {k}: not a key the loader knows (a skin has no '{k}')");
    if (skin.TryGetValue("portrait", out var portrait) && !File.Exists(Path.Combine(dir, portrait))) Bad($"[Skin] portrait = {portrait}: no such file in the pack");
    if (!skin.ContainsKey("description") && !(variant && defaultSkin != null && defaultSkin.ContainsKey("description"))) Bad("[Skin] has no description (the Mounts panel shows it; a line or two)");
    foreach (var sec in cfg.Keys.Where(k => k.Equals("Mount", StringComparison.OrdinalIgnoreCase) || k.StartsWith("Mount.", StringComparison.OrdinalIgnoreCase) || k.Equals("Jump", StringComparison.OrdinalIgnoreCase)))
        Bad($"[{sec}] in a skin.cfg: a mount's values go in its mount.cfg (the loader ignores it here)");
    // the values the loader reads by name (anything else silently means the default in the game)
    if (skin.TryGetValue("layer", out var layer) && layer != "replace" && layer != "over") Bad($"[Skin] layer = {layer}: replace or over");
    if (skin.TryGetValue("colours", out var coloursv) && coloursv != "own" && coloursv != "palette") Bad($"[Skin] colours = {coloursv}: own or palette");
    if (skin.TryGetValue("seat", out var seat) && seat != "picture" && !int.TryParse(seat, out _) && !(variant && seat == "none")) Bad($"[Skin] seat = {seat}: picture, or the canvas row (from the top) the rider's hip sits on{(variant ? ", or none (not the default's)" : "")}");
    int seatRow = -1;
    bool seatRowGiven = skin.TryGetValue("seat", out var seatRowText) && int.TryParse(seatRowText, out seatRow);
    if (skin.TryGetValue("source", out var srcv) && srcv != "pictures" && srcv != "game") Bad($"[Skin] source = {srcv}: pictures or game");
    if (skin.TryGetValue("erase", out var erase))
    {
        if (erase != "none" && !Regex.IsMatch(erase, "^[0-9A-Fa-f]{6}$")) Bad($"[Skin] erase = {erase}: RRGGBB or none");
        if (!skin.TryGetValue("layer", out var l2) || l2 != "over") Bad("[Skin] erase is for layer = over only");
    }
    int cw = 96, ch = 96;
    // a variant without a canvas of its own has its default's
    if (!skin.ContainsKey("canvas") && defaultSkin != null && defaultSkin.TryGetValue("canvas", out var dcanvas))
    {
        var dparts = dcanvas.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (dparts.Length == 2 && int.TryParse(dparts[0], out int dcw) && int.TryParse(dparts[1], out int dch)) { cw = dcw; ch = dch; }
    }
    if (skin.TryGetValue("canvas", out var canvas))
    {
        var parts = canvas.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out cw) || !int.TryParse(parts[1], out ch)) Bad($"canvas = {canvas}: two numbers expected");
        else if (cw < 8 || cw > maxCanvas || ch < 8 || ch > maxCanvas) Bad($"canvas = {canvas}: 8 to {maxCanvas} (Looks.MaxCanvas: the loader refuses more)");
    }
    if (seatRowGiven && (seatRow < 0 || seatRow >= ch)) Bad($"[Skin] seat = {seatRow}: a row of the {cw}x{ch} canvas (0 to {ch - 1}, from the top)");
    // the sections and their order (the canonical layout, tools/skins/canonical_cfg.py)
    var order = SectionOrder(cfgPath);
    foreach (var sec in order)
    {
        if (!knownSections.Contains(sec) && !sec.StartsWith("Base.", StringComparison.OrdinalIgnoreCase) && !sec.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase) && !sec.StartsWith("Region.", StringComparison.OrdinalIgnoreCase) && !sec.StartsWith("Mount", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}]: not a section the loader knows");
        if (sec.StartsWith("Base.", StringComparison.OrdinalIgnoreCase))
        {
            string region = sec.Substring(5);
            if (!mountRegions.Contains(region)) Bad($"[{sec}]: not a mount region ({string.Join(", ", mountRegions)})");
            foreach (var kv in cfg[sec]) if (!baseKnobs.Contains(kv.Key)) Bad($"[{sec}] {kv.Key}: not a colour knob ({string.Join(", ", baseKnobs)})");
        }
        if (sec.StartsWith("Region.", StringComparison.OrdinalIgnoreCase))
        {
            // a look's name for a mount region's part (SkinRegions; docs/second-tone.md 8), row = none, no row for the
            // region (docs/mount-colours.md 7.13), or colours =, the skin's own colours in it (7.15: read as a [Colors]
            // line, checked with them below): a region of the six; a name with row = none is never shown
            string region = sec.Substring(7);
            if (!mountRegions.Contains(region)) Bad($"[{sec}]: not a mount region ({string.Join(", ", mountRegions)})");
            foreach (var kv in cfg[sec]) if (!kv.Key.Equals("name", StringComparison.OrdinalIgnoreCase) && !kv.Key.Equals("row", StringComparison.OrdinalIgnoreCase) && !kv.Key.Equals("colours", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}] {kv.Key}: not a region's key (name, row, colours)");
            bool rowless = cfg[sec].TryGetValue("row", out string rowValue);
            if (rowless && !rowValue.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}] row = {rowValue}: none is its one value");
            bool named = cfg[sec].TryGetValue("name", out string rname) && !string.IsNullOrWhiteSpace(rname);
            bool coloured = cfg[sec].ContainsKey("colours");
            if (rowless && named) Bad($"[{sec}]: a name and row = none: the region has no row, so the name is never shown");
            if (!rowless && !named && !coloured) Bad($"[{sec}]: no name, row = none or colours");
            if (coloured && region.Equals("Coat", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}] colours: every colour not named is Coat; don't list it");
        }
    }
    if (order.Count == 0 || !order[0].Equals("Skin", StringComparison.OrdinalIgnoreCase)) Bad("[Skin] must be the first section");
    int gameAt = order.FindIndex(s => s.Equals("Game", StringComparison.OrdinalIgnoreCase));
    if (gameAt > 1) Bad("[Game] stands right after [Skin]");
    int firstColour = order.FindIndex(s => s.Equals("Colors", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Region.", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Base.", StringComparison.OrdinalIgnoreCase));
    foreach (var sec in skinOwnSections)
    {
        int at = order.FindIndex(s => s.Equals(sec, StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && firstColour >= 0 && at > firstColour) Bad($"[{sec}] after the colour sections: the skin's tuning and sound sections stand before them");
        if (at >= 0 && gameAt >= 0 && at < gameAt) Bad($"[{sec}] before [Game]: [Game] stands right after [Skin]");
    }
    {
        // among themselves: [Tuning], [Tuning.Ride], [Tuning.Cart], then [Sounds], [Sounds.Ride], [Sounds.Cart] (the canonical writer's order)
        int last = -1;
        foreach (var sec in skinOwnSections)
        {
            int at = order.FindIndex(s => s.Equals(sec, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            if (at < last) Bad($"[{sec}] stands before an earlier section of the skin's own: the order is [Tuning], [Tuning.Ride], [Tuning.Cart], [Sounds], [Sounds.Ride], [Sounds.Cart]");
            last = at;
        }
    }
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
        int found = 0, fromDefault = 0;
        foreach (var key in knownKeys)
        {
            string png = Path.Combine(dir, key + ".png");
            if (!File.Exists(png) && variant && DefaultHas(key)) { fromDefault++; continue; } // its default's picture is drawn there
            if (!File.Exists(png)) { Bad($"picture {key}.png missing"); continue; }
            found++;
            var (w, h) = PngSize(png);
            if (w != cw || h != ch) Bad($"{key}.png is {w}x{h}, the canvas is {cw}x{ch}");
        }
        pictures += found;
        foreach (var png in Directory.GetFiles(dir, "*.png"))
        {
            string key = Path.GetFileNameWithoutExtension(png);
            if (!knownKeys.Contains(key) && !FlightKey(key) && !(skin.TryGetValue("portrait", out var pfile) && pfile.Equals(key + ".png", StringComparison.OrdinalIgnoreCase))) Bad($"{key}.png: not a frame key of the donkey base (nor a flying one)");
        }
        if (variant) Console.WriteLine($"INFO {id}: built on {folderMount}/default: {found} own picture(s), {fromDefault} from it{(skin.GetValueOrDefault("layer") == "over" ? ", drawn over it" : "")}");
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
    // a variant's [Colors] may name colours of its default's pictures too (the ones it takes from there)
    if (variant && inPictures != null && Directory.Exists(defaultDir) && !(defaultSkin?.ContainsKey("sheet") ?? false))
    {
        try { inPictures.UnionWith(InheritedColours(dir, defaultDir, skin, defaultSkin, over)); }
        catch (InvalidDataException e) { Bad("../default: " + e.Message); }
    }
    // the erase colour is an over skin's: a replace skin drawing it would show magenta in the game
    if (!over && inPictures != null)
    {
        string eraseHex = (skin.GetValueOrDefault("erase") ?? "FF00FF").ToUpperInvariant();
        if (eraseHex != "NONE" && PngColours(dir).Contains(eraseHex)) Bad($"pixels of the erase colour {eraseHex} in a replace skin: only a skin over the donkey (or over its default) erases; they would be drawn as magenta");
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
    // the sounds: its own lines and its own sounds\ (the mount's are in mount.cfg and the mount folder's sounds\)
    CheckSounds(cfg, dir, Bad);
    // [Colors] (docs/mount-colours.md): regions from the mount vocabulary (Colors.cs MountRegionKeys, never Coat: it is
    // everything not named), every colour RRGGBB, in the pictures, and under one region only. A game-source skin has
    // no pictures here to check against.
    var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // a named colour's place: its region, or [Tone.<id>]
    // a region's colours, from a [Colors] line or a [Region.<region>] colours = line (the loader reads both the same way)
    int NameColours(string label, string region, string value)
    {
        int n = 0;
        foreach (var tok in value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string hex = tok.TrimStart('#').ToUpperInvariant();
            if (!Regex.IsMatch(hex, "^[0-9A-F]{6}$")) { Bad($"{label}: '{tok}' is not a colour (RRGGBB)"); continue; }
            if (seen.TryGetValue(hex, out var other) && !other.Equals(region, StringComparison.OrdinalIgnoreCase)) Bad($"{label}: {hex} is under {other} and {region}; one region per colour");
            seen[hex] = region;
            if (inPictures != null && !inPictures.Contains(hex)) Bad($"{label} {hex}: no picture of the skin has this colour (a stale list)");
            n++;
        }
        return n;
    }
    if (cfg.TryGetValue("Colors", out var colorsSec))
    {
        int named = 0;
        foreach (var kv in colorsSec)
        {
            if (!mountRegions.Contains(kv.Key)) { Bad($"[Colors] {kv.Key}: not a mount region ({string.Join(", ", mountRegions)})"); continue; }
            if (kv.Key.Equals("Coat", StringComparison.OrdinalIgnoreCase)) { Bad("[Colors] Coat: every colour not named is Coat; don't list it"); continue; }
            named += NameColours($"[Colors] {kv.Key}", kv.Key, kv.Value);
        }
        if (ok) Pass($"{id}: [Colors] names {named} colour(s) under {colorsSec.Count} region(s)" + (inPictures != null ? $" of the pictures' {inPictures.Count}" : ""));
    }
    foreach (var sec in order)
    {
        if (!sec.StartsWith("Region.", StringComparison.OrdinalIgnoreCase) || !cfg[sec].TryGetValue("colours", out var rc)) continue;
        string region = sec.Substring(7);
        if (!mountRegions.Contains(region) || region.Equals("Coat", StringComparison.OrdinalIgnoreCase)) continue; // said above
        int n = NameColours($"[{sec}] colours", mountRegions.First(r => r.Equals(region, StringComparison.OrdinalIgnoreCase)), rc);
        if (ok) Pass($"{id}: [{sec}] names {n} colour(s) of its own");
    }
    var followingTones = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "[Tone.<id>]" of the tones with follows = (the tone block below)
    // the donkey is under a skin over the donkey only (over another mount's default, layer = over draws over that)
    bool overDonkey = over && mount.Equals("donkey", StringComparison.OrdinalIgnoreCase);
    // the exact swap's colours with no tone set (Colors.Rig.Rebuild), whenSet the more with every tone set: over the
    // donkey its pairs, the donkey's colours and its [Colors] ones (a tone, one that follows a region too since
    // 2026-11-04, is paired only once the player sets it: Colors.Rig.NamedMaps; docs/second-tone.md 8); else every colour
    // of its pictures. By the tone block's exact-swap check and by the colour-path line.
    int SwapColours(HashSet<string> pics, out int whenSet)
    {
        whenSet = 0;
        if (!overDonkey) return pics.Count;
        int always = donkeyColours.Count;
        foreach (var kv in seen)
        {
            // the donkey's pair already (the game pairs it once); one no picture has pairs nothing (said above)
            if (donkeyColours.ContainsKey(kv.Key) || !pics.Contains(kv.Key)) continue;
            if (!kv.Value.StartsWith("[", StringComparison.Ordinal)) always++;
            else whenSet++;
        }
        return always;
    }
    // [Tone.<id>] (docs/mount-colours.md section 7): the skin's own tones, each an id (lower case letters, digits, '-': it
    // is part of the settings' keys), a name for its row and its colours: RRGGBB, in the pictures, in one place only (a
    // tone or a [Colors] region), not a colour of the donkey under an over skin (that one follows the donkey's regions);
    // and none in a colour-cube cell with another colour of the look (Colors.Rig.LutIndex: on the cube, over the exact
    // swap's limit or with the player's Lut setting, colours of one cell change together). A palette skin has none.
    // follows = a mount region (the part was that region's before it was a tone): its colours may stay listed under that
    // region in [Colors] too (an API without tones reads them there; the loader moves them to the tone).
    var toneSecs = order.Where(s => s.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); // a section given twice is one (ReadCfg merges it)
    // a variant has its default's tones too: its own colours are checked against them even with no [Tone.*] of its own
    bool defaultTones = defaultCfg != null && defaultCfg.Keys.Any(k => k.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase));
    if (toneSecs.Count > 0 || defaultTones)
    {
        // palette colours: its own word, or a variant's default's when it says none (LooksInherit.Resolve ignores its tones then)
        string colourMode = skin.GetValueOrDefault("colours") ?? (variant ? defaultSkin?.GetValueOrDefault("colours") : null) ?? "own";
        if (toneSecs.Count > 0 && colourMode.Equals("palette", StringComparison.OrdinalIgnoreCase)) Bad($"[Tone.*] on a skin in the donkey's palette colours (colours = palette{(skin.ContainsKey("colours") ? "" : ", its default's")}): the loader ignores it");
        // the colours it draws, its sheet's too, and a variant's default's (the frames it takes from there)
        HashSet<string>? tonePics = null;
        var fromDefault = new HashSet<string>(StringComparer.Ordinal); // the default's colours in the look (a variant's)
        if (!game)
        {
            try
            {
                tonePics = SkinColours(dir, skin);
                if (variant && Directory.Exists(defaultDir)) fromDefault = InheritedColours(dir, defaultDir, skin, defaultSkin, over);
                tonePics.UnionWith(fromDefault);
            }
            catch (InvalidDataException) { tonePics = null; } // said above, with the skin's other pictures
        }
        var toneOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // hex -> tone id
        foreach (var sec in toneSecs)
        {
            string tid = sec.Substring(5).ToLowerInvariant(); // the loader reads section names in lower case: [Tone.Cloth] is the tone cloth
            if (!Regex.IsMatch(tid, "^[a-z0-9-]{1,40}$")) Bad($"[{sec}]: a tone's id is lower case letters, digits and '-' (it is part of the settings' keys; the loader leaves the tone out)");
            var tone = cfg[sec];
            foreach (var k in tone.Keys) if (!k.Equals("name", StringComparison.OrdinalIgnoreCase) && !k.Equals("colours", StringComparison.OrdinalIgnoreCase) && !k.Equals("colors", StringComparison.OrdinalIgnoreCase) && !k.Equals("follows", StringComparison.OrdinalIgnoreCase)) Bad($"[{sec}] {k}: not a tone's key (name, colours, follows)");
            string? follows = tone.GetValueOrDefault("follows");
            if (follows != null && !mountRegions.Contains(follows)) Bad($"[{sec}] follows = {follows}: not a mount region ({string.Join(", ", mountRegions)}; the loader leaves the tone out)");
            else if (follows != null) followingTones.Add("[" + sec + "]");
            // over the donkey a colour named nowhere is drawn as it is, never Coat: following Coat keeps no earlier look there
            if (follows != null && overDonkey && follows.Trim().Equals("Coat", StringComparison.OrdinalIgnoreCase)) Console.WriteLine($"INFO {id}: [{sec}] follows = Coat over the donkey: the part takes the player's saved coat colour as its first value, where it was drawn as it is before (a colour named nowhere is never Coat on a skin over the donkey); meant for a part that should follow the coat");
            // a variant's tone of its default's id adds shades to it (keeping its name) or renames it (no colours): both fine
            bool inherited = defaultCfg != null && defaultCfg.ContainsKey(sec);
            if (!tone.ContainsKey("name") && !inherited) Bad($"[{sec}] has no name: its row would be labelled '{tid}'");
            string list = tone.GetValueOrDefault("colours") ?? tone.GetValueOrDefault("colors") ?? "";
            if (list.Trim().Length == 0 && !inherited) Bad($"[{sec}] names no colours: it colours nothing and gets no row");
            foreach (var tok in list.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string hex = tok.TrimStart('#').ToUpperInvariant();
                if (!Regex.IsMatch(hex, "^[0-9A-F]{6}$")) { Bad($"[{sec}]: '{tok}' is not a colour (RRGGBB)"); continue; }
                // listed under the region it follows too: the compatible listing (the loader moves it to the tone)
                bool compatible = follows != null && seen.TryGetValue(hex, out var under) && under.Equals(follows, StringComparison.OrdinalIgnoreCase);
                if (!compatible && seen.TryGetValue(hex, out var other) && !other.Equals("[" + sec + "]", StringComparison.OrdinalIgnoreCase)) Bad($"{hex} is under {other} and [{sec}]; one place per colour (the loader leaves the tone out)");
                seen[hex] = "[" + sec + "]";
                toneOf[hex] = tid;
                if (tonePics != null && !tonePics.Contains(hex)) Bad($"[{sec}] {hex}: no picture of the skin has this colour (a stale list)");
                if (overDonkey && donkeyColours.ContainsKey(hex)) Bad($"[{sec}] {hex} is the donkey's own colour ({donkeyColours[hex]}): it follows the donkey's regions, not the tone; draw it a shade apart");
            }
        }
        if (tonePics != null)
        {
            // a variant's default's colours keep the default's places (its [Colors] regions and its tones), its own over them
            var defaultPlace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (defaultCfg != null)
            {
                // as the loader reads the default: its [Colors] first, then its tones in the file's order, a tone left out
                // whole when one of its colours is in another place than the region it follows (the compatible listing:
                // the tone's then; the default's own check says the rest)
                static IEnumerable<string> Hexes(string? list) => (list ?? "").Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.TrimStart('#').ToUpperInvariant());
                if (defaultCfg.TryGetValue("Colors", out var dColors))
                    foreach (var kv in dColors) foreach (var hex in Hexes(kv.Value)) defaultPlace[hex] = kv.Key;
                foreach (var dsec in defaultCfg)
                {
                    if (!dsec.Key.StartsWith("Tone.", StringComparison.OrdinalIgnoreCase)) continue;
                    string place = "Tone." + dsec.Key.Substring(5).ToLowerInvariant();
                    string? dfollows = dsec.Value.GetValueOrDefault("follows");
                    var dhexes = Hexes(dsec.Value.GetValueOrDefault("colours") ?? dsec.Value.GetValueOrDefault("colors")).ToList();
                    if (dhexes.Any(h => defaultPlace.TryGetValue(h, out var p) && !p.Equals(place, StringComparison.OrdinalIgnoreCase) && !(dfollows != null && p.Equals(dfollows, StringComparison.OrdinalIgnoreCase)))) continue;
                    foreach (var hex in dhexes) defaultPlace[hex] = place;
                }
            }
            // every colour the look draws on the mount and its place: the donkey's under a skin over it, then the skin's
            var placeOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (overDonkey) foreach (var kv in donkeyColours) placeOf[kv.Key] = "the donkey's " + kv.Value;
            foreach (var hex in tonePics)
            {
                if (placeOf.ContainsKey(hex)) continue;
                placeOf[hex] = toneOf.TryGetValue(hex, out var tn) ? "Tone." + tn.ToLowerInvariant() : seen.TryGetValue(hex, out var rg) ? rg : defaultPlace.TryGetValue(hex, out var dp) ? dp : over && !fromDefault.Contains(hex) ? "kept as drawn" : "Coat";
            }
            foreach (var cell in placeOf.GroupBy(p => LutCell(p.Key)))
            {
                var places = cell.Select(p => p.Value).Distinct().ToList();
                if (places.Count > 1 && places.Any(p => p.StartsWith("Tone.", StringComparison.Ordinal)))
                    Bad($"a tone's colour shares a colour-cube cell with another colour of the look: {string.Join(", ", cell.OrderBy(p => p.Key).Select(p => p.Key + " (" + p.Value + ")"))} (draw one of them a step apart)");
            }
            // the exact swap (the palette shader: a texel within 0.004 of a source colour takes its replacement, and one
            // step in one channel is 1/255, 0.0039): two colours one step apart in one channel are one colour to it, so a
            // tone's colour beside another place's is recoloured with it, or that one with the tone. Checked while the skin
            // is on the swap with no tone set (on the cube even then, the cube check above holds); a skin without tones has
            // no tone's colour to check (check_skin.py shows its other places' one-step pairs for the eye)
            if (SwapColours(tonePics, out _) <= (overDonkey ? paletteMaxOver : paletteMax))
                foreach (var p in placeOf.OrderBy(p => p.Key, StringComparer.Ordinal))
                    foreach (var n in StepNeighbours(p.Key))
                        if (placeOf.TryGetValue(n, out var np) && np != p.Value && (p.Value.StartsWith("Tone.", StringComparison.Ordinal) || np.StartsWith("Tone.", StringComparison.Ordinal)))
                            Bad($"a tone's colour one step from another colour of the look in one channel: {p.Key} ({p.Value}), {n} ({np}); the exact swap takes them as one colour (draw one of them a step further apart)");
        }
        if (ok) Pass($"{id}: {toneSecs.Count} tone(s) of its own with {toneOf.Count} colour(s){(defaultTones ? " and its default's" : "")}, each in one place, its own colour-cube cell and more than a step from another place's colours");
    }
    // which colour path the game takes for it (Colors.cs, the limits read above): the exact swap, or the colour cube, where
    // two colours in one cell change together (anti-aliased edges and the front layer count too). In its own colours
    // (over its default too): the pictures' colours against PaletteMaxColours. Over the donkey: its pairs against
    // PaletteMaxColoursOver, the donkey's colours and its [Colors] ones always, a tone only while the player sets it
    // (Colors.Rig.NamedMaps; one that follows a region too, since 2026-11-04)
    if (inPictures != null)
    {
        string PathFor(int n, int max) => n > max ? $"over {max}, the colour cube (a few near-identical colours may change together; fewer colours = the exact swap)" : "the exact swap";
        int always = SwapColours(inPictures, out int whenSet);
        if (!overDonkey) Console.WriteLine($"INFO {id}: {always} colour(s) in the pictures: {PathFor(always, paletteMax)}");
        else Console.WriteLine($"INFO {id}: over the donkey, {always} colour pair(s) with no tone set: {PathFor(always, paletteMaxOver)}" + (whenSet > 0 ? $"; {always + whenSet} with every tone set: {PathFor(always + whenSet, paletteMaxOver)}" : ""));
        // on the colour cube whatever the player sets (over the exact swap's limit) a cell is recoloured as one, so it holds
        // one part's colours: the worm's mouth one cell with its outline would have taken the outline's 5896 pixels with
        // a Mouth colour (2026-11-04). Shades of one region in a cell are fine (near-identical, they change together); a
        // tone's cells are the tone check's above; a palette skin draws the donkey's own colours; the donkey's own colours
        // sharing a cell are the game's art. With the player's Lut fallback every look is on the cube: that setting's
        // description says neighbouring regions may change together there.
        int limit = overDonkey ? paletteMaxOver : paletteMax;
        string mode = skin.GetValueOrDefault("colours") ?? (variant ? defaultSkin?.GetValueOrDefault("colours") : null) ?? "own";
        if (always > limit && !mode.Equals("palette", StringComparison.OrdinalIgnoreCase))
        {
            // a colour's part: its [Colors] region (a variant's own over its default's), the donkey's region under a skin
            // over it, else Coat (in its own colours) or kept as drawn (over the donkey)
            var partOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (defaultCfg != null && defaultCfg.TryGetValue("Colors", out var dCols))
                foreach (var kv in dCols) foreach (var t in kv.Value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries)) partOf[t.TrimStart('#').ToUpperInvariant()] = kv.Key;
            foreach (var kv in seen) partOf[kv.Key] = kv.Value;
            string PartOf(string hex) => overDonkey && donkeyColours.TryGetValue(hex, out var dr) ? dr : partOf.TryGetValue(hex, out var p) ? p : overDonkey ? "kept as drawn" : "Coat";
            var drawn = new HashSet<string>(inPictures, StringComparer.OrdinalIgnoreCase);
            if (overDonkey) drawn.UnionWith(donkeyColours.Keys);
            int mixed = 0;
            foreach (var cell in drawn.Where(h => !PartOf(h).StartsWith("[", StringComparison.Ordinal)).GroupBy(LutCell))
            {
                var parts = cell.Select(PartOf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (parts.Count < 2 || cell.All(h => overDonkey && donkeyColours.ContainsKey(h))) continue;
                mixed++;
                Bad($"on the colour cube ({always} colours, over the exact swap's {limit}) a cell holds colours of {parts.Count} parts: {string.Join(", ", cell.OrderBy(h => h, StringComparer.Ordinal).Select(h => h + " (" + PartOf(h) + ")"))}; a colour set for one recolours the other (draw one of them a cube step apart: Colors.Rig.LutIndex)");
            }
            if (mixed == 0) Pass($"{id}: on the colour cube, every cell holds one part's colours");
        }
    }
    if (!mount.Equals("donkey", StringComparison.OrdinalIgnoreCase))
    {
        // what the skin lacks: the donkey's own events with no line in its own or its mount's blocks (riding plain; the
        // babies' answer, played by babies only, by a [Sounds.Baby] line too: Sounds.Coverage does the same)
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        mountCfgs.TryGetValue(mount, out var mountCfg);
        foreach (var c in new[] { cfg, defaultCfg, mountCfg }) // a variant: its default's lines too
            if (c != null)
                foreach (var sec in new[] { "Sounds", "Sounds.Ride", "Sounds.Baby" })
                    if (c.TryGetValue(sec, out var lines)) foreach (var key in lines.Keys) { var parts = key.Split('.'); if ((parts.Length == 1 || parts.Length == 2 && soundVariants.Contains(parts[1])) && (sec != "Sounds.Baby" || parts[0].Equals("answer", StringComparison.OrdinalIgnoreCase))) covered.Add(parts[0]); }
        var missing = donkeyOwn.Where(e => !covered.Contains(e)).OrderBy(e => e).ToList();
        Console.WriteLine(missing.Count == 0 ? $"INFO {id}: no sound missing (every one of the donkey's own events has a line)" : $"INFO {id}: sounds MISSING (the donkey's own, silent on this mount until given): {string.Join(", ", missing)}");
    }
    if (ok) Pass($"{id}: skin.cfg, {(sheet ? "a sheet" : game ? "the game's own frames (source = game)" : variant ? "built on its default" : "44 pictures")}{(Directory.Exists(Path.Combine(dir, "front")) ? " + a front layer" : "")}, mount '{mount}'");
}
Console.WriteLine($"INFO {skins} shipped skins in {mountDirs.Count} mount folders, {mountCfgs.Count} mounts declared ({string.Join(", ", mountCfgs.Keys.OrderBy(k => k))}), {pictures} pictures");
Console.WriteLine($"INFO built in: {string.Join(", ", mountDirs.Where(d => PackOf(d) == null).Select(Path.GetFileName))}; external: {string.Join("; ", mountDirs.Where(d => PackOf(d) != null).GroupBy(d => PackOf(d)).Select(g => g.Key + " (" + string.Join(", ", g.Select(Path.GetFileName)) + ")"))}");
Console.WriteLine(fails == 0 ? $"ALL PASS ({passes})" : $"FAILURES: {fails}");
return fails;
