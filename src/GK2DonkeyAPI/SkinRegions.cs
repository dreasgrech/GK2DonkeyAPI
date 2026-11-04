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
    // Dark or Eye row.
    internal static class SkinRegions
    {
        internal const string Prefix = "region.";

        // [Region.<region>] key = value (Looks.Parse): name, nothing else.
        internal static void Read(Looks.SkinDef d, string region, string key, string value)
        {
            string r = Colors.RegionKeyOf(region);
            if (r == null)
            {
                d.Report += $"[Region.{region}]: no such mount region ({string.Join(", ", Colors.MountRegionKeys)}); ignored; ";
                return;
            }
            if (!string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
            {
                d.Report += $"[Region.{r}] {key}: not a region's key (name), ignored; ";
                return;
            }
            if (string.IsNullOrWhiteSpace(value)) return;
            if (d.RegionNames == null) d.RegionNames = new Dictionary<string, string>(StringComparer.Ordinal);
            d.RegionNames[r] = value.Trim();
        }

        internal static string KeyOf(string skinId, string region) => "skin." + skinId + ".region." + region.ToLowerInvariant() + ".name";

        // The donkey's own, in the API's words: its look in the game's art, and under a skin worn over it. Its outline
        // shares its legs', mane's and ears' colours in the game's art, so they are one part.
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

        // The look's name of a region's part, in the player's language; null: it names none.
        internal static Term NameTerm(string skinId, string region)
        {
            string r = Colors.RegionKeyOf(region);
            if (r == null) return null;
            if (string.IsNullOrEmpty(skinId) || skinId.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return DonkeyWord(r);
            Looks.SkinDef d = Looks.Find(skinId);
            Looks.SkinDef root = d;
            for (Looks.SkinDef s = d; s != null; s = s.Parent)
            {
                if (s.RegionNames != null && s.RegionNames.TryGetValue(r, out string name)) return new Term(s.Owner, KeyOf(s.Id, r), name);
                root = s;
            }
            bool overDonkey = root != null && root.Over && string.Equals(root.MountId, Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase);
            return overDonkey ? DonkeyWord(r) : null;
        }

        // The names a skin's own file gives, for its owner's English (Texts): every one it declares, by region.
        internal static IEnumerable<KeyValuePair<string, string>> Declared(Looks.SkinDef d) =>
            d?.RegionNames ?? (IEnumerable<KeyValuePair<string, string>>)Array.Empty<KeyValuePair<string, string>>();
    }
}
