using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // The skins the donkey can wear (docs/skins-design.md; docs/skins.md for pack authors): the game's own look
    // ("donkey"), the built-in skins, every skin pack found on disk, and the skins mods register here. A skin is named
    // within its mount: its Id is "<mount id>/<skin>" ("saddleup.dragon/default", "donkey/hellfire"), and every mount
    // but the donkey has one skin called "default", its own look. A donkey wears one through its instance tuning key
    // "Skin" (handle.Tuning.Set(owner, "Skin", info.Id); "donkey" or "" is the game's own look), or through
    // MountCompanion.Skin.
    //
    // Registering: a skin is a picture per donkey frame (FrameKeys names the 44 frames: "bdy_walk_left_03", ...), 96x96
    // facing left with the donkey's anchor in the middle, or a bigger canvas with the same centre. Four ways:
    //   - RegisterFolder: mount packs under a folder of yours, sorted by mount: gk2mounts/<mount>/mount.cfg (the mount,
    //     its sounds/ beside it) and gk2mounts/<mount>/skins/<skin>/ (skin.cfg and PNGs; a folder without mount.cfg
    //     adds skins to a mount registered elsewhere, gk2mounts/donkey/skins/ to the donkey); packs anywhere under
    //     BepInEx/plugins are found by the API itself at start, so most mods need nothing here (docs/skins.md);
    //   - RegisterEmbedded: the same tree embedded in your DLL (EmbeddedResource with logical names
    //     "<prefix><mount>\mount.cfg", "<prefix><mount>\skins\<skin>\<file>", e.g.
    //     "MyMod.mounts.me.rat\skins\default\bdy_walk_left_03.png");
    //   - Register: pictures from your own code (IMountSkinSource gives a Texture2D per frame key; the textures stay
    //     yours, the API never destroys them);
    //   - RegisterRecipe: a rule over the donkey's own frames (a function from the frame's pixels to new pixels), so the
    //     result covers every clip and nothing of the game's art is shipped. Built a few frames a tick when first worn.
    // Every registration returns a handle: dispose it to withdraw the skin (the donkeys wearing it show the game's own
    // look from their next frame and keep their choice). Register in Awake: a skin registered after Saddle Up's settings
    // page is made works at once through the cfg and is in its list from the page's catch-up (MountSettingsPage.CatchUp).
    public sealed class MountLooks
    {
        internal MountLooks()
        {
        }

        // The id of the game's own look, first in Skins.
        public const string DonkeyId = "donkey";

        // Every skin, the game's own look first, then the built-in ones, then packs and mods' skins, each group by name.
        public IReadOnlyList<MountSkinInfo> Skins
        {
            get
            {
                var list = new List<MountSkinInfo> { MountSkinInfo.Donkey };
                var rest = new List<MountSkinInfo>();
                foreach (Looks.SkinDef d in Looks.All)
                {
                    rest.Add(new MountSkinInfo(d));
                }
                rest.Sort((a, b) => a.IsBuiltIn != b.IsBuiltIn ? (a.IsBuiltIn ? -1 : 1) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                list.AddRange(rest);
                return list;
            }
        }

        // A skin by its full name ("saddleup.dragon/default"; case doesn't matter), or null. "donkey" and "" give the
        // game's own look. A short name finds one when it is clear: a mount's id gives its default, a skin's own name
        // the skin when only one mount has a skin of that name.
        public MountSkinInfo Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Trim().Equals(DonkeyId, StringComparison.OrdinalIgnoreCase))
            {
                return MountSkinInfo.Donkey;
            }
            Looks.SkinDef d = Looks.Find(id);
            return d == null ? null : new MountSkinInfo(d);
        }

        // What the loader found for a skin: its pictures, names that match no frame, frames still to draw.
        public string Report(string id) => Looks.Validate(id);

        // The donkey's frame keys: the names a skin's pictures use (44: the body standing, walking, its ear, grazing,
        // chewing, lips, turning its head; the standing head).
        public IReadOnlyList<string> FrameKeys => Looks.KnownKeys;

        // Raised after a skin is registered or withdrawn (a settings list may re-read Skins).
        public event Action SkinsChanged;

        internal void RaiseChanged()
        {
            Action h = SkinsChanged;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Looks: a SkinsChanged handler of {Said.ModOf(d)} threw: {e.Message}");
                }
            }
        }

        // A skin from your own code, a look of def.Mount named def.Id within it ("default" for your mount's own look).
        // The name must be new for that mount (or yours already: then it replaces). Dispose to withdraw.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // the calling mod's assembly (Mounts.NoteOwner)
        public IDisposable Register(string ownerId, MountSkinDef def)
        {
            Owner(ownerId);
            Mounts.NoteOwner(ownerId, Assembly.GetCallingAssembly());
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.Source == null) throw new ArgumentException("A MountSkinDef needs a Source (IMountSkinSource).", nameof(def));
            if (!Mounts.MountsEnabled) return Mounts.KeptOut(ownerId, "the skin", def.Id);
            Looks.SkinDef d = def.ToDef(ownerId);
            d.Sprites = def.Source; // the mod's sprites stay the mod's
            d.FrontSprites = def.FrontSource;
            try
            {
                Looks.Register(d);
            }
            catch (Looks.LeftOut e)
            {
                // the rules between mods, not a mistake in the call: a warning, and a handle that does nothing
                Plugin.Log.LogWarning("Looks: " + e.Message);
            }
            return new Handle(d);
        }

        // A rule over the donkey's own frames: pixels(frame) returns the new pixels (the same size as frame.Pixels, the
        // same layout). Built when first worn, a few frames a tick, and worn once complete.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // the calling mod's assembly (Mounts.NoteOwner)
        public IDisposable RegisterRecipe(string ownerId, string id, string name, Func<MountSkinFrame, Color32[]> pixels, string author = null, string version = null)
        {
            Owner(ownerId);
            Mounts.NoteOwner(ownerId, Assembly.GetCallingAssembly());
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (!Mounts.MountsEnabled) return Mounts.KeptOut(ownerId, "the recipe skin", id);
            var d = new Looks.SkinDef
            {
                Id = id, Name = name, Author = author, Version = version, Owner = ownerId, OwnColours = true,
                Recipe = f => pixels(new MountSkinFrame(f)),
                Report = "a recipe: drawn from the donkey's own frames when first worn",
            };
            try
            {
                Looks.Register(d);
            }
            catch (Looks.LeftOut e)
            {
                Plugin.Log.LogWarning("Looks: " + e.Message);
            }
            return new Handle(d);
        }

        // Every mount pack under a folder: <folder>/gk2mounts/<mount>/ and <folder>/*/gk2mounts/<mount>/ (mount.cfg,
        // sounds/, skins/<skin>/). Returns how many skins registered.
        public int RegisterFolder(string ownerId, string folder)
        {
            Owner(ownerId);
            if (!Mounts.MountsEnabled)
            {
                Mounts.KeptOut(ownerId, "the skin packs under", folder);
                return 0;
            }
            return Looks.RegisterFolder(folder, ownerId);
        }

        // Every mount and skin embedded in an assembly under a resource-name prefix (<prefix><mount>\mount.cfg,
        // <prefix><mount>\sounds\<file>, <prefix><mount>\skins\<skin>\<file>). Returns how many skins registered.
        public int RegisterEmbedded(string ownerId, Assembly assembly, string prefix)
        {
            Owner(ownerId);
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A resource-name prefix is required.", nameof(prefix));
            if (!Mounts.MountsEnabled)
            {
                Mounts.KeptOut(ownerId, "the embedded skins under", prefix);
                return 0;
            }
            return Looks.RegisterEmbedded(assembly, prefix, ownerId);
        }

        // Withdraws a skin this owner registered (packs and embedded skins included). False if it isn't theirs.
        public bool Unregister(string ownerId, string id)
        {
            Owner(ownerId);
            return Looks.Unregister(id, ownerId);
        }

        private static void Owner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.");
            }
        }

        // Withdraws exactly the skin it registered, never one registered since under the same name.
        private sealed class Handle : IDisposable
        {
            private readonly Looks.SkinDef skin;
            private bool done;

            internal Handle(Looks.SkinDef skin)
            {
                this.skin = skin;
            }

            public void Dispose()
            {
                if (done) return;
                done = true;
                if (skin.Id != null && Looks.Find(skin.Id) == skin) Looks.Unregister(skin.Id, skin.Owner);
            }
        }
    }

    // What a skin registered from code says about itself (the same as a pack's skin.cfg).
    public sealed class MountSkinDef
    {
        // The skin's name within its mount ("default": the mount's own look, one per mount; "frost"), or its full
        // name ("me.dragon/frost"). The registered skin's Id is "<Mount>/<name>".
        public string Id;
        public string Name;
        public string Author;
        public string Version;
        // Drawn in the donkey's exact palette colours, so the player's coat and harness colours recolour it.
        public bool PaletteColours;
        // Drawn over the donkey (a saddle, a hat) instead of in its place. On a skin of another mount (not its "default")
        // not drawn over that default yet: refused with a line in its report, drawn in its place.
        public bool Over;
        // The mount this skin is a look of (Mounts.Catalog: "<mod>.<name>"; "donkey" by default: a differently drawn donkey).
        public string Mount = MountCatalog.DonkeyId;
        // For information (the sprites carry their own size and pivot): the pictures' size and where the donkey's
        // anchor sits in them, from the bottom left. On a mount other than the donkey, a skin that isn't the mount's
        // "default" is built on that default: every value below left at its "not said" value (96x96 and 48,48 here,
        // -1 and false for the seat, zero and null for the offsets, null for the effect, description, portrait and
        // Regions) is the default's (docs/skins.md "Variants: built on the default").
        public int CanvasWidth = 96, CanvasHeight = 96;
        public float PivotX = 48f, PivotY = 48f;
        // The pictures moved on the donkey, in art pixels (x towards the head, y up): riding, and with the cart hitched
        // (a creature that should stand closer to the shafts). Null: the same as riding.
        public Vector2 Offset;
        public Vector2? CartOffset;
        // The canvas row (from the top) the rider's hip sits on, every frame and for every player whatever their
        // AutoSeat setting; the mount's SeatAdjustPx nudges from it. -1: the base's seat (the player's own keys); on a
        // skin built on its mount's default, -1 (with SeatFromPicture false) is the default's seat.
        // A pack says the same with "seat = <row>" (docs/skins.md, docs/seat-investigation.md).
        public int SeatRow = -1;
        // The skin's own values for existing tuning keys (SeatAdjustPx, RiderOffsetPx, LegAdjustYPx, TorsoOffsetPx, ...),
        // typed as the key is: set on the wearer as overrides owned by the skin while it is worn (a key another mod has
        // set on that donkey is left to it), cleared when it comes off, never saved. Null: none.
        public Dictionary<string, object> Tuning;
        // The same for one style only, on top of Tuning: riding, and with the cart hitched (a pack's [Tuning.Ride] /
        // [Tuning.Cart]). The skin owns its drawing's geometry: the seat row and these keys; the mount keeps what the
        // creature is (docs/mounts-design.md).
        public Dictionary<string, object> TuningRide, TuningCart;
        // The skin's own sounds by event key ("voice", "step.grass": MountSound; a pack's [Sounds] block): over its
        // mount's, under a mod's override; SoundsRide / SoundsCart for one style, over Sounds. Null: the mount's.
        public Dictionary<string, MountSound> Sounds, SoundsRide, SoundsCart;
        // The babies' own sounds (an instance drawn smaller than the grown one), over the blocks above for them: a
        // squeak where the grown one roars; a pack's [Sounds.Baby]. Null: the grown one's, pitched up by the size.
        public Dictionary<string, MountSound> SoundsBaby;
        // The rider sits on this frame's picture top instead of a row (a game creature that hops: Gerry).
        public bool SeatFromPicture;
        // Which of the skin's own colours are Dark, Muzzle, Eye or Harness (hex RRGGBB per region; everything not
        // named is Coat), as a pack's [Colors]; the player's colours then reach them. Null: all Coat.
        public Dictionary<string, string[]> Regions;
        // Over skins only: the colour whose pixels make the donkey transparent there (a pack's erase =); null: none.
        public Color32? Erase;
        // Over skins only: the donkey's regions recoloured under the skin (a pack's [Base.<Region>]: knob -> value text,
        // the knobs Hue, Saturation, Brightness, Contrast, Colorize, ColorizeAmount, Tint, Overlay, OverlayAmount).
        public Dictionary<string, Dictionary<string, string>> BaseRegions;
        // The pictures, by frame key.
        public IMountSkinSource Source;
        // The front layer, optional: pictures drawn in front of the rider (the near wing, a saddle's near side), by the
        // same frame keys; a missing key draws nothing there.
        public IMountSkinSource FrontSource;
        // For the Mounts panel: a line or two about the look, and a portrait for its list (your own texture, kept by
        // you; null: the panel draws the mount wearing it).
        public string Description;
        public Texture2D Portrait;

        private static Vector2 FiniteOffset(Vector2 v) => new Vector2(Numbers.Finite(v.x, -Looks.MaxCanvas, Looks.MaxCanvas, 0f), Numbers.Finite(v.y, -Looks.MaxCanvas, Looks.MaxCanvas, 0f));

        internal Looks.SkinDef ToDef(string owner)
        {
            var d = new Looks.SkinDef
            {
                Id = Id, Name = string.IsNullOrEmpty(Name) ? Id : Name, Author = Author, Version = Version, Owner = owner,
                OwnColours = !PaletteColours, Over = Over, CanvasW = CanvasWidth, CanvasH = CanvasHeight,
                PivotX = Numbers.Finite(PivotX, -Looks.MaxCanvas, 2 * Looks.MaxCanvas, 48f), PivotY = Numbers.Finite(PivotY, -Looks.MaxCanvas, 2 * Looks.MaxCanvas, 48f),
                MountId = string.IsNullOrWhiteSpace(Mount) ? MountCatalog.DonkeyId : Mount.Trim(),
                OffsetRide = FiniteOffset(Offset), OffsetCart = FiniteOffset(CartOffset ?? Offset), SeatRow = SeatRow,
                TuningValues = Tuning != null && Tuning.Count > 0 ? new Dictionary<string, object>(Tuning, StringComparer.OrdinalIgnoreCase) : null,
                TuningRide = TuningRide != null && TuningRide.Count > 0 ? new Dictionary<string, object>(TuningRide, StringComparer.OrdinalIgnoreCase) : null,
                TuningCart = TuningCart != null && TuningCart.Count > 0 ? new Dictionary<string, object>(TuningCart, StringComparer.OrdinalIgnoreCase) : null,
                SeatFromPicture = SeatFromPicture, Erase = Over ? Erase : null,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(), PortraitTexture = Portrait,
                Report = "from code" + ((Tuning?.Count ?? 0) + (TuningRide?.Count ?? 0) + (TuningCart?.Count ?? 0) is int n && n > 0 ? ", " + n + " tuning key(s)" : "")
                    + ((Sounds?.Count ?? 0) + (SoundsRide?.Count ?? 0) + (SoundsCart?.Count ?? 0) + (SoundsBaby?.Count ?? 0) is int ns && ns > 0 ? ", " + ns + " sound(s)" : ""),
            };
            // what the mod said, by the sentinels (docs/skin-layering-plan.md 2.2): on a mount other than the donkey, a skin
            // that isn't the mount's own look takes the rest from that default
            d.Given = Looks.SkinGiven.Colours | Looks.SkinGiven.Layer;
            if (CanvasWidth != 96 || CanvasHeight != 96) d.Given |= Looks.SkinGiven.Canvas;
            if (PivotX != 48f || PivotY != 48f) d.Given |= Looks.SkinGiven.Pivot;
            if (SeatRow >= 0 || SeatFromPicture) d.Given |= Looks.SkinGiven.Seat;
            if (Offset != Vector2.zero || CartOffset.HasValue) d.Given |= Looks.SkinGiven.Offset;
            if (CartOffset.HasValue) d.Given |= Looks.SkinGiven.OffsetCart;
            if (!string.IsNullOrWhiteSpace(Description)) d.Given |= Looks.SkinGiven.Description;
            if (Portrait != null) d.Given |= Looks.SkinGiven.Portrait;
            if (Over && Erase.HasValue) d.Given |= Looks.SkinGiven.Erase;
            if ((Sounds?.Count ?? 0) + (SoundsRide?.Count ?? 0) + (SoundsCart?.Count ?? 0) + (SoundsBaby?.Count ?? 0) > 0) d.SoundBlocks =Looks.BlocksFromApi(Sounds, SoundsRide, SoundsCart, SoundsBaby, "Sounds", "skin " + Id);
            if (Regions != null && PaletteColours) d.Report += "Regions on a skin in the donkey's palette colours, ignored (the donkey's regions apply); ";
            else if (Regions != null)
            {
                foreach (var kv in Regions)
                {
                    if (kv.Value == null || kv.Value.Length == 0) continue;
                    Looks.ColorRegionLine(d, kv.Key, string.Join(" ", kv.Value), "skin '" + Id + "' (code)");
                }
            }
            if (BaseRegions != null && Over)
            {
                foreach (var kv in BaseRegions)
                {
                    string region = Colors.RegionKeyOf(kv.Key);
                    if (region == null) { d.Report += "[Base." + kv.Key + "]: no such region; ignored; "; continue; }
                    if (d.BaseRegions == null) d.BaseRegions = new Dictionary<string, Colors.RegionTweak>(StringComparer.Ordinal);
                    if (!d.BaseRegions.TryGetValue(region, out Colors.RegionTweak tweak)) d.BaseRegions[region] = tweak = new Colors.RegionTweak();
                    foreach (var knob in kv.Value ?? new Dictionary<string, string>()) if (!tweak.Set(knob.Key, knob.Value)) d.Report += "[Base." + kv.Key + "] " + knob.Key + ": not a recolour knob or value, ignored; ";
                }
            }
            else if (BaseRegions != null) d.Report += "BaseRegions is for an Over skin, ignored; ";
            return d;
        }
    }

    // Where a code skin's pictures come from. Keys lists the frame keys it has (MountLooks.FrameKeys names them; a
    // missing one shows the donkey's own); TryGetFrame gives the Sprite for a key, asked once per key when the skin is
    // first worn. The sprite is yours, from any texture (your own sheet, say): 50 pixels per unit, the pivot where
    // the donkey's anchor is (the centre of a 96x96 picture), point filtering; keep it, reuse it, destroy it when your
    // mod is done with it. The API only points its overlays at it.
    public interface IMountSkinSource
    {
        IEnumerable<string> Keys { get; }
        bool TryGetFrame(string key, out Sprite sprite);
    }

    // A donkey frame on its canvas, for a recipe: the pixels (Unity's order: the bottom row first), the size, and the
    // frame's bob (how far below the standing frame's its back sits; a pattern that should ride with the body uses
    // y + Bob instead of y). Helpers for the usual needs: the outline, one pixel inside it, luminance, a fixed noise,
    // a colour ramp.
    public sealed class MountSkinFrame
    {
        private readonly Looks.SkinFrame f;

        internal MountSkinFrame(Looks.SkinFrame f)
        {
            this.f = f;
        }

        public string Key => f.Key;
        public int Width => f.W;
        public int Height => f.H;
        public Color32[] Pixels => f.P;
        public int Bob => f.Bob;

        public bool Opaque(int x, int y) => f.Opaque(x, y);
        public bool Rim(int x, int y) => f.Rim(x, y);
        public bool Inner(int x, int y) => f.Inner(x, y);
        public static float Luminance(Color32 c) => Looks.SkinFrame.Lum(c);
        public static float Noise(int x, int y, int salt) => Looks.SkinFrame.Noise(x, y, salt);
        public static Color32 Ramp(float t, params (float At, Color32 Colour)[] stops) => Looks.SkinFrame.Ramp(t, stops);
    }

    public sealed class MountSkinInfo
    {
        internal static readonly MountSkinInfo Donkey = new MountSkinInfo();

        private MountSkinInfo()
        {
            Id = MountLooks.DonkeyId;
            Name = "Donkey";
            Author = "the game";
            Version = "";
            Owner = "";
            IsBuiltIn = true;
            OwnColours = false;
            Mount = MountLooks.DonkeyId;
            CanvasWidth = CanvasHeight = 96;
            SeatRow = -1;
            Regions = new List<string>(Colors.MountRegionKeys);
        }

        internal MountSkinInfo(Looks.SkinDef d)
        {
            Id = d.Id;
            Name = d.Name ?? d.Id;
            Author = d.Author ?? "";
            Version = d.Version ?? "";
            Owner = d.Owner ?? "";
            IsBuiltIn = d.BuiltIn;
            OwnColours = d.OwnColours;
            Over = d.Over;
            Mount = d.Mount.Id;
            CanvasWidth = d.CanvasW;
            CanvasHeight = d.CanvasH;
            Source = d.Source;
            IsRecipe = d.Recipe != null;
            SeatRow = d.SeatRow;
            SeatFromPicture = d.SeatFromPicture;
            FromGame = d.Game != null;
            Description = d.Description ?? "";
            HasPortrait = d.PortraitTexture != null || !string.IsNullOrEmpty(d.Portrait) || (d.Parent != null && !d.Has(Looks.SkinGiven.Portrait) && (d.Parent.PortraitTexture != null || !string.IsNullOrEmpty(d.Parent.Portrait)));
            BuiltOn = d.Parent?.Id;
            OverDefault = d.OverDefault;
            IsAvailable = d.Unavailable == null;
            SetAsideWhy = d.Unavailable;
            var regions = new List<string>();
            if (!d.OwnColours) regions.AddRange(Colors.MountRegionKeys);
            else
            {
                regions.Add("Coat");
                if (d.ColorRegions != null) foreach (string r in d.ColorRegions.Keys) if (!regions.Contains(r)) regions.Add(r);
            }
            Regions = regions;
            HasFrontLayer = d.FrontSprites != null || d.LoadFrontSheetPng != null || (d.FileNames != null && d.FileNames.Exists(f => f.StartsWith(Looks.FrontKey, StringComparison.Ordinal)));
        }

        public string Id { get; }
        // False while the skin is set aside (a game-source skin whose frames the game lacks): not offered, the donkey
        // comes for whoever has it chosen; SetAsideWhy says why, null when available.
        public bool IsAvailable { get; }
        public string SetAsideWhy { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        // For the Mounts panel: a line or two about the look; whether it brings a portrait.
        public string Description { get; }
        public bool HasPortrait { get; }
        // The plugin GUID that registered it (packs found by the API: the API's).
        public string Owner { get; }
        public bool IsBuiltIn { get; }
        // Drawn from the donkey's own frames at runtime.
        public bool IsRecipe { get; }
        // Drawn in its own colours (the player's colours reach its Regions: Coat and what its [Colors] names), or in the
        // donkey's palette colours (the donkey's regions apply as they are).
        public bool OwnColours { get; }
        // Drawn over the donkey (a saddle, a hat) instead of in its place.
        public bool Over { get; }
        // The mount this skin is a look of.
        public string Mount { get; }
        public int CanvasWidth { get; }
        public int CanvasHeight { get; }
        // A pack's folder on disk, or null.
        public string Source { get; }
        // Where the rider sits on this skin (docs/seat-investigation.md): the canvas row (from the top) its hip is
        // on, or -1 when the skin declares none (the player's own seat keys apply); SeatFromPicture: on this
        // frame's picture top instead (Gerry).
        public int SeatRow { get; }
        public bool SeatFromPicture { get; }
        // Pictures drawn in front of the rider (a near wing).
        public bool HasFrontLayer { get; }
        // The colour regions the player's colours reach on this skin (Coat always on a skin in its own colours, then
        // what its [Colors] names; every region on the donkey's palette).
        public IReadOnlyList<string> Regions { get; }
        // Its frames are another game character's, read at run time (source = game).
        public bool FromGame { get; }
        // The skin it is built on: its mount's own look ("<mount>/default") for every other skin of a mount but the
        // donkey, whose pictures and settings it doesn't give come from there; null for a mount's own look, a donkey skin,
        // or one whose mount has no default registered (it stands alone).
        public string BuiltOn { get; }
        // Drawn over that default (layer = over on a skin of a mount other than the donkey): the default's pictures with
        // the skin's on top, holes where its erase colour is. Over stays false for it (Over is "over the game's donkey").
        public bool OverDefault { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
