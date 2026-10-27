# Mount id collisions: two mods, one id

**2026-10-27:** the donkey's skins are the Donkey Skins pack's, `donkey/saddleup-donkey-skins.hellfire` and
`.royal-parade`, and Hellfire's effect `saddleup-donkey-skins.hellfire` (the pack's prefix, as every pack's ids). The
API keeps no table of any mod's old ids: `Looks.Find` no longer takes `hellfire` or `donkey/hellfire`, and an old
mount id finds nothing; a short name (the name after a mount's prefix, when only one mount has it) still finds it.

**2026-10-26 (API 0.40.0, docs/multi-mod-loading.md):** effect ids follow the same `<mod>.<name>` and version rule
(`saddleup.hellfire`); a code skin that loses no longer throws; a newer pack of a mount takes the old pack's skins out
with it; a mount.cfg whose id isn't its folder's is refused; ids are ASCII, at most 64. The author stamp (section 4's
add-on) is still not built.

## Decided (2026-10-24)

The user decided, and the code follows it (the rest of this page is the investigation that led there):
- **Every mount id carries its mod's prefix**, `<mod>.<name>`, ours too: `saddleup.dragon`, `saddleup.rat`,
  `saddleup.snail`, `saddleup.worm`, `saddleup.gerry`; another author's `acme.dragon`. Only the game's `donkey` is
  plain. A code or pack mount without a dot is refused (`Catalog.CheckId`). The folder name is the id. This goes
  further than option B below: there is no bare first-party space.
- **Skins are named within their mount**: full name `<mount id>/<skin>` (`saddleup.dragon/default`,
  `donkey/saddleup.hellfire`). The skin folder's name is its name; skin.cfg has no `id =`. The cfg rows and the side
  file hold full names.
- **A skin added to a mount another mod owns carries its mod's prefix, `<mod>.<name>`** (later the same day: the
  author convention is now enforced, `Looks.Register` / `Looks.AddsToOthers`): every skin in a mount folder without
  `mount.cfg` (`saddleup.dragon\skins\acme.frost\`), every donkey skin (the donkey is the game's: Saddle Up's are
  `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`, `Looks.Find` still takes `hellfire` and
  `donkey/hellfire`), a code skin on a mount another owner registered, every recipe (`donkey/example.sepia`). A pack's
  without the prefix is left out with a warning, a code one throws. Our own mounts' skins are free: a skin on the
  mount its own pack or mod registered takes any name (`saddleup.dragon/default`).
- **One default look per mount: `skins\default\`**. `[Mount] default_skin` is gone; a pack mount without
  `skins\default\skin.cfg` is refused; a folder adding skins to another mod's mount may not bring a `default`. The
  donkey has none: its own look is the game's art.
- **One version rule for two mounts of one id, no throw** (later still, the user: "one version rule, no throw";
  `Catalog.Register`, `Catalog.Declare`, `Catalog.Outranks`): code against code, code against a pack and pack against
  pack alike, the higher version wins, at a tie code or a DLL beats a loose folder, else the first stays.
  `Mounts.Catalog.Register` no longer throws for another owner's id: the loser is left out with one warning and its
  handle does nothing; a newer code mount replaces the registered one (`Catalog.Retire`: the old one's pack sound
  files or code clips go). A newer pack `mount.cfg` replaces a code mount; an older one is refused with its skins.
  A code mount's clips are banked under its mod (`Catalog.SoundBankOwner`, `"mount/<owner>/<id>"`; bank ids
  `gk2.mount.<owner>/<id>.<key>`), a code skin's the same (`"skin/<owner>/<skin id>"`), so two mods' mounts of one
  id never share an entry. This settles the open item "a code mount against a loose pack's mount of the same id"
  (section 2.3 below is the state before). With it, an asymmetry is fixed: an **embedded** pack whose `mount.cfg` is
  refused now drops its skins as a loose folder's always did (`Looks.RegisterEmbedded`; before, its skins registered
  onto the other pack's mount). The self-test "ids: a code mount meets the version rule" checks it.

Question (the user, 2026-10-24): two different mods both define a mount `dragon`. What should happen?

Read-only investigation, 2026-10-24. Line numbers are the live files of that day. Skins scoped per mount
(`dragon/default`, `dragon/hellfire`) are decided but not built; this report takes them as coming.

**Short answer.** A refused mount at start is the harmless case. The harmful case is a **silent swap**: everything
we keep names a bare id and never the mod that made it. Remove mod A, install mod B with its own `dragon`, and every
saved dragon and the player's Mount row become B's creature without a word. Recommended: **namespaced mount ids**
(`acme.dragon`), enforced for every mod but ours, decided and built **together with** the scoped skins, so the saved
data changes shape once. Section 4.

## 1. Where a mount id lives

| Place | What is stored | Owner in it? | Code |
|---|---|---|---|
| The registry | `Catalog.defs`, keyed by id, case-insensitive | entry has `Owner`, key does not | Catalog.cs:51 |
| Id check | letters, digits, `.`, `-`, `_` (dots already allowed) | - | Catalog.cs:141-148 |
| Saddle Up's cfg | `Mount = <mount id>`, `Skin_<mount id> = <skin id>` | no | Api/MountSettingsPage.cs:406, 453 |
| Saddle Up's cfg | per-mount colour rows `Coat_<mount id>` ... (the donkey's have no suffix) | no | Api/MountSettingsPage.cs:965-972 |
| The side file | per instance a `set` line `[Skin, owner, <skin id>]`; the mount is derived from the skin | the line's owner is who *set* the key, not who made the skin | Persistence.cs:407-420 |
| Skin to mount | `SkinDef.MountId`; a pack skin's is its folder's | no | Looks.cs:180, 580, 720 |
| Wearing a saved skin | `FindWanted(id)`: by id, then the rename table; no owner check | no | Looks.cs:281, 2108, 2241 |
| Ability owner | `"<guid>.ability.<mount id>"` | yes (GUID) | Api/MountCatalog.cs:98; Persistence.cs:782-786 |
| Ability values in the save | never saved (session only) | - | Persistence.cs:279 |
| Pack mount sounds | bank `gk2.<owner>.mount-<id>.<key>`; cache `BepInEx\cache\GK2DonkeyAPI\sounds\<owner>\mount-<id>` | yes | MountPacks.cs:36; SoundBank.cs:306-308, 367 |
| Code mount sounds | bank `gk2.mount.<id>.<key>`, bank owner `mount/<id>` | **no** | Sounds.cs:818-820, 843; SoundBank.cs:387-393; Catalog.cs:234 |
| Mounts panel | lists `Catalog.All`, picks by id; a missing one says "Not installed ... Your choice is kept" | no | MountsPanel.cs:455-465 |
| Tuning panel save | writes the mount's rows into its `mount.cfg` | via the pack | MountPacks.cs:236ff |

So the only persisted ids are the **skin id** (side file) and the **mount id** (Saddle Up's cfg keys and values). None
records which mod they came from.

## 2. What happens today

Order matters: the loose-folder scan runs in the API's Awake (Looks.cs:125), before every mod's Awake. Embedded
packs and code mounts register in their mod's Awake, in BepInEx's dependency order.

### 2.1 Code vs code (`Mounts.Catalog.Register`)
- Same owner: replaces, its instances re-apply (Catalog.cs:155-158).
- Another owner: `ArgumentException` "already registered by ..." (Catalog.cs:155). `MountCatalog.Register` does not
  catch it (Api/MountCatalog.cs:104-105), so it **lands in the second mod's Awake**. Unless that mod wraps the call,
  the rest of its Awake does not run.
- First come wins. Which is first is load order.

### 2.2 Pack vs pack (`Catalog.Declare`, loose or embedded)
- Same owner, same facts: kept (Catalog.cs:176).
- Otherwise the version rule (`Outranks`, Catalog.cs:195-200): higher `[Mount] version` wins; a tie goes to a DLL over
  a loose folder; else the first stays.
- Winner: its facts replace the old entry **in place** (Catalog.cs:177-184, `Redeclare` 260-288). The loser's skins
  keep pointing at the same object, so they now run on the winner's facts (cart, speed, tuning, sounds). A warning
  names the loser.
- Loser refused: `ArgumentException` (Catalog.cs:186), caught and logged (MountPacks.cs:209-213).
  - **Loose folder**: `RegisterMountFolder` returns 0 when the declare fails (MountPacks.cs:164-165). Its skins are
    **dropped**.
  - **Embedded**: `RegisterEmbedded` declares (Looks.cs:385-391), ignores the result, then registers every
    `<mount>/skins/<skin>` against whatever mount has that id (`Catalog.Find(parts[0])`, Looks.cs:458-459). Its skins
    **join the winner's mount**.
  - The two paths disagree. Not a bug report yet, but worth one rule.
- One owner per loose `gk2mounts` parent folder (`<api>.packs.<folder>`, Looks.cs:439-455), sorted by name, so two
  loose folders meet the version rule instead of replacing each other silently.

### 2.3 Code vs pack
- Pack first, then code: `Register` throws for another owner (Catalog.cs:155). Since loose packs always scan first,
  **a loose `dragon` folder blocks every code `dragon`**.
- Code first, then pack: `Declare` throws "registered in code ... add skins to it" (Catalog.cs:175). A code mount is
  never replaced.
- Open item since 2026-10-20 (docs/open-items.md:337-341): no version rule here.

### 2.4 Skin ids (today, before scoping)
- Global. Another owner: version rule (`Looks.Outranks`, Looks.cs:337-342); the loser's wearers take the winner at once
  (Looks.cs:296-305, 320).
- A third-party skin id without a dot gets one Info line asking for `<author>.<name>` (Looks.cs:316-319). Our own
  owners (`dreas.gk2.*`, except loose packs) are exempt (`FirstParty`, Looks.cs:331). Mount ids get no such line:
  `Catalog` has none.
- A folder without `mount.cfg` joins whatever mount has that id (MountPacks.cs:146-150 with `mountId = dirName`).

### 2.5 The mod is removed
- Code mount withdrawn (`Unregister`, Catalog.cs:229-238): its skins fall back to the donkey mount ("nobody
  registered; it is a donkey for now", Looks.cs:195-199).
- Pack mount with no skin left is withdrawn (`NoSkinLeft`, MountPacks.cs:218-224).
- Saved instances: `Skin = <id>` stays in the side file; they load as donkeys; the label says "donkey"
  (Persistence.cs:407-420). The cfg keeps `Mount = dragon`; the panel says "Not installed ... Your choice is kept"
  (MountsPanel.cs:462). The choice returns with the mod (docs/mounts-as-mods.md:100-109).
- This is good: dormant, not lost.

### 2.6 Another mod's `dragon` is loaded instead
- The cfg row `Mount = dragon` resolves to B's dragon. `Coat_dragon` etc. apply B's colour regions to A's choices.
- Saved instances keep their skin id. Today A's skin was `red-dragon`: if B has no skin of that id, they stay
  donkeys (safe by luck). If B also ships `red-dragon`, or once skins are scoped (`dragon/default`, which every
  mount will have), **every saved instance silently wears B's creature**.
- Nothing logs it: the ids match, so it looks like the mod came back.

## 3. How other ecosystems do it

Web search returned thin, mostly forum sources. Points marked (k) are from my own knowledge, not verified today.

- **Minecraft (Forge/NeoForge/Fabric).** Every id is `namespace:path` (`minecraft:stone`, `create:cogwheel`). The
  namespace is the mod id, so two mods never share an id; a mod can only collide with itself (k). The save stores the
  full name. On load, Forge compares the save's registry map with the mods present and warns that missing entries
  will be deleted on the next save, and makes a backup ([gameserverkings](https://www.gameserverkings.com/knowledge-base/minecraft/error-missing-mods-in-world/),
  [Forge forum](https://forums.minecraftforge.net/topic/63883-fatally-missing-registry-entries)). Mods remap renamed
  ids through a missing-mappings event, the analogue of our dormant records and rename table (k).
- **Factorio.** Prototype names are unique per type across all mods. A later `data:extend` with an existing name
  replaces the earlier one silently; load order (dependencies, then name) decides (k). Authors are told to prefix
  their names with the mod's name ([Factorio forum](https://forums.factorio.com/viewtopic.php?p=305109)). Removed
  prototypes are stripped from a save on load with a notice; `migrations/` files rename them (k).
- **RimWorld.** `defName` is global per def type. Duplicates in one mod are skipped with an error; across mods the
  later-loaded def can override; the community uses a conflict checker, and defNames are not renamed because they are
  baked into saves ([RimWorld wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/Compatibility),
  [Steam discussion](https://steamcommunity.com/app/294100/discussions/0/350540780276216686)). The wiki says: patch
  with XPath rather than redefine whole defs.
- **Stardew Valley 1.6 / Content Patcher.** Ids became strings; the official rule is to prefix with the mod's unique
  id, `Example.PufferchickMod_PufferchickPlushy`; Content Patcher fills `{{ModId}}_` in for you; ids are limited to
  letters, digits, `_` and `.`; old ids migrate through a trigger action that rewrites the save
  ([Stardew wiki: migrate to 1.6](https://stardewvalleywiki.com/Modding:Migrate_to_Stardew_Valley_1.6),
  [Cross compatibility](https://stardewmodding.wiki.gg/wiki/Cross_Compatibility)). JSON Assets (before 1.6) gave
  numeric ids per save and kept a name map in the save to re-fix them (k).
- **Skyrim / Bethesda.** A FormID's top byte is the plugin's load-order slot (ESL: `FE` plus a sub-index), so the
  plugin is part of every id. Two plugins cannot mint the same record; to change another's record you override it on
  purpose by its FormID, and the last loaded wins. Removing a plugin leaves orphaned references in the save (k).
- **BepInEx.** Plugins are identified by GUID (reverse-domain by convention). With two copies of one GUID, the
  chainloader keeps the higher version and skips the other with a log line (k; matches our version rule for the
  *same* author's two copies).

The pattern: **identity = (who, name)**. A bare global name works only with a strong prefix convention, and then
same-name collisions are treated as the same thing in two versions (Stardew, BepInEx) or as a deliberate override
(Skyrim, Factorio). Saves keep the full name; a missing one is kept or dropped with a notice, never quietly re-bound
to someone else's content.

## 4. Options

All three keep: the version rule for two copies of the *same* mount (a loose copy and the DLL, an old and a new
release), the dormant behaviour when a mod is gone, and dots as the namespace separator (Windows folders can't hold
`:`; dots already pass `CheckId`).

### A. Keep global ids, add the convention for mounts (today plus one Info line)
- Mount ids stay bare; `Catalog` logs the same "no author prefix" Info as skins.
- Skins-only pack: `gk2mounts\dragon\skins\frost\` (unchanged).
- Saves: the silent swap of 2.6 stays. With scoped skins it gets more likely (`dragon/default` exists for every
  dragon).
- Ergonomics: the simplest names. The collision cost lands on the player, and late.

### B. Namespaced ids, enforced for others (recommended)
- A mount id from any owner but ours must be `<author>.<name>` (at least one dot): `acme.dragon`. A bare id from
  another owner is **refused** with one clear warning naming the fix. Our own (`FirstParty`) keep the bare ids
  (`dragon`, `rat`): the bare space is ours, like `minecraft:`.
- The id is still written by the author, in `mount.cfg` (or as the folder name) and in `MountDef.Id`. No hidden
  derivation, so a mount that moves from a loose pack into a DLL keeps its id and its saves.
- Two mods can only meet on one id if one copies the other's prefix. Then they really are two copies of one mount,
  and the version rule is the right answer.
- **Skin local ids too.** Scoped skins end clashes between mounts, not inside one: two authors adding `frost` to our
  bare `dragon` both make `dragon/frost`. So the same rule applies to a skin's local id from another owner on a
  mount that is not theirs: it carries their prefix. The existing skin Info line (Looks.cs:316-319) becomes this
  refusal.
- Skins-only pack, two shapes:
  - for its own mount: `gk2mounts\acme.dragon\skins\frost\` -> `acme.dragon/frost`;
  - for someone else's mount (ours or Bob's): `gk2mounts\dragon\skins\acme.frost\` -> `dragon/acme.frost`.
- Cfg keys: `Skin_acme.dragon`, `Coat_acme.dragon`. BepInEx config keys accept dots (k). Nothing in our cfg handling
  splits a key on dots: the per-mount suffix is found at the first `_` (`LightnessKey`,
  Api/MountSettingsPage.cs:746), and the key names before it have none; `tools/page-carryover.py` has no dot or
  underscore split (grep, 2026-10-24).
- **Loose copies of our own packs.** `FirstParty` excludes loose-pack owners (Looks.cs:331), so a plain "refuse bare
  ids from others" would refuse a loose `gk2mounts\dragon\` folder. Since 2026-10-24 a newer loose copy of our pack
  replaces the DLL's by the version rule (docs/mounts-as-mods.md:275, CHANGELOG.md:26), and a skins-only folder for
  our `dragon` must stay possible. Rule: a loose folder whose bare id is already one of our mounts is a copy or a
  joiner and meets the version rule; a loose folder with a bare id we don't own is refused. Catch: the loose scan
  runs in the API's Awake, before Saddle Up registers its mounts (Looks.cs:125), so "one of ours" is not known yet
  then. Either decide it at game-ready, or treat a bare loose id as provisional until then.
- Saves: the side file keeps `Skin = acme.dragon/frost`. A different author's dragon is `bob.dragon`; A's saved
  dragons stay dormant donkeys ("Not installed") instead of turning into Bob's.
- Ergonomics: one dot. Mounts are gated off in every release so far (docs/mounts-as-mods.md, Debug.MountsPreview), so
  no third-party mount ids exist in the wild yet: enforcing now costs no compat shim. Later it would.
- Cost: the refusal text, the docs (making-a-mount.md, skins.md), the self-test "ids", and one Info -> refuse switch.

### C. Owner-qualified identity, derived (Minecraft / `{{ModId}}_` style)
- The full id is `<namespace>.<name>`, the namespace taken from the owner: the plugin GUID for code and embedded
  packs (`com.acme.gk2dragons.dragon`), and for loose packs a required `namespace =` line in `mount.cfg` (a loose
  folder has no GUID; its owner `<api>.packs.<folder>` changes when the player renames the folder).
- Skins-only pack must name the target in full: folder `gk2mounts\com.acme.gk2dragons.dragon\...` or a `target =`
  line. Long, and easy to get wrong.
- Saves: the most robust; nobody can take another's id even by copying.
- Ergonomics: the worst. A mount that moves from a loose pack to a DLL changes namespace unless it pins one, which is
  option B with more steps.

### Add-on to any option: notice a swap
- Write the mount's author (or owner) next to the skin in the side file and next to `Mount` in the cfg. On load, if
  the live mount of that id has a different author, log one Info and keep the instance dormant. Cheap insurance for
  the copied-prefix case; optional with B, necessary with A.

## 5. Recommendation

**B**, decided and built in the same change as the scoped skins:

1. Scoped skins put the mount id into the saved skin id. Namespacing mount ids afterwards would be a second
   migration of the same data. Do both together: one rewrite of `Skin = red-dragon` to `Skin = dragon/<skin>` (our
   first-party ids stay bare, so our rename table, Looks.cs:273-281, stays the only shim, and only for dev worlds).
2. Enforce the dot for non-first-party mount ids, and for non-first-party skin local ids on a mount not their own,
   now, while mounts are gated off for players; refuse, don't just log. Turn the skin Info (Looks.cs:316-319) into
   that refusal and add it to `Catalog.Register` and `Catalog.Declare`. A loose folder with a bare id that is already
   one of our mounts stays a copy or joiner (section 4B).
3. One collision rule for all three paths: code vs code, pack vs pack, code vs pack all go through the version rule,
   since a same-id meeting can now only be two copies of one author's mount. This also closes open-items.md:337.
4. Make a refused mount.cfg's skins behave the same loose and embedded: join the surviving mount (same author, same
   mount), logged. Today the loose path drops them (MountPacks.cs:164-165) and the embedded one joins (Looks.cs:385-391).
5. Have `Mounts.Catalog.Register` return a refusal result (or catch and log) instead of throwing into a mod's Awake
   (Catalog.cs:155, Api/MountCatalog.cs:104-105).
6. Give code mount sound banks an owner in the id like pack mounts have (`gk2.mount.<id>.<key>` today,
   SoundBank.cs:389), so two owners never share a bank entry even transiently.
7. Dotted ids make the `.ability.` substring check a little less far-fetched (open-items.md:342-344,
   Persistence.cs:782-786): tighten it to `<loaded guid>.ability.<mount id>` in the same change.
8. Optional: the author stamp of section 4's add-on, for the case where two authors pick the same prefix.

Not recommended: C. It buys protection against prefix copying at a high cost to every skins-only pack author,
and B plus the version rule already treats a copied prefix as "same mount, newest wins".
