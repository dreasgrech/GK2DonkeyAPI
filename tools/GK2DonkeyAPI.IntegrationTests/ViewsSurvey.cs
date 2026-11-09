using GK2DonkeyAPI.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The views survey (measures only; the views investigation of 2026-11-09, docs/performance.md): every way a mount's
    // view comes and goes, driven on purpose, each phase's cost by path and step from the views probe (Dev/ViewCost.cs).
    // A mixed crowd (plain donkeys, every shipped look, babies, carts) made in view; the keeper taken far away (asleep:
    // the views released or parked) and back (woken: from the pool or made again, then prepared); taken to an idle
    // distance (frozen or switched off) and back (switched on); far and back again with the pool warm. Nothing is
    // checked but that it ran; the [test] INFO lines are the data.
    internal static class ViewsSurvey
    {
        internal static IEnumerator Run()
        {
            string noPack = Tests.MissingPack("dragon", "rat", "snail", "worm", "gerry", "hellfire", "royal-parade");
            if (noPack != null) { Skip(noPack); yield break; }
            if (!DevTools.Unlocked) { Skip("the views probe is dev only"); yield break; }
            var looks = new List<string> { Tests.Sk("hellfire"), Tests.Sk("dragon"), Tests.Sk("gerry"), Tests.Sk("royal-parade"), Tests.Sk("rat"), Tests.Sk("snail"), Tests.Sk("worm") };
            Vector3? far = null;
            foreach (float d in new[] { 60f, 52f, 46f, 42f })
            {
                far = Spot(home, d);
                if (far != null) break;
            }
            Vector3? idle = Spot(home, 24f) ?? Spot(home, 20f);
            if (far == null || idle == null) { Skip("no spot far enough from the test spot for the keeper (sleep) or at an idle distance"); yield break; }

            var made = new List<MountHandle>();
            bool was = ViewCost.Forced;
            try
            {
                ViewCost.Forced = true;
                yield return Settle(15f);
                ViewCost.Reset();

                // 1. made in view: 8 plain donkeys, 7 in each look, 3 with carts, a baby for every second adult
                int pool0 = ViewPool.Count, reused0 = Lod.ReusedTotal, spawned0 = Lod.SpawnedTotal;
                for (int k = 0; k < 18; k++)
                {
                    Vector3? at = FreeNear(home, 2.5f, 8f);
                    if (at == null) continue;
                    bool cart = k >= 15;
                    MountHandle h = Spawn(at.Value, k % 2 == 0, withCart: cart);
                    if (h == null) continue;
                    made.Add(h);
                    if (k >= 8 && k < 15) h.Tuning.Set(Owner, "Skin", looks[k - 8]);
                    if (k % 2 == 1 && !cart)
                    {
                        MountHandle baby = Mounts.SpawnFoal(Owner, h, persist: false); // its size its parent mount's, as a player's
                        if (baby != null) made.Add(baby);
                    }
                }
                yield return Until(() => AllReady(made), 20f);
                yield return Seconds(3f);
                Phase($"1. {made.Count} made in view (8 plain, 7 looks, 3 carts, babies)", pool0, reused0, spawned0);

                for (int round = 1; round <= 2; round++)
                {
                    // 2. the keeper far away: everything asleep, views released (parked when they can be)
                    pool0 = ViewPool.Count; reused0 = Lod.ReusedTotal; spawned0 = Lod.SpawnedTotal;
                    PutKeeper(far.Value, homeFacing);
                    yield return Seconds(2f);
                    yield return Settle(10f);
                    Phase($"{round * 2}. the keeper {Vector3.Distance(far.Value, home):F0} away: asleep (round {round})", pool0, reused0, spawned0);

                    // 3. back: woken, the views from the pool or made again, prepared
                    pool0 = ViewPool.Count; reused0 = Lod.ReusedTotal; spawned0 = Lod.SpawnedTotal;
                    PutKeeper(home, homeFacing);
                    yield return Seconds(2f);
                    yield return Until(() => AllReady(made), 15f);
                    yield return Settle(10f);
                    Phase($"{round * 2 + 1}. back: woken (round {round}, the pool {(round == 1 ? "as the sleep left it" : "warm")})", pool0, reused0, spawned0);
                }

                // 6. an idle distance: off screen within the sleep radius (frozen, or switched off without a cart)
                pool0 = ViewPool.Count; reused0 = Lod.ReusedTotal; spawned0 = Lod.SpawnedTotal;
                PutKeeper(idle.Value, homeFacing);
                yield return Seconds(2f);
                yield return Settle(10f);
                Phase($"6. the keeper {Vector3.Distance(idle.Value, home):F0} away: idle", pool0, reused0, spawned0);

                // 7. back: active again (switched on, unfrozen)
                pool0 = ViewPool.Count; reused0 = Lod.ReusedTotal; spawned0 = Lod.SpawnedTotal;
                PutKeeper(home, homeFacing);
                yield return Seconds(2f);
                yield return Settle(10f);
                Phase("7. back: active again", pool0, reused0, spawned0);

                // 8. idle switched off against idle frozen (Lod.IdleSwitchesOff), uncapped, with 60 more plain donkeys:
                // switching on costs every wake 0.4-0.6 ms (phases 6 and 7); does switching off save frame time? The
                // levels self-test measures it at the frame cap, which hides it. Each mode twice, the order reversed.
                for (int k = 0; k < 60; k++)
                {
                    Vector3? at = FreeNear(home, 2f, 12f);
                    if (at == null) continue;
                    MountHandle h = Spawn(at.Value, k % 2 == 0);
                    if (h != null) made.Add(h);
                }
                yield return Until(() => AllReady(made), 20f);
                yield return Settle(10f);
                ViewCost.Reset();
                bool offWas = Lod.IdleSwitchesOff;
                int vsyncWas = QualitySettings.vSyncCount, rateWas = Application.targetFrameRate;
                try
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    foreach (bool off in new[] { true, false, false, true })
                    {
                        PutKeeper(home, homeFacing);
                        Lod.IdleSwitchesOff = off;
                        yield return Seconds(1.5f);
                        yield return Settle(10f);
                        ViewCost.Reset();
                        PutKeeper(idle.Value, homeFacing);
                        yield return Seconds(1.5f);
                        yield return Settle(10f);
                        int idleN = 0, offN = 0;
                        foreach (Mount.Instance i in Mount.All) if (i.tier == Mount.Tier.Idle) { idleN++; if (i.HasView && !i.Ready) offN++; }
                        double sum = 0;
                        int n = 0;
                        float end = Time.unscaledTime + 4f;
                        Perf.PhaseStart();
                        while (Time.unscaledTime < end && !abort) { yield return null; sum += Time.unscaledDeltaTime * 1000.0; n++; }
                        double mod = Perf.PhaseModMsPerFrame;
                        Perf.PhaseEnd("idle comparison");
                        Info($"[views] 8. idle {(off ? "switched off" : "frozen")}: {idleN} idle ({offN} switched off), {n} frames, {(n > 0 ? sum / n : 0):F2} ms a frame uncapped, the mod {mod:F3} ms");
                        foreach (string l in ViewCost.Report()) Info("[views]   " + l);
                        ViewCost.Reset();
                    }
                }
                finally
                {
                    Lod.IdleSwitchesOff = offWas;
                    QualitySettings.vSyncCount = vsyncWas;
                    Application.targetFrameRate = rateWas;
                }
            }
            finally
            {
                foreach (MountHandle h in made) { try { if (h != null && h.Exists) h.Remove(); } catch { } }
                ViewCost.Reset();
                ViewCost.Forced = was;
                PutKeeper(home, homeFacing);
            }
            Check(true, "the views survey ran (its lines above: each phase's views by path and step)");
        }

        private static void Phase(string label, int pool0, int reused0, int spawned0)
        {
            int views = 0, active = 0, idle = 0, asleep = 0;
            foreach (Mount.Instance i in Mount.All)
            {
                if (i.HasView) views++;
                if (i.tier == Mount.Tier.Active) active++; else if (i.tier == Mount.Tier.Idle) idle++; else asleep++;
            }
            Info($"[views] {label}: {Mount.All.Count} instances, {views} views; levels {active} active / {idle} idle / {asleep} asleep; views made {Lod.SpawnedTotal - spawned0} ({Lod.ReusedTotal - reused0} from the pool); parked {pool0} -> {ViewPool.Count}");
            List<string> lines = ViewCost.Report();
            if (lines.Count == 0) Info("[views]   no view came or went");
            foreach (string l in lines) Info("[views]   " + l);
            ViewCost.Reset();
        }

        private static bool AllReady(List<MountHandle> made)
        {
            foreach (MountHandle h in made) if (h.Exists && !h.IsReady) return false;
            return true;
        }

        // A free spot with ground between minR and maxR of 'from' (the flattened ring the crowd uses), or null.
        private static Vector3? FreeNear(Vector3 from, float minR, float maxR)
        {
            for (int tries = 0; tries < 16; tries++)
            {
                Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(minR, maxR);
                Vector3 p = from + new Vector3(d.x, 0f, d.y * 0.6f);
                float g = Mount.Instance.GroundHeight(p, out _);
                if (float.IsNaN(g)) continue;
                p.y = g;
                if (FreeSpot(p)) return p;
            }
            return null;
        }
    }
}
