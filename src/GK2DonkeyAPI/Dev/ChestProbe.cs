using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Step-0 probe for the chest on the cart (docs/portable-chest.md §6, Debug tab). Dev-only: it settles the facts
    // the decompile can't (the balance asset defeats the offline reader) and tries the three moves the design rests
    // on, one button each, on a chest the keeper faces (or the nearest one within 6 m):
    //   - the chest and building definitions, to a file;
    //   - hide, move 4 m along the keeper's facing, re-register in the chunk system, show (and a button to move it back);
    //   - zone remove and re-add at the chest's current position;
    //   - a stand-in view of the chest (the build-preview technique) made and destroyed, with the town quality logged;
    //   - the chest view's colliders, per layer;
    //   - which scenes keep the previous scene loaded through a door.
    // Everything acts on real saved objects, so: an empty test chest, and reload without saving afterwards.
    internal static class ChestProbe
    {
        private static WgoData moved;
        private static Vector3 movedFrom;
        private static Wgo standIn;
        private static WgoData standInData;

        public static string DumpDefinitions()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("GK2 Donkey API chest facts " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("[wgo] every world object with interaction Chest or an inventory:");
                int n = 0;
                foreach (WGODef d in GameBalance.Me.wgoDefs)
                {
                    if (d == null || (d.interactionType != WGODef.InteractionType.Chest && d.inventorySize <= 0))
                    {
                        continue;
                    }
                    n++;
                    sb.AppendLine($"  {d.id}: interaction={d.interactionType} inventorySize={d.inventorySize} openInMultiInventory={d.OpenInMultiInventory} movable={d.isMovable} townQuality={d.townQuality} attachedScript='{d.attachedScript}' npcGroup='{d.npcLifeSimGroup}' refToOtherInventory={d.hasRefToOtherWgoInventory}:'{d.refToOtherWgoInventory}' conveyor={d.conveyorType} navHole={d.forceSetNavigationHoleType} hp={d.hp} dropInventoryOnDeath={d.dropInventoryOnDeath} replaceOnDie='{(d.replaceToWgoOnDie != null && d.replaceToWgoOnDie.HasExpression ? d.replaceToWgoOnDie.ToString() : "")}' transferData={d.transferDataToNewWgo} startItems={d.startItems?.Count ?? 0} whitelist=[{Join(d.inventoryWhiteList?.itemsIds)}|{Join(d.inventoryWhiteList?.groupsIds)}] blacklist=[{Join(d.inventoryBlackList?.itemsIds)}|{Join(d.inventoryBlackList?.groupsIds)}] group='{d.wgoGroup}' customVisual='{d.customVisualId}' craftInventory={d.craftInventorySize}");
                    foreach (BuildingDef b in GameBalance.Me.buildingDefs)
                    {
                        if (b == null || (b.wgoId != d.id && !(b.id ?? "").StartsWith(d.id + "_")))
                        {
                            continue;
                        }
                        sb.AppendLine($"      build {b.id}: wgo={b.wgoId} mode={b.buildingMode} instantDestroy={b.deleteInstantly} startCraft='{b.startCraft}' tab='{b.tab}' needsUnlock={b.isNeedsUnlock} limitMax={b.limitMax} buildsIn=[{Join(b.buildsIn)}] needs=[{string.Join(", ", b.needItems.Select(x => x.id + " x" + x.count))}] out=[{string.Join(", ", b.outputItems.chanceOutputItems.Select(x => x.id + " x" + x.count + (x.chance != null && x.chance.HasExpression ? " @" + x.chance : "")))}]");
                    }
                }
                sb.AppendLine($"  total {n}");
                sb.AppendLine();
                sb.AppendLine("[scenes] previous scene kept loaded through a door (disableUnloadOnTeleport):");
                GameSceneManager gsm = LazySingleton<GameSceneManager>.Instance;
                foreach (GameSceneData sd in MainGame.WorldData.gameSceneDataList)
                {
                    sb.AppendLine($"  {sd.id}: {(gsm != null && gsm.DisabledUnloadOnTeleport(sd.id) ? "KEPT LOADED" : "unloaded")}; zones: {string.Join(", ", sd.worldZones.Select(z => z.id + (z.IsContainer ? "" : "(not a container)") + " p" + z.processingPriority))}");
                }
                string path = System.IO.Path.Combine(Paths.BepInExRootPath, "GK2-chest-facts.txt");
                File.WriteAllText(path, sb.ToString());
                return $"wrote {n} definitions to {path}";
            }
            catch (Exception e)
            {
                return "dump failed: " + e;
            }
        }

        // The chest the keeper faces, else the nearest chest within 6 m, in his scene.
        private static WgoData Target(out string why)
        {
            why = null;
            PlayerData pd = MainGame.PlayerData;
            PlayerController pc = MainGame.PlayerController;
            if (pd == null || pc == null)
            {
                why = "no game loaded";
                return null;
            }
            Wgo facing = pc.PlayerInteractionComponent?.WgoUnderInteraction;
            if (facing != null && facing.Data != null && facing.Data.Definition?.interactionType == WGODef.InteractionType.Chest)
            {
                return facing.Data;
            }
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(pd.currentGameSceneId);
            WgoData best = null;
            float bestD = 6f;
            foreach (WgoData d in sd?.wgoDataList ?? new List<WgoData>())
            {
                if (d?.Definition == null || d.Definition.interactionType != WGODef.InteractionType.Chest)
                {
                    continue;
                }
                float dist = Vector3.Distance(d.Position, pd.position.Value);
                if (dist < bestD)
                {
                    bestD = dist;
                    best = d;
                }
            }
            if (best == null)
            {
                why = "no chest faced or within 6 m";
            }
            return best;
        }

        private static string Where(WgoData d)
        {
            Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
            string zones = Zones(d);
            return $"{d.id} {d.UniqueId} at {d.Position:F2} hidden={d.IsHidden} interactable={d.IsInteractable} zone='{d.WorldZoneData?.id}' (in lists: {zones}) view={(v != null ? $"{(v.gameObject.activeSelf ? "active" : "inactive")} at {v.transform.position:F2} registered={v.RegisteredInChunker}" : "none")}";
        }

        private static string Zones(WgoData d)
        {
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
            var l = new List<string>();
            foreach (WorldZoneData z in sd?.worldZones ?? new List<WorldZoneData>())
            {
                int c = z.wgoDataList.Count(g => g == d.UniqueId);
                if (c > 0)
                {
                    l.Add(z.id + (c > 1 ? " x" + c : ""));
                }
            }
            return l.Count == 0 ? "none" : string.Join(", ", l);
        }

        // Hide, move 4 m along the keeper's facing, re-register in the chunk system, show. The order matters: the
        // chunk entry is found from the view's bounds, so it is unregistered at the old spot and registered at the new.
        public static string HideMoveShow()
        {
            try
            {
                WgoData d = Target(out string why);
                if (d == null)
                {
                    return why;
                }
                if (moved != null)
                {
                    return "a chest is already moved; move it back first";
                }
                var sb = new StringBuilder();
                sb.AppendLine("before: " + Where(d));
                Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
                PlayerData pd = MainGame.PlayerData;
                Vector3 to = d.Position + new Vector3(pd.Direction.x, 0f, pd.Direction.y).normalized * 4f;
                Unregister(v, sb);
                d.IsHidden = true;
                sb.AppendLine("hidden: " + Where(d));
                movedFrom = d.Position;
                d.Position = to;
                d.IsHidden = false;
                Register(v, sb);
                moved = d;
                sb.AppendLine("after: " + Where(d));
                sb.Append("Now walk away until the old spot is off screen and come back, look at the new spot from near and far, then press Move back.");
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "hide/move/show failed: " + e;
            }
        }

        public static string MoveBack()
        {
            try
            {
                if (moved == null)
                {
                    return "nothing moved";
                }
                var sb = new StringBuilder();
                Wgo v = GameScene.GetWgoViewGlobal(moved.UniqueId);
                Unregister(v, sb);
                moved.IsHidden = true;
                moved.Position = movedFrom;
                moved.IsHidden = false;
                Register(v, sb);
                sb.Append("back: " + Where(moved));
                moved = null;
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "move back failed: " + e;
            }
        }

        private static void Unregister(Wgo v, StringBuilder sb)
        {
            if (v == null)
            {
                sb.AppendLine("chunk: no view, nothing to unregister");
                return;
            }
            if (v.Data.Definition.isMovable)
            {
                sb.AppendLine("chunk: a movable definition (dynamic chunk object), left registered");
                return;
            }
            if (v.RegisteredInChunker)
            {
                LazySingleton<ChunkManager>.Instance.UnregisterStaticChunkableObject(v, ChunkManagerLayerType.StaticWgo);
                v.RegisteredInChunker = false;
                sb.AppendLine("chunk: unregistered at the old spot");
            }
            else
            {
                sb.AppendLine("chunk: was not registered");
            }
        }

        private static void Register(Wgo v, StringBuilder sb)
        {
            if (v == null || v.Data.Definition.isMovable || v.RegisteredInChunker)
            {
                return;
            }
            LazySingleton<ChunkManager>.Instance.RegisterStaticChunkableObject(v, ChunkManagerLayerType.StaticWgo);
            v.RegisteredInChunker = true;
            LazySingleton<ChunkManager>.Instance.RequestVisibilityRecheck(v);
            sb.AppendLine("chunk: registered at the new spot, visibility recheck requested");
        }

        // Out of every zone, then back in through the game's own order (zones by descending processing priority,
        // the first that accepts). Logged: the zone lists before, between and after, so a duplicate would show as x2.
        public static string ZoneRoundTrip()
        {
            try
            {
                WgoData d = Target(out string why);
                if (d == null)
                {
                    return why;
                }
                var sb = new StringBuilder();
                sb.AppendLine("before: " + Where(d));
                GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                foreach (WorldZoneData z in sd.worldZones)
                {
                    z.RemoveWgoData(d);
                }
                d.WorldZoneData = null;
                sb.AppendLine("removed: " + Where(d));
                string added = null;
                foreach (KeyValuePair<int, List<WorldZoneData>> kv in sd.WorldZonesByProcessingPriority.Reverse())
                {
                    foreach (WorldZoneData z in kv.Value)
                    {
                        if (z.TryAddWgoData(d))
                        {
                            added = z.id;
                            break;
                        }
                    }
                    if (added != null)
                    {
                        break;
                    }
                }
                sb.AppendLine($"re-added to '{added ?? "NO ZONE ACCEPTED"}': " + Where(d));
                sb.Append("Check: a workbench in this area still lists the chest's items; villagers path around the chest.");
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "zone round trip failed: " + e;
            }
        }

        // A stand-in view of the faced chest at the keeper's drop position, made as the build preview is (a temp
        // object, not in the scene, no chunk registration, colliders off). The previous stand-in is destroyed first,
        // so pressing ten times is the spawn-and-destroy test; "Remove" destroys the last one.
        public static string StandIn()
        {
            try
            {
                WgoData d = Target(out string why);
                if (d == null)
                {
                    return why;
                }
                if (!Mount.Instance.IsInGame(out PlayerController pc, out GameScene scene))
                {
                    return "no scene";
                }
                var sb = new StringBuilder();
                int q0 = MainGame.Instance.GameSave.townSystem.Quality;
                sb.AppendLine(RemoveStandIn());
                PlayerData pd = pc.PlayerData;
                if (!SpecialPhysicsCastUtils.GetPlayerDropPosition(pd.position.Value, pd.Direction, out Vector3 pos))
                {
                    pos = pd.position.Value + new Vector3(pd.Direction.x, 0f, pd.Direction.y);
                }
                Mount.Instance.SuppressScripts = true;
                try
                {
                    standInData = new WgoData(d.id, pos, pd.currentGameSceneId) { isTempObject = true, IsInteractable = false };
                    standInData.MainWgoPartData.variationId = d.MainWgoPartData.variationId;
                    standInData.MainWgoPartData.rotationIndex = d.MainWgoPartData.rotationIndex;
                }
                finally
                {
                    Mount.Instance.SuppressScripts = false;
                }
                standIn = Wgo.Spawn(standInData, scene.transform, registerInChunkManagerIfStatic: true, ignoreChunkRegistration: true, applyDefaultWgoPartState: true);
                standIn.UpdateChunkVisibility(isVisible: true);
                int cols = 0;
                foreach (Collider c in standIn.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    c.gameObject.SetActive(false);
                    cols++;
                }
                int q1 = MainGame.Instance.GameSave.townSystem.Quality;
                sb.Append($"stand-in {d.id} (variation '{standInData.MainWgoPartData.variationId}') at {pos:F2}, {cols} collider object(s) off, town quality {q0} -> {q1}{(q0 != q1 ? " CHANGED" : "")}; in scene list: {(MainGame.WorldData.GetGameSceneDataById(pd.currentGameSceneId).wgoDataList.Contains(standInData) ? "YES (wrong)" : "no")}");
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "stand-in failed: " + e;
            }
        }

        public static string RemoveStandIn()
        {
            if (standIn == null || !standIn)
            {
                standIn = null;
                standInData = null;
                return "no stand-in";
            }
            int q0 = MainGame.Instance.GameSave.townSystem.Quality;
            UnityEngine.Object.Destroy(standIn.gameObject);
            standIn = null;
            standInData = null;
            return $"stand-in destroyed (town quality {q0})";
        }

        public static string DumpColliders()
        {
            try
            {
                WgoData d = Target(out string why);
                if (d == null)
                {
                    return why;
                }
                Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
                if (v == null)
                {
                    return "no view for " + d.id;
                }
                var sb = new StringBuilder();
                sb.AppendLine($"colliders of {d.id} (interactable colliders: {v.MainWgoPart?.InteractableColliders?.Count ?? 0}):");
                foreach (Collider c in v.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    sb.AppendLine($"  {PathOf(c.transform, v.transform)}: {c.GetType().Name} layer {c.gameObject.layer} '{LayerMask.LayerToName(c.gameObject.layer)}' enabled={c.enabled} trigger={c.isTrigger} active={c.gameObject.activeInHierarchy} interactable={(v.MainWgoPart?.InteractableColliders?.Contains(c) ?? false)}");
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "collider dump failed: " + e;
            }
        }

        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }

        private static string Join(IEnumerable<string> l) => l == null ? "" : string.Join(", ", l);
    }
}
