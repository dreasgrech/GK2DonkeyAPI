# Looks per skin, the rest per mount (design, 2026-10-28)

The user, 2026-10-28, the day 0.12.0 and the Donkey Skin Pack went public: "our colours customization currently seems
to apply per mount instead of per skin but this doesn't make sense because skins look different", and after the
survey below: "since lantern toggles are part of the customization, we should probably make it per skin too so that
everything in the customization page will be skin specific." Saddle Up 0.13.0 / GK2 Donkey API 0.41.0.

## 1. What is per mount today, and why colours can't be

`MountLook` (`Api/MountCompanion.cs`) is keyed by **mount id** (`LookOf(mountId)`, `looks` dictionary) and holds two
kinds of things at once: the colours (coat, custom coat, harness, custom harness, muzzle = second tone, custom muzzle,
paint, wheels, hubs, lantern light and their customs) with the lantern toggles, and the mount's own (name, the babies'
count / follow on foot / come indoors / wear the skin, the three volumes). Saddle Up builds one row set per mount with
`MountSettingsPage.PerMount` (`Coat`, `Coat_saddleup.gerry`, ...; `Plugin.cs` "Mount colours", "Cart colours"); the
released 0.12.0 cfg holds 126 such colour values over seven mounts.

The colour rows mean different things on different skins, because each skin names its own regions (`skin.cfg`
`[Colors]`; docs/skins.md): Royal Parade's `Harness` is its cloth, Hellfire's `Muzzle` is its lava cracks (with
`[Base.Coat]` making the ash coat), the dragon's, rat's, snail's and worm's `Dark` / `Muzzle` / `Eye` each something
else. A red coat at 140% with a green second tone is a Hellfire look and nonsense on Royal Parade, so a player redoes
the colours at every skin change, and `MountSettingsPage.MountHasRegion` enables a row when *any* skin of the mount
has the region (Harness enabled on Hellfire because Royal Parade has one).

## 2. The split

| Per **skin** (the look: everything in the Colours window) | Per **mount** (the animal: the journal rows) |
|---|---|
| Coat, custom coat (+ lightness), Harness, custom harness, Second tone, custom second tone | Name (the user, 2026-10-26: "the name is per mount not per skin") |
| Paint, Wheels, Wheel hubs, Lantern light, their customs (+ lightness) | the Skin choice (`Skin_<mount>`) |
| Lantern, Lantern lit by day (the user, 2026-10-28: part of the customization) | Babies, Babies follow you on foot, Babies come indoors (the babies wear the parent's skin, so they follow it) |
| | Voices / Steps / Baby steps volume |

The game's own donkey art is the skin `donkey` (as `MountLooks.DonkeyId` already is), so the plain donkey is one look
among the donkey's.

## 3. The API (0.41.0)

- `MountLook` keeps its name and becomes the **per-skin** look: the colour properties and `Lantern`, `LanternByDay`;
  `SkinId` (new) and `MountId` (the skin's mount, kept for readers). `MountCompanion.LookOf(string skinId)` (the id
  of a skin; a mount id given by an old caller is taken as that mount's first skin, said once in the log).
- `MountOwn` (new, per mount): `MountId`, `Name`, `FoalCount`, `FoalsFollowOnFoot`, `FoalsComeIndoors`,
  `FoalsWearSkin`, `SoundVolume`, `StepsVolume`, `BabyStepsVolume`. `MountCompanion.OwnOf(string mountId)`. The
  companion's pass-through properties (`Name`, `FoalCount`, `Lantern`, `Coat`, ...) keep working: the colours and
  lantern ones read the current skin's look, the rest the current mount's own.
- `Current` = `LookOf(Skin)`; the paper (the panel's preview) = `LookOf(PaperSkin)`: `ApplyLook` loses its
  `MountOfSkin(ps)` detour (the look of the previewed skin is the previewed skin's).
- `LookChanged(l)` applies when `l.SkinId` is the current skin or the paper's; `OwnChanged(o)` when `o.MountId` is
  the current mount's.
- `MountSettingsPage.PerSkin(Action<string skinId, string mountId, string sfx, Func<bool> shown> rows)`: one row set
  per skin of every mount, `shown` true while that mount is chosen **and** its Skin row says that skin; skins
  registered after the page (a pack loaded later) get their rows at `CatchUp`, placed after the builder's last row as
  `PerMount`'s are. `Row.SkinId` beside `Row.MountId`.
- `MountSettingsPage.SkinHasRegion(string skinId, string region)`: the skin's own regions, plus the donkey's (coat,
  harness, muzzle) under an *over* skin of the donkey (docs/skins.md: the donkey under it keeps its regions).
  `MountHasRegion` stays for the menus (the union, as today) and the API.

## 4. The keys (Saddle Up 0.13.0) and the upgrade from 0.12.0

- A mount's **first (default) skin keeps today's keys**: the donkey's own art the plain `Coat`, another mount's default
  `Coat_<mountId>`. A 0.12.0 file loads unchanged; `page-carryover.py` and the upgrade self-test stay green over the
  0.12.0 fixture with no `MovedKey`.
- Every other skin gets `<key>_<skinId>`: `Coat_donkey/saddleup-donkey-skins.hellfire`,
  `Lantern_donkey/saddleup-donkey-skins.royal-parade` (BepInEx refuses only `=`, newlines, tabs, `\`, quotes and
  brackets in a key; `/`, `.` and `-` are fine, as `Skin_saddleup-dragon.dragon` already is).
- **The seed.** A 0.12.0 player who had a non-default skin chosen tuned the per-mount values *for that skin*. The first
  time the page binds a skin's rows and finds none of them in the file while the mount's Skin row holds that skin and
  the mount's own keys are in the file (a 0.12.0 file), the mount's values are copied into the skin's keys, once, said
  in the log ("Settings: Hellfire's colours seeded from the donkey's (0.12.0 kept them per mount)"). Other skins start
  at the game's defaults. `MountSettingsPage.SeedFrom(section, key, fromKey)` does the copy from the file's orphaned
  or bound value; the upgrade integration test gets a crafted 0.12.0 cfg (Skin_donkey = Hellfire, Coat = Red, Lantern
  = false) and checks Hellfire red and lantern-less, the plain donkey at its defaults.

## 5. The panel

- The Colours window is the chosen skin's: titled "Colours: Donkey — Hellfire" (the skin's name after the mount's
  when it isn't the mount's first), its rows the `PerSkin` set whose `shown` is true. Changing the Skin row in the
  journal rebuilds the window's rows (the panel's row signature; `ApplyMountRows` on the Skin row's change).
- The preview already draws the previewed skin with its own look (`PaperSkin`).

## 6. Tests, tools, docs

- Self-tests: "settings: the Mount, Skin and babies rows" follows the colour rows to `PerSkin` (the beast's default
  skin keys as today, a second temp skin of the beast with its own `Coat_<skin>`); new "looks per skin: two skins of
  one mount keep their colours and lantern, switching the Skin row applies each, the paper shows the previewed skin's";
  the integration tests' Looks/Shipped rows follow; the upgrade seed above; `UpgradeFiles` over 0.9.1, 0.10.0, 0.12.0.
- `tools/page-carryover.py`: parses `PerSkin` rows (a key per skin of every shipped mount, the default skin's plain).
- `tests/ApiSurface`: `MountOwn`, `OwnOf`, `LookOf`'s meaning, `PerSkin`, `SkinHasRegion`, `Row.SkinId`.
- Docs: this file; mounts-panel-design.md (the Colours window per skin), mount-colours.md (per skin), api.md (0.41.0),
  compatibility.md (the seed), CHANGELOG (players: "colours and the lantern are kept per skin now; what you had set
  goes to the skin you were wearing"; mod authors: the API).

## 7. Status

Design agreed 2026-10-28 (the split, the lantern toggles per skin). Built the same day: §3 (the seed is a **move**:
the first skin's rows go to their defaults, a copy would have left the plain donkey in the worn skin's colours),
§4, §5 and the self-test "looks per skin" (Dev/SelfTest.PerSkin.cs: a row set per skin, the seed from a crafted
0.12 file, no second seed over the saved file, each skin's look when worn, the shown set following the Skin row, a
late skin caught up, SkinHasRegion). Full F11 2026-10-28 ~03:40: 1346 passed, 0 failed, 0 skipped; the user's own
0.12 cfg (Royal Parade worn) seeded on the first start: "Royal Parade's look took 9 value(s) of donkey's ... and 14
more", the plain donkey at its defaults. On the way: CatchUp counted a late mount twice (fixed), and the hillside
scenario showed a pre-existing fragility (the jump's air speed capped by a ride top measured in an earlier ride; the
test forgets the cap before each jump now).
