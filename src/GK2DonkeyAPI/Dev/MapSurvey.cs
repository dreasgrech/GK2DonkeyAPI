using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flying experiment's whole-map survey (docs/flying.md, "the map"): everything the loaded scenes hold that says
    // how high things are, written once to BepInEx/GK2-map-survey-HHmmss/ for tools/flying/map_survey.py to draw:
    //   terrain.csv  every LazyTerrain chunk (the drawn ground): its world place, its mesh's bounds, readable or not;
    //   cells.csv    for the chunks whose meshes can still be read, the highest near-flat triangle per 0.5 cell;
    //   props.csv    every baked prop of the scenes (rocks, walls, trees, ruins: GameScene.bakedChunkableObjectComponentDatas),
    //                spawned or not, with its bounds;
    //   wgos.csv     every world object (Wgo) loaded, with its baked bounds;
    //   colliders.csv every ground (11, 12), water (4), see-through (15) and world-zone (17) collider loaded, with bounds;
    //   scenes.txt   the scenes, terrains and zones found, and the counts.
    // Nothing changes in the game; one press, a second or two of work. The Flying tab's button.
    internal static class MapSurvey
    {
        internal static string LastResult = "";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Run()
        {
            string dir = System.IO.Path.Combine(Paths.BepInExRootPath, $"GK2-map-survey-{System.DateTime.Now:HHmmss}");
            Directory.CreateDirectory(dir);
            var info = new StringBuilder();
            int terrains = 0, chunks = 0, readable = 0, fromTiles = 0, cells = 0, props = 0, wgos = 0, cols = 0;
            var rects = new List<Rect>();

            // the drawn ground
            var terrain = new StringBuilder("terrain;chunk;x;z;local_y;min_y;max_y;size_x;size_z;readable;vertices;tiles_read\n");
            var cellOut = new StringBuilder("x;z;top\n");
            var cellTop = new Dictionary<long, float>();
            foreach (LazyTerrain t in Object.FindObjectsByType<LazyTerrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                terrains++;
                string tp = PathOf(t.transform);
                info.AppendLine($"terrain {tp}: at {V(t.transform.position)}, {t.width}x{t.height}, meshSize {t.meshSize}, {t.meshesData?.Count ?? 0} chunks, active {t.gameObject.activeInHierarchy}");
                if (t.meshesData == null) continue;
                for (int i = 0; i < t.meshesData.Count; i++)
                {
                    LazyTerrainMeshData d = t.meshesData[i];
                    if (d == null) continue;
                    chunks++;
                    Vector3 origin = t.transform.position + d.localPosition;
                    Mesh m = d.mesh;
                    if (m == null) { terrain.Append(tp).Append(';').Append(i).Append(';').Append(F(origin.x)).Append(';').Append(F(origin.z)).Append(";;;;;;no mesh;\n"); continue; }
                    Bounds b = m.bounds;
                    terrain.Append(tp).Append(';').Append(i).Append(';').Append(F(origin.x + b.center.x)).Append(';').Append(F(origin.z + b.center.z)).Append(';')
                        .Append(F(d.localPosition.y)).Append(';').Append(F(origin.y + b.min.y)).Append(';').Append(F(origin.y + b.max.y)).Append(';')
                        .Append(F(b.size.x)).Append(';').Append(F(b.size.z)).Append(';').Append(m.isReadable).Append(';').Append(m.vertexCount).Append(';').Append(DrawnGround.DrawnTiles(m, rects)).Append('\n');
                    if (!m.isReadable)
                    {
                        // freed by the game: the tiles the mod read before it did (DrawnGround), each tile its cells
                        if (!DrawnGround.DrawnTiles(m, rects)) continue;
                        fromTiles++;
                        foreach (Rect r in rects)
                            for (int x = Mathf.FloorToInt((origin.x + r.xMin) * 2f); x < Mathf.CeilToInt((origin.x + r.xMax) * 2f); x++)
                                for (int z = Mathf.FloorToInt((origin.z + r.yMin) * 2f); z < Mathf.CeilToInt((origin.z + r.yMax) * 2f); z++)
                                {
                                    long key = ((long)x << 32) ^ (uint)z;
                                    if (!cellTop.TryGetValue(key, out float old) || origin.y > old) cellTop[key] = origin.y;
                                }
                        continue;
                    }
                    readable++;
                    Vector3[] v = m.vertices;
                    int[] tri = m.triangles;
                    for (int k = 0; k + 2 < tri.Length; k += 3)
                    {
                        Vector3 a = origin + v[tri[k]], bb = origin + v[tri[k + 1]], c = origin + v[tri[k + 2]];
                        Vector3 n = Vector3.Cross(bb - a, c - a);
                        if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.7f) continue; // walls and cliff faces: not a top
                        float top = Mathf.Max(a.y, Mathf.Max(bb.y, c.y));
                        int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(bb.x, c.x)) * 2f), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(bb.x, c.x)) * 2f);
                        int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(bb.z, c.z)) * 2f), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(bb.z, c.z)) * 2f);
                        for (int x = x0; x <= x1; x++)
                            for (int z = z0; z <= z1; z++)
                            {
                                long key = ((long)x << 32) ^ (uint)z;
                                if (!cellTop.TryGetValue(key, out float old) || top > old) cellTop[key] = top;
                            }
                    }
                }
            }
            foreach (var kv in cellTop)
            {
                int x = (int)(kv.Key >> 32), z = (int)(uint)(kv.Key & 0xffffffff);
                cellOut.Append(F(x * 0.5f + 0.25f)).Append(';').Append(F(z * 0.5f + 0.25f)).Append(';').Append(F(kv.Value)).Append('\n');
                cells++;
            }

            // the scenes' baked props, spawned or not
            var propOut = new StringBuilder("scene;path;x;y;z;min_y;max_y;size_x;size_z\n");
            foreach (GameScene gs in Object.FindObjectsByType<GameScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gs == null) continue;
                info.AppendLine($"scene {gs.Id} ({PathOf(gs.transform)}): {gs.bakedChunkableObjectComponentDatas?.Count ?? 0} baked props");
                if (gs.bakedChunkableObjectComponentDatas == null) continue;
                foreach (BakedChunkableObjectComponentData d in gs.bakedChunkableObjectComponentDatas)
                {
                    if (d == null) continue;
                    props++;
                    Bounds b = Box(d.GetChunkableData());
                    propOut.Append(gs.Id).Append(';').Append((d.pathToObject ?? "").Replace(';', ',')).Append(';').Append(F(d.worldPos.x)).Append(';').Append(F(d.worldPos.y)).Append(';').Append(F(d.worldPos.z)).Append(';')
                        .Append(F(b.min.y)).Append(';').Append(F(b.max.y)).Append(';').Append(F(b.size.x)).Append(';').Append(F(b.size.z)).Append('\n');
                }
            }

            // the world objects
            var wgoOut = new StringBuilder("name;x;y;z;min_y;max_y;size_x;size_z\n");
            foreach (Wgo w in Object.FindObjectsByType<Wgo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (w == null) continue;
                wgos++;
                Vector3 p = w.transform.position;
                float minY = float.NaN, maxY = float.NaN, sx = float.NaN, sz = float.NaN;
                try
                {
                    WgoData d = w.Data;
                    if (d != null && d.HasSerializedBounds)
                    {
                        Bounds b = Box(d.SerializedBounds.withoutShadows);
                        minY = p.y + b.min.y; maxY = p.y + b.max.y; sx = b.size.x; sz = b.size.z;
                    }
                }
                catch { }
                wgoOut.Append(w.name.Replace(';', ',')).Append(';').Append(F(p.x)).Append(';').Append(F(p.y)).Append(';').Append(F(p.z)).Append(';')
                    .Append(F(minY)).Append(';').Append(F(maxY)).Append(';').Append(F(sx)).Append(';').Append(F(sz)).Append('\n');
            }

            // ground, water, see-through shapes, zones
            var colOut = new StringBuilder("layer;type;name;path;min_x;min_y;min_z;max_x;max_y;max_z;enabled\n");
            int wanted = (1 << 11) | (1 << 12) | (1 << 4) | (1 << 15) | (1 << 17);
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c == null || ((1 << c.gameObject.layer) & wanted) == 0) continue;
                cols++;
                Bounds b = c.bounds;
                colOut.Append(LayerMask.LayerToName(c.gameObject.layer)).Append(';').Append(c.GetType().Name).Append(';').Append(c.name.Replace(';', ',')).Append(';').Append(PathOf(c.transform).Replace(';', ',')).Append(';')
                    .Append(F(b.min.x)).Append(';').Append(F(b.min.y)).Append(';').Append(F(b.min.z)).Append(';').Append(F(b.max.x)).Append(';').Append(F(b.max.y)).Append(';').Append(F(b.max.z)).Append(';')
                    .Append(c.enabled && c.gameObject.activeInHierarchy).Append('\n');
            }
            foreach (WorldZone z in Object.FindObjectsByType<WorldZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z != null) info.AppendLine($"zone {z.Id} ({PathOf(z.transform)}) at {V(z.transform.position)}");

            Rigidbody rb = MainGame.Instance != null ? MainGame.PlayerController?.PhysicalBody?.Rb : null;
            info.Insert(0, $"# map survey {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}; keeper at {(rb != null ? V(rb.position) : "-")}; {terrains} terrains, {chunks} chunks ({readable} readable meshes, {fromTiles} from the tiles read before the game freed them, {cells} cells), {props} baked props, {wgos} world objects, {cols} ground/water/see-through/zone colliders\n");
            File.WriteAllText(System.IO.Path.Combine(dir, "scenes.txt"), info.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir, "terrain.csv"), terrain.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir, "cells.csv"), cellOut.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir, "props.csv"), propOut.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir, "wgos.csv"), wgoOut.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir, "colliders.csv"), colOut.ToString());
            LastResult = $"{terrains} terrains, {chunks} chunks ({readable} readable, {fromTiles} from read tiles), {props} baked props, {wgos} objects, {cols} colliders: BepInEx/{System.IO.Path.GetFileName(dir)}";
            Plugin.Log.LogInfo("[map-survey] " + LastResult);
        }

        // BurstableBounds holds Unity.Mathematics float3s (not referenced by the mod): read through reflection.
        private static Bounds Box(object burstable)
        {
            if (burstable is Bounds plain) return plain;
            var t = HarmonyLib.Traverse.Create(burstable);
            return new Bounds(F3(t.Field("center").GetValue()), F3(t.Field("size").GetValue()));
        }

        private static Vector3 F3(object f)
        {
            var t = HarmonyLib.Traverse.Create(f);
            return new Vector3(t.Field("x").GetValue<float>(), t.Field("y").GetValue<float>(), t.Field("z").GetValue<float>());
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; i < 6 && t != null; i++, t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string V(Vector3 v) => $"{v.x.ToString("0.00", Inv)},{v.y.ToString("0.00", Inv)},{v.z.ToString("0.00", Inv)}";
        private static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "" : v.ToString("0.###", Inv);
    }
}
