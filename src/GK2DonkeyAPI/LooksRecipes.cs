using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // Recipe skins (docs/skins-design.md §4.4): a rule applied to the base's own frames at runtime, so the result
    // covers every clip the game has and nothing of the game's art is shipped. The frames are read back from the
    // character atlas once per key (Graphics.Blit + ReadPixels, as Colors' preview does), put on the canvas at their
    // trim offset, run through the recipe's pixel function and put into the skin's sheet like any other picture
    // (Looks.cs AdvanceBuilds: a few frames per tick, worn once whole).
    internal static partial class Looks
    {
        // A frame on the canvas, for a recipe: the pixels (bottom-left first, as Unity's), the size, and the row of the
        // back at column 58 relative to the standing frame's (the bob: patterns that use Y - Bob ride with the body).
        internal sealed class SkinFrame
        {
            internal string Key;
            internal int W, H;
            internal Color32[] P;   // the donkey's own pixels, canvas-sized
            internal int Bob;       // this frame's back row (from the top) minus the standing frame's: positive, the body sits lower
            // Unity's rows run from the bottom, so a pattern that rides with the body uses y + Bob (the top-down prototypes used y - Bob).

            internal bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && P[y * W + x].a != 0;

            // Opaque with a transparent neighbour: the outline.
            internal bool Rim(int x, int y) => Opaque(x, y) && (!Opaque(x + 1, y) || !Opaque(x - 1, y) || !Opaque(x, y + 1) || !Opaque(x, y - 1));

            // One pixel inside the outline.
            internal bool Inner(int x, int y) => Opaque(x, y) && !Rim(x, y) && (Rim(x + 1, y) || Rim(x - 1, y) || Rim(x, y + 1) || Rim(x, y - 1));

            internal static float Lum(Color32 c) => (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;

            // A fixed pseudo-random 0..1 per cell and salt (the same every run and on every machine).
            internal static float Noise(int x, int y, int salt)
            {
                unchecked
                {
                    uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                    h ^= h >> 13;
                    h *= 0x5bd1e995;
                    h ^= h >> 15;
                    return (h & 0xff) / 255f;
                }
            }

            // A colour along stops (position 0..1, colour), at t.
            internal static Color32 Ramp(float t, params (float At, Color32 C)[] stops)
            {
                t = Mathf.Clamp01(t);
                for (int i = 0; i < stops.Length - 1; i++)
                {
                    if (stops[i].At <= t && t <= stops[i + 1].At)
                    {
                        float span = stops[i + 1].At - stops[i].At;
                        float k = span > 0f ? (t - stops[i].At) / span : 0f;
                        Color32 a = stops[i].C, b = stops[i + 1].C;
                        return new Color32((byte)(a.r + (b.r - a.r) * k), (byte)(a.g + (b.g - a.g) * k), (byte)(a.b + (b.b - a.b) * k), 255);
                    }
                }
                return stops[stops.Length - 1].C;
            }
        }

        // ------------------------------------------------------------------ the built-in recipes

        private static void RegisterRecipes()
        {
            // No built-in recipe is left: the earlier five (zebra, ghost, ember, golden, shadow) were recolours and
            // patterns, which the colour settings cover (gone 2026-10-18); the last one changed the drawing itself and was
            // code until 2026-10-19, when it became a skin pack drawn over the donkey, the erase colour and the [Base.Coat]
            // recolour doing what only code could before. Recipes stay for mods (Mounts.Looks.RegisterRecipe).
        }

        // The base's own sprites by frame key, found once from its atlases (the sprites loaded while any view of it
        // exists), the originals rather than the skin changer's "(Clone)" copies.
        private static readonly Dictionary<string, Sprite> gameFrames = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static string gamePrefix;
        private static SkinBase gameBase;

        private static void FindGameFrames(SkinBase b, string prefix, bool quiet = false)
        {
            if (gameBase == b && gamePrefix == prefix && gameFrames.Count > 0) return;
            gameFrames.Clear();
            gamePrefix = prefix;
            gameBase = b;
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null || !s.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (s.texture == null || s.texture.name.IndexOf(b.AtlasName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string key = FrameKey(s.name);
                if (!b.KeySet.Contains(key)) continue;
                bool clone = s.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0;
                if (!gameFrames.TryGetValue(key, out Sprite have) || (have.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0 && !clone))
                {
                    gameFrames[key] = s;
                }
            }
            if (!quiet || gameFrames.Count > 0) Plugin.Log.LogInfo($"Looks: the {b.Id}'s frames found for the recipes: {gameFrames.Count} of {b.Keys.Length} ('{prefix}*' in the {b.AtlasName} atlases).");
        }

        // Whether a skin is built from the base's own frames: a recipe, or an over skin that erases (WrapErase); a skin
        // built on its default when the default is (LooksInherit.cs).
        internal static bool NeedsBaseFrames(SkinDef d) => d.Recipe != null || (d.Over && d.Erase != null) || (InheritsPictures(d) && NeedsBaseFrames(d.Parent));

        // Whether the base's frames are in memory now (the game loads them with the world, not at the main menu). The
        // atlases are searched again at most every 3 s while they aren't, without a log line.
        private static float baseFramesCheckAt;

        internal static bool BaseFramesInMemory(SkinBase b, string prefix)
        {
            if (gameBase == b && gamePrefix == prefix && gameFrames.Count > 0) return true;
            if (Time.realtimeSinceStartup < baseFramesCheckAt) return false;
            baseFramesCheckAt = Time.realtimeSinceStartup + 3f;
            FindGameFrames(b, prefix, quiet: true);
            return gameFrames.Count > 0;
        }

        // The sprite's pixels on its canvas, at its trim offset (ReadPixels from a blit of its atlas).
        private static SkinFrame ReadFrame(string key, Sprite s, SkinBase b)
        {
            Texture2D tex = s.texture;
            Rect r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            int cw = Mathf.RoundToInt(s.rect.width), ch = Mathf.RoundToInt(s.rect.height);
            if (tex == null || w <= 0 || h <= 0 || cw <= 0 || ch <= 0) return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D tmp = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                Color32[] crop = tmp.GetPixels32();
                var canvas = new Color32[cw * ch];
                int ox = Mathf.FloorToInt(s.textureRectOffset.x), oy = Mathf.FloorToInt(s.textureRectOffset.y);
                for (int y = 0; y < h; y++)
                {
                    int cy = oy + y;
                    if (cy < 0 || cy >= ch) continue;
                    for (int x = 0; x < w; x++)
                    {
                        int cx = ox + x;
                        if (cx >= 0 && cx < cw) canvas[cy * cw + cx] = crop[y * w + x];
                    }
                }
                var f = new SkinFrame { Key = key, W = cw, H = ch, P = canvas };
                // the back row from the top at the base's back column: Unity's rows run from the bottom
                int backFromTop = -1;
                for (int yTop = 0; yTop < ch; yTop++)
                {
                    int yUnity = ch - 1 - yTop;
                    if (b.BackCol < cw && canvas[yUnity * cw + b.BackCol].a != 0)
                    {
                        backFromTop = yTop;
                        break;
                    }
                }
                f.Bob = backFromTop < 0 ? 0 : backFromTop - b.BackRow;
                return f;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (tmp != null) Object.Destroy(tmp);
            }
        }

        // ------------------------------------------------------------------ building into the sheet

        // The recipe run over the base's frames as they are produced (Looks.cs AdvanceBuilds, a few per tick).
        private static Producer RecipeProducer(SkinDef d, string prefix)
        {
            SkinBase b = d.Base;
            FindGameFrames(b, prefix);
            var keys = new List<string>(gameFrames.Keys);
            Func<SkinFrame, Color32[]> recipe = d.Recipe;
            Func<string, Pixels> produce = key =>
            {
                if (!gameFrames.TryGetValue(key, out Sprite s) || s == null) return new Pixels { Why = "the game's frame is not in memory" };
                SkinFrame f = ReadFrame(key, s, b);
                if (f == null) return new Pixels { Why = "the game's frame could not be read" };
                Color32[] px = recipe(f);
                if (px == null || px.Length != f.W * f.H) return new Pixels { Why = "the recipe returned " + (px == null ? "nothing" : px.Length + " pixels for " + f.W * f.H) };
                return new Pixels { P = px, W = f.W, H = f.H };
            };
            return new Producer { Keys = keys, Produce = produce };
        }
    }
}
