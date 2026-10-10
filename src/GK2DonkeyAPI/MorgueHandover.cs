using System;
using HarmonyLib;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A corpse into the morgue (docs/api.md "A corpse into the morgue", 0.27.0).
    //
    // Driving the cart through the morgue door with a corpse aboard, the ride ends at the door as it always does (a
    // house door is the game's teleport within the scene, followed by its indoor light preset: DismountIndoors; a door
    // that loads another scene: "scene changed"), and the cart is parked where it stood outside, with its load. This
    // takes the corpse off that parked cart and into the keeper's hands, so he arrives inside carrying it. Only at the
    // morgue: it is the one building a body is worked in (its pallets, the autopsy table, the crematorium); in a house
    // or the church a body in hand is of no use, and no other cargo comes inside either.
    //
    // Knowing it was the morgue door, two ways, either one enough (CorpseIntoMorguePlaces lists both kinds):
    //   - the building area the keeper arrives in, as the game itself tracks it (PlayerData.CurrentWorldZoneData, set
    //     by PlayerPhysicalBody.UpdateWorldZone from a raycast every physics step; SetPosition clears the old area at
    //     once, so at the dismount it is null or not yet the new one: this waits for it);
    //   - the door's destination (the teleport point's id from the PlayerController.Teleport call, known the moment the
    //     ride ends, while the screen is black). The game's doors are expressions in GameBalance (build 25752683):
    //     into the morgue TeleportTo("tp_RT_morgue_exit", "dungeons", "door") and TeleportTo("tp_morgue_to_graveyard",
    //     "dungeons", "door"); the point is named for where it stands, so "_exit" is the inside of the entrance.
    // The decision waits at most a moment after control is back (the area is read by then). Not the morgue: nothing
    // happens and the cart keeps its load.
    //
    // Only bodies (body_corpse), as many as his hands take (one; more with the game's extra_overhead); zombies and
    // everything else stay on the cart. Nothing is dropped: a body the hands refuse goes back onto the cart. The old
    // summon (the ephemeral mount) is not touched: it hands its whole load back at every door anyway.
    internal static partial class Mount
    {
        internal static class MorgueHandover
        {
            // The last teleport while the keeper rode: what the door passed, for the decision and the log.
            internal static string LastDoorId, LastDoorPreset, LastDoorScene;
            internal static float LastDoorAt = -1f;

            private static Instance armed;
            private static float armedAt;
            private static float controlBackAt = -1f;

            internal static bool Armed => armed != null;

            // PlayerController.Teleport, before anything has faded or moved (PlayerController_Teleport_DoorNote_Patch).
            internal static void NoteTeleport(TeleportDataBase td)
            {
                if (td == null)
                {
                    return;
                }
                LastDoorId = null;
                LastDoorPreset = td.environmentPreset;
                LastDoorScene = null;
                LastDoorAt = Time.unscaledTime;
                try
                {
                    LastDoorId = td.GetDestinationId();
                    LastDoorScene = td.GetDestinationSceneData()?.id;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogInfo("Mount: the door's destination could not be read: " + e.Message);
                }
                Instance r = Ridden;
                string load = r != null && r.HasCart ? $"{r.CargoRig.Count} item(s) on the cart" : "no cart";
                Plugin.Log.LogInfo($"Mount: a door while riding: to {LastDoorId ?? "?"} in {LastDoorScene ?? "?"} (preset {LastDoorPreset ?? "none"}); {load}.");
            }

            // A door's teleport counts for this long: "scene changed" comes after a loading screen, "indoors" a frame
            // after the teleport. Beyond it the ride ended for another reason that reads like a door (a script's indoor
            // preset), and nothing is handed over.
            private const float DoorWindow = 60f;

            // The ride ended at a door (Instance.Dismount, reasons "indoors" and "scene changed"), before Park.
            internal static void Arm(Instance i, string reason)
            {
                armed = null;
                if (i == null || i.Ephemeral || !i.HasCart || !Plugin.CorpseIntoMorgue.Value || !HasCorpse(i))
                {
                    return;
                }
                if (LastDoorAt < 0f || Time.unscaledTime - LastDoorAt > DoorWindow)
                {
                    Plugin.Log.LogInfo($"Mount: {i.Describe()} stays with its corpse: the ride ended ({reason}) without a door's teleport.");
                    return;
                }
                armed = i;
                armedAt = Time.unscaledTime;
                controlBackAt = -1f;
                Plugin.Log.LogInfo($"Mount: {i.Describe()} stays at the door ({reason}) with a corpse aboard; it goes into the keeper's hands if this is the morgue.");
            }

            // Every frame (MountRegistry.Tick) while armed.
            internal static void Tick(PlayerController pc)
            {
                if (armed == null)
                {
                    return;
                }
                PlayerData pd = pc?.PlayerData;
                if (pd == null)
                {
                    return;
                }
                Instance i = armed;
                if (Ridden != null)
                {
                    Disarm("the keeper rides again");
                    return;
                }
                if (!Plugin.CorpseIntoMorgue.Value)
                {
                    Disarm("switched off");
                    return;
                }
                if (!HasCorpse(i))
                {
                    Disarm("no corpse on the cart any more");
                    return;
                }
                string zone = pd.CurrentWorldZoneData?.Definition?.id;
                string door = LastDoorId; // the door Arm was gated on
                if (Decide(zone, door, Plugin.CorpseIntoMorguePlaces.Value, out string why))
                {
                    armed = null;
                    HandOver(i, pd, why);
                    return;
                }
                if (pc.IsControlsEnabled)
                {
                    // The area is read every physics step; a moment after control is back it is the new one.
                    if (controlBackAt < 0f)
                    {
                        controlBackAt = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - controlBackAt > 0.25f)
                    {
                        Disarm($"not the morgue (area {zone ?? "none"}, door to {door ?? "unknown"})");
                        return;
                    }
                }
                if (Time.unscaledTime - armedAt > 20f)
                {
                    Disarm("the door took too long");
                }
            }

            private static void Disarm(string why)
            {
                Plugin.Log.LogInfo($"Mount: the corpse stays on {armed?.Describe()}: {why}.");
                armed = null;
            }

            internal static bool HasCorpse(Instance i)
            {
                foreach (Item it in i.CargoRig.ItemsNow())
                {
                    if (it.id == Cargo.CorpseId)
                    {
                        return true;
                    }
                }
                return false;
            }

            // The rule alone (self-tested): the area the keeper is in, or the door's destination, is one of the places
            // (comma-separated, case and spaces don't matter). An empty list never matches.
            internal static bool Decide(string zoneId, string doorId, string places, out string why)
            {
                why = null;
                if (string.IsNullOrEmpty(places))
                {
                    return false;
                }
                foreach (string raw in places.Split(','))
                {
                    string p = raw.Trim();
                    if (p.Length == 0)
                    {
                        continue;
                    }
                    if (zoneId != null && string.Equals(p, zoneId, StringComparison.OrdinalIgnoreCase))
                    {
                        why = "the " + zoneId + " area";
                        return true;
                    }
                    if (doorId != null && string.Equals(p, doorId, StringComparison.OrdinalIgnoreCase))
                    {
                        why = "the door to " + doorId;
                        return true;
                    }
                }
                return false;
            }

            // The corpses off the cart into the hands (Cargo.Rig.HandCorpsesTo). Returns how many went.
            internal static int HandOver(Instance i, PlayerData pd, string why)
            {
                if (i == null || pd == null || !Plugin.CorpseIntoMorgue.Value)
                {
                    return 0;
                }
                int n = i.CargoRig.HandCorpsesTo(pd, out string stop);
                if (n > 0)
                {
                    Plugin.Log.LogInfo($"Mount: {n} corpse(s) taken off {i.Describe()} into the keeper's hands at {why}; {i.CargoRig.Count} item(s) stay on the cart{(stop != null ? " (" + stop + ")" : "")}.");
                }
                else
                {
                    Plugin.Log.LogInfo($"Mount: no corpse came off {i.Describe()} at {why}: {stop ?? "none aboard"}.");
                }
                return n;
            }
        }
    }

    // Every teleport while the keeper rides: what the door passed (MorgueHandover.NoteTeleport). It never refuses: the
    // game's doors take control before calling this, and a refusal would leave the keeper frozen (see CartChest's).
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Teleport))]
    internal static class PlayerController_Teleport_DoorNote_Patch
    {
        private static void Prefix(TeleportDataBase teleportData)
        {
            try
            {
                if (Mount.IsMounted)
                {
                    Mount.MorgueHandover.NoteTeleport(teleportData);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Mount: noting the door failed: " + e.Message);
            }
        }
    }
}
