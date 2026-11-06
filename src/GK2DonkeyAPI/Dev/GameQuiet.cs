using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GK2DonkeyAPI
{
    // While the self-tests run (SelfTest.Drive), the game's own story is held. F11 of 2026-11-06: the yard's jump test
    // slid the keeper into GDZone_Intro_Graveyard_Morgue. Its one-time trigger started the story event
    // Event_33_Base_Complain: the keeper's controls taken for its talk (the bubble the user saw), quest
    // 33_base_complain_morgue completed and 42_crossroad_nun_donkey started, and the 14 tests after it refused ("Not
    // right now."). In memory only (the game saves on waking), but a sleep after the run would have kept it.
    // - GlobalEventsSystem.FireTrigger: the one place a trigger starts, finishes or fails a quest (a zone or a world
    //   zone walked into, a teleport, an interaction, a speech...). Skipped while a run goes on, so the trigger stays
    //   armed for the player.
    // - GDZone.ExecuteEvent: a zone's own flow scripts (on enter, exit, crossed), skipped too.
    // - The visited world zones (the save's knowledge): the ones the run's walks added are taken off again at its end.
    // Patched at the run's start and unpatched at its end (its own Harmony id), so a player's game is never hooked.
    // Each held trigger something waits on is said once; the counts at the end.
    internal static class GameQuiet
    {
        private static Harmony harmony;
        private static readonly HashSet<string> said = new HashSet<string>(StringComparer.Ordinal);
        private static List<string> visitedBefore;
        private static int heldTriggers, heldScripts;

        internal static void Begin()
        {
            said.Clear();
            heldTriggers = 0;
            heldScripts = 0;
            List<string> visited = MainGame.Instance?.GameSave?.knowledgeSystem?.visitedWorldZones;
            visitedBefore = visited != null ? new List<string>(visited) : null;
            try
            {
                harmony = new Harmony(Plugin.Guid + ".selftest-quiet");
                Patch(AccessTools.DeclaredMethod(typeof(GlobalEventsSystem), "FireTrigger"), "GlobalEventsSystem.FireTrigger", nameof(TriggerPrefix));
                Patch(AccessTools.DeclaredMethod(typeof(GDZone), "ExecuteEvent"), "GDZone.ExecuteEvent", nameof(ZoneScriptPrefix));
                Plugin.Log.LogInfo("[test] the game's own story is held for the run: no quest trigger fires, no zone runs its script, and the world zones the walks visit are taken off the visited list at the end.");
            }
            catch (Exception e)
            {
                // Said as a warning: a run without the hold can start a story event and change quests in the save.
                Plugin.Log.LogWarning("[test] the game's own story could not be held for the run (" + e.Message + "): a test that moves the keeper may start a quest's event.");
                try { harmony?.UnpatchSelf(); } catch { }
                harmony = null;
            }
        }

        internal static void End()
        {
            try { harmony?.UnpatchSelf(); } catch (Exception e) { Plugin.Log.LogWarning("[test] unholding the game's story failed: " + e.Message); }
            harmony = null;
            var taken = new List<string>();
            List<string> visited = MainGame.Instance?.GameSave?.knowledgeSystem?.visitedWorldZones;
            if (visited != null && visitedBefore != null)
            {
                for (int k = visited.Count - 1; k >= 0; k--)
                {
                    if (!visitedBefore.Contains(visited[k]))
                    {
                        taken.Add(visited[k]);
                        visited.RemoveAt(k);
                    }
                }
            }
            visitedBefore = null;
            Plugin.Log.LogInfo($"[test] the game's own story held during the run: {heldTriggers} quest trigger(s), {heldScripts} zone script(s); " +
                (taken.Count == 0 ? "no world zone newly visited." : $"{taken.Count} world zone(s) the walks visited taken off the visited list again ({string.Join(", ", taken)})."));
        }

        // The target looked up at the call (a literal type and name: the dependency manifest reads those).
        private static void Patch(MethodInfo original, string what, string prefix)
        {
            if (original == null)
            {
                throw new InvalidOperationException($"{what} not found.");
            }
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(GameQuiet), prefix));
        }

        private static bool TriggerPrefix(GlobalEventsSystem.Event.Type type, string id)
        {
            if (!SelfTest.Running)
            {
                return true;
            }
            // Something waits on it (a quest's start or finish check): said once, counted. Without one the game's own
            // FireTrigger does nothing either.
            GlobalEventsSystem events = MainGame.Instance?.GameSave?.globalEventsSystem;
            if (events != null && events.GetEvents(type, out Dictionary<string, GlobalEventsSystem.Event> byId) && byId != null && byId.ContainsKey(id ?? ""))
            {
                heldTriggers++;
                if (said.Add("t|" + type + "|" + id))
                {
                    Plugin.Log.LogInfo($"[test] held the game's trigger {type} '{id}': a quest waits on it, and a test run starts or finishes none.");
                }
            }
            return false;
        }

        private static bool ZoneScriptPrefix(GDZone __instance, GDZone.GDZoneEvent e)
        {
            if (!SelfTest.Running)
            {
                return true;
            }
            if (e != null && e.flowScript != null)
            {
                heldScripts++;
                string zone = __instance != null ? __instance.name : "?";
                if (said.Add("z|" + zone + "|" + e.flowScript.name))
                {
                    Plugin.Log.LogInfo($"[test] held the zone {zone}'s script {e.flowScript.name}: a test run runs none.");
                }
            }
            return false;
        }
    }
}
