using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GK2AssetProbe
{
    // The probe's questions (docs/external-assets.md):
    //   1. what the game has that we could reuse (every addressable key; the effects the game spawns by name);
    //   2. which shaders a mod can put on its own materials (Shader.Find only finds shaders built into the game);
    //   3. what the donkey's art is made of (sprite size, pixels per unit, the textures its shader reads besides the
    //      picture, the frames per clip): the bill for a replacement animal;
    //   4. whether a picture from outside the game lights like the game's own art: one picture drawn five ways beside
    //      the donkey (a control made from the donkey's own frame, then the outside picture with a plain sprite shader,
    //      with the donkey's shader alone, and with the donkey's shader and white or black extra textures);
    //   5-7. flames around the donkey, fiery steps, and the game's own effects played at the donkey.
    //   8. the game's own flame materials (its fire textures, cut into frames as its particles cut them) for 5 and 6;
    //   9-11. your own pictures, donkey frames and sounds from test/ (MyArt.cs);
    //   12-13. the game's own lamp fire copied whole, around the donkey and at its steps (LampFlames.cs).
    // Everything drawn is the probe's own GameObject; the donkey is only read.
    internal static class Probe
    {
        private const string Tag = "[assetprobe] ";
        private static ManualLogSource log;
        private static string outDir;
        internal static string OutDir => outDir;
        internal static string Status = "Ready. Stand next to the donkey (or ride it).";

        // the donkey found last (nearest to the keeper)
        private static SpriteRenderer body, head;
        internal static SpriteRenderer Body => body;
        internal static SpriteRenderer Head => head;
        private static Animator anim;

        private static Texture2D cardTex, flameTex;
        private static readonly List<Object> owned = new List<Object>();      // materials, sprites, textures made per use
        private static readonly List<(GameObject Go, string Label)> pictures = new List<(GameObject, string)>();

        internal static void Init(ManualLogSource l)
        {
            log = l;
            outDir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2-asset-probe");
            SceneManager.activeSceneChanged += (a, b) => ClearScene("the scene changed");
        }

        internal static void Info(string s) => log.LogInfo(Tag + s);
        internal static void Warn(string s) => log.LogWarning(Tag + s);

        // ---------------------------------------------------------------- the donkey

        internal static bool FindDonkey() => FindDonkey(false);

        // A fresh look, ignoring the remembered donkey. True when the donkey's view changed since (or was lost).
        internal static bool Recheck()
        {
            SpriteRenderer was = body;
            if (!FindDonkey(true)) return was != null;
            return body != was;
        }

        private static bool FindDonkey(bool fresh)
        {
            if (!fresh && body != null && body.gameObject.activeInHierarchy && anim != null)
            {
                return true;
            }
            SpriteRenderer before = body;
            body = head = null;
            anim = null;
            PlayerController pc = MainGame.Instance != null ? MainGame.PlayerController : null;
            if (pc == null)
            {
                Status = "Not in a game.";
                return false;
            }
            Vector3 at = pc.transform.position;
            float best = 30f * 30f;
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.name != "bdy" || sr.sprite == null || !sr.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (Mathf.Abs(sr.transform.lossyScale.y) < 1.3f)
                {
                    continue; // a foal (drawn at 0.6 size), never the donkey the probe works on
                }
                Animator a = sr.GetComponentInParent<Animator>();
                if (a == null || a.runtimeAnimatorController == null || a.runtimeAnimatorController.name.IndexOf("Donkey", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                float d = (sr.transform.position - at).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    body = sr;
                    anim = a;
                }
            }
            if (body == null)
            {
                Status = "No donkey within 30 units of the keeper.";
                return false;
            }
            Transform hed = body.transform.parent != null ? body.transform.parent.Find("hed") : null;
            head = hed != null ? hed.GetComponent<SpriteRenderer>() : null;
            if (body != before)
            {
                Info($"donkey: {Path(body.transform)} ({Mathf.Sqrt(best):0.0} from the keeper), animator {anim.runtimeAnimatorController.name}, view {body.GetInstanceID()}");
            }
            return true;
        }

        internal static Transform Root => anim != null ? (anim.transform.parent != null ? anim.transform.parent : anim.transform) : null;

        // Where the donkey stands as drawn: the root's height and depth, under the middle of the drawn body. The root is
        // the ground anchor, and the body sprite sits beside it, on the side that flips with the facing (the API's
        // centering cancels the same offset, Mount.cs bodySprite); a ring round the root sat off to one side (2026-10-17).
        internal static Vector3 Center
        {
            get
            {
                Transform r = Root;
                if (r == null) return Vector3.zero;
                if (body == null) return r.position;
                Vector3 c = body.bounds.center;
                return new Vector3(c.x, r.position.y, r.position.z);
            }
        }

        internal static string PathOf(Transform t) => Path(t);

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int n = 0; t != null && n < 6; t = t.parent, n++) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---------------------------------------------------------------- 1. names

        internal static void DumpKeys()
        {
            try
            {
                Directory.CreateDirectory(outDir);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                int locators = 0;
                foreach (var loc in Addressables.ResourceLocators)
                {
                    locators++;
                    foreach (object k in loc.Keys)
                    {
                        if (k is string s && !IsHash(s)) keys.Add(s);
                    }
                }
                var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                File.WriteAllLines(System.IO.Path.Combine(outDir, "addressable-keys.txt"), sorted);

                // the effects WorldFX.Spawn knows by name (the pool's start list), and every bare "<name>.prefab" key
                var fx = new List<string>();
                try
                {
                    foreach (var item in WorldFXPoolInitialSizeConfig.Instance.ActiveInitialSizeConfigs.List) fx.Add(item.type);
                }
                catch (Exception e)
                {
                    Info("the effects' start list could not be read: " + e.Message);
                }
                var bare = sorted.Where(k => k.EndsWith(".prefab", StringComparison.Ordinal) && k.IndexOf('/') < 0).ToList();
                File.WriteAllLines(System.IO.Path.Combine(outDir, "effects.txt"),
                    new[] { "# the pool's start list (WorldFXPoolInitialSizeConfig): " + fx.Count }.Concat(fx.OrderBy(x => x))
                    .Concat(new[] { "", "# every bare <name>.prefab key (what WorldFX.Spawn can load): " + bare.Count }).Concat(bare));

                Info($"{locators} locators, {sorted.Count} names (hashes left out) -> {outDir}\\addressable-keys.txt; effects: {fx.Count} in the start list, {bare.Count} bare prefab keys -> effects.txt");
                Info("effects in the start list: " + string.Join(", ", fx.OrderBy(x => x)));
                string[] words = { "fire", "flame", "burn", "ember", "spark", "torch", "candle", "smoke", "glow", "lava", "step", "foot", "trail", "dust",
                                   "rat", "horse", "mule", "cow", "goat", "pig", "dog", "wolf", "bat", "spider", "sheep", "bear", "cat", "mount" };
                foreach (string w in words)
                {
                    var hits = sorted.Where(k => Contains(k, w)).ToList();
                    if (hits.Count > 0)
                    {
                        Info($"'{w}': {hits.Count} names, e.g. {string.Join(", ", hits.Take(25))}");
                    }
                }
                Status = $"1: {sorted.Count} names written to BepInEx\\GK2-asset-probe\\.";
            }
            catch (Exception e)
            {
                log.LogWarning(Tag + "the name dump failed: " + e);
                Status = "1 failed: see the log.";
            }
        }

        // A word on its own in a path or name ("rat" in "rat_01", not in "decoration").
        private static bool Contains(string k, string w)
        {
            int i = 0;
            while ((i = k.IndexOf(w, i, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                bool startOk = i == 0 || !char.IsLetter(k[i - 1]);
                int e = i + w.Length;
                bool endOk = e >= k.Length || !char.IsLetter(k[e]) || (e + 1 <= k.Length && k[e] == 's' && (e + 1 == k.Length || !char.IsLetter(k[e + 1])));
                if (startOk && endOk) return true;
                i = e;
            }
            return false;
        }

        private static bool IsHash(string s)
        {
            if (s.Length != 32) return false;
            foreach (char c in s) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        // ---------------------------------------------------------------- 2. shaders

        private static readonly string[] ShaderNames =
        {
            "Sprites/Default", "Custom/3D Sprite", "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended Premultiply", "Particles/Standard Unlit", "Particles/Standard Surface",
            "Mobile/Particles/Additive", "Mobile/Particles/Alpha Blended", "Unlit/Transparent", "Unlit/Texture", "UI/Default", "Standard",
        };

        // the choices for the flames and steps: a plain shader by name, a copy of the game's own effect material, or one
        // of the game's flame materials (8) with its own texture, cut into frames the way the game's particles cut it
        private sealed class Choice
        {
            internal string Name;
            internal Func<Material> Make;
            internal bool KeepTexture;          // the game's texture, not our flame.png
            internal ParticleSystem SheetFrom;  // a game particle system drawn with it: its frame grid is copied
        }

        private static readonly List<Choice> effectShaders = new List<Choice>();
        private static readonly List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material>> flameHandles =
            new List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material>>();
        private static int effectShader;
        private static Material effectMat;
        internal static string EffectShaderName => effectShaders.Count > 0 ? effectShaders[effectShader].Name : "run 2 first: Sprites/Default";

        internal static void Shaders()
        {
            try
            {
                Directory.CreateDirectory(outDir);
                Info($"Unity {Application.unityVersion}, graphics {SystemInfo.graphicsDeviceType}, colour space {QualitySettings.activeColorSpace}, render pipeline {(GraphicsSettings.currentRenderPipeline == null ? "built-in" : GraphicsSettings.currentRenderPipeline.name)}, asset bundles loaded now {AssetBundle.GetAllLoadedAssetBundles().Count()}");
                effectShaders.RemoveAll(c => !c.KeepTexture);
                foreach (string n in ShaderNames)
                {
                    Shader s = Shader.Find(n);
                    Info($"Shader.Find(\"{n}\"): {(s == null ? "not in the game" : "found, supported " + s.isSupported)}");
                    if (s != null && s.isSupported && n != "Custom/3D Sprite" && n != "Standard")
                    {
                        string name = n;
                        effectShaders.Add(new Choice { Name = name, Make = () => new Material(Shader.Find(name)) });
                    }
                }
                // every shader loaded right now (the game's own included), to a file
                var all = Resources.FindObjectsOfTypeAll<Shader>().Select(s => s.name).Distinct().OrderBy(x => x).ToList();
                File.WriteAllLines(System.IO.Path.Combine(outDir, "shaders-loaded.txt"), all);
                Info($"{all.Count} shaders loaded now -> shaders-loaded.txt");

                // the game's own smoke puff: what its particles are drawn with
                var h = Addressables.LoadAssetAsync<GameObject>("puff_npc.prefab");
                GameObject puff = h.WaitForCompletion();
                if (puff != null)
                {
                    foreach (Renderer r in puff.GetComponentsInChildren<Renderer>(true))
                    {
                        Material m = r.sharedMaterial;
                        Info($"puff_npc: {r.GetType().Name} '{r.name}', layer {r.gameObject.layer}, sorting {r.sortingLayerName}/{r.sortingOrder}, material {m?.name}, shader {m?.shader?.name}, texture {m?.mainTexture?.name}");
                        if (m != null && !effectShaders.Any(e => e.Name.StartsWith("game puff")))
                        {
                            Material copy = new Material(m) { name = "assetprobe puff copy" };
                            owned.Add(copy);
                            effectShaders.Insert(0, new Choice { Name = "game puff (" + m.shader?.name + ")", Make = () => new Material(copy) });
                        }
                    }
                }
                else
                {
                    Info("puff_npc.prefab did not load");
                }
                if (h.IsValid()) Addressables.Release(h);
                effectShader = 0;
                RebuildEffectMaterial();
                Status = $"2: {effectShaders.Count} shaders usable for effects; see the log.";
            }
            catch (Exception e)
            {
                log.LogWarning(Tag + "the shader check failed: " + e);
                Status = "2 failed: see the log.";
            }
        }

        internal static void NextEffectShader()
        {
            if (effectShaders.Count == 0)
            {
                Shaders();
                return;
            }
            effectShader = (effectShader + 1) % effectShaders.Count;
            RebuildEffectMaterial();
        }

        private static void RebuildEffectMaterial()
        {
            Material old = effectMat;
            Shader fallback = Shader.Find("Sprites/Default");
            if (effectShaders.Count == 0 && fallback == null)
            {
                Info("no shader for the effects: run 2 and see the log");
                effectMat = old;
                return;
            }
            Choice c = effectShaders.Count > 0 ? effectShaders[effectShader] : null;
            effectMat = c != null ? c.Make() : new Material(fallback);
            effectMat.name = "assetprobe effect";
            if (c == null || !c.KeepTexture) effectMat.mainTexture = Flame();
            if (flames != null) DressParticles(flames.GetComponent<ParticleSystem>(), c);
            if (stepsGo != null) DressParticles(stepsGo.GetComponent<ParticleSystem>(), c);
            if (old != null) Object.Destroy(old);
            Info("effects now drawn with " + EffectShaderName);
        }

        // The material and frame grid for a particle system of ours.
        private static void DressParticles(ParticleSystem ps, Choice c)
        {
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = effectMat;
            var tsa = ps.textureSheetAnimation;
            if (c?.SheetFrom == null)
            {
                tsa.enabled = false;
                return;
            }
            var from = c.SheetFrom.textureSheetAnimation;
            tsa.enabled = from.enabled;
            tsa.mode = from.mode;
            tsa.numTilesX = from.numTilesX;
            tsa.numTilesY = from.numTilesY;
            tsa.animation = from.animation;
            tsa.rowMode = from.rowMode;
            tsa.rowIndex = from.rowIndex;
            tsa.frameOverTime = from.frameOverTime;
            tsa.startFrame = from.startFrame;
            tsa.cycleCount = from.cycleCount;
        }

        // ---------------------------------------------------------------- 8. the game's flame materials

        private static readonly string[] FlameMaterials =
        {
            "Assets/AddressableAssets/Materials/flame_fx.mat",
            "Assets/AddressableAssets/Materials/FX/Fire_1_fx (MultiplyDouble).mat",
            "Assets/AddressableAssets/Materials/FX/Fire_2_fx (AlphaBlendet).mat",
            "Assets/AddressableAssets/Materials/techpoint drop fire fx.mat",
            "Assets/AddressableAssets/Materials/techpoint drop fire fx 1.mat",
            "Assets/AddressableAssets/Materials/sparks_fx.mat",
            "Assets/AddressableAssets/Materials/FX/Sparks_fx (MultiplyDouble).mat",
            "Assets/AddressableAssets/Materials/Glow.mat",
        };

        internal static void GameFlames()
        {
            try
            {
                if (effectShaders.Count == 0) Shaders();
                if (effectShaders.Any(c => c.KeepTexture))
                {
                    // loaded already: jump to the first of them
                    effectShader = effectShaders.FindIndex(c => c.KeepTexture);
                    RebuildEffectMaterial();
                    return;
                }
                // the game's particle systems in memory, by material: whose frame grid each flame material is cut by
                var users = new Dictionary<Material, List<ParticleSystemRenderer>>();
                foreach (var r in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
                {
                    if (r.sharedMaterial == null || r.name.StartsWith("assetprobe")) continue;
                    if (!users.TryGetValue(r.sharedMaterial, out var l)) users[r.sharedMaterial] = l = new List<ParticleSystemRenderer>();
                    l.Add(r);
                }
                int added = 0, at = effectShaders.Count;
                foreach (string key in FlameMaterials)
                {
                    var h = Addressables.LoadAssetAsync<Material>(key);
                    Material m = h.WaitForCompletion();
                    if (m == null)
                    {
                        Info($"game flame material {key}: did not load");
                        if (h.IsValid()) Addressables.Release(h);
                        continue;
                    }
                    flameHandles.Add(h);
                    Texture t = m.mainTexture;
                    users.TryGetValue(m, out var used);
                    ParticleSystem sheet = null;
                    var notes = new List<string>();
                    if (used != null)
                    {
                        foreach (var r in used)
                        {
                            var ps = r.GetComponent<ParticleSystem>();
                            if (ps == null) continue;
                            var tsa = ps.textureSheetAnimation;
                            notes.Add($"{Path(r.transform)} (scene {(r.gameObject.scene.IsValid() ? r.gameObject.scene.name : "none: a prefab")}, sheet {(tsa.enabled ? tsa.numTilesX + "x" + tsa.numTilesY : "off")}, size {ps.main.startSize.constant:0.##}/{ps.main.startSize.constantMax:0.##}, life {ps.main.startLifetime.constant:0.##}/{ps.main.startLifetime.constantMax:0.##}, render {r.renderMode})");
                            if (sheet == null || (tsa.enabled && !sheet.textureSheetAnimation.enabled)) sheet = ps;
                        }
                    }
                    Info($"game flame material {m.name}: shader {m.shader?.name}, texture {t?.name} {t?.width}x{t?.height}, keywords [{string.Join(" ", m.shaderKeywords)}], queue {m.renderQueue}; used by {(notes.Count == 0 ? "no particle system in memory now" : string.Join("; ", notes))}");
                    Material src = m;
                    string label = m.name;
                    effectShaders.Add(new Choice { Name = "game " + label, Make = () => new Material(src), KeepTexture = true, SheetFrom = sheet });
                    added++;
                }
                if (added == 0)
                {
                    Status = "8: no game flame material loaded; see the log.";
                    return;
                }
                effectShader = at;
                RebuildEffectMaterial();
                Status = $"8: {added} game flame materials added to \"Next shader\"; turn on 5 and 6.";
            }
            catch (Exception e)
            {
                log.LogWarning(Tag + "the game flame materials failed: " + e);
                Status = "8 failed: see the log.";
            }
        }

        // ---------------------------------------------------------------- 3. the donkey's art

        private static float sampleUntil = -1f;
        private static readonly HashSet<string> allBody = new HashSet<string>(), allHead = new HashSet<string>();
        private static readonly Dictionary<string, (HashSet<string> Body, HashSet<string> Head, int Frames)> perClip = new Dictionary<string, (HashSet<string>, HashSet<string>, int)>();

        internal static void ArtSpec()
        {
            if (!FindDonkey()) return;
            try
            {
                Directory.CreateDirectory(outDir);
                LogSprite("bdy", body);
                if (head != null) LogSprite("hed", head);
                Material m = body.sharedMaterial;
                if (m != null && m.shader != null)
                {
                    Shader s = m.shader;
                    var props = new StringBuilder();
                    for (int i = 0; i < s.GetPropertyCount(); i++)
                    {
                        string pn = s.GetPropertyName(i);
                        var pt = s.GetPropertyType(i);
                        props.Append(pn).Append(':').Append(pt);
                        if (pt == ShaderPropertyType.Texture && m.HasProperty(pn)) props.Append('=').Append(m.GetTexture(pn)?.name ?? "none");
                        if (pt == ShaderPropertyType.Float || pt == ShaderPropertyType.Range) props.Append('=').Append(m.GetFloat(pn).ToString("0.###"));
                        props.Append("  ");
                    }
                    Info($"bdy material {m.name}, shader {s.name}, keywords [{string.Join(" ", m.shaderKeywords)}], render queue {m.renderQueue}; properties: {props}");
                }
                var clips = anim.runtimeAnimatorController.animationClips.GroupBy(c => c.name).Select(g => g.First()).OrderBy(c => c.name);
                Info("clips: " + string.Join(", ", clips.Select(c => $"{c.name} {c.length:0.00}s@{c.frameRate:0}fps")));
                Info($"animator layers {anim.layerCount}; the root {Path(Root)} scale {Root.lossyScale}, gfx rotation {body.transform.rotation.eulerAngles}");
                perClip.Clear();
                allBody.Clear();
                allHead.Clear();
                sampleUntil = Time.time + 30f;
                Status = "3: sampling frames for 30 s: stand still, walk, ride, stop.";
            }
            catch (Exception e)
            {
                log.LogWarning(Tag + "the art spec failed: " + e);
                Status = "3 failed: see the log.";
            }
        }

        private static void LogSprite(string part, SpriteRenderer r)
        {
            Sprite sp = r.sprite;
            if (sp == null)
            {
                Info($"{part}: no sprite now");
                return;
            }
            Texture2D t = sp.texture;
            if (r.HasPropertyBlock())
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                Info($"{part}: has a property block (per-renderer shader data; empty {mpb.isEmpty}): copied onto the test pictures");
            }
            else
            {
                Info($"{part}: no property block");
            }
            Info($"{part}: sprite {sp.name}, texture {t?.name} {t?.width}x{t?.height} {t?.format} filter {t?.filterMode}, rect {sp.rect}, pivot px {sp.pivot}, pixels per unit {sp.pixelsPerUnit}, packed {sp.packed}, layer {r.gameObject.layer} ({LayerMask.LayerToName(r.gameObject.layer)}), sorting {r.sortingLayerName}/{r.sortingOrder}, scale {r.transform.lossyScale}, flip {r.flipX}");
            Dump(t, sp.textureRect, t, part + "-main.png");
            int n = sp.GetSecondaryTextureCount();
            if (n > 0)
            {
                var sec = new SecondarySpriteTexture[n];
                sp.GetSecondaryTextures(sec);
                foreach (var st in sec)
                {
                    Texture2D tex = st.texture;
                    Info($"{part}: extra texture '{st.name}' = {tex?.name} {tex?.width}x{tex?.height} {tex?.format}");
                    if (tex != null) Dump(tex, sp.textureRect, t, part + "-" + st.name.Trim('_') + ".png");
                }
            }
            else
            {
                Info($"{part}: no extra textures");
            }
        }

        // The sprite's piece of a texture, to a PNG (through a render texture: the game's textures aren't readable).
        private static void Dump(Texture2D tex, Rect rectInMain, Texture2D main, string file)
        {
            if (tex == null || main == null) return;
            float sx = (float)tex.width / main.width, sy = (float)tex.height / main.height;
            var r = new RectInt(Mathf.FloorToInt(rectInMain.x * sx), Mathf.FloorToInt(rectInMain.y * sy), Mathf.CeilToInt(rectInMain.width * sx), Mathf.CeilToInt(rectInMain.height * sy));
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Texture2D crop = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                crop = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                crop.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                crop.Apply();
                File.WriteAllBytes(System.IO.Path.Combine(outDir, file), crop.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (crop != null) Object.Destroy(crop);
            }
        }

        private static void Sample()
        {
            if (sampleUntil < 0f) return;
            if (body == null || anim == null)
            {
                sampleUntil = -1f;
                Info("frame sampling stopped: the donkey is gone");
                return;
            }
            var playing = new List<string>();
            for (int layer = 0; layer < anim.layerCount; layer++)
            {
                if (layer > 0 && anim.GetLayerWeight(layer) <= 0f) continue;
                var infos = anim.GetCurrentAnimatorClipInfo(layer);
                if (infos.Length == 0) continue;
                playing.Add("L" + layer + ":" + infos.OrderByDescending(i => i.weight).First().clip.name);
            }
            string clip = playing.Count == 0 ? "(none)" : string.Join(" + ", playing);
            if (body.sprite != null && allBody.Add(body.sprite.name)) MyArt.ExportFrame(body.sprite);
            if (head != null && head.sprite != null && allHead.Add(head.sprite.name)) MyArt.ExportFrame(head.sprite);
            if (!perClip.TryGetValue(clip, out var e))
            {
                e = (new HashSet<string>(), new HashSet<string>(), 0);
            }
            if (body.sprite != null) e.Body.Add(body.sprite.name);
            if (head != null && head.sprite != null) e.Head.Add(head.sprite.name);
            perClip[clip] = (e.Body, e.Head, e.Frames + 1);
            if (Time.time >= sampleUntil)
            {
                sampleUntil = -1f;
                int total = 0;
                foreach (var kv in perClip.OrderBy(k => k.Key))
                {
                    total += kv.Value.Body.Count + kv.Value.Head.Count;
                    Info($"clip {kv.Key} ({kv.Value.Frames} frames seen): body {kv.Value.Body.Count} pictures [{string.Join(" ", kv.Value.Body.OrderBy(x => x))}], head {kv.Value.Head.Count} [{string.Join(" ", kv.Value.Head.OrderBy(x => x))}]");
                }
                Info($"frame sampling done: {perClip.Count} clip combinations seen; distinct pictures overall: body {allBody.Count}, head {allHead.Count} (the art bill for one facing); each saved whole to donkey-frames\\ (templates to paint over)");
                Status = $"3 done: body {allBody.Count} + head {allHead.Count} pictures; see the log.";
            }
        }

        // ---------------------------------------------------------------- 4. test pictures

        internal static bool HasLabels => pictures.Count > 0;

        private static Texture2D Card() => cardTex != null ? cardTex : (cardTex = LoadArt("card.png"));
        private static Texture2D Flame() => flameTex != null ? flameTex : (flameTex = LoadArt("flame.png"));

        // From BepInEx/GK2-asset-probe/<name> if there is one (your own art), else the copy inside the DLL.
        private static Texture2D LoadArt(string name)
        {
            byte[] bytes;
            string loose = System.IO.Path.Combine(outDir, name);
            string from;
            if (File.Exists(loose))
            {
                bytes = File.ReadAllBytes(loose);
                from = loose;
            }
            else
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GK2AssetProbe." + name))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                from = "inside the DLL";
            }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "assetprobe " + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            bool ok = t.LoadImage(bytes);
            Info($"{name} loaded from {from}: {ok}, {t.width}x{t.height} {t.format}");
            return t;
        }

        internal static void TestPictures()
        {
            if (!FindDonkey()) return;
            try
            {
                ClearPictures();
                Sprite src = body.sprite;
                Texture2D card = Card();
                float ppu = src.pixelsPerUnit;
                SecondarySpriteTexture[] sec = null;
                int n = src.GetSecondaryTextureCount();
                if (n > 0)
                {
                    sec = new SecondarySpriteTexture[n];
                    src.GetSecondaryTextures(sec);
                }
                Shader plain = Shader.Find("Sprites/Default");

                // 0: control, the donkey's own frame through our own sprite and material copy (should look like the donkey)
                Sprite control = sec != null
                    ? Sprite.Create(src.texture, src.textureRect, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect, Vector4.zero, false, sec)
                    : Sprite.Create(src.texture, src.textureRect, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
                Add("0 control (donkey frame)", control, new Material(body.sharedMaterial));
                Add("A card, Sprites/Default", Sprite.Create(card, new Rect(0, 0, card.width, card.height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect), plain != null ? new Material(plain) : null);
                Add("B card, donkey shader", Sprite.Create(card, new Rect(0, 0, card.width, card.height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect), new Material(body.sharedMaterial));
                if (sec != null)
                {
                    Add("C card + white extras", WithExtras(card, sec, Color.white, ppu), new Material(body.sharedMaterial));
                    Add("D card + black extras", WithExtras(card, sec, Color.black, ppu), new Material(body.sharedMaterial));
                }
                Info($"test pictures: {pictures.Count} beside the donkey, left to right: {string.Join(" | ", pictures.Select(p => p.Label))}");
                Status = "4: compare the pictures by day and by night (the director's time hold).";
            }
            catch (Exception e)
            {
                log.LogWarning(Tag + "the test pictures failed: " + e);
                Status = "4 failed: see the log.";
            }
        }

        private static Sprite WithExtras(Texture2D card, SecondarySpriteTexture[] like, Color fill, float ppu)
        {
            var sec = new SecondarySpriteTexture[like.Length];
            for (int i = 0; i < like.Length; i++)
            {
                var t = new Texture2D(card.width, card.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "assetprobe extra " + like[i].name };
                var px = new Color32[card.width * card.height];
                Color32 c = fill;
                for (int p = 0; p < px.Length; p++) px[p] = c;
                t.SetPixels32(px);
                t.Apply(false, false);
                owned.Add(t);
                sec[i] = new SecondarySpriteTexture { name = like[i].name, texture = t };
            }
            return Sprite.Create(card, new Rect(0, 0, card.width, card.height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect, Vector4.zero, false, sec);
        }

        internal static void Add(string label, Sprite sprite, Material mat)
        {
            owned.Add(sprite);
            if (mat != null)
            {
                mat.name = "assetprobe " + label;
                owned.Add(mat);
            }
            Camera cam = Camera.main;
            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            var go = new GameObject("assetprobe " + label) { layer = body.gameObject.layer };
            go.transform.position = Root.position + right * (1.3f * (pictures.Count + 1));
            go.transform.rotation = body.transform.rotation;
            Vector3 s = body.transform.lossyScale;
            go.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (mat != null) r.sharedMaterial = mat;
            r.sortingLayerID = body.sortingLayerID;
            r.sortingOrder = body.sortingOrder;
            if (mat != null && mat.shader == body.sharedMaterial.shader && body.HasPropertyBlock())
            {
                // the game's per-renderer data on the donkey (if any) goes with its shader, or the control can't match
                var mpb = new MaterialPropertyBlock();
                body.GetPropertyBlock(mpb);
                r.SetPropertyBlock(mpb);
            }
            pictures.Add((go, label));
        }

        private static GUIStyle labelStyle;

        internal static void DrawLabels()
        {
            if (pictures.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.box) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            foreach (var (go, label) in pictures)
            {
                if (go == null) continue;
                Vector3 p = cam.WorldToViewportPoint(go.transform.position + Vector3.up * 2.0f);
                if (p.z <= 0f) continue;
                GUI.Box(new Rect(p.x * Screen.width - 50f, (1f - p.y) * Screen.height - 20f, 100f, 40f), label, labelStyle);
            }
        }

        internal static void ClearPictures()
        {
            foreach (var (go, _) in pictures) if (go != null) Object.Destroy(go);
            pictures.Clear();
        }

        // ---------------------------------------------------------------- 5. flames, 6. steps

        private static GameObject flames;
        internal static bool FlamesOn => flames != null;

        internal static void ToggleFlames()
        {
            if (flames != null)
            {
                Object.Destroy(flames);
                flames = null;
                Info("flames off");
                return;
            }
            if (!FindDonkey()) return;
            if (effectMat == null) RebuildEffectMaterial();
            flames = new GameObject("assetprobe flames") { layer = body.gameObject.layer };
            flames.transform.position = Root.position;
            var ps = flames.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 60f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.4f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var pr = flames.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sortingLayerID = body.sortingLayerID;
            pr.sortingOrder = body.sortingOrder;
            DressParticles(ps, effectShaders.Count > 0 ? effectShaders[effectShader] : null);
            ps.Play();
            Info($"flames on ({EffectShaderName}), layer {flames.layer}, sorting {pr.sortingLayerName}/{pr.sortingOrder}");
        }

        // One particle per step, from a world-space system that emits nothing by itself: a game material shows its own
        // texture (a sprite would force ours), and the frame grid applies as for the flames.
        private static GameObject stepsGo;
        private static bool stepsOn;
        private static Vector3 lastStep;
        private static bool leftFoot;
        private const float StepLife = 1.4f;
        internal static bool StepsOn => stepsOn;

        internal static void ToggleSteps()
        {
            if (stepsOn)
            {
                stepsOn = false;
                Info("steps off");
                return;
            }
            if (!FindDonkey()) return;
            if (effectMat == null) RebuildEffectMaterial();
            if (stepsGo == null)
            {
                stepsGo = new GameObject("assetprobe steps") { layer = body.gameObject.layer };
                var ps = stepsGo.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.startLifetime = StepLife;
                main.startSpeed = 0f;
                main.startSize = 0.45f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 100;
                var em = ps.emission;
                em.rateOverTime = 0f;
                var shape = ps.shape;
                shape.enabled = false;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
                var pr = stepsGo.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortingLayerID = body.sortingLayerID;
                pr.sortingOrder = body.sortingOrder;
                DressParticles(ps, effectShaders.Count > 0 ? effectShaders[effectShader] : null);
                ps.Play();
            }
            lastStep = Root.position;
            stepsOn = true;
            Info($"steps on ({EffectShaderName})");
        }

        private static void TickSteps()
        {
            if (!stepsOn || stepsGo == null || Root == null || body == null) return;
            Vector3 p = Root.position;
            if ((p - lastStep).sqrMagnitude < 0.45f * 0.45f) return;
            lastStep = p;
            leftFoot = !leftFoot;
            Camera cam = Camera.main;
            Vector3 side = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            var ep = new ParticleSystem.EmitParams
            {
                position = p + side * (leftFoot ? 0.12f : -0.12f) + Vector3.up * 0.15f,
                applyShapeToPosition = false,
                velocity = Vector3.zero,
            };
            stepsGo.GetComponent<ParticleSystem>().Emit(ep, 1);
        }

        // ---------------------------------------------------------------- 7. the game's own effects

        internal static void PlayGameFx(string name)
        {
            if (!FindDonkey() || string.IsNullOrEmpty(name)) return;
            bool known = false;
            foreach (var loc in Addressables.ResourceLocators)
            {
                if (loc.Locate(name + ".prefab", typeof(GameObject), out _))
                {
                    known = true;
                    break;
                }
            }
            if (!known)
            {
                Info($"no effect '{name}' (no key {name}.prefab); see effects.txt");
                Status = $"7: no effect '{name}'.";
                return;
            }
            WorldFX fx = WorldFX.Spawn(Root.position, name);
            Info($"game effect '{name}' played at the donkey: {(fx != null ? "ok" : "failed")}");
            Status = $"7: played '{name}'.";
        }

        // ---------------------------------------------------------------- every frame

        internal static void Tick()
        {
            Sample();
            TickSteps();
            MyArt.Recheck();
            MyArt.Tick();
            LampFlames.Tick();
        }

        internal static void Follow()
        {
            if (flames != null && Root != null) flames.transform.position = Root.position + Vector3.up * 0.1f;
            LampFlames.Follow();
        }

        // ---------------------------------------------------------------- clean-up

        private static void ClearScene(string why)
        {
            if (pictures.Count == 0 && flames == null && stepsGo == null && !stepsOn && sampleUntil < 0f && MyArt.Dress == MyArt.Mode.Off && !LampFlames.RingOn && !LampFlames.StepsOn) return;
            ClearPictures();
            MyArt.Undress(why);
            LampFlames.Clear();
            if (flames != null) Object.Destroy(flames);
            flames = null;
            stepsOn = false;
            if (stepsGo != null) Object.Destroy(stepsGo);
            stepsGo = null;
            sampleUntil = -1f;
            body = head = null;
            anim = null;
            Info("cleared: " + why);
        }

        internal static void ClearAll()
        {
            ClearScene("Clear all");
            MyArt.ClearAll();
            body = head = null;
            anim = null;
            foreach (Object o in owned) if (o != null) Object.Destroy(o);
            owned.Clear();
            if (effectMat != null) Object.Destroy(effectMat);
            effectMat = null;
            foreach (var h in flameHandles) if (h.IsValid()) Addressables.Release(h);
            flameHandles.Clear();
            if (cardTex != null) Object.Destroy(cardTex);
            if (flameTex != null) Object.Destroy(flameTex);
            cardTex = flameTex = null;
            effectShaders.Clear();
            effectShader = 0;
            Status = "Cleared.";
        }
    }
}
