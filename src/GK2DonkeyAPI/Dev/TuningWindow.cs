using System;
using BepInEx;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // In-game tuning panel (Unity IMGUI), shown in the top-right corner while mounted when Debug.TuningWindow is on.
    // Tabs: General (behaviour), Riding (everything that places and animates rider and donkey, for the style
    // currently mounted), Lantern, Sounds, Colors, Instances, Debug, Sources. Every control edits the live config value and the mount
    // reacts immediately; BepInEx saves the cfg on every change. A row whose value differs from its mod default is
    // yellow, and right-click puts it back to that default (see Defaults.cs for yours against the mod defaults).
    internal static partial class TuningWindow
    {
        private const int WindowId = 0x6d6e7431; // 'mnt1'
        private static Rect rect = new Rect(0f, 0f, 470f, 10f);
        private static GUIStyle label, header, step, small, changed, overridden, instanceSet, mountSet, wrapToggle, wrapLabel, good, bad, warnWrap, barBack, barFill, oneLine, oneLineSmall;

        // Cyan while another mod overrides the value this session (the row edits the player's cfg value underneath,
        // which shows again once the override ends); else yellow when it differs from its mod default.
        private static GUIStyle LabelFor(ConfigEntryBase e)
        {
            if (EditsInstance(e) && editing.Overrides.ContainsKey(e.Definition.Key))
            {
                return editing.OverrideOwners.TryGetValue(e.Definition.Key, out string o) && IsMountOwner(o) ? mountSet : instanceSet;
            }
            if (RiddenHas(e, out _, out string by))
            {
                return IsMountOwner(by) ? mountSet : overridden; // the ridden donkey's own value wins over the row
            }
            if (Tuned.OverriddenBy(e) != null)
            {
                return overridden;
            }
            return e != null && Defaults.IsTracked(e) && Defaults.Differs(e) ? changed : label;
        }
        private static bool stylesReady;
        private static int tab;
        private static string donkeyEvent = "";
        private static string cargoItemId = "wood";
        private static string spawnItemId = "wood";
        private static readonly string[] TabNames = { "General", "Riding", "Cargo", "Lantern", "Sounds", "Colors", "Instances", "Debug", "Tests", "Performance", "Sources", "Mounts" };
        private const int TabCargo = 2, TabInstances = 6, TabDebug = 7, TabTests = 8, TabPerformance = 9, TabMounts = 11;
        private const int TabsPerRow = 6; // two rows: eleven names in one row of 470 would be cut short

        // Whether the panel draws this frame: in a game, switched on, not hidden by its key, and not while the cargo sheet
        // photographs the cart. Plugin.LateUpdate switches Unity's layout pass off when it doesn't (useGUILayout).
        internal static bool Showing()
        {
            bool inGame = Mount.IsMounted || (MainGame.Instance != null && MainGame.PlayerData != null);
            return inGame && DevTools.Unlocked && Plugin.TuningWindowEnabled.Value && !Plugin.TuningWindowHidden && !CargoSheet.Running;
        }

        // The mouse is over the panel, or a panel control is being dragged: the game's player input is skipped that
        // frame (PlayerInputHandler_UpdateInput_Patch), so a click on a slider no longer swings the sword or turns the
        // keeper (the user, 2026-10-14). The panel is immediate-mode GUI and consumes its own events, but the game polls
        // the mouse directly. Mouse y counts from the bottom, GUI y from the top; the panel draws under GUI.matrix's scale.
        internal static bool PointerOverPanel()
        {
            if (!Showing())
            {
                return false;
            }
            try
            {
                float scale = Mathf.Clamp(Plugin.TuningWindowScale.Value, 0.75f, 3f);
                Vector3 m = UnityInput.Current.mousePosition;
                var gui = new Vector2(m.x / scale, (Screen.height - m.y) / scale);
                if (rect.Contains(gui))
                {
                    return true;
                }
            }
            catch
            {
            }
            try
            {
                return GUIUtility.hotControl != 0; // a slider still held while the mouse left the panel
            }
            catch
            {
                return false;
            }
        }

        public static void Draw()
        {
            CargoLayout.SaveIfDue(now: !Mount.IsMounted);
            MountsSaveIfDue(); // the Mounts tab's values into the skin's file a moment after the last change, whatever tab shows
            if (!Showing())
            {
                return;
            }
            float scale = Mathf.Clamp(Plugin.TuningWindowScale.Value, 0.75f, 3f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();
            float vw = Screen.width / scale;
            // The tab contents scroll when they do not fit the screen (the Riding tab is long); the toolbar and the
            // Save/Reset row stay in view. The chrome (title bar, toolbar, footer) is measured from the last frame:
            // window height minus the scroll view's height, so wrapped footer text at any font or width is counted.
            maxContentHeight = Mathf.Max(150f, Screen.height / scale - 32f - chromeHeight);
            rect.x = vw - W - 16f;
            rect.y = 16f;
            rect.width = W;
            // A layout window grows to its content but never shrinks unless the height is reset. Reset only in the
            // Layout event, and never keep a zero-height result: the window hands back height 0 for events it has
            // not laid out yet, and keeping that drew the next repaint as an empty window for a frame (found
            // 2026-09-30 from the [panel] log lines below: height 0 on repaint on every tab, most often while
            // dragging a slider, which sends extra events every frame).
            EventType drawType = Event.current.type;
            Rect want = rect;
            if (drawType == EventType.Layout)
            {
                want.height = 0f;
            }
            if (editing != null && !Mount.IsRegistered(editing))
            {
                editing = null; // removed, or hitched away
            }
            Rect got = GUILayout.Window(WindowId, want, DrawContents,
                Plugin.TuningWindowKey.Value == KeyCode.None ? "GK2 Donkey API tuning" : $"GK2 Donkey API tuning ({Plugin.TuningWindowKey.Value} hides)",
                Wd(W));
            if (got.height >= 1f)
            {
                rect = got;
            }
            if (drawType == EventType.Repaint && got.height < 60f && Time.unscaledTime >= nextFlickerLog)
            {
                nextFlickerLog = Time.unscaledTime + 1f;
                Plugin.Log.LogInfo($"[panel] empty window repainted: frame {Time.frameCount}, height {got.height:F0}, layout ran this frame {layoutRunFrame == Time.frameCount}, repaint ran {repaintRunFrame == Time.frameCount}, tab {tab}, scroll height {lastScrollHeight:F0}, chrome {chromeHeight:F0}, max content {maxContentHeight:F0}, content {contentHeight[tab]:F0}, last abort: {lastAbort}");
            }
            if (lastScrollHeight > 0f && rect.height > lastScrollHeight)
            {
                chromeHeight = Mathf.Clamp(rect.height - lastScrollHeight, 60f, 600f);
            }
            using (Perf.Measure(Perf.S.Markers))
            {
                DrawWorldMarkers(scale);
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private static Vector2 scroll;
        private static float maxContentHeight = 800f;
        private static float chromeHeight = 130f;   // measured; 130 is the first-frame guess
        private static float lastScrollHeight;
        private static readonly float[] contentHeight = new float[TabNames.Length]; // measured per tab, last repaint (TabNames is declared above: static fields initialise in order)

        // Panel width in panel units (before the overall scale) and the widths derived from it, so rows shrink and
        // grow with the panel instead of overflowing it.
        private static float W => Mathf.Clamp(Plugin.TuningWindowWidth.Value, 300f, 900f);
        private static float LabelW => W * 0.62f;
        private static float SliderW => W * 0.30f;

        // The width inside the scroll view: the panel minus the window's padding and the vertical scrollbar. The tab
        // contents are held to it. An IMGUI scroll view gives its content unlimited width, and a wrapping label wraps
        // at the width of its column, which is set by the widest control in it; without this cap one wide row
        // widens the column and every sentence then runs past the panel's edge.
        private static float ContentW
        {
            get
            {
                GUISkin k = GUI.skin;
                float bar = k.verticalScrollbar.fixedWidth > 0f ? k.verticalScrollbar.fixedWidth : 15f;
                return Mathf.Max(200f, W - k.window.padding.horizontal - bar - k.verticalScrollbar.margin.horizontal - k.scrollView.padding.horizontal - 4f);
            }
        }

        private static float appliedFontScale = -1f;

        private static void EnsureStyles()
        {
            float fontScale = Mathf.Clamp(Plugin.TuningWindowFontScale.Value, 0.5f, 2f);
            if (stylesReady && Mathf.Approximately(fontScale, appliedFontScale))
            {
                return;
            }
            appliedFontScale = fontScale;
            tipStyle = null; // rebuilt at the new size on the next hover
            // Rich text on the text styles: a word can carry its own colour (<color=#8ee68e>tuned</color>), which is how
            // the Cargo tab marks states; section headers are gold on every tab so a skim finds them, steps are blue.
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * fontScale), richText = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(11f * fontScale), wordWrap = true, richText = true };
            small.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            header = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * fontScale), fontStyle = FontStyle.Bold, richText = true };
            header.normal.textColor = new Color(1f, 0.84f, 0.5f);
            step = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14f * fontScale), fontStyle = FontStyle.Bold, richText = true };
            step.normal.textColor = new Color(0.55f, 0.82f, 1f);
            wrapToggle = new GUIStyle(GUI.skin.toggle) { wordWrap = true, fontSize = Mathf.RoundToInt(13f * fontScale) };
            changed = new GUIStyle(label);
            changed.normal.textColor = new Color(1f, 0.85f, 0.3f);
            overridden = new GUIStyle(label);
            overridden.normal.textColor = new Color(0.35f, 0.85f, 1f);
            instanceSet = new GUIStyle(label);
            instanceSet.normal.textColor = new Color(1f, 0.5f, 0.95f);
            mountSet = new GUIStyle(label);
            mountSet.normal.textColor = new Color(1f, 0.62f, 0.25f); // orange: the mount's value (Mounts tab)
            wrapLabel = new GUIStyle(label) { wordWrap = true };
            // A line whose text changes length: one line, clipped at the right, never a second line (a status that
            // wrapped to two lines moved everything below it up and down as it changed, 2026-10-15).
            oneLine = new GUIStyle(label) { wordWrap = false, clipping = TextClipping.Clip };
            oneLineSmall = new GUIStyle(small) { wordWrap = false, clipping = TextClipping.Clip };
            good = new GUIStyle(wrapLabel);
            good.normal.textColor = new Color(0.55f, 0.9f, 0.55f);
            bad = new GUIStyle(wrapLabel);
            bad.normal.textColor = new Color(1f, 0.45f, 0.45f);
            warnWrap = new GUIStyle(wrapLabel);
            warnWrap.normal.textColor = changed.normal.textColor;
            barBack = new GUIStyle();
            barBack.normal.background = Solid(new Color(1f, 1f, 1f, 0.12f));
            barFill = new GUIStyle();
            barFill.normal.background = Solid(new Color(0.55f, 0.9f, 0.55f, 0.8f));
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.35f));
            tex.Apply();
            rule = new GUIStyle();
            rule.normal.background = tex;
            rule.fixedHeight = 1f;
            rule.margin = new RectOffset(0, 0, 10, 4);
            stylesReady = true;
        }

        private static GUIStyle rule;

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // A plain bar, filled to frac of its width; drawn on the repaint only (it takes no input). Its rect comes from
        // the layout, not from Wd (a width that changes every frame would churn that cache).
        private static void ProgressBar(float frac)
        {
            Rect r = GUILayoutUtility.GetRect(10f, 12f, ExpandW);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            GUI.Box(r, GUIContent.none, barBack);
            Rect f = r;
            f.width = Mathf.Round(r.width * Mathf.Clamp01(frac));
            if (f.width >= 1f)
            {
                GUI.Box(f, GUIContent.none, barFill);
            }
        }

        // Layout options made once per width: GUILayout.Width makes an option object and a params array on every call,
        // for every row on every event (the panel's own garbage, 2026-10-01). Options never change, so one per width.
        private static readonly Dictionary<float, GUILayoutOption[]> widths = new Dictionary<float, GUILayoutOption[]>();
        private static readonly GUILayoutOption[] ExpandW = { GUILayout.ExpandWidth(true) };

        private static GUILayoutOption[] Wd(float w)
        {
            if (!widths.TryGetValue(w, out GUILayoutOption[] o))
            {
                if (widths.Count > 256)
                {
                    widths.Clear(); // a panel width or text size being dragged makes new ones
                }
                o = new[] { GUILayout.Width(w) };
                widths[w] = o;
            }
            return o;
        }

        private static readonly string[] FixedFormats = { "F0", "F1", "F2", "F3", "F4" };

        private static string Fixed(int decimals) => decimals >= 0 && decimals < FixedFormats.Length ? FixedFormats[decimals] : "F" + decimals;

        // A section title with a rule line above it, so the rows below visibly belong to it.
        // ------------------------------------------------------------------ tooltips
        // A control's GUIContent may carry a tooltip; IMGUI sets GUI.tooltip while the mouse is over it (Repaint). The
        // box is drawn at the very end of the window function with GUI.Label at a rect: no layout control, so the slider
        // order is untouched. Clamped inside the window (a window clips its contents).
        private static GUIStyle tipStyle;

        internal static GUIContent Tip(string text, string tip) => string.IsNullOrEmpty(tip) ? new GUIContent(text) : new GUIContent(text, tip);

        // A label of exactly one line for text whose length changes: clipped at the right, the whole text in the tooltip,
        // so the rows below never move. small: the smaller font.
        private static void OneLine(string text, bool smallFont = false)
        {
            GUIStyle st = smallFont ? oneLineSmall : oneLine;
            string plain = text ?? "";
            GUILayout.Label(new GUIContent(plain, plain), st, GUILayout.Height(st.lineHeight + 4f), GUILayout.ExpandWidth(true));
        }

        // A settings row's tooltip: the one given, else the setting's own description (the text written for the cfg file),
        // cut to whole sentences within ~260 characters so the box stays short. Every settings row on every tab gets one.
        private static readonly Dictionary<ConfigEntryBase, string> tipCache = new Dictionary<ConfigEntryBase, string>();

        private static string TipFor(ConfigEntryBase entry, string tip)
        {
            if (!string.IsNullOrEmpty(tip) || entry == null) return tip;
            if (tipCache.TryGetValue(entry, out string cached)) return cached;
            string d = entry.Description?.Description ?? "";
            if (d.Length > 260)
            {
                int cut = d.LastIndexOf(". ", 258, StringComparison.Ordinal);
                d = cut > 60 ? d.Substring(0, cut + 1) : d.Substring(0, 257) + "...";
            }
            tipCache[entry] = d;
            return d;
        }

        // A button with a tooltip; active false draws it greyed (the control order never changes) and returns false.
        private static bool TipButton(string text, string tip, float width, bool active = true)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && active;
            bool pressed = GUILayout.Button(Tip(text, tip), Wd(width));
            GUI.enabled = was;
            return pressed && active;
        }

        // GUI.tooltip is set by a hovered control during Repaint but never cleared by the player when the mouse leaves
        // (the box stayed on screen with the last text, 2026-10-12): the window clears it at the start of each repaint
        // (ClearTooltip), so at the end it holds only what the mouse is over now. Shown after the mouse has rested on the
        // same control for HoverDelay seconds, and hidden again as soon as it moves, like any tooltip.
        private const float HoverDelay = 0.45f;
        private static string hoverTip = "";
        private static float hoverSince;
        private static Vector2 hoverAt;

        private static void ClearTooltip()
        {
            if (Event.current.type == EventType.Repaint)
            {
                GUI.tooltip = "";
            }
        }

        private static void DrawTooltip()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            string now = GUI.tooltip ?? "";
            Vector2 m = Event.current.mousePosition;
            if (now != hoverTip || (m - hoverAt).sqrMagnitude > 16f)
            {
                hoverTip = now;
                hoverSince = Time.unscaledTime;
                hoverAt = m;
                return;
            }
            if (now.Length == 0 || Time.unscaledTime - hoverSince < HoverDelay)
            {
                return;
            }
            if (tipStyle == null)
            {
                tipStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.RoundToInt(11f * appliedFontScale), alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true,
                    padding = new RectOffset(8, 8, 6, 6), normal = { textColor = new Color(0.92f, 0.92f, 0.92f) },
                };
                var bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.97f));
                bg.Apply();
                tipStyle.normal.background = bg;
            }
            var content = new GUIContent(now);
            float w = Mathf.Min(340f, rect.width - 24f);
            float h = tipStyle.CalcHeight(content, w);
            float x = Mathf.Clamp(m.x + 14f, 4f, Mathf.Max(4f, rect.width - w - 4f));
            float y = m.y + 22f;
            if (y + h > rect.height - 4f) y = Mathf.Max(4f, m.y - h - 8f); // above the mouse near the bottom
            GUI.Label(new Rect(x, y, w, h), content, tipStyle);
        }

        private static void Header(string text)
        {
            GUILayout.Box(GUIContent.none, rule, ExpandW);
            GUILayout.Label(text, header);
        }

        // The settings that differ from their mod defaults, taken once per layout pass. IMGUI lays out every event
        // first and then runs it again; the button and the list below must have the same controls in both passes,
        // so a value changed by this event (a slider moved off its default) shows up from the next event on.
        private static List<(string Group, ConfigEntryBase Entry)> diffSnapshot = new List<(string, ConfigEntryBase)>();

        private static int overrideSnapshot; // taken with diffSnapshot, for the same reason

        // Taken in a Layout event, at most four times a second: comparing every setting with its default on every
        // layout pass was a good part of the panel's own cost. A change shows within a quarter second. The footer's
        // texts are made with it, not on every event.
        private static float nextDiffSnapshot;
        private static string footerDiffText = "", footerOverrideText = "", footerDonkeyText = "";

        private static void TakeDiffSnapshot()
        {
            if (Time.unscaledTime < nextDiffSnapshot)
            {
                return;
            }
            nextDiffSnapshot = Time.unscaledTime + 0.25f;
            Defaults.Differing(diffSnapshot);
            overrideSnapshot = Tuned.OverrideCount();
            int differing = diffSnapshot.Count;
            footerDiffText = differing == 0
                ? "Your changes are saved as you make them. All settings match the mod defaults."
                : $"Your changes are saved as you make them. {differing} setting{(differing == 1 ? "" : "s")} differ from the mod defaults ({DiffByGroup()}; yellow, listed on the Sources tab). Saved to your cfg; the Sources tab says where every value comes from.";
            footerOverrideText = overrideSnapshot > 0
                ? $"{overrideSnapshot} value{(overrideSnapshot == 1 ? " is" : "s are")} set by another mod for this session ({OwnersOfSessionValues()}; cyan): those rows are locked and show the value in effect. Your saved value is unchanged and comes back when the mod clears it or the game restarts."
                : "";
            Mount.Instance r = Mount.Ridden;
            int own = r != null ? r.Overrides.Count : 0;
            footerDonkeyText = r == null ? ""
                : own > 0 || r.OwnLook
                    ? $"The donkey you ride ({r.FullId}) has {own} value{(own == 1 ? "" : "s")} of its own{(r.OwnLook ? " and its own look (your colours don't apply to it)" : "")}, set by {OwnersOf(r)}: they win over every row here (orange: the mount's, cyan: a mod's). Sources tab."
                    : "";
        }

        // Who set the session values, e.g. "dreas.gk2.saddleup: 7".
        private static string OwnersOfSessionValues()
        {
            var by = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (ITuned t in Tuned.ByEntry.Values)
            {
                if (t.HasOverride)
                {
                    string o = t.OverrideOwner ?? "?";
                    by[o] = by.TryGetValue(o, out int n) ? n + 1 : 1;
                }
            }
            var parts = new List<string>();
            foreach (var kv in by)
            {
                parts.Add(kv.Key + ": " + kv.Value);
            }
            return string.Join(", ", parts);
        }

        private static string OwnersOf(Mount.Instance i)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string o in i.OverrideOwners.Values) set.Add(FriendlyOwner(o));
            if (set.Count == 0)
            {
                set.Add(i.OwnerId);
            }
            return string.Join(", ", set);
        }

        private static string CfgPath => Plugin.Instance != null ? Plugin.Instance.Config.ConfigFilePath : "dreas.gk2.donkeyapi.cfg";

        private static string DiffByGroup()
        {
            int g = 0, r = 0, c = 0, k = 0;
            foreach (var d in diffSnapshot)
            {
                if (d.Group == "Ride") r++;
                else if (d.Group == "Cart") c++;
                else if (d.Group.StartsWith(Colors.Section, StringComparison.Ordinal)) k++;
                else g++;
            }
            var parts = new List<string>();
            if (r > 0) parts.Add("Ride " + r);
            if (c > 0) parts.Add("Cart " + c);
            if (k > 0) parts.Add("Colors " + k);
            if (g > 0) parts.Add("General " + g);
            return string.Join(", ", parts);
        }

        // Flicker investigation (2026-09-30, the user's video 13-29-01): while a colour slider was dragged, the
        // window was drawn for a frame at its minimum size with no contents, and nothing was in either log. This
        // wrapper records which events reached the window function each frame and logs anything that aborts it
        // (ExitGUIException included, which IMGUI swallows silently); Draw logs a repaint of an empty window.
        private static int layoutRunFrame = -1, repaintRunFrame = -1;
        private static string lastAbort = "";
        private static float nextFlickerLog;

        private static void DrawContents(int id)
        {
            EventType type = Event.current.type;
            if (type == EventType.Layout) layoutRunFrame = Time.frameCount;
            if (type == EventType.Repaint) repaintRunFrame = Time.frameCount;
            try
            {
                // The edited instance bound (IMGUI calls this at the end of OnGUI, so the binding is made here): rows
                // that read a setting directly show its value, like Val does.
                using (Mount.Bind(editing))
                using (Perf.MeasureAlloc())
                using (Perf.Measure(Perf.S.Window))
                {
                    DrawContentsInner(id);
                }
            }
            catch (Exception e)
            {
                lastAbort = $"{e.GetType().Name} in {type} (tab {tab}, frame {Time.frameCount}): {e.Message}";
                if (Time.unscaledTime >= nextFlickerLog)
                {
                    nextFlickerLog = Time.unscaledTime + 1f;
                    Plugin.Log.LogInfo("[panel] the window function was aborted: " + lastAbort + "\n" + e.StackTrace);
                }
                throw;
            }
        }

        private static void DrawContentsInner(int id)
        {
            rawSeq = 0;
            ClearTooltip();
            if (tab != TabCargo)
            {
                nudgePending = 0f; // keys pressed elsewhere never land on a cargo slider later
            }
            using (Perf.Measure(Perf.S.WinTop))
            {
                if (Event.current.type == EventType.Layout)
                {
                    TakeDiffSnapshot();
                }
                int newTab = GUILayout.SelectionGrid(tab, TabNames, TabsPerRow);
                if (newTab != tab)
                {
                    tab = newTab;
                    scroll = Vector2.zero;
                }
                DrawScope();
                GUILayout.Space(6f);
            }
            Perf.PanelTab = TabNames[Mathf.Clamp(tab, 0, TabNames.Length - 1)];
            // An IMGUI scroll view needs an explicit height, or it collapses; the content's height is measured on
            // each repaint (last frame's value is used) and capped to what fits on the screen.
            // The vertical scrollbar is always shown so its width never changes how text wraps (an appearing and
            // disappearing bar changes the content height, which changes whether the bar is needed: a flicker loop).
            // Small height jitter is ignored for the same reason.
            float h = contentHeight[tab] > 0f ? Mathf.Min(contentHeight[tab], maxContentHeight) : maxContentHeight;
            lastScrollHeight = h;
            // No horizontal scrollbar: the content is held to the panel's width (ContentW).
            scroll = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(h));
            GUILayout.BeginVertical(Wd(ContentW));
            using (Perf.Measure(Perf.S.WinTab))
            {
                switch (tab)
                {
                    case 0: DrawGeneral(); break;
                    case 1: DrawRiding(); break;
                    case TabCargo: DrawCargoTab(); break;
                    case 3: DrawLantern(); break;
                    case 4: DrawSounds(); break;
                    case 5: DrawColors(); break;
                    case TabInstances: DrawInstances(); break;
                    case TabDebug: DrawDebug(); break;
                    case TabTests: DrawTests(); break;
                    case TabPerformance: DrawPerformance(); break;
                    case TabMounts: DrawMounts(); break;
                    default: DrawDefaults(); break;
                }
            }
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                float measured = GUILayoutUtility.GetLastRect().height + 6f;
                if (Mathf.Abs(measured - contentHeight[tab]) > 3f)
                {
                    contentHeight[tab] = measured;
                }
            }
            GUILayout.EndScrollView();

            using var footer = Perf.Measure(Perf.S.WinFooter);
            GUILayout.Space(8f);
            int differing = diffSnapshot.Count;
            GUILayout.Label(footerDiffText, small);
            if (overrideSnapshot > 0)
            {
                GUILayout.Label(footerOverrideText, small);
            }
            if (footerDonkeyText.Length > 0)
            {
                GUILayout.Label(footerDonkeyText, small);
            }
            DrawDefaultsButtons(differing);
            GUILayout.Label("Right-click a row to put it back to its mod default.", small);
            DrawTooltip(); // last: over everything, no layout control
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ------------------------------------------------------------------ tabs

        private static void DrawGeneral()
        {
            Header("Controls");
            GUILayout.Label($"GK2 Donkey API has no keys of its own: the mod that uses it mounts and dismounts (this ride: {Mounts.Owner ?? "-"}). Keys, and the behaviour switches below, are that mod's settings; rows it sets are cyan and locked.", small);

            Header("Behaviour");
            Toggle("Stay mounted when going through doors (scene changes)", Plugin.StayMountedThroughDoors, null);
            Toggle("Dismount when entering a building, church or dungeon (no mounting indoors)", Plugin.DismountIndoors, null);
            Toggle("Zombies ride on the cart", Plugin.CargoZombies, null);
            IntSlider("Cart capacity (items)", Plugin.CartSlots, 1, 6, () => Mount.ApplyCartSlots("the panel"));
            GUILayout.Label("How many items the cart carries (1-6). Three and more ride in layers of up to two. Lowering it never drops anything: a fuller cart stays full until unloaded. Saddle Up's own 'Cart capacity' (2 / 4 / 6) overrides this row while it is installed; editing one donkey on the Instances tab gives that cart its own number.", small);
            Toggle("A storage chest rides on the cart (alone; F on it while driving, E with nothing in front sets it down)", Plugin.CargoChest, null);
            if (CartChest.Locked)
            {
                GUILayout.Label("Chest on the cart: OFF for this session: " + CartChest.LockReason + ". Riding chests were set down at their carts.", small);
            }
            Toggle("Show a thought bubble when mounting is refused", Plugin.ShowMessages, null);
            GUILayout.Label("While mounted the keeper faces only left or right, like the donkey.", small);
            GUILayout.Label("You always dismount for work, ladders, building, fishing, fighting, sleep and co-op; chests and inventory keep you mounted.", small);

            Header("Corpses at the crossroads (docs/corpse-delivery.md)");
            Toggle("The game's delivery donkey leaves its bodies at the crossroads by the nun, for you to haul (Saddle Up: \"Donkey leaves corpses at the crossroads\")", Corpses.AtCrossroads, null);
            GUILayout.Label(Corpses.Applied ? "Hooked right now: the pallet step, the graph's two fallback drops, the delivery walk and the New Body notification."
                : Corpses.ApplyError != null ? "Not hooked: patching failed (" + Corpses.ApplyError + "); switch off and on to retry." : "Not hooked: the game's donkey code is untouched while this is off.", small);
            Toggle("It walks out from behind its tree to the stand point, unloads there and walks back (off: it walks to the morgue as in the game; the bodies still appear at the drop point)", Corpses.UnloadAtCrossroads, null);
            TextRow("Stand point: GD point it walks to (donkey_01_01 = the road beside the nun)", Corpses.StandPoint);
            TextRow("Drop point: GD point the bodies are left at (nun_body_drop_point = the game's spot by her)", Corpses.DropPoint);
            Toggle("The bodies it leaves never rot (the game: a grave mound after ~75 min out of sight)", Corpses.NeverRot, null);
            TextRow("Notification text (empty: the game's \"New Body\")", Corpses.Notification);
            FloatSlider("On-the-spot unload only: seconds before the bodies appear", Corpses.UnloadDelay, 0.1f, 10f, 1, null);
            FloatSlider("On-the-spot unload only: seconds before it vanishes", Corpses.LeaveDelay, 0.1f, 10f, 1, null);
            if (GUILayout.Button(Tip("Start a delivery now (the game's own donkey, its body roll forced; not while it is already out)", "Sends the game's delivery donkey out right now with a body guaranteed. Ignored while it is already out; the result goes to the log.")))
            {
                Plugin.Log.LogInfo("[script] delivery: " + Mounts.StartCorpseDelivery()); // not "Corpses:": that prefix means the feature acted
            }
            GUILayout.Label("The game's rules stay: every second day, the morgue's capacity (bodies on the ground count), the quests that keep the donkey at the morgue for a talk.", small);

            Header("Setup");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mount: world-object id to summon (General.MountWgoId)", label, Wd(LabelW));
            string wgo = GUILayout.TextField(Plugin.MountWgoId.Value ?? "", Wd(150f)).Trim(); // ids have no spaces
            GUILayout.EndHorizontal();
            if (ResetOnRightClick(Plugin.MountWgoId, null)) { }
            else if (wgo != Plugin.MountWgoId.Value)
            {
                Plugin.MountWgoId.Value = wgo;
            }
            GUILayout.Label("Used at the next mount. The game defines npc_donkey (and npc_donkey_test, the same art); anything without a cart gives Ride style only.", small);
            Toggle("Safety: stop the mod whenever the game changed code it uses (Safety.StopOnChangedGameCode)", GK2Shared.GameDependencies.StopOnChangedGameCode, null);
            Toggle("Safety: run the mod even over changed core code, the chest off (Safety.ForceRunOnChangedGameCode)", GameCode.ForceRun, null);
            GUILayout.Label($"The game's code is checked once at game start (a change here counts from the next start): {GameCode.Status} General.Enabled is {Plugin.Enabled.Value} (set in the cfg; turning the mod off while mounted would strand the donkey, so it is not a switch here).", small);
        }

        private static void DrawRiding()
        {
            StyleSettings s = Mount.S;
            bool ride = PanelStyle == MountStyle.Ride;

            Header("Style being tuned: " + (ride ? "riding on the donkey's back" : "the donkey pulling its cart") + "  [" + s.Section + "]");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Switch style (the donkey is summoned again)", label, Wd(LabelW));
            if (GUILayout.Button(Tip(ride ? "Go to cart" : "Go to riding", ride ? "Dismounts and mounts again in Cart style, in place. Refused if a rule blocks the cart; then the ride goes on unchanged." : "Dismounts and mounts again riding on the donkey's back, in place. Refused if a rule blocks it; then the ride goes on unchanged."), Wd(SliderW)))
            {
                Mounts.SwitchStyle(ride ? MountStyle.Cart : MountStyle.Ride);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Each style keeps its own values below; they are saved under [" + s.Section + "] in the cfg.", small);

            Header("Speed");
            FloatSlider("Speed (x normal walking)", s.SpeedMultiplier, 0.5f, 4f, 2, Mount.RefreshSpeed);

            Header("Donkey position");
            FloatSlider("Donkey in front of (-) / behind (+) the rider", s.Depth, -0.5f, 0.5f, 2, null);
            CenteringToggle(s.CenterOffset);
            Toggle("Donkey follows the ground height at its own position (slopes)", s.SnapToGround, null);

            Header("Rider position");
            IntSlider("Move whole rider along the donkey: tail (-) / head (+)", s.RiderOffsetPx, -16, 16, null);

            Header("Rider torso");
            IntSlider("Move torso only along the donkey: tail (-) / head (+)", s.TorsoOffsetPx, -16, 16, Mount.RefreshSeat);
            Toggle("Torso height automatic (hip on the donkey's back)", s.AutoSeat, Mount.RefreshSeat);
            if (s.AutoSeat.Value)
            {
                IntSlider("Move torso down (-) / up (+), pixels", s.SeatAdjustPx, -16, 16, Mount.RefreshSeat);
            }
            else
            {
                FloatSlider("Torso height (0 = standing on the ground)", s.SeatOffset, -1f, 1f, 2, Mount.RefreshSeat);
            }
            Toggle("Torso bobs with the donkey's walk", s.Bob, Mount.RefreshSeat);
            if (s.Bob.Value)
            {
                FloatSlider("Bob amount (x the donkey's back, which moves 1 px)", s.BobScale, 0f, 3f, 2, Mount.RefreshSeat);
                IntSlider("Bob trails the back by (walk frames)", s.BobLagFrames, 0, 3, Mount.RefreshSeat);
                IntSlider("Head settles after the body by (walk frames, 0 = with it)", s.HeadLagFrames, 0, 2, null);
                if (s.HeadLagFrames.Value > 0)
                {
                    FloatSlider("How much the head settles (1 = all: a bobble head)", s.HeadSettle, 0f, 1f, 2, null);
                }
            }

            Header("Rider hands (while walking)");
            FloatSlider("Hands steadier than the body (0 = with it, 1 = level)", s.HandsSteady, 0f, 1f, 2, null);
            FloatSlider("Hands forward and back with each stride (px)", s.HandsSwayPx, 0f, 2f, 2, null);

            Header("Rider pose and animation");
            EnumCycler("Body pose", s.Pose, Mount.ApplyPoseNow);
            if (Mount.PoseBlocksMovement)
            {
                GUILayout.Label("This pose makes the game take control: the keeper cannot move while it plays. Choose another.", small);
            }
            Toggle("Freeze the pose while standing still (arms stop moving)", s.FreezePoseWhenIdle, null);
            if (s.FreezePoseWhenIdle.Value)
            {
                FloatSlider("Fidget: play the pose briefly every N seconds (0 = hard freeze)", s.IdleFidgetEverySeconds, 0f, 30f, 1, null);
                FloatSlider("Fidget playback speed (x clip speed)", s.IdleFidgetSpeed, 0.05f, 2f, 2, null);
                FloatSlider("Crawl speed between fidgets (0 = fully still)", s.IdleCrawlSpeed, 0f, 0.5f, 2, null);
                GUILayout.Label("The freeze stops the whole animator, the game's breathing layer included. The crawl keeps a trace of it; a fidget plays the pose for about a second at the fidget speed.", small);
            }
            FloatSlider("Pose animation speed while moving (x clip speed)", s.PoseSpeed, 0.1f, 4f, 2, null);
            Toggle("Scale pose animation speed with the riding speed", s.PoseSpeedFollowsDonkey, null);

            Header("Rider in armour (the fights)");
            FloatSlider("Speed while aiming (right click held), x the riding speed", s.FocusSpeed, 0.25f, 1f, 2, null);
            IntSlider("Seen from behind: sits higher by (px)", s.BackSeatExtraPx, 0, 12, null);
            GUILayout.Label("Aiming up in armour he sits side-saddle: the legs are hidden on the far side and he sits this much higher, so the armour skirt's hem meets the donkey's back.", small);

            Header("Rider legs (drawn on the donkey's side)");
            Toggle("Show legs on the donkey's side", s.NearLeg, Mount.RefreshLeg);
            if (s.NearLeg.Value)
            {
                EnumCycler("Legs taken from", s.LegSource, Mount.RefreshLeg);
                if (s.LegSource.Value == LegSource.Body)
                {
                    IntSlider("How much body counts as legs (px up from feet)", s.LegHeightPx, 4, 20, Mount.RefreshLeg);
                }
                IntSlider("Move legs along the donkey: head (-) / tail (+)", s.LegAdjustXPx, -12, 12, null);
                IntSlider("Move legs down (-) / up (+), pixels", s.LegAdjustYPx, -12, 12, null);
            }
            else
            {
                GUILayout.Label("Off: the keeper's own legs show, hidden wherever the donkey is drawn in front of them.", small);
            }

            // Everything about the cart itself sits together at the end; the sections only show in Cart style.
            if (PanelStyle == MountStyle.Cart)
            {
                Header("Cart on slopes");
                Toggle("Cart follows the ground under the cart", s.CartFollowsGround, null);
                if (s.CartFollowsGround.Value)
                {
                    FloatSlider("Cart: largest height shift allowed", s.CartGroundMaxShift, 0f, 1.5f, 2, null);
                    EnumCycler("How the cart meets a slope", s.CartSlope, null);
                    CartSlopeStyle next = (CartSlopeStyle)(((int)s.CartSlope.Value + 1) % Enum.GetValues(typeof(CartSlopeStyle)).Length);
                    bool slopeLocked = Tuned.OverriddenBy(s.CartSlope.Entry) != null;
                    GUI.enabled = !slopeLocked;
                    bool compare = GUILayout.Button(Tip("Compare: switch to " + next + " (live)", "Switches to the next slope style while you ride, so you can compare them on a hill. Greyed when a mod locks this setting.")) && !slopeLocked;
                    GUI.enabled = true;
                    if (compare)
                    {
                        Put(s.CartSlope.Entry, next);
                        Plugin.Log.LogInfo("Mount: cart slope style " + next + ".");
                    }
                    if (s.CartSlope.Value == CartSlopeStyle.WholeCartPitch)
                    {
                        FloatSlider("Steepest cart pitch, degrees", s.CartPitchMaxDegrees, 0f, 45f, 0, null);
                    }
                    GUILayout.Label(CartSlopeNote(s.CartSlope.Value) + " " + Mount.CartSlopeStatus(), small);
                }

                GUILayout.Label("The cargo (its places, the packer's rules, its behaviour) and the lantern have their own tabs.", small);
            }
        }

        // Everything about the cart's lantern: pole placement, sway, and the lamp's lights and sprites. All of it is
        // per style ([Cart]); the tab shows a note in Ride style, where there is no cart.
        private static void DrawLantern()
        {
            if (PanelStyle != MountStyle.Cart)
            {
                GUILayout.Label("The lantern belongs to the cart. Set Style = Cart on the General tab and mount again to see and tune it here.", small);
                return;
            }
            StyleSettings s = Mount.S;
            Header("Show");
            Toggle("Show the lantern pole and lamp at all", s.LanternShow, Lantern.Refresh);
            if (!s.LanternShow.Value)
            {
                GUILayout.Label("Hidden: pole, lamp, glow and both lights. Everything below applies once it is shown again.", small);
                return;
            }

            Header("Pole placement");
            GUILayout.Label("The prefab puts the pole at the front rail, where the rider's head hides it. Offsets in world units, angles in degrees; the pole turns and scales about its own foot, the lamp and its lights follow the top, and the cart's walking sway is kept.", small);
            FloatSlider("Along the cart: towards the donkey (-) / the back (+)", s.LanternAlong, -0.6f, 0.9f, 2, Lantern.Refresh);
            FloatSlider("Up (+) / down (-)", s.LanternUp, -0.5f, 0.8f, 2, Lantern.Refresh);
            FloatSlider("Across: away from the camera (-) / towards it (+)", s.LanternAcross, -0.6f, 0.6f, 2, Lantern.Refresh);
            FloatSlider("Tilt in the screen plane: top towards the donkey (-) / the back (+)", s.LanternTilt, -60f, 60f, 0, Lantern.Refresh);
            FloatSlider("Lean: top away from the camera (-) / towards it (+)", s.LanternLean, -60f, 60f, 0, Lantern.Refresh);
            FloatSlider("Size of the pole (1 = the game's)", s.LanternScale, 0.5f, 1.6f, 2, Lantern.Refresh);
            Toggle("Mirror the pole (foot stays; it leans out over the back instead of over the donkey)", s.LanternFlip, Lantern.Refresh);
            Toggle("Lamp hangs straight from its hook (on slopes, while walking, with tilt or lean)", s.LanternHangs, Lantern.Refresh);
            GUILayout.Label(LampHang.Note + (LampHang.Active ? $"; turned {LampHang.LastTurn:F1}° to hang" : ""), small);

            Header("Sway");
            FloatSlider("Pendulum about the foot, degrees either way (0 = off; scaled by the wind)", s.LanternSwayDegrees, 0f, 10f, 1, Lantern.Refresh);
            FloatSlider("Sway period, seconds", s.LanternSwaySeconds, 0.5f, 10f, 1, Lantern.Refresh);
            GUILayout.Label("The game never sways the pole except by rocking the whole cart while it rolls; this adds a slow swing whose amplitude drifts and follows the weather's wind value.", small);

            Header("Main light (lights the ground and the cart)");
            Toggle("On", s.LanternLightOn, Lantern.Refresh);
            FloatSlider("Brightness (x the game's)", s.LanternLightIntensity, 0f, 4f, 2, Lantern.Refresh);
            FloatSlider("Reach (x the game's)", s.LanternLightRange, 0.2f, 3f, 2, Lantern.Refresh);
            Toggle("Casts soft shadows (as the game's does)", s.LanternLightShadows, Lantern.Refresh);
            FloatSlider("Brightness in full daylight (1 = always lit, as the game's cart; 0 = off by day)", s.LanternLightDayDim, 0f, 1f, 2, Lantern.Refresh);
            ColorRow("Colour of both lights (hex; the game's is FF9600)", s.LanternLightColor, Lantern.Refresh);

            Header("Character light (lights only people and sprites nearby)");
            Toggle("On", s.LanternSelfLightOn, Lantern.Refresh);
            FloatSlider("Brightness (x the game's)", s.LanternSelfLightIntensity, 0f, 4f, 2, Lantern.Refresh);
            FloatSlider("Reach (x the game's)", s.LanternSelfLightRange, 0.2f, 3f, 2, Lantern.Refresh);

            Header("Lamp sprites");
            Toggle("Glow around the lamp", s.LanternGlowOn, Lantern.Refresh);
            FloatSlider("Glow opacity (x the game's)", s.LanternGlowAlpha, 0f, 4f, 2, Lantern.Refresh);
            FloatSlider("Glow size (x the game's)", s.LanternGlowScale, 0.3f, 3f, 2, Lantern.Refresh);
            Toggle("Lit lantern glass", s.LanternGlassOn, Lantern.Refresh);

            GUILayout.Label(Lantern.LightSummary(), small);
            GUILayout.Label(Lantern.Summary(), small);
        }

        private static string CartSlopeNote(CartSlopeStyle style)
        {
            switch (style)
            {
                case CartSlopeStyle.WholeCartPitch:
                    return "WholeCartPitch: the wheels follow the ground and the cart pitches about its axle, so the shafts stay at the donkey; the lamp keeps hanging upright.";
                case CartSlopeStyle.ShaftsTilt:
                    return "ShaftsTilt: the cart is lifted level by the ground difference and only the shafts turn about their harness end.";
                default:
                    return "HalfLift: the cart is lifted level by the ground difference and the shafts by half of it.";
            }
        }


        private static void DrawSounds()
        {
            StyleSettings s = Mount.S;
            bool cart = PanelStyle == MountStyle.Cart;
            Header("Sounds for this style  [" + s.Section + "]");
            GUILayout.Label("All sounds are the game's own, played at the donkey through the game's sound player, so the in-game SFX volume applies. Volumes and pitches multiply the game's values for that sound (1 = as the game plays it). Sound ids can be any id from the game's list (PROBE-FINDINGS Round 12); an unknown id plays nothing and is marked here. The play buttons preview a sound with the settings below.", small);
            Toggle("All mount sounds on", s.SoundsOn, null);
            FloatSlider("Master volume", s.SoundVolume, 0f, 3f, 2, null);
            if (!s.SoundsOn.Value)
            {
                GUILayout.Label("Off: the game's own behaviour (a silent donkey, the keeper's footsteps, the game's lip sound).", small);
                return;
            }

            Header("Hoofsteps");
            Toggle("Hoofsteps while the donkey walks", s.HoofOn, null);
            SoundIdRow("Sound id (empty = by the ground: grass, dirt, wood, stone)", s.HoofSoundId, "hoof", allowEmpty: true);
            Toggle("Pick the step by the ground under the donkey (empty id only; off = dirt)", s.HoofSurfaceAware, null);
            FloatSlider("Volume", s.HoofVolume, 0f, 3f, 2, null);
            FloatSlider("Pitch (below 1 = heavier)", s.HoofPitch, 0.3f, 2f, 2, null);
            FloatSlider("Random pitch change per step, either way", s.HoofPitchVariation, 0f, 0.5f, 2, null);
            IntSlider("Footfalls per walk cycle", s.HoofStepsPerCycle, 1, 8, null);
            FloatSlider("Timing within the cycle (fraction of a step)", s.HoofPhase, 0f, 1f, 2, null);

            Header("Grazing and lips (standing still)");
            Toggle("Chewing sound when the donkey grazes", s.GrazeOn, null);
            SoundIdRow("Graze sound id", s.GrazeSoundId, "graze", allowEmpty: false);
            FloatSlider("Graze volume", s.GrazeVolume, 0f, 3f, 2, null);
            FloatSlider("Lip-smacking volume (the game's own lips sound; 0 = silent)", s.LipsVolume, 0f, 3f, 2, null);
            if (GUILayout.Button(Tip("Play the lips sound", "Plays the game's own lip-smacking sound at the donkey once. Needs a mounted donkey."), Wd(160f))) MountSounds.Preview("lips");

            Header("Braying");
            SoundIdRow("Bray sound id (voice_donkey: four short ee-aw sounds)", s.BraySoundId, "bray", allowEmpty: false);
            FloatSlider("Chance of a bray when you mount", s.BrayOnMountChance, 0f, 1f, 2, null);
            FloatSlider("One idle bray on average every ... seconds (0 = never)", s.BrayIdleEverySeconds, 0f, 300f, 0, null);
            Toggle("Idle brays also while moving", s.BrayWhileWalking, null);
            FloatSlider("Volume", s.BrayVolume, 0f, 3f, 2, null);
            FloatSlider("Pitch", s.BrayPitch, 0.3f, 2f, 2, null);
            Toggle("Follows the SFX volume (off: the speech volume, as in the donkey's dialogue)", s.BrayInSfxChannel, null);

            Header("Getting on and off (this style's own sounds: id#n plays only clip n)");
            Toggle("Sound when getting on", s.MountOn, null);
            SoundIdRow("Getting-on sound id", s.MountSoundId, "mount", allowEmpty: false);
            FloatSlider("Getting-on volume", s.MountVolume, 0f, 3f, 2, null);
            FloatSlider("Getting-on pitch", s.MountPitch, 0.3f, 2f, 2, null);
            FloatSlider("Getting-on: start this far into the clip (s)", s.MountSoundStart, 0f, 1f, 2, null);
            Toggle("Sound when getting off", s.DismountOn, null);
            SoundIdRow("Getting-off sound id", s.DismountSoundId, "dismount", allowEmpty: false);
            FloatSlider("Getting-off volume", s.DismountVolume, 0f, 3f, 2, null);
            FloatSlider("Getting-off pitch", s.DismountPitch, 0.3f, 2f, 2, null);
            FloatSlider("Getting-off: start this far into the clip (s)", s.DismountSoundStart, 0f, 1f, 2, null);

            Header("Cart");
            if (!cart)
            {
                GUILayout.Label("Wheels, the cargo thump and the hitching sound belong to the cart. Mount in Cart style to tune them.", small);
            }
            else
            {
                Toggle("Wheel rumble while the cart moves", s.WheelOn, null);
                SoundIdRow("Wheel sound id (conveyor_assembly_wheel = chain and rev)", s.WheelSoundId, "wheel", allowEmpty: false);
                FloatSlider("Wheel volume", s.WheelVolume, 0f, 3f, 2, null);
                FloatSlider("Wheel pitch", s.WheelPitch, 0.3f, 2f, 2, null);
                Toggle("Wheel pitch rises with the walking speed", s.WheelPitchFollowsSpeed, null);
                IntSlider("Once every ... walk cycles", s.WheelEveryCycles, 1, 4, null);
                FloatSlider("Timing within the cycle", s.WheelPhase, 0f, 1f, 2, null);
                Toggle("Thump when an item lands in the cart", s.CargoThumpOn, null);
                SoundIdRow("Thump sound id", s.CargoThumpSoundId, "thump", allowEmpty: false);
                FloatSlider("Thump volume", s.CargoThumpVolume, 0f, 3f, 2, null);
                FloatSlider("Thump pitch", s.CargoThumpPitch, 0.3f, 2f, 2, null);
                Toggle("Sound when a cart is hitched", s.HitchOn, null);
                SoundIdRow("Hitching sound id", s.HitchSoundId, "hitch", allowEmpty: false);
                FloatSlider("Hitching volume", s.HitchVolume, 0f, 3f, 2, null);
                FloatSlider("Hitching pitch", s.HitchPitch, 0.3f, 2f, 2, null);
                Toggle("Sound when the cart is left standing", s.UnhitchOn, null);
                SoundIdRow("Unhitching sound id", s.UnhitchSoundId, "unhitch", allowEmpty: false);
                FloatSlider("Unhitching volume", s.UnhitchVolume, 0f, 3f, 2, null);
                FloatSlider("Unhitching pitch", s.UnhitchPitch, 0.3f, 2f, 2, null);
            }

            Header("Bell");
            SoundIdRow("Bell sound id", s.BellSoundId, "bell", allowEmpty: false);
            Toggle("Ring when the donkey sets off after standing", s.BellOnSetOff, null);
            Toggle("... only with cargo on the cart", s.BellOnlyWithCargo, null);
            FloatSlider("... after standing at least (seconds)", s.BellSetOffIdleSeconds, 0f, 60f, 0, null);
            FloatSlider("Volume", s.BellVolume, 0f, 3f, 2, null);
            FloatSlider("Pitch", s.BellPitch, 0.3f, 2f, 2, null);

            Header("Status");
            GUILayout.Label("Last sound: " + (string.IsNullOrEmpty(MountSounds.LastEvent) ? "none yet" : MountSounds.LastEvent), small);
            if (GUILayout.Button(Tip("Reset the sounds of this style to defaults", "Puts every sound setting of this style (ids, volumes, pitches, timings) back to the mod defaults. Other styles and tabs are untouched.")))
            {
                s.ResetSounds();
            }
        }

        // A sound id as a text field with a play button; an id the game does not have is flagged.
        private static void SoundIdRow(string name, ConfigEntry<string> entry, string preview, bool allowEmpty)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, LabelFor(entry), Wd(LabelW));
            if (GUILayout.Button(Tip("Play", "Plays this row's sound once at the donkey, with the volume and pitch set here. Needs a mounted donkey; an unknown id plays nothing."), Wd(50f)))
            {
                MountSounds.Preview(preview);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool locked = Locked(entry, out string shown);
            GUI.enabled = !locked;
            string v = GUILayout.TextField(shown ?? "", Wd(LabelW));
            GUI.enabled = true;
            string id = (v ?? "").Trim(); // stored trimmed: a space around a pasted id would not be found
            bool ok = (allowEmpty && id.Length == 0) || MountSounds.KnownId(id);
            GUILayout.Label(ok ? "" : "unknown id", small);
            GUILayout.EndHorizontal();
            if (locked) { return; }
            if (ResetOnRightClick(entry, null)) { return; }
            if (id != Val(entry))
            {
                Put(entry, id);
            }
        }

        // A hex colour as three 0-1 sliders; the entry keeps RRGGBB so the config stays hand-editable.
        private static void ColorRow(string name, ConfigEntry<string> entry, Action onChange, string tip = null)
        {
            bool locked = Locked(entry, out string shown);
            Color c = Lantern.ParseColor(shown, new Color(1f, 0.59f, 0f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            GUILayout.Label(shown, label, Wd(70f));
            GUILayout.EndHorizontal();
            if (!locked && ResetOnRightClick(entry, onChange)) { return; }
            GUI.enabled = !locked;
            Color n = c;
            n.r = Channel("Red", c.r);
            n.g = Channel("Green", c.g);
            n.b = Channel("Blue", c.b);
            GUI.enabled = true;
            if (locked) { return; }
            if (n != c)
            {
                Put(entry, Lantern.ToHex(n));
                onChange?.Invoke();
            }
        }

        private static float Channel(string name, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("    " + name, label, Wd(LabelW));
            float r = GUILayout.HorizontalSlider(v, 0f, 1f, Wd(90f));
            GUILayout.Label(Mathf.RoundToInt(r * 255f).ToString(), label, Wd(45f));
            GUILayout.EndHorizontal();
            return Mathf.Abs(r - v) > 0.002f ? r : v;
        }

        // ------------------------------------------------------------------ Colors tab

        private static string colorRegion = "Coat";
        private static string swapSource;
        private static float confirmColorResetUntil = -1f;

        // Every colour region of the donkey, the cart and the lantern (Colors.cs), then the lamp's lights. The rows
        // edit the [Colors] sections of the cfg; the donkey looks the same in both styles, so there is one set.
        private static void DrawColors()
        {
            Header("Colours");
            GUILayout.Label("These colours are your settings only (your cfg). They are never made mod defaults: every player starts with the game's own art. A donkey with its own look (Saddle Up's, set by its coat, harness, paint and light choices) ignores them; Sources tab.", small);
            GUILayout.Label("Recolours the donkey, the cart and the lantern through the game's own colour paths: an exact colour swap for the donkey's sprites, the game's colour cube for the 3D cart. Each region below is a set of the art's own colours; the same settings apply in both styles. Nothing is touched while a region is at its defaults.", small);
            Toggle("Colours on", Colors.Enabled, Colors.MarkDirty);
            EnumCycler("Donkey sprites recoloured with", Colors.SpriteMethod, Colors.MarkDirty);
            GUILayout.Label(Colors.SpriteMethod.Value == SpriteColorMethod.Palette
                ? "Palette: every colour swapped exactly, as the game does for the keeper's outfit. If the donkey does not change at all, try Lut."
                : "Lut: the game's 32-step colour cube, as for zombies. A few near-identical colours of neighbouring regions change together.", small);
            GUILayout.BeginHorizontal();
            if (Colors.TestActive)
            {
                if (GUILayout.Button(Tip("Clear the test colours", "Removes the loud test colours again; your own colour settings show as before."))) Colors.ClearTest();
            }
            else if (GUILayout.Button(Tip("Test: obvious colours everywhere", "Paints every region a loud distinct colour for this session (rows lock, shown cyan) to see what each region covers. Your values stay.")))
            {
                Colors.ApplyTest();
            }
            if (GUILayout.Button(Tip("Write a colour report to the log", "Writes what the recolouring finds (sprites, materials, regions, method in use) to BepInEx/LogOutput.log. Changes nothing.")))
            {
                Plugin.Log.LogInfo(Colors.Diagnose());
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("The test is a session override (cyan, locked rows): purple coat, red harness, green eye, turquoise cart body, cyan wheels, yellow hubs, green shafts, pink pole, red lamp glass, blue glow and glass. Your own values are unchanged underneath. " + Colors.Note, small);

            Header("Region");
            foreach (string area in new[] { "Donkey", "Cart", "Lantern" })
            {
                var keys = new List<string>();
                var names = new List<string>();
                foreach (MountColorRegion reg in Colors.RegionList)
                {
                    if (reg.Area != area) continue;
                    keys.Add(reg.Key);
                    names.Add((Colors.RegionChanged(reg.Key) ? "* " : "") + ShortName(reg));
                }
                GUILayout.Label(area, label);
                int at = keys.IndexOf(colorRegion);
                int picked = GUILayout.SelectionGrid(at, names.ToArray(), 3);
                if (picked != at && picked >= 0)
                {
                    colorRegion = keys[picked];
                    swapSource = null;
                }
            }
            GUILayout.Label("* = changed. Right-click a row to put it back to its default.", small);

            MountColorRegion region = null;
            foreach (MountColorRegion reg in Colors.RegionList)
            {
                if (reg.Key == colorRegion) region = reg;
            }
            Colors.RegionSettings rs = Colors.SettingsOf(colorRegion);
            if (region != null && rs != null)
            {
                DrawColorRegion(region, rs);
            }

            Header("Lamp light and glow (cart style)");
            ColorRow("Colour of both lights (hex; the game's is FF9600)", Plugin.Cart.LanternLightColor, Lantern.Refresh);
            OptionalColorRow("Character light colour (off = as the main light)", Colors.CharacterLightColor, "FFD080", null);
            OptionalColorRow("Glow colour (off = the game's)", Colors.GlowColor, "FFB040", null);
            OptionalColorRow("Lit glass tint (off = the game's)", Colors.LitGlassTint, "FFFFFF", null);
            GUILayout.Label("The lights' brightness and reach and the glow's opacity are on the Lantern tab.", small);

            Header("Reset");
            bool confirming = Time.unscaledTime < confirmColorResetUntil;
            if (GUILayout.Button(Tip(confirming ? "Click again: every colour back to its default" : "Reset all colours to the mod defaults", confirming ? "Second click within 4 seconds: every region, swap and lamp colour goes back to the game's own art." : "Asks once more, then puts every region, swap and lamp colour back to the game's own art. One region alone: its own Reset below.")))
            {
                if (!confirming)
                {
                    confirmColorResetUntil = Time.unscaledTime + 4f;
                }
                else
                {
                    confirmColorResetUntil = -1f;
                    Colors.ResetAll();
                    swapSource = null;
                }
            }
        }

        private static string ShortName(MountColorRegion reg)
        {
            string n = reg.Name;
            if (n.StartsWith("Donkey ", StringComparison.Ordinal)) n = n.Substring(7);
            if (n.Length > 18) n = n.Substring(0, 17) + ".";
            return char.ToUpperInvariant(n[0]) + n.Substring(1);
        }

        private static void DrawColorRegion(MountColorRegion region, Colors.RegionSettings s)
        {
            Header(region.Name + "  [" + Colors.Section + "." + region.Key + "]");
            GUILayout.Label("The region's own colours: on top the game's, below what they become. Click one to swap it for an exact colour of your own.", small);
            Locked(s.Swaps.Entry, out string swapText);
            Dictionary<string, string> swaps = Colors.ParseSwaps(swapText);
            const float size = 22f;
            int perRow = Mathf.Max(4, Mathf.FloorToInt(ContentW / (size + 6f)));
            for (int i = 0; i < region.SourceColors.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(region.SourceColors.Count, i + perRow); j++)
                {
                    string hex = region.SourceColors[j];
                    Colors.TryHex(hex, out Color32 src);
                    Color32 dst = Colors.Preview(region.Key, src);
                    GUILayout.BeginVertical(Wd(size));
                    Rect top = Swatch(src, size);
                    Rect bottom = Swatch(dst, size);
                    GUILayout.EndVertical();
                    Rect both = new Rect(top.x, top.y, size, bottom.yMax - top.y);
                    if (GUI.Button(both, GUIContent.none, GUIStyle.none))
                    {
                        swapSource = swapSource == hex ? null : hex;
                    }
                    if (Event.current.type == EventType.Repaint && (swapSource == hex || swaps.ContainsKey(hex)))
                    {
                        // a line above and below: white for the selected colour, grey for a swapped one
                        Color frame = swapSource == hex ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                        GUI.DrawTexture(new Rect(both.x - 1f, both.y - 3f, size + 2f, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, frame, 0f, 0f);
                        GUI.DrawTexture(new Rect(both.x - 1f, both.yMax + 1f, size + 2f, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, frame, 0f, 0f);
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (swapSource != null && ContainsHex(region.SourceColors, swapSource))
            {
                DrawSwapEditor(swapSource, s, swaps);
            }
            GUILayout.Label(swaps.Count == 0 ? "No swaps." : $"{swaps.Count} swap{(swaps.Count == 1 ? "" : "s")}: {Colors.FormatSwaps(swaps)}", small);

            FloatSlider("Hue shift, degrees", s.Hue, -180f, 180f, 0, Colors.MarkDirty);
            FloatSlider("Saturation (x; 0 = grey)", s.Saturation, 0f, 3f, 2, Colors.MarkDirty);
            FloatSlider("Brightness (x)", s.Brightness, 0f, 3f, 2, Colors.MarkDirty);
            FloatSlider("Contrast (x)", s.Contrast, 0f, 3f, 2, Colors.MarkDirty);
            OptionalColorRow("Colorize: this hue, the art's shading", s.Colorize, "4A7BD0", Colors.MarkDirty);
            if (Colors.TryHex(s.Colorize.Value, out _))
            {
                FloatSlider("    Colorize amount", s.ColorizeAmount, 0f, 1f, 2, Colors.MarkDirty);
            }
            OptionalColorRow("Tint: multiplied in", s.Tint, "FFFFFF", Colors.MarkDirty);
            OptionalColorRow("Overlay: a flat colour mixed over it", s.Overlay, "C03030", Colors.MarkDirty);
            if (Colors.TryHex(s.Overlay.Value, out _))
            {
                FloatSlider("    Overlay amount", s.OverlayAmount, 0f, 1f, 2, Colors.MarkDirty);
            }
            GUILayout.Label("Order: a swap wins; else hue, saturation, brightness, contrast, colorize, tint, overlay.", small);
            if (GUILayout.Button(Tip("Reset this region", "Puts this region's swaps, hue, saturation, brightness, contrast, colorize, tint and overlay back to the game's art. Other regions stay.")))
            {
                Colors.ResetRegion(region.Key);
                swapSource = null;
            }
        }

        private static bool ContainsHex(IReadOnlyList<string> list, string hex)
        {
            foreach (string h in list)
            {
                if (string.Equals(h, hex, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // The replacement for one source colour: RGB sliders writing its SRC=DST pair into the region's Swaps.
        private static void DrawSwapEditor(string src, Colors.RegionSettings s, Dictionary<string, string> swaps)
        {
            bool locked = Locked(s.Swaps.Entry, out _);
            Colors.TryHex(src, out Color32 from);
            bool has = swaps.TryGetValue(src, out string dstHex);
            Color32 to = has && Colors.TryHex(dstHex, out Color32 d) ? d : Colors.Preview(colorRegion, from);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Swap #{src} for #{Colors.Hex(to)}", LabelFor(s.Swaps.Entry), Wd(LabelW));
            Swatch(to, 22f);
            GUILayout.EndHorizontal();
            GUI.enabled = !locked;
            Color c = to;
            Color n = c;
            n.r = Channel("Red", c.r);
            n.g = Channel("Green", c.g);
            n.b = Channel("Blue", c.b);
            GUILayout.BeginHorizontal();
            bool remove = GUILayout.Button(Tip(has ? "Remove this swap" : "(no swap yet)", has ? "Drops the exact-colour replacement of the selected colour; it follows the region's sliders again." : "Nothing to remove yet: move the sliders above to give this colour a replacement first.")) && has;
            bool clear = GUILayout.Button(Tip("Remove every swap of this region", "Clears all exact-colour replacements of this region at once; the sliders still apply. Does nothing when there are none.")) && swaps.Count > 0;
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (locked)
            {
                return;
            }
            if (clear)
            {
                Put(s.Swaps.Entry, "");
            }
            else if (remove)
            {
                swaps.Remove(src);
                Put(s.Swaps.Entry, Colors.FormatSwaps(swaps));
            }
            else if (n != c)
            {
                swaps[src] = Colors.Hex(n);
                Put(s.Swaps.Entry, Colors.FormatSwaps(swaps));
            }
        }

        // A colour square; returns its rectangle.
        private static Rect Swatch(Color32 c, float size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 0f);
            }
            return r;
        }

        // A hex colour that can be off (empty): an On/Off button, a swatch, and RGB sliders while on.
        private static void OptionalColorRow(string name, ConfigEntry<string> entry, string whenOn, Action onChange)
        {
            bool locked = Locked(entry, out string shown);
            bool on = Colors.TryHex(shown, out Color32 c32);
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, LabelFor(entry), Wd(LabelW));
            if (on)
            {
                Swatch(c32, 18f);
                GUILayout.Label(Colors.Hex(c32), label, Wd(62f));
            }
            GUI.enabled = !locked;
            bool clicked = GUILayout.Button(on ? "On" : "Off", Wd(44f)) && !locked;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!locked && ResetOnRightClick(entry, onChange)) { return; }
            if (clicked)
            {
                Put(entry, on ? "" : whenOn);
                onChange?.Invoke();
                return;
            }
            if (!on)
            {
                return;
            }
            GUI.enabled = !locked;
            Color c = c32;
            Color n = c;
            n.r = Channel("Red", c.r);
            n.g = Channel("Green", c.g);
            n.b = Channel("Blue", c.b);
            GUI.enabled = true;
            if (!locked && n != c)
            {
                Put(entry, Colors.Hex(n));
                onChange?.Invoke();
            }
        }


        // The Debug tab in four groups (one shown at a time) and the explanations folded away: it had grown into one
        // long page of every tool ever built, hard to find anything in (2026-10-12).
        private static int debugGroup;
        private static bool debugHelp;
        private static readonly string[] DebugGroups = { "Cargo tools", "Panel & levels", "Logs & checks", "Investigations" };

        private static void DrawDebug()
        {
            // drawn by the group and the help state at the start of the pass: a click switches them from the next pass on
            int group = debugGroup;
            bool help = debugHelp;
            GUILayout.BeginHorizontal();
            debugGroup = GUILayout.Toolbar(debugGroup, DebugGroups);
            debugHelp = GUILayout.Toggle(debugHelp, " explanations", Wd(110f));
            GUILayout.EndHorizontal();
            helpShown = help;
            Help("The self-tests and the performance readout have their own tabs (Tests, Performance).");
            switch (group)
            {
                case 0: DrawDebugCargo(); break;
                case 1: DrawDebugPanel(); break;
                case 2: DrawDebugLogs(); break;
                default: DrawDebugProbes(); break;
            }
        }

        private static bool helpShown;

        // A tool's explanation, shown when 'explanations' is on.
        private static void Help(string text)
        {
            if (helpShown)
            {
                GUILayout.Label(text, small);
            }
        }

        private static void DrawDebugCargo()
        {
            Header("Every load of up to six items: status, coverage, pictures");
            if (GUILayout.Button(Tip("Tuning status, instant (status.html: every contact, what to tune next, no pictures)", "Runs every load of up to six items through the packer and writes status.html to the sheet folder. Instant, needs no cart, nothing changes."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[coverage] " + CargoSheet.StartStatus());
            }
            if (GUILayout.Button(Tip("Photograph the coverage set (coverage.html: every contact in its smallest load, the fewest loads showing all)", "Puts the fewest loads showing every contact on the cart in turn and photographs them into coverage.html. Needs Cart style and an empty cart."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[coverage] " + CargoSheet.StartCoverage());
            }
            if (GUILayout.Button(Tip($"Photograph every load (all.html: all {CargoSheet.CoverageMaxItems}-or-fewer item loads, with filters; minutes)", "Photographs every possible load into all.html with filters. Takes minutes; needs Cart style and an empty cart; your cargo is put back after."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[coverage] " + CargoSheet.StartAllLoads());
            }
            Help("Every load of 1 to 6 items goes through the packer with the layout as it is now; a load is built from contacts (x on y, x beside y, x alone on a pair, x alone), and each contact is one tuning. The status page needs nothing; the photo runs need Cart style and an empty cart, like the pair pictures below, and put everything back. Same folder as the pair pictures.");
            GUILayout.Label(CargoSheet.Status, small); // the last run of any of these buttons, and of the pictures below

            DrawTestLoadBuilder();
            Header("Item shapes (measured from the meshes)");
            if (GUILayout.Button(Tip("Measure the item shapes (cargo-shapes.cfg and shape-heights.html)", "Reads every item size's mesh back from the graphics card and saves its top and bottom surfaces to the repo's cargo-shapes.cfg. Nothing in the game changes."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[shapes] " + ShapeProbe.MeasureAll());
            }
            if (GUILayout.Button(Tip("Check the saved shapes against the items on the cart", "Compares the saved shape file with the items on the cart right now and reports differences below and in the log. Needs items on the cart."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[shapes] " + ShapeProbe.CheckOnCart());
            }
            GUILayout.Label(ShapeProbe.Status, small);
            Help("Reads every size class's triangles back from the graphics card (the meshes are not readable otherwise) and saves its top and bottom surface on a 1 cm grid to the repo's cargo-shapes.cfg, with a page to check them by eye in the sheet folder. Nothing in the game changes. The seat suggestions and the problem scan are built on it.");
            Header("The building blocks only: every pair, every item alone");
            if (GUILayout.Button(Tip("Photograph every pair of size classes, beside and stacked (index.html)", "Puts every item shape and every ordered pair on the cart, photographs each into a grid (index.html). Needs Cart style; the panel hides; cargo is put back."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[sheet] " + CargoSheet.Start());
            }
            if (GUILayout.Button(Tip("Photograph every item alone, grouped by size class (shapes.html)", "Photographs each item on the cart by itself, sorted by shape group, into shapes.html. Needs Cart style; cargo is put back afterwards."), GUILayout.ExpandWidth(true)))
            {
                Plugin.Log.LogInfo("[sheet] " + CargoSheet.StartShapes());
            }
            Help("Mount in Cart style, stand still with the cart in view, press. Each distinct item shape alone and every ordered pair is put on the cart in turn, photographed and cropped; index.html in the sheet folder (the repo's tools/cargo-sheet/out on the machine with the sources, else BepInEx/GK2-cargo-sheet; emptied of the last run's pictures first) shows the grid (rows: first item, columns: second). Zombies are in it through two temporary stand-in zombies (real ones, made and removed by the sheet), with every zombie head alone at the end. The panel hides during the run; your real cargo is put back afterwards.");

            Header("Drop an item in front of the keeper");
            GUILayout.BeginHorizontal();
            System.Collections.Generic.List<string> big = CarryProbe.BigItemIds();
            int at = big.IndexOf(spawnItemId.Trim());
            if (GUILayout.Button(Tip("<", "The previous big item the game defines (zombies excluded)."), Wd(28f)) && big.Count > 0)
            {
                spawnItemId = big[(at <= 0 ? big.Count : at) - 1];
            }
            spawnItemId = GUILayout.TextField(spawnItemId, Wd(200f));
            if (GUILayout.Button(Tip(">", "The next big item the game defines (zombies excluded)."), Wd(28f)) && big.Count > 0)
            {
                spawnItemId = big[(at + 1) % big.Count];
            }
            if (GUILayout.Button(Tip("Drop it here", "Creates the item with this id and drops it in front of the keeper like a normal world drop. It is saved with the game; pick it up to be rid of it."), Wd(120f)))
            {
                Plugin.Log.LogInfo("[cargo] " + CarryProbe.SpawnDrop(spawnItemId.Trim()));
            }
            GUILayout.EndHorizontal();
            Help($"Uses the game's own drop routine at the keeper's drop position, so the item is a normal world drop (it is saved like any other; pick it up with the interaction key). < and > cycle through the {big.Count} big items the game defines (zombies excluded, they are linked world objects). A corpse is generated from the delivery script's body definition.");
            if (GUILayout.Button(Tip("Spawn a test zombie here", "Lays a real zombie in front of the keeper, the way the game's quests do. It is saved like any zombie: reload without saving to be rid of it."), Wd(220f)))
            {
                Plugin.Log.LogInfo("[cargo] " + CarryProbe.SpawnZombie());
            }
            Help("A real zombie lying in front of the keeper, made the way the game's own quest scripts drop one (its organs, collar and looks included). It is saved like any other zombie: reload without saving to be rid of it.");
        }

        private static void DrawDebugPanel()
        {
            Header("This panel");
            // Stepped, not sliders: dragging a slider that rescales the panel moves the slider under the mouse.
            Stepper("Panel size, everything (Debug.TuningWindowScale)", Plugin.TuningWindowScale, 0.1f, 0.75f, 3f, "0.00");
            Stepper("Text size inside the panel (Debug.TuningWindowFontScale)", Plugin.TuningWindowFontScale, 0.05f, 0.5f, 2f, "0.00");
            Stepper("Panel width (Debug.TuningWindowWidth)", Plugin.TuningWindowWidth, 20f, 300f, 900f, "0");
            Toggle("Tuning keys (PageUp/Down torso, Home/End legs sideways, Insert/Delete legs up/down)", Plugin.TuningKeys, null);
            Toggle($"Mounts, skins and effects preview (Debug.MountsPreview; now {(Plugin.MountsOn ? "on" : "off")}, a change takes the next start)", Plugin.MountsPreview, null,
                "Off, as players have it in this version: the donkey is the only mount, no skins, no effects; a mod's registrations are kept out with a log line. On: the dragon, the rat, the snail, the worm, Gerry, Hellfire, the skin packs and the effects, as the next update will have them.");
            Help("Debug.TuningWindow=false in the cfg hides this panel for normal play.");

            Header("Levels: far and off-screen donkeys do less (this session)");
            Help("Active: ridden, edited here, near the keeper or on screen: everything. Idle: off screen up to the sleep distance: the view stays but stands frozen. Asleep: beyond it: no view (it comes back inside the wake distance). New views: a few per frame, nearest first.");
            Lod.On = GUILayout.Toggle(Lod.On, " Levels on (off: every donkey is always active, as before)");
            Lod.ActiveRadius = SessionFloatRow("Always active within", Lod.ActiveRadius, 3f, 60f, "Donkeys closer than this to the keeper are always fully active, on screen or not. In world units; this session only.");
            Lod.SleepRadius = SessionFloatRow("Asleep beyond", Lod.SleepRadius, 10f, 200f, "Donkeys farther than this lose their view entirely until the keeper comes back within the wake distance. This session only.");
            Lod.WakeRadius = Mathf.Min(SessionFloatRow("Awake again within", Lod.WakeRadius, 5f, 200f, "A sleeping donkey gets its view back when the keeper comes this close. Kept below the sleep distance so it cannot flicker. This session only."), Lod.SleepRadius - 2f);
            Lod.ScreenMargin = SessionFloatRow("On screen margin (fraction of the screen)", Lod.ScreenMargin, 0f, 1f, "How far past the screen edge a donkey still counts as on screen (and so active). 0.2 = a fifth of the screen. This session only.");
            Lod.SpawnsPerFrame = SessionIntRow("New views per frame", Lod.SpawnsPerFrame, 1, 20, "How many donkey views may be created in one frame when several wake at once, nearest first. Higher is faster but can stutter. This session only.");
            Lod.IdleSwitchesOff = GUILayout.Toggle(Lod.IdleSwitchesOff, " Idle donkeys without a cart: view switched off (off: frozen, view kept on)");
        }

        private static void DrawDebugLogs()
        {
            Header("Diagnostics");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Write values to log", "Writes one line with this style's current rider and donkey settings to BepInEx/LogOutput.log. Changes nothing.")))
            {
                StyleSettings s = Mount.S;
                Plugin.Log.LogInfo($"[tune] [{s.Section}] speed {s.SpeedMultiplier.Value}, depth {s.Depth.Value}, centering {s.CenterOffset.Value}, rider {s.RiderOffsetPx.Value} px, torso {s.TorsoOffsetPx.Value} px, autoSeat {s.AutoSeat.Value}, seat adjust {s.SeatAdjustPx.Value} px, seat offset {s.SeatOffset.Value}, bob {s.Bob.Value}, freeze {s.FreezePoseWhenIdle.Value}, pose {s.Pose.Value}, legs {s.NearLeg.Value}/{s.LegSource.Value} h {s.LegHeightPx.Value} x {s.LegAdjustXPx.Value} y {s.LegAdjustYPx.Value} | general: doors {Plugin.StayMountedThroughDoors.Value}");
            }
            if (GUILayout.Button(Tip("Write geometry to log", "Writes one line with every position involved in placing rider and donkey to the log. Only while riding; press once per facing.")))
            {
                Mount.LogGeometry();
            }
            GUILayout.EndHorizontal();
            Help("Both write one line to BepInEx/LogOutput.log. Geometry: press once per facing (and once while walking) when reporting a placement problem.");
            Mount.CartGroundProbeOn = GUILayout.Toggle(Mount.CartGroundProbeOn, " Log the cart's wheels / the ridden donkey's hooves against the ground (5 lines a second)");
            Help("[cartground] lines with the cart: its lift and pitch, each wheel's bottom against the ground under it. [rideground] lines riding without it: the body's ground line against the ground under the body and its front and back hooves. gap > 0 floats, < 0 is inside. Off again after a restart.");

            Header("Keeper moves (safety check)");
            if (GUILayout.Button(Tip("Write what the move check says around the keeper to the log", "Tests 32 spots around the keeper and logs each as allowed or refused with the reason. Changes nothing; use it next to a rock or wall.")))
            {
                ReachProbe.Write();
            }
            Help("[reach] lines: 32 spots around the keeper (1.0 and 1.8 away), each allowed or refused with the reason. Next to a rock or a wall, every spot on its far side must be refused.");

            Header("Held item (investigation)");
            if (GUILayout.Button(Tip("Write the keeper's held-item view to the log (also F5, on foot too)", "Logs one line about what the keeper holds, the animation layers and the view drawing it. Works on foot and riding; changes nothing.")))
            {
                HeldItemProbe.Write();
            }
            Help("One [held] line: the items in the keeper's hands, the animator's layers with their weights and clips, and the drop view that draws a held item (active, position, renderers). Press once on foot carrying something and once riding.");
        }

        private static void DrawDebugProbes()
        {
            Header("Lantern pole (investigation)");
            if (GUILayout.Button(Tip("Sample the lantern for 6 s, then write GK2-lantern-facts.txt", "Records the pole and lights every frame for 6 seconds, then writes BepInEx/GK2-lantern-facts.txt. Ride and turn once meanwhile; nothing moves.")) && !LanternProbe.Running)
            {
                Plugin.Log.LogInfo("[lantern] " + LanternProbe.Start(6f));
            }
            GUILayout.Label(LanternProbe.Running ? "Sampling: ride around and turn once, so both facings are seen." : LanternProbe.Status, small);
            Help("Reads the pole and light nodes at the end of every frame (after the donkey's animator) while you ride, and writes BepInEx/GK2-lantern-facts.txt: whether the animator swings them and about which axis, the pole's foot and head relative to its pivot, the lights, how the root mirrors when turning, and the pole's overlap with the keeper's body on screen. Nothing is moved.");

            Header("Game donkey (for comparison)");
            if (GUILayout.Button(Tip("Start a real delivery now (the game's own cart brings a body)", "Fires the game donkey's delivery event with its body roll forced: the cart drives from the road to the morgue. Blocked by some quests; the log says.")))
            {
                Plugin.Log.LogInfo("[script] " + ScriptDump.StartDelivery());
            }
            Help("Fires the donkey script's 'full_cycle' event with its body roll forced to pass: the cart appears at the road point donkey_01 and drives to the morgue point donkey_05. Nothing happens while global_lock_on_drop_bodies is set or while the quest 33_base_complain_go is in progress (then it walks to the morgue instead); the log line says which.");
            if (GUILayout.Button(Tip("Dump the game donkey's script (npc_donkey) to a file", "Writes the game donkey's behaviour graph as JSON to BepInEx/GK2-script-npc_donkey.json; a summary of its nodes goes to the log.")))
            {
                Plugin.Log.LogInfo("[script] " + ScriptDump.DumpWgoScript("npc_donkey"));
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Send event to the real donkey's script", label, Wd(LabelW));
            donkeyEvent = GUILayout.TextField(donkeyEvent, Wd(110f));
            if (GUILayout.Button(Tip("Send", "Sends the typed event name to the game donkey's running script, like the game's own flow does, and logs what happened."), Wd(50f)))
            {
                Plugin.Log.LogInfo("[script] " + ScriptDump.FireEventOnWgo("npc_donkey", donkeyEvent.Trim()));
            }
            GUILayout.EndHorizontal();
            Help("Other events from the dumped graph: move_to_morgue, full_cycle_back, move_from_morgue, stay_morgue, Disable, SetInteractive, SetNonInteractive.");

            Header("Cart cargo (investigation)");
            if (GUILayout.Button(Tip("Dump carry facts to a file (drawer rules, inventory filters, big items)", "Writes the loaded-cart drawer rules, the donkey's inventory filters and every big item to BepInEx/GK2-carry-facts.txt. Mount first for the rules.")))
            {
                Plugin.Log.LogInfo("[cargo] " + CarryProbe.DumpFacts());
            }
            Help("Writes BepInEx/GK2-carry-facts.txt: the donkey prefab's ConditionalDrawer rules (what switches the loaded-cart mesh), the npc_donkey definition's inventory size and item filters, every item with itemSize Big, and the mesh elements the keeper's overhead view can show. Mount first for the drawer rules.");
            if (GUILayout.Button(Tip("Dump head-item facts (patch checks, quest listeners, script expressions, item mechanics)", "Writes BepInEx/GK2-head-facts.txt and runs a live test on the cart's inventory. Mount in Cart style with empty hands and a free slot for everything.")))
            {
                Plugin.Log.LogInfo("[head] " + HeadProbe.Dump());
            }
            GUILayout.Label("Writes BepInEx/GK2-head-facts.txt for routing pickups straight onto the cart: whether patches on the game's small head methods take effect when the game calls them, which quests listen to the head triggers, every expression in the game data and every world-object script that mentions the head, and a live test of the item mechanics on the cart's inventory. Mount in Cart style with empty hands and one free cart slot for the full set. " + HeadProbe.Status, small);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Test item id", label, Wd(100f));
            cargoItemId = GUILayout.TextField(cargoItemId, Wd(110f));
            if (GUILayout.Button(Tip("Put in cart inventory", "Adds the test item id to the donkey's inventory the way the delivery script does and logs whether the cart meshes switched. Nothing is saved."), Wd(170f)))
            {
                Plugin.Log.LogInfo("[cargo] " + CarryProbe.TestLoad(cargoItemId.Trim()));
            }
            if (GUILayout.Button(Tip("Take first out", "Removes the first item from the donkey's inventory again and logs the mesh result. The test item vanishes."), Wd(120f)))
            {
                Plugin.Log.LogInfo("[cargo] " + CarryProbe.TestUnload());
            }
            GUILayout.EndHorizontal();
            Help("Adds a plain item to our donkey's inventory exactly as the delivery script does, and logs whether the game's drawer switched the cart meshes. Test items vanish; the donkey is a temp object, nothing is saved.");

            Header("Chest on the cart: step-0 probe (docs/portable-chest.md §6)");
            if (GUILayout.Button(Tip("Dump chest and building definitions to a file", "Writes every chest-like object with its flags, build and remove costs, and the door-linked scenes to BepInEx/GK2-chest-facts.txt.")))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.DumpDefinitions());
            }
            Help("Writes BepInEx/GK2-chest-facts.txt: every world object with a chest interaction or an inventory, with the flags the design depends on (size, multi-inventory, town quality, scripts, HP behaviour, filters), its build and remove definitions with costs and refunds, and which scenes keep the previous scene loaded through a door.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Hide, move 4 m, show", "Hides the chest the keeper faces (or the nearest within 6 m), moves it 4 m and shows it again: the set-down mechanism by hand. Use a test chest."), Wd(170f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.HideMoveShow());
            }
            if (GUILayout.Button(Tip("Move back", "Returns the chest moved by the button to the left to where it stood."), Wd(100f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.MoveBack());
            }
            if (GUILayout.Button(Tip("Zone out and in", "Takes the faced chest out of the world's zoning and back in; nearby workbenches and villagers should still see it. Logged."), Wd(130f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.ZoneRoundTrip());
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Stand-in here", "Makes a preview-style copy of the faced chest in front of the keeper; each press destroys the last one first."), Wd(130f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.StandIn());
            }
            if (GUILayout.Button(Tip("Remove stand-in", "Destroys the stand-in copy made by the button to the left."), Wd(130f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.RemoveStandIn());
            }
            if (GUILayout.Button(Tip("Log its colliders", "Writes the faced chest's colliders (shape, size, layer) to the log. Changes nothing."), Wd(130f)))
            {
                Plugin.Log.LogInfo("[chest] " + ChestProbe.DumpColliders());
            }
            GUILayout.EndHorizontal();
            Help("By hand, on the chest the keeper faces (or the nearest within 6 m); use an empty test chest and reload without saving afterwards. The same three moves run scripted in \"Run self-tests\" above (the three \"chest:\" scenarios make their own test chest and remove it), so these buttons are for looking at things by eye. Hide/move/show is the set-down mechanism: the chest should appear at the new spot from near and far, also after walking away and back. Zone out and in should leave a workbench nearby still seeing the chest's items and villagers pathing around it. Stand-in: each press destroys the last and makes a new preview-style copy in front of the keeper.");
        }

        // ------------------------------------------------------------------ Tests tab

        private static void DrawTests()
        {
            Header("Self-tests");
            GUILayout.Label("Scripted checks that drive the game over frames (docs/self-tests.md): the per-mod API, the side file, hitching, shared colours, the levels, view reuse, foals following, the spawn cap and leaks, the chest on the cart, the morgue door, the corpses at the crossroads. All of them take about 4 minutes; a group or one scenario alone has the same start and finish (the keeper moved to the test spot and put back, every test donkey removed, the world's own donkeys checked). They spawn their own test donkeys (never kept) and save nothing. Don't touch the keys while they run. Each scenario's tally shows here as it runs; the full [test] lines are in the log.", small);
            GUILayout.BeginHorizontal();
            if (!SelfTest.Running && GUILayout.Button(Tip("Run all", "Starts every self-test scenario on the next frame, about 4 minutes. Test donkeys are never kept, nothing is saved; keep your hands off the keys."), Wd(110f)))
            {
                SelfTest.RequestStart(); // started on the next frame (SelfTest.PollStart), not inside this GUI event
            }
            if (SelfTest.Running && GUILayout.Button(Tip("Abort", "Stops the running self-tests at the next step; the keeper is put back and the test donkeys removed."), Wd(110f)))
            {
                SelfTest.Abort();
            }
            GUILayout.Label(SelfTest.Status, wrapLabel);
            GUILayout.EndHorizontal();
            if (SelfTest.Running)
            {
                int done = SelfTest.RunDone, total = SelfTest.RunCount;
                ProgressBar(total > 0 ? (float)done / total : 0f);
                GUILayout.Label($"{done} of {total} scenario(s) done after {Mathf.FloorToInt(Time.unscaledTime - SelfTest.RunStarted)} s; so far {SelfTest.PassCount} passed, {SelfTest.FailCount} failed, {SelfTest.WarnCount} warning(s).", small);
            }
            foreach (string g in SelfTest.Groups)
            {
                GUILayout.Box(GUIContent.none, rule, ExpandW);
                GUILayout.BeginHorizontal();
                GUILayout.Label(g, header, ExpandW);
                if (!SelfTest.Running && GUILayout.Button(Tip("Run group", "Runs only this group's scenarios, with the same start and finish as a full run. Nothing is saved."), Wd(90f)))
                {
                    SelfTest.RequestStartGroup(g);
                }
                GUILayout.EndHorizontal();
                foreach (SelfTest.Scenario s in SelfTest.Catalog)
                {
                    if (s.Group != g)
                    {
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ScenarioState(s, out GUIStyle st), st, Wd(W * 0.36f));
                    GUILayout.Label(s.Name, wrapLabel, ExpandW);
                    if (!SelfTest.Running && GUILayout.Button(Tip("Run", "Runs this one scenario alone; the keeper is moved to the test spot and put back afterwards. Nothing is saved."), Wd(48f)))
                    {
                        SelfTest.RequestStart(s);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            Header("Failures and warnings of the last run");
            int shown = 0;
            for (int k = SelfTest.Lines.Count - 1; k >= 0 && shown < 40; k--)
            {
                string l = SelfTest.Lines[k];
                if (l.Contains(" FAIL ") || l.Contains(" WARN "))
                {
                    GUILayout.Label(l, l.Contains(" FAIL ") ? bad : warnWrap);
                    shown++;
                }
            }
            if (shown == 0)
            {
                GUILayout.Label(SelfTest.Lines.Count == 0 ? "No run this session." : "None.", small);
            }
        }

        // One scenario's state for its row: not run, running (with its time so far), or its tally, coloured.
        private static string ScenarioState(SelfTest.Scenario s, out GUIStyle style)
        {
            switch (s.State)
            {
                case SelfTest.RunState.Running:
                    style = warnWrap;
                    return $"running, {Mathf.FloorToInt(Time.unscaledTime - s.Started)} s";
                case SelfTest.RunState.Done:
                    if (s.Fail > 0)
                    {
                        style = bad;
                        return $"FAIL {s.Fail}, {s.Pass} passed{(s.Warn > 0 ? $", {s.Warn} warn" : "")}";
                    }
                    if (s.Warn > 0)
                    {
                        style = warnWrap;
                        return $"WARN {s.Warn}, {s.Pass} passed";
                    }
                    if (s.Skipped)
                    {
                        style = small;
                        return "SKIP (not in this setup)";
                    }
                    style = good;
                    return $"PASS {s.Pass} ({s.Elapsed:F0} s)";
                default:
                    style = small;
                    return "not run";
            }
        }

        // ------------------------------------------------------------------ Performance tab

        private static void DrawPerformance()
        {
            Header("Performance (the last second)");
            GUILayout.Label(Perf.SummaryText, small);
            GUILayout.Label("Each part includes the parts inside it (Line and Ground inside Follow, inside Shuffle, inside Stand, inside Tick). Panel is this window itself: close it (its key) to see the frame without it, and read the numbers from the log line instead.", small);
            Perf.LogEvery10s = GUILayout.Toggle(Perf.LogEvery10s, " Write this to the log every 10 s ([perf] lines; keeps going with the panel closed)");
            Perf.ExpNoShuffle = GUILayout.Toggle(Perf.ExpNoShuffle, " Experiment: standing donkeys don't shuffle or follow (the mod's movement work off)");
            Perf.ExpHideViews = GUILayout.Toggle(Perf.ExpHideViews, " Experiment: hide standing donkeys (no drawing, no animation: the game's cost of showing them)");
            GUILayout.Label("The levels (far and off-screen donkeys doing less) are tuned on the Debug tab; the \"frame time by mode\" self-test on the Tests tab measures them.", small);
        }

        // Your settings (saved as you change them) against the mod defaults (defaults.cfg, shipped in the DLL).
        private static string exportStatus = "";
        private static float confirmMakeDefaultsUntil = -1f;

        private static void DrawDefaults()
        {
            Header("Where a value comes from (the first that has one wins)");
            Mount.Instance r = Mount.Ridden;
            GUILayout.Label("1. The donkey's own values: set on one donkey by a mod (Saddle Up's looks and name) or on the Instances tab. Kept with the save when the donkey is. "
                + (r == null ? "You aren't riding one now." : $"The donkey you ride ({r.FullId}): {r.Overrides.Count} value(s){(r.Overrides.Count > 0 ? ", set by " + OwnersOf(r) : "")}{(r.OwnLook ? "; its own look: it ignores the colours of rows 3 to 5 (they are yours, not its)" : "")}."), small);
            GUILayout.Label("2. Another mod, for this session only: cyan, locked rows. Never saved; gone when the mod clears them or the game closes. "
                + (overrideSnapshot > 0 ? $"Now: {overrideSnapshot} ({OwnersOfSessionValues()})." : "Now: none."), small);
            GUILayout.Label($"3. Your settings: {CfgPath}. Every row of this panel reads and writes this file; a change is saved the moment you make it. Yellow: differs from its mod default ({diffSnapshot.Count} now).", small);
            GUILayout.Label("4. Mod defaults: what a fresh install starts with, and what right-click and the reset buttons go back to. Read from " + Defaults.SourceShort + ".", small);
            GUILayout.Label("5. The code's own values: for anything the mod defaults leave out. All colours are left out on purpose, so every player starts with the game's own art.", small);
            GUILayout.Label("Other mods keep their own settings in their own cfg (e.g. dreas.gk2.saddleup.cfg for Saddle Up's coat or speed): they reach this mod as 1 or 2, never as rows of this panel.", small);

            Header("Where a change is saved");
            GUILayout.Label("A row of this panel: your settings (3). Right-click: back to the mod default, saved the same way.", small);
            GUILayout.Label("The Instances tab's settings of one donkey: that donkey (1), kept with the save if the donkey is.", small);
            GUILayout.Label(Defaults.CanWriteDefaults
                ? "Make these the mod defaults (below): the repo's defaults.cfg (4), colours excepted; a rebuild ships it."
                : "Make these the mod defaults: not on this machine (the mod's source folder isn't here).", small);
            GUILayout.Label("Cargo corrections (Riding tab): " + (CargoLayout.SavesToRepo ? "the repo's cargo-layout.cfg." : "this session only."), small);

            List<(string Group, ConfigEntryBase Entry)> diff = diffSnapshot;
            Header(diff.Count == 0 ? "All settings match the mod defaults" : $"{diff.Count} setting{(diff.Count == 1 ? "" : "s")} differ from the mod defaults");
            foreach ((string group, ConfigEntryBase e) in diff)
            {
                GUILayout.Label($"[{group}] {e.Definition.Key}: yours {Defaults.Show(e.BoxedValue)}, mod default {Defaults.Show(Defaults.DefaultFor(e))}", small);
            }
            DrawDefaultsButtons(diff.Count);

            Header("Cargo layout (item and pair corrections)");
            if (CargoLayout.SavesToRepo)
            {
                GUILayout.Label("The corrections edited on the Riding tab are saved to the repo's cargo-layout.cfg as you make them. They have no separate 'yours': the file is the mod default, and a rebuild ships it.", small);
            }
            else
            {
                GUILayout.Label("The corrections edited on the Riding tab last for this session only: the mod's source folder is not on this machine, so there is nowhere to save them.", small);
            }
            if (GUILayout.Button(Tip("Re-read the layout file", "Loads the cargo layout file from disk again and re-packs every cart. Edits in the file since the start show up; unsaved panel changes are lost.")))
            {
                CargoLayout.Load();
                Cargo.RefreshViews();
                exportStatus = "Cargo layout reloaded: " + CargoLayout.Summary();
            }
            GUILayout.Label("Loaded now: " + CargoLayout.Summary(), small);
            if (!string.IsNullOrEmpty(CargoLayout.LastSaveNote))
            {
                GUILayout.Label(CargoLayout.LastSaveNote, small);
            }

            if (!string.IsNullOrEmpty(exportStatus))
            {
                Header("Last result");
                GUILayout.Label(exportStatus, small);
            }
        }

        // "Make these the mod defaults" (only where the sources are) and "Reset this style"; used by the footer and
        // the Sources tab.
        private static void DrawDefaultsButtons(int differing)
        {
            GUILayout.BeginHorizontal();
            if (Defaults.CanWriteDefaults && differing > 0)
            {
                bool confirming = Time.unscaledTime < confirmMakeDefaultsUntil;
                int writable = Defaults.DifferingWritable();
                if (differing > writable)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Label($"Colours are never made mod defaults: {differing - writable} of your {differing} differing settings are colours and stay yours only (your cfg). Every player starts with the game's own art.", small);
                    GUILayout.BeginHorizontal();
                }
                if (writable > 0 && GUILayout.Button(Tip(confirming ? $"Click again: make {writable} the mod defaults (no colours)" : $"Make these the mod defaults ({writable}, no colours)", confirming ? "Second click within 4 seconds: writes your differing settings to the repo's defaults.cfg. Colours are left out; a rebuild ships them." : "Asks once more, then writes your differing settings to the repo's defaults.cfg as the new mod defaults. Colours never; a rebuild ships them.")))
                {
                    if (!confirming)
                    {
                        confirmMakeDefaultsUntil = Time.unscaledTime + 4f;
                    }
                    else
                    {
                        confirmMakeDefaultsUntil = -1f;
                        try
                        {
                            string path = Defaults.MakeCurrentTheDefaults();
                            exportStatus = $"{writable} setting{(writable == 1 ? "" : "s")} are now the mod defaults, written to the repo's defaults.cfg (colours left out). Rebuild the mod to ship them in the DLL.";
                            Plugin.Log.LogInfo(exportStatus);
                        }
                        catch (Exception e)
                        {
                            exportStatus = "Making the mod defaults failed: " + e.Message;
                            Plugin.Log.LogWarning(exportStatus);
                        }
                    }
                }
            }
            if (GUILayout.Button(Tip("Reset this style to the mod defaults", "Puts every setting of the current style (Ride or Cart) back to the mod defaults and refreshes the ride. Colours and the other style stay.")))
            {
                Mount.S.ResetAll();
                Mount.RefreshLeg();
                Mount.RefreshSeat();
                Mount.RefreshSpeed();
                Mount.ApplyPoseNow();
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ controls

        // CenterOffset is a switch in practice: any value above 0 means "cancel the sprite's built-in sideways offset
        // so the donkey sits under the rider" (the exact offset is measured from the sprite each frame); 0 leaves
        // the donkey where the game draws it. Shown as a toggle; the stored value stays a float for the cfg.
        private static void CenteringToggle(ConfigEntry<float> entry)
        {
            bool locked = Locked(entry, out float shown);
            bool on = shown > 0.0001f;
            bool clicked = OnOffRow("Keep the donkey centred under the rider", on, LabelFor(entry), enabled: !locked);
            if (locked) { return; }
            if (ResetOnRightClick(entry, null)) { return; }
            if (clicked)
            {
                Put(entry, on ? 0f : 0.48f);
            }
        }

        // Layout-file corrections for what is on the cart right now. One item: its 'alone' correction (never used in a
        // pair). Two items: the pair's own tuning, which freezes the pair (CargoLayout: a tuned pair ignores the class
        // entries, alone offsets, gap, overhang, nestle, mode and load order; only the floor moves it). The class
        // entries' bottom/beside and on-top values stay in the file as the starting point of untuned pairs and are no
        // longer edited here, so tuning one pair can never move another. The packer re-runs on every change, and the
        // layout file is saved shortly after (CargoLayout.MarkChanged / SaveIfDue).
        // A - value + row for a float setting, stepping by 'step' within [min, max]; right-click the value resets it.
        private static void Stepper(string name, ConfigEntry<float> entry, float step, float min, float max, string format, string tip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            bool locked = Locked(entry, out float shown);
            GUI.enabled = !locked;
            if (GUILayout.Button("-", Wd(30f)) && !locked)
            {
                Put(entry, Mathf.Max(min, (float)Math.Round(Val(entry) - step, 2)));
            }
            GUILayout.Label(shown.ToString(format), label, Wd(50f));
            Rect r = GUILayoutUtility.GetLastRect();
            if (!locked && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
            {
                if (EditsInstance(entry)) Unput(entry); else Defaults.ResetToDefault(entry);
                Event.current.Use();
            }
            if (GUILayout.Button("+", Wd(30f)) && !locked)
            {
                Put(entry, Mathf.Min(max, (float)Math.Round(Val(entry) + step, 2)));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // A slider on a plain float (layout-file values). Right-click resets to 0. Returns true when it moved.
        // Numpad nudges (the Cargo tab turns them on while it draws, TuningWindow.Cargo.cs): every RawSlider of a pass gets
        // a number in drawing order; the one last dragged is remembered and a pending nudge is applied to it in a repaint.
        private static int rawSeq, nudgeId = -1;
        private static bool nudgeEnabled;
        private static float nudgePending;

        private static bool RawSlider(string name, ref float value, float min, float max, bool canReset = true, string tip = null)
        {
            int id = ++rawSeq;
            bool focused = nudgeEnabled && id == nudgeId;
            GUILayout.BeginHorizontal();
            // Inside a box (its padding) with a slider and a value: the label gets the rest of the content width.
            string shownName = focused ? "<color=#ffd257>» " + name.TrimStart() + $"  ({Plugin.CargoNudgeUp.Value} / {Plugin.CargoNudgeDown.Value})</color>" : name;
            GUILayout.Label(Tip(shownName, tip), label, Wd(Mathf.Max(80f, ContentW - SliderW - 50f - GUI.skin.box.padding.horizontal - 12f)));
            float v = GUILayout.HorizontalSlider(value, min, max, Wd(SliderW));
            // Only a drag (or the right-click reset) is a change. The slider hands back the value it was given when it
            // is not touched (clamped to its range); rounding that for display must not count as a move, or a value
            // with more decimals (a pair's measured places) "changed" on every event: the pair re-tuned itself right
            // after "Back to automatic" (found 2026-09-30, four "tuned" lines in the log).
            bool touched = v != Mathf.Clamp(value, min, max);
            if (touched && nudgeEnabled)
            {
                nudgeId = id; // dragged: the numpad moves this one from now on
            }
            if (focused && !touched && nudgePending != 0f && GUI.enabled && Event.current.type == EventType.Repaint)
            {
                v = Mathf.Clamp(value + nudgePending, min, max);
                touched = true;
                nudgePending = 0f;
            }
            Rect r = GUILayoutUtility.GetLastRect();
            if (canReset && GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
            {
                v = 0f;
                touched = true;
                Event.current.Use();
            }
            GUILayout.Label((touched ? v : value).ToString("0.00"), label, Wd(50f));
            GUILayout.EndHorizontal();
            if (!touched)
            {
                return false;
            }
            v = (float)Math.Round(v, 2);
            if (Mathf.Abs(v - value) > 0.0001f)
            {
                value = v;
                return true;
            }
            return false;
        }

        private static void FloatSlider(string name, ConfigEntry<float> entry, float min, float max, int decimals, Action onChange, string tip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            bool locked = Locked(entry, out float shown);
            GUI.enabled = !locked;
            float v = GUILayout.HorizontalSlider(shown, min, max, Wd(90f));
            GUI.enabled = true;
            v = (float)Math.Round(v, decimals);
            GUILayout.Label(v.ToString(Fixed(decimals)), label, Wd(45f));
            GUILayout.EndHorizontal();
            if (locked) { return; }
            if (ResetOnRightClick(entry, onChange)) { return; }
            if (Mathf.Abs(v - Val(entry)) > 0.00001f)
            {
                Put(entry, v);
                onChange?.Invoke();
            }
        }

        private static void IntSlider(string name, ConfigEntry<int> entry, int min, int max, Action onChange, string tip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            // The slider is laid out between the buttons and reads the value before "+" runs; a button press must
            // therefore win over the slider's (stale) value in the same pass.
            bool pressed = false;
            bool locked = Locked(entry, out int shown);
            GUI.enabled = !locked;
            if (GUILayout.Button("-", Wd(22f)) && !locked && Val(entry) > min) { Put(entry, Val(entry) - 1); pressed = true; onChange?.Invoke(); }
            int v = Mathf.RoundToInt(GUILayout.HorizontalSlider(locked ? shown : Val(entry), min, max, Wd(50f)));
            if (GUILayout.Button("+", Wd(22f)) && !locked && Val(entry) < max) { Put(entry, Val(entry) + 1); pressed = true; onChange?.Invoke(); }
            GUI.enabled = true;
            GUILayout.Label((locked ? shown : Val(entry)).ToString(), label, Wd(30f));
            GUILayout.EndHorizontal();
            if (locked) { return; }
            if (ResetOnRightClick(entry, onChange)) { return; }
            if (!pressed && v != Val(entry))
            {
                Put(entry, v);
                onChange?.Invoke();
            }
        }

        // Booleans as a labelled On/Off button in the value column (a label-styled GUILayout.Toggle draws no box).
        private static bool OnOffRow(string name, bool value, GUIStyle style = null, bool enabled = true, string tip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, tip), style ?? label, Wd(LabelW));
            GUI.enabled = enabled;
            bool clicked = GUILayout.Button(Tip(value ? "On" : "Off", tip), Wd(70f)) && enabled;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            return clicked;
        }

        // A free text value (a GD point id, a notification text), trimmed.
        private static void TextRow(string name, ConfigEntry<string> entry, string tip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            bool locked = Locked(entry, out string shown);
            GUI.enabled = !locked;
            string v = GUILayout.TextField(shown ?? "", Wd(SliderW));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (locked) { return; }
            if (ResetOnRightClick(entry, null)) { return; }
            v = (v ?? "").Trim();
            if (v != Val(entry))
            {
                Put(entry, v);
            }
        }

        private static void Toggle(string name, ConfigEntry<bool> entry, Action onChange, string tip = null)
        {
            bool locked = Locked(entry, out bool shown);
            bool clicked = OnOffRow(name, shown, LabelFor(entry), enabled: !locked, tip: RowTip(entry, tip));
            if (locked) { return; }
            if (ResetOnRightClick(entry, onChange)) { return; }
            if (clicked)
            {
                Put(entry, !Val(entry));
                onChange?.Invoke();
            }
        }

        // An enum's values and names, read once per type: Enum.GetValues makes an array and an enum's ToString goes
        // through reflection, on every row and every event.
        private static class EnumInfo<T> where T : struct, Enum
        {
            internal static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
            private static readonly Dictionary<T, string> names = new Dictionary<T, string>();

            internal static string Name(T v)
            {
                if (!names.TryGetValue(v, out string n))
                {
                    n = v.ToString();
                    names[v] = n;
                }
                return n;
            }
        }

        // Type inference does not look through Tuned<T>'s implicit conversion, hence this overload.
        private static void EnumCycler<T>(string name, Tuned<T> tuned, Action onChange, string tip = null) where T : struct, Enum
        {
            EnumCycler<T>(name, tuned.Entry, onChange, tip);
        }

        private static void EnumCycler<T>(string name, ConfigEntry<T> entry, Action onChange, string tip = null) where T : struct, Enum
        {
            T[] values = EnumInfo<T>.Values;
            int idx = Array.IndexOf(values, Val(entry));
            bool locked = Locked(entry, out T shown);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, RowTip(entry, tip)), LabelFor(entry), Wd(LabelW));
            bool changed = false;
            GUI.enabled = !locked;
            if (GUILayout.Button("<", Wd(22f)) && !locked) { idx = (idx - 1 + values.Length) % values.Length; changed = true; }
            GUILayout.Label(EnumInfo<T>.Name(shown), label, Wd(90f));
            if (GUILayout.Button(">", Wd(22f)) && !locked) { idx = (idx + 1) % values.Length; changed = true; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (locked) { return; }
            if (ResetOnRightClick(entry, onChange)) { return; }
            if (changed)
            {
                Put(entry, values[idx]);
                onChange?.Invoke();
            }
        }

        // A value another mod overrides this session (Tuned) is shown as the value in effect and locked: the controls
        // are greyed out and neither they nor right-click write anything, so the row cannot look like it changes
        // something that the override then ignores. Once the override ends the row shows and edits the cfg again.
        private static bool Locked<T>(ConfigEntry<T> entry, out T shown)
        {
            if (EditsInstance(entry))
            {
                shown = Val(entry);
                return false;
            }
            if (RiddenHas(entry, out object own, out _) && own is T ov)
            {
                shown = ov; // the ridden donkey's own value (a mount's, a mod's): the cfg row can't change what it shows
                return true;
            }
            if (Tuned.OverriddenBy(entry) != null && Tuned.ByEntry.TryGetValue(entry, out ITuned t) && t.Current is T v)
            {
                shown = v;
                return true;
            }
            shown = Val(entry);
            return false;
        }

        // Right-clicking a control row puts that setting back to its default. Called right after the row is laid out,
        // so GetLastRect is the row's rectangle.
        private static bool ResetOnRightClick<T>(ConfigEntry<T> entry, Action onChange)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 1)
            {
                return false;
            }
            if (!GUILayoutUtility.GetLastRect().Contains(e.mousePosition))
            {
                return false;
            }
            e.Use();
            if (EditsInstance(entry))
            {
                if (Unput(entry))
                {
                    onChange?.Invoke();
                }
                return true;
            }
            T def = (T)Defaults.DefaultFor(entry);
            if (!Equals(Val(entry), def))
            {
                Put(entry, def);
                onChange?.Invoke();
            }
            return true;
        }
    }
}
