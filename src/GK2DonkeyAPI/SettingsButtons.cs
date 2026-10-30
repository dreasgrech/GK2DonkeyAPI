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
    // game's own Mods window makes its buttons (UICodeModsWindow.CreateButton, read 2026-10-30). Found through
    // LazyWindowsStackController.OnWindowOpened (no patch), added the first time each Settings window opens.
    // Guarded: if the window or its button isn't found (a game update changed it), nothing is added, the options are
    // still in the cfg, and the log says so once.
    internal static class SettingsButtons
    {
        private const string Prefix = "GK2DonkeyAPI options button ";

        private static readonly List<MountSettingsPage> pages = new List<MountSettingsPage>();
        private static bool subscribed, failed;

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

        private static void Fail(string why)
        {
            failed = true;
            Plugin.Log.LogWarning($"Settings: the mods' options buttons can't go into the game's Settings window ({why}). The options are in their cfg files (BepInEx\\config).");
        }

        // The window's buttons: one per page, above its OK button, made once per window (a new window after a language
        // change gets its own).
        private static void AddButtons(UIGameSettingsWindow settings)
        {
            UIDialogWindowButton ok = null;
            foreach (UIDialogWindowButton b in settings.GetComponentsInChildren<UIDialogWindowButton>(true))
            {
                if (b.name.StartsWith(Prefix, StringComparison.Ordinal)) return; // ours are there already
                if (ok == null) ok = b;
            }
            if (ok == null)
            {
                Fail("no button to copy in the window");
                return;
            }
            Transform parent = ok.transform.parent;
            int at = ok.transform.GetSiblingIndex();
            foreach (MountSettingsPage page in pages)
            {
                MountSettingsPage p = page;
                UIDialogWindowButton b = UnityEngine.Object.Instantiate(ok, parent, false);
                b.name = Prefix + p.OwnerId;
                foreach (LocalizedLabel l in b.GetComponentsInChildren<LocalizedLabel>(true)) l.IgnoreLocalize = true; // our words, not a translation key's
                // replaceForGamepad false: a button on screen with the gamepad too (the OK button is a key tip there)
                b.Draw(new UIDialogWindowData.ButtonData(() => OptionsWindow.Open(p), p.Title + " options", null, replaceForGamepad: false, GameKey.Select));
                GamepadNavigationItem item = b.GetComponentInChildren<GamepadNavigationItem>(true);
                if (item == null && b.LazyButton != null)
                {
                    b.LazyButton.gameObject.AddComponent<GamepadNavigationItem>();
                    b.LazyButton.SetCallbacksIntoGamepadNavigationItem();
                }
                b.transform.SetSiblingIndex(at++);
                b.gameObject.SetActive(true);
            }
            ((RectTransform)settings.transform).RefreshContentFitter();
            settings.GetComponent<GamepadNavigationController>()?.ReinitItems(focusOnFirstActive: false);
            Plugin.Log.LogInfo($"Settings: {pages.Count} options button(s) added to the game's Settings window.");
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
    }
}
