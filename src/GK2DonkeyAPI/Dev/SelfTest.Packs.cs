using System;
using System.Collections;
using System.IO;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Built in or external (the user, 2026-10-26; docs/making-a-mount.md "Built in or external"): Saddle Up's Gerry and
    // its donkey skins come from its DLL; the dragon, rat, snail and worm from their packs in the game's plugins folder
    // (packs/SaddleUpDragon, packs/SaddleUpCritters, linked by the dev build), each under the pack's own owner and with
    // its ids by the pack; the old ids of a dev's own world find the new mounts (Catalog.renamedMounts).
    internal static partial class SelfTest
    {
        private static IEnumerator ExternalPacks()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview)");
                yield break;
            }
            var packOf = new[] { ("dragon", "SaddleUpDragon"), ("rat", "SaddleUpCritters"), ("snail", "SaddleUpCritters"), ("worm", "SaddleUpCritters") };
            foreach ((string name, string pack) in packOf)
            {
                Catalog.Entry k = Catalog.Find(M(name));
                if (k == null)
                {
                    Skip($"the pack {pack} is not in the game ({M(name)} isn't registered): tools/packs.py link, or build Saddle Up with the game closed");
                    yield break;
                }
                string source = k.Pack?.Source ?? "";
                Check(k.Owner == Plugin.Guid + ".packs." + pack && source.IndexOf(Path.Combine("plugins", pack, Looks.PackDirName), StringComparison.OrdinalIgnoreCase) >= 0 && k.Pack?.SourceAssembly == null,
                    $"{M(name)}: from the pack {pack} in the game's plugins folder (owner {k.Owner}; {source})");
                Check(!string.IsNullOrEmpty(k.Needs), $"{M(name)}: its mount.cfg says the API it needs (needs = {k.Needs})");
                Looks.SkinDef own = Looks.Find(Sk(name));
                Check(own != null && own.Owner == k.Owner && own.Source != null, $"{Sk(name)}: its own look from the same pack ({own?.Owner})");
            }
            Catalog.Entry gerry = Catalog.Find(M("gerry"));
            Check(gerry != null && gerry.Pack?.Source == null && gerry.Pack?.SourceAssembly?.GetName().Name == "GK2SaddleUp",
                $"Gerry: built into Saddle Up's DLL ({gerry?.Pack?.SourceAssembly?.GetName().Name ?? "none"})");
            foreach (string skin in new[] { Sk("hellfire"), Sk("royal-parade") })
            {
                Looks.SkinDef d = Looks.Find(skin);
                Check(d != null && d.Source == null && d.SourceAssembly?.GetName().Name == "GK2SaddleUp", $"{skin}: built into Saddle Up's DLL");
            }
            // the dev's own world from before (never released): the old ids find the new mounts and looks
            Check(Catalog.Find("saddleup.dragon") == Catalog.Find(M("dragon")) && Catalog.Find("saddleup.worm") == Catalog.Find(M("worm")), "the old mount ids find the packs' mounts (saddleup.dragon -> " + M("dragon") + ")");
            Check(Looks.Find("saddleup.rat/default") == Looks.Find(Sk("rat")) && Looks.Find("dragon") == Looks.Find(Sk("dragon")), "the old skin ids and the short names find their looks");
            Check(MountSettingsPage.RenamedMountKey("Coat_" + M("dragon")) == "Coat_saddleup.dragon" && MountSettingsPage.RenamedMountKey("Coat_" + M("gerry")) == null,
                "a per-mount cfg key of a renamed mount carries the old key's value over (Coat_saddleup.dragon); Gerry's kept its id");
            yield break;
        }
    }
}
