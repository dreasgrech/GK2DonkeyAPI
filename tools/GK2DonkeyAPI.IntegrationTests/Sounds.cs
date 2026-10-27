using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;
using UnityEngine.Audio;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.Sounds.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // The flying events (docs/mount-sounds-design.md 3.2) on a flier of the test's own, every event given a game
        // sound: the take-off, the launch's flaps and the beats airborne, the fly loop, the landing, the thud, landed;
        // a jump's own two; a jump plays none of the flight's.
        // The dragon's sound files (the user, 2026-10-24: "make sure that the sounds are structured properly as files ... a
        // solid foundation"; the mount folders the same day): its flight's files are the mount's, in its pack's mount
        // folder's sounds\ (packs/GK2SaddleUpDragonsMountPack, an external pack since 2026-10-26, embedded in its own DLL), for every
        // skin of the dragon; its own look (skins\default\) has none of its own.
        internal static IEnumerator SoundsDragonFiles()
        {
            string noPack = MissingPack("dragon"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Looks.SkinDef red = Looks.Find(Sk("dragon"));
            Catalog.Entry mount = Catalog.Find(M("dragon"));
            if (red == null || mount == null) { Skip("the dragon's pack is not installed (build packs/GK2SaddleUpDragonsMountPack with the game closed)"); yield break; }
            var events = new[] { "takeoff", "flap", "glide", "land" };
            int inMount = mount.Pack != null ? mount.Pack.FileNames.FindAll(f => f.StartsWith(Looks.SoundsKey, StringComparison.OrdinalIgnoreCase)).Count : 0;
            int inSkin = red.FileNames.FindAll(f => f.StartsWith(Looks.SoundsKey, StringComparison.OrdinalIgnoreCase)).Count;
            Check(inMount >= 12 && inSkin == 0 && Looks.DefaultSkinOf(mount) == Sk("dragon"), $"the pack's mount folder: {inMount} sound file(s) in {M("dragon")}\\sounds\\ (the mount's), {inSkin} in its own look's (skins\\default\\); default look {Looks.DefaultSkinOf(mount)}");
            var skinLines = new List<string>();
            foreach (string e in events) if (Looks.SkinSoundBlock(red, Looks.SoundLayer.Both)?.Get(e, null) != null) skinLines.Add(e);
            Check(skinLines.Count == 0, "no flight sound is the red dragon skin's own line" + (skinLines.Count > 0 ? ": " + string.Join(", ", skinLines) : ""));
            var from = new List<string>();
            bool allMount = true;
            foreach (var c in Sounds.Coverage(red, mount))
            {
                if (Array.IndexOf(events, c.Event) < 0) continue;
                from.Add(c.Event + " " + c.From);
                allMount &= c.From == "mount";
            }
            Check(allMount && from.Count == events.Length, "the coverage report: the flight's sounds are the mount's (" + string.Join(", ", from) + ")");
            foreach (string e in events)
            {
                SoundLine l = Looks.MountSoundBlock(mount, Looks.SoundLayer.Both)?.Get(e, null);
                Check(l != null && l.Files != null, $"mount.cfg [Sounds] {e}: {(l?.Files != null ? l.Files.Length + " file(s)" : "no files")}");
            }
            // loaded from the pack's DLL through the cache file, once, under the mount's own name
            SoundLine tl = Looks.MountSoundBlock(mount, Looks.SoundLayer.Both)?.Get("takeoff", null);
            string id = tl != null ? SoundBank.ForFiles(mount.Pack.SoundFiles, tl.BankKey, tl.Files, false, false, tl.Folder) : null;
            yield return Until(() => SoundBank.StateOf(id) == SoundBank.State.Ready, 8f);
            SoundBank.Entry te = SoundBank.Find(id);
            Check(waited && te != null && te.Clips.Length == tl.Files.Length, $"the take-off's files decode from the pack's DLL ({id}: {(te != null ? te.Clips.Length : 0)} clip(s) of {tl?.Files?.Length})");
            string cache = Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "GK2DonkeyAPI", "sounds", mount.Owner, "mount-" + M("dragon"));
            Check(mount.Pack?.SourceAssembly?.GetName().Name == "GK2SaddleUpDragonsMountPack" && Directory.Exists(cache) && File.Exists(Path.Combine(cache, "takeoff_1.ogg")),
                "embedded in GK2SaddleUpDragonsMountPack.dll, its cache files under the mount's own name (" + cache + ")");
        }

        // The snail on a standing wearer: no voice and no lips by its [Sounds] and by the rule (the donkey's own sounds are
        // not another mount's), its steps its own squelch since 2026-10-25 (not the donkey's hoof), through the bind in the
        // rig. From the API's "the donkey unchanged" scenario, step 5, 2026-10-27.
        internal static IEnumerator SnailStandingSounds()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            string noPack = MissingPack("snail");
            if (noPack != null) { Skip(noPack); yield break; }
            bool standingWas = Plugin.StandingSounds.Value;
            MountHandle standing = null;
            try
            {
                SetStandingSounds(true);
                standing = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (standing == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => standing.IsReady && standing.I.SoundsRig != null, 6f);
                Sounds.Rig sr = standing.I.SoundsRig;
                if (sr == null) { Fail("the standing donkey has no sound rig"); yield break; }
                standing.Tuning.Set(Owner, "Skin", Sk("snail"));
                yield return Until(() => standing.Mount == M("snail"), 8f);
                yield return Frames(2);
                sr.Invalidate();
                SoundSpec sv = sr.Resolve("voice"), sl = sr.Resolve("lips"), ss = sr.Resolve("step");
                bool Off(SoundSpec sp) => sp != null && (sp.Silent || sp.Volume < 0.001f);
                bool ownStep = ss != null && !Off(ss) && (ss.Describe() ?? "").Contains(M("snail"));
                Check(waited && Off(sv) && Off(sl) && ownStep,
                    $"a standing snail: no voice and no lips by its [Sounds] and the rule, its step its own squelch (voice {sv?.Describe()}; lips {sl?.Describe()}; step {ss?.Describe()}; {(waited ? "the snail mount on the wearer" : "NOT the snail mount within 8 s")})");
                Check(sr.Emit("voice") == null && sr.Emit("lips") == null && sr.Takes("lips"), "... so nothing plays for them, and the lips event is still taken (silent, not the game's)");
                standing.Tuning.Set(Owner, "Skin", "");
                yield return Until(() => standing.Mount == MountCatalog.DonkeyId, 8f);
            }
            finally
            {
                SetStandingSounds(standingWas);
                try { if (standing != null && standing.Exists) standing.Remove(); } catch { }
            }
        }

        // Saddle Up's StandingSounds row drives the Behaviour key (from the API's "herds and babies" scenario, 2026-10-27).
        internal static IEnumerator SaddleUpStandingSoundsRow()
        {
            var held = new BehaviourHeld("StandingSounds");
            Mounts.Behaviour.ClearAny("StandingSounds"); // the row's own value from here (the run holds the key)
            try
            {
                // Saddle Up's row
                MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
                MountSettingsPage.Row row = saddle?.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "StandingSounds" && x.ShownSection == "Sound");
                if (saddle == null) Info("Saddle Up's page isn't shown: its row not checked");
                else
                {
                    Check(row != null, "Saddle Up has the StandingSounds row in its Sound section");
                    if (row != null)
                    {
                        var re = (BepInEx.Configuration.ConfigEntry<bool>)row.Entry;
                        bool was = re.Value;
                        try
                        {
                            re.Value = false;
                            yield return null;
                            Check(!Plugin.StandingSounds.Value, "the row off: the Behaviour key StandingSounds follows");
                        }
                        finally { re.Value = was; }
                        yield return null;
                    }
                }
            }
            finally { held.Restore(); }
        }
    }
}
