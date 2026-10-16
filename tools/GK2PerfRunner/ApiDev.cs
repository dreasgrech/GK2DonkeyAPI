using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace GK2PerfRunner
{
    // The Donkey API's dev internals the runner needs, by reflection (they are internal; the runner is never shipped):
    //   TestLoads.Put / TakeOff / OnCart: the test load on the driven cart (Dev/TestLoads.cs). Put refuses while the cart
    //     holds any cargo that isn't a test load; TakeOff and OnCart work only while driving (Cart style).
    //   KeyActions.entries: the settings pages' keys and their pressed actions. Saddle Up's RideKey and CartKey actions
    //     are invoked as they are, so Performance Hog bills the work to Saddle Up's own handler (its entry point), the
    //     same as a real key press, not to the runner.
    //   Plugin.TuningWindowEnabled: the dev panel's switch (its [perf] logging adds to the frames).
    internal static class ApiDev
    {
        internal const string SaddleUp = "dreas.gk2.saddleup";
        private static Type testLoads, keyActions, plugin;

        internal static string Problem { get; private set; }

        internal static bool Find()
        {
            if (testLoads != null) return true;
            Assembly a = typeof(GK2DonkeyAPI.Api.Mounts).Assembly;
            testLoads = a.GetType("GK2DonkeyAPI.TestLoads");
            keyActions = a.GetType("GK2DonkeyAPI.KeyActions");
            plugin = a.GetType("GK2DonkeyAPI.Plugin");
            if (testLoads == null || keyActions == null || plugin == null
                || testLoads.GetMethod("Put", BindingFlags.Static | BindingFlags.Public) == null
                || testLoads.GetMethod("TakeOff", BindingFlags.Static | BindingFlags.Public) == null
                || testLoads.GetMethod("OnCart", BindingFlags.Static | BindingFlags.NonPublic) == null
                || keyActions.GetField("entries", BindingFlags.Static | BindingFlags.NonPublic) == null)
            {
                Problem = "the Donkey API's dev internals changed (TestLoads, KeyActions)";
                testLoads = null;
                return false;
            }
            return true;
        }

        internal static string PutLoad(IList<string> ids, string why) =>
            (string)testLoads.GetMethod("Put", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { ids, why });

        internal static string TakeOff() => (string)testLoads.GetMethod("TakeOff", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);

        internal static int OnCart() => (int)testLoads.GetMethod("OnCart", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        internal static bool TuningWindowOn()
        {
            try
            {
                var e = plugin.GetField("TuningWindowEnabled", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as ConfigEntry<bool>;
                return e != null && e.Value;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Saddle Up's key action for its settings key (RideKey: H, CartKey: G), as registered; null if not found.
        internal static Action KeyAction(string key)
        {
            var list = keyActions.GetField("entries", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as IList;
            if (list == null) return null;
            foreach (object e in list)
            {
                Type t = e.GetType();
                string owner = t.GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(e) as string;
                var entry = t.GetField("Key", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(e) as ConfigEntry<BepInEx.Configuration.KeyboardShortcut>;
                if (owner == SaddleUp && entry != null && entry.Definition.Key == key)
                {
                    return t.GetField("Pressed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(e) as Action;
                }
            }
            return null;
        }
    }
}
