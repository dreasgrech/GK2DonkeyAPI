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
    // Moved from the API's Dev/SelfTest.Garbage.cs (2026-10-27): the scenarios that need GK2 Saddle Up or
    // one of its packs, run by the API's self-test runner through its helpers (using static SelfTest).
    internal static partial class Tests
    {
        // the garbage survey's own group in the Tests tab
        internal const string GroupGarbage = "Garbage";

        internal static IEnumerator GarbageSurvey()
        {
            string noPack = MissingPack("dragon"); // our mount packs may be left out: Saddle Up alone, or one pack
            if (noPack != null) { Skip(noPack); yield break; }
            bool bySection = Perf.AllocBySection, probe = StrideProbe.On;
            var made = new List<MountHandle>();
            try
            {
                Perf.AllocBySection = true;
                StrideProbe.On = false;
                surveyHarmony = new Harmony(Plugin.Guid + ".garbagesurvey");
                surveyHarmony.Patch(AccessTools.Method(typeof(LazyInput), nameof(LazyInput.GetDirection)),
                    postfix: new HarmonyMethod(typeof(SelfTest), nameof(SurveyStickPostfix)));
            }
            catch (Exception e)
            {
                Perf.AllocBySection = bySection;
                StrideProbe.On = probe;
                Fail("the stick could not be patched: " + e.Message);
                yield break;
            }
            try
            {
                // a dragon with two babies and a donkey standing near the keeper
                MountHandle parent = Spawn(Spot(home, 3f) ?? home, right: true);
                if (parent == null) { Fail("could not spawn the dragon"); yield break; }
                made.Add(parent);
                yield return Until(() => parent.IsReady, 6f);
                parent.Tuning.Set(Owner, "Skin", Sk("dragon"));
                yield return Until(() => parent.Mount == M("dragon"), 8f);
                if (!waited) Info($"the dragon's look did not take ({parent.Mount}); measured with what it wears");
                for (int k = 0; k < 2; k++)
                {
                    MountHandle baby = Mounts.SpawnFoal(Owner, parent, 0.6f, persist: false);
                    if (baby != null) made.Add(baby);
                }
                MountHandle other = Spawn(Spot(home, 4f) ?? home, right: false);
                if (other != null) made.Add(other);
                foreach (MountHandle h in made) yield return Until(() => !h.Exists || h.IsReady, 6f);
                yield return Seconds(3f); // the looks built, the views settled

                // 1. on foot, standing
                Perf.PhaseStart();
                yield return Seconds(10f);
                Info(Perf.PhaseEnd("on foot, standing near a dragon, its two babies and a donkey"));

                // 2. on foot, walking
                Perf.PhaseStart();
                for (int k = 0; k < 2; k++)
                {
                    surveyStick = new Vector2(1f, 0f);
                    yield return Seconds(2.5f);
                    surveyStick = new Vector2(-1f, 0f);
                    yield return Seconds(2.5f);
                }
                surveyStick = null;
                Info(Perf.PhaseEnd("on foot, walking right and left"));

                // 3. riding, walking; 4. riding, standing still
                MountResult r = parent.Ride(showMessage: false);
                if (!r.Ok) Info($"the dragon was not ridden ({Said(r)}): the riding phases skipped");
                else
                {
                    yield return Until(() => Mounts.IsMounted, 5f);
                    yield return Seconds(1f);
                    Perf.PhaseStart();
                    for (int k = 0; k < 2; k++)
                    {
                        surveyStick = new Vector2(1f, 0f);
                        yield return Seconds(2.5f);
                        surveyStick = new Vector2(-1f, 0f);
                        yield return Seconds(2.5f);
                    }
                    surveyStick = null;
                    Info(Perf.PhaseEnd("riding the dragon, walking right and left, its babies following"));
                    yield return Seconds(0.5f);
                    Perf.PhaseStart();
                    yield return Seconds(5f);
                    Info(Perf.PhaseEnd("riding the dragon, standing still"));

                    // 6. the dev stride probe on, riding and walking (its own cost)
                    if (DevTools.Unlocked)
                    {
                        StrideProbe.On = true;
                        Perf.PhaseStart();
                        surveyStick = new Vector2(1f, 0f);
                        yield return Seconds(2.5f);
                        surveyStick = new Vector2(-1f, 0f);
                        yield return Seconds(2.5f);
                        surveyStick = null;
                        Info(Perf.PhaseEnd("riding and walking with the dev stride probe on (DevProbe is its part)"));
                        StrideProbe.On = false;
                    }
                    Mounts.Dismount("self-test");
                    yield return Seconds(0.5f);
                }

                // 5. the Mounts panel, a mount picked every half second
                MountSettingsPage page = null;
                foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow) { page = live; break; }
                if (page == null || LazyWindowsStackController.ActiveWindow != null) Info("no settings page with a Mount row, or a game window open: the panel phase skipped");
                else
                {
                    MountsPanel.Open(page, toggle: false);
                    yield return Seconds(1f);
                    var ids = new List<string> { MountCatalog.DonkeyId };
                    foreach (string id in new[] { "dragon", "rat", "snail", "worm", "gerry" }) if (Catalog.Find(M(id)) != null) ids.Add(M(id));
                    Perf.PhaseStart();
                    for (int k = 0; k < 20; k++)
                    {
                        MountsPanel.TestSelect(ids[k % ids.Count]);
                        yield return Seconds(0.5f);
                    }
                    Info(Perf.PhaseEnd($"the Mounts panel open, a mount picked every half second ({ids.Count} mounts, 20 picks)"));
                    MountsPanel.TestClose(keep: false); // the player's own choice put back
                    yield return Seconds(1f);
                }
            }
            finally
            {
                surveyStick = null;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                Perf.PhaseAbort();
                Perf.AllocBySection = bySection;
                StrideProbe.On = probe;
                try { surveyHarmony?.UnpatchSelf(); } catch { }
                surveyHarmony = null;
                foreach (MountHandle h in made) { try { if (h != null && h.Exists) h.Remove(); } catch { } }
            }
            Check(true, "the garbage survey ran (its lines above: the garbage by part in each phase)");
        }
    }
}
