using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // The Mounts panel (MountSettingsPage.PanelButton / OpenPanel; docs/mounts-foundation-2.md 3, the user's decision of
    // 2026-10-20: the panel IS the mount list): every mount a player can ride on the left, the chosen one drawn on the
    // right with its description, what it does (the cart, babies, speed, its effect), its own stat lines and its
    // abilities, and under them the page's look rows for that mount: its Skin, the effect and the colours. Done keeps
    // what was chosen, Cancel puts back what was set when the panel opened. Only in a game.
    //
    // It grew out of the look bar (2026-10-18 to 20: the look rows at the bottom of the screen); the Mods menu and the
    // game's Settings window no longer hold the look rows (the Mount and Skin rows, the colours, the effect), only the
    // button that opens this, so the settings page stops growing with every mount a player installs (22 cfg keys per
    // mount stay in the cfg, where they cost nothing). Built from the game's own parts, copied out of its Settings
    // window: a "< value >" row per setting (SettingsRows), its dialog button for the list and for Done and Cancel. Its
    // own canvas sorts above the HUD. If a part can't be found or copied, the panel doesn't open and the log says so.
    //
    // Nothing in it ever moves while a value changes: the picture sits in a slot of fixed size, a row shown only with
    // a preset ("Custom") stays in place greyed; choosing another mount rebuilds the right side (only the chosen
    // mount's rows exist, so thirty installed mounts cost thirty list entries, not thirty sets of rows); a panel larger
    // than the screen allows is scaled down as a whole, never cut; a list longer than a page is paged.
    internal static class MountsPanel
    {
        private static GameObject panel, picture, pictureSlot, listColumn, rightColumn, infoArea, rowsArea;
        private static RawImage pictureImage;
        private static Texture2D pictureTex;
        private static float nextPicture;
        private static MountSettingsPage page;
        private static GameObject templateRow;
        private static UIDialogWindowButton dialogTemplate;
        private static readonly List<(MountSettingsPage.Row Row, GameObject Go, Action Refresh, CanvasGroup Group)> rows = new List<(MountSettingsPage.Row, GameObject, Action, CanvasGroup)>();
        private static readonly List<(string Id, CanvasGroup Group)> listMarks = new List<(string, CanvasGroup)>();
        private static List<Entry> entries = new List<Entry>();
        private static string builtFor; // the mount the right side was built for
        private static int listPage;
        private const int ListPageSize = 10;
        private const float PictureSlotW = 480f, PictureSlotH = 220f, PictureScale = 2f; // the slot in canvas units; pictures up to 2x
        private const float EdgeGap = 24f; // the least room left at the screen's edges
        private const int WrapAt = 58;     // a description is wrapped into lines of about this many characters
        private static readonly Dictionary<ConfigEntryBaseKey, object> before = new Dictionary<ConfigEntryBaseKey, object>();

        // ConfigEntryBase as a key without a BepInEx using here.
        private readonly struct ConfigEntryBaseKey : IEquatable<ConfigEntryBaseKey>
        {
            internal readonly BepInEx.Configuration.ConfigEntryBase E;
            internal ConfigEntryBaseKey(BepInEx.Configuration.ConfigEntryBase e) { E = e; }
            public bool Equals(ConfigEntryBaseKey o) => ReferenceEquals(E, o.E);
            public override bool Equals(object o) => o is ConfigEntryBaseKey k && Equals(k);
            public override int GetHashCode() => E == null ? 0 : E.GetHashCode();
        }

        // One mount of the list: what the panel says about it. Built from the registry alone (the self-tests read it
        // without a window).
        internal sealed class Entry
        {
            internal string Id, Label, Description;
            internal readonly List<string> Lines = new List<string>(); // what it does, its stats, its abilities
            internal bool HasPortrait;
        }

        internal static bool IsOpen => panel != null && panel.activeSelf;

        // Every mount a player can pick (Looks.KindChoices: the kinds with a skin, the donkey first), with its words.
        internal static List<Entry> Entries()
        {
            var list = new List<Entry>();
            foreach ((string id, string label) in Looks.KindChoices())
            {
                Kinds.KindDef k = Kinds.Find(id);
                if (k == null) continue;
                var e = new Entry { Id = id, Label = label };
                Looks.SkinDef def = Looks.Find(Looks.DefaultSkinOf(k));
                e.Description = !string.IsNullOrEmpty(k.Description) ? k.Description : def?.Description ?? (k == Kinds.Donkey ? "The game's own donkey, as it draws it." : "");
                e.HasPortrait = k.PortraitTexture != null || !string.IsNullOrEmpty(k.Portrait) || (def != null && (def.PortraitTexture != null || !string.IsNullOrEmpty(def.Portrait)));
                e.Lines.Add(k.Cart ? "Pulls the cart" : "Can't pull the cart");
                e.Lines.Add(k.Foals ? (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f ? $"Has small babies ({k.FoalScale:0.##} of its size)" : "Has babies") : "No babies");
                object speed = ValueOf(k, "SpeedMultiplier");
                if (speed != null) e.Lines.Add("Speed: " + SpeedText(speed));
                if (!string.IsNullOrEmpty(k.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(k.Effect)?.Name ?? k.Effect) + (k.EffectWhen == MountEffectWhen.Night ? " (at night)" : ""));
                else if (def != null && !string.IsNullOrEmpty(def.Effect)) e.Lines.Add("Comes with: " + (GK2DonkeyAPI.Effects.Find(def.Effect)?.Name ?? def.Effect));
                int skins = (k == Kinds.Donkey ? 1 : 0);
                foreach (Looks.SkinDef sd in Kinds.SkinsOf(k)) if (sd.Unavailable == null) skins++; // the ones a player can pick
                if (skins > 1) e.Lines.Add(skins + " skins");
                if (k.Stats != null) foreach (var st in k.Stats) e.Lines.Add(string.IsNullOrEmpty(st.Value) ? st.Key : st.Key + ": " + st.Value);
                foreach (MountAbility a in k.Abilities)
                {
                    string name = string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name;
                    e.Lines.Add(string.IsNullOrEmpty(a.Description) ? name : name + ": " + a.Description);
                }
                list.Add(e);
            }
            return list;
        }

        private static object ValueOf(Kinds.KindDef k, string key)
        {
            object v = null;
            if (k.Tuning != null && k.Tuning.TryGetValue(key, out object t)) v = t;
            if (k.TuningRide != null && k.TuningRide.TryGetValue(key, out object r)) v = r;
            return v;
        }

        private static string SpeedText(object v)
        {
            if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) v = f;
            try
            {
                if (v is IConvertible && !(v is string)) v = Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture); // a code kind's int or double
            }
            catch
            {
            }
            return v is float x ? x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " times walking speed" : v.ToString(); // SpeedMultiplier: times the keeper's walking speed (docs/reference.md)
        }

        internal static void Open(MountSettingsPage p, bool toggle)
        {
            try
            {
                if (!Mount.IsGameReady)
                {
                    Plugin.Log.LogInfo("Settings: the Mounts panel opens in a game only (load your game first)."); // no keeper to say it in the main menu
                    return;
                }
                UIGameSettingsWindow settings = LazyUI.GetWindow<UIGameSettingsWindow>();
                if (settings == null)
                {
                    throw new InvalidOperationException("the game's Settings window isn't there to copy from");
                }
                if (IsOpen && page == p)
                {
                    if (toggle)
                    {
                        Close(keep: true); // the key again: the panel goes, what was chosen stays
                    }
                    else
                    {
                        OutOfTheWay(settings); // the button again (the menus were over the panel): they go, the panel stays as it is
                        FitToScreen();
                    }
                    return;
                }
                Close(keep: true);
                page = p;
                OutOfTheWay(settings);
                Build(settings);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: the Mounts panel can't open (" + e.Message + "); the mount and its colours are still in the cfg.");
                Close(keep: true);
            }
        }

        // Out of the way: the Settings window (without reopening the pause menu) and the pause menu.
        private static void OutOfTheWay(UIGameSettingsWindow settings)
        {
            if (LazyWindowsStackController.IsWindowOpened(settings))
            {
                settings.onClosed = null;
                settings.CloseWithoutCallback();
            }
            UIGamePauseWindow pause = LazyUI.GetWindow<UIGamePauseWindow>();
            if (pause != null && LazyWindowsStackController.IsWindowOpened(pause))
            {
                pause.Close();
            }
        }

        private static void Build(UIGameSettingsWindow settings)
        {
            UISwitchButton template = settings.GetComponentInChildren<UISwitchButton>(true);
            dialogTemplate = settings.GetComponentInChildren<UIDialogWindowButton>(true);
            if (template == null || dialogTemplate == null)
            {
                throw new InvalidOperationException("no row or button to copy in the Settings window");
            }
            Transform content = SettingsRows.ContentOf(template.transform);
            templateRow = SettingsRows.RowGameObject(template.transform, content);
            // The window's frame (the path the framework reads too); else a plain dark panel.
            Image frame = settings.transform.Find("GenericWIndowLayout/Frame")?.GetComponent<Image>();

            panel = new GameObject("GK2DonkeyAPI mounts panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(settings.transform.parent, false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.15f, 0.97f);
            if (frame != null && frame.sprite != null)
            {
                var border = new GameObject("GK2DonkeyAPI mounts panel frame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                border.transform.SetParent(panel.transform, false);
                border.GetComponent<LayoutElement>().ignoreLayout = true;
                var bi = border.GetComponent<Image>();
                bi.sprite = frame.sprite;
                bi.type = Image.Type.Sliced;
                bi.raycastTarget = false;
                var brt = (RectTransform)border.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = brt.offsetMax = Vector2.zero;
            }
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = panel.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            // What was set when the panel opened, for Cancel: every look row of the page (the mount, every mount's
            // skin and colours, the effect), not only the chosen mount's, so Cancel after choosing another mount puts
            // the first one back.
            before.Clear();
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.IsLook && r.Entry != null) before[new ConfigEntryBaseKey(r.Entry)] = r.Entry.BoxedValue;
            }

            SettingsRows.MakeHeading(templateRow, panel.transform, page, "Mounts");
            var columns = new GameObject("GK2DonkeyAPI mounts panel columns", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            columns.transform.SetParent(panel.transform, false);
            var ch = columns.GetComponent<HorizontalLayoutGroup>();
            ch.spacing = 32f;
            ch.childAlignment = TextAnchor.UpperCenter;
            ch.childControlWidth = ch.childControlHeight = false;
            ch.childForceExpandWidth = ch.childForceExpandHeight = false;
            var cf = columns.GetComponent<ContentSizeFitter>();
            cf.horizontalFit = cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listColumn = Column(columns.transform, "list").gameObject;
            rightColumn = Column(columns.transform, "right").gameObject;

            // the right side: the picture in a slot of fixed size, then the words, then the rows
            pictureSlot = new GameObject("GK2DonkeyAPI mounts panel picture slot", typeof(RectTransform), typeof(LayoutElement));
            pictureSlot.transform.SetParent(rightColumn.transform, false);
            var slotLe = pictureSlot.GetComponent<LayoutElement>();
            slotLe.preferredWidth = PictureSlotW;
            slotLe.preferredHeight = PictureSlotH;
            ((RectTransform)pictureSlot.transform).sizeDelta = new Vector2(PictureSlotW, PictureSlotH);
            picture = new GameObject("GK2DonkeyAPI mounts panel picture", typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(pictureSlot.transform, false);
            var prt = (RectTransform)picture.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            pictureImage = picture.GetComponent<RawImage>();
            pictureImage.raycastTarget = false;
            picture.SetActive(false);
            infoArea = Column(rightColumn.transform, "info").gameObject;
            rowsArea = new GameObject("GK2DonkeyAPI mounts panel rows", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowsArea.transform.SetParent(rightColumn.transform, false);
            var rh = rowsArea.GetComponent<HorizontalLayoutGroup>();
            rh.spacing = 32f;
            rh.childAlignment = TextAnchor.UpperCenter;
            rh.childControlWidth = rh.childControlHeight = false;
            rh.childForceExpandWidth = rh.childForceExpandHeight = false;
            var rf = rowsArea.GetComponent<ContentSizeFitter>();
            rf.horizontalFit = rf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var buttons = new GameObject("GK2DonkeyAPI mounts panel buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            buttons.transform.SetParent(panel.transform, false);
            var hl = buttons.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 16f;
            hl.childControlWidth = hl.childControlHeight = false;
            var bf = buttons.GetComponent<ContentSizeFitter>();
            bf.horizontalFit = bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Button(dialogTemplate, buttons.transform, "Done", () => Close(keep: true));
            Button(dialogTemplate, buttons.transform, "Cancel", () => Close(keep: false));

            entries = Entries();
            pictureOff = false;
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Kinds.KindsChanged -= OnRegistryChanged;
            Mounts.Looks.SkinsChanged += OnRegistryChanged;
            Mounts.Kinds.KindsChanged += OnRegistryChanged;
            string chosen = page.MountChosen ?? MountKinds.DonkeyId;
            int at = entries.FindIndex(e => e.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            listPage = at < 0 ? 0 : at / ListPageSize;
            BuildList();
            BuildRight();
            panel.SetActive(true);
            FitToScreen();
            Plugin.Log.LogInfo($"Settings: the Mounts panel of {page.Title} is open ({entries.Count} mount(s), {rows.Count} rows for {chosen}{(panel.transform.localScale.x < 1f ? $", scaled to {panel.transform.localScale.x:0.##} to fit the screen" : "")}).");
        }

        // The list: one button per mount of the current page of the list, the chosen one full, the others dimmed;
        // Up and Down when the list is longer than a page.
        private static void BuildList()
        {
            foreach (Transform c in listColumn.transform) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); } // off before the deferred Destroy: the layout measures the new ones alone
            listMarks.Clear();
            int pages = Math.Max(1, (entries.Count + ListPageSize - 1) / ListPageSize);
            listPage = Mathf.Clamp(listPage, 0, pages - 1);
            if (pages > 1) Button(dialogTemplate, listColumn.transform, listPage > 0 ? "Up" : "", () => { if (listPage > 0) { listPage--; Plugin.NextFrame(Repage); } }); // a frame later: the clicked button is among the ones rebuilt
            for (int i = listPage * ListPageSize; i < Math.Min(entries.Count, (listPage + 1) * ListPageSize); i++)
            {
                Entry e = entries[i];
                string id = e.Id;
                GameObject go = Button(dialogTemplate, listColumn.transform, e.Label, () => Select(id));
                listMarks.Add((id, go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>())); // the copied button may carry one
            }
            if (pages > 1) Button(dialogTemplate, listColumn.transform, listPage < pages - 1 ? "Down" : "", () => { if (listPage < pages - 1) { listPage++; Plugin.NextFrame(Repage); } });
            MarkList();
        }

        private static void Repage()
        {
            if (panel == null || listColumn == null) return;
            BuildList();
            FitToScreen();
        }

        private static void MarkList()
        {
            string chosen = page?.MountChosen ?? MountKinds.DonkeyId;
            foreach ((string id, CanvasGroup g) in listMarks)
            {
                if (g != null) g.alpha = id.Equals(chosen, StringComparison.OrdinalIgnoreCase) ? 1f : 0.55f;
            }
        }

        // A mount chosen in the list: the page's Mount row takes it (its apply heals the skin and sets the companion,
        // and every per-mount row's condition flips with it), then the right side is built again for it.
        private static void Select(string id)
        {
            if (page == null) return;
            page.SelectMount(id);
            BuildRight();
            MarkList();
            FitToScreen();
        }

        private static void BuildRight()
        {
            foreach (Transform c in infoArea.transform) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            foreach (Transform c in rowsArea.transform) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
            rows.Clear();
            string chosen = page.MountChosen ?? MountKinds.DonkeyId;
            builtFor = chosen;
            Entry e = entries.Find(x => x.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            if (e != null)
            {
                SettingsRows.MakeHeading(templateRow, infoArea.transform, page, e.Label);
                Texture2D portrait = null;
                try { portrait = Looks.PortraitOfKind(Kinds.Find(chosen)); } catch (Exception ex) { Plugin.Log.LogWarning($"Settings: the portrait of {chosen} could not be read: {ex.Message}"); }
                if (portrait != null) Portrait(portrait);
                foreach (string line in Wrap(e.Description)) Line(line);
                foreach (string line in e.Lines) foreach (string part in Wrap(line)) Line(part);
            }
            else
            {
                // the chosen mount is not in the list: its mod or pack is gone, or its every skin is set aside (a game-source
                // skin whose frames the game lacks); the choice is kept, the donkey comes instead, and the panel says so
                Kinds.KindDef k = Kinds.Find(chosen);
                string why = null;
                if (k != null) foreach (Looks.SkinDef sd in Kinds.SkinsOf(k)) if (sd.Unavailable != null) { why = sd.Unavailable; break; }
                SettingsRows.MakeHeading(templateRow, infoArea.transform, page, k?.Label ?? chosen);
                string said = k == null ? "Not installed: the mod or pack that had this mount is gone. Your choice is kept; the donkey comes instead."
                    : why != null ? "Set aside: the game no longer has the pictures this mount's skin needs (the log names them). Your choice is kept; the donkey comes instead."
                    : "It has no skin to wear. Your choice is kept; the donkey comes instead.";
                foreach (string line in Wrap(said)) Line(line);
            }
            // the page's look rows that show for this mount, in groups as on the page (a section, or a heading inside
            // one with the rows after it), dealt into two columns by their counts
            var sections = new List<(string Name, List<MountSettingsPage.Row> Rows)>();
            int total = 0;
            string heading = null, headingSection = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (!r.IsLook) continue;
                if (r.Kind == MountSettingsPage.Kind.Note)
                {
                    heading = r.Label;
                    headingSection = r.ShownSection;
                    continue;
                }
                if (r.Shown != null && !r.Shown()) continue; // another mount's row
                if (page.IsMountRow(r)) continue; // the list is the picker
                string name = heading != null && r.ShownSection == headingSection ? heading : r.ShownSection;
                int at = sections.FindIndex(s => s.Name == name);
                if (at < 0)
                {
                    sections.Add((name, new List<MountSettingsPage.Row>()));
                    at = sections.Count - 1;
                }
                sections[at].Rows.Add(r);
                total++;
            }
            Transform left = Column(rowsArea.transform, "rows left"), right = sections.Count > 1 ? Column(rowsArea.transform, "rows right") : null;
            int inLeft = 0, half = (total + 1) / 2;
            bool rightStarted = false;
            for (int s = 0; s < sections.Count; s++)
            {
                int shown = sections[s].Rows.Count;
                bool toLeft = right == null || (!rightStarted && s < sections.Count - 1 && Math.Abs(inLeft + shown - half) <= Math.Abs(inLeft - half));
                Transform column = toLeft ? left : right;
                if (toLeft) inLeft += shown; else rightStarted = true;
                SettingsRows.MakeHeading(templateRow, column, page, sections[s].Name);
                foreach (MountSettingsPage.Row r in sections[s].Rows)
                {
                    GameObject go = SettingsRows.MakeRow(templateRow, column, r, page, out Action refresh);
                    go.SetActive(true); // a copied row starts switched off (SettingsRows.Copy)
                    CanvasGroup group = r.Enabled != null || (r.Shown == null && r.Visible != null) ? go.AddComponent<CanvasGroup>() : null;
                    rows.Add((r, go, refresh, group));
                }
            }
            if (right != null && right.childCount == 0)
            {
                UnityEngine.Object.Destroy(right.gameObject);
            }
            Refresh();
        }

        private static void Line(string text)
        {
            GameObject go = SettingsRows.MakeHeading(templateRow, infoArea.transform, page, text);
            foreach (TextMeshProUGUI t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                t.enableAutoSizing = false; // the size set here stays (an auto-size pass would undo it)
                t.fontSize *= 0.85f; // a line of words, a size under the headings
                t.fontStyle = FontStyles.Normal;
                t.textWrappingMode = TextWrappingModes.NoWrap; // Wrap() made the lines; a second wrap inside a fixed-height row would overlap the next
                t.overflowMode = TextOverflowModes.Overflow;
            }
        }

        // The mount's portrait (a pack's PNG, or a code kind's texture) under its name: pixel art shown whole, 2x when
        // small, never past 96 px a side.
        private static void Portrait(Texture2D tex)
        {
            var go = new GameObject("GK2DonkeyAPI mounts panel portrait", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(infoArea.transform, false);
            RawImage img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            float scale = Mathf.Max(tex.width, tex.height) <= 48 ? 2f : Mathf.Min(1f, 96f / Mathf.Max(1, Mathf.Max(tex.width, tex.height)));
            ((RectTransform)go.transform).sizeDelta = new Vector2(Mathf.Round(tex.width * scale), Mathf.Round(tex.height * scale));
        }

        // Words wrapped into lines of about WrapAt characters (a copied label row doesn't wrap by itself).
        private static IEnumerable<string> Wrap(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var line = new System.Text.StringBuilder();
            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > WrapAt)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) yield return line.ToString();
        }

        private static Transform Column(Transform parent, string name)
        {
            var col = new GameObject("GK2DonkeyAPI mounts panel " + name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            col.transform.SetParent(parent, false);
            var vl = col.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 4f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = false;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var f = col.GetComponent<ContentSizeFitter>();
            f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return col.transform;
        }

        // The whole panel scaled down when it would reach past the screen (the game's canvas), in width or height,
        // never cut.
        private static bool refitQueued, pictureOff;

        private static void FitToScreen()
        {
            FitNow();
            if (refitQueued) return;
            refitQueued = true;
            Plugin.NextFrame(() =>
            {
                refitQueued = false;
                if (panel != null && panel.activeSelf) FitNow(); // the nested fitters have had a layout pass of their own by now
            });
        }

        // The self-test's view of the panel ("settings: the Mounts panel opens...").
        internal static int TestRows => rows.Count;
        internal static int TestEntries => entries.Count;
        internal static string TestBuiltFor => builtFor;
        internal static void TestSelect(string id) => Select(id);
        internal static void TestClose(bool keep) => Close(keep);
        internal static (float W, float H, float Scale, float RoomW, float RoomH) TestMeasure()
        {
            if (panel == null) return default;
            var rt = (RectTransform)panel.transform;
            var canvasRt = panel.transform.parent as RectTransform;
            return (rt.rect.width, rt.rect.height, panel.transform.localScale.x, canvasRt != null ? canvasRt.rect.width - 2f * EdgeGap : 0f, canvasRt != null ? canvasRt.rect.height - 2f * EdgeGap : 0f);
        }

        private static void FitNow()
        {
            if (panel == null) return;
            var rt = (RectTransform)panel.transform;
            rt.RefreshContentFitter();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            var canvasRt = panel.transform.parent as RectTransform;
            if (canvasRt == null) return;
            float roomH = canvasRt.rect.height - 2f * EdgeGap, roomW = canvasRt.rect.width - 2f * EdgeGap;
            float scale = 1f;
            if (rt.rect.height > roomH && rt.rect.height > 0f) scale = Mathf.Min(scale, roomH / rt.rect.height);
            if (rt.rect.width > roomW && rt.rect.width > 0f) scale = Mathf.Min(scale, roomW / rt.rect.width);
            scale = Mathf.Max(0.5f, scale);
            panel.transform.localScale = new Vector3(scale, scale, 1f);
            Plugin.Log.LogInfo($"Settings: the Mounts panel measures {rt.rect.width:0}x{rt.rect.height:0} (room {roomW:0}x{roomH:0}): scale {scale:0.##}."); // measure: the second line (a frame later) must match the first, else the nested layout settles late
        }

        private static GameObject Button(UIDialogWindowButton template, Transform parent, string text, Action onClick)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = "GK2DonkeyAPI " + text;
            foreach (LocalizedLabel l in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                UnityEngine.Object.DestroyImmediate(l);
            }
            foreach (TextMeshProUGUI t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                t.text = text;
            }
            LazyButton b = go.GetComponentInChildren<LazyButton>(true);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => onClick());
            go.SetActive(true);
            return go;
        }

        // After a change: every row's value (one row's change can show another), and the rows that go with a preset
        // greyed or live; the layout never moves.
        internal static void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            string chosen = page?.MountChosen ?? MountKinds.DonkeyId;
            if (!chosen.Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
            {
                BuildRight(); // the mount changed under the panel (the cfg, a key): the right side follows
                MarkList();
                FitToScreen();
                return;
            }
            foreach (var (row, go, refresh, group) in rows)
            {
                refresh?.Invoke();
                Func<bool> enabled = row.Enabled ?? (row.Shown == null ? row.Visible : null);
                bool on = enabled == null || enabled();
                if (group != null)
                {
                    group.alpha = on ? 1f : 0.4f;
                    group.interactable = on;
                    group.blocksRaycasts = on;
                    foreach (GamepadNavigationItem n in go.GetComponentsInChildren<GamepadNavigationItem>(true))
                    {
                        n.enabled = on;
                    }
                }
            }
            MarkList();
        }

        internal static void Tick()
        {
            if (panel != null && (!Mount.IsGameReady || !panel.activeSelf))
            {
                Close(keep: true);
                return;
            }
            if (panel == null || Time.unscaledTime < nextPicture)
            {
                return;
            }
            nextPicture = Time.unscaledTime + 0.2f;
            if (page != null && !(page.MountChosen ?? MountKinds.DonkeyId).Equals(builtFor ?? "", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Refresh(); // the mount changed under the panel without a row click (the cfg): the right side follows
                }
                catch (Exception e)
                {
                    // never out of here: this runs inside Mount.Tick, whose catch dismounts the rider
                    Plugin.Log.LogWarning("Settings: the Mounts panel could not follow the mount change (" + e.Message + "); closed.");
                    Close(keep: true);
                    return;
                }
            }
            if (pictureOff) return;
            try
            {
                Mount.Instance paper = LookPreview.PaperOf(page);
                Texture pic = PreviewStage.Picture(paper);
                if (pic == null && PreviewStage.Failed)
                {
                    pictureTex = paper != null ? Colors.BuildPreview(paper, pictureTex) : null;
                    pic = pictureTex;
                }
                if (pic != null)
                {
                    // as large as the slot allows, up to PictureScale, centred in it: the slot's size never changes
                    pictureImage.texture = pic;
                    Vector2 art = PreviewStage.ArtSize(pic);
                    float k = Mathf.Min(PictureScale, PictureSlotW / Mathf.Max(1f, art.x), PictureSlotH / Mathf.Max(1f, art.y));
                    ((RectTransform)picture.transform).sizeDelta = new Vector2(art.x * k, art.y * k);
                }
                picture.SetActive(pic != null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Settings: the Mounts panel's picture is off (" + e.Message + ").");
                if (picture != null)
                {
                    picture.SetActive(false);
                }
                pictureOff = true; // the picture alone: the panel keeps following the mount
            }
        }

        // A skin or mount registered, reloaded or withdrawn while the panel is open (the Mounts tab, the pack watch): the
        // list and the right side are built again a frame later (the registry may be mid-change when the event comes).
        private static bool registryQueued;

        private static void OnRegistryChanged()
        {
            if (!IsOpen || registryQueued) return;
            registryQueued = true;
            Plugin.NextFrame(() =>
            {
                registryQueued = false;
                if (!IsOpen) return;
                try
                {
                    entries = Entries();
                    BuildList();
                    BuildRight();
                    FitToScreen();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Settings: the Mounts panel could not follow a change of the mounts (" + e.Message + "); closed.");
                    Close(keep: true);
                }
            });
        }

        private static void Close(bool keep)
        {
            Mounts.Looks.SkinsChanged -= OnRegistryChanged;
            Mounts.Kinds.KindsChanged -= OnRegistryChanged;
            try
            {
                if (!keep)
                {
                    foreach (var kv in before)
                    {
                        kv.Key.E.BoxedValue = kv.Value; // its SettingChanged applies it
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings: putting the mount and its colours back failed: " + e.Message);
            }
            try
            {
                page?.RefreshMenu?.Invoke(); // the Mods menu re-reads its conditions: a row the chosen mount pins or leaves out
            }
            catch
            {
            }
            before.Clear();
            rows.Clear();
            listMarks.Clear();
            builtFor = null;
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
                panel = null;
            }
            if (pictureTex != null)
            {
                UnityEngine.Object.Destroy(pictureTex);
                pictureTex = null;
            }
            picture = null;
            pictureSlot = null;
            listColumn = rightColumn = infoArea = rowsArea = null;
            templateRow = null;
            dialogTemplate = null;
            nextPicture = 0f;
            page = null;
        }
    }
}
