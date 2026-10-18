using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // Skins: the donkey drawn as something else (docs/skins-design.md). A skin is a picture per animation frame, named
    // after the donkey's frames ("bdy_walk_left_03"), drawn on an overlay sprite of our own under each of the donkey's
    // sprites (bdy, hed) while the donkey's own picture is kept from rendering. The donkey's sprites stay as the game
    // sets them: the rider's seat, bob and leg read their names every frame (Mount.cs BackHeightPx, RiderMotion.cs),
    // and the game's own skin changer re-points them (and rewrites `enabled`) every LateUpdate, so an overlay it is.
    //
    // What a skin is drawn on is a base (SkinBase): the view's parts, frame keys, canvas, pivot, pixels per unit and
    // where its own frames live. The donkey is the only base today; a second (another view, another creature) is a
    // second record here, and every skin names its base, so nothing of the donkey is assumed by the loader or the rig.
    //
    // Pictures come as PNGs per frame (a pack on disk or embedded in a DLL), as one sprite sheet (the same, one PNG),
    // from a mod's own Sprites (code), or from a recipe over the base's own frames. Whatever the source, the API packs
    // what it decodes into ONE texture per skin (the sheet: every frame a cell with a one-pixel gutter) and makes its
    // sprites from that, so every donkey wearing a skin draws from one texture (one material state to switch, one
    // texture in memory) and the frames are decoded a few per tick, never all in one frame; the skin is worn once the
    // whole sheet is ready (an atomic switch: no half-built looks). A sprite source's sprites are the mod's own.
    //
    // Lifecycle, the same points as Colors.Rig: Attach after PrepareParts (Mount.cs), Tick in the plugin's LateUpdate
    // (the ridden tick, StandTick, PreviewStage.Pose), the per-frame apply in Camera.onPreCull (after every LateUpdate,
    // the game's included, and before any camera culls; once per frame, on its first enabled camera: PreviewStage's
    // cameras are disabled components rendered by hand and apply themselves), Detach in ReleaseView before the prefab
    // audit (the overlays unparented, then destroyed; the hide cleared), Release when the instance goes (the skin's
    // pictures are refcounted and freed when no instance wears them).
    //
    // The per-frame apply allocates nothing in steady state: a sprite seen before is a dictionary hit (Resolved), the
    // property block is reused, and the overlay's state is only written when it differs. Perf.S.Looks measures it
    // (time and bytes, the [perf] lines).
    //
    // What the rig touches on the pooled parts: forceRenderingOff on bdy/hed (the API's own hide, as HideDonkeyOnly), and
    // the named overlay children (DonkeyPrefab.ModObjects). Both are undone in Detach and caught by the audit if they
    // weren't. The game members this touches are the prefab's part names and Unity types only; its freeze follows
    // Colors.Frozen, which guards the same borrowed parts (docs/skins-design.md §11).
    internal static partial class Looks
    {
        internal const string PackDirName = "gk2skins";
        internal const string Section = "Look";
        internal const string OverlayPrefix = "gk2_skin_";
        internal const string OverlayBody = OverlayPrefix + "bdy";
        internal const string OverlayHead = OverlayPrefix + "hed";
        internal const string FrontSuffix = "_front";   // the second overlay per part: drawn in front of the rider
        internal const string FrontKey = "front/";      // a front picture's key: "front/bdy_walk_left_03"
        internal const string TuningOwner = "dreas.gk2.donkeyapi.skin"; // the owner of the overrides a skin's tuning sets

        // The skin an instance wears, by id ("" or "donkey" = the game's own look). A look key (IsLook): an instance
        // with its own look reads only its own override or this default, never the player's cfg.
        internal static Tuned<string> Skin;
        internal static MountTuning Tuning;

        // ------------------------------------------------------------------ bases: what a skin is drawn on

        internal sealed class SkinBase
        {
            internal string Id;
            internal string[] Keys;             // the frame keys, in sheet order
            internal HashSet<string> KeySet;
            internal string[] Parts;            // the view's sprite renderers, by name; each gets an overlay
            internal int CanvasW, CanvasH;      // a picture's size
            internal float PivotX, PivotY;      // the view's anchor in the picture, from the bottom left
            internal float PixelsPerUnit;
            internal string GamePrefix;         // the game's preset prefix on its frame sprites ("0301_bdy_walk_left_03")
            internal string AtlasName;          // the atlas its frames live in (for the recipes' read-back)
            internal int BackCol, BackRow;      // the back line in the standing frame (column, row from the top)
            internal int GroundRow;             // the feet's row in the standing frame (from the bottom): where a game creature's feet go
        }

        // The donkey's frame keys (the character atlases' 0301_* sprites, 2026-10-17, docs/external-assets.md): the names
        // a skin's pictures use, and the list the loader checks names against.
        internal static readonly string[] KnownKeys =
        {
            "bdy_static_left",
            "bdy_walk_left_01", "bdy_walk_left_02", "bdy_walk_left_03", "bdy_walk_left_04", "bdy_walk_left_05", "bdy_walk_left_06", "bdy_walk_left_07", "bdy_walk_left_08",
            "bdy_ear_left_01", "bdy_ear_left_02", "bdy_ear_left_03", "bdy_ear_left_04", "bdy_ear_left_05",
            "bdy_graze_left_01", "bdy_graze_left_02", "bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05",
            "bdy_chew_left_01", "bdy_chew_left_02", "bdy_chew_left_03",
            "bdy_lips_left_01", "bdy_lips_left_02", "bdy_lips_left_03", "bdy_lips_left_04", "bdy_lips_left_05", "bdy_lips_left_06", "bdy_lips_left_07",
            "bdy_lips_left_08", "bdy_lips_left_09", "bdy_lips_left_10", "bdy_lips_left_11", "bdy_lips_left_12", "bdy_lips_left_13",
            "bdy_turn_left_01", "bdy_turn_left_02", "bdy_turn_left_03", "bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06",
            "hed_static_down", "hed_static_down_02", "hed_static_down_03",
        };

        internal static readonly SkinBase Donkey = new SkinBase
        {
            Id = MountLooks.DonkeyId, Keys = KnownKeys, KeySet = new HashSet<string>(KnownKeys, StringComparer.Ordinal), Parts = new[] { "bdy", "hed" },
            CanvasW = 96, CanvasH = 96, PivotX = 48f, PivotY = 48f, PixelsPerUnit = 50f, GamePrefix = "0301_", AtlasName = "Characters",
            BackCol = 58, BackRow = 41, // measured 2026-10-17
            GroundRow = 28, // the standing frame is drawn 50x47 at 23,28 on its canvas (the settings picture's log line)
        };

        private static readonly Dictionary<string, SkinBase> bases = new Dictionary<string, SkinBase>(StringComparer.OrdinalIgnoreCase) { [Donkey.Id] = Donkey };

        internal static SkinBase BaseOf(string id) => !string.IsNullOrEmpty(id) && bases.TryGetValue(id.Trim(), out SkinBase b) ? b : null;

        internal static void Bind(ConfigFile cfg)
        {
            Skin = cfg.Bind(Section, "Skin", Defaults.Get(Section, "Skin", ""),
                "The skin the donkey wears, by id (empty: the donkey as the game draws it). Set per donkey by the mods that own them (Saddle Up's Donkey settings); this is the value for donkeys without one of their own.");
            Tuned.MarkLook(Skin);
            Tuning = new MountTuning(Section, new ITuned[] { Skin });
            RegisterEmbedded(typeof(Looks).Assembly, "GK2DonkeyAPI.skins.", Plugin.Guid, builtIn: true);
            RegisterRecipes();
            try
            {
                int packs = RegisterFolder(BepInEx.Paths.PluginPath, Plugin.Guid + ".packs");
                if (packs > 0) Plugin.Log.LogInfo($"Looks: {packs} skin pack(s) found under plugins\\ (gk2skins\\<id>\\skin.cfg).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Looks: looking for skin packs failed: " + e.Message);
            }
            Camera.onPreCull += OnPreCull;
        }

        internal static void Unbind()
        {
            Camera.onPreCull -= OnPreCull;
        }

        // "0301_bdy_walk_left_03(Clone)" -> "bdy_walk_left_03": the game's preset id (any leading digits) and the skin
        // changer's "(Clone)" dropped. Called once per sprite instance seen (Loaded.Resolve caches), not per frame.
        internal static string FrameKey(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return "";
            string n = spriteName;
            int c = n.IndexOf("(Clone)", StringComparison.Ordinal);
            if (c >= 0) n = n.Substring(0, c);
            n = n.Trim();
            int i = 0;
            while (i < n.Length && char.IsDigit(n[i])) i++;
            if (i > 0 && i < n.Length && n[i] == '_') n = n.Substring(i + 1);
            return n;
        }

        // ------------------------------------------------------------------ the registry

        // A skin as registered: where its pictures come from (one of the four sources), and what the loader found.
        internal sealed class SkinDef
        {
            internal string Id, Name, Author, Version, Owner;
            internal SkinBase Base = Donkey;
            internal bool OwnColours = true;   // false: drawn in the base's palette colours (Colors recolours it)
            internal bool Over;               // true: drawn over the base, which stays visible
            internal string KindId = Kinds.DonkeyId;   // the mount kind this skin is a look of (docs/mounts-design.md)
            internal Kinds.KindDef DeclaredKind;       // a pack's [Mount] section: declared when the skin registers
            private Kinds.KindDef kind;
            private int kindVersion;
            private bool kindFallbackLogged;

            // The kind, resolved again whenever the kinds change (Kinds.Version); the donkey kind when the named one
            // isn't registered (said once).
            internal Kinds.KindDef Kind
            {
                get
                {
                    if (kind == null || kindVersion != Kinds.Version)
                    {
                        kindVersion = Kinds.Version;
                        kind = Kinds.Find(KindId);
                        if (kind == null)
                        {
                            kind = Kinds.Donkey;
                            if (!kindFallbackLogged) Plugin.Log.LogInfo($"Looks: skin '{Id}' names the mount '{KindId}', which nobody registered; it is a donkey for now.");
                            kindFallbackLogged = true;
                        }
                        else
                        {
                            kindFallbackLogged = false;
                        }
                    }
                    return kind;
                }
            }

            internal int CanvasW, CanvasH;    // a picture's size (the base's by default)
            internal float PivotX, PivotY;    // the base's anchor in the picture, from the bottom left
            internal bool BuiltIn;
            internal string Report = "";      // what the loader found (one block, logged once)
            internal string Source;           // a pack's folder, for the report
            internal string DirName;          // the skin directory's name (the id by default)
            internal Func<string, byte[]> ReadFile;    // the pack's files by name, for a reload
            internal List<string> FileNames;           // the pack's picture names, for a reload
            // The skin's own tuning (skin.cfg [Tuning], MountSkinDef.Tuning): existing tuning keys (SeatAdjustPx, RiderOffsetPx,
            // LegAdjustYPx, ...) set on the wearer as overrides owned by the skin while it is worn, never saved.
            internal Dictionary<string, string> Tuning;        // a pack's: the cfg text per key
            internal Dictionary<string, object> TuningValues;  // a code skin's: typed values
            // The pictures moved on the view, in art pixels (x towards the head, y up), riding and with the cart hitched
            // (a creature that should sit closer to the shafts, say).
            internal Vector2 OffsetRide, OffsetCart;

            // the sources, one of:
            internal Func<IEnumerable<string>> Keys;          // pictures: the frame keys the source has ...
            internal Func<string, byte[]> LoadPng;            // ... and a PNG per key (canvas-sized), or null
            internal Func<byte[]> LoadSheetPng;               // a sheet: one PNG, cells of the canvas size ...
            internal string[] SheetKeys;                      // ... in this order, left to right, top to bottom
            internal int SheetColumns;                        // 0: the sheet's width over the canvas width
            internal Func<byte[]> LoadFrontSheetPng;          // the front layer's sheet, the same cells (optional)
            internal IMountSkinSource Sprites;                // a mod's own sprites (not owned here)
            internal IMountSkinSource FrontSprites;           // ... and its front layer's (optional)
            internal Func<SkinFrame, Color32[]> Recipe;       // a rule over the base's own frames (LooksRecipes.cs)
            internal GameSource Game;                         // another game character's frames, mapped and scaled (LooksGame.cs)
            // skin.cfg "seat = picture": the rider sits on this skin's own picture (its top at the pivot column, per
            // frame, so he rides a hop), whatever the player's seat settings; else the base's back line / the seat height.
            internal bool SeatFromPicture;

            internal bool OwnsSheet => Sprites == null;
        }

        private static readonly Dictionary<string, SkinDef> defs = new Dictionary<string, SkinDef>(StringComparer.OrdinalIgnoreCase);
        internal static IEnumerable<SkinDef> All => defs.Values;

        internal static SkinDef Find(string id)
        {
            return !string.IsNullOrEmpty(id) && defs.TryGetValue(id.Trim(), out SkinDef d) ? d : null;
        }

        internal static void Register(SkinDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) throw new ArgumentException("A skin needs an id.");
            if (d.Id.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("'donkey' is the game's own look, not a skin id.");
            foreach (char c in d.Id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')) throw new ArgumentException($"Skin id '{d.Id}': letters, digits, dots, dashes and underscores only.");
            }
            if (d.Base == null) throw new ArgumentException($"Skin '{d.Id}' names no base.");
            // Everything that can refuse the skin comes before its mount is declared: a mount with no skin would be
            // offered with nothing to wear (seen 2026-10-18: Gerry listed, "donkey (not installed)" for its skin).
            if (d.Sprites == null && d.Recipe == null && d.Game == null && d.LoadSheetPng == null && (d.Keys == null || d.LoadPng == null)) throw new ArgumentException($"Skin '{d.Id}' has no source of pictures.");
            if (defs.TryGetValue(d.Id, out SkinDef old) && old.Owner != d.Owner)
            {
                throw new ArgumentException($"Skin id '{d.Id}' is already registered by {old.Owner}.");
            }
            if (d.DeclaredKind != null)
            {
                d.DeclaredKind.Owner = d.Owner;
                Kinds.KindDef declared = Kinds.Declare(d.DeclaredKind, "skin '" + d.Id + "'");
                if (string.IsNullOrEmpty(d.KindId) || d.KindId.Equals(Kinds.DonkeyId, StringComparison.OrdinalIgnoreCase)) d.KindId = declared.Id;
            }
            if (d.CanvasW <= 0 || d.CanvasH <= 0)
            {
                d.CanvasW = d.Base.CanvasW;
                d.CanvasH = d.Base.CanvasH;
                d.PivotX = d.Base.PivotX;
                d.PivotY = d.Base.PivotY;
            }
            defs[d.Id] = d;
            Plugin.Log.LogInfo($"Looks: skin '{d.Id}' ({d.Name}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}{(d.Report.Length > 0 ? ": " + d.Report : "")}.");
            if (old != null)
            {
                Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false); // replaced: its wearers take the new one
            }
            Mounts.Looks.RaiseChanged();
        }

        // Every skin embedded in an assembly under a prefix: resources named <prefix><skin dir>[\/]<file>, one skin.cfg
        // per skin directory (the csproj embeds assets\skins\**\* that way).
        internal static int RegisterEmbedded(Assembly asm, string prefix, string owner, bool builtIn = false)
        {
            var files = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase); // skin dir -> file -> resource
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string rest = res.Substring(prefix.Length);
                int sep = rest.LastIndexOfAny(new[] { '\\', '/' });
                if (sep <= 0) continue;
                string dir = rest.Substring(0, sep).Trim('\\', '/'), file = rest.Substring(sep + 1);
                if (!files.TryGetValue(dir, out var d)) files[dir] = d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                d[file] = res;
            }
            int n = 0;
            foreach (var kv in files)
            {
                if (!kv.Value.TryGetValue("skin.cfg", out string cfgRes)) continue;
                try
                {
                    var pics = kv.Value;
                    // the front layer's pictures: the skin dir's "front" subdirectory, keyed "front/<file>"
                    foreach (string sep in new[] { "\\", "/" })
                    {
                        if (files.TryGetValue(kv.Key + sep + "front", out var front))
                        {
                            foreach (var f in front) pics[FrontKey + f.Key] = f.Value;
                        }
                    }
                    byte[] Bytes(string file)
                    {
                        if (!pics.TryGetValue(file, out string res)) return null;
                        using (Stream s = asm.GetManifestResourceStream(res))
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }
                    string cfgText = Encoding.UTF8.GetString(Bytes("skin.cfg"));
                    SkinDef def = FromCfg(cfgText, kv.Key, owner, "embedded " + asm.GetName().Name + " " + kv.Key, Bytes, pics.Keys);
                    def.BuiltIn = builtIn;
                    Register(def);
                    n++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: the embedded skin '{kv.Key}' could not be registered: {e.Message}");
                }
            }
            return n;
        }

        // Every skin pack under a folder: <root>/gk2skins/<id>/skin.cfg and <root>/*/gk2skins/<id>/skin.cfg (a pack
        // inside a mod's own folder, or dropped straight into plugins). Pictures are read from disk when the skin is
        // first worn, so an installed pack costs nothing until then.
        internal static int RegisterFolder(string root, string owner)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return 0;
            var packDirs = new List<string>();
            string direct = Path.Combine(root, PackDirName);
            if (Directory.Exists(direct)) packDirs.Add(direct);
            foreach (string sub in Directory.GetDirectories(root))
            {
                string nested = Path.Combine(sub, PackDirName);
                if (Directory.Exists(nested)) packDirs.Add(nested);
            }
            int n = 0;
            foreach (string packDir in packDirs)
            {
                foreach (string skinDir in Directory.GetDirectories(packDir))
                {
                    if (!File.Exists(Path.Combine(skinDir, "skin.cfg"))) continue;
                    try
                    {
                        RegisterPack(skinDir, owner);
                        n++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Looks: the skin pack '{skinDir}' could not be registered: {e.Message}");
                    }
                }
            }
            return n;
        }

        // One pack folder: skin.cfg plus its pictures (a PNG per frame, or one sheet).
        internal static SkinDef RegisterPack(string skinDir, string owner)
        {
            string dir = skinDir;
            string cfgText = File.ReadAllText(Path.Combine(dir, "skin.cfg"), Encoding.UTF8);
            var names = new List<string>();
            foreach (string f in Directory.GetFiles(dir, "*.png")) names.Add(Path.GetFileName(f));
            string frontDir = Path.Combine(dir, "front");
            if (Directory.Exists(frontDir))
            {
                foreach (string f in Directory.GetFiles(frontDir, "*.png")) names.Add(FrontKey + Path.GetFileName(f));
            }
            byte[] Bytes(string file)
            {
                string f = Path.Combine(dir, file.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(f) ? File.ReadAllBytes(f) : null;
            }
            SkinDef def = FromCfg(cfgText, Path.GetFileName(skinDir), owner, "pack " + skinDir, Bytes, names);
            def.Source = skinDir;
            Register(def);
            return def;
        }

        // A skin withdrawn by its owner (a pack reloaded, a mod unloaded): the donkeys wearing it show the donkey from
        // their next tick (the choice is kept), and its pictures go when the last of them lets go.
        internal static bool Unregister(string id, string owner)
        {
            SkinDef d = Find(id);
            if (d == null || d.Owner != owner) return false;
            defs.Remove(d.Id);
            Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false);
            Plugin.Log.LogInfo($"Looks: skin '{d.Id}' withdrawn by {owner}.");
            Mounts.Looks.RaiseChanged();
            return true;
        }

        // What the loader found for a skin, for tools and the panel.
        internal static string Validate(string id)
        {
            SkinDef d = Find(id);
            return d == null ? $"no skin '{id}'" : $"{d.Id} ({d.Name}){(d.Source != null ? " from " + d.Source : "")}: {d.Report}";
        }

        private static IEnumerable<string> KeysOf(IEnumerable<string> fileNames)
        {
            foreach (string f in fileNames)
            {
                if (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) yield return f.Substring(0, f.Length - 4);
            }
        }

        // skin.cfg: "key = value" lines, "#" comments, a section line ignored. bytes(file) reads a file of the pack;
        // fileNames lists its files (the PNGs are the frames, unless the cfg names a sheet).
        internal static SkinDef FromCfg(string text, string dirName, string owner, string where, Func<string, byte[]> bytes, IEnumerable<string> fileNames)
        {
            var d = new SkinDef { Id = dirName, Name = dirName, Owner = owner, DirName = dirName, ReadFile = bytes, FileNames = new List<string>(fileNames) };
            fileNames = d.FileNames;
            var unknown = new List<string>();
            int format = 1;
            string baseId = Donkey.Id, sheet = null, frontSheet = null, frames = null;
            bool pivotGiven = false;
            string section = "skin";
            var mount = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var game = new Dictionary<string, string>(StringComparer.Ordinal); // [Game]: prefix, scale, and base key = game frame lines
            string source = null;
            Dictionary<string, object> kt = null, ktRide = null, ktCart = null;
            var unknownSections = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[')
                {
                    section = line.Trim('[', ']', ' ').ToLowerInvariant();
                    if (section != "skin" && section != "tuning" && section != "game" && section != "mount" && section != "mount.tuning" && section != "mount.tuning.ride" && section != "mount.tuning.cart") unknownSections.Add(section);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                int hash = value.IndexOf('#');
                if (hash >= 0) value = value.Substring(0, hash).Trim();
                switch (section)
                {
                    case "tuning": (d.Tuning ?? (d.Tuning = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "game": game[key] = value; continue;
                    case "mount": mount[key] = value; continue;
                    case "mount.tuning": (kt ?? (kt = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "mount.tuning.ride": (ktRide ?? (ktRide = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "mount.tuning.cart": (ktCart ?? (ktCart = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "skin": break;
                    default: continue;
                }
                switch (key.ToLowerInvariant())
                {
                    case "format": int.TryParse(value, out format); break;
                    case "id": d.Id = value; break;
                    case "name": d.Name = value; break;
                    case "author": d.Author = value; break;
                    case "version": d.Version = value; break;
                    case "base": baseId = value; break;
                    case "kind": d.OwnColours = !value.Equals("palette", StringComparison.OrdinalIgnoreCase); break;
                    case "layer": d.Over = value.Equals("over", StringComparison.OrdinalIgnoreCase); break;
                    case "mount": d.KindId = value; break;
                    case "canvas": Pair(value, ref d.CanvasW, ref d.CanvasH); break;
                    case "pivot": PairF(value, ref d.PivotX, ref d.PivotY); pivotGiven = true; break;
                    case "offset": PairF(value, ref d.OffsetRide.x, ref d.OffsetRide.y); if (!text.Contains("offset_cart")) d.OffsetCart = d.OffsetRide; break;
                    case "offset_cart": PairF(value, ref d.OffsetCart.x, ref d.OffsetCart.y); break;
                    case "sheet": sheet = value; break;
                    case "sheet_front": frontSheet = value; break;
                    case "columns": int.TryParse(value, out d.SheetColumns); break;
                    case "frames": frames = value; break;
                    case "source": source = value.ToLowerInvariant(); break;
                    case "seat": d.SeatFromPicture = value.Equals("picture", StringComparison.OrdinalIgnoreCase); break;
                    default: unknown.Add(key); break;
                }
            }
            if (format > 1) throw new FormatException($"{where}: skin.cfg format {format} is newer than this version knows (1)");
            d.Base = BaseOf(baseId) ?? throw new FormatException($"{where}: base '{baseId}' is not known ({string.Join(", ", bases.Keys)})");
            if (d.CanvasW <= 0 || d.CanvasH <= 0)
            {
                d.CanvasW = d.Base.CanvasW;
                d.CanvasH = d.Base.CanvasH;
            }
            if (d.CanvasW < 8 || d.CanvasH < 8 || d.CanvasW > 512 || d.CanvasH > 512) throw new FormatException($"{where}: canvas {d.CanvasW}x{d.CanvasH} is out of range (8..512)");
            if (!pivotGiven)
            {
                d.PivotX = d.CanvasW / 2f;
                d.PivotY = d.CanvasH / 2f;
            }
            if (unknown.Count > 0) d.Report += "unknown keys ignored: " + string.Join(", ", unknown) + "; ";
            if (unknownSections.Count > 0) d.Report += "unknown sections ignored: " + string.Join(", ", unknownSections) + "; ";
            if (d.Tuning != null) d.Report += d.Tuning.Count + " tuning key(s); ";
            if (mount.TryGetValue("id", out string mountId) && !string.IsNullOrEmpty(mountId))
            {
                var k = new Kinds.KindDef { Id = mountId.Trim(), Tuning = kt, TuningRide = ktRide, TuningCart = ktCart };
                if (mount.TryGetValue("name", out string mn)) k.Name = mn;
                if (mount.TryGetValue("author", out string ma)) k.Author = ma;
                if (mount.TryGetValue("version", out string mv)) k.Version = mv;
                if (mount.TryGetValue("cart", out string mc)) k.Cart = !mc.Equals("false", StringComparison.OrdinalIgnoreCase);
                if (mount.TryGetValue("foals", out string mf)) k.Foals = !mf.Equals("false", StringComparison.OrdinalIgnoreCase);
                if (mount.TryGetValue("foal_scale", out string ms) && float.TryParse(ms, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fs)) k.FoalScale = Mathf.Clamp(fs, 0.2f, 1f);
                if (mount.TryGetValue("effect", out string me) && !MountEffects.IsNone(me)) k.Effect = me.Trim();
                if (mount.TryGetValue("effect_when", out string mw) && Enum.TryParse(mw.Trim(), true, out MountEffectWhen when)) k.EffectWhen = when;
                d.DeclaredKind = k;
                d.Report += "declares the mount '" + k.Id + "'; ";
            }
            else if (kt != null || ktRide != null || ktCart != null)
            {
                d.Report += "a [Mount.Tuning] section without a [Mount] id is ignored; ";
            }
            if (source == "game")
            {
                // another game character's frames (LooksGame.cs): no pictures of its own; [Game] says whose and how
                var g = new GameSource();
                foreach (var kv in game)
                {
                    switch (kv.Key.ToLowerInvariant())
                    {
                        case "prefix": g.Prefix = kv.Value; break;
                        case "scale": float.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g.Scale); break;
                        case "lift": if (float.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lift)) g.Lift = Mathf.Clamp01(lift); break;
                        default: g.Map[kv.Key] = kv.Value; break;
                    }
                }
                if (string.IsNullOrEmpty(g.Prefix)) throw new FormatException($"{where}: source = game needs a [Game] section with prefix = <the character's preset prefix, like 0901_>");
                if (g.Scale < 1f || g.Scale > 8f || Mathf.Abs(g.Scale - Mathf.Round(g.Scale)) > 0.001f) throw new FormatException($"{where}: [Game] scale = {g.Scale} must be a whole number from 1 to 8");
                d.Game = g;
                d.Keys = () => d.Base.Keys;
                d.Report += $"the game's own '{g.Prefix}*' frames scaled {g.Scale:0}x, {g.Map.Count} key(s) mapped by hand; ";
            }
            else if (source != null && source != "pictures")
            {
                throw new FormatException($"{where}: source = {source} is not known (pictures, or game)");
            }
            else if (sheet != null)
            {
                string file = sheet, frontFile = frontSheet;
                d.LoadSheetPng = () => bytes(file);
                if (frontFile != null) d.LoadFrontSheetPng = () => bytes(frontFile);
                d.SheetKeys = frames != null ? frames.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries) : d.Base.Keys;
                Validate(d, d.SheetKeys, "a sheet for " + d.SheetKeys.Length + " frames" + (frontFile != null ? " and a front sheet" : ""));
            }
            else
            {
                var list = new List<string>(fileNames);
                d.Keys = () => KeysOf(list);
                d.LoadPng = key => bytes(key + ".png");
                Validate(d, KeysOf(list), null);
            }
            return d;
        }

        private static void Pair(string v, ref int a, ref int b)
        {
            string[] p = v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y)) { a = x; b = y; }
        }

        private static void PairF(string v, ref float a, ref float b)
        {
            string[] p = v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) { a = x; b = y; }
        }

        // What the names say before any picture is decoded: typos, a head drawn in walks, what is still to draw.
        private static void Validate(SkinDef d, IEnumerable<string> keys, string what)
        {
            SkinBase b = d.Base;
            var have = new HashSet<string>(StringComparer.Ordinal);
            int front = 0;
            var unknown = new List<string>();
            var walkHead = new List<string>();
            foreach (string key in keys)
            {
                string k = key;
                if (k.StartsWith(FrontKey, StringComparison.Ordinal))
                {
                    k = k.Substring(FrontKey.Length);
                    front++;
                }
                else
                {
                    have.Add(k);
                }
                if (b.KeySet.Contains(k)) continue;
                if (IsPartName(b, k) || IsClipName(b, k)) continue;
                if (k.StartsWith("hed_walk", StringComparison.Ordinal)) walkHead.Add(key); else unknown.Add(key);
            }
            int missing = 0;
            foreach (string k in b.Keys)
            {
                if (!have.Contains(k) && !have.Contains(ClipOf(k)) && !have.Contains(PartOf(k))) missing++;
            }
            var sb = new StringBuilder(d.Report);
            sb.Append(what ?? have.Count + " pictures");
            if (front > 0) sb.Append(" and ").Append(front).Append(" in front of the rider");
            if (missing > 0) sb.Append(", ").Append(missing).Append(" of the ").Append(b.Id).Append("'s ").Append(b.Keys.Length).Append(" frames not drawn (the ").Append(b.Id).Append(" shows there)");
            if (unknown.Count > 0) sb.Append("; names that match no frame: ").Append(string.Join(", ", unknown));
            if (walkHead.Count > 0) sb.Append("; a head in walk frames is never shown (the body frames carry the head): ").Append(string.Join(", ", walkHead));
            d.Report = sb.ToString();
        }

        private static bool IsPartName(SkinBase b, string k) => Array.IndexOf(b.Parts, k) >= 0;

        private static bool IsClipName(SkinBase b, string k)
        {
            foreach (string known in b.Keys)
            {
                if (ClipOf(known) == k) return true;
            }
            return false;
        }

        // "bdy_walk_left_03" -> "bdy_walk_left"; "bdy_static_left" -> itself (no frame number)
        private static string ClipOf(string key)
        {
            int us = key.LastIndexOf('_');
            if (us <= 0) return key;
            string tail = key.Substring(us + 1);
            foreach (char c in tail) if (!char.IsDigit(c)) return key;
            return key.Substring(0, us);
        }

        private static string PartOf(string key)
        {
            int us = key.IndexOf('_');
            return us <= 0 ? key : key.Substring(0, us);
        }

        // A PNG's pixels (bottom row first), or null with the reason.
        private static Color32[] DecodePng(byte[] png, out int w, out int h, out string why)
        {
            w = h = 0;
            why = null;
            if (png == null)
            {
                why = "no file";
                return null;
            }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(png, markNonReadable: false))
                {
                    why = "not a picture Unity can read";
                    return null;
                }
                w = t.width;
                h = t.height;
                return t.GetPixels32();
            }
            finally
            {
                Object.Destroy(t);
            }
        }

        // ------------------------------------------------------------------ the skin's file: for the tuning panel

        // The skin.cfg a skin's values are kept in: a pack's own file, or a built-in's in the repo's assets on the machine
        // that has the sources; null when there is none to write (a code skin, a built-in elsewhere).
        internal static string CfgPathFor(SkinDef d)
        {
            if (d == null) return null;
            if (!string.IsNullOrEmpty(d.Source)) return Path.Combine(d.Source, "skin.cfg");
            if (d.BuiltIn) return Defaults.RepoPath(Path.Combine("assets", "skins", d.DirName ?? d.Id, "skin.cfg"));
            return null;
        }

        // The file rewritten with the live values: the [Skin] section's own lines kept (foals, mount, offset and
        // offset_cart replaced), [Tuning] and, where the file declares the mount, [Mount] and its tuning sections
        // written anew from the kind. Returns null, or why not.
        internal static string SaveCfg(SkinDef d) => SaveCfg(d, out _);

        // changed: false when the file already held exactly this text (nothing written).
        internal static string SaveCfg(SkinDef d, out bool changed)
        {
            changed = false;
            string path = CfgPathFor(d);
            if (path == null) return "no file to write (a code skin, or the sources aren't on this machine)";
            try
            {
                var head = new List<string>();
                var skinLines = new List<string>();
                var otherSections = new List<(string Header, List<string> Lines)>(); // [Game] and anything else: kept as written
                bool declaresMount = false;
                string section = null;
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("["))
                        {
                            section = line.Trim('[', ']', ' ').ToLowerInvariant();
                            if (section == "mount") declaresMount = true;
                            if (section != "skin" && section != "tuning" && !section.StartsWith("mount", StringComparison.Ordinal)) otherSections.Add((raw.Trim(), new List<string>()));
                            continue;
                        }
                        if (section == null) head.Add(raw);
                        else if (section == "skin")
                        {
                            int eq = line.IndexOf('=');
                            string key = eq > 0 ? line.Substring(0, eq).Trim().ToLowerInvariant() : "";
                            if (key == "mount" || key == "offset" || key == "offset_cart") continue;
                            skinLines.Add(raw);
                        }
                        else if (section != "tuning" && !section.StartsWith("mount", StringComparison.Ordinal) && otherSections.Count > 0)
                        {
                            otherSections[otherSections.Count - 1].Lines.Add(raw);
                        }
                    }
                }
                var sb = new StringBuilder();
                foreach (string h in head) sb.Append(h).Append('\n');
                sb.Append("[Skin]\n");
                while (skinLines.Count > 0 && skinLines[skinLines.Count - 1].Trim().Length == 0) skinLines.RemoveAt(skinLines.Count - 1); // no blank lines piling up per save
                foreach (string l in skinLines) sb.Append(l).Append('\n');
                Kinds.KindDef k = d.Kind;
                sb.Append("mount = ").Append(d.KindId).Append('\n');
                if (d.OffsetRide != Vector2.zero || d.OffsetCart != Vector2.zero)
                {
                    sb.Append("offset = ").Append(Num(d.OffsetRide.x)).Append(' ').Append(Num(d.OffsetRide.y)).Append('\n');
                    if (d.OffsetCart != d.OffsetRide) sb.Append("offset_cart = ").Append(Num(d.OffsetCart.x)).Append(' ').Append(Num(d.OffsetCart.y)).Append('\n');
                }
                if (d.Tuning != null && d.Tuning.Count > 0)
                {
                    sb.Append("\n[Tuning]\n");
                    foreach (var kv in d.Tuning) sb.Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
                }
                foreach ((string Header, List<string> Lines) other in otherSections)
                {
                    sb.Append('\n').Append(other.Header).Append('\n');
                    foreach (string l in other.Lines) sb.Append(l).Append('\n');
                }
                if (declaresMount && k != Kinds.Donkey)
                {
                    sb.Append("\n[Mount]\nid = ").Append(k.Id).Append("\nname = ").Append(k.Label).Append('\n');
                    if (!string.IsNullOrEmpty(k.Author)) sb.Append("author = ").Append(k.Author).Append('\n');
                    if (!string.IsNullOrEmpty(k.Version)) sb.Append("version = ").Append(k.Version).Append('\n');
                    sb.Append("cart = ").Append(k.Cart ? "true" : "false").Append("\nfoals = ").Append(k.Foals ? "true" : "false").Append('\n');
                    if (Mathf.Abs(k.FoalScale - 0.6f) > 0.001f) sb.Append("foal_scale = ").Append(Num(k.FoalScale)).Append('\n');
                    if (!string.IsNullOrEmpty(k.Effect)) sb.Append("effect = ").Append(k.Effect).Append("\neffect_when = ").Append(k.EffectWhen).Append('\n');
                    WriteSection(sb, "[Mount.Tuning]", k.Tuning);
                    WriteSection(sb, "[Mount.Tuning.Ride]", k.TuningRide);
                    WriteSection(sb, "[Mount.Tuning.Cart]", k.TuningCart);
                }
                string text = sb.ToString();
                if (File.Exists(path) && File.ReadAllText(path) == text) return null; // the same already: no write, no undo step
                File.WriteAllText(path, text, new UTF8Encoding(false));
                changed = true;
                Plugin.Log.LogInfo($"Looks: skin '{d.Id}' written to {path}.");
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static void WriteSection(StringBuilder sb, string name, Dictionary<string, object> values)
        {
            if (values == null || values.Count == 0) return;
            sb.Append('\n').Append(name).Append('\n');
            foreach (var kv in values) sb.Append(kv.Key).Append(" = ").Append(kv.Value is float f ? Num(f) : kv.Value).Append('\n');
        }

        private static string Num(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        // The file read again and the skin registered anew (the same owner replaces); a kind it declares takes the
        // file's values in place. Returns null, or why not.
        internal static string ReloadCfg(SkinDef d)
        {
            string path = CfgPathFor(d);
            if (path == null || !File.Exists(path)) return "no file to read";
            if (d.ReadFile == null || d.FileNames == null) return "a code skin: nothing to reload";
            try
            {
                SkinDef fresh = FromCfg(File.ReadAllText(path, Encoding.UTF8), d.DirName ?? d.Id, d.Owner, "reload " + path, d.ReadFile, d.FileNames);
                fresh.BuiltIn = d.BuiltIn;
                fresh.Source = d.Source;
                if (fresh.DeclaredKind != null)
                {
                    Kinds.KindDef have = Kinds.Find(fresh.DeclaredKind.Id);
                    if (have != null && have.FromPack && have.Owner == d.Owner)
                    {
                        fresh.DeclaredKind.Owner = d.Owner;
                        Kinds.Redeclare(have, fresh.DeclaredKind);
                        fresh.DeclaredKind = null;
                        fresh.KindId = have.Id;
                    }
                }
                Register(fresh);
                Kinds.Touch();
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        // ------------------------------------------------------------------ kinds: the cart rule, the settings list

        // The kind an instance is about to be ridden as: its Skin key's (wanted, even while that skin still builds).
        internal static Kinds.KindDef KindOf(SkinDef d) => d != null ? d.Kind : Kinds.Donkey;

        internal static Kinds.KindDef KindWanted(Mount.Instance i)
        {
            string want;
            using (Mount.Bind(i))
            {
                want = Skin.Value ?? "";
            }
            SkinDef d = want.Length > 0 && !want.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? Find(want) : null;
            return KindOf(d);
        }

        // A kind that doesn't pull the cart: why not, else null (docs/mounts-design.md §3).
        internal static string WhyNotCartByKind(Mount.Instance i)
        {
            if (i == null) return null;
            Kinds.KindDef k = KindWanted(i);
            return k.Cart ? null : $"A {k.Label.ToLowerInvariant()} can't pull the cart.";
        }

        // The tuning keys a skin's [Tuning] may set: the ones that exist because of how the pictures are drawn (where
        // the rider sits on them, where the cart meets them). Everything else is the mount's (docs/mounts-design.md §1).
        internal static readonly HashSet<string> SkinTuningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RiderOffsetPx", "TorsoOffsetPx", "SeatAdjustPx", "SeatOffset", "LegAdjustXPx", "LegAdjustYPx", "LegHeightPx", "BackSeatExtraPx",
            "HitchGapPx", "Depth", "CenterOffset",
        };

        // The value a creature's layers give key on instance i for style, in the order Rig.ApplyTuning merges them
        // (another owner's override first, then the skin's [Tuning], the kind's per-style tuning, the kind's both-styles
        // tuning), or null when none sets it: for a reader that needs a style the instance isn't in yet (the hitch
        // wants the cart style's gap while the mount is still ridden plain).
        internal static object LayeredValue(Mount.Instance i, MountStyle style, string key)
        {
            if (i == null) return null;
            if (i.OverrideOwners.TryGetValue(key, out string by) && by != TuningOwner && i.Overrides.TryGetValue(key, out object o)) return o;
            SkinDef d = i.LooksRig?.DefNow;
            Kinds.KindDef k = d != null ? d.Kind : Kinds.Donkey;
            if (d != null && d.TuningValues != null && d.TuningValues.TryGetValue(key, out object typed)) return typed;
            if (d != null && d.Tuning != null && d.Tuning.TryGetValue(key, out string text)) return text;
            Dictionary<string, object> perStyle = style == MountStyle.Cart ? k.TuningCart : k.TuningRide;
            if (perStyle != null && perStyle.TryGetValue(key, out object styled)) return styled;
            if (k.Tuning != null && k.Tuning.TryGetValue(key, out object both)) return both;
            return null;
        }

        // The hitch gap this instance has in the cart style, in world units: HitchGapPx from its layers, else the cfg.
        internal static float HitchGapWorldFor(Mount.Instance i)
        {
            int px = Plugin.For(MountStyle.Cart).HitchGapPx.Entry.Value;
            object v = LayeredValue(i, MountStyle.Cart, "HitchGapPx");
            if (v != null)
            {
                try
                {
                    px = Convert.ToInt32(v is string s ? BepInEx.Configuration.TomlTypeConverter.ConvertToValue(s, typeof(int)) : v, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: {i.Describe()}: HitchGapPx = {v} isn't a whole number ({e.Message}); the cfg's {px} applies.");
                }
            }
            return Mathf.Clamp(px, -32, 64) * Mount.Instance.PxWorld;
        }

        // For a settings page's Mount dropdown: every mount, the donkey first, the rest by name.
        internal static IReadOnlyList<(string Id, string Label)> KindChoices()
        {
            var list = new List<(string, string)>();
            foreach (MountKindInfo k in Mounts.Kinds.Kinds) list.Add((k.Id, k.Name));
            return list;
        }

        // For a mount's Skin dropdown: its skins by name (the donkey's own art first for the donkey).
        internal static IReadOnlyList<(string Id, string Label)> SkinChoicesOf(string kindId)
        {
            Kinds.KindDef k = Kinds.Find(kindId) ?? Kinds.Donkey;
            var list = new List<(string Id, string Label)>();
            if (k == Kinds.Donkey) list.Add((MountLooks.DonkeyId, "The game's own"));
            var skins = Kinds.SkinsOf(k);
            skins.Sort((a, b) => string.Compare(a.Name ?? a.Id, b.Name ?? b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (SkinDef s in skins) list.Add((s.Id, string.IsNullOrEmpty(s.Name) ? s.Id : s.Name));
            return list;
        }

        // A mount's first skin id (the default of its Skin row): the donkey's own art, or the first by name.
        internal static string FirstSkinOf(string kindId)
        {
            IReadOnlyList<(string Id, string Label)> c = SkinChoicesOf(kindId);
            return c.Count > 0 ? c[0].Id : MountLooks.DonkeyId;
        }

        // For a settings page's Mount line: the mount a skin id belongs to and the mount's other skins.
        internal static string MountLineFor(string skinId)
        {
            SkinDef d = string.IsNullOrEmpty(skinId) || skinId.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? null : Find(skinId);
            Kinds.KindDef k = KindOf(d);
            var names = new List<string>();
            if (k == Kinds.Donkey) names.Add("the game's own");
            foreach (SkinDef s in Kinds.SkinsOf(k)) names.Add(string.IsNullOrEmpty(s.Name) ? s.Id : s.Name);
            string note = d == null && !string.IsNullOrEmpty(skinId) && !skinId.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? " (the skin '" + skinId + "' isn't installed: drawn as the donkey)" : "";
            return k.Label + (names.Count > 0 ? " - skins: " + string.Join(", ", names) : "") + note;
        }

        // The skins to pick from: the donkey first, then every skin by kind and name, labelled by the kind when the
        // skin's name doesn't say it (docs/mounts-design.md §6).
        internal static IReadOnlyList<(string Id, string Label)> SkinChoices()
        {
            var list = new List<(string Id, string Label, string Sort)> { (MountLooks.DonkeyId, Kinds.Donkey.Label + ": the game's own", "") };
            var perKind = new Dictionary<Kinds.KindDef, int>();
            foreach (SkinDef s in All) perKind[s.Kind] = perKind.TryGetValue(s.Kind, out int n) ? n + 1 : 1;
            foreach (SkinDef s in All)
            {
                Kinds.KindDef k = s.Kind;
                string name = string.IsNullOrEmpty(s.Name) ? s.Id : s.Name;
                bool says = name.IndexOf(k.Label, StringComparison.OrdinalIgnoreCase) >= 0;
                bool only = k != Kinds.Donkey && perKind[k] == 1;
                string label = says || only ? name : k.Label + ": " + name;
                list.Add((s.Id, label, (k == Kinds.Donkey ? "0" : "1" + k.Label) + "|" + name));
            }
            list.Sort((a, b) => string.Compare(a.Sort, b.Sort, StringComparison.OrdinalIgnoreCase));
            var result = new List<(string, string)>(list.Count);
            foreach (var e in list) result.Add((e.Id, e.Label));
            return result;
        }

        // ------------------------------------------------------------------ loaded skins (refcounted)

        // The pixels of one frame, from whatever source, on the way into the sheet.
        internal struct Pixels
        {
            internal Color32[] P;
            internal int W, H;
            internal string Why; // P null: why (null when there is simply nothing for the key)
        }

        internal sealed class Loaded
        {
            internal SkinDef Def;
            internal readonly Dictionary<string, Sprite> Frames = new Dictionary<string, Sprite>(StringComparer.Ordinal); // "front/..." too
            // the game's sprite instance -> our sprite (or null): one lookup per part per frame once seen; the front layer's apart
            internal readonly Dictionary<Sprite, Sprite> Resolved = new Dictionary<Sprite, Sprite>();
            internal readonly Dictionary<Sprite, Sprite> ResolvedFront = new Dictionary<Sprite, Sprite>();
            internal readonly HashSet<string> MissingLogged = new HashSet<string>(StringComparer.Ordinal);
            internal int Refs;
            internal bool Ready;                // false until every frame is in the sheet (the switch is whole)
            internal Queue<string> ToBuild;     // the keys still to produce
            internal Func<string, Pixels> Produce;
            internal int Bad, NotInSheet;
            // The picture's top at the pivot column per key (a row from the bottom; -1: nothing drawn there) and per
            // sheet sprite: where a rider sits on a skin that says "seat = picture" (Rig.TrySeatTop).
            private readonly Dictionary<string, int> seatTopByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<Sprite, int> SeatTopBySprite = new Dictionary<Sprite, int>();

            // the sheet: one texture, a cell per key with a one-pixel gutter
            internal Texture2D Sheet;
            private static Color32[] zero;
            private int cols, cellW, cellH, placed;
            private readonly List<(string Key, RectInt Cell)> cells = new List<(string, RectInt)>();

            internal void MakeSheet(int count)
            {
                cellW = Def.CanvasW;
                cellH = Def.CanvasH;
                cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
                int rows = Mathf.Max(1, (count + cols - 1) / cols);
                int w = cols * (cellW + 1) + 1, h = rows * (cellH + 1) + 1;
                int max = SystemInfo.maxTextureSize;
                if (w > max || h > max) throw new InvalidOperationException($"the sheet would be {w}x{h}, over this machine's {max}");
                Sheet = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "gk2skin " + Def.Id, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                // A new texture's memory is undefined: the gutter lines are cleared (the cells are overwritten by Put, and a
                // cell never placed is never drawn). One zero buffer for every sheet, not a canvas-sized array per load.
                int lines = Mathf.Max(w, h);
                if (zero == null || zero.Length < lines) zero = new Color32[lines];
                for (int c = 0; c <= cols; c++) Sheet.SetPixels32(c * (cellW + 1), 0, 1, h, zero);
                for (int r = 0; r <= rows; r++) Sheet.SetPixels32(0, r * (cellH + 1), w, 1, zero);
                Live.TextureMade(Sheet);
            }

            // A frame into its cell: the canvas size exactly, or it is skipped (reported).
            internal bool Put(string key, Pixels px)
            {
                if (px.P == null)
                {
                    if (px.Why != null)
                    {
                        Bad++;
                        Plugin.Log.LogWarning($"Looks: skin '{Def.Id}', picture {key}: skipped ({px.Why}).");
                    }
                    return false;
                }
                if (px.W != cellW || px.H != cellH || px.P.Length != px.W * px.H)
                {
                    Bad++;
                    Plugin.Log.LogWarning($"Looks: skin '{Def.Id}', picture {key}: skipped ({px.W}x{px.H}, the skin's canvas is {cellW}x{cellH}).");
                    return false;
                }
                int i = placed++;
                var cell = new RectInt(1 + (i % cols) * (cellW + 1), 1 + (i / cols) * (cellH + 1), cellW, cellH);
                Sheet.SetPixels32(cell.x, cell.y, cellW, cellH, px.P);
                cells.Add((key, cell));
                if (!key.StartsWith(FrontKey, StringComparison.Ordinal))
                {
                    // the top of what is drawn at the pivot column (five columns wide), for the rider's seat
                    int col = Mathf.Clamp(Mathf.RoundToInt(Def.PivotX), 0, cellW - 1), top = -1;
                    for (int y = cellH - 1; y >= 0 && top < 0; y--)
                    {
                        for (int x = Mathf.Max(0, col - 2); x <= Mathf.Min(cellW - 1, col + 2); x++)
                        {
                            if (px.P[y * cellW + x].a != 0) { top = y; break; }
                        }
                    }
                    seatTopByKey[key] = top;
                }
                return true;
            }

            // The sheet uploaded once, its sprites made: the skin is wearable.
            internal void Finish()
            {
                if (Sheet != null)
                {
                    Sheet.Apply(false, true);
                    var pivot = new Vector2(Def.PivotX / cellW, Def.PivotY / cellH);
                    foreach (var (key, cell) in cells)
                    {
                        Sprite sp = Sprite.Create(Sheet, new Rect(cell.x, cell.y, cell.width, cell.height), pivot, Def.Base.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                        sp.name = "gk2skin " + Def.Id + " " + key;
                        Frames[key] = sp;
                        if (seatTopByKey.TryGetValue(key, out int top)) SeatTopBySprite[sp] = top;
                    }
                    cells.Clear();
                }
                Ready = true;
                ToBuild = null;
                Produce = null;
            }

            internal string SizeNote => Sheet != null ? $"one sheet {Sheet.width}x{Sheet.height}, {Sheet.width * Sheet.height * 4 / 1024} KB" : "the mod's own sprites";

            internal Sprite Resolve(Sprite game, bool front = false)
            {
                Dictionary<Sprite, Sprite> cache = front ? ResolvedFront : Resolved;
                if (cache.TryGetValue(game, out Sprite s)) return s;
                string key = FrameKey(game.name);
                string p = front ? FrontKey : "";
                if (!Frames.TryGetValue(p + key, out s) && !Frames.TryGetValue(p + ClipOf(key), out s) && !Frames.TryGetValue(p + PartOf(key), out s))
                {
                    s = null;
                    if (!front && MissingLogged.Add(key)) Plugin.Log.LogInfo($"Looks: skin '{Def.Id}' has no picture for {key}; the {Def.Base.Id}'s own shows there.");
                }
                cache[game] = s;
                return s;
            }
        }

        private static readonly Dictionary<string, Loaded> loaded = new Dictionary<string, Loaded>(StringComparer.OrdinalIgnoreCase);
        internal static int LoadedCount => loaded.Count;

        // A hold on a skin's pictures: loaded on the first, shared after. A sheet is filled a few frames a tick
        // (AdvanceBuilds) and the skin is Ready when whole; a sprite source is ready at once.
        private static Loaded Acquire(SkinDef d, string gamePrefix)
        {
            if (loaded.TryGetValue(d.Id, out Loaded l) && l.Def == d)
            {
                l.Refs++;
                return l;
            }
            l = new Loaded { Def = d, Refs = 1 };
            loaded[d.Id] = l;
            try
            {
                if (d.Sprites != null)
                {
                    int n = 0;
                    foreach ((IMountSkinSource src, string p) in new[] { (d.Sprites, ""), (d.FrontSprites, FrontKey) })
                    {
                        if (src == null) continue;
                        foreach (string key in src.Keys ?? Array.Empty<string>())
                        {
                            try
                            {
                                if (src.TryGetFrame(key, out Sprite s) && s != null)
                                {
                                    l.Frames[p + key] = s;
                                    n++;
                                }
                            }
                            catch (Exception e)
                            {
                                l.Bad++;
                                Plugin.Log.LogWarning($"Looks: skin '{d.Id}', picture {p}{key}: {e.Message}");
                            }
                        }
                    }
                    l.Finish();
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}' loaded: {n} pictures{(l.Bad > 0 ? ", " + l.Bad + " skipped" : "")} ({l.SizeNote}).");
                }
                else if (d.Game != null)
                {
                    StartGameBuild(l);
                }
                else if (d.Recipe != null)
                {
                    StartBuild(l, gamePrefix);
                }
                else if (d.LoadSheetPng != null)
                {
                    StartSheetBuild(l);
                }
                else
                {
                    var keys = new List<string>(d.Keys());
                    l.MakeSheet(keys.Count);
                    l.ToBuild = new Queue<string>(keys);
                    l.Produce = key =>
                    {
                        byte[] png;
                        try
                        {
                            png = d.LoadPng(key);
                        }
                        catch (Exception e)
                        {
                            return new Pixels { Why = e.Message };
                        }
                        var px = new Pixels();
                        px.P = DecodePng(png, out px.W, out px.H, out px.Why);
                        return px;
                    };
                    building.Add(l);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}' could not be loaded: {e.Message}");
                l.Finish(); // ready with nothing: the base shows
            }
            return l;
        }

        // A sheet pack: the one PNG decoded on the first frame, sliced per key after (the cells in reading order); the
        // front layer's sheet the same, for the "front/" keys.
        private static void StartSheetBuild(Loaded l)
        {
            SkinDef d = l.Def;
            string[] keys = d.SheetKeys;
            var all = new List<string>(keys);
            if (d.LoadFrontSheetPng != null) foreach (string k in keys) all.Add(FrontKey + k);
            l.MakeSheet(all.Count);
            l.ToBuild = new Queue<string>(all);
            var sheets = new SheetSource[] { new SheetSource { Load = d.LoadSheetPng }, new SheetSource { Load = d.LoadFrontSheetPng } };
            l.Produce = key =>
            {
                bool front = key.StartsWith(FrontKey, StringComparison.Ordinal);
                SheetSource s = sheets[front ? 1 : 0];
                if (s.Pixels == null && s.Why == null)
                {
                    byte[] png;
                    try
                    {
                        png = s.Load();
                    }
                    catch (Exception e)
                    {
                        png = null;
                        s.Why = e.Message;
                    }
                    if (s.Why == null) s.Pixels = DecodePng(png, out s.W, out s.H, out s.Why);
                    if (s.Pixels != null)
                    {
                        s.Columns = d.SheetColumns > 0 ? d.SheetColumns : Mathf.Max(1, s.W / d.CanvasW);
                        if (s.W < d.CanvasW || s.H < d.CanvasH)
                        {
                            s.Why = $"the sheet is {s.W}x{s.H}, smaller than one {d.CanvasW}x{d.CanvasH} cell";
                            s.Pixels = null;
                        }
                    }
                }
                if (s.Pixels == null) return new Pixels { Why = s.Why };
                int i = Array.IndexOf(keys, front ? key.Substring(FrontKey.Length) : key);
                int cx = (i % s.Columns) * d.CanvasW, cyTop = (i / s.Columns) * d.CanvasH;
                if (cx + d.CanvasW > s.W || cyTop + d.CanvasH > s.H)
                {
                    l.NotInSheet++; // fewer cells than frames: the base shows there (said once in the loaded line)
                    return new Pixels();
                }
                var px = new Pixels { W = d.CanvasW, H = d.CanvasH, P = new Color32[d.CanvasW * d.CanvasH] };
                for (int y = 0; y < d.CanvasH; y++)
                {
                    int sy = s.H - cyTop - d.CanvasH + y; // the sheet's rows run from the top; Unity's from the bottom
                    Array.Copy(s.Pixels, sy * s.W + cx, px.P, y * d.CanvasW, d.CanvasW);
                }
                return px;
            };
            building.Add(l);
        }

        private sealed class SheetSource
        {
            internal Func<byte[]> Load;
            internal Color32[] Pixels;
            internal int W, H, Columns;
            internal string Why;
        }

        private static void Release(Loaded l)
        {
            if (l == null || --l.Refs > 0) return;
            if (loaded.TryGetValue(l.Def.Id, out Loaded cur) && cur == l) loaded.Remove(l.Def.Id);
            building.Remove(l);
            if (l.Def.OwnsSheet)
            {
                foreach (Sprite s in l.Frames.Values) if (s != null) Object.Destroy(s);
            }
            if (l.Sheet != null)
            {
                Live.TextureGone(l.Sheet);
                Object.Destroy(l.Sheet);
                l.Sheet = null;
            }
            l.Frames.Clear();
            l.Resolved.Clear();
            l.ResolvedFront.Clear();
            Plugin.Log.LogInfo($"Looks: skin '{l.Def.Id}' unloaded (no donkey wears it).");
        }

        // ------------------------------------------------------------------ building, a few frames a tick

        private const int BuildPerTick = 6;
        private static readonly List<Loaded> building = new List<Loaded>();
        private static int builtFrame = -1;

        // Called once per frame from the rigs' ticks: up to BuildPerTick frames of the skins being loaded or built.
        internal static void AdvanceBuilds()
        {
            if (building.Count == 0 || Time.frameCount == builtFrame) return;
            builtFrame = Time.frameCount;
            using (Perf.Measure(Perf.S.LooksBuild))
            {
                int budget = BuildPerTick;
                for (int k = building.Count - 1; k >= 0 && budget > 0; k--)
                {
                    Loaded l = building[k];
                    if (l.Refs <= 0 || l.ToBuild == null)
                    {
                        building.RemoveAt(k); // nobody waits for it any more
                        continue;
                    }
                    while (budget > 0 && l.ToBuild.Count > 0)
                    {
                        string key = l.ToBuild.Dequeue();
                        budget--;
                        try
                        {
                            l.Put(key, l.Produce(key));
                        }
                        catch (Exception e)
                        {
                            l.Bad++;
                            Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}', frame {key}: {e.Message}");
                        }
                    }
                    if (l.ToBuild.Count == 0)
                    {
                        building.RemoveAt(k);
                        l.Finish();
                        Plugin.Log.LogInfo($"Looks: skin '{l.Def.Id}' loaded: {l.Frames.Count} pictures{(l.Bad > 0 ? ", " + l.Bad + " skipped" : "")}{(l.NotInSheet > 0 ? ", " + l.NotInSheet + " frames past the sheet's last cell (the " + l.Def.Base.Id + " shows there)" : "")} ({l.SizeNote}).");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the per-frame apply

        private static readonly List<Rig> attached = new List<Rig>();
        private static int appliedFrame = -1;

        // Before the first enabled camera of the frame culls: after every LateUpdate (the game's skin changer included).
        // Disabled cameras (PreviewStage's, rendered by hand) are not counted: the stage applies its own rig in Pose.
        private static void OnPreCull(Camera cam)
        {
            if (cam == null || !cam.enabled || Time.frameCount == appliedFrame || attached.Count == 0) return;
            appliedFrame = Time.frameCount;
            using (Perf.Measure(Perf.S.Looks))
            {
                for (int k = attached.Count - 1; k >= 0; k--)
                {
                    attached[k].ApplySafe();
                }
            }
        }

        // ------------------------------------------------------------------ the rig: one per instance

        internal sealed partial class Rig
        {
            // One of the view's parts, the overlay drawn in its place, and the front overlay drawn above the rider.
            private sealed class Slot
            {
                internal SpriteRenderer Part, Overlay, Front;
                internal bool Hid;   // forceRenderingOff set by this rig (cleared only by it)
            }

            private readonly Mount.Instance owner;
            private readonly SkinBase viewBase = Donkey;   // what this view is: the skins it can wear name it
            private SkinDef tunedFor;                       // the skin whose tuning keys are set on the owner now
            private Kinds.KindDef tunedKind;
            private MountStyle tunedStyle;
            private readonly List<string> tunedKeys = new List<string>();
            // the kind's abilities on this instance (docs/mounts-design.md §4)
            private Kinds.KindDef abilityKind;
            private bool[] abilityOff;
            private bool wasRidden;
            private MountStyle riddenStyle;

            // The kind this instance is now: its worn skin's (the donkey's while it wears none).
            internal Kinds.KindDef KindNow => skin != null ? skin.Def.Kind : Kinds.Donkey;
            internal SkinDef DefNow => skin?.Def;
            private GameObject root;
            private readonly Slot[] slots;
            private int slotCount;
            private Loaded skin, pending;   // pending: a sheet still being filled; worn once it is ready
            private string wornId = "";
            private bool dirty = true, failed;
            private MaterialPropertyBlock block;
            private readonly List<Renderer> renderers = new List<Renderer>(2);

            internal Rig(Mount.Instance owner)
            {
                this.owner = owner;
                slots = new Slot[viewBase.Parts.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = new Slot();
            }

            internal string Note => failed ? "off (failed)" : root == null ? "not attached" : skin != null ? "wears " + skin.Def.Id : "the " + viewBase.Id;
            internal IReadOnlyList<Renderer> Renderers => renderers;

            // After PrepareParts (the parts are final; the view is still hidden, so the overlays are hidden with it), or
            // on the settings' preview copy (AttachRoot: the same).
            internal void Attach(GameObject viewRoot)
            {
                Detach();
                failed = false;
                if (viewRoot == null || !viewRoot || Colors.Frozen) return;
                foreach (SpriteRenderer sr in viewRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
                {
                    if (UnderCargo(sr.transform)) continue;
                    int i = Array.IndexOf(viewBase.Parts, sr.name);
                    if (i >= 0 && slots[i].Part == null) slots[i].Part = sr;
                }
                if (slots[0].Part == null)
                {
                    for (int i = 0; i < slots.Length; i++) slots[i].Part = null;
                    return;
                }
                root = viewRoot;
                slotCount = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Part == null) continue;
                    slots[i].Overlay = MakeOverlay(slots[i].Part, OverlayPrefix + viewBase.Parts[i]);
                    slots[i].Front = MakeOverlay(slots[i].Part, OverlayPrefix + viewBase.Parts[i] + FrontSuffix);
                    slotCount++;
                }
                attached.Add(this);
                dirty = true;
            }

            internal void AttachRoot(GameObject copyRoot) => Attach(copyRoot);

            private static bool UnderCargo(Transform t)
            {
                for (Transform p = t.parent; p != null; p = p.parent)
                {
                    if (p.name == Cargo.ContainerName) return true;
                }
                return false;
            }

            private SpriteRenderer MakeOverlay(SpriteRenderer part, string name)
            {
                var go = new GameObject(name) { layer = part.gameObject.layer };
                go.transform.SetParent(part.transform, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var r = go.AddComponent<SpriteRenderer>();
                r.enabled = false;
                r.sortingLayerID = part.sortingLayerID;
                r.sortingOrder = part.sortingOrder;
                renderers.Add(r);
                return r;
            }

            // Every tick (the plugin's LateUpdate, the instance bound): which skin this instance wears now.
            internal void Tick()
            {
                if (root == null || failed) return;
                AdvanceBuilds();
                ApplyTuning();   // the skin's, the kind's and the style's values, re-applied only when one of them changes
                using (Perf.Measure(Perf.S.Abilities))
                {
                    TickAbilities();
                }
                if (pending != null)
                {
                    if (pending.Ready)
                    {
                        Loaded was = skin;
                        skin = pending;
                        pending = null;
                        if (was != null) Looks.Release(was);
                    }
                    else if (dirty)
                    {
                        Looks.Release(pending); // the choice changed again before it was ready
                        pending = null;
                    }
                    else
                    {
                        return;
                    }
                }
                string want;
                using (Mount.Bind(owner))
                {
                    want = Skin.Value ?? "";
                }
                want = want.Trim();
                if (!dirty && string.Equals(want, wornId, StringComparison.OrdinalIgnoreCase)) return;
                missingIdLogged.Remove(want);
                dirty = false;
                wornId = want;
                Loaded next = null;
                if (want.Length > 0 && !want.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase))
                {
                    SkinDef d = Find(want);
                    if (d == null)
                    {
                        if (missingIdLogged.Add(want)) Plugin.Log.LogWarning($"Looks: no skin '{want}' is installed; the donkey is drawn as the game draws it (the choice is kept).");
                    }
                    else if (d.Base != viewBase)
                    {
                        if (missingIdLogged.Add(want)) Plugin.Log.LogWarning($"Looks: skin '{want}' is drawn on a {d.Base.Id}, not a {viewBase.Id}; not worn.");
                    }
                    else if (owner != null && owner.ViewScale != 1f && !d.Kind.Foals)
                    {
                        next = null; // the skin says no foals
                    }
                    else
                    {
                        Sprite bodySprite = slots[0].Part != null ? slots[0].Part.sprite : null;
                        string prefix = bodySprite != null ? GamePrefix(bodySprite.name) : viewBase.GamePrefix;
                        using (Perf.Measure(Perf.S.LooksBuild)) // the sheet made, a recipe's game frames found once a session
                        {
                            next = Acquire(d, prefix);
                        }
                    }
                }
                if (next != null && !next.Ready)
                {
                    pending = next; // worn once built; the previous look stays until then
                    return;
                }
                Loaded prev = skin;
                skin = next;
                if (prev != null) Looks.Release(prev);
            }

            private static readonly HashSet<string> missingIdLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // The kind's tuning (both styles, then this style's) and the skin's own on top, onto the wearer as overrides
            // owned by the skin system (a key another mod overrides is left to it, said once per skin and key); the
            // previous set cleared first. Re-done only when the skin, the kind or the style changes. Nothing of it is saved.
            private void ApplyTuning()
            {
                SkinDef d = skin?.Def;
                Kinds.KindDef k = d != null ? d.Kind : Kinds.Donkey;
                MountStyle style = owner != null ? owner.CurrentStyle : MountStyle.Ride;
                if (d == tunedFor && k == tunedKind && style == tunedStyle) return;
                if (owner != null && owner.Handle == null && !owner.IsRidden) return; // the settings picture's paper donkey: no rider to seat
                ClearTuning(refresh: false); // one refresh for the whole re-merge, below
                tunedFor = d;
                tunedKind = k;
                tunedStyle = style;
                if (owner == null) return;
                var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                void Take(Dictionary<string, object> from)
                {
                    if (from != null) foreach (var kv in from) merged[kv.Key] = kv.Value;
                }
                Take(k.Tuning);
                Take(style == MountStyle.Cart ? k.TuningCart : k.TuningRide);
                if (d != null)
                {
                    // A skin is the drawing: its [Tuning] may move the rider and the cart to fit its pictures, never
                    // change what the mount is (speed, cart, foals, effect belong to the mount; docs/mounts-design.md §1).
                    void TakeSkin(string key, object value)
                    {
                        if (SkinTuningKeys.Contains(key)) merged[key] = value;
                        else if (tuningLogged.Add(d.Id + "/" + key)) Plugin.Log.LogWarning($"Looks: skin '{d.Id}' wants to set {key}: a skin may only move the rider and the cart to fit its pictures ({string.Join(", ", SkinTuningKeys)}); the mount '{k.Id}' sets the rest. Not applied.");
                    }
                    if (d.Tuning != null) foreach (var kv in d.Tuning) TakeSkin(kv.Key, kv.Value);
                    if (d.TuningValues != null) foreach (var kv in d.TuningValues) TakeSkin(kv.Key, kv.Value);
                }
                var tuning = new InstanceTuning(owner);
                bool any = tunedKeysWereCleared; // nothing left to set: the clear alone is the change to refresh
                if (merged.Count == 0)
                {
                    if (any) tuning.ChangedByLooks();
                    return;
                }
                any = false;
                string id = d != null ? d.Id : k.Id;
                foreach (var kv in merged)
                {
                    try
                    {
                        ITuned t = tuning.Find(kv.Key);
                        if (t.IsLook)
                        {
                            if (tuningLogged.Add(id + "/" + t.Key)) Plugin.Log.LogWarning($"Looks: '{id}' wants to set {t.Key}, a look key (the skin, the effect, a colour): a creature can't choose those; not applied.");
                            continue;
                        }
                        if (owner.OverrideOwners.TryGetValue(t.Key, out string by) && by != TuningOwner)
                        {
                            if (tuningLogged.Add(id + "/" + t.Key)) Plugin.Log.LogInfo($"Looks: '{id}' wants {t.Key} = {kv.Value}, which {by} has set on {owner.Describe()}; left to it.");
                            continue;
                        }
                        object v = kv.Value is string s ? BepInEx.Configuration.TomlTypeConverter.ConvertToValue(s, t.ValueType) : kv.Value;
                        if (v != null && v.GetType() != t.ValueType && !t.ValueType.IsEnum && v is IConvertible)
                        {
                            v = Convert.ChangeType(v, t.ValueType, System.Globalization.CultureInfo.InvariantCulture); // 4f for an int key, 2 for a float one
                        }
                        v = t.Clamp(v);
                        owner.Overrides[t.Key] = v;
                        owner.OverrideOwners[t.Key] = TuningOwner;
                        tunedKeys.Add(t.Key);
                        any = true;
                    }
                    catch (Exception e)
                    {
                        if (tuningLogged.Add(id + "/" + kv.Key)) Plugin.Log.LogWarning($"Looks: '{id}', tuning {kv.Key} = {kv.Value}: not applied ({e.Message}).");
                    }
                }
                if (any) tuning.ChangedByLooks();
            }

            // The kind's abilities: Attached/Detached when the kind changes, Mounted/Dismounted on the ridden edge (and a
            // style switch), Tick while ridden (or standing, when the ability asks). Only for instances with a handle.
            // An ability that throws is off on this instance for the session, said once.
            private void TickAbilities()
            {
                MountHandle h = owner?.Handle;
                if (h == null) return;
                Kinds.KindDef k = KindNow;
                if (k != abilityKind)
                {
                    if (abilityKind != null)
                    {
                        if (wasRidden) CallAll(a => a.Dismounted(h), "Dismounted");
                        CallAll(a => a.Detached(h), "Detached");
                    }
                    abilityKind = k;
                    abilityOff = k.Abilities.Length > 0 ? new bool[k.Abilities.Length] : null;
                    wasRidden = false;
                    CallAll(a => a.Attached(h), "Attached");
                }
                if (k.Abilities.Length == 0) return;
                bool ridden = owner.IsRidden;
                MountStyle style = owner.CurrentStyle;
                if (ridden != wasRidden || (ridden && style != riddenStyle))
                {
                    if (wasRidden) CallAll(a => a.Dismounted(h), "Dismounted");
                    if (ridden) CallAll(a => a.Mounted(h, style), "Mounted");
                    wasRidden = ridden;
                    riddenStyle = style;
                }
                float dt = Time.deltaTime;
                for (int i = 0; i < k.Abilities.Length; i++)
                {
                    if (abilityOff[i]) continue;
                    MountAbility a = k.Abilities[i];
                    if (!ridden && !a.TicksWhileStanding) continue;
                    try
                    {
                        a.Tick(h, dt);
                    }
                    catch (Exception e)
                    {
                        abilityOff[i] = true;
                        Plugin.Log.LogWarning($"Kinds: ability '{a.Id}' of '{k.Id}' threw on {owner.Describe()}; off for this one: {e.Message}");
                    }
                }
            }

            private void CallAll(Action<MountAbility> call, string what)
            {
                Kinds.KindDef k = abilityKind;
                if (k == null || abilityOff == null) return;
                for (int i = 0; i < k.Abilities.Length; i++)
                {
                    if (abilityOff[i]) continue;
                    try
                    {
                        call(k.Abilities[i]);
                    }
                    catch (Exception e)
                    {
                        abilityOff[i] = true;
                        Plugin.Log.LogWarning($"Kinds: ability '{k.Abilities[i].Id}' of '{k.Id}' threw in {what} on {owner.Describe()}; off for this one: {e.Message}");
                    }
                }
            }

            // The instance goes, or its kind is about to be dropped with the skin: the abilities told.
            private void ReleaseAbilities()
            {
                if (abilityKind == null) return;
                MountHandle h = owner?.Handle;
                if (h != null)
                {
                    if (wasRidden) CallAll(a => a.Dismounted(h), "Dismounted");
                    CallAll(a => a.Detached(h), "Detached");
                }
                abilityKind = null;
                abilityOff = null;
                wasRidden = false;
            }

            // Every override the creature owns on this instance goes, tracked here or not (one moved in with a cart, or
            // read from an old side file, is the creature's to set again, never a fixed value).
            private bool tunedKeysWereCleared;

            private void ClearTuning(bool refresh = true)
            {
                tunedFor = null;
                tunedKind = null;
                tunedKeys.Clear();
                tunedKeysWereCleared = false;
                if (owner == null || owner.OverrideOwners.Count == 0) return;
                bool any = false;
                foreach (var kv in new List<KeyValuePair<string, string>>(owner.OverrideOwners))
                {
                    if (kv.Value == TuningOwner)
                    {
                        owner.Overrides.Remove(kv.Key);
                        owner.OverrideOwners.Remove(kv.Key);
                        any = true;
                    }
                }
                tunedKeysWereCleared = any;
                if (any && refresh) new InstanceTuning(owner).ChangedByLooks();
            }

            private static readonly HashSet<string> tuningLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            internal void MarkDirty() => dirty = true;
            internal void RetuneNow() => tunedKind = null; // the kind's or skin's values changed in place: ApplyTuning re-merges on the next tick
            internal string Wearing => skin?.Def.Id ?? "";
            internal bool Building => pending != null;

            // "0301_bdy_walk_left_03(Clone)" -> "0301_": the game's skin preset id, the prefix of every donkey frame.
            private string GamePrefix(string spriteName)
            {
                int i = 0;
                while (i < spriteName.Length && char.IsDigit(spriteName[i])) i++;
                return i > 0 && i < spriteName.Length && spriteName[i] == '_' ? spriteName.Substring(0, i + 1) : viewBase.GamePrefix;
            }
            internal void CheckNow() => dirty = true;

            internal void ApplySafe()
            {
                try
                {
                    Apply();
                }
                catch (Exception e)
                {
                    failed = true;
                    Plugin.Log.LogWarning($"Looks: {owner?.Describe() ?? "the preview"}: drawing the skin failed, off for this view: {e.Message}");
                    Detach();
                }
            }

            // The frame's pictures onto the overlays and the view's own kept from rendering, or everything back.
            internal void Apply()
            {
                if (root == null || !root) return;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Overlay != null) ApplyPart(slots[i]);
                }
            }

            // Where the rider sits on a skin that says "seat = picture": the top of the body picture at the pivot
            // column, this frame, in the world (the overlay's transform carries the trim and the skin's offset, and the
            // view's scale and stretch). False when the skin doesn't say so, isn't shown, or draws nothing there.
            internal bool TrySeatTop(out Vector3 world)
            {
                world = default;
                if (skin == null || !skin.Def.SeatFromPicture || slots == null || slots.Length == 0) return false;
                SpriteRenderer ov = slots[0].Overlay;
                if (ov == null || !ov || !ov.enabled || ov.sprite == null) return false;
                if (!skin.SeatTopBySprite.TryGetValue(ov.sprite, out int top) || top < 0) return false;
                world = ov.transform.TransformPoint(new Vector3(0f, (top + 1 - skin.Def.PivotY) / skin.Def.Base.PixelsPerUnit, 0f));
                return true;
            }

            private void ApplyPart(Slot s)
            {
                SpriteRenderer part = s.Part, ov = s.Overlay;
                if (part == null || !part || ov == null || !ov) return;
                Sprite game = part.sprite;
                Sprite mine = game != null && skin != null ? skin.Resolve(game) : null;
                bool show = mine != null && part.enabled && part.gameObject.activeInHierarchy;
                if (ov.sprite != mine) ov.sprite = mine;
                if (ov.enabled != show) ov.enabled = show;
                if (show)
                {
                    Follow(ov, part, game, part.sortingLayerID, part.sortingOrder);
                }
                // the front layer: the same picture rules, sorted above the keeper while he rides (as the leg overlay,
                // RiderLeg: his layer, one above his body; the legs are +1, this is +2), else just above the part
                SpriteRenderer fr = s.Front;
                if (fr != null && fr)
                {
                    Sprite front = game != null && skin != null && part.enabled && part.gameObject.activeInHierarchy ? skin.Resolve(game, front: true) : null;
                    bool showFront = front != null;
                    if (fr.sprite != front) fr.sprite = front;
                    if (fr.enabled != showFront) fr.enabled = showFront;
                    if (showFront)
                    {
                        SpriteRenderer keeper = owner?.KeeperBody;
                        if (keeper != null && keeper) Follow(fr, part, game, keeper.sortingLayerID, keeper.sortingOrder + 2);
                        else Follow(fr, part, game, part.sortingLayerID, part.sortingOrder + 1);
                    }
                }
                bool hide = show && !skin.Def.Over;
                if (hide)
                {
                    if (!part.forceRenderingOff) part.forceRenderingOff = true;
                    s.Hid = true;
                }
                else if (s.Hid)
                {
                    part.forceRenderingOff = false;
                    s.Hid = false;
                }
            }

            // An overlay takes the part's material, block, flip, colour and the frame's trim offset (plus the skin's own
            // offset), and the sorting it is given. Nothing is written that is already so.
            private void Follow(SpriteRenderer ov, SpriteRenderer part, Sprite game, int layer, int order)
            {
                if (ov.sharedMaterial != part.sharedMaterial) ov.sharedMaterial = part.sharedMaterial;
                if (part.HasPropertyBlock())
                {
                    if (block == null) block = new MaterialPropertyBlock();
                    part.GetPropertyBlock(block);
                    ov.SetPropertyBlock(block);
                }
                if (ov.sortingLayerID != layer) ov.sortingLayerID = layer;
                if (ov.sortingOrder != order) ov.sortingOrder = order;
                if (ov.flipX != part.flipX) ov.flipX = part.flipX;
                if (ov.flipY != part.flipY) ov.flipY = part.flipY;
                if (ov.color != part.color) ov.color = part.color;
                // the game's frame is trimmed in its atlas: its sub-pixel trim offset moves it within the canvas; the
                // skin's own offset (art px, towards the head and up) on top
                Vector2 off = game.textureRectOffset;
                float ppu = game.pixelsPerUnit;
                Vector2 own = owner != null && owner.HasCart ? skin.Def.OffsetCart : skin.Def.OffsetRide;
                var local = new Vector3((off.x - Mathf.Floor(off.x) - own.x) / ppu * (part.flipX ? -1f : 1f), (off.y - Mathf.Floor(off.y) + own.y) / ppu, 0f);
                if (ov.transform.localPosition != local) ov.transform.localPosition = local;
            }

            // Before the prefab audit: the view's own sprites render again, the overlays go (unparented first: Destroy
            // waits for the end of the frame, and the audit runs now).
            internal void Detach()
            {
                attached.Remove(this);
                foreach (Slot s in slots)
                {
                    try
                    {
                        if (s.Hid && s.Part != null && s.Part) s.Part.forceRenderingOff = false;
                    }
                    catch
                    {
                    }
                    s.Hid = false;
                    s.Part = null;
                    s.Overlay = null;
                    s.Front = null;
                }
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r) continue;
                    try
                    {
                        r.transform.SetParent(null, worldPositionStays: false);
                        Object.Destroy(r.gameObject);
                    }
                    catch
                    {
                    }
                }
                renderers.Clear();
                slotCount = 0;
                root = null;
            }

            // The instance goes: its hold on the skin's pictures.
            internal void Release()
            {
                Detach();
                ReleaseAbilities();
                ClearTuning();
                if (skin != null) Looks.Release(skin);
                if (pending != null) Looks.Release(pending);
                skin = null;
                pending = null;
                wornId = "";
                dirty = true;
            }

            // For the self-tests: a skin picture drawn on the body and the view's own body kept from rendering; the front
            // layer drawn.
            internal bool TestShowing() => slots[0].Overlay != null && slots[0].Overlay && slots[0].Overlay.enabled && slots[0].Overlay.sprite != null && slots[0].Hid;
            internal bool TestFrontShowing() => slots[0].Front != null && slots[0].Front && slots[0].Front.enabled && slots[0].Front.sprite != null;
        }
    }
}
