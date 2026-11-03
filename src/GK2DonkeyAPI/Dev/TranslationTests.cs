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
    // catalogue (RunCatalogue, also run without the game by tests/TranslationScan). All put back the run's English
    // (SelfTest.Run sets it).
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
            MountSettingsPage.Row skinRow = null;
            int skinAt = -1;
            if (page != null && skin != null)
            {
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    if (r.RowKind != MountSettingsPage.RowKind.List) continue;
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

        // ---- the translator mistake catalogue (docs/localization.md, "Translator mistakes") ----

        // One mistake: the files a person's spreadsheet or editor wrote, the game's language, what each key must show
        // (the English when the reader can't know the translation: never a wrong text) and whether the translator is told.
        private sealed class Mistake
        {
            internal string Name, Lang = "fr";
            internal readonly List<(string Where, string File, byte[] Bytes)> Files = new List<(string, string, byte[])>();
            internal readonly List<(string Key, string Shows, object[] Args, bool MayDrop)> Shows = new List<(string, string, object[], bool)>();
            internal bool Told;
            internal bool Discover; // the translators' folders found by the real search over a made-up plugins folder

            internal Mistake File(string where, string name, byte[] bytes) { Files.Add((where, name, bytes)); return this; }
            internal Mistake Discovered() { Discover = true; return this; }
            internal Mistake Show(string key, string shows) { Shows.Add((key, shows, null, false)); return this; }
            internal Mistake Fill(string key, string shows, bool mayDrop, params object[] args) { Shows.Add((key, shows, args, mayDrop)); return this; }
        }

        private const string MistakeOwner = "test.gk2.l10n.mistakes";

        // The made-up owner's English: dotted keys as every real key is (a header in other words is told from a key so).
        private static readonly List<(string Key, string English, string What)> MistakeEnglish = new List<(string, string, string)>
        {
            ("t.greet", "Hello", null), ("t.bye", "Bye", null), ("t.fmt", "Ride {0}", null), ("t.noun", "This {0} pulls", null),
            ("t.one", "One", null), ("t.two", "Two", null), ("t.three", "Three", null), ("t.four", "Four", null), ("t.lines", "First line", null), ("t.ask", "Ready?", null),
            ("t.pair", "Ride, then stop", null),
        };

        private static string EnglishOf(string key)
        {
            foreach (var x in MistakeEnglish) if (x.Key == key) return x.English;
            return key;
        }

        private static byte[] U8(string s, bool bom = false)
        {
            byte[] b = new UTF8Encoding(false).GetBytes(s);
            if (!bom) return b;
            var all = new byte[b.Length + 3];
            all[0] = 0xEF; all[1] = 0xBB; all[2] = 0xBF;
            Array.Copy(b, 0, all, 3, b.Length);
            return all;
        }

        private static byte[] Page(string lang, string s)
        {
            LegacyText.Encode(s, LegacyText.PageFor(lang, out _), out byte[] b);
            return b;
        }

        // UTF-8 that a spreadsheet opened in the language's Windows code page and saved again as UTF-8 ("Ã©" for "é").
        private static byte[] Garble(string s, string lang) => U8(LegacyText.Decode(U8(s), 0, LegacyText.PageFor(lang, out _)));

        private static byte[] Join(params byte[][] parts)
        {
            var all = new List<byte>();
            foreach (byte[] p in parts) all.AddRange(p);
            return all.ToArray();
        }

        private static byte[] Utf16(string s, bool bom)
        {
            byte[] b = Encoding.Unicode.GetBytes(s);
            if (!bom) return b;
            var all = new byte[b.Length + 2];
            all[0] = 0xFF; all[1] = 0xFE;
            Array.Copy(b, 0, all, 2, b.Length);
            return all;
        }

        private static List<Mistake> Catalogue()
        {
            const string ok = "key,value,english\nt.greet,Bonjour,Hello\n";
            var c = new List<Mistake>();
            Mistake M(string name, string lang = "fr") { var m = new Mistake { Name = name, Lang = lang }; c.Add(m); return m; }
            // what it's saved as
            M("plain UTF-8 with a BOM (the template's own way)").File("bundled", "fr.csv", U8(ok, bom: true)).Show("t.greet", "Bonjour");
            M("Excel's plain \"CSV\": Windows-1252, ; separated: not read, said (save as CSV UTF-8)").File("bundled", "fr.csv", Page("fr", "key;value;english\r\nt.greet;Été, œuvre ’ok’ €;Hello\r\n")).Show("t.greet", "Hello").Told = true;
            M("Polish in Windows-1250: not read, said", "pl").File("bundled", "pl.csv", Page("pl", "key,value,english\nt.greet,Cześć łódź ąę,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("Russian in Windows-1251: not read, said", "ru").File("bundled", "ru.csv", Page("ru", "key,value,english\nt.greet,Привет ёлка Ёж,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("Turkish in Windows-1254: not read, said", "tr").File("bundled", "tr.csv", Page("tr", "key,value,english\nt.greet,Merhaba ğüşı İz,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("Japanese not in UTF-8 (Shift-JIS): not readable, English", "ja").File("bundled", "ja.csv", new byte[] { 0x74, 0x2E, 0x67, 0x72, 0x65, 0x65, 0x74, 0x2C, 0x82, 0xA0, 0x2C, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x0A }).Show("t.greet", "Hello").Told = true;
            M("Excel's \"Unicode Text\": UTF-16 with tabs, saved as fr.txt").File("bundled", "fr.txt", Utf16("key\tvalue\tenglish\r\nt.greet\tBonjour\tHello\r\n", bom: true)).Show("t.greet", "Bonjour").Told = true;
            M("UTF-16 without its BOM").File("bundled", "fr.csv", Utf16("key,value,english\r\nt.greet,Bonjour,Hello\r\n", bom: false)).Show("t.greet", "Bonjour");
            M("a workbook saved with a .csv name").File("bundled", "fr.csv", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 }).Show("t.greet", "Hello").Told = true;
            M("a workbook beside: fr.xlsx").File("bundled", "fr.xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }).Show("t.greet", "Hello").Told = true;
            M("UTF-8 opened as ANSI and saved again (Ã©): left out, said (the repair is off)").File("bundled", "fr.csv", U8(LegacyText.Decode(U8("t.greet,Été déjà là ’ok’,Hello\nt.bye,À bientôt,Bye\n"), 0, LegacyText.Western))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("garbled letters, only two (Ã¶ for ö): left out, said", "de").File("bundled", "de.csv", Garble("key,value,english\nt.greet,Schöne Tür,Hello\n", "de")).Show("t.greet", "Hello").Told = true;
            M("garbled Cyrillic (UTF-8 opened as Windows-1251 and saved again): left out, said", "ru").File("bundled", "ru.csv", Garble("key,value,english\nt.greet,Привет ослик,Hello\n", "ru")).Show("t.greet", "Hello").Told = true;
            M("a correct Russian text with ё after lowercase letters (полёт, ещё, её): read as it is", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,\"Нажмите ещё раз в полёте, её взлёт и полёт\",Hello\n")).Show("t.greet", "Нажмите ещё раз в полёте, её взлёт и полёт");
            M("garbled Polish (Windows-1250): left out, said", "pl").File("bundled", "pl.csv", Garble("key,value,english\nt.greet,Cześć łódź,Hello\n", "pl")).Show("t.greet", "Hello").Told = true;
            M("garbled Turkish (Windows-1254): left out, said", "tr").File("bundled", "tr.csv", Garble("key,value,english\nt.greet,Merhaba ğüşı İz,Hello\n", "tr")).Show("t.greet", "Hello").Told = true;
            M("garbled twice: left out, said").File("bundled", "fr.csv", U8(LegacyText.Decode(Garble("key,value,english\nt.greet,Été déjà,Hello\n", "fr"), 0, LegacyText.Western))).Show("t.greet", "Hello").Told = true;
            M("UTF-8 with one line saved in Windows-1252 (an edit in another program): not read, said").File("bundled", "fr.csv", Join(U8("key,value,english\nt.greet,Été déjà,Hello\n"), Page("fr", "key,value,english\nt.bye,À bientôt,Bye\n"))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("garbled letters that can't be repaired: that line English").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Ã©tÃ© ✓,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("UTF-16 big-endian without its BOM, Japanese", "ja").File("bundled", "ja.csv", Encoding.BigEndianUnicode.GetBytes("key,value,english\r\nt.greet,こんにちは,Hello\r\n")).Show("t.greet", "こんにちは");
            M("UTF-16 without its BOM, a Japanese letter first (a title line before the header: said)", "ja").File("bundled", "ja.csv", Utf16("日本語訳\r\nkey,value,english\r\nt.greet,こんにちは,Hello\r\n", bom: false)).Show("t.greet", "こんにちは").Told = true;
            M("a header in Japanese words (キー,値,英語): not read, said", "ja").File("bundled", "ja.csv", U8("キー,値,英語\nt.greet,こんにちは,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("empty file").File("bundled", "fr.csv", new byte[0]).Show("t.greet", "Hello");
            M("only a header").File("bundled", "fr.csv", U8("key,value,english\n")).Show("t.greet", "Hello");
            M("random bytes").File("bundled", "fr.csv", new byte[] { 0xFF, 0x00, 0x13, 0x80, 0x22, 0x0A, 0x2C, 0xC3, 0x28, 0x22, 0x22, 0x0A, 0x81 }).Show("t.greet", "Hello").Told = true;
            M("old Mac line ends (CR only)").File("bundled", "fr.csv", U8("key,value,english\rt.greet,Bonjour,Hello\rt.bye,Au revoir,Bye\r")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            // the columns
            M("no header, ; separated: not read, said").File("bundled", "fr.csv", U8("t.greet;Bonjour;Hello\nt.bye;Au revoir;Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a header in other words (clé;valeur;anglais): not read, said (copy en.csv's header)").File("bundled", "fr.csv", U8("clé;valeur;anglais\nt.greet;Bonjour;Hello\n")).Show("t.greet", "Hello").Told = true;
            M("Excel's sep= line").File("bundled", "fr.csv", U8("sep=;\nkey;value;english\nt.greet;Bonjour;Hello\n")).Show("t.greet", "Bonjour");
            M("each row whole in one cell").File("bundled", "fr.csv", U8("\"key,value,english\"\n\"t.greet,Bonjour,Hello\"\n\"t.bye,Au revoir,Bye\"\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("two columns, no english: English, said (nothing says which English a line translates)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("the english column deleted, the note kept (key,value,note): English, said").File("bundled", "fr.csv", U8("key,value,note\nt.greet,Hello,a greeting\nt.bye,Bye,leaving\nt.two,Two,a number\nt.one,Un,a number\n")).Show("t.greet", "Hello").Show("t.one", "One").Told = true;
            M("the same without its header: English, said").File("bundled", "fr.csv", U8("t.greet,Hello,a greeting\nt.bye,Bye,leaving\nt.two,Two,a number\nt.one,Un,a number\n")).Show("t.greet", "Hello").Show("t.one", "One").Told = true;
            M("a column of the translator's own after the english (todo): read, the column said").File("bundled", "fr.csv", U8("key,value,english,todo\nt.greet,Bonjour,Hello,check\n")).Show("t.greet", "Bonjour").Told = true;
            M("a column of the translator's own, no header: not read, said").File("bundled", "fr.csv", U8("t.greet,Bonjour,Hello,TODO\n")).Show("t.greet", "Hello").Told = true;
            M("the english column before the translation (key,english,value): not read, said").File("bundled", "fr.csv", U8("key,english,value\nt.greet,Hello,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("one line's english left empty: that line English, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\nt.bye,Au revoir,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("the whole file still the English (en.csv saved as fr.csv)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Hello,Hello\nt.bye,Bye,Bye\n")).Show("t.greet", "Hello");
            M("a comment first, then ; rows with a comma in a quoted text").File("bundled", "fr.csv", U8("# French, by Ann, Bob; thanks\nkey;value;english\nt.greet;\"Bonjour, toi\";Hello\n")).Show("t.greet", "Bonjour, toi");
            M("a blank first line, then ; rows").File("bundled", "fr.csv", U8("\nkey;value;english\nt.greet;\"Bonjour, toi\";Hello\n")).Show("t.greet", "Bonjour, toi");
            M("spaces around fields, empty columns after").File("bundled", "fr.csv", U8("key , value , english\n t.greet , Bonjour , Hello ,,,\n")).Show("t.greet", "Bonjour");
            M("a second header (two files put together)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\nkey,value,english\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("comment rows").File("bundled", "fr.csv", U8("# my notes, with a comma\nkey,value,english\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Bonjour");
            M("a row with a key and nothing else").File("bundled", "fr.csv", U8("key,value,english\nt.greet\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            // quotes
            M("a quote inside a text").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Il a dit \"bonjour\",Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Il a dit \"bonjour\"").Show("t.bye", "Au revoir");
            M("a quoted text with \"\" and the separator").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Il dit \"\"oui\"\", puis non\",Hello\n")).Show("t.greet", "Il dit \"oui\", puis non");
            M("a quote never closed: that line English, the rest read").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Bonjour,Hello\nt.bye,Au revoir,Bye\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("a quote that swallows the rows after it").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Bonjour\" dit-il,Hello\nt.bye,Au revoir,Bye\nt.one,\"Un\",One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("a text over lines with an \"e.g.,\" line").File("bundled", "fr.csv", U8("key,value,english\nt.lines,\"Première\ne.g., deux\",First line\nt.bye,Au revoir,Bye\n")).Show("t.lines", "Première e.g., deux").Show("t.bye", "Au revoir");
            M("a text over two lines in quotes (a one-line text: a space)").File("bundled", "fr.csv", U8("key,value,english\nt.lines,\"Première\nligne\",First line\n")).Show("t.lines", "Première ligne");
            // keys
            M("a key in other case, invisible characters").File("bundled", "fr.csv", U8("key,value,english\nT.Greet,Bonjour,Hello\n\u200Bt.bye\uFEFF,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("a key typed wrong: left out, said (the rescue by its English is off)").File("bundled", "fr.csv", U8("key,value,english\nt.greeet,Bonjour,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a key translated, its English unknown: left out").File("bundled", "fr.csv", U8("key,value,english\nt.salut,Bonjour,Salut\n")).Show("t.greet", "Hello").Told = true;
            M("a key twice: the first").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\nt.greet,Salut,Hello\n")).Show("t.greet", "Bonjour").Told = true;
            // the english column
            M("value and english columns swapped: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.one,One,Un\nt.two,Two,Deux\nt.three,Three,Trois\nt.four,Four,Quatre\n")).Show("t.one", "One").Show("t.four", "Four").Told = true;
            M("the english column translated too: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.one,Un,Un\nt.two,Deux,Deux\nt.three,Trois,Trois\n")).Show("t.one", "One").Show("t.three", "Three").Told = true;
            M("one line's English changed since: English").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Salut,Hi there\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir");
            M("every line's English changed since (a stale file, or notes in the english column): English, said").File("bundled", "fr.csv", U8("key,value,english\nt.one,Un,Uno\nt.two,Deux,Dos\nt.three,Trois,Tres\n")).Show("t.one", "One").Show("t.three", "Three").Told = true;
            // the texts
            M("a literal \\n").File("bundled", "fr.csv", U8("key,value,english\nt.two,Deux\\nlignes,Two\n")).Show("t.two", "Deux lignes");
            M("{ 0 } with spaces").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter { 0 },Ride {0}\n")).Fill("t.fmt", "Monter Bella", false, "Bella");
            M("｛０｝ of Japanese input", "ja").File("bundled", "ja.csv", U8("key,value,english\nt.fmt,｛０｝に乗る,Ride {0}\n")).Fill("t.fmt", "Bellaに乗る", false, "Bella");
            M("{O}, a letter O").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {O},Ride {0}\n")).Fill("t.fmt", "Monter Bella", false, "Bella");
            M("a template that lost its {0}: English").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter,Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a {1} the English hasn't: English").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {1},Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a broken brace: English").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {0,Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a broken template, asked twice: English both times").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {0} },Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("joining characters inside a text kept, invisible ones at its edges taken off").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\u200BBon\u200Djour\u200B,Hello\n")).Show("t.greet", "Bon\u200Djour");
            M("a noun the word allows to drop").File("bundled", "fr.csv", U8("key,value,english\nt.noun,Elle tire,This {0} pulls\n")).Fill("t.noun", "Elle tire", true, "donkey");
            M("a spreadsheet error cell (#NAME?): English").File("bundled", "fr.csv", U8("key,value,english\nt.greet,#NAME?,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("a very long text").File("bundled", "fr.csv", U8("key,value,english\nt.greet," + new string('a', 5000) + ",Hello\n")).Show("t.greet", new string('a', 5000));
            M("<nobr> tags").File("bundled", "fr.csv", U8("key,value,english\nt.greet,<nobr>Bonjour</nobr>,Hello\n")).Show("t.greet", "Bonjour");
            // names
            M("FR.csv (upper case)").File("bundled", "FR.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("fr-FR.csv (a region): read, said").File("bundled", "fr-FR.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("pt.csv for pt-br (the base language): read, said", "pt-br").File("bundled", "pt.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr - Copy.csv: a copy, said, not read").File("bundled", "fr - Copy.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("fr_old.csv: a copy, said, not read").File("bundled", "fr_old.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("fr (1).csv: a download's copy, said, not read").File("bundled", "fr (1).csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("a copy beside fr.csv: fr.csv read, the copy said").File("bundled", "fr.csv", U8(ok)).File("bundled", "fr - Copy.csv", U8("key,value,english\nt.greet,Salut,Hello\n")).Show("t.greet", "Bonjour").Told = true;
            M("zh-TW.csv in a zh_cn game: another language, not read", "zh_cn").File("bundled", "zh-TW.csv", U8(ok)).Show("t.greet", "Hello");
            M("french.csv (a language's name)").File("bundled", "french.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("français.csv").File("bundled", "français.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr.csv.txt (a hidden extension)").File("bundled", "fr.csv.txt", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr.txt in the old key = value way: English").File("bundled", "fr.txt", U8("key,value,english\nt.greet = Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("pt_BR.csv for pt-br", "pt-br").File("bundled", "pt_BR.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("zh-CN.csv for zh_cn", "zh_cn").File("bundled", "zh-CN.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("jp.csv for ja", "ja").File("bundled", "jp.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("de.csv in a French game: not read").File("bundled", "de.csv", U8("key,value,english\nt.greet,Hallo,Hello\n")).Show("t.greet", "Hello");
            // places
            M("en.csv translated in place: English, said").File("bundled", "en.csv", U8("key,value,english\nt.one,Un,One\nt.two,Deux,Two\nt.three,Trois,Three\n")).Show("t.one", "One").Told = true;
            M("beside the DLL, not in Languages\\").File("plugin", "fr.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("straight in Translations\\, no mod folder").File("translations", "fr.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("Translations\\ folder named after the mod, not its GUID").File("byname", "fr.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("a translator's own beats the bundled").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("key,value,english\nt.greet,Salut,Hello\n")).Show("t.greet", "Salut");
            M("a translator's line older than the bundled one: the bundled").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("key,value,english\nt.greet,Salut,Hi there\nt.bye,Ciao,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Ciao");
            M("a translator's half-done copy of the English: the bundled where it is still English").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("key,value,english\nt.greet,Hello,Hello\nt.one,One,One\nt.bye,Salut,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Salut");
            M("a small translator's file with one word left English on purpose: kept").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("key,value,english\nt.greet,Hello,Hello\nt.bye,Salut,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Salut");
            M("a translator's fr-FR.csv beats the bundled fr.csv").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr-FR.csv", U8("key,value,english\nt.greet,Salut,Hello\n")).Show("t.greet", "Salut").Told = true;
            // code review round 2 (2026-11-01)
            M("letters lost as ? (Russian saved as plain CSV on a Western Windows)", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,?????? ?????,Hello\nt.fmt,{0}???,Ride {0}\n")).Show("t.greet", "Hello").Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("letters lost as ? inside words (Polish)", "pl").File("bundled", "pl.csv", U8("key,value,english\nt.greet,Cze?? ?ód?,Hello\nt.bye,?ó?ko,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("Excel's CSV (MS-DOS): French in code page 850: not read, said").File("bundled", "fr.csv", Join(U8("key,value,english\nt.greet,"), new byte[] { 0x90, 0x74, 0x82, 0x20, 0x85, 0x20, 0x63, 0x93, 0x74, 0x82 }, U8(",Hello\nt.bye,Au revoir,Bye\n"))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("Excel's CSV (MS-DOS): German in code page 850: not read, said", "de").File("bundled", "de.csv", Join(U8("key,value,english\nt.greet,Sch"), new byte[] { 0x94 }, U8("ne Gr"), new byte[] { 0x81, 0xE1 }, U8("e,Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("Excel's CSV (MS-DOS): Russian in code page 866: not read, said", "ru").File("bundled", "ru.csv", Join(U8("key,value,english\nt.greet,"), new byte[] { 0x8F, 0xE0, 0xA8, 0xA2, 0xA5, 0xE2 }, U8(",Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("Excel's CSV (MS-DOS): Polish in code page 852: not read, said", "pl").File("bundled", "pl.csv", Join(U8("key,value,english\nt.greet,Cze"), new byte[] { 0x98, 0x86, 0x20, 0x88, 0xA2, 0x64, 0xAB }, U8(",Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("Excel's CSV (MS-DOS): Turkish in code page 857: not read, said", "tr").File("bundled", "tr.csv", Join(U8("key,value,english\nt.greet,Merhaba "), new byte[] { 0xA7, 0x81, 0x9F, 0x8D, 0x98 }, U8(",Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("Excel's CSV (Macintosh): French in Mac Roman: not read, said").File("bundled", "fr.csv", Join(U8("key,value,english\nt.greet,"), new byte[] { 0x83, 0x74, 0x8E, 0x20, 0x88, 0x20, 0x63, 0x99, 0x74, 0x8E }, U8(",Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("Excel's CSV (Macintosh): Russian in Mac Cyrillic: not read, said", "ru").File("bundled", "ru.csv", Join(U8("key,value,english\nt.greet,"), new byte[] { 0x8F, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2 }, U8(",Hello\n"))).Show("t.greet", "Hello").Told = true;
            M("spreadsheet error cells in other languages (#ИМЯ?, #¿NOMBRE?, #NUL!, #N/D): English").File("bundled", "fr.csv", U8("key,value,english\nt.one,#ИМЯ?,One\nt.two,#¿NOMBRE?,Two\nt.three,#NUL!,Three\nt.four,#N/D,Four\nt.greet,#1 du classement,Hello\n")).Show("t.one", "One").Show("t.two", "Two").Show("t.three", "Three").Show("t.four", "Four").Show("t.greet", "#1 du classement").Told = true;
            M("{{0}} (the habit of other translation tools)").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {{0}},Ride {0}\n")).Fill("t.fmt", "Monter Bella", false, "Bella");
            M("garbled by Mac Excel (√© for é): left out, said").File("bundled", "fr.csv", U8(LegacyText.Decode(U8("t.greet,Été à côté,Hello\n"), 0, LegacyText.MacRoman))).Show("t.greet", "Hello").Told = true;
            M("garbled Japanese (UTF-8 read as Shift-JIS, saved again): English", "ja").File("bundled", "ja.csv", U8("key,value,english\nt.greet,縺薙ｓ縺ｫ縺｡縺ｯ,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("garbled Chinese (UTF-8 read as GBK, saved again): English", "zh_cn").File("bundled", "zh_cn.csv", U8("key,value,english\nt.greet,浣犲ソ鏈嬪弸,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a correct short Russian text that looks like UTF-8 in its page (Её, ЧЁ): read as it is", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,Её,Hello\nt.bye,ЧЁ,Bye\n")).Show("t.greet", "Её").Show("t.bye", "ЧЁ");
            M("correct Russian typography (no-break spaces after В, before a dash): read as it is", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,\"Это\u00A0— осёл, это\u00A0— конь, а\u00A0это\u00A0— скакун\",Hello\nt.bye,В\u00A0сундуке. С\u00A0телегой. К\u00A0Смотрителю.,Bye\n")).Show("t.greet", "Это\u00A0— осёл, это\u00A0— конь, а\u00A0это\u00A0— скакун").Show("t.bye", "В\u00A0сундуке. С\u00A0телегой. К\u00A0Смотрителю.");
            M("a stray quote swallows a row with spaces around its key").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Bonjour\" dit-il,Hello\n t.bye , Au revoir , Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("a stray quote swallows a row whose key is quoted").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Bonjour\" dit-il,Hello\n\"t.bye\",\"Au revoir\",\"Bye\"\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("a stray quote before a row of a key the mod doesn't have: that row left out, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\"Bonjour\" dit-il,Hello\ngreet.old,\"Salut\",Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("the report's rows added under the old ones (a key twice, the first out of date)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hi there\nt.bye,Hello,Hello\nt.greet,Salut à tous,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Salut à tous").Show("t.bye", "Au revoir");
            M("the translation typed into a column named after the language beside the value column: English, said (another column is never taken)").File("bundled", "fr.csv", U8("key,value,french,english\nt.greet,Hello,Bonjour,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("the translation in a column named after the language, no value column, after the english (key,english,french): not read, said").File("bundled", "fr.csv", U8("key,english,french\nt.greet,Hello,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("Translations\\ straight in plugins\\ (no folder of its own)").Discovered().File("loose", "fr.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("a folder unzipped into one of its own name (plugins\\X\\X\\Translations)").Discovered().File("doubled", "fr.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("Languages\\ inside the mod's Translations folder").File("theirsLanguages", "fr.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("a translator's deliberate English word over the bundled translation (a translated file)").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("key,value,english\nt.greet,Hello,Hello\nt.bye,Salut,Bye\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Salut");
            M("jp_JP.csv for ja", "ja").File("bundled", "jp_JP.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("kr_KR.csv for ko", "ko").File("bundled", "kr_KR.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("french_FR.csv: read, said").File("bundled", "french_FR.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr with no extension: read, said").File("bundled", "fr", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("a text that is only a line break or tags: English").File("bundled", "fr.csv", U8("key,value,english\nt.greet,\\n,Hello\nt.bye,<nobr> </nobr>,Bye\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Show("t.one", "Un");
            M("a Japanese file with a line not in UTF-8: not read (it must be UTF-8 throughout)", "ja").File("bundled", "ja.csv", Join(U8("key,value,english\nt.lines,\"一行目\n"), new byte[] { 0x82, 0xA0 }, U8("\n三行目\",First line\nt.greet,こんにちは,Hello\n"))).Show("t.lines", "First line").Show("t.greet", "Hello").Told = true;
            M("a key typed wrong before the right one: the right one").File("bundled", "fr.csv", U8("key,value,english\nt.greeet,Salut,Hello\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Bonjour").Told = true;
            M("one stray Windows-1252 byte in a UTF-8 line: not read, said").File("bundled", "fr.csv", Join(U8("key,value,english\nt.greet,Été l"), new byte[] { 0x92 }, U8("an,Hello\nt.bye,Au revoir,Bye\n"))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            // code review round 3 (2026-11-01): an encoding is the file's; the sweep in tests/TranslationScan checks the shipped files saved every wrong way
            M("a correct Windows-1251 file whose bytes look like UTF-8 here and there (Щёлкните, Чёрный): not read, said", "ru").File("bundled", "ru.csv", Page("ru", "key,value,english\nt.greet,Щёлкните,Hello\nt.bye,Чёрный,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("Excel's CSV (Macintosh) with CR line ends (German in Mac Roman): not read, said", "de").File("bundled", "de.csv", Join(U8("key,value,english\nt.greet,H"), new byte[] { 0x9A }, U8("llenfeuer,Hello\rt.bye,Tsch"), new byte[] { 0x9F }, U8("ss,Bye\r"))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("Korean saved in its Windows page (949): not read", "ko").File("bundled", "ko.csv", Join(U8("key,value,english\nt.greet,"), new byte[] { 0xC5, 0xB0 }, U8(",Hello\nt.bye,"), new byte[] { 0xBE, 0xC8, 0xB3, 0xE7 }, U8(",Bye\n"))).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a Latin letter alone in a Russian text (Её saved as Windows-1251 inside a UTF-8 file): English", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,Ÿ,Hello\nt.bye,Пока,Bye\n", bom: true)).Show("t.greet", "Hello").Show("t.bye", "Пока").Told = true;
            M("a Chinese character in a French text (UTF-8 garbled through GBK): English").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour 茅,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("Mac Roman garbling by a Windows program (its Ω is U+2126): left out, said", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet,–æ–\u2126,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("Mac Roman garbled Cyrillic with a character the repair can't take: English", "ru").File("bundled", "ru.csv", U8("key,value,english\nt.greet," + LegacyText.Decode(U8("Привет"), 0, LegacyText.MacRoman) + " ✓,Hello\nt.bye,Пока,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Пока").Told = true;
            M("a one-letter word lost as ? (Japanese on a Western Windows): English", "ja").File("bundled", "ja.csv", U8("key,value,english\nt.greet,?,Hello\nt.bye,???,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a letter lost as ? at a word's end (Polish): English", "pl").File("bundled", "pl.csv", U8("key,value,english\nt.greet,Przywró?,Hello\nt.bye,Dzie? dobry,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a real question mark where the English has one: read").File("bundled", "fr.csv", U8("key,value,english\nt.ask,Prêt ?,Ready?\n")).Show("t.ask", "Prêt ?");
            M("a real question mark in Spanish: read", "es").File("bundled", "es.csv", U8("key,value,english\nt.ask,¿Listo?,Ready?\n")).Show("t.ask", "¿Listo?");
            M("a pandas index column before the key (,key,value,english): read without it, said").File("bundled", "fr.csv", U8(",key,value,english,note\n0,t.greet,Bonjour,Hello,\n1,t.bye,Au revoir,Bye,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir").Told = true;
            M("a rescue never shows a key as the text").File("bundled", "fr.csv", U8("key,value,english\nx.1,t.greet,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a reviewer's column named after the language beside the value column: the value column").File("bundled", "fr.csv", U8("key,value,english,note,fr\nt.greet,Bonjour,Hello,,ok\nt.bye,Au revoir,Bye,,todo\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir").Told = true;
            M("the translation typed into a column of its own beside the template's value: English, said").File("bundled", "fr.csv", U8("key,value,english,note,translation\nt.greet,Hello,Hello,,Bonjour\nt.bye,Bye,Bye,,Au revoir\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("an empty column named after the language and no value column: English, said").File("bundled", "fr.csv", U8("key,english,fr\nt.greet,Hello,\nt.bye,Bye,\n")).Show("t.greet", "Hello").Told = true;
            M("a filled-in report kept as fr.missing.csv: said, not read").File("bundled", "fr.missing.csv", U8("key,value,english\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a comma inside a text without quotes, its English after it: English, said (the join is off)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour, toi,Hello\nt.bye,Oui, non,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a quote inside a quoted text, not doubled: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.bye,\"Bonjour\" dit-il, puis \"salut\",Bye\nt.greet,Bonjour,Hello\n")).Show("t.bye", "Bye").Show("t.greet", "Bonjour").Told = true;
            M("a {0} the English hasn't, in a plain text: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour {0},Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a header in the translator's language, reordered, the english first (key;Englisch;Deutsch): not read, said", "de").File("bundled", "de.csv", U8("key;Englisch;Deutsch\nt.greet;Hello;Hallo\n")).Show("t.greet", "Hello").Told = true;
            // code review round 4 (2026-11-01): a code-page file is read through every page its language's programs save in (the sweep's small files)
            M("a comma row with a comma inside its text, in a ; file: English, said").File("bundled", "fr.csv", U8("key;value;english\nt.greet,Bonjour, toi,Hello\nt.bye;Au revoir;Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("an index column that ends at a second header without one (files put together)").File("bundled", "fr.csv", U8(",key,value,english\n0,t.greet,Bonjour,Hello\nkey,value,english\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir").Told = true;
            M("a Turkish header, the english first (key;İngilizce;Türkçe): not read, said", "tr").File("bundled", "tr.csv", U8("key;İngilizce;Türkçe\nt.greet;Hello;Merhaba\n")).Show("t.greet", "Hello").Told = true;
            M("an ASCII ? before the next sentence in Japanese: read", "ja").File("bundled", "ja.csv", U8("key,value,english\nt.ask,準備はいい?出発しよう,Ready?\n")).Show("t.ask", "準備はいい?出発しよう");
            // code review round 5 (2026-11-01): code-page files aren't read (TranslationFiles.ReadCodePages)
            M("a lone CR ending rows among LF rows").File("bundled", "fr.csv", U8("key,value,english\rt.greet,D'accord,Hello\rt.bye,Bien,Bye\nt.one,Un,One\r")).Show("t.greet", "D'accord").Show("t.bye", "Bien").Show("t.one", "Un");
            M("CR rows with a line break inside a quoted cell (Mac Excel)").File("bundled", "fr.csv", U8("key,value,english\rt.lines,\"Première\nligne\",First line\rt.one,Un,One\r")).Show("t.lines", "Première ligne").Show("t.one", "Un");
            M("notes in the translator's language in the english column's place (key,value,nota): English, said", "es").File("bundled", "es.csv", U8("key,value,nota\nt.greet,Hola,saludo\nt.bye,Adiós,despedida\n")).Show("t.greet", "Hello").Told = true;
            M("notes in the english column's place, no header: English, said", "es").File("bundled", "es.csv", U8("t.greet,Hola,saludo\nt.bye,Adiós,despedida\nt.one,Uno,número\n")).Show("t.greet", "Hello").Show("t.one", "One").Told = true;
            M("a stale line beside a column of the new English: never joined to it").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Salut toi,Hi there,Hello\n")).Show("t.greet", "Hello");
            M("symbols and an emoji in a text: read").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour ♥ → ✓ ≥ 😀,Hello\n")).Show("t.greet", "Bonjour ♥ → ✓ ≥ 😀");
            // code review round 6 (2026-11-01): the repairs are off (TranslationFiles.RepairGarbled, Texts.RescueByEnglish, Texts.JoinSeparator)
            M("a multi-line cell whose last line starts with a web address (www.exemple.fr): read whole").File("bundled", "fr.csv", U8("key,value,english\nt.lines,\"Première ligne,\nwww.exemple.fr, deux\",First line\nt.bye,Au revoir,Bye\n")).Show("t.lines", "Première ligne, www.exemple.fr, deux").Show("t.bye", "Au revoir");
            M("a header naming no translation column (key,english,): English, said").File("bundled", "fr.csv", U8("key,english,\nt.greet,Hello,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("no header, an empty first column: not read, said").File("bundled", "fr.csv", U8(",t.greet,Bonjour,Hello\n,t.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("numbered rows without a header: not read, said").File("bundled", "fr.csv", U8("1,t.greet,Bonjour,Hello\n2,t.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("an empty column after the key, no header: English, said").File("bundled", "fr.csv", U8("t.greet,,Bonjour,Hello\nt.bye,,Au revoir,Bye\n")).Show("t.greet", "Hello").Told = true;
            M("an index column and a stray quote: the rows after it read, said").File("bundled", "fr.csv", U8(",key,value,english\n0,t.greet,\"Bonjour\" dit-il,Hello\n1,t.bye,Au revoir,Bye\n2,t.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("a control character in a text: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bon\u000Bjour,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            // code review round 7 (2026-11-01)
            M("rows whole in one cell with a quote inside not doubled (Excel in a ; country re-saving): English, said").File("bundled", "fr.csv", U8("key,value,english\n\"t.greet,\"\"Bonjour\"\" dit-il, \"\"salut\"\",Hello\"\n\"t.one,\"\"Un\"\" et \"\"deux\"\",One\"\n")).Show("t.greet", "Hello").Show("t.one", "One").Told = true;
            M("a comma row with a quote inside not doubled, in a ; file: English, said").File("bundled", "fr.csv", U8("key;value;english\nt.greet,\"Bonjour\" et \"salut\",Hello\nt.bye;Au revoir;Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("a two-line text cut by Excel's re-save into one cell a line: English, said").File("bundled", "fr.csv", U8("key,english,value\n\"t.lines,First line,\"\"Première ligne\"\n\"deuxième ligne\"\"\"\nt.bye,Bye,Au revoir\n")).Show("t.lines", "First line").Show("t.bye", "Bye").Told = true;
            M("a full-width brace that is no placeholder: kept", "ja").File("bundled", "ja.csv", U8("key,value,english\nt.greet,｛注意｝こんにちは,Hello\nt.bye,さようなら｛笑｝,Bye\n")).Show("t.greet", "｛注意｝こんにちは").Show("t.bye", "さようなら｛笑｝");
            M("a header naming the value column in the translator's words (key,valeur,anglais,note): not read, said").File("bundled", "fr.csv", U8("key,valeur,anglais,note\nt.greet,Bonjour,Hello,un salut\n")).Show("t.greet", "Hello").Told = true;
            M("a header naming no value column, two columns it could be (key,checked,texte,anglais): English, said").File("bundled", "fr.csv", U8("key,checked,texte,anglais\nt.greet,x,Bonjour,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a quote opening a comment row swallows the rows after it: read line by line, said").File("bundled", "fr.csv", U8("key,value,english\n\"# Section : fenêtres, par Ann\nt.greet,Bonjour,Hello\nt.bye,Au revoir,Bye\nt.lines,\"Première ligne\",First line\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir").Show("t.lines", "Première ligne").Told = true;
            M("a quote opening a key swallows the rows after it: read line by line, said").File("bundled", "fr.csv", U8("key,value,english\n\"t.greet,Bonjour,Hello\nt.bye,Au revoir,Bye\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("an empty key column with text after it, under a header: English, said").File("bundled", "fr.csv", U8("key,value,english\n,t.greet,Bonjour,Hello\n,Un,One\n")).Show("t.greet", "Hello").Told = true;
            M("{{ }} with no number: kept as written").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour {{ }} salut,Hello\n")).Show("t.greet", "Bonjour {{ }} salut");
            // code review round 8 (2026-11-01): the english before the translation is refused; more cells than the header is a cut text
            M("an unquoted comma inside a text, the english column first: not read, said").File("bundled", "fr.csv", U8("key,english,value\nt.greet,Hello,Bonjour, toi\n")).Show("t.greet", "Hello").Told = true;
            M("two rows joined by a deleted line break: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,a greetingt.bye,Au revoir,Bye,leaving\nt.one,Un,One,\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("a stray separator before the text, under a header: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,,Bonjour,Hello,a greeting\nt.one,Un,One,\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("Excel in a ; country re-saving with its padding (each row in one cell, then ;)").File("bundled", "fr.csv", U8("\"key,value,english,note\";\n\"t.greet,Bonjour,Hello,a greeting\";\n\"t.one,Un,One,a number\";\n")).Show("t.greet", "Bonjour").Show("t.one", "Un");
            M("doubled braces in a template's translation: English, said").File("bundled", "fr.csv", U8("key,value,english\nt.fmt,Monter {0} {{x}},Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a second header with two columns the value could be, after a good section: the good section read").File("bundled", "fr.csv", U8("key,value,english\nt.bye,Au revoir,Bye\nkey,checked,texte,anglais\nt.greet,x,Bonjour,Hello\n")).Show("t.bye", "Au revoir").Show("t.greet", "Hello").Told = true;
            M("a translation column beside the value column, used on one line: said").File("bundled", "fr.csv", U8("key,value,english,translation\nt.greet,Bonjour,Hello,\nt.bye,Bye,Bye,Au revoir\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            // code review round 9 (2026-11-01): the header's columns by en.csv's names only
            M("a header naming its columns otherwise (key,Value (French),Notes (translator),English): not read, said").File("bundled", "fr.csv", U8("key,Value (French),Notes (translator),English\nt.greet,Bonjour,check,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("a notes column named with a translation word beside the value (key,value,english,Notes de traduction): read, the column said").File("bundled", "fr.csv", U8("key,value,english,Notes de traduction\nt.greet,Bonjour,Hello,à vérifier\n")).Show("t.greet", "Bonjour").Told = true;
            M("a reordered header in other words (clé,note,valeur,anglais): not read, said").File("bundled", "fr.csv", U8("key,value,english\nclé,note,valeur,anglais\nt.bye,keep it,Bye,Bye\nt.greet,salut,Bonjour,Hello\n")).Show("t.bye", "Bye").Show("t.greet", "Hello").Told = true;
            M("Excel's padding after the header and a comma typed into a text: English, said").File("bundled", "fr.csv", U8("key,value,english,note,,\nt.greet,Bon,jour,Hello,,\nt.one,Un,One,,,\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("joined rows without a header: not read, said").File("bundled", "fr.csv", U8("t.greet,Bonjour,Hello,t.bye,Au revoir,Bye,\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Show("t.one", "One").Told = true;
            M("a comma typed into the english text: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hel,lo,\nt.one,Un,One,\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("a comma typed at a text's start under a header: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,,Bonjour,Hello,\nt.one,Un,One,\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("an English with a comma, its quotes deleted in the english column: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.pair,\"Monter, puis s'arrêter\",Ride, then stop\nt.one,Un,One,\n")).Show("t.pair", "Ride, then stop").Show("t.one", "Un").Told = true;
            M("an English with a comma, quoted as en.csv has it: read").File("bundled", "fr.csv", U8("key,value,english,note\nt.pair,\"Monter, puis s'arrêter\",\"Ride, then stop\",\nt.one,Un,One,\n")).Show("t.pair", "Monter, puis s'arrêter").Show("t.one", "Un");
            M("a column without a name after the note, holding the translator's notes: read, the column said").File("bundled", "fr.csv", U8("key,value,english,note,\nt.greet,Bonjour,Hello,,à vérifier\nt.one,Un,One,,\n")).Show("t.greet", "Bonjour").Show("t.one", "Un").Told = true;
            M("a comma typed at a line's start in a headerless file: not read, said").File("bundled", "fr.csv", U8(",t.greet,Bonjour,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            // code review round 10 (2026-11-01): read from en.csv's header row on; a header in other words under it told by its width
            M("a file without en.csv's header row, in a translation tool's order (key, note, english, value): not read, said").File("bundled", "fr.csv", U8("t.greet,une salutation,Hello,Bonjour\nt.bye,un départ,Bye,Au revoir\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a translation tool's header (ID,Status,English,Français): not read, said").File("bundled", "fr.csv", U8("ID,Status,English,Français\nt.greet,fait,Hello,Bonjour\nt.bye,à revoir,Bye,Au revoir\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a key header with a dot in its words (Key (e.g. t.greet),Context,English,French): not read, said").File("bundled", "fr.csv", U8("Key (e.g. t.greet),Context,English,French\nt.greet,une salutation,Hello,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("the key header cleared (,note,english,value): not read, said").File("bundled", "fr.csv", U8(",note,english,value\nt.greet,une salutation,Hello,Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("a section pasted under a translation tool's header (ID,Status,English,Français): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.one,Un,One,\nID,Status,English,Français\nt.greet,fait,Hello,Bonjour\nt.bye,à revoir,Bye,Au revoir\n")).Show("t.one", "Un").Show("t.greet", "Hello").Show("t.bye", "Bye").Told = true;
            M("a section pasted under a cleared key header (,note,english,value): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.one,Un,One,\n,note,english,value\nt.greet,une salutation,Hello,Bonjour\n")).Show("t.one", "Un").Show("t.greet", "Hello").Told = true;
            M("a section pasted under a key header with a dot in its words: not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.one,Un,One,\nKey (e.g. t.greet),Context,English,French\nt.greet,une salutation,Hello,Bonjour\n")).Show("t.one", "Un").Show("t.greet", "Hello").Told = true;
            M("a numbered row under a header without an index column: that line only, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\n1,t.bye,Au revoir,Bye\nt.one,Un,One\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("the header in capitals, quoted, with spaces and a no-break space: read").File("bundled", "fr.csv", U8("\"KEY\", Value ,english\u00A0,Note\nt.greet,Bonjour,Hello,\n")).Show("t.greet", "Bonjour");
            M("the header typed on a Turkish keyboard in capitals (KEY,VALUE,ENGLİSH,NOTE): read", "tr").File("bundled", "tr.csv", U8("KEY,VALUE,ENGLİSH,NOTE\nt.greet,Merhaba,Hello,\n")).Show("t.greet", "Merhaba");
            M("comments and a blank line before the header: read").File("bundled", "fr.csv", U8("# French\n\n# by Ann\nkey,value,english\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Bonjour");
            M("two rows joined under a header with a column without a name: English, said").File("bundled", "fr.csv", U8("key,value,english,note,\nt.greet,Bonjour,Hello,,à vérifier\nt.one,Un,Onet.bye,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.one", "One").Show("t.bye", "Bye").Told = true;
            M("two rows joined under Excel's padded header: English, said").File("bundled", "fr.csv", U8("key;value;english;note;;;;\nt.greet;Bonjour;Hello;;t.bye;Au revoir;Bye;\nt.one;Un;One;;;;;\n")).Show("t.greet", "Hello").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("a text cut by a comma typed into it, its tail the English: English, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour, Hello,Hello,\nt.one,Un,One,\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("a comment line without # under a header, fewer commas than its columns: the lines after it not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nChecked up to here, the rest later, ok\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.one", "One").Told = true;
            M("a comment line without # as wide as the header's columns: the lines after it not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nChecked up to here, the rest later, ok, thanks\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.one", "One").Told = true;
            M("a section pasted under a reordered header in other words (clé,note,valeur,anglais): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nclé,note,valeur,anglais\nt.bye,keep it,Bye,Bye\nt.one,,Un,One\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Show("t.one", "One").Told = true;
            M("a header in other words as wide as a three-column header (key,value,english, then clé,note,valeur): not read, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\nclé,note,valeur\nt.bye,keep it,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("a key without its dot and its english edited, under a header: that line only, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\ntbye,Au revoir,Byee,\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("a title line above en.csv's header (Translated by Marie, 2026, v1.2): read from the header, the title said").File("bundled", "fr.csv", U8("Translated by Marie, 2026, v1.2\nkey,value,english,note\nt.greet,Bonjour,Hello,\n")).Show("t.greet", "Bonjour").Told = true;
            // code review round 11 (2026-11-01): under a header every line is one of the mod's, a # comment or blank
            M("a section pasted under a header in other words with a cell left empty (ID,,English,Français): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,n\nID,,English,Français\nt.bye,x,Bye,Au revoir\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("a section pasted under a header in other words narrower than its rows (ID,Status,English): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nID,Status,English\nt.bye,done,Bye,Au revoir\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("a section pasted under a header in other words, below a header with a name twice: not read, said").File("bundled", "fr.csv", U8("key,value,english,note,Note\nt.greet,Bonjour,Hello,,\nID,Status,English,Français\nt.bye,done,Bye,Au revoir\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("a section pasted under a narrower header in other words with no en.csv name (Clé,Statut,Anglais): not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nClé,Statut,Anglais\nt.bye,ok,Bye,Au revoir\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Told = true;
            M("a section pasted under a header in other words with its key cell empty (,Kontext,Englisch,Deutsch): not read, said", "de").File("bundled", "de.csv", U8("key,value,english,note\nt.greet,Hallo,Hello,\n,Kontext,Englisch,Deutsch\nt.bye,Abschied,Bye,Tschüss\n")).Show("t.greet", "Hallo").Show("t.bye", "Bye").Told = true;
            M("a one-word line without # under a header (TODO): the lines after it not read, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nTODO\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.one", "One").Told = true;
            M("a comma typed into a key before its dot: that line only, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nt,bye,Au revoir,Bye,a note\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("a space typed into a key: that line only, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.greet,Bonjour,Hello,\nt.b ye,Au revoir,Bye,\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.bye", "Bye").Show("t.one", "Un").Told = true;
            M("a stray comma before the header (,key,value,english,note): read, said").File("bundled", "fr.csv", U8(",key,value,english,note\nt.greet,Bonjour,Hello,\nt.one,Un,One,\n")).Show("t.greet", "Bonjour").Show("t.one", "Un").Told = true;
            M("a column of the translator's own before the key (done,key,value,english): not read, said").File("bundled", "fr.csv", U8("done,key,value,english\nx,t.greet,Bonjour,Hello\n")).Show("t.greet", "Hello").Told = true;
            M("translations in a column without a name (key,,value,english,note): the value column read, the column said").File("bundled", "fr.csv", U8("key,,value,english,note\nt.greet,Bonjour,Hello,Hello,a note\n")).Show("t.greet", "Hello").Told = true;
            M("a quote never closed before a text's line holding commas (the line-by-line read): that text only, said").File("bundled", "fr.csv", U8("key,value,english,note\nt.lines,\"Première ligne\nAppuyez sur X, puis Y, puis Z, puis W\nt.greet,Bonjour,Hello,\nt.one,Un,One,\n")).Show("t.lines", "First line").Show("t.greet", "Bonjour").Show("t.one", "Un").Told = true;
            // the first in-game run (2026-11-01): a line typed with spaces for its separators, or as key = value, is the mod's line
            M("a line typed with spaces for its commas, under a header: that line only, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet Bonjour Hello\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("a line typed as key = value, under a header: that line only, said").File("bundled", "fr.csv", U8("key,value,english\nt.greet = Bonjour\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.one", "Un").Told = true;
            M("the report's row of a line still the English, added under it: the translation").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Hello,Hello\nt.bye,Au revoir,Bye\nt.one,Un,One\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Bonjour");
            return c;
        }

        // Every mistake of the catalogue in turn, in a folder of the test's own: what shows, and whether the translator is told.
        internal static IEnumerator Mistakes()
        {
            string root = Path.Combine(Paths.BepInExRootPath, "GK2-l10n-test-mistakes");
            List<(string Name, bool Good, string Why)> results = RunCatalogue(root);
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

        // The catalogue run in root (made and deleted), as the self-test and tests/TranslationScan run it: no Unity, no
        // game; every Texts handle put back. A case is good when every key shows what it must and the translator is told
        // exactly when the case says so (the warnings it gave are in Why).
        internal static List<(string Name, bool Good, string Why)> RunCatalogue(string root)
        {
            var results = new List<(string Name, bool Good, string Why)>();
            string languageWas = Texts.LanguageOverride;
            bool? testWas = Texts.TestForced;
            List<string> rootsWas = Texts.TestTranslators;
            string pluginsWas = Texts.TestPluginsPath;
            List<string> heardWas = TranslationFiles.TestHeard;
            bool quietWas = Texts.TestQuiet;
            try
            {
                Texts.TestEnglish[MistakeOwner] = MistakeEnglish;
                Texts.TestNames[MistakeOwner] = new List<string> { "TestModFolder", "Bad|Name<>:" }; // a plugin name no folder can have: never a throw
                Texts.TestForced = false;
                Texts.TestQuiet = true;
                foreach (Mistake m in Catalogue())
                {
                    var heard = new List<string>();
                    var wrong = new List<string>();
                    string error = null;
                    try
                    {
                        // the case's folders made anew (in the try: a handle Windows releases late fails this case, not the run)
                        string plugin = Path.Combine(root, "plugin"), bundled = Path.Combine(plugin, "Languages");
                        string plugins = Path.Combine(root, "plugins"); // a made-up BepInEx\plugins for the Discovered cases
                        string translator = Path.Combine(root, "translator"), translations = Path.Combine(translator, "Translations");
                        if (Directory.Exists(root)) Directory.Delete(root, true);
                        Directory.CreateDirectory(bundled);
                        Directory.CreateDirectory(Path.Combine(translations, MistakeOwner));
                        foreach (var (where, name, bytes) in m.Files)
                        {
                            string dir = where == "bundled" ? bundled : where == "plugin" ? plugin : where == "translations" ? translations
                                : where == "byname" ? Path.Combine(translations, "TestModFolder")
                                : where == "theirsLanguages" ? Path.Combine(Path.Combine(translations, MistakeOwner), "Languages")
                                : where == "loose" ? Path.Combine(Path.Combine(plugins, "Translations"), MistakeOwner)
                                : where == "doubled" ? Path.Combine(Path.Combine(Path.Combine(Path.Combine(plugins, "X"), "X"), "Translations"), MistakeOwner)
                                : Path.Combine(translations, MistakeOwner);
                            Directory.CreateDirectory(dir);
                            File.WriteAllBytes(Path.Combine(dir, name), bytes);
                        }
                        Texts.TestBundled[MistakeOwner] = bundled;
                        Texts.TestTranslators = m.Discover ? null : new List<string> { translator };
                        Texts.TestPluginsPath = m.Discover ? plugins : null;
                        Texts.LanguageOverride = m.Lang;
                        Texts.TestWarnings = 0;
                        TranslationFiles.TestHeard = heard;
                        Texts.Forget();
                        foreach (var (key, shows, args, mayDrop) in m.Shows)
                        {
                            string got = args == null ? Texts.Of(MistakeOwner, key, EnglishOf(key)) : Texts.Fill(MistakeOwner, key, EnglishOf(key), mayDrop, args);
                            if (got != shows) wrong.Add($"{key} shows \"{Short(got)}\", not \"{Short(shows)}\"");
                        }
                    }
                    catch (Exception e)
                    {
                        error = e.GetType().Name + ": " + e.Message;
                    }
                    bool told = Texts.TestWarnings > 0;
                    bool good = error == null && wrong.Count == 0 && told == m.Told;
                    string why = (error ?? string.Join("; ", wrong)) + (told != m.Told ? (told ? " (told, not expected)" : " (NOT told)") : "")
                        + (heard.Count > 0 ? " [said: " + string.Join(" | ", heard) + "]" : "");
                    results.Add((m.Name, good, why.Trim()));
                }
            }
            finally
            {
                Texts.TestEnglish.Remove(MistakeOwner);
                Texts.TestNames.Remove(MistakeOwner);
                Texts.TestBundled.Remove(MistakeOwner);
                Texts.TestTranslators = rootsWas;
                Texts.TestPluginsPath = pluginsWas;
                Texts.LanguageOverride = languageWas;
                Texts.TestForced = testWas;
                Texts.TestQuiet = quietWas;
                TranslationFiles.TestHeard = heardWas;
                Texts.Forget();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch { } // a folder of the test's own; the next run deletes it first
            }
            return results;
        }

        private static string Short(string s) => s == null ? "(null)" : s.Length > 40 ? s.Substring(0, 40) + "..." : s.Replace("\n", "\\n");

        private static MountSettingsPage PageWithOptions()
        {
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && OptionsWindow.HasOptions(live)) return live;
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
                OptionsWindow.Open(page);
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
                    foreach (MountSettingsPage.Row r in page.Rows)
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
            var chosen = new List<MountSettingsPage.Row>();
            MountSettingsPage.Row stale = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || r.LabelWord != null || r.LabelOf != null || string.IsNullOrEmpty(r.Label) || r.RowKind == MountSettingsPage.RowKind.Note) continue;
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
                foreach (MountSettingsPage.Row r in chosen) csv.Append(Csv(r.TextKey + ".label")).Append(',').Append(Csv("Ä " + r.Label)).Append(',').Append(Csv(r.Label)).Append('\n');
                if (stale != null) csv.Append(Csv(stale.TextKey + ".label")).Append(",Ä alt,An older English of this row\n");
                File.WriteAllText(Path.Combine(pageDir, "de.csv"), csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(apiDir, "de.csv"), "key;value;english\r\noptions.done;Fertig;Done\r\njournal.title;\"Reittier-Tagebuch; Seite\";Mount Journal\r\ncolours.title;Farben: {0};Colours: {0}\r\n", new UTF8Encoding(true));
                Texts.TestBundled[page.OwnerId] = pageDir;
                Texts.TestBundled[Texts.Api] = apiDir;
                Texts.TestForced = false;
                Texts.LanguageOverride = "de";
                Texts.Forget();
                OptionsWindow.Open(page);
                yield return SelfTest.Frames(2);
                SelfTest.Check(OptionsWindow.IsShown, "the options window opens with a German translation");
                if (!OptionsWindow.IsShown) yield break;
                OptionsWindow.TestShowSection(chosen.Count > 0 ? chosen[0].ShownSection ?? chosen[0].Section : null);
                yield return SelfTest.Frames(1);
                int german = 0, shown = 0;
                var seen = new List<string>();
                foreach (MountSettingsPage.Row r in chosen)
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
                    OptionsWindow.Open(page);
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
                    OptionsWindow.Open(page);
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
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || (r.ShownSection ?? r.Section) != section || (r.Visible != null && !r.Visible())) continue;
                list.Add((r.Label, OptionsWindow.DescriptionOf(r, true, false, false)));
                if (r.Enabled != null || r.RowKind == MountSettingsPage.RowKind.Button) list.Add((r.Label + " (greyed)", OptionsWindow.DescriptionOf(r, false, false, false)));
                if (r.RowKind != MountSettingsPage.RowKind.Key) continue;
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
                    UIDialogWindowButton ours = SettingsButtons.ButtonOf(settings, page);
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
                    string want = UiWords.PageOptions.Format(Texts.Title(page));
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
            foreach (MountSettingsPage.Row r in page.Rows) if (page.InOptions(r) && !names.Contains(r.ShownSection ?? r.Section)) names.Add(r.ShownSection ?? r.Section);
            return names;
        }

        // A section's rows' descriptions as the description line shows them (in the language now).
        private static List<(string Name, string Text)> DescriptionsOf(MountSettingsPage page, string section)
        {
            var list = new List<(string Name, string Text)>();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (page.InOptions(r) && (r.ShownSection ?? r.Section) == section && (r.Visible == null || r.Visible())) list.Add((r.Label, OptionsWindow.TestDescription(r)));
            }
            return list;
        }

        // The three windows in English, each section, its descriptions too: the room every text has now (TextRoom, "english: ...").
        private static IEnumerator MeasureEnglish(MountSettingsPage page)
        {
            OptionsWindow.Open(page);
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
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.RowKind == MountSettingsPage.RowKind.Key && r.Entry != null) keep.Add(KeyCapture.Words((BepInEx.Configuration.KeyboardShortcut)r.Entry.BoxedValue));
                if (r.RowKind == MountSettingsPage.RowKind.List && r.ChoiceLabels != null)
                {
                    for (int i = 0; i < r.ChoiceLabels.Length; i++)
                    {
                        Looks.SkinDef s = r.ChoiceValues != null && i < r.ChoiceValues.Length ? Looks.Find(r.ChoiceValues.GetValue(i) as string) : null;
                        if (s == null || string.IsNullOrEmpty(s.Name)) keep.Add(r.ChoiceLabels[i]); // no name: its id
                    }
                }
                if (r.RowKind == MountSettingsPage.RowKind.Text && r.Entry?.BoxedValue is string typed && typed.Length > 0) keep.Add(typed); // the player's own words (a mount's name)
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
