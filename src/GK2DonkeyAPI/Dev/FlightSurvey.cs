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
            File.WriteAllText(System.IO.Path.Combine(dir, stem + ".csv"), $"# keeper at {at.x:0.000},{at.y:0.000},{at.z:0.000}; camera {cam.transform.position:F3} ortho {cam.orthographicSize:0.###}; screen {Screen.width}x{Screen.height}; radius {radius}\n" + sb);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, stem + ".png"));
            var parts = new List<string>();
            foreach (var kv in layers) parts.Add($"{kv.Key} {kv.Value}");
            LastResult = $"{rows} colliders within {radius:0} (by layer: {string.Join(", ", parts)}); BepInEx/{stem}.csv and .png (survey {runs} this session)";
            Plugin.Log.LogInfo("[flight-survey] " + LastResult);
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
