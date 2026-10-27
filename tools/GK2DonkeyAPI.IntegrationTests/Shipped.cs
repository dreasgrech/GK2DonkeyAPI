using System;
using System.Collections.Generic;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Our mounts and skins by their short names, and which of our packs a scenario needs (moved from the API's
    // SelfTest.cs, 2026-10-27: the API knows none of them).
    internal static partial class Tests
    {
        // Our shipped mounts by the name after their prefix: Saddle Up's built-in Gerry, and its external packs' (packs/,
        // the ids by the pack since 2026-10-26: saddleup-dragon.dragon, saddleup-critters.rat ...).
        internal static readonly Dictionary<string, string> ShippedMounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dragon"] = "saddleup-dragon.dragon", ["rat"] = "saddleup-critters.rat", ["snail"] = "saddleup-critters.snail",
            ["worm"] = "saddleup-critters.worm", ["gerry"] = "saddleup.gerry",
        };

        // The donkey's skins of the Donkey Skins pack (packs/SaddleUpDonkeySkins, 2026-10-27; Saddle Up's before), by name.
        internal static readonly Dictionary<string, string> ShippedDonkeySkins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hellfire"] = "saddleup-donkey-skins.hellfire", ["royal-parade"] = "saddleup-donkey-skins.royal-parade",
        };

        // The dragon's body raised on longer legs (its pictures): its seat 6 px above the donkey's.
        internal const float DragonRaisePx = 6f;

        // Which of our packs a scenario needs is not installed (the user, 2026-10-27: "have we done tests without loading
        // the extra mounts dlls"): a player may have Saddle Up alone, or one pack. Names: our mounts' short names (Gerry is
        // Saddle Up's own) and the donkey skins'. Null: all there; else the skip line naming the pack.
        internal static string MissingPack(params string[] names)
        {
            foreach (string m in names)
            {
                bool skin = ShippedDonkeySkins.ContainsKey(m);
                if (skin ? Looks.Find(Sk(m)) != null : Catalog.Find(M(m)) != null) continue;
                string pack = skin ? "GK2 Saddle Up: Donkey Skins" : m == "dragon" ? "GK2 Saddle Up: Dragon" : m == "gerry" ? "GK2 Saddle Up" : "GK2 Saddle Up: Critters";
                return $"{pack} isn't installed (no {m}): this needs it";
            }
            return null;
        }

        // A shipped mount's id by its short name: "dragon" -> "saddleup-dragon.dragon"; "donkey" and any other id as it is.
        internal static string M(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return ShippedMounts.TryGetValue(name.Trim(), out string id) ? id : name;
        }

        // A shipped skin's full name: "dragon" -> "saddleup-dragon.dragon/default" (every mount's own look is its
        // skins\default\), the same for the rat, snail, worm and Gerry; "hellfire" -> "donkey/saddleup-donkey-skins.hellfire",
        // "royal-parade" the same; "" and a full name as they are. A plain string map: it holds with nothing registered.
        internal static string Sk(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('/') >= 0) return name;
            string n = name.Trim().ToLowerInvariant();
            if (ShippedDonkeySkins.TryGetValue(n, out string skin)) return Looks.SkinKey(Catalog.DonkeyId, skin);
            int dot = n.IndexOf('.');
            if (dot > 0 && ShippedMounts.ContainsKey(n.Substring(dot + 1))) n = n.Substring(dot + 1); // saddleup-dragon.dragon
            return ShippedMounts.TryGetValue(n, out string id) ? Looks.SkinKey(id, Looks.DefaultSkinName) : name;
        }
    }
}
