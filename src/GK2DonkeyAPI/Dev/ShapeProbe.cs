using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.Rendering;

namespace GK2DonkeyAPI
{
    // Measures the real shape of every cargo size class (docs/cargo-tuning-tools.md, phase B): the top and bottom
    // surface of the meshes the cart draws, as height maps on a 1 cm grid, saved to the repo's cargo-shapes.cfg so the
    // seat suggestions, the problem scan and PackerScan can use them without the game. The cargo meshes are not
    // readable on the CPU (isReadable false), so the triangles are read back from the GPU (Mesh.GetVertexBuffer /
    // GetIndexBuffer, GraphicsBuffer.GetData; Unity 6). Coordinates as the packer's: x along, y up, z across, relative to
    // the class box's footprint centre and bottom. Writes nothing to the game; a failure is reported, never thrown.
    internal static class ShapeProbe
    {
        internal const float Cell = 0.01f;
        public static string Status { get; private set; } = "";

        internal sealed class Shape
        {
            public string Class, Name;
            public Vector3 Box;
            public int Nx, Nz;
            public float X0, Z0;         // the first cell's centre, relative to the footprint centre
            public float[] Top, Bottom;  // [z * Nx + x], height above the box bottom; NaN where the mesh has nothing
            public int Triangles;
            public string Note;
        }

        public static string MeasureAll()
        {
            var shapes = new List<Shape>();
            var log = new StringBuilder();
            try
            {
                foreach (string rep in CargoSheet.ClassReps())
                {
                    Shape s = Measure(rep, log);
                    if (s != null) shapes.Add(s);
                }
                if (shapes.Count == 0)
                {
                    Plugin.Log.LogWarning("[shapes] no class could be measured:\n" + log);
                    return Status = "no class could be measured (log)";
                }
                string path = Defaults.RepoPath("cargo-shapes.cfg") ?? Path.Combine(Paths.BepInExRootPath, "GK2-cargo-shapes.cfg");
                File.WriteAllText(path, ToText(shapes));
                CargoShapes.Load();
                string page = Path.Combine(CargoSheet.OutDir(), "shape-heights.html");
                Directory.CreateDirectory(Path.GetDirectoryName(page));
                File.WriteAllText(page, Html(shapes));
                Plugin.Log.LogInfo($"[shapes] {shapes.Count} classes measured, written to {path} and {page}" + (log.Length > 0 ? ":\n" + log : "."));
                return Status = $"{shapes.Count} of {CargoSheet.ClassReps().Count} classes measured: {path}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[shapes] measuring failed: " + e);
                return Status = "failed: " + e.Message;
            }
        }

        private static Shape Measure(string rep, StringBuilder log)
        {
            Transform el;
            if (rep == CargoSheet.ZombieRep)
            {
                el = Cargo.ZombieDropPrefab();
            }
            else
            {
                ItemDef def = GameBalance.Me.GetDataOrNull<ItemDef>(rep);
                el = def != null ? Cargo.FindOverheadElement(def) : null;
            }
            string name = CargoSheet.ClassLabel(rep);
            if (el == null)
            {
                log.AppendLine($"  {name} ({rep}): no mesh element found (carry something once, or mount, so the overhead view exists)");
                return null;
            }
            if (!Cargo.MeasureMeshes(el, el, out Bounds box, includeInactive: true))
            {
                log.AppendLine($"  {name}: no meshes to measure");
                return null;
            }
            var tris = new List<Vector3>();
            int meshes = 0;
            foreach (MeshFilter mf in el.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || Cargo.IsShadowMesh(mf.gameObject.name) || mf.gameObject.name.EndsWith("-blackout")) continue;
                if (!ReadTriangles(mesh, mf.transform, el, tris, out string how))
                {
                    log.AppendLine($"  {name}: mesh {mf.gameObject.name} not readable from the GPU: {how}");
                    continue;
                }
                meshes++;
            }
            if (tris.Count == 0)
            {
                return null;
            }
            // relative to the box's footprint centre and bottom (the packer's Target)
            var s = new Shape { Class = rep, Name = name, Box = box.size, Triangles = tris.Count / 3 };
            s.Nx = Mathf.CeilToInt(box.size.x / Cell) + 1;
            s.Nz = Mathf.CeilToInt(box.size.z / Cell) + 1;
            s.X0 = -(s.Nx - 1) * Cell / 2f;
            s.Z0 = -(s.Nz - 1) * Cell / 2f;
            s.Top = new float[s.Nx * s.Nz];
            s.Bottom = new float[s.Nx * s.Nz];
            for (int k = 0; k < s.Top.Length; k++) { s.Top[k] = float.NaN; s.Bottom[k] = float.NaN; }
            Vector3 origin = new Vector3(box.center.x, box.min.y, box.center.z);
            Vector3 tmin = Vector3.positiveInfinity, tmax = Vector3.negativeInfinity;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = tris[t] - origin, b = tris[t + 1] - origin, c = tris[t + 2] - origin;
                tmin = Vector3.Min(tmin, Vector3.Min(a, Vector3.Min(b, c)));
                tmax = Vector3.Max(tmax, Vector3.Max(a, Vector3.Max(b, c)));
                Rasterize(s, a, b, c);
            }
            Vector3 tsize = tmax - tmin;
            if (Mathf.Abs(tsize.x - box.size.x) > 0.02f || Mathf.Abs(tsize.y - box.size.y) > 0.02f || Mathf.Abs(tsize.z - box.size.z) > 0.02f)
            {
                s.Note = $"triangles span {tsize.x:0.00} × {tsize.y:0.00} × {tsize.z:0.00}, the box {box.size.x:0.00} × {box.size.y:0.00} × {box.size.z:0.00}";
                log.AppendLine($"  {name}: {s.Note}");
            }
            log.AppendLine($"  {name}: {s.Triangles} triangles from {meshes} mesh(es), grid {s.Nx} × {s.Nz}");
            return s;
        }

        // The check that the saved shapes are in the packer's frame: every item drawn on the ridden cart is measured again
        // from its own view, in the cargo holder's space (exactly as placed), and compared with the saved shape as it is,
        // mirrored along, mirrored across and turned half round. The saved file is right when "as saved" wins.
        public static string CheckOnCart()
        {
            try
            {
                Cargo.Rig rig = Cargo.Current;
                if (rig == null || rig.Container == null) return Status = "ride in Cart style with items on the cart";
                if (CargoShapes.Count == 0) return Status = "measure the item shapes first";
                List<Cargo.PlacedItem> placed = Cargo.Placed;
                var views = rig.PlacedViews;
                var lines = new List<string>();
                int asSaved = 0, other = 0;
                for (int i = 0; i < placed.Count && i < views.Count; i++)
                {
                    DropViewAtomMesh v = views[i];
                    CargoShapes.Shape saved = CargoShapes.ByClass(CargoSheet.ClassOf(placed[i].Id));
                    string name = CargoSheet.ClassLabel(placed[i].Id);
                    if (v == null || saved == null)
                    {
                        lines.Add($"{name}: {(saved == null ? "no saved shape" : "no view")}");
                        continue;
                    }
                    var tris = new List<Vector3>();
                    foreach (MeshFilter mf in v.GetComponentsInChildren<MeshFilter>(false))
                    {
                        Mesh mesh = mf.sharedMesh;
                        if (mesh == null || Cargo.IsShadowMesh(mf.gameObject.name) || mf.gameObject.name.EndsWith("-blackout")) continue;
                        ReadTriangles(mesh, mf.transform, rig.Container, tris, out _);
                    }
                    if (tris.Count == 0)
                    {
                        lines.Add($"{name}: nothing readable on the cart");
                        continue;
                    }
                    var b = new Bounds(tris[0], Vector3.zero);
                    foreach (Vector3 t in tris) b.Encapsulate(t);
                    var live = new Shape { Box = b.size, Nx = saved.Nx, Nz = saved.Nz, X0 = saved.X0, Z0 = saved.Z0, Top = new float[saved.Nx * saved.Nz], Bottom = new float[saved.Nx * saved.Nz] };
                    for (int k = 0; k < live.Top.Length; k++) { live.Top[k] = float.NaN; live.Bottom[k] = float.NaN; }
                    Vector3 o = new Vector3(b.center.x, b.min.y, b.center.z);
                    for (int t = 0; t + 2 < tris.Count; t += 3) Rasterize(live, tris[t] - o, tris[t + 1] - o, tris[t + 2] - o);
                    float d0 = Diff(live, saved, false, false), dx = Diff(live, saved, true, false), dz = Diff(live, saved, false, true), dxz = Diff(live, saved, true, true);
                    float best = Mathf.Min(Mathf.Min(d0, dx), Mathf.Min(dz, dxz));
                    string verdict = best == d0 ? "as saved" : best == dx ? "MIRRORED ALONG" : best == dz ? "MIRRORED ACROSS" : "TURNED HALF ROUND";
                    if (best == d0) asSaved++; else other++;
                    lines.Add($"{name}: {verdict} (mean height difference: as saved {d0:0.000}, mirrored along {dx:0.000}, across {dz:0.000}, both {dxz:0.000}; box on the cart {b.size.x:0.00} × {b.size.y:0.00} × {b.size.z:0.00})");
                }
                Plugin.Log.LogInfo("[shapes] check on the cart:\n  " + string.Join("\n  ", lines));
                return Status = other == 0 ? $"all {asSaved} item(s) on the cart match the saved shapes as saved" : $"{other} item(s) differ from the saved shapes (see the log); {asSaved} match";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[shapes] the check on the cart failed: " + e);
                return Status = "check failed: " + e.Message;
            }
        }

        // Mean absolute difference of the top surfaces where both have one; saved read mirrored as asked.
        private static float Diff(Shape live, CargoShapes.Shape saved, bool mirrorX, bool mirrorZ)
        {
            double sum = 0;
            int n = 0;
            for (int z = 0; z < live.Nz; z++)
            {
                for (int x = 0; x < live.Nx; x++)
                {
                    float a = live.Top[z * live.Nx + x];
                    int sx = mirrorX ? saved.Nx - 1 - x : x, sz = mirrorZ ? saved.Nz - 1 - z : z;
                    float c = saved.Top[sz * saved.Nx + sx];
                    if (float.IsNaN(a) || float.IsNaN(c)) continue;
                    sum += Mathf.Abs(a - c);
                    n++;
                }
            }
            return n == 0 ? 9f : (float)(sum / n);
        }

        // The mesh's triangles in space's coordinates, read back from the GPU buffers.
        private static bool ReadTriangles(Mesh mesh, Transform from, Transform space, List<Vector3> into, out string how)
        {
            how = null;
            try
            {
                if (!mesh.HasVertexAttribute(VertexAttribute.Position))
                {
                    how = "no positions";
                    return false;
                }
                int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
                int offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
                VertexAttributeFormat fmt = mesh.GetVertexAttributeFormat(VertexAttribute.Position);
                if (fmt != VertexAttributeFormat.Float32 && fmt != VertexAttributeFormat.Float16)
                {
                    how = "position format " + fmt;
                    return false;
                }
                var pos = new Vector3[mesh.vertexCount];
                using (GraphicsBuffer vb = mesh.GetVertexBuffer(stream))
                {
                    if (vb == null)
                    {
                        how = "no vertex buffer";
                        return false;
                    }
                    var raw = new byte[vb.count * vb.stride];
                    vb.GetData(raw);
                    int stride = vb.stride;
                    for (int i = 0; i < pos.Length && (i * stride + offset + 12) <= raw.Length; i++)
                    {
                        int o = i * stride + offset;
                        pos[i] = fmt == VertexAttributeFormat.Float32
                            ? new Vector3(BitConverter.ToSingle(raw, o), BitConverter.ToSingle(raw, o + 4), BitConverter.ToSingle(raw, o + 8))
                            : new Vector3(Mathf.HalfToFloat(BitConverter.ToUInt16(raw, o)), Mathf.HalfToFloat(BitConverter.ToUInt16(raw, o + 2)), Mathf.HalfToFloat(BitConverter.ToUInt16(raw, o + 4)));
                    }
                }
                int[] idx;
                using (GraphicsBuffer ib = mesh.GetIndexBuffer())
                {
                    if (ib == null)
                    {
                        how = "no index buffer";
                        return false;
                    }
                    var raw = new byte[ib.count * ib.stride];
                    ib.GetData(raw);
                    bool wide = mesh.indexFormat == IndexFormat.UInt32;
                    idx = new int[raw.Length / (wide ? 4 : 2)];
                    for (int i = 0; i < idx.Length; i++) idx[i] = wide ? BitConverter.ToInt32(raw, i * 4) : BitConverter.ToUInt16(raw, i * 2);
                }
                bool any = false;
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    SubMeshDescriptor d = mesh.GetSubMesh(sm);
                    if (d.topology != MeshTopology.Triangles) continue;
                    for (int j = d.indexStart; j + 2 < d.indexStart + d.indexCount && j + 2 < idx.Length; j += 3)
                    {
                        for (int q = 0; q < 3; q++)
                        {
                            int v = idx[j + q] + d.baseVertex;
                            if (v < 0 || v >= pos.Length) { how = "index out of range"; return false; }
                            into.Add(space.InverseTransformPoint(from.TransformPoint(pos[v])));
                        }
                        any = true;
                    }
                }
                if (!any) how = "no triangles";
                return any;
            }
            catch (Exception e)
            {
                how = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        private static void Rasterize(Shape s, Vector3 a, Vector3 b, Vector3 c)
        {
            float area = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
            if (Mathf.Abs(area) < 1e-9f) return; // edge-on from above: its neighbours carry the surface
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - s.X0) / Cell));
            int x1 = Mathf.Min(s.Nx - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - s.X0) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - s.Z0) / Cell));
            int z1 = Mathf.Min(s.Nz - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - s.Z0) / Cell));
            for (int zi = z0; zi <= z1; zi++)
            {
                float pz = s.Z0 + zi * Cell;
                for (int xi = x0; xi <= x1; xi++)
                {
                    float px = s.X0 + xi * Cell;
                    float w0 = ((b.x - px) * (c.z - pz) - (c.x - px) * (b.z - pz)) / area;
                    float w1 = ((c.x - px) * (a.z - pz) - (a.x - px) * (c.z - pz)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float y = w0 * a.y + w1 * b.y + w2 * c.y;
                    int k = zi * s.Nx + xi;
                    if (float.IsNaN(s.Top[k]) || y > s.Top[k]) s.Top[k] = y;
                    if (float.IsNaN(s.Bottom[k]) || y < s.Bottom[k]) s.Bottom[k] = y;
                }
            }
        }

        private static string F(float v) => float.IsNaN(v) ? "-" : v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string ToText(List<Shape> shapes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# GK2 Donkey API cargo shapes: the top and bottom surface of every cargo size class, measured from the meshes");
            sb.AppendLine("# the cart draws (Debug tab, Cargo tools, 'Measure the item shapes'; ShapeProbe.cs, docs/cargo-tuning-tools.md).");
            sb.AppendLine("# Generated: do not edit by hand. x along the cart, z across, y up; relative to the class box's footprint centre and bottom.");
            sb.AppendLine("# grid = nx nz x0 z0 (x0 z0: the first cell's centre); top_<row> / bottom_<row>: one row of nx heights per z, '-' where the mesh has nothing.");
            sb.AppendLine("version = 1");
            sb.AppendLine("cell = " + F(Cell));
            foreach (Shape s in shapes)
            {
                sb.AppendLine().AppendLine($"[class {s.Class}]");
                sb.AppendLine("name = " + s.Name);
                sb.AppendLine($"box = {F(s.Box.x)} {F(s.Box.y)} {F(s.Box.z)}");
                sb.AppendLine($"triangles = {s.Triangles}");
                sb.AppendLine($"grid = {s.Nx} {s.Nz} {F(s.X0)} {F(s.Z0)}");
                if (s.Note != null) sb.AppendLine("# " + s.Note);
                for (int z = 0; z < s.Nz; z++)
                {
                    var row = new string[s.Nx];
                    for (int x = 0; x < s.Nx; x++) row[x] = F(s.Top[z * s.Nx + x]);
                    sb.AppendLine($"top_{z} = " + string.Join(" ", row));
                }
                for (int z = 0; z < s.Nz; z++)
                {
                    var row = new string[s.Nx];
                    for (int x = 0; x < s.Nx; x++) row[x] = F(s.Bottom[z * s.Nx + x]);
                    sb.AppendLine($"bottom_{z} = " + string.Join(" ", row));
                }
            }
            return sb.ToString();
        }

        // A page to check the measurement by eye: each class's top and bottom height map (light = high) and its profile
        // seen from the side (the highest top and lowest bottom along each x).
        private static string Html(List<Shape> shapes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Cargo shapes</title><style>body{font-family:sans-serif;background:#1e1e1e;color:#ddd;margin:16px}canvas{image-rendering:pixelated;border:1px solid #444;margin:4px;background:#111}.c{display:inline-block;vertical-align:top;margin:0 18px 18px 0}</style></head><body>");
            sb.AppendLine($"<h1>Cargo shapes</h1><p>{DateTime.Now:yyyy-MM-dd HH:mm}. Measured from the meshes the cart draws (1 cm grid). Per class: the top surface seen from above, the bottom surface seen from below (light = high), and the side profile (top in orange, bottom in blue; the grey frame is the class box).</p>");
            int n = 0;
            foreach (Shape s in shapes)
            {
                n++;
                var top = new StringBuilder("[");
                var bot = new StringBuilder("[");
                for (int k = 0; k < s.Top.Length; k++)
                {
                    top.Append(float.IsNaN(s.Top[k]) ? "null" : s.Top[k].ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    bot.Append(float.IsNaN(s.Bottom[k]) ? "null" : s.Bottom[k].ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                }
                top.Append(']');
                bot.Append(']');
                sb.AppendLine($"<div class=\"c\"><h3>{s.Name} <small>({s.Class}; {s.Box.x:0.00} × {s.Box.y:0.00} × {s.Box.z:0.00}; {s.Triangles} triangles){(s.Note != null ? " · " + s.Note : "")}</small></h3><canvas id=\"t{n}\"></canvas><canvas id=\"b{n}\"></canvas><br><canvas id=\"p{n}\"></canvas></div>");
                sb.AppendLine($"<script>(function(){{var nx={s.Nx},nz={s.Nz},H={s.Box.y.ToString(CultureInfo.InvariantCulture)},T={top},B={bot},S=4;"
                    + "function map(id,A){var c=document.getElementById(id);c.width=nx*S;c.height=nz*S;var g=c.getContext('2d');for(var z=0;z<nz;z++)for(var x=0;x<nx;x++){var v=A[z*nx+x];if(v===null)continue;var l=Math.round(40+215*Math.max(0,Math.min(1,v/H)));g.fillStyle='rgb('+l+','+l+','+l+')';g.fillRect(x*S,(nz-1-z)*S,S,S);}}"
                    + $"map('t{n}',T);map('b{n}',B);var c=document.getElementById('p{n}');var sc=300;c.width=nx*S;c.height=Math.round(H*sc)+8;var g=c.getContext('2d');g.strokeStyle='#555';g.strokeRect(0,4,nx*S,Math.round(H*sc));"
                    + "for(var x=0;x<nx;x++){var hi=null,lo=null;for(var z=0;z<nz;z++){var t=T[z*nx+x],b=B[z*nx+x];if(t!==null&&(hi===null||t>hi))hi=t;if(b!==null&&(lo===null||b<lo))lo=b;}if(hi!==null){g.fillStyle='#ff9933';g.fillRect(x*S,4+Math.round((H-hi)*sc),S,2);}if(lo!==null){g.fillStyle='#59a6ff';g.fillRect(x*S,4+Math.round((H-lo)*sc),S,2);}}"
                    + "})();</script>");
            }
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
