using System;
using System.Collections.Generic;
using System.Text;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Skin layering (docs/skin-layering-plan.md; the user, 2026-10-24: "all other skins are built on top of it"): every
    // skin of a mount but the donkey is built on that mount's own look, <mount>/default. What a skin's file (or code)
    // doesn't say comes from the default: its settings are resolved in place into the skin's own fields (Resolve), so
    // every reader stays as it is, and its pictures are taken from the default's frame by frame as the sheet is built
    // (Acquire, VariantProducer). layer = over on such a skin draws it over the default (holes from the erase colour),
    // the way Hellfire is drawn over the game's donkey. The donkey's skins have no parent: the game's art is the
    // donkey's default, and they stay exactly as they were.
    internal static partial class Looks
    {
        // What a skin says itself; everything else comes from its default (or, standing alone, is what it read).
        [Flags]
        internal enum SkinGiven
        {
            None = 0,
            Canvas = 1 << 0,
            Pivot = 1 << 1,
            Colours = 1 << 2,
            Layer = 1 << 3,
            Erase = 1 << 4,
            Seat = 1 << 5,
            Offset = 1 << 6,
            OffsetCart = 1 << 7,
            Description = 1 << 9,
            Portrait = 1 << 10,
            Base = 1 << 11,
        }

        internal sealed partial class SkinDef
        {
            internal SkinGiven Given;          // what its own file or code says (FromCfg, MountSkinDef.ToDef, the Mounts tab)
            internal SkinDef Parent;           // the default it is built on (Resolve); null: it stands alone
            internal bool OverDefault;         // layer = over on a skin of a mount other than the donkey: drawn over its default (Over stays "over the game's donkey")
            internal Dictionary<string, HashSet<string>> OwnColorRegions; // its own [Colors]; ColorRegions is the resolved map (the default's, then its own)
            internal Alone AloneValues;        // its values as read, for when it stands alone (taken at its first Resolve)
            internal string AsideBy;           // why layering set it aside: "default" (its default is set aside), "pictures" (nothing to draw), "build" (the default's pictures failed in its build); null: not by layering

            internal bool Has(SkinGiven g) => (Given & g) != 0;
        }

        // A skin's own values as it was read (FromCfg, ToDef): what it falls back to standing alone. A value the skin
        // gave itself is never replaced; this is only read for the ones it didn't give (and for a given seat, effect or
        // offset the Mounts tab takes back: then the neutral value, as if never said).
        internal sealed class Alone
        {
            internal int CanvasW, CanvasH;
            internal float PivotX, PivotY;
            internal bool OwnColours;
            internal SkinBase Base;
            internal int SeatRow;
            internal bool SeatFromPicture;
            internal Vector2 OffsetRide, OffsetCart;
            internal string Description;
        }

        // A skin that can be built on a default: one of a mount other than the donkey, not that mount's own look.
        internal static bool IsVariantSlot(SkinDef d) => d != null && IsVariantSlot(d.MountId, d.LocalId);

        internal static bool IsVariantSlot(string mountId, string localId)
        {
            if (string.IsNullOrWhiteSpace(mountId) || mountId.Trim().Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return false;
            return localId != null && !localId.Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase);
        }

        // A mount's own look (its skins\default\), the parent of its other skins.
        internal static bool IsDefaultSlot(SkinDef d) => d != null && !d.MountId.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase) && DefaultSkinName.Equals(d.LocalId, StringComparison.OrdinalIgnoreCase);

        // Whether a skin's pictures are built on its default's: both are pictures (not a mod's own sprites: not layered
        // yet), the default isn't drawn over the game's donkey, and the two have the same canvas and pivot (a frame of one
        // is a frame of the other).
        internal static bool InheritsPictures(SkinDef d)
        {
            SkinDef p = d?.Parent;
            if (p == null || d.Sprites != null || p.Sprites != null || p.Over) return false;
            return d.Base == p.Base && d.CanvasW == p.CanvasW && d.CanvasH == p.CanvasH && Mathf.Approximately(d.PivotX, p.PivotX) && Mathf.Approximately(d.PivotY, p.PivotY);
        }

        // Whether a skin has any picture of its own (a code skin's sprites, a game source, a recipe, a sheet, a PNG).
        internal static bool HasOwnPictures(SkinDef d)
        {
            if (d.Sprites != null || d.Game != null || d.Recipe != null || d.LoadSheetPng != null) return true;
            if (d.Keys == null) return false;
            foreach (string _ in d.Keys()) return true;
            return false;
        }

        // Whether a skin has its own picture for a key (the Mounts tab's count, the icon).
        internal static bool HasOwnKey(SkinDef d, string key)
        {
            if (d.Game != null || d.Recipe != null) return true;
            if (d.Sprites != null) return d.Sprites.TryGetFrame(key, out Sprite s) && s != null;
            if (d.LoadSheetPng != null) return d.SheetKeys != null && Array.IndexOf(d.SheetKeys, key) >= 0;
            if (d.Keys == null) return false;
            foreach (string k in d.Keys()) if (k == key) return true;
            return false;
        }

        // How many of the base's frames a variant draws itself and how many come from its default (the Mounts tab, the
        // log): own pictures count the base's keys it has, the rest the default has.
        internal static (int Own, int FromDefault) PictureCounts(SkinDef d)
        {
            int own = 0, from = 0;
            var ownKeys = new HashSet<string>(StringComparer.Ordinal);
            if (d.LoadSheetPng != null && d.SheetKeys != null) ownKeys.UnionWith(d.SheetKeys);
            else if (d.Game != null) ownKeys.UnionWith(d.Base.Keys);
            else if (d.Keys != null && d.Sprites == null) foreach (string k in d.Keys()) ownKeys.Add(k);
            var parentKeys = new HashSet<string>(StringComparer.Ordinal);
            SkinDef p = InheritsPictures(d) ? d.Parent : null;
            if (p != null)
            {
                if (p.LoadSheetPng != null && p.SheetKeys != null) parentKeys.UnionWith(p.SheetKeys);
                else if (p.Game != null) parentKeys.UnionWith(p.Base.Keys);
                else if (p.Keys != null) foreach (string k in p.Keys()) parentKeys.Add(k);
            }
            foreach (string k in d.Base.Keys)
            {
                if (ownKeys.Contains(k)) own++;
                else if (parentKeys.Contains(k)) from++;
            }
            return (own, from);
        }

        private static readonly HashSet<string> layerSaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "<id>|<what>": the once-lines

        // A skin's parent found and its settings resolved, in place: what it gave itself stays, the rest is its default's
        // (or, with none, what it read). Idempotent: called at its registration, whenever a default of its mount comes or
        // goes, and on every Catalog.Touch (the Mounts tab's edits to the default reach its variants that way).
        internal static void Resolve(SkinDef d)
        {
            if (!IsVariantSlot(d))
            {
                d.Parent = null; // the donkey's skins and every mount's own look: nothing to build on, nothing rewritten
                return;
            }
            if (d.AloneValues == null)
            {
                d.OwnColorRegions = d.ColorRegions;
                d.AloneValues = new Alone
                {
                    CanvasW = d.CanvasW, CanvasH = d.CanvasH, PivotX = d.PivotX, PivotY = d.PivotY, OwnColours = d.OwnColours, Base = d.Base,
                    // a value it gave is never read from here, unless the Mounts tab takes it back: then as if never said
                    SeatRow = d.Has(SkinGiven.Seat) ? -1 : d.SeatRow, SeatFromPicture = !d.Has(SkinGiven.Seat) && d.SeatFromPicture,
                    OffsetRide = d.Has(SkinGiven.Offset) ? Vector2.zero : d.OffsetRide, OffsetCart = d.Has(SkinGiven.Offset) || d.Has(SkinGiven.OffsetCart) ? Vector2.zero : d.OffsetCart,
                    Description = d.Description,
                };
            }
            SkinDef was = d.Parent;
            SkinDef p = Find(SkinKey(d.MountId, DefaultSkinName));
            if (p == d || p == null || !p.LocalId.Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase) || !p.MountId.Equals(d.MountId, StringComparison.OrdinalIgnoreCase)) p = null;
            d.Parent = p;
            Alone a = d.AloneValues;
            if (!d.Has(SkinGiven.Base)) d.Base = p?.Base ?? a.Base;
            if (!d.Has(SkinGiven.Canvas))
            {
                d.CanvasW = p?.CanvasW ?? a.CanvasW;
                d.CanvasH = p?.CanvasH ?? a.CanvasH;
            }
            if (!d.Has(SkinGiven.Pivot))
            {
                // a canvas of its own and no pivot: the centre of its own canvas (as read); neither: the default's
                bool own = p == null || d.Has(SkinGiven.Canvas);
                d.PivotX = own ? a.PivotX : p.PivotX;
                d.PivotY = own ? a.PivotY : p.PivotY;
            }
            if (!d.Has(SkinGiven.Colours)) d.OwnColours = p?.OwnColours ?? a.OwnColours;
            d.ColorRegions = MergeRegions(p?.ColorRegions, d.OwnColorRegions);
            if (!d.Has(SkinGiven.Seat))
            {
                // a seat row is a row of the canvas: only the same height and pivot row read it the same
                bool fits = p != null && d.CanvasH == p.CanvasH && Mathf.Approximately(d.PivotY, p.PivotY);
                d.SeatRow = fits ? p.SeatRow : a.SeatRow;
                d.SeatFromPicture = fits ? p.SeatFromPicture : a.SeatFromPicture;
            }
            if (!d.Has(SkinGiven.Offset)) d.OffsetRide = p?.OffsetRide ?? a.OffsetRide;
            if (!d.Has(SkinGiven.OffsetCart)) d.OffsetCart = d.Has(SkinGiven.Offset) ? d.OffsetRide : p?.OffsetCart ?? a.OffsetCart; // offset without offset_cart: the cart's is the ride's, as in a file
            if (!d.Has(SkinGiven.Description)) d.Description = p?.Description ?? a.Description;
            ResolveAside(d, was);
            if (p == null && was == null && layerSaid.Add(d.Id + "|alone"))
            {
                Plugin.Log.LogInfo($"Looks: skin '{d.Id}' is built on '{SkinKey(d.MountId, DefaultSkinName)}', which isn't registered yet; it stands alone until it is.");
            }
            if (p != null) SayLayerOnce(d, p);
        }

        // The default's [Colors] with every colour the skin names taken out of them, then the skin's own lines.
        private static Dictionary<string, HashSet<string>> MergeRegions(Dictionary<string, HashSet<string>> parent, Dictionary<string, HashSet<string>> own)
        {
            if (parent == null || parent.Count == 0) return own;
            var named = new HashSet<string>(StringComparer.Ordinal);
            if (own != null) foreach (var kv in own) named.UnionWith(kv.Value);
            var merged = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in parent)
            {
                var set = new HashSet<string>(kv.Value, StringComparer.Ordinal);
                set.ExceptWith(named);
                if (set.Count > 0) merged[kv.Key] = set;
            }
            if (own != null)
            {
                foreach (var kv in own)
                {
                    if (!merged.TryGetValue(kv.Key, out HashSet<string> set)) merged[kv.Key] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.UnionWith(kv.Value);
                }
            }
            return merged;
        }

        // The lines a variant gets once about its pictures: a canvas of its own (not built on the default's frames), a
        // mod's own sprites (not layered yet), a default drawn over the game's donkey.
        private static void SayLayerOnce(SkinDef d, SkinDef p)
        {
            if (d.Sprites != null || p.Sprites != null)
            {
                if (layerSaid.Add(d.Id + "|sprites")) Plugin.Log.LogInfo($"Looks: skin '{d.Id}' is built on '{p.Id}' for its settings; {(d.Sprites != null ? "its pictures are a mod's own sprites" : "the default's pictures are a mod's own sprites")}, which aren't layered yet: it draws its own pictures only.");
                return;
            }
            if (p.Over)
            {
                if (layerSaid.Add(d.Id + "|over")) Plugin.Log.LogInfo($"Looks: skin '{d.Id}': its default '{p.Id}' is drawn over the donkey; its pictures are not built on the default's (its own only).");
                return;
            }
            if (!InheritsPictures(d) && layerSaid.Add(d.Id + "|canvas"))
            {
                bool seat = d.CanvasH == p.CanvasH && Mathf.Approximately(d.PivotY, p.PivotY);
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}' has a {d.CanvasW}x{d.CanvasH} canvas (pivot {Num(d.PivotX)} {Num(d.PivotY)}), its default '{p.Id}' {p.CanvasW}x{p.CanvasH} (pivot {Num(p.PivotX)} {Num(p.PivotY)}): its pictures are not built on the default's"
                    + (d.OverDefault ? " and it is drawn alone, its erase pixels left out" : "") + (seat ? "" : "; the default's seat is not taken either (another canvas height or pivot row)") + ". Give it the default's canvas and pivot, or every picture of its own.");
            }
        }

        // The set-aside states layering owns: carried from a default set aside, nothing to draw at all, a build that
        // failed on the default's pictures (cleared when the default changes or its frames are back).
        private static void ResolveAside(SkinDef d, SkinDef was)
        {
            if (d.AsideBy == "default" || d.AsideBy == "pictures" || (d.AsideBy == "build" && was != d.Parent))
            {
                d.Unavailable = null;
                d.AsideBy = null;
            }
            if (d.Unavailable != null) return; // set aside by something else (its own game frames): left to that
            SkinDef p = d.Parent;
            if (p != null && p.Unavailable != null && InheritsPictures(d))
            {
                d.Unavailable = $"its default look '{p.Id}' is set aside ({p.Unavailable})";
                d.AsideBy = "default";
            }
            else if (p == null && !HasOwnPictures(d))
            {
                d.Unavailable = "no pictures of its own and no default look to build on";
                d.AsideBy = "pictures";
            }
        }

        // Every variant of a mount (every mount: null) resolved again; one whose default changed says so and its
        // wearers rebuild (Acquire compares the default the sheet was built on).
        internal static void RefreshInherited(string mountId = null)
        {
            if (defs.Count == 0) return;
            bool changed = false;
            foreach (SkinDef d in new List<SkinDef>(defs.Values))
            {
                if (!IsVariantSlot(d)) continue;
                if (mountId != null && !d.MountId.Equals(mountId, StringComparison.OrdinalIgnoreCase)) continue;
                SkinDef was = d.Parent;
                Resolve(d);
                if (was == d.Parent) continue;
                changed = true;
                ForgetPortraits(d.Id); // its icon and portrait come from the default it has now
                SkinDef p = d.Parent;
                if (was == null)
                {
                    (int own, int from) = PictureCounts(d);
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}' is now built on '{p.Id}' ({own} own pictures, {from} from it; {InheritedSettings(d)} from it).");
                }
                else if (p == null)
                {
                    // a pack withdrawing itself takes its default and its other looks together: only another mod's look
                    // left standing alone is worth a warning
                    string line = $"Looks: skin '{d.Id}': its default look '{was.Id}' was withdrawn by {was.Owner}; it stands alone now.";
                    if (was.Owner == d.Owner) Plugin.Log.LogInfo(line); else Plugin.Log.LogWarning(line);
                }
                else
                {
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}' takes the new '{p.Id}' ({Where(p)}, version {p.Version ?? "none"}); its wearers rebuild.");
                }
            }
            if (changed) MarkWearersDirty();
        }

        // The settings a variant takes from its default (for the "now built on" line): the ones it doesn't give.
        internal static string InheritedSettings(SkinDef d)
        {
            var names = new List<string>();
            if (!d.Has(SkinGiven.Canvas)) names.Add("canvas");
            if (!d.Has(SkinGiven.Seat)) names.Add("seat");
            if (!d.Has(SkinGiven.Offset)) names.Add("offset");
            if (!d.Has(SkinGiven.Colours)) names.Add("colours");
            if (!d.Has(SkinGiven.Description)) names.Add("description");
            if (!d.Has(SkinGiven.Portrait)) names.Add("portrait");
            names.Add("tuning and sounds it doesn't set");
            return string.Join(", ", names);
        }

        // A skin set aside (its game frames gone): the variants built on its pictures go with it, and come back with it.
        internal static void SetAside(SkinDef d, string why)
        {
            d.Unavailable = why;
            foreach (SkinDef v in defs.Values)
            {
                if (v.Parent != d || !InheritsPictures(v) || (v.Unavailable != null && v.AsideBy != "default")) continue;
                v.Unavailable = $"its default look '{d.Id}' is set aside ({why})";
                v.AsideBy = "default";
            }
        }

        private static void Healed(SkinDef d)
        {
            foreach (SkinDef v in defs.Values)
            {
                if (v.Parent != d || (v.AsideBy != "default" && v.AsideBy != "build")) continue;
                v.Unavailable = null;
                v.AsideBy = null;
                Plugin.Log.LogInfo($"Looks: skin '{v.Id}': its default '{d.Id}' is there again; offered again.");
            }
        }

        // A picture drawn over its default's: the default's pixels, those under the erase colour cleared (the hole), the
        // variant's other opaque pixels on top (WrapErase's rule over the game's donkey). erased: the holes made.
        internal static Color32[] ComposeOver(Color32[] own, Color32[] parent, Color32? erase, out int erased)
        {
            var outp = (Color32[])parent.Clone();
            erased = 0;
            for (int i = 0; i < own.Length; i++)
            {
                Color32 c = own[i];
                if (c.a == 0) continue;                                                                                // the default's shows
                if (erase.HasValue && c.r == erase.Value.r && c.g == erase.Value.g && c.b == erase.Value.b) { outp[i] = default(Color32); erased++; } // the hole
                else outp[i] = c;                                                                                      // the variant's pixel over the default's
            }
            return outp;
        }

        // A variant's picture for one key as its sheet will have it (its own; over its default: composed on the
        // default's), for the panel's icon; null when it has none. Built apart from any sheet (nothing is loaded).
        internal static Color32[] ResolvedPicture(SkinDef d, string key, out int w, out int h)
        {
            w = h = 0;
            var counts = new Loaded { Def = d };
            Pixels o = MakeProducer(d, d.Base.GamePrefix, counts).Produce(key);
            if (o.P == null) return null;
            w = o.W;
            h = o.H;
            if (!d.OverDefault || !InheritsPictures(d)) return o.P;
            Pixels p = MakeProducer(d.Parent, d.Base.GamePrefix, counts).Produce(key);
            if (p.P == null || p.W != o.W || p.H != o.H) return o.P;
            return ComposeOver(o.P, p.P, d.Erase, out _);
        }

        // For the self-tests: a skin's picture for a key as its sheet gets it (its own, the default's, the two composed),
        // produced apart from any sheet (the sheet itself can't be read back once uploaded); null when it has none.
        internal static Color32[] TestPicture(string id, string key, out int w, out int h)
        {
            w = h = 0;
            SkinDef d = Find(id);
            if (d == null || d.Sprites != null) return null;
            var l = new Loaded { Def = d, Parent = d.Parent };
            Producer own = MakeProducer(d, d.Base.GamePrefix, l);
            Func<string, Pixels> produce = InheritsPictures(d) ? VariantProduce(l, own, MakeProducer(d.Parent, d.Base.GamePrefix, l), out _) : own.Produce;
            Pixels p = produce(key);
            w = p.W;
            h = p.H;
            return p.P;
        }

        // For the self-tests: the loaded sheet of a skin (its counts, its colours by region), or null.
        internal static Loaded TestLoaded(string id) => !string.IsNullOrEmpty(id) && loaded.TryGetValue(id, out Loaded l) ? l : null;

        // At the game's start (every pack and mod registered): a variant still without a default stands alone for the
        // session. A pack mount that should have one (its mount.cfg came with skins\default\) is worth a warning; a code
        // mount may name its look otherwise (DefaultSkinOf), so an Info there.
        internal static void CheckParentsAtReady()
        {
            foreach (SkinDef d in defs.Values)
            {
                if (!IsVariantSlot(d) || d.Parent != null || !layerSaid.Add(d.Id + "|ready")) continue;
                Catalog.Entry k = Catalog.Find(d.MountId);
                if (k == null) continue; // no such mount: SkinDef.Mount says so
                string line = $"Looks: skin '{d.Id}': its mount '{k.Id}' has no default look; it stands alone (the donkey where it has no picture).";
                if (k.FromPack) Plugin.Log.LogWarning(line); else Plugin.Log.LogInfo(line);
            }
        }
    }
}
