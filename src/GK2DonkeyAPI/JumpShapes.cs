using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Each mount's own jump (the user, 2026-10-25: the donkey's arc doesn't suit every mount, so each may say its
    // own; docs/flying.md "Each mount its own jump").
    // The jump's shape (its height, its time in the air, how it rises and falls, the crouch and the squash) is the
    // mount's: mount.cfg [Jump], MountDef.Jump. A key the mount doesn't set is the default jump's (Flight's values, the
    // dev Flying tab's Jump rows; the donkey's). What a jump does (over things, the perch, the steering) stays one for
    // every mount.
    internal static class JumpShapes
    {
        // one key of [Jump]: its name in the file, its range, what it is (the Mounts tab's row and its tip)
        internal sealed class Key
        {
            internal string Name, Label, Tip;
            internal float Min, Max;
            internal bool Bool;                    // true / false in the file (kept as 1 / 0)
        }

        // in the order the file keeps them
        internal static readonly Key[] Keys =
        {
            new Key { Name = "height", Label = "Jump height, the key held (px)", Min = 1f, Max = 120f, Tip = "The top of a jump in art pixels, the key held (the default's 28)." },
            new Key { Name = "tap_height", Label = "Jump height, the key tapped (px)", Min = 1f, Max = 120f, Tip = "The top of a jump with the key let go at once: the rise brakes hard, down to this at the least (never over the held height)." },
            new Key { Name = "hold_min", Label = "A press counts as at least (seconds)", Min = 0f, Max = 0.3f, Tip = "A shorter press jumps as if held this long: an ordinary press is a jump, a flick a hop." },
            new Key { Name = "time", Label = "Time in the air, held (seconds)", Min = 0.15f, Max = 2f, Tip = "From leaving the ground to landing, the key held: the rise, the hang and the fall." },
            new Key { Name = "rise_share", Label = "The rise's share of that time", Min = 0.2f, Max = 0.8f, Tip = "0.5: as fast down as up (floaty). Above 0.5: a slower rise and a heavier, quicker fall (the platformer feel). Below: a quick spring and a slow sink." },
            new Key { Name = "hang", Label = "Held at the top (seconds)", Min = 0f, Max = 0.5f, Tip = "Still at the top before the fall (at most 0.3 of the time in the air)." },
            new Key { Name = "crouch", Label = "Crouch before (px)", Min = 0f, Max = 12f, Tip = "Down into the ground before leaving it." },
            new Key { Name = "crouch_time", Label = "Crouch time (seconds)", Min = 0f, Max = 0.3f, Tip = "How long the crouch takes: the press to leaving the ground." },
            new Key { Name = "squash", Label = "Squash on landing (px)", Min = 0f, Max = 12f, Tip = "Into the ground on landing, over 0.1 s." },
            new Key { Name = "fall_ease", Label = "The fall eased (wings braking)", Min = 0f, Max = 1f, Tip = "0: the fall speeds up to the ground (a stone's). 1: it starts slow, sinks and slows again to touch down softly (wings braking). Between: a blend." },
            new Key { Name = "wings", Label = "Wings: take-off and landing frames", Min = 0f, Max = 1f, Bool = true, Tip = "On: the jump plays the skin's flying frames, a small take-off and landing (the crouch, the push-off, a flap up, wings open coming down, reaching for the ground, the touch, the fold); a skin without them walks in the air as before. Off: the legs' own frames." },
        };

        internal static Key Find(string name)
        {
            foreach (Key k in Keys) if (k.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return k;
            return null;
        }

        // The default jump's value of a key: Flight's (the dev Flying tab moves them for the session).
        internal static float Default(string name)
        {
            switch (name)
            {
                case "height": return Flight.JumpPx;
                case "tap_height": return Flight.JumpTapPx;
                case "hold_min": return Flight.JumpHoldMinS;
                case "time": return Flight.JumpTimeS;
                case "rise_share": return Flight.JumpRiseShare;
                case "hang": return Flight.JumpHangS;
                case "crouch": return Flight.JumpCrouchPx;
                case "crouch_time": return Flight.JumpCrouchS;
                case "squash": return Flight.JumpSquashPx;
                case "fall_ease": return Flight.JumpFallEase;
                case "wings": return Flight.JumpWings ? 1f : 0f;
            }
            return 0f;
        }

        // The jump a mount makes: its own keys over the default's. From: "the default jump" or "<mount>'s own (keys)".
        internal static Flight.JumpShape Of(Catalog.Entry k)
        {
            var s = new Flight.JumpShape
            {
                Height = Flight.JumpPx, Tap = Flight.JumpTapPx, HoldMin = Flight.JumpHoldMinS, Time = Flight.JumpTimeS, RiseShare = Flight.JumpRiseShare,
                Hang = Flight.JumpHangS, Crouch = Flight.JumpCrouchPx, CrouchTime = Flight.JumpCrouchS, Squash = Flight.JumpSquashPx,
                FallEase = Flight.JumpFallEase, Wings = Flight.JumpWings, From = "the default jump",
            };
            Dictionary<string, float> own = k?.Jump;
            if (own == null || own.Count == 0) return s;
            var set = new List<string>();
            foreach (Key key in Keys)
            {
                if (!own.TryGetValue(key.Name, out float v)) continue;
                set.Add(key.Name + " " + Text(key, v));
                switch (key.Name)
                {
                    case "height": s.Height = v; break;
                    case "tap_height": s.Tap = v; break;
                    case "hold_min": s.HoldMin = v; break;
                    case "time": s.Time = v; break;
                    case "rise_share": s.RiseShare = v; break;
                    case "hang": s.Hang = v; break;
                    case "crouch": s.Crouch = v; break;
                    case "crouch_time": s.CrouchTime = v; break;
                    case "squash": s.Squash = v; break;
                    case "fall_ease": s.FallEase = v; break;
                    case "wings": s.Wings = v > 0.5f; break;
                }
            }
            s.From = k.Label + "'s own (" + string.Join(", ", set) + "; the rest the default's)";
            return s;
        }

        // A pack's [Jump] line: kept (clamped to its range) or said in the report.
        internal static void Parse(ref Dictionary<string, float> into, string name, string text, ref string report)
        {
            Key key = Find(name);
            if (key == null)
            {
                report += "[Jump] " + name + " is not a jump key (" + KeyNames() + "), ignored; ";
                return;
            }
            if (key.Bool)
            {
                if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase))
                    (into ?? (into = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[key.Name] = text.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
                else report += "[Jump] " + key.Name + " = " + text + " is not true or false, the default's applies; ";
                return;
            }
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) || float.IsNaN(v) || float.IsInfinity(v))
            {
                report += "[Jump] " + key.Name + " = " + text + " is not a number (" + Num(key.Min) + " to " + Num(key.Max) + "), the default's applies; ";
                return;
            }
            float c = Mathf.Clamp(v, key.Min, key.Max);
            if (c != v) report += "[Jump] " + key.Name + " = " + text + " is outside " + Num(key.Min) + " to " + Num(key.Max) + ", taken as " + Num(c) + "; ";
            (into ?? (into = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[key.Name] = c;
        }

        // A code mount's MountDef.Jump as the catalogue keeps it (clamped; null: none set).
        internal static Dictionary<string, float> FromApi(MountJump j, string mountId)
        {
            if (j == null) return null;
            Dictionary<string, float> d = null;
            void Put(string name, float? v)
            {
                if (!v.HasValue) return;
                Key key = Find(name);
                float c = Mathf.Clamp(v.Value, key.Min, key.Max);
                if (float.IsNaN(v.Value) || c != v.Value) Plugin.Log.LogWarning($"Catalog: mount '{mountId}': its jump's {name} = {v.Value} is outside {Num(key.Min)} to {Num(key.Max)}, taken as {Num(float.IsNaN(c) ? key.Min : c)}.");
                (d ?? (d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[name] = float.IsNaN(c) ? key.Min : c;
            }
            Put("height", j.Height);
            Put("tap_height", j.TapHeight);
            Put("hold_min", j.HoldMin);
            Put("time", j.Time);
            Put("rise_share", j.RiseShare);
            Put("hang", j.Hang);
            Put("crouch", j.Crouch);
            Put("crouch_time", j.CrouchTime);
            Put("squash", j.Squash);
            Put("fall_ease", j.FallEase);
            if (j.Wings.HasValue) (d ?? (d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))["wings"] = j.Wings.Value ? 1f : 0f;
            return d;
        }

        // The jump a mount makes, every value filled (MountInfo.Jump).
        internal static MountJump ToApi(Catalog.Entry k)
        {
            Flight.JumpShape s = Of(k);
            return new MountJump { Height = s.Height, TapHeight = s.Tap, HoldMin = s.HoldMin, Time = s.Time, RiseShare = s.RiseShare, Hang = s.Hang, Crouch = s.Crouch, CrouchTime = s.CrouchTime, Squash = s.Squash, FallEase = s.FallEase, Wings = s.Wings };
        }

        internal static string KeyNames()
        {
            var names = new List<string>();
            foreach (Key k in Keys) names.Add(k.Name);
            return string.Join(", ", names);
        }

        // a value as the file writes it
        internal static string Text(Key key, float v) => key.Bool ? (v > 0.5f ? "true" : "false") : Num(v);

        internal static string Num(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
