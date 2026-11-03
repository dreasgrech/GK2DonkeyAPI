using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A skin's own tones (docs/mount-colours.md section 7; the user, 2026-11-03: "the skin can introduce its own color
    // tones depending on what it needs"): parts of its drawing the player colours apart from the five mount regions,
    // each an id, a name for its row and the exact colours of its pictures. A tone's colours sit in the skin's
    // ColorRegions under "tone.<id>", so the one-place-per-colour check, a variant's merge (LooksInherit.MergeRegions)
    // and the colour collection (Loaded.SortColours) treat them as regions; the engine pairs them after the five
    // (Colors.Rig.Rebuild) with the instance's value (Mount.Instance.Tones), which the companion sets from the skin's
    // look (MountLook.Tone, ApplyLook).
    internal static class SkinTones
    {
        internal const string Prefix = "tone.";

        // A tone as a skin declares it, in its file's order; its colours are the skin's ColorRegions[KeyOf(Id)]. NameGiven:
        // its own name = line (without one the name is the id, and a variant's tone keeps its default's name).
        internal sealed class ToneDef
        {
            internal string Id, Name;
            internal bool NameGiven;
        }

        internal static string KeyOf(string id) => Prefix + id;

        internal static bool IsTone(string regionKey) => regionKey != null && regionKey.StartsWith(Prefix, StringComparison.Ordinal);

        // Lower case letters, digits and '-': the id is part of the settings' keys ("Tone.<id>_<skin>"), where '_'
        // starts the skin's part (MountSettingsPage.LightnessKey splits there).
        internal static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 40) return false;
            foreach (char c in id)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-') return false;
            }
            return true;
        }

        // A tone of a skin, from a pack's [Tone.<id>] or a code skin's MountSkinTone: its colours under ColorRegions
        // (a colour named twice, here or under [Colors], is a format error, as a bad [Colors] line is), the tone in its
        // order. Without colours of its own it colours nothing and has no row (ToneRows, MountSkinInfo leave it out),
        // said in the report, unless it is a variant's tone of its default's id: then its name renames that tone.
        internal static void Add(Looks.SkinDef d, string id, string name, string colours, string where)
        {
            if (!ValidId(id)) throw new FormatException($"{where}: [Tone.{id}]: a tone's id is lower case letters, digits and '-' (it is part of the settings' keys)");
            if (string.IsNullOrWhiteSpace(colours)) d.Report += $"[Tone.{id}] names no colours of its own (a variant's renames its default's tone of that id; any other colours nothing); ";
            else Looks.AddRegionColours(d, KeyOf(id), "[Tone." + id + "]", colours, where);
            bool named = !string.IsNullOrWhiteSpace(name);
            if (d.Tones == null) d.Tones = new List<ToneDef>();
            foreach (ToneDef t in d.Tones)
            {
                if (t.Id != id) continue;
                if (named) // declared twice: one tone, the colours of both, the last name
                {
                    t.Name = name.Trim();
                    t.NameGiven = true;
                }
                return;
            }
            d.Tones.Add(new ToneDef { Id = id, Name = named ? name.Trim() : id, NameGiven = named });
        }

        // A variant's tones (LooksInherit.Resolve): its default's, in their order, then its own. One of its own with a
        // default's id: with a name of its own it takes that tone's place (its name, its translation), without one the
        // default's tone stays (a variant adding a shade keeps the default's name); the colours merge with the
        // regions either way (MergeRegions).
        internal static List<ToneDef> Merge(List<ToneDef> parent, List<ToneDef> own)
        {
            if (parent == null || parent.Count == 0) return own;
            var merged = new List<ToneDef>();
            foreach (ToneDef p in parent)
            {
                ToneDef mine = Find(own, p.Id);
                merged.Add(mine != null && mine.NameGiven ? mine : p);
            }
            if (own != null)
            {
                foreach (ToneDef t in own)
                {
                    if (Find(merged, t.Id) == null) merged.Add(t);
                }
            }
            return merged;
        }

        private static ToneDef Find(List<ToneDef> list, string id)
        {
            if (list == null) return null;
            foreach (ToneDef t in list)
            {
                if (t.Id == id) return t;
            }
            return null;
        }

        // A tone's colours as the skin resolved them (sorted, upper case).
        internal static List<string> ColoursOf(Looks.SkinDef d, string id)
        {
            var list = new List<string>();
            if (d?.ColorRegions != null && d.ColorRegions.TryGetValue(KeyOf(id), out HashSet<string> set)) list.AddRange(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // The tones a skin declares itself (a variant's own, not the ones it takes from its default; LooksInherit.Resolve
        // keeps them apart once it has run for it): the ones its owner translates.
        internal static List<ToneDef> DeclaredBy(Looks.SkinDef d) => d.AloneValues != null ? d.OwnTones : d.Tones;

        // The tone's name in the player's language: the text of the skin that declares it (a variant's tone taken from its
        // default is the default's), by its owner (docs/localization.md step 3).
        internal static Term NameTerm(Looks.SkinDef d, ToneDef t)
        {
            List<ToneDef> own = DeclaredBy(d);
            Looks.SkinDef by = d.Parent != null && (own == null || !own.Contains(t)) ? d.Parent : d;
            return new Term(by.Owner, "skin." + by.Id + ".tone." + t.Id + ".name", t.Name);
        }

        // The presets' colours (ToneColor): the tone takes their hue and saturation, and their lightness against its own.
        internal static string PresetHex(ToneColor c)
        {
            switch (c)
            {
                case ToneColor.Red: return "C83C3C";
                case ToneColor.Gold: return "D8A838";
                case ToneColor.Green: return "4E8C3C";
                case ToneColor.Blue: return "3C6EB4";
                case ToneColor.Purple: return "7A48A8";
                case ToneColor.Pink: return "E8649A";
                case ToneColor.Brown: return "7A4A2A";
                case ToneColor.Black: return "2C2A30";
                case ToneColor.White: return "ECE8E0";
                default: return null;
            }
        }

        // How light the colour is against the tone's own art (the average value of its colours), by the custom colours'
        // rule (MountCompanion.LightnessAgainst): colorize keeps each pixel's brightness, so White would stay the art's
        // mid tone without it. The colours its pictures draw once the skin is loaded (Loaded.ColoursByRegion: a listed
        // colour no picture has, or a default's a variant doesn't draw, doesn't count), its list until then; the
        // companion applies the look again when the sheet is whole (Looks.LoadedReady). A handful of colours, nothing kept.
        internal static float Lightness(string hex, Looks.SkinDef d, string id)
        {
            float sum = 0f;
            int n = 0;
            Looks.Loaded look = d != null ? Looks.LoadedOwnColours(d.Id) : null;
            if (look != null && look.ColoursByRegion.TryGetValue(KeyOf(id), out List<Color32> drawn) && drawn.Count > 0)
            {
                foreach (Color32 a in drawn)
                {
                    Color.RGBToHSV(a, out _, out _, out float av);
                    sum += av;
                    n++;
                }
            }
            else if (d?.ColorRegions != null && d.ColorRegions.TryGetValue(KeyOf(id), out HashSet<string> set))
            {
                foreach (string h in set)
                {
                    if (!Colors.TryHex(h, out Color32 a)) continue;
                    Color.RGBToHSV(a, out _, out _, out float av);
                    sum += av;
                    n++;
                }
            }
            return MountCompanion.LightnessAgainst(hex, n > 0 ? sum / n : 0.5f);
        }

        // A skin's look's tones as the engine takes them: id to a colorize colour and its brightness; null when every tone
        // is as drawn (or the skin has none).
        internal static Dictionary<string, Colors.RegionTweak> ValuesOf(MountLook look, string skinId)
        {
            Looks.SkinDef d = Looks.Find(skinId);
            if (look == null || d?.Tones == null) return null;
            Dictionary<string, Colors.RegionTweak> values = null;
            foreach (ToneDef t in d.Tones)
            {
                MountTone v = look.FindTone(t.Id);
                if (v == null || v.Color == ToneColor.Game) continue;
                // a custom colour as the regions' are read (a bad one said once in the log, the tone as drawn)
                string hex = v.Color == ToneColor.Custom ? MountCompanion.Hex(v.Custom, "'" + t.Name + "' tone") : PresetHex(v.Color);
                if (hex == null) continue;
                if (values == null) values = new Dictionary<string, Colors.RegionTweak>(StringComparer.Ordinal);
                values[t.Id] = new Colors.RegionTweak { Colorize = hex, ColorizeAmount = 1f, Brightness = Lightness(hex, d, t.Id) };
            }
            return values;
        }

        private static readonly HashSet<string> refusedSaid = new HashSet<string>(StringComparer.Ordinal); // "<instance>|<owner>"

        // A skin's tones (ValuesOf) onto an instance, as the owner's (the companion's id), for the skin they were made
        // for: the engine maps them only while that skin is worn (Colors.Rig.NamedMaps), so a skin changing under them
        // (another sheet still building, another look) never takes another skin's colours. Its colours rebuilt when
        // they changed. A value belongs to the owner that set it, as an override does (InstanceTuning.MayReplace):
        // another owner's tones are left to it (said once), and they go with their owner's overrides
        // (InstanceTuning.ClearOwner, Reset: Mounts.ClearTuning).
        internal static void Apply(Mount.Instance i, Dictionary<string, Colors.RegionTweak> tones, string owner, string skinId)
        {
            if (i == null) return;
            if (i.TonesOwner != null && i.TonesOwner != owner)
            {
                if (refusedSaid.Add(i.Id + "|" + owner)) Plugin.Log.LogInfo($"Looks: {owner} wants to set a skin's tones on {i.Describe()}, which {i.TonesOwner} has set; left to it (a value belongs to the owner that set it).");
                return;
            }
            string skin = tones == null ? null : Looks.Find(skinId)?.Id ?? skinId;
            i.TonesOwner = tones == null ? null : owner;
            if (Same(i.Tones, tones) && string.Equals(i.TonesSkin, skin, StringComparison.OrdinalIgnoreCase)) return;
            i.Tones = tones;
            i.TonesSkin = skin;
            i.ColorsRig.MarkDirty();
        }

        // The owner's tones off an instance (or anyone's: ownerId null), as its overrides go.
        internal static void Clear(Mount.Instance i, string ownerId)
        {
            if (i?.Tones == null || (ownerId != null && i.TonesOwner != ownerId)) return;
            i.Tones = null;
            i.TonesOwner = null;
            i.TonesSkin = null;
            i.ColorsRig.MarkDirty();
        }

        private static bool Same(Dictionary<string, Colors.RegionTweak> a, Dictionary<string, Colors.RegionTweak> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            if (na == 0) return true;
            foreach (KeyValuePair<string, Colors.RegionTweak> kv in a)
            {
                if (!b.TryGetValue(kv.Key, out Colors.RegionTweak o)) return false;
                if (!string.Equals(o.Colorize, kv.Value.Colorize, StringComparison.OrdinalIgnoreCase) || Mathf.Abs(o.Brightness - kv.Value.Brightness) > 0.0001f || Mathf.Abs(o.ColorizeAmount - kv.Value.ColorizeAmount) > 0.0001f) return false;
            }
            return true;
        }
    }
}
