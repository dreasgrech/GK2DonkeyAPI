using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2AssetProbe
{
    // Records the donkey as the game draws it, rider, lighting and effects included: a few seconds of the screen around
    // it, read at the end of each frame (after every camera and the GUI), saved as PNGs in captures\<time>\. The probe's
    // window is hidden while it records. tools/skin_preview_html.py plays the captures next to the skin previews, so a
    // look can be judged with the keeper seated (2026-10-17: does the rider's leg clash with the dragon's wing?).
    // The crop is fixed in size for the whole recording and centred on the donkey every frame, so the donkey stays still
    // in the picture and only its own motion shows.
    internal static class Capture
    {
        private const float Seconds = 4f;
        private const float Fps = 15f;
        internal static bool Recording { get; private set; }
        internal static string Last { get; private set; } = "";

        internal static void Start(MonoBehaviour host)
        {
            if (Recording) return;
            if (!Probe.FindDonkey()) return;
            host.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            Recording = true;
            string dir = Path.Combine(Probe.OutDir, "captures", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            int saved = 0;
            Texture2D tex = null;
            try
            {
                Directory.CreateDirectory(dir);
                // a fixed crop: the donkey's drawn height plus room above for the rider, as a square, in screen pixels
                Camera cam = Probe.WorldCam;
                Bounds b = Probe.Body.bounds;
                float pxPerUnit = UnitPixels(cam, b.center);
                int size = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(b.size.x, b.size.y + 1.6f) * 1.5f * pxPerUnit), 64, Mathf.Min(Screen.width, Screen.height));
                tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                float until = Time.realtimeSinceStartup + Seconds, next = 0f;
                Probe.Info($"capture: {Seconds} s at {Fps} a second, {size}x{size} px around the donkey -> {dir}");
                while (Time.realtimeSinceStartup < until)
                {
                    yield return new WaitForEndOfFrame();
                    if (Time.realtimeSinceStartup < next) continue;
                    next = Time.realtimeSinceStartup + 1f / Fps;
                    if (Probe.Body == null || cam == null) break;
                    // the middle of the donkey and its rider: the drawn body, raised by half the rider's room
                    Vector3 mid = Probe.Body.bounds.center + Vector3.up * 0.8f;
                    Vector3 v = cam.WorldToViewportPoint(mid);
                    int cx = Mathf.RoundToInt(v.x * Screen.width), cy = Mathf.RoundToInt(v.y * Screen.height);
                    int x = Mathf.Clamp(cx - size / 2, 0, Screen.width - size), y = Mathf.Clamp(cy - size / 2, 0, Screen.height - size);
                    tex.ReadPixels(new Rect(x, y, size, size), 0, 0, false);
                    tex.Apply(false);
                    File.WriteAllBytes(Path.Combine(dir, $"frame_{saved:000}.png"), tex.EncodeToPNG());
                    saved++;
                }
            }
            finally
            {
                if (tex != null) Object.Destroy(tex);
                Recording = false;
            }
            Last = Path.GetFileName(dir);
            Probe.Info($"capture done: {saved} pictures in captures\\{Last} (tools/skin_preview_html.py plays them)");
            Probe.Status = $"Captured {saved} pictures: captures\\{Last}.";
        }

        // The rider alone, for the preview page (tools/skin_preview_html.py lays him over every skin): a 96x96 picture in
        // the donkey frames' own pixels, centred on the donkey's frame centre, so it lines up with every skin. Rendered
        // twice through a copy of the world camera: as the game draws it, and with the keeper's pieces hidden for that
        // one render; the pixels that differ are the rider, lit as in game. Both renders happen in this one call (no
        // game code runs between them). Saved as rider.png, with the whole square as rider-scene.png to check by eye.
        internal static void ExportRider()
        {
            if (!Probe.FindDonkey()) return;
            PlayerController pc = MainGame.Instance != null ? MainGame.PlayerController : null;
            Camera world = Probe.WorldCam;
            if (pc == null || world == null)
            {
                Probe.Status = "No keeper or no camera.";
                return;
            }
            if ((pc.transform.position - Probe.Root.position).sqrMagnitude > 1.5f * 1.5f)
            {
                Probe.Status = "Ride the donkey first (the rider is exported while mounted).";
                return;
            }
            // the keeper's pieces: his own sprites, and the API's leg slice under the donkey's body
            var parts = new System.Collections.Generic.List<Renderer>();
            // not the shadow casters ("-hed", "-bdy", ...) or the grass-deform sprite: hiding them changes the ground around
            // him, which then counted as rider (stray specks in the first export, 2026-10-17)
            foreach (var r in pc.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r.enabled && r.sprite != null && !r.forceRenderingOff && !r.name.StartsWith("-") && r.name != "DeformTextureSprite") parts.Add(r);
            }
            Transform legs = Probe.Body.transform.Find("mount_rider_legs");
            if (legs != null && legs.gameObject.activeInHierarchy) foreach (var r in legs.GetComponentsInChildren<SpriteRenderer>(false)) if (r.enabled) parts.Add(r);

            const int N = 96;
            var go = new GameObject("assetprobe rider camera");
            go.SetActive(false);
            var cam = go.AddComponent<Camera>();
            RenderTexture rt = RenderTexture.GetTemporary(N, N, 24, RenderTextureFormat.ARGB32);
            Texture2D a = new Texture2D(N, N, TextureFormat.RGBA32, false), b = new Texture2D(N, N, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            try
            {
                cam.CopyFrom(world);
                cam.targetTexture = rt;
                cam.orthographic = true;
                cam.orthographicSize = N / 2f / 50f;               // 96 art pixels of 50 per unit, top to bottom
                cam.aspect = 1f;
                cam.transform.rotation = world.transform.rotation;
                cam.transform.position = Probe.Body.transform.position - world.transform.forward * 30f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 200f;
                RenderTexture.active = rt;
                cam.Render();
                a.ReadPixels(new Rect(0, 0, N, N), 0, 0, false);
                a.Apply(false);
                foreach (var r in parts) r.forceRenderingOff = true;
                try
                {
                    cam.Render();
                }
                finally
                {
                    foreach (var r in parts) if (r != null) r.forceRenderingOff = false;
                }
                b.ReadPixels(new Rect(0, 0, N, N), 0, 0, false);
                b.Apply(false);
                Color32[] pa = a.GetPixels32(), pb = b.GetPixels32();
                var rider = new Color32[pa.Length];
                int count = 0;
                for (int i = 0; i < pa.Length; i++)
                {
                    if (pa[i].r != pb[i].r || pa[i].g != pb[i].g || pa[i].b != pb[i].b)
                    {
                        rider[i] = new Color32(pa[i].r, pa[i].g, pa[i].b, 255);
                        count++;
                    }
                }
                a.Apply(false);
                File.WriteAllBytes(Path.Combine(Probe.OutDir, "rider-scene.png"), a.EncodeToPNG());
                // the frames are drawn as the donkey prefab has them; when the body is drawn mirrored on screen, the rider
                // is mirrored back, so rider.png always matches the frames whichever way the donkey faced
                bool mirrored = Probe.Body.transform.lossyScale.x < 0 != Probe.Body.flipX;
                if (mirrored)
                {
                    var flipped = new Color32[rider.Length];
                    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) flipped[y * N + x] = rider[y * N + (N - 1 - x)];
                    rider = flipped;
                }
                b.SetPixels32(rider);
                b.Apply(false);
                File.WriteAllBytes(Path.Combine(Probe.OutDir, "rider.png"), b.EncodeToPNG());
                Probe.Info($"rider exported: {count} pixels from {parts.Count} pieces ({string.Join(", ", parts.ConvertAll(r => r.name))}), facing {(mirrored ? "mirrored on screen (flipped back)" : "as drawn")}, camera {(world.orthographic ? "orthographic " + world.orthographicSize.ToString("0.###") : "perspective")} -> rider.png, rider-scene.png");
                Probe.Status = $"Rider exported ({count} pixels): rider.png.";
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(a);
                Object.Destroy(b);
                Object.Destroy(go);
            }
        }

        // Screen pixels per world unit at a point, measured through the camera (the game may draw at its own size).
        private static float UnitPixels(Camera cam, Vector3 at)
        {
            Vector3 a = cam.WorldToViewportPoint(at), b = cam.WorldToViewportPoint(at + cam.transform.up);
            return Mathf.Abs(b.y - a.y) * Screen.height;
        }
    }
}
