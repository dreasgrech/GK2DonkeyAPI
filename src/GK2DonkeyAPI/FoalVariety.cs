using System;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Each baby its own (the user, 2026-10-26: "they all look alike and all have the same size, same colors ... Persisted
    // randomness though so that they wont constantly change"; docs/foals.md "Babies of their own"). Everything comes from
    // one seed: the family's salt (a number kept on the parent, made once), the mount it is a baby of and its place
    // among its siblings. So a baby is the same after every load, the dragon's babies are not the donkey's, and the
    // donkey's come back the same after a while on the dragon; nothing of it is saved but the salt.
    //   Size     its mount's baby size, a bit up or down (made at that size: never changes in view)
    //   Colours  its own shade of the parent's: hue, brightness, saturation (MountCompanion.ApplyLook)
    //   Temper   one "energy" (lively and roaming, or calm and staying close) and its own trot, walk rate and voice
    // Strength: ShuffleFoalVariety (1: the user's "noticeable": size ±12 %, brightness ±15 %, hue ±10°; 0: alike).
    internal struct FoalLooks
    {
        internal float Size;        // times the mount's baby size
        internal float Hue;         // degrees
        internal float Brightness;  // times
        internal float Saturation;  // times
        internal float MuzzleHue, MuzzleBrightness;
        internal float Tinge;       // 0..1: on a near-grey coat (the Black preset), how much of the art's own colour comes back
        internal Mount.FoalTemper Temper;

        internal static readonly FoalLooks Alike = new FoalLooks
        {
            Size = 1f, Brightness = 1f, Saturation = 1f, MuzzleBrightness = 1f, Temper = Mount.FoalTemper.Plain,
        };

        // k: the strength (ShuffleFoalVariety).
        internal static FoalLooks For(string salt, string mountId, int index, float k)
        {
            if (k <= 0.001f)
            {
                return Alike;
            }
            var r = new System.Random(Seed(salt, mountId, index));
            float U() => (float)(r.NextDouble() * 2.0 - 1.0); // -1..1, even
            // Mostly near the middle, now and then towards an end (two draws averaged): a family, not a random crowd.
            float Mid() => (U() + U()) * 0.5f;
            var v = new FoalLooks
            {
                // evenly over the whole ±12 %: two averaged kept most of them at the mount's own size (seen 2026-10-26:
                // five dragon babies at 0.57 to 0.66, alike to the eye). Its own draw, without the mount: a size is made
                // at spawn, and with the mount in its seed every mount switch remade every baby (30 in one self-test
                // run, and the player's own babies came out of it new); the same factor on every mount keeps them
                // through a switch between mounts of one baby size, as before.
                Size = 1f + 0.12f * k * (float)(new System.Random(Seed(salt, "size", index)).NextDouble() * 2.0 - 1.0),
                Hue = 10f * k * Mid() * 1.4f,
                Brightness = 1f + 0.15f * k * Mid() * 1.4f,
                Saturation = 1f + 0.15f * k * Mid() * 1.4f,
                MuzzleHue = 10f * k * Mid(),
                MuzzleBrightness = 1f + 0.12f * k * Mid(),
                Tinge = Mathf.Clamp01((U() + 1f) * 0.5f * Mathf.Min(1f, k)),
            };
            float energy = Mathf.Clamp(U() * Mathf.Min(1f, k), -1f, 1f); // its own temperament, the whole range
            v.Temper = new Mount.FoalTemper
            {
                Speed = 1f + 0.25f * energy,
                Pause = 1f - 0.4f * energy,
                Hop = 1f + 0.7f * energy,
                Chain = 1f + 0.5f * energy,
                Area = 1f + 0.3f * energy,
                Trot = 1f + 0.1f * k * U(),
                Walk = 1f + 0.08f * k * U(),
                Pitch = 1f + 0.07f * k * U(),
                Energy = energy,
            };
            v.Size = Mathf.Clamp(v.Size, 0.7f, 1.3f);
            v.Brightness = Mathf.Clamp(v.Brightness, 0.6f, 1.4f);
            v.Saturation = Mathf.Clamp(v.Saturation, 0.5f, 1.5f);
            return v;
        }

        // FNV-1a over the three: the same number on every run (string.GetHashCode is not promised to be).
        internal static int Seed(string salt, string mountId, int index)
        {
            unchecked
            {
                uint h = 2166136261;
                void Add(string s)
                {
                    if (s == null) return;
                    foreach (char c in s)
                    {
                        h = (h ^ c) * 16777619;
                    }
                    h = (h ^ 0x7C) * 16777619; // a separator: "ab"+"c" is not "a"+"bc"
                }
                Add(salt);
                Add(mountId != null ? mountId.ToLowerInvariant() : null);
                Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return (int)h;
            }
        }

        // A colour "RRGGBB" with its hue turned and its saturation scaled (a colorize colour: its hue key would be
        // painted over by it).
        internal static string ShiftHex(string hex, float hueDeg, float sat)
        {
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c))
            {
                return hex;
            }
            Color.RGBToHSV(c, out float h, out float s, out float val);
            h = Mathf.Repeat(h + hueDeg / 360f, 1f);
            s = Mathf.Clamp01(s * sat);
            return ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, s, val));
        }
    }

    internal static partial class Mount
    {
        // A baby's temperament, times the cfg's values where they are read (all 1: as before). Not saved: set again from
        // the seed (FoalLooks) whenever its family looks at it.
        internal struct FoalTemper
        {
            internal float Speed;   // its steps' walk and quick trots
            internal float Pause;   // between steps (below 1: restless)
            internal float Hop;     // how often it hops
            internal float Chain;   // steps one after another
            internal float Area;    // how far it wanders from its spot (below 1: stays close)
            internal float Trot;    // following
            internal float Walk;    // the walk cycle's rate
            internal float Pitch;   // its voice
            internal float Energy;  // -1 calm .. 1 lively, for the log and the panel

            internal static readonly FoalTemper Plain = new FoalTemper
            {
                Speed = 1f, Pause = 1f, Hop = 1f, Chain = 1f, Area = 1f, Trot = 1f, Walk = 1f, Pitch = 1f,
            };

            internal bool IsPlain => Speed == 1f && Pause == 1f && Hop == 1f && Chain == 1f && Area == 1f && Trot == 1f && Walk == 1f && Pitch == 1f;
        }

        internal sealed partial class Instance
        {
            internal FoalTemper Temper = FoalTemper.Plain;
        }
    }
}
