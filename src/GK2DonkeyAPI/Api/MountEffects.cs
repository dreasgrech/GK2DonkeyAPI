using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // Particle effects (docs/api.md "Effects"): the effects mods register here (for one, the game's lamp
    // fire on a ring round the mount, with a trail). Nothing picks an effect for a mount in this version: the player's
    // rows, the Effect tuning keys, MountCompanion.Effect, Mounts.EffectSettings and the packs' effect = went on
    // 2026-10-25 (the user: "remove all the stuff we're exposing regarding the effects"); how a skin comes with one is
    // being designed. So a registered effect is kept, listed and ready, and shows nowhere yet.
    //
    // An effect is a MountEffectDef: parts, each a source of particles (IMountEffectSource: a template GameObject of
    // particle systems, the game's own or your prefab's) with emitters placed on a ring round the body, at the body,
    // or as a trail left behind while the donkey moves. The API spawns and follows them, scales them for foals, and
    // removes them when the view or the donkey goes. Dispose the handle to withdraw the effect.
    public sealed class MountEffects
    {
        internal MountEffects()
        {
        }

        // Every effect: the built-in ones first, then mods', each group by name.
        public IReadOnlyList<MountEffectInfo> Effects
        {
            get
            {
                var list = new List<MountEffectInfo>();
                foreach (GK2DonkeyAPI.Effects.EffectDef d in GK2DonkeyAPI.Effects.All) list.Add(new MountEffectInfo(d));
                list.Sort((a, b) => a.IsBuiltIn != b.IsBuiltIn ? (a.IsBuiltIn ? -1 : 1) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }

        public MountEffectInfo Find(string id)
        {
            GK2DonkeyAPI.Effects.EffectDef d = GK2DonkeyAPI.Effects.Find(id);
            return d == null ? null : new MountEffectInfo(d);
        }

        public event Action EffectsChanged;

        internal void RaiseChanged()
        {
            Action h = EffectsChanged;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Effects: an EffectsChanged handler of {Said.ModOf(d)} threw: {e.Message}");
                }
            }
        }

        // An effect of your own, its id "<mod>.<name>". Yours already: it replaces. Another mod's of the same id: the
        // newer version wins, the other is left out with a warning (never a throw; its handle then does nothing).
        // Dispose to withdraw.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // the calling mod's assembly (Mounts.NoteOwner)
        public IDisposable Register(string ownerId, MountEffectDef def)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            Mounts.NoteOwner(ownerId, Assembly.GetCallingAssembly());
            if (def == null) throw new ArgumentNullException(nameof(def));
            var d = new GK2DonkeyAPI.Effects.EffectDef { Id = def.Id, Name = string.IsNullOrEmpty(def.Name) ? def.Id : def.Name, Author = def.Author, Version = def.Version, Owner = ownerId, Foals = def.Foals, Show = def.Show };
            foreach (MountEffectPart p in def.Parts)
            {
                if (p == null || p.Source == null) throw new ArgumentException($"Effect '{def.Id}': every part needs a source.", nameof(def));
                var part = new GK2DonkeyAPI.Effects.EffectDef.Part { Source = p.Source };
                part.Emitters.AddRange(p.Emitters);
                d.Parts.Add(part);
            }
            Mounts.RequireStarted(ownerId, "the effect", def.Id);
            GK2DonkeyAPI.Effects.Register(d);
            return new Handle(d.Id, ownerId);
        }

        public bool Unregister(string ownerId, string id)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            return GK2DonkeyAPI.Effects.Unregister(id, ownerId);
        }

        // A source for the game's own particles: a particle system drawn with that material, copied, its material held
        // through Addressables by its key so the game can't unload it under the copies. The lamp flame is
        // ("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat"); its sparks "sparks_fx".
        public IMountEffectSource GameParticles(string materialName, string addressableMaterialKey)
        {
            if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(addressableMaterialKey)) throw new ArgumentException("A material name and its Addressables key are required.");
            return new GK2DonkeyAPI.Effects.GameParticles(materialName, addressableMaterialKey);
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
                GK2DonkeyAPI.Effects.Unregister(id, owner);
            }
        }
    }

    public enum MountEffectPlacement
    {
        Ring,   // Count emitters on a ring of Radius round the body, moving with it
        Body,   // one emitter at the body, moving with it
        Trail,  // left behind every Every units while the donkey moves: emits for Burst seconds, then burns out
    }

    public sealed class MountEffectEmitter
    {
        public MountEffectPlacement Placement = MountEffectPlacement.Ring;
        public int Count = 1;
        public float Radius = 0.8f;   // Ring: the ring's radius, in world units (wider with the cart)
        public float Angle;           // Ring: where the first emitter sits, in degrees
        public float Size = 1f;       // a multiple of the source's own size
        public float Height;          // above the ground
        public Vector3 Offset;        // from where the placement puts it (x along the ground, y up, z into the ground plane)
        public float Every = 0.5f;    // Trail: units walked between two
        public float Burst = 0.4f;    // Trail: seconds each one emits
    }

    public sealed class MountEffectPart
    {
        public IMountEffectSource Source;
        public List<MountEffectEmitter> Emitters = new List<MountEffectEmitter>();
    }

    public sealed class MountEffectDef
    {
        public string Id;
        public string Name;
        public string Author;
        public string Version;
        public bool Foals = true;
        // Your own condition, asked every tick for each donkey showing the effect, on top of the built-in ones (indoors,
        // fights, foals): false hides it. Keep it cheap. Null: none.
        public Func<MountHandle, bool> Show;
        public List<MountEffectPart> Parts = new List<MountEffectPart>();
    }

    // Where an effect's particles come from: MakeTemplate returns a GameObject holding the particle systems (made
    // inactive or active; the API keeps it as a template, never shown, and copies it per emitter). Return null if it
    // can't be made now. Release is called when the template is dropped: let go of what you held for it.
    public interface IMountEffectSource
    {
        GameObject MakeTemplate();
        void Release();
    }

    public sealed class MountEffectInfo
    {
        internal MountEffectInfo(GK2DonkeyAPI.Effects.EffectDef d)
        {
            Id = d.Id;
            Name = d.Name ?? d.Id;
            Author = d.Author ?? "";
            Version = d.Version ?? "";
            Owner = d.Owner ?? "";
            IsBuiltIn = d.BuiltIn;
            Foals = d.Foals;
            Parts = d.Parts.Count;
        }

        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        public string Owner { get; }
        public bool IsBuiltIn { get; }
        public bool Foals { get; }
        public int Parts { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
