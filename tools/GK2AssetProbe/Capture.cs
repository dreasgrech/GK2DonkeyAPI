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
                Camera cam = Camera.main;
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

        // Screen pixels per world unit at a point, measured through the camera (the game may draw at its own size).
        private static float UnitPixels(Camera cam, Vector3 at)
        {
            Vector3 a = cam.WorldToViewportPoint(at), b = cam.WorldToViewportPoint(at + cam.transform.up);
            return Mathf.Abs(b.y - a.y) * Screen.height;
        }
    }
}
