using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The keys of the mods' settings pages that do something when pressed (MountSettingsPage.Key with pressed:), and
    // the mods that use the API: polled from the plugin's LateUpdate, before the mounts' update, while the API is on.
    // - A key counts when its main key went down this frame with the shortcut's own modifiers held; other keys held
    //   don't matter. Not KeyboardShortcut.IsDown(): that also demands that no other key is held, so it never fired
    //   while the player walks (W/A/S/D down), which is exactly when mounting matters (found in game 2026-09-30).
    //   But of two of our keys on one main key, the more specific acts alone (Shadowed, 2026-10-30): Ctrl + G of one
    //   row, G of another, Ctrl + G pressed: only the Ctrl + G row acts.
    // - A mod that is unloaded (its plugin destroyed, e.g. by a script reloader) has its tuning cleared and its keys
    //   dropped: mods don't need an OnDestroy of their own for it.
    internal static class KeyActions
    {
        private sealed class Entry
        {
            internal string Owner;
            internal ConfigEntry<KeyboardShortcut> Key;
            internal Action Pressed;
            // The binding changed (its SettingChanged: the options window's key capture, a cfg reload).
            // Was a KeyboardShortcut.Serialize() of every key every frame, compared as text: about half the mod's steady
            // garbage once the foals' names were gone (0.22 to 0.44 KB a frame, the garbage survey 2026-10-25 12:52).
            internal volatile bool Changed;
            internal bool WaitRelease;   // the binding just changed: the key that set it is still down, not a press of it
            internal bool OverPanel;     // the Mounts panel's own key: it still works while the panel is open (it closes it)
        }

        private static readonly List<Entry> entries = new List<Entry>();

        // Owners whose plugin was seen alive (true) or not yet (false).
        private static readonly Dictionary<string, bool> owners = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static float nextOwnerCheck;

        internal static void Add(string owner, ConfigEntry<KeyboardShortcut> key, Action pressed, bool overPanel = false)
        {
            Track(owner);
            var e = new Entry { Owner = owner, Key = key, Pressed = pressed, OverPanel = overPanel };
            key.SettingChanged += (s, a) => e.Changed = true;
            entries.Add(e);
        }

        // For the self-test: whether a mod's key still works while the Mounts panel is open (null: not a polled key).
        internal static bool? WorksOverPanel(string owner, string key)
        {
            Entry e = entries.Find(x => x.Owner == owner && x.Key.Definition.Key == key);
            return e?.OverPanel;
        }

        internal static void Track(string owner)
        {
            if (!string.IsNullOrEmpty(owner) && !owners.ContainsKey(owner))
            {
                owners[owner] = false;
            }
        }

        internal static void Poll()
        {
            if (Time.unscaledTime >= nextOwnerCheck)
            {
                nextOwnerCheck = Time.unscaledTime + 1f;
                CheckOwners();
            }
            if (WaitReason() != null)
            {
                return;
            }
            // The Mounts panel, its colours window or the options window open: the mods' keys wait, as the game's own do
            // (the user, 2026-10-22: H in the panel got him on and off). Only the panel's own key works, to close it,
            // while the panel is the one shown.
            bool windowOpen = MountsPanel.IsOpen || OptionsWindow.IsOpen;
            bool panelShown = MountsPanel.IsShownOnTop && !OptionsWindow.IsShown;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                // The binding changed since the last poll (the options window's key capture writes it on the key-down, or a
                // cfg reload): the press that set it is not a press of it. Nothing fires until
                // the new key has been let go (the user, 2026-10-21: binding the Mounts panel key opened the panel at once).
                if (e.Changed)
                {
                    e.Changed = false;
                    e.WaitRelease = e.Key.Value.MainKey != KeyCode.None;
                }
                if (e.WaitRelease)
                {
                    if (UnityInput.Current.GetKey(e.Key.Value.MainKey)) continue;
                    e.WaitRelease = false;
                }
                if (!Pressed(e.Key.Value) || Shadowed(e.Key.Value)) // a more specific key of ours pressed: that one acts
                {
                    continue;
                }
                if (windowOpen && !(e.OverPanel && panelShown))
                {
                    continue;
                }
                try
                {
                    e.Pressed();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"{e.Owner}: the action of its key {e.Key.Definition.Key} failed. {ex}");
                }
            }
        }

        // Why the mods' keys wait this frame, or null: what Poll asks first (the self-test asks it too).
        internal static string WaitReason()
        {
            if (KeyCapture.Active)
            {
                return "a key is being set"; // in the options window
            }
            if (MountsPanel.SearchFocused)
            {
                return "typing in the Mounts panel"; // its search box or a name: H is a letter, not the ride key
            }
            if (TextBoxHasCursor())
            {
                return "typing in a text box"; // another window's (another mod's, the game's bug report): the same
            }
            return null;
        }

        // A text box anywhere in the UI has the cursor. The keys are read from the keyboard (UnityInput), not through the
        // game's LazyInput, so a window that switches LazyInput off while its box is typed in (the game's bug report
        // window does) doesn't stop them: a letter typed in another mod's search box over a game window opened the Mounts
        // panel (the user, 2026-10-30). Both kinds of input field make themselves the selected object when they take
        // the cursor; its components are looked up once per selected object.
        private static GameObject selSeen;
        private static TMPro.TMP_InputField selTmp;
        private static UnityEngine.UI.InputField selUi;

        internal static bool TextBoxHasCursor()
        {
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null)
            {
                return false;
            }
            if (!ReferenceEquals(sel, selSeen))
            {
                selSeen = sel;
                selTmp = sel.GetComponent<TMPro.TMP_InputField>();
                selUi = sel.GetComponent<UnityEngine.UI.InputField>();
            }
            return (selTmp != null && selTmp.isFocused) || (selUi != null && selUi.isFocused);
        }

        internal static bool Pressed(KeyboardShortcut k)
        {
            if (k.MainKey == KeyCode.None || !UnityInput.Current.GetKeyDown(k.MainKey))
            {
                return false;
            }
            return ModifiersHeld(k);
        }

        // A shortcut's keys without garbage: its own array (BepInEx's KeyboardShortcut._allKeys, the main key first, its
        // modifiers after), read in place. KeyboardShortcut.Modifiers is a LINQ Skip(1) over it, a new iterator per read,
        // and the flight's key checks compared them with SequenceEqual three times a ridden frame. Null when the field
        // isn't there (another BepInEx): the callers fall back to the public members.
        private static readonly HarmonyLib.AccessTools.StructFieldRef<KeyboardShortcut, KeyCode[]> allKeys = AllKeysRef();

        private static HarmonyLib.AccessTools.StructFieldRef<KeyboardShortcut, KeyCode[]> AllKeysRef()
        {
            try { return HarmonyLib.AccessTools.StructFieldRefAccess<KeyboardShortcut, KeyCode[]>("_allKeys"); }
            catch { return null; }
        }

        private static KeyCode[] KeysOf(ref KeyboardShortcut k)
        {
            if (allKeys == null) return null;
            try { return allKeys(ref k); }
            catch { return null; }
        }

        // The modifiers of a shortcut all held now.
        internal static bool ModifiersHeld(KeyboardShortcut k)
        {
            KeyCode[] keys = KeysOf(ref k);
            if (keys == null)
            {
                foreach (KeyCode m in k.Modifiers) if (!UnityInput.Current.GetKey(m)) return false;
                return true;
            }
            for (int i = 1; i < keys.Length; i++) if (!UnityInput.Current.GetKey(keys[i])) return false;
            return true;
        }

        // The fallback when the array can't be read: the public Modifiers walked side by side.
        private static bool SameModifiers(KeyboardShortcut a, KeyboardShortcut b)
        {
            using (IEnumerator<KeyCode> x = a.Modifiers.GetEnumerator())
            using (IEnumerator<KeyCode> y = b.Modifiers.GetEnumerator())
            {
                while (true)
                {
                    bool mx = x.MoveNext(), my = y.MoveNext();
                    if (mx != my) return false;
                    if (!mx) return true;
                    if (x.Current != y.Current) return false;
                }
            }
        }

        // The most specific of our keys acts alone (docs/options-window.md §14; the user, 2026-10-30): our keys ignore what
        // is held with them (walking keys, the climb's Shift), so with G and Ctrl + G both ours, Ctrl + G did both. True
        // when another key row of a page has k's main key and k's modifiers and more, and those are all held now: that
        // key acts, k doesn't. A key used alone is unchanged (Shift held while pressing H still rides when nothing of ours
        // is on Shift + H). Asked only when k was pressed, so walking the rows costs nothing in a frame without a press.
        internal static bool Shadowed(KeyboardShortcut k)
        {
            if (k.MainKey == KeyCode.None) return false;
            foreach (MountSettingsPage p in MountSettingsPage.All)
            {
                foreach (MountSettingsPage.Row o in p.Rows)
                {
                    if (o.RowKind != MountSettingsPage.RowKind.Key || o.Entry == null) continue;
                    var ok = (KeyboardShortcut)o.Entry.BoxedValue;
                    if (MoreSpecific(ok, k) && ModifiersHeld(ok)) return true;
                }
            }
            return false;
        }

        // o has k's main key, every modifier of k's, and more.
        internal static bool MoreSpecific(KeyboardShortcut o, KeyboardShortcut k)
        {
            if (o.MainKey == KeyCode.None || o.MainKey != k.MainKey) return false;
            KeyCode[] ko = KeysOf(ref o), kk = KeysOf(ref k);
            if (ko == null || kk == null)
            {
                var om = new List<KeyCode>(o.Modifiers);
                int n = 0;
                foreach (KeyCode m in k.Modifiers) { if (!om.Contains(m)) return false; n++; }
                return om.Count > n;
            }
            int no = ko.Length, nk = kk.Length < 1 ? 1 : kk.Length; // the main key first, its modifiers after
            if (no <= nk) return false;
            for (int i = 1; i < nk; i++)
            {
                bool found = false;
                for (int j = 1; j < no; j++) if (ko[j] == kk[i]) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        // The self-test: the key rows that would act instead of k (Shadowed, its held-keys part left out).
        internal static List<string> TestMoreSpecificThan(KeyboardShortcut k)
        {
            var labels = new List<string>();
            foreach (MountSettingsPage p in MountSettingsPage.All)
                foreach (MountSettingsPage.Row o in p.Rows)
                    if (o.RowKind == MountSettingsPage.RowKind.Key && o.Entry != null && MoreSpecific((KeyboardShortcut)o.Entry.BoxedValue, k)) labels.Add(o.Label);
            return labels;
        }

        // The same combination (its main key and its modifiers, in order).
        internal static bool SameKey(KeyboardShortcut a, KeyboardShortcut b)
        {
            if (a.MainKey != b.MainKey) return false;
            KeyCode[] ka = KeysOf(ref a), kb = KeysOf(ref b);
            if (ka == null || kb == null) return SameModifiers(a, b);
            int na = ka.Length, nb = kb.Length;
            if (na != nb) return na <= 1 && nb <= 1; // both without modifiers (an empty shortcut's array may be empty or hold None)
            for (int i = 1; i < na; i++) if (ka[i] != kb[i]) return false;
            return true;
        }

        private static readonly List<string> gone = new List<string>();

        private static void CheckOwners()
        {
            gone.Clear();
            var seen = new List<string>();
            foreach (var kv in owners)
            {
                BaseUnityPlugin inst = Chainloader.PluginInfos.TryGetValue(kv.Key, out PluginInfo info) ? info.Instance : null;
                bool alive = inst != null && inst; // Unity's null: a destroyed plugin
                if (alive && !kv.Value)
                {
                    seen.Add(kv.Key);
                }
                else if (!alive && kv.Value)
                {
                    gone.Add(kv.Key);
                }
            }
            foreach (string id in seen)
            {
                owners[id] = true;
            }
            foreach (string id in gone)
            {
                owners.Remove(id);
                entries.RemoveAll(e => e.Owner == id);
                try
                {
                    Api.Mounts.ClearTuning(id);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"{id} was unloaded; clearing its tuning failed: {e.Message}");
                }
                Plugin.Log.LogInfo($"{id} was unloaded: its tuning is cleared and its keys dropped.");
            }
        }
    }
}
