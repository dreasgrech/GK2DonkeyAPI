using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace GK2DonkeyAPI
{
    // The developer tools (the tuning panel and everything on it, F5, the tuning keys, the nudge keys, the perf log) are
    // in every build, but a player never meets them: their [Debug] keys go into the cfg only when the tools are
    // unlocked, by the sources on this machine (the build records the folder, Defaults.cs) or by the marker file
    // BepInEx\config\dreas.gk2.dev (an empty file; one marker for every dreas.gk2 mod, Move Building reads the same
    // one). Locked, the same keys bind into a scratch config that is never written: every read works and answers its
    // default, the player's cfg shows no [Debug] section, and none of the keys does anything (the release audit of
    // 2026-10-13: the Debug tab spawns items, zombies and deliveries, and the self-tests move things, so a player
    // who finds the switch in the cfg must not be able to flip it).
    internal static class DevTools
    {
        public const string MarkerName = "dreas.gk2.dev";
        public static string MarkerPath => Path.Combine(Paths.ConfigPath, MarkerName);

        public static bool Unlocked { get; private set; }

        // For the log and the panel's Sources tab: what unlocked the tools.
        public static string How { get; private set; } = "locked";

        private static ConfigFile scratch;

        // The config the [Debug] keys bind into: the mod's own when unlocked, else the scratch one (a path that never
        // exists: the ConfigFile reads a file only when it is there, and SaveOnConfigSet off means it never writes one).
        public static ConfigFile DebugConfig(ConfigFile own)
        {
            bool sources = Defaults.RepoPath(Defaults.FileName) != null;
            bool marker = false;
            try
            {
                marker = File.Exists(MarkerPath);
            }
            catch
            {
            }
            Unlocked = sources || marker;
            How = sources ? "the sources on this machine" : marker ? "the marker file " + MarkerPath : "locked";
            if (Unlocked)
            {
                return own;
            }
            scratch = new ConfigFile(Path.Combine(Paths.ConfigPath, "dreas.gk2.donkeyapi.locked-debug.cfg"), false) { SaveOnConfigSet = false };
            return scratch;
        }
    }
}
