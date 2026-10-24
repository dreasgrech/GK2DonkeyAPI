using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Mount packs (the user, 2026-10-24: "i want to see things sorted by mount ... a skin can only belong to one mount";
    // docs/skins.md "What a pack is"): a mount is a folder of its own,
    //   <root>\gk2mounts\<mount>\mount.cfg      the mount: [Mount] [Stats] [Tuning] [Tuning.Ride] [Tuning.Cart]
    //                                           [Sounds] [Sounds.Ride] [Sounds.Cart] [Sounds.Baby]
    //   <root>\gk2mounts\<mount>\sounds\        the mount's sound files (every skin of it)
    //   <root>\gk2mounts\<mount>\skins\<skin>\  its looks: skin.cfg, the pictures, front\, sounds\ (that look's own)
    // A folder without mount.cfg adds skins to a mount registered elsewhere (another mod's, or the game's donkey:
    // gk2mounts\donkey\skins\...). Before this a mount was a [Mount] block inside its default skin's skin.cfg.
    internal static partial class Looks
    {
        internal const string MountCfgName = "mount.cfg";
        internal const string SkinsDirName = "skins";

        // A pack mount's files: where its mount.cfg and sounds\ are read from (a folder, or a DLL's resources).
        internal sealed class MountPack
        {
            internal string Id, Owner, DirName, Version;
            internal string Source;                    // the mount's folder on disk (a loose pack); null: embedded
            internal Assembly SourceAssembly;          // an embedded mount's DLL: its GK2MountsSourceDir says where its files are on this machine
            internal bool BuiltIn;
            internal Func<string, byte[]> ReadFile;    // its files by name ("mount.cfg", "sounds/<file>", "portrait.png")
            internal List<string> FileNames = new List<string>();
            internal long DiskStamp;
            internal string Report = "";

            internal SoundBank.FileSource SoundFiles => new SoundBank.FileSource(Owner, "mount-" + Id, Source, ReadFile, "mount '" + Id + "'");
        }

        // The keys of [Mount], in the order the file keeps them (docs/mounts-foundation-audit.md). No default_skin: a
        // mount's own look is its skins\default\ (2026-10-24).
        internal static readonly string[] MountKeys = { "id", "name", "noun", "description", "portrait", "author", "version", "cart", "foals", "flies", "jumps", "foal_scale", "speed", "cargo", "effect", "effect_when" };

        private static readonly string[] MountSections = { "mount", "stats", "tuning", "tuning.ride", "tuning.cart", "sounds", "sounds.ride", "sounds.cart", "sounds.baby" };

        // mount.cfg read into a catalogue entry (not registered). dirName: the mount's folder, its id unless [Mount] id
        // says otherwise (a pack's id must equal its folder: said in the report).
        internal static Catalog.Entry MountFromCfg(string text, MountPack p, string where)
        {
            var mount = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var stats = new List<KeyValuePair<string, string>>();
            Dictionary<string, object> kt = null, ktRide = null, ktCart = null, ks = null, ksRide = null, ksCart = null, ksBaby = null;
            var unknownSections = new List<string>();
            string section = "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[')
                {
                    int c = line.IndexOf('#');
                    if (c >= 0) line = line.Substring(0, c).Trim();
                    section = line.Trim('[', ']', ' ').ToLowerInvariant();
                    if (Array.IndexOf(MountSections, section) < 0) unknownSections.Add(section);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                int hash = value.IndexOf('#');
                if (hash >= 0) value = value.Substring(0, hash).Trim();
                switch (section)
                {
                    case "mount": mount[key] = value; break;
                    case "stats":
                        if (Catalog.ReservedStat(key)) p.Report += "[Stats] " + key + " left out: the speed and the cargo are the API's own lines (speed = and cargo = in [Mount]); ";
                        else stats.Add(new KeyValuePair<string, string>(key, value));
                        break;
                    case "tuning": (kt ?? (kt = Dict()))[key] = value; break;
                    case "tuning.ride": (ktRide ?? (ktRide = Dict()))[key] = value; break;
                    case "tuning.cart": (ktCart ?? (ktCart = Dict()))[key] = value; break;
                    case "sounds": (ks ?? (ks = Dict()))[key] = value; break;
                    case "sounds.ride": (ksRide ?? (ksRide = Dict()))[key] = value; break;
                    case "sounds.cart": (ksCart ?? (ksCart = Dict()))[key] = value; break;
                    case "sounds.baby": (ksBaby ?? (ksBaby = Dict()))[key] = value; break;
                }
            }
            if (!mount.TryGetValue("id", out string mountId) || string.IsNullOrWhiteSpace(mountId)) mountId = p.DirName;
            mountId = mountId.Trim();
            if (!mountId.Equals(p.DirName, StringComparison.OrdinalIgnoreCase)) p.Report += $"[Mount] id = {mountId} differs from its folder '{p.DirName}': the folder's name is the id by convention (its skins are found in it either way); ";
            var k = new Catalog.Entry { Id = mountId, Tuning = kt, TuningRide = ktRide, TuningCart = ktCart, Sounds = ks, SoundsRide = ksRide, SoundsCart = ksCart, SoundsBaby = ksBaby, Pack = p, FromPack = true, BuiltIn = p.BuiltIn, Owner = p.Owner };
            k.Name = mount.TryGetValue("name", out string mn) && !string.IsNullOrWhiteSpace(mn) ? mn.Trim() : k.Id;
            if (mount.TryGetValue("noun", out string mnoun) && !string.IsNullOrWhiteSpace(mnoun)) k.Noun = mnoun.Trim();
            if (mount.TryGetValue("description", out string mdesc) && !string.IsNullOrWhiteSpace(mdesc)) k.Description = mdesc.Trim();
            if (mount.TryGetValue("portrait", out string mport) && !string.IsNullOrWhiteSpace(mport)) k.Portrait = mport.Trim();
            if (mount.ContainsKey("default_skin")) p.Report += "[Mount] default_skin ignored: a mount's own look is its skins\\default\\ folder; ";
            k.Stats = stats;
            if (mount.TryGetValue("author", out string ma)) k.Author = ma;
            if (mount.TryGetValue("version", out string mv)) k.Version = mv;
            p.Version = k.Version;
            if (mount.TryGetValue("cart", out string mc)) k.Cart = !mc.Equals("false", StringComparison.OrdinalIgnoreCase);
            if (mount.TryGetValue("foals", out string mf)) k.Foals = !mf.Equals("false", StringComparison.OrdinalIgnoreCase);
            if (mount.TryGetValue("flies", out string mfl)) k.Flies = mfl.Equals("true", StringComparison.OrdinalIgnoreCase);   // off unless said
            if (mount.TryGetValue("jumps", out string mj)) k.Jumps = !mj.Equals("false", StringComparison.OrdinalIgnoreCase);   // on unless said
            if (mount.TryGetValue("foal_scale", out string ms))
            {
                if (float.TryParse(ms, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fs)) k.FoalScale = Mathf.Clamp(fs, 0.2f, 1f);
                else p.Report += "[Mount] foal_scale = " + ms + " is not a number (0.2 to 1), the babies stay 0.6; ";
            }
            if (mount.TryGetValue("speed", out string speedText))
            {
                if (float.TryParse(speedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float speedValue) && speedValue > 0f)
                {
                    if (speedValue < Catalog.SpeedMin || speedValue > Catalog.SpeedMax) p.Report += "[Mount] speed = " + speedText + " is outside 0.5 to 4, taken as " + Num(Mathf.Clamp(speedValue, Catalog.SpeedMin, Catalog.SpeedMax)) + "; ";
                    k.Speed = Mathf.Clamp(speedValue, Catalog.SpeedMin, Catalog.SpeedMax);
                }
                else p.Report += "[Mount] speed = " + speedText + " is not a number (0.5 to 4, times walking speed), the donkey's applies; ";
            }
            if (mount.TryGetValue("cargo", out string mcargo))
            {
                if (int.TryParse(mcargo.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int cargo) && cargo >= 1 && cargo <= Catalog.CargoMax) k.Cargo = cargo;
                else p.Report += "[Mount] cargo = " + mcargo + " is not a whole number 1 to " + Catalog.CargoMax + ", taken as " + Catalog.CargoMax + "; ";
            }
            if (mount.TryGetValue("effect", out string me) && !MountEffects.IsNone(me)) k.Effect = me.Trim();
            if (mount.TryGetValue("effect_when", out string mw))
            {
                if (Enum.TryParse(mw.Trim(), true, out MountEffectWhen when)) k.EffectWhen = when;
                else p.Report += "[Mount] effect_when = " + mw + " is not Off, Night or Always, taken as Always; ";
            }
            var mountUnknown = new List<string>();
            foreach (string mk in mount.Keys) if (Array.IndexOf(MountKeys, mk.ToLowerInvariant()) < 0) mountUnknown.Add(mk);
            if (mountUnknown.Count > 0) p.Report += "[Mount] unknown keys ignored: " + string.Join(", ", mountUnknown) + "; ";
            if (unknownSections.Count > 0) p.Report += "unknown sections ignored: " + string.Join(", ", unknownSections) + " (mount.cfg has [Mount], [Stats], [Tuning*] and [Sounds*]); ";
            int tuningKeys = (kt?.Count ?? 0) + (ktRide?.Count ?? 0) + (ktCart?.Count ?? 0);
            if (tuningKeys > 0) p.Report += tuningKeys + " tuning key(s); ";
            int soundLines = ReportMountSoundBlocks(k, p);
            if (soundLines > 0) p.Report += soundLines + " sound line(s); ";
            return k;
        }

        private static Dictionary<string, object> Dict() => new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ registering the tree

        // One mount folder on disk: its mount.cfg (when it has one: else its skins join the mount of that id registered
        // elsewhere), then every skins\<skin>\ with a skin.cfg. The number of skins registered.
        internal static int RegisterMountFolder(string mountDir, string owner)
        {
            string dirName = Path.GetFileName(mountDir);
            string mountId = dirName;
            Catalog.Entry declared = null;
            string cfgPath = Path.Combine(mountDir, MountCfgName);
            bool ownMount = File.Exists(cfgPath);
            if (ownMount && !dirName.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(mountDir, SkinsDirName, DefaultSkinName, "skin.cfg")))
            {
                Plugin.Log.LogWarning($"Looks: the mount pack '{mountDir}' has no {SkinsDirName}\\{DefaultSkinName}\\skin.cfg: a mount's own look is its skins\\default\\ (docs/skins.md); left out with its skins.");
                return 0;
            }
            if (ownMount)
            {
                var p = new MountPack { Owner = owner, DirName = dirName, Source = mountDir };
                p.FileNames.Add(MountCfgName);
                string soundsDir = Path.Combine(mountDir, "sounds");
                if (Directory.Exists(soundsDir)) foreach (string f in Directory.GetFiles(soundsDir)) if (SoundBlock.IsSoundFile(f)) p.FileNames.Add(SoundsKey + Path.GetFileName(f));
                foreach (string f in Directory.GetFiles(mountDir, "*.png")) p.FileNames.Add(Path.GetFileName(f));
                p.ReadFile = file =>
                {
                    string full = Path.Combine(mountDir, file.Replace('/', Path.DirectorySeparatorChar));
                    return File.Exists(full) ? File.ReadAllBytes(full) : null;
                };
                declared = RegisterMount(p, File.ReadAllText(cfgPath, Encoding.UTF8), "the folder " + mountDir);
                if (declared == null) return 0;
                mountId = declared.Id;
                p.DiskStamp = PackStamp(p);
            }
            int n = 0;
            string skinsDir = Path.Combine(mountDir, SkinsDirName);
            if (Directory.Exists(skinsDir))
            {
                string[] skinDirs = Directory.GetDirectories(skinsDir);
                // the mount's own look first, then by name: its other skins are built on it from their registration
                bool IsDefault(string dir) => Path.GetFileName(dir).Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase);
                Array.Sort(skinDirs, (a, b) => IsDefault(a) != IsDefault(b) ? (IsDefault(a) ? -1 : 1) : StringComparer.OrdinalIgnoreCase.Compare(a, b));
                foreach (string skinDir in skinDirs)
                {
                    if (!File.Exists(Path.Combine(skinDir, "skin.cfg"))) continue;
                    if (!ownMount && Path.GetFileName(skinDir).Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase) && !dirName.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Log.LogWarning($"Looks: '{skinDir}': a mount's '{DefaultSkinName}' look comes with its mount.cfg; a folder that adds skins to a mount gives them names of their own. Left out.");
                        continue;
                    }
                    try
                    {
                        RegisterPack(skinDir, owner, mountId, !ownMount);
                        n++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Looks: the skin pack '{skinDir}' could not be registered: {e.Message}");
                    }
                }
            }
            if (declared != null) NoSkinLeft(declared, owner);
            return n;
        }

        // A mount registered from its pack; null when refused (said in the log).
        internal static Catalog.Entry RegisterMount(MountPack p, string cfgText, string where)
        {
            try
            {
                if (p.DirName.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.LogWarning($"Looks: {where}: the donkey is the game's own mount; its folder holds skins only (its mount.cfg is ignored).");
                    return null;
                }
                Catalog.Entry k = MountFromCfg(cfgText, p, where);
                p.Id = k.Id;
                Catalog.Entry live = Catalog.Declare(k, where);
                Plugin.Log.LogInfo($"Looks: mount '{k.Id}' ({k.Label}) from {where}{(p.Report.Length > 0 ? ": " + p.Report : "")}.");
                return live;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: the mount pack {where} could not be registered: {e.Message}");
                return null;
            }
        }

        // A mount whose pack brought no own look that registered (its skins\default\ refused): withdrawn with its other
        // skins (a mount offered with nothing to wear showed as "donkey (not installed)", 2026-10-18; one default per
        // mount, 2026-10-24).
        private static void NoSkinLeft(Catalog.Entry k, string owner)
        {
            if (k == null || !k.FromPack || k.Owner != owner) return;
            if (Find(SkinKey(k.Id, DefaultSkinName)) != null) return;
            Plugin.Log.LogWarning($"Looks: mount '{k.Id}' has no own look that registered (its skins\\{DefaultSkinName}\\ was refused): withdrawn with its skins.");
            foreach (SkinDef sd in Catalog.SkinsOf(k)) if (sd.Owner == owner) Unregister(sd.Id, owner);
            UnregisterMount(k.Id, owner);
        }

        internal static bool UnregisterMount(string id, string owner)
        {
            Catalog.Entry k = Catalog.Find(id);
            if (k == null || !k.FromPack || k.Owner != owner) return false;
            if (k.Pack != null) SoundBank.ForgetFiles(k.Pack.SoundFiles);
            ForgetPortraits(k.Id + "(mount)");
            return Catalog.Unregister(id, owner);
        }

        // ------------------------------------------------------------------ the file: for the tuning panel

        // The mount.cfg a pack mount's values are kept in: its folder's, or an embedded mount's in its mod's sources on
        // the machine that has them (the DLL's GK2MountsSourceDir, written by a dev build); null when there is none.
        internal static string CfgPathFor(MountPack p)
        {
            if (p == null) return null;
            if (!string.IsNullOrEmpty(p.Source)) return Path.Combine(p.Source, MountCfgName);
            string dir = p.SourceAssembly != null ? Defaults.SourceDirOf(p.SourceAssembly, "GK2MountsSourceDir") : null;
            return dir != null ? Path.Combine(dir, p.DirName ?? p.Id, MountCfgName) : null;
        }

        internal static string SaveMountCfg(Catalog.Entry k) => SaveMountCfg(k, out _);

        // mount.cfg rewritten from the live mount: every section regenerated in the canonical order with the comments
        // it held kept in place (SectionNotes, as SaveCfg does for a skin), unknown sections kept as they were at the end.
        internal static string SaveMountCfg(Catalog.Entry k, out bool changed)
        {
            changed = false;
            string path = CfgPathFor(k?.Pack);
            if (path == null) return "no file to write (a code mount, or the sources aren't on this machine)";
            try
            {
                var head = new List<string>();
                var notes = new Dictionary<string, SectionNotes>(StringComparer.OrdinalIgnoreCase);
                var otherSections = new List<(string Header, List<string> Above, List<string> Lines)>();
                var pending = new List<string>();
                string section = null, nl = "\n";
                string existing = File.Exists(path) ? File.ReadAllText(path) : null;
                if (existing != null)
                {
                    if (existing.Contains("\r\n")) nl = "\r\n";
                    foreach (string raw in existing.Split('\n'))
                    {
                        string kept = raw.TrimEnd('\r').TrimEnd();
                        string line = kept.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("#"))
                        {
                            if (section == null) head.Add(kept); else pending.Add(kept);
                            continue;
                        }
                        if (line.StartsWith("["))
                        {
                            string header = line.Split('#')[0].Trim();
                            section = header.Trim('[', ']', ' ').ToLowerInvariant();
                            if (Array.IndexOf(MountSections, section) >= 0)
                            {
                                var n = new SectionNotes();
                                n.Lead.AddRange(pending);
                                notes[section] = n;
                            }
                            else otherSections.Add((header, new List<string>(pending), new List<string>()));
                            pending.Clear();
                            continue;
                        }
                        if (section != null && notes.TryGetValue(section, out SectionNotes sn))
                        {
                            sn.KeyLine(line, pending);
                            pending.Clear();
                        }
                        else if (otherSections.Count > 0)
                        {
                            otherSections[otherSections.Count - 1].Lines.AddRange(pending);
                            pending.Clear();
                            otherSections[otherSections.Count - 1].Lines.Add(kept);
                        }
                    }
                    if (section != null && notes.TryGetValue(section, out SectionNotes last)) last.Trail.AddRange(pending);
                    else if (otherSections.Count > 0) otherSections[otherSections.Count - 1].Lines.AddRange(pending);
                }
                SectionNotes NotesOf(string name) => notes.TryGetValue(name, out SectionNotes n2) ? n2 : new SectionNotes();
                var sb = new StringBuilder();
                foreach (string h in head) sb.Append(h).Append('\n');
                SectionNotes mn = NotesOf("mount");
                foreach (string l in mn.Lead) sb.Append(l).Append('\n');
                sb.Append("[Mount]\n");
                mn.Key(sb, "id", k.Id);
                mn.Key(sb, "name", k.Label);
                if (!string.IsNullOrEmpty(k.Noun)) mn.Key(sb, "noun", k.Noun);
                if (!string.IsNullOrEmpty(k.Description)) mn.Key(sb, "description", k.Description);
                if (!string.IsNullOrEmpty(k.Portrait)) mn.Key(sb, "portrait", k.Portrait);
                mn.Key(sb, "author", k.Author ?? "");
                mn.Key(sb, "version", k.Version ?? "");
                mn.Key(sb, "cart", k.Cart ? "true" : "false");
                mn.Key(sb, "foals", k.Foals ? "true" : "false");
                if (k.Flies) mn.Key(sb, "flies", "true");
                if (!k.Jumps) mn.Key(sb, "jumps", "false");
                if (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f) mn.Key(sb, "foal_scale", Num(k.FoalScale));
                if (k.Speed.HasValue) mn.Key(sb, "speed", Num(k.Speed.Value));
                if (k.Cargo.HasValue) mn.Key(sb, "cargo", k.Cargo.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(k.Effect))
                {
                    mn.Key(sb, "effect", k.Effect);
                    mn.Key(sb, "effect_when", k.EffectWhen.ToString());
                }
                foreach (string l in mn.Trail) sb.Append(l).Append('\n');
                if (k.Stats != null && k.Stats.Count > 0)
                {
                    var statLines = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var st in k.Stats) statLines[st.Key] = st.Value;
                    WriteSection(sb, "[Stats]", statLines, NotesOf("stats"));
                }
                WriteSection(sb, "[Tuning]", k.Tuning, NotesOf("tuning"));
                WriteSection(sb, "[Tuning.Ride]", k.TuningRide, NotesOf("tuning.ride"));
                WriteSection(sb, "[Tuning.Cart]", k.TuningCart, NotesOf("tuning.cart"));
                WriteSection(sb, "[Sounds]", k.Sounds, NotesOf("sounds"));
                WriteSection(sb, "[Sounds.Ride]", k.SoundsRide, NotesOf("sounds.ride"));
                WriteSection(sb, "[Sounds.Cart]", k.SoundsCart, NotesOf("sounds.cart"));
                WriteSection(sb, "[Sounds.Baby]", k.SoundsBaby, NotesOf("sounds.baby"));
                foreach ((string Header, List<string> Above, List<string> Lines) other in otherSections)
                {
                    sb.Append('\n');
                    foreach (string l in other.Above) sb.Append(l).Append('\n');
                    sb.Append(other.Header).Append('\n');
                    foreach (string l in other.Lines) sb.Append(l).Append('\n');
                }
                string text = nl == "\n" ? sb.ToString() : sb.ToString().Replace("\n", nl);
                if (File.Exists(path) && File.ReadAllText(path) == text) return null;
                File.WriteAllText(path, text, new UTF8Encoding(false));
                changed = true;
                if (k.Pack.Source != null)
                {
                    k.Pack.DiskStamp = PackStamp(k.Pack);
                    packSeen["mount|" + k.Id] = k.Pack.DiskStamp;
                    packPending.Remove("mount|" + k.Id);
                }
                Plugin.Log.LogInfo($"Looks: mount '{k.Id}' written to {path}.");
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        // mount.cfg read again: the live mount takes the file's values in place (Catalog.Redeclare), its sound files
        // and portrait read anew. Returns null, or why not.
        internal static string ReloadMountCfg(Catalog.Entry k)
        {
            MountPack p = k?.Pack;
            string path = CfgPathFor(p);
            if (path == null || !File.Exists(path)) return "no file to read";
            try
            {
                string before = p.Report;
                p.Report = "";
                Catalog.Entry fresh = MountFromCfg(File.ReadAllText(path, Encoding.UTF8), p, "reload " + path);
                if (!fresh.Id.Equals(k.Id, StringComparison.OrdinalIgnoreCase))
                {
                    p.Report = before;
                    return $"its id changed to '{fresh.Id}': a renamed mount needs a restart";
                }
                SoundBank.ForgetFiles(p.SoundFiles);
                ForgetPortraits(k.Id + "(mount)");
                Catalog.Redeclare(k, fresh);
                p.DiskStamp = PackStamp(p);
                Plugin.Log.LogInfo($"Looks: mount '{k.Id}' read again from {path}{(p.Report.Length > 0 ? ": " + p.Report : "")}.");
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        // The newest write time over a mount folder's files (0 for an embedded one).
        internal static long PackStamp(MountPack p)
        {
            if (p == null || string.IsNullOrEmpty(p.Source)) return 0;
            long newest = 0;
            foreach (string name in p.FileNames)
            {
                try
                {
                    string f = Path.Combine(p.Source, name.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(f)) newest = Math.Max(newest, File.GetLastWriteTimeUtc(f).Ticks);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return newest;
        }

        // Dev (Debug.SkinPackAutoReload): the mounts of the skins being worn, read again when their folder changed.
        private static void TickMountWatch(HashSet<Catalog.Entry> mounts)
        {
            foreach (Catalog.Entry k in mounts)
            {
                MountPack p = k.Pack;
                if (p == null || string.IsNullOrEmpty(p.Source)) continue;
                string key = "mount|" + k.Id;
                long stamp = PackStamp(p);
                if (!packSeen.TryGetValue(key, out long seen)) { seen = p.DiskStamp; packSeen[key] = seen; }
                if (stamp == seen) { packPending.Remove(key); continue; }
                if (!packPending.TryGetValue(key, out long pending) || pending != stamp) { packPending[key] = stamp; continue; }
                packPending.Remove(key);
                packSeen[key] = stamp;
                string e = ReloadMountCfg(k);
                if (e == null) Plugin.Log.LogInfo($"Looks: mount '{k.Id}' changed on disk: read again (Debug.SkinPackAutoReload).");
                else Plugin.Log.LogWarning($"Looks: mount '{k.Id}' changed on disk but could not be read again: {e}");
            }
        }
    }
}
