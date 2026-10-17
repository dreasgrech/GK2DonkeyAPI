using System;
using System.Collections.Generic;

namespace GK2DonkeyAPI.Api
{
    // Mount kinds (docs/mounts-design.md): what the player rides as a creature type (the donkey, the giant rat, the
    // dragon, ...), against a skin, which is how it is drawn (the red and the blue dragon are two skins of the dragon
    // kind). A kind owns the rider's seat for it (values for existing tuning keys, per style), whether it pulls the
    // cart and has foals, a default effect, and its abilities (MountAbility: code the API calls at the instance's
    // lifecycle points). A skin names its kind (MountSkinDef.Mount, a pack's `mount =`; the default is the donkey);
    // an instance's kind is its skin's (MountHandle.Kind), so a player picks a skin and the kind follows.
    //
    // Three ways to make a kind: here (Register, with abilities), a pack's [Mount] section in skin.cfg (no code; the
    // first declaration of an id wins and later skins join it), or none: a skin of the donkey kind.
    public sealed class MountKinds
    {
        public const string DonkeyId = MountLooks.DonkeyId;

        internal MountKinds()
        {
        }

        // Every kind: the donkey first, then the rest by name.
        public IReadOnlyList<MountKindInfo> Kinds
        {
            get
            {
                var list = new List<MountKindInfo>();
                foreach (GK2DonkeyAPI.Kinds.KindDef d in GK2DonkeyAPI.Kinds.All) list.Add(new MountKindInfo(d));
                list.Sort((a, b) => a.Id == DonkeyId ? -1 : b.Id == DonkeyId ? 1 : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }

        public MountKindInfo Find(string id)
        {
            GK2DonkeyAPI.Kinds.KindDef d = GK2DonkeyAPI.Kinds.Find(id);
            return d == null ? null : new MountKindInfo(d);
        }

        // Raised after a kind is registered, declared by a pack or withdrawn.
        public event Action KindsChanged;

        internal void RaiseChanged()
        {
            Action h = KindsChanged;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Kinds: a KindsChanged handler threw: " + e.Message);
                }
            }
        }

        // A kind of your own. The id must be new (or yours already: then it replaces, and every instance of it
        // re-applies). Dispose to withdraw: its skins fall back to the donkey kind.
        public IDisposable Register(string ownerId, MountKindDef def)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            if (def == null) throw new ArgumentNullException(nameof(def));
            var d = new GK2DonkeyAPI.Kinds.KindDef
            {
                Id = def.Id, Name = string.IsNullOrEmpty(def.Name) ? def.Id : def.Name, Author = def.Author, Version = def.Version, Owner = ownerId,
                Cart = def.Cart, Foals = def.Foals, FoalScale = Math.Max(0.2f, Math.Min(1f, def.FoalScale)), Effect = MountEffects.IsNone(def.Effect) ? null : def.Effect.Trim(), EffectWhen = def.EffectWhen,
                Tuning = Copy(def.Tuning), TuningRide = Copy(def.TuningRide), TuningCart = Copy(def.TuningCart),
                Abilities = def.Abilities != null ? def.Abilities.ToArray() : Array.Empty<MountAbility>(),
            };
            foreach (MountAbility a in d.Abilities)
            {
                if (a == null || string.IsNullOrEmpty(a.Id)) throw new ArgumentException($"Mount kind '{def.Id}': every ability needs an Id.", nameof(def));
            }
            GK2DonkeyAPI.Kinds.Register(d);
            return new Handle(d.Id, ownerId);
        }

        public bool Unregister(string ownerId, string id)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            return GK2DonkeyAPI.Kinds.Unregister(id, ownerId);
        }

        private static Dictionary<string, object> Copy(Dictionary<string, object> d)
        {
            return d != null && d.Count > 0 ? new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase) : null;
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
                GK2DonkeyAPI.Kinds.Unregister(id, owner);
            }
        }
    }

    public sealed class MountKindDef
    {
        public string Id;
        public string Name;
        public string Author;
        public string Version;
        // May pull the cart (false: the cart style, Hitch and MountCompanion.Use(Cart) are refused for this kind).
        public bool Cart = true;
        // Foals (babies) may be drawn as it.
        public bool Foals = true;
        // A baby's size as a fraction of the grown one (0.2 to 1; the donkey's foals are 0.6): a mount drawn three
        // times its creature's size (Gerry) sets 0.33 so its babies are the creature's own size.
        public float FoalScale = 0.6f;
        // The effect an instance of this kind shows when its own Effect key is MountEffects.KindId ("mount"): an effect
        // id, or null for none; and when.
        public string Effect;
        public MountEffectWhen EffectWhen = MountEffectWhen.Always;
        // Values for existing tuning keys (SeatAdjustPx, RiderOffsetPx, LegAdjustYPx, SpeedMultiplier, ...), typed as the
        // key is: set on every instance of this kind as overrides owned by the skin system while it is of this kind.
        // Tuning: both styles; TuningRide / TuningCart: one style, over Tuning. A skin's own Tuning refines them.
        public Dictionary<string, object> Tuning;
        public Dictionary<string, object> TuningRide;
        public Dictionary<string, object> TuningCart;
        // Code the API calls for every instance of this kind: see MountAbility.
        public List<MountAbility> Abilities = new List<MountAbility>();
    }

    // Behaviour a mod attaches to a kind. One object per kind, shared by every instance of it: keep per-instance state
    // in a dictionary keyed by the handle, or in handle.SetData. The API calls the hooks for instances that have a
    // handle (not the settings picture's paper donkey, not a plain summon without one). An ability that throws is
    // switched off for the session on that instance, with one warning. Keys, settings rows, sounds and effects are the
    // mod's own calls from these hooks (the settings page, Mounts.Effects, ...).
    public abstract class MountAbility
    {
        public abstract string Id { get; }

        // Tick runs only while the instance is ridden, unless this is true (then every frame it has a view): keep it
        // false unless the ability needs to act on a standing creature.
        public virtual bool TicksWhileStanding => false;

        // The instance became this kind (spawned, loaded, or its skin changed to one of this kind).
        public virtual void Attached(MountHandle mount)
        {
        }

        // It stopped being this kind (its skin changed), or it is removed.
        public virtual void Detached(MountHandle mount)
        {
        }

        // The keeper got on (or switched style: Dismounted then Mounted with the new style) / got off.
        public virtual void Mounted(MountHandle mount, MountStyle style)
        {
        }

        public virtual void Dismounted(MountHandle mount)
        {
        }

        // Every frame (LateUpdate) while ridden, or while standing too with TicksWhileStanding; dt is the frame's time.
        public virtual void Tick(MountHandle mount, float dt)
        {
        }
    }

    public sealed class MountKindInfo
    {
        internal MountKindInfo(GK2DonkeyAPI.Kinds.KindDef d)
        {
            Id = d.Id;
            Name = d.Label;
            Author = d.Author ?? "";
            Version = d.Version ?? "";
            Owner = d.Owner ?? "";
            IsBuiltIn = d.BuiltIn;
            FromPack = d.FromPack;
            Cart = d.Cart;
            Foals = d.Foals;
            FoalScale = d.FoalScale;
            Effect = d.Effect ?? "";
            Abilities = d.Abilities.Length;
            var skins = new List<string>();
            if (d == GK2DonkeyAPI.Kinds.Donkey) skins.Add(MountLooks.DonkeyId);
            foreach (GK2DonkeyAPI.Looks.SkinDef s in GK2DonkeyAPI.Kinds.SkinsOf(d)) skins.Add(s.Id);
            Skins = skins;
        }

        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        public string Owner { get; }
        public bool IsBuiltIn { get; }
        public bool FromPack { get; }
        public bool Cart { get; }
        public bool Foals { get; }
        // A baby's size as a fraction of the grown one.
        public float FoalScale { get; }
        public string Effect { get; }
        public int Abilities { get; }
        // The ids of its skins (the donkey kind's first is "donkey", the game's own art).
        public IReadOnlyList<string> Skins { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
