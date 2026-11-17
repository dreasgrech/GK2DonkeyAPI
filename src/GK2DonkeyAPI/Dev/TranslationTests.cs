using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The translations' self-tests (docs/localization.md, steps 1 to 3; Texts). Files: written for a made-up owner into a
    // folder of the test's own (BepInEx\GK2-l10n-test, gone after) and read by Texts' rules. Windows: the test language over
    // the options window (every section), the Mount Journal and its colours window: every word drawn looked up, the room
    // each has at 1.6 times its length measured (TextRoom). Bubbles: the keeper's sentences, the prompts on foot and the
    // crossroads notice. PackNames: the mounts' and skins' names in their owners' files. Mistakes: the translator mistake
    // catalogue (the window library's TranslationCatalogue, also run without the game by its tests/TranslationScan).
    // All put back the run's English (SelfTest.StartRun sets it).
    internal static class TranslationTests
    {
        private const string Owner = "test.gk2.l10n";

        private static string Of(string key, string english) => Texts.Of(Owner, key, english);

        internal static IEnumerator Files()
        {
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test");
            string bundled = Path.Combine(root, "bundled");
            string translator = Path.Combine(root, "translator");
            string theirs = Path.Combine(Path.Combine(translator, "Translations"), Owner);
            // (a translator's own folder, Translations\<owner>\, and the bundled one; CSV only since 2026-11-01)
            string languageWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            List<string> rootsWas = Texts.TestTranslators;
            bool quietWas = Texts.TestQuiet;
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(bundled);
                Directory.CreateDirectory(theirs);
                // a translator's own pt-br (commas, the template's note column), a bundled pt-br saved with tabs and a bundled
                // pt saved as European Excel does (; and a BOM)
                File.WriteAllText(Path.Combine(theirs, "pt-br.csv"), "key,value,english,note\nt.greet,Oi,Hello,\"a note, not read\"\nt.bye,Tchau,Bye,\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bundled, "pt-br.csv"), "key\tvalue\tenglish\nt.tabkey\tTab\tTab key\nt.greet\tBundled\tHello\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bundled, "pt.csv"),
                    "key;value;english\r\nt.greet;Olá;Hello\r\nt.changed;Mudado;Old English\r\nt.noen;Sem;\r\nt.empty;;Empty\r\nt.fmt;Montar {0};Ride {0}\r\n"
                    + "t.badfmt;Montar {1};Ride {0}\r\nt.two;\"Duas\r\nlinhas\";\"Two\r\nlines\"\r\nt.csvonly;\"Só; com \"\"aspas\"\"\";Only CSV\r\na line without a separator\r\n", new UTF8Encoding(true));
                Texts.TestBundled[Owner] = bundled;
                Texts.TestTranslators = new List<string> { translator };
                Texts.TestForced = false;
                Texts.TestQuiet = true;
                Texts.TestWarnings = 0;
                Texts.LanguageOverride = "pt-br";
                Texts.Forget();
                SelfTest.Check(Of("t.greet", "Hello") == "Oi", $"pt-br: a translator's own pt-br.csv beats the bundled ones ({Of("t.greet", "Hello")})");
                SelfTest.Check(Of("t.bye", "Bye") == "Tchau", "a CSV with commas and the template's fourth column (the note, not read)");
                SelfTest.Check(Of("t.tabkey", "Tab key") == "Tab", "a CSV saved with tabs");
                SelfTest.Check(Of("t.csvonly", "Only CSV") == "Só; com \"aspas\"", $"a CSV saved with ; and a BOM (European Excel), a quoted field with ; and \"\" ({Of("t.csvonly", "Only CSV")})");
                SelfTest.Check(Of("t.changed", "New English") == "New English", "a line whose English changed since shows the English");
                SelfTest.Check(Of("t.noen", "Anything") == "Anything", "a line with no English recorded is left out: the English (nothing could tell when the English changes under it)");
                SelfTest.Check(Of("t.missing", "Missing") == "Missing" && Of("t.empty", "Empty") == "Empty", "a line left out or left empty shows the English");
                SelfTest.Check(Texts.Format(Owner, "t.fmt", "Ride {0}", "Bella") == "Montar Bella", "a template fills in its {0}");
                SelfTest.Check(Texts.Format(Owner, "t.badfmt", "Ride {0}", "Bella") == "Ride Bella", "a template that doesn't take its values: the English");
                SelfTest.Check(Of("t.two", "Two\nlines") == "Duas\nlinhas", "a line break inside a quoted field");
                Texts.Table table = Texts.TableOf(Owner);
                SelfTest.Check(table != null && table.Malformed == 2 && Texts.TestWarnings >= 2, $"the line without a separator and the one without its English are left out and said ({table?.Malformed} left out, {Texts.TestWarnings} warning(s), not logged in the test)");
                yield return null;
                Texts.LanguageOverride = "pt";
                Texts.Forget();
                SelfTest.Check(Of("t.greet", "Hello") == "Oi" && Texts.Format(Owner, "t.fmt", "Ride {0}", "Bella") == "Montar Bella" && Of("t.tabkey", "Tab key") == "Tab", $"pt: a translator's pt-br.csv first (a translator's file of any name before the bundled ones), then the bundled pt.csv, then the bundled pt-br.csv ({Of("t.greet", "Hello")})");
                Texts.LanguageOverride = "de";
                Texts.Forget();
                SelfTest.Check(Of("t.greet", "Hello") == "Hello", "a language with no file: the English");
                Texts.LanguageOverride = "en";
                Texts.Forget();
                SelfTest.Check(Of("t.greet", "Hello") == "Hello", "English: the code's, no file read");
                Texts.TestForced = true;
                string t = Of("t.greet", "Hello"), f = Texts.Format(Owner, "t.fmt", "Ride {0}", "Bella");
                SelfTest.Check(t.StartsWith("[Hello") && t.EndsWith("]") && t.Length >= 9 && f.StartsWith("[Ride Bella") && f.EndsWith("]"), $"the test language: longer and in brackets, a template still filled ({t}; {f})");
                Texts.TestForced = false;
                // the API's English template copied as a translation (a translator's first step: en.csv saved as de.csv)
                // reads back whole: every line found, its English the code's, none left out
                List<(string Key, string English, string What)> words = Texts.EnglishOf(Texts.Api);
                string copied = Path.Combine(root, "copied");
                Directory.CreateDirectory(copied);
                File.WriteAllText(Path.Combine(copied, "de.csv"), Texts.EnglishCsv(words), new UTF8Encoding(true));
                Texts.TestBundled[Owner] = copied;
                Texts.TestTranslators = new List<string>();
                Texts.LanguageOverride = "de";
                Texts.Forget();
                Texts.Table back = Texts.TableOf(Owner);
                int same = 0;
                foreach (var x in words) if (back != null && back.Lines.TryGetValue(x.Key, out Texts.Line l) && l.Value == x.English && l.English == x.English) same++;
                SelfTest.Check(back != null && same == words.Count && back.Malformed == 0, $"the API's English template read back as a translation: {same} of its {words.Count} lines, {back?.Malformed ?? 0} left out (en.csv saved as de.csv is a whole translation)");
                Texts.LanguageOverride = "en";
                Texts.Forget();
                // what there is to translate: the API's words, each key once
                List<(string Key, string English, string What)> api = Texts.EnglishOf(Texts.Api);
                var keys = new HashSet<string>();
                foreach (var x in api) keys.Add(x.Key);
                SelfTest.Check(api.Count == UiWords.All.Count && keys.Count == api.Count && api.Count > 50, $"the API's English: its {UiWords.All.Count} words, each key once ({api.Count} lines)");
                foreach (MountSettingsPage p in MountSettingsPage.All)
                {
                    if (!p.Shown) continue;
                    List<(string Key, string English, string What)> own = Texts.EnglishOf(p.OwnerId);
                    int labels = 0;
                    foreach (var x in own) if (x.Key.EndsWith(".label")) labels++;
                    SelfTest.Info($"{p.Title}'s English: {own.Count} lines ({labels} labels)");
                }
            }
            finally
            {
                Texts.TestBundled.Remove(Owner);
                Texts.TestTranslators = rootsWas;
                Texts.LanguageOverride = languageWas;
                Texts.TestForced = testWas;
                Texts.TestQuiet = quietWas;
                Texts.Forget();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception e) { SelfTest.Warn($"the test's folder {root} couldn't be deleted: {e.Message}"); }
            }
        }

        // The keeper's bubbles, the prompts on foot and the crossroads notice (step 2). Under the test language: every say.
        // word found by its English (a fixed sentence, or one a template made a moment ago, Word.Make) and shown bracketed;
        // the reason a template makes stays English; another mod's sentence as it is; a bubble said for real shows the
        // translation (Mount.Say, in a game). Then a German de.csv of the API's (a fixed sentence, a template with the
        // donkey's name in it, a prompt, the notice, a line whose English changed since) read back.
        internal static IEnumerator Bubbles()
        {
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test-say");
            string languageWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            List<string> rootsWas = Texts.TestTranslators;
            Texts.TestTranslators = new List<string>(); // no real translators' files under the test's own
            bool hadApi = Texts.TestBundled.TryGetValue(Texts.Api, out string apiWas);
            bool live = Mount.IsGameReady && Plugin.ShowMessages.Value;
            try
            {
                Texts.TestForced = true;
                int fixedAll = 0, fixedOk = 0, made = 0, madeOk = 0;
                var bad = new List<string>();
                foreach (Word w in UiWords.All)
                {
                    if (!w.Key.StartsWith("say.", StringComparison.Ordinal)) continue;
                    if (w.English.IndexOf('{') < 0)
                    {
                        fixedAll++;
                        if (UiWords.Shown(w.English) == Texts.Test(w.English)) fixedOk++;
                        else bad.Add(w.Key);
                        continue;
                    }
                    made++;
                    string reason = w.Make("Q1", "Q2");
                    string shown = UiWords.Shown(reason);
                    if (reason.IndexOf('[') < 0 && reason.Contains("Q1") && IsBracketed(shown) && shown.Contains("Q1")) madeOk++;
                    else bad.Add($"{w.Key} ({reason} / {shown})");
                }
                SelfTest.Check(fixedAll > 50 && fixedOk == fixedAll, $"every fixed sentence of the keeper's is found by its English and looked up ({fixedOk} of {fixedAll}{(bad.Count > 0 ? "; NOT: " + string.Join(", ", bad) : "")})");
                SelfTest.Check(made >= 6 && madeOk == made, $"every sentence made from a template is found and looked up, its reason in English with the values ({madeOk} of {made})");
                string cant = UiWords.SayCantPullTheCart.Make(Catalog.NameTerm(Catalog.Donkey));
                string cantShown = UiWords.Shown(cant);
                SelfTest.Check(cant == "Donkey can't pull the cart." && cantShown.Contains(Texts.Test("Donkey")), $"a value with its own translation (the donkey's name): English in the reason, looked up in the bubble ({cant} / {cantShown})");
                SelfTest.Check(UiWords.Shown("A mod's own sentence.") == "A mod's own sentence.", "another mod's sentence is shown as it is");
                int prompts = 0, promptsOk = 0;
                foreach (Word w in UiWords.All)
                {
                    if (!w.Key.StartsWith("prompt.", StringComparison.Ordinal)) continue;
                    prompts++;
                    string p = w.English.IndexOf('{') < 0 ? w.Text : w.Format("Bella");
                    if (IsBracketed(p) && (w.English.IndexOf('{') < 0 || p.Contains("Bella"))) promptsOk++;
                }
                SelfTest.Check(prompts >= 8 && promptsOk == prompts, $"the prompts on foot are looked up, \"Ride {{0}}\" with the name the player gave ({promptsOk} of {prompts})");
                SelfTest.Check(Corpses.NotificationShown(UiWords.NoticeCrossroads.English) == Texts.Test(UiWords.NoticeCrossroads.English) && Corpses.NotificationShown("My own words") == "My own words",
                    "the crossroads notice: the shipped words looked up, a player's own text as written");
                if (live)
                {
                    Mount.Say(UiWords.SayCartFull.English);
                    SelfTest.Check(Mount.Instance.TestLastShown == Texts.Test(UiWords.SayCartFull.English), $"a bubble said for real shows the translation ({Mount.Instance.TestLastShown})");
                    Mount.Say(UiWords.SayTooMuchOnCart.Make(6, 4));
                    SelfTest.Check(Mount.Instance.TestLastShown != null && IsBracketed(Mount.Instance.TestLastShown) && Mount.Instance.TestLastShown.Contains("6"), $"and one made from a template, with its values ({Mount.Instance.TestLastShown})");
                }
                else SelfTest.Info("not in a game, or the bubbles are off (General.ShowMessages): no bubble said for real");
                yield return SelfTest.Frames(1);

                // German
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "de.csv"),
                    "key,value,english\n"
                    + "say.cart_full,Der Karren ist voll.,The cart is full.\n"
                    + "say.cant_pull_the_cart,{0} kann den Karren nicht ziehen.,{0} can't pull the cart.\n"
                    + "mount.donkey.name,Esel,Donkey\n"
                    + "prompt.ride_named,{0} reiten,Ride {0}\n"
                    + "notice.crossroads,Der Esel hat eine Leiche an der Kreuzung gelassen,The donkey left a body at the crossroads\n"
                    + "say.too_much_on_cart,Zu viel ({0} von {1}).,An older English of this sentence\n", new UTF8Encoding(false));
                Texts.TestBundled[Texts.Api] = root;
                Texts.TestForced = false;
                Texts.LanguageOverride = "de";
                Texts.Forget();
                SelfTest.Check(UiWords.Shown(UiWords.SayCartFull.English) == "Der Karren ist voll.", $"German: a fixed sentence ({UiWords.Shown(UiWords.SayCartFull.English)})");
                string deCant = UiWords.SayCantPullTheCart.Make(Catalog.NameTerm(Catalog.Donkey));
                SelfTest.Check(deCant == "Donkey can't pull the cart." && UiWords.Shown(deCant) == "Esel kann den Karren nicht ziehen.", $"German: a template with the donkey's name, the reason still English ({deCant} / {UiWords.Shown(deCant)})");
                string tooMuch = UiWords.SayTooMuchOnCart.Make(6, 4);
                SelfTest.Check(UiWords.Shown(tooMuch) == tooMuch, $"German: a line whose English changed since shows the English ({UiWords.Shown(tooMuch)})");
                SelfTest.Check(UiWords.PromptRideNamed.Format("Bella") == "Bella reiten", $"German: the prompt with the player's name for the mount ({UiWords.PromptRideNamed.Format("Bella")})");
                SelfTest.Check(Corpses.NotificationShown(UiWords.NoticeCrossroads.English) == "Der Esel hat eine Leiche an der Kreuzung gelassen" && Corpses.NotificationShown("Mein Text") == "Mein Text", "German: the crossroads notice, a player's own text as written");
                MountsPanel.Entry donkey = MountsPanel.Entries().Find(x => x.Id == MountCatalog.DonkeyId);
                SelfTest.Check(donkey != null && donkey.Label == "Esel", $"German: the journal lists the donkey by its German name ({donkey?.Label})");
                if (live)
                {
                    Mount.Say(UiWords.SayCartFull.English);
                    SelfTest.Check(Mount.Instance.TestLastShown == "Der Karren ist voll.", $"German: a bubble said for real ({Mount.Instance.TestLastShown})");
                }
            }
            finally
            {
                if (hadApi) Texts.TestBundled[Texts.Api] = apiWas; else Texts.TestBundled.Remove(Texts.Api);
                Texts.LanguageOverride = languageWas;
                Texts.TestForced = testWas;
                Texts.TestTranslators = rootsWas;
                Texts.Forget();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception e) { SelfTest.Warn($"the test's folder {root} couldn't be deleted: {e.Message}"); }
            }
        }

        // The mounts' and skins' names (step 3), in the files of the mod or pack that registered them: a mount other than the
        // donkey and a named skin, under the test language (the name, the journal's entry and header, the Skin list's choice)
        // and with German files in the owners' bundled folders (read back the same ways); every owner's English lists its
        // mounts and skins (what the dev run writes for the packs, BepInEx\GK2-l10n\<owner>\en.csv).
        internal static IEnumerator PackNames()
        {
            MountSettingsPage page = null;
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow) { page = live; break; }
            Catalog.Entry k = null;
            foreach (MountsPanel.Entry e in MountsPanel.Entries())
            {
                if (e.Fake || e.Id == MountCatalog.DonkeyId) continue;
                Catalog.Entry c = Catalog.Find(e.Id);
                if (c != null && !string.IsNullOrEmpty(c.Name)) { k = c; break; }
            }
            Looks.SkinDef skin = null;
            foreach (Looks.SkinDef s in Looks.All)
            {
                if (s.Unavailable == null && !string.IsNullOrEmpty(s.Name) && !s.LocalId.Equals(Looks.DefaultSkinName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(s.Owner)) { skin = s; break; }
            }
            // every owner's English: its mounts' names (the dev run writes these for the packs)
            int owners = 0, missing = 0;
            var ownerLines = new List<string>();
            foreach (string owner in Texts.Owners())
            {
                List<(string Key, string English, string What)> lines = Texts.EnglishOf(owner);
                var keys = new HashSet<string>();
                foreach (var x in lines) keys.Add(x.Key);
                int mounts = 0, skins = 0;
                foreach (var x in lines)
                {
                    if (x.Key.StartsWith("mount.", StringComparison.Ordinal) && x.Key.EndsWith(".name", StringComparison.Ordinal)) mounts++;
                    if (x.Key.StartsWith("skin.", StringComparison.Ordinal)) skins++;
                }
                foreach (Catalog.Entry c in Catalog.All) if (c != Catalog.Donkey && !string.IsNullOrEmpty(c.Name) && string.Equals(c.Owner, owner, StringComparison.OrdinalIgnoreCase) && !keys.Contains("mount." + c.Id + ".name")) missing++;
                owners++;
                ownerLines.Add($"{owner}: {lines.Count} lines ({mounts} mount name(s), {skins} skin line(s))");
            }
            SelfTest.Check(missing == 0 && owners >= 1, $"every owner's English lists its mounts' names ({owners} owner(s), {missing} name(s) missing)");
            SelfTest.Info("the English per owner: " + string.Join("; ", ownerLines));
            if (k == null && skin == null)
            {
                SelfTest.Skip("no mount but the donkey and no named skin is installed: the names' lookup not tried");
                yield break;
            }
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test-names");
            string languageWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            List<string> rootsWas = Texts.TestTranslators;
            Texts.TestTranslators = new List<string>(); // no real translators' files under the test's own
            var owned = new List<string>();
            if (k != null && !owned.Contains(k.Owner)) owned.Add(k.Owner);
            if (skin != null && !owned.Contains(skin.Owner)) owned.Add(skin.Owner);
            var had = new Dictionary<string, string>();
            foreach (string o in owned) if (Texts.TestBundled.TryGetValue(o, out string was)) had[o] = was;
            string chosenWas = page?.MountChosen; // the player's pick, put back in the finally
            bool picked = false;
            SettingsPage.Row skinRow = null;
            int skinAt = -1;
            if (page != null && skin != null)
            {
                foreach (SettingsPage.Row r in page.Rows)
                {
                    if (r.RowKind != SettingsPage.RowKind.List) continue;
                    MountSettingsPage.FillList(r);
                    if (r.ChoiceValues == null) continue;
                    for (int i = 0; i < r.ChoiceValues.Length; i++) if (string.Equals(r.ChoiceValues.GetValue(i) as string, skin.Id, StringComparison.OrdinalIgnoreCase)) { skinRow = r; skinAt = i; }
                    if (skinRow != null) break;
                }
            }
            try
            {
                Texts.TestForced = true;
                if (k != null)
                {
                    MountsPanel.Entry e = MountsPanel.Entries().Find(x => x.Id == k.Id);
                    SelfTest.Check(Catalog.NameTerm(k).Text == Texts.Test(k.Name) && e != null && e.Label == Texts.Test(k.Name), $"the test language: {k.Name} ({k.Owner}'s) is looked up, in the journal's list too ({e?.Label})");
                }
                if (skin != null)
                {
                    SelfTest.Check(Looks.NameTerm(skin).Text == Texts.Test(skin.Name), $"the test language: the skin {skin.Name} ({skin.Owner}'s) is looked up");
                    if (skinRow != null) SelfTest.Check(Texts.Choice(skinRow, skinAt) == Texts.Test(skin.Name), $"and in the Skin list ({Texts.Choice(skinRow, skinAt)})");
                    else SelfTest.Info($"the skin {skin.Id} is in no Skin list of a shown page");
                }
                yield return SelfTest.Frames(1);

                // German, in each owner's bundled folder as its pack would ship it
                if (Directory.Exists(root)) Directory.Delete(root, true);
                var files = new Dictionary<string, StringBuilder>();
                foreach (string o in owned) files[o] = new StringBuilder("key,value,english\n");
                if (k != null) files[k.Owner].Append(Csv("mount." + k.Id + ".name")).Append(',').Append(Csv("Ä " + k.Name)).Append(',').Append(Csv(k.Name)).Append('\n');
                if (skin != null) files[skin.Owner].Append(Csv("skin." + skin.Id + ".name")).Append(',').Append(Csv("Ä " + skin.Name)).Append(',').Append(Csv(skin.Name)).Append('\n');
                int n = 0;
                foreach (var kv in files)
                {
                    string dir = Path.Combine(root, (n++).ToString());
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "de.csv"), kv.Value.ToString(), new UTF8Encoding(false));
                    Texts.TestBundled[kv.Key] = dir;
                }
                Texts.TestForced = false;
                Texts.LanguageOverride = "de";
                Texts.Forget();
                if (k != null)
                {
                    MountsPanel.Entry e = MountsPanel.Entries().Find(x => x.Id == k.Id);
                    SelfTest.Check(Catalog.NameTerm(k).Text == "Ä " + k.Name && e != null && e.Label == "Ä " + k.Name, $"German: {k.Name} from its owner's de.csv, in the journal's list too ({e?.Label})");
                    // the journal on that mount: only with a pick to put back (SelectMount can't put an empty one back)
                    if (page != null && !string.IsNullOrEmpty(chosenWas) && LazyWindowsStackController.ActiveWindow == null && !MountsPanel.IsOpen && !OptionsWindow.IsOpen)
                    {
                        MountsPanel.Open(page, toggle: false);
                        yield return SelfTest.Frames(3);
                        if (MountsPanel.IsOpen)
                        {
                            picked = true;
                            MountsPanel.TestSelect(k.Id);
                            yield return SelfTest.Frames(3);
                            List<string> journal = TextsUnder(MountsPanel.TestRoot(coloursWindow: false));
                            SelfTest.Check(journal.Contains("Ä " + k.Name), $"German: the journal's header and card name {k.Name} in German");
                            MountsPanel.TestSelect(chosenWas);
                            picked = false;
                            yield return SelfTest.Frames(2);
                            MountsPanel.TestStepBack();
                            yield return SelfTest.Frames(2);
                        }
                    }
                }
                if (skin != null)
                {
                    SelfTest.Check(Looks.NameTerm(skin).Text == "Ä " + skin.Name, $"German: the skin {skin.Name} from its owner's de.csv");
                    if (skinRow != null) SelfTest.Check(Texts.Choice(skinRow, skinAt) == "Ä " + skin.Name, $"German: and in the Skin list ({Texts.Choice(skinRow, skinAt)})");
                }
            }
            finally
            {
                if (picked && MountsPanel.IsOpen) MountsPanel.TestSelect(chosenWas);
                if (MountsPanel.IsOpen) MountsPanel.TestStepBack();
                foreach (string o in owned) { if (had.TryGetValue(o, out string was)) Texts.TestBundled[o] = was; else Texts.TestBundled.Remove(o); }
                Texts.LanguageOverride = languageWas;
                Texts.TestForced = testWas;
                Texts.TestTranslators = rootsWas;
                Texts.Forget();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception e) { SelfTest.Warn($"the test's folder {root} couldn't be deleted: {e.Message}"); }
            }
        }

        // ---- the translator mistake catalogue ----
        // The cases and their run are the window library's (lib/GK2DreasUI/dev/TranslationCatalogue.cs, compiled in
        // through <GK2DreasUIDev>): what a person's spreadsheet or editor does to a translation file, what each key must
        // show and whether the translator is told. Here they run in the game, on its Mono; the library's
        // tests/TranslationScan runs the same without the game.

        // Every mistake of the catalogue in turn, in a folder of the test's own: what shows, and whether the translator is told.
        internal static IEnumerator Mistakes()
        {
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test-mistakes");
            List<(string Name, bool Good, string Why)> results = TranslationCatalogue.RunCatalogue(root);
            int passed = 0;
            var failed = new List<string>();
            foreach (var r in results)
            {
                SelfTest.Check(r.Good, $"mistake: {r.Name}{(r.Good ? "" : " -> " + r.Why)}");
                if (r.Good) passed++;
                else failed.Add(r.Name + ": " + r.Why);
            }
            SelfTest.Info($"the translator mistake catalogue: {passed} of {results.Count} handled as expected{(failed.Count > 0 ? "; NOT: " + string.Join(" | ", failed) : "")}");
            yield break;
        }

        // A made-up skin no pack provides, for a mount's Skin row while its room is measured (as long as a real skin's
        // two-word name); its name as the journal makes it from the id (MountsPanel.NameFromId)
        private const string MissingSkinId = "selftest-missing-skin";
        private static string MissingSkinName => MountsPanel.NameFromId("x/" + MissingSkinId);

        // A mount's Skin row on the page (its cfg entry: "Skin_<mount>", MountSettingsPage.AddSkinRow), or null
        private static BepInEx.Configuration.ConfigEntry<string> SkinEntry(MountSettingsPage page, string mount) =>
            page != null && !string.IsNullOrEmpty(mount) && page.HasSkinRow(mount)
                ? page.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "Skin_" + mount)?.Entry as BepInEx.Configuration.ConfigEntry<string> : null;

        // The page's lists read again on the next build (a Skin row's stored id that no skin provides is added by FillList),
        // as the journal does when a skin comes or goes, and the journal refreshed as after a pick in its Skin row (it watches
        // only the mount by itself: a Skin row's value set from here showed the journal as it was, 2026-11-07)
        private static void Relist(MountSettingsPage page)
        {
            foreach (SettingsPage.Row r in page.Rows)
                if (r.IsLook && r.RowKind == SettingsPage.RowKind.List) { r.ChoiceLabels = null; r.ChoiceValues = null; }
            if (MountsPanel.IsOpen) MountsPanel.Refresh();
        }

        private static MountSettingsPage PageWithOptions()
        {
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && OptionsWindow.HasOptions(live.Page)) return live;
            return null;
        }

        internal static IEnumerator Windows()
        {
            MountSettingsPage page = PageWithOptions();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page with options is shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            bool? testWas = Texts.TestForced;
            var keep = KeepWords(page);
            try
            {
                // the room in English first, every window and section (what each text has now), then the test language: one
                // run gives both (GK2-text-room.tsv "english: ..." and "test language: ...")
                Texts.TestForced = false;
                yield return MeasureEnglish(page);
                Texts.TestForced = true;
                OptionsWindow.Open(page.Page);
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown, "the options window opens in the test language");
                if (!OptionsWindow.IsShown) yield break;
                List<string> names = SectionsOf(page);
                foreach (string name in names)
                {
                    OptionsWindow.TestShowSection(name);
                    yield return SelfTest.Frames(1);
                    List<string> texts = TextsUnder(OptionsWindow.TestRoot);
                    string headingText = OptionsWindow.TestHeadingText ?? "";
                    SelfTest.Check(IsBracketed(headingText) && headingText.StartsWith("[" + name), $"{name}: its heading over the rows is looked up ({headingText})");
                    int labels = 0, plain = 0;
                    foreach (SettingsPage.Row r in page.Rows)
                    {
                        if (!page.InOptions(r) || (r.ShownSection ?? r.Section) != name || (r.Visible != null && !r.Visible())) continue;
                        GameObject go = OptionsWindow.TestRowObject(r);
                        if (go == null) continue;
                        TextMeshProUGUI label = LabelOf(go);
                        if (label == null) continue;
                        labels++;
                        if (!IsBracketed(label.text)) plain++;
                    }
                    SelfTest.Check(plain == 0 && labels > 0, $"{name}: every row's label is looked up ({labels - plain} of {labels})");
                    Leftovers("options: " + name, texts, keep);
                    RoomHolds("options: " + name, TextRoom.Measure(OptionsWindow.TestRoot, "test language: options: " + name));
                    TextRoom.MeasureIn(OptionsWindow.TestDescriptionBox, "test language: options: " + name + " (descriptions)", DescriptionsOf(page, name));
                }
                List<string> bottom = TextsUnder(OptionsWindow.TestRoot);
                SelfTest.Check(bottom.Contains(Texts.Test(UiWords.Done.English)) && bottom.Contains(Texts.Test(UiWords.Defaults.English)), "Done and Defaults are looked up");
                OptionsWindow.TestClose(back: true);
                yield return SelfTest.Frames(2);

                MountsPanel.Open(page, toggle: false);
                yield return SelfTest.Frames(3);
                SelfTest.Check(MountsPanel.IsOpen, "the Mount Journal opens in the test language");
                if (!MountsPanel.IsOpen) yield break;
                List<string> journal = TextsUnder(MountsPanel.TestRoot(coloursWindow: false));
                bool words = journal.Contains(Texts.Test(UiWords.JournalTitle.English)) && journal.Contains(Texts.Test(UiWords.Customize.English)) && journal.Contains(Texts.Test(UiWords.Stats.English));
                SelfTest.Check(words, "the journal's title, Customize and Stats are looked up");
                // step 3: every mount's name, description and abilities in the list's entries (the cards and the header show them)
                int mountCount = 0;
                var plainNames = new List<string>();
                foreach (MountsPanel.Entry e in MountsPanel.Entries())
                {
                    mountCount++;
                    if (!IsBracketed(e.Label)) plainNames.Add(e.Label);
                    if (!string.IsNullOrEmpty(e.Description) && !IsBracketed(e.Description)) plainNames.Add(e.Label + ": its description");
                    foreach (var a in e.Abilities) if (!IsBracketed(a.Name) || (a.Line.Length > 0 && !IsBracketed(a.Line))) plainNames.Add(e.Label + ": " + a.Name);
                }
                SelfTest.Check(plainNames.Count == 0 && mountCount > 0, $"every mount's name, description and abilities are looked up ({mountCount} mount(s){(plainNames.Count > 0 ? "; NOT: " + string.Join(" | ", plainNames) : "")})");
                Leftovers("journal", journal, keep);
                RoomHolds("the journal", TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), "test language: journal"));
                // the chosen mount with a skin not installed (its pack gone): the Skin row by the name alone, the stats card's
                // "Skin: Not installed" (the user's journal, 2026-11-07: "[Armored (not installed)]" didn't fit; made here, as
                // a player's cfg holds it only by chance)
                BepInEx.Configuration.ConfigEntry<string> skinRow = SkinEntry(page, page.MountChosen);
                if (skinRow != null)
                {
                    string skinWas = skinRow.Value;
                    try
                    {
                        skinRow.Value = page.MountChosen + "/" + MissingSkinId;
                        Relist(page);
                        yield return SelfTest.Frames(3);
                        List<string> missing = TextsUnder(MountsPanel.TestRoot(coloursWindow: false));
                        SelfTest.Check(missing.Contains(MissingSkinName) && missing.Contains(Texts.Test(UiWords.NotInstalled.English)) && missing.Contains(Texts.Test(UiWords.SkinRow.English)),
                            $"a skin not installed: the Skin row shows its name alone (\"{MissingSkinName}\"), the stats card \"Skin: Not installed\" (looked up)");
                        RoomHolds("the journal (a skin not installed)", TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), "test language: journal (a skin not installed)"));
                    }
                    finally
                    {
                        skinRow.Value = skinWas;
                        Relist(page);
                    }
                    yield return SelfTest.Frames(3);
                }
                MountsPanel.TestOpenColours();
                yield return SelfTest.Frames(3);
                if (MountsPanel.IsColoursOpen)
                {
                    List<string> colours = TextsUnder(MountsPanel.TestRoot(coloursWindow: true));
                    SelfTest.Check(colours.Contains(Texts.Test(UiWords.Done.English)), "the colours window's Done is looked up");
                    Leftovers("colours window", colours, keep);
                    RoomHolds("the colours window", TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: true), "test language: colours window"));
                    MountsPanel.TestStepBack();
                    yield return SelfTest.Frames(2);
                }
                MountsPanel.TestStepBack();
                yield return SelfTest.Frames(2);
            }
            finally
            {
                Texts.TestForced = testWas;
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
            }
        }

        // A German translation as a translator would ship it, the windows drawn with it (our lookup in German; the game's own
        // language untouched): the page's de.csv in a test copy of its Languages folder (four rows translated, a fifth whose
        // English changed since), the API's de.csv saved as European Excel does (; between columns, a BOM, a quoted field).
        // Read back from the windows: the translated rows in German, the stale one and the rest in English, the API's words
        // (Done, the journal's title, the colours window's title) in German.
        internal static IEnumerator German()
        {
            MountSettingsPage page = PageWithOptions();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page with options is shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test-de");
            string pageDir = Path.Combine(root, "page"), apiDir = Path.Combine(root, "api");
            string languageWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            List<string> rootsWas = Texts.TestTranslators;
            Texts.TestTranslators = new List<string>(); // no real translators' files under the test's own
            bool hadPage = Texts.TestBundled.TryGetValue(page.OwnerId, out string pageWas), hadApi = Texts.TestBundled.TryGetValue(Texts.Api, out string apiWas);
            var chosen = new List<SettingsPage.Row>();
            SettingsPage.Row stale = null;
            foreach (SettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || r.LabelWord != null || r.LabelOf != null || string.IsNullOrEmpty(r.Label) || r.RowKind == SettingsPage.RowKind.Note) continue;
                if (r.Visible != null && !r.Visible()) continue;
                if (chosen.Count < 4) chosen.Add(r);
                else if (stale == null && (r.ShownSection ?? r.Section) == (chosen[0].ShownSection ?? chosen[0].Section)) stale = r;
            }
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(pageDir);
                Directory.CreateDirectory(apiDir);
                var csv = new StringBuilder("key,value,english\n");
                foreach (SettingsPage.Row r in chosen) csv.Append(Csv(r.TextKey + ".label")).Append(',').Append(Csv("Ä " + r.Label)).Append(',').Append(Csv(r.Label)).Append('\n');
                if (stale != null) csv.Append(Csv(stale.TextKey + ".label")).Append(",Ä alt,An older English of this row\n");
                File.WriteAllText(Path.Combine(pageDir, "de.csv"), csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(apiDir, "de.csv"), "key;value;english\r\noptions.done;Fertig;Done\r\njournal.title;\"Reittier-Tagebuch; Seite\";Mount Journal\r\ncolours.title;Farben: {0};Colours: {0}\r\n", new UTF8Encoding(true));
                Texts.TestBundled[page.OwnerId] = pageDir;
                Texts.TestBundled[Texts.Api] = apiDir;
                Texts.TestForced = false;
                Texts.LanguageOverride = "de";
                Texts.Forget();
                OptionsWindow.Open(page.Page);
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown, "the options window opens with a German translation");
                if (!OptionsWindow.IsShown) yield break;
                OptionsWindow.TestShowSection(chosen.Count > 0 ? chosen[0].ShownSection ?? chosen[0].Section : null);
                yield return SelfTest.Frames(1);
                int german = 0, shown = 0;
                var seen = new List<string>();
                foreach (SettingsPage.Row r in chosen)
                {
                    GameObject go = OptionsWindow.TestRowObject(r);
                    TextMeshProUGUI label = go != null ? LabelOf(go) : null;
                    if (label == null) continue; // in another section
                    shown++;
                    seen.Add(label.text);
                    if (label.text == "Ä " + r.Label) german++;
                }
                SelfTest.Check(shown > 0 && german == shown, $"the translated rows are drawn in German ({german} of {shown}: {string.Join(" | ", seen)})");
                if (stale != null && OptionsWindow.TestRowObject(stale) is GameObject sgo && LabelOf(sgo) is TextMeshProUGUI sl)
                {
                    SelfTest.Check(sl.text == stale.Label, $"the row whose English changed since shows its English (\"{sl.text}\")");
                }
                List<string> texts = TextsUnder(OptionsWindow.TestRoot);
                SelfTest.Check(texts.Contains("Fertig"), "the API's Done from its ;-separated de.csv: \"Fertig\"");
                SelfTest.Info($"German: {Texts.TableOf(page.OwnerId)?.Lines.Count ?? 0} line(s) of the page's read, {Texts.TableOf(Texts.Api)?.Lines.Count ?? 0} of the API's");
                OptionsWindow.TestClose(back: true);
                yield return SelfTest.Frames(2);
                MountsPanel.Open(page, toggle: false);
                yield return SelfTest.Frames(3);
                if (MountsPanel.IsOpen)
                {
                    List<string> journal = TextsUnder(MountsPanel.TestRoot(coloursWindow: false));
                    SelfTest.Check(journal.Contains("Reittier-Tagebuch; Seite"), "the journal's title in German, a quoted CSV field with its ; kept");
                    MountsPanel.TestOpenColours();
                    yield return SelfTest.Frames(3);
                    if (MountsPanel.IsColoursOpen)
                    {
                        List<string> colours = TextsUnder(MountsPanel.TestRoot(coloursWindow: true));
                        bool farben = false;
                        foreach (string s in colours) if (s.StartsWith("Farben: ")) farben = true;
                        SelfTest.Check(farben, "the colours window's title from the German template \"Farben: {0}\"");
                        MountsPanel.TestStepBack();
                        yield return SelfTest.Frames(2);
                    }
                    MountsPanel.TestStepBack();
                    yield return SelfTest.Frames(2);
                }
            }
            finally
            {
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                if (hadPage) Texts.TestBundled[page.OwnerId] = pageWas; else Texts.TestBundled.Remove(page.OwnerId);
                if (hadApi) Texts.TestBundled[Texts.Api] = apiWas; else Texts.TestBundled.Remove(Texts.Api);
                Texts.LanguageOverride = languageWas;
                Texts.TestForced = testWas;
                Texts.TestTranslators = rootsWas;
                Texts.Forget();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception e) { SelfTest.Warn($"the test's folder {root} couldn't be deleted: {e.Message}"); }
            }
        }

        // Every language the game offers, switched to for a moment as its own Settings window does (not saved; the player's
        // own back at the end): the font our options window reads from the game's Settings row (before and after its style
        // is applied for the language), the font the row's style gives that language, and whether the font we draw with has
        // the characters of that language's own words (the game's "Options", "Cancel" and "Language" in it; its fallbacks
        // searched). One line per language in BepInEx\GK2-l10n-fonts.tsv.
        internal static IEnumerator Fonts()
        {
            MountSettingsPage page = PageWithOptions();
            GameSettings gs = GameSettings.Instance;
            if (page == null || gs == null)
            {
                SelfTest.Skip(page == null ? "no mod's settings page with options is shown" : "the game's settings aren't there");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            string languageWas = gs.language;
            var ids = new List<string>(LLBase.AvailableLanguages.Keys);
            var lines = new List<string> { "language\tfont before restyle\trestyled\tfont used\tstyle's font for the language\tsample\tall its characters\tmissing\tour translations' characters\tmissing from ours" };
            int good = 0;
            try
            {
                foreach (string id in ids)
                {
                    Switch(gs, id);
                    yield return SelfTest.Frames(2);
                    OptionsWindow.Open(page.Page);
                    yield return SelfTest.Frames(2);
                    UiKit ui = OptionsWindow.TestUi;
                    string sample = Texts.Plain(LLBase.L("ui_menu_options") + " " + LLBase.L("btn_cancel") + " " + LLBase.L("ui_settings_language"));
                    TMP_FontAsset used = ui?.Font;
                    uint[] missing = null;
                    bool all = false;
                    try { all = used != null && used.HasCharacters(sample, out missing, true, true); }
                    catch (Exception e) { SelfTest.Info($"{id}: the font's characters couldn't be checked ({e.Message})"); }
                    bool same = used != null && ui.StyleFont != null && used == ui.StyleFont;
                    if (all && (same || ui?.StyleFont == null)) good++;
                    var missText = new StringBuilder();
                    if (missing != null) foreach (uint c in missing) missText.Append(char.ConvertFromUtf32((int)c));
                    string miss = missText.ToString();
                    // every character of our own translations for the language (each owner's bundled <id>.csv): a font cut to the
                    // game's own characters (the East Asian ones may be) would draw a box for the rest
                    string ours = OurCharacters(id);
                    uint[] oursMissing = null;
                    bool oursAll = true;
                    if (ours.Length > 0 && used != null)
                    {
                        try { oursAll = used.HasCharacters(ours, out oursMissing, true, true); }
                        catch (Exception e) { oursAll = false; SelfTest.Info($"{id}: our translations' characters couldn't be checked ({e.Message})"); }
                    }
                    var oursMissText = new StringBuilder();
                    if (oursMissing != null) foreach (uint c in oursMissing) oursMissText.Append(char.ConvertFromUtf32((int)c));
                    if (ours.Length > 0) SelfTest.Check(oursAll, $"{id}: the font has every character of our {id} translations ({ours.Length} different){(oursMissText.Length > 0 ? " (MISSING: " + oursMissText + ")" : "")}");
                    lines.Add(string.Join("\t", id, ui?.FontBeforeRestyle ?? "-", ui != null && ui.Restyled ? "yes" : "no", used != null ? used.name : "-", ui?.StyleFont != null ? ui.StyleFont.name : "-", sample, all ? "yes" : "no", miss,
                        ours.Length == 0 ? "-" : ours.Length.ToString(), oursMissText.ToString()));
                    SelfTest.Check(all, $"{id}: our windows' font {(used != null ? used.name : "none")} has every character of \"{sample}\"{(miss.Length > 0 ? " (missing: " + miss + ")" : "")}");
                    if (ui?.StyleFont != null) SelfTest.Check(same, $"{id}: it is the font the game's own style gives the language ({ui.StyleFont.name}; the Settings row wore {ui.FontBeforeRestyle} before its style was applied)");
                    OptionsWindow.TestClose(back: true);
                    yield return SelfTest.Frames(2);
                }
            }
            finally
            {
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                Switch(gs, languageWas);
                try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-l10n-fonts.tsv"), string.Join("\n", lines) + "\n", new UTF8Encoding(false)); }
                catch (Exception e) { SelfTest.Info("GK2-l10n-fonts.tsv couldn't be written: " + e.Message); }
            }
            SelfTest.Info($"fonts: {good} of {ids.Count} languages drawn in a font that has their characters (BepInEx\\GK2-l10n-fonts.tsv); the game's language back to {languageWas}");
        }

        // The different characters of every owner's bundled translation for the language (<id>.csv beside each mod's DLL).
        private static string OurCharacters(string id)
        {
            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (string owner in Texts.Owners())
            {
                string folder = Texts.BundledFolder(owner);
                if (folder == null) continue;
                string file = Path.Combine(folder, id + ".csv");
                if (!File.Exists(file)) continue;
                TranslationFiles.File read = TranslationFiles.Read(file, id);
                foreach (TranslationFiles.Row r in read.Rows)
                {
                    foreach (char c in r.Value ?? "")
                    {
                        if (char.IsWhiteSpace(c) || !seen.Add(c)) continue;
                        sb.Append(c);
                    }
                }
            }
            return sb.ToString();
        }

        // Every game language with our real translations, in the font the game gives it (switched as its Settings window
        // does, not saved; our lookup set to it): the options window (every section and its descriptions), the Mount
        // Journal on every mount, and its colours window measured (TextRoom: GK2-text-room.tsv, the lines "<id>: ...").
        // A one-line text without room at its smallest size fails; a wrapped text cut (more lines than its box holds) is a
        // warning. The player's language, mount and windows put back after.
        internal static IEnumerator Layout()
        {
            MountSettingsPage page = PageWithOptions();
            GameSettings gs = GameSettings.Instance;
            if (page == null || gs == null)
            {
                SelfTest.Skip(page == null ? "no mod's settings page with options is shown" : "the game's settings aren't there");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            string languageWas = gs.language, overrideWas = Texts.LanguageOverride, chosenWas = page.MountChosen;
            bool? testWas = Texts.TestForced;
            var ids = new List<string>(LLBase.AvailableLanguages.Keys);
            int languages = 0, texts = 0;
            try
            {
                Texts.TestForced = false;
                foreach (string id in ids)
                {
                    if (Texts.IsEnglishId(id)) continue; // English: the Windows scenario's "english: ..." lines
                    Switch(gs, id);
                    Texts.LanguageOverride = id;
                    Texts.Forget();
                    yield return SelfTest.Frames(2);
                    languages++;
                    var shortOnes = new List<(string Text, float Room, bool Wraps)>();
                    int measured = 0;
                    OptionsWindow.Open(page.Page);
                    yield return SelfTest.Frames(2);
                    if (OptionsWindow.IsShown)
                    {
                        foreach (string name in SectionsOf(page))
                        {
                            OptionsWindow.TestShowSection(name);
                            yield return SelfTest.Frames(1);
                            measured += TextRoom.Measure(OptionsWindow.TestRoot, $"{id}: options: {name}", shortOnes).Count;
                            List<(string Name, string Text)> descriptions = EveryDescriptionOf(page, name);
                            measured += descriptions.Count;
                            TextRoom.MeasureIn(OptionsWindow.TestDescriptionBox, $"{id}: options: {name} (descriptions)", descriptions, shortOnes);
                            // the Defaults question, with this section's name in it (only looked at, never answered)
                            OptionsWindow.TestOpenConfirm();
                            yield return SelfTest.Frames(1);
                            if (OptionsWindow.TestConfirmRoot != null) measured += TextRoom.Measure(OptionsWindow.TestConfirmRoot, $"{id}: options: {name} (Defaults question)", shortOnes).Count;
                            else SelfTest.Warn($"{id}: the Defaults question of {name} didn't open; not measured");
                            OptionsWindow.TestConfirmNo();
                            yield return SelfTest.Frames(1);
                        }
                        OptionsWindow.TestClose(back: true);
                        yield return SelfTest.Frames(2);
                    }
                    else SelfTest.Warn($"{id}: the options window didn't open; not measured");
                    MountsPanel.Open(page, toggle: false);
                    yield return SelfTest.Frames(3);
                    if (MountsPanel.IsOpen)
                    {
                        if (!string.IsNullOrEmpty(chosenWas))
                        {
                            foreach (MountsPanel.Entry e in MountsPanel.Entries())
                            {
                                if (e.Fake) continue;
                                MountsPanel.TestSelect(e.Id);
                                yield return SelfTest.Frames(3);
                                measured += TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), $"{id}: journal: {e.Id}", shortOnes).Count;
                                // and with a skin not installed (its pack gone): the Skin row by the name alone, the stats
                                // card's "Skin: Not installed" (2026-11-07: "Armored (не установлено)" didn't fit in ru, pl)
                                BepInEx.Configuration.ConfigEntry<string> skinRow = SkinEntry(page, e.Id);
                                if (skinRow != null)
                                {
                                    string skinWas = skinRow.Value;
                                    try
                                    {
                                        skinRow.Value = e.Id + "/" + MissingSkinId;
                                        Relist(page);
                                        yield return SelfTest.Frames(3);
                                        measured += TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), $"{id}: journal: {e.Id} (a skin not installed)", shortOnes).Count;
                                    }
                                    finally
                                    {
                                        skinRow.Value = skinWas;
                                        Relist(page);
                                    }
                                    yield return SelfTest.Frames(3);
                                }
                                // its colours window (each mount has its own colour rows), when it has one
                                MountsPanel.TestOpenColours();
                                yield return SelfTest.Frames(3);
                                if (MountsPanel.IsColoursOpen)
                                {
                                    measured += TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: true), $"{id}: colours window: {e.Id}", shortOnes).Count;
                                    MountsPanel.TestStepBack();
                                    yield return SelfTest.Frames(2);
                                }
                            }
                            MountsPanel.TestSelect(chosenWas);
                            yield return SelfTest.Frames(3);
                        }
                        else
                        {
                            measured += TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), $"{id}: journal", shortOnes).Count;
                            MountsPanel.TestOpenColours();
                            yield return SelfTest.Frames(3);
                            if (MountsPanel.IsColoursOpen)
                            {
                                measured += TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: true), $"{id}: colours window", shortOnes).Count;
                                MountsPanel.TestStepBack();
                                yield return SelfTest.Frames(2);
                            }
                        }
                        MountsPanel.TestStepBack();
                        yield return SelfTest.Frames(2);
                    }
                    else SelfTest.Warn($"{id}: the Mount Journal didn't open; not measured");
                    texts += measured;
                    Verdict(id, shortOnes);
                }
            }
            finally
            {
                if (OptionsWindow.IsOpen) OptionsWindow.TestClose(back: true);
                if (MountsPanel.IsColoursOpen) MountsPanel.TestStepBack();
                if (MountsPanel.IsOpen)
                {
                    if (!string.IsNullOrEmpty(chosenWas)) MountsPanel.TestSelect(chosenWas);
                    MountsPanel.TestStepBack();
                }
                Switch(gs, languageWas);
                Texts.LanguageOverride = overrideWas;
                Texts.TestForced = testWas;
                Texts.Forget();
            }
            SelfTest.Info($"layout: {languages} language(s), {texts} texts measured in their own fonts (BepInEx\\GK2-text-room.tsv, the lines starting with a language id); the game's language back to {languageWas}");
        }

        // A language's texts without room (TextRoom): a one-line text that doesn't fit at its smallest size fails, a wrapped
        // text cut (more lines than its box holds) warns; the same text in several places is said once.
        private static void Verdict(string id, List<(string Text, float Room, bool Wraps)> shortOnes)
        {
            var oneLine = new List<string>();
            var cut = new List<string>();
            foreach (var s in shortOnes)
            {
                string said = $"\"{(s.Text.Length > 40 ? s.Text.Substring(0, 40) + "..." : s.Text)}\" {s.Room:0.00}";
                if (s.Wraps) { if (!cut.Contains(said)) cut.Add(said); }
                else if (!oneLine.Contains(said)) oneLine.Add(said);
            }
            SelfTest.Check(oneLine.Count == 0, $"{id}: every one-line text fits at its smallest size{(oneLine.Count > 0 ? " (NOT: " + string.Join(", ", oneLine) + ")" : "")}");
            if (cut.Count > 0) SelfTest.Warn($"{id}: wrapped texts cut (more lines than their box holds): {string.Join(", ", cut)}");
        }

        // The game's own widgets our words go into, in every language (the layout test measures our windows): the Keeper's
        // thought bubble (UIDialogBubble; Bubble.Talk refreshes its content fitter after showing a message) with the
        // language's longest sentence, said for real, and the interaction hint on foot (a UIObjectBubble; its row's label
        // has a LayoutElement) with the longest prompt, over the nearest mount of the scene. Measured as the layout test
        // measures (TextRoom, Verdict). A game is needed, the bubbles on; the player's language back after.
        internal static IEnumerator GameBubbles()
        {
            GameSettings gs = GameSettings.Instance;
            if (!Mount.IsGameReady || gs == null || MainGame.PlayerData == null)
            {
                SelfTest.Skip("not in a game");
                yield break;
            }
            if (!Plugin.ShowMessages.Value)
            {
                SelfTest.Skip("the bubbles are off (General.ShowMessages)");
                yield break;
            }
            // the nearest mount of the player's scene, for the hint
            Mount.Instance over = null;
            string scene = MainGame.PlayerData.currentGameSceneId;
            Vector3 k = MainGame.PlayerData.position.Value;
            float best = float.MaxValue;
            foreach (Mount.Instance i in Mount.All)
            {
                if (!i.Ready || i.SceneId != scene) continue;
                float d = (i.Position - k).sqrMagnitude;
                if (d < best) { best = d; over = i; }
            }
            string languageWas = gs.language, overrideWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            var ids = new List<string>(LLBase.AvailableLanguages.Keys);
            int languages = 0, bubbles = 0, hints = 0;
            try
            {
                Texts.TestForced = false;
                foreach (string id in ids)
                {
                    Switch(gs, id);
                    Texts.LanguageOverride = id;
                    Texts.Forget();
                    yield return SelfTest.Frames(2);
                    languages++;
                    var shortOnes = new List<(string Text, float Room, bool Wraps)>();
                    // the Keeper's longest sentence in this language (a template with a long name and a number in it)
                    string english = null, shown = null;
                    foreach (Word w in UiWords.All)
                    {
                        if (!w.Key.StartsWith("say.", StringComparison.Ordinal)) continue;
                        string e = w.English.IndexOf('{') < 0 ? w.English : w.Make("Bartholomew the Second", 12);
                        string s = UiWords.Shown(e);
                        if (shown == null || s.Length > shown.Length) { english = e; shown = s; }
                    }
                    Mount.Say(english);
                    yield return SelfTest.Frames(20);
                    string start = shown.Length > 10 ? shown.Substring(0, 10) : shown;
                    Transform bubble = null;
                    var seen = new List<string>();
                    foreach (UIDialogBubble b in UnityEngine.Object.FindObjectsByType<UIDialogBubble>(FindObjectsSortMode.None))
                    {
                        if (b == null || !b.isActiveAndEnabled) continue;
                        // (the game types a bubble in: its whole text with a transparent colour tag at the typing position,
                        // LazyBearTechnology.TextAnimator, so the text is compared without its tags)
                        foreach (TextMeshProUGUI t in b.GetComponentsInChildren<TextMeshProUGUI>(true))
                        {
                            string plain = WithoutTags(t.text);
                            if (plain.Length > 0) seen.Add(plain.Length > 30 ? plain.Substring(0, 30) + "..." : plain);
                            if (plain.Contains(start)) bubble = b.transform;
                        }
                    }
                    if (bubble != null)
                    {
                        bubbles++;
                        TextRoom.Measure(bubble, $"{id}: the Keeper's bubble: {shown}", shortOnes);
                    }
                    else SelfTest.Warn($"{id}: the Keeper's bubble with \"{start}...\" wasn't found; not measured (the bubbles seen: {(seen.Count > 0 ? string.Join(" | ", seen) : "none")})");
                    // the longest prompt over the nearest mount
                    if (over != null)
                    {
                        string prompt = null;
                        foreach (Word w in UiWords.All)
                        {
                            if (!w.Key.StartsWith("prompt.", StringComparison.Ordinal)) continue;
                            string p = w.English.IndexOf('{') < 0 ? w.Text : w.Format("Bartholomew the Second");
                            if (prompt == null || p.Length > prompt.Length) prompt = p;
                        }
                        InstanceControls.TestShowHint(over, prompt);
                        yield return SelfTest.Frames(4);
                        Transform hint = InstanceControls.TestHintRoot();
                        if (hint != null)
                        {
                            hints++;
                            // what the label is drawn in, and whether it cuts what is past its box (2026-11-01: a CJK
                            // font's line is taller than the label, drawn over its edges)
                            SelfTest.Info($"{id}: the prompt's label: {LabelFacts(hint)}");
                            TextRoom.Measure(hint, $"{id}: the prompt on foot: {prompt}", shortOnes);
                        }
                        else SelfTest.Warn($"{id}: the prompt \"{prompt}\" wasn't shown by the game's bubble manager; not measured");
                        InstanceControls.TestHideHint();
                    }
                    Verdict(id, shortOnes);
                }
            }
            finally
            {
                InstanceControls.TestHideHint();
                Switch(gs, languageWas);
                Texts.LanguageOverride = overrideWas;
                Texts.TestForced = testWas;
                Texts.Forget();
            }
            if (over == null) SelfTest.Info("no mount in this scene: the prompt on foot not measured (stand near one and run again)");
            SelfTest.Info($"the game's widgets: {languages} language(s), {bubbles} bubble(s) and {hints} prompt(s) measured (BepInEx\\GK2-text-room.tsv); the game's language back to {languageWas}");
        }

        // A bubble's labels as drawn: the font, the font its style gives the language now, the box, the width its text
        // wants in its font now, its lines, whether it cuts what is past its box.
        private static string LabelFacts(Transform root)
        {
            var said = new List<string>();
            foreach (TextMeshProUGUI l in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (l == null || string.IsNullOrEmpty(l.text)) continue;
                string styleFont = "no style";
                TextStyleComponent style = l.GetComponent<TextStyleComponent>();
                if (style != null)
                {
                    try { styleFont = style.CurrentTextStyle?.Font?.GetFontAssetFor(LLBase.CurrentLang, false)?.name ?? "none"; }
                    catch (Exception e) { styleFont = "unknown (" + e.Message + ")"; }
                }
                float wants = 0f;
                int lines = 0;
                try
                {
                    wants = l.GetPreferredValues(l.text, float.PositiveInfinity, float.PositiveInfinity).x;
                    l.ForceMeshUpdate();
                    lines = l.textInfo != null ? l.textInfo.lineCount : 0;
                }
                catch (Exception e) { said.Add($"{l.name}: couldn't be measured ({e.Message})"); continue; }
                said.Add($"{l.name}: font {(l.font != null ? l.font.name : "none")}, its style's for the language {styleFont}, width {l.rectTransform.rect.width:0}, height {l.rectTransform.rect.height:0}, its text wants {wants:0}, {lines} line(s), overflow {l.overflowMode}{(TextRoom.Clips(l) ? ", clips" : ", draws over its edges")}");
            }
            return said.Count > 0 ? string.Join("; ", said) : "no label";
        }

        // A text without its rich-text tags (<color=...>, </color>, <b>...).
        private static string WithoutTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '<') depth++;
                else if (c == '>' && depth > 0) depth--;
                else if (depth == 0) sb.Append(c);
            }
            return sb.ToString();
        }

        // Every text the options window's description box can show in a section: each row's description as it is; greyed
        // with the reason after it, for a row that can be greyed (a condition of its own, or a button outside a game); a
        // key's with how to set it, by mouse and by gamepad, and the "press the key" line while it is set; the hints shown
        // over no row.
        private static List<(string Name, string Text)> EveryDescriptionOf(MountSettingsPage page, string section)
        {
            var list = new List<(string Name, string Text)>
            {
                ("(no row, mouse)", OptionsWindow.DescriptionOf(null, false, false, false)),
                ("(no row, gamepad)", OptionsWindow.DescriptionOf(null, false, false, true)),
            };
            foreach (SettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || (r.ShownSection ?? r.Section) != section || (r.Visible != null && !r.Visible())) continue;
                list.Add((r.Label, OptionsWindow.DescriptionOf(r, true, false, false)));
                if (r.Enabled != null || r.RowKind == SettingsPage.RowKind.Button) list.Add((r.Label + " (greyed)", OptionsWindow.DescriptionOf(r, false, false, false)));
                if (r.RowKind != SettingsPage.RowKind.Key) continue;
                list.Add((r.Label + " (gamepad)", OptionsWindow.DescriptionOf(r, true, false, true)));
                list.Add((r.Label + " (being set)", OptionsWindow.DescriptionOf(r, true, true, false)));
            }
            return list;
        }

        // The game's language switched as its Settings window does (GameSettings, then its labels and their fonts), not saved.
        // The Settings window's options button after each language switch, before any hover (the user, 2026-11-01: it didn't
        // fit, sometimes didn't translate, until hovered): its words the language's, its font the game's own OK button's
        // restyled as a hover would (the game restyles a button's label only when its state changes), its text within its
        // width.
        internal static IEnumerator SettingsButtonLanguages()
        {
            GameSettings gs = GameSettings.Instance;
            MountSettingsPage page = null;
            foreach (MountSettingsPage p in MountSettingsPage.All) if (p.Shown) { page = p; break; }
            if (gs == null || page == null)
            {
                SelfTest.Skip(gs == null ? "no game settings" : "no page shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
            if (settings == null)
            {
                SelfTest.Skip("no Settings window");
                yield break;
            }
            string languageWas = gs.language, overrideWas = Texts.LanguageOverride;
            var ids = new List<string>(LLBase.AvailableLanguages.Keys);
            int checkedLanguages = 0;
            var wrong = new List<string>();
            try
            {
                Texts.LanguageOverride = null;
                settings.onClosed = null;
                settings.Open(null);
                yield return SelfTest.Frames(2);
                foreach (string id in ids)
                {
                    Switch(gs, id);
                    yield return SelfTest.Frames(1);
                    UIDialogWindowButton ours = SettingsButtons.ButtonOf(settings, page.Page);
                    UIDialogWindowButton ok = UiKit.OkButtonOf(settings);
                    // the OK button keeps its font until its state changes (a hover; "OK" reads in the Latin font): restyled
                    // here as a hover would, the font the game's own button style gives the language
                    ok?.LazyButton?.RefreshTextTransitions();
                    TextMeshProUGUI label = ours != null ? ours.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                    TextMeshProUGUI okLabel = ok != null ? ok.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                    if (label == null)
                    {
                        wrong.Add(id + ": no button");
                        continue;
                    }
                    checkedLanguages++;
                    string want = UiWords.PageOptions.Format(Texts.Title(page.Page));
                    float wants = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x, has = label.rectTransform.rect.width;
                    if (label.text != want) wrong.Add($"{id}: the words \"{label.text}\", not \"{want}\"");
                    if (okLabel != null && label.font != okLabel.font) wrong.Add($"{id}: the font {(label.font != null ? label.font.name : "none")}, the OK button's {(okLabel.font != null ? okLabel.font.name : "none")}");
                    if (wants > has + 0.5f) wrong.Add($"{id}: the text wants {wants:0} of {has:0}");
                }
                SelfTest.Check(checkedLanguages == ids.Count && wrong.Count == 0, $"every language ({checkedLanguages} of {ids.Count}): the button's words, its font the OK button's and its width right at the switch, before any hover{(wrong.Count > 0 ? "; NOT: " + string.Join("; ", wrong) : "")}");
            }
            finally
            {
                Switch(gs, languageWas);
                Texts.LanguageOverride = overrideWas;
                Texts.Forget();
                if (LazyWindowsStackController.IsWindowOpened(settings)) settings.Close();
            }
        }

        private static void Switch(GameSettings gs, string id)
        {
            gs.language = id;
            gs.ApplyLanguageSettings(applySave: false);
            GUIElements.Instance?.UpdateLocalizedLabels();
            TextStyleComponent.RefreshAll();
        }

        private static List<string> SectionsOf(MountSettingsPage page)
        {
            var names = new List<string>();
            foreach (SettingsPage.Row r in page.Rows) if (page.InOptions(r) && !names.Contains(r.ShownSection ?? r.Section)) names.Add(r.ShownSection ?? r.Section);
            return names;
        }

        // A section's rows' descriptions as the description line shows them (in the language now).
        private static List<(string Name, string Text)> DescriptionsOf(MountSettingsPage page, string section)
        {
            var list = new List<(string Name, string Text)>();
            foreach (SettingsPage.Row r in page.Rows)
            {
                if (page.InOptions(r) && (r.ShownSection ?? r.Section) == section && (r.Visible == null || r.Visible())) list.Add((r.Label, OptionsWindow.TestDescription(r)));
            }
            return list;
        }

        // The three windows in English, each section, its descriptions too: the room every text has now (TextRoom, "english: ...").
        private static IEnumerator MeasureEnglish(MountSettingsPage page)
        {
            OptionsWindow.Open(page.Page);
            yield return SelfTest.Frames(2);
            if (OptionsWindow.IsShown)
            {
                foreach (string name in SectionsOf(page))
                {
                    OptionsWindow.TestShowSection(name);
                    yield return SelfTest.Frames(1);
                    TextRoom.Measure(OptionsWindow.TestRoot, "english: options: " + name);
                    TextRoom.MeasureIn(OptionsWindow.TestDescriptionBox, "english: options: " + name + " (descriptions)", DescriptionsOf(page, name));
                }
                OptionsWindow.TestClose(back: true);
                yield return SelfTest.Frames(2);
            }
            MountsPanel.Open(page, toggle: false);
            yield return SelfTest.Frames(3);
            if (!MountsPanel.IsOpen) yield break;
            TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: false), "english: journal");
            MountsPanel.TestOpenColours();
            yield return SelfTest.Frames(3);
            if (MountsPanel.IsColoursOpen)
            {
                TextRoom.Measure(MountsPanel.TestRoot(coloursWindow: true), "english: colours window");
                MountsPanel.TestStepBack();
                yield return SelfTest.Frames(2);
            }
            MountsPanel.TestStepBack();
            yield return SelfTest.Frames(2);
        }

        // The words a window draws that aren't ours to look up: key names (D8), the player's own words, an unnamed skin's id.
        // The mounts' and skins' names are looked up since step 3.
        private static HashSet<string> KeepWords(MountSettingsPage page)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (SettingsPage.Row r in page.Rows)
            {
                if (r.RowKind == SettingsPage.RowKind.Key && r.Entry != null) keep.Add(KeyCapture.Words((BepInEx.Configuration.KeyboardShortcut)r.Entry.BoxedValue));
                if (r.RowKind == SettingsPage.RowKind.List && r.ChoiceLabels != null)
                {
                    for (int i = 0; i < r.ChoiceLabels.Length; i++)
                    {
                        Looks.SkinDef s = r.ChoiceValues != null && i < r.ChoiceValues.Length ? Looks.Find(r.ChoiceValues.GetValue(i) as string) : null;
                        if (s == null || string.IsNullOrEmpty(s.Name)) keep.Add(r.ChoiceLabels[i]); // no name: its id
                    }
                }
                if (r.RowKind == SettingsPage.RowKind.Text && r.Entry?.BoxedValue is string typed && typed.Length > 0) keep.Add(typed); // the player's own words (a mount's name)
            }
            return keep;
        }

        private static List<string> TextsUnder(Transform root)
        {
            var list = new List<string>();
            if (root == null) return list;
            foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(false)) if (!string.IsNullOrWhiteSpace(t.text)) list.Add(t.text);
            return list;
        }

        private static TextMeshProUGUI LabelOf(GameObject row)
        {
            foreach (TextMeshProUGUI t in row.GetComponentsInChildren<TextMeshProUGUI>(true)) if (t.name.EndsWith("label")) return t;
            return null;
        }

        private static bool IsBracketed(string s) => s.Length >= 2 && s[0] == '[' && s[s.Length - 1] == ']';

        // A field of a comma CSV as a spreadsheet writes it: quoted when it holds a comma, a quote or a line break.
        private static string Csv(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";

        // The long-text rule (UiKit.OwnButton): under the test language every one-line text that shrinks (a button, a
        // dropdown's face, a card) fits at its smallest size; a wrapped one that still doesn't is said (it ends in "..." by
        // design, the journal's two-line description).
        private static void RoomHolds(string where, List<(string Text, float Shrunk, bool Wraps)> shrinking)
        {
            var tight = new List<string>();
            var cut = new List<string>();
            foreach (var x in shrinking)
            {
                if (x.Shrunk >= 1f) continue;
                string s = $"\"{(x.Text.Length > 30 ? x.Text.Substring(0, 30) + "..." : x.Text)}\" {x.Shrunk:0.00}";
                if (x.Wraps) cut.Add(s);
                else tight.Add(s);
            }
            SelfTest.Check(tight.Count == 0, $"{where}: every one-line text that shrinks fits at its smallest size in the test language ({(tight.Count == 0 ? shrinking.Count + " checked" : "NOT: " + string.Join(", ", tight))})");
            if (cut.Count > 0) SelfTest.Info($"{where}: wrapped texts still cut with \"...\" at their smallest in the test language: {string.Join(", ", cut)}");
        }

        // The texts drawn without brackets that have letters and aren't names or keys: words that miss the lookup, said.
        private static void Leftovers(string where, List<string> texts, HashSet<string> keep)
        {
            var left = new List<string>();
            foreach (string drawn in texts)
            {
                string s = drawn.Trim('​'); // a text box's text ends in a zero-width space (TMP_InputField)
                if (IsBracketed(s) || keep.Contains(s) || left.Contains(s)) continue;
                bool letters = false;
                foreach (char c in s) if (char.IsLetter(c)) { letters = true; break; }
                if (letters) left.Add(s.Length > 40 ? s.Substring(0, 40) + "..." : s);
            }
            SelfTest.Info($"the test language, {where}: {(left.Count == 0 ? "every word looked up" : left.Count + " text(s) not looked up: " + string.Join(" | ", left))}");
        }
    }
}
