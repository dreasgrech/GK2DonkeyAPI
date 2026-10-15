using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The review of every load (docs/cargo-review.md): the user looks at each of the 1,715 loads the cart can carry, one
    // by one in the Cargo tab, and marks it; the marks are kept in the repo's cargo-review.cfg so the review goes on
    // across sessions. Scene-free: the panel (Dev/TuningWindow.Review.cs) and the offline scan (tests/PackerScan
    // --review) both use it.
    //   A load is keyed by its classes' readable names, sorted ("corpse+log+log"): the class representatives the sheet
    //   picks are the first item of each class in the game's list and could change with a game update; the names don't.
    //   A mark of "ok" carries the FINGERPRINT of the load's tuning at the time: the stored values of every contact in
    //   the load (pair beside or top values by kind, the alone and alone-on-a-pair offsets with their "set" flags, the
    //   pair mode when two items show it, the class offsets of every class in the load) and, for an untuned contact,
    //   the fallback settings it is placed with. A later edit of any of those (a drag, a nudge, an accept, an undo)
    //   changes the fingerprint, and the load shows as "to re-check" until it is looked at again. That is what lets an
    //   ok from last week be trusted: it is an ok of exactly this tuning.
    //   The file: one line per marked load in the analysis's order (so a diff reads top to bottom by size), with the
    //   verdict, the date, the fingerprint, the reason tags and a note; a header keeps where the review stopped.
    internal static class CargoReview
    {
        internal enum Verdict { None, Ok, Wrong, Later }

        internal sealed class Mark
        {
            public string Key;
            public Verdict Verdict;
            public string When = "";
            public string Fingerprint = "";
            public string Tags = "";   // comma-separated TagNames
            public string Note = "";
        }

        internal const string FileName = "cargo-review.cfg";
        internal static readonly string[] TagNames = { "floats", "sinks", "overlaps", "off the bed", "wrong spot", "other" };

        private static readonly Dictionary<string, Mark> marks = new Dictionary<string, Mark>(StringComparer.Ordinal);
        private static bool loaded, dirty;
        private static int lastChange; // Environment.TickCount (ms): scene-free, so the offline scan can use the store

        internal static int Count => marks.Count;
        internal static string LastPositionKey { get; set; } = ""; // the load the review stopped at (header)
        internal static string LastSaveNote { get; private set; } = "";
        internal static string Source { get; private set; } = "(not loaded)";

        internal static IEnumerable<Mark> All
        {
            get
            {
                EnsureLoaded();
                return marks.Values;
            }
        }

        internal static Mark Get(string key)
        {
            EnsureLoaded();
            return key != null && marks.TryGetValue(key, out Mark m) ? m : null;
        }

        internal static void Set(string key, Verdict verdict, string fingerprint, string tags, string note)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key) || verdict == Verdict.None)
            {
                return;
            }
            marks[key] = new Mark
            {
                Key = key, Verdict = verdict, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Fingerprint = fingerprint ?? "", Tags = tags ?? "", Note = (note ?? "").Replace('|', '/').Replace('\n', ' ').Trim(),
            };
            Touch();
        }

        internal static void Clear(string key)
        {
            EnsureLoaded();
            if (key != null && marks.Remove(key))
            {
                Touch();
            }
        }

        private static void Touch()
        {
            dirty = true;
            lastChange = Environment.TickCount;
        }

        // ------------------------------------------------------------------ keys and fingerprints

        // "corpse+log+log": the readable class names of the load, sorted.
        internal static string KeyOf(CargoCoverage.Report rep, int[] classes)
        {
            var names = new List<string>(classes.Length);
            foreach (int c in classes)
            {
                names.Add(rep.Label(rep.Classes[c]));
            }
            names.Sort(StringComparer.Ordinal);
            return string.Join("+", names);
        }

        // The load's tuning, as a short hash of everything that places its items: see the class comment.
        // forItem / forPair: the layout's lookups (CargoLayout.ForItem / ForPair in the game; the scan passes its own).
        // fallbacks: the packer settings an untuned contact is placed with (CargoSheet.CurrentSignature without the
        // layout version), or null when they are not known.
        internal static string Fingerprint(CargoCoverage.Report rep, CargoCoverage.Load load,
            Func<string, CargoLayout.ItemEntry> forItem, Func<string, string, CargoLayout.PairEntry> forPair, string fallbacks)
        {
            var sb = new StringBuilder();
            bool twoItems = load.Classes.Length == 2;
            foreach (string key in load.Keys)
            {
                if (!rep.ByKey.TryGetValue(key, out CargoCoverage.Contact c))
                {
                    sb.Append(key).Append("=?;");
                    continue;
                }
                sb.Append(key).Append('=');
                switch (c.Kind)
                {
                    case "on":
                    {
                        CargoLayout.PairEntry p = forPair(c.Lower, c.Upper);
                        sb.Append(p == null ? "none" : $"{p.TopTuned}:{V(p.TopA)}:{V(p.TopB)}{(twoItems ? ":" + p.Mode : "")}");
                        break;
                    }
                    case "beside":
                    {
                        CargoLayout.PairEntry p = forPair(c.Lower, c.Upper);
                        sb.Append(p == null ? "none" : $"{p.BesideTuned}:{V(p.BesideA)}:{V(p.BesideB)}{(twoItems ? ":" + p.Mode : "")}");
                        break;
                    }
                    case "onpair":
                    {
                        CargoLayout.ItemEntry e = forItem(c.Lower);
                        sb.Append(e == null ? "none" : $"{e.HasOnPair}:{V(e.OnPair)}");
                        break;
                    }
                    default:
                    {
                        CargoLayout.ItemEntry e = forItem(c.Lower);
                        sb.Append(e == null ? "none" : $"{e.HasAlone}:{V(e.Alone)}");
                        break;
                    }
                }
                if (!c.Tuned && fallbacks != null)
                {
                    sb.Append(":fb=").Append(fallbacks);
                }
                sb.Append(';');
            }
            // the class offsets of every class in the load (the item boxes' along / up / across and the on-top ones)
            var seen = new HashSet<int>();
            foreach (int ci in load.Classes)
            {
                if (!seen.Add(ci))
                {
                    continue;
                }
                string id = rep.Classes[ci];
                CargoLayout.ItemEntry e = forItem(id);
                sb.Append("cls:").Append(id).Append('=').Append(e == null ? "none" : $"{V(e.Base)}:{V(e.Top)}").Append(';');
            }
            return Hash(sb.ToString());
        }

        private static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:0.000},{1:0.000},{2:0.000}", v.x, v.y, v.z);

        private static string Hash(string s)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(h[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ the file

        private static string RepoPath => Defaults.RepoPath(FileName);

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            string path = RepoPath;
            if (path == null)
            {
                Source = "(no sources on this machine: marks are kept for the session only)";
                return;
            }
            try
            {
                if (File.Exists(path))
                {
                    Parse(File.ReadAllText(path));
                    Source = path;
                }
                else
                {
                    Source = path + " (new)";
                }
            }
            catch (Exception e)
            {
                Source = "(read failed: " + e.Message + ")";
                Plugin.Log.LogWarning("Cargo review: reading " + path + " failed: " + e.Message);
            }
        }

        internal static void Parse(string text)
        {
            marks.Clear();
            LastPositionKey = "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string rest = line.Substring(eq + 1).Trim();
                if (key == "position")
                {
                    LastPositionKey = rest;
                    continue;
                }
                string[] f = rest.Split('|');
                for (int i = 0; i < f.Length; i++)
                {
                    f[i] = f[i].Trim();
                }
                if (f.Length == 0 || !Enum.TryParse(f[0], true, out Verdict v) || v == Verdict.None)
                {
                    continue;
                }
                marks[key] = new Mark
                {
                    Key = key, Verdict = v,
                    When = f.Length > 1 ? f[1] : "", Fingerprint = f.Length > 2 ? f[2] : "", Tags = f.Length > 3 ? f[3] : "", Note = f.Length > 4 ? f[4] : "",
                };
            }
        }

        // order: the loads' keys in the analysis's order (size, then class); marks for keys not in it follow, sorted.
        internal static string ToText(IList<string> order)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# GK2 Donkey API cargo review: every load the cart can carry, looked at one by one in the Cargo tab (docs/cargo-review.md).");
            sb.AppendLine("# <classes, sorted> = ok | wrong | later, then | date | fingerprint of the load's tuning when marked | reason tags | note.");
            sb.AppendLine("# An ok whose fingerprint no longer matches the layout shows as 'to re-check' in the panel (something it depends on was edited since).");
            sb.AppendLine("# Written by the panel as you mark; not embedded, not shipped.");
            sb.AppendLine("position = " + (LastPositionKey ?? ""));
            sb.AppendLine();
            var written = new HashSet<string>(StringComparer.Ordinal);
            if (order != null)
            {
                foreach (string key in order)
                {
                    if (marks.TryGetValue(key, out Mark m) && written.Add(key))
                    {
                        sb.AppendLine(Line(m));
                    }
                }
            }
            var rest = new List<string>();
            foreach (string key in marks.Keys)
            {
                if (!written.Contains(key))
                {
                    rest.Add(key);
                }
            }
            rest.Sort(StringComparer.Ordinal);
            foreach (string key in rest)
            {
                sb.AppendLine(Line(marks[key]));
            }
            return sb.ToString();
        }

        private static string Line(Mark m) => $"{m.Key} = {m.Verdict.ToString().ToLowerInvariant()} | {m.When} | {m.Fingerprint} | {m.Tags} | {m.Note}";

        // Called every pass by the panel: writes 0.75 s after the last change (as the layout does).
        internal static void SaveIfDue(IList<string> order, bool now = false)
        {
            if (!dirty || (!now && unchecked(Environment.TickCount - lastChange) < 750))
            {
                return;
            }
            dirty = false;
            string path = RepoPath;
            if (path == null)
            {
                return;
            }
            try
            {
                File.WriteAllText(path, ToText(order));
                LastSaveNote = "Saved to the repo's " + FileName + " at " + DateTime.Now.ToString("HH:mm:ss") + ".";
            }
            catch (Exception e)
            {
                LastSaveNote = "Saving the review failed: " + e.Message;
                Plugin.Log.LogWarning("Cargo review: " + LastSaveNote);
            }
        }

        internal static void MarkPositionDirty() => Touch();
    }
}
