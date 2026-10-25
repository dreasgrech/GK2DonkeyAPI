using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // What the mod costs per frame, by part (docs/performance.md). Stopwatch timestamps around each part,
    // summed per frame, averaged over one second: the average and the worst frame of that second, and how often each
    // part ran per frame. Parts nest (Shuffle is inside Stand, Line inside Shuffle and Follow), so each number includes
    // the parts inside it. The frame time itself is Unity's (Time.unscaledDeltaTime), so the game's own share is the
    // frame minus ours. A timestamp costs about 25 ns: a few thousand a frame are well under 0.1 ms.
    internal static class Perf
    {
        internal enum S
        {
            Tick,       // Mount.Tick, everything per frame outside the panel
            Ride,       // the ridden instance's frame
            Stand,      // every standing instance's frame (StandTick), views spawned and prepared included
            Spawn,      // the spawn queue and preparing views: everything below
            SpawnView,  //   making a view (TrySpawn: the game's Wgo.Spawn)
            Prepare,    //   preparing a new view's parts (PrepareParts: cart parts, colliders, lantern, cargo holder)
            ColorBuild, //   building and uploading a view's colours (Colors rebuild)
            Release,    //   releasing a far view (DestroyDonkey, Tiers.cs)
            Shuffle,    // the idle shuffle (ShuffleTick), foals' following included
            Follow,     // foals' following (FollowParentTick)
            Route,      // route searches (FoalRouteSearch steps)
            Line,       // straight-line walkability tests (LineWalkable: ground and collider samples)
            Ground,     // ground height tests (GroundHeight: navmesh, else raycast)
            Free,       // collider tests (FreeAt: OverlapCapsule)
            Pose,       // posing a standing view (Stand)
            Lantern,    // lantern rigs of standing carts
            Colors,     // colour rigs
            Looks,      // skin rigs: the frame's pictures onto the overlays, before the cameras (Looks.cs)
            LooksBuild, //   decoding a skin's pictures when the first donkey wears it
            LooksTick,  //   the skin rigs' tick: which skin, the tuning merge (MountInstance.Tick)
            Effects,    // particle effects on donkeys: the ring that follows, the trail (Effects.cs)
            Abilities,  // the mounts' abilities (MountAbility hooks, Looks.Rig.TickAbilities)
            Controls,   // the keeper's ride / hitch prompt (InstanceControls)
            Panel,      // the dev panel (OnGUI: every event of the frame), without its window's contents
            Window,     // the panel window's contents (IMGUI runs them after OnGUI returns, so they are not in Panel)
            Markers,    // the panel's world markers
            WinTop,     //   inside Window: the tab bar, the scope line and the settings-differ snapshot
            WinTab,     //   inside Window: the open tab's rows (PanelTab names it)
            WinFooter,  //   inside Window: the footer (differ text, defaults buttons)
            Landing,    // a flight's landing judged (FlightGround.WhyCannotLandAt: the ground, the body, the walk-away test)
            LandGrid,   //   inside Landing: the game's walk grid rescanned round the keeper (WhyNotOnGameGrid; a touchdown hitch, 2026-10-23)
            FlyFree,    // the free flight's frame (Flight.FreeTick: the floor, the ground under it, the safe-spot check, a landing)
            DrawnIndex, //   the drawn ground's index: a search for the terrains, only when none woke under the patch (DrawnGround.Index)
            SafeSpots,  // never lost: the safe-spot record and the fall watchdog (SafeSpots.Tick)
            Sounds,     // the mount sounds' rigs (Sounds.Rig.Tick: the ridden one's frame and every standing Active instance's)
            Pick,       // a mount picked in the Mounts panel (MountsPanel.Select, a button's click, outside Tick): the choice applied
            PickRows,   //   inside Pick: the panel's right side built again for it (BuildRight, MarkList)
            PickApply,  //   inside Pick: the Mount row set, its handlers run (the companion re-dressed, the rows' conditions)
            PickSave,   //   inside Pick: the mod's cfg file written (BepInEx writes it whole on every change)
            PanelTick,  // the Mounts panel's frame (MountsPanel.Tick: input, the picture fitted)
            Preview,    // the panel's pictures (PreviewStage: the walk drawn again each frame, a framing step, a photo)
            PrevPose,   //   inside Preview: the copy posed (the rigs' tick: a new look's sprites put on, its colours)
            PrevFrame,  //   inside Preview: a framing (the remembered one used, or a measuring step: a photo read back)
            PrevRender, //   inside Preview: the photo itself (the stage camera's Render)
            SndLookup,  // a sound played: the game's sound found (SoundBank.Ensure, AudioConfig.Get)
            SndPlay,    // a sound played: the game's play call (LazyAudio.PlayAtGameObject, with our clip pick round it)
            SndStart,   // a pack's sound file started loading (its bytes read, the cache file, the request)
            SndDecode,  // a pack's sound file made a clip (DownloadHandlerAudioClip.GetContent)
            SndResolve, //   inside Sounds: an event's layers merged (Rig.ResolveNow; Resolve keeps it half a second)
            SndEmit,    //   inside Sounds: a sound emitted, whole (its resolve, the ground under it, the play)
            SndSurface, //   inside SndEmit: the ground's surface under the mount (a raycast) and its step sound
            DevProbe,   //   inside Sounds: the dev-only stride probe (StrideProbe.Tick/Step: a log line and a CSV append a cycle)
            Keys,       // the mods' keys polled and the Settings window's key capture (Plugin.Update, before Tick)
            Count,
        }

        private static readonly string[] Names = Enum.GetNames(typeof(S));
        private static readonly long[] frameTicks = new long[(int)S.Count];
        private static readonly int[] frameCalls = new int[(int)S.Count];
        private static readonly double[] sumMs = new double[(int)S.Count];
        private static readonly double[] maxMs = new double[(int)S.Count];
        private static readonly long[] sumCalls = new long[(int)S.Count];
        private static int frames;
        private static double frameSumMs, frameMaxMs;
        private static float windowStart = -1f;
        private static int lastFrame = -1;
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        // The last full second.
        internal static readonly double[] AvgMs = new double[(int)S.Count];
        internal static readonly double[] WorstMs = new double[(int)S.Count];
        internal static readonly double[] CallsPerFrame = new double[(int)S.Count];
        internal static double Fps, FrameAvgMs, FrameWorstMs;
        internal static int Instances, Views, Foals;
        internal static int LogLinesPerSecond;
        internal static string SummaryText = "Measuring (one second)...";
        internal static string PanelTab; // the panel's open tab, named after WinTab in the summary

        // The panel's switches (session only): a summary line in the log every 10 s, and the two experiments
        // (Mount.Instance.ApplyPerfExperiment).
        internal static bool LogEvery10s, ExpNoShuffle, ExpHideViews;
        private static float nextLog;
        private static int logLines;

        internal readonly struct Scope : IDisposable
        {
            private readonly int s;
            private readonly long t0;
            private readonly long m0;

            internal Scope(S s)
            {
                this.s = (int)s;
                m0 = AllocBySection ? AllocatedNow() : 0;
                t0 = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                frameTicks[s] += Stopwatch.GetTimestamp() - t0;
                frameCalls[s]++;
                if (AllocBySection)
                {
                    long d = AllocatedNow() - m0;
                    if (d > 0)
                    {
                        frameBytes[s] += d; // inclusive, like the times; a collection in between reads low
                    }
                }
            }
        }

        // Managed allocations per part too (two heap reads per part and call: a few microseconds a frame). On with the
        // [perf] lines; shown as "+N B" after each part's time.
        internal static bool AllocBySection; // with the dev panel on (Plugin), like the [perf] lines
        private static readonly long[] frameBytes = new long[(int)S.Count];
        private static readonly long[] sumBytes = new long[(int)S.Count];
        internal static readonly double[] BytesPerFrame = new double[(int)S.Count];

        internal static Scope Measure(S s) => new Scope(s);

        // Managed memory the mod allocates (GC.GetTotalMemory around Tick and the panel window; a collection in between
        // only makes a frame read low) and the collections of the whole game per second: garbage the mod leaves is
        // paid for in the game's GC pauses, which show as single slow frames.
        private static long allocFrame, allocSum, allocMax;
        private static int gcAtWindow = -1;
        internal static double AllocKbPerFrame, AllocKbWorst;
        internal static int GcPerSecond;

        // The bytes this thread has allocated so far, exactly (GC.GetAllocatedBytesForCurrentThread), when the runtime
        // has it and it counts (tested once: 1,000 bytes must show). Otherwise the managed heap's size, which in this
        // Unity moves in 4 KB pages: a part then shows 4 KB when a page happened to start during it (found 2026-10-01).
        private static Func<long> allocatedFn;
        internal static bool AllocExact;

        internal static long AllocatedBytes() => AllocatedNow();

        private static long AllocatedNow()
        {
            if (allocatedFn == null)
            {
                allocatedFn = PickAllocated();
            }
            return allocatedFn();
        }

        private static Func<long> PickAllocated()
        {
            try
            {
                var mi = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null)
                {
                    var f = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), mi);
                    long a = f();
                    var probe = new byte[1000];
                    long b = f();
                    if (b - a >= 1000 && probe.Length == 1000)
                    {
                        AllocExact = true;
                        Plugin.Log.LogInfo("[perf] allocations are counted exactly (GC.GetAllocatedBytesForCurrentThread).");
                        return f;
                    }
                }
            }
            catch
            {
            }
            AllocExact = false;
            Plugin.Log.LogInfo("[perf] allocations are counted from the heap size, in 4 KB pages: per part only roughly.");
            return () => GC.GetTotalMemory(false);
        }

        internal readonly struct AllocScope : IDisposable
        {
            private readonly long m0;

            internal AllocScope(bool _)
            {
                m0 = AllocatedNow();
            }

            public void Dispose()
            {
                long d = AllocatedNow() - m0;
                if (d > 0)
                {
                    allocFrame += d;
                }
            }
        }

        internal static AllocScope MeasureAlloc() => new AllocScope(true);

        // The managed heap of the whole game (MB) at each window, for leaks: it should stay level while the number of
        // instances does (GC.GetTotalMemory, no collection forced).
        internal static double HeapMb;

        // A frame over SlowFrameMs: what each part of the mod took in it, at most once a second (a hitch is ours or not).
        private const double SlowFrameMs = 20.0;
        private static float nextSlowLog;

        // A pick's trace (the user, 2026-10-25: picking the dragon in the Mounts panel freezes the game for some frames; the
        // summary and the once-a-second slow-frame line could not show which frames, nor the panel's own work): from a
        // pick, TraceFrames frames, each over TraceSlowMs or with the mod's parts over 2 ms named with them, and notes (a
        // look ready, a picture framed) on the frame they happened in; one line when done.
        private const int TraceFrames = 120;
        private const double TraceSlowMs = 12.0;
        private static string traceWhat;
        private static int traceLeft, traceSeen, traceSlow;
        private static double traceWorst, traceSum;
        private static readonly StringBuilder trace = new StringBuilder();
        private static string traceNotes;

        internal static void TracePick(string what)
        {
            if (!LogEvery10s) return; // dev only: the perf logging's switch (the dev panel), never a player's
            // The click runs in the EventSystem's Update, maybe before this frame's BeginFrame (Plugin.Update): the previous
            // frame is closed first, so the click's work is counted in the frame it happened in, #1 (it was counted in the
            // frame before, and the frame it slowed showed the rest only; the 2026-10-25 01:31 log).
            BeginFrame();
            if (traceWhat != null) EndTrace("another pick");
            traceWhat = what;
            traceLeft = TraceFrames;
            traceSeen = traceSlow = 0;
            traceWorst = traceSum = 0;
            trace.Length = 0;
        }

        // Something that happened in this frame, shown with it in a running pick trace.
        internal static void TraceNote(string note)
        {
            if (traceWhat == null) return;
            traceNotes = traceNotes == null ? note : traceNotes + "; " + note;
        }

        private static void TraceFrame(double frameMs)
        {
            if (traceWhat == null) return;
            traceSeen++;
            traceSum += frameMs;
            if (frameMs > traceWorst) traceWorst = frameMs;
            double mod = 0;
            var parts = new StringBuilder();
            for (int i = 0; i < (int)S.Count; i++)
            {
                double ms = frameTicks[i] * MsPerTick;
                if (i != (int)S.Tick) mod = Math.Max(mod, ms);
                if (ms >= 0.5) parts.Append($"{(parts.Length > 0 ? ", " : "")}{Names[i]} {ms:F1}");
            }
            if (frameMs > TraceSlowMs) traceSlow++;
            if (frameMs > TraceSlowMs || mod >= 2.0 || traceNotes != null)
            {
                trace.Append($" #{traceSeen} {frameMs:F0} ms ({(parts.Length > 0 ? parts.ToString() : "the mod under 0.5")}{(traceNotes != null ? "; " + traceNotes : "")});");
            }
            traceNotes = null;
            if (--traceLeft <= 0) EndTrace(null);
        }

        private static void EndTrace(string cut)
        {
            Plugin.Log.LogInfo($"[pick] {traceWhat}: {traceSeen} frames from the click, {traceSlow} over {TraceSlowMs:F0} ms, the worst {traceWorst:F0} ms, on average {(traceSeen > 0 ? traceSum / traceSeen : 0):F1} ms{(cut != null ? " (cut by " + cut + ")" : "")};{(trace.Length > 0 ? trace.ToString() : " no frame of note.")}");
            traceWhat = null;
            trace.Length = 0;
            traceNotes = null;
        }

        // A log line the mod wrote (counted for the panel: writing the log costs too).
        internal static void NoteLogLine() => logLines++;

        // Once per frame, first thing in Mount.Tick: closes the previous frame.
        internal static void BeginFrame()
        {
            int f = Time.frameCount;
            if (f == lastFrame)
            {
                return;
            }
            lastFrame = f;
            float now = Time.unscaledTime;
            if (windowStart < 0f)
            {
                windowStart = now;
                Clear();
                return;
            }
            double frameMs = Time.unscaledDeltaTime * 1000.0;
            if (frameMs > SlowFrameMs && frames > 0 && now >= nextSlowLog && LogEvery10s)
            {
                nextSlowLog = now + 1f;
                var sb = new StringBuilder();
                for (int i = 0; i < (int)S.Count; i++)
                {
                    double ms = frameTicks[i] * MsPerTick;
                    if (ms >= 0.5)
                    {
                        sb.Append($", {Names[i]} {ms:F1}");
                    }
                }
                Plugin.Log.LogInfo($"[perf] slow frame {frameMs:F0} ms: the mod{(sb.Length > 0 ? sb.ToString() : " under 0.5 ms in every part")}; allocated {allocFrame / 1024.0:F0} KB; GC collections so far {GC.CollectionCount(0)}.");
            }
            TraceFrame(frameMs);
            allocSum += allocFrame;
            allocMax = Math.Max(allocMax, allocFrame);
            logAlloc += allocFrame;
            logFrames++;
            if (phaseOn) { phaseAlloc += allocFrame; phaseFrames++; phaseFrameMs += frameMs; }
            allocFrame = 0;
            frames++;
            frameSumMs += frameMs;
            frameMaxMs = Math.Max(frameMaxMs, frameMs);
            for (int i = 0; i < (int)S.Count; i++)
            {
                double ms = frameTicks[i] * MsPerTick;
                sumMs[i] += ms;
                maxMs[i] = Math.Max(maxMs[i], ms);
                sumCalls[i] += frameCalls[i];
                sumBytes[i] += frameBytes[i];
                logBytes[i] += frameBytes[i];
                if (phaseOn) { phaseBytes[i] += frameBytes[i]; phaseTicks[i] += frameTicks[i]; }
                frameTicks[i] = 0;
                frameCalls[i] = 0;
                frameBytes[i] = 0;
            }
            if (now - windowStart >= 1f && frames > 0)
            {
                for (int i = 0; i < (int)S.Count; i++)
                {
                    AvgMs[i] = sumMs[i] / frames;
                    WorstMs[i] = maxMs[i];
                    CallsPerFrame[i] = (double)sumCalls[i] / frames;
                    BytesPerFrame[i] = (double)sumBytes[i] / frames;
                }
                FrameAvgMs = frameSumMs / frames;
                FrameWorstMs = frameMaxMs;
                Fps = frames / (now - windowStart);
                LogLinesPerSecond = (int)Math.Round(logLines / (now - windowStart));
                AllocKbPerFrame = allocSum / 1024.0 / frames;
                AllocKbWorst = allocMax / 1024.0;
                int gc = GC.CollectionCount(0);
                GcPerSecond = gcAtWindow < 0 ? 0 : gc - gcAtWindow;
                gcAtWindow = gc;
                HeapMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                CountInstances();
                facingWritesPerFrame = (double)FacingWrites / Math.Max(1, frames);
                positionWritesPerFrame = (double)PositionWrites / Math.Max(1, frames);
                poseSkipsPerFrame = (double)PoseSkips / Math.Max(1, frames);
                FacingWrites = PositionWrites = PoseSkips = 0;
                SummaryText = Summary(oneLine: false); // the panel shows this one (built once a second, not per event)
                windowStart = now;
                Clear();
                if (LogEvery10s && now >= nextLog)
                {
                    nextLog = now + 10f;
                    Plugin.Log.LogInfo("[perf] " + Summary(oneLine: true));
                    if (AllocBySection && logFrames > 0) Plugin.Log.LogInfo("[perf] " + AllocByPart());
                }
            }
        }

        // The garbage by part over the 10 s between two log lines (the 4 KB pages each part was in when the heap grew,
        // averaged: per part it is only rough in a single frame, and fair over a thousand). Parts hold their inner parts;
        // "outside the parts" is the mod's total (Tick and the panel window) less the parts at the top.
        private static readonly long[] logBytes = new long[(int)S.Count];
        private static long logAlloc;
        private static int logFrames;

        // A phase of a self-test measured on its own (SelfTest.Garbage.cs): its frames, the mod's time and garbage by part,
        // and the collections in it.
        private static bool phaseOn;
        private static readonly long[] phaseBytes = new long[(int)S.Count], phaseTicks = new long[(int)S.Count];
        private static long phaseAlloc;
        private static int phaseFrames, phaseGc;
        private static double phaseFrameMs;

        internal static void PhaseStart()
        {
            BeginFrame(); // the frame so far belongs to before the phase
            Array.Clear(phaseBytes, 0, phaseBytes.Length);
            Array.Clear(phaseTicks, 0, phaseTicks.Length);
            phaseAlloc = 0;
            phaseFrames = 0;
            phaseFrameMs = 0;
            phaseGc = GC.CollectionCount(0);
            phaseOn = true;
        }

        internal static void PhaseAbort() => phaseOn = false;

        internal static string PhaseEnd(string label)
        {
            phaseOn = false;
            int n = Math.Max(1, phaseFrames);
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < (int)S.Count; i++) if (phaseBytes[i] > 0 || phaseTicks[i] * MsPerTick / n >= 0.05) order.Add(i);
            order.Sort((a, b) => phaseBytes[b] != phaseBytes[a] ? phaseBytes[b].CompareTo(phaseBytes[a]) : phaseTicks[b].CompareTo(phaseTicks[a]));
            var sb = new StringBuilder();
            sb.Append($"[garbage] {label}: {phaseFrames} frames, {phaseFrameMs / n:F2} ms a frame, the mod {phaseTicks[(int)S.Tick] * MsPerTick / n:F2} ms; ");
            sb.Append($"garbage the mod {phaseAlloc / 1024.0 / n:F2} KB a frame ({phaseAlloc / 1024.0 / Math.Max(0.001, phaseFrameMs / 1000.0):F0} KB a second), {GC.CollectionCount(0) - phaseGc} collection(s); by part (holding its inner parts), KB and ms a frame:");
            foreach (int i in order) sb.Append($" {Names[i]} {phaseBytes[i] / 1024.0 / n:F2}/{phaseTicks[i] * MsPerTick / n:F2};");
            return sb.ToString();
        }

        private static string AllocByPart()
        {
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < (int)S.Count; i++) if (logBytes[i] / (double)logFrames >= 205) order.Add(i); // 0.2 KB a frame or more
            order.Sort((a, b) => logBytes[b].CompareTo(logBytes[a]));
            var sb = new StringBuilder();
            sb.Append($"garbage over the last {logFrames} frames, KB a frame (a part holds its inner parts): the mod {logAlloc / 1024.0 / logFrames:F1}");
            foreach (int i in order) sb.Append($", {Names[i]} {logBytes[i] / 1024.0 / logFrames:F1}");
            sb.Append('.');
            Array.Clear(logBytes, 0, logBytes.Length);
            logAlloc = 0;
            logFrames = 0;
            return sb.ToString();
        }

        private static void Clear()
        {
            frames = 0;
            frameSumMs = frameMaxMs = 0;
            logLines = 0;
            allocSum = allocMax = 0;
            Array.Clear(sumMs, 0, sumMs.Length);
            Array.Clear(maxMs, 0, maxMs.Length);
            Array.Clear(sumCalls, 0, sumCalls.Length);
            Array.Clear(sumBytes, 0, sumBytes.Length);
        }

        private static void CountInstances()
        {
            int n = 0, v = 0, f = 0, act = 0, idle = 0, asleep = 0;
            foreach (Mount.Instance i in Mount.All)
            {
                n++;
                if (i.HasView) v++;
                if (i.Parent != null) f++;
                if (i.tier == Mount.Tier.Idle) idle++;
                else if (i.tier == Mount.Tier.Asleep) asleep++;
                else act++;
            }
            Instances = n;
            Views = v;
            Foals = f;
            Lod.ActiveCount = act;
            Lod.IdleCount = idle;
            Lod.AsleepCount = asleep;
            SpawnedPerSecond = Lod.Spawned;
            ReleasedPerSecond = Lod.Released;
            ParkedPerSecond = Lod.Parked;
            ReusedPerSecond = Lod.Reused;
            Lod.Spawned = Lod.Released = Lod.Parked = Lod.Reused = 0;
        }

        internal static int SpawnedPerSecond, ReleasedPerSecond, ParkedPerSecond, ReusedPerSecond;

        // Pose's writes to the game's data (each runs the game's listeners), and the frames it had nothing to write.
        internal static int FacingWrites, PositionWrites, PoseSkips;
        private static double facingWritesPerFrame, positionWritesPerFrame, poseSkipsPerFrame;

        // Ours: the per-frame work (Tick) plus the panel (Panel contains Markers; Window is measured on its own).
        internal static double OursAvgMs => AvgMs[(int)S.Tick] + AvgMs[(int)S.Panel] + AvgMs[(int)S.Window];

        internal static string Summary(bool oneLine)
        {
            var sb = new StringBuilder();
            string sep = oneLine ? "; " : "\n";
            sb.Append($"{Fps:F0} fps, frame {FrameAvgMs:F1} ms (worst {FrameWorstMs:F0}); mod {OursAvgMs:F2} ms ({(FrameAvgMs > 0 ? OursAvgMs / FrameAvgMs * 100 : 0):F0}% of the frame); {Instances} instance(s), {Views} view(s), {Foals} foal(s); levels {Lod.ActiveCount} active / {Lod.IdleCount} idle / {Lod.AsleepCount} asleep{(Lod.On ? "" : " (levels off)")}, views made {SpawnedPerSecond}/s ({ReusedPerSecond} of them reused), released {ReleasedPerSecond}/s ({ParkedPerSecond} parked, {ViewPool.Count} in the pool); pose writes {positionWritesPerFrame:F1} place / {facingWritesPerFrame:F1} facing, {poseSkipsPerFrame:F1} skipped per frame; mod allocates {AllocKbPerFrame:F1} KB/frame (worst {AllocKbWorst:F0}{(AllocExact ? ", exact" : ", page-quantized")}), GC {GcPerSecond}/s, heap {HeapMb:F0} MB; live: {Live.Textures} texture(s) {Live.TextureBytes / 1024} KB, {Live.Materials} material(s), {Live.LampObjects} lamp(s), {Live.Meshes} mesh(es), {Live.CargoHolders} cargo holder(s), {Colors.SharedTextures.Count} shared colour texture(s); {LogLinesPerSecond} log line(s)/s");
            for (int i = 0; i < (int)S.Count; i++)
            {
                if (AvgMs[i] < 0.005 && CallsPerFrame[i] < 0.01)
                {
                    continue;
                }
                sb.Append(sep).Append($"{Names[i]}{(i == (int)S.WinTab && PanelTab != null ? $" [{PanelTab}]" : "")} {AvgMs[i]:F2} ms (worst {WorstMs[i]:F1}, {CallsPerFrame[i]:F1}x{(BytesPerFrame[i] >= 1 ? $", +{BytesPerFrame[i]:F0} B" : "")})");
            }
            return sb.ToString();
        }
    }
}

namespace GK2DonkeyAPI
{
    // Every Unity object the mod makes itself and must destroy itself (Unity never collects them), counted up when made
    // and down when destroyed: a number that keeps growing while the instances don't is a leak (docs/performance.md).
    internal static class Live
    {
        internal static int Textures, Materials, LampObjects, Meshes, CargoHolders;
        internal static long TextureBytes;

        internal static void TextureMade(UnityEngine.Texture2D t)
        {
            Textures++;
            TextureBytes += (long)t.width * t.height * 4;
        }

        internal static void TextureGone(UnityEngine.Texture2D t)
        {
            if (t == null) return;
            Textures--;
            TextureBytes -= (long)t.width * t.height * 4;
        }
    }

    // The panel's two experiments (session only): they tell the mod's share of a slow frame from the game's.
    //   ExpNoShuffle  standing donkeys skip their idle shuffle (and foals their following): the mod's movement work off.
    //   ExpHideViews  every standing donkey's view stops rendering and animating (forceRenderingOff, Animator off):
    //                 what drawing and animating the views costs the game. Undone when switched off.
    internal static partial class Mount
    {
        internal sealed partial class Instance
        {
            private Wgo expView;
            private readonly System.Collections.Generic.List<UnityEngine.Renderer> expRenderers = new System.Collections.Generic.List<UnityEngine.Renderer>();
            private readonly System.Collections.Generic.List<UnityEngine.Behaviour> expAnimators = new System.Collections.Generic.List<UnityEngine.Behaviour>();

            internal void ApplyPerfExperiment()
            {
                bool want = Perf.ExpHideViews && donkey != null && donkey;
                if (want && ReferenceEquals(expView, donkey))
                {
                    return;
                }
                if (!want && expView == null)
                {
                    return;
                }
                foreach (UnityEngine.Renderer r in expRenderers)
                {
                    if (r != null && r) r.forceRenderingOff = false;
                }
                foreach (UnityEngine.Behaviour a in expAnimators)
                {
                    if (a != null && a) a.enabled = true;
                }
                expRenderers.Clear();
                expAnimators.Clear();
                expView = null;
                if (!want)
                {
                    return;
                }
                foreach (UnityEngine.Renderer r in donkey.GetComponentsInChildren<UnityEngine.Renderer>(includeInactive: true))
                {
                    if (!r.forceRenderingOff)
                    {
                        r.forceRenderingOff = true;
                        expRenderers.Add(r);
                    }
                }
                foreach (UnityEngine.Animator a in donkey.GetComponentsInChildren<UnityEngine.Animator>(includeInactive: true))
                {
                    if (a.enabled)
                    {
                        a.keepAnimatorStateOnDisable = true; // keeps its facing for when the experiment ends (Tiers.cs)
                        a.enabled = false;
                        expAnimators.Add(a);
                    }
                }
                expView = donkey;
            }
        }
    }
}
