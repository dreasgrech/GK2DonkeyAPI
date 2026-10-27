using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The stride probe (the user, 2026-10-24: the footsteps, the movement and the legs "do not match ... they are not
    // synchronized"; "whatever data we need lets get it and get as much as possible"; docs/flying.md "The walk,
    // studied"). Every mount whose walk clip runs (ridden, standing, babies, with a cart) writes one row per completed
    // walk cycle (the animator's normalized time passing a whole number) into BepInEx/GK2-strides/strides-<time>.csv:
    // the clip, its length and the animator's speeds, the multiplier the mod set, the mount's speed keys, the cycle's
    // time, how far the mount moved (world units and the picture's own art px), the body frames shown (each frame's
    // number and for how many rendered frames), and every footstep sound with the frame and clip time it played at.
    // The log gets the file's name and the first rows of each mount. Dev only; no behaviour changes.
    internal static class StrideProbe
    {
        // Off until a survey turns it on (the walk survey; the garbage survey's probe phase), even with the dev tools: on
        // all the time it appended a row to its file every walk cycle of every walking mount, each a file opened on the
        // main thread, 1033 times in 17 s riding a mount with its babies, 5 to 11 ms a time (Performance Hog,
        // 2026-10-25 13:38), and every dev measurement carried it.
        internal static bool On;

        private sealed class Track
        {
            internal SpriteRenderer Body;
            internal long Cycle = long.MinValue;
            internal float StartTime;
            internal Vector3 StartPos;
            internal readonly StringBuilder Frames = new StringBuilder(64);
            internal readonly StringBuilder Steps = new StringBuilder(64);
            internal int StepCount;
            internal int LastFrame = -2, FrameRun;
            internal int RenderFrames;
            internal int Logged;
        }

        private static readonly Dictionary<Mount.Instance, Track> tracks = new Dictionary<Mount.Instance, Track>();
        private static string file;
        private static int rows;
        private const int MaxRows = 5000;

        internal static string Label = ""; // what the walk survey is doing (Dev/SelfTest.Walk.cs), a column of its own
        internal static string FilePath => file;
        internal static int Rows => rows;

        internal static string Folder => Path.Combine(Paths.BepInExRootPath, "GK2-strides");

        // Each rig tick with the walk clip running (Sounds.Rig.TickInner, bound to the instance).
        internal static void Tick(Mount.Instance i, Animator a, string clip, float t, bool inAir)
        {
            if (!On || rows >= MaxRows || i == null) return;
            try
            {
                if (!tracks.TryGetValue(i, out Track k))
                {
                    k = new Track();
                    tracks[i] = k;
                }
                if (k.Body == null || !k.Body)
                {
                    k.Body = FindBody(i);
                    if (k.Body == null) return;
                }
                long cycle = (long)Math.Floor(t);
                int frame = FrameOf(k.Body);
                if (cycle != k.Cycle)
                {
                    if (k.Cycle != long.MinValue && Math.Abs(cycle - k.Cycle) == 1 && Time.time > k.StartTime)
                    {
                        Row(i, k, a, clip, inAir);
                    }
                    k.Cycle = cycle;
                    k.StartTime = Time.time;
                    k.StartPos = k.Body.transform.position;
                    k.Frames.Length = 0;
                    k.Steps.Length = 0;
                    k.StepCount = 0;
                    k.LastFrame = -2;
                    k.FrameRun = 0;
                    k.RenderFrames = 0;
                }
                k.RenderFrames++;
                if (frame != k.LastFrame)
                {
                    FlushRun(k);
                    k.LastFrame = frame;
                    k.FrameRun = 1;
                }
                else
                {
                    k.FrameRun++;
                }
            }
            catch (Exception e)
            {
                On = false;
                Plugin.Log.LogWarning("[stride] probe off after an error: " + e.Message);
            }
        }

        // A footstep sound played by the rig (Sounds.Rig.TickInner): the frame and the clip time it came at.
        internal static void Step(Mount.Instance i, float t, long index)
        {
            if (!On || i == null || !tracks.TryGetValue(i, out Track k) || k.Body == null || !k.Body) return;
            k.StepCount++;
            if (k.Steps.Length > 0) k.Steps.Append(' ');
            k.Steps.Append("f").Append(FrameOf(k.Body)).Append("@").Append((t - Math.Floor(t)).ToString("0.000", CultureInfo.InvariantCulture))
                .Append("#").Append(index);
        }

        internal static void Forget(Mount.Instance i) => tracks.Remove(i);

        private static void FlushRun(Track k)
        {
            if (k.LastFrame == -2) return;
            if (k.Frames.Length > 0) k.Frames.Append(' ');
            k.Frames.Append(k.LastFrame).Append('x').Append(k.FrameRun);
        }

        private static void Row(Mount.Instance i, Track k, Animator a, string clip, bool inAir)
        {
            FlushRun(k);
            float dt = Time.time - k.StartTime;
            Vector3 pos = k.Body.transform.position;
            Vector3 d = pos - k.StartPos;
            Vector3 local = k.Body.transform.InverseTransformVector(d);
            Sprite sp = k.Body.sprite;
            float ppu = sp != null ? sp.pixelsPerUnit : 0f;
            AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
            float clipLen = 0f;
            var infos = a.GetCurrentAnimatorClipInfo(0);
            if (infos.Length > 0 && infos[0].clip != null) clipLen = infos[0].clip.length;
            bool ridden = ReferenceEquals(Mount.Ridden, i);
            string role = ridden ? "ridden" : i.ViewScale < 1f ? "baby" : "standing";
            float setMult = ridden ? i.WalkMultiplierNow : i.StandWalkSpeedApplied;
            var c = CultureInfo.InvariantCulture;
            string line = string.Join(";", new[]
            {
                Time.time.ToString("0.000", c), Label, i.FullId, i.LooksRig?.DefNow?.Id ?? "donkey", role, i.ViewScale.ToString("0.##", c),
                i.HasCart ? "cart" : "-", inAir ? "air" : "ground", clip, clipLen.ToString("0.000", c), st.speed.ToString("0.###", c),
                st.speedMultiplier.ToString("0.###", c), a.speed.ToString("0.###", c), setMult.ToString("0.###", c),
                i.S.WalkCycleSpeed.Value.ToString("0.###", c), i.S.SpeedMultiplier.Value.ToString("0.###", c),
                dt.ToString("0.000", c), d.magnitude.ToString("0.000", c), (d.magnitude / Mathf.Max(0.001f, dt)).ToString("0.00", c),
                (Mathf.Abs(local.x) * ppu).ToString("0.0", c), (local.y * ppu).ToString("0.0", c),
                (Mathf.Abs(local.x) * ppu / Mathf.Max(0.001f, dt)).ToString("0.0", c), ppu.ToString("0.#", c),
                k.Body.transform.lossyScale.x.ToString("0.###", c), k.RenderFrames.ToString(c),
                (k.RenderFrames / Mathf.Max(0.001f, dt)).ToString("0", c), Time.timeScale.ToString("0.##", c),
                k.StepCount.ToString(c), k.Frames.ToString(), k.Steps.ToString(), sp != null ? sp.name : "",
            });
            Write(line);
            if (k.Logged++ < 3)
            {
                Plugin.Log.LogInfo($"[stride] {i.FullId} ({i.LooksRig?.DefNow?.Id ?? "donkey"}, {role}{(i.HasCart ? ", cart" : "")}): a cycle in {dt:0.000} s, moved {Mathf.Abs(local.x) * ppu:0.0} art px ({d.magnitude:0.000} units), clip '{clip}' {clipLen:0.000} s at state speed {st.speed:0.###} x {st.speedMultiplier:0.###}, multiplier set {setMult:0.###}; steps {k.StepCount} [{k.Steps}]; frames [{k.Frames}]");
            }
        }

        private static void Write(string line)
        {
            if (file == null)
            {
                Directory.CreateDirectory(Folder);
                file = Path.Combine(Folder, "strides-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                File.AppendAllText(file, "time;label;mount;skin;role;scale;cart;where;clip;clip_len_s;state_speed;state_speed_mult;animator_speed;mult_set;WalkCycleSpeed;SpeedMultiplier;cycle_s;world_units;units_per_s;art_px_across;art_px_up;art_px_per_s;ppu;scale_x;render_frames;fps;time_scale;steps;frames(frame x renders);steps(frame@clip_time#index);sprite\n");
                Plugin.Log.LogInfo("[stride] writing " + file);
            }
            File.AppendAllText(file, line + "\n");
            rows++;
        }

        private static SpriteRenderer FindBody(Mount.Instance i)
        {
            var d = i.Donkey;
            if (d == null || !d) return null;
            foreach (SpriteRenderer sr in d.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
            {
                if (sr.name == "bdy") return sr;
            }
            return null;
        }

        // the number at the end of the body frame's name (0301_bdy_walk_left_03 -> 3), -1 for a frame without one
        private static int FrameOf(SpriteRenderer body)
        {
            string name = body.sprite != null ? body.sprite.name : null;
            if (name == null) return -1;
            int k = name.Length;
            while (k > 0 && char.IsDigit(name[k - 1])) k--;
            return k < name.Length && int.TryParse(name.Substring(k), out int n) ? n : -1;
        }
    }
}
