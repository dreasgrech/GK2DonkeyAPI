using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Setting a key in our windows (a key box, RowControls): a click starts it, the next key pressed is the key, with the
    // Shift, Ctrl or Alt held with it. A Shift, Ctrl or Alt let go with no other key is the key on its own (the Climb and
    // Descend keys' defaults, Left Shift and Left Ctrl: the game's Settings window and GK2 Mod Framework couldn't set one).
    // Backspace alone: no key. Escape, the gamepad's Back, a click elsewhere or 10 seconds: cancelled. One key at a time;
    // read every frame before the mods' keys act (Plugin.LateUpdate), and while it waits the mods' keys and our windows'
    // Escape wait (Active), so pressing G to set a key doesn't also bring the cart.
    internal static class KeyCapture
    {
        private const float Seconds = 10f;

        private static MountSettingsPage.Row row;
        private static GameObject owner;   // the window it was started in: gone or hidden, it's cancelled
        private static Action changed;     // the window follows (its key box says "Press a key...", then the key)
        private static int from, endedFrame = -10;
        private static float until;
        private static KeyCode loneModifier; // a modifier pressed with nothing else yet: on its own when let go
        private static KeyCode[] keyboardKeys;

        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        // A key is being set, or was this frame: the mods' keys and our windows' Escape wait.
        internal static bool Active => row != null || Time.frameCount == endedFrame;

        // The row whose key is being set, or null.
        internal static MountSettingsPage.Row Row => row;

        internal static void Start(MountSettingsPage.Row r, GameObject window, Action onChange)
        {
            if (r == null || r.Entry == null || r.RowKind != MountSettingsPage.RowKind.Key) return;
            Action before = changed;
            row = r;
            owner = window;
            changed = onChange;
            from = Time.frameCount + 1; // not the press that started it (a click, Select)
            until = Time.unscaledTime + Seconds;
            loneModifier = KeyCode.None;
            if (before != null && before != onChange) Run(before);
            Run(changed);
        }

        internal static void Cancel()
        {
            if (row == null) return;
            End();
        }

        private static void End()
        {
            Action after = changed;
            row = null;
            owner = null;
            changed = null;
            endedFrame = Time.frameCount;
            loneModifier = KeyCode.None;
            Run(after);
        }

        private static void Run(Action a)
        {
            try
            {
                a?.Invoke();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the window could not follow the key being set (" + e.Message + ").");
            }
        }

        // Every frame (Plugin.LateUpdate, before the mods' keys act).
        internal static void Tick()
        {
            if (row == null) return;
            if (owner == null || !owner.activeInHierarchy || Time.unscaledTime > until)
            {
                End(); // cancelled: its window went, or nothing was pressed
                return;
            }
            if (Time.frameCount < from) return;
            var input = UnityInput.Current;
            if (input.GetKeyDown(KeyCode.Escape) || (LazyInput.IsGamepadActive && LazyInput.GetKeyDown(GameKey.Back)))
            {
                try { LazyInput.ClearKeyDown(GameKey.InGameMenu); LazyInput.ClearKeyDown(GameKey.Back); } catch { } // this Escape cancels the key, nothing else
                End();
                return;
            }
            if (input.GetKeyDown(KeyCode.Mouse0) || input.GetKeyDown(KeyCode.Mouse1))
            {
                End(); // a click elsewhere (a click on a key box starts that one: the UI's click comes before this)
                return;
            }
            if (keyboardKeys == null)
            {
                var list = new List<KeyCode>();
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    // the keyboard only: no mouse or joystick buttons; the modifiers are read apart
                    if (k != KeyCode.None && k < KeyCode.Mouse0 && Array.IndexOf(Modifiers, k) < 0 && !list.Contains(k)) list.Add(k);
                }
                keyboardKeys = list.ToArray();
            }
            foreach (KeyCode k in keyboardKeys)
            {
                if (!input.GetKeyDown(k)) continue;
                List<KeyCode> held = Held(KeyCode.None);
                Set(k == KeyCode.Backspace && held.Count == 0 ? KeyboardShortcut.Empty : new KeyboardShortcut(k, held.ToArray()));
                return;
            }
            foreach (KeyCode m in Modifiers)
            {
                if (input.GetKeyDown(m) && loneModifier == KeyCode.None) loneModifier = m;
            }
            if (loneModifier != KeyCode.None && input.GetKeyUp(loneModifier))
            {
                Set(new KeyboardShortcut(loneModifier, Held(loneModifier).ToArray())); // let go with no other key: it is the key, the others held with it
            }
        }

        private static List<KeyCode> Held(KeyCode except)
        {
            var held = new List<KeyCode>();
            foreach (KeyCode m in Modifiers) if (m != except && UnityInput.Current.GetKey(m)) held.Add(m);
            return held;
        }

        private static void Set(KeyboardShortcut set)
        {
            MountSettingsPage.Row r = row;
            try
            {
                r.Entry.BoxedValue = set; // its SettingChanged applies it (MountSettingsPage.Wire)
                Plugin.Log.LogInfo($"Settings: {r.Label} set to {Words(set)}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {r.Label} could not be set: {e.Message}");
            }
            End();
        }

        // The self-test's way to set a key (it can't press one): started as a click starts it, then set as a press would.
        internal static bool TestSet(MountSettingsPage.Row r, GameObject window, Action onChange, KeyboardShortcut set)
        {
            Start(r, window, onChange);
            if (row != r) return false;
            Set(set);
            return true;
        }

        // Another key row with the same key, when either acts on its press (both would act): its label, or null. Two rows
        // the API reads apart (the Jump and Fly keys share Space by design) are no clash.
        internal static string Clash(MountSettingsPage.Row r)
        {
            if (r == null || r.RowKind != MountSettingsPage.RowKind.Key || r.Entry == null) return null;
            var k = (KeyboardShortcut)r.Entry.BoxedValue;
            if (k.MainKey == KeyCode.None) return null;
            foreach (MountSettingsPage p in MountSettingsPage.All)
            {
                foreach (MountSettingsPage.Row o in p.Rows)
                {
                    if (o == r || o.RowKind != MountSettingsPage.RowKind.Key || o.Entry == null || !(r.HasPress || o.HasPress)) continue;
                    if (KeyActions.SameKey(k, (KeyboardShortcut)o.Entry.BoxedValue)) return o.Label;
                }
            }
            return null;
        }

        // A key in words: "H", "Ctrl + G", "Left Shift", "None".
        internal static string Words(KeyboardShortcut k)
        {
            if (k.MainKey == KeyCode.None) return "None";
            var sb = new StringBuilder();
            bool any = false;
            foreach (KeyCode m in k.Modifiers)
            {
                sb.Append(ModifierWord(m)).Append(" + ");
                any = true;
            }
            sb.Append(any || Array.IndexOf(Modifiers, k.MainKey) < 0 ? Word(k.MainKey) : LoneModifierWord(k.MainKey));
            return sb.ToString();
        }

        private static string ModifierWord(KeyCode m)
        {
            switch (m)
            {
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                default: return Word(m);
            }
        }

        private static string LoneModifierWord(KeyCode m)
        {
            switch (m)
            {
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                default: return Word(m);
            }
        }

        private static string Word(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Num " + (int)(k - KeyCode.Keypad0);
            switch (k)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
            }
            // "PageUp" -> "Page Up"
            string s = k.ToString();
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
