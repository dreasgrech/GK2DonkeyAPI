using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The settings' picture of the donkey with its cart (LookPreview, LookBar), drawn by the game itself: a copy of the
    // donkey's look from the game's files (Assets/AddressableAssets/WGOs/<id>.prefab, the file the game draws the
    // donkey from) far below the world, wearing a paper donkey's colours and lantern (its Colors.Rig and Lantern.Rig
    // attached to the copy), photographed into a RenderTexture by a camera of its own at the game camera's angle.
    // - Two pictures of the one copy, each with its own camera and framing (View): the donkey with its cart, and the
    //   donkey alone (every renderer but the donkey's own sprites switched off for that photo only, then on again:
    //   not a list of the cart's parts, since the lantern's hanging lamp is made later, on its first tick, and was
    //   missed that way). One copy, so the colour and lantern rigs, which hold one root each, dress both.
    // - Kept apart by distance, not by layer: the game names all 32 layers (found 2026-10-02), so the copy keeps its
    //   own; the game's cameras never look 5000 units down, and ours see nothing else there.
    // - The copy keeps only what draws (transforms, mesh and sprite renderers, the game's Object3DMesh, which sets up
    //   the 3D cart's materials on its own) and its animator, walking on the spot, with its events off. Every other
    //   script, collider, light, sound is removed before the copy is ever switched on. The game's file stays loaded
    //   while the copy exists (released, the copy's meshes went with it).
    // - The cart's parts on as in Cart style, the loaded-cart body and the effect helpers off.
    // - A point light of its own with a short reach: lit the same in the main menu and at night, the world untouched.
    // - Each picture framed on what's drawn (Frame, then Tighten on the read-back pixels), again whenever it's changed
    //   or something reaches the edge.
    // Any failure: the stage is taken down for the session, logged once, and the picture is the donkey alone
    // (Colors.BuildPreview) or nothing. The game is never touched.
    internal static class PreviewStage
    {
        private const float GameCameraPitch = 53.130104f; // MainCamera.cs

        // The prefab's helpers that only feed the game's effects: the shadow-casting sprite and cylinder, and the mask
        // that bends the grass (7.68 units wide: it had made the picture tiny).
        private static readonly string[] Helpers = { "gfx (90 rotated)", "Cylinder Shadowcaster", "DeformTextureSprite" };

        private static readonly string[] CartPartNames =
        {
            "wheel_left", "wheel_right", "light_sway",
            "donkey_cart_wheel_f", "donkey_cart_wheel_b", "donkey_cart_bars",
            "donkey_cart_empty_sway", "donkey_cart_lantern_sway",
        };

        // One picture: its camera, its texture, its framing.
        private sealed class View
        {
            internal readonly bool WithCart;
            internal readonly string Name;
            internal Camera Cam;
            internal RenderTexture Rt;
            internal bool Reframe = true;
            internal float NextEdgeCheck, LastAsked = -10f;
            internal int LastRenderFrame = -1;

            internal View(bool withCart)
            {
                WithCart = withCart;
                Name = withCart ? "with the cart" : "the donkey alone";
            }
        }

        private static readonly View cartView = new View(true), donkeyView = new View(false);
        private static readonly View[] views = { cartView, donkeyView };

        private static GameObject stage, copy;
        private static Light lamp;
        private static Animator animator;
        private static bool failed, loadStarted, described;
        private static int framesSinceBuilt, lastPoseFrame = -1;
        private static Mount.Instance dressedAs;
        private static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> loading;
        private const int SettleFrames = 8;
        private static Mount.Instance shownPaper;
        private static float ppu = 50f;
        private static readonly List<Renderer> donkeyRenderers = new List<Renderer>(); // its body and head sprites
        private static readonly List<Renderer> allRenderers = new List<Renderer>();
        private static readonly List<Renderer> switchedOff = new List<Renderer>();

        // Picture pixels per art pixel. The walk animation rocks the cart body by up to 3 degrees; at one picture pixel
        // per art pixel that tilt snaps into a one-pixel step across the middle of the cart (it looked split in two).
        // The game draws the cart at screen resolution, where the tilt is smooth: so does the picture, at the size it's
        // shown (three canvas units per art pixel at most).
        internal const int Detail = 3;

        // The picture's size in art pixels (the CPU stand-in is drawn at one pixel per art pixel).
        internal static Vector2 ArtSize(Texture pic)
        {
            float d = pic == cartView.Rt || pic == donkeyView.Rt ? Detail : 1f;
            return new Vector2(pic.width / d, pic.height / d);
        }

        // The pictures won't come this session: show the donkey alone (Colors.BuildPreview) instead.
        internal static bool Failed => failed;

        // Every frame (MountRegistry.Tick): each picture asked for a moment ago is drawn again, so the walk plays
        // smoothly between the displays' four-a-second updates.
        internal static void Tick()
        {
            if (shownPaper == null || failed || copy == null)
            {
                return;
            }
            foreach (View v in views)
            {
                if (Time.unscaledTime - v.LastAsked < 0.5f)
                {
                    Picture(shownPaper, v.WithCart, ask: false);
                }
            }
        }

        // The picture of the paper donkey, with its cart or alone, or null (not ready yet, or failed: use the donkey
        // alone from Colors.BuildPreview).
        internal static Texture Picture(Mount.Instance paper, bool withCart = true, bool ask = true)
        {
            if (failed || paper == null)
            {
                return null;
            }
            View v = withCart ? cartView : donkeyView;
            if (ask)
            {
                v.LastAsked = Time.unscaledTime;
                shownPaper = paper;
            }
            try
            {
                if (copy == null)
                {
                    Build();
                    return null;
                }
                if (dressedAs != paper)
                {
                    dressedAs = paper;
                    // Bound, so the first build reads the paper donkey's look: attached unbound, the picture showed the
                    // game's own colours until a setting changed (the video of 2026-10-02 20-57).
                    using (Mount.Bind(paper))
                    {
                        paper.ColorsRig.AttachRoot(copy);
                        paper.ColorsRig.MarkDirty();
                        paper.LanternRig.AttachRoot(copy.transform); // the pole where the tuning puts it, the lamp hanging
                        paper.LooksRig.AttachRoot(copy);              // the skin's overlays count as the donkey's sprites
                        donkeyRenderers.RemoveAll(r => r == null);
                        donkeyRenderers.AddRange(paper.LooksRig.Renderers);
                    }
                    foreach (View each in views)
                    {
                        each.Reframe = true;
                    }
                }
                // The copy is posed once a frame, for both pictures.
                bool newFrame = Time.frameCount != lastPoseFrame;
                if (newFrame)
                {
                    lastPoseFrame = Time.frameCount;
                }
                // Settled before it's first framed and shown: Object3DMesh sets its materials on Start, the lamp is hung on
                // the lantern's first tick, and the walk starts from the idle pose. Framed earlier, the picture changed
                // size a moment after it first showed (2026-10-02).
                if (framesSinceBuilt < SettleFrames)
                {
                    if (newFrame)
                    {
                        if (framesSinceBuilt >= 1)
                        {
                            Pose(paper);
                        }
                        framesSinceBuilt++;
                    }
                    return null;
                }
                if (!described)
                {
                    described = true;
                    Describe("after start");
                }
                if (newFrame)
                {
                    Pose(paper);
                }
                if (Time.frameCount != v.LastRenderFrame)
                {
                    v.LastRenderFrame = Time.frameCount;
                    Render(v);
                }
                return v.Rt;
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return null;
            }
        }

        // One photo: for the donkey alone, everything but the donkey's sprites is off just for it (and on again after,
        // also on a failure).
        private static void Render(View v)
        {
            switchedOff.Clear();
            if (!v.WithCart)
            {
                copy.GetComponentsInChildren(false, allRenderers);
                foreach (Renderer r in allRenderers)
                {
                    if (r != null && r.enabled && !donkeyRenderers.Contains(r))
                    {
                        r.enabled = false;
                        switchedOff.Add(r);
                    }
                }
            }
            try
            {
                if (v.Reframe)
                {
                    v.Reframe = false;
                    Frame(v);
                    v.Cam.Render();
                    Tighten(v);
                }
                v.Cam.Render();
                if (Time.unscaledTime >= v.NextEdgeCheck)
                {
                    v.NextEdgeCheck = Time.unscaledTime + 1f;
                    if (TouchesEdge(v))
                    {
                        v.Reframe = true; // something reached the edge (the lamp hung, a setting changed): frame again
                    }
                }
            }
            finally
            {
                foreach (Renderer r in switchedOff)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                    }
                }
                switchedOff.Clear();
            }
        }

        // The pose for this frame: walking on the spot, then the lantern (after the animator: this runs in the
        // plugin's LateUpdate) and the colours.
        private static void Pose(Mount.Instance paper)
        {
            if (animator != null)
            {
                // Walking on the spot, facing left: the game's own parameters (AnimationComponentBase: State 1 = Walk,
                // Direction 0 right / -180 left, walk_speed 1 = the clip's own pace).
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.name == "Direction" && p.type == AnimatorControllerParameterType.Float) animator.SetFloat("Direction", -180f);
                    else if (p.name == "State" && p.type == AnimatorControllerParameterType.Int) animator.SetInteger("State", 1);
                    else if (p.name == "walk_speed" && p.type == AnimatorControllerParameterType.Float) animator.SetFloat("walk_speed", 1f);
                }
            }
            using (Mount.Bind(paper))
            {
                paper.LanternRig.Tick();
                paper.ColorsRig.Tick(); // rebuilt only when the look changed (MountCompanion marks it)
                paper.LooksRig.Tick();
                paper.LooksRig.ApplySafe(); // the stage's cameras are rendered by hand: not covered by the per-frame apply
            }
        }

        private static void Fail(string why)
        {
            failed = true;
            Plugin.Log.LogInfo("Settings: the picture with the cart is off for this session (" + why + "); the donkey alone is shown.");
            try
            {
                dressedAs?.ColorsRig.Detach();
                dressedAs?.LanternRig.Detach();
                dressedAs?.LooksRig.Detach();
                if (loading.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(loading);
                }
                if (stage != null)
                {
                    UnityEngine.Object.Destroy(stage);
                }
                foreach (View v in views)
                {
                    if (v.Rt != null)
                    {
                        v.Rt.Release();
                        UnityEngine.Object.Destroy(v.Rt);
                    }
                    v.Rt = null;
                    v.Cam = null;
                }
            }
            catch
            {
            }
            stage = copy = null;
            dressedAs = null;
        }

        private static void Build()
        {
            if (!loadStarted)
            {
                loadStarted = true;
                loading = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("Assets/AddressableAssets/WGOs/" + Plugin.MountWgoId.Value + ".prefab");
                return;
            }
            if (!loading.IsValid() || !loading.IsDone)
            {
                return;
            }
            GameObject prefab = loading.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? loading.Result : null;
            if (prefab == null)
            {
                Fail("the donkey's look isn't in the game's files");
                return;
            }

            // Off while it's made, so no script of the copy ever wakes.
            stage = new GameObject("GK2DonkeyAPI preview stage");
            stage.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(stage);
            stage.transform.position = new Vector3(0f, -5000f, 0f);
            copy = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
            copy.name = "GK2DonkeyAPI preview copy";
            Strip(copy);
            animator = copy.GetComponentInChildren<Animator>(true);
            donkeyRenderers.Clear();
            SpriteRenderer body = null;
            int cartParts = 0;
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                if (Array.IndexOf(CartPartNames, t.name) >= 0)
                {
                    t.gameObject.SetActive(true);
                    cartParts++;
                }
                else if (t.name == "donkey_cart_body_sway")
                {
                    t.gameObject.SetActive(false); // the loaded cart (the corpse shroud)
                }
                else if (Array.IndexOf(Helpers, t.name) >= 0)
                {
                    t.gameObject.SetActive(false); // drawn for the game's effects, not to be seen
                }
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null && t.name == "hed")
                {
                    // The head sprite is for turning the head; the game's HeadSpriteComponentMarker (stripped) keeps it
                    // in step. The walk frames of the body draw the head already: left on, it floated beside it.
                    sr.enabled = false;
                }
                if (sr != null && (t.name == "bdy" || t.name == "hed"))
                {
                    donkeyRenderers.Add(sr);
                    if (t.name == "bdy")
                    {
                        body = sr;
                    }
                }
            }
            if (body == null)
            {
                Fail("no donkey body in the copy");
                return;
            }
            ppu = body.sprite != null ? body.sprite.pixelsPerUnit : 50f;
            stage.SetActive(true); // the copy's Object3DMesh starts: its materials are set by the next frame

            foreach (View v in views)
            {
                var camGo = new GameObject("GK2DonkeyAPI preview camera (" + v.Name + ")");
                camGo.transform.SetParent(stage.transform, false);
                v.Cam = camGo.AddComponent<Camera>();
                v.Cam.enabled = false;
                v.Cam.orthographic = true;
                v.Cam.cullingMask = ~(1 << 5); // everything but UI: there's nothing but the copy down here
                v.Cam.clearFlags = CameraClearFlags.SolidColor;
                v.Cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                v.Reframe = true;
            }

            var lightGo = new GameObject("GK2DonkeyAPI preview light");
            lightGo.transform.SetParent(stage.transform, false);
            lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = 1f;
            lamp.color = Color.white;

            framesSinceBuilt = 0;
            described = false;
            Frame(cartView);
            Plugin.Log.LogInfo($"Settings: the pictures are set up ({cartParts} cart parts on, {donkeyRenderers.Count} donkey sprites, {ppu} pixels per unit).");
            Describe("built");
        }

        // The camera at the game's angle, framed wide on every shown part's bounds (then Tighten crops to what's drawn).
        // The light is placed by the cart picture's framing only, so both pictures are lit the same.
        private static void Frame(View v)
        {
            Transform ct = v.Cam.transform;
            ct.rotation = Quaternion.Euler(GameCameraPitch, 0f, 0f);
            Vector3 forward = ct.forward;
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled)
                {
                    continue;
                }
                foreach (Vector3 corner in Corners(r.bounds))
                {
                    Vector3 c = ct.InverseTransformDirection(corner - stage.transform.position);
                    if (!any)
                    {
                        b = new Bounds(c, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(c);
                    }
                }
            }
            if (!any || b.size.x <= 0f || b.size.y <= 0f)
            {
                throw new InvalidOperationException("nothing to frame");
            }
            float density = ppu * Detail;
            int w = Mathf.Clamp(Mathf.CeilToInt(b.size.x * density) + 8 * Detail, 16, 768 * Detail);
            int h = Mathf.Clamp(Mathf.CeilToInt(b.size.y * density) + 8 * Detail, 16, 768 * Detail);
            v.Cam.orthographicSize = h / density / 2f;
            v.Cam.aspect = (float)w / h;
            Vector3 centre = stage.transform.position + ct.TransformDirection(new Vector3(b.center.x, b.center.y, 0f));
            ct.position = centre - forward * (b.extents.z + 50f);
            v.Cam.nearClipPlane = 0.01f;
            v.Cam.farClipPlane = b.size.z + 100f;
            NewTexture(v, w, h);
            if (v.WithCart)
            {
                lamp.transform.position = centre - forward * 4f;
                lamp.range = Mathf.Max(b.size.x, b.size.y) * 3f + 8f; // the stage only: the world is 5000 units away
            }
        }

        private static void NewTexture(View v, int w, int h)
        {
            if (v.Rt != null && v.Rt.width == w && v.Rt.height == h)
            {
                return;
            }
            v.Cam.targetTexture = null;
            if (v.Rt != null)
            {
                v.Rt.Release();
                UnityEngine.Object.Destroy(v.Rt);
            }
            v.Rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "GK2DonkeyAPI preview (" + v.Name + ")" };
            v.Rt.Create();
            v.Cam.targetTexture = v.Rt;
        }

        private static Color32[] ReadBack(View v)
        {
            RenderTexture prev = RenderTexture.active;
            Texture2D t = null;
            try
            {
                RenderTexture.active = v.Rt;
                t = new Texture2D(v.Rt.width, v.Rt.height, TextureFormat.RGBA32, mipChain: false);
                t.ReadPixels(new Rect(0, 0, v.Rt.width, v.Rt.height), 0, 0, false);
                return t.GetPixels32();
            }
            finally
            {
                RenderTexture.active = prev;
                if (t != null)
                {
                    UnityEngine.Object.Destroy(t);
                }
            }
        }

        // Is anything drawn on the picture's outermost pixels? Then it was cut off.
        private static bool TouchesEdge(View v)
        {
            Color32[] px = ReadBack(v);
            int w = v.Rt.width, h = v.Rt.height;
            for (int x = 0; x < w; x++)
            {
                if (px[x].a > 8 || px[(h - 1) * w + x].a > 8) return true;
            }
            for (int y = 0; y < h; y++)
            {
                if (px[y * w].a > 8 || px[y * w + w - 1].a > 8) return true;
            }
            return false;
        }

        // Onto what was drawn: the camera moved and narrowed onto the non-empty pixels, a margin round.
        private static void Tighten(View v)
        {
            Color32[] px = ReadBack(v);
            int w0 = v.Rt.width, h0 = v.Rt.height;
            int minX = w0, minY = h0, maxX = -1, maxY = -1;
            for (int y = 0; y < h0; y++)
            {
                for (int x = 0; x < w0; x++)
                {
                    if (px[y * w0 + x].a > 8)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0)
            {
                Plugin.Log.LogInfo($"[preview] the picture {v.Name} is empty; framing left wide.");
                return;
            }
            const int margin = 6 * Detail; // room for the walk's bob and the lamp's swing
            minX = Math.Max(0, minX - margin); minY = Math.Max(0, minY - margin);
            maxX = Math.Min(w0 - 1, maxX + margin); maxY = Math.Min(h0 - 1, maxY + margin);
            int w = maxX - minX + 1, h = maxY - minY + 1;
            float unitsPerPixel = v.Cam.orthographicSize * 2f / h0;
            float dx = ((minX + maxX + 1) / 2f - w0 / 2f) * unitsPerPixel;
            float dy = ((minY + maxY + 1) / 2f - h0 / 2f) * unitsPerPixel;
            Transform ct = v.Cam.transform;
            ct.position += ct.right * dx + ct.up * dy;
            NewTexture(v, w, h);
            v.Cam.orthographicSize = h * unitsPerPixel / 2f;
            v.Cam.aspect = (float)w / h;
            Plugin.Log.LogInfo($"[preview] {v.Name}: framed on what's drawn, {w} x {h} pixels (from {w0} x {h0}).");
        }

        // ------------------------------------------------------------------ the log

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null && p != stage.transform; p = p.parent)
            {
                parts.Insert(0, p.name);
            }
            return string.Join("/", parts);
        }

        // Everything the picture is made of (the log's [preview] lines): what draws, where, with what.
        private static void Describe(string when)
        {
            try
            {
                Camera cam = cartView.Cam;
                RenderTexture rt = cartView.Rt;
                Transform ct = cam.transform;
                Plugin.Log.LogInfo($"[preview] {when}: camera at {ct.position}, angle {ct.eulerAngles}, ortho size {cam.orthographicSize:0.###}, aspect {cam.aspect:0.###}, image {rt.width}x{rt.height}; animator {(animator != null ? animator.runtimeAnimatorController?.name ?? "no controller" : "none")}.");
                int n = 0, on = 0;
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    n++;
                    bool shown = r.enabled && r.gameObject.activeInHierarchy;
                    if (shown) on++;
                    Bounds wb = r.bounds;
                    Vector3 c = ct.InverseTransformPoint(wb.center);
                    string what = r.GetType().Name;
                    if (r is SpriteRenderer sr)
                    {
                        what += $" sprite={(sr.sprite != null ? sr.sprite.name : "none")} flipX={sr.flipX}";
                    }
                    else
                    {
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        Mesh m = mf != null ? mf.sharedMesh : null;
                        what += $" mesh={(m != null ? m.name + " (" + m.vertexCount + " verts)" : "none")}";
                    }
                    Plugin.Log.LogInfo($"[preview]   {(shown ? "on " : "off")} {PathOf(r.transform)}: {what}; local {r.transform.localPosition}; world centre {wb.center} size {wb.size}; to the camera {c.x:0.##},{c.y:0.##} depth {c.z:0.##}.");
                }
                Plugin.Log.LogInfo($"[preview] {on} of {n} renderers on.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("[preview] describing failed: " + e.Message);
            }
        }

        // Every component but what draws (and the animator), children first and scripts before what they need.
        private static void Strip(GameObject root)
        {
            var removed = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SpriteRenderer || c is Object3DMesh || c is Animator)
                    {
                        continue;
                    }
                    if (pass == 0 && !(c is MonoBehaviour))
                    {
                        continue; // scripts first: they may require the others
                    }
                    string type = c.GetType().Name;
                    UnityEngine.Object.DestroyImmediate(c);
                    removed[type] = removed.TryGetValue(type, out int k) ? k + 1 : 1;
                }
            }
            // The animator stays: it gives the parts the game's own idle pose. Its events are off, so no clip can call
            // into the game, and it doesn't move the copy.
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
            {
                a.fireEvents = false;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var list = new List<string>();
            foreach (var kv in removed)
            {
                list.Add(kv.Key + " " + kv.Value);
            }
            Plugin.Log.LogInfo("[preview] removed from the copy: " + (list.Count == 0 ? "nothing" : string.Join(", ", list)) + ".");
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            yield return new Vector3(mn.x, mn.y, mn.z);
            yield return new Vector3(mx.x, mn.y, mn.z);
            yield return new Vector3(mn.x, mx.y, mn.z);
            yield return new Vector3(mx.x, mx.y, mn.z);
            yield return new Vector3(mn.x, mn.y, mx.z);
            yield return new Vector3(mx.x, mn.y, mx.z);
            yield return new Vector3(mn.x, mx.y, mx.z);
            yield return new Vector3(mx.x, mx.y, mx.z);
        }
    }
}
