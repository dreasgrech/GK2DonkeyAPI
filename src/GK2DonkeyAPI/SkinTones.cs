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
        // its own name = line (without one the name is the id, and a variant's tone keeps its default's name). Follows:
        // the mount region (Coat, Dark, Muzzle, Eye, Harness, Outline) its Game default follows (a skin's part that was under
        // that region before it was a tone: the player's saved colour of the region keeps reaching it, docs/mount-colours.md
        // 7.9); OwnFollows as the skin said it, Follows as resolved (a variant's taken from its default's tone).
        // NameFromParent: a variant's resolved tone showing its default's name (Merge), so the default translates it.
        internal sealed class ToneDef
        {
            internal string Id, Name, Follows, OwnFollows;
            internal bool NameGiven, NameFromParent;
        }

        // A tone id as kept everywhere (the loader reads section names in lower case): MountSkinTone, MountLook.Tone.
        internal static string NormalId(string id) => (id ?? "").Trim().ToLowerInvariant();

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

        // A tone of a skin, from a pack's [Tone.<id>] or a code skin's MountSkinTone: its colours under ColorRegions, the
        // tone in its order. A tone the loader can't take (a bad id, a follows = that is no mount region, a colour that
        // isn't RRGGBB or that another place names) is said in the skin's report and left out, the skin loads without it:
        // a typo in a pack's new section never costs a player the skin (the sixth code review; an unknown section never
        // did, and [Colors] keeps refusing as in 0.43). Without colours of its own it colours nothing and has no row
        // (ToneRows, MountSkinInfo leave it out), said in the report, unless it is a variant's tone of its default's id:
        // then its name renames that tone. follows: a mount region its Game default follows (null: as drawn). Its colours
        // may also be listed under that region in [Colors]: they move to the tone here, and an API without tones still
        // reads them from [Colors] as before (a released skin given tones looks the same on either API).
        internal static void Add(Looks.SkinDef d, string id, string name, string colours, string follows)
        {
            string problem = null, region = null;
            if (!ValidId(id)) problem = "its id isn't lower case letters, digits and '-' (it is part of the settings' keys)";
            else if (!string.IsNullOrWhiteSpace(follows) && (region = Colors.RegionKeyOf(follows.Trim())) == null)
                problem = $"follows = {follows} is not a mount region ({string.Join(", ", Colors.MountRegionKeys)})";
            List<string> hexes = Looks.ColourTokens(colours);
            if (problem == null) problem = ColourProblem(d, id, region, hexes);
            if (problem != null)
            {
                d.Report += $"[Tone.{id}]: {problem}; the tone is left out; ";
                return;
            }
            if (hexes.Count == 0) d.Report += $"[Tone.{id}] names no colours of its own (a variant's renames its default's tone of that id; any other colours nothing); ";
            else
            {
                // the ones its region names too move here (the compatible listing): the one-place check then holds
                if (region != null && d.ColorRegions != null && d.ColorRegions.TryGetValue(region, out HashSet<string> under))
                {
                    foreach (string hex in hexes) under.Remove(hex);
                    if (under.Count == 0) d.ColorRegions.Remove(region);
                }
                Looks.RegionColours(d, KeyOf(id)).UnionWith(hexes); // as ColourProblem checked them (never AddRegionColours' throws: a tone is left out, never the skin)
            }
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
                if (region != null) t.Follows = t.OwnFollows = region;
                return;
            }
            d.Tones.Add(new ToneDef { Id = id, Name = named ? name.Trim() : id, NameGiven = named, Follows = region, OwnFollows = region });
        }

        // What keeps a tone's colours out, or null: one that isn't RRGGBB, or one another place names (another tone, a
        // [Colors] region but the one the tone follows: that one is the compatible listing). Checked before anything is
        // moved, so a tone left out leaves the skin's regions as they were.
        private static string ColourProblem(Looks.SkinDef d, string id, string region, List<string> hexes)
        {
            string key = KeyOf(id);
            foreach (string hex in hexes)
            {
                if (!Looks.IsHexColour(hex)) return $"'{hex}' is not a colour (RRGGBB)"; // the test AddRegionColours applies (Looks.IsHexColour)
                if (d.ColorRegions == null) continue;
                foreach (KeyValuePair<string, HashSet<string>> other in d.ColorRegions)
                {
                    if (other.Key == key || other.Key == region || !other.Value.Contains(hex)) continue;
                    return $"{hex} is under {Looks.PlaceOf(other.Key)} too (one place per colour, a region or a tone)";
                }
            }
            return null;
        }

        // The skin's tones as the API shows them (MountSkinInfo.Tones): the ones with colours, made once per resolved
        // state of the skin (LooksInherit.Resolve forgets them) and shared by every MountSkinInfo of it, which is made at
        // every Find (the rows' checks among them, each frame the colours window is open).
        internal static IReadOnlyList<MountSkinTone> InfosOf(Looks.SkinDef d)
        {
            if (d == null || !d.OwnColours || d.Tones == null) return Array.Empty<MountSkinTone>();
            if (d.ToneInfos != null) return d.ToneInfos;
            var list = new List<MountSkinTone>();
            foreach (ToneDef t in d.Tones)
            {
                if (HasColours(d, t.Id)) list.Add(MountSkinTone.Listed(t.Follows, t.Id, t.Name, t.NameGiven && !t.NameFromParent, ColoursOf(d, t.Id)));
            }
            d.ToneInfos = list.AsReadOnly();
            return d.ToneInfos;
        }

        // A variant's tones (LooksInherit.Resolve): its default's, in their order, then its own, as new records every time
        // (the declared ones are never changed, so a re-resolve or a default gone leaves nothing of the last one). One of
        // its own with a default's id: its name if it gives one, else the default's (NameFromParent: a variant adding a
        // shade keeps the default's name and translation); what it follows if it says, else the default's; the colours
        // merge with the regions either way (MergeRegions). Standing alone, its own as declared.
        internal static List<ToneDef> Merge(List<ToneDef> parent, List<ToneDef> own)
        {
            if (parent == null || parent.Count == 0) return own;
            var merged = new List<ToneDef>();
            foreach (ToneDef p in parent)
            {
                ToneDef mine = Find(own, p.Id);
                bool myName = mine != null && mine.NameGiven;
                merged.Add(new ToneDef
                {
                    Id = p.Id, Name = myName ? mine.Name : p.Name, NameGiven = myName || p.NameGiven, NameFromParent = !myName,
                    Follows = mine?.OwnFollows ?? p.Follows, OwnFollows = mine?.OwnFollows,
                });
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

        // Whether a tone has any colours as the skin resolved them (a tone without any colours nothing and has no row).
        internal static bool HasColours(Looks.SkinDef d, string id) => d?.ColorRegions != null && d.ColorRegions.TryGetValue(KeyOf(id), out HashSet<string> set) && set.Count > 0;

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

        // The tone's name in the player's language: the text of the skin whose name it shows (a variant's tone with its
        // default's name is the default's), by its owner (docs/localization.md step 3).
        internal static Term NameTerm(Looks.SkinDef d, ToneDef t)
        {
            Looks.SkinDef by = t.NameFromParent && d.Parent != null ? d.Parent : d;
            return new Term(by.Owner, "skin." + by.Id + ".tone." + t.Id + ".name", t.Name);
        }

        // Whether a tone a skin declares has its name shown in a row (the English dump, Texts.EnglishOf): on that skin
        // (its resolved tone named by it, with colours), or on a variant of it that shows the name (its default's); never
        // on a skin in palette colours (no tone rows).
        internal static bool NameShown(Looks.SkinDef s, ToneDef declared)
        {
            ToneDef mine = Find(s.Tones, declared.Id);
            if (s.OwnColours && mine != null && !mine.NameFromParent && HasColours(s, declared.Id)) return true;
            foreach (Looks.SkinDef v in Looks.All)
            {
                if (v == s || v.Parent != s || !v.OwnColours) continue;
                ToneDef theirs = Find(v.Tones, declared.Id);
                if (theirs != null && theirs.NameFromParent && HasColours(v, declared.Id)) return true;
            }
            return false;
        }

        // Each ToneColor's word on the rows and its colour, in one place (the eighth code review: two lists in the enum's
        // order drifted apart silently when a preset was added to one). The colour: the tone takes its hue and saturation,
        // and its lightness against its own; null for Game (as drawn) and Custom (the player's own). A value without an
        // entry has neither (Choices refuses to build without its word).
        private static (Word Word, string Hex) Preset(ToneColor c)
        {
            switch (c)
            {
                case ToneColor.Game: return (UiWords.ToneGame, null);
                case ToneColor.Red: return (UiWords.ToneRed, "C83C3C");
                case ToneColor.Gold: return (UiWords.ToneGold, "D8A838");
                case ToneColor.Green: return (UiWords.ToneGreen, "4E8C3C");
                case ToneColor.Blue: return (UiWords.ToneBlue, "3C6EB4");
                case ToneColor.Purple: return (UiWords.TonePurple, "7A48A8");
                case ToneColor.Pink: return (UiWords.TonePink, "E8649A");
                case ToneColor.Brown: return (UiWords.ToneBrown, "7A4A2A");
                case ToneColor.Black: return (UiWords.ToneBlack, "2C2A30");
                case ToneColor.White: return (UiWords.ToneWhite, "ECE8E0");
                case ToneColor.Custom: return (UiWords.ToneCustomChoice, null);
                default: return (null, null);
            }
        }

        internal static string PresetHex(ToneColor c) => Preset(c).Hex;

        // Whether the skin, as loaded, draws any of the tone's colours (Loaded.ColoursByRegion: its pictures' colours, a
        // variant's default's frames it takes among them); true while that isn't known: not loaded and sorted yet, or a
        // sheet built before the skin was resolved again (a new def, or its default registered or reloaded since: the
        // eleventh code review). Its rows are greyed when false (MountSettingsPage.ToneRows).
        internal static bool Draws(string skinId, string toneId)
        {
            Looks.Loaded l = Looks.LoadedOwnColours(skinId);
            return l == null || l.Def != Looks.Find(skinId) || l.Parent != l.Def.Parent || (l.ColoursByRegion.TryGetValue(KeyOf(toneId), out List<Color32> cols) && cols.Count > 0);
        }

        private static Word[] choices;

        // A tone's row's choices, ToneColor's values in their order (a choice row's words go by the value's position):
        // the first one the region it follows ("As the harness", skin.cfg follows =), Game default without one.
        internal static Word[] Choices()
        {
            if (choices == null)
            {
                var all = (ToneColor[])Enum.GetValues(typeof(ToneColor));
                var words = new Word[all.Length];
                for (int i = 0; i < all.Length; i++) words[i] = Preset(all[i]).Word ?? throw new InvalidOperationException($"ToneColor.{all[i]} has no word (SkinTones.Preset)");
                choices = words;
            }
            return choices;
        }

        // How light the colour is against the tone's own art (the average value of its colours), by the custom colours'
        // rule (MountCompanion.LightnessAgainst): colorize keeps each pixel's brightness, so White would stay the art's
        // mid tone without it. The colours its pictures draw once the skin is loaded (Loaded.ColoursByRegion: a listed
        // colour no picture has, or a default's a variant doesn't draw, doesn't count), its list until then; the
        // companion applies the look again when the sheet is whole (Looks.LoadedReady). A handful of colours, nothing kept.
        // Brightness multiplies each pixel's value before the colorize (Colors.RegionMap.Apply), so it stops where the
        // tone's lightest shade reaches white: past that the lit shades would all clip to one flat colour (Gold on the
        // five navy shades: 1.62 made the two lightest one; the fourth code review, 2026-11-04). A light colour on a
        // dark tone comes out as light as the tone's shading allows.
        // A Custom colour (cap false) is the player's own lightness, as the other rows' custom colours are (the seventh
        // review: capped, its lightness row did nothing past the tone's lightest shade on a light tone).
        internal static float Lightness(string hex, Looks.SkinDef d, string id, bool cap = true)
        {
            ArtRange(d, id, out float art, out float top);
            float b = MountCompanion.LightnessAgainst(hex, art);
            return cap ? Mathf.Min(b, CapOf(top)) : b;
        }

        // The most a preset's brightness is on a tone (Lightness): where the tone's lightest shade reaches white.
        internal static float BrightnessCap(Looks.SkinDef d, string id)
        {
            ArtRange(d, id, out _, out float top);
            return CapOf(top);
        }

        private static float CapOf(float top) => top > 0.001f ? Mathf.Max(1f, 1f / top) : float.MaxValue;

        // The values of a tone's colours (Colors.ValueRange: their mean and the lightest), as its pictures draw them once
        // the skin is loaded, its list until then.
        private static void ArtRange(Looks.SkinDef d, string id, out float art, out float top)
        {
            Looks.Loaded look = d != null ? Looks.LoadedOwnColours(d.Id) : null;
            if (look != null && look.ColoursByRegion.TryGetValue(KeyOf(id), out List<Color32> drawn) && drawn.Count > 0) Colors.ValueRange(drawn, out art, out top);
            else if (d?.ColorRegions != null && d.ColorRegions.TryGetValue(KeyOf(id), out HashSet<string> set)) Colors.ValueRange(Colors.ColoursOfHexes(set), out art, out top);
            else { art = 0.5f; top = 1f; }
        }

        // A mount region's colour given as a part's (MountLook.Dark, Eye; docs/second-tone.md 8): the instance's keys
        // (<region>Colorize, ColorizeAmount, Brightness) by a tone's rule against the region's own art (Colors.ArtRange: a
        // preset stops where the region's lightest shade reaches white, Custom is the player's own lightness); null: Game,
        // as drawn (or Custom without a usable colour).
        internal static Dictionary<string, object> RegionValues(MountTone t, string region, string skinId)
        {
            if (t == null || t.Color == ToneColor.Game) return null;
            bool preset = t.Color != ToneColor.Custom;
            string hex = preset ? PresetHex(t.Color) : MountCompanion.Hex(t.Custom, region.ToLowerInvariant() + " colour");
            if (hex == null) return null;
            Colors.ArtRange(region, skinId, out float art, out float top);
            float b = MountCompanion.LightnessAgainst(hex, art);
            if (preset) b = Mathf.Min(b, CapOf(top));
            return new Dictionary<string, object> { [region + "Colorize"] = hex, [region + "ColorizeAmount"] = 1f, [region + "Brightness"] = b };
        }

        // A skin's look's tones as the engine takes them: id to a colorize colour and its brightness; null when every tone
        // is as drawn (or the skin has none). baby: a baby's own look (FoalVariety.cs), each tone given a colour in the
        // baby's shade (Shade); null: the parent's.
        internal static Dictionary<string, Colors.RegionTweak> ValuesOf(MountLook look, string skinId, FoalLooks? baby = null)
        {
            Looks.SkinDef d = Looks.Find(skinId);
            if (look == null || d?.Tones == null || !d.OwnColours) return null; // a palette skin's tones are ignored (LooksInherit.Resolve)
            Dictionary<string, Colors.RegionTweak> values = null;
            foreach (ToneDef t in d.Tones)
            {
                MountTone v = look.FindTone(t.Id);
                if (v == null || v.Color == ToneColor.Game) continue;
                // a custom colour as the regions' are read (a bad one said once in the log); Custom without a usable colour
                // is the tone as drawn, an identity value (not nothing: a tone that follows a region would follow it again)
                bool preset = v.Color != ToneColor.Custom;
                string hex = preset ? PresetHex(v.Color) : MountCompanion.Hex(v.Custom, "'" + t.Name + "' tone");
                if (values == null) values = new Dictionary<string, Colors.RegionTweak>(StringComparer.Ordinal);
                if (hex == null)
                {
                    values[t.Id] = new Colors.RegionTweak();
                    continue;
                }
                var tweak = new Colors.RegionTweak { Colorize = hex, ColorizeAmount = 1f, Brightness = Lightness(hex, d, t.Id, cap: preset) };
                if (baby.HasValue) Shade(tweak, baby.Value, preset ? BrightnessCap(d, t.Id) : float.MaxValue);
                values[t.Id] = tweak;
            }
            return values;
        }

        // A baby's own shade of a tone given a colour: its coat's, as MountCompanion.Shade shades a coat's colorize colour
        // (its hue turned and its saturation scaled, FoalLooks.ShiftHex, and its brightness stepped), so a baby reads as
        // one animal, its trappings with its coat (the user, 2026-11-04: a part's own colour "ideally" shaded per baby as
        // the coat is). A tone is colorized whole, so its colour carries the hue and the saturation: a saturation value of
        // its own would be painted over (Colors.RegionMap.Apply colorizes last, keeping each pixel's value). A preset
        // stays under the cap Lightness keeps it to: a baby lighter than that would clip the tone's lit shades to one flat
        // colour, so it comes out darker, never lighter, than the cap. A Custom colour is the player's own lightness and
        // isn't capped. Nothing new to draw: a baby has colours of its own already (its coat), so its own palette.
        private static void Shade(Colors.RegionTweak v, FoalLooks own, float cap)
        {
            if (FoalLooks.IsAlike(own.Hue, own.Brightness, own.Saturation, own.Tinge)) return;
            v.Colorize = FoalLooks.ShiftHex(v.Colorize, own.Hue, own.Saturation);
            v.Brightness = Mathf.Min(FoalLooks.StepBrightness(v.Brightness, own.Brightness), cap);
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
                if (!string.Equals(o.Colorize, kv.Value.Colorize, StringComparison.OrdinalIgnoreCase) || Mathf.Abs(o.Brightness - kv.Value.Brightness) > 0.0001f
                    || Mathf.Abs(o.ColorizeAmount - kv.Value.ColorizeAmount) > 0.0001f || Mathf.Abs(o.Saturation - kv.Value.Saturation) > 0.0001f
                    || Mathf.Abs(o.Hue - kv.Value.Hue) > 0.001f) return false; // a baby's shade moves the saturation too
            }
            return true;
        }
    }
}
