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
        internal const string PackDirName = "gk2mounts"; // <root>\gk2mounts\<mount>\ (MountPacks.cs; docs/skins.md "What a pack is")
        internal const int MaxCanvas = 256; // a sheet is 44 cells (88 with a front layer) of RGBA32: 256 is 12 MB, the old 512 was 49 (docs/mounts-foundation-2.md 2.5)
        internal const string Section = "Look";
        internal const string OverlayPrefix = "gk2_skin_";
        internal const string OverlayBody = OverlayPrefix + "bdy";
        internal const string OverlayHead = OverlayPrefix + "hed";
        internal const string FrontSuffix = "_front";   // the second overlay per part: drawn in front of the rider
        internal const string FrontKey = "front/";
        internal const string SoundsKey = "sounds/"; // a pack's sounds\ folder (a skin's own, or a mount folder's: the mount's), keyed "sounds/<file>" (SoundBank.ForFiles reads them)
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
            Camera.onPreCull += OnPreCull;
        }

        // The API ships no skin of its own since 2026-10-21 (the user: the backend, the donkey alone; every skin and mount,
        // Hellfire and Royal Parade included, is Saddle Up's and comes through RegisterEmbedded like any mod's). What
        // is left here: the recipes (none shipped) and the loose skin packs under plugins\. Called after the dev keys
        // bind.
        internal static void RegisterBuiltIns()
        {
            RegisterRecipes();
            try
            {
                int packs = RegisterFolder(BepInEx.Paths.PluginPath, Plugin.Guid + ".packs");
                if (packs > 0) Plugin.Log.LogInfo($"Looks: {packs} skin pack(s) found under plugins\\ (gk2mounts\\<mount>\\skins\\<skin>\\skin.cfg).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Looks: looking for skin packs failed: " + e.Message);
            }
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

        // A skin as registered: where its pictures come from (one of the four sources), and what the loader found. What
        // it is built on (its mount's default) and what it said itself: LooksInherit.cs.
        internal sealed partial class SkinDef
        {
            // Id: the skin's full name, "<mount>/<skin>" (dragon/default, donkey/hellfire): a skin is named within its mount,
            // so two mods' "fire" for two mounts never meet (the user, 2026-10-24). LocalId: the "<skin>" part, its folder's
            // name; "default" is the mount's own look (DefaultSkinName).
            internal string Id, LocalId, Name, Author, Version, Owner;
            internal string Description;      // for the Mounts panel: a line or two about the look
            internal string Unavailable;      // why the skin can't be worn now (a game-source skin whose frames the game no longer has): not offered, the donkey shows
            internal string Portrait;         // a pack's: a PNG of the pack (next to the frames) for the panel's list; null: the mount drawn
            internal Texture2D PortraitTexture; // a code skin's portrait, the mod's own texture
            internal SkinBase Base = Donkey;
            internal bool OwnColours = true;   // false: drawn in the base's palette colours (Colors recolours it with the base's own region map)
            // [Colors] (docs/mount-colours.md): the skin's own colours by mount region (Dark, Muzzle, Eye, Harness; every
            // colour not named is Coat), hex upper case. The colours themselves are collected from the pictures as the
            // sheet is built (Loaded.Put); this only says which of them aren't Coat.
            internal Dictionary<string, HashSet<string>> ColorRegions;
            internal bool Over;               // true: drawn over the base, which stays visible
            // A skin over the base may erase from it: every pixel of this colour in a picture makes the base transparent
            // there (the ears gone under horns, the tail gone under another). Such a frame is built whole at load (the
            // base's own frame with the holes, the picture's other pixels on top) and drawn in the base's place; a frame
            // without it stays a plain overlay. FF00FF by default for layer = over; "erase = none" switches it off.
            internal Color32? Erase;
            // A skin over the base may recolour the base's own regions under it: skin.cfg [Base.Coat], [Base.Eye] ...
            // with the player's knobs (Brightness, Saturation, Colorize ...), applied before the player's settings
            // (Colors.Rebuild). An ash coat under Hellfire's horns, without a pixel of the game's art in the pack.
            internal Dictionary<string, Colors.RegionTweak> BaseRegions;
            internal string MountId = Catalog.DonkeyId;   // the mount this skin is a look of (docs/mounts-design.md): a pack's is its mount folder's
            internal string MountDirName;                // a pack's: the mount folder it sits in (gk2mounts\<this>\skins\<skin>), for its file's path
            internal bool AddedToOthers;                 // a pack's from a mount folder without mount.cfg: a look added to a mount another mod owns (its name carries its mod's prefix)
            internal bool MountMissingAtRegister;        // a code skin registered before its mount: checked again when the mount comes (MountArrived)
            private Catalog.Entry mount;
            private int mountVersion;
            private bool mountFallbackLogged;

            // The mount, resolved again whenever the mounts change (Catalog.Version); the donkey mount when the named one
            // isn't registered (said once).
            internal Catalog.Entry Mount
            {
                get
                {
                    if (mount == null || mountVersion != Catalog.Version)
                    {
                        mountVersion = Catalog.Version;
                        mount = Catalog.Find(MountId);
                        if (mount == null)
                        {
                            mount = Catalog.Donkey;
                            if (!mountFallbackLogged) Plugin.Log.LogInfo($"Looks: skin '{Id}' names the mount '{MountId}', which nobody registered; it is a donkey for now.");
                            mountFallbackLogged = true;
                        }
                        else
                        {
                            mountFallbackLogged = false;
                        }
                    }
                    return mount;
                }
            }

            internal int CanvasW, CanvasH;    // a picture's size (the base's by default)
            internal float PivotX, PivotY;    // the base's anchor in the picture, from the bottom left
            internal bool BuiltIn;
            internal string Report = "";      // what the loader found (one block, logged once)
            internal string Source;           // a pack's folder, for the report
            internal Assembly SourceAssembly; // an embedded skin's DLL (a mod's): its GK2MountsSourceDir says where its files are on this machine
            internal long DiskStamp;          // a pack's files' newest write time as read (PackStamp): what the watch compares against
            internal string DirName;          // the skin directory's name (the id by default)
            internal Func<string, byte[]> ReadFile;    // the pack's files by name, for a reload
            internal List<string> FileNames;           // the pack's picture names, for a reload
            // The skin's own tuning (skin.cfg [Tuning], MountSkinDef.Tuning): existing tuning keys (SeatAdjustPx, RiderOffsetPx,
            // LegAdjustYPx, ...) set on the wearer as overrides owned by the skin while it is worn, never saved.
            internal Dictionary<string, string> Tuning;        // a pack's: the cfg text per key
            internal Dictionary<string, object> TuningValues;  // a code skin's: typed values
            // The skin's per-style values ([Tuning.Ride] / [Tuning.Cart]: a pack's text, a code skin's typed values), on
            // top of [Tuning] for that style. The skin owns its drawing's geometry (docs/mounts-parity-matrix.md 9.1).
            internal Dictionary<string, object> TuningRide, TuningCart;
            // The skin's sounds (skin.cfg [Sounds], [Sounds.Ride], [Sounds.Cart]: the raw lines, written back by SaveCfg)
            // and their parsed form (LooksSounds.cs; docs/mount-sounds-design.md).
            internal Dictionary<string, string> Sounds, SoundsRide, SoundsCart, SoundsBaby;
            internal SoundBlocks SoundBlocks;
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
            internal bool SeatFromPicture;        // seat = picture: the rider sits on this frame's picture top (Gerry)
            internal int SeatRow = -1;             // seat = <row>: the canvas row (from the top) the rider's hip sits on, every frame, whatever the player's AutoSeat (docs/seat-investigation.md option A)

            internal bool OwnsSheet => Sprites == null;

            // its own sound files (sounds\ next to its skin.cfg) for the bank
            internal SoundBank.FileSource SoundFiles => new SoundBank.FileSource(Owner, Id, Source, ReadFile, "skin '" + Id + "'");
        }

        private static readonly Dictionary<string, SkinDef> defs = new Dictionary<string, SkinDef>(StringComparer.OrdinalIgnoreCase);
        internal static IEnumerable<SkinDef> All => defs.Values;

        // A skin by its full name ("saddleup.dragon/default"). A short name finds one too: a mount's id, or the name
        // after its prefix when only one mount has it, is that mount's default look ("dragon", "dragon/default": what a
        // dev's own world saved before the names, 2026-10-24); a skin's own name ("hellfire") the skin when only one
        // mount has a skin of that name.
        internal static SkinDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string t = id.Trim();
            if (defs.TryGetValue(t, out SkinDef d)) return d;
            if (oldDonkeySkins.TryGetValue(t, out string renamed) && defs.TryGetValue(renamed, out d)) return d; // "donkey/hellfire"
            int slash = t.IndexOf('/');
            if (slash >= 0) return (Catalog.Find(t.Substring(0, slash)) ?? Catalog.FindShort(t.Substring(0, slash))) is Catalog.Entry m && defs.TryGetValue(SkinKey(m.Id, t.Substring(slash + 1)), out d) ? d : null; // "dragon/default": the dragon's; "saddleup.dragon/default": a renamed mount's (Catalog.renamedMounts)
            if (t.Length == 0) return null;
            if (oldLookNames.TryGetValue(t, out string mountShort)) t = mountShort; // "red-dragon": the dragon's default
            if (defs.TryGetValue(SkinKey(t, DefaultSkinName), out d)) return d;
            if (Catalog.FindShort(t) is Catalog.Entry byName && defs.TryGetValue(SkinKey(byName.Id, DefaultSkinName), out d)) return d;
            SkinDef only = null;
            foreach (SkinDef s in defs.Values)
            {
                if (!string.Equals(s.LocalId, t, StringComparison.OrdinalIgnoreCase)) continue;
                if (only != null) return null; // two mounts have a skin of that name: say which (dragon/fire)
                only = s;
            }
            return only;
        }

        // The names the built-in looks had for a few hours on 2026-10-24 (the mount folders, before skins\default\): a
        // dev's world and cfg saved then still find the look (the user: "also fix in the old names thing"). Each is its
        // mount's default; never released.
        private static readonly Dictionary<string, string> oldLookNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["red-dragon"] = "dragon", ["grey-rat"] = "rat", ["garden-snail"] = "snail", ["earth-worm"] = "worm",
        };

        // Whether a skin is a look added to a mount it doesn't own (Register: its name needs its mod's prefix).
        internal static bool AddsToOthers(SkinDef d)
        {
            if (d.MountId.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return true;
            if (d.ReadFile != null) return d.AddedToOthers; // a pack's: its folder says
            Catalog.Entry k = Catalog.Find(d.MountId);
            return k != null && k.Owner != d.Owner;
        }

        // Saddle Up's donkey skins before they carried its prefix (2026-10-24, a few hours; never released).
        private static readonly Dictionary<string, string> oldDonkeySkins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["donkey/hellfire"] = "donkey/saddleup.hellfire", ["hellfire"] = "donkey/saddleup.hellfire",
            ["donkey/royal-parade"] = "donkey/saddleup.royal-parade", ["royal-parade"] = "donkey/saddleup.royal-parade",
        };

        // A mount's own look: the folder skins\default\ of its pack, the skin a new one of it wears (one per mount; the
        // user, 2026-10-24: every other skin is built on top of it).
        internal const string DefaultSkinName = "default";

        // A skin's full name: its mount's id and its own name.
        internal static string SkinKey(string mountId, string localId) => (string.IsNullOrWhiteSpace(mountId) ? Catalog.DonkeyId : mountId.Trim()) + "/" + localId;

        // The "<skin>" part of a full name ("dragon/default" -> "default"); a short name as it is.
        internal static string LocalOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            int s = id.IndexOf('/');
            return s >= 0 ? id.Substring(s + 1) : id;
        }

        internal static void Register(SkinDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) throw new ArgumentException("A skin needs an id.");
            if (string.IsNullOrWhiteSpace(d.MountId)) d.MountId = Catalog.DonkeyId;
            d.MountId = d.MountId.Trim();
            if (d.LocalId == null)
            {
                // a code skin's Id: its own name ("fire"), or the full name of its mount ("dragon/fire")
                string given = d.Id.Trim();
                int slash = given.IndexOf('/');
                if (slash >= 0)
                {
                    if (!given.Substring(0, slash).Equals(d.MountId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Skin id '{given}' names the mount '{given.Substring(0, slash)}', but the skin is a look of '{d.MountId}' (its Mount).");
                    given = given.Substring(slash + 1);
                }
                d.LocalId = given;
            }
            if (d.LocalId.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) && d.MountId.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("'donkey' is the game's own look, not a skin id.");
            if (d.LocalId.Length == 0) throw new ArgumentException("A skin needs an id.");
            string idWhy = Catalog.IdRule(d.LocalId, "Skin id");
            if (idWhy != null) throw new ArgumentException(idWhy);
            // the donkey's own look is the game's art: no skin of it is its default
            if (d.MountId.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase) && d.LocalId.Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"'{DefaultSkinName}' is the donkey's own look (the game's art): a donkey skin needs a name of its own.");
            d.Id = SkinKey(d.MountId, d.LocalId);
            // a look added to a mount another mod owns is named "<mod>.<name>" (donkey/saddleup.hellfire): two mods'
            // "frost" for one mount are two skins, and a saved one never turns into the other mod's (the user,
            // 2026-10-24; docs/mount-id-collisions.md). On your own mount any name. A pack's: a folder without mount.cfg;
            // a code skin's: a mount registered by another owner; every donkey skin (the donkey is the game's).
            // a mod's own look of its own mount, which lost its id to another mod's (the version rule): left out with it,
            // never put on the winner's mount (where it read as a missing prefix)
            if (d.ReadFile == null && Catalog.Lost(d.MountId, d.Owner))
            {
                throw new LeftOut($"Skin '{d.Id}': its mount '{d.MountId}' from {d.Owner} is left out (another mod's of that id is newer), so it is left out with it.");
            }
            // a code skin of a mount not registered yet: whether it is its own mount's or another mod's is known when the
            // mount comes (MountArrived); the name rule is checked again then
            d.MountMissingAtRegister = d.ReadFile == null && !d.MountId.Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase) && Catalog.Find(d.MountId) == null;
            if (AddsToOthers(d))
            {
                int dot = d.LocalId.IndexOf('.');
                if (dot <= 0 || dot == d.LocalId.Length - 1) throw new ArgumentException($"Skin '{d.Id}': a skin added to a mount another mod owns is named '<mod>.<name>', your mod's prefix and the skin's name ({SkinKey(d.MountId, "saddleup." + d.LocalId)}; docs/skins.md).");
            }
            if (d.Base == null) throw new ArgumentException($"Skin '{d.Id}' names no base.");
            if (d.CanvasW > MaxCanvas || d.CanvasH > MaxCanvas || d.CanvasW > 0 && d.CanvasW < 8 || d.CanvasH > 0 && d.CanvasH < 8)
                throw new ArgumentException($"Skin '{d.Id}': canvas {d.CanvasW}x{d.CanvasH} is out of range (8..{MaxCanvas}; a sheet is 44 cells of it).");
            // Everything that can refuse the skin comes before its mount is declared: a mount with no skin would be
            // offered with nothing to wear (seen 2026-10-18: Gerry listed, "donkey (not installed)" for its skin).
            if (d.Sprites == null && d.Recipe == null && d.Game == null && d.LoadSheetPng == null && (d.Keys == null || d.LoadPng == null)) throw new ArgumentException($"Skin '{d.Id}' has no source of pictures.");
            if (defs.TryGetValue(d.Id, out SkinDef old) && old.Owner != d.Owner)
            {
                // The same id from two places (docs/mounts-foundation-2.md 6.1): the higher version wins, a tie goes to
                // the DLL (a folder is a copy someone dropped in; a release carries its version), and the log names the
                // one that lost and where it is. The loser's wearers take the winner at once.
                if (!Outranks(d, old))
                {
                    throw new LeftOut($"Skin id '{d.Id}' is already registered by {old.Owner} ({Where(old)}, version {old.Version ?? "none"}); this one ({Where(d)}, version {d.Version ?? "none"}) is not newer, so it is left out. Delete the one you don't want, or raise the version of the one you do.");
                }
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}' from {Where(d)} (version {d.Version ?? "none"}) replaces the one from {Where(old)} (version {old.Version ?? "none"}, registered by {old.Owner}): the newer wins. Delete the old one.");
            }
            if (old != null)
            {
                ForgetPortraits(old.Id);
                SoundBank.ForgetSkin(old);
            }
            if (d.CanvasW <= 0 || d.CanvasH <= 0)
            {
                d.CanvasW = d.Base.CanvasW;
                d.CanvasH = d.Base.CanvasH;
                d.PivotX = d.Base.PivotX;
                d.PivotY = d.Base.PivotY;
            }
            if (d.Sprites != null && d.Over && IsVariantSlot(d))
            {
                // over its default would need the default's pictures under a mod's own sprites (not layered yet)
                d.Over = false;
                d.Erase = null;
                d.BaseRegions = null;
                d.Report += "Over on a code skin of a mount other than the donkey is not drawn over its default yet: drawn in its place; ";
            }
            Resolve(d); // built on its mount's default (LooksInherit.cs): what it doesn't say, the default's
            defs[d.Id] = d;
            Catalog.Late(d.Owner, "the skin '" + d.Id + "'");
            Plugin.Log.LogInfo($"Looks: skin '{d.Id}' ({d.Name}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}{(d.Parent != null ? ", built on '" + d.Parent.Id + "'" + (d.OverDefault ? " (drawn over it)" : "") : "")}{(d.Report.Length > 0 ? ": " + d.Report : "")}.");
            if (IsDefaultSlot(d))
            {
                // a mount's own look came (or a newer one replaced it): its other skins are built on it now
                RefreshInherited(d.MountId);
                Catalog.Touch(Catalog.Find(d.MountId));
            }
            MarkWearersDirty(); // replaced: its wearers take the new one; new: an instance whose Skin already names it wears it now (its tick gave up on the missing id)
            Mounts.Looks.RaiseChanged();
        }

        // A mount came: the code skins of it registered before it (a skin mod that BepInEx loaded first) meet the rules they
        // couldn't before (docs/multi-mod-loading.md 4.3). On another mod's mount a skin is named "<mod>.<name>", and its
        // own look ("default") is never another mod's: one that breaks either is withdrawn with a warning.
        internal static void MountArrived(Catalog.Entry k)
        {
            if (k == null) return;
            foreach (SkinDef d in new List<SkinDef>(defs.Values))
            {
                if (!d.MountMissingAtRegister || !d.MountId.Equals(k.Id, StringComparison.OrdinalIgnoreCase)) continue;
                d.MountMissingAtRegister = false;
                if (d.Owner == k.Owner) continue;
                int dot = d.LocalId.IndexOf('.');
                string why = d.LocalId.Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase) ? $"'{DefaultSkinName}' is the own look of the mount, which {k.Owner} registered"
                    : dot <= 0 || dot == d.LocalId.Length - 1 ? $"a skin added to a mount another mod owns is named '<mod>.<name>' ({SkinKey(d.MountId, "yourmod." + d.LocalId)})" : null;
                if (why == null) continue;
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}' of {d.Owner}, registered before its mount: {why}; withdrawn.");
                Unregister(d.Id, d.Owner);
            }
        }

        // A skin that is left out by the rules between mods (another's of the same id is newer; its mount lost), not by a
        // mistake in it: the code API says it in a warning instead of throwing into the mod's Awake.
        internal sealed class LeftOut : ArgumentException
        {
            internal LeftOut(string message) : base(message) { }
        }

        // Our own mods (the API, Saddle Up, the self-tests) ship the plain ids; everyone else's carry an author prefix.
        internal static bool FirstParty(string owner) => owner != null && owner.StartsWith("dreas.gk2.", StringComparison.Ordinal) && !owner.StartsWith(Plugin.Guid + ".packs", StringComparison.Ordinal);

        internal static string Where(SkinDef d) => d.Source != null ? "the folder " + d.Source : d.SourceAssembly != null ? d.SourceAssembly.GetName().Name + ".dll" : "code (" + d.Owner + ")";

        // Whether a skin of the same id from another owner replaces the registered one: a higher version; at a tie the
        // DLL over a folder; two folders or two DLLs at a tie: the first stays.
        internal static bool Outranks(SkinDef d, SkinDef old)
        {
            int cmp = CompareVersions(d.Version, old.Version);
            if (cmp != 0) return cmp > 0;
            return d.Source == null && old.Source != null; // a DLL (or code) over a loose folder
        }

        // Two versions as numbers (1.10 > 1.9; 1.0 = 1; none = 0). One that isn't a number ("1.0-beta") ranks below any
        // that is (an ordinal compare had "9" beat "10" against it); two that aren't compare as text.
        internal static int CompareVersions(string a, string b)
        {
            bool pa = System.Version.TryParse(Pad(a), out System.Version va), pb = System.Version.TryParse(Pad(b), out System.Version vb);
            if (pa && pb) return va.CompareTo(vb);
            if (pa != pb) return pa ? 1 : -1;
            return string.Compare(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
            string Pad(string v)
            {
                string t = string.IsNullOrWhiteSpace(v) ? "0" : v.Trim();
                for (int parts = t.Split('.').Length; parts < 4; parts++) t += ".0"; // 1.0 and 1.0.0 are the same version
                return t;
            }
        }

        // Whether an embedded mount folder has its mount.cfg (a folder holding only skins, like the donkey's, has no files of its own).
        private static bool MountCfgIn(Dictionary<string, Dictionary<string, string>> files, string dir) => files.TryGetValue(dir, out var top) && top.ContainsKey(MountCfgName);

        // Every mount and skin embedded in an assembly under a prefix: resources named <prefix><mount>\mount.cfg,
        // <prefix><mount>\sounds\<file>, <prefix><mount>\skins\<skin>\<file> (+ front\, sounds\): the csproj embeds
        // assets\mounts\**\* that way. The mounts first, then their skins. The number of skins registered.
        internal static int RegisterEmbedded(Assembly asm, string prefix, string owner, bool builtIn = false)
        {
            using (Catalog.Batch()) return RegisterEmbeddedNow(asm, prefix, owner, builtIn);
        }

        private static int RegisterEmbeddedNow(Assembly asm, string prefix, string owner, bool builtIn)
        {
            var files = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase); // dir ("saddleup.dragon", "saddleup.dragon/skins/default/front") -> file -> resource
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string rest = res.Substring(prefix.Length).Replace('\\', '/');
                int sep = rest.LastIndexOf('/');
                if (sep <= 0) continue;
                string dir = rest.Substring(0, sep).Trim('/'), file = rest.Substring(sep + 1);
                if (!files.TryGetValue(dir, out var d)) files[dir] = d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                d[file] = res;
            }
            Func<string, byte[]> Reader(Dictionary<string, string> map) => file =>
            {
                if (!map.TryGetValue(file, out string res)) return null;
                using (Stream st = asm.GetManifestResourceStream(res))
                using (var ms = new MemoryStream())
                {
                    st.CopyTo(ms);
                    return ms.ToArray();
                }
            };
            var dirs = new List<string>(files.Keys);
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            // the mounts: a top-level dir with a mount.cfg (and its own look, skins/default)
            var refusedMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in dirs)
            {
                if (dir.IndexOf('/') >= 0 || !files[dir].ContainsKey(MountCfgName)) continue;
                if (!files.TryGetValue(dir + "/" + SkinsDirName + "/" + DefaultSkinName, out var defFiles) || !defFiles.ContainsKey("skin.cfg"))
                {
                    Plugin.Log.LogWarning($"Looks: the embedded mount '{dir}' has no {SkinsDirName}\\{DefaultSkinName}\\skin.cfg: a mount's own look is its skins\\default\\ (docs/skins.md); left out with its skins.");
                    refusedMounts.Add(dir);
                    continue;
                }
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files[dir]) map[f.Key] = f.Value;
                if (files.TryGetValue(dir + "/sounds", out var ms)) foreach (var f in ms) if (SoundBlock.IsSoundFile(f.Key)) map[SoundsKey + f.Key] = f.Value;
                var p = new MountPack { Owner = owner, DirName = dir, SourceAssembly = asm, BuiltIn = builtIn, ReadFile = Reader(map), FileNames = new List<string>(map.Keys) };
                // refused (another's of the same id is newer, the id has no prefix): its skins are left out with it, as a
                // loose folder's are (MountPacks.RegisterMountFolder; the two differed before 2026-10-24)
                if (RegisterMount(p, Numbers.CfgText(p.ReadFile(MountCfgName)), "embedded " + asm.GetName().Name + " " + dir) == null) refusedMounts.Add(dir);
            }
            // the skins: <mount>/skins/<skin> with a skin.cfg; each mount's own look first, so its other skins are built on
            // it from their registration ("black" sorts before "default"; docs/skin-layering-plan.md 2.1)
            int n = 0;
            var mountsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool IsDefaultDir(string dir)
            {
                string[] p = dir.Split('/');
                return p.Length == 3 && p[1].Equals(SkinsDirName, StringComparison.OrdinalIgnoreCase) && p[2].Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase);
            }
            var ordered = new List<string>(dirs.Count);
            foreach (string dir in dirs) if (IsDefaultDir(dir)) ordered.Add(dir);
            foreach (string dir in dirs) if (!IsDefaultDir(dir)) ordered.Add(dir);
            foreach (string dir in ordered)
            {
                if (!files[dir].ContainsKey("skin.cfg")) continue;
                string[] parts = dir.Split('/');
                if (parts.Length != 3 || !parts[1].Equals(SkinsDirName, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.LogWarning($"Looks: the embedded skin '{dir}' is not under <mount>/skins/<skin>/ (assets\\mounts\\<mount>\\skins\\<skin>\\): left out.");
                    continue;
                }
                try
                {
                    var pics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in files[dir]) pics[f.Key] = f.Value;
                    if (files.TryGetValue(dir + "/front", out var front)) foreach (var f in front) pics[FrontKey + f.Key] = f.Value;
                    if (files.TryGetValue(dir + "/sounds", out var sounds)) foreach (var f in sounds) if (SoundBlock.IsSoundFile(f.Key)) pics[SoundsKey + f.Key] = f.Value;
                    Func<string, byte[]> bytes = Reader(pics);
                    if (refusedMounts.Contains(parts[0])) continue;
                    if (parts[2].Equals(DefaultSkinName, StringComparison.OrdinalIgnoreCase) && !MountCfgIn(files, parts[0]) && !parts[0].Equals(Catalog.DonkeyId, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Log.LogWarning($"Looks: the embedded skin '{dir}': a mount's '{DefaultSkinName}' look comes with its mount.cfg; a folder that adds skins to a mount gives them names of their own. Left out.");
                        continue;
                    }
                    Catalog.Entry mount = Catalog.Find(parts[0]);
                    string mountId = mount != null ? mount.Id : parts[0];
                    SkinDef def = FromCfg(Numbers.CfgText(bytes("skin.cfg")), parts[2], owner, "embedded " + asm.GetName().Name + " " + dir, bytes, pics.Keys, mountId);
                    def.MountDirName = parts[0];
                    def.AddedToOthers = !MountCfgIn(files, parts[0]);
                    def.BuiltIn = builtIn;
                    def.SourceAssembly = asm;
                    Register(def);
                    mountsSeen.Add(mountId);
                    n++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: the embedded skin '{dir}' could not be registered: {e.Message}");
                }
            }
            foreach (string dir in dirs)
            {
                if (dir.IndexOf('/') < 0 && files[dir].ContainsKey(MountCfgName)) NoSkinLeft(Catalog.Find(dir), owner);
            }
            return n;
        }

        // Every mount and skin pack under a folder: <root>/gk2mounts/<mount>/ and <root>/*/gk2mounts/<mount>/ (a pack
        // inside a mod's own folder, or dropped straight into plugins): its mount.cfg, then its skins\<skin>\ (MountPacks.cs).
        // Pictures are read from disk when the skin is first worn, so an installed pack costs nothing until then.
        // Each gk2mounts folder is an owner of its own ("<owner>.<its parent folder>"), so two folders with one id meet
        // the version rule instead of replacing each other silently; the folders and the packs in them are taken in
        // name order, so a start is the same every time (docs/mounts-foundation-2.md 2.2).
        internal static int RegisterFolder(string root, string owner)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return 0;
            using (Catalog.Batch()) return RegisterFolderNow(root, owner);
        }

        private static int RegisterFolderNow(string root, string owner)
        {
            var packDirs = new List<(string Dir, string Owner)>();
            string direct = Path.Combine(root, PackDirName);
            if (Directory.Exists(direct)) packDirs.Add((direct, owner));
            string[] subs;
            try
            {
                subs = Directory.GetDirectories(root);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: the folders in '{root}' could not be listed ({e.Message}); only {Path.Combine(root, PackDirName)} is read.");
                subs = new string[0];
            }
            Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
            foreach (string sub in subs)
            {
                // one folder that can't be read (its rights, a broken link) is left out on its own, never every pack after it
                try
                {
                    string nested = Path.Combine(sub, PackDirName);
                    if (Directory.Exists(nested)) packDirs.Add((nested, owner + "." + Path.GetFileName(sub)));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: '{sub}' could not be read ({e.Message}); left out.");
                }
            }
            int n = 0;
            foreach ((string packDir, string packOwner) in packDirs)
            {
                string[] mountDirs;
                try
                {
                    mountDirs = Directory.GetDirectories(packDir);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: the packs in '{packDir}' could not be listed ({e.Message}); left out.");
                    continue;
                }
                Array.Sort(mountDirs, StringComparer.OrdinalIgnoreCase);
                // the mounts declared here first, so a skin under one of them finds it whatever the folders' order
                foreach (string mountDir in mountDirs)
                {
                    bool oldShape;
                    try { oldShape = !File.Exists(Path.Combine(mountDir, MountCfgName)) && File.Exists(Path.Combine(mountDir, "skin.cfg")); }
                    catch { oldShape = false; }
                    if (oldShape)
                    {
                        Plugin.Log.LogWarning($"Looks: '{mountDir}' holds a skin.cfg: a skin now sits in gk2mounts\\<mount>\\skins\\<skin>\\ (docs/skins.md); left out.");
                    }
                }
                foreach (string mountDir in mountDirs)
                {
                    try
                    {
                        n += RegisterMountFolder(mountDir, packOwner);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Looks: the mount pack '{mountDir}' could not be registered: {e.Message}");
                    }
                }
            }
            return n;
        }

        // One skin folder: skin.cfg plus its pictures (a PNG per frame, or one sheet) and its sounds\; mountId: the mount
        // folder it sits in.
        internal static SkinDef RegisterPack(string skinDir, string owner, string mountId, bool addedToOthers)
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
            string soundsDir = Path.Combine(dir, "sounds");
            if (Directory.Exists(soundsDir))
            {
                foreach (string f in Directory.GetFiles(soundsDir)) if (SoundBlock.IsSoundFile(f)) names.Add(SoundsKey + Path.GetFileName(f));
            }
            byte[] Bytes(string file)
            {
                string f = Numbers.InFolder(dir, file); // never outside the pack (docs/multi-mod-loading.md 2.5)
                return f != null && File.Exists(f) ? File.ReadAllBytes(f) : null;
            }
            SkinDef def = FromCfg(cfgText, Path.GetFileName(skinDir), owner, "pack " + skinDir, Bytes, names, mountId);
            def.MountDirName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(skinDir.TrimEnd('\\', '/'))));
            def.Source = skinDir;
            def.AddedToOthers = addedToOthers;
            def.DiskStamp = PackStamp(def);
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
            ForgetPortraits(d.Id);
            SoundBank.ForgetSkin(d); // its files' entries, so a pack put back is read anew
            SoundBank.UnregisterAll("skin/" + d.Owner + "/" + d.Id); // a code skin's clips (the entries only: the clips are the mod's; Sounds.Rig banks them under its mod)
            // the pack mount of the same owner goes with its last skin (a mount with nothing to wear is not offered); a
            // code mount (not FromPack) is its mod's handle's to withdraw
            Catalog.Entry live = Catalog.Find(d.MountId);
            if (live != null && live.FromPack && live.Owner == owner && Catalog.SkinsOf(live).Count == 0) UnregisterMount(live.Id, owner);
            if (IsDefaultSlot(d))
            {
                // a mount's own look gone: its other skins stand alone (their wearers rebuild)
                RefreshInherited(d.MountId);
                Catalog.Touch(Catalog.Find(d.MountId));
            }
            MarkWearersDirty();
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

        // A [Colors] line: "<Region> = hex hex ..." (spaces or commas), the region one of the mount regions but Coat
        // (Coat is everything not named). A bad region or colour, or a colour named twice, is a format error: a pack
        // that says the wrong thing is refused, like a wrong canvas.
        internal static void ColorRegionLine(SkinDef d, string region, string value, string where)
        {
            string key = null;
            foreach (string r in Colors.MountRegionKeys)
            {
                if (r.Equals(region, StringComparison.OrdinalIgnoreCase)) key = r;
            }
            if (key == null) throw new FormatException($"{where}: [Colors] {region} is not a mount region ({string.Join(", ", Colors.MountRegionKeys)})");
            if (key == "Coat") throw new FormatException($"{where}: [Colors] Coat is every colour not named under another region; don't list it");
            if (d.ColorRegions == null) d.ColorRegions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!d.ColorRegions.TryGetValue(key, out HashSet<string> set)) d.ColorRegions[key] = set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tok in value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string hex = tok.TrimStart('#').ToUpperInvariant();
                if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
                    throw new FormatException($"{where}: [Colors] {key}: '{tok}' is not a colour (RRGGBB)");
                foreach (var other in d.ColorRegions)
                {
                    if (other.Key != key && other.Value.Contains(hex)) throw new FormatException($"{where}: [Colors] {hex} is under {other.Key} and {key}; one region per colour");
                }
                set.Add(hex);
            }
        }

        // skin.cfg: "key = value" lines, "#" comments, a section line ignored. bytes(file) reads a file of the pack;
        // fileNames lists its files (the PNGs are the frames, unless the cfg names a sheet).
        internal static SkinDef FromCfg(string text, string dirName, string owner, string where, Func<string, byte[]> bytes, IEnumerable<string> fileNames, string folderMount = null)
        {
            var d = new SkinDef { Id = dirName, LocalId = dirName, Name = dirName, Owner = owner, DirName = dirName, ReadFile = bytes, FileNames = new List<string>(fileNames) };
            if (!string.IsNullOrEmpty(folderMount)) d.MountId = folderMount;
            string mountLine = null;
            fileNames = d.FileNames;
            var unknown = new List<string>();
            int format = 1;
            string baseId = Donkey.Id, sheet = null, frontSheet = null, frames = null;
            bool pivotGiven = false, eraseGiven = false;
            string section = "skin";
            var mountSections = new List<string>(); // [Mount*] in a skin.cfg: the mount's own file says those now
            var game = new Dictionary<string, string>(StringComparer.Ordinal); // [Game]: prefix, scale, and base key = game frame lines
            string source = null;
            var unknownSections = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[')
                {
                    // a comment after the section name is dropped ("[Colors]  # ..." was read as a section called
                    // "colors] # ..." and ignored, 2026-10-18)
                    int c = line.IndexOf('#');
                    if (c >= 0) line = line.Substring(0, c).Trim();
                    section = line.Trim('[', ']', ' ').ToLowerInvariant();
                    if (section == "mount" || section.StartsWith("mount.", StringComparison.Ordinal) || section == "jump") mountSections.Add(section); // the jump is the creature's, not its look's
                    else if (section != "skin" && section != "tuning" && section != "tuning.ride" && section != "tuning.cart" && section != "game" && section != "sounds" && section != "sounds.ride" && section != "sounds.cart" && section != "sounds.baby" && section != "colors" && !section.StartsWith("base.", StringComparison.Ordinal)) unknownSections.Add(section);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                int hash = value.IndexOf('#');
                if (hash >= 0) value = value.Substring(0, hash).Trim();
                if (section.StartsWith("base.", StringComparison.Ordinal))
                {
                    // [Base.Coat] Brightness = 0.5 ...: the base's region under an over skin, recoloured by the skin
                    string region = Colors.RegionKeyOf(section.Substring(5));
                    if (region == null) { d.Report += $"[{section}]: no such region of the {d.Base.Id} ({string.Join(", ", Colors.MountRegionKeys)}); ignored; "; continue; }
                    if (d.BaseRegions == null) d.BaseRegions = new Dictionary<string, Colors.RegionTweak>(StringComparer.Ordinal);
                    if (!d.BaseRegions.TryGetValue(region, out Colors.RegionTweak tweak)) d.BaseRegions[region] = tweak = new Colors.RegionTweak();
                    if (!tweak.Set(key, value)) d.Report += $"[{section}] {key} = {value}: not a recolour knob or value, ignored; ";
                    continue;
                }
                switch (section)
                {
                    case "tuning": (d.Tuning ?? (d.Tuning = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "tuning.ride": (d.TuningRide ?? (d.TuningRide = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "tuning.cart": (d.TuningCart ?? (d.TuningCart = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "sounds": (d.Sounds ?? (d.Sounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "sounds.ride": (d.SoundsRide ?? (d.SoundsRide = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "sounds.cart": (d.SoundsCart ?? (d.SoundsCart = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "sounds.baby": (d.SoundsBaby ?? (d.SoundsBaby = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value; continue;
                    case "game": game[key] = value; continue;
                    case "colors": ColorRegionLine(d, key, value, where); continue;
                    case "skin": break;
                    default: continue;
                }
                switch (key.ToLowerInvariant())
                {
                    case "format":
                        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out format)) throw new FormatException($"{where}: format = {value} is not a number (this version knows 1)");
                        break;
                    case "needs":
                        // the API version the pack was written for: an older API refuses it with the reason instead of
                        // drawing it with the keys it doesn't know left out (docs/mounts-foundation-2.md 4.5)
                        if (!System.Version.TryParse(value.Trim().IndexOf('.') < 0 ? value.Trim() + ".0" : value.Trim(), out System.Version needs)) throw new FormatException($"{where}: needs = {value} is not a version (like 0.35)");
                        if (needs > Mounts.ApiVersion) throw new FormatException($"{where}: needs GK2 Donkey API {needs}, this is {Mounts.ApiVersion}; update GK2 Donkey API");
                        break;
                    case "description": d.Description = value; d.Given |= SkinGiven.Description; break;
                    case "portrait": d.Portrait = value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value; d.Given |= SkinGiven.Portrait; break; // none: no portrait, not its default's either
                    case "id": if (!LocalOf(value).Equals(dirName, StringComparison.OrdinalIgnoreCase)) d.Report += $"id = {value} ignored: a skin's name is its folder's ('{dirName}'); "; break;
                    case "name": d.Name = value; break;
                    case "author": d.Author = value; break;
                    case "version": d.Version = value; break;
                    case "base": baseId = value; d.Given |= SkinGiven.Base; break;
                    case "colours": d.OwnColours = !value.Equals("palette", StringComparison.OrdinalIgnoreCase); d.Given |= SkinGiven.Colours; break;
                    case "layer": d.Over = value.Equals("over", StringComparison.OrdinalIgnoreCase); d.Given |= SkinGiven.Layer; break;
                    case "erase":
                        eraseGiven = true;
                        d.Given |= SkinGiven.Erase;
                        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) d.Erase = null;
                        else if (Colors.TryHex(value, out Color32 ec)) d.Erase = ec;
                        else throw new FormatException($"{where}: erase = {value} is not a hex colour (RRGGBB) or none");
                        break;
                    case "mount": mountLine = value; break;
                    case "canvas": if (!Pair(value, ref d.CanvasW, ref d.CanvasH)) d.Report += "canvas = " + value + " is not two whole numbers, ignored; "; d.Given |= SkinGiven.Canvas; break;
                    case "pivot": if (!PairF(value, ref d.PivotX, ref d.PivotY, -MaxCanvas, 2 * MaxCanvas)) d.Report += "pivot = " + value + " is not two numbers, ignored; "; pivotGiven = true; d.Given |= SkinGiven.Pivot; break;
                    case "offset": if (!PairF(value, ref d.OffsetRide.x, ref d.OffsetRide.y, -MaxCanvas, MaxCanvas)) d.Report += "offset = " + value + " is not two numbers, ignored; "; if (!text.Contains("offset_cart")) d.OffsetCart = d.OffsetRide; d.Given |= SkinGiven.Offset; break; // "offset = 0 0" given: cancels a default's
                    case "offset_cart": if (!PairF(value, ref d.OffsetCart.x, ref d.OffsetCart.y, -MaxCanvas, MaxCanvas)) d.Report += "offset_cart = " + value + " is not two numbers, ignored; "; d.Given |= SkinGiven.OffsetCart; break;
                    case "sheet": sheet = value; break;
                    case "sheet_front": frontSheet = value; break;
                    case "columns": if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out d.SheetColumns) || d.SheetColumns < 0 || d.SheetColumns > 256) { d.SheetColumns = 0; d.Report += "columns = " + value + " is not a whole number from 1 to 256, ignored; "; } break;
                    case "frames": frames = value; break;
                    case "source": source = value.ToLowerInvariant(); break;
                    case "seat":
                        d.Given |= SkinGiven.Seat;
                        if (value.Equals("picture", StringComparison.OrdinalIgnoreCase)) d.SeatFromPicture = true;
                        else if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) d.SeatRow = -1; // no seat of its own, and not its default's: the player's own seat keys
                        else if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seatRow)) d.SeatRow = seatRow;
                        else { d.Report += "seat = " + value + " is neither picture, none nor a row number, ignored; "; d.Given &= ~SkinGiven.Seat; }
                        break;
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
            if (d.CanvasW < 8 || d.CanvasH < 8 || d.CanvasW > MaxCanvas || d.CanvasH > MaxCanvas) throw new FormatException($"{where}: canvas {d.CanvasW}x{d.CanvasH} is out of range (8..{MaxCanvas}; a sheet is 44 cells of it, {MaxCanvas} is 12 MB)");
            if (!pivotGiven)
            {
                d.PivotX = d.CanvasW / 2f;
                d.PivotY = d.CanvasH / 2f;
            }
            // layer = over on a skin built on its mount's default (not the donkey's, not the default itself): drawn over
            // that default, not over the game's donkey (docs/skin-layering-plan.md 2.4)
            string finalMount = !string.IsNullOrEmpty(folderMount) ? folderMount : mountLine ?? d.MountId;
            if (d.Over && IsVariantSlot(finalMount, d.LocalId))
            {
                d.Over = false;
                d.OverDefault = true;
            }
            if ((d.Over || d.OverDefault) && !eraseGiven) d.Erase = new Color32(255, 0, 255, 255);
            if (d.OverDefault && d.BaseRegions != null)
            {
                d.Report += "[Base.*] recolours the donkey under a skin drawn over it; this one is drawn over its mount's default, ignored; ";
                d.BaseRegions = null;
            }
            if (!d.Over && !d.OverDefault && d.Erase != null)
            {
                d.Report += "erase is for layer = over only, ignored; ";
                d.Erase = null;
            }
            if (!d.Over && d.BaseRegions != null)
            {
                d.Report += "[Base.*] is for layer = over only (a recolour of the donkey under the skin), ignored; ";
                d.BaseRegions = null;
            }
            if (!d.OwnColours && d.ColorRegions != null) d.Report += "[Colors] is for a skin in its own colours (colours = own); a palette skin takes the donkey's regions, ignored; ";
            if (unknown.Count > 0) d.Report += "unknown keys ignored: " + string.Join(", ", unknown) + "; ";
            if (unknownSections.Count > 0) d.Report += "unknown sections ignored: " + string.Join(", ", unknownSections) + "; ";
            int tuningKeys = (d.Tuning?.Count ?? 0) + (d.TuningRide?.Count ?? 0) + (d.TuningCart?.Count ?? 0);
            if (tuningKeys > 0) d.Report += tuningKeys + " tuning key(s)" + (d.TuningRide != null || d.TuningCart != null ? " (some per style)" : "") + "; ";
            int soundLines = ReportSoundBlocks(d); // every line checked, the problems named (LooksSounds.cs)
            if (soundLines > 0) d.Report += soundLines + " sound line(s); ";
            if (mountSections.Count > 0) d.Report += "[" + string.Join("], [", mountSections) + "] ignored: a mount's own values go in its mount.cfg (gk2mounts\\<mount>\\mount.cfg, docs/skins.md); ";
            if (mountLine != null)
            {
                // the skin's mount is its folder's; a code-free cfg read elsewhere (a reload of an old file) takes the line
                if (string.IsNullOrEmpty(folderMount)) d.MountId = mountLine;
                else if (!mountLine.Equals(folderMount, StringComparison.OrdinalIgnoreCase)) d.Report += $"mount = {mountLine} ignored: the skin sits in the folder of the mount '{folderMount}', which is its mount; ";
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
                        case "scale": if (!Numbers.TryFloat(kv.Value, out g.Scale)) g.Scale = 0f; break; // refused below
                        case "lift": if (Numbers.TryFloat(kv.Value, out float lift)) g.Lift = Mathf.Clamp01(lift); else d.Report += "[Game] lift = " + kv.Value + " is not a number, ignored; "; break;
                        default: g.Map[kv.Key] = kv.Value; break;
                    }
                }
                if (string.IsNullOrEmpty(g.Prefix)) throw new FormatException($"{where}: source = game needs a [Game] section with prefix = <the character's preset prefix, like 0901_>");
                if (g.Scale < 1f || g.Scale > 8f || Mathf.Abs(g.Scale - Mathf.Round(g.Scale)) > 0.001f) throw new FormatException($"{where}: [Game] scale = {g.Scale} must be a whole number from 1 to 8");
                var gameUnknown = new List<string>();
                foreach (string gk in g.Map.Keys) if (Array.IndexOf(d.Base.Keys, gk) < 0) gameUnknown.Add(gk); // a line that names no frame key of the base (a typo: lfit) is reported like every other section's
                foreach (string gk in gameUnknown) g.Map.Remove(gk);
                if (gameUnknown.Count > 0) d.Report += "[Game] unknown keys ignored: " + string.Join(", ", gameUnknown) + "; ";
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
                if (frames != null)
                {
                    // a frame's name is a key (bdy_walk_left_03): letters, digits, _ and -; anything else (a path) is left out
                    var ok = new List<string>();
                    foreach (string fk in d.SheetKeys)
                    {
                        bool good = fk.Length <= 64;
                        foreach (char c in fk) if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-'))) good = false;
                        if (good) ok.Add(fk);
                        else d.Report += "frames: '" + fk + "' is not a frame name, its cell is skipped; ";
                    }
                    if (ok.Count != d.SheetKeys.Length) d.SheetKeys = ok.ToArray();
                }
                if (IsVariantSlot(d))
                {
                    // a variant's own pictures are its sheet's: loose PNGs beside it are not read (its default gives the rest)
                    int loose = 0;
                    foreach (string png in KeysOf(fileNames)) if (!(png + ".png").Equals(file, StringComparison.OrdinalIgnoreCase) && !(png + ".png").Equals(frontFile ?? "", StringComparison.OrdinalIgnoreCase) && !(png + ".png").Equals(d.Portrait ?? "", StringComparison.OrdinalIgnoreCase)) loose++;
                    if (loose > 0) d.Report += loose + " loose PNG(s) not read: a skin with sheet = has its sheet's pictures as its own, the rest from its default; ";
                }
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

        // Two numbers, "64 48" (a comma may stand for the space; a decimal comma, "1,5 2", is three tokens and refused).
        private static bool Pair(string v, ref int a, ref int b)
        {
            string[] p = v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && int.TryParse(p[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int x)
                && int.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int y)) { a = x; b = y; return true; }
            return false;
        }

        private static bool PairF(string v, ref float a, ref float b, float min, float max)
        {
            string[] p = v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && Numbers.TryFloat(p[0], out float x) && Numbers.TryFloat(p[1], out float y)) { a = Mathf.Clamp(x, min, max); b = Mathf.Clamp(y, min, max); return true; }
            return false;
        }

        // What the names say before any picture is decoded: typos, a head drawn in walks, what is still to draw.
        private static void Validate(SkinDef d, IEnumerable<string> keys, string what)
        {
            SkinBase b = d.Base;
            var have = new HashSet<string>(StringComparer.Ordinal);
            int front = 0;
            var unknown = new List<string>();
            var walkHead = new List<string>();
            int flight = 0;
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
                if (Flight.IsSkinFrame(k)) { if (!key.StartsWith(FrontKey, StringComparison.Ordinal)) flight++; continue; } // the mod plays them (Flight.FrameFor)
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
            if (flight > 0) sb.Append(", ").Append(flight).Append(" of them the flight's own (take-off, beat, glide, landing)");
            if (missing > 0 && IsVariantSlot(d)) sb.Append(", ").Append(missing).Append(" of the ").Append(b.Id).Append("'s ").Append(b.Keys.Length).Append(" frames from its default (").Append(SkinKey(d.MountId, DefaultSkinName)).Append(")"); // built on it (LooksInherit.cs)
            else if (missing > 0) sb.Append(", ").Append(missing).Append(" of the ").Append(b.Id).Append("'s ").Append(b.Keys.Length).Append(" frames not drawn (the ").Append(b.Id).Append(" shows there)");
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

        // The largest picture side decoded at all (a sheet of 44 cells of the largest canvas fits well inside).
        internal const int MaxPictureSide = 4096;

        // A PNG's pixels (bottom row first), or null with the reason. Its size is read from its header first: a picture
        // larger than maxW x maxH (a frame: its canvas) is refused before anything is decoded (one 16k x 16k PNG in a
        // pack was a gigabyte on the main thread, docs/multi-mod-loading.md 2.2).
        internal static Color32[] DecodePng(byte[] png, out int w, out int h, out string why, int maxW = MaxPictureSide, int maxH = MaxPictureSide, long maxPixels = (long)MaxPictureSide * MaxPictureSide)
        {
            w = h = 0;
            why = null;
            if (png == null)
            {
                why = "no file";
                return null;
            }
            if (!Numbers.PngSize(png, out int hw, out int hh))
            {
                why = "not a PNG file";
                return null;
            }
            if (hw > maxW || hh > maxH || (long)hw * hh > maxPixels)
            {
                w = hw;
                h = hh;
                why = $"it is {hw}x{hh}, larger than {Math.Min(maxW, MaxPictureSide)}x{Math.Min(maxH, MaxPictureSide)}";
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

        // The skin.cfg a skin's values are kept in: a pack's own file, or an embedded skin's in its mod's sources on the
        // machine that has them (the DLL's GK2MountsSourceDir metadata, written by a dev build: Saddle Up's
        // assets\mounts\<mount>\skins\<skin>\; docs/mounts-as-mods.md); null when there is none to write (a code skin, an
        // embedded skin of a release build).
        internal static string CfgPathFor(SkinDef d)
        {
            if (d == null) return null;
            if (!string.IsNullOrEmpty(d.Source)) return Path.Combine(d.Source, "skin.cfg");
            string dir = d.SourceAssembly != null ? Defaults.SourceDirOf(d.SourceAssembly, "GK2MountsSourceDir") : null;
            return dir != null && d.MountDirName != null ? Path.Combine(dir, d.MountDirName, SkinsDirName, d.DirName ?? d.Id, "skin.cfg") : null;
        }


        // The portrait a pack names (a PNG next to its frames), read once; null when it has none or it can't be read.
        private static readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> portraitFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // a read that failed: not tried again (nor warned again)

        // Every wearer looks at its skin again on its next tick: the world's mounts and the paper ones the settings
        // page and the Mounts panel draw (MountCompanion.Paper, never registered: before, a skin offered again after
        // being set aside showed on every mount in the world while the page's picture kept the donkey).
        internal static void MarkWearersDirty()
        {
            Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false);
            MountCompanion.ForEachPaper(p => p.LooksRig.MarkDirty());
        }

        // A skin withdrawn, replaced or reloaded: its portrait (and its mount's stand-in) read again next time.
        internal static void ForgetPortraits(string id)
        {
            MountIcons.Forget(id); // its decoded standing picture too
            foreach (string key in new List<string>(portraits.Keys))
            {
                if (!key.StartsWith(id + "|", StringComparison.OrdinalIgnoreCase) && !key.StartsWith(id + "(mount)|", StringComparison.OrdinalIgnoreCase)) continue;
                if (portraits[key] != null) UnityEngine.Object.Destroy(portraits[key]);
                portraits.Remove(key);
            }
            portraitFailed.RemoveWhere(k => k.StartsWith(id + "|", StringComparison.OrdinalIgnoreCase) || k.StartsWith(id + "(mount)|", StringComparison.OrdinalIgnoreCase));
        }

        internal const int MaxPortraitSide = 512;

        internal static Texture2D PortraitOf(SkinDef d)
        {
            if (d == null) return null;
            if (d.PortraitTexture != null) return d.PortraitTexture;
            if (string.IsNullOrEmpty(d.Portrait) && !d.Has(SkinGiven.Portrait) && d.Parent != null) return PortraitOf(d.Parent); // built on its default, no portrait of its own (nor "portrait = none"): the default's
            if (string.IsNullOrEmpty(d.Portrait) || d.ReadFile == null) return null;
            string key = d.Id + "|" + d.Portrait;
            if (portraits.TryGetValue(key, out Texture2D have) && have != null) return have;
            if (portraitFailed.Contains(key)) return null;
            try
            {
                byte[] png = d.ReadFile(d.Portrait);
                if (png == null) { Plugin.Log.LogWarning($"Looks: skin '{d.Id}': portrait = {d.Portrait} is not in the pack."); portraitFailed.Add(key); return null; }
                if (!Numbers.PngSize(png, out int pw, out int ph) || pw > MaxPortraitSide || ph > MaxPortraitSide)
                {
                    Plugin.Log.LogWarning($"Looks: skin '{d.Id}': portrait = {d.Portrait} is {(pw > 0 ? pw + "x" + ph : "not a PNG")}; a portrait is a PNG of at most {MaxPortraitSide}x{MaxPortraitSide}. None shown.");
                    portraitFailed.Add(key);
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "portrait " + d.Id, filterMode = FilterMode.Point };
                if (!ImageConversion.LoadImage(tex, png, true)) { UnityEngine.Object.Destroy(tex); Plugin.Log.LogWarning($"Looks: skin '{d.Id}': portrait {d.Portrait} is not a picture Unity can read."); portraitFailed.Add(key); return null; }
                portraits[key] = tex;
                return tex;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}': portrait {d.Portrait} could not be read: {e.Message}");
                portraitFailed.Add(key);
                return null;
            }
        }

        // A mount's portrait: its own (a code mount's texture, or the PNG of its mount folder), else its
        // Default skin's, else null (the panel draws the mount instead).
        internal static Texture2D PortraitOfMount(Catalog.Entry k)
        {
            if (k == null) return null;
            if (k.PortraitTexture != null) return k.PortraitTexture;
            if (!string.IsNullOrEmpty(k.Portrait) && k.Pack?.ReadFile != null)
            {
                var stand = new SkinDef { Id = k.Id + "(mount)", Portrait = k.Portrait, ReadFile = k.Pack.ReadFile }; // a PNG of the mount's own folder
                Texture2D own = PortraitOf(stand);
                if (own != null) return own;
            }
            return PortraitOf(Find(DefaultSkinOf(k)));
        }

        // The file rewritten with the live values: the [Skin] section's own lines kept (mount, offset and
        // offset_cart replaced), [Tuning] and [Sounds] (a mount's values are its mount.cfg's: SaveMountCfg) and their sections
        // written anew from the mount, with the comments those sections held kept in place (a comment above a key
        // stays above it, one at the end of its line stays on it; the rest of a section's comments come first).
        // Returns null, or why not.
        internal static string SaveCfg(SkinDef d) => SaveCfg(d, out _);

        // The comments of one rewritten section (the mount block and its tuning sections), placed by the one rule the
        // canonical layout uses (tools/skins/canonical_cfg.py): a comment belongs to what follows it. Lines above the
        // header stay above it (Lead), lines above a key stay above that key (Above), a remark at the end of a key's
        // line stays on it (Inline), lines after the last key of the file's last section stay there (Trail).
        private sealed class SectionNotes
        {
            internal readonly List<string> Lead = new List<string>();
            internal readonly Dictionary<string, List<string>> Above = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, string> Inline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<string> Trail = new List<string>();

            // A key line of the section, with the comment lines that stood above it.
            internal void KeyLine(string line, List<string> above)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) return;
                string key = line.Substring(0, eq).Trim();
                int hash = line.IndexOf('#', eq);
                if (hash > 0) Inline[key] = line.Substring(hash).Trim();
                if (above.Count > 0) Above[key] = new List<string>(above);
            }

            internal void Key(StringBuilder sb, string key, string value)
            {
                if (Above.TryGetValue(key, out List<string> above)) foreach (string l in above) sb.Append(l).Append('\n');
                sb.Append(key).Append(" = ").Append(value);
                if (Inline.TryGetValue(key, out string note)) sb.Append("  ").Append(note);
                sb.Append('\n');
            }
        }

        // changed: false when the file already held exactly this text (nothing written).
        internal static string SaveCfg(SkinDef d, out bool changed)
        {
            changed = false;
            string path = CfgPathFor(d);
            if (path == null) return "no file to write (a code skin, or the sources aren't on this machine)";
            try
            {
                // Read with the canonical layout's rule: blank lines are dropped, a comment belongs to what follows it
                // (the next header, or the next key); the file's own line ending is kept.
                var head = new List<string>();                                   // before the first section
                var skinLines = new List<string>();                              // [Skin]'s own lines (mount, offset, offset_cart replaced)
                var otherSections = new List<(string Header, List<string> Above, List<string> Lines)>(); // [Game], [Colors], [Base.*], anything else: as written
                var notes = new Dictionary<string, SectionNotes>(StringComparer.OrdinalIgnoreCase);     // the rewritten sections: [Tuning*] and [Mount*]
                var pending = new List<string>();                                // comment lines waiting for the next header or key
                string seatNote = null;                                          // the remark on the [Skin] seat line (the key is regenerated)
                bool Rewritten(string sec) => sec != null && (sec == "tuning" || sec.StartsWith("tuning.", StringComparison.Ordinal) || sec == "sounds" || sec.StartsWith("sounds.", StringComparison.Ordinal) || sec == "mount" || sec.StartsWith("mount.", StringComparison.Ordinal)); // [Mount*]: dropped (the mount's own file)
                string section = null;
                string nl = "\n";
                string existing = File.Exists(path) ? File.ReadAllText(path) : null;
                if (existing != null)
                {
                    if (existing.Contains("\r\n")) nl = "\r\n";
                    foreach (string raw in existing.Split('\n'))
                    {
                        string kept = raw.TrimEnd('\r').TrimEnd();
                        string line = kept.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("#"))
                        {
                            if (section == null) head.Add(kept); else pending.Add(kept);
                            continue;
                        }
                        if (line.StartsWith("["))
                        {
                            string header = line.Split('#')[0].Trim();
                            section = header.Trim('[', ']', ' ').ToLowerInvariant();
                            if (Rewritten(section))
                            {
                                var n = new SectionNotes();
                                n.Lead.AddRange(pending);
                                notes[section] = n;
                            }
                            else if (section == "skin") head.AddRange(pending);
                            else otherSections.Add((header, new List<string>(pending), new List<string>()));
                            pending.Clear();
                            continue;
                        }
                        // a key line
                        if (section == "skin")
                        {
                            int eq = line.IndexOf('=');
                            string key = eq > 0 ? line.Substring(0, eq).Trim().ToLowerInvariant() : "";
                            skinLines.AddRange(pending);
                            pending.Clear();
                            if (key == "seat")
                            {
                                int hash = line.IndexOf('#');
                                seatNote = hash > 0 ? line.Substring(hash).Trim() : null;
                                continue;
                            }
                            if (key == "mount" || key == "offset" || key == "offset_cart") continue;
                            skinLines.Add(kept);
                        }
                        else if (Rewritten(section))
                        {
                            notes[section].KeyLine(line, pending);
                            pending.Clear();
                        }
                        else if (otherSections.Count > 0)
                        {
                            otherSections[otherSections.Count - 1].Lines.AddRange(pending);
                            pending.Clear();
                            otherSections[otherSections.Count - 1].Lines.Add(kept);
                        }
                    }
                    // the file's last comments stay in its last section
                    if (Rewritten(section)) notes[section].Trail.AddRange(pending);
                    else if (section == "skin") skinLines.AddRange(pending);
                    else if (section != null && section != "tuning" && otherSections.Count > 0) otherSections[otherSections.Count - 1].Lines.AddRange(pending);
                }
                SectionNotes NotesOf(string name)
                {
                    return notes.TryGetValue(name, out SectionNotes n) ? n : new SectionNotes();
                }
                // Written in the canonical order: [Skin], [Game], [Tuning], the colour sections and the rest as they
                // came, the mount block last.
                var sb = new StringBuilder();
                foreach (string h in head) sb.Append(h).Append('\n');
                sb.Append("[Skin]\n");
                foreach (string l in skinLines) sb.Append(l).Append('\n');
                // a skin built on its mount's default writes only what it says itself (its Given): the values it takes
                // from the default stay the default's, and "none" says it on purpose (docs/skin-layering-plan.md 2.8);
                // every other skin by its values, as always
                bool layered = d.Parent != null;
                if (layered ? d.Has(SkinGiven.Seat) : d.SeatFromPicture || d.SeatRow >= 0)
                {
                    sb.Append("seat = ").Append(d.SeatFromPicture ? "picture" : d.SeatRow >= 0 ? d.SeatRow.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none");
                    if (seatNote != null) sb.Append("  ").Append(seatNote);
                    sb.Append('\n');
                }
                if (layered)
                {
                    if (d.Has(SkinGiven.Offset)) sb.Append("offset = ").Append(Num(d.OffsetRide.x)).Append(' ').Append(Num(d.OffsetRide.y)).Append('\n');
                    if (d.Has(SkinGiven.OffsetCart) || (d.Has(SkinGiven.Offset) && d.OffsetCart != d.OffsetRide)) sb.Append("offset_cart = ").Append(Num(d.OffsetCart.x)).Append(' ').Append(Num(d.OffsetCart.y)).Append('\n');
                }
                else if (d.OffsetRide != Vector2.zero || d.OffsetCart != Vector2.zero)
                {
                    sb.Append("offset = ").Append(Num(d.OffsetRide.x)).Append(' ').Append(Num(d.OffsetRide.y)).Append('\n');
                    if (d.OffsetCart != d.OffsetRide) sb.Append("offset_cart = ").Append(Num(d.OffsetCart.x)).Append(' ').Append(Num(d.OffsetCart.y)).Append('\n');
                }
                if (d.MountDirName == null && d.MountId != Catalog.DonkeyId) sb.Append("mount = ").Append(d.MountId).Append('\n'); // a pack's mount is its folder's
                void WriteTuning()
                {
                    // the skin's own geometry: [Tuning], then [Tuning.Ride], [Tuning.Cart], each with its comments
                    WriteSection(sb, "[Tuning]", AsObjects(d.Tuning), NotesOf("tuning"));
                    WriteSection(sb, "[Tuning.Ride]", d.TuningRide, NotesOf("tuning.ride"));
                    WriteSection(sb, "[Tuning.Cart]", d.TuningCart, NotesOf("tuning.cart"));
                    // the skin's sounds, after its geometry (docs/mount-sounds-design.md)
                    WriteSection(sb, "[Sounds]", AsObjects(d.Sounds), NotesOf("sounds"));
                    WriteSection(sb, "[Sounds.Ride]", AsObjects(d.SoundsRide), NotesOf("sounds.ride"));
                    WriteSection(sb, "[Sounds.Cart]", AsObjects(d.SoundsCart), NotesOf("sounds.cart"));
                    WriteSection(sb, "[Sounds.Baby]", AsObjects(d.SoundsBaby), NotesOf("sounds.baby"));
                }
                bool tuningWritten = false;
                foreach ((string Header, List<string> Above, List<string> Lines) other in otherSections)
                {
                    bool game = other.Header.Trim('[', ']', ' ').Equals("game", StringComparison.OrdinalIgnoreCase);
                    if (!game && !tuningWritten)
                    {
                        WriteTuning();
                        tuningWritten = true;
                    }
                    sb.Append('\n');
                    foreach (string l in other.Above) sb.Append(l).Append('\n');
                    sb.Append(other.Header).Append('\n');
                    foreach (string l in other.Lines) sb.Append(l).Append('\n');
                    if (game && !tuningWritten)
                    {
                        WriteTuning();
                        tuningWritten = true;
                    }
                }
                if (!tuningWritten) WriteTuning();
                string text = nl == "\n" ? sb.ToString() : sb.ToString().Replace("\n", nl);
                if (File.Exists(path) && File.ReadAllText(path) == text) return null; // the same already: no write, no undo step
                File.WriteAllText(path, text, new UTF8Encoding(false));
                changed = true;
                if (d.Source != null)
                {
                    d.DiskStamp = PackStamp(d); // our own write is not a change to read back (the auto-reload)
                    packSeen[d.Id] = d.DiskStamp;
                    packPending.Remove(d.Id);
                }
                Plugin.Log.LogInfo($"Looks: skin '{d.Id}' written to {path}.");
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static Dictionary<string, object> AsObjects(Dictionary<string, string> text)
        {
            if (text == null) return null;
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in text) d[kv.Key] = kv.Value;
            return d;
        }

        private static void WriteSection(StringBuilder sb, string name, Dictionary<string, object> values, SectionNotes notes = null)
        {
            if (values == null || values.Count == 0) return;
            sb.Append('\n');
            if (notes != null) foreach (string l in notes.Lead) sb.Append(l).Append('\n');
            sb.Append(name).Append('\n');
            foreach (var kv in values)
            {
                string v = kv.Value is float f ? Num(f) : kv.Value?.ToString() ?? "";
                if (notes != null) notes.Key(sb, kv.Key, v);
                else sb.Append(kv.Key).Append(" = ").Append(v).Append('\n');
            }
            if (notes != null) foreach (string l in notes.Trail) sb.Append(l).Append('\n');
        }

        private static string Num(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        // The file read again and the skin registered anew (the same owner replaces); a mount it declares takes the
        // file's values in place. Returns null, or why not.
        internal static string ReloadCfg(SkinDef d)
        {
            string path = CfgPathFor(d);
            if (path == null || !File.Exists(path)) return "no file to read";
            if (d.ReadFile == null || d.FileNames == null) return "a code skin: nothing to reload";
            try
            {
                SkinDef fresh = FromCfg(File.ReadAllText(path, Encoding.UTF8), d.DirName ?? d.Id, d.Owner, "reload " + path, d.ReadFile, d.FileNames, d.MountDirName != null ? d.MountId : null);
                fresh.MountDirName = d.MountDirName;
                fresh.AddedToOthers = d.AddedToOthers;
                fresh.BuiltIn = d.BuiltIn;
                fresh.Source = d.Source;
                fresh.SourceAssembly = d.SourceAssembly; // an embedded skin keeps its DLL, so CfgPathFor still finds its sources after a reload
                fresh.DiskStamp = PackStamp(fresh);
                ForgetPortraits(d.Id);
                SoundBank.ForgetSkin(d); // a changed sound file is read anew on the next play
                Register(fresh);
                Catalog.Touch();
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }


        // Dev (Debug.SkinPackAutoReload, docs/sprite-tools.md): a pack in use whose files changed on disk is read again,
        // as the panel's "Reload from the file" does. Once a second, the loaded packs only (the skins being worn); a
        // change is acted on when the files have stood still for a check (an editor's export is not read half written).
        // The pictures it knows are the ones found at the start, as for the button.
        private static float nextPackCheck;
        private static readonly Dictionary<string, long> packSeen = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, long> packPending = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        internal static void TickPackWatch()
        {
            if (Plugin.SkinPackAutoReload == null || !Plugin.SkinPackAutoReload.Value || Time.unscaledTime < nextPackCheck)
            {
                return;
            }
            nextPackCheck = Time.unscaledTime + 1f;
            var mounts = new HashSet<Catalog.Entry>();
            foreach (Loaded l in loaded.Values) if (l.Def.Mount?.Pack != null) mounts.Add(l.Def.Mount);
            TickMountWatch(mounts);
            // the skins being worn, and the defaults they are built on (a default's change reaches its variants: a reload
            // registers it anew, and its variants rebuild on it)
            var watched = new List<SkinDef>();
            foreach (Loaded l in loaded.Values)
            {
                if (!watched.Contains(l.Def)) watched.Add(l.Def);
                if (l.Parent != null && !watched.Contains(l.Parent)) watched.Add(l.Parent);
            }
            foreach (SkinDef d in watched)
            {
                if (Find(d.Id) != d) continue; // withdrawn or replaced, its sheet still held by a sleeper: not the registered skin, nothing to reload
                if (string.IsNullOrEmpty(d.Source) || d.ReadFile == null || d.FileNames == null)
                {
                    continue; // a built-in or a code skin: nothing on disk to watch
                }
                long stamp = PackStamp(d);
                if (!packSeen.TryGetValue(d.Id, out long seen))
                {
                    // the baseline is the files as read, not as they are at the first check: a change made before the
                    // switch went on is a change too (the self-test turned the switch on after saving; 2026-10-19)
                    seen = d.DiskStamp;
                    packSeen[d.Id] = seen;
                }
                if (stamp == seen)
                {
                    packPending.Remove(d.Id);
                    continue;
                }
                if (!packPending.TryGetValue(d.Id, out long pending) || pending != stamp)
                {
                    packPending[d.Id] = stamp; // changed: once more unchanged, then read
                    continue;
                }
                packPending.Remove(d.Id);
                packSeen[d.Id] = stamp;
                string e = ReloadCfg(d);
                if (e == null) Plugin.Log.LogInfo($"Looks: pack '{d.Id}' changed on disk: read again (Debug.SkinPackAutoReload).");
                else Plugin.Log.LogWarning($"Looks: pack '{d.Id}' changed on disk but could not be read again: {e}");
            }
        }

        // The newest write time over the pack's skin.cfg and the pictures it had at the start (0 for one that's gone).
        internal static long PackStamp(SkinDef d)
        {
            long newest = 0;
            if (string.IsNullOrEmpty(d.Source) || d.FileNames == null) return 0; // a built-in (embedded) or a code skin: no folder to watch (2026-10-20: a built-in's reload threw "path1 cannot be null" here)
            void See(string path)
            {
                try
                {
                    if (File.Exists(path)) newest = Math.Max(newest, File.GetLastWriteTimeUtc(path).Ticks);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            See(Path.Combine(d.Source, "skin.cfg"));
            foreach (string name in d.FileNames) See(Path.Combine(d.Source, name.Replace('/', Path.DirectorySeparatorChar)));
            return newest;
        }

        // ------------------------------------------------------------------ mounts: the cart rule, the settings list

        // The mount an instance is about to be ridden as: its Skin key's (wanted, even while that skin still builds).
        internal static Catalog.Entry MountOf(SkinDef d) => d != null ? d.Mount : Catalog.Donkey;

        internal static Catalog.Entry MountWanted(Mount.Instance i) => i?.LooksRig?.MountNow ?? Catalog.Donkey; // the rig's rules (a set-aside skin, another base, a no-foals skin on a foal: the donkey)

        // A mount that doesn't pull the cart: why not, else null (docs/mounts-design.md §3).
        internal static string WhyNotCartByMount(Mount.Instance i)
        {
            if (i == null) return null;
            Catalog.Entry k = MountWanted(i);
            return k.Cart ? null : $"{Who(i, k)} can't pull the cart.";
        }

        // Who a refusal names (the user, 2026-10-27: "it should say the mount name if available or Gerry (mount type) if
        // not"): the player's name for it ("Bella"), else its mount's (Gerry, Rat, Dragon), never "a skull".
        internal static string Who(Mount.Instance i, Catalog.Entry k) => !string.IsNullOrEmpty(i?.DisplayName) ? i.DisplayName : k.Label;

        // A mount that pulls the cart, but fewer items than this cart holds (0.40.0; the user, 2026-10-27: Gerry, who pulls
        // 4, hitched to the six the donkey had loaded): why not, else null. The mount's own cargo only: a mod's session cap
        // (CartSlots) lowers the slots and never refuses; a dev tool's slot floor (the cargo sheet, test loads) never either.
        internal static string WhyNotCartByLoad(Mount.Instance mount, Mount.Instance cart)
        {
            if (mount == null || cart == null || Cargo.SlotFloor > 0) return null;
            Catalog.Entry k = MountWanted(mount);
            if (!k.Cart) return null;
            int n = cart.CargoRig != null ? cart.CargoRig.Count : 0, cap = Catalog.CargoOf(k);
            return n > cap ? $"{Who(mount, k)} pulls {cap} items at most. Take {n - cap} off the cart first." : null;
        }

        // The tuning keys a skin's [Tuning] may set: the ones that exist because of how the pictures are drawn (where
        // the rider sits on them, where the cart meets them). Everything else is the mount's (docs/mounts-design.md §1).
        internal static readonly HashSet<string> SkinTuningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RiderOffsetPx", "TorsoOffsetPx", "SeatAdjustPx", "SeatOffset", "LegAdjustXPx", "LegAdjustYPx", "LegHeightPx", "BackSeatExtraPx",
            "HitchGapPx", "Depth", "CenterOffset",
        };

        // The value a creature's layers give key on instance i for style, in the order Rig.ApplyTuning merges them
        // (another owner's override first, then the skin's [Tuning.Ride]/[Tuning.Cart] for the style, the skin's
        // [Tuning], the mount's per-style tuning, the mount's both-styles tuning), or null when none sets it: for a reader
        // that needs a style the instance isn't in yet (the hitch wants the cart style's gap while the mount is still
        // ridden plain). Keep in step with Rig.ApplyTuning: the geometry is the WORN drawing's (the pictures on screen),
        // the mount is the wanted skin's (the creature's facts), so a hitch while a sheet still builds uses the gap of the
        // pictures the cart is drawn against.
        internal static object LayeredValue(Mount.Instance i, MountStyle style, string key)
        {
            if (i == null) return null;
            if (i.OverrideOwners.TryGetValue(key, out string by) && by != TuningOwner && i.Overrides.TryGetValue(key, out object o)) return o;
            SkinDef d = i.LooksRig?.WornDef ?? i.LooksRig?.WantedDef; // asleep (no drawing worn): the wanted skin's geometry, as the next view will have it
            Catalog.Entry k = i.LooksRig?.MountNow ?? Catalog.Donkey;
            // the skin's own, then (built on its mount's default) the default's: Rig.ApplyTuning's order turned round
            for (SkinDef layer = d; layer != null; layer = layer == d ? d.Parent : null)
            {
                Dictionary<string, object> skinStyle = style == MountStyle.Cart ? layer.TuningCart : layer.TuningRide;
                if (skinStyle != null && skinStyle.TryGetValue(key, out object skinStyled)) return skinStyled;
                if (layer.TuningValues != null && layer.TuningValues.TryGetValue(key, out object typed)) return typed;
                if (layer.Tuning != null && layer.Tuning.TryGetValue(key, out string text)) return text;
            }
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
        internal static IReadOnlyList<(string Id, string Label)> MountChoices()
        {
            var list = new List<(string, string)>();
            foreach (MountInfo k in Mounts.Catalog.All)
            {
                if (k.Skins.Count == 0) continue; // a mount with no skin would be invisible: not offered (docs/mounts-design.md §1)
                bool any = k.Id == Catalog.DonkeyId;
                foreach (string sid in k.Skins) if (Find(sid)?.Unavailable == null) any = true;
                if (!any) continue; // every skin of it set aside (its game frames gone)
                list.Add((k.Id, k.Name));
            }
            return list;
        }

        // The skin a mount comes as, shown as "Default" in its Skin row (the user, 2026-10-18: every mount's default
        // skin is called Default): the donkey's own art for the donkey; for another mount its skin named "default"
        // (a pack's skins\default\, one per mount, 2026-10-24); a code mount that registered none: its first by name.
        internal static string DefaultSkinOf(Catalog.Entry k)
        {
            if (k == Catalog.Donkey) return MountLooks.DonkeyId;
            List<SkinDef> skins = Catalog.SkinsOf(k);
            foreach (SkinDef s in skins) if (DefaultSkinName.Equals(s.LocalId, StringComparison.OrdinalIgnoreCase)) return s.Id;
            skins.Sort((a, b) => string.Compare(a.Name ?? a.Id, b.Name ?? b.Id, StringComparison.OrdinalIgnoreCase));
            return skins.Count > 0 ? skins[0].Id : MountLooks.DonkeyId;
        }

        internal const string DefaultLabel = "Default";

        // For a mount's Skin dropdown: "Default" first (DefaultSkinOf), then its other skins by name.
        internal static IReadOnlyList<(string Id, string Label)> SkinChoicesOf(string mountId)
        {
            Catalog.Entry k = Catalog.Find(mountId) ?? Catalog.Donkey;
            var list = new List<(string Id, string Label)>();
            string def = DefaultSkinOf(k);
            if (k == Catalog.Donkey || Find(def)?.Unavailable == null) list.Add((def, DefaultLabel)); // set aside (its game frames gone): not offered
            var skins = Catalog.SkinsOf(k);
            skins.Sort((a, b) => string.Compare(a.Name ?? a.Id, b.Name ?? b.Id, StringComparison.OrdinalIgnoreCase));
            foreach (SkinDef s in skins)
            {
                if (s.Unavailable != null) continue; // set aside: its game frames are gone
                if (!s.Id.Equals(def, StringComparison.OrdinalIgnoreCase)) list.Add((s.Id, string.IsNullOrEmpty(s.Name) ? s.Id : s.Name));
            }
            return list;
        }

        // A mount's first skin id (the default of its Skin row): the donkey's own art, or the first by name.
        internal static string FirstSkinOf(string mountId)
        {
            IReadOnlyList<(string Id, string Label)> c = SkinChoicesOf(mountId);
            return c.Count > 0 ? c[0].Id : MountLooks.DonkeyId;
        }

        // For a settings page's Mount line: the mount a skin id belongs to and the mount's other skins.
        internal static string MountLineFor(string skinId)
        {
            SkinDef d = string.IsNullOrEmpty(skinId) || skinId.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? null : Find(skinId);
            Catalog.Entry k = MountOf(d);
            var names = new List<string>();
            string def = DefaultSkinOf(k);
            names.Add(DefaultLabel);
            foreach (SkinDef s in Catalog.SkinsOf(k))
            {
                if (s.Unavailable == null && !s.Id.Equals(def, StringComparison.OrdinalIgnoreCase)) names.Add(string.IsNullOrEmpty(s.Name) ? s.Id : s.Name);
            }
            string note = d == null && !string.IsNullOrEmpty(skinId) && !skinId.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? " (the skin '" + skinId + "' isn't installed: drawn as the donkey)"
                : d != null && d.Unavailable != null ? " (set aside: drawn as the donkey)" : "";
            return k.Label + (names.Count > 0 ? " - skins: " + string.Join(", ", names) : "") + note;
        }

        // The skins to pick from: the donkey first, then every skin by mount and name, labelled by the mount when the
        // skin's name doesn't say it (docs/mounts-design.md §6).
        internal static IReadOnlyList<(string Id, string Label)> SkinChoices()
        {
            var list = new List<(string Id, string Label, string Sort)> { (MountLooks.DonkeyId, Catalog.Donkey.Label + ": the game's own", "") };
            var perMount = new Dictionary<Catalog.Entry, int>();
            foreach (SkinDef s in All) if (s.Unavailable == null) perMount[s.Mount] = perMount.TryGetValue(s.Mount, out int n) ? n + 1 : 1;
            foreach (SkinDef s in All)
            {
                if (s.Unavailable != null) continue; // set aside: not to pick
                Catalog.Entry k = s.Mount;
                string name = string.IsNullOrEmpty(s.Name) ? s.Id : s.Name;
                bool says = name.IndexOf(k.Label, StringComparison.OrdinalIgnoreCase) >= 0;
                bool only = k != Catalog.Donkey && perMount[k] == 1;
                string label = says || only ? name : k.Label + ": " + name;
                list.Add((s.Id, label, (k == Catalog.Donkey ? "0" : "1" + k.Label) + "|" + name));
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

        // What building a skin's sheet cost, by step (the loaded line): the user, 2026-10-25: "everytime im selecting the
        // dragon again in the mount journal, game is freezing for some frames"; measured before the cure is chosen.
        internal sealed class BuildCost
        {
            internal static readonly double MsPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            internal long Sheet, Make, Read, Decode, Put, Upload, Sprites, Colours;
            internal long FrameWork, WorstFrameWork, Alloc;
            internal int Frames, LastFrame = -1;
            internal float WorstGameFrame;
            internal static long Now => System.Diagnostics.Stopwatch.GetTimestamp();
            internal static string Ms(long ticks) => (ticks * MsPerTick).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            internal string Line(int pictures)
            {
                long work = Sheet + Make + Put + Upload + Sprites + Colours;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                return $"built over {Frames} frame(s), {Ms(work)} ms of work: the sheet made {Ms(Sheet)}; {pictures} pictures made {Ms(Make)}" +
                    (Read + Decode > 0 ? $" (files read {Ms(Read)}, decoded {Ms(Decode)})" : "") +
                    $", put in the sheet {Ms(Put)}; uploaded {Ms(Upload)}, sprites {Ms(Sprites)}, colours {Ms(Colours)}; " +
                    $"the most in one frame {Ms(WorstFrameWork)} ms, the game's slowest frame meanwhile {(WorstGameFrame * 1000f).ToString("F0", ci)} ms; " +
                    $"allocated {(Alloc / 1048576.0).ToString("F1", ci)} MB";
            }
        }

        internal sealed class Loaded
        {
            internal SkinDef Def;
            internal readonly BuildCost Cost = new BuildCost();
            internal readonly Dictionary<string, Sprite> Frames = new Dictionary<string, Sprite>(StringComparer.Ordinal); // "front/..." too
            // the game's sprite instance -> our sprite (or null): one lookup per part per frame once seen; the front layer's apart
            internal readonly Dictionary<Sprite, Sprite> Resolved = new Dictionary<Sprite, Sprite>();
            internal readonly Dictionary<Sprite, Sprite> ResolvedFront = new Dictionary<Sprite, Sprite>();
            internal readonly HashSet<string> MissingLogged = new HashSet<string>(StringComparer.Ordinal);
            internal int Refs;
            // False until every frame is in the sheet (the switch is whole); a sheet's from the frame after the one it was
            // uploaded in: the picture switching to it took 5 to 12.5 ms in that frame (the trace of 2026-10-25 11:03).
            private bool ready;
            private int readyFrame;
            internal bool Ready => ready && Time.frameCount >= readyFrame;
            internal Queue<string> ToBuild;     // the keys still to produce
            internal Func<string, Pixels> Produce;
            internal int Bad, NotInSheet;
            // Built on a default (LooksInherit.cs): the default it was built on (reused only for the same one), whether its
            // pictures were layered on the default's, how many frames were its own, the default's, or its own drawn over
            // the default's; and the colours that came from the default's pixels (a skin over its default: those are Coat).
            internal SkinDef Parent;
            internal bool Layered;
            internal int OwnPictures, Inherited, Composed;
            internal HashSet<int> ParentColours;
            // The frames an over skin built whole (its erase colour cleared the base there): drawn in the base's place
            // instead of over it (Rig.ApplyPart hides the part for them). By key while building, by sprite after.
            internal readonly HashSet<string> WholeKeys = new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<Sprite> Whole = new HashSet<Sprite>();
            internal int Erased;                // pixels the erase colour cleared, over every frame (the loaded line)
            // The picture's top at the pivot column per key (a row from the bottom; -1: nothing drawn there) and per
            // sheet sprite: where a rider sits on a skin that says "seat = picture" (Rig.TrySeatTop).
            private readonly Dictionary<string, int> seatTopByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<Sprite, int> SeatTopBySprite = new Dictionary<Sprite, int>();
            // The opaque columns of each cell (min, max; the body's edges on the canvas) and the standing frame's drawn
            // width in px: the cart readout measures the body's rear edge with the first, the effect ring scales by the
            // second (docs/mounts-parity-matrix.md 9.3; the donkey's standing frame is 46 px wide).
            internal readonly Dictionary<string, (int Min, int Max)> ColumnsByKey = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
            internal int StandWidthPx;
            // Every distinct colour the pictures draw (RGB of any pixel with alpha above zero: the shader swaps by RGB
            // and never looks at alpha), collected as the cells are put, sorted at Finish so two wearers build the
            // same shared textures; and by mount region (Def.ColorRegions; the rest Coat). docs/mount-colours.md.
            private readonly HashSet<int> colourSet = new HashSet<int>();
            private static readonly HashSet<string> unreadableSaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A code skin's sprite: its colours join the look's (the player's regions reach them) when the texture can
            // be read; said once per skin when it can't (most mods' textures aren't readable).
            private Dictionary<Texture2D, Color32[]> pixelCache; // one copy per texture while the sheet builds (an atlas shared by 44 sprites is read once)
            internal void CollectSpriteColours(Sprite s, string skinId)
            {
                Texture2D t = s.texture;
                if (t == null) return;
                if (!t.isReadable)
                {
                    if (unreadableSaid.Add(skinId)) Plugin.Log.LogInfo($"Looks: skin '{skinId}': its sprites' texture isn't readable, so the player's colours can't reach its colours (make the texture readable, or use a pack).");
                    return;
                }
                Rect r = s.textureRect;
                if (pixelCache == null) pixelCache = new Dictionary<Texture2D, Color32[]>();
                if (!pixelCache.TryGetValue(t, out Color32[] px)) pixelCache[t] = px = t.GetPixels32();
                int w = t.width;
                for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                    for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                    {
                        Color32 c = px[y * w + x];
                        if (c.a != 0) colourSet.Add((c.r << 16) | (c.g << 8) | c.b);
                    }
            }
            internal readonly List<Color32> Colours = new List<Color32>();
            internal readonly Dictionary<string, List<Color32>> ColoursByRegion = new Dictionary<string, List<Color32>>(StringComparer.Ordinal);

            // the sheet: one texture, a cell per key with a one-pixel gutter
            internal Texture2D Sheet;
            private static Color32[] zero;
            private int cols, cellW, cellH, placed;
            private readonly List<(string Key, RectInt Cell)> cells = new List<(string, RectInt)>();

            // The cells the sheet will have, until AdvanceBuilds makes it (-1: made, or none to make); the producers' step
            // before the pictures (Producer.Prepare); the frame the build was asked for (nothing of it runs in that frame:
            // the pick's own work is there; the trace of 2026-10-25 11:36, Gerry's first picture in the click's frame).
            internal int SheetFor = -1;
            internal Action Prepare;
            internal int AskedFrame = -1;

            internal void MakeSheet(int count)
            {
                long t0 = BuildCost.Now;
                try { MakeSheetNow(count); }
                finally { Cost.Sheet += BuildCost.Now - t0; }
            }

            private void MakeSheetNow(int count)
            {
                cellW = Def.CanvasW;
                cellH = Def.CanvasH;
                cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
                int rows = Mathf.Max(1, (count + cols - 1) / cols);
                int w = cols * (cellW + 1) + 1, h = rows * (cellH + 1) + 1;
                int max = SystemInfo.maxTextureSize;
                if (w > max || h > max) throw new InvalidOperationException($"the sheet would be {w}x{h}, over this machine's {max}");
                // Uninitialized: Unity would fill it with zeros first, 14.7 ms for the dragon's 15.6 MB in the frame of a
                // first pick (the trace of 2026-10-25 11:36); the gutters are cleared below and every cell is overwritten.
                Sheet = new Texture2D(w, h, TextureFormat.RGBA32, false, false, true) { name = "gk2skin " + Def.Id, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                // A new texture's memory is undefined: the gutter lines are cleared (the cells are overwritten by Put, and a
                // cell never placed is never drawn). One zero buffer for every sheet, not a canvas-sized array per load.
                int lines = Mathf.Max(w, h);
                if (zero == null || zero.Length < lines) zero = new Color32[lines];
                for (int c = 0; c <= cols; c++) Sheet.SetPixels32(c * (cellW + 1), 0, 1, h, zero);
                for (int r = 0; r <= rows; r++) Sheet.SetPixels32(0, r * (cellH + 1), w, 1, zero);
                Live.TextureMade(Sheet);
            }

            // A frame into its cell: the canvas size exactly, or it is skipped (reported). One reason is said once per skin
            // (a missing sheet was a warning for each of its 44 frames, 88 with a front layer); the count is in the
            // skin's loaded line (Bad).
            private HashSet<string> saidWhy;

            private void SaySkipped(string key, string why)
            {
                if ((saidWhy ?? (saidWhy = new HashSet<string>(StringComparer.Ordinal))).Add(why))
                    Plugin.Log.LogWarning($"Looks: skin '{Def.Id}', picture {key}: skipped ({why}).{(saidWhy.Count == 1 ? " Other pictures skipped for the same reason are counted, not said again." : "")}");
            }

            internal bool Put(string key, Pixels px)
            {
                if (px.P == null)
                {
                    if (px.Why != null)
                    {
                        Bad++;
                        SaySkipped(key, px.Why);
                    }
                    return false;
                }
                if (px.W != cellW || px.H != cellH || px.P.Length != px.W * px.H)
                {
                    Bad++;
                    SaySkipped(key, $"{px.W}x{px.H}, the skin's canvas is {cellW}x{cellH}");
                    return false;
                }
                int i = placed++;
                var cell = new RectInt(1 + (i % cols) * (cellW + 1), 1 + (i / cols) * (cellH + 1), cellW, cellH);
                Sheet.SetPixels32(cell.x, cell.y, cellW, cellH, px.P);
                cells.Add((key, cell));
                HashPicture(key, px.P);
                foreach (Color32 c in px.P)
                {
                    if (c.a != 0) colourSet.Add((c.r << 16) | (c.g << 8) | c.b);
                }
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
                    int minX = cellW, maxX = -1;
                    for (int y = 0; y < cellH; y++)
                    {
                        int row = y * cellW;
                        for (int x = 0; x < cellW; x++)
                        {
                            if (px.P[row + x].a == 0) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                        }
                    }
                    ColumnsByKey[key] = (minX, maxX);
                    if (key == "bdy_static_left" && maxX >= minX) StandWidthPx = maxX - minX + 1;
                }
                return true;
            }

            // The sheet uploaded once, its sprites made: the skin is wearable.
            internal void Finish()
            {
                pixelCache = null; // the colours are collected; the copies go
                long t0 = BuildCost.Now;
                if (Sheet != null)
                {
                    Sheet.Apply(false, true);
                    long t1 = BuildCost.Now;
                    Cost.Upload += t1 - t0;
                    t0 = t1;
                    var pivot = new Vector2(Def.PivotX / cellW, Def.PivotY / cellH);
                    foreach (var (key, cell) in cells)
                    {
                        Sprite sp = Sprite.Create(Sheet, new Rect(cell.x, cell.y, cell.width, cell.height), pivot, Def.Base.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                        sp.name = "gk2skin " + Def.Id + " " + key;
                        Frames[key] = sp;
                        if (seatTopByKey.TryGetValue(key, out int top)) SeatTopBySprite[sp] = top;
                        if (WholeKeys.Contains(key)) Whole.Add(sp);
                    }
                    cells.Clear();
                    long t2 = BuildCost.Now;
                    Cost.Sprites += t2 - t0;
                    t0 = t2;
                }
                SortColours();
                Cost.Colours += BuildCost.Now - t0;
                Perf.TraceNote(Def.Id + " built");
                ready = true;
                readyFrame = Sheet != null ? Time.frameCount + 1 : Time.frameCount; // a sheet: worn from the next frame on (below)
                ToBuild = null;
                Produce = null;
                LoadedReady?.Invoke(Def.Id);
            }

            // The collected colours in a fixed order, and each under its region: the ones Def.ColorRegions names
            // where it says, every other one Coat; on a skin worn over the donkey (layer = over) every other one under
            // no region: kept as drawn (the cloth's gold is not the donkey's coat). A colour named that no picture
            // has is said once (the author's list is stale, or a hex is off by one): it maps nothing and changes nothing.
            private void SortColours()
            {
                Colours.Clear();
                ColoursByRegion.Clear();
                var packed = new List<int>(colourSet);
                packed.Sort();
                foreach (string r in Colors.MountRegionKeys) ColoursByRegion[r] = new List<Color32>();
                var missing = new List<string>();
                var named = new Dictionary<int, string>();
                if (Def.ColorRegions != null)
                {
                    foreach (var kv in Def.ColorRegions)
                    {
                        foreach (string hex in kv.Value)
                        {
                            int v = int.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                            if (colourSet.Contains(v)) named[v] = kv.Key;
                            else if (OwnLine(kv.Key, hex)) missing.Add(kv.Key + " " + hex); // a colour its default names may simply not be in its pictures
                        }
                    }
                }
                foreach (int v in packed)
                {
                    var c = new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255);
                    Colours.Add(c);
                    // over its default: a colour of the default's pixels is Coat, one only in its own pixels is kept as drawn
                    string region = named.TryGetValue(v, out string r) ? r : Def.Over ? null : Def.OverDefault && (ParentColours == null || !ParentColours.Contains(v)) ? null : "Coat";
                    if (region != null) ColoursByRegion[region].Add(c);
                }
                if (missing.Count > 0) Plugin.Log.LogWarning($"Looks: skin '{Def.Id}' [Colors] names {missing.Count} colour(s) its pictures don't have ({string.Join(", ", missing)}); they map nothing.");
            }

            // Whether a [Colors] line is the skin's own (not one of its default's it took).
            private bool OwnLine(string region, string hex) => Def.Parent == null || (Def.OwnColorRegions != null && Def.OwnColorRegions.TryGetValue(region, out HashSet<string> set) && set.Contains(hex));

            internal long SheetBytes => Sheet != null ? (long)Sheet.width * Sheet.height * 4 : 0;

            // Every picture put, in order, hashed (FNV-1a over its key and pixels): what the picture's framing on disk is
            // kept under (PreviewStage), so a changed look is measured again. 0: nothing put.
            internal ulong PixelHash;

            private void HashPicture(string key, Color32[] p)
            {
                ulong h = PixelHash == 0 ? 14695981039346656037UL : PixelHash;
                foreach (char c in key) { h ^= c; h *= 1099511628211UL; }
                for (int i = 0; i < p.Length; i++)
                {
                    Color32 c = p[i];
                    h ^= (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24);
                    h *= 1099511628211UL;
                }
                PixelHash = h;
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

        // A skin's sheet became whole (its colours are known): the companions wearing it set their custom colours'
        // lightness against it again (MountCompanion; the anchor is the look's own art, docs/mount-colours.md 2.4).
        internal static event Action<string> LoadedReady;

        internal static bool IsLoaded(string id) => !string.IsNullOrEmpty(id) && loaded.ContainsKey(id);

        // The whole, loaded sheet of a skin drawn in its own colours, or null (not loaded, still building, or a skin
        // in the donkey's palette colours).
        internal static Loaded LoadedOwnColours(string id)
        {
            return !string.IsNullOrEmpty(id) && loaded.TryGetValue(id, out Loaded l) && l.Ready && l.Def.OwnColours ? l : null;
        }
        internal static int LoadedCount => loaded.Count;

        // A hold on a skin's pictures: loaded on the first, shared after. A sheet is filled a few frames a tick
        // (AdvanceBuilds) and the skin is Ready when whole; a sprite source is ready at once. A skin built on its mount's
        // default (LooksInherit.cs) takes the default's pictures where it has none of its own, and its sheet is kept only
        // for the default it was built on: a new default (replaced, reloaded, withdrawn) is a new sheet.
        private static Loaded Acquire(SkinDef d, string gamePrefix)
        {
            if (loaded.TryGetValue(d.Id, out Loaded l) && l.Def == d && l.Parent == d.Parent)
            {
                l.Refs++;
                return l;
            }
            l = new Loaded { Def = d, Refs = 1, Parent = d.Parent };
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
                                    if (d.OwnColours && p.Length == 0) l.CollectSpriteColours(s, d.Id); // the player's colours reach a readable sprite's colours
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
                else
                {
                    Producer own = MakeProducer(d, gamePrefix, l);
                    Producer parent = null;
                    if (InheritsPictures(d))
                    {
                        try
                        {
                            parent = MakeProducer(d.Parent, gamePrefix, l);
                        }
                        catch (Exception e)
                        {
                            // the default's pictures can't be read (a game-source default whose frames aren't loaded): set
                            // aside as a game skin is, rather than worn half drawn; checked again while it is chosen
                            d.Unavailable = $"its default '{d.Parent.Id}' has no pictures to build on ({e.Message})";
                            d.AsideBy = "build";
                            Plugin.Log.LogInfo($"Looks: skin '{d.Id}' is set aside: {d.Unavailable}; checked again while it is chosen, offered again when they are there.");
                            if (loaded.TryGetValue(d.Id, out Loaded mine) && mine == l) loaded.Remove(d.Id);
                            return null;
                        }
                    }
                    l.Prepare = (Action)Delegate.Combine(own.Prepare, parent?.Prepare);
                    l.AskedFrame = Time.frameCount;
                    if (parent != null)
                    {
                        VariantProducer(l, own, parent);
                    }
                    else
                    {
                        l.SheetFor = own.Keys.Count; // made by AdvanceBuilds, in a frame of its own
                        l.ToBuild = new Queue<string>(own.Keys);
                        l.Produce = own.Produce;
                    }
                    building.Add(l);
                    Perf.TraceNote(d.Id + " build starts");
                }
                if (d.Over && d.Erase != null && l.Produce != null) WrapErase(l, gamePrefix);
                else if (d.OverDefault && !l.Layered && d.Erase != null && l.Produce != null) WrapAlone(l);
            }
            catch (Exception e)
            {
                if (d.Game != null)
                {
                    // a game-source skin with no frames to read (its character's atlas not loaded, or a wrong prefix): set aside
                    // rather than worn as a blank sheet (which would take the player's colours off the donkey and put the
                    // skin's geometry on it); the donkey shows, and the skin is checked again while chosen (RecheckGameSource);
                    // the skins built on it go with it (SetAside)
                    SetAside(d, "its game frames were not found (" + e.Message + ")");
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}' is set aside: {d.Unavailable}; checked again while it is chosen, offered again when they are there.");
                    if (loaded.TryGetValue(d.Id, out Loaded mine) && mine == l) loaded.Remove(d.Id);
                    return null;
                }
                Plugin.Log.LogWarning($"Looks: skin '{d.Id}' could not be loaded: {e.Message}");
                l.Finish(); // ready with nothing: the base shows
            }
            return l;
        }

        // A skin's own pictures apart from the sheet they go into: the keys in sheet order and a picture per key.
        internal sealed class Producer
        {
            internal List<string> Keys;
            internal Func<string, Pixels> Produce;
            internal Action Prepare; // optional: what the first picture would do first, run by AdvanceBuilds in a frame of its own
        }

        // The producer of a skin's pictures, by its source: another game character's frames (LooksGame.cs, throws when the
        // game has none), a recipe over the base's (LooksRecipes.cs), a sheet, or a PNG per key. counts: the sheet whose
        // NotInSheet a short sheet counts.
        private static Producer MakeProducer(SkinDef d, string gamePrefix, Loaded counts)
        {
            if (d.Game != null) return GameProducer(d);
            if (d.Recipe != null) return RecipeProducer(d, gamePrefix);
            if (d.LoadSheetPng != null) return SheetProducer(d, counts);
            var keys = new List<string>(d.Keys());
            return new Producer
            {
                Keys = keys,
                Produce = key =>
                {
                    byte[] png;
                    long t0 = BuildCost.Now;
                    try
                    {
                        png = d.LoadPng(key);
                    }
                    catch (Exception e)
                    {
                        return new Pixels { Why = e.Message };
                    }
                    long t1 = BuildCost.Now;
                    var px = new Pixels();
                    px.P = DecodePng(png, out px.W, out px.H, out px.Why, d.CanvasW, d.CanvasH);
                    if (counts != null)
                    {
                        counts.Cost.Read += t1 - t0;
                        counts.Cost.Decode += BuildCost.Now - t1;
                    }
                    return px;
                },
            };
        }

        // A variant's sheet (docs/skin-layering-plan.md 2.3, 2.4): the default's keys in its order, then the variant's
        // own keys the default lacks (its flight frames, a front picture of its own). Per key its own picture when it has
        // one (an all-transparent one too: a frame blanked on purpose), else the default's; an own picture that can't be
        // used is said and the default's drawn. Drawn over the default (layer = over): the default's picture with the
        // erase colour's pixels cleared and the variant's other pixels on top, as Hellfire over the game's donkey; every
        // frame is then a plain picture of its own (no second layer to draw).
        private static void VariantProducer(Loaded l, Producer own, Producer parent)
        {
            Func<string, Pixels> produce = VariantProduce(l, own, parent, out List<string> keys);
            l.SheetFor = keys.Count; // made by AdvanceBuilds, in a frame of its own
            l.ToBuild = new Queue<string>(keys);
            l.Produce = produce;
        }

        // The variant's picture per key and its keys (VariantProducer; the self-tests' TestPicture, with no sheet).
        private static Func<string, Pixels> VariantProduce(Loaded l, Producer own, Producer parent, out List<string> keyList)
        {
            SkinDef d = l.Def;
            var keys = new List<string>(parent.Keys);
            var parentKeys = new HashSet<string>(parent.Keys, StringComparer.Ordinal);
            var ownKeys = new HashSet<string>(own.Keys, StringComparer.Ordinal);
            foreach (string k in own.Keys) if (!parentKeys.Contains(k)) keys.Add(k);
            l.Layered = true;
            if (d.OverDefault) l.ParentColours = new HashSet<int>();
            Color32? erase = d.OverDefault ? d.Erase : null;
            bool aloneSaid = false;
            keyList = keys;
            return key =>
            {
                Pixels o = ownKeys.Contains(key) ? own.Produce(key) : default(Pixels);
                if ((o.P == null && o.Why != null) || (o.P != null && (o.W != d.CanvasW || o.H != d.CanvasH || o.P.Length != o.W * o.H)))
                {
                    l.Bad++;
                    Plugin.Log.LogWarning($"Looks: skin '{d.Id}', picture {key}: its own is skipped ({(o.P == null ? o.Why : $"{o.W}x{o.H}, the skin's canvas is {d.CanvasW}x{d.CanvasH}")}); the default's is drawn there.");
                    o = default(Pixels);
                }
                Pixels p = parentKeys.Contains(key) ? parent.Produce(key) : default(Pixels);
                if (o.P == null)
                {
                    // nothing of its own: the default's (a Why of the default's is said by Put, as for any picture)
                    if (p.P != null)
                    {
                        l.Inherited++;
                        if (l.ParentColours != null) NoteColours(l.ParentColours, p.P, null);
                    }
                    return p;
                }
                l.OwnPictures++;
                if (!d.OverDefault) return o;
                if (p.P == null || p.W != o.W || p.H != o.H)
                {
                    // over a default that has no picture here: its own alone, the erase pixels left out (never drawn)
                    int cleared = 0;
                    var alone = new Pixels { W = o.W, H = o.H, P = (Color32[])o.P.Clone() };
                    if (erase.HasValue)
                    {
                        Color32 ec = erase.Value;
                        for (int i = 0; i < alone.P.Length; i++)
                        {
                            Color32 c = alone.P[i];
                            if (c.a != 0 && c.r == ec.r && c.g == ec.g && c.b == ec.b) { alone.P[i] = default(Color32); cleared++; }
                        }
                    }
                    if (cleared > 0 && !aloneSaid)
                    {
                        aloneSaid = true;
                        Plugin.Log.LogWarning($"Looks: skin '{d.Id}', picture {key}: drawn over its default, which has no picture there; its erase colour's pixels are left out. Said once.");
                    }
                    return alone;
                }
                var outp = new Pixels { W = p.W, H = p.H, P = ComposeOver(o.P, p.P, erase, out int n) };
                NoteColours(l.ParentColours, outp.P, o.P);
                l.Composed++;
                l.Erased += n;
                return outp;
            };
        }

        // The colours of px (RGB, alpha above zero) into set; with mask, only where mask is transparent (the default's
        // pixels left showing under a variant drawn over it).
        private static void NoteColours(HashSet<int> set, Color32[] px, Color32[] mask)
        {
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                if (c.a == 0 || (mask != null && mask[i].a != 0)) continue;
                set.Add((c.r << 16) | (c.g << 8) | c.b);
            }
        }

        // A skin drawn over its default, built without the default's pictures (no default, another canvas): its own
        // pictures alone, the erase colour's pixels left out (never drawn), said once.
        private static void WrapAlone(Loaded l)
        {
            Func<string, Pixels> inner = l.Produce;
            Color32 erase = l.Def.Erase.Value;
            bool said = false;
            l.Produce = key =>
            {
                Pixels px = inner(key);
                if (px.P == null) return px;
                int n = 0;
                Color32[] outp = null;
                for (int i = 0; i < px.P.Length; i++)
                {
                    Color32 c = px.P[i];
                    if (c.a == 0 || c.r != erase.r || c.g != erase.g || c.b != erase.b) continue;
                    if (outp == null) outp = (Color32[])px.P.Clone();
                    outp[i] = default(Color32);
                    n++;
                }
                if (n == 0) return px;
                if (!said)
                {
                    said = true;
                    Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}' is drawn over its default but built without its pictures ({(l.Def.Parent == null ? "no default look" : "another canvas")}); its erase colour's pixels are left out. Said once.");
                }
                return new Pixels { P = outp, W = px.W, H = px.H };
            };
        }

        // A sheet pack: the one PNG decoded on the first frame, sliced per key after (the cells in reading order); the
        // front layer's sheet the same, for the "front/" keys.
        private static Producer SheetProducer(SkinDef d, Loaded counts)
        {
            string[] keys = d.SheetKeys;
            var all = new List<string>(keys);
            if (d.LoadFrontSheetPng != null) foreach (string k in keys) all.Add(FrontKey + k);
            var sheets = new SheetSource[] { new SheetSource { Load = d.LoadSheetPng }, new SheetSource { Load = d.LoadFrontSheetPng } };
            Func<string, Pixels> produce = key =>
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
                    if (s.Why == null) s.Pixels = DecodePng(png, out s.W, out s.H, out s.Why, d.CanvasW * keys.Length, d.CanvasH * keys.Length, 2L * d.CanvasW * d.CanvasH * keys.Length);
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
                    counts.NotInSheet++; // fewer cells than frames: the base shows there (said once in the loaded line)
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
            return new Producer { Keys = all, Produce = produce };
        }

        private sealed class SheetSource
        {
            internal Func<byte[]> Load;
            internal Color32[] Pixels;
            internal int W, H, Columns;
            internal string Why;
        }

        // ------------------------------------------------------------------ the erase colour of an over skin

        // Every picture of an over skin passes here as it is produced: one with pixels of the skin's erase colour is
        // built whole, the base's own frame (read from the atlas as a recipe reads it) with those pixels transparent
        // and the picture's other pixels on top, and the key is noted (Loaded.WholeKeys: drawn in the base's place).
        // A picture without the colour is left as it is, a plain overlay. The erase pixels themselves are never
        // drawn: a frame whose base can't be read (not in memory, another canvas) gets them cleared, with a line.
        private static void WrapErase(Loaded l, string gamePrefix)
        {
            Func<string, Pixels> inner = l.Produce;
            Color32 erase = l.Def.Erase.Value;
            bool said = false;
            l.Produce = key =>
            {
                Pixels px = inner(key);
                if (px.P == null) return px;
                int n = 0;
                for (int i = 0; i < px.P.Length; i++)
                {
                    Color32 c = px.P[i];
                    if (c.a != 0 && c.r == erase.r && c.g == erase.g && c.b == erase.b) n++;
                }
                if (n == 0) return px;
                SkinFrame f = null;
                string why = null;
                if (key.StartsWith(FrontKey, StringComparison.Ordinal)) why = "the front layer can't erase";
                else
                {
                    SkinBase b = l.Def.Base;
                    FindGameFrames(b, gamePrefix);
                    if (!gameFrames.TryGetValue(key, out Sprite s) || s == null) why = "the base's frame is not in memory";
                    else
                    {
                        f = ReadFrame(key, s, b);
                        if (f == null) why = "the base's frame could not be read";
                        else if (f.W != px.W || f.H != px.H) { why = $"the base's canvas is {f.W}x{f.H}, the picture {px.W}x{px.H}"; f = null; }
                    }
                }
                var outp = new Pixels { W = px.W, H = px.H, P = f != null ? (Color32[])f.P.Clone() : new Color32[px.P.Length] };
                for (int i = 0; i < px.P.Length; i++)
                {
                    Color32 c = px.P[i];
                    if (c.a == 0) { if (f == null) outp.P[i] = default; continue; }
                    if (c.r == erase.r && c.g == erase.g && c.b == erase.b) outp.P[i] = default;  // the hole
                    else outp.P[i] = c;                                                          // the skin's pixel over the base's
                }
                if (f != null)
                {
                    l.WholeKeys.Add(key);
                    l.Erased += n;
                }
                else if (!said)
                {
                    said = true;
                    Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}', picture {key}: its erase colour can't clear the {l.Def.Base.Id} ({why}); those pixels are left out. Said once.");
                }
                return outp;
            };
        }

        // The looks the Mounts panel has shown, held while it is open: picked again, a look is there at once instead of
        // built again (the dragon's 237 pictures took 40 frames each time, and the panel's picture stood still meanwhile;
        // the user, 2026-10-25). Only a look already built is held; closing the panel lets them go (unloaded when nothing
        // else wears them), so the world's rule stands: a sheet nobody wears is freed.
        // At most PanelHoldBytes of sheets (other mods' mounts make the list unbounded): the least recently shown goes
        // first, never the one shown now.
        private static readonly List<Loaded> panelHolds = new List<Loaded>();
        private const long PanelHoldBytes = 64L * 1024 * 1024;

        internal static void HoldForPanel(string id)
        {
            if (string.IsNullOrEmpty(id) || !loaded.TryGetValue(id, out Loaded l) || !l.Ready) return;
            int at = panelHolds.IndexOf(l);
            if (at == panelHolds.Count - 1 && at >= 0) return; // the most recent already
            if (at >= 0) panelHolds.RemoveAt(at);
            else l.Refs++;
            panelHolds.Add(l);
            long bytes = 0;
            foreach (Loaded h in panelHolds) bytes += h.SheetBytes;
            while (bytes > PanelHoldBytes && panelHolds.Count > 1)
            {
                Loaded oldest = panelHolds[0];
                panelHolds.RemoveAt(0);
                bytes -= oldest.SheetBytes;
                Release(oldest);
            }
        }

        // A look's pictures as built, hashed (Loaded.PixelHash); 0 when it has none of its own (a code skin's sprites).
        internal static ulong ContentHash(string id) => !string.IsNullOrEmpty(id) && loaded.TryGetValue(id, out Loaded l) && l.Ready ? l.PixelHash : 0UL;

        internal static void ReleasePanelHolds()
        {
            if (panelHolds.Count == 0) return;
            var let = new List<Loaded>(panelHolds);
            panelHolds.Clear();
            foreach (Loaded l in let) Release(l);
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

        // ------------------------------------------------------------------ the next skin built ahead

        // The skin the player will most likely wear next (the companion's choice, MountCompanion.ApplyLook): its sheet is
        // acquired ahead and built in the background with the others (BuildMsPerFrame a frame), and held, so the first
        // spawn wears it at once. Built on the first spawn, the mount showed as the game's donkey for the build's ~29
        // frames (the dragon's 173 pictures at 6 a frame) and then changed (the user, 2026-10-23: "the first time you
        // press h to spawn the mount, for a couple of frames i see the game donkey instead of my mount"). One skin held
        // at a time: a new choice releases the old one (unloaded when no mount wears it). A reloaded pack's new
        // definition is acquired again.
        internal static string WarmWanted = "";
        private static Loaded warm;

        internal static void TickWarm()
        {
            string id = (WarmWanted ?? "").Trim();
            SkinDef d = id.Length == 0 || id.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) ? null : Find(id);
            if (d != null && d.Unavailable != null) d = null;
            if (warm != null && warm.Def == d && warm.Parent == d.Parent) return; // a new default is a new sheet
            if (warm == null && d == null) return;
            // a skin built from the base's own frames (an over skin that erases, a recipe) waits until they are in memory:
            // built at the main menu it lost its holes (2026-10-24 log: Hellfire "its erase colour can't clear the donkey
            // (the base's frame is not in memory)", 0 of 44 frames found), and the first spawn wore it so
            if (d != null && NeedsBaseFrames(d) && !BaseFramesInMemory(d.Base, d.Base.GamePrefix)) return;
            // a game-source skin (Gerry) waits for the game: at the main menu its character's sprites aren't loaded, so it
            // was looked for (every sprite in memory scanned), refused with an exception and set aside, then offered
            // again after the load (Performance Hog, 2026-10-25 13:43: the exception at every start with Gerry chosen)
            if (d != null && d.Game != null && !Mount.IsGameReady) return;
            Loaded prev = warm;
            warm = null;
            if (d != null)
            {
                try
                {
                    warm = Acquire(d, d.Base.GamePrefix);
                    Plugin.Log.LogInfo($"Looks: skin '{d.Id}' built ahead (the companion's choice), worn at once on the next spawn.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: skin '{d.Id}' could not be built ahead: {e.Message}");
                }
            }
            if (prev != null) Release(prev);
        }

        // ------------------------------------------------------------------ building, a few frames a tick

        // The building's time a frame: pictures are made until it is spent (one at least). Six pictures a frame was 4 to 17
        // ms: a game-source picture is read back through the graphics card, 2 to 3.5 ms each and more when it is busy, and
        // the frame of the click took the sheet and six of them besides the pick (the trace of 2026-10-25 11:03).
        private const double BuildMsPerFrame = 2.0;
        private static readonly long buildTicks = (long)(BuildMsPerFrame / BuildCost.MsPerTick);
        private static readonly List<Loaded> building = new List<Loaded>();
        private static int builtFrame = -1;

        // Called once per frame from the rigs' ticks: the skins being loaded or built, for BuildMsPerFrame. A new sheet is
        // made in a frame of its own (2.4 ms for the dragon's 15.6 MB), its pictures from the next.
        internal static void AdvanceBuilds()
        {
            if (building.Count == 0 || Time.frameCount == builtFrame) return;
            builtFrame = Time.frameCount;
            using (Perf.Measure(Perf.S.LooksBuild))
            {
                long until = BuildCost.Now + buildTicks;
                bool worked = false;
                for (int k = building.Count - 1; k >= 0 && !(worked && BuildCost.Now >= until); k--)
                {
                    Loaded l = building[k];
                    if (l.Refs <= 0 || l.ToBuild == null)
                    {
                        building.RemoveAt(k); // nobody waits for it any more
                        continue;
                    }
                    if (l.AskedFrame == Time.frameCount) continue; // from the next frame
                    BuildCost cost = l.Cost;
                    long f0 = BuildCost.Now, a0 = Perf.AllocatedBytes();
                    if (cost.LastFrame != Time.frameCount)
                    {
                        if (cost.Frames > 0 && Time.unscaledDeltaTime > cost.WorstGameFrame) cost.WorstGameFrame = Time.unscaledDeltaTime;
                        cost.Frames++;
                        cost.LastFrame = Time.frameCount;
                        cost.FrameWork = 0;
                    }
                    if (l.SheetFor >= 0)
                    {
                        int count = l.SheetFor;
                        l.SheetFor = -1;
                        try
                        {
                            l.MakeSheet(count);
                        }
                        catch (Exception e)
                        {
                            // too big for this machine: ready with nothing, the base shows (as Acquire's catch did when it was made there)
                            Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}' could not be loaded: {e.Message}");
                            l.ToBuild.Clear();
                        }
                        cost.FrameWork += BuildCost.Now - f0;
                        if (cost.FrameWork > cost.WorstFrameWork) cost.WorstFrameWork = cost.FrameWork;
                        cost.Alloc += Perf.AllocatedBytes() - a0;
                        if (l.ToBuild.Count > 0) break; // its pictures from the next frame
                        f0 = BuildCost.Now;
                        a0 = Perf.AllocatedBytes();
                    }
                    if (l.Prepare != null)
                    {
                        Action prepare = l.Prepare;
                        l.Prepare = null;
                        try
                        {
                            prepare();
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}': getting ready to build failed ({e.Message}); its pictures do it themselves.");
                        }
                        cost.Make += BuildCost.Now - f0;
                        cost.FrameWork += BuildCost.Now - f0;
                        if (cost.FrameWork > cost.WorstFrameWork) cost.WorstFrameWork = cost.FrameWork;
                        cost.Alloc += Perf.AllocatedBytes() - a0;
                        if (l.ToBuild.Count > 0) break; // its pictures from the next frame
                        f0 = BuildCost.Now;
                        a0 = Perf.AllocatedBytes();
                    }
                    while (l.ToBuild.Count > 0 && !(worked && BuildCost.Now >= until))
                    {
                        string key = l.ToBuild.Dequeue();
                        worked = true;
                        try
                        {
                            long p0 = BuildCost.Now;
                            Pixels made = l.Produce(key);
                            long p1 = BuildCost.Now;
                            l.Put(key, made);
                            cost.Make += p1 - p0;
                            cost.Put += BuildCost.Now - p1;
                        }
                        catch (Exception e)
                        {
                            l.Bad++;
                            Plugin.Log.LogWarning($"Looks: skin '{l.Def.Id}', frame {key}: {e.Message}");
                        }
                    }
                    bool finished = false;
                    if (l.ToBuild.Count == 0)
                    {
                        building.RemoveAt(k);
                        l.Finish();
                        finished = true;
                    }
                    cost.FrameWork += BuildCost.Now - f0;
                    if (cost.FrameWork > cost.WorstFrameWork) cost.WorstFrameWork = cost.FrameWork;
                    cost.Alloc += Perf.AllocatedBytes() - a0;
                    if (finished)
                    {
                        Plugin.Log.LogInfo($"Looks: skin '{l.Def.Id}' loaded: {l.Frames.Count} pictures{(l.Bad > 0 ? ", " + l.Bad + " skipped" : "")}{(l.NotInSheet > 0 ? ", " + l.NotInSheet + " frames past the sheet's last cell (" + (l.Layered ? "its default's" : "the " + l.Def.Base.Id) + " shows there)" : "")}{(l.Layered ? $", built on '{l.Parent.Id}': {l.OwnPictures} own, {l.Inherited} from it{(l.Composed > 0 ? $", {l.Composed} drawn over it ({l.Erased} pixels erased)" : "")}" : "")}{(l.WholeKeys.Count > 0 ? $", {l.WholeKeys.Count} built whole over the {l.Def.Base.Id} ({l.Erased} pixels erased)" : "")} ({l.SizeNote}); {l.Cost.Line(l.Frames.Count)}.");
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
                    Rig rig = attached[k];
                    if (rig.OwnerIdle) continue; // frozen far off: nothing of it changes between frames
                    rig.ApplySafe();
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
                internal bool Body;  // the body part (bdy): the one the flight's own frames replace (Flight.FrameFor)
                internal bool Hid;   // forceRenderingOff set by this rig (cleared only by it)
            }

            private readonly Mount.Instance owner;
            private readonly SkinBase viewBase = Donkey;   // what this view is: the skins it can wear name it
            private SkinDef tunedFor;                       // the skin whose tuning keys are set on the owner now
            private Catalog.Entry tunedMount;
            private MountStyle tunedStyle;
            private readonly List<string> tunedKeys = new List<string>();
            // the mount's abilities on this instance (docs/mounts-design.md §4)
            private Catalog.Entry abilityMount;
            private bool[] abilityOff;
            private bool wasRidden;
            private MountStyle riddenStyle;

            // The mount this instance is now: its skin's (the donkey's while it has none). The skin it wears when it
            // has a view, else the one its "Skin" key names (WantedDef): an instance asleep far from the keeper, with
            // no view to draw on, is still a Gerry. Seen 2026-10-18: the parent asleep outside counted as a donkey, so
            // the companion kept its babies at 0.6 until the parent was called and woke.
            // Creature facts (the mount: cart, babies, effect, abilities) follow the WANTED skin at once (a sheet still
            // building, or no view at all); the drawing's geometry (the tuning merge) follows the WORN skin, so a rider
            // on the old picture never takes the new picture's numbers (docs/mounts-parity-matrix.md 2.10).
            internal Catalog.Entry MountNow => (pending?.Def ?? WantedDef ?? skin?.Def)?.Mount ?? Catalog.Donkey;
            internal SkinDef DefNow => pending?.Def ?? WantedDef ?? skin?.Def;
            internal SkinDef WornDef => skin?.Def;
            // A new look on its way: a sheet still building, or the Skin key naming another skin than the one worn (the
            // settings picture holds its last finished photo meanwhile, PreviewStage).
            internal bool Settling => pending != null || !string.Equals(WantedDef?.Id ?? "", skin?.Def.Id ?? "", StringComparison.OrdinalIgnoreCase);

            // A column of the worn body picture (0 = the canvas's left edge, art facing left) as a world point on the
            // ground row of the body overlay, mirrored with the part: the cart readout measures the body's rear edge.
            internal bool TryBodyColumnWorld(int column, out Vector3 world)
            {
                world = default;
                if (skin == null || slots == null || slots.Length == 0) return false;
                SpriteRenderer ov = slots[0].Overlay;
                if (ov == null || !ov || !ov.enabled || ov.sprite == null) return false;
                SkinDef def = skin.Def;
                float sign = ov.flipX ? -1f : 1f;
                world = ov.transform.TransformPoint(new Vector3((column + 0.5f - def.PivotX) / def.Base.PixelsPerUnit * sign, 0f, 0f));
                return true;
            }

            // The opaque columns of the worn skin's picture for key (the body's edges), or null.
            internal (int Min, int Max)? BodyColumns(string key) => skin != null && skin.ColumnsByKey.TryGetValue(key, out (int Min, int Max) c) && c.Max >= c.Min ? c : ((int, int)?)null;

            // The skin this instance is set to wear, by its "Skin" key, whether or not it has a view: null for the
            // donkey's own look, a skin that isn't installed or drawn on another base, or a no-foals skin on a foal
            // (the same rules as Tick, which logs them; this doesn't).
            internal SkinDef WantedDef
            {
                get
                {
                    if (owner == null) return null;
                    string want;
                    using (Mount.Bind(owner))
                    {
                        want = (Skin.Value ?? "").Trim();
                    }
                    if (want.Length == 0 || want.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return null;
                    SkinDef d = Find(want);
                    if (d == null || d.Unavailable != null || d.Base != viewBase) return null; // set aside (its game frames gone): the donkey comes instead, in mount too, not only in the drawing
                    if (Catalog.Find(d.MountId) == null) return null; // a skin whose mount isn't installed: the donkey as it is, never the skin on the donkey's facts (the choice is kept)
                    if (owner.ViewScale != 1f && !d.Mount.Foals) return null;
                    return d;
                }
            }
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
                for (int i = 0; i < slots.Length; i++) slots[i] = new Slot { Body = viewBase.Parts[i] == "bdy" };
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
                Catalog.Entry mountNow = MountNow; // the wanted skin's mount, read once per tick
                ApplyTuning(mountNow);   // the skin's, the mount's and the style's values, re-applied only when one of them changes
                using (Perf.Measure(Perf.S.Abilities))
                {
                    TickAbilities();
                }
                if (DropCartIfRefused(mountNow)) return; // an unhitch rebuilt the view (Restyle: this rig is detached): nothing of this tick may run after it
                if (pending != null)
                {
                    if (pending.Ready)
                    {
                        Loaded was = skin;
                        skin = pending;
                        pending = null;
                        if (was != null) Looks.Release(was);
                        owner?.ColorsRig.MarkDirty(); // the colours' source lists are the worn look's (Colors.Rig.Rebuild)
                    }
                    else if (dirty)
                    {
                        string stillWant;
                        using (Mount.Bind(owner))
                        {
                            stillWant = (Skin.Value ?? "").Trim();
                        }
                        if (ReferenceEquals(pending.Def, Find(stillWant)) && pending.Parent == pending.Def.Parent) return; // the same skin (the same definition: a replaced or reloaded one is new and must be switched to), still building: a colour or mount change doesn't throw the half-built sheet away
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
                if (!string.Equals(want, wornId, StringComparison.OrdinalIgnoreCase)) missingIdLogged.Remove(want); // a new choice: its line may be said again
                dirty = false;
                wornId = want;
                Loaded next = null;
                if (want.Length > 0 && !want.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase))
                {
                    SkinDef d = Find(want);
                    if (d != null && d.Unavailable != null)
                    {
                        if (RecheckGameSource(d))
                        {
                            dirty = true; // offered again: the next tick wears it
                            return;
                        }
                        if (missingIdLogged.Add(want)) Plugin.Log.LogInfo($"Looks: skin '{want}' is set aside ({d.Unavailable}); the donkey is drawn as the game draws it (the choice is kept).");
                        if (skin == null && pending == null)
                        {
                            dirty = true; // stay dirty: the recheck (throttled) runs again next tick; nothing else to do with no drawing worn
                            return;
                        }
                    }
                    else if (d == null)
                    {
                        if (missingIdLogged.Add(want))
                        {
                            // Not a fault of anything here: the mod or pack that had the skin is gone (mounts come from mods of
                            // their own, docs/mounts-as-mods.md), or a cfg names one that was never there. An Info either way:
                            // a warning is for something to fix, and a player who removed a mod has nothing to fix.
                            Plugin.Log.LogInfo($"Looks: no skin '{want}' is installed (its mod or pack isn't loaded); the donkey is drawn as the game draws it (the choice is kept and comes back with it).");
                        }
                    }
                    else if (d.Base != viewBase)
                    {
                        if (missingIdLogged.Add(want)) Plugin.Log.LogWarning($"Looks: skin '{want}' is drawn on a {d.Base.Id}, not a {viewBase.Id}; not worn.");
                    }
                    else if (owner != null && owner.ViewScale != 1f && !d.Mount.Foals)
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
                owner?.ColorsRig.MarkDirty();
            }

            private static readonly HashSet<string> missingIdLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // The mount's tuning (both styles, then this style's) and the skin's own on top, onto the wearer as overrides
            // owned by the skin system (a key another mod overrides is left to it, said once per skin and key); the
            // previous set cleared first. Re-done only when the skin, the mount or the style changes. Nothing of it is saved.
            internal int TuneStamp { get; private set; }     // counts the re-merges (a reader caching a layered value)
            internal bool OwnerIdle => owner != null && owner.tier == Mount.Tier.Idle; // the view frozen far off (Tiers): nothing to draw anew
            private void ApplyTuning(Catalog.Entry k)
            {
                SkinDef d = skin?.Def;                      // the worn drawing: its geometry
                // k: the wanted skin's mount (MountNow): the creature's facts
                MountStyle style = owner != null ? owner.CurrentStyle : MountStyle.Ride;
                if (d == tunedFor && k == tunedMount && style == tunedStyle) return;
                // The settings picture's paper donkey gets no overrides (no rider to seat), but its stamp still counts
                // the re-merges: the stage reads the layered hitch gap on the stamp, and when the paper returned before
                // it the gap came a second late, on the stage's fallback timer (the cart sliding into place after a
                // mount switch; the user's video and the preview self-test, 2026-10-20).
                bool paper = owner != null && owner.Handle == null && !owner.IsRidden;
                if (!paper) ClearTuning(refresh: false); // one refresh for the whole re-merge, below
                tunedFor = d;
                tunedMount = k;
                tunedStyle = style;
                TuneStamp++;
                if (owner == null || paper) return;
                if (owner.CargoRig != null && owner.CargoRig.ApplySlots()) // the cart's slots follow the mount's cargo (a mount switched while hitched)
                {
                    Plugin.Log.LogInfo($"Cargo: the cart's slot count follows {k.Id}'s cargo ({Catalog.CargoOf(k)}) on {owner.Describe()}.");
                    Mount.OnCargoChanged(); // the hint over what the keeper faces: room on the cart
                }
                var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                // What a mount may pin (docs/mounts-foundation-2.md 2.1): the ride and cart families (speed, the seat and the
                // rider's motion and pose, the cart's geometry, the sounds) and the shuffle's values; never a look key (the
                // skin, the effect, a colour), a behaviour switch (the player's rows: messages, the idle shuffle, the cart's
                // slots and what rides on it) or the lantern's (the cart's own). Said once when refused.
                var fromMount = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void TakeMount(Dictionary<string, object> from)
                {
                    if (from == null) return;
                    foreach (var kv in from)
                    {
                        // its speed is its word ([Mount] speed, MountDef.Speed: the API's numbers), never a number of its own
                        if (kv.Key.Equals("SpeedMultiplier", StringComparison.OrdinalIgnoreCase))
                        {
                            if (tuningLogged.Add(k.Id + "/SpeedMultiplier")) Plugin.Log.LogWarning($"Looks: mount '{k.Id}' sets SpeedMultiplier in its tuning: a mount's speed is one of the API's words ([Mount] speed = {string.Join(" / ", Catalog.SpeedWords())}, MountDef.Speed); not applied.");
                            continue;
                        }
                        merged[kv.Key] = kv.Value;
                        fromMount.Add(kv.Key);
                    }
                }
                TakeMount(k.Tuning);
                TakeMount(style == MountStyle.Cart ? k.TuningCart : k.TuningRide);
                float? ownSpeed = Catalog.OwnSpeed(k, style); // [Mount] speed (with the cart times the donkey's ratio), or Freeplay's one speed: over a SpeedMultiplier in its tuning
                if (ownSpeed.HasValue)
                {
                    merged["SpeedMultiplier"] = ownSpeed.Value;
                    fromMount.Add("SpeedMultiplier");
                }
                if (d != null)
                {
                    // A skin is the drawing: its [Tuning] may move the rider and the cart to fit its pictures, never
                    // change what the mount is (speed, cart, foals, effect belong to the mount; docs/mounts-design.md §1).
                    void TakeSkin(string key, object value)
                    {
                        if (SkinTuningKeys.Contains(key)) { merged[key] = value; fromMount.Remove(key); }
                        else if (tuningLogged.Add(d.Id + "/" + key)) Plugin.Log.LogWarning($"Looks: skin '{d.Id}' wants to set {key}: a skin may only move the rider and the cart to fit its pictures ({string.Join(", ", SkinTuningKeys)}); the mount '{k.Id}' sets the rest. Not applied.");
                    }
                    // a skin built on its mount's default takes the default's geometry first, then its own over it
                    // (LooksInherit.cs; keep in step with LayeredValue)
                    foreach (SkinDef layer in d.Parent != null ? new[] { d.Parent, d } : new[] { d })
                    {
                        if (layer.Tuning != null) foreach (var kv in layer.Tuning) TakeSkin(kv.Key, kv.Value);
                        if (layer.TuningValues != null) foreach (var kv in layer.TuningValues) TakeSkin(kv.Key, kv.Value);
                        Dictionary<string, object> skinStyle = style == MountStyle.Cart ? layer.TuningCart : layer.TuningRide;
                        if (skinStyle != null) foreach (var kv in skinStyle) TakeSkin(kv.Key, kv.Value); // this style's, over [Tuning]
                    }
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
                        if (fromMount.Contains(kv.Key) && !MountMayPin(t.Key))
                        {
                            if (tuningLogged.Add(k.Id + "/" + t.Key)) Plugin.Log.LogWarning($"Looks: mount '{k.Id}' wants to set {t.Key}: a mount may set its speed, seat, pose, sounds, cart geometry and shuffle, never the player's behaviour switches, the lantern or a look key; not applied."); // once per mount and key (keyed on the skin it said it twice: before and after a skin was worn, 2026-10-21)
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

            // The keys a mount's mount.cfg [Tuning] (or a code mount's Tuning) may set on its instances: a ride or cart key that is
            // not the lantern's, or a shuffle key. Not a behaviour key (Mounts.Behaviour: the player's switches), not a
            // colour, not a look key (filtered before this), not the lantern's (the cart's own).
            internal static bool MountMayPin(string key)
            {
                if (Mounts.Behaviour.TryFind(key, out _) || Mounts.Colors.TryFind(key, out _)) return false;
                if (key.StartsWith("Lantern", StringComparison.OrdinalIgnoreCase)) return false;
                return Mounts.Tuning(MountStyle.Ride).TryFind(key, out _) || Mounts.Tuning(MountStyle.Cart).TryFind(key, out _) || Mounts.Shuffle.TryFind(key, out _);
            }

            // The mount's abilities: Attached/Detached when the mount changes, Mounted/Dismounted on the ridden edge (and a
            // style switch), Tick while ridden (or standing, when the ability asks). Only for instances with a handle.
            // An ability that throws is off on this instance for the session, said once.
            private void TickAbilities()
            {
                MountHandle h = owner?.Handle;
                if (h == null) return;
                Catalog.Entry k = MountNow;
                if (k != abilityMount)
                {
                    if (abilityMount != null)
                    {
                        if (wasRidden) CallAll(a => a.Dismounted(h), "Dismounted");
                        CallAll(a => a.Detached(h), "Detached");
                        ForgetAbilityValues(abilityMount);
                    }
                    abilityMount = k;
                    abilityOff = k.Abilities.Length > 0 ? new bool[k.Abilities.Length] : null;
                    wasRidden = false;
                    ForgetAbilityValues(k); // nothing of an earlier life under this owner (an older side file) before Attached
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
                    try
                    {
                        if (!ridden && !a.TicksWhileStanding) continue; // a mod's getter: in the try, as its Tick
                        a.Tick(h, dt);
                    }
                    catch (Exception e)
                    {
                        abilityOff[i] = true;
                        Plugin.Log.LogWarning($"Catalog: ability '{a.Id}' of '{k.Id}' threw on {owner.Describe()}; off for this one: {e.Message}");
                    }
                }
            }

            private void CallAll(Action<MountAbility> call, string what)
            {
                Catalog.Entry k = abilityMount;
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
                        Plugin.Log.LogWarning($"Catalog: ability '{k.Abilities[i].Id}' of '{k.Id}' threw in {what} on {owner.Describe()}; off for this one: {e.Message}");
                    }
                }
            }

            // The instance goes, or its mount is about to be dropped with the skin: the abilities told.
            private void ReleaseAbilities()
            {
                if (abilityMount == null) return;
                MountHandle h = owner?.Handle;
                if (h != null)
                {
                    if (wasRidden) CallAll(a => a.Dismounted(h), "Dismounted");
                    CallAll(a => a.Detached(h), "Detached");
                }
                ForgetAbilityValues(abilityMount);
                abilityMount = null;
                abilityOff = null;
                wasRidden = false;
            }

            // Nothing of a mount's abilities outlives the mount on an instance: every value set under the abilities' owner
            // goes with it (an ability that forgot its Detached, or never got one: a save taken mid-ride).
            private void ForgetAbilityValues(Catalog.Entry k)
            {
                if (k == null || owner == null || string.IsNullOrEmpty(k.AbilityOwner) || k.Abilities.Length == 0) return;
                new InstanceTuning(owner).ClearOwner(k.AbilityOwner);
            }

            // Every override the creature owns on this instance goes, tracked here or not (one moved in with a cart, or
            // read from an old side file, is the creature's to set again, never a fixed value).
            private bool tunedKeysWereCleared;

            private void ClearTuning(bool refresh = true)
            {
                tunedFor = null;
                tunedMount = null;
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
            internal bool TestDirty => dirty;            // the self-test: whether the next tick looks at the skin again
            internal void TestClearDirty() => dirty = false;
            internal void RetuneNow() => tunedMount = null; // the mount's or skin's values changed in place: ApplyTuning re-merges on the next tick

            // The cart rule while hitched (docs/mounts-design.md §3, the user's decision of 2026-10-20): a mount with a
            // view whose (wanted) mount can't pull the cart leaves it standing where it is. Done once per mount; a refused
            // unhitch (ridden over water, say) is tried again once a second and said once, not every tick.
            private Catalog.Entry cartCheckedFor, cartRefusalSaid;
            private float cartRetryAt;
            private bool DropCartIfRefused(Catalog.Entry k)
            {
                if (owner == null || !owner.HasCart || owner.Handle == null) return false;
                // the mount pulls the cart but fewer items than it holds: left standing too, with its load (2026-10-27)
                string overload = k.Cart ? WhyNotCartByLoad(owner, owner) : null;
                if ((k.Cart && overload == null) || (!k.Cart && k == cartCheckedFor)) return false;
                if (Time.unscaledTime < cartRetryAt) return false;
                bool ridden = owner.IsRidden;
                MountHandle left = Mounts.Unhitch(owner.Handle);
                if (left != null)
                {
                    cartCheckedFor = k;
                    cartRefusalSaid = null;
                    Plugin.Log.LogInfo(overload != null
                        ? $"Looks: {owner.Describe()} became a {k.NounText}, which pulls {Catalog.CargoOf(k)} items at most, and the cart holds {left.I.CargoRig.Count}: the cart is left standing where it was, with its load."
                        : $"Looks: {owner.Describe()} became a {k.NounText}, which can't pull the cart: the cart is left standing where it was.");
                    if (overload != null && ridden) Mount.Say(overload);
                    return true;
                }
                cartRetryAt = Time.unscaledTime + 1f;
                if (cartRefusalSaid != k)
                {
                    cartRefusalSaid = k;
                    Plugin.Log.LogInfo($"Looks: {owner.Describe()} became a {k.NounText}, which can't pull the cart, but the cart can't be left here (tried again every second; it stays hitched as data until it can).");
                }
                return false;
            }
            internal string Wearing => skin?.Def.Id ?? "";
            internal bool HasFrame(string key) => skin != null && skin.Frames.ContainsKey(key); // the worn skin has this picture (the flight's glide frames)
            internal bool Building => pending != null;
            // The worn look whose own colours the colour regions map (null: the donkey's own look, or a skin drawn
            // in the donkey's palette colours, which take the donkey's map).
            internal Loaded WornOwnColours => skin != null && skin.Ready && skin.Def.OwnColours ? skin : null;
            // The effect ring's size against the donkey's: the worn skin's standing frame width over the donkey's 46 px
            // (the rat 85 wide gets a ring almost twice the donkey's; an over skin or a code-sprite skin keeps 1.0;
            // docs/mounts-parity-matrix.md 9.3).
            internal const float DonkeyStandWidthPx = 46f;
            internal float EffectScale => skin != null && skin.Ready && !skin.Def.Over && skin.StandWidthPx > 0 ? Mathf.Max(0.5f, skin.StandWidthPx / DonkeyStandWidthPx) : 1f;

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
            // The skin's own seat, if it declares one: seat = picture (this frame's picture top at the pivot column,
            // Gerry) or seat = <row> (a fixed canvas row, from the top: the rider's hip sits on it in every frame, so
            // the mount's seat is the same for every player whatever their AutoSeat; docs/seat-investigation.md).
            // fixedRow says which, so the caller adds the walk bob to a fixed row as the manual seat does.
            internal bool TrySeatTop(out Vector3 world) => TrySeatTop(out world, out _);

            internal bool TrySeatTop(out Vector3 world, out bool fixedRow)
            {
                world = default;
                fixedRow = false;
                if (skin == null || slots == null || slots.Length == 0) return false;
                SkinDef def = skin.Def;
                if (!def.SeatFromPicture && def.SeatRow < 0) return false;
                SpriteRenderer ov = slots[0].Overlay;
                if (ov == null || !ov || !ov.enabled || ov.sprite == null) return false;
                int top;
                if (def.SeatFromPicture)
                {
                    if (!skin.SeatTopBySprite.TryGetValue(ov.sprite, out top) || top < 0) return false;
                }
                else
                {
                    top = Mathf.Clamp(def.CanvasH - 1 - def.SeatRow, 0, def.CanvasH - 1); // the row counted from the bottom, as the picture top is
                    fixedRow = true;
                }
                world = ov.transform.TransformPoint(new Vector3(0f, (top + 1 - def.PivotY) / def.Base.PixelsPerUnit, 0f));
                return true;
            }

            private void ApplyPart(Slot s)
            {
                SpriteRenderer part = s.Part, ov = s.Overlay;
                if (part == null || !part || ov == null || !ov) return;
                Sprite game = part.sprite;
                Sprite mine = game != null && skin != null ? skin.Resolve(game) : null;
                // flying (the flying experiment): the skin's own flying, take-off and landing frames, when it has them,
                // by the flight's timing, in place of whatever walk or idle frame the game shows (docs/flying.md)
                string flyKey = s.Body && skin != null && owner != null ? Flight.FrameFor(owner) : null;
                bool flying = flyKey != null && skin.Frames.TryGetValue(flyKey, out Sprite flyFrame) && (mine = flyFrame) != null;
                bool show = mine != null && part.enabled && part.gameObject.activeInHierarchy;
                if (ov.sprite != mine) ov.sprite = mine;
                if (ov.enabled != show) ov.enabled = show;
                if (show)
                {
                    // A skin drawn over the body (layer = over) sorts one above the part and sits OverLift towards
                    // the camera: the two sprites are coplanar, and when the game draws them by distance (the sprite
                    // material's queue, not the sorting order) a tie in the depth test flickered the overlay in and
                    // out of the body while the donkey moved (Royal Parade, 2026-10-19; steady while idle, the tie
                    // breaking the same way every frame). A replacing skin's part isn't drawn at all.
                    bool over = skin.Def.Over;
                    Follow(ov, part, game, part.sortingLayerID, part.sortingOrder + (over ? 1 : 0), over ? OverLift : 0f);
                }
                // the front layer: the same picture rules, sorted above the keeper while he rides (as the leg overlay,
                // RiderLeg: his layer, one above his body; the legs are +1, this is +2), else just above the part
                SpriteRenderer fr = s.Front;
                if (fr != null && fr)
                {
                    Sprite front = game != null && skin != null && part.enabled && part.gameObject.activeInHierarchy
                        ? (flying ? (skin.Frames.TryGetValue("front/" + flyKey, out Sprite ff) ? ff : null) : skin.Resolve(game, front: true)) : null;
                    bool showFront = front != null;
                    if (fr.sprite != front) fr.sprite = front;
                    if (fr.enabled != showFront) fr.enabled = showFront;
                    if (showFront)
                    {
                        SpriteRenderer keeper = owner?.KeeperBody;
                        if (keeper != null && keeper)
                        {
                            // the game draws sprites by distance, and the keeper's plane sits in front of the mount's
                            // (Depth): the order alone left the wing behind him (the front-layer readback, 2026-10-20),
                            // so the front picture is lifted past the nearest of his sprites, by FrontLift: a fifth of
                            // a pixel (OverLift) tied in the depth buffer and his shirt flickered through the wing while
                            // he moved (the same day)
                            float keeperDepth = owner.KeeperNearestDepth(part.transform); // local -z = towards the camera
                            if (float.IsNaN(keeperDepth)) keeperDepth = part.transform.InverseTransformPoint(keeper.transform.position).z;
                            // Flying, the lift goes along the camera's line of sight: along the part's own -z it moved the
                            // front picture on the screen, the camera looking down at an angle: 3.1 art px lower than the
                            // body picture in every frame of the user's video (2026-10-23 08-07-28, the front picture's
                            // outline registered on the frames at 8 screen px an art px), so the near wing's halves in the
                            // two layers no longer met and the hole round the rider's head sat 3 px low ("there are still
                            // problems with the wing occlusion"). On the ground too since the readout measured it there
                            // (2.7 art px lower, both facings, 2026-10-23): the dragon's fold is drawn that much lower in
                            // its picture instead (make_creatures.py), so it looks the same ridden and unridden, on the
                            // ground and at the take-off and landing, and where it was judged.
                            Follow(fr, part, game, keeper.sortingLayerID, keeper.sortingOrder + 2, FrontLift + Mathf.Max(0f, -keeperDepth), alongView: true);
                        }
                        else Follow(fr, part, game, part.sortingLayerID, part.sortingOrder + 2, OverLift, alongView: true); // above the body overlay too
                        if (s.Body) FrontReadout.Note(owner, part, ov, fr, flying ? flyKey : null);
                    }
                }
                bool hide = show && (!skin.Def.Over || skin.Whole.Contains(mine)); // a whole frame (the erase colour) takes the part's place
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

            // How far an over skin's overlay sits in front of the body part, in world units along the sprite's own
            // facing (its local -z, towards the camera): enough to win the depth test over the coplanar part, a fifth
            // of an art pixel, too little to see. The self-tests zero it for a comparison render.
            internal static float OverLift = 0.004f;

            // How far a skin's front layer sits in front of the nearest of the rider's sprites, world units along the
            // part's facing: 2.5 art pixels, well past the depth buffer's step at the camera's distance (OverLift's fifth
            // of a pixel tied there: the dragon's wing flickered with the rider's shirt while he moved, 2026-10-20);
            // along the view axis, so nothing moves on screen. The front-layer self-test compares both.
            internal static float FrontLift = 0.05f;

            // An overlay takes the part's material, block, flip, colour and the frame's trim offset (plus the skin's own
            // offset), and the sorting it is given; lift: how far in front of the part (OverLift). Nothing is written
            // that is already so.
            // alongView: the lift along the camera's line of sight (nothing moves on the screen, the camera being
            // orthographic), not along the part's -z (which moves the picture on the screen when the camera looks down at
            // an angle); the same distance.
            private void Follow(SpriteRenderer ov, SpriteRenderer part, Sprite game, int layer, int order, float lift = 0f, bool alongView = false)
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
                var local = new Vector3((off.x - Mathf.Floor(off.x) - own.x) / ppu * (part.flipX ? -1f : 1f), (off.y - Mathf.Floor(off.y) + own.y) / ppu, -lift);
                Camera cam = alongView && lift > 0f ? Camera.main : null;
                if (cam != null)
                {
                    Transform pt = part.transform;
                    float world = pt.TransformVector(new Vector3(0f, 0f, lift)).magnitude;
                    local = new Vector3(local.x, local.y, 0f) + pt.InverseTransformVector(-cam.transform.forward * world);
                }
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

            // The instance goes to sleep (its view released far from the keeper): its hold on the skin's pictures goes
            // with the view, so a sheet nobody awake wears is freed and a sheet still building is not held by a
            // sleeper (docs/mounts-foundation-2.md 2.5); the wanted skin is re-acquired by the first tick with a view
            // again. The mount, the tuning and the abilities' state stay: an instance asleep is still its mount.
            internal void Sleep()
            {
                Detach();
                if (skin != null) Looks.Release(skin);
                if (pending != null) Looks.Release(pending);
                skin = null;
                pending = null;
                wornId = "";
                dirty = true;
                tunedFor = null; // the geometry follows the worn drawing: merged again once a drawing is worn
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
            internal SpriteRenderer TestFront() => slots != null && slots.Length > 0 ? slots[0].Front : null;
            internal bool TestFrontShowing() => slots[0].Front != null && slots[0].Front && slots[0].Front.enabled && slots[0].Front.sprite != null;

            // The body's overlay renderer and the body part itself, for the self-tests' readback of what the world
            // draws (the real materials, the real sorting).
            internal SpriteRenderer TestOverlay() => slots != null && slots.Length > 0 && slots[0].Overlay != null && slots[0].Overlay ? slots[0].Overlay : null;
            internal SpriteRenderer TestPart() => slots != null && slots.Length > 0 && slots[0].Part != null && slots[0].Part ? slots[0].Part : null;
        }
    }
}
