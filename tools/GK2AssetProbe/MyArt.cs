using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace GK2AssetProbe
{
    // Your own art and sounds, never in the game, tried out in it (docs/external-assets.md, "Testing your own art").
    // Everything is read from BepInEx/GK2-asset-probe/test/, again on every press, so a file can be changed while the
    // game runs:
    //   9.  every PNG in test/ is shown beside the donkey, drawn with the donkey's material (lit like the game);
    //   10. PNGs in test/donkey/ dress the donkey frame by frame, the way Clerk Desk dresses its zombie: one overlay
    //       sprite per donkey part (bdy, hed), its picture picked by the game frame's name each frame before the camera
    //       draws. "over" draws on top of the donkey, "replace" also hides the donkey's own picture for the frames you
    //       have drawn: forceRenderingOff from the first camera of a frame to the end of that frame (after every
    //       camera and the GUI), so every camera skips the donkey and no game code ever sees it hidden: a view going
    //       back to the API's pool can't carry it (2026-10-17: two [pool] leftovers when it stayed set between frames;
    //       then the donkey still showed when only the main camera's draw hid it: the game draws it with another one);
    //   11. .ogg/.wav files in test/ are played one after the other (a plain 2D sound: the game's volume settings
    //       don't apply to it).
    // The donkey's frames to paint over come from step 3: each frame it sees is saved whole (96x96, as the game
    // places it) to donkey-frames/<name>.png, which is also the name test/donkey/ looks for.
    internal static class MyArt
    {
        private static string Dir => Path.Combine(Probe.OutDir, "test");
        // The dressing pictures: test\donkey\, or one of the folders in test\skins\ (button "Next test skin").
        private static string skin; // null: test\donkey\
        private static string DonkeyDir => skin == null ? Path.Combine(Dir, "donkey") : Path.Combine(Dir, "skins", skin);
        internal static string SkinName => skin ?? "test\\donkey";

        internal static void NextSkin() => StepSkin(+1);
        internal static void PrevSkin() => StepSkin(-1);

        private static void StepSkin(int step)
        {
            EnsureDirs();
            string root = Path.Combine(Dir, "skins");
            Directory.CreateDirectory(root);
            var all = new List<string> { null };
            all.AddRange(Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            int i = all.IndexOf(skin);
            skin = all[((i + step) % all.Count + all.Count) % all.Count];
            ForgetFrames();
            Probe.Info($"test skin now: {SkinName} ({Directory.GetFiles(DonkeyDir, "*.png").Length} pictures; {all.Count - 1} skins in test\\skins\\)");
            Probe.Status = "Skin: " + SkinName;
        }
        private static string FramesDir => Path.Combine(Probe.OutDir, "donkey-frames");
        private const float PixelsPerUnit = 50f; // the donkey's (and Clerk Desk's)

        // textures read from test/: the pictures' (9) and the donkey frames' (10), apart, so a reload of one leaves the other
        private static readonly List<Object> pictureArt = new List<Object>(), frameArt = new List<Object>();

        private static void EnsureDirs()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(DonkeyDir);
        }

        private static Texture2D Load(string file, List<Object> owner)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "assetprobe " + Path.GetFileName(file), filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!t.LoadImage(File.ReadAllBytes(file)))
            {
                Object.Destroy(t);
                Probe.Info($"{file}: not a picture Unity can read");
                return null;
            }
            owner.Add(t);
            return t;
        }

        // ---------------------------------------------------------------- 9. pictures

        internal static void ShowPictures()
        {
            try
            {
                EnsureDirs();
                string[] files = Directory.GetFiles(Dir, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
                if (files.Length == 0)
                {
                    Probe.Status = "9: put PNG files in BepInEx\\GK2-asset-probe\\test\\ first.";
                    return;
                }
                if (!Probe.FindDonkey()) return;
                Probe.ClearPictures();
                foreach (Object o in pictureArt) if (o != null) Object.Destroy(o);
                pictureArt.Clear();
                foreach (string f in files)
                {
                    Texture2D t = Load(f, pictureArt);
                    if (t == null) continue;
                    Sprite sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sp.name = "assetprobe " + Path.GetFileName(f);
                    Probe.Add(Path.GetFileNameWithoutExtension(f), sp, new Material(Probe.Body.sharedMaterial));
                    Probe.Info($"my picture {Path.GetFileName(f)}: {t.width}x{t.height} px = {t.width / PixelsPerUnit:0.##} x {t.height / PixelsPerUnit:0.##} units before the game's upright stretch");
                }
                Probe.Status = $"9: {files.Length} of your pictures beside the donkey.";
            }
            catch (Exception e)
            {
                Probe.Warn("your pictures failed: " + e);
                Probe.Status = "9 failed: see the log.";
            }
        }

        // ---------------------------------------------------------------- 10. dressing the donkey

        internal enum Mode { Off, Over, Replace }

        internal static Mode Dress { get; private set; }
        private static bool hooked;
        private static readonly Dictionary<SpriteRenderer, SpriteRenderer> overlays = new Dictionary<SpriteRenderer, SpriteRenderer>();
        private static readonly Dictionary<SpriteRenderer, bool> hidden = new Dictionary<SpriteRenderer, bool>(); // part -> its own forceRenderingOff
        private static readonly Dictionary<string, (Sprite Sprite, string From)> frames = new Dictionary<string, (Sprite, string)>();
        private static readonly HashSet<string> missing = new HashSet<string>();
        private static readonly HashSet<string> used = new HashSet<string>();
        private static MaterialPropertyBlock block;

        internal static void NextDress() => SetDress(Dress == Mode.Off ? Mode.Over : Dress == Mode.Over ? Mode.Replace : Mode.Off);

        internal static void SetDress(Mode next)
        {
            if (next == Dress) return;
            if (next == Mode.Off)
            {
                Undress("turned off");
                return;
            }
            if (Dress == Mode.Off)
            {
                EnsureDirs();
                if (!Directory.GetFiles(DonkeyDir, "*.png").Any())
                {
                    Probe.Status = $"No pictures in {SkinName}: pick another skin, or put PNGs there (names: see donkey-frames\\).";
                    return;
                }
                if (!Probe.FindDonkey()) return;
                ForgetFrames();
                if (!hooked)
                {
                    hooked = true;
                    Camera.onPreCull += PreCull;

                }
            }
            if (next == Mode.Over) RestoreHidden();
            Dress = next;
            Probe.Info($"dressing the donkey: {Dress} ({Directory.GetFiles(DonkeyDir, "*.png").Length} pictures in {SkinName})");
            Probe.Status = $"10: dressing {(Dress == Mode.Over ? "over the donkey" : "instead of the donkey")}.";
        }

        // Reads test/donkey/ again on the next frame (for "Reload").
        internal static void ForgetFrames()
        {
            foreach (var f in frames.Values) if (f.Sprite != null) Object.Destroy(f.Sprite);
            frames.Clear();
            missing.Clear();
            foreach (Object o in frameArt) if (o != null) Object.Destroy(o);
            frameArt.Clear();
        }

        private static float recheckAt;

        // Twice a second: is the donkey still the one being dressed? Hitching and unhitching change its view, and the
        // old view's parts go back to the API's pool, where a foal can get them (2026-10-17: a foal wore the skin).
        internal static void Recheck()
        {
            if (Dress == Mode.Off || Time.unscaledTime < recheckAt) return;
            recheckAt = Time.unscaledTime + 0.5f;
            if (Probe.Recheck())
            {
                DropOverlays();
                Probe.Info("dressing: the donkey's view changed (hitch, unhitch, a door); dressing the new one");
            }
        }

        private static void DropOverlays()
        {
            RestoreHidden();
            foreach (var ov in overlays.Values) if (ov != null) Object.Destroy(ov.gameObject);
            overlays.Clear();
        }

        // At the end of every frame (Plugin's coroutine, after all cameras and the GUI): the donkey shows again before any
        // game code runs.
        internal static void EndOfFrame() => RestoreHidden();

        private static int dressedFrame = -1;
        private static readonly HashSet<string> camerasSeen = new HashSet<string>();

        // Every camera, before it culls: the first one of a frame dresses (and hides) for all of them.
        private static void PreCull(Camera cam)
        {
            if (Dress == Mode.Off || cam == null) return;
            if (camerasSeen.Add(cam.name))
            {
                Probe.Info($"dressing: camera '{cam.name}' draws (depth {cam.depth}, culling mask {cam.cullingMask:X}, target {(cam.targetTexture != null ? cam.targetTexture.name : "the screen")}, main {cam == Camera.main})");
            }
            if (dressedFrame == Time.frameCount) return;
            dressedFrame = Time.frameCount;
            try
            {
                DressNow(Camera.main != null ? Camera.main : cam);
            }
            catch (Exception e)
            {
                Probe.Warn("dressing the donkey failed, turned off: " + e);
                Undress("failed");
            }
        }

        private static void DressNow(Camera cam)
        {
            SpriteRenderer b = Probe.Body;
            Transform root = Probe.Root;
            PlayerController pc = MainGame.Instance != null ? MainGame.PlayerController : null;
            if (b == null || root == null || pc == null || !b.gameObject.activeInHierarchy)
            {
                // its view was swapped (riding <-> driving the cart, a door): the dressing stays on and moves to the new
                // view once found (2026-10-17: switching to the cart turned it off). Only Off, Clear all or a scene
                // change turn it off.
                DropOverlays();
                if (Time.unscaledTime >= recheckAt)
                {
                    recheckAt = Time.unscaledTime + 0.25f;
                    if (Probe.Recheck()) Probe.Info("dressing: the donkey's view changed; dressing the new one");
                }
                return;
            }
            if ((root.position - pc.transform.position).sqrMagnitude > 20f * 20f)
            {
                // its view could go back to the game's pool and be reused: never dress (or hide) a donkey we lost track
                // of; the dressing waits (stays on) until the donkey is near again
                DropOverlays();
                return;
            }
            Dress1(b, cam);
            if (Probe.Head != null) Dress1(Probe.Head, cam);
        }

        private static void Dress1(SpriteRenderer part, Camera cam)
        {
            if (!overlays.TryGetValue(part, out SpriteRenderer ov) || ov == null)
            {
                var go = new GameObject("assetprobe dress " + part.name) { layer = part.gameObject.layer };
                ov = go.AddComponent<SpriteRenderer>();
                overlays[part] = ov;
            }
            Sprite game = part.sprite;
            Sprite mine = game != null ? FrameFor(part.name, game.name) : null;
            bool partShows = part.enabled && part.gameObject.activeInHierarchy && game != null;
            ov.sprite = mine;
            ov.sharedMaterial = part.sharedMaterial;
            if (part.HasPropertyBlock())
            {
                if (block == null) block = new MaterialPropertyBlock();
                part.GetPropertyBlock(block);
                ov.SetPropertyBlock(block);
            }
            ov.sortingLayerID = part.sortingLayerID;
            ov.sortingOrder = part.sortingOrder;
            ov.flipX = part.flipX;
            ov.flipY = part.flipY;
            ov.color = part.color;
            Transform pt = part.transform, ot = ov.transform;
            ot.rotation = pt.rotation;
            ot.localScale = pt.lossyScale;
            // the game frame is trimmed in its atlas; its sub-pixel offset moves it within the canvas (Clerk Desk does the same)
            Vector2 off = game != null ? game.textureRectOffset : Vector2.zero;
            float ppu = game != null ? game.pixelsPerUnit : PixelsPerUnit;
            var local = new Vector3((off.x - Mathf.Floor(off.x)) / ppu * (part.flipX ? -1f : 1f), (off.y - Mathf.Floor(off.y)) / ppu, 0f);
            ot.position = pt.TransformPoint(local) - cam.transform.forward * 0.001f;
            ov.enabled = partShows && mine != null;

            // hidden for the rest of this frame (EndOfFrame puts it back); no picture of ours for this frame: the donkey's own
            // shows, so a gap is plain to see
            if (Dress == Mode.Replace && partShows && mine != null && !part.forceRenderingOff)
            {
                hidden[part] = false;
                part.forceRenderingOff = true;
            }
        }

        // The picture for a game frame: test/donkey/<frame>.png ("bdy_walk_left_03"), else <clip>.png
        // ("bdy_walk_left", one picture for the whole clip), else <part>.png ("bdy", one picture for everything).
        private static Sprite FrameFor(string part, string gameName)
        {
            if (frames.TryGetValue(gameName, out var hit)) return hit.Sprite;
            string key = FrameKey(gameName);
            var tries = new List<string> { key };
            int us = key.LastIndexOf('_');
            if (us > 0 && key.Substring(us + 1).All(char.IsDigit)) tries.Add(key.Substring(0, us));
            tries.Add(part);
            Sprite found = null;
            string from = null;
            foreach (string t in tries)
            {
                string f = Path.Combine(DonkeyDir, t + ".png");
                if (!File.Exists(f)) continue;
                Texture2D tex = Load(f, frameArt);
                if (tex == null) continue;
                // centred like the game's frames (96x96, pivot in the middle): a 96x96 picture sits exactly on the frame
                found = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                found.name = "assetprobe " + t;
                from = t;
                break;
            }
            frames[gameName] = (found, from);
            used.Add(key);
            if (found == null && missing.Add(key))
            {
                Probe.Info($"dressing: no picture for {key} (tried {string.Join(", ", tries.Select(t => t + ".png"))}); the donkey's own shows");
            }
            else if (found != null)
            {
                Probe.Info($"dressing: {key} drawn with {from}.png");
            }
            return found;
        }

        // "0301_bdy_walk_left_01(Clone)" -> "bdy_walk_left_01"
        internal static string FrameKey(string spriteName)
        {
            string n = spriteName.Replace("(Clone)", "").Trim();
            int us = n.IndexOf('_');
            if (us > 0 && n.Substring(0, us).All(char.IsDigit)) n = n.Substring(us + 1);
            return n;
        }

        private static void RestoreHidden()
        {
            foreach (var kv in hidden) if (kv.Key != null) kv.Key.forceRenderingOff = kv.Value;
            hidden.Clear();
        }

        internal static void Undress(string why)
        {
            bool was = Dress != Mode.Off || overlays.Count > 0 || hidden.Count > 0;
            Dress = Mode.Off;
            DropOverlays();
            if (was)
            {
                if (used.Count > 0)
                {
                    try
                    {
                        File.WriteAllLines(Path.Combine(Probe.OutDir, "donkey-frames-used.txt"),
                            used.OrderBy(k => k).Select(k => k + (missing.Contains(k) ? "   (no picture)" : "")));
                    }
                    catch (Exception e)
                    {
                        Probe.Warn("donkey-frames-used.txt not written: " + e.Message);
                    }
                }
                Probe.Info($"dressing off ({why}); {used.Count} donkey frames shown, {missing.Count} without a picture -> donkey-frames-used.txt");
            }
        }

        // ---------------------------------------------------------------- every donkey frame

        // All of the donkey's sprites loaded now (its atlases come whole, so every clip's frames are in memory while a
        // donkey is out): each saved whole to donkey-frames\, the same as step 3 does for the frames it sees.
        internal static void ExportAllFrames()
        {
            try
            {
                if (!Probe.FindDonkey()) return;
                string prefix = Probe.Body.sprite.name.Replace("(Clone)", "").Trim();
                int us = prefix.IndexOf('_');
                prefix = us > 0 ? prefix.Substring(0, us + 1) : prefix; // "0301_"
                var seen = new HashSet<string>(StringComparer.Ordinal);
                int saved = 0;
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp == null || !sp.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (sp.texture == null || sp.texture.name.IndexOf("Characters", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string key = FrameKey(sp.name);
                    if (!seen.Add(key)) continue;
                    bool had = File.Exists(Path.Combine(FramesDir, key + ".png"));
                    ExportFrame(sp);
                    if (!had) saved++;
                }
                var groups = seen.GroupBy(k => { int u = k.LastIndexOf('_'); return u > 0 && k.Substring(u + 1).All(char.IsDigit) ? k.Substring(0, u) : k; })
                    .OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count());
                Probe.Info($"every donkey frame ('{prefix}*' in the character atlases): {seen.Count}, {saved} new in donkey-frames\\; by clip: {string.Join(", ", groups)}");
                Probe.Status = $"{seen.Count} donkey frames in donkey-frames\\.";
            }
            catch (Exception e)
            {
                Probe.Warn("exporting every donkey frame failed: " + e);
                Probe.Status = "Export failed: see the log.";
            }
        }

        // ---------------------------------------------------------------- templates (from step 3)

        // The game frame, whole (its canvas, the trimmed picture put back where the game draws it), as a PNG to paint over.
        internal static void ExportFrame(Sprite sp)
        {
            if (sp == null) return;
            string key = FrameKey(sp.name);
            string file = Path.Combine(FramesDir, key + ".png");
            if (File.Exists(file)) return;
            Directory.CreateDirectory(FramesDir);
            Texture2D tex = sp.texture;
            Rect tr = sp.textureRect;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Texture2D crop = null, canvas = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                int w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);
                crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                crop.ReadPixels(new Rect(Mathf.Round(tr.x), Mathf.Round(tr.y), w, h), 0, 0);
                crop.Apply();
                int cw = Mathf.RoundToInt(sp.rect.width), ch = Mathf.RoundToInt(sp.rect.height);
                canvas = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                canvas.SetPixels32(new Color32[cw * ch]);
                int ox = Mathf.FloorToInt(sp.textureRectOffset.x), oy = Mathf.FloorToInt(sp.textureRectOffset.y);
                if (ox >= 0 && oy >= 0 && ox + w <= cw && oy + h <= ch)
                {
                    canvas.SetPixels(ox, oy, w, h, crop.GetPixels());
                }
                canvas.Apply();
                File.WriteAllBytes(file, canvas.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (crop != null) Object.Destroy(crop);
                if (canvas != null) Object.Destroy(canvas);
            }
        }

        // ---------------------------------------------------------------- 11. sounds

        private static int soundIndex;
        private static UnityWebRequest loading;
        private static string loadingName;
        private static AudioSource speaker;
        private static AudioClip lastClip;

        internal static void NextSound()
        {
            try
            {
                EnsureDirs();
                string[] files = Directory.GetFiles(Dir).Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
                if (files.Length == 0)
                {
                    Probe.Status = "11: put .ogg or .wav files in BepInEx\\GK2-asset-probe\\test\\ first.";
                    return;
                }
                if (loading != null) return;
                string f = files[soundIndex % files.Length];
                soundIndex++;
                AudioType type = f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV : AudioType.OGGVORBIS;
                loading = UnityWebRequestMultimedia.GetAudioClip(new Uri(f).AbsoluteUri, type);
                loadingName = Path.GetFileName(f);
                loading.SendWebRequest();
                Probe.Status = "11: loading " + loadingName;
            }
            catch (Exception e)
            {
                Probe.Warn("your sound failed: " + e);
                Probe.Status = "11 failed: see the log.";
            }
        }

        internal static void Tick()
        {
            if (loading == null || !loading.isDone) return;
            UnityWebRequest req = loading;
            loading = null;
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Probe.Info($"my sound {loadingName}: did not load ({req.error})");
                    Probe.Status = "11: " + loadingName + " did not load.";
                    return;
                }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (lastClip != null)
                {
                    if (speaker != null) speaker.Stop();
                    Object.Destroy(lastClip);
                }
                lastClip = clip;
                if (speaker == null)
                {
                    var go = new GameObject("assetprobe speaker");
                    Object.DontDestroyOnLoad(go);
                    speaker = go.AddComponent<AudioSource>();
                    speaker.spatialBlend = 0f;
                    speaker.playOnAwake = false;
                    speaker.outputAudioMixerGroup = SoundsGroup();
                }
                speaker.PlayOneShot(clip);
                Probe.Info($"my sound {loadingName}: {clip.length:0.00} s, {clip.channels} channels, {clip.frequency} Hz, playing");
                Probe.Status = "11: playing " + loadingName;
            }
            finally
            {
                req.Dispose();
            }
        }

        // The game's mixer (AudioConfig.audioMixer): every group is logged once, and the sound effects one is used, so
        // our sounds follow the game's Sounds volume like its own (none found: plain output, as before).
        private static UnityEngine.Audio.AudioMixerGroup SoundsGroup()
        {
            try
            {
                var cfg = LazyBearTechnology.LazySingletonSO<LazyBearTechnology.AudioConfig>.Instance;
                var mixer = cfg != null ? cfg.audioMixer : null;
                if (mixer == null)
                {
                    Probe.Info("sounds: the game's mixer was not found; plain output (the game's volume settings don't apply)");
                    return null;
                }
                var groups = mixer.FindMatchingGroups(string.Empty);
                Probe.Info($"sounds: the game's mixer '{mixer.name}' has {groups.Length} groups: {string.Join(", ", groups.Select(g => g.name))}");
                string[] wanted = { "sfx", "sound", "effect" };
                var pick = groups.FirstOrDefault(g => wanted.Any(w => g.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
                Probe.Info("sounds: played through " + (pick != null ? "the group '" + pick.name + "'" : "no group (none looks like sound effects): plain output"));
                return pick;
            }
            catch (Exception e)
            {
                Probe.Warn("sounds: reading the game's mixer failed: " + e.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- clean-up

        internal static void ClearAll()
        {
            Undress("Clear all");
            ForgetFrames();
            foreach (Object o in pictureArt) if (o != null) Object.Destroy(o);
            pictureArt.Clear();
            used.Clear();
            if (speaker != null) Object.Destroy(speaker.gameObject);
            speaker = null;
            if (lastClip != null) Object.Destroy(lastClip);
            lastClip = null;
            if (loading != null)
            {
                loading.Abort();
                loading.Dispose();
                loading = null;
            }
        }
    }
}
