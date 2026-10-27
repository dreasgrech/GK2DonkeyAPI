using System;
using System.Collections;
using System.IO;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Built in or external (the user, 2026-10-26; docs/making-a-mount.md "Built in or external"): Saddle Up's Gerry and
    // its donkey skins come from its DLL; the dragon, rat, snail and worm from their packs' own DLLs (packs/SaddleUpDragon,
    // packs/SaddleUpCritters: a small plugin each, its folder embedded), each under the pack's own plugin GUID and with
    // its ids by the pack; the old ids of a dev's own world find the new mounts (Catalog.renamedMounts).
    internal static partial class SelfTest
    {
        private static IEnumerator ExternalPacks()
        {
            var packOf = new[] { ("dragon", "SaddleUpDragon", "dreas.gk2.saddleup.dragon"), ("rat", "SaddleUpCritters", "dreas.gk2.saddleup.critters"),
                ("snail", "SaddleUpCritters", "dreas.gk2.saddleup.critters"), ("worm", "SaddleUpCritters", "dreas.gk2.saddleup.critters") };
            foreach ((string name, string pack, string guid) in packOf)
            {
                Catalog.Entry k = Catalog.Find(M(name));
                if (k == null)
                {
                    Skip($"the pack {pack} is not installed ({M(name)} isn't registered): build packs/{pack}/{pack}.csproj with the game closed");
                    yield break;
                }
                string asm = k.Pack?.SourceAssembly?.GetName().Name;
                Check(k.Owner == guid && asm == pack && k.Pack?.Source == null,
                    $"{M(name)}: from the pack's own DLL {pack}.dll (owner {k.Owner}; {asm ?? "no DLL"})");
                Check(!string.IsNullOrEmpty(k.Needs), $"{M(name)}: its mount.cfg says the API it needs (needs = {k.Needs})");
                Looks.SkinDef own = Looks.Find(Sk(name));
                Check(own != null && own.Owner == k.Owner && own.SourceAssembly?.GetName().Name == pack, $"{Sk(name)}: its own look from the same DLL ({own?.Owner})");
                Check(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid), $"{pack} is a BepInEx plugin of its own ({guid})");
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
