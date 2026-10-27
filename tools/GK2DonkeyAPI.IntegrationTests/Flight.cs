using GK2DonkeyAPI.Api;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.Flight.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // Saddle Up's Jump and Fly rows: in Keys, Space both, shown only while flying is on, and the Behaviour values
        // follow the rows.
        internal static IEnumerator FlightKeyRows()
        {
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null) { Skip("Saddle Up's page isn't shown"); yield break; }
            MountSettingsPage.Row Row(string key) => saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == key && x.ShownSection == "Keys");
            MountSettingsPage.Row jump = Row("JumpKey"), fly = Row("FlyKey");
            Check(jump != null && fly != null, "Saddle Up has Jump and Fly rows in its Keys section");
            if (jump == null || fly == null) yield break;
            // flying for players (0.38.0): the Climb and Descend rows, their keys the flight's (Left Shift and Left Ctrl as shipped)
            MountSettingsPage.Row climb = Row("ClimbKey"), descend = Row("DescendKey");
            Check(climb != null && descend != null && climb.Enabled != null && descend.Enabled != null, "and Climb and Descend rows (0.38.0), shown with the others");
            Check(Plugin.ClimbKey != null && Plugin.DescendKey != null && Flight.UpKey == Plugin.ClimbKey.Value.MainKey && Flight.DownKey == Plugin.DescendKey.Value.MainKey,
                $"the flight's climb and descend keys are the Behaviour keys' main keys ({Flight.UpKey}, {Flight.DownKey})");
            bool on = Mounts.FlyingEnabled;
            Check((jump.Enabled == null || jump.Enabled() == on) && (fly.Enabled == null || fly.Enabled() == on) && jump.Enabled != null && fly.Enabled != null,
                $"both shown only while flying is on (flying {(on ? "on" : "off")}: rows {(jump.Enabled?.Invoke() ?? true ? "shown" : "hidden")})");
            var je = (BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut>)jump.Entry;
            Check(Flight.SameKey(Plugin.JumpKey.Value, je.Value) && Flight.SameKey(Plugin.FlyKey.Value, ((BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut>)fly.Entry).Value),
                $"the Behaviour keys are the rows' ({Plugin.JumpKey.Value}, {Plugin.FlyKey.Value})");
            var was = je.Value;
            try
            {
                var ctrlK = new BepInEx.Configuration.KeyboardShortcut(KeyCode.K, KeyCode.LeftControl);
                je.Value = ctrlK;
                yield return null;
                Check(Flight.SameKey(Plugin.JumpKey.Value, ctrlK), $"the Jump row set to Ctrl+K: the Behaviour JumpKey is Ctrl+K, the modifier kept ({Plugin.JumpKey.Value})");
                Check(!Flight.SameKey(Flight.JumpKeyNow, Flight.FlyKeyNow), "and the two keys are no longer one");
            }
            finally { je.Value = was; }
            yield return null;
            Check(Flight.SameKey(Plugin.JumpKey.Value, was), $"and back ({Plugin.JumpKey.Value})");
        }

        // Our mounts' flies and jumps flags as their packs say them (from the API's "mounts: the flies and jumps flags"
        // scenario, 2026-10-27, which keeps the rule on a pack of its own).
        internal static IEnumerator ShippedFlightFlags()
        {
            string noPack = MissingPack("dragon", "rat", "snail", "worm", "gerry");
            if (noPack != null) { Skip(noPack); yield break; }
            MountInfo dragon = Mounts.Catalog.Find(M("dragon")), rat = Mounts.Catalog.Find(M("rat"));
            Check(dragon != null && dragon.Flies && dragon.Jumps, $"the dragon flies and jumps (its [Mount] says flies = true): {dragon?.Flies} {dragon?.Jumps}");
            var grounded = new System.Collections.Generic.List<string>();
            foreach (string id in new[] { "rat", "snail", "worm", "gerry" })
            {
                MountInfo k = Mounts.Catalog.Find(M(id));
                if (k == null) grounded.Add(id + " not loaded");
                else if (k.Flies || !k.Jumps) grounded.Add($"{id} flies {k.Flies}, jumps {k.Jumps}");
            }
            Check(grounded.Count == 0, "the other built-in mounts jump and don't fly" + (grounded.Count > 0 ? ": " + string.Join("; ", grounded) : ""));
            Check(!Catalog.Donkey.Flies && Catalog.Donkey.Jumps, "the donkey jumps and doesn't fly");
            Check(rat != null && !rat.Flies, "the rat's flag is its own, not the dragon's (no mount id decides it)");
            yield break;
        }

        // The dragon's own jump (from the API's "jump: each mount its own" scenario, 2026-10-27, which keeps the rule on a
        // pack of its own): its mount.cfg [Jump] with wings and a braked fall, and live: a small take-off and landing on its
        // flying frames, never its walk.
        internal static IEnumerator DragonJump()
        {
            string noPack = MissingPack("dragon");
            if (noPack != null) { Skip(noPack); yield break; }
            MountInfo dragonInfo = Mounts.Catalog.Find(M("dragon"));
            Check(dragonInfo != null && dragonInfo.OwnJump && dragonInfo.Jump.Wings == true && dragonInfo.Jump.FallEase > 0.5f, $"the dragon has its own jump with wings ({dragonInfo?.Jump.Height} px, {dragonInfo?.Jump.Time} s, fall ease {dragonInfo?.Jump.FallEase}, wings {dragonInfo?.Jump.Wings})");
            if (!Flight.Active) { Info("the live part wants the flying experiment on: skipped"); yield break; }
            if (Mount.Instance.IsIndoors()) { Info("indoors: the live part wants open ground"); yield break; }
            MountHandle h = null;
            try
            {
                h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
                if (h == null) { Fail("could not spawn"); yield break; }
                yield return Until(() => h.IsReady, 6f);
                MountResult r = h.Ride(showMessage: false);
                if (!r.Ok) { Fail($"not ridden ({Said(r)})"); yield break; }
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, 5f);
                Mount.Instance i;
                // the dragon: its jump a small take-off and landing on its flying frames
                yield return Frames(10);
                h.Tuning.Set(Owner, "Skin", M("dragon") + "/default");
                yield return Until(() => h.Mount == M("dragon"), 8f);
                // its pictures built before the jump (they take about 20 frames: a jump begun earlier drew the walk, 2026-10-26)
                yield return Until(() => h.I.TestSkinWearing() == M("dragon") + "/default" && !h.I.LooksRig.Building, 15f);
                yield return Frames(10);
                i = Mount.Ridden;
                var seen = new System.Collections.Generic.List<string>();
                Flight.TestJumpHeld = true;
                Flight.TestMoveDir = Vector2.zero;
                try
                {
                    float start = Time.time;
                    Flight.Jump(i);
                    while (Flight.Now == Flight.Phase.Jumping && Time.time - start < Flight.J.Time + Flight.JumpPerchS + 3f)
                    {
                        string f = Flight.FrameFor(i) ?? "walk";
                        if (seen.Count == 0 || seen[seen.Count - 1] != f) seen.Add(f);
                        yield return null;
                    }
                    string fold = Flight.FrameFor(i);
                    string said = string.Join(" ", seen).Replace("bdy_", "").Replace("_left_", "");
                    int crouchAt = seen.IndexOf("bdy_takeoff_left_01"), push = seen.FindIndex(x => x == "bdy_takeoff_left_02" || x == "bdy_takeoff_left_03" || x == "bdy_takeoff_left_04");
                    int open = seen.FindIndex(x => x.StartsWith("bdy_glide_left_") || x.StartsWith("bdy_fly_left_")), reach = seen.FindIndex(x => x == "bdy_land_left_01" || x == "bdy_land_left_02"), touch = seen.LastIndexOf("bdy_land_left_03");
                    Check(crouchAt == 0 && push > crouchAt && open > push && reach > open && !seen.Contains("walk"),
                        $"the dragon's jump: the crouch, the push-off, wings in the air, reaching for the ground, never its walk ({said})");
                    Check(touch > reach || fold == "bdy_land_left_04", $"then the touch and the fold ({said}; after: {fold ?? "walk"})");
                    // the wing never flashes (the user's video 2026-10-25: take-off 4, wings down, then fly 1, wings up): the
                    // beat goes on from the push-off's pose, and the top goes into the glide by its way in
                    int off4 = seen.IndexOf("bdy_takeoff_left_04"), glideAt = seen.FindIndex(x => x.StartsWith("bdy_glide_left_"));
                    Check(off4 >= 0 && off4 + 1 < seen.Count && seen[off4 + 1] == "bdy_fly_left_05", $"after the push-off the beat goes on from its pose (take-off 4, then fly 5): {said}");
                    Check(glideAt < 0 || (glideAt > 0 && seen[glideAt - 1].StartsWith("bdy_glidein_left_")), $"the wings open into the glide by its way in, never straight from the beat: {said}");
                }
                finally { Flight.TestJumpHeld = null; Flight.TestMoveDir = null; }
                if (Flight.Now != Flight.Phase.Ground) Flight.Release(i, "self-test");
            }
            finally
            {
                Flight.TestJumpHeld = null;
                Flight.TestMoveDir = null;
                if (Flight.Now != Flight.Phase.Ground && Mount.Ridden != null) Flight.Release(Mount.Ridden, "self-test");
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                try { if (h != null && h.Exists) h.Remove(); } catch { }
            }
        }
    }
}
