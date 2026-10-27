using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    internal static partial class Tests
    {
        // Saddle Up's cfg as every public release wrote it (tests/fixtures/released/<version>/dreas.gk2.saddleup.cfg,
        // docs/compatibility.md), through its live page's own carry-over (renumbered sections, moved keys): every line
        // of the old file lands on a row of today's page and reads as the row's type (an enum name that changed shows
        // here). Split from the API's "upgrade" scenario on 2026-10-27 (the API's own files stay there).
        internal static IEnumerator UpgradeSaddleUpCfg()
        {
            string root = Defaults.RepoPath(System.IO.Path.Combine("..", "..", "tests", "fixtures", "released"));
            if (root == null || !System.IO.Directory.Exists(root))
            {
                Warn("tests/fixtures/released isn't on this machine (no sources): skipped");
                yield break;
            }
            string[] dirs = System.IO.Directory.GetDirectories(root);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            foreach (string dir in dirs)
            {
                string version = System.IO.Path.GetFileName(dir);

                // Saddle Up's cfg through the live page (its real sections, keys and MovedKey calls)
                string su = System.IO.Path.Combine(dir, "dreas.gk2.saddleup.cfg");
                if (System.IO.File.Exists(su))
                {
                    MountSettingsPage page = MountSettingsPage.All.Find(p => p.Config != null && string.Equals(System.IO.Path.GetFileName(p.Config.ConfigFilePath), "dreas.gk2.saddleup.cfg", StringComparison.OrdinalIgnoreCase));
                    if (page == null)
                    {
                        Warn($"{version}: GK2 Saddle Up isn't loaded, its cfg isn't checked");
                    }
                    else
                    {
                        Dictionary<string, Dictionary<string, string>> file = MountSettingsPage.ReadSections(su);
                        int n = 0;
                        foreach (var s in file) n += s.Value.Count;
                        List<MountSettingsPage.UpgradeClaim> claims = page.Upgrade(file, out List<(string Section, string Key, string Raw)> unclaimed);
                        var lost = new StringBuilder();
                        foreach (var u in unclaimed) lost.Append(lost.Length > 0 ? ", " : "").Append('[').Append(u.Section).Append("] ").Append(u.Key);
                        Check(unclaimed.Count == 0, $"{version} Saddle Up cfg: every one of its {n} values lands on a row of today's page" + (unclaimed.Count > 0 ? $"; lost: {lost}" : ""));
                        var bad = new StringBuilder();
                        int found = 0, moved = 0;
                        foreach (MountSettingsPage.UpgradeClaim c in claims)
                        {
                            if (c.Raw == null) continue;
                            found++;
                            if (c.Moved) moved++;
                            try
                            {
                                TomlTypeConverter.ConvertToValue(c.Raw, c.Row.Entry.SettingType);
                            }
                            catch (Exception e)
                            {
                                bad.Append(bad.Length > 0 ? "; " : "").Append('[').Append(c.Section).Append("] ").Append(c.Key).Append(" = ").Append(c.Raw).Append(" as ").Append(c.Row.Entry.SettingType.Name).Append(": ").Append(e.Message);
                            }
                        }
                        Check(bad.Length == 0, $"{version} Saddle Up cfg: every carried value reads as its row's type (enum names, keys)" + (bad.Length > 0 ? $": {bad}" : ""));
                        Info($"{version} Saddle Up cfg: {found} values found, {moved} of them from a moved section or key; {claims.Count - found} rows are new since");
                    }
                }
            }
        }
    }
}
