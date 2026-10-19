using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // A dev export of the skins as the game built them (docs/sprite-tools.md, the review page): every loaded skin's
    // pictures read back from the texture the game draws from (the sheet after the erase compose, a mod's own sprites),
    // one PNG per frame key, and the donkey's own frames as the game places them on the canvas. The review page
    // (tools/skin_preview_html.py) shows these in place of its own composite and counts where the two differ, so the
    // page and the game can't drift apart unnoticed. The player's colours are the shader's and are not in these files.
    internal static partial class Looks
    {
        internal const string ExportDirName = "game-skins";

        // Writes <folder>\<skin id>\<key>.png for every loaded skin (worn by someone since the start) and
        // <folder>\donkey\<key>.png for the base's frames; returns what was written, or the error.
        internal static string ExportBuilt(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var lines = new List<string> { "exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " by GK2 Donkey API " + Plugin.Version };
                // the base's frames, as the recipes and the erase compose read them (ReadFrame: the canvas at the trim offset)
                SkinBase b = Donkey;
                FindGameFrames(b, b.GamePrefix);
                string donkeyDir = Path.Combine(folder, "donkey");
                Directory.CreateDirectory(donkeyDir);
                int donkeyN = 0;
                foreach (var kv in gameFrames)
                {
                    SkinFrame f = ReadFrame(kv.Key, kv.Value, b);
                    if (f == null) continue;
                    WritePng(Path.Combine(donkeyDir, kv.Key + ".png"), f.P, f.W, f.H);
                    donkeyN++;
                }
                lines.Add($"donkey: {donkeyN} frames (the game's own, on the {b.CanvasW}x{b.CanvasH} canvas at their trim offset)");
                int skins = 0;
                foreach (Loaded l in loaded.Values)
                {
                    if (!l.Ready || l.Frames.Count == 0) continue;
                    string dir = Path.Combine(folder, l.Def.Id);
                    Directory.CreateDirectory(dir);
                    Directory.CreateDirectory(Path.Combine(dir, "front"));
                    int n = 0;
                    var whole = new List<string>();
                    foreach (var kv in l.Frames)
                    {
                        Sprite s = kv.Value;
                        if (s == null || !s) continue;
                        Color32[] px = ReadSprite(s, out int w, out int h);
                        if (px == null) continue;
                        string key = kv.Key;
                        string path = key.StartsWith(FrontKey, StringComparison.Ordinal) ? Path.Combine(dir, "front", key.Substring(FrontKey.Length) + ".png") : Path.Combine(dir, key + ".png");
                        WritePng(path, px, w, h);
                        n++;
                        if (l.Whole.Contains(s)) whole.Add(key);
                    }
                    skins++;
                    lines.Add($"{l.Def.Id}: {n} pictures, layer {(l.Def.Over ? "over" : "replace")}, canvas {l.Def.CanvasW}x{l.Def.CanvasH}" + (whole.Count > 0 ? $", built whole over the donkey: {whole.Count} ({string.Join(" ", whole)})" : ""));
                }
                File.WriteAllLines(Path.Combine(folder, "manifest.txt"), lines);
                string summary = $"{skins} skin(s) and the donkey's {donkeyN} frames written to {folder}";
                Plugin.Log.LogInfo("Looks: export: " + summary + " (" + string.Join("; ", lines) + ")");
                return summary;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Looks: the export failed: " + e.Message);
                return "failed: " + e.Message;
            }
        }

        // A sprite's pixels (bottom row first) read back from its texture through a blit, since a sheet is not readable.
        private static Color32[] ReadSprite(Sprite s, out int w, out int h)
        {
            Rect r = s.textureRect;
            w = Mathf.RoundToInt(r.width);
            h = Mathf.RoundToInt(r.height);
            Texture2D tex = s.texture;
            if (tex == null || w <= 0 || h <= 0) return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D tmp = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                return tmp.GetPixels32();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (tmp != null) Object.Destroy(tmp);
            }
        }

        private static void WritePng(string path, Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                t.SetPixels32(px);
                t.Apply(false);
                File.WriteAllBytes(path, t.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(t);
            }
        }
    }
}
