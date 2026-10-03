using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2DonkeyAPI
{
    // Navmesh holes of the mod's donkeys (docs/foals-indoors.md, "The door bug"). A WgoData can make the game cut holes in
    // the navmesh the keeper walks on, outside its view, held by its UniqueId in GlobalNavigationManager:
    //   - a forced hole at the place it was made (WgoData's constructor, SetupForcedNavigationHoleIfNeeded, when its
    //     WGODef says SpawnHoleForce): GraphCutUnit / GraphUpdateSceneUnit, never moved;
    //   - the part's baked cut prefabs (GraphCustomNavMeshCutUnit), tracked to its position, added again by the game's
    //     Wgo.UpdateWgoPartState.
    // The game's own donkey may need them; the mod's donkeys must never block the keeper (a donkey parked in front of a
    // door shut him in the doorway, 2026-10-03). GlobalNavigationManager.RemoveCutUnit(holder) removes all three kinds.
    // Which kinds a view had is read from the manager's own dictionaries (private) and logged, so the log says what
    // was there.
    internal static class NavHoles
    {
        private static readonly FieldInfo Cuts = AccessTools.Field(typeof(GlobalNavigationManager), "usedUnits");
        private static readonly FieldInfo SceneUpdates = AccessTools.Field(typeof(GlobalNavigationManager), "usedGraphUpdateSceneUnits");
        private static readonly FieldInfo CustomCuts = AccessTools.Field(typeof(GlobalNavigationManager), "usedCustomNavMeshCutUnits");
        private static int logged;
        private static bool failed;

        // Removes every hole the game holds for this donkey's data. Returns what was there ("" for nothing).
        internal static string Clear(WgoData data)
        {
            if (failed || data == null)
            {
                return "";
            }
            try
            {
                GlobalNavigationManager nav = LazySingleton<GlobalNavigationManager>.Instance;
                if (nav == null)
                {
                    return "";
                }
                SGuid id = data.UniqueId;
                string found = Has(nav, Cuts, id, "cut") + Has(nav, SceneUpdates, id, "scene update") + Has(nav, CustomCuts, id, "baked cut prefabs");
                if (found.Length > 0)
                {
                    data.StopCustomNavMeshCutTracking(); // else the next part-state update adds the baked cuts again
                    nav.RemoveCutUnit(id);
                }
                return found;
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogWarning("Mount: the donkeys' navmesh holes can't be checked this session (" + e.Message + "); a parked donkey may block the keeper's way.");
                return "";
            }
        }

        // Clear, with a log line for the first few views that had holes.
        internal static void ClearAndLog(WgoData data, string who)
        {
            string found = Clear(data);
            if (found.Length > 0 && logged < 5)
            {
                logged++;
                Plugin.Log.LogInfo($"Mount: {who} cut holes in the keeper's navmesh ({found.TrimEnd(',', ' ')}; the game's own setting for {data.id}: {HoleSetting(data)}); removed.");
            }
        }

        private static string Has(GlobalNavigationManager nav, FieldInfo f, SGuid id, string name)
        {
            if (f == null)
            {
                return "";
            }
            return f.GetValue(nav) is IDictionary d && d.Contains(id) ? name + ", " : "";
        }

        private static string HoleSetting(WgoData data)
        {
            try
            {
                return data.Definition != null ? data.Definition.forceSetNavigationHoleType.ToString() : "no definition";
            }
            catch
            {
                return "?";
            }
        }
    }
}
