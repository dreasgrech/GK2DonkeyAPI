using System;
using System.Collections.Generic;

namespace GK2DonkeyAPI.Api
{
    // A colour for one of a skin's own tones (MountLook.Tone): Game is the skin as drawn; every other preset is a colour
    // whose hue and saturation the tone takes, each pixel keeping its shading, as light against the tone's own art as
    // the colour itself is (so Gold reads gold on a dark tone and on a pale one); Custom uses MountTone.Custom.
    public enum ToneColor { Game, Red, Gold, Green, Blue, Purple, Pink, Brown, Black, White, Custom }

    // A part of a skin's drawing the player colours on its own, apart from the five mount regions (docs/mount-colours.md
    // section 7): a pack's skin.cfg [Tone.<id>], a code skin's MountSkinDef.Tones. Id: lower case letters, digits and
    // '-' (it is part of the settings' keys). Name: its row's label (null: the id, and on a variant of a mount's default
    // the default's tone of that id keeps its own name). Colours: the exact colours of the skin's pictures that
    // belong to it, hex RRGGBB; a colour is in one tone or one region only. They come as a list, not a params list (the
    // ninth code review): with params, a call that left out the name took the first colour for it, and compiled.
    public sealed class MountSkinTone
    {
        public MountSkinTone(string id, string name, IEnumerable<string> colours) : this(null, id, name, colours, !string.IsNullOrWhiteSpace(name))
        {
        }

        // A tone whose Game default follows a mount region (Follows; a pack's follows =): a part that was under that
        // region before it was a tone. A region that is none of the five throws here (the eleventh code review: the
        // region and the id swapped compiled, and the tone was only left out in the skin's report).
        public static MountSkinTone Following(string region, string id, string name, IEnumerable<string> colours)
        {
            string key = Colors.RegionKeyOf((region ?? "").Trim());
            if (key == null) throw new ArgumentException($"'{region}' is not a mount region ({string.Join(", ", Colors.MountRegionKeys)}): MountSkinTone.Following(region, id, name, colours)", nameof(region));
            return new MountSkinTone(key, id, name, colours, !string.IsNullOrWhiteSpace(name));
        }

        // A resolved skin's tone as MountSkinInfo lists it: its name as shown, nameGiven whether the skin gave it one (a
        // variant's tone showing its default's name, or one named by its id, gave none), so one fed back into a
        // MountSkinDef keeps its meaning.
        internal static MountSkinTone Listed(string follows, string id, string name, bool nameGiven, IEnumerable<string> colours) => new MountSkinTone(follows, id, name, colours, nameGiven);

        private MountSkinTone(string follows, string id, string name, IEnumerable<string> colours, bool nameGiven)
        {
            Id = SkinTones.NormalId(id); // as the loader keeps it (a [Tone.Cloth] is the tone cloth)
            bool named = !string.IsNullOrWhiteSpace(name);
            NameGiven = nameGiven && named;
            Name = named ? name.Trim() : Id;
            Follows = string.IsNullOrWhiteSpace(follows) ? null : follows.Trim();
            var list = new List<string>(); // a copy: the caller's list stays the caller's
            if (colours != null) list.AddRange(colours);
            Colours = list.AsReadOnly();
        }

        internal bool NameGiven { get; } // a name of its own (MountSkinDef.ToDef: a variant's tone without one keeps its default's)

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Colours { get; }
        // The mount region (Coat, Dark, Muzzle, Eye, Harness) the tone's Game default follows: for a part that was under
        // that region before it was a tone, so the player's saved colour of the region keeps reaching it (a pack's
        // follows =). Null: as drawn. Given with MountSkinTone.Following; the tones MountSkinInfo lists are shared, so read only.
        public string Follows { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }

    // One tone's colour in a skin's look (MountLook.Tone): kept per skin with the rest of the look; setting it applies it
    // at once when that skin is worn (or shown in the Mounts panel's picture). A tone the worn skin doesn't have
    // changes nothing.
    public sealed class MountTone
    {
        private readonly MountLook look;
        private ToneColor color;
        private string custom;

        internal MountTone(MountLook look, string id)
        {
            this.look = look;
            Id = id;
        }

        public string Id { get; }
        // A set to the value it has changes nothing (the rows set theirs at every bind: each Changed is the look applied again).
        public ToneColor Color { get => color; set { if (color == value) return; color = value; look.Changed(); } }
        // Hex RRGGBB, used while Color is Custom (only then does a new one apply the look: a hidden custom row's swatch
        // changes nothing on the mount).
        public string Custom { get => custom; set { if (custom == value) return; custom = value; if (color == ToneColor.Custom) look.Changed(); } }
    }
}
