using System;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2DonkeyAPI
{
    // The game's Halloween head swap forced for a self-test (docs/game-update-26404424.md "Halloween"): a prefix on
    // HalloweenHeadSkinOverride.Resolve, which every head and every lying or carried zombie picture goes through
    // (ResolveDropIconId, SkinPresetGK2), so a scenario sees the swap on or off on any date. Begin, Set, End in a finally.
    internal static class HalloweenSwap
    {
        private static bool? force; // null: the game's own date
        private static Harmony harmony;

        // False with the reason when the method is not there or can't be patched (a game update): the scenario skips.
        internal static bool Begin(out string error)
        {
            End();
            error = null;
            try
            {
                // looked up by a literal type and name (the dependency manifest reads those)
                MethodInfo resolve = AccessTools.DeclaredMethod(typeof(HalloweenHeadSkinOverride), "Resolve", new[] { typeof(int) });
                if (resolve == null)
                {
                    error = "HalloweenHeadSkinOverride.Resolve not found";
                    return false;
                }
                harmony = new Harmony(Plugin.Guid + ".selftest.halloween");
                harmony.Patch(resolve, prefix: new HarmonyMethod(typeof(HalloweenSwap), nameof(ResolvePrefix)));
                return true;
            }
            catch (Exception e)
            {
                error = "patching HalloweenHeadSkinOverride.Resolve failed: " + e.Message;
                End();
                return false;
            }
        }

        // On: the table's swap whatever the date; off: every head its own.
        internal static void Set(bool on) => force = on;

        internal static void End()
        {
            force = null;
            if (harmony == null) return;
            try { harmony.UnpatchSelf(); } catch (Exception e) { Plugin.Log.LogWarning("[test] unpatching HalloweenHeadSkinOverride.Resolve failed: " + e.Message); }
            harmony = null;
        }

        private static bool ResolvePrefix(int headId, ref int __result)
        {
            if (force == null)
            {
                return true;
            }
            HalloweenHeadSkinOverride table = force.Value ? LazySingletonSO<HalloweenHeadSkinOverride>.Instance : null;
            __result = table != null ? table.Replace(headId) : headId;
            return false;
        }
    }
}
