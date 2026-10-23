using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flying experiment's survey (docs/flying.md, "how tall is a thing"): every collider around the keeper, with what
    // could tell how tall the thing is in the world: the collider's own top, the pictures beside it (the renderers under
    // its parent, and under its object's root), the ground under it, the objects it belongs to. Written to
    // BepInEx/GK2-flight-survey.csv with a screenshot (GK2-flight-survey.png) and every collider's place on that
    // screenshot, so the numbers can be held against what the eye sees. The Flying tab's button; nothing else runs it.
    internal static class FlightSurvey
    {
        internal static string LastResult = "";
        private static readonly Collider[] found = new Collider[2048];
        private static int runs;

        internal static void Run(float radius)
        {
            PlayerController pc = MainGame.Instance != null ? MainGame.PlayerController : null;
            Rigidbody rb = pc?.PhysicalBody?.Rb;
            Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            if (rb == null || cam == null) { LastResult = "no keeper or no camera"; return; }
            Vector3 at = rb.position;
            int n = Physics.OverlapSphereNonAlloc(at, radius, found, ~0, QueryTriggerInteraction.Collide);
            var sb = new StringBuilder();
            sb.AppendLine("name;path;layer;type;trigger;rigidbody;bounds_min_y;bounds_max_y;size_x;size_y;size_z;center_x;center_z;ground_y;collider_height;beside_top;beside_count;beside_names;root;root_top;root_count;wgo;drawn_top;wgo_baked_top;see_through_shape;screen_x_top;screen_y_top;screen_x_base;screen_y_base;components");
            int rows = 0;
            var layers = new Dictionary<string, int>();
            for (int i = 0; i < n && i < found.Length; i++)
            {
                Collider c = found[i];
                if (c == null || !c) continue;
                if (c.attachedRigidbody == rb) continue;
                Bounds b = c.bounds;
                string layer = LayerMask.LayerToName(c.gameObject.layer);
                if (string.IsNullOrEmpty(layer)) layer = c.gameObject.layer.ToString();
                layers[layer] = layers.TryGetValue(layer, out int k) ? k + 1 : 1;
                float g = Mount.Instance.GroundHeight(new Vector3(b.center.x, at.y, b.center.z), out _);
                Transform beside = c.transform.parent != null ? c.transform.parent : c.transform;
                Tops(beside, out float besideTop, out int besideCount, out string besideNames);
                Transform root = Root(c.transform);
                Tops(root, out float rootTop, out int rootCount, out _);
                Vector3 sTop = cam.WorldToScreenPoint(new Vector3(b.center.x, Mathf.Max(b.max.y, besideTop), b.center.z));
                Vector3 sBase = cam.WorldToScreenPoint(new Vector3(b.center.x, float.IsNaN(g) ? b.min.y : g, b.center.z));
                sb.Append(Csv(c.name)).Append(';').Append(Csv(Path(c.transform))).Append(';').Append(layer).Append(';').Append(c.GetType().Name).Append(';')
                  .Append(c.isTrigger).Append(';').Append(c.attachedRigidbody != null ? (c.attachedRigidbody.isKinematic ? "kinematic" : "dynamic") : "-").Append(';')
                  .Append(F(b.min.y)).Append(';').Append(F(b.max.y)).Append(';').Append(F(b.size.x)).Append(';').Append(F(b.size.y)).Append(';').Append(F(b.size.z)).Append(';')
                  .Append(F(b.center.x)).Append(';').Append(F(b.center.z)).Append(';').Append(F(g)).Append(';').Append(F(float.IsNaN(g) ? b.size.y : b.max.y - g)).Append(';')
                  .Append(F(besideTop)).Append(';').Append(besideCount).Append(';').Append(Csv(besideNames)).Append(';')
                  .Append(Csv(root.name)).Append(';').Append(F(rootTop)).Append(';').Append(rootCount).Append(';')
                  .Append(Csv(WgoName(c, out float meshTop, out float bakedTop))).Append(';').Append(F(meshTop)).Append(';').Append(F(bakedTop)).Append(';').Append(c.gameObject.layer == 15).Append(';')
                  .Append(F(sTop.x)).Append(';').Append(F(Screen.height - sTop.y)).Append(';').Append(F(sBase.x)).Append(';').Append(F(Screen.height - sBase.y)).Append(';')
                  .Append(Csv(Components(c.transform))).AppendLine();
                rows++;
            }
            string dir = Paths.BepInExRootPath;
            runs++;
            string stem = $"GK2-flight-survey-{System.DateTime.Now:HHmmss}";
            File.WriteAllText(System.IO.Path.Combine(dir, stem + "-column.txt"), Column(at, pc));
            File.WriteAllText(System.IO.Path.Combine(dir, stem + ".csv"), $"# keeper at {at.x:0.000},{at.y:0.000},{at.z:0.000}; camera {cam.transform.position:F3} ortho {cam.orthographicSize:0.###}; screen {Screen.width}x{Screen.height}; radius {radius}\n" + sb);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, stem + ".png"));
            var parts = new List<string>();
            foreach (var kv in layers) parts.Add($"{kv.Key} {kv.Value}");
            LastResult = $"{rows} colliders within {radius:0} (by layer: {string.Join(", ", parts)}); BepInEx/{stem}.csv and .png (survey {runs} this session)";
            Plugin.Log.LogInfo("[flight-survey] " + LastResult);
        }

        // What stands in the column over the keeper (the mount's flight above him): every collider a ray from 60 above
        // meets straight down, any layer, triggers too, and every renderer whose bounds hold his x and z, any size, with
        // whether it has a collider of its own and whether its mesh could be read (made into a collider). For the places
        // a flight goes through drawn ground (the user, 2026-10-22: a high rock table with no ground under it to physics).
        private static string Column(Vector3 at, PlayerController pc)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# the column over the keeper at {at.x:0.000},{at.y:0.000},{at.z:0.000}; flight: {Flight.Now}, {Flight.HeightPx:0} px, free {Flight.Free}");
            sb.AppendLine("## colliders straight down from 60 above (y, layer, type, trigger, name, path)");
            Vector3 top = new Vector3(at.x, at.y + 60f, at.z);
            RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, 120f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (RaycastHit h in hits)
            {
                Collider c = h.collider;
                sb.AppendLine($"{h.point.y:0.000}; {LayerMask.LayerToName(c.gameObject.layer)}; {c.GetType().Name}; {c.isTrigger}; {c.name}; {Path(c.transform)}");
            }
            sb.AppendLine("## also with back faces (a one-sided mesh seen from below its surface)");
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                foreach (RaycastHit h in Physics.RaycastAll(top, Vector3.down, 120f, ~0, QueryTriggerInteraction.Collide))
                    sb.AppendLine($"{h.point.y:0.000}; {LayerMask.LayerToName(h.collider.gameObject.layer)}; {h.collider.name}; {Path(h.collider.transform)}");
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
            sb.AppendLine("## renderers whose bounds hold his x and z (min y, max y, size x z, layer, type, enabled, shadows, mesh, readable, vertices, own collider, path)");
            foreach (Renderer rd in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (rd == null || !rd.gameObject.activeInHierarchy) continue;
                Bounds b = rd.bounds;
                if (at.x < b.min.x || at.x > b.max.x || at.z < b.min.z || at.z > b.max.z) continue;
                if (b.max.y < at.y - 20f || b.min.y > at.y + 40f) continue;
                Mesh mesh = rd.GetComponent<MeshFilter>()?.sharedMesh;
                string meshInfo = mesh != null ? $"{mesh.name}; {mesh.isReadable}; {mesh.vertexCount}" : (rd is SpriteRenderer sr && sr.sprite != null ? $"sprite {sr.sprite.name}; -; -" : "-; -; -");
                Collider own = rd.GetComponent<Collider>();
                sb.AppendLine($"{b.min.y:0.000}; {b.max.y:0.000}; {b.size.x:0.0}x{b.size.z:0.0}; {LayerMask.LayerToName(rd.gameObject.layer)}; {rd.GetType().Name}; {rd.enabled}; {rd.shadowCastingMode}; {meshInfo}; {(own != null ? own.GetType().Name + " " + LayerMask.LayerToName(own.gameObject.layer) : "-")}; {Path(rd.transform)}");
            }
            // The drawn terrain (the game's LazyTerrain: chunks of baked meshes, the only copy of the drawn ground; the game
            // strips their CPU data when it shows them, LazyTerrainMesh.DrawFromData): every terrain, its height, and its
            // chunks whose x/z bounds hold the keeper or lie within 4 of him, with whether the mesh can still be read.
            sb.AppendLine("## LazyTerrain objects (path, position, size, chunks, castShadows)");
            foreach (LazyTerrain t in Object.FindObjectsByType<LazyTerrain>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                sb.AppendLine($"terrain {Path(t.transform)}; pos {t.transform.position.x:0.000},{t.transform.position.y:0.000},{t.transform.position.z:0.000}; {t.width}x{t.height} meshSize {t.meshSize} tile {t.meshTileSize}; chunks {t.meshesData?.Count ?? 0}; active {t.gameObject.activeInHierarchy}");
                if (t.meshesData == null) continue;
                foreach (LazyTerrainMeshData d in t.meshesData)
                {
                    if (d == null) continue;
                    Vector3 wp = t.transform.position + d.localPosition;
                    if (Mathf.Abs(wp.x - at.x) > 5f || Mathf.Abs(wp.z - at.z) > 5f) continue;
                    Mesh m = d.mesh;
                    string mi = m == null ? "no mesh" : $"mesh {m.name}; readable {m.isReadable}; vertices {m.vertexCount}; submeshes {m.subMeshCount}; mesh bounds y {m.bounds.min.y:0.000}..{m.bounds.max.y:0.000} (local), x {m.bounds.min.x:0.00}..{m.bounds.max.x:0.00} z {m.bounds.min.z:0.00}..{m.bounds.max.z:0.00}";
                    sb.AppendLine($"  chunk at {wp.x:0.000},{wp.y:0.000},{wp.z:0.000} (local {d.localPosition.x:0.00},{d.localPosition.y:0.00},{d.localPosition.z:0.00}; center {d.center.x:0.00},{d.center.y:0.00},{d.center.z:0.00}); data bounds {d.bounds.center.y:0.000}±{d.bounds.extents.y:0.000}; {mi}");
                    if (m != null && m.isReadable && m.vertexCount > 0)
                    {
                        // the heights its vertices sit at (world), as a histogram: does a chunk hold one level or several?
                        var ys = new Dictionary<float, int>();
                        foreach (Vector3 v in m.vertices)
                        {
                            float y = Mathf.Round((wp.y + v.y) * 100f) / 100f;
                            ys[y] = ys.TryGetValue(y, out int k) ? k + 1 : 1;
                        }
                        var parts = new List<string>();
                        foreach (var kv in ys) if (parts.Count < 30) parts.Add($"{kv.Key:0.00}x{kv.Value}");
                        sb.AppendLine("    vertex heights: " + string.Join(" ", parts));
                    }
                }
            }
            sb.AppendLine($"## the flight's own reads here: ground under the flight {Mount.Instance.GroundHeight(at, out string src):0.000} ({src}), the floor under the mount {Mount.Instance.FloorUnder(pc, at, at.y + Mathf.Max(0f, Flight.HeightPx) * 2f * 0.01666667f):0.000}");
            return sb.ToString();
        }

        // The collider's object: its Wgo's name, its meshes' top (what the flight's bumping reads) and its baked bounds' top.
        private static string WgoName(Collider c, out float meshTop, out float bakedTop)
        {
            meshTop = Mount.Instance.ObjectTop(c, out Wgo w);
            bakedTop = float.NaN;
            if (w == null) return "";
            try
            {
                var d = w.Data;
                if (d != null && d.HasSerializedBounds) bakedTop = w.transform.position.y + d.SerializedBounds.withoutShadows.max.y;
            }
            catch { }
            return w.name;
        }

        // The highest drawn point under 't' (sprites and meshes, not particles nor the scene's huge ones).
        private static void Tops(Transform t, out float top, out int count, out string names)
        {
            top = float.NegativeInfinity; count = 0;
            var nm = new StringBuilder();
            foreach (Renderer rd in t.GetComponentsInChildren<Renderer>(false))
            {
                if (rd is ParticleSystemRenderer || !rd.enabled) continue;
                Bounds rb = rd.bounds;
                if (rb.size.x > 30f || rb.size.z > 30f) continue;
                count++;
                if (rb.max.y > top) top = rb.max.y;
                if (nm.Length < 120) nm.Append(rd.name).Append(rd is SpriteRenderer sr && sr.sprite != null ? "[" + sr.sprite.name + "]" : "").Append(' ');
            }
            names = nm.ToString().Trim();
        }

        // The object a collider belongs to: up the parents until the scene's own containers (a parent with very many
        // children) or five levels.
        private static Transform Root(Transform t)
        {
            Transform r = t;
            for (int i = 0; i < 5 && r.parent != null && r.parent.childCount < 60; i++) r = r.parent;
            return r;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; i < 5 && t != null; i++, t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string Components(Transform t)
        {
            var names = new List<string>();
            for (int i = 0; i < 4 && t != null; i++, t = t.parent)
                foreach (Component c in t.GetComponents<Component>())
                    if (c != null && !(c is Transform) && !(c is Renderer) && !(c is Collider) && names.Count < 12 && !names.Contains(c.GetType().Name)) names.Add(c.GetType().Name);
            return string.Join(" ", names);
        }

        private static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "" : v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        private static string Csv(string s) => (s ?? "").Replace(';', ',').Replace('\n', ' ');
    }
}
