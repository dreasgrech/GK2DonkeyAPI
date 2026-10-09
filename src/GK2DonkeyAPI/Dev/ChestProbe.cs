using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Step-0 probe for the chest on the cart (docs/portable-chest.md §6, Debug tab). Dev-only: it settles the facts
    // the decompile can't (the balance asset defeats the offline reader) and tries the three moves the design rests
    // on, by hand on a chest the keeper faces (or the nearest one within 6 m), or scripted by the self-tests
    // (SelfTest.Chest.cs), which make their own test chest:
    //   - the chest and building definitions, to a file;
    //   - hide, move 4 m along the keeper's facing, re-register in the chunk system, show (and a button to move it back);
    //   - zone remove and re-add at the chest's current position;
    //   - a stand-in view of the chest (the build-preview technique) made and destroyed, with the town quality logged;
    //   - the chest view's colliders, per layer;
    //   - which scenes keep the previous scene loaded through a door.
    // The buttons act on real saved objects, so: an empty test chest, and reload without saving afterwards.
    internal static class ChestProbe
    {
        private static WgoData moved;
        private static Vector3 movedFrom;
        private static Wgo standIn;
        private static WgoData standInData;

        // ------------------------------------------------------------------ the pieces (also used by the self-tests)

        // The moves themselves live in ChestMoves (CartChest.cs); these keep the probe's log around them.
        internal static void Relocate(WgoData d, Wgo v, Vector3 to, StringBuilder sb)
        {
            ChestMoves.Hide(d, v);
            sb?.AppendLine("hidden: " + Where(d));
            ChestMoves.Show(d, v, to);
            sb?.AppendLine("shown at the new spot" + (v != null && v.RegisteredInChunker ? ", registered in the chunk system" : ""));
        }

        internal static void ZoneRemoveAll(GameSceneData sd, WgoData d) => ChestMoves.ZoneRemoveAll(sd, d);

        internal static WorldZoneData ZoneAddByPriority(GameSceneData sd, WgoData d) => ChestMoves.ZoneAddByPriority(sd, d);

        internal static bool HasNavCut(WgoData d) => ChestMoves.HasNavCut(d);

        internal static Wgo SpawnStandIn(WgoData src, Vector3 pos, string sceneId, Transform parent, out WgoData temp, out int collidersOff)
            => ChestMoves.SpawnStandIn(src, pos, sceneId, parent, out temp, out collidersOff);

        // The chunk that holds this view, for the self-test's check that a moved view sits in a chunk around its new
        // spot: centre and size of that chunk's bounds, or false when no chunk holds it. Reads the chunk manager's
        // private layer list; BurstableBounds is Unity.Mathematics, read by reflection so the project needs no reference.
        internal static bool ChunkOf(Wgo v, out Vector3 center, out Vector3 size, out int chunks)
        {
            center = size = Vector3.zero;
            chunks = 0;
            ChunkManager cm = LazySingleton<ChunkManager>.Instance;
            if (cm == null || v == null)
            {
                return false;
            }
            var layers = AccessTools.Field(typeof(ChunkManager), "layers").GetValue(cm) as System.Collections.IEnumerable;
            bool found = false;
            foreach (object layer in layers ?? Array.Empty<object>())
            {
                var arr = AccessTools.Field(layer.GetType(), "chunks").GetValue(layer) as Chunk[];
                int count = (int)AccessTools.Property(layer.GetType(), "Count").GetValue(layer);
                for (int i = 0; i < count && arr != null && i < arr.Length; i++)
                {
                    Chunk c = arr[i];
                    if (c != null && c.ContainsChunkableObject(v))
                    {
                        chunks++;
                        if (!found)
                        {
                            object b = c.GetChunkableData();
                            center = Float3(AccessTools.Field(b.GetType(), "center").GetValue(b));
                            size = Float3(AccessTools.Field(b.GetType(), "size").GetValue(b));
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        // True when one of the chunks holding this view has bounds around 'point' (x and z): an object on a chunk
        // boundary is held by two chunks (ChunkManager.SplitChunkableObjectBetweenChunks), and either may be the one.
        internal static bool ChunkHolds(Wgo v, Vector3 point, out int chunks, out string where)
        {
            chunks = 0;
            where = "";
            ChunkManager cm = LazySingleton<ChunkManager>.Instance;
            if (cm == null || v == null)
            {
                return false;
            }
            var layers = AccessTools.Field(typeof(ChunkManager), "layers").GetValue(cm) as System.Collections.IEnumerable;
            bool holds = false;
            foreach (object layer in layers ?? Array.Empty<object>())
            {
                var arr = AccessTools.Field(layer.GetType(), "chunks").GetValue(layer) as Chunk[];
                int count = (int)AccessTools.Property(layer.GetType(), "Count").GetValue(layer);
                for (int i = 0; i < count && arr != null && i < arr.Length; i++)
                {
                    Chunk c = arr[i];
                    if (c == null || !c.ContainsChunkableObject(v))
                    {
                        continue;
                    }
                    chunks++;
                    object b = c.GetChunkableData();
                    Vector3 center = Float3(AccessTools.Field(b.GetType(), "center").GetValue(b));
                    Vector3 size = Float3(AccessTools.Field(b.GetType(), "size").GetValue(b));
                    where += $"{(where.Length > 0 ? "; " : "")}centre {center:F1} size {size:F1}";
                    if (Mathf.Abs(point.x - center.x) <= size.x * 0.5f + 0.01f && Mathf.Abs(point.z - center.z) <= size.z * 0.5f + 0.01f)
                    {
                        holds = true;
                    }
                }
            }
            return holds;
        }

        internal static Vector3 ViewBoundsCenter(Wgo v)
        {
            object b = v.GetChunkableData();
            return Float3(AccessTools.Field(b.GetType(), "center").GetValue(b));
        }

        private static Vector3 Float3(object f)
        {
            Type t = f.GetType();
            return new Vector3((float)t.GetField("x").GetValue(f), (float)t.GetField("y").GetValue(f), (float)t.GetField("z").GetValue(f));
        }

        // The part pool's bookkeeping for a chest's main part: how many are idle in the pool and how many it counts
        // as borrowed (1.009: NoteBorrowed/NoteReturned).
        internal static void PoolCounts(WgoData d, out string key, out int idle, out int borrowed)
        {
            string asset = d.Definition != null ? d.Definition.ResolveAssetId(d.id, d) : d.id;
            key = WgoPartPool.GetAddressableKey(asset);
            idle = borrowed = 0;
            WgoPartPool pool = LazySingleton<WgoPartPool>.Instance;
            if (pool == null)
            {
                return;
            }
            var pools = AccessTools.Field(typeof(WgoPartPool), "pools").GetValue(pool) as System.Collections.IDictionary;
            if (pools != null && pools.Contains(key))
            {
                idle = (int)AccessTools.Property(pools[key].GetType(), "Count").GetValue(pools[key]);
            }
            var b = AccessTools.Field(typeof(WgoPartPool), "borrowedCounts").GetValue(pool) as System.Collections.IDictionary;
            if (b != null && b.Contains(key))
            {
                borrowed = (int)b[key];
            }
        }

        internal static string Where(WgoData d)
        {
            Wgo v = GameScene.GetWgoViewGlobal(d.UniqueId);
            return $"{d.id} {d.UniqueId} at {d.Position:F2} hidden={d.IsHidden} interactable={d.IsInteractable} zone='{d.WorldZoneData?.id}' (in lists: {Zones(d)}) view={(v != null ? $"{(v.gameObject.activeSelf ? "active" : "inactive")} at {v.transform.position:F2} parts={(v.MainWgoPart != null ? "loaded" : "none")} registered={v.RegisteredInChunker}" : "none")}";
        }

        // The zones whose saved lists hold the object, "x2" marking a duplicate entry.
        internal static string Zones(WgoData d)
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

        internal static int ZoneEntries(WgoData d)
        {
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
            int n = 0;
            foreach (WorldZoneData z in sd?.worldZones ?? new List<WorldZoneData>())
            {
                n += z.wgoDataList.Count(g => g == d.UniqueId);
            }
            return n;
        }

        // ------------------------------------------------------------------ the buttons

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
                PlayerData pd = MainGame.PlayerData;
                Vector3 to = d.Position + new Vector3(pd.Direction.x, 0f, pd.Direction.y).normalized * 4f;
                movedFrom = d.Position;
                Relocate(d, GameScene.GetWgoViewGlobal(d.UniqueId), to, sb);
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
                Relocate(moved, GameScene.GetWgoViewGlobal(moved.UniqueId), movedFrom, sb);
                sb.Append("back: " + Where(moved));
                moved = null;
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "move back failed: " + e;
            }
        }

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
                sb.AppendLine("before: " + Where(d) + $" navCut={HasNavCut(d)}");
                GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(d.WorldId);
                ZoneRemoveAll(sd, d);
                sb.AppendLine("removed: " + Where(d) + $" navCut={HasNavCut(d)}");
                WorldZoneData added = ZoneAddByPriority(sd, d);
                sb.AppendLine($"re-added to '{added?.id ?? "NO ZONE ACCEPTED"}': " + Where(d) + $" navCut={HasNavCut(d)}");
                sb.Append("Check: a workbench in this area still lists the chest's items; villagers path around the chest.");
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "zone round trip failed: " + e;
            }
        }

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
                standIn = SpawnStandIn(d, pos, pd.currentGameSceneId, scene.transform, out standInData, out int cols);
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
                return Colliders(d);
            }
            catch (Exception e)
            {
                return "collider dump failed: " + e;
            }
        }

        internal static string Colliders(WgoData d)
        {
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
            return sb.ToString().TrimEnd();
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
