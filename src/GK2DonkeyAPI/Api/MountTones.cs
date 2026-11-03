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
    // '-' (it is part of the settings' keys). Name: its row's label. Colours: the exact colours of the skin's pictures
    // that belong to it, hex RRGGBB; a colour is in one tone or one region only.
    public sealed class MountSkinTone
    {
        public MountSkinTone(string id, string name, params string[] colours)
        {
            Id = id ?? "";
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            Colours = Array.AsReadOnly(colours ?? Array.Empty<string>());
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Colours { get; }

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
        public ToneColor Color { get => color; set { color = value; look.Changed(); } }
        // Hex RRGGBB, used while Color is Custom.
        public string Custom { get => custom; set { custom = value; look.Changed(); } }
    }
}
