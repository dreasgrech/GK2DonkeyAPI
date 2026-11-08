# A mount whose saved skin isn't installed (investigation, 2026-11-05 night)

The user, in rehearsal B of the 0.16.0 release: "im selecting dragon but its keep showing the donkey". Then: "let's tackle
this bug because it does look serious and then we'll have it as part of the same major update ... public release will
wait after it", and "are we sure that the fix will be enough as it is?"

Status: **built 2026-11-05 night (sections 6 and 7); seen by hand fixed ~23:03; the whole F11 green 23:20 (2000 passed, 0 failed, 0 warnings); in the 0.16.0 release zip since 2026-11-06 01:09, rehearsal B passed with it (section 8).**

## 1. What was missing

Nothing in the dragon was missing. The Dragons Mount Pack was installed with its one skin,
`saddleup-dragon.dragon/default`; a mount always has one (a pack has exactly one `skins\default\`, and the Mount list
leaves out a mount without a skin). What matched nothing was the skin **named in the cfg**: the 0.15.0 fixture sets
every value off its default, so the dragon's Skin row held `saddleup-dragon.dragon/default-x`. The journal said so,
"Default x (not installed)".

In play, a saved skin name stops matching while its mount is installed in four ways:
1. the skin came from another pack, which was removed (a second dragon skin in a skin pack);
2. a pack update renamed or dropped one of its skins;
3. a hand-edited cfg;
4. test data.

Only 3 and 4 are possible today: no mount but the donkey has a second skin, and the donkey's case is right as it is.
The saved name is kept on purpose (it comes back with its pack).

## 2. The cause: no one rule, each place falls back to the donkey

Every place that turns a saved skin id into a skin or a mount does its own `Looks.Find(id)` and falls back to the
donkey when it gets nothing. The first look (the bug report in docs/open-items.md) named four of them. A pass over
every `Find(` of a skin id and every fallback to the donkey outside `Dev/` found these, with what each decides:

| # | Place | What it decides | Today with a missing skin |
|---|---|---|---|
| 1 | `Looks.Rig.WantedDef` (Looks.cs 2855) | the instance's mount, `MountNow`: speed, flying, the cart rule, seat, babies' size, sounds (`MountWanted`, Sounds.cs 948) | the donkey |
| 2 | the rig's skin tick (Looks.cs ~3000) | the sheet drawn | none: the game's donkey |
| 3 | `MountCompanion.MountOfSkin` (384), `MountId` | the mount's own settings: name, babies, volumes, follow switches | the donkey's |
| 4 | `MountCompanion.Current` (387, `LookOf(skin)`), `ApplyLook` (1835) | the colours put on it | a blank look (the saved id's own, which no row sets) |
| 5 | `MountCompanion.LookChanged` (411) and the `LoadedReady` handler (307) | when its look is applied again (a colour changed, a sheet built) | keyed on the saved id: the worn sheet's events missed |
| 6 | `MountCompanion.ApplyLeftLook` (1971) | a left mount's colours | none applied |
| 7 | `MountSettingsPage.PinnedBy` (1075) | the rows its mount pins (greyed) | none ("the donkey comes instead") |
| 8 | `MountSettingsPage.SkinChosen` (1427) | which skin's colour rows the journal shows, and the colours window's title | the saved id's: no rows |
| 9 | `Mounts.Spawn`'s cart rule (Mounts.cs 532) | a spawn with a cart refused for a mount that pulls none | not checked (the rig drops the cart later) |
| 10 | `Looks.TickWarm` (2627) | the chosen skin's sheet built ahead | none built |
| 11 | `Persistence` record label (501) | the log's "dragon" or "donkey" | "donkey" |

Right as they are:
- `FamilyMountOf` (the left mounts' family): the id's own mount part, already the dragon.
- The Skin row's label "X (not installed)" (MountSettingsPage.cs 706): true, and kept. (Changed 2026-11-07, section 9:
  the name alone, dimmed, the stats card saying "Skin: Not installed".)
- The places that take a skin of the catalog (icons, the journal's list and stats, texts, the default skins).

Asked before about the case where the mount itself is gone (the Dragons pack removed: "Dragon (not installed)", the
donkey comes, docs/mounts-as-mods.md): that one is meant, and stays.

The same holds for a skin that is there but **set aside** (a game-source skin whose frames the game lacks): every place
above treats it as missing. Today only mounts' default skins are game-source (Larry's), so the donkey is the only
fallback there. A rule that falls back to the mount's default covers a set-aside second skin too, at no cost.

## 3. The fix: one rule, used in all eleven

`Looks.Worn(id)`: the skin a mount wears for a saved skin id.
- the id's own skin, when it can be worn (installed, not set aside, its mount installed);
- else, when its mount is installed (the id's mount part, `<mount>/<skin>`, or the skin's own mount) and that mount's
  own default look can be worn: that default;
- else none: the donkey's own art. That covers the donkey's ids, a mount that isn't installed, and a mount whose only
  look is set aside.

Each place in the table asks it instead of `Find`. The saved id stays everywhere it is stored: the cfg's Skin row, the
companion's `Skin`, each instance's `Skin` setting and the side file. Only what is drawn, what the instance is, and
whose colours and rows apply follow `Worn`. The log says it as an Info line each time a mount or one of its babies takes the stand-in on (as the old "the donkey is drawn" line was said): "no skin 'X' is installed (its mod or pack
isn't loaded); the <mount>'s own look is worn (the choice is kept and comes back with it)".

## 4. Tests (F11, written with the fix)

- **A test mount with a missing skin id:** it is that mount (`MountNow`, the companion's `MountId`), it wears its
  default sheet, and its baby is that mount's. The saved id stays on the companion and the instance, and the side
  file's text keeps it.
- **A missing donkey skin** (`donkey/selftest.nope`): the plain donkey, as before.
- **A missing mount** (`selftest.nomount/x`): the donkey, as before.
- **Integration, Saddle Up's page:** the dragon's Skin row set to a missing id. `SkinChosen` gives the default, its
  colour rows are shown, and the row is put back.

**How the eleven are wired** (from the review of this plan):
- **Some places need a def, others an id.** Beside `Worn(id)` (a def), `WornId(id)` (`Worn(id)?.Id ?? id`) serves
  `LookOf`, `SameSkin`, `LookSetOf` and `SkinChosen`.
- **The rig still rechecks first.** For a set-aside skin it runs `RecheckGameSource` on the wanted skin, so a
  game-source skin that comes back is offered again. Only when the wanted skin stays unwearable is the substitute
  worn. `WantedDef` works the same way: the wanted skin first, then the substitute.
- **Colouring writes the default's look.** A player who colours the mount while it wears the substitute colours what
  they see, so the rows shown (`SkinChosen`) are the default skin's and write its look. The missing skin's own look is
  untouched, for when its pack returns.

**Checked:** no self-test or integration test asserts the donkey for a mount's missing skin. The ones about
"(not installed)" are the Skin list's label (kept) and a missing *mount* (kept: `SelfTest.cs` 3438, `Order.cs`).
`Looks.MountLineFor` (1771) has no caller left.

## 5. The release

The fix is the API's alone; the two pack zips don't carry the API, so only `GK2-Saddle-Up-0.16.0.zip` changes. It was
never uploaded, but the user's commit `00605d4` holds it, and `release-versions.py published GK2DonkeyAPI` names it.
So `build-dist.sh --rebuild GK2DonkeyAPI` refuses ("it is public, raise its version"), and a plain `release.sh` would
put the old, unfixed DLL back. Two ways:
- **Keep 0.16.0:** the user commits the removal of that unuploaded zip, the tool then sees it as never published, and
  `build-dist.sh --rebuild GK2DonkeyAPI` and `make-release-zip.py --only saddleup` write it again.
- **Raise to 0.16.1 / API 0.44.1:** no git step, but a never-shipped 0.16.0 stays in `release/`, and the notes,
  headings and fixture are renamed.

Either way, then the scans, the Saddle Up Workshop folder restaged, and rehearsals A and B (B is the one that met it).

## 6. Built (2026-11-05 night)

The user: "Build, keep 0.16.0".

**`Looks.StandIn(id)`** (Looks.cs, by `DefaultSkinOf`) gives one of two answers:
- **The id itself** when `Find` knows it, or when it is the donkey's, has no `<mount>/` part, or its mount isn't
  installed (or is the donkey).
- **Else that mount's `DefaultSkinOf`**, when `Find` knows it. Only an id that names no skin ever changes, so
  set-aside skins and the rig's `RecheckGameSource` are untouched.

**Where it is used** (the eleven of section 2):
- the rig: `WantedDef`, the skin tick (an Info line each time an instance takes it on: "the <mount>'s own look '<id>' is worn instead (the choice
  is kept and comes back with it)"), and `TickWarm`;
- the companion:
  - `MountOfSkin`, and `Current` = `LookOf(Worn(skin))`;
  - `LookSetOf(l, skinId)` (the parts and tones by the worn look, `SkinKeys` by the chosen id, so each instance's
    `Skin` keeps the choice), and `LookOnFamily`;
  - `ApplyLeftLook` (chosen and worn kept apart), the paper's presets in `ApplyLook`;
  - `LookChanged` and the `LoadedReady` handler;
- `MountSettingsPage.PinnedBy` and `SkinChosen` (the Skin row's value itself unchanged);
- `Mounts.Spawn`'s cart rule;
- `Persistence`'s record label.

**Tests:**
- the self-test "a mount whose saved skin isn't installed wears its own look ..." (`LeaveMountsTests.MissingSkin`);
- the integration test "skins: Saddle Up's dragon with a skin not installed ..." (`SaddleUpMissingSkinRows`).

**Built and deployed** 22:13, 0 warnings. NoLinq, Cecil, ApiSurface (no public change) and layering all pass; two
comments named the dragon at first and were reworded.

**Done since** (section 8): the F11, the user's commit removing the unuploaded zip, the rebuild, the scans, and
rehearsals A and B.

## 7. The hand test found a twelfth place (2026-11-05, ~23:00)

The F11 of 22:50 was green. The user then picked mounts in the journal with the 0.15.0 fixture's made-up skins staged
(dev build): "when i select another mount in the journal, the current preview gets stuck". The screenshot shows Larry
picked and the dragon still drawn.

- **The log:** "Looks: skin 'saddleup.gerry/default' loaded: 44 pictures ... 301 ms ...", then "unloaded (no donkey
  wears it)", over and over.
- **The cause** (`Looks.cs` 2997): while a sheet is still building, a refresh checks it is still the skin wanted with
  `Find(stillWant)`, the saved id. For a stand-in that finds nothing, so the half-built sheet was let go as "the choice
  changed", built again, let go again, and the picture kept the mount before.
- **Why F11 missed it:** the test mount's stand-in is one picture and builds at once, so no sheet was ever pending.
  Larry's takes about 44 frames, and the panel's picture refreshes in between.
- **Why the audit missed it:** the search was for `Find(` with a few names of its argument, and this one is called
  `stillWant`.
- **The fix:** `Find(StandIn(stillWant))`.
- **A second, complete search:** every read of an instance's `Skin` value outside `Dev/` (`Skin.Value`,
  `Tuning.Get<string>("Skin")`). There are three in the rig (`WantedDef`, the pending check, the tick) and the
  companion's `WornSkin`, all through the stand-in now. Every remaining `Find(` of a skin id in shipped code takes an id
  that is already the worn one or a registered one.
- **The regression test:** the integration scenario "skins: a slow stand-in (Larry with a skin not installed) is worn
  though its rig is refreshed every frame while it builds" (`MissingSkinSlowBuild`). Without the fix the sheet is let go
  at every refresh and never worn.

**Seen by hand 2026-11-05 ~23:03** (dev build, the 0.15.0 fixture's made-up dragon and Larry skins staged; the user:
"ok seems fixed now"):
- the journal's picture follows each pick;
- the dragon is ridden as the dragon ("Mounted ... dragon"; it was "donkey" before);
- "unloaded (no donkey wears it)" 4 times, the ordinary switching, against 348 in the session before;
- no warning or error.

The user's own Saddle Up cfg was put back afterwards and the saves compared: identical.

## 8. In the release (2026-11-06)

- **The first 0.16.0 zip** (2026-11-05 19:42, never uploaded: Steam's item still held 0.15.0's bytes) held the API from
  before the fix. The user's yes, then their commit `e16dd41` removed it.
- **`build-dist.sh --rebuild GK2DonkeyAPI`** (00:59): only the API new (3,245,056 bytes, still 0.44.0, its manifest of
  902 game members identical to the installed game); Saddle Up and the packs kept their committed bytes. 27 of 27
  release scans green; the zip written again (01:09, 1,191,828 bytes).
- **Rehearsal A** (01:29) on fresh cfgs: no warning or error; the same cfg keys as the 0.16.0 fixture.
- **Rehearsal B** (01:41) over the 0.15.0 fixture's Saddle Up cfg: the dragon picked with its saved skin `default-x`
  wore its own look ("the dragon's own look 'saddleup-dragon.dragon/default' is worn instead") and the ride went on
  as the dragon; Larry the same; the choice kept in the cfg; 188 of 189 values kept, 0 missing; no warning or error.

## 9. The Skin row's label didn't fit (2026-11-07)

The F11 translations scenario, run with the player's dragon still set to a test skin that had been deleted
(`saddleup-dragon.dragon/saddleup-dragon.armored`, the Armored Dragon's loose copy before it went into the Dragons
pack), failed three times on one text: the journal's Skin row "Armored (not installed)". In Russian ("Armored (не
установлено)") and Polish ("Armored (nie zainstalowano)") it didn't fit at its smallest size (0.99 and 0.95 of the
room), and in the test language not at all. A player who removes a pack while a mount wears one of its skins sees it;
a longer name ("Royal Parade") would be cut in more languages.

Three ways were put to the user: shorter Russian and Polish words (fixes two languages for short names only), the
name alone with the state said elsewhere, or leaving it. The user picked the second.

**Built:**
- **The Skin row** (`Texts.Choice`): a skin not installed, or set aside, shows by its name alone; `Texts.Dimmed`
  marks it, and the dropdown draws it at half strength, closed and in its open list (`RowControls`), as the journal
  greys a mount not installed. The row only ever holds a name, which fits as every installed skin's does.
- **Why it is dimmed** (`MountsPanel.BuildRight`): the stats card's last line, in place of the skins' count, says
  "Skin: Not installed" while the shown mount's Skin row holds an id none of its skins has
  (`MountSettingsPage.SkinNotInstalled`: the dropdown's own rule). Both words were already translated in all 11
  languages (`row.skin`, `journal.not_installed`): nothing new to translate. The journal now also rebuilds its right
  side when that changes (a skin not installed stands in as the default, so picking the default from it changed
  nothing the journal watched).
- **Kept:** the English list labels `FillList` writes ("Gone skin (not installed)", its self-test), and a missing
  *mount*'s "(not installed)" in the mount list and header (sections 2 and 4: meant, and the test didn't flag it).

**Tests:** the translations scenario now makes the state instead of relying on a player's cfg: in every language the
layout pass gives each mount's Skin row a made-up skin no pack provides (`<mount>/selftest-missing-skin`, "Selftest
missing skin") and measures the journal again, and the test-language pass does the same for the chosen mount and checks
the row shows the name alone and the stats card "Skin: Not installed"; the value is put back after each. (The first run failed its own check: the journal watches only the *mount* by itself, and a Skin row value
set from the test left it showing the state before; the tests now refresh it as a pick in the Skin row does. The
layout pass had passed without measuring the missing case.) The settings
self-test checks the shown label ("Gone skin", dimmed; an installed choice not dimmed).
