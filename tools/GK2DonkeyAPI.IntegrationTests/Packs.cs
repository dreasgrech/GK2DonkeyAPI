using GK2DonkeyAPI.Api;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.Packs.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        internal static IEnumerator ExternalPacks()
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
            // the donkey's two skins are a pack of their own since 2026-10-27 (packs/SaddleUpDonkeySkins): Saddle Up's DLL
            // holds no donkey skin
            bool suDonkeySkin = false;
            foreach (Looks.SkinDef d in Looks.All) if (d.MountId == Catalog.DonkeyId && d.SourceAssembly?.GetName().Name == "GK2SaddleUp") suDonkeySkin = true;
            Check(!suDonkeySkin, "Saddle Up's DLL brings no donkey skin (they are the Donkey Skins pack's)");
            string noSkins = MissingPack("hellfire", "royal-parade");
            if (noSkins != null) Info(noSkins + ": its skins not checked");
            else
            {
                const string skinsGuid = "dreas.gk2.saddleup.donkeyskins", skinsDll = "GK2SaddleUpDonkeySkins";
                foreach (string skin in new[] { Sk("hellfire"), Sk("royal-parade") })
                {
                    Looks.SkinDef d = Looks.Find(skin);
                    Check(d != null && d.Source == null && d.Owner == skinsGuid && d.SourceAssembly?.GetName().Name == skinsDll,
                        $"{skin}: from the Donkey Skins pack's own DLL {skinsDll}.dll (owner {d?.Owner}; {d?.SourceAssembly?.GetName().Name ?? "no DLL"})");
                }
                Check(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(skinsGuid), $"the Donkey Skins pack is a BepInEx plugin of its own ({skinsGuid})");
                var fire = GK2DonkeyAPI.Effects.Find("saddleup-donkey-skins.hellfire");
                Check(fire != null && fire.Owner == skinsGuid, $"Hellfire's effect comes with its skin, the pack's (saddleup-donkey-skins.hellfire, owner {fire?.Owner})");
                // the late skins: the pack loads after Saddle Up (its soft dependency), so its skins come after Saddle Up's page
                // was built; its donkey Skin row reads its choices when it is shown, and offers both
                MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
                MountSettingsPage.Row skinRow = saddle?.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "Skin_" + Catalog.DonkeyId);
                if (skinRow == null) Info("Saddle Up's page isn't shown: its donkey Skin row not checked");
                else
                {
                    MountSettingsPage.FillList(skinRow);
                    var offered = new List<string>();
                    if (skinRow.ChoiceValues != null) foreach (object v in skinRow.ChoiceValues) offered.Add(v as string);
                    Check(offered.Contains(Sk("hellfire")) && offered.Contains(Sk("royal-parade")), $"Saddle Up's donkey Skin row offers both, registered after its page was built ({string.Join(", ", offered)})");
                }
            }
            // the ids the mounts had inside Saddle Up (never released) are no one's now: the API keeps no table of a mod's
            // old ids (2026-10-27, the API knows nothing of Saddle Up); a short name still finds the one mount that has it
            Check(Catalog.Find("saddleup.dragon") == null && Catalog.Find("saddleup.worm") == null, "the old mount ids (saddleup.dragon) find nothing: no rename table in the API");
            Check(Looks.Find("dragon") == Looks.Find(Sk("dragon")), "a short name finds its look (dragon -> " + Sk("dragon") + ")");
            yield break;
        }
    }
}
