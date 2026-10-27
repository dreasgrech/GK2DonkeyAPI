using System;
using System.Reflection;
using FlowCanvas;
using GK2.FlowCanvasNodes;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Corpses at the crossroads (docs/corpse-delivery.md): the game's own donkey leaves the bodies it brings where it
    // appears, by the nun, instead of putting them on the morgue pallets, so the keeper hauls them himself (the cart
    // takes them).
    //
    // How the game does it: the delivery is the donkey's visual script (the FlowCanvas graph npc_donkey), fired every
    // second day. It puts a dummy body on the donkey, teleports it to GD point donkey_01 (behind a tree at the
    // crossroads) and walks it to donkey_05 at the morgue; on arrival ("full_cycle_back") it removes the dummy, and per
    // body runs "Generate Body" -> "Try To Add Corpse To Pallet" -> "Show New Body Notification" -> cur_bodies_count + 1;
    // with no morgue door, or full pallets, its own "Drop Body" nodes drop the body beside or inside the morgue instead.
    // Then it walks back to donkey_01 and "Disable" teleports it off-map.
    //
    // What this does, with three Harmony prefixes that exist only while Mounts.Corpses "CorpsesAtCrossroads" is on
    // (Tick applies them when it turns on and removes them when it turns off; off means the game's donkey code is not
    // hooked at all):
    //  - the pallet node: the generated body is dropped at CorpsesDropPoint with the game's drop system and the node's
    //    "yes" is called, so the graph itself still notifies, counts and fires its quest triggers;
    //  - the graph's own two fallback drops (no door / pallets full), while the donkey is unloading: the same;
    //  - the walk node: the delivery walk goes to CorpsesStandPoint (out from behind the tree, beside the nun) with the
    //    same arrival event, so the graph unloads there; the walk back stays the game's. A quest that waits for the
    //    donkey at the morgue keeps the game's walk.
    // The "New Body" notification is replaced by CorpsesNotification for a body this left (same frame), through a
    // fourth prefix. Nothing of this mod's own goes into the save: the body is the game's instance, dropped by the
    // game's drop system; the unload timing uses the game's own delayed WGO events only when the donkey unloads on the
    // spot.
    internal static class Corpses
    {
        internal const string Section = "Corpses";

        // [Corpses] (Plugin binds them; overridable per session through Mounts.Corpses, a mod may set the first).
        internal static Tuned<bool> AtCrossroads;      // the feature: off = the game's own delivery, nothing patched
        internal static Tuned<bool> UnloadAtCrossroads; // the donkey walks out beside the nun and unloads there; off: it walks to the morgue, the bodies still appear at the drop point
        internal static Tuned<string> StandPoint;      // GD point it walks to and unloads at
        internal static Tuned<string> DropPoint;       // GD point the bodies are left at
        internal static Tuned<bool> NeverRot;          // the game's never-auto-destroy property on the bodies it leaves
        internal static Tuned<string> Notification;    // text instead of "New Body"; empty = the game's
        internal static Tuned<float> UnloadDelay;      // on-the-spot unload only: seconds before the bodies appear
        internal static Tuned<float> LeaveDelay;       // on-the-spot unload only: seconds before it vanishes

        internal static ITuned[] Settings => new ITuned[] { AtCrossroads, UnloadAtCrossroads, StandPoint, DropPoint, NeverRot, Notification, UnloadDelay, LeaveDelay };

        // Patched right now (the prefixes are in).
        internal static bool Applied { get; private set; }
        internal static string ApplyError { get; private set; }

        private static Harmony harmony;
        private static bool failed;

        // A patch target missing at start (a game update): this feature stays off, the rest of the API runs.
        internal static void Unavailable(string why)
        {
            failed = true;
            ApplyError = "unavailable: " + why;
            Plugin.Log.LogError("Corpses: unavailable, the game's code changed (" + why + "). The game's own delivery runs; the rest of GK2 Donkey API is unaffected.");
        }

        // The start-up check found the game changed code this feature uses (GameCode.cs): off for the session, the
        // switch does nothing (the gate's warning said so; the game's own delivery runs).
        internal static void Freeze(string why)
        {
            failed = true;
            ApplyError = "unavailable: " + why;
        }

        // Called every frame by the plugin: the patches follow the switch.
        internal static void Tick()
        {
            bool want = Plugin.Patched && Plugin.Enabled.Value && AtCrossroads != null && AtCrossroads.Value;
            if (want == Applied)
            {
                return;
            }
            if (want)
            {
                if (failed)
                {
                    return; // tried this session and failed (or a target is missing): off until the switch is turned off and on again
                }
                Apply();
            }
            else
            {
                Remove();
                if (ApplyError != null && !ApplyError.StartsWith("unavailable"))
                {
                    failed = false; // a failed Apply may be retried; a missing target (Unavailable) stays off
                }
            }
        }

        private static void Apply()
        {
            try
            {
                harmony = new Harmony(Plugin.Guid + ".corpses");
                Patch(AccessTools.DeclaredMethod(typeof(Flow_TryToAddCorpseToPallet), "TryToAddCorpseToPallet"), "Flow_TryToAddCorpseToPallet.TryToAddCorpseToPallet", nameof(PalletPrefix));
                Patch(AccessTools.DeclaredMethod(typeof(Flow_DropItem), "Drop"), "Flow_DropItem.Drop", nameof(DropPrefix));
                Patch(AccessTools.DeclaredMethod(typeof(Flow_GoTo), "GoTo"), "Flow_GoTo.GoTo", nameof(GoToPrefix));
                harmony.Patch(AccessTools.Method(typeof(UINotificator), nameof(UINotificator.ShowNewBodyNotification), new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(Corpses), nameof(NotificationPrefix)));
                Applied = true;
                ApplyError = null;
                Plugin.Log.LogInfo("Corpses: on. The game's donkey leaves the bodies it brings at the crossroads (" + Describe() + ").");
            }
            catch (Exception e)
            {
                failed = true;
                ApplyError = e.GetType().Name + ": " + e.Message;
                Plugin.Log.LogError("Corpses: patching failed; the game's own delivery runs. " + e);
                try { harmony?.UnpatchSelf(); } catch { }
                harmony = null;
                Applied = false;
            }
        }

        // The target is looked up at the call (a literal type and name: the dependency manifest reads those).
        private static void Patch(MethodInfo original, string what, string prefix)
        {
            if (original == null)
            {
                throw new InvalidOperationException($"{what} not found.");
            }
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(Corpses), prefix));
        }

        internal static void Remove()
        {
            if (!Applied && harmony == null)
            {
                return;
            }
            try { harmony?.UnpatchSelf(); } catch (Exception e) { Plugin.Log.LogWarning("Corpses: unpatching failed. " + e); }
            harmony = null;
            if (Applied)
            {
                Plugin.Log.LogInfo("Corpses: off. The game's donkey delivers to the morgue as it does on its own.");
            }
            Applied = false;
        }

        // The private ports the prefixes take, checked at start (Access.Verify) so a renamed one disables the mod
        // rather than failing at the first delivery.
        internal static void Verify()
        {
            // One literal per lookup: the dependency manifest lists each (a computed name would hide it from the check).
            Port(AccessTools.Field(typeof(Flow_TryToAddCorpseToPallet), "item"), "Flow_TryToAddCorpseToPallet.item");
            Port(AccessTools.Field(typeof(Flow_TryToAddCorpseToPallet), "yes"), "Flow_TryToAddCorpseToPallet.yes");
            Port(AccessTools.Field(typeof(Flow_DropItem), "out"), "Flow_DropItem.out");
            Port(AccessTools.Field(typeof(Flow_DropItem), "item"), "Flow_DropItem.item");
            Port(AccessTools.Field(typeof(Flow_DropItem), "gdPointDataDropOn"), "Flow_DropItem.gdPointDataDropOn");
            Port(AccessTools.Field(typeof(Flow_GoTo), "out"), "Flow_GoTo.out");
            Port(AccessTools.Field(typeof(Flow_GoTo), "onFinish"), "Flow_GoTo.onFinish");
            Port(AccessTools.Field(typeof(Flow_GoTo), "fireEventOnFinish"), "Flow_GoTo.fireEventOnFinish");
            Port(AccessTools.Field(typeof(Flow_GoTo), "gdPointData"), "Flow_GoTo.gdPointData");
            Port(AccessTools.Field(typeof(Flow_GoTo), "gdPointId"), "Flow_GoTo.gdPointId");
            Port(AccessTools.Field(typeof(Flow_GoTo), "useGdDataInsteadId"), "Flow_GoTo.useGdDataInsteadId");
            Port(AccessTools.Field(typeof(Flow_GoTo), "navigation"), "Flow_GoTo.navigation");
            Port(AccessTools.Field(typeof(Flow_GoTo), "speed"), "Flow_GoTo.speed");
            if (GetWgoDataMethod == null) throw new InvalidOperationException("GKCustomFlowNodeWithWgoData.GetWgoData not found.");
            if (AccessTools.DeclaredMethod(typeof(Flow_TryToAddCorpseToPallet), "TryToAddCorpseToPallet") == null
                || AccessTools.DeclaredMethod(typeof(Flow_DropItem), "Drop") == null
                || AccessTools.DeclaredMethod(typeof(Flow_GoTo), "GoTo") == null
                || AccessTools.Method(typeof(UINotificator), nameof(UINotificator.ShowNewBodyNotification), new[] { typeof(string) }) == null)
            {
                throw new InvalidOperationException("A corpse-delivery patch target is missing.");
            }
        }

        private static void Port(FieldInfo f, string what)
        {
            if (f == null) throw new InvalidOperationException(what + " not found.");
        }

        internal static string Describe()
        {
            return $"drop point {DropPoint.Value}, {(UnloadAtCrossroads.Value ? "unloads at " + StandPoint.Value : "walks to the morgue")}, never rot {NeverRot.Value}";
        }

        // ---------------------------------------------------------------- the drop

        // Set by the pallet / fallback prefixes for each body redirected, consumed by the notification prefix in the
        // same frame (the graph's loop is synchronous).
        private static int redirectedPending;
        private static int redirectedFrame = -1;

        private static bool pointWarned, failWarned;
        internal static void ResetWarnings() { pointWarned = false; failWarned = false; }

        internal static bool TryLeaveAtCrossroads(Item body) => TryLeaveAt(body, DropPoint.Value);

        // The body goes to the ground at the GD point. True = done (the caller calls the node's "yes" / "out" and skips
        // the game's code). False = nothing happened to the world; the caller lets the game's code run (it generates a
        // body of its own; the instance read here is never used). pointId apart from the setting for the self-tests.
        internal static bool TryLeaveAt(Item body, string pointId)
        {
            try
            {
                if (body == null || body.IsEmpty || body.Definition == null)
                {
                    Warn(ref failWarned, "the generated body is empty; the game's own delivery runs");
                    return false;
                }
                // DropSystem.DropItem keeps the instance only for a single item; its Count > 1 path builds fresh items
                // by id, which would lose a body's parts, pockets and burial reward. Bodies are always 1.
                if (body.Count != 1)
                {
                    Warn(ref failWarned, $"body {body.id} has count {body.Count}; the game's own delivery runs");
                    return false;
                }
                WorldData world = MainGame.Instance?.GameSave?.worldData;
                GDPointData point = string.IsNullOrEmpty(pointId) ? null : world?.gdPointsData?.GetGDPointDataById(pointId);
                if (point == null || string.IsNullOrEmpty(point.GameSceneDataId))
                {
                    Warn(ref pointWarned, $"drop point \"{pointId}\" not found in this world; the game's own delivery runs");
                    return false;
                }
                if (NeverRot.Value && !body.HasProperty<NeverAutoDestroyDropSerializedItemProperty>())
                {
                    body.AddProperty(new NeverAutoDestroyDropSerializedItemProperty());
                }
                if (!MainGame.Instance.dropSystem.DropItem(body, point.GameSceneDataId, point.Position))
                {
                    Warn(ref failWarned, $"the game refused to drop {body.id} at {point.Id}; the game's own delivery runs");
                    return false;
                }
                if (redirectedFrame != Time.frameCount)
                {
                    redirectedPending = 0;
                    redirectedFrame = Time.frameCount;
                }
                redirectedPending++;
                Plugin.Log.LogInfo($"Corpses: {body.id} left at {point.Id} {point.Position:F1} in {point.GameSceneDataId}{(NeverRot.Value ? ", never rots" : "")}.");
                Mounts.RaiseCorpseLeft(new CorpseInfo(body.id, body.UniqueId.ToString(), point.Id, point.Position));
                return true;
            }
            catch (Exception e)
            {
                Warn(ref failWarned, "failed; the game's own delivery runs. " + e);
                return false;
            }
        }

        // The notification that follows a redirected body, in the same frame.
        internal static bool TakeRedirectedNotification()
        {
            if (redirectedFrame != Time.frameCount)
            {
                redirectedPending = 0;
                return false;
            }
            if (redirectedPending <= 0)
            {
                return false;
            }
            redirectedPending--;
            return true;
        }

        // The body item's icon as the game's notification shows it, or the game's generic body icon.
        internal static string IconFor(string bodyId)
        {
            try
            {
                BodyDef body = GameBalance.Me.GetData<BodyDef>(bodyId);
                ItemDef item = body == null ? null : GameBalance.Me.GetData<ItemDef>(body.linkedBodyItemId);
                string iconId = item?.iconId;
                if (!string.IsNullOrEmpty(iconId) && LazySingletonSO<EasySpritesCollection>.Instance.GetSprite(iconId) != null)
                {
                    return iconId;
                }
            }
            catch (Exception)
            {
                // the generic icon
            }
            return "i_body";
        }

        private static void Warn(ref bool already, string what)
        {
            if (already) return;
            already = true;
            Plugin.Log.LogWarning("Corpses: " + what);
        }

        // ---------------------------------------------------------------- the walk

        // The game's graph checks these before deciding whether the donkey waits at the morgue for a scripted talk
        // (nodes 49-56 of npc_donkey). While one holds, the walk stays the game's, or the talk would start at the
        // wrong place.
        private static readonly (string quest, QuestStatus status)[] MorgueTalks =
        {
            ("21_base_donkey_nun", QuestStatus.Awaiting),
            ("137_base_resurrection_donkey", QuestStatus.InProgress),
            ("165_base_donkey1_speak", QuestStatus.InProgress),
            ("166_base_donkey2_speak", QuestStatus.InProgress),
            ("167_base_donkey3_speak", QuestStatus.InProgress),
        };

        internal static string QuestHoldingDonkeyAtMorgue()
        {
            QuestSystemData quests = MainGame.Instance?.GameSave?.questSystemData;
            if (quests == null) return null;
            foreach (var (quest, status) in MorgueTalks)
            {
                if (quests.IsQuestInStatus(quest, status)) return quest;
            }
            return null;
        }

        internal enum WalkAction { Vanilla, UnloadHere, LeaveHere }

        // What to do with a GoTo of the donkey's graph, by the event it fires on arrival. Pure, so the self-tests can
        // drive it with made-up positions.
        //  full_cycle_back: the delivery walk to the morgue. Unload at the crossroads instead, unless a quest talk
        //                   waits at the morgue.
        //  Disable: the walk back before vanishing. A real walk (from the stand point, or from the morgue) stays the
        //           game's; when the donkey already stands on its target (it unloaded on the spot), the game's
        //           movement code would only call the node's onFinish and never fire Disable, so fire it after a beat.
        internal static WalkAction Decide(string finishEvent, bool isDonkey, Vector3 wgoPos, Vector3? target, string questHold)
        {
            if (!isDonkey) return WalkAction.Vanilla;
            if (finishEvent == "full_cycle_back") return questHold == null ? WalkAction.UnloadHere : WalkAction.Vanilla;
            if (finishEvent == "Disable" && target.HasValue)
            {
                Vector3 d = wgoPos - target.Value;
                d.y = 0f;
                return d.magnitude < 1f ? WalkAction.LeaveHere : WalkAction.Vanilla;
            }
            return WalkAction.Vanilla;
        }

        internal static bool IsDonkey(WgoData wgo) => wgo != null && (wgo.id == "npc_donkey" || wgo.CustomTag == "npc_donkey");

        private static readonly MethodInfo GetWgoDataMethod = AccessTools.Method(typeof(GKCustomFlowNodeWithWgoData), "GetWgoData");

        internal static WgoData WgoOf(GKCustomFlowNodeWithWgoData node)
        {
            try { return GetWgoDataMethod?.Invoke(node, null) as WgoData; }
            catch (Exception) { return null; }
        }

        // The stand point, when it is a real walk away from where the donkey is; null to unload on the spot.
        internal static GDPointData StandPointToWalkTo(WgoData wgo)
        {
            GDPointData stand = string.IsNullOrEmpty(StandPoint.Value) ? null : MainGame.Instance?.GameSave?.worldData?.gdPointsData?.GetGDPointDataById(StandPoint.Value);
            if (stand == null)
            {
                Plugin.Log.LogWarning($"Corpses: stand point \"{StandPoint.Value}\" not found; the donkey unloads where it appears.");
                return null;
            }
            Vector3 d = wgo.Position - stand.Position;
            d.y = 0f;
            return d.magnitude > 0.5f ? stand : null;
        }

        // While the donkey unloads at the crossroads (from the walk decision until the walk back), so the graph's own
        // fallback drops of that chain can be told from any other body drop of the donkey's graph.
        internal static float UnloadWindowUntil = -1f;
        internal static bool InUnloadWindow => Time.time < UnloadWindowUntil;

        internal static string DescribeMorgue()
        {
            WorldData world = MainGame.Instance?.GameSave?.worldData;
            bool door = world?.GetWgoData("morgue_entrance_door") != null || world?.GetWgoDataByCustomTag("morgue_entrance_door") != null;
            int pallets = world?.GetWgoDataListByGroup("morgue_pallets")?.Count ?? 0;
            return $"morgue door {(door ? "present" : "absent")}, {pallets} corpse pallet(s), cur_bodies_count {MainGame.PlayerData?.GetResInt("cur_bodies_count")}";
        }

        // Debug aid (Mounts.StartCorpseDelivery, the panel): the game's own full_cycle, with the body roll forced.
        internal static string StartDeliveryNow()
        {
            WorldData world = MainGame.Instance?.GameSave?.worldData;
            WgoData donkey = world?.GetWgoDataByCustomTag("npc_donkey") ?? world?.GetWgoData("npc_donkey");
            if (donkey == null)
            {
                return "no npc_donkey in this world (not in a game, or the donkey isn't in the save yet)";
            }
            // Away from its off-map parking the donkey is on a trip, or waiting at the morgue for a quest talk; a
            // second full_cycle would teleport it back to the start mid-walk.
            GDPointData spot = world.gdPointsData?.GetGDPointDataById("RT_spot");
            if (spot != null && Vector3.Distance(donkey.Position, spot.Position) > 3f)
            {
                return $"not sent: the donkey is already out at {donkey.Position:F1} (a delivery under way, or a quest keeps it at the morgue)";
            }
            return ScriptDump.StartDelivery();
        }

        // ---------------------------------------------------------------- the prefixes (manual Harmony patches, see Apply)

        // Flow_TryToAddCorpseToPallet.TryToAddCorpseToPallet(Flow): vanilla reads item.value once per pallet it tries
        // (the port re-evaluates "Generate Body" on every read), adds it to the first pallet with room and calls yes,
        // else no. Used by the npc_donkey graph and two dev graphs only (every bundle scanned, 2026-10-09).
        private static bool PalletPrefix(Flow flow, ValueInput<Item> ___item, FlowOutput ___yes)
        {
            if (!Applied || ___item == null || ___yes == null)
            {
                return true;
            }
            Item body = ___item.value; // exactly once: every read generates a new body
            if (!TryLeaveAtCrossroads(body))
            {
                return true; // nothing changed; the game's pallet code runs with a body of its own
            }
            // Outside the try/catch on purpose: the rest of the graph runs from here (notification, count, quest
            // triggers), and a failure in it must surface as it would in the unmodified game, not re-run the pallet
            // code on top of a body that is already on the ground.
            ___yes.Call(flow);
            return false;
        }

        // Flow_DropItem.Drop: the graph's own body drops. Its arrival chain uses two as fallbacks instead of the pallet
        // node: at RT_body_spawn (beside the morgue entrance) with no morgue_entrance_door in the save yet, and at
        // RT_morgue_body_spawn (inside the morgue) when every pallet is full. Both go to the crossroads too, but only
        // for the donkey's own graph, only those two points, and only while the donkey is unloading (the graph's
        // quest event drop_body uses the same node and point and stays the game's).
        private static bool DropPrefix(Flow_DropItem __instance, Flow flow, FlowOutput ___out, ValueInput<Item> ___item, ValueInput<GDPointData> ___gdPointDataDropOn)
        {
            if (!Applied || !InUnloadWindow || ___out == null || ___item == null)
            {
                return true;
            }
            try
            {
                if (!__instance.dropBody || __instance.dropTargetType != Flow_DropItem.DropTargetType.GDPoint) return true;
                WgoDataScript script = CustomFlowNode.GetComponentFromNode<WgoDataScript>(__instance);
                if (script == null || !IsDonkey(script.wgoData)) return true;
                string target = ___gdPointDataDropOn?.value?.Id;
                if (target != "RT_body_spawn" && target != "RT_morgue_body_spawn") return true;
                Item body = ___item.value; // exactly once: this port, too, is a fresh "Generate Body" per read
                if (!TryLeaveAtCrossroads(body))
                {
                    return true;
                }
                Plugin.Log.LogInfo($"Corpses: the graph's own fallback drop at {target} ({(target == "RT_body_spawn" ? "no morgue door yet" : "pallets full")}) went to the crossroads instead.");
                ___out.Call(flow);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Corpses: fallback drop check failed; the game's own drop runs. " + e);
                return true;
            }
        }

        private static string holdLogged;

        // Flow_GoTo.GoTo (protected virtual): starts the WGO's walk to a GD point and names the WGO event to fire on
        // arrival. Only the donkey's two walks are looked at; every other GoTo in the game is untouched.
        private static bool GoToPrefix(Flow_GoTo __instance, Flow flow, FlowOutput ___out, FlowOutput ___onFinish, ValueInput<string> ___fireEventOnFinish,
            ValueInput<GDPointData> ___gdPointData, ValueInput<string> ___gdPointId, bool ___useGdDataInsteadId,
            ValueInput<MovementType> ___navigation, ValueInput<float> ___speed)
        {
            if (!Applied || ___out == null)
            {
                return true;
            }
            try
            {
                string finishEvent = ___fireEventOnFinish?.value;
                if (finishEvent != "full_cycle_back" && finishEvent != "Disable") return true;
                WgoData wgo = WgoOf(__instance);
                if (!IsDonkey(wgo)) return true;
                if (!UnloadAtCrossroads.Value)
                {
                    // The game's walk to the morgue and back; the bodies still go to the drop point on arrival (the
                    // pallet prefix, and the fallback prefix while this window is open: the walk takes about 35 s).
                    if (finishEvent == "full_cycle_back")
                    {
                        UnloadWindowUntil = Time.time + 180f;
                        Plugin.Log.LogInfo($"Corpses: the donkey walks to the morgue as in the game; the bodies will be left at {DropPoint.Value} when it arrives ({DescribeMorgue()}).");
                    }
                    else if (InUnloadWindow)
                    {
                        UnloadWindowUntil = -1f;
                    }
                    return true;
                }
                Vector3? target = null;
                if (finishEvent == "Disable")
                {
                    GDPointData p = ___useGdDataInsteadId ? ___gdPointData?.value
                        : MainGame.Instance?.GameSave?.worldData?.gdPointsData?.GetGDPointDataById(___gdPointId?.value);
                    target = p?.Position;
                }
                string hold = finishEvent == "full_cycle_back" ? QuestHoldingDonkeyAtMorgue() : null;
                switch (Decide(finishEvent, true, wgo.Position, target, hold))
                {
                    case WalkAction.UnloadHere:
                    {
                        // As vanilla: Out first (the graph fires its DonkeyStart quest trigger from it), then the walk.
                        ___out.Call(flow);
                        UnloadWindowUntil = Time.time + 60f; // closed again when the walk back starts
                        GDPointData stand = StandPointToWalkTo(wgo);
                        if (stand != null && wgo.MovementComponent != null)
                        {
                            // The node's own call with our destination: same navigation, speed and arrival event, so the
                            // game's movement fires full_cycle_back when the donkey gets there and the graph unloads.
                            MovementComponent.StartPathResult r = wgo.MovementComponent.StartPath(stand.Position, wgo.WorldId, stand.GameSceneDataId,
                                ___navigation.value, ___speed.value, "full_cycle_back", delegate { ___onFinish.Call(flow); }, null);
                            if (r == MovementComponent.StartPathResult.Started)
                            {
                                Plugin.Log.LogInfo($"Corpses: the donkey walks out to {stand.Id} {stand.Position:F1} and unloads there instead of walking to the morgue ({DescribeMorgue()}).");
                                return false;
                            }
                            Plugin.Log.LogWarning($"Corpses: the walk to {stand.Id} did not start ({r}); the donkey unloads where it appears.");
                        }
                        wgo.AddDelayedEvent("full_cycle_back", Mathf.Max(0.1f, UnloadDelay.Value));
                        Plugin.Log.LogInfo($"Corpses: the donkey unloads where it appears ({wgo.Position:F1}) in {UnloadDelay.Value:F1} s instead of walking to the morgue ({DescribeMorgue()}).");
                        return false;
                    }
                    case WalkAction.LeaveHere:
                        ___out.Call(flow);
                        UnloadWindowUntil = -1f;
                        wgo.AddDelayedEvent("Disable", Mathf.Max(0.1f, LeaveDelay.Value));
                        Plugin.Log.LogInfo($"Corpses: the donkey leaves from {wgo.Position:F1} in {LeaveDelay.Value:F1} s.");
                        return false;
                    default:
                        if (finishEvent == "Disable" && InUnloadWindow)
                        {
                            UnloadWindowUntil = -1f; // the unload is over; the walk back is the game's
                            Plugin.Log.LogInfo($"Corpses: the donkey walks back from {wgo.Position:F1} and vanishes there, as in the game.");
                        }
                        else if (hold != null)
                        {
                            // The game's walk to the morgue; the bodies still go to the drop point on arrival (the
                            // fallback prefix needs the window open for that).
                            UnloadWindowUntil = Time.time + 180f;
                            if (holdLogged != hold)
                            {
                                holdLogged = hold;
                                Plugin.Log.LogInfo($"Corpses: quest {hold} wants the donkey at the morgue for a talk; it walks there as in the unmodified game (the bodies still go to the crossroads).");
                            }
                        }
                        return true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Corpses: walk check failed; the donkey walks as in the unmodified game. " + e);
                return true;
            }
        }

        // UINotificator.ShowNewBodyNotification(string): the pooled "New Body" notification. The parameterless
        // overload forwards to this one. Replaced only for a body this left (same frame), never for the game's other
        // uses of the node (the nun's own body script during the donkey strike).
        private static bool NotificationPrefix(UINotificator __instance, string bodyId)
        {
            if (!Applied || !TakeRedirectedNotification())
            {
                return true;
            }
            string text = Notification.Value;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true; // the game's own "New Body"
            }
            try
            {
                // LLBase.L returns an unknown key verbatim, so the text itself serves as the key.
                __instance.ShowSimpleTextWithIconNotification(text, IconFor(bodyId));
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Corpses: our notification failed, showing the game's. " + e);
                return true;
            }
        }
    }
}
