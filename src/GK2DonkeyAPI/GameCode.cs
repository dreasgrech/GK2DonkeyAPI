using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using GK2Shared;

namespace GK2DonkeyAPI
{
    // The start-up check of the game's code and what the mod does with its verdict (docs/dependency-check.md). The
    // shared GameDependencies compares the manifest embedded in this DLL (every game member the mod uses, hashed when
    // it was built) with the installed game; this class reads the verdict by the mod's own classes behind each changed
    // member (game-code.txt, embedded):
    //
    //   identical            run
    //   missing / failed     stop (a member that is gone, or a check that could not run): the game runs unmodified
    //   changed, core        stop, unless Safety.ForceRunOnChangedGameCode (then everything runs but the chest, loudly)
    //   changed, a feature   that feature is off for the session (frozen) or runs with a warning (warned); the rest runs
    //   changed, dev tools   the dev tools are locked for the session; the mod runs
    //   Safety.StopOnChangedGameCode turns every changed verdict into a stop.
    //
    // A stopped or partly frozen mod never endangers an item: nothing of the mod's is in the game's save (the cargo is
    // lent to it as drops at the cart, a chest stands there), so a load with a feature off leaves things where the save
    // holds them (docs/persistence.md).
    internal static class GameCode
    {
        internal const string ResourceName = "GK2DonkeyAPI.game-code.txt";

        internal static ConfigEntry<bool> ForceRun;

        // A page's Status row (Mounts.StatusText): "Active", or what the start decided and why.
        internal static string Status { get; private set; } = "Active";

        // True when the mod stopped at start (Status says why); Mounts.StatusText shows that instead of the generic "off".
        internal static bool Stopped { get; private set; }

        // The dev tools' members changed: DevTools locks them for the session (checked before the [Debug] keys bind).
        internal static bool DevToolsChanged { get; private set; }

        private static GameDependencies.Result result;
        private static readonly HashSet<string> skippedPatches = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> frozenFeatures = new List<string>();

        private enum Kind { Core, Frozen, Warned, Dev, Verify }

        private struct Class
        {
            public Kind Kind;
            public string Feature;   // Frozen/Warned: the feature's name as the log says it
        }

        private static Dictionary<string, Class> classes;              // the mod's class name -> how a changed member it uses is treated
        private static Dictionary<string, List<string>> classesOfFeature; // feature -> its classes (the patches to leave out when frozen)

        // What a frozen feature does with itself at start (nothing is in the game yet; the log line is the gate's).
        private static readonly Dictionary<string, Action<string>> Freezers = new Dictionary<string, Action<string>>(StringComparer.Ordinal)
        {
            { "the corpses at the crossroads", why => Corpses.Freeze(why) },
            { "the chest on the cart", why => CartChest.Lock(why, loud: false) },
            { "the colours", why => Colors.Frozen = true },
            { "the lantern", why => Lantern.Frozen = true },
        };

        internal static void Bind(ConfigFile cfg)
        {
            GameDependencies.Bind(cfg); // Safety.StopOnChangedGameCode, Safety.LastDependencyCheck
            ForceRun = cfg.Bind("Safety", "ForceRunOnChangedGameCode", false,
                "The mod stops when the game changed the code of a core member it uses since the mod was built (riding, the cart, the cargo, the save hooks). " +
                "true runs it anyway, at your own risk, with the chest on the cart off for the session. A member that is gone still stops the mod.");
        }

        // The comparison itself, before the [Debug] keys bind (DevTools reads DevToolsChanged). Fast when the game is
        // the build the mod was made against; otherwise the cached verdict, or one read of the game's assemblies.
        internal static void Check()
        {
            result = GameDependencies.Check(typeof(Plugin).Assembly, typeof(WgoData).Assembly, typeof(LazyBearTechnology.LazyAudio).Assembly);
            if (result.State == GameDependencies.State.Changed)
            {
                LoadClasses();
                foreach (string item in result.Changed)
                {
                    if (KindOf(item, out _) == Kind.Dev)
                    {
                        DevToolsChanged = true;
                    }
                }
            }
        }

        // After every key is bound: logs the verdict, applies it (freezes, locks) and says whether the mod may patch.
        internal static bool Apply()
        {
            GameDependencies.Result r = result;
            string status;
            string builds = $"Assembly-CSharp {r.GameMvid}, built against {r.AuditedGameMvid}; LazyBearTechnology {r.LazyBearMvid}, built against {r.AuditedLazyBearMvid}";
            if (r.State == GameDependencies.State.Missing)
            {
                Plugin.Log.LogError($"GK2 Donkey API: game members this mod uses are gone since it was built ({builds}): {Named(r.Missing)}. Mod is DISABLED; the game runs unmodified. " +
                    "Your donkeys, carts and their loads stay in the save as the game holds them (the cargo lies at the cart, a chest stands there; nothing is lost). Update the mod.");
                return Stop($"Disabled: the game no longer has what this mod uses ({r.Missing.Count} member(s)). Update the mod.");
            }
            if (r.State != GameDependencies.State.Changed)
            {
                // Identical (one info line when the game was recompiled) or failed: the shared texts.
                bool run = GameDependencies.Apply(r, "GK2 Donkey API", Plugin.Log, "Running anyway", out status);
                Status = status;
                Stopped = !run;
                return run;
            }
            if (GameDependencies.StopOnChangedGameCode.Value)
            {
                Plugin.Log.LogError($"GK2 Donkey API: the game changed the code of {r.Changed.Count} of the {r.Checked} game members this mod uses since it was built ({builds}): {Named(r.Changed)}. Mod is DISABLED because Safety.StopOnChangedGameCode=true; the game runs unmodified.");
                return Stop($"Disabled: the game changed {r.Changed.Count} member(s) this mod uses, and Safety.StopOnChangedGameCode is set.");
            }
            // The changed members by what the mod does about them.
            List<string> core = new List<string>();
            Dictionary<string, List<string>> frozen = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Dictionary<string, List<string>> warned = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<string> dev = new List<string>();
            List<string> verifyOnly = new List<string>();
            foreach (string item in r.Changed)
            {
                string named = Named(item);
                switch (KindOf(item, out string feature))
                {
                    case Kind.Core: core.Add(named); break;
                    case Kind.Frozen: Group(frozen, feature).Add(named); break;
                    case Kind.Warned: Group(warned, feature).Add(named); break;
                    case Kind.Dev: dev.Add(named); break;
                    default: verifyOnly.Add(named); break;
                }
            }
            string head = $"GK2 Donkey API: the game changed the code of {r.Changed.Count} of the {r.Checked} game members this mod uses since it was built ({builds}).";
            if (core.Count > 0 && !ForceRun.Value)
            {
                Plugin.Log.LogError($"{head} Core members: {string.Join("; ", core.ToArray())}. Mod is DISABLED; the game runs unmodified. Your donkeys, carts and their loads stay in the save as the game holds them (the cargo lies at the cart, a chest stands there; nothing is lost). " +
                    "Wait for a re-audited build, or set Safety.ForceRunOnChangedGameCode=true to run it anyway at your own risk (the chest on the cart stays off then).");
                return Stop($"Disabled: the game changed {core.Count} core member(s) this mod uses since this build. Wait for an updated mod.");
            }
            // The mod runs. Frozen features first (they must be off before anything is patched or spawned).
            StringBuilder what = new StringBuilder();
            foreach (KeyValuePair<string, List<string>> f in frozen)
            {
                if (!Freeze(f.Key, $"the game changed code {f.Key} uses ({string.Join("; ", f.Value.ToArray())})"))
                {
                    // game-code.txt freezes a feature this build has no switch for (a build defect): the safe side.
                    Plugin.Log.LogError($"{head} game-code.txt says \"{f.Key}\" is off then, but this build has no switch for it; the mod stops instead. Fix the table.");
                    return Stop($"Disabled: a defect in this build's game-code.txt (\"{f.Key}\"). Update the mod.");
                }
                what.Append($" Off for this session: {f.Key} ({string.Join("; ", f.Value.ToArray())}).");
            }
            foreach (KeyValuePair<string, List<string>> w in warned)
            {
                what.Append($" Running on changed code: {w.Key} ({string.Join("; ", w.Value.ToArray())}).");
            }
            if (dev.Count > 0)
            {
                what.Append($" The dev tools are locked for this session ({string.Join("; ", dev.ToArray())}).");
            }
            if (verifyOnly.Count > 0)
            {
                what.Append($" Named only by the start-up verifier, not used (overloads of what the mod calls): {string.Join("; ", verifyOnly.ToArray())}.");
            }
            if (core.Count > 0)
            {
                // Forced: the chest is off whatever its own members say (it is the one feature that moves saved objects).
                Freeze("the chest on the cart", "the game changed core code this mod uses and Safety.ForceRunOnChangedGameCode is set");
                Plugin.Log.LogError($"{head} Core members: {string.Join("; ", core.ToArray())}.{what} RUNNING ANYWAY because Safety.ForceRunOnChangedGameCode=true, at your own risk; the chest on the cart is off for this session.");
                Status = $"Running forced: the game changed {core.Count} core member(s) this mod uses since this build and Safety.ForceRunOnChangedGameCode is set. The chest on the cart is off.";
                return true;
            }
            if (frozen.Count == 0 && warned.Count == 0)
            {
                Plugin.Log.LogInfo($"{head} No feature uses them.{what} The mod runs.");
                Status = "Active";
                return true;
            }
            Plugin.Log.LogWarning($"{head}{what} The rest runs; set Safety.StopOnChangedGameCode=true to stop the mod instead, until a re-audited build.");
            List<string> parts = new List<string>();
            if (frozen.Count > 0) parts.Add("with " + Join(new List<string>(frozen.Keys)) + " off");
            if (warned.Count > 0) parts.Add("with " + Join(new List<string>(warned.Keys)) + " on changed code");
            Status = $"Running {string.Join(" and ", parts.ToArray())}: the game changed {r.Changed.Count} member(s) this mod uses since this build. A re-audited build is pending.";
            return true;
        }

        // Patching failed after the check passed: the Status says so instead of the generic "off".
        internal static void StoppedBy(string why)
        {
            Stopped = true;
            Status = "Disabled: " + why;
        }

        // A patch class of a frozen feature: left out of the patching (Plugin.PatchClasses).
        internal static bool SkipsPatch(Type patchClass) => skippedPatches.Contains(patchClass.Name);

        internal static bool IsFrozen(string feature) => frozenFeatures.Contains(feature);

        private static bool Stop(string status)
        {
            Status = status;
            Stopped = true;
            return false;
        }

        // False when this build has no switch for the feature (the four names in Freezers are the contract with game-code.txt).
        private static bool Freeze(string feature, string why)
        {
            if (frozenFeatures.Contains(feature))
            {
                return true;
            }
            if (!Freezers.TryGetValue(feature, out Action<string> freeze))
            {
                return false;
            }
            frozenFeatures.Add(feature);
            if (classesOfFeature != null && classesOfFeature.TryGetValue(feature, out List<string> names))
            {
                foreach (string n in names)
                {
                    if (n.EndsWith("_Patch", StringComparison.Ordinal)) skippedPatches.Add(n);
                }
            }
            freeze(why);
            return true;
        }

        // "M System.Void X::Foo()" -> "X::Foo() (used by Mount, Cargo)"
        private static string Named(string item)
        {
            string key = item.Length > 2 ? item.Substring(2) : item;
            int sp = key.IndexOf(' ');
            if (sp >= 0 && key.IndexOf("::", StringComparison.Ordinal) > sp) key = key.Substring(sp + 1); // without the return/field type
            string by = result.UsedBy.TryGetValue(item, out string u) ? u : "";
            List<string> names = new List<string>();
            foreach (string full in by.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string n = Short(full);
                if (n != "Access") names.Add(n);
            }
            return names.Count == 0 ? key : key + " (used by " + string.Join(", ", names.ToArray()) + ")";
        }

        private static string Named(List<string> items)
        {
            List<string> l = new List<string>();
            foreach (string i in items) l.Add(Named(i));
            return string.Join("; ", l.ToArray());
        }

        // The most serious treatment among the mod's classes that use the member. Access (the start-up verifier) is
        // not a user: a member only it names (an overload sibling of what the mod calls) is Verify, which changes
        // nothing. A class the table does not list, or a manifest line with no class at all, is core.
        private static Kind KindOf(string item, out string feature)
        {
            feature = null;
            Kind worst = Kind.Verify;
            string by = result.UsedBy.TryGetValue(item, out string u) ? u : "";
            if (by.Length == 0)
            {
                return Kind.Core;
            }
            foreach (string full in by.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string n = Short(full);
                Class c = classes.TryGetValue(n, out Class k) ? k : new Class { Kind = Kind.Core };
                if (c.Kind < worst || (c.Kind == worst && feature == null))
                {
                    worst = c.Kind;
                    feature = c.Feature;
                }
            }
            return worst;
        }

        private static string Short(string fullName)
        {
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
        }

        private static List<string> Group(Dictionary<string, List<string>> d, string key)
        {
            if (!d.TryGetValue(key, out List<string> l)) d[key] = l = new List<string>();
            return l;
        }

        private static string Join(List<string> names)
        {
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
        }

        // game-code.txt: "core A B C", "frozen <feature>: A B", "warned <feature>: A B", "dev A B", "verify A"; # comments.
        private static void LoadClasses()
        {
            classes = new Dictionary<string, Class>(StringComparer.Ordinal);
            classesOfFeature = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            using (Stream s = typeof(GameCode).Assembly.GetManifestResourceStream(ResourceName))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int sp = line.IndexOf(' ');
                    string kindWord = sp < 0 ? line : line.Substring(0, sp);
                    string rest = sp < 0 ? "" : line.Substring(sp + 1).Trim();
                    Kind kind;
                    switch (kindWord)
                    {
                        case "core": kind = Kind.Core; break;
                        case "frozen": kind = Kind.Frozen; break;
                        case "warned": kind = Kind.Warned; break;
                        case "dev": kind = Kind.Dev; break;
                        case "verify": kind = Kind.Verify; break;
                        default: throw new FormatException("game-code.txt: " + line);
                    }
                    string feature = null;
                    if (kind == Kind.Frozen || kind == Kind.Warned)
                    {
                        int colon = rest.IndexOf(':');
                        if (colon < 0) throw new FormatException("game-code.txt: " + line);
                        feature = rest.Substring(0, colon).Trim();
                        rest = rest.Substring(colon + 1);
                    }
                    foreach (string n in rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        classes[n] = new Class { Kind = kind, Feature = feature };
                        if (feature != null) Group(classesOfFeature, feature).Add(n);
                    }
                }
            }
        }
    }
}
