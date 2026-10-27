using System;
using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Each mount its own jump (JumpShapes.cs, docs/flying.md "Each mount its own jump"): a pack's mount.cfg [Jump] read
    // (the keys it sets, a bad line and an unknown key in the report, the rest the default jump's), its arc by itself,
    // saved back and read again; [Jump] in a skin.cfg refused; a code mount's MountDef.Jump; then ridden and jumped live:
    // the jump takes its mount's shape at the press and tops out at its height.
    internal static partial class SelfTest
    {
        internal static IEnumerator FlightJumpPerMount()
        {
            const string owner = Owner + ".jumpshape";
            const string id = "selftest.jump-beast";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-jumpshape", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Jump Test\nmount = " + id + "\n\n[Jump]\nheight = 5\n\n" +
                    "[Mount]\nid = " + id + "\nname = Jump Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n\n" +
                    "# keep: the jump's comment\n[Mount.Jump]\nheight = 50  # keep: a high one\ntime = 0.9\nrise_share = 0.4\nsquash = 0\nhang = abc\nbounce = 3\ncrouch = 99\n");
            }
            catch (Exception e) { Warn("the temp pack could not be written: " + e.Message); yield break; }
            MountHandle h = null;
            IDisposable codeReg = null;
            try
            {
                Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the pack registered");
                Catalog.Entry kd = Catalog.Find(id);
                MountInfo k = Mounts.Catalog.Find(id);
                if (kd == null || k == null) { Fail("the pack's mount is missing"); yield break; }
                Flight.JumpShape def = Flight.DefaultJump;
                Check(k.OwnJump && Mathf.Abs(k.Jump.Height.Value - 50f) < 0.001f && Mathf.Abs(k.Jump.Time.Value - 0.9f) < 0.001f && Mathf.Abs(k.Jump.RiseShare.Value - 0.4f) < 0.001f && k.Jump.Squash.Value == 0f,
                    $"its [Jump] is the mount's: height {k.Jump.Height}, time {k.Jump.Time}, rise share {k.Jump.RiseShare}, squash {k.Jump.Squash}");
                Check(Mathf.Abs(k.Jump.TapHeight.Value - def.Tap) < 0.001f && Mathf.Abs(k.Jump.HoldMin.Value - def.HoldMin) < 0.001f && Mathf.Abs(k.Jump.Hang.Value - def.Hang) < 0.001f,
                    $"the keys it doesn't set (or sets wrong) are the default jump's: tap {k.Jump.TapHeight} ({def.Tap}), hold min {k.Jump.HoldMin} ({def.HoldMin}), hang {k.Jump.Hang} ({def.Hang})");
                Check(kd.Jump != null && kd.Jump.TryGetValue("crouch", out float crouch) && Mathf.Abs(crouch - 12f) < 0.001f, $"crouch = 99 is taken as the top of its range, 12 ({(kd.Jump != null && kd.Jump.ContainsKey("crouch") ? kd.Jump["crouch"].ToString() : "not set")})");
                string report = kd.Pack?.Report ?? "";
                Check(report.Contains("hang = abc is not a number") && report.Contains("bounce is not a jump key") && report.Contains("crouch = 99 is outside"), "the report says the bad line, the unknown key and the clamp: " + report);
                Check(!Catalog.Donkey.Jump?.ContainsKey("height") ?? true, "the donkey has no jump of its own (it is the default jump)");
                MountInfo donkey = Mounts.Catalog.Find(Catalog.DonkeyId);
                Check(donkey != null && !donkey.OwnJump && Mathf.Abs(donkey.Jump.Height.Value - def.Height) < 0.001f, $"the donkey's MountInfo: the default jump ({donkey?.Jump.Height} px)");

                Looks.SkinDef skin = Looks.Find(id + "/default");
                Check(skin != null && skin.Report.Contains("[jump]") && skin.Report.Contains("mount.cfg"), "[Jump] in a skin.cfg is refused (a mount's own value): " + (skin?.Report ?? "no skin"));

                // the arc by itself: its height held, its time in the air
                Flight.JumpShape s = JumpShapes.Of(kd);
                Check(s.From.Contains("Jump Beast"), "the shape says whose it is: " + s.From);
                float max = 0f, offT = -1f, landT = -1f;
                for (int n = 1; n < 40000 && landT < 0f; n++)
                {
                    float t = n * 1e-4f, hgt = Flight.JumpHeightAt(s, t, float.PositiveInfinity, out bool done);
                    max = Mathf.Max(max, hgt);
                    if (offT < 0f && hgt > 0f) offT = t;
                    if (offT >= 0f && hgt <= 0f) landT = t;
                    if (done && landT < 0f) landT = t;
                }
                // time counts from the crouch's end (the rise starts from its dip, under the ground: F11 2026-10-25 measured
                // 0.863 s above the ground with this pack's 12 px crouch)
                Check(Mathf.Abs(max - 50f) < 0.05f && Mathf.Abs(landT - s.CrouchTime - 0.9f) < 0.02f, $"its arc: the top at {max:0.00} px (50), {landT - s.CrouchTime:0.000} s from the crouch to the ground (its time 0.9), {landT - offT:0.000} s above the ground");

                // the eased fall (wings braking): the same time down, touching down slowly where the stone's fall is fastest
                Flight.JumpShape stone = s, eased = s;
                stone.FallEase = 0f; eased.FallEase = 1f;
                float TouchSpeed(Flight.JumpShape js)
                {
                    float t0 = -1f;
                    for (int n = 1; n < 40000; n++)
                    {
                        float tt = n * 1e-4f;
                        if (Flight.JumpHeightAt(js, tt, float.PositiveInfinity, out _) <= 0f && tt > js.CrouchTime + 0.05f) { t0 = tt; break; }
                    }
                    return t0 < 0f ? -1f : (Flight.JumpHeightAt(js, t0 - 0.02f, float.PositiveInfinity, out _) - Flight.JumpHeightAt(js, t0, float.PositiveInfinity, out _)) / 0.02f;
                }
                float vStone = TouchSpeed(stone), vEased = TouchSpeed(eased);
                Check(vStone > 0f && vEased >= 0f && vEased < vStone * 0.25f, $"fall_ease 1 touches down softly: {vEased:0} px/s against the stone's {vStone:0}");

                Check(donkey != null && donkey.Jump.Wings == false, "the donkey's jump has no wings");

                // saved and read again, the comments kept
                string file = TempMountCfg(tempRoot, id);
                kd.Jump["crouch"] = 4f; // the Mounts tab's row does this
                string err = Looks.SaveMountCfg(kd);
                string text = System.IO.File.ReadAllText(file).Replace("\r\n", "\n");
                Check(err == null && text.Contains("# keep: the jump's comment\n[Jump]\n") && text.Contains("height = 50  # keep: a high one\n") && text.Contains("\ncrouch = 4\n") && text.Contains("\nrise_share = 0.4\n") && !text.Contains("bounce") && !text.Contains("hang"),
                    "saved: [Jump] with its comments, the crouch changed, the bad and unknown lines gone" + (err != null ? " (" + err + ")" : ""));
                kd.Jump = null;
                err = Looks.ReloadMountCfg(kd);
                Check(err == null && kd.Jump != null && kd.Jump.Count == 5 && Mathf.Abs(kd.Jump["crouch"] - 4f) < 0.001f, $"read again: the file's jump is the mount's ({(kd.Jump != null ? kd.Jump.Count + " keys" : "none")})");

                // a code mount's MountDef.Jump
                codeReg = Mounts.Catalog.Register(owner, new MountDef { Id = "selftest.jump-code", Name = "Jump Code Beast", Jump = new MountJump { Height = 40f, Hang = 0.1f } });
                MountInfo c = Mounts.Catalog.Find("selftest.jump-code");
                Check(c != null && c.OwnJump && Mathf.Abs(c.Jump.Height.Value - 40f) < 0.001f && Mathf.Abs(c.Jump.Hang.Value - 0.1f) < 0.001f && Mathf.Abs(c.Jump.Time.Value - def.Time) < 0.001f,
                    $"a code mount's MountDef.Jump: height {c?.Jump.Height}, hang {c?.Jump.Hang}, the time the default's {c?.Jump.Time}");

                // live: ridden plain, a held jump tops out at its own height
                if (!Flight.Active) { Info("the live part wants the flying experiment on: skipped"); yield break; }
                if (Mount.Instance.IsIndoors()) { Info("indoors: the live part wants open ground"); yield break; }
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                h.Tuning.Set(Owner, "Skin", id + "/default");
                yield return Until(() => h.Mount == id, 8f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                yield return Frames(5);
                Mount.Instance i = Mount.Ridden;
                Flight.TestJumpHeld = true;
                Flight.TestMoveDir = Vector2.zero;
                try
                {
                    float start = Time.time, top = -99f;
                    Flight.Jump(i);
                    Check(Flight.Now == Flight.Phase.Jumping && Flight.J.From.Contains("Jump Beast"), $"the jump takes its mount's shape at the press ({Flight.Now}; {Flight.J.From})");
                    while (Flight.Now == Flight.Phase.Jumping && Time.time - start < Flight.J.Time + Flight.JumpPerchS + 3f)
                    {
                        top = Mathf.Max(top, Flight.HeightPx);
                        yield return null;
                    }
                    Check(Flight.Now == Flight.Phase.Ground && top > 48f && top < 51f, $"jumped live: the top at {top:0.0} px (its 50; the default's {def.Height:0}), down after {Time.time - start:0.00} s ({Flight.Now})");
                }
                finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                if (Flight.Now != Flight.Phase.Ground) Flight.Release(i, "self-test");
            }
            finally
            {
                Flight.TestJumpHeld = null;
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
                codeReg?.Dispose();
                Looks.Unregister(id + "/default", owner);
                try { System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
            yield return Frames(3);
            Check(Mounts.Catalog.Find(id) == null && Mounts.Catalog.Find("selftest.jump-code") == null, "the mounts go with their pack and handle");
        }
    }
}
