using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;

namespace GK2DonkeyAPI.Api
{
    // A player mod's settings, defined once and shown where the player looks (Mounts.SettingsPage):
    // - with GK2 Mod Framework installed: in its Mods menu (the mod's own entry, its sections, every kind of row);
    // - without it: in the game's own Settings window, on pages of the mod's own after the game's settings, made of
    //   copies of the game's own rows (SettingsRows.cs). Keys and text there show read-only; they're set in the cfg.
    // Each row binds a BepInEx entry in the mod's own cfg (section and key as given), runs its apply action now and on
    // every change (from the menu, the Settings window or the cfg), and shows under its section. Call Publish once,
    // after the rows, from the mod's Awake.
    public sealed class MountSettingsPage
    {
        internal enum Kind { Toggle, Choice, Steps, Key, Text, Info }

        internal sealed class Row
        {
            internal Kind Kind;
            internal ConfigEntryBase Entry;
            internal string Section, Label, Description;
            internal string[] ChoiceLabels;
            internal Array ChoiceValues;
            internal int Min, Max, Step;
            internal Func<int, string> Format;
            internal Func<string> InfoValue;
            internal Func<bool> Visible;
        }

        internal readonly List<Row> Rows = new List<Row>();
        internal readonly string OwnerId, Title, Author, Description;
        internal readonly ConfigFile Config;
        internal readonly Func<string> Status;
        private bool published;

        internal MountSettingsPage(string ownerId, ConfigFile config, string title, string author, string description, Func<string> status)
        {
            OwnerId = ownerId;
            Config = config;
            Title = title;
            Author = author;
            Description = description;
            Status = status;
        }

        internal string Version
        {
            get
            {
                return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(OwnerId, out PluginInfo p) && p?.Metadata != null
                    ? p.Metadata.Version.ToString()
                    : "0.0.0";
            }
        }

        // On or off.
        public ConfigEntry<bool> Toggle(string section, string key, bool value, string label, string description, Action<bool> apply = null)
        {
            ConfigEntry<bool> e = Config.Bind(section, key, value, description);
            Add(new Row { Kind = Kind.Toggle, Entry = e, Section = section, Label = label, Description = description });
            Wire(e, apply);
            return e;
        }

        // One of an enum's values; labels (one per value, in order) are what players see, else the value names.
        public ConfigEntry<T> Choice<T>(string section, string key, T value, string label, string description, Action<T> apply = null, string[] labels = null) where T : struct, Enum
        {
            ConfigEntry<T> e = Config.Bind(section, key, value, description);
            Array values = Enum.GetValues(typeof(T));
            string[] shown = new string[values.Length];
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = labels != null && i < labels.Length ? labels[i] : values.GetValue(i).ToString();
            }
            Add(new Row { Kind = Kind.Choice, Entry = e, Section = section, Label = label, Description = description, ChoiceLabels = shown, ChoiceValues = values });
            Wire(e, apply);
            return e;
        }

        // A whole number from min to max in steps; format turns it into the shown text (e.g. "100%").
        public ConfigEntry<int> Steps(string section, string key, int value, int min, int max, int step, string label, string description, Action<int> apply = null, Func<int, string> format = null)
        {
            ConfigEntry<int> e = Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
            Add(new Row { Kind = Kind.Steps, Entry = e, Section = section, Label = label, Description = description, Min = min, Max = max, Step = Math.Max(1, step), Format = format });
            Wire(e, apply);
            return e;
        }

        // A key (with modifiers). Read-only in the game's Settings window.
        public ConfigEntry<KeyboardShortcut> Key(string section, string key, KeyboardShortcut value, string label, string description, Action<KeyboardShortcut> apply = null)
        {
            ConfigEntry<KeyboardShortcut> e = Config.Bind(section, key, value, description);
            Add(new Row { Kind = Kind.Key, Entry = e, Section = section, Label = label, Description = description });
            Wire(e, apply);
            return e;
        }

        // Free text. Read-only in the game's Settings window. visible: shown only while it says so (e.g. a custom
        // colour only while its choice is Custom).
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply = null, Func<bool> visible = null)
        {
            ConfigEntry<string> e = Config.Bind(section, key, value, description);
            Add(new Row { Kind = Kind.Text, Entry = e, Section = section, Label = label, Description = description, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A read-only line, e.g. what the mod is doing.
        public void Info(string section, string label, Func<string> value, string description = "")
        {
            Add(new Row { Kind = Kind.Info, Section = section, Label = label, Description = description, InfoValue = value });
        }

        // Shows the rows: in the framework's Mods menu when it's installed, else in the game's Settings window.
        public void Publish()
        {
            if (published)
            {
                return;
            }
            published = true;
            if (FrameworkLink.TryRegister(this))
            {
                Plugin.Log.LogInfo($"Settings: {Title} is in GK2 Mod Framework's Mods menu ({Rows.Count} rows).");
                return;
            }
            SettingsRows.Add(this);
        }

        private void Add(Row r)
        {
            if (published)
            {
                throw new InvalidOperationException("Add every row before Publish.");
            }
            Rows.Add(r);
        }

        private void Wire<T>(ConfigEntry<T> e, Action<T> apply)
        {
            if (apply == null)
            {
                return;
            }
            e.SettingChanged += (_, __) => Run(e, apply);
            Run(e, apply);
        }

        private void Run<T>(ConfigEntry<T> e, Action<T> apply)
        {
            try
            {
                apply(e.Value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Settings: {OwnerId} {e.Definition.Section}.{e.Definition.Key} could not be applied: {ex.Message}");
            }
        }
    }
}
