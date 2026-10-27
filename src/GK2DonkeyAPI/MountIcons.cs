using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Mounts panel's icons (docs/mounts-panel-design.md): a mount's portrait when its pack or code names one, else
    // its Default skin's standing picture (bdy_static_left): a pack's own PNG decoded once into a small texture cropped to
    // its opaque pixels, a code skin's sprite, a game-source skin's game sprite found by name, the donkey's own game
    // sprite. Given as a texture and the part of it to show (a RawImage's uvRect): nothing is copied or read back from
    // the GPU, and a game atlas is shown as it is.
    internal static class MountIcons
    {
        internal readonly struct Icon
        {
            internal readonly Texture Texture;
            internal readonly Rect Uv;      // the part of the texture, normalized
            internal readonly int W, H;     // in art pixels

            internal Icon(Texture t, Rect uv, int w, int h)
            {
                Texture = t;
                Uv = uv;
                W = w;
                H = h;
            }

            internal bool Any => Texture != null;
        }

        private static readonly Dictionary<string, Texture2D> decoded = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase); // a pack's standing picture by skin id (null: tried, nothing)
        private static readonly Dictionary<string, Sprite> gameSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly HashSet<string> failedLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const string RestKey = "bdy_static_left";

        internal static Icon Of(Catalog.Entry k)
        {
            if (k == null) return default;
            try
            {
                Texture2D portrait = Looks.PortraitOfMount(k);
                if (portrait != null) return Whole(portrait);
                if (k == Catalog.Donkey) return FromGameSprite(Looks.Donkey.GamePrefix + RestKey);
                Looks.SkinDef d = Looks.Find(Looks.DefaultSkinOf(k));
                return d != null ? OfSkin(d) : default;
            }
            catch (Exception e)
            {
                if (failedLogged.Add(k.Id)) Plugin.Log.LogInfo($"Settings: no icon for the mount '{k.Id}' ({e.Message}); its card has none.");
                return default;
            }
        }

        internal static Icon OfSkin(Looks.SkinDef d)
        {
            if (d == null) return default;
            if (d.PortraitTexture != null) return Whole(d.PortraitTexture);
            if (Looks.InheritsPictures(d))
            {
                // built on its mount's default: no standing picture of its own, the default's; drawn over the default, the
                // two composed as its sheet will have them (docs/skin-layering-plan.md 2.10)
                if (!Looks.HasOwnKey(d, RestKey)) return OfSkin(d.Parent);
                if (d.OverDefault)
                {
                    if (!decoded.TryGetValue(d.Id, out Texture2D comp) || (comp != null && !comp))
                    {
                        Color32[] px = Looks.ResolvedPicture(d, RestKey, out int w, out int h);
                        comp = px != null ? Cropped(px, w, h, d.Id) : null;
                        decoded[d.Id] = comp;
                    }
                    if (comp != null) return Whole(comp);
                }
            }
            Sprite s = null;
            try { if (d.Sprites != null && !d.Sprites.TryGetFrame(RestKey, out s)) s = null; } catch { s = null; } // a mod's source that throws: no icon from it
            if (s != null && s) return FromSprite(s);
            if (d.Game != null)
            {
                string name = d.Game.Map.TryGetValue(RestKey, out string mapped) && !string.IsNullOrEmpty(mapped) && !mapped.Equals("none", StringComparison.OrdinalIgnoreCase) ? mapped : RestKey;
                foreach (string candidate in new[] { name, "bdy_walk_left_01", "hed_static_left" })
                {
                    Icon g = FromGameSprite(d.Game.Prefix + candidate);
                    if (g.Any) return g;
                }
                return default;
            }
            if (d.LoadPng != null)
            {
                if (!decoded.TryGetValue(d.Id, out Texture2D tex) || (tex != null && !tex))
                {
                    tex = Decode(d);
                    decoded[d.Id] = tex; // null too: not tried again until the skin is reloaded (Forget)
                }
                if (tex != null) return Whole(tex);
            }
            if (d.Over) return FromGameSprite(Looks.Donkey.GamePrefix + RestKey); // drawn over the donkey: the donkey is what stands there
            return default;
        }

        // The Mounts panel's ability icons (2026-10-27): a frame of the mount's own look by its key (bdy_glide_left_01 for
        // Flying: the mount in the air), else its standing picture; and the cart's wheel as the game draws it, found on any
        // mount whose cart is drawn (the first found is kept).
        internal static Icon FrameOf(Catalog.Entry k, string key)
        {
            if (k == null) return default;
            try
            {
                Looks.SkinDef d = Looks.Find(Looks.DefaultSkinOf(k));
                if (d != null)
                {
                    Sprite s = null;
                    try { if (d.Sprites != null && !d.Sprites.TryGetFrame(key, out s)) s = null; } catch { s = null; }
                    if (s != null && s) return FromSprite(s);
                    if (d.LoadPng != null && Looks.HasOwnKey(d, key))
                    {
                        string cacheKey = d.Id + "|" + key;
                        if (!decoded.TryGetValue(cacheKey, out Texture2D tex) || (tex != null && !tex))
                        {
                            tex = Decode(d, key);
                            decoded[cacheKey] = tex;
                        }
                        if (tex != null) return Whole(tex);
                    }
                }
            }
            catch (Exception e)
            {
                if (failedLogged.Add(k.Id + "|" + key)) Plugin.Log.LogInfo($"Settings: no '{key}' picture for the mount '{k.Id}' ({e.Message}); its standing one instead.");
            }
            return Of(k);
        }

        private static Sprite wheel;

        private static bool wheelMissSaid;

        // first: the panel's own picture of the mount and its cart (its paper; the world's mounts may have no cart drawn,
        // seen 2026-10-27: no icon), then any mount whose cart is drawn
        internal static Icon CartWheel(Mount.Instance paper = null)
        {
            if (wheel == null || !wheel)
            {
                wheel = paper != null && paper.ColorsRig != null ? paper.ColorsRig.WheelSprite : null;
                if (wheel == null) Mount.ForEachInstance(i => { if (wheel == null && i.HasCart && i.ColorsRig != null) wheel = i.ColorsRig.WheelSprite; }, bind: false);
                if (wheel == null && !wheelMissSaid)
                {
                    wheelMissSaid = true;
                    Plugin.Log.LogInfo($"Settings: no cart wheel drawn anywhere yet for the Cargo cart icon (the picture {(paper == null ? "none" : paper.HasCart ? "with its cart" : "alone")}); it comes once one is.");
                }
            }
            return wheel != null ? FromSprite(wheel) : default;
        }

        // A pack's standing picture, cropped to its opaque pixels, as a small texture of its own (a few KB).
        private static Texture2D Decode(Looks.SkinDef d, string key = RestKey)
        {
            byte[] png = d.LoadPng(key);
            if (png == null) return null;
            Color32[] px = Looks.DecodePng(png, out int w, out int h, out string why, d.CanvasW, d.CanvasH);
            return Cropped(px, w, h, d.Id);
        }

        // A picture cropped to its opaque pixels, as a small texture of its own; null when nothing is drawn.
        private static Texture2D Cropped(Color32[] px, int w, int h, string id)
        {
            if (px == null || w <= 0 || h <= 0) return null;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX) return null; // nothing drawn
            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            var cropped = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
            {
                Array.Copy(px, (minY + y) * w + minX, cropped, y * cw, cw);
            }
            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { name = "icon " + id, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(cropped);
            tex.Apply(false, true);
            return tex;
        }

        private static Icon Whole(Texture t) => t == null ? default : new Icon(t, new Rect(0f, 0f, 1f, 1f), t.width, t.height);

        private static Icon FromSprite(Sprite s)
        {
            Texture2D t = s.texture;
            if (t == null) return default;
            Rect r = s.textureRect;
            return new Icon(t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height), Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
        }

        // One of the game's sprites by its full name, found once among the loaded sprites (the game's own, never destroyed
        // by this mod; a copy on a renderer is taken when nothing else carries the name).
        private static Icon FromGameSprite(string name)
        {
            if (!gameSprites.TryGetValue(name, out Sprite found) || (found != null && !found)) // never looked, or a found one destroyed since; a miss is remembered (no rescan per card)
            {
                found = null;
                foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || !s) continue;
                    string n = s.name;
                    bool clone = n.EndsWith("(Clone)", StringComparison.Ordinal);
                    if (clone) n = n.Substring(0, n.Length - 7).TrimEnd();
                    if (!n.Equals(name, StringComparison.Ordinal)) continue;
                    if (found == null || !clone) found = s;
                    if (!clone) break;
                }
                gameSprites[name] = found;
            }
            return found != null ? FromSprite(found) : default;
        }

        // A skin reloaded or withdrawn: its decoded picture read again next time.
        internal static void Forget(string skinId)
        {
            if (decoded.TryGetValue(skinId, out Texture2D tex))
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
                decoded.Remove(skinId);
            }
            failedLogged.Remove(skinId);
        }
    }
}
