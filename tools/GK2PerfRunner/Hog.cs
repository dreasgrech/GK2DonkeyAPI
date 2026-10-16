using System;
using System.Linq;
using System.Reflection;

namespace GK2PerfRunner
{
    // Performance Hog (1.2.0), reached by reflection: its classes are internal, their members public static.
    //   Recorder.State: Idle, Preparing (patching its timers, 8 ms a frame), Recording, Stopping (taking them off).
    //   Recorder.Start() returns silently unless Idle, the mod catalog is built and Performance Hog is on in the Mods
    //     menu (Collectors.ModEnabled): the caller checks the state afterwards.
    //   Recorder.Stop(): Finish() keeps the recording (Last) and its frames (Timeline.Last) at once; the timers come off
    //     over the next frames (Stopping), so the next Start or a snapshot waits for Idle.
    //   MemorySnapshot.Start(): walks over frames (Walking); never during a recording.
    //   ReportWriter.Write(out string error): the report and its -frames.csv in BepInEx/PerformanceHog; returns the
    //     report's path, or null (error says why).
    internal static class Hog
    {
        private static Type recorder, snapshot, writer, collectors;
        private static bool looked;

        internal static string Problem { get; private set; }

        internal static bool Find()
        {
            if (looked) return Problem == null;
            looked = true;
            Assembly a = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "PerformanceHog");
            if (a == null)
            {
                Problem = "Performance Hog is not installed";
                return false;
            }
            recorder = a.GetType("PerformanceHog.Recorder");
            snapshot = a.GetType("PerformanceHog.MemorySnapshot");
            writer = a.GetType("PerformanceHog.ReportWriter");
            collectors = a.GetType("PerformanceHog.Collectors");
            if (recorder == null || snapshot == null || writer == null || collectors == null
                || recorder.GetProperty("State") == null || recorder.GetMethod("Start", Type.EmptyTypes) == null
                || recorder.GetMethod("Stop", Type.EmptyTypes) == null || snapshot.GetMethod("Start", Type.EmptyTypes) == null
                || snapshot.GetProperty("Walking") == null || writer.GetMethod("Write") == null || collectors.GetProperty("ModEnabled") == null)
            {
                Problem = $"Performance Hog {a.GetName().Version} is not the version this runner knows (1.2.0)";
                return false;
            }
            return true;
        }

        internal static string State => Find() ? recorder.GetProperty("State").GetValue(null).ToString() : "missing";
        internal static bool Idle => State == "Idle";
        internal static bool Recording => State == "Recording";
        internal static bool Enabled => Find() && (bool)collectors.GetProperty("ModEnabled").GetValue(null);
        internal static bool Walking => Find() && (bool)snapshot.GetProperty("Walking").GetValue(null);

        internal static void StartRecording() => recorder.GetMethod("Start", Type.EmptyTypes).Invoke(null, null);
        internal static void StopRecording() => recorder.GetMethod("Stop", Type.EmptyTypes).Invoke(null, null);
        internal static bool StartSnapshot() => (bool)snapshot.GetMethod("Start", Type.EmptyTypes).Invoke(null, null);

        internal static string Export(out string error)
        {
            object[] args = { null };
            string path = (string)writer.GetMethod("Write").Invoke(null, args);
            error = (string)args[0];
            return path;
        }
    }
}
