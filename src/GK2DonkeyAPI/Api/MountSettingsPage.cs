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
    // Each row binds a BepInEx entry in the mod's own cfg, runs its apply action now and on every change (from the menu,
    // the Settings window or the cfg), and shows under its section. Call Publish once, after the rows, from the mod's
    // Awake.
    // Sections keep the order they're first used in: the cfg section is numbered ("1. Keys", "2. Behaviour"), because
    // the framework's Mods menu sorts sections by name and has no other way to order them. The game's Settings window
    // shows the plain names.
    // Looks: Preview shows a picture of the mod's donkey in its current colours next to the settings (LookPreview.cs),
    // and LookButton opens a bar of the look rows over the game, so the player sees the real donkey change (LookBar.cs).
    // Both are extras: if anything about them fails, they don't show, and the settings work as before.
    public sealed class MountSettingsPage
    {
        internal enum Kind { Toggle, Choice, Steps, Key, Text, Info, Button, Preview }

        internal sealed class Row
        {
            internal Kind Kind;
            internal ConfigEntryBase Entry;
            internal string Section, Label, Description;
            internal string ShownSection; // the plain name, without the number
            internal string[] ChoiceLabels;
            internal Array ChoiceValues;
            internal int Min, Max, Step;
            internal Func<int, string> Format;
            internal Func<string> InfoValue;
            internal Func<bool> Visible;
            internal bool IsLook;           // shown in the look bar (LookButton)
            internal Action OnClick;         // Kind.Button
            internal bool WithCart;          // Kind.Preview: the donkey with its cart, or alone
        }

        internal readonly List<Row> Rows = new List<Row>();
        private readonly List<string> sections = new List<string>();
        internal MountCompanion PreviewOf;
        internal Action RefreshMenu; // the Mods menu re-reads the rows' visibility (set by FrameworkLink)

        // "Keys" -> "1. Keys", numbered in the order sections are first used.
        private string Sec(string name)
        {
            int at = sections.IndexOf(name);
            if (at < 0)
            {
                sections.Add(name);
                at = sections.Count - 1;
            }
            return (at + 1) + ". " + name;
        }

        internal readonly string OwnerId, Title, Author, Description;
        internal readonly ConfigFile Config;
        internal readonly Func<string> StatusValue;
        private bool published;

        internal MountSettingsPage(string ownerId, ConfigFile config, string title, string author, string description, Func<string> status)
        {
            OwnerId = ownerId;
            Config = config;
            Title = title;
            Author = author;
            Description = description;
            StatusValue = status ?? (() => Mounts.StatusText);
            KeyActions.Track(ownerId);
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
            string cfgSection = Sec(section);
            ConfigEntry<bool> e = Config.Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Toggle, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description });
            Wire(e, apply);
            return e;
        }

        // One of an enum's values; labels (one per value, in order) are what players see, else the value names.
        // look: also shown in the look bar (LookButton).
        public ConfigEntry<T> Choice<T>(string section, string key, T value, string label, string description, Action<T> apply = null, string[] labels = null, bool look = false, Func<bool> visible = null) where T : struct, Enum
        {
            string cfgSection = Sec(section);
            ConfigEntry<T> e = Config.Bind(cfgSection, key, value, description);
            Array values = Enum.GetValues(typeof(T));
            string[] shown = new string[values.Length];
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = labels != null && i < labels.Length ? labels[i] : values.GetValue(i).ToString();
            }
            Add(new Row { Kind = Kind.Choice, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, ChoiceLabels = shown, ChoiceValues = values, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A whole number from min to max in steps; format turns it into the shown text (e.g. "100%").
        public ConfigEntry<int> Steps(string section, string key, int value, int min, int max, int step, string label, string description, Action<int> apply = null, Func<int, string> format = null, bool look = false, Func<bool> visible = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<int> e = Config.Bind(cfgSection, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
            Add(new Row { Kind = Kind.Steps, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Min = min, Max = max, Step = Math.Max(1, step), Format = format, IsLook = look, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A key (with modifiers). Read-only in the game's Settings window.
        // pressed: called when the player presses the key (its modifiers held; other keys held, like walking, don't
        // matter), while the API is on. The API polls it: the mod needs no Update of its own.
        public ConfigEntry<KeyboardShortcut> Key(string section, string key, KeyboardShortcut value, string label, string description, Action<KeyboardShortcut> apply = null, Action pressed = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<KeyboardShortcut> e = Config.Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Key, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description });
            Wire(e, apply);
            if (pressed != null)
            {
                KeyActions.Add(OwnerId, e, pressed);
            }
            return e;
        }

        // Free text. Read-only in the game's Settings window. visible: shown only while it says so.
        public ConfigEntry<string> Text(string section, string key, string value, string label, string description, Action<string> apply = null, Func<bool> visible = null)
        {
            string cfgSection = Sec(section);
            ConfigEntry<string> e = Config.Bind(cfgSection, key, value, description);
            Add(new Row { Kind = Kind.Text, Entry = e, Section = cfgSection, ShownSection = section, Label = label, Description = description, Visible = visible });
            Wire(e, apply);
            return e;
        }

        // A colour a player picks by name (PaletteColor: Chestnut, Sand, Moss, Sky, ...) and makes lighter or darker
        // (the picture, Preview, shows the result; a swatch row did before it existed). Bound as <key> and <key>Lightness; apply gets the colour as hex RRGGBB. visible:
        // shown only while it says so (e.g. only while its part's choice is Custom). In the look bar too.
        public void Color(string section, string key, string label, string description, PaletteColor value, Action<string> apply, Func<bool> visible = null)
        {
            ConfigEntry<PaletteColor> colour = null;
            ConfigEntry<int> lightness = null;
            string Now() => colour != null && lightness != null ? MountPalette.Hex(colour.Value, lightness.Value) : null;
            void Combine()
            {
                string hex = Now();
                if (hex != null)
                {
                    apply?.Invoke(hex);
                }
            }
            colour = Choice(section, key, value, label, description, _ => Combine(), null, look: true, visible: visible);
            lightness = Steps(section, key + "Lightness", 100, 40, 160, 10, label + ": lightness", "100% is the colour itself; lower is darker, higher is lighter.", _ => Combine(), n => n + "%", look: true, visible: visible);
            Combine();
        }

        // The read-only "Status" row: the page's status (Mounts.StatusText unless the page was given its own).
        public void Status(string section, string label = "Status", string description = "What the mod is doing right now.")
        {
            Info(section, label, () => StatusValue(), description);
        }

        // A read-only line, e.g. what the mod is doing.
        public void Info(string section, string label, Func<string> value, string description = "")
        {
            Add(new Row { Kind = Kind.Info, Section = Sec(section), ShownSection = section, Label = label, Description = description, InfoValue = value });
        }

        // A picture of the companion's donkey and cart in their current colours, walking on the spot: a row of the page,
        // here in this section, in the order called (LookPreview); call it once per section that should show it (e.g.
        // at the top of each section with look rows, so it's in view from each). Also above the Mods window, where it
        // stays in view whatever row is scrolled to, when there's room there (1440p and up, not 1080p).
        // label: the row's name; cart: the row's picture is the donkey with its cart, or the donkey alone (above the
        // window it's always with the cart). Above the game's Settings window without the Mods menu; in the look bar
        // too. It needs no donkey in the game: drawn from the game's own files in the companion's colours, so it shows
        // in the main menu and indoors.
        public void Preview(string section, MountCompanion companion, string label = "Your donkey", bool cart = true)
        {
            PreviewOf = companion;
            Add(new Row
            {
                Kind = Kind.Preview, Section = Sec(section), ShownSection = section, Label = label, Description = PreviewRoom,
                Visible = () => LookPreview.RowsShown, WithCart = cart,
            });
        }

        // The rows are found on screen by their labels (LookPreview), and the Mods menu sizes a row by its description
        // (only line breaks count as empty: the minimum height), so the picture's room is eight lines of one fully
        // transparent dot each (a zero-width space didn't count).
        internal bool HasPreviewRows => Rows.Exists(r => r.Kind == Kind.Preview);

        internal Row PreviewRowLabelled(string text) => Rows.Find(r => r.Kind == Kind.Preview && r.Label == text);
        internal const string PreviewRoom = "<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>\n<alpha=#00>.</alpha>";

        // A button that closes the menu and opens the look bar over the game: the look rows (Choice and Steps with look,
        // and Color) at the bottom of the screen, the real donkey in view, with Done (keep) and Cancel (put back).
        // Only in a game; in the main menu it says so.
        public void LookButton(string section, string label, string description)
        {
            Add(new Row { Kind = Kind.Button, Section = Sec(section), ShownSection = section, Label = label, Description = description, OnClick = () => LookBar.Open(this) });
        }

        // Shows the rows: in the framework's Mods menu when it's installed, else in the game's Settings window.
        public void Publish()
        {
            if (published)
            {
                return;
            }
            published = true;
            LookPreview.Watch(this);
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
