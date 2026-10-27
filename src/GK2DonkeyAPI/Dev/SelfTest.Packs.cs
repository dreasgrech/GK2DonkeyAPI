using System.Collections.Generic;
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
            int missing = 0;
            foreach ((string name, string pack, string guid) in packOf)
            {
                Catalog.Entry k = Catalog.Find(M(name));
                if (k == null)
                {
                    // a pack left out (Saddle Up alone, or one pack; the user, 2026-10-27): the others are still checked
                    Info($"GK2 Saddle Up's {pack} pack isn't installed ({M(name)} isn't registered): not checked");
                    missing++;
                    continue;
                }
                string asm = k.Pack?.SourceAssembly?.GetName().Name, dll = "GK2" + pack; // the DLL GK2-prefixed since 2026-10-27, the folder packs/<pack>
                Check(k.Owner == guid && asm == dll && k.Pack?.Source == null,
                    $"{M(name)}: from the pack's own DLL {dll}.dll (owner {k.Owner}; {asm ?? "no DLL"})");
                Check(!string.IsNullOrEmpty(k.Needs), $"{M(name)}: its mount.cfg says the API it needs (needs = {k.Needs})");
                Looks.SkinDef own = Looks.Find(Sk(name));
                Check(own != null && own.Owner == k.Owner && own.SourceAssembly?.GetName().Name == dll,$"{Sk(name)}: its own look from the same DLL ({own?.Owner})");
                Check(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid), $"{pack} is a BepInEx plugin of its own ({guid})");
            }
            if (missing > 0) Info($"{missing} of {packOf.Length} pack mounts not installed: Saddle Up's own content below is checked all the same");
            // a chosen mount whose pack is gone shows in the Mounts panel by a name made from its id (2026-10-27)
            Check(MountsPanel.NameFromId("saddleup-dragon.dragon") == "Dragon" && MountsPanel.NameFromId("my-mod.big-crab") == "Big crab" && MountsPanel.NameFromId("plain") == "Plain",
                $"a missing mount's name from its id: {MountsPanel.NameFromId("saddleup-dragon.dragon")}, {MountsPanel.NameFromId("my-mod.big-crab")}");
            Catalog.Entry gerry = Catalog.Find(M("gerry"));
            Check(gerry != null && gerry.Pack?.Source == null && gerry.Pack?.SourceAssembly?.GetName().Name == "GK2SaddleUp",
                $"Gerry: built into Saddle Up's DLL ({gerry?.Pack?.SourceAssembly?.GetName().Name ?? "none"})");
            // a mount with a voice and no answer of its own: its babies answer with its voice, pitched up (the user, 2026-10-27:
            // Gerry's babies never answered him)
            var gerrySounds = Sounds.Coverage(Looks.Find(Sk("gerry")), gerry, baby: true);
            var answer = gerrySounds.Find(x => x.Event == "answer");
            Check(answer.What != null && answer.What.Contains("its voice") && answer.From != "missing", $"Gerry's babies answer with his voice ({answer.From}: {answer.What})");
            // the Mounts panel's abilities card (2026-10-27): Gerry's cart with his cargo; the dragon flies and pulls none
            List<MountsPanel.Entry> panelEntries = MountsPanel.Entries();
            MountsPanel.Entry gerryEntry = panelEntries.Find(x => x.Id == M("gerry")), dragonEntry = panelEntries.Find(x => x.Id == M("dragon"));
            Check(gerryEntry != null && gerryEntry.Abilities.Exists(x => x.Name == "Cargo cart" && x.Line.Contains("4 items")) && !gerryEntry.Abilities.Exists(x => x.Name == "Flying"),
                $"Gerry's abilities: a cargo cart of 4 ({(gerryEntry == null ? "no entry" : string.Join(", ", gerryEntry.Abilities.ConvertAll(x => x.Name)))})");
            if (dragonEntry != null) Check(dragonEntry.Abilities.Exists(x => x.Name == "Flying") != !Flight.Active && !dragonEntry.Abilities.Exists(x => x.Name == "Cargo cart"),
                $"the dragon's abilities: flying, no cargo cart ({string.Join(", ", dragonEntry.Abilities.ConvertAll(x => x.Name))})");
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
