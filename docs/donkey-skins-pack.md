# The donkey's skins as a mod of their own (2026-10-25)

**2026-10-27 (built):** the pack exists as **GK2 Saddle Up: Donkey Skins** (`GK2SaddleUpDonkeySkins.dll` in
`BepInEx\plugins\GK2SaddleUpDonkeySkins\`, `dreas.gk2.saddleup.donkeyskins`, 1.0.0, needs the API 0.40.0; its zip
`GK2-Saddle-Up-Donkey-Skins-1.0.0.zip`), and Saddle Up's built-in content is Gerry alone. Where it differs from the
investigation below: it lives in the repo's `packs/SaddleUpDonkeySkins/` (`gk2mounts/donkey/skins/<skin>/`, no
`mount.cfg`), an external pack like the Dragon and the Critters, not a project under `src/`; its ids carry the pack's
prefix (`donkey/saddleup-donkey-skins.hellfire`, `donkey/saddleup-donkey-skins.royal-parade`) instead of keeping
`saddleup.` (never released, so no player had the old ones, and the API keeps no table of old ids); Hellfire's effect
moved with the skin, as section 4 recommends, and took the pack's prefix too (`saddleup-donkey-skins.hellfire`,
registered by the pack, still shown nowhere); it has a soft dependency on Saddle Up (it loads after it), like the other packs, not a hard one; and
the in-game tests that need Saddle Up or its packs moved out of the API into the dev plugin
`tools/GK2DonkeyAPI.IntegrationTests` (docs/self-tests.md), where "built in or external" checks the skins come from
the pack's DLL, the effect is the pack's, Saddle Up's DLL has no donkey skin, and Saddle Up's Skin row offers both
though they registered after its page was built; the API's own tests use its own test picture. There is no combined
packs zip. Current: docs/making-a-mount.md "Built in or external" and the CHANGELOG.

**2026-10-26 (built in or external):** the dragon, rat, snail and worm are no longer in Saddle Up's DLL but external
packs of their own in the repo's `packs/` (`SaddleUpDragon`, `SaddleUpCritters`), ids by the pack
(`saddleup-dragon.dragon`, `saddleup-critters.rat` ...); Saddle Up keeps Gerry and the donkey's two skins. Paths and ids
below are of their day; docs/making-a-mount.md "Built in or external" is current.

The user: *"let's start investigating to see what it would involve if we decide to take out the donkey skins from
saddle up and instead have them in a separate mod (which has a dependency on saddle up), which is something like GK2
Saddle Up Donkey Skins Pack where once this mod is installed, you get the skins in saddle up. Dont create git repos or
anything yet, lets only investigate it"*.

The skins in question are Saddle Up's two donkey skins, **Hellfire** and **Royal Parade**
(`src/SaddleUp/assets/mounts/donkey/skins/saddleup.hellfire/` and `saddleup.royal-parade/`, 45 files each, 260 KB of
Saddle Up's 1.8 MB of packs). The five mounts (dragon, rat, snail, worm, Gerry) stay in Saddle Up. This document is
the investigation: the code was read, not assumed, and nothing was changed while writing it. It follows
[mounts-as-mods.md](mounts-as-mods.md) (2026-10-20), which asked the same question for the mounts and whose
preconditions were all built that evening.

## 1. The verdict

**Yes, and the runtime needs no change.** A donkey skin is already a pack the API reads from any mod's DLL in the
same way. Saddle Up's Awake makes one call, `Mounts.Looks.RegisterEmbedded(Guid, assembly, "GK2SaddleUp.mounts.")`,
and the two donkey skins come in through it next to the five mounts. They are a mount folder with no `mount.cfg`
(`donkey\skins\<skin>\`), which the loader already treats as "skins added to a mount registered elsewhere". A new
DLL with only those two folders embedded, making the same call under its own prefix, gives the player exactly what
they have today. No API change: no new public member, no ApiSurface change, no version bump needed for the move.

The work is all around the runtime:
- a new project;
- Hellfire's effect, which today is registered in Saddle Up's code;
- the self-tests, whose sample picture comes from Royal Parade inside `GK2SaddleUp.dll`;
- the release scripts, which know two DLLs;
- wording in about a dozen files.

## 2. Why the runtime is ready (checked)

- **The loader:**
  - `Looks.RegisterEmbedded` (Looks.cs:446) groups any assembly's resources by `<prefix><mount>\skins\<skin>\<file>`.
  - A `donkey\` folder without `mount.cfg` joins the game's donkey.
  - The public `MountLooks.RegisterEmbedded` (Api/MountLooks.cs:144) is the same call Saddle Up makes. It has no
    Saddle Up special case, and it passes `builtIn: false` for everyone.
- **The ids can stay `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`.**
  - The rule for a skin added to a mount another mod owns (`Looks.Register`, `AddsToOthers`) only requires the form
    `<mod>.<name>`. Nothing compares the prefix with the registering plugin's GUID (`Looks.FirstParty`, which looks
    at owners, is not called anywhere).
  - So a pack under the GUID `dreas.gk2.saddleup.donkeyskins` keeps the `saddleup.` prefix. That is honest anyway:
    it is the Saddle Up family's pack.
  - Every saved choice, cfg row, test alias (`Sk("hellfire")`) and the dev worlds' side files stay valid.
- **The order of loading:**
  - With a hard dependency on Saddle Up, BepInEx runs the pack's Awake *after* Saddle Up's, so the skins arrive
    after Saddle Up has built its page. This is the case the 2026-10-20 fix covers (mounts-as-mods.md 10):
    - `Publish` hands the page to the menus on the next frame, after every Awake;
    - the donkey's Skin row reads its choices through a function (`Looks.SkinChoicesOf`, MountSettingsPage.cs:468)
      when the menu is filled;
    - the Mounts panel reads lazily.
  - So both skins are in the donkey's Skin list whatever the order.
  - One gap in the proof: the self-test "settings: the Mount, Skin and babies rows" registers a late *mount*, not a
    late *skin of an existing mount*. A late donkey skin is covered by construction but has never been run (see 7).
- **Saddle Up's settings hold nothing per skin:**
  - The colours are per mount (`Coat`, `Muzzle`...: the donkey's rows have no suffix).
  - The skin choice is the cfg row `Skin_donkey` and the side file's `Skin`, both holding the full id.
- **Without the pack, the 2026-10-20 behaviour applies** (mounts-as-mods.md 3 and 10):
  - the donkey's Skin list offers Default only;
  - a donkey that wore Hellfire shows the game's donkey;
  - the choice is kept, and the log says it with an Info line, not a warning;
  - the skin comes back when the pack is installed again.
- **Compatibility:**
  - No released version has a skin on: `Debug.MountsPreview` keeps every registration out for players.
  - The 0.10.0 fixtures name Hellfire only in description text: saddleup.cfg lines 44, 67, 77 and the API cfg's
    retired `[Effects]` line.
  - So nothing a player has changes, and the 0.9.1/0.10.0 upgrade tests are unaffected.
  - As with the mounts in 2026-10-20: free now, not free after the release that switches skins on.

## 3. What the mod would be

| | |
|---|---|
| Name | GK2 Saddle Up Donkey Skins Pack (the user's working name) |
| GUID | `dreas.gk2.saddleup.donkeyskins` (the `dreas.gk2.<feature>` rule) |
| Assembly / folder | `GK2SaddleUpDonkeySkins.dll` in `BepInEx\plugins\GK2SaddleUpDonkeySkins\` |
| Project | `src/SaddleUpDonkeySkins/` in this repo (why `src/`: section 5) |
| Content | `assets/mounts/donkey/skins/saddleup.hellfire/` and `saddleup.royal-parade/`, moved (not copied) from Saddle Up |
| csproj | Saddle Up's, cut down: the `EmbeddedResource` line with `LogicalName="GK2SaddleUpDonkeySkins.mounts.%(RecursiveDir)%(Filename)%(Extension)"`, the dev-only `GK2MountsSourceDir` attribute (the F11 Mounts tab saves tuned values back into the repo's `skin.cfg` through it, `Looks.CfgPathFor`), the API project reference, `BepInEx.dll`, `DeployToGame.targets` |
| Awake | the API version check (as Saddle Up's `NeedsApi`), `Mounts.Looks.RegisterEmbedded(Guid, Assembly.GetExecutingAssembly(), "GK2SaddleUpDonkeySkins.mounts.")`, one log line; and Hellfire's effect if it moves (section 4) |
| Dependencies | `[BepInDependency("dreas.gk2.donkeyapi", "0.36.0")]` and `[BepInDependency("dreas.gk2.saddleup", "<the Saddle Up version without the skins>")]` |
| Game references | none; the effect, if it moves, names the game's particles by string, as Saddle Up does today. So, like Saddle Up, no dependency manifest, and `check-dist.py`'s "references no game assembly" rule holds. Without the effect it needs no `UnityEngine` reference either. |

**About the dependency on Saddle Up:**
- The skins are the API's: the registration needs only the API, and once registered any mod's donkey could wear
  them, not only Saddle Up's.
- The Saddle Up dependency is the product promise ("you get the skins in Saddle Up"). BepInEx enforces it: without
  Saddle Up the pack is skipped with BepInEx's own missing-dependency line, which is clear to the player.
- The minimum version should be the first Saddle Up without the two skins. A dev who pairs the pack with a Saddle Up
  that still embeds them gets the API's version rule, not a crash: the same id from two DLLs, the higher skin
  `version` wins, a tie keeps the first, and either way the log has one warning.

**Mods menu:** the pack has no settings, so it has no page. Whether GK2 Mod Framework lists a plugin with no page
was not checked; the mod would show in BepInEx's log and in mod managers in any case.

## 4. The decision inside it: Hellfire's effect

`RegisterHellfire()` (src/SaddleUp/Plugin.cs:42) registers the effect `hellfire`:
- the game's lamp flame on a ring of six;
- the sparks on three of those spots;
- a trail.

Nothing shows it in this version (2026-10-25: selection removed). The open design is how a skin comes with its
effect (open-items.md "Effects on skins").

- **Recommended: the effect moves with the skin.** The pack then holds everything Hellfire is, and the coming design
  ("the skin consumer" uses the API's effect) has the skin and its effect in one mod. Cost: the effect code (15
  lines) moves, the pack references `UnityEngine.CoreModule` for nothing but compile-time types (to check), and
  `tests/SkinsScan` stops reading the shipped effect ids from `src/SaddleUp/Plugin.cs` (Program.cs:63-65) and reads
  them from the pack's Plugin.cs, or from every `src/*/Plugin.cs`.
- Or it stays in Saddle Up: an effect the player can't get without the pack, registered by a mod that no longer has
  its skin.

## 5. What moves, file by file

**Assets and project:**
- `src/SaddleUp/assets/mounts/donkey/` → `src/SaddleUpDonkeySkins/assets/mounts/donkey/` (git mv; Saddle Up's
  `donkey\` folder is then gone).
- A new csproj and Plugin.cs as in section 3.
- `SaddleUp.csproj`: the header comment only (the embed line `assets\mounts\**` simply finds no `donkey\` any more).

**Saddle Up's code and texts:**
- `src/SaddleUp/Plugin.cs`:
  - the header comment and line 16 ("the donkey's Hellfire and Royal Parade under donkey\skins\");
  - the log line at 67;
  - `RegisterHellfire` (if it moves, section 4);
  - the mounts-off Info row's text at 129 ("the Hellfire skin" is no longer Saddle Up's).
- `src/SaddleUp/README.md`: the Skin row (48) and the Second tone row's "Hellfire's lava" (57; still true with the
  pack installed).

**The tools:**
- Nothing that walks the packs changes, provided the project sits under `src/`:
  - `tools/skins/skinroots.py` `roots()` globs `src/*/assets/mounts`;
  - `tests/SkinsScan` does the same (Program.cs:19);
  - `tools/release-scans.sh` checks every skin through them.
- Under `examples/` or a `mods/` folder, all three need a new root.
- What names Saddle Up's folder directly:
  - `tools/skins/hellfire/make_hellfire.py:28` (`ASSETS`);
  - `tools/skins/royal-parade/make_royal.py` (lines 4 and its target);
  - comments in `skinroots.py:3-4`, `new_skin.py`, `skin_preview_html.py`, `skin_colors.py` and `release-scans.sh:37`.
- `tools/page-carryover.py` reads Saddle Up's page; it is unaffected (no row moves).

**The self-tests** (the API's Dev code; the in-game suite):
- **`TempPicture()` (SelfTest.cs:2939, 3 callers).** It feeds every temporary pack the suite writes: about 24
  `WriteTempPack` scenarios, plus `SkinPack` (3836-3850). It reads Royal Parade's `bdy_static_left.png` out of
  `GK2SaddleUp.dll` (`SkinsAssembly()`, 2892). After the move that resource is gone, the stream is null, and all of
  those scenarios fail. Two routes:
  - **Recommended:** read it from the API's own test pack, `src/GK2DonkeyAPI/assets/testmounts/donkey/skins/selftest.embedded-sound-test/bdy_static_left.png`,
    already embedded in the API DLL. The pack scenarios then depend on no mod at all.
  - Or point `SkinsAssembly()` at the new DLL.
  - `SkinPack` also takes `bdy_walk_left_01.png` and `hed_static_down.png` from Royal Parade; the test pack has one
    picture only, so it would write the same picture three times, or the test pack gains two pictures (to look at
    while building).
- **The scenarios that wear the two skins:**
  - the seat readout (the list at 1572);
  - `SkinEffect` and `EffectOnOff` (`Sk("hellfire")`, the ring 0.80 on Hellfire);
  - `OverBuiltIns` (Hellfire whole with its erase marks, Royal Parade's reds);
  - Layering (d) (SelfTest.Layering.cs:298);
  - `MountsOff` (2830: no Hellfire skin or effect with the mounts off; stays true).
  - They need the pack deployed in the dev game: its csproj deploys like Saddle Up's on every dev build (`Deploy`
    defaults to on, unlike the examples).
  - Whether each one skips or fails cleanly when the pack is missing was not read line by line. Worth one look
    while building, so a player-like install (no pack) gives skips with a reason, not failures.
- **`SkinsAssembly()` and the temp pack's comment** (3836: "every skin is Saddle Up's since 2026-10-21"): updated.

**The release:**
- `tools/build-dist.sh`: a third `build` line.
- `tools/check-dist.py`: a `NO_MANIFEST` entry for `GK2SaddleUpDonkeySkins.dll` (it checks every DLL in `dist/`, and
  a DLL without a manifest needs a stated reason).
- `tools/make-release-zip.py`: a second zip, `GK2-Saddle-Up-Donkey-Skins-<version>.zip` with
  `BepInEx/plugins/GK2SaddleUpDonkeySkins/`. Its version is read from the pack's Plugin.cs; it gets the same
  read-back check.
- `docs/release-checklist.md`: the pack's steps, its own Nexus page and the requirements Saddle Up + API.
- `dist/README.md` and `release/`: at the release.
- The first public release of the pack can only be with the release that turns `Debug.MountsPreview` off. Before
  that, the API keeps the pack's registration out for players (`Mounts.KeptOut`), exactly as it keeps Saddle Up's
  own skins out today.

**The docs:**
- Where "Saddle Up ships Hellfire and Royal Parade" is said:
  - README.md;
  - `src/GK2DonkeyAPI/README.md:210` (the skins table);
  - skins.md "Worked examples";
  - skins-inventory.md;
  - making-a-mount.md: "Building one from this repo"; its "Shape 1" covers a pack in a DLL, and a skins-only DLL is
    that shape with no `mount.cfg`, worth one sentence;
  - mounts-as-mods.md (an amendment line);
  - sprite-tools.md (the generators' targets);
  - api.md where Hellfire is the example;
  - self-tests.md.
- CHANGELOG Unreleased: For players (the skins come as their own download), For mod authors (Saddle Up's own skins
  are the worked example of a skins-only pack), Development.

## 6. What does not change

- The API's runtime code and public surface: Looks.cs, Catalog.cs, MountLooks.cs, the effect engine, Persistence,
  the settings page.
- Saddle Up's settings and its cfg keys; the skin ids; the side files.
- The page-carryover tool; the upgrade fixtures.

## 7. Open points and decisions for the user

1. **Hellfire's effect: with the skin (recommended) or left in Saddle Up** (section 4).
2. **Where the project lives:** `src/SaddleUpDonkeySkins/` in this repo (recommended: the tools find it as they are,
   and it shares the build, the deploy and the self-tests), or a repo of its own (every tool copied). You said no
   new repo yet; this repo needs none.
3. **The dependency's strength:** a hard `BepInDependency` on Saddle Up (recommended: it is what "you get the skins in
   Saddle Up" means, and the player gets BepInEx's clear message without it), or only the API (the skins would then
   also serve any other mod's donkey without Saddle Up installed).
4. **The name and the GUID:** "GK2 Saddle Up Donkey Skins Pack", `dreas.gk2.saddleup.donkeyskins`,
   `GK2SaddleUpDonkeySkins.dll` (proposed), and whether the skins keep their `saddleup.` prefix (recommended: yes, ids
   never change).
5. **One proof to add when building:**
   - a self-test that registers a skin for an existing mount after a page is built, and finds it in that mount's Skin
     list (the late-skin case of section 2);
   - and the pack's absence checked by hand once: the donkey's Skin list shows Default only, a saved Hellfire choice
     is kept, the log has the Info line and no warning.
6. **Release timing:** the pack is inert for players until the release that switches the mounts and skins on, so it
   ships with that release or after it, with its own zip and Nexus page.

Estimated size of the move:
- one new project of two files;
- one folder moved;
- about 15 lines in the self-tests (the sample picture);
- three release scripts;
- two generator paths;
- wording in about a dozen docs.

Then one F11 run and the release scans. No API version bump is needed, unless Hellfire's effect move changes what
Saddle Up registers (it does not change the API).
