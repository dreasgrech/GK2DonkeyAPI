using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // A built-in skin on a spawned donkey: drawn (its overlay shows, the donkey's own body kept from rendering),
        // its pictures loaded; cleared: the donkey's own body again; removed: the pictures freed.
        internal static IEnumerator SkinOnOff()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            // a built-in nobody wears at the moment: a skin's sheet is one texture shared by its wearers (refcounted), so
            // the counts below only move for a skin that is not loaded yet (the player's own mount may be the rat)
            string id = null;
            foreach (string s in new[] { "rat", "worm", "snail", "dragon" })
                if (Looks.Find(Sk(s)) != null && !Looks.IsLoaded(Sk(s))) { id = Sk(s); break; }
            if (id == null)
            {
                Warn("every built-in creature skin is worn by someone already (the counts can't be read): skipped");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int tex0 = Live.Textures, loaded0 = Looks.LoadedCount;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", id);
            yield return Until(() => a != null && a.IsReady && a.I.TestSkinShowing(), 8f);
            Check(waited, $"the {id} skin shows (an overlay drawn, the donkey's own body kept from rendering)");
            Check(Looks.LoadedCount == loaded0 + 1 && Live.Textures == tex0 + 1, $"its pictures are loaded on one sheet ({tex0} -> {Live.Textures} textures)");
            a?.Tuning.Set(Owner, "Skin", "");
            yield return Frames(3);
            Check(a != null && !a.I.TestSkinShowing(), "cleared: the donkey's own body renders again");
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"cleared: its pictures freed ({Live.Textures} textures)");
            a?.Tuning.Set(Owner, "Skin", id);
            yield return Until(() => a != null && a.I.TestSkinShowing(), 4f);
            a?.Remove();
            yield return Frames(2);
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"removed while wearing it: pictures freed ({tex0} -> {Live.Textures})");
        }

        // Many skinned donkeys spawned and removed, twice: the skin loads once, and everything is freed at the end.
        internal static IEnumerator SkinLeaks()
        {
            string noPack = MissingPack("rat", "snail"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            if (Looks.Find(Sk("rat")) == null)
            {
                Warn("no built-in skin 'rat': skipped");
                yield break;
            }
            yield return Settle(10f);
            int tex0 = Live.Textures, loaded0 = Looks.LoadedCount, inst0 = Mount.All.Count;
            // a skin one of them already wears (the settings' paper donkey in the snail after the Mounts panel was
            // open, 2026-10-18) loads nothing new: the test's two skins count only when they aren't loaded yet
            int newSkins = (Looks.IsLoaded(Sk("rat")) ? 0 : 1) + (Looks.IsLoaded(Sk("snail")) ? 0 : 1);
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                var made = new List<MountHandle>();
                for (int k = 0; k < 40; k++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 30f;
                    MountHandle h = Spawn(home + new Vector3(r.x, 0f, r.y), k % 2 == 0, withCart: k % 10 == 0);
                    if (h != null)
                    {
                        h.Tuning.Set(Owner, "Skin", k % 3 == 0 ? Sk("snail") : Sk("rat"));
                        made.Add(h);
                    }
                }
                yield return Seconds(4f);
                Check(Looks.LoadedCount == loaded0 + newSkins, $"cycle {cycle}: {newSkins} skin(s) loaded for {made.Count} donkeys ({Looks.LoadedCount - loaded0} new; {2 - newSkins} of the two were loaded already)");
                foreach (MountHandle h in made)
                {
                    if (h.Exists) h.Remove();
                }
                yield return Seconds(2f);
            }
            yield return Settle(10f);
            Check(Mount.All.Count == inst0, $"instances back ({inst0} -> {Mount.All.Count})");
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"skins unloaded, textures back ({tex0} -> {Live.Textures}, loaded {loaded0} -> {Looks.LoadedCount})");
        }

        // A wearer's sheet stays through a view rebuild (an unhitch restyles: Detach, not Sleep; before 0.35.0's review the
        // sheet was freed and built again there, a pop to the donkey), goes when its only wearer sleeps far away (the
        // sleep tier: Sleep), and is built again when it wakes (docs/mounts-foundation-2.md 2.5).
        internal static IEnumerator SkinSleep()
        {
            string noPack = MissingPack("rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            string skin = null;
            foreach (string id in new[] { "rat", "snail", "worm" }) if (Looks.Find(Sk(id)) != null && !Looks.IsLoaded(Sk(id))) { skin = Sk(id); break; }
            if (skin == null)
            {
                Warn("the rat, snail and worm are all worn already (or none is installed): no sheet of its own to watch; skipped");
                yield break;
            }
            yield return Settle(10f);
            Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
            if (far == null)
            {
                Warn("no spot 42-50 away for the sleeping checks: skipped");
                yield break;
            }
            int loaded0 = Looks.LoadedCount;
            Vector3 near = home + new Vector3(4f, 0f, 0f);
            MountHandle a = Spawn(near, true, withCart: true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            MountHandle left = null;
            try
            {
                a.Tuning.Set(Owner, "Skin", Sk(skin));
                yield return Until(() => a.IsReady && a.I.TestSkinShowing(), 10f);
                if (!waited)
                {
                    Fail($"never wore {skin}");
                    yield break;
                }
                Check(Looks.IsLoaded(skin), $"worn: its sheet loaded ({loaded0} -> {Looks.LoadedCount})");
                string mountId = Looks.Find(skin).Mount.Id;
                // 1. with its cart, its only wearer asleep far away (a cart is not parked: the view is destroyed): the sheet goes
                for (int round = 1; round <= 2; round++)
                {
                    string how = round == 1 ? "with its cart (the view destroyed)" : "without a cart (the view parked)";
                    MountResult mv = a.MoveTo(far.Value);
                    if (!mv.Ok)
                    {
                        Warn($"could not move it far away ({mv.Reason}): the sleeping check {how} skipped");
                        yield break;
                    }
                    yield return Until(() => a.I.tier == Mount.Tier.Asleep && !a.I.HasViewObject, 8f);
                    if (!waited)
                    {
                        Warn($"not asleep without a view {Vector3.Distance(home, far.Value):F0} away (level {a.I.tier}): the sleeping check {how} skipped");
                        yield break;
                    }
                    yield return Frames(2);
                    Check(!Looks.IsLoaded(skin), $"asleep far away {how}, its only wearer: the sheet is freed ({Looks.LoadedCount} loaded)"); // the one sheet, not the count: other sleepers and wakers move the count
                    Check(a.Mount == mountId, $"asleep it is still the mount ({a.Mount})");
                    // back near the keeper: worn again
                    mv = a.MoveTo(near);
                    if (!mv.Ok)
                    {
                        Warn($"could not move it back ({mv.Reason}): the waking check skipped");
                        yield break;
                    }
                    yield return Until(() => a.IsReady && a.I.TestSkinShowing(), 12f);
                    Check(waited, $"awake again {how}: it wears {skin} again ({Looks.LoadedCount - loaded0} sheet loaded)");
                    if (!waited) yield break;
                    if (round == 1)
                    {
                        // 2. unhitched: the view is made again (a restyle); the sheet stays loaded through it
                        left = Mounts.Unhitch(a);
                        bool dropped = false;
                        for (int f = 0; f < 150 && !(a.IsReady && a.I.TestSkinShowing()); f++)
                        {
                            if (!Looks.IsLoaded(skin)) dropped = true;
                            yield return Frames(1);
                        }
                        Check(a.IsReady && a.I.TestSkinShowing(), "unhitched: it wears the skin on its new view");
                        Check(!dropped, dropped ? "unhitched: the sheet was freed and built again (a pop to the donkey)" : "unhitched: the sheet never left (no rebuild, no pop)");
                        if (left != null && left.Exists) left.Remove();
                        left = null;
                    }
                }
            }
            finally
            {
                if (a.Exists) a.Remove();
                if (left != null && left.Exists) left.Remove();
            }
            yield return Frames(3);
            Check(!Looks.IsLoaded(skin), $"removed: the sheet freed ({Looks.LoadedCount} loaded)");
        }

        // Saddle Up's Hellfire: lit on a spawned donkey (the game's lamp fire copied, its emitters on the ring), follows
        // it, out when no longer asked for, gone when the donkey is removed (nothing of it left in the world). Nothing
        // picks an effect in this version: the rig is told through its test hook (TestWant).
        internal static IEnumerator EffectOnOff()
        {
            string noPack = MissingPack("hellfire"); // Hellfire's effect is the Donkey Skins pack's
            if (noPack != null) { Skip(noPack); yield break; }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            yield return Until(() => a != null && a.IsReady, 6f);
            if (a != null) a.I.EffectsRig.TestWant = "saddleup-donkey-skins.hellfire";
            yield return Until(() => a != null && a.I.TestEffectLit(), 6f);
            if (!waited)
            {
                Warn("the fire never lit (no lamp flame in memory, or indoors): skipped");
                a?.Remove();
                yield break;
            }
            int n = a.I.TestEffectEmitters();
            Check(n == 9, $"the ring has its six flames and three sparks ({n} emitters)");
            Vector3? ringAt = a.I.TestEffectRingPosition();
            Check(ringAt.HasValue, "the ring is in the world");
            Vector3 before = ringAt ?? Vector3.zero;
            MountResult moved = a != null ? a.MoveTo(near + new Vector3(3f, 0f, 0f)) : MountResult.Refused("gone");
            Check(moved.Ok, "moved it: " + (moved.Ok ? "ok" : moved.Reason));
            yield return Seconds(1f);
            ringAt = a?.I.TestEffectRingPosition();
            Check(ringAt.HasValue && (ringAt.Value - before).sqrMagnitude > 0.25f, "the ring followed the donkey");
            if (a != null) a.I.EffectsRig.TestWant = null;
            yield return Frames(3);
            Check(a != null && !a.I.TestEffectLit(), "no longer asked for: the fire is out");
            if (a != null) a.I.EffectsRig.TestWant = "saddleup-donkey-skins.hellfire";
            yield return Until(() => a != null && a.I.TestEffectLit(), 4f);
            int lit = a != null ? a.I.TestEffectInWorld() : 0;
            a?.Remove();
            yield return Frames(3);
            int left = a != null ? a.I.TestEffectInWorld() : 0;
            Check(lit > 0 && left == 0, $"removed while lit: nothing of its fire left in the world ({lit} -> {left} pieces)");
        }

        internal static IEnumerator GameCreature()
        {
            if (Looks.Find(Sk("gerry")) == null)
            {
                Fail($"the built-in skin '{Sk("gerry")}' isn't registered");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            a.Tuning.Set(Owner, "Skin", Sk("gerry"));
            // Wearing is set when the skin is acquired; the sheet fills a few frames a tick (44 reads with a blit each),
            // and the pictures' counts are known once it is whole (Ready): wait for that, not for the acquire.
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("gerry") && Looks.TestCellOpaque(Sk("gerry"), "bdy_static_left") >= 0, 30f);
            if (!waited)
            {
                Fail($"the skin never became whole (the last game-source build: {Looks.LastGameSourceNote}; set aside: {Looks.Find(Sk("gerry"))?.Unavailable ?? "no"})");
                a.Remove();
                yield break;
            }
            yield return Frames(3);
            Check(Looks.LastGameSourceFound >= 60, $"Gerry's frames found: {Looks.LastGameSourceNote} (73 expected)");
            Check(a.Mount == M("gerry"), $"the wearer is the Gerry mount ({a.Mount})");
            int rest = Looks.TestCellOpaque(Sk("gerry"), "bdy_static_left");
            int walk = Looks.TestCellOpaque(Sk("gerry"), "bdy_walk_left_05");
            int turn = Looks.TestCellOpaque(Sk("gerry"), "bdy_turn_left_03");
            int head = Looks.TestCellOpaque(Sk("gerry"), "hed_static_down");
            Check(rest > 9 * 100, $"the rest picture is drawn and scaled 3x ({rest} opaque pixels; Gerry's skull is about 200 pixels, so 1800 or more)");
            Check(walk > 0 && turn > 0, $"the walk ({walk}) and the somersault ({turn}) pictures are drawn");
            Check(head == 0, $"the head key is empty on purpose, so nothing of the donkey's head shows ({head})");
            Check(a.Tuning.IsSet("SeatAdjustPx") && a.Tuning.SetBy("SeatAdjustPx") == Looks.TuningOwner && a.Tuning.Get<int>("SeatAdjustPx") == -16, $"the Gerry skin's one seat value ([Tuning] SeatAdjustPx = -16, both styles: the skin owns its geometry) is on the wearer ({(a.Tuning.IsSet("SeatAdjustPx") ? a.Tuning.Get<int>("SeatAdjustPx").ToString() : "not set")})");
            // seat = picture: the rider's seat is the skull picture's top at the pivot column, above the ground and
            // within the picture (160 px canvas: under 3.2 units) whatever the player's AutoSeat / SeatOffset
            bool seatOnPicture = a.I.LooksRig.TrySeatTop(out Vector3 seatTop);
            float seatUp = seatTop.y - a.Position.y;
            Check(seatOnPicture && seatUp > 0.2f && seatUp < 6f, $"the seat is the picture's own top, {seatUp:0.00} above the ground");
            // the babies' size lives on the mount: Gerry's 0.33 (its skin.cfg), the donkey's 0.6
            MountInfo gk = Mounts.Catalog.Find(M("gerry"));
            Check(gk != null && Math.Abs(gk.FoalScale - 0.33f) < 0.001f, $"the Gerry mount's babies are 0.33x ({gk?.FoalScale})");
            Check(gk != null && !gk.IsBuiltIn && gk.FromPack && gk.Author == "dreas" && gk.Version == "1.0", $"Saddle Up's mount is from its pack, not built into the API (Saddle Up ships the five mounts since 2026-10-20), with the file's author and version ({gk?.Author} {gk?.Version})");
            Check(gk != null && gk.Noun == "skull" && a.I.Noun == "skull", $"the mount's noun is the file's ('{gk?.Noun}'; the sentence word '{a.I.Noun}')");
            SoundSpec gerryLips = a.I.SoundsRig.Resolve("lips");
            Check(gerryLips != null && gerryLips.Silent && !a.Tuning.IsSet("LipsVolume"), $"the skull's lip smack is silent by its mount (mount.cfg [Sounds] lips = none; no LipsVolume pin since the sound events): {gerryLips?.Describe()}");
            // the babies: a baby spawned without a size is the parent mount's own (0.33)
            MountHandle baby = Mounts.SpawnFoal(Owner, a);
            yield return Until(() => baby != null && baby.IsReady, 6f);
            Check(baby != null && Math.Abs(baby.I.ViewScale - 0.33f) < 0.03f, $"a baby spawned without a size is the mount's 0.33 ({baby?.I.ViewScale:0.00})");
            baby?.Remove();
            // a built-in's "Reload from the file" (the Mounts tab, the undo) reads the repo's cfg where the sources are
            // (2026-10-20: it threw on the pack folder a built-in doesn't have, so the panel's undo never took)
            Looks.SkinDef gd = Looks.Find(Sk("gerry"));
            if (gd != null && Looks.CfgPathFor(gd) != null)
            {
                string rl = Looks.ReloadCfg(gd);
                Catalog.Entry gkd = Catalog.Find(M("gerry"));
                Looks.SkinDef gdAgain = Looks.Find(Sk("gerry"));
                Check(rl == null && gkd != null && gdAgain != null && gdAgain.Tuning != null && gdAgain.Tuning.TryGetValue("SeatAdjustPx", out string sv) && sv.Trim() == "-16" && (gkd.Tuning == null || !gkd.Tuning.ContainsKey("SeatAdjustPx")), "a built-in reloads from the repo's cfg: the seat value is the skin's [Tuning], the mount holds no pixel key" + (rl != null ? " (" + rl + ")" : ""));
            }
            else Info("no sources on this machine: the built-in reload is not checked");
            MountInfo dk = Mounts.Catalog.Find(MountCatalog.DonkeyId);
            Check(dk != null && Math.Abs(dk.FoalScale - 0.6f) < 0.001f, $"the donkey's babies are 0.6x ({dk?.FoalScale})");
            a.Remove();
            yield return Frames(3);

            // A Gerry asleep far away, with no view to draw the skin on, is still a Gerry: its mount comes from its
            // "Skin" key, not from the sheet it wears (2026-10-18: a sleeping parent counted as a donkey and its
            // babies stayed at 0.6 until it woke).
            Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
            if (far == null)
            {
                Warn("no spot 42-50 away for the sleeping check: skipped");
                yield break;
            }
            MountHandle s = Spawn(far.Value, true);
            if (s == null)
            {
                Fail("could not spawn far away");
                yield break;
            }
            s.Tuning.Set(Owner, "Skin", Sk("gerry"));
            yield return Until(() => s.I.tier == Mount.Tier.Asleep && !s.I.HasViewObject, 6f);
            if (!waited)
            {
                Warn($"not asleep without a view {Vector3.Distance(home, far.Value):F0} away (level {s.I.tier}): the sleeping check skipped");
            }
            else
            {
                Check(s.Mount == M("gerry") && s.I.Noun == "skull", $"asleep without a view it is still the Gerry mount ({s.Mount}, \"{s.I.Noun}\")");
                s.Tuning.Set(Owner, "Skin", "");
                Check(s.Mount == MountCatalog.DonkeyId, $"the key cleared: a donkey again ({s.Mount})");
            }
            s.Remove();
            yield return Frames(2);
        }

        // A game-creature skin (LooksGame.cs): Gerry's 0901_ frames are found in memory or through a sprite atlas,
        // every base key gets a picture (the mapped ones drawn, the head keys empty on purpose), scaled onto the 128
        // canvas, and a donkey wears it as the Gerry mount.
        // The seat as the game applies it (docs/seat-investigation.md): every built-in skin ridden with the player's
        // AutoSeat off (the default for everyone) and on, the seat read back in art pixels with the keys that made
        // it (the instance's SeatOffset / SeatAdjustPx and who set them). A readout, not a judgement: it fails only
        // when a mount can't be ridden. The player's AutoSeat value is put back at the end.
        internal static IEnumerator SeatReadout()
        {
            string noPack = MissingPack("hellfire", "royal-parade", "dragon", "rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            StyleSettings ride = Plugin.For(MountStyle.Ride);
            bool autoWas = ride.AutoSeat.Entry.Value;
            const float px = 0.02f * 1.6667f; // Mount.PxWorldY: one art pixel along the seat's axis
            var lines = new List<string>();
            var seats = new Dictionary<string, float>(StringComparer.Ordinal); // "<id>|off" / "<id>|on" -> the seat in px
            string[] skins = { "", "hellfire", "royal-parade", "rat", "snail", "worm", "dragon", "gerry" };
            foreach (bool auto in new[] { false, true })
            {
                ride.AutoSeat.Entry.Value = auto;
                foreach (string id in skins)
                {
                    Vector3 near = Spot(home, 4f) ?? home;
                    MountHandle a = Spawn(near, true);
                    if (a == null)
                    {
                        Fail("could not spawn");
                        continue;
                    }
                    if (id.Length > 0) a.Tuning.Set(Owner, "Skin", Sk(id));
                    yield return Until(() => a.IsReady && (id.Length == 0 || a.I.TestSkinWearing() == Sk(id)), 30f);
                    if (!waited)
                    {
                        Fail($"'{id}' never became whole");
                        a.Remove();
                        continue;
                    }
                    yield return Frames(3);
                    MountResult r = a.Ride(showMessage: false);
                    if (!r.Ok)
                    {
                        Fail($"'{(id.Length == 0 ? "donkey" : id)}' can't be ridden: {r.Reason}");
                        a.Remove();
                        continue;
                    }
                    yield return Until(() => Mounts.IsMounted, 5f);
                    yield return Frames(8); // the seat settles (the picture seat, the auto seat, the bob)
                    float seat = a.I.TestSeatApplied;
                    seats[(id.Length == 0 ? "donkey" : id) + (auto ? "|on" : "|off")] = seat / px;
                    if (id == "snail" && !auto)
                    {
                        Check(a.I.SoundsRig.Resolve("voice") is SoundSpec snailVoice && snailVoice.Silent, "the snail is silent: its voice resolves to none (its [Mount.Sounds]; the donkey's own sounds never reach another mount)");
                        MountSkinInfo snailInfo = Mounts.Looks.Find(Sk("snail"));
                        var snailRegions = snailInfo != null ? new List<string>(snailInfo.Regions) : new List<string>(); Check(snailInfo != null && snailRegions.Contains("Muzzle") && !snailRegions.Contains("Harness") && !MountSettingsPage.MountHasRegion(M("snail"), "Harness"), "the snail's regions are Coat, Dark and Muzzle: a Harness row greys for it");
                        // the legs (NearLeg on) hang from the skin's seat row, not the donkey's back line
                        a.Tuning.Set(Owner, "NearLeg", true);
                        yield return Frames(6);
                        float legY = RiderLeg.TestLocalY;
                        bool seatOk = a.I.LooksRig.TrySeatTop(out Vector3 seatTop, out _);
                        SpriteRenderer body = a.I.TestBodyRenderer;
                        float want = seatOk && body != null ? body.transform.InverseTransformPoint(seatTop).y + ride.LegAdjustYPx.Entry.Value * 0.02f : float.NaN;
                        Check(!float.IsNaN(legY) && !float.IsNaN(want) && Math.Abs(legY - want) < 0.02f * 1.5f, $"the leg overlay hangs from the snail's seat row ({legY:0.000} vs {want:0.000}, within 1.5 px)");
                        a.Tuning.Clear(Owner, "NearLeg");
                    }
                    string who(string key) => a.Tuning.IsSet(key) ? $"{key} = {a.Tuning.Get<object>(key)} ({(a.Tuning.SetBy(key) == Looks.TuningOwner ? "the mount" : a.Tuning.SetBy(key))})" : $"{key} = the player's {(key == "SeatOffset" ? ride.SeatOffset.Entry.Value.ToString("0.00") : ride.SeatAdjustPx.Entry.Value.ToString())}";
                    string seatDecl = a.I.LooksRig.TrySeatTop(out _, out bool fixedRow) ? (fixedRow ? $"seat = {Looks.Find(Sk(id))?.SeatRow}; " : "seat = picture; ") : "";
                    lines.Add($"AutoSeat {(auto ? "on " : "off")} | {(id.Length == 0 ? "donkey" : id),-13} mount {a.Mount,-7} | seat {seat / px,6:0.0} px ({seat:0.000}) | {seatDecl}{who("SeatOffset")}; {who("SeatAdjustPx")}");
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                    a.Remove();
                    yield return Frames(2);
                }
            }
            ride.AutoSeat.Entry.Value = autoWas;
            foreach (string l in lines) Info(l);
            Check(lines.Count == skins.Length * 2, $"{lines.Count} of {skins.Length * 2} rides read out (the lines above: docs/seat-investigation.md)");
            // the seat row (docs/seat-investigation.md, option A, built 2026-10-20): a skin that declares one seats the
            // rider the same whatever the player's AutoSeat; the game's own donkey art declares none and keeps the
            // player's two seats exactly as they were (6.6 px off, the compatibility rule)
            float At(string k) => seats.TryGetValue(k, out float v) ? v : float.NaN;
            Check(Math.Abs(At("donkey|off") - 6.6f) < 0.3f, $"the plain donkey's manual seat is untouched: {At("donkey|off"):0.0} px (6.6)");
            foreach (string id in new[] { "rat", "worm" })
                Check(Math.Abs(At(id + "|off") - 6.6f) < 0.8f && Math.Abs(At(id + "|on") - At(id + "|off")) < 0.3f, $"{id}: seat = 52 seats the rider where the donkey's seat was, in both modes ({At(id + "|off"):0.0} / {At(id + "|on"):0.0} px)");
            // the dragon's body 6 px higher on its longer legs (make_creatures.py RAISE, the user's pick 2026-10-23): its
            // seat row 68 -> 62, the rider on its raised back 6 px above the donkey's seat
            Check(Math.Abs(At("dragon|off") - (6.6f + DragonRaisePx)) < 0.8f && Math.Abs(At("dragon|on") - At("dragon|off")) < 0.3f, $"dragon: seat = 62 seats the rider {DragonRaisePx:0} px above the donkey's seat (its raised body), in both modes ({At("dragon|off"):0.0} / {At("dragon|on"):0.0} px)");
            Check(Math.Abs(At("snail|off") - 3.5f) < 0.8f && Math.Abs(At("snail|on") - At("snail|off")) < 0.3f, $"snail: seat = 55 seats the rider 3 px lower in both modes ({At("snail|off"):0.0} / {At("snail|on"):0.0} px)");
            Check(Math.Abs(At("gerry|on") - At("gerry|off")) < 0.3f, $"gerry: the picture seat is the same in both modes ({At("gerry|off"):0.0} / {At("gerry|on"):0.0} px)");
            // the skin's own SeatAdjustPx = -16 (an override on the instance, owned by Looks) counts on a declared seat;
            // 25.5 px is the recorded value (docs/seat-investigation.md 8); 41.5 would mean the nudge was dropped
            Check(Math.Abs(At("gerry|off") - 25.5f) < 0.8f, $"gerry: the skin's own SeatAdjustPx = -16 nudges the picture seat ({At("gerry|off"):0.0} px, 25.5 recorded)");
        }

        // The cart on every built-in mount (docs/mounts-parity-matrix.md 1.2, 6.2): the gap between the body's rear edge
        // and the cart body, and the seat in the cart style, read back in art px. The rat's declared gap must put the cart
        // as far from its body as the donkey's cart is from the donkey's; the snail, worm and Gerry are read out, and
        // their gaps declared from these numbers (M2b of the plan). The dragon pulls no cart (the user, 2026-10-25): a
        // spawn with a cart wearing it is refused instead.
        internal static IEnumerator CartReadout()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            const float pxX = 0.02f;            // Mount.PxWorld: one art pixel along the ground
            const float pxY = 0.02f * 1.6667f;  // Mount.PxWorldY: one art pixel along the seat's axis
            var clearance = new Dictionary<string, float>(StringComparer.Ordinal);
            var seatsCart = new Dictionary<string, float>(StringComparer.Ordinal);
            var lines = new List<string>();
            string[] skins = { "", "rat", "snail", "worm", "gerry" };
            foreach (string id in skins)
            {
                string name = id.Length == 0 ? "donkey" : id;
                Vector3 near = Spot(home, 5f) ?? home;
                MountHandle a = Spawn(near, true, withCart: true);
                if (a == null) { Fail($"{name}: could not spawn with a cart"); continue; }
                if (id.Length > 0) a.Tuning.Set(Owner, "Skin", Sk(id));
                yield return Until(() => a.IsReady && a.HasCart && (id.Length == 0 || a.I.TestSkinWearing() == Sk(id)), 30f);
                if (!waited) { Fail($"{name}: never ready with its cart"); a.Remove(); continue; }
                yield return Frames(6);
                Vector3 dir = a.I.HitchDirWorld;
                if (dir == Vector3.zero) { Fail($"{name}: no hitch direction (no live cart body)"); a.Remove(); continue; }
                // the body's rear edge: a skin's rearmost opaque column of the standing frame; the donkey's own sprite bounds
                Vector3 rear = default;
                bool haveRear;
                if (id.Length > 0)
                {
                    (int Min, int Max)? cols = a.I.LooksRig.BodyColumns("bdy_static_left");
                    haveRear = cols.HasValue && a.I.LooksRig.TryBodyColumnWorld(cols.Value.Max, out rear);
                    if (!haveRear) rear = default;
                }
                else
                {
                    SpriteRenderer body = a.I.TestBodyRenderer;
                    haveRear = body != null && body.sprite != null;
                    rear = haveRear ? Far(body.bounds, dir) : default;
                }
                Renderer cartBody = a.I.TestCartPartRenderer("donkey_cart_empty_sway") ?? a.I.TestCartPartRenderer("donkey_cart_bars");
                if (!haveRear || cartBody == null) { Fail($"{name}: no body edge or cart body to measure ({haveRear}, {cartBody != null})"); a.Remove(); continue; }
                float gapPx = Vector3.Dot(Near(cartBody.bounds, dir) - rear, dir) / pxX;
                clearance[name] = gapPx;
                // the cart is rigid: every part shifted by the hitch gap alike (a wheel a wheel's width out of place would show here)
                List<(string Name, float ShiftPx)> shifts = a.I.TestCartPartShifts(dir);
                float lo = float.MaxValue, hi = float.MinValue;
                var parts = new System.Text.StringBuilder();
                foreach ((string n, float s) in shifts) { lo = Math.Min(lo, s); hi = Math.Max(hi, s); parts.Append(parts.Length > 0 ? ", " : "").Append(n).Append(' ').Append(s.ToString("0.0")); }
                Info($"{name}: cart parts shifted along the hitch direction (px): {parts}");
                Check(shifts.Count > 0 && hi - lo < 1f, $"{name}: the cart's parts moved as one ({lo:0.0} to {hi:0.0} px)");
                // the seat with the cart hitched (the cart style)
                MountResult r = a.Ride(showMessage: false);
                float seatPx = float.NaN;
                if (r.Ok)
                {
                    yield return Until(() => Mounts.IsMounted, 5f);
                    yield return Frames(8);
                    seatPx = a.I.TestSeatApplied / pxY;
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                }
                seatsCart[name] = seatPx;
                lines.Add($"{name,-7} mount {a.Mount,-7} | hitch gap {a.I.HitchGapWorld / pxX,5:0.0} px | shaft tips past the body's rear {gapPx,6:0.0} px | cart-style seat {(float.IsNaN(seatPx) ? "not ridden: " + r.Reason : seatPx.ToString("0.0") + " px")}");
                a.Remove();
                yield return Frames(2);
            }
            foreach (string l in lines) Info(l);
            Check(lines.Count == skins.Length, $"{lines.Count} of {skins.Length} mounts read out with a cart (the lines above: docs/mounts-parity-matrix.md 1.2)");
            // The measure is the cart mesh's front edge (the shaft tips) past the body's rear edge, negative = the shafts
            // reach along the body, as they do on the donkey (-30 px). The rule (docs/seat-investigation.md 9): no mount's
            // cart stands closer to its body than the donkey's; the declared gap (the rat's 9, judged by eye; the dragon's 24
            // until it stopped pulling the cart) puts it about 10 px further, the snail, worm and Gerry are narrower and
            // stand further without a gap.
            if (clearance.TryGetValue("donkey", out float donkeyGap))
            {
                foreach (string id in new[] { "rat", "snail", "worm", "gerry" })
                    Check(clearance.TryGetValue(id, out float g) && g >= donkeyGap - 2f, $"{id}: the cart stands no closer to its body than the donkey's ({(clearance.TryGetValue(id, out float gg) ? gg.ToString("0.0") : "?")} vs {donkeyGap:0.0} px)");
            }
            // the cart-style seat: a declared row seats the rider the same as riding (the player's SeatAdjustPx, whose
            // default differs by style, does not count on a declared seat), so the mounts sit at the donkey's 6.6 px
            // with the cart too, the snail 3 px lower
            if (seatsCart.TryGetValue("donkey", out float donkeySeat) && !float.IsNaN(donkeySeat))
            {
                foreach (string id in new[] { "rat", "worm" })
                    Check(seatsCart.TryGetValue(id, out float sp) && Math.Abs(sp - donkeySeat) < 0.8f, $"{id}: the cart-style seat is the donkey's ({(seatsCart.TryGetValue(id, out float s2) ? s2.ToString("0.0") : "?")} vs {donkeySeat:0.0} px)");
                Check(seatsCart.TryGetValue("snail", out float ss) && Math.Abs(ss - (donkeySeat - 3f)) < 0.8f, $"snail: the cart-style seat is 3 px lower than the donkey's ({(seatsCart.TryGetValue("snail", out float s3) ? s3.ToString("0.0") : "?")} vs {donkeySeat:0.0} px)");
                Check(seatsCart.TryGetValue("gerry", out float sg) && Math.Abs(sg - 25.5f) < 0.8f, $"gerry: the cart-style seat is the picture seat with the skin's -16 ({(seatsCart.TryGetValue("gerry", out float s4) ? s4.ToString("0.0") : "?")} px, 25.5 recorded)");
            }
            // the dragon pulls no cart (the user, 2026-10-25): a spawn with a cart wearing its skin is refused (the pack rule's door)
            if (Looks.Find(Sk("dragon")) != null)
            {
                MountHandle dr = Mounts.Spawn(Owner, new MountSpawn { Position = Spot(home, 5f) ?? home, WithCart = true, Skin = Sk("dragon") });
                Check(dr == null, "the dragon: a spawn with a cart wearing its skin is refused (it pulls no cart)");
                dr?.Remove();
            }
        }

        // The ring is the worn skin's drawn width against the donkey's (the rat: 85 of 46 px; an over skin keeps the
        // donkey's). Nothing picks an effect in this version, so the rig is told through its test hook (TestWant).
        internal static IEnumerator SkinEffect()
        {
            string noPack = MissingPack("hellfire", "rat"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null) { Fail("could not spawn"); yield break; }
            a.I.EffectsRig.TestWant = "saddleup-donkey-skins.hellfire";
            a.Tuning.Set(Owner, "Skin", Sk("hellfire"));
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("hellfire") && a.I.EffectsRig.Lit, 20f);
            Check(waited, "Hellfire worn, the effect asked for by the test hook: lit");
            float donkeyRing = a.I.EffectsRig.TestRingRadiusX;
            Check(donkeyRing > 0.6f && donkeyRing < 1.0f, $"the ring on the donkey-shaped Hellfire reaches {donkeyRing:0.00} along the body (0.8: an over skin keeps the donkey's size)");
            a.Tuning.Set(Owner, "Skin", Sk("rat"));
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("rat") && a.I.EffectsRig.Lit && a.I.EffectsRig.TestRingRadiusX > 1.2f, 20f);
            float ratRing = a.I.EffectsRig.TestRingRadiusX;
            Check(waited && Math.Abs(ratRing - 0.8f * 85f / 46f) < 0.2f, $"the ring on the rat reaches {ratRing:0.00} along the body (0.8 x 85/46 = 1.48: the rat is 85 px wide to the donkey's 46)");
            a.Remove();
            yield return Frames(2);
        }

        // The front layer on the built-in that has one (docs/mounts-parity-matrix.md 6.1): the dragon's near wing,
        // drawn in front of the rider. Read back through the game's own camera at one spot: the body overlay, the
        // front picture and the keeper's body each alone, then each with the keeper; where two overlap, the pair's
        // pixels show whose colour is in front: the keeper's over the body, the wing's over the keeper.
        internal static IEnumerator FrontLayer()
        {
            string noPack = MissingPack("dragon"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null) { Fail("could not spawn"); yield break; }
            a.Tuning.Set(Owner, "Skin", Sk("dragon"));
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("dragon") && a.I.LooksRig.TestFrontShowing(), 30f);
            Check(waited, "the dragon is worn and its front layer shows");
            MountResult r = a.Ride(showMessage: false);
            Check(r.Ok, "ridden: " + (r.Ok ? "ok" : r.Reason));
            if (r.Ok)
            {
                yield return Until(() => Mounts.IsMounted && a.I.KeeperBody != null && a.I.KeeperBody.sprite != null, 6f);
                yield return Frames(8);
                SpriteRenderer front = a.I.LooksRig.TestFront(), ov = a.I.LooksRig.TestOverlay(), keeper = a.I.KeeperBody;
                if (front != null && ov != null && keeper != null)
                {
                    Vector3 at = ov.bounds.center;
                    float hipRow = a.I.TestHipCanvasRow();
                    int seatRow = Looks.Find(Sk("dragon"))?.SeatRow ?? 62; // the skin's own (62 since the longer legs, 2026-10-23)
                    Info($"the rider's hip sits on canvas row {hipRow:0.0} of the dragon's 128 (declared seat = {seatRow}; the review strips draw him there)");
                    Check(!float.IsNaN(hipRow) && Math.Abs(hipRow - seatRow) < 2.5f, $"the rider's hip is on the declared row ({hipRow:0.0} vs {seatRow})");
                    SpriteRenderer partNow = a.I.LooksRig.TestPart();
                    if (partNow != null) Info($"depths in the body part's space (local z, negative = towards the camera): the keeper {partNow.transform.InverseTransformPoint(keeper.transform.position).z:0.000}, the front picture {partNow.transform.InverseTransformPoint(front.transform.position).z:0.000}, the body overlay {partNow.transform.InverseTransformPoint(ov.transform.position).z:0.000}");
                    // the nearest of his sprites decides the lift (Looks.Rig.FrontLift): each drawn one, in the same space
                    SpriteRenderer[] his = a.I.KeeperRenderers;
                    if (partNow != null && his != null)
                    {
                        var depths = new List<string>();
                        foreach (SpriteRenderer sr in his)
                        {
                            if (sr != null && sr && sr.enabled && sr.sprite != null && sr.gameObject.activeInHierarchy) depths.Add($"{sr.name} {partNow.transform.InverseTransformPoint(sr.transform.position).z:0.000}");
                        }
                        Info($"the keeper's drawn sprites in the body part's space: {string.Join(", ", depths)}; the nearest {a.I.KeeperNearestDepth(partNow.transform):0.000}, the front picture {partNow.transform.InverseTransformPoint(front.transform.position).z:0.000} (lift {Looks.Rig.FrontLift} past the nearest)");
                    }
                    DrawnAlone frontAlone = RenderAlone(front, at: at), bodyAlone = RenderAlone(ov, at: at), keeperAlone = RenderAlone(keeper, at: at);
                    DrawnAlone frontWith = RenderAlone(front, with: keeper, at: at), bodyWith = RenderAlone(ov, with: keeper, at: at);
                    (int Overlap, int KeeperInFront) body = bodyAlone?.Overlap(keeperAlone, bodyWith) ?? (0, 0);
                    (int Overlap, int KeeperInFront) wing = frontAlone?.Overlap(keeperAlone, frontWith) ?? (0, 0);
                    Info($"front layer alone {frontAlone?.Pixels} px, the body {bodyAlone?.Pixels}, the keeper {keeperAlone?.Pixels}; body/keeper overlap {body.Overlap} px, the keeper's colour there {body.KeeperInFront}; wing/keeper overlap {wing.Overlap} px, the keeper's colour there {wing.KeeperInFront}");
                    Check(frontAlone != null && frontAlone.Pixels > 50, $"the front picture draws ({frontAlone?.Pixels} pixels)");
                    if (body.Overlap < 30) Warn($"the rider and the body overlap in {body.Overlap} px only: nothing to judge the order on");
                    else Check(body.KeeperInFront > body.Overlap / 2, $"the rider is drawn over the body under him ({body.KeeperInFront} of {body.Overlap} overlapping pixels show him)");
                    if (wing.Overlap < 10) Info($"the wing and the rider overlap in {wing.Overlap} px: the folded wing's curtain hangs behind him by design (the references), so the order is judged on the body alone");
                    else Check(wing.KeeperInFront < wing.Overlap / 2, $"the front layer is drawn over the rider ({wing.Overlap - wing.KeeperInFront} of {wing.Overlap} overlapping pixels show the wing)");
                    // while he moves (the user saw the wing flicker with his shirt, 2026-10-20: a fifth of a pixel of
                    // lift tied in the depth buffer): the keeper is moved a little every frame, the wing read back with
                    // him, every other frame at the shipped lift and the others at the old fifth of a pixel; the pixels
                    // of their overlap that show him are lost to the tie
                    float shipped = Looks.Rig.FrontLift;
                    int moves = 0, lostShipped = 0, lostOld = 0, overlapShipped = 0, overlapOld = 0;
                    try
                    {
                        for (int i = 0; i < 40; i++)
                        {
                            Vector3 k = Keeper();
                            PutKeeper(k + new Vector3(-0.03f, 0f, 0f), -1f);
                            bool old = i % 2 == 1;
                            Looks.Rig.FrontLift = old ? 0.004f : shipped;
                            yield return null;
                            if (Keeper() != k) moves++;
                            if (keeper.sprite == null || !front.enabled) continue;
                            Vector3 here = ov.bounds.center;
                            DrawnAlone fa = RenderAlone(front, at: here), ka = RenderAlone(keeper, at: here), fw = RenderAlone(front, with: keeper, at: here);
                            (int Overlap, int KeeperInFront) o = fa?.Overlap(ka, fw) ?? (0, 0);
                            if (old) { overlapOld += o.Overlap; lostOld += o.KeeperInFront; }
                            else { overlapShipped += o.Overlap; lostShipped += o.KeeperInFront; }
                        }
                    }
                    finally
                    {
                        Looks.Rig.FrontLift = shipped;
                    }
                    Info($"moving {moves} of 40 frames: the rider showed through the wing in {lostShipped} of {overlapShipped} overlapping pixels at the shipped lift ({shipped}), in {lostOld} of {overlapOld} at a lift of 0.004");
                    Check(moves >= 30, $"the keeper moved for the probe ({moves} of 40 frames)");
                    // the readback classes a pixel by colour, and along the wing's outline a few match him by chance at
                    // either lift (2 to 3 of a hundred with no flicker in the world, 2026-10-20): the flicker that was
                    // seen covered a good part of his torso, so one in twenty is the line
                    Check(overlapShipped > 0 && lostShipped * 20 < overlapShipped, $"the wing does not let the rider through while he moves ({lostShipped} of {overlapShipped} overlapping pixels at the shipped lift, under one in twenty; {lostOld} of {overlapOld} at the old fifth of a pixel)");
                }
                else Fail("no front overlay, body overlay or keeper body to read back");
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
            }
            a.Remove();
            yield return Frames(2);
        }

        // The settings picture after a mount switch: the user saw the cart take many frames to settle at the new
        // mount's hitch gap (the video of 2026-10-20 16-24). Frame by frame after the switch, rat to worm and back (the
        // dragon until it stopped pulling the cart, 2026-10-25):
        // the worn skin, the gap applied to the stage's cart parts, the picture's reframes (every change logged); the
        // gap must follow the worn skin within two frames.
        internal static IEnumerator PreviewGap()
        {
            string noPack = MissingPack("rat", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            if (Looks.Find(Sk("worm")) == null || Looks.Find(Sk("rat")) == null)
            {
                Warn("the built-in worm or rat isn't registered: skipped");
                yield break;
            }
            const string owner = Owner + ".previewgap";
            MountCompanion me = Mounts.Companion(owner);
            me.Skin = Sk("rat");
            Mount.Instance paper = me.Paper;
            yield return Until(() => PreviewStage.Picture(paper, withCart: true) != null && paper.LooksRig.Wearing == Sk("rat") && !paper.LooksRig.Building, 20f);
            if (!waited)
            {
                Warn($"no settings picture of the rat within 20 s (stage failed: {PreviewStage.Failed}): skipped");
                yield break;
            }
            for (int i = 0; i < 6; i++) { PreviewStage.Picture(paper, withCart: true); yield return null; }
            float ratGap = PreviewStage.TestGapApplied;
            Info($"the rat's cart in the picture: hitch gap {ratGap:0.###} world units (HitchGapPx 9)");
            Check(ratGap > 0.1f, $"the rat's gap is on the picture's cart ({ratGap:0.###})");
            foreach (string to in new[] { "worm", "rat" })
            {
                float want = to == "worm" ? 0f : ratGap;
                me.Skin = Sk(to);
                var log = new List<string>();
                int wornAt = -1, settledAt = -1, lastReframe = -1, reframes0 = PreviewStage.TestReframes, rfPrev = 0;
                string lastLine = null;
                for (int f = 0; f < 150; f++)
                {
                    PreviewStage.Picture(paper, withCart: true);
                    yield return null;
                    string wearing = paper.LooksRig.Wearing;
                    bool building = paper.LooksRig.Building;
                    float gap = PreviewStage.TestGapApplied;
                    int rf = PreviewStage.TestReframes - reframes0;
                    string line = $"wearing {wearing}{(building ? " (building)" : "")}, gap {gap:0.###}, reframes {rf}";
                    if (line != lastLine)
                    {
                        log.Add($"f{f}: {line}");
                        lastLine = line;
                    }
                    if (wornAt < 0 && wearing == Sk(to) && !building) wornAt = f;
                    if (settledAt < 0 && Math.Abs(gap - want) < 0.0005f) settledAt = f;
                    if (rf != rfPrev) { lastReframe = f; rfPrev = rf; }
                }
                Info($"to the {to}: {string.Join("; ", log)}");
                Check(wornAt >= 0, $"the {to} is worn on the picture within 150 frames (at f{wornAt})");
                Check(wornAt >= 0 && settledAt >= 0 && settledAt - wornAt <= 2, $"the cart's gap follows the worn skin within two frames (worn at f{wornAt}, gap {want:0.###} applied at f{settledAt}, last reframe at f{lastReframe})");
            }
        }

        // The player's Coat on every built-in creature (the user, 2026-10-20, after the dragon's belly ignored it): each
        // worn on a test donkey, Coat colorized magenta, the mount group's changed colours read back: every colour of
        // the skin's Coat region changed (two may not: a grey keeps its hue) and nothing of another region. The exact
        // group's pairs say which colour changed, on the exact swap and on the cube path alike (the snail's 300 and
        // the worm's 720 are all paired, the first run showed, 2026-10-20).
        internal static IEnumerator CoatEveryBuiltIn()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null) { Fail("could not spawn"); yield break; }
            foreach (string skin in new[] { "rat", "snail", "worm", "dragon" })
            {
                if (Looks.Find(Sk(skin)) == null)
                {
                    Warn($"the built-in {skin} isn't registered: skipped");
                    continue;
                }
                a.Tuning.Set(Owner, "CoatColorizeAmount", 0f);
                a.Tuning.Set(Owner, "Skin", Sk(skin));
                yield return Until(() => a.IsReady && a.I.LooksRig.Wearing == Sk(skin) && !a.I.LooksRig.Building, 20f);
                if (!waited)
                {
                    Fail($"the {skin} was never worn");
                    continue;
                }
                a.Tuning.Set(Owner, "CoatColorize", "FF00FF");
                a.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
                yield return Frames(3);
                var d = a.I.ColorsRig.TestMountGroup();
                Looks.Loaded look = a.I.LooksRig.WornOwnColours;
                List<Color32> cs = look != null && look.ColoursByRegion.TryGetValue("Coat", out List<Color32> found) ? found : null;
                int coat = cs?.Count ?? -1;
                int others = 0;
                if (look != null) foreach (var kv in look.ColoursByRegion) if (kv.Key != "Coat") others += kv.Value.Count;
                int fromCoat = 0, fromOthers = 0;
                foreach (Color32 c in d.ChangedSources)
                {
                    if (cs != null && cs.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) fromCoat++;
                    else fromOthers++;
                }
                Info($"{skin}: {d.Colours} colours ({d.Method}), Coat {coat}, the other regions {others}; Coat = magenta changed {d.Changed} ({fromCoat} Coat colours, {fromOthers} of other regions)");
                Check(coat > 0, $"{skin}: its Coat region has colours ({coat})");
                Check(d.Pairs == d.Colours, $"{skin}: every colour is paired on the {d.Method} path ({d.Pairs} of {d.Colours})");
                Check(fromCoat >= coat - 2, $"{skin}: the Coat row reaches every body colour ({fromCoat} of {coat} changed)");
                Check(fromOthers == 0, $"{skin}: the Coat row changes no colour of another region ({fromOthers} did)");
            }
            a.Tuning.Set(Owner, "CoatColorizeAmount", 0f);
            a.Remove();
            yield return Frames(2);
        }

        // The page's Second tone row (the API's Muzzle region; the user, 2026-10-20: every part of every skin reachable
        // from the page): a companion's preset lands as the Muzzle keys on its paper mount and on its donkey wearing the
        // rat, where every Muzzle colour changes and no other; the row is left out on a mount whose skins have no such
        // region (the worm) and live on the rat, the snail, the dragon (its belly) and the donkey.
        internal static IEnumerator SecondTone()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Check(MountSettingsPage.MountHasRegion(M("rat"), "Muzzle") && MountSettingsPage.MountHasRegion(M("snail"), "Muzzle") && MountSettingsPage.MountHasRegion(M("dragon"), "Muzzle") && MountSettingsPage.MountHasRegion(MountCatalog.DonkeyId, "Muzzle"), "the rat, the snail, the dragon and the donkey have a second tone: the row is live for them");
            Check(!MountSettingsPage.MountHasRegion(M("worm"), "Muzzle"), "the worm has none: the row is left out for it");
            const string owner = Owner + ".secondtone";
            MountCompanion me = Mounts.Companion(owner);
            me.Skin = Sk("rat");
            me.Muzzle = MuzzleColor.Custom;
            me.CustomMuzzle = "00FF00";
            Mount.Instance paper = me.Paper;
            yield return Frames(2);
            Check(paper != null && paper.Overrides.ContainsKey("MuzzleColorize") && paper.OverrideOwners.TryGetValue("MuzzleColorize", out string by) && by == me.OwnerId, "the companion's second tone lands on its paper mount as MuzzleColorize, owned by the companion");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null) { Fail("could not spawn"); yield break; }
            a.Tuning.Set(Owner, "Skin", Sk("rat"));
            yield return Until(() => a.IsReady && a.I.LooksRig.Wearing == Sk("rat") && !a.I.LooksRig.Building, 20f);
            if (!waited) { Fail("the rat was never worn"); a.Remove(); yield break; }
            a.Tuning.Set(Owner, "MuzzleColorize", "00FF00");
            a.Tuning.Set(Owner, "MuzzleColorizeAmount", 1f);
            yield return Frames(3);
            var d = a.I.ColorsRig.TestMountGroup();
            Looks.Loaded look = a.I.LooksRig.WornOwnColours;
            List<Color32> pinks = look != null && look.ColoursByRegion.TryGetValue("Muzzle", out List<Color32> m) ? m : new List<Color32>();
            int fromPink = 0, fromOthers = 0;
            foreach (Color32 c in d.ChangedSources) { if (pinks.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) fromPink++; else fromOthers++; }
            Info($"the rat with a green second tone: {d.Changed} of {d.Colours} colours changed ({fromPink} of its {pinks.Count} Muzzle colours, {fromOthers} of other regions)");
            Check(pinks.Count > 0 && fromPink == pinks.Count && fromOthers == 0, $"the second tone reaches every pink colour of the rat and nothing else ({fromPink} of {pinks.Count}; {fromOthers} others)");
            me.Muzzle = MuzzleColor.Game;
            a.Remove();
            yield return Frames(2);
        }

        // The two over skins we ship, on the built-ins themselves (6.4: the over-skin scenarios run on temp packs):
        // Hellfire built whole with its erase marks and the ash coat under it, kept while walking; Royal Parade's
        // reds under the harness colour and its cloth drawn in the walk; the snail's own rider offset on its wearer.
        internal static IEnumerator OverBuiltIns()
        {
            string noPack = MissingPack("hellfire", "royal-parade", "snail"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null) { Fail("could not spawn"); yield break; }
            // Hellfire
            a.Tuning.Set(Owner, "Skin", Sk("hellfire"));
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("hellfire") && !a.I.LooksRig.Building, 30f);
            Check(waited, "Hellfire is worn and whole");
            Looks.Loaded hf = Looks.LoadedOwnColours(Sk("hellfire"));
            Check(hf != null && hf.WholeKeys.Count == 44 && hf.Erased > 2000, $"every Hellfire frame is built whole over the donkey with its erase marks ({hf?.WholeKeys.Count} whole, {hf?.Erased} pixels erased)");
            yield return Frames(3);
            {
                var grp = a.I.ColorsRig.TestMountGroup();
                MountColorRegion coat = null;
                foreach (MountColorRegion rg in Colors.RegionList) if (rg.Key == "Coat") coat = rg;
                int coatChanged = 0;
                if (coat != null) foreach (string hex in coat.SourceColors) { Colors.TryHex(hex, out Color32 c); if (grp.ChangedSources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) coatChanged++; }
                Check(coat != null && coatChanged == coat.SourceColors.Count, $"Hellfire's [Base.Coat] recolours every coat colour under it (the ash coat: {coatChanged} of {coat?.SourceColors.Count})");
            }
            SpriteRenderer part = a.I.LooksRig.TestPart(), ov = a.I.LooksRig.TestOverlay();
            int wholeWalk = 0, plainWalk = 0, walkPics = 0;
            yield return WalkFor(a, part, 40, () =>
            {
                if (ov == null || ov.sprite == null || !ov.sprite.name.Contains("walk")) return;
                if (a.I.LooksRig.TestShowing() || hf.Whole.Contains(ov.sprite)) wholeWalk++; else plainWalk++;
            }, n => walkPics = n);
            if (walkPics > 0) Check(wholeWalk > 0 && plainWalk == 0, $"walking: every Hellfire walk frame is the whole picture (the horns and the devil's tail on the body, the ears gone): {wholeWalk} whole, {plainWalk} plain");
            else Fail("no walk picture showed in 40 frames of walking");
            // Royal Parade
            a.Tuning.Set(Owner, "Skin", Sk("royal-parade"));
            yield return Until(() => a.I.TestSkinWearing() == Sk("royal-parade") && !a.I.LooksRig.Building, 30f);
            Check(waited, "Royal Parade is worn");
            Looks.SkinDef rp = Looks.Find(Sk("royal-parade"));
            Check(rp != null && rp.ColorRegions != null && rp.ColorRegions.TryGetValue("Harness", out HashSet<string> reds) && reds.Count == 4, "Royal Parade names its four reds under Harness");
            a.Tuning.Set(Owner, "HarnessColorize", "0000FF");
            a.Tuning.Set(Owner, "HarnessColorizeAmount", 1f);
            yield return Frames(3);
            {
                var d = a.I.ColorsRig.TestMountGroup();
                int redsChanged = 0;
                if (rp?.ColorRegions != null && rp.ColorRegions.TryGetValue("Harness", out HashSet<string> rr))
                    foreach (string hex in rr) { Colors.TryHex(hex, out Color32 c); if (d.ChangedSources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) redsChanged++; }
                Check(redsChanged == 4, $"the player's harness colour reaches the cloth's four reds ({redsChanged} of 4)");
            }
            a.Tuning.Clear(Owner, "HarnessColorize");
            a.Tuning.Clear(Owner, "HarnessColorizeAmount");
            ov = a.I.LooksRig.TestOverlay();
            int clothDrawn = 0, clothEmpty = 0; walkPics = 0;
            yield return WalkFor(a, part, 40, () =>
            {
                if (ov == null || ov.sprite == null || !ov.sprite.name.Contains("walk")) return;
                DrawnAlone cloth = RenderAlone(ov);
                if (cloth != null && cloth.Pixels > 30) clothDrawn++; else clothEmpty++;
            }, n => walkPics = n);
            if (walkPics > 0) Check(clothDrawn > 0 && clothEmpty == 0, $"walking: the cloth and crown are drawn in every walk frame ({clothDrawn} drawn, {clothEmpty} empty)");
            else Fail("no walk picture showed in 40 frames of walking");
            // the snail: its own rider offset, from its skin, on the wearer while ridden
            a.Tuning.Set(Owner, "Skin", Sk("snail"));
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == Sk("snail") && !a.I.LooksRig.Building, 30f);
            MountResult r = a.Ride(showMessage: false);
            if (r.Ok)
            {
                yield return Until(() => Mounts.IsMounted, 5f);
                yield return Frames(4);
                Check(a.Tuning.IsSet("RiderOffsetPx") && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner && a.Tuning.Get<int>("RiderOffsetPx") == -6, $"the snail's [Tuning.Ride] RiderOffsetPx = -6 is on its wearer while ridden ({(a.Tuning.IsSet("RiderOffsetPx") ? a.Tuning.Get<int>("RiderOffsetPx").ToString() : "not set")})");
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
            }
            else Fail("the snail can't be ridden: " + r.Reason);
            a.Remove();
            yield return Frames(2);
        }

        // The colour settings mean the same on every mount (docs/mount-colours.md): the worm (720 colours of its own,
        // none of the donkey's) on the test companion's paper donkey, its coat set to a green through the rows a
        // player uses, the settings picture rendered and read back: the pink is gone and the worm leans green; the
        // engine took the worm's own colours through the colour cube. Then the dragon on a spawned donkey: its
        // Muzzle setting changes the belly's colours only (its [Colors]).
        internal static IEnumerator SkinColours()
        {
            string noPack = MissingPack("dragon", "rat", "worm"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            if (Looks.Find(Sk("worm")) == null || Looks.Find(Sk("dragon")) == null)
            {
                Warn("the built-in worm or dragon isn't registered: skipped");
                yield break;
            }
            const string owner = Owner + ".colours";
            MountCompanion me = Mounts.Companion(owner);
            me.Coat = CoatColor.Game;
            me.Skin = Sk("worm");
            Mount.Instance paper = me.Paper;
            // the stage builds on the first ask (the donkey prefab loads over a few frames); ask every frame until a
            // picture of the worn worm comes
            yield return Until(() => PreviewStage.Picture(paper, withCart: false) != null && paper.LooksRig.Wearing == Sk("worm") && !paper.LooksRig.Building, 20f);
            if (!waited)
            {
                Warn($"no settings picture of the worm within 20 s (stage failed: {PreviewStage.Failed}): skipped");
                me.Skin = "";
                yield break;
            }
            for (int i = 0; i < 4; i++) { PreviewStage.Picture(paper, withCart: false); yield return null; }
            float before = PreviewStage.TestDominantHue(false, out int pinkPixels, out string topBefore);
            // at night the preview stage takes the scene's blue-tinted light (seen 2026-10-19: the worm's outline 36141C read
            // back as 1E0A1C, red and green halved, blue kept; pink 350° came out violet 291°), so the as-drawn hue is
            // only meaningful by day; the mapping and the custom-coat checks below hold either way
            if (GK2DonkeyAPI.Effects.Night()) Info($"at night the preview is blue-lit ({before:0}°): the as-drawn hue check needs daylight; rerun by day (the picture at night is an open item)");
            else Check(pinkPixels > 100 && (before >= 320f || before <= 20f), $"the worm as drawn leans pink ({before:0}°, {pinkPixels} coloured pixels; most drawn: {topBefore})");
            var before2 = paper.ColorsRig.TestMountGroup();
            bool regionsAtDefault = true;
            foreach (string r in Colors.MountRegionKeys) regionsAtDefault &= !Colors.RegionChanged(r);
            if (regionsAtDefault) Check(before2.Colours > Colors.PaletteMaxColours && before2.Changed == 0, $"the game's colours: the worm's {before2.Colours} colours mapped, none changed");
            else Info($"the mount regions aren't at their defaults in the cfg (the Colors tab): the 'none changed' check skipped ({before2.Changed} of {before2.Colours} changed)");
            me.CustomCoat = "20C040";
            me.Coat = CoatColor.Custom;
            for (int i = 0; i < 4; i++) { PreviewStage.Picture(paper, withCart: false); yield return null; }
            float after = PreviewStage.TestDominantHue(false, out int greenPixels, out string topAfter);
            Check(greenPixels > 100 && after >= 80f && after <= 170f, $"a custom green coat: the picture leans green ({after:0}°, {greenPixels} coloured pixels; most drawn: {topAfter})");
            var g = paper.ColorsRig.TestMountGroup();
            Check(g.Colours > Colors.PaletteMaxColours && g.Method == SpriteColorMethod.Lut, $"{g.Colours} colours of its own: the colour cube ({g.Method})");
            Check(g.Pairs == g.Colours && g.Changed >= g.Colours - 8, $"every one of its colours is mapped ({g.Pairs} pairs), the coat's all changed ({g.Changed}; its 3 Dark colours may not)");
            me.Coat = CoatColor.Game;
            me.Skin = "";
            yield return Frames(2);

            // the rat: Muzzle is its pink tail, ears and nose (skin.cfg [Colors]), and only that (the dragon's belly was
            // this test's region until it joined its Coat: a gold belly under a green coat read as wood, 2026-10-20)
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            a.Tuning.Set(Owner, "Skin", Sk("rat"));
            yield return Until(() => a.IsReady && a.I.LooksRig.Wearing == Sk("rat") && !a.I.LooksRig.Building, 20f);
            if (!waited)
            {
                Fail("the rat was never worn");
                a.Remove();
                yield break;
            }
            a.Tuning.Set(Owner, "MuzzleColorize", "00FF00");
            a.Tuning.Set(Owner, "MuzzleColorizeAmount", 1f);
            yield return Frames(3);
            var d = a.I.ColorsRig.TestMountGroup();
            Looks.Loaded look = a.I.LooksRig.WornOwnColours;
            int pink = look != null && look.ColoursByRegion.TryGetValue("Muzzle", out List<Color32> m) ? m.Count : -1;
            bool onlyPink = d.Changed == pink && pink > 0;
            foreach (Color32 c in d.ChangedSources) onlyPink &= look.ColoursByRegion["Muzzle"].Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);
            Check(d.Method == SpriteColorMethod.Palette && d.Colours <= Colors.PaletteMaxColours, $"the rat's {d.Colours} colours: the exact swap ({d.Method})");
            Check(onlyPink, $"Muzzle changes the rat's pink colours only ({d.Changed} changed of {d.Pairs}; the pink parts have {pink})");
            a.Remove();
            yield return Frames(2);
        }

        // The Donkey Skins pack's skins: the donkey has no default look of a pack, so nothing is resolved or rewritten for them
        // (from the API's "layering: a variant before its default" scenario, step d, 2026-10-27).
        internal static IEnumerator DonkeySkinsUnlayered()
        {
            string noPack = MissingPack("hellfire", "royal-parade");
            if (noPack != null) { Skip(noPack); yield break; }
            // (d) the donkey's skins: no default, nothing rewritten
            foreach (string id in new[] { Sk("hellfire"), Sk("royal-parade") })
            {
                Looks.SkinDef s = Looks.Find(id);
                if (s == null) { Warn($"{id} is not registered (Saddle Up not loaded?)"); continue; }
                Check(s.Parent == null && s.AloneValues == null && !s.OverDefault && !Looks.IsVariantSlot(s) && Mounts.Looks.Find(id)?.BuiltOn == null && Looks.TestLoaded(id)?.Layered != true, $"{id}: no default, nothing resolved, as before (over {s.Over})");
            }
            yield break;
        }
    }
}
