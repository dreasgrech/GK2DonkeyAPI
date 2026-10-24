using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // One layer's sound for an event of the catalogue (docs/mount-sounds-design.md): what a skin, a mount or a mod says
    // plays for "voice", "step.grass", "mount", ... A null field is "not said": the layer below keeps its value. The
    // source is one of GameId (a game sound id, "id#n" for clip n), Files (a pack's sounds folder; a code skin has no
    // folder, so it gives Clips), Clips (your own AudioClips, registered as a game sound by the API and never destroyed
    // by it), Silent (nothing plays whatever the layer below says) or ByGround (steps: the game's step sound for the
    // ground under the mount). Volume and Pitch multiply the player's; PitchVariation is +/- per play; Start is seconds
    // into the clip; Loop keeps a stoppable event's clip going until Stop; Speech plays in the dialogue voices' mixer
    // group; Numbers are the event's own (MountSoundEvent.Numbers: "idle_every", "per_cycle", ...).
    public sealed class MountSound
    {
        public string GameId;
        public string[] Files;
        public AudioClip[] Clips;
        public bool Silent;
        public bool ByGround;
        public float? Volume, Pitch, PitchVariation, Start;
        public bool? Loop, Speech;
        public Dictionary<string, float> Numbers;

        public static MountSound Game(string id) => new MountSound { GameId = id };
        public static MountSound Own(params AudioClip[] clips) => new MountSound { Clips = clips };
        public static MountSound None => new MountSound { Silent = true };
        public static MountSound Ground => new MountSound { ByGround = true };

        internal SoundLine ToLine(string key, string bankKey, string owner)
        {
            var l = new SoundLine
            {
                Key = key, BankKey = bankKey, Owner = owner,
                Source = Silent ? "none" : ByGround ? "ground" : !string.IsNullOrWhiteSpace(GameId) ? GameId.Trim() : null,
                Files = Files != null && Files.Length > 0 ? (string[])Files.Clone() : null,
                Clips = Clips != null && Clips.Length > 0 ? (AudioClip[])Clips.Clone() : null,
                Volume = Volume, Pitch = Pitch, PitchVariation = PitchVariation, Start = Start, Loop = Loop, Speech = Speech,
                Numbers = Numbers != null && Numbers.Count > 0 ? new Dictionary<string, float>(Numbers, StringComparer.OrdinalIgnoreCase) : null,
            };
            if (l.Files != null || l.Clips != null) l.Source = null;
            return l;
        }

        internal static MountSound FromSpec(SoundSpec s)
        {
            if (s == null) return null;
            return new MountSound
            {
                GameId = s.BankId ?? s.GameId, Silent = s.Silent, ByGround = s.BySurface,
                Volume = s.Volume, Pitch = s.Pitch, PitchVariation = s.PitchVariation, Start = s.Start, Loop = s.Loop, Speech = s.Speech,
                Numbers = s.Numbers != null ? new Dictionary<string, float>(s.Numbers, StringComparer.OrdinalIgnoreCase) : null,
            };
        }

        public override string ToString()
        {
            string src = Silent ? "none" : ByGround ? "ground" : Clips != null ? Clips.Length + " clip(s)" : Files != null ? string.Join(", ", Files) : GameId ?? "(not said)";
            return src + (Volume.HasValue ? " x" + Volume.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "") + (Pitch.HasValue ? " p" + Pitch.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "");
        }
    }

    // A row of the sound catalogue: an event every mount may emit, with its variants ("grass", "dirt", "wood",
    // "stone" for step) and its own numbers.
    public sealed class MountSoundEvent
    {
        public string Id { get; }
        public string Description { get; }
        public bool Stoppable { get; }
        public IReadOnlyList<string> Variants { get; }
        public IReadOnlyList<string> Numbers { get; }

        internal MountSoundEvent(SoundEvents.Def d)
        {
            Id = d.Id;
            Description = d.Description;
            Stoppable = d.Stoppable;
            Variants = d.Variants;
            Numbers = d.Numbers;
        }

        public override string ToString() => Id;
    }

    // The mount sounds (docs/mount-sounds-design.md): every mount instance plays the events of one catalogue (steps,
    // the voice, grazing, getting on and off, the cart's sounds, ...) through the game's own player; what plays comes
    // from layers: a mod's override here, the skin's [Sounds] (MountSkinDef.Sounds), the mount's [Mount.Sounds]
    // (MountDef.Sounds), the player's own keys (Mounts.Tuning: the donkey's sounds as shipped and as tuned). The
    // player's switches and volumes scale whatever layer won.
    public sealed class MountSounds
    {
        internal MountSounds()
        {
        }

        private static IReadOnlyList<MountSoundEvent> catalogue;

        // The catalogue, in its order.
        public IReadOnlyList<MountSoundEvent> Catalogue
        {
            get
            {
                if (catalogue == null)
                {
                    var list = new List<MountSoundEvent>();
                    foreach (SoundEvents.Def d in SoundEvents.All) list.Add(new MountSoundEvent(d));
                    catalogue = list;
                }
                return catalogue;
            }
        }

        // The event ids.
        public IReadOnlyList<string> Events
        {
            get
            {
                var ids = new List<string>();
                foreach (SoundEvents.Def d in SoundEvents.All) ids.Add(d.Id);
                return ids;
            }
        }

        // Plays an event on this instance now, as the game would (its layers, the player's volumes, the ground under
        // it for a step). Refused with the reason when nothing played: no such event, the instance gone or without a
        // view, the sounds off, the event silent or its files still loading.
        public MountResult Play(MountHandle mount, string eventId, string variant = null)
        {
            if (mount == null || !mount.Exists) return MountResult.Refused("No such mount.");
            if (SoundEvents.Find(eventId) == null) return MountResult.Refused($"No sound event '{eventId}'.");
            GK2DonkeyAPI.Sounds.Rig rig = mount.I.SoundsRig;
            if (rig == null) return MountResult.Refused("The mount has no sounds.");
            LazyBearTechnology.SoundHandler h = rig.Emit(eventId, variant);
            return h != null ? MountResult.Success : MountResult.Refused("Nothing played: " + (string.IsNullOrEmpty(rig.LastEvent) ? "the sounds are off, or the mount has no view" : rig.LastEvent) + ".");
        }

        // Stops a stoppable event's sound on this instance (grazing, a loop).
        public void Stop(MountHandle mount, string eventId)
        {
            if (mount == null || !mount.Exists) return;
            mount.I.SoundsRig?.Stop(eventId);
        }

        // What plays for an event on this instance now, and which layer gave it: "mod", "skin", "mount", "base" (the
        // player's keys) or "none" (no layer gives the event a sound: null is returned). GameId holds the bank's id
        // for a layer's files or clips (a game sound id of the API's own).
        public MountSound Resolved(MountHandle mount, string eventId, out string layer, string variant = null)
        {
            layer = "none";
            if (mount == null || !mount.Exists || mount.I.SoundsRig == null) return null;
            SoundSpec s = mount.I.SoundsRig.Resolve(eventId, variant);
            if (s == null) return null;
            layer = s.Layer;
            return MountSound.FromSpec(s);
        }

        // Your sound for an event (or "event.variant") on this instance, over every other layer, for the session;
        // Dispose takes it back. Clips you give are registered as a game sound under your owner id and left to you.
        public IDisposable Set(string ownerId, MountHandle mount, string eventId, MountSound sound)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            if (mount == null || !mount.Exists) throw new ArgumentException("No such mount.", nameof(mount));
            if (sound == null) throw new ArgumentNullException(nameof(sound));
            SoundEvents.Def d = SoundEvents.Split(eventId, out string variant);
            if (d == null || variant != null && !SoundEvents.IsVariant(d, variant)) throw new ArgumentException($"No sound event '{eventId}'.", nameof(eventId));
            string key = variant != null ? d.Id + "." + variant.ToLowerInvariant() : d.Id;
            string bankKey = "mod:" + key;
            mount.I.SoundsRig.SetOverride(key, sound.ToLine(key, bankKey, ownerId));
            return new Override(mount.I, key, "gk2.mod." + ownerId + "." + bankKey);
        }

        private sealed class Override : IDisposable
        {
            private readonly GK2DonkeyAPI.Mount.Instance i;
            private readonly string key, bankId;
            private bool done;

            internal Override(GK2DonkeyAPI.Mount.Instance i, string key, string bankId)
            {
                this.i = i;
                this.key = key;
                this.bankId = bankId;
            }

            public void Dispose()
            {
                if (done) return;
                done = true;
                try { i.SoundsRig?.SetOverride(key, null); } catch { }
                try { SoundBank.Unregister(bankId); } catch { }
            }
        }

        // After an event played on an instance (through its rig: the ride, a standing one, a mod's Play).
        public event Action<MountHandle, string> Played;

        private static bool playedWarned;

        internal void RaisePlayed(MountHandle h, string eventId)
        {
            if (Played == null || h == null) return;
            try
            {
                Played(h, eventId);
            }
            catch (Exception e)
            {
                if (!playedWarned)
                {
                    playedWarned = true;
                    Plugin.Log.LogWarning("Sounds: a Played handler threw: " + e.Message);
                }
            }
        }
    }
}
