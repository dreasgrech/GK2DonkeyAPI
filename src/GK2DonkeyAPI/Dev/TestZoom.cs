using System;
using System.Reflection;
using HarmonyLib;

namespace GK2DonkeyAPI
{
    // While the self-tests run (SelfTest.Drive), the camera is at the game's own zoom (100%), whatever a zoom mod has.
    // F11 of 2026-11-12: Grave Zoom at 400% (its ZoomFactor, remembered across sessions) left 3.2 world units each side
    // of the keeper, and a test's pen 3.5 from him was off screen (measured: x 2680 of 2560), so a baby rightly caught up
    // out of sight and the test failed; the green run the day before was at another zoom. The user: "always run the test
    // at normal 100% zoom and make sure grave zoom is disabled during the tests".
    // How a zoom mod zooms (Grave Zoom 1.3.1, decompiled): a Harmony postfix on CameraSystem.CalculateOrthographicSize
    // scales the game's size, and CameraSystem.OnResolutionChanged applies it again. So for the run two postfixes of
    // our own on the same method: one first of all (Priority.First) keeps the game's own value, one last of all
    // (Priority.Last) gives it back, then OnResolutionChanged applies it. Every postfix between them (any zoom mod's)
    // counts for nothing, its hotkeys and triggers too (each zoom step only re-applies through the same method). Nothing
    // of the other mod is touched: its config, saved zoom and patches stay as they are, and at the run's end (done or
    // aborted) ours are taken off and the size applied again, so the player's zoom is back. Patched at the run's start
    // and unpatched at its end (its own Harmony id), as GameQuiet: a player's game is never hooked.
    internal static class TestZoom
    {
        private static Harmony harmony;
        private static float gameValue; // the game's own value of the call in progress (the main thread only)

        internal static void Begin()
        {
            float before = Size();
            try
            {
                harmony = new Harmony(Plugin.Guid + ".selftest-zoom");
                MethodInfo original = AccessTools.DeclaredMethod(typeof(CameraSystem), "CalculateOrthographicSize");
                if (original == null)
                {
                    throw new InvalidOperationException("CameraSystem.CalculateOrthographicSize not found.");
                }
                harmony.Patch(original, postfix: new HarmonyMethod(typeof(TestZoom), nameof(KeepGameValue)) { priority = Priority.First });
                harmony.Patch(original, postfix: new HarmonyMethod(typeof(TestZoom), nameof(GiveGameValue)) { priority = Priority.Last });
                Apply();
                float now = Size();
                string found = before > 0f && now > 0f && Math.Abs(before - now) > 0.0001f
                    ? $"it was at {100f * now / before:0}% (a zoom mod's), held off for the run"
                    : "it was there already";
                Plugin.Log.LogInfo($"[test] the camera at the game's own zoom (100%) for the run: {found}; zoom keys do nothing until the run ends.");
            }
            catch (Exception e)
            {
                // Said as a warning: a run at another zoom puts things on or off screen that a test means otherwise.
                Plugin.Log.LogWarning("[test] the camera could not be held at the game's own zoom for the run (" + e.Message + "): tests that need a spot on screen may fail.");
                try { harmony?.UnpatchSelf(); } catch { }
                harmony = null;
            }
        }

        internal static void End()
        {
            if (harmony == null)
            {
                return;
            }
            try { harmony.UnpatchSelf(); } catch (Exception e) { Plugin.Log.LogWarning("[test] the camera's zoom hold could not be taken off: " + e.Message); }
            harmony = null;
            try
            {
                Apply();
                Plugin.Log.LogInfo("[test] the camera's zoom given back to the player (any zoom mod's again).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[test] the camera's zoom could not be applied again after the run (" + e.Message + "): a resolution change or a zoom key puts it back.");
            }
        }

        // The game applies the size for the screen as it is: a zoom mod's reapply is this same call.
        private static void Apply()
        {
            CameraSystem cam = CameraSystem.Instance;
            if (cam != null && GameSettings.Instance != null)
            {
                cam.OnResolutionChanged(GameSettings.Instance.GetResolutionIntVector2());
            }
        }

        // The size for a fixed screen, as patched now: only its ratio before and after is read.
        private static float Size()
        {
            try { return CameraSystem.CalculateOrthographicSize(1080, 2); } catch { return 0f; }
        }

        private static void KeepGameValue(float __result) => gameValue = __result;

        private static void GiveGameValue(ref float __result) => __result = gameValue;
    }
}
