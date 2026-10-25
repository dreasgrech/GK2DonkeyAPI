using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The walk survey (the user, 2026-10-24: the footsteps, the movement and the legs "are not synchronized"; "can this
    // be automated"): every mount ridden on the ground, riding and with the cart, steered right and then left by the
    // test (the keeper's stick: LazyInput.GetDirection answered while the test drives, patched only for the run), with
    // a standing one and a baby left to shuffle near; the stride probe (Dev/StrideProbe.cs) writes a row per walk cycle
    // into BepInEx/GK2-strides/. Measures only: nothing is judged (the rows are read afterwards).
    internal static partial class SelfTest
    {
        private const string GroupWalk = "Walk";
        private static Vector2? surveyStick;
        private static Harmony surveyHarmony;

        private static void SurveyStickPostfix(ref Vector2 __result)
        {
            if (surveyStick.HasValue) __result = surveyStick.Value;
        }

        private static IEnumerator WalkSurvey()
        {
            if (!Plugin.MountsOn) { Skip("the mounts are off (Debug.MountsPreview)"); yield break; }
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
