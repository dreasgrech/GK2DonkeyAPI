using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // A look's own names for the mount regions' parts (docs/second-tone.md 8; the user, 2026-11-04: every row of the
    // colours window a part of the look by its name, never "Second tone"): skin.cfg [Region.<region>] name =, translated
    // by the skin's owner as skin.<skin id>.region.<region>.name. A row shows the look's name: its own, else its
    // default's (a variant), else the donkey's (its own look, and a skin worn over it: the donkey's regions are under
    // it). A look naming none of a region, an older pack's, keeps the page's words (Coat colour, Second tone) and gets no
    // Dark or Eye row. A look may also take a region's row away (row = none; the user, 2026-11-04: "skins should have the
    // ability ... if they don't want any fields, they must have the ability to remove them"; docs/mount-colours.md 7.13):
    // the region is drawn as the look has it then, a player's value for it on this look not applied (Colors.Rig).
    internal static class SkinRegions
    {
        internal const string Prefix = "region.";

        // [Region.<region>] key = value (Looks.Parse, MountSkinDef.RegionNames / RegionsWithoutRow): name, or row = none.
        internal static void Read(Looks.SkinDef d, string region, string key, string value)
        {
            string r = Colors.RegionKeyOf(region);
            if (r == null)
            {
                d.Report += $"[Region.{region}]: no such mount region ({string.Join(", ", Colors.MountRegionKeys)}); ignored; ";
                return;
            }
            if (string.Equals(key, "row", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals((value ?? "").Trim(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    d.Report += $"[Region.{r}] row = {value}: none is its one value (the look gives the region no row), ignored; ";
                    return;
                }
                if (d.RegionsWithoutRow == null) d.RegionsWithoutRow = new HashSet<string>(StringComparer.Ordinal);
                d.RegionsWithoutRow.Add(r);
                return;
            }
            if (!string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
            {
                d.Report += $"[Region.{r}] {key}: not a region's key (name, row), ignored; ";
                return;
            }
            if (string.IsNullOrWhiteSpace(value)) return;
            if (d.RegionNames == null) d.RegionNames = new Dictionary<string, string>(StringComparer.Ordinal);
            d.RegionNames[r] = value.Trim();
        }

        internal static string KeyOf(string skinId, string region) => "skin." + skinId + ".region." + region.ToLowerInvariant() + ".name";

        // The donkey's own, in the API's words: its look in the game's art, and under a skin worn over it. Its outline is
        // in no region (docs/mount-colours.md 7.11), so the dark parts' row is its mane, ears, legs and hooves.
        private static Word DonkeyWord(string region)
        {
            switch (region)
            {
                case "Coat": return UiWords.RegionCoat;
                case "Dark": return UiWords.RegionDonkeyDark;
                case "Muzzle": return UiWords.RegionDonkeyMuzzle;
                case "Eye": return UiWords.RegionEyes;
                case "Harness": return UiWords.RegionHarness;
                default: return null;
            }
        }

        // Whether a look gives a region a row: the nearest of the look and its defaults that says decides (row = none:
        // no; a name: yes, a variant bringing back a row its default took away), none saying: yes. The donkey's own look
        // has every row; a region key that is no mount region (the cart's) has its row.
        internal static bool HasRow(string skinId, string region)
        {
            if (string.IsNullOrEmpty(skinId) || skinId.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return true;
            return HasRow(Looks.Find(skinId), region);
        }

        internal static bool HasRow(Looks.SkinDef d, string region)
        {
            for (Looks.SkinDef s = d; s != null; s = s.Parent)
            {
                if (s.RegionsWithoutRow != null && s.RegionsWithoutRow.Contains(region)) return false;
                if (s.RegionNames != null && s.RegionNames.ContainsKey(region)) return true;
            }
            return true;
        }

        // The look's name of a region's part, in the player's language; null: it names none, or gives it no row.
        internal static Term NameTerm(string skinId, string region)
        {
            string r = Colors.RegionKeyOf(region);
            if (r == null) return null;
            if (string.IsNullOrEmpty(skinId) || skinId.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return DonkeyWord(r);
            Looks.SkinDef d = Looks.Find(skinId);
            if (!HasRow(d, r)) return null;
            Looks.SkinDef root = d;
            for (Looks.SkinDef s = d; s != null; s = s.Parent)
            {
                if (s.RegionNames != null && s.RegionNames.TryGetValue(r, out string name)) return new Term(s.Owner, KeyOf(s.Id, r), name);
                root = s;
            }
            bool overDonkey = root != null && root.Over && string.Equals(root.MountId, Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            return overDonkey ? DonkeyWord(r) : null;
        }

        // The names a skin's own file gives, for its owner's English (Texts): every one it declares, by region, but for a
        // region it also gives no row (a name never shown).
        internal static IEnumerable<KeyValuePair<string, string>> Declared(Looks.SkinDef d)
        {
            if (d?.RegionNames == null) yield break;
            foreach (KeyValuePair<string, string> kv in d.RegionNames)
            {
                if (d.RegionsWithoutRow != null && d.RegionsWithoutRow.Contains(kv.Key)) continue;
                yield return kv;
            }
        }
    }
}
