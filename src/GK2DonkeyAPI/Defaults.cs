using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;

namespace GK2DonkeyAPI
{
    // Two kinds of values, and only two:
    //   Your settings: dreas.gk2.donkeyapi.cfg. BepInEx writes it on every change (SaveOnConfigSet), so the panel has no
    //     save button; what you set is kept.
    //   Mod defaults: what a fresh install starts with, and what "reset" goes back to. They live in defaults.cfg next
    //     to the sources and are embedded into the DLL at build time.
    // The build records the source folder (AssemblyMetadata GK2DonkeyAPISourceDir). On the machine that has it (the
    // developer's), the mod reads the defaults from the repo file instead of the embedded copy, and the panel's "Make
    // these the mod defaults" writes that file directly: no path to set, no copy step, and the panel shows the new
    // defaults at once. A rebuild then ships them. On any other machine the embedded copy is used and the button is
    // absent. Values missing from the file fall back to the constants in code.
    internal static class Defaults
    {
        private const string ResourceName = "GK2DonkeyAPI.defaults.cfg";
        public const string FileName = "defaults.cfg";

        private static Dictionary<string, string> values;
        private static readonly Dictionary<ConfigEntryBase, object> converted = new Dictionary<ConfigEntryBase, object>();

        // Where the defaults were read from: the repo file's path, or "built into the DLL".
        public static string Source { get; private set; } = "(not loaded)";

        // Source for running text: no full path (one long unbreakable word in a wrapped label).
        public static string SourceShort => Source == RepoDefaultsPath && Source != null ? "the repo's defaults.cfg (src/GK2DonkeyAPI)" : Source;

        private static readonly string SourceDir = ReadSourceDir();

        private static string ReadSourceDir()
        {
            try
            {
                foreach (AssemblyMetadataAttribute a in typeof(Defaults).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (a.Key == "GK2DonkeyAPISourceDir")
                    {
                        return a.Value;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        // A folder a dev build of a DLL recorded as assembly metadata (the API's GK2DonkeyAPISourceDir, a mod's
        // GK2SkinsSourceDir for its embedded skin packs), when it exists on this machine; else null.
        internal static string SourceDirOf(Assembly asm, string key)
        {
            try
            {
                foreach (AssemblyMetadataAttribute a in asm.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (a.Key == key) return !string.IsNullOrEmpty(a.Value) && Directory.Exists(a.Value) ? a.Value : null;
                }
            }
            catch
            {
            }
            return null;
        }

        // A file in the mod's source folder, or null when this machine does not have the sources.
        public static string RepoFile(string name)
        {
            if (string.IsNullOrEmpty(SourceDir))
            {
                return null;
            }
            try
            {
                string p = Path.Combine(SourceDir, name);
                return File.Exists(p) ? p : null;
            }
            catch
            {
                return null;
            }
        }

        public static string RepoDefaultsPath => RepoFile(FileName);

        // A path in the mod's source folder whether the file is there or not; null without the sources.
        public static string RepoPath(string name)
        {
            try
            {
                return string.IsNullOrEmpty(SourceDir) || !Directory.Exists(SourceDir) ? null : Path.Combine(SourceDir, name);
            }
            catch
            {
                return null;
            }
        }
        public static bool CanWriteDefaults => RepoDefaultsPath != null;

        public static void Load()
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            converted.Clear();
            string repo = RepoDefaultsPath;
            if (repo != null)
            {
                try
                {
                    Parse(File.ReadAllText(repo));
                    Source = repo;
                    return;
                }
                catch (Exception e)
                {
                    values.Clear();
                    Plugin.Log?.LogWarning("Defaults: could not read " + repo + " (" + e.Message + "); using the built-in copy.");
                }
            }
            try
            {
                using (Stream s = typeof(Defaults).Assembly.GetManifestResourceStream(ResourceName))
                {
                    if (s == null)
                    {
                        Source = "(none, code constants)";
                        return;
                    }
                    using (var r = new StreamReader(s))
                    {
                        Parse(r.ReadToEnd());
                    }
                    Source = "built into the DLL";
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("Defaults: could not read embedded defaults: " + e.Message);
            }
        }

        private static void Parse(string text)
        {
            string section = "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                values[section + "." + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
        }

        private static bool TryConvert(string raw, Type t, out object value)
        {
            value = null;
            try
            {
                if (t == typeof(float)) value = float.Parse(raw, CultureInfo.InvariantCulture);
                else if (t == typeof(int)) value = int.Parse(raw, CultureInfo.InvariantCulture);
                else if (t == typeof(bool)) value = bool.Parse(raw);
                else if (t.IsEnum) value = Enum.Parse(t, raw, ignoreCase: true);
                else if (t == typeof(string)) value = raw;
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        // Used by Config.Bind: the mod default for a key, or the code constant.
        public static T Get<T>(string section, string key, T fallback)
        {
            if (values == null)
            {
                Load();
            }
            if (!values.TryGetValue(section + "." + key, out string raw))
            {
                return fallback;
            }
            if (TryConvert(raw, typeof(T), out object v))
            {
                return (T)v;
            }
            Plugin.Log?.LogWarning($"Defaults: bad value for {section}.{key} = '{raw}'; using {fallback}.");
            return fallback;
        }

        // The current mod default of a setting. BepInEx's own DefaultValue is fixed at Bind time; after "Make these
        // the mod defaults" the file is re-read and this answers with the new value.
        public static object DefaultFor(ConfigEntryBase e)
        {
            if (values == null)
            {
                Load();
            }
            if (converted.TryGetValue(e, out object c))
            {
                return c;
            }
            object v = e.DefaultValue;
            if (values.TryGetValue(e.Definition.Section + "." + e.Definition.Key, out string raw) && TryConvert(raw, e.SettingType, out object parsed))
            {
                v = parsed;
            }
            converted[e] = v;
            return v;
        }

        public static void ResetToDefault(ConfigEntryBase e)
        {
            e.BoxedValue = DefaultFor(e);
        }

        public static bool Differs(ConfigEntryBase e)
        {
            object d = DefaultFor(e);
            // The common types read without BoxedValue, which boxes the value on every read (every row of the panel asks).
            switch (e)
            {
                case ConfigEntry<float> f when d is float fd:
                    return Math.Abs(f.Value - fd) > 0.0001f;
                case ConfigEntry<int> n when d is int nd:
                    return n.Value != nd;
                case ConfigEntry<bool> bo when d is bool bd:
                    return bo.Value != bd;
                case ConfigEntry<string> s:
                    return !string.Equals(s.Value ?? "", d as string ?? "", StringComparison.Ordinal);
            }
            object v = e.BoxedValue;
            if (v is float a && d is float b)
            {
                return Math.Abs(a - b) > 0.0001f;
            }
            if (v is string sa || d is string)
            {
                return !string.Equals(v as string ?? "", d as string ?? "", StringComparison.Ordinal);
            }
            return !Equals(v, d);
        }

        // The settings that make up the mod defaults: the General keys that shape the look, then both styles.
        private static List<(string Group, ConfigEntryBase Entry)> tracked;
        private static HashSet<ConfigEntryBase> trackedSet;

        public static List<(string Group, ConfigEntryBase Entry)> Tracked
        {
            get
            {
                if (tracked == null)
                {
                    tracked = new List<(string, ConfigEntryBase)>();
                    foreach (ConfigEntryBase e in new ConfigEntryBase[] { Plugin.StayMountedThroughDoors, Plugin.DismountIndoors, Plugin.CartIndoors, Plugin.ShowMessages, Plugin.CargoZombies, Plugin.CargoChest, Plugin.InstanceControls, Plugin.HitchKey, Plugin.CartZoneSide, Plugin.CartZoneRear, Plugin.CartZoneFrontShift, Plugin.DonkeyZoneAhead, IdleShuffle.On })
                    {
                        tracked.Add(("General", e));
                    }
                    foreach (ConfigEntryBase e in IdleShuffle.Entries)
                    {
                        tracked.Add((IdleShuffle.Section, e));
                    }
                    foreach (StyleSettings s in new[] { Plugin.Ride, Plugin.Cart })
                    {
                        foreach (ConfigEntryBase e in s.AllEntries)
                        {
                            tracked.Add((s.Section, e));
                        }
                    }
                    // The colours: the group is the entry's own section ([Colors], [Colors.Coat], ...), which the
                    // defaults file is written with.
                    foreach (ConfigEntryBase e in Colors.AllEntries())
                    {
                        tracked.Add((e.Definition.Section, e));
                    }
                    trackedSet = new HashSet<ConfigEntryBase>();
                    foreach (var t in tracked)
                    {
                        trackedSet.Add(t.Entry);
                    }
                }
                return tracked;
            }
        }

        public static bool IsTracked(ConfigEntryBase e)
        {
            _ = Tracked;
            return e != null && trackedSet.Contains(e);
        }

        public static List<(string Group, ConfigEntryBase Entry)> Differing()
        {
            var list = new List<(string, ConfigEntryBase)>();
            Differing(list);
            return list;
        }

        // Into a kept list (the panel takes this a few times a second).
        public static void Differing(List<(string Group, ConfigEntryBase Entry)> into)
        {
            into.Clear();
            foreach (var t in Tracked)
            {
                if (Differs(t.Entry))
                {
                    into.Add(t);
                }
            }
        }

        public static string Show(object v)
        {
            if (v is IFormattable f)
            {
                return f.ToString(null, CultureInfo.InvariantCulture);
            }
            string s = v?.ToString() ?? "";
            return s.Length == 0 ? "(empty)" : s;
        }

        // Colours are never mod defaults. Every player starts with the game's own art; colours are chosen by a mod
        // (Saddle Up's presets, Mounts.Colors) or by the player. Found 2026-10-02: a colour test on the panel was
        // still set when this button was pressed, and the shipped defaults gave every player a green donkey.
        // tests/DefaultsScan fails if a colour ever gets into defaults.cfg again.
        public static bool IsColor(string group) => group != null && group.StartsWith("Colors", StringComparison.OrdinalIgnoreCase);

        // A colour group, or a colour of the look in another group (the lamp's light colour, Tuned.IsLook).
        public static bool IsColor((string Group, ConfigEntryBase Entry) t) => IsColor(t.Group) || (Tuned.ByEntry.TryGetValue(t.Entry, out ITuned tu) && tu.IsLook);

        // The differing settings the button would write (the colours left out).
        public static int DifferingWritable()
        {
            int n = 0;
            foreach (var t in Tracked)
            {
                if (!IsColor(t) && Differs(t.Entry))
                {
                    n++;
                }
            }
            return n;
        }

        // Writes every tracked setting's current value, colours excepted, to the repo's defaults.cfg and re-reads it,
        // so the panel's defaults (reset targets, the yellow marks) are the new ones at once. Returns the path.
        public static string MakeCurrentTheDefaults()
        {
            string path = RepoDefaultsPath;
            if (path == null)
            {
                throw new InvalidOperationException("the mod's source folder is not on this machine");
            }
            var sb = new StringBuilder();
            sb.AppendLine("# GK2 Donkey API: the mod's default values, embedded into the DLL at build time.");
            sb.AppendLine("# Written by the tuning panel's \"Make these the mod defaults\" button; rebuild to ship them.");
            sb.AppendLine("# Format: [Section] then Key = Value, same names as dreas.gk2.donkeyapi.cfg.");
            string group = null;
            sb.AppendLine("# No colours: they are never mod defaults (the game's art for everyone), see Defaults.IsColor.");
            foreach (var t in Tracked)
            {
                if (IsColor(t))
                {
                    continue; // left out: the code's own value (the game's look) is the default
                }
                if (t.Group != group)
                {
                    sb.AppendLine().AppendLine("[" + t.Group + "]");
                    group = t.Group;
                }
                object v = t.Entry.BoxedValue;
                string text = v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : (v?.ToString() ?? "");
                sb.AppendLine(t.Entry.Definition.Key + " = " + text);
            }
            File.WriteAllText(path, sb.ToString());
            Load();
            return path;
        }
    }
}
