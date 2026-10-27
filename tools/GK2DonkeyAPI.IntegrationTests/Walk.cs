using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Moved from the API's Dev/SelfTest.Walk.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // the walk survey's own group in the Tests tab
        internal const string GroupWalk = "Walk";

        internal static IEnumerator WalkSurvey()
        {
            string noPack = MissingPack("dragon"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            if (!DevTools.Unlocked) { Skip("the stride probe is dev only (dev tools locked)"); yield break; }
            if (!Plugin.For(MountStyle.Ride).SoundsOn.Value) Info("the mount sounds are off: the rig (and the probe in it) only ticks with them on; no rows will come");
            try
            {
                surveyHarmony = new Harmony(Plugin.Guid + ".walksurvey");
                surveyHarmony.Patch(AccessTools.Method(typeof(LazyInput), nameof(LazyInput.GetDirection)),
                    postfix: new HarmonyMethod(typeof(SelfTest), nameof(SurveyStickPostfix)));
            }
            catch (Exception e) { Fail("the stick could not be patched: " + e.Message); yield break; }
            StrideProbe.On = true; // for this survey only (off again below)

            // the mounts: the plain donkey, then each built-in mount by its own look (saddleup.dragon/default)
            var mounts = new List<(string Skin, string Mount)> { (null, MountCatalog.DonkeyId) };
            foreach (string id in new[] { "dragon", "rat", "snail", "worm", "gerry" }) mounts.Add((Sk(id), M(id)));
            try
            {
                foreach (var m in mounts)
                {
                    foreach (bool cart in new[] { false, true })
                    {
                        if (m.Mount != MountCatalog.DonkeyId && Catalog.Find(m.Mount) == null) { Info($"{m.Mount}: not loaded, skipped"); break; }
                        if (cart && m.Mount != MountCatalog.DonkeyId && !Catalog.Find(m.Mount).Cart) { Info($"{m.Mount}: pulls no cart, the cart pass skipped"); continue; }
                        MountHandle h = null;
                        StrideProbe.Label = $"{m.Mount}{(cart ? "+cart" : "")}";
                        try
                        {
                            h = Spawn(Spot(home, 3f) ?? home, right: true, withCart: cart);
                            if (h == null) { Fail($"{StrideProbe.Label}: could not spawn"); continue; }
                            yield return Until(() => h.IsReady, 6f);
                            if (m.Skin != null)
                            {
                                h.Tuning.Set(Owner, "Skin", m.Skin);
                                yield return Until(() => h.Mount == m.Mount, 8f);
                                if (!waited) { Info($"{StrideProbe.Label}: the skin did not take ({h.Mount}), skipped"); continue; }
                            }
                            MountResult r = h.Ride(showMessage: false);
                            if (!r.Ok) { Info($"{StrideProbe.Label}: not ridden ({Said(r)}), skipped"); continue; }
                            yield return Until(() => Mounts.IsMounted, 5f);
                            yield return Seconds(0.6f);
                            Vector3 from = Mount.Ridden != null && Mount.Ridden.Donkey != null ? Mount.Ridden.Donkey.transform.position : Vector3.zero;
                            surveyStick = new Vector2(1f, 0f);
                            yield return Seconds(3.5f);
                            surveyStick = Vector2.zero;
                            yield return Seconds(0.4f);
                            surveyStick = new Vector2(-1f, 0f);
                            yield return Seconds(3.5f);
                            surveyStick = null;
                            yield return Seconds(0.4f);
                            Vector3 to = Mount.Ridden != null && Mount.Ridden.Donkey != null ? Mount.Ridden.Donkey.transform.position : Vector3.zero;
                            Info($"{StrideProbe.Label}: driven right 3.5 s and left 3.5 s ({Mounts.Style}; ended {(to - from).magnitude:0.0} units from the start)");
                        }
                        finally
                        {
                            surveyStick = null;
                            if (Mounts.IsMounted) Mounts.Dismount("self-test");
                        }
                        yield return Seconds(0.3f);
                        try { if (h != null && h.Exists) h.Remove(); } catch { }
                        yield return Seconds(0.3f);
                    }
                }

                // standing: a grown dragon and a baby shuffling near the keeper (whatever they do in 15 s)
                StrideProbe.Label = "standing";
                MountHandle g = Spawn(Spot(home, 2.5f) ?? home, right: true);
                MountHandle b = Spawn(Spot(home, 3.5f) ?? home, right: false, scale: 0.6f);
                if (g != null) { yield return Until(() => g.IsReady, 6f); g.Tuning.Set(Owner, "Skin", Sk("dragon")); }
                if (b != null) { yield return Until(() => b.IsReady, 6f); b.Tuning.Set(Owner, "Skin", Sk("dragon")); }
                yield return Seconds(15f);
                try { if (g != null && g.Exists) g.Remove(); } catch { }
                try { if (b != null && b.Exists) b.Remove(); } catch { }
                Info("standing: a grown dragon and a baby left to shuffle 15 s near the keeper");
                Info("the rows: " + StrideProbe.FilePath + $" ({StrideProbe.Rows} so far)");
                Check(StrideProbe.Rows > 0, "the probe wrote rows");
            }
            finally
            {
                surveyStick = null;
                StrideProbe.Label = "";
                StrideProbe.On = false;
                try { surveyHarmony?.UnpatchSelf(); } catch { }
                surveyHarmony = null;
            }
        }
    }
}
