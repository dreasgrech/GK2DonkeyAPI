using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The game's Settings window (the pause menu's and the main menu's): a button per mod settings page, "<mod> options",
    // above its OK button, opening the page's options window (OptionsWindow; docs/options-window.md). The button is a
    // copy of the window's own OK button (its look, sounds and gamepad stop), drawn with our words and action, as the
    // game's own Mods window makes its buttons (UICodeModsWindow.CreateButton, read 2026-10-30).
    //
    // Other mods in the same window (the user, 2026-10-30: "hundreds of other mods"; docs/options-window.md §12):
    // - the OK button copied is the window's own (UiKit.OkButtonOf: its field), not the first button found;
    // - the copy is made inactive and cleaned of every component that isn't the game's (UiKit.StripForeign: a mod's
    //   component on OK would act on our button too) before it wakes;
    // - added when the window opens (LazyWindowsStackController.OnWindowOpened, no patch), and checked twice a second
    //   while the window is on top (Tick): the game raises that event as one multicast, so another mod's handler that
    //   throws before ours stops ours, and a mod may rebuild the window's content; a missing button is added again.
    // Guarded: if the window or its button isn't found (a game update changed it), nothing is added, the options are
    // still in the cfg, and the log says so once.
    internal static class SettingsButtons
    {
        private const string Prefix = "GK2DonkeyAPI options button ";
        private const float CheckEvery = 0.5f;

        private static readonly List<MountSettingsPage> pages = new List<MountSettingsPage>();
        private static readonly Dictionary<MountSettingsPage, UIDialogWindowButton> made = new Dictionary<MountSettingsPage, UIDialogWindowButton>();
        private static UIGameSettingsWindow madeIn;
        private static bool subscribed, failed, readdSaid;
        private static float nextCheck;

        internal static void Add(MountSettingsPage page)
        {
            if (!OptionsWindow.HasOptions(page)) return;
            pages.Add(page);
            if (!subscribed)
            {
                subscribed = true;
                LazyWindowsStackController.OnWindowOpened += OnOpened;
            }
            int n = 0;
            foreach (MountSettingsPage.Row r in page.Rows) if (page.InOptions(r)) n++;
            Plugin.Log.LogInfo($"Settings: {page.Title}'s options ({n} rows) open from its button in the game's Settings window and from the Mounts panel.");
        }

        private static void OnOpened(LazyWidgetBase w)
        {
            if (failed || !(w is UIGameSettingsWindow settings)) return;
            try
            {
                AddButtons(settings);
            }
            catch (Exception e)
            {
                Fail("adding the buttons failed: " + e.Message);
            }
        }

        // Every frame (Plugin.LateUpdate), twice a second at most: while the Settings window is the window on top, every
        // page's button is there (added again if not).
        internal static void Tick()
        {
            if (failed || pages.Count == 0 || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckEvery;
            if (!(LazyWindowsStackController.ActiveWindow is UIGameSettingsWindow settings) || !settings.gameObject.activeInHierarchy) return;
            if (AllThere(settings)) return;
            try
            {
                if (!readdSaid)
                {
                    readdSaid = true; // said once: a mod that rebuilds the window every open would say it each time
                    Plugin.Log.LogInfo("Settings: the options button wasn't in the open Settings window (another mod's window handler failed before ours, or it rebuilt the window): added now. Said once.");
                }
                AddButtons(settings);
            }
            catch (Exception e)
            {
                Fail("adding the buttons failed: " + e.Message);
            }
        }

        private static bool AllThere(UIGameSettingsWindow settings)
        {
            if (madeIn != settings) return false;
            foreach (MountSettingsPage p in pages)
            {
                if (!made.TryGetValue(p, out UIDialogWindowButton b) || b == null || !b.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static void Fail(string why)
        {
            failed = true;
            Plugin.Log.LogWarning($"Settings: the mods' options buttons can't go into the game's Settings window ({why}). The options are in their cfg files (BepInEx\\config).");
        }

        // The window's buttons: one per page, above its OK button; a page's button already there is kept (a new window,
        // after a language change, gets its own).
        private static void AddButtons(UIGameSettingsWindow settings)
        {
            if (madeIn != settings)
            {
                made.Clear();
                madeIn = settings;
            }
            UIDialogWindowButton ok = UiKit.OkButtonOf(settings);
            if (ok == null)
            {
                Fail("no OK button to copy in the window");
                return;
            }
            Transform parent = ok.transform.parent;
            int added = 0;
            foreach (MountSettingsPage page in pages)
            {
                if (made.TryGetValue(page, out UIDialogWindowButton was) && was != null)
                {
                    if (!was.gameObject.activeSelf) was.gameObject.SetActive(true); // switched off by another mod: on again
                    continue;
                }
                MountSettingsPage p = page;
                // copied under an inactive holder: nothing of the copy wakes before it is cleaned of other mods' parts
                var holder = new GameObject("GK2DonkeyAPI options button holder", typeof(RectTransform));
                holder.SetActive(false);
                holder.transform.SetParent(parent, false);
                UIDialogWindowButton b;
                try
                {
                    b = UnityEngine.Object.Instantiate(ok, holder.transform, false);
                    b.name = Prefix + p.OwnerId;
                    List<string> removed = UiKit.StripForeign(b.gameObject);
                    if (removed.Count > 0) Plugin.Log.LogInfo($"Settings: the Settings window's OK button carries {removed.Count} part(s) of other mods ({string.Join(", ", removed)}): left off our copy.");
                    b.transform.SetParent(parent, false);
                    b.transform.SetSiblingIndex(ok.transform.GetSiblingIndex()); // just above OK
                }
                finally
                {
                    UnityEngine.Object.Destroy(holder);
                }
                foreach (LocalizedLabel l in b.GetComponentsInChildren<LocalizedLabel>(true)) l.IgnoreLocalize = true; // our words, not a translation key's
                b.gameObject.SetActive(true);
                // replaceForGamepad false: a button on screen with the gamepad too (the OK button is a key tip there)
                b.Draw(new UIDialogWindowData.ButtonData(() => OptionsWindow.Open(p), p.Title + " options", null, replaceForGamepad: false, GameKey.Select));
                GamepadNavigationItem item = b.GetComponentInChildren<GamepadNavigationItem>(true);
                if (item == null && b.LazyButton != null)
                {
                    b.LazyButton.gameObject.AddComponent<GamepadNavigationItem>();
                    b.LazyButton.SetCallbacksIntoGamepadNavigationItem();
                }
                made[page] = b;
                added++;
            }
            if (added == 0) return;
            ((RectTransform)settings.transform).RefreshContentFitter();
            settings.GetComponent<GamepadNavigationController>()?.ReinitItems(focusOnFirstActive: false);
            Plugin.Log.LogInfo($"Settings: {added} options button(s) added to the game's Settings window.");
        }

        // The self-test's view: the button of a page in a Settings window, or null.
        internal static UIDialogWindowButton ButtonOf(UIGameSettingsWindow settings, MountSettingsPage page)
        {
            if (settings == null || page == null) return null;
            foreach (UIDialogWindowButton b in settings.GetComponentsInChildren<UIDialogWindowButton>(true))
            {
                if (b.name == Prefix + page.OwnerId) return b;
            }
            return null;
        }

        internal static string TextOf(UIDialogWindowButton b) => b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true)?.text : null;

        // The self-test: the watchdog checks now (not half a second on).
        internal static void TestCheckNow()
        {
            nextCheck = 0f;
            Tick();
        }
    }
}
