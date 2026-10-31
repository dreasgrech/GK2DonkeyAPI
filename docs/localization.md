# Translating Saddle Up: what the game offers, what it would take, and the decisions

An investigation, 2026-10-31, in two passes. **Nothing was built or changed.** A player asked for "an English file" in
the mod so that other people can add their own translations; the game itself comes in 11 languages.

- **The first pass** read how the game translates itself and how our text is made.
- **The second pass** (the user: "investigate the decisions we need to make and the findings you found") measured:
  - the game's own language tables and font data, read out of the game's files;
  - the log, for when the game sets its language;
  - the Workshop copier and BepInEx's cfg writer;
  - an exact inventory of what a player sees.

**Sources:**

- the decompiled game 1.011, Steam build 26297960: `LanguageModLoader`, `ModsPaths`, `ModsCsv`, `LanguageModManifest`,
  `ModsBootstrap`, `LLBase`, `LL`, `GameSettings`, `CodeModLoc`, `LocalizedLabel`, `TextStyle`, `LazyFontData` and
  `WorkshopSyncCore`;
- the game's `resources.assets` (the `lng_<id>` tables and the `LazyFontData` assets, read with UnityPy);
- `BepInEx.dll` (`ConfigFile.Save`);
- the GK2 Mod Framework's source;
- our code at commit `c4e0355`.

**The inventory** (section 3) is a 356-row list: owner, kind, file:line, flags and the English. It is in the jobs folder
of this session (`C:\Users\User\.claude\jobs\3d9fcfd0\tmp\l10n_inventory.tsv`, with `l10n_inventory_notes.md`), not in
the repository: it is a snapshot that the English file's generator of section 5 would replace.

## 1. The short answer

- **It is possible.** Nothing in the game stands in the way, and the game does it for its own new Mods window.
- **Nobody can translate us today.** Every word is English typed into the code of our DLLs. There is no file to copy,
  and the game's language system never sees our text.
- **The work:**
  - move the on-screen words into an English file that ships with each mod;
  - look up the file of the player's game language at the moment we draw;
  - use English for any line a translation lacks.
- **The mechanism has to be the Donkey API's.** Most of what a Saddle Up player reads is the API's text:
  - the Mount Journal and the colours window;
  - the options window;
  - the prompts on foot;
  - the keeper's bubbles;
  - key names and the speed words.

  The API looks text up without knowing who registered it. Saddle Up only gets an English file. Its code changes in four
  places (section 4).
- **Size:** 234 different texts a player meets in normal play with the released content:
  - 102 are Saddle Up's;
  - 126 are the API's;
  - 6 are the released packs'.

  Counting rare paths and unreleased content, it is 294.

## 2. How the game translates itself

**The 11 languages** (`LL.GetAvailableLanguages`; confirmed in the game's files: 11 tables of 9,474 lines each):

| id | Language | id | Language |
|---|---|---|---|
| `en` | English | `pl` | Polski |
| `de` | Deutsch | `ja` | Japanese |
| `fr` | Français | `zh_cn` | Chinese (Simplified) |
| `pt-br` | Português do Brasil | `ko` | Korean |
| `es` | Español | `tr` | Türkçe |
| `ru` | Русский | | |

(`LLBase` has a longer table with Italian, Ukrainian, Traditional Chinese, Thai and Vietnamese. `LL` overrides it with
these 11, and a community pack can't take one of the longer table's ids either: `TryAddPackDirectory` checks it.)

**Lookup and switching:**

- Each language is a table of key to text (`Resources/Locales/lng_<id>`).
- `LLBase.L(key)` returns the text, or the key itself when the key is missing.
- `LLBase.CurrentLang` is the current id. It reads `en` until the game loads a language.
- The Settings window's Language row runs `GameSettings.ApplyLanguageSettings`, which raises
  `GameSettings.OnLanguageChanged`.

**Fonts** (the `LazyFontData` assets):

- `small_font`, `small_font_bold` and `tiny_font` switch to their own fonts for `ja`, `zh_cn` and `ko` only. Every other
  language uses the same font as English.
- Japanese and Chinese lines are wrapped in `<nobr>` (9,417 and 9,323 of 9,474), which controls where a line may break.
  Korean and the European languages have none. A Japanese or Chinese translator of ours would do the same.

**Numbers in text:** the game avoids plurals by its wording ("Days played: %1"). Its newest lines, the Mods window's,
use `{0}`.

**The game's own Mods window** (new in 1.011): `CodeModLoc.L("ui_mods_section_code", "Code mods")` looks up the key, else
uses the English in the code. Its 32 `ui_mods*` lines are in all 11 tables, but still English in the ones checked:
German, French and Russian "Code mods", Japanese `<nobr>Code mods</nobr>`.

**Community language packs** (`LanguageModLoader`):

- **Where:** a folder `Languages/<id>/` under each search root:
  - the `Mods\` folder in `AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2`;
  - the game folder;
  - every subscribed Workshop item.
- **Files:**
  - `language.json`: `name`, `font`, `fontSettings`, `rtl`, `preprocessors`, `fallback` and `requireResize`;
  - `strings.csv`: `key,value` lines in UTF-8. A value with a comma, a quote or a line break goes in quotes, with quotes
    doubled (`ModsCsv`).
- **What a pack becomes:**
  - The folder's name is its id, and the pack is a **new** language in the Settings window's list.
  - Lines it lacks come from its `fallback`: a shipped language, another pack, or English.
  - Lines whose keys English doesn't have are added as well.
  - For a pack in `Mods\` or the game folder, the game writes `_missing_lines.csv` next to it.
- **The limit:** a pack whose id is a shipped language is skipped ("id collides with a shipped language"). A pack
  can't add lines to the shipped German.
- **The example pack:** Shift+F10 in the game writes `Mods\Languages\~Example\` (the full English `strings.csv` and
  READMEs) and reloads every pack.

**How three other GK2 mods do it** (the user downloaded them on 2026-10-31 "to see languages"; their DLLs were
decompiled and read, nothing was run):

- **WeeklyOverview 0.1.7** (`de.fletty.gk2.weeklyoverview`, Nexus 131), "11 languages":
  - **No file.** Every translation is typed into the C# code, a `switch` on the language per text. The translated text is
    small: the window's title, "Not yet discovered", and the six days', sins' and activities' names.
  - Nobody but its author can add a language or fix a line.
  - **The language** comes from `GameSettings.Instance.language`, the saved choice. A prefix maps it onto its ten
    languages (`pt…` is `pt-BR`, `zh…` is `zh-CN`); anything else, a community pack's language included, is English.
  - **Language changes:** it rebuilds its window on `GameSettings.OnLanguageChanged`.
  - **Fonts:** it takes the game's `small_font` by name. For Japanese, Korean, Chinese, Russian, Polish and Turkish it
    checks whether that font has the characters (`HasCharacter`), and if not, picks the loaded game font that has the
    most of them.
  - The game's own Japanese, Chinese and Korean fonts are separate assets (section 2, Fonts). So a font found by name
    has to be checked like this. Ours copies the font of the game's live Settings row instead, which the game has already
    set for the language. That is still to see in the game (section 4, "Still to measure").
  - **Worth taking:** the same character check, as the safety net of our font copy.
- **Work Faster 1.3.0** (Nexus 122):
  - JSON files, one per language, in the Framework's folder (`BepInEx/plugins/GK2.Framework/Localization/gk2.workfaster/`).
  - Every text goes through `GK2.Framework.FrameworkLocalization.Get`. The mod declares the Framework as a hard
    dependency (`BepInDependency`), so it doesn't run without it.
  - Its files are `de`, `en`, `es`, `fr`, `it`, `ja`, `ko`, `pl`, `pt`, `ru` and `zh`. The Framework maps the game's
    `pt-br` to `pt` and `zh_cn` to `zh` through its "part before `_`" step. `it.json` is never used: the game has no
    Italian, and a community pack can't take the id `it`.
- **Keeper's Longer Days 1.6** (Nexus 5):
  - JSON in the Framework's folder too (`gk2.longerdays/`, no `en.json`: the English is in its code).
  - It reaches `FrameworkLocalization.Get` through reflection and translates only while the Framework's own menu draws
    its settings. Everywhere else, and without the Framework, it shows English.

**What this means for us:**

- None of the three gives translators a file that works without the GK2 Mod Framework. The one without the Framework
  has no file at all.
- Our plan (D1: our own files) is the gap they leave.
- Their choice of JSON follows from the Framework, which dictates it, so it is no evidence about the format itself (D2).

## 3. What a player reads, and whose text it is

From the inventory: every string traced to where it reaches the screen. Rows are call sites; "different" counts texts
that differ only in their placeholders once.

| Owner | Different texts in normal play | What |
|---|---:|---|
| Saddle Up, `Plugin.cs` only | **102** (134 rows) | 48 row labels and 28 descriptions; 43 choice words, 16 different (e.g. "Game default", "Custom"); 7 section names; 2 headings; the page's title and description |
| Donkey API, 22 files | **126** (142 rows; 177 different counting rare and other-mod paths) | 36 different bubbles (79 with rare paths); the journal (21); the options window (18); 23 key texts (key names and the "Same key as" lines); 9 prompts; 7 stat words; 5 speed words; the colours window (5); the crossroads notification |
| Released packs | **6** | Gerry's and the Dragon's name and description; Hellfire and Royal Parade. Also 2 plugin names in the game's Mods window |

**Where the API's text sits:** `MountsPanel.cs` 33 rows, `KeyCapture.cs` 32, `CartChest.cs` 23, `Mount.cs` 13, `Cargo.cs`
13, `OptionsWindow.cs` 12, `MountSettingsPage.cs` 11, `MountInstance.cs` 11, `InstanceControls.cs` 10, and 13 more files
with 8 rows or fewer each.

**Written, but never drawn:**

- the descriptions of the journal's and colours window's rows: 20 of Saddle Up's, 6 of the API's. Only the options
  window shows descriptions, so these are cfg comments only;
- Saddle Up's picture row "Your mount";
- the section "Colours", which has look rows only;
- the journal entry's `Lines`: "Pulls the cart", "Has small babies (0.6 of its size)", "Speed: …", "Cargo: …" and the
  `[Stats]` lines. Only self-tests read them;
- every skin description;
- a default skin's own name. "Red Dragon" and Gerry's skin "Gerry" show as "Default".

**Never on screen (the cfg only):**

- the API's own settings: 476 entries, 330 different descriptions. No window shows them; only the cfg file has them;
- Saddle Up's rows also write their descriptions into its cfg.

## 4. The findings, checked

| Finding | What was measured | Verdict |
|---|---|---|
| **The language at startup** | `Player.log`: Saddle Up's "loaded" at line 85, the chain loader done at 119, the first `LoadLanguageResource("en")` at line 645 | **Confirmed.** Our plugins start before the game loads any language, so `CurrentLang` reads `en` in every `Awake`. Text must be looked up when drawn. |
| **A language switch while our windows are open** | `OptionsWindow.Open` and `MountsPanel.Build` run `ui.Read` and build anew on every open. `GameWindowAside` switches the game window under ours off. | **Settled.** The language can't change while one of ours is open, so rebuilding on open is enough. Prompts and bubbles are made when shown. `OnLanguageChanged` would only matter for text kept longer. |
| **Fonts for ja, zh_cn and ko** | Only these three get other fonts (`LazyFontData`). Our windows copy the font of the game's Settings row each time they open (`UiKit.ReadFont`). | **Probably fine. To see in the game:** set the game to Japanese, open the journal and the options window, and look at the font. Every other language uses the default font, which the game itself uses for Russian, Polish and Turkish. |
| **Room** | The game's own lines (wide characters counted double; short labels are English up to 30 characters). Labels: French median 1.25×, 90th percentile 1.80×, 34% over 1.4×; Spanish 1.20×, 1.65×, 26%; German 1.12×, 1.57×, 19%. Descriptions grow less: French 1.31× median, German 1.19×. Chinese and Korean are shorter. | **Confirmed, a real limit.** `UiKit`'s labels (lines 439-442) neither shrink nor cut: they run past their box (`Overflow`). Only some descriptions shrink to 75-80% and then end in "…". |
| **Sentences glued in code** | 60 rows: `"Ride " + name`, `"Same key as " + x`, `description + " " + hint` (`OptionsWindow.cs` 595-596), `v + "%"` and the clash lines | **Confirmed, bigger than first counted.** Each needs one line with placeholders (`Ride {0}`). |
| **Plurals** | One chosen in code ("A skin" / "N skins", rare). Five count texts have none: `{holds} items`, "Pulls a cart that holds {0} items.", "{0} pulls {1} items at most…", Freeplay's "carries {0} items" | **Corrected.** English already says "1 items" when a mount's cargo is 1. Russian and Polish need three forms. |
| **Words that are also data** | Speed words, read back from `mount.cfg` by `Catalog.ParseBand`. Section names, which are cfg sections and are compared with `"Mount"` in code (`MountsPanel.cs` 653, 1440). Key names (Unity's `KeyCode` names). The crossroads notification, a cfg value. | **Confirmed.** Only the drawn copy may change; comparisons and parsing stay English. |
| **Text fixed at startup** | Saddle Up copies `Mounts.Catalog.SpeedWords` into its Freeplay list in `Awake`. Its max-cargo description writes the number in then. | **Confirmed.** Either the API translates a row's choice words when drawing, or the row carries ids and the API supplies the words. The number becomes `{0}`. |
| **Names inside sentences** | `{Who}` / `{Noun}` ("This {Noun} already pulls a cart.", "{Who} pulls {cap} items at most…"); `Noun` is lower-cased in code | **Confirmed.** Cases and genders (German, Russian, Polish) can't take a noun in one form. Lower-casing breaks German nouns. |
| **Saddle Up's code changes** | the `v + "%"` on three sliders; the max-cargo number; the speed words in Freeplay; the page description's "Press H …" names default keys | **Four places**, all small. |
| **The cfg stays English** | `ConfigFile.Save` rewrites the file from each entry's description, given at `Bind` | **Confirmed, and automatic.** `Bind` runs in `Awake`, before any language, so as long as `Bind` gets the English the cfg never changes language. |
| **The Workshop copier carries the files** | `WorkshopSyncCore.SyncTree` copies every file, except `core/`, `cache/`, `patchers/` and `*.log`. `config/` files are copied once and never over a player's. | **Confirmed.** A `.csv` in our item reaches the player. |
| **The game's words we could reuse** | Our words looked up in the English table | **Few.** Cancel (`btn_cancel`), OK, Options (`ui_menu_options`), Name (`ui_name`), On/Off, Yes/No. Not Done, Defaults, Ride, Speed, Cargo, Skin or the colours. Gerry and Donkey have no entries of their own. |
| **Self-tests read English** | `SelfTest.Order.cs` 67, `SelfTest.cs` 1511 and the panel checks | **Confirmed.** They would run in English or compare keys. |

**Still to measure in the game** (the third round, 2026-10-31):

- **Room, text by text.**
  - The self-tests (F11) now measure every text of the options window (each section, and every row's description in
    the description line), of the journal (for each mount the journal test opens) and of the colours window
    (`Dev/TextRoom.cs`). A text grows by repeating its own words and is laid out by TMP in its own box. Its room is the
    largest factor that still fits, in steps of 0.1 up to 3, at its full size and, for a text that shrinks, at its
    smallest.
  - The run writes `BepInEx\GK2-text-room.tsv` and an INFO line per window.
  - Each text's room is then set against the game's own growth per language: the share of the game's lines of the same
    kind that grew more is the chance that a translation of it would not fit.
- **The Japanese font.**
  - Set the game to Japanese and switch `Debug.UiProbe` on.
  - Open the journal: `BepInEx\GK2-ui-probe.txt` names the font it read (`japanese` or `small_font`).
  - Paste a Japanese name into the journal's Name box to see whether our own labels draw the characters. The prompt on
    foot is the game's own UI and proves nothing about ours.
- **Option A** (D1), only if it is still wanted.

**Found on the way: problems that have nothing to do with translating.** They are listed in `docs/open-items.md` with
a proposal each, and nothing is changed.

- **A bubble shows coordinates.**
  - `Mounts.ReturnToSafeSpot` says its refusal in the keeper's bubble (`Mounts.cs` 435) and logs nothing.
  - When no safe spot holds, `SafeSpots.Return` refuses with "No safe spot of this scene still holds (…)". Inside the
    brackets are the last spot's coordinates and why it failed, with game collider names (`SafeSpots.cs` 246, 261).
  - So the detail is only in the bubble, not in the log. Saddle Up's own comment says coordinates mean nothing to a
    player (the user, 2026-10-27).
  - **Proposal:** the bubble says "No safe spot of this scene still holds." and the detail goes into a `[safe-spot]`
    log line, as a return already writes one.
- **"1 items".**
  - A mount's `cargo` may be 1 to 6. At 1 it shows:
    - the stat "1 items" (`MountsPanel.cs` 173);
    - the ability line "Pulls a cart that holds 1 items." (192);
    - the bubble "… pulls 1 items at most." (`Looks.cs` 1571).
  - No released mount has cargo 1: the Critters' are 2, 4 and 6, and Gerry's is 4. So only another mod's mount shows it.
  - **Proposal**, worded without a plural (D7):
    - the stat as a number ("Cargo 4", "2 of 4"), as Skins already is;
    - the ability line "Pulls a cart (cargo {0}).";
    - the bubble "Too much on the cart ({0} of at most {1}). Take some off first.", which also drops the noun.
- **Dead text.**
  - The 26 descriptions of look rows are written and never shown, but they are the cfg's comments for players who
    edit it by hand. **Proposal:** keep them, and leave them out of the translation files.
  - The picture row's "Your mount" is a label the API requires. **Proposal:** keep it, untranslated.
  - The journal entry's `Lines` and `Extra` are built for every mount, and only self-tests read `Lines`. **Proposal:**
    take them out with their self-test checks, the next time the journal is worked on.

## 5. The decisions, investigated

### D1. The game's language packs (A) or our own files (B)

**Decided (the user, 2026-10-31): B, our own files.**


| | A: the game's packs | B: our own files |
|---|---|---|
| **What the player does** | Picks "Deutsch (mods)" instead of Deutsch: a second German in the game's list | Picks Deutsch as always |
| **Several mods** | One pack per language for every mod that does this, or packs chained through `fallback`. A player picks one language. | Each mod's files on their own |
| **Side effects** | For a pack in `Mods\` or the game folder, the game writes `_missing_lines.csv` with every line the pack lacks: about 9,470 of the game's own, as our lines are all it carries | None |
| **Our work** | Keys in code, `LLBase.L` with English as the fallback | A loader (folder, file, language), a lookup with English as the fallback, a generator of the English file |
| **Who does it this way** | Nobody seen | The game's own Mods window (`CodeModLoc.L`), GK2 Mod Framework |

**Verified:** A, by reading `BuildPack` only. A test in the game takes two minutes, if A is still wanted:

1. Make the folder `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Languages\de-modtest\`.
2. Put a `language.json` in it holding `{ "name": "Deutsch (Mod-Test)", "fallback": "de" }`.
3. Put a `strings.csv` in it with the two lines `key,value` and `dreas_l10n_test,Hallo aus dem Mod-Test`.
4. Start the game and pick "Deutsch (Mod-Test)" in Settings.
5. The game should be German, and the log should say `LoadLanguageResource("de-modtest") [mod], 9475 lines loaded`:
   the extra line got in.
6. Delete the folder afterwards.

**Recommendation: B.** B can still take A's lines when someone makes such a pack: look up `LLBase.HasL(our key)` before
English. The crossroads notification already goes through the game's lookup with its English as the key.

### D2. The file format: the data (the user asked for more data, 2026-10-31)

**Four candidates**, shown with the same two lines translated into German:

- **The game's CSV** (`strings.csv`), plus a third column for the English:

  ```
  key,value,english
  Keys.RideKey.label,Reite dein Reittier,Ride your mount
  Keys.RideKey.desc,"Ruft dein Reittier und du steigst auf. Drücke erneut, um abzusteigen.","Calls your mount and gets you on. Press it again to get off."
  ```

- **`key = value` lines**, like our `mount.cfg`, with the English as the comment above each line:

  ```
  # Ride your mount
  Keys.RideKey.label = Reite dein Reittier
  # Calls your mount and gets you on. Press it again to get off.
  Keys.RideKey.desc = Ruft dein Reittier und du steigst auf. Drücke erneut, um abzusteigen.
  ```

- **JSON**, like the GK2 Mod Framework:

  ```
  {
    "Keys.RideKey.label": "Reite dein Reittier",
    "Keys.RideKey.desc": "Ruft dein Reittier und du steigst auf. Drücke erneut, um abzusteigen."
  }
  ```

- **gettext `.po`**, the open-source standard (the English is the `msgid`):

  ```
  msgctxt "Keys.RideKey.label"
  msgid "Ride your mount"
  msgstr "Reite dein Reittier"
  ```

**Our own text** (the inventory's 298 different English texts; script `l10n_format_m49.py`):

| What a format has to handle | Texts |
|---|---:|
| commas: CSV must put the whole text in quotes | 34 (35 need quoting in all) |
| double quotes: CSV doubles them, JSON and `.po` escape them | 2 |
| apostrophes: fine in every format | 32 |
| `{0}` placeholders | 41 |
| line breaks, leading or trailing spaces, `#`, backslashes | 0 |

So `key = value` needs no escaping for any of our texts. JSON and `.po` need it for 2, and CSV quotes 35.

**Translator mistakes**, put through each format's reader:

- The test file is 12 of our real lines in German. "Right" means what the translator meant.
- The game's CSV reader is ported line for line from the 1.011 decompile (`ModsCsv`).
- "CSV, separator from the header" is a reader that takes `;` or tab when the first line says `key;value`.
- JSON was read by both Python and the game's own Newtonsoft.Json 13.
- The `.po` reader is lenient. A strict gettext tool refuses an unescaped quote; Poedit escapes it for the translator.
- Scripts: `l10n_format_m50.py` and `l10n_newtonsoft_m51.ps1`.

| Mistake | Game's CSV reader | CSV, separator from the header | `key = value` | `.po` | JSON |
|---|---|---|---|---|---|
| none | 12/12 | 12/12 | 12/12 | 12/12 | 12/12 |
| saved by European Excel, with `;` between columns | **0/12** | 12/12 | (no spreadsheet) | (no spreadsheet) | (no spreadsheet) |
| two quotes typed on purpose, not doubled or escaped | 11/12, 1 silently wrong (the quotes vanish) | 11/12, 1 silently wrong | 12/12 | 12/12 | **whole file refused** |
| one stray quote typed by mistake | **3/12**: the quote swallows the lines after it | **3/12** | 12/12 | 12/12 | **whole file refused** |
| a comma left out between two entries | (n/a) | (n/a) | (n/a) | (n/a) | **whole file refused** |
| a line break typed inside a text | 11/12, the line cut | 11/12 | 11/12 | 11/12 | Newtonsoft 12/12 (Python refuses) |

**European Excel** (desk evidence; not tried on a machine set to another language):

- Excel takes the CSV column separator from Windows' list separator. On European Windows the comma is the decimal mark,
  so that separator is `;`, and a comma file opens with everything in the first column. Sources:
  [Ablebits](https://ablebits.com/office-addins-blog/change-excel-csv-delimiter),
  [Paessler](https://helpdesk.paessler.com/en/support/solutions/articles/76000063695-i-have-trouble-opening-csv-files-with-microsoft-excel-is-there-a-quick-way-to-fix-this)
  and a [Microsoft Tech Community](https://techcommunity.microsoft.com/discussions/excelgeneral/convert-excel-to-csv/4026933)
  thread; no Microsoft article was found.
- That covers 7 of our 10 languages: German, French, Russian, Polish, Turkish, Spanish and Portuguese.
- A first line `sep=,` tells Excel the separator for that one file. Our reader could skip it, but the game's `ModsCsv`
  would read it as a row.
- Excel's plain "CSV" save writes the old code page instead of UTF-8, unless "CSV UTF-8" is chosen. This is general
  knowledge, not checked here.

**Tools and people:**

| | CSV | `key = value` | `.po` | JSON |
|---|---|---|---|---|
| **Spreadsheets** | yes (the `;` caveat above) | no | no | no |
| **Notepad and other text editors** | yes; quoting by hand | yes, the simplest | yes, but wordy | yes; strict syntax |
| **A dedicated free editor** | no | no | Poedit: English beside each line, changed English flagged | no |
| **Translation platforms** (Weblate: CSV, JSON, PO and Java properties all [supported](https://docs.weblate.org/en/weblate-5.16/formats.html)) | yes | yes, as properties | yes | yes, flat key-value only |
| **Who knows it** | the game's own translators (Shift+F10 writes the game's English as CSV) | our pack authors (`mount.cfg`) | open-source translators | the Framework's translators |
| **The English beside each line** | a third column | the comment above | built in (`msgid`) | no place |
| **When our English changes** | our reader compares the third column | our reader compares the comment | tools mark it "fuzzy" ([gettext manual](https://ftp.gnu.org/old-gnu/Manuals/gettext-0.10.35/html_chapter/gettext_5.html)) | nothing tells |
| **Plurals** | worded out (D7) | worded out | built in | worded out |
| **Our reader** | about 60 lines (or the game's `ModsCsv`, a game dependency) | about 20 lines; the API reads this style already | about 70 lines | one call to the game's Newtonsoft (a game dependency) |

**What the data says:**

- **JSON is the least forgiving.** One wrong quote or one missing comma and the whole language is gone. It also has no
  place for the English.
- **CSV suits spreadsheet users and is the game's own convention.** By hand it is fragile: a single stray quote cost 9 of
  12 lines in the game's reader. European Excel breaks it, unless our reader takes the separator from the header.
- **`key = value` survives hand-editing best.** Every mistake costs at most its own line, and none of our texts needs
  escaping. Pack authors know it, and a platform can treat it as properties. It doesn't open in a spreadsheet.
- **`.po` has the best tools**: plurals, and changed English flagged. It is the least familiar to game modders and the
  wordiest by hand.

**How often our English changes** (third round; `l10n_churn_m56.py`, read from git).

The repository has no tags, so the release commits come from the CHANGELOG dates and the release notes: 0.9.0
`6be69a2`, 0.9.1 `7ac9a6c`, 0.10.0 `f578d01`, 0.12.0 `7013e71` (0.11.0 was never public), 0.13.0 `971058b`, 0.13.1
`49c0944`, 0.13.2 `67d2c0c`, 0.14.0 `9159207`.

| Release | Saddle Up's rows | added | removed | moved to another section | English changed | Today's on-screen texts first seen here |
|---|---:|---:|---:|---:|---:|---:|
| 0.9.0 | 35 | | | | | 149 |
| 0.9.1 | 35 | 0 | 0 | 0 | 0 | 0 |
| 0.10.0 | 33 | 1 | 3 | 1 | 9 | 11 |
| 0.12.0 | 51 | 22 | 4 | 22 | 22 (16 of them moved too) | 89 |
| 0.13.0 | 51 | 0 | 0 | 0 | 0 | 0 |
| 0.13.1 | 51 | 0 | 0 | 0 | 0 | 0 |
| 0.13.2 | 51 | 0 | 0 | 0 | 1 | 1 |
| 0.14.0 | 51 | 0 | 0 | 0 | 0 | 16 (the options window, the API's) |

- **Small releases** hand a translator 0 to 16 lines.
- **Feature releases** hand them 20 to 90. 0.12.0 changed the English of 22 existing rows: exactly the lines a
  translator can't find unless the file shows the English each line was translated from, or a tool flags it.
- So the English-beside-the-line support matters on feature releases, and every format but JSON has it.

**See them:** the same 20 real lines (section names, labels, a long description with commas, one with quotes, choice
words, a `{0}` template, a bubble; keys approximate, German by Claude, for the look only) in each format, as we would
ship them and as a translator would send them back. They are in the session scratchpad's `l10n-formats` folder:
`en.txt` / `de.txt`, `en.csv` / `de.csv` (opens in a spreadsheet), `en.json` / `de.json`, `template.pot` / `de.po`.

**The cost of a wrong pick:**

- Before the first translation exists, changing formats is one generator change.
- After, it is one script over each language's file.
- The loader can also read two formats side by side. Either way the choice can be undone.

**The choice comes down to who translates.** The data doesn't crown one format; it splits by how the translator works:

- **In a spreadsheet:** CSV with the English as a third column, our reader taking `;` or tab from the header.
- **In a text editor:** `key = value` with the English as the comment above each line. Robust: a slip costs one line.
- **In Poedit or on a platform such as Weblate:** `.po`. Its tools flag changed English and handle plurals.
- **JSON** suits none of them better than these; its one advantage, the Framework's convention, doesn't apply to us.

The same loader can read the text-editor format and the spreadsheet format side by side. The question that decides is
how the first translator, probably the person who asked, wants to work.

### D3. Where translations live

**Decided (the user, 2026-10-31): both.** Contributed translations ship in our items; translators may
publish their own, in a folder of their own.


The rule: two Workshop items that share a file path lose it when either goes (`docs/mods-window-and-workshop.md`
section 10). On Nexus, the zip unpacked last simply overwrites the other's file.

| | Bundled in our items | In translators' own items | Both |
|---|---|---|---|
| **For players** | one download, always matching the version | a second download; it can lag our version | either |
| **For us** | we ship files we can't read: a second speaker checks, or we trust the translator, with their name in the notes | nothing to ship | the API reads `Translations/<mod guid>/<lang>` files in any plugin folder, besides our own folder |
| **Paths** | ours only | theirs only, never inside our folder | separate |

**Nexus** has a "Translation" file type: a translator uploads their own mod, picks the original from a list, and the
original's page lists its translations with their language and author. This comes from Nexus's
[announcement](https://www.nexusmods.com/halo/news/12285) and a community
[guide](https://www.nexusmods.com/baldursgate3/mods/7288); the current upload steps weren't checked. So translators'
own items have a home on Nexus, and on the Workshop a translation item can name ours as Required.

**Recommendation: both, decided before the first translation arrives.**

- Contributed files ship bundled.
- A translator's own item keeps to its own folder.
- One rule says which wins when both have the language. I'd say the separately installed one, because the player chose
  it.
- The folder is better named `Translations` than `Languages`, so nobody mixes it with the game's packs. The game only
  scans `Languages/` at a Mods, game or Workshop root, and our plugin folder isn't one, so `Languages` would work too.

### D4. The order of work

- **Saddle Up's 102 texts** all reach the screen through the API's row drawing: `OptionsWindow`, `MountsPanel` and
  `RowControls`. One lookup there translates the whole page, with four small changes in Saddle Up (section 4).
- **The API's 126 texts** sit in 22 files and need templates for the glued sentences.
- **The packs' 6** need the API to look up `mount.cfg` and `skin.cfg` text by id.

**Recommendation, in steps:**

1. The lookup and the generator, with the page rows (Saddle Up's 102). Run a test language of section 6 and check the
   fonts.
2. The API's own words.
3. The packs.

Each step is a release that works on its own: lines not yet in the file stay English.

### D5. Where the English lives

| | The code is the source, the English file generated | The English file is the source, the code holds keys only |
|---|---|---|
| **Code** | stays readable, as now | `Text.Of("journal.ride")` everywhere |
| **Layering** | Saddle Up passes English as now, and the API makes the keys | every mod keeps its own table |
| **Drift** | a release check compares the file with the code | the file can't drift |

**Recommendation:** the code is the source, and the English file is generated with a release check.

### D6. Keys

- **Settings rows:** keys from the row's cfg key, without its section: `<key>.label` / `.desc` / `.choice.<n>`.
  - 0.12.0 moved 22 rows to other sections. Keys that included the section would have lost those 22 translations.
  - Saddle Up's 46 keyed rows are unique across sections today (`l10n_keys_m58.py`).
  - The section joins the key only for a page whose keys repeat across sections.
- **Per-mount rows:** they drop the mount part. The API already does this for the journal (`MountsPanel.BaseKey`:
  `Foals_<mount>` is `Foals`).
- **Rows without a cfg key** (headings, buttons, the panel button) and **the API's own words** get named keys
  (`journal.ride`, `bubble.cart_full`).
- **Choice words:** one per row, not shared by their English. "Custom" may take a different form in German or French
  depending on the noun it belongs to (coat, paint, light). The generator fills a new row's words with ones already
  translated, for the translator to accept.

### D7. Plurals and nouns in sentences

- **Option 1:** word them out, as the game does ("Cargo: 4", "Holds up to {0}"), and leave nouns out of sentences
  ("It already pulls a cart.").
- **Option 2:** plural forms per language and grammatical forms of each mount's name.

**Recommendation: option 1.** It also fixes "1 items" in English.

### D8. Key names

- 18 key names are typed into `KeyCapture`: Shift, Ctrl, Esc, the arrow keys and so on.
- Every other key name is Unity's `KeyCode` name with spaces: Space, Page Up, F1 and so on.
- The game draws its own keys as pictures, not words.

**Recommendation:** keep the keyboard's key names in English for now (they match the keycaps). Add a table later if
translators ask.

### D9. What stays English

**Recommended to stay English:**

- the product names (GK2 Saddle Up, the packs' names);
- the cfg file;
- the log;
- dev tools.

**To decide:** mount names. "Donkey" and "Dragon" are words; "Gerry" is a name. The recommendation is that they are
translatable lines like any other, and a translator keeps a name as it is.

## 6. A first step, if B is chosen

1. **The API's lookup:**
   - the folder, the file and the language: `CurrentLang` at draw time, then the part before `-` or `_`, then the English file,
     then the English in the code;
   - the "English changed" rule (the English beside each line, D2);
   - the English file's generator and its release check.
2. **Saddle Up's page through it:**
   - the options window's rows, sections and headings;
   - the journal's and colours window's labels and choice words, drawn translated;
   - the comparisons with "Mount" moved to the untranslated name;
   - Saddle Up's four small changes.
3. **A test language:** every line 60% longer and in brackets. That sits between German's 90th percentile (1.57×) and
   French's (1.80×), for short labels.
   - Cut text shows as a missing bracket, and a leftover English line as one without brackets.
   - Labels get a rule for long text: shrink to about 80%, then end in "…".
4. **In the game:** Japanese, for the fonts, and the test language, for the room.
5. **For players:** a "Translations" section in the README: the folder, the language ids, the format (D2), which
   tools to use, and how to send a file.

## 7. Built: step 1 (2026-10-31)

The plan was agreed with the user (D1 our own files, D3 both places, the format left to the translator: both
templates shipped, both read; step 1 = the settings rows and the windows' own words). Built and deployed (API build of
2026-10-31); the F11 run and the in-game checks are the user's next session.

- **`Texts`** (`src/GK2DonkeyAPI/Texts.cs`) is the lookup.
  - It reads the owner's files in the order translators' folders, then the bundled `Languages/`.
  - In one folder `.txt` beats `.csv`.
  - The language: the game's id, then its part before `-` or `_`, then the English.
  - The `# en:` line or `english` column decides whether a translation is still current.
  - Each owner's table is cached per language.
  - One INFO line per owner and language: so many of so many lines, missing, changed. One WARNING per broken line or
    file.
- **`UiWords`** (`src/GK2DonkeyAPI/UiWords.cs`) holds the API's own words, each with its key and English: the options
  window, the row words, the rows `MountRows`/`FoalRows` add, the Mount Journal, the colours window, the speed words.
  The table is complete by construction; the English file lists it by reflection.
- **`MountSettingsPage.Row`** has `Owner`, `TextKey` (its cfg key without the mount or skin part), `LabelWord` and
  `ButtonWord`. The text fields stay English: they are compared (`ShownSection == "Mount"`, the colours groups) and
  logged.
- **Drawn through the lookup:** `OptionsWindow` (title, cards, heading, notes, the help line through the
  `"{0} {1}"` template, the Defaults box, Done/Defaults), `RowControls` (label, choices, None, "Press a key...", button
  words), `MountsPanel` (title, buttons, Stats, Abilities, stat words and values, the not-installed notes, the
  donkey's name and description, the colours window's title, headings and Done), `KeyCapture` (the clash lines, None)
  and `SettingsButtons` (the "{0} options" button).
  - The Settings-window button retitles itself when the language changes in that window.
- **The English templates:**
  - A dev run writes them, `BepInEx\GK2-l10n\<guid>\en.txt` and `en.csv`, when a page is shown.
  - `tools/l10n-sync.py` copies them into `src/<Plugin>/Languages/`. Its `--check` is in `tools/release-scans.sh`.
  - `DeployToGame.targets` deploys `Languages\` with the DLL, and `tools/make-release-zip.py` ships it in the zip and
    the Workshop folder.
- **For translators:** `[Translations] Report` (the API's cfg, off by default) writes
  `BepInEx\GK2-l10n\<guid>\<id>.missing.txt` with the lines still to translate.
- **The test language** (`Debug.TestLanguage`, dev only, off by default): every looked-up text 1.6 times as long and
  in brackets.
- **Self-tests:**
  - `TranslationTests.Files`: the folders' and formats' rules, with fixture files.
  - `TranslationTests.Windows`: the test language over the three windows; every row label and window word bracketed,
    the rest listed, and TextRoom at 1.6 times.
  - F11 runs everything else in English with the test language off, whatever the game's language.
  - CecilScan sorts `Texts` as a warned feature (the game's language) and `TranslationTests` as dev.
- **Saddle Up's code is unchanged.** `ApiSurface` is unchanged. NoLinqScan is clean.

**The first dev start and a review (2026-10-31, before the user's tests).** The start wrote both templates, with
a clean log (Saddle Up 310 lines, the API 60). A fresh reviewer read the change. Fixed:

- **Too many template lines:** 168 of Saddle Up's 310 were the palette's colour names of the seven custom-colour rows,
  drawn as swatches only: left out.
- **The API's templates had no hints:** each now says where it shows and what its `{0}` is (`Word.What`, the template's
  comment).
- **The Settings-window button:** it kept the language of its first drawing when kept, not redrawn (F11 after a
  German open would have failed). It now retitles whenever the language isn't the one it was drawn in.
- **A game update renaming `LLBase.CurrentLang`** would have failed the getter before its try. It is read in a
  method of its own, `NoInlining`.
- **`l10n-sync.py --check` compared bytes**, and git's `core.autocrlf` turns LF into CRLF on checkout: line endings
  don't count now.
- **`<nobr>`:** our labels draw no rich text, so a translator copying the game's habit would see the tags. They are
  taken out.
- **The Skin list's "(not installed)", "(set aside)" and "(none)"** were still English: `FillList` marks them
  (`ChoiceNotes`, `ChoiceNames`) and `Texts.Choice` draws them with the API's words.
- **A cfg key used in two sections**, or two buttons with one label, would share a translation line. The later row's
  key takes its section.
- **The heading test** could pass on the section's card: it reads the heading itself now.
- **The bad-template test** logged a real warning on every F11: quiet in the test now, and the message tells a
  broken English template, which is a bug of the mod, from a translator's.
- **A `.csv` value or English with a space after its separator** counted as changed: trimmed, as the `.txt`'s are.
- **`make-release-zip.py`** refuses to build while a `Languages/` folder has changes not committed: the zip takes
  it from the working tree, the DLLs from the committed `dist/`.

**Left as it is, on purpose:** `DeployToGame.targets` copies `Languages\` but never deletes there. A tester's
hand-made `de.txt` in the deployed folder survives a rebuild; a file removed from the repository stays deployed until
deleted by hand.

**Done since:**

- the templates: synced 2026-10-31 22:53, Saddle Up 142 lines and the API 61;
- the first F11 of the Translations group: 37/0/0 (section 8);
- the long-text rule.

**Tested in one session (2026-10-31, 23:31: Translations 74 passed, 0 failed, 0 warnings):**

- **German:** a German translation read back from the three windows. The four translated rows were in German
  ("Ä Mount Journal" among them) and the stale one in English. The API's `;` CSV with a quoted `;` worked.
- **Fonts** (`GK2-l10n-fonts.tsv`): every one of the 11 game languages was drawn in the font the game's own style
  gives it, with all the characters of its own words.
  - The Settings row still wore the previous language's font until its style was applied: zh_cn japanese,
    ko chinese, tr korean.
  - The font fix of the same day, the row's style applied for the language before its font is copied, was needed.
- **Room** (`GK2-text-room.tsv`, `l10n_room_m47.py`):
  - In English, with the shrink rule, every text has at least 1.8 times its length in room.
  - Against the game's own growth per language, no text is likely to overflow in any language. The tightest are
    the slider values and the lantern-light face, under 10% in French.
  - At 1.6 times, nothing fails at its smallest size.

**Not yet:**

- steps 2 and 3, planned in section 8 and waiting for the user's yes.

## 8. Steps 2 and 3 (planned 2026-10-31, built 2026-11-01)

**Built 2026-11-01** (the user: "yes lets continue and lets do a lot so we get this translation finished and ready";
the plan below, with the four rewordings taken as part of that yes, each in CHANGELOG "For players" to undo on its
own):

- **The bubbles** (`UiWords`, `Mount.Say`):
  - the keeper's 71 fixed sentences (`say.*`) are words; the code returns a word's English
    (`UiWords.SayCartFull.English`), so `MountResult.Reason` stays English and the table is the one source;
  - the 6 sentences with values go through `Word.Make(args)`, which returns the English and remembers it with its
    values (the last 8): `Mount.Say` finds a sentence by its English (`UiWords.Shown`) and shows the translation,
    filled in; a value with a translation of its own (a `Term`: the donkey's or a pack's mount name) goes into the
    reason in English and into the bubble translated;
  - another mod's reason is shown as it is;
  - the player-facing refusals only: the API's refusals to a mod's call (registry checks, "Not mounted.", the flight
    API's) stay English, for authors;
  - `tools/l10n-sync.py --check` fails on a `Say("...")`, a prompt or a hint said with a literal in the game code.
- **The prompts on foot** (8 words, `prompt.*`): "Ride", "Ride {0}" (the player's name for the mount as typed),
  "Hitch the cart", "Load on cart", "Cart is full", "Take from cart", the chest's "Set down" and "Load onto the cart".
- **The crossroads notice:** translated while the player's `CorpsesNotification` is the shipped English
  (`Corpses.NotificationShown`); a text of their own as written.
- **The packs' texts** (step 3, `Catalog.NameTerm`, `Looks.NameTerm`): a mount's name, description and abilities, a
  skin's name and description, by the owner that registered them (`mount.<id>.name`, `.description`,
  `.ability.<id>.name/.description`, `skin.<id>.name/.description`). Drawn in the journal's list, header,
  description and abilities, the waiting skins' sentence, the colours window's title and the Skin list. A mount's
  own look's name isn't listed: the Skin list shows it as "Default".
  - Nouns ("This donkey already pulls a cart.") stay the mount's English noun; the two sentences' hints tell a
    translator the `{0}` may be left out (gendered languages).
- **The English files:** a dev start writes one for every owner (`Texts.Owners()`: the pages', the API's, every loaded
  plugin that registered a mount or a skin) when a page is shown; `l10n-sync.py` already maps the packs' GUIDs to
  `packs/<Pack>/Languages/`, `DeployToGame.targets` deploys them and `make-release-zip.py` ships them.
- **The rewordings** (the user's yes, 2026-11-01):
  1. the journal's Cargo a number ("4", "2 of 4"), the ability "Pulls a cart (cargo {0}).";
  2. the overload: "Too much on the cart ({0} of at most {1}). Take some off first.";
  3. the waiting skins in one form: "Skins for this mount are installed ({0}), but not the mount itself: install the
     mod that adds it to ride it.";
  4. the safe spot's refusal without the coordinates and colliders (they go to a `[safe-spot] no return` log line).
- **Also:** the babies slider's "None" is the API's word.
- **Self-tests** (the Translations group): `Bubbles` (every fixed sentence and template under the test language,
  their reasons English, another mod's as is, the prompts, the notice, a bubble said for real; then a German de.txt
  read back: a sentence, a template with the donkey's German name, a stale line, a prompt, the notice, the journal's
  list); `PackNames` (every owner's English lists its mounts' names; a pack's mount and a named skin under the test
  language and in German from their owners' files: the name, the journal's list and header, the Skin list);
  `Windows` checks every mount's name, description and abilities in the journal's entries.

**Tested 2026-11-01, 00:13** (the Translations group, 7 scenarios: 98 passed, 0 failed, 0 warnings): every bubble
sentence and template looked up with its reason English, bubbles said for real in the test language and in German,
the prompts, the notice; a pack's mount and a skin pack's skin read from German files in their owners' folders, in
the journal and the Skin list.

**The English files of that start, synced the same night:** the API 147 lines (88 added, the two old cargo words
gone), Saddle Up 144 (Gerry), Critters 6, Donkey Skins 3, Dragons 2, in `packs/<Pack>/Languages/` for the first time
and deployed with each pack. The start's dump also listed 8 skin descriptions no window draws (a skin's description
shows only as its mount's, for a mount with none of its own, and every shipped mount has one): `Texts.EnglishOf` lists
a skin's description only where it is drawn now, and the repository's files were trimmed to match. The next dev start
(2026-11-01, 00:28) wrote the dumps anew (Saddle Up 144, Critters 6, Dragons 2, Donkey Skins 3; the API's unchanged)
and `l10n-sync.py --check` found all 10 files the same: the trim confirmed.

**Not yet:** the versions that ship the Languages folders (the user's).

### The plan (2026-10-31)

**The first F11 of the Translations group** (2026-10-31, 23:08): 37 passed, 0 failed, 0 warnings.

- **Every word looked up:** every heading, row label and window word of the three windows. The one text not looked up
  was the player's own mount name in its Name box (left out of the check since).
- **Room at 1.6 times the English:**
  - only the colours window's lantern-light face "Warm (game default)" ran past its box (0.92);
  - the options card "Mount Behaviour" and the journal's "Drive the cart" just fit (1.00).

**The long-text rule, built** (the plan's section 5, after that measurement):

- What ran short were our buttons and a dropdown's face, all `UiKit.OwnButton` labels. They already ended in "…".
- Now they first shrink to 80% (TMP auto size, its largest size the label's own).
- English that fits keeps its size. The next run's TextRoom "fits up to shrunk" shows the room it gained.

### Step 2: the API's remaining words

**The keeper's bubbles:** 36 different in normal play, 79 counting rare paths; 70 of the 88 call sites are fixed
sentences.

- They reach the bubble as plain strings. Many are a `MountResult.Reason`, which is public: other mods read it, log
  it, and may compare it.
- **Proposal:**
  - The reason stays English.
  - Only what `Mount.Say` shows is translated: the API's bubble sentences become words in `UiWords`, and `Mount.Say`
    finds a fixed sentence by its English.
  - A sentence made from a template with values ("{0} pulls {1} items at most…") is recorded with its values when
    it is made, so `Mount.Say` finds it too.
  - Another mod's own reason is shown as it is.
- **The decision:** keep `Reason` English (recommended), or hand mods the translated text.

**The prompts on foot (9):** "Ride", "Ride {0}", "Hitch the cart", "Load on cart", "Take from cart", "Cart is full",
the chest's two.

- They become words and templates; the game's key icon before them stays as it is.

**The rest:**

- **The crossroads notification** (a cfg value): when the player's value is still the shipped English, the
  translation is shown; a text of the player's own as it is.
- **Key names** stay English (D8).
- **GK2 Mod Framework's entry** (its name and buttons, given once at start, before any language) stays English. The
  Framework has its own translation files for a mod's name.

**English to change first: the user's yes or no, each on its own** (open items). None blocks step 2:

1. **The safe-spot bubble's brackets** (coordinates and game collider names): drop them from the bubble, into the log.
   That also removes 6 texts nobody could translate.
2. **"1 items"**: "Cargo 4" as a number, "Pulls a cart (cargo {0}).", "Too much on the cart ({0} of at most {1}). Take
   some off first."
3. **The waiting skins' sentence** ("A skin … is / 3 skins … are installed …", the one plural chosen in code): one
   form, "Skins for this mount are installed ({0}), but not the mount itself: install the mod that adds it to ride it."

**Size:**

- about 60 more words in `UiWords`, and `Mount.Say`'s lookup;
- call sites in about 15 files: `CartChest`, `Mount`, `Cargo`, `MountInstance`, `SafeSpots`, `MountInstances`,
  `Flight`, `InstanceControls`, `HeadReroute`, `Looks`, `Corpses`;
- a self-test: every bubble word said once under the test language, bracketed.

### Step 3: the packs' texts

**What:** a mount's `name` and `description` (`mount.cfg`) and a skin's `name` (`skin.cfg`).

- Saddle Up's built-in Gerry is Saddle Up's.
- The Dragon, Red Dragon, Hellfire, Royal Parade and Armored are their packs'.

**Owner:** the plugin that registered the mount or skin. The API already keeps it (`Looks.SkinDef.Owner`; the
catalogue's mounts likewise), so the lookup needs no new knowledge and the layering holds.

**Keys:** `mount.<mount id>.name`, `mount.<mount id>.description`, `skin.<skin id>.name`.

**Files:** in the pack's own `Languages/`, beside its DLL. The packs embed their cfgs, but a translation file is loose
like anyone's.

- `tools/make-release-zip.py` already ships a pack's `Languages/`.
- `tools/l10n-sync.py` already maps a pack's GUID to its folder.

**Where drawn:**

- the journal's list cards and header;
- the colours window's title;
- the Skin list's choices (`Texts.Choice`, the List branch);
- "Ride {0}" (a mount's own name given by the player stays as typed).

**The English file:** the dev run writes one for every owner of a registered mount or skin (the packs have no page),
at the first page shown.
