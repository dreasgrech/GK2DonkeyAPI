using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string GroupSounds = "Sounds";

        // The pooled player behind a handler (a private field of the game's; Access.Verify requires it).
        private static readonly AccessTools.FieldRef<SoundHandler, SoundController> HandlerSource = AccessTools.FieldRefAccess<SoundHandler, SoundController>("sourceHolder");

        // The standing switch for a test: through Saddle Up's row when its page is up (its apply sets the Behaviour
        // override Saddle Up holds, which masks the cfg entry: Tuned.Value answers the override first), else the entry.
        private static void SetStandingSounds(bool on)
        {
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            MountSettingsPage.Row row = saddle?.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "StandingSounds" && x.ShownSection == "Sound");
            if (row != null && row.Entry is BepInEx.Configuration.ConfigEntry<bool> e) e.Value = on;
            else Plugin.StandingSounds.Value = on;
        }

        private static string ClipOf(SoundHandler h)
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

        private static IEnumerator SoundsProbe()
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
        private static IEnumerator SoundsDonkeyUnchanged()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
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

                // 5. a mount's pins on a standing wearer: the snail is silent by its [Mount.Tuning]
                if (Mounts.Looks.Find("snail") != null)
                {
                    standing.Tuning.Set(Owner, "Skin", "snail");
                    yield return Until(() => standing.Mount == "snail", 8f);
                    yield return Until(() => standing.Tuning.IsSet("BrayVolume"), 10f); // the mount's pins land on the wearer once its pictures are in (the skin rig's merge)
                    yield return Frames(2);
                    sr.Invalidate();
                    SoundSpec sv = sr.Resolve("voice"), sl = sr.Resolve("lips"), ss = sr.Resolve("step");
                    bool Off(SoundSpec sp) => sp != null && (sp.Silent || sp.Volume < 0.001f); // the mount's pins (volume 0) and its [Mount.Sounds] none both silence
                    Check(waited && Off(sv) && Off(sl) && Off(ss),
                        $"a standing snail: its mount's pins and [Mount.Sounds] reach the rig (voice {sv?.Describe()}; lips {sl?.Describe()}; step {ss?.Describe()}; the pins {(waited ? "on the wearer" : "NOT on the wearer within 10 s")}, BrayVolume {(standing.Tuning.IsSet("BrayVolume") ? "set by " + standing.Tuning.SetBy("BrayVolume") : "unset")})");
                    Check(sr.Emit("voice") == null && sr.Emit("lips") == null && sr.Takes("lips"), "... so nothing plays for them, and the lips event is still taken (silent, not the game's)");
                    standing.Tuning.Set(Owner, "Skin", "");
                    yield return Until(() => standing.Mount == "donkey", 8f);
                }
                else Info("the snail skin is not installed: the mount pins on a standing wearer not seen");
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
        private static IEnumerator SoundsPack()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".sounds";
            string tempRoot = null, packDir = null;
            MountHandle h = null, hc = null;
            int bankBefore = SoundBank.Count;
            bool soundsWas = Plugin.For(MountStyle.Ride).SoundsOn.Value;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-soundpack", "sounds-test",
                    "# a test pack\n[Skin]\nformat = 1\nid = sounds-test\nname = Sounds Test\nmount = sounds-beast\n\n" +
                    "[Sounds]\n# keep: the voice\nvoice = voice_1.wav, voice_2.wav\nvoice.volume = 0.5\nstep.grass = step_grass\nlips = none\nbogus = x.wav\n\n" +
                    "[Sounds.Cart]\nvoice = voice_cart.wav\n\n" +
                    "[Mount]\nid = sounds-beast\nname = Sounds Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n\n" +
                    "[Mount.Sounds]\nvoice = mount_voice.wav\nbell.volume = 0.25\n");
                packDir = Path.Combine(tempRoot, Looks.PackDirName, "sounds-test");
                string sdir = Path.Combine(packDir, "sounds");
                Directory.CreateDirectory(sdir);
                File.WriteAllBytes(Path.Combine(sdir, "voice_1.wav"), SoundBank.TestTone(0.3f, 440f));
                File.WriteAllBytes(Path.Combine(sdir, "voice_2.wav"), SoundBank.TestTone(0.3f, 520f));
                File.WriteAllBytes(Path.Combine(sdir, "voice_cart.wav"), SoundBank.TestTone(0.3f, 330f));
                File.WriteAllBytes(Path.Combine(sdir, "mount_voice.wav"), SoundBank.TestTone(0.3f, 220f));
                File.WriteAllBytes(Path.Combine(sdir, "graze_1.wav"), SoundBank.TestTone(0.6f, 180f)); // the convention: no cfg line
                string pack2 = Path.Combine(tempRoot, Looks.PackDirName, "sounds-test-2");
                Directory.CreateDirectory(pack2);
                File.Copy(Path.Combine(packDir, "bdy_static_left.png"), Path.Combine(pack2, "bdy_static_left.png"));
                File.WriteAllText(Path.Combine(pack2, "skin.cfg"), "[Skin]\nformat = 1\nid = sounds-test-2\nname = Sounds Test Two\nmount = sounds-beast\n");
            }
            catch (Exception e) { Warn("the temp packs could not be written: " + e.Message); yield break; }
            try
            {
                int n = Looks.RegisterFolder(tempRoot, owner);
                Check(n == 2, $"two packs found ({n})");
                Looks.SkinDef def = Looks.Find("sounds-test");
                Check(def != null && def.Report.Contains("sound line(s)") && def.Report.Contains("no sound event 'bogus'"), "the loader counts the sound lines and names the bogus event: " + (def?.Report ?? "-"));
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null || def == null) { Fail("could not spawn, or the pack is missing"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "sounds-test");
                yield return Until(() => h.Mount == "sounds-beast", 8f);
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
                    hc.Tuning.Set(Owner, "Skin", "sounds-test");
                    yield return Until(() => hc.Mount == "sounds-beast", 8f);
                    hc.I.SoundsRig.Invalidate();
                    SoundSpec vc = hc.I.SoundsRig.Resolve("voice");
                    Check(vc != null && vc.Layer == "skin" && vc.BankId != null && vc.BankId.EndsWith("sounds.cart:voice", StringComparison.Ordinal), $"with the cart: [Sounds.Cart] voice wins ({vc?.Describe()})");
                    hc.Remove();
                    hc = null;
                }

                // a second skin of the mount, no [Sounds]: the mount's voice, its file from the declaring skin's folder
                h.Tuning.Set(Owner, "Skin", "sounds-test-2");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "sounds-test-2", 8f);
                rig.Invalidate();
                SoundSpec v2 = rig.Resolve("voice");
                Check(v2 != null && v2.Layer == "mount" && v2.BankId != null && v2.BankId.EndsWith("mount.sounds:voice", StringComparison.Ordinal), $"the second skin (no [Sounds]) gets the mount's voice ({v2?.Describe()})");
                string mountVoiceId = v2?.BankId;
                yield return Until(() => SoundBank.StateOf(mountVoiceId) == SoundBank.State.Ready, 6f);
                Check(waited, "the mount's file loaded (from the declaring skin's folder)");

                // the save round trip keeps the sound sections with their lines and comment
                string err = Looks.SaveCfg(def);
                string text = File.ReadAllText(Path.Combine(packDir, "skin.cfg")).Replace("\r\n", "\n");
                Check(err == null && text.Contains("[Sounds]\n# keep: the voice\nvoice = voice_1.wav, voice_2.wav") && text.Contains("voice.volume = 0.5") && text.Contains("[Sounds.Cart]\nvoice = voice_cart.wav")
                    && text.Contains("[Mount.Sounds]\nvoice = mount_voice.wav") && text.Contains("bell.volume = 0.25") && text.IndexOf("[Sounds]") < text.IndexOf("[Mount]") && text.IndexOf("[Mount.Sounds]") > text.IndexOf("[Mount]"),
                    "saved: [Sounds], [Sounds.Cart] and [Mount.Sounds] kept with their lines and comment, in their places" + (err != null ? " (" + err + ")" : ""));

                // a changed file: the pack read again (the dev auto-reload does this by itself), the bank's entry
                // forgotten and the new file decoded on the next play
                h.Tuning.Set(Owner, "Skin", "sounds-test");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "sounds-test", 8f);
                File.WriteAllBytes(Path.Combine(packDir, "sounds", "voice_1.wav"), SoundBank.TestTone(0.5f, 440f));
                File.WriteAllBytes(Path.Combine(packDir, "sounds", "voice_2.wav"), SoundBank.TestTone(0.5f, 520f));
                string reloadErr = Looks.ReloadCfg(def);
                Check(reloadErr == null && SoundBank.Find(voiceId) == null, "read again after a file changed: the bank's entry is forgotten" + (reloadErr != null ? " (" + reloadErr + ")" : ""));
                def = Looks.Find("sounds-test");
                rig.Invalidate();
                SoundSpec v3 = rig.Resolve("voice");
                yield return Until(() => v3 != null && SoundBank.StateOf(v3.BankId) == SoundBank.State.Ready, 6f);
                SoundBank.Entry ve3 = SoundBank.Find(v3?.BankId);
                Check(waited && ve3 != null && ve3.Clips.Length == 2 && Mathf.Abs(ve3.Clips[0].length - 0.5f) < 0.03f, $"... and the new files play ({(ve3 != null && ve3.Clips.Length > 0 ? ve3.Clips[0].length.ToString("0.00") : "-")} s now, 0.30 before)");

                // the clean-up: the skins withdrawn, the bank's entries gone
                Check(Looks.Unregister("sounds-test-2", owner) && Looks.Unregister("sounds-test", owner), "both skins withdrawn");
                yield return null;
                Check(SoundBank.Count == bankBefore && SoundBank.Find(voiceId) == null, $"the bank's entries are gone ({SoundBank.Count}, {bankBefore} before)");
            }
            finally
            {
                Plugin.For(MountStyle.Ride).SoundsOn.Value = soundsWas;
                try { if (hc != null && hc.Exists) hc.Remove(); } catch { }
                try { Looks.Unregister("sounds-test-2", owner); } catch { }
                LeftOver(owner, "sounds-test", tempRoot);
            }
        }

        // The public API (docs/mount-sounds-design.md section 9): the catalogue, what plays and from which layer, a mod's
        // own sound over everything (a game id, its own clip kept by the mod, silence) and back on Dispose, Play and
        // Stop with their reasons, the Played event; a code mount's MountSound shared by a pack skin that joins it.
        private static IEnumerator SoundsApi()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
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
                Check(cat.Count == SoundEvents.All.Length && Mounts.Sounds.Events.Contains("voice") && cat[0].Id == "step" && cat[0].Variants.Count == 4 && cat[0].Numbers.Contains("per_cycle"),
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
                    mountReg = Mounts.Catalog.Register(modOwner, new MountDef { Id = "sound-code-beast", Name = "Sound Code Beast", Sounds = new Dictionary<string, MountSound> { ["voice"] = MountSound.Game("donkey_bell"), ["lips"] = MountSound.None } });
                    tempRoot = WriteTempPack("gk2donkeyapi-selftest-soundapi", "soundapi-test", "[Skin]\nformat = 1\nid = soundapi-test\nname = Sound API Test\nmount = sound-code-beast\n");
                }
                catch (Exception e) { Warn("the code mount or the temp pack could not be made: " + e.Message); yield break; }
                MountInfo info = Mounts.Catalog.Find("sound-code-beast");
                Check(info != null && info.Sounds != null && info.Sounds["voice"] is MountSound, "MountInfo carries the code mount's sounds");
                Looks.RegisterFolder(tempRoot, modOwner);
                h.Tuning.Set(Owner, "Skin", "soundapi-test");
                yield return Until(() => h.Mount == "sound-code-beast", 8f);
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
                try { Looks.Unregister("soundapi-test", modOwner); } catch { }
                try { mountReg?.Dispose(); } catch { }
                try { if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                try { if (own != null) UnityEngine.Object.Destroy(own); } catch { }
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }

        // An embedded pack's sounds, the route the built-in skins' sounds take (Saddle Up's are embedded in its DLL):
        // the API's own test pack (assets\testskin, never registered at start), its .ogg decoded through the cache
        // file under BepInEx\cache, its .wav parsed in place by the convention name, a looping event stopped.
        private static IEnumerator SoundsEmbedded()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".embedded";
            MountHandle h = null;
            try
            {
                int n = Looks.RegisterEmbedded(typeof(Plugin).Assembly, "GK2DonkeyAPI.testskins.", owner);
                Looks.SkinDef def = Looks.Find("embedded-sound-test");
                Check(n == 1 && def != null && def.FileNames.Contains("sounds/voice_1.ogg") && def.FileNames.Contains("sounds/step_1.wav"),
                    $"the API's embedded test pack registers with its two sound files ({n} pack(s); {(def != null ? string.Join(", ", def.FileNames) : "-")})");
                if (def == null) yield break;
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "embedded-sound-test");
                yield return Until(() => h.I.LooksRig.DefNow != null && h.I.LooksRig.DefNow.Id == "embedded-sound-test", 8f);
                Sounds.Rig rig = h.I.SoundsRig;
                rig.Invalidate();
                SoundSpec st = rig.Resolve("step");
                Check(st != null && st.Layer == "skin" && st.BankId != null && SoundBank.StateOf(st.BankId) == SoundBank.State.Ready, $"step_1.wav by convention, parsed in place: ready at once ({st?.Describe()})");
                SoundBank.Entry se = SoundBank.Find(st?.BankId);
                Check(se != null && se.Clips.Length == 1 && Mathf.Abs(se.Clips[0].length - 0.25f) < 0.03f, $"... a 0.25 s clip ({(se != null && se.Clips.Length > 0 ? se.Clips[0].length.ToString("0.00") : "-")})");
                SoundSpec v = rig.Resolve("voice");
                string vid = v?.BankId;
                Check(v != null && v.Layer == "skin" && vid != null, $"voice_1.ogg: resolved to the bank ({v?.Describe()})");
                yield return Until(() => SoundBank.StateOf(vid) == SoundBank.State.Ready, 8f);
                SoundBank.Entry ve = SoundBank.Find(vid);
                Check(waited && ve != null && ve.Clips.Length == 1 && Mathf.Abs(ve.Clips[0].length - 0.4f) < 0.05f, $"... decoded through the cache file: a 0.4 s clip ({(ve != null && ve.Clips.Length > 0 ? ve.Clips[0].length.ToString("0.00") : "-")})");
                string cache = Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "GK2DonkeyAPI", "sounds", owner, "embedded-sound-test", "voice_1.ogg");
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
                try { Looks.Unregister("embedded-sound-test", owner); } catch { }
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }

        // The flying events (docs/mount-sounds-design.md 3.2) on a flier of the test's own, every event given a game
        // sound: the take-off, the launch's flaps and the beats airborne, the fly loop, the landing, the thud, landed;
        // a jump's own two; a jump plays none of the flight's.
        private static IEnumerator SoundsFlying()
        {
            if (!Flight.Active || !Plugin.MountsOn) { Skip(!Flight.Active ? "the flying experiment is off" : "the mounts are off (Debug.MountsPreview)"); yield break; }
            if (Mount.Instance.IsIndoors()) { Skip("indoors: flying indoors is off"); yield break; }
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".flysounds";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-flysounds", "flysound-test",
                    "[Skin]\nformat = 1\nid = flysound-test\nname = Fly Sound Test\nmount = flysound-beast\n\n" +
                    "[Sounds]\ntakeoff = fishing_cast_swoosh\nflap = conveyor_furnace_bellows\nglide = tree_wind\nfly = weather_wind\nland = bush_chop\ntouchdown = oh_wood_drop\nlanded = item_put\njump = equip_tool\njumpland = oh_wood_container_drop\n\n" +
                    "[Mount]\nid = flysound-beast\nname = Fly Sound Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\n");
            }
            catch (Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle h = null;
            try
            {
                Looks.RegisterFolder(tempRoot, owner);
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", "flysound-test");
                yield return Until(() => h.Mount == "flysound-beast", 8f);
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
                Check(waited && Plays("takeoff") == 1 && Plays("flap") >= Flight.Beats, $"the take-off: takeoff once, {Plays("flap")} flap(s) in the launch ({Flight.Beats} beats), fly {Plays("fly")}");
                Check(Plays("fly") == 1, "the fly loop started once");
                int afterLaunch = Plays("flap");
                yield return Seconds(2f);
                Check(Plays("flap") > afterLaunch, $"airborne: the flaps go on with the beat ({Plays("flap")} after two seconds, {afterLaunch} after the launch)");
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
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
                Looks.Unregister("flysound-test", owner);
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // The herd (docs/mount-sounds-design.md 3.3): a standing grown one calls by itself, rarer than ridden and never
        // within the scene's voice gap of another; its babies answer one after another, with [Sounds.Baby]'s sound
        // where the pack gives one; nothing of it with the standing switch off; Saddle Up's row drives the switch.
        private static IEnumerator SoundsHerd()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
            if (!LazyAudio.IsInitialized) { Skip("the game's audio is not up"); yield break; }
            const string owner = Owner + ".herd";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-herd", "herd-test",
                    "[Skin]\nformat = 1\nid = herd-test\nname = Herd Test\nmount = herd-beast\n\n[Sounds]\nvoice = donkey_bell\nvoice.idle_every = 1\n\n[Sounds.Baby]\nanswer = fishing_blop\n\n" +
                    "[Mount]\nid = herd-beast\nname = Herd Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
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
                a.Tuning.Set(Owner, "Skin", "herd-test");
                yield return Until(() => a.Mount == "herd-beast", 8f);
                k1 = Mounts.SpawnFoal(Owner, a);
                k2 = Mounts.SpawnFoal(Owner, a);
                yield return Until(() => k1 != null && k2 != null && k1.IsReady && k2.IsReady, 8f);
                if (k1 == null || k2 == null) { Fail("no foals could be spawned here"); yield break; }
                k1.Tuning.Set(Owner, "Skin", "herd-test");
                k2.Tuning.Set(Owner, "Skin", "herd-test");
                yield return Until(() => k1.Mount == "herd-beast" && k2.Mount == "herd-beast", 8f);
                yield return Frames(3);
                Sounds.Rig ra = a.I.SoundsRig, r1 = k1.I.SoundsRig, r2 = k2.I.SoundsRig;
                ra.Invalidate(); r1.Invalidate(); r2.Invalidate();
                int Plays(Sounds.Rig r, string e) => r.TestPlays.TryGetValue(e, out int c) ? c : 0;

                // the layers: the babies' block is the babies' only
                SoundSpec aa = ra.Resolve("answer"), ka = r1.Resolve("answer");
                Check(!ra.IsBaby && r1.IsBaby && r2.IsBaby, $"a grown one and two babies ({a.I.ViewScale:0.00}, {k1.I.ViewScale:0.00}, {k2.I.ViewScale:0.00})");
                Check(ka != null && ka.GameId == "fishing_blop" && ka.Layer == "skin", $"a baby's answer: [Sounds.Baby]'s ({ka?.Describe()})");
                Check(aa != null && aa.Layer == "base" && aa.GameId == Plugin.For(MountStyle.Ride).BraySoundId.Value, $"the grown one's answer: the base's, the babies' block not for it ({aa?.Describe()})");

                // the standing call and the answers
                Sounds.TestResetHerd();
                ra.TestPlays.Clear(); r1.TestPlays.Clear(); r2.TestPlays.Clear();
                yield return Until(() => Plays(ra, "voice") >= 1, 10f);
                SoundSpec av = ra.Resolve("voice");
                Check(waited, $"a standing grown one calls by itself within 10 s (idle_every {av?.Number("idle_every", -1f):0.##} x the standing factor {Sounds.StandingIdleFactor}; the rig: {ra.TestState}; voice {av?.Describe()})");
                yield return Until(() => Plays(r1, "answer") >= 1 && Plays(r2, "answer") >= 1, 3f);
                Check(waited, $"both babies answered within 3 s ({Plays(r1, "answer")}, {Plays(r2, "answer")})");
                var times = new List<(float At, Mount.Instance Who, string Event)>(Sounds.TestVoiceTimes);
                float call = -1f, first = -1f, second = -1f;
                foreach (var t in times)
                {
                    if (t.Event == "voice" && call < 0f) call = t.At;
                    else if (t.Event == "answer" && call >= 0f && t.At >= call) { if (first < 0f) first = t.At; else if (second < 0f) second = t.At; }
                }
                Check(call >= 0f && first >= call + 0.45f && first <= call + 1.0f && second > first + 0.2f, $"the answers come half a second on and one after the other (call at {call:0.00}, answers at {first:0.00} and {second:0.00})");

                // one voice at a time: a second grown one standing by, calls at least the gap apart
                Sounds.VoiceGapS = 4f;
                b = Spawn(Spot(home, 5f) ?? home, right: homeFacing > 0f);
                if (b != null)
                {
                    yield return Until(() => b.IsReady, 6f);
                    b.Tuning.Set(Owner, "Skin", "herd-test");
                    yield return Until(() => b.Mount == "herd-beast", 8f);
                    Sounds.TestResetHerd();
                    yield return Seconds(12f);
                    var calls = new List<float>();
                    foreach (var t in Sounds.TestVoiceTimes) if (t.Event == "voice") calls.Add(t.At);
                    float minGap = float.MaxValue;
                    for (int i = 1; i < calls.Count; i++) minGap = Mathf.Min(minGap, calls[i] - calls[i - 1]);
                    Check(calls.Count >= 2 && minGap >= Sounds.VoiceGapS - 0.1f, $"two standing grown ones: {calls.Count} call(s) in 12 s, the nearest two {(calls.Count >= 2 ? minGap.ToString("0.00") : "-")} s apart (at least {Sounds.VoiceGapS})");
                }
                else Info("no room for a second grown one: the voice gap not seen");

                // the standing switch off: nothing standing calls or answers
                SetStandingSounds(false);
                ra.Invalidate(); r1.Invalidate(); r2.Invalidate();
                Sounds.TestResetHerd();
                yield return Seconds(6f);
                Check(Sounds.TestVoiceTimes.Count == 0, $"StandingSounds off: no call and no answer in 6 s ({Sounds.TestVoiceTimes.Count})");
                SetStandingSounds(true);

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
            finally
            {
                Sounds.VoiceGapS = gapWas;
                SetStandingSounds(standingWas);
                try { if (k1 != null && k1.Exists) k1.Remove(); } catch { }
                try { if (k2 != null && k2.Exists) k2.Remove(); } catch { }
                try { if (b != null && b.Exists) b.Remove(); } catch { }
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                LeftOver(owner, "herd-test", tempRoot);
            }
        }

        // Every event with a base: the resolved spec equals the style's keys. Returns how many matched.
        private static int CheckBase(Sounds.Rig rig, StyleSettings s, MountStyle style)
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
    }
}
