using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Logging;

namespace GK2PerfRunner
{
    // A count of every static collection in the Donkey API (lists, dictionaries, sets, queues, arrays of objects), taken
    // at the start and the end of a suite: the ones that grew name the cache or the leak behind Performance Hog's memory
    // column, which gives one total per mod (the first suites, 2026-10-16: +99 KB, then +39 KB). Read only, by
    // reflection; instance fields are not walked (each instance's own state goes with the instance).
    internal static class Census
    {
        internal static Dictionary<string, int> Take()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            Assembly a = typeof(GK2DonkeyAPI.Api.Mounts).Assembly;
            Type[] types;
            try
            {
                types = a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = Array.FindAll(e.Types, t => t != null);
            }
            foreach (Type t in types)
            {
                if (t.ContainsGenericParameters)
                {
                    continue;
                }
                foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral)
                    {
                        continue;
                    }
                    object v;
                    try
                    {
                        v = f.GetValue(null);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (v is string || !(v is ICollection c))
                    {
                        continue;
                    }
                    if (v is Array arr && arr.GetType().GetElementType().IsValueType)
                    {
                        continue; // a fixed buffer of numbers, not a cache
                    }
                    counts[t.FullName + "." + f.Name] = c.Count;
                }
            }
            return counts;
        }

        // The collections that grew between two counts (and the new ones), largest growth first, for the log.
        internal static string Grown(Dictionary<string, int> before, Dictionary<string, int> after, out int grownCount)
        {
            var rows = new List<(string Name, int From, int To)>();
            foreach (var kv in after)
            {
                int from = before.TryGetValue(kv.Key, out int b) ? b : 0;
                if (kv.Value > from)
                {
                    rows.Add((kv.Key, from, kv.Value));
                }
            }
            rows.Sort((x, y) => (y.To - y.From).CompareTo(x.To - x.From));
            grownCount = rows.Count;
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                sb.Append($"\n    {r.Name}: {r.From} -> {r.To} (+{r.To - r.From})");
            }
            return sb.ToString();
        }

        internal static void LogGrown(ManualLogSource log, string what, Dictionary<string, int> before, Dictionary<string, int> after)
        {
            string list = Grown(before, after, out int n);
            log.LogInfo(n == 0
                ? $"[perfrunner] census {what}: no static collection of the Donkey API grew ({after.Count} counted)."
                : $"[perfrunner] census {what}: {n} of {after.Count} static collections of the Donkey API grew:{list}");
        }
    }
}
