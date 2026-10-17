using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // The skins the donkey can wear (docs/skins-design.md; docs/skins.md for pack authors): the game's own look
    // ("donkey"), the built-in skins, every skin pack found on disk, and the skins mods register here. A donkey wears
    // one through its instance tuning key "Skin" (handle.Tuning.Set(owner, "Skin", info.Id); "donkey" or "" is the
    // game's own look), or through MountCompanion.Skin.
    //
    // Registering: a skin is a picture per donkey frame (FrameKeys names the 44 frames: "bdy_walk_left_03", ...), 96x96
    // facing left with the donkey's anchor in the middle, or a bigger canvas with the same centre. Four ways:
    //   - RegisterFolder: skin packs (gk2skins/<id>/skin.cfg and PNGs) under a folder of yours; packs anywhere under
    //     BepInEx/plugins are found by the API itself at start, so most mods need nothing here;
    //   - RegisterEmbedded: the same files embedded in your DLL (EmbeddedResource with logical names
    //     "<prefix><skin dir>\<file>", e.g. "MyMod.skins.rat\bdy_walk_left_03.png");
    //   - Register: pictures from your own code (IMountSkinSource gives a Texture2D per frame key; the textures stay
    //     yours, the API never destroys them);
    //   - RegisterRecipe: a rule over the donkey's own frames (a function from the frame's pixels to new pixels), so the
    //     result covers every clip and nothing of the game's art is shipped. Built a few frames a tick when first worn.
    // Every registration returns a handle: dispose it to withdraw the skin (the donkeys wearing it show the game's own
    // look from their next frame and keep their choice). Register in Awake: a skin registered after Saddle Up's settings
    // page is made works at once through the cfg and appears in its list after a restart.
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

        // A skin by id (case doesn't matter), or null. "donkey" and "" give the game's own look.
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
                    Plugin.Log.LogWarning("Looks: a SkinsChanged handler threw: " + e.Message);
                }
            }
        }

        // A skin from your own code. The id must be new (or yours already: then it replaces). Dispose to withdraw.
        public IDisposable Register(string ownerId, MountSkinDef def)
        {
            Owner(ownerId);
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.Source == null) throw new ArgumentException("A MountSkinDef needs a Source (IMountSkinSource).", nameof(def));
            Looks.SkinDef d = def.ToDef(ownerId);
            d.Sprites = def.Source; // the mod's sprites stay the mod's
            d.FrontSprites = def.FrontSource;
            Looks.Register(d);
            return new Handle(d.Id, ownerId);
        }

        // A rule over the donkey's own frames: pixels(frame) returns the new pixels (the same size as frame.Pixels, the
        // same layout). Built when first worn, a few frames a tick, and worn once complete.
        public IDisposable RegisterRecipe(string ownerId, string id, string name, Func<MountSkinFrame, Color32[]> pixels, string author = null, string version = null, bool foals = true)
        {
            Owner(ownerId);
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            var d = new Looks.SkinDef
            {
                Id = id, Name = name, Author = author, Version = version, Owner = ownerId, OwnColours = true, Foals = foals,
                Recipe = f => pixels(new MountSkinFrame(f)),
                Report = "a recipe: drawn from the donkey's own frames when first worn",
            };
            Looks.Register(d);
            return new Handle(d.Id, ownerId);
        }

        // Every skin pack under a folder: <folder>/gk2skins/<id>/ and <folder>/*/gk2skins/<id>/. Returns how many.
        public int RegisterFolder(string ownerId, string folder)
        {
            Owner(ownerId);
            return Looks.RegisterFolder(folder, ownerId);
        }

        // Every skin embedded in an assembly under a resource-name prefix. Returns how many.
        public int RegisterEmbedded(string ownerId, Assembly assembly, string prefix)
        {
            Owner(ownerId);
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A resource-name prefix is required.", nameof(prefix));
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

        private sealed class Handle : IDisposable
        {
            private readonly string id, owner;
            private bool done;

            internal Handle(string id, string owner)
            {
                this.id = id;
                this.owner = owner;
            }

            public void Dispose()
            {
                if (done) return;
                done = true;
                Looks.Unregister(id, owner);
            }
        }
    }

    // What a skin registered from code says about itself (the same as a pack's skin.cfg).
    public sealed class MountSkinDef
    {
        public string Id;
        public string Name;
        public string Author;
        public string Version;
        // Drawn in the donkey's exact palette colours, so the player's coat and harness colours recolour it.
        public bool PaletteColours;
        // Drawn over the donkey (a saddle, a hat) instead of in its place.
        public bool Over;
        // Foals may wear it; null: as the kind says.
        public bool? Foals;
        // The mount kind this skin is a look of (Mounts.Kinds; "donkey" by default: a differently drawn donkey).
        public string Mount = MountKinds.DonkeyId;
        // For information (the sprites carry their own size and pivot): the pictures' size and where the donkey's
        // anchor sits in them, from the bottom left.
        public int CanvasWidth = 96, CanvasHeight = 96;
        public float PivotX = 48f, PivotY = 48f;
        // The pictures moved on the donkey, in art pixels (x towards the head, y up): riding, and with the cart hitched
        // (a creature that should stand closer to the shafts). Null: the same as riding.
        public Vector2 Offset;
        public Vector2? CartOffset;
        // The skin's own values for existing tuning keys (SeatAdjustPx, RiderOffsetPx, LegAdjustYPx, TorsoOffsetPx, ...),
        // typed as the key is: set on the wearer as overrides owned by the skin while it is worn (a key another mod has
        // set on that donkey is left to it), cleared when it comes off, never saved. Null: none.
        public Dictionary<string, object> Tuning;
        // The pictures, by frame key.
        public IMountSkinSource Source;
        // The front layer, optional: pictures drawn in front of the rider (the near wing, a saddle's near side), by the
        // same frame keys; a missing key draws nothing there.
        public IMountSkinSource FrontSource;

        internal Looks.SkinDef ToDef(string owner)
        {
            return new Looks.SkinDef
            {
                Id = Id, Name = string.IsNullOrEmpty(Name) ? Id : Name, Author = Author, Version = Version, Owner = owner,
                OwnColours = !PaletteColours, Over = Over, Foals = Foals, CanvasW = CanvasWidth, CanvasH = CanvasHeight, PivotX = PivotX, PivotY = PivotY,
                KindId = string.IsNullOrWhiteSpace(Mount) ? MountKinds.DonkeyId : Mount.Trim(),
                OffsetRide = Offset, OffsetCart = CartOffset ?? Offset,
                TuningValues = Tuning != null && Tuning.Count > 0 ? new Dictionary<string, object>(Tuning, StringComparer.OrdinalIgnoreCase) : null,
                Report = "from code" + (Tuning != null && Tuning.Count > 0 ? ", " + Tuning.Count + " tuning key(s)" : ""),
            };
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
            Foals = true;
            CanvasWidth = CanvasHeight = 96;
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
            Foals = d.FoalsEffective;
            Mount = d.Kind.Id;
            CanvasWidth = d.CanvasW;
            CanvasHeight = d.CanvasH;
            Source = d.Source;
            IsRecipe = d.Recipe != null;
        }

        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        // The plugin GUID that registered it (packs found by the API: the API's).
        public string Owner { get; }
        public bool IsBuiltIn { get; }
        // Drawn from the donkey's own frames at runtime.
        public bool IsRecipe { get; }
        // Drawn in its own colours (the coat and harness colours don't apply), or in the donkey's palette colours.
        public bool OwnColours { get; }
        // Drawn over the donkey (a saddle, a hat) instead of in its place.
        public bool Over { get; }
        // Foals may wear it.
        public bool Foals { get; }
        // The mount kind this skin is a look of.
        public string Mount { get; }
        public int CanvasWidth { get; }
        public int CanvasHeight { get; }
        // A pack's folder on disk, or null.
        public string Source { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
