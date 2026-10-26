# Content from many mods: what the API needs (2026-10-26)

The user (2026-10-26): *"let's start preparing to have the donkey api properly accept work from multiple mods and we
see what changes we need to the api to support this. We need to be careful about load order, errors while loading,
mismatches, incompatibilities, corruption issues and all the rest and side effects there could be when loading from
other mods."* The question behind it: every mount and skin stays in the Saddle Up repo but ships as its own mod, and
other authors ship theirs.

**Built the same day (API 0.40.0; the user: "lets start working on this before we actually start the splitting.
Let's make sure our foundation is solid first")**, with the user's decisions: D1 yes (effect ids namespaced, Hellfire
renamed), D2 Awake/Start only, D4 listed as unavailable, D6 refuse and cap. Section 10 says what was done with each
finding and what is left. The text below is the investigation as written; its line numbers are of before the work.

This is a read-only investigation. Nothing was changed. Five surveys of the current code (load order, errors and
isolation, versions and conflicts, persistence, resources and safety), and the worst claims re-read by hand (marked
**checked**). Line numbers are of 2026-10-26 (API 0.39.0, uncommitted jump work included). It builds on
docs/mounts-as-mods.md (the three routes in, the load-order fix of §10) and docs/mount-id-collisions.md (the id rules
and the one version rule, decided 2026-10-24); what those settled is not repeated.

**Short answer.** The foundation is sound: every route goes through one loader, ids are namespaced, one version rule
decides between two copies of a mount, each pack is loaded in its own try, nothing a pack brings reaches the game's
save, and a removed mod leaves its data dormant, not lost. What is missing is the defensive layer you need once
strangers' files and code are loaded: **three ways to lose the side file's data**, **a handful of single files that
can freeze or exhaust the game**, **a few calls that still throw into another mod's Awake**, **a replace path that
mixes two versions of one mount**, and **no way for a mount.cfg to say which API it needs**. Most are small and need
no decision. Section 8 lists the decisions.

## 1. Data loss (fix first; affects every player, with or without other mods)

**1.1 An error in the tick wipes every kept instance, and the next save deletes the side file.** **Checked.**
`Plugin.cs:845-851` catches anything thrown by `Mount.Tick` and calls `Mount.ForceCleanup("error")`.
`MountRegistry.cs:680-738` sets down the cargo, `Discard`s and `Unregister`s **every** instance, parked carts,
babies and other mods' instances included. At the next save, `WriteSideFile` (`Persistence.cs:325-334`) finds nothing
kept and **deletes the file**, or rewrites it with whatever is left. The same happens when `Enabled` is switched off
mid-game (`Plugin.cs:807-814`); the save patch stays installed.
- How reachable it is from another mod: abilities, skin drawing, rules and event handlers are already caught per call
  (section 7), so a pack or code mod reaches this only through a fault in code that isn't wrapped. It still exists
  today, and its result (every parked cart, name, baby and mod data line gone after the player next sleeps) is bad
  enough to fix whoever triggers it.
- Fix: a `persistenceTainted` flag set by `ForceCleanup` outside a scene change. While it is set, `WriteSideFile`
  leaves the file alone: no delete and no rewrite. Or it writes from a snapshot taken before the cleanup. Log it once.

**1.2 A failed write turns into a stale file, which is ignored, then overwritten.** **Checked.** The save patch
catches any error from `WriteSideFile` (`Patches.cs:263-275`): an antivirus or Steam Cloud lock on `File.Replace`, or
a full disk. The game's save has already succeeded with a new time. At the next load the file's `saved`/`day` no
longer match (`Persistence.cs:557-564`), so it is "ignored, nothing restored", left in place, and replaced at the next
save. Unlike the format branch just above it (`SetAside`, `:545-555`), nothing is kept. The same happens after a
session without the API, or after a cloud or backup restore.
- Fix: set a stale file aside as `<slot>.gk2donkeyapi.stale-<saved>.dat` (the `SetAside` that exists). Retry
  `File.Replace` a few times, and keep the previous file as `.bak`.

**1.3 A restore that throws half-way loses the rest.** `MountRegistry.cs:381-388` catches anything from
`RestoreFromSideFile` and marks the game ready. `Spawn` and the code around it (`Persistence.cs:~671`) have no try per
record, so the records after the failing one never reach memory, and the next save writes without them.
- Fix: a try per record. A record that fails is carried forward verbatim, as dormant records are, and a failed
  restore sets the old file aside before the next write.

**1.4 Records skipped at load are not written back.** Cut off, without a scene, duplicated (`Persistence.cs:582-597`),
"could not be spawned again" (`:697-701`), a parent that didn't come back (`:658-663`): each is logged and gone at the
next save. Only dormant records keep their lines.
- Fix: every well-formed record that wasn't restored goes into the dormant list with its raw lines.

**1.5 A loaded mod's value on a key it doesn't know is dropped.** `RestoreSetting` (`Persistence.cs:798-812`) asks
`InstanceTuning.Find`, which throws for an unknown key; the line is warned about and dropped. `AsideSets` keeps values
only of owners that aren't loaded. So a downgrade, or two machines on different versions sharing a save through the
cloud, loses those values.
- Fix: unknown-key `set` lines go to `AsideSets` as well. Unknown line kinds in a live record are kept as raw lines.

**1.6 Smaller holes in the same place.**
- `MountHandle.Remove()` (`Api/MountInstances.cs:378-381`) has no owner check, so any mod can delete another mod's
  kept instance with its cargo. `SetData` has no size limit, and the side file syncs to Steam Cloud.
- At hitch, a free cart's `AsideSets` are not moved with it (lost). At unhitch, mod `data` stays on the donkey
  (`MountInstance.cs:845, 884-943`).
- The `.ability.` owner test is a substring match (`Persistence.cs:796`): a mod whose GUID contains `.ability.` loses
  every value it sets. Tightening it was item 7 of mount-id-collisions.md §5 and was never done.
- An aside file of the same format is overwritten by the next (`:365`). Aside files end in `.dat`, so they go to the
  cloud. Dormant records can't be forgotten outside the dev tab.

## 2. One bad file freezes, exhausts or blinds the game

**2.1 A malformed embedded WAV loops forever on the main thread.** **Checked.** `SoundBank.FromWav`
(`SoundBank.cs:208-227`) reads the chunk length as `Int32` and sets `pos = body + len + (len & 1)`. A length of -8
leaves `pos` where it was: an endless loop that no catch can stop. Embedded `.wav` files reach it at first play.
- Fix: refuse `len < 0 || len > bytes.Length - body`. Cap the size of a WAV (it is decoded into a `float[]` on the main
  thread).

**2.2 A huge PNG is decoded whole before any size check.** `DecodePng` (`Looks.cs:989-1013`) calls `LoadImage` on any
bytes. Only afterwards does `Put` skip a frame of the wrong size. A sheet stays decoded for its build. Every `.png` in
the folder becomes a key (`KeysOf`, `:659-665`), including names the base doesn't know. Portraits (`:1068-1072`)
have no limit, stay readable and are cached per skin. One 16k×16k PNG is about 1 GB allocated on the main thread.
- Fix: read the IHDR width and height (bytes 16-23) before `LoadImage`. A frame must be the canvas size, a sheet at
  most cells × canvas, a portrait at most 512². Build only the keys the base knows. Portraits are made non-readable.

**2.3 NaN and Infinity get through.** `float.TryParse(..., NumberStyles.Float)` accepts both, and
`Mathf.Clamp`/`Math.Min`/`Max` pass NaN on. It reaches:
- `foal_scale` (`MountPacks.cs:103-110`);
- `pivot`/`offset`/`offset_cart` (`PairF`, `Looks.cs:912-917`, no range check either: `offset = 5000 0` is
  taken);
- `[Game] scale` (`:868`);
- sound volume and pitch (`SoundBlock.cs:166`);
- from code: `MountDef.Speed` and `FoalScale` (`Api/MountCatalog.cs:73, 117`), and a code skin's canvas (only
  `<= 0` is checked, `Looks.cs:386`; a 4096 canvas builds 44 huge cells).

`[Jump]` (`JumpShapes.cs:117, 137`) and `speed` already refuse it.
- Fix: one `Num.Finite(value, min, max, report)` used by every float a pack or a code mod hands in. `MaxCanvas` also
  applies to code skins.

**2.4 One unreadable folder ends the loose scan for everyone.** `Directory.GetDirectories(root)` and
`GetDirectories(packDir)` (`Looks.cs:558, 570`) sit outside the per-pack try. One folder that can't be read (rights,
a broken junction) stops the scan, and every later pack is missing. The single warning comes from `:128`. In
`RegisterMountFolder`, a failing `GetDirectories(skinsDir)` after the declare leaves a mount with no skins
(`MountPacks.cs:180`).
- Fix: a try per folder at both levels. A mount whose skins can't be listed is withdrawn.

**2.5 Files outside the pack can be read, including over the network.** `sheet =`, `sheet_front =` and `portrait =`
go through `Path.Combine(dir, value)` (`Looks.cs:612-615` **checked**, `:767, 787-788`; `MountPacks.cs:157-167`).
`Path.Combine` keeps a rooted second argument as it is. A downloaded pack with `portrait = \\host\share\p.png` makes
Windows open an SMB connection when the panel shows it, which sends the user's NTLM hash to that host. `..\..\` reads
any PNG on the disk. Sounds are already confined: names are checked against the folder listing, `SoundBlock.cs:196`.
- Fix: the same rule for pictures. A name must be in the pack's own file list; rooted names and `..` are refused.

**2.6 Embedded cfg text keeps a UTF-8 BOM.** `Encoding.UTF8.GetString` (`Looks.cs:488, 526`) keeps U+FEFF, which can
hide the first `[Mount]` header. Then id, version, cart and the rest are silently defaults. Loose files are read with
BOM detection. *Unverified* whether `Trim()` on Unity's Mono strips it; the fix (strip U+FEFF before parsing) is free
either way.

**2.7 Dev export paths come from pack frame names.** `LooksExport.cs:56` builds `Path.Combine(dir, key + ".png")`
with `key` from a sheet's `frames =` list, unchecked. Dev only (the F11 export button), but it writes. Fix: frame keys
go through the id character check at parse time.

## 3. Calls that throw into another mod's Awake

The 2026-10-24 decision for mounts was "one version rule, no throw". It was never applied to the rest:
- **Effects** (`Effects.cs:138-150`, **checked**) have no `<mod>.` rule and no version rule, and throw when another owner
  has the id. Saddle Up registers the bare `hellfire` (`src/SaddleUp/Plugin.cs:44`, called without a try at `:68`).
  So an effect `hellfire` from a mod that sorts first stops Saddle Up's Awake after its skins: no companion, no
  settings page.
- **Code skins** that lose the version rule throw (`Looks.cs:370-381`). So does a code `default` skin whose mount lost
  its own collision: it lands on the winner's mount and gets the prefix error, `:361-365`.
- **Settings-row callbacks** (`Visible`, `Enabled`, `InfoValue`) run unguarded in `SettingsRows.cs:145, 531` and
  `MountsPanel.cs:555, 569, 675, 1340`. One mod's throwing `Visible` closes the shared Mounts panel with the wrong
  message ("could not follow the mount change"). The same delegates go to GK2 Mod Framework unwrapped
  (`FrameworkLink.cs:163, 194-200`).
- **`IMountSkinSource.TryGetFrame`** is guarded in `Acquire` but not in `LooksInherit.cs:98` or `MountIcons.cs:75`. So is an
  ability's `TicksWhileStanding` getter (`Looks.cs:2973`).
- **Calling before the API's Awake** (a mod without `[BepInDependency]` that sorts first): `KeptOut` logs through a
  null `Plugin.Log`, which throws a NullReferenceException (`Mounts.cs:432-436`).

Fix: the mounts' rule everywhere. The loser gets one warning naming both sides and a handle that does nothing.
Callbacks are wrapped once when the row is built (logged once with the owner named). An entry point says "add
[BepInDependency]" when the API hasn't woken.

## 4. Order and identity

**4.1 A newer pack takes the mount's facts, the old pack keeps its pictures and sounds.** **Checked.** Pack against
pack, `Catalog.Declare` (`Catalog.cs:230-238`) swaps `Owner` and `Pack` and calls `Redeclare`. It doesn't call
`Retire(have)`, as the code-mount branch above it does, and it doesn't withdraw the old owner's skins. The new pack's
`skins\default\` then meets the skins' version rule on its own. Two loose folders whose skin.cfg has no `version`
tie, and the first stays. A player who unzips v2 next to v1 gets v2's speed, canvas, jump and sounds with v1's
pictures. The old pack's sound files stay in the bank, and its portrait stays cached. No `MountsChanged` is raised
(`Redeclare` → `Touch`), so an open panel shows the old facts.
- Fix: when another owner's pack takes a mount, withdraw that owner's skins of it, `Retire` it (sounds, portrait),
  then register the winner's skins. Raise `MountsChanged`.

**4.2 Embedded `[Mount] id` different from the folder.** `MountFromCfg` takes the id from the file and only reports
the difference ("its skins are found in it either way", `MountPacks.cs:91`, **checked**). `RegisterEmbedded` finds
the skins' mount by folder name (`Looks.cs:524-525`) and calls `NoSkinLeft(Catalog.Find(dir))` (`:541-542`). The
mount is listed with nothing to wear, and its skins hang on a mount nobody registered. The loose path uses the
declared id and works.
- Fix: refuse an id that differs from the folder (the decided rule: "the folder name is the id").

**4.3 A code skin registered before its mount skips the name rule.** **Checked.** `AddsToOthers` (`Looks.cs:301-307`)
returns false for a code skin when its mount isn't registered yet. So mod B, sorting first, can register
`acme.dragon/default`. When Acme's mod comes, its own default ties and the first stays: Acme's dragon wears B's look,
or Acme's Awake gets the throw. Packs are safe, because their folder sets `AddedToOthers`.
- Fix: a code skin adds to others unless its owner is the mount's owner. Before the mount exists, the skin's mount
  must later turn out to be its owner's, or the skin is set aside. `default` is never taken from another owner.

**4.4 Skins of a mount that isn't installed are offered as donkey skins.** **Checked.** `SkinDef.Mount` falls back to
`Catalog.Donkey` (`Looks.cs:193-213`), and `Catalog.SkinsOf` (`Catalog.cs:346-354`) uses it. So `SkinChoicesOf(donkey)`
lists them. Scenario: `gk2mounts\acme.dragon\skins\me.frost\` is installed without Acme's mod. The donkey's Skin row
offers Frost, and `Skin_donkey = acme.dragon/me.frost` is saved. Install the dragon later, and with Mount = Donkey the
player rides a dragon.
- Fix: a skin whose mount isn't registered is dormant. It is in no list and no count, and is worn by id once its mount
  arrives.

**4.5 Late registration (after the first frame).** `CatchUp` returns once the page is shown
(`Api/MountSettingsPage.cs:518`, **checked**), and `Add` throws after Publish (`:902`). A mount registered from
`GameReady`, a coroutine or a script reloader gets no Skin or colour rows. Worse, at the next start `ApplyMountRows`
(Saddle Up's Awake) finds no Skin row for it and sets the companion's skin to the donkey. The Mount row says Dragon;
the player rides a donkey, every start. The panel's Skin dropdown is also filled once (`FillList` returns early once
`ChoiceLabels` is set, `:627`), so a skin added or withdrawn later is shown as "not installed" or stays pickable.
- Fix or rule: see decision D2.

**4.6 Handles and losers.**
- A handle matches on id and owner only (`MountCatalog.cs:150-171`, `MountLooks.cs:170-190`). A mod that registers
  again and disposes its older handle withdraws its live registration. Fix: the handle holds the registered object,
  and withdraws only while that is still the one registered.
- The loser of a version conflict is never brought back (`Catalog.cs:186-204`). If the winner is withdrawn, the mount
  is gone for the session although the other copy is installed. Fix: keep the losers per id and restore the next
  best, or document "until restart".
- `RegisterEmbedded` and `RegisterFolder` raise `MountsChanged` per mount before its skins exist. Fix: raise once at
  the end of the batch.
- Owner ids are self-declared strings. Two mods built from the example with its GUID unchanged replace and withdraw
  each other's skins without a word. Fix: warn when one owner id registers from two assemblies.

## 5. Versions and mismatches

**5.1 A mount.cfg can't say which API it needs.** `MountKeys` (`MountPacks.cs:41`) has no `needs` and no `format`.
Only skin.cfg has them (`Looks.cs:760-764, 802`). Unknown keys and sections in a mount.cfg are logged at **Info**
(`MountPacks.cs:218`). So API 0.38 loads a 0.39 dragon without its `[Jump]`: the mount is quietly wrong. The worked
example in docs/making-a-mount.md shows `[Jump]` (0.39) with `needs = 0.36` (line 98) and `BepInDependency 0.36.0`.
- Fix: `needs` and `format` in `[Mount]`, refused the way skin.cfg refuses them. An unknown section in a mount.cfg is
  a warning. Correct the example to 0.39.

**5.2 Breaking API changes are invisible to code mods.** Before 1.0, a breaking change bumps the minor
(docs/api-design.md:191). 0.36 and 0.37 removed members (`MountDef.Effect` and others). A mod checks only a minimum:
`BepInDependency(min)`, `Mounts.ApiVersion`. A mod built against 0.36 passes every check on 0.39, then throws
`MissingFieldException` when Mono compiles its Awake. That happens before the version check on the next line runs,
unless the check sits in its own method.
- Decision D3.

**5.3 `tests/ApiSurface` misses binary breaks.** It records kinds and member signatures (`Program.cs:17-31`), not
abstract/virtual/sealed, base types, interfaces or nested types. Adding an abstract member to `MountAbility`, or a
member to `IMountSkinSource`, reads as "ADDED" yet breaks every implementer. Sealing a class doesn't show at all.
- Fix: record those. An added member on an interface or abstract type is flagged as breaking.

**5.4 A skin pack for a mount that changed shape.** Wrong-size frames are skipped one by one, with a warning each (up
to 44), and the default is drawn. A canvas different from the default's is warned once. A sheet skin that leaves out
`canvas` (as docs/skins.md:284 advises) is cut by the new canvas into wrong cells, with no warning. Fix: an optional
`for_canvas` (or `mount_version`) in skin.cfg, compared at load, and the per-frame warnings folded into one.

**5.5 Without Saddle Up**, a mount mod registers and nothing offers it. Only a settings page or companion offers
mounts, and nothing says so. Fix: one Info at game ready ("N mounts registered, no mod offers them").

**5.6 Ids and version strings.**
- `CheckId` takes any Unicode letter (`Catalog.cs:155-166`): `saddleup.drаgon` with a Cyrillic а is a second dragon.
  It also accepts a trailing dot, which Windows strips from cache folder names.
- `CompareVersions` (`Looks.cs:428`) falls back to an ordinal compare when one side doesn't parse (`9` beats `10`).
- `SafeName` maps `/` to `_` (`a.b_c/d` and `a.b/c_d` share a cache folder).
- Sound bank ids are joined with dots (`SoundBank.cs:372`), so two different owner/name pairs can give one id.

Fix: ASCII only, no leading or trailing dot, a length cap, version strings that must parse, and a separator ids can't
contain.

## 6. Resources and cleanup

- The sound cache (`BepInEx\cache\GK2DonkeyAPI\sounds\<owner>\<skin>\`) is checked by length only
  (`SoundBank.cs:316-317`): an updated `.ogg` of the same length plays the old one. It is never cleaned when a mod
  leaves. Fix: compare a hash or the source's time; prune owners not loaded for N starts.
- `framings.txt` grows one line per content hash, never pruned (`PreviewStage.cs:116-140`).
- Set-aside game-source skins (Gerry style, a wrong `prefix =`) are re-checked in one frame once a minute, each one a
  full `Resources.FindObjectsOfTypeAll<Sprite>()` (`LooksGame.cs:135-155, 258-300`). N bad packs means N full scans per
  minute. Fix: re-check on scene load only.
- `PackStamp` stats every file of every pack at start (`Looks.cs:620`, `MountPacks.cs:171`), and only the dev
  auto-reload uses it. Fix: compute it only when that is on.
- Log spam:
  - a broken sheet warns once per key (44-88 lines) at each acquire;
  - an effect source every 5 s, `TickAll` every frame, the flight's FixedUpdate every physics step, `Mount.Tick`'s
    error every frame;
  - `Played` is one multicast call, so the first throwing subscriber silences the later mods' handlers, and it
    names no mod.

  Fix: warn once per cause, call each delegate on its own, name the assembly.
- `Mounts.Sounds.Set` overrides have no owner (`Sounds.cs:922-926`, `Api/MountSounds.cs:192-198`): mod B silently
  replaces mod A's line, and A's Dispose removes B's. Fix: first owner wins, as for session tuning (`MayReplace`).

## 7. Already handled well (don't redo)

- **One loader for every route.** Each pack and each skin is loaded in its own try; the API's Awake is protected
  (`Looks.cs:120-131, 578-588`, `MountPacks.cs:189-225`, embedded `Looks.cs:510-538`).
- **No mount without a look.** Everything that can refuse a skin is checked before its mount is declared. A refused
  default withdraws its mount (`NoSkinLeft`), and a refused embedded mount leaves its skins out.
- **Namespaced ids and one version rule for mounts**, code and pack alike, with no throw (docs/mount-id-collisions.md
  "Decided"). The gate `KeptOut` is applied at every public entry.
- **Load order between Awakes.** Publish waits a frame, and `CatchUp` adds rows for everything registered in any Awake
  or Start, whatever BepInEx's order (docs/mounts-as-mods.md §10). A skin's mount is re-resolved on every catalog
  change. The Mounts panel follows `MountsChanged`/`SkinsChanged`, coalesced to the next frame.
- **Callbacks caught per call, owner named.** Abilities (switched off per instance, logged once), rules (allow on a
  throw), key actions, buttons, cargo and instance events, `GameReady`, `InstancesRestored`, and the effects' Show
  rule.
- **Pictures bounded once decoded.** Canvas 8-256, sheets checked against one cell and `maxTextureSize`, built 2 ms
  per frame, freed by reference count, panel holds capped at 64 MB. Nothing is decoded at Awake.
- **Sounds.** Started one file per frame; failed files remembered; owner-scoped bank ids under `gk2.`; removed from
  `AudioConfig` and destroyed on withdraw; pack sound names confined to the folder's listing.
- **Tuning from packs** is limited to the allowed keys, clamped, per instance, and respects other owners' overrides.
  Session tuning goes to the first owner.
- **Persistence.**
  - The write is atomic (`.tmp` + `File.Replace`), and each record is parsed on its own.
  - A file of an unknown format is set aside.
  - A removed owner's records stay dormant verbatim, with their numbers and keys reserved, and their `set` values go
    to `AsideSets`.
  - Every mod's `data` lines are written whatever the owner's state.
  - Cargo is reclaimed by GUID within 3 units, so nothing can be duplicated.
- **The game's own save is never touched.** Mount objects are temp objects (`Mount.cs:1244`), and
  `WhyDefinitionTouchesTheSave` refuses anything else (`Mount.cs:836-853`). Pack sounds live only in the runtime
  `AudioConfig`.
- **Nothing executed from packs.** No scripts, no type names, no deserialiser. Panel text has `richText = false`. No
  threads beyond UnityWebRequest's own I/O.

## 8. Decisions for the user

- **D1. Effect ids.** Recommended: the same `<mod>.<name>` rule and version rule as mounts, and Saddle Up's `hellfire`
  becomes `saddleup.hellfire` (the old name kept as a lookup, as the donkey skins' was). This costs nothing now:
  effects aren't picked or saved anywhere since API 0.37.0, and mounts are gated off for players. After the next
  release it costs a shim.
- **D2. The registration window.** Either (a) "Awake or Start, nothing later": a late mount or skin logs one Info and
  gets no rows this session, and `ApplyMountRows` never heals a stored skin to the donkey because its mount isn't in
  yet. That second part is needed either way. Or (b) late registration fully supported: `CatchUp` runs again on
  `MountsChanged`/`SkinsChanged` after Show, for look rows only (they are panel-only, never in the Mods menu or the game's
  Settings), with the panel's lists refilled. Recommended **(a) now, with the heal fixed**. (b) can follow if an author
  needs it.
- **D3. Code mods and breaking changes.** Either go to **1.0 when the mounts gate opens**, after which breaking means
  2.0 and `BepInDependency` works as mod authors expect, or add `Mounts.OldestCompatible` with a documented check in
  its own method. Recommended: 1.0 with the gate, plus the ApiSurface fix (5.3) so a break can't slip in unannounced.
- **D4. Skins of an absent mount** (4.4). Recommended: dormant, out of every list, worn when their mount arrives.
- **D5. Author stamp** (mount-id-collisions.md §4 add-on, never built). The mount's owner next to the skin in the
  side file and next to `Mount` in the cfg; a different owner of the same id keeps the instance dormant with one Info.
  Optional; it only matters when two authors pick the same prefix.
- **D6. `MountHandle.Remove` and `SetData` across owners** (1.6). Recommended: `Remove` refused for another owner's
  instance (the panel and the API exempt), and a cap on data key and value length.
- **D7. Which mods the content ships as.** Recommended: no API change. Folder packs or one tiny DLL per mount, as
  sketched in chat on 2026-10-26. Decided after the API work, because 5.1 (`needs` in mount.cfg) is what makes the
  folder-only shape safe.

## 9. The work, in order, and its proof

Each step is a self-test or a scan rule that is red today and green after. The user runs F11.

1. **Data loss (1.1-1.5).** F11 "persistence: never lost":
   - force a tick error with kept instances, save, and check the file is unchanged;
   - make the write fail (file locked), load, and check the stale file is set aside;
   - a record that throws on spawn is carried forward;
   - an unknown key's value survives a round trip.

   The upgrade fixtures (0.9.1, 0.10.0) stay green.
2. **Hostile files (2.1-2.6).** A temp pack per case: a WAV with a -8 chunk; a 300×300 frame and a 4000×4000 sheet
   (refused from the header, with no allocation); `foal_scale = NaN` and `offset = NaN 0`; `portrait = ..\x.png` and an
   absolute path; an embedded cfg with a BOM; an unreadable folder beside a good pack (the good one still loads).
   SkinsScan gets the same rules.
3. **No throw into an Awake (3).** Self-test: two owners register one effect id and one code skin id. Neither call
   throws, the older one is out with one warning, and a throwing `Visible` doesn't close the panel.
4. **Order and identity (4.1-4.4, 4.6).**
   - Two loose folders of one mount, v1 and v2: v2's facts and v2's pictures, and v1's sounds gone from the bank.
   - An embedded pack with `id` different from its folder: refused.
   - A code `default` from another owner before the mount: set aside.
   - A skin pack without its mount: in no list.
   - A re-register then dispose of the old handle: the mount stays.
5. **Versions (5.1, 5.3).** `needs = 9.0` in a mount.cfg is refused with its skins; ApiSurface flags an added
   interface member; making-a-mount.md's example says 0.39.
6. **The rest (5.4-5.6, 6).** The id character rules, cache by hash, re-check on scene load, the folded warnings, and
   sound overrides with an owner.

Steps 1-3 need no decision and touch no format a player has. Step 4 needs D4, and D2 for 4.5. D1 goes with step 3,
and D3 with the release that opens the gate.

## 10. Built (2026-10-26, API 0.40.0)

Every build deployed with the game closed; the release scans 22 of 22, SkinsScan, NoLinqScan and ApiSurface green.
The self-tests below are new and not yet run in the game (F11).

| Finding | What was done | Proof |
|---|---|---|
| 1.1 an error wipes the side file | `Mount.HoldSideFile` from the update's error and the API switched off mid-game: saves leave the file as it is until another game loads (`WriteSideFileAt`) | Never lost: "side file: held after an error ..." |
| 1.2 a failed write, then overwritten | the stale branch sets the file aside (`SetAsideAs`, `.stale-day<N>.dat`); every write keeps `.bak`; a busy file tried three times (`Retry`); deleting a save deletes both | the same scenario |
| 1.3 a restore that throws half-way | a try per record; a failed one is carried forward (its items set down again, its view gone); the whole restore failing holds the file | "side file: a record that fails ..." |
| 1.4 skipped records lost | spawn refused, an error, a parent that didn't come back: carried forward as read (`CarryForward`), tried at the next load | the same scenario |
| 1.5 unknown keys dropped | kept in `AsideSets`; lines of an unknown kind kept on the instance (`ExtraLines`) and written back | the same scenario |
| 1.6 Remove, data, ability owner, hitch | `Remove(ownerId)`, the parameterless one by the caller's assembly; `DataKeyMax` / `DataValueMax` / `DataKeysMax`; `IsAbilityOwner` exact; `AsideSets` move with the cart | "handles: ..." and the held-file scenario |
| 2.1 WAV endless loop | a chunk length below 0 or past the file refused; sound files at most 16 MB (`SoundBank.MaxSoundBytes`) | Skins and effects: "hostile files ..." |
| 2.2 PNG decoded before its size | `Numbers.PngSize` from the header; `DecodePng` takes a maximum (a frame: its canvas, a sheet: its cells, an icon: its canvas); portraits at most 512 and not readable | the same |
| 2.3 NaN and Infinity | `Numbers.TryFloat` / `Finite` in every pack and code float (foal_scale, speed, pivot and offsets kept in range and reported, `[Game]` scale and lift, sound numbers, colours, tuning values, the side file's positions); code skins' canvas range | the same |
| 2.4 one folder ends the scan | a try per folder at both levels and around a mount's skins | not testable without a locked folder; read |
| 2.5 files outside the pack | `Numbers.InFolder` in both loose readers and the loose sound loader | "hostile files" (`..\..\outside.png` not read) |
| 2.6 BOM in an embedded cfg | `Numbers.CfgText` | "hostile files" |
| 2.7 frame names as paths | a sheet's `frames =` names checked | read |
| 3 throws into an Awake | effects: prefix and version rule, no throw; code skins and recipes: `Looks.LeftOut` caught by the API (a warning); a lost mount's own code look left out; row callbacks wrapped at Show (`GuardCallbacks`); `TryGetFrame` and `TicksWhileStanding` guarded; a call before the API's Awake says to add `[BepInDependency]` | "between mods: no throw" |
| 4.1 v2 facts, v1 pictures | a pack that takes another owner's mount withdraws that owner's skins of it, forgets its sound files and portrait, raises MountsChanged | "between mods: order and identity"; the "ids" scenario updated (v1's other skin now goes with its pack) |
| 4.2 id not the folder's | refused | the same |
| 4.3 code skin before its mount | flagged at registration (`MountMissingAtRegister`), checked when the mount comes (`Looks.MountArrived`) | the same |
| 4.4 skins of an absent mount | in no mount's `SkinsOf` (never the donkey's), worn by no instance (the donkey as it is, the choice kept), the panel lists the mount "(not installed)", greyed (D4) | the same |
| 4.5 late registration | the window is Awake/Start (D2): `Catalog.Late` says once per mod when a mount or skin comes after a page was shown; the panel's Skin lists read again on a registry change | read; docs/making-a-mount.md "Living next to other mods" |
| 4.6 handles, events, owners | handles hold the registered object; `Catalog.Batch` makes a folder or a DLL one MountsChanged; one owner id from two DLLs warned (`Mounts.NoteOwner`) | "order and identity" (handles) |
| 5.1 needs in mount.cfg | `needs =` and `format =` in `[Mount]` (kept by the Mounts tab's Save; `canonical_cfg.py` knows them); an unknown section in a mount.cfg is a warning; the worked example says 0.40 | "between mods: versions and ids" |
| 5.3 ApiSurface | records sealed / abstract, base types, interfaces, nested public types, abstract and virtual members; marks an added interface or abstract member BREAKING | the snapshot rewritten (644 lines), green |
| 5.5 without Saddle Up | one Info at game ready when mounts are registered and nothing offers them | read |
| 5.6 ids and versions | `Catalog.IdRule` (ASCII, at most 64, no dot at an end or two in a row) for mounts, skins and effects; a version that isn't a number ranks below; file sound ids joined with `|` | "versions and ids" |
| 6 sound overrides | first owner wins, Dispose clears only its own | "versions and ids" |
| 6 cache, rechecks, spam | the sound cache compared by content; set-aside game skins checked one per frame; one warning per reason for a skin's skipped pictures; the effect source, companion and flight-step warnings at most once a minute (`Said.Warn`); Played, MountsChanged, SkinsChanged and EffectsChanged handlers called one by one and named by their mod | read |

**Left, not built:** D3 (1.0 when the mounts switch opens, decided at that release), D5 (the author stamp), D7 (how the
mounts ship); 5.4 `for_canvas` in skin.cfg (a skin pack for a mount whose canvas changed); a loser of the version rule
brought back when the winner is withdrawn (until the next start); the sound cache and `framings.txt` never pruned;
`PackStamp` still read at every start; mod data left on the donkey at an unhitch; forgetting a dormant record outside
the dev tab. In docs/open-items.md.
