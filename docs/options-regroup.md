# The options window's tabs and rows (review, 2026-11-05)

The user, 2026-11-05: "we have a lot of tabs on the side when a lot of the options can be consolidated into single tabs
instead of split, like mount and mount behaviour since these are from the time when mount customization was in there."

Status: **built 2026-11-05 evening** (section 5): the user took layout A (five tabs) and items 1, 2 and 3 of section 3.
Saddle Up's page only (`src/SaddleUp/Plugin.cs`); the API and the packs add no page of their own. F11 and a look at the
window in game are open.

## 1. What the window shows today

Seven tabs (the cfg has eight sections: Colours has only journal rows, so it is no tab).

| Tab | Rows | What |
|---|---|---|
| Keys | 10 | Mount Journal L, Ride your mount H, Drive the cart G, Ring the bell B, Call out V, Send your mount away (none), Jump Space, Fly Space, Climb (hold) Left Shift, Descend (hold) Left Ctrl |
| Mount | 4 | Mounts (button: opens the journal), Keep your mount with the save, Leave your mounts in the world, Send your other mounts home |
| Mount Behaviour | 4 | Say why you can't get on, Shuffles while standing, Allow mounting indoors, Allow mounting with the cart indoors |
| Cart | 4 | Zombies ride on the cart, A storage chest rides on the cart, Carry the corpse into the morgue, Donkey leaves corpses at the crossroads |
| Sound | 2 | Mount calls, Standing mounts and babies sound |
| Safe spot | 3 | Bring me back if I fall out of the world, Back to the last safe spot (key), Back to the last safe spot (button) |
| Freeplay | 2 or 3 | All mounts carry max cargo, All mounts have the same speed, Speed of every mount (shown with the row above on) |

The user's point holds: Mount is four rows because everything else it had (the picture, Mount, Skin, Name, the babies,
the volumes) went into the Mount Journal between 2026-10-20 and 10-26. Those rows are still in the Mount section, as
journal rows (`look: true`), so the window doesn't show them.

## 2. The tabs: seven to five (recommended)

**Keys | Mount | Cart | Safe spot | Freeplay**

Mount Behaviour and Sound go into Mount, under two headings:

| Mount tab | |
|---|---|
| Mount Journal | button (today labelled "Mounts") |
| Keep your mount with the save | on |
| Leave your mounts in the world | off |
| Send your other mounts home | button |
| ***Mount Behaviour*** (heading) | |
| Allow mounting indoors | off |
| Allow mounting with the cart indoors | off (greyed until the row above is on) |
| Say why you can't get on | on |
| Shuffles while standing | on |
| ***Sound*** (heading) | |
| Mount calls | on |
| Standing mounts and babies sound | on |

**It fits without a scrollbar, just.**
- The rows area is 445 px high (`OptionsWindow`: H 600, rows from y 87 to 532).
- A row takes 34 px (30 + 4) and a heading 38 px, plus 4 px at the top and bottom.
- 10 rows and 2 headings make 424 px. A third heading (462 px) would scroll, so Mount can't take Safe spot as well.

**Safe spot and Freeplay stay their own tabs.** Both were the user's calls on 2026-10-27:
- Safe spot "a section of its own at the end": its key had been in Keys and the rest in Behaviour;
- Freeplay the last one, "for players who dont want the gameplay stuff".

Safe spot is about the keeper, not the mount: it works on foot too. So it doesn't belong under Mount.

**Cart stays.** Its four rows are the cart's work. Cart and Mount together would be 14 rows and 3 headings, which
scrolls.

Other layouts considered:
- **B, six tabs:** only Mount Behaviour goes into Mount, and Sound stays a tab of two rows. The smallest change, but it
  leaves the thinnest tab.
- **C, four tabs:** Safe spot and Freeplay together in one tab. That needs a new tab name in 11 languages, and it makes
  the grab bag the user took apart on 2026-10-19 ("Behaviour a grab bag"). Not recommended.

## 3. The rows and their values (a yes or no each)

1. **The journal button's label: "Mounts" becomes "Mount Journal".**
   - The key row above it is "Mount Journal", with the same description word for word; the window has been called
     Mount Journal since 0.14.0.
   - No new words: every language has "Mount Journal" already (`MountsKey.label`).
   - Recommended.
2. **"Send your other mounts home": shown only while "Leave your mounts in the world" is on.**
   - With the option off, no mount stays left, so the button is greyed for good. Freeplay's speed row already shows
     only with its switch on.
   - The cost: the page's `Button` has no `visible:` yet, so this is an API addition. The release's NeedsApi rises to
     this API anyway, for LeaveMountsInWorld.
   - Optional.
3. **The Keys order: Send your mount away moves up, after Drive the cart.**
   - That makes call, call with the cart, send away; then the rest as they were (Ring the bell, Call out, Jump, Fly,
     Climb, Descend).
   - No texts change, and players' keys stay as they are.
   - Optional.
4. **"Donkey leaves corpses at the crossroads" stays under Cart.**
   - It is the game's delivery donkey, not yours, but what it changes is the cart's work: you haul the bodies in.
   - "Donkey" could be read as your own. Rewording it would cost new text in 11 languages, so keep it unless it
     confuses someone.
5. **The defaults:** none I would flip.
   - Leave your mounts in the world: off (the user's, 2026-11-05).
   - Both indoors rows: off, so the ride ends at the door as before (2026-10-21).
   - Corpses at the crossroads: off (the game's own way).
   - Everything else: on. Freeplay: off.
6. **Wording**, optional, each one new text in 11 languages:
   - "Shuffles while standing" has no subject, though it reads well under the heading "Mount Behaviour";
   - "Standing mounts and babies sound" is clumsy.
   - My advice: leave both.
7. **The safe spot's key stays in Safe spot** (the user's call, 2026-10-27), so it's the one key outside Keys. Moving
   it to Keys leaves Safe spot with two rows. Mentioned only.

## 4. What layout A costs

**Plugin.cs (Saddle Up only):**
- The six rows get section "Mount", with a `MovedKey` before each one for every old place it had:
  - `ShowMessages`, `IdleShuffle`, `MountIndoors`, `CartIndoors`: from Mount Behaviour (0.12 to 0.15) and from
    Behaviour (up to 0.11);
  - `Brays`, `StandingSounds`: from Sound.
- `EnableWhen` (CartIndoors) and `TuningKey` (Brays) name the new section.
- The two `Heading`s go in.
- None of the six keys is used in Mount already.

**Players' files:**
- The values carry over, and the old lines leave the file (`DropOldCopies`).
- The cfg sections renumber by themselves: 1 Keys, 2 Mount, 3 Colours, 4 Cart, 5 Safe spot, 6 Freeplay.
- Checked with `tools/page-carryover.py` over the five fixtures (0.9.1, 0.10.0, 0.12.0, 0.13.0, 0.15.0), then the
  upgrade self-test.

**The journal: unchanged** (read in the code).
- The journal takes only journal rows. A row of the Mount section goes into the journal's one group whatever heading
  stands before it (`MountsPanel.cs` 660-666).
- A heading shows in the window only when the row after it is a window row (`InOptions`).
- A mount or skin added late is inserted after its own builder's rows, not at the end, so headings don't catch it.

**Translations: no new words.**
- Row keys don't carry their section, so the moved rows keep their lines.
- The headings reuse the values of the tabs they replace: `heading.mount_behaviour.label` takes
  `section.mount_behaviour`'s value, and `heading.sound.label` takes `section.sound`'s, in each of the 11 files. Those
  two section lines go.
- The button's `panel.mounts.*` lines become `panel.mount_journal.*`, with `MountsKey.label`'s value.
- The note column says the new section.
- ja, ko and zh_cn get no new characters.

**The Defaults button:**
- Mount's now sets back eight settings instead of two (the buttons have no value).
- Setting "Leave your mounts in the world" back to off sends the mounts left home, as it already does today.

**Tests:**
- `tools/GK2DonkeyAPI.IntegrationTests/Sounds.cs:111` looks for StandingSounds in "Sound": it changes to "Mount".
- Babies, Safety and Freeplay still pass as they are (Mount, Safe spot, Freeplay last).
- New checks: the two headings are in the window's Mount tab, and the tab doesn't scroll.
- These are the first headings drawn in the options window, so they also get a screenshot.

**Docs:**
- the README's settings table (section numbers and rows);
- `docs/options-window.md`;
- the CHANGELOG (a short player line: the options have five tabs);
- `media/options-window.png`: already old (its first key reads "Mounts panel"), so a new shot for the Nexus and
  Workshop texts.

**Versions:** Saddle Up changes; the API only if item 2 is taken.

## 5. Built (2026-11-05 evening)

The user's choices: layout A, and items 1, 2 and 3 of section 3 (the journal button's label, the Send-home button
shown with its option on, Send your mount away after Drive the cart).

**The API:**
- `MountSettingsPage.Button` takes `visible:`.
- The old signature is kept, with every parameter required.
- The options window has two test hooks for the rows' height and the column's room.
- The Smoke test says each section's height against its room.

**Saddle Up** (`src/SaddleUp/Plugin.cs`):
- After the Mount section's journal rows, two headings, "Mount Behaviour" and "Sound", each with its rows.
- A `MovedKey` per old place:
  - MountIndoors, CartIndoors, ShowMessages and IdleShuffle from Mount Behaviour and Behaviour;
  - Brays and StandingSounds from Sound.
- `EnableWhen` (CartIndoors) and `TuningKey` (Brays) in Mount.
- The `PanelButton` is labelled "Mount Journal".
- "Send your other mounts home" takes `visible: () => leave.Value`, inside `LeaveMountsRow`. Its
  `MissingMethodException` catch also covers an API without `visible`.
- SendAwayKey follows CartKey.

**The language files** (en.csv and the 10 translations):
- `section.mount_behaviour` and `section.sound` go;
- `heading.mount_behaviour.label` and `heading.sound.label` take their values;
- `panel.mounts.*` become `panel.mount_journal.*`, the label with the file's own `MountsKey.label`;
- the moved rows' notes say Mount;
- the lines are in the dump's order;
- 149 lines each, no new words.

**Checks:**
- `tools/page-carryover.py`: 0 lost over the five fixtures. Over the live dev cfg, nothing new is lost: what it lists
  are the dev builds' named colour keys, which it doesn't model, and two rows that were never released where they sat.
- NoLinqScan, ApiSurface (after `--update`), CecilScan, TranslationScan, DefaultsScan, FrameworkScan, SkinsScan,
  layering and DepManifest: all pass.
- `l10n-sync --check`: only the 4 renamed keys, until the next dev run's dump.

**New in F11:**
- the integration scenario "options: Saddle Up's five tabs": the tabs in order, the Mount tab's rows and headings in
  order, the journal button's label, the keys' order, and the Mount tab drawn without a scrollbar with every row shown;
- the Leave row's test: the button shows only with the row on;
- the StandingSounds test looks in Mount.

**F11 (17:55): 1957 passed, 0 failed, 0 warnings.**
- The new scenario passed, the Mount tab measured 424 of 445 px drawn, and the Leave button showed and hid with its row.
- Every released cfg landed on today's page.
- The first start carried the six moved keys and three renumbered sections over (INFO lines only).
- `l10n-sync --check`: "same" for every template.

**Open:**
- a look at the window in game (the first headings drawn in it);
- `media/options-window.png` for the Nexus and Workshop texts;
- at the version bump (the user's): Saddle Up's NeedsApi rises to this API (already due for LeaveMountsInWorld);
- at the release: a fixture of this cfg (docs/release-checklist.md 3b). Six keys changed section and the sections after
  Colours renumbered, so 0.14.0's "no new fixture, the same keys" doesn't hold.
