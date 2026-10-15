using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Corrections on top of the size-based cargo packer, kept in a small text file (cargo-layout.cfg):
    //
    //   [item <id>]   applies to every item whose measured box matches <id>'s box within the size tolerance, i.e. to
    //                 the whole size class (stone's entry also covers iron ore; box_appliances_1's covers 15 crates).
    //                 along/up/across: offsets when the item is at the bottom or beside another item, and when
    //                 alone unless it has its own alone offsets.
    //                 alone_along/alone_up/alone_across: offsets when it is the only item on the cart (optional;
    //                 added 2026-09-30, because a correction tuned for two logs side by side also moved one log).
    //                 top_along/top_up/top_across: offsets when it is stacked on top of another item.
    //   [pair <a>+<b>] applies to that pair of size classes in either order. Two RELATIONS between the two, each
    //                 tunable on its own (docs/cargo-layout-layers.md, 2026-10-10; before that a pair held one):
    //                 beside_a_*/beside_b_*: the two lying beside each other (side or along), the exact place of a
    //                 and of b (footprint centre along and across, bottom height above the layer's floor);
    //                 top_a_*/top_b_*: b on top of a, both places as a two-item cart shows them; inside a bigger
    //                 load b is placed relative to a (b minus a), wherever a is.
    //                 mode = auto|side|along|stack: which relation a two-item cart shows (auto: the packer decides).
    //                 A tuned relation is frozen: class entries, alone offsets, gap, overhang, nestle, the layer
    //                 shift and the load order no longer change it; only the floor (CargoBedDepth) moves it.
    //                 Positions belong to a and b by id, not to "first" and "second", so which item was picked up
    //                 first does not matter.
    //   [onpair <item> on <a>+<b>] the alone-on-a-pair offset of <item>'s size class, only when it lies alone on top
    //                 of a pair of <a> and <b> (size classes, either order): along/up/across, all three written. It
    //                 replaces the class's onpair_* there, so one crate can sit differently on two crates than on two
    //                 bodies (asked 2026-10-15); everywhere else the class's onpair_* applies.
    //   Lookup is deterministic: an exact id (or id pair) wins over a size-class match; among class matches the
    //   first in the file wins, and a second one is reported once in the log (it would never apply).
    //
    // The file ships embedded in the DLL (the repo copy). On the machine with the sources (Defaults.RepoFile) the mod
    // reads the repo file itself and the panel's corrections are written back to it as you edit, so there is one copy
    // and a rebuild ships it; elsewhere the embedded copy is used and edits last for the session. Entries are keyed by item
    // id and resolved through the items' measured boxes at runtime, so a game update that adds an item of a known
    // size is covered automatically, and an item whose mesh changes size simply stops matching (the packer's
    // defaults then apply, and the sheet shows it).
    internal static class CargoLayout
    {
        public const float SizeTolerance = 0.05f;
        public const string RepoFileName = "cargo-layout.cfg";

        // The cargo sheet's hold on how a pair is laid out: "beside" (the pair's own mode, or the packer's choice) or
        // "stack" (on top), so every pair is pictured in both relations; null normally. A rig's isolation
        // (Cargo.Rig.Isolate) does the same for one cart.
        public static string ForcedRelation;

        // True on the machine with the sources: edits are saved to the repo file.
        public static bool SavesToRepo => Defaults.RepoFile(RepoFileName) != null;

        private static bool dirty;
        private static float lastChange;

        // A correction changed on the panel; written to the repo file shortly after the last change (SaveIfDue).
        // Counts every change to the entries (the panel's edits, a reload), so what is computed from them (the coverage
        // report) knows when to redo it.
        public static int Version { get; private set; }

        public static void MarkChanged()
        {
            Version++;
            dirty = true;
            lastChange = UnityEngine.Time.unscaledTime;
        }

        public static string LastSaveNote { get; private set; } = "";

        // ------------------------------------------------------------------ undo, redo, compare (docs/cargo-tuning-tools.md)
        // Snapshots are the file's text. The panel calls Checkpoint after a finished edit (never mid-drag); Undo / Redo
        // put a snapshot back and save it like an edit. Compare shows the layout as it was when the session began, with
        // saving held off and editing locked, and puts the current one back when it ends: the file always keeps the
        // current layout, whatever happens while comparing.
        private static readonly List<string> undoStack = new List<string>(), redoStack = new List<string>();
        private static string lastText, sessionStart, compareSaved;
        private const int UndoDepth = 200;

        public static bool CanUndo => undoStack.Count > 0 && !Comparing;
        public static bool CanRedo => redoStack.Count > 0 && !Comparing;
        public static int UndoCount => undoStack.Count;
        public static bool Comparing => compareSaved != null;
        public static bool DiffersFromSessionStart => sessionStart != null && (compareSaved ?? lastText ?? ToText()) != sessionStart;

        // After a finished edit: the state before it goes on the undo stack. merge: a nudge (a key press); nudges that
        // follow each other within two seconds are one undo step, not one per press.
        private static float lastMergeAt = -10f;

        public static void Checkpoint(bool merge = false)
        {
            if (Comparing) return;
            string now = ToText();
            if (lastText == null)
            {
                lastText = now;
                return;
            }
            if (now == lastText) return;
            float t = UnityEngine.Time.unscaledTime;
            bool merging = merge && t - lastMergeAt < 2f && undoStack.Count > 0;
            lastMergeAt = merge ? t : -10f;
            if (merging)
            {
                lastText = now; // the step on the stack already holds the state before the first nudge
                return;
            }
            undoStack.Add(lastText);
            if (undoStack.Count > UndoDepth) undoStack.RemoveAt(0);
            redoStack.Clear();
            lastText = now;
        }

        public static bool Undo()
        {
            if (!CanUndo) return false;
            redoStack.Add(lastText);
            lastText = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            Restore(lastText, save: true);
            return true;
        }

        public static bool Redo()
        {
            if (!CanRedo) return false;
            undoStack.Add(lastText);
            lastText = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            Restore(lastText, save: true);
            return true;
        }

        // Shows the session's starting layout (on) or puts the current one back (off). Never saved while on.
        public static void Compare(bool on)
        {
            if (on == Comparing || sessionStart == null) return;
            if (on)
            {
                SaveIfDue(now: true); // the current layout is on disk before anything else is shown
                compareSaved = ToText();
                Restore(sessionStart, save: false);
            }
            else
            {
                string back = compareSaved;
                compareSaved = null;
                Restore(back, save: false);
            }
        }

        private static void Restore(string text, bool save)
        {
            Items.Clear();
            itemOrder.Clear();
            Pairs.Clear();
            OnPairs.Clear();
            Parse(text);
            Version++;
            if (save)
            {
                MarkChanged();
            }
        }

        public static void SaveIfDue(bool now = false)
        {
            if (!dirty || Comparing || (!now && UnityEngine.Time.unscaledTime - lastChange < 0.75f))
            {
                return;
            }
            dirty = false;
            string repo = Defaults.RepoFile(RepoFileName);
            if (repo == null)
            {
                return;
            }
            try
            {
                Save(repo);
                LastSaveNote = "Saved to the repo's cargo-layout.cfg at " + DateTime.Now.ToString("HH:mm:ss") + ".";
            }
            catch (Exception e)
            {
                LastSaveNote = "Saving the layout failed: " + e.Message;
                Plugin.Log.LogWarning("Cargo layout: " + LastSaveNote);
            }
        }

        public class ItemEntry
        {
            public string Id;
            public Vector3 Base;   // along, up, across
            public Vector3 Top;    // when stacked on top
            public bool HasAlone;  // alone on the cart: Alone instead of Base
            public Vector3 Alone;
            public Vector3 OnPair; // alone on top of a pair (a lone item on an upper layer, placed over the middle of the two below): an extra offset, in that layer only
            public bool HasOnPair; // alone on a pair accepted or tuned (all three onpair_* written, zeros included)
            public bool OnPairTuned => HasOnPair || OnPair != Vector3.zero;
            public bool IsEmpty => Base == Vector3.zero && Top == Vector3.zero && OnPair == Vector3.zero && !HasAlone && !HasOnPair;
        }

        public class PairEntry
        {
            public string A, B;
            public string Mode = "auto"; // auto | side (across) | along (end to end) | stack: what a two-item cart shows
            // The two relations. Beside: both places in the layer's frame (along, height above the layer floor,
            // across). On top: a below, b above, both places as a two-item cart shows them (b is applied relative to a).
            public bool BesideTuned, TopTuned;
            public Vector3 BesideA, BesideB, TopA, TopB;
            public bool IsEmpty => Mode == "auto" && !BesideTuned && !TopTuned;

            public static bool IsStack(string mode) => mode == "stack";

            // The relation a two-item cart in this mode uses.
            public bool TunedFor(string mode) => IsStack(mode) ? TopTuned : BesideTuned;
            public Vector3 PosAFor(string mode) => IsStack(mode) ? TopA : BesideA;
            public Vector3 PosBFor(string mode) => IsStack(mode) ? TopB : BesideB;

            public void SetFor(string mode, Vector3 a, Vector3 b)
            {
                if (IsStack(mode)) { TopTuned = true; TopA = a; TopB = b; }
                else { BesideTuned = true; BesideA = a; BesideB = b; }
            }

            public void SetPos(string mode, bool isA, Vector3 v)
            {
                if (IsStack(mode)) { TopTuned = true; if (isA) TopA = v; else TopB = v; }
                else { BesideTuned = true; if (isA) BesideA = v; else BesideB = v; }
            }

            public void ClearFor(string mode)
            {
                if (IsStack(mode)) { TopTuned = false; TopA = TopB = Vector3.zero; }
                else { BesideTuned = false; BesideA = BesideB = Vector3.zero; }
            }

        }

        // An item's alone-on-a-pair offset on one pair only ([onpair <item> on <a>+<b>]); its presence is the choice.
        public class OnPairEntry
        {
            public string Item, A, B;
            public Vector3 Offset; // along, up, across: in place of the item class's OnPair on this pair
        }

        public static readonly Dictionary<string, ItemEntry> Items = new Dictionary<string, ItemEntry>(StringComparer.Ordinal);
        public static readonly List<PairEntry> Pairs = new List<PairEntry>();
        public static readonly List<OnPairEntry> OnPairs = new List<OnPairEntry>();
        public static string Source { get; private set; } = "(not loaded)";

        // Boxes by item id, measured from the keeper's overhead mesh set; filled lazily, once per session.
        private static readonly Dictionary<string, Vector3> boxes = new Dictionary<string, Vector3>(StringComparer.Ordinal);

        public static void Load()
        {
            LoadFile();
            // the session's starting point for undo and compare
            undoStack.Clear();
            redoStack.Clear();
            compareSaved = null;
            sessionStart = lastText = ToText();
        }

        private static void LoadFile()
        {
            Version++;
            Items.Clear();
            itemOrder.Clear();
            Pairs.Clear();
            OnPairs.Clear();
            dirty = false;
            string repo = Defaults.RepoFile(RepoFileName);
            try
            {
                if (repo != null)
                {
                    Parse(File.ReadAllText(repo));
                    Source = repo;
                    return;
                }
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GK2DonkeyAPI.cargo-layout.cfg"))
                {
                    if (s != null)
                    {
                        using (var r = new StreamReader(s))
                        {
                            Parse(r.ReadToEnd());
                        }
                        Source = "embedded";
                        return;
                    }
                }
                Source = "(no file, packer defaults)";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cargo layout: reading the layout file failed, packer defaults in use: " + e.Message);
                Source = "(read failed)";
            }
        }

        private static void Parse(string text)
        {
            ItemEntry item = null;
            PairEntry pair = null;
            OnPairEntry onPair = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    line = line.Substring(0, hash).Trim();
                }
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string head = line.Substring(1, line.Length - 2).Trim();
                    item = null;
                    pair = null;
                    onPair = null;
                    if (head.StartsWith("onpair ", StringComparison.OrdinalIgnoreCase))
                    {
                        // [onpair <item> on <a>+<b>]
                        string rest = head.Substring(7).Trim();
                        int on = rest.IndexOf(" on ", StringComparison.OrdinalIgnoreCase);
                        string[] ab = on < 0 ? new string[0] : rest.Substring(on + 4).Split('+');
                        if (on > 0 && ab.Length == 2)
                        {
                            onPair = new OnPairEntry { Item = rest.Substring(0, on).Trim(), A = ab[0].Trim(), B = ab[1].Trim() };
                            OnPairs.Add(onPair);
                        }
                        else
                        {
                            WarnOnce($"[{head}]: not of the form [onpair <item> on <a>+<b>]; skipped.");
                        }
                    }
                    else if (head.StartsWith("item ", StringComparison.OrdinalIgnoreCase))
                    {
                        item = new ItemEntry { Id = head.Substring(5).Trim() };
                        if (!Items.ContainsKey(item.Id)) itemOrder.Add(item.Id);
                        Items[item.Id] = item;
                    }
                    else if (head.StartsWith("pair ", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] ab = head.Substring(5).Split('+');
                        if (ab.Length == 2)
                        {
                            pair = new PairEntry { A = ab[0].Trim(), B = ab[1].Trim() };
                            Pairs.Add(pair);
                        }
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
                if (onPair != null)
                {
                    switch (key)
                    {
                        case "along": onPair.Offset.x = f; break;
                        case "up": onPair.Offset.y = f; break;
                        case "across": onPair.Offset.z = f; break;
                    }
                }
                else if (item != null)
                {
                    switch (key)
                    {
                        case "along": item.Base.x = f; break;
                        case "up": item.Base.y = f; break;
                        case "across": item.Base.z = f; break;
                        case "top_along": item.Top.x = f; break;
                        case "top_up": item.Top.y = f; break;
                        case "top_across": item.Top.z = f; break;
                        case "alone_along": item.Alone.x = f; item.HasAlone = true; break;
                        case "alone_up": item.Alone.y = f; item.HasAlone = true; break;
                        case "alone_across": item.Alone.z = f; item.HasAlone = true; break;
                        case "onpair_along": item.OnPair.x = f; item.HasOnPair = true; break;
                        case "onpair_up": item.OnPair.y = f; item.HasOnPair = true; break;
                        case "onpair_across": item.OnPair.z = f; item.HasOnPair = true; break;
                    }
                }
                else if (pair != null)
                {
                    switch (key)
                    {
                        case "mode": pair.Mode = val.ToLowerInvariant(); break;
                        case "beside_a_along": pair.BesideA.x = f; pair.BesideTuned = true; break;
                        case "beside_a_up": pair.BesideA.y = f; pair.BesideTuned = true; break;
                        case "beside_a_across": pair.BesideA.z = f; pair.BesideTuned = true; break;
                        case "beside_b_along": pair.BesideB.x = f; pair.BesideTuned = true; break;
                        case "beside_b_up": pair.BesideB.y = f; pair.BesideTuned = true; break;
                        case "beside_b_across": pair.BesideB.z = f; pair.BesideTuned = true; break;
                        case "top_a_along": pair.TopA.x = f; pair.TopTuned = true; break;
                        case "top_a_up": pair.TopA.y = f; pair.TopTuned = true; break;
                        case "top_a_across": pair.TopA.z = f; pair.TopTuned = true; break;
                        case "top_b_along": pair.TopB.x = f; pair.TopTuned = true; break;
                        case "top_b_up": pair.TopB.y = f; pair.TopTuned = true; break;
                        case "top_b_across": pair.TopB.z = f; pair.TopTuned = true; break;
                    }
                }
            }
        }

        // Writes the current entries (empty ones dropped) to path.
        private static void Save(string path)
        {
            File.WriteAllText(path, ToText());
            Source = path;
        }

        // The file's text for the current entries (also the undo snapshots).
        internal static string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# GK2 Donkey API cargo layout: corrections on top of the size-based cargo packer (see CargoLayout.cs).");
            sb.AppendLine("# Embedded into the DLL at build time. The tuning panel writes this file as you edit; rebuild to ship it.");
            sb.AppendLine("# [item <id>] applies to every item of the same size class (measured box within " + SizeTolerance.ToString("0.00", CultureInfo.InvariantCulture) + ").");
            sb.AppendLine("#   along / up / across: at the bottom or beside another item (and alone, without alone_*); top_along / top_up / top_across: stacked on top.");
            sb.AppendLine("#   alone_along / alone_up / alone_across: the only item on the cart (all three written when set, zeros included).");
            sb.AppendLine("#   onpair_along / onpair_up / onpair_across: an extra offset when it lies alone on top of a pair (a lone item on an upper layer; all three written when set, zeros included).");
            sb.AppendLine("# [pair <a>+<b>] applies to that pair of size classes in either order. Two relations, each tunable on its own:");
            sb.AppendLine("#   beside_a_* / beside_b_*: the two lying beside each other (side or along), the exact place of a and b (along, height above the layer's floor, across);");
            sb.AppendLine("#   top_a_* / top_b_*: b on top of a, both places as a two-item cart shows them; inside a bigger load b is placed relative to a.");
            sb.AppendLine("#   mode = auto | side (across the bed) | along (end to end) | stack: which relation a two-item cart shows. A tuned relation is frozen against everything but the floor.");
            sb.AppendLine("# [onpair <item> on <a>+<b>]: along / up / across in place of the item's onpair_* when it lies alone on top of a pair of <a> and <b> (size classes, either order; all three written).");
            sb.AppendLine("# Exact ids win over size-class matches; among class matches the first in this file wins.");
            sb.AppendLine("#");
            sb.AppendLine("# Size classes at the time of writing (27 items): log (wood); flour sack (box_flour); tall crate (box_beer,");
            sb.AppendLine("# box_clothes_1/2, box_fabric, box_vine); body (body_corpse, body_wild_zombie); low crate (15 box_* incl. preserves);");
            sb.AppendLine("# slab (stone, iron_ore_2h); no mesh (marble -> shown with the slab mesh, box_engineering_mechanism -> generic crate);");
            sb.AppendLine("# zombie (body_zombie: the game's lying zombie, one body for all ten heads; a class of its own, matched by id only);");
            sb.AppendLine("# chest (chest_rough / chest / chest_good riding on the cart, CartChest.cs: a class each, matched by id only; alone_* places it).");
            sb.AppendLine("version = 2");
            foreach (string id in itemOrder)
            {
                ItemEntry e = Items[id];
                if (e.IsEmpty)
                {
                    continue;
                }
                sb.AppendLine().AppendLine("[item " + id + "]");
                Line(sb, "along", e.Base.x); Line(sb, "up", e.Base.y); Line(sb, "across", e.Base.z);
                Line(sb, "top_along", e.Top.x); Line(sb, "top_up", e.Top.y); Line(sb, "top_across", e.Top.z);
                if (e.OnPairTuned)
                {
                    // all three, zeros too: their presence is what says "tuned" (accepted as it is: all zero)
                    sb.AppendLine("onpair_along = " + e.OnPair.x.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("onpair_up = " + e.OnPair.y.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("onpair_across = " + e.OnPair.z.ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (e.HasAlone)
                {
                    // all three, zeros too: their presence is what says "own alone offsets"
                    sb.AppendLine("alone_along = " + e.Alone.x.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("alone_up = " + e.Alone.y.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("alone_across = " + e.Alone.z.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
            foreach (OnPairEntry o in OnPairs)
            {
                // all three, zeros too: the entry's presence is the choice
                sb.AppendLine().AppendLine("[onpair " + o.Item + " on " + o.A + "+" + o.B + "]");
                sb.AppendLine("along = " + o.Offset.x.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("up = " + o.Offset.y.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("across = " + o.Offset.z.ToString("0.###", CultureInfo.InvariantCulture));
            }
            foreach (PairEntry p in Pairs)
            {
                if (p.IsEmpty)
                {
                    continue;
                }
                sb.AppendLine().AppendLine("[pair " + p.A + "+" + p.B + "]");
                sb.AppendLine("mode = " + p.Mode);
                // all six of a relation, zeros too: their presence is what makes it tuned
                if (p.BesideTuned)
                {
                    foreach (var kv in new (string, float)[] { ("beside_a_along", p.BesideA.x), ("beside_a_up", p.BesideA.y), ("beside_a_across", p.BesideA.z), ("beside_b_along", p.BesideB.x), ("beside_b_up", p.BesideB.y), ("beside_b_across", p.BesideB.z) })
                    {
                        sb.AppendLine(kv.Item1 + " = " + kv.Item2.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
                if (p.TopTuned)
                {
                    foreach (var kv in new (string, float)[] { ("top_a_along", p.TopA.x), ("top_a_up", p.TopA.y), ("top_a_across", p.TopA.z), ("top_b_along", p.TopB.x), ("top_b_up", p.TopB.y), ("top_b_across", p.TopB.z) })
                    {
                        sb.AppendLine(kv.Item1 + " = " + kv.Item2.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
            }
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string key, float v)
        {
            if (Mathf.Abs(v) > 0.0001f)
            {
                sb.AppendLine(key + " = " + v.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        // ------------------------------------------------------------------ size classes

        public static Vector3 BoxOf(string itemId)
        {
            if (boxes.TryGetValue(itemId, out Vector3 b))
            {
                return b;
            }
            b = Vector3.zero;
            try
            {
                ItemDef def = GameBalance.Me.GetDataOrNull<ItemDef>(itemId);
                // A zombie has no overhead element of its own: its box is the game's lying zombie (one body for all ten
                // heads, identical head bounds: the asset bundles, 2026-10-04).
                Transform el = def == null ? null : Cargo.IsZombie(def) ? Cargo.ZombieDropPrefab() : Cargo.FindOverheadElement(def);
                if (el != null && Cargo.MeasureMeshes(el, el, out Bounds bb, includeInactive: true))
                {
                    b = bb.size;
                }
            }
            catch
            {
            }
            boxes[itemId] = b;
            return b;
        }

        public static bool SameClass(string a, string b)
        {
            if (a == b)
            {
                return true;
            }
            if (IsZombieId(a) || IsZombieId(b) || CartChest.Allowed.Contains(a) || CartChest.Allowed.Contains(b))
            {
                return false; // zombies and the chests are classes of their own: tuning one never moves another item, nor the other way
            }
            Vector3 d = BoxOf(a) - BoxOf(b);
            return Mathf.Abs(d.x) <= SizeTolerance && Mathf.Abs(d.y) <= SizeTolerance && Mathf.Abs(d.z) <= SizeTolerance;
        }

        private static readonly Dictionary<string, bool> zombieIds = new Dictionary<string, bool>(StringComparer.Ordinal);

        internal static bool IsZombieId(string itemId)
        {
            if (itemId == null)
            {
                return false;
            }
            if (!zombieIds.TryGetValue(itemId, out bool z))
            {
                try
                {
                    z = Cargo.IsZombie(GameBalance.Me.GetDataOrNull<ItemDef>(itemId));
                }
                catch
                {
                    z = false;
                }
                zombieIds[itemId] = z;
            }
            return z;
        }

        // The entry covering this item's size class, or null. The exact id wins over a class match.
        public static ItemEntry ForItem(string itemId)
        {
            if (Items.TryGetValue(itemId, out ItemEntry exact))
            {
                return exact;
            }
            ItemEntry found = null;
            foreach (ItemEntry e in ItemOrder)
            {
                if (!SameClass(e.Id, itemId))
                {
                    continue;
                }
                if (found == null)
                {
                    found = e;
                }
                else if (!e.IsEmpty)
                {
                    WarnOnce($"item {itemId}: both [item {found.Id}] and [item {e.Id}] cover its size class; [item {found.Id}] applies (first in the file).");
                }
            }
            return found;
        }

        // Items in file order (Dictionary order is not guaranteed): new entries go to the end.
        private static IEnumerable<ItemEntry> ItemOrder
        {
            get
            {
                foreach (string id in itemOrder)
                {
                    if (Items.TryGetValue(id, out ItemEntry e)) yield return e;
                }
            }
        }

        private static readonly List<string> itemOrder = new List<string>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        private static void WarnOnce(string text)
        {
            if (warned.Add(text))
            {
                Plugin.Log.LogWarning("Cargo layout: " + text);
            }
        }

        // For a pair that matches (a, b): whether item a is the entry's A (else its B). Both ways possible (a pair
        // of one size class, e.g. two logs): the first-placed item is A.
        public static bool FirstIsA(PairEntry p, string first, string second)
        {
            if (p.A == first && p.B == second) return true;
            if (p.A == second && p.B == first) return false;
            bool straight = SameClass(p.A, first) && SameClass(p.B, second);
            bool crossed = SameClass(p.A, second) && SameClass(p.B, first);
            return straight || !crossed;
        }

        public static PairEntry ForPair(string a, string b)
        {
            foreach (PairEntry p in Pairs)
            {
                if ((p.A == a && p.B == b) || (p.A == b && p.B == a))
                {
                    return p; // exact ids win
                }
            }
            PairEntry found = null;
            foreach (PairEntry p in Pairs)
            {
                if (!((SameClass(p.A, a) && SameClass(p.B, b)) || (SameClass(p.A, b) && SameClass(p.B, a))))
                {
                    continue;
                }
                if (found == null)
                {
                    found = p;
                }
                else if (!p.IsEmpty)
                {
                    WarnOnce($"pair {a}+{b}: both [pair {found.A}+{found.B}] and [pair {p.A}+{p.B}] cover these size classes; [pair {found.A}+{found.B}] applies (first in the file).");
                }
            }
            return found;
        }

        // The one-pair offset for item lying alone on a pair of a and b, or null (the class's OnPair applies then). Exact
        // ids win over class matches; among class matches the first in the file. same: the size-class test (the scan
        // passes its own).
        internal static OnPairEntry FindOnPair(IList<OnPairEntry> list, string item, string a, string b, Func<string, string, bool> same)
        {
            if (list == null || list.Count == 0 || item == null || a == null || b == null)
            {
                return null;
            }
            foreach (OnPairEntry o in list)
            {
                if (o.Item == item && ((o.A == a && o.B == b) || (o.A == b && o.B == a))) return o;
            }
            foreach (OnPairEntry o in list)
            {
                if (same(o.Item, item) && ((same(o.A, a) && same(o.B, b)) || (same(o.A, b) && same(o.B, a)))) return o;
            }
            return null;
        }

        public static OnPairEntry ForOnPair(string item, string a, string b) => FindOnPair(OnPairs, item, a, b, SameClass);

        // A one-pair offset for item on (a, b), starting at start (where the item lies now, so nothing jumps).
        public static OnPairEntry AddOnPair(string item, string a, string b, Vector3 start)
        {
            OnPairEntry o = ForOnPair(item, a, b);
            if (o == null)
            {
                o = new OnPairEntry { Item = item, A = a, B = b, Offset = start };
                OnPairs.Add(o);
            }
            return o;
        }

        public static void RemoveOnPair(OnPairEntry o) => OnPairs.Remove(o);

        // Entry to edit for this item: the existing class entry, or a new one keyed by this item's id.
        public static ItemEntry EditItem(string itemId)
        {
            ItemEntry e = ForItem(itemId);
            if (e == null)
            {
                e = new ItemEntry { Id = itemId };
                Items[itemId] = e;
                itemOrder.Add(itemId);
            }
            return e;
        }

        public static PairEntry EditPair(string a, string b)
        {
            PairEntry p = ForPair(a, b);
            if (p == null)
            {
                p = new PairEntry { A = a, B = b };
                Pairs.Add(p);
            }
            return p;
        }

        public static string Summary()
        {
            int items = 0, pairs = 0;
            foreach (ItemEntry e in Items.Values)
            {
                if (!e.IsEmpty)
                {
                    items++;
                }
            }
            foreach (PairEntry p in Pairs)
            {
                if (!p.IsEmpty)
                {
                    pairs++;
                }
            }
            string from = Source.EndsWith(RepoFileName, StringComparison.OrdinalIgnoreCase) && Source.IndexOf('\\') >= 0 ? "the repo's cargo-layout.cfg" : Source;
            string one = OnPairs.Count == 0 ? "" : $", {OnPairs.Count} one-pair offset{(OnPairs.Count == 1 ? "" : "s")}";
            return $"{items} item entr{(items == 1 ? "y" : "ies")}, {pairs} pair entr{(pairs == 1 ? "y" : "ies")}{one}, from {from}";
        }
    }
}
