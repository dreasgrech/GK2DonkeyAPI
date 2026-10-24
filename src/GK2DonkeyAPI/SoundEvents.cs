using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // What plays for one sound event of one instance, once the layers are merged (docs/mount-sounds-design.md section
    // 4): the source (a game sound id, the bank's id for files of a pack, by the ground for steps, or silent), how
    // (volume, pitch, a random pitch change, a start offset, a loop, the speech group) and the event's own numbers
    // (footfalls per cycle, the idle interval, ...). Layer says which layer gave the source: base (the player's cfg
    // keys), mount, skin or mod.
    internal sealed class SoundSpec
    {
        internal string GameId;        // a game sound id ("voice_donkey", "donkey_lips#2"); null: no game id
        internal string BankId;        // the sound bank's id (a pack's files registered as a game sound, SoundBank)
        internal bool BySurface;       // steps: the game's step sound for the ground under the mount
        internal bool Silent;          // "none": nothing plays, whatever the layer below says
        internal bool On = true;       // the player's switch for this event (HoofOn, GrazeOn, ...)
        internal float Volume = 1f;    // x the player's SoundVolume
        internal float Pitch = 1f;
        internal float PitchVariation; // +/- per play, as a fraction of the pitch
        internal float Start;          // seconds into the clip
        internal bool Loop;            // a stoppable event's clip looped until Stop
        internal bool Speech;          // the dialogue voices' mixer group instead of the in-world sfx one
        internal bool Loading;         // the layer's files are still being decoded: nothing plays yet
        internal Dictionary<string, float> Numbers; // the event's own numbers (SoundEvents.Def.Numbers)
        internal string Layer = "base";

        internal bool HasSource => Silent || BySurface || GameId != null || BankId != null;

        internal float Number(string key, float fallback)
        {
            return Numbers != null && Numbers.TryGetValue(key, out float v) ? v : fallback;
        }

        internal SoundSpec Set(string key, float value)
        {
            (Numbers ?? (Numbers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[key] = value;
            return this;
        }

        // What the dev tab shows: the source, the volume and pitch, the layer.
        internal string Describe()
        {
            string src = Silent ? "none" : BankId != null ? BankId + (Loading ? " (loading)" : "") : GameId ?? (BySurface ? "by the ground" : "nothing");
            return $"{src}  x{Volume.ToString("0.00", CultureInfo.InvariantCulture)} vol, {Pitch.ToString("0.00", CultureInfo.InvariantCulture)} pitch{(PitchVariation > 0f ? " +/-" + PitchVariation.ToString("0.00", CultureInfo.InvariantCulture) : "")}{(Start > 0f ? " from " + Start.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "")}{(Loop ? ", loop" : "")}{(Speech ? ", speech" : "")}{(On ? "" : ", OFF")}  [{Layer}]";
        }
    }

    // The catalogue of sound events (docs/mount-sounds-design.md section 3): one row per event id, the single place
    // an event is defined. Base builds the event's spec from the player's cfg keys of a style (the base layer: the
    // donkey's sounds as shipped and as the player tuned them); null: the event has no sound unless a mount or skin
    // gives one. The ids are what packs ([Sounds] voice = ...) and mods (Mounts.Sounds) name.
    internal static class SoundEvents
    {
        internal sealed class Def
        {
            internal string Id;
            internal string Description;
            internal bool Stoppable;       // plays until Stop (the graze cut when the clip ends early; a loop)
            internal bool StandingGated;   // an unridden instance plays it only with StandingSounds on (the animator's events, the voice)
            internal string[] Variants;    // sub-keys a layer may give ("step.grass"): the surfaces for step
            internal string[] Numbers;     // the event's own numbers a layer may set ("voice.idle_every")
            internal Func<StyleSettings, SoundSpec> Base;
        }

        internal static readonly string[] Surfaces = { "grass", "dirt", "wood", "stone" };
        private static readonly string[] None = Array.Empty<string>();

        internal static readonly Def[] All =
        {
            new Def
            {
                Id = "step", Description = "each footfall of the walk cycle (the game's step sound for the ground under the mount unless a layer says otherwise)",
                StandingGated = true, Variants = Surfaces, Numbers = new[] { "per_cycle", "phase", "surface_aware" },
                Base = s => new SoundSpec
                {
                    GameId = string.IsNullOrEmpty(s.HoofSoundId.Value?.Trim()) ? null : s.HoofSoundId.Value.Trim(),
                    BySurface = string.IsNullOrEmpty(s.HoofSoundId.Value?.Trim()),
                    On = s.HoofOn.Value, Volume = s.HoofVolume.Value, Pitch = s.HoofPitch.Value, PitchVariation = s.HoofPitchVariation.Value,
                }.Set("per_cycle", s.HoofStepsPerCycle.Value).Set("phase", s.HoofPhase.Value).Set("surface_aware", s.HoofSurfaceAware.Value ? 1f : 0f),
            },
            new Def
            {
                Id = "wheel", Description = "once per walk cycle while the cart moves (every_cycles, phase; follows_speed: the pitch rises with the walking speed)",
                StandingGated = true, Variants = None, Numbers = new[] { "every_cycles", "phase", "follows_speed" },
                Base = s => new SoundSpec { GameId = s.WheelSoundId.Value, On = s.WheelOn.Value, Volume = s.WheelVolume.Value, Pitch = s.WheelPitch.Value }
                    .Set("every_cycles", s.WheelEveryCycles.Value).Set("phase", s.WheelPhase.Value).Set("follows_speed", s.WheelPitchFollowsSpeed.Value ? 1f : 0f),
            },
            new Def { Id = "setoff", Description = "walking starts after standing", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "stop", Description = "walking ends", StandingGated = true, Variants = None, Numbers = None },
            new Def
            {
                Id = "graze", Description = "the graze clip starts (cut when it ends early)", Stoppable = true, StandingGated = true, Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.GrazeSoundId.Value, On = s.GrazeOn.Value, Volume = s.GrazeVolume.Value },
            },
            new Def
            {
                Id = "lips", Description = "the game's lip-smack animation event", StandingGated = true, Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = "donkey_lips", Volume = s.LipsVolume.Value },
            },
            new Def { Id = "ear", Description = "the ear clip starts", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "turn", Description = "the turning-head clip starts", StandingGated = true, Variants = None, Numbers = None },
            new Def
            {
                Id = "voice", Description = "the creature's call: the idle timer (idle_every seconds, 0 = never; while_walking), a chance at mounting (mount_chance), Mounts.Bray()",
                StandingGated = true, Variants = None, Numbers = new[] { "idle_every", "mount_chance", "while_walking" },
                Base = s => new SoundSpec { GameId = s.BraySoundId.Value, Volume = s.BrayVolume.Value, Pitch = s.BrayPitch.Value, Speech = !s.BrayInSfxChannel.Value }
                    .Set("idle_every", s.BrayIdleEverySeconds.Value).Set("mount_chance", s.BrayOnMountChance.Value).Set("while_walking", s.BrayWhileWalking.Value ? 1f : 0f),
            },
            new Def { Id = "refused", Description = "a ride or a hitch refused at the mount", Variants = None, Numbers = None },
            new Def
            {
                Id = "mount", Description = "the keeper gets on", Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.MountSoundId.Value, On = s.MountOn.Value, Volume = s.MountVolume.Value, Pitch = s.MountPitch.Value, Start = s.MountSoundStart.Value },
            },
            new Def
            {
                Id = "dismount", Description = "he gets off (not at doors, scene changes, failed re-summons, style changes)", Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.DismountSoundId.Value, On = s.DismountOn.Value, Volume = s.DismountVolume.Value, Pitch = s.DismountPitch.Value, Start = s.DismountSoundStart.Value },
            },
            new Def
            {
                Id = "hitch", Description = "a cart hitched", Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.HitchSoundId.Value, On = s.HitchOn.Value, Volume = s.HitchVolume.Value, Pitch = s.HitchPitch.Value },
            },
            new Def
            {
                Id = "unhitch", Description = "the cart left standing", Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.UnhitchSoundId.Value, On = s.UnhitchOn.Value, Volume = s.UnhitchVolume.Value, Pitch = s.UnhitchPitch.Value },
            },
            new Def
            {
                Id = "cargo", Description = "an item lands on the cart", Variants = None, Numbers = None,
                Base = s => new SoundSpec { GameId = s.CargoThumpSoundId.Value, On = s.CargoThumpOn.Value, Volume = s.CargoThumpVolume.Value, Pitch = s.CargoThumpPitch.Value },
            },
            new Def
            {
                Id = "bell", Description = "the bell: on setting off after standing (on_set_off, set_off_idle seconds, only_with_cargo), the bell key, Mounts.RingBell()",
                Variants = None, Numbers = new[] { "on_set_off", "set_off_idle", "only_with_cargo" },
                Base = s => new SoundSpec { GameId = s.BellSoundId.Value, Volume = s.BellVolume.Value, Pitch = s.BellPitch.Value }
                    .Set("on_set_off", s.BellOnSetOff.Value ? 1f : 0f).Set("set_off_idle", s.BellSetOffIdleSeconds.Value).Set("only_with_cargo", s.BellOnlyWithCargo.Value ? 1f : 0f),
            },
            new Def { Id = "sent", Description = "sent away", Variants = None, Numbers = None },
            new Def { Id = "called", Description = "called over", Variants = None, Numbers = None },
            // flying and jumping (docs/mount-sounds-design.md 3.2; the hooks in Flight.cs): silent unless a layer gives a sound
            new Def { Id = "takeoff", Description = "the take-off begins (the crouch; a jump turning into a take-off)", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "flap", Description = "each wing beat: the launch's kicks, then every beat airborne (not while gliding)", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "glide", Description = "the glide begins (stoppable: a loop until the way out)", Stoppable = true, StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "fly", Description = "airborne (stoppable: a loop through the flight, stopped on the ground)", Stoppable = true, StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "land", Description = "the landing begins (the dive)", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "touchdown", Description = "the thud on touching the ground", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "landed", Description = "a flight is over, on the ground", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "refused_takeoff", Description = "a take-off refused (indoors, with the cart, a mount that doesn't fly)", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "jump", Description = "a jump leaves the ground", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "jumpland", Description = "a jump comes down (the squash)", StandingGated = true, Variants = None, Numbers = None },
            new Def { Id = "perch", Description = "a jump comes down on top of something", StandingGated = true, Variants = None, Numbers = None },
        };

        private static readonly Dictionary<string, Def> byId = Index();

        private static Dictionary<string, Def> Index()
        {
            var d = new Dictionary<string, Def>(StringComparer.OrdinalIgnoreCase);
            foreach (Def e in All) d[e.Id] = e;
            return d;
        }

        internal static Def Find(string id) => id != null && byId.TryGetValue(id, out Def d) ? d : null;

        // "step.grass" -> step + grass; "voice" -> voice + null. A second part that is no variant of the event is a
        // number's name or unknown: the caller decides.
        internal static Def Split(string key, out string variant)
        {
            variant = null;
            if (string.IsNullOrEmpty(key)) return null;
            int dot = key.IndexOf('.');
            if (dot < 0) return Find(key);
            Def d = Find(key.Substring(0, dot));
            if (d != null) variant = key.Substring(dot + 1);
            return d;
        }

        internal static bool IsVariant(Def d, string variant)
        {
            if (d == null || variant == null) return false;
            foreach (string v in d.Variants) if (string.Equals(v, variant, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
