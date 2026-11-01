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
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(bundled);
                Directory.CreateDirectory(theirs);
                // a translator's own pt-br (commas, the template's note column), a bundled pt-br saved with tabs and a bundled
                // pt saved as European Excel does (; and a BOM)
                File.WriteAllText(Path.Combine(theirs, "pt-br.csv"), "key,value,english,note\ngreet,Oi,Hello,\"a note, not read\"\nbye,Tchau,Bye,\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bundled, "pt-br.csv"), "key\tvalue\tenglish\ntabkey\tTab\tTab key\ngreet\tBundled\tHello\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bundled, "pt.csv"),
                    "key;value;english\r\ngreet;Olá;Hello\r\nchanged;Mudado;Old English\r\nnoen;Sem;\r\nempty;;Empty\r\nfmt;Montar {0};Ride {0}\r\n"
                    + "badfmt;Montar {1};Ride {0}\r\ntwo;\"Duas\r\nlinhas\";\"Two\r\nlines\"\r\ncsvonly;\"Só; com \"\"aspas\"\"\";Only CSV\r\na line without a separator\r\n", new UTF8Encoding(true));
                Texts.TestBundled[Owner] = bundled;
                Texts.TestTranslators = new List<string> { translator };
                Texts.TestForced = false;
                Texts.TestQuiet = true;
                Texts.TestWarnings = 0;
                Texts.LanguageOverride = "pt-br";
                Texts.Forget();
                SelfTest.Check(Of("greet", "Hello") == "Oi", $"pt-br: a translator's own pt-br.csv beats the bundled ones ({Of("greet", "Hello")})");
                SelfTest.Check(Of("bye", "Bye") == "Tchau", "a CSV with commas and the template's fourth column (the note, not read)");
                SelfTest.Check(Of("tabkey", "Tab key") == "Tab", "a CSV saved with tabs");
                SelfTest.Check(Of("csvonly", "Only CSV") == "Só; com \"aspas\"", $"a CSV saved with ; and a BOM (European Excel), a quoted field with ; and \"\" ({Of("csvonly", "Only CSV")})");
                SelfTest.Check(Of("changed", "New English") == "New English", "a line whose English changed since shows the English");
                SelfTest.Check(Of("noen", "Anything") == "Sem", "a line with no English recorded is used");
                SelfTest.Check(Of("missing", "Missing") == "Missing" && Of("empty", "Empty") == "Empty", "a line left out or left empty shows the English");
                SelfTest.Check(Texts.Format(Owner, "fmt", "Ride {0}", "Bella") == "Montar Bella", "a template fills in its {0}");
                SelfTest.Check(Texts.Format(Owner, "badfmt", "Ride {0}", "Bella") == "Ride Bella", "a template that doesn't take its values: the English");
                SelfTest.Check(Of("two", "Two\nlines") == "Duas\nlinhas", "a line break inside a quoted field");
                Texts.Table table = Texts.TableOf(Owner);
                SelfTest.Check(table != null && table.Malformed == 1 && Texts.TestWarnings >= 1, $"the line without a separator is left out and said ({table?.Malformed} left out, {Texts.TestWarnings} warning(s), not logged in the test)");
                yield return null;
                Texts.LanguageOverride = "pt";
                Texts.Forget();
                SelfTest.Check(Of("greet", "Hello") == "Olá" && Of("bye", "Bye") == "Tchau" && Of("tabkey", "Tab key") == "Tab", $"pt: its own pt.csv first, then the pt-br files (a close language before the English) ({Of("bye", "Bye")})");
                Texts.LanguageOverride = "de";
                Texts.Forget();
                SelfTest.Check(Of("greet", "Hello") == "Hello", "a language with no file: the English");
                Texts.LanguageOverride = "en";
                Texts.Forget();
                SelfTest.Check(Of("greet", "Hello") == "Hello", "English: the code's, no file read");
                Texts.TestForced = true;
                string t = Of("greet", "Hello"), f = Texts.Format(Owner, "fmt", "Ride {0}", "Bella");
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
                Texts.TestQuiet = false;
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

            internal Mistake File(string where, string name, byte[] bytes) { Files.Add((where, name, bytes)); return this; }
            internal Mistake Show(string key, string shows) { Shows.Add((key, shows, null, false)); return this; }
            internal Mistake Fill(string key, string shows, bool mayDrop, params object[] args) { Shows.Add((key, shows, args, mayDrop)); return this; }
        }

        private const string MistakeOwner = "test.gk2.l10n.mistakes";

        // The made-up owner's English: dotted keys as every real key is (a header in other words is told from a key so).
        private static readonly List<(string Key, string English, string What)> MistakeEnglish = new List<(string, string, string)>
        {
            ("t.greet", "Hello", null), ("t.bye", "Bye", null), ("t.fmt", "Ride {0}", null), ("t.noun", "This {0} pulls", null),
            ("t.one", "One", null), ("t.two", "Two", null), ("t.three", "Three", null), ("t.four", "Four", null), ("t.lines", "First line", null),
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
            M("Excel's plain \"CSV\": Windows-1252, ; separated").File("bundled", "fr.csv", Page("fr", "key;value;english\r\nt.greet;Été, œuvre ’ok’ €;Hello\r\n")).Show("t.greet", "Été, œuvre ’ok’ €").Told = true;
            M("Polish in Windows-1250", "pl").File("bundled", "pl.csv", Page("pl", "key,value,english\nt.greet,Cześć łódź ąę,Hello\n")).Show("t.greet", "Cześć łódź ąę").Told = true;
            M("Russian in Windows-1251", "ru").File("bundled", "ru.csv", Page("ru", "key,value,english\nt.greet,Привет ёЁ,Hello\n")).Show("t.greet", "Привет ёЁ").Told = true;
            M("Turkish in Windows-1254", "tr").File("bundled", "tr.csv", Page("tr", "key,value,english\nt.greet,Merhaba ğüşıİ,Hello\n")).Show("t.greet", "Merhaba ğüşıİ").Told = true;
            M("Japanese not in UTF-8 (Shift-JIS): not readable, English", "ja").File("bundled", "ja.csv", new byte[] { 0x74, 0x2E, 0x67, 0x72, 0x65, 0x65, 0x74, 0x2C, 0x82, 0xA0, 0x2C, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x0A }).Show("t.greet", "Hello").Told = true;
            M("Excel's \"Unicode Text\": UTF-16 with tabs, saved as fr.txt").File("bundled", "fr.txt", Utf16("key\tvalue\tenglish\r\nt.greet\tBonjour\tHello\r\n", bom: true)).Show("t.greet", "Bonjour").Told = true;
            M("UTF-16 without its BOM").File("bundled", "fr.csv", Utf16("key,value,english\r\nt.greet,Bonjour,Hello\r\n", bom: false)).Show("t.greet", "Bonjour");
            M("a workbook saved with a .csv name").File("bundled", "fr.csv", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 }).Show("t.greet", "Hello").Told = true;
            M("a workbook beside: fr.xlsx").File("bundled", "fr.xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }).Show("t.greet", "Hello").Told = true;
            M("UTF-8 opened as ANSI and saved again (Ã©)").File("bundled", "fr.csv", U8(LegacyText.Decode(U8("key,value,english\nt.greet,Été déjà là ’ok’,Hello\nt.bye,À bientôt,Bye\n"), 0, LegacyText.Western))).Show("t.greet", "Été déjà là ’ok’").Show("t.bye", "À bientôt").Told = true;
            M("empty file").File("bundled", "fr.csv", new byte[0]).Show("t.greet", "Hello");
            M("only a header").File("bundled", "fr.csv", U8("key,value,english\n")).Show("t.greet", "Hello");
            M("random bytes").File("bundled", "fr.csv", new byte[] { 0xFF, 0x00, 0x13, 0x80, 0x22, 0x0A, 0x2C, 0xC3, 0x28, 0x22, 0x22, 0x0A, 0x81 }).Show("t.greet", "Hello").Told = true;
            M("old Mac line ends (CR only)").File("bundled", "fr.csv", U8("key,value,english\rt.greet,Bonjour,Hello\rt.bye,Au revoir,Bye\r")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            // the columns
            M("no header, ; separated").File("bundled", "fr.csv", U8("t.greet;Bonjour;Hello\nt.bye;Au revoir;Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("a header in other words (clé;valeur;anglais)").File("bundled", "fr.csv", U8("clé;valeur;anglais\nt.greet;Bonjour;Hello\n")).Show("t.greet", "Bonjour");
            M("Excel's sep= line").File("bundled", "fr.csv", U8("sep=;\nkey;value;english\nt.greet;Bonjour;Hello\n")).Show("t.greet", "Bonjour");
            M("each row whole in one cell").File("bundled", "fr.csv", U8("\"t.greet,Bonjour,Hello\"\n\"t.bye,Au revoir,Bye\"\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("two columns, no english").File("bundled", "fr.csv", U8("t.greet,Bonjour\n")).Show("t.greet", "Bonjour");
            M("spaces around fields, empty columns after").File("bundled", "fr.csv", U8("key , value , english\n t.greet , Bonjour , Hello ,,,\n")).Show("t.greet", "Bonjour");
            M("a second header (two files put together)").File("bundled", "fr.csv", U8("key,value,english\nt.greet,Bonjour,Hello\nkey,value,english\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("comment rows").File("bundled", "fr.csv", U8("# my notes, with a comma\nt.greet,Bonjour,Hello\n")).Show("t.greet", "Bonjour");
            M("a row with a key and nothing else").File("bundled", "fr.csv", U8("t.greet\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            // quotes
            M("a quote inside a text").File("bundled", "fr.csv", U8("t.greet,Il a dit \"bonjour\",Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Il a dit \"bonjour\"").Show("t.bye", "Au revoir");
            M("a quoted text with \"\" and the separator").File("bundled", "fr.csv", U8("t.greet,\"Il dit \"\"oui\"\", puis non\",Hello\n")).Show("t.greet", "Il dit \"oui\", puis non");
            M("a quote never closed: that line English, the rest read").File("bundled", "fr.csv", U8("t.greet,\"Bonjour,Hello\nt.bye,Au revoir,Bye\nt.one,Un,One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("a quote that swallows the rows after it").File("bundled", "fr.csv", U8("t.greet,\"Bonjour\" dit-il,Hello\nt.bye,Au revoir,Bye\nt.one,\"Un\",One\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Show("t.one", "Un").Told = true;
            M("a text over two lines in quotes (a one-line text: a space)").File("bundled", "fr.csv", U8("t.lines,\"Première\nligne\",First line\n")).Show("t.lines", "Première ligne");
            // keys
            M("a key in other case, invisible characters").File("bundled", "fr.csv", U8("T.Greet,Bonjour,Hello\n\u200Bt.bye\uFEFF,Au revoir,Bye\n")).Show("t.greet", "Bonjour").Show("t.bye", "Au revoir");
            M("a key typed wrong, found by its English").File("bundled", "fr.csv", U8("t.greeet,Bonjour,Hello\n")).Show("t.greet", "Bonjour").Told = true;
            M("a key translated, its English unknown: left out").File("bundled", "fr.csv", U8("t.salut,Bonjour,Salut\n")).Show("t.greet", "Hello").Told = true;
            M("a key twice: the first").File("bundled", "fr.csv", U8("t.greet,Bonjour,Hello\nt.greet,Salut,Hello\n")).Show("t.greet", "Bonjour").Told = true;
            // the english column
            M("value and english columns swapped").File("bundled", "fr.csv", U8("t.one,One,Un\nt.two,Two,Deux\nt.three,Three,Trois\nt.four,Four,Quatre\n")).Show("t.one", "Un").Show("t.four", "Quatre").Told = true;
            M("the english column translated too").File("bundled", "fr.csv", U8("t.one,Un,Un\nt.two,Deux,Deux\nt.three,Trois,Trois\n")).Show("t.one", "Un").Show("t.three", "Trois").Told = true;
            M("one line's English changed since: English").File("bundled", "fr.csv", U8("t.greet,Salut,Hi there\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir");
            M("most lines' English changed since (a stale file): English").File("bundled", "fr.csv", U8("t.one,Un,Uno\nt.two,Deux,Dos\nt.three,Trois,Tres\n")).Show("t.one", "One").Show("t.three", "Three");
            // the texts
            M("a literal \\n").File("bundled", "fr.csv", U8("t.two,Deux\\nlignes,Two\n")).Show("t.two", "Deux lignes");
            M("{ 0 } with spaces").File("bundled", "fr.csv", U8("t.fmt,Monter { 0 },Ride {0}\n")).Fill("t.fmt", "Monter Bella", false, "Bella");
            M("｛０｝ of Japanese input", "ja").File("bundled", "ja.csv", U8("t.fmt,｛０｝に乗る,Ride {0}\n")).Fill("t.fmt", "Bellaに乗る", false, "Bella");
            M("{O}, a letter O").File("bundled", "fr.csv", U8("t.fmt,Monter {O},Ride {0}\n")).Fill("t.fmt", "Monter Bella", false, "Bella");
            M("a template that lost its {0}: English").File("bundled", "fr.csv", U8("t.fmt,Monter,Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a {1} the English hasn't: English").File("bundled", "fr.csv", U8("t.fmt,Monter {1},Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a broken brace: English").File("bundled", "fr.csv", U8("t.fmt,Monter {0,Ride {0}\n")).Fill("t.fmt", "Ride Bella", false, "Bella").Told = true;
            M("a noun the word allows to drop").File("bundled", "fr.csv", U8("t.noun,Elle tire,This {0} pulls\n")).Fill("t.noun", "Elle tire", true, "donkey");
            M("a spreadsheet error cell (#NAME?): English").File("bundled", "fr.csv", U8("t.greet,#NAME?,Hello\nt.bye,Au revoir,Bye\n")).Show("t.greet", "Hello").Show("t.bye", "Au revoir").Told = true;
            M("a very long text").File("bundled", "fr.csv", U8("t.greet," + new string('a', 5000) + ",Hello\n")).Show("t.greet", new string('a', 5000));
            M("<nobr> tags").File("bundled", "fr.csv", U8("t.greet,<nobr>Bonjour</nobr>,Hello\n")).Show("t.greet", "Bonjour");
            // names
            M("FR.csv (upper case)").File("bundled", "FR.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("fr-FR.csv (a region)").File("bundled", "fr-FR.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("french.csv (a language's name)").File("bundled", "french.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("français.csv").File("bundled", "français.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr.csv.txt (a hidden extension)").File("bundled", "fr.csv.txt", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("fr.txt in the old key = value way: English").File("bundled", "fr.txt", U8("t.greet = Bonjour\n")).Show("t.greet", "Hello").Told = true;
            M("pt_BR.csv for pt-br", "pt-br").File("bundled", "pt_BR.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("zh-CN.csv for zh_cn", "zh_cn").File("bundled", "zh-CN.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("jp.csv for ja", "ja").File("bundled", "jp.csv", U8(ok)).Show("t.greet", "Bonjour").Told = true;
            M("de.csv in a French game: not read").File("bundled", "de.csv", U8("t.greet,Hallo,Hello\n")).Show("t.greet", "Hello");
            // places
            M("en.csv translated in place: English, said").File("bundled", "en.csv", U8("t.one,Un,One\nt.two,Deux,Two\nt.three,Trois,Three\n")).Show("t.one", "One").Told = true;
            M("beside the DLL, not in Languages\\").File("plugin", "fr.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("straight in Translations\\, no mod folder").File("translations", "fr.csv", U8(ok)).Show("t.greet", "Hello").Told = true;
            M("Translations\\ folder named after the mod, not its GUID").File("byname", "fr.csv", U8(ok)).Show("t.greet", "Bonjour");
            M("a translator's own beats the bundled").File("bundled", "fr.csv", U8(ok)).File("theirs", "fr.csv", U8("t.greet,Salut,Hello\n")).Show("t.greet", "Salut");
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
            List<string> heardWas = TranslationFiles.TestHeard;
            bool quietWas = Texts.TestQuiet;
            try
            {
                Texts.TestEnglish[MistakeOwner] = MistakeEnglish;
                Texts.TestNames[MistakeOwner] = new List<string> { "TestModFolder" };
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
                        string translator = Path.Combine(root, "translator"), translations = Path.Combine(translator, "Translations");
                        if (Directory.Exists(root)) Directory.Delete(root, true);
                        Directory.CreateDirectory(bundled);
                        Directory.CreateDirectory(Path.Combine(translations, MistakeOwner));
                        foreach (var (where, name, bytes) in m.Files)
                        {
                            string dir = where == "bundled" ? bundled : where == "plugin" ? plugin : where == "translations" ? translations
                                : where == "byname" ? Path.Combine(translations, "TestModFolder") : Path.Combine(translations, MistakeOwner);
                            Directory.CreateDirectory(dir);
                            File.WriteAllBytes(Path.Combine(dir, name), bytes);
                        }
                        Texts.TestBundled[MistakeOwner] = bundled;
                        Texts.TestTranslators = new List<string> { translator };
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
            bool hadPage = Texts.TestBundled.TryGetValue(page.OwnerId, out string pageWas), hadApi = Texts.TestBundled.TryGetValue(Texts.Api, out string apiWas);
            var chosen = new List<MountSettingsPage.Row>();
            MountSettingsPage.Row stale = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!page.InOptions(r) || r.LabelWord != null || string.IsNullOrEmpty(r.Label) || r.RowKind == MountSettingsPage.RowKind.Note) continue;
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
            var lines = new List<string> { "language\tfont before restyle\trestyled\tfont used\tstyle's font for the language\tsample\tall its characters\tmissing" };
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
                    lines.Add(string.Join("\t", id, ui?.FontBeforeRestyle ?? "-", ui != null && ui.Restyled ? "yes" : "no", used != null ? used.name : "-", ui?.StyleFont != null ? ui.StyleFont.name : "-", sample, all ? "yes" : "no", miss));
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

        // The game's language switched as its Settings window does (GameSettings, then its labels and their fonts), not saved.
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
