using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.Audio;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The sound system's scenarios (docs/mount-sounds-design.md). The probe: a clip of the mod's own, registered as a
    // game sound (SoundBank), plays through the game's own player with its 3D preset and mixer group. The donkey
    // unchanged: every event of the rig resolves to the player's cfg keys as before, standing ones and foals sound
    // under the standing switch, a mount's pins still silence (the bind in the rig).
    internal static partial class SelfTest
    {
        internal const string GroupSounds = "Sounds";

        // The pooled player behind a handler (a private field of the game's; Access.Verify requires it).
        internal static readonly AccessTools.FieldRef<SoundHandler, SoundController> HandlerSource = AccessTools.FieldRefAccess<SoundHandler, SoundController>("sourceHolder");

        // The standing switch for a test: the test's own Behaviour override (whoever held it, a mod's settings page, has
        // its value back at the run's end: Run holds the key), so the switch is the test's whatever a mod set.
        internal static void SetStandingSounds(bool on)
        {
            Mounts.Behaviour.ClearAny("StandingSounds");
            Mounts.Behaviour.Set(Owner, "StandingSounds", on);
        }

        internal static string ClipOf(SoundHandler h)
        {
            try
            {
                SoundController sc = h != null ? HandlerSource(h) : null;
                return sc != null && sc.audioSource != null && sc.audioSource.clip != null ? sc.audioSource.clip.name : "-";
            }
            catch
            {
                return "-";
            }
        }

        internal static IEnumerator SoundsProbe()
        {
            const string id = "gk2.selftest.probe";
            string tempRoot = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-sounds");
            var clips = new List<AudioClip>();
            try
            {
                AudioConfig cfg = SoundBank.Config;
                if (cfg == null || cfg.sounds == null || !LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }

                // 1. the presets the game copies onto its players
                List<SoundBank.Preset> presets = SoundBank.Presets(log: true);
                SoundBank.Preset d3 = presets.Find(p => p.Type == AudioSettings3DType.Default3D.value);
                Check(d3 != null && d3.SpatialBlend > 0.5f, d3 != null ? $"the Default3D preset is 3D: spatial blend {d3.SpatialBlend:0.00}, distance {d3.MinDistance:0.#}..{d3.MaxDistance:0.#}, rolloff {d3.Rolloff} ({presets.Count} presets)" : $"no Default3D preset among {presets.Count}");
                string binding = SoundBank.ConfigBinding();
                Check(binding.StartsWith("LazyAudio plays from the AudioConfig we add to"), binding);

                // 2. a tone from WAV bytes, and the same bytes through a file and Unity's decoder
                byte[] wav = SoundBank.TestTone(0.5f, 440f);
                AudioClip a = SoundBank.FromWav(wav, "probe-tone");
                clips.Add(a);
                Check(a != null && Mathf.Abs(a.length - 0.5f) < 0.02f && a.channels == 1, $"WAV bytes parsed: {(a != null ? a.length.ToString("0.00") : "-")} s, {a?.channels} channel(s), {a?.frequency} Hz");
                Directory.CreateDirectory(tempRoot);
                string file = Path.Combine(tempRoot, "tone.wav");
                File.WriteAllBytes(file, wav);
                AudioClip b = null;
                string err = null;
                bool done = false;
                SoundBank.LoadFile(file, (c, e) => { b = c; err = e; done = true; });
                yield return Until(() => done, 5f);
                if (b != null) clips.Add(b);
                Check(waited && b != null && Mathf.Abs(b.length - 0.5f) < 0.02f, $"the file loaded through the web request: {(b != null ? b.length.ToString("0.00") + " s, " + b.frequency + " Hz" : err ?? "timed out")}");

                // 3. registered as a game sound
                int before = cfg.sounds.Count;
                SoundBank.Register(id, clips, Owner);
                Check(cfg.Get(id) != null && cfg.sounds.Count == before + 1, $"registered: AudioConfig.Get finds it, the list {before} -> {cfg.sounds.Count}");

                // 4. played through the game's player at the keeper
                Transform at = PC.transform;
                SoundHandler h = Sounds.PlayId(id, at, 1f, 1f);
                Check(h != null && h.IsActive, "PlayAtGameObject returned a live handler");
                yield return null;
                SoundController sc = h != null ? HandlerSource(h) : null;
                AudioSource src = sc != null ? sc.audioSource : null;
                AudioMixerGroup want = SoundBank.Group(false);
                Check(src != null && src.clip != null && clips.Contains(src.clip), $"the pooled player has our clip ({(src != null && src.clip != null ? src.clip.name : "-")})");
                Check(src != null && want != null && src.outputAudioMixerGroup == want, $"its mixer group is the donkey's own in-world group ({(src != null && src.outputAudioMixerGroup != null ? src.outputAudioMixerGroup.name : "none")}; wanted {(want != null ? want.name : "none")})");
                Check(src != null && d3 != null && Mathf.Abs(src.spatialBlend - d3.SpatialBlend) < 0.01f && Mathf.Abs(src.maxDistance - d3.MaxDistance) < 0.01f, $"the Default3D preset was applied (blend {(src != null ? src.spatialBlend.ToString("0.00") : "-")}, max distance {(src != null ? src.maxDistance.ToString("0.#") : "-")})");
                float gap = sc != null ? Vector3.Distance(sc.transform.position, at.position) : -1f;
                Check(sc != null && gap < 0.1f, $"the player sits at the target ({gap:0.00} away)");
                yield return Until(() => h == null || !h.IsActive, 2f);
                Check(waited, "it ended by itself (the handler inactive within 2 s)");

                // 5. unregistered: gone from the list, the clips destroyed
                SoundBank.Unregister(id);
                Check(cfg.Get(id) == null && cfg.sounds.Count == before, $"unregistered: gone from the list ({cfg.sounds.Count} sounds again)");
                yield return null;
                Check(a == null && b == null, "the clips are destroyed");
            }
            finally
            {
                try { SoundBank.Unregister(id); } catch { }
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // The rig per instance plays the donkey as the static code did: every event resolves to the cfg keys of the
        // style (ids, volumes, pitches, starts, the numbers), the API's bray and bell still play the game's sounds, a
        // standing one and a foal sound under the standing switch, the lips event is taken only when the rig plays it,
        // and a mount's pins silence a standing wearer too (the bind inside the rig).
        internal static IEnumerator SoundsDonkeyUnchanged()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            MountHandle h = null, cartH = null, standing = null, foal = null;
            bool standingWas = Plugin.StandingSounds.Value;
            bool soundsWas = Plugin.For(MountStyle.Ride).SoundsOn.Value;
            try
            {
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(3);
                Mount.Instance i = Mount.Ridden;
                Sounds.Rig rig = i?.SoundsRig;
                if (rig == null) { Fail("the ridden instance has no sound rig"); yield break; }

                // 1. the base layer is the cfg of the style, event by event
                int mapped = CheckBase(rig, Plugin.For(MountStyle.Ride), MountStyle.Ride);
                Check(mapped == 11, $"riding plain: {mapped} of 11 events with a base resolve to the [Ride] keys");

                // 2. the API's bray and bell play the game's own sounds, the step a step of the ground
                SoundHandler bray = rig.Emit("voice");
                string brayClip = ClipOf(bray);
                Check(bray != null && brayClip.StartsWith("v_donkey", StringComparison.Ordinal), $"Emit(voice) plays the donkey's dialogue voice ({brayClip})");
                yield return Until(() => bray == null || !bray.IsActive, 2f);
                SoundHandler bell = rig.Emit("bell");
                string bellClip = ClipOf(bell);
                Check(bell != null && bellClip.IndexOf("bell", StringComparison.OrdinalIgnoreCase) >= 0, $"Emit(bell) plays the bell ({bellClip})");
                if (bell != null) bell.Stop();
                SoundHandler step = rig.Emit("step");
                string stepClip = ClipOf(step);
                Check(step != null, $"Emit(step) plays the game's step sound for the ground ({stepClip}; {rig.LastEvent})");
                Check(rig.Takes("lips"), "the lips animation event is taken by the rig (played at the player's volume)");
                Plugin.For(MountStyle.Ride).SoundsOn.Value = false;
                rig.Invalidate();
                Check(!rig.Takes("lips") && rig.Emit("voice") == null, "sounds off: the lips event is handed back to the game and nothing is emitted");
                Plugin.For(MountStyle.Ride).SoundsOn.Value = soundsWas;
                rig.Invalidate();

                // 3. with the cart: the cart style's keys
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 4f);
                cartH = Spawn(Spot(home, 4f) ?? home, right: homeFacing > 0f, withCart: true);
                if (cartH != null)
                {
                    yield return Until(() => cartH.IsReady, 6f);
                    MountResult rc = cartH.Ride(showMessage: false);
                    if (rc.Ok)
                    {
                        yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart, 5f);
                        yield return Frames(3);
                        Sounds.Rig cartRig = Mount.Ridden?.SoundsRig;
                        int mappedCart = cartRig != null ? CheckBase(cartRig, Plugin.For(MountStyle.Cart), MountStyle.Cart) : 0;
                        Check(mappedCart == 11, $"with the cart: {mappedCart} of 11 events resolve to the [Cart] keys");
                        Mounts.Dismount("self-test");
                        yield return Until(() => !Mounts.IsMounted, 4f);
                    }
                    else Warn($"the cart was not ridden ({Said(rc)}): the cart style's keys not checked");
                }

                // 4. a standing one: takes its lips, grazes, under the standing switch; a foal is higher and softer
                standing = h;
                yield return Frames(2);
                Sounds.Rig sr = standing.I.SoundsRig;
                SetStandingSounds(true);
                sr.Invalidate();
                Check(standing.I.Donkey != null && sr.Takes("lips"), "a standing donkey near the keeper takes its lips event (StandingSounds on)");
                SoundHandler graze = sr.Emit("graze");
                Check(graze != null && graze.IsActive, $"a standing donkey's graze plays ({ClipOf(graze)})");
                sr.Stop("graze");
                yield return null; // the game's player resets a stopped sound on its next update, and the handler goes inactive then
                Check(graze == null || !graze.IsActive, "Stop(graze) cuts it (the handler inactive a frame later)");
                SoundHandler unhitchStanding = sr.Emit("hitch");
                Check(unhitchStanding != null, "the hitch moment plays standing (not gated by the standing switch)");
                SetStandingSounds(false);
                sr.Invalidate();
                Check(!sr.Takes("lips") && sr.Emit("graze") == null, "StandingSounds off: the lips event goes back to the game and nothing standing is emitted");
                Check(sr.Emit("hitch") != null, "... but the hitch moment still plays (as before the rig)");
                SetStandingSounds(true);
                sr.Invalidate();
                foal = Mounts.SpawnFoal(Owner, standing);
                if (foal != null)
                {
                    yield return Until(() => foal.IsReady, 8f);
                    yield return Frames(2);
                    Sounds.Rig fr = foal.I.SoundsRig;
                    Check(fr.PitchScale > 1.2f && fr.VolumeScale < 0.9f, $"a foal (scale {foal.I.ViewScale:0.00}) is pitched up x{fr.PitchScale:0.00} and softer x{fr.VolumeScale:0.00}");
                    SoundHandler fs = fr.Emit("step");
                    Check(fs != null, $"the foal's step plays ({ClipOf(fs)})");
                }
                else Info("no foal could be spawned here: the foal's scaling not seen");
            }
            finally
            {
                SetStandingSounds(standingWas);
                Plugin.For(MountStyle.Ride).SoundsOn.Value = soundsWas;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (foal != null && foal.Exists) foal.Remove(); } catch { }
                try { if (cartH != null && cartH.Exists) cartH.Remove(); } catch { }
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }

        // A pack's own sounds (docs/mount-sounds-design.md section 4): [Sounds] lines, the sounds\ folder's files loaded
        // as game sounds, the convention name, the layers (the skin's over the mount's over the player's keys, a style
        // block over both), the player's switches on top, a second skin of the mount, the save round trip, the clean-up.
        internal static IEnumerator SoundsPack()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".sounds";
            string tempRoot = null, packDir = null;
            MountHandle h = null, hc = null;
            int bankBefore = SoundBank.Count;
            bool soundsWas = Plugin.For(MountStyle.Ride).SoundsOn.Value;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-soundpack", Looks.DefaultSkinName,
                    "# a test pack\n[Skin]\nformat = 1\nname = Sounds Test\nmount = selftest.sounds-beast\n\n" +
                    "[Sounds]\n# keep: the voice\nvoice = voice_1.wav, voice_2.wav\nvoice.volume = 0.5\nstep.grass = step_grass\nlips = none\nbogus = x.wav\n\n" +
                    "[Sounds.Cart]\nvoice = voice_cart.wav\n\n" +
                    "[Mount]\nid = selftest.sounds-beast\nname = Sounds Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n\n" +
                    "[Mount.Sounds]\nvoice = mount_voice.wav\nbell.volume = 0.25\nhitch = voice_1.wav\n");
                packDir = TempSkinDir(tempRoot, "selftest.sounds-beast", Looks.DefaultSkinName);
                string sdir = Path.Combine(packDir, "sounds");
                Directory.CreateDirectory(sdir);
                File.WriteAllBytes(Path.Combine(sdir, "voice_1.wav"), SoundBank.TestTone(0.3f, 440f));
                File.WriteAllBytes(Path.Combine(sdir, "voice_2.wav"), SoundBank.TestTone(0.3f, 520f));
                File.WriteAllBytes(Path.Combine(sdir, "voice_cart.wav"), SoundBank.TestTone(0.3f, 330f));
                // the mount's files: the mount folder's own sounds\ (a line's, and one by its name); voice_1.wav is the
                // skin's only, so the mount's hitch line naming it finds no file there
                string mdir = Path.Combine(TempMountDir(tempRoot, "selftest.sounds-beast"), "sounds");
                Directory.CreateDirectory(mdir);
                File.WriteAllBytes(Path.Combine(mdir, "mount_voice.wav"), SoundBank.TestTone(0.3f, 220f));
                File.WriteAllBytes(Path.Combine(mdir, "stop_1.wav"), SoundBank.TestTone(0.3f, 260f));
                File.WriteAllBytes(Path.Combine(sdir, "graze_1.wav"), SoundBank.TestTone(0.6f, 180f)); // the convention: no cfg line
                string pack2 = TempSkinDir(tempRoot, "selftest.sounds-beast", "sounds-test-2");
                Directory.CreateDirectory(pack2);
                File.Copy(Path.Combine(packDir, "bdy_static_left.png"), Path.Combine(pack2, "bdy_static_left.png"));
                File.WriteAllText(Path.Combine(pack2, "skin.cfg"), "[Skin]\nformat = 1\nname = Sounds Test Two\nmount = selftest.sounds-beast\n");
            }
            catch (Exception e) { Warn("the temp packs could not be written: " + e.Message); yield break; }
            try
            {
                int n = Looks.RegisterFolder(tempRoot, owner);
                Check(n == 2, $"two packs found ({n})");
                Looks.SkinDef def = Looks.Find("selftest.sounds-beast/default");
                Check(def != null && def.Report.Contains("sound line(s)") && def.Report.Contains("no sound event 'bogus'"), "the loader counts the sound lines and names the bogus event: " + (def?.Report ?? "-"));
                Catalog.Entry beast = Catalog.Find("selftest.sounds-beast");
                Check(def != null && def.FileNames.Contains("sounds/voice_1.wav") && !def.FileNames.Contains("sounds/mount_voice.wav") && beast?.Pack != null && beast.Pack.FileNames.Contains("sounds/mount_voice.wav") && beast.Pack.FileNames.Contains("sounds/stop_1.wav"),
                    "the files: the skin's sounds\\ its own, the mount folder's sounds\\ the mount's");
                Check(beast?.Pack != null && beast.Pack.Report.Contains("[Sounds] hitch: the pack has no sounds\\voice_1.wav"), "a mount.cfg line reads the mount folder's sounds\\ only: hitch = voice_1.wav (a file of the skin's) is named and left out: " + beast?.Pack?.Report);
                Check(def != null && Looks.SkinSoundBlock(def, Looks.SoundLayer.Both)?.Get("stop", null) == null, "the skin's name rule reads its own sounds\\ only: the mount's stop_1.wav is no line of the skin's");
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null || def == null) { Fail("could not spawn, or the pack is missing"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "selftest.sounds-beast/default");
                yield return Until(() => h.Mount == "selftest.sounds-beast", 8f);
                Check(waited, "the donkey wears the pack's skin and is its mount");
                Sounds.Rig rig = h.I.SoundsRig;
                rig.Invalidate();

                // the layers
                SoundSpec v = rig.Resolve("voice");
                Check(v != null && v.Layer == "skin" && v.BankId != null && v.BankId.EndsWith("sounds:voice", StringComparison.Ordinal), $"voice resolves to the skin's files ({v?.Describe()})");
                string voiceId = v?.BankId;
                yield return Until(() => SoundBank.StateOf(voiceId) == SoundBank.State.Ready, 6f);
                SoundBank.Entry ve = SoundBank.Find(voiceId);
                Check(waited && ve != null && ve.Clips.Length == 2, $"its two files are loaded as one game sound with two samples ({(ve != null ? ve.Clips.Length : 0)})");
                rig.Invalidate();
                v = rig.Resolve("voice");
                float playerVol = Plugin.For(MountStyle.Ride).BrayVolume.Value;
                Check(v != null && !v.Loading && Mathf.Abs(v.Volume - 0.5f * playerVol) < 0.001f, $"voice.volume 0.5 x the player's BrayVolume {playerVol:0.00} = {(v != null ? v.Volume.ToString("0.00") : "-")}");
                SoundSpec sg = rig.Resolve("step", "grass"), sd = rig.Resolve("step", "dirt");
                Check(sg != null && sg.GameId == "step_grass" && sg.Layer == "skin", $"step.grass: the skin's game id ({sg?.Describe()})");
                Check(sd != null && sd.Layer == "base", $"step.dirt: the base's ({sd?.Describe()})");
                SoundSpec sl = rig.Resolve("lips");
                Check(sl != null && sl.Silent && sl.Layer == "skin" && rig.Emit("lips") == null && rig.Takes("lips"), "lips = none: silent, the event still taken");
                SoundSpec sgr = rig.Resolve("graze");
                Check(sgr != null && sgr.Layer == "skin" && sgr.BankId != null && sgr.BankId.EndsWith("sounds:graze", StringComparison.Ordinal), $"graze_1.wav by convention, no cfg line ({sgr?.Describe()})");
                SoundSpec st0 = rig.Resolve("stop"), hi0 = rig.Resolve("hitch");
                Check(st0 != null && st0.Layer == "mount" && st0.BankId != null && st0.BankId.EndsWith("|mount-selftest.sounds-beast|sounds:stop", StringComparison.Ordinal), $"the mount folder's stop_1.wav by the name rule: the mount's stop ({st0?.Describe()})");
                Check(hi0 != null && hi0.Layer == "base", $"the mount's hitch line with no file in its sounds\\ falls through to the base ({hi0?.Describe()})");
                SoundSpec sb = rig.Resolve("bell");
                float bellVol = Plugin.For(MountStyle.Ride).BellVolume.Value;
                Check(sb != null && sb.Layer == "base" && Mathf.Abs(sb.Volume - 0.25f * bellVol) < 0.001f, $"bell.volume 0.25 from [Mount.Sounds] scales the base's bell, the source stays the base's ({sb?.Describe()})");

                // played, and the player's switch on top
                SoundHandler hv = rig.Emit("voice");
                SoundController hsc = hv != null ? HandlerSource(hv) : null;
                Check(hv != null && ve != null && hsc != null && hsc.audioSource != null && Array.IndexOf(ve.Clips, hsc.audioSource.clip) >= 0, $"Emit(voice) plays one of the pack's clips ({ClipOf(hv)})");
                Plugin.For(MountStyle.Ride).SoundsOn.Value = false;
                rig.Invalidate();
                Check(rig.Emit("voice") == null, "SoundsOn off silences the pack's voice");
                Plugin.For(MountStyle.Ride).SoundsOn.Value = soundsWas;
                rig.Invalidate();

                // the style block: with the cart, [Sounds.Cart] voice wins
                hc = Spawn(Spot(home, 4.5f) ?? home, right: homeFacing > 0f, withCart: true);
                if (hc != null)
                {
                    yield return Until(() => hc.IsReady, 6f);
                    hc.Tuning.Set(Owner, "Skin", "selftest.sounds-beast/default");
                    yield return Until(() => hc.Mount == "selftest.sounds-beast", 8f);
                    hc.I.SoundsRig.Invalidate();
                    SoundSpec vc = hc.I.SoundsRig.Resolve("voice");
                    Check(vc != null && vc.Layer == "skin" && vc.BankId != null && vc.BankId.EndsWith("sounds.cart:voice", StringComparison.Ordinal), $"with the cart: [Sounds.Cart] voice wins ({vc?.Describe()})");
                    hc.Remove();
                    hc = null;
                }

                // a second skin of the mount, no [Sounds]: built on the mount's own look (its default), it takes the default's
                // lines before the mount's (docs/skin-layering-plan.md 2.5): the default's voice, the same bank entry (loaded once)
                h.Tuning.Set(Owner, "Skin", "selftest.sounds-beast/sounds-test-2");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "selftest.sounds-beast/sounds-test-2", 8f);
                rig.Invalidate();
                SoundSpec v2 = rig.Resolve("voice");
                Check(v2 != null && v2.Layer == "default" && v2.BankId != null && v2.BankId == voiceId && SoundBank.StateOf(v2.BankId) == SoundBank.State.Ready, $"the second skin (no [Sounds]) gets its default's voice, the default's bank entry: loaded once ({v2?.Describe()})");
                SoundSpec st2 = rig.Resolve("stop");
                Check(st2 != null && st2.Layer == "mount" && st0 != null && st2.BankId == st0.BankId, $"and the mount's stop by the name rule (the default has none), the same bank entry as under the default: loaded once ({st2?.Describe()})");
                // the default's lips = none and graze by convention reach it; the donkey's own snort, which no layer gives, is
                // silent on another creature; the neutral ones stay the donkey's
                SoundSpec l2 = rig.Resolve("lips"), g2 = rig.Resolve("graze"), m2 = rig.Resolve("mount"), sd2 = rig.Resolve("step", "dirt"), b2 = rig.Resolve("bell");
                Check(l2 != null && l2.Silent && l2.Layer == "default" && g2 != null && !g2.Silent && g2.Layer == "default" && sgr != null && g2.BankId == sgr.BankId && m2 != null && m2.Silent,
                    $"the default's lips = none and graze_1.wav reach it (layer default), the getting-on snort no layer gives is silent (lips {l2?.Describe()}; graze {g2?.Describe()})");
                Check(sd2 != null && !sd2.Silent && sd2.Layer == "base" && b2 != null && !b2.Silent, $"... while the step by the ground and the bell stay the donkey's ({sd2?.Describe()}; {b2?.Describe()})");
                var cover = Sounds.Coverage(Looks.Find("selftest.sounds-beast/sounds-test-2"), Catalog.Find("selftest.sounds-beast"));
                var missing = new List<string>();
                string voiceFrom = "-", grazeFrom = "-";
                foreach (var c in cover) { if (c.From == "missing") missing.Add(c.Event); if (c.Event == "voice") voiceFrom = c.From; if (c.Event == "graze") grazeFrom = c.From; }
                Check(voiceFrom == "default" && grazeFrom == "default" && missing.Contains("mount") && missing.Contains("dismount") && !missing.Contains("answer") && !missing.Contains("voice") && !missing.Contains("lips") && !missing.Contains("graze") && !missing.Contains("step"),
                    $"the coverage report: voice and graze from the default; missing: {string.Join(", ", missing)}");
                Info(Sounds.CoverageLine(Looks.Find("selftest.sounds-beast/sounds-test-2")));

                // the save round trip keeps the sound sections with their lines and comment
                string err = Looks.SaveCfg(def) ?? Looks.SaveMountCfg(beast);
                string text = File.ReadAllText(Path.Combine(packDir, "skin.cfg")).Replace("\r\n", "\n");
                string mtext = File.ReadAllText(TempMountCfg(tempRoot, "selftest.sounds-beast")).Replace("\r\n", "\n");
                Check(err == null && text.Contains("[Sounds]\n# keep: the voice\nvoice = voice_1.wav, voice_2.wav") && text.Contains("voice.volume = 0.5") && text.Contains("[Sounds.Cart]\nvoice = voice_cart.wav") && text.IndexOf("[Mount", StringComparison.Ordinal) < 0
                    && mtext.Contains("[Sounds]\nvoice = mount_voice.wav") && mtext.Contains("bell.volume = 0.25") && mtext.IndexOf("[Sounds]") > mtext.IndexOf("[Mount]"),
                    "saved: the skin's [Sounds] and [Sounds.Cart] in skin.cfg, the mount's [Sounds] in mount.cfg, with their lines and comment" + (err != null ? " (" + err + ")" : ""));

                // a changed file: the pack read again (the dev auto-reload does this by itself), the bank's entry
                // forgotten and the new file decoded on the next play
                h.Tuning.Set(Owner, "Skin", "selftest.sounds-beast/default");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "selftest.sounds-beast/default", 8f);
                File.WriteAllBytes(Path.Combine(packDir, "sounds", "voice_1.wav"), SoundBank.TestTone(0.5f, 440f));
                File.WriteAllBytes(Path.Combine(packDir, "sounds", "voice_2.wav"), SoundBank.TestTone(0.5f, 520f));
                string reloadErr = Looks.ReloadCfg(def);
                Check(reloadErr == null && SoundBank.Find(voiceId) == null, "read again after a file changed: the bank's entry is forgotten" + (reloadErr != null ? " (" + reloadErr + ")" : ""));
                def = Looks.Find("selftest.sounds-beast/default");
                rig.Invalidate();
                SoundSpec v3 = rig.Resolve("voice");
                yield return Until(() => v3 != null && SoundBank.StateOf(v3.BankId) == SoundBank.State.Ready, 6f);
                SoundBank.Entry ve3 = SoundBank.Find(v3?.BankId);
                Check(waited && ve3 != null && ve3.Clips.Length == 2 && Mathf.Abs(ve3.Clips[0].length - 0.5f) < 0.03f, $"... and the new files play ({(ve3 != null && ve3.Clips.Length > 0 ? ve3.Clips[0].length.ToString("0.00") : "-")} s now, 0.30 before)");

                // the clean-up: the skins withdrawn, the bank's entries gone
                Check(Looks.Unregister("selftest.sounds-beast/sounds-test-2", owner) && Looks.Unregister("selftest.sounds-beast/default", owner), "both skins withdrawn");
                yield return null;
                Check(SoundBank.Count == bankBefore && SoundBank.Find(voiceId) == null, $"the bank's entries are gone ({SoundBank.Count}, {bankBefore} before)");
            }
            finally
            {
                Plugin.For(MountStyle.Ride).SoundsOn.Value = soundsWas;
                try { if (hc != null && hc.Exists) hc.Remove(); } catch { }
                try { Looks.Unregister("selftest.sounds-beast/sounds-test-2", owner); } catch { }
                LeftOver(owner, "selftest.sounds-beast/default", tempRoot);
            }
        }

        // The public API (docs/mount-sounds-design.md section 9): the catalogue, what plays and from which layer, a mod's
        // own sound over everything (a game id, its own clip kept by the mod, silence) and back on Dispose, Play and
        // Stop with their reasons, the Played event; a code mount's MountSound shared by a pack skin that joins it.
        internal static IEnumerator SoundsApi()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string modOwner = Owner + ".soundmod";
            MountHandle h = null;
            IDisposable set = null, mountReg = null;
            string tempRoot = null;
            AudioClip own = null;
            var played = new List<string>();
            Action<MountHandle, string> onPlayed = (mh, e) => played.Add(e);
            Mounts.Sounds.Played += onPlayed;
            try
            {
                IReadOnlyList<MountSoundEvent> cat = Mounts.Sounds.Catalogue;
                Check(cat.Count == SoundEvents.All.Length && Has(Mounts.Sounds.Events, "voice") && cat[0].Id == "step" && cat[0].Variants.Count == 4 && Has(cat[0].Numbers, "per_cycle"),
                    $"the catalogue: {cat.Count} events, step's four variants and its numbers");
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                yield return Frames(2);

                // what plays, from which layer
                MountSound r = Mounts.Sounds.Resolved(h, "voice", out string layer);
                Check(r != null && layer == "base" && r.GameId == Plugin.For(MountStyle.Ride).BraySoundId.Value, $"Resolved(voice): the base layer, {r?.GameId} ({layer})");
                Check(Mounts.Sounds.Resolved(h, "ear", out string none) == null && none == "none", "Resolved(ear): null, no layer gives it a sound");
                MountResult bad = Mounts.Sounds.Play(h, "nope");
                Check(!bad.Ok && bad.Reason.Contains("No sound event"), $"Play(nope) refused with the reason ({Said(bad)})");

                // a mod's own sound over everything: a game id
                set = Mounts.Sounds.Set(modOwner, h, "voice", MountSound.Game("donkey_bell"));
                r = Mounts.Sounds.Resolved(h, "voice", out layer);
                Check(r != null && layer == "mod" && r.GameId == "donkey_bell", $"Set(voice, donkey_bell): resolved from the mod layer ({r?.GameId}, {layer})");
                played.Clear();
                MountResult pr = Mounts.Sounds.Play(h, "voice");
                yield return null;
                Check(pr.Ok && played.Contains("voice"), $"Play(voice) ok and the Played event came ({Said(pr)}; {played.Count} event(s))");
                set.Dispose();
                r = Mounts.Sounds.Resolved(h, "voice", out layer);
                Check(r != null && layer == "base", $"disposed: the base layer again ({layer})");

                // the mod's own clip, kept by the mod
                own = SoundBank.FromWav(SoundBank.TestTone(0.3f, 660f), "mod-own");
                set = Mounts.Sounds.Set(modOwner, h, "voice", MountSound.Own(own));
                r = Mounts.Sounds.Resolved(h, "voice", out layer);
                Check(r != null && layer == "mod" && r.GameId != null && r.GameId.StartsWith("gk2.mod." + modOwner, StringComparison.Ordinal) && SoundBank.StateOf(r.GameId) == SoundBank.State.Ready, $"Set(voice, own clip): registered as a game sound at once ({r?.GameId})");
                string bankId = r?.GameId;
                SoundHandler hv = h.I.SoundsRig.Emit("voice");
                Check(hv != null && ClipOf(hv) == "mod-own", $"it plays the mod's clip ({ClipOf(hv)})");
                set.Dispose();
                yield return null;
                Check(SoundBank.Find(bankId) == null && own != null, "disposed: the bank entry is gone and the mod's clip is not destroyed");

                // silence, and Play's reason
                set = Mounts.Sounds.Set(modOwner, h, "voice", MountSound.None);
                MountResult silent = Mounts.Sounds.Play(h, "voice");
                Check(!silent.Ok && silent.Reason.Contains("silent"), $"Set(voice, None): Play refused as silent ({Said(silent)})");
                set.Dispose();
                set = null;

                // Stop on a stoppable event
                MountResult gr = Mounts.Sounds.Play(h, "graze");
                SoundSpec gs = h.I.SoundsRig.Resolve("graze");
                Check(gr.Ok || gs == null || !gs.On, $"Play(graze) ({Said(gr)})");
                Mounts.Sounds.Stop(h, "graze");
                yield return null;
                Pass("Stop(graze) returned");

                // a code mount with its own sounds, a pack skin joining it
                try
                {
                    mountReg = Mounts.Catalog.Register(modOwner, new MountDef { Id = "selftest.sound-code-beast", Name = "Sound Code Beast", Sounds = new Dictionary<string, MountSound> { ["voice"] = MountSound.Game("donkey_bell"), ["lips"] = MountSound.None } });
                    tempRoot = WriteTempPack("gk2donkeyapi-selftest-soundapi", "selftest.soundapi-test", "[Skin]\nformat = 1\nname = Sound API Test\nmount = selftest.sound-code-beast\n");
                }
                catch (Exception e) { Warn("the code mount or the temp pack could not be made: " + e.Message); yield break; }
                MountInfo info = Mounts.Catalog.Find("selftest.sound-code-beast");
                Check(info != null && info.Sounds != null && info.Sounds["voice"] is MountSound, "MountInfo carries the code mount's sounds");
                Looks.RegisterFolder(tempRoot, modOwner);
                h.Tuning.Set(Owner, "Skin", "selftest.sound-code-beast/selftest.soundapi-test");
                yield return Until(() => h.Mount == "selftest.sound-code-beast", 8f);
                h.I.SoundsRig.Invalidate();
                r = Mounts.Sounds.Resolved(h, "voice", out layer);
                Check(waited && r != null && layer == "mount" && r.GameId == "donkey_bell", $"the pack skin of the code mount: voice from the mount's MountSound ({r?.GameId}, {layer})");
                MountSound lips = Mounts.Sounds.Resolved(h, "lips", out string lipsLayer);
                Check(lips != null && lips.Silent && lipsLayer == "mount", "and its lips are silent by the mount");
                h.Tuning.Set(Owner, "Skin", "");
                yield return Until(() => h.Mount == MountCatalog.DonkeyId, 8f);
            }
            finally
            {
                Mounts.Sounds.Played -= onPlayed;
                try { set?.Dispose(); } catch { }
                try { Looks.Unregister("selftest.sound-code-beast/selftest.soundapi-test", modOwner); } catch { }
                try { mountReg?.Dispose(); } catch { }
                try { if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                try { if (own != null) UnityEngine.Object.Destroy(own); } catch { }
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }

        // An embedded pack's sounds, the route the built-in skins' sounds take (a mod's own, embedded in its DLL):
        // the API's own test pack (assets\testskin, never registered at start), its .ogg decoded through the cache
        // file under BepInEx\cache, its .wav parsed in place by the convention name, a looping event stopped.
        internal static IEnumerator SoundsEmbedded()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".embedded";
            MountHandle h = null;
            try
            {
                int n = Looks.RegisterEmbedded(typeof(Plugin).Assembly, "GK2DonkeyAPI.testmounts.", owner);
                Looks.SkinDef def = Looks.Find("donkey/selftest.embedded-sound-test");
                Check(n == 1 && def != null && def.FileNames.Contains("sounds/voice_1.ogg") && def.FileNames.Contains("sounds/step_1.wav"),
                    $"the API's embedded test pack registers with its two sound files ({n} pack(s); {(def != null ? string.Join(", ", def.FileNames) : "-")})");
                if (def == null) yield break;
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "donkey/selftest.embedded-sound-test");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "donkey/selftest.embedded-sound-test", 8f);
                Sounds.Rig rig = h.I.SoundsRig;
                rig.Invalidate();
                SoundSpec st = rig.Resolve("step");
                // the pack's files start one a frame (SoundBank.Advance): the .wav is parsed in place when its turn comes,
                // a frame or two later, not in the frame it was asked for
                string sid = st?.BankId;
                yield return Until(() => SoundBank.StateOf(sid) == SoundBank.State.Ready, 1f);
                Check(st != null && st.Layer == "skin" && sid != null && waited, $"step_1.wav by convention, parsed in place: ready within a second, its turn in the queue ({st?.Describe()}; {SoundBank.StateOf(sid)})");
                SoundBank.Entry se = SoundBank.Find(st?.BankId);
                Check(se != null && se.Clips.Length == 1 && Mathf.Abs(se.Clips[0].length - 0.25f) < 0.03f, $"... a 0.25 s clip ({(se != null && se.Clips.Length > 0 ? se.Clips[0].length.ToString("0.00") : "-")})");
                SoundSpec v = rig.Resolve("voice");
                string vid = v?.BankId;
                Check(v != null && v.Layer == "skin" && vid != null, $"voice_1.ogg: resolved to the bank ({v?.Describe()})");
                yield return Until(() => SoundBank.StateOf(vid) == SoundBank.State.Ready, 8f);
                SoundBank.Entry ve = SoundBank.Find(vid);
                Check(waited && ve != null && ve.Clips.Length == 1 && Mathf.Abs(ve.Clips[0].length - 0.4f) < 0.05f, $"... decoded through the cache file: a 0.4 s clip ({(ve != null && ve.Clips.Length > 0 ? ve.Clips[0].length.ToString("0.00") : "-")})");
                string cache = Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "GK2DonkeyAPI", "sounds", owner, "donkey_selftest.embedded-sound-test", "voice_1.ogg");
                Check(File.Exists(cache), "the cache file exists: " + cache);
                rig.Invalidate();
                SoundHandler hv = rig.Emit("voice");
                Check(hv != null && hv.IsActive, $"Emit(voice) plays it ({ClipOf(hv)})");
                // graze names the same file under its own line: its own bank entry, decoded on its first resolve (a
                // play before that is silent, as a pack's first seconds are); waited for here
                SoundSpec gs = rig.Resolve("graze");
                yield return Until(() => gs != null && SoundBank.StateOf(gs.BankId) == SoundBank.State.Ready, 8f);
                rig.Invalidate();
                SoundHandler g = rig.Emit("graze");
                yield return Seconds(1f);
                Check(g != null && g.IsActive, $"graze.loop = true: the 0.4 s clip is still playing after a second (looping; {(g != null ? "played" : "not played: " + rig.LastEvent)})");
                rig.Stop("graze");
                yield return null;
                Check(g == null || !g.IsActive, "Stop(graze) ends the loop");
                h.Tuning.Set(Owner, "Skin", "");
                yield return Until(() => h.Mount == MountCatalog.DonkeyId, 8f);
            }
            finally
            {
                try { Looks.Unregister("donkey/selftest.embedded-sound-test", owner); } catch { }
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }

        internal static IEnumerator SoundsFlying()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors: flying indoors is off"); yield break; }
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".flysounds";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-flysounds", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Fly Sound Test\nmount = selftest.flysound-beast\n\n" +
                    "[Sounds]\ntakeoff = fishing_cast_swoosh\nflap = conveyor_furnace_bellows\nglide = tree_wind\nfly = weather_wind\nland = bush_chop\ntouchdown = oh_wood_drop\nlanded = item_put\njump = equip_tool\njumpland = oh_wood_container_drop\n\n" +
                    "[Mount]\nid = selftest.flysound-beast\nname = Fly Sound Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\n");
            }
            catch (Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle h = null;
            try
            {
                Looks.RegisterFolder(tempRoot, owner);
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "selftest.flysound-beast/default");
                yield return Until(() => h.Mount == "selftest.flysound-beast", 8f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance i = Mount.Ridden;
                Sounds.Rig rig = i.SoundsRig;
                rig.TestPlays.Clear();
                int Plays(string e) => rig.TestPlays.TryGetValue(e, out int c) ? c : 0;

                MountResult t = Mounts.TakeOff();
                Check(t.Ok, $"take-off ({Said(t)})");
                yield return Until(() => Flight.Now == Flight.Phase.Airborne, 10f);
                Check(waited && Plays("takeoff") == 1 && Plays("flap") >= 1, $"the take-off: takeoff once, {Plays("flap")} flap(s) in the launch ({Flight.Beats} beats), fly {Plays("fly")}");
                Check(Plays("fly") == 1, "the fly loop started once");
                int afterLaunch = Plays("flap");
                int stepsBefore = Plays("step"), airFrames = rig.TestAirFrames;
                Flight.TestMoveDir = new Vector2(1f, 0f); // steered: the view's walk clip runs under the flying frames
                yield return Seconds(2f);
                Flight.TestMoveDir = null;
                Check(Plays("flap") > afterLaunch, $"airborne: the flaps go on with the beat ({Plays("flap")} after two seconds, {afterLaunch} after the launch)");
                Check(Plays("step") == stepsBefore, $"airborne and steered: no footstep ({Plays("step") - stepsBefore} played; the walk clip ran {rig.TestAirFrames - airFrames} frame(s) in the air, its footfalls kept silent)");
                MountResult l = Mounts.Land();
                Check(l.Ok, $"land ({Said(l)})");
                yield return Until(() => Flight.Now == Flight.Phase.Ground, 15f);
                Check(waited && Plays("land") == 1 && Plays("touchdown") == 1 && Plays("landed") == 1, $"the landing: land {Plays("land")}, touchdown {Plays("touchdown")}, landed {Plays("landed")} ({Flight.Why})");
                yield return Frames(10);

                // a jump: its own two events, nothing of the flight
                rig.TestPlays.Clear();
                i = Mount.Ridden;
                Flight.TestJumpHeld = true;
                Flight.Press(i, true, false, same: true);
                yield return Until(() => Flight.Now == Flight.Phase.Jumping && Flight.HeightPx > 2f, 2f);
                Flight.TestJumpHeld = false;
                yield return Until(() => Flight.Now == Flight.Phase.Ground, 10f);
                Check(waited && Plays("jump") == 1 && Plays("jumpland") >= 1, $"the jump: jump {Plays("jump")}, jumpland {Plays("jumpland")} ({Flight.Why})");
                Check(Plays("takeoff") == 0 && Plays("flap") == 0 && Plays("fly") == 0 && Plays("land") == 0, "a jump plays none of the flight's events");
            }
            finally
            {
                Flight.TestJumpHeld = null;
                Flight.TestMoveDir = null;
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
                Looks.Unregister("selftest.flysound-beast/default", owner);
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // The herd (docs/mount-sounds-design.md 3.3): a standing grown one calls by itself, rarer than ridden and never
        // within the scene's voice gap of another; its babies answer one after another, with [Sounds.Baby]'s sound
        // where the pack gives one; nothing of it with the standing switch off.
        internal static IEnumerator SoundsHerd()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".herd";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-herd", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Herd Test\nmount = selftest.herd-beast\n\n[Sounds]\nvoice = donkey_bell\nvoice.idle_every = 1\n\n[Sounds.Baby]\nanswer = fishing_blop\n\n" +
                    "[Mount]\nid = selftest.herd-beast\nname = Herd Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
            }
            catch (Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle a = null, b = null, k1 = null, k2 = null;
            bool standingWas = Plugin.StandingSounds.Value;
            float gapWas = Sounds.VoiceGapS;
            try
            {
                Looks.RegisterFolder(tempRoot, owner);
                SetStandingSounds(true);
                a = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (a == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => a.IsReady, 6f);
                a.Tuning.Set(Owner, "Skin", "selftest.herd-beast/default");
                yield return Until(() => a.Mount == "selftest.herd-beast", 8f);
                k1 = Mounts.SpawnFoal(Owner, a);
                k2 = Mounts.SpawnFoal(Owner, a);
                yield return Until(() => k1 != null && k2 != null && k1.IsReady && k2.IsReady, 8f);
                if (k1 == null || k2 == null) { Fail("no foals could be spawned here"); yield break; }
                k1.Tuning.Set(Owner, "Skin", "selftest.herd-beast/default");
                k2.Tuning.Set(Owner, "Skin", "selftest.herd-beast/default");
                yield return Until(() => k1.Mount == "selftest.herd-beast" && k2.Mount == "selftest.herd-beast", 8f);
                yield return Frames(3);
                Sounds.Rig ra = a.I.SoundsRig, r1 = k1.I.SoundsRig, r2 = k2.I.SoundsRig;
                ra.Invalidate(); r1.Invalidate(); r2.Invalidate();
                int Plays(Sounds.Rig r, string e) => r.TestPlays.TryGetValue(e, out int c) ? c : 0;

                // the layers: the babies' block is the babies' only
                SoundSpec aa = ra.Resolve("answer"), ka = r1.Resolve("answer");
                Check(!ra.IsBaby && r1.IsBaby && r2.IsBaby, $"a grown one and two babies ({a.I.ViewScale:0.00}, {k1.I.ViewScale:0.00}, {k2.I.ViewScale:0.00})");
                Check(ka != null && ka.GameId == "fishing_blop" && ka.Layer == "skin", $"a baby's answer: [Sounds.Baby]'s ({ka?.Describe()})");
                Check(aa != null && aa.Silent && aa.Layer == "base", $"the grown one's answer: not the babies' block; the donkey's own sound, silent on another mount ({aa?.Describe()})");

                // the standing call and the answers
                Sounds.TestResetHerd();
                ra.TestPlays.Clear(); r1.TestPlays.Clear(); r2.TestPlays.Clear();
                yield return Until(() => Plays(ra, "voice") >= 1, 10f);
                SoundSpec av = ra.Resolve("voice");
                Check(waited, $"a standing grown one calls by itself within 10 s (idle_every {av?.Number("idle_every", -1f):0.##} x the standing factor {Sounds.StandingIdleFactor}; the rig: {ra.TestState}; voice {av?.Describe()})");
                Sounds.TestEveryBabyAnswers = true; // each baby answers by its chance in play (2026-10-27): both here
                yield return Until(() => Plays(r1, "answer") >= 1 && Plays(r2, "answer") >= 1, Sounds.Rig.CallWaitMaxS + 3f);
                Sounds.TestEveryBabyAnswers = false;
                Check(waited, $"both babies answered ({Plays(r1, "answer")}, {Plays(r2, "answer")})");
                var times = new List<(float At, Mount.Instance Who, string Event)>(Sounds.TestVoiceTimes);
                float call = -1f, first = -1f, second = -1f;
                foreach (var t in times)
                {
                    if (t.Event == "voice" && call < 0f) call = t.At;
                    else if (t.Event == "answer" && call >= 0f && t.At >= call) { if (first < 0f) first = t.At; else if (second < 0f) second = t.At; }
                }
                float ended = ra.TestCallEndedAt;
                Check(call >= 0f && ended >= call && first >= ended + Sounds.Rig.AnswerAfterS - 0.05f && first <= ended + 0.6f && second >= first,
                    $"the answers come after the call has ended (call at {call:0.00}, ended {ended:0.00}, answers at {first:0.00} and {second:0.00})");
                // ... and not the same every time (2026-10-26: always in spawn order, on one even beat): 40 plans of three
                var order = new List<int>();
                var delays = new List<float>();
                var firsts = new HashSet<int>();
                float gapMin = float.MaxValue, gapMax = 0f;
                for (int n = 0; n < 40; n++)
                {
                    Sounds.Rig.PlanAnswers(3, order, delays);
                    firsts.Add(order[0]);
                    for (int k = 1; k < delays.Count; k++)
                    {
                        gapMin = Mathf.Min(gapMin, delays[k] - delays[k - 1]);
                        gapMax = Mathf.Max(gapMax, delays[k] - delays[k - 1]);
                    }
                }
                Check(firsts.Count == 3 && gapMin < 0.2f && gapMax > 0.65f,
                    $"the answers vary: each of three babies answers first sometimes ({firsts.Count} of 3), the gaps {gapMin:0.00} to {gapMax:0.00} s over 40 calls");

                // one voice at a time: a second grown one standing by, calls at least the gap apart
                Sounds.VoiceGapS = 4f;
                b = Spawn(Spot(home, 5f) ?? home, right: homeFacing > 0f);
                if (b != null)
                {
                    yield return Until(() => b.IsReady, 6f);
                    b.Tuning.Set(Owner, "Skin", "selftest.herd-beast/default");
                    yield return Until(() => b.Mount == "selftest.herd-beast", 8f);
                    Sounds.TestResetHerd();
                    // a standing call comes 1.5 to 4.5 s after the last (idle_every 1 x StandingIdleFactor 3, x 0.5..1.5), a
                    // step puts it off 2 to 6 s, and no two are nearer than VoiceGapS: a second one can take past 12 s
                    // (one call in 12 s, 2026-10-26); waited for up to 30
                    var calls = new List<float>();
                    float waitFrom = Time.time;
                    while (Time.time - waitFrom < 30f)
                    {
                        calls.Clear();
                        foreach (var t in Sounds.TestVoiceTimes) if (t.Event == "voice") calls.Add(t.At);
                        if (calls.Count >= 2 && Time.time - waitFrom >= 12f) break;
                        yield return null;
                    }
                    float minGap = float.MaxValue;
                    for (int i = 1; i < calls.Count; i++) minGap = Mathf.Min(minGap, calls[i] - calls[i - 1]);
                    Check(calls.Count >= 2 && minGap >= Sounds.VoiceGapS - 0.1f, $"two standing grown ones: {calls.Count} call(s) in {Time.time - waitFrom:0} s, the nearest two {(calls.Count >= 2 ? minGap.ToString("0.00") : "-")} s apart (at least {Sounds.VoiceGapS})");
                }
                else Info("no room for a second grown one: the voice gap not seen");

                // the standing switch off: nothing standing calls or answers
                SetStandingSounds(false);
                ra.Invalidate(); r1.Invalidate(); r2.Invalidate();
                Sounds.TestResetHerd();
                yield return Seconds(6f);
                Check(Sounds.TestVoiceTimes.Count == 0, $"StandingSounds off: no call and no answer in 6 s ({Sounds.TestVoiceTimes.Count})");
                SetStandingSounds(true);
            }
            finally
            {
                Sounds.VoiceGapS = gapWas;
                SetStandingSounds(standingWas);
                try { if (k1 != null && k1.Exists) k1.Remove(); } catch { }
                try { if (k2 != null && k2.Exists) k2.Remove(); } catch { }
                try { if (b != null && b.Exists) b.Remove(); } catch { }
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                LeftOver(owner, "selftest.herd-beast/default", tempRoot);
            }
        }

        // Every event with a base: the resolved spec equals the style's keys. Returns how many matched.
        internal static int CheckBase(Sounds.Rig rig, StyleSettings s, MountStyle style)
        {
            int ok = 0;
            bool Near(float a, float b) => Math.Abs(a - b) < 0.0005f;
            void Row(string evt, bool match, string got, string want)
            {
                if (match) ok++;
                else Fail($"{style}: {evt} resolves to {got}, the keys say {want}");
            }
            rig.Invalidate();
            SoundSpec v = rig.Resolve("voice");
            Row("voice", v != null && v.GameId == s.BraySoundId.Value && Near(v.Volume, s.BrayVolume.Value) && Near(v.Pitch, s.BrayPitch.Value) && v.Speech == !s.BrayInSfxChannel.Value
                && Near(v.Number("idle_every", -1f), s.BrayIdleEverySeconds.Value) && Near(v.Number("mount_chance", -1f), s.BrayOnMountChance.Value), v?.Describe(), $"{s.BraySoundId.Value} x{s.BrayVolume.Value} p{s.BrayPitch.Value}");
            SoundSpec st = rig.Resolve("step");
            bool bySurface = string.IsNullOrEmpty(s.HoofSoundId.Value?.Trim());
            Row("step", st != null && st.BySurface == bySurface && (bySurface || st.GameId == s.HoofSoundId.Value.Trim()) && st.On == s.HoofOn.Value && Near(st.Volume, s.HoofVolume.Value) && Near(st.Pitch, s.HoofPitch.Value)
                && Near(st.PitchVariation, s.HoofPitchVariation.Value) && Near(st.Number("per_cycle", -1f), s.HoofStepsPerCycle.Value) && Near(st.Number("phase", -1f), s.HoofPhase.Value), st?.Describe(), $"hoof keys ({(bySurface ? "by the ground" : s.HoofSoundId.Value)} x{s.HoofVolume.Value} p{s.HoofPitch.Value}, {s.HoofStepsPerCycle.Value}/cycle)");
            SoundSpec g = rig.Resolve("graze");
            Row("graze", g != null && g.GameId == s.GrazeSoundId.Value && g.On == s.GrazeOn.Value && Near(g.Volume, s.GrazeVolume.Value), g?.Describe(), $"{s.GrazeSoundId.Value} x{s.GrazeVolume.Value}");
            SoundSpec l = rig.Resolve("lips");
            Row("lips", l != null && l.GameId == "donkey_lips" && Near(l.Volume, s.LipsVolume.Value), l?.Describe(), $"donkey_lips x{s.LipsVolume.Value}");
            SoundSpec w = rig.Resolve("wheel");
            Row("wheel", w != null && w.GameId == s.WheelSoundId.Value && w.On == s.WheelOn.Value && Near(w.Volume, s.WheelVolume.Value) && Near(w.Pitch, s.WheelPitch.Value) && Near(w.Number("every_cycles", -1f), s.WheelEveryCycles.Value), w?.Describe(), $"{s.WheelSoundId.Value} x{s.WheelVolume.Value}");
            SoundSpec c = rig.Resolve("cargo");
            Row("cargo", c != null && c.GameId == s.CargoThumpSoundId.Value && c.On == s.CargoThumpOn.Value && Near(c.Volume, s.CargoThumpVolume.Value) && Near(c.Pitch, s.CargoThumpPitch.Value), c?.Describe(), $"{s.CargoThumpSoundId.Value} x{s.CargoThumpVolume.Value}");
            SoundSpec m = rig.Resolve("mount");
            Row("mount", m != null && m.GameId == s.MountSoundId.Value && m.On == s.MountOn.Value && Near(m.Volume, s.MountVolume.Value) && Near(m.Pitch, s.MountPitch.Value) && Near(m.Start, s.MountSoundStart.Value), m?.Describe(), $"{s.MountSoundId.Value} from {s.MountSoundStart.Value}");
            SoundSpec dm = rig.Resolve("dismount");
            Row("dismount", dm != null && dm.GameId == s.DismountSoundId.Value && dm.On == s.DismountOn.Value && Near(dm.Volume, s.DismountVolume.Value) && Near(dm.Pitch, s.DismountPitch.Value) && Near(dm.Start, s.DismountSoundStart.Value), dm?.Describe(), $"{s.DismountSoundId.Value} from {s.DismountSoundStart.Value}");
            SoundSpec hi = rig.Resolve("hitch");
            Row("hitch", hi != null && hi.GameId == s.HitchSoundId.Value && hi.On == s.HitchOn.Value && Near(hi.Volume, s.HitchVolume.Value) && Near(hi.Pitch, s.HitchPitch.Value), hi?.Describe(), s.HitchSoundId.Value);
            SoundSpec un = rig.Resolve("unhitch");
            Row("unhitch", un != null && un.GameId == s.UnhitchSoundId.Value && un.On == s.UnhitchOn.Value && Near(un.Volume, s.UnhitchVolume.Value) && Near(un.Pitch, s.UnhitchPitch.Value), un?.Describe(), s.UnhitchSoundId.Value);
            SoundSpec b = rig.Resolve("bell");
            Row("bell", b != null && b.GameId == s.BellSoundId.Value && Near(b.Volume, s.BellVolume.Value) && Near(b.Pitch, s.BellPitch.Value) && Near(b.Number("set_off_idle", -1f), s.BellSetOffIdleSeconds.Value)
                && Near(b.Number("on_set_off", -1f), s.BellOnSetOff.Value ? 1f : 0f) && Near(b.Number("only_with_cargo", -1f), s.BellOnlyWithCargo.Value ? 1f : 0f), b?.Describe(), $"{s.BellSoundId.Value} x{s.BellVolume.Value}");
            foreach (string none in new[] { "setoff", "stop", "ear", "turn", "refused", "sent", "called" })
            {
                if (rig.Resolve(none) != null) Fail($"{style}: {none} has a base sound; the donkey never had one");
            }
            return ok;
        }

        // A pack's own clip and the base's start offset (2026-10-30): the donkey's getting-on and getting-off snorts start
        // 0.42 s in (MountSoundStart / DismountSoundStart, their silence skipped); a pack's own file for the event is a
        // different clip, so it starts at 0 unless its line says start. Before: the rat's 0.33 s squeak was seeked past its
        // end at every ride-style mount and Unity logged "invalid seek position" (Player.log, never an exception); the
        // dragon's growl lost its first 0.42 s. The guard in PlayId: a start past the clip's end is dropped with one
        // warning, never seeked. The Unity log is read back (Application.logMessageReceived) while the clips play.
        internal static IEnumerator SoundsStartOffset()
        {
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".soundstart";
            string tempRoot = null;
            MountHandle h = null;
            var unityErrors = new List<string>();
            Application.LogCallback onLog = (condition, stack, type) => { if (type == LogType.Error || type == LogType.Exception) unityErrors.Add(condition); };
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-soundstart", Looks.DefaultSkinName,
                    "# a test pack\n[Skin]\nformat = 1\nname = Start Test\nmount = selftest.start-beast\n\n" +
                    "[Sounds]\nmount = mount_1.wav\ndismount = dismount_1.wav\ndismount.start = 0.1\n\n" +
                    "[Mount]\nid = selftest.start-beast\nname = Start Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = false\n");
                string sdir = Path.Combine(TempSkinDir(tempRoot, "selftest.start-beast", Looks.DefaultSkinName), "sounds");
                Directory.CreateDirectory(sdir);
                File.WriteAllBytes(Path.Combine(sdir, "mount_1.wav"), SoundBank.TestTone(0.2f, 500f));    // shorter than the donkey's 0.42 s offset
                File.WriteAllBytes(Path.Combine(sdir, "dismount_1.wav"), SoundBank.TestTone(0.5f, 400f));
            }
            catch (Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            Application.logMessageReceived += onLog;
            try
            {
                int n = Looks.RegisterFolder(tempRoot, owner);
                Check(n == 1, $"one pack found ({n})");
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "selftest.start-beast/default");
                yield return Until(() => h.Mount == "selftest.start-beast", 8f);
                Check(waited, "the donkey wears the pack's skin and is its mount");
                Sounds.Rig rig = h.I.SoundsRig;
                rig.Invalidate();
                SoundSpec m = rig.Resolve("mount");
                string mid = m?.BankId;
                yield return Until(() => mid != null && SoundBank.StateOf(mid) == SoundBank.State.Ready, 6f);
                rig.Invalidate();
                m = rig.Resolve("mount");
                float baseStart = Plugin.For(MountStyle.Ride).MountSoundStart.Value;
                Check(m != null && m.Layer == "skin" && m.BankId != null && m.Start == 0f, $"mount = a file of the pack's starts at 0, not at the donkey's {baseStart:0.00} s ({m?.Describe()})");
                SoundSpec dm = rig.Resolve("dismount");
                Check(dm != null && dm.Layer == "skin" && Mathf.Abs(dm.Start - 0.1f) < 0.001f, $"dismount.start = 0.1 is the line's own ({dm?.Describe()})");
                unityErrors.Clear();
                SoundHandler hm = rig.Emit("mount");
                yield return null;
                yield return null;
                Check(hm != null && unityErrors.Count == 0, hm == null ? "Emit(mount) played nothing" : $"the 0.2 s clip plays with no Unity error logged ({unityErrors.Count}: {string.Join(" | ", unityErrors.ToArray())})");
                // the guard: a start past the clip's end asked for directly is dropped (one warning of the mod's), not seeked
                unityErrors.Clear();
                Line("[test] INFO " + current + ": the next \"not longer than its start offset\" warning is this test's own");
                SoundHandler hp = Sounds.PlayId(mid, PC.transform, 1f, 1f, 5f);
                yield return null;
                yield return null;
                Check(hp != null && unityErrors.Count == 0, hp == null ? "PlayId played nothing" : $"a start of 5 s on the 0.2 s clip: played from its start, no Unity error ({unityErrors.Count})");
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                if (h != null) h.Remove();
                try { Looks.Unregister("selftest.start-beast/default", owner); } catch { }
                try { if (tempRoot != null) Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}
