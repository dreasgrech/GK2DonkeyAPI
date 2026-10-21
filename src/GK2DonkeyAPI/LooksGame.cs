using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // Game-creature skins (docs/skins.md "source = game"; docs/gerry-mount.md): the pictures are another character's
    // frames out of the game's character atlas (Gerry's 0901_*), each base frame key mapped to one of them (by hand in
    // the skin's [Game] section, else by rule), scaled by whole pixels onto the skin's canvas with the creature's feet
    // on the base's ground row and its centre on the base's pivot. Nothing of the game's art is shipped: the frames
    // are read back at run time like the recipes' (ReadFrame), a few per tick (AdvanceBuilds), into the skin's sheet.
    //
    // The rules for a key the [Game] section doesn't name: the same frame name when the character has it; a frame of
    // the same clip spread over the character's count (the base's walk 1..8 over a 15-frame walk); a hed_* key is
    // left empty (the creature's whole body is its bdy frame, so nothing of the base's head may show); any other key
    // shows the creature's rest picture (bdy_static_left, else bdy_walk_left_01, else hed_static_left). A value of
    // "none" leaves the key empty on purpose.
    internal static partial class Looks
    {
        internal sealed class GameSource
        {
            internal string Prefix;        // the preset prefix on the character's sprites ("0901_")
            internal float Scale = 1f;     // a whole number: each of its pixels becomes scale x scale on the canvas
            internal float Lift = 1f;      // how much of the creature's own jump is kept (a frame whose feet leave the rest row is moved down by the rest): 1 all, 0.25 a quarter
            internal readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal); // base key -> frame name (without the prefix)
        }

        // What the last game-source build found, for the log and the self-test: "<n> '0901_*' frames found (loaded sprites)".
        internal static string LastGameSourceNote = "";
        internal static int LastGameSourceFound;

        // A frame of the creature: how to get its sprite, and whether that sprite is ours to destroy after the read
        // (a sprite atlas hands out a copy per call; a sprite already in memory may be on one of the game's renderers).
        private struct CreatureFrame
        {
            internal Func<Sprite> Get;
            internal bool Owned;
        }

        private static void StartGameBuild(Loaded l)
        {
            SkinDef d = l.Def;
            GameSource g = d.Game;
            SkinBase b = d.Base;
            Dictionary<string, CreatureFrame> frames = FindCreatureFrames(g.Prefix, b.AtlasName, NeededFrames(g), out string how);
            LastGameSourceFound = frames.Count;
            LastGameSourceNote = $"{frames.Count} '{g.Prefix}*' frames found ({how})";
            Plugin.Log.LogInfo($"Looks: skin '{d.Id}': {LastGameSourceNote}; scaled {g.Scale:0}x onto {d.CanvasW}x{d.CanvasH}.");
            if (frames.Count == 0)
            {
                // nothing to draw: refused here, so Acquire finishes the sheet with nothing and the base shows (before
                // 0.35.0 the sheet was built of 44 empty cells and the mount was invisible, docs/mounts-foundation-2.md 4.1)
                throw new InvalidOperationException($"no '{g.Prefix}*' sprite is in memory and no sprite atlas hands them out; the {b.Id} shows. Is the prefix right (the character's preset number, like 0901_ for Gerry)?");
            }
            // every base key to a frame name, or null (an empty picture)
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (string key in b.Keys) map[key] = ResolveGameFrame(key, g, frames, b, missing);
            if (missing.Count > 0) Plugin.Log.LogWarning($"Looks: skin '{d.Id}': [Game] names frames the game doesn't have: {string.Join(", ", missing)}; those keys stay empty.");
            l.MakeSheet(b.Keys.Length);
            l.ToBuild = new Queue<string>(b.Keys);
            int scale = Mathf.Max(1, Mathf.RoundToInt(g.Scale));
            // The creature's own geometry, from its rest picture: the feet (its lowest opaque row) go on the base's
            // ground row, its centre column on the base's pivot column. Read once, on the first frame produced.
            int groundSrc = -1, centreSrc = -1;
            string restName = ResolveGameFrame("bdy_static_left", g, frames, b, null);
            l.Produce = key =>
            {
                string name = map[key];
                if (name == null) return new Pixels { P = new Color32[d.CanvasW * d.CanvasH], W = d.CanvasW, H = d.CanvasH }; // empty on purpose
                if (groundSrc < 0)
                {
                    SkinFrame rest = restName != null && frames.TryGetValue(restName, out CreatureFrame restFrame) ? ReadCreatureFrame(restFrame, restName, b) : null;
                    Measure(rest, out groundSrc, out centreSrc);
                    if (rest == null || groundSrc < 0) { groundSrc = b.GroundRow; centreSrc = Mathf.RoundToInt(b.PivotX); }
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}': the rest picture '{restName}' has its feet on row {groundSrc} and its centre on column {centreSrc} of the game's {b.CanvasW}x{b.CanvasH} canvas; they go to row {Mathf.RoundToInt(d.PivotY) - (Mathf.RoundToInt(b.PivotY) - b.GroundRow)} and column {Mathf.RoundToInt(d.PivotX)} of the {d.CanvasW}x{d.CanvasH} one, {scale}x.");
                }
                if (!frames.TryGetValue(name, out CreatureFrame get)) return new Pixels { Why = $"the game has no '{g.Prefix}{name}' sprite in memory" };
                SkinFrame f = ReadCreatureFrame(get, name, b);
                if (f == null) return new Pixels { Why = "the game's frame could not be read" };
                // The creature's own jump, reduced: a frame whose feet are above the rest row (a hop) is moved down by
                // the part of the rise the skin doesn't keep (Lift), so a mount under a rider hops a little, not a lot.
                int groundThis = groundSrc;
                if (g.Lift < 0.999f)
                {
                    Measure(f, out int feet, out _);
                    if (feet > groundSrc) groundThis = groundSrc + Mathf.RoundToInt((feet - groundSrc) * (1f - g.Lift));
                }
                Color32[] px = Scaled(f, scale, groundThis, centreSrc, d);
                int opaque = 0;
                for (int i = 0; i < px.Length; i++) if (px[i].a != 0) opaque++;
                GameSourceOpaque[d.Id + "/" + key] = opaque; // for the self-test: the sheet itself is unreadable once uploaded
                return new Pixels { P = px, W = d.CanvasW, H = d.CanvasH };
            };
            building.Add(l);
        }

        // The opaque pixels each produced picture had, by "<skin id>/<key>" (the self-test reads them; a key left empty
        // on purpose has no entry). Small: one int per key of a game-creature skin.
        internal static readonly Dictionary<string, int> GameSourceOpaque = new Dictionary<string, int>(StringComparer.Ordinal);

        // The creature's sprites by frame name (without the prefix), as a getter each (a sprite atlas hands out a copy
        // per call, destroyed after the read). First the sprites already in memory (the character has been on screen),
        // else every loaded sprite atlas asked by name.
        // The frame names a skin's [Game] section asks for by name (its map's values but "none"): what the skin needs the
        // game to have (its sprite manifest). Not the rest picture: a map may name it (Gerry: bdy_static_left =
        // bdy_walk_left_01, the game has no 0901_bdy_static_left) and the build falls back on three rest names itself;
        // demanding the name set every such skin aside at game-ready (the F11 run of 2026-10-21).
        internal static List<string> NeededFrames(GameSource g)
        {
            var needed = new List<string>();
            foreach (var kv in g.Map)
            {
                string v = kv.Value?.Trim();
                if (string.IsNullOrEmpty(v) || v.Equals("none", StringComparison.OrdinalIgnoreCase) || needed.Contains(v)) continue;
                needed.Add(v);
            }
            return needed;
        }

        // A set-aside game-source skin someone has chosen, checked again now and then (its character's atlas may come
        // with a scene): offered again once every named frame is there. Every 10 seconds at most per skin.
        private static readonly Dictionary<string, float> recheckAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        // Every set-aside game-source skin checked once a minute (one nobody wears has no rig tick to ask): offered again
        // when its frames arrive with a scene, so a player can choose it without restarting.
        private static float nextAsideSweep;

        internal static void RecheckAsideSkins()
        {
            if (Time.unscaledTime < nextAsideSweep) return;
            nextAsideSweep = Time.unscaledTime + 60f;
            foreach (SkinDef d in new List<SkinDef>(defs.Values))
            {
                if (d.Game != null && d.Unavailable != null) RecheckGameSource(d);
            }
        }

        internal static void TestResetRecheck(string id) => recheckAt.Remove(id); // the self-test: a recheck now, not in ten seconds

        internal static bool RecheckGameSource(SkinDef d)
        {
            if (d == null || d.Game == null || d.Unavailable == null) return false;
            if (recheckAt.TryGetValue(d.Id, out float at) && Time.unscaledTime < at) return false;
            recheckAt[d.Id] = Time.unscaledTime + 10f;
            try
            {
                List<string> needed = NeededFrames(d.Game);
                Dictionary<string, CreatureFrame> frames = FindCreatureFrames(d.Game.Prefix, d.Base.AtlasName, needed, out string how);
                if (frames.Count == 0) return false;
                foreach (string n in needed) if (!frames.ContainsKey(n)) return false;
                d.Unavailable = null;
                Plugin.Log.LogInfo($"Looks: skin '{d.Id}': its named game frames are there again ({frames.Count} '{d.Game.Prefix}*' frames, {how}); offered again.");
                Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false); // a wearer that gave up on it (drawn as the donkey) wears it on its next tick
                Mounts.Looks.RaiseChanged();
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}': its game frames could not be checked again: {e.Message}");
                return false;
            }
        }

        // Every game-source skin checked once the game is up (the API's game-ready, from MountRegistry): the frames it
        // names must be there (in memory or in a sprite atlas). A skin missing any is set aside (Unavailable: not
        // offered, the donkey shows for whoever had it chosen) with one warning naming the frames, since a game update
        // that renamed them is something to fix in the skin (docs/mounts-foundation-2.md 4.4, the sprite manifest).
        internal static void VerifyGameSources()
        {
            bool changed = false;
            foreach (SkinDef d in defs.Values)
            {
                if (d.Game == null) continue;
                try
                {
                    List<string> needed = NeededFrames(d.Game);
                    Dictionary<string, CreatureFrame> frames = FindCreatureFrames(d.Game.Prefix, d.Base.AtlasName, needed, out string how);
                    var missing = new List<string>();
                    foreach (string n in needed) if (!frames.ContainsKey(n)) missing.Add(n);
                    string was = d.Unavailable;
                    if (frames.Count == 0)
                    {
                        // nothing of the prefix is loaded yet (the character's atlas comes with its scene): not a verdict;
                        // the first wear searches again, and a skin with no frames then shows the donkey (StartGameBuild)
                        Plugin.Log.LogInfo($"Looks: skin '{d.Id}': no '{d.Game.Prefix}*' frame is loaded yet ({how}); checked again when it is worn.");
                        continue;
                    }
                    d.Unavailable = missing.Count > 0 ? $"the game lacks the frames {string.Join(", ", missing)} of '{d.Game.Prefix}*'" : null;
                    if (d.Unavailable != was) changed = true;
                    if (d.Unavailable != null && was == null) Plugin.Log.LogWarning($"Looks: skin '{d.Id}' is set aside: {d.Unavailable} ({frames.Count} found, {how}). A game update may have renamed them: the skin's [Game] section needs the new names.");
                    else if (d.Unavailable == null) Plugin.Log.LogInfo($"Looks: skin '{d.Id}': its {needed.Count} named game frame(s) are there ({frames.Count} '{d.Game.Prefix}*' frames, {how}).");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: skin '{d.Id}': its game frames could not be checked: {e.Message}");
                }
            }
            if (changed) Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false); // a wearer of a skin set aside now draws the donkey; one healed wears it again
            Mounts.Looks.RaiseChanged(); // the lists may have lost a skin
        }

        // The creature's frames: the sprites already in memory first (the game's own, never destroyed), then every
        // loaded sprite atlas for the needed names that memory lacks (a listing per atlas; its copies destroyed;
        // before 0.35.0 a partial set in memory stopped the search, docs/mounts-foundation-2.md 4.2).
        private static Dictionary<string, CreatureFrame> FindCreatureFrames(string prefix, string atlasName, List<string> needed, out string how)
        {
            var result = new Dictionary<string, CreatureFrame>(StringComparer.Ordinal);
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null || !s.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (s.texture == null || s.texture.name.IndexOf(atlasName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string name = CleanName(s.name.Substring(prefix.Length));
                bool clone = s.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0;
                if (!result.ContainsKey(name) || !clone)
                {
                    Sprite keep = s; // the game's own (maybe on one of its renderers): read, never destroyed
                    result[name] = new CreatureFrame { Get = () => keep, Owned = false };
                }
            }
            bool complete = result.Count > 0;
            if (complete && needed != null) foreach (string n in needed) if (!result.ContainsKey(n)) complete = false;
            if (complete)
            {
                how = "sprites in memory";
                return result;
            }
            int inMemory = result.Count;
            int atlases = 0;
            foreach (SpriteAtlas atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
            {
                if (atlas == null) continue;
                atlases++;
                var all = new Sprite[atlas.spriteCount];
                atlas.GetSprites(all);
                foreach (Sprite s in all)
                {
                    if (s == null) continue;
                    if (s.name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        string name = CleanName(s.name.Substring(prefix.Length));
                        string spriteName = prefix + name;
                        SpriteAtlas from = atlas;
                        if (!result.ContainsKey(name)) result[name] = new CreatureFrame { Get = () => from.GetSprite(spriteName), Owned = true }; // a fresh copy per call, ours; memory's kept where it had one
                    }
                    Object.DestroyImmediate(s); // GetSprites made a copy of each for this listing; gone now, not at the frame's end (a build in the same frame would take the copies for the game's own sprites)
                }
            }
            how = inMemory > 0 ? $"{inMemory} in memory, the rest from {atlases} sprite atlas(es)" : $"{atlases} sprite atlas(es) asked by name";
            return result;
        }

        private static string CleanName(string n)
        {
            int clone = n.IndexOf("(Clone)", StringComparison.Ordinal);
            return (clone >= 0 ? n.Substring(0, clone) : n).Trim();
        }

        // The frame on its own canvas (ReadFrame); a copy of ours (an atlas hand-out) destroyed afterwards, the game's
        // own sprite left alone.
        private static SkinFrame ReadCreatureFrame(CreatureFrame fr, string name, SkinBase b)
        {
            Sprite s = fr.Get();
            if (s == null) return null;
            try
            {
                return ReadFrame(name, s, b);
            }
            finally
            {
                if (fr.Owned) Object.DestroyImmediate(s); // our copy, never on a renderer
            }
        }

        // The feet's row (from the bottom) and the centre column of a frame's opaque pixels; -1 for none.
        private static void Measure(SkinFrame f, out int ground, out int centre)
        {
            ground = -1;
            centre = -1;
            if (f == null) return;
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue;
            for (int y = 0; y < f.H; y++)
            {
                for (int x = 0; x < f.W; x++)
                {
                    if (f.P[y * f.W + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                }
            }
            if (maxX < 0) return;
            ground = minY;
            centre = (minX + maxX) / 2;
        }

        // The frame scaled by whole pixels onto the skin's canvas: the creature's ground row lands on the base's
        // ground row, its centre column on the pivot column; nearest neighbour, so each source pixel is a scale x scale
        // block and nothing is blurred.
        private static Color32[] Scaled(SkinFrame f, int scale, int groundSrc, int centreSrc, SkinDef d)
        {
            int w = d.CanvasW, h = d.CanvasH;
            var o = new Color32[w * h];
            int groundDst = Mathf.RoundToInt(d.PivotY) - (Mathf.RoundToInt(d.Base.PivotY) - d.Base.GroundRow); // the base's feet, below its pivot
            int centreDst = Mathf.RoundToInt(d.PivotX);
            for (int y = 0; y < h; y++)
            {
                int sy = FloorDiv(y - groundDst, scale) + groundSrc;
                if (sy < 0 || sy >= f.H) continue;
                for (int x = 0; x < w; x++)
                {
                    int sx = FloorDiv(x - centreDst, scale) + centreSrc;
                    if (sx < 0 || sx >= f.W) continue;
                    o[y * w + x] = f.P[sy * f.W + sx];
                }
            }
            return o;
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        // The frame name for a base key, in this order: by hand ([Game] lines; "none" = empty; a name the game
        // doesn't have is reported through missing and left empty); nothing for a hed_* key (the creature's body frame
        // is the whole creature); a numbered clip spread over the creature's count of that clip (the base's walk 1..8
        // over a 15-frame walk: 1, 2, 4, 6, 8, 10, 12, 14, so the whole hop is in it); the same name; else the rest
        // picture.
        private static string ResolveGameFrame(string key, GameSource g, Dictionary<string, CreatureFrame> frames, SkinBase b, List<string> missing)
        {
            if (g.Map.TryGetValue(key, out string named))
            {
                if (string.IsNullOrEmpty(named) || named.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
                if (frames.ContainsKey(named)) return named;
                if (missing != null && !missing.Contains(named)) missing.Add(named);
                return null;
            }
            if (key.StartsWith("hed_", StringComparison.Ordinal)) return null;
            string clip = ClipOf(key);
            if (clip != key)
            {
                int n = int.Parse(key.Substring(clip.Length + 1));
                int baseCount = 0, gameCount = 0;
                foreach (string k in b.Keys) if (ClipOf(k) == clip) baseCount++;
                while (frames.ContainsKey($"{clip}_{gameCount + 1:00}")) gameCount++;
                if (gameCount > 0 && baseCount > 0)
                {
                    int idx = 1 + (n - 1) * gameCount / baseCount;
                    return $"{clip}_{Mathf.Clamp(idx, 1, gameCount):00}";
                }
            }
            if (frames.ContainsKey(key)) return key;
            foreach (string rest in new[] { "bdy_static_left", "bdy_walk_left_01", "hed_static_left" })
            {
                if (g.Map.TryGetValue(rest, out string r) && frames.ContainsKey(r)) return r;
                if (frames.ContainsKey(rest)) return rest;
            }
            return null;
        }

        // For the self-test: the opaque pixels a game-creature skin's picture had for a key (0 for one left empty on
        // purpose, -1 when the skin isn't loaded or the key wasn't produced).
        internal static int TestCellOpaque(string skinId, string key)
        {
            if (!loaded.TryGetValue(skinId, out Loaded l) || !l.Ready) return -1;
            return GameSourceOpaque.TryGetValue(skinId + "/" + key, out int n) ? n : (l.Frames.ContainsKey(key) ? 0 : -1);
        }
    }
}
