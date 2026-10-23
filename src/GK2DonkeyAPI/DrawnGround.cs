using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The drawn ground (docs/flying.md, "a drawn-ground map"): where the game draws ground, at which height, also where
    // no collider lies under it (a rock table, a ledge, decoration). The game's ground is a stack of flat levels, one
    // LazyTerrain per level (a plane at its own height), each cut into chunks of 5 x 5 tiles (0.96 x 1.2) that share
    // baked meshes; a tile is drawn when its quad carries data in the mesh's uv channels 2 and 3 (31 442 of the 104 121
    // tiles of the game's 4 209 meshes carry none). The game frees a mesh's readable copy the first time it shows it
    // (LazyTerrainMesh.DrawFromData -> LazyTerrainMeshCollection.Runtime_StripCpuMeshData), so the tiles are read just
    // before (the prefix below), or on demand while still readable. Then the highest drawn level at x, z is a lookup.
    //
    // Step 1 (2026-10-22): the map and its checks only (the Flying tab); the flight doesn't use it yet. Only where the
    // dev tools are unlocked: for a player the prefix returns at once and nothing is kept.
    internal static class DrawnGround
    {
        // a mesh's drawn tiles: a bit per tile of its grid (columns along x, rows along z, from its bounds' corner)
        private struct Tiles
        {
            internal int Cols, Rows;
            internal float MinX, MinZ, TileX, TileZ;
            internal ulong Mask;
            internal int Drawn;
        }

        private static readonly Dictionary<int, Tiles> tiles = new Dictionary<int, Tiles>(); // by Mesh instance id
        private static readonly List<Vector4> uvA = new List<Vector4>(160), uvB = new List<Vector4>(160);
        private static readonly List<Vector3> verts = new List<Vector3>(160);
        internal static int Captured, CapturedAtStrip, Failed;

        // the per-scene lookup: every chunk of every active terrain, in buckets of 6 x 6 world units
        private sealed class Chunk
        {
            internal LazyTerrain Terrain;
            internal LazyTerrainMeshData Data;
            internal Vector3 Origin; // the terrain's position + the chunk's local position: the mesh's (0, 0, 0)
            internal float Y;
        }

        private const float Bucket = 6f;
        private static readonly Dictionary<long, List<Chunk>> buckets = new Dictionary<long, List<Chunk>>();
        private static int indexedTerrains = -1, indexedChunks;
        internal static string IndexNote = "";

        // ------------------------------------------------------------------ reading the tiles

        // The game is about to free the mesh's readable copy: read its tiles first.
        internal static void BeforeStrip(Mesh mesh)
        {
            if (!DevTools.Unlocked || mesh == null || !mesh.isReadable) return;
            if (Read(mesh)) CapturedAtStrip++;
        }

        private static bool Read(Mesh mesh)
        {
            int id = mesh.GetInstanceID();
            if (tiles.ContainsKey(id)) return true;
            try
            {
                mesh.GetVertices(verts);
                mesh.GetUVs(2, uvA);
                mesh.GetUVs(3, uvB);
                Bounds b = mesh.bounds;
                var t = new Tiles { TileX = 0.96f, TileZ = 1.2f, MinX = b.min.x, MinZ = b.min.z };
                t.Cols = Mathf.Max(1, Mathf.RoundToInt(b.size.x / t.TileX));
                t.Rows = Mathf.Max(1, Mathf.RoundToInt(b.size.z / t.TileZ));
                if (t.Cols * t.Rows > 64) { Failed++; return false; } // not a terrain chunk the mod knows
                for (int q = 0; q + 5 < verts.Count; q += 6)
                {
                    bool drawn = false;
                    for (int k = q; k < q + 6 && !drawn; k++)
                    {
                        if (k < uvA.Count && uvA[k] != Vector4.zero) drawn = true;
                        if (k < uvB.Count && uvB[k] != Vector4.zero) drawn = true;
                    }
                    if (!drawn) continue;
                    Vector3 c = (verts[q] + verts[q + 1] + verts[q + 2] + verts[q + 3] + verts[q + 4] + verts[q + 5]) / 6f;
                    int col = Mathf.Clamp(Mathf.FloorToInt((c.x - t.MinX) / t.TileX), 0, t.Cols - 1);
                    int row = Mathf.Clamp(Mathf.FloorToInt((c.z - t.MinZ) / t.TileZ), 0, t.Rows - 1);
                    ulong bit = 1UL << (row * t.Cols + col);
                    if ((t.Mask & bit) == 0) { t.Mask |= bit; t.Drawn++; }
                }
                tiles[id] = t;
                Captured++;
                return true;
            }
            catch (System.Exception e)
            {
                Failed++;
                if (Failed <= 3) Plugin.Log.LogWarning($"[drawn-ground] a terrain mesh's tiles could not be read: {e.Message}");
                return false;
            }
        }

        // The drawn tiles of a mesh already read, as rectangles in the mesh's own space (min x, min z, size x, size z):
        // the map survey draws the freed chunks with them.
        internal static bool DrawnTiles(Mesh m, List<Rect> into)
        {
            into.Clear();
            if (m == null || !tiles.TryGetValue(m.GetInstanceID(), out Tiles t)) return false;
            for (int row = 0; row < t.Rows; row++)
                for (int col = 0; col < t.Cols; col++)
                    if ((t.Mask & (1UL << (row * t.Cols + col))) != 0)
                        into.Add(new Rect(t.MinX + col * t.TileX, t.MinZ + row * t.TileZ, t.TileX, t.TileZ));
            return true;
        }

        // ------------------------------------------------------------------ the lookup

        // The index kept as the terrains come and go: each LazyTerrain adds its chunks when it wakes (the patch on its
        // Awake, during the scene's loading) and takes them out when it is destroyed. It used to be built on the first
        // lookup and checked every 3 s by finding every LazyTerrain in the scene: the build was 12.6 ms on the first
        // take-off of a session and the scene search alone 8-10 ms every 3 s in flight (the [perf] lines, 2026-10-23:
        // "FlyFree 17.1, DrawnIndex 12.6", "DrawnIndex worst 8.8"). A scene's terrains are placed once, so what they
        // cover doesn't change while they live. A lookup with nothing indexed yet in this scene (the mod loaded after
        // its terrains woke) searches once, as before.
        private static readonly Dictionary<int, List<(long Key, Chunk Chunk)>> byTerrain = new Dictionary<int, List<(long, Chunk)>>();
        private static int oddTerrains;
        private static double indexMs;
        private static bool searched, noted;

        internal static void TerrainAwake(LazyTerrain t)
        {
            if (!DevTools.Unlocked || t == null || t.meshesData == null) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Add(t);
            indexMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        internal static void TerrainGone(LazyTerrain t)
        {
            if (t == null || !byTerrain.TryGetValue(t.GetInstanceID(), out var mine)) return;
            foreach (var (key, ch) in mine)
            {
                if (buckets.TryGetValue(key, out List<Chunk> list))
                {
                    list.Remove(ch);
                    if (list.Count == 0) buckets.Remove(key);
                }
            }
            byTerrain.Remove(t.GetInstanceID());
            indexedTerrains = byTerrain.Count;
            indexedChunks -= t.meshesData?.Count ?? 0;
            if (byTerrain.Count == 0) { searched = false; noted = false; indexMs = 0; oddTerrains = 0; indexedChunks = 0; } // a scene left: the next one indexes afresh
        }

        private static void Add(LazyTerrain t)
        {
            int id = t.GetInstanceID();
            if (byTerrain.ContainsKey(id)) return;
            var mine = new List<(long, Chunk)>(t.meshesData.Count * 2);
            Transform tt = t.transform;
            if (Quaternion.Angle(tt.rotation, Quaternion.identity) > 0.1f || (tt.lossyScale - Vector3.one).sqrMagnitude > 1e-4f) oddTerrains++; // read as unrotated, unscaled
            foreach (LazyTerrainMeshData d in t.meshesData)
            {
                if (d == null || d.mesh == null) continue;
                var ch = new Chunk { Terrain = t, Data = d, Origin = tt.position + d.localPosition };
                ch.Y = ch.Origin.y;
                Bounds mb = d.mesh.bounds;
                int x0 = Mathf.FloorToInt((ch.Origin.x + mb.min.x) / Bucket), x1 = Mathf.FloorToInt((ch.Origin.x + mb.max.x) / Bucket);
                int z0 = Mathf.FloorToInt((ch.Origin.z + mb.min.z) / Bucket), z1 = Mathf.FloorToInt((ch.Origin.z + mb.max.z) / Bucket);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        long key = ((long)x << 32) ^ (uint)z;
                        if (!buckets.TryGetValue(key, out List<Chunk> list)) buckets[key] = list = new List<Chunk>(8);
                        list.Add(ch);
                        mine.Add((key, ch));
                    }
            }
            byTerrain[id] = mine;
            indexedTerrains = byTerrain.Count;
            indexedChunks += t.meshesData.Count;
            noted = false;
        }

        private static void Index()
        {
            if (byTerrain.Count == 0 && !searched)
            {
                // nothing woke under the patch in this scene: the terrains were there before the mod (search once)
                searched = true;
                using var perf = Perf.Measure(Perf.S.DrawnIndex);
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                foreach (LazyTerrain t in Object.FindObjectsByType<LazyTerrain>(FindObjectsSortMode.None))
                    if (t != null && t.meshesData != null && t.gameObject.activeInHierarchy) Add(t);
                indexMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
            if (noted) return;
            noted = true;
            IndexNote = $"{indexedTerrains} terrains, {indexedChunks} chunks indexed{(oddTerrains > 0 ? $", {oddTerrains} rotated or scaled (read as if not)" : "")}";
            Plugin.Log.LogInfo($"[drawn-ground] {IndexNote} as they loaded ({indexMs:0.0} ms in all){(searched ? ", found by a search" : "")}; {Captured} meshes' tiles read so far ({CapturedAtStrip} as the game freed them)");
        }

        internal enum Tile { None, Drawn, Unknown }

        // Every level's tile at x, z: what each chunk covering the point says (drawn, not drawn, or unknown: its mesh
        // was freed before the mod read it). For the checks and the readout.
        internal static void LevelsAt(float x, float z, List<(float Y, Tile Tile, string Terrain)> into)
        {
            into.Clear();
            if (!DevTools.Unlocked) return;
            Index();
            long key = ((long)Mathf.FloorToInt(x / Bucket) << 32) ^ (uint)Mathf.FloorToInt(z / Bucket);
            if (!buckets.TryGetValue(key, out List<Chunk> list)) return;
            foreach (Chunk ch in list)
            {
                if (ch.Terrain == null || !ch.Terrain.gameObject.activeInHierarchy) continue;
                Tile t = TileAt(ch, x, z, out bool inside);
                if (inside) into.Add((ch.Y, t, ch.Terrain.name));
            }
            into.Sort((a, b) => b.Y.CompareTo(a.Y));
        }

        // The highest drawn level at x, z at or below 'below' (NaN: none known). 'unknown' counts the chunks there whose
        // tiles weren't read.
        internal static float HighestAt(float x, float z, float below, out int unknown)
        {
            unknown = 0;
            if (!DevTools.Unlocked) return float.NaN;
            Index();
            long key = ((long)Mathf.FloorToInt(x / Bucket) << 32) ^ (uint)Mathf.FloorToInt(z / Bucket);
            if (!buckets.TryGetValue(key, out List<Chunk> list)) return float.NaN;
            float best = float.NaN;
            foreach (Chunk ch in list)
            {
                if (ch.Terrain == null || ch.Y > below || (!float.IsNaN(best) && ch.Y <= best) || !ch.Terrain.gameObject.activeInHierarchy) continue;
                Tile t = TileAt(ch, x, z, out bool inside);
                if (!inside) continue;
                if (t == Tile.Unknown) unknown++;
                else if (t == Tile.Drawn) best = ch.Y;
            }
            return best;
        }

        private static Tile TileAt(Chunk ch, float x, float z, out bool inside)
        {
            Mesh m = ch.Data.mesh;
            inside = false;
            if (m == null) return Tile.Unknown;
            int id = m.GetInstanceID();
            if (!tiles.TryGetValue(id, out Tiles t))
            {
                if (!m.isReadable || !Read(m))
                {
                    Bounds mb = m.bounds;
                    float lx = x - ch.Origin.x, lz = z - ch.Origin.z;
                    inside = lx >= mb.min.x && lx <= mb.max.x && lz >= mb.min.z && lz <= mb.max.z;
                    return Tile.Unknown;
                }
                t = tiles[id];
            }
            float ux = x - ch.Origin.x - t.MinX, uz = z - ch.Origin.z - t.MinZ;
            int col = Mathf.FloorToInt(ux / t.TileX), row = Mathf.FloorToInt(uz / t.TileZ);
            if (col < 0 || row < 0 || col >= t.Cols || row >= t.Rows) return Tile.None;
            inside = true;
            return (t.Mask & (1UL << (row * t.Cols + col))) != 0 ? Tile.Drawn : Tile.None;
        }

        // ------------------------------------------------------------------ the checks (the Flying tab)

        internal static string CheckResult = "";
        private static readonly List<(float Y, Tile Tile, string Terrain)> levels = new List<(float, Tile, string)>();

        // Against the walkable ground, where both exist: rays straight down on a grid around the keeper (radius 'r',
        // every 'step'); where one meets a ground collider (layers 11 and 12, not water) at y, the drawn ground at x, z
        // under y + 0.3 must be at y (within 0.3: a ramp or a stair between two levels may differ). Counted: agree,
        // disagree (the drawn level and the collider's), unknown (the chunk's tiles weren't read), nothing drawn.
        internal static void Check(float r = 30f, float step = 0.75f)
        {
            Rigidbody rb = MainGame.Instance != null ? MainGame.PlayerController?.PhysicalBody?.Rb : null;
            if (rb == null) { CheckResult = "not in a game"; return; }
            Index();
            Vector3 at = rb.position;
            int agree = 0, disagree = 0, unknown = 0, none = 0, rays = 0;
            var worst = new List<string>();
            for (float x = at.x - r; x <= at.x + r; x += step)
                for (float z = at.z - r; z <= at.z + r; z += step)
                {
                    if (!Physics.Raycast(new Vector3(x, at.y + 40f, z), Vector3.down, out RaycastHit hit, 80f, 6144, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<WaterComponentTag>() != null) continue;
                    rays++;
                    float y = HighestAt(x, z, hit.point.y + 0.3f, out int unk);
                    if (float.IsNaN(y)) { if (unk > 0) unknown++; else none++; continue; }
                    if (Mathf.Abs(y - hit.point.y) <= 0.3f) agree++;
                    else
                    {
                        disagree++;
                        if (worst.Count < 8) worst.Add($"({x:0.0},{z:0.0}) drawn {y:0.00}, collider {hit.point.y:0.00} ({hit.collider.name})");
                    }
                }
            LevelsAt(at.x, at.z, levels);
            var here = new List<string>();
            foreach (var l in levels) here.Add($"{l.Y:0.00} {l.Tile} ({l.Terrain})");
            CheckResult = $"walkable ground within {r:0} sampled at {rays} points: drawn level agrees {agree}, disagrees {disagree}, unknown {unknown}, nothing drawn {none}. Under the keeper: {(here.Count > 0 ? string.Join("; ", here) : "no chunk")}. Tiles read: {Captured} meshes ({CapturedAtStrip} as the game freed them, {Failed} failed); {IndexNote}.";
            Plugin.Log.LogInfo("[drawn-ground] check: " + CheckResult + (worst.Count > 0 ? " Disagreeing, e.g.: " + string.Join("; ", worst) : ""));
        }

        // Whether the level at height 'y' has a drawn tile at x, z (any of its terrains' chunks there).
        internal static bool DrawnAtLevel(float x, float z, float y)
        {
            long key = ((long)Mathf.FloorToInt(x / Bucket) << 32) ^ (uint)Mathf.FloorToInt(z / Bucket);
            if (!buckets.TryGetValue(key, out List<Chunk> list)) return false;
            foreach (Chunk ch in list)
            {
                if (ch.Terrain == null || Mathf.Abs(ch.Y - y) > 0.05f) continue;
                if (TileAt(ch, x, z, out bool inside) == Tile.Drawn && inside) return true;
            }
            return false;
        }

        // How many tiles in from its level's edge a drawn tile at x, z, level y lies: 0 = an edge tile (a tile beside it
        // along x or z isn't drawn), 1 = one in, ... up to 'max' (deeper counts as max).
        internal static int TilesFromEdge(float x, float z, float y, int max = 4)
        {
            for (int n = 1; n <= max; n++)
            {
                float dx = n * 0.96f, dz = n * 1.2f;
                if (!DrawnAtLevel(x + dx, z, y) || !DrawnAtLevel(x - dx, z, y) || !DrawnAtLevel(x, z + dz, y) || !DrawnAtLevel(x, z - dz, y)) return n - 1;
            }
            return max;
        }

        // The cover check: where the walkable ground has a higher level drawn over it (a rock table over the ground it
        // hides, a rim's edge tiles over the foot of its cliff), and how far in from that level's edge. Rays straight
        // down on a grid as Check; counted: covered by tiles-from-edge (0 = the edge tile, 1, 2, 3, 4+), with examples.
        internal static string CoverResult = "";

        internal static void CoverCheck(float r = 30f, float step = 0.5f)
        {
            Rigidbody rb = MainGame.Instance != null ? MainGame.PlayerController?.PhysicalBody?.Rb : null;
            if (rb == null) { CoverResult = "not in a game"; return; }
            Index();
            Vector3 at = rb.position;
            int rays = 0, covered = 0;
            var byDepth = new int[5];
            var examples = new List<string>();
            var deep = new List<string>();
            for (float x = at.x - r; x <= at.x + r; x += step)
                for (float z = at.z - r; z <= at.z + r; z += step)
                {
                    if (!Physics.Raycast(new Vector3(x, at.y + 40f, z), Vector3.down, out RaycastHit hit, 80f, 6144, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<WaterComponentTag>() != null) continue;
                    rays++;
                    float top = HighestAt(x, z, float.PositiveInfinity, out _);
                    if (float.IsNaN(top) || top <= hit.point.y + 0.6f) continue;
                    covered++;
                    int d = TilesFromEdge(x, z, top);
                    byDepth[Mathf.Min(d, 4)]++;
                    string e = $"({x:0.0},{z:0.0}) walkable {hit.point.y:0.00} ({hit.collider.name}) under drawn {top:0.00}, {d} tile(s) in";
                    if (d >= 2 && deep.Count < 8) deep.Add(e);
                    else if (examples.Count < 6) examples.Add(e);
                }
            CoverResult = $"within {r:0} ({rays} rays on walkable ground): covered by a higher drawn level at {covered} ({(rays > 0 ? 100f * covered / rays : 0f):0.0} %): edge tile {byDepth[0]}, 1 in {byDepth[1]}, 2 in {byDepth[2]}, 3 in {byDepth[3]}, 4+ in {byDepth[4]}.";
            Plugin.Log.LogInfo("[drawn-ground] cover check: " + CoverResult + (deep.Count > 0 ? " Deep (2+ tiles in), e.g.: " + string.Join("; ", deep) : "") + (examples.Count > 0 ? " Edge, e.g.: " + string.Join("; ", examples) : ""));
        }

        // The tab's readout: the levels under the keeper now.
        internal static string Here()
        {
            Rigidbody rb = MainGame.Instance != null ? MainGame.PlayerController?.PhysicalBody?.Rb : null;
            if (rb == null) return "";
            LevelsAt(rb.position.x, rb.position.z, levels);
            var parts = new List<string>();
            foreach (var l in levels) parts.Add($"{l.Y:0.0}{(l.Tile == Tile.Drawn ? " drawn" : l.Tile == Tile.Unknown ? " ?" : " -")}");
            return parts.Count > 0 ? string.Join(", ", parts) : "no terrain chunk";
        }
    }

    // A terrain wakes (the scene loading) or goes: the drawn ground's index follows (DrawnGround.TerrainAwake). Dev tools only.
    [HarmonyPatch(typeof(LazyTerrain), "Awake")]
    internal static class LazyTerrain_Awake_Patch
    {
        private static void Postfix(LazyTerrain __instance)
        {
            try { DrawnGround.TerrainAwake(__instance); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[drawn-ground] a terrain could not be indexed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LazyTerrain), "OnDestroy")]
    internal static class LazyTerrain_OnDestroy_Patch
    {
        private static void Prefix(LazyTerrain __instance)
        {
            try { DrawnGround.TerrainGone(__instance); }
            catch { }
        }
    }

    // The game frees a terrain mesh's readable copy here; its tiles are read first (DrawnGround). Dev tools only.
    [HarmonyPatch(typeof(LazyTerrainMeshCollection), nameof(LazyTerrainMeshCollection.Runtime_StripCpuMeshData))]
    internal static class LazyTerrainMeshCollection_Runtime_StripCpuMeshData_Patch
    {
        private static void Prefix(Mesh mesh)
        {
            try { DrawnGround.BeforeStrip(mesh); }
            catch { }
        }
    }
}
