using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // Named colours a player picks from (MountSettingsPage.Color): real colours with names anyone understands, instead
    // of hex values or a hue number.
    public enum PaletteColor
    {
        Chestnut, Sand, Cream, Snow, Ash, Slate, Charcoal, Black,
        Rust, Brick, Wine, Coral, Rose, Plum, Lavender, Navy,
        Sky, Teal, Mint, Moss, Forest, Olive, Mustard, Gold,
    }

    public static class MountPalette
    {
        private static readonly string[] Hexes =
        {
            "8B4A2B", "C8A26B", "E6D8B4", "E8E6E0", "8E8B85", "5A6670", "3A3A3A", "1E1E1E",
            "A0461E", "9C4A3A", "7A2232", "D9765F", "C77B8F", "6B3A5E", "9A86C2", "2B3F6B",
            "6FA8DC", "2F7F7A", "8FC9A3", "5E6B34", "2F5233", "7A7A3A", "C4A23A", "C99A2E",
        };

        // The colour as hex RRGGBB.
        public static string Hex(PaletteColor c) => Hexes[Mathf.Clamp((int)c, 0, Hexes.Length - 1)];

        // The colour made lighter or darker: lightness 100 is the colour itself, 50 half as light, 150 half again.
        public static string Hex(PaletteColor c, int lightness)
        {
            ColorUtility.TryParseHtmlString("#" + Hex(c), out Color col);
            Color.RGBToHSV(col, out float h, out float s, out float v);
            float k = Mathf.Max(0f, lightness) / 100f;
            float nv = v * k;
            float ns = s;
            if (nv > 1f)
            {
                ns = s / nv; // past full brightness the colour turns paler instead
                nv = 1f;
            }
            return ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, Mathf.Clamp01(ns), Mathf.Clamp01(nv)));
        }

        // A block of the colour as TextMeshPro rich text (the game's labels show it): for a swatch row.
        public static string Swatch(string hex)
        {
            return string.IsNullOrEmpty(hex) ? "" : "<mark=#" + hex.TrimStart('#') + "FF>          </mark>  #" + hex.TrimStart('#');
        }
    }
}
