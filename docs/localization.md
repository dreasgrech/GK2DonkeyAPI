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
| Released packs | **6** | Larry's and the Dragon's name and description; Hellfire and Royal Parade. Also 2 plugin names in the game's Mods window |

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
- a default skin's own name. "Red Dragon" and Larry's skin "Larry" show as "Default".

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
| **The game's words we could reuse** | Our words looked up in the English table | **Few.** Cancel (`btn_cancel`), OK, Options (`ui_menu_options`), Name (`ui_name`), On/Off, Yes/No. Not Done, Defaults, Ride, Speed, Cargo, Skin or the colours. Larry and Donkey have no entries of their own. |
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
  - No released mount has cargo 1: the Critters' are 2, 4 and 6, and Larry's is 4. So only another mod's mount shows it.
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

**Decided again 2026-11-01 (the user): CSV only.** "Scrap the txt files and lets stick to csv only. less to
maintain." Built step 1 shipped and read both (below); since then the mod reads `<id>.csv` alone and ships `en.csv`
alone, with a fourth column, `note`, for the context the `.txt`'s comments carried. The data below is kept as it was.

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

**To decide:** mount names. "Donkey" and "Dragon" are words; "Larry" is a name. The recommendation is that they are
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

(Since 2026-11-01 CSV only: the `.txt` reader and `en.txt` are gone, see D2. What follows is step 1 as built.)

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
  - `tools/l10n-glyphs.py` lists the characters of the translations the game's own text never draws in their
    language (from its strings tables, GK2_L10N_CORPUS): the ones a font may lack, for the in-game Fonts check.
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
gone), Saddle Up 144 (Larry), Critters 6, Donkey Skins 3, Dragons 2, in `packs/<Pack>/Languages/` for the first time
and deployed with each pack. The start's dump also listed 8 skin descriptions no window draws (a skin's description
shows only as its mount's, for a mount with none of its own, and every shipped mount has one): `Texts.EnglishOf` lists
a skin's description only where it is drawn now, and the repository's files were trimmed to match. The next dev start
(2026-11-01, 00:28) wrote the dumps anew (Saddle Up 144, Critters 6, Dragons 2, Donkey Skins 3; the API's unchanged)
and `l10n-sync.py --check` found all 10 files the same: the trim confirmed.

**Not yet:** the versions that ship the Languages folders (the user's).

**The first translation (2026-11-01): French**, for GK2 Saddle Up and GK2 Donkey API (`fr.csv`, 147 and 144 lines;
the user: "so we can see it and test it"). In the game's own French words where it has them (from its strings table,
`lng_fr.tsv`): Gardien, corps, Carrefour, Morgue, coffre, zombie, lanterne, Paramètres, Annuler; "charrette" for the
cart (the game has none) and "âne" for the donkey, as the game says. Key names in the sentences stay the mod's
(Shift, Space, Esc: D8). Within the measured room: the tightest, the lantern's "Chaude (défaut du jeu)", grows 1.16x
against 1.9x of room. `l10n-sync.py` checks it with every shipped translation (147 of 147, 144 of 144).

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

**What:** a mount's `name` and `description` (`mount.cfg`) and a skin's `name` (`skin.cfg`); since 2026-11-03 also
the names of a skin's own tones (`[Tone.<id>] name =`, the labels of their dropdowns in the colours window;
docs/mount-colours.md section 7), whose choice words are the API's.

- Saddle Up's built-in Larry is Saddle Up's.
- The Dragon, Red Dragon, Hellfire, Royal Parade and Armored are their packs'.

**Owner:** the plugin that registered the mount or skin. The API already keeps it (`Looks.SkinDef.Owner`; the
catalogue's mounts likewise), so the lookup needs no new knowledge and the layering holds.

**Keys:** `mount.<mount id>.name`, `mount.<mount id>.description`, `skin.<skin id>.name`,
`skin.<skin id>.tone.<tone id>.name` (by the skin that declares the tone: a variant's tone taken from its default is
the default's line).

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

## 9. Translator mistakes (built 2026-11-01)

The user: "other people aka users will be adding the translations which means that we need to expect EVERY TYPE OF
STUPID mistake they can do". A translation file is read whatever a person or their spreadsheet did to it
(`TranslationFiles.cs`, with `Texts.Load`/`Take` for what needs the mod's English, `LegacyText.cs` for the code pages).

**The rule (as it reads since review round 6, section 11):** the mod never shows a text that isn't in the file, nor a
text for an English it wasn't made for; a mistake shows English, and is said. The mod repairs nothing in a file: six
review rounds each found a repair that made a wrong text in a corner (code pages, garbled letters, a key found by its
English, a text joined across its separator), and each is off behind a switch of the user's. Garbled text the mod
detects is left out; garbled text it doesn't is shown as the file has it, for the translator to see. Every mistake that loses or changes something is a warning in the log naming the file and line (at
most 10 a file, then one saying there are more); the log's line per mod and language counts what was read, left out
and repaired. Nothing in a file can throw: the whole load falls back to English.

**The catalogue** (each a case of `TranslationTests.Mistakes`, run in the game by the Translations group and without it
by `tests/TranslationScan`, which also reads every shipped `Languages/*.csv`: no warning allowed):

| Mistake | What the mod does |
|---|---|
| Excel's plain "CSV" (the system's ANSI code page), "CSV (MS-DOS)", "CSV (Macintosh)", a Linux or LibreOffice page (ISO-8859, KOI8-R), a file of UTF-8 and code-page lines, one stray byte | not read, said ("save it as CSV UTF-8"): a file must be UTF-8 or UTF-16 (since round 5; the code-page reader, `TranslationFiles.ReadCodePages`, is off: section 11) |
| letters lost as "?" (Excel's plain "CSV" of a Russian or Japanese text on a Windows set to another language) | the line left out (more "?" than its English, two together, or one before a letter), said, in a file without its language's own letters (one that has them wasn't saved so: its "?" are question marks) |
| Excel's "Unicode Text" (UTF-16, tabs, saved as `.txt`), UTF-16 without a BOM (either byte order, found by its zero bytes) | read; the `.txt` name said |
| a workbook (`.xlsx`, `.ods`) beside, or renamed `.csv` | not read, said ("Save As CSV UTF-8") |
| UTF-8 opened in a code page and saved again ("Ã©" for "é", "â€™" for "’", Cyrillic "Рџ" for "П", Polish "Ä…" for "ą", "√©"; even only two letters, twice, or in some lines only) | left out, said, where the mod sees it (Garbled); shown as the file has it where it doesn't (the repair, `RepairGarbled`, is off since round 6) |
| Mac Excel's garbling ("√©" for "é") | repaired through Mac Roman, said |
| a correct text that looks garbled through its page (Russian "Её", a capital and a no-break space) | read as it is (a garbled text's marks must cover most of its letters) |
| a text still garbled (a character came in after the garbling, so its bytes can't come back); Mac garbling without its "√" ("–ü—Ä"); Japanese read as Shift-JIS ("縺薙ｓ"), Chinese as GBK, Korean; a script the language doesn't write (a Chinese character in French: UTF-8 through GBK; "Ÿ" alone in Russian, "Ű" in Korean) | left out (English), said |
| an empty file, only a header, random bytes, old Mac line ends (CR only) | no crash; English, or the lines read |
| no header (deleted, or a tool's file without one), also in a translation tool's order (`key,note,english,value`) | not read, said: a file is read from en.csv's header row on (read by its columns' places, a note column showed as the translation) |
| Excel's `sep=` line, comments, a blank line or a title line (`日本語訳`, `Translated by Marie, 2026, v1.2`) before the header | the separator: the one after the first cell of the first rows (comments and blank lines skipped); a title line said; read from the header |
| a header in other words or a translation tool's (`clé;valeur;anglais`, `キー,値,英語`, `ID,Status,English,Français`, `Key (e.g. options.done),…`, `,note,english,value`), also reordered | not read, said (copy en.csv's header row as it is): a header's columns are matched by en.csv's own names only (`key`, `value`, `english`, `note`; any case; spaces, quotes or a no-break space around) |
| a section pasted under such a header, below one of en.csv's (also with a cell left empty, narrower than its rows, or under a header with a name twice) | not read up to the next header of en.csv's, said: under a header every line is one of the mod's (a key, also one damaged by one keystroke), a `#` comment or blank, and any other line stops the reading (each test of a header in other words by its words or its width let a section through, read by the columns above it) |
| a key damaged by one keystroke (its dot deleted, a comma or a space typed into it); a line typed with spaces for its commas, or as `key = value` (its first word a key); a numbered row under a header without an index column; a line break typed into a text or its english (the line after it holds the rest and the row's English) | that line left out, said |
| a comment, a title or a line of notes without `#` under the header (`Checked up to here, the rest later`, `TODO`) | the lines after it not read up to a header of en.csv's, said (a comment starts with `#`; such a line can't be told from a header in other words) |
| a section in the other separator pasted under its own header (a `;` section in a `,` file) whose text holds the file's separator | the lines from that text on not read, said (the section's rows are split at the file's separator) |
| rows of another column order pasted under en.csv's header without a header of their own | a limit: read as the header above them (nothing in the file says otherwise) |
| something before `key` in the header: a stray separator | read without it, said |
| something before `key` in the header: a column of its own (`done`, `No.`), a garbled BOM (`ï»¿key`) | not read, said so |
| each row whole in one cell (Excel in a `;` country), also with Excel's `;` padding after it | split again, with the same quote checks (a quote not doubled, or a text cut at its line: left out, said) |
| a template's translation with a doubled brace (`{{x}}`) | English, said |
| two columns (no english), an english cell left empty | those lines left out (English), said: nothing could tell when the English changes under them (a choice's key is its place in the list) |
| an index column before the key, named in the header (pandas' `to_csv`: `,key,value,english`; `#,key,…`) | read without it, said (up to a header without one) |
| a separator typed at a text's start (the row's English one column right of the english column) | English, said |
| a header that doesn't name `value` and `english` once each (`key,english,`, `key,value,note`, `key,value,english,value`, `key,french,english`) | not read, said |
| a control character in a text | English, said |
| a multi-line text whose last line starts with a web address (`www.exemple.fr, deux`) | read whole (a swallowed row starts with one of the mod's keys) |
| a separator inside a text without quotes (`t.greet,Bonjour, toi,Hello`), also a comma row in a `;` file | English, said (its English after the rest of it; the join, `JoinSeparator`, is off since round 6) |
| a lone CR ending rows, also among LF rows (Mac Excel) | a row end |
| none of the english cells the mod's English (a stale file, notes in the english column's place) | English, said |
| a separator inside the english text (typed into it, or an English with a comma whose quotes were deleted) | English, said |
| symbols and emoji in a text (♥ → ✓ ≥ 😀) | read; a character no text of its language writes is named when its line is left out |
| a quote inside a quoted text, not doubled (`"Bonjour" dit-il, puis "salut"`) | left out, said |
| a `{n}` its English hasn't | left out, said |
| a header in the translator's language after `key` (`key,valeur,anglais`) | not read, said (copy en.csv's header row) |
| a header naming its columns otherwise (`key,checked,texte,anglais`, `key,Value (French),Notes (translator),English`) | not read, said (no column is taken by a guess at its name: a notes column named "Notes (translator)" was once read as the translation) |
| a quote opening a row's first cell (a comment or a key) | the file read line by line, said |
| an empty key column with text after it, under a header | left out, said |
| a full-width brace that is no placeholder (`｛注意｝`), `{{ }}` with no number | kept as written |
| a translation in another column beside the template's `value` (`translation`, `french`) | the value column is read (another column is never taken: it may be a reviewer's "ok", a translator's notes); a column en.csv hasn't is said when it holds text |
| spaces around fields, empty columns after (also a column without a name: said when it holds text), a second header (files put together), `#` comment rows, a column of the translator's own (`todo`, `Notes de traduction`: said when it holds text), the columns in another order named by en.csv's names with the translation before the english (`key,note,value,english`) | read; an empty one: said |
| the english column before the translation (`key,english,value`, `key;Englisch;Deutsch`) | not read, said (a comma typed into a text there would cut it with nothing to show it: put the columns as en.csv has them) |
| text in more cells than the header has columns; one of the mod's keys past the header's named columns, or text there and the English out of its place (two lines joined, also under Excel's padding or a column without a name) | that line left out, said |
| Excel's `;` padding after the header, then a comma typed into a text | English, said (the test of a text cut by a separator counts the header up to its last named column) |
| a text cut by a comma typed into it whose tail is the English (`Bonjour, Hello,Hello`); a column after english holding the English | English, said (its English twice; the message names both causes) |
| a key with nothing after it | left out, said |
| a quote inside a text; a quoted text with `""` and the separator | read as meant (a quote opens a quoted field only at its start) |
| a quote never closed; a quote that swallows the rows after it | the file read line by line: the broken line English (with the lines of its text after it, up to the next key's line), the rest read; said |
| a quoted text over lines, one of them starting "e.g.," | read (a swallowed row starts with a key: the mod's own or key-shaped) |
| a stray quote swallowing rows with spaces or quotes around their keys, or a key the mod doesn't have | the file read line by line, said |
| a line break in a one-line text; a literal `\n` | a space (a line break only where the English has one) |
| a key in other case, with invisible characters (zero-width space, BOM) | read |
| invisible characters at a text's ends; joiners inside it (U+200C, U+200D: Persian and the Indic scripts) | the ends taken off; inside kept |
| a key typed wrong or translated | left out, said (finding it again by its English, `RescueByEnglish`, is off since round 6) |
| a key twice | the first, said; the report's rows added under old ones (out of date, or still the English): the current line, not said |
| a key typed wrong whose text is a key (`x.1,t.greet,Hello`) | left out, said |
| the report kept as `fr.missing.csv`, filled in | not read, said ("add its rows to fr.csv") |
| the value and english columns swapped; the english column deleted and the note moved into its place | those lines show English, said (a "swap back" drew the note as the translation) |
| the english column translated too (it equals the value) | those lines show English, said |
| a translator's line out of date, or still the English in a file mostly the English (a half-done copy), beside the bundled file | the bundled translation of that line (a file mostly translated keeps a word it left English on purpose) |
| a line's English changed since (one line or a whole stale file) | English (the stale-English rule stands) |
| `{ 0 }`, the full-width `｛０｝` of Japanese input, `{O}` (a letter) | read as `{0}` |
| a template that lost a `{n}` the English has, a `{1}` the English hasn't, a broken brace | English, said (the two noun sentences may drop their `{0}`: `Word.MayDropValues`); a broken one isn't formatted again |
| a spreadsheet error cell in any language (`#NAME?`, `#VALUE!`, `#ИМЯ?`, `#¿NOMBRE?`, `#N/D`, LibreOffice `Err:`) | English, said (known by its shape) |
| `{{0}}` (other translation tools) | read as `{0}` |
| a text that is only `\n` or tags | English |
| `<nobr>` tags; a very long text | tags out; read |
| names: `FR.csv`, `pt_BR.csv`, `zh-CN.csv` | read |
| names: `fr-FR.csv`, `pt.csv` in a `pt-br` game, `jp_JP.csv`, `french_FR.csv` (a region, the base language, a name with a region), `fr` without an extension | read after the language's own file in its folder, said |
| names: `fr - Copy.csv`, `fr_old.csv`, `fr (1).csv` (a copy, an old version, a download's second copy) | not read, said |
| names: `zh-TW.csv` in a `zh_cn` game | not read (Traditional Chinese is another language) |
| names: `french.csv`, `français.csv`, `jp.csv`, `fr.csv.txt` (a hidden extension) | read, said ("name it fr.csv") |
| the old `key = value` `.txt` | not CSV: English, said |
| `en.csv` translated in place | English (en.csv is never read), said ("save it as fr.csv") |
| the file beside the DLL instead of in `Languages\`, or straight in `Translations\` without the mod's folder | not read, said with the right place |
| the `Translations\` folder named after the mod (`GK2SaddleUp`) instead of its GUID | read (an INFO line names the GUID) |
| `Translations\` straight in `plugins\`, one folder deeper (`plugins\X\X\Translations`, a zip unpacked into its own name), a `Languages\` folder inside the mod's | read |

**Also:** the folders are read in turn, a translator's before the bundled one (a translator's file of any name before
the bundled ones); in a folder the language's own file first, then a language name's, then the base language's or a
region's (a game in `pt` reads `pt.csv`, then `pt-br.csv`); `tools/l10n-sync.py --check` fails on a shipped line whose
`{n}` don't match.

**The side effects fixed with it:** the files are read ahead (`Texts.TickWarm`, one mod a frame, at a game's start and after
a language change); the journal closes when the language changes under it (normal play can't: the game's Settings
window doesn't open over ours), keeping the choices; the named prompt ("Monter Bella") is made once per name and
language, not every frame. The packs have their French too (11 lines).

**Left as decisions:** key names (D8: English; the game itself translates only the four arrows) and the GK2 Mod
Framework's entry (the Framework reads translations from its own folder, so our zip would write into another mod's).

## 10. Every game language (2026-11-01)

The user: "how difficult would it now be to add translations for all the languages? First of all, double check our list
of languages is correct and then lets create the translations. might as well."

**The list, checked against the game:** the Fonts self-test switches through the game's own `LLBase.AvailableLanguages`
and records each id: en, de, fr, pt-br, es, ru, pl, ja, zh_cn, ko, tr (ja, zh_cn and ko have fonts of their own:
japanese, chinese, korean; the rest small_font). The game's own strings tables (11 x 9,474 lines) were extracted
earlier (`lng_<id>.tsv`).

**How they were made:** one agent a language (de, es, pt-br, ru, pl, tr, ja, zh_cn, ko; French was written before),
each from the English with its notes and the French for meaning, the game's own words looked up in its table for that
language (the Keeper, the donkey, a body, the Crossroads, the Morgue, a chest, zombie, lantern, options, cancel), and
written only through a validating writer (every key, every `{n}`, no line breaks, the measured room, a wide East Asian
character counting 2). Grammar rules given to all: a name in `{0}` never takes a case ending, article or particle
(colons, the subject's place); no noun after a number; key names English; one term each for the mount, the cart, the
skin (not the game's word for flesh), the babies and the Mount Journal. Each language: 147 + 144 + 6 + 3 + 2 lines;
`tests/TranslationScan` reads all 50 files clean and `l10n-sync.py --check` counts every one whole.

| | Keeper | donkey | cart | mount | Mount Journal | skin | babies | address |
|---|---|---|---|---|---|---|---|---|
| fr | Gardien | âne | charrette | monture | Journal des montures | apparence | petits | vous |
| de | Wärter | Esel | Karren | Reittier | Reittier-Journal | Skin | Jungtiere | du |
| es | guardián | asno | carreta | montura | Diario de monturas | aspecto | crías | tú |
| pt-br | Coveiro | burro | carroça | montaria | Diário de Montarias | aparência | filhotes | você |
| ru | Смотритель | ослик | телега | скакун | Журнал скакунов | облик | детёныши | вы |
| pl | grabarz | osioł | wóz | wierzchowiec | Dziennik wierzchowców | skórka | młode | ty |
| tr | Bekçi | eşek | araba | binek | Binek Günlüğü | görünüm | yavrular | sen |
| ja | 墓守 | ロバ | 荷車 | 乗り物 | 乗り物図鑑 | スキン | 子供 | polite / the Keeper plain |
| zh_cn | 守墓人 | 毛驴 | 板车 | 坐骑 | 坐骑图鉴 | 外观 | 幼崽 | |
| ko | 묘지기 | 당나귀 | 수레 | 탈것 | 탈것 도감 | 외형 | 새끼 | 합니다 / the Keeper casual |

**For a native speaker to look at** (the agents' own doubts):
- de: "Tempo" for the speed stat. (The harness colour was "Zaumzeug", the bridle only, until the review rounds: it
  paints the whole tack, straps, saddle and reins, so "Sattelzeug"; "Geschirr" is the game's own word for tableware.)
- es: carreta or carro; "Acorazado" for Armored.
- pt-br: "Blindado" for Armored; "Minhoca" for the worm.
- ru: the game says Хранитель in its menus and Смотритель in its dialogue (96 to 11): Смотритель chosen; "Опции" as the game has it.
- pl: "Wykonaj" for the Go button; "Robak" or "Czerw" for the worm.
- tr: "araba" for the cart is also today's "car".
- ja: 8 characters the game's own Japanese never uses were worded around (普 隣 淡 鍮 暖 昼 四 俊: the font may lack them).
- zh_cn: 这家伙 ("this fellow") where the noun is dropped; the snail description's wordplay lost.
- ko: 털 색상 (fur colour) for the coat.
- Several: "Your mount is nowhere to be found." became the Keeper's first person ("my mount") in the bubble.

**The fonts, still to measure:** zh_cn uses 22 characters and ko 13 that the game's own text in those languages never
uses (among them 龙, the dragon; 蜗, the snail; 崽, the babies; Korean 싣 in the load prompts). If those fonts are cut to
the game's characters, they would draw boxes. The Fonts self-test now checks every character of our translations in
each language's font (fallbacks searched, a dynamic font may add them): its next run says which, if any, are missing;
only then is anything reworded.

## 11. Review rounds (2026-11-01)

The user: "give the translations and the entire system another look", then "do more rounds until we find no issues for
some runs" and "make sure we have enough tests to cover them especially for ui layout issues".

**The translations:** every language proofread again and again by a fresh reviewer (one shared brief: the English, each
line's note, the game's own words for its things; change only clear problems, never taste; the same doubts not raised
again), until two rounds in a row changed nothing. Lines changed per round:

| | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| fr | 48 | 0 | 1 | 0 | 0 | |
| de | 10 | 0 | 0 | 0 | 0 | |
| es | 9 | 2 | 0 | 0 | | |
| pt-br | 9 | 0 | 0 | | | |
| ru | 6 | 1 | 0 | 1 | 0 | 0 |
| pl | 12 | 0 | 0 | | | |
| tr | 9 | 3 | 0 | 0 | | |
| ja | 5 | 1 | 0 | 0 | | |
| zh_cn | 4 | 0 | 0 | | | |
| ko | 15 | 0 | 0 | | | |

What the later rounds found: the key that loads a chest is the game's Work control and the set-down key its Interact
control (the English said "the action key", a name the game gives neither: fixed in the English and all ten); each
language's own word for the town fight; "simple" read as the game's Simple Chest (French); a bubble in which the Keeper
addressed someone (Russian); the climb and descend keys (Spanish); the warm colour (Japanese). German's harness word
was changed after its third round, from the code (the colour paints the whole tack), and German was read twice more
(rounds 4 and 5, nothing found).

**The code:** an independent review of the reader and the lookup, with its own probe against the built DLL, found 18
issues; two showed a wrong text: the column "repairs" (a translator who deleted the english column and kept the note
saw the notes drawn as translations) and garbled letters left garbled (two marks only, Cyrillic or Polish garbling, one
stray byte turning a whole UTF-8 file into a code page). All are fixed (the table in section 9 is the result): lines
without their English left out; columns by the header's names; the repair line by line through the language's page;
a file of two encodings read line by line; a translator's out-of-date or half-done line no longer hiding the bundled
translation; a translator's file of any name before the bundled ones; copies not read; zh-TW never for zh_cn; the
separator from the first rows' first cell; the swallowed-row test by the mod's keys; UTF-16 by its zero bytes; a broken
template not formatted again; a renamed game field said once; the translators' folders looked for again on a language
change; a plugin name no folder can have skipped; joiners kept inside a text; the report counting a line still the
English apart; the self-test driver disposing what it unwinds; `l10n-sync.py` reading a file as the mod does;
`tests/TranslationScan` refusing a DLL older than the source. One fix of the review's was itself wrong at first and
was caught by the shipped files: the leftover-garbling check counted Russian "лё" as a mark (ё is a later byte in
Windows-1251); a mark is now a whole UTF-8 sequence.

A second round (a fresh reviewer, its own probes) found the first round's fixes sound and 14 more, 8 of them showing a
wrong text: letters lost as "?" by Excel's plain "CSV" on a Windows set to another language (the most common: a Russian
or Japanese file all "?"), Excel's "CSV (MS-DOS)" and "CSV (Macintosh)" read through the Windows page, error cells in
the other languages' spreadsheets, `{{0}}`, Mac Excel's garbling, Japanese and Chinese garbling, a correct short Russian
text "repaired" into another letter, a stray quote before a row with spaces or quotes around its key; and the report's
rows added under old ones losing to the old ones, a correct Russian typography left out, a translation typed into a
column of its own, translators' folders one level off, a word a translator left English on purpose replaced, file names
like `jp_JP.csv`, a lost line inside a quoted text, a typo key before the right one, one stray byte losing a line. All
fixed, each first a failing catalogue case (32 failed: 128 cases now); `tests/TranslationScan` also fails on a shipped
line the mod would leave out, and `l10n-sync.py --check` too. Not handled: a file started from another language's copy,
or a column sorted alone in Excel (both give right-looking wrong texts no reader can tell from translations).

A third round brought a new instrument: a sweep that saves every shipped file every wrong way a spreadsheet or editor
saves it and reads it back. It found 7 more ways to a wrong text, and the cause they shared: the reader decided the
encoding line by line, and every round patched one more line-level case. **The method changed** (the advisor's call,
2026-11-01): an encoding is the file's. A file that isn't UTF-8 is read as a whole in its language's Windows page; a
UTF-8 line among its lines is restored by the repair, a line with one stray byte read piecewise; every other line must
hold only the letters and punctuation its language writes (`Plausible`), else the file is in a third encoding (Excel's
"CSV (MS-DOS)", "CSV (Macintosh)") and isn't read at all, its plain ASCII lines too. Chinese, Japanese and Korean files
must be UTF-8 throughout (their Windows pages need tables the mod hasn't). Round 3's other findings (letters lost as "?"
counted against the English, Mac garbling and a script the language doesn't write, an index column, a reviewer's column
taken as the translation, a separator inside a text, broken quoting, a `{n}` the English hasn't, a report kept as
`fr.missing.csv`, a header in the translator's language, the report's row of a line still the English) are fixed too.

**The sweep** (`tests/TranslationScan`, its third part): every shipped translation (50 files) saved as UTF-8 with and
without a BOM, UTF-16 (both orders, with and without a BOM), Excel's plain "CSV" in its Windows page, "CSV (MS-DOS)"
and "CSV (Macintosh)" in the language's DOS and Mac pages, with letters lost as "?" on a Windows set to another
language, and garbled by a re-save through its page, Windows-1252, Mac Roman, GBK, Shift-JIS and the Korean page; each
with LF, CRLF and CR line ends: 2,250 reads through `Texts.Of`. Every key must show its translation or English, never
anything else; the correct ways must show every translation. Before the change: 519 reads failing; after: **0**, with
60 at the documented limit below. A clean review round is defined by it: no wrong text through a realistic save path,
the sweep at 0, the catalogue green; documented limits don't count.

**Documented limits** (the user's decisions, docs/open-items.md): Chinese or Japanese UTF-8 garbled by a re-save through
another East Asian page (GBK, Shift-JIS: the garbage is real Chinese characters; Korean is caught); a Chinese, Japanese
or Korean file saved in its own Windows page (cp936, cp932, cp949: not read, said: tables for them would grow the DLL);
a file started from another language's copy, or a column sorted alone in Excel (right-looking wrong texts no reader can
tell from translations); a program that transliterates instead of writing "?" (Polish "Czesc" for "Cześć").

A fourth round found the file-level check only as strong as the file is long: a translator's override of one or two
lines, saved as "CSV (MS-DOS)" or "CSV (Macintosh)", looked plausible in the Windows page and was read wrong ("Cr¡as"),
and garbling through the language's own Mac page or ISO-8859-1/-15 slipped through. The answer was the same principle
one level up, "nothing else may be meant": a non-UTF-8 file is read through every page its language's programs save in
(LegacyText.Candidates, generated like the rest), the pages with an implausible line dropped, the readings left merged
line by line (a line read two ways is left out), so an MS-DOS or Mac file is now read in its own page when only that
page reads it plausibly; a garbled line is repaired through every page it may have passed, and left out when two pages
repair it otherwise (a correct text never round-trips: such a line is garbled for sure). Plausible grew a rule (a capital
right after a small letter), the Western lists every Latin-1 letter but the few that made pages look alike, and a value's
script is checked for every character (a code-page file valid UTF-8 by accident: Mac Turkish "ıç" are a Syriac letter's
bytes). The sweep grew a second phase: every shipped line alone (27,482 small files) saved every code-page and garbled
way as a translator's override over the correct bundled file: before the change 1,567 read wrong, after: **0**.

**Round 5, and the decision.** A fifth round found three more ways to a wrong text, all through code pages and all
with one cause: a page ruled out for the whole file stopped being a possible meaning even for lines it read plausibly
(a UTF-8 file with one line in the Windows page; a Windows file with a "½" whose twin page ISO-8859-15 reads it as "œ";
an MS-DOS file with one line retyped in Windows Excel). Five rounds on the same axis: every reader that admits more than
one encoding has a corner where two readings are plausible and the file is small or mixed, and the rounds were listing
those corners. **The code-page reader is switched off** (`TranslationFiles.ReadCodePages = false`, the advisor's call):
a file that isn't UTF-8 or UTF-16 isn't read, and the translator is told to save it as "CSV UTF-8" (one re-save; our own
template opens as UTF-8 in Excel, it has a BOM). It can't show a wrong text, because it isn't read. The reader stays
compiled with its tables, so switching it back is one line; the costs, measured by the reviewers, for that decision:
- on as it is (file-level page choice, line merge): wrong texts remain in the mixed and rare-sign corners above
  (e.g. 8 of 122 French lines re-saved alone in the Windows page inside a UTF-8 file; "½" in a Windows-1252 file);
- on with every page counted per line (the only variant with a safety argument): 15 to 47% of a correct Windows-page
  file's lines left out (Russian 140 of 300, German 27 of 85, Spanish 38 of 96), told.
Round 5's other findings (a lone CR as a row end, english cells matching none of the English, the separator repair
joining a stale line to a reference column, symbols and emoji, the culture-free line-end test) are fixed for UTF-8 files.
The sweep grew a third phase, a UTF-8 file with one line saved in a code page (3,564 reads); with the reader off, every
code-page way is refused: never a wrong text by construction.

**Round 6, and the rule as it reads.** With code pages off, a sixth round found the same shape in what was left: every
remaining way to a wrong text was a repair the mod made to the translator's file. The garbled-letter repair gave a wrong
letter when a literal `\n` stood before a capital, and garbled Polish all-capitals; the rescue of a mistyped key by its
English took a line of a multi-line text for a row (a correct file with a web address in a text showed `deux"`); the
separator join joined a stale line to a column of the new English. Each repair is off behind a switch, kept compiled:
`TranslationFiles.RepairGarbled`, `Texts.RescueByEnglish`, `Texts.JoinSeparator`, and `TranslationFiles.ReadCodePages`
from round 5. The rule says what a reader can promise: **the mod never shows a text that isn't in the file, nor one for an
English it wasn't made for.** Garbled text the translator saved is that file's content: where the mod sees it, the line
is left out and said; where it doesn't, it shows as written, and the translator sees it in the game. Round 6's parse bugs
are fixed (a header naming no translation column, an index or empty first column without a header, an empty column
after the key, an index with a stray quote, control characters, a swallowed-row test that took a web address for a key),
and three self-tests no longer read real translators' folders. The sweep's garbled ways accept the file's own text as
written; anything the mod made fails. A fourth phase reads **the game's own strings tables** as translation files (9,473
lines a language, with literal `\n`, capitals, quotes, multi-line texts; given by `GK2_L10N_CORPUS`, never a path in the
repository), correct and garbled through eight pages: 0 wrong in 85 reads. It also showed the "?" rules leaving correct
lines out (Korean 235, Russian 114, French 112: translators add questions): they now apply only to a file without its
language's own letters, and the game's correct tables read whole (but its real `#ERROR!` cells).

**Round 7** checked the rule as it reads now and found parse corners where the mod still produced a text the file
hasn't: a row Excel in a `;` country put whole in one cell was split again without the quote checks (quotes lost, a
two-line text cut at its line), and a full-width `｛` that is no placeholder became an ASCII `{`; also a header naming no
value column took a reviewer's column, a quote opening a row's first cell swallowed rows silently, an empty key column
was skipped silently, `{{ }}` lost a brace, and a table read before a late mount or skin registered never took its
lines. All fixed, each a catalogue case first (8 failed before). The instrument grew where round 7 showed it blind: the
sweep re-saves every shipped file as Excel in a `;` country does (every row in one quoted cell), and the corpus checks
the correct tables against a text made without the mod's own cleaning (no character changed) and fails a line left out
unsaid.

**Round 8** fuzzed every shipped file line by line with the mistakes a text editor makes (a comma, a quote, a line
break typed into a text; quotes or separators deleted; two lines joined) and in other column orders, and found the one
layout that can't check itself: with the english column before the translation, a comma typed into the text cuts it and
nothing after it moves, so the cut text showed silently (in the template's order its tail lands in the english column,
which then doesn't match). That layout is refused (said: put the columns as en.csv has them), a row with text in more
cells than its header is left out (said), a row whose other cells are only Excel's `;` padding is split again, a
template's translation with a doubled brace shows English, and a translation column beside the value one is always said.
The fuzzer is now a test: every unquoted shipped line with one edit typed into its text (12,800 reads), and the sweep
saves as Excel in a `;` country really does (cells split at `;`, short rows padded).

**Round 9** fuzzed the header: its columns renamed (en.csv's names in other cases and spaces; names in the
translator's language, a hint added, notes named with a translation word), reordered and added to. The header's names
were matched in many languages and by guesses (a translation word, the language's name, the first column that wasn't a
note's), and a notes column named "Notes (translator)" or "Notes de traduction" was read as the translation (1,040 of
5,952 French header variants showed a wrong text); a header in other words, reordered, was read by position and showed a
note. The same answer as rounds 6 to 8: the guess removed. A header's columns are matched by en.csv's own names only
(`key`, `value`, `english`, `note`, any case, spaces around); a header that doesn't name `value` and `english` once each,
or a row that is a header in other words (its first cell without a key's dot, two more cells text, none an English of
the mod's), isn't read (said: copy en.csv's header row); a column en.csv hasn't is never read and said when it holds
text. Also: Excel's `;` padding after a header no longer hides a comma typed into a text (that test counts the header up
to its last named column), rows joined in a file without a header are left out (text past en.csv's four columns), a
separator typed at a text's start under a header or into the english text is said, an index column without a header is
dropped row by row (a comma typed at one line's start no longer shifts the lines after it), and a dotless key on the
first two lines is said like any unknown key. The fuzzer is now a test, phase 6: every shipped translation with its
header renamed one column at a time and all in other words, in all 24 column orders, and with a column of the
translator's own put in at each place (55,600 reads: with en.csv's names, the key first and the value before the
english, every translation must show; else every key its translation or English; a control that alters every value must
be caught, and is on all 50 files).

**Round 10** found the header-in-other-words test itself a guess: a translation tool's header (`ID,Status,English,
Français`: key, context, source, target) wasn't told as one when a column's name was one of the mod's English texts
(`Status`), when its first cell held a dot (`Key (e.g. options.done)`) or was empty (`,note,english,value`), and the rows
after it were read by their columns' places: the context column showed as the translation (48 of 147 API lines, 144 of
144 Saddle Up lines in its probes). The same answer again: reading by the columns' places removed. A file is read from
en.csv's header row on, and a file without one isn't read (said: its first line is en.csv's header row). Under a header,
a section pasted under a header in other words is told by the row's own place: its first cell no key (one the mod has, or
a dotted word without spaces), the cell under the header's english column none of the mod's English, and as wide as the
columns the header names. A comment line without `#` with fewer commas costs its own line: measured, round 9's test
stopped the rest of a section on it (up to 145 keys, told). Also: two rows joined under a header with a column without a
name, or under Excel's padding, are left out (one of the mod's keys past the named columns, or text there and the
English out of its place); a text cut by a comma whose tail is the English is left out; and l10n-sync.py mirrors the
header rule, the index column and the width test (the quotes' and the texts' own checks are TranslationScan's, which
release-scans.sh runs). The instrument counted only wrong texts, never the correct lines an edit costs: edited lines now
edits every row, also quoted ones (typed into its text or english, its key's dot or a quote deleted, a comment line put
in after it), and fails on any other key fallen to English (26,615 reads; a comment line as wide as the header stops the
reading 2,970 times, counted as the limit it is). No shipped text has a line break, so the line-by-line read's cost to
multi-line texts isn't exercised by the shipped files (the catalogue has it). The catalogue's files got en.csv's header
(107 had none: a shorthand that now isn't read); the cases about a missing header flip to not read.

**Round 11**, the last (the user ended the rounds there), found round 10's width test leaking the same way: a section
pasted under a header in other words with a cell left empty (`ID,,English,Français`), narrower than its rows
(`ID,Status,English`), or under a header with a name twice, was read by the columns above it and its status column showed
as the translation (74 of 147 API lines, 71 of 144 Saddle Up lines, no warning). The class closed rather than the width
patched again: under a header every line is one of the mod's, a `#` comment or blank, and any other line stops the
reading up to en.csv's header row (said: a comment starts with `#`). What would make that rule cost neighbours is handled
by the line's own structure, not by a guess: a key damaged by one keystroke (its dot deleted, a comma or a space typed
into it) is found among the mod's keys and costs its own line; a line break typed into a text or its english cuts the
row, and the line after it, holding the rest and the row's own English, is left out with it; in the line-by-line read the
lines after a quote never closed go with its text up to the next key's line. Also: an index column is dropped row by row
(a stray separator before the header no longer costs every key), a column without a name holding text is said, a header
with something before `key` is said so, and the English-twice message names a column holding the English. Edited lines
also types a comma and a space into keys (32,655 reads, 0 other keys lost; every comment line a stop, 5,940, said). The
catalogue's round-10 header insertion had also hit literals that aren't a file (the text `LegacyText.Decode` garbles, the
later pieces of `Join`): taken out. Known and kept (section 9): a comment line without `#` stops the reading; a section
in the other separator stops at a text holding the file's separator; rows of another order pasted without their own
header read as the header above them.

**The tests:** the mistake catalogue went from 63 to 94, then 128, then 150, then 155, then 161, then 168, then 178, then 185, then 197, then 217, then 229, then 231 cases, each new one written first and seen
failing (29, 32, then 22 failed before the fixes); three expectations flipped on purpose in round 3 (a file in a third
encoding isn't read at all; a CJK file not UTF-8 throughout isn't read; a real question mark is one its English has). The Layout scenario (F11, Translations) switches the game through every language with our real
translations and measures in each language's own font: every section of the options window; every text its description
box can hold (each row's description, greyed with its reason where a row can be greyed, a key's how-to by mouse and by
gamepad, the "press the key" line, the hints); the Defaults question with each section's name; the Mount Journal on
every mount and every mount's colours window. A one-line text without room at its smallest size fails; a wrapped text
cut warns; `GK2-text-room.tsv` gets every line with its language. The game's own widgets our words go into are measured
by a scenario of their own, in every language: the Keeper's longest sentence said for real (the thought bubble is a
`UIDialogBubble`; the game's `Bubble.Talk` refreshes its content fitter after showing a message, read from its code)
and the longest prompt shown over the nearest mount (a `UIObjectBubble`; its row's label has a `LayoutElement`). Not
measured: the crossroads notification (the game's notification panel) and the "{0} options" button, which sits in the
game's Settings window.

**The fonts:** the prepared rewordings for characters a font may lack (`zh_cn`: 22, `ko`: 13; applied only to what the
next Fonts check finds missing) had two defects, now fixed: a quote pair half ASCII and half curly (Chinese), and the
Korean chest description still saying "action key". Where a mount's name has the missing character (the snail, the
dragon), its rewording is another name (巨螺, 巨翼兽, 우렁이): a known trade-off, used only if the character is missing.
The first in-game Fonts check (the user's run, 2026-11-01) found 6 of them missing in the Chinese font (毂 踱 ； 跶 驮
蜗) and 4 in the Korean (끕 싣 뜁 옅): those words were replaced in the 18 lines holding them, only the missing
characters' words (轮毂 → 轮心, 蜗牛 → 巨螺, 싣기 → 올리기, ...), the rest of each line as it was; the other prepared
rewordings stay unused. The same run: every one-line text fits in all 10 languages (the layout scenario); three section
headings only by shrinking (fr, pt-br and es "Mount Behaviour", 0.91x to 0.99x of their room); the game's prompt over a
mount with a 22-letter name "cut" in ja, zh_cn and ko (0.86x), the measurement's: measured in the next runs, the label has
its language's font, is as wide as its text wants and takes one line; a CJK font's line is ~14 high, the label 12, and
it draws over its edges (as the game's own Japanese prompts do), so the measurement judges a label that doesn't clip by
its lines since (TextRoom.Fits; a label that clips keeps the height); the Keeper's bubble wasn't found by the test (the
game types a bubble in with a transparent colour tag inside its text: the test compares without tags since).

**The skins' part names** (2026-11-04): the Donkey Skin Pack's 8 new part names (docs/mount-colours.md 7.9) brought
three characters the Japanese font lacks (摺 鍮 鞍; 鍮 one of the eight worded around before) and two the Chinese
lacks (鞍 褥), found by the user's F11 run. Reworded with characters the game's own text in that language draws
(`lng_ja.tsv`, `lng_zh_cn.tsv`) or the same run found in the font (草, 敷): 草ずり, 黄銅, くら敷き; 饰布. Before a new
CJK text ships, its characters are looked up with `tools/l10n-glyphs.py` (GK2_L10N_CORPUS): it lists the characters
of our translations the game's own text never draws (that day ja 4, zh_cn 16, ko 9, and the Fonts check found every
one of them in the fonts: a font holds more than the game's text); a new one it lists waits for the next Fonts check,
which judges.

