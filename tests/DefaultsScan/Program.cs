// The shipped mod defaults (src/GK2DonkeyAPI/defaults.cfg, embedded into the DLL) hold no colours: every player
// starts with the game's own art, and colours come from a mod or the player. Found 2026-10-02: the panel's "Make these
// the mod defaults" saved a colour test that was still set, and every player would have had a green donkey. The
// button now leaves colours out (Defaults.IsColor); this fails if any get in again.
// Usage: dotnet run -c Release
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
var path = Path.Combine(repo, "src", "GK2DonkeyAPI", "defaults.cfg");
int fails = 0;
string? section = null;
int lineNo = 0, entries = 0;
foreach (var raw in File.ReadLines(path))
{
    lineNo++;
    var line = raw.Trim();
    if (line.StartsWith("[") && line.EndsWith("]"))
    {
        section = line[1..^1];
        continue;
    }
    if (line.Length == 0 || line.StartsWith("#") || !line.Contains('='))
    {
        continue;
    }
    entries++;
    var key = line[..line.IndexOf('=')].Trim();
    bool color = section != null && section.StartsWith("Colors", StringComparison.OrdinalIgnoreCase);
    // The lamp's light colour is a style key, also part of the look: only the game's own orange is allowed.
    bool light = key.Equals("LanternLightColor", StringComparison.OrdinalIgnoreCase) && !line.EndsWith("FF9600", StringComparison.OrdinalIgnoreCase);
    if (color || light)
    {
        Console.WriteLine($"FAIL line {lineNo}: [{section}] {line} (a colour in the mod defaults)");
        fails++;
    }
}
Console.WriteLine($"INFO {entries} defaults in {path}");
Console.WriteLine(fails == 0 ? "PASS no colours in the mod defaults (the game's art for everyone)" : $"FAILURES: {fails}");
Console.WriteLine(fails == 0 ? "ALL PASS" : "");
return fails;
