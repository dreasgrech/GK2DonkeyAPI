using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GK2ShowcaseDirector
{
    // The cargo picture probe (docs/cargo-auto-tune.md, "What would work: judging the picture"): what the camera shows
    // of each item on the cart, in the game's own pixels. A private camera copies the world camera and renders the cargo
    // (and the cart body) into its own texture with every renderer's materials swapped for a probe material, item by
    // item: each item's outline and, with the depth material, its camera distance per pixel; which item is in front of
    // which is then the nearest distance per pixel, exactly what the depth buffer does. The probe material is chosen per
    // session by a test on the first item. First "native": the items keep their own materials and are drawn into a
    // depth-only target, which is copied out with the engine's blit (always in a build) into a float texture: outline
    // (depth written) and camera distance in one render, from the game's own shaders. Then the engine's
    // depth-and-normals shader. Then "slice": the items' own materials into the colour target, drawn on two background
    // colours: a pixel the item covers comes out the same on both; its camera distance by sweeping the far plane
    // forward (the first slice a pixel shows in). Then flat colour shaders.
    // 2026-10-16: this build has neither Hidden/Internal-DepthNormalsTexture nor Unlit/Color, and Hidden/Internal-Colored
    // draws 0 px of a corpse (it multiplies by the vertex colours, which this game's meshes use for other data).
    // Only a region around the cart is read back (the world camera renders at the screen's full size).
    // Safety: a Session swaps the renderers' layer, materials and property blocks, and an item's position for a sweep,
    // and puts every one back in Unswap / Dispose (and in a finally); it renders only by explicit Camera.Render calls
    // inside one frame step, so the game never draws a swapped object. Reaches into the API's cargo rig by reflection
    // (internal, dev tool to dev tool) for the placed views; nothing is written to the game, the layout or the save.
    internal static class PictureProbe
    {
        private const int ProbeLayer = 31;
        internal const int Cart = 6; // the index of the cart body (items are 0..5)

        private static readonly Color32[] Palette =
        {
            new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255),
            new Color32(255, 255, 0, 255), new Color32(255, 0, 255, 255), new Color32(0, 255, 255, 255),
            new Color32(128, 128, 128, 255),
        };

        private static readonly string[] Candidates = { "Hidden/Internal-DepthNormalsTexture", "Hidden/Internal-Colored", "Unlit/Color" };

        private static bool IsShadowMesh(string name) =>
            name.EndsWith("_sh") || name.EndsWith("-sh") || name.EndsWith("_sh2") || name.EndsWith("-sh2");

        internal static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(o);

        internal static object Rig()
        {
            Type cargo = typeof(GK2DonkeyAPI.Api.Mounts).Assembly.GetType("GK2DonkeyAPI.Cargo");
            return cargo?.GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        }

        internal static object RigMember(object rig, string name)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = rig.GetType();
            return t.GetProperty(name, F)?.GetValue(rig) ?? t.GetField(name, F)?.GetValue(rig);
        }

        internal static bool CargoViews(out IList views, out IList placed, out string why)
        {
            views = null;
            placed = null;
            object rig = Rig();
            if (rig == null)
            {
                why = "no cart rig (drive the cart with a load on it)";
                return false;
            }
            views = RigMember(rig, "PlacedViews") as IList;
            placed = RigMember(rig, "Placed") as IList;
            if (views == null || placed == null)
            {
                why = $"the rig's views or Placed not found (views {(views == null ? "missing" : "ok")}, Placed {(placed == null ? "missing" : "ok")})";
                return false;
            }
            if (views.Count == 0 || views.Count != placed.Count)
            {
                why = $"{views.Count} views for {placed.Count} placed items";
                return false;
            }
            why = null;
            return true;
        }

        internal sealed class Session : IDisposable
        {
            public int W, H, N;          // the read region's size (pixels), the items
            public RectInt Roi;          // the read region in the world camera's target (rows upward)
            public Camera World;
            public string Mode = "";     // "depth" or "flat"
            public string ShaderName = "";
            public string Test = "";     // the shader test's numbers
            public string Note = "";
            public int CartRenderers;
            public readonly List<Transform> Views = new List<Transform>();
            private readonly List<Renderer>[] renderers = new List<Renderer>[Cart + 1];
            private readonly Material[] mats = new Material[Cart + 1];
            private readonly Dictionary<GameObject, int> oldLayers = new Dictionary<GameObject, int>();
            private readonly Dictionary<Renderer, Material[]> oldMats = new Dictionary<Renderer, Material[]>();
            private readonly Dictionary<Renderer, MaterialPropertyBlock[]> oldBlocks = new Dictionary<Renderer, MaterialPropertyBlock[]>();
            private readonly Dictionary<Transform, Vector3> oldPos = new Dictionary<Transform, Vector3>();
            private readonly float[][] soloDepth = new float[Cart + 1][]; // depth mode: each item's distances in the region (NaN: none), until it moves
            private GameObject go;
            private Camera cam;
            private RenderTexture rt, depthRt, floatRt;
            private Texture2D readF;
            private Texture2D read;
            private bool swapped;
            private float far;

            public static Session Begin(out string why)
            {
                var s = new Session();
                why = s.Open();
                if (why != null)
                {
                    s.Dispose();
                    return null;
                }
                return s;
            }

            private string Open()
            {
                World = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
                if (World == null) return "no world camera";
                if (!CargoViews(out IList views, out IList placed, out string why)) return why;
                N = Math.Min(views.Count, Cart);
                RenderTexture target = World.targetTexture;
                int tw = target != null ? target.width : World.pixelWidth, th = target != null ? target.height : World.pixelHeight;
                for (int k = 0; k <= Cart; k++) renderers[k] = new List<Renderer>();
                var cargoSet = new HashSet<Renderer>();
                for (int k = 0; k < N; k++)
                {
                    var view = views[k] as Component;
                    Views.Add(view != null ? view.transform : null);
                    if (view == null) continue;
                    foreach (Renderer r in view.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!r.enabled || IsShadowMesh(r.gameObject.name) || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                        renderers[k].Add(r);
                        cargoSet.Add(r);
                    }
                }
                Transform container = RigMember(Rig(), "container") as Transform;
                Transform body = container != null ? container.parent : null;
                if (body != null)
                {
                    foreach (Renderer r in body.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!r.enabled || cargoSet.Contains(r) || IsShadowMesh(r.gameObject.name) || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                        if (container != null && r.transform.IsChildOf(container)) continue;
                        renderers[Cart].Add(r);
                    }
                }
                CartRenderers = renderers[Cart].Count;

                // the read region: every item's and the cart's bounds on the screen, plus room for a sweep
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, dmax = 0f;
                Matrix4x4 toCam = World.worldToCameraMatrix;
                for (int k = 0; k <= Cart; k++)
                {
                    foreach (Renderer r in renderers[k])
                    {
                        Bounds b = r.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                            Vector3 v = World.WorldToViewportPoint(p);
                            x0 = Mathf.Min(x0, v.x * tw); x1 = Mathf.Max(x1, v.x * tw);
                            y0 = Mathf.Min(y0, v.y * th); y1 = Mathf.Max(y1, v.y * th);
                            dmax = Mathf.Max(dmax, -toCam.MultiplyPoint(p).z);
                        }
                    }
                }
                if (x1 < x0) return "nothing to draw";
                const int margin = 80;
                int rx0 = Mathf.Clamp(Mathf.FloorToInt(x0) - margin, 0, tw), ry0 = Mathf.Clamp(Mathf.FloorToInt(y0) - margin, 0, th);
                int rx1 = Mathf.Clamp(Mathf.CeilToInt(x1) + margin, 0, tw), ry1 = Mathf.Clamp(Mathf.CeilToInt(y1) + margin, 0, th);
                Roi = new RectInt(rx0, ry0, rx1 - rx0, ry1 - ry0);
                W = Roi.width;
                H = Roi.height;
                far = dmax + 2f; // the depth shader writes distance / far: a tight far keeps its 16 bits fine (~0.2 mm here)

                go = new GameObject("director-picture-probe");
                cam = go.AddComponent<Camera>();
                cam.CopyFrom(World);
                cam.enabled = false;
                rt = new RenderTexture(tw, th, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 };
                read = new Texture2D(W, H, TextureFormat.RGBA32, false);

                // the probe material: the first candidate that draws the first item's real outline
                var test = new StringBuilder();
                int expect = ExpectedArea(0);
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth) && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
                {
                    depthRt = new RenderTexture(tw, th, 24, RenderTextureFormat.Depth) { filterMode = FilterMode.Point };
                    floatRt = new RenderTexture(tw, th, 0, RenderTextureFormat.RFloat) { filterMode = FilterMode.Point };
                    readF = new Texture2D(W, H, TextureFormat.RFloat, false);
                    Mode = "native";
                    ShaderName = "the items' own materials into a depth target (reversed z " + SystemInfo.usesReversedZBuffer + ")";
                    Array.Clear(soloDepth, 0, soloDepth.Length);
                    int got = CountSolo(0);
                    test.Append($"native depth: {got} px of a {expect} px box; ");
                    Unswap();
                    if (expect > 0 && got >= expect / 4)
                    {
                        Test = test.ToString();
                        return null;
                    }
                }
                else
                {
                    test.Append("native depth: no depth or float render textures; ");
                }
                {
                    Mode = "slice";
                    ShaderName = "the items' own materials, two backgrounds, the far plane swept";
                    Array.Clear(soloDepth, 0, soloDepth.Length);
                    int got = CountSolo(0);
                    test.Append($"slice: {got} px of a {expect} px box; ");
                    Unswap();
                    if (expect > 0 && got >= expect / 4)
                    {
                        Test = test.ToString();
                        return null;
                    }
                }
                foreach (string name in Candidates)
                {
                    Shader sh = Shader.Find(name);
                    if (sh == null) { test.Append($"{name}: not in the build; "); continue; }
                    UseShader(sh, name.Contains("DepthNormals") ? "depth" : "flat");
                    int got = CountSolo(0);
                    test.Append($"{name}: {got} px of a {expect} px box; ");
                    Unswap();
                    if (expect > 0 && got >= expect / 4)
                    {
                        Test = test.ToString();
                        return null;
                    }
                }
                Test = test.ToString();
                return "no probe shader draws the item's outline: " + Test;
            }

            // The pixels of k's bounding box on the screen (an upper bound of its outline's area).
            private int ExpectedArea(int k)
            {
                if (renderers[k].Count == 0) return 0;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (Renderer r in renderers[k])
                {
                    Bounds b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 v = World.WorldToViewportPoint(p);
                        float px = v.x * rt.width, py = v.y * rt.height;
                        x0 = Mathf.Min(x0, px); x1 = Mathf.Max(x1, px); y0 = Mathf.Min(y0, py); y1 = Mathf.Max(y1, py);
                    }
                }
                return Mathf.Max(0, Mathf.RoundToInt((x1 - x0) * (y1 - y0)));
            }

            private void UseShader(Shader sh, string mode)
            {
                Mode = mode;
                ShaderName = sh.name;
                for (int k = 0; k <= Cart; k++)
                {
                    if (mats[k] != null) UnityEngine.Object.Destroy(mats[k]);
                    mats[k] = new Material(sh);
                    if (mode == "flat")
                    {
                        mats[k].SetColor("_Color", Palette[k]);
                        mats[k].SetInt("_ZWrite", 1);
                        mats[k].SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                        mats[k].SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    }
                }
                Array.Clear(soloDepth, 0, soloDepth.Length);
            }

            private int CountSolo(int k)
            {
                int n = 0;
                if (Mode != "flat")
                {
                    foreach (float d in SoloDepth(k)) if (!float.IsNaN(d)) n++;
                }
                else
                {
                    foreach (int o in Owners(1 << k)) if (o == k) n++;
                }
                return n;
            }

            public bool HasRenderers(int k) => renderers[k].Count > 0;

            // The normal picture of the region (the game's own materials, the world camera's layers).
            public Color32[] NormalPixels()
            {
                Unswap();
                cam.targetTexture = rt;
                cam.cullingMask = World.cullingMask;
                cam.clearFlags = World.clearFlags;
                cam.backgroundColor = World.backgroundColor;
                cam.nearClipPlane = World.nearClipPlane;
                cam.farClipPlane = World.farClipPlane;
                cam.Render();
                return ReadBack();
            }

            private void Swap()
            {
                if (swapped) return;
                swapped = true;
                var block = new MaterialPropertyBlock();
                for (int k = 0; k <= Cart; k++)
                {
                    foreach (Renderer r in renderers[k])
                    {
                        if (oldMats.ContainsKey(r)) continue;
                        if (!oldLayers.ContainsKey(r.gameObject)) oldLayers[r.gameObject] = r.gameObject.layer;
                        if (Mode == "native" || Mode == "slice") continue; // its own materials: only its layer is moved (Draw)
                        Material[] sm = r.sharedMaterials;
                        oldMats[r] = sm;
                        var blocks = new MaterialPropertyBlock[sm.Length];
                        for (int m = 0; m < sm.Length; m++)
                        {
                            blocks[m] = new MaterialPropertyBlock();
                            r.GetPropertyBlock(blocks[m], m);
                        }
                        oldBlocks[r] = blocks;
                        var probe = new Material[sm.Length];
                        for (int m = 0; m < sm.Length; m++) probe[m] = mats[k];
                        r.sharedMaterials = probe;
                        block.Clear();
                        if (Mode == "flat") block.SetColor("_Color", Palette[k]);
                        for (int m = 0; m < sm.Length; m++) r.SetPropertyBlock(block, m);
                    }
                }
                cam.targetTexture = Mode == "native" ? depthRt : rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << ProbeLayer;
            }

            public void Unswap()
            {
                if (!swapped) return;
                RestoreMaterials();
                swapped = false;
            }

            private void RestoreMaterials()
            {
                // native: no materials were swapped, only layers (oldMats is empty)
                foreach (var kv in oldMats)
                {
                    if (kv.Key == null) continue;
                    kv.Key.sharedMaterials = kv.Value;
                    MaterialPropertyBlock[] blocks = oldBlocks[kv.Key];
                    for (int m = 0; m < blocks.Length; m++) kv.Key.SetPropertyBlock(blocks[m], m);
                }
                oldMats.Clear();
                oldBlocks.Clear();
                foreach (var kv in oldLayers)
                {
                    if (kv.Key != null) kv.Key.layer = kv.Value;
                }
                oldLayers.Clear();
            }

            private void Draw(int mask, float near, float farPlane)
            {
                Swap();
                for (int k = 0; k <= Cart; k++)
                {
                    bool on = (mask & (1 << k)) != 0;
                    foreach (Renderer r in renderers[k]) r.gameObject.layer = on ? ProbeLayer : oldLayers[r.gameObject];
                }
                cam.targetTexture = Mode == "native" ? depthRt : rt;
                cam.nearClipPlane = near;
                cam.farClipPlane = farPlane;
                cam.Render();
            }

            // depth mode: k's camera distance per region pixel (NaN where k has nothing); one render, kept until k moves
            private float[] SoloDepth(int k)
            {
                if (soloDepth[k] != null) return soloDepth[k];
                if (Mode == "native")
                {
                    float near = World.nearClipPlane;
                    Draw(1 << k, near, far);
                    Graphics.Blit(depthRt, floatRt);
                    RenderTexture was = RenderTexture.active;
                    RenderTexture.active = floatRt;
                    readF.ReadPixels(new Rect(Roi.x, Roi.y, W, H), 0, 0, false);
                    readF.Apply(false);
                    RenderTexture.active = was;
                    Color[] raw = readF.GetPixels();
                    bool rev = SystemInfo.usesReversedZBuffer;
                    var dn = new float[raw.Length];
                    for (int i = 0; i < raw.Length; i++)
                    {
                        float z = raw[i].r;
                        // cleared: 0 with a reversed buffer, 1 otherwise; orthographic depth is linear between the planes
                        bool empty = rev ? z <= 1e-7f : z >= 1f - 1e-7f;
                        float z01 = rev ? 1f - z : z;
                        dn[i] = empty ? float.NaN : near + z01 * (far - near);
                    }
                    return soloDepth[k] = dn;
                }
                if (Mode == "slice")
                {
                    var ds = new float[W * H];
                    for (int i = 0; i < ds.Length; i++) ds[i] = float.NaN;
                    float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                    Matrix4x4 toCam = World.worldToCameraMatrix;
                    foreach (Renderer r in renderers[k])
                    {
                        Bounds b = r.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                            float dd = -toCam.MultiplyPoint(p).z;
                            lo = Mathf.Min(lo, dd);
                            hi = Mathf.Max(hi, dd);
                        }
                    }
                    if (float.IsInfinity(lo)) return soloDepth[k] = ds;
                    const float step = 0.01f;
                    int slices = Mathf.Clamp(Mathf.CeilToInt((hi - lo) / step) + 2, 2, 300);
                    for (int sl = 0; sl < slices; sl++)
                    {
                        float fp = lo - step + sl * step;
                        bool[] here = Covered(1 << k, World.nearClipPlane, Mathf.Max(fp, World.nearClipPlane + 1e-3f));
                        for (int i = 0; i < ds.Length; i++)
                        {
                            if (here[i] && float.IsNaN(ds[i])) ds[i] = fp;
                        }
                    }
                    return soloDepth[k] = ds;
                }
                Draw(1 << k, World.nearClipPlane, far);
                Color32[] px = ReadBack();
                var d = new float[px.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    // EncodeDepthNormal: the depth (distance / far) in z and w, as EncodeFloatRG
                    float v = px[i].b / 255f + px[i].a / 255f / 255f;
                    d[i] = v > 1e-5f ? v * far : float.NaN;
                }
                return soloDepth[k] = d;
            }

            // slice: the region pixels the drawn items cover, as the pixels that come out the same on a black and on a
            // white background (the game's own materials; a pixel of the item never depends on what is behind it)
            private bool[] Covered(int mask, float near, float farPlane)
            {
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                Draw(mask, near, farPlane);
                Color32[] a = ReadBack();
                cam.backgroundColor = new Color(1f, 1f, 1f, 1f);
                Draw(mask, near, farPlane);
                Color32[] b = ReadBack();
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                var on = new bool[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    on[i] = Math.Abs(a[i].r - b[i].r) < 3 && Math.Abs(a[i].g - b[i].g) < 3 && Math.Abs(a[i].b - b[i].b) < 3;
                }
                return on;
            }

            // The owner of every region pixel (-1 nothing, an item, Cart) with only the items whose bit is set drawn.
            public int[] Owners(int mask)
            {
                if (Mode != "flat")
                {
                    var owner = new int[W * H];
                    var best = new float[W * H];
                    for (int i = 0; i < owner.Length; i++) { owner[i] = -1; best[i] = float.PositiveInfinity; }
                    for (int k = 0; k <= Cart; k++)
                    {
                        if ((mask & (1 << k)) == 0 || renderers[k].Count == 0) continue;
                        float[] d = SoloDepth(k);
                        for (int i = 0; i < d.Length; i++)
                        {
                            if (!float.IsNaN(d[i]) && d[i] < best[i]) { best[i] = d[i]; owner[i] = k; }
                        }
                    }
                    return owner;
                }
                Draw(mask, World.nearClipPlane, World.farClipPlane);
                Color32[] px = ReadBack();
                var o = new int[px.Length];
                for (int i = 0; i < px.Length; i++) o[i] = FlatOwner(px[i]);
                return o;
            }

            private static int FlatOwner(Color32 p)
            {
                if (p.a < 128) return -1;
                for (int k = 0; k <= Cart; k++)
                {
                    Color32 q = Palette[k];
                    if (Math.Abs(p.r - q.r) < 40 && Math.Abs(p.g - q.g) < 40 && Math.Abs(p.b - q.b) < 40) return k;
                }
                return -2;
            }

            // k's front distances (front) or back distances inside box (region pixels), NaN where none. Depth mode: the
            // front from the one render, no back (NaN). Flat mode: the clipping planes swept through k in steps of step.
            public float[] Depth(int k, bool front, RectInt box, float step, out float dmin, out float dmax)
            {
                dmin = float.PositiveInfinity;
                dmax = float.NegativeInfinity;
                Matrix4x4 toCam = World.worldToCameraMatrix;
                foreach (Renderer r in renderers[k])
                {
                    Bounds b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                        float d = -toCam.MultiplyPoint(p).z;
                        dmin = Mathf.Min(dmin, d);
                        dmax = Mathf.Max(dmax, d);
                    }
                }
                var depth = new float[box.width * box.height];
                for (int i = 0; i < depth.Length; i++) depth[i] = float.NaN;
                if (float.IsInfinity(dmin)) return depth;
                if (Mode != "flat")
                {
                    if (!front) return depth;
                    float[] sd = SoloDepth(k);
                    for (int y = 0; y < box.height; y++)
                    {
                        for (int x = 0; x < box.width; x++)
                        {
                            int sx = box.x + x, sy = box.y + y;
                            if (sx >= 0 && sy >= 0 && sx < W && sy < H) depth[y * box.width + x] = sd[sy * W + sx];
                        }
                    }
                    return depth;
                }
                mats[k].SetInt("_Cull", (int)(front ? UnityEngine.Rendering.CullMode.Back : UnityEngine.Rendering.CullMode.Front));
                try
                {
                    int slices = Mathf.Clamp(Mathf.CeilToInt((dmax - dmin) / step) + 2, 2, 400);
                    for (int sl = 0; sl < slices; sl++)
                    {
                        float d = dmin - step + sl * step;
                        if (front) Draw(1 << k, World.nearClipPlane, Mathf.Max(d, World.nearClipPlane + 1e-3f));
                        else Draw(1 << k, Mathf.Max(d, World.nearClipPlane), World.farClipPlane);
                        Color32[] px = ReadBack();
                        for (int y = 0; y < box.height; y++)
                        {
                            for (int x = 0; x < box.width; x++)
                            {
                                int sx = box.x + x, sy = box.y + y;
                                if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                                bool here = FlatOwner(px[sy * W + sx]) == k;
                                int di = y * box.width + x;
                                if (front) { if (here && float.IsNaN(depth[di])) depth[di] = d; }
                                else if (here) depth[di] = d;
                            }
                        }
                    }
                }
                finally
                {
                    mats[k].SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                }
                return depth;
            }

            public void Move(int k, Vector3 offset)
            {
                Transform t = Views[k];
                if (t == null) return;
                if (!oldPos.ContainsKey(t)) oldPos[t] = t.localPosition;
                t.localPosition = oldPos[t] + offset;
                soloDepth[k] = null;
            }

            public void PutBack(int k)
            {
                Transform t = Views[k];
                if (t != null && oldPos.TryGetValue(t, out Vector3 p)) t.localPosition = p;
                soloDepth[k] = null;
            }

            private Color32[] ReadBack()
            {
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(Roi.x, Roi.y, W, H), 0, 0, false);
                read.Apply(false);
                RenderTexture.active = was;
                return read.GetPixels32();
            }

            public byte[] Crop(Color32[] px, RectInt c, Func<Color32, Color32> map)
            {
                int cw = Math.Max(1, c.width), ch = Math.Max(1, c.height);
                var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                var outPx = new Color32[cw * ch];
                for (int y = 0; y < c.height; y++)
                {
                    for (int x = 0; x < c.width; x++)
                    {
                        int sx = c.x + x, sy = c.y + y;
                        outPx[y * cw + x] = sx >= 0 && sy >= 0 && sx < W && sy < H ? map(px[sy * W + sx]) : new Color32(0, 0, 0, 0);
                    }
                }
                tex.SetPixels32(outPx);
                tex.Apply(false);
                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                return png;
            }

            public void Dispose()
            {
                foreach (var kv in oldPos)
                {
                    if (kv.Key != null) kv.Key.localPosition = kv.Value;
                }
                oldPos.Clear();
                RestoreMaterials();
                swapped = false;
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); rt = null; }
                if (depthRt != null) { depthRt.Release(); UnityEngine.Object.Destroy(depthRt); depthRt = null; }
                if (floatRt != null) { floatRt.Release(); UnityEngine.Object.Destroy(floatRt); floatRt = null; }
                if (readF != null) { UnityEngine.Object.Destroy(readF); readF = null; }
                if (read != null) { UnityEngine.Object.Destroy(read); read = null; }
                for (int k = 0; k < mats.Length; k++)
                {
                    if (mats[k] != null) { UnityEngine.Object.Destroy(mats[k]); mats[k] = null; }
                }
                if (go != null) { UnityEngine.Object.Destroy(go); go = null; }
            }
        }

        // ------------------------------------------------------------------ measures

        internal sealed class ItemMeasure
        {
            public int Solo, Visible, ByCart;
            public RectInt Bounds;
            public int[] CoveredBy = new int[Cart];
            public int GapCols, GapMax, GapSum, UnderCols; // the air under an upper item: columns with a gap, the largest, the sum; columns with anything under it
        }

        // Every item's outline (solo[k]) and what shows with all (and the cart) drawn; bounds in region pixels.
        internal static ItemMeasure[] Measure(Session s, int[][] solo, int[] all, int[] layers)
        {
            var m = new ItemMeasure[s.N];
            for (int k = 0; k < s.N; k++)
            {
                var im = new ItemMeasure();
                int x0 = s.W, y0 = s.H, x1 = -1, y1 = -1;
                for (int i = 0; i < solo[k].Length; i++)
                {
                    if (solo[k][i] != k) continue;
                    im.Solo++;
                    int x = i % s.W, y = i / s.W;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                    int o = all[i];
                    if (o == k) im.Visible++;
                    else if (o == Cart) im.ByCart++;
                    else if (o >= 0 && o < Cart) im.CoveredBy[o]++;
                }
                im.Bounds = x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                if (layers[k] > 0 && x1 >= 0) Gap(s, solo, layers, k, im);
                m[k] = im;
            }
            return m;
        }

        // The air under upper item k, column by column: from its lowest outline pixel down to the highest outline pixel
        // of an item on a lower layer (rows run upward). Only columns with something under it count.
        private static void Gap(Session s, int[][] solo, int[] layers, int k, ItemMeasure im)
        {
            for (int x = im.Bounds.x; x < im.Bounds.x + im.Bounds.width; x++)
            {
                int bottom = -1;
                for (int y = im.Bounds.y; y < im.Bounds.y + im.Bounds.height; y++)
                {
                    if (solo[k][y * s.W + x] == k) { bottom = y; break; }
                }
                if (bottom < 0) continue;
                int top = -1;
                for (int y = bottom; y >= 0 && top < 0; y--)
                {
                    for (int j = 0; j < s.N; j++)
                    {
                        if (layers[j] < layers[k] && solo[j][y * s.W + x] == j) { top = y; break; }
                    }
                }
                if (top < 0) continue;
                im.UnderCols++;
                int gap = Math.Max(0, bottom - top - 1);
                if (gap > 0)
                {
                    im.GapCols++;
                    im.GapSum += gap;
                    if (gap > im.GapMax) im.GapMax = gap;
                }
            }
        }

        // Pixels of a's outline covered by b, and of b's covered by a, with only the two drawn: both non-zero is a
        // visible cut-in (neither is wholly in front of the other where they overlap).
        internal static void Mutual(int[] pair, int[] soloA, int[] soloB, int a, int b, out int aByB, out int bByA)
        {
            aByB = bByA = 0;
            for (int i = 0; i < pair.Length; i++)
            {
                if (soloA[i] == a && pair[i] == b) aByB++;
                if (soloB[i] == b && pair[i] == a) bByA++;
            }
        }

        internal static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        internal static string V(Vector3 v) => $"{F(v.x)} {F(v.y)} {F(v.z)}";
        internal static string M(Matrix4x4 m)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) sb.Append(c == 0 && r == 0 ? "" : " ").Append(F(m[r, c]));
            return sb.ToString();
        }
    }
}
