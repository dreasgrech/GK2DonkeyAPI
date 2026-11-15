using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The API as the owner of its copy of the window code (GK2DreasUI.UiOwner; docs/ui-library.md): what the windows
    // ask their owner for, answered with the API's own parts. Run once at the start of Awake, right after the logger,
    // before anything of the windows or the texts runs.
    internal static class UiOwnerSetup
    {
        internal static void Apply()
        {
            UiOwner.Guid = Plugin.Guid;
            UiOwner.DisplayName = "GK2 Donkey API";
            UiOwner.Log = Plugin.Log;
            UiOwner.InGame = () => Mount.IsGameReady;
            // the Mount Journal under the options: only the journal has an Options button (not its colours window)
            UiOwner.MayOpenOptions = () => !MountsPanel.IsColoursOpen;
            UiOwner.UnderShown = () => MountsPanel.IsShownOnTop;
            UiOwner.HideUnder = MountsPanel.HideUnderOptions;
            UiOwner.ShowUnder = MountsPanel.ShowAfterOptions;
            UiOwner.UnderName = "the Mounts panel";
            UiOwner.OwnWindowOpen = () => MountsPanel.Exists;
            UiOwner.StatusText = () => Mounts.StatusText;
            UiOwner.RegisterKey = (owner, entry, pressed, overPanel) => KeyActions.Add(owner, entry, pressed, overPanel);
            UiOwner.TrackOwner = KeyActions.Track;
            UiOwner.KeyAlias = MountSettingsPage.UnprefixedMountKey;
            // a List row's stored id none of its choices is (a skin whose pack is gone): its name, and whether set aside
            UiOwner.MissingChoice = id => (MountsPanel.NameFromId(id), Looks.Find(id)?.Unavailable != null);
            // a List row's choice drawn: the Skin list's "Default" is the API's word, a named skin's name its owner's term
            UiOwner.ListChoiceText = (r, i, english) =>
            {
                if (english == Looks.DefaultLabel) return UiWords.DefaultSkin.Text;
                Looks.SkinDef s = r.ChoiceValues != null && i < r.ChoiceValues.Length ? Looks.Find(r.ChoiceValues.GetValue(i) as string) : null;
                return s != null && !string.IsNullOrEmpty(s.Name) && english == s.Name ? Looks.NameTerm(s).Text : null;
            };
            UiOwner.DevUnlocked = () => DevTools.Unlocked;
            UiOwner.TestLanguage = () => DevTools.Unlocked && Plugin.TestLanguageOn != null && Plugin.TestLanguageOn.Value;
            UiOwner.TranslationReport = () => Plugin.TranslationReport != null && Plugin.TranslationReport.Value;
            UiOwner.OwnWords = () => UiWords.All;
            UiOwner.MoreEnglish = MoreEnglish;
            UiOwner.MoreOwners = MoreOwners;
            UiOwner.EnglishStamp = () => Catalog.Version * 31 + Looks.Count * 7;
            Words.Remembered = UiWords.Remember;
        }

        // The mounts and skins an owner registered, by id: the names and descriptions the journal, the colours window and
        // the Skin list show (Catalog.NameTerm, Looks.NameTerm); the donkey's are the API's words.
        private static void MoreEnglish(string owner, Action<string, string, string> put)
        {
            var mounts = new List<Catalog.Entry>();
            foreach (Catalog.Entry k in Catalog.All) if (k != Catalog.Donkey && string.Equals(k.Owner, owner, StringComparison.OrdinalIgnoreCase)) mounts.Add(k);
            mounts.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (Catalog.Entry k in mounts)
            {
                if (!string.IsNullOrEmpty(k.Name)) put("mount." + k.Id + ".name", k.Name, "a mount's name (the Mount Journal; the keeper's bubbles)");
                put("mount." + k.Id + ".description", k.Description, "a mount's description in the Mount Journal");
                foreach (MountAbility a in k.Abilities)
                {
                    if (!string.IsNullOrWhiteSpace(a.Name)) put("mount." + k.Id + ".ability." + a.Id + ".name", a.Name, "an ability of the mount, in the Mount Journal");
                    put("mount." + k.Id + ".ability." + a.Id + ".description", a.Description, "what the ability does, in the Mount Journal");
                }
            }
            var skins = new List<Looks.SkinDef>();
            foreach (Looks.SkinDef s in Looks.All) if (string.Equals(s.Owner, owner, StringComparison.OrdinalIgnoreCase)) skins.Add(s);
            skins.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (Looks.SkinDef s in skins)
            {
                // a mount's own look ("default") shows as the API's "Default" in the Skin list and nowhere by its name
                if (!string.IsNullOrEmpty(s.Name) && !s.LocalId.Equals(Looks.DefaultSkinName, StringComparison.OrdinalIgnoreCase)) put("skin." + s.Id + ".name", s.Name, "a skin's name (the Skin list, the colours window's title)");
                // a skin's description shows only as its mount's, for a mount with none of its own, from its own look (the journal's
                // header, MountsPanel.Entries); any other is drawn nowhere (the first dump listed 8 such, 2026-11-01)
                Catalog.Entry m = Looks.MountOf(s);
                if (m != null && string.IsNullOrEmpty(m.Description) && string.Equals(Looks.DefaultSkinOf(m), s.Id, StringComparison.OrdinalIgnoreCase))
                    put("skin." + s.Id + ".description", s.Description, "the mount's description in the Mount Journal (its own look's: the mount has none of its own)");
                // its own tones' names: their rows' labels in the colours window (MountSettingsPage.ToneRows); the ones a
                // row shows, its own or a variant's that takes it (a variant's tone without a name of its own shows its
                // default's; a tone with no colours, on it or on any variant taking it, has no row)
                List<SkinTones.ToneDef> tones = SkinTones.DeclaredBy(s);
                if (tones != null)
                {
                    foreach (SkinTones.ToneDef t in tones)
                    {
                        if (SkinTones.NameShown(s, t)) put("skin." + s.Id + ".tone." + t.Id + ".name", t.Name, "a part of the skin the player colours on its own: its row's label in the colours window");
                    }
                }
                // its names of the mount regions' parts (SkinRegions: [Region.<region>]): their rows' labels, on it and on a
                // variant that takes them
                foreach (KeyValuePair<string, string> n in SkinRegions.Declared(s))
                    put(SkinRegions.KeyOf(s.Id, n.Key), n.Value, "a part of the mount the player colours: its row's label in the colours window (the skin's name for it)");
            }
        }

        // Every loaded mod or pack that registered a mount or a skin: an owner with words to translate.
        private static IEnumerable<string> MoreOwners()
        {
            var list = new List<string>();
            foreach (Catalog.Entry k in Catalog.All) if (!string.IsNullOrEmpty(k.Owner) && !list.Contains(k.Owner)) list.Add(k.Owner);
            foreach (Looks.SkinDef s in Looks.All) if (!string.IsNullOrEmpty(s.Owner) && !list.Contains(s.Owner)) list.Add(s.Owner);
            return list;
        }
    }
}
