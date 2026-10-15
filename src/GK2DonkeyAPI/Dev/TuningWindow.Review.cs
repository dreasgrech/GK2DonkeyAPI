using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Cargo tab's review strip (docs/cargo-review.md): every load the cart can carry, one by one, with a verdict
    // each, kept in cargo-review.cfg (CargoReview). Sits between the coverage line and "Tune this load", so a wrong
    // load is tuned in the boxes right below and then marked ok.
    //   The walk is the analysis's load order (size, then class: the ladder, so an ok that freezes lower relations comes
    //   before the loads built on them), narrowed by the filters: number of items, classes it must contain, and which
    //   marks to show (all, unreviewed, wrong, to re-check).
    //   Ok = "Everything here looks right" (every untuned contact of the load frozen as it lies) + the mark with the
    //   fingerprint taken after the freeze. Wrong = the mark with the reason tags; the load stays on the cart to be
    //   tuned below (Looks right then turns it ok). Later = the mark; the walk moves on.
    //   To re-check: an ok whose fingerprint differs from the load's tuning now (an edit, accept or undo touched
    //   something it depends on). Recomputed when the layout or the fallbacks change (the coverage signature), never
    //   per frame.
    //   Frozen-view rule (DrawCargoTab): everything drawn comes from state read before the buttons; a click's effect
    //   shows from the next pass.
    internal static partial class TuningWindow
    {
        private const string TipReviewOk = "This load looks right: every contact still on the fallbacks is frozen as it lies (as 'Everything here looks right'), and the load is marked ok with a fingerprint of its tuning. Edits that touch it later make it 'to re-check'.";
        private const string TipReviewWrong = "Mark this load wrong, with the reason tags ticked. It stays on the cart: tune it in the boxes below, then Looks right.";
        private const string TipReviewLater = "You saw it and will come back: marked 'later' (its own filter), counted as reviewed, the walk moves on.";
        private const string TipReviewClear = "Remove this load's mark.";
        private const string TipReviewLoad = "Put this load on the cart (a test load: real items; the cart must hold nothing of yours).";

        private enum ReviewShow { All, Unreviewed, Wrong, Recheck, Later, Ok }
        private enum ReviewState { Unreviewed, Ok, Wrong, Later, Recheck }

        private static bool reviewOpen;
        private static bool reviewAutoLoad = true, reviewAutoAdvance = true;
        private static ReviewShow reviewShow = ReviewShow.All;
        private static readonly bool[] reviewSizes = { true, true, true, true, true, true, true }; // index = items (1..6)
        private static readonly HashSet<int> reviewClasses = new HashSet<int>(); // class indices the load must contain (empty: any)
        private static readonly bool[] reviewTags = new bool[CargoReview.TagNames.Length];
        private static string reviewNote = "";
        private static int reviewIndex;         // into reviewWalk
        private static bool reviewPositioned;   // the saved position restored once
        private static bool reviewVisited;      // a load was put on the cart by the strip this session: until then the first > loads the current one (the lone corpse at position 1 was skipped by a first > on 2026-10-15)
        private static string reviewStatus = "";
        private static int reviewKeyFrame;

        // Per analysis and layout version: keys, fingerprints, states, the walk.
        private static CargoCoverage.Report reviewFor;
        private static string reviewFpSig;
        private static string[] reviewKeys;
        private static string[] reviewFps;
        private static ReviewState[] reviewStates;
        private static List<int> reviewWalk = new List<int>();
        private static string reviewWalkSig;
        private static int rvOk, rvWrong, rvLater, rvRecheck;
        private static readonly int[] rvSizeTotal = new int[8], rvSizeOk = new int[8];

        private static void EnsureReviewCache()
        {
            string sig = coverageSig + "|" + CargoLayout.Version;
            if (ReferenceEquals(reviewFor, coverage) && reviewFpSig == sig && reviewKeys != null)
            {
                return;
            }
            bool newReport = !ReferenceEquals(reviewFor, coverage);
            reviewFor = coverage;
            reviewFpSig = sig;
            int n = coverage.Loads.Count;
            if (newReport || reviewKeys == null || reviewKeys.Length != n)
            {
                reviewKeys = new string[n];
                for (int i = 0; i < n; i++)
                {
                    reviewKeys[i] = CargoReview.KeyOf(coverage, coverage.Loads[i].Classes);
                }
                reviewWalkSig = null;
            }
            string fallbacks = FallbackSignature();
            reviewFps = new string[n];
            reviewStates = new ReviewState[n];
            rvOk = rvWrong = rvLater = rvRecheck = 0;
            Array.Clear(rvSizeTotal, 0, rvSizeTotal.Length);
            Array.Clear(rvSizeOk, 0, rvSizeOk.Length);
            for (int i = 0; i < n; i++)
            {
                CargoCoverage.Load ld = coverage.Loads[i];
                reviewFps[i] = CargoReview.Fingerprint(coverage, ld, CargoLayout.ForItem, CargoLayout.ForPair, fallbacks);
                CargoReview.Mark m = CargoReview.Get(reviewKeys[i]);
                ReviewState st = ReviewState.Unreviewed;
                if (m != null)
                {
                    switch (m.Verdict)
                    {
                        case CargoReview.Verdict.Ok: st = m.Fingerprint == reviewFps[i] ? ReviewState.Ok : ReviewState.Recheck; break;
                        case CargoReview.Verdict.Wrong: st = ReviewState.Wrong; break;
                        case CargoReview.Verdict.Later: st = ReviewState.Later; break;
                    }
                }
                reviewStates[i] = st;
                int size = Mathf.Clamp(ld.Classes.Length, 0, 7);
                rvSizeTotal[size]++;
                switch (st)
                {
                    case ReviewState.Ok: rvOk++; rvSizeOk[size]++; break;
                    case ReviewState.Wrong: rvWrong++; break;
                    case ReviewState.Later: rvLater++; break;
                    case ReviewState.Recheck: rvRecheck++; break;
                }
            }
            reviewWalkSig = null; // the states changed: the walk may too
        }

        // The fallback settings an untuned contact is placed with: the coverage signature without the layout version.
        private static string FallbackSignature()
        {
            string s = coverageSig ?? "";
            int bar = s.IndexOf('|');
            // plus the seat pass switch: an untuned item is seated on the measured shapes only while it is on
            return (bar >= 0 ? s.Substring(bar + 1) : s) + "|seat=" + (Plugin.CargoSeatOnShapes != null && Plugin.CargoSeatOnShapes.Value);
        }

        private static void EnsureReviewWalk()
        {
            var sb = new StringBuilder();
            sb.Append((int)reviewShow).Append('|');
            for (int i = 1; i <= 6; i++) sb.Append(reviewSizes[i] ? '1' : '0');
            sb.Append('|');
            foreach (int c in reviewClasses) sb.Append(c).Append(',');
            string sig = sb.ToString();
            if (sig == reviewWalkSig)
            {
                return;
            }
            int keep = reviewWalk.Count > 0 && reviewIndex >= 0 && reviewIndex < reviewWalk.Count ? reviewWalk[reviewIndex] : -1;
            reviewWalkSig = sig;
            reviewWalk = new List<int>();
            for (int i = 0; i < coverage.Loads.Count; i++)
            {
                CargoCoverage.Load ld = coverage.Loads[i];
                int size = ld.Classes.Length;
                if (size < 1 || size > 6 || !reviewSizes[size])
                {
                    continue;
                }
                if (reviewClasses.Count > 0)
                {
                    bool all = true;
                    foreach (int c in reviewClasses)
                    {
                        if (Array.IndexOf(ld.Classes, c) < 0)
                        {
                            all = false;
                            break;
                        }
                    }
                    if (!all)
                    {
                        continue;
                    }
                }
                ReviewState st = reviewStates[i];
                bool show = reviewShow == ReviewShow.All
                    || (reviewShow == ReviewShow.Unreviewed && st == ReviewState.Unreviewed)
                    || (reviewShow == ReviewShow.Wrong && st == ReviewState.Wrong)
                    || (reviewShow == ReviewShow.Recheck && st == ReviewState.Recheck)
                    || (reviewShow == ReviewShow.Later && st == ReviewState.Later)
                    || (reviewShow == ReviewShow.Ok && st == ReviewState.Ok);
                if (show)
                {
                    reviewWalk.Add(i);
                }
            }
            // stay on the same load when it is still in the walk, else the nearest after it
            reviewIndex = 0;
            if (keep >= 0)
            {
                for (int w = 0; w < reviewWalk.Count; w++)
                {
                    if (reviewWalk[w] >= keep)
                    {
                        reviewIndex = w;
                        break;
                    }
                }
            }
        }

        private static void DrawReviewStrip(List<Cargo.PlacedItem> placed, bool isolating, bool onCart)
        {
            if (coverage == null)
            {
                return;
            }
            EnsureReviewCache();
            if (!reviewPositioned)
            {
                reviewPositioned = true;
                string pos = CargoReview.LastPositionKey;
                if (!string.IsNullOrEmpty(pos))
                {
                    EnsureReviewWalk();
                    for (int w = 0; w < reviewWalk.Count; w++)
                    {
                        if (reviewKeys[reviewWalk[w]] == pos)
                        {
                            reviewIndex = w;
                            break;
                        }
                    }
                }
            }
            EnsureReviewWalk();
            CargoReview.SaveIfDue(reviewKeys);

            int total = coverage.Loads.Count;
            int reviewed = rvOk + rvWrong + rvRecheck + rvLater; // a 'later' was seen: it counts, and has its own filter
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            bool open = GUILayout.Toggle(reviewOpen, Tip(reviewOpen ? "[-] Review every load" : "[+] Review every load", "Every load the cart can carry, one by one: load it, look, mark it. The marks are kept in the repo's cargo-review.cfg, so the review goes on where it stopped."), Wd(170f));
            if (open != reviewOpen)
            {
                reviewOpen = open;
            }
            string col = reviewed == total && rvRecheck == 0 && rvWrong == 0 ? "#8ee68e" : "#ffd257";
            GUILayout.Label($"<color={col}>{reviewed} of {total} reviewed</color> · <color=#8ee68e>{rvOk} ok</color> · <color=#ff8c69>{rvWrong} wrong</color> · <color=#ffd257>{rvLater} later</color>" + (rvRecheck > 0 ? $" · <color=#ffd257>{rvRecheck} to re-check</color>" : ""), label);
            GUILayout.EndHorizontal();
            DrawBar(total == 0 ? 0f : (float)rvOk / total, total == 0 ? 0f : (float)(rvWrong + rvRecheck) / total);
            if (!reviewOpen)
            {
                GUILayout.EndVertical();
                return;
            }
            // The fingerprint format changed since this file's marks were made: every ok would show as to re-check.
            // One press re-stamps them with today's fingerprints; the user vouches that the layout is as it was.
            if (CargoReview.FileFingerprintVersion != CargoReview.FingerprintVersion && rvOk + rvRecheck > 0)
            {
                GUILayout.Label($"<color=#ffd257>The fingerprint format changed (file v{CargoReview.FileFingerprintVersion}, code v{CargoReview.FingerprintVersion}): {rvRecheck} ok(s) read as to re-check.</color> Re-stamp them if the layout is unchanged since; if it did change, look at them instead.", wrapLabel);
                if (GUILayout.Button(Tip("Re-stamp the ok marks", "The fingerprint format changed (a code update). If nothing of the layout changed since these marks were made, press this: every ok takes today's fingerprint and stays ok. If the layout did change, do not: look at the 'to re-check' loads instead."), Wd(170f)))
                {
                    var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int i = 0; i < reviewKeys.Length; i++) byKey[reviewKeys[i]] = reviewFps[i];
                    int n = CargoReview.Restamp(k => byKey.TryGetValue(k, out string f) ? f : null);
                    reviewStatus = $"{n} ok mark(s) re-stamped.";
                    reviewFpSig = null;
                }
            }

            // Filters: items, what to show, classes.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Items:", Wd(44f));
            for (int i = 1; i <= 6; i++)
            {
                bool on = GUILayout.Toggle(reviewSizes[i], Tip($"{i}  <color=#9a9a9a>{rvSizeOk[i]}/{rvSizeTotal[i]}</color>", $"Loads of {i} item{(i == 1 ? "" : "s")}: {rvSizeOk[i]} ok of {rvSizeTotal[i]}."), Wd(96f));
                if (on != reviewSizes[i]) reviewSizes[i] = on;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show:", Wd(44f));
            var shows = new[] { "all", "unreviewed", "wrong", "to re-check", "later", "ok" };
            int sel = GUILayout.SelectionGrid((int)reviewShow, shows, shows.Length, Wd(520f));
            if (sel != (int)reviewShow) reviewShow = (ReviewShow)sel;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("With:", Wd(44f));
            for (int c = 0; c < coverage.Classes.Count; c++)
            {
                bool on = reviewClasses.Contains(c);
                bool now = GUILayout.Toggle(on, Tip(coverage.Label(coverage.Classes[c]), "Only loads containing this class (tick several: all of them)."), Wd(90f));
                if (now != on)
                {
                    if (now) reviewClasses.Add(c); else reviewClasses.Remove(c);
                }
            }
            if (reviewClasses.Count > 0 && GUILayout.Button("any", Wd(44f)))
            {
                reviewClasses.Clear();
            }
            GUILayout.EndHorizontal();

            if (reviewWalk.Count == 0)
            {
                GUILayout.Label("No load matches these filters.", small);
                GUILayout.EndVertical();
                return;
            }
            reviewIndex = Mathf.Clamp(reviewIndex, 0, reviewWalk.Count - 1);
            int li = reviewWalk[reviewIndex];
            CargoCoverage.Load load = coverage.Loads[li];
            ReviewState state = reviewStates[li];
            CargoReview.Mark mark = CargoReview.Get(reviewKeys[li]);
            bool matches = onCart && LoadMatches(placed, load);
            bool canAct = !CargoLayout.Comparing;

            // Position and stepping.
            GUILayout.BeginHorizontal();
            if (TintedButton(new GUIContent("<"), TintStep, 28f)) Step(-1, null);
            if (TintedButton(new GUIContent(">"), TintStep, 28f)) Step(1, null);
            if (TintedButton(Tip("Next unreviewed", "The next load after this one with no mark at all (a 'later' has its own filter)."), TintStep, 120f)) Step(1, ReviewState.Unreviewed);
            if (TintedButton(Tip("Next wrong", "The next load marked wrong."), TintWrong, 90f, rvWrong > 0)) Step(1, ReviewState.Wrong);
            if (TintedButton(Tip("Next to re-check", "The next load marked ok whose tuning changed since."), TintLater, 125f, rvRecheck > 0)) Step(1, ReviewState.Recheck);
            if (TintedButton(Tip("Next later", "The next load marked later."), TintLater, 85f, rvLater > 0)) Step(1, ReviewState.Later);
            if (TipButton("Load it", TipReviewLoad, 70f, onCart && !matches)) PutReviewLoad(load);
            GUILayout.EndHorizontal();
            string stateText = state == ReviewState.Unreviewed ? "<color=#9a9a9a>unreviewed</color>"
                : state == ReviewState.Ok ? "<color=#8ee68e>ok</color>"
                : state == ReviewState.Wrong ? "<color=#ff8c69>wrong</color>"
                : state == ReviewState.Later ? "<color=#ffd257>later</color>" : "<color=#ffd257>to re-check (tuned since)</color>";
            string when = mark != null && mark.When.Length > 0 ? $" <color=#9a9a9a>{mark.When}</color>" : "";
            string tags = mark != null && mark.Tags.Length > 0 ? $" <color=#ff8c69>{mark.Tags}</color>" : "";
            string note = mark != null && mark.Note.Length > 0 ? $" <color=#9a9a9a>\"{mark.Note}\"</color>" : "";
            OneLine($"Load {reviewIndex + 1} of {reviewWalk.Count}{(reviewWalk.Count != total ? $" <color=#9a9a9a>(of {total})</color>" : "")}: <b>{coverage.Describe(load.Classes)}</b> · {load.Untuned} untuned · {stateText}{when}{tags}{note}");
            // Always one line here (empty when the cart shows the load), so the verdict row never moves.
            OneLine(!onCart ? "Mount in Cart style to put the loads on the cart."
                : !matches ? (reviewVisited
                    ? $"<color=#ffd257>The cart does not show this load</color> (Load it, or step with 'load as I step' on).{(TestLoads.Status.Length > 0 ? " " + TestLoads.Status : "")}"
                    : "<color=#ffd257>Not on the cart yet:</color> press > (or Load it) to put this first load on the cart; the next > then moves on.")
                : "", smallFont: true);

            // Verdicts.
            GUILayout.BeginHorizontal();
            bool verdictsOn = canAct && matches;
            if (TintedButton(Tip("Looks right", TipReviewOk), TintOk, 100f, verdictsOn))
            {
                ReviewOk(li, placed, isolating);
            }
            if (TintedButton(Tip("Wrong", TipReviewWrong), TintWrong, 70f, verdictsOn))
            {
                ReviewWrong(li);
            }
            if (TintedButton(Tip("Later", TipReviewLater), TintLater, 60f, verdictsOn))
            {
                CargoReview.Set(reviewKeys[li], CargoReview.Verdict.Later, "", "", "");
                reviewFpSig = null;
                if (reviewAutoAdvance) Step(1, ReviewState.Unreviewed);
            }
            if (TipButton("Clear", TipReviewClear, 60f, canAct && mark != null))
            {
                CargoReview.Clear(reviewKeys[li]);
                reviewFpSig = null;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Why:", Wd(44f));
            for (int t = 0; t < CargoReview.TagNames.Length; t++)
            {
                reviewTags[t] = GUILayout.Toggle(reviewTags[t], CargoReview.TagNames[t], Wd(t == 3 || t == 4 ? 100f : 80f));
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Note:", Wd(40f));
            reviewNote = GUILayout.TextField(reviewNote, Wd(300f));
            reviewAutoLoad = GUILayout.Toggle(reviewAutoLoad, Tip("load as I step", "Stepping puts the load on the cart at once (a test load)."), Wd(110f));
            reviewAutoAdvance = GUILayout.Toggle(reviewAutoAdvance, Tip("next after a verdict", "After Looks right or Later the walk moves to the next unreviewed load and loads it."), Wd(140f));
            GUILayout.EndHorizontal();
            string keys = $"{Plugin.ReviewPrevKey.Value}/{Plugin.ReviewNextKey.Value} step, {Plugin.ReviewOkKey.Value} ok, {Plugin.ReviewWrongKey.Value} wrong, {Plugin.ReviewLaterKey.Value} later";
            OneLine($"<color=#9a9a9a>{keys} (while this tab shows and no text field has the focus).</color>", smallFont: true);
            OneLine($"<color=#9a9a9a>{CargoReview.LastSaveNote} {reviewStatus}</color>", smallFont: true);
            GUILayout.EndVertical();

            PollReviewKeys(li, placed, isolating, matches, canAct);
        }

        // The verdicts and the jumps in their colours (green ok, red wrong, yellow later / re-check, grey the rest), so the
        // strip can be read at a glance. The tint is the button's background; a disabled button keeps it, dimmed.
        private static readonly Color TintOk = new Color(0.55f, 1f, 0.55f), TintWrong = new Color(1f, 0.55f, 0.5f), TintLater = new Color(1f, 0.9f, 0.45f), TintStep = new Color(0.8f, 0.85f, 1f);

        private static bool TintedButton(GUIContent content, Color tint, float width, bool active = true)
        {
            bool was = GUI.enabled;
            Color back = GUI.backgroundColor;
            GUI.enabled = was && active;
            GUI.backgroundColor = tint;
            bool pressed = GUILayout.Button(content, Wd(width));
            GUI.backgroundColor = back;
            GUI.enabled = was;
            return pressed;
        }

        private static void DrawBar(float ok, float bad)
        {
            Rect r = GUILayoutUtility.GetRect(10f, 4f, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none, barBack);
            if (ok > 0f)
            {
                GUI.Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ok), r.height), GUIContent.none, barFill);
            }
            if (bad > 0f)
            {
                Color was = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.41f);
                GUI.Box(new Rect(r.x + r.width * Mathf.Clamp01(ok), r.y, r.width * Mathf.Clamp01(bad), r.height), GUIContent.none, barFill);
                GUI.color = was;
            }
        }

        // The cart shows this load: the same classes, as a multiset.
        private static bool LoadMatches(List<Cargo.PlacedItem> placed, CargoCoverage.Load load)
        {
            if (placed.Count != load.Classes.Length)
            {
                return false;
            }
            var want = new List<string>();
            foreach (int c in load.Classes) want.Add(coverage.Classes[c]);
            want.Sort(StringComparer.Ordinal);
            var have = new List<string>();
            foreach (Cargo.PlacedItem p in placed) have.Add(CargoSheet.ClassOf(p.Id));
            have.Sort(StringComparer.Ordinal);
            for (int i = 0; i < want.Count; i++)
            {
                if (want[i] != have[i]) return false;
            }
            return true;
        }

        // Moves by dir within the walk; with a wanted state, to the next load in that state (round the walk once).
        private static void Step(int dir, ReviewState? wanted)
        {
            if (reviewWalk.Count == 0)
            {
                return;
            }
            // The first step of a session, forward and plain: the load the strip stands on has never been put on the
            // cart, so that is what the user wants to see; moving past it would skip it.
            if (!reviewVisited && dir > 0 && wanted == null && cargoViewOnCart)
            {
                reviewVisited = true;
                reviewStatus = "";
                PutReviewLoad(coverage.Loads[reviewWalk[reviewIndex]]);
                return;
            }
            int n = reviewWalk.Count;
            int at = reviewIndex;
            for (int k = 1; k <= n; k++)
            {
                int w = ((at + dir * k) % n + n) % n;
                ReviewState st = reviewStates[reviewWalk[w]];
                bool ok = wanted == null || st == wanted.Value;
                if (ok)
                {
                    reviewIndex = w;
                    break;
                }
                if (k == n)
                {
                    reviewStatus = wanted == null ? "" : $"No other load is {(wanted == ReviewState.Recheck ? "to re-check" : wanted.Value.ToString().ToLowerInvariant())}.";
                    return;
                }
            }
            reviewStatus = "";
            CargoReview.LastPositionKey = reviewKeys[reviewWalk[reviewIndex]];
            CargoReview.MarkPositionDirty();
            if (reviewAutoLoad && cargoViewOnCart)
            {
                reviewVisited = true;
                PutReviewLoad(coverage.Loads[reviewWalk[reviewIndex]]);
            }
        }

        private static void PutReviewLoad(CargoCoverage.Load load)
        {
            reviewVisited = true;
            string r = TestLoads.Put(LoadIds(load.Classes), "review: " + coverage.Describe(load.Classes));
            reviewStatus = r != null && r.StartsWith("on the cart", StringComparison.Ordinal) ? "" : r ?? "";
        }

        private static void ReviewOk(int li, List<Cargo.PlacedItem> placed, bool isolating)
        {
            AcceptAll(placed, isolating, cargoViewPairMode);
            // the fingerprint of the tuning as it is now (the accept just wrote it)
            string fp = CargoReview.Fingerprint(coverage, coverage.Loads[li], CargoLayout.ForItem, CargoLayout.ForPair, FallbackSignature());
            CargoReview.Set(reviewKeys[li], CargoReview.Verdict.Ok, fp, "", "");
            ClearReviewReason();
            reviewFpSig = null;
            if (reviewAutoAdvance) Step(1, ReviewState.Unreviewed);
        }

        // The load on the cart marked ok in the review (the Accept buttons of the tuning ladder call this after their
        // freeze): found by its key among the analysis's loads; nothing when the cart shows no known load.
        private static void ReviewRecordCart(List<Cargo.PlacedItem> placed)
        {
            if (coverage == null || placed == null || placed.Count == 0 || reviewKeys == null)
            {
                return;
            }
            var names = new List<string>();
            foreach (Cargo.PlacedItem p in placed) names.Add(coverage.Label(CargoSheet.ClassOf(p.Id)));
            names.Sort(StringComparer.Ordinal);
            string key = string.Join("+", names);
            int li = Array.IndexOf(reviewKeys, key);
            if (li < 0)
            {
                return;
            }
            string fp = CargoReview.Fingerprint(coverage, coverage.Loads[li], CargoLayout.ForItem, CargoLayout.ForPair, FallbackSignature());
            CargoReview.Set(key, CargoReview.Verdict.Ok, fp, "", "");
            reviewFpSig = null;
        }

        private static void ReviewWrong(int li)
        {
            var tags = new List<string>();
            for (int t = 0; t < reviewTags.Length; t++) if (reviewTags[t]) tags.Add(CargoReview.TagNames[t]);
            CargoReview.Set(reviewKeys[li], CargoReview.Verdict.Wrong, reviewFps[li], string.Join(",", tags), reviewNote);
            ClearReviewReason();
            reviewFpSig = null;
            reviewStatus = "Marked wrong: tune it below, then Looks right; or > to move on.";
        }

        private static void ClearReviewReason()
        {
            Array.Clear(reviewTags, 0, reviewTags.Length);
            reviewNote = "";
        }

        private static void PollReviewKeys(int li, List<Cargo.PlacedItem> placed, bool isolating, bool matches, bool canAct)
        {
            if (Time.frameCount == reviewKeyFrame || GUIUtility.keyboardControl != 0)
            {
                return;
            }
            reviewKeyFrame = Time.frameCount;
            var input = BepInEx.UnityInput.Current;
            bool Down(BepInEx.Configuration.ConfigEntry<KeyCode> k) => k.Value != KeyCode.None && input.GetKeyDown(k.Value);
            if (Down(Plugin.ReviewPrevKey)) Step(-1, null);
            else if (Down(Plugin.ReviewNextKey)) Step(1, null);
            else if (Down(Plugin.ReviewOkKey) && canAct && matches) ReviewOk(li, placed, isolating);
            else if (Down(Plugin.ReviewWrongKey) && canAct && matches) ReviewWrong(li);
            else if (Down(Plugin.ReviewLaterKey) && canAct && matches)
            {
                CargoReview.Set(reviewKeys[li], CargoReview.Verdict.Later, "", "", "");
                reviewFpSig = null;
                if (reviewAutoAdvance) Step(1, ReviewState.Unreviewed);
            }
        }
    }
}
