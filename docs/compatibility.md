# Compatibility: what an update must never break (since the public 0.9.1 / API 0.29.1, 2026-10-18)

GK2 Saddle Up 0.9.1 with GK2 Donkey API 0.29.1 is on Nexus (2026-10-17). From here on every release is an **update
over a player's existing files**, and the rule is simple: a player who installs the new version keeps every setting,
every colour, every parked cart with its load, and never has to do anything by hand. This page names the files a
player owns, what in them may change and how, and the checks that prove it before each release.

The repo's old rule ("nothing shipped, so no compatibility code") now applies only to the parts that never shipped
and to code inside one API version. The code still stays clean: compatibility is a read path for old data, not a
second copy of anything.

## 1. The four stores a player owns

| Store | Where | What's in it | Who writes it |
|---|---|---|---|
| Saddle Up's cfg | `BepInEx\config\dreas.gk2.saddleup.cfg` | keys, name, mount and skin, babies, effect, behaviour, the donkey's and cart's colour choices (`Coat = Custom` ...), lantern, sounds | BepInEx, every bound key with its value, sections numbered by their place on the page (`[5. Donkey]`) |
| The API's cfg | `BepInEx\config\dreas.gk2.donkeyapi.cfg` | every tuning value, `[Colors]` and `[Colors.<Region>]` (the 24 custom colours a player mixed), `[Shuffle]`, `[Corpses]`, `[Look]`, `[Effects]`, `[Safety]` | BepInEx, every bound key with its value, sections unnumbered |
| The side file | `<save>.gk2donkeyapi.dat` next to each save (Steam Cloud carries it) | the player's donkey, foals and carts: scene, position, facing, size, name, the cargo on each cart (item GUIDs), a chest aboard, per-instance settings and each mod's data lines | `Mount.WriteSideFile` after every save, format 2 (`docs/persistence.md`) |
| The game's save | the game's own | nothing of ours: a save loads without the mod | the game |

Colours are the case the user named: a player who mixed custom colours has `Coat = Custom` in Saddle Up's cfg and
the numbers in the API's `[Colors.Coat]`. Both must survive every update as they are.

## 2. The rules, per store

### 2.1 Saddle Up's cfg (every `MountSettingsPage`)

- **Per-mount rows keep the donkey's keys.** `PerMount` gives the donkey's row set no suffix, so `Coat`, `CustomCoat`,
  `Paint`... are the 0.9.1 keys with the 0.9.1 values; the other mounts' sets are new keys (`Coat_dragon`). Nothing
  to carry over.

- **A key's name and type are permanent.** Labels, descriptions and the choice labels shown to the player can change
  freely; the cfg key (`Coat`), its type and an enum's member names (`CoatColor.Custom`) cannot. A renamed enum member
  would make `SetSerializedValue` fail and the page would apply the default: the player's choice gone. The upgrade
  self-test reads every released value as its row's type to catch exactly this.
- **Sections may move on the page.** The number is the section's place, so a move renumbers it; the page's carry-over
  finds a key under the same plain name with another number, takes the fullest copy, and drops the old lines
  (`MountSettingsPage.Bind`, `MovedValue`, `DropOldCopies`; the file is written once at Publish, `FinishBinding`,
  since a save inside a Bind would write the old line before the drop). Seen in game 2026-10-18 with the user's own
  cfg (Donkey 3 → 5) and with the 0.9.1 fixture (29 values carried, 25 old lines left out, no warning, 21:00).
- **A key may move to another section or name**, with a `MovedKey(section, key, oldSection, oldKey)` call before the
  row (Saddle Up: Name, Foals and FoalsIndoors went from Donkey to Mount). The call stays as long as a released
  version wrote the old place.
- **A key may be dropped** only when its value has nowhere to go; the self-test lists the drop and the allow-list
  in the test names it on purpose.
- **A default may change**, but it reaches new players only: BepInEx writes every key with its value, so an existing
  player's file holds the old default as an explicit value. See 3.

### 2.2 The API's cfg (`Tuned` binds, `Colors`, `Shuffle`, `Corpses`, `Look`, `Effects`)

- **Section and key names are permanent.** There is no carry-over here: `cfg.Bind` reads the file by (section, key)
  and a renamed key resets silently to its default. A rename, if ever, is a new key whose bind reads the old one the
  way `MovedKey` does; until then, don't rename. The upgrade self-test fails on any released key this build no longer
  binds.
- **The colour sections never change shape.** `[Colors]` and the fourteen `[Colors.<Region>]` sections with their ten
  keys each are what a player's custom colours are; no key there is renamed, retyped or re-scaled.
- **New keys are free**: BepInEx adds them with their default on the next start.
- **A default may change** with the same caveat as above (3).

### 2.3 The side file

- **Every format a release wrote stays readable.** `Mount.SideFileFormat` is 2 since before 0.9.0. A bump comes with
  a reader for the old format that brings every record back; "nothing restored" for a public format is a data loss
  (a parked cart's load) and is never acceptable.
- **A file this version can't read is set aside, never overwritten.** `RestoreFromSideFile` moves it to
  `<save>.gk2donkeyapi.format<N>.dat` (`Mount.SetAside`, log warning) before anything else: `WriteSideFile` deletes
  the side file when nothing is kept and replaces it otherwise, so without this an unreadable file was gone at the
  next save. Built and self-tested 2026-10-18. **The rule that makes the log line true:** a version that reads
  format N also restores from `<save>.gk2donkeyapi.formatN.dat` when the plain side file is missing (the same
  saved/day check against the save, then the file is renamed back and read as usual). Nothing reads an aside file
  yet because no format but 2 exists; the first format bump builds this with its reader.
- **Unknown lines are skipped, missing lines default.** The reader is a `switch` on the line's name with no
  `default:` abort; a record is whole when its `end` line was read. New per-record lines (`set` lines a skin owns,
  `chest`, `name`) are additions, never a change to an existing line's meaning.
- **A newer file under an older version** is the only unguarded case: an older version sees a format it doesn't
  know and, before 0.10.0, deletes it at the next save. Players don't downgrade; Steam Cloud can carry a file between
  two machines on different versions, which is why the warning names the file and the newer version.

### 2.4 The API for mod authors

Semver with `tests/ApiSurface` (any change to the public surface fails the scan until reviewed and the snapshot
updated) and `MountSettingsPage`'s `NeedsApi` / `BepInDependency` minimum in each consumer. Inside an unreleased API
version, change in place; after it ships, a removed or changed member is a major bump and a changelog line.

## 3. Changed defaults: the open decision

BepInEx writes every key with its value. When a mod default changes (a seat value, `BackSeatExtraPx` 0 → 5, a cargo
offset), a player who never touched the key still has the old value in the file, and the new default never reaches
them. Nothing distinguishes that player from one who chose that value on purpose.

The only mechanism is a stamp plus a table: the cfg carries the version that last wrote it (a `[General]` key), the
build embeds the released defaults per version (`tests/fixtures/released/<version>/defaults.cfg` holds 0.29.1's),
and at start every key whose file value equals the *old* default and whose default moved takes the new one, with one
log line per key and the stamp moved so it runs once. The trap (a player who deliberately chose the old default is
moved too) is the same one a fresh file has, and the log line is the player's way to see it.

Decision pending; the implementation can follow the 0.10.0 release, since 0.10.0 changes no default a 0.9.1 player
could hold (`defaults.cfg` is unchanged since 7ac9a6c; `HitchGapPx` is new). Until then: a changed default is a
changelog line that says it reaches new installs only.

## 4. The checks

- **`tests/fixtures/released/<version>/`**: the player files as that release wrote them. `0.9.1` is reconstructed
  (from the sources at 7ac9a6c and the live cfg on this machine, each file's header says so); from 0.10.0 on they are
  captured at release (checklist 5): the two cfgs after the first run and a side file with a parked loaded cart.
  Every value in a cfg fixture differs from its default, so a lost value shows.
- **Self-test "upgrade: a released version's player files are all read"** (group "API, side file, carts, colours"):
  for every version folder, Saddle Up's cfg through the live page's own carry-over (`MountSettingsPage.Upgrade`:
  each old line lands on a row and reads as its type), the API's cfg key by key against what this build binds, the
  side file through the real reader (every record whole), and the set-aside of a format-99 file. Run it before every
  release and after any change to a page, a bind, an enum or the side file.
- **`tests/ApiSurface`** for the mod-author API.
- **The in-game upgrade rehearsal** (checklist 5): the release zip over a config written by the previous release, the
  parked loaded cart from before the update still there with its load, the colours as they were.
- **The live run** of the page carry-over (the self-test is a dry run: `Upgrade` never binds or writes): with the game
  closed, move `BepInEx\config\dreas.gk2.saddleup.cfg` aside and copy the oldest fixture's in its place; start to
  the main menu only (the fixture rebinds Ride to R and Cart to T and turns "Keep with the save" off: don't load a
  save); Mods → GK2 Saddle Up shows Name Bella, Mount Donkey, Coat Custom, Harness Red, Cart capacity 4, Lantern
  light Green, Volume 120, Send away F6; quit; the log has the "carried over" lines and no warning; the cfg has
  `[2. Mount] Name = Bella` and no `[3. Donkey]`; put the real cfg back.

## 5. When you change something

| Change | Do |
|---|---|
| Move a section on a page | nothing more; the carry-over handles numbers |
| Move or rename a page key | `MovedKey` before the row; keep it; changelog |
| Rename an enum member behind a `Choice` | don't; add a member instead |
| Rename or drop an API cfg key | don't; a new key reads the old one once, the old stays bound until the next major |
| Change a default | changelog line "new installs only" until section 3 is built |
| Add a side-file line | the writer adds it, the reader takes it, older files default |
| Change a side-file line's meaning or the format | a reader for the old format first; bump `SideFileFormat`; the new reader also takes `<save>.gk2donkeyapi.format<new>.dat` left by an older version (2.3); a fixture of the old file; the self-test |
| Change the public API | `ApiSurface --update` after review; version bump; `docs/api.md` history |
