using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
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

        // What a row's key shares with another key, as the line under its name, or null (docs/options-window.md §14). Not
        // only the same combination: one press can do two keys' actions when the readers ignore keys held with theirs.
        // Ours ignore every key held but their modifiers (walking keys, the climb's Shift); the game does the same; most
        // other mods read BepInEx's IsDown, which any other key held stops; a mod's plain KeyCode setting is read alone.
        // - another key row of a page, when either acts on its press (two rows the API reads apart, the Jump and Fly keys
        //   sharing Space by design, are no clash): the same combination; or one's main key is a modifier of the other's
        //   (a key on Left Shift goes off on the way into Shift + H), when that one acts on its press (Climb and Descend,
        //   held on Left Shift and Left Ctrl while flying, would mark every Shift + and Ctrl + key). Ctrl + G over G of ours is no clash: the most
        //   specific of ours acts alone (KeyActions.Shadowed);
        // - another mod's key setting ("GK2 Tech Tree Search: ToggleKey"): its exact press doing ours too (the same main
        //   key, ours' modifiers among its), ours doing it (the same combination), or one's main key a modifier of the other;
        //   a plain KeyCode setting: it is our main key or one of our modifiers;
        // - for a row acting on its press, the game's own key ("the game's Inventory"): it is our main key, and one's keys
        //   held with it are among the other's.
        // "Same key as X" for the same combination, "Clashes with X (its key)" for an overlap. The other mods' and the
        // game's keys are read when the options window opens (Snapshot).
        internal static string Clash(MountSettingsPage.Row r)
        {
            if (r == null || r.RowKind != MountSettingsPage.RowKind.Key || r.Entry == null) return null;
            var k = (KeyboardShortcut)r.Entry.BoxedValue;
            if (k.MainKey == KeyCode.None) return null;
            List<KeyCode> km = ModsOf(k);
            foreach (MountSettingsPage p in MountSettingsPage.All)
            {
                foreach (MountSettingsPage.Row o in p.Rows)
                {
                    if (o == r || o.RowKind != MountSettingsPage.RowKind.Key || o.Entry == null || !(r.HasPress || o.HasPress)) continue;
                    var ok = (KeyboardShortcut)o.Entry.BoxedValue;
                    if (ok.MainKey == KeyCode.None) continue;
                    if (KeyActions.SameKey(k, ok)) return "Same key as " + o.Label;
                    List<KeyCode> om = ModsOf(ok);
                    // the one on the lone modifier must act on its press to go off on the way: a key read while held (Climb,
                    // Descend: Left Shift, Left Ctrl, while flying) marked every Shift + and Ctrl + key of ours, at worst a dip
                    if ((o.HasPress && OnTheWay(ok.MainKey, om, km)) || (r.HasPress && OnTheWay(k.MainKey, km, om))) return $"Clashes with {o.Label} ({Words(ok)})";
                }
            }
            foreach (OtherKey o in others)
            {
                switch (o.Kind)
                {
                    case OtherKind.Shortcut:
                        if (KeyActions.SameKey(k, o.Shortcut)) return "Same key as " + o.Name;
                        List<KeyCode> om = ModsOf(o.Shortcut);
                        // ours on a lone modifier counts only when it acts on its press: Climb and Descend (held while flying) met
                        // every Shift + and Ctrl + key of every mod ("Descend (hold) (Left Ctrl): Clashes with GK2 Tech Tree Search: FocusKey (Ctrl + F)")
                        if ((o.Shortcut.MainKey == k.MainKey && Inside(km, om)) || (r.HasPress && OnTheWay(k.MainKey, km, om)) || OnTheWay(o.Shortcut.MainKey, om, km))
                            return $"Clashes with {o.Name} ({Words(o.Shortcut)})";
                        break;
                    case OtherKind.Code: // a mod's plain KeyCode setting: most read the key alone, whatever is held with it
                        if (k.MainKey == o.Code && km.Count == 0) return "Same key as " + o.Name;
                        if (k.MainKey == o.Code || km.Contains(o.Code)) return $"Clashes with {o.Name} ({Word(o.Code)})";
                        break;
                    case OtherKind.Game: // the game's binding: its key, the keys it wants held (held with more, it still acts)
                        if (!r.HasPress || k.MainKey != o.Code) break;
                        var gm = new List<KeyCode>(o.Held ?? new KeyCode[0]);
                        if (Inside(gm, km) && Inside(km, gm)) return "Same key as " + o.Name;
                        if (Inside(gm, km) || Inside(km, gm)) return $"Clashes with {o.Name} ({GameWords(o.Code, gm)})";
                        break;
                }
            }
            return null;
        }

        // A shortcut's modifiers, in their order.
        private static List<KeyCode> ModsOf(KeyboardShortcut k)
        {
            var m = new List<KeyCode>();
            foreach (KeyCode c in k.Modifiers) m.Add(c);
            return m;
        }

        // Every key of a among b's.
        private static bool Inside(List<KeyCode> a, List<KeyCode> b)
        {
            foreach (KeyCode c in a) if (!b.Contains(c)) return false;
            return true;
        }

        // A key whose main key is one of another's modifiers, its own modifiers among the rest: it goes off on the way into
        // the other (Left Shift on the way into Shift + H).
        private static bool OnTheWay(KeyCode main, List<KeyCode> mods, List<KeyCode> otherMods) => otherMods.Contains(main) && Inside(mods, otherMods);

        private static string GameWords(KeyCode code, List<KeyCode> held)
        {
            var sb = new StringBuilder();
            foreach (KeyCode h in held) sb.Append(ModifierWord(h)).Append(" + ");
            return sb.Append(Word(code)).ToString();
        }

        private enum OtherKind { Shortcut, Code, Game }

        private struct OtherKey
        {
            internal OtherKind Kind;
            internal string Name;
            internal KeyboardShortcut Shortcut;
            internal KeyCode Code;
            internal KeyCode[] Held;
        }

        private static readonly List<OtherKey> others = new List<OtherKey>();
        private static bool gameKeysFailed;

        // The keys other mods and the game use, read now (the options window opening): every loaded plugin's cfg but ours
        // (the pages' own and the API's), its KeyboardShortcut and KeyCode settings; the game's keyboard bindings but the
        // menus' own (Up, Down, Select, Back...).
        internal static void Snapshot()
        {
            others.Clear();
            var ours = new HashSet<ConfigFile>();
            foreach (MountSettingsPage p in MountSettingsPage.All) if (p.Config != null) ours.Add(p.Config);
            foreach (KeyValuePair<string, PluginInfo> kv in Chainloader.PluginInfos)
            {
                PluginInfo info = kv.Value;
                if (info == null || info.Instance == null || info.Metadata.GUID == Plugin.Guid) continue;
                ConfigFile cfg;
                try { cfg = info.Instance.Config; } catch { continue; }
                if (cfg == null || ours.Contains(cfg)) continue;
                try
                {
                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> e in cfg)
                    {
                        object v = e.Value?.BoxedValue;
                        string name = info.Metadata.Name + ": " + e.Key.Key;
                        if (v is KeyboardShortcut s && s.MainKey != KeyCode.None) others.Add(new OtherKey { Kind = OtherKind.Shortcut, Name = name, Shortcut = s });
                        else if (v is KeyCode c && c != KeyCode.None) others.Add(new OtherKey { Kind = OtherKind.Code, Name = name, Code = c });
                    }
                }
                catch (Exception x)
                {
                    Plugin.Log.LogInfo($"Settings: the keys of {info.Metadata.Name} could not be read for the clash check ({x.Message}).");
                }
            }
            try
            {
                foreach (KeyBinding b in LazyInput.GameBindings.keyBindings)
                {
                    if (b == null || b.gameKey == null || b.gameKey.value < 100 || b.keyCode == KeyCode.None) continue; // under 100: the menus' keys
                    others.Add(new OtherKey { Kind = OtherKind.Game, Name = "the game's " + GameKeyName(b), Code = b.keyCode, Held = b.additionalKeyCodes });
                }
            }
            catch (Exception x)
            {
                if (!gameKeysFailed) Plugin.Log.LogInfo("Settings: the game's key bindings could not be read for the clash check (" + x.Message + ").");
                gameKeysFailed = true;
            }
        }

        // A game binding's name in the game's language ("Inventory"), else its key's name.
        private static string GameKeyName(KeyBinding b)
        {
            string n = null;
            try { n = string.IsNullOrEmpty(b.localeId) ? null : LLBase.L(b.localeId); } catch { }
            if (string.IsNullOrEmpty(n) || n == b.localeId || n.StartsWith("[", StringComparison.Ordinal)) n = Enumeration.GetNameOfStaticField<GameKey>(b.gameKey.value);
            return string.IsNullOrEmpty(n) ? "key " + b.gameKey.value : n;
        }

        // The self-test: a key another mod has (as Snapshot would read it), until the next Snapshot.
        internal static void TestAddOther(string name, KeyboardShortcut key) => others.Add(new OtherKey { Kind = OtherKind.Shortcut, Name = name, Shortcut = key });

        // The self-test: how many keys of other mods and of the game the last Snapshot read.
        internal static (int Mods, int Game) TestOtherCounts()
        {
            int mods = 0, game = 0;
            foreach (OtherKey o in others) if (o.Kind == OtherKind.Game) game++; else mods++;
            return (mods, game);
        }

        // The self-test: the game's first binding the clash check would name (its key alone), or KeyCode.None.
        internal static KeyCode TestGameKey(out string name)
        {
            foreach (OtherKey o in others)
            {
                if (o.Kind == OtherKind.Game && (o.Held == null || o.Held.Length == 0)) { name = o.Name; return o.Code; }
            }
            name = null;
            return KeyCode.None;
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
