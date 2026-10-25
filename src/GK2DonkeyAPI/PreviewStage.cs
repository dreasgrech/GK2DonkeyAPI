using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The settings' picture of the donkey with its cart (LookPreview, MountsPanel), drawn by the game itself: a copy of the
    // donkey's look from the game's files (Assets/AddressableAssets/WGOs/<id>.prefab, the file the game draws the
    // donkey from) far below the world, wearing a paper donkey's colours and lantern (its Colors.Rig and Lantern.Rig
    // attached to the copy), photographed into a RenderTexture by a camera of its own at the game camera's angle.
    // - Two pictures of the one copy, each with its own camera and framing (View): the donkey with its cart, and the
    //   donkey alone (every renderer but the donkey's own sprites switched off for that photo only, then on again:
    //   not a list of the cart's parts, since the lantern's hanging lamp is made later, on its first tick, and was
    //   missed that way). One copy, so the colour and lantern rigs, which hold one root each, dress both.
    // - Kept apart by distance, not by layer: the game names all 32 layers (found 2026-10-02), so the copy keeps its
    //   own; the game's cameras never look 5000 units down, and ours see nothing else there.
    // - The copy keeps only what draws (transforms, mesh and sprite renderers, the game's Object3DMesh, which sets up
    //   the 3D cart's materials on its own) and its animator, walking on the spot, with its events off. Every other
    //   script, collider, light, sound is removed before the copy is ever switched on. The game's file stays loaded
    //   while the copy exists (released, the copy's meshes went with it).
    // - The cart's parts on as in Cart style, the loaded-cart body and the effect helpers off.
    // - A point light of its own with a short reach: lit the same in the main menu and at night, the world untouched.
    // - Each picture framed on what's drawn (Frame, then Tighten on the read-back pixels), again whenever it's changed
    //   or something reaches the edge.
    // Any failure: the stage is taken down for the session, logged once, and the picture is the donkey alone
    // (Colors.BuildPreview) or nothing. The game is never touched.
    internal static class PreviewStage
    {
        private const float GameCameraPitch = 53.130104f; // MainCamera.cs

        // The prefab's helpers that only feed the game's effects: the shadow-casting sprite and cylinder, and the mask
        // that bends the grass (7.68 units wide: it had made the picture tiny).
        private static readonly string[] Helpers = { "gfx (90 rotated)", "Cylinder Shadowcaster", "DeformTextureSprite" };

        private static readonly string[] CartPartNames =
        {
            "wheel_left", "wheel_right", "light_sway",
            "donkey_cart_wheel_f", "donkey_cart_wheel_b", "donkey_cart_bars",
            "donkey_cart_empty_sway", "donkey_cart_lantern_sway",
        };

        // One picture: its camera, its texture, its framing.
        private sealed class View
        {
            internal readonly bool WithCart;
            internal readonly string Name;
            internal Camera Cam;
            internal RenderTexture Rt;
            internal bool Reframe = true;
            internal float NextEdgeCheck, LastAsked = -10f;
            // The edge check (a whole picture read back, 2 to 3.6 ms) runs once a second only for EdgeCheckSeconds after
            // a framing or a tuning change, not as long as the picture shows.
            internal float EdgeChecksUntil;
            internal int EdgeStamp = -1;
            internal int LastRenderFrame = -1;
            internal bool Drawn;               // a finished photo is in Rt (what a look still settling holds on to)
            // The framing measured over the walk a step a frame (Tighten): the camera renders into Scratch meanwhile and Rt
            // keeps the last photo. All twenty steps in one frame held the game 40 to 60 ms on every mount picked in the
            // Mounts panel (the user, 2026-10-24: "the game freezes for some frames" on the dragon; the log's slow frames).
            internal int TightenStep = -1;     // -1: not measuring
            internal int MinX, MinY, MaxX, MaxY;
            internal bool EdgeReframe;         // framed again because something reached the edge: measured anew, not taken from memory
            internal RenderTexture Scratch;
            internal Texture2D Readback;       // one buffer for this view's read-backs (a new texture and array each was 1.3 MB a read)

            internal View(bool withCart)
            {
                WithCart = withCart;
                Name = withCart ? "with the cart" : "the donkey alone";
            }
        }

        private static readonly View cartView = new View(true), donkeyView = new View(false);

        // Each look's framing once measured, per picture: the same mount picked again in the panel is framed at once.
        // Forgotten when the stage is built again; a framing that something outgrows (the edge check) is measured anew.
        private struct Framing
        {
            internal Vector3 CamFromStage, LampFromStage;
            internal float Ortho, Aspect, Near, Far, LampRange;
            internal int W, H;
        }

        private static readonly Dictionary<string, Framing> framings = new Dictionary<string, Framing>(StringComparer.Ordinal);

        private static string FramingKey(View v) => (framedLook ?? "") + "|" + v.Name;

        // The framings measured, kept on disk across sessions (docs/mount-switching-performance.md 2.1): a look is measured
        // once ever, not once a session. Kept under what decides a framing: the look's pictures (their hash), its canvas
        // and pivot, the hitch gap, the stage's pixel density, the game's version, and this file's version; any of them
        // changed is another key, measured anew. A look with no hash (a code skin's own sprites) stays per session.
        private const int FramingFileVersion = 1;
        private static Dictionary<string, Framing> onDisk;
        private static string FramingFile => System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "GK2DonkeyAPI", "framings.txt");

        private static string DiskKey(View v)
        {
            ulong hash = Looks.ContentHash(framedLook);
            if (hash == 0UL) return null;
            Looks.SkinDef d = Looks.Find(framedLook);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float gap = dressedAs != null ? Looks.HitchGapWorldFor(dressedAs) : 0f;
            return string.Join("|", FramingFileVersion.ToString(ci), Application.version, framedLook, v.Name, hash.ToString("x16"),
                d != null ? $"{d.CanvasW}x{d.CanvasH}@{d.PivotX.ToString("0.###", ci)},{d.PivotY.ToString("0.###", ci)}" : "-",
                gap.ToString("0.####", ci), (ppu * Detail).ToString("0.###", ci));
        }

        private static bool FromDisk(View v, out Framing f)
        {
            f = default;
            string key = DiskKey(v);
            if (key == null) return false;
            if (onDisk == null) LoadFramings();
            return onDisk.TryGetValue(key, out f);
        }

        private static void ToDisk(View v, Framing f)
        {
            string key = DiskKey(v);
            if (key == null) return;
            if (onDisk == null) LoadFramings();
            onDisk[key] = f;
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# GK2 Donkey API: the Mounts panel's picture framings, measured once per look (safe to delete)");
                foreach (KeyValuePair<string, Framing> kv in onDisk)
                {
                    Framing x = kv.Value;
                    float[] n = { x.CamFromStage.x, x.CamFromStage.y, x.CamFromStage.z, x.LampFromStage.x, x.LampFromStage.y, x.LampFromStage.z, x.Ortho, x.Aspect, x.Near, x.Far, x.LampRange };
                    sb.Append(kv.Key).Append('\t');
                    foreach (float one in n) sb.Append(one.ToString("R", ci)).Append(' ');
                    sb.Append(x.W.ToString(ci)).Append(' ').Append(x.H.ToString(ci)).AppendLine();
                }
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FramingFile));
                System.IO.File.WriteAllText(FramingFile, sb.ToString());
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("[preview] the framings could not be written (" + e.Message + "); measured again next session.");
            }
        }

        private static void LoadFramings()
        {
            onDisk = new Dictionary<string, Framing>(StringComparer.Ordinal);
            try
            {
                if (!System.IO.File.Exists(FramingFile)) return;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (string line in System.IO.File.ReadAllLines(FramingFile))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string[] p = line.Substring(tab + 1).Trim().Split(' ');
                    if (p.Length != 13) continue;
                    var f = new float[11];
                    bool ok = true;
                    for (int i = 0; i < 11 && ok; i++) ok = float.TryParse(p[i], System.Globalization.NumberStyles.Float, ci, out f[i]);
                    if (!ok || !int.TryParse(p[11], System.Globalization.NumberStyles.Integer, ci, out int w) || !int.TryParse(p[12], System.Globalization.NumberStyles.Integer, ci, out int h) || w < 16 || h < 16) continue;
                    onDisk[line.Substring(0, tab)] = new Framing
                    {
                        CamFromStage = new Vector3(f[0], f[1], f[2]), LampFromStage = new Vector3(f[3], f[4], f[5]),
                        Ortho = f[6], Aspect = f[7], Near = f[8], Far = f[9], LampRange = f[10], W = w, H = h,
                    };
                }
                Plugin.Log.LogInfo($"[preview] {onDisk.Count} framing(s) read from {FramingFile}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("[preview] the framings could not be read (" + e.Message + "); measured anew.");
            }
        }
        private static readonly View[] views = { cartView, donkeyView };

        private static GameObject stage, copy;
        private static Light lamp;
        private static Animator animator;
        private static bool failed, loadStarted, described;
        private static int framesSinceBuilt, lastPoseFrame = -1;
        private static Mount.Instance dressedAs;
        private static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> loading;
        private const int SettleFrames = 8;
        private static Mount.Instance shownPaper;
        private static string framedLook = ""; // the skin the pictures were last framed for (LooksRig.Wearing)
        private static float ppu = 50f;
        private static readonly List<Renderer> donkeyRenderers = new List<Renderer>(); // its body and head sprites
        private static readonly List<Renderer> allRenderers = new List<Renderer>();
        private static readonly List<Renderer> switchedOff = new List<Renderer>();

        // Picture pixels per art pixel. The walk animation rocks the cart body by up to 3 degrees; at one picture pixel
        // per art pixel that tilt snaps into a one-pixel step across the middle of the cart (it looked split in two).
        // The game draws the cart at screen resolution, where the tilt is smooth: so does the picture, at the size it's
        // shown (three canvas units per art pixel at most).
        internal const int Detail = 3;

        // The picture's size in art pixels (the CPU stand-in is drawn at one pixel per art pixel).
        internal static Vector2 ArtSize(Texture pic)
        {
            float d = pic == cartView.Rt || pic == donkeyView.Rt ? Detail : 1f;
            return new Vector2(pic.width / d, pic.height / d);
        }

        // The pictures won't come this session: show the donkey alone (Colors.BuildPreview) instead.
        internal static bool Failed => failed;

        // Every frame (MountRegistry.Tick): each picture asked for a moment ago is drawn again, so the walk plays
        // smoothly between the displays' four-a-second updates.
        internal static void Tick()
        {
            if (shownPaper == null || failed || copy == null)
            {
                return;
            }
            foreach (View v in views)
            {
                if (Time.unscaledTime - v.LastAsked < 0.5f)
                {
                    Picture(shownPaper, v.WithCart, ask: false);
                }
            }
        }

        // The picture of the paper donkey, with its cart or alone, or null (not ready yet, or failed: use the donkey
        // alone from Colors.BuildPreview).
        internal static Texture Picture(Mount.Instance paper, bool withCart = true, bool ask = true)
        {
            if (failed || paper == null)
            {
                return null;
            }
            if (withCart && !Looks.MountWanted(paper).Cart) withCart = false; // a mount that can't pull the cart: the picture without it
            View v = withCart ? cartView : donkeyView;
            if (ask)
            {
                v.LastAsked = Time.unscaledTime;
                shownPaper = paper;
            }
            try
            {
                if (copy == null)
                {
                    Build();
                    return null;
                }
                if (dressedAs != paper)
                {
                    // The previous paper donkey's rigs come off the copy first: they put back what they changed on it
                    // (the lantern's split mesh above all: attached on top of it, the next LampHang saw a mesh it
                    // didn't know and gave up with a warning, 2026-10-18, the colours self-test dressing the stage
                    // before Saddle Up's page did).
                    if (dressedAs != null)
                    {
                        using (Mount.Bind(dressedAs))
                        {
                            dressedAs.ColorsRig.Detach();
                            dressedAs.LanternRig.Detach();
                            dressedAs.LooksRig.Detach();
                        }
                        // the copy's own body and head stay in donkeyRenderers (Build put them there; cleared, the donkey-
                        // alone picture had every renderer off and nothing to frame, 2026-10-18); the previous skin's
                        // overlays are destroyed by its Detach and drop out as nulls below
                    }
                    dressedAs = paper;
                    // Bound, so the first build reads the paper donkey's look: attached unbound, the picture showed the
                    // game's own colours until a setting changed (the video of 2026-10-02 20-57).
                    using (Mount.Bind(paper))
                    {
                        paper.ColorsRig.AttachRoot(copy);
                        paper.ColorsRig.MarkDirty();
                        paper.LanternRig.AttachRoot(copy.transform); // the pole where the tuning puts it, the lamp hanging
                        paper.LooksRig.AttachRoot(copy);              // the skin's overlays count as the donkey's sprites
                        donkeyRenderers.RemoveAll(r => r == null);
                        donkeyRenderers.AddRange(paper.LooksRig.Renderers);
                    }
                    foreach (View each in views)
                    {
                        each.Reframe = true;
                    }
                }
                // The copy is posed once a frame, for both pictures.
                bool newFrame = Time.frameCount != lastPoseFrame;
                if (newFrame)
                {
                    lastPoseFrame = Time.frameCount;
                }
                // Settled before it's first framed and shown: Object3DMesh sets its materials on Start, the lamp is hung on
                // the lantern's first tick, and the walk starts from the idle pose. Framed earlier, the picture changed
                // size a moment after it first showed (2026-10-02).
                if (framesSinceBuilt < SettleFrames)
                {
                    if (newFrame)
                    {
                        if (framesSinceBuilt >= 1)
                        {
                            Pose(paper);
                        }
                        framesSinceBuilt++;
                    }
                    return null;
                }
                if (!described)
                {
                    described = true;
                    Describe("after start");
                }
                if (newFrame)
                {
                    using (Perf.Measure(Perf.S.PrevPose)) Pose(paper);
                    // Another skin worn (the Mount or Skin row changed; a sheet that finished building): framed again
                    // before this frame's picture, on the new pictures' own size, not a second later by the edge check
                    // with the new mount drawn inside the old one's frame.
                    string look = paper.LooksRig.Wearing;
                    if (look != framedLook)
                    {
                        Perf.TraceNote("the picture shows " + look);
                        framedLook = look;
                        foreach (View each in views)
                        {
                            each.Reframe = true;
                        }
                    }
                }
                // A new look still being put together (its sheet building, the Skin key ahead of the worn skin): the last
                // finished photo stays, and the new one comes whole, framed and spaced for it, in one frame (the user's
                // videos, 2026-10-22: the cart's colours first, the new mount ten frames later in the old one's frame and
                // with its hitch gap, then the framing). Nothing to hold on to yet: nothing, rather than a half look.
                if (paper.LooksRig.Settling)
                {
                    return v.Drawn ? v.Rt : null;
                }
                if (Time.frameCount != v.LastRenderFrame)
                {
                    v.LastRenderFrame = Time.frameCount;
                    Render(v);
                    if (v.TightenStep < 0) v.Drawn = true;
                }
                // measuring the new framing (a step a frame): the last finished photo stays meanwhile, as for a look settling
                if (v.TightenStep >= 0) return v.Drawn ? v.Rt : null;
                return v.Rt;
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return null;
            }
        }

        // One photo: for the donkey alone, everything but the donkey's sprites is off just for it (and on again after,
        // also on a failure).
        private static void Render(View v)
        {
            using (Midday.Hold())
            {
                RenderLit(v);
            }
        }

        // The pictures by the outdoor midday light, whatever the hour or the room (the user, 2026-10-22: a picture washed
        // tan at dusk; the colours self-test, 2026-10-19: blue at night). The game lights its scene from its time-of-day
        // presets (EnvironmentEngine.ApplyPreset: the sun and back lights, the ambient colour, GlobalShaderParameters'
        // shader globals); for the stage's own renders only, those are set to the outdoor presets' midday blend and put
        // back at once, before any camera of the game's draws. The colour grading (the main camera's post effect) never
        // reached the stage. Nothing found (the main menu, presets not loaded yet): the scene's light as it is.
        // And what the game draws over its world by place: the vertical fog (by height: the stage, 5000 units below the
        // world, sat under all of it, so a misty hour washed the picture in the fog's colour; the user, 2026-10-22,
        // still at times with the midday light), the light texture (the lamps and fires, sampled by screen place: off
        // the game's screen the stage took its edge) and the clouds' shadows (the same): off for the stage's renders,
        // as each of them switches itself off (VerticalFog, LightRTManager.ClearGlobals, CloudsController), put back after.
        private sealed class Midday : IDisposable
        {
            private static readonly int idVertFog = Shader.PropertyToID("_VertFog");
            private static readonly int idFogIntensity = Shader.PropertyToID("_FogIntensity");
            private static readonly int idFogColor = Shader.PropertyToID("_FogColor");
            private static readonly int idLightRTIntensity = Shader.PropertyToID("_LightRTIntensity");
            private static readonly int idCloudsRTIntensity = Shader.PropertyToID("_CloudsRTIntensity");
            private const string FogKeyword = "VERT_FOG", LightRTKeyword = "USE_LIGHT_RT", CloudsKeyword = "USE_CLOUDS_RT";
            private bool sky, fogKw, lightKw, cloudsKw;
            private float vertFog, lightRT, cloudsRT;
            private static float nextSaid;
            private static readonly int idBacklightColor = Shader.PropertyToID("_BacklightColor");
            private static readonly int idLightMaxBurn = Shader.PropertyToID("_LightMaxBurn");
            private static readonly int idSunLight = Shader.PropertyToID("_SunLight");
            private static readonly int idTimeOfDay = Shader.PropertyToID("_TimeOfDay");
            private static readonly Midday held = new Midday();
            private static LightEnvironmentPreset noon;
            private static bool said;
            private bool on;
            private Light sun, back;
            private Color sunColor, backColor, ambient, gspBack;
            private float sunIntensity, sunShadow, backIntensity, gspBurn, gspSun, gspTime;
            private Quaternion sunRotation;

            internal static IDisposable Hold()
            {
                Midday m = held;
                // the sky's effects by place: off for this render (said every 10 s while pictures are drawn, as they were)
                m.sky = true;
                m.fogKw = Shader.IsKeywordEnabled(FogKeyword); m.lightKw = Shader.IsKeywordEnabled(LightRTKeyword); m.cloudsKw = Shader.IsKeywordEnabled(CloudsKeyword);
                m.vertFog = Shader.GetGlobalFloat(idVertFog); m.lightRT = Shader.GetGlobalFloat(idLightRTIntensity); m.cloudsRT = Shader.GetGlobalFloat(idCloudsRTIntensity);
                if (Time.unscaledTime >= nextSaid)
                {
                    nextSaid = Time.unscaledTime + 10f;
                    Plugin.Log.LogInfo($"[preview] the scene's effects kept off the picture: fog {(m.fogKw ? "on" : "off")} ({m.vertFog:0.##}, intensity {Shader.GetGlobalFloat(idFogIntensity):0.##}, colour {ColorUtility.ToHtmlStringRGB(Shader.GetGlobalColor(idFogColor))}), lamps' light texture {(m.lightKw ? "on" : "off")} ({m.lightRT:0.##}), cloud shadows {(m.cloudsKw ? "on" : "off")} ({m.cloudsRT:0.##}); ambient {ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight)}.");
                }
                Shader.DisableKeyword(FogKeyword); Shader.DisableKeyword(LightRTKeyword); Shader.DisableKeyword(CloudsKeyword);
                Shader.SetGlobalFloat(idVertFog, 0f); Shader.SetGlobalFloat(idLightRTIntensity, 0f); Shader.SetGlobalFloat(idCloudsRTIntensity, 0f);
                LightEnvironmentPreset p = Noon();
                LightsSystem ls = p != null ? LightsSystem.Instance : null;
                if (ls == null || ls.SunLight == null || ls.BackLight == null) return m; // the light as it is (on stays false), the sky's effects put back
                m.sun = ls.SunLight;
                m.back = ls.BackLight;
                m.sunColor = m.sun.color; m.sunIntensity = m.sun.intensity; m.sunShadow = m.sun.shadowStrength; m.sunRotation = m.sun.transform.rotation;
                m.backColor = m.back.color; m.backIntensity = m.back.intensity;
                m.ambient = RenderSettings.ambientLight;
                m.gspBack = Shader.GetGlobalColor(idBacklightColor); m.gspBurn = Shader.GetGlobalFloat(idLightMaxBurn);
                m.gspSun = Shader.GetGlobalFloat(idSunLight); m.gspTime = Shader.GetGlobalFloat(idTimeOfDay);
                m.on = true;
                m.sun.color = p.sunLightColor; m.sun.intensity = p.sunLightIntensity; m.sun.shadowStrength = p.sunLightShadowStrength;
                m.sun.transform.rotation = Quaternion.Euler(p.sunLightRotation);
                m.back.color = p.backLightColor; m.back.intensity = p.backLightIntensity;
                RenderSettings.ambientLight = p.ambientLightColor;
                Shader.SetGlobalColor(idBacklightColor, p.gspBacklightColor);
                Shader.SetGlobalFloat(idLightMaxBurn, p.gspMaxLightBurn);
                Shader.SetGlobalFloat(idSunLight, p.sunLightAmount);
                Shader.SetGlobalFloat(idTimeOfDay, 0.5f);
                return m;
            }

            public void Dispose()
            {
                if (sky)
                {
                    sky = false;
                    Shader.SetGlobalFloat(idVertFog, vertFog); Shader.SetGlobalFloat(idLightRTIntensity, lightRT); Shader.SetGlobalFloat(idCloudsRTIntensity, cloudsRT);
                    if (fogKw) Shader.EnableKeyword(FogKeyword);
                    if (lightKw) Shader.EnableKeyword(LightRTKeyword);
                    if (cloudsKw) Shader.EnableKeyword(CloudsKeyword);
                }
                if (!on) return;
                on = false;
                if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; sun.shadowStrength = sunShadow; sun.transform.rotation = sunRotation; }
                if (back != null) { back.color = backColor; back.intensity = backIntensity; }
                RenderSettings.ambientLight = ambient;
                Shader.SetGlobalColor(idBacklightColor, gspBack);
                Shader.SetGlobalFloat(idLightMaxBurn, gspBurn);
                Shader.SetGlobalFloat(idSunLight, gspSun);
                Shader.SetGlobalFloat(idTimeOfDay, gspTime);
            }

            // The outdoor presets' blend at midday (time 0.5), as EnvironmentEngine.OnTimeOfDayChanged picks and blends
            // them; made once, when the outdoor presets are there (EnvironmentEngine's availablePresets, loaded with the game).
            private static LightEnvironmentPreset Noon()
            {
                if (noon != null) return noon;
                EnvironmentEngine env = EnvironmentEngine.Instance;
                if (env == null) return null;
                try
                {
                    var all = HarmonyLib.AccessTools.Field(typeof(EnvironmentEngine), "availablePresets")?.GetValue(env) as Dictionary<string, TimeOfDayPresets>;
                    TimeOfDayPresets outdoor = null;
                    if (all != null) all.TryGetValue("outdoor", out outdoor);
                    if (outdoor == null)
                    {
                        var current = HarmonyLib.AccessTools.Field(typeof(EnvironmentEngine), "timesOfDay")?.GetValue(env) as TimeOfDayPresets;
                        if (current != null && !current.indoorPreset) outdoor = current;
                    }
                    if (outdoor == null || outdoor.presets == null || outdoor.presets.Count == 0) return null; // not loaded yet: asked again next time
                    List<TimeOfDayPresets.TimeAndPreset> list = outdoor.presets;
                    LightEnvironmentPreset a = list[0].preset, b = a;
                    float t = 0f;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].time <= 0.5f) { a = list[i].preset; b = a; t = 0f; if (i + 1 < list.Count && list[i + 1].time > 0.5f) { b = list[i + 1].preset; t = (0.5f - list[i].time) / Mathf.Max(0.0001f, list[i + 1].time - list[i].time); } }
                    }
                    if (a == null || b == null) return null;
                    var made = ScriptableObject.CreateInstance<LightEnvironmentPreset>();
                    made.hideFlags = HideFlags.HideAndDontSave;
                    LightEnvironmentPreset.Lerp(made, a, b, t);
                    noon = made;
                    Plugin.Log.LogInfo($"[preview] the pictures are lit as outdoors at midday ({a.name}{(b != a ? " to " + b.name + $" at {t:0.##}" : "")}: sun {ColorUtility.ToHtmlStringRGB(noon.sunLightColor)} x{noon.sunLightIntensity:0.##}, ambient {ColorUtility.ToHtmlStringRGB(noon.ambientLightColor)}).");
                    return noon;
                }
                catch (Exception e)
                {
                    if (!said)
                    {
                        said = true;
                        Plugin.Log.LogWarning("[preview] the midday light for the pictures could not be made (" + e.Message + "); they take the scene's light as it is.");
                    }
                    noon = null;
                    return null;
                }
            }
        }

        private static void RenderLit(View v)
        {
            switchedOff.Clear();
            if (!v.WithCart)
            {
                copy.GetComponentsInChildren(false, allRenderers);
                foreach (Renderer r in allRenderers)
                {
                    if (r != null && r.enabled && !donkeyRenderers.Contains(r))
                    {
                        r.enabled = false;
                        switchedOff.Add(r);
                    }
                }
            }
            try
            {
                if (v.Reframe)
                using (Perf.Measure(Perf.S.PrevFrame))
                {
                    v.Reframe = false;
                    TestReframes++;
                    if (!v.EdgeReframe && (framings.TryGetValue(FramingKey(v), out Framing known) || FromDisk(v, out known)))
                    {
                        framings[FramingKey(v)] = known;
                        Use(v, known); // this look framed before (this session, or on disk): at once
                        v.TightenStep = -1;
                    }
                    else
                    {
                        framings.Remove(FramingKey(v));
                        Frame(v, scratch: true);
                        StartTighten(v);
                    }
                    v.EdgeReframe = false;
                    v.EdgeChecksUntil = Time.unscaledTime + EdgeCheckSeconds; // checked a while after each framing
                }
                if (v.TightenStep >= 0)
                {
                    using (Perf.Measure(Perf.S.PrevFrame)) TightenStep(v);
                    if (v.TightenStep >= 0) return; // still measuring: the shown picture keeps its last photo
                }
                using (Perf.Measure(Perf.S.PrevRender)) v.Cam.Render();
                if (gapStamp != v.EdgeStamp)
                {
                    v.EdgeStamp = gapStamp; // the paper's tuning re-merged (a setting changed): checked again a while
                    v.EdgeChecksUntil = Time.unscaledTime + EdgeCheckSeconds;
                }
                if (Time.unscaledTime >= v.NextEdgeCheck && Time.unscaledTime < v.EdgeChecksUntil)
                {
                    v.NextEdgeCheck = Time.unscaledTime + 1f;
                    if (TouchesEdge(v))
                    {
                        v.Reframe = true; // something reached the edge (the lamp hung, a setting changed): frame again
                        v.EdgeReframe = true;
                    }
                }
            }
            finally
            {
                foreach (Renderer r in switchedOff)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                    }
                }
                switchedOff.Clear();
            }
        }

        // The pose for this frame: walking on the spot, then the lantern (after the animator: this runs in the
        // plugin's LateUpdate) and the colours.
        private static void Pose(Mount.Instance paper)
        {
            if (animator != null)
            {
                // Walking on the spot, facing left: the game's own parameters (AnimationComponentBase: State 1 = Walk,
                // Direction 0 right / -180 left, walk_speed 1 = the clip's own pace).
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.name == "Direction" && p.type == AnimatorControllerParameterType.Float) animator.SetFloat("Direction", -180f);
                    else if (p.name == "State" && p.type == AnimatorControllerParameterType.Int) animator.SetInteger("State", 1);
                    else if (p.name == "walk_speed" && p.type == AnimatorControllerParameterType.Float) animator.SetFloat("walk_speed", 1f);
                }
            }
            using (Mount.Bind(paper))
            {
                paper.LanternRig.Tick();
                paper.ColorsRig.Tick(); // rebuilt only when the look changed (MountCompanion marks it)
                paper.LooksRig.Tick();
                paper.LooksRig.ApplySafe(); // the stage's cameras are rendered by hand: not covered by the per-frame apply
            }
            // The mount's hitch gap (the skin's [Tuning.Cart] HitchGapPx, docs/mounts-parity-matrix.md 2.2): the whole
            // cart sits that much further behind the body in the picture, as Mount.ApplyHitchGap moves it in the world.
            // The animator writes the sway parts' positions every frame, so a non-zero gap is put back after it every
            // frame; the value itself is read from the layers every frame too (read on the rig's re-merge stamp before,
            // a new mount's gap came frames after its picture: the donkey drawn with the dragon's long shafts, 2026-10-22).
            gapStamp = paper.LooksRig.TuneStamp;
            gapWanted = Looks.HitchGapWorldFor(paper);
            float gap = gapWanted;
            if (Mathf.Abs(gap) > 0.0005f || Mathf.Abs(gapApplied) > 0.0005f)
            {
                gapApplied = gap;
                foreach (KeyValuePair<Transform, Vector3> kv in cartRest)
                {
                    Transform t = kv.Key;
                    if (t == null || !t || t.parent == null) continue;
                    t.localPosition = kv.Value + (Mathf.Abs(gap) < 0.0005f ? Vector3.zero : t.parent.InverseTransformVector(cartDir * gap));
                }
            }
        }

        private static readonly Dictionary<Transform, Vector3> cartRest = new Dictionary<Transform, Vector3>();
        private static Vector3 cartDir = Vector3.right;
        private static float gapApplied;
        private static float gapWanted;                  // the layers' gap, read when the rig re-merged them (TuneStamp) or once a second
                                                         // (static like cartRest/cartDir: the stage poses one paper at a time)
        private static int gapStamp = -1;
        internal static float TestGapApplied => gapApplied;   // the self-tests: the hitch gap on the stage's cart parts now
        internal static int TestReframes;                     // the self-tests: how often a picture was framed again

        private static void Fail(string why)
        {
            failed = true;
            Plugin.Log.LogInfo("Settings: the picture with the cart is off for this session (" + why + "); the donkey alone is shown.");
            try
            {
                dressedAs?.ColorsRig.Detach();
                dressedAs?.LanternRig.Detach();
                dressedAs?.LooksRig.Detach();
                if (loading.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(loading);
                }
                if (stage != null)
                {
                    UnityEngine.Object.Destroy(stage);
                }
                foreach (View v in views)
                {
                    if (v.Rt != null)
                    {
                        v.Rt.Release();
                        UnityEngine.Object.Destroy(v.Rt);
                    }
                    if (v.Scratch != null)
                    {
                        v.Scratch.Release();
                        UnityEngine.Object.Destroy(v.Scratch);
                    }
                    if (v.Readback != null) UnityEngine.Object.Destroy(v.Readback);
                    v.Rt = null;
                    v.Scratch = null;
                    v.Readback = null;
                    v.TightenStep = -1;
                    v.Cam = null;
                }
            }
            catch
            {
            }
            stage = copy = null;
            dressedAs = null;
        }

        private static void Build()
        {
            if (!loadStarted)
            {
                loadStarted = true;
                loading = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("Assets/AddressableAssets/WGOs/" + Plugin.MountWgoId.Value + ".prefab");
                return;
            }
            if (!loading.IsValid() || !loading.IsDone)
            {
                return;
            }
            GameObject prefab = loading.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? loading.Result : null;
            if (prefab == null)
            {
                Fail("the donkey's look isn't in the game's files");
                return;
            }

            // Off while it's made, so no script of the copy ever wakes.
            stage = new GameObject("GK2DonkeyAPI preview stage");
            stage.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(stage);
            stage.transform.position = new Vector3(0f, -5000f, 0f);
            copy = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
            copy.name = "GK2DonkeyAPI preview copy";
            Strip(copy);
            animator = copy.GetComponentInChildren<Animator>(true);
            donkeyRenderers.Clear();
            SpriteRenderer body = null;
            int cartParts = 0;
            cartRest.Clear();
            cartDir = Vector3.right;
            gapApplied = 0f;
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                if (Array.IndexOf(CartPartNames, t.name) >= 0)
                {
                    t.gameObject.SetActive(true);
                    // a mesh child named like its part (the far wheel's is donkey_cart_wheel_f too) moves with its parent:
                    // shifting it as well would put it a hitch gap out (Mount.CollectCartParts skips them the same way)
                    bool nested = false;
                    for (Transform up = t.parent; up != null && up != copy.transform; up = up.parent)
                        if (Array.IndexOf(CartPartNames, up.name) >= 0) { nested = true; break; }
                    if (nested) continue;
                    cartParts++;
                    cartRest[t] = t.localPosition; // the prefab's rest: the hitch gap is added on top (Pose)
                    if (t.name == "donkey_cart_empty_sway")
                    {
                        Vector3 w = t.position - copy.transform.position;
                        w.y = 0f;
                        if (w.sqrMagnitude > 0.0001f) cartDir = w.normalized;
                    }
                }
                else if (t.name == "donkey_cart_body_sway")
                {
                    t.gameObject.SetActive(false); // the loaded cart (the corpse shroud)
                }
                else if (Array.IndexOf(Helpers, t.name) >= 0)
                {
                    t.gameObject.SetActive(false); // drawn for the game's effects, not to be seen
                }
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null && t.name == "hed")
                {
                    // The head sprite is for turning the head; the game's HeadSpriteComponentMarker (stripped) keeps it
                    // in step. The walk frames of the body draw the head already: left on, it floated beside it.
                    sr.enabled = false;
                }
                if (sr != null && (t.name == "bdy" || t.name == "hed"))
                {
                    donkeyRenderers.Add(sr);
                    if (t.name == "bdy")
                    {
                        body = sr;
                    }
                }
            }
            if (body == null)
            {
                Fail("no donkey body in the copy");
                return;
            }
            ppu = body.sprite != null ? body.sprite.pixelsPerUnit : 50f;
            stage.SetActive(true); // the copy's Object3DMesh starts: its materials are set by the next frame

            foreach (View v in views)
            {
                var camGo = new GameObject("GK2DonkeyAPI preview camera (" + v.Name + ")");
                camGo.transform.SetParent(stage.transform, false);
                v.Cam = camGo.AddComponent<Camera>();
                v.Cam.enabled = false;
                v.Cam.orthographic = true;
                v.Cam.cullingMask = ~(1 << 5); // everything but UI: there's nothing but the copy down here
                v.Cam.clearFlags = CameraClearFlags.SolidColor;
                v.Cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                v.Reframe = true;
            }

            var lightGo = new GameObject("GK2DonkeyAPI preview light");
            lightGo.transform.SetParent(stage.transform, false);
            lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = 1f;
            lamp.color = Color.white;

            framesSinceBuilt = 0;
            described = false;
            framings.Clear(); // a new stage: measured again
            Frame(cartView);
            Plugin.Log.LogInfo($"Settings: the pictures are set up ({cartParts} cart parts on, {donkeyRenderers.Count} donkey sprites, {ppu} pixels per unit).");
            Describe("built");
        }

        // The camera at the game's angle, framed wide on every shown part's bounds (then Tighten crops to what's drawn).
        // The light is placed by the cart picture's framing only, so both pictures are lit the same.
        private static void Frame(View v, bool scratch = false)
        {
            Transform ct = v.Cam.transform;
            ct.rotation = Quaternion.Euler(GameCameraPitch, 0f, 0f);
            Vector3 forward = ct.forward;
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled)
                {
                    continue;
                }
                foreach (Vector3 corner in Corners(r.bounds))
                {
                    Vector3 c = ct.InverseTransformDirection(corner - stage.transform.position);
                    if (!any)
                    {
                        b = new Bounds(c, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(c);
                    }
                }
            }
            if (!any || b.size.x <= 0f || b.size.y <= 0f)
            {
                throw new InvalidOperationException("nothing to frame");
            }
            float density = ppu * Detail;
            int w = Mathf.Clamp(Mathf.CeilToInt(b.size.x * density) + 8 * Detail, 16, 768 * Detail);
            int h = Mathf.Clamp(Mathf.CeilToInt(b.size.y * density) + 8 * Detail, 16, 768 * Detail);
            v.Cam.orthographicSize = h / density / 2f;
            v.Cam.aspect = (float)w / h;
            Vector3 centre = stage.transform.position + ct.TransformDirection(new Vector3(b.center.x, b.center.y, 0f));
            ct.position = centre - forward * (b.extents.z + 50f);
            v.Cam.nearClipPlane = 0.01f;
            v.Cam.farClipPlane = b.size.z + 100f;
            if (scratch) ScratchTexture(v, w, h);
            else NewTexture(v, w, h);
            if (v.WithCart)
            {
                lamp.transform.position = centre - forward * 4f;
                lamp.range = Mathf.Max(b.size.x, b.size.y) * 3f + 8f; // the stage only: the world is 5000 units away
            }
        }

        // The picture's texture at this size: the same object resized (released, resized, created again), so the
        // displays showing it keep showing it; a new one destroyed under them went blank until their next refresh.
        private static void NewTexture(View v, int w, int h)
        {
            if (v.Rt != null && v.Rt.width == w && v.Rt.height == h)
            {
                return;
            }
            v.Cam.targetTexture = null;
            if (v.Rt != null)
            {
                v.Rt.Release();
                v.Rt.width = w;
                v.Rt.height = h;
            }
            else
            {
                v.Rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "GK2DonkeyAPI preview (" + v.Name + ")" };
            }
            v.Rt.Create();
            v.Cam.targetTexture = v.Rt;
        }

        // The wide picture the framing is measured on (Frame with scratch): the shown Rt keeps the last photo meanwhile.
        private static void ScratchTexture(View v, int w, int h)
        {
            if (v.Scratch == null || v.Scratch.width != w || v.Scratch.height != h)
            {
                v.Cam.targetTexture = null;
                if (v.Scratch != null)
                {
                    v.Scratch.Release();
                    UnityEngine.Object.Destroy(v.Scratch);
                }
                v.Scratch = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "GK2DonkeyAPI preview measure (" + v.Name + ")" };
                v.Scratch.Create();
            }
            v.Cam.targetTexture = v.Scratch;
        }

        // A framing measured before, put back as it was.
        private static void Use(View v, Framing f)
        {
            Transform ct = v.Cam.transform;
            ct.rotation = Quaternion.Euler(GameCameraPitch, 0f, 0f);
            ct.position = stage.transform.position + f.CamFromStage;
            v.Cam.orthographicSize = f.Ortho;
            v.Cam.aspect = f.Aspect;
            v.Cam.nearClipPlane = f.Near;
            v.Cam.farClipPlane = f.Far;
            NewTexture(v, f.W, f.H);
            v.Cam.targetTexture = v.Rt;
            if (v.WithCart)
            {
                lamp.transform.position = stage.transform.position + f.LampFromStage;
                lamp.range = f.LampRange;
            }
        }

        // A picture's pixels, read into this view's one buffer (valid until the next read-back of the view).
        private static NativeArray<Color32> ReadBack(View v, RenderTexture rt = null)
        {
            rt = rt ?? v.Rt;
            RenderTexture prev = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                if (v.Readback == null || v.Readback.width != rt.width || v.Readback.height != rt.height)
                {
                    if (v.Readback != null) UnityEngine.Object.Destroy(v.Readback);
                    v.Readback = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, mipChain: false) { name = "GK2DonkeyAPI preview read-back (" + v.Name + ")" };
                }
                v.Readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                return v.Readback.GetRawTextureData<Color32>();
            }
            finally
            {
                RenderTexture.active = prev;
            }
        }

        // The self-tests' view of a picture: the hue its coloured pixels lean to (degrees; the mean of the hue vectors
        // of pixels with alpha and some saturation) and how many such pixels there are. -1 with no picture.
        internal static float TestDominantHue(bool withCart, out int coloured, out string topColours)
        {
            coloured = 0;
            topColours = "";
            View v = withCart ? cartView : donkeyView;
            if (failed || v.Rt == null) return -1f;
            NativeArray<Color32> px = ReadBack(v);
            double x = 0, y = 0;
            var counts = new Dictionary<int, int>();
            foreach (Color32 c in px)
            {
                if (c.a <= 8) continue;
                int key = (c.r << 16) | (c.g << 8) | c.b;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                Color.RGBToHSV(c, out float h, out float s, out _);
                if (s < 0.25f) continue;
                coloured++;
                x += Math.Cos(h * 2 * Math.PI);
                y += Math.Sin(h * 2 * Math.PI);
            }
            // the five most drawn colours, as read back: the channel order is checked here (a readback with red and
            // blue swapped would turn the worm's pink into violet)
            var top = new List<KeyValuePair<int, int>>(counts);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Math.Min(5, top.Count); i++) sb.Append(i > 0 ? " " : "").Append(top[i].Key.ToString("X6")).Append('x').Append(top[i].Value);
            topColours = sb.ToString();
            if (coloured == 0) return -1f;
            float deg = (float)(Math.Atan2(y, x) * 180 / Math.PI);
            return deg < 0 ? deg + 360f : deg;
        }

        // Is anything drawn on the picture's outermost pixels? Then it was cut off.
        private static bool TouchesEdge(View v)
        {
            NativeArray<Color32> px = ReadBack(v);
            int w = v.Rt.width, h = v.Rt.height;
            for (int x = 0; x < w; x++)
            {
                if (px[x].a > 8 || px[(h - 1) * w + x].a > 8) return true;
            }
            for (int y = 0; y < h; y++)
            {
                if (px[y * w].a > 8 || px[y * w + w - 1].a > 8) return true;
            }
            return false;
        }

        // Onto what is drawn over a whole walk cycle: the animator is stepped by hand through CycleSeconds of its
        // walk, the picture taken at each step, and the camera moved and narrowed onto the union of the non-empty
        // pixels, a margin round. One frame's pixels were not enough: a mount that hops (Gerry) or chatters reached
        // the edge a moment later, the edge check reframed it, and the picture changed size two or three times in the
        // first second after a mount change (the video of 2026-10-18 21-07).
        private const float CycleSeconds = 2f, CycleStep = 0.1f;
        private const float EdgeCheckSeconds = 5f;

        private static void StartTighten(View v)
        {
            v.TightenStep = 0;
            v.MinX = v.Scratch.width;
            v.MinY = v.Scratch.height;
            v.MaxX = -1;
            v.MaxY = -1;
        }

        // One step of the measure a frame (the animator stepped by hand, a photo into Scratch, its drawn pixels added);
        // after the last, the camera narrowed onto them, the framing remembered, and the first photo taken into Rt.
        private static void TightenStep(View v)
        {
            int w0 = v.Scratch.width, h0 = v.Scratch.height;
            int steps = Mathf.Max(1, Mathf.RoundToInt(CycleSeconds / CycleStep));
            if (v.TightenStep > 0)
            {
                if (animator != null) animator.Update(CycleStep);
                if (dressedAs != null)
                {
                    using (Mount.Bind(dressedAs))
                    {
                        dressedAs.LooksRig.Tick();
                        dressedAs.LooksRig.ApplySafe();
                    }
                }
            }
            v.Cam.targetTexture = v.Scratch;
            v.Cam.Render();
            NativeArray<Color32> px = ReadBack(v, v.Scratch);
            for (int y = 0; y < h0; y++)
            {
                int row = y * w0;
                for (int x = 0; x < w0; x++)
                {
                    if (px[row + x].a > 8)
                    {
                        if (x < v.MinX) v.MinX = x;
                        if (x > v.MaxX) v.MaxX = x;
                        if (y < v.MinY) v.MinY = y;
                        if (y > v.MaxY) v.MaxY = y;
                    }
                }
            }
            if (++v.TightenStep < steps) return;
            v.TightenStep = -1;
            if (v.MaxX < 0)
            {
                Plugin.Log.LogInfo($"[preview] the picture {v.Name} is empty; framing left wide.");
                NewTexture(v, w0, h0);
                v.Cam.targetTexture = v.Rt;
                v.Cam.Render();
                return;
            }
            const int margin = 6 * Detail; // room for the walk's bob and the lamp's swing
            int minX = Math.Max(0, v.MinX - margin), minY = Math.Max(0, v.MinY - margin);
            int maxX = Math.Min(w0 - 1, v.MaxX + margin), maxY = Math.Min(h0 - 1, v.MaxY + margin);
            int w = maxX - minX + 1, h = maxY - minY + 1;
            float unitsPerPixel = v.Cam.orthographicSize * 2f / h0;
            float dx = ((minX + maxX + 1) / 2f - w0 / 2f) * unitsPerPixel;
            float dy = ((minY + maxY + 1) / 2f - h0 / 2f) * unitsPerPixel;
            Transform ct = v.Cam.transform;
            ct.position += ct.right * dx + ct.up * dy;
            NewTexture(v, w, h);
            v.Cam.targetTexture = v.Rt;
            v.Cam.orthographicSize = h * unitsPerPixel / 2f;
            v.Cam.aspect = (float)w / h;
            var measured = new Framing
            {
                CamFromStage = ct.position - stage.transform.position, Ortho = v.Cam.orthographicSize, Aspect = v.Cam.aspect,
                Near = v.Cam.nearClipPlane, Far = v.Cam.farClipPlane, W = w, H = h,
                LampFromStage = lamp.transform.position - stage.transform.position, LampRange = lamp.range,
            };
            framings[FramingKey(v)] = measured;
            ToDisk(v, measured);
            v.Cam.Render();
            Plugin.Log.LogInfo($"[preview] {v.Name}: framed on what's drawn over {CycleSeconds:0.#} s of the walk ({steps} photos, one a frame), {w} x {h} pixels (from {w0} x {h0}); remembered for this look.");
        }

        // ------------------------------------------------------------------ the log

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null && p != stage.transform; p = p.parent)
            {
                parts.Insert(0, p.name);
            }
            return string.Join("/", parts);
        }

        // Everything the picture is made of (the log's [preview] lines): what draws, where, with what.
        private static void Describe(string when)
        {
            if (!DevTools.Unlocked) return; // its renderers listed, some 45 lines: for the developer, not a player's log
            try
            {
                Camera cam = cartView.Cam;
                RenderTexture rt = cartView.Rt;
                Transform ct = cam.transform;
                Plugin.Log.LogInfo($"[preview] {when}: camera at {ct.position}, angle {ct.eulerAngles}, ortho size {cam.orthographicSize:0.###}, aspect {cam.aspect:0.###}, image {rt.width}x{rt.height}; animator {(animator != null ? animator.runtimeAnimatorController?.name ?? "no controller" : "none")}.");
                int n = 0, on = 0;
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    n++;
                    bool shown = r.enabled && r.gameObject.activeInHierarchy;
                    if (shown) on++;
                    Bounds wb = r.bounds;
                    Vector3 c = ct.InverseTransformPoint(wb.center);
                    string what = r.GetType().Name;
                    if (r is SpriteRenderer sr)
                    {
                        what += $" sprite={(sr.sprite != null ? sr.sprite.name : "none")} flipX={sr.flipX}";
                    }
                    else
                    {
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        Mesh m = mf != null ? mf.sharedMesh : null;
                        what += $" mesh={(m != null ? m.name + " (" + m.vertexCount + " verts)" : "none")}";
                    }
                    Plugin.Log.LogInfo($"[preview]   {(shown ? "on " : "off")} {PathOf(r.transform)}: {what}; local {r.transform.localPosition}; world centre {wb.center} size {wb.size}; to the camera {c.x:0.##},{c.y:0.##} depth {c.z:0.##}.");
                }
                Plugin.Log.LogInfo($"[preview] {on} of {n} renderers on.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("[preview] describing failed: " + e.Message);
            }
        }

        // Every component but what draws (and the animator), children first and scripts before what they need.
        private static void Strip(GameObject root)
        {
            var removed = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SpriteRenderer || c is Object3DMesh || c is Animator)
                    {
                        continue;
                    }
                    if (pass == 0 && !(c is MonoBehaviour))
                    {
                        continue; // scripts first: they may require the others
                    }
                    string type = c.GetType().Name;
                    UnityEngine.Object.DestroyImmediate(c);
                    removed[type] = removed.TryGetValue(type, out int k) ? k + 1 : 1;
                }
            }
            // The animator stays: it gives the parts the game's own idle pose. Its events are off, so no clip can call
            // into the game, and it doesn't move the copy.
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
            {
                a.fireEvents = false;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var list = new List<string>();
            foreach (var kv in removed)
            {
                list.Add(kv.Key + " " + kv.Value);
            }
            Plugin.Log.LogInfo("[preview] removed from the copy: " + (list.Count == 0 ? "nothing" : string.Join(", ", list)) + ".");
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            yield return new Vector3(mn.x, mn.y, mn.z);
            yield return new Vector3(mx.x, mn.y, mn.z);
            yield return new Vector3(mn.x, mx.y, mn.z);
            yield return new Vector3(mx.x, mx.y, mn.z);
            yield return new Vector3(mn.x, mn.y, mx.z);
            yield return new Vector3(mx.x, mn.y, mx.z);
            yield return new Vector3(mn.x, mx.y, mx.z);
            yield return new Vector3(mx.x, mx.y, mx.z);
        }
    }
}
