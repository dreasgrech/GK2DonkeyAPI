using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // A dev export of the skins as the game built them (docs/sprite-tools.md, the review page): every loaded skin's
    // pictures read back from the texture the game draws from (the sheet after the erase compose, a mod's own sprites),
    // one PNG per frame key, and the donkey's own frames as the game places them on the canvas. The review page
    // (tools/skins/skin_preview_html.py) shows these in place of its own composite and counts where the two differ, so the
    // page and the game can't drift apart unnoticed. ExportEverySkin builds every skin first, for the whole page at once. The player's colours are the shader's and are not in these files,
    // nor the donkey's repainted eye and nostril pixels (FacePixels): the donkey's frames and a skin's whole frames are
    // written as the game draws the donkey, the page composing over the asset probe's frames.
    internal static partial class Looks
    {
        internal const string ExportDirName = "game-skins";

        // Writes <folder>\<skin id>\<key>.png for every loaded skin (worn by someone at this moment) and
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
                    SkinFrame f = ReadFrame(kv.Key, kv.Value, b, FacePixels.Read.AsDrawn);
                    if (f == null) continue;
                    WritePng(Path.Combine(donkeyDir, kv.Key + ".png"), f.P, f.W, f.H);
                    donkeyN++;
                }
                lines.Add($"donkey: {donkeyN} frames (the game's own, on the {b.CanvasW}x{b.CanvasH} canvas at their trim offset)");
                int skins = 0;
                foreach (Loaded l in loaded.Values)
                {
                    if (!l.Ready || l.Frames.Count == 0) continue;
                    string dir = Path.Combine(folder, l.Def.Id.Replace('/', Path.DirectorySeparatorChar)); // game-skins\<mount>\<skin>\, sorted by mount (the donkey's own frames sit in donkey\ itself)
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
                        if (l.Def.Over && l.Def.Base == Donkey && l.Whole.Contains(s)) FacePixels.Apply(key, px, w, h, FacePixels.Read.AsDrawn); // the donkey in it as drawn
                        string path = key.StartsWith(FrontKey, StringComparison.Ordinal) ? Path.Combine(dir, "front", key.Substring(FrontKey.Length) + ".png") : Path.Combine(dir, key + ".png");
                        WritePng(path, px, w, h);
                        n++;
                        if (l.Whole.Contains(s)) whole.Add(key);
                    }
                    skins++;
                    lines.Add($"{l.Def.Id}: {n} pictures, layer {(l.Def.Over ? "over" : l.Def.OverDefault ? "over its default" : "replace")}, canvas {l.Def.CanvasW}x{l.Def.CanvasH}" + (whole.Count > 0 ? $", built whole over the donkey: {whole.Count} ({string.Join(" ", whole)})" : "")
                        + (l.Layered ? $", built on {l.Parent.Id}: {l.OwnPictures} own, {l.Inherited} from it, {l.Composed} drawn over it" : "")); // a variant's frames by where they came from (LooksInherit.cs)
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

        // Every skin registered, built and written (the user, 2026-11-05: the button wrote only the skins worn at that
        // moment, one mount at a time, "we need something that will export all of them"): each skin that can be built now
        // is held (Acquire: loaded and built a few frames a tick, as for a wearer), waited for until ready (two minutes
        // at most), written with ExportBuilt with everything else loaded, and let go (unloaded unless someone wears it).
        // Left out, and said: a skin set aside (its game frames gone), one built from the base's frames while they aren't
        // in memory (the main menu), a game-source one before the game is loaded. done gets ExportBuilt's line and those.
        internal static IEnumerator ExportEverySkin(string folder, Action<string> done)
        {
            var held = new List<Loaded>();
            var left = new List<string>();
            foreach (SkinDef d in new List<SkinDef>(All))
            {
                string why = d.Unavailable != null ? "set aside: " + d.Unavailable
                    : NeedsBaseFrames(d) && !BaseFramesInMemory(d.Base, d.Base.GamePrefix) ? "its base's frames are not in memory"
                    : d.Game != null && !Mount.IsGameReady ? "a game-source skin before the game is loaded" : null;
                if (why != null)
                {
                    left.Add($"{d.Id} ({why})");
                    continue;
                }
                try
                {
                    held.Add(Acquire(d, d.Base.GamePrefix));
                }
                catch (Exception e)
                {
                    left.Add($"{d.Id} ({e.Message})");
                }
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < until && held.Exists(l => !l.Ready)) yield return null;
            var notReady = new List<string>();
            foreach (Loaded l in held) if (!l.Ready) notReady.Add(l.Def.Id);
            string result = ExportBuilt(folder);
            foreach (Loaded l in held) Release(l);
            if (left.Count > 0) result += "; left out: " + string.Join(", ", left.ToArray());
            if (notReady.Count > 0) result += "; not built in two minutes, not written: " + string.Join(", ", notReady.ToArray());
            Plugin.Log.LogInfo($"Looks: export of every skin: {held.Count} held and built, {result}.");
            done?.Invoke(result);
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
