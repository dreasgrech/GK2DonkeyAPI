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
the repository: it is a snapshot that the `en.csv` generator of section 5 would replace.

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

**Found on the way: problems that have nothing to do with translating.** They are listed in `docs/open-items.md` and not
fixed.

- **A bubble shows coordinates.** `Mounts.ReturnToSafeSpot` says its refusal in the keeper's bubble (`Mounts.cs` 435).
  When no safe spot holds, the refusal is "No safe spot of this scene still holds (…)" with coordinates and game
  collider names inside (`SafeSpots.cs` 246, 261). Saddle Up's own comment says coordinates mean nothing to a player
  (the user, 2026-10-27), and the same detail is in the log.
- **"1 items"** shows in English for a mount whose cargo is 1.
- **Dead text:** the 26 descriptions of look rows, the picture row's "Your mount" and the journal entry's `Lines` are
  written and never shown.

## 5. The decisions, investigated

### D1. The game's language packs (A) or our own files (B)

| | A: the game's packs | B: our own files |
|---|---|---|
| **What the player does** | Picks "Deutsch (mods)" instead of Deutsch: a second German in the game's list | Picks Deutsch as always |
| **Several mods** | One pack per language for every mod that does this, or packs chained through `fallback`. A player picks one language. | Each mod's files on their own |
| **Side effects** | For a pack in `Mods\` or the game folder, the game writes `_missing_lines.csv` with every line the pack lacks: about 9,470 of the game's own, as our lines are all it carries | None |
| **Our work** | Keys in code, `LLBase.L` with English as the fallback | A loader (folder, file, language), a lookup with English as the fallback, an `en.csv` generator |
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

### D2. The file format

| | The game's CSV | JSON (the Framework's) | `key = value` text (our cfg style) |
|---|---|---|---|
| **Known to** | the game's translators (`strings.csv`, Shift+F10 writes the full English) | the Framework's translators | our pack authors (`mount.cfg`) |
| **Edited in** | a spreadsheet or a text editor | a text editor | a text editor |
| **Breaks on** | a spreadsheet saving with `;` or in the wrong encoding (below) | one missing comma or unescaped quote breaks the whole file | little; long lines need `\n` |
| **Reader** | ours, about 40 lines (the game's `ModsCsv` is public, but a game dependency) | `Newtonsoft.Json` is in the game's `Managed` | the API reads cfg files already |
| **The English beside each line** | a third column | no place for it | a comment line |

**The spreadsheet risk** comes from general knowledge of Excel and was not tried here:

- On Windows set to a language that writes decimals with a comma, Excel reads and writes CSV with `;` between columns.
  That covers German, French, Russian, Polish, Turkish, Spanish and Portuguese, 7 of our 10.
- Excel's plain "CSV" save also writes the old code page instead of UTF-8, unless "CSV UTF-8" is chosen.

The game's own packs have the same risk.

**Recommendation:** the game's CSV, with a third column holding the English each line was translated from
(`key,value,english`).

- The game's parser ignores a third column. Ours would use it to show English when the English has changed since,
  instead of an out-of-date translation.
- The reader accepts `;` and tab as well as `,`, with or without a BOM. A file that isn't UTF-8 gets one warning in the
  log.
- The README would point translators to LibreOffice, Google Sheets or a text editor.

### D3. Where translations live

The rule: two Workshop items that share a file path lose it when either goes (`docs/mods-window-and-workshop.md`
section 10). On Nexus, the zip unpacked last simply overwrites the other's file.

| | Bundled in our items | In translators' own items | Both |
|---|---|---|---|
| **For players** | one download, always matching the version | a second download; it can lag our version | either |
| **For us** | we ship files we can't read: a second speaker checks, or we trust the translator, with their name in the notes | nothing to ship | the API reads `Translations/<mod guid>/<lang>.csv` in any plugin folder, besides our own folder |
| **Paths** | ours only | theirs only, never inside our folder | separate |

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

| | The code is the source, `en.csv` generated | `en.csv` is the source, the code holds keys only |
|---|---|---|
| **Code** | stays readable, as now | `Text.Of("journal.ride")` everywhere |
| **Layering** | Saddle Up passes English as now, and the API makes the keys | every mod keeps its own table |
| **Drift** | a release check compares the file with the code | the file can't drift |

**Recommendation:** the code is the source, and `en.csv` is generated with a release check.

### D6. Keys

- **Settings rows:** keys from what the row already has, `<section>.<key>.label` / `.desc` / `.choice.<n>`. Players'
  files depend on a row's cfg key, so it changes only when a row moves (`MovedKey`, rare). When it does, that row's
  translation shows English until the file follows.
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
   - the folder, the file and the language: `CurrentLang` at draw time, then the part before `-` or `_`, then `en.csv`,
     then the English in the code;
   - the third column's "English changed" rule;
   - the `en.csv` generator and its release check.
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
5. **For players:** a "Translations" section in the README: the folder, the language ids, the three columns, which
   tools to use, and how to send a file.
