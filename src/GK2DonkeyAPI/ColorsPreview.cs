using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A picture of one donkey in its current colours, for the settings (LookPreview.cs, LookBar.cs).
    // - The base: the donkey's body and head in their own colours, from the game's own files: the look the game loads
    //   for the donkey, Assets/AddressableAssets/WGOs/<id>.prefab (WgoPartLoadManager), read through Addressables in
    //   the main menu, indoors, anywhere, with no donkey in the world. Its sprites' pixels are copied through a
    //   RenderTexture (the game's textures aren't readable). A game update that redraws the donkey shows at once.
    //   Only if that file can't be loaded (a game update moved it): taken from a donkey of ours when one is drawn.
    // - The picture: the base recoloured on the CPU with the colour mapping the game's shader gets (RegionMap, read
    //   with the instance bound, so its own look counts; MountCompanion's paper donkey carries the settings' look).
    // Anything that fails returns null or false: no picture, nothing else.
    internal static partial class Colors
    {
        private sealed class Image
        {
            internal int W, H;
            internal Color32[] P;
        }

        private static readonly Dictionary<Sprite, Image> spritePixels = new Dictionary<Sprite, Image>();
        private static Image baseImage;
        private static bool previewFailed, loadStarted, loadFailed, loggedParts;

        // How light a region's own art is on average (HSV value, 0..1): a custom colour's own lightness is set against
        // it (MountCompanion), since Colorize keeps each pixel's brightness (Black would stay a mid grey).
        // For a mount region on a skin drawn in its own colours (skinId, loaded and whole), the average of that
        // look's colours in the region, so a picked colour comes out as light on the worm as on the donkey; the
        // donkey's otherwise (and for a region the look has no colours in, where nothing maps anyway; and for a skin
        // worn over the donkey, whose regions are still mostly the donkey's own colours).
        internal static float ArtLightness(string regionKey, string skinId = null)
        {
            Looks.Loaded look = Looks.LoadedOwnColours(skinId);
            if (look != null && !look.Def.Over && look.ColoursByRegion.TryGetValue(regionKey, out List<Color32> cols) && cols.Count > 0)
            {
                float s = 0f;
                foreach (Color32 c in cols)
                {
                    Color.RGBToHSV(c, out _, out _, out float v);
                    s += v;
                }
                return s / cols.Count;
            }
            foreach (RegionDef d in Regions)
            {
                if (d.Key != regionKey)
                {
                    continue;
                }
                float sum = 0f;
                int n = 0;
                foreach (var part in d.Parts)
                {
                    foreach (string hex in part.Colors)
                    {
                        if (TryHex(hex, out Color32 c))
                        {
                            Color.RGBToHSV(c, out _, out _, out float v);
                            sum += v;
                            n++;
                        }
                    }
                }
                return n > 0 ? sum / n : 0.5f;
            }
            return 0.5f;
        }
        private static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> loading;

        internal sealed partial class Rig
        {
            // The donkey's body and head as they are drawn now: only the shown ones (the view also holds hidden copies).
            internal List<SpriteRenderer> DonkeySprites()
            {
                var list = new List<SpriteRenderer>();
                foreach (SpriteTarget t in sprites)
                {
                    if (t.G == Group.DonkeySprites && t.R != null && t.R && t.R.sprite != null && t.R.enabled && t.R.gameObject.activeInHierarchy)
                    {
                        list.Add(t.R);
                    }
                }
                return list;
            }
        }

        internal static bool HasPreviewBase => baseImage != null;

        // Starts loading the donkey's look from the game's files, and picks it up when it's there. Called a few times
        // a second while a page with a picture exists; cheap once done.
        internal static void PollBase()
        {
            if (previewFailed || baseImage != null || loadFailed)
            {
                return;
            }
            try
            {
                if (!loadStarted)
                {
                    loadStarted = true;
                    string key = "Assets/AddressableAssets/WGOs/" + Plugin.MountWgoId.Value + ".prefab";
                    loading = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(key);
                    return;
                }
                if (!loading.IsValid() || !loading.IsDone)
                {
                    return;
                }
                GameObject prefab = loading.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? loading.Result : null;
                Image img = prefab != null ? ComposeFrom(prefab.GetComponentsInChildren<SpriteRenderer>(true), fromPrefab: true) : null;
                UnityEngine.AddressableAssets.Addressables.Release(loading);
                if (img == null)
                {
                    loadFailed = true;
                    Plugin.Log.LogInfo("Settings: the donkey's look isn't in the game's files where it was (" + Plugin.MountWgoId.Value + "); its picture comes from your donkey once it's drawn.");
                    return;
                }
                baseImage = img;
                Plugin.Log.LogInfo($"Settings: the donkey's picture is read from the game's files ({img.W} x {img.H}).");
            }
            catch (Exception e)
            {
                loadFailed = true;
                Plugin.Log.LogInfo("Settings: the donkey's look can't be read from the game's files (" + e.Message + "); its picture comes from your donkey once it's drawn.");
            }
        }

        // The fallback when the game's file couldn't be read: from this instance's view. True when there is a base.
        internal static bool CaptureBase(Mount.Instance i)
        {
            if (previewFailed || baseImage != null || !loadFailed || i == null)
            {
                return baseImage != null;
            }
            try
            {
                Image img = ComposeFrom(i.ColorsRig.DonkeySprites().ToArray(), fromPrefab: false);
                if (img == null)
                {
                    return false;
                }
                baseImage = img;
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        // into: reused when it has the size, else replaced (the old one destroyed). Null: no picture.
        internal static Texture2D BuildPreview(Mount.Instance i, Texture2D into)
        {
            if (previewFailed || i == null)
            {
                return null;
            }
            try
            {
                PollBase();
                Image b = baseImage;
                if (b == null)
                {
                    return null;
                }
                Dictionary<int, Color32> map = DonkeyMap(i);
                var cache = new Dictionary<int, Color32>();
                var outPx = new Color32[b.P.Length];
                for (int n = 0; n < outPx.Length; n++)
                {
                    Color32 c = b.P[n];
                    if (c.a == 0)
                    {
                        continue;
                    }
                    Color32 m = Map(c, map, cache);
                    m.a = c.a;
                    outPx[n] = m;
                }
                if (into == null || into.width != b.W || into.height != b.H)
                {
                    if (into != null)
                    {
                        UnityEngine.Object.Destroy(into);
                    }
                    into = new Texture2D(b.W, b.H, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "GK2DonkeyAPI preview" };
                }
                into.SetPixels32(outPx);
                into.Apply(false);
                return into;
            }
            catch (Exception e)
            {
                Fail(e);
                return null;
            }
        }

        private static void Fail(Exception e)
        {
            previewFailed = true; // once: a picture isn't worth a log line every frame
            Plugin.Log.LogWarning("Settings: the donkey's picture can't be drawn (" + e.Message + "); the settings work without it.");
        }

        // The shown body and head in one image, in their own colours, laid out in the body sprite's own space: the
        // sprites lie in a tilted plane of the 3D scene, so only that space has the sprite's x and y (found
        // 2026-10-02: laid out in their parent's space the head sat a body lower, two heads). The facing flip is
        // undone, so the picture always faces the same way.
        // fromPrefab: the asset's renderers (not in a scene): the shown ones are those whose own object and every
        // parent are switched on and that are enabled, named bdy or hed (the colour code's donkey sprites).
        private static Image ComposeFrom(SpriteRenderer[] all, bool fromPrefab)
        {
            var rs = new List<SpriteRenderer>();
            foreach (SpriteRenderer r in all)
            {
                if (r == null || r.sprite == null || (r.name != "bdy" && r.name != "hed"))
                {
                    continue;
                }
                if (fromPrefab && !ShownInPrefab(r))
                {
                    continue;
                }
                rs.Add(r);
            }
            if (rs.Count == 0)
            {
                return null;
            }
            SpriteRenderer body = rs.Find(r => r.name == "bdy") ?? rs[0];
            Transform space = body.transform;
            rs.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var placed = new List<(Image Px, int X, int Y, bool Flip)>();
            foreach (SpriteRenderer r in rs)
            {
                Sprite s = r.sprite;
                Image px = Extract(s);
                if (px == null)
                {
                    return null;
                }
                float ppu = s.pixelsPerUnit;
                Vector3 local = space.InverseTransformPoint(r.transform.position);
                bool flip = r.flipX != (Vector3.Dot(space.right, r.transform.right) < 0f);
                // The atlas keeps only the sprite's drawn pixels (textureRect), trimmed out of its full rectangle by
                // textureRectOffset; the pivot is in the full rectangle. Without the offset the head sat a head lower.
                Vector2 pivot = s.pivot;
                Vector2 off = s.textureRectOffset;
                float full = s.rect.width;
                int x = Mathf.RoundToInt(flip
                    ? local.x * ppu - (full - pivot.x) + (full - off.x - px.W)
                    : local.x * ppu - pivot.x + off.x);
                int y = Mathf.RoundToInt(local.y * ppu - pivot.y + off.y);
                if (!loggedParts)
                {
                    Plugin.Log.LogInfo($"Settings: picture part {r.name}: sprite {s.name}, full {s.rect.width}x{s.rect.height}, drawn {px.W}x{px.H} at offset {off.x},{off.y}, pivot {pivot.x},{pivot.y}, place {local.x * ppu:0.0},{local.y * ppu:0.0}, flip {flip}, order {r.sortingOrder}.");
                }
                placed.Add((px, x, y, flip));
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + px.W); maxY = Math.Max(maxY, y + px.H);
            }
            loggedParts = true;
            int w = maxX - minX, h = maxY - minY;
            if (w <= 0 || h <= 0 || w > 1024 || h > 1024)
            {
                return null;
            }
            var outPx = new Color32[w * h];
            foreach (var (px, x0, y0, flip) in placed)
            {
                for (int y = 0; y < px.H; y++)
                {
                    for (int x = 0; x < px.W; x++)
                    {
                        Color32 c = px.P[y * px.W + x];
                        if (c.a == 0)
                        {
                            continue;
                        }
                        int tx = (flip ? px.W - 1 - x : x) + x0 - minX;
                        int ty = y + y0 - minY;
                        outPx[ty * w + tx] = c;
                    }
                }
            }
            return new Image { W = w, H = h, P = outPx };
        }

        private static bool ShownInPrefab(SpriteRenderer r)
        {
            if (!r.enabled)
            {
                return false;
            }
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        // The donkey sprites' source colours and what they become for this instance (its own look counts: bound).
        private static Dictionary<int, Color32> DonkeyMap(Mount.Instance i)
        {
            var map = new Dictionary<int, Color32>();
            using (Mount.Bind(i))
            {
                bool on = Enabled.Value;
                foreach (RegionDef d in Regions)
                {
                    RegionMap rm = null;
                    foreach (var part in d.Parts)
                    {
                        if (part.G != Group.DonkeySprites)
                        {
                            continue;
                        }
                        rm = rm ?? new RegionMap(d.S);
                        foreach (string hex in part.Colors)
                        {
                            if (TryHex(hex, out Color32 src))
                            {
                                map[Key(src)] = on ? rm.Apply(src) : src;
                            }
                        }
                    }
                }
            }
            return map;
        }

        private static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        // The exact colour, else the nearest within a few steps (a copy through a RenderTexture can shift a colour by
        // one or two), else unchanged.
        private static Color32 Map(Color32 c, Dictionary<int, Color32> map, Dictionary<int, Color32> cache)
        {
            int k = Key(c);
            if (cache.TryGetValue(k, out Color32 hit))
            {
                return hit;
            }
            Color32 result = c;
            if (map.TryGetValue(k, out Color32 exact))
            {
                result = exact;
            }
            else
            {
                int best = 3 * 6 * 6 + 1;
                foreach (var kv in map)
                {
                    int dr = ((kv.Key >> 16) & 255) - c.r, dg = ((kv.Key >> 8) & 255) - c.g, db = (kv.Key & 255) - c.b;
                    int dist = dr * dr + dg * dg + db * db;
                    if (dist < best)
                    {
                        best = dist;
                        result = kv.Value;
                    }
                }
            }
            cache[k] = result;
            return result;
        }

        private static Image Extract(Sprite s)
        {
            if (spritePixels.TryGetValue(s, out Image known))
            {
                return known;
            }
            if (s.packed && s.packingMode == SpritePackingMode.Tight)
            {
                return null; // its rectangle holds other sprites' pixels too
            }
            Texture2D tex = s.texture;
            Rect r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            if (tex == null || w <= 0 || h <= 0)
            {
                return null;
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D tmp = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                tmp = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
                tmp.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                var px = new Image { W = w, H = h, P = tmp.GetPixels32() };
                spritePixels[s] = px;
                return px;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (tmp != null)
                {
                    UnityEngine.Object.Destroy(tmp);
                }
            }
        }
    }
}
