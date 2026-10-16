using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GK2ShowcaseDirector
{
    // The probe run (docs/cargo-auto-tune.md): one unattended pass that gathers everything the picture can tell about
    // the cargo layout, for the auto-tune calibration. Loads: every load with a review mark, every load of one to three
    // items, and a fixed sample of 25 loads each of four, five and six items (the same sample every run). Each is put on
    // the cart as an API test load (TestLoads.Put: it only ever replaces a test load and refuses while the cart holds
    // anything else), left to settle, and probed in one frame step (PictureProbe.Session): the normal picture, an id
    // mask, every item's outline, what shows of it and who covers it, every pair's mutual cover (a visible cut-in), the
    // air under every upper item, and the 3D side (placements, boxes, the bed, the views' places and bounds, the
    // camera's matrices). Loads of up to three items also get sweeps: every item but the first moved by -8..+8 cm on
    // each axis (the view only, put straight back) and measured again. Written to <shots>/probe-run_<time>/: one .txt
    // per load, its normal and mask pictures, and summary.csv. The last test load is taken off at the end. Stop ends it
    // after the current load.
    internal sealed partial class Shots
    {
        private bool probeStop;
        private string probeStatus = "";
        private static readonly float[] SweepSteps = { -0.08f, -0.04f, -0.02f, 0.02f, 0.04f, 0.08f };
        private const int SampleEach = 25, SettleFrames = 10;

        private static Assembly Api => typeof(GK2DonkeyAPI.Api.Mounts).Assembly;
        private const BindingFlags BS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags BI = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static object Member(object o, string name)
        {
            Type t = o.GetType();
            return t.GetProperty(name, BI)?.GetValue(o) ?? t.GetField(name, BI)?.GetValue(o);
        }

        // atlasOnly: the item atlas and the cart body alone (a short run for another facing of the cart).

        private IEnumerator ProbeRunRoutine(bool atlasOnly)
        {
            probeStop = false;
            object rep;
            IList loads, classes;
            MethodInfo describe, keyOf, getMark, put, takeOff;
            try
            {
                rep = Api.GetType("GK2DonkeyAPI.CargoSheet").GetMethod("AnalyseNow", BS).Invoke(null, new object[] { 6 });
                loads = (IList)Member(rep, "Loads");
                classes = (IList)Member(rep, "Classes");
                describe = rep.GetType().GetMethod("Describe", BI);
                Type review = Api.GetType("GK2DonkeyAPI.CargoReview");
                keyOf = review.GetMethod("KeyOf", BS);
                getMark = review.GetMethod("Get", BS);
                Type tl = Api.GetType("GK2DonkeyAPI.TestLoads");
                put = tl.GetMethod("Put", BS);
                takeOff = tl.GetMethod("TakeOff", BS);
            }
            catch (Exception e)
            {
                Say("Probe run: the API's analysis could not be reached (" + e.Message + ")");
                log.LogWarning("[probe] " + e);
                yield break;
            }

            // the selection
            var pick = new List<int>();
            var marks = new Dictionary<int, string>();
            var bySize = new Dictionary<int, List<int>>();
            for (int li = 0; li < loads.Count; li++)
            {
                int[] cls = (int[])Member(loads[li], "Classes");
                string key = (string)keyOf.Invoke(null, new object[] { rep, cls });
                object mark = getMark.Invoke(null, new object[] { key });
                string verdict = mark != null ? Member(mark, "Verdict").ToString() : "";
                if (verdict.Length > 0) marks[li] = verdict;
                if (verdict.Length > 0 || cls.Length <= 3) pick.Add(li);
                else
                {
                    if (!bySize.TryGetValue(cls.Length, out var l)) bySize[cls.Length] = l = new List<int>();
                    l.Add(li);
                }
            }
            var rng = new System.Random(20261015);
            foreach (int size in new[] { 4, 5, 6 })
            {
                if (!bySize.TryGetValue(size, out var l)) continue;
                for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = l[i]; l[i] = l[j]; l[j] = t; }
                for (int i = 0; i < Math.Min(SampleEach, l.Count); i++) pick.Add(l[i]);
            }

            string facing = Facing();
            string dir = Path.Combine(folder.Value, $"probe-{(atlasOnly ? "atlas" : "run")}_{DateTime.Now:yyyyMMdd_HHmmss}_{facing}");
            Directory.CreateDirectory(dir);
            if (atlasOnly) pick.Clear();
            File.WriteAllText(Path.Combine(dir, "run.txt"), $"started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nfacing {facing}\n{CameraLines()}loads {pick.Count}\nsweep_steps {string.Join(" ", Array.ConvertAll(SweepSteps, x => PictureProbe.F(x)))}\n");
            var summary = new StringBuilder("index,key,verdict,items,layers,min_visible_pct,max_mutual_px,max_gap_px,gap_cols,cart_covers_px\n");
            Capturing = true;
            HidePanel();
            HUD hud = Hud();
            bool hid = false;
            if (hud != null && hud.gameObject.activeSelf)
            {
                SetHud(false);
                hid = true;
            }
            int done = 0, skipped = 0;
            log.LogInfo($"[probe] run of {pick.Count} loads into {dir} (facing {facing})");
            try
            {
                // the atlas: every carryable item alone, with its depth, and the cart body
                IEnumerator atlas = AtlasRoutine(dir, put);
                while (!probeStop && atlas.MoveNext()) yield return atlas.Current;
                for (int p = 0; p < pick.Count && !probeStop; p++)
                {
                    int li = pick[p];
                    int[] cls = (int[])Member(loads[li], "Classes");
                    var ids = new List<string>();
                    foreach (int c in cls) ids.Add((string)classes[c]);
                    string key = (string)keyOf.Invoke(null, new object[] { rep, cls });
                    string desc = (string)describe.Invoke(rep, new object[] { cls });
                    probeStatus = $"{p + 1} of {pick.Count}: {desc}";
                    string r = (string)put.Invoke(null, new object[] { ids, "probe run" });
                    if (r == null || !r.StartsWith("on the cart"))
                    {
                        log.LogWarning($"[probe] {key}: not put on the cart: {r}");
                        if (r != null && r.Contains("not a test load"))
                        {
                            Say("Probe run stopped: the cart holds cargo that is not a test load. Set it down first.");
                            break;
                        }
                        skipped++;
                        continue;
                    }
                    // until the views show the whole load, then a little longer for everything to settle
                    int waited = 0;
                    for (; waited < 180; waited++)
                    {
                        yield return null;
                        if (OnCart(ids)) break;
                    }
                    for (int f = 0; f < SettleFrames; f++) yield return null;
                    string why = ProbeOne(dir, p, key, marks.TryGetValue(li, out string vd) ? vd : "", desc, cls.Length <= 3, summary);
                    if (why != null)
                    {
                        log.LogWarning($"[probe] {key}: {why}");
                        skipped++;
                        continue;
                    }
                    done++;
                    if (done % 10 == 0) File.WriteAllText(Path.Combine(dir, "summary.csv"), summary.ToString());
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(dir, "summary.csv"), summary.ToString());
                try { takeOff.Invoke(null, null); } catch (Exception e) { log.LogWarning("[probe] taking the test load off: " + e.Message); }
                if (hid) SetHud(true);
                RestorePanel();
                Capturing = false;
                probeStatus = "";
            }
            string end = $"Probe run: {done} loads probed, {skipped} skipped{(probeStop ? " (stopped)" : "")}, into {dir}";
            log.LogInfo("[probe] " + end);
            Say(end);
        }

        // The load on the cart now, measured and written (one frame step, no yield); null when done, else why not.
        private string ProbeOne(string dir, int index, string key, string verdict, string desc, bool sweep, StringBuilder summary)
        {
            if (!PictureProbe.CargoViews(out IList views, out IList placed, out string why)) return why;
            int n = Math.Min(views.Count, PictureProbe.Cart);
            var layers = new int[n];
            var txt = new StringBuilder();
            txt.AppendLine($"load {key}");
            txt.AppendLine($"describe {desc}");
            txt.AppendLine($"verdict {(verdict.Length > 0 ? verdict : "none")}");
            using (PictureProbe.Session s = PictureProbe.Session.Begin(out why))
            {
                if (s == null) return why;
                Camera w = s.World;
                txt.AppendLine($"picture {s.W} {s.H} roi {s.Roi.x} {s.Roi.y} {s.Roi.width} {s.Roi.height} (pixel coordinates below are inside the roi, rows upward)");
                txt.AppendLine($"probe_mode {s.Mode} shader {s.ShaderName} test {s.Test}");
                txt.AppendLine($"camera ortho {w.orthographic} size {PictureProbe.F(w.orthographicSize)} pos {PictureProbe.V(w.transform.position)} rot {PictureProbe.V(w.transform.eulerAngles)}");
                txt.AppendLine($"world_to_camera {PictureProbe.M(w.worldToCameraMatrix)}");
                txt.AppendLine($"projection {PictureProbe.M(w.projectionMatrix)}");
                object rig = PictureProbe.Rig();
                Transform container = PictureProbe.RigMember(rig, "container") as Transform;
                if (container != null) txt.AppendLine($"container_to_world {PictureProbe.M(container.localToWorldMatrix)}");
                txt.AppendLine($"bed width {PictureProbe.F((float)PictureProbe.RigMember(rig, "BedWidth"))} length {PictureProbe.F((float)PictureProbe.RigMember(rig, "BedLength"))} floor {PictureProbe.F((float)PictureProbe.RigMember(rig, "LastFloor"))}");
                txt.AppendLine($"cart_renderers {s.CartRenderers}");
                txt.AppendLine($"facing {Facing()}");
                Color32[] normal = null;
                {
                    // the normal picture first, before anything is swapped (Owners swaps)
                    normal = NormalPixels(s);
                }
                int allBits = (1 << n) - 1, cartBit = 1 << PictureProbe.Cart;
                int[] all = s.Owners(allBits | cartBit);
                int[] noCart = s.Owners(allBits);
                var solo = new int[n][];
                for (int k = 0; k < n; k++) solo[k] = s.Owners(1 << k);
                for (int k = 0; k < n; k++)
                {
                    object pl = placed[k];
                    layers[k] = PictureProbe.Field<int>(pl, "Layer");
                    Transform vt = s.Views[k];
                    string bounds = "";
                    if (vt != null)
                    {
                        bool any = false;
                        Bounds b = default;
                        foreach (Renderer rr in vt.GetComponentsInChildren<Renderer>(false))
                        {
                            if (!rr.enabled) continue;
                            if (!any) { b = rr.bounds; any = true; } else b.Encapsulate(rr.bounds);
                        }
                        bounds = any ? $"{PictureProbe.V(b.min)} {PictureProbe.V(b.max)}" : "none";
                    }
                    txt.AppendLine($"item {k} id {PictureProbe.Field<string>(pl, "Id")} layer {layers[k]} relation {PictureProbe.Field<string>(pl, "Relation")} partner {PictureProbe.Field<int>(pl, "Partner")} tuned {PictureProbe.Field<bool>(pl, "Tuned")}" +
                        $" pos {PictureProbe.V(PictureProbe.Field<Vector3>(pl, "Pos"))} box {PictureProbe.V(PictureProbe.Field<Vector3>(pl, "Box"))} rel {PictureProbe.V(PictureProbe.Field<Vector3>(pl, "Rel"))}" +
                        $" view_local {(vt != null ? PictureProbe.V(vt.localPosition) : "none")} view_parent_is_container {(vt != null && vt.parent == container)} view_world {(vt != null ? PictureProbe.V(vt.position) : "none")} bounds {bounds}");
                }
                PictureProbe.ItemMeasure[] m = PictureProbe.Measure(s, solo, all, layers);
                float minVis = 100f;
                int maxGap = 0, gapCols = 0, cartCovers = 0, maxMutual = 0;
                for (int k = 0; k < n; k++)
                {
                    PictureProbe.ItemMeasure im = m[k];
                    float vis = im.Solo > 0 ? 100f * im.Visible / im.Solo : 0f;
                    minVis = Mathf.Min(minVis, vis);
                    maxGap = Math.Max(maxGap, im.GapMax);
                    gapCols += im.GapCols;
                    cartCovers += im.ByCart;
                    txt.AppendLine($"measure {k} outline {im.Solo} visible {im.Visible} by_cart {im.ByCart} bounds {im.Bounds.x} {im.Bounds.y} {im.Bounds.width} {im.Bounds.height} covered_by {string.Join(" ", Array.ConvertAll(im.CoveredBy, x => x.ToString()))} gap_cols {im.GapCols} gap_max {im.GapMax} gap_sum {im.GapSum} under_cols {im.UnderCols}");
                }
                for (int a = 0; a < n; a++)
                {
                    for (int b = a + 1; b < n; b++)
                    {
                        int[] pair = s.Owners((1 << a) | (1 << b));
                        PictureProbe.Mutual(pair, solo[a], solo[b], a, b, out int ab, out int ba);
                        maxMutual = Math.Max(maxMutual, Math.Min(ab, ba));
                        txt.AppendLine($"pair {a} {b} a_by_b {ab} b_by_a {ba}");
                    }
                }
                // the pictures: the normal one and the mask (r: owner with the cart, +1; g: the outlines' bits; b: owner
                // without the cart, +1), cut to the load's outlines plus a margin
                RectInt crop = Union(m, s.W, s.H, 16);
                txt.AppendLine($"crop {crop.x} {crop.y} {crop.width} {crop.height}");
                string stem = Path.Combine(dir, $"{index:0000}_{key.Replace('+', '-').Replace(' ', '_')}");
                File.WriteAllBytes(stem + "_normal.png", s.Crop(normal, crop, c => c));
                var mask = new Color32[s.W * s.H];
                for (int i = 0; i < mask.Length; i++)
                {
                    int bits = 0;
                    for (int k = 0; k < n; k++) if (solo[k][i] == k) bits |= 1 << k;
                    mask[i] = new Color32((byte)(all[i] + 1 < 0 ? 255 : all[i] + 1), (byte)bits, (byte)(noCart[i] + 1 < 0 ? 255 : noCart[i] + 1), 255);
                }
                File.WriteAllBytes(stem + "_mask.png", s.Crop(mask, crop, c => c));

                // the "before" variants: every lone item on a pair placed by a one-pair offset, measured where the class's
                // every-pair offset would put it (the view moved by the difference, put back)
                for (int k = 0; k < n; k++)
                {
                    Vector3? delta = OnePairDelta(placed, k, layers);
                    if (delta == null) continue;
                    s.Move(k, delta.Value);
                    Color32[] nk = s.NormalPixels();
                    int[] allK = s.Owners(allBits | cartBit);
                    var soloK = (int[][])solo.Clone();
                    soloK[k] = s.Owners(1 << k);
                    PictureProbe.ItemMeasure[] mk = PictureProbe.Measure(s, soloK, allK, layers);
                    var line = new StringBuilder($"variant {k} without_one_pair_offset delta {PictureProbe.V(delta.Value)}");
                    for (int j = 0; j < n; j++)
                    {
                        line.Append($" | {j} outline {mk[j].Solo} visible {mk[j].Visible} by_cart {mk[j].ByCart} gap_cols {mk[j].GapCols} gap_max {mk[j].GapMax}");
                        if (j != k)
                        {
                            int[] pair = s.Owners((1 << k) | (1 << j));
                            PictureProbe.Mutual(pair, soloK[k], soloK[j], k, j, out int kj, out int jk);
                            line.Append($" mutual {kj} {jk}");
                        }
                    }
                    txt.AppendLine(line.ToString());
                    File.WriteAllBytes($"{Path.Combine(dir, $"{index:0000}_{key.Replace('+', '-').Replace(' ', '_')}")}_variant{k}_before_normal.png", s.Crop(nk, crop, c => c));
                    s.PutBack(k);
                }

                // the sweeps: every item but the first, along / up / across (the container's x, y, z)
                if (sweep && s.Mode == "slice")
                {
                    txt.AppendLine("sweeps skipped: slice mode (every move would need a new far-plane sweep; computed offline from the atlas instead)");
                }
                else if (sweep)
                {
                    string[] axisName = { "along", "up", "across" };
                    for (int k = 1; k < n; k++)
                    {
                        for (int ax = 0; ax < 3; ax++)
                        {
                            foreach (float d in SweepSteps)
                            {
                                Vector3 off = Vector3.zero;
                                off[ax] = d;
                                s.Move(k, off);
                                if (Mathf.Abs(d) >= 0.039f)
                                {
                                    // a picture of the bigger steps, for the ok / wrong sheet
                                    Color32[] np = s.NormalPixels();
                                    File.WriteAllBytes($"{stem}_sweep{k}_{axisName[ax]}_{(d > 0 ? "+" : "-")}{Mathf.RoundToInt(Mathf.Abs(d) * 100f)}_normal.png", s.Crop(np, crop, c => c));
                                }
                                int[] allK = s.Owners(allBits | cartBit);
                                var soloK = (int[][])solo.Clone();
                                soloK[k] = s.Owners(1 << k);
                                PictureProbe.ItemMeasure[] mk = PictureProbe.Measure(s, soloK, allK, layers);
                                var line = new StringBuilder($"sweep {k} {axisName[ax]} {PictureProbe.F(d)}");
                                for (int j = 0; j < n; j++)
                                {
                                    PictureProbe.ItemMeasure im = mk[j];
                                    line.Append($" | {j} outline {im.Solo} visible {im.Visible} by_cart {im.ByCart} gap_cols {im.GapCols} gap_max {im.GapMax}");
                                    if (j != k)
                                    {
                                        int[] pair = s.Owners((1 << k) | (1 << j));
                                        PictureProbe.Mutual(pair, soloK[k], soloK[j], k, j, out int kj, out int jk);
                                        line.Append($" mutual {kj} {jk}");
                                    }
                                }
                                txt.AppendLine(line.ToString());
                            }
                            s.PutBack(k);
                        }
                    }
                }
                txt.AppendLine($"note {s.Note}");
                summary.AppendLine($"{index},{key},{(verdict.Length > 0 ? verdict : "none")},{n},{Max(layers) + 1},{minVis:0.0},{maxMutual},{maxGap},{gapCols},{cartCovers}");
            }
            File.WriteAllText(Path.Combine(dir, $"{index:0000}_{key.Replace('+', '-').Replace(' ', '_')}.txt"), txt.ToString());
            return null;
        }

        private static Color32[] NormalPixels(PictureProbe.Session s) => s.NormalPixels();

        // Which way the cart's container axes point on the screen: "x<dir>-z<dir>" (r l u d), e.g. xr-zu.
        private static string Facing()
        {
            object rig = PictureProbe.Rig();
            Transform c = rig != null ? PictureProbe.RigMember(rig, "container") as Transform : null;
            Camera w = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            if (c == null || w == null) return "unknown";
            string Dir(Vector3 local)
            {
                Vector3 a = w.WorldToScreenPoint(c.position), b = w.WorldToScreenPoint(c.TransformPoint(local));
                Vector2 d = new Vector2(b.x - a.x, b.y - a.y);
                return Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x >= 0 ? "r" : "l") : (d.y >= 0 ? "u" : "d");
            }
            return $"x{Dir(Vector3.right)}-z{Dir(Vector3.forward)}";
        }

        private static string CameraLines()
        {
            Camera w = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            if (w == null) return "camera none\n";
            RenderTexture t = w.targetTexture;
            return $"camera ortho {w.orthographic} size {PictureProbe.F(w.orthographicSize)} near {PictureProbe.F(w.nearClipPlane)} far {PictureProbe.F(w.farClipPlane)} target {(t != null ? $"{t.width} {t.height}" : $"{w.pixelWidth} {w.pixelHeight}")} screen {Screen.width} {Screen.height}\n" +
                   $"world_to_camera {PictureProbe.M(w.worldToCameraMatrix)}\nprojection {PictureProbe.M(w.projectionMatrix)}\n";
        }

        // Every carryable item alone on the cart (the size classes' members, the zombie by its representative): its
        // placement, outline, normal picture and depth (front and back), written to atlas/; the cart body once.
        private IEnumerator AtlasRoutine(string dir, MethodInfo put)
        {
            string adir = Path.Combine(dir, "atlas");
            Directory.CreateDirectory(adir);
            Type sheet = Api.GetType("GK2DonkeyAPI.CargoSheet");
            var reps = (List<string>)sheet.GetMethod("ClassReps", BS).Invoke(null, null);
            MethodInfo members = sheet.GetMethod("ClassMembers", BS);
            var ids = new List<string>();
            var repOf = new Dictionary<string, string>();
            foreach (string rep in reps)
            {
                var ms = (List<string>)members.Invoke(null, new object[] { rep });
                bool real = ms != null && ms.Count > 0 && !ms[0].StartsWith("every ");
                if (!real) { if (!ids.Contains(rep)) { ids.Add(rep); repOf[rep] = rep; } continue; }
                foreach (string m in ms) if (!ids.Contains(m)) { ids.Add(m); repOf[m] = rep; }
            }
            var index = new StringBuilder("id,class_rep,outline_px,box_x,box_y,box_w,box_h,depth_min,depth_max\n");
            bool cartDone = false;
            for (int a = 0; a < ids.Count && !probeStop; a++)
            {
                string id = ids[a];
                probeStatus = $"atlas {a + 1} of {ids.Count}: {id}";
                string r = (string)put.Invoke(null, new object[] { new List<string> { id }, "probe atlas" });
                if (r == null || !r.StartsWith("on the cart"))
                {
                    log.LogWarning($"[probe] atlas {id}: not put on the cart: {r}");
                    if (r != null && r.Contains("not a test load")) { probeStop = true; Say("Probe run stopped: the cart holds cargo that is not a test load."); }
                    continue;
                }
                for (int waited = 0; waited < 180; waited++)
                {
                    yield return null;
                    if (OnCart(new List<string> { id })) break;
                }
                for (int f = 0; f < SettleFrames; f++) yield return null;
                try
                {
                    AtlasOne(adir, id, repOf[id], !cartDone, index);
                    cartDone = true;
                }
                catch (Exception e)
                {
                    log.LogWarning($"[probe] atlas {id}: {e}");
                }
            }
            File.WriteAllText(Path.Combine(adir, "atlas.csv"), index.ToString());
        }

        private void AtlasOne(string adir, string id, string rep, bool withCart, StringBuilder index)
        {
            if (!PictureProbe.CargoViews(out IList views, out IList placed, out string why)) { log.LogWarning("[probe] atlas " + id + ": " + why); return; }
            using (PictureProbe.Session s = PictureProbe.Session.Begin(out why))
            {
                if (s == null) { log.LogWarning("[probe] atlas " + id + ": " + why); return; }
                Color32[] normal = s.NormalPixels();
                int[] solo = s.Owners(1);
                var layers = new[] { 0 };
                PictureProbe.ItemMeasure im = PictureProbe.Measure(s, new[] { solo }, solo, layers)[0];
                RectInt box = Grow(im.Bounds, 2, s.W, s.H);
                float[] front = s.Depth(0, true, box, 0.01f, out float dmin, out float dmax);
                float[] back = s.Depth(0, false, box, 0.01f, out _, out _);
                string stem = Path.Combine(adir, id);
                WriteDepth(stem + ".depth", box, front, back);
                File.WriteAllBytes(stem + "_normal.png", s.Crop(normal, Grow(im.Bounds, 12, s.W, s.H), c => c));
                object pl = placed[0];
                Transform vt = s.Views[0];
                Transform container = PictureProbe.RigMember(PictureProbe.Rig(), "container") as Transform;
                var t = new StringBuilder();
                t.AppendLine($"id {id}");
                t.AppendLine($"class_rep {rep}");
                t.AppendLine($"roi {s.Roi.x} {s.Roi.y} {s.Roi.width} {s.Roi.height} probe_mode {s.Mode} shader {s.ShaderName} test {s.Test}");
                t.AppendLine($"pos {PictureProbe.V(PictureProbe.Field<Vector3>(pl, "Pos"))} box {PictureProbe.V(PictureProbe.Field<Vector3>(pl, "Box"))}");
                t.AppendLine($"view_local {(vt != null ? PictureProbe.V(vt.localPosition) : "none")} view_rot {(vt != null ? PictureProbe.V(vt.localEulerAngles) : "none")} view_scale {(vt != null ? PictureProbe.V(vt.localScale) : "none")} view_world {(vt != null ? PictureProbe.V(vt.position) : "none")} view_parent_is_container {(vt != null && vt.parent == container)}");
                if (container != null) t.AppendLine($"container_to_world {PictureProbe.M(container.localToWorldMatrix)}");
                t.AppendLine($"outline {im.Solo} bounds {im.Bounds.x} {im.Bounds.y} {im.Bounds.width} {im.Bounds.height} depth_box {box.x} {box.y} {box.width} {box.height} depth_range {PictureProbe.F(dmin)} {PictureProbe.F(dmax)}");
                File.WriteAllText(stem + ".txt", t.ToString());
                index.AppendLine($"{id},{rep},{im.Solo},{im.Bounds.x},{im.Bounds.y},{im.Bounds.width},{im.Bounds.height},{PictureProbe.F(dmin)},{PictureProbe.F(dmax)}");
                if (withCart && s.HasRenderers(PictureProbe.Cart))
                {
                    int[] cart = s.Owners(1 << PictureProbe.Cart);
                    int x0 = s.W, y0 = s.H, x1 = -1, y1 = -1;
                    for (int i = 0; i < cart.Length; i++)
                    {
                        if (cart[i] != PictureProbe.Cart) continue;
                        int x = i % s.W, y = i / s.W;
                        x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    }
                    if (x1 >= 0)
                    {
                        RectInt cb = Grow(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1), 2, s.W, s.H);
                        float[] cf = s.Depth(PictureProbe.Cart, true, cb, 0.01f, out float cmin, out float cmax);
                        float[] cbk = s.Depth(PictureProbe.Cart, false, cb, 0.01f, out _, out _);
                        WriteDepth(Path.Combine(adir, "_cart.depth"), cb, cf, cbk);
                        File.WriteAllText(Path.Combine(adir, "_cart.txt"), $"cart_renderers {s.CartRenderers}\ndepth_box {cb.x} {cb.y} {cb.width} {cb.height} depth_range {PictureProbe.F(cmin)} {PictureProbe.F(cmax)}\n");
                    }
                }
            }
        }

        private static RectInt Grow(RectInt r, int m, int w, int h)
        {
            if (r.width == 0) return new RectInt(0, 0, w, h);
            int x0 = Math.Max(0, r.x - m), y0 = Math.Max(0, r.y - m), x1 = Math.Min(w, r.x + r.width + m), y1 = Math.Min(h, r.y + r.height + m);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        // "GKD1", the box (x, y, w, h: texture pixels, rows upward) as int32, then w*h float32 front distances and w*h
        // back ones (NaN: nothing there), little-endian.
        private static void WriteDepth(string path, RectInt box, float[] front, float[] back)
        {
            using (var bw = new BinaryWriter(File.Create(path)))
            {
                bw.Write(new[] { (byte)71, (byte)75, (byte)68, (byte)49 });
                bw.Write(box.x); bw.Write(box.y); bw.Write(box.width); bw.Write(box.height);
                foreach (float f in front) bw.Write(f);
                foreach (float f in back) bw.Write(f);
            }
        }

        // For a lone item k on a pair placed by a one-pair offset: the move from there to where the class's every-pair
        // offset would put it (container space); null otherwise.
        private static Vector3? OnePairDelta(IList placed, int k, int[] layers)
        {
            int inLayer = 0;
            var below = new List<int>();
            for (int j = 0; j < layers.Length; j++)
            {
                if (layers[j] == layers[k]) inLayer++;
                if (layers[j] == layers[k] - 1) below.Add(j);
            }
            if (layers[k] == 0 || inLayer != 1 || below.Count != 2) return null;
            Type layout = Api.GetType("GK2DonkeyAPI.CargoLayout");
            string id = PictureProbe.Field<string>(placed[k], "Id");
            object one = layout.GetMethod("ForOnPair", BS).Invoke(null, new object[] { id, PictureProbe.Field<string>(placed[below[0]], "Id"), PictureProbe.Field<string>(placed[below[1]], "Id") });
            if (one == null) return null;
            object e = layout.GetMethod("ForItem", BS).Invoke(null, new object[] { id });
            Vector3 cls = e != null ? (Vector3)e.GetType().GetField("OnPair").GetValue(e) : Vector3.zero;
            Vector3 off = (Vector3)one.GetType().GetField("Offset").GetValue(one);
            return cls - off;
        }

        // The cart's views show exactly these items (by id, in any order).
        private static bool OnCart(List<string> ids)
        {
            if (!PictureProbe.CargoViews(out IList views, out IList placed, out _) || placed.Count != ids.Count) return false;
            var want = new List<string>(ids);
            foreach (object p in placed)
            {
                if (!want.Remove(PictureProbe.Field<string>(p, "Id"))) return false;
            }
            foreach (object v in views)
            {
                if (!(v is Component c) || c == null || !c.gameObject.activeInHierarchy) return false;
            }
            return true;
        }

        private static int Max(int[] a)
        {
            int m = 0;
            foreach (int x in a) if (x > m) m = x;
            return m;
        }

        private static RectInt Union(PictureProbe.ItemMeasure[] m, int w, int h, int margin)
        {
            int x0 = w, y0 = h, x1 = 0, y1 = 0;
            foreach (var im in m)
            {
                if (im.Solo == 0) continue;
                x0 = Math.Min(x0, im.Bounds.x);
                y0 = Math.Min(y0, im.Bounds.y);
                x1 = Math.Max(x1, im.Bounds.x + im.Bounds.width);
                y1 = Math.Max(y1, im.Bounds.y + im.Bounds.height);
            }
            if (x1 <= x0) return new RectInt(0, 0, w, h);
            x0 = Math.Max(0, x0 - margin);
            y0 = Math.Max(0, y0 - margin);
            x1 = Math.Min(w, x1 + margin);
            y1 = Math.Min(h, y1 + margin);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
