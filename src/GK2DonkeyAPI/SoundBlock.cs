using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // What one layer says about one event (or event.variant) of the sound catalogue: a [Sounds] block's lines for it
    // (docs/mount-sounds-design.md section 4). A null field is "not said": the layer below keeps its value. The
    // source is one of: files of the pack's sounds\ folder, a game sound id, "none" (silent) or "ground" (a step by the
    // ground under the mount).
    internal sealed class SoundLine
    {
        internal string Key;                 // "voice", "step.grass"
        internal string BankKey;             // the bank's key for this line's files: "<section>:<key>" ("sounds.cart:voice"), so each block's files are an entry of their own
        internal string Owner;               // a mod's override line: the mod (its clips are registered under it)
        internal string Source;              // a game id, "none", "ground"; null when files or not said
        internal string[] Files;             // the pack's files (names in Folder) when the source is files
        internal string Folder = Looks.SoundsKey; // the folder key of Files: "sounds/" (a skin's own) or "sounds/mount/" (its mount's)
        internal AudioClip[] Clips;          // a code mount's or skin's own clips (the API)
        internal float? Volume, Pitch, PitchVariation, Start;
        internal bool? Loop, Speech;
        internal Dictionary<string, float> Numbers;
        internal string BankId;              // once the files or clips are registered with the bank (SoundBank.ForSkin)
        internal bool HasSource => Source != null || Files != null || Clips != null;

        internal SoundLine Copy()
        {
            return new SoundLine
            {
                Key = Key, BankKey = BankKey, Owner = Owner, Source = Source, Files = Files, Folder = Folder, Clips = Clips, Volume = Volume, Pitch = Pitch, PitchVariation = PitchVariation, Start = Start,
                Loop = Loop, Speech = Speech, Numbers = Numbers != null ? new Dictionary<string, float>(Numbers, StringComparer.OrdinalIgnoreCase) : null, BankId = BankId,
            };
        }
    }

    // A [Sounds] block parsed: its lines by event or event.variant. The text form (a pack's skin.cfg, the Mounts tab):
    //   voice = squeak_1.ogg, squeak_2.ogg    files of the pack's sounds\ folder (one at random per play)
    //   voice = voice_donkey                  a game sound id (id#n: only clip n)
    //   lips = none                           silent, whatever the layer below says
    //   step = ground                         the game's step sound for the ground under the mount
    //   step.grass = step_grass               a variant (the surfaces for step)
    //   voice.volume = 0.8                    x the player's; voice.pitch, voice.pitch_variation, voice.start (seconds),
    //                                         voice.loop (true/false, a stoppable event), voice.channel (sfx or speech)
    //   voice.idle_every = 25                 the event's own numbers (SoundEvents.Def.Numbers)
    internal sealed class SoundBlock
    {
        internal readonly Dictionary<string, SoundLine> Lines = new Dictionary<string, SoundLine>(StringComparer.OrdinalIgnoreCase);

        internal int Count => Lines.Count;

        internal SoundLine Get(string eventId, string variant)
        {
            if (variant != null && Lines.TryGetValue(eventId + "." + variant, out SoundLine v)) return v;
            return Lines.TryGetValue(eventId, out SoundLine l) ? l : null;
        }

        private static readonly string[] Attributes = { "volume", "pitch", "pitch_variation", "start", "loop", "channel" };

        internal static bool IsSoundFile(string s)
        {
            return s != null && (s.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
        }

        // The block from its raw lines. fileNames: the pack's files ("sounds/<file>" among them) to check a file line
        // against; null: no check. report: a problem, one sentence (the loader's report); the line is dropped. folder:
        // where its file lines read from (Looks.SoundsKey: the sounds\ folder of the skin or of the mount folder).
        internal static SoundBlock Parse(IEnumerable<KeyValuePair<string, string>> raw, string section, ICollection<string> fileNames, Action<string> report, string folder = Looks.SoundsKey)
        {
            var b = new SoundBlock();
            if (raw == null) return b;
            foreach (KeyValuePair<string, string> kv in raw)
            {
                string key = kv.Key.Trim(), value = (kv.Value ?? "").Trim();
                string[] parts = key.Split('.');
                SoundEvents.Def d = SoundEvents.Find(parts[0]);
                if (d == null)
                {
                    report?.Invoke($"[{section}] {key}: no sound event '{parts[0]}' (the events: {string.Join(", ", Ids())})");
                    continue;
                }
                string lineKey = d.Id, attr = null;
                if (parts.Length == 2)
                {
                    if (SoundEvents.IsVariant(d, parts[1])) lineKey = d.Id + "." + parts[1].ToLowerInvariant();
                    else attr = parts[1];
                }
                else if (parts.Length == 3)
                {
                    if (!SoundEvents.IsVariant(d, parts[1]))
                    {
                        report?.Invoke($"[{section}] {key}: '{parts[1]}' is no variant of {d.Id}{(d.Variants.Length > 0 ? " (" + string.Join(", ", d.Variants) + ")" : " (it has none)")}");
                        continue;
                    }
                    lineKey = d.Id + "." + parts[1].ToLowerInvariant();
                    attr = parts[2];
                }
                else if (parts.Length > 3)
                {
                    report?.Invoke($"[{section}] {key}: not an event, event.variant or event.attribute");
                    continue;
                }
                if (!b.Lines.TryGetValue(lineKey, out SoundLine l)) b.Lines[lineKey] = l = new SoundLine { Key = lineKey, BankKey = section.ToLowerInvariant() + ":" + lineKey, Folder = folder };
                if (attr == null)
                {
                    SetSource(l, value, section, key, fileNames, report, folder);
                    continue;
                }
                string a = attr.ToLowerInvariant();
                switch (a)
                {
                    case "volume": l.Volume = Num(value, section, key, 0f, 3f, report); break;
                    case "pitch": l.Pitch = Num(value, section, key, 0.1f, 3f, report); break;
                    case "pitch_variation": l.PitchVariation = Num(value, section, key, 0f, 1f, report); break;
                    case "start": l.Start = Num(value, section, key, 0f, 60f, report); break;
                    case "loop":
                        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase)) l.Loop = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else report?.Invoke($"[{section}] {key} = {value}: true or false");
                        break;
                    case "channel":
                        if (value.Equals("speech", StringComparison.OrdinalIgnoreCase) || value.Equals("sfx", StringComparison.OrdinalIgnoreCase)) l.Speech = value.Equals("speech", StringComparison.OrdinalIgnoreCase);
                        else report?.Invoke($"[{section}] {key} = {value}: sfx or speech");
                        break;
                    default:
                        if (Array.FindIndex(d.Numbers, n => n.Equals(a, StringComparison.OrdinalIgnoreCase)) >= 0)
                        {
                            float? n = Num(value, section, key, float.MinValue, float.MaxValue, report);
                            if (n.HasValue) (l.Numbers ?? (l.Numbers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[a] = n.Value;
                        }
                        else report?.Invoke($"[{section}] {key}: '{attr}' is neither an attribute ({string.Join(", ", Attributes)}) nor a number of {d.Id}{(d.Numbers.Length > 0 ? " (" + string.Join(", ", d.Numbers) + ")" : " (it has none)")}");
                        break;
                }
            }
            return b;
        }

        // A code skin's or mount's sounds (the API's MountSound by event key), as one block; a bad key is logged once.
        internal static SoundBlock FromApi(IDictionary<string, Api.MountSound> sounds, string section, string who)
        {
            var b = new SoundBlock();
            if (sounds == null) return b;
            foreach (KeyValuePair<string, Api.MountSound> kv in sounds)
            {
                if (kv.Value == null) continue;
                SoundEvents.Def d = SoundEvents.Split(kv.Key, out string variant);
                if (d == null || variant != null && !SoundEvents.IsVariant(d, variant))
                {
                    Plugin.Log.LogWarning($"Sounds: '{who}': [{section}] {kv.Key} is no sound event (or variant); ignored");
                    continue;
                }
                string key = variant != null ? d.Id + "." + variant.ToLowerInvariant() : d.Id;
                b.Lines[key] = kv.Value.ToLine(key, section.ToLowerInvariant() + ":" + key, null);
            }
            return b;
        }

        private static IEnumerable<string> Ids()
        {
            foreach (SoundEvents.Def d in SoundEvents.All) yield return d.Id;
        }

        private static float? Num(string value, string section, string key, float min, float max, Action<string> report)
        {
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0f;
            if (Numbers.TryFloat(value, out float f)) return Mathf.Clamp(f, min, max);
            report?.Invoke($"[{section}] {key} = {value}: not a number");
            return null;
        }

        internal static string FolderText(string folder) => folder.Replace('/', '\\'); // "sounds\\mount\\" for the reports

        private static void SetSource(SoundLine l, string value, string section, string key, ICollection<string> fileNames, Action<string> report, string folder)
        {
            if (value.Length == 0)
            {
                report?.Invoke($"[{section}] {key}: empty (a game id, files, none or ground)");
                return;
            }
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) { l.Source = "none"; l.Files = null; return; }
            if (value.Equals("ground", StringComparison.OrdinalIgnoreCase)) { l.Source = "ground"; l.Files = null; return; }
            string[] toks = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < toks.Length; i++) toks[i] = toks[i].Trim();
            bool anyFile = Array.Exists(toks, IsSoundFile);
            if (!anyFile)
            {
                if (toks.Length != 1) report?.Invoke($"[{section}] {key} = {value}: one game id, or files (.ogg, .wav)");
                l.Source = toks[0];
                l.Files = null;
                return;
            }
            var files = new List<string>();
            foreach (string t in toks)
            {
                if (!IsSoundFile(t)) { report?.Invoke($"[{section}] {key}: '{t}' is not a sound file (.ogg or .wav); left out"); continue; }
                if (fileNames != null && !fileNames.Contains(folder + t)) { report?.Invoke($"[{section}] {key}: the pack has no {FolderText(folder)}{t}; left out"); continue; }
                files.Add(t);
            }
            if (files.Count == 0)
            {
                report?.Invoke($"[{section}] {key}: none of its files is in the pack's {FolderText(folder)} folder; the layer below applies");
                return;
            }
            l.Source = null;
            l.Files = files.ToArray();
        }

        // The convention: <folder><event>[.<variant>][_<n>].ogg|wav with no cfg line for that key is that key's files
        // (voice_1.ogg, voice_2.ogg; step.grass_1.wav). A key the cfg gives a source keeps the cfg's. The folder's own
        // files only, never a subfolder's.
        internal void AddConvention(IEnumerable<string> fileNames, string section, Action<string> report, string folder = Looks.SoundsKey)
        {
            if (fileNames == null) return;
            var byKey = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in fileNames)
            {
                if (!f.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || !IsSoundFile(f)) continue;
                string name = f.Substring(folder.Length);
                if (name.IndexOf('/') >= 0) continue; // a subfolder's
                string stem = name.Substring(0, name.LastIndexOf('.'));
                int us = stem.LastIndexOf('_');
                if (us > 0 && int.TryParse(stem.Substring(us + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) stem = stem.Substring(0, us);
                SoundEvents.Def d = SoundEvents.Split(stem, out string variant);
                if (d == null || variant != null && !SoundEvents.IsVariant(d, variant))
                {
                    report?.Invoke($"{FolderText(folder)}{name}: '{stem}' names no sound event (or variant), so it is not played by convention; name it <event>[.<variant>][_<n>] or line it up in [{section}]");
                    continue;
                }
                string key = variant != null ? d.Id + "." + variant.ToLowerInvariant() : d.Id;
                if (!byKey.TryGetValue(key, out List<string> list)) byKey[key] = list = new List<string>();
                list.Add(name);
            }
            foreach (KeyValuePair<string, List<string>> kv in byKey)
            {
                if (Lines.TryGetValue(kv.Key, out SoundLine l) && l.HasSource) continue;
                if (l == null) Lines[kv.Key] = l = new SoundLine { Key = kv.Key, BankKey = section.ToLowerInvariant() + ":" + kv.Key, Folder = folder };
                kv.Value.Sort(StringComparer.OrdinalIgnoreCase);
                l.Files = kv.Value.ToArray();
                l.Folder = folder;
                l.Source = null;
            }
        }
    }
}
